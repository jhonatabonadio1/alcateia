using Godot;

/// <summary>
/// Mede a queda de um corpo (o jogador e o bot, o mesmo código): o ponto
/// mais alto desde que saiu do chão até pousar. Na parede, na escada e
/// preso ao chão o corpo conta como apoiado — a queda começa de onde ele
/// solta. Uma queda que passou por um golpe que voa (o bote, o especial) ou
/// pelo salto da parede não conta (Jhon, 26/11: "não deve tomar dano de
/// queda no ataque secundário e com a habilidade suprema").
/// </summary>
public sealed class FallTracker
{
	private bool _airborne;
	private bool _exempt;
	private float _peak;

	/// <summary>Um passo. Devolve a altura da queda no passo em que pousou
	/// (0 nos outros, e nas quedas isentas).</summary>
	/// <param name="supported">No chão, na parede ou na escada.</param>
	/// <param name="exempt">Neste passo o corpo está num golpe que voa ou
	/// saiu da parede num salto.</param>
	public float Step(bool supported, float y, bool exempt)
	{
		if (!supported)
		{
			if (!_airborne)
			{
				_airborne = true;
				_exempt = false;
				_peak = y;
			}
			_peak = Mathf.Max(_peak, y);
			_exempt |= exempt;
			return 0f;
		}

		if (!_airborne) return 0f;
		_airborne = false;
		bool counts = !_exempt && !exempt;
		_exempt = false;
		return counts ? Mathf.Max(_peak - y, 0f) : 0f;
	}

	/// <summary>
	/// O passo do corpo inteiro: mede e, se pousou de alto, fere — pela vida
	/// de quem caiu, como a própria granada (<see cref="Health.TakeOwnDamage"/>:
	/// a proteção de quem nasceu não vale, e se matar o feed diz "queda").
	/// O boneco de outro jogador não se fere aqui (a vida dele é da máquina
	/// dele). Devolve o dano.
	/// </summary>
	public float Update(Node3D body, Health health, FallProfile profile, bool wolf, bool supported, bool exempt)
	{
		if (health == null || health.IsDead)
		{
			Reset();
			return 0f;
		}
		float height = Step(supported, body.GlobalPosition.Y, exempt);
		if (height <= 0f || profile == null) return 0f;
		float damage = profile.Damage(height, wolf);
		LastHeight = height;
		if (damage > 0f) health.TakeOwnDamage(damage, body.GlobalPosition + Vector3.Down, KillCause.Fall, "");
		return damage;
	}

	/// <summary>A altura da última queda que contou (para os testes).</summary>
	public float LastHeight { get; private set; }

	/// <summary>Esquece a queda em curso (renasceu, foi levado para outro lugar).</summary>
	public void Reset()
	{
		_airborne = false;
		_exempt = false;
	}
}
