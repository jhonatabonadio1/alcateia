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

			if (older.Epoch != newer.Epoch)
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
/// estima que passo é agora no servidor. Segue o estado que chega mais
/// cedo (o caminho mais rápido da rede) e anda devagar quando a rede
/// piora, para o desenho dos outros não dar trancos.
/// </summary>
public sealed class ServerClock
{
	private double _offset;
	private bool _known;

	public bool Known => _known;

	/// <summary>Chegou um estado do passo <paramref name="tick"/> quando o
	/// relógio daqui marcava <paramref name="localSeconds"/>.</summary>
	public void Observe(uint tick, double localSeconds)
	{
		double sample = tick * NetRates.TickSeconds - localSeconds;
		if (!_known)
		{
			_offset = sample;
			_known = true;
			return;
		}

		// Chegou mais cedo do que o esperado: a rede melhorou, adota já.
		// Mais tarde: vai se acostumando aos poucos.
		_offset = sample > _offset ? sample : _offset + (sample - _offset) * 0.05;
	}

	/// <summary>Segundos de servidor agora.</summary>
	public double Now(double localSeconds) => localSeconds + _offset;

	/// <summary>O instante em que os outros são desenhados.</summary>
	public double RenderTime(double localSeconds) => Now(localSeconds) - NetRates.InterpolationDelay;
}
