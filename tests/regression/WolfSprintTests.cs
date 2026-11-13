using Godot;
using System.Threading.Tasks;

/// <summary>
/// A corrida de quatro (v0.27.1): W duas vezes, de lobo, e ele corre de
/// quatro 30% mais rápido; soltou o W, volta a correr em pé. Um toque só,
/// o humano ou agachado não correm de quatro.
/// </summary>
public sealed class WolfSprintTest : RegressionTest
{
	public override string Name => "CorridaDeQuatro";
	public override string Description => "W duas vezes: o lobo corre de quatro 30% mais rápido, e solta ao soltar o W";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		Player p = b.Player;

		// Humano: dois toques não fazem nada.
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
		await DoubleTap(b);
		b.Check(!p.IsSprinting, "o humano não corre de quatro");
		RegressionBench.ReleaseAll();

		await b.BecomeWolf();
		float run = p.HumanSpeed * b.Form.Breed.SpeedMultiplier;

		// Um toque: corrida normal.
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
		RegressionBench.Press("move_forward");
		await b.Seconds(0.6);
		b.Check(!p.IsSprinting, "um toque só: corre em pé");
		b.Near(run, RegressionBench.Flat(await b.Displacement(p, 60)), 0.03, "corrida em pé (m/s)");
		RegressionBench.ReleaseAll();

		// Dois toques: de quatro, 30% mais rápido, no clipe de quatro patas.
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
		await DoubleTap(b);
		b.Check(p.IsSprinting, "dois toques: corre de quatro");
		await b.Seconds(0.6);
		b.Near(run * p.WolfSprintFactor, RegressionBench.Flat(await b.Displacement(p, 60)), 0.04, "corrida de quatro (m/s)");
		var tree = p.GetNode("WolfVisual").FindChildren("*", nameof(AnimationTree), true, false)[0] as AnimationTree;
		b.Near(1, (float)tree.Get("parameters/Machine/Locomotion/GaitCrouch/blend_amount"), 0.01, "toca o clipe de quatro patas");

		// A escala da animação: metade deixa a animação na metade do ritmo,
		// e o corpo anda igual.
		var visual = p.GetNode<WolfVisual>("WolfVisual");
		float normal = (float)tree.Get("parameters/Machine/Locomotion/CrawlSpeed/scale");
		visual.SprintAnimScale = 0.5f;
		await b.PhysicsFrame();
		await b.PhysicsFrame();
		b.Near(normal * 0.5, (float)tree.Get("parameters/Machine/Locomotion/CrawlSpeed/scale"), 0.03, "SprintAnimScale 0,5: animação na metade do ritmo");
		b.Near(run * p.WolfSprintFactor, RegressionBench.Flat(await b.Displacement(p, 60)), 0.04, "e o corpo corre igual (m/s)");
		visual.SprintAnimScale = 1f;

		// Agachar corta; soltar o W também.
		RegressionBench.Press("crouch");
		await b.PhysicsFrame();
		await b.PhysicsFrame();
		b.Check(!p.IsSprinting, "agachar corta a corrida de quatro");
		RegressionBench.Release("crouch", "move_forward");
		await b.PhysicsFrame();

		await DoubleTap(b);
		b.Check(p.IsSprinting, "dois toques de novo");
		RegressionBench.Release("move_forward");
		await b.PhysicsFrame();
		await b.PhysicsFrame();
		b.Check(!p.IsSprinting, "soltou o W: para de correr de quatro");

		// Dois toques devagar demais não contam.
		await b.Tap("move_forward");
		await b.Seconds(p.DoubleTapWindow + 0.15);
		RegressionBench.Press("move_forward");
		await b.PhysicsFrame();
		await b.PhysicsFrame();
		b.Check(!p.IsSprinting, "toques espaçados: corre em pé");
		RegressionBench.ReleaseAll();
	}

	/// <summary>Toca o W, solta, e aperta de novo (segurando).</summary>
	private static async Task DoubleTap(RegressionBench b)
	{
		await b.Tap("move_forward");
		await b.Seconds(0.08);
		RegressionBench.Press("move_forward");
		await b.PhysicsFrame();
		await b.PhysicsFrame();
	}
}
