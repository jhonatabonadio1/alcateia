namespace Alcateia.UnitTests;

/// <summary>O fôlego da corrida do humano (scripts/domain/SprintBreath.cs, 27/11).</summary>
public static class SprintBreathTests
{
	private const float Dt = 1f / 60f;

	[Test("fôlego: corre 4 s seguidos, fica 1,5 s sem fôlego e depois corre de novo")]
	public static void RunsOutAndComesBack()
	{
		var b = new SprintBreath(4f, 3f, 1.5f);
		int ran = 0;
		while (b.CanSprint && ran < 1000) { b.Step(Dt, true); ran++; }
		Check.Near(4.0, ran * Dt, 0.05, "corre 4 s");
		Check.True(b.Winded, "e fica sem fôlego");

		int waited = 0;
		while (!b.CanSprint && waited < 1000) { b.Step(Dt, false); waited++; }
		Check.Near(1.5, waited * Dt, 0.05, "espera 1,5 s");
		Check.Near(2.0, b.Left, 0.05, "e volta com 2 s de corrida (encheu enquanto esperava)");
	}

	[Test("fôlego: parar antes de acabar não tem espera, e o tanque enche inteiro em 3 s")]
	public static void StopEarlyRefills()
	{
		var b = new SprintBreath(4f, 3f, 1.5f);
		for (int i = 0; i < 180; i++) b.Step(Dt, true);     // 3 s correndo
		Check.True(b.CanSprint && !b.Winded, "ainda tem fôlego");
		for (int i = 0; i < 45; i++) b.Step(Dt, false);     // 0,75 s parado
		Check.Near(2.0, b.Left, 0.05, "0,75 s parado devolve 1 s de corrida");
		for (int i = 0; i < 180; i++) b.Step(Dt, false);
		Check.Near(4.0, b.Left, 1e-3, "enche até o teto, não passa");
	}

	[Test("fôlego: quem segura o Shift o tempo todo corre pouco mais da metade do tempo (2 s a cada 3,5 s)")]
	public static void HoldingShiftForever()
	{
		var b = new SprintBreath(4f, 3f, 1.5f);
		int ran = 0, steps = 60 * 60;
		for (int i = 0; i < steps; i++)
		{
			bool run = b.CanSprint;
			b.Step(Dt, run);
			if (run) ran++;
		}
		double share = ran / (double)steps;
		Check.True(share > 0.55 && share < 0.65, $"corre {share:P0} do tempo");
	}
}
