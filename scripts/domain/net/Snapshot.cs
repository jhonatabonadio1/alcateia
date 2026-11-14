using System;
using System.Numerics;

[Flags]
public enum EntityFlags : byte
{
	None = 0,
	Present = 1,
	Grounded = 2,
	Crouching = 4,
	Dead = 8,
	WallRunning = 16,
	Sprinting = 32,
	Aiming = 64
}

/// <summary>Um corpo no estado do mundo que o servidor manda: 24 bytes.</summary>
public struct EntityState
{
	public const int Size = 24;

	/// <summary>O lugar do corpo na escalação (o Welcome diz de quem é cada
	/// número): 1 byte em vez dos 8 da conta.</summary>
	public byte Index;
	public EntityFlags Flags;
	public byte Form, Slot, Epoch;
	public Vector3 Position, Velocity;
	public ushort Yaw;
	public short Pitch;

	public bool Has(EntityFlags f) => (Flags & f) != 0;

	public void Write(ref PacketWriter w)
	{
		w.Byte(Index);
		w.Byte((byte)Flags);
		w.Byte(Form);
		w.Byte(Slot);
		w.Byte(Epoch);
		w.Position(Position);
		w.Velocity(Velocity);
		w.U16(Yaw);
		w.I16(Pitch);
	}

	public static EntityState Read(ref PacketReader r) => new()
	{
		Index = r.Byte(),
		Flags = (EntityFlags)r.Byte(),
		Form = r.Byte(),
		Slot = r.Byte(),
		Epoch = r.Byte(),
		Position = r.PositionCm(),
		Velocity = r.VelocityCm(),
		Yaw = r.U16(),
		Pitch = r.I16()
	};
}

/// <summary>
/// O estado do mundo que o servidor manda a cada dois passos:
///
/// <c>[tipo][passo u32][último comando seu simulado u32][n u8][n × EntityState]</c>
///
/// O corpo de cada estado é o mesmo para todos; só o "último comando seu"
/// muda de um jogador para outro. Por isso o corpo é escrito uma vez
/// (<see cref="WriteBody"/>) e cada jogador recebe o cabeçalho dele mais
/// a cópia (<see cref="Compose"/>).
/// </summary>
public static class SnapshotWire
{
	public const int HeaderSize = 10;

	public static int WriteBody(Span<byte> buffer, ReadOnlySpan<EntityState> entities)
	{
		var w = new PacketWriter(buffer);
		w.Byte((byte)entities.Length);
		foreach (EntityState e in entities) e.Write(ref w);
		return w.Length;
	}

	/// <summary>O pacote de um jogador: cabeçalho dele + o corpo comum.</summary>
	public static int Compose(Span<byte> packet, byte type, uint tick, uint ack, ReadOnlySpan<byte> body)
	{
		var w = new PacketWriter(packet);
		w.Byte(type);
		w.U32(tick);
		w.U32(ack);
		w.Bytes(body);
		return w.Length;
	}

	/// <summary>Lê o cabeçalho (depois do tipo) e devolve quantos corpos vêm.</summary>
	public static int ReadHeader(ref PacketReader r, out uint tick, out uint ack)
	{
		tick = r.U32();
		ack = r.U32();
		return r.Byte();
	}
}
