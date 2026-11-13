namespace Alcateia.UnitTests;

/// <summary>O sorteio com semente (scripts/domain/GameRandom.cs).</summary>
public static class GameRandomTests
{
	[Test("a mesma semente dá a mesma sequência")]
	public static void SameSeedSameSequence()
	{
		var a = new GameRandom(42);
		var b = new GameRandom(42);
		for (int i = 0; i < 1000; i++)
			Check.Equal(a.NextULong(), b.NextULong(), $"sorteio {i}");
	}

	[Test("sementes vizinhas não andam juntas")]
	public static void NeighbourSeedsDiffer()
	{
		var a = new GameRandom(GameRandom.Derive(1, "Bot1"));
		var b = new GameRandom(GameRandom.Derive(1, "Bot2"));
		int equal = 0;
		for (int i = 0; i < 100; i++)
			if (a.Index(10) == b.Index(10)) equal++;
		Check.Between(0, 25, equal, "coincidências em 100 sorteios de 0 a 9");
	}

	[Test("guardar o estado e voltar refaz a sequência (replay, correção do servidor)")]
	public static void StateRewinds()
	{
		var r = new GameRandom(7);
		r.NextULong();
		ulong saved = r.State;
		float first = r.Unit();
		r.Unit(); r.Unit();
		r.State = saved;
		Check.Near(first, r.Unit(), 0, "o mesmo sorteio depois de voltar");
	}

	[Test("Unit fica em [0, 1) e cobre o intervalo por igual")]
	public static void UnitIsUniform()
	{
		var r = new GameRandom(123);
		int[] buckets = new int[10];
		for (int i = 0; i < 100_000; i++)
		{
			float u = r.Unit();
			Check.True(u >= 0f && u < 1f, $"fora do intervalo: {u}");
			buckets[(int)(u * 10)]++;
		}
		foreach (int count in buckets)
			Check.Between(9_500, 10_500, count, "cada décimo leva ~10%");
	}

	[Test("Chance acerta a proporção pedida")]
	public static void ChanceMatchesProbability()
	{
		var r = new GameRandom(9);
		int hits = 0;
		for (int i = 0; i < 100_000; i++)
			if (r.Chance(0.2f)) hits++;
		Check.Between(19_500, 20_500, hits, "20% de 100 mil");
	}

	[Test("Range e Sign ficam dentro do pedido")]
	public static void RangeAndSign()
	{
		var r = new GameRandom(5);
		int plus = 0;
		for (int i = 0; i < 10_000; i++)
		{
			float x = r.Range(-3f, 2f);
			Check.True(x >= -3f && x < 2f, $"Range fora: {x}");
			int s = r.Sign();
			Check.True(s == 1 || s == -1, $"Sign: {s}");
			if (s > 0) plus++;
			int k = r.Index(4);
			Check.True(k >= 0 && k < 4, $"Index fora: {k}");
		}
		Check.Between(4_800, 5_200, plus, "metade +1");
	}
}
