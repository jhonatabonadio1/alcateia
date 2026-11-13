using Godot;
using System.Threading.Tasks;

/// <summary>Começa correndo na parede da esquerda do treino (face em
/// z = −5,5, normal +Z), indo para +X.</summary>
public abstract class WallTestBase : RegressionTest
{
	protected static async Task<bool> StartRun(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();
		await b.Place(new Vector3(-8, 3, -5.1f), -90f, settle: 0.05);
		b.Player.Velocity = new Vector3(3.5f, 0, 0);
		RegressionBench.Press("move_forward");
		bool grabbed = await b.Until(() => b.Player.IsWallRunning, 1.0);
		b.Check(grabbed, "grudou na parede");
		return grabbed;
	}
}

/// <summary>O salto da parede: sai para fora e para cima, mais alto que o
/// pulo do chão, e conserva parte do avanço da corrida.</summary>
public sealed class WallJumpTest : WallTestBase
{
	public override string Name => "SaltoDaParede";
	public override string Description => "pular da parede empurra para fora e para cima e conserva 80% do avanço";

	public override async Task Run(RegressionBench b)
	{
		if (!await StartRun(b)) return;
		await b.Seconds(0.6);

		Player p = b.Player;
		float jumpScale = b.Form.Breed.JumpMultiplier;
		float run = p.HumanSpeed * b.Form.Breed.SpeedMultiplier * p.WallRunSpeedBonus;

		RegressionBench.Press("jump");
		bool left = await b.Until(() => !p.IsWallRunning, 0.2);
		Vector3 v = p.Velocity;
		RegressionBench.ReleaseAll();

		b.Check(left, "soltou a parede");
		b.Check(p.JumpedFromWall, "conta como salto da parede");
		b.Near(p.JumpVelocity * jumpScale * p.WallJumpClimb, v.Y, 0.25, "subida do salto");
		b.Near(p.WallJumpPush, v.Z, 0.25, "empurrão para fora da parede");
		b.Near(run * p.WallJumpKeepSpeed, v.X, 0.25, "avanço conservado");

		// A mesma parede não regruda sem tocar o chão.
		RegressionBench.Press("move_forward");
		await b.Seconds(0.5);
		b.Check(!p.IsWallRunning, "não regruda na mesma parede");
		RegressionBench.ReleaseAll();
	}
}

/// <summary>Segurar o ataque na parede agarra: o lobo freia até parar e
/// fica, sem cair.</summary>
public sealed class WallClingTest : WallTestBase
{
	public override string Name => "AgarraNaParede";
	public override string Description => "segurar o ataque na parede freia até parar, na mesma altura";

	public override async Task Run(RegressionBench b)
	{
		if (!await StartRun(b)) return;
		await b.Seconds(0.5);

		RegressionBench.Press("attack");
		await b.Seconds(0.6);
		Vector3 moved = await b.Displacement(b.Player, 20);

		b.Check(b.Player.IsWallRunning, "continua na parede");
		b.AtMost(0.02, Mathf.Abs(moved.X), "parado ao longo da parede");
		b.AtMost(0.02, Mathf.Abs(moved.Y), "sem cair");

		// Soltou o ataque, volta a correr.
		RegressionBench.Release("attack");
		await b.Seconds(0.6);
		moved = await b.Displacement(b.Player, 30);
		RegressionBench.ReleaseAll();
		b.AtLeast(3.5, moved.X / 0.5, "voltou a correr");
	}
}

/// <summary>Colado na parede, um pulo agarra logo: o lobo não precisa
/// chegar ao alto do pulo (pedido do Jhon, 13/11).</summary>
public sealed class WallLowGrabTest : RegressionTest
{
	public override string Name => "AgarraBaixo";
	public override string Description => "colado na parede, o pulo agarra perto do chão, sem esperar o alto do pulo";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();

		// No chão, colado à parede da esquerda do treino (z = −5,5).
		await b.Place(new Vector3(-8, 1, -5.1f), -90f, settle: 0.4);
		Player p = b.Player;
		float floor = p.GlobalPosition.Y;

		RegressionBench.Press("move_forward");
		await b.Frames(3);
		RegressionBench.Press("jump");
		await b.Frames(2);
		RegressionBench.Release("jump");

		bool grabbed = await b.Until(() => p.IsWallRunning, 1.0);
		float height = p.GlobalPosition.Y - floor;
		bool rising = p.Velocity.Y >= 0f;
		RegressionBench.ReleaseAll();

		float apex = Mathf.Pow(p.JumpVelocity * b.Form.Breed.JumpMultiplier, 2) / (2f * (float)ProjectSettings.GetSetting("physics/3d/default_gravity"));
		b.Check(grabbed, "agarrou");
		b.AtMost(apex * 0.75f, height, $"agarrou antes do alto do pulo (alto a {apex:0.00} m)");
		b.AtLeast(p.WallGrabHeight - 0.05f, height, "mas não rente ao chão");
		b.Check(rising, "agarrou ainda subindo");
	}
}
