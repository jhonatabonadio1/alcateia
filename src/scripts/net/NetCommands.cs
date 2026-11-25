using Godot;

/// <summary>
/// A ponte entre a intenção do jogador (<see cref="CharacterIntent"/>,
/// do jogo) e o comando que vai pela rede (<see cref="InputCommand"/>,
/// do domínio), nos dois sentidos. O jogo e o servidor passam pela mesma
/// conversão — é o que faz os dois darem o mesmo passo.
/// </summary>
public static class NetCommands
{
	/// <summary>O comando de um passo do jogador local.</summary>
	public static InputCommand From(in CharacterIntent i, uint seq, float yaw, float pitch)
	{
		var b = InputButtons.None;
		if (i.Forward) b |= InputButtons.Forward;
		if (i.ForwardTap) b |= InputButtons.ForwardTap;
		if (i.Jump) b |= InputButtons.Jump;
		if (i.JumpHeld) b |= InputButtons.JumpHeld;
		if (i.CrouchHeld) b |= InputButtons.Crouch;
		if (i.WalkHeld) b |= InputButtons.Walk;
		if (i.Attack) b |= InputButtons.Attack;
		if (i.AttackHeld) b |= InputButtons.AttackHeld;
		if (i.AttackReleased) b |= InputButtons.AttackReleased;
		if (i.Special) b |= InputButtons.Special;
		if (i.SpecialReleased) b |= InputButtons.SpecialReleased;
		if (i.Pounce) b |= InputButtons.Pounce;
		if (i.AimHeld) b |= InputButtons.Aim;
		if (i.Howl) b |= InputButtons.Howl;
		if (i.HowlHeld) b |= InputButtons.HowlHeld;
		if (i.Drop) b |= InputButtons.Drop;

		return new InputCommand
		{
			Seq = seq,
			Buttons = b,
			MoveX = Quant.Axis(i.Move.X),
			MoveY = Quant.Axis(i.Move.Y),
			Yaw = Quant.Yaw(yaw),
			Pitch = Quant.Pitch(pitch),
			HasSlotRequest = i.Slot.HasValue,
			SlotRequest = (byte)(i.Slot ?? 0)
		};
	}

	/// <summary>Lugar na escalação de quem não tem um (o boneco ainda sem
	/// número, um alvo de teste): o servidor usa só onde o jogo o via.</summary>
	public const byte NoSeat = 255;

	/// <summary>
	/// O especial do lobo no comando (partida autoritativa): o ponto mirado do
	/// salto que saiu neste passo e o alvo travado (armado ou no voo), com onde
	/// o jogo o via — o que o voo usou. O servidor faz o mesmo salto com isto
	/// (<see cref="ServerBody"/>). <paramref name="seatOf"/> dá o lugar de um
	/// corpo na escalação (<see cref="NoSeat"/> se não tem).
	/// </summary>
	public static void Special(ref InputCommand c, Weapon weapon, System.Func<Node3D, byte> seatOf)
	{
		if (weapon == null) return;
		if (weapon.NetAimThisStep)
		{
			c.HasAim = true;
			c.AimNone = weapon.NetAim is not Vector3;
			if (weapon.NetAim is Vector3 aim) c.Aim = aim.ToNumerics();
		}

		if (weapon.NetFocus is Node3D focus)
		{
			var (at, velocity) = weapon.NetFocusView(focus);
			c.HasFocus = true;
			c.Focus = seatOf(focus);
			c.FocusAt = at.ToNumerics();
			c.FocusVelocity = velocity.ToNumerics();
		}
	}

	/// <summary>
	/// A intenção que o servidor simula a partir do comando. Nesta leva o
	/// servidor não troca de arma nem solta arma (a arma na mão e a forma
	/// vêm do jogo, no próprio comando) — só move o corpo.
	/// </summary>
	public static CharacterIntent ToIntent(in InputCommand c) => new()
	{
		Move = new Vector2(Quant.Axis(c.MoveX), Quant.Axis(c.MoveY)),
		Forward = c.Has(InputButtons.Forward),
		ForwardTap = c.Has(InputButtons.ForwardTap),
		Jump = c.Has(InputButtons.Jump),
		JumpHeld = c.Has(InputButtons.JumpHeld),
		CrouchHeld = c.Has(InputButtons.Crouch),
		WalkHeld = c.Has(InputButtons.Walk),
		Attack = c.Has(InputButtons.Attack),
		AttackHeld = c.Has(InputButtons.AttackHeld),
		AttackReleased = c.Has(InputButtons.AttackReleased),
		Special = c.Has(InputButtons.Special),
		SpecialReleased = c.Has(InputButtons.SpecialReleased),
		Pounce = c.Has(InputButtons.Pounce),
		AimHeld = c.Has(InputButtons.Aim),
		Howl = c.Has(InputButtons.Howl),
		HowlHeld = c.Has(InputButtons.HowlHeld)
	};

	/// <summary>A intenção com o eixo do movimento comprimido como vai no
	/// pacote (o jogo prevê com isto).</summary>
	public static CharacterIntent Quantize(in CharacterIntent i) => i with
	{
		Move = new Vector2(Quant.Axis(Quant.Axis(i.Move.X)), Quant.Axis(Quant.Axis(i.Move.Y)))
	};

	/// <summary>
	/// O pacote Input: <c>[tipo][n][n comandos, do mais novo ao mais velho]</c>.
	/// <paramref name="recent"/> é o anel dos últimos comandos (pelo número
	/// módulo o tamanho), <paramref name="seq"/> o mais novo.
	/// </summary>
	public static byte[] InputPacket(InputCommand[] recent, uint seq, byte[] buffer)
	{
		var w = new PacketWriter(buffer);
		w.Byte(NetWire.Input);
		int count = (int)System.Math.Min(seq, (uint)recent.Length);
		w.Byte((byte)count);
		for (int i = 0; i < count; i++) recent[(seq - (uint)i) % (uint)recent.Length].Write(ref w);
		return w.Written.ToArray();
	}

	/// <summary>Os comandos de um pacote Input, para a fila do corpo no servidor.</summary>
	public static void ReadInputPacket(byte[] packet, CommandBuffer into)
	{
		var r = new PacketReader(packet, 1);
		int count = System.Math.Min((int)r.Byte(), InputCommand.Redundancy * 2);
		for (int i = 0; i < count; i++) into.Push(InputCommand.Read(ref r));
	}

	public static Vector3 ToGodot(this System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
	public static System.Numerics.Vector3 ToNumerics(this Vector3 v) => new(v.X, v.Y, v.Z);
}
