using Godot;

public enum BotAction
{
    Patrol,
    Investigate,
    Engage,
    Push,
    Reposition,
    Reload,
    Transform,
    Revert,
    Howl,
    Retreat
}

/// <summary>Fotografia do mundo no instante da decisão. O cérebro lê
/// só isto — nunca a cena — o que o torna testável e barato.</summary>
public struct BotContext
{
    public bool HasTarget;
    public bool CanSee;
    public float Distance;
    public float HealthRatio;
    public float ShieldRatio;
    public float AmmoRatio;
    public bool IsWolf;
    public bool CanTransform;
    public bool MagEmpty;
    public float ThreatAge;      // segundos desde o último dano levado
    public float BeliefAge;      // segundos desde a última informação
    public bool HasBelief;
    public float Aggression;
    public float PreferredRange;
}

/// <summary>
/// Cérebro de utilidade.
///
/// Em vez de transições fixas ("se vejo, combato"), cada ação dá uma
/// nota de 0 a 1 e a maior vence. A diferença prática aparece nos
/// casos em que duas coisas são verdade ao mesmo tempo: uma máquina de
/// estados precisa de uma regra para decidir se recarrega ou atira com
/// pouca munição e o inimigo longe, enquanto aqui as duas notas se
/// comparam sozinhas.
///
/// Três regras de construção:
///
/// **Multiplicar, não somar.** Uma consideração ruim derruba a ação
/// inteira. Recarregar no meio de um tiroteio deve valer zero, não a
/// média entre "preciso" e "não dá".
///
/// **Compensar o produto.** Com muitas considerações o produto afunda,
/// e uma ação bem avaliada perderia para outra mal avaliada e simples.
///
/// **Inércia.** A ação atual ganha um bônus. Sem isso o bot oscila
/// entre duas opções quase empatadas e parece indeciso — que é o
/// defeito mais visível de utilidade mal feita.
/// </summary>
public static class BotBrain
{
    /// <summary>Bônus da ação em curso, para evitar oscilação.</summary>
    public const float Inertia = 0.12f;

    public static BotAction Choose(in BotContext c, BotAction current)
    {
        BotAction best = BotAction.Patrol;
        float bestScore = -1f;

        foreach (BotAction action in System.Enum.GetValues<BotAction>())
        {
            float score = Score(action, c);

            if (action == current && score > 0f)
                score += Inertia;

            if (score <= bestScore) continue;

            bestScore = score;
            best = action;
        }

        return best;
    }

    public static float Score(BotAction action, in BotContext c) => action switch
    {
        BotAction.Patrol => Patrol(c),
        BotAction.Investigate => Investigate(c),
        BotAction.Engage => Engage(c),
        BotAction.Push => Push(c),
        BotAction.Reposition => Reposition(c),
        BotAction.Reload => Reload(c),
        BotAction.Transform => Transform(c),
        BotAction.Revert => Revert(c),
        BotAction.Howl => Howl(c),
        BotAction.Retreat => Retreat(c),
        _ => 0f
    };

    // ------------------------------------------------------------------
    // As ações. Cada uma multiplica suas considerações e compensa.

    /// <summary>Andar sem rumo é o piso: vale pouco, mas nunca zero.
    /// Sem um piso, um bot sem informação nenhuma ficaria parado.</summary>
    private static float Patrol(in BotContext c)
    {
        if (c.HasBelief) return 0.05f;

        return 0.2f;
    }

    private static float Investigate(in BotContext c)
    {
        if (!c.HasBelief || c.CanSee) return 0f;

        // Informação fresca vale mais. Depois de alguns segundos o
        // palpite envelhece e patrulhar volta a competir.
        float freshness = 1f - Utility.Normalize(c.BeliefAge, 0f, 6f);

        return Utility.Compensate(0.75f * freshness, 2);
    }

    private static float Engage(in BotContext c)
    {
        if (!c.CanSee || c.MagEmpty) return 0f;
        if (c.IsWolf) return 0f;

        // Boa na distância preferida, cai perto demais e longe demais.
        float range = Utility.Band(c.Distance, c.PreferredRange, c.PreferredRange);
        float ammo = Utility.Inverse(c.AmmoRatio);

        return Utility.Compensate(0.8f * range * ammo, 3);
    }

    /// <summary>Fechar a distância. Em deathmatch isto compete de
    /// verdade com atirar parado — quem administra distância o tempo
    /// todo transforma a partida numa perseguição.</summary>
    private static float Push(in BotContext c)
    {
        if (!c.CanSee) return 0f;

        float far = Utility.Normalize(c.Distance, c.PreferredRange, c.PreferredRange * 3f);
        float wolf = c.IsWolf ? 1f : c.Aggression;

        return Utility.Compensate(0.7f * far * wolf, 3);
    }

    private static float Reposition(in BotContext c)
    {
        if (!c.CanSee) return 0f;

        // Perto demais com arma de fogo: abrir espaço vira prioridade.
        float tooClose = 1f - Utility.Normalize(c.Distance, 0f, c.PreferredRange * 0.6f);
        float notWolf = c.IsWolf ? 0f : 1f;

        return Utility.Compensate(0.65f * tooClose * notWolf, 3);
    }

    /// <summary>
    /// Recarregar só vale quando dá. Levar tiro agora zera a nota — e
    /// é por isso que a consideração multiplica em vez de somar.
    /// </summary>
    private static float Reload(in BotContext c)
    {
        if (c.IsWolf) return 0f;

        float need = 1f - c.AmmoRatio;
        if (need <= 0.05f) return 0f;

        // Carregador vazio é urgente mesmo sob fogo.
        if (c.MagEmpty) return 0.95f;

        float safe = Utility.Normalize(c.ThreatAge, 1.5f, 4f);
        float hidden = c.CanSee ? 0.25f : 1f;

        return Utility.Compensate(0.7f * Utility.Quadratic(need) * safe * hidden, 4);
    }

    /// <summary>
    /// Virar lobo é aposta de desespero: vida baixa e alvo ao alcance.
    /// Transformar cedo gruda o bot no jogador e tira dele a arma.
    /// </summary>
    private static float Transform(in BotContext c)
    {
        if (c.IsWolf || !c.CanTransform || !c.HasTarget) return 0f;

        float hurt = 1f - Utility.Normalize(c.HealthRatio, 0.15f, 0.55f);
        float reachable = 1f - Utility.Normalize(c.Distance, 6f, 18f);

        return Utility.Compensate(0.85f * hurt * reachable, 3);
    }

    private static float Revert(in BotContext c)
    {
        if (!c.IsWolf) return 0f;

        // Alvo longe ou sumido: a garra não alcança, a arma sim.
        float far = Utility.Normalize(c.Distance, 14f, 26f);
        float lost = c.CanSee ? 0f : 0.6f;

        return Utility.Compensate(0.7f * Mathf.Max(far, lost), 2);
    }

    /// <summary>Rugir é cura que custa ficar parado e barulhento.
    /// Só vale escondido e com o escudo baixo.</summary>
    private static float Howl(in BotContext c)
    {
        if (!c.IsWolf || c.CanSee) return 0f;

        float empty = 1f - c.ShieldRatio;
        float safe = Utility.Normalize(c.ThreatAge, 1f, 3f);

        return Utility.Compensate(0.8f * Utility.Quadratic(empty) * safe, 3);
    }

    /// <summary>Quebrar a linha de visão quando não há mais o que
    /// fazer: vida baixa, sem lobo disponível.</summary>
    private static float Retreat(in BotContext c)
    {
        if (!c.CanSee || c.IsWolf || c.CanTransform) return 0f;

        float desperate = 1f - Utility.Normalize(c.HealthRatio, 0.1f, 0.35f);

        return Utility.Compensate(0.75f * Utility.Quadratic(desperate), 2);
    }
}
