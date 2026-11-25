using System;
using System.Buffers.Binary;
using System.Numerics;

/// <summary>
/// Escreve um pacote num buffer que já existe, sem alocar nada: o
/// servidor monta milhares de pacotes por segundo, e cada alocação vira
/// trabalho para o coletor de lixo — que é pausa no meio da partida.
///
/// Inteiros em little-endian. Posição em centímetros em 3 bytes por eixo
/// (±83 km), velocidade em cm/s em 2 bytes (±327 m/s), ângulos em 2 bytes
/// (yaw: volta inteira em 65536 passos, 0,0055°; pitch: ±90° em ±32767).
/// </summary>
public ref struct PacketWriter
{
	private readonly Span<byte> _buffer;
	public int Length { get; private set; }

	public PacketWriter(Span<byte> buffer)
	{
		_buffer = buffer;
		Length = 0;
	}

	public ReadOnlySpan<byte> Written => _buffer[..Length];

	public void Byte(byte v) => _buffer[Length++] = v;
	public void SByte(sbyte v) => _buffer[Length++] = (byte)v;
	public void Bool(bool v) => _buffer[Length++] = (byte)(v ? 1 : 0);

	public void U16(ushort v)
	{
		BinaryPrimitives.WriteUInt16LittleEndian(_buffer[Length..], v);
		Length += 2;
	}

	public void I16(short v)
	{
		BinaryPrimitives.WriteInt16LittleEndian(_buffer[Length..], v);
		Length += 2;
	}

	public void U32(uint v)
	{
		BinaryPrimitives.WriteUInt32LittleEndian(_buffer[Length..], v);
		Length += 4;
	}

	public void I64(long v)
	{
		BinaryPrimitives.WriteInt64LittleEndian(_buffer[Length..], v);
		Length += 8;
	}

	public void F32(float v)
	{
		BinaryPrimitives.WriteSingleLittleEndian(_buffer[Length..], v);
		Length += 4;
	}

	/// <summary>Inteiro com sinal em 3 bytes.</summary>
	public void I24(int v)
	{
		v = Math.Clamp(v, -8_388_608, 8_388_607);
		_buffer[Length++] = (byte)v;
		_buffer[Length++] = (byte)(v >> 8);
		_buffer[Length++] = (byte)(v >> 16);
	}

	/// <summary>Posição: centímetro por eixo, 9 bytes.</summary>
	public void Position(Vector3 p)
	{
		I24(Quant.Cm(p.X));
		I24(Quant.Cm(p.Y));
		I24(Quant.Cm(p.Z));
	}

	/// <summary>Velocidade: cm/s por eixo, 6 bytes.</summary>
	public void Velocity(Vector3 v)
	{
		I16(Quant.CmPerSecond(v.X));
		I16(Quant.CmPerSecond(v.Y));
		I16(Quant.CmPerSecond(v.Z));
	}

	public void Bytes(ReadOnlySpan<byte> bytes)
	{
		bytes.CopyTo(_buffer[Length..]);
		Length += bytes.Length;
	}
}

/// <summary>Lê o que o <see cref="PacketWriter"/> escreveu. Pacote curto
/// demais estoura <see cref="IndexOutOfRangeException"/> — quem chama
/// trata como pacote torto.</summary>
public ref struct PacketReader
{
	private readonly ReadOnlySpan<byte> _buffer;
	public int Position { get; private set; }

	public PacketReader(ReadOnlySpan<byte> buffer, int start = 0)
	{
		_buffer = buffer;
		Position = start;
	}

	public int Left => _buffer.Length - Position;

	public byte Byte() => _buffer[Position++];
	public sbyte SByte() => (sbyte)_buffer[Position++];
	public bool Bool() => _buffer[Position++] != 0;

	public ushort U16()
	{
		ushort v = BinaryPrimitives.ReadUInt16LittleEndian(_buffer[Position..]);
		Position += 2;
		return v;
	}

	public short I16()
	{
		short v = BinaryPrimitives.ReadInt16LittleEndian(_buffer[Position..]);
		Position += 2;
		return v;
	}

	public uint U32()
	{
		uint v = BinaryPrimitives.ReadUInt32LittleEndian(_buffer[Position..]);
		Position += 4;
		return v;
	}

	public long I64()
	{
		long v = BinaryPrimitives.ReadInt64LittleEndian(_buffer[Position..]);
		Position += 8;
		return v;
	}

	public float F32()
	{
		float v = BinaryPrimitives.ReadSingleLittleEndian(_buffer[Position..]);
		Position += 4;
		return v;
	}

	public int I24()
	{
		int v = _buffer[Position] | (_buffer[Position + 1] << 8) | (_buffer[Position + 2] << 16);
		Position += 3;
		return (v << 8) >> 8;   // estende o sinal
	}

	public Vector3 PositionCm() => new(I24() / 100f, I24() / 100f, I24() / 100f);

	public Vector3 VelocityCm() => new(I16() / 100f, I16() / 100f, I16() / 100f);
}

/// <summary>As contas de compressão: quanto de precisão cada coisa leva.</summary>
public static class Quant
{
	public static int Cm(float meters) => (int)MathF.Round(meters * 100f);

	public static short CmPerSecond(float metersPerSecond) =>
		(short)Math.Clamp((int)MathF.Round(metersPerSecond * 100f), short.MinValue, short.MaxValue);

	/// <summary>Yaw em radianos (qualquer volta) → 0..65535.</summary>
	public static ushort Yaw(float radians)
	{
		float turns = radians / MathF.Tau;
		turns -= MathF.Floor(turns);
		return (ushort)((int)MathF.Round(turns * 65536f) & 0xFFFF);
	}

	/// <summary>0..65535 → yaw em radianos, de −π a π.</summary>
	public static float Yaw(ushort q)
	{
		float r = q / 65536f * MathF.Tau;
		return r > MathF.PI ? r - MathF.Tau : r;
	}

	/// <summary>Pitch em radianos (−π/2..π/2) → ±32767.</summary>
	public static short Pitch(float radians) =>
		(short)Math.Clamp((int)MathF.Round(radians / (MathF.PI / 2f) * 32767f), -32767, 32767);

	public static float Pitch(short q) => q / 32767f * (MathF.PI / 2f);

	/// <summary>Um ponto como chega pelo pacote (centímetro por eixo): quem
	/// prevê usa o mesmo número que o servidor vai ler.</summary>
	public static float Position(float meters) => Cm(meters) / 100f;

	/// <summary>Uma velocidade como chega pelo pacote (cm/s por eixo).</summary>
	public static float Velocity(float metersPerSecond) => CmPerSecond(metersPerSecond) / 100f;

	/// <summary>Eixo do movimento (−1..1) → −127..127.</summary>
	public static sbyte Axis(float v) => (sbyte)Math.Clamp((int)MathF.Round(v * 127f), -127, 127);

	public static float Axis(sbyte q) => q / 127f;
}
