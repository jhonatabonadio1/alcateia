using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Alcateia.Dominio.Contas;

/// <summary>Quem está falando: a conta por trás de um token.</summary>
public sealed record Account(long Id, string Name);

/// <summary>
/// O que é um nome e uma senha aceitáveis. O nome é único sem diferenciar
/// maiúsculas: guarda-se como foi escrito e, à parte, a chave em
/// minúsculas (<see cref="Key"/>).
/// </summary>
public static class AccountRules
{
	public const int NameMin = 3, NameMax = 16;
	public const int PasswordMin = 8, PasswordMax = 128;

	/// <summary>Presas com que toda conta nova começa.</summary>
	public const long StarterPresas = 3000;

	private static readonly Regex ValidName = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

	public static string Clean(string name) => (name ?? "").Trim();

	public static string Key(string name) => Clean(name).ToLowerInvariant();

	/// <summary>Confere nome e senha de uma conta nova; devolve o nome limpo.</summary>
	public static string CheckNew(string name, string password)
	{
		name = Clean(name);
		if (name.Length < NameMin || name.Length > NameMax || !ValidName.IsMatch(name))
			throw new Failure(FailureKind.Invalid, "nome_invalido", $"O nome tem de {NameMin} a {NameMax} letras, números ou _.");
		if (password == null || password.Length < PasswordMin || password.Length > PasswordMax)
			throw new Failure(FailureKind.Invalid, "senha_fraca", $"A senha precisa de pelo menos {PasswordMin} caracteres.");
		return name;
	}
}

/// <summary>O perfil: o que o servidor manda (XP, Presas, estatísticas) e o
/// que é só do jogador (<see cref="DataJson"/>: conjuntos, personagem).</summary>
public sealed record Profile(long Id, string Name, string CreatedAt, long Xp, long Presas,
	IReadOnlyDictionary<string, int> Stats, string DataJson)
{
	/// <summary>As estatísticas, na ordem em que vão para o cliente.</summary>
	public static readonly string[] StatNames = { "matches", "wins", "losses", "draws", "kills", "deaths", "assists", "headshots", "mvps" };

	/// <summary>Teto do que o jogador guarda no perfil.</summary>
	public const int MaxData = 64 * 1024;
}

/// <summary>Uma linha do ranking (por XP).</summary>
public sealed record RankingEntry(int Position, string Name, long Xp, int Kills, int Deaths, int Wins, int Matches);
