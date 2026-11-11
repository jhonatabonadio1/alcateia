using Godot;

/// <summary>
/// Oponente controlado por código.
///
/// Reusa Health, FormController e Weapon — os mesmos componentes do
/// jogador. Isso não é economia de código: é o que garante que o
/// balanceamento testado contra o bot valha contra uma pessoa. Um bot
/// com regras próprias mediria outro jogo.
///
/// Navegação é por direção e desvio, não por caminho calculado. Num
/// mapa aberto funciona; em corredores ele vai encostar em quina.
/// Navegação de verdade pede navmesh, e navmesh pede mapa.
///
/// Fase 2: o bot roda só no servidor. Nada aqui é replicado.
/// </summary>
public partial class Bot : CharacterBody3D
{
    // --- Percepção ---

    [Export] public float SightRange = 40f;

    /// <summary>Campo de visão em graus, total.</summary>
    [Export] public float FieldOfView = 140f;

    /// <summary>Tempo que continua perseguindo depois de perder de
    /// vista. Sem isso o bot desiste no primeiro obstáculo.</summary>
    [Export] public float Memory = 3f;

    // --- Movimento ---

    [Export] public float Speed = 4.2f;
    [Export] public float TurnSpeed = 8f;

    /// <summary>Distância que tenta manter do alvo na forma humana.
    /// Perto demais, a arma perde a vantagem.</summary>
    [Export] public float PreferredRange = 12f;

    // --- Decisão de forma ---

    /// <summary>Abaixo desta distância, vira lobo: é onde o
    /// corpo-a-corpo ganha da arma.</summary>
    [Export] public float WolfRange = 9f;

    /// <summary>Acima desta, volta a humano.</summary>
    [Export] public float HumanRange = 16f;

    [Export] public float DecisionInterval = 0.4f;

    // --- Pontaria ---

    /// <summary>Erro de mira em graus. Zero faz um bot desumano.</summary>
    [Export] public float AimError = 4f;

    [Export] public bool Debug = false;

    private Health _health;
    private FormController _form;
    private Weapon _weapon;
    private Node3D _head;
    private Node3D _target;

    private float _gravity = 9.8f;
    private Vector3 _spawn;
    private double _memoryTimer;
    private double _decisionTimer;
    private Vector3 _aimOffset;

    public override void _Ready()
    {
        _health = GetNode<Health>("Health");
        _form = GetNode<FormController>("FormController");
        _weapon = GetNode<Weapon>("Weapon");
        _head = GetNode<Node3D>("Head");

        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
        _spawn = GlobalPosition;

        _health.Died += OnDied;

        // O bot é dono de si: sem isto o Weapon e o FormController
        // rejeitariam os comandos num jogo em rede.
        SetMultiplayerAuthority(GetMultiplayerAuthority());
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_health.IsDead)
        {
            Velocity = new Vector3(0, Velocity.Y - _gravity * (float)delta, 0);
            MoveAndSlide();
            return;
        }

        _target ??= GetTree().GetFirstNodeInGroup("player") as Node3D;

        if (_target == null)
        {
            Idle(delta);
            return;
        }

        Perceive(delta);

        if (_memoryTimer <= 0)
        {
            Idle(delta);
            return;
        }

        _decisionTimer -= delta;

        if (_decisionTimer <= 0)
        {
            _decisionTimer = DecisionInterval;
            DecideForm();
            ShuffleAim();
        }

        Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
        float distance = new Vector3(toTarget.X, 0, toTarget.Z).Length();

        FaceTarget(delta);
        Advance(distance, toTarget, delta);
        Engage(distance);
    }

    // ------------------------------------------------------------------

    /// <summary>Vê o alvo? Precisa estar perto, à frente e sem parede
    /// no meio. Os três, não um dos três.</summary>
    private void Perceive(double delta)
    {
        _memoryTimer -= delta;

        Vector3 toTarget = _target.GlobalPosition - GlobalPosition;

        if (toTarget.Length() > SightRange) return;

        Vector3 flat = new Vector3(toTarget.X, 0, toTarget.Z).Normalized();
        Vector3 facing = -GlobalTransform.Basis.Z;

        float angle = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(flat.Dot(facing), -1f, 1f)));
        if (angle > FieldOfView * 0.5f) return;

        if (!HasLineOfSight()) return;

        _memoryTimer = Memory;
    }

    private bool HasLineOfSight()
    {
        var query = PhysicsRayQueryParameters3D.Create(
            _head.GlobalPosition, _target.GlobalPosition + Vector3.Up * 0.5f);

        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return true;

        return hit["collider"].Obj == _target;
    }

    private void Idle(double delta)
    {
        Vector3 velocity = Velocity;

        velocity.X = Mathf.MoveToward(velocity.X, 0, Speed * 3f * (float)delta);
        velocity.Z = Mathf.MoveToward(velocity.Z, 0, Speed * 3f * (float)delta);
        velocity.Y = IsOnFloor() ? 0 : velocity.Y - _gravity * (float)delta;

        Velocity = velocity;
        MoveAndSlide();
    }

    /// <summary>
    /// Vira lobo de perto, volta a humano de longe. As duas distâncias
    /// são diferentes de propósito: iguais, o bot ficaria oscilando
    /// entre as formas na fronteira.
    /// </summary>
    private void DecideForm()
    {
        float distance = GlobalPosition.DistanceTo(_target.GlobalPosition);

        if (!_form.IsWolf && distance < WolfRange)
            _weapon.SelectSlot(Slot.Wolf);
        else if (_form.IsWolf && distance > HumanRange)
            _weapon.SelectSlot(Slot.Primary);
    }

    private void ShuffleAim()
    {
        float spread = Mathf.DegToRad(AimError);

        _aimOffset = new Vector3(
            (float)GD.RandRange(-spread, spread),
            (float)GD.RandRange(-spread, spread),
            0);
    }

    private void FaceTarget(double delta)
    {
        Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
        Vector3 flat = new Vector3(toTarget.X, 0, toTarget.Z);

        if (flat.LengthSquared() < 0.01f) return;

        float desired = Mathf.Atan2(-flat.X, -flat.Z) + _aimOffset.Y;
        Rotation = new Vector3(
            0,
            Mathf.LerpAngle(Rotation.Y, desired, TurnSpeed * (float)delta),
            0);

        // A cabeça mira na altura certa; a arma parte dela.
        float height = toTarget.Y - _head.Position.Y;
        float pitch = Mathf.Atan2(height, flat.Length()) + _aimOffset.X;

        _head.Rotation = new Vector3(
            Mathf.Clamp(pitch, Mathf.DegToRad(-70f), Mathf.DegToRad(70f)),
            0, 0);
    }

    /// <summary>
    /// De lobo, fecha a distância. De humano, mantém o alcance da arma:
    /// aproxima se está longe, recua se está perto demais.
    /// </summary>
    private void Advance(float distance, Vector3 toTarget, double delta)
    {
        Vector3 flat = new Vector3(toTarget.X, 0, toTarget.Z).Normalized();
        Vector3 desired = Vector3.Zero;

        if (_form.IsWolf)
        {
            desired = flat;
        }
        else if (distance > PreferredRange * 1.2f)
        {
            desired = flat;
        }
        else if (distance < PreferredRange * 0.6f)
        {
            desired = -flat;
        }

        if (desired != Vector3.Zero)
            desired = Avoid(desired);

        Vector3 velocity = Velocity;
        float speed = Speed * (_form.IsWolf ? _form.Breed.SpeedMultiplier : 1f);

        velocity.X = Mathf.MoveToward(velocity.X, desired.X * speed, speed * 6f * (float)delta);
        velocity.Z = Mathf.MoveToward(velocity.Z, desired.Z * speed, speed * 6f * (float)delta);
        velocity.Y = IsOnFloor() ? 0 : velocity.Y - _gravity * (float)delta;

        Velocity = velocity;
        MoveAndSlide();
    }

    /// <summary>Desvio raso: se o caminho está fechado, tenta os lados.
    /// Não é pathfinding e não resolve labirinto.</summary>
    private Vector3 Avoid(Vector3 desired)
    {
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

    private void Engage(float distance)
    {
        if (!_form.CanAttack) return;

        float reach = _form.IsWolf ? _form.Breed.AttackRange : _weapon.PrimaryRange;

        if (distance > reach) return;
        if (!HasLineOfSight()) return;

        _weapon.TryAttack();
    }

    private void OnDied(int killerId)
    {
        if (Debug) GD.Print($"{Name} morreu");

        GlobalPosition = _spawn;
        Velocity = Vector3.Zero;
        _memoryTimer = 0;
    }
}
