using System;
using System.Numerics;

/// <summary>
/// O pé do lobo no chão (alpha fechado, 26/11: "os pés do lobo atravessam o
/// chão, precisa corrigir para ele tocar o chão perfeitamente"). Em C# puro,
/// com teste de unidade (tests/unit/FootPlantTests). Quem lê a sola e o chão
/// e gira os ossos é o <c>WolfFootPlant</c>.
///
/// Três contas:
/// - <see cref="Stance"/>: a pata está apoiando? A que apoia fica parada em
///   relação ao corpo na vertical (o passo a leva para trás, não para cima); a
///   que dá o passo sobe e desce. Parada, apoio; subindo ou descendo, no ar.
/// - <see cref="Lift"/>: quanto a sola sobe ou desce para encostar no chão.
///   Dentro do chão, sobe sempre tudo. Acima dele, a pata de apoio desce até
///   encostar (o pé flutuando numa rampa, num degrau, ou pela diferença de
///   proporção entre o esqueleto dos clipes e o do corpo), até
///   <c>band</c> de altura; a que dá o passo não é puxada.
/// - <see cref="Solve"/>: o IK de dois ossos (coxa e canela, braço e
///   antebraço). Leva a ponta (tornozelo, pulso) ao alvo mantendo o plano
///   em que o joelho dobra; se o alvo está longe demais, estica até o limite.
/// </summary>
public static class FootPlant
{
	/// <summary>O quanto a pata está apoiando (0 a 1) pela velocidade
	/// vertical da sola em relação ao corpo, em m/s: até metade de
	/// <paramref name="still"/>, apoio; a partir de <paramref name="still"/>,
	/// no ar; entre os dois, suave.</summary>
	public static float Stance(float verticalSpeed, float still)
	{
		float speed = MathF.Abs(verticalSpeed);
		if (still <= 0f) return speed <= 0f ? 1f : 0f;
		return 1f - Smooth(0.5f * still, still, speed);
	}

	/// <summary>Quanto mover a sola na vertical (positivo, para cima):
	/// <paramref name="gap"/> é a altura da sola acima do chão (negativo:
	/// dentro dele), <paramref name="contact"/> o quanto a pata apoia
	/// (<see cref="Stance"/>). Dentro do chão sobe tudo, sempre. Acima, desce o
	/// vão vezes o apoio, até <paramref name="band"/> de altura (o peso cai
	/// suave a partir de 60% da faixa).</summary>
	public static float Lift(float gap, float contact, float band)
	{
		if (gap <= 0f) return -gap;
		if (band <= 0f || gap >= band) return 0f;
		float fade = 1f - Smooth(0.6f * band, band, gap);
		return -gap * Math.Clamp(contact, 0f, 1f) * fade;
	}

	private static float Smooth(float from, float to, float x)
	{
		if (to <= from) return x >= to ? 1f : 0f;
		float t = Math.Clamp((x - from) / (to - from), 0f, 1f);
		return t * t * (3f - 2f * t);
	}

	/// <summary>
	/// IK de dois ossos: <paramref name="root"/> (quadril/ombro),
	/// <paramref name="mid"/> (joelho/cotovelo) e <paramref name="end"/>
	/// (tornozelo/pulso), e o alvo da ponta. Devolve o joelho e a ponta novos,
	/// com os comprimentos dos dois ossos iguais aos de antes e o joelho do
	/// mesmo lado do plano em que já dobrava.
	/// </summary>
	public static (Vector3 Mid, Vector3 End) Solve(Vector3 root, Vector3 mid, Vector3 end, Vector3 target)
	{
		float a = Vector3.Distance(root, mid);
		float b = Vector3.Distance(mid, end);
		if (a < 1e-6f || b < 1e-6f) return (mid, end);

		Vector3 toTarget = target - root;
		float c = toTarget.Length();
		if (c < 1e-6f) return (mid, end);
		Vector3 dir = toTarget / c;

		// O alvo fora do alcance (perna esticada demais ou dobrada demais): até a borda.
		float min = MathF.Abs(a - b) + 1e-4f, max = a + b - 1e-4f;
		c = Math.Clamp(c, min, max);

		// O lado para onde o joelho aponta: a parte do joelho atual que não está
		// na linha raiz→alvo. Joelho reto (sem dobra): a dobra atual não diz nada,
		// e qualquer perpendicular serve, a mais próxima da linha raiz→ponta.
		Vector3 bend = (mid - root) - Vector3.Dot(mid - root, dir) * dir;
		if (bend.LengthSquared() < 1e-10f)
		{
			Vector3 side = (end - root) - Vector3.Dot(end - root, dir) * dir;
			bend = side.LengthSquared() > 1e-10f ? side : Vector3.Cross(dir, MathF.Abs(dir.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
		}
		bend = Vector3.Normalize(bend);

		// Lei dos cossenos: o ângulo na raiz entre a linha para o alvo e o primeiro osso.
		float cos = Math.Clamp((a * a + c * c - b * b) / (2f * a * c), -1f, 1f);
		float sin = MathF.Sqrt(MathF.Max(0f, 1f - cos * cos));
		Vector3 newMid = root + a * (dir * cos + bend * sin);
		Vector3 newEnd = root + dir * c;
		return (newMid, newEnd);
	}

	/// <summary>O giro mais curto que leva a direção <paramref name="from"/> à
	/// <paramref name="to"/>.</summary>
	public static Quaternion Arc(Vector3 from, Vector3 to)
	{
		from = Vector3.Normalize(from);
		to = Vector3.Normalize(to);
		float dot = Math.Clamp(Vector3.Dot(from, to), -1f, 1f);
		if (dot > 0.999999f) return Quaternion.Identity;
		Vector3 axis = Vector3.Cross(from, to);
		if (axis.LengthSquared() < 1e-12f)
			axis = Vector3.Cross(from, MathF.Abs(from.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY);
		return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Acos(dot));
	}
}
