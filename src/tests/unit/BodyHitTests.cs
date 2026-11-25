using System;
using System.Collections.Generic;
using System.Numerics;

namespace Alcateia.UnitTests;

/// <summary>As peças do corpo que levam tiro (scripts/domain/BodyHit.cs):
/// a peça ajustada à pele e o raio entrando nela (Jhon, 25/11: "deixe mais
/// preciso, exigir mais mira para dar headshot").</summary>
public static class BodyHitTests
{
	/// <summary>A pele de um cilindro: anéis ao longo do eixo, de
	/// <paramref name="from"/> a <paramref name="to"/>.</summary>
	private static List<Vector3> Tube(Vector3 from, Vector3 to, float radius, int rings = 40, int around = 24)
	{
		var points = new List<Vector3>();
		Vector3 axis = Vector3.Normalize(to - from);
		Vector3 side = Vector3.Normalize(Vector3.Cross(axis, MathF.Abs(axis.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
		Vector3 up = Vector3.Cross(axis, side);
		for (int r = 0; r < rings; r++)
		{
			Vector3 c = Vector3.Lerp(from, to, r / (rings - 1f));
			for (int k = 0; k < around; k++)
			{
				float a = MathF.Tau * k / around;
				points.Add(c + (side * MathF.Cos(a) + up * MathF.Sin(a)) * radius);
			}
		}
		return points;
	}

	/// <summary>A pele de uma cabeça: um elipsoide de semieixos dados, com os
	/// pontos espalhados por igual (como os vértices de uma malha).</summary>
	private static List<Vector3> Head(Vector3 center, Vector3 half, int count = 900)
	{
		var points = new List<Vector3>();
		float golden = MathF.PI * (3f - MathF.Sqrt(5f));
		for (int i = 0; i < count; i++)
		{
			float y = 1f - 2f * (i + 0.5f) / count;
			float r = MathF.Sqrt(1f - y * y);
			float a = golden * i;
			points.Add(center + new Vector3(MathF.Cos(a) * r * half.X, y * half.Y, MathF.Sin(a) * r * half.Z));
		}
		return points;
	}

	[Test("um antebraço (tubo de 5 cm de raio e 30 cm, inclinado) vira uma peça no eixo dele, da grossura e do comprimento dele")]
	public static void FitsATube()
	{
		var from = new Vector3(0.1f, 1.2f, 0.0f);
		var to = new Vector3(0.3f, 1.0f, 0.15f);
		BodyHit.Shape s = BodyHit.Fit(Tube(from, to, 0.05f));
		Check.Near(0.05, s.Wide, 0.003, "a grossura de um lado é a do tubo");
		Check.Near(0.05, s.Deep, 0.003, "e a do outro");
		Check.True(MathF.Abs(Vector3.Dot(s.Axis, Vector3.Normalize(to - from))) > 0.999f, "o eixo é o do tubo");
		Check.Near(Vector3.Distance(from, to), s.Length, 0.02, "o comprimento é o do tubo");
		Check.Near(0, Vector3.Distance((from + to) * 0.5f, s.Center), 0.005, "no meio do tubo");
	}

	[Test("a cabeça (16 cm de largura, 21 de altura, 19 de fundo), com o ajuste da cabeça, vira um elipsoide do tamanho dela, menor que a esfera de antes (12 cm)")]
	public static void HeadIsNotBigger()
	{
		var center = new Vector3(0, 1.62f, 0);
		BodyHit.Shape s = BodyHit.Fit(Head(center, new Vector3(0.08f, 0.105f, 0.095f)), BodyHit.HeadContains);
		Check.Near(0, s.Half, 1e-3, "sem segmento: um elipsoide");
		Check.True(s.Cap <= 0.108f && s.Wide <= 0.108f && s.Deep <= 0.108f, $"nenhum raio passa da cabeça mais que 3 mm ({s.Cap:0.000} {s.Wide:0.000} {s.Deep:0.000})");
		float[] r = { s.Cap, s.Wide, s.Deep };
		Array.Sort(r);
		Check.Near(0.08, r[0], 0.01, "a meia largura (a ponta de 3% de cada lado fica de fora)");
		Check.Near(0.095, r[1], 0.01, "o meio fundo");
		Check.Near(0.105, r[2], 0.01, "a meia altura");
		Check.True(s.Reach < 0.12f, "cabe na esfera de antes");
	}

	[Test("o raio que passa 2 cm fora da cabeça não acerta; no meio, sim; de lado e de cima também")]
	public static void HeadshotNeedsTheHead()
	{
		var center = new Vector3(0, 1.62f, 0);
		BodyHit.Shape s = BodyHit.Fit(Head(center, new Vector3(0.08f, 0.105f, 0.095f)), BodyHit.HeadContains);
		var from = new Vector3(0, 1.62f, 10f);
		Check.True(BodyHit.Enter(from, -Vector3.UnitZ, s) is float t && MathF.Abs(t - (10f - 0.095f)) < 0.01f, "no meio, entra na frente da cabeça");
		Check.True(BodyHit.Enter(from + new Vector3(0.10f, 0, 0), -Vector3.UnitZ, s) == null, "2 cm ao lado da cabeça (8 cm de meia largura): não");
		Check.True(BodyHit.Enter(from + new Vector3(0.07f, 0, 0), -Vector3.UnitZ, s) != null, "dentro da largura: sim");
		Check.True(BodyHit.Enter(from + new Vector3(0, 0.125f, 0), -Vector3.UnitZ, s) == null, "2 cm acima do topo: não");
		Check.True(BodyHit.Enter(new Vector3(0, 3f, 0), -Vector3.UnitY, s) is float top && MathF.Abs(top - (3f - 1.725f)) < 0.01f, "de cima, entra no topo");
		Check.True(BodyHit.Enter(from, Vector3.UnitZ, s) == null, "atrás de quem atira: não");
		Check.Near(0, BodyHit.Enter(center, Vector3.UnitX, s) ?? -1, 1e-6, "nascendo dentro, entra já");
	}

	[Test("a cápsula: o raio entra no cilindro e nas pontas; paralelo ao eixo, só nas pontas; atrás de quem atira, não")]
	public static void RayEntersCapsule()
	{
		Vector3 a = Vector3.Zero, b = new(0, 1, 0);
		Check.Near(4.9, BodyHit.Capsule(new Vector3(5, 0.5f, 0), -Vector3.UnitX, a, b, 0.1f) ?? -1, 1e-4, "no meio do cilindro");
		Check.Near(5.0, BodyHit.Capsule(new Vector3(0, 6.1f, 0), -Vector3.UnitY, a, b, 0.1f) ?? -1, 1e-4, "pela ponta de cima, ao longo do eixo (entra a 1,1 m)");
		Check.True(BodyHit.Capsule(new Vector3(5, 1.08f, 0), -Vector3.UnitX, a, b, 0.1f) is float t && t > 4.9f && t < 5f, "na bola da ponta de cima");
		Check.True(BodyHit.Capsule(new Vector3(5, 1.11f, 0), -Vector3.UnitX, a, b, 0.1f) == null, "1 cm acima da ponta: não");
		Check.True(BodyHit.Capsule(new Vector3(5, 0.5f, 0.11f), -Vector3.UnitX, a, b, 0.1f) == null, "1 cm ao lado: não");
		Check.True(BodyHit.Capsule(new Vector3(5, 0.5f, 0), Vector3.UnitX, a, b, 0.1f) == null, "atrás de quem atira: não");
		Check.Near(0, BodyHit.Capsule(new Vector3(0, 0.5f, 0), Vector3.UnitX, a, b, 0.1f) ?? -1, 1e-6, "nascendo dentro, entra já");
	}

	[Test("a peça de corte oval (tronco de 36 × 22 cm) leva tiro dentro do oval e não fora dele")]
	public static void OvalCut()
	{
		// Um tronco: 40 cm de altura, 36 de largura (x), 22 de fundo (z).
		var shape = new BodyHit.Shape(new Vector3(0, 1.2f, 0), Vector3.UnitY, Vector3.UnitX, 0.09f, 0.11f, 0.18f, 0.11f);
		Check.True(BodyHit.Enter(new Vector3(0.16f, 1.2f, 5f), -Vector3.UnitZ, shape) != null, "de frente, 2 cm antes da borda do lado: sim");
		Check.True(BodyHit.Enter(new Vector3(0.20f, 1.2f, 5f), -Vector3.UnitZ, shape) == null, "2 cm fora do lado: não");
		Check.True(BodyHit.Enter(new Vector3(5f, 1.2f, 0.10f), -Vector3.UnitX, shape) != null, "de lado, dentro do fundo: sim");
		Check.True(BodyHit.Enter(new Vector3(5f, 1.2f, 0.13f), -Vector3.UnitX, shape) == null, "de lado, 2 cm fora do fundo: não (um corte redondo de 18 cm pegava)");
		Check.Near(5f - 0.11f, BodyHit.Enter(new Vector3(0, 1.2f, 5f), -Vector3.UnitZ, shape) ?? -1, 1e-3, "a distância certa até a frente");
	}

	[Test("pele de menos (um ponto só) vira uma peça vazia, que não leva tiro")]
	public static void TinyCloud()
	{
		BodyHit.Shape one = BodyHit.Fit(new List<Vector3> { new(1, 2, 3) });
		Check.True(BodyHit.Enter(new Vector3(1, 2, 10), -Vector3.UnitZ, one) == null, "nenhum tiro acerta");
	}
}
