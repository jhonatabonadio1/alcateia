using System;
using System.Globalization;

namespace Alcateia.Dominio;

/// <summary>
/// O dia do jogo. Vira à meia-noite de Brasília (UTC−3, sem horário de
/// verão desde 2019) para todo mundo: o servidor é um só. É por ele que
/// trocam os desafios diários e a vitrine do dia.
/// </summary>
public static class GameClock
{
	public static readonly TimeSpan Zone = TimeSpan.FromHours(-3);

	/// <summary>O dia do jogo em que cai esse instante (<c>2026-11-20</c>).</summary>
	public static string DayKey(DateTime utc) => (utc + Zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

	/// <summary>Quando esse dia do jogo começou, em UTC.</summary>
	public static DateTime DayStart(DateTime utc) => (utc + Zone).Date - Zone;

	/// <summary>Quando o dia do jogo vira, em UTC.</summary>
	public static DateTime NextDay(DateTime utc) => DayStart(utc).AddDays(1);
}
