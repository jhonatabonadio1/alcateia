using System;
using System.Globalization;
using Alcateia.Dominio.Partidas;

namespace Alcateia.Dominio.Desafios;

public static class Periods
{
	public const string Daily = "daily", Weekly = "weekly", Permanent = "permanent", Event = "event";
	public static readonly string[] All = { Daily, Weekly, Permanent, Event };
}

/// <summary>O que um desafio conta (as métricas do jogo, em <c>MissionMetric</c>).</summary>
public static class Metrics
{
	public const string Matches = "matches", Wins = "wins", Kills = "kills", KillsWolf = "kills_wolf", KillsHuman = "kills_human",
		Headshots = "headshots", Assists = "assists", Minutes = "minutes", Mvps = "mvps";
	public static readonly string[] All = { Matches, Wins, Kills, KillsWolf, KillsHuman, Headshots, Assists, Minutes, Mvps };
}

/// <summary>
/// Um desafio (missão) criado no painel: o que contar, até quanto, e o
/// prêmio — Presas, XP, Sangue e/ou um item por tantos dias. Desafio de
/// evento tem começo e fim; diário e semanal recomeçam a cada período.
/// </summary>
public sealed record Challenge(string Id, string Period, string Text, string Metric, int Target, int Presas, int Xp, int Sangue,
	string ItemId, int ItemDays, string Title, bool Active, DateTime? StartsAt, DateTime? EndsAt, int Sort)
{
	public bool RunningAt(DateTime now) => Active && (StartsAt == null || StartsAt <= now) && (EndsAt == null || now < EndsAt);
}

/// <summary>O progresso de uma conta num desafio, no período atual.</summary>
public sealed record ChallengeState(Challenge Challenge, string PeriodKey, int Progress, bool Claimed)
{
	public bool Done => Progress >= Challenge.Target;
}

public static class ChallengeRules
{
	/// <summary>O dia do jogo vira à meia-noite de Brasília (UTC−3, sem
	/// horário de verão desde 2019): o servidor é um só para todos.</summary>
	public static readonly TimeSpan GameZone = TimeSpan.FromHours(-3);

	public static void Check(Challenge c)
	{
		if (string.IsNullOrWhiteSpace(c.Id) || c.Id.Length > 40) throw Invalid("id_invalido", "Identificador inválido.");
		foreach (char ch in c.Id)
			if (!(char.IsAsciiLetterOrDigit(ch) || ch == '_')) throw Invalid("id_invalido", "No identificador, só letras, números e _.");
		if (Array.IndexOf(Periods.All, c.Period) < 0) throw Invalid("periodo_invalido", "Período inválido.");
		if (Array.IndexOf(Metrics.All, c.Metric) < 0) throw Invalid("metrica_invalida", "Métrica inválida.");
		if (string.IsNullOrWhiteSpace(c.Text) || c.Text.Length > 120) throw Invalid("texto_invalido", "Texto de 1 a 120 caracteres.");
		if (c.Target is < 1 or > 1_000_000) throw Invalid("alvo_invalido", "Alvo inválido.");
		if (c.Presas is < 0 or > 1_000_000 || c.Xp is < 0 or > 100_000 || c.Sangue is < 0 or > 100_000) throw Invalid("premio_invalido", "Prêmio fora do limite.");
		if (c.ItemDays is < 0 or > 3650) throw Invalid("premio_invalido", "Dias do item inválidos.");
		if (c.Period == Periods.Event && (c.StartsAt == null || c.EndsAt == null || c.EndsAt <= c.StartsAt))
			throw Invalid("periodo_invalido", "Desafio de evento precisa de começo e fim.");
	}

	/// <summary>A chave do período em curso: o progresso é por ela.</summary>
	public static string PeriodKey(Challenge c, DateTime utc)
	{
		DateTime local = utc + GameZone;
		return c.Period switch
		{
			Periods.Daily => local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			Periods.Weekly => $"{ISOWeek.GetYear(local)}-S{ISOWeek.GetWeekOfYear(local):00}",
			_ => "sempre"
		};
	}

	/// <summary>Quanto esta partida soma no desafio.</summary>
	public static int Gain(string metric, SettledLine line, int minutes, int killsWolf, int killsHuman) => metric switch
	{
		Metrics.Matches => 1,
		Metrics.Wins => line.Won ? 1 : 0,
		Metrics.Kills => line.Kills,
		Metrics.KillsWolf => killsWolf,
		Metrics.KillsHuman => killsHuman,
		Metrics.Headshots => line.Headshots,
		Metrics.Assists => line.Assists,
		Metrics.Minutes => minutes,
		Metrics.Mvps => line.Mvp ? 1 : 0,
		_ => 0
	};

	private static Failure Invalid(string code, string message) => new(FailureKind.Invalid, code, message);
}
