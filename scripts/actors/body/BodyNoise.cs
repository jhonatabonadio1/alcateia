using Godot;

/// <summary>
/// Os ruídos do corpo, publicados no <see cref="NoiseBus"/> — o mesmo
/// para o jogador e para o bot, que ouvem e são ouvidos pela mesma regra:
///
/// - **Passo** por distância percorrida, não por tempo: a cadência segue
///   a velocidade sozinha, e andar devagar faz menos barulho.
/// - **Baque** ao pousar.
/// - **Rugido** já no começo e a cada meio segundo enquanto dura.
///
/// Quem anda em silêncio (agachado, ou com Shift) não emite passo. Os
/// raios vêm do <see cref="BodyNoiseProfile"/>.
/// </summary>
public sealed class BodyNoise
{
	/// <summary>Intervalo do ruído do rugido, em segundos.</summary>
	public const double HowlInterval = 0.5;

	/// <summary>Abaixo disto (m/s) o corpo conta como parado.</summary>
	public const float StillSpeed = 0.5f;

	private readonly Node3D _source;
	private readonly BodyNoiseProfile _profile;
	private bool _wasAirborne;
	private float _walked;
	private double _howlTimer;

	public BodyNoise(Node3D source, BodyNoiseProfile profile)
	{
		_source = source;
		_profile = profile ?? new BodyNoiseProfile();
	}

	public void Update(double delta, bool grounded, Vector3 velocity, bool isWolf, bool howling, bool silent)
	{
		if (grounded && _wasAirborne)
			NoiseBus.Emit(_source.GlobalPosition, _profile.Land, NoiseKind.Land, _source);
		_wasAirborne = !grounded;

		if (howling)
		{
			_howlTimer -= delta;
			if (_howlTimer <= 0)
			{
				_howlTimer = HowlInterval;
				NoiseBus.Emit(_source.GlobalPosition, _profile.Howl, NoiseKind.Howl, _source);
			}
		}
		else
		{
			_howlTimer = 0;
		}

		if (!grounded || silent) return;

		float speed = new Vector3(velocity.X, 0, velocity.Z).Length();
		if (speed < StillSpeed) return;

		_walked += speed * (float)delta;
		if (_walked < _profile.StepDistance) return;
		_walked = 0;

		NoiseBus.Emit(_source.GlobalPosition, isWolf ? _profile.StepWolf : _profile.StepHuman, NoiseKind.Step, _source);
	}
}
