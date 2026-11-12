using Godot;

/// <summary>
/// Um lote de armamento. O jogador escolhe um a cada vida.
///
/// O custo em WP amarra o lote à economia: lote bom custa o que se
/// ganhou matando, e morrer sem recurso devolve ao lote básico. É um
/// ciclo que premia desempenho sem dar vantagem permanente — quem joga
/// bem não fica mais forte para sempre, fica mais forte na próxima
/// vida.
///
/// Custo negativo concede WP: é assim que o lote padrão funciona.
/// Escolher o básico não é só economizar, é capitalizar para a
/// vida seguinte.
///
/// Cada arma traz os próprios números. Dano, alcance, cadência,
/// carregador, recarga e recuo juntos é o que faz um lote ser uma
/// escolha de estilo e não só um número maior.
/// </summary>
[GlobalClass]
public partial class Loadout : Resource
{
    [Export] public string DisplayName = "PADRÃO";

    /// <summary>WP gasto. Negativo concede em vez de cobrar.</summary>
    [Export] public int WpCost = -50;

    // --- Arma principal ---

    [Export] public string PrimaryName = "RIFLE";
    [Export] public float PrimaryDamage = 28f;
    [Export] public float PrimaryRange = 120f;
    [Export] public float PrimaryCooldown = 0.11f;

    /// <summary>Tiros por carregador. A reserva é infinita: o custo de
    /// atirar é o tempo de recarga, não o estoque. Contar balas no
    /// inventário só pune quem já está perdendo a luta.</summary>
    [Export] public int PrimaryMag = 40;

    [Export] public float PrimaryReload = 2f;

    /// <summary>Recuo vertical por tiro, em graus.</summary>
    [Export] public float PrimaryRecoil = 0.22f;

    // --- Arma secundária ---

    [Export] public string SecondaryName = "PISTOLA";
    [Export] public float SecondaryDamage = 65f;
    [Export] public float SecondaryRange = 150f;
    [Export] public float SecondaryCooldown = 0.5f;
    [Export] public int SecondaryMag = 12;
    [Export] public float SecondaryReload = 1.4f;
    [Export] public float SecondaryRecoil = 0.5f;

    [Export] public int Grenades = 2;
}
