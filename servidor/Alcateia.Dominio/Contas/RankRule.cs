using System;

namespace Alcateia.Dominio.Contas;

/// <summary>
/// O nível a partir do XP (era a patente; Jhon, 21/11: "ao invés de
/// patentes, será níveis"). O número continua sendo o "rank": rank 0 é o
/// nível 1. É a mesma conta do jogo (<c>scripts/meta/Ranks.cs</c>, que usa
/// tipos do Godot e por isso não é compartilhado): mudou lá, mude aqui —
/// o teste "nível" confere os degraus conhecidos.
/// </summary>
public static class RankRule
{
	/// <summary>Nível máximo: 3000 (rank 2999).</summary>
	public const int Count = 3000;

	private const long CostStart = 100, CostStep = 10, CostCap = 1000;
	private const int Ramp = (int)((CostCap - CostStart) / CostStep);   // 90

	public static int LevelOf(int rank) => Math.Clamp(rank, 0, Count - 1) + 1;

	public static long XpFor(int rank)
	{
		long r = Math.Clamp(rank, 0, Count - 1);
		if (r <= Ramp) return CostStart * r + CostStep * r * (r - 1) / 2;
		return XpFor(Ramp) + CostCap * (r - Ramp);
	}

	/// <summary>O metal (0 Iniciante, 1 Bronze ... 6 Mestre).</summary>
	public static int Family(int rank)
	{
		int level = LevelOf(rank);
		return level < 100 ? 0 : Math.Min(1 + (level - 100) / 500, 6);
	}

	/// <summary>Presas de prêmio ao chegar ao rank (a mesma conta de
	/// <c>Ranks.PromotionReward</c>, no jogo).</summary>
	public static int PromotionReward(int rank)
	{
		int level = LevelOf(rank);
		if (level % 100 == 0) return 500 + 100 * Family(rank);
		if (level % 10 == 0) return 100 + 25 * Family(rank);
		return 20;
	}

	/// <summary>O que se ganha de Presas por subir de <paramref name="xpBefore"/>
	/// a <paramref name="xpAfter"/>: a soma do prêmio de cada nível alcançado.</summary>
	public static long PromotionPresas(long xpBefore, long xpAfter)
	{
		long presas = 0;
		for (int rank = RankFor(xpBefore) + 1; rank <= RankFor(xpAfter); rank++) presas += PromotionReward(rank);
		return presas;
	}

	public static int RankFor(long xp)
	{
		int low = 0, high = Count - 1;
		while (low < high)
		{
			int mid = (low + high + 1) / 2;
			if (XpFor(mid) <= xp) low = mid; else high = mid - 1;
		}
		return low;
	}
}
