using Godot;

public enum BotLevel
{
    Easy,
    Medium,
    Hard
}

/// <summary>
/// Perfis de dificuldade dos bots.
///
/// O que muda entre os níveis não é vida nem dano — é **competência**.
/// Um bot fácil não é um bot fraco que morre fácil; é um bot que
/// percebe tarde, reage devagar, erra muito e não sustenta fogo.
/// Inflar vida do inimigo para "dificultar" produz luta longa e chata,
/// não luta difícil.
///
/// As quatro alavancas, em ordem de impacto real:
///   1. Reação — quanto tempo até se virar e atirar
///   2. Erro de mira — quanto desvia do alvo
///   3. Taxa de erro deliberado — quantos tiros vão longe de propósito
///   4. Sustentação — quanto tempo atira antes de pausar
/// </summary>
public static class Difficulty
{
    public static BotLevel Current { get; private set; } = BotLevel.Medium;

    public static void Set(BotLevel level)
    {
        Current = level;
        ApplyToAll();
    }

    /// <summary>Aplica em todos os bots já na cena.</summary>
    public static void ApplyToAll()
    {
        SceneTree tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;

        foreach (Node node in tree.GetNodesInGroup("bot"))
        {
            if (node is Bot bot)
                Apply(bot);
        }
    }

    public static void Apply(Bot bot)
    {
        switch (Current)
        {
            case BotLevel.Easy:
                // Percebe tarde, demora a se virar, erra quase metade
                // dos tiros e desiste rápido de perseguir.
                bot.SightRange = 26f;
                bot.FieldOfView = 100f;
                bot.Memory = 1.5f;
                bot.Hearing = 0.6f;
                bot.HearingError = 5f;

                bot.ReactionTime = 0.8f;
                bot.ReactionJitter = 0.35f;
                bot.AlertTurnSpeed = 2.8f;
                bot.TurnSpeed = 4f;

                bot.AimError = 9f;
                bot.MissChance = 0.45f;

                bot.BurstTime = 0.45f;
                bot.BurstPause = 1.3f;
                bot.EngageRange = 24f;

                bot.Speed = 3.4f;
                bot.StrafeFactor = 0.5f;
                bot.StrafeInterval = 2.2f;
                bot.PreferredRange = 11f;
                bot.Aggression = 0.5f;
                bot.PatrolSpeedFactor = 0.45f;
                bot.PatrolPause = 2.2f;
                break;

            case BotLevel.Medium:
                bot.SightRange = 40f;
                bot.FieldOfView = 140f;
                bot.Memory = 3f;
                bot.Hearing = 1f;
                bot.HearingError = 2.5f;

                bot.ReactionTime = 0.4f;
                bot.ReactionJitter = 0.2f;
                bot.AlertTurnSpeed = 4.5f;
                bot.TurnSpeed = 8f;

                bot.AimError = 4.5f;
                bot.MissChance = 0.2f;

                bot.BurstTime = 0.7f;
                bot.BurstPause = 0.65f;
                bot.EngageRange = 40f;

                bot.Speed = 4.2f;
                bot.StrafeFactor = 0.8f;
                bot.StrafeInterval = 1.4f;
                bot.PreferredRange = 9f;
                bot.Aggression = 0.8f;
                bot.PatrolSpeedFactor = 0.55f;
                bot.PatrolPause = 1.5f;
                break;

            case BotLevel.Hard:
                // Continua errando de propósito: um bot que acerta
                // sempre não é difícil, é injusto, e o jogador para de
                // tentar em vez de melhorar.
                bot.SightRange = 55f;
                bot.FieldOfView = 170f;
                bot.Memory = 5f;
                bot.Hearing = 1.35f;
                bot.HearingError = 1.2f;

                bot.ReactionTime = 0.2f;
                bot.ReactionJitter = 0.1f;
                bot.AlertTurnSpeed = 7.5f;
                bot.TurnSpeed = 11f;

                bot.AimError = 2f;
                bot.MissChance = 0.08f;

                bot.BurstTime = 0.95f;
                bot.BurstPause = 0.35f;
                bot.EngageRange = 55f;

                bot.Speed = 4.9f;
                bot.StrafeFactor = 1f;
                bot.StrafeInterval = 0.9f;
                bot.PreferredRange = 7f;
                bot.Aggression = 1f;
                bot.PatrolSpeedFactor = 0.7f;
                bot.PatrolPause = 0.9f;
                break;
        }
    }

    public static string Label(BotLevel level) => level switch
    {
        BotLevel.Easy => "FÁCIL",
        BotLevel.Medium => "MÉDIO",
        BotLevel.Hard => "DIFÍCIL",
        _ => ""
    };
}
