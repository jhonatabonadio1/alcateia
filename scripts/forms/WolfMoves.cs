using System.Collections.Generic;

/// <summary>Os golpes do lobo e o uivo.</summary>
public enum WolfMove
{
    /// <summary>Clique esquerdo, primeiro braço ("swiping").</summary>
    Claw,

    /// <summary>Clique esquerdo, o outro braço ("punch").</summary>
    Punch,

    /// <summary>Botão direito no chão: a patada pesada.</summary>
    Heavy,

    /// <summary>Botão direito no ar: o bote.</summary>
    Leap,

    /// <summary>R: o rugido que devolve o escudo.</summary>
    Howl
}

/// <summary>Trecho de um clipe do lobo: de onde a onde ele toca, e o
/// instante do golpe, em segundos do arquivo original.</summary>
public readonly record struct WolfClip(float Start, float End, float Impact, bool FallOnly = false)
{
    public float Length => End - Start;
}

/// <summary>
/// O tempo de cada golpe do lobo, **medido nas animações**. É a fonte
/// única: a arma (<see cref="Weapon"/>) aplica o dano no instante do
/// golpe e trava o próximo até a animação acabar, e o corpo
/// (<see cref="WolfVisual"/>) toca o mesmo trecho na mesma velocidade.
/// Como os dois leem daqui, não tem como sair de sincronia.
///
/// Os números vieram de <c>tools/analisar_golpes.gd</c>, que reconstrói
/// a posição das mãos quadro a quadro: o golpe é o pico de velocidade
/// da mão que bate, perto do ponto em que ela está mais à frente.
///
/// | Clipe | Arquivo | Golpe medido | Trecho usado |
/// |---|---|---|---|
/// | claw | mutant swiping (2,67 s) | mão esquerda a 22,5 m/s em 1,30 s | 0,75–2,00 s (sem a preparação longa) |
/// | heavy | mutant punch (1,10 s) | mão direita a 12,6 m/s em 0,30 s | 0–1,00 s |
/// | leap | mutant jump attack (3,70 s) | as duas mãos ao aterrissar, 1,63 s | 1,03–2,77 s: só a queda |
/// | howl | mutant roaring (5,40 s) | — | inteiro; o escudo volta no fim |
///
/// Trocou um clipe? Rode a ferramenta de novo e corrija a linha dele.
/// </summary>
public static class WolfMoves
{
    public static readonly Dictionary<string, WolfClip> Clips = new()
    {
        ["claw"] = new WolfClip(0.75f, 2.0f, 1.30f),
        ["heavy"] = new WolfClip(0f, 1.0f, 0.30f),
        ["leap"] = new WolfClip(1.03f, 2.77f, 1.63f, FallOnly: true),
        ["howl"] = new WolfClip(0f, 5.4f, 5.4f),
    };

    /// <summary>Clipe e velocidade de cada golpe. A velocidade é a única
    /// coisa a mexer para deixar um golpe mais rápido ou mais pesado: a
    /// duração e o instante do dano acompanham sozinhos.</summary>
    public static (string Clip, float Speed) Of(WolfMove move) => move switch
    {
        WolfMove.Claw => ("claw", 1.8f),
        WolfMove.Punch => ("heavy", 1.4f),
        WolfMove.Heavy => ("heavy", 1.0f),
        WolfMove.Leap => ("leap", 1.2f),
        _ => ("howl", 1.5f)
    };

    /// <summary>Quanto o golpe dura na tela, em segundos. O próximo só
    /// sai depois disso: golpe não interrompe golpe.</summary>
    public static float Duration(WolfMove move)
    {
        var (clip, speed) = Of(move);
        return Clips[clip].Length / speed;
    }

    /// <summary>Do clique ao instante em que a garra chega, em segundos.
    /// É quando o dano sai.</summary>
    public static float ImpactDelay(WolfMove move)
    {
        var (clip, speed) = Of(move);
        WolfClip c = Clips[clip];
        return (c.Impact - c.Start) / speed;
    }
}
