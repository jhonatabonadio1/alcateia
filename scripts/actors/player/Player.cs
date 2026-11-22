using Godot;

/// <summary>
/// O jogador: lê a intenção (<see cref="CharacterIntent"/>) e move o
/// corpo. Velocidade, pulo e corrida em parede mudam conforme a forma; o
/// Player não conhece as regras da transformação — pergunta ao
/// <see cref="FormController"/>.
///
/// Dividido por assunto: aqui o ciclo (nascer, o passo de física, morrer e
/// renascer); Player.Movement, Player.WallRun, Player.Camera e
/// Player.Actions no resto. O corpo que ele divide com o bot (golpes do
/// lobo, ruído, cadáver) está em scripts/actors/body.
///
/// Multiplayer: este _PhysicsProcess vira a PREDIÇÃO do cliente. O mesmo
/// código roda no servidor sobre a intenção recebida, e por isso precisa
/// ser determinístico — nada de aleatoriedade aqui dentro.
/// </summary>
public partial class Player : CharacterBody3D
{
	/// <summary>Quanto barulho o corpo faz (resources/body/ruido.tres, o
	/// mesmo do bot). Agachado e andando devagar não fazem passo.</summary>
	[ExportGroup("Corpo")]
	[Export] public BodyNoiseProfile Noise;

	/// <summary>Freada de quem fica preso no lugar, em m/s². Parar seco a
	/// 6,5 m/s era um tranco de câmera; assim o lobo desliza meio metro
	/// enquanto agacha para o salto, que é o embalo que se espera ver.</summary>
	[Export] public float FreezeBrake = 30f;

	/// <summary>Altura abaixo da qual o jogador é devolvido ao spawn.
	/// Sem isto, sair do mapa é queda infinita.</summary>
	[Export] public float FallLimit = -25f;

	private MeshInstance3D _mesh;
	private Node3D _head;
	private FormController _form;
	private Health _health;
	private Weapon _weapon;

	private float _gravity = 9.8f;

	/// <summary>Saiu do chão (ou está no bote) correndo de quatro: chegando
	/// com o W apertado, a aterrissagem solta e a corrida continua.</summary>
	private bool _sprintIntoAir;

	/// <summary>O que o jogador quer neste quadro (<see cref="CharacterIntent"/>):
	/// lido uma vez no começo do passo de física.</summary>
	private CharacterIntent _intent;
	private BodyNoise _noise;
	private WolfMoveMotor _moves;
	private Corpse _corpse;

	private Vector3 _spawn;
	private CollisionShape3D _collision;
	private CharacterAnimator _animator;

	/// <summary>
	/// Corpo do servidor de partida (modo autoritativo, decisão 0011):
	/// quem o move são os comandos que chegam da rede
	/// (<see cref="Simulate"/>), não o teclado. Sem câmera, som, desenho
	/// nem HUD — só o que mexe no corpo. Definido antes de entrar na cena.
	/// </summary>
	public bool Driven { get; set; }

	/// <summary>O que o jogador quis no último passo (a rede manda isto).</summary>
	public CharacterIntent Intent => _intent;

	/// <summary>Para onde a cabeça olha (rotação X, em radianos).</summary>
	public float HeadPitch => _head.Rotation.X;

	public override void _Ready()
	{
		InputSetup.EnsureActions();

		_head = GetNode<Node3D>("Head");
		_camera = GetNode<Camera3D>("Head/Camera3D");
		_baseFov = _camera.Fov;
		_springArm = GetNode<SpringArm3D>("Head/SpringArm3D");
		_wolfCamera = GetNode<Camera3D>("Head/SpringArm3D/WolfCamera");
		_mesh = GetNode<MeshInstance3D>("MeshInstance3D");
		_form = GetNode<FormController>("FormController");
		_health = GetNode<Health>("Health");
		_weapon = GetNode<Weapon>("Weapon");

		_gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
		// Na rampa e na escada o corpo anda o mesmo que no chão (sem isto o
		// Godot tira da subida o que a inclinação "come": 12% numa escada).
		FloorConstantSpeed = true;
		_spawn = GlobalPosition;

		BuildBody();
		if (Driven)
		{
			ReadyDriven();
			return;
		}

		// O braço recua sozinho quando há parede atrás — sem isso a
		// câmera atravessa o cenário durante a corrida em parede.
		_springArm.SpringLength = ThirdPersonDistance;
		_springArm.AddExcludedObject(GetRid());

		// A classe de lobo e as melhorias vêm do perfil. Cópia do recurso:
		// o original é o mesmo dos bots.
		WolfBreed breed = Profile.BuildBreed();
		if (breed != null) _form.Breed = breed;

		_form.FormChanged += OnFormChanged;
		_weapon.Fired += OnWeaponFired;
		_health.Respawned += OnRespawned;
		_health.Died += _ => OnDied();

		SetProcessInput(IsMultiplayerAuthority() && !Driven);
		ApplyCamera();

		// O desenho entre um passo da física e o outro (VisualSmoothing):
		// sem isto o corpo e a câmera andam aos trancos numa tela mais
		// rápida que a física.
		AddChild(new VisualSmoothing { Name = "VisualSmoothing" });

		// Só do dono: o cartão de duelo e a marca nos inimigos são da tela
		// de quem joga.
		// (Driven: o corpo de outra pessoa que esta máquina move, no servidor;
		// não é a tela de ninguém aqui.)
		if (IsMultiplayerAuthority() && !Driven && GetTree().CurrentScene is Node scene)
		{
			// O cartão de duelo ("abatido por", com o placar entre os dois)
			// saiu a pedido do Jhon (18/11): a câmera de abate conta isso.
			scene.CallDeferred(Node.MethodName.AddChild, new EnemyMarks { Name = "EnemyMarks" });
			// As bordas: aliado branca, inimigo vermelha (TeamOutlines).
			scene.CallDeferred(Node.MethodName.AddChild, new TeamOutlines { Name = "TeamOutlines" });
			// O rádio dos bots (com moderação).
			scene.CallDeferred(Node.MethodName.AddChild, new BotRadio { Name = "BotRadio" });
			scene.CallDeferred(Node.MethodName.AddChild, new ChargeBar { Name = "ChargeBar" });
			scene.CallDeferred(Node.MethodName.AddChild, new PounceAim { Name = "PounceAim" });
			scene.CallDeferred(Node.MethodName.AddChild, new LockMark { Name = "LockMark" });
		}

		if (IsMultiplayerAuthority() && !Driven && !UiState.AnyOpen)
			Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	/// <summary>O que move o corpo, o mesmo no jogo e no servidor.</summary>
	private void BuildBody()
	{
		_collision = GetNode<CollisionShape3D>("CollisionShape3D");
		_noise = new BodyNoise(this, Noise);
		_moves = new WolfMoveMotor(this, _weapon) { FreezeBrake = FreezeBrake };
		_corpse = new Corpse(this, _collision);

		// Duplica a forma: sem isto todos os jogadores da cena
		// compartilham a mesma cápsula e agacham juntos.
		_capsule = (CapsuleShape3D)_collision.Shape.Duplicate();
		_collision.Shape = _capsule;

		_height = StandHeight;
		_capsule.Height = StandHeight;
	}

	/// <summary>
	/// O corpo do servidor: o mesmo movimento, sem nada do que é só de
	/// quem está jogando. O que desenha (modelo, lobo, arma na mão, som)
	/// fica parado — no servidor ninguém vê, e cada passo economizado
	/// ali é partida a mais na mesma máquina.
	/// </summary>
	private void ReadyDriven()
	{
		_weapon.Puppet = true;
		_health.AutoRespawn = false;
		_camera.Current = false;
		_wolfCamera.Current = false;

		foreach (string path in new[] { "Human", "WolfVisual", "MeshInstance3D", "Head/Camera3D/ViewModel", "Head/SpringArm3D" })
			if (GetNodeOrNull(path) is Node node)
			{
				node.ProcessMode = ProcessModeEnum.Disabled;
				if (node is Node3D visual) visual.Visible = false;
			}

		// Som não existe no servidor: o nó sai inteiro (parado, ainda
		// tocaria quando um sinal chamasse).
		GetNodeOrNull("CombatantAudio")?.QueueFree();

		SetProcessInput(false);
		SetPhysicsProcess(false);
		// Morto, este corpo não dá passo (ServerBody não o move): a colisão
		// sai na morte, não ao assentar (Corpse.Drop).
		_health.Died += _ => _corpse.Drop();
		_health.Respawned += () =>
		{
			_corpse.Revive();
			ReleaseWall();
		};
	}

	/// <summary>
	/// Um passo do corpo do servidor: o comando de um passo do jogador,
	/// com a cabeça para onde ele olhava. É o mesmo passo que o jogo dele
	/// deu — por isso, com a mesma entrada, os dois chegam ao mesmo lugar.
	/// </summary>
	public void Simulate(in CharacterIntent intent, float yaw, float pitch, double delta, bool frozen = false)
	{
		Rotation = new Vector3(0, yaw, 0);
		_head.Rotation = new Vector3(pitch, 0, 0);
		_intent = intent;
		_drivenFrozen = frozen;
		Step(delta);
	}

	/// <summary>O jogador estava parado neste passo (contagem, menu): o
	/// corpo do servidor segura junto.</summary>
	private bool _drivenFrozen;

	/// <summary>Neste passo o corpo fica parado, com gravidade: só na
	/// contagem. O menu do Esc e o fim da partida não congelam: o corpo
	/// segue, com a intenção vazia (pedidos do Jhon, 17/11: a animação
	/// parava no meio de um golpe ou de um pulo).</summary>
	public bool Frozen => Driven ? _drivenFrozen : Match.IsCountdown;

	/// <summary>Põe o corpo do servidor onde o jogador nasceu (primeira
	/// leva: quem diz é o jogo), parado e fora da parede.</summary>
	public void PlaceDriven(Vector3 at)
	{
		GlobalPosition = at;
		Velocity = Vector3.Zero;
		_spawn = at;
		SpawnPoints.SetHome(this, at);

		// Já pisando: sem isto o primeiro passo é "no ar" (o corpo ainda
		// não sabe do chão), acelera mais devagar que o do jogo e os dois
		// começam a partida 7 cm separados.
		ApplyFloorSnap();
		ReleaseWall();
		_lastWall = Vector3.Zero;
		_stunTimer = 0;
		_stunSlow = 1f;
	}

	/// <summary>
	/// Morreu: câmera para trás e corpo fora da física.
	///
	/// Desligar a colisão não é enfeite. Cadáver que continua sendo um
	/// corpo sólido é um corpo que o motor precisa manter fora do
	/// cenário todo quadro, e com colisão de dois lados no mapa a
	/// resolução dessa penetração pode empurrar para o lado errado —
	/// que é o boneco afundando no chão aos poucos. Sem colisão não há
	/// o que resolver, e de quebra ninguém esbarra no morto.
	/// </summary>
	private void OnDied()
	{
		ApplyCamera();
		_cameraKick = 0;

		// Quem te matou: silhueta vermelha só para você, e a câmera vira
		// para ele até você renascer.
		_killCam?.Finish();
		_killCam = null;
		// Sem matador (queda, a própria granada), vai direto a assistir os
		// aliados (KillCam, modo espectador).
		// Só de quem joga nesta máquina: no servidor, os corpos dos outros
		// (Driven) também são "daqui", e cada morte deles abria a câmera de
		// abate na tela de quem hospeda (o "abatido por" de 19/11).
		if (IsMultiplayerAuthority() && !Driven)
			_killCam = KillCam.Begin(GetTree().CurrentScene ?? GetParent(), this,
				_health.Killer is Node3D killer && killer != this ? killer : null, GetViewport().GetCamera3D());
	}

	/// <summary>Assenta o corpo morto (<see cref="Corpse"/>).</summary>
	private void Settle(double delta) => _corpse.Settle(delta, _gravity);

	/// <summary>Parado no lugar, com gravidade. Sem a gravidade quem
	/// estivesse no ar quando a partida acabou ficaria pendurado.</summary>
	private void Hold(double delta)
	{
		ReleaseWall();
		BodyMotion.Hold(this, delta, _gravity);
	}

	/// <summary>Reviver no lugar onde se morreu devolve o jogador para
	/// dentro do tiroteio que o matou. Volta ao ponto dele na base
	/// (sempre o mesmo, veja <see cref="SpawnPoints"/>).</summary>
	private void OnRespawned()
	{
		_killCam?.Finish();
		_killCam = null;
		ApplyCamera();
		_corpse.Revive();

		GlobalPosition = SpawnPoints.Pick(this, _spawn);
		Velocity = Vector3.Zero;
		_onLadder = false;

		ReleaseWall();
		_lastWall = Vector3.Zero;
		_stunTimer = 0;
		_stunSlow = 1f;
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsMultiplayerAuthority() || Driven) return;

		// Shift ou Ctrl cujo "soltou" o sistema engoliu: solta aqui.
		StuckKeys.Watch(@event);

		// Menu aberto: o mouse é do menu. Sem isto a câmera gira atrás
		// da tela de lotes enquanto o jogador tenta clicar num cartão.
		// Esc não solta o mouse aqui: abre o menu, e quem decide o cursor
		// é o UiState.
		if (UiState.AnyOpen) return;

		MacCtrlClick.Translate(@event);

		// A mira é sempre do mouse: no modo alvo do especial, a trava segue
		// quem estiver perto do centro da tela (Player.Lock).
		if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
			Look(motion);
	}

	/// <summary>
	/// Time da sala, aplicado antes de qualquer _Ready.
	///
	/// Os filhos fazem _Ready antes do pai, e o corpo animado e a barra
	/// de vida escolhem a cor pelo time no _Ready deles. Aplicar aqui é o
	/// único jeito de eles já nascerem com a cor certa.
	/// </summary>
	public override void _EnterTree()
	{
		var health = GetNodeOrNull<Health>("Health");
		if (RoomConfig.Active && health != null) health.Team = RoomConfig.PlayerTeam;

		// O corpo do servidor não lê o perfil desta máquina: quem o cria
		// diz o personagem (Character).
		if (!Driven && health != null) Character ??= Profile.CharacterFor(health.Team);
		ApplyCharacter();
	}

	/// <summary>
	/// O personagem deste corpo (humano, lobo e vida), ou nulo para o de
	/// sempre do time. Vale se definido antes de entrar na árvore.
	/// </summary>
	public CharacterSkin Character { get; set; }

	private bool _characterApplied;

	/// <summary>
	/// Passa o personagem aos filhos antes do _Ready deles: o corpo
	/// humano e o lobo montam o modelo no _Ready, e o FormController
	/// enche a vida com o HumanHp no dele.
	/// </summary>
	private void ApplyCharacter()
	{
		if (Character == null || _characterApplied) return;
		_characterApplied = true;

		if (GetNodeOrNull<CharacterAnimator>("Human") is CharacterAnimator human)
			human.ModelOverride = Character.HumanModel;
		if (GetNodeOrNull<WolfVisual>("WolfVisual") is WolfVisual wolf)
			wolf.ModelOverride = Character.WolfModel;
		if (GetNodeOrNull<FormController>("FormController") is FormController form)
			form.HumanHp *= Character.HpMultiplier;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Driven || !IsMultiplayerAuthority()) return;

		// Menu aberto: ninguém aperta nada. O corpo continua (cai, termina o
		// golpe, a animação segue), mas uma tecla esquecida embaixo não leva
		// o personagem andando sozinho.
		// Fim de partida, o mesmo: ninguém comanda mais.
		_intent = UiState.AnyOpen || Match.IsOver ? CharacterIntent.None : CharacterIntent.FromInput();
		UpdateLock(delta);

		// Partida autoritativa: o jogo prevê com a mesma entrada que o
		// servidor vai receber — o eixo do movimento e a cabeça já
		// comprimidos como vão no pacote. Sem isto, 0,2% de diferença no
		// passo vira uma correção a cada poucos segundos.
		if (NetQuantize)
		{
			_intent = NetCommands.Quantize(_intent);
			Rotation = new Vector3(0, Quant.Yaw(Quant.Yaw(Rotation.Y)), 0);
			_head.Rotation = new Vector3(Quant.Pitch(Quant.Pitch(_head.Rotation.X)), 0, 0);
		}
		Step(delta);
	}

	/// <summary>Partida autoritativa em curso: a entrada é comprimida antes
	/// de mover (veja <see cref="NetCommands"/>).</summary>
	public static bool NetQuantize { get; set; }

	/// <summary>Um passo do corpo, com a intenção já lida (do teclado ou da rede).</summary>
	private void Step(double delta)
	{
		if (GlobalPosition.Y < FallLimit)
		{
			GlobalPosition = _spawn;
			Velocity = Vector3.Zero;
			ReleaseWall();
			_lastWall = Vector3.Zero;
			return;
		}

		if (_health.IsDead)
		{
			Settle(delta);
			return;
		}

		// Contagem: o corpo fica, a cabeça gira. Olhar em volta durante a
		// contagem é como se lê o mapa antes de valer. O menu do Esc e o
		// fim da partida não seguram o corpo: ele segue com a intenção
		// vazia (veja _PhysicsProcess).
		if (Frozen)
		{
			Hold(delta);
			return;
		}

		if (_stunTimer > 0)
		{
			_stunTimer -= delta;
			if (_stunTimer <= 0)
				_stunSlow = 1f;
		}

		BufferJump(delta);
		HandleActions(delta);

		if (IsOnFloor() || _onWall || !_form.IsWolf)
			_wolfJump = false;

		if (IsOnFloor() || _onWall || _weapon.IsDiving || _weapon.IsPounceFlying || _weapon.IsRooted)
			IsJumpAirborne = false;

		if (IsOnFloor())
		{
			ReleaseWall();
			_wallTimer = 0;
			_lastWall = Vector3.Zero;
		}

		if (_wallCooldown > 0)
			_wallCooldown -= delta;

		if (_jumpLock > 0)
			_jumpLock -= delta;

		UpdateCrouch(delta);
		UpdateSprint(delta);
		UpdateNoise(delta);
		UpdateCameraKick(delta);
		UpdateAimCamera();

		// Um corta o outro (Jhon, 20/11): o golpe que já bateu (o bote que
		// encostou, o salto que caiu) não prende o lobo se ele pula ou
		// corre de quatro — sai neste mesmo passo.
		//
		// Andar também corta (Jhon, 21/11: "W W, pula, secundário com o W
		// apertado; ao cair, o lobo corre no mesmo lugar travado"): quem
		// chega do bote apertando para onde ir quer ir, e só a corrida de
		// quatro cortava — soltar o W um instante no ar já desligava ela.
		// Quem entrou no ar correndo de quatro volta correndo de quatro.
		if (IsOnFloor() && !_weapon.IsRecovering && !_weapon.IsDiving) _sprintIntoAir = _sprinting;
		else if (_sprinting) _sprintIntoAir = true;
		bool moves = _form.IsWolf && _intent.Move.LengthSquared() > 0.01f;
		if (_weapon.IsRecovering && (JumpPending || moves))
		{
			_weapon.CutRecovery();
			if (moves && _intent.Forward && _sprintIntoAir && !_crouching) _sprinting = true;
		}

		// Os golpes especiais do lobo (bote, voo do salto, preso ao chão)
		// movem o corpo pela arma; o resto do movimento espera.
		if (_moves.Controls)
			ReleaseWall();
		if (_moves.Step(delta, _gravity))
			return;

		if (TryLadder(delta))
			return;

		if (TryWallRun(delta))
			return;

		NormalMovement(delta);
	}

	/// <summary>
	/// Publica os ruídos do corpo. O passo sai por distância
	/// percorrida, não por tempo: assim a cadência acompanha a
	/// velocidade sozinha e andar devagar faz menos barulho.
	///
	/// Agachado não emite nada. É o que dá ao agachamento um propósito
	/// além de ficar menor.
	/// </summary>
	/// <summary>Os passos dados e o ponto da passada de agora — veja
	/// <see cref="BodyNoise.StridePhase"/>.</summary>
	public int Steps => _noise.Steps;
	public float StridePhase => _noise.StridePhase;

	/// <summary>De lobo, o passo segue o clipe que está tocando: em pé, a
	/// passada do andar na direção de agora; de quatro, uma batida quando
	/// as patas se juntam (<see cref="WolfVisual.SprintBeats"/>). Sem o
	/// corpo do lobo (servidor sem modelo), vale o <see cref="BodyNoiseProfile"/>.</summary>
	private void UpdateNoise(double delta)
	{
		_wolfVisual ??= GetNodeOrNull<WolfVisual>("WolfVisual");
		WolfVisual visual = _form.IsWolf ? _wolfVisual : null;
		int beats = visual != null && IsSprinting && visual.SprintDrivesSteps ? visual.SprintBeats : -1;
		_noise.Update(delta, IsOnFloor(), Velocity, _form.IsWolf, _howling, silent: _crouching || _walking,
			sprinting: IsSprinting, stride: visual?.StepStride ?? 0f, beats: beats);
	}

	private WolfVisual _wolfVisual;
}
