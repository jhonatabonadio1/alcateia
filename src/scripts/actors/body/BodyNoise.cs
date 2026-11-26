using Godot;

/// <summary>
/// Os ruídos do corpo, publicados no <see cref="NoiseBus"/> — o mesmo
/// para o jogador e para o bot, que ouvem e são ouvidos pela mesma regra:
///
/// - **Passo** a cada passada (a distância entre um pé e o outro, do
///   <see cref="BodyNoiseProfile"/> ou do clipe que o lobo está tocando),
///   não por tempo: o som cai com o pé da animação e a cadência segue a
///   velocidade sozinha. A exceção é a corrida de quatro, que toca a 1×:
///   ali o passo é a batida do clipe, quando as patas se juntam.
/// - **Baque** ao pousar.
/// - **Rugido** já no começo e a cada meio segundo enquanto dura.
///
/// Quem anda agachado não emite passo; o humano correndo (Shift) faz mais
/// barulho que andando. Os raios vêm do <see cref="BodyNoiseProfile"/>.
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
	private int _beats = -1;

	/// <summary>Quantos passos o corpo já deu, e em que ponto da passada
	/// de agora ele está (0 logo depois da pisada, 1 na seguinte). Conta
	/// também quando o passo é mudo (agachado). É por aqui que o
	/// balanço da arma na mão cai junto com o som do pé.</summary>
	public int Steps { get; private set; }
	public float StridePhase => Mathf.Clamp(_walked / Mathf.Max(_stride, 0.01f), 0f, 1f);

	/// <summary>A passada de agora, em metros (0 antes do primeiro passo).</summary>
	public float StrideLength => _stride;
	private double _howlTimer;

	public BodyNoise(Node3D source, BodyNoiseProfile profile)
	{
		_source = source;
		_profile = profile ?? new BodyNoiseProfile();
	}

	/// <param name="stride">Passada de quem sabe o clipe que toca (o
	/// lobo em pé); 0 usa a do perfil.</param>
	/// <param name="beats">Batidas contadas pela animação (a corrida de
	/// quatro: uma quando as patas se juntam); −1 conta pela distância.</param>
	/// <param name="sprinting">De lobo, a corrida de quatro; de humano, a
	/// corrida do Shift (outra passada e um passo mais alto).</param>
	public void Update(double delta, bool grounded, Vector3 velocity, bool isWolf, bool howling, bool silent,
		bool sprinting = false, float stride = 0f, int beats = -1)
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

		// O ritmo vem da animação: um passo a cada batida nova.
		if (beats >= 0)
		{
			if (_beats < 0 || beats < _beats) _beats = beats;
			while (_beats < beats)
			{
				_beats++;
				Steps++;
				if (!silent)
					NoiseBus.Emit(_source.GlobalPosition, isWolf ? _profile.StepWolf : _profile.StepHuman, NoiseKind.Step, _source);
			}
			return;
		}
		_beats = -1;

		if (stride <= 0f)
			stride = isWolf ? (sprinting ? _profile.StrideWolfSprint : _profile.StrideWolf)
				: sprinting ? _profile.StrideHumanSprint : _profile.StrideHuman;
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

		float radius = isWolf ? _profile.StepWolf : sprinting ? _profile.StepHumanSprint : _profile.StepHuman;
		NoiseBus.Emit(_source.GlobalPosition, radius, NoiseKind.Step, _source);
	}
}
