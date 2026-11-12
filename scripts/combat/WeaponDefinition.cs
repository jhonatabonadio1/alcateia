using Godot;

public enum WeaponClass
{
    Rifle,
    Smg,
    Shotgun,
    Sniper,
    Pistol,
    Utility
}

public enum FireMode
{
    /// <summary>Segurar dispara.</summary>
    Auto,

    /// <summary>Um clique, um tiro.</summary>
    Semi,

    /// <summary>Um clique, um tiro, e o ciclo da bomba antes do próximo.</summary>
    Pump,

    /// <summary>Um clique, um tiro, e o ciclo do ferrolho.</summary>
    Bolt
}

/// <summary>
/// Uma arma do arsenal, com os números da especificação (docs/ARSENAL.md).
///
/// Os valores de dano estão como no documento, calibrados para 100 HP.
/// O humano tem 200, então a <see cref="Arsenal.DamageScale"/> dobra o
/// dano na hora do tiro. Assim os "tiros para matar" e o TTK de cada
/// arma ficam exatamente os do documento, e quem ler um número aqui pode
/// conferi-lo lá sem fazer conta.
///
/// Recurso e não código: dá para afinar uma arma no Inspector do Godot
/// e o ajuste chega a todos os conjuntos sozinho, porque o custo de um
/// conjunto nunca é guardado — é sempre somado a partir daqui.
/// </summary>
[GlobalClass]
public partial class WeaponDefinition : Resource
{
    [ExportGroup("Identidade")]
    [Export] public string Id = "";
    [Export] public string DisplayName = "";
    [Export] public WeaponClass Class = WeaponClass.Rifle;
    [Export(PropertyHint.MultilineText)] public string Description = "";

    /// <summary>Custo em WP. Zero entra no conjunto padrão.</summary>
    [Export] public int WpCost;

    [ExportGroup("Dano")]
    [Export] public float Damage = 30f;
    [Export] public float HeadshotMultiplier = 2.5f;

    /// <summary>Projéteis por disparo. Mais de um é escopeta.</summary>
    [Export] public int Pellets = 1;

    /// <summary>Lentidão aplicada a um lobo atingido, em segundos.</summary>
    [Export] public float WolfStun;

    [ExportGroup("Cadência e munição")]
    [Export] public float Rpm = 650f;
    [Export] public FireMode Mode = FireMode.Auto;
    [Export] public int Magazine = 30;

    /// <summary>Reserva do documento. Só para exibição: a reserva é
    /// infinita no jogo, regra que vale desde o primeiro lote.</summary>
    [Export] public int Reserve = 90;

    [Export] public float ReloadTactical = 2.3f;
    [Export] public float ReloadEmpty = 2.9f;

    /// <summary>Recarga cartucho a cartucho, interrompível pelo tiro.</summary>
    [Export] public bool ShellByShell;
    [Export] public float ShellTime = 0.55f;
    [Export] public float ShellEnter = 0.3f;

    [Export] public float DrawTime = 0.5f;

    [ExportGroup("Dispersão (graus de meio-ângulo)")]
    [Export] public float SpreadBase = 0.35f;
    [Export] public float SpreadPerShot = 0.22f;
    [Export] public float SpreadMax = 4f;

    /// <summary>Graus por segundo de volta à base.</summary>
    [Export] public float SpreadRecovery = 9f;

    /// <summary>Espera depois do último tiro antes de recuperar.</summary>
    [Export] public float SpreadDelay = 0.12f;

    [Export] public float MoveSpreadMultiplier = 2.2f;
    [Export] public float CrouchSpreadMultiplier = 0.7f;

    [ExportGroup("Recuo (graus por tiro)")]
    [Export] public float RecoilVertical = 1f;

    /// <summary>Recuo depois de <see cref="RecoilEarlyShots"/> tiros seguidos.</summary>
    [Export] public float RecoilVerticalLate = 0.55f;
    [Export] public int RecoilEarlyShots = 4;
    [Export] public float RecoilHorizontal = 0.18f;

    /// <summary>Horizontal mais forte e imprevisível depois de N tiros
    /// (o AK). Zero desliga.</summary>
    [Export] public float RecoilHorizontalLate;
    [Export] public int RecoilHorizontalAfter;

    [Export] public float RecoilRecovery = 7f;

    [ExportGroup("Alcance")]
    [Export] public float Range = 150f;
    [Export] public float FalloffStart = 35f;
    [Export] public float FalloffEnd = 60f;

    /// <summary>Fração do dano que sobra a partir de <see cref="FalloffEnd"/>.</summary>
    [Export] public float FalloffMin = 0.7f;

    /// <summary>Velocidade de caminhada com a arma na mão (1 = base).</summary>
    [ExportGroup("Corpo")]
    [Export] public float SpeedMultiplier = 1f;

    /// <summary>Raio do estampido para a audição dos bots, em metros.</summary>
    [Export] public float NoiseRadius = 45f;

    [ExportGroup("Estrelas (0 a 5, alto é melhor)")]
    [Export] public float StarDamage;
    [Export] public float StarPenetration;
    [Export] public float StarAccuracy;
    [Export] public float StarRate;
    [Export] public float StarMobility;
    [Export] public float StarRecoil;
    [Export] public float StarRange;

    public float Interval => 60f / Mathf.Max(Rpm, 1f);

    public bool IsAutomatic => Mode == FireMode.Auto;

    /// <summary>Multiplicador de queda por distância.</summary>
    public float Falloff(float distance)
    {
        if (distance <= FalloffStart) return 1f;
        if (distance >= FalloffEnd) return FalloffMin;

        float t = (distance - FalloffStart) / Mathf.Max(FalloffEnd - FalloffStart, 0.01f);
        return Mathf.Lerp(1f, FalloffMin, t);
    }
}
