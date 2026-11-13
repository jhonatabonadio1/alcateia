using System;

/// <summary>
/// O sorteio do jogo, com semente: a mesma semente dá a mesma sequência,
/// em qualquer máquina. É o que o multiplayer precisa (o servidor e a
/// predição do cliente sorteiam a mesma dispersão do tiro) e o que deixa
/// reproduzir um defeito que dependeu da sorte — rode com a mesma semente
/// e ele volta.
///
/// Cada sorteador é um fluxo independente (SplitMix64): o bot "Bot3" e a
/// arma do jogador não dividem sequência, então um tiro a mais de um não
/// muda a sorte do outro. Use <see cref="MatchRandom.Stream"/> para pegar
/// o fluxo de alguém.
///
/// Só para regra de jogo. Enfeite (o giro de uma arma no chão) pode usar
/// o sorteio do Godot.
/// </summary>
public sealed class GameRandom
{
	private ulong _state;

	public GameRandom(ulong seed) => _state = seed;

	/// <summary>Onde a sequência está: guardar e voltar é como um replay
	/// (ou o servidor, na correção) refaz um trecho.</summary>
	public ulong State
	{
		get => _state;
		set => _state = value;
	}

	public ulong NextULong()
	{
		ulong z = _state += 0x9E3779B97F4A7C15UL;
		z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
		z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
		return z ^ (z >> 31);
	}

	/// <summary>De 0 (incluso) a 1 (excluso).</summary>
	public float Unit() => (NextULong() >> 40) * (1f / (1 << 24));

	/// <summary>Verdadeiro com probabilidade <paramref name="probability"/>.</summary>
	public bool Chance(float probability) => Unit() < probability;

	public float Range(float min, float max) => min + (max - min) * Unit();

	public double Range(double min, double max) => min + (max - min) * Unit();

	/// <summary>−1 ou +1, meio a meio.</summary>
	public int Sign() => Unit() < 0.5f ? -1 : 1;

	/// <summary>De 0 a <paramref name="count"/> − 1.</summary>
	public int Index(int count) => count <= 0 ? 0 : (int)(NextULong() % (ulong)count);

	/// <summary>Mistura uma semente com o nome de um fluxo: sementes
	/// vizinhas e nomes parecidos dão sequências sem relação.</summary>
	public static ulong Derive(ulong seed, string stream)
	{
		ulong hash = 14695981039346656037UL; // FNV-1a
		foreach (char c in stream ?? "")
		{
			hash ^= c;
			hash *= 1099511628211UL;
		}

		var mixer = new GameRandom(seed ^ hash);
		return mixer.NextULong();
	}
}

/// <summary>
/// A semente da partida e os fluxos de cada um. Começa com uma semente
/// do relógio; <see cref="Begin"/> fixa outra (o servidor manda a dele; um
/// teste, ou quem reproduz um defeito, passa <c>--seed=N</c>).
/// </summary>
public static class MatchRandom
{
	private static readonly System.Collections.Generic.Dictionary<string, GameRandom> Shared = new();

	public static ulong Seed { get; private set; } = (ulong)DateTime.UtcNow.Ticks;

	/// <summary>Fixa a semente. Os fluxos compartilhados recomeçam; quem
	/// guardou o seu (<see cref="Stream"/>) pede de novo ao nascer.</summary>
	public static void Begin(ulong seed)
	{
		Seed = seed;
		Shared.Clear();
	}

	/// <summary>Um fluxo de quem não tem dono (os pontos de nascimento, as
	/// rotas de ronda): o mesmo objeto para o mesmo nome, até a próxima
	/// semente.</summary>
	public static GameRandom For(string name)
	{
		if (!Shared.TryGetValue(name, out GameRandom rng))
			Shared[name] = rng = Stream(name);
		return rng;
	}

	/// <summary>O fluxo de alguém nesta partida, pelo nome (o nome do nó
	/// serve: é o mesmo no servidor e no cliente).</summary>
	public static GameRandom Stream(string name) => new(GameRandom.Derive(Seed, name));
}
