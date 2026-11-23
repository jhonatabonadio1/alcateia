using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O lobo na parede na partida online (Jhon, 21/11: "a animação do lobo de
/// andar na parede não tá funcionando com o servidor em produção"). Os
/// testes põem o servidor de partida dentro do teste (o mesmo
/// <see cref="MatchWorld"/> e <see cref="ServerBody"/>), ligado ao jogador
/// por uma rede de mentira, e conferem a parede dos três lados: o jogo, o
/// servidor e o boneco de quem vê de outra máquina.
/// </summary>
public abstract class WallNetTest : NetcodeTest
{
	/// <summary>Uma rede que oscila: o atraso de cada pacote sorteado entre
	/// <see cref="MinDelay"/> e <see cref="MaxDelay"/> passos, um a cada
	/// <see cref="DropOneIn"/> perdido. Sorteio fixo: o teste se repete.</summary>
	protected sealed class JitterLink
	{
		public int MinDelay = 3, MaxDelay = 11, DropOneIn = 5;
		private readonly List<(uint Due, byte[] Packet)> _flying = new();
		private readonly Random _rng = new(7);

		public void Send(uint now, byte[] packet)
		{
			if (DropOneIn > 0 && _rng.Next(DropOneIn) == 0) return;
			_flying.Add((now + (uint)_rng.Next(MinDelay, MaxDelay + 1), packet));
		}

		public IEnumerable<byte[]> Arrived(uint now)
		{
			var due = _flying.Where(f => f.Due <= now).ToList();
			_flying.RemoveAll(f => f.Due <= now);
			return due.Select(f => f.Packet);
		}
	}

	/// <summary>O comando do passo que o jogador acabou de dar, como o
	/// <see cref="NetMatch"/> monta.</summary>
	protected static InputCommand Command(Player p, uint seq, bool withSpawn, Vector3 spawn)
	{
		InputCommand c = NetCommands.From(p.Intent, seq, p.Rotation.Y, p.HeadPitch);
		c.Form = (byte)p.GetNode<FormController>("FormController").Current;
		c.HeldSlot = (byte)p.GetNode<Weapon>("Weapon").HumanSlot;
		c.Frozen = p.Frozen;
		c.OnWall = p.IsWallRunning;
		if (withSpawn)
		{
			c.HasSpawn = true;
			c.Spawn = spawn.ToNumerics();
		}
		return c;
	}

	protected static string CurrentState(WolfVisual visual)
	{
		var tree = visual.FindChildren("*", nameof(AnimationTree), true, false).OfType<AnimationTree>().FirstOrDefault();
		var playback = tree?.Get("parameters/Machine/playback").As<AnimationNodeStateMachinePlayback>();
		return playback?.GetCurrentNode().ToString() ?? "";
	}

	protected static (uint Ack, EntityState Me) ReadSnapshot(byte[] packet)
	{
		var r = new PacketReader(packet, 1);
		SnapshotWire.ReadHeader(ref r, out _, out uint ack);
		return (ack, EntityState.Read(ref r));
	}

	protected static byte[] Snapshot(ServerBody server, uint ack)
	{
		var body = new byte[64];
		var packet = new byte[128];
		int length = SnapshotWire.WriteBody(body, new[] { server.State() });
		int size = SnapshotWire.Compose(packet, NetWire.Snapshot, 0, ack, body.AsSpan(0, length));
		return packet.AsSpan(0, size).ToArray();
	}

	/// <summary>Um pátio de teste bem acima do mapa, igual no mundo do jogo e
	/// no do servidor: um piso e uma parede de 18 m ao longo de
	/// <paramref name="along"/>, com o jogador do lado de
	/// <paramref name="normal"/>. Os mapas não têm parede reta e livre
	/// desse tamanho perto do nascimento.</summary>
	protected static bool PlaceTestWall(Player p, Node clientWorld, Node serverWorld, out Vector3 start, out Vector3 along, out Vector3 normal)
	{
		const float Length = 18f, Height = 4f, Thickness = 0.4f;
		var space = p.GetWorld3D().DirectSpaceState;
		var exclude = new Godot.Collections.Array<Rid> { p.GetRid() };
		along = Vector3.Right;
		normal = Vector3.Back;   // da parede (em z = 0 do pátio) para o jogador (+Z)
		var floorShape = new BoxShape3D { Size = new Vector3(30f, 1f, 30f) };
		var wallShape = new BoxShape3D { Size = new Vector3(Length, Height, Thickness) };

		foreach (float lift in new[] { 80f, 160f, 300f })
		{
			Vector3 floorCenter = p.GlobalPosition + Vector3.Up * lift;
			var free = new PhysicsShapeQueryParameters3D
			{
				Shape = new BoxShape3D { Size = new Vector3(30f, 8f, 30f) },
				Transform = new Transform3D(Basis.Identity, floorCenter + Vector3.Up * 3.5f),
				Exclude = exclude
			};
			if (space.IntersectShape(free, 1).Count > 0) continue;

			Vector3 wallCenter = floorCenter + new Vector3(0, 0.5f + Height / 2f, 0);
			foreach (Node world in new[] { clientWorld, serverWorld })
			{
				var floor = new StaticBody3D { Name = "PisoDeTeste" };
				floor.AddChild(new CollisionShape3D { Shape = floorShape });
				world.AddChild(floor);
				floor.GlobalPosition = floorCenter;

				var wall = new StaticBody3D { Name = "ParedeDeTeste" };
				wall.AddChild(new CollisionShape3D { Shape = wallShape });
				world.AddChild(wall);
				wall.GlobalPosition = wallCenter;
			}
			start = floorCenter + new Vector3(-Length / 2f + 1f, 0.5f + 0.9f, Thickness / 2f + 0.42f);
			return true;
		}

		start = Vector3.Zero;
		return false;
	}
}

/// <summary>
/// Quem vê de outra máquina: o estado do mundo que o servidor manda move um
/// boneco, e o lobo do boneco tem de tocar o clipe da parede e deitar nela,
/// não sair com o pulo, de pé. Até 21/11 o desenho do lobo só perguntava
/// "está na parede?" ao jogador daqui.
/// </summary>
public sealed class WallRunOnlineTest : WallNetTest
{
	public override string Name => "ParedeOnline";
	public override string Description => "online, quem vê de outra máquina vê o lobo deitado na parede, com o clipe dela; e a parede vai e volta no estado do repasse";

	public override async Task Run(RegressionBench b)
	{
		// Partida de repasse: o estado que cada jogo manda leva a parede
		// num bit do byte de Grounded e Dead.
		using (var stream = new System.IO.MemoryStream())
		{
			new NetState { WallRunning = true, Grounded = false, Dead = false }.Write(new System.IO.BinaryWriter(stream));
			stream.Position = 0;
			NetState back = NetState.Read(new System.IO.BinaryReader(stream));
			b.Check(back.WallRunning && !back.Grounded && !back.Dead, "repasse: a parede vai e volta no estado, sem mexer nos outros bits");
		}

		b.RemoveBots();
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);
		await b.BecomeWolf();
		Player p = b.Player;
		await b.Place(p.GlobalPosition, p.RotationDegrees.Y, settle: 0.5);

		MatchWorld world = await StartServer(b);
		if (!PlaceTestWall(p, b.GetTree().CurrentScene, world, out Vector3 start, out Vector3 along, out Vector3 normal))
		{
			b.Check(false, "achou lugar para a parede de teste");
			return;
		}
		await b.Place(start, Mathf.RadToDeg(Mathf.Atan2(-along.X, -along.Z)), settle: 0.6);

		var server = new ServerBody(1, 0, world.SpawnBody("Servidor", Teams.Blue));
		server.ApplyLoadout(Profile.Breed, Profile.OwnedTattoos());

		// O boneco de quem vê de outra máquina. Não colide: ocupa o mesmo
		// lugar do jogador deste teste.
		var puppet = GD.Load<PackedScene>("res://scenes/bot.tscn").Instantiate<Bot>();
		puppet.Name = "Boneco";
		puppet.Team = Teams.Orange;
		puppet.Remote = true;
		b.GetTree().CurrentScene.AddChild(puppet);
		await b.Frames(2);
		var visual = puppet.GetNode<WolfVisual>("WolfVisual");

		Player.NetQuantize = true;
		var up = new FakeLink();
		var down = new FakeLink { DropEvery = 0 };
		var prediction = new Prediction();
		var recent = new InputCommand[InputCommand.Redundancy];
		var buffer = new byte[256];
		Vector3 spawn = p.GlobalPosition;
		uint seq = 0, tick = 0;
		bool confirmed = false;
		int clientWall = 0, serverWall = 0, puppetWall = 0, puppetClip = 0;
		float lying = -1f;

		for (int step = 0; step < 3 * 60; step++)
		{
			if (step == 30) RegressionBench.Press("move_forward");
			if (step == 33) RegressionBench.Press("jump");
			if (step == 35) RegressionBench.Release("jump");

			await b.Frames(1);
			tick++;

			seq++;
			recent[seq % InputCommand.Redundancy] = Command(p, seq, !confirmed, spawn);
			prediction.Record(seq, p.GlobalPosition.ToNumerics(), p.Velocity.ToNumerics());
			up.Send(tick, NetCommands.InputPacket(recent, seq, buffer));

			foreach (byte[] packet in up.Arrived(tick)) NetCommands.ReadInputPacket(packet, server.Commands);
			server.Step(tick, NetRates.TickSeconds);

			if (p.IsWallRunning) clientWall++;
			if (server.Body.IsWallRunning) serverWall++;
			if (puppet.RemoteWallRun)
			{
				puppetWall++;
				if (CurrentState(visual) == "Wall") puppetClip++;
				if (visual.Model != null) lying = Mathf.Max(lying, visual.Model.GlobalBasis.Y.Normalized().Dot(normal));
			}

			if (tick % NetRates.SnapshotEvery == 0 && server.Placed)
				down.Send(tick, Snapshot(server, server.Commands.LastProcessed));
			foreach (byte[] packet in down.Arrived(tick))
			{
				(uint acked, EntityState e) = ReadSnapshot(packet);
				confirmed = true;
				System.Numerics.Vector3 fix = prediction.Reconcile(acked, seq, e.Position);
				if (fix != System.Numerics.Vector3.Zero) p.GlobalPosition += fix.ToGodot();

				// O que o NetMatch faz com cada boneco (UpdatePuppets).
				Form form = Enum.IsDefined(typeof(Form), (int)e.Form) ? (Form)e.Form : Form.Human;
				puppet.RemoteSnapshot(e.Position.ToGodot(), e.Velocity.ToGodot(), Quant.Yaw(e.Yaw), Quant.Pitch(e.Pitch),
					e.Has(EntityFlags.Grounded), form, (Slot)e.Slot);
				puppet.RemoteWallRun = e.Has(EntityFlags.WallRunning);
				puppet.CollisionLayer = 0;
			}
		}
		RegressionBench.ReleaseAll();
		Player.NetQuantize = false;

		RegressionBench.Note($"passos na parede: jogo {clientWall}, servidor {serverWall}, boneco {puppetWall} (com o clipe da parede: {puppetClip}); correções {prediction.Corrections}");
		b.AtLeast(60, clientWall, "o jogo correu na parede (passos)");
		b.AtLeast(60, serverWall, "o servidor correu na parede junto (passos)");
		b.AtMost(2, prediction.Corrections, "sem correção do servidor na parede");
		b.AtLeast(50, puppetWall, "o boneco soube que está na parede (passos)");
		b.AtLeast(puppetWall - 3, puppetClip, "e tocou o clipe da parede, não o do pulo (passos)");
		b.AtLeast(0.9, lying, "deitado na parede: o 'em cima' do lobo é a normal dela");

		puppet.QueueFree();
		world.QueueFree();
	}
}

/// <summary>
/// O seu lobo, na sua tela (Jhon, 21/11: online o lobo "corria sem
/// animação, em pé, mas andando na parede"). Com a rede oscilando, um
/// puxão de correção tira o jogo da parede e o servidor continua nela. A
/// correção só acerta a posição e, solto, o jogo não regrudava (regra da
/// mesma parede): o lobo era arrastado pela parede de pé, na pose do pulo.
/// O teste dá o puxão no meio da corrida (o corpo do jogo, e só ele, meio
/// metro para fora) e confere que o jogo volta para a parede assim que o
/// servidor diz que está nela.
/// </summary>
public sealed class WallRunCorrectionTest : WallNetTest
{
	public override string Name => "ParedeCorrecao";
	public override string Description => "online, com a rede oscilando: um puxão tira o jogo da parede e o servidor segue nela; o jogo volta para a parede, não fica arrastado de pé";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);
		await b.BecomeWolf();
		Player p = b.Player;
		await b.Place(p.GlobalPosition, p.RotationDegrees.Y, settle: 0.5);

		MatchWorld world = await StartServer(b);
		if (!PlaceTestWall(p, b.GetTree().CurrentScene, world, out Vector3 start, out Vector3 along, out Vector3 normal))
		{
			b.Check(false, "achou lugar para a parede de teste");
			return;
		}
		await b.Place(start, Mathf.RadToDeg(Mathf.Atan2(-along.X, -along.Z)), settle: 0.6);

		var server = new ServerBody(1, 0, world.SpawnBody("Servidor", Teams.Blue));
		server.ApplyLoadout(Profile.Breed, Profile.OwnedTattoos());
		var mine = p.GetNode<WolfVisual>("WolfVisual");

		Player.NetQuantize = true;
		var up = new JitterLink();
		var down = new JitterLink { DropOneIn = 0 };
		var prediction = new Prediction();
		var recent = new InputCommand[InputCommand.Redundancy];
		var buffer = new byte[256];
		var wallAt = new Dictionary<uint, bool>();
		Vector3 spawn = p.GlobalPosition;
		uint seq = 0, tick = 0, syncSeq = 0;
		bool confirmed = false;
		const int Push = 100, End = 240;
		int apartAfter = 0, wallAfter = 0, wallClipAfter = 0, resumed = 0;
		bool pushedOff = false;

		for (int step = 0; step < End; step++)
		{
			if (step == 30) RegressionBench.Press("move_forward");
			if (step == 33) RegressionBench.Press("jump");
			if (step == 35) RegressionBench.Release("jump");

			// O puxão: só o corpo do jogo, para fora da parede.
			if (step == Push)
			{
				b.Check(p.IsWallRunning && server.Body.IsWallRunning, "antes do puxão, os dois na parede");
				p.GlobalPosition += normal * 0.5f;
			}

			await b.Frames(1);
			tick++;
			if (step >= Push && step < Push + 20 && !p.IsWallRunning) pushedOff = true;

			seq++;
			recent[seq % InputCommand.Redundancy] = Command(p, seq, !confirmed, spawn);
			prediction.Record(seq, p.GlobalPosition.ToNumerics(), p.Velocity.ToNumerics());
			wallAt[seq] = p.IsWallRunning;
			up.Send(tick, NetCommands.InputPacket(recent, seq, buffer));

			foreach (byte[] packet in up.Arrived(tick)) NetCommands.ReadInputPacket(packet, server.Commands);
			server.Step(tick, NetRates.TickSeconds);

			if (step > Push + 20)
			{
				if (server.Body.IsWallRunning && !p.IsWallRunning) apartAfter++;
				if (p.IsWallRunning)
				{
					wallAfter++;
					if (CurrentState(mine) == "Wall") wallClipAfter++;
				}
			}

			if (tick % NetRates.SnapshotEvery == 0 && server.Placed)
				down.Send(tick, Snapshot(server, server.Commands.LastProcessed));
			foreach (byte[] packet in down.Arrived(tick))
			{
				(uint acked, EntityState e) = ReadSnapshot(packet);
				confirmed = true;
				if (acked == 0) continue;

				// O que o NetMatch faz (Reconcile → SyncWall).
				if (e.Has(EntityFlags.WallRunning) && acked > syncSeq && wallAt.TryGetValue(acked, out bool clientWall) && !clientWall
					&& p.ResumeWallFromServer())
				{
					syncSeq = seq;
					resumed++;
				}
				System.Numerics.Vector3 fix = prediction.Reconcile(acked, seq, e.Position);
				if (fix != System.Numerics.Vector3.Zero) p.GlobalPosition += fix.ToGodot();
			}
		}
		RegressionBench.ReleaseAll();
		Player.NetQuantize = false;

		RegressionBench.Note($"depois do puxão: o servidor na parede e o jogo fora em {apartAfter} passos; o jogo na parede {wallAfter} (com o clipe dela: {wallClipAfter}); voltou por ordem do servidor {resumed} vez(es); correções {prediction.Corrections}; servidor repetiu {server.Commands.Starved}, pulou {server.Commands.Skipped}");
		b.Check(pushedOff, "o puxão tirou o jogo da parede");
		b.AtLeast(1, resumed, "o jogo voltou para a parede pelo que o servidor disse");
		b.AtMost(6, apartAfter, "do terço de segundo depois do puxão em diante, o servidor na parede e o jogo fora dela quase nunca (passos)");
		b.AtLeast(60, wallAfter, "o jogo seguiu na parede (passos)");
		b.AtLeast(wallAfter - 3, wallClipAfter, "com o clipe da parede (passos)");

		world.QueueFree();
	}
}

/// <summary>
/// O outro lado do <see cref="WallRunCorrectionTest"/>: o corpo do
/// servidor sai da parede e o do jogo segue nela (o que sobra de
/// diferença entre os dois: uma quina, o corpo de outro jogador; até
/// 22/11, também os passos a mais e a menos da fila de comandos com a rede
/// oscilando). Sem conserto, a regra da mesma parede ("salte para outra
/// ou toque o chão") não deixava o servidor regrudar: ele caía de pé, quem
/// via de outra máquina via o lobo na pose do pulo deslizando pela parede,
/// e a correção puxava o jogo (Jhon, 22/11: "o lobo fica com a pose de
/// pular deslizando pela parede"). Com a rede oscilando o tempo todo
/// (atraso de 50 a 183 ms, um pacote em cada cinco perdido).
/// </summary>
public sealed class WallRunServerDropTest : WallNetTest
{
	public override string Name => "ParedeServidorSolta";
	public override string Description => "online: o corpo do servidor sai da parede e o do jogo não; o servidor volta para ela (o jogo conta que está na parede), quem vê de fora vê o lobo na parede e a correção não arrasta o jogo";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);
		await b.BecomeWolf();
		Player p = b.Player;
		await b.Place(p.GlobalPosition, p.RotationDegrees.Y, settle: 0.5);

		MatchWorld world = await StartServer(b);
		if (!PlaceTestWall(p, b.GetTree().CurrentScene, world, out Vector3 start, out Vector3 along, out Vector3 normal))
		{
			b.Check(false, "achou lugar para a parede de teste");
			return;
		}
		await b.Place(start, Mathf.RadToDeg(Mathf.Atan2(-along.X, -along.Z)), settle: 0.6);

		var server = new ServerBody(1, 0, world.SpawnBody("Servidor", Teams.Blue));
		server.ApplyLoadout(Profile.Breed, Profile.OwnedTattoos());

		Player.NetQuantize = true;
		var up = new JitterLink();
		var down = new JitterLink { DropOneIn = 0 };
		var prediction = new Prediction();
		var recent = new InputCommand[InputCommand.Redundancy];
		var buffer = new byte[256];
		Vector3 spawn = p.GlobalPosition;
		uint seq = 0, tick = 0;
		bool confirmed = false;
		const int Push = 100, End = 240;
		int apart = 0, serverOff = 0, fixesAfter = 0;
		float dragged = 0f;
		bool pushedOff = false;

		for (int step = 0; step < End; step++)
		{
			if (step == 30) RegressionBench.Press("move_forward");
			if (step == 33) RegressionBench.Press("jump");
			if (step == 35) RegressionBench.Release("jump");

			// O tropeço: só o corpo do servidor solta a parede (como solta
			// numa quina ou num passo pulado).
			if (step == Push)
			{
				b.Check(p.IsWallRunning && server.Body.IsWallRunning, "antes do tropeço, os dois na parede");
				typeof(Player).GetMethod("ReleaseWall", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
					.Invoke(server.Body, null);
			}

			await b.Frames(1);
			tick++;

			seq++;
			recent[seq % InputCommand.Redundancy] = Command(p, seq, !confirmed, spawn);
			prediction.Record(seq, p.GlobalPosition.ToNumerics(), p.Velocity.ToNumerics());
			up.Send(tick, NetCommands.InputPacket(recent, seq, buffer));

			foreach (byte[] packet in up.Arrived(tick)) NetCommands.ReadInputPacket(packet, server.Commands);
			server.Step(tick, NetRates.TickSeconds);
			if (step >= Push && step < Push + 10 && !server.Body.IsWallRunning) pushedOff = true;

			if (step > Push + 20)
			{
				if (p.IsWallRunning && !server.Body.IsWallRunning) apart++;
				if (!server.Body.IsWallRunning) serverOff++;
			}

			if (tick % NetRates.SnapshotEvery == 0 && server.Placed)
				down.Send(tick, Snapshot(server, server.Commands.LastProcessed));
			foreach (byte[] packet in down.Arrived(tick))
			{
				(uint acked, EntityState e) = ReadSnapshot(packet);
				confirmed = true;
				if (acked == 0) continue;
				System.Numerics.Vector3 fix = prediction.Reconcile(acked, seq, e.Position);
				if (fix != System.Numerics.Vector3.Zero)
				{
					p.GlobalPosition += fix.ToGodot();
					if (step > Push + 20)
					{
						fixesAfter++;
						dragged = Mathf.Max(dragged, fix.ToGodot().Length());
					}
				}
			}
		}
		RegressionBench.ReleaseAll();
		Player.NetQuantize = false;

		RegressionBench.Note($"depois do tropeço no servidor: o jogo na parede e o servidor fora em {apart} passos (servidor fora {serverOff}); correções depois {fixesAfter}, a maior {dragged * 100f:0} cm");
		b.Check(pushedOff, "o tropeço tirou o servidor da parede");
		RegressionBench.Note($"o servidor voltou para a parede pelo que o jogo contou {server.WallResumes} vez(es)");
		b.AtMost(6, apart, "do terço de segundo depois do tropeço em diante, o jogo na parede e o servidor fora dela quase nunca (passos)");
		b.AtMost(0.25f, dragged, "a correção não arrasta o jogo (m)");

		world.QueueFree();
	}
}

/// <summary>
/// O mesmo, na partida de repasse (o modo padrão online): cada jogo manda o
/// próprio estado (NetState) e o boneco das outras máquinas segue por ele
/// (NetMatch.Apply). Jhon, 22/11: "a animação de correr na parede não tá
/// indo para o online, novamente".
/// </summary>
public sealed class WallRunRelayTest : WallNetTest
{
	public override string Name => "ParedeRepasse";
	public override string Description => "online no repasse: quem vê de outra máquina vê o lobo deitado na parede, com o clipe dela, pelo estado que o dono manda";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);
		await b.BecomeWolf();
		Player p = b.Player;
		await b.Place(p.GlobalPosition, p.RotationDegrees.Y, settle: 0.5);

		var dummy = new Node3D { Name = "MundoDeTeste" };
		b.GetTree().CurrentScene.AddChild(dummy);
		if (!PlaceTestWall(p, b.GetTree().CurrentScene, dummy, out Vector3 start, out Vector3 along, out Vector3 normal))
		{
			b.Check(false, "achou lugar para a parede de teste");
			return;
		}
		await b.Place(start, Mathf.RadToDeg(Mathf.Atan2(-along.X, -along.Z)), settle: 0.6);

		var puppet = GD.Load<PackedScene>("res://scenes/bot.tscn").Instantiate<Bot>();
		puppet.Name = "Boneco";
		puppet.Team = Teams.Orange;
		puppet.Remote = true;
		b.GetTree().CurrentScene.AddChild(puppet);
		await b.Frames(2);
		var visual = puppet.GetNode<WolfVisual>("WolfVisual");
		var form = p.GetNode<FormController>("FormController");
		var weapon = p.GetNode<Weapon>("Weapon");
		var health = p.GetNode<Health>("Health");

		int clientWall = 0, puppetWall = 0, puppetClip = 0;
		float lying = -1f;
		var states = new System.Collections.Generic.Dictionary<string, int>();
		for (int step = 0; step < 3 * 60; step++)
		{
			if (step == 30) RegressionBench.Press("move_forward");
			if (step == 33) RegressionBench.Press("jump");
			if (step == 35) RegressionBench.Release("jump");
			await b.Frames(1);

			if (step % 2 == 0)
			{
				// O que o NetMatch manda (SendState) e o que faz ao chegar (Apply).
				var sent = new NetState
				{
					Position = p.GlobalPosition, Velocity = p.Velocity, Yaw = p.Rotation.Y,
					Pitch = p.GetNode<Node3D>("Head").Rotation.X, Form = (byte)form.Current,
					Slot = (byte)weapon.HumanSlot, Grounded = p.IsOnFloor(), WallRunning = p.IsWallRunning,
					Sprinting = p.IsSprinting, Crouching = p.IsCrouching, Dead = health.IsDead,
					Hp = health.Hp, MaxHp = health.MaxHp, Shield = health.Shield, MaxShield = health.MaxShield,
					Primary = weapon.PrimaryDef?.Id ?? "", Secondary = weapon.SecondaryDef?.Id ?? "", HasGear = true
				};
				using var stream = new System.IO.MemoryStream();
				sent.Write(new System.IO.BinaryWriter(stream));
				stream.Position = 0;
				NetState s = NetState.Read(new System.IO.BinaryReader(stream));
				Form f = (Form)s.Form;
				puppet.RemoteUpdate(s.Position + Vector3.Back * 0f, s.Velocity, s.Yaw, s.Pitch, s.Grounded, f, (Slot)s.Slot,
					s.Primary, s.Secondary, s.Hp, s.MaxHp, s.Shield, s.MaxShield, s.Dead);
				puppet.RemoteWallRun = s.WallRunning && !s.Dead;
				puppet.RemoteMotion(s.Sprinting, s.Crouching, false, false, false, false);
				puppet.CollisionLayer = 0;
			}

			if (p.IsWallRunning) clientWall++;
			if (puppet.RemoteWallRun)
			{
				puppetWall++;
				string st = CurrentState(visual);
				states[st] = states.GetValueOrDefault(st) + 1;
				if (st == "Wall") puppetClip++;
				if (visual.Model != null) lying = Mathf.Max(lying, visual.Model.GlobalBasis.Y.Normalized().Dot(normal));
			}
		}
		RegressionBench.ReleaseAll();
		RegressionBench.Note($"passos na parede: jogo {clientWall}, boneco {puppetWall} (com o clipe: {puppetClip}); estados {string.Join(", ", states.Select(kv => kv.Key + "=" + kv.Value))}; deitado {lying:0.00}");
		b.AtLeast(60, clientWall, "o jogo correu na parede (passos)");
		b.AtLeast(50, puppetWall, "o boneco soube que está na parede (passos)");
		b.AtLeast(puppetWall - 3, puppetClip, "e tocou o clipe da parede (passos)");
		b.AtLeast(0.9, lying, "deitado na parede");
		puppet.QueueFree();
	}
}
