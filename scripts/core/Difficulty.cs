using Godot;
using System.Collections.Generic;

public enum BotLevel
{
	Easy,
	Medium,
	Hard
}

/// <summary>
/// A dificuldade dos bots na partida: qual nível vale e escrevê-lo em cada
/// bot. Os números de cada nível são dado, não código — um
/// <see cref="BotDifficulty"/> por nível em resources/bots/.
///
/// O que muda entre os níveis não é vida nem dano — é **competência**.
/// Inflar vida do inimigo para "dificultar" produz luta longa e chata,
/// não luta difícil. As quatro alavancas, em ordem de impacto real:
///   1. Reação — quanto tempo até se virar e atirar
///   2. Erro de mira — quanto desvia do alvo
///   3. Taxa de erro deliberado — quantos tiros vão longe de propósito
///   4. Sustentação — quanto tempo atira antes de pausar
///
/// O fácil percebe tarde, demora a se virar, erra quase metade dos tiros
/// e desiste rápido de perseguir. O difícil continua errando de
/// propósito: um bot que acerta sempre não é difícil, é injusto, e o
/// jogador para de tentar em vez de melhorar.
/// </summary>
public static class Difficulty
{
	private static readonly Dictionary<BotLevel, string> Files = new()
	{
		[BotLevel.Easy] = "res://resources/bots/facil.tres",
		[BotLevel.Medium] = "res://resources/bots/medio.tres",
		[BotLevel.Hard] = "res://resources/bots/dificil.tres",
	};

	private static readonly Dictionary<BotLevel, BotDifficulty> Loaded = new();

	public static BotLevel Current { get; private set; } = BotLevel.Medium;

	public static void Set(BotLevel level)
	{
		Current = level;
		ApplyToAll();
	}

	/// <summary>Os números de um nível. Sem o arquivo, avisa e usa os do
	/// médio (os padrões do <see cref="BotDifficulty"/>), para a partida
	/// não cair por falta de um .tres.</summary>
	public static BotDifficulty Preset(BotLevel level)
	{
		if (Loaded.TryGetValue(level, out BotDifficulty preset))
			return preset;

		preset = ResourceLoader.Load<BotDifficulty>(Files[level]);
		if (preset == null)
		{
			GD.PushError($"Dificuldade: não achei {Files[level]}; usando os valores do médio.");
			preset = new BotDifficulty();
		}

		Loaded[level] = preset;
		return preset;
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

	public static void Apply(Bot bot) => Preset(Current).ApplyTo(bot);

	public static string Label(BotLevel level) => Preset(level).Label;

	public static string Summary(BotLevel level) => Preset(level).Summary;
}
