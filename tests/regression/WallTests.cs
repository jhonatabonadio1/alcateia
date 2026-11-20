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

/// <summary>Só gruda numa parede diferente (decisão do Jhon, 17/11):
/// saltando de uma parede olhando para ela e voltando, o lobo não gruda
/// de novo nela — cai até o chão. Até 17/11 ele regrudava abaixo da
/// altura do salto (bug do Jhon de 14/11, agora regra trocada).</summary>
public sealed class WallRegrabTest : WallTestBase
{
	public override string Name => "RegrudaNaParede";
	public override string Description => "saltar da parede e voltar não gruda na mesma parede; só numa outra, ou depois do chão";

	public override async Task Run(RegressionBench b)
	{
		if (!await StartRun(b)) return;
		await b.Seconds(0.3);

		Player p = b.Player;
		p.Rotation = Vector3.Zero; // olha para a parede (−Z)
		await b.Frames(2);

		RegressionBench.Press("jump");
		bool left = await b.Until(() => !p.IsWallRunning, 0.2);
		RegressionBench.Release("jump");
		b.Check(left, "soltou a parede");

		bool grabbed = await b.Until(() => p.IsWallRunning, 2.5);
		RegressionBench.ReleaseAll();
		b.Check(!grabbed, "não grudou de novo na mesma parede");
	}
}

/// <summary>Parede redonda (tonel, tanque): o lobo dá a volta inteira, sem
/// soltar e grudar de novo a cada pedaço da curva (Jhon, 15/11, tonel da
/// Estação Polar).</summary>
public sealed class WallBarrelTest : RegressionTest
{
	public override string Name => "VoltaNoTonel";
	public override string Description => "o lobo contorna um tonel na parede dele, a volta inteira, sem soltar";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();

		// Um tonel de 1,5 m de raio e 8 m de altura, acima de tudo.
		var center = new Vector3(-15, 14, 13);
		var barrel = new StaticBody3D();
		barrel.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 1.5f, Height = 8f } });
		b.Scene.AddChild(barrel);
		barrel.GlobalPosition = center;

		// Ao lado dele (+Z), no ar, correndo para +X ao longo da curva.
		await b.Place(center + new Vector3(0, 1, 2.1f), -90f, settle: 0.05);
		b.Player.Velocity = new Vector3(3.5f, 0, 0);
		RegressionBench.Press("move_forward");
		await b.Until(() => b.Player.IsWallRunning, 1.0);
		b.Check(b.Player.IsWallRunning, "grudou no tonel");

		int off = 0, frames = 0;
		float swept = 0, last = Angle(b, center), lowest = b.Player.GlobalPosition.Y, highest = lowest;
		double end = b.Time + 3.0;
		while (b.Time < end)
		{
			await b.PhysicsFrame();
			frames++;
			if (!b.Player.IsWallRunning) off++;
			float now = Angle(b, center);
			swept += Mathf.Abs(Mathf.Wrap(now - last, -Mathf.Pi, Mathf.Pi));
			last = now;
			lowest = Mathf.Min(lowest, b.Player.GlobalPosition.Y);
			highest = Mathf.Max(highest, b.Player.GlobalPosition.Y);
		}
		RegressionBench.ReleaseAll();
		barrel.QueueFree();

		RegressionBench.Note($"tonel: {RegressionBench.F(Mathf.RadToDeg(swept))}° em 3 s, {off} de {frames} quadros fora da parede, altura variou {RegressionBench.F(highest - lowest)} m");
		b.Check(off == 0, "não soltou do tonel nenhuma vez");
		b.AtLeast(360, Mathf.RadToDeg(swept), "deu a volta inteira (graus)");
		b.AtMost(0.3, highest - lowest, "sem subir nem cair (m)");
	}

	private static float Angle(RegressionBench b, Vector3 center)
	{
		Vector3 d = b.Player.GlobalPosition - center;
		return Mathf.Atan2(d.Z, d.X);
	}
}

/// <summary>Parede invisível do limite do mapa (as <c>Limite…</c> e as
/// <c>Barreira…</c> do alto do muro): o lobo não corre nela (Jhon, 20/11).
/// A mesma parede com outro nome serve de controle: nela ele gruda.</summary>
public sealed class InvisibleWallTest : WallTestBase
{
	public override string Name => "ParedeInvisivel";
	public override string Description => "o lobo não gruda nem corre na parede invisível do limite do mapa";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();
		Player p = b.Player;

		foreach (string name in new[] { "ParedeTeste", "BarreiraTeste", "LimiteTeste" })
		{
			// Lá no alto, longe do cenário da bancada: só esta parede.
			var wall = new StaticBody3D { Name = name, Position = new Vector3(0, 40, -2f) };
			wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(30, 12, 0.4f) } });
			b.GetTree().CurrentScene.AddChild(wall);
			await b.Frames(2);

			await b.Place(new Vector3(-6, 40, -1.45f), -90f, settle: 0.02);
			p.Velocity = new Vector3(3.5f, 0, 0);
			RegressionBench.Press("move_forward");
			bool grabbed = await b.Until(() => p.IsWallRunning, 0.7);
			RegressionBench.ReleaseAll();

			if (name == "ParedeTeste") b.Check(grabbed, "controle: na parede comum ele gruda");
			else b.Check(!grabbed, $"não gruda na {name}");

			wall.QueueFree();
			await b.Frames(2);
		}

		// O muro que fecha o mapa: parede comum com a barreira 6 cm atrás da face.
		{
			var wall = new StaticBody3D { Name = "MuroTeste", Position = new Vector3(0, 40, -2f) };
			wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(30, 12, 0.4f) } });
			var barrier = new StaticBody3D { Name = "BarreiraAtras", Position = new Vector3(0, 40, -2.06f) };
			barrier.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(30, 30, 0.4f) } });
			b.GetTree().CurrentScene.AddChild(wall);
			b.GetTree().CurrentScene.AddChild(barrier);
			await b.Frames(2);

			await b.Place(new Vector3(-6, 40, -1.45f), -90f, settle: 0.02);
			p.Velocity = new Vector3(3.5f, 0, 0);
			RegressionBench.Press("move_forward");
			bool grabbed = await b.Until(() => p.IsWallRunning, 0.7);
			RegressionBench.ReleaseAll();
			b.Check(!grabbed, "não gruda no muro que tem a barreira do limite por trás");

			wall.QueueFree();
			barrier.QueueFree();
			await b.Frames(2);
		}
	}
}
