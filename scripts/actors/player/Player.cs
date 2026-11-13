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

	/// <summary>O que o jogador quer neste quadro (<see cref="CharacterIntent"/>):
	/// lido uma vez no começo do passo de física.</summary>
	private CharacterIntent _intent;
	private BodyNoise _noise;
	private WolfMoveMotor _moves;
	private Corpse _corpse;

	private Vector3 _spawn;
	private CollisionShape3D _collision;
	private CharacterAnimator _animator;

	public override void _Ready()
	{
		InputSetup.EnsureActions();

		_head = GetNode<Node3D>("Head");
		_camera = GetNode<Camera3D>("Head/Camera3D");
		_springArm = GetNode<SpringArm3D>("Head/SpringArm3D");
		_wolfCamera = GetNode<Camera3D>("Head/SpringArm3D/WolfCamera");
		_mesh = GetNode<MeshInstance3D>("MeshInstance3D");
		_form = GetNode<FormController>("FormController");
		_health = GetNode<Health>("Health");
		_weapon = GetNode<Weapon>("Weapon");

		_gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
		_spawn = GlobalPosition;

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

		// O braço recua sozinho quando há parede atrás — sem isso a
		// câmera atravessa o cenário durante a corrida em parede.
		_springArm.SpringLength = ThirdPersonDistance;
		_springArm.AddExcludedObject(GetRid());

		// A classe de lobo e as tatuagens vêm do perfil. Cópia do recurso:
		// o original é o mesmo dos bots.
		WolfBreed breed = Profile.BuildBreed();
		if (breed != null) _form.Breed = breed;

		_form.FormChanged += OnFormChanged;
		_weapon.Fired += OnWeaponFired;
		_health.Respawned += OnRespawned;
		_health.Died += _ => OnDied();

		SetProcessInput(IsMultiplayerAuthority());
		ApplyCamera();

		// Só do dono: o cartão de duelo e a marca nos inimigos são da tela
		// de quem joga.
		if (IsMultiplayerAuthority() && GetTree().CurrentScene is Node scene)
		{
			scene.CallDeferred(Node.MethodName.AddChild, new DuelCard { Name = "DuelCard" });
			scene.CallDeferred(Node.MethodName.AddChild, new EnemyMarks { Name = "EnemyMarks" });
			scene.CallDeferred(Node.MethodName.AddChild, new ChargeBar { Name = "ChargeBar" });
		}

		if (IsMultiplayerAuthority() && !UiState.AnyOpen)
			Input.MouseMode = Input.MouseModeEnum.Captured;
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
		if (IsMultiplayerAuthority() && _health.Killer is Node3D killer && killer != this)
			_killCam = KillCam.Begin(GetTree().CurrentScene ?? GetParent(), this, killer, GetViewport().GetCamera3D());
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
	/// dentro do tiroteio que o matou. Volta ao spawn.</summary>
	private void OnRespawned()
	{
		_killCam?.Finish();
		_killCam = null;
		ApplyCamera();
		_corpse.Revive();

		GlobalPosition = SpawnPoints.Pick(this, _spawn);
		Velocity = Vector3.Zero;

		ReleaseWall();
		_lastWall = Vector3.Zero;
		_stunTimer = 0;
		_stunSlow = 1f;
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsMultiplayerAuthority()) return;

		// Menu aberto: o mouse é do menu. Sem isto a câmera gira atrás
		// da tela de lotes enquanto o jogador tenta clicar num cartão.
		// Esc não solta o mouse aqui: abre o menu, e quem decide o cursor
		// é o UiState.
		if (UiState.AnyOpen) return;

		MacCtrlClick.Translate(@event);

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
		if (!RoomConfig.Active) return;

		var health = GetNodeOrNull<Health>("Health");
		if (health != null) health.Team = RoomConfig.PlayerTeam;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsMultiplayerAuthority()) return;

		_intent = CharacterIntent.FromInput();

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

		// Contagem e fim de partida: o corpo fica, a cabeça gira. Olhar
		// em volta durante a contagem é como se lê o mapa antes de valer.
		// Menu aberto também segura o corpo. O menu do Esc não pausa o
		// jogo — a partida continua e você continua levando tiro —, mas
		// uma tecla esquecida pressionada não pode levar o personagem
		// andando sozinho enquanto o mouse está no menu.
		if (!Match.IsLive || UiState.AnyOpen)
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
		UpdateNoise(delta);
		UpdateCameraKick(delta);

		// Os golpes especiais do lobo (bote, voo do salto, preso ao chão)
		// movem o corpo pela arma; o resto do movimento espera.
		if (_moves.Controls)
			ReleaseWall();
		if (_moves.Step(delta, _gravity))
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
	private void UpdateNoise(double delta) =>
		_noise.Update(delta, IsOnFloor(), Velocity, _form.IsWolf, _howling, silent: _crouching || _walking);
}
