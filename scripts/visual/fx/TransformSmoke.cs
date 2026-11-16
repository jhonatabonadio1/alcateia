using Godot;

/// <summary>
/// A fumaça da transformação: uma nuvem que nasce em volta do corpo no
/// começo da transformação, fica mais densa na metade — quando o corpo
/// troca (<see cref="FormController.ShowsWolf"/>) — e se desfaz. Um
/// clarão curto, na cor do lobo, marca a troca.
///
/// A duração vem do tempo da transformação (FormController.ShiftDuration,
/// ShiftBackDuration): a fumaça também mostra a janela em que o corpo
/// leva mais dano.
///
/// Montada por código para não depender de cena; a textura é
/// assets/efeitos/fumaca.png (4×4 quadros).
/// </summary>
public partial class TransformSmoke : Node3D
{
	public const string TexturePath = "res://assets/efeitos/fumaca.png";

	/// <summary>Quantas nuvens, na qualidade Alta (as outras usam menos).
	/// Pouca e transparente (pedido do Jhon, 16/11): ela vela a troca do
	/// corpo, não a esconde por inteiro.</summary>
	[Export] public int Amount = 16;

	private GpuParticles3D _smoke;
	private OmniLight3D _flash;
	private ParticleProcessMaterial _process;

	public override void _Ready()
	{
		_process = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 0.7f,
			Direction = Vector3.Up,
			Spread = 180f,
			InitialVelocityMin = 0.4f,
			InitialVelocityMax = 1.4f,
			Gravity = new Vector3(0, 0.35f, 0),
			DampingMin = 1.2f,
			DampingMax = 2.0f,
			ScaleMin = 0.9f,
			ScaleMax = 1.7f,
			AngleMin = -180f,
			AngleMax = 180f,
			AngularVelocityMin = -25f,
			AngularVelocityMax = 25f,
			AnimSpeedMin = 1f,
			AnimSpeedMax = 1f,
		};
		_process.ScaleCurve = new CurveTexture { Curve = Ramp(0.6f, 1f, 1.5f) };

		// Alfa: entra rápido (cobre a troca), segura e sai.
		var colors = new Gradient();
		colors.SetColor(0, new Color(1, 1, 1, 0f));
		colors.SetColor(1, new Color(1, 1, 1, 0f));
		colors.AddPoint(0.08f, new Color(1, 1, 1, 0.45f));
		colors.AddPoint(0.5f, new Color(1, 1, 1, 0.3f));
		_process.ColorRamp = new GradientTexture1D { Gradient = colors };

		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			VertexColorUseAsAlbedo = true,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
			BillboardKeepScale = true,
			ParticlesAnimHFrames = 4,
			ParticlesAnimVFrames = 4,
			ParticlesAnimLoop = false,
			AlbedoTexture = ResourceLoader.Exists(TexturePath) ? GD.Load<Texture2D>(TexturePath) : null,
			AlbedoColor = new Color(0.45f, 0.44f, 0.46f),
			// Some perto da câmera em vez de cortar reto (a câmera do lobo
			// fica dentro). Sem o "proximity fade": ele apagava a fumaça
			// justo onde ela encosta no corpo — o corpo trocando aparecia.
			DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelAlpha,
			DistanceFadeMinDistance = 0.2f,
			DistanceFadeMaxDistance = 1.2f,
		};

		_smoke = new GpuParticles3D
		{
			Name = "Fumaca",
			Emitting = false,
			OneShot = true,
			Explosiveness = 0.9f,
			Randomness = 0.5f,
			LocalCoords = false,
			Amount = Amount,
			ProcessMaterial = _process,
			DrawPass1 = new QuadMesh { Size = new Vector2(1.4f, 1.4f), Material = material },
			VisibilityAabb = new Aabb(new Vector3(-3, -1, -3), new Vector3(6, 5, 6)),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Position = new Vector3(0, 0f, 0),
		};
		AddChild(_smoke);

		_flash = new OmniLight3D
		{
			Name = "Clarao",
			LightEnergy = 0f,
			OmniRange = 3.5f,
			ShadowEnabled = false,
			Visible = false,
			Position = new Vector3(0, 0.3f, 0),
		};
		AddChild(_flash);
	}

	private static Curve Ramp(float a, float b, float c)
	{
		var curve = new Curve();
		curve.AddPoint(new Vector2(0, a));
		curve.AddPoint(new Vector2(0.5f, b));
		curve.AddPoint(new Vector2(1, c));
		return curve;
	}

	/// <summary>Solta a nuvem: <paramref name="duration"/> é o tempo da
	/// transformação; a fumaça dura o dobro (some depois do corpo novo
	/// aparecer). <paramref name="ratio"/> vem da qualidade.</summary>
	public void Play(float duration, Color tint, float ratio)
	{
		if (_smoke == null) return;
		float life = Mathf.Max(duration * 2.2f, 0.5f);
		_smoke.Lifetime = life;
		_smoke.AmountRatio = Mathf.Clamp(ratio, 0.05f, 1f);
		_smoke.Restart();
		_smoke.Emitting = true;
		Flash(duration * 0.5f, tint);
	}

	/// <summary>O clarão na troca do corpo, depois de <paramref name="delay"/> segundos.</summary>
	private async void Flash(float delay, Color tint)
	{
		if (_flash == null) return;
		if (delay > 0f) await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(_flash)) return;
		_flash.LightColor = tint;
		_flash.Visible = true;
		_flash.LightEnergy = 1.5f;
		Tween tween = CreateTween();
		tween.TweenProperty(_flash, "light_energy", 0f, 0.25f);
		tween.TweenCallback(Callable.From(() => _flash.Visible = false));
	}

	public bool IsPlaying => _smoke != null && _smoke.Emitting;
}
