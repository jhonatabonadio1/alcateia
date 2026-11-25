using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O lobo encostando num bot na partida autoritativa (Jhon, 25/11: "com
/// movimentos complexos dá uma lagada, volta no tempo"). O bot é movido
/// pela máquina do dono e o servidor não tinha corpo para ele: no jogo o
/// lobo batia na cápsula do bot, no servidor atravessava, e a correção
/// puxava o lobo para dentro do bot. Agora o servidor põe uma cápsula onde
/// o dono diz que o bot está (<see cref="BotProxies"/>). E o aliado, que o
/// jogo atravessa (TeamCollision), também é atravessado no servidor.
///
/// Cada caso roda sem as cápsulas (como antes de 25/11, só para medir) e
/// com elas: andar até o bot e empurrar, arranhar encostado e dar o bote
/// nele.
/// </summary>
public sealed class BotBodyNetTest : WallNetTest
{
	public override string Name => "EncostaNoBotOnline";
	public override string Description => "online (autoritativo): o lobo que encosta, arranha e dá o bote num bot bate nele no servidor como no jogo; o aliado é atravessado nos dois";

	public override void Prepare()
	{
		base.Prepare();
		RoomConfig.Members.Add(new RoomMember { Name = "Inimigo", Team = Teams.Orange, IsBot = true });
		RoomConfig.Members.Add(new RoomMember { Name = "Amigo", Team = Teams.Blue, IsBot = true });
	}

	private const long EnemyId = -10, AllyId = -11;
	private static uint _seq, _tick;
	private static byte _epoch;

	public override async Task Run(RegressionBench b)
	{
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live && b.Bots().Length >= 2, 30);
		await b.BecomeWolf();
		Player p = b.Player;
		int mine = b.Health.Team;
		CharacterBody3D enemy = b.Bots()[0], ally = b.Bots()[1];
		foreach (CharacterBody3D bot in new[] { enemy, ally })
		{
			RegressionBench.Freeze(bot);
			bot.GetNode<Health>("Health").SetPool(5000f, 0f, refill: true);
		}
		enemy.GetNode<Health>("Health").Team = Teams.Other(mine);
		ally.GetNode<Health>("Health").Team = mine;
		await b.Frames(2);

		MatchWorld world = await StartServer(b);
		if (!PlaceTestWall(p, b.GetTree().CurrentScene, world, out Vector3 start, out Vector3 along, out _))
		{
			b.Check(false, "achou lugar para o pátio de teste");
			return;
		}
		// Longe da parede do pátio: aqui só conta o bot no caminho.
		start += new Vector3(0, 0, 6f);

		var server = new ServerBody(1, 0, world.SpawnBody("Servidor", mine));
		server.ApplyLoadout(Profile.Breed, Profile.OwnedTattoos());

		// Dois corpos do mesmo time no servidor atravessam um ao outro, como
		// os bonecos no jogo.
		var friend = new ServerBody(2, 1, world.SpawnBody("Aliado", mine));
		server.Step(0, NetRates.TickSeconds);
		friend.Step(0, NetRates.TickSeconds);
		b.Check(server.Body.GetCollisionExceptions().Contains(friend.Body), "no servidor, dois corpos do mesmo time atravessam um ao outro");
		friend.Body.QueueFree();

		_seq = _tick = 0;
		_epoch = 0;
		foreach (bool isEnemy in new[] { true, false })
		{
			CharacterBody3D inWay = isEnemy ? enemy : ally, aside = isEnemy ? ally : enemy;
			string who = isEnemy ? "inimigo" : "aliado";
			foreach (bool proxies in new[] { false, true })
			{
				inWay.GlobalPosition = start + along * 4f;
				aside.GlobalPosition = start + along * 4f + new Vector3(0, 0, 8f);
				await b.Place(start, Mathf.RadToDeg(Mathf.Atan2(-along.X, -along.Z)), settle: 0.5);
				_epoch++;
				var bots = proxies ? new BotProxies(world) : null;
				var r = await Push(b, p, server, bots, inWay, aside, isEnemy);
				bots?.Clear();

				string what = $"{who} no caminho, {(proxies ? "com" : "sem")} a cápsula no servidor";
				RegressionBench.Note($"{what}: {r.Fixes} correções (soma {RegressionBench.F(r.Pulled)} m, a maior {RegressionBench.F(r.Biggest)} m); no fim, jogo e servidor a {RegressionBench.F(r.Apart)} m; o lobo do jogo chegou a {RegressionBench.F(r.Reach)} m do começo, o do servidor a {RegressionBench.F(r.ServerReach)} m");
				if (!proxies) continue;
				b.AtMost(2, r.Fixes, $"{what}: o servidor quase não corrige o jogo");
				b.AtMost(0.1, r.Biggest, $"{what}: a maior correção (m)");
				b.AtMost(0.1, r.Apart, $"{what}: jogo e servidor no mesmo lugar no fim (m)");
				if (isEnemy) b.AtMost(4f, r.Reach, $"{what}: o inimigo segura o lobo (m)");
				else b.AtLeast(4.5f, r.Reach, $"{what}: o lobo atravessa o aliado (m)");
			}
		}

		world.QueueFree();
	}

	private readonly record struct Result(int Fixes, float Pulled, float Biggest, float Apart, float Reach, float ServerReach);

	private static async Task<Result> Push(RegressionBench b, Player p, ServerBody server, BotProxies bots, CharacterBody3D inWay, CharacterBody3D aside, bool isEnemy)
	{
		Player.NetQuantize = true;
		ServerBody.AllowTeleport = true;
		var up = new FakeLink { Latency = 3, DropEvery = 0 };
		var down = new FakeLink { Latency = 3, DropEvery = 0 };
		var prediction = new Prediction();
		var recent = new InputCommand[InputCommand.Redundancy];
		var buffer = new byte[256];
		Vector3 spawn = p.GlobalPosition, from = p.GlobalPosition;
		Vector3 line = (inWay.GlobalPosition - from) with { Y = 0 };
		line = line.Normalized();
		uint seq = _seq, tick = _tick;
		bool confirmed = false;
		int fixes = 0;
		float pulled = 0f, biggest = 0f, reach = 0f, serverReach = 0f;

		for (int step = 0; step < 240; step++)
		{
			// Anda até o bot e empurra, arranhando; depois para, pula e dá o bote nele.
			if (step == 10) RegressionBench.Press("move_forward");
			if (step is 70 or 90 or 110) RegressionBench.Press("attack");
			if (step is 71 or 91 or 111) RegressionBench.Release("attack");
			if (step == 130) RegressionBench.Release("move_forward");
			if (step == 140) RegressionBench.Press("jump");
			if (step == 143) RegressionBench.Release("jump");
			if (step == 157) RegressionBench.Press("special");
			if (step == 159) RegressionBench.Release("special");

			await b.Frames(1);
			tick++;
			seq++;
			InputCommand c = Command(p, seq, !confirmed, spawn);
			c.Epoch = _epoch;
			recent[seq % InputCommand.Redundancy] = c;
			prediction.Record(seq, p.GlobalPosition.ToNumerics(), p.Velocity.ToNumerics());
			up.Send(tick, NetCommands.InputPacket(recent, seq, buffer));

			// O dono dos bots conta onde eles estão (NetWire.StateRate, 30 por
			// segundo). Parados: o bot congelado guarda a velocidade de antes.
			if (bots != null && tick % 2 == 0)
			{
				bots.Report(EnemyId, isEnemy ? Teams.Other(server.Health.Team) : server.Health.Team, inWay.GlobalPosition, Vector3.Zero, false);
				bots.Report(AllyId, isEnemy ? server.Health.Team : Teams.Other(server.Health.Team), aside.GlobalPosition, Vector3.Zero, false);
			}
			bots?.Step(NetRates.TickSeconds);

			foreach (byte[] packet in up.Arrived(tick)) NetCommands.ReadInputPacket(packet, server.Commands);
			server.Step(tick, NetRates.TickSeconds);

			if (tick % NetRates.SnapshotEvery == 0 && server.Placed)
				down.Send(tick, Snapshot(server, server.Commands.LastProcessed));
			foreach (byte[] packet in down.Arrived(tick))
			{
				(uint acked, EntityState e) = ReadSnapshot(packet);
				confirmed = true;
				if (acked == 0) continue;
				System.Numerics.Vector3 fix = prediction.Reconcile(acked, seq, e.Position);
				if (fix == System.Numerics.Vector3.Zero) continue;
				Vector3 f = fix.ToGodot();
				p.GlobalPosition += f;
				fixes++;
				pulled += f.Length();
				biggest = Mathf.Max(biggest, f.Length());
			}
			reach = Mathf.Max(reach, (p.GlobalPosition - from).Dot(line));
			if (server.Placed && server.Epoch == _epoch) serverReach = Mathf.Max(serverReach, (server.Body.GlobalPosition - from).Dot(line));
		}
		RegressionBench.ReleaseAll();
		Player.NetQuantize = false;
		ServerBody.AllowTeleport = false;
		_seq = seq;
		_tick = tick;
		Weapon w = p.GetNode<Weapon>("Weapon");
		await b.Until(() => !w.IsRooted && !w.IsDiving, 2.0);
		return new Result(fixes, pulled, biggest, RegressionBench.Flat(p.GlobalPosition - server.Body.GlobalPosition), reach, serverReach);
	}
}
