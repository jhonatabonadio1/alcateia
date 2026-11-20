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
	/// <summary>Tempo entre dois golpes. 0,40 s desde 19/11 (era 0,55: "o
	/// ataque da faca precisa responder clique a clique, sendo um pouco
	/// mais rápido"). O clique dado no meio de um golpe não se perde: fica
	/// guardado e sai quando o golpe acaba (<see cref="TryAttack"/>).</summary>
	[Export] public float KnifeCooldown = 0.40f;

	/// <summary>
	/// Facada. Não faz ruído de tiro: é a arma de quem está se movendo
	/// em silêncio e não quer entregar a posição por um abate.
	/// </summary>
	private void Knife()
	{
		// A versão da faca muda um pouco o dano, o ritmo e o alcance (Arsenal.KnifeStats).
		var knife = Arsenal.KnifeStats(MeleeId);
		_cooldown = KnifeCooldown * knife.Cooldown;
		EmitSignal(SignalName.Fired);

		Vector3 from = _head.GlobalPosition;
		Vector3 to = from - _head.GlobalTransform.Basis.Z * (KnifeRange * knife.Range);

		Health hit = Trace(from, to, Heads(), out _, out _, out bool headshot);
		if (hit == null) return;

		float damage = KnifeDamage * knife.Damage * (headshot ? HeadshotMultiplier : 1f);

		Damage(hit, damage, from, headshot);
	}
}
