using Godot;
using System.Collections.Generic;

/// <summary>
/// A faca: saída silenciosa e último recurso. Não faz ruído para os bots.
/// </summary>
public partial class Weapon
{
	[ExportGroup("Faca")]
	[Export] public float KnifeDamage = 85f;

	/// <summary>Velocidade com a faca na mão, sobre a do humano. 3,36 m/s
	/// desde a v0.27.1, 12% acima dos 3,0 de antes (decisão do Jhon): como o
	/// humano já anda 8% a mais, a faca soma 1,12 / 1,08.</summary>
	[Export] public float KnifeSpeedMultiplier = 1.037037f;
	[Export] public float KnifeRange = 2.2f;
	[Export] public float KnifeCooldown = 0.55f;

	/// <summary>
	/// Facada. Não faz ruído de tiro: é a arma de quem está se movendo
	/// em silêncio e não quer entregar a posição por um abate.
	/// </summary>
	private void Knife()
	{
		_cooldown = KnifeCooldown;
		EmitSignal(SignalName.Fired);

		Vector3 from = _head.GlobalPosition;
		Vector3 to = from - _head.GlobalTransform.Basis.Z * KnifeRange;

		Health hit = Raycast(from, to, out Vector3 impact, out Node collider);
		if (hit == null) return;

		bool headshot = IsHeadshot(collider, impact);
		float damage = KnifeDamage * (headshot ? HeadshotMultiplier : 1f);

		Damage(hit, damage, from, headshot);
	}
}
