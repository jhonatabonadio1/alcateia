using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// Os amigos (Jhon, 27/11: "deixe a parte do social, amigos, igual
/// Valorant, inclusive convidar, aceitar pedidos"): quem são, quem está
/// conectado e onde, os pedidos que chegaram e os que eu mandei, e os
/// convites para sala.
///
/// E o grupo (27/11: "criar grupo para jogarem juntos no mesmo time"): os
/// amigos que andam comigo pelo saguão. O líder escolhe a sala e os outros
/// vão junto, para o mesmo time — as regras são do servidor
/// (<c>LobbyService.Groups.cs</c>); aqui fica quem está nele e os convites.
///
/// Pede-se amizade pelo nome com a tag (<c>Nome#0042</c>; a de cada um
/// aparece no painel social e no perfil).
///
/// Quem sabe disso é o servidor: a lista chega pelo saguão
/// (<see cref="LobbyLink"/>, mensagem <c>friends</c>) ao conectar e toda
/// vez que algo muda — um amigo entrou, saiu, foi para uma sala. Aqui fica
/// só o espelho do que veio, para as telas lerem, e os pedidos que as telas
/// fazem viram mensagens ao servidor. Nada é decidido deste lado.
///
/// O contrato das mensagens está em docs/SERVIDOR.md ("Amigos").
/// </summary>
public static class Social
{
	/// <summary>Onde um amigo está agora. <c>App</c>: com o jogo fechado e o
	/// cliente de instalação aberto — dá para ver, não dá para convidar.</summary>
	public enum Presence { Offline, App, Online, Room, Playing }

	public sealed record Person(long Id, string Name, long Xp, string Photo, Presence Status = Presence.Offline, int Room = 0, string Tag = "")
	{
		public bool Connected => Status != Presence.Offline;

		/// <summary>Está com o jogo aberto: dá para convidar para sala e grupo.</summary>
		public bool InGame => Status is Presence.Online or Presence.Room or Presence.Playing;
		public int Rank => Ranks.RankFor(Xp);

		/// <summary>O nome com a tag (Nome#0042).</summary>
		public string Handle => string.IsNullOrEmpty(Tag) ? Name : $"{Name}#{Tag}";
	}

	/// <summary>Um convite para o grupo de um amigo, à espera de resposta.</summary>
	public sealed record GroupInvite(Person From, int Size, double At);

	/// <summary>Um convite para a sala de um amigo, à espera de resposta.</summary>
	public sealed record Invite(Person From, int Room, string Title, string Map, int Players, int Capacity, double At);

	public static IReadOnlyList<Person> Friends => _friends;
	public static IReadOnlyList<Person> Requests => _requests;
	public static IReadOnlyList<Person> Sent => _sent;
	public static IReadOnlyList<Invite> Invites => _invites;

	/// <summary>O meu grupo, na ordem em que entraram (vazio: sem grupo), quem
	/// lidera e quantos cabem.</summary>
	public static IReadOnlyList<Person> Group => _group;
	public static long GroupLeader { get; private set; }
	public static int GroupMax { get; private set; } = 6;
	public static IReadOnlyList<GroupInvite> GroupInvites => _groupInvites;

	public static bool InGroup => _group.Count > 1;
	public static bool LeadsGroup => InGroup && GroupLeader == Backend.AccountId;
	public static bool InMyGroup(long id) => InGroup && _group.Any(p => p.Id == id);

	private static List<Person> _friends = new(), _requests = new(), _sent = new(), _group = new();
	private static readonly List<Invite> _invites = new();
	private static readonly List<GroupInvite> _groupInvites = new();

	/// <summary>A lista já chegou do servidor nesta conexão.</summary>
	public static bool Loaded { get; private set; }

	/// <summary>O servidor desta conexão é de antes dos amigos: a tela diz
	/// isso em vez de mostrar uma lista vazia.</summary>
	public static bool Unsupported { get; private set; }

	/// <summary>A lista, os pedidos ou os convites mudaram.</summary>
	public static event Action Changed;

	/// <summary>Chegou um convite para sala (a tela mostra o cartão).</summary>
	public static event Action<Invite> Invited;

	/// <summary>Chegou um convite para grupo.</summary>
	public static event Action<GroupInvite> GroupInvited;

	/// <summary>Algo para avisar num toast: "Fulano aceitou o seu pedido".</summary>
	public static event Action<string> Notice;

	public static int OnlineCount => _friends.Count(f => f.Connected);

	/// <summary>Quanto tempo um convite fica na tela, em segundos (o
	/// servidor guarda por uns 2 minutos).</summary>
	public const double InviteSeconds = 60;

	// ------------------------------------------------------------------ pedidos ao servidor

	private static LobbyLink Link => LobbyLink.Instance;

	public static void Refresh() => Link.SendFriend("friends.list");
	public static void Add(string name) => Link.SendFriend("friend.add", ("name", (name ?? "").Trim()));
	public static void Accept(long id) => Link.SendFriend("friend.accept", ("id", id));
	public static void Decline(long id) => Link.SendFriend("friend.decline", ("id", id));
	public static void Remove(long id) => Link.SendFriend("friend.remove", ("id", id));
	public static void InviteToRoom(long id) => Link.SendFriend("friend.invite", ("id", id));

	public static void AcceptInvite(Invite invite)
	{
		Drop(invite);
		Link.SendFriend("invite.accept", ("from", invite.From.Id));
	}

	public static void DeclineInvite(Invite invite)
	{
		Drop(invite);
		Link.SendFriend("invite.decline", ("from", invite.From.Id));
	}

	private static void Drop(Invite invite)
	{
		if (_invites.Remove(invite)) Changed?.Invoke();
	}

	// ---- grupo
	public static void InviteToGroup(long id) => Link.SendFriend("group.invite", ("id", id));
	public static void LeaveGroup() => Link.SendFriend("group.leave");
	public static void KickFromGroup(long id) => Link.SendFriend("group.kick", ("id", id));
	public static void PromoteInGroup(long id) => Link.SendFriend("group.lead", ("id", id));

	public static void AcceptGroup(GroupInvite invite)
	{
		if (_groupInvites.Remove(invite)) Changed?.Invoke();
		Link.SendFriend("group.accept", ("from", invite.From.Id));
	}

	public static void DeclineGroup(GroupInvite invite)
	{
		if (_groupInvites.Remove(invite)) Changed?.Invoke();
		Link.SendFriend("group.decline", ("from", invite.From.Id));
	}

	/// <summary>Tira os convites vencidos (a tela chama de tempos em tempos).</summary>
	public static void Expire(double now)
	{
		int gone = _invites.RemoveAll(i => now - i.At > InviteSeconds) + _groupInvites.RemoveAll(i => now - i.At > InviteSeconds);
		if (gone > 0) Changed?.Invoke();
	}

	// ------------------------------------------------------------------ o que chega

	/// <summary>Uma mensagem do saguão. Verdadeiro se era de amigos.</summary>
	public static bool Apply(JsonObject m, double now)
	{
		switch (m["t"]?.GetValue<string>())
		{
			case "friends":
				_friends = People(m["friends"]);
				_requests = People(m["requests"]);
				_sent = People(m["sent"]);
				Loaded = true;
				Unsupported = false;
				// Convite de quem não é mais amigo, ou saiu, não vale mais.
				_invites.RemoveAll(i => _friends.All(f => f.Id != i.From.Id || !f.InGame));
				_groupInvites.RemoveAll(i => _friends.All(f => f.Id != i.From.Id || !f.InGame));
				Changed?.Invoke();
				return true;

			case "group":
				bool had = InGroup;
				_group = People(m["members"]);
				GroupLeader = m["leader"]?.GetValue<long>() ?? 0;
				GroupMax = m["max"]?.GetValue<int>() ?? GroupMax;
				// Entrei num grupo: os outros convites para grupo não valem mais.
				if (InGroup) _groupInvites.Clear();
				if (had && !InGroup) Notice?.Invoke(Loc.T("O grupo acabou"));
				Changed?.Invoke();
				return true;

			case "group.invited":
				if (PersonOf(m["from"]) is not Person leader) return true;
				var asked = new GroupInvite(leader, m["size"]?.GetValue<int>() ?? 1, now);
				_groupInvites.RemoveAll(i => i.From.Id == leader.Id);
				_groupInvites.Add(asked);
				GroupInvited?.Invoke(asked);
				Changed?.Invoke();
				return true;

			case "group.declined":
				if (m["by"]?["name"]?.GetValue<string>() is string refused) Notice?.Invoke(Loc.F("{0} recusou o convite para o grupo", refused));
				return true;

			case "group.kicked":
				if (m["by"]?["name"]?.GetValue<string>() is string boss) Notice?.Invoke(Loc.F("{0} tirou você do grupo", boss));
				return true;

			case "friend.request":
				if (PersonOf(m["from"]) is Person wants) Notice?.Invoke(Loc.F("{0} quer ser seu amigo", wants.Name));
				return true;

			case "friend.accepted":
				if (PersonOf(m["by"]) is Person accepted) Notice?.Invoke(Loc.F("{0} aceitou o seu pedido de amizade", accepted.Name));
				return true;

			case "friend.invited":
				if (PersonOf(m["from"]) is not Person from) return true;
				JsonNode room = m["room"];
				var invite = new Invite(from,
					room?["id"]?.GetValue<int>() ?? 0,
					room?["title"]?.GetValue<string>() ?? "",
					room?["map"]?.GetValue<string>() ?? "",
					room?["players"]?.GetValue<int>() ?? 0,
					room?["capacity"]?.GetValue<int>() ?? 0,
					now);
				_invites.RemoveAll(i => i.From.Id == from.Id);
				_invites.Add(invite);
				Invited?.Invoke(invite);
				Changed?.Invoke();
				return true;

			case "invite.declined":
				if (PersonOf(m["by"]) is Person declined) Notice?.Invoke(Loc.F("{0} recusou o convite", declined.Name));
				return true;
		}
		return false;
	}

	/// <summary>O servidor não conhece as mensagens de amigos (versão antiga).</summary>
	public static void MarkUnsupported()
	{
		if (Unsupported) return;
		Unsupported = true;
		Changed?.Invoke();
	}

	/// <summary>Caiu a ligação, saiu da conta: a lista é de quem está conectado.</summary>
	public static void Reset()
	{
		_friends = new();
		_requests = new();
		_sent = new();
		_group = new();
		GroupLeader = 0;
		_invites.Clear();
		_groupInvites.Clear();
		Loaded = false;
		Changed?.Invoke();
	}

	private static List<Person> People(JsonNode list)
	{
		var people = new List<Person>();
		foreach (JsonNode node in list as JsonArray ?? new JsonArray())
			if (PersonOf(node) is Person person) people.Add(person);
		return people;
	}

	private static Person PersonOf(JsonNode node)
	{
		if (node?["id"] == null) return null;
		Presence status = node["status"]?.GetValue<string>() switch
		{
			"app" => Presence.App,
			"online" => Presence.Online,
			"room" => Presence.Room,
			"playing" => Presence.Playing,
			_ => Presence.Offline
		};
		return new Person(node["id"].GetValue<long>(), node["name"]?.GetValue<string>() ?? "",
			node["xp"]?.GetValue<long>() ?? 0, node["photo"]?.GetValue<string>(), status,
			node["room"]?.GetValue<int?>() ?? 0, node["tag"]?.GetValue<string>() ?? "");
	}
}
