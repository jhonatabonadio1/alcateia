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
		await b.Seconds(0.6);

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
