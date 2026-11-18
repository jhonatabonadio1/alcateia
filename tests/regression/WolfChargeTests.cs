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
		// O rugido novo (18/11) abre os braços baixos e largos, em vez de os
		// erguer para o céu: a mão sobe menos em relação ao peito.
		b.AtLeast(0.10, full.Y - idle.Y, "o rugido toca inteiro: a mão sobe (m)");
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
		// a força cheia, para onde a mira aponta. Sem segurar, sem carga
		// andando e sem anel no chão — o que este teste media antes.
		Weapon w = b.Weapon;
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.8);

		// Sem a carga do especial cheia, o Q não faz nada.
		w.SpecialFree = false;
		w.AddSpecial(0.5f);
		await b.Tap("pounce");
		await b.Tap("attack"); // Q arma, o clique salta (18/11)
		await b.Seconds(0.3);
		b.Check(!w.IsPounceFlying && !w.IsChargingPounce, "com a carga pela metade, o Q não salta");

		w.FillSpecial();
		Vector3 start = b.Player.GlobalPosition;
		await b.Tap("pounce");
		await b.Tap("attack"); // Q arma, o clique salta (18/11)
		b.Check(await b.Until(() => w.IsPounceFlying, 1.0), "apertou o Q no chão: salta");
		b.Check(!w.IsChargingPounce, "não fica carregando");
		await b.Until(() => !w.IsPounceFlying, 3.0);
		await b.Seconds(0.2);
		Vector3 flown = b.Player.GlobalPosition - start;
		RegressionBench.Note($"especial: caiu a {RegressionBench.F(RegressionBench.Flat(flown))} m, {RegressionBench.F(Mathf.Abs(flown.Z))} m para o lado (alcance máximo {RegressionBench.F(w.PounceMaxDistance)} m)");
		b.AtLeast(w.PounceMaxDistance * 0.6, flown.X, "o salto vai longe, para onde a mira aponta (m)");
		b.AtMost(w.PounceMaxDistance + 2.5, RegressionBench.Flat(flown), "e não passa do alcance (m)");
		b.AtMost(1.0, Mathf.Abs(flown.Z), "em linha reta (m para o lado)");
		b.Check(!w.PounceReady, "o especial gasta a carga");
		b.AtMost(0.01, w.SpecialCharge, "a carga zera");

		// ---- no ar (pedido do Jhon, 18/11): pulou e apertou Q, sai dali.
		w.FillSpecial();
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.8);
		Vector3 ground = b.Player.GlobalPosition;
		await b.Tap("jump");
		await b.Until(() => !b.Player.IsOnFloor() && b.Player.GlobalPosition.Y > ground.Y + 0.4f, 1.0);
		float jumpedTo = b.Player.GlobalPosition.Y - ground.Y;
		await b.Tap("pounce");
		await b.Tap("attack"); // Q arma, o clique salta (18/11)
		b.Check(await b.Until(() => w.IsPounceFlying, 0.5), $"no ar ({RegressionBench.F(jumpedTo)} m acima do chão), o Q salta");
		await b.Until(() => !w.IsPounceFlying, 3.0);
		await b.Seconds(0.4);
		Vector3 air = b.Player.GlobalPosition - ground;
		RegressionBench.Note($"especial no ar: caiu a {RegressionBench.F(RegressionBench.Flat(air))} m, {RegressionBench.F(air.Y)} m de altura");
		b.AtLeast(w.PounceMaxDistance * 0.6, air.X, "do ar, também vai longe para onde a mira aponta (m)");
		b.Check(b.Player.IsOnFloor() && Mathf.Abs(air.Y) < 0.3f, "e cai no chão");

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

/// <summary>O especial é carga em % (decisão do Jhon, 18/11): abate enche
/// um bom pedaço, assistência menos, e sozinha enche bem devagar.</summary>
public sealed class SpecialChargeTest : RegressionTest
{
	public override string Name => "CargaDoEspecial";
	public override string Description => "abate e assistência enchem a carga do especial, e o tempo enche devagar; morrer não tira";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		Weapon w = b.Weapon;
		var form = b.Form;
		var health = b.Player.GetNode<Health>("Health");

		// O tempo: em 5 s, 5 / SpecialFillTime.
		float before = w.SpecialCharge;
		await b.Seconds(5);
		float passive = w.SpecialCharge - before;
		RegressionBench.Note($"em 5 s, sozinha: +{RegressionBench.F(passive * 100)}% (cheia em {RegressionBench.F(w.SpecialFillTime)} s)");
		b.Near(5.0 / w.SpecialFillTime, passive, 0.004, "enche sozinha, devagar");

		// Abate e assistência, pelo mesmo caminho do WP.
		before = w.SpecialCharge;
		form.OnKill();
		b.Near(w.SpecialPerKill, w.SpecialCharge - before, 0.002, "um abate enche");
		before = w.SpecialCharge;
		form.OnAssist();
		b.Near(w.SpecialPerAssist, w.SpecialCharge - before, 0.002, "uma assistência enche menos");
		b.AtLeast(w.SpecialPerAssist * 2f, w.SpecialPerKill, "abate vale bem mais que assistência");

		// Não passa de 100%.
		for (int i = 0; i < 6; i++) form.OnKill();
		b.Near(1.0, w.SpecialCharge, 1e-6, "para em 100%");
		b.Check(w.PounceReady, "cheia, o especial está pronto");

		// Morrer não tira a carga.
		health.TakeDamage(99999, 0, b.Player.GlobalPosition, false);
		await b.Until(() => !health.IsDead, 12.0);
		await b.Seconds(0.5);
		b.Near(1.0, w.SpecialCharge, 1e-6, "renasce com a carga que tinha");
	}
}

