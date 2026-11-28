using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Alcateia.Dominio.Fila;

namespace Alcateia.Fila.Simulacao;

/// <summary>
/// A régua do jogo rápido. Cada jogador simulado tem uma habilidade de
/// verdade (que decide quem ganha) e uma nota (o que o formador vê). Mede:
///
/// - **uma noite de fila** em vários movimentos (de 1 chegando por segundo a
///   1 a cada 70 s): partidas por formato, espera média e do pior décimo,
///   diferença média entre as notas dos times e a chance real do time mais
///   forte ganhar (50% é o equilíbrio perfeito);
/// - **a divisão**: em centenas de partidas com grupos, se alguma divisão
///   feita ao acaso sai melhor que a escolhida;
/// - **as notas**: começando da largada pelo histórico (com erro), quantas
///   partidas até a nota acompanhar a habilidade;
/// - **o tempo**: quanto o formador leva com milhares na fila (é dentro da
///   trava do saguão).
/// </summary>
public static class Program
{
	private sealed class Person
	{
		public long Id;
		public double True;
		public Skill Skill;
	}

	private static double Normal(Random r, double mean, double dev)
	{
		double u = 1 - r.NextDouble(), v = 1 - r.NextDouble();
		return mean + dev * Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v);
	}

	public static void Main()
	{
		Split();
		Console.WriteLine();
		Console.WriteLine("Uma noite de fila (habilidade real ~ normal 1000 ± 220):");
		Night("cheia (1 chegando por segundo)", 1, 900, 0, 1, true);
		Night("cheia, 30% em grupo", 1, 900, 0.3, 2, true);
		Night("normal (1 a cada 6 s)", 6, 1800, 0.15, 3, true);
		Night("vazia (1 a cada 25 s)", 25, 3600, 0.1, 4, true);
		Night("quase ninguém (1 a cada 70 s)", 70, 7200, 0, 5, true);
		Night("contas novas (só a largada)", 6, 1800, 0.15, 6, false);
		Console.WriteLine();
		Ratings();
		Console.WriteLine();
		Timing();
		Console.WriteLine();
		Console.WriteLine("Largada pelo histórico: novato " + SkillRules.Seed(1, 0, 0, 0, 0, 0, 0).ToString("0")
			+ " | veterano bom (nível 40, 300 partidas, 62% de vitória, K/D 1,9, 25% MVP) " + SkillRules.Seed(40, 300, 186, 114, 5700, 3000, 75).ToString("0")
			+ " | veterano fraco (nível 25, 200 partidas, 38%, K/D 0,6) " + SkillRules.Seed(25, 200, 76, 124, 1200, 2000, 4).ToString("0")
			+ " | 3 partidas ótimas " + SkillRules.Seed(2, 3, 3, 0, 45, 9, 2).ToString("0"));
	}

	// ------------------------------------------------------------------ a divisão

	private static void Split()
	{
		var r = new Random(7);
		int better = 0, cases = 0;
		for (int c = 0; c < 400; c++)
		{
			int size = r.Next(2, 7);
			var tickets = new List<QueueTicket>();
			int left = size * 2;
			long id = 1;
			while (left > 0)
			{
				int party = Math.Min(left, r.NextDouble() < 0.3 ? r.Next(2, Math.Min(size, 4) + 1) : 1);
				var members = Enumerable.Range(0, party).Select(_ => new QueueMember(id++, "x", new Skill(Normal(r, 1000, 250), 20))).ToList();
				tickets.Add(new QueueTicket(members[0].AccountId, members, DateTime.UtcNow));
				left -= party;
			}
			QuickMatch best = Matchmaker.Split(tickets, size);
			if (best == null) continue;   // os grupos não fecham dois times iguais
			cases++;
			for (int k = 0; k < 600; k++)
			{
				var shuffled = tickets.OrderBy(_ => r.Next()).ToList();
				var one = new List<QueueTicket>();
				int n = 0;
				foreach (QueueTicket t in shuffled)
					if (n + t.Size <= size)
					{
						one.Add(t);
						n += t.Size;
					}
				if (n != size) continue;
				double a = one.SelectMany(t => t.Ratings).Sum() / size, b = shuffled.Except(one).SelectMany(t => t.Ratings).Sum() / size;
				// A escolhida pode perder por pouco para uma ao acaso: o desempate (os melhores de cada lado) custa até uns pontos.
				if (Math.Abs(a - b) < best.Gap - 12) better++;
			}
		}
		Console.WriteLine($"Divisão: {cases} partidas com grupos; {better} vez(es) uma divisão ao acaso saiu melhor que a escolhida.");
	}

	// ------------------------------------------------------------------ uma noite

	private static void Night(string name, double every, int seconds, double partyChance, int seed, bool knownRatings)
	{
		var r = new Random(seed);
		var start = new DateTime(2026, 11, 27, 20, 0, 0, DateTimeKind.Utc);
		var queue = new Dictionary<long, QueueTicket>();
		var people = new Dictionary<long, Person>();
		long next = 1;
		double arrive = 0;
		var waits = new List<double>();
		var gaps = new List<double>();
		var edge = new List<double>();
		var sizes = new SortedDictionary<int, int>();

		for (int t = 0; t <= seconds + 200; t++)
		{
			DateTime at = start.AddSeconds(t);
			while (arrive <= t && t <= seconds)
			{
				int party = r.NextDouble() < partyChance ? r.Next(2, 5) : 1;
				var members = new List<QueueMember>();
				for (int i = 0; i < party; i++)
				{
					double truth = Math.Clamp(Normal(r, 1000, 220), 300, 2200);
					var p = new Person
					{
						Id = next++, True = truth,
						// Conta nova: a nota é a largada pelo histórico, que acerta só em parte.
						Skill = new Skill(knownRatings ? truth : Math.Clamp(truth + Normal(r, 0, 150), SkillRules.SeedMin, SkillRules.SeedMax), knownRatings ? 50 : 0)
					};
					people[p.Id] = p;
					members.Add(new QueueMember(p.Id, "P" + p.Id, p.Skill));
				}
				queue[members[0].AccountId] = new QueueTicket(members[0].AccountId, members, at);
				arrive += every * (0.4 + 1.2 * r.NextDouble());
			}

			foreach (QuickMatch m in Matchmaker.FormAll(queue.Values, at))
			{
				foreach (QueueTicket k in m.Tickets)
				{
					waits.Add(k.Wait(at));
					queue.Remove(k.Id);
				}
				double one = m.One.SelectMany(k => k.Members).Average(x => people[x.AccountId].True);
				double two = m.Two.SelectMany(k => k.Members).Average(x => people[x.AccountId].True);
				gaps.Add(m.Gap);
				edge.Add(Math.Abs(SkillRules.Expected(one, two) - 0.5));
				sizes[m.TeamSize] = sizes.GetValueOrDefault(m.TeamSize) + 1;
				if (m.One.Sum(k => k.Size) != m.TeamSize || m.Two.Sum(k => k.Size) != m.TeamSize) throw new InvalidOperationException("times de tamanho errado");
			}
		}

		waits.Sort();
		string formats = string.Join(" ", sizes.Select(k => $"{k.Key}v{k.Key}:{k.Value}"));
		double p90 = waits.Count > 0 ? waits[(int)(waits.Count * 0.9)] : 0;
		Console.WriteLine($"  {name,-32} {sizes.Values.Sum(),4} partidas  {formats,-34} espera {waits.DefaultIfEmpty().Average(),5:0.0} s (pior décimo {p90,5:0.0} s)  " +
			$"diferença {gaps.DefaultIfEmpty().Average(),5:0.0}  mais forte ganha {50 + 100 * edge.DefaultIfEmpty().Average(),4:0.0}%  sobrou {queue.Values.Sum(k => k.Size)}");
	}

	// ------------------------------------------------------------------ as notas

	private static void Ratings()
	{
		var r = new Random(11);
		List<Person> people = Enumerable.Range(1, 400).Select(i =>
		{
			double truth = Math.Clamp(Normal(r, 1000, 220), 300, 2200);
			// A largada acerta uns 30% da diferença para a média, com erro.
			return new Person { Id = i, True = truth, Skill = new Skill(Math.Clamp(1000 + (truth - 1000) * 0.3 + Normal(r, 0, 120), SkillRules.SeedMin, SkillRules.SeedMax), 0) };
		}).ToList();
		Dictionary<long, Person> byId = people.ToDictionary(p => p.Id);

		double Correlation()
		{
			double mx = people.Average(p => p.True), my = people.Average(p => p.Skill.Rating);
			double sxy = people.Sum(p => (p.True - mx) * (p.Skill.Rating - my));
			double sx = Math.Sqrt(people.Sum(p => Math.Pow(p.True - mx, 2))), sy = Math.Sqrt(people.Sum(p => Math.Pow(p.Skill.Rating - my, 2)));
			return sxy / (sx * sy);
		}

		Console.WriteLine($"Notas (400 jogadores, a fila inteira a cada rodada): correlação com a habilidade real na largada {Correlation():0.00}");
		DateTime now = DateTime.UtcNow;
		int upsets = 0, matches = 0;
		double edge = 0;
		for (int round = 1; round <= 60; round++)
		{
			var queue = people.OrderBy(_ => r.Next()).Select(p => new QueueTicket(p.Id, new[] { new QueueMember(p.Id, "p", p.Skill) }, now.AddSeconds(-20 - r.Next(0, 30)))).ToList();
			foreach (QuickMatch m in Matchmaker.FormAll(queue, now))
			{
				double one = m.One.SelectMany(k => k.Members).Average(x => byId[x.AccountId].True), two = m.Two.SelectMany(k => k.Members).Average(x => byId[x.AccountId].True);
				bool oneWins = r.NextDouble() < SkillRules.Expected(one, two);
				int loser = r.Next(10, 38);
				int Points(long id) => Math.Max(0, (int)Normal(r, 100 + (byId[id].True - 1000) * 0.15, 30));
				List<SkillRules.Played> played = m.One.SelectMany(k => k.Members).Select(x => new SkillRules.Played(x.AccountId, 1, byId[x.AccountId].Skill, Points(x.AccountId), false))
					.Concat(m.Two.SelectMany(k => k.Members).Select(x => new SkillRules.Played(x.AccountId, 2, byId[x.AccountId].Skill, Points(x.AccountId), false))).ToList();
				foreach (SkillRules.Change c in SkillRules.Update(played, oneWins ? 1 : 2, oneWins ? 40 : loser, oneWins ? loser : 40)) byId[c.AccountId].Skill = c.After;
				if (round > 40)
				{
					matches++;
					edge += Math.Abs(SkillRules.Expected(one, two) - 0.5);
					if (one > two != oneWins) upsets++;
				}
			}
			if (round is 5 or 10 or 20 or 40 or 60) Console.WriteLine($"  depois de {round,2} partidas: correlação {Correlation():0.00}");
		}
		Console.WriteLine($"  nas últimas 20: o time de fato mais forte tinha {50 + 100 * edge / matches:0.0}% de chance e perdeu {100.0 * upsets / matches:0}% das partidas");
	}

	// ------------------------------------------------------------------ o tempo

	private static void Timing()
	{
		var r = new Random(3);
		DateTime now = DateTime.UtcNow;
		foreach (int n in new[] { 100, 1000, 3000 })
		{
			List<QueueTicket> queue = Enumerable.Range(1, n).Select(i => new QueueTicket(i,
				new[] { new QueueMember(i, "p", new Skill(Normal(r, 1000, 220), 30)) }, now.AddSeconds(-r.Next(0, 40)))).ToList();
			var clock = Stopwatch.StartNew();
			IReadOnlyList<QuickMatch> matches = Matchmaker.FormAll(queue, now);
			clock.Stop();
			Console.WriteLine($"Tempo: {n,5} na fila -> {matches.Count,4} partidas em {clock.Elapsed.TotalMilliseconds,6:0.0} ms");
		}
	}
}
