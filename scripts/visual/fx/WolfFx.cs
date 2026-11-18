using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Os efeitos visuais do lobo (guia de efeitos do Jhon, 16/11):
///
/// - **Aura**: contorno de luz no corpo (assets/efeitos/aura.gdshader), no
///   rugido e carregando o salto — o lobo usando poder.
/// - **Ar de velocidade**: riscos de vento passando em volta do corpo
///   (partículas) e um brilho frio nas bordas (assets/efeitos/vento.gdshader).
///   Fraco correndo normal, cheio de quatro, no salto e no bote. Sutil e com
///   poucos riscos (pedido do Jhon, 16/11). Some ao frear.
/// - **Rastro das garras** (<see cref="ClawTrail"/>): feixe de luz na mão
///   que bate, na janela do golpe — garrada, salto e bote.
/// - **Fumaça da transformação** (<see cref="TransformSmoke"/>): o corpo
///   troca na metade dela (<see cref="FormController.ShowsWolf"/>).
/// - **Raios do especial** (<see cref="LightningBurst"/>): saem do corpo
///   quando o especial dispara e correm pelo chão quando o lobo cai.
///
/// **A cor é do personagem** (<see cref="CharacterSkin.EffectColor"/>);
/// sem personagem, a do time.
///
/// Tudo é cosmético e local: nada vai pela rede. Cada máquina desenha os
/// efeitos de cada lobo a partir do estado que já recebe (forma, golpe,
/// velocidade) — o mesmo que a animação usa. Por lobo, o que muda (cor,
/// quanto aparece) é <c>instance uniform</c>: dois lobos com o mesmo
/// material não se afetam.
///
/// Fica no corpo, ao lado do WolfVisual (que o cria), e não dentro dele:
/// a fumaça aparece com o lobo ainda escondido.
/// </summary>
public partial class WolfFx : Node3D
{
	public const string AuraPath = "res://assets/efeitos/aura.tres";
	public const string TrailPath = "res://assets/efeitos/rastro.tres";

	/// <summary>Os efeitos existem nesta máquina. Não no servidor dedicado
	/// (que nem carrega mapa).</summary>
	public static bool Enabled => !OS.HasFeature("dedicated_server");

	/// <summary>A partir de que fração da velocidade máxima (a de quatro) o
	/// vento aparece. 0,35: correndo normal (58% da máxima) já tem um pouco.</summary>
	[Export] public float AirFrom = 0.35f;

	/// <summary>Quanto a aura aparece no rugido e carregando o salto (0 a 1).</summary>
	[Export] public float PowerAura = 1f;

	/// <summary>Quanto a câmera do dono abre (graus) com o vento cheio.</summary>
	[Export] public float AirFovKick = 5f;

	/// <summary>Cores dos efeitos dos lobos de sempre, por time.</summary>
	public static readonly Color BlueEffect = new(0.35f, 0.75f, 1f);
	public static readonly Color OrangeEffect = new(1f, 0.5f, 0.15f);

	private CharacterBody3D _body;
	private FormController _form;
	private Weapon _weapon;
	private Health _health;
	private WolfVisual _visual;
	private Player _player;

	private readonly List<MeshInstance3D> _meshes = new();
	private Material _overlay;
	private bool _overlayOn;
	private int _warm = 3;
	private float _aura, _air;

	private ClawTrail _left, _right;
	private double _strikeLeft;
	private TransformSmoke _smoke;
	private GpuParticles3D _streaks;
	private ParticleProcessMaterial _streakProcess;

	private Camera3D _wolfCamera;
	private float _wolfCameraFov;

	/// <summary>Quanto o vento aparece agora (0 a 1). Para os testes.</summary>
	public float AirAmount => _air;

	/// <summary>Quanto a aura aparece agora (0 a 1). Para os testes.</summary>
	public float AuraAmount => _aura;

	/// <summary>As garras estão riscando.</summary>
	public bool TrailsActive => _left?.Active == true || _right?.Active == true;

	public TransformSmoke Smoke => _smoke;

	/// <summary>As duas garras (esquerda, direita), para os testes.</summary>
	public ClawTrail LeftTrail => _left;
	public ClawTrail RightTrail => _right;

	/// <summary>A cor dos efeitos deste lobo: a do personagem, ou a do time.</summary>
	public Color EffectColor
	{
		get
		{
			Color own = _player?.Character?.EffectColor ?? new Color(0, 0, 0, 0);
			if (own.A > 0.01f) return new Color(own, 1f);
			return _health != null && _health.Team == Teams.Orange ? OrangeEffect : BlueEffect;
		}
	}

	public override void _Ready()
	{
		_body = GetParent<CharacterBody3D>();
		_form = _body.GetNode<FormController>("FormController");
		_weapon = _body.GetNode<Weapon>("Weapon");
		_health = _body.GetNodeOrNull<Health>("Health");
		_visual = _body.GetNodeOrNull<WolfVisual>("WolfVisual");
		_player = _body as Player;
		_wolfCamera = _body.GetNodeOrNull<Camera3D>("Head/SpringArm3D/WolfCamera");
		if (_wolfCamera != null) _wolfCameraFov = _wolfCamera.Fov;

		_smoke = new TransformSmoke { Name = "Fumaca" };
		AddChild(_smoke);
		BuildStreaks();

		if (_visual?.Model is Node3D model)
		{
			foreach (Node node in model.FindChildren("*", "MeshInstance3D", true, false))
				_meshes.Add((MeshInstance3D)node);
			_overlay = ResourceLoader.Exists(AuraPath) ? GD.Load<Material>(AuraPath) : null;

			var skeleton = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
			Material trail = ResourceLoader.Exists(TrailPath) ? GD.Load<Material>(TrailPath) : null;
			if (skeleton != null && trail != null)
			{
				_left = ClawTrail.For(skeleton, "LeftHand", trail);
				_right = ClawTrail.For(skeleton, "RightHand", trail);
				if (_left != null) AddChild(_left);
				if (_right != null) AddChild(_right);
			}
		}

		_form.FormChanged += OnFormChanged;
		_weapon.WolfMoveStarted += OnMove;
		if (_health != null)
		{
			_health.Died += _ => StopAll();
			_health.Respawned += StopAll;
		}

		ApplyQuality();
		// Liga o material por alguns quadros já no começo: o shader compila
		// aqui, e não no primeiro rugido no meio da luta.
		SetOverlay(_overlay != null && _meshes.Count > 0);
	}

	/// <summary>Os riscos de vento: traços finos que nascem numa caixa em
	/// volta do corpo e ficam no mundo — o lobo passa por eles, e eles
	/// escorrem para trás. Alinhados com a velocidade.</summary>
	private void BuildStreaks()
	{
		_streakProcess = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
			EmissionBoxExtents = new Vector3(0.7f, 0.6f, 0.7f),
			Direction = Vector3.Back,
			Spread = 4f,
			InitialVelocityMin = 0.3f,
			InitialVelocityMax = 1f,
			Gravity = Vector3.Zero,
			ScaleMin = 0.6f,
			ScaleMax = 1.2f,
		};
		var fade = new Gradient();
		fade.SetColor(0, new Color(1, 1, 1, 0));
		fade.SetColor(1, new Color(1, 1, 1, 0));
		fade.AddPoint(0.3f, new Color(1, 1, 1, 0.18f));
		_streakProcess.ColorRamp = new GradientTexture1D { Gradient = fade };

		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			VertexColorUseAsAlbedo = true,
			AlbedoColor = new Color(0.85f, 0.93f, 1f),
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};
		_streaks = new GpuParticles3D
		{
			Name = "Riscos",
			Emitting = false,
			Amount = 12,
			Lifetime = 0.18f,
			LocalCoords = false,
			ProcessMaterial = _streakProcess,
			TransformAlign = GpuParticles3D.TransformAlignEnum.ZBillboardYToVelocity,
			DrawPass1 = new QuadMesh { Size = new Vector2(0.012f, 0.4f), Material = material },
			VisibilityAabb = new Aabb(new Vector3(-3, -2, -3), new Vector3(6, 4, 6)),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Position = new Vector3(0, 0.1f, 0),
		};
		AddChild(_streaks);
	}

	/// <summary>Quantidade de cada efeito pela qualidade (Configurações).</summary>
	private void ApplyQuality()
	{
		int q = Mathf.Clamp(Settings.Values.Quality, 0, 3);
		int points = q switch { 0 => 12, 1 => 20, _ => 32 };
		if (_left != null) _left.MaxPoints = points;
		if (_right != null) _right.MaxPoints = points;
		if (_smoke != null) _smoke.Amount = q switch { 0 => 6, 1 => 10, _ => 16 };
		if (_streaks != null) _streaks.Amount = q switch { 0 => 4, 1 => 8, _ => 12 };
	}

	private static float SmokeRatio => Mathf.Clamp(Settings.Values.Quality, 0, 3) switch { 0 => 0.35f, 1 => 0.65f, _ => 1f };

	private void OnFormChanged(int form)
	{
		Color tint = EffectColor;
		switch ((Form)form)
		{
			case Form.ShiftingToWolf:
				_smoke?.Play(_form.ShiftDuration, tint, SmokeRatio);
				break;
			case Form.ShiftingToHuman:
				_smoke?.Play(_form.ShiftBackDuration, tint, SmokeRatio);
				StopAll();
				break;
		}
	}

	/// <summary>Um golpe começou: as garras riscam na janela dele.</summary>
	private void OnMove(int move)
	{
		if (!_form.IsWolf) return;
		switch ((WolfMove)move)
		{
			case WolfMove.Claw:
			case WolfMove.Punch:
				// Do clique até um pouco depois do impacto. O rastro só
				// aparece onde a garra anda rápido (ClawTrail.FullSpeed):
				// a preparação e o recolher, lentos, não riscam.
				float impact = WolfMoves.ImpactDelay((WolfMove)move);
				StrikeWindow(0f, impact + 0.3f);
				break;

			// O especial: raios saindo do corpo no disparo (e de novo na
			// queda, rente ao chão — veja _Process).
			case WolfMove.Pounce:
				LightningBurst.Spawn(_body, new Vector3(0, 0.1f, 0), EffectColor, flat: false);
				break;
		}
	}

	private bool _wasFlying;

	/// <summary>O lobo acabou de cair do especial: raios pelo chão.</summary>
	private void WatchLanding()
	{
		bool flying = _weapon.IsPounceFlying;
		if (_wasFlying && !flying && _form.IsWolf && (_health == null || !_health.IsDead)
			&& _body.GetParent() is Node3D world)
		{
			// Solto no mundo, não no corpo: o lobo sai andando e os raios ficam.
			float feet = _player?.FeetOffset ?? -0.875f;
			LightningBurst burst = LightningBurst.Spawn(world, Vector3.Zero, EffectColor, flat: true);
			burst.GlobalPosition = _body.GlobalPosition + new Vector3(0, feet + 0.08f, 0);
		}
		_wasFlying = flying;
	}

	private async void StrikeWindow(float delay, float length)
	{
		if (delay > 0f) await ToSignal(GetTree().CreateTimer(delay, false), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this)) return;
		_strikeLeft = Mathf.Max((float)_strikeLeft, length);
	}

	private void StopAll()
	{
		_strikeLeft = 0;
		_left?.Clear();
		_right?.Clear();
		_aura = _air = 0f;
		Push();
	}

	public override void _Process(double delta)
	{
		WatchLanding();
		float dt = (float)delta;
		bool shown = _form.ShowsWolf && (_health == null || !_health.IsDead);

		// Garras: a janela do golpe, o voo do salto e o bote.
		if (_strikeLeft > 0) _strikeLeft -= delta;
		bool striking = shown && (_strikeLeft > 0 || _weapon.IsPounceFlying || _weapon.IsDiving);
		Color color = EffectColor;
		foreach (ClawTrail trail in new[] { _left, _right })
		{
			if (trail == null) continue;
			trail.Active = striking;
			trail.Color = color;
		}

		// Aura: poder em uso (rugido, carga do salto).
		bool power = shown && ((_visual?.Howling ?? false) || _weapon.IsChargingPounce);
		_aura = Mathf.MoveToward(_aura, power ? PowerAura : 0f, dt / 0.3f);

		// Vento: velocidade acima de AirFrom da máxima (a de quatro).
		float max = MaxSpeed();
		float speed = _body.Velocity.Length();
		float t = max > 0.01f ? Mathf.Clamp(speed / max, 0f, 1.5f) : 0f;
		float air = shown && Settings.Values.Quality > 0 ? Mathf.Clamp((t - AirFrom) / (1f - AirFrom), 0f, 1f) : 0f;
		_air = Mathf.MoveToward(_air, air, dt / (air > _air ? 0.15f : 0.3f));

		Push();
		UpdateStreaks();
		UpdateCamera(dt);
	}

	private void UpdateStreaks()
	{
		if (_streaks == null) return;
		bool on = _air > 0.05f;
		if (on)
		{
			Vector3 velocity = _body.Velocity;
			Vector3 back = velocity.LengthSquared() > 0.01f ? -velocity.Normalized() : _body.GlobalBasis.Z;
			// A direção do processo é no espaço do emissor (que gira com o corpo).
			_streakProcess.Direction = (_streaks.GlobalBasis.Inverse() * back).Normalized();
			_streaks.AmountRatio = Mathf.Clamp(_air, 0.05f, 1f);
		}
		if (_streaks.Emitting != on) _streaks.Emitting = on;
	}

	/// <summary>A velocidade de quatro deste lobo: a do vento cheio.</summary>
	private float MaxSpeed()
	{
		if (_player == null) return 7f;
		float breed = _form.Breed?.SpeedMultiplier ?? 1f;
		return _player.HumanSpeed * breed * _player.WolfSprintFactor;
	}

	/// <summary>Passa a cor e as quantidades para cada malha do lobo, e
	/// tira o material de cima quando nada aparece (não gasta placa).</summary>
	private void Push()
	{
		if (_overlay == null || _meshes.Count == 0) return;
		bool need = _aura > 0.001f || _air > 0.001f || _warm > 0;
		if (_warm > 0) _warm--;
		if (need != _overlayOn) SetOverlay(need);
		if (!need) return;

		Color color = EffectColor;
		Vector3 dir = _body.Velocity with { Y = _body.Velocity.Y * 0.5f };
		dir = dir.LengthSquared() > 0.01f ? dir.Normalized() : -_body.GlobalBasis.Z;
		foreach (MeshInstance3D mesh in _meshes)
		{
			if (!IsInstanceValid(mesh)) continue;
			mesh.SetInstanceShaderParameter("aura_color", color);
			mesh.SetInstanceShaderParameter("aura_amount", _aura);
			mesh.SetInstanceShaderParameter("speed_amount", _air);
			mesh.SetInstanceShaderParameter("move_dir", dir);
		}
	}

	private void SetOverlay(bool on)
	{
		_overlayOn = on;
		foreach (MeshInstance3D mesh in _meshes)
			if (IsInstanceValid(mesh)) mesh.MaterialOverlay = on ? _overlay : null;
	}

	/// <summary>Para o dono do lobo, a câmera abre um pouco com o vento: é
	/// o que vende velocidade de dentro.</summary>
	private void UpdateCamera(float dt)
	{
		if (_wolfCamera == null || _player == null || !_wolfCamera.Current) return;
		float k = 1f - Mathf.Exp(-8f * dt);
		_wolfCamera.Fov = Mathf.Lerp(_wolfCamera.Fov, _wolfCameraFov + AirFovKick * _air, k);
	}
}
