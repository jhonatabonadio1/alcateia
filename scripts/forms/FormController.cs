using Godot;

public enum Form
{
    Human,
    ShiftingToWolf,
    Wolf,
    ShiftingToHuman
}

/// <summary>
/// A máquina de estados da transformação — o coração do jogo.
///
/// Os dois estados intermediários são ESTADOS REAIS, não "animação
/// tocando". Hitbox ambígua durante a transição gera dano fantasma,
/// e esse é o pior bug para rastrear quando a rede entrar.
///
///   Humano        --[tecla + WP]--> VirandoLobo
///   VirandoLobo   --[fim]-->        Lobo
///   Lobo          --[tecla]-->      VirandoHumano
///   VirandoHumano --[fim]-->        Humano
///
/// Fase 2: tudo isto passa a rodar no servidor. O cliente manda
/// intenção e recebe o estado. Por isso a lógica está isolada aqui,
/// sem ler Input diretamente.
/// </summary>
public partial class FormController : Node
{
    [Signal] public delegate void FormChangedEventHandler(int form);
    [Signal] public delegate void WpChangedEventHandler(float wp, float max);

    /// <summary>Tentou transformar sem WP. O HUD usa isto para avisar —
    /// falha silenciosa faz o jogador achar que o botão está quebrado.</summary>
    [Signal] public delegate void TransformDeniedEventHandler(float missing);

    [Export] public WolfBreed Breed;

    [Export] public float MaxWp = 1000f;
    [Export] public float WpPerSecond = 12f;
    [Export] public float WpPerDamageDealt = 0.5f;

    [Export] public float HumanHp = 200f;

    /// <summary>Duração de cada transição. Curta o bastante para ser
    /// um gesto, longa o bastante para o inimigo reagir.</summary>
    [Export] public float ShiftDuration = 0.55f;

    /// <summary>Transição de volta para humano. Mais curta que a ida:
    /// virar lobo é um compromisso, voltar é uma retirada — e punir a
    /// retirada com a mesma espera só faz o jogador se sentir travado.</summary>
    [Export] public float ShiftBackDuration = 0.2f;

    /// <summary>Vulnerabilidade extra durante a transição. É o que dá
    /// peso à decisão de transformar.</summary>
    [Export] public float ShiftDamageMultiplier = 1.5f;

    /// <summary>Encher a vida ao trocar de forma. Com transformação
    /// livre isto vira cura infinita e torna o uivo inútil, por isso
    /// o padrão é falso: a proporção de vida é preservada entre os
    /// corpos. Meio de vida como humano = meio de vida como lobo.</summary>
    [Export] public bool RefillOnTransform = false;

    public Form Current { get; private set; } = Form.Human;
    public float Wp { get; private set; }

    public bool CanAttack => Current == Form.Human || Current == Form.Wolf;

    /// <summary>
    /// Corpo de lobo: velocidade, pulo e corrida em parede.
    ///
    /// Vale já na transição PARA lobo e deixa de valer assim que a
    /// transição DE VOLTA começa — trocou de personagem, trocou de
    /// física, sem esperar a animação acabar. Esperar o fim fazia o
    /// jogador apertar 3, pular e receber o pulo humano.
    ///
    /// A vulnerabilidade da transição continua: o que não vale durante
    /// ela é atacar, não se mover.
    /// </summary>
    public bool IsWolf => Current == Form.ShiftingToWolf || Current == Form.Wolf;

    /// <summary>
    /// Câmera em terceira pessoa. Segue o corpo pela mesma regra da
    /// física: entra já na transição para lobo e sai no instante em
    /// que a volta começa. Deixá-la presa até o fim da transição fazia
    /// o 1 parecer ter atraso.
    /// </summary>
    public bool IsThirdPerson => IsWolf;

    private Health _health;
    private double _shiftTimer;

    /// <summary>Vida guardada de cada corpo. O lobo e o humano são
    /// corpos distintos e cada um lembra o próprio estado.</summary>
    private float _humanHp;
    private float _wolfHp;

    public override void _Ready()
    {
        _health = GetParent().GetNode<Health>("Health");
        _health.SetPool(HumanHp, 0f, true);

        _humanHp = HumanHp;
        _wolfHp = 0f;

        _health.Respawned += OnRespawned;
        EmitSignal(SignalName.WpChanged, Wp, MaxWp);
    }

    public override void _Process(double delta)
    {
        // WP acumula jogando. A barra tem que estar sempre visível:
        // a decisão só existe se o jogador enxerga o recurso.
        AddWp(WpPerSecond * (float)delta);

        if (Current != Form.ShiftingToWolf && Current != Form.ShiftingToHuman)
            return;

        _shiftTimer -= delta;
        if (_shiftTimer > 0) return;

        if (Current == Form.ShiftingToWolf)
            Enter(Form.Wolf);
        else
            Enter(Form.Human);
    }

    /// <summary>Intenção do jogador. Um botão só, nos dois sentidos —
    /// controles que mudam por forma destroem a memória muscular.</summary>
    public void ToggleForm()
    {
        if (_health.IsDead) return;

        switch (Current)
        {
            case Form.Human:
                if (Breed == null)
                {
                    GD.PrintErr("FormController: nenhuma WolfBreed atribuída.");
                    return;
                }
                if (Wp < Breed.WpCost)
                {
                    EmitSignal(SignalName.TransformDenied, Breed.WpCost - Wp);
                    return;
                }

                Wp -= Breed.WpCost;
                EmitSignal(SignalName.WpChanged, Wp, MaxWp);
                Enter(Form.ShiftingToWolf);
                break;

            case Form.Wolf:
                Enter(Form.ShiftingToHuman);
                break;

            // Transição em andamento não aceita cancelamento.
            // Comprometer-se faz parte do custo.
            default:
                break;
        }
    }

    /// <summary>Renascer zera os dois corpos: morrer apaga o estado
    /// guardado, senão o lobo voltaria ferido da vida passada.</summary>
    private void OnRespawned()
    {
        _humanHp = HumanHp;
        _wolfHp = Breed != null ? Breed.Hp : 0f;
    }

    public void AddWp(float amount)
    {
        if (amount <= 0) return;

        float before = Wp;
        Wp = Mathf.Min(Wp + amount, MaxWp);

        if (!Mathf.IsEqualApprox(before, Wp))
            EmitSignal(SignalName.WpChanged, Wp, MaxWp);
    }

    public void OnDamageDealt(float amount) => AddWp(amount * WpPerDamageDealt);

    /// <summary>Uivo: recarrega escudo em troca de ficar parado e
    /// denunciar a posição pelo som. Só faz sentido na forma lobo.</summary>
    public bool CanHowl => Current == Form.Wolf;

    private bool KeepRatio => !RefillOnTransform;

    private void Enter(Form next)
    {
        // Antes de trocar, guarda a vida do corpo que está saindo.
        if (Current == Form.Human) _humanHp = _health.Hp;
        else if (Current == Form.Wolf) _wolfHp = _health.Hp;

        Current = next;

        switch (next)
        {
            case Form.ShiftingToWolf:
                _shiftTimer = ShiftDuration;
                _health.DamageMultiplier = ShiftDamageMultiplier;
                break;

            case Form.ShiftingToHuman:
                _shiftTimer = ShiftBackDuration;
                _health.DamageMultiplier = ShiftDamageMultiplier;
                break;

            case Form.Wolf:
                _health.DamageMultiplier = 1f;
                _health.ShieldDecay = Breed.ShieldDecay;

                // Dois corpos, duas vidas. O lobo volta como estava na
                // última vez — sair e entrar não é cura, senão
                // transformar vira botão de regeneração.
                _health.SetPool(Breed.Hp, Breed.Shield, false);
                _health.SetHp(_wolfHp > 0 ? _wolfHp : Breed.Hp);
                _health.RestoreShield(Breed.Shield);
                break;

            case Form.Human:
                _health.DamageMultiplier = 1f;
                _health.ShieldDecay = 0f;

                _health.SetPool(HumanHp, 0f, false);
                _health.SetHp(_humanHp > 0 ? _humanHp : HumanHp);
                break;
        }

        EmitSignal(SignalName.FormChanged, (int)next);
    }
}
