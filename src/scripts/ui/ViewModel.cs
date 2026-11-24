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

	/// <summary>A luz só das mãos e da arma (pedido do Jhon, 17/11: na
	/// sombra elas ficavam quase pretas). Uma lâmpada fraca presa à câmera,
	/// sem sombra, que só acende o que está na camada da mão
	/// (<see cref="HandLayer"/>): o mapa e os outros não mudam. A sombra do
	/// mundo continua escurecendo a arma — entrar num contêiner ainda se
	/// nota —, só não chega ao preto.</summary>
	///
	/// **18/11: ela nunca tinha acendido.** A lâmpada era posta na câmera
	/// dentro do <c>_Ready</c> deste nó, que é filho da câmera: o Godot
	/// recusa ("Parent node is busy setting up children") e a luz não
	/// entrava na cena. Por isso o Jhon continuou vendo a arma e a mão
	/// pretas nos becos. Agora entra no quadro seguinte.
	[ExportGroup("Luz da mão")]
	[Export] public float HandFillEnergy = 0.6f;
	[Export] public Color HandFillColor = new Color(1f, 0.96f, 0.9f);

	/// <summary>A luz dos personagens (pedido do Jhon, 18/11: "tem alguns
	/// becos muito escuros no mapa que deixam a arma, a mão e o personagem
	/// praticamente escuros; a luz do mapa tá excelente, só uns pequenos
	/// ajustes nesses detalhes"). Outra lâmpada fraca na câmera, sem
	/// sombra, que só acende quem está na camada dos personagens
	/// (<see cref="CharacterLayer"/>): lobos e humanos perto de você não
	/// somem no escuro, e o mapa não muda. Fraca de propósito: no claro
	/// quase não se nota; no beco é o que separa o personagem do preto.</summary>
	[ExportGroup("Luz dos personagens")]
	[Export] public float CharacterFillEnergy = 0.38f;
	[Export] public float CharacterFillRange = 13f;
	[Export] public Color CharacterFillColor = new Color(0.92f, 0.95f, 1f);

	/// <summary>Camada de desenho a mais dos personagens (a 18): é por ela
	/// que a luz dos personagens os acha. Eles continuam na camada 1.</summary>
	public const uint CharacterLayer = 1u << 17;

	/// <summary>Põe tudo o que se desenha embaixo de <paramref name="model"/>
	/// também na camada dos personagens. Não usar no corpo que o dono não
	/// vê (o humano em primeira pessoa): ele voltaria a aparecer.</summary>
	public static void MarkCharacter(Node model)
	{
		if (model is VisualInstance3D visual && model is not Light3D) visual.Layers |= CharacterLayer;
		foreach (Node child in model.GetChildren()) MarkCharacter(child);
	}

	/// <summary>Camada de desenho das mãos e da arma em primeira pessoa
	/// (a 20, a última): só a luz da mão a acende sozinha.</summary>
	public const uint HandLayer = 1u << 19;

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
	private float _baseFov = 75f;
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
		_baseFov = _camera.Fov;
		_lastLook = _camera.GlobalRotation;

		// A arma na mão fica a menos de meio metro do olho: o plano de corte
		// da câmera não pode comê-la.
		// 1,5 cm: o conjunto é desenhado encolhido para perto do olho
		// (RigScale), e na mira a alça do AR-15 fica a 6 cm do olho no
		// modelo — menos de 3 cm na tela. Com 3 cm de corte a alça sumia.
		_camera.Near = Mathf.Min(_camera.Near, 0.015f);
		PreloadRigs();

		_weapon.SlotChanged += OnSlot;
		_form.FormChanged += OnForm;
		_weapon.Fired += OnFired;
		_weapon.Butted += OnButted;
		_weapon.Stabbed += OnStabbed;
		_weapon.AmmoChanged += OnAmmo;
		_weapon.Thrown += OnThrown;

		BuildEffects();
		BuildHandFill();
		Refresh();
	}

	private void OnSlot(int _) => SwitchWeapon();
	private void OnForm(int _) => Refresh();

	/// <summary>A arma e a forma são do corpo e vivem mais que esta mão (a
	/// emprestada ao espectador some antes): sem soltar os sinais, o próximo
	/// tiro chamava uma mão já apagada.</summary>
	public override void _Notification(int what)
	{
		if (what != NotificationPredelete) return;
		if (IsInstanceValid(_weapon))
		{
			_weapon.SlotChanged -= OnSlot;
			_weapon.Fired -= OnFired;
			_weapon.Butted -= OnButted;
			_weapon.Stabbed -= OnStabbed;
			_weapon.AmmoChanged -= OnAmmo;
		}
		if (IsInstanceValid(_form)) _form.FormChanged -= OnForm;
	}

	private void BuildHandFill()
	{
		// Acima e um pouco atrás do olho, como a luz do teto chegando por
		// cima do ombro: ilumina o lado de cima da arma, que é o que se vê.
		var hand = new OmniLight3D
		{
			Name = "LuzDaMao",
			Position = new Vector3(0.15f, 0.35f, 0.15f),
			LightEnergy = HandFillEnergy,
			LightColor = HandFillColor,
			OmniRange = 1.6f,
			OmniAttenuation = 0.6f,
			LightCullMask = HandLayer,
			ShadowEnabled = false,
			LightSpecular = 0.3f
		};
		_handLight = hand;
		var characters = new OmniLight3D
		{
			Name = "LuzDosPersonagens",
			LightEnergy = CharacterFillEnergy,
			LightColor = CharacterFillColor,
			OmniRange = CharacterFillRange,
			OmniAttenuation = 0.8f,
			LightCullMask = CharacterLayer,
			ShadowEnabled = false,
			LightSpecular = 0.15f,
			// Solta da cabeça: vai para onde a câmera de verdade está (abaixo,
			// FollowView). De lobo a câmera fica atrás, e a luz presa na
			// cabeça acendia uma mancha nas costas do próprio lobo (19/11).
			TopLevel = true
		};
		_charactersLight = characters;
		// No quadro seguinte: agora a câmera ainda está montando os filhos
		// (este nó é um deles) e recusaria as lâmpadas.
		_camera.CallDeferred(Node.MethodName.AddChild, hand);
		_camera.CallDeferred(Node.MethodName.AddChild, characters);
		MoveToHandLayer(this);
	}

	/// <summary>Tudo o que se desenha embaixo de <paramref name="node"/>
	/// passa para a camada da mão (e só para ela).</summary>
	private static void MoveToHandLayer(Node node)
	{
		if (node is VisualInstance3D visual && node is not Light3D) visual.Layers = HandLayer;
		foreach (Node child in node.GetChildren()) MoveToHandLayer(child);
	}

	private int _maskEvery;

	/// <summary>A mão e a arma marcam o stencil (<see cref="Silhouette.Mask"/>):
	/// a borda dos corpos nunca passa por cima delas. Confere a cada
	/// poucos quadros, porque a arma na mão troca.</summary>
	private static void MaskHands(Node node)
	{
		if (node is GeometryInstance3D mesh && mesh.MaterialOverlay == null) mesh.MaterialOverlay = Silhouette.Mask;
		foreach (Node child in node.GetChildren()) MaskHands(child);
	}

	private OmniLight3D _charactersLight;
	private OmniLight3D _handLight;

	/// <summary>Na mira, a luz da mão vem para perto do olho e acende
	/// mais: a alça e a massa ficam de frente para quem mira, e com a luz
	/// lá no alto, atrás, elas ficavam escuras contra o cenário (pedido do
	/// Jhon, 18/11: "luz na arma ao mirar").</summary>
	private static readonly Vector3 HandLightHip = new(0.15f, 0.35f, 0.15f), HandLightAim = new(0.02f, 0.14f, 0.06f);
	[Export] public float AimLightEnergy = 1.25f;

	private void AimLight()
	{
		if (_handLight == null || !IsInstanceValid(_handLight) || _weapon == null) return;
		float aim = _form != null && _form.IsWolf ? 0f : Mathf.Clamp(_weapon.AimBlend, 0f, 1f);
		_handLight.Position = HandLightHip.Lerp(HandLightAim, aim);
		_handLight.LightEnergy = Mathf.Lerp(HandFillEnergy, AimLightEnergy, aim);
	}

	/// <summary>A luz dos personagens sai da câmera que está desenhando.</summary>
	private void FollowView()
	{
		if (_charactersLight == null || !IsInstanceValid(_charactersLight) || !_charactersLight.IsInsideTree()) return;
		Camera3D view = GetViewport().GetCamera3D();
		_charactersLight.GlobalPosition = (view ?? _camera).GlobalPosition;
	}

	public override void _Process(double delta)
	{
		FollowView();
		AimLight();
		if (--_maskEvery <= 0) { _maskEvery = 12; MaskHands(this); }
		float dt = (float)delta;
		// Guardou: a arma nova sobe (mesmo que seja a mesma, de volta).
		if (_holster > 0f && (_holster -= dt) <= 0f)
		{
			_heldId = "\0";
			Refresh();
		}
		_recoil = Mathf.MoveToward(_recoil, 0f, dt * 5.5f);
		_draw = Mathf.MoveToward(_draw, 0f, dt * 3.6f);

		Vector3 bob = UpdateBob(dt);
		UpdateSway(dt);
		UpdateLanding(dt);

		// No zoom (botão direito) a arma fica onde está, mas quase não
		// balança: quem mira é a mira circular (Crosshair).
		float aim = _weapon.AimEase;
		float steady = 1f - 0.8f * aim;
		bob *= steady;
		Vector2 sway = _sway * (1f - 0.65f * aim);

		// Conjunto fiel (o AR-15, pedido do Jhon em 15/11: "não coloque nem
		// acrescente nada pelo engine"): só a animação dele, sem balanço,
		// atraso ao olhar, coice nem afundar no pouso de código.
		bool raw = HasRig && _rigRaw;
		if (raw)
		{
			bob = Vector3.Zero;
			sway = Vector2.Zero;
		}

		// Com os braços animados, o tiro e o sacar já vêm da animação.
		// O conjunto sem animação de sacar sobe de baixo, como as mãos de código.
		float kick = raw ? 0f : Ease(_recoil) * (HasRig ? _rigKick : 1f) * (1f - 0.4f * aim);
		// O conjunto está a meio metro do olho, não a um: a subida é menor.
		float draw = HasRig && _rigClips.ContainsKey("sacar") ? 0f : _draw;
		float drop = HasRig ? 0.06f : 0.2f, tilt = HasRig ? 14f : 24f;
		var swing = new Vector3(sway.Y + KickPitch * kick - draw * tilt, sway.X, -sway.X * 0.6f);
		Vector3 position = _rest + bob + new Vector3(0, -draw * drop - (raw ? 0f : _dip), Kick * kick);
		if (_butt >= 0f) Butt(dt, ref swing, ref position);

		// A câmera fecha no zoom, mas a arma não pode crescer junto (é o
		// mesmo que ter uma câmera só para ela, como todo FPS): encolhe na
		// largura e na altura da tela na razão das aberturas.
		float zoom = Mathf.Tan(Mathf.DegToRad(_camera.Fov * 0.5f)) / Mathf.Tan(Mathf.DegToRad(_baseFov * 0.5f));
		Basis shrink = Basis.FromScale(new Vector3(zoom, zoom, 1f));
		Transform = new Transform3D(shrink * Basis.FromEuler(swing * Mathf.DegToRad(1f)), shrink * position);

		UpdateHands(dt);
		UpdateEffects(dt);
		UpdateCameraTrack(dt);
		UpdateScope();
	}

	/// <summary>Na luneta (fuzil de precisão) a arma sai da tela no fim da
	/// subida: quem mira é o retículo (<see cref="Crosshair.PaintSniperScope"/>).</summary>
	private void UpdateScope()
	{
		if (_held == null) return;
		_held.Visible = !_weapon.Scoped;
	}

	// ------------------------------------------------------------ a coronhada

	/// <summary>Duração da coronhada na mão, em segundos.</summary>
	[Export] public float ButtTime = 0.34f;

	private float _butt = -1f;

	/// <summary>A coronhada está tocando na mão. Para os testes.</summary>
	public bool Butting => _butt >= 0f || (_rigPlaying == "coronhada" && _rigBusy > 0);

	/// <summary>
	/// A coronhada começou. O conjunto que tem o clipe próprio (o AR-15:
	/// <c>tools/armas/coronhada.py</c> — a arma atravessa na frente do
	/// rosto e bate com a lateral, com os braços acompanhando) toca o
	/// clipe; os outros usam o movimento de código (<see cref="Butt"/>).
	/// </summary>
	private void OnButted()
	{
		if (HasRig && _rigClips.TryGetValue("coronhada", out Clip clip))
		{
			_rigAiming = false;
			PlayRig("coronhada", 0, 0.04);
			_rigBusy = clip.Length;
			return;
		}

		_butt = 0f;
	}

	/// <summary>
	/// A estocada da faca (botão direito; Jhon, 24/11): o começo do
	/// "inspecionar" do conjunto (<c>"estocada"</c> na ficha: a mão sobe,
	/// crava e volta), no tempo dele — o dano sai quando a lâmina chega à
	/// frente (<see cref="Weapon.KnifeStabHitDelay"/>, o <c>contato</c> do
	/// clipe). Sem o clipe, o golpe de código da faca.
	/// </summary>
	private void OnStabbed()
	{
		if (HasRig && _rigClips.TryGetValue("estocada", out Clip clip))
		{
			_swingTail = null;
			PlayRig("estocada", 0, 0.04);
			_rigBusy = clip.Length;
			return;
		}
		OnFired();
	}

	/// <summary>A estocada está tocando na mão. Para os testes.</summary>
	public bool Stabbing => _rigPlaying == "estocada" && _rigBusy > 0;

	/// <summary>
	/// A coronhada (V), em cima de qualquer arma de fogo: a arma recolhe um
	/// instante para o ombro, gira (o cano sai para a esquerda e a coronha
	/// vem para a frente) e soca no meio da tela; depois volta. Três
	/// tempos: arma (22%), bate (até 46%), volta (o resto, devagar no fim).
	/// É de código, por cima do conjunto animado — vale para todas as
	/// armas sem mexer nos arquivos delas.
	/// </summary>
	private void Butt(float dt, ref Vector3 swing, ref Vector3 position)
	{
		_butt += dt / Mathf.Max(ButtTime, 0.05f);
		if (_butt >= 1f) { _butt = -1f; return; }

		// Os dois extremos: recolhida e batendo (graus; metros).
		// O giro é contido: os braços do conjunto acabam no cotovelo, e
		// girando demais o corte aparecia no meio da tela.
		// E o avanço é de poucos centímetros: o conjunto fica colado no
		// olho (RigScale), e 20 cm para a frente era a arma indo embora.
		var windTurn = new Vector3(5f, -4f, -7f); var windMove = new Vector3(0.012f, -0.012f, 0.03f);
		var hitTurn = new Vector3(-9f, 13f, 16f); var hitMove = new Vector3(-0.022f, 0.016f, -0.05f);

		Vector3 turn, move;
		if (_butt < 0.22f)
		{
			float u = ButtEase(_butt / 0.22f);
			turn = windTurn * u; move = windMove * u;
		}
		else if (_butt < 0.46f)
		{
			float u = (_butt - 0.22f) / 0.24f;
			u = 1f - (1f - u) * (1f - u);                       // sai rápido, chega seco
			turn = windTurn.Lerp(hitTurn, u); move = windMove.Lerp(hitMove, u);
		}
		else
		{
			float u = ButtEase((_butt - 0.46f) / 0.54f);
			turn = hitTurn * (1f - u); move = hitMove * (1f - u);
		}

		swing += turn;
		position += move;
	}

	private static float ButtEase(float x)
	{
		x = Mathf.Clamp(x, 0f, 1f);
		return x * x * (3f - 2f * x);
	}

	/// <summary>
	/// O balanço do passo, tirado da passada do corpo: zero em cada
	/// pisada, no alto no meio dela, e de um lado para o outro a cada
	/// passo. Como o som do pé sai da mesma conta, os dois caem juntos.
	/// </summary>
	private Vector3 UpdateBob(float dt)
	{
		var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
		// O jogador ou um bot (visto pelo espectador, em primeira pessoa).
		Bot bot = _body as Bot;
		bool stepping = _player != null || bot != null;
		bool walking = _body.IsOnFloor() && flat.Length() > 0.5f && stepping;
		_bobWeight = Mathf.MoveToward(_bobWeight, walking ? 1f : 0f, dt * 6f);
		if (!stepping || _bobWeight <= 0f) return Vector3.Zero;

		// O conjunto animado que tem o próprio andar já balança sozinho —
		// menos o que a ficha diz que balança pouco (o AR-15: o pacote é
		// da Unreal, onde o balanço é de código).
		if (HasRig && _rigClips.ContainsKey("andar") && !_rigBob) return Vector3.Zero;

		float stride = _player != null ? (_player.Steps + _player.StridePhase) * Mathf.Pi : (bot.Steps + bot.StridePhase) * Mathf.Pi;
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

	/// <summary>Trocou de arma: o conjunto que tem animação de guardar (o
	/// AR-15) guarda antes; o resto troca já.</summary>
	private void SwitchWeapon()
	{
		string id = _form.IsWolf ? "" : _weapon.HeldId;
		if (id != _heldId && id != "" && StartHolster()) return;
		Refresh();
	}

	/// <summary>Mostra só o que está na mão, com o modelo da arma
	/// escolhida. Trocar de arma reconstrói e a nova sobe de baixo.</summary>
	private void Refresh()
	{
		_holster = 0f;
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
			MoveToHandLayer(this);
			return;
		}

		_held = WeaponModel.Build(id);
		_held.Position = Anchor(id);
		_held.Scale = Vector3.One * ModelScale;
		_held.RotationDegrees = new Vector3(0, AimYaw, 0);
		AddChild(_held);

		_muzzle = _held.GetNodeOrNull<Node3D>(WeaponModel.MuzzleName);
		BuildHands(id);
		MoveToHandLayer(this);

		_draw = 1f;
	}

	private static Vector3 Anchor(string id) => id switch
	{
		_ when Arsenal.IsKnife(id) => KnifeAnchor,
		_ when Arsenal.IsGrenade(id) => GrenadeAnchor,
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
