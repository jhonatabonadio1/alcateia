using Godot;
using System.Threading.Tasks;

/// <summary>
/// A coronhada (V): com a arma de fogo na mão, bate rápido no que está
/// colado, sem gastar munição; a arma fica ocupada um instante; de longe
/// não pega; de lobo não existe. Com a faca o V golpeia e com a granada
/// lança.
/// </summary>
public sealed class ButtTest : RegressionTest
{
	public override string Name => "Coronhada";
	public override string Description => "V dá a coronhada: fere de perto em menos de 0,2 s, não gasta munição, ocupa a arma e anima na mão; com a faca golpeia, com a granada lança";

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

		// Com a pistola é a mesma regra (o clipe é o de uma mão, coronhada_pistola.py).
		await b.Seconds(0.4);
		w.SelectSlot(Slot.Secondary);
		await b.Seconds(0.8);
		await b.Tap("melee");
		await b.Frames(2);
		b.Check(w.IsButting && view.Butting, "com a pistola, V dá a coronhada");
		await b.Seconds(0.6);

		// Com a faca, V é o golpe do clique; com a granada, o arremesso (Jhon, 26/11:
		// "da faca deve dar uma facada normal, golpe normal. granada deve lançar a
		// granada"). Antes o V não fazia nada com as duas.
		enemy.GlobalPosition = b.Player.GlobalPosition - b.Player.GlobalBasis.Z * 1.3f;
		w.SelectSlot(Slot.Knife);
		await b.Seconds(0.8);
		before = hp.Hp + hp.Shield;
		await b.Tap("melee");
		await b.Seconds(0.5);
		b.Check(!w.IsButting, "com a faca não é coronhada");
		b.AtLeast(1, before - (hp.Hp + hp.Shield), "com a faca, V golpeia e fere");

		enemy.GlobalPosition = b.Player.GlobalPosition - b.Player.GlobalBasis.Z * 12f;
		w.SelectSlot(Slot.Grenade);
		await b.Seconds(0.8);
		int grenades = w.Grenades;
		await b.Tap("melee");
		await b.Seconds(0.8);
		b.Check(w.Grenades == grenades - 1, $"com a granada, V lança (tinha {grenades}, ficou {w.Grenades})");

		// De lobo não existe.
		await b.Seconds(0.4);
		await b.BecomeWolf();
		b.Check(!w.TryButt(), "de lobo não há coronhada");
		b.Check(!w.TryMelee(), "nem o V de humano");
	}
}
