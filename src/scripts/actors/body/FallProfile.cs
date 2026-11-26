using Godot;

/// <summary>
/// O dano de queda, um arquivo só para o jogador e para o bot
/// (resources/body/queda.tres), como o ruído (<see cref="BodyNoiseProfile"/>):
/// os dois caem pela mesma regra.
///
/// Pedido do Jhon (26/11): "humano começar a tomar dano de queda, lobo
/// também, mas menos. Não deve tomar dano de queda no ataque secundário (ao
/// pular e atacar) e com a habilidade suprema." A conta é
/// <see cref="CombatRules.FallDamage"/>; quem mede a queda é o
/// <see cref="FallTracker"/>.
/// </summary>
[GlobalClass]
public partial class FallProfile : Resource
{
	/// <summary>Humano: até esta altura (m) a queda não fere. 4 m é mais que
	/// um contêiner (2,9 m): descer de um não dói, de dois empilhados (5,8 m)
	/// sim.</summary>
	[Export] public float HumanSafe = 4f;

	/// <summary>Humano: dano por metro além do seguro. 25: 200 (a vida
	/// inteira) a 12 m.</summary>
	[Export] public float HumanPerMeter = 25f;

	/// <summary>Lobo: começa mais alto (as pernas aguentam o pulo dele e o
	/// salto da parede sem contar)...</summary>
	[Export] public float WolfSafe = 7f;

	/// <summary>...e fere metade do humano por metro: 12,5. O lobo de vida e
	/// escudo cheios (300) só morre de queda a 31 m.</summary>
	[Export] public float WolfPerMeter = 12.5f;

	public const string Path = "res://src/resources/body/queda.tres";

	/// <summary>O arquivo do jogo, ou os números daqui se ele faltar.</summary>
	public static FallProfile Load() =>
		ResourceLoader.Exists(Path) ? GD.Load<FallProfile>(Path) : new FallProfile();

	public float Damage(float height, bool wolf) =>
		CombatRules.FallDamage(height, wolf ? WolfSafe : HumanSafe, wolf ? WolfPerMeter : HumanPerMeter);
}
