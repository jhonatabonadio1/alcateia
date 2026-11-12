using Godot;

/// <summary>
/// Vida, escudo, morte e respawn.
///
/// O escudo é separado do HP de propósito: o documento aponta isso como
/// um dos detalhes que vendem a lembrança. Dano come escudo primeiro.
///
/// Fase 1 roda local. Na Fase 2 o TakeDamage passa a ser chamado
/// SOMENTE no servidor — o cliente nunca decide dano.
/// </summary>
public partial class Health : Node
{
    [Signal] public delegate void ChangedEventHandler(float hp, float maxHp, float shield);
    [Signal] public delegate void DiedEventHandler(int killerId);
    [Signal] public delegate void RespawnedEventHandler();

    /// <summary>Levou dano, e de onde veio. A posição permite ao bot
    /// reagir a tiro pelas costas e ao HUD apontar a direção.</summary>
    [Signal] public delegate void DamagedEventHandler(float amount, Vector3 from);

    [Export] public float MaxHp = 200f;
    [Export] public float RespawnDelay = 3f;

    /// <summary>Segundos de imortalidade ao renascer. Sem isso, quem
    /// morre perto de um inimigo volta direto para a mesma morte.</summary>
    [Export] public float SpawnProtection = 3f;

    public bool IsProtected => _protection > 0;

    /// <summary>Escudo perdido por segundo. Zero para quem não tem
    /// escudo que vaza, como o humano.</summary>
    public float ShieldDecay { get; set; }

    /// <summary>Segundos restantes até reviver. O HUD mostra isto —
    /// morrer sem aviso na tela é indistinguível de travar.</summary>
    public float RespawnIn => (float)Mathf.Max(_respawnTimer, 0);

    public float Hp { get; private set; }
    public float MaxShield { get; private set; }
    public float Shield { get; private set; }
    public bool IsDead { get; private set; }

    /// <summary>Multiplicador aplicado ao dano recebido. A transformação
    /// sobe isso — transformar tem que ter risco, senão a economia de WP
    /// não significa nada.</summary>
    public float DamageMultiplier { get; set; } = 1f;

    private double _respawnTimer;
    private double _protection;

    public override void _Ready()
    {
        Hp = MaxHp;
        EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
    }

    public override void _Process(double delta)
    {
        if (!IsDead && Shield > 0 && ShieldDecay > 0)
        {
            Shield = Mathf.Max(Shield - ShieldDecay * (float)delta, 0);
            EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
        }

        if (_protection > 0)
            _protection -= delta;

        if (!IsDead) return;

        _respawnTimer -= delta;
        if (_respawnTimer <= 0)
            Respawn();
    }

    /// <summary>Troca o pool de vida ao mudar de forma. O lobo não
    /// "cura" ao transformar: ele passa a usar outro corpo.</summary>
    public void SetPool(float maxHp, float maxShield, bool refill, bool keepRatio = false)
    {
        float oldMaxHp = MaxHp;
        float oldHp = Hp;

        MaxHp = maxHp;
        MaxShield = maxShield;

        if (refill)
        {
            Hp = maxHp;
            Shield = maxShield;
        }
        else if (keepRatio && oldMaxHp > 0)
        {
            // Corpo novo, mesmo estado: meio de vida como humano vira
            // meio de vida como lobo. Sem isto, transformar cura.
            Hp = maxHp * Mathf.Clamp(oldHp / oldMaxHp, 0f, 1f);
            Shield = maxShield;
        }
        else
        {
            Hp = Mathf.Min(Hp, maxHp);
            Shield = Mathf.Min(Shield, maxShield);
        }

        EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
    }

    /// <summary>Define a vida diretamente, sem contar como dano. Usado
    /// ao restaurar o estado guardado de uma forma.</summary>
    public void SetHp(float value)
    {
        Hp = Mathf.Clamp(value, 1f, MaxHp);
        EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
    }

    public void TakeDamage(float amount, int attackerId = 0, Vector3 from = default)
    {
        if (IsDead || amount <= 0 || IsProtected) return;

        float dealt = amount * DamageMultiplier;
        EmitSignal(SignalName.Damaged, dealt, from);

        // Escudo absorve primeiro.
        if (Shield > 0)
        {
            float absorbed = Mathf.Min(Shield, dealt);
            Shield -= absorbed;
            dealt -= absorbed;
        }

        Hp -= dealt;
        EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);

        if (Hp <= 0)
            Die(attackerId);
    }

    /// <summary>Recarga de escudo pelo uivo. Nunca passa do teto —
    /// o Smart Wolf original provou que cura sem teto quebra o jogo.</summary>
    public void RestoreShield(float amount)
    {
        if (IsDead) return;

        Shield = Mathf.Min(Shield + amount, MaxShield);
        EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
    }

    private void Die(int killerId)
    {
        IsDead = true;
        Hp = 0;
        Shield = 0;
        _respawnTimer = RespawnDelay;

        EmitSignal(SignalName.Died, killerId);
        EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
    }

    private void Respawn()
    {
        IsDead = false;
        Hp = MaxHp;
        Shield = MaxShield;
        _protection = SpawnProtection;

        EmitSignal(SignalName.Respawned);
        EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
    }
}
