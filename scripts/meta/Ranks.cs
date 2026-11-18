using Godot;

/// <summary>
/// As patentes: 30, em dez famílias de três graus.
///
/// Nomes e insígnias são próprios do projeto. A insígnia distingue
/// família por forma e cor antes do detalhe fino (o grau): com trinta
/// patentes, o olho precisa reconhecer "é um Caçador" antes de contar
/// losangos.
///
/// A curva (pedido do Jhon, 17/11: subir tem de custar muito, e cada vez
/// mais): XP para a patente <c>r</c> = 230 × r^2,6, arredondado de 10 em
/// 10. Com uns 100 XP por partida (<see cref="Rewards"/>):
///
/// | patente | XP | partidas (total) | a mais que a anterior |
/// |---|---|---|---|
/// | 1 Filhote II | 230 | ~2 | 2 |
/// | 2 Filhote III | 1.390 | ~14 | 12 |
/// | 3 Batedor I | 4.000 | ~40 | 26 |
/// | 6 Rastreador I | 24.260 | ~240 | 92 |
/// | 9 Caçador I | 69.620 | ~700 | 184 |
/// | 15 Uivador I | 262.760 | ~2.600 | 430 |
/// | 21 Lua Cheia I | 630.230 | ~6.300 | 750 |
/// | 29 Alfa III | 1.458.690 | ~14.600 | 1.270 |
///
/// Jogando 5 partidas por dia, a última patente leva uns 8 anos (o alvo
/// do Jhon, docs/PENDENCIAS.md). Antes: 250 × r^1,9 com 400 XP por
/// partida, ~430 partidas até o topo.
///
/// Sem rebaixamento: patente de progressão não cai.
/// </summary>
public static class Ranks
{
	public const int Count = 30;

	/// <summary>Sem patente: os bots (decisão do Jhon, 13/11). Patente é
	/// de quem joga; o bot aparece sem insígnia, só com "Bot".</summary>
	public const int None = -1;

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

	public static string NameOf(int rank) => rank < 0 ? "Bot" : $"{Families[Family(rank)]} {Roman[Tier(rank)]}";
	public static string FamilyName(int rank) => Families[Family(rank)];
	public static Color ColorOf(int rank) => rank < 0 ? new Color("8a93a0") : FamilyColors[Family(rank)];

	/// <summary>A cor da família para **texto**: a mesma, um pouco mais
	/// clara, para as famílias de bronze não ficarem apagadas no escuro.</summary>
	public static Color InkOf(int rank) => ColorOf(rank).Lightened(0.18f);

	/// <summary>XP acumulado necessário para chegar à patente.</summary>
	public static long XpFor(int rank)
	{
		rank = Clamp(rank);
		if (rank == 0) return 0;
		return (long)(Mathf.Round(230.0 * Mathf.Pow(rank, 2.6) / 10.0) * 10.0);
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
}
