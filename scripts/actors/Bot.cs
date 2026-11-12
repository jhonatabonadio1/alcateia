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
/// </summary>
public partial class Bot : CharacterBody3D
{
    // --- Percepção: visão ---

    [Export] public float SightRange = 40f;

    /// <summary>
    /// Time deste bot. 0 mantém o que estiver no Health da cena.
    ///
    /// Fica no bot, e não só no Health, porque é o bot que a cena da
    /// partida instancia e configura: sobrescrever uma propriedade de
    /// um filho de cena instanciada é frágil no formato .tscn, e uma
    /// propriedade da raiz não é.
    /// </summary>
    [Export] public int Team = 0;

    /// <summary>Nome no placar e no feed. Vazio usa o nome do nó.</summary>
    [Export] public string Callsign = "";

    /// <summary>Intervalo entre reavaliações de alvo, em segundos.</summary>
    [Export] public float RetargetInterval = 0.5f;

    /// <summary>
    /// Vantagem, em metros, do alvo atual sobre um candidato novo.
    ///
    /// Sem ela, dois inimigos a distâncias parecidas fazem o bot trocar
    /// de alvo a cada reavaliação, e ele passa a luta girando a cabeça
    /// entre os dois sem acertar nenhum.
    /// </summary>
    [Export] public float TargetStickiness = 6f;

    /// <summary>Campo de visão em graus, total.</summary>
    [Export] public float FieldOfView = 140f;

    /// <summary>Tempo que continua acreditando no alvo depois de
    /// perdê-lo de vista.</summary>
    [Export] public float Memory = 3f;

    /// <summary>
    /// Tempo que o bot segue vigiando de onde veio o tiro, depois de
    /// ter se virado.
    ///
    /// A memória normal de três segundos serve para perder alguém de
    /// vista numa troca de tiros. Levar um tiro pelas costas é outra
    /// coisa: ali existe alguém, e o bot sabe disso. Esquecer em três
    /// segundos faz ele voltar a patrulhar de costas para o mesmo
    /// corredor, que é o comportamento que faz um bot parecer burro.
    /// </summary>
    [Export] public float Vigilance = 7f;

    /// <summary>Impulso do pulo do bot. O mesmo do humano: bot que
    /// pula mais alto que o jogador alcança lugares que o jogador não
    /// alcança, e isso não é dificuldade, é trapaça.</summary>
    [Export] public float JumpVelocity = 4.4f;

    /// <summary>Chance por segundo de o lobo do bot usar um golpe especial
    /// quando dá (o salto carregado de 4 a 11 m, o pulo com bote de perto).
    /// Sobe com a dificuldade: no fácil o lobo quase só corre e arranha.</summary>
    [Export] public float WolfSpecialRate = 0.35f;

    /// <summary>Erro da carga do salto do bot, em fração da carga: quanto
    /// ele erra o alcance. Nunca zero — um salto que sempre cai na cabeça
    /// é injusto, não difícil.</summary>
    [Export] public float PounceError = 0.12f;

    /// <summary>
    /// Ruído dos passos, pouso e uivo do bot — os mesmos raios do
    /// Player. Antes o bot andava em silêncio: os bots ouviam você, mas
    /// nem você nem os outros bots ouviam o bot. Agora o que ele faz de
    /// barulho entra no mesmo barramento, e o som que você ouve dele é
    /// esse ruído.
    /// </summary>
    [ExportGroup("Ruído")]
    [Export] public float StepNoiseHuman = 13f;
    [Export] public float StepNoiseWolf = 20f;
    [Export] public float LandNoise = 17f;
    [Export] public float HowlNoise = 45f;
    [Export] public float StepDistance = 2.2f;
    [ExportGroup("")]

    /// <summary>Altura máxima do obstáculo que o bot tenta vencer
    /// pulando.</summary>
    [Export] public float JumpMaxHeight = 1.1f;

    /// <summary>Distância à frente em que ele procura o obstáculo.</summary>
    [Export] public float JumpProbe = 1.1f;

    // --- Percepção: audição ---

    /// <summary>Multiplicador do raio dos ruídos. Abaixo de 1 o bot é
    /// surdo para sons distantes.</summary>
    [Export] public float Hearing = 1f;

    /// <summary>Erro ao localizar um som, em metros. Ouvir não é ver:
    /// o bot vai à direção certa, não ao ponto exato.</summary>
    [Export] public float HearingError = 2.5f;

    /// <summary>
    /// Tendência a ir atrás do que ouviu, de 0 a 1.
    ///
    /// Sorteada por bot no início, dentro de uma faixa. Dois bots
    /// idênticos reagindo igual ao mesmo tiro entregam que são
    /// scripts; um indo e outro ficando parece decisão. É o mesmo
    /// motivo do erro de mira ter variação: regularidade é o que
    /// denuncia a máquina.
    /// </summary>
    [Export] public float Curiosity = 0.6f;

    /// <summary>Variação sorteada em volta da curiosidade base.</summary>
    [Export] public float CuriositySpread = 0.35f;

    // --- Movimento ---

    [Export] public float Speed = 3.0f;
    [Export] public float TurnSpeed = 8f;

    /// <summary>Distância que tenta manter do alvo na forma humana.</summary>
    [Export] public float PreferredRange = 12f;

    /// <summary>Raio de chegada ao destino de investigação.</summary>
    [Export] public float ArriveRadius = 2.5f;

    /// <summary>Tempo olhando em volta ao chegar no destino.</summary>
    [Export] public float ScanTime = 2.5f;

    // --- Cobertura ---
    //
    // Nenhum ponto é marcado no mapa. O bot descobre parede testando:
    // se de um ponto a linha até o alvo está bloqueada, aquele ponto é
    // cobertura. Funciona em qualquer geometria, inclusive num .glb
    // que ninguém anotou.

    /// <summary>Quantas posições testar ao procurar parede.</summary>
    [Export] public int CoverSamples = 12;

    /// <summary>Até onde procurar.</summary>
    [Export] public float CoverSearchRadius = 11f;

    /// <summary>Distância mínima do alvo que uma cobertura precisa ter.
    /// Parede colada no inimigo não protege de nada.</summary>
    [Export] public float CoverMinDistance = 6f;

    // --- Patrulha ---
    //
    // Bot parado no spawn denuncia que é script. Andar sem rumo também
    // é informação para o jogador: um bot caminhando longe é uma
    // oportunidade, e um bot chegando é uma ameaça.

    /// <summary>Raio da área patrulhada, medido do centro do mapa.</summary>
    [Export] public float PatrolRadius = 16f;

    /// <summary>Centro da área patrulhada.</summary>
    [Export] public Vector3 PatrolCenter = Vector3.Zero;

    /// <summary>Pausa entre um destino e o próximo.</summary>
    [Export] public float PatrolPause = 1.5f;

    /// <summary>Fração da velocidade ao patrulhar. Andar devagar faz
    /// menos barulho e deixa o bot menos previsível de ouvir.</summary>
    [Export] public float PatrolSpeedFactor = 0.55f;

    /// <summary>Tempo preso no mesmo lugar antes de desistir do
    /// destino. Sem pathfinding, encostar numa quina é questão de
    /// tempo — o que não pode é ficar empurrando a parede para sempre.</summary>
    [Export] public float StuckTime = 1.2f;

    // --- Decisão de forma ---
    //
    // O bot é um atirador. Virar lobo é recurso de desespero: como o
    // lobo é corpo-a-corpo, um bot que transforma cedo gruda no
    // jogador e nunca mais atira.

    [Export] public float WolfHealthThreshold = 0.4f;
    [Export] public float WolfRange = 14f;
    [Export] public float HumanRange = 20f;
    [Export] public float DecisionInterval = 0.4f;

    // --- Combate ---

    [Export] public float BurstTime = 0.7f;
    [Export] public float BurstPause = 0.6f;
    [Export] public float EngageRange = 45f;
    [Export] public float StrafeFactor = 0.8f;
    [Export] public float StrafeInterval = 1.4f;

    /// <summary>
    /// Quanto o bot avança em vez de circular. Deathmatch não premia
    /// quem administra distância: premia quem vai atrás. Em 1 ele
    /// fecha a distância enquanto atira, mantendo o strafe só como
    /// movimento lateral.
    /// </summary>
    [Export] public float Aggression = 0.75f;

    /// <summary>Distância abaixo da qual ele para de avançar. Colar no
    /// alvo com arma de fogo é pior que manter um palmo.</summary>
    [Export] public float MinRange = 4f;

    /// <summary>Erro de mira em graus. Zero faz um bot desumano.</summary>
    [Export] public float AimError = 4f;

    /// <summary>Fração dos tiros que erra de propósito, com desvio bem
    /// maior. Nem no difícil vai a zero: um bot que acerta sempre não
    /// é difícil, é injusto.</summary>
    [Export] public float MissChance = 0.2f;

    [Export] public float MissSpread = 14f;

    // --- Reação a levar tiro ---

    [Export] public float ReactionTime = 0.35f;
    [Export] public float ReactionJitter = 0.2f;
    [Export] public float AlertTurnSpeed = 4.5f;

    [Export] public bool Debug = false;

    public BotState State { get; private set; } = BotState.Idle;

    /// <summary>Ação escolhida pelo cérebro de utilidade.</summary>
    public BotAction Action { get; private set; } = BotAction.Patrol;

    private Health _health;
    private FormController _form;
    private Weapon _weapon;
    private Node3D _head;
    private Node3D _target;
    private Health _targetHealth;
    private double _retargetTimer;

    private float _gravity = 9.8f;
    private Vector3 _spawn;

    /// <summary>Onde o bot acredita que o alvo está. Toda ação sai
    /// daqui, nunca da posição real.</summary>
    private Vector3 _lastKnown;
    private bool _hasBelief;

    private double _memoryTimer;
    private double _vigilTimer;
    private CharacterAnimator _animator;
    private CollisionShape3D _deadShape;
    private bool _settled;

    /// <summary>Rota de ronda atual e em que ponto dela o bot está.
    /// −1 significa que ainda não pegou nenhuma.</summary>
    private int _route = -1;
    private int _leg;
    private int _legStep = 1;
    private double _scanTimer;
    private double _burstTimer;
    private bool _firing;
    private double _strafeTimer;
    private int _strafeSide = 1;
    private double _reactionTimer;
    private bool _reacting;
    private bool _alerted;
    private Vector3 _aimOffset;
    private float _scanAngle;

    private Action<Noise> _ear;
    private NavigationAgent3D _nav;
    private Vector3 _cover;
    private bool _hasCover;
    private double _threatAge = 99;
    private double _beliefAge = 99;
    private double _brainTimer;

    /// <summary>Tempo que falta do rugido. Rugir é compromisso, como
    /// para o jogador: o bot só volta a decidir depois do fim.</summary>
    private double _howlLeft;

    private Vector3 _patrolTarget;
    private bool _hasPatrolTarget;
    private double _patrolPause;
    private double _stuckTimer;
    private Vector3 _lastPosition;

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

        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
        _spawn = GlobalPosition;

        AddToGroup("bot");
        Difficulty.Apply(this);

        // Personalidade: cada bot nasce mais ou menos curioso.
        Curiosity = Mathf.Clamp(
            Curiosity + (float)GD.RandRange(-CuriositySpread, CuriositySpread), 0f, 1f);

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
            Fall(delta);
            return;
        }

        // Os golpes especiais do lobo movem o corpo, como no jogador.
        if (_weapon.IsDiving)
        {
            BotDive();
            return;
        }

        if (_weapon.IsPounceFlying)
        {
            Vector3 next = _weapon.PounceFlightPoint(delta);
            Velocity = ((next - GlobalPosition) / (float)delta).LimitLength(28f);
            MoveAndSlide();
            return;
        }

        if (_weapon.IsRooted)
        {
            Vector3 frozen = Velocity;
            Vector3 flat = _weapon.IsStepping ? _weapon.StepVelocity : new Vector3(frozen.X, 0, frozen.Z).MoveToward(Vector3.Zero, 30f * (float)delta);
            Velocity = new Vector3(flat.X, IsOnFloor() ? 0 : frozen.Y - _gravity * (float)delta, flat.Z);
            MoveAndSlide();
            return;
        }

        UpdateNoise(delta);

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
            _hasBelief = false;
            _memoryTimer = 0;
            _alerted = false;
            _reacting = false;

            if (State != BotState.Idle)
            {
                State = BotState.Idle;
                _hasPatrolTarget = false;
                _patrolPause = PatrolPause;
            }

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

    /// <summary>Rugir: parado, recuperando escudo.</summary>
    private bool _wasAirborne;
    private float _stepDistance;
    private double _howlNoiseTimer;

    /// <summary>Mesma regra do Player: passo por distância percorrida,
    /// baque ao pousar, uivo a cada meio segundo enquanto dura.</summary>
    private void UpdateNoise(double delta)
    {
        bool grounded = IsOnFloor();

        if (grounded && _wasAirborne)
            NoiseBus.Emit(GlobalPosition, LandNoise, NoiseKind.Land, this);
        _wasAirborne = !grounded;

        if (Action == BotAction.Howl)
        {
            _howlNoiseTimer -= delta;
            if (_howlNoiseTimer <= 0)
            {
                _howlNoiseTimer = 0.5;
                NoiseBus.Emit(GlobalPosition, HowlNoise, NoiseKind.Howl, this);
            }
        }
        else
        {
            _howlNoiseTimer = 0;
        }

        if (!grounded) return;

        var flat = new Vector3(Velocity.X, 0, Velocity.Z);
        if (flat.Length() < 0.5f) return;

        _stepDistance += flat.Length() * (float)delta;
        if (_stepDistance < StepDistance) return;
        _stepDistance = 0;

        NoiseBus.Emit(
            GlobalPosition,
            _form.IsWolf ? StepNoiseWolf : StepNoiseHuman,
            NoiseKind.Step,
            this);
    }

    /// <summary>
    /// Rugido inteiro, parado, e o escudo cheio só no fim — a mesma regra
    /// do jogador, no mesmo tempo da animação (<see cref="WolfMoves"/>).
    /// </summary>
    private void RunHowl(double delta)
    {
        MoveTowards(Vector3.Zero, Speed, delta);

        if (!_form.IsWolf || _health.IsDead)
        {
            _howlLeft = 0;
            Action = BotAction.Engage;
            return;
        }

        _howlLeft -= delta;
        if (_howlLeft > 0) return;

        _health.RestoreShield(_health.MaxShield);
        _howlLeft = 0;
        Action = BotAction.Engage;
        _brainTimer = 0;
    }

    /// <summary>
    /// Procura um ponto de onde o alvo não enxerga.
    ///
    /// Testa posições num anel em volta: de cada candidato, dispara um
    /// raio até o peito do alvo. Bloqueado significa parede no meio, e
    /// parede no meio é cobertura — sem precisar que alguém tenha
    /// marcado nada no mapa.
    ///
    /// Vence o candidato mais perto do bot: fugir para o outro lado do
    /// mapa não é se proteger, é desaparecer da luta.
    /// </summary>
    private bool FindCover(out Vector3 point)
    {
        point = Vector3.Zero;

        if (_target == null) return false;

        Vector3 chest = _target.GlobalPosition + Vector3.Up * 0.5f;
        float best = float.MaxValue;
        bool found = false;

        for (int i = 0; i < CoverSamples; i++)
        {
            float angle = Mathf.Tau * i / CoverSamples;
            float radius = CoverSearchRadius * (0.45f + 0.55f * ((i % 3) / 2f));

            Vector3 candidate = GlobalPosition + new Vector3(
                Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);

            if (candidate.DistanceTo(_target.GlobalPosition) < CoverMinDistance)
                continue;

            var query = PhysicsRayQueryParameters3D.Create(
                candidate + Vector3.Up * 0.5f, chest);

            var exclude = new Godot.Collections.Array<Rid> { GetRid() };

            if (_target is CollisionObject3D body)
                exclude.Add(body.GetRid());

            query.Exclude = exclude;

            // Linha livre significa que dali ainda se é visto.
            if (GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0)
                continue;

            float distance = GlobalPosition.DistanceTo(candidate);
            if (distance >= best) continue;

            best = distance;
            point = candidate;
            found = true;
        }

        return found;
    }

    private void RunTakeCover(double delta)
    {
        if (!_hasCover)
        {
            RunCombat(delta);
            return;
        }

        Vector3 step = PathTo(_cover);

        // Atira indo para a parede: correr para o esconderijo sem revidar
        // entregava o duelo. E parado sem caminho, ou "chegou" e ainda vê
        // o alvo, a parede não protege: desiste dela por um tempo e luta.
        // Era o defeito dos bots que paravam de atirar — dois bots a 3 m,
        // os dois "indo para a parede", parados e se encarando por meio
        // minuto.
        bool sees = _target != null && CanSeeTarget();
        if (sees)
        {
            UpdateBurst(delta);
            Vector3 to = _target.GlobalPosition - GlobalPosition;
            Engage(new Vector3(to.X, 0, to.Z).Length());
        }

        if (step == Vector3.Zero)
        {
            if (sees)
            {
                _noCoverTimer = NoCoverTime;
                _hasCover = false;
            }

            if (_target != null)
                FaceDirection(_target.GlobalPosition, delta);

            MoveTowards(Vector3.Zero, Speed, delta);
            return;
        }

        if (_target != null)
            FaceDirection(_target.GlobalPosition, delta);

        MoveTowards(step, Speed, delta);
    }

    /// <summary>Quanto tempo o bot esquece a ideia de parede depois de uma
    /// que não protegeu (sem caminho, ou chegou e ainda vê o alvo).</summary>
    [Export] public float NoCoverTime = 3f;
    private double _noCoverTimer;

    /// <summary>Recuar quebrando a linha de visão: afasta-se do alvo
    /// em vez de dar as costas em linha reta.</summary>
    private void RunRetreat(double delta)
    {
        if (_target == null)
        {
            RunPatrol(delta);
            return;
        }

        Vector3 away = GlobalPosition - _target.GlobalPosition;
        Vector3 flat = new Vector3(away.X, 0, away.Z).Normalized();

        FaceDirection(_target.GlobalPosition, delta);
        MoveTowards(Avoid(flat), Speed, delta);
    }

    // ------------------------------------------------------------------
    // PERCEPÇÃO

    /// <summary>Visão: precisa estar perto, à frente e sem parede no
    /// meio. Os três, não um dos três.</summary>
    private void UpdateSight(double delta)
    {
        _memoryTimer -= delta;
        UpdateVigilance(delta);

        if (_target == null) return;
        if (_targetHealth != null && _targetHealth.IsDead) return;

        Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
        if (toTarget.Length() > SightRange) return;

        Vector3 flat = new Vector3(toTarget.X, 0, toTarget.Z).Normalized();
        Vector3 facing = -GlobalTransform.Basis.Z;

        float angle = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(flat.Dot(facing), -1f, 1f)));
        if (angle > FieldOfView * 0.5f) return;

        if (!HasLineOfSight()) return;

        Believe(_target.GlobalPosition, Memory);
    }

    /// <summary>
    /// Audição. Som não revela o quê, só o onde — e com erro, porque
    /// ouvir não é ver. Um bot que localiza som com precisão cirúrgica
    /// parece trapacear.
    /// </summary>
    private void OnNoise(Noise noise)
    {
        if (_health.IsDead) return;
        if (noise.Source == this) return;

        // Passo de aliado não é pista. Sem isto o time inteiro passaria
        // a partida investigando os próprios passos dentro da base.
        if (noise.Source?.GetNodeOrNull<Health>("Health") is Health source
            && source.Team == _health.Team)
            return;

        float distance = GlobalPosition.DistanceTo(noise.Position);
        if (distance > noise.Radius * Hearing) return;

        // Já vendo o alvo, som não acrescenta nada.
        if (State == BotState.Combat) return;

        // Nem todo som vira ida. Tiro e explosão chamam mais atenção
        // que um passo, e cada bot tem a própria tendência a investigar.
        float interest = noise.Kind switch
        {
            NoiseKind.Shot => 1f,
            NoiseKind.Explosion => 1f,
            NoiseKind.Howl => 0.9f,
            NoiseKind.Land => 0.6f,
            _ => 0.45f
        };

        // Som perto importa mais que som no limite da audição.
        float closeness = 1f - Mathf.Clamp(distance / (noise.Radius * Hearing), 0f, 1f);
        float chance = Curiosity * interest * (0.45f + closeness * 0.55f);

        if (GD.Randf() > chance)
        {
            // Ouviu e decidiu ficar. Mesmo assim fica alerta: vira na
            // direção do som sem abandonar a posição.
            _alerted = true;
            _threatAge = 0;

            if (Debug) GD.Print($"{Name} ouviu {noise.Kind} e ficou");
            return;
        }

        var error = new Vector3(
            (float)GD.RandRange(-HearingError, HearingError),
            0,
            (float)GD.RandRange(-HearingError, HearingError));

        Believe(noise.Position + error, Memory);

        if (Debug) GD.Print($"{Name} ouviu {noise.Kind} a {distance:0}m e foi olhar");
    }

    /// <summary>Registra onde o bot acha que o alvo está.</summary>
    private void Believe(Vector3 position, double duration)
    {
        _lastKnown = position;
        _hasBelief = true;
        _memoryTimer = duration;
        _beliefAge = 0;
    }

    private bool CanSeeTarget()
    {
        if (_target == null) return false;
        if (_targetHealth != null && _targetHealth.IsDead) return false;

        Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
        if (toTarget.Length() > SightRange) return false;

        Vector3 flat = new Vector3(toTarget.X, 0, toTarget.Z).Normalized();
        float angle = Mathf.RadToDeg(
            Mathf.Acos(Mathf.Clamp(flat.Dot(-GlobalTransform.Basis.Z), -1f, 1f)));

        return angle <= FieldOfView * 0.5f && HasLineOfSight();
    }

    /// <summary>
    /// Escolhe o inimigo a perseguir.
    ///
    /// Antes o alvo era sempre o jogador, pego uma vez pelo grupo
    /// "player". Com times isso deixa de fazer sentido de duas formas:
    /// o bot aliado mirava em você, e dois bots inimigos passavam um
    /// pelo outro sem se ver.
    ///
    /// Agora vale qualquer um vivo do outro time. Quem está à vista
    /// ganha de quem não está, e entre iguais ganha o mais perto — com a
    /// vantagem de <see cref="TargetStickiness"/> para o alvo atual, que
    /// é o que impede a cabeça de ficar indo e voltando entre dois.
    /// </summary>
    private void ChooseTarget()
    {
        Node3D best = null;
        Health bestHealth = null;
        float bestScore = float.MaxValue;

        foreach (string group in new[] { "player", "bot" })
        {
            foreach (Node node in GetTree().GetNodesInGroup(group))
            {
                if (node == this || node is not Node3D body) continue;

                var health = body.GetNodeOrNull<Health>("Health");
                if (health == null || health.IsDead || health.Team == _health.Team) continue;

                float distance = GlobalPosition.DistanceTo(body.GlobalPosition);
                float score = distance;

                if (distance <= SightRange && CanSee(body)) score -= 1000f;
                if (body == _target) score -= TargetStickiness;

                if (score >= bestScore) continue;

                bestScore = score;
                best = body;
                bestHealth = health;
            }
        }

        // A crença não é zerada na troca, de propósito. Com vários
        // inimigos ela deixa de ser "onde fulano está" e passa a ser "de
        // onde vem a ameaça" — e zerar aqui apagava justamente o tiro
        // pelas costas que a vigília existe para lembrar: o bot levava
        // tiro de um, trocava de alvo meio segundo depois e esquecia de
        // onde o tiro veio.
        _target = best;
        _targetHealth = bestHealth;
    }

    /// <summary>Linha de visão até um corpo qualquer, da cabeça ao peito.</summary>
    private bool CanSee(Node3D body)
    {
        var query = PhysicsRayQueryParameters3D.Create(
            _head.GlobalPosition, body.GlobalPosition + Vector3.Up * 0.4f);

        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return true;

        return hit["collider"].Obj == body;
    }

    private bool HasLineOfSight()
    {
        if (_target == null) return false;

        var query = PhysicsRayQueryParameters3D.Create(
            _head.GlobalPosition, _target.GlobalPosition + Vector3.Up * 0.4f);

        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return true;

        return hit["collider"].Obj == _target;
    }

    // ------------------------------------------------------------------
    // ESTADO

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

    // ------------------------------------------------------------------
    // AÇÃO

    private void RunCombat(double delta)
    {
        if (_target == null)
        {
            RunPatrol(delta);
            return;
        }

        UpdateBurst(delta);

        Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
        float distance = new Vector3(toTarget.X, 0, toTarget.Z).Length();

        FaceDirection(_target.GlobalPosition, delta);
        Maneuver(distance, toTarget, delta);

        // O especial vem antes da garra: arranhar no meio do pulo ou já
        // em alcance deixava o golpe em recarga e o bote não saía.
        if (_form.IsWolf)
        {
            WolfTactics(distance, delta);
            if (_wantDive || _weapon.IsChargingPounce || _weapon.IsPounceFlying || _weapon.IsDiving) return;
        }

        Engage(distance);
    }

    /// <summary>
    /// Vai até a última posição conhecida e olha em volta. Se não achar
    /// nada, desiste. É o que transforma um som em comportamento
    /// legível: o jogador vê o bot indo aonde ele estava.
    /// </summary>
    private void RunSearch(double delta)
    {
        // Procurando e deu de cara com o alvo: luta, não termina a busca.
        if (_target != null && CanSeeTarget())
        {
            RunCombat(delta);
            return;
        }

        float distance = new Vector3(
            _lastKnown.X - GlobalPosition.X, 0,
            _lastKnown.Z - GlobalPosition.Z).Length();

        if (distance > ArriveRadius)
        {
            Vector3 step = PathTo(_lastKnown);

            FaceDirection(_lastKnown, delta);
            MoveTowards(step, Speed * 0.85f, delta);
            return;
        }

        // Chegou: varre o entorno girando devagar.
        _scanTimer -= delta;

        if (_scanTimer <= 0)
        {
            _hasBelief = false;
            _memoryTimer = 0;
            return;
        }

        _scanAngle += (float)delta * 1.8f;
        Rotation = new Vector3(0, _scanAngle, 0);

        MoveTowards(Vector3.Zero, Speed, delta);
    }

    /// <summary>
    /// Patrulha: escolhe um ponto, vai até ele, pausa e escolhe outro.
    ///
    /// Sem navmesh não há garantia de que o ponto seja alcançável, e é
    /// por isso que existe o detector de travamento: em vez de tentar
    /// provar que o caminho existe, o bot tenta e desiste quando para
    /// de progredir. É mais barato e falha de forma visível, em vez de
    /// empurrar a parede indefinidamente.
    /// </summary>
    private void RunPatrol(double delta)
    {
        if (_target != null && CanSeeTarget())
        {
            RunCombat(delta);
            return;
        }

        if (_patrolPause > 0)
        {
            _patrolPause -= delta;
            MoveTowards(Vector3.Zero, Speed, delta);
            return;
        }

        if (!_hasPatrolTarget)
        {
            PickPatrolTarget();
            return;
        }

        Vector3 flat = new Vector3(
            _patrolTarget.X - GlobalPosition.X, 0,
            _patrolTarget.Z - GlobalPosition.Z);

        if (flat.Length() <= ArriveRadius)
        {
            _hasPatrolTarget = false;
            _patrolPause = PatrolPause * GD.RandRange(0.6, 1.6);
            return;
        }

        // Progrediu? Se não, o caminho não serve.
        if (GlobalPosition.DistanceTo(_lastPosition) < 0.04f)
        {
            _stuckTimer += delta;

            if (_stuckTimer >= StuckTime)
            {
                if (Debug) GD.Print($"{Name}: travado, trocando de destino");
                _hasPatrolTarget = false;
                _stuckTimer = 0;
                return;
            }
        }
        else
        {
            _stuckTimer = 0;
        }

        _lastPosition = GlobalPosition;

        Vector3 step = PathTo(_patrolTarget);

        if (step == Vector3.Zero)
        {
            _hasPatrolTarget = false;
            return;
        }

        FaceDirection(GlobalPosition + step * 4f, delta);
        MoveTowards(step, Speed * PatrolSpeedFactor, delta);
    }

    private void PickPatrolTarget()
    {
        if (PickRouteTarget()) return;

        float angle = (float)GD.RandRange(0, Mathf.Tau);

        // Raiz quadrada distribui por área; elevar o expoente puxa os
        // destinos para o meio do mapa, que é onde a luta acontece.
        // Patrulha que vive na borda é patrulha que ninguém encontra.
        float radius = PatrolRadius * Mathf.Pow((float)GD.Randf(), 0.85f);

        _patrolTarget = PatrolCenter + new Vector3(
            Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);

        _hasPatrolTarget = true;
        _stuckTimer = 0;
        _lastPosition = GlobalPosition;
    }

    /// <summary>
    /// Próximo ponto da rota, pegando uma nova quando a atual acaba.
    ///
    /// Entra pelo ponto mais próximo, não pelo primeiro: rota é um
    /// caminho, não uma fila com começo obrigatório, e fazer o bot
    /// voltar ao ponto um antes de andar é a travessia boba que
    /// denuncia o roteiro.
    ///
    /// O sentido é sorteado, então a mesma rota não vira um trilho
    /// onde dá para esperar o bot aparecer sempre pelo mesmo lado.
    /// </summary>
    private bool PickRouteTarget()
    {
        if (PatrolRoutes.RouteCount == 0) return false;

        if (_route < 0 || _leg < 0 || _leg >= PatrolRoutes.Length(_route))
        {
            _route = PatrolRoutes.Nearest(GlobalPosition);
            if (_route < 0) return false;

            PatrolRoutes.ClosestPoint(_route, GlobalPosition, out _leg);
            _legStep = GD.Randf() < 0.5f ? 1 : -1;

            // Entrando pela ponta, metade dos sorteios apontava para
            // fora da rota: o bot pegava um ponto só, a rota acabava
            // e ele sorteava outra. De longe isso é idêntico a não ter
            // rota nenhuma — ia parecer que o recurso não existe.
            if (_leg + _legStep < 0 || _leg + _legStep >= PatrolRoutes.Length(_route))
                _legStep = -_legStep;
        }

        _patrolTarget = PatrolRoutes.Point(_route, _leg);
        _leg += _legStep;

        if (Debug)
            GD.Print($"{Name}: rota {_route} ponto {_leg - _legStep} -> {_patrolTarget}");

        // Acabou a rota: na próxima vez pega outra.
        if (_leg < 0 || _leg >= PatrolRoutes.Length(_route))
            _route = -1;

        _hasPatrolTarget = true;
        _stuckTimer = 0;
        _lastPosition = GlobalPosition;
        return true;
    }

    private void Maneuver(float distance, Vector3 toTarget, double delta)
    {
        Vector3 flat = new Vector3(toTarget.X, 0, toTarget.Z).Normalized();
        Vector3 side = flat.Cross(Vector3.Up).Normalized() * _strafeSide;
        Vector3 desired;

        if (_form.IsWolf)
        {
            desired = flat;
        }
        else if (distance < MinRange)
        {
            // Perto demais até para um bot agressivo: recua de lado.
            desired = (-flat * 0.6f + side).Normalized();
        }
        else
        {
            // Avança enquanto circula. O peso da agressão decide o
            // quanto da intenção é ir para cima e o quanto é lateral.
            float push = Action == BotAction.Push
                ? 1f
                : distance > PreferredRange ? 1f : Aggression;

            desired = (flat * push + side * StrafeFactor).Normalized();
        }

        float speed = Speed * (_form.IsWolf ? _form.Breed.SpeedMultiplier : 1f);

        // Longe do alvo, segue o caminho: ir em linha reta esbarra em
        // parede. Perto, manda a intenção direta, porque navegação
        // ponto a ponto estraga strafe e recuo.
        if (distance > PreferredRange * 1.5f)
        {
            Vector3 step = PathTo(_target.GlobalPosition);

            if (step != Vector3.Zero)
            {
                MoveTowards(step, speed, delta);
                return;
            }
        }

        MoveTowards(Avoid(desired), speed, delta);
    }

    private void MoveTowards(Vector3 direction, float speed, double delta)
    {
        Vector3 velocity = Velocity;

        velocity.X = Mathf.MoveToward(velocity.X, direction.X * speed, speed * 6f * (float)delta);
        velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * speed, speed * 6f * (float)delta);
        velocity.Y = IsOnFloor() ? 0 : velocity.Y - _gravity * (float)delta;

        if (IsOnFloor() && ShouldJump(direction))
        {
            velocity.Y = JumpVelocity;

            _animator ??= GetNodeOrNull<CharacterAnimator>("Human");
            _animator?.Jump();
        }

        Velocity = velocity;
        MoveAndSlide();
    }

    /// <summary>
    /// Há degrau à frente que valha um pulo?
    ///
    /// A navegação resolve o chão e ignora o resto: ela devolve um
    /// caminho pelo piso e nada diz sobre o caixote de um metro no
    /// meio dele. O bot então empurrava contra o caixote até o
    /// detector de travamento desistir e mandá-lo para outro lugar —
    /// e de fora isso parece um bot que não entende o mapa.
    ///
    /// A leitura é em três perguntas, nessa ordem, porque cada uma só
    /// faz sentido se a anterior passou: existe algo à frente na
    /// altura da canela? O topo desse algo está dentro do alcance do
    /// pulo? E existe piso logo depois, para onde cair?
    ///
    /// A terceira é a que importa. Sem ela o bot pula em parede.
    /// </summary>
    private bool ShouldJump(Vector3 direction)
    {
        Vector3 flat = new Vector3(direction.X, 0, direction.Z);
        if (flat.LengthSquared() < 0.01f) return false;

        flat = flat.Normalized();

        var space = GetWorld3D().DirectSpaceState;
        var exclude = new Godot.Collections.Array<Rid> { GetRid() };

        // 1. obstáculo na canela
        Vector3 shin = GlobalPosition + Vector3.Down * 0.6f;
        var ahead = PhysicsRayQueryParameters3D.Create(shin, shin + flat * JumpProbe);
        ahead.Exclude = exclude;

        if (space.IntersectRay(ahead).Count == 0) return false;

        // 2. passa por cima na altura do pulo?
        Vector3 over = GlobalPosition + Vector3.Up * JumpMaxHeight;
        var clear = PhysicsRayQueryParameters3D.Create(over, over + flat * (JumpProbe + 0.4f));
        clear.Exclude = exclude;

        if (space.IntersectRay(clear).Count > 0) return false;

        // 3. tem chão do outro lado?
        Vector3 beyond = over + flat * (JumpProbe + 0.4f);
        var ground = PhysicsRayQueryParameters3D.Create(
            beyond, beyond + Vector3.Down * (JumpMaxHeight + 1.6f));
        ground.Exclude = exclude;

        return space.IntersectRay(ground).Count > 0;
    }

    /// <summary>
    /// Assenta o corpo morto.
    ///
    /// São duas coisas em ordem, e inverter quebra as duas. Primeiro a
    /// gravidade trabalha, com a colisão ligada, até o corpo tocar o
    /// chão — quem morre no ar precisa cair. Só então a colisão
    /// desliga e o corpo para onde parou.
    ///
    /// Desligar antes, como eu fazia, derruba o corpo pelo cenário sem
    /// nada para segurá-lo. Nunca desligar deixa o cadáver sendo
    /// empurrado para fora do chão quadro a quadro, que com colisão de
    /// dois lados pode resolver para o lado errado e afundar devagar.
    /// </summary>
    private void Settle(double delta)
    {
        // Assentado, o corpo não se move mais — nem a gravidade. Era o
        // defeito: com a colisão desligada, IsOnFloor() responde falso
        // no quadro seguinte, porque sem colisão não existe chão, e a
        // gravidade voltava a puxar o corpo através do mapa.
        if (_settled) return;

        if (IsOnFloor())
        {
            Velocity = Vector3.Zero;

            if (!_settled)
            {
                _settled = true;
                _deadShape ??= GetNode<CollisionShape3D>("CollisionShape3D");
                _deadShape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
            }
        }
        else
        {
            Velocity = new Vector3(0, Velocity.Y - _gravity * (float)delta, 0);
        }

        MoveAndSlide();
    }

    /// <summary>O bote do bot: segue a velocidade do mergulho até encostar.</summary>
    private void BotDive()
    {
        Velocity = _weapon.DiveVelocity;
        MoveAndSlide();
        if (IsOnFloor() || IsOnWall() || IsOnCeiling())
        {
            _weapon.DiveContact();
            Velocity = Vector3.Zero;
        }
    }

    private double _specialRoll;
    private bool _wantDive;

    /// <summary>
    /// O lobo do bot com as jogadas do jogador, na medida da dificuldade:
    ///
    /// - **Salto carregado** de 4 a 11 m, com o alvo à vista: carrega o
    ///   que precisa para cair numa garra dele, errando
    ///   <see cref="PounceError"/>.
    /// - **Pulo com bote** de 3 a 6 m: pula em direção ao alvo e mergulha
    ///   no alto do pulo.
    ///
    /// Sorteado uma vez por segundo com <see cref="WolfSpecialRate"/>: o
    /// lobo não vive de especial, ele chega correndo e arranha — o especial
    /// é a surpresa.
    /// </summary>
    private void WolfTactics(float distance, double delta)
    {
        if (_wantDive)
        {
            if (IsOnFloor()) { _wantDive = false; return; }
            if (Velocity.Y < 1.5f)
            {
                _wantDive = false;
                _weapon.TrySpecial();
                if (Debug) GD.Print($"{Name}: bote {(_weapon.IsDiving ? "saiu" : "não saiu")}");
            }
            return;
        }

        _specialRoll -= delta;
        if (_specialRoll > 0) return;
        _specialRoll = 0.5;

        if (!IsOnFloor() || _target == null || !CanSeeTarget()) return;
        if (GD.Randf() > WolfSpecialRate * 0.5f) return;

        if (distance >= 4f && distance <= 11f)
        {
            float power = _weapon.PounceChargeFor(distance) + (float)GD.RandRange(-PounceError, PounceError);
            bool went = _weapon.BotPounce(_target, power);
            if (Debug) GD.Print($"{Name}: salto a {distance:0.0} m, carga {power:0.00}{(went ? "" : " (não deu)")}");
        }
        else if (distance >= 3f && distance < 6f)
        {
            Vector3 to = _target.GlobalPosition - GlobalPosition;
            to.Y = 0;
            Vector3 hop = to.Normalized() * 3.2f;
            // Sai do chão já neste quadro: no próximo, o andar zeraria a
            // subida de quem ainda está no chão.
            Velocity = new Vector3(hop.X, JumpVelocity * (_form.Breed?.JumpMultiplier ?? 1f), hop.Z);
            MoveAndSlide();
            _wantDive = true;
        }
    }

    private void Fall(double delta)
    {
        Velocity = new Vector3(0, Velocity.Y - _gravity * (float)delta, 0);
        MoveAndSlide();
    }

    /// <summary>
    /// Direção para chegar a um ponto, seguindo o mapa de navegação.
    ///
    /// Devolve Zero quando já chegou. Se a navegação não tiver mapa
    /// assado, cai no vetor direto — melhor andar torto que ficar
    /// parado esperando uma malha que pode não existir.
    /// </summary>
    private Vector3 PathTo(Vector3 destination)
    {
        _nav.TargetPosition = destination;

        if (_nav.IsNavigationFinished())
            return Vector3.Zero;

        Vector3 next = _nav.GetNextPathPosition();
        Vector3 delta = next - GlobalPosition;
        var flat = new Vector3(delta.X, 0, delta.Z);

        if (flat.LengthSquared() < 0.0001f)
        {
            Vector3 direct = destination - GlobalPosition;
            flat = new Vector3(direct.X, 0, direct.Z);
        }

        return flat.Normalized();
    }

    /// <summary>Desvio raso: se o caminho está fechado, tenta os lados.
    /// Não é pathfinding e não resolve labirinto.</summary>
    private Vector3 Avoid(Vector3 desired)
    {
        if (desired == Vector3.Zero) return desired;
        if (!Blocked(desired)) return desired;

        for (int sign = -1; sign <= 1; sign += 2)
        {
            for (int step = 1; step <= 3; step++)
            {
                Vector3 candidate = desired.Rotated(Vector3.Up, sign * step * Mathf.Pi / 6f);
                if (!Blocked(candidate)) return candidate;
            }
        }

        return Vector3.Zero;
    }

    private bool Blocked(Vector3 direction)
    {
        Vector3 origin = GlobalPosition;

        var query = PhysicsRayQueryParameters3D.Create(origin, origin + direction * 1.6f);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        return GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0;
    }

    private void FaceDirection(Vector3 point, double delta)
    {
        Vector3 flat = new Vector3(point.X - GlobalPosition.X, 0, point.Z - GlobalPosition.Z);
        if (flat.LengthSquared() < 0.01f) return;

        float desired = Mathf.Atan2(-flat.X, -flat.Z) + _aimOffset.Y;

        // Pego de surpresa, gira mais devagar até ficar de frente.
        float turn = _alerted ? AlertTurnSpeed : TurnSpeed;
        float next = Mathf.LerpAngle(Rotation.Y, desired, turn * (float)delta);

        if (_alerted && Mathf.Abs(Mathf.AngleDifference(next, desired)) < 0.15f)
            _alerted = false;

        Rotation = new Vector3(0, next, 0);
        _scanAngle = next;

        float height = point.Y - GlobalPosition.Y - _head.Position.Y;
        float pitch = Mathf.Atan2(height, flat.Length()) + _aimOffset.X;

        _head.Rotation = new Vector3(
            Mathf.Clamp(pitch, Mathf.DegToRad(-70f), Mathf.DegToRad(70f)), 0, 0);
    }


    /// <summary>Rajada e pausa. Fogo contínuo não deixa o jogador
    /// respirar e esconde de onde vem o tiro.</summary>
    private void UpdateBurst(double delta)
    {
        _burstTimer -= delta;
        if (_burstTimer > 0) return;

        _firing = !_firing;
        _burstTimer = _firing ? BurstTime : BurstPause;
    }

    private void UpdateStrafe(double delta)
    {
        _strafeTimer -= delta;
        if (_strafeTimer > 0) return;

        _strafeSide = GD.Randf() < 0.5f ? -1 : 1;
        _strafeTimer = StrafeInterval * GD.RandRange(0.6, 1.4);
    }

    /// <summary>
    /// Sorteia o desvio da mira. Na maior parte das vezes é o erro
    /// normal; de vez em quando é um tiro jogado fora de propósito.
    ///
    /// Errar explicitamente é melhor que só aumentar o erro médio:
    /// mantém os acertos convincentes e dá ao jogador janelas claras
    /// em vez de um chuvisco de quase-acertos.
    /// </summary>
    private void ShuffleAim()
    {
        bool miss = GD.Randf() < MissChance;
        float spread = Mathf.DegToRad(miss ? MissSpread : AimError);

        _aimOffset = new Vector3(
            (float)GD.RandRange(-spread, spread),
            (float)GD.RandRange(-spread, spread),
            0);
    }

    private void Engage(float distance)
    {
        if (!_form.CanAttack) return;

        // Recarregando ou recuando não se atira. É o cérebro que
        // decide, e a ação em curso é a decisão. Indo para a parede, sim:
        // é fogo de cobertura.
        if (Action == BotAction.Reload || Action == BotAction.Retreat) return;

        float reach = _form.IsWolf
            ? _form.Breed.AttackRange
            : Mathf.Min(EngageRange, _weapon.PrimaryDef?.Range ?? EngageRange);

        if (distance > reach) return;
        if (!_form.IsWolf && !_firing) return;
        if (!HasLineOfSight()) return;

        _weapon.TryAttack();
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Levou tiro. Não reage na hora: marca a origem e conta um tempo
    /// curto antes de se virar. Reação instantânea é desumana e anula
    /// o prêmio de atacar pelas costas.
    /// </summary>
    private void OnDamaged(float amount, Vector3 from)
    {
        if (_health.IsDead || from == Vector3.Zero || _reacting) return;

        _threatAge = 0;
        _reacting = true;
        _reactionTimer = ReactionTime + GD.RandRange(0, ReactionJitter);
        Believe(from, Memory);
    }

    private void UpdateReaction(double delta)
    {
        if (!_reacting) return;

        _reactionTimer -= delta;
        if (_reactionTimer > 0) return;

        _reacting = false;
        _alerted = true;

        // Virou-se. Agora vigia: enquanto este tempo corre, a crença
        // não expira, então o bot continua olhando e indo para onde o
        // tiro veio em vez de voltar à ronda.
        _vigilTimer = Vigilance;
    }

    /// <summary>Mantém viva a crença de onde veio o tiro.</summary>
    private void UpdateVigilance(double delta)
    {
        if (_vigilTimer <= 0) return;

        _vigilTimer -= delta;

        // Só sustenta enquanto não há alvo à vista: ver alguém é uma
        // informação melhor do que lembrar de onde veio um tiro.
        if (State != BotState.Combat && _hasBelief)
            _memoryTimer = Mathf.Max(_memoryTimer, 0.5f);
    }

    private void OnDied(int killerId)
    {
        if (Debug) GD.Print($"{Name} morreu");

        _deadShape ??= GetNode<CollisionShape3D>("CollisionShape3D");
        _settled = false;

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
        _deadShape ??= GetNode<CollisionShape3D>("CollisionShape3D");
        _settled = false;
        _deadShape.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);

        GlobalPosition = SpawnPoints.Pick(this, _spawn);
        Velocity = Vector3.Zero;

        if (_form.IsWolf)
            _weapon.SelectSlot(Slot.Primary);
    }
}
