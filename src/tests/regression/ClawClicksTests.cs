using Godot;
using System.Threading.Tasks;

/// <summary>Um clique, uma garrada (pedido do Jhon, 18/11): cada clique vira
/// um golpe inteiro, alternando os braços; cliques dados durante o golpe
/// ficam guardados e saem em seguida; e a garrada não demora.</summary>
public sealed class ClawClicksTest : RegressionTest
{
	public override string Name => "CliquesDaGarra";
	public override string Description => "um clique, uma garrada: braços alternados, cliques rápidos guardados, ritmo de 0,34 s";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
		Weapon w = b.Weapon;

		var moves = new System.Collections.Generic.List<(int Move, double At)>();
		double clock = 0;
		void OnMove(int move) => moves.Add((move, clock));
		w.WolfMoveStarted += OnMove;

		async Task Run(double seconds)
		{
			double until = clock + seconds;
			while (clock < until)
			{
				await b.PhysicsFrame();
				clock += 1.0 / Engine.PhysicsTicksPerSecond;
			}
		}

		// ---- um clique sozinho: uma garrada, e só uma.
		await b.Tap("attack");
		await Run(1.0);
		b.Check(moves.Count == 1, $"um clique, uma garrada (saíram {moves.Count})");

		// ---- três cliques bem rápidos (0,1 s entre eles): três garradas, braços alternados.
		moves.Clear();
		clock = 0;
		for (int i = 0; i < 3; i++)
		{
			Input.ActionPress("attack");
			await Run(0.05);
			Input.ActionRelease("attack");
			await Run(0.05);
		}
		await Run(1.4);
		b.Check(moves.Count == 3, $"três cliques rápidos, três garradas (saíram {moves.Count})");
		if (moves.Count == 3)
		{
			b.Check(moves[0].Move != moves[1].Move && moves[1].Move != moves[2].Move, "os braços alternam");
			double gap = (moves[2].At - moves[0].At) / 2.0;
			RegressionBench.Note($"ritmo encadeado: {RegressionBench.F(gap)} s entre garradas ({RegressionBench.F(1.0 / gap)} por segundo)");
			b.Near(WolfMoves.ImpactDelay(WolfMove.Claw) + w.ClawCancelAfter, gap, 0.04, "encadeadas, uma a cada 0,34 s");
			b.AtMost(0.45, gap, "a garrada não demora (s entre golpes)");
		}

		// ---- segurar o botão não repete: é um clique por golpe.
		moves.Clear();
		Input.ActionPress("attack");
		await Run(1.2);
		Input.ActionRelease("attack");
		b.Check(moves.Count == 1, $"segurando o botão, sai uma garrada só (saíram {moves.Count})");

		// ---- o golpe chega rápido: do clique ao dano.
		b.AtMost(0.18, WolfMoves.ImpactDelay(WolfMove.Claw), "do clique ao dano (s)");
		b.AtMost(0.60, WolfMoves.Duration(WolfMove.Claw), "o golpe inteiro (s)");

		w.WolfMoveStarted -= OnMove;
	}
}
