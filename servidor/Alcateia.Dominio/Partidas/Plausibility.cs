using System;
using System.Collections.Generic;

namespace Alcateia.Dominio.Partidas;

/// <summary>
/// O que um resultado de partida tem de estranho: abates demais para o
/// tempo jogado, tiro na cabeça em quase todos, mais tiros na cabeça do
/// que abates — e, com o servidor de partida autoritativo (decisão 0016),
/// muitos acertos recusados por ele (parede no caminho, fora da mira,
/// rápido demais) e tempo em chamas (o rage) que os abates não explicam.
/// Não pune sozinho — vira evento de segurança na ficha
/// do jogador, e quem decide é uma pessoa.
/// </summary>
public static class Plausibility
{
	public const double MaxKillsPerMinute = 8;
	public const int HeadshotSample = 20;
	public const double MaxHeadshotRate = 0.9;
	public const int FlawlessKills = 40;

	/// <summary>Acertos recusados numa partida a partir dos quais vale olhar:
	/// um ou outro é ping alto; dezenas é trapaça ou defeito.</summary>
	public const int RejectedHitsAlert = 15;

	/// <summary>Acertos de arma de fogo a partir dos quais a proporção com
	/// tranco de mira diz alguma coisa, e a proporção (por cento) que vira
	/// alerta. Os mesmos números do <c>AimAudit</c> do jogo.</summary>
	public const int AimSample = 25;
	public const int AimSnapShare = 40;

	/// <summary>Formas recusadas numa partida a partir das quais vale olhar.</summary>
	public const int RefusedFormsAlert = 3;

	/// <summary>Folga, em segundos, antes de estranhar o tempo em chamas
	/// (o rage): arredondamento e o último quadro da partida.</summary>
	public const int FireSlack = 3;

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
			// O rage: tempo em chamas que os abates não explicam, ou maior que a
			// partida. O prêmio já é cortado (Rewards); aqui vira evento.
			int fireMax = Rage.MaxFireSeconds(l.Kills, l.Assists);
			if (l.FireSeconds < 0 || l.FireSeconds > fireMax + FireSlack || l.FireSeconds > minutes * 60 + FireSlack)
				flags.Add((l.AccountId, $"{l.FireSeconds} s em chamas com {l.Kills} abates e {l.Assists} assistências (o máximo possível é {fireMax} s)"));
			// A mira que não é de gente (o AimAudit do servidor de partida): quase
			// todo acerto logo depois de um giro de 20° em 0,05 s.
			if (l.AimHits >= AimSample && l.AimSnaps * 100 >= l.AimHits * AimSnapShare)
				flags.Add((l.AccountId, $"{l.AimSnaps} de {l.AimHits} acertos de arma de fogo logo depois de um tranco de mira (suspeita de aimbot)"));
			// A forma que as regras não deixam (o FormAudit): uma vez pode ser rede; várias, não.
			if (l.RefusedForms >= RefusedFormsAlert)
				flags.Add((l.AccountId, $"{l.RefusedForms} vezes lobo fora das regras (além do tempo, sem esperar, sem transformar ou em modo só de humanos)"));
			if (l.RejectedHits >= RejectedHitsAlert)
				flags.Add((l.AccountId, $"{l.RejectedHits} acertos recusados pelo servidor de partida (parede, mira, alcance ou ritmo)"));
		}
		return flags;
	}
}
