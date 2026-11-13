using Godot;
using static AnimationKit;
using System.Collections.Generic;

/// <summary>
/// O corpo do lobo: o Mutant do Mixamo, animado com o Creature Pack.
/// Cada time tem o seu — o azul usa <c>mutant.fbx</c>, o laranja
/// <c>mutant_red.fbx</c> (decisão do Jhon).
///
/// Fica no corpo e não na câmera porque a forma lobo é em terceira
/// pessoa: o dono vê o próprio lobo, e os outros reconhecem a silhueta
/// de longe — curvado, largo, braços compridos. Numa luta rápida
/// ninguém lê textura; o contorno é o que separa lobo de humano a
/// trinta metros.
///
/// Mesma regra do <see cref="CharacterAnimator"/>: **lê** estado e
/// nunca decide. A velocidade escolhe entre parado, andando e correndo;
/// os sinais da arma disparam a garrada, o salto e o bote; o uivo (R)
/// toca o rugido da cintura para cima, por cima do andar — o lobo ruge
/// em movimento. Virar lobo não tem animação (decisão do Jhon): o lobo
/// aparece já andando.
///
/// **O pulo usa só o voo do "mutant jumping"**: o clipe agacha, salta e
/// aterrissa sozinho, e no jogo quem salta é a física, no quadro do
/// clique — tocado inteiro, o bicho pularia duas vezes. Então o agachar
/// fica de fora, e o voo não corre no relógio: anda pela velocidade
/// vertical do corpo (saindo do chão, no topo, caindo), e o quadril fica
/// na altura de pé (quem sobe é a cápsula). Chegou devagar, toca a
/// aterrissagem; chegou correndo, volta direto para a corrida — agachar
/// com o corpo a 5 m/s faria o pé patinar.
///
/// | Clipe | Arquivo do pack | Quando |
/// |---|---|---|
/// | idle | mutant breathing idle | parado |
/// | walk / run | mutant walking / run | andando, correndo |
/// | claw | mutant swiping | garrada (clique), alternando com o punch |
/// | heavy | mutant punch | a outra garrada |
/// | pounce | mutant jump attack, inteiro | salto (direito no chão) |
/// | dive | mutant jump attack, só a queda | bote (direito no ar), até encostar |
/// | dive_land | mutant jump attack, da pancada ao fim | bote, ao encostar |
/// | howl | mutant roaring | uivando (R), da cintura para cima |
/// | jump | mutant jumping, só o voo | no ar depois de um pulo (do chão ou da parede) |
/// | jump_land | mutant jumping, a aterrissagem | tocou o chão depois do pulo, devagar |
/// | wallrun | Running Crawl | correndo na parede, de barriga para ela |
/// | death | mutant dying | morreu de lobo |
///
/// Os golpes e o rugido tocam o trecho e a velocidade de
/// <see cref="WolfMoves"/>, a mesma tabela que a arma usa para o dano
/// sair no quadro do golpe. Nenhum golpe corta outro. As garradas só
/// mexem da cintura para cima — o lobo continua correndo enquanto bate
/// —; o salto e o bote, que prendem o lobo, mexem o corpo inteiro.
///
/// Dividido em montar e tocar: WolfVisual.Model (modelo e clipes) e
/// WolfVisual.Tree (a árvore) rodam uma vez; WolfVisual.Locomotion,
/// .Air e .Moves leem o estado a cada quadro.
/// </summary>
public partial class WolfVisual : Node3D
{
	/// <summary>O Mutant de cada time.</summary>
	[ExportGroup("Modelo")]
	[Export(PropertyHint.File, "*.fbx,*.glb,*.tscn")]
	public string BlueModel = "res://assets/characters/wolf/mutant.fbx";

	[Export(PropertyHint.File, "*.fbx,*.glb,*.tscn")]
	public string OrangeModel = "res://assets/characters/wolf/mutant_red.fbx";

	[Export(PropertyHint.Dir)]
	public string ClipFolder = "res://assets/characters/wolf";

	/// <summary>O Mutant tem 1,86 m; o lobo fica um pouco maior que
	/// um homem.</summary>
	[Export] public float ModelScale = WolfMoves.BodyScale;

	/// <summary>Mixamo olha para +Z; o corpo, para −Z.</summary>
	[Export] public float ModelYaw = 180f;

	/// <summary>Imprime os clipes carregados e os golpes tocados.</summary>
	[ExportGroup("Diagnóstico")]
	[Export] public bool Debug;

	private FormController _form;
	private Weapon _weapon;
	private Health _health;
	private CharacterBody3D _body;
	private Player _player;
	private Bot _bot;

	private Node3D _model;
	private AnimationPlayer _anims;
	private AnimationTree _tree;
	private AnimationNodeStateMachinePlayback _playback;

	private bool _dead;
	private double _deadTimer;

	public override void _Ready()
	{
		_body = GetParent<CharacterBody3D>();
		_form = _body.GetNode<FormController>("FormController");
		_weapon = _body.GetNode<Weapon>("Weapon");
		_health = _body.GetNodeOrNull<Health>("Health");
		_player = _body as Player;
		_bot = _body as Bot;

		if (!BuildModel()) return;

		_form.FormChanged += OnFormChanged;
		_weapon.WolfMoveStarted += OnMove;
		_weapon.WolfDiveLanded += OnDiveLanded;
		if (_health != null)
		{
			_health.Died += _ => OnDied();
			_health.Respawned += OnRespawned;
		}

		Visible = _form.IsWolf;
	}

	public override void _Process(double delta)
	{
		if (_tree == null || !Visible) return;

		// O lobo da cena fica no pé da cápsula, que encolhe ao agachar.
		float feet = _player != null ? _player.FeetOffset : -0.875f;
		Position = new Vector3(0, feet + CrawlLift * _crouch * _moveMix, 0);

		if (_dead)
		{
			// Depois de cair, o corpo some.
			_deadTimer += delta;
			float hold = (_lengths.TryGetValue("death", out float d) ? d : 1.5f) + 1.2f;
			if (_deadTimer > hold) _model.Visible = false;
			return;
		}

		UpdateLocomotion((float)delta);

		// No ar depois de um pulo, o voo do clipe de pulo; caindo de uma
		// beirada, o corpo segue na pose de antes.
		bool onWall = _player != null && _player.IsWallRunning && Has("wallrun");

		// No ar o lobo cai, não anda: depois de um pulo (do chão ou da
		// parede) e também soltando da parede ou caindo de uma beirada.
		// Um instante no ar descendo rampa ou degrau não conta.
		_airTime = _body.IsOnFloor() ? 0f : _airTime + (float)delta;
		bool jumped = _player != null && _player.IsJumpAirborne;
		bool falling = !_body.IsOnFloor() && (_airTime > 0.12f || _body.Velocity.Y < -2f);
		bool special = _weapon.IsDiving || _weapon.IsPounceFlying || _weapon.IsRooted;
		bool jumping = !onWall && !special && (jumped || falling) && Has("jump");
		if (onWall != _onWall || jumping != _jumping)
		{
			// Acabou o pulo no chão: chegou devagar, aterrissa; chegou
			// correndo, a corrida já é a aterrissagem.
			if (_jumping && !jumping && !onWall && _body.IsOnFloor() && _speed < MovingSpeed && !_weapon.IsStriking)
				Fire("jump_land", JumpLandSpeed);

			_onWall = onWall;
			_jumping = jumping;
			_playback?.Travel(onWall ? "Wall" : jumping ? "Jump" : "Locomotion");
		}

		if (_jumping) UpdateJump();

		// Aterrissando e já saiu andando: a aterrissagem sai.
		if (Has("jump_land") && _speed > MovingSpeed
			&& (bool)_tree.Get("parameters/JumpLand/active"))
			_tree.Set("parameters/JumpLand/request", (int)AnimationNodeOneShot.OneShotRequest.FadeOut);

		// O rugido vai por cima do andar, uma vez, na velocidade de
		// WolfMoves: acaba junto com o escudo voltando. Soltou R antes,
		// ele sai.
		bool howling = IsHowling();
		if (howling != _howling)
		{
			_howling = howling;
			if (howling) Fire("howl", WolfMoves.Of(WolfMove.Howl).Speed);
			else if (Has("howl")) _tree.Set("parameters/Howl/request", (int)AnimationNodeOneShot.OneShotRequest.FadeOut);
		}

		UpdateCharge(delta);
		UpdateLean((float)delta);
		UpdateWall((float)delta);
	}

	private void OnFormChanged(int form)
	{
		Visible = _form.IsWolf;

		if ((Form)form == Form.ShiftingToWolf)
		{
			_model.Visible = true;
			ResetLocomotion();
			_playback?.Start("Locomotion");
		}
	}

	private void OnDied()
	{
		if (!_form.IsWolf) return;
		_dead = true;
		_deadTimer = 0;
		_playback?.Travel("Death");
	}

	private void OnRespawned()
	{
		_dead = false;
		_model.Visible = true;
		_howling = false;
		_onWall = false;
		_jumping = false;
		_wallBlend = 0;
		ResetLocomotion();
		_playback?.Start("Locomotion");
		Visible = _form.IsWolf;
	}
}
