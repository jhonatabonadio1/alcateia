using System;
using System.Numerics;

/// <summary>Onde um corpo estava num passo: o que a volta no tempo precisa.</summary>
public struct PoseSample
{
	public uint Tick;
	public Vector3 Position;
	public float Yaw, Pitch, Height;
	public bool Valid;
}

/// <summary>
/// O passado recente de um corpo, no servidor: um anel com os últimos
/// <see cref="NetRates.HistoryTicks"/> passos (pouco mais de um segundo).
///
/// É a base da volta no tempo do tiro: quando alguém atira, o servidor
/// pergunta onde cada corpo estava no instante que aparecia na tela de
/// quem atirou (<see cref="Sample"/>, com passo fracionário: entre dois
/// passos, interpola) e confere o acerto ali.
/// </summary>
public sealed class PoseHistory
{
	private readonly PoseSample[] _ring;

	public PoseHistory(int capacity = NetRates.HistoryTicks) => _ring = new PoseSample[capacity];

	public uint Newest { get; private set; }

	public void Record(uint tick, Vector3 position, float yaw, float pitch, float height)
	{
		_ring[tick % (uint)_ring.Length] = new PoseSample
		{
			Tick = tick, Position = position, Yaw = yaw, Pitch = pitch, Height = height, Valid = true
		};
		if (tick > Newest) Newest = tick;
	}

	/// <summary>Onde estava no passo <paramref name="tick"/> (pode ser
	/// fracionário). Falso se esse passo já saiu do anel ou ainda não existe.</summary>
	public bool Sample(double tick, out PoseSample pose)
	{
		pose = default;
		if (tick < 0 || tick > Newest) return false;

		uint a = (uint)Math.Floor(tick);
		uint b = Math.Min(a + 1, Newest);
		if (!Get(a, out PoseSample pa)) return false;
		if (!Get(b, out PoseSample pb)) pb = pa;

		float t = (float)(tick - a);
		pose = new PoseSample
		{
			Tick = a,
			Position = Vector3.Lerp(pa.Position, pb.Position, t),
			Yaw = LerpAngle(pa.Yaw, pb.Yaw, t),
			Pitch = pa.Pitch + (pb.Pitch - pa.Pitch) * t,
			Height = pa.Height + (pb.Height - pa.Height) * t,
			Valid = true
		};
		return true;
	}

	private bool Get(uint tick, out PoseSample pose)
	{
		pose = _ring[tick % (uint)_ring.Length];
		return pose.Valid && pose.Tick == tick;
	}

	public static float LerpAngle(float from, float to, float t)
	{
		float diff = (to - from) % MathF.Tau;
		if (diff > MathF.PI) diff -= MathF.Tau;
		if (diff < -MathF.PI) diff += MathF.Tau;
		return from + diff * t;
	}
}
