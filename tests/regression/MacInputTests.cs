using Godot;
using System.Threading.Tasks;

/// <summary>
/// No Mac, Ctrl + clique esquerdo chega como clique direito. Agachado no
/// Ctrl, clicar precisa atirar (humano) e arranhar (lobo) — não virar o
/// especial (<see cref="MacCtrlClick"/>). O teste manda o clique direito
/// com Ctrl que o Godot do Mac mandaria.
/// </summary>
public sealed class MacCtrlClickTest : RegressionTest
{
	public override string Name => "CtrlCliqueNoMac";
	public override string Description => "agachado no Ctrl, o Ctrl-clique do Mac atira e arranha em vez de virar o especial";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		bool wasEnabled = MacCtrlClick.Enabled;
		MacCtrlClick.Enabled = true;

		try
		{
			// Humano agachado: atira.
			RegressionBench.Press("crouch");
			await b.Seconds(0.5);
			int before = b.Weapon.PrimaryAmmo;
			await CtrlClick(b, hold: 0.3);
			b.Check(b.Player.IsCrouching, "agachado");
			b.Check(b.Weapon.PrimaryAmmo < before, $"atirou agachado ({before} → {b.Weapon.PrimaryAmmo})");

			// Lobo agachado: arranha, não carrega o salto.
			RegressionBench.ReleaseAll();
			await b.BecomeWolf();
			RegressionBench.Press("crouch");
			await b.Seconds(0.5);
			bool charged = false, struck = false;
			Input.ParseInputEvent(Click(pressed: true));
			for (int frame = 0; frame < 20; frame++)
			{
				await b.PhysicsFrame();
				charged |= b.Weapon.IsChargingPounce;
				struck |= b.Weapon.IsStriking;
			}
			Input.ParseInputEvent(Click(pressed: false));
			b.Check(struck, "o lobo arranhou");
			b.Check(!charged, "não carregou o salto");

			// Sem Ctrl, o clique direito continua sendo o especial.
			RegressionBench.ReleaseAll();
			await b.Seconds(1.5);
			Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
			await b.Seconds(0.2);
			b.Check(b.Weapon.IsChargingPounce, "sem Ctrl, o direito carrega o salto");
			Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false });
			await b.Seconds(0.5);
		}
		finally
		{
			MacCtrlClick.Enabled = wasEnabled;
			RegressionBench.ReleaseAll();
		}
	}

	private static InputEventMouseButton Click(bool pressed) =>
		new() { ButtonIndex = MouseButton.Right, Pressed = pressed, CtrlPressed = pressed };

	private static async Task CtrlClick(RegressionBench b, double hold)
	{
		Input.ParseInputEvent(Click(pressed: true));
		await b.Seconds(hold);
		Input.ParseInputEvent(Click(pressed: false));
		await b.Seconds(0.1);
	}
}
