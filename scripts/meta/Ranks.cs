using Godot;

/// <summary>
/// Os **níveis** (Jhon, 21/11: "ao invés de patentes, será níveis; cada
/// nível tem uma borda", no modelo do Overwatch). O nome da classe ficou
/// <c>Ranks</c> e o número continua sendo o "rank" no código, no servidor
/// e no banco: <c>rank</c> 0 é o **nível 1**. Mudou a regra, não o dado —
/// o XP de cada conta é o mesmo, só passa a ser lido como nível.
///
/// Como se lê um nível L (= rank + 1):
///
/// | L | metal | estrelas | número na placa |
/// |---|---|---|---|
/// | 1–99 | Iniciante (cinza) | 0 | L |
/// | 100–599 | Bronze | 1 a 5, uma a cada 100 | L % 100 |
/// | 600–1099 | Prata | 1 a 5 | L % 100 |
/// | 1100–1599 | Ouro | … | … |
/// | 1600–2099 | Platina | | |
/// | 2100–2599 | Diamante | | |
/// | 2600–3000 | Mestre | | |
///
/// O nível 520 aparece "20 ★★★★★" em bronze. A borda (assets/ui/bordas/)
/// muda a cada 10 níveis dentro do metal (asas, louros, coroa) — 7 metais
/// × 10 desenhos, gerados por tools/bordas.py.
///
/// A curva: do nível L ao L+1 custa 100 + 10 × (L−1) XP, até o teto de
/// 1.000 (a partir do 91). Uma partida rende uns 70 a 100 XP
/// (<see cref="Rewards"/>): o nível 2 sai na primeira partida, o 10 em
/// umas 12, o 100 (bronze) em umas 600, e daí ~10 partidas por nível.
/// Sem rebaixamento.
///
/// <b>A mesma conta está no servidor</b> (RankRule.cs): mudou aqui, mude lá.
/// </summary>
public static class Ranks
{
	/// <summary>Nível máximo: 3000 (rank 2999).</summary>
	public const int Count = 3000;

	/// <summary>Sem nível: os bots. O bot aparece sem borda, só com "Bot".</summary>
	public const int None = -1;

	/// <summary>Custo de subir do nível L para o L+1 cresce até este teto.</summary>
	private const int CostStart = 100, CostStep = 10, CostCap = 1000;
	private const int Ramp = (CostCap - CostStart) / CostStep;   // 90 níveis até o teto

	public static readonly string[] MetalNames = { "Iniciante", "Bronze", "Prata", "Ouro", "Platina", "Diamante", "Mestre" };

	/// <summary>A cor de cada metal: a da borda, a da placa e a das estrelas.</summary>
	public static readonly Color[] MetalColors =
	{
		new("aeb6c2"), new("c98a5e"), new("cdd6e1"), new("f2c14e"), new("7fd6e6"), new("a98bff"), new("ff6a3d")
	};

	public static int Clamp(int rank) => Mathf.Clamp(rank, 0, Count - 1);

	/// <summary>O nível que se mostra (1 a 3000).</summary>
	public static int LevelOf(int rank) => Clamp(rank) + 1;

	/// <summary>O metal: 0 Iniciante, 1 Bronze ... 6 Mestre.</summary>
	public static int Family(int rank)
	{
		int level = LevelOf(rank);
		return level < 100 ? 0 : Mathf.Min(1 + (level - 100) / 500, MetalNames.Length - 1);
	}

	/// <summary>Estrelas embaixo da borda: uma a cada 100 níveis, de 1 a 5 por metal.</summary>
	public static int Stars(int rank)
	{
		int level = LevelOf(rank);
		return level < 100 ? 0 : Mathf.Min((level - 100) % 500 / 100 + 1, 5);
	}

	/// <summary>O desenho da borda dentro do metal, 0 a 9: muda a cada 10 níveis.</summary>
	public static int Tier(int rank) => LevelOf(rank) % 100 / 10;

	/// <summary>O número da placa: o nível sem as centenas (as estrelas contam elas).</summary>
	public static int Shown(int rank) => LevelOf(rank) < 100 ? LevelOf(rank) : LevelOf(rank) % 100;

	public static string NameOf(int rank) => rank < 0 ? "Bot" : $"Nível {LevelOf(rank)}";
	public static string FamilyName(int rank) => MetalNames[Family(rank)];
	public static Color ColorOf(int rank) => rank < 0 ? new Color("8a93a0") : MetalColors[Family(rank)];

	/// <summary>A cor do metal para **texto**: um pouco mais clara.</summary>
	public static Color InkOf(int rank) => ColorOf(rank).Lightened(0.18f);

	/// <summary>XP acumulado necessário para chegar ao rank (nível rank + 1).</summary>
	public static long XpFor(int rank)
	{
		long r = Clamp(rank);
		if (r <= Ramp) return CostStart * r + CostStep * r * (r - 1) / 2;
		return XpFor(Ramp) + CostCap * (r - Ramp);
	}

	public static int RankFor(long xp)
	{
		int low = 0, high = Count - 1;   // o maior rank com XpFor(rank) <= xp
		while (low < high)
		{
			int mid = (low + high + 1) / 2;
			if (XpFor(mid) <= xp) low = mid; else high = mid - 1;
		}
		return low;
	}

	/// <summary>Quanto do nível atual já foi percorrido, de 0 a 1. No último, sempre 1.</summary>
	public static float Progress(long xp)
	{
		int rank = RankFor(xp);
		if (rank >= Count - 1) return 1f;
		long from = XpFor(rank), to = XpFor(rank + 1);
		return Mathf.Clamp((float)(xp - from) / (to - from), 0f, 1f);
	}

	/// <summary>Presas ao subir para o rank: 20 por nível; borda nova (a cada
	/// 10) vale mais, e estrela nova (a cada 100) mais ainda.</summary>
	public static int PromotionReward(int rank)
	{
		int level = LevelOf(rank);
		if (level % 100 == 0) return 500 + 100 * Family(rank);
		if (level % 10 == 0) return 100 + 25 * Family(rank);
		return 20;
	}

	/// <summary>A subida que merece a tela de promoção: borda ou estrela nova.
	/// Os outros níveis só aparecem na barra do resultado.</summary>
	public static bool IsMilestone(int rank) => LevelOf(rank) % 10 == 0;
}
