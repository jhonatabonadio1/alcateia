using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Quase morrendo, a tela avermelha e a respiração ofegante entra — de
/// humano **e de lobo** — e some quando a vida volta.
/// </summary>
public sealed class DangerTest : RegressionTest
{
	public override string Name => "PerigoNaTela";
	public override string Description => "abaixo de 30% de vida a tela avermelha e a respiração entra, de humano e de lobo";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		Hud hud = b.GetTree().Root.FindChildren("*", "", true, false).OfType<Hud>().First();
		Health hp = b.Health;

		foreach (bool wolf in new[] { false, true })
		{
			string who = wolf ? "lobo" : "humano";
			if (wolf) await b.BecomeWolf();
			hp.SetHp(hp.MaxHp);
			await b.Seconds(2.2);
			b.AtMost(0.02, hud.Danger, $"{who}, vida cheia: sem vermelho");

			// Sem escudo e com a vida a 15% (o lobo: 15% da soma de vida e escudo).
			for (int i = 0; i < 20 && hp.Shield > 0.5f; i++) hp.TakeDamage(hp.Shield + 5f);
			hp.SetHp((hp.MaxHp + hp.MaxShield) * 0.15f);
			await b.Seconds(0.6);
			RegressionBench.Note($"{who}: vida {RegressionBench.F(hp.Hp)} de {RegressionBench.F(hp.MaxHp)}, escudo {RegressionBench.F(hp.Shield)}, perigo {RegressionBench.F(hud.Danger)}");
			b.AtLeast(0.3, hud.Danger, $"{who} com 15% de vida (e escudo): a tela avermelha");
			b.Check(hud.Breathing, $"{who} com 15% de vida: a respiração ofegante toca");
		}
	}
}
