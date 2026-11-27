using System;
using System.Numerics;

/// <summary>Um corpo de outro jogador num instante do servidor.</summary>
public struct NetPose
{
	/// <summary>Em segundos de servidor (passo / 60).</summary>
	public double Time;
	public Vector3 Position, Velocity;
	public float Yaw, Pitch;
	public EntityFlags Flags;
	public byte Form, Slot, Epoch;
}

/// <summary>
/// Os últimos estados de um corpo de outro jogador, no jogo: ele é
/// desenhado <see cref="NetRates.InterpolationDelay"/> no passado, entre
/// dois estados que já chegaram — por isso ninguém teleporta, mesmo com
/// um pacote perdido aqui e ali. Faltando estado (rede travou), adivinha
/// à frente pela velocidade por até <see cref="NetRates.MaxExtrapolation"/>
/// e para.
///
/// Mudou de vida (renasceu) entre dois estados: não desliza do lugar da
/// morte até a base — pula.
/// </summary>
public sealed class InterpolationBuffer
{
	public const int Capacity = 32;

	private readonly NetPose[] _ring = new NetPose[Capacity];
	private int _count, _head;   // _head: o mais novo

	public int Count => _count;

	public NetPose Newest => _ring[_head];

	public void Add(in NetPose pose)
	{
		if (_count > 0 && pose.Time <= _ring[_head].Time) return;
		_head = (_head + 1) % Capacity;
		_ring[_head] = pose;
		if (_count < Capacity) _count++;
	}

	private NetPose At(int age) => _ring[((_head - age) % Capacity + Capacity) % Capacity];

	public bool Sample(double time, out NetPose pose)
	{
		pose = default;
		if (_count == 0) return false;

		NetPose newest = At(0);
		if (time >= newest.Time)
		{
			// À frente do que chegou: adivinha pela velocidade, até um limite.
			float ahead = (float)Math.Min(time - newest.Time, NetRates.MaxExtrapolation);
			pose = newest;
			if (!newest.Has(EntityFlags.Dead)) pose.Position += newest.Velocity * ahead;
			pose.Time = time;
			return true;
		}

		for (int age = 1; age < _count; age++)
		{
			NetPose older = At(age), newer = At(age - 1);
			if (time < older.Time) continue;

			// Outra vida, ou o corpo que o servidor deixou de contar (ou voltou a
			// contar: SightRules): não se desliza de um lugar ao outro.
			if (older.Epoch != newer.Epoch || older.Has(EntityFlags.Present) != newer.Has(EntityFlags.Present))
			{
				pose = newer;
				return true;
			}

			float t = (float)((time - older.Time) / (newer.Time - older.Time));
			pose = newer;
			pose.Time = time;
			pose.Position = Vector3.Lerp(older.Position, newer.Position, t);
			pose.Velocity = Vector3.Lerp(older.Velocity, newer.Velocity, t);
			pose.Yaw = PoseHistory.LerpAngle(older.Yaw, newer.Yaw, t);
			pose.Pitch = older.Pitch + (newer.Pitch - older.Pitch) * t;
			return true;
		}

		pose = At(_count - 1);   // mais velho que tudo: o mais velho que há
		return true;
	}

	public void Clear()
	{
		_count = 0;
	}
}

public static class NetPoseExtensions
{
	public static bool Has(this NetPose p, EntityFlags f) => (p.Flags & f) != 0;
}

/// <summary>
/// O relógio do servidor, visto do jogo: a partir dos estados que chegam,
/// estima que instante é agora no servidor e em que instante os outros são
/// desenhados.
///
/// O "agora" segue o estado que chega mais cedo (o caminho mais rápido da
/// rede) e se acostuma devagar quando a rede piora. O instante do desenho
/// (<see cref="RenderTime"/>) é outra coisa desde 26/11 (Jhon:
/// "microtravamentos, os personagens voltam no tempo e a tela fica
/// tremendo"): ele **anda junto com o relógio daqui e só muda de ritmo
/// devagar** — até <see cref="MaxRate"/> mais rápido ou mais devagar para
/// alcançar o alvo —, nunca pula nem volta. Antes era o "agora" menos 100
/// ms: um estado que chegava mais cedo puxava todo mundo para a frente de
/// uma vez, e o servidor engasgando deixava o desenho passar do último
/// estado, adivinhar à frente e voltar quando os atrasados chegavam.
///
/// - O atraso do desenho cresce com a irregularidade da rede (o jitter,
///   medido no quanto cada estado chega depois do caminho mais rápido), até
///   <see cref="MaxExtraDelay"/> a mais que os 100 ms de base.
/// - Perto do último estado que chegou, o desenho desacelera até parar, no
///   máximo <see cref="Overrun"/> além dele (o que se adivinha pela
///   velocidade), em vez de seguir adivinhando: num engasgo do servidor o
///   boneco vai mais devagar e para, e depois alcança, liso.
/// - O ritmo nunca muda de uma vez: no máximo <see cref="RateSlew"/> por
///   segundo (o boneco que corre não dá tranco quando o desenho freia ou
///   acelera).
/// - Longe demais do alvo (começo, rede que caiu por mais de um segundo),
///   vai direto (<see cref="SnapError"/>).
/// </summary>
public sealed class ServerClock
{
	/// <summary>O quanto o desenho pode andar mais rápido ou mais devagar que o relógio daqui.</summary>
	public const double MaxRate = 0.1;

	/// <summary>Quanto a irregularidade da rede pode somar ao atraso do desenho, em segundos.</summary>
	public const double MaxExtraDelay = 0.08;

	/// <summary>O quanto o desenho pode passar do último estado que chegou (adivinhando
	/// pela velocidade) enquanto freia, em segundos. Menos que
	/// <see cref="NetRates.MaxExtrapolation"/>: o boneco nunca chega a ficar parado no limite.</summary>
	public const double Overrun = 0.08;

	/// <summary>Mais longe que isto do alvo, o desenho vai direto (segundos).</summary>
	public const double SnapError = 1.0;

	/// <summary>O quanto o ritmo do desenho muda por segundo, no máximo (de 1 a 0 em
	/// 1/6 s; num quadro, 10%: um corredor a 3,5 m/s muda 0,35 m/s, o que não se vê).</summary>
	public const double RateSlew = 6.0;

	private double _offset, _late, _newest;
	private bool _known;

	private double _render, _renderLocal, _rate = 1;
	private bool _rendering;

	public bool Known => _known;

	/// <summary>A irregularidade da rede agora (segundos), alisada.</summary>
	public double Jitter => _late;

	/// <summary>Quanto atrás do "agora" do servidor os outros são desenhados.</summary>
	public double Delay => NetRates.InterpolationDelay + Math.Min(_late * 1.5, MaxExtraDelay);

	/// <summary>Chegou um estado do passo <paramref name="tick"/> quando o
	/// relógio daqui marcava <paramref name="localSeconds"/>.</summary>
	public void Observe(uint tick, double localSeconds) => Observe(tick * NetRates.TickSeconds, localSeconds);

	/// <summary>Chegou um estado do instante <paramref name="remoteSeconds"/>
	/// (no relógio de quem mandou) quando o daqui marcava <paramref name="localSeconds"/>.</summary>
	public void Observe(double remoteSeconds, double localSeconds)
	{
		if (remoteSeconds > _newest || !_known) _newest = remoteSeconds;
		double sample = remoteSeconds - localSeconds;
		if (!_known)
		{
			_offset = sample;
			_known = true;
			return;
		}

		// Chegou mais cedo do que o esperado: a rede melhorou, o "agora" adota já
		// (o desenho não pula: ele só anda um pouco mais rápido até lá).
		// Mais tarde: vai se acostumando aos poucos.
		_offset = sample > _offset ? sample : _offset + (sample - _offset) * 0.05;

		// O quanto este estado chegou depois do caminho mais rápido: sobe logo,
		// desce devagar (uns 2 s), para o atraso do desenho não ficar mudando.
		double late = _offset - sample;
		_late += (late - _late) * (late > _late ? 0.5 : 0.02);
	}

	/// <summary>Segundos de servidor agora.</summary>
	public double Now(double localSeconds) => localSeconds + _offset;

	/// <summary>O instante em que os outros são desenhados agora. Chamar mais de
	/// uma vez no mesmo instante daqui devolve o mesmo valor.</summary>
	public double RenderTime(double localSeconds)
	{
		double target = Now(localSeconds) - Delay;
		if (!_rendering || Math.Abs(target - _render) > SnapError)
		{
			_render = target;
			_renderLocal = localSeconds;
			_rate = 1;
			_rendering = true;
			return _render;
		}

		double dt = localSeconds - _renderLocal;
		if (dt <= 0) return _render;
		_renderLocal = localSeconds;

		double want = 1 + Math.Clamp((target - _render) * 2.0, -MaxRate, MaxRate);
		// Chegando no último estado que há: o mais rápido que ainda dá para parar
		// até Overrun além dele, freando a RateSlew (v² = 2·a·d). O pedaço que
		// passa do último, enquanto freia, o InterpolationBuffer adivinha.
		double room = _newest + Overrun - _render;
		want = Math.Min(want, Math.Sqrt(2 * RateSlew * Math.Max(0, room)));
		double slew = RateSlew * dt;
		_rate += Math.Clamp(want - _rate, -slew, slew);
		_render += dt * _rate;
		return _render;
	}
}
