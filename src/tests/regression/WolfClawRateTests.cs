using Godot;
using System.Threading.Tasks;

/// <summary>Garrada rápida (15/11): clicando rápido, a próxima corta a
/// anterior logo depois do impacto, e cada clique vira um golpe — não
/// espera a animação inteira (0,733 s).</summary>
public sealed class WolfClawRateTest : RegressionTest
{
	public override string Name => "GarradaRapida";
	public override string Description => "clicando rápido, cada garrada sai logo depois do impacto da anterior, braço alternado";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);

		Weapon w = b.Weapon;
		int claws = 0, punches = 0;
		w.WolfMoveStarted += m =>
		{
			if ((WolfMove)m == WolfMove.Claw) claws++;
			else if ((WolfMove)m == WolfMove.Punch) punches++;
		};

		// Um clique a cada 0,15 s por 1,5 s.
		double end = b.Time + 1.5;
		while (b.Time < end)
		{
			w.TryAttack();
			await b.Seconds(0.15);
		}
		await b.Seconds(0.8);

		int total = claws + punches;
		RegressionBench.Note($"1,5 s clicando: {total} garradas ({claws} direita, {punches} esquerda)");
		b.AtLeast(5, total, "garradas em 1,5 s (antes saíam 2 a 3)");
		b.AtMost(1, System.Math.Abs(claws - punches), "os braços alternam");
	}
}
