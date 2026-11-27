using Godot;

/// <summary>
/// As estatísticas do lobo. Um só desde 26/11 (decisão do Jhon: "remover
/// as classes de lobo, deixar apenas melhorias"): src/resources/breeds/lobo.tres,
/// no meio do Bruto e do Ligeiro de antes, canhão de vidro (escudo 100, o
/// corpo leva o dano inteiro). As melhorias de lobo (Catalog.cs) mexem
/// numa cópia (<see cref="Profile.BuildBreed"/>). É um Resource: os números
/// se ajustam no .tres, sem recompilar.
/// </summary>
[GlobalClass]
public partial class WolfBreed : Resource
{
	[Export] public string DisplayName = "Bruto";

	/// <summary>Custo em WP para assumir esta forma.</summary>
	[Export] public float WpCost = 250f;

	/// <summary>Escudo do lobo, recarregado pelo rugido. Separado do
	/// HP de propósito: é o recurso que define a forma.</summary>
	[Export] public float Shield = 200f;

	/// <summary>Quanto de escudo escorre por segundo. É o que dá
	/// urgência ao rugido — o escudo não espera, ele vaza.</summary>
	[Export] public float ShieldDecay = 9f;

	/// <summary>Multiplicador de velocidade sobre a velocidade humana base.</summary>
	[Export] public float SpeedMultiplier = 1.15f;

	/// <summary>Multiplicador de altura do pulo.</summary>
	[Export] public float JumpMultiplier = 1.4f;

	/// <summary>
	/// Multiplicador do dano recebido NO CORPO. Abaixo de 1 o lobo é
	/// mais resistente — é o que faz dele uma ameaça de frente, e não
	/// só um corpo rápido.
	///
	/// A cabeça não entra: headshot atravessa a resistência inteira.
	/// Sem essa saída, o humano com rifle não teria resposta nenhuma
	/// contra um lobo vindo de frente.
	/// </summary>
	[Export] public float BodyResistance = 0.65f;

	[Export] public float AttackDamage = 120f;
	[Export] public float AttackRange = 2.5f;

	/// <summary>Bônus de dano ao acertar caindo de altura. O documento
	/// aponta o ataque aéreo como memória central do jogo.</summary>
	[Export] public float AirAttackBonus = 1.5f;
}
