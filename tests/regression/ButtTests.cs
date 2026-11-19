using Godot;
using System.Threading.Tasks;

/// <summary>
/// A coronhada (V): com a arma de fogo na mão, bate rápido no que está
/// colado, sem gastar munição; a arma fica ocupada um instante; de longe
/// não pega; de lobo e com a faca não existe.
/// </summary>
public sealed class ButtTest : RegressionTest
{
	public override string Name => "Coronhada";
	public override string Description => "V dá a coronhada: fere de perto em menos de 0,2 s, não gasta munição, ocupa a arma e anima na mão";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(keep: 1);
		var enemy = b.Bots()[0];
		RegressionBench.Freeze(enemy);
		await b.Place(new Vector3(4, 1, -3.5f), 90f, settle: 0.5);
		Weapon w = b.Weapon;
		var view = b.Player.GetNode<ViewModel>("Head/Camera3D/ViewModel");
		Health hp = enemy.GetNode<Health>("Health");

		// De perto.
		enemy.GlobalPosition = b.Player.GlobalPosition - b.Player.GlobalBasis.Z * 1.3f;
		await b.Seconds(0.2);
		float before = hp.Hp + hp.Shield;
		int ammo = w.AmmoInMag;
		await b.Tap("melee");
		await b.Frames(2);
		b.Check(w.IsButting && view.Butting, "V começa a coronhada, na arma e na mão");
		b.Check(!w.CanAim, "no meio da coronhada não mira");
		await b.Seconds(0.2);
		b.Near(w.ButtDamage, before - (hp.Hp + hp.Shield), 0.5, "a coronhada fere de perto");
		b.Check(w.AmmoInMag == ammo, "não gasta munição");
		await b.Tap("attack");
		await b.Frames(2);
		b.Check(w.AmmoInMag == ammo, "com a arma ocupada, o tiro espera");
		await b.Seconds(w.ButtCooldown);
		b.Check(!w.IsButting && !view.Butting, "acabou: a arma volta");

		// De longe não pega.
		enemy.GlobalPosition = b.Player.GlobalPosition - b.Player.GlobalBasis.Z * 4f;
		await b.Seconds(0.2);
		before = hp.Hp + hp.Shield;
		await b.Tap("melee");
		await b.Seconds(0.3);
		b.Near(0, before - (hp.Hp + hp.Shield), 0.01, "a 4 m a coronhada não alcança");

		// De lobo não existe.
		await b.Seconds(0.4);
		await b.BecomeWolf();
		b.Check(!w.TryButt(), "de lobo não há coronhada");
	}
}
