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

    /// <summary>Quanto do passo o lobo leva para o pulo do chão. O lobo
    /// corre a 5,7 m/s e o pulo dele dura mais de um segundo: com o passo
    /// inteiro, ele planava 6 m para a frente parado na pose de correr.
    /// Vale para o pulo todo, até tocar o chão ou grudar numa parede — o
    /// salto da parede não entra, ele conserva o avanço da corrida.</summary>
    [Export] public float WolfJumpCarry = 0.55f;

    /// <summary>Fração da velocidade enquanto agachado.</summary>
    [Export] public float CrouchSpeedFactor = 0.45f;

    /// <summary>Fração da velocidade andando com Shift. Só no humano:
    /// o lobo é puro movimento e não teria por que abrir mão disso.</summary>
    [Export] public float WalkSpeedFactor = 0.42f;

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
    /// Duração do rugido: a animação inteira do "roaring", na velocidade
    /// de <see cref="WolfMoves"/>. Apertar R começa e não tem volta — o
    /// lobo fica parado até o fim, e só então o escudo volta.
    ///
    /// É isso que torna rugir uma aposta: segundos parado no meio de uma
    /// luta, com o som entregando a posição.
    /// </summary>
    public static float HowlDuration => WolfMoves.Duration(WolfMove.Howl);

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

    /// <summary>Velocidade ao longo da parede, sobre a velocidade da forma.
    /// Era 1,25: rápido demais para escolher onde descer.</summary>
    [Export] public float WallRunSpeedBonus = 1.0f;

    /// <summary>Aceleração ao longo da parede, em m/s². A corrida
    /// começa aos poucos em vez de sair a toda no quadro em que gruda.</summary>
    [Export] public float WallAccel = 18f;

    /// <summary>Freada ao agarrar (botão de ataque), em m/s². Mais forte
    /// que a aceleração: agarrar é escolher o ponto, e o lobo para em
    /// meio metro.</summary>
    [Export] public float WallBrake = 32f;

    /// <summary>Velocidade máxima com que o lobo é puxado contra a parede.
    /// A puxada é proporcional à folga: mantém o corpo a
    /// <see cref="WallHoldDistance"/> da parede, sem bater nem descolar.</summary>
    [Export] public float WallStickForce = 4f;

    /// <summary>Distância do centro do corpo à parede durante a corrida:
    /// o raio da cápsula e um fio de folga.</summary>
    [Export] public float WallHoldDistance = 0.34f;

    /// <summary>Tempo que a corrida sobrevive sem encostar em nada antes
    /// de soltar. Com a sonda que acha o ponto de parede mais perto, a
    /// quina não perde a parede; isto cobre falha de geometria.</summary>
    [Export] public float CornerGrace = 0.15f;

    /// <summary>Subindo mais rápido que isto (m/s), o lobo ainda não
    /// gruda: ele gruda no alto do pulo, na altura em que chegou, e fica
    /// nela. Grudar subindo carregava o impulso para cima da parede.</summary>
    [Export] public float WallGrabMaxRise = 0.6f;

    /// <summary>Quanto o lobo passa da ponta da parede, em metros, antes
    /// de soltar e seguir reto. Na quina ele não contorna: a parede
    /// acabou, a corrida acaba e o embalo continua no ar.</summary>
    [Export] public float WallEndMargin = 0.1f;

    /// <summary>Indo para longe da parede mais rápido que isto (m/s), não
    /// gruda. É o que impede de regrudar na ponta que acabou de deixar.</summary>
    [Export] public float WallLeaveSpeed = 0.5f;

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

    /// <summary>No ar por um pulo do chão, de lobo: o passo fica em
    /// <see cref="WolfJumpCarry"/>.</summary>
    private bool _wolfJump;
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

    /// <summary>Normal da parede em que o lobo corre (para fora dela).
    /// O corpo do lobo deita com os pés na parede a partir daqui.</summary>
    public Vector3 WallNormal => _onWall ? _wallNormal : Vector3.Zero;

    /// <summary>Sentido da corrida na parede.</summary>
    public Vector3 WallAlong => _onWall ? _wallAlong : Vector3.Zero;

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

    /// <summary>A câmera de onde o jogador mira: a de terceira pessoa no
    /// lobo, a da cabeça no humano. Não é a câmera atual da tela — essa
    /// pode ser a da morte ou outra qualquer.</summary>
    public Camera3D AimCamera => _form.IsThirdPerson ? _wolfCamera : _camera;

    /// <summary>
    /// O bote em curso: o corpo segue a velocidade fixada no clique, em
    /// linha reta, até encostar em algo. É o compromisso do golpe — o
    /// ponto de impacto é escolhido no clique e não se controla mais
    /// nada até aterrissar. Encostou: para seco e avisa a arma, que bate.
    /// </summary>
    private void DiveMovement()
    {
        ReleaseWall();
        Velocity = _weapon.DiveVelocity;
        MoveAndSlide();

        // Parede só conta se o bote vai de encontro a ela. Saindo de uma
        // corrida na parede, o corpo ainda roça nela no primeiro quadro, e
        // isso não pode virar impacto.
        Vector3 heading = _weapon.DiveVelocity.Normalized();
        bool wall = IsOnWall() && GetWallNormal().Dot(heading) < -0.3f;

        if (!IsOnFloor() && !wall && !IsOnCeiling()) return;

        _weapon.DiveContact();
        Velocity = Vector3.Zero;
    }

    /// <summary>
    /// O voo do salto: o corpo vai para o ponto do arco no próximo quadro.
    /// Por posição e não por força, para o pulo cair exatamente onde foi
    /// planejado; a física ainda manda — bater em algo no caminho para o
    /// corpo ali. A velocidade é limitada para nunca atravessar parede.
    /// </summary>
    private void PounceFlight(double delta)
    {
        ReleaseWall();

        Vector3 next = _weapon.PounceFlightPoint(delta);
        Vector3 velocity = (next - GlobalPosition) / (float)delta;
        Velocity = velocity.LimitLength(28f);
        MoveAndSlide();
    }

    /// <summary>Freada de quem fica preso no lugar, em m/s². Parar seco a
    /// 6,5 m/s era um tranco de câmera; assim o lobo desliza meio metro
    /// enquanto agacha para o salto, que é o embalo que se espera ver.</summary>
    [Export] public float FreezeBrake = 30f;

    /// <summary>Preso no lugar: sem andar nem pular, só a gravidade.
    /// Rugir, o salto no chão e a aterrissagem do bote usam. O jogador
    /// não anda, mas o golpe pode andar por ele (o avanço do salto).</summary>
    private void Freeze(double delta)
    {
        ReleaseWall();

        Vector3 frozen = Velocity;

        // O salto preso ao chão ainda avança: o passo dele leva o corpo até
        // o ponto da mira, por cima da freada.
        var flat = _weapon.IsStepping
            ? _weapon.StepVelocity
            : new Vector3(frozen.X, 0, frozen.Z).MoveToward(Vector3.Zero, FreezeBrake * (float)delta);
        frozen.X = flat.X;
        frozen.Z = flat.Z;
        if (!IsOnFloor())
            frozen.Y -= _gravity * (float)delta;
        Velocity = frozen;
        MoveAndSlide();
    }

    /// <summary>Recuo de câmera: o cano sobe a cada tiro e volta
    /// sozinho. Sem isso, atirar não tem peso nenhum.</summary>
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

        if (IsOnFloor() || _onWall || !_form.IsWolf)
            _wolfJump = false;

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

        // O bote segue a trajetória do clique até encostar.
        if (_weapon.IsDiving)
        {
            DiveMovement();
            return;
        }

        // O salto voa no arco que a arma planejou.
        if (_weapon.IsPounceFlying)
        {
            PounceFlight(delta);
            return;
        }

        // Uivar trava você no lugar — é o preço da cura —, e o salto e a
        // aterrissagem do bote também: golpe pesado não se faz andando.
        if (IsHowling || _weapon.IsRooted)
        {
            Freeze(delta);
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

    /// <summary>Especial apertado junto com o pulo: vira bote no quadro
    /// seguinte.</summary>
    private bool _diveAfterJump;

    /// <summary>
    /// Rugido: segurar R do começo ao fim da animação devolve o escudo
    /// cheio. Soltar no meio corta o rugido sem escudo. Como o escudo
    /// escorre sozinho, rugir é decidir ficar exposto agora para
    /// sobreviver depois — e o som entrega a posição, que é a outra
    /// metade do preço.
    ///
    /// Morrer ou deixar de ser lobo no meio também corta sem pagar.
    /// </summary>
    private void UpdateHowl(double delta)
    {
        if (!_howling)
        {
            bool starts = Input.IsActionJustPressed("howl")
                && _form.CanHowl
                && !_weapon.IsStriking
                && !_health.IsDead
                && !UiState.AnyOpen;

            if (!starts) return;

            _howling = true;
            _howlProgress = 0;
        }

        // Segurar é o compromisso: soltar R no meio corta o rugido sem
        // escudo (decisão do Jhon). Morrer ou deixar de ser lobo também.
        if (_health.IsDead || !_form.IsWolf || !Input.IsActionPressed("howl"))
        {
            _howling = false;
            _howlProgress = 0;
            return;
        }

        _howlProgress += delta;
        if (_howlProgress < HowlDuration) return;

        _health.RestoreShield(_health.MaxShield);
        _howling = false;
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

        // Rugindo, o lobo não faz mais nada até o rugido acabar.
        if (_howling)
        {
            UpdateHowl(delta);
            return;
        }

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

        // Botão direito: salto no chão, bote no ar. Quem move o corpo
        // durante os dois é o passo de física, pela arma.
        //
        // Pulo e especial no mesmo quadro: quem apertou os dois quer o
        // bote, não o salto do chão. O pulo sai agora e o bote no quadro
        // seguinte, já no ar.
        if (_diveAfterJump)
        {
            _diveAfterJump = false;
            if (!IsOnFloor()) _weapon.TrySpecial();
        }
        else if (Input.IsActionJustPressed("special"))
        {
            if (_form.IsWolf && IsOnFloor() && Input.IsActionJustPressed("jump"))
                _diveAfterJump = true;
            else
                _weapon.TrySpecial(Input.IsActionPressed("move_forward"));
        }

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

            // O lobo pula para cima, não para a frente.
            if (_form.IsWolf)
            {
                _wolfJump = true;
                velocity.X *= WolfJumpCarry;
                velocity.Z *= WolfJumpCarry;
            }

            _animator ??= GetNodeOrNull<CharacterAnimator>("Human");
            _animator?.Jump();
        }

        Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Vector3 direction = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
        float speed = HumanSpeed * SpeedScale;
        if (_wolfJump) speed *= WolfJumpCarry;

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

        // O passo da garrada parada: o corpo anda junto com o pé do clipe.
        if (_weapon.IsStepping)
            horizontal = _weapon.StepVelocity;

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

        bool found = ProbeWall(out Vector3 normal, out float wallDistance);

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

            // Gruda no alto do pulo, não na subida: grudar subindo levava o
            // impulso para cima da parede e o lobo subia em vez de ficar na
            // altura em que encostou.
            if (Velocity.Y > WallGrabMaxRise)
            {
                WallLog("subindo — gruda no alto do pulo");
                return false;
            }

            if (IsNearFloor())
            {
                WallLog("perto demais do chão");
                return false;
            }

            // Indo embora da parede não gruda: passou da ponta dela, ou
            // saltou para longe. Sem isto a ponta que o lobo acabou de
            // deixar o puxava de volta.
            if (new Vector3(Velocity.X, 0, Velocity.Z).Dot(normal) > WallLeaveSpeed)
            {
                WallLog("indo para longe da parede");
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
            // O ponto de parede mais perto ficou para trás: ou a parede
            // acabou (quina) ou o lobo acabou de virar num canto e a parede
            // de trás é a antiga. No canto há outra parede ao lado.
            if (normal.Dot(_wallAlong) * wallDistance > WallEndMargin
                && ProbeWall(out Vector3 beside, out float besideDistance, behind: true)
                && beside.Dot(_wallAlong) * besideDistance <= WallEndMargin)
            {
                normal = beside;
                wallDistance = besideDistance;
            }

            // Passou da ponta: segue reto, sem contornar. Contornar trazia
            // o lobo de volta pela outra face — numa parede fina, ele
            // voltava pelo outro lado em vez de continuar.
            if (normal.Dot(_wallAlong) * wallDistance > WallEndMargin)
            {
                WallLog("a parede acabou: segue reto");
                LeaveWallStraight();
                return false;
            }

            _wallNormal = normal;
            _cornerGrace = 0;

            // Parede que vira para dentro (côncava): o sentido deita no
            // plano dela e acompanha. Para fora, não: é a ponta, tratada
            // acima. Nunca vira para trás.
            if (normal.Dot(_wallAlong) < 0f)
            {
                Vector3 along = _wallAlong - normal * _wallAlong.Dot(normal);
                along.Y = 0;
                if (along.LengthSquared() > 0.04f)
                    _wallAlong = along.Normalized();
            }
        }
        else if (_cornerGrace <= 0)
        {
            // Perdeu a parede: segue reto um instante antes de soltar.
            _cornerGrace = CornerGrace;
            WallLog("perdeu a parede");
        }
        else
        {
            _cornerGrace -= delta;

            if (_cornerGrace <= 0)
            {
                WallLog("soltou: nada para encostar");
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

        // Parede à frente: é um canto. Ela vira a parede da corrida e o
        // lobo segue por ela, para longe da antiga, no mesmo passo. Antes
        // ele soltava e regrudava na parede de trás, voltando.
        if (IsBlocked(_wallAlong, out Vector3 front))
        {
            Vector3 turn = _wallNormal - front * _wallNormal.Dot(front);
            turn.Y = 0;

            if (turn.LengthSquared() < 0.04f || IsBlocked(turn.Normalized(), out _))
            {
                WallLog("soltou, caminho bloqueado à frente");
                ReleaseWall();
                return false;
            }

            float pace = new Vector3(Velocity.X, 0, Velocity.Z).Dot(_wallAlong);
            _wallAlong = turn.Normalized();
            _wallNormal = front;
            velocity = _wallAlong * Mathf.Max(pace, 0f) + Vector3.Up * velocity.Y;
            WallLog("canto: segue pela parede da frente");
        }

        // A altura fica a do instante em que grudou. O pulo não é morto
        // por isso: o lobo só gruda no alto do pulo (WallGrabMaxRise), e
        // pular colado na parede sobe primeiro e gruda depois.
        velocity.Y = WallRunVerticalDrift;

        // Agarrado com o botão de ataque e sem o pulo: fica parado.
        float target = _clinging && !Input.IsActionPressed("jump")
            ? 0f
            : HumanSpeed * SpeedScale * WallRunSpeedBonus;

        // O passo muda aos poucos: acelera ao grudar e freia ao agarrar,
        // em vez de saltar entre parado e correndo num quadro. É o que
        // deixa parar no ponto certo da parede. O olhar continua livre —
        // a direção é a escolhida ao grudar.
        float current = new Vector3(velocity.X, 0, velocity.Z).Dot(_wallAlong);
        float rate = target < current ? WallBrake : WallAccel;
        float speed = Mathf.MoveToward(current, target, rate * (float)delta);

        // Puxada contra a parede proporcional à folga: mantém o corpo a
        // WallHoldDistance dela. Força fixa batia na quina e empurrava o
        // lobo para dentro do canto; proporcional, ele contorna a aresta
        // na mesma distância. Sem parede (instante de tolerância), nada.
        float stick = _cornerGrace > 0
            ? 0f
            : Mathf.Clamp((wallDistance - WallHoldDistance) * 12f, -2f, WallStickForce);

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

    /// <summary>A parede acabou: solta e segue no sentido da corrida, com
    /// o mesmo passo, sem a puxada contra a parede. Uma recarga curta
    /// leva o corpo para fora do alcance da ponta antes de poder grudar
    /// de novo.</summary>
    private void LeaveWallStraight()
    {
        float pace = new Vector3(Velocity.X, 0, Velocity.Z).Dot(_wallAlong);
        Velocity = _wallAlong * Mathf.Max(pace, 0f) + Vector3.Up * Velocity.Y;
        ReleaseWall();
        _wallCooldown = WallRunCooldown;
    }

    /// <summary>
    /// A direção da corrida sai do movimento que o lobo já tem, não de
    /// para onde a câmera aponta: saltar de uma parede e encostar na
    /// seguinte mantém o sentido sem tocar no mouse.
    ///
    /// O olhar só decide quando não há momento horizontal — caso de
    /// quem pula parado contra a parede.
    /// </summary>
    private Vector3 ChooseDirection(Vector3 normal, out bool byMomentum)
    {
        Vector3 along = normal.Cross(Vector3.Up).Normalized();

        Vector3 flat = new Vector3(Velocity.X, 0, Velocity.Z);
        float momentum = flat.Dot(along);

        byMomentum = Mathf.Abs(momentum) > 0.5f;
        if (byMomentum)
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
        along = ChooseDirection(normal, out bool byMomentum);

        if (!IsBlocked(along, out _)) return true;

        // Com embalo, o sentido é o do embalo: virar ao contrário fazia o
        // lobo voltar pela parede ao chegar num canto.
        if (byMomentum) return false;

        along = -along;
        return !IsBlocked(along, out _);
    }

    private PhysicsShapeQueryParameters3D _wallProbe;

    /// <summary>
    /// O ponto de parede mais perto do corpo, num cilindro do alcance de
    /// grudar em volta dele. A normal sai do ponto para o corpo, então
    /// numa aresta ela aponta na diagonal e gira suave enquanto o lobo
    /// contorna — o leque de raios antigo pulava de uma face para a outra
    /// na quina. O cilindro fica acima dos pés e abaixo da cabeça: chão e
    /// teto não contam. Gente também não é parede.
    /// </summary>
    private Rid _wallBody;

    private bool ProbeWall(out Vector3 normal, out float distance, bool behind = false)
    {
        normal = Vector3.Zero;
        distance = 0f;

        _wallProbe ??= new PhysicsShapeQueryParameters3D
        {
            Shape = new CylinderShape3D { Radius = WallHoldDistance + WallCheckDistance * 0.55f, Height = 0.9f },
            CollideWithAreas = false
        };

        var space = GetWorld3D().DirectSpaceState;
        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        _wallProbe.Transform = new Transform3D(Basis.Identity, GlobalPosition);
        _wallProbe.Exclude = exclude;

        foreach (var hit in space.IntersectShape(_wallProbe, 8))
            if (hit["collider"].Obj is CharacterBody3D) exclude.Add((Rid)hit["rid"]);

        // A segunda busca, no canto: sem a parede que ficou para trás.
        if (behind && _wallBody.IsValid) exclude.Add(_wallBody);
        _wallProbe.Exclude = exclude;

        var rest = space.GetRestInfo(_wallProbe);
        if (rest.Count == 0) return false;
        if (!behind) _wallBody = (Rid)rest["rid"];

        Vector3 away = GlobalPosition - (Vector3)rest["point"];
        away.Y = 0;

        if (away.LengthSquared() < 1e-6f)
        {
            away = (Vector3)rest["normal"];
            away.Y = 0;
            if (away.LengthSquared() < 1e-6f) return false;
        }

        normal = away.Normalized();
        distance = away.Length();
        return true;
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
    private bool IsBlocked(Vector3 along, out Vector3 normal)
    {
        normal = Vector3.Zero;
        Vector3 origin = GlobalPosition;

        var query = PhysicsRayQueryParameters3D.Create(
            origin, origin + along * WallRunClearance);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return false;

        Vector3 n = (Vector3)hit["normal"];
        if (n.Dot(along) > 0) n = -n;
        if (Mathf.Abs(n.Y) >= 0.4f) return false;

        n.Y = 0;
        normal = n.Normalized();
        return true;
    }
}
