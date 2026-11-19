using Godot;
using System.Threading.Tasks;

/// <summary>O especial armado (pedido do Jhon, 18/11): Q arma e o lobo
/// segue andando com o modo alvo aberto; armado, leva menos dano; Q de novo
/// desarma sem gastar; o clique salta no alvo travado e o voo o persegue.</summary>
public sealed class SpecialArmedTest : RegressionTest
{
	public override string Name => "EspecialArmado";
	public override string Description => "Q arma andando, escudo extra, trava no alvo, clique salta e persegue o alvo que se mexe";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(keep: 1);
		var enemy = b.Bots()[0];
		RegressionBench.Freeze(enemy);
		enemy.GlobalPosition = new Vector3(0, 1, 25);
		await b.BecomeWolf();
		Weapon w = b.Weapon;
		Health hp = b.Player.GetNode<Health>("Health");
		w.SpecialFree = false;
		w.FillSpecial();
		await b.Place(new Vector3(4, 1, -3.5f), 90f);

		// ---- Q arma: não salta, a carga fica, o lobo anda.
		await b.Tap("pounce");
		b.Check(w.SpecialArmed && !w.IsPounceFlying && !w.IsChargingPounce, "Q arma o especial sem saltar");
		b.Check(b.Player.TargetMode, "armado, o modo alvo abre");
		Vector3 before = b.Player.GlobalPosition;
		Input.ActionPress("move_forward");
		await b.Seconds(0.8);
		Input.ActionRelease("move_forward");
		float walked = RegressionBench.Flat(b.Player.GlobalPosition - before);
		b.AtLeast(2.0, walked, "armado, o lobo anda pelo mapa (m)");
		b.Check(w.SpecialArmed && w.PounceReady, "andando, segue armado e com a carga");

		// ---- escudo extra: armado leva menos dano.
		float full = hp.Hp + hp.Shield;
		hp.TakeDamage(20f);
		float armedLoss = full - (hp.Hp + hp.Shield);
		b.Check(w.SpecialGuarded, "armado, a guarda está ligada");

		// ---- Q de novo não desarma: armou, é saltar ou perder.
		await b.Tap("pounce");
		b.Check(w.SpecialArmed && w.PounceReady, "Q de novo não desarma");
		b.Check(w.SpecialArmLeft > 0f && w.SpecialArmLeft < w.SpecialArmTime, $"o prazo de usar corre ({RegressionBench.F(w.SpecialArmLeft)} s)");

		// ---- passou o prazo sem saltar: perde a carga.
		await b.Seconds(w.SpecialArmLeft + 0.15);
		b.Check(!w.SpecialArmed && !b.Player.TargetMode, "passou o prazo: desarma");
		b.AtMost(0.01, w.SpecialCharge, "passou o prazo: a carga foi embora");
		await b.PhysicsFrame();
		full = hp.Hp + hp.Shield;
		hp.TakeDamage(20f);
		float normalLoss = full - (hp.Hp + hp.Shield);
		RegressionBench.Note($"dano de 20: armado perde {RegressionBench.F(armedLoss)}, desarmado {RegressionBench.F(normalLoss)}");
		b.Near(normalLoss * w.SpecialGuard, armedLoss, 0.6, "armado, leva a fração do dano do escudo");

		// ---- trava no inimigo à frente e o persegue.
		await b.Place(new Vector3(4, 1, -3.5f), 90f);
		enemy.GlobalPosition = new Vector3(-4, 1, -3.5f);
		await b.Seconds(0.3);
		w.FillSpecial();
		await b.Tap("pounce");
		await b.Frames(3);
		b.Check(b.Player.LockTarget == enemy, "armado, trava no inimigo perto da mira");

		Health enemyHp = enemy.GetNode<Health>("Health");
		float enemyBefore = enemyHp.Hp + enemyHp.Shield;
		await b.Tap("attack");
		b.Check(await b.Until(() => w.IsPounceFlying, 1.0), "o clique salta");
		b.Check(!w.SpecialArmed && w.SpecialGuarded, "saltando: desarmou e a guarda segue");

		// O alvo anda de lado e para trás durante o voo.
		Vector3 enemyStart = enemy.GlobalPosition;
		double t = 0;
		while (w.IsPounceFlying && t < 2.0)
		{
			await b.PhysicsFrame();
			t += 1.0 / Engine.PhysicsTicksPerSecond;
			enemy.GlobalPosition = enemyStart + new Vector3(-2.5f, 0, 3.0f) * Mathf.Min((float)t / 0.5f, 1f);
		}
		await b.Seconds(0.2);
		float miss = RegressionBench.Flat(b.Player.GlobalPosition - enemy.GlobalPosition);
		RegressionBench.Note($"especial perseguindo: alvo andou 3,9 m no voo; o lobo caiu a {RegressionBench.F(miss)} m dele");
		b.AtMost(1.6, miss, "caiu em cima do alvo que se mexeu (m)");
		b.Check(w.SpecialGuarded, "caído, ainda no golpe: a guarda (e os raios) seguem");
		await b.Until(() => !w.IsRooted, 2.0);
		float dealt = enemyBefore - (enemyHp.Hp + enemyHp.Shield);
		RegressionBench.Note($"especial: o alvo travado perdeu {RegressionBench.F(dealt)} (vida {RegressionBench.F(enemyHp.Hp)}, morto {enemyHp.IsDead})");
		b.Check(dealt > 50f || enemyHp.IsDead, "o especial dá dano no alvo travado");
		await b.Frames(3);
		b.Check(!w.SpecialGuarded, "acabou o especial: a guarda desliga");
		b.Check(!w.PounceReady, "o salto gasta a carga");
	}
}
