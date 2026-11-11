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

    /// <summary>Força do empurrão ao saltar da parede.</summary>
    [Export] public float WallJumpPush = 6.5f;

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
            _onWall = false;
            _wallTimer = 0;
        }

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
    private bool TryWallRun(double delta)
    {
        if (!_form.IsWolf || IsOnFloor() || IsStunned)
        {
            _onWall = false;
            return false;
        }

        // Precisa estar querendo ir para frente. Soltar o W solta a parede.
        float forward = Input.GetActionStrength("move_forward");
        if (forward < 0.1f)
        {
            _onWall = false;
            return false;
        }

        if (!FindWall(out Vector3 normal))
        {
            _onWall = false;
            return false;
        }

        // Entrando na parede agora: reinicia o cronômetro.
        if (!_onWall)
        {
            _onWall = true;
            _wallTimer = WallRunMaxTime;
            _wallNormal = normal;

            // A direção é escolhida UMA vez, no momento de grudar, pelo
            // lado para onde o jogador olhava. Depois disso o olhar fica
            // livre para mirar sem desviar a corrida.
            _wallAlong = normal.Cross(Vector3.Up).Normalized();

            if (_wallAlong.Dot(-GlobalTransform.Basis.Z) < 0)
                _wallAlong = -_wallAlong;
        }
        else
        {
            _wallNormal = normal;

            if (WallRunMaxTime > 0)
            {
                _wallTimer -= delta;

                if (_wallTimer <= 0)
                {
                    _onWall = false;
                    return false;
                }
            }
        }

        Vector3 velocity = Velocity;

        // Salto da parede: empurra para fora e para cima. É a saída
        // que transforma a corrida em jogada ofensiva.
        if (Input.IsActionJustPressed("jump"))
        {
            velocity = _wallNormal * WallJumpPush;
            velocity.Y = JumpVelocity * JumpScale * 1.1f;

            _onWall = false;
            Velocity = velocity;
            MoveAndSlide();
            return true;
        }

        // Altura travada: na parede o lobo não sobe nem desce.
        velocity.Y = WallRunVerticalDrift;

        // A direção acompanha a curva da parede, mas nunca se inverte:
        // dos dois sentidos possíveis, fica com o que continua o
        // movimento já em curso. Para ir para o outro lado é preciso
        // cair e recomeçar.
        Vector3 along = _wallNormal.Cross(Vector3.Up).Normalized();

        if (along.Dot(_wallAlong) < 0)
            along = -along;

        _wallAlong = along;

        // Caminho bloqueado à frente: solta da parede e cai.
        if (IsBlocked(along))
        {
            _onWall = false;
            return false;
        }

        float speed = HumanSpeed * SpeedScale * WallRunSpeedBonus;
        velocity.X = along.X * speed;
        velocity.Z = along.Z * speed;

        Velocity = velocity;
        MoveAndSlide();
        return true;
    }

    /// <summary>Procura parede à esquerda e à direita. Devolve a normal
    /// da que encontrar primeiro.</summary>
    private bool FindWall(out Vector3 normal)
    {
        normal = Vector3.Zero;

        Vector3 origin = GlobalPosition;
        Vector3 right = GlobalTransform.Basis.X;

        if (Cast(origin, right, out normal)) return true;
        if (Cast(origin, -right, out normal)) return true;

        return false;
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

    private bool Cast(Vector3 origin, Vector3 direction, out Vector3 normal)
    {
        normal = Vector3.Zero;

        var query = PhysicsRayQueryParameters3D.Create(
            origin, origin + direction * WallCheckDistance);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return false;

        Vector3 n = (Vector3)hit["normal"];

        // Rejeita chão e teto: só parede vale.
        if (Mathf.Abs(n.Y) > 0.4f) return false;

        normal = n;
        return true;
    }
}
