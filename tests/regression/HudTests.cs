using Godot;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// O HUD reage aos sinais: a vida cai no número, o tiro tira do pente, e
/// virar lobo troca o painel de munição pelo das recargas. Lê os campos
/// privados do <see cref="Hud"/> por reflexão — o teste olha o que a tela
/// mostra, não como ela é montada.
/// </summary>
public sealed class HudTest : RegressionTest
{
	public override string Name => "Hud";
	public override string Description => "vida, pente e painel de forma acompanham o jogador";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		var huds = b.GetTree().Root.FindChildren("*", nameof(CanvasLayer), true, false).OfType<Hud>().ToList();
		b.Check(huds.Count == 1, $"um HUD na cena ({huds.Count})");
		if (huds.Count != 1) return;
		Hud h = huds[0];

		Label hp = Field<Label>(h, "_hpNumber");
		Label mag = Field<Label>(h, "_mag");
		Control human = Field<Control>(h, "_human");
		Control wolf = Field<Control>(h, "_wolf");

		await b.Seconds(3.5); // proteção de quem acabou de nascer
		b.Check(hp.Text == "200", $"vida cheia no número: {hp.Text}");
		b.Health.TakeDamage(60f);
		await b.PhysicsFrame();
		b.Check(hp.Text == "140", $"vida depois de 60 de dano: {hp.Text}");

		int before = int.Parse(mag.Text);
		await b.Tap("attack");
		await b.Seconds(0.2);
		int after = int.Parse(mag.Text);
		b.Check(after == before - 1, $"um tiro tira um do pente: {before} → {after}");

		b.Check(human.Visible && !wolf.Visible, "humano: painel de munição");
		await b.BecomeWolf();
		b.Check(wolf.Visible && !human.Visible, "lobo: painel das recargas");
	}

	private static T Field<T>(Hud hud, string name) where T : class =>
		typeof(Hud).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(hud) as T;
}
