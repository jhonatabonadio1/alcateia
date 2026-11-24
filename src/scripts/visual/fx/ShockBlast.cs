using Godot;

/// <summary>
/// O estouro da granada de choque (pedido do Jhon, 18/11: "a granada de
/// choque deve ter uma explosão de choque, que desacelera quem estiver
/// perto e atordoa o inimigo"). Elétrico, azul-branco — nada de fogo:
///
/// - o **clarão** frio, que acende o lugar e apaga em 0,3 s;
/// - o **núcleo** branco-azulado, que incha e some em 0,12 s;
/// - **raios** quebrados saindo do ponto para todos os lados e rente ao
///   chão (os mesmos do especial do lobo, <see cref="LightningBurst"/>, em
///   azul), em duas levas;
/// - a **onda de choque**: dois anéis azuis rente ao chão que abrem até o
///   raio da granada — mostram até onde ela atordoa;
/// - **faíscas** azuis e brancas pulando por meio segundo.
/// </summary>
public partial class ShockBlast : Node3D
{
	public float Radius { get; set; } = 6.5f;

	/// <summary>Estouros vivos agora. Para os testes.</summary>
	public static int Alive { get; private set; }

	private static readonly Color Blue = new(0.35f, 0.75f, 1f);

	private struct Spark
	{
		public Vector3 At, Velocity;
		public float Age, Life, Size;
		public Color Color;
	}

	private const int Count = 70;
	private readonly Spark[] _sparks = new Spark[Count];
	private MultiMesh _mesh;
	private readonly RandomNumberGenerator _rng = new();
	private float _age;

	public static ShockBlast Spawn(Node parent, Vector3 at, float radius)
	{
		var blast = new ShockBlast { Radius = radius };
		parent.AddChild(blast);
		blast.GlobalPosition = at;
		blast.Start();
		return blast;
	}

	public override void _EnterTree() => Alive++;
	public override void _ExitTree() => Alive--;

	private void Start()
	{
		_rng.Randomize();

		var light = new OmniLight3D { LightColor = new Color(0.6f, 0.85f, 1f), LightEnergy = 8f, OmniRange = Radius * 2f, ShadowEnabled = false };
		AddChild(light);
		Tween glow = light.CreateTween();
		glow.TweenProperty(light, "light_energy", 0f, 0.3).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
		glow.TweenCallback(Callable.From(light.QueueFree));

		// O núcleo.
		var coreMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.85f, 0.95f, 1f, 0.95f),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
		var core = new MeshInstance3D
		{
			Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 14, Rings = 7 },
			MaterialOverride = coreMaterial,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Scale = Vector3.One * 0.25f
		};
		AddChild(core);
		Tween pop = core.CreateTween().SetParallel();
		pop.TweenProperty(core, "scale", Vector3.One * Radius * 0.16f, 0.12).SetEase(Tween.EaseType.Out);
		pop.TweenProperty(coreMaterial, "albedo_color:a", 0f, 0.12);
		pop.Chain().TweenCallback(Callable.From(core.QueueFree));

		// Os raios: para todos os lados e rente ao chão; a segunda leva um instante depois.
		Bolts(false, 2.6f);
		Bolts(true, Radius * 0.6f);
		GetTree().CreateTimer(0.12).Timeout += () =>
		{
			if (IsInstanceValid(this)) Bolts(true, Radius * 0.85f);
		};

		// A onda: dois anéis.
		Ring(Radius, 0.34f, 0.75f);
		Ring(Radius * 0.6f, 0.26f, 0.5f);

		// As faíscas.
		var gradient = new Gradient();
		gradient.SetColor(0, Colors.White);
		gradient.SetColor(1, new Color(1, 1, 1, 0));
		gradient.SetOffset(0, 0.4f);
		_mesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseColors = true,
			Mesh = new QuadMesh
			{
				Size = Vector2.One,
				Material = new StandardMaterial3D
				{
					ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
					AlbedoTexture = new GradientTexture2D
					{
						Gradient = gradient, Width = 32, Height = 32,
						Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f)
					},
					VertexColorUseAsAlbedo = true,
					Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
					BlendMode = BaseMaterial3D.BlendModeEnum.Add,
					BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
					BillboardKeepScale = true
				}
			},
			InstanceCount = Count,
			VisibleInstanceCount = Count
		};
		AddChild(new MultiMeshInstance3D
		{
			Multimesh = _mesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			CustomAabb = new Aabb(Vector3.One * -30f, Vector3.One * 60f)
		});
		for (int i = 0; i < Count; i++)
		{
			var dir = new Vector3(_rng.Randfn(), Mathf.Abs(_rng.Randfn()) * 0.6f + 0.1f, _rng.Randfn()).Normalized();
			_sparks[i] = new Spark
			{
				At = dir * 0.2f, Velocity = dir * _rng.RandfRange(4f, 15f),
				Life = _rng.RandfRange(0.25f, 0.65f), Size = _rng.RandfRange(0.05f, 0.11f),
				Color = _rng.Randf() < 0.4f ? Colors.White : Blue.Lightened(_rng.RandfRange(0f, 0.4f))
			};
		}
	}

	private void Bolts(bool flat, float radius)
	{
		// Montado antes de entrar na cena: o estouro reserva os raios ao nascer.
		AddChild(new LightningBurst
		{
			Tint = Blue,
			Flat = flat,
			Position = flat ? Vector3.Up * 0.12f : Vector3.Up * 0.4f,
			Bolts = flat ? 18 : 14,
			Radius = radius,
			Life = 0.55f
		});
	}

	private void Ring(float radius, float time, float alpha)
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = new Color(Blue.Lightened(0.3f), alpha),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		var ring = new MeshInstance3D
		{
			Mesh = new TorusMesh { InnerRadius = 0.94f, OuterRadius = 1f, Rings = 40, RingSegments = 6 },
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Scale = new Vector3(0.3f, 0.1f, 0.3f)
		};
		AddChild(ring);
		Tween wave = ring.CreateTween().SetParallel();
		wave.TweenProperty(ring, "scale", new Vector3(radius, 0.05f, radius), time).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		wave.TweenProperty(material, "albedo_color:a", 0f, time).SetEase(Tween.EaseType.In);
		wave.Chain().TweenCallback(Callable.From(ring.QueueFree));
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_age += dt;
		if (_mesh != null)
			for (int i = 0; i < Count; i++)
			{
				Spark s = _sparks[i];
				s.Age += dt;
				float t = Mathf.Clamp(s.Age / s.Life, 0f, 1f);
				s.Velocity += Vector3.Down * 9f * dt;
				s.Velocity *= 1f - Mathf.Min(3.5f * dt, 0.9f);
				s.At += s.Velocity * dt;
				if (s.At.Y < -0.1f) { s.At.Y = -0.1f; s.Velocity = Vector3.Zero; }
				_sparks[i] = s;
				Color color = s.Color;
				// Pisca, como faísca elétrica.
				color.A = (1f - t * t) * (0.55f + 0.45f * Mathf.Sin(_age * 60f + i));
				_mesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * (t >= 1f ? 0.0001f : s.Size)), s.At));
				_mesh.SetInstanceColor(i, color);
			}

		if (_age > 1.2f) QueueFree();
	}
}
