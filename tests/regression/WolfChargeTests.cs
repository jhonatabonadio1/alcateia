using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O rugido e o especial: apertou R andando, o lobo para e ruge de corpo
/// inteiro; apertou Q, salta na hora com a carga cheia (18/11 — antes
/// segurava o botão direito para carregar, andando encolhido, com um anel
/// no chão marcando a queda).
/// </summary>
public sealed class WolfChargeTest : RegressionTest
{
	public override string Name => "CargaDoSalto";
	public override string Description => "rugindo o lobo para; o Q salta na hora, com a carga cheia, para onde a mira aponta";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.8);

		var visual = b.Player.GetNode<WolfVisual>("WolfVisual");
		var skeleton = visual.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().First();
		int hips = Bone(skeleton, "Hips"), hand = Bone(skeleton, "LeftHand"), chest = Bone(skeleton, "Spine2");
		Vector3 Global(int bone) => skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone).Origin;
		// O quadril acima dos pés: em pé ~1 m, encolhido na carga ~0,63 m.
		float HipsUp() => Global(hips).Y - (b.Player.GlobalPosition.Y + b.Player.FeetOffset);
		await b.Seconds(0.4);
		float standing = HipsUp();

		// ---- andando e apertando R: o lobo para e ruge de corpo inteiro.
		Vector3 idle = Global(hand) - Global(chest);
		RegressionBench.Press("move_forward");
		await b.Seconds(0.6);
		b.AtLeast(3.0, RegressionBench.Flat(b.Player.Velocity), "andando antes do rugido (m/s)");
		Input.ActionPress("howl");
		await b.Seconds(0.75);
		Vector3 full = Global(hand) - Global(chest);
		float howlSpeed = RegressionBench.Flat(b.Player.Velocity);
		Vector3 before = b.Player.GlobalPosition;
		await b.Seconds(0.4);
		RegressionBench.Note($"rugido andando: a mão sobe {RegressionBench.F(full.Y - idle.Y)} m, corpo a {RegressionBench.F(howlSpeed)} m/s, andou {RegressionBench.F(RegressionBench.Flat(b.Player.GlobalPosition - before))} m em 0,4 s");
		b.AtLeast(0.25, full.Y - idle.Y, "o rugido toca inteiro: a mão sobe (m)");
		b.AtMost(0.05, howlSpeed, "rugindo, o lobo para (m/s)");
		Input.ActionRelease("howl");
		RegressionBench.ReleaseAll();
		await b.Seconds(0.8);

		// ---- andando, pula e aperta R no ar: ruge no ar, e o corpo segue
		// o arco do pulo para a frente até cair (pedido do Jhon, 16/11).
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
		RegressionBench.Press("move_forward");
		await b.Seconds(0.5);
		await b.Tap("jump");
		await b.Until(() => !b.Player.IsOnFloor(), 0.5);
		await b.Seconds(0.05);
		Input.ActionPress("howl");
		await b.Seconds(0.1);
		bool inAir = !b.Player.IsOnFloor();
		float airSpeed = RegressionBench.Flat(b.Player.Velocity);
		RegressionBench.Note($"rugido no ar: progresso {RegressionBench.F(b.Player.HowlProgress)}, corpo a {RegressionBench.F(airSpeed)} m/s");
		b.Check(inAir, "ainda no ar quando apertou R");
		b.Check(b.Player.HowlProgress > 0f, "no ar, o rugido começa");
		b.AtLeast(3.0, airSpeed, "no ar, o corpo segue para a frente (m/s)");
		await b.Until(() => b.Player.IsOnFloor(), 2.0);
		await b.Seconds(0.3);
		b.AtMost(0.05, RegressionBench.Flat(b.Player.Velocity), "pousou rugindo: para (m/s)");
		Input.ActionRelease("howl");
		RegressionBench.ReleaseAll();
		await b.Seconds(0.8);

		// ---- o especial (Q, 18/11): um toque e o lobo salta na hora, com
		// a carga cheia, para onde a mira aponta. Sem segurar, sem carga
		// andando e sem anel no chão — o que este teste media antes.
		Weapon w = b.Weapon;
		await b.Until(() => w.PounceReady, w.PounceRecharge + 2.0);
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.8);
		Vector3 start = b.Player.GlobalPosition;
		await b.Tap("pounce");
		b.Check(await b.Until(() => w.IsPounceFlying, 1.0), "apertou o Q no chão: salta");
		b.Check(!w.IsChargingPounce, "não fica carregando");
		await b.Until(() => !w.IsPounceFlying, 3.0);
		await b.Seconds(0.2);
		Vector3 flown = b.Player.GlobalPosition - start;
		RegressionBench.Note($"especial: caiu a {RegressionBench.F(RegressionBench.Flat(flown))} m, {RegressionBench.F(Mathf.Abs(flown.Z))} m para o lado (alcance máximo {RegressionBench.F(w.PounceMaxDistance)} m)");
		b.AtLeast(w.PounceMaxDistance * 0.6, flown.X, "o salto vai longe, para onde a mira aponta (m)");
		b.AtMost(w.PounceMaxDistance + 2.5, RegressionBench.Flat(flown), "e não passa do alcance (m)");
		b.AtMost(1.0, Mathf.Abs(flown.Z), "em linha reta (m para o lado)");
		b.Check(!w.PounceReady, "o especial entra em recarga");

		// O botão direito no chão não carrega mais nada.
		await b.Seconds(0.5);
		RegressionBench.Press("special");
		await b.Seconds(0.4);
		b.Check(!w.IsChargingPounce && !w.IsPounceFlying, "o botão direito, no chão, não salta");
		RegressionBench.ReleaseAll();
		await b.Seconds(0.3);
	}

	private static int Bone(Skeleton3D skeleton, string end) =>
		Enumerable.Range(0, skeleton.GetBoneCount()).First(i => skeleton.GetBoneName(i).EndsWith(end));
}
