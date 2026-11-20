using System;
using System.Collections.Generic;

namespace Alcateia.Dominio.Painel;

/// <summary>O que cada ação do painel exige.</summary>
public enum Permission
{
	/// <summary>Ver tudo (visão geral, jogadores, catálogo, segurança).</summary>
	View,
	/// <summary>Banir, desbanir, derrubar sessão, bloquear IP.</summary>
	Moderate,
	/// <summary>Catálogo, mapas, promoções, desafios.</summary>
	Content,
	/// <summary>Dar e tirar moeda e item.</summary>
	Economy,
	/// <summary>Configurações do servidor e administradores.</summary>
	Manage
}

/// <summary>
/// Os papéis do painel. Autorização é por ação, não só "está logado"
/// (lição do CT): cada rota diz o que exige.
/// </summary>
public static class Roles
{
	public const string Owner = "dono", Admin = "admin", Moderator = "moderador", Viewer = "leitor";

	public static readonly string[] All = { Owner, Admin, Moderator, Viewer };

	private static readonly Dictionary<string, HashSet<Permission>> Grants = new()
	{
		[Owner] = new() { Permission.View, Permission.Moderate, Permission.Content, Permission.Economy, Permission.Manage },
		[Admin] = new() { Permission.View, Permission.Moderate, Permission.Content, Permission.Economy },
		[Moderator] = new() { Permission.View, Permission.Moderate },
		[Viewer] = new() { Permission.View }
	};

	public static bool Valid(string role) => Grants.ContainsKey(role ?? "");

	public static bool Can(string role, Permission permission) =>
		Grants.TryGetValue(role ?? "", out HashSet<Permission> set) && set.Contains(permission);

	public static IEnumerable<Permission> Of(string role) =>
		Grants.TryGetValue(role ?? "", out HashSet<Permission> set) ? set : Array.Empty<Permission>();
}

/// <summary>Um administrador do painel. Conta separada das de jogador:
/// nome de jogador não entra no painel, e vice-versa.</summary>
public sealed record AdminUser(long Id, string Name, string Role, bool TotpEnabled, bool Disabled, DateTime CreatedAt, DateTime? LastLoginAt, string LastIp);

/// <summary>Regras de conta do painel: senha mais comprida que a do jogo e
/// trava depois de poucas tentativas.</summary>
public static class AdminRules
{
	public const int PasswordMin = 12, PasswordMax = 128;
	public const int MaxFailedLogins = 5;
	public static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);
	public static readonly TimeSpan SessionLife = TimeSpan.FromHours(12);

	public static string CheckNew(string name, string password, string role)
	{
		name = (name ?? "").Trim();
		if (name.Length is < 3 or > 32) throw new Failure(FailureKind.Invalid, "nome_invalido", "O nome tem de 3 a 32 caracteres.");
		foreach (char c in name)
			if (!char.IsLetterOrDigit(c) && c is not ('_' or '-' or '.'))
				throw new Failure(FailureKind.Invalid, "nome_invalido", "Use letras, números, ponto, _ ou -.");
		CheckPassword(password);
		if (!Roles.Valid(role)) throw new Failure(FailureKind.Invalid, "papel_invalido", "Papel inválido.");
		return name;
	}

	public static void CheckPassword(string password)
	{
		if (password == null || password.Length < PasswordMin || password.Length > PasswordMax)
			throw new Failure(FailureKind.Invalid, "senha_fraca", $"A senha do painel precisa de pelo menos {PasswordMin} caracteres.");
	}
}

/// <summary>Uma linha da auditoria: quem fez o quê, em quem, de onde.</summary>
public sealed record AuditEntry(long Id, DateTime At, string AdminName, string Action, string Target, string Detail, string Ip);
