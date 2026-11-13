using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public enum MissionMetric
{
	Matches,
	Wins,
	Kills,
	KillsWolf,
	KillsHuman,
	Headshots,
	Assists,
	Minutes,
	Mvps
}

public enum MissionPeriod
{
	Daily,
	Weekly,
	Permanent
}

public sealed class MissionDef
{
	public string Id;
	public string Text;
	public MissionMetric Metric;
	public int Target;
	public int Presas;
	public int Xp;
	public MissionPeriod Period;
	/// <summary>Título exibível no perfil (só missões permanentes).</summary>
	public string Title;
}

public sealed class MissionProgress
{
	public string Id { get; set; }
	public int Progress { get; set; }
	public bool Claimed { get; set; }
}

/// <summary>
/// As missões: três diárias, cinco semanais e os marcos permanentes.
///
/// Diárias e semanais saem de um sorteio pela data (mesmo dia, mesmas
/// missões), e trocam sozinhas na virada. O resgate é manual de
/// propósito: o clique de resgatar é o momento da recompensa, e
/// resgatar sozinho desperdiçaria esse momento.
/// </summary>
public sealed class MissionBook
{
	public const int DailyCount = 3;
	public const int WeeklyCount = 5;

	public string DailyKey { get; set; } = "";
	public List<MissionProgress> Daily { get; set; } = new();
	public string WeeklyKey { get; set; } = "";
	public List<MissionProgress> Weekly { get; set; } = new();
	public List<MissionProgress> Permanent { get; set; } = new();

	// ------------------------------------------------------------------ catálogo

	private static readonly List<MissionDef> Defs = new()
	{
		// diárias
		D("d_partidas", "Jogue 3 partidas", MissionMetric.Matches, 3, 150, 60),
		D("d_vitoria", "Vença 1 partida", MissionMetric.Wins, 1, 150, 80),
		D("d_abates", "Faça 20 abates", MissionMetric.Kills, 20, 200, 80),
		D("d_lobo", "Faça 10 abates como lobo", MissionMetric.KillsWolf, 10, 220, 90),
		D("d_humano", "Faça 15 abates como humano", MissionMetric.KillsHuman, 15, 180, 70),
		D("d_cabeca", "Acerte 5 tiros na cabeça que matam", MissionMetric.Headshots, 5, 180, 70),
		D("d_assist", "Faça 5 assistências", MissionMetric.Assists, 5, 150, 60),

		// semanais
		W("w_partidas", "Jogue 15 partidas", MissionMetric.Matches, 15, 900, 300),
		W("w_vitorias", "Vença 7 partidas", MissionMetric.Wins, 7, 1000, 350),
		W("w_abates", "Faça 120 abates", MissionMetric.Kills, 120, 1100, 400),
		W("w_lobo", "Faça 60 abates como lobo", MissionMetric.KillsWolf, 60, 1200, 420),
		W("w_cabeca", "Acerte 30 tiros na cabeça que matam", MissionMetric.Headshots, 30, 1000, 360),
		W("w_assist", "Faça 25 assistências", MissionMetric.Assists, 25, 900, 300),
		W("w_tempo", "Jogue 90 minutos", MissionMetric.Minutes, 90, 800, 280),
		W("w_mvp", "Seja o MVP de 3 partidas", MissionMetric.Mvps, 3, 1000, 380),

		// permanentes
		P("p_partidas", "Jogue 50 partidas", MissionMetric.Matches, 50, 1500, 500, "Frequentador"),
		P("p_vitorias", "Vença 100 partidas", MissionMetric.Wins, 100, 4000, 1500, "Veterano"),
		P("p_abates", "Faça 1.000 abates", MissionMetric.Kills, 1000, 5000, 2000, "Matador"),
		P("p_lobo", "Faça 1.000 abates como lobo", MissionMetric.KillsWolf, 1000, 6000, 2500, "Fera"),
		P("p_cabeca", "Acerte 500 tiros na cabeça que matam", MissionMetric.Headshots, 500, 5000, 2000, "Olho de Águia"),
		P("p_assist", "Faça 250 assistências", MissionMetric.Assists, 250, 3000, 1200, "Parceiro"),
		P("p_mvp", "Seja o MVP de 25 partidas", MissionMetric.Mvps, 25, 4000, 1600, "Destaque"),
	};

	private static MissionDef D(string id, string text, MissionMetric m, int target, int presas, int xp) =>
		new() { Id = id, Text = text, Metric = m, Target = target, Presas = presas, Xp = xp, Period = MissionPeriod.Daily };

	private static MissionDef W(string id, string text, MissionMetric m, int target, int presas, int xp) =>
		new() { Id = id, Text = text, Metric = m, Target = target, Presas = presas, Xp = xp, Period = MissionPeriod.Weekly };

	private static MissionDef P(string id, string text, MissionMetric m, int target, int presas, int xp, string title) =>
		new() { Id = id, Text = text, Metric = m, Target = target, Presas = presas, Xp = xp, Period = MissionPeriod.Permanent, Title = title };

	public static MissionDef Def(string id) => Defs.FirstOrDefault(d => d.Id == id);

	// ------------------------------------------------------------------ períodos

	public static string DayKey(DateTime local) => local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

	public static string WeekKey(DateTime local) =>
		$"{ISOWeek.GetYear(local)}-S{ISOWeek.GetWeekOfYear(local):00}";

	public static TimeSpan UntilDailyReset(DateTime local) => local.Date.AddDays(1) - local;

	public static TimeSpan UntilWeeklyReset(DateTime local)
	{
		int daysToMonday = ((int)DayOfWeek.Monday - (int)local.DayOfWeek + 7) % 7;
		if (daysToMonday == 0) daysToMonday = 7;
		return local.Date.AddDays(daysToMonday) - local;
	}

	/// <summary>Troca diárias e semanais se virou o dia ou a semana, e
	/// garante que todo marco permanente tenha uma linha.</summary>
	public void Refresh(DateTime local)
	{
		string day = DayKey(local);
		if (DailyKey != day || Daily.Count != DailyCount)
		{
			DailyKey = day;
			Daily = Draw(MissionPeriod.Daily, DailyCount, local.Year * 1000 + local.DayOfYear);
		}

		string week = WeekKey(local);
		if (WeeklyKey != week || Weekly.Count != WeeklyCount)
		{
			WeeklyKey = week;
			Weekly = Draw(MissionPeriod.Weekly, WeeklyCount, ISOWeek.GetYear(local) * 100 + ISOWeek.GetWeekOfYear(local) + 7919);
		}

		foreach (MissionDef def in Defs.Where(d => d.Period == MissionPeriod.Permanent))
			if (Permanent.All(p => p.Id != def.Id))
				Permanent.Add(new MissionProgress { Id = def.Id });

		Permanent.RemoveAll(p => Def(p.Id) == null);
	}

	private static List<MissionProgress> Draw(MissionPeriod period, int count, int seed)
	{
		// A semente sai da própria data, não de GetHashCode de texto: no
		// .NET o hash de string muda a cada execução.
		var rng = new Random(seed);
		return Defs.Where(d => d.Period == period)
			.OrderBy(_ => rng.Next())
			.Take(count)
			.Select(d => new MissionProgress { Id = d.Id })
			.ToList();
	}

	public IEnumerable<MissionProgress> All() => Daily.Concat(Weekly).Concat(Permanent);

	public int ClaimableCount => All().Count(p => !p.Claimed && Def(p.Id) is MissionDef d && p.Progress >= d.Target);

	// ------------------------------------------------------------------ progresso

	/// <summary>Soma o resultado de uma partida. Devolve as missões que
	/// ficaram completas agora (para a tela de resultado avisar).</summary>
	public List<MissionDef> Record(MatchResult result)
	{
		var done = new List<MissionDef>();

		foreach (MissionProgress p in All())
		{
			MissionDef def = Def(p.Id);
			if (def == null || p.Claimed || p.Progress >= def.Target) continue;

			p.Progress = Math.Min(def.Target, p.Progress + Amount(def.Metric, result));
			if (p.Progress >= def.Target) done.Add(def);
		}

		return done;
	}

	private static int Amount(MissionMetric metric, MatchResult r) => metric switch
	{
		MissionMetric.Matches => 1,
		MissionMetric.Wins => r.Won ? 1 : 0,
		MissionMetric.Kills => r.Kills,
		MissionMetric.KillsWolf => r.KillsWolf,
		MissionMetric.KillsHuman => r.KillsHuman,
		MissionMetric.Headshots => r.Headshots,
		MissionMetric.Assists => r.Assists,
		MissionMetric.Minutes => (int)(r.Seconds / 60.0),
		MissionMetric.Mvps => r.Mvp ? 1 : 0,
		_ => 0
	};

	/// <summary>Resgata: credita Presas e XP e, no marco permanente,
	/// destrava o título. Devolve as promoções que o XP causou.</summary>
	public bool Claim(string id, out List<int> promotions)
	{
		promotions = new List<int>();
		MissionProgress p = All().FirstOrDefault(m => m.Id == id);
		MissionDef def = Def(id);
		if (p == null || def == null || p.Claimed || p.Progress < def.Target) return false;

		p.Claimed = true;
		Profile.Earn(Currency.Presas, def.Presas, save: false);
		promotions = Profile.AddXp(def.Xp);

		if (!string.IsNullOrEmpty(def.Title) && !Profile.Titles.Contains(def.Title))
			Profile.Titles.Add(def.Title);

		Profile.Save();
		return true;
	}
}
