using Godot;
using System.Threading.Tasks;

/// <summary>
/// Shift preso (18/11, Jhon: "aperto e solto e ele continua andando
/// lento"): o "soltou" do Shift não chega (o sistema engole), e o humano
/// seguia andando devagar (hoje o Shift corre: seguiria correndo). O próximo evento de mouse, que diz que o Shift
/// não está apertado, solta a ação (<see cref="StuckKeys"/>). Apertar por
/// código (sem tecla) não é tocado.
/// </summary>
public sealed class StuckKeysTest : RegressionTest
{
	public override string Name => "ShiftPreso";
	public override string Description => "Shift cujo soltar o sistema engoliu: o próximo evento de mouse solta a corrida";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Seconds(0.5);

		// Aperta o Shift de verdade (evento de tecla) e anda.
		Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Shift, Keycode = Key.Shift, Pressed = true, ShiftPressed = true });
		RegressionBench.Press("move_forward");
		// O mouse mexe com o Shift apertado: nada muda.
		Input.ParseInputEvent(new InputEventMouseMotion { Relative = new Vector2(1, 0), ShiftPressed = true });
		await b.Seconds(0.8);
		// (Até 26/11 o Shift andava devagar; agora corre. O vigia é o mesmo.)
		float held = RegressionBench.Flat(b.Player.Velocity);
		b.Check(Input.IsActionPressed("sprint") && b.Player.IsHumanSprinting, "com o Shift, corre");

		// O Shift sobe, mas o evento de soltar NÃO chega. O mouse mexe, sem Shift.
		int freed = StuckKeys.Freed;
		Input.ParseInputEvent(new InputEventMouseMotion { Relative = new Vector2(1, 0) });
		await b.Seconds(0.8);
		float loose = RegressionBench.Flat(b.Player.Velocity);
		RegressionBench.Note($"com Shift {RegressionBench.F(held)} m/s; depois do Shift preso solto {RegressionBench.F(loose)} m/s");
		b.Check(!Input.IsActionPressed("sprint"), "o evento de mouse sem Shift solta a corrida");
		b.Check(StuckKeys.Freed == freed + 1, "foi o vigia que soltou");
		b.AtLeast(loose * 1.15, held, "com o Shift corria mais que sem ele (m/s)");
		b.Check(!b.Player.IsHumanSprinting, "e parou de correr");

		// Apertado por código (teste, controle): o vigia não mexe.
		RegressionBench.Press("sprint");
		Input.ParseInputEvent(new InputEventMouseMotion { Relative = new Vector2(1, 0) });
		await b.Seconds(0.2);
		b.Check(Input.IsActionPressed("sprint"), "ação apertada por código não é solta pelo vigia");
		RegressionBench.ReleaseAll();
	}
}
