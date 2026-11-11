using Godot;

/// <summary>
/// Estatísticas de uma raça de lobo. É um Resource, então você cria
/// um arquivo .tres por classe e ajusta os números no Inspector sem
/// recompilar nada.
///
/// Regra de design (do documento): UM eixo de troca por classe.
/// Não dois, não três.
/// </summary>
[GlobalClass]
public partial class WolfBreed : Resource
{
    [Export] public string DisplayName = "Bruto";

    /// <summary>Custo em WP para assumir esta forma.</summary>
    [Export] public float WpCost = 250f;

    /// <summary>Vida da forma lobo (substitui a vida humana enquanto dura).</summary>
    [Export] public float Hp = 300f;

    /// <summary>Escudo recarregável pelo uivo. Separado do HP de propósito.</summary>
    [Export] public float Shield = 100f;

    /// <summary>Multiplicador de velocidade sobre a velocidade humana base.</summary>
    [Export] public float SpeedMultiplier = 1.15f;

    /// <summary>Multiplicador de altura do pulo.</summary>
    [Export] public float JumpMultiplier = 1.4f;

    [Export] public float AttackDamage = 120f;
    [Export] public float AttackRange = 2.5f;
    [Export] public float AttackCooldown = 0.8f;

    /// <summary>Bônus de dano ao acertar caindo de altura. O documento
    /// aponta o ataque aéreo como memória central do jogo.</summary>
    [Export] public float AirAttackBonus = 1.5f;
}
