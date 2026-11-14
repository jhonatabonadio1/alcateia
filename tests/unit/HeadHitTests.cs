using System.Numerics;

namespace Alcateia.UnitTests;

/// <summary>A cabeça do modelo como esfera, e quando o raio a acerta.</summary>
public static class HeadHitTests
{
	[Test("a cabeça é a esfera no meio dos dois ossos, com 55% da distância de raio")]
	public static void SphereFromBones()
	{
		var (center, radius) = HeadHit.Sphere(new Vector3(0, 0.64f, 0), new Vector3(0, 0.84f, 0));
		Check.Near(0.74, center.Y, 1e-5, "centro na altura do meio");
		Check.Near(0.11, radius, 1e-5, "raio");
	}

	[Test("o raio entra na esfera na distância certa, passa ao lado sem tocar e não volta para trás")]
	public static void RayEntersSphere()
	{
		Vector3 center = new(10, 0, 0);
		Check.Near(9.9, HeadHit.Enter(Vector3.Zero, Vector3.UnitX, center, 0.1f) ?? -1, 1e-4, "de frente");
		Check.True(HeadHit.Enter(new Vector3(0, 0.11f, 0), Vector3.UnitX, center, 0.1f) == null, "11 cm acima de uma cabeça de 10 cm não toca");
		Check.True(HeadHit.Enter(Vector3.Zero, -Vector3.UnitX, center, 0.1f) == null, "a cabeça atrás de quem atira não conta");
		Check.Near(0, HeadHit.Enter(center, Vector3.UnitX, center, 0.1f) ?? -1, 1e-6, "nascendo dentro, entra já");
	}

	[Test("parede na frente protege a cabeça; a cápsula do próprio dono, só até um palmo")]
	public static void WhatCounts()
	{
		Check.True(HeadHit.Counts(10f, null, false), "o raio não bateu em nada: a cabeça conta");
		Check.True(!HeadHit.Counts(10f, 8f, false), "parede a 8 m, cabeça a 10: não conta");
		Check.True(HeadHit.Counts(10f, 12f, false), "a cabeça vem antes da parede");
		Check.True(HeadHit.Counts(10.1f, 10f, true), "a cabeça do humano, 10 cm dentro da cápsula dele");
		Check.True(!HeadHit.Counts(11.5f, 10f, true), "entrou pelo pé, sairia na cabeça: não conta");
	}
}
