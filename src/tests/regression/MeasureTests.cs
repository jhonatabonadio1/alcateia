using Godot;
using System.Threading.Tasks;

/// <summary>
/// As medidas de <c>docs/MEDIDAS.md</c>, medidas no jogo: velocidade,
/// altura do pulo, distância do pulo correndo, o degrau que se sobe sem
/// pular e o caixote que se sobe pulando — do humano e do lobo (um só
/// desde 26/11: as classes Bruto e Ligeiro saíram).
///
/// Os mapas são feitos com esses números. Se este teste falhar, uma
/// mudança de jogo mexeu numa medida: ou ela volta, ou o documento (e os
/// mapas que dependem dela) mudam junto.
/// </summary>
public sealed class MeasureTest : RegressionTest
{
	public override string Name => "Medidas";
	public override string Description => "as medidas de docs/MEDIDAS.md: velocidade, pulo, degrau e caixote do humano e do lobo";

	private static readonly Vector3 At = new(-15, 1, 13);

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();

		var human = await Measure(b, "humano", false);
		// 2,82 desde 27/11 (HumanSpeedBonus 0,88, "pode acelerar um pouco"; era 2,56, e antes 3,46).
		b.Near(2.82, human.Run, 0.05, "humano correndo (m/s)");
		b.Near(1.02, human.Rise, 0.04, "humano: o pulo sobe (m)");
		// ~2,1 m desde 27/11: o humano anda 2,82 m/s (1,9 a 2,56; a 3,46 o pulo ia 2,6 m).
		b.Near(2.1, human.Leap, 0.25, "humano: pulo correndo vai (m)");
		b.AtLeast(1.0, human.Box, "humano sobe pulando num caixote de 1 m");
		b.AtMost(1.15, human.Box, "humano não sobe pulando em 1,2 m");

		// O lobo único (decisão do Jhon, 26/11), no meio do Bruto e do Ligeiro de
		// antes: correndo 4,15 e 4,50, de quatro 7,14 e 7,73, o pulo 1,67 e 2,56 m.
		var wolf = await Measure(b, "lobo", true);
		b.Near(4.33, wolf.Run, 0.06, "lobo correndo (m/s)");
		b.Near(7.44, wolf.Sprint, 0.12, "lobo de quatro (m/s)");
		b.Near(2.09, wolf.Rise, 0.05, "lobo: o pulo sobe (m)");
		b.AtLeast(0.8, wolf.Box, "lobo sobe pulando num caixote de 0,8 m");
	}

	private record struct Result(float Run, float Crouch, float Sprint, float Rise, float Leap, float Step, float Box);

	private static async Task<Result> Measure(RegressionBench b, string who, bool wolf)
	{
		var p = b.Player;
		if (wolf) await b.BecomeWolf();

		float run = await Speed(b, false, "move_forward");
		float crouch = await Speed(b, false, "move_forward", "crouch");
		// O de cima do correr: o lobo de quatro (W duas vezes); o humano não tem.
		float sprint = wolf ? await Speed(b, true, "move_forward") : 0f;

		// Pulo parado: o quanto o corpo sobe.
		await b.Place(At, -90f, settle: 0.6);
		float y0 = p.GlobalPosition.Y, top = y0;
		RegressionBench.Press("jump");
		double end = b.Time + 1.6;
		while (b.Time < end)
		{
			await b.PhysicsFrame();
			top = Mathf.Max(top, p.GlobalPosition.Y);
		}
		RegressionBench.ReleaseAll();
		await b.Seconds(0.3);

		// Pulo correndo: do chão ao chão.
		await b.Place(At, -90f, settle: 0.5);
		RegressionBench.Press("move_forward");
		await b.Seconds(0.8);
		float x0 = p.GlobalPosition.X;
		await b.Tap("jump");
		await b.Until(() => !p.IsOnFloor(), 0.3);
		await b.Until(() => p.IsOnFloor(), 2.0);
		float leap = p.GlobalPosition.X - x0;
		RegressionBench.ReleaseAll();
		await b.Seconds(0.3);

		float step = 0, box = 0;
		foreach (float h in new[] { 0.05f, 0.1f, 0.15f, 0.2f, 0.3f })
			if (await Climbs(b, h, false)) step = h;
		foreach (float h in new[] { 0.6f, 0.7f, 0.8f, 0.9f, 1.0f, 1.1f, 1.2f, 1.4f, 1.6f, 1.8f, 2.0f, 2.2f, 2.4f, 2.6f })
			if (await Climbs(b, h, true)) box = h;

		RegressionBench.Note($"{who}: correndo {RegressionBench.F(run)} m/s, agachado {RegressionBench.F(crouch)}"
			+ (wolf ? $", de quatro {RegressionBench.F(sprint)}" : "")
			+ $"; o pulo sobe {RegressionBench.F(top - y0)} m, correndo vai {RegressionBench.F(leap)} m;"
			+ $" sobe sem pular {RegressionBench.F(step)} m, pulando {RegressionBench.F(box)} m");
		return new Result(run, crouch, sprint, top - y0, leap, step, box);
	}

	private static async Task<float> Speed(RegressionBench b, bool sprint, params string[] keys)
	{
		await b.Place(At, -90f, settle: 0.5);
		if (sprint)
		{
			await b.Tap("move_forward");
			await b.Seconds(0.08);
		}
		RegressionBench.Press(keys);
		await b.Seconds(1.0);
		float v = RegressionBench.Flat(b.Player.Velocity);
		RegressionBench.ReleaseAll();
		await b.Seconds(0.3);
		return v;
	}

	/// <summary>Um caixote comprido de altura <paramref name="h"/> à frente:
	/// o corpo, andando (ou pulando colado nele), termina em cima?</summary>
	private static async Task<bool> Climbs(RegressionBench b, float h, bool jump)
	{
		await b.Place(At, -90f, settle: 0.4);
		float floorY = b.Player.GlobalPosition.Y + b.Player.FeetOffset;
		var block = new StaticBody3D();
		block.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(14f, h, 4f) } });
		b.Scene.AddChild(block);
		// Pulando: parado a 0,5 m da quina (o corpo tem 0,3 de raio), pula e
		// segura W — o mais alto que se sobe. Andando: vem de 1,5 m.
		float gap = jump ? 0.8f : 1.5f;
		block.GlobalPosition = new Vector3(At.X + gap + 7f, floorY + h / 2f, At.Z);
		await b.PhysicsFrame();

		if (jump)
		{
			RegressionBench.Press("jump");
			await b.Seconds(0.05);
		}
		RegressionBench.Press("move_forward");
		await b.Seconds(1.4);
		RegressionBench.ReleaseAll();
		bool on = b.Player.GlobalPosition.Y + b.Player.FeetOffset > floorY + h - 0.05f;
		block.QueueFree();
		await b.PhysicsFrame();
		return on;
	}
}
