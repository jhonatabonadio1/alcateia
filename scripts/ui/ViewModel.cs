using Godot;

/// <summary>
/// O que o jogador vê nas próprias mãos: os dois braços segurando a arma
/// que ele escolheu. A AK aparece como AK, a M3 como M3 — o modelo e a
/// posição de cada mão vêm de <see cref="WeaponModel"/>, o mesmo que os
/// outros veem na sua mão e no chão quando você solta.
///
/// Reage a tudo o que a arma e o corpo fazem:
///
/// - **Tiro**: coice para trás e para cima, clarão no cano, o ferrolho
///   vai e volta, a cápsula salta (ViewModel.Effects).
/// - **Recarga**: a mão esquerda larga o guarda-mão, tira o carregador,
///   some com ele, volta com outro, encaixa e puxa o ferrolho se o pente
///   esvaziou; na escopeta, um cartucho por vez (ViewModel.Hands). Segue
///   o relógio da própria arma (<see cref="Weapon.ReloadProgress"/>):
///   recarga mais longa dura mais na tela.
/// - **Passo**: o balanço cai junto com o som do pé — os dois saem da
///   mesma passada (<see cref="BodyNoise.StridePhase"/>).
/// - **Olhar**: a arma atrasa um pouco em relação à câmera e alcança.
/// - **Pouso**: afunda e volta.
///
/// É só o que se vê: nada aqui decide tiro, recarga ou movimento.
/// </summary>
public partial class ViewModel : Node3D
{
	/// <summary>Recuo do disparo, em metros, e o quanto o cano sobe, em graus.</summary>
	[ExportGroup("Tiro")]
	[Export] public float Kick = 0.055f;
	[Export] public float KickPitch = 3.2f;

	/// <summary>Duração do clarão do cano.</summary>
	[Export] public float FlashLife = 0.05f;
	[Export] public Color FlashColor = new Color(1f, 0.82f, 0.45f);

	/// <summary>Balanço do passo: quanto sobe e quanto vai de lado, em metros.</summary>
	[ExportGroup("Movimento")]
	[Export] public float BobAmount = 0.011f;
	[Export] public float BobSide = 0.007f;

	/// <summary>Atraso da arma ao olhar: graus de arma por grau de
	/// câmera num quadro, o teto em graus e a rapidez com que alcança.</summary>
	[Export] public float SwayAmount = 0.55f;
	[Export] public float SwayMax = 3.5f;
	[Export] public float SwayReturn = 9f;

	/// <summary>Quanto a arma afunda ao pousar, em metros por m/s de queda.</summary>
	[Export] public float LandDip = 0.006f;

	// Onde fica o punho de cada tipo de arma, a partir da câmera.
	// O modelo é em tamanho real; na mão ele fica um pouco menor e
	// levemente virado para o centro, para se ver o lado da arma e não
	// só a coronha encostada na câmera.
	private static readonly Vector3 RifleAnchor = new(0.15f, -0.155f, -0.36f);
	private static readonly Vector3 PistolAnchor = new(0.1f, -0.135f, -0.46f);
	private static readonly Vector3 KnifeAnchor = new(0.2f, -0.13f, -0.38f);
	private static readonly Vector3 GrenadeAnchor = new(0.19f, -0.12f, -0.38f);
	private const float ModelScale = 0.72f;
	private const float AimYaw = 4f;

	private Weapon _weapon;
	private FormController _form;
	private CharacterBody3D _body;
	private Player _player;
	private Camera3D _camera;

	private Node3D _held;
	private string _heldId = "";
	private Node3D _muzzle;

	private Vector3 _rest;
	private float _recoil;
	private float _draw;
	private float _dip;
	private float _dipSpeed;
	private bool _wasOnFloor = true;
	private float _fallSpeed;
	private Vector2 _sway;
	private Vector3 _lastLook;
	private float _bobWeight;

	/// <summary>
	/// Ponta do cano da arma na mão, em coordenadas de mundo.
	///
	/// O traçante sai daqui, não da câmera. O raio continua partindo
	/// do centro da tela, porque é dele que vem a precisão — mas o
	/// jogador vê a linha nascer no cano, que é de onde ela deveria
	/// nascer. Desenhar a partir da câmera deixa o tiro saindo da
	/// testa, e é o artefato que mais entrega um FPS improvisado.
	/// </summary>
	public bool TryGetMuzzle(out Vector3 position)
	{
		position = Vector3.Zero;
		if (_muzzle == null || _form.IsWolf || !Visible || _weapon.Held == null) return false;

		position = _muzzle.GlobalPosition;
		return true;
	}

	/// <summary>A arma que está na mão agora (o modelo), ou null.</summary>
	public Node3D Held => _held;

	public override void _Ready()
	{
		_camera = GetParent<Camera3D>();
		_body = GetParent().GetParent().GetParent<CharacterBody3D>();
		_player = _body as Player;
		_weapon = _body.GetNode<Weapon>("Weapon");
		_form = _body.GetNode<FormController>("FormController");

		_rest = Position;
		_lastLook = _camera.GlobalRotation;

		// A arma na mão fica a menos de meio metro do olho: o plano de corte
		// da câmera não pode comê-la.
		_camera.Near = Mathf.Min(_camera.Near, 0.03f);
		PreloadRigs();

		_weapon.SlotChanged += _ => Refresh();
		_form.FormChanged += _ => Refresh();
		_weapon.Fired += OnFired;
		_weapon.AmmoChanged += OnAmmo;

		BuildEffects();
		Refresh();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_recoil = Mathf.MoveToward(_recoil, 0f, dt * 5.5f);
		_draw = Mathf.MoveToward(_draw, 0f, dt * 3.6f);

		Vector3 bob = UpdateBob(dt);
		UpdateSway(dt);
		UpdateLanding(dt);

		// Com os braços animados, o tiro e o sacar já vêm da animação.
		// O conjunto sem animação de sacar sobe de baixo, como as mãos de código.
		float kick = Ease(_recoil) * (HasRig ? RigKick : 1f);
		// O conjunto está a meio metro do olho, não a um: a subida é menor.
		float draw = HasRig && _rigClips.ContainsKey("sacar") ? 0f : _draw;
		float drop = HasRig ? 0.06f : 0.2f, tilt = HasRig ? 14f : 24f;
		Position = _rest + bob + new Vector3(0, -draw * drop - _dip, Kick * kick);
		RotationDegrees = new Vector3(_sway.Y + KickPitch * kick - draw * tilt, _sway.X, -_sway.X * 0.6f);

		UpdateHands(dt);
		UpdateEffects(dt);
	}

	/// <summary>
	/// O balanço do passo, tirado da passada do corpo: zero em cada
	/// pisada, no alto no meio dela, e de um lado para o outro a cada
	/// passo. Como o som do pé sai da mesma conta, os dois caem juntos.
	/// </summary>
	private Vector3 UpdateBob(float dt)
	{
		var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
		bool walking = _body.IsOnFloor() && flat.Length() > 0.5f && _player != null;
		_bobWeight = Mathf.MoveToward(_bobWeight, walking ? 1f : 0f, dt * 6f);
		if (_player == null || _bobWeight <= 0f) return Vector3.Zero;

		// O conjunto animado que tem o próprio andar já balança sozinho.
		if (HasRig && _rigClips.ContainsKey("andar")) return Vector3.Zero;

		float stride = (_player.Steps + _player.StridePhase) * Mathf.Pi;
		float speed = Mathf.Clamp(flat.Length() / 3.2f, 0.4f, 1.3f);
		return new Vector3(Mathf.Sin(stride) * BobSide, Mathf.Abs(Mathf.Sin(stride)) * BobAmount - BobAmount * 0.5f, 0)
			* (_bobWeight * speed);
	}

	/// <summary>A arma atrasa em relação ao olhar: virou a câmera, ela
	/// fica um pouco para trás e alcança.</summary>
	private void UpdateSway(float dt)
	{
		Vector3 look = _camera.GlobalRotation;
		float yaw = Mathf.RadToDeg(Mathf.AngleDifference(_lastLook.Y, look.Y));
		float pitch = Mathf.RadToDeg(Mathf.AngleDifference(_lastLook.X, look.X));
		_lastLook = look;

		_sway.X = Mathf.Clamp(_sway.X - yaw * SwayAmount, -SwayMax, SwayMax);
		_sway.Y = Mathf.Clamp(_sway.Y - pitch * SwayAmount, -SwayMax, SwayMax);
		_sway = _sway.Lerp(Vector2.Zero, Mathf.Clamp(dt * SwayReturn, 0f, 1f));
	}

	/// <summary>Pousou: a arma afunda na proporção da queda e volta como mola.</summary>
	private void UpdateLanding(float dt)
	{
		bool onFloor = _body.IsOnFloor();
		if (!onFloor) _fallSpeed = Mathf.Max(_fallSpeed, -_body.Velocity.Y);
		if (onFloor && !_wasOnFloor)
		{
			_dipSpeed += Mathf.Min(_fallSpeed, 12f) * LandDip * 14f;
			_fallSpeed = 0;
		}
		_wasOnFloor = onFloor;

		// Mola amortecida, sem passar do ponto mais de uma vez.
		_dipSpeed += (-_dip * 160f - _dipSpeed * 16f) * dt;
		_dip = Mathf.Clamp(_dip + _dipSpeed * dt, -0.02f, 0.08f);
	}

	private void OnFired()
	{
		if (_form.IsWolf) return;

		_recoil = 1f;
		if (_weapon.HumanSlot == Slot.Knife)
		{
			if (!RigSwing()) StartSlash();
			return;
		}

		if (_weapon.Held == null) return;
		RigFired();
		FireEffects();
	}

	/// <summary>Mostra só o que está na mão, com o modelo da arma
	/// escolhida. Trocar de arma reconstrói e a nova sobe de baixo.</summary>
	private void Refresh()
	{
		string id = _form.IsWolf ? "" : _weapon.HeldId;
		if (id == _heldId && _held != null) return;

		_held?.QueueFree();
		_held = null;
		_muzzle = null;
		_heldId = id;
		ForgetHands();
		ForgetRig();

		if (id == "") return;

		// Arma de fogo com conjunto animado: os braços do animador.
		// O resto (faca, granada): o modelo e as mãos de código.
		if (BuildRig(id))
		{
			_muzzle = _held.GetNodeOrNull<Node3D>(WeaponModel.MuzzleName);
			_draw = 1f;
			return;
		}

		_held = WeaponModel.Build(id);
		_held.Position = Anchor(id);
		_held.Scale = Vector3.One * ModelScale;
		_held.RotationDegrees = new Vector3(0, AimYaw, 0);
		AddChild(_held);

		_muzzle = _held.GetNodeOrNull<Node3D>(WeaponModel.MuzzleName);
		BuildHands(id);

		_draw = 1f;
	}

	private static Vector3 Anchor(string id) => id switch
	{
		Arsenal.KnifeId => KnifeAnchor,
		Arsenal.GrenadeId => GrenadeAnchor,
		_ when WeaponModel.IsPistol(id) => PistolAnchor,
		_ => RifleAnchor
	};

	private static float Smooth(float t)
	{
		t = Mathf.Clamp(t, 0f, 1f);
		return t * t * (3f - 2f * t);
	}

	/// <summary>Sai rápido e volta devagar: o coice.</summary>
	private static float Ease(float t) => t * t * (3f - 2f * t);

	/// <summary>Quanto de um trecho [de, até] já passou, alisado.</summary>
	private static float Span(float p, float from, float to) => Smooth((p - from) / Mathf.Max(to - from, 0.0001f));
}
