using Godot;
using System.Threading.Tasks;

/// <summary>
/// Shift preso (18/11, Jhon: "aperto e solto e ele continua andando
/// lento"): o "soltou" do Shift não chega (o sistema engole), e o humano
/// seguia andando devagar. O próximo evento de mouse, que diz que o Shift
/// não está apertado, solta a ação (<see cref="StuckKeys"/>). Apertar por
/// código (sem tecla) não é tocado.
/// </summary>
public sealed class StuckKeysTest : RegressionTest
{
	public override string Name => "ShiftPreso";
	public override string Description => "Shift cujo soltar o sistema engoliu: o próximo evento de mouse solta o andar devagar";

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
		float slow = RegressionBench.Flat(b.Player.Velocity);
		b.Check(Input.IsActionPressed("walk"), "com o Shift, anda devagar");

		// O Shift sobe, mas o evento de soltar NÃO chega. O mouse mexe, sem Shift.
		int freed = StuckKeys.Freed;
		Input.ParseInputEvent(new InputEventMouseMotion { Relative = new Vector2(1, 0) });
		await b.Seconds(0.8);
		float fast = RegressionBench.Flat(b.Player.Velocity);
		RegressionBench.Note($"com Shift {RegressionBench.F(slow)} m/s; depois do Shift preso solto {RegressionBench.F(fast)} m/s");
		b.Check(!Input.IsActionPressed("walk"), "o evento de mouse sem Shift solta o andar devagar");
		b.Check(StuckKeys.Freed == freed + 1, "foi o vigia que soltou");
		b.AtLeast(slow * 1.8, fast, "voltou a correr (m/s)");

		// Apertado por código (teste, controle): o vigia não mexe.
		RegressionBench.Press("walk");
		Input.ParseInputEvent(new InputEventMouseMotion { Relative = new Vector2(1, 0) });
		await b.Seconds(0.2);
		b.Check(Input.IsActionPressed("walk"), "ação apertada por código não é solta pelo vigia");
		RegressionBench.ReleaseAll();
	}
}
