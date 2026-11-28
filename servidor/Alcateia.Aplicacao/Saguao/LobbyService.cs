using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Contas;
using Alcateia.Aplicacao.Frota;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Conversas;
using Alcateia.Dominio.Fila;
using Alcateia.Dominio.Frota;
using Alcateia.Dominio.Partidas;
using Alcateia.Dominio.Salas;

namespace Alcateia.Aplicacao.Saguao;

/// <summary>
/// O saguão: as salas, quem está em cada uma e o chat.
///
/// Mensagens em JSON com um campo <c>t</c> dizendo o que são. O cliente
/// manda <c>rooms.list</c>, <c>room.create</c>, <c>room.join</c>,
/// <c>room.leave</c>, <c>room.team</c>, <c>room.ready</c>,
/// <c>room.settings</c>, <c>room.bot.add</c>, <c>room.bot.remove</c>,
/// <c>room.start</c>, <c>room.enter</c>, <c>chat</c> e <c>ping</c>; o servidor responde com
/// <c>hello</c>, <c>rooms</c>, <c>room</c>, <c>left</c>, <c>chat</c>,
/// <c>match.start</c>, <c>match.end</c>, <c>pong</c> e <c>error</c>. O
/// formato de cada uma está em servidor/LEIA-ME.md.
///
/// Os amigos (lista, pedidos, presença e convites para a sala) ficam na
/// outra parte da classe, <c>LobbyService.Friends.cs</c>; os grupos (amigos
/// que entram juntos nas salas, no mesmo time), em
/// <c>LobbyService.Groups.cs</c>; o jogo rápido (a fila que junta os
/// jogadores por habilidade e monta os times), em <c>LobbyService.Queue.cs</c>;
/// as mensagens diretas entre amigos, em <c>LobbyService.Direct.cs</c>.
///
/// Cada conta tem até duas ligações: a do jogo (<see cref="_sessions"/>) e a
/// do cliente de instalação (<see cref="_apps"/>), que só vê e mexe nos
/// amigos e conversa com eles — não entra em sala, grupo nem chat da sala.
///
/// As salas vivem em memória: sala é coisa de agora, e se o servidor
/// reiniciar os jogadores voltam para a lista. As regras de cada sala são
/// do domínio (<see cref="Room"/>); aqui ficam quem está ligado, a trava
/// e quem precisa saber do quê. A conexão em si (o WebSocket) é de fora:
/// chega como <see cref="ILobbyConnection"/>.
/// </summary>
public sealed partial class LobbyService : IDisposable
{
	public const int MaxChat = ChatRules.RoomMax;

	/// <summary>Salas abertas ao mesmo tempo, no máximo (decisão do Jhon, 13/11).</summary>
	public const int DefaultMaxRooms = 1000;

	/// <summary>Quantas salas a lista mostra: as que dá para entrar primeiro.
	/// Com mil salas, mandar todas para cada um a cada mudança seria o
	/// servidor inteiro ocupado em repetir a mesma lista.</summary>
	public const int MaxListed = 200;

	/// <summary>Um jogador ligado no saguão.</summary>
	public sealed class Session
	{
		public Account Account { get; init; }
		internal ILobbyConnection Connection;
		internal Room Room;
		internal long Xp;
		internal readonly Queue<DateTime> Chats = new();
		/// <summary>A foto de perfil que os outros veem (<see cref="PhotoRule"/>).</summary>
		internal string Photo = PhotoRule.Default;
		/// <summary>As fotos da loja que a conta tem (lidas ao entrar e de novo se pedir uma que não estava).</summary>
		internal HashSet<string> Photos = new();
		/// <summary>Os itens da conta (menos as fotos): vão na escalação da partida,
		/// e o servidor de partida só aceita levar a arma que está aqui ou é de série.
		/// Lidos ao entrar e de novo antes de começar a partida.</summary>
		internal HashSet<string> Items = new();

		// ---- amigos (LobbyService.Friends.cs)
		/// <summary>A lista de amigos e pedidos da conta, lida do banco ao
		/// entrar e de novo a cada mudança.</summary>
		internal Dictionary<long, FriendLink> Links = new();
		/// <summary>Quantos de <see cref="Links"/> são amigos. Zero: a conta
		/// não custa nada à presença.</summary>
		internal int FriendCount;
		/// <summary>A presença como os amigos a viram por último.</summary>
		internal string Presence;
		/// <summary>A conexão acabou (caiu, foi derrubada ou trocada por outra).</summary>
		internal bool Gone;
		/// <summary>Convites para sala recebidos, por quem convidou.</summary>
		internal readonly Dictionary<long, (int Room, DateTime At)> Invites = new();
		internal readonly Queue<DateTime> FriendAdds = new();
		internal readonly Queue<DateTime> FriendWrites = new();
		internal readonly Queue<DateTime> InvitesSent = new();

		// ---- mensagens diretas (LobbyService.Direct.cs)
		internal readonly Queue<DateTime> DirectBurst = new();
		internal readonly Queue<DateTime> DirectMinute = new();

		// ---- grupo (LobbyService.Groups.cs)
		/// <summary>O grupo em que a conta está, ou null.</summary>
		internal Group Group;
		/// <summary>Convites para grupo recebidos: de quem e quando.</summary>
		internal readonly Dictionary<long, DateTime> GroupInvites = new();
		internal readonly Queue<DateTime> GroupInvitesSent = new();

		// ---- jogo rápido (LobbyService.Queue.cs)
		/// <summary>A nota de habilidade da conta, lida ao entrar e acertada no
		/// fim de cada partida de fila.</summary>
		internal Skill Skill = new(SkillRules.Start, 0);
		/// <summary>A entrada na fila (a dele, ou a do grupo dele), ou null.</summary>
		internal QueueTicket Ticket;
		internal readonly Queue<DateTime> QueueJoins = new();

		/// <summary>A ligação é do cliente de instalação, não do jogo: vê os
		/// amigos, pede e aceita amizade, e aparece para eles como "no
		/// aplicativo". Conversa com os amigos (mensagens diretas); não entra
		/// em sala, grupo nem chat da sala.</summary>
		internal bool App;
	}

	private readonly FleetService _fleet;
	private readonly AccountService _accounts;
	private readonly IClock _clock;
	private readonly TimeSpan _ticketLife;
	private readonly object _lock = new();
	private readonly Dictionary<long, Session> _sessions = new();
	/// <summary>As ligações dos clientes de instalação, por conta: à parte das
	/// do jogo, para abrir o jogo não derrubar o aplicativo (nem o contrário).</summary>
	private readonly Dictionary<long, Session> _apps = new();
	private readonly Dictionary<int, Room> _rooms = new();
	private int _nextRoom = 1;

	// A lista de salas vai para quem está olhando para ela junta, de tempos
	// em tempos (ListEvery), e não a cada mudança: com milhares no saguão,
	// cada sala criada ou cheia seria uma lista inteira para cada um.
	private readonly System.Threading.Timer _listTimer;
	private bool _listChanged;
	private readonly HashSet<int> _closing = new();   // salas com a partida quase acabando, como a lista mostrou
	private readonly int _maxRooms;

	/// <summary><paramref name="listEvery"/>: de quanto em quanto tempo a
	/// lista de salas que mudou vai para quem está no saguão.</summary>
	public LobbyService(FleetService fleet, AccountService accounts, IClock clock, TimeSpan ticketLife,
		TimeSpan? listEvery = null, int maxRooms = DefaultMaxRooms)
	{
		_maxRooms = maxRooms;
		_fleet = fleet;
		_accounts = accounts;
		_clock = clock;
		_ticketLife = ticketLife;
		_fleet.Lost += OnMatchLost;

		TimeSpan every = listEvery ?? TimeSpan.FromMilliseconds(500);
		_listTimer = new System.Threading.Timer(_ => _ = FlushList(), null, every, every);
	}

	public void Dispose() => _listTimer.Dispose();

	/// <summary>Manda a lista de salas, se mudou, para quem está no saguão.</summary>
	public async Task FlushList()
	{
		var outbox = new Outbox();
		lock (_lock)
		{
			// A partida de uma sala passou a estar quase acabando (o servidor de
			// partida conta a cada batida): a lista e quem está na sala de fora
			// dela ficam sabendo que não dá mais para entrar.
			foreach (Room room in _rooms.Values)
			{
				bool closing = Closing(room);
				if (closing == _closing.Contains(room.Id)) continue;
				if (closing) _closing.Add(room.Id); else _closing.Remove(room.Id);
				RoomState(room, outbox);
				_listChanged = true;
			}
			_closing.RemoveWhere(id => !_rooms.ContainsKey(id));

			if (_listChanged)
			{
				_listChanged = false;
				outbox.To(_sessions.Values.Where(s => s.Room == null), RoomsMessage());
			}
			TickQueue(outbox);
			FlushFriends(outbox);
		}
		await outbox.Deliver();
	}

	public int Online { get { lock (_lock) return _sessions.Count; } }

	/// <summary>O mapa pode ser jogado? O painel desliga mapa; quem decide
	/// é quem conhece os mapas (os dados do jogo). Por padrão, todos.</summary>
	public Func<string, bool> MapAllowed { get; set; } = _ => true;

	/// <summary>Os itens de uma conta, para conferir as fotos de perfil
	/// compradas. Sem isso (testes), só as grátis valem.</summary>
	public Func<long, Task<IReadOnlyList<Alcateia.Dominio.Loja.OwnedItem>>> Inventory { get; set; }

	public bool IsOnline(long accountId)
	{
		lock (_lock) return _sessions.ContainsKey(accountId);
	}

	/// <summary>Salas abertas e quantas estão em partida, para o painel.</summary>
	public (int Rooms, int Playing) RoomCount()
	{
		lock (_lock) return (_rooms.Count, _rooms.Values.Count(r => r.State != Dominio.Salas.RoomState.Waiting));
	}

	/// <summary>
	/// Avisa o jogo de uma conta que ela mudou por fora dele — o painel deu
	/// moeda, item ou nível (Jhon, 27/11: "o nível e as moedas devem entrar em
	/// tempo real para o jogador"). Vai só o aviso (<c>conta</c>); o jogo relê
	/// a conta pela API, como faz ao abrir a loja. Falso se ela não estava ligada.
	///
	/// O saguão guarda o XP de cada um (a patente que a sala, os amigos e o
	/// grupo mostram), lido ao ligar: relê aqui também, e a sala em que ela
	/// está manda o estado novo para todos nela (Jhon, 27/11: "quando subi
	/// nível pelo painel, atualizou em tudo, menos na sala. tive que sair e
	/// entrar").
	/// </summary>
	public async Task<bool> AccountChanged(long accountId)
	{
		if (!IsOnline(accountId)) return false;
		long? xp = null;
		try
		{
			xp = (await _accounts.Profile(accountId)).Xp;
		}
		catch (Failure)
		{
			// Sem o perfil, vai só o aviso: o jogo relê o dele pela API.
		}

		var outbox = new Outbox();
		lock (_lock)
		{
			outbox.To(Live(accountId), new JsonObject { ["t"] = "conta" });
			if (!_sessions.TryGetValue(accountId, out Session session)) return false;
			if (xp is long now) SetXp(session, now, outbox);
			FlushFriends(outbox);
		}
		await outbox.Deliver();
		return true;
	}

	/// <summary>O XP da conta mudou por fora de uma partida (o painel): a
	/// sala dela, os amigos e o grupo passam a ver a patente nova.</summary>
	private void SetXp(Session s, long xp, Outbox outbox)
	{
		if (s.Xp == xp) return;
		s.Xp = xp;
		if (_apps.TryGetValue(s.Account.Id, out Session app)) app.Xp = xp;
		if (s.Room?.Members.FirstOrDefault(m => m.Account.Id == s.Account.Id) is RoomMember member)
		{
			member.Xp = xp;
			RoomState(s.Room, outbox);
		}
		NotifyFriends(s, outbox);
		if (s.Group != null) outbox.Groups.Add(s.Group);
	}

	/// <summary>Derruba a conexão de uma conta (banimento, ou a pedido do
	/// painel). Falso se ela não estava ligada.</summary>
	public async Task<bool> Kick(long accountId, string why)
	{
		var outbox = new Outbox();
		Session session, app;
		lock (_lock)
		{
			// O aplicativo da conta cai junto: banido não fica olhando os amigos.
			if (_apps.Remove(accountId, out app)) app.Gone = true;
			if (_sessions.TryGetValue(accountId, out session))
			{
				session.Gone = true;
				QueueLeave(session, outbox, "Um jogador do grupo caiu: a busca parou.");
				LeaveGroup(session, outbox);
				Leave(session, outbox);
				_sessions.Remove(accountId);
			}
			if ((session ?? app) is Session gone) NotifyFriends(gone, outbox);
			FlushFriends(outbox);
		}
		await outbox.Deliver();
		if (app != null) _ = app.Connection.Close(why);
		if (session != null) _ = session.Connection.Close(why);
		return session != null;
	}

	private void RequireMap(JsonObject settings)
	{
		string map = settings?["map"]?.GetValue<string>();
		if (map != null && !MapAllowed(map))
			throw new Failure(FailureKind.Conflict, "mapa_desligado", "Esse mapa está fora de rotação no momento.");
	}

	/// <summary>A lista de salas, como vai para o cliente (sem senha).</summary>
	public JsonArray Rooms()
	{
		lock (_lock) return RoomList();
	}

	// ------------------------------------------------------------ a conexão

	/// <summary>Um jogador chegou. Uma conexão por conta e por programa: a
	/// nova derruba a velha. <paramref name="app"/>: é o cliente de instalação
	/// (<see cref="ConnectApp"/>).</summary>
	public async Task<Session> Connect(Account account, ILobbyConnection connection, bool app = false)
	{
		if (app) return await ConnectApp(account, connection);

		long xp = 0;
		var skill = new Skill(SkillRules.Start, 0);
		try
		{
			Profile profile = await _accounts.Profile(account.Id);
			xp = profile.Xp;
			skill = SkillOf(profile);
		}
		catch (Failure)
		{
			// Conta sem perfil ainda entra; só aparece sem patente.
		}

		var session = new Session { Account = account, Connection = connection, Xp = xp, Skill = skill, Photos = await OwnedPhotos(account.Id), Items = await OwnedItems(account.Id) };
		SetLinks(session, await LoadLinks(account.Id));
		var outbox = new Outbox();
		Session old;
		lock (_lock)
		{
			_sessions.TryGetValue(account.Id, out old);
			if (old != null)
			{
				old.Gone = true;
				QueueLeave(old, outbox, "A conta abriu o jogo em outro lugar: a busca parou.");
				LeaveGroup(old, outbox);
				Leave(old, outbox);
			}
			_sessions[account.Id] = session;
			outbox.To(session, Hello(account));
			outbox.To(session, RoomsMessage());
			// A lista de amigos vem sem pedir, e os amigos ligados ficam sabendo que chegou.
			outbox.Dirty.Add(session);
			SyncPresence(session, outbox);
			FlushFriends(outbox);
		}

		await outbox.Deliver();
		await PushUnread(session);
		if (old != null) _ = old.Connection.Close("outra conexão da mesma conta");
		return session;
	}

	/// <summary>
	/// O cliente de instalação de uma conta chegou (Jhon, 27/11: "cliente de
	/// instalação igual o battle.net [...] fazer login, adicionar novos
	/// amigos"). Recebe o olá e a lista de amigos, e daí em diante o que muda
	/// nela; os amigos passam a vê-lo "no aplicativo" enquanto o jogo dele
	/// estiver fechado. Não mexe na ligação do jogo.
	/// </summary>
	private async Task<Session> ConnectApp(Account account, ILobbyConnection connection)
	{
		long xp = 0;
		try
		{
			xp = (await _accounts.Profile(account.Id)).Xp;
		}
		catch (Failure)
		{
		}

		var session = new Session { Account = account, Connection = connection, Xp = xp, App = true };
		SetLinks(session, await LoadLinks(account.Id));
		var outbox = new Outbox();
		Session old;
		lock (_lock)
		{
			if (_apps.TryGetValue(account.Id, out old)) old.Gone = true;
			_apps[account.Id] = session;
			outbox.To(session, Hello(account));
			outbox.Dirty.Add(session);
			// Sem o jogo aberto, os amigos veem a conta sair de "fora do ar".
			if (old == null && !_sessions.ContainsKey(account.Id)) NotifyFriends(session, outbox);
			FlushFriends(outbox);
		}

		await outbox.Deliver();
		await PushUnread(session);
		if (old != null) _ = old.Connection.Close("outro aplicativo da mesma conta");
		return session;
	}

	private JsonObject Hello(Account account) => new()
	{
		["t"] = "hello", ["id"] = account.Id, ["name"] = account.Name,
		["tag"] = AccountRules.TagText(account.Tag), ["online"] = _sessions.Count
	};

	/// <summary>As ligações vivas de uma conta: a do jogo e a do aplicativo.</summary>
	private IEnumerable<Session> Live(long accountId)
	{
		if (_sessions.TryGetValue(accountId, out Session game)) yield return game;
		if (_apps.TryGetValue(accountId, out Session app)) yield return app;
	}

	/// <summary>Esta ainda é a ligação que vale para a conta (não foi trocada nem caiu).</summary>
	private bool IsCurrent(Session s) =>
		!s.Gone && (s.App ? _apps : _sessions).TryGetValue(s.Account.Id, out Session current) && current == s;

	/// <summary>O que o cliente de instalação pode pedir: os amigos e as conversas com eles.</summary>
	private static bool AllowedInApp(string type) => type is "ping" or "friends.list" || IsFriendWrite(type) || IsDirect(type);

	/// <summary>Uma mensagem do jogador. Pedido que não dá vira um <c>error</c> só para ele.</summary>
	public async Task Handle(Session session, string text)
	{
		var outbox = new Outbox();
		try
		{
			if (JsonNode.Parse(text) is not JsonObject message)
				throw new Failure(FailureKind.Invalid, "mensagem_invalida", "Mensagem inválida.");
			string type = message["t"]?.GetValue<string>() ?? "";
			if (session.App && !AllowedInApp(type))
				throw new Failure(FailureKind.Conflict, "so_no_jogo", "Isso só dá para fazer de dentro do jogo.");
			// O que grava amizade passa pelo banco, fora da trava.
			if (IsFriendWrite(type)) await FriendWrite(session, type, message, outbox);
			// As conversas também: gravar e ler a mensagem vão ao banco.
			else if (IsDirect(type)) await Direct(session, type, message, outbox);
			else
			{
				await RefreshItemsFor(session, type);
				lock (_lock)
				{
					// Mesmo que o pedido não dê, o que mudou antes dele (saiu da
					// sala para entrar noutra, por exemplo) chega aos amigos.
					try { Dispatch(session, message, outbox); }
					finally { FlushFriends(outbox); }
				}
			}
		}
		catch (Failure e)
		{
			outbox.To(session, ErrorMessage(e.Code, e.Message));
		}
		catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
		{
			outbox.To(session, ErrorMessage("mensagem_invalida", "Mensagem inválida."));
		}
		await outbox.Deliver();
	}

	/// <summary>A conexão fechou (ou caiu): sai como quem fechou.</summary>
	public async Task Disconnect(Session session)
	{
		var outbox = new Outbox();
		lock (_lock)
		{
			if (IsCurrent(session))
			{
				session.Gone = true;
				if (session.App)
				{
					_apps.Remove(session.Account.Id);
					// Com o jogo aberto, fechar o aplicativo não muda o que os amigos veem.
					if (!_sessions.ContainsKey(session.Account.Id)) NotifyFriends(session, outbox);
				}
				else
				{
					QueueLeave(session, outbox, "Um jogador do grupo caiu: a busca parou.");
					LeaveGroup(session, outbox);
					Leave(session, outbox);
					_sessions.Remove(session.Account.Id);
					NotifyFriends(session, outbox);
				}
				FlushFriends(outbox);
			}
		}
		await outbox.Deliver();
	}

	// ------------------------------------------------------------ partidas

	/// <summary>A partida de uma sala terminou com resultado: ela volta a
	/// esperar e cada um recebe o que ganhou.</summary>
	public async Task MatchEnded(SettledMatch match)
	{
		var summary = new JsonObject
		{
			["matchKey"] = match.Assignment.MatchKey, ["winner"] = match.Winner,
			["scoreBlue"] = match.ScoreBlue, ["scoreOrange"] = match.ScoreOrange
		};
		var gains = match.Lines.ToDictionary(l => l.Seat.AccountId, Gain);

		var outbox = new Outbox();
		IReadOnlyList<SkillRules.Change> rated = null;
		lock (_lock)
		{
			Room room = _rooms.Values.FirstOrDefault(r => r.MatchKey == match.Assignment.MatchKey);
			// Quem ainda estava jogando quando acabou (o resto largou a partida).
			HashSet<long> present = room?.Members.Where(m => m.InMatch).Select(m => m.Account.Id).ToHashSet();
			if (room != null && room.EndMatch(match.Assignment.MatchKey))
			{
				if (room.Quick) rated = RateQuick(match, present);
				foreach (RoomMember m in room.Members)
				{
					var end = new JsonObject { ["t"] = "match.end", ["match"] = summary.DeepClone() };
					if (gains.TryGetValue(m.Account.Id, out JsonObject gain))
					{
						end["you"] = gain.DeepClone();
						m.Xp += gain["xp"].GetValue<long>();
						if (_sessions.TryGetValue(m.Account.Id, out Session s)) s.Xp = m.Xp;
					}
					outbox.To(SessionOf(m), end);
				}
				if (room.Quick) Dissolve(room, outbox);
				else RoomState(room, outbox);
				ListChanged();
			}
			FlushFriends(outbox);
		}
		await outbox.Deliver();
		await SaveRated(rated);
	}

	/// <summary>O que um jogador levou de uma partida, como vai para o cliente.</summary>
	public static JsonObject Gain(SettledLine l) => new()
	{
		["xp"] = l.Gain.Xp, ["presas"] = l.Gain.Presas, ["won"] = l.Won, ["draw"] = l.Draw, ["mvp"] = l.Mvp,
		["kills"] = l.Kills, ["deaths"] = l.Deaths, ["assists"] = l.Assists
	};

	/// <summary>A partida acabou sem resultado: a sala volta a esperar e
	/// cada um sabe por quê.</summary>
	private void OnMatchLost(LostMatch lost)
	{
		var outbox = new Outbox();
		lock (_lock)
		{
			if (!_rooms.TryGetValue(lost.Assignment.RoomId, out Room room) || !room.EndMatch(lost.Assignment.MatchKey)) return;

			// Sala do jogo rápido não volta a esperar: acaba, e ninguém sobe nem desce de nota.
			string after = room.Quick ? "" : " A sala voltou a esperar.";
			JsonObject why = lost.Reason == MatchLoss.ServerDown
				? ErrorMessage("servidor_caiu", "O servidor da partida caiu." + after)
				: ErrorMessage("partida_encerrada", "A partida foi encerrada sem resultado." + after);
			foreach (RoomMember m in room.Members) outbox.To(SessionOf(m), why);
			_quick.Remove(lost.Assignment.MatchKey);
			if (room.Quick) Dissolve(room, outbox);
			else RoomState(room, outbox);
			ListChanged();
			FlushFriends(outbox);
		}
		_ = outbox.Deliver();
	}

	// ------------------------------------------------------------ mensagens

	private void Dispatch(Session c, JsonObject m, Outbox outbox)
	{
		string type = m["t"]?.GetValue<string>() ?? "";
		// Quem está na fila e resolve ir para uma sala sai da fila (a dele ou a do grupo).
		if (c.Ticket != null && type is "room.create" or "room.join" or "invite.accept")
			QueueLeave(c, outbox, "Você foi para uma sala: a busca parou.");

		switch (type)
		{
			case "ping":
				outbox.To(c, new JsonObject { ["t"] = "pong" });
				break;

			case "rooms.list":
				outbox.To(c, RoomsMessage());
				break;

			case "room.create":
				RequireMap(m["settings"] as JsonObject);
				Create(c, m["settings"] as JsonObject, outbox);
				break;

			case "room.join":
				Join(c, m["id"]?.GetValue<int>() ?? 0, m["password"]?.GetValue<string>() ?? "", outbox);
				break;

			case "room.leave":
				LeaveTogether(c, outbox);
				break;

			case "room.team":
				ChangeTeam(c, m["team"]?.GetValue<int>() ?? 0, outbox);
				break;

			case "room.ready":
				InRoom(c).SetReady(c.Account.Id, m["ready"]?.GetValue<bool>() ?? false);
				RoomState(c.Room, outbox);
				break;

			case "room.settings":
				RequireMap(m["settings"] as JsonObject);
				InRoom(c).ChangeSettings(c.Account.Id, m["settings"] as JsonObject);
				Changed(c.Room, outbox);
				break;

			case "room.bot.add":
				InRoom(c).AddBot(c.Account.Id, m["team"]?.GetValue<int>() ?? 0);
				Changed(c.Room, outbox);
				break;

			case "room.bot.remove":
				if (InRoom(c).RemoveBot(c.Account.Id, m["id"]?.GetValue<long>() ?? 0)) Changed(c.Room, outbox);
				break;

			case "room.start":
				Start(c, outbox);
				break;

			case "room.enter":
				Enter(c, outbox);
				break;

			case "room.restart":
				Restart(c, outbox);
				break;

			case "chat":
				Chat(c, m["text"]?.GetValue<string>() ?? "", m["team"]?.GetValue<bool>() ?? false, outbox);
				break;

			case "profile.photo":
				string photo = m["id"]?.GetValue<string>() ?? "";
				if (!PhotoRule.Exists(photo)) break;
				if (PhotoRule.Free.Contains(photo) || c.Photos.Contains(photo)) SetPhoto(c, photo, outbox);
				else _ = RecheckPhoto(c, photo);   // pode ter acabado de comprar
				break;

			case "friends.list":
				outbox.Dirty.Add(c);
				break;

			case "friend.invite":
				Invite(c, m["id"]?.GetValue<long>() ?? 0, outbox);
				break;

			case "invite.accept":
				AcceptInvite(c, m["from"]?.GetValue<long>() ?? 0, outbox);
				break;

			case "invite.decline":
				DeclineInvite(c, m["from"]?.GetValue<long>() ?? 0, outbox);
				break;

			case "queue.join":
				QueueJoin(c, outbox);
				break;

			case "queue.leave":
				QueueLeave(c, outbox, null);
				break;

			case "group.invite":
				GroupInvite(c, m["id"]?.GetValue<long>() ?? 0, outbox);
				break;

			case "group.accept":
				GroupAccept(c, m["from"]?.GetValue<long>() ?? 0, outbox);
				break;

			case "group.decline":
				GroupDecline(c, m["from"]?.GetValue<long>() ?? 0, outbox);
				break;

			case "group.leave":
				if (c.Group == null) throw GroupRules.NoGroup();
				LeaveGroup(c, outbox);
				break;

			case "group.kick":
				GroupKick(c, m["id"]?.GetValue<long>() ?? 0, outbox);
				break;

			case "group.lead":
				GroupPromote(c, m["id"]?.GetValue<long>() ?? 0, outbox);
				break;

			default:
				throw new Failure(FailureKind.Invalid, "mensagem_desconhecida", $"Não conheço '{type}'.");
		}
	}

	private void Create(Session c, JsonObject settings, Outbox outbox)
	{
		if (_rooms.Count >= _maxRooms) throw new Failure(FailureKind.Unavailable, "cheio", "O servidor está com salas demais.");
		if (c.Group != null)
		{
			CreateWithGroup(c, settings, outbox);
			return;
		}
		Leave(c, outbox);

		Room room = Room.Create(_nextRoom++, c.Account, c.Xp, settings);
		_rooms[room.Id] = room;
		c.Room = room;
		Changed(room, outbox);
	}

	private void Join(Session c, int id, string password, Outbox outbox, bool invited = false)
	{
		if (!_rooms.TryGetValue(id, out Room room)) throw new Failure(FailureKind.NotFound, "sala_sumiu", "Essa sala não existe mais.");
		if (c.Room == room) return;
		if (c.Group != null)
		{
			JoinWithGroup(c, room, password, invited, outbox);
			return;
		}

		// Com a partida em curso (Jhon, 24/11: "ao sair da partida, posso entrar
		// nela novamente caso tenha vaga"; 25/11: "ele entra na sala para poder
		// escolher o time"): entra na sala, de fora da partida, no time em que
		// jogou (se voltou e ainda cabe) ou no que dá para entrar; a partida é
		// com room.enter. Quase acabando, não entra ("não pode entrar em sala
		// cuja partida está quase acabando").
		bool playing = room.State == Dominio.Salas.RoomState.Playing;
		if (playing) RequireEnterable(room);
		int team = playing ? _fleet.SeatTeam(room.MatchKey, c.Account.Id) : 0;

		// A sala nova confere tudo antes: recusado, o jogador fica onde estava.
		room.Join(c.Account, c.Xp, password, team, invited);
		Leave(c, outbox);
		c.Room = room;
		Changed(room, outbox);
	}

	/// <summary>A partida da sala ainda recebe gente: existe na frota e não
	/// está quase acabando.</summary>
	private void RequireEnterable(Room room)
	{
		if (room.MatchAddress == null || _fleet.Find(room.MatchKey) == null)
			throw new Failure(FailureKind.Conflict, "em_partida", "A partida desta sala está acabando. Tente de novo em instantes.");
		if (Closing(room))
			throw new Failure(FailureKind.Conflict, "partida_acabando", "A partida está quase acabando. Espere a próxima.");
	}

	/// <summary>A partida da sala está quase acabando (<see cref="MatchProgress.NearEnd"/>).</summary>
	private bool Closing(Room room) =>
		room.State == Dominio.Salas.RoomState.Playing && (_fleet.Progress(room.MatchKey)?.NearEnd ?? false);

	/// <summary>
	/// Quem está na sala de fora da partida em curso entra nela, pelo time que
	/// escolheu (equilibrado: <see cref="Room.Enter"/>). A escalação ganha o
	/// lugar (ou o de antes, de quem volta, no time escolhido agora) e o
	/// jogador recebe o ingresso, marcado como entrada no meio.
	/// </summary>
	private void Enter(Session c, Outbox outbox)
	{
		Room room = InRoom(c);
		if (room.State != Dominio.Salas.RoomState.Playing)
			throw new Failure(FailureKind.Conflict, "sem_partida", "A sala não está em partida.");
		if (room.Seat(c.Account.Id).InMatch) return;
		RequireEnterable(room);

		room.Enter(c.Account.Id);
		RoomMember seat = room.Seat(c.Account.Id);
		if (!_fleet.AddSeat(room.MatchKey, new MatchSeat(c.Account.Id, c.Account.Name, seat.Team, 0, c.Items.ToList())))
		{
			room.Unenter(c.Account.Id);
			throw new Failure(FailureKind.Conflict, "em_partida", "A partida desta sala está acabando. Tente de novo em instantes.");
		}
		Changed(room, outbox);
		SendTicket(room, room.MatchKey, c, false, outbox, late: true);
	}

	/// <summary>Tira o jogador da sala em que está. Sala vazia some.</summary>
	private void Leave(Session c, Outbox outbox)
	{
		Room room = c.Room;
		if (room == null) return;

		room.Leave(c.Account.Id);
		c.Room = null;
		outbox.To(c, new JsonObject { ["t"] = "left", ["id"] = room.Id });
		if (!c.Gone) SyncPresence(c, outbox);

		if (room.IsEmpty)
		{
			// Sem ninguém na sala, a partida dela vai junto (Jhon, 24/11: "se não
			// tiver mais players na sala, o jogo é excluído"): sai da frota, e o
			// resultado, se chegar, é recusado como de partida desconhecida.
			if (room.State == Dominio.Salas.RoomState.Playing)
			{
				_fleet.Release(room.MatchKey);
				_quick.Remove(room.MatchKey);
				room.EndMatch(room.MatchKey);
			}
			_rooms.Remove(room.Id);
		}
		else RoomState(room, outbox);
		ListChanged();
	}

	private void Start(Session c, Outbox outbox)
	{
		Room room = InRoom(c);
		RequireMap(room.Settings);
		MatchAssignment assignment = room.Lineup(c.Account.Id, Guid.NewGuid().ToString("N"));
		AddItems(room, assignment);

		(string Address, int Port)? server = _fleet.Allocate(assignment);
		if (server == null) throw new Failure(FailureKind.Unavailable, "sem_servidor", "Não há servidor de partida livre agora. Tente de novo em instantes.");

		Launch(room, assignment, server.Value, false, outbox);
	}

	/// <summary>
	/// O dono recomeça a partida para a sala inteira (Jhon, 21/11): a de
	/// agora é descartada — sai da frota, e o resultado dela, quando o
	/// servidor de partida mandar, é recusado como partida desconhecida:
	/// ninguém ganha nem perde nada por ela — e todos recebem o ingresso de
	/// uma nova, como no começar. Também vale na tela do resultado (a sala já
	/// esperando): "jogar de novo" do dono leva todo mundo, pronto ou não.
	/// </summary>
	private void Restart(Session c, Outbox outbox)
	{
		Room room = InRoom(c);
		RequireMap(room.Settings);
		MatchAssignment assignment = room.RestartLineup(c.Account.Id, Guid.NewGuid().ToString("N"));
		AddItems(room, assignment);

		(string Address, int Port)? server = _fleet.Allocate(assignment);
		if (server == null) throw new Failure(FailureKind.Unavailable, "sem_servidor", "Não há servidor de partida livre agora. Tente de novo em instantes.");

		string old = room.State == Dominio.Salas.RoomState.Playing ? room.MatchKey : null;
		if (old != null)
		{
			_fleet.Release(old);
			room.EndMatch(old);
		}
		Launch(room, assignment, server.Value, old != null, outbox);
	}

	/// <summary>A sala entra na partida: cada um recebe o endereço e o seu ingresso.</summary>
	private void Launch(Room room, MatchAssignment assignment, (string Address, int Port) server, bool restart, Outbox outbox)
	{
		room.BeginMatch(assignment.MatchKey, server.Address, server.Port);
		foreach (RoomMember m in room.Members) SendTicket(room, assignment.MatchKey, SessionOf(m), restart, outbox);
		Changed(room, outbox);
	}

	/// <summary>O endereço da partida e o ingresso de um jogador. <paramref name="late"/>:
	/// entrou com ela em curso (o jogo dele trata os bots como bonecos até o
	/// servidor de partida dizer de quem são).</summary>
	private void SendTicket(Room room, string matchKey, Session session, bool restart, Outbox outbox, bool late = false)
	{
		if (session == null) return;
		long expires = new DateTimeOffset(_clock.UtcNow.Add(_ticketLife)).ToUnixTimeSeconds();
		var start = new JsonObject
		{
			["t"] = "match.start",
			["address"] = room.MatchAddress,
			["port"] = room.MatchPort,
			["matchKey"] = matchKey,
			["accountId"] = session.Account.Id,
			["expires"] = expires,
			["ticket"] = _fleet.Ticket(matchKey, session.Account.Id, expires),
			["settings"] = room.Settings.DeepClone(),
			["restart"] = restart
		};
		if (late) start["late"] = true;
		outbox.To(session, start);
	}

	/// <summary>
	/// O chat da sala, que também é o da partida (Jhon, 27/11: "chat em
	/// partida, chat na sala"): a sala continua aberta enquanto se joga, e a
	/// ligação do saguão também. <paramref name="team"/>: só para o time de
	/// quem fala (o "U" dos FPS) — fora de sala não há time.
	/// </summary>
	private void Chat(Session c, string text, bool team, Outbox outbox)
	{
		text = ChatRules.Clean(text, ChatRules.RoomMax);
		if (text.Length == 0) return;
		RoomMember seat = c.Room?.Members.FirstOrDefault(x => x.Account.Id == c.Account.Id);
		if (team && seat == null) InRoom(c);

		// Cinco mensagens a cada cinco segundos: o bastante para conversar.
		DateTime now = _clock.UtcNow;
		while (c.Chats.Count > 0 && now - c.Chats.Peek() > TimeSpan.FromSeconds(5)) c.Chats.Dequeue();
		if (c.Chats.Count >= 5) throw new Failure(FailureKind.TooMany, "devagar", "Devagar com as mensagens.");
		c.Chats.Enqueue(now);

		int side = seat?.Team ?? 0;
		var line = new JsonObject
		{
			["t"] = "chat",
			["from"] = c.Account.Name,
			["id"] = c.Account.Id,
			["room"] = c.Room?.Id,
			// O time de quem falou (0 fora de sala): a tela pinta o nome com a cor dele.
			["side"] = side,
			["team"] = team,
			["text"] = text,
			["at"] = new DateTimeOffset(now).ToUnixTimeSeconds()
		};

		// Na sala, fala com a sala (ou só com o time); fora dela, com quem está na lista.
		IEnumerable<Session> to = c.Room != null
			? c.Room.Members.Where(x => !team || x.Team == side).Select(SessionOf).Where(s => s != null)
			: _sessions.Values.Where(x => x.Room == null);
		outbox.To(to, line);
	}

	private static Room InRoom(Session c) =>
		c.Room ?? throw new Failure(FailureKind.Conflict, "fora_de_sala", "Você não está numa sala.");

	private Session SessionOf(RoomMember m) => _sessions.TryGetValue(m.Account.Id, out Session s) ? s : null;

	// ------------------------------------------------------------ saída

	/// <summary>A sala mudou de um jeito que a lista mostra.</summary>
	private void Changed(Room room, Outbox outbox)
	{
		RoomState(room, outbox);
		ListChanged();
	}

	/// <summary>As salas que a lista mostra: primeiro as que têm lugar
	/// (esperando ou, desde 24/11, jogando com vaga), das mais novas para as
	/// mais velhas; até <see cref="MaxListed"/>.</summary>
	private JsonArray RoomList()
	{
		var list = new JsonArray();
		IEnumerable<Room> shown = _rooms.Values
			.Where(r => !r.Quick)   // a sala do jogo rápido não é de entrar: quem a monta é a fila
			.OrderByDescending(r => r.HasRoom)
			.ThenByDescending(r => r.Id)
			.Take(MaxListed);
		foreach (Room r in shown)
			list.Add(new JsonObject
			{
				["id"] = r.Id,
				["settings"] = r.Settings.DeepClone(),
				["hasPassword"] = r.HasPassword,
				// Gente (jogando, se a partida está em curso) e bots; o jogo mostra
				// a soma (Jhon, 25/11: "partida com bot deve contabilizar aqui").
				["players"] = r.State == Dominio.Salas.RoomState.Waiting ? r.Members.Count : r.Members.Count(m => m.InMatch),
				["bots"] = r.Bots.Count,
				["capacity"] = r.Capacity,
				["state"] = r.StateName,
				["host"] = r.HostName,
				// Dá para entrar agora? Com a partida em curso, os bots dela contam,
				// um time não fica com dois a mais, e quase acabando não entra.
				["open"] = r.HasRoom && !Closing(r),
				["closing"] = Closing(r)
			});
		return list;
	}

	/// <summary>Os itens da conta, sem as fotos. Sem a lista (o banco falhou), vazio:
	/// a pessoa joga só com o que vem de série.</summary>
	private async Task<HashSet<string>> OwnedItems(long accountId)
	{
		if (Inventory == null) return new HashSet<string>();
		try
		{
			// Item vencido (temporário) não vale: o jogo também não o deixa equipar.
			DateTime now = _clock.UtcNow;
			return (await Inventory(accountId)).Where(i => i.ValidAt(now)).Select(i => i.ItemId)
				.Where(id => !id.StartsWith(PhotoRule.ItemPrefix, StringComparison.Ordinal)).ToHashSet();
		}
		catch (Exception)
		{
			return new HashSet<string>();
		}
	}

	/// <summary>Antes de montar a escalação, relê o que cada um tem: a compra pode ter
	/// sido agora, depois de entrar. Começar e recomeçar leem a sala inteira; entrar
	/// no meio, só quem entra.</summary>
	private async Task RefreshItemsFor(Session session, string type)
	{
		List<Session> who;
		lock (_lock)
		{
			if (type == "room.enter") who = new List<Session> { session };
			else if (type is "room.start" or "room.restart" && session.Room != null)
				who = session.Room.Members.Select(SessionOf).Where(x => x != null).ToList();
			// A partida da fila começa sem ninguém pedir: os itens valem como estavam ao entrar nela.
			else if (type == "queue.join")
				who = session.Group == null ? new List<Session> { session }
					: session.Group.Members.Select(id => _sessions.TryGetValue(id, out Session x) ? x : null).Where(x => x != null).ToList();
			else return;
		}
		foreach (Session x in who)
		{
			HashSet<string> items = await OwnedItems(x.Account.Id);
			lock (_lock) x.Items = items;
		}
	}

	/// <summary>A escalação com o que cada jogador tem (bot não tem conta: nada).</summary>
	private void AddItems(Room room, MatchAssignment assignment)
	{
		var items = room.Members.Select(SessionOf).Where(x => x != null).ToDictionary(x => x.Account.Id, x => x.Items);
		assignment.Players = assignment.Players
			.Select(p => items.TryGetValue(p.AccountId, out HashSet<string> own) ? p with { Weapons = own.ToList() } : p).ToList();
	}

	private async Task<HashSet<string>> OwnedPhotos(long accountId)
	{
		if (Inventory == null) return new HashSet<string>();
		try
		{
			return PhotoRule.Owned((await Inventory(accountId)).Select(i => i.ItemId));
		}
		catch (Exception)   // sem os itens, a pessoa entra só com as grátis
		{
			return new HashSet<string>();
		}
	}

	private void SetPhoto(Session c, string photo, Outbox outbox)
	{
		if (c.Photo == photo) return;
		c.Photo = photo;
		if (c.Room != null) RoomState(c.Room, outbox);
		SyncPresence(c, outbox);
	}

	/// <summary>Pediu uma foto da loja que não estava na lista: relê os itens
	/// (a compra pode ter sido agora) e, se for dela, troca.</summary>
	private async Task RecheckPhoto(Session c, string photo)
	{
		HashSet<string> owned = await OwnedPhotos(c.Account.Id);
		var outbox = new Outbox();
		lock (_lock)
		{
			c.Photos = owned;
			if (owned.Contains(photo)) SetPhoto(c, photo, outbox);
			FlushFriends(outbox);
		}
		await outbox.Deliver();
	}

	private JsonObject RoomsMessage() => new() { ["t"] = "rooms", ["rooms"] = RoomList(), ["total"] = _rooms.Count };

	/// <summary>O estado inteiro da sala, para todos nela.</summary>
	private void RoomState(Room room, Outbox outbox)
	{
		var members = new JsonArray();
		foreach (RoomMember m in room.Members)
			members.Add(new JsonObject
			{
				["id"] = m.Account.Id,
				["name"] = m.Account.Name,
				["team"] = m.Team,
				["ready"] = m.Ready,
				["host"] = m.Account.Id == room.HostId,
				["inMatch"] = m.InMatch,
				["xp"] = m.Xp,
				["photo"] = SessionOf(m)?.Photo ?? PhotoRule.Default
			});

		// Os bots entram na mesma lista, marcados: sempre prontos, sem XP
		// (bot não tem patente), movidos pela máquina do dono da sala.
		foreach (BotSeat b in room.Bots)
			members.Add(new JsonObject
			{
				["id"] = b.Id,
				["name"] = b.Name,
				["team"] = b.Team,
				["ready"] = true,
				["host"] = false,
				["bot"] = true,
				["owner"] = room.HostId
			});

		var state = new JsonObject
		{
			["t"] = "room",
			["id"] = room.Id,
			["settings"] = room.Settings.DeepClone(),
			["hasPassword"] = room.HasPassword,
			["state"] = room.StateName,
			["members"] = members
		};

		// Com a partida em curso: por quais times dá para entrar agora (quem
		// está de fora escolhe entre eles) e se ela está quase acabando.
		if (room.State == Dominio.Salas.RoomState.Playing)
		{
			bool closing = Closing(room);
			var enter = new JsonArray();
			if (!closing)
				foreach (int t in new[] { Room.Blue, Room.Orange })
					if (room.CanEnter(t)) enter.Add(t);
			state["enter"] = enter;
			state["closing"] = closing;
		}
		outbox.To(room.Members.Select(SessionOf), state);

		// Entrar, sair, a partida começar ou acabar muda o que os amigos de
		// cada um veem (quem não tem amigo não custa nada aqui).
		foreach (RoomMember m in room.Members) SyncPresence(SessionOf(m), outbox);
	}

	/// <summary>A lista de salas mudou: vai na próxima leva (<see cref="FlushList"/>).</summary>
	private void ListChanged() => _listChanged = true;

	private static JsonObject ErrorMessage(string code, string message) =>
		new() { ["t"] = "error", ["code"] = code, ["message"] = message };

	/// <summary>O que sai de um pedido: juntado dentro da trava, entregue
	/// fora dela (mandar pela rede pode demorar).</summary>
	private sealed class Outbox
	{
		private readonly List<(Session Session, string Text)> _items = new();

		/// <summary>Quem precisa da lista de amigos de novo: junta aqui e sai
		/// uma mensagem só por pessoa, montada no fim da trava
		/// (<see cref="FlushFriends"/>).</summary>
		public readonly HashSet<Session> Dirty = new();

		/// <summary>Os grupos que mudaram (quem está, quem lidera, onde cada um
		/// está): cada um vai uma vez só para os seus, na mesma passagem.</summary>
		public readonly HashSet<Group> Groups = new();

		public void To(Session session, JsonObject message)
		{
			if (session != null) _items.Add((session, message.ToJsonString()));
		}

		/// <summary>A mesma mensagem para vários: vira texto uma vez só.</summary>
		public void To(IEnumerable<Session> sessions, JsonObject message)
		{
			string text = null;
			foreach (Session session in sessions)
			{
				if (session == null) continue;
				text ??= message.ToJsonString();
				_items.Add((session, text));
			}
		}

		/// <summary>Entrega: cada conexão tem a própria fila (lá fora), então
		/// mandar para milhares não espera nenhum deles.</summary>
		public async Task Deliver()
		{
			foreach ((Session session, string text) in _items)
				if (session.Connection.IsOpen) await session.Connection.Send(text);
		}
	}
}
