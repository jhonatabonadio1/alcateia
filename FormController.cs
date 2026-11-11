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
    [Export] public float ShiftDuration = 0.8f;

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

    /// <summary>Câmera em terceira pessoa. Vale também durante as duas
    /// transições: ver o próprio corpo mudar é o gesto de poder, e em
    /// primeira pessoa ele simplesmente não existe.</summary>
    public bool IsThirdPerson => Current != Form.Human;

    private Health _health;
    private double _shiftTimer;

    public override void _Ready()
    {
        _health = GetParent().GetNode<Health>("Health");
        _health.SetPool(HumanHp, 0f, true);
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
        Current = next;

        switch (next)
        {
            case Form.ShiftingToWolf:
            case Form.ShiftingToHuman:
                _shiftTimer = ShiftDuration;
                _health.DamageMultiplier = ShiftDamageMultiplier;
                break;

            case Form.Wolf:
                _health.DamageMultiplier = 1f;
                _health.SetPool(Breed.Hp, Breed.Shield, RefillOnTransform, KeepRatio);
                break;

            case Form.Human:
                _health.DamageMultiplier = 1f;
                _health.SetPool(HumanHp, 0f, RefillOnTransform, KeepRatio);
                break;
        }

        EmitSignal(SignalName.FormChanged, (int)next);
    }
}
