using Godot;

/// <summary>
/// Os ruídos do corpo, publicados no <see cref="NoiseBus"/> — o mesmo
/// para o jogador e para o bot, que ouvem e são ouvidos pela mesma regra:
///
/// - **Passo** a cada passada (a distância entre um pé e o outro, do
///   <see cref="BodyNoiseProfile"/>), não por tempo: o som cai com o pé
///   da animação e a cadência segue a velocidade sozinha.
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
	private float _stride = 1f;

	/// <summary>Quantos passos o corpo já deu, e em que ponto da passada
	/// de agora ele está (0 logo depois da pisada, 1 na seguinte). Conta
	/// também quando o passo é mudo (agachado, Shift). É por aqui que o
	/// balanço da arma na mão cai junto com o som do pé.</summary>
	public int Steps { get; private set; }
	public float StridePhase => Mathf.Clamp(_walked / Mathf.Max(_stride, 0.01f), 0f, 1f);
	private double _howlTimer;

	public BodyNoise(Node3D source, BodyNoiseProfile profile)
	{
		_source = source;
		_profile = profile ?? new BodyNoiseProfile();
	}

	public void Update(double delta, bool grounded, Vector3 velocity, bool isWolf, bool howling, bool silent, bool sprinting = false)
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

		if (!grounded) return;

		float stride = !isWolf ? _profile.StrideHuman : sprinting ? _profile.StrideWolfSprint : _profile.StrideWolf;
		float speed = new Vector3(velocity.X, 0, velocity.Z).Length();
		_stride = stride;

		// Parado, o próximo pé cai depois de meia passada: quem sai do
		// lugar não espera um passo inteiro para fazer barulho.
		if (speed < StillSpeed)
		{
			_walked = stride * 0.5f;
			return;
		}

		_walked += speed * (float)delta;
		if (_walked < stride) return;
		_walked -= stride;
		Steps++;

		// O pé caiu de qualquer jeito; o ruído é que não sai de quem anda
		// em silêncio.
		if (silent) return;

		NoiseBus.Emit(_source.GlobalPosition, isWolf ? _profile.StepWolf : _profile.StepHuman, NoiseKind.Step, _source);
	}
}
