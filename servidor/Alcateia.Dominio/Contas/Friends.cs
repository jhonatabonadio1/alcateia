namespace Alcateia.Dominio.Contas;

/// <summary>O que uma conta é da outra na lista de amigos.</summary>
public enum FriendKind
{
	/// <summary>Amigos: o pedido foi aceito.</summary>
	Friend,

	/// <summary>A outra conta me pediu e eu ainda não respondi.</summary>
	Incoming,

	/// <summary>Eu pedi e a outra conta ainda não respondeu.</summary>
	Outgoing
}

/// <summary>Uma linha da lista de amigos de uma conta: a outra conta (com
/// o nome e o XP de quando a lista foi lida) e o que ela é.</summary>
public sealed record FriendLink(long Id, string Name, long Xp, FriendKind Kind, int Tag = 0);

/// <summary>Uma conta achada pelo nome, para pedir amizade. O pedido só
/// vale se a tag escrita for a dela.</summary>
public sealed record FriendTarget(long Id, string Name, long Xp, int Tag = 0);

/// <summary>O que um pedido de amizade virou.</summary>
public enum FriendRequestOutcome
{
	/// <summary>O pedido ficou esperando a resposta.</summary>
	Requested,

	/// <summary>A outra conta já tinha me pedido: viraram amigos na hora.</summary>
	Accepted
}

/// <summary>
/// As regras da lista de amigos (Jhon, 27/11: "a parte do social (amigos),
/// inclusive convidar, aceitar pedidos"). A amizade é de mão dupla e nasce
/// de um pedido aceito; pedidos cruzados viram amizade sem esperar ninguém.
/// Os tetos existem para a lista caber numa mensagem e para ninguém encher
/// a caixa dos outros.
/// </summary>
public static class FriendRules
{
	/// <summary>Amigos por conta, no máximo.</summary>
	public const int MaxFriends = 100;

	/// <summary>Pedidos esperando resposta, no máximo: os que a conta
	/// mandou e, à parte, os que ela recebeu.</summary>
	public const int MaxPending = 50;

	public static Failure Self() =>
		new(FailureKind.Invalid, "amigo_voce", "Você não pode adicionar a si mesmo.");

	/// <summary>Nome que não existe e tag errada respondem igual: quem chuta
	/// tags não descobre se o nome existe.</summary>
	public static Failure NoAccount() =>
		new(FailureKind.NotFound, "conta_inexistente", "Não existe conta com esse nome e essa tag.");

	public static Failure NoTag() =>
		new(FailureKind.Invalid, "tag_faltando", "Escreva o nome com a tag, assim: Nome#0000. A tag de cada um aparece no perfil.");

	public static Failure AlreadyFriends() =>
		new(FailureKind.Conflict, "ja_amigos", "Vocês já são amigos.");

	public static Failure AlreadyRequested() =>
		new(FailureKind.Conflict, "pedido_ja_enviado", "Você já mandou um pedido para essa conta.");

	public static Failure ListFull() =>
		new(FailureKind.Conflict, "amigos_cheio", $"A sua lista de amigos está cheia ({MaxFriends}).");

	public static Failure TheirListFull() =>
		new(FailureKind.Conflict, "amigos_cheio_dele", "A lista de amigos dessa conta está cheia.");

	public static Failure TooManySent() =>
		new(FailureKind.Conflict, "pedidos_demais", $"Você tem pedidos demais esperando resposta ({MaxPending}). Cancele algum.");

	public static Failure TheirInboxFull() =>
		new(FailureKind.Conflict, "pedidos_demais_dele", "Essa conta tem pedidos demais esperando resposta.");

	public static Failure NoRequest() =>
		new(FailureKind.NotFound, "pedido_inexistente", "Esse pedido de amizade não existe mais.");

	public static Failure NotFriends() =>
		new(FailureKind.NotFound, "nao_e_amigo", "Essa conta não está na sua lista de amigos.");

	public static Failure FriendOffline() =>
		new(FailureKind.Conflict, "amigo_offline", "Esse amigo não está conectado.");

	public static Failure FriendNotInGame() =>
		new(FailureKind.Conflict, "amigo_fora_do_jogo", "Esse amigo está com o jogo fechado.");

	public static Failure AlreadyHere() =>
		new(FailureKind.Conflict, "ja_na_sala", "Esse amigo já está na sua sala.");

	public static Failure InviteGone() =>
		new(FailureKind.NotFound, "convite_vencido", "Esse convite não vale mais.");

	public static Failure Unavailable() =>
		new(FailureKind.Unavailable, "amigos_indisponivel", "A lista de amigos está fora do ar. Tente de novo em instantes.");
}
