using System;
using System.Numerics;

/// <summary>Os botões de um comando, um bit cada.</summary>
[Flags]
public enum InputButtons : ushort
{
	None = 0,
	Forward = 1 << 0,
	ForwardTap = 1 << 1,
	Jump = 1 << 2,
	JumpHeld = 1 << 3,
	Crouch = 1 << 4,
	Walk = 1 << 5,
	Attack = 1 << 6,
	AttackHeld = 1 << 7,
	AttackReleased = 1 << 8,
	Special = 1 << 9,
	SpecialReleased = 1 << 10,
	Aim = 1 << 11,
	Howl = 1 << 12,
	HowlHeld = 1 << 13,
	Drop = 1 << 14
}

/// <summary>
/// Um passo do jogador, como vai para o servidor: o que ele apertou
/// naquele passo de física e para onde olhava. É a única coisa que o
/// jogo manda sobre o próprio movimento — o servidor move o corpo a
/// partir disto, com o mesmo código, e não acredita em posição nenhuma.
///
/// Por enquanto (primeira leva, decisão 0011) o jogo ainda conta a forma,
/// a arma na mão, se está morto e onde renasceu (<see cref="Epoch"/> e
/// <see cref="Spawn"/>): a vida e a transformação vão para o servidor na
/// leva seguinte.
///
/// 17 bytes (26 com o renascimento). Cada pacote leva os últimos
/// <see cref="InputCommand.Redundancy"/> comandos: um pacote perdido não
/// perde o passo.
/// </summary>
public struct InputCommand
{
	/// <summary>Quantos comandos cada pacote leva (o deste passo e os anteriores).</summary>
	public const int Redundancy = 4;

	public const int MaxSize = 26;

	[Flags]
	private enum Flags : byte
	{
		None = 0,
		Dead = 1,
		HasSpawn = 2,
		HasSlot = 4,
		Frozen = 8
	}

	public uint Seq;
	public InputButtons Buttons;
	public sbyte MoveX, MoveY;
	public ushort Yaw;
	public short Pitch;

	/// <summary>Tecla de 1 a 5 apertada neste passo (o número do slot), se
	/// <see cref="HasSlotRequest"/>.</summary>
	public byte SlotRequest;
	public bool HasSlotRequest;

	/// <summary>A forma e a arma na mão, como o jogo as tem (primeira leva).</summary>
	public byte Form, HeldSlot;

	/// <summary>Quantas vezes renasceu (dá a volta em 256). Comando de outra
	/// vida não move o corpo desta.</summary>
	public byte Epoch;

	public bool Dead;

	/// <summary>O corpo ficou parado neste passo (contagem, fim de partida,
	/// menu aberto): o servidor segura o dele também.</summary>
	public bool Frozen;

	/// <summary>Onde nasceu nesta vida: vai nos primeiros comandos de cada
	/// vida, até o servidor confirmar.</summary>
	public bool HasSpawn;
	public Vector3 Spawn;

	public bool Has(InputButtons b) => (Buttons & b) != 0;

	public void Write(ref PacketWriter w)
	{
		var flags = (Flags)0;
		if (Dead) flags |= Flags.Dead;
		if (HasSpawn) flags |= Flags.HasSpawn;
		if (HasSlotRequest) flags |= Flags.HasSlot;
		if (Frozen) flags |= Flags.Frozen;

		w.U32(Seq);
		w.U16((ushort)Buttons);
		w.SByte(MoveX);
		w.SByte(MoveY);
		w.U16(Yaw);
		w.I16(Pitch);
		w.Byte((byte)flags);
		w.Byte(Form);
		w.Byte(HeldSlot);
		w.Byte(Epoch);
		w.Byte(SlotRequest);
		if (HasSpawn) w.Position(Spawn);
	}

	public static InputCommand Read(ref PacketReader r)
	{
		var c = new InputCommand
		{
			Seq = r.U32(),
			Buttons = (InputButtons)r.U16(),
			MoveX = r.SByte(),
			MoveY = r.SByte(),
			Yaw = r.U16(),
			Pitch = r.I16()
		};
		var flags = (Flags)r.Byte();
		c.Form = r.Byte();
		c.HeldSlot = r.Byte();
		c.Epoch = r.Byte();
		c.SlotRequest = r.Byte();
		c.Dead = (flags & Flags.Dead) != 0;
		c.HasSlotRequest = (flags & Flags.HasSlot) != 0;
		c.Frozen = (flags & Flags.Frozen) != 0;
		c.HasSpawn = (flags & Flags.HasSpawn) != 0;
		if (c.HasSpawn) c.Spawn = r.PositionCm();
		return c;
	}

	/// <summary>O mesmo comando, só que nada foi apertado neste passo: o
	/// que o servidor usa no lugar de um comando que não chegou (segura o
	/// que estava segurado, mas não repete o pulo nem o tiro).</summary>
	public InputCommand Held()
	{
		InputCommand c = this;
		c.Buttons &= InputButtons.Forward | InputButtons.JumpHeld | InputButtons.Crouch | InputButtons.Walk
			| InputButtons.AttackHeld | InputButtons.Aim | InputButtons.HowlHeld;
		c.HasSlotRequest = false;
		c.HasSpawn = false;
		return c;
	}
}
