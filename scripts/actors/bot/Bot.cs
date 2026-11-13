using Godot;
using System;

public enum BotState
{
	/// <summary>Não sabe de nada. Fica de guarda.</summary>
	Idle,

	/// <summary>Ouviu algo. Vai até lá sem saber o que é.</summary>
	Investigate,

	/// <summary>Vê o alvo. Atira.</summary>
	Combat,

	/// <summary>Viu e perdeu. Procura em volta da última posição.</summary>
	Search
}

/// <summary>
/// Oponente controlado por código.
///
/// Reusa Health, FormController e Weapon — os mesmos componentes do
/// jogador. Isso garante que o balanceamento medido contra o bot valha
/// contra uma pessoa; um bot com regras próprias mediria outro jogo.
///
/// Arquitetura em três camadas, que é como IA de FPS se organiza desde
/// os anos 2000:
///
///   PERCEPÇÃO  sentidos independentes (visão, audição) que alimentam
///              uma única crença: onde o alvo provavelmente está.
///   ESTADO     máquina explícita, sem comportamento implícito em
///              condicionais espalhadas.
///   AÇÃO       movimento e tiro decorrem do estado atual.
///
/// O bot nunca lê a posição do jogador direto para decidir: ele age
/// sobre a última posição conhecida. É o que produz comportamento
/// legível — ele vai aonde você estava, e você entende por quê.
///
/// Dividido nas camadas: aqui o ciclo, o estado e o cérebro
/// (<see cref="BotBrain"/>); Bot.Perception, Bot.Combat, Bot.Wolf,
/// Bot.Navigation, Bot.Cover e Bot.Patrol no resto. O corpo que ele divide
/// com o jogador (golpes do lobo, ruído, cadáver) está em
/// scripts/actors/body.
/// </summary>
public partial class Bot : CharacterBody3D
{
	/// <summary>
	/// Time deste bot. 0 mantém o que estiver no Health da cena.
	///
	/// Fica no bot, e não só no Health, porque é o bot que a cena da
	/// partida instancia e configura: sobrescrever uma propriedade de
	/// um filho de cena instanciada é frágil no formato .tscn, e uma
	/// propriedade da raiz não é.
	/// </summary>
	[ExportGroup("Bot")]
	[Export] public int Team = 0;

	/// <summary>Nome no placar e no feed. Vazio usa o nome do nó.</summary>
	[Export] public string Callsign = "";

	/// <summary>
	/// Quanto barulho o corpo faz — o mesmo arquivo do jogador
	/// (resources/body/ruido.tres). Os bots ouvem você, e você e os outros
	/// bots ouvem o bot, pela mesma regra.
	/// </summary>
	[Export] public BodyNoiseProfile Noise;

	/// <summary>De quanto em quanto tempo o cérebro decide, em segundos.</summary>
	[Export] public float DecisionInterval = 0.4f;

	/// <summary>Imprime as decisões no Output.</summary>
	[ExportGroup("Diagnóstico")]
	[Export] public bool Debug = false;

	public BotState State { get; private set; } = BotState.Idle;

	/// <summary>Ação escolhida pelo cérebro de utilidade.</summary>
	public BotAction Action { get; private set; } = BotAction.Patrol;

	private Health _health;
	private FormController _form;
	private Weapon _weapon;
	private Node3D _head;

	private float _gravity = 9.8f;
	private Vector3 _spawn;

	private CharacterAnimator _animator;
	/// <summary>O sorteio deste bot (<see cref="GameRandom"/>): mira, reação,
	/// ronda, especiais. Fluxo próprio, pelo nome.</summary>
	private GameRandom _rng;
	private BodyNoise _noise;
	private WolfMoveMotor _moves;
	private Corpse _corpse;

	private double _brainTimer;

	/// <summary>
	/// Time e nome entram aqui, e não no _Ready.
	///
	/// Os filhos fazem _Ready antes do pai. A barra de vida e o corpo
	/// animado leem o time no _Ready deles para escolher a cor — se o
	/// time só fosse aplicado no _Ready do bot, eles já teriam pintado
	/// todo mundo com o time padrão da cena. O _EnterTree do pai vem
	/// antes de tudo isso.
	/// </summary>
	public override void _EnterTree()
	{
		var health = GetNodeOrNull<Health>("Health");
		if (health == null) return;

		if (Team > 0) health.Team = Team;
		if (!string.IsNullOrEmpty(Callsign)) health.DisplayName = Callsign;
	}

	public override void _Ready()
	{
		_health = GetNode<Health>("Health");
		_form = GetNode<FormController>("FormController");
		_weapon = GetNode<Weapon>("Weapon");
		_head = GetNode<Node3D>("Head");
		_rng = MatchRandom.Stream(Name);
		_noise = new BodyNoise(this, Noise);
		_moves = new WolfMoveMotor(this, _weapon);
		_corpse = new Corpse(this, GetNode<CollisionShape3D>("CollisionShape3D"));

		_gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
		_spawn = GlobalPosition;

		AddToGroup(Groups.Bots);
		Difficulty.Apply(this);

		// Personalidade: cada bot nasce mais ou menos curioso.
		Curiosity = Mathf.Clamp(
			Curiosity + _rng.Range(-CuriositySpread, CuriositySpread), 0f, 1f);

		_health.Died += OnDied;
		_health.Respawned += OnRespawned;
		_health.Damaged += OnDamaged;

		_ear = OnNoise;
		NoiseBus.Subscribe(_ear);

		// Navegação de verdade. O desvio raso funcionava em campo
		// aberto e travava na primeira parede longa — rotacionar o
		// vetor 90 graus não contorna 24 metros de concreto.
		_nav = new NavigationAgent3D
		{
			PathDesiredDistance = 1.2f,
			TargetDesiredDistance = 1.2f,
			AvoidanceEnabled = false,
			PathMaxDistance = 6f
		};
		AddChild(_nav);
	}

	public override void _ExitTree() => NoiseBus.Unsubscribe(_ear);

	public override void _PhysicsProcess(double delta)
	{
		if (_health.IsDead)
		{
			Settle(delta);
			return;
		}

		if (!Match.IsLive)
		{
			BodyMotion.Hold(this, delta, _gravity);
			return;
		}

		UpdateNoise(delta);

		// Os golpes especiais do lobo movem o corpo pela arma, com o mesmo
		// motor do jogador.
		if (_moves.Step(delta, _gravity))
			return;

		_retargetTimer -= delta;

		bool lost = _target == null || _targetHealth == null || _targetHealth.IsDead;
		if (lost || _retargetTimer <= 0)
		{
			_retargetTimer = RetargetInterval;
			ChooseTarget();
		}

		// Ninguém vivo do outro lado. Sem isto o bot continua
		// descarregando no último corpo, o que é ruim de assistir e torna
		// o renascimento uma emboscada.
		if (_target == null || _targetHealth == null || _targetHealth.IsDead)
		{
			ForgetTarget();
			RunPatrol(delta);
			return;
		}

		_threatAge += delta;
		_beliefAge += delta;
		if (_noCoverTimer > 0) _noCoverTimer -= delta;

		UpdateReaction(delta);
		UpdateSight(delta);
		UpdateState(delta);
		UpdateBrain(delta);

		RunAction(delta);
	}

	/// <summary>Sem alvo: esquece o que sabia e volta à ronda.</summary>
	private void ForgetTarget()
	{
		_hasBelief = false;
		_memoryTimer = 0;
		_alerted = false;
		_reacting = false;

		if (State == BotState.Idle) return;

		State = BotState.Idle;
		_hasPatrolTarget = false;
		_patrolPause = PatrolPause;
	}

	/// <summary>Executa a ação que o cérebro escolheu.</summary>
	private void RunAction(double delta)
	{
		switch (Action)
		{
			case BotAction.Engage:
			case BotAction.Push:
			case BotAction.Reposition:
				RunCombat(delta);
				break;

			case BotAction.Reload:
				_weapon.TryReload();
				RunCombat(delta);
				break;

			case BotAction.Transform:
				_weapon.SelectSlot(Slot.Wolf);
				RunCombat(delta);
				break;

			case BotAction.Revert:
				_weapon.SelectSlot(Slot.Primary);
				RunCombat(delta);
				break;

			case BotAction.Howl:
				RunHowl(delta);
				break;

			case BotAction.Retreat:
				RunRetreat(delta);
				break;

			case BotAction.TakeCover:
				RunTakeCover(delta);
				break;

			case BotAction.Investigate:
				RunSearch(delta);
				break;

			default:
				RunPatrol(delta);
				break;
		}
	}

	/// <summary>
	/// Monta a fotografia do mundo e pede uma ação ao cérebro.
	///
	/// A decisão sai em intervalos, não a cada frame: pensar sessenta
	/// vezes por segundo custa caro e produz um bot que muda de ideia
	/// antes de terminar o que começou.
	/// </summary>
	private void UpdateBrain(double delta)
	{
		_brainTimer -= delta;
		if (_brainTimer > 0 || _howlLeft > 0) return;

		_brainTimer = DecisionInterval;

		bool canSee = CanSeeTarget();
		float distance = _target != null
			? GlobalPosition.DistanceTo(_target.GlobalPosition)
			: 999f;

		int mag = _weapon.CurrentSlot == Slot.Secondary
			? _weapon.SecondaryMag
			: _weapon.PrimaryMag;

		int ammo = _weapon.CurrentSlot == Slot.Secondary
			? _weapon.SecondaryAmmo
			: _weapon.PrimaryAmmo;

		_hasCover = canSee && !_form.IsWolf && _noCoverTimer <= 0 && FindCover(out _cover);

		var context = new BotContext
		{
			HasTarget = _target != null,
			CanSee = canSee,
			Distance = distance,
			HealthRatio = _health.MaxHp > 0 ? _health.Hp / _health.MaxHp : 1f,
			ShieldRatio = _health.MaxShield > 0 ? _health.Shield / _health.MaxShield : 0f,
			AmmoRatio = mag > 0 ? (float)ammo / mag : 1f,
			MagEmpty = ammo <= 0,
			IsWolf = _form.IsWolf,
			CanTransform = !_form.IsWolf && _form.Breed != null
				&& RoomConfig.Rules.Transform != TransformRule.Bloqueada,
			ThreatAge = (float)_threatAge,
			BeliefAge = (float)_beliefAge,
			HasBelief = _hasBelief && _memoryTimer > 0,
			Aggression = Aggression,
			PreferredRange = PreferredRange,
			HasCover = _hasCover,

			// Protegido agora: o alvo existe e não há linha até ele.
			InCover = _target != null && !canSee && distance < SightRange
		};

		BotAction next = BotBrain.Choose(context, Action);

		if (Debug && next != Action)
			GD.Print($"{Name}: {Action} -> {next}");

		if (next == BotAction.Howl && Action != BotAction.Howl)
			_howlLeft = WolfMoves.Duration(WolfMove.Howl);

		Action = next;

		ShuffleAim();
		UpdateStrafe(delta);
	}

	/// <summary>Mesma regra do jogador (<see cref="BodyNoise"/>): o bot
	/// não agacha nem anda devagar, então o passo sempre sai.</summary>
	private void UpdateNoise(double delta) =>
		_noise.Update(delta, IsOnFloor(), Velocity, _form.IsWolf, howling: Action == BotAction.Howl, silent: false);

	/// <summary>
	/// Transições explícitas. Ter os estados nomeados vale mais que a
	/// economia de um punhado de condicionais: o comportamento fica
	/// legível para quem depura e previsível para quem joga.
	/// </summary>
	private void UpdateState(double delta)
	{
		BotState previous = State;

		if (CanSeeTarget())
		{
			State = BotState.Combat;
			_scanTimer = 0;
		}
		else if (State == BotState.Combat)
		{
			// Perdeu de vista: vai até onde o alvo estava.
			State = BotState.Search;
			_scanTimer = ScanTime;
		}
		else if (_hasBelief && _memoryTimer > 0)
		{
			if (State != BotState.Search)
				State = BotState.Investigate;
		}
		else if (_memoryTimer <= 0)
		{
			if (State != BotState.Idle)
			{
				State = BotState.Idle;
				_hasPatrolTarget = false;
				_patrolPause = PatrolPause;
			}

			_hasBelief = false;
		}

		if (Debug && State != previous)
			GD.Print($"{Name}: {previous} -> {State}");
	}

	/// <summary>Assenta o corpo morto (<see cref="Corpse"/>).</summary>
	private void Settle(double delta) => _corpse.Settle(delta, _gravity);

	private void OnDied(int killerId)
	{
		if (Debug) GD.Print($"{Name} morreu");

		_memoryTimer = 0;
		_vigilTimer = 0;
		_route = -1;
		_hasBelief = false;
		_alerted = false;
		_reacting = false;
		_hasPatrolTarget = false;
		_howlLeft = 0;
		State = BotState.Idle;
	}

	private void OnRespawned()
	{
		_corpse.Revive();

		GlobalPosition = SpawnPoints.Pick(this, _spawn);
		Velocity = Vector3.Zero;

		if (_form.IsWolf)
			_weapon.SelectSlot(Slot.Primary);
	}
}
