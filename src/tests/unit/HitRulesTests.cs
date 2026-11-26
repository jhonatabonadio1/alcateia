using System.Numerics;

namespace Alcateia.UnitTests;

/// <summary>As contas do acerto conferido no servidor (scripts/domain/net/HitRules.cs, decisão 0016).</summary>
public static class HitRulesTests
{
	[Test("a mira: olhando para a frente (-Z), o ponto à frente está a 0°; o do lado, a 90°; para cima é arfagem positiva")]
	public static void AimDirection()
	{
		Vector3 eye = Vector3.Zero;
		Check.True(HitRules.AimErrorDegrees(eye, 0f, 0f, new Vector3(0, 0, -10)) < 0.01f, "à frente");
		Check.True(System.MathF.Abs(HitRules.AimErrorDegrees(eye, 0f, 0f, new Vector3(10, 0, 0)) - 90f) < 0.01f, "do lado");
		Check.True(HitRules.AimErrorDegrees(eye, 0f, 0.5f, new Vector3(0, System.MathF.Tan(0.5f) * 10, -10)) < 0.01f, "olhando para cima");
		// Guinada de 90° (π/2) vira para -X, como a Rotation.Y do Godot.
		Check.True(HitRules.AimErrorDegrees(eye, System.MathF.PI / 2, 0f, new Vector3(-10, 0, 0)) < 0.01f, "virado para a esquerda");
	}

	[Test("a mira cabe com o tamanho do alvo, a dispersão e a folga; muito fora, não")]
	public static void AimFits()
	{
		Check.True(HitRules.AimFits(10f, 20f, 4f), "10° a 20 m com 4° de dispersão");
		Check.True(!HitRules.AimFits(40f, 20f, 4f), "40° a 20 m");
		Check.True(HitRules.AimFits(60f, 0.3f, 0f), "colado no alvo, qualquer ângulo");
	}

	[Test("a volta no tempo: o que o jogo via, mas nunca no futuro nem antes do limite")]
	public static void Rewind()
	{
		uint fire = 1000;
		Check.Equal(990.0, HitRules.RewindTick(990, fire), "dentro do limite");
		Check.Equal((double)fire, HitRules.RewindTick(1200, fire), "no futuro, vale o tiro");
		Check.Equal((double)(fire - HitRules.MaxRewindTicks), HitRules.RewindTick(10, fire), "longe demais, o limite");
		Check.Equal(18, HitRules.MaxRewindTicks, "0,3 s são 18 passos");
	}

	[Test("os pontos do alvo: a cabeça em cima, as pernas embaixo, os ombros dos lados de quem atira")]
	public static void TargetPoints()
	{
		System.Span<Vector3> p = stackalloc Vector3[6];
		int n = HitRules.TargetPoints(new Vector3(0, 1, 0), 1.75f, new Vector3(0, 1, 10), p);
		Check.Equal(6, n, "seis pontos");
		Check.True(p[0].Y > p[1].Y && p[1].Y > p[2].Y && p[2].Y > p[3].Y, "de cima para baixo");
		Check.True(System.MathF.Abs(p[4].X - p[5].X) > 0.5f && System.MathF.Abs(p[4].Z - p[5].Z) < 0.01f, "ombros atravessados ao tiro");
	}

	[Test("a distância até o segmento do corpo (do pé ao olho)")]
	public static void Segment()
	{
		Check.True(HitRules.DistanceToSegment(new Vector3(1, 1, 0), Vector3.Zero, new Vector3(0, 2, 0)) - 1f < 0.001f, "do lado");
		Check.True(HitRules.DistanceToSegment(new Vector3(0, 5, 0), Vector3.Zero, new Vector3(0, 2, 0)) - 3f < 0.001f, "acima do olho");
	}

	[Test("o ritmo: cabe o que a arma dá por segundo, mais a folga; atirar mais rápido não cabe")]
	public static void Budget()
	{
		var b = new DamageBudget();
		b.Configure(perSecond: 300, burst: 100);
		Check.True(b.Spend(100, 0), "a folga inteira de uma vez");
		Check.True(!b.Spend(50, 0.1), "0,1 s depois só encheu 30");
		Check.True(b.Spend(30, 0.1), "mas 30 cabem");
		Check.True(b.Spend(100, 1.0), "um segundo depois, cheio de novo");
		int accepted = 0;
		for (int i = 0; i < 100; i++) if (b.Spend(30, 1.0 + i * 0.05)) accepted++;
		// O balde ficou vazio em 1,0 s; em 5 s a 300 por segundo enchem
		// 1.485: 49 acertos de 30. Atirando no dobro do ritmo, metade passa.
		Check.True(accepted >= 47 && accepted <= 51, $"no ritmo da arma, metade passa ({accepted} de 100)");
	}

	[Test("o compasso do lobo no servidor: no mesmo alvo, um golpe por garrada (20 comandos); bote, um a cada 58; o mesmo golpe pega vários alvos")]
	public static void WolfPace()
	{
		var pace = new WolfStrikePace();
		Check.True(pace.Allow(7, 100, false, 20, 58), "a primeira garrada vale");
		Check.True(!pace.Allow(7, 110, false, 20, 58), "no mesmo alvo, 10 comandos depois (0,17 s), não");
		Check.True(pace.Allow(8, 110, false, 20, 58), "em outro alvo, vale");
		Check.True(pace.Allow(7, 121, false, 20, 58), "21 comandos depois, vale");

		// Clique em rajada: um golpe a cada 6 comandos (0,1 s) num alvo, por 4 s.
		var spam = new WolfStrikePace();
		int ok = 0;
		for (uint seq = 1; seq <= 240; seq += 6) if (spam.Allow(1, seq, false, 20, 58)) ok++;
		Check.True(ok <= 12, $"rajada de 40 golpes em 4 s: valem {ok}, no máximo 12 (um a cada 0,34 s)");

		var leap = new WolfStrikePace();
		Check.True(leap.Allow(1, 500, true, 20, 58) && leap.Allow(2, 500, true, 20, 58), "um bote pega dois de uma vez");
		Check.True(!leap.Allow(3, 530, true, 20, 58), "outro bote meio segundo depois, não");
		Check.True(leap.Allow(3, 560, true, 20, 58), "um segundo depois, vale");
		Check.True(new WolfStrikePace().Allow(1, 0, false, 20, 58) && new WolfStrikePace().Allow(1, 0, true, 20, 58), "sem número de comando (jogo antigo), vale");
	}
}
