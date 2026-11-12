using Godot;

/// <summary>
/// Controlador de personagem em primeira pessoa.
///
/// Velocidade, pulo e corrida em parede mudam conforme a forma atual.
/// O Player não conhece as regras da transformação — ele pergunta ao
/// FormController.
///
/// Fase 2: este _PhysicsProcess vira a PREDIÇÃO do cliente. O mesmo
/// código roda no servidor sobre o input recebido, e por isso precisa
/// ser determinístico — nada de aleatoriedade aqui dentro.
/// </summary>
public partial class Player : CharacterBody3D
{
    [Export] public float HumanSpeed = 4.2f;
    [Export] public float JumpVelocity = 4.4f;

    // --- Agachar: vale nas duas formas ---

    // --- Aceleração e inércia ---

    /// <summary>Aceleração no chão, em m/s². Alta deixa o controle
    /// responsivo sem virar teletransporte.</summary>
    [Export] public float GroundAccel = 60f;

    /// <summary>Desaceleração no chão ao soltar as teclas.</summary>
    [Export] public float GroundFriction = 50f;

    /// <summary>Aceleração no ar. Baixa, para o pulo ser um
    /// compromisso: você escolhe a trajetória antes de saltar.</summary>
    [Export] public float AirAccel = 18f;

    /// <summary>Fração da gravidade ao segurar o pulo durante a queda.
    /// Pouco abaixo de 1: dá um respiro no controle do salto sem virar
    /// planagem.</summary>
    [Export] public float HoldFallGravity = 0.78f;

    /// <summary>Arrasto no ar quando nenhuma tecla está pressionada.
    /// É o que dá controle sobre o alcance do pulo: segurar mantém o
    /// avanço, soltar encurta. Zero conserva tudo e tira a escolha.</summary>
    [Export] public float AirFriction = 7f;

    /// <summary>Fração da velocidade enquanto agachado.</summary>
    [Export] public float CrouchSpeedFactor = 0.45f;

    /// <summary>Fração da velocidade andando com Shift. Só no humano:
    /// o lobo é puro movimento e não teria por que abrir mão disso.</summary>
    [Export] public float WalkSpeedFactor = 0.42f;

    /// <summary>
    /// Velocidade do bote aéreo do lobo, na direção da mira.
    ///
    /// O bote segue o olhar, inclusive para baixo: não é um martelo
    /// vertical, é um salto dirigido. Mirar no alvo e apertar leva o
    /// lobo até ele.
    /// </summary>
    [Export] public float LeapSpeed = 9f;

    /// <summary>Queda extra somada à direção do bote. Faz o lobo
    /// chegar antes do que chegaria só pela gravidade.</summary>
    [Export] public float LeapDrop = 5f;

    /// <summary>
    /// Multiplicador do recuo da arma. O valor base vem de cada arma
    /// no lote; isto é o ajuste global de quanto o recuo pesa.
    /// </summary>
    [Export] public float RecoilScale = 1f;

    /// <summary>Teto do recuo acumulado, em graus. Sem teto, segurar
    /// o gatilho aponta a arma para o céu e a arma vira inútil em vez
    /// de difícil.</summary>
    [Export] public float RecoilMax = 4.5f;

    /// <summary>
    /// Quanto tempo sem atirar até a mira começar a descer, em
    /// segundos. Maior que o intervalo de qualquer automática da
    /// primeira leva (0,08 s): enquanto o dedo está no gatilho a mira só
    /// sobe, e desce quando ele solta. É o mesmo atraso que o documento
    /// usa para a dispersão.
    /// </summary>
    [Export] public float RecoilReturnDelay = 0.12f;

    [Export] public float StandHeight = 1.75f;
    [Export] public float CrouchHeight = 1.0f;

    /// <summary>Altura do olho em pé e agachado.</summary>
    [Export] public float StandEyeHeight = 0.62f;
    [Export] public float CrouchEyeHeight = 0.1f;

    /// <summary>Velocidade da transição entre em pé e agachado.</summary>
    [Export] public float CrouchLerpSpeed = 22f;
    [Export] public float MouseSensitivity = 0.002f;

    /// <summary>Sensibilidade efetiva: a base daqui vezes a das opções.</summary>
    private float Sensitivity => MouseSensitivity * Settings.Values.Sensitivity;

    /// <summary>Escudo recuperado por segundo de rugido. O rugido é
    /// segurado, não apertado: é uma troca contínua entre ficar parado
    /// e recuperar o escudo.</summary>
    /// <summary>
    /// Tempo segurando R até o rugido completar. É uma conclusão, não
    /// uma torneira: soltar antes do fim não recupera nada.
    ///
    /// Isso é o que torna rugir uma aposta. Um segundo parado no meio
    /// de uma luta é muito tempo, e é por isso que a decisão importa.
    /// </summary>
    [Export] public float HowlDuration = 1f;

    // --- Ruído ---
    //
    // Som é informação de jogo: o bot ouve o que o jogador ouviria.
    // Agachar é silencioso, e é isso que torna o agachamento uma
    // decisão tática em vez de um botão de encolher.

    /// <summary>Raio do passo humano andando.</summary>
    [Export] public float StepNoiseHuman = 13f;

    /// <summary>Raio do passo do lobo. Mais alto: o corpo é maior e
    /// mais rápido, e isso precisa ter um custo.</summary>
    [Export] public float StepNoiseWolf = 20f;

    /// <summary>Raio do baque ao aterrissar.</summary>
    [Export] public float LandNoise = 17f;

    /// <summary>Raio do rugido. Muito alto de propósito — entregar a
    /// posição é metade do preço de recuperar o escudo.</summary>
    [Export] public float HowlNoise = 45f;

    /// <summary>Distância percorrida entre um passo e outro.</summary>
    [Export] public float StepDistance = 2.2f;

    // --- Corrida em parede (só na forma lobo) ---

    /// <summary>Distância máxima até a parede para grudar.</summary>
    [Export] public float WallCheckDistance = 0.75f;

    /// <summary>Tempo máximo preso na parede. Zero ou menos = sem
    /// limite: corre enquanto segurar W e a parede existir.</summary>
    [Export] public float WallRunMaxTime = 0f;

    /// <summary>Distância de checagem à frente. Bater em algo solta
    /// da parede.</summary>
    [Export] public float WallRunClearance = 0.7f;

    /// <summary>Deriva vertical na parede, em metros por segundo.
    /// Zero mantém a altura travada: o lobo não sobe nem desce.</summary>
    [Export] public float WallRunVerticalDrift = 0f;

    /// <summary>Velocidade ao longo da parede, sobre a velocidade da forma.</summary>
    [Export] public float WallRunSpeedBonus = 1.25f;

    /// <summary>Sucção contra a parede. Mantém o lobo colado em vez de
    /// apenas deslizar perto dela.</summary>
    [Export] public float WallStickForce = 4f;

    /// <summary>Quanto à frente olhar para antecipar o fim da face.</summary>
    [Export] public float CornerLookahead = 0.9f;

    /// <summary>Tempo que a corrida sobrevive sem encostar em nada, ao
    /// contornar uma quina, antes de reencontrar a face seguinte.</summary>
    [Export] public float CornerGrace = 0.3f;

    /// <summary>
    /// Multiplicador de altura do salto da parede. Acima de 1 cada
    /// salto sai mais alto do que entrou, permitindo escalar um
    /// corredor ricocheteando entre as paredes.
    /// </summary>
    [Export] public float WallJumpClimb = 1.35f;

    /// <summary>Força do empurrão para fora ao saltar da parede.</summary>
    [Export] public float WallJumpPush = 6.5f;

    /// <summary>Fração do avanço ao longo da parede que o salto
    /// conserva. Zero faz o lobo sair de lado e perder tudo que
    /// acumulou correndo.</summary>
    [Export] public float WallJumpKeepSpeed = 0.8f;

    /// <summary>
    /// Impede regrudar na MESMA parede sem tocar o chão, mas libera
    /// saltar para outra. Sem isso a parede vira elevador; com isso
    /// demais, o lobo não consegue encadear paredes opostas.
    ///
    /// Duas normais acima desta semelhança contam como a mesma parede.
    /// </summary>
    [Export] public float SameWallThreshold = 0.85f;

    /// <summary>Tempo mínimo entre uma corrida e a seguinte.</summary>
    [Export] public float WallRunCooldown = 0.12f;

    /// <summary>Altura abaixo da qual o jogador é devolvido ao spawn.
    /// Sem isto, sair do mapa é queda infinita.</summary>
    [Export] public float FallLimit = -25f;

    /// <summary>Imprime por que a corrida em parede não começou. Só
    /// imprime quando o motivo muda, então não enche o Output.</summary>
    [Export] public bool WallDebug = true;

    /// <summary>Distância da câmera na forma lobo.</summary>
    [Export] public float ThirdPersonDistance = 3.5f;

    private Camera3D _camera;        // primeira pessoa — humano
    private Camera3D _wolfCamera;    // terceira pessoa — lobo
    private SpringArm3D _springArm;
    private MeshInstance3D _mesh;
    private Node3D _head;
    private FormController _form;
    private Health _health;
    private Weapon _weapon;

    private float _gravity = 9.8f;
    private bool _howling;
    private double _howlProgress;
    private float _stepDistance;
    private bool _wasAirborne;
    private double _howlNoiseTimer;

    private double _stunTimer;
    private float _stunSlow = 1f;

    /// <summary>Atordoado pela granada: lento e sem corrida em parede.
    /// É a única resposta que o humano tem contra o lobo.</summary>
    public bool IsStunned => _stunTimer > 0;

    private bool _onWall;
    private Vector3 _wallNormal;
    private Vector3 _wallAlong;
    private double _cornerGrace;
    private Vector3 _lastWall;
    private bool _clinging;
    private double _wallCooldown;
    private string _lastWallLog = "";
    private Vector3 _spawn;
    private CollisionShape3D _collision;
    private CharacterAnimator _animator;
    private bool _settled;
    private CapsuleShape3D _capsule;
    private bool _crouching;

    /// <summary>Está agachado? Leitura para quem desenha o corpo. O
    /// agachamento é decidido aqui e em lugar nenhum mais.</summary>
    public bool IsCrouching => _crouching;

    /// <summary>
    /// Onde ficam os pés, em relação ao centro do corpo.
    ///
    /// A cápsula encolhe ao agachar e o corpo desce metade da
    /// diferença, então a base **se move**: −0,875 de pé, −0,5
    /// agachado. Quem desenha o corpo precisa seguir isso, senão o
    /// modelo fica fixo na base de pé e afunda 37 cm no chão quando o
    /// jogador agacha.
    /// </summary>
    public float FeetOffset => -_height * 0.5f;
    private bool _walking;
    private float _cameraKick;
    private double _sinceKick;
    private double _jumpLock;
    private float _height;
    private double _wallTimer;

    public bool IsWallRunning => _onWall;

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

        if (IsMultiplayerAuthority() && !UiState.AnyOpen)
            Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void OnFormChanged(int form) => ApplyCamera();

    /// <summary>Recuo de câmera: o cano sobe a cada tiro e volta
    /// sozinho. Sem isso, atirar não tem peso nenhum.</summary>
    /// <summary>
    /// Mergulho do golpe especial: o lobo despenca na direção em que
    /// olha. É o que transforma a patada dupla numa investida em vez
    /// de dois golpes dados no ar.
    ///
    /// Também é o compromisso: você escolhe o ponto de impacto no
    /// momento do clique e não controla mais nada até aterrissar.
    /// </summary>
    private void Dive()
    {
        // A direção é a da mira inteira, com o pitch: olhar para baixo
        // produz um bote curto e mergulhado, olhar à frente produz um
        // salto longo e raso. É o jogador que escolhe o arco.
        Vector3 look = -_head.GlobalTransform.Basis.Z;

        Vector3 velocity = look.Normalized() * LeapSpeed;
        velocity.Y -= LeapDrop;

        Velocity = velocity;
        ReleaseWall();
    }

    private void OnWeaponFired()
    {
        if (_form.IsWolf) return;

        float kick = Mathf.DegToRad(_weapon.Recoil * RecoilScale);
        float room = Mathf.DegToRad(RecoilMax) - _cameraKick;

        // Perto do teto o recuo para de crescer: a arma fica difícil,
        // não inútil.
        kick = Mathf.Min(kick, Mathf.Max(room, 0));
        if (kick <= 0) return;

        _cameraKick += kick;
        _sinceKick = 0;

        Vector3 rotation = _head.Rotation;
        rotation.X = Mathf.Clamp(
            rotation.X + kick, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));
        _head.Rotation = rotation;

        // O lateral não volta sozinho, de propósito: é ele que o jogador
        // aprende a compensar com o mouse, e devolvê-lo automaticamente
        // tiraria o padrão que diferencia uma arma da outra.
        RotateY(-Mathf.DegToRad(_weapon.RecoilYaw * RecoilScale));
    }

    private void UpdateCameraKick(double delta)
    {
        if (_cameraKick <= 0) return;

        _sinceKick += delta;
        if (_sinceKick < RecoilReturnDelay) return;

        // Volta linear, na velocidade do documento (°/s): o M16 desce os
        // 4,5° do teto em 0,6 s, o AK em 0,8 s, a M3 em 1,1 s.
        //
        // O defeito antigo: a volta SOMAVA ao ângulo da cabeça em vez de
        // subtrair, então a mira subia de novo ao parar de atirar.
        float rate = Mathf.DegToRad((_weapon.Held?.RecoilRecovery ?? 7f) * RecoilScale);
        float recover = Mathf.Min(rate * (float)delta, _cameraKick);

        _cameraKick -= recover;
        _head.Rotation = new Vector3(
            Mathf.Clamp(_head.Rotation.X - recover, Mathf.DegToRad(-89f), Mathf.DegToRad(89f)),
            _head.Rotation.Y, _head.Rotation.Z);
    }

    /// <summary>Reviver no lugar onde se morreu devolve o jogador para
    /// dentro do tiroteio que o matou. Volta ao spawn.</summary>
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
        _settled = false;
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
                _collision.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
            }
        }
        else
        {
            Velocity = new Vector3(0, Velocity.Y - _gravity * (float)delta, 0);
        }

        MoveAndSlide();
    }



    /// <summary>Parado no lugar, com gravidade. Sem a gravidade quem
    /// estivesse no ar quando a partida acabou ficaria pendurado.</summary>
    private void Hold(double delta)
    {
        ReleaseWall();

        Velocity = new Vector3(0,
            IsOnFloor() ? 0 : Velocity.Y - _gravity * (float)delta, 0);

        MoveAndSlide();
    }

    private void OnRespawned()
    {
        ApplyCamera();
        _settled = false;
        _collision.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);

        GlobalPosition = SpawnPoints.Pick(this, _spawn);
        Velocity = Vector3.Zero;

        ReleaseWall();
        _lastWall = Vector3.Zero;
        _stunTimer = 0;
        _stunSlow = 1f;
    }

    private void ApplyCamera()
    {
        bool isMine = IsMultiplayerAuthority();

        // Morrer joga a câmera para trás. É a única hora em que o dono
        // vê o próprio corpo em primeira pessoa — e é justamente a
        // hora em que ver importa, porque mostra o que aconteceu em
        // vez de congelar a tela onde você já não está.
        bool third = _form.IsThirdPerson || (_health != null && _health.IsDead);

        _camera.Current = isMine && !third;
        _wolfCamera.Current = isMine && third;

        // A arma na mão é do dono e só existe em primeira pessoa.
        Node3D view = _camera.GetNodeOrNull<Node3D>("ViewModel");
        if (view != null) view.Visible = isMine && !third;

        // Em primeira pessoa o dono não vê o próprio corpo; os outros
        // jogadores continuam vendo normalmente.
        //
        // Com modelo humano na cena a cápsula não volta nunca: ela era
        // o corpo provisório, e reacendê-la na morte poria uma cápsula
        // cinza em pé ao lado do boneco caído.
        if (isMine && GetNodeOrNull<Node3D>("Human") == null)
            _mesh.Visible = third;
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

    public override void _Input(InputEvent @event)
    {
        if (!IsMultiplayerAuthority()) return;

        // Menu aberto: o mouse é do menu. Sem isto a câmera gira atrás
        // da tela de lotes enquanto o jogador tenta clicar num cartão.
        if (UiState.AnyOpen) return;

        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-motion.Relative.X * Sensitivity);

            float pitch = motion.Relative.Y * Sensitivity;

            // Puxar o mouse para baixo durante o recuo já é a volta,
            // feita à mão. Desconta do que falta voltar, senão a volta
            // automática soma em cima e a mira afunda abaixo do alvo.
            if (pitch > 0 && _cameraKick > 0)
                _cameraKick = Mathf.Max(_cameraKick - pitch, 0);

            Vector3 headRot = _head.Rotation;
            headRot.X = Mathf.Clamp(
                headRot.X - pitch,
                Mathf.DegToRad(-89f),
                Mathf.DegToRad(89f));
            _head.Rotation = headRot;
        }

        // Esc não solta mais o mouse aqui: abre o menu, e quem decide
        // o cursor é o UiState. Dois donos do mesmo cursor foi o defeito
        // que o UiState nasceu para resolver.
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsMultiplayerAuthority()) return;

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

        // Uivar trava você no lugar. É o preço da cura.
        if (IsHowling)
        {

            Vector3 frozen = Velocity;
            frozen.X = 0;
            frozen.Z = 0;
            if (!IsOnFloor())
                frozen.Y -= _gravity * (float)delta;
            Velocity = frozen;
            MoveAndSlide();
            return;
        }

        if (TryWallRun(delta))
            return;

        NormalMovement(delta);
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Publica os ruídos do corpo. O passo sai por distância
    /// percorrida, não por tempo: assim a cadência acompanha a
    /// velocidade sozinha e andar devagar faz menos barulho.
    ///
    /// Agachado não emite nada. É o que dá ao agachamento um propósito
    /// além de ficar menor.
    /// </summary>
    private void UpdateNoise(double delta)
    {
        bool grounded = IsOnFloor();

        // Baque de aterrissagem.
        if (grounded && _wasAirborne)
            NoiseBus.Emit(GlobalPosition, LandNoise, NoiseKind.Land, this);

        _wasAirborne = !grounded;

        if (_howling)
        {
            _howlNoiseTimer -= delta;

            if (_howlNoiseTimer <= 0)
            {
                _howlNoiseTimer = 0.5;
                NoiseBus.Emit(GlobalPosition, HowlNoise, NoiseKind.Howl, this);
            }
        }

        // Agachado e andando devagar não fazem barulho. São as duas
        // ferramentas de furtividade do humano, e é o que dá ao jogador
        // escolha contra um inimigo que ouve.
        if (!grounded || _crouching || _walking) return;

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

    private bool IsHowling => _howling;

    /// <summary>
    /// Rugido: segurar R recupera o escudo enquanto trava o lobo no
    /// lugar. Como o escudo escorre sozinho, rugir é decidir ficar
    /// exposto agora para sobreviver depois — e o som entrega a
    /// posição, que é a outra metade do preço.
    /// </summary>
    private void UpdateHowl(double delta)
    {
        bool wants = Input.IsActionPressed("howl")
            && _form.CanHowl
            && !_health.IsDead;

        if (!wants)
        {
            _howling = false;
            _howlProgress = 0;
            return;
        }

        _howling = true;
        _howlProgress += delta;

        if (_howlProgress < HowlDuration) return;

        // Completou: escudo cheio e o contador reinicia, então segurar
        // mais tempo rende outro rugido.
        _health.RestoreShield(_health.MaxShield);
        _howlProgress = 0;
    }

    /// <summary>Fração do rugido já concluída, para o HUD mostrar.</summary>
    public float HowlProgress =>
        _howling ? (float)(_howlProgress / HowlDuration) : 0f;

    /// <summary>Aplica atordoamento. O mais forte vence: uma segunda
    /// granada nao pode enfraquecer a primeira.</summary>
    public void ApplyStun(float duration, float slowFactor)
    {
        if (duration <= 0) return;

        if (duration > _stunTimer)
            _stunTimer = duration;

        _stunSlow = Mathf.Min(_stunSlow, Mathf.Clamp(slowFactor, 0.05f, 1f));
    }

    private float SpeedScale =>
        (_form.IsWolf ? _form.Breed.SpeedMultiplier : 1f)
        * (IsStunned ? _stunSlow : 1f)
        * (_crouching ? CrouchSpeedFactor : 1f)
        * (_walking ? WalkSpeedFactor : 1f)
        // A arma na mão pesa: 1,12 com a M9, 0,9 com a M3. É metade do
        // que faz trocar para a secundária ser fuga e não só munição.
        * (_form.IsWolf ? 1f : _weapon.MoveSpeedMultiplier);
    private float JumpScale => _form.IsWolf ? _form.Breed.JumpMultiplier : 1f;

    private void HandleActions(double delta)
    {
        if (UiState.AnyOpen) return;

        if (Input.IsActionJustPressed("slot_1")) _weapon.SelectSlot(Slot.Primary);
        if (Input.IsActionJustPressed("slot_2")) _weapon.SelectSlot(Slot.Secondary);
        if (Input.IsActionJustPressed("slot_3")) _weapon.SelectSlot(Slot.Wolf);
        if (Input.IsActionJustPressed("slot_4")) _weapon.SelectSlot(Slot.Knife);
        if (Input.IsActionJustPressed("slot_5")) _weapon.SelectSlot(Slot.Grenade);

        // Só a arma principal é automática. A secundária e a garrada
        // exigem um clique por golpe.
        bool firing = _weapon.IsAutomatic
            ? Input.IsActionPressed("attack")
            : Input.IsActionJustPressed("attack");

        if (firing)
            _weapon.TryAttack();

        // Botão direito: patada pesada no chão, bote dirigido no ar.
        // Só o bote move o corpo, e é por isso que TrySpecial avisa.
        if (Input.IsActionJustPressed("special") && _weapon.TrySpecial())
            Dive();

        // R faz o que a forma permite: ruge de lobo, recarrega de
        // humano. Uma tecla, um gesto, dois corpos.
        if (Input.IsActionJustPressed("howl") && !_form.IsWolf)
            _weapon.TryReload();

        // B: solta a arma da mão para frente e passa para a próxima.
        if (Input.IsActionJustPressed("drop"))
            _weapon.TryDrop();

        UpdateHowl(delta);
    }

    private void NormalMovement(double delta)
    {
        Vector3 velocity = Velocity;

        if (!IsOnFloor())
        {
            // Segurar o pulo durante a queda alivia um pouco a
            // gravidade. É sutil de propósito: dá controle fino sobre
            // onde aterrissar sem transformar o salto em planagem.
            bool holding = velocity.Y < 0 && Input.IsActionPressed("jump");
            float scale = holding ? HoldFallGravity : 1f;

            velocity.Y -= _gravity * scale * (float)delta;
        }
        else if (WantsJump())
        {
            velocity.Y = JumpVelocity * JumpScale;

            _animator ??= GetNodeOrNull<CharacterAnimator>("Human");
            _animator?.Jump();
        }

        Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Vector3 direction = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
        float speed = HumanSpeed * SpeedScale;

        bool grounded = IsOnFloor();
        var horizontal = new Vector3(velocity.X, 0, velocity.Z);

        if (direction != Vector3.Zero)
        {
            float accel = grounded ? GroundAccel : AirAccel;
            horizontal = horizontal.MoveToward(direction * speed, accel * (float)delta);
        }
        else
        {
            // No chão freia rápido. No ar o arrasto é suave: o corpo
            // segue a trajetória do pulo, mas soltar a tecla encurta o
            // alcance — é assim que o jogador controla a distância.
            float drag = grounded ? GroundFriction : AirFriction;
            horizontal = horizontal.MoveToward(Vector3.Zero, drag * (float)delta);
        }

        velocity.X = horizontal.X;
        velocity.Z = horizontal.Z;

        Velocity = velocity;
        MoveAndSlide();
    }

    /// <summary>
    /// Corrida em parede. Só no lobo, só no ar, só segurando para frente.
    ///
    /// Retorna true se assumiu o controle do movimento neste frame.
    /// </summary>
    /// <summary>
    /// Corrida em parede. Só no lobo, só no ar, só na descida do pulo,
    /// só segurando para frente.
    ///
    /// Retorna true se assumiu o controle do movimento neste frame.
    /// </summary>
    private bool TryWallRun(double delta)
    {
        if (!_form.IsWolf || IsOnFloor() || IsStunned)
        {
            ReleaseWall();
            return false;
        }

        // A corrida é sustentada pelo W: pula-se contra a parede e
        // segura para frente. Soltar solta a parede.
        //
        // Deixar o pulo livre é o que torna o salto entre paredes uma
        // jogada: segura-se o W o tempo todo e o pulo serve só para
        // trocar de parede, sem disputar com a tecla que sustenta.
        //
        // O botão de ataque agarra sem avançar — é a pausa que permite
        // escolher a hora de saltar em vez de correr até a parede
        // acabar.
        _clinging = Input.IsActionPressed("attack");

        if (!Input.IsActionPressed("move_forward") && !_clinging)
        {
            // Este era o caminho mudo, e é o mais provável de todos:
            // chega-se à parede sem nada segurando a corrida e o
            // código voltava antes de olhar, então o log ficava calado
            // como se estivesse tudo bem.
            if (!IsOnFloor()) WallLog("segure W para correr na parede");

            ReleaseWall();
            return false;
        }

        bool found = FindWall(out Vector3 normal);

        if (!_onWall)
        {
            // Uma corrida por ida ao ar. Sem isto o lobo regruda na
            // mesma altura logo após o salto e a parede vira elevador.
            if (_wallCooldown > 0)
            {
                WallLog("ainda em recarga desde a última corrida");
                return false;
            }

            if (!found)
            {
                WallLog($"nenhuma a menos de {WallCheckDistance}m");
                return false;
            }


            // A mesma parede, sem ter tocado o chão, viraria elevador:
            // sobe, salta, regruda mais alto, repete. Uma parede
            // diferente é outra coisa — é a jogada de quicar entre as
            // duas paredes de um corredor, e essa vale.
            if (_lastWall != Vector3.Zero && normal.Dot(_lastWall) > SameWallThreshold)
            {
                WallLog("mesma parede — salte para outra ou toque o chão");
                return false;
            }

            _onWall = true;
            _wallTimer = WallRunMaxTime;
            _wallNormal = normal;
            _cornerGrace = 0;

            // Numa quina côncava o sentido escolhido pode apontar para
            // dentro da parede vizinha. Vira para o outro lado antes de
            // desistir, senão o lobo solta no mesmo frame em que gruda.
            if (!ResolveDirection(normal, out _wallAlong))
            {
                WallLog("os dois sentidos estão bloqueados");
                _onWall = false;
                return false;
            }

            WallLog($"grudou, normal {_wallNormal}, sentido {_wallAlong}");
        }
        else if (found)
        {
            bool turned = normal.Dot(_wallNormal) < 0.8f;

            _wallNormal = normal;
            _cornerGrace = 0;

            Vector3 along = normal.Cross(Vector3.Up).Normalized();

            if (turned)
            {
                // Parede nova em ângulo. Comparar com o sentido antigo
                // não resolve: numa parede perpendicular os dois lados
                // dão zero e o desempate manda o lobo de volta. Quem
                // decide é o movimento que ele já carrega, e o lado
                // bloqueado é descartado.
                Vector3 flat = new Vector3(Velocity.X, 0, Velocity.Z);

                if (Mathf.Abs(flat.Dot(along)) > 0.5f)
                    along = flat.Dot(along) > 0 ? along : -along;
                else if (IsBlocked(along))
                    along = -along;
            }
            else if (along.Dot(_wallAlong) < 0)
            {
                // Mesma parede, apenas curvando: mantém o sentido.
                along = -along;
            }

            _wallAlong = along;
        }
        else if (_cornerGrace <= 0)
        {
            // A face acabou. Numa quina convexa, a face seguinte tem
            // normal igual à direção em que estávamos correndo, e o novo
            // sentido é o oposto da normal antiga. Gira e segue, com uma
            // janela curta para reencontrar a parede do outro lado.
            Vector3 oldNormal = _wallNormal;
            _wallNormal = _wallAlong;
            _wallAlong = -oldNormal;
            _cornerGrace = CornerGrace;

            WallLog($"quina: tentando face seguinte, normal {_wallNormal}");
        }
        else
        {
            _cornerGrace -= delta;

            if (_cornerGrace <= 0)
            {
                WallLog("quina: não achou a face seguinte, soltou");
                ReleaseWall();
                return false;
            }
        }

        if (WallRunMaxTime > 0)
        {
            _wallTimer -= delta;

            if (_wallTimer <= 0)
            {
                ReleaseWall();
                return false;
            }
        }

        Vector3 velocity = Velocity;

        // Salto da parede: empurra para fora e para cima. É a saída que
        // transforma a corrida em jogada ofensiva.
        if (WantsJump())
        {
            // Conserva o avanço ao longo da parede e soma o empurrão
            // para fora. Sem isso o salto zera tudo que a corrida
            // acumulou, e o lobo sai de lado em vez de seguir em frente
            // — justamente o oposto da jogada de cair em cima de alguém.
            float carried = HumanSpeed * SpeedScale * WallRunSpeedBonus * WallJumpKeepSpeed;

            velocity = _wallAlong * carried + _wallNormal * WallJumpPush;

            // Sai mais alto do que entrou. É o que permite escalar um
            // corredor ricocheteando entre as duas paredes, e é essa
            // subida que dá ao lobo o ângulo que as armas defendem mal:
            // de cima e por trás.
            velocity.Y = JumpVelocity * JumpScale * WallJumpClimb;

            // O salto de parede também é um pulo. Faltava avisar, e no
            // modo lobo é justamente este o pulo que se usa o tempo
            // todo — então o corpo saía da parede sem trocar de pose.
            _animator ??= GetNodeOrNull<CharacterAnimator>("Human");
            _animator?.Jump();

            _lastWall = _wallNormal;

            ReleaseWall();
            _wallCooldown = WallRunCooldown;

            Velocity = velocity;
            MoveAndSlide();
            return true;
        }

        // Caminho bloqueado à frente: solta e cai.
        if (IsBlocked(_wallAlong))
        {
            WallLog("soltou, caminho bloqueado à frente");
            ReleaseWall();
            return false;
        }

        // A subida é PRESERVADA ao grudar, e só depois a altura trava.
        //
        // Era aqui o erro: travar a altura no instante do contato
        // matava o pulo de quem grudava subindo — o jogador apertava
        // pular colado na parede e só colava. Impedir de grudar
        // subindo consertava o pulo e quebrava a jogada inteira.
        //
        // Deixar o impulso morrer sozinho resolve os dois: pula-se
        // contra a parede, sobe-se o que o pulo rendeu, e dali em
        // diante a corrida é plana.
        velocity.Y = velocity.Y > WallRunVerticalDrift
            ? Mathf.Max(velocity.Y - _gravity * 0.55f * (float)delta, WallRunVerticalDrift)
            : WallRunVerticalDrift;

        // Agarrado com o botão de ataque e sem o pulo: fica parado.
        float speed = _clinging && !Input.IsActionPressed("jump")
            ? 0f
            : HumanSpeed * SpeedScale * WallRunSpeedBonus;

        // Sucção contra a parede, exceto ao contornar uma quina — ali
        // ela empurraria o lobo para dentro do canto e o prenderia.
        float stick = _cornerGrace > 0 ? 0f : WallStickForce;

        velocity.X = _wallAlong.X * speed - _wallNormal.X * stick;
        velocity.Z = _wallAlong.Z * speed - _wallNormal.Z * stick;

        Velocity = velocity;
        MoveAndSlide();
        return true;
    }

    /// <summary>
    /// Agachar vale nas duas formas: encolhe a cápsula, baixa o olho e
    /// corta a velocidade. Na parede não se agacha — a corrida já é um
    /// estado de corpo inteiro.
    /// </summary>
    private void UpdateCrouch(double delta)
    {
        _crouching = !_onWall && Input.IsActionPressed("crouch");

        // Andar devagar é ferramenta do humano. O lobo vive de
        // movimento; dar a ele a opção de abrir mão disso só criaria
        // um lobo furtivo, que é o oposto da forma.
        _walking = !_onWall && !_form.IsWolf && Input.IsActionPressed("walk");

        float target = _crouching ? CrouchHeight : StandHeight;
        float previous = _height;

        _height = Mathf.MoveToward(_height, target, CrouchLerpSpeed * (float)delta);

        if (Mathf.IsEqualApprox(previous, _height))
            return;

        _capsule.Height = _height;

        // A cápsula encolhe em torno do próprio centro, então os pés
        // sobem no ar e a gravidade os traz de volta — o que aparece
        // como um agachamento em dois tempos. Descer o corpo metade da
        // diferença mantém os pés plantados e o movimento contínuo.
        Vector3 position = GlobalPosition;
        position.Y += (_height - previous) * 0.5f;
        GlobalPosition = position;

        // O olho acompanha a cápsula na mesma proporção.
        float t = Mathf.InverseLerp(CrouchHeight, StandHeight, _height);

        Vector3 head = _head.Position;
        head.Y = Mathf.Lerp(CrouchEyeHeight, StandEyeHeight, t);
        _head.Position = head;
    }

    /// <summary>Imprime só quando a mensagem muda. Sem isso cada frame
    /// de queda vira uma linha e o sinal some no meio do ruído.</summary>
    private void WallLog(string message)
    {
        if (!WallDebug || message == _lastWallLog) return;

        _lastWallLog = message;
        GD.Print($"parede: {message}");
    }

    /// <summary>
    /// Um toque, um pulo. Segurar a tecla não repete, e a trava curta
    /// impede que o mesmo toque dispare o pulo do chão e o da parede.
    /// </summary>
    private bool WantsJump()
    {
        if (_jumpLock > 0) return false;
        if (!Input.IsActionJustPressed("jump")) return false;

        _jumpLock = 0.2;
        return true;
    }

    private void ReleaseWall()
    {
        _onWall = false;
        _cornerGrace = 0;
    }

    /// <summary>
    /// A direção da corrida sai do movimento que o lobo já tem, não de
    /// para onde a câmera aponta: saltar de uma parede e encostar na
    /// seguinte mantém o sentido sem tocar no mouse.
    ///
    /// O olhar só decide quando não há momento horizontal — caso de
    /// quem pula parado contra a parede.
    /// </summary>
    private Vector3 ChooseDirection(Vector3 normal)
    {
        Vector3 along = normal.Cross(Vector3.Up).Normalized();

        Vector3 flat = new Vector3(Velocity.X, 0, Velocity.Z);
        float momentum = flat.Dot(along);

        if (Mathf.Abs(momentum) > 0.5f)
            return momentum > 0 ? along : -along;

        return along.Dot(-GlobalTransform.Basis.Z) < 0 ? -along : along;
    }

    /// <summary>
    /// Escolhe o sentido inicial e garante que ele não esbarra de
    /// imediato. Devolve falso quando os dois lados estão fechados —
    /// um canto apertado demais para correr.
    /// </summary>
    private bool ResolveDirection(Vector3 normal, out Vector3 along)
    {
        along = ChooseDirection(normal);

        if (!IsBlocked(along)) return true;

        along = -along;
        return !IsBlocked(along);
    }

    /// <summary>
    /// Varre o entorno em leque e devolve a normal da parede mais
    /// próxima. Em leque, e não só para os lados do corpo, porque o
    /// ângulo do corpo não deve decidir se existe parede ali.
    /// </summary>
    private bool FindWall(out Vector3 normal)
    {
        normal = Vector3.Zero;

        Vector3 origin = GlobalPosition;
        float best = float.MaxValue;
        bool any = false;

        for (int i = 0; i < 8; i++)
        {
            float angle = Mathf.Tau * i / 8f;
            var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

            if (!Cast(origin, dir, out Vector3 n, out float distance)) continue;
            if (distance >= best) continue;

            best = distance;
            normal = n;
            any = true;
        }

        return any;
    }

    /// <summary>Altura mínima dos pés ao chão para a parede pegar.
    /// Baixa de propósito: alta demais mata a jogada em vez de
    /// filtrar o esbarrão.</summary>
    [Export] public float WallMinHeight = 0.18f;

    /// <summary>Chão logo abaixo dos pés.</summary>
    private bool IsNearFloor()
    {
        Vector3 origin = GlobalPosition;

        var query = PhysicsRayQueryParameters3D.Create(
            origin, origin + Vector3.Down * (_height * 0.5f + WallMinHeight));
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        return GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0;
    }

    /// <summary>
    /// Há obstáculo de verdade no caminho à frente, ao longo da parede?
    ///
    /// Só parede bloqueia. Chão, rampa, meio-fio e beiral de telhado
    /// não — são coisas que o lobo passa por cima, e vetar a corrida
    /// por causa deles fecha os dois sentidos num mapa real.
    ///
    /// A distinção não existia antes porque não precisava: com
    /// colisão de uma face, metade da geometria do mapa era invisível
    /// para o raio e quase nada bloqueava. Consertar a colisão fez
    /// este teste passar a acertar tudo, inclusive o que não deveria
    /// contar — um conserto que cria o problema seguinte.
    /// </summary>
    private bool IsBlocked(Vector3 along)
    {
        Vector3 origin = GlobalPosition;

        var query = PhysicsRayQueryParameters3D.Create(
            origin, origin + along * WallRunClearance);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return false;

        Vector3 n = (Vector3)hit["normal"];
        if (n.Dot(along) > 0) n = -n;

        return Mathf.Abs(n.Y) < 0.4f;
    }

    private bool Cast(Vector3 origin, Vector3 direction, out Vector3 normal, out float distance)
    {
        normal = Vector3.Zero;
        distance = 0f;

        var query = PhysicsRayQueryParameters3D.Create(
            origin, origin + direction * WallCheckDistance);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return false;

        Vector3 n = (Vector3)hit["normal"];

        // Orienta a normal contra o raio.
        //
        // Com colisão de dois lados, bater no verso de um triângulo
        // devolve a normal geométrica, que aponta para o mesmo lado em
        // que o raio viaja. Usada crua, ela inverteria a força que
        // segura o lobo: em vez de colar na parede, ele seria empurrado
        // para dentro dela. Virar a normal para quem perguntou vale
        // para qualquer sentido de face e não muda nada no caso normal.
        if (n.Dot(direction) > 0) n = -n;

        // Rejeita chão e teto: só parede vale.
        if (Mathf.Abs(n.Y) > 0.4f) return false;

        normal = n;
        distance = origin.DistanceTo((Vector3)hit["position"]);
        return true;
    }
}
