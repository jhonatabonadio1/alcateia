using Godot;
using System.Threading.Tasks;

/// <summary>Velocidades no chão (regra 9 do CLAUDE.md): o humano anda a
/// passada do clipe, o lobo é multiplicador dele.</summary>
public sealed class GroundSpeedTest : RegressionTest
{
	public override string Name => "VelocidadeNoChao";
	public override string Description => "humano 3,0 m/s, agachado 1,35, faca 3,84; Bruto 4,15, agachado 1,87";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();

		// Os números do jogo de hoje: Player.HumanSpeed 3,2 na cena (docs/MEDIDAS.md,
		// a base do lobo), × 0,9375 do humano desde 26/11 (era 1,08: 3,46 m/s; Jhon:
		// "deve andar um pouco mais devagar", "tá deslizando no mapa"). Agachado e
		// com Shift descem junto.
		b.Near(3.0, await Measure(b, "move_forward"), 0.02, "humano correndo");
		b.Near(1.35, await Measure(b, "move_forward", "crouch"), 0.05, "humano agachado");
		b.Near(1.26, await Measure(b, "move_forward", "walk"), 0.05, "humano andando (Shift)");

		b.Weapon.SelectSlot(Slot.Knife);
		// A faca na mão ×1,28 desde 26/11 (era ×1,037, 3,58 m/s; Jhon: "aumentar
		// velocidade de movimento com faca equipada"): 3,84 m/s.
		b.Near(3.84, await Measure(b, "move_forward"), 0.02, "humano com a faca");
		b.Weapon.SelectSlot(Slot.Primary);

		await b.BecomeWolf();
		b.Near(4.154, await Measure(b, "move_forward"), 0.03, "Bruto correndo");
		b.Near(1.869, await Measure(b, "move_forward", "crouch"), 0.05, "Bruto agachado");
		b.Near(4.154, await Measure(b, "move_right"), 0.03, "Bruto de lado");
		// Para trás o lobo anda a 75% (WolfBackFactor, pedido do Jhon, 15/11).
		b.Near(4.154 * b.Player.WolfBackFactor, await Measure(b, "move_back"), 0.03, "Bruto para trás");
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
/// e não contêiner; o Bruto alcança o contêiner a partir do caixote.</summary>
public sealed class JumpHeightTest : RegressionTest
{
	public override string Name => "AlturaDoPulo";
	public override string Description => "humano pula 0,99 m, Bruto 1,62 m";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		b.Near(0.99, await Measure(b), 0.04, "pulo do humano");

		await b.BecomeWolf();
		b.Near(1.62, await Measure(b), 0.05, "pulo do Bruto");
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
	public override string Description => "o Bruto corre na parede a 4,75 m/s sem subir nem descer";

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
		// 4,746: a base 3,2 da cena × 1,298 do Bruto × 1,1427 da parede (era 4,45 com a base 3,0).
		b.Near(4.746, moved.X / 0.5, 0.05, "velocidade na parede");
		b.Near(0, moved.Y, 0.02, "altura travada");
	}
}
