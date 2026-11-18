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
	[Export] public float PowerAura = 0.7f;

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
	private WindWisps _wisps;
	private LightningAura _sparks;
	private RoarBurst _roar;
	private Skeleton3D _skeleton;
	private int _muzzle = -1;
	private bool _wasArmed;

	/// <summary>Os raios do especial armado, o rugido e o vento. Para os testes.</summary>
	public LightningAura Sparks => _sparks;
	public RoarBurst Roar => _roar;
	public WindWisps Wisps => _wisps;

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
		_wisps = new WindWisps { Name = "Vento" };
		AddChild(_wisps);
		float feet = _player?.FeetOffset ?? -0.875f;
		_sparks = new LightningAura { Name = "Raios", Feet = feet };
		AddChild(_sparks);
		_roar = new RoarBurst { Name = "Rugido", Feet = feet };
		AddChild(_roar);

		if (_visual?.Model is Node3D model)
		{
			foreach (Node node in model.FindChildren("*", "MeshInstance3D", true, false))
				_meshes.Add((MeshInstance3D)node);
			_overlay = ResourceLoader.Exists(AuraPath) ? GD.Load<Material>(AuraPath) : null;

			var skeleton = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
			_skeleton = skeleton;
			if (skeleton != null)
			{
				for (int i = 0; i < skeleton.GetBoneCount(); i++)
				{
					string bone = skeleton.GetBoneName(i);
					if (bone.EndsWith("headfront")) { _muzzle = i; break; }
					if (bone.EndsWith("Head") && _muzzle < 0) _muzzle = i;
				}
			}
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

	/// <summary>Quantidade de cada efeito pela qualidade (Configurações).</summary>
	private void ApplyQuality()
	{
		int q = Mathf.Clamp(Settings.Values.Quality, 0, 3);
		int points = q switch { 0 => 12, 1 => 20, _ => 32 };
		if (_left != null) _left.MaxPoints = points;
		if (_right != null) _right.MaxPoints = points;
		if (_smoke != null) _smoke.Amount = q switch { 0 => 6, 1 => 10, _ => 16 };
		if (_wisps != null) _wisps.Max = q switch { 0 => 0, 1 => 5, _ => 8 };
		if (_sparks != null) _sparks.Arcs = q switch { 0 => 3, 1 => 5, _ => 7 };
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
				// O corte: três riscos de neon na frente, quando a garra chega.
				Slash(impact - 0.03f, (WolfMove)move == WolfMove.Claw);
				break;

			// O especial: raios saindo do corpo no disparo (e de novo na
			// queda, rente ao chão — veja _Process).
			case WolfMove.Pounce:
				LightningBurst.Spawn(_body, new Vector3(0, 0.1f, 0), EffectColor, flat: false);
				Sound(BoltSound, SpecialSoundRadius);
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
			Sound(StrikeSound, SpecialSoundRadius);
		}
		_wasFlying = flying;
	}

	/// <summary>Os três riscos de neon do golpe (<see cref="NeonSlash"/>),
	/// soltos no mundo no instante em que a garra chega.</summary>
	private async void Slash(float delay, bool rightHand)
	{
		if (delay > 0f) await ToSignal(GetTree().CreateTimer(delay, false), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || !IsInsideTree() || !_form.ShowsWolf || (_health != null && _health.IsDead)) return;
		if (_body.GetParent() is not Node3D world) return;
		LastSlash = NeonSlash.Spawn(world, _body.GlobalTransform, EffectColor, rightHand);
	}

	/// <summary>O último corte solto. Para os testes.</summary>
	public NeonSlash LastSlash { get; private set; }

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
		_wisps?.Clear();
		if (_sparks != null) _sparks.Active = false;
		if (_roar != null) _roar.Active = false;
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

		// Aura: poder em uso (rugido e o especial, da saída à queda). Com
		// o especial no Q a carga não dura um quadro; sem o voo aqui, a
		// aura do especial tinha sumido junto com ela.
		bool power = shown && ((_visual?.Howling ?? false) || _weapon.IsChargingPounce || _weapon.SpecialArmed || _weapon.IsPounceFlying);
		_aura = Mathf.MoveToward(_aura, power ? PowerAura : 0f, dt / 0.3f);

		// Vento: velocidade acima de AirFrom da máxima (a de quatro).
		float max = MaxSpeed();
		float speed = _body.Velocity.Length();
		float t = max > 0.01f ? Mathf.Clamp(speed / max, 0f, 1.5f) : 0f;
		float air = shown && Settings.Values.Quality > 0 ? Mathf.Clamp((t - AirFrom) / (1f - AirFrom), 0f, 1f) : 0f;
		_air = Mathf.MoveToward(_air, air, dt / (air > _air ? 0.15f : 0.3f));

		Push();
		PushFur(dt, shown, power);
		UpdatePower(shown, color);
		UpdateStreaks();
		UpdateCamera(dt);
	}

	/// <summary>Os sons de raio do especial (pedido do Jhon, 18/11: "um
	/// barulho que todos podem ouvir"): ao armar, no salto e na queda. Como
	/// os efeitos, tocam em cada máquina a partir do estado do lobo: quem
	/// é o dono ouve de dentro; os outros, do lugar onde ele está, até
	/// <see cref="SpecialSoundRadius"/> metros (o mapa inteiro).</summary>
	[Export] public string ArmSound = "lobo_especial_armar";
	[Export] public string BoltSound = "lobo_especial_raio";
	[Export] public string StrikeSound = "lobo_especial_queda";
	[Export] public float ArmSoundRadius = 40f;
	[Export] public float SpecialSoundRadius = 95f;

	private double _armedHum;

	private void Sound(string name, float radius, float volumeDb = 0f)
	{
		if (string.IsNullOrEmpty(name) || !Sfx.Has(name)) return;
		if (_body == Groups.LocalPlayer(GetTree())) Sfx.Own(name, volumeDb);
		else Sfx.At(name, _body.GlobalPosition, radius, volumeDb, radius);
	}

	/// <summary>Os raios do especial (do Q até o lobo cair) e o rugido.</summary>
	private void UpdatePower(bool shown, Color color)
	{
		bool armed = shown && _weapon.SpecialArmed;
		// Armou: um estalo de raios na hora, e eles ficam no corpo. O chiado
		// elétrico se repete enquanto o especial está armado.
		if (armed && !_wasArmed)
		{
			LightningBurst.Spawn(_body, new Vector3(0, 0.1f, 0), color, flat: false);
			_armedHum = 0;
		}
		if (armed)
		{
			_armedHum -= GetProcessDeltaTime();
			if (_armedHum <= 0)
			{
				_armedHum = 1.0;
				Sound(ArmSound, ArmSoundRadius, _wasArmed ? -5f : 0f);
			}
		}
		_wasArmed = armed;

		_sparks.Tint = color;
		_sparks.Active = armed || (shown && (_weapon.IsPounceFlying || _weapon.SpecialGuarded));

		bool roaring = shown && (_visual?.Howling ?? false);
		_roar.Tint = color;
		_roar.Active = roaring;
		if (roaring && _skeleton != null && _muzzle >= 0 && IsInstanceValid(_skeleton))
		{
			// A boca, pelo osso do focinho; o rugido vai para onde o corpo olha.
			Vector3 muzzle = (_skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_muzzle)).Origin;
			Vector3 forward = -_body.GlobalBasis.Z;
			_roar.Mouth = _roar.ToLocal(muzzle + forward * 0.25f);
			_roar.Forward = _roar.GlobalBasis.Inverse() * (forward + Vector3.Up * 0.12f).Normalized();
		}
	}

	/// <summary>Quanto o pelo deita por m/s de velocidade (correndo, o
	/// vento o leva para trás), em metros.</summary>
	[Export] public float FurDrag = 0.0026f;

	/// <summary>Quanto o pelo sobra por m/s de mudança de velocidade
	/// (arrancou, ele fica para trás; freou, vai para a frente).</summary>
	[Export] public float FurInertia = 0.0075f;

	/// <summary>O máximo que a ponta de um tufo sai do lugar, em metros.</summary>
	[Export] public float FurMaxPush = 0.045f;

	private Vector3 _furLag;
	private Vector3 _furPush;
	private float _neon;

	/// <summary>O pelo e o neon do corpo (assets/efeitos/lobo.gdshader):
	/// o empurrão do movimento nas pontas dos tufos, a força do vento e o
	/// neon aceso com o poder em uso. Parâmetros de instância: cada lobo o
	/// seu.</summary>
	private void PushFur(float dt, bool shown, bool power)
	{
		if (_meshes.Count == 0) return;

		Vector3 velocity = shown ? _body.Velocity : Vector3.Zero;
		_furLag = _furLag.Lerp(velocity, 1f - Mathf.Exp(-7f * dt));
		Vector3 push = ((_furLag - velocity) * FurInertia - velocity * FurDrag).LimitLength(FurMaxPush);
		// Mola amortecida curta: o pelo não teleporta quando a velocidade pula.
		_furPush = _furPush.Lerp(push, 1f - Mathf.Exp(-14f * dt));
		_neon = Mathf.MoveToward(_neon, power ? 1f : 0f, dt / 0.25f);

		float gust = Mathf.Clamp(velocity.Length() / Mathf.Max(MaxSpeed(), 0.1f), 0f, 1.2f);
		foreach (MeshInstance3D mesh in _meshes)
		{
			if (!IsInstanceValid(mesh)) continue;
			// O empurrão vai no espaço da malha (com a escala dela).
			mesh.SetInstanceShaderParameter("fur_push", mesh.GlobalBasis.Inverse() * _furPush);
			mesh.SetInstanceShaderParameter("fur_gust", gust);
			mesh.SetInstanceShaderParameter("neon_boost", _neon);
		}
	}

	private void UpdateStreaks()
	{
		if (_wisps == null) return;
		_wisps.Amount = _air;
		_wisps.Velocity = _body.Velocity;
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
