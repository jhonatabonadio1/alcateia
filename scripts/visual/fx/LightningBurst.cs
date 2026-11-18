using Godot;

/// <summary>
/// Os raios do especial do lobo (pedido do Jhon, 18/11: "deve ter efeito
/// visual, uns raios, saindo"): relâmpagos quebrados que saem do corpo
/// para todos os lados, tremem e somem em menos de um segundo, com um
/// clarão. Duas vezes por especial: quando ele dispara (em volta do
/// corpo) e quando o lobo cai (rente ao chão, mais largo).
///
/// Cada raio é uma fita dupla em cruz (para ter largura de qualquer
/// ângulo), sem sombra e somando luz. O desenho é refeito várias vezes
/// por segundo — é isso que faz o raio tremer. Montado por código; some
/// sozinho no fim.
/// </summary>
public partial class LightningBurst : Node3D
{
	/// <summary>Quantos raios.</summary>
	public int Bolts { get; set; } = 12;

	/// <summary>Até onde eles chegam, em metros.</summary>
	public float Radius { get; set; } = 2.4f;

	/// <summary>Quanto tempo o efeito dura.</summary>
	public float Life { get; set; } = 0.55f;

	/// <summary>Rente ao chão (a queda) em vez de para todos os lados.</summary>
	public bool Flat { get; set; }

	public Color Tint { get; set; } = new(1f, 0.6f, 0.2f);

	/// <summary>De quanto em quanto tempo o desenho muda.</summary>
	private const double Flicker = 0.045;

	private const int Segments = 7;
	private const float Width = 0.07f;

	private ImmediateMesh _mesh;
	private StandardMaterial3D _material;
	private OmniLight3D _flash;
	private readonly RandomNumberGenerator _rng = new();
	private double _age, _nextDraw;
	private Vector3[] _directions;

	/// <summary>Solta os raios em <paramref name="parent"/>, na posição
	/// local dada.</summary>
	public static LightningBurst Spawn(Node3D parent, Vector3 local, Color tint, bool flat)
	{
		var burst = new LightningBurst
		{
			Tint = tint,
			Flat = flat,
			Position = local,
			Bolts = flat ? 14 : 12,
			Radius = flat ? 3.2f : 2.4f,
			Life = flat ? 0.5f : 0.6f
		};
		parent.AddChild(burst);
		return burst;
	}

	public override void _Ready()
	{
		_rng.Randomize();
		_mesh = new ImmediateMesh();
		_material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			VertexColorUseAsAlbedo = true,
			AlbedoColor = Colors.White
		};
		AddChild(new MeshInstance3D
		{
			Mesh = _mesh,
			MaterialOverride = _material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		});

		_flash = new OmniLight3D
		{
			LightColor = Tint.Lerp(Colors.White, 0.4f),
			LightEnergy = 5f,
			OmniRange = Radius * 2.2f,
			ShadowEnabled = false
		};
		AddChild(_flash);

		// Cada raio tem a sua direção; o caminho até lá é que muda.
		_directions = new Vector3[Bolts];
		for (int i = 0; i < Bolts; i++)
		{
			float angle = Mathf.Tau * (i + _rng.Randf() * 0.7f) / Bolts;
			float up = Flat ? _rng.RandfRange(0.02f, 0.22f) : _rng.RandfRange(-0.25f, 0.95f);
			_directions[i] = new Vector3(Mathf.Cos(angle), up, Mathf.Sin(angle)).Normalized();
		}
	}

	public override void _Process(double delta)
	{
		_age += delta;
		if (_age >= Life)
		{
			QueueFree();
			return;
		}

		float t = (float)(_age / Life);
		// Estoura cheio e apaga; o clarão vai mais rápido que os raios.
		float strength = 1f - t * t;
		_flash.LightEnergy = 5f * Mathf.Max(0f, 1f - t * 2.2f);

		if (_age < _nextDraw) return;
		_nextDraw = _age + Flicker;
		Draw(strength, t);
	}

	private void Draw(float strength, float t)
	{
		_mesh.ClearSurfaces();
		_mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

		// Os raios crescem para fora no começo.
		float reach = Radius * Mathf.Min(1f, 0.35f + t * 4f);
		var core = new Color(1f, 0.97f, 0.9f, strength);
		var glow = new Color(Tint.R, Tint.G, Tint.B, strength * 0.55f);

		for (int b = 0; b < Bolts; b++)
		{
			// Uns falham a cada desenho: relâmpago não é contínuo.
			if (_rng.Randf() < 0.2f) continue;

			Vector3 dir = _directions[b];
			float length = reach * _rng.RandfRange(0.55f, 1f);
			Vector3 side = dir.Cross(Vector3.Up);
			if (side.LengthSquared() < 0.001f) side = Vector3.Right;
			side = side.Normalized();
			Vector3 lift = dir.Cross(side).Normalized();

			var points = new Vector3[Segments + 1];
			for (int i = 0; i <= Segments; i++)
			{
				float along = i / (float)Segments;
				// Quebra mais no meio, firme nas pontas.
				float jag = length * 0.11f * Mathf.Sin(along * Mathf.Pi);
				points[i] = dir * (0.25f + length * along)
					+ side * _rng.RandfRange(-jag, jag) + lift * _rng.RandfRange(-jag, jag);
			}

			Ribbon(points, side, Width * 2.6f, glow);
			Ribbon(points, lift, Width * 2.6f, glow);
			Ribbon(points, side, Width, core);
			Ribbon(points, lift, Width, core);

			// Um galho curto em metade dos raios.
			if (_rng.Randf() < 0.5f)
			{
				int from = _rng.RandiRange(2, Segments - 2);
				Vector3 fork = (dir + side * _rng.RandfRange(-0.9f, 0.9f) + lift * _rng.RandfRange(-0.9f, 0.9f)).Normalized();
				var branch = new Vector3[4];
				for (int i = 0; i < 4; i++)
					branch[i] = points[from] + fork * (length * 0.1f * i)
						+ side * _rng.RandfRange(-0.04f, 0.04f) * i;
				Ribbon(branch, side, Width * 0.7f, core);
				Ribbon(branch, lift, Width * 0.7f, core);
			}
		}

		_mesh.SurfaceEnd();
	}

	/// <summary>Uma fita ao longo dos pontos, afilando até a ponta.</summary>
	private void Ribbon(Vector3[] points, Vector3 across, float width, Color color)
	{
		for (int i = 0; i < points.Length - 1; i++)
		{
			float w0 = width * (1f - i / (float)points.Length) * 0.5f;
			float w1 = width * (1f - (i + 1) / (float)points.Length) * 0.5f;
			Vector3 a = points[i] - across * w0, b = points[i] + across * w0;
			Vector3 c = points[i + 1] - across * w1, d = points[i + 1] + across * w1;

			foreach (Vector3 v in new[] { a, b, c, b, d, c })
			{
				_mesh.SurfaceSetColor(color);
				_mesh.SurfaceAddVertex(v);
			}
		}
	}
}
