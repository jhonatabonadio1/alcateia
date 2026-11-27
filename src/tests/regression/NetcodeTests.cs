using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Base das partidas autoritativas (decisão 0011): um "servidor" de
/// verdade dentro do teste — o mesmo <see cref="MatchWorld"/> e o mesmo
/// <see cref="ServerBody"/> do servidor de partida — num mundo de física
/// próprio, ligado ao jogador da partida por uma rede de mentira com
/// atraso e perda de pacote. O jogador anda com as teclas de sempre.
/// </summary>
public abstract class NetcodeTest : RegressionTest
{
	public override string Scene => RegressionBench.Match;

	public override void Prepare()
	{
		RoomConfig.Settings = new RoomSettings { Map = "armazem_estendido", TeamSize = 4 };
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Teste", Team = Teams.Blue });
		RoomConfig.Active = true;
	}

	/// <summary>Uma rede de mentira: cada pacote chega <see cref="Latency"/>
	/// passos depois; um a cada <see cref="DropEvery"/> some.</summary>
	protected sealed class FakeLink
	{
		public int Latency = 6, DropEvery = 7;
		private readonly List<(uint Due, byte[] Packet)> _flying = new();
		private int _sent;

		public void Send(uint now, byte[] packet)
		{
			if (DropEvery > 0 && ++_sent % DropEvery == 0) return;
			_flying.Add((now + (uint)Latency, packet));
		}

		public IEnumerable<byte[]> Arrived(uint now)
		{
			var due = _flying.Where(f => f.Due <= now).ToList();
			_flying.RemoveAll(f => f.Due <= now);
			return due.Select(f => f.Packet);
		}
	}

	protected static async Task<MatchWorld> StartServer(RegressionBench b)
	{
		var world = new MatchWorld("armazem_estendido");
		b.AddChild(world);
		await b.Frames(2);
		return world;
	}
}

/// <summary>
/// O servidor dá o mesmo passo que o jogo previu: com a mesma entrada
/// (andar, virar, pular, agachar, andar devagar), atraso de 100 ms e um
/// pacote a cada sete perdido, o corpo do servidor fica onde o jogo achou
/// que estaria. E quando discordam (o servidor empurra o corpo), o jogo
/// corrige uma vez e volta a bater.
/// </summary>
public sealed class ServerPredictionTest : NetcodeTest
{
	public override string Name => "PredicaoNoServidor";
	public override string Description => "o servidor move o corpo pelos comandos e chega onde o jogo previu (100 ms de atraso, pacote perdido); discordou, o jogo corrige e volta a bater";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);
		Player p = b.Player;
		await b.Place(p.GlobalPosition, p.RotationDegrees.Y, settle: 0.5);

		MatchWorld world = await StartServer(b);
		var server = new ServerBody(1, 0, world.SpawnBody("Servidor", Teams.Blue));
		server.ApplyLoadout(Profile.OwnedWolfUpgrades());

		Player.NetQuantize = true;
		var up = new FakeLink();
		var down = new FakeLink { DropEvery = 0 };
		var prediction = new Prediction();
		var recent = new InputCommand[InputCommand.Redundancy];
		var buffer = new byte[256];
		var clientAt = new Dictionary<uint, Vector3>();
		Vector3 spawn = p.GlobalPosition;
		uint seq = 0, tick = 0;
		bool confirmed = false, pushed = false;
		float worstBefore = 0f, worstAfter = 0f;
		double worstAt = 0;
		int correctionsBefore = 0;
		var watch = new Stopwatch();
		double serverMs = 0;

		// Roteiro: segundo em que começa → o que segura.
		var plan = new (double At, string[] Hold, bool Jump, float Turn)[]
		{
			(0.0, new[] { "move_forward" }, false, 0f),
			(1.0, new[] { "move_forward", "move_right" }, false, 60f),
			(1.6, new[] { "move_forward" }, true, 0f),
			(2.0, new[] { "move_back" }, false, -40f),
			(2.6, new[] { "crouch", "move_left" }, false, 0f),
			(3.4, new[] { "move_forward" }, true, 30f),
			(4.0, new[] { "move_forward" }, true, 0f),
			(4.4, new[] { "sprint", "move_forward" }, false, 0f),
			(5.0, new string[0], false, 0f),
			// Daqui em diante o servidor empurra o corpo (o jogo não sabe) e anda de novo.
			(5.6, new[] { "move_forward" }, false, 20f),
			(7.5, new string[0], false, 0f),
		};

		string[] held = new string[0];
		for (int step = 0; step < 8.5 * 60; step++)
		{
			double t = step / 60.0;
			var phase = plan.Last(x => x.At <= t + 1e-6);
			if (Mathf.IsEqualApprox((float)phase.At, (float)t))
			{
				foreach (string a in held) RegressionBench.Release(a);
				held = phase.Hold;
				foreach (string a in held) RegressionBench.Press(a);
				if (phase.Jump) RegressionBench.Press("jump");
			}
			else if (step > 0 && plan.Any(x => x.Jump && Mathf.IsEqualApprox((float)(x.At + 1 / 60.0), (float)t))) RegressionBench.Release("jump");

			if (phase.Turn != 0) p.RotateY(Mathf.DegToRad(phase.Turn) / 60f);

			await b.Frames(1);
			tick++;

			// O jogo: o comando do passo que acabou de dar.
			seq++;
			InputCommand c = NetCommands.From(p.Intent, seq, p.Rotation.Y, p.HeadPitch);
			c.Form = (byte)p.GetNode<FormController>("FormController").Current;
			c.HeldSlot = (byte)p.GetNode<Weapon>("Weapon").HumanSlot;
			c.Frozen = p.Frozen;
			if (!confirmed)
			{
				c.HasSpawn = true;
				c.Spawn = spawn.ToNumerics();
			}
			recent[seq % InputCommand.Redundancy] = c;
			prediction.Record(seq, p.GlobalPosition.ToNumerics(), p.Velocity.ToNumerics());
			clientAt[seq] = p.GlobalPosition;
			up.Send(tick, NetCommands.InputPacket(recent, seq, buffer));

			// O servidor: recebe o que chegou e dá o passo dele.
			foreach (byte[] packet in up.Arrived(tick)) NetCommands.ReadInputPacket(packet, server.Commands);
			watch.Restart();
			server.Step(tick, NetRates.TickSeconds);
			serverMs += watch.Elapsed.TotalMilliseconds;

			if (!pushed && t >= 5.5)
			{
				// O servidor discorda: algo empurrou o corpo meio metro.
				pushed = true;
				correctionsBefore = prediction.Corrections;
				server.Body.GlobalPosition += new Vector3(0.5f, 0, 0);
			}

			uint ack = server.Commands.LastProcessed;
			if (ack > 0 && clientAt.TryGetValue(ack, out Vector3 predicted))
			{
				float error = predicted.DistanceTo(server.Body.GlobalPosition);
				if (!pushed && error > worstBefore)
				{
					worstBefore = error;
					worstAt = t;
				}
				else if (t > 6.6) worstAfter = Mathf.Max(worstAfter, error);
			}

			// O estado do mundo volta para o jogo (a cada dois passos, com atraso).
			if (tick % NetRates.SnapshotEvery == 0 && server.Placed)
				down.Send(tick, Snapshot(server, ack));
			foreach (byte[] packet in down.Arrived(tick))
			{
				(uint acked, EntityState e) = ReadSnapshot(packet);
				confirmed = true;
				System.Numerics.Vector3 fix = prediction.Reconcile(acked, seq, e.Position);
				if (fix != System.Numerics.Vector3.Zero)
				{
					p.GlobalPosition += fix.ToGodot();
					foreach (uint s in clientAt.Keys.Where(s => s >= acked).ToList()) clientAt[s] += fix.ToGodot();
				}
			}
		}
		foreach (string a in held) RegressionBench.Release(a);
		Player.NetQuantize = false;

		RegressionBench.Note($"comandos {seq}, simulados {server.Simulated}, repetidos {server.Commands.Starved}, pulados {server.Commands.Skipped}");
		RegressionBench.Note($"servidor: {RegressionBench.F(serverMs / tick)} ms por passo para um corpo");
		RegressionBench.Note($"pior diferença antes do empurrão: {RegressionBench.F(worstBefore)} m aos {RegressionBench.F(worstAt)} s");
		b.Check(server.Placed, "o servidor pôs o corpo onde o jogo nasceu");
		b.AtLeast(seq * 0.9, server.Simulated, "o servidor simulou (quase) todos os comandos");
		b.AtMost(0.05, worstBefore, "sem empurrão, servidor e jogo no mesmo lugar (pior diferença, m)");
		b.AtMost(2, correctionsBefore, "sem empurrão, quase nenhuma correção");
		b.AtLeast(1, prediction.Corrections - correctionsBefore, "o empurrão do servidor vira correção no jogo");
		b.AtMost(3, prediction.Corrections - correctionsBefore, "uma correção (ou poucas), sem ficar oscilando");
		b.AtMost(0.05, worstAfter, "depois da correção, os dois voltam a bater (pior diferença, m)");

		world.QueueFree();
	}

	private static (uint Ack, EntityState Me) ReadSnapshot(byte[] packet)
	{
		var r = new PacketReader(packet, 1);
		SnapshotWire.ReadHeader(ref r, out _, out uint ack);
		return (ack, EntityState.Read(ref r));
	}

	private static byte[] Snapshot(ServerBody server, uint ack)
	{
		var body = new byte[64];
		var packet = new byte[128];
		int length = SnapshotWire.WriteBody(body, new[] { server.State() });
		int size = SnapshotWire.Compose(packet, NetWire.Snapshot, 0, ack, body.AsSpan(0, length));
		return packet.AsSpan(0, size).ToArray();
	}
}

/// <summary>
/// Quanto custa ao servidor: partidas de 16 jogadores (o máximo da sala)
/// correndo, virando, pulando e agachando, cada uma no próprio mundo.
/// Mede o passo inteiro (comandos, movimento, histórico e o estado do
/// mundo escrito) e confere que cabe com folga nos 16,7 ms do passo.
/// </summary>
public sealed class ServerStepCostTest : NetcodeTest
{
	public override string Name => "PassoDoServidor";
	public override string Description => "o passo do servidor com partidas de 16 corpos correndo e pulando cabe com folga nos 16,7 ms";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);
		List<Vector3> spawns = SpawnPoints.PointsFor(Teams.Blue).Concat(SpawnPoints.PointsFor(Teams.Orange)).ToList();

		foreach (int matches in new[] { 1, 4 })
		{
			var worlds = new List<MatchWorld>();
			var bodies = new List<ServerBody>();
			for (int m = 0; m < matches; m++)
			{
				MatchWorld world = await StartServer(b);
				worlds.Add(world);
				for (int i = 0; i < 16; i++)
					bodies.Add(new ServerBody(i + 1, (byte)i, world.SpawnBody($"Corpo{i}", i % 2 == 0 ? Teams.Blue : Teams.Orange)));
			}

			var states = new EntityState[16];
			var snapshot = new byte[1 + 16 * EntityState.Size];
			var times = new List<double>();
			var watch = new Stopwatch();
			uint seq = 0;

			for (int step = 0; step < 660; step++)
			{
				seq++;
				watch.Restart();
				for (int k = 0; k < bodies.Count; k++)
				{
					ServerBody body = bodies[k];
					int i = k % 16;
					var c = new InputCommand
					{
						Seq = seq,
						Buttons = InputButtons.Forward
							| (step % 90 == i ? InputButtons.Jump | InputButtons.JumpHeld : 0)
							| (step % 240 > 200 ? InputButtons.Crouch : 0),
						MoveY = -127,
						MoveX = (sbyte)(step % 120 < 60 ? 0 : 90),
						Yaw = Quant.Yaw(step * 0.02f + i),
						Epoch = 0
					};
					if (seq <= 2)
					{
						c.HasSpawn = true;
						c.Spawn = (spawns[i % spawns.Count] + Vector3.Up * 0.2f).ToNumerics();
					}
					body.Commands.Push(c);
					body.Step(seq, NetRates.TickSeconds);
				}
				for (int m = 0; m < matches; m++)
				{
					for (int i = 0; i < 16; i++) states[i] = bodies[m * 16 + i].State();
					SnapshotWire.WriteBody(snapshot, states);
				}
				if (step >= 60) times.Add(watch.Elapsed.TotalMilliseconds);
				await b.Frames(1);
			}

			times.Sort();
			double average = times.Average(), p99 = times[(int)(times.Count * 0.99)];
			RegressionBench.Note($"{matches} partida(s), {bodies.Count} corpos: média {RegressionBench.F(average)} ms, p99 {RegressionBench.F(p99)} ms, pior {RegressionBench.F(times[^1])} ms por passo");
			b.Check(bodies.All(x => x.Placed && x.Body.GlobalPosition.Y > -5f), $"{bodies.Count} corpos no mapa, nenhum caiu");
			b.AtMost(matches == 1 ? 4.0 : 12.0, average, $"{matches} partida(s) de 16: passo médio (ms; o passo tem 16,7)");

			foreach (MatchWorld world in worlds) world.QueueFree();
			await b.Frames(2);
		}
	}
}

/// <summary>
/// O estado de um corpo pela rede leva o que o HUD dele mostra (19/11:
/// quem assiste um bot de outra máquina no espectador vê o pente, a
/// recarga, as granadas, o WP e a carga certos) — e quem manda a mensagem
/// antiga, sem isso, ainda é lido.
/// </summary>
public sealed class NetGearTest : RegressionTest
{
	public override string Name => "EstadoComEquipamento";
	public override string Description => "o estado pela rede leva pente, recarga, granadas, WP e carga, e a mensagem antiga ainda é lida";

	public override async Task Run(RegressionBench b)
	{
		var sent = new NetState { Position = new Vector3(1, 2, 3), Hp = 150, MaxHp = 200, Primary = "ar15", Secondary = "deagle",
			HasGear = true, Ammo = 17, Reloading = true, Grenades = 1, Wp = 420, Charge = 0.75f };
		var stream = new System.IO.MemoryStream();
		sent.Write(new System.IO.BinaryWriter(stream));
		NetState got = NetState.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(stream.ToArray())));
		b.Check(got.HasGear && got.Ammo == 17 && got.Reloading && got.Grenades == 1 && Mathf.IsEqualApprox(got.Wp, 420) && Mathf.IsEqualApprox(got.Charge, 0.75f),
			"pente, recarga, granadas, WP e carga chegam");
		b.Check(got.Primary == "ar15" && Mathf.IsEqualApprox(got.Hp, 150), "e o resto do estado também");

		var old = new NetState { Position = Vector3.One, Hp = 90, MaxHp = 200, Primary = "ar15", Secondary = "deagle" };
		stream = new System.IO.MemoryStream();
		old.Write(new System.IO.BinaryWriter(stream));
		got = NetState.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(stream.ToArray())));
		b.Check(!got.HasGear && Mathf.IsEqualApprox(got.Hp, 90), "a mensagem sem equipamento continua sendo lida");
		await b.Frames(1);
	}
}
