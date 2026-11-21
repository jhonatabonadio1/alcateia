using Godot;

/// <summary>
/// A explosão da granada. Refeita em 20/11 (pedido do Jhon: "explosão mais
/// luminosa, não precisa de tanta partícula, só um efeito de luz e explosão
/// já basta"). A de 18/11 eram sete camadas com quase duzentas partículas
/// simuladas; esta é luz e forma, em seis peças, cada uma com o seu tempo:
///
/// - a **luz**: um clarão forte que acende o lugar inteiro e faz sombra
///   (pico em 34, cai em décimos de segundo e fica um resto quente até
///   0,75 s);
/// - o **clarão**: um disco de luz virado para a câmera, que abre e some
///   em 0,16 s;
/// - a **bola de fogo**: uma esfera de superfície revolta (o desenho é do
///   material, <c>explosao_fogo.gdshader</c>) que nasce branca de tão
///   quente, vira amarela, laranja e vermelho-escura enquanto incha e sobe,
///   e se desfaz em buracos — com um miolo menor, mais claro, por dentro;
/// - o **chão**: a onda de choque (um anel fino de luz que corre até o raio
///   da granada — mostra até onde ela pega) e a mancha de calor no meio;
/// - a **fumaça**: a mesma esfera, escura, que sobe e abre por 1,5 s;
/// - as **fagulhas**: vinte e oito pontos quentes que caem, quicam uma vez
///   e apagam (eram noventa, mais as línguas de fogo, as bolas de fumaça e
///   os estilhaços).
///
/// Os tempos e as medidas saíram de fotos tiradas quadro a quadro, de dia e
/// de noite.
/// </summary>
public partial class Explosion : Node3D
{
	/// <summary>O raio da granada, para a onda de choque (m).</summary>
	public float Radius { get; set; } = 6.5f;

	/// <summary>Explosões vivas agora. Para os testes.</summary>
	public static int Alive { get; private set; }

	private const string FireShader = "res://assets/efeitos/explosao_fogo.gdshader";
	private const string SmokeShader = "res://assets/efeitos/explosao_fumaca.gdshader";
	private const string GroundShader = "res://assets/efeitos/explosao_chao.gdshader";

	/// <summary>Quanto dura cada peça, em segundos.</summary>
	private const float LightTime = 0.75f, FlashTime = 0.16f, FireTime = 0.62f, CoreTime = 0.28f,
		SmokeStart = 0.1f, SmokeTime = 1.5f, GroundTime = 0.9f, TotalTime = 1.7f;

	private struct Bit
	{
		public Vector3 At, Velocity;
		public float Age, Life, Size;
		public Color Color;
		public bool Bounced;
	}

	private const int Sparks = 28;

	private readonly Bit[] _sparks = new Bit[Sparks];
	private MultiMesh _sparkMesh;
	private readonly RandomNumberGenerator _rng = new();
	private float _age, _floorY;

	private OmniLight3D _light;
	private MeshInstance3D _fire, _core, _smoke, _ground, _flash;
	private ShaderMaterial _fireMaterial, _coreMaterial, _smokeMaterial, _groundMaterial;
	private StandardMaterial3D _flashMaterial;

	public static Explosion Spawn(Node parent, Vector3 at, float radius)
	{
		var boom = new Explosion { Radius = radius };
		parent.AddChild(boom);
		boom.GlobalPosition = at;
		boom.Start();
		return boom;
	}

	public override void _EnterTree() => Alive++;
	public override void _ExitTree() => Alive--;

	private void Start()
	{
		_rng.Randomize();
		_floorY = GlobalPosition.Y - 0.12f;
		float seed = _rng.RandfRange(0f, 40f);

		// ---- a luz: acende o lugar e faz sombra (um mapa de sombra só,
		// dos baratos, por menos de um segundo)
		_light = new OmniLight3D
		{
			LightColor = new Color(1f, 0.72f, 0.38f),
			OmniRange = Radius * 3.2f,
			OmniAttenuation = 0.7f,
			ShadowEnabled = true,
			OmniShadowMode = OmniLight3D.ShadowMode.DualParaboloid,
			Position = new Vector3(0f, 1.2f, 0f)
		};
		AddChild(_light);

		// ---- a bola de fogo, o miolo e a fumaça: a mesma esfera
		var sphere = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 40, Rings = 20 };
		_fire = Layer(sphere, FireShader, seed, out _fireMaterial);
		_core = Layer(sphere, FireShader, seed + 5.4f, out _coreMaterial);
		_coreMaterial?.SetShaderParameter("forca", 1.8f);
		_smoke = Layer(sphere, SmokeShader, seed + 2.1f, out _smokeMaterial);
		_smoke.Visible = false;

		// ---- o chão: a onda de choque e a mancha de calor
		_ground = Layer(new QuadMesh { Size = new Vector2(2f, 2f), Orientation = PlaneMesh.OrientationEnum.Y }, GroundShader, seed, out _groundMaterial);
		_ground.Scale = new Vector3(Radius, 1f, Radius);
		_ground.Position = new Vector3(0f, -0.08f, 0f);

		// ---- o clarão: um disco de luz virado para a câmera
		var gradient = new Gradient();
		gradient.SetColor(0, Colors.White);
		gradient.SetColor(1, new Color(1, 1, 1, 0));
		gradient.SetOffset(0, 0f);
		_flashMaterial = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoTexture = new GradientTexture2D
			{
				Gradient = gradient, Width = 96, Height = 96,
				Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f)
			},
			AlbedoColor = new Color(1f, 0.92f, 0.75f, 1f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
			BillboardKeepScale = true,
			NoDepthTest = true
		};
		_flash = new MeshInstance3D
		{
			Mesh = new QuadMesh { Size = Vector2.One },
			MaterialOverride = _flashMaterial,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Position = new Vector3(0f, 1f, 0f)
		};
		AddChild(_flash);

		// ---- as fagulhas
		_sparkMesh = Cloud(Sparks);
		for (int i = 0; i < Sparks; i++)
		{
			Vector3 dir = Sphere(0.15f);
			_sparks[i] = new Bit
			{
				At = dir * 0.2f, Velocity = dir * _rng.RandfRange(6f, 18f) + Vector3.Up * _rng.RandfRange(1f, 5f),
				Life = _rng.RandfRange(0.45f, 1.0f), Size = _rng.RandfRange(0.07f, 0.15f),
				Color = new Color(1f, _rng.RandfRange(0.6f, 0.95f), _rng.RandfRange(0.15f, 0.5f))
			};
		}

		Pose(0f);
	}

	/// <summary>Uma peça da explosão: a malha com o material do arquivo
	/// (se o arquivo faltar, a peça fica sem material — aparece, feia, em
	/// vez de derrubar a partida).</summary>
	private MeshInstance3D Layer(Mesh mesh, string shaderPath, float seed, out ShaderMaterial material)
	{
		material = null;
		if (ResourceLoader.Exists(shaderPath))
		{
			material = new ShaderMaterial { Shader = GD.Load<Shader>(shaderPath) };
			material.SetShaderParameter("semente", seed);
			material.SetShaderParameter("vida", 0f);
		}
		var layer = new MeshInstance3D
		{
			Mesh = mesh,
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Visible = material != null
		};
		AddChild(layer);
		return layer;
	}

	/// <summary>Cada peça no instante <paramref name="t"/> (segundos desde
	/// o estouro): o tamanho, o lugar, a força e a "vida" que o material
	/// de cada uma desenha.</summary>
	private void Pose(float t)
	{
		float scale = Radius;

		// A luz: o pico que cai em décimos de segundo, e o resto quente.
		if (_light != null)
		{
			_light.LightEnergy = t > LightTime ? 0f : 34f * Mathf.Exp(-t * 16f) + 9f * Mathf.Pow(1f - t / LightTime, 2f);
			_light.Visible = t <= LightTime;
		}

		float fire = Mathf.Clamp(t / FireTime, 0f, 1f), grown = 1f - Mathf.Pow(1f - fire, 3f);
		if (_fireMaterial != null)
		{
			_fire.Visible = fire < 1f;
			_fire.Scale = Vector3.One * scale * (0.16f + 0.42f * grown);
			_fire.Position = new Vector3(0f, 0.5f + 1.5f * grown, 0f);
			_fireMaterial.SetShaderParameter("vida", fire);
		}

		float core = Mathf.Clamp(t / CoreTime, 0f, 1f);
		if (_coreMaterial != null)
		{
			_core.Visible = core < 1f;
			_core.Scale = Vector3.One * scale * (0.10f + 0.22f * (1f - Mathf.Pow(1f - core, 2f)));
			_core.Position = new Vector3(0f, 0.6f + 0.6f * core, 0f);
			_coreMaterial.SetShaderParameter("vida", core * 0.75f);
		}

		float smoke = Mathf.Clamp((t - SmokeStart) / SmokeTime, 0f, 1f);
		if (_smokeMaterial != null)
		{
			_smoke.Visible = t > SmokeStart && smoke < 1f;
			_smoke.Scale = Vector3.One * scale * (0.30f + 0.34f * Mathf.Sqrt(smoke));
			_smoke.Position = new Vector3(0f, 1.4f + 2.6f * smoke, 0f);
			_smokeMaterial.SetShaderParameter("vida", smoke);
		}

		float ground = Mathf.Clamp(t / GroundTime, 0f, 1f);
		if (_groundMaterial != null)
		{
			_ground.Visible = ground < 1f;
			_groundMaterial.SetShaderParameter("vida", ground);
		}

		float flash = Mathf.Clamp(t / FlashTime, 0f, 1f);
		if (_flash != null)
		{
			_flash.Visible = flash < 1f;
			_flash.Scale = Vector3.One * scale * (1f + 1.6f * flash);
			_flashMaterial.AlbedoColor = new Color(1f, 0.92f, 0.75f, Mathf.Pow(1f - flash, 1.5f));
		}
	}

	/// <summary>Uma direção qualquer, com o mínimo de "para cima" pedido
	/// (a explosão é no chão: metade de baixo não existe).</summary>
	private Vector3 Sphere(float minUp)
	{
		var dir = new Vector3(_rng.Randfn(), _rng.Randfn(), _rng.Randfn()).Normalized();
		dir.Y = Mathf.Abs(dir.Y) * (1f - minUp) + minUp;
		return dir.Normalized();
	}

	/// <summary>As fagulhas, desenhadas de uma vez (MultiMesh): pontos de
	/// luz virados para a câmera.</summary>
	private MultiMesh Cloud(int count)
	{
		var gradient = new Gradient();
		gradient.SetColor(0, Colors.White);
		gradient.SetColor(1, new Color(1, 1, 1, 0));
		gradient.SetOffset(0, 0.55f);
		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoTexture = new GradientTexture2D
			{
				Gradient = gradient, Width = 48, Height = 48,
				Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f)
			},
			VertexColorUseAsAlbedo = true,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
			BillboardKeepScale = true,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add
		};
		var mesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseColors = true,
			Mesh = new QuadMesh { Size = Vector2.One, Material = material },
			InstanceCount = count,
			VisibleInstanceCount = count
		};
		AddChild(new MultiMeshInstance3D
		{
			Multimesh = mesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			CustomAabb = new Aabb(Vector3.One * -40f, Vector3.One * 80f)
		});
		return mesh;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_age += dt;

		Pose(_age);
		StepSparks(dt);

		if (_age > TotalTime) QueueFree();
	}

	private void StepSparks(float dt)
	{
		if (_sparkMesh == null) return;
		float floor = _floorY - GlobalPosition.Y;
		for (int i = 0; i < _sparks.Length; i++)
		{
			Bit b = _sparks[i];
			b.Age += dt;
			float t = Mathf.Clamp(b.Age / b.Life, 0f, 1f);
			b.Velocity += Vector3.Down * 14f * dt;
			b.Velocity *= 1f - Mathf.Min(1.6f * dt, 0.9f);
			b.At += b.Velocity * dt;
			if (b.At.Y < floor)
			{
				b.At.Y = floor;
				b.Velocity = b.Bounced ? Vector3.Zero : new Vector3(b.Velocity.X * 0.5f, -b.Velocity.Y * 0.35f, b.Velocity.Z * 0.5f);
				b.Bounced = true;
			}
			_sparks[i] = b;

			float size = b.Size * (t >= 1f ? 0f : 1f);
			Color color = b.Color;
			color.A *= 1f - Mathf.Pow(t, 2.2f);
			_sparkMesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * Mathf.Max(size, 0.0001f)), b.At));
			_sparkMesh.SetInstanceColor(i, color);
		}
	}
}
