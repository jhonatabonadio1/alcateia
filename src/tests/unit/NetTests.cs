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
		Check.Equal(26, w.Length, "com renascimento");
		r = new PacketReader(w.Written);
		Check.Near(-30, InputCommand.Read(ref r).Spawn.Z, 0.01, "onde renasceu");
	}

	[Test("comando do especial: o ponto mirado e o alvo travado vão juntos, cabem no máximo e voltam iguais")]
	public static void InputCommandSpecialRoundTrip()
	{
		Span<byte> buffer = stackalloc byte[64];
		var c = new InputCommand
		{
			Seq = 77, Buttons = InputButtons.Attack, Epoch = 2,
			HasSpawn = true, Spawn = new Vector3(1, 0, 2),
			HasAim = true, Aim = new Vector3(-4.321f, 1.5f, 812.25f),
			HasFocus = true, Focus = 7, FocusAt = new Vector3(-5, 1.2f, 815), FocusVelocity = new Vector3(3.5f, 0, -1.25f)
		};
		var w = new PacketWriter(buffer);
		c.Write(ref w);
		// Pior caso: renascimento (9) + ponto (9) + alvo (1 + 9 + 6) sobre os 17.
		Check.Equal(InputCommand.MaxSize, w.Length, "tudo junto é o máximo");
		var r = new PacketReader(w.Written);
		InputCommand back = InputCommand.Read(ref r);
		Check.True(back.HasAim && !back.AimNone && back.HasFocus && back.HasSpawn, "marcas do especial");
		Check.Near(812.25, back.Aim.Z, 0.0051, "ponto mirado, em cm");
		Check.Equal((byte)7, back.Focus, "lugar do alvo");
		Check.Near(-5, back.FocusAt.X, 0.0051, "onde o jogo via o alvo");
		Check.Near(-1.25, back.FocusVelocity.Z, 0.0051, "para onde ele ia");
		Check.Equal(0, r.Left, "não sobra byte para o próximo comando");

		// Saltou para o céu: a marca vai sem o ponto (1 bit, nenhum byte).
		c = new InputCommand { Seq = 78, HasAim = true, AimNone = true };
		w = new PacketWriter(buffer);
		c.Write(ref w);
		Check.Equal(17, w.Length, "sem ponto, só a marca");
		r = new PacketReader(w.Written);
		back = InputCommand.Read(ref r);
		Check.True(back.HasAim && back.AimNone, "o céu volta como céu");

		// Comando que não chegou: o clique não se repete, o alvo segue.
		c = new InputCommand { Seq = 79, HasAim = true, Aim = Vector3.One, HasFocus = true, Focus = 3 };
		InputCommand held = c.Held();
		Check.False(held.HasAim, "o ponto do clique não repete");
		Check.True(held.HasFocus && held.Focus == 3, "o alvo travado continua");
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

	[Test("fila de comandos: funda demais por meio segundo, apressa devagar até voltar; o engasgo curto não apressa")]
	public static void CommandBufferCatchUp()
	{
		// Mudou de propósito (26/11, "microtravamentos"): antes eram dois por
		// passo assim que passava de 4; depois de um engasgo do servidor o corpo
		// andava o dobro e quem o via dava um tranco.
		var b = new CommandBuffer();
		uint seq = 0;
		for (int i = 0; i < 18; i++) b.Push(Cmd(++seq));
		int extra = 0;
		// O servidor recupera o engasgo: 16 passos seguidos, um por passo.
		for (int i = 0; i < 16; i++)
		{
			int wanted = b.WantedThisTick;
			extra += wanted - 1;
			for (int k = 0; k < wanted; k++) b.Next(out _, out _);
		}
		Check.Equal(0, extra, "engasgo do servidor recuperado em poucos quadros: nenhum passo apressado");

		// O jogo à frente de verdade: a fila fica funda, um comando novo por passo.
		var c = new CommandBuffer();
		seq = 0;
		for (int i = 0; i < 12; i++) c.Push(Cmd(++seq));
		int ticks = 0, doubles = 0;
		while (c.Depth > CommandBuffer.HighWater && ticks < 400)
		{
			c.Push(Cmd(++seq));
			int wanted = c.WantedThisTick;
			if (wanted == 2) doubles++;
			for (int k = 0; k < wanted; k++) c.Next(out _, out _);
			ticks++;
		}
		Check.True(c.Depth <= CommandBuffer.HighWater, $"a fila volta ao normal ({ticks} passos)");
		Check.True(ticks >= CommandBuffer.DeepPatience, "mas só depois de meio segundo funda");
		Check.True(doubles * CommandBuffer.CatchUpEvery <= ticks, $"e apressa devagar: um a mais a cada {CommandBuffer.CatchUpEvery} passos ({doubles} em {ticks})");
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
		// Mudou de propósito (26/11, "microtravamentos"): o desenho fica mais
		// atrás com a rede irregular (o atraso cresce com o jitter, até +80 ms).
		Check.Near(clock.Now(1.5) - clock.Delay, clock.RenderTime(1.5), 0.0001, "os outros, no passado: 100 ms mais a folga do jitter");
		Check.True(clock.Delay > NetRates.InterpolationDelay && clock.Delay <= NetRates.InterpolationDelay + ServerClock.MaxExtraDelay + 1e-9,
			$"com um estado atrasado, a folga cresce, até o teto ({clock.Delay:0.000} s)");
	}

	/// <summary>Um estado a cada dois passos (30 por segundo), de <paramref name="from"/>
	/// até <paramref name="to"/> segundos daqui; o atraso de cada um, de <paramref name="late"/>.</summary>
	private static void Feed(ServerClock clock, ref uint tick, double from, double to, System.Func<double, double> late)
	{
		for (double t = from; t < to; t += 2.0 / 60)
		{
			tick += 2;
			clock.Observe(tick, t + late(t));
		}
	}

	[Test("relógio do servidor: o desenho nunca volta nem pula, com jitter e com o servidor engasgando (26/11)")]
	public static void ServerClockRendersSmoothly()
	{
		var clock = new ServerClock();
		var rng = new System.Random(7);
		uint tick = 0;
		var arrivals = new System.Collections.Generic.List<(double At, uint Tick)>();
		// 10 s de estados: atraso de 75 ms, jitter de 0 a 40 ms, e o servidor parado
		// 0,3 s aos 4 s (os estados desse trecho chegam todos juntos no fim).
		for (double t = 0; t < 10; t += 2.0 / 60)
		{
			tick += 2;
			double at = t + 0.075 + rng.NextDouble() * 0.04;
			if (t >= 4.0 && t < 4.3) at = 4.3 + 0.075;
			arrivals.Add((at, tick));
		}
		arrivals.Sort((a, b) => a.At.CompareTo(b.At));

		int next = 0;
		double last = double.NaN, lastStep = double.NaN, worstChange = 0, smallestStep = double.MaxValue;
		for (double local = 0; local < 10; local += 1.0 / 60)
		{
			while (next < arrivals.Count && arrivals[next].At <= local) clock.Observe(arrivals[next].Tick, arrivals[next++].At);
			if (!clock.Known) continue;
			double render = clock.RenderTime(local);
			if (!double.IsNaN(last))
			{
				double step = render - last;
				smallestStep = System.Math.Min(smallestStep, step);
				if (!double.IsNaN(lastStep) && local > 1) worstChange = System.Math.Max(worstChange, System.Math.Abs(step - lastStep) * 60);
				lastStep = step;
			}
			last = render;
		}
		Check.True(smallestStep >= 0, $"o desenho nunca volta no tempo (menor passo {smallestStep * 1000:0.00} ms)");
		Check.True(worstChange <= ServerClock.RateSlew / 60 + 1e-6, $"e o ritmo muda devagar, sem tranco (maior mudança de um quadro para o outro {worstChange:0.000})");
	}

	[Test("relógio do servidor: chegando no último estado, freia e para em vez de seguir adivinhando; depois alcança")]
	public static void ServerClockCushions()
	{
		var clock = new ServerClock();
		uint tick = 0;
		Feed(clock, ref tick, 0, 2, _ => 0.05);
		double newest = tick * NetRates.TickSeconds;
		double render = 0;
		// Para de chegar estado por meio segundo.
		for (double local = 2; local < 2.5; local += 1.0 / 60) render = clock.RenderTime(local);
		Check.True(render <= newest + NetRates.MaxExtrapolation, $"sem estado novo, o desenho freia e para: no máximo o que se adivinha além do último ({(render - newest) * 1000:0.0} ms além dele)");
		// Voltam os estados: o desenho volta a andar e alcança o alvo.
		Feed(clock, ref tick, 2.5, 6, _ => 0.05);
		double target = clock.Now(6) - clock.Delay;
		Check.Near(target, clock.RenderTime(6), 0.03, "e depois alcança o alvo, andando um pouco mais rápido");
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
