using System.Collections.Generic;

namespace Alcateia.Dominio.Salas;

/// <summary>
/// Um grupo (Jhon, 27/11: "criar grupo para jogarem juntos no mesmo time"):
/// amigos que andam juntos pelo saguão. Quem escolhe a sala é o líder, e os
/// outros vão com ele, para o mesmo time.
///
/// Aqui ficam só quem está no grupo, quem lidera e os tetos; o que o grupo
/// faz com as salas é do saguão (<c>LobbyService.Groups.cs</c>) e da sala
/// (<see cref="Room.JoinTogether"/>, <see cref="Room.ChangeTeamTogether"/>).
/// O grupo vive em memória, como a sala: é coisa de agora.
/// </summary>
public sealed class Group
{
	/// <summary>Gente por grupo, no máximo: um time inteiro
	/// (<see cref="Room.MaxTeamSize"/>). Maior que isso não haveria sala em
	/// que coubessem todos do mesmo lado.</summary>
	public const int MaxSize = Room.MaxTeamSize;

	private readonly List<long> _members = new();

	/// <summary>Quem está no grupo, na ordem em que entrou. O primeiro é o
	/// mais antigo: é ele quem assume se o líder sair.</summary>
	public IReadOnlyList<long> Members => _members;

	public long Leader { get; private set; }

	public bool IsFull => _members.Count >= MaxSize;

	/// <summary>Sobrou um só (ou ninguém): não é mais um grupo.</summary>
	public bool IsOver => _members.Count < 2;

	public Group(long leader)
	{
		Leader = leader;
		_members.Add(leader);
	}

	public bool Has(long accountId) => _members.Contains(accountId);

	public void Add(long accountId)
	{
		if (Has(accountId)) return;
		if (IsFull) throw GroupRules.Full();
		_members.Add(accountId);
	}

	/// <summary>Tira alguém. Se era o líder, o mais antigo que ficou assume.</summary>
	public void Remove(long accountId)
	{
		if (!_members.Remove(accountId)) return;
		if (Leader == accountId && _members.Count > 0) Leader = _members[0];
	}

	/// <summary>O líder passa a liderança para outro do grupo.</summary>
	public void Promote(long by, long to)
	{
		if (Leader != by) throw GroupRules.NotLeader();
		if (!Has(to)) throw GroupRules.NotInGroup();
		Leader = to;
	}
}

/// <summary>As recusas do grupo.</summary>
public static class GroupRules
{
	public static Failure Full() =>
		new(FailureKind.Conflict, "grupo_cheio", $"O grupo está cheio ({Group.MaxSize}).");

	public static Failure NoGroup() =>
		new(FailureKind.Conflict, "sem_grupo", "Você não está num grupo.");

	public static Failure NotLeader() =>
		new(FailureKind.Forbidden, "nao_e_lider", "Só o líder do grupo pode fazer isso.");

	public static Failure NotInGroup() =>
		new(FailureKind.NotFound, "fora_do_grupo", "Essa conta não está no seu grupo.");

	public static Failure AlreadyIn() =>
		new(FailureKind.Conflict, "ja_no_grupo", "Esse amigo já está no seu grupo.");

	public static Failure InviteGone() =>
		new(FailureKind.NotFound, "convite_vencido", "Esse convite não vale mais.");

	/// <summary>Quem não é o líder tentou escolher a sala.</summary>
	public static Failure LeaderPicks(string leader) =>
		new(FailureKind.Conflict, "so_o_lider", $"Você está num grupo: quem escolhe a sala é o líder ({leader}). Saia do grupo para entrar sozinho.");

	/// <summary>Quem não é o líder tentou trocar de time.</summary>
	public static Failure LeaderPicksTeam() =>
		new(FailureKind.Conflict, "so_o_lider", "O grupo joga no mesmo time: quem troca de time é o líder.");

	/// <summary>A sala não tem lugar para o grupo inteiro.</summary>
	public static Failure NoRoom(int size) =>
		new(FailureKind.Conflict, "grupo_nao_cabe", $"A sala não tem lugar para o grupo inteiro ({size}).");

	/// <summary>Nenhum time tem lugar para o grupo inteiro do mesmo lado.</summary>
	public static Failure NoTeam(int size) =>
		new(FailureKind.Conflict, "grupo_nao_cabe", $"Nenhum time tem lugar para o grupo inteiro ({size}) do mesmo lado.");
}
