using System;
using System.Numerics;

/// <summary>
/// O tiro achou a cabeça? Em C# puro, para ter teste de unidade
/// (tests/unit/HeadHitTests). Quem acha os ossos no modelo é
/// <c>HeadHitbox</c>; quem dispara o raio é a arma.
///
/// A cabeça é uma esfera entre o osso da cabeça (a base do crânio) e o do
/// topo, no modelo que se vê. A cápsula de movimento não serve de cabeça
/// (bug do Jhon, 14/11): no humano a zona de cima dela pegava só a metade
/// de cima da cabeça, e no lobo, curvado, a cabeça fica meio metro à
/// frente, fora da cápsula.
/// </summary>
public static class HeadHit
{
	/// <summary>Raio da cabeça sobre a distância entre os dois ossos. Do
	/// humano saem 12 cm (a cabeça de 21 cm de altura, do queixo ao topo);
	/// do lobo, 12,5.</summary>
	public const float RadiusFactor = 0.55f;

	/// <summary>Quanto o raio ainda anda depois de entrar no corpo do
	/// mesmo dono e acha a cabeça, em metros. A cabeça do humano fica
	/// dentro da cápsula, um palmo atrás da casca: o raio bate na cápsula
	/// e só depois chega nela. Mais que isto, é um tiro que entrou pelo pé
	/// e sairia na cabeça.</summary>
	public const float Depth = 0.45f;

	/// <summary>A esfera da cabeça a partir dos dois ossos.</summary>
	public static (Vector3 Center, float Radius) Sphere(Vector3 headBone, Vector3 topBone) =>
		((headBone + topBone) * 0.5f, Vector3.Distance(headBone, topBone) * RadiusFactor);

	/// <summary>Onde, ao longo do raio (de <paramref name="from"/>, direção
	/// unitária), ele entra na esfera. Nulo se não toca ou se ela ficou para
	/// trás; zero se o raio nasce dentro dela.</summary>
	public static float? Enter(Vector3 from, Vector3 direction, Vector3 center, float radius)
	{
		Vector3 m = from - center;
		float b = Vector3.Dot(m, direction);
		float c = m.LengthSquared() - radius * radius;

		// Fora dela e indo embora.
		if (c > 0f && b > 0f) return null;

		float disc = b * b - c;
		if (disc < 0f) return null;

		return MathF.Max(-b - MathF.Sqrt(disc), 0f);
	}

	/// <summary>
	/// A cabeça, a <paramref name="head"/> metros no raio, conta como
	/// acerto? <paramref name="hit"/> é onde o raio bateu em alguma coisa
	/// (nulo: em nada); <paramref name="sameBody"/>, se essa coisa é o corpo
	/// dono da cabeça. Conta se ela vem antes do que o raio bateu — parede
	/// na frente protege — ou, se o que ele bateu é o próprio corpo, até
	/// <see cref="Depth"/> depois.
	/// </summary>
	public static bool Counts(float head, float? hit, bool sameBody) =>
		hit is not float at || head <= at || (sameBody && head <= at + Depth);
}
