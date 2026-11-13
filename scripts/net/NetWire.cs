using Godot;
using System.IO;
using System.Text;

/// <summary>
/// O que passa entre o jogo e o servidor de partida, byte a byte.
///
/// Cada pacote começa com um byte dizendo o que é. O estado (onde estou,
/// para onde olho) vai 20 vezes por segundo, sem garantia de entrega —
/// perdeu um, o seguinte já vale. O resto (entrei, ataquei, acertei,
/// morri, o abate, o relógio) vai com garantia.
///
/// | Tipo | Quem manda | Conteúdo |
/// |---|---|---|
/// | Hello | jogo | chave da partida, conta, validade e ingresso |
/// | Welcome / Refused | servidor | o relógio, ou por que não entrou |
/// | State | jogo → todos | <see cref="NetState"/> |
/// | Attack | jogo → todos | o que tinha na mão e para onde mirava |
/// | Hit | jogo → vítima | dano, cabeça, causa, arma, de onde veio |
/// | Died | vítima | quem a matou e quem ajudou |
/// | Kill | servidor | o abate aceito e o placar |
/// | Clock | servidor | fase, contagem, tempo e placar |
/// | Gone | servidor | alguém saiu |
///
/// O que o servidor repassa de um jogador aos outros leva a conta do
/// autor logo depois do tipo.
/// </summary>
public static class NetWire
{
	public const byte Hello = 1, Welcome = 2, Refused = 3, State = 4, Attack = 5,
		Hit = 6, Died = 7, Kill = 8, Clock = 9, Gone = 10;

	/// <summary>Estados por segundo que cada jogo manda.</summary>
	public const int StateRate = 20;

	public static BinaryWriter Begin(byte type, out MemoryStream stream)
	{
		stream = new MemoryStream(64);
		var writer = new BinaryWriter(stream, Encoding.UTF8);
		writer.Write(type);
		return writer;
	}

	public static BinaryReader Open(byte[] packet) => new(new MemoryStream(packet), Encoding.UTF8);

	public static void Write(this BinaryWriter w, Vector3 v)
	{
		w.Write(v.X);
		w.Write(v.Y);
		w.Write(v.Z);
	}

	public static Vector3 ReadVector(this BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

	/// <summary>O pacote de um jogador, repassado aos outros: o tipo, a
	/// conta do autor e o resto como veio.</summary>
	public static byte[] Relay(byte[] packet, long from)
	{
		var relayed = new byte[packet.Length + 8];
		relayed[0] = packet[0];
		System.BitConverter.TryWriteBytes(new System.Span<byte>(relayed, 1, 8), from);
		System.Array.Copy(packet, 1, relayed, 9, packet.Length - 1);
		return relayed;
	}
}

/// <summary>Um instante de um jogador, como ele mesmo conta.</summary>
public struct NetState
{
	public Vector3 Position, Velocity;
	public float Yaw, Pitch;
	public byte Form, Slot;
	public bool Grounded, Dead;
	public float Hp, MaxHp, Shield, MaxShield;
	public string Primary, Secondary;

	public void Write(BinaryWriter w)
	{
		w.Write(Position);
		w.Write(Velocity);
		w.Write(Yaw);
		w.Write(Pitch);
		w.Write(Form);
		w.Write(Slot);
		w.Write((byte)((Grounded ? 1 : 0) | (Dead ? 2 : 0)));
		w.Write(Hp);
		w.Write(MaxHp);
		w.Write(Shield);
		w.Write(MaxShield);
		w.Write(Primary ?? "");
		w.Write(Secondary ?? "");
	}

	public static NetState Read(BinaryReader r)
	{
		var s = new NetState
		{
			Position = r.ReadVector(),
			Velocity = r.ReadVector(),
			Yaw = r.ReadSingle(),
			Pitch = r.ReadSingle(),
			Form = r.ReadByte(),
			Slot = r.ReadByte()
		};
		byte flags = r.ReadByte();
		s.Grounded = (flags & 1) != 0;
		s.Dead = (flags & 2) != 0;
		s.Hp = r.ReadSingle();
		s.MaxHp = r.ReadSingle();
		s.Shield = r.ReadSingle();
		s.MaxShield = r.ReadSingle();
		s.Primary = r.ReadString();
		s.Secondary = r.ReadString();
		return s;
	}
}
