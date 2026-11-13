using Godot;
using System.Threading.Tasks;

/// <summary>Morrer de lobo e renascer: volta humano, com vida cheia, e
/// perde 100 de WP (decisões do Jhon, v0.26.8 e v0.26.10).</summary>
public sealed class RespawnTest : RegressionTest
{
	public override string Name => "RenasceHumano";
	public override string Description => "quem morre de lobo renasce humano, de vida cheia, e perde 100 de WP";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();

		float wp = b.Form.Wp;
		b.Health.TakeDamage(99999f);
		await b.Seconds(0.3);

		b.Check(b.Health.IsDead, "morreu");
		b.Near(wp - 100f, b.Form.Wp, 0.01, "WP depois de morrer");

		await b.Seconds(b.Health.RespawnDelay + 0.2);
		b.Check(b.Health.TryRespawn(), "renasceu");
		await b.Seconds(0.3);

		b.Check(b.Form.Current == Form.Human, $"forma ao renascer: {b.Form.Current}");
		b.Check(!b.Player.GetNode<Node3D>("WolfVisual").Visible, "o lobo some");
		b.Near(b.Health.MaxHp, b.Health.Hp, 0.01, "vida cheia");
		b.Near(0, b.Health.MaxShield, 0.01, "sem escudo de lobo");
		b.Check(!b.Form.IsThirdPerson, "câmera em primeira pessoa");
	}
}

/// <summary>A garra do Bruto contra humano: o dano da classe × 1,6.</summary>
public sealed class ClawDamageTest : RegressionTest
{
	public override string Name => "DanoDaGarra";
	public override string Description => "a garra do Bruto tira 75 × 1,6 = 120 de um humano";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(keep: 1);
		var target = b.Bots()[0];
		RegressionBench.Freeze(target);
		var health = target.GetNode<Health>("Health");

		await b.BecomeWolf();
		await b.Place(new Vector3(0, 1, 2), 0f);
		target.GlobalPosition = new Vector3(0, 1, 0.6f);
		await b.Seconds(3.5); // proteção de quem acabou de nascer

		float before = health.Hp;
		await b.Tap("attack");
		await b.Seconds(0.6);
		b.Near(120f, before - health.Hp, 0.5, "dano da primeira garrada");
	}
}
