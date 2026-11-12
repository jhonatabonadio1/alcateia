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
}

/// <summary>
/// O que uma partida rende fora dela: XP para a patente e Presas para a
/// loja. Também é aqui que as estatísticas vitalícias e as missões
/// recebem a partida.
///
/// A conta (por partida, no ritmo de uma partida de 10 minutos): uns 150
/// de XP por participar, mais 20 por abate e 10 por assistência, mais
/// vitória e MVP. Dá entre 300 e 600 por partida — o que faz as três
/// primeiras patentes saírem numa sessão (veja <see cref="Ranks"/>).
///
/// Partida abandonada não chama isto: sair no meio não rende nada.
/// </summary>
public static class Progression
{
    public const int XpBase = 150, XpKill = 20, XpAssist = 10, XpWin = 120, XpDraw = 60, XpMvp = 80;
    public const int PresasBase = 100, PresasKill = 8, PresasAssist = 4, PresasWin = 80, PresasDraw = 40, PresasMvp = 40;

    public static RewardSummary Apply(MatchResult r)
    {
        var summary = new RewardSummary
        {
            XpBefore = Profile.Xp,
            PresasBefore = Profile.Presas
        };

        void Line(string label, long xp, long presas)
        {
            if (xp == 0 && presas == 0) return;
            summary.Lines.Add(new RewardLine { Label = label, Xp = xp, Presas = presas });
        }

        Line("Participação", XpBase, PresasBase);
        Line($"Abates ×{r.Kills}", XpKill * r.Kills, PresasKill * r.Kills);
        Line($"Assistências ×{r.Assists}", XpAssist * r.Assists, PresasAssist * r.Assists);
        if (r.Won) Line("Vitória", XpWin, PresasWin);
        else if (r.Draw) Line("Empate", XpDraw, PresasDraw);
        if (r.Mvp) Line("MVP da partida", XpMvp, PresasMvp);

        long xp = 0, presas = 0;
        foreach (RewardLine line in summary.Lines) { xp += line.Xp; presas += line.Presas; }

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

        Profile.Earn(Currency.Presas, presas, save: false);
        summary.Promotions = Profile.AddXp(xp);

        foreach (int rank in summary.Promotions)
            Line($"Promoção: {Ranks.NameOf(rank)}", 0, Ranks.PromotionReward(rank));

        Profile.Missions.Refresh(System.DateTime.Now);
        summary.MissionsCompleted = Profile.Missions.Record(r);

        summary.XpAfter = Profile.Xp;
        summary.PresasAfter = Profile.Presas;

        Profile.Save();
        Profile.NotifyChanged();
        return summary;
    }
}
