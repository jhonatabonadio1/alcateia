using Godot;

/// <summary>
/// O estouro de raios do especial do lobo: relâmpagos quebrados que saem
/// do corpo para todos os lados, tremem e somem em meio segundo, com um
/// clarão. Quando o especial dispara (em volta do corpo) e quando o lobo
/// cai (rente ao chão, mais largo, com uma onda de choque).
///
/// Refeito em 18/11 (pedido do Jhon: "FX melhores"): cada raio é uma fita
/// de neon de frente para a câmera — clarão macio na cor, miolo branco —
/// com galhos, em vez das duas tiras duras em cruz. O desenho é refeito
/// várias vezes por segundo: é o que faz o raio tremer.
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
	private const double Flicker = 0.04;

	private const int Segments = 8;
	private const float Width = 0.05f;

	private ImmediateMesh _mesh;
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
			Bolts = flat ? 16 : 12,
			Radius = flat ? 3.4f : 2.4f,
			Life = flat ? 0.55f : 0.6f
		};
		parent.AddChild(burst);
		return burst;
	}

	public override void _Ready()
	{
		_rng.Randomize();
		_mesh = new ImmediateMesh();
		AddChild(FxRibbon.Canvas(_mesh, world: false));

		_flash = new OmniLight3D
		{
			LightColor = Tint.Lerp(Colors.White, 0.4f),
			LightEnergy = 6f,
			OmniRange = Radius * 2.2f,
			ShadowEnabled = false
		};
		AddChild(_flash);

		// Cada raio tem a sua direção; o caminho até lá é que muda.
		_directions = new Vector3[Bolts];
		for (int i = 0; i < Bolts; i++)
		{
			float angle = Mathf.Tau * (i + _rng.Randf() * 0.7f) / Bolts;
			float up = Flat ? _rng.RandfRange(0.02f, 0.2f) : _rng.RandfRange(-0.25f, 0.95f);
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
		_flash.LightEnergy = 6f * Mathf.Max(0f, 1f - t * 2.2f);

		// A onda de choque anda todo quadro; os raios só tremem de tanto em tanto.
		if (_age >= _nextDraw)
		{
			_nextDraw = _age + Flicker;
			_seed = _rng.Randi();
		}
		Draw(1f - t * t, t);
	}

	private uint _seed;

	private void Draw(float strength, float t)
	{
		_mesh.ClearSurfaces();
		_mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

		Vector3 eye = GetViewport().GetCamera3D() is Camera3D camera ? ToLocal(camera.GlobalPosition) : new Vector3(0, 2, 6);
		var rng = new RandomNumberGenerator { Seed = _seed };

		// Os raios crescem para fora no começo.
		float reach = Radius * Mathf.Min(1f, 0.35f + t * 4f);

		for (int b = 0; b < Bolts; b++)
		{
			// Uns falham a cada desenho: relâmpago não é contínuo.
			if (rng.Randf() < 0.22f) continue;

			Vector3 dir = _directions[b];
			float length = reach * rng.RandfRange(0.55f, 1f);
			Vector3[] points = FxRibbon.Jagged(rng, dir * 0.2f, dir * (0.2f + length), Segments, length * 0.12f);
			FxRibbon.Bolt(_mesh, points, eye, Width, Tint, strength);

			// Um galho curto em metade dos raios.
			if (rng.Randf() < 0.55f)
			{
				int from = rng.RandiRange(2, Segments - 2);
				Vector3 fork = (dir + new Vector3(rng.RandfRange(-0.9f, 0.9f), rng.RandfRange(-0.4f, 0.9f), rng.RandfRange(-0.9f, 0.9f))).Normalized();
				Vector3[] branch = FxRibbon.Jagged(rng, points[from], points[from] + fork * length * 0.35f, 4, length * 0.05f);
				FxRibbon.Bolt(_mesh, branch, eye, Width * 0.6f, Tint, strength * 0.8f);
			}
		}

		// Na queda: a onda de choque abrindo pelo chão, e um segundo anel atrás.
		if (Flat)
		{
			float grow = 1f - Mathf.Pow(1f - t, 3f);
			var ring = new Color(Tint.R, Tint.G, Tint.B, 0.75f * (1f - t));
			FxRibbon.Ring(_mesh, Vector3.Up * 0.03f, Vector3.Up, Radius * 1.25f * grow, 0.5f * (1f - t * 0.5f), ring);
			var hot = new Color(1f, 0.96f, 0.92f, 0.8f * (1f - t));
			FxRibbon.Ring(_mesh, Vector3.Up * 0.04f, Vector3.Up, Radius * 1.25f * grow, 0.12f, hot);
			float late = Mathf.Clamp(t * 1.6f - 0.25f, 0f, 1f);
			if (late > 0f)
				FxRibbon.Ring(_mesh, Vector3.Up * 0.03f, Vector3.Up, Radius * 0.8f * (1f - Mathf.Pow(1f - late, 3f)), 0.3f,
					new Color(Tint.R, Tint.G, Tint.B, 0.4f * (1f - late)));
		}

		_mesh.SurfaceEnd();
	}
}
