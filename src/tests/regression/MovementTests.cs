using Godot;
using System.Threading.Tasks;

/// <summary>Velocidades no chão (regra 9 do CLAUDE.md): o humano anda a
/// passada do clipe, o lobo é multiplicador dele.</summary>
public sealed class GroundSpeedTest : RegressionTest
{
	public override string Name => "VelocidadeNoChao";
	public override string Description => "humano 3,46 m/s, agachado 1,56, faca 3,58; lobo 4,33, agachado 1,95";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();

		// Os números do jogo de hoje: Player.HumanSpeed 3,2 na cena (docs/MEDIDAS.md),
		// × 1,08 do humano. Eram 3,24 (a base 3,0 do código, que a cena sobrescreve).
		b.Near(3.456, await Measure(b, "move_forward"), 0.02, "humano correndo");
		b.Near(1.555, await Measure(b, "move_forward", "crouch"), 0.05, "humano agachado");
		b.Near(1.452, await Measure(b, "move_forward", "walk"), 0.05, "humano andando (Shift)");

		b.Weapon.SelectSlot(Slot.Knife);
		b.Near(3.584, await Measure(b, "move_forward"), 0.02, "humano com a faca");
		b.Weapon.SelectSlot(Slot.Primary);

		// O lobo é um só desde 26/11 (decisão do Jhon: sem classes), no meio do
		// Bruto (4,15) e do Ligeiro (4,50): a base 3,2 × 1,352.
		await b.BecomeWolf();
		b.Near(4.326, await Measure(b, "move_forward"), 0.03, "lobo correndo");
		b.Near(1.947, await Measure(b, "move_forward", "crouch"), 0.05, "lobo agachado");
		b.Near(4.326, await Measure(b, "move_right"), 0.03, "lobo de lado");
		// Para trás o lobo anda a 75% (WolfBackFactor, pedido do Jhon, 15/11).
		b.Near(4.326 * b.Player.WolfBackFactor, await Measure(b, "move_back"), 0.03, "lobo para trás");
	}

	/// <summary>Velocidade média depois de embalar, no campo aberto.</summary>
	private static async Task<float> Measure(RegressionBench b, params string[] keys)
	{
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
		RegressionBench.Press(keys);
		await b.Seconds(0.6);
		Vector3 moved = await b.Displacement(b.Player, 60);
		RegressionBench.ReleaseAll();
		return RegressionBench.Flat(moved) / 1.0f;
	}
}

/// <summary>Altura do pulo, amarrada ao mapa: o humano sobe caixote (0,8 m)
/// e não contêiner; o lobo alcança o contêiner a partir do caixote.</summary>
public sealed class JumpHeightTest : RegressionTest
{
	public override string Name => "AlturaDoPulo";
	public override string Description => "humano pula 0,99 m, lobo 2,09 m";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		b.Near(0.99, await Measure(b), 0.04, "pulo do humano");

		await b.BecomeWolf();
		// O lobo único (26/11), no meio do Bruto (1,62) e do Ligeiro (2,56): ×1,435.
		b.Near(2.09, await Measure(b), 0.05, "pulo do lobo");
	}

	private static async Task<float> Measure(RegressionBench b)
	{
		await b.Place(new Vector3(-15, 1, 13), -90f);
		float floor = b.Player.GlobalPosition.Y;
		float top = floor;

		RegressionBench.Press("jump");
		double end = b.Time + 1.6;
		while (b.Time < end)
		{
			await b.PhysicsFrame();
			top = Mathf.Max(top, b.Player.GlobalPosition.Y);
		}
		RegressionBench.ReleaseAll();
		return top - floor;
	}
}

/// <summary>Corrida na parede: 7% além do chão (decisão do Jhon,
/// v0.26.11), na altura em que grudou.</summary>
public sealed class WallRunTest : RegressionTest
{
	public override string Name => "CorridaNaParede";
	public override string Description => "o lobo corre na parede a 4,94 m/s sem subir nem descer";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();

		// No ar, encostado na parede da esquerda (face em z = −5,5),
		// andando ao longo dela com W.
		await b.Place(new Vector3(-8, 3, -5.1f), -90f, settle: 0.05);
		b.Player.Velocity = new Vector3(3.5f, 0, 0);
		RegressionBench.Press("move_forward");

		bool grabbed = await b.Until(() => b.Player.IsWallRunning, 1.0);
		b.Check(grabbed, "grudou na parede");
		await b.Seconds(1.2);

		Vector3 moved = await b.Displacement(b.Player, 30);
		RegressionBench.ReleaseAll();

		b.Check(b.Player.IsWallRunning, "continuava na parede");
		// 4,944: a base 3,2 da cena × 1,352 do lobo único (26/11; era 1,298 do Bruto,
		// 4,746) × 1,1427 da parede.
		b.Near(4.944, moved.X / 0.5, 0.05, "velocidade na parede");
		b.Near(0, moved.Y, 0.02, "altura travada");
	}
}
