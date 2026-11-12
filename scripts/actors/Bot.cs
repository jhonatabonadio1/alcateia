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
    //
    // O bot é um atirador. Virar lobo é recurso de desespero, não
    // tática padrão: como lobo é corpo-a-corpo, um bot que transforma
    // cedo gruda no jogador e nunca mais atira.

    /// <summary>Vira lobo abaixo desta fração de vida.</summary>
    [Export] public float WolfHealthThreshold = 0.4f;

    /// <summary>E só se o alvo estiver a esta distância ou menos —
    /// virar lobo longe do alvo é suicídio.</summary>
    [Export] public float WolfRange = 14f;

    /// <summary>Volta a humano acima desta distância.</summary>
    [Export] public float HumanRange = 20f;

    [Export] public float DecisionInterval = 0.4f;

    // --- Combate ---

    /// <summary>Duração da rajada e da pausa entre elas. Fogo contínuo
    /// não dá ao jogador nenhuma janela para reagir.</summary>
    [Export] public float BurstTime = 0.7f;
    [Export] public float BurstPause = 0.6f;

    /// <summary>Alcance efetivo. Além disso não atira: um bot que
    /// acerta do outro lado do mapa é insuportável.</summary>
    [Export] public float EngageRange = 45f;

    /// <summary>Velocidade do deslocamento lateral em combate.</summary>
    [Export] public float StrafeFactor = 0.8f;

    /// <summary>Intervalo entre trocas de lado no strafe.</summary>
    [Export] public float StrafeInterval = 1.4f;

    // --- Pontaria ---

    /// <summary>Erro de mira em graus. Zero faz um bot desumano.</summary>
    [Export] public float AimError = 4f;

    /// <summary>Fração dos tiros que erra de propósito, com desvio
    /// bem maior. É o que separa um bot fácil de um difícil mais que
    /// qualquer outra coisa — e mesmo no difícil não vai a zero: um
    /// bot que acerta sempre não é difícil, é injusto.</summary>
    [Export] public float MissChance = 0.2f;

    /// <summary>Desvio em graus de um tiro errado de propósito.</summary>
    [Export] public float MissSpread = 14f;

    // --- Reação a levar tiro ---

    /// <summary>Atraso entre levar tiro e reagir. Virar na hora é
    /// desumano e tira do jogador o prêmio de atacar pelas costas.</summary>
    [Export] public float ReactionTime = 0.35f;

    /// <summary>Variação aleatória do atraso, para dois bots não
    /// reagirem em sincronia.</summary>
    [Export] public float ReactionJitter = 0.2f;

    /// <summary>Velocidade de giro ao se virar para o atacante. Mais
    /// lenta que o giro normal: o bot foi pego de surpresa.</summary>
    [Export] public float AlertTurnSpeed = 4.5f;

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
    private double _burstTimer;
    private bool _firing;
    private double _strafeTimer;
    private int _strafeSide = 1;
    private double _reactionTimer;
    private Vector3 _threatFrom;
    private bool _reacting;
    private bool _alerted;

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

        _health.Died += OnDied;
        _health.Damaged += OnDamaged;
        _health.Respawned += OnRespawned;

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

        UpdateReaction(delta);

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

        UpdateBurst(delta);
        UpdateStrafe(delta);

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
        float healthRatio = _health.MaxHp > 0 ? _health.Hp / _health.MaxHp : 1f;

        if (!_form.IsWolf)
        {
            // Encurralado e com o alvo ao alcance: aposta no lobo.
            if (healthRatio <= WolfHealthThreshold && distance <= WolfRange)
                _weapon.SelectSlot(Slot.Wolf);

            return;
        }

        // Alvo escapou, ou a vida voltou ao normal depois de renascer.
        if (distance > HumanRange || healthRatio > WolfHealthThreshold)
            _weapon.SelectSlot(Slot.Primary);
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

    /// <summary>Troca de lado periodicamente. Um bot parado em linha
    /// reta é alvo de tiro ao prato.</summary>
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
    /// em vez de um chuvisco constante de quase-acertos.
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

    private void FaceTarget(double delta)
    {
        Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
        Vector3 flat = new Vector3(toTarget.X, 0, toTarget.Z);

        if (flat.LengthSquared() < 0.01f) return;

        float desired = Mathf.Atan2(-flat.X, -flat.Z) + _aimOffset.Y;

        // Pego de surpresa, gira mais devagar até ficar de frente.
        float turn = _alerted ? AlertTurnSpeed : TurnSpeed;
        float next = Mathf.LerpAngle(Rotation.Y, desired, turn * (float)delta);

        if (_alerted && Mathf.Abs(Mathf.AngleDifference(next, desired)) < 0.15f)
            _alerted = false;

        Rotation = new Vector3(0, next, 0);

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
        Vector3 side = flat.Cross(Vector3.Up).Normalized() * _strafeSide;
        Vector3 desired;

        if (_form.IsWolf)
        {
            // Como lobo, fechar a distância é a única jogada.
            desired = flat;
        }
        else if (distance > PreferredRange * 1.25f)
        {
            // Longe demais: aproxima, mas já deslocando de lado.
            desired = (flat + side * StrafeFactor * 0.5f).Normalized();
        }
        else if (distance < PreferredRange * 0.7f)
        {
            // Perto demais: recua sem dar as costas.
            desired = (-flat + side * StrafeFactor * 0.5f).Normalized();
        }
        else
        {
            // Na distância certa: só circula. É aqui que ele atira,
            // em vez de grudar no jogador.
            desired = side * StrafeFactor;
        }

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

    private void Engage(float distance)
    {
        if (!_form.CanAttack) return;

        float reach = _form.IsWolf
            ? _form.Breed.AttackRange
            : Mathf.Min(EngageRange, _weapon.PrimaryRange);

        if (distance > reach) return;

        // A garrada não tem rajada: é um golpe por cooldown.
        if (!_form.IsWolf && !_firing) return;

        if (!HasLineOfSight()) return;

        _weapon.TryAttack();
    }

    /// <summary>
    /// Levou tiro. Não reage na hora: marca a origem e conta um tempo
    /// curto antes de se virar. Reação instantânea é desumana e anula
    /// o prêmio de atacar pelas costas.
    /// </summary>
    private void OnDamaged(float amount, Vector3 from)
    {
        if (_health.IsDead) return;
        if (from == Vector3.Zero) return;
        if (_reacting) return;

        _reacting = true;
        _threatFrom = from;
        _reactionTimer = ReactionTime + GD.RandRange(0, ReactionJitter);
    }

    private void UpdateReaction(double delta)
    {
        if (!_reacting) return;

        _reactionTimer -= delta;
        if (_reactionTimer > 0) return;

        _reacting = false;

        // Acordou: passa a saber que há alguém, mesmo sem ter visto.
        // O giro em si é gradual, feito pelo FaceTarget.
        _memoryTimer = Memory;
        _alerted = true;
    }

    private void OnDied(int killerId)
    {
        if (Debug) GD.Print($"{Name} morreu");

        _memoryTimer = 0;
    }

    /// <summary>Só volta ao spawn ao reviver, não ao morrer: o corpo
    /// precisa ficar onde caiu durante a contagem.</summary>
    private void OnRespawned()
    {
        GlobalPosition = _spawn;
        Velocity = Vector3.Zero;
        _memoryTimer = 0;

        if (_form.IsWolf)
            _weapon.SelectSlot(Slot.Primary);
    }
}
