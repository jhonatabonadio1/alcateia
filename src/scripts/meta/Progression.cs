using System.Collections.Generic;

/// <summary>O que o jogador local fez numa partida que terminou.</summary>
public sealed class MatchResult
{
	public bool Won;
	public bool Draw;
	public int Kills;
	public int Deaths;
	public int Assists;
	public int Headshots;
	public int KillsWolf;
	public int KillsHuman;
	public int Points;
	public bool Mvp;
	public double Seconds;
	public double SecondsHuman;
	public double SecondsWolf;
	public Dictionary<string, int> KillsByWeapon = new();
	public string Breed;
	/// <summary>Segundos em chamas (o rage): vira XP.</summary>
	public int FireSeconds;
}

public sealed class RewardLine
{
	public string Label;
	public long Xp;
	public long Presas;
}

/// <summary>O antes e o depois de uma partida, para a tela de resultado
/// animar: a barra de patente enchendo, as Presas contando.</summary>
public sealed class RewardSummary
{
	public long XpBefore;
	public long XpAfter;
	public long PresasBefore;
	public long PresasAfter;
	public List<RewardLine> Lines = new();
	public List<int> Promotions = new();
	public List<MissionDef> MissionsCompleted = new();
	/// <summary>A partida rende: foi online e com conta. Senão foi treino.</summary>
	public bool Earns;
}

/// <summary>
/// O que uma partida rende fora dela: XP para a patente e Presas para a
/// loja. Também é aqui que as estatísticas vitalícias e as missões
/// recebem a partida.
///
/// A conta está em <see cref="Rewards"/> (uns 100 de XP numa partida
/// comum) e a curva das patentes em <see cref="Ranks"/>.
///
/// Partida abandonada não chama isto: sair no meio não rende nada.
/// </summary>
public static class Progression
{
	// Os valores moram em Rewards (scripts/domain): o servidor usa os mesmos.
	public const int XpBase = Rewards.XpBase, XpKill = Rewards.XpKill, XpAssist = Rewards.XpAssist,
		XpWin = Rewards.XpWin, XpDraw = Rewards.XpDraw, XpMvp = Rewards.XpMvp;
	public const int PresasBase = Rewards.PresasBase, PresasKill = Rewards.PresasKill, PresasAssist = Rewards.PresasAssist,
		PresasWin = Rewards.PresasWin, PresasDraw = Rewards.PresasDraw, PresasMvp = Rewards.PresasMvp;

	/// <summary>
	/// O fim de uma partida, para a tela de resultado.
	///
	/// <paramref name="online"/>, com conta: o prêmio é do servidor — ele
	/// recebe o resultado do servidor de partida e paga (o mesmo
	/// <see cref="Rewards"/> daqui). O que sai deste método é a previsão,
	/// para a tela animar na hora; o saldo de verdade chega com
	/// <see cref="AccountSync.RefreshAfterMatch"/>.
	///
	/// Offline, ou sem conta: a partida é treino e não rende XP, Presas
	/// nem missão (decisão 0015) — o jogo não tem como provar ao servidor
	/// o que aconteceu numa partida que só ele viu.
	///
	/// As estatísticas vitalícias são locais e contam nos dois casos.
	/// </summary>
	public static RewardSummary Apply(MatchResult r, bool online = false)
	{
		var summary = new RewardSummary
		{
			XpBefore = Profile.Xp,
			PresasBefore = Profile.Presas,
			XpAfter = Profile.Xp,
			PresasAfter = Profile.Presas
		};

		Record(r);

		summary.Earns = online && Profile.HasAccount;
		if (!summary.Earns)
		{
			Profile.Save();
			return summary;
		}

		void Line(string label, long xp, long presas)
		{
			if (xp == 0 && presas == 0) return;
			summary.Lines.Add(new RewardLine { Label = label, Xp = xp, Presas = presas });
		}

		// O servidor conta os abates até um teto por partida.
		int kills = System.Math.Min(r.Kills, Rewards.MaxKills);
		Line("Participação", XpBase, PresasBase);
		Line(Loc.F("Abates ×{0}", kills), XpKill * kills, PresasKill * kills);
		Line(Loc.F("Assistências ×{0}", r.Assists), XpAssist * r.Assists, PresasAssist * r.Assists);
		if (r.Won) Line("Vitória", XpWin, PresasWin);
		else if (r.Draw) Line("Empate", XpDraw, PresasDraw);
		if (r.Mvp) Line("MVP da partida", XpMvp, PresasMvp);
		Line(Loc.F("Em chamas {0}:{1:00}", r.FireSeconds / 60, r.FireSeconds % 60), Rewards.FireXp(r.FireSeconds), 0);

		long xp = 0, presas = 0;
		foreach (RewardLine line in summary.Lines) { xp += line.Xp; presas += line.Presas; }

		// Evento do painel (XP ou Presas em dobro): entra como uma linha.
		long bonusXp = (long)System.Math.Round(xp * ServerData.XpBonus) - xp;
		long bonusPresas = (long)System.Math.Round(presas * ServerData.PresasBonus) - presas;
		if (bonusXp > 0 || bonusPresas > 0) Line("Bônus de evento", bonusXp, bonusPresas);
		xp += bonusXp;
		presas += bonusPresas;

		int before = Profile.Rank, after = Ranks.RankFor(Profile.Xp + xp);
		for (int rank = before + 1; rank <= after; rank++)
		{
			summary.Promotions.Add(rank);
			Line(Loc.F("Promoção: {0}", Ranks.NameOf(rank)), 0, Ranks.PromotionReward(rank));
			presas += Ranks.PromotionReward(rank);
		}

		summary.MissionsCompleted = Profile.Missions.Predict(r);
		summary.XpAfter = Profile.Xp + xp;
		summary.PresasAfter = Profile.Presas + presas;

		Profile.Save();
		return summary;
	}

	/// <summary>Soma a partida nas estatísticas vitalícias (locais).</summary>
	private static void Record(MatchResult r)
	{
		// Estatísticas vitalícias.
		LifetimeStats s = Profile.Stats;
		s.Matches++;
		if (r.Won) s.Wins++;
		else if (r.Draw) s.Draws++;
		else s.Losses++;
		s.Kills += r.Kills;
		s.Deaths += r.Deaths;
		s.Assists += r.Assists;
		s.Headshots += r.Headshots;
		s.KillsHuman += r.KillsHuman;
		s.KillsWolf += r.KillsWolf;
		if (r.Mvp) s.Mvps++;
		s.SecondsPlayed += r.Seconds;
		s.SecondsHuman += r.SecondsHuman;
		s.SecondsWolf += r.SecondsWolf;

		foreach (var (weapon, kills) in r.KillsByWeapon)
			s.KillsByWeapon[weapon] = (s.KillsByWeapon.TryGetValue(weapon, out int n) ? n : 0) + kills;

		if (!string.IsNullOrEmpty(r.Breed) && r.KillsWolf > 0)
			s.KillsByBreed[r.Breed] = (s.KillsByBreed.TryGetValue(r.Breed, out int b) ? b : 0) + r.KillsWolf;
	}
}
