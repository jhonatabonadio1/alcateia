using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Alcateia.Dominio.Contas;

/// <summary>Quem está falando: a conta por trás de um token.
/// <paramref name="Tag"/>: os quatro números que vão depois do nome
/// (<c>Nome#0042</c>) e que é preciso saber para pedir amizade
/// (<see cref="AccountRules.Handle"/>).</summary>
public sealed record Account(long Id, string Name, int Tag = 0);

/// <summary>
/// O que é um nome e uma senha aceitáveis. O nome é único sem diferenciar
/// maiúsculas: guarda-se como foi escrito e, à parte, a chave em
/// minúsculas (<see cref="Key"/>).
/// </summary>
public static class AccountRules
{
	public const int NameMin = 3, NameMax = 16;
	public const int PasswordMin = 8, PasswordMax = 128;

	/// <summary>Cash (Presas no código) com que toda conta nova começa: nenhum
	/// (Jhon, 25/11). O IPs (Sangue) também começa em zero.</summary>
	public const long StarterPresas = 0;

	private static readonly Regex ValidName = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

	public static string Clean(string name) => (name ?? "").Trim();

	public static string Key(string name) => Clean(name).ToLowerInvariant();

	/// <summary>Quantas tags existem: de 0000 a 9999 (Jhon, 27/11: "cada
	/// player terá uma tag #0000 para adicionar"). A tag é sorteada quando a
	/// conta nasce e não muda, nem na troca de nome. O nome continua único
	/// (é com ele que se entra); a tag é o que só quem a pessoa contou sabe:
	/// ver um nome no placar ou no ranking não basta para encher a caixa de
	/// pedidos dela.</summary>
	public const int TagCount = 10000;

	/// <summary>A tag como se escreve: quatro algarismos.</summary>
	public static string TagText(int tag) => tag.ToString("D4");

	/// <summary>O nome com a tag, como o jogador passa para os amigos.</summary>
	public static string Handle(string name, int tag) => $"{name}#{TagText(tag)}";

	/// <summary>Separa <c>Nome#0042</c> em nome e tag. Falso se não está
	/// nesse formato (sem #, tag que não é de um a quatro algarismos).</summary>
	public static bool TryParseHandle(string text, out string name, out int tag)
	{
		name = "";
		tag = 0;
		text = Clean(text);
		int at = text.LastIndexOf('#');
		if (at <= 0) return false;
		string digits = text[(at + 1)..].Trim();
		if (digits.Length is 0 or > 4) return false;
		foreach (char c in digits)
			if (c is < '0' or > '9') return false;
		name = text[..at].Trim();
		tag = int.Parse(digits);
		return name.Length > 0;
	}

	/// <summary>O item da loja que paga uma troca de nome.</summary>
	public const string RenameItem = "troca_nome";

	/// <summary>Confere um nome de conta; devolve o nome limpo.</summary>
	public static string CheckName(string name)
	{
		name = Clean(name);
		if (name.Length < NameMin || name.Length > NameMax || !ValidName.IsMatch(name))
			throw new Failure(FailureKind.Invalid, "nome_invalido", $"O nome tem de {NameMin} a {NameMax} letras, números ou _.");
		return name;
	}

	/// <summary>Confere nome e senha de uma conta nova; devolve o nome limpo.</summary>
	public static string CheckNew(string name, string password)
	{
		name = CheckName(name);
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
	/// <summary>A moeda paga (só compra o que não dá poder).</summary>
	public long Sangue { get; init; }

	/// <summary>A tag da conta (<see cref="AccountRules.Handle"/>).</summary>
	public int Tag { get; init; }

	/// <summary>A conta pede o código do aplicativo autenticador para entrar.</summary>
	public bool TwoFactor { get; init; }

	/// <summary>A nota de habilidade do jogo rápido (<c>Dominio/Fila/SkillRules</c>)
	/// e quantas partidas de fila a formaram. Null: nunca jogou pela fila — a
	/// largada sai do histórico. Não vai para o jogo: é do formador de partidas.</summary>
	public double? Skill { get; init; }
	public int SkillMatches { get; init; }

	/// <summary>A patente, tirada do XP.</summary>
	public int Rank => RankRule.RankFor(Xp);

	/// <summary>As estatísticas, na ordem em que vão para o cliente.</summary>
	public static readonly string[] StatNames = { "matches", "wins", "losses", "draws", "kills", "deaths", "assists", "headshots", "mvps" };

	/// <summary>Teto do que o jogador guarda no perfil.</summary>
	public const int MaxData = 64 * 1024;
}

/// <summary>Uma linha do ranking (por XP).</summary>
public sealed record RankingEntry(int Position, string Name, long Xp, int Kills, int Deaths, int Wins, int Matches);
