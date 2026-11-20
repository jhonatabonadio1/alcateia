using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

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
	Permanent,
	/// <summary>Vale entre duas datas (criada no painel do servidor).</summary>
	Event
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
	/// <summary>Sangue de prêmio (o painel pode pôr; as que vêm com o jogo não dão).</summary>
	public int Sangue;
	/// <summary>Item de prêmio, e por quantos dias (0: para sempre).</summary>
	public string ItemId;
	public int ItemDays;
	/// <summary>Quando a missão de evento acaba.</summary>
	public DateTime? EndsUtc;
}

public sealed class MissionProgress
{
	public string Id { get; set; }
	public int Progress { get; set; }
	public bool Claimed { get; set; }
}

/// <summary>
/// As missões da conta: diárias, semanais, permanentes e de evento.
///
/// Quem manda é o servidor (docs/decisoes/0015): ele diz quais valem
/// hoje (as diárias e semanais revezam, as mesmas para todo mundo), soma
/// o progresso com o resultado que o servidor de partida entrega e paga
/// o prêmio uma vez por período. Aqui fica o espelho do que ele disse por
/// último, para as telas — sem conta, não há missão.
///
/// O resgate é manual de propósito: o clique de resgatar é o momento da
/// recompensa, e resgatar sozinho desperdiçaria esse momento.
/// </summary>
public sealed class MissionBook
{
	public List<MissionProgress> Daily { get; private set; } = new();
	public List<MissionProgress> Weekly { get; private set; } = new();
	public List<MissionProgress> Permanent { get; private set; } = new();
	public List<MissionProgress> Event { get; private set; } = new();

	/// <summary>Quando as diárias e as semanais viram, pelo relógio do servidor.</summary>
	public DateTime? DailyResetsUtc { get; private set; }
	public DateTime? WeeklyResetsUtc { get; private set; }

	/// <summary>As definições que o servidor mandou (texto, alvo, prêmio).</summary>
	private readonly Dictionary<string, MissionDef> _server = new();

	// ------------------------------------------------------------------ catálogo

	/// <summary>As missões que vêm com o jogo. São a semente do servidor
	/// (tools/servidor/exportar_dados.py lê esta lista, linha por linha):
	/// depois de semeadas, quem edita, cria e desliga é o painel.</summary>
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

	/// <summary>O XP das missões caiu junto com o das partidas (17/11, um
	/// quarto do de antes): os números da lista são os antigos, e a missão
	/// rende um quarto deles, de 5 em 5. As Presas não mudaram.</summary>
	private static int MissionXp(int xp) => System.Math.Max(5, (int)System.Math.Round(xp / 4.0 / 5.0) * 5);

	private static MissionDef D(string id, string text, MissionMetric m, int target, int presas, int xp) =>
		new() { Id = id, Text = text, Metric = m, Target = target, Presas = presas, Xp = MissionXp(xp), Period = MissionPeriod.Daily };

	private static MissionDef W(string id, string text, MissionMetric m, int target, int presas, int xp) =>
		new() { Id = id, Text = text, Metric = m, Target = target, Presas = presas, Xp = MissionXp(xp), Period = MissionPeriod.Weekly };

	private static MissionDef P(string id, string text, MissionMetric m, int target, int presas, int xp, string title) =>
		new() { Id = id, Text = text, Metric = m, Target = target, Presas = presas, Xp = MissionXp(xp), Period = MissionPeriod.Permanent, Title = title };

	/// <summary>A definição da missão: a que o servidor mandou, ou a que
	/// vem com o jogo.</summary>
	public static MissionDef Def(string id) =>
		Profile.Missions._server.TryGetValue(id ?? "", out MissionDef def) ? def : Defs.FirstOrDefault(d => d.Id == id);

	// ------------------------------------------------------------------ o que o servidor disse

	/// <summary>Lê a resposta de <c>GET /v1/challenges</c>.</summary>
	public void Apply(JsonObject reply)
	{
		_server.Clear();
		Daily = new(); Weekly = new(); Permanent = new(); Event = new();

		foreach (JsonNode c in reply?["challenges"] as JsonArray ?? new JsonArray())
		{
			string id = c["id"]?.GetValue<string>();
			if (string.IsNullOrEmpty(id)) continue;

			MissionPeriod period = c["period"]?.GetValue<string>() switch
			{
				"daily" => MissionPeriod.Daily,
				"weekly" => MissionPeriod.Weekly,
				"event" => MissionPeriod.Event,
				_ => MissionPeriod.Permanent
			};

			_server[id] = new MissionDef
			{
				Id = id, Period = period,
				Text = c["text"]?.GetValue<string>() ?? id,
				Metric = MetricOf(c["metric"]?.GetValue<string>()),
				Target = Math.Max(1, c["target"]?.GetValue<int>() ?? 1),
				Presas = c["presas"]?.GetValue<int>() ?? 0,
				Xp = c["xp"]?.GetValue<int>() ?? 0,
				Sangue = c["sangue"]?.GetValue<int>() ?? 0,
				ItemId = c["itemId"]?.GetValue<string>(),
				ItemDays = c["itemDays"]?.GetValue<int>() ?? 0,
				Title = c["title"]?.GetValue<string>(),
				EndsUtc = Utc(c["endsAt"])
			};

			var progress = new MissionProgress
			{
				Id = id,
				Progress = c["progress"]?.GetValue<int>() ?? 0,
				Claimed = c["claimed"]?.GetValue<bool>() ?? false
			};
			(period switch
			{
				MissionPeriod.Daily => Daily,
				MissionPeriod.Weekly => Weekly,
				MissionPeriod.Event => Event,
				_ => Permanent
			}).Add(progress);
		}

		DailyResetsUtc = Utc(reply?["dailyResetsAt"]);
		WeeklyResetsUtc = Utc(reply?["weeklyResetsAt"]);
	}

	private static DateTime? Utc(JsonNode node) =>
		DateTime.TryParse(node?.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime at) ? at : null;

	private static MissionMetric MetricOf(string metric) => metric switch
	{
		"wins" => MissionMetric.Wins,
		"kills" => MissionMetric.Kills,
		"kills_wolf" => MissionMetric.KillsWolf,
		"kills_human" => MissionMetric.KillsHuman,
		"headshots" => MissionMetric.Headshots,
		"assists" => MissionMetric.Assists,
		"minutes" => MissionMetric.Minutes,
		"mvps" => MissionMetric.Mvps,
		_ => MissionMetric.Matches
	};

	// ------------------------------------------------------------------ períodos

	/// <summary>Quanto falta para as diárias virarem (zero se o servidor ainda não disse).</summary>
	public TimeSpan UntilDailyReset() => Left(DailyResetsUtc);

	public TimeSpan UntilWeeklyReset() => Left(WeeklyResetsUtc);

	private static TimeSpan Left(DateTime? utc) =>
		utc == null || utc <= DateTime.UtcNow ? TimeSpan.Zero : utc.Value - DateTime.UtcNow;

	public IEnumerable<MissionProgress> All() => Daily.Concat(Weekly).Concat(Event).Concat(Permanent);

	public int ClaimableCount => All().Count(p => !p.Claimed && Def(p.Id) is MissionDef d && p.Progress >= d.Target);

	// ------------------------------------------------------------------ progresso

	/// <summary>
	/// As missões que esta partida deve completar — para a tela de
	/// resultado avisar na hora. É uma previsão com a mesma conta do
	/// servidor: o progresso de verdade é o que ele somar, e chega no
	/// próximo <see cref="AccountSync.Refresh"/>.
	/// </summary>
	public List<MissionDef> Predict(MatchResult result)
	{
		var done = new List<MissionDef>();
		foreach (MissionProgress p in All())
		{
			MissionDef def = Def(p.Id);
			if (def == null || p.Claimed || p.Progress >= def.Target) continue;
			if (p.Progress + Amount(def.Metric, result) >= def.Target) done.Add(def);
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
}
