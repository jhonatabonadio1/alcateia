using System;
using System.Collections.Generic;
using System.Linq;

namespace Alcateia.Dominio.Fila;

/// <summary>Alguém na fila, com a nota de quando entrou.</summary>
public sealed record QueueMember(long AccountId, string Name, Skill Skill);

/// <summary>
/// Uma entrada na fila: um jogador sozinho, ou um grupo inteiro — que entra
/// junto e fica no mesmo time. <paramref name="Id"/> é a conta de quem pediu
/// (o líder do grupo).
/// </summary>
public sealed record QueueTicket(long Id, IReadOnlyList<QueueMember> Members, DateTime Since)
{
	public int Size => Members.Count;

	/// <summary>A nota de cada um como o formador a vê: quem entra em grupo
	/// vale um pouco mais que sozinho (<see cref="Matchmaker.PartyBonus"/>),
	/// porque gente combinada joga melhor que a soma das notas.</summary>
	public IEnumerable<double> Ratings => Members.Select(m => m.Skill.Rating + Matchmaker.PartyBonus * (Size - 1));

	/// <summary>A nota da entrada: a média dos seus.</summary>
	public double Rating => Ratings.Average();

	public double Wait(DateTime now) => Math.Max(0, (now - Since).TotalSeconds);
}

/// <summary>Uma partida formada: quem vai em cada time e a nota de cada time.</summary>
public sealed record QuickMatch(IReadOnlyList<QueueTicket> One, IReadOnlyList<QueueTicket> Two, int TeamSize, double RatingOne, double RatingTwo)
{
	/// <summary>A diferença entre as notas dos times: zero é o equilíbrio perfeito.</summary>
	public double Gap => Math.Abs(RatingOne - RatingTwo);

	/// <summary>A chance prevista do primeiro time (0,5 = meio a meio).</summary>
	public double ChanceOne => SkillRules.Expected(RatingOne, RatingTwo);

	public IEnumerable<QueueTicket> Tickets => One.Concat(Two);

	/// <summary>Os mesmos times com os lados trocados (quem sorteia o lado é o saguão).</summary>
	public QuickMatch Swapped() => new(Two, One, TeamSize, RatingTwo, RatingOne);
}

/// <summary>
/// Forma as partidas do jogo rápido: escolhe na fila quem joga com quem e
/// divide em dois times do mesmo tamanho e da mesma força.
///
/// **Quem entra.** A partida se forma em volta de quem espera há mais tempo
/// (ninguém fica para trás), com os de nota mais próxima da dele. No começo
/// só serve quem está bem perto; a cada segundo de espera a faixa abre
/// (<see cref="Spread"/>), até aceitar qualquer um.
///
/// **Quantos.** O formato recomendado é <see cref="Recommended"/> contra
/// <see cref="Recommended"/> — é o que se forma assim que há gente para
/// isso. Com a fila cheia, os times crescem (5 com 10 esperando, 6 com 12);
/// com a fila vazia, a espera vai liberando formatos menores
/// (<see cref="MinTeam"/>), até o um contra um. Grupo grande pede time do
/// tamanho dele. O mapa é sorteado entre os que servem para o tamanho
/// (<see cref="PickMap"/>).
///
/// **Como divide.** Entre todas as divisões possíveis em dois times iguais
/// (grupo fica junto), fica a de menor diferença entre as notas médias
/// (<see cref="Split"/>); no empate, a que separa melhor os dois mais fortes.
/// Se nem a melhor divisão fica dentro do aceitável para o tempo de espera
/// (<see cref="Tolerance"/>), a partida não se forma ainda.
///
/// C# puro: quem chama é o saguão (<c>LobbyService.Queue.cs</c>), que tem a
/// fila, o relógio e o sorteio.
/// </summary>
public static class Matchmaker
{
	/// <summary>Jogadores por time: o formato recomendado e o maior.</summary>
	public const int Recommended = 4, MaxTeam = 6;

	/// <summary>Quanto cada colega de grupo soma à nota de quem entra junto.</summary>
	public const double PartyBonus = 12;

	/// <summary>Depois disto na fila, vale qualquer nota e qualquer divisão:
	/// jogar uma partida desigual é melhor que não jogar.</summary>
	public const double GiveUp = 100;

	/// <summary>Quantas entradas (as de nota mais próxima) o formador olha
	/// para montar uma partida. Com a fila enorme, as primeiras bastam.</summary>
	private const int Considered = 28;

	/// <summary>Quanto a nota de alguém pode diferir da de quem espera há
	/// <paramref name="wait"/> segundos para entrarem na mesma partida.</summary>
	public static double Spread(double wait) => wait >= GiveUp ? double.MaxValue : Math.Min(600, 80 + 8 * wait);

	/// <summary>A maior diferença aceitável entre as notas médias dos times
	/// (25 pontos são 53,6% de chance para o mais forte; 100, 64%).</summary>
	public static double Tolerance(double wait) => wait >= GiveUp ? double.MaxValue : Math.Min(100, 25 + wait);

	/// <summary>O menor time aceitável para quem espera há <paramref name="wait"/> segundos.</summary>
	public static int MinTeam(double wait) => wait < 25 ? Recommended : wait < 50 ? 3 : wait < 75 ? 2 : 1;

	/// <summary>Os pontos para vencer, pelo tamanho do time: a partida dura
	/// parecido com 2 ou com 12 em campo.</summary>
	public static int ScoreFor(int teamSize) => teamSize <= 1 ? 15 : Math.Clamp(teamSize * 10, 20, 60);

	/// <summary>Os modos que o jogo rápido sorteia: os quatro contra outros
	/// jogadores (o número é o <c>play</c> das regras da sala: Livre, Só
	/// humanos, Só lobos, Humanos × Lobos). Os de bot ficam de fora.</summary>
	public static readonly IReadOnlyList<int> Modes = new[] { 0, 1, 2, 3 };

	// ------------------------------------------------------------------ formar

	/// <summary>
	/// Forma todas as partidas que dá para formar com quem está na fila
	/// agora, numa passada só. Quem não entrou em nenhuma fica para a próxima
	/// (o saguão chama de novo a cada segundo, e a faixa de cada um abre com a
	/// espera).
	///
	/// A fila é ordenada pela nota uma vez; para cada um, os candidatos são
	/// os vizinhos dele nessa ordem — com milhares procurando, cada partida
	/// custa o mesmo que com dez.
	/// </summary>
	public static IReadOnlyList<QuickMatch> FormAll(IReadOnlyCollection<QueueTicket> queue, DateTime now)
	{
		var matches = new List<QuickMatch>();
		if (queue.Count < 2) return matches;

		QueueTicket[] byRating = queue.OrderBy(t => t.Rating).ThenBy(t => t.Id).ToArray();
		var position = new Dictionary<long, int>(byRating.Length);
		for (int i = 0; i < byRating.Length; i++) position[byRating[i].Id] = i;
		var used = new bool[byRating.Length];

		foreach (QueueTicket anchor in queue.OrderBy(t => t.Since).ThenBy(t => t.Id))
		{
			int at = position[anchor.Id];
			if (used[at]) continue;
			QuickMatch match = FormAround(anchor, Neighbours(byRating, used, at, Spread(anchor.Wait(now))), now);
			if (match == null) continue;
			matches.Add(match);
			foreach (QueueTicket t in match.Tickets) used[position[t.Id]] = true;
		}
		return matches;
	}

	/// <summary>A primeira partida que dá para formar agora, ou null (o mesmo que <see cref="FormAll"/>, parando na primeira).</summary>
	public static QuickMatch Form(IReadOnlyCollection<QueueTicket> queue, DateTime now) => FormAll(queue, now).FirstOrDefault();

	/// <summary>Os vizinhos de nota de quem está em <paramref name="at"/>
	/// (os mais próximos primeiro, até <see cref="Considered"/>), dentro da
	/// faixa, pulando quem já entrou numa partida nesta passada.</summary>
	private static List<QueueTicket> Neighbours(QueueTicket[] byRating, bool[] used, int at, double spread)
	{
		var near = new List<QueueTicket>(Considered);
		double rating = byRating[at].Rating;
		int left = at - 1, right = at + 1;
		while (near.Count < Considered)
		{
			while (left >= 0 && used[left]) left--;
			while (right < byRating.Length && used[right]) right++;
			if (left < 0 && right >= byRating.Length) break;   // a fila acabou dos dois lados
			double dl = left >= 0 ? rating - byRating[left].Rating : double.PositiveInfinity;
			double dr = right < byRating.Length ? byRating[right].Rating - rating : double.PositiveInfinity;
			if (Math.Min(dl, dr) > spread) break;
			if (dl <= dr) near.Add(byRating[left--]);
			else near.Add(byRating[right++]);
		}
		return near;
	}

	/// <summary>A partida em volta de quem espera, com os candidatos dados:
	/// do maior formato que a fila permite ao menor que a espera dele aceita.</summary>
	private static QuickMatch FormAround(QueueTicket anchor, List<QueueTicket> near, DateTime now)
	{
		double wait = anchor.Wait(now);
		int total = anchor.Size + near.Sum(t => t.Size);
		int largest = Math.Min(MaxTeam, total / 2);
		int wanted = total >= 12 ? 6 : total >= 10 ? 5 : Recommended;
		int from = Math.Min(largest, Math.Max(wanted, anchor.Size));
		int to = Math.Max(MinTeam(wait), anchor.Size);

		for (int size = from; size >= to; size--)
		{
			List<QueueTicket> chosen = Select(anchor, near, size);
			if (chosen == null) continue;
			QuickMatch match = Split(chosen, size);
			if (match != null && match.Gap <= Tolerance(wait)) return match;
		}
		return null;
	}

	/// <summary>
	/// Escolhe as entradas de uma partida de <paramref name="size"/> contra
	/// <paramref name="size"/>: a de quem espera e, das outras, as de nota
	/// mais próxima que fecham a conta exata e dão para dividir em dois times
	/// iguais com os grupos inteiros. Null se não fecha.
	/// </summary>
	private static List<QueueTicket> Select(QueueTicket anchor, List<QueueTicket> near, int size)
	{
		if (anchor.Size > size) return null;
		var chosen = new List<QueueTicket> { anchor };
		int budget = 4000;   // grupos de tamanhos ruins não fazem a busca rodar para sempre
		return Fill(near, 0, size * 2 - anchor.Size, size, chosen, ref budget) ? chosen : null;
	}

	private static bool Fill(List<QueueTicket> near, int at, int left, int size, List<QueueTicket> chosen, ref int budget)
	{
		if (left == 0) return Divides(chosen, size);
		if (at >= near.Count || --budget <= 0) return false;

		QueueTicket next = near[at];
		if (next.Size <= left && next.Size <= size)
		{
			chosen.Add(next);
			if (Fill(near, at + 1, left - next.Size, size, chosen, ref budget)) return true;
			chosen.RemoveAt(chosen.Count - 1);
		}
		return Fill(near, at + 1, left, size, chosen, ref budget);
	}

	/// <summary>Dá para pôr estas entradas em dois times de <paramref name="size"/>, sem partir grupo?</summary>
	private static bool Divides(List<QueueTicket> tickets, int size)
	{
		// Soma de subconjunto: que tamanhos de time dá para montar com as entradas.
		long reachable = 1;   // bit n: dá para somar n
		foreach (QueueTicket t in tickets) reachable |= reachable << t.Size;
		return (reachable >> size & 1) != 0;
	}

	// ------------------------------------------------------------------ dividir

	/// <summary>
	/// A divisão mais equilibrada das entradas em dois times de
	/// <paramref name="size"/> jogadores, com cada grupo inteiro de um lado.
	/// Olha todas (são no máximo 12 entradas: 2 048 divisões). Null se não há
	/// divisão em times iguais.
	/// </summary>
	public static QuickMatch Split(IReadOnlyList<QueueTicket> tickets, int size)
	{
		int n = tickets.Count;
		if (n < 2 || n > 20 || tickets.Sum(t => t.Size) != size * 2) return null;

		double[] sum = tickets.Select(t => t.Ratings.Sum()).ToArray(), best = tickets.Select(t => t.Ratings.Max()).ToArray();
		double total = sum.Sum();

		int chosen = -1;
		double least = double.MaxValue;
		// A primeira entrada fica sempre no primeiro time: a divisão espelhada é a mesma.
		for (int mask = 1; mask < 1 << n; mask += 2)
		{
			int count = 0;
			double one = 0, topOne = 0, topTwo = 0;
			for (int i = 0; i < n; i++)
			{
				if ((mask >> i & 1) != 0)
				{
					count += tickets[i].Size;
					one += sum[i];
					topOne = Math.Max(topOne, best[i]);
				}
				else topTwo = Math.Max(topTwo, best[i]);
			}
			if (count != size) continue;

			// A diferença das médias manda; a dos melhores de cada lado desempata
			// (dois craques no mesmo time com a média igual ainda é pior de jogar).
			double cost = Math.Abs(one - (total - one)) / size + 0.02 * Math.Abs(topOne - topTwo);
			if (cost < least) (chosen, least) = (mask, cost);
		}
		if (chosen < 0) return null;

		var teamOne = new List<QueueTicket>();
		var teamTwo = new List<QueueTicket>();
		double ratingOne = 0;
		for (int i = 0; i < n; i++)
		{
			if ((chosen >> i & 1) != 0)
			{
				teamOne.Add(tickets[i]);
				ratingOne += sum[i];
			}
			else teamTwo.Add(tickets[i]);
		}
		return new QuickMatch(teamOne, teamTwo, size, ratingOne / size, (total - ratingOne) / size);
	}

	// ------------------------------------------------------------------ mapa e modo

	/// <summary>
	/// Os tamanhos de time que cabem bem num mapa, pelo porte dele (o que o
	/// catálogo de mapas diz: Pequeno, Médio, Grande): num pequeno, 6 contra 6
	/// vira fila de renascimento; num grande, 1 contra 1 é procurar o outro.
	/// </summary>
	public static (int Min, int Max) TeamsFor(string mapSize) => (mapSize ?? "").Trim().ToLowerInvariant() switch
	{
		"pequeno" => (1, 3),
		"grande" => (4, MaxTeam),
		_ => (2, MaxTeam)   // médio, ou porte que o servidor não conhece
	};

	/// <summary>
	/// Sorteia o mapa entre os que servem para o tamanho do time. Nenhum
	/// serve (o painel desligou os daquele porte): entre todos os ligados.
	/// <paramref name="random"/>: um número de 0 a n-1.
	/// </summary>
	public static string PickMap(IReadOnlyList<(string Id, string Size)> maps, int teamSize, Func<int, int> random)
	{
		if (maps == null || maps.Count == 0) return null;
		List<(string Id, string Size)> fit = maps.Where(m =>
		{
			(int min, int max) = TeamsFor(m.Size);
			return teamSize >= min && teamSize <= max;
		}).ToList();
		if (fit.Count == 0) fit = maps.ToList();
		return fit[random(fit.Count)].Id;
	}

	public static int PickMode(Func<int, int> random) => Modes[random(Modes.Count)];
}
