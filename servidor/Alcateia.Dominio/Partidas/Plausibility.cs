using System;
using System.Collections.Generic;

namespace Alcateia.Dominio.Partidas;

/// <summary>
/// O que um resultado de partida tem de estranho. O servidor de partida é
/// quem conta, mas enquanto vida e acerto ainda são decididos nos
/// clientes (decisão 0008), um trapaceiro aparece primeiro aqui: abates
/// demais para o tempo jogado, tiro na cabeça em quase todos, mais tiros
/// na cabeça do que abates. Não pune sozinho — vira evento de segurança
/// na ficha do jogador, e quem decide é uma pessoa.
/// </summary>
public static class Plausibility
{
	public const double MaxKillsPerMinute = 8;
	public const int HeadshotSample = 20;
	public const double MaxHeadshotRate = 0.9;
	public const int FlawlessKills = 40;

	public static List<(long AccountId, string What)> Flags(MatchReport report, double minutes)
	{
		var flags = new List<(long, string)>();
		minutes = Math.Max(1, minutes);
		foreach (ReportedLine l in report.Players)
		{
			if (l.Kills > Rewards.MaxKills) flags.Add((l.AccountId, $"{l.Kills} abates numa partida (o teto é {Rewards.MaxKills})"));
			else if (l.Kills / minutes > MaxKillsPerMinute) flags.Add((l.AccountId, $"{l.Kills} abates em {minutes:0.#} min"));

			if (l.Headshots > l.Kills) flags.Add((l.AccountId, $"{l.Headshots} tiros na cabeça com {l.Kills} abates"));
			else if (l.Kills >= HeadshotSample && l.Headshots >= l.Kills * MaxHeadshotRate)
				flags.Add((l.AccountId, $"{l.Headshots} de {l.Kills} abates na cabeça"));

			if (l.Kills >= FlawlessKills && l.Deaths == 0) flags.Add((l.AccountId, $"{l.Kills} abates sem morrer"));
			if (l.Kills < 0 || l.Deaths < 0 || l.Assists < 0 || l.Points < 0) flags.Add((l.AccountId, "número negativo no resultado"));
		}
		return flags;
	}
}
