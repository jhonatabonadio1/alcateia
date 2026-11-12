using Godot;
using System;

public enum KillCause
{
    Gun,
    Headshot,
    Knife,
    Wolf,
    Grenade,
    Fall
}

public readonly struct Kill
{
    public readonly string Killer;
    public readonly string Victim;
    public readonly int KillerTeam;
    public readonly int VictimTeam;
    public readonly KillCause Cause;

    public Kill(string killer, int killerTeam, string victim, int victimTeam, KillCause cause)
    {
        Killer = killer;
        KillerTeam = killerTeam;
        Victim = victim;
        VictimTeam = victimTeam;
        Cause = cause;
    }
}

/// <summary>
/// Barramento de abates. Quem mata reporta, o HUD escuta.
///
/// Mesmo padrão do NoiseBus, e pela mesma razão: quem atira não
/// precisa conhecer a interface, e a interface não precisa vasculhar
/// o mundo atrás de mortes.
///
/// O feed não é enfeite. É como o jogador entende o que aconteceu
/// fora do seu campo de visão — e, principalmente, como ele descobre
/// de que morreu. Morrer sem saber a causa é a forma mais rápida de
/// um jogo parecer injusto.
/// </summary>
public static class KillFeed
{
    public static event Action<Kill> Reported;

    public static void Report(Kill kill) => Reported?.Invoke(kill);

    public static string Describe(KillCause cause) => cause switch
    {
        KillCause.Headshot => "na cabeça",
        KillCause.Knife => "na faca",
        KillCause.Wolf => "de lobo",
        KillCause.Grenade => "de granada",
        KillCause.Fall => "caiu",
        _ => "abateu"
    };
}

public static class Teams
{
    public const int Blue = 1;
    public const int Red = 2;

    public static Color ColorOf(int team) => team == Blue
        ? new Color(0.35f, 0.65f, 1f)
        : new Color(1f, 0.4f, 0.35f);

    public static string NameOf(int team) => team == Blue ? "AZUL" : "VERMELHO";
}
