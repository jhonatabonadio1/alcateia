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

    [Export] public float MaxHp = 200f;
    [Export] public float RespawnDelay = 3f;

    public float Hp { get; private set; }
    public float MaxShield { get; private set; }
    public float Shield { get; private set; }
    public bool IsDead { get; private set; }

    /// <summary>Multiplicador aplicado ao dano recebido. A transformação
    /// sobe isso — transformar tem que ter risco, senão a economia de WP
    /// não significa nada.</summary>
    public float DamageMultiplier { get; set; } = 1f;

    private double _respawnTimer;

    public override void _Ready()
    {
        Hp = MaxHp;
        EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
    }

    public override void _Process(double delta)
    {
        if (!IsDead) return;

        _respawnTimer -= delta;
        if (_respawnTimer <= 0)
            Respawn();
    }

    /// <summary>Troca o pool de vida ao mudar de forma. O lobo não
    /// "cura" ao transformar: ele passa a usar outro corpo.</summary>
    public void SetPool(float maxHp, float maxShield, bool refill)
    {
        MaxHp = maxHp;
        MaxShield = maxShield;

        if (refill)
        {
            Hp = maxHp;
            Shield = maxShield;
        }
        else
        {
            Hp = Mathf.Min(Hp, maxHp);
            Shield = Mathf.Min(Shield, maxShield);
        }

        EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
    }

    public void TakeDamage(float amount, int attackerId = 0)
    {
        if (IsDead || amount <= 0) return;

        float dealt = amount * DamageMultiplier;

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
        EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
    }
}
