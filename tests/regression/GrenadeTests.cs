using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Granada de segurar e soltar (v0.26.9) e mais forte (v0.26.10):
/// nada sai enquanto segura; soltou, ela vai a uns 15,5 m olhando reto.</summary>
public sealed class GrenadeRangeTest : RegressionTest
{
	public override string Name => "Granada";
	public override string Description => "a granada arma enquanto segura, sai ao soltar e cai a ~15,5 m";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Place(new Vector3(-15, 1, 13), -90f);
		b.Weapon.Equip(b.Weapon.PrimaryDef, b.Weapon.SecondaryDef, 2);
		await b.Tap("slot_5");
		// Da arma principal para a granada: o tempo de guardá-la (o AR-15
		// guarda no tempo da animação do Jhon) e os 0,2 s da troca.
		await b.Seconds(0.6 + b.Weapon.PrimaryDef.HolsterTime);

		RegressionBench.Press("attack");
		await b.Seconds(1.2);
		b.Check(b.Weapon.IsGrenadeReady, "segurando, a granada fica armada");
		b.Check(FindGrenade(b) == null, "nada sai enquanto segura");

		Vector3 from = b.Player.GlobalPosition;
		RegressionBench.Release("attack");

		Grenade grenade = null;
		await b.Until(() => (grenade = FindGrenade(b)) != null, 0.5);
		b.Check(grenade != null, "soltou, a granada saiu");
		if (grenade == null) return;

		float landed = float.NaN;
		await b.Until(() =>
		{
			if (!GodotObject.IsInstanceValid(grenade)) return true;
			if (grenade.GlobalPosition.Y > 0.15f) return false;
			landed = RegressionBench.Flat(grenade.GlobalPosition - from);
			return true;
		}, 4.0);

		b.Near(15.5, landed, 1.0, "onde a granada toca o chão (m)");
		b.Check(b.Weapon.Grenades == 1, $"sobrou uma granada (sobraram {b.Weapon.Grenades})");
	}

	private static Grenade FindGrenade(RegressionBench b) =>
		b.GetTree().Root.FindChildren("*", "RigidBody3D", true, false).OfType<Grenade>().FirstOrDefault();
}

/// <summary>
/// A granada cai onde a mira aponta: mirando o chão a 6 m e a 13 m, ela
/// explode a menos de 1,2 m do ponto; e a explosão deixa o efeito novo
/// (<see cref="Explosion"/>), com a granada de verdade no ar.
/// </summary>
public sealed class GrenadeAimTest : RegressionTest
{
	public override string Name => "GranadaNaMira";
	public override string Description => "a granada explode perto do ponto mirado (6 m e 13 m), voa com o modelo da granada e explode com o efeito novo";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		foreach (float pitch in new[] { -15f, -7f })
		{
			await b.Place(new Vector3(-15, 1, 13), -90f, pitch, settle: 0.5);
			b.Weapon.Equip(b.Weapon.PrimaryDef, b.Weapon.SecondaryDef, 2);
			b.Health.SetHp(b.Health.MaxHp);
			await b.Tap("slot_5");
			await b.Seconds(0.6 + b.Weapon.PrimaryDef.HolsterTime);

			Camera3D cam = b.GetViewport().GetCamera3D();
			var query = PhysicsRayQueryParameters3D.Create(cam.GlobalPosition, cam.GlobalPosition - cam.GlobalBasis.Z * 32f);
			query.Exclude = new Godot.Collections.Array<Rid> { b.Player.GetRid() };
			var hit = b.Player.GetWorld3D().DirectSpaceState.IntersectRay(query);
			b.Check(hit.Count > 0, $"mira a {pitch}°: há um ponto no chão");
			if (hit.Count == 0) continue;
			Vector3 aim = hit["position"].AsVector3();
			float far = RegressionBench.Flat(aim - b.Player.GlobalPosition);

			RegressionBench.Press("attack");
			await b.Seconds(0.3);
			RegressionBench.Release("attack");
			Grenade grenade = null;
			await b.Until(() => (grenade = b.GetTree().Root.FindChildren("*", "RigidBody3D", true, false).OfType<Grenade>().FirstOrDefault()) != null, 0.5);
			b.Check(grenade != null, "a granada saiu");
			if (grenade == null) continue;
			b.Check(grenade.GetNodeOrNull("Modelo") != null, "voa o modelo da granada, não a bola");

			Vector3 last = grenade.GlobalPosition;
			await b.Until(() =>
			{
				if (!GodotObject.IsInstanceValid(grenade)) return true;
				last = grenade.GlobalPosition;
				return false;
			}, 4.0);
			RegressionBench.Note($"mirou a {RegressionBench.F(far)} m; explodiu a {RegressionBench.F(RegressionBench.Flat(last - aim))} m do ponto");
			b.AtMost(1.2, RegressionBench.Flat(last - aim), $"a {RegressionBench.F(far)} m: distância entre a explosão e o ponto mirado (m)");
			b.Check(Explosion.Alive > 0, "a explosão nova está no ar");
			await b.Seconds(2.3);
		}
		b.Check(Explosion.Alive == 0, "as explosões se desfazem sozinhas");
	}
}
