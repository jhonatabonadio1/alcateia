using Godot;
using System.Threading.Tasks;

/// <summary>
/// Depois do bote (pulo + botão direito), a garra sai na hora: o dano do
/// bote caiu, o clique esquerdo já arranha e solta o lobo da aterrissagem
/// (pedido do Jhon, 18/11: "é um jogo rápido, de decisão rápida").
/// </summary>
public sealed class DiveFollowUpTest : RegressionTest
{
	public override string Name => "GarraDepoisDoBote";
	public override string Description => "depois do dano do bote no ar, a garra sai em menos de 0,15 s e solta o lobo da aterrissagem";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
		Weapon w = b.Weapon;

		int claws = 0;
		w.WolfMoveStarted += move => { if ((WolfMove)move is WolfMove.Claw or WolfMove.Punch) claws++; };

		await b.Tap("jump");
		await b.Seconds(0.28);
		await b.Tap("special");
		b.Check(await b.Until(() => w.IsDiving, 0.4), "pulo + botão direito: o bote sai");
		b.Check(await b.Until(() => !w.IsDiving, 2.0), "o bote encosta no chão");
		b.Check(w.IsRooted, "sem clicar, a aterrissagem prende o lobo");

		await b.Seconds(0.07);
		await b.Tap("attack");
		await b.Frames(3);
		b.Check(claws == 1, $"0,07 s depois do dano, a garra já sai ({claws} golpe)");
		b.Check(!w.IsRooted, "a garra solta o lobo da aterrissagem");

		// E o combo segue no ritmo normal da garra.
		await b.Seconds(0.75);
		await b.Tap("attack");
		await b.Frames(3);
		b.Check(claws == 2, "o segundo golpe sai em seguida");
	}
}

/// <summary>
/// Correndo de quatro (W W) o lobo não arranha: tem de sair da corrida
/// para bater (regra do Jhon, 18/11).
/// </summary>
public sealed class NoClawOnSprintTest : RegressionTest
{
	public override string Name => "SemGarraNaCorrida";
	public override string Description => "na corrida de quatro (W W) o clique não arranha; saiu da corrida, arranha";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
		Weapon w = b.Weapon;
		int claws = 0;
		w.WolfMoveStarted += move => { if ((WolfMove)move is WolfMove.Claw or WolfMove.Punch) claws++; };

		await b.Tap("move_forward");
		await b.Frames(3);
		Input.ActionPress("move_forward");
		await b.Seconds(0.5);
		b.Check(b.Player.IsSprinting, "W W: correndo de quatro");
		await b.Tap("attack");
		await b.Frames(4);
		b.Check(claws == 0 && b.Player.IsSprinting, "na corrida o clique não arranha, e a corrida segue");

		// Pulou da corrida (o W segue apertado): no ar a garra sai (Jhon, 23/11: "correndo
		// de 4 e pulo, não consigo dar ataque primário, só secundário").
		await b.Tap("jump");
		await b.Frames(10);
		b.Check(!b.Player.IsOnFloor(), "pulou da corrida");
		await b.Tap("attack");
		await b.Frames(4);
		b.Check(claws == 1, $"no ar, pulando da corrida, a garra sai ({claws} golpes)");
		for (int i = 0; i < 240 && !b.Player.IsOnFloor(); i++) await b.Frames(1);
		await b.Seconds(1.0);
		claws = 0;

		Input.ActionRelease("move_forward");
		await b.Frames(4);
		b.Check(!b.Player.IsSprinting, "soltou o W: saiu da corrida");
		await b.Tap("attack");
		await b.Frames(4);
		b.Check(claws == 1, "fora da corrida, a garra sai");
	}
}

/// <summary>
/// No ar, o bote corta a garrada (Jhon, 23/11: "se eu estiver no ar apertando o ataque
/// primário e apertar o secundário, ele deve interromper o primário e acionar o secundário
/// imediatamente"). Antes a garrada segurava o bote até acabar.
/// </summary>
public sealed class DiveCutsClawTest : RegressionTest
{
	public override string Name => "BoteCortaGarra";
	public override string Description => "no ar, garrada e logo o botão direito: o bote sai na hora e a garrada some da tela";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
		Weapon w = b.Weapon;
		int claws = 0;
		bool cancelled = false;
		w.WolfMoveStarted += move => { if ((WolfMove)move is WolfMove.Claw or WolfMove.Punch) claws++; };
		w.WolfMoveCancelled += move => { if ((WolfMove)move is WolfMove.Claw or WolfMove.Punch) cancelled = true; };

		await b.Tap("jump");
		await b.Seconds(0.28);
		await b.Tap("attack");
		await b.Frames(2);
		b.Check(claws == 1 && w.IsStriking && !w.IsDiving, "no ar, a garrada sai");
		await b.Tap("special");
		b.Check(await b.Until(() => w.IsDiving, 0.05), "o botão direito no meio da garrada: o bote sai na hora");
		b.Check(cancelled, "a garrada é cortada na tela");
		b.Check(await b.Until(() => !w.IsDiving, 2.0), "o bote encosta no chão");
	}
}
