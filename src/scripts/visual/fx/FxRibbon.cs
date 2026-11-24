using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Fitas de luz para os efeitos (raios, vento, ondas do rugido): uma tira
/// de frente para a câmera ao longo de uma linha de pontos, macia nas
/// bordas (a textura apaga para os lados), somando luz. Neon é sempre
/// duas fitas no mesmo caminho: uma larga na cor (o clarão) e uma fina
/// quase branca (o miolo).
///
/// Quem usa abre e fecha a superfície do <see cref="ImmediateMesh"/>
/// (triângulos); aqui só entram os vértices.
/// </summary>
public static class FxRibbon
{
	private static ImageTexture _soft;
	private static StandardMaterial3D _additive;

	/// <summary>Perfil da fita: transparente nas bordas, cheio no meio.</summary>
	public static Texture2D Soft()
	{
		if (_soft != null) return _soft;
		const int width = 64;
		Image image = Image.CreateEmpty(width, 4, false, Image.Format.Rgba8);
		for (int x = 0; x < width; x++)
		{
			float d = Mathf.Abs(x / (width - 1f) * 2f - 1f);
			float a = Mathf.Pow(Mathf.Clamp(1f - d, 0f, 1f), 1.7f);
			for (int y = 0; y < 4; y++) image.SetPixel(x, y, new Color(1, 1, 1, a));
		}
		_soft = ImageTexture.CreateFromImage(image);
		return _soft;
	}

	/// <summary>O material das fitas: sem luz, somando, sem sombra, a cor
	/// e o alfa vêm do vértice.</summary>
	public static StandardMaterial3D Additive() => _additive ??= new StandardMaterial3D
	{
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		BlendMode = BaseMaterial3D.BlendModeEnum.Add,
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		VertexColorUseAsAlbedo = true,
		AlbedoTexture = Soft(),
		DisableReceiveShadows = true,
		DisableFog = true
	};

	/// <summary>Uma malha pronta para receber fitas, solta no mundo
	/// (coordenadas do mundo) ou presa ao pai.</summary>
	public static MeshInstance3D Canvas(ImmediateMesh mesh, bool world) => new()
	{
		Mesh = mesh,
		MaterialOverride = Additive(),
		CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		TopLevel = world,
		// A malha muda todo quadro: sem a margem ela some quando o ponto
		// onde nasceu sai da tela.
		ExtraCullMargin = 8f
	};

	/// <summary>
	/// Uma fita ao longo de <paramref name="points"/>, de frente para
	/// <paramref name="eye"/> (no mesmo espaço dos pontos).
	/// <paramref name="width"/> e <paramref name="color"/> recebem a
	/// posição ao longo da fita, de 0 a 1.
	/// </summary>
	public static void Strip(ImmediateMesh mesh, IReadOnlyList<Vector3> points, Vector3 eye,
		Func<float, float> width, Func<float, Color> color)
	{
		int n = points.Count;
		if (n < 2) return;

		Vector3 prevL = default, prevR = default;
		Color prevC = default;
		float prevT = 0f;
		for (int i = 0; i < n; i++)
		{
			float t = i / (n - 1f);
			Vector3 along = points[Mathf.Min(i + 1, n - 1)] - points[Mathf.Max(i - 1, 0)];
			Vector3 across = along.Cross(eye - points[i]);
			across = across.LengthSquared() > 1e-10f ? across.Normalized() : Vector3.Up;
			float half = width(t) * 0.5f;
			Vector3 l = points[i] - across * half, r = points[i] + across * half;
			Color c = color(t);

			if (i > 0)
			{
				Add(mesh, prevL, 0f, prevT, prevC);
				Add(mesh, prevR, 1f, prevT, prevC);
				Add(mesh, l, 0f, t, c);
				Add(mesh, prevR, 1f, prevT, prevC);
				Add(mesh, r, 1f, t, c);
				Add(mesh, l, 0f, t, c);
			}

			prevL = l; prevR = r; prevC = c; prevT = t;
		}
	}

	/// <summary>Um anel deitado no plano de <paramref name="axis"/>, com
	/// a largura dada: a onda de choque.</summary>
	public static void Ring(ImmediateMesh mesh, Vector3 center, Vector3 axis, float radius, float width, Color color, int segments = 40)
	{
		axis = axis.Normalized();
		Vector3 u = axis.Cross(Mathf.Abs(axis.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized();
		Vector3 v = axis.Cross(u);
		float inner = Mathf.Max(radius - width * 0.5f, 0f), outer = radius + width * 0.5f;
		for (int i = 0; i < segments; i++)
		{
			float a0 = Mathf.Tau * i / segments, a1 = Mathf.Tau * (i + 1) / segments;
			Vector3 d0 = u * Mathf.Cos(a0) + v * Mathf.Sin(a0), d1 = u * Mathf.Cos(a1) + v * Mathf.Sin(a1);
			Add(mesh, center + d0 * inner, 0f, 0f, color);
			Add(mesh, center + d0 * outer, 1f, 0f, color);
			Add(mesh, center + d1 * inner, 0f, 1f, color);
			Add(mesh, center + d0 * outer, 1f, 0f, color);
			Add(mesh, center + d1 * outer, 1f, 1f, color);
			Add(mesh, center + d1 * inner, 0f, 1f, color);
		}
	}

	private static void Add(ImmediateMesh mesh, Vector3 at, float u, float v, Color color)
	{
		mesh.SurfaceSetColor(color);
		mesh.SurfaceSetUV(new Vector2(u, v));
		mesh.SurfaceAddVertex(at);
	}

	/// <summary>Um raio quebrado de <paramref name="from"/> a
	/// <paramref name="to"/>: firme nas pontas, torto no meio.</summary>
	public static Vector3[] Jagged(RandomNumberGenerator rng, Vector3 from, Vector3 to, int segments, float jitter)
	{
		var points = new Vector3[segments + 1];
		Vector3 dir = to - from;
		Vector3 side = dir.Cross(Mathf.Abs(dir.Normalized().Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized();
		Vector3 lift = dir.Cross(side).Normalized();
		for (int i = 0; i <= segments; i++)
		{
			float t = i / (float)segments;
			float jag = jitter * Mathf.Sin(t * Mathf.Pi);
			points[i] = from + dir * t + side * rng.RandfRange(-jag, jag) + lift * rng.RandfRange(-jag, jag);
		}
		return points;
	}

	/// <summary>Um raio de neon: o clarão na cor e o miolo branco.</summary>
	public static void Bolt(ImmediateMesh mesh, Vector3[] points, Vector3 eye, float width, Color tint, float strength)
	{
		var glow = new Color(tint.R, tint.G, tint.B, 0.5f * strength);
		var core = new Color(1f, 0.97f, 0.94f, strength);
		Strip(mesh, points, eye, t => width * 3.4f * (1f - 0.55f * t), _ => glow);
		Strip(mesh, points, eye, t => width * (1f - 0.6f * t), _ => core);
	}
}
