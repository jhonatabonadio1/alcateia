using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// Os níveis dos bots (resources/bots/*.tres). Não confere número por
/// número — esses são para ajustar à vontade —, confere a regra: cada
/// nível é mais competente que o anterior em tudo, nem o difícil acerta
/// sempre, e o nível escolhido chega ao bot.
/// </summary>
public sealed class DifficultyTest : RegressionTest
{
	public override string Name => "Dificuldade";
	public override string Description => "fácil < médio < difícil em competência, o difícil ainda erra, e o nível chega ao bot";

	/// <summary>Campo, e se "mais competente" é maior (+1) ou menor (−1).</summary>
	private static readonly (string Name, Func<BotDifficulty, float> Get, int Better)[] Levers =
	{
		("SightRange", d => d.SightRange, +1),
		("FieldOfView", d => d.FieldOfView, +1),
		("Memory", d => d.Memory, +1),
		("Hearing", d => d.Hearing, +1),
		("HearingError", d => d.HearingError, -1),
		("Curiosity", d => d.Curiosity, +1),
		("ReactionTime", d => d.ReactionTime, -1),
		("ReactionJitter", d => d.ReactionJitter, -1),
		("AlertTurnSpeed", d => d.AlertTurnSpeed, +1),
		("TurnSpeed", d => d.TurnSpeed, +1),
		("AimError", d => d.AimError, -1),
		("MissChance", d => d.MissChance, -1),
		("BurstTime", d => d.BurstTime, +1),
		("BurstPause", d => d.BurstPause, -1),
		("EngageRange", d => d.EngageRange, +1),
		("Speed", d => d.Speed, +1),
		("StrafeFactor", d => d.StrafeFactor, +1),
		("StrafeInterval", d => d.StrafeInterval, -1),
		("Aggression", d => d.Aggression, +1),
		("WolfSpecialRate", d => d.WolfSpecialRate, +1),
		("PounceError", d => d.PounceError, -1),
	};

	public override async Task Run(RegressionBench b)
	{
		BotLevel before = Difficulty.Current;
		var easy = Difficulty.Preset(BotLevel.Easy);
		var medium = Difficulty.Preset(BotLevel.Medium);
		var hard = Difficulty.Preset(BotLevel.Hard);

		b.Check(easy.Label == "FÁCIL" && medium.Label == "MÉDIO" && hard.Label == "DIFÍCIL",
			$"os três arquivos carregam ({easy.Label}, {medium.Label}, {hard.Label})");

		foreach (var (name, get, better) in Levers)
		{
			float e = get(easy) * better, m = get(medium) * better, h = get(hard) * better;
			b.Check(e < m && m < h,
				$"{name} melhora a cada nível ({RegressionBench.F(get(easy))} → {RegressionBench.F(get(medium))} → {RegressionBench.F(get(hard))})");
		}

		b.Check(hard.MissChance > 0 && hard.AimError > 0 && hard.PounceError > 0, "o difícil ainda erra de propósito");

		// O nível escolhido chega aos bots da cena.
		b.RemoveBots(keep: 1);
		var bot = (Bot)b.Bots()[0];
		Difficulty.Set(BotLevel.Hard);
		await b.PhysicsFrame();
		b.Near(hard.ReactionTime, bot.ReactionTime, 1e-6, "reação do bot no difícil");
		b.Near(hard.AimError, bot.AimError, 1e-6, "mira do bot no difícil");
		Difficulty.Set(BotLevel.Easy);
		b.Near(easy.Speed, bot.Speed, 1e-6, "velocidade do bot no fácil");
		Difficulty.Set(before);
	}
}
