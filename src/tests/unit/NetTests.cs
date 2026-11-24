using System;
using System.Numerics;

namespace Alcateia.UnitTests;

/// <summary>O núcleo da rede autoritativa (scripts/domain/net, decisão 0011).</summary>
public static class NetTests
{
	private static InputCommand Cmd(uint seq, InputButtons buttons = InputButtons.None) =>
		new() { Seq = seq, Buttons = buttons, MoveY = -127, Yaw = 1000 };

	[Test("comprime posição em cm, ângulos em 2 bytes e devolve quase o mesmo")]
	public static void QuantizationRoundTrip()
	{
		Span<byte> buffer = stackalloc byte[64];
		var w = new PacketWriter(buffer);
		w.Position(new Vector3(-123.456f, 7.891f, 4321.5f));
		w.Velocity(new Vector3(6.54f, -12.3f, 0.004f));
		w.I24(-5);
		var r = new PacketReader(w.Written);
		Vector3 p = r.PositionCm(), v = r.VelocityCm();
		Check.Near(-123.46, p.X, 0.0051, "x em cm");
		Check.Near(7.89, p.Y, 0.0051, "y em cm");
		Check.Near(4321.5, p.Z, 0.0051, "z em cm (longe da origem)");
		Check.Near(-12.3, v.Y, 0.0051, "velocidade em cm/s");
		Check.Equal(-5, r.I24(), "inteiro negativo em 3 bytes");
		Check.Equal(9 + 6 + 3, w.Length, "posição 9 bytes, velocidade 6");

		foreach (float yaw in new[] { 0f, 1f, -1f, 3.1f, -3.1f, 7f })
		{
			float back = Quant.Yaw(Quant.Yaw(yaw));
			float diff = MathF.Abs(MathF.IEEERemainder(back - yaw, MathF.Tau));
			Check.True(diff < 0.0002f, $"yaw {yaw} volta igual (diferença {diff})");
		}
		Check.Near(1.2, Quant.Pitch(Quant.Pitch(1.2f)), 0.0001, "pitch");
		Check.Near(-1, Quant.Axis(Quant.Axis(-1f)), 0.0001, "eixo do movimento");
	}

	[Test("comando de entrada: 17 bytes, 26 com o renascimento, e volta igual")]
	public static void InputCommandRoundTrip()
	{
		Span<byte> buffer = stackalloc byte[64];
		var c = new InputCommand
		{
			Seq = 123456, Buttons = InputButtons.Jump | InputButtons.Aim, MoveX = 12, MoveY = -127,
			Yaw = 40000, Pitch = -2000, Form = 3, HeldSlot = 1, Epoch = 9, Dead = true,
			HasSlotRequest = true, SlotRequest = 2, Frozen = true
		};
		var w = new PacketWriter(buffer);
		c.Write(ref w);
		Check.Equal(17, w.Length, "sem renascimento");

		var r = new PacketReader(w.Written);
		InputCommand back = InputCommand.Read(ref r);
		Check.Equal(c.Seq, back.Seq, "número");
		Check.Equal(c.Buttons, back.Buttons, "botões");
		Check.True(back.Dead && back.Frozen && back.HasSlotRequest && back.SlotRequest == 2 && back.Epoch == 9, "marcas");

		c.HasSpawn = true;
		c.Spawn = new Vector3(10, 2, -30);
		w = new PacketWriter(buffer);
		c.Write(ref w);
		Check.Equal(InputCommand.MaxSize, w.Length, "com renascimento");
		r = new PacketReader(w.Written);
		Check.Near(-30, InputCommand.Read(ref r).Spawn.Z, 0.01, "onde renasceu");
	}

	[Test("comando repetido (não chegou) segura o que estava segurado, sem repetir pulo nem tiro")]
	public static void HeldCommandDropsPresses()
	{
		InputCommand c = Cmd(5, InputButtons.Jump | InputButtons.JumpHeld | InputButtons.Attack | InputButtons.AttackHeld | InputButtons.Forward);
		c.HasSlotRequest = true;
		InputCommand held = c.Held();
		Check.True(held.Has(InputButtons.JumpHeld) && held.Has(InputButtons.AttackHeld) && held.Has(InputButtons.Forward), "segurados ficam");
		Check.False(held.Has(InputButtons.Jump) || held.Has(InputButtons.Attack) || held.HasSlotRequest, "apertos não repetem");
	}

	[Test("fila de comandos: começa com folga, consome na ordem, ignora repetidos e velhos")]
	public static void CommandBufferInOrder()
	{
		var b = new CommandBuffer();
		b.Push(Cmd(1));
		Check.False(b.Next(out _, out _), "com um só, ainda não começa");
		b.Push(Cmd(2));
		b.Push(Cmd(1));   // repetido
		b.Push(Cmd(3));

		Check.True(b.Next(out InputCommand c, out bool rep) && c.Seq == 1 && !rep, "primeiro");
		Check.True(b.Next(out c, out _) && c.Seq == 2, "segundo");
		b.Push(Cmd(2));   // velho
		Check.True(b.Next(out c, out _) && c.Seq == 3, "terceiro");
		Check.Equal(3u, b.LastProcessed, "último simulado");
	}

	[Test("fila de comandos: faltou, o corpo espera sem dar passo; o que se perdeu sai com o último segurado")]
	public static void CommandBufferGaps()
	{
		var b = new CommandBuffer();
		b.Push(Cmd(1, InputButtons.Jump | InputButtons.Forward));
		b.Push(Cmd(2, InputButtons.Forward));
		b.Next(out _, out _);
		b.Next(out _, out _);

		// Um passo a mais aqui (repetir o último) deixava o corpo do servidor
		// um passo à frente do jogo a cada pacote atrasado.
		Check.False(b.Next(out _, out _), "faltou: espera, sem passo");
		Check.Equal(2u, b.LastProcessed, "esperar não avança o número");
		Check.Equal(1, b.Starved, "conta a espera");

		b.Push(Cmd(3, InputButtons.Forward));
		Check.True(b.Next(out InputCommand c, out bool rep) && c.Seq == 3 && !rep, "o que faltou chegou: vale");

		// 4 e 5 nunca chegam; 6 e 7 chegam.
		b.Push(Cmd(6));
		b.Push(Cmd(7, InputButtons.Jump | InputButtons.Forward));
		int waits = 0;
		while (!b.Next(out c, out rep)) waits++;
		Check.Equal(CommandBuffer.GapPatience, waits, "espera um pouco antes de dar o buraco por perdido");
		Check.True(rep && c.Seq == 4 && c.Has(InputButtons.Forward) && !c.Has(InputButtons.Jump), "o perdido sai com o último segurado, no número dele");
		Check.True(b.Next(out c, out rep) && rep && c.Seq == 5, "o resto do buraco sai junto, sem esperar de novo");
		Check.True(b.Next(out c, out rep) && !rep && c.Seq == 6, "e segue do próximo que chegou");
		Check.Equal(2, b.Skipped, "conta os perdidos");

		b.Push(Cmd(4));
		b.Push(Cmd(5));
		Check.True(b.Next(out c, out _) && c.Seq == 7, "o perdido que chega tarde não vale mais (o passo dele já foi dado)");
	}

	[Test("fila de comandos: funda demais, consome dois por passo até voltar")]
	public static void CommandBufferCatchUp()
	{
		var b = new CommandBuffer();
		for (uint s = 1; s <= 10; s++) b.Push(Cmd(s));
		Check.Equal(2, b.WantedThisTick, "dez na fila: dois por passo");
		for (int i = 0; i < 8; i++) b.Next(out _, out _);
		Check.Equal(1, b.WantedThisTick, "voltou ao normal: um por passo");
	}

	[Test("estado do mundo: 24 bytes por corpo, corpo comum e cabeçalho de cada um")]
	public static void SnapshotRoundTrip()
	{
		var entities = new[]
		{
			new EntityState { Index = 0, Flags = EntityFlags.Present | EntityFlags.Grounded, Position = new Vector3(1, 2, 3), Yaw = 100 },
			new EntityState { Index = 3, Flags = EntityFlags.Present | EntityFlags.Dead, Epoch = 7, Velocity = new Vector3(0, -9.8f, 0) }
		};
		byte[] body = new byte[256], packet = new byte[256];
		int bodyLength = SnapshotWire.WriteBody(body, entities);
		Check.Equal(1 + 2 * EntityState.Size, bodyLength, "tamanho do corpo");

		int length = SnapshotWire.Compose(packet, 18, 600, 42, body.AsSpan(0, bodyLength));
		var r = new PacketReader(packet.AsSpan(0, length), 1);
		int n = SnapshotWire.ReadHeader(ref r, out uint tick, out uint ack);
		Check.True(n == 2 && tick == 600 && ack == 42, "cabeçalho");
		EntityState a = EntityState.Read(ref r), b = EntityState.Read(ref r);
		Check.Near(3, a.Position.Z, 0.001, "posição");
		Check.True(b.Has(EntityFlags.Dead) && b.Epoch == 7 && b.Index == 3, "marcas e vida");
		Check.Near(-9.8, b.Velocity.Y, 0.01, "velocidade");
		Check.Equal(0, r.Left, "nada sobrando");
	}

	[Test("histórico: volta no tempo interpola entre passos e esquece o que saiu do anel")]
	public static void PoseHistoryRewind()
	{
		var h = new PoseHistory(64);
		for (uint t = 1; t <= 100; t++) h.Record(t, new Vector3(t, 0, 0), 0, 0, 1.75f);

		Check.True(h.Sample(90.5, out PoseSample p), "passo recente");
		Check.Near(90.5, p.Position.X, 0.0001, "entre dois passos, no meio");
		Check.False(h.Sample(20, out _), "passo velho demais já saiu");
		Check.False(h.Sample(101, out _), "passo que ainda não houve");
		float half = PoseHistory.LerpAngle(3.0f, -3.0f, 0.5f);
		Check.Near(0, Math.IEEERemainder(half - Math.PI, Math.Tau), 0.001, "ângulo pelo caminho curto (por trás, não pela frente)");
	}

	[Test("interpolação: desenha entre dois estados, adivinha um pouco à frente e pula ao renascer")]
	public static void InterpolationBufferSamples()
	{
		var buffer = new InterpolationBuffer();
		buffer.Add(new NetPose { Time = 1.0, Position = new Vector3(0, 0, 0), Velocity = new Vector3(10, 0, 0) });
		buffer.Add(new NetPose { Time = 1.1, Position = new Vector3(1, 0, 0), Velocity = new Vector3(10, 0, 0) });

		Check.True(buffer.Sample(1.05, out NetPose p), "entre os dois");
		Check.Near(0.5, p.Position.X, 0.0001, "no meio");

		buffer.Sample(1.15, out p);
		Check.Near(1.5, p.Position.X, 0.0001, "à frente: pela velocidade");
		buffer.Sample(5.0, out p);
		Check.Near(1 + 10 * NetRates.MaxExtrapolation, p.Position.X, 0.0001, "e só até o limite");

		buffer.Add(new NetPose { Time = 1.2, Position = new Vector3(50, 0, 0), Epoch = 1 });
		buffer.Sample(1.15, out p);
		Check.Near(50, p.Position.X, 0.0001, "renasceu entre os dois: pula, não desliza");
	}

	[Test("relógio do servidor: segue o estado que chega mais cedo e se acostuma devagar ao atraso")]
	public static void ServerClockTracks()
	{
		var clock = new ServerClock();
		clock.Observe(600, 0.0);   // passo 600 = 10 s
		Check.Near(10.0, clock.Now(0.0), 0.0001, "primeira notícia");
		clock.Observe(660, 1.0);
		Check.Near(11.0, clock.Now(1.0), 0.0001, "no ritmo");
		clock.Observe(661, 1.5);   // chegou atrasado (~0,5 s)
		Check.True(clock.Now(1.5) > 11.47, "um estado atrasado mexe pouco");
		Check.Near(clock.Now(1.5) - NetRates.InterpolationDelay, clock.RenderTime(1.5), 0.0001, "os outros, 100 ms no passado");
	}

	[Test("predição: diferença pequena é ruído; grande corrige o agora e os passos seguintes")]
	public static void PredictionReconciles()
	{
		var p = new Prediction();
		for (uint s = 1; s <= 10; s++) p.Record(s, new Vector3(s, 0, 0), Vector3.Zero);

		Check.Equal(Vector3.Zero, p.Reconcile(5, 10, new Vector3(5.01f, 0, 0)), "1 cm: nada");
		Vector3 fix = p.Reconcile(5, 10, new Vector3(5, 0, 0.5f));
		Check.Near(0.5, fix.Z, 0.0001, "meio metro: corrige");
		Check.Equal(Vector3.Zero, p.Reconcile(8, 10, new Vector3(8, 0, 0.5f)), "os passos seguintes já estão corrigidos");
		Check.Equal(1, p.Corrections, "uma correção só");
	}
}
