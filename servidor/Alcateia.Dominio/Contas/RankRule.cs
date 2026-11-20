using System;

namespace Alcateia.Dominio.Contas;

/// <summary>
/// A patente a partir do XP. É a mesma conta do jogo
/// (<c>scripts/meta/Ranks.cs</c>, que usa tipos do Godot e por isso não é
/// compartilhado): mudou lá, mude aqui — o teste "patente" confere os
/// degraus conhecidos.
/// </summary>
public static class RankRule
{
	public const int Count = 30;

	public static long XpFor(int rank)
	{
		rank = Math.Clamp(rank, 0, Count - 1);
		if (rank == 0) return 0;
		return (long)(Math.Round(230.0 * MathF.Pow(rank, 2.6f) / 10.0) * 10.0);
	}

	public static int RankFor(long xp)
	{
		int rank = 0;
		while (rank + 1 < Count && xp >= XpFor(rank + 1)) rank++;
		return rank;
	}
}
