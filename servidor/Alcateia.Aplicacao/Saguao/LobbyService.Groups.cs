using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Alcateia.Dominio;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Salas;

namespace Alcateia.Aplicacao.Saguao;

/// <summary>
/// Os grupos no saguão (Jhon, 27/11: "criar grupo para jogarem juntos no
/// mesmo time"): amigos que entram juntos nas salas, do mesmo lado.
///
/// O jogo manda <c>group.invite</c>, <c>group.accept</c>,
/// <c>group.decline</c>, <c>group.leave</c>, <c>group.kick</c> e
/// <c>group.lead</c>; o servidor responde com <c>group</c> (o grupo inteiro,
/// com onde cada um está; sem membros = você não está em grupo) e avisa com
/// <c>group.invited</c>, <c>group.declined</c> e <c>group.kicked</c>. O
/// formato está em servidor/LEIA-ME.md.
///
/// **As regras.**
/// - O grupo nasce quando o primeiro convite é aceito; quem convidou lidera.
///   Só o líder convida, tira gente e passa a liderança. Sobrou um, acabou.
/// - **Quem escolhe a sala é o líder.** Ele cria ou entra numa sala e o grupo
///   vai junto, para o mesmo time — se não cabem todos do mesmo lado, ninguém
///   entra. Quem está no meio de uma partida não é arrancado dela: fica, e
///   entra na sala do líder depois (para quem é do grupo ela não pede senha).
/// - Quem não lidera não cria sala nem entra em outra que não a do líder.
///   Sair da sala pode (continua no grupo); se quem sai é o líder, os do
///   grupo que estão nela, fora de partida, saem com ele.
/// - Com a sala esperando, o time é do grupo: o líder troca e todos trocam.
///   Com a partida em curso, cada um entra por onde o equilíbrio dos times
///   deixar (<see cref="Room.CanEnter"/>) — aí o grupo pode se dividir.
/// - Quem cai da ligação sai do grupo; se era o líder, o mais antigo assume.
///
/// O grupo é de memória, como a sala e os convites. Só a ligação do jogo
/// participa: o cliente de instalação não entra em grupo.
/// </summary>
public sealed partial class LobbyService
{
	// ------------------------------------------------------------ convites

	/// <summary>Chama um amigo que está no jogo para o grupo. Quem não tem
	/// grupo passa a liderar um quando o amigo aceitar.</summary>
	private void GroupInvite(Session c, long friendId, Outbox outbox)
	{
		if (!c.Links.TryGetValue(friendId, out FriendLink link) || link.Kind != FriendKind.Friend) throw FriendRules.NotFriends();
		if (!_sessions.TryGetValue(friendId, out Session friend)) throw NotInGame(friendId);

		Group group = c.Group;
		if (group != null)
		{
			if (group.Leader != c.Account.Id) throw GroupRules.NotLeader();
			if (group.Has(friendId)) throw GroupRules.AlreadyIn();
			if (group.IsFull) throw GroupRules.Full();
		}

		DateTime now = _clock.UtcNow;
		if (friend.GroupInvites.TryGetValue(c.Account.Id, out DateTime last) && now - last < InviteAgain)
			throw new Failure(FailureKind.TooMany, "devagar", "Você acabou de convidar esse amigo.");
		Throttle(c.GroupInvitesSent, InvitesPerWindow, InviteWindow, "Devagar com os convites.");

		foreach (long old in friend.GroupInvites.Where(i => now - i.Value > InviteLife).Select(i => i.Key).ToList())
			friend.GroupInvites.Remove(old);
		friend.GroupInvites[c.Account.Id] = now;

		outbox.To(friend, new JsonObject
		{
			["t"] = "group.invited",
			["from"] = Person(c),
			["size"] = group?.Members.Count ?? 1,
			["max"] = Group.MaxSize
		});
	}

	/// <summary>
	/// Entra no grupo de quem convidou. O convite vale por
	/// <see cref="InviteLife"/>, enquanto quem convidou estiver no jogo e
	/// (se já tem grupo) continuar liderando. Quem estava noutro grupo sai
	/// dele. E o grupo anda junto: se o líder está numa sala, o novo vai para
	/// ela — se não couber, entra no grupo do mesmo jeito e fica sabendo.
	/// </summary>
	private void GroupAccept(Session c, long from, Outbox outbox)
	{
		if (!c.GroupInvites.TryGetValue(from, out DateTime at)) throw GroupRules.InviteGone();
		bool valid = _clock.UtcNow - at <= InviteLife
			&& _sessions.TryGetValue(from, out Session host)
			&& (host.Group == null || host.Group.Leader == from);
		if (!valid)
		{
			c.GroupInvites.Remove(from);
			throw GroupRules.InviteGone();
		}

		host = _sessions[from];
		if (host.Group != null && host.Group.Has(c.Account.Id))
		{
			c.GroupInvites.Remove(from);
			return;
		}
		// Cheio agora: o convite continua valendo, dá para tentar quando abrir lugar.
		if (host.Group is { IsFull: true }) throw GroupRules.Full();

		c.GroupInvites.Remove(from);
		// O grupo vai mudar: quem estava procurando partida (sozinho, ou o grupo) para e procura de novo.
		QueueLeave(c, outbox, "Você entrou num grupo: a busca parou.");
		QueueLeave(host, outbox, "O grupo mudou: a busca parou.");
		if (c.Group != null) LeaveGroup(c, outbox);

		Group group = host.Group ??= new Group(from);
		group.Add(c.Account.Id);
		c.Group = group;
		outbox.Groups.Add(group);

		try
		{
			Follow(c, host, outbox);
		}
		catch (Failure e)
		{
			// Não coube na sala do líder: está no grupo, e sabe por que ficou de fora.
			outbox.To(c, ErrorMessage(e.Code, e.Message));
		}
	}

	/// <summary>Quem acabou de entrar no grupo vai para a sala em que o líder
	/// está (ou, se já está nela, para o time dele). Quem está jogando outra
	/// partida fica onde está.</summary>
	private void Follow(Session c, Session leader, Outbox outbox)
	{
		Room room = leader.Room;
		if (room == null || InMatch(c) && c.Room != room) return;
		JoinWithGroup(c, room, "", true, outbox, force: true);
	}

	private void GroupDecline(Session c, long from, Outbox outbox)
	{
		if (!c.GroupInvites.Remove(from)) return;
		if (_sessions.TryGetValue(from, out Session host))
			outbox.To(host, new JsonObject
			{
				["t"] = "group.declined",
				["by"] = new JsonObject { ["id"] = c.Account.Id, ["name"] = c.Account.Name }
			});
	}

	// ------------------------------------------------------------ sair, tirar, liderar

	/// <summary>Tira a conexão do grupo dela. Sobrou um só, o grupo acaba.</summary>
	private void LeaveGroup(Session c, Outbox outbox)
	{
		Group group = c.Group;
		if (group == null) return;

		QueueLeave(c, outbox, "O grupo mudou: a busca parou.");
		group.Remove(c.Account.Id);
		c.Group = null;
		if (!c.Gone) outbox.To(c, NoGroupMessage());

		if (group.IsOver)
		{
			foreach (long id in group.Members)
				if (_sessions.TryGetValue(id, out Session last) && last.Group == group)
				{
					last.Group = null;
					outbox.To(last, NoGroupMessage());
				}
			outbox.Groups.Remove(group);
		}
		else outbox.Groups.Add(group);
	}

	private void GroupKick(Session c, long id, Outbox outbox)
	{
		Group group = c.Group ?? throw GroupRules.NoGroup();
		if (group.Leader != c.Account.Id) throw GroupRules.NotLeader();
		if (id == c.Account.Id || !group.Has(id)) throw GroupRules.NotInGroup();

		if (_sessions.TryGetValue(id, out Session target) && target.Group == group)
		{
			outbox.To(target, new JsonObject
			{
				["t"] = "group.kicked",
				["by"] = new JsonObject { ["id"] = c.Account.Id, ["name"] = c.Account.Name }
			});
			LeaveGroup(target, outbox);
		}
	}

	private void GroupPromote(Session c, long id, Outbox outbox)
	{
		Group group = c.Group ?? throw GroupRules.NoGroup();
		group.Promote(c.Account.Id, id);
		outbox.Groups.Add(group);
	}

	// ------------------------------------------------------------ o grupo e as salas

	/// <summary>A conexão está jogando a partida da sala em que está.</summary>
	private static bool InMatch(Session s) =>
		s.Room is { State: Dominio.Salas.RoomState.Playing } room && room.Seat(s.Account.Id).InMatch;

	/// <summary>Os do grupo que estão nesta sala.</summary>
	private List<Session> Mates(Group group, Room room) =>
		group.Members.Select(id => _sessions.TryGetValue(id, out Session s) ? s : null)
			.Where(s => s != null && s.Room == room).ToList();

	/// <summary>Quem o líder leva para uma sala: o grupo, menos quem está
	/// jogando uma partida em outra sala (ninguém é arrancado da partida).</summary>
	private List<Session> Followers(Group group, Room target) =>
		group.Members.Select(id => _sessions.TryGetValue(id, out Session s) ? s : null)
			.Where(s => s != null && (s.Room == target || s.Account.Id == group.Leader || !InMatch(s))).ToList();

	private string LeaderName(Group group) =>
		_sessions.TryGetValue(group.Leader, out Session leader) ? leader.Account.Name : "";

	/// <summary>O líder abre uma sala e o grupo nasce dentro dela, no time dele.
	/// Se os times da sala são menores que o grupo, ela não abre — e ninguém
	/// saiu de onde estava.</summary>
	private void CreateWithGroup(Session c, JsonObject settings, Outbox outbox)
	{
		Group group = c.Group;
		if (group.Leader != c.Account.Id) throw GroupRules.LeaderPicks(LeaderName(group));

		Room room = Room.Create(_nextRoom, c.Account, c.Xp, settings);
		List<Session> party = Followers(group, null);
		room.JoinTogether(party.Select(s => (s.Account, s.Xp)).ToList(), "", Room.Blue, invited: true);

		_nextRoom++;
		foreach (Session s in party)
		{
			Leave(s, outbox);
			s.Room = room;
		}
		_rooms[room.Id] = room;
		Changed(room, outbox);
	}

	/// <summary>
	/// Alguém de um grupo entra numa sala. O líder leva o grupo
	/// (<see cref="Followers"/>); quem não lidera só entra, sozinho, na sala em
	/// que o líder está, e sem senha (é a sala do grupo). Todos do mesmo lado:
	/// o do líder, se ele já está nela e o grupo cabe; senão o que couber.
	/// <paramref name="force"/>: veio do aceite do convite do grupo, e vale
	/// também para alinhar o time de quem já estava na sala.
	/// </summary>
	private void JoinWithGroup(Session c, Room room, string password, bool invited, Outbox outbox, bool force = false)
	{
		Group group = c.Group;
		_sessions.TryGetValue(group.Leader, out Session leader);

		List<Session> party;
		if (group.Leader == c.Account.Id) party = Followers(group, room);
		else
		{
			if (leader?.Room != room) throw GroupRules.LeaderPicks(leader?.Account.Name ?? "");
			invited = true;
			party = Mates(group, room);
			if (!party.Contains(c)) party.Add(c);
		}
		if (c.Room == room && !force) return;

		bool playing = room.State == Dominio.Salas.RoomState.Playing;
		if (playing && party.Any(s => s.Room != room)) RequireEnterable(room);
		int team = leader?.Room == room ? room.Seat(leader.Account.Id).Team
			: playing ? _fleet.SeatTeam(room.MatchKey, c.Account.Id) : 0;

		// A sala confere tudo antes: recusado, cada um fica onde estava.
		room.JoinTogether(party.Select(s => (s.Account, s.Xp)).ToList(), password, team, invited);
		foreach (Session s in party)
		{
			if (s.Room == room) continue;
			Leave(s, outbox);
			s.Room = room;
		}
		Changed(room, outbox);
	}

	/// <summary>Sai da sala. Se quem sai lidera um grupo, os do grupo que estão
	/// nela, fora de partida, saem junto: o grupo segue o líder.</summary>
	private void LeaveTogether(Session c, Outbox outbox)
	{
		var leaving = new List<Session> { c };
		if (c.Room != null && c.Group is Group group && group.Leader == c.Account.Id)
			leaving.AddRange(Mates(group, c.Room).Where(s => s != c && !InMatch(s)));

		foreach (Session s in leaving)
		{
			Leave(s, outbox);
			outbox.To(s, RoomsMessage());
		}
	}

	/// <summary>Troca de time. Num grupo, com a sala esperando e o líder
	/// nela, o time é do grupo: o líder troca e todos vão; os outros não
	/// trocam sozinhos.</summary>
	private void ChangeTeam(Session c, int team, Outbox outbox)
	{
		Room room = InRoom(c);
		Group group = c.Group;
		List<Session> mates = group == null ? null : Mates(group, room);
		bool together = mates != null && mates.Count > 1 && room.State == Dominio.Salas.RoomState.Waiting
			&& mates.Any(s => s.Account.Id == group.Leader);

		if (!together) room.ChangeTeam(c.Account.Id, team);
		else if (group.Leader != c.Account.Id) throw GroupRules.LeaderPicksTeam();
		else room.ChangeTeamTogether(mates.Select(s => s.Account.Id).ToHashSet(), team);
		RoomState(room, outbox);
	}

	// ------------------------------------------------------------ o que vai para o jogo

	private static JsonObject NoGroupMessage() => new() { ["t"] = "group", ["members"] = new JsonArray() };

	/// <summary>O grupo inteiro, na ordem em que entraram, com onde cada um está.</summary>
	private JsonObject GroupMessage(Group group)
	{
		var members = new JsonArray();
		foreach (long id in group.Members)
		{
			if (!_sessions.TryGetValue(id, out Session s)) continue;
			JsonObject person = Person(s);
			(string status, int? room) = StatusOf(id);
			person["status"] = status;
			person["room"] = room;
			members.Add(person);
		}
		return new JsonObject { ["t"] = "group", ["leader"] = group.Leader, ["max"] = Group.MaxSize, ["members"] = members };
	}

	/// <summary>Manda cada grupo que mudou (<see cref="Outbox.Groups"/>) para
	/// os seus, uma vez. Dentro da trava, junto com a lista de amigos.</summary>
	private void FlushGroups(Outbox outbox)
	{
		if (outbox.Groups.Count == 0) return;
		foreach (Group group in outbox.Groups)
		{
			if (group.IsOver) continue;
			outbox.To(group.Members.Select(id => _sessions.TryGetValue(id, out Session s) && s.Group == group ? s : null), GroupMessage(group));
		}
		outbox.Groups.Clear();
	}
}
