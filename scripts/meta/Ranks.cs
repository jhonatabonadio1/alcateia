using Godot;

/// <summary>
/// As patentes: 30, em dez famílias de três graus.
///
/// Nomes e insígnias são próprios do projeto. A insígnia distingue
/// família por forma e cor antes do detalhe fino (o grau): com trinta
/// patentes, o olho precisa reconhecer "é um Caçador" antes de contar
/// losangos.
///
/// A curva: as três primeiras saem numa sessão, as altas levam meses.
/// XP para a patente <c>r</c> = 250 × r^1,9, arredondado de 10 em 10.
/// Com uns 400 XP por partida, Batedor I sai na quarta ou quinta partida
/// e Alfa III passa de 300 partidas.
///
/// Sem rebaixamento: patente de progressão não cai.
/// </summary>
public static class Ranks
{
	public const int Count = 30;

	private static readonly string[] Families =
	{
		"Filhote", "Batedor", "Rastreador", "Caçador", "Sentinela",
		"Uivador", "Presa de Ferro", "Lua Cheia", "Ancião", "Alfa"
	};

	/// <summary>Cor de cada família: bronze, prata, ouro, aço, platina...</summary>
	private static readonly Color[] FamilyColors =
	{
		new("b08d6a"), new("c49a6c"), new("b9c1cb"), new("d4dbe3"), new("e0b84f"),
		new("f2c94c"), new("7fb0d9"), new("c9d6f0"), new("b48cff"), new("ff6a3d")
	};

	private static readonly string[] Roman = { "I", "II", "III" };

	public static int Clamp(int rank) => Mathf.Clamp(rank, 0, Count - 1);

	public static int Family(int rank) => Clamp(rank) / 3;
	public static int Tier(int rank) => Clamp(rank) % 3;

	public static string NameOf(int rank) => $"{Families[Family(rank)]} {Roman[Tier(rank)]}";
	public static string FamilyName(int rank) => Families[Family(rank)];
	public static Color ColorOf(int rank) => FamilyColors[Family(rank)];

	/// <summary>XP acumulado necessário para chegar à patente.</summary>
	public static long XpFor(int rank)
	{
		rank = Clamp(rank);
		if (rank == 0) return 0;
		return (long)(Mathf.Round(250.0 * Mathf.Pow(rank, 1.9) / 10.0) * 10.0);
	}

	public static int RankFor(long xp)
	{
		int rank = 0;
		while (rank + 1 < Count && xp >= XpFor(rank + 1)) rank++;
		return rank;
	}

	/// <summary>Quanto da patente atual já foi percorrido, de 0 a 1. Na
	/// última patente, sempre 1.</summary>
	public static float Progress(long xp)
	{
		int rank = RankFor(xp);
		if (rank >= Count - 1) return 1f;

		long from = XpFor(rank);
		long to = XpFor(rank + 1);
		return Mathf.Clamp((float)(xp - from) / (to - from), 0f, 1f);
	}

	/// <summary>Presas de recompensa ao ser promovido para a patente.
	/// Crescem com a família: promoção alta tem que valer a espera.</summary>
	public static int PromotionReward(int rank) => 150 + 100 * Family(rank);

	/// <summary>Patente fixa de um bot, derivada do nome — sempre a
	/// mesma para o mesmo bot, para o placar não sortear a cada partida.</summary>
	public static int ForBot(string name, int difficulty)
	{
		int hash = 0;
		foreach (char c in name ?? "") hash = hash * 31 + c;
		int baseRank = 2 + difficulty * 5;
		return Clamp(baseRank + Mathf.Abs(hash) % 6);
	}
}
