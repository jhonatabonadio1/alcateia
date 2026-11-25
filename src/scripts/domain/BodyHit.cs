using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// As peças do corpo que levam tiro, em C# puro (teste de unidade em
/// tests/unit/BodyHitTests). Jhon, 25/11: "ajuste o hitbox dos personagens,
/// deixe mais preciso, exigir mais mira para dar headshot"; "tá muito fácil
/// dar headshot".
///
/// Antes o tiro acertava a cápsula de movimento (60 cm de largura, mais
/// larga e mais funda que o corpo) e a cabeça era uma esfera de 12 a 13 cm
/// de raio entre os ossos da cabeça, que passava da cabeça visível pelos
/// lados e por baixo. Agora cada peça do corpo (cabeça, tronco, braço,
/// antebraço, mão, coxa, canela, pé) é uma <see cref="Shape"/> ajustada à
/// pele que pesa naquele osso, no espaço do osso: segue a animação e é do
/// tamanho do que se vê.
///
/// <see cref="Fit"/> ajusta (uma vez por modelo); <see cref="Enter"/> diz
/// onde o raio entra na peça.
/// </summary>
public static class BodyHit
{
	/// <summary>
	/// Uma peça: uma cápsula de corte oval. O eixo <see cref="Axis"/> é o
	/// comprimento; o corte tem os raios <see cref="Wide"/> (na direção
	/// <see cref="Across"/>) e <see cref="Deep"/> (na terceira); as pontas são
	/// meios elipsoides de <see cref="Cap"/> ao longo do eixo. Peça curta
	/// (<see cref="Half"/> zero) é um elipsoide: a cabeça, um pedaço de tronco.
	/// </summary>
	public readonly record struct Shape(Vector3 Center, Vector3 Axis, Vector3 Across, float Half, float Cap, float Wide, float Deep)
	{
		/// <summary>A terceira direção do corte.</summary>
		public Vector3 Depth => Vector3.Cross(Axis, Across);

		/// <summary>A bola que contém a peça inteira (o teste rápido).</summary>
		public float Reach => Half + MathF.Max(Cap, MathF.Max(Wide, Deep));

		/// <summary>Comprimento de ponta a ponta.</summary>
		public float Length => 2f * (Half + Cap);

		/// <summary>A peça levada por uma transformação rígida com escala
		/// uniforme <paramref name="scale"/> (o osso no mundo).</summary>
		public Shape Moved(Func<Vector3, Vector3> point, Func<Vector3, Vector3> direction, float scale) =>
			new(point(Center), Vector3.Normalize(direction(Axis)), Vector3.Normalize(direction(Across)),
				Half * scale, Cap * scale, Wide * scale, Deep * scale);
	}

	/// <summary>O corte: do ponto 3% ao 97% da pele em cada direção (as
	/// pontas de pelo, cabelo e dedo soltas não engordam a peça). O oval
	/// cabe na caixa desses pontos: onde o corte é quadrado, fica por dentro.</summary>
	public const float CrossQuantile = 0.03f;

	/// <summary>O comprimento: do ponto 2% ao 98% ao longo do eixo.</summary>
	public const float LengthQuantile = 0.02f;

	/// <summary>A partir de quantas vezes o maior raio do corte o comprimento
	/// (a metade) faz da peça uma cápsula; abaixo disso, ela é um elipsoide.</summary>
	public const float ElongatedFrom = 1.5f;

	/// <summary>Quanto da pele a peça contém: o oval é aumentado ou diminuído,
	/// sem mudar a proporção, até ter dentro esta fração dos pontos. Onde o
	/// corte é quadrado (o tronco), o oval da caixa deixa os cantos de fora;
	/// onde a pele tem pontas soltas (pelo, cabelo), a caixa sairia dela.
	/// Medido em 25/11 contra a silhueta (teste CorpoPrecisoNoTiro), de 0,6 a
	/// 0,9: com 0,8 o ar em volta que leva tiro (uma faixa de 1 a 2 cm) fica em
	/// 11–18% da silhueta e o corpo visível que não leva, em 2–8% (com 0,9,
	/// 25% e 1%; com 0,6, 8% e 10%). Antes, a cápsula de movimento: 55–90% e 2–37%.</summary>
	public const float Contains = 0.8f;

	/// <summary>O mesmo para a cabeça, mais justo: headshot exige mira (Jhon,
	/// 25/11). Com 0,6 a cabeça quase não sai da cabeça visível (0–10% da área
	/// dela) e a borda dela (pontas de cabelo, juba) conta como corpo ou erra.</summary>
	public const float HeadContains = 0.6f;

	/// <summary>
	/// As peças de uma nuvem de pontos da pele de um osso. Comprida (braço,
	/// coxa, canela, pé), vira duas, uma em cada metade: o membro afina
	/// (coxa grossa, joelho fino) e uma peça só, da grossura maior, deixava
	/// ar em volta da ponta fina levando tiro. Curta, uma só.
	/// </summary>
	public static List<Shape> FitPieces(IReadOnlyList<Vector3> points, float contains = Contains, bool split = true)
	{
		Shape whole = Fit(points, contains);
		var pieces = new List<Shape>();
		if (!split || whole.Half <= 0f || points.Count < 32)
		{
			pieces.Add(whole);
			return pieces;
		}
		var low = new List<Vector3>();
		var high = new List<Vector3>();
		foreach (Vector3 p in points)
			(Vector3.Dot(p - whole.Center, whole.Axis) < 0f ? low : high).Add(p);
		foreach (var half in new[] { low, high })
			if (half.Count >= 16) pieces.Add(Fit(half, contains));
		if (pieces.Count == 0) pieces.Add(whole);
		return pieces;
	}

	/// <summary>
	/// A peça de uma nuvem de pontos da pele (no espaço do osso): o eixo é a
	/// direção em que ela é mais comprida (componente principal), o meio e a
	/// proporção do oval saem da caixa dos pontos nos três eixos principais, e
	/// o tamanho, de conter <see cref="Contains"/> da pele. Curta, é um
	/// elipsoide; comprida, as pontas têm o raio médio do corte.
	/// </summary>
	public static Shape Fit(IReadOnlyList<Vector3> points, float contains = Contains)
	{
		int n = points.Count;
		// Pele de menos para dizer a forma: nenhuma peça.
		if (n < 8) return new Shape(Vector3.Zero, Vector3.UnitY, Vector3.UnitX, 0f, 0f, 0f, 0f);

		Vector3 mean = Vector3.Zero;
		foreach (Vector3 p in points) mean += p;
		mean /= n;

		// A covariância e os dois primeiros eixos principais (iteração de potência).
		float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
		foreach (Vector3 p in points)
		{
			Vector3 d = p - mean;
			xx += d.X * d.X; xy += d.X * d.Y; xz += d.X * d.Z;
			yy += d.Y * d.Y; yz += d.Y * d.Z; zz += d.Z * d.Z;
		}
		Vector3 Mul(Vector3 v) => new(xx * v.X + xy * v.Y + xz * v.Z, xy * v.X + yy * v.Y + yz * v.Z, xz * v.X + yz * v.Y + zz * v.Z);
		Vector3 e1 = Principal(Mul, Vector3.Zero);
		Vector3 e2 = Principal(Mul, e1);
		Vector3 e3 = Vector3.Normalize(Vector3.Cross(e1, e2));

		var u = new float[n];
		var v = new float[n];
		var w = new float[n];
		for (int i = 0; i < n; i++)
		{
			Vector3 d = points[i] - mean;
			u[i] = Vector3.Dot(d, e1); v[i] = Vector3.Dot(d, e2); w[i] = Vector3.Dot(d, e3);
		}

		float uLow = Quantile(u, LengthQuantile), uHigh = Quantile(u, 1f - LengthQuantile);
		float vLow = Quantile(v, CrossQuantile), vHigh = Quantile(v, 1f - CrossQuantile);
		float wLow = Quantile(w, CrossQuantile), wHigh = Quantile(w, 1f - CrossQuantile);
		Vector3 center = mean + e1 * ((uLow + uHigh) * 0.5f) + e2 * ((vLow + vHigh) * 0.5f) + e3 * ((wLow + wHigh) * 0.5f);

		float halfLength = MathF.Max((uHigh - uLow) * 0.5f, 1e-4f);
		float wide = MathF.Max((vHigh - vLow) * 0.5f, 1e-4f), deep = MathF.Max((wHigh - wLow) * 0.5f, 1e-4f);
		// Pouco mais comprida que grossa (a cabeça, um pedaço de tronco):
		// um elipsoide. Comprida (braço, coxa): cápsula de pontas do raio médio.
		float cap = halfLength <= ElongatedFrom * MathF.Max(wide, deep) ? halfLength : (wide + deep) * 0.5f;
		var shape = new Shape(center, e1, e2, halfLength - cap, cap, wide, deep);

		// O tamanho que contém a fração pedida da pele, na mesma proporção. Na
		// peça comprida, só o corte conta (o comprimento já é o da pele): as
		// pontas retas de um membro ficariam fora da ponta redonda e engordariam
		// a peça inteira.
		var reach = new List<float>(n);
		foreach (Vector3 p in points)
		{
			if (shape.Half > 0f)
			{
				Vector3 d = p - center;
				if (MathF.Abs(Vector3.Dot(d, e1)) > shape.Half) continue;
				float a = Vector3.Dot(d, e2) / wide, b = Vector3.Dot(d, shape.Depth) / deep;
				reach.Add(MathF.Sqrt(a * a + b * b));
			}
			else reach.Add(Normalized(p, shape));
		}
		if (reach.Count < 8) return shape;
		float scale = Quantile(reach.ToArray(), contains);
		if (!(scale > 0f)) return shape;
		return new Shape(center, e1, e2, shape.Half, shape.Cap * scale, shape.Wide * scale, shape.Deep * scale);
	}

	/// <summary>Quão longe o ponto está, na medida da peça: 1 na casca, menos
	/// dentro, mais fora (a distância ao segmento no espaço em que ela é uma
	/// cápsula de raio 1).</summary>
	public static float Normalized(Vector3 point, Shape shape)
	{
		Vector3 d = point - shape.Center;
		float along = Vector3.Dot(d, shape.Axis);
		float past = MathF.Max(MathF.Abs(along) - shape.Half, 0f) / MathF.Max(shape.Cap, 1e-6f);
		float a = Vector3.Dot(d, shape.Across) / MathF.Max(shape.Wide, 1e-6f);
		float b = Vector3.Dot(d, shape.Depth) / MathF.Max(shape.Deep, 1e-6f);
		return MathF.Sqrt(past * past + a * a + b * b);
	}

	/// <summary>
	/// Onde, ao longo do raio (de <paramref name="from"/>, direção unitária),
	/// ele entra na peça. Nulo se não toca ou se ela ficou para trás; zero se
	/// o raio nasce dentro dela.
	/// </summary>
	public static float? Enter(Vector3 from, Vector3 direction, Shape shape)
	{
		// No espaço em que a peça é uma cápsula de raio 1: cada eixo dividido
		// pelo raio dele. A conta é linear, então a distância ao longo do raio
		// é a do espaço da peça dividida pelo quanto a direção esticou.
		if (shape.Cap <= 1e-6f || shape.Wide <= 1e-6f || shape.Deep <= 1e-6f) return null;
		Vector3 depth = shape.Depth;
		Vector3 Local(Vector3 p) => new(Vector3.Dot(p, shape.Axis) / shape.Cap, Vector3.Dot(p, shape.Across) / shape.Wide, Vector3.Dot(p, depth) / shape.Deep);
		Vector3 o = Local(from - shape.Center);
		Vector3 d = Local(direction);
		float stretch = d.Length();
		if (stretch < 1e-9f) return null;
		d /= stretch;
		float h = shape.Half / shape.Cap;
		float? t = Capsule(o, d, new Vector3(-h, 0, 0), new Vector3(h, 0, 0), 1f);
		return t is float at ? at / stretch : null;
	}

	/// <summary>O raio (direção unitária) entrando numa cápsula de pontas A e B.</summary>
	public static float? Capsule(Vector3 from, Vector3 direction, Vector3 a, Vector3 b, float radius)
	{
		// As duas pontas são bolas; o meio, um cilindro.
		float? best = HeadHit.Enter(from, direction, a, radius);
		float? end = HeadHit.Enter(from, direction, b, radius);
		if (end is float e && (best is not float b0 || e < b0)) best = end;

		Vector3 axis = b - a;
		float length2 = axis.LengthSquared();
		if (length2 < 1e-10f) return best;

		Vector3 oa = from - a;
		float bard = Vector3.Dot(axis, direction);
		float baoa = Vector3.Dot(axis, oa);
		float qa = length2 - bard * bard;
		if (qa < 1e-10f) return best;          // paralelo ao eixo: só as pontas

		float qb = length2 * Vector3.Dot(direction, oa) - baoa * bard;
		float qc = length2 * oa.LengthSquared() - baoa * baoa - radius * radius * length2;
		float disc = qb * qb - qa * qc;
		if (disc < 0f) return best;

		float root = MathF.Sqrt(disc);
		float t = (-qb - root) / qa;
		if ((-qb + root) / qa < 0f) return best;   // o cilindro ficou para trás
		float y = baoa + MathF.Max(t, 0f) * bard;
		if (y > 0f && y < length2)
		{
			t = MathF.Max(t, 0f);
			if (best is not float b1 || t < b1) best = t;
		}
		return best;
	}

	private static Vector3 Principal(Func<Vector3, Vector3> mul, Vector3 not)
	{
		// Começa de um eixo que não seja o já achado.
		Vector3 x = MathF.Abs(not.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
		x = Vector3.Normalize(x + new Vector3(0.13f, 0.29f, 0.41f));
		for (int i = 0; i < 48; i++)
		{
			x -= not * Vector3.Dot(x, not);
			Vector3 y = mul(x);
			y -= not * Vector3.Dot(y, not);
			float len = y.Length();
			if (len < 1e-12f) break;
			x = y / len;
		}
		x -= not * Vector3.Dot(x, not);
		return x.LengthSquared() < 1e-12f ? Vector3.Normalize(Vector3.Cross(not, Vector3.UnitZ + Vector3.UnitX * 0.1f)) : Vector3.Normalize(x);
	}

	private static float Quantile(float[] values, float q)
	{
		var sorted = (float[])values.Clone();
		Array.Sort(sorted);
		float at = Math.Clamp(q, 0f, 1f) * (sorted.Length - 1);
		int lo = (int)MathF.Floor(at), hi = Math.Min(lo + 1, sorted.Length - 1);
		return sorted[lo] + (sorted[hi] - sorted[lo]) * (at - lo);
	}
}
