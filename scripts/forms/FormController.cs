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

    [Export] public float MaxWp = 2500f;
    /// <summary>
    /// WP vem de abater e de renascer, não do relógio.
    ///
    /// Ganhar por tempo premia esperar; ganhar por abate premia jogar.
    /// O bônus por renascer existe para que morrer não deixe o
    /// jogador sem recurso nenhum — castigo que se acumula afunda
    /// quem já está perdendo.
    /// </summary>
    [Export] public float WpPerKill = 100f;

    /// <summary>Abate feito de lobo vale menos que de arma: o lobo já é
    /// gratuito e fica disponível a qualquer hora, e premiar igual faria
    /// dele a forma mais barata de enriquecer.</summary>
    [Export] public float WpPerWolfKill = 70f;

    [Export] public float WpPerAssist = 35f;


    /// <summary>Zero desde o arsenal: o documento paga abate, lobo e
    /// assistência, e só. Pagar por dano fazia spray em parede render WP.</summary>
    [Export] public float WpPerDamageDealt = 0f;

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



    public override void _Ready()
    {
        _health = GetParent().GetNode<Health>("Health");
        _health.SetPool(HumanHp, 0f, true);

        EmitSignal(SignalName.WpChanged, Wp, MaxWp);
    }

    public override void _Process(double delta)
    {
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

    /// <summary>
    /// Tenta gastar WP. Devolve falso se não houver o bastante.
    ///
    /// Custo negativo concede: é como o lote padrão devolve recurso a
    /// quem está sem nada, em vez de deixar o jogador preso no fundo.
    /// </summary>
    public bool TrySpend(int amount)
    {
        if (amount > 0 && Wp < amount) return false;

        Wp = Mathf.Clamp(Wp - amount, 0f, MaxWp);
        EmitSignal(SignalName.WpChanged, Wp, MaxWp);

        return true;
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

    public void OnKill(bool asWolf = false) => AddWp(asWolf ? WpPerWolfKill : WpPerKill);

    public void OnAssist() => AddWp(WpPerAssist);

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
                _health.BodyResistance = Breed.BodyResistance;

                // Vida é uma só. O que a forma lobo acrescenta é o
                // escudo, e é ele que define a forma — vida separada
                // faria de transformar um segundo tanque de vida.
                _health.SetPool(HumanHp, Breed.Shield, false);
                _health.RestoreShield(Breed.Shield);
                break;

            case Form.Human:
                _health.DamageMultiplier = 1f;
                _health.ShieldDecay = 0f;
                _health.BodyResistance = 1f;
                _health.SetPool(HumanHp, 0f, false);
                break;
        }

        EmitSignal(SignalName.FormChanged, (int)next);
    }
}
