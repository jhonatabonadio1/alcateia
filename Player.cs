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
    [Export] public float HumanSpeed = 7.0f;
    [Export] public float JumpVelocity = 5.0f;
    [Export] public float MouseSensitivity = 0.002f;

    [Export] public float HowlShield = 60f;
    [Export] public float HowlDuration = 1.2f;

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

    /// <summary>Força do empurrão para fora ao saltar da parede.</summary>
    [Export] public float WallJumpPush = 6.5f;

    /// <summary>Fração do avanço ao longo da parede que o salto
    /// conserva. Zero faz o lobo sair de lado e perder tudo que
    /// acumulou correndo.</summary>
    [Export] public float WallJumpKeepSpeed = 0.8f;

    /// <summary>Exigir tocar o chão entre uma corrida em parede e a
    /// seguinte. Sem isso o lobo regruda na mesma altura e a parede
    /// vira elevador. Desligue para permitir encadear saltos entre
    /// paredes opostas.</summary>
    [Export] public bool RequireFloorBetweenWallRuns = true;

    /// <summary>Tempo mínimo entre corridas quando o chão não é
    /// exigido. Impede regrudar na mesma parede no mesmo pulo.</summary>
    [Export] public float WallRunCooldown = 0.45f;

    /// <summary>Imprime por que a corrida em parede não começou.
    /// Desligue quando a física estiver no ponto.</summary>
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
    private double _howlTimer;

    private double _stunTimer;
    private float _stunSlow = 1f;

    /// <summary>Atordoado pela granada: lento e sem corrida em parede.
    /// É a única resposta que o humano tem contra o lobo.</summary>
    public bool IsStunned => _stunTimer > 0;

    private bool _onWall;
    private Vector3 _wallNormal;
    private Vector3 _wallAlong;
    private double _cornerGrace;
    private bool _wallSpent;
    private double _wallCooldown;
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

        // O braço recua sozinho quando há parede atrás — sem isso a
        // câmera atravessa o cenário durante a corrida em parede.
        _springArm.SpringLength = ThirdPersonDistance;
        _springArm.AddExcludedObject(GetRid());

        _form.FormChanged += OnFormChanged;

        SetProcessInput(IsMultiplayerAuthority());
        ApplyCamera();

        if (IsMultiplayerAuthority())
            Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void OnFormChanged(int form) => ApplyCamera();

    private void ApplyCamera()
    {
        bool isMine = IsMultiplayerAuthority();
        bool third = _form.IsThirdPerson;

        _camera.Current = isMine && !third;
        _wolfCamera.Current = isMine && third;

        // Em primeira pessoa o dono não vê o próprio corpo; os outros
        // jogadores continuam vendo normalmente.
        if (isMine)
            _mesh.Visible = third;
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsMultiplayerAuthority()) return;

        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-motion.Relative.X * MouseSensitivity);

            Vector3 headRot = _head.Rotation;
            headRot.X = Mathf.Clamp(
                headRot.X - motion.Relative.Y * MouseSensitivity,
                Mathf.DegToRad(-89f),
                Mathf.DegToRad(89f));
            _head.Rotation = headRot;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsMultiplayerAuthority()) return;

        if (_health.IsDead)
        {
            Velocity = Vector3.Zero;
            MoveAndSlide();
            return;
        }

        if (_howlTimer > 0)
            _howlTimer -= delta;

        if (_stunTimer > 0)
        {
            _stunTimer -= delta;
            if (_stunTimer <= 0)
                _stunSlow = 1f;
        }

        HandleActions();

        if (IsOnFloor())
        {
            ReleaseWall();
            _wallTimer = 0;
            _wallSpent = false;
        }

        if (_wallCooldown > 0)
            _wallCooldown -= delta;

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

    private bool IsHowling => _howlTimer > 0;

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
        (_form.IsWolf ? _form.Breed.SpeedMultiplier : 1f) * (IsStunned ? _stunSlow : 1f);
    private float JumpScale => _form.IsWolf ? _form.Breed.JumpMultiplier : 1f;

    private void HandleActions()
    {
        if (Input.IsActionJustPressed("slot_1")) _weapon.SelectSlot(Slot.Primary);
        if (Input.IsActionJustPressed("slot_2")) _weapon.SelectSlot(Slot.Secondary);
        if (Input.IsActionJustPressed("slot_3")) _weapon.SelectSlot(Slot.Wolf);
        if (Input.IsActionJustPressed("slot_4")) _weapon.SelectSlot(Slot.Grenade);

        // Só a arma principal é automática. A secundária e a garrada
        // exigem um clique por golpe.
        bool firing = _weapon.IsAutomatic
            ? Input.IsActionPressed("attack")
            : Input.IsActionJustPressed("attack");

        if (firing)
            _weapon.TryAttack();

        if (Input.IsActionJustPressed("howl") && _form.CanHowl && !IsHowling)
        {
            _howlTimer = HowlDuration;
            _health.RestoreShield(HowlShield);
        }
    }

    private void NormalMovement(double delta)
    {
        Vector3 velocity = Velocity;

        if (!IsOnFloor())
            velocity.Y -= _gravity * (float)delta;
        else if (Input.IsActionJustPressed("jump"))
            velocity.Y = JumpVelocity * JumpScale;

        Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Vector3 direction = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
        float speed = HumanSpeed * SpeedScale;

        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * speed;
            velocity.Z = direction.Z * speed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0, speed);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0, speed);
        }

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

        // Soltar o W solta a parede.
        if (Input.GetActionStrength("move_forward") < 0.1f)
        {
            ReleaseWall();
            return false;
        }

        bool found = FindWall(out Vector3 normal);

        if (!_onWall)
        {
            // Uma corrida por ida ao ar. Sem isto o lobo regruda na
            // mesma altura logo após o salto e a parede vira elevador.
            if (RequireFloorBetweenWallRuns && _wallSpent)
            {
                if (WallDebug) GD.Print("parede: já usou esta ida ao ar — precisa tocar o chão");
                return false;
            }

            if (_wallCooldown > 0) return false;

            // Só gruda na descida. Encostado na parede no chão não basta:
            // é preciso pular, e a aderência começa quando o lobo começa
            // a cair. Isso faz o gesto ser uma decisão, não um acidente.
            if (!found)
            {
                if (WallDebug && Velocity.Y <= 0)
                    GD.Print($"parede: nenhuma a menos de {WallCheckDistance}m");
                return false;
            }

            if (Velocity.Y > 0) return false;

            _wallSpent = true;

            _onWall = true;
            _wallTimer = WallRunMaxTime;
            _wallNormal = normal;
            _cornerGrace = 0;

            // Numa quina côncava o sentido escolhido pode apontar para
            // dentro da parede vizinha. Vira para o outro lado antes de
            // desistir, senão o lobo solta no mesmo frame em que gruda.
            if (!ResolveDirection(normal, out _wallAlong))
            {
                if (WallDebug) GD.Print("parede: os dois sentidos estão bloqueados");
                _onWall = false;
                _wallSpent = false;
                return false;
            }

            if (WallDebug) GD.Print($"parede: grudou, normal {_wallNormal}, sentido {_wallAlong}");
        }
        else if (found)
        {
            _wallNormal = normal;
            _cornerGrace = 0;

            // Acompanha a curva da parede sem nunca inverter o sentido.
            Vector3 along = normal.Cross(Vector3.Up).Normalized();

            if (along.Dot(_wallAlong) < 0)
                along = -along;

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
        }
        else
        {
            _cornerGrace -= delta;

            if (_cornerGrace <= 0)
            {
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
        if (Input.IsActionJustPressed("jump"))
        {
            // Conserva o avanço ao longo da parede e soma o empurrão
            // para fora. Sem isso o salto zera tudo que a corrida
            // acumulou, e o lobo sai de lado em vez de seguir em frente
            // — justamente o oposto da jogada de cair em cima de alguém.
            float carried = HumanSpeed * SpeedScale * WallRunSpeedBonus * WallJumpKeepSpeed;

            velocity = _wallAlong * carried + _wallNormal * WallJumpPush;
            velocity.Y = JumpVelocity * JumpScale * 1.1f;

            ReleaseWall();
            _wallCooldown = WallRunCooldown;

            Velocity = velocity;
            MoveAndSlide();
            return true;
        }

        // Caminho bloqueado à frente: solta e cai.
        if (IsBlocked(_wallAlong))
        {
            if (WallDebug) GD.Print("parede: soltou, caminho bloqueado à frente");
            ReleaseWall();
            return false;
        }

        // Altura travada: na parede o lobo não sobe nem desce.
        velocity.Y = WallRunVerticalDrift;

        float speed = HumanSpeed * SpeedScale * WallRunSpeedBonus;

        // Sucção contra a parede, exceto ao contornar uma quina — ali
        // ela empurraria o lobo para dentro do canto e o prenderia.
        float stick = _cornerGrace > 0 ? 0f : WallStickForce;

        velocity.X = _wallAlong.X * speed - _wallNormal.X * stick;
        velocity.Z = _wallAlong.Z * speed - _wallNormal.Z * stick;

        Velocity = velocity;
        MoveAndSlide();
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

    /// <summary>Há obstáculo no caminho à frente, ao longo da parede?</summary>
    private bool IsBlocked(Vector3 along)
    {
        Vector3 origin = GlobalPosition;

        var query = PhysicsRayQueryParameters3D.Create(
            origin, origin + along * WallRunClearance);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        return GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0;
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

        // Rejeita chão e teto: só parede vale.
        if (Mathf.Abs(n.Y) > 0.4f) return false;

        normal = n;
        distance = origin.DistanceTo((Vector3)hit["position"]);
        return true;
    }
}
