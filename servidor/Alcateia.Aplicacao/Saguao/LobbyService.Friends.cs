using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Salas;

namespace Alcateia.Aplicacao.Saguao;

/// <summary>
/// Os amigos no saguão (Jhon, 27/11: "a parte do social (amigos), inclusive
/// convidar, aceitar pedidos"): a lista, os pedidos, a presença e o convite
/// para a sala.
///
/// O jogo manda <c>friends.list</c>, <c>friend.add</c>, <c>friend.accept</c>,
/// <c>friend.decline</c>, <c>friend.remove</c>, <c>friend.invite</c>,
/// <c>invite.accept</c> e <c>invite.decline</c>; o servidor responde com
/// <c>friends</c> (a lista inteira, com a presença de cada amigo) e avisa com
/// <c>friend.request</c>, <c>friend.accepted</c>, <c>friend.invited</c> e
/// <c>invite.declined</c>. O formato está em servidor/LEIA-ME.md.
///
/// **Onde mora cada coisa.** A amizade é do banco (<see cref="IFriendRepository"/>);
/// cada conexão guarda uma cópia da lista da conta dela (<see cref="Session.Links"/>),
/// lida ao entrar e relida inteira a cada mudança — pedir, aceitar, recusar e
/// remover são raros, e reler é o que impede a memória de discordar do banco.
/// A presença e os convites são só de memória: valem enquanto a conexão vale.
///
/// **Custo.** A presença não tem relógio: sai quando algo muda (entrou, caiu,
/// sala, partida, foto), e só para os amigos que estão ligados. Conta sem
/// amigo sai na primeira linha de <see cref="SyncPresence"/>. Várias mudanças
/// no mesmo pedido viram uma mensagem só por pessoa (<see cref="FlushFriends"/>).
/// </summary>
public sealed partial class LobbyService
{
	/// <summary>Onde as amizades ficam guardadas. Sem isso (um saguão montado
	/// sem banco), os pedidos de amizade respondem que está fora do ar.</summary>
	public IFriendRepository FriendStore { get; set; }

	/// <summary>Pedidos de amizade por minuto, por conexão: quem cola nomes
	/// para achar contas ou encher a caixa dos outros para aqui.</summary>
	public const int FriendAddsPerMinute = 10;

	/// <summary>Aceitar, recusar e remover por minuto, por conexão (cada um é
	/// uma ida ao banco).</summary>
	public const int FriendWritesPerMinute = 40;

	/// <summary>Convites para sala a cada <see cref="InviteWindow"/>.</summary>
	public const int InvitesPerWindow = 8;
	public static readonly TimeSpan InviteWindow = TimeSpan.FromSeconds(20);

	/// <summary>O mesmo amigo não é convidado de novo antes disto.</summary>
	public static readonly TimeSpan InviteAgain = TimeSpan.FromSeconds(5);

	/// <summary>Quanto um convite vale sem resposta.</summary>
	public static readonly TimeSpan InviteLife = TimeSpan.FromMinutes(2);

	// ------------------------------------------------------------ a lista

	private static bool IsFriendWrite(string type) =>
		type is "friend.add" or "friend.accept" or "friend.decline" or "friend.remove";

	/// <summary>A lista da conta, do banco. Banco fora: lista vazia — a
	/// pessoa entra no saguão do mesmo jeito.</summary>
	private async Task<IReadOnlyList<FriendLink>> LoadLinks(long accountId)
	{
		if (FriendStore == null) return Array.Empty<FriendLink>();
		try
		{
			return await FriendStore.Links(accountId);
		}
		catch (Exception e) when (e is not Failure)
		{
			return Array.Empty<FriendLink>();
		}
	}

	private static void SetLinks(Session s, IReadOnlyList<FriendLink> links)
	{
		s.Links = links.ToDictionary(l => l.Id);
		s.FriendCount = links.Count(l => l.Kind == FriendKind.Friend);
	}

	/// <summary>
	/// Pedir, aceitar, recusar (ou cancelar) e remover: grava no banco, relê a
	/// lista dos dois lados (de quem estiver ligado) e manda a lista nova e o
	/// aviso. Roda fora da trava do saguão; só a parte de memória entra nela.
	/// </summary>
	private async Task FriendWrite(Session c, string type, JsonObject m, Outbox outbox)
	{
		IFriendRepository store = FriendStore ?? throw FriendRules.Unavailable();
		long me = c.Account.Id, other;
		string notice = null;      // o aviso que o outro lado recebe, além da lista

		lock (_lock)
			Throttle(type == "friend.add" ? c.FriendAdds : c.FriendWrites,
				type == "friend.add" ? FriendAddsPerMinute : FriendWritesPerMinute, TimeSpan.FromMinutes(1),
				"Devagar com os pedidos de amizade. Espere um minuto.");

		try
		{
			switch (type)
			{
				case "friend.add":
				{
					// O pedido é pelo nome com a tag (Nome#0042): só quem a pessoa
					// contou sabe a tag dela.
					if (!AccountRules.TryParseHandle(m["name"]?.GetValue<string>(), out string name, out int tag)) throw FriendRules.NoTag();
					string key = AccountRules.Key(name);
					if (key.Length > AccountRules.NameMax) throw FriendRules.NoAccount();
					if (key == AccountRules.Key(c.Account.Name)) throw FriendRules.Self();
					FriendTarget target = await store.Find(key) ?? throw FriendRules.NoAccount();
					if (target.Id == me) throw FriendRules.Self();
					if (target.Tag != tag) throw FriendRules.NoAccount();
					other = target.Id;
					notice = await store.Request(me, other) == FriendRequestOutcome.Accepted ? "friend.accepted" : "friend.request";
					break;
				}

				case "friend.accept":
					other = m["id"]?.GetValue<long>() ?? 0;
					if (!await store.Accept(me, other)) throw FriendRules.NoRequest();
					notice = "friend.accepted";
					break;

				case "friend.decline":
					other = m["id"]?.GetValue<long>() ?? 0;
					if (!await store.RemovePending(me, other)) throw FriendRules.NoRequest();
					break;

				default:   // friend.remove
					other = m["id"]?.GetValue<long>() ?? 0;
					if (!await store.RemoveFriend(me, other)) throw FriendRules.NotFriends();
					break;
			}
		}
		catch (Exception e) when (e is not Failure and not InvalidOperationException and not FormatException)
		{
			// O banco não respondeu: o jogador fica sabendo, e a conexão segue.
			throw FriendRules.Unavailable();
		}

		// A lista nova dos dois, lida fora da trava; quem não está ligado lê ao entrar.
		IReadOnlyList<FriendLink> mine = await LoadLinks(me);
		IReadOnlyList<FriendLink> theirs = IsLive(other) ? await LoadLinks(other) : null;

		lock (_lock)
		{
			// O jogo e o aplicativo da mesma conta veem a mesma lista.
			foreach (Session self in Live(me))
			{
				SetLinks(self, mine);
				outbox.Dirty.Add(self);
			}
			foreach (Session peer in Live(other))
			{
				// Ligou entre a leitura e agora: fica com a lista que leu ao entrar, que já é a nova.
				if (theirs != null) SetLinks(peer, theirs);
				outbox.Dirty.Add(peer);
				if (notice == "friend.request")
					outbox.To(peer, new JsonObject { ["t"] = notice, ["from"] = Person(c) });
				else if (notice == "friend.accepted")
					outbox.To(peer, new JsonObject { ["t"] = notice, ["by"] = Person(c) });
			}

			// Deixaram de ser amigos: os convites entre os dois caem junto.
			if (type == "friend.remove")
			{
				if (_sessions.TryGetValue(other, out Session peerGame)) { peerGame.Invites.Remove(me); peerGame.GroupInvites.Remove(me); }
				if (_sessions.TryGetValue(me, out Session selfGame)) { selfGame.Invites.Remove(other); selfGame.GroupInvites.Remove(other); }
			}
			FlushFriends(outbox);
		}
	}

	/// <summary>A conta tem alguma ligação viva (o jogo ou o aplicativo)?</summary>
	private bool IsLive(long accountId)
	{
		lock (_lock) return _sessions.ContainsKey(accountId) || _apps.ContainsKey(accountId);
	}

	/// <summary>Uma conta como vai para o jogo: com o nome, o XP e a foto de
	/// agora se ela está ligada; senão, com o que se sabia dela.</summary>
	private JsonObject Person(long id, string name, long xp, int tag)
	{
		_sessions.TryGetValue(id, out Session live);
		return new JsonObject
		{
			["id"] = id,
			["name"] = live?.Account.Name ?? name,
			["tag"] = AccountRules.TagText(live?.Account.Tag ?? tag),
			["xp"] = live?.Xp ?? xp,
			["photo"] = live?.Photo ?? PhotoRule.Default
		};
	}

	private JsonObject Person(Session s) => Person(s.Account.Id, s.Account.Name, s.Xp, s.Account.Tag);

	/// <summary>O que a conta está fazendo, como os amigos veem: fora do ar,
	/// só com o cliente de instalação aberto ("app"), no saguão, numa sala
	/// esperando ou jogando.</summary>
	private (string Status, int? Room) StatusOf(long id)
	{
		if (!_sessions.TryGetValue(id, out Session s) || s.Gone)
			return (_apps.TryGetValue(id, out Session app) && !app.Gone ? "app" : "offline", null);
		Room room = s.Room;
		if (room == null) return ("online", null);
		bool playing = room.State == Dominio.Salas.RoomState.Playing && room.Members.Any(m => m.Account.Id == id && m.InMatch);
		return (playing ? "playing" : "room", room.Id);
	}

	/// <summary>A lista de amigos de uma conexão: amigos (os ligados primeiro,
	/// depois por nome), pedidos recebidos e pedidos enviados.</summary>
	private JsonObject FriendsMessage(Session s)
	{
		var friends = new List<(int On, string Name, JsonObject Node)>();
		var requests = new List<(string Name, JsonObject Node)>();
		var sent = new List<(string Name, JsonObject Node)>();

		foreach (FriendLink link in s.Links.Values)
		{
			JsonObject person = Person(link.Id, link.Name, link.Xp, link.Tag);
			string name = person["name"].GetValue<string>();
			switch (link.Kind)
			{
				case FriendKind.Friend:
					(string status, int? room) = StatusOf(link.Id);
					person["status"] = status;
					person["room"] = room;
					// Quem está só no aplicativo vem depois de quem está no jogo.
					friends.Add((status == "offline" ? 0 : status == "app" ? 1 : 2, name, person));
					break;
				case FriendKind.Incoming:
					requests.Add((name, person));
					break;
				default:
					sent.Add((name, person));
					break;
			}
		}

		static JsonArray Sorted(IEnumerable<(string Name, JsonObject Node)> list) =>
			new(list.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => (JsonNode)x.Node).ToArray());

		return new JsonObject
		{
			["t"] = "friends",
			["friends"] = new JsonArray(friends.OrderByDescending(f => f.On).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
				.Select(f => (JsonNode)f.Node).ToArray()),
			["requests"] = Sorted(requests),
			["sent"] = Sorted(sent)
		};
	}

	// ------------------------------------------------------------ presença

	/// <summary>A presença desta conexão mudou desde a última vez que os
	/// amigos a viram? Então eles recebem a lista de novo.</summary>
	private void SyncPresence(Session s, Outbox outbox)
	{
		if (s == null || (s.FriendCount == 0 && s.Group == null)) return;
		(string status, int? room) = StatusOf(s.Account.Id);
		string now = $"{status}:{room}:{s.Photo}";
		if (now == s.Presence) return;
		s.Presence = now;
		NotifyFriends(s, outbox);
		// O grupo mostra onde cada um está: os outros dele também ficam sabendo.
		if (s.Group != null) outbox.Groups.Add(s.Group);
	}

	/// <summary>Os amigos ligados desta conta (no jogo ou no aplicativo)
	/// precisam da lista de novo.</summary>
	private void NotifyFriends(Session s, Outbox outbox)
	{
		if (s.FriendCount == 0) return;
		foreach (FriendLink link in s.Links.Values)
			if (link.Kind == FriendKind.Friend)
				foreach (Session friend in Live(link.Id))
					outbox.Dirty.Add(friend);
	}

	/// <summary>Monta a lista de amigos de quem ficou marcado
	/// (<see cref="Outbox.Dirty"/>), uma vez por pessoa, e o estado dos grupos
	/// que mudaram (<see cref="FlushGroups"/>). Dentro da trava, no fim de
	/// cada pedido.</summary>
	private void FlushFriends(Outbox outbox)
	{
		FlushGroups(outbox);
		if (outbox.Dirty.Count == 0) return;
		foreach (Session s in outbox.Dirty)
			if (IsCurrent(s))
				outbox.To(s, FriendsMessage(s));
		outbox.Dirty.Clear();
	}

	// ------------------------------------------------------------ convites

	/// <summary>Chama um amigo ligado para a sala em que se está.</summary>
	private void Invite(Session c, long friendId, Outbox outbox)
	{
		Room room = InRoom(c);
		if (room.Quick)
			throw new Failure(FailureKind.Conflict, "sala_da_fila", "Para a partida do jogo rápido não se convida: quem monta os times é a fila.");
		if (!c.Links.TryGetValue(friendId, out FriendLink link) || link.Kind != FriendKind.Friend) throw FriendRules.NotFriends();
		if (!_sessions.TryGetValue(friendId, out Session friend)) throw NotInGame(friendId);
		if (friend.Room == room) throw FriendRules.AlreadyHere();

		DateTime now = _clock.UtcNow;
		if (friend.Invites.TryGetValue(c.Account.Id, out var last) && now - last.At < InviteAgain)
			throw new Failure(FailureKind.TooMany, "devagar", "Você acabou de convidar esse amigo.");
		Throttle(c.InvitesSent, InvitesPerWindow, InviteWindow, "Devagar com os convites.");

		// Os vencidos saem na passagem: a lista de convites não cresce sozinha.
		foreach (long old in friend.Invites.Where(i => now - i.Value.At > InviteLife).Select(i => i.Key).ToList())
			friend.Invites.Remove(old);
		friend.Invites[c.Account.Id] = (room.Id, now);

		outbox.To(friend, new JsonObject
		{
			["t"] = "friend.invited",
			["from"] = Person(c),
			["room"] = new JsonObject
			{
				["id"] = room.Id,
				["title"] = room.Settings["title"]?.DeepClone(),
				["map"] = room.Settings["map"]?.DeepClone(),
				["mode"] = room.Settings["play"]?.DeepClone() ?? 0,
				["players"] = room.Occupied,
				["capacity"] = room.Capacity,
				["state"] = room.StateName
			}
		});
	}

	/// <summary>O amigo não está no jogo: desligado, ou só com o aplicativo aberto.</summary>
	private Failure NotInGame(long friendId) =>
		_apps.ContainsKey(friendId) ? FriendRules.FriendNotInGame() : FriendRules.FriendOffline();

	/// <summary>
	/// Entra na sala para a qual um amigo convidou. O convite dispensa a senha
	/// e mais nada (lotação, times e partida quase acabando valem igual), e só
	/// vale enquanto quem convidou continuar naquela sala e por
	/// <see cref="InviteLife"/>. Usado ou vencido, some.
	/// </summary>
	private void AcceptInvite(Session c, long from, Outbox outbox)
	{
		if (!c.Invites.TryGetValue(from, out var invite)) throw FriendRules.InviteGone();
		bool valid = _clock.UtcNow - invite.At <= InviteLife
			&& _sessions.TryGetValue(from, out Session host) && host.Room?.Id == invite.Room;
		if (!valid)
		{
			c.Invites.Remove(from);
			throw FriendRules.InviteGone();
		}

		// Recusado (sala cheia, por exemplo), o convite continua valendo: dá para tentar de novo.
		Join(c, invite.Room, "", outbox, invited: true);
		c.Invites.Remove(from);
	}

	private void DeclineInvite(Session c, long from, Outbox outbox)
	{
		if (!c.Invites.Remove(from)) return;
		if (_sessions.TryGetValue(from, out Session host))
			outbox.To(host, new JsonObject
			{
				["t"] = "invite.declined",
				["by"] = new JsonObject { ["id"] = c.Account.Id, ["name"] = c.Account.Name }
			});
	}

	// ------------------------------------------------------------ freio

	/// <summary>No máximo <paramref name="max"/> a cada <paramref name="window"/>, por conexão.</summary>
	private void Throttle(Queue<DateTime> recent, int max, TimeSpan window, string message)
	{
		DateTime now = _clock.UtcNow;
		while (recent.Count > 0 && now - recent.Peek() > window) recent.Dequeue();
		if (recent.Count >= max) throw new Failure(FailureKind.TooMany, "devagar", message);
		recent.Enqueue(now);
	}
}
