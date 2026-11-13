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
/// <c>room.start</c>, <c>chat</c> e <c>ping</c>; o servidor responde com
/// <c>hello</c>, <c>rooms</c>, <c>room</c>, <c>left</c>, <c>chat</c>,
/// <c>match.start</c>, <c>match.end</c>, <c>pong</c> e <c>error</c>. O
/// formato de cada uma está em servidor/LEIA-ME.md.
///
/// As salas vivem em memória: sala é coisa de agora, e se o servidor
/// reiniciar os jogadores voltam para a lista. As regras de cada sala são
/// do domínio (<see cref="Room"/>); aqui ficam quem está ligado, a trava
/// e quem precisa saber do quê. A conexão em si (o WebSocket) é de fora:
/// chega como <see cref="ILobbyConnection"/>.
/// </summary>
public sealed class LobbyService
{
	public const int MaxChat = 200, MaxRooms = 500;

	/// <summary>Um jogador ligado no saguão.</summary>
	public sealed class Session
	{
		public Account Account { get; init; }
		internal ILobbyConnection Connection;
		internal Room Room;
		internal long Xp;
		internal readonly Queue<DateTime> Chats = new();
	}

	private readonly FleetService _fleet;
	private readonly AccountService _accounts;
	private readonly IClock _clock;
	private readonly TimeSpan _ticketLife;
	private readonly object _lock = new();
	private readonly Dictionary<long, Session> _sessions = new();
	private readonly Dictionary<int, Room> _rooms = new();
	private int _nextRoom = 1;

	public LobbyService(FleetService fleet, AccountService accounts, IClock clock, TimeSpan ticketLife)
	{
		_fleet = fleet;
		_accounts = accounts;
		_clock = clock;
		_ticketLife = ticketLife;
		_fleet.Lost += OnMatchLost;
	}

	public int Online { get { lock (_lock) return _sessions.Count; } }

	/// <summary>A lista de salas, como vai para o cliente (sem senha).</summary>
	public JsonArray Rooms()
	{
		lock (_lock) return RoomList();
	}

	// ------------------------------------------------------------ a conexão

	/// <summary>Um jogador chegou. Uma conexão por conta: a nova derruba a velha.</summary>
	public async Task<Session> Connect(Account account, ILobbyConnection connection)
	{
		long xp = 0;
		try
		{
			xp = (await _accounts.Profile(account.Id)).Xp;
		}
		catch (Failure)
		{
			// Conta sem perfil ainda entra; só aparece sem patente.
		}

		var session = new Session { Account = account, Connection = connection, Xp = xp };
		var outbox = new Outbox();
		Session old;
		lock (_lock)
		{
			_sessions.TryGetValue(account.Id, out old);
			if (old != null) Leave(old, outbox);
			_sessions[account.Id] = session;
			outbox.To(session, new JsonObject { ["t"] = "hello", ["id"] = account.Id, ["name"] = account.Name, ["online"] = _sessions.Count });
			outbox.To(session, RoomsMessage());
		}

		await outbox.Deliver();
		if (old != null) _ = old.Connection.Close("outra conexão da mesma conta");
		return session;
	}

	/// <summary>Uma mensagem do jogador. Pedido que não dá vira um <c>error</c> só para ele.</summary>
	public async Task Handle(Session session, string text)
	{
		var outbox = new Outbox();
		try
		{
			if (JsonNode.Parse(text) is not JsonObject message)
				throw new Failure(FailureKind.Invalid, "mensagem_invalida", "Mensagem inválida.");
			lock (_lock) Dispatch(session, message, outbox);
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
			if (_sessions.TryGetValue(session.Account.Id, out Session current) && current == session)
			{
				Leave(session, outbox);
				_sessions.Remove(session.Account.Id);
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
		lock (_lock)
		{
			Room room = _rooms.Values.FirstOrDefault(r => r.MatchKey == match.Assignment.MatchKey);
			if (room != null && room.EndMatch(match.Assignment.MatchKey))
			{
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
				RoomState(room, outbox);
				RoomListToAll(outbox);
			}
		}
		await outbox.Deliver();
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

			JsonObject why = lost.Reason == MatchLoss.ServerDown
				? ErrorMessage("servidor_caiu", "O servidor da partida caiu. A sala voltou a esperar.")
				: ErrorMessage("partida_encerrada", "A partida foi encerrada sem resultado. A sala voltou a esperar.");
			foreach (RoomMember m in room.Members) outbox.To(SessionOf(m), why);
			RoomState(room, outbox);
			RoomListToAll(outbox);
		}
		_ = outbox.Deliver();
	}

	// ------------------------------------------------------------ mensagens

	private void Dispatch(Session c, JsonObject m, Outbox outbox)
	{
		string type = m["t"]?.GetValue<string>() ?? "";
		switch (type)
		{
			case "ping":
				outbox.To(c, new JsonObject { ["t"] = "pong" });
				break;

			case "rooms.list":
				outbox.To(c, RoomsMessage());
				break;

			case "room.create":
				Create(c, m["settings"] as JsonObject, outbox);
				break;

			case "room.join":
				Join(c, m["id"]?.GetValue<int>() ?? 0, m["password"]?.GetValue<string>() ?? "", outbox);
				break;

			case "room.leave":
				Leave(c, outbox);
				outbox.To(c, RoomsMessage());
				break;

			case "room.team":
				InRoom(c).ChangeTeam(c.Account.Id, m["team"]?.GetValue<int>() ?? 0);
				RoomState(c.Room, outbox);
				break;

			case "room.ready":
				InRoom(c).SetReady(c.Account.Id, m["ready"]?.GetValue<bool>() ?? false);
				RoomState(c.Room, outbox);
				break;

			case "room.settings":
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

			case "chat":
				Chat(c, m["text"]?.GetValue<string>() ?? "", outbox);
				break;

			default:
				throw new Failure(FailureKind.Invalid, "mensagem_desconhecida", $"Não conheço '{type}'.");
		}
	}

	private void Create(Session c, JsonObject settings, Outbox outbox)
	{
		if (_rooms.Count >= MaxRooms) throw new Failure(FailureKind.Unavailable, "cheio", "O servidor está com salas demais.");
		Leave(c, outbox);

		Room room = Room.Create(_nextRoom++, c.Account, c.Xp, settings);
		_rooms[room.Id] = room;
		c.Room = room;
		Changed(room, outbox);
	}

	private void Join(Session c, int id, string password, Outbox outbox)
	{
		if (!_rooms.TryGetValue(id, out Room room)) throw new Failure(FailureKind.NotFound, "sala_sumiu", "Essa sala não existe mais.");
		if (c.Room == room) return;
		if (room.State != Dominio.Salas.RoomState.Waiting) throw new Failure(FailureKind.Conflict, "em_partida", "A sala está em partida.");

		// A sala nova confere tudo antes: recusado, o jogador fica onde estava.
		room.Join(c.Account, c.Xp, password);
		Leave(c, outbox);
		c.Room = room;
		Changed(room, outbox);
	}

	/// <summary>Tira o jogador da sala em que está. Sala vazia some.</summary>
	private void Leave(Session c, Outbox outbox)
	{
		Room room = c.Room;
		if (room == null) return;

		room.Leave(c.Account.Id);
		c.Room = null;
		outbox.To(c, new JsonObject { ["t"] = "left", ["id"] = room.Id });

		if (room.IsEmpty) _rooms.Remove(room.Id);
		else RoomState(room, outbox);
		RoomListToAll(outbox);
	}

	private void Start(Session c, Outbox outbox)
	{
		Room room = InRoom(c);
		MatchAssignment assignment = room.Lineup(c.Account.Id, Guid.NewGuid().ToString("N"));

		(string Address, int Port)? server = _fleet.Allocate(assignment);
		if (server == null) throw new Failure(FailureKind.Unavailable, "sem_servidor", "Não há servidor de partida livre agora. Tente de novo em instantes.");

		room.BeginMatch(assignment.MatchKey);
		long expires = new DateTimeOffset(_clock.UtcNow.Add(_ticketLife)).ToUnixTimeSeconds();

		foreach (RoomMember m in room.Members)
			outbox.To(SessionOf(m), new JsonObject
			{
				["t"] = "match.start",
				["address"] = server.Value.Address,
				["port"] = server.Value.Port,
				["matchKey"] = assignment.MatchKey,
				["accountId"] = m.Account.Id,
				["expires"] = expires,
				["ticket"] = _fleet.Ticket(assignment.MatchKey, m.Account.Id, expires),
				["settings"] = room.Settings.DeepClone()
			});

		Changed(room, outbox);
	}

	private void Chat(Session c, string text, Outbox outbox)
	{
		text = text.Trim();
		if (text.Length == 0) return;
		if (text.Length > MaxChat) text = text[..MaxChat];

		// Cinco mensagens a cada cinco segundos: o bastante para conversar.
		DateTime now = _clock.UtcNow;
		while (c.Chats.Count > 0 && now - c.Chats.Peek() > TimeSpan.FromSeconds(5)) c.Chats.Dequeue();
		if (c.Chats.Count >= 5) throw new Failure(FailureKind.TooMany, "devagar", "Devagar com as mensagens.");
		c.Chats.Enqueue(now);

		var line = new JsonObject
		{
			["t"] = "chat",
			["from"] = c.Account.Name,
			["room"] = c.Room?.Id,
			["text"] = text,
			["at"] = new DateTimeOffset(now).ToUnixTimeSeconds()
		};

		// Na sala, fala com a sala; fora dela, com quem está na lista.
		IEnumerable<Session> to = c.Room != null
			? c.Room.Members.Select(SessionOf).Where(s => s != null)
			: _sessions.Values.Where(x => x.Room == null);
		foreach (Session other in to) outbox.To(other, line);
	}

	private static Room InRoom(Session c) =>
		c.Room ?? throw new Failure(FailureKind.Conflict, "fora_de_sala", "Você não está numa sala.");

	private Session SessionOf(RoomMember m) => _sessions.TryGetValue(m.Account.Id, out Session s) ? s : null;

	// ------------------------------------------------------------ saída

	/// <summary>A sala mudou de um jeito que a lista mostra.</summary>
	private void Changed(Room room, Outbox outbox)
	{
		RoomState(room, outbox);
		RoomListToAll(outbox);
	}

	private JsonArray RoomList()
	{
		var list = new JsonArray();
		foreach (Room r in _rooms.Values.OrderByDescending(r => r.Id))
			list.Add(new JsonObject
			{
				["id"] = r.Id,
				["settings"] = r.Settings.DeepClone(),
				["hasPassword"] = r.HasPassword,
				["players"] = r.Members.Count,
				["bots"] = r.Bots.Count,
				["capacity"] = r.Capacity,
				["state"] = r.StateName,
				["host"] = r.HostName
			});
		return list;
	}

	private JsonObject RoomsMessage() => new() { ["t"] = "rooms", ["rooms"] = RoomList() };

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
				["xp"] = m.Xp
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
		foreach (RoomMember m in room.Members) outbox.To(SessionOf(m), state);
	}

	/// <summary>A lista de salas, para quem está olhando para ela.</summary>
	private void RoomListToAll(Outbox outbox)
	{
		JsonObject list = RoomsMessage();
		foreach (Session s in _sessions.Values.Where(s => s.Room == null)) outbox.To(s, list);
	}

	private static JsonObject ErrorMessage(string code, string message) =>
		new() { ["t"] = "error", ["code"] = code, ["message"] = message };

	/// <summary>O que sai de um pedido: juntado dentro da trava, entregue
	/// fora dela (mandar pela rede pode demorar).</summary>
	private sealed class Outbox
	{
		private readonly List<(Session Session, string Text)> _items = new();

		public void To(Session session, JsonObject message)
		{
			if (session != null) _items.Add((session, message.ToJsonString()));
		}

		public async Task Deliver()
		{
			foreach ((Session session, string text) in _items)
				if (session.Connection.IsOpen) await session.Connection.Send(text);
		}
	}
}
