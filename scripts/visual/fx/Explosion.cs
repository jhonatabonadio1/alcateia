using Godot;

/// <summary>
/// A explosão da granada (pedido do Jhon, 18/11: "insira explosão de
/// granada avançada, com partículas"). No lugar da esfera clara que
/// inchava, sete camadas curtas, cada uma com o seu tempo:
///
/// - o **clarão**: uma luz que acende o lugar e apaga em 0,35 s;
/// - o **núcleo**: branco, incha e some em 0,1 s;
/// - o **fogo**: dezenas de línguas que nascem quase brancas, viram
///   amarelas, laranja e vermelho-escuras enquanto incham e sobem ("deve
///   ser fogo, explosão mesmo" — a primeira versão era uma cúpula laranja
///   transparente);
/// - a **onda de choque**: um anel rente ao chão até o raio da granada —
///   mostra até onde ela pega;
/// - as **fagulhas**: dezenas de pontos quentes jogados para todo lado,
///   que caem com a gravidade, quicam uma vez e apagam;
/// - a **fumaça**: bolas escuras e macias que sobem e abrem por 1,6 s;
/// - os **estilhaços**: pedaços escuros maiores, poucos, que voam longe.
///
/// As partículas são simuladas aqui (são menos de duzentas, por meio
/// segundo) e desenhadas de uma vez cada tipo (MultiMesh), como a poeira
/// da corrida (<see cref="SprintDust"/>).
/// </summary>
public partial class Explosion : Node3D
{
	/// <summary>O raio da granada, para a onda de choque (m).</summary>
	public float Radius { get; set; } = 6.5f;

	/// <summary>Explosões vivas agora. Para os testes.</summary>
	public static int Alive { get; private set; }

	private struct Bit
	{
		public Vector3 At, Velocity;
		public float Age, Life, Size;
		public Color Color;
		public bool Bounced;
	}

	private const int Sparks = 90, Puffs = 26, Shards = 16, Flames = 46;

	private readonly Bit[] _sparks = new Bit[Sparks], _puffs = new Bit[Puffs], _shards = new Bit[Shards], _fire = new Bit[Flames];
	private MultiMesh _sparkMesh, _puffMesh, _shardMesh, _fireMesh;
	private readonly RandomNumberGenerator _rng = new();
	private float _age, _floorY;

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

		// ---- o clarão
		var light = new OmniLight3D { LightColor = new Color(1f, 0.78f, 0.45f), LightEnergy = 9f, OmniRange = Radius * 2.2f, ShadowEnabled = false };
		AddChild(light);
		Tween glow = light.CreateTween();
		glow.TweenProperty(light, "light_energy", 0f, 0.35).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
		glow.TweenCallback(Callable.From(light.QueueFree));

		// ---- o núcleo e a bola de fogo
		Ball(new Color(1f, 0.97f, 0.85f, 1f), Radius * 0.2f, 0.1f, 0.35f, false);

		// ---- a onda de choque
		var ringMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(1f, 0.92f, 0.75f, 0.7f),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		var ring = new MeshInstance3D
		{
			Mesh = new TorusMesh { InnerRadius = 0.93f, OuterRadius = 1f, Rings = 40, RingSegments = 6 },
			MaterialOverride = ringMaterial,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Scale = new Vector3(0.3f, 0.12f, 0.3f)
		};
		AddChild(ring);
		Tween wave = ring.CreateTween().SetParallel();
		wave.TweenProperty(ring, "scale", new Vector3(Radius, 0.05f, Radius), 0.32).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		wave.TweenProperty(ringMaterial, "albedo_color:a", 0f, 0.32).SetEase(Tween.EaseType.In);
		wave.Chain().TweenCallback(Callable.From(ring.QueueFree));

		// ---- as partículas
		// ---- o fogo: línguas claras que incham, sobem e escurecem
		_fireMesh = Cloud(Flames, soft: true, lit: true);
		for (int i = 0; i < Flames; i++)
		{
			Vector3 dir = Sphere(0.2f);
			float reach = _rng.RandfRange(0.2f, 1f);
			_fire[i] = new Bit
			{
				At = dir * reach * 0.5f, Velocity = dir * (2f + 9f * reach) + Vector3.Up * _rng.RandfRange(1.5f, 4.5f),
				Life = _rng.RandfRange(0.38f, 0.8f), Size = _rng.RandfRange(0.9f, 1.9f) * (Radius / 6.5f),
				Color = Colors.White
			};
		}

		_sparkMesh = Cloud(Sparks, soft: false, lit: true);
		_puffMesh = Cloud(Puffs, soft: true, lit: false);
		_shardMesh = Cloud(Shards, soft: false, lit: false);

		for (int i = 0; i < Sparks; i++)
		{
			Vector3 dir = Sphere(0.15f);
			_sparks[i] = new Bit
			{
				At = dir * 0.2f, Velocity = dir * _rng.RandfRange(5f, 17f) + Vector3.Up * _rng.RandfRange(1f, 5f),
				Life = _rng.RandfRange(0.4f, 1.0f), Size = _rng.RandfRange(0.06f, 0.13f),
				Color = new Color(1f, _rng.RandfRange(0.55f, 0.95f), _rng.RandfRange(0.1f, 0.5f))
			};
		}
		for (int i = 0; i < Puffs; i++)
		{
			Vector3 dir = Sphere(0.05f);
			float gray = _rng.RandfRange(0.1f, 0.26f);
			_puffs[i] = new Bit
			{
				At = dir * _rng.RandfRange(0.2f, 0.9f), Velocity = dir * _rng.RandfRange(0.8f, 3.2f) + Vector3.Up * _rng.RandfRange(0.6f, 2.2f),
				Life = _rng.RandfRange(1.1f, 1.9f), Size = _rng.RandfRange(1.1f, 2.3f),
				Color = new Color(gray, gray * 0.96f, gray * 0.9f, _rng.RandfRange(0.6f, 0.85f))
			};
		}
		for (int i = 0; i < Shards; i++)
		{
			Vector3 dir = Sphere(0.25f);
			_shards[i] = new Bit
			{
				At = dir * 0.2f, Velocity = dir * _rng.RandfRange(6f, 13f) + Vector3.Up * _rng.RandfRange(2f, 6f),
				Life = _rng.RandfRange(0.7f, 1.3f), Size = _rng.RandfRange(0.07f, 0.14f),
				Color = new Color(0.1f, 0.09f, 0.08f)
			};
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

	private void Ball(Color color, float size, float time, float start, bool darken)
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
		var ball = new MeshInstance3D
		{
			Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 16, Rings = 8 },
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Scale = Vector3.One * size * start
		};
		AddChild(ball);
		Tween grow = ball.CreateTween().SetParallel();
		grow.TweenProperty(ball, "scale", Vector3.One * size, time).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		grow.TweenProperty(material, "albedo_color:a", 0f, time).SetEase(Tween.EaseType.In);
		if (darken) grow.TweenProperty(material, "albedo_color", new Color(0.25f, 0.08f, 0.02f, 0f), time);
		grow.Chain().TweenCallback(Callable.From(ball.QueueFree));
	}

	private MultiMesh Cloud(int count, bool soft, bool lit)
	{
		var gradient = new Gradient();
		gradient.SetColor(0, Colors.White);
		gradient.SetColor(1, new Color(1, 1, 1, 0));
		gradient.SetOffset(0, soft ? 0.05f : 0.55f);
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
			BlendMode = lit ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix
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

		StepFire(dt);
		Step(_sparks, _sparkMesh, dt, gravity: 14f, drag: 1.6f, bounce: true, grow: 0f);
		Step(_puffs, _puffMesh, dt, gravity: -0.5f, drag: 2.4f, bounce: false, grow: 1.1f);
		Step(_shards, _shardMesh, dt, gravity: 15f, drag: 0.6f, bounce: true, grow: 0f);

		if (_age > 2f) QueueFree();
	}

	/// <summary>O fogo: cada língua nasce quase branca, vira amarela,
	/// laranja e vermelho-escura enquanto incha e sobe, e apaga — a fumaça
	/// fica no lugar dela.</summary>
	private void StepFire(float dt)
	{
		if (_fireMesh == null) return;
		for (int i = 0; i < _fire.Length; i++)
		{
			Bit b = _fire[i];
			b.Age += dt;
			float t = Mathf.Clamp(b.Age / b.Life, 0f, 1f);
			b.Velocity *= 1f - Mathf.Min(5.5f * dt, 0.9f);
			b.Velocity += Vector3.Up * 3.2f * dt;
			b.At += b.Velocity * dt;
			_fire[i] = b;

			Color color = t < 0.18f ? new Color(1f, 0.96f, 0.72f).Lerp(new Color(1f, 0.74f, 0.2f), t / 0.18f)
				: t < 0.55f ? new Color(1f, 0.74f, 0.2f).Lerp(new Color(1f, 0.36f, 0.05f), (t - 0.18f) / 0.37f)
				: new Color(1f, 0.36f, 0.05f).Lerp(new Color(0.35f, 0.05f, 0.01f), (t - 0.55f) / 0.45f);
			color.A = t < 0.08f ? t / 0.08f : 1f - Mathf.Pow(t, 1.8f);
			float size = b.Size * (0.45f + 1.25f * Mathf.Sqrt(t)) * (t >= 1f ? 0f : 1f);
			_fireMesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * Mathf.Max(size, 0.0001f)), b.At));
			_fireMesh.SetInstanceColor(i, color);
		}
	}

	private void Step(Bit[] bits, MultiMesh mesh, float dt, float gravity, float drag, bool bounce, float grow)
	{
		if (mesh == null) return;
		float floor = _floorY - GlobalPosition.Y;
		for (int i = 0; i < bits.Length; i++)
		{
			Bit b = bits[i];
			b.Age += dt;
			float t = Mathf.Clamp(b.Age / b.Life, 0f, 1f);
			b.Velocity += Vector3.Down * gravity * dt;
			b.Velocity *= 1f - Mathf.Min(drag * dt, 0.9f);
			b.At += b.Velocity * dt;
			if (bounce && b.At.Y < floor)
			{
				b.At.Y = floor;
				b.Velocity = b.Bounced ? Vector3.Zero : new Vector3(b.Velocity.X * 0.5f, -b.Velocity.Y * 0.35f, b.Velocity.Z * 0.5f);
				b.Bounced = true;
			}
			bits[i] = b;

			float size = b.Size * (1f + grow * t) * (t >= 1f ? 0f : 1f);
			Color color = b.Color;
			color.A *= t < 0.25f && grow > 0f ? t / 0.25f : 1f - Mathf.Pow(t, grow > 0f ? 1.4f : 2.2f);
			mesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * Mathf.Max(size, 0.0001f)), b.At));
			mesh.SetInstanceColor(i, color);
		}
	}
}
