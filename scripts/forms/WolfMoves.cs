using System.Collections.Generic;

/// <summary>Os golpes do lobo e o uivo.</summary>
public enum WolfMove
{
    /// <summary>Clique esquerdo, primeiro braço ("swiping").</summary>
    Claw,

    /// <summary>Clique esquerdo, o outro braço ("punch").</summary>
    Punch,

    /// <summary>Botão direito no chão: o salto inteiro do "jump attack",
    /// com o dano na aterrissagem.</summary>
    Pounce,

    /// <summary>Botão direito no ar: o bote, um mergulho na direção da
    /// mira que bate quando encosta em algo.</summary>
    Leap,

    /// <summary>R: o rugido que devolve o escudo.</summary>
    Howl
}

/// <summary>Trecho de um clipe do lobo: de qual arquivo, de onde a onde
/// ele toca e o instante do golpe, em segundos do arquivo original.
///
/// <paramref name="StepFrom"/>, <paramref name="StepTo"/> e
/// <paramref name="Step"/>: o passo que o clipe dá, em segundos do
/// arquivo e metros do modelo. O quadril do clipe é preso no lugar (o
/// corpo é quem anda), então o corpo precisa andar essa distância nesse
/// trecho para o pé não deslizar.</summary>
public readonly record struct WolfClip(string Source, float Start, float End, float Impact,
    bool FallOnly = false, float StepFrom = 0f, float StepTo = 0f, float Step = 0f)
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
/// a posição das mãos, dos pés e do quadril quadro a quadro: o golpe é o
/// pico de velocidade da mão que bate; o passo, quanto o quadril anda
/// para a frente; no salto, o quadro em que os pés voltam ao chão.
///
/// | Clipe | Arquivo | Medido | Trecho usado |
/// |---|---|---|---|
/// | claw | mutant swiping (2,67 s) | mão esquerda a 22,5 m/s em 1,30 s; passo de 0,91 m de 0,97 s até o quadril parar em 1,43 s | 0,75–2,00 s (sem a preparação longa) |
/// | heavy | mutant punch (1,10 s) | mão direita a 12,6 m/s em 0,30 s; o clipe só passa o peso (0,19 m) — o passo de 0,35 m é do jogo | 0–1,00 s |
/// | pounce | mutant jump attack (3,70 s) | agacha até 0,5 s, topo em 0,97 s, pés no chão em 1,53 s, garras no chão em 1,60 s, de pé em 3,0 s; quadril anda 1,71 m | 0–3,00 s: o salto inteiro; o corpo anda do impulso (0,45 s) aos pés no chão (1,55 s) — quanto, quem diz é a mira |
/// | dive | o mesmo jump attack | do topo (1,03 s) às garras no chão (1,60 s) | só a queda |
/// | dive_land | o mesmo jump attack | da pancada (1,60 s) a de pé | a aterrissagem do bote |
/// | howl | mutant roaring (5,40 s) | parado até 0,4 s; a volta ao repouso, depois de 4,9 s, fica com a troca para o andar | 0,30–4,90 s |
///
/// Trocou um clipe? Rode a ferramenta de novo e corrija a linha dele.
/// </summary>
public static class WolfMoves
{
    public static readonly Dictionary<string, WolfClip> Clips = new()
    {
        ["claw"] = new WolfClip("claw", 0.75f, 2.0f, 1.30f, StepFrom: 0.97f, StepTo: 1.43f, Step: 0.91f),
        ["heavy"] = new WolfClip("heavy", 0f, 1.0f, 0.30f, StepFrom: 0.05f, StepTo: 0.40f, Step: 0.35f),
        ["pounce"] = new WolfClip("leap", 0f, 3.0f, 1.60f, StepFrom: 0.45f, StepTo: 1.55f, Step: 1.71f),
        ["dive"] = new WolfClip("leap", 1.03f, 1.60f, 1.60f, FallOnly: true),
        ["dive_land"] = new WolfClip("leap", 1.60f, 2.77f, 1.60f, FallOnly: true),
        ["howl"] = new WolfClip("howl", 0.30f, 4.90f, 4.90f),
    };

    /// <summary>Clipe e velocidade de cada golpe. A velocidade é a única
    /// coisa a mexer para deixar um golpe mais rápido ou mais pesado: a
    /// duração e o instante do dano acompanham sozinhos.
    ///
    /// O bote é a exceção: a queda toca na velocidade que fizer o
    /// impacto da animação cair no quadro em que o lobo encosta
    /// (<see cref="DiveSpeed"/>), e a aterrissagem, na de
    /// <see cref="Land"/>.</summary>
    public static (string Clip, float Speed) Of(WolfMove move) => move switch
    {
        WolfMove.Claw => ("claw", 2.4f),
        WolfMove.Punch => ("heavy", 1.9f),
        WolfMove.Pounce => ("pounce", 1.4f),
        WolfMove.Leap => ("dive", 1f),
        _ => ("howl", 1.6f)
    };

    /// <summary>Escala do Mutant no jogo (<see cref="WolfVisual.ModelScale"/>):
    /// o passo medido no modelo vira distância no mundo por ela.</summary>
    public const float BodyScale = 1.1f;

    /// <summary>O passo do golpe no tempo do jogo: quando começa depois do
    /// clique, quanto dura e quantos metros anda. Zero se o clipe não dá
    /// passo.</summary>
    public static (float Delay, float Duration, float Distance) StepOf(WolfMove move)
    {
        var (clip, speed) = Of(move);
        WolfClip c = Clips[clip];
        if (c.Step <= 0f) return (0f, 0f, 0f);
        return ((c.StepFrom - c.Start) / speed, (c.StepTo - c.StepFrom) / speed, c.Step * BodyScale);
    }

    /// <summary>A aterrissagem do bote, do impacto até de pé.</summary>
    public static (string Clip, float Speed) Land => ("dive_land", 1.5f);

    /// <summary>Quanto a aterrissagem do bote prende o lobo no chão.</summary>
    public static float LandDuration => Clips[Land.Clip].Length / Land.Speed;

    /// <summary>Velocidade da queda do bote para o impacto da animação
    /// chegar em <paramref name="eta"/> segundos — com 10% de folga, para
    /// a queda nunca acabar antes de o lobo encostar (o corpo voltaria à
    /// pose parada no ar). Encostou antes, a aterrissagem entra por cima.
    /// O piso de 0,6 cobre o mergulho mais longo (0,9 s).</summary>
    public static float DiveSpeed(float eta) =>
        System.Math.Clamp(Clips["dive"].Length / (System.Math.Max(eta, 0.05f) * 1.1f), 0.6f, 4f);

    /// <summary>Quanto o golpe dura na tela, em segundos. O próximo só
    /// sai depois disso: golpe não interrompe golpe.</summary>
    public static float Duration(WolfMove move)
    {
        var (clip, speed) = Of(move);
        return Clips[clip].Length / speed;
    }

    /// <summary>Do clique ao instante em que a garra chega, em segundos.
    /// É quando o dano sai. Não vale para o bote, cujo dano sai quando
    /// ele encosta.</summary>
    public static float ImpactDelay(WolfMove move)
    {
        var (clip, speed) = Of(move);
        WolfClip c = Clips[clip];
        return (c.Impact - c.Start) / speed;
    }
}
