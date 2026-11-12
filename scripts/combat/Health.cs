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

    /// <summary>Time do dono. Define fogo amigo e a cor no feed.</summary>
    [Export] public int Team = Teams.Blue;

    /// <summary>Nome exibido no feed de abates.</summary>
    [Export] public string DisplayName = "";

    /// <summary>
    /// Nome para mostrar, nunca vazio.
    ///
    /// Bots não tinham <see cref="DisplayName"/>, e quem reportava o
    /// abate usava <c>DisplayName ?? nome do nó</c> — que não cai no
    /// nó, porque string vazia não é nula. O feed mostrava abates sem
    /// autor, e um placar por jogador juntaria todos os bots numa linha
    /// só, a do nome "".
    /// </summary>
    public string PublicName => string.IsNullOrEmpty(DisplayName)
        ? GetParent()?.Name.ToString() ?? Name.ToString()
        : DisplayName;

    [Export] public float MaxHp = 200f;
    [Export] public float RespawnDelay = 3f;

    /// <summary>Segundos de imortalidade ao renascer. Sem isso, quem
    /// morre perto de um inimigo volta direto para a mesma morte.</summary>
    [Export] public float SpawnProtection = 3f;

    public bool IsProtected => _protection > 0;

    /// <summary>Renascer sozinho ao fim da contagem. O jogador desliga
    /// isto: quem escolhe lote decide quando volta.</summary>
    [Export] public bool AutoRespawn = true;

    /// <summary>Carência antes do escudo voltar a escorrer depois de
    /// ser recarregado. Sem ela, o rugido termina e o escudo já começa
    /// a cair no mesmo instante — o jogador não vê o que ganhou.</summary>
    [Export] public float ShieldDecayGrace = 1f;

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

    /// <summary>Multiplicador do dano recebido no corpo. Headshot não
    /// passa por aqui: é a saída que o atirador tem contra um corpo
    /// resistente.</summary>
    public float BodyResistance { get; set; } = 1f;

    private double _respawnTimer;
    private double _protection;
    private double _decayHold;

    public override void _Ready()
    {
        if (string.IsNullOrEmpty(DisplayName))
            DisplayName = GetParent().Name;

        Hp = MaxHp;
        EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
    }

    public override void _Process(double delta)
    {
        if (_decayHold > 0)
            _decayHold -= delta;

        if (!IsDead && Shield > 0 && ShieldDecay > 0 && _decayHold <= 0)
        {
            Shield = Mathf.Max(Shield - ShieldDecay * (float)delta, 0);
            EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
        }

        if (_protection > 0)
            _protection -= delta;

        if (!IsDead) return;

        _respawnTimer -= delta;

        if (_respawnTimer <= 0 && AutoRespawn)
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

    public void TakeDamage(float amount, int attackerId = 0, Vector3 from = default, bool headshot = false)
    {
        if (IsDead || amount <= 0 || IsProtected) return;

        float dealt = amount * DamageMultiplier;

        if (!headshot)
            dealt *= BodyResistance;
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
        _decayHold = ShieldDecayGrace;

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

    /// <summary>Renasce agora, respeitando o tempo mínimo de espera.
    /// Usado por quem controla o próprio renascimento.</summary>
    public bool TryRespawn()
    {
        if (!IsDead || _respawnTimer > 0) return false;

        Respawn();
        return true;
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
