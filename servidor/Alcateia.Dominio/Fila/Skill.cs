using System;
using System.Collections.Generic;
using System.Linq;

namespace Alcateia.Dominio.Fila;

/// <summary>A habilidade de uma conta: a nota e quantas partidas de jogo
/// rápido a formaram (com poucas, a nota ainda anda depressa).</summary>
public readonly record struct Skill(double Rating, int Matches);

/// <summary>
/// A nota de habilidade do jogo rápido (Jhon, 27/11: "sistema de jogo rápido
/// que escolhe os players baseado nas suas habilidades, níveis, experiências
/// [...] extremamente bem equilibrado").
///
/// É uma nota no estilo Elo, por time: antes da partida, a diferença entre
/// as médias dos dois times diz a chance de cada um ganhar
/// (<see cref="Expected"/>); depois, cada jogador sobe ou desce pelo quanto
/// o resultado contrariou essa chance. Ganhar de um time mais forte vale
/// muito; ganhar de um mais fraco, pouco. Com o tempo a nota de cada um para
/// onde ele ganha metade das partidas — que é o que "equilibrado" quer dizer.
///
/// Três coisas a mais que o Elo puro:
/// - **A largada vem do histórico** (<see cref="Seed"/>): quem chega na fila
///   pela primeira vez não começa do zero; o nível, as vitórias, o K/D e os
///   MVPs de tudo o que já jogou dizem de onde partir. É onde entram "níveis
///   e experiências".
/// - **Quem tem poucas partidas anda mais depressa** (<see cref="K"/>): as
///   dez primeiras são de colocação (<see cref="Placement"/>).
/// - **O desempenho individual conta um pouco** (<see cref="PerformanceShare"/>):
///   quem carregou um time que perdeu cai menos; quem foi carregado sobe
///   menos. Pouco, para ninguém largar o objetivo por abate — e mais na
///   colocação, quando é o que mais diz de quem acabou de chegar.
///
/// Só a partida do jogo rápido mexe na nota. Sala criada à mão não: dá para
/// combinar resultado com amigo ou encher de bot.
/// </summary>
public static class SkillRules
{
	/// <summary>A nota de quem nunca jogou nada, e os limites dela.</summary>
	public const double Start = 1000, Min = 100, Max = 3000;

	/// <summary>Entre que valores a largada pelo histórico pode cair: o
	/// histórico inclui partida contra bot, então não decide sozinho.</summary>
	public const double SeedMin = 700, SeedMax = 1500;

	/// <summary>As primeiras partidas de fila de uma conta são de colocação:
	/// a nota anda depressa e o desempenho individual pesa mais.</summary>
	public const int Placement = 10;

	/// <summary>Quanto do passo da nota vem do desempenho individual (o resto
	/// é o resultado do time): mais na colocação, quando ainda se sabe pouco
	/// de quem chegou; pouco depois, para ninguém largar o objetivo por abate.</summary>
	public const double PerformanceShare = 0.2, PlacementPerformanceShare = 0.4;

	/// <summary>A chance de <paramref name="mine"/> ganhar de
	/// <paramref name="theirs"/> (0 a 1): 400 pontos de diferença são 10 para 1.</summary>
	public static double Expected(double mine, double theirs) => 1.0 / (1.0 + Math.Pow(10, (theirs - mine) / 400.0));

	/// <summary>O tamanho do passo: grande nas partidas de colocação, menor depois.</summary>
	public static double K(int matches) => matches < Placement ? 48 : matches < 30 ? 32 : 24;

	/// <summary>
	/// De onde parte a nota de quem entra na fila pela primeira vez, pelo que
	/// a conta já fez em todas as partidas. Cada medida é puxada para o meio
	/// enquanto a amostra é pequena (três partidas boas não fazem um craque).
	/// </summary>
	/// <param name="level">O nível da conta, a partir de 1.</param>
	public static double Seed(int level, int matches, int wins, int losses, int kills, int deaths, int mvps)
	{
		double kd = (kills + 150.0) / (deaths + 150.0);
		double win = (wins + 6.0) / (wins + losses + 12.0);
		double mvp = matches <= 0 ? 0 : Math.Min(1.0, mvps / (matches + 8.0));
		double experience = Math.Min(120, Math.Max(0, level - 1) * 4.0);

		double rating = Start + (kd - 1.0) * 220 + (win - 0.5) * 500 + mvp * 200 + experience;
		return Math.Clamp(rating, SeedMin, SeedMax);
	}

	/// <summary>Um jogador numa partida de fila que acabou.</summary>
	/// <param name="Points">Os pontos dele na partida (o que o placar mostra).</param>
	/// <param name="Abandoned">Saiu antes do fim: conta como derrota, ganhe o time ou não.</param>
	public sealed record Played(long AccountId, int Team, Skill Before, int Points, bool Abandoned);

	public sealed record Change(long AccountId, Skill Before, Skill After)
	{
		public double Delta => After.Rating - Before.Rating;
	}

	/// <summary>
	/// As notas depois de uma partida. <paramref name="winner"/>: 1 ou 2 (o
	/// time), 0 se empatou. A margem do placar pesa: uma partida apertada
	/// mexe menos que uma surra.
	/// </summary>
	public static IReadOnlyList<Change> Update(IReadOnlyList<Played> players, int winner, int scoreOne, int scoreTwo)
	{
		List<Played> one = players.Where(p => p.Team == 1).ToList(), two = players.Where(p => p.Team == 2).ToList();
		if (one.Count == 0 || two.Count == 0)
			return players.Select(p => new Change(p.AccountId, p.Before, p.Before)).ToList();

		double ratingOne = one.Average(p => p.Before.Rating), ratingTwo = two.Average(p => p.Before.Rating);
		double expectedOne = Expected(ratingOne, ratingTwo);
		double resultOne = winner == 1 ? 1 : winner == 2 ? 0 : 0.5;

		int top = Math.Max(scoreOne, scoreTwo);
		double margin = top <= 0 ? 0 : Math.Abs(scoreOne - scoreTwo) / (double)top;   // 0 apertada, 1 de lavada
		double weight = 0.85 + 0.45 * margin;

		double meanPoints = players.Average(p => (double)Math.Max(0, p.Points));

		var changes = new List<Change>(players.Count);
		foreach (Played p in players)
		{
			double expected = p.Team == 1 ? expectedOne : 1 - expectedOne;
			double result = p.Team == 1 ? resultOne : 1 - resultOne;
			double performance = meanPoints <= 0 ? 0 : Math.Clamp((Math.Max(0, p.Points) - meanPoints) / meanPoints, -1, 1);
			double k = K(p.Before.Matches);
			double share = p.Before.Matches < Placement ? PlacementPerformanceShare : PerformanceShare;

			double delta;
			if (p.Abandoned)
				// Quem larga a partida perde, e pela margem cheia: sair não pode ser o jeito de não cair.
				delta = k * 1.3 * (0 - expected) + k * share * Math.Min(0, performance);
			else
				delta = k * weight * (result - expected) + k * share * performance;

			var after = new Skill(Math.Clamp(p.Before.Rating + delta, Min, Max), p.Before.Matches + 1);
			changes.Add(new Change(p.AccountId, p.Before, after));
		}
		return changes;
	}
}
