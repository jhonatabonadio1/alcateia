using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O especial do lobo na partida autoritativa (Jhon, 25/11, jogando no Docker
/// com o servidor de partida simulando os corpos: "o lobo não avança e cai e
/// fica, ele fica indo e voltando como se desse conflito com algum objeto";
/// "avança no inimigo, mata (ou não) e volta para trás ao invés de ficar no
/// local do inimigo"; "os outros inimigos avançando com especial em mim fica
/// perfeito; no primeira pessoa que buga").
///
/// O jogo prevê o próprio salto; o servidor dá o mesmo passo com os comandos
/// que chegam, e a diferença vira correção no jogo. Aqui o servidor de
/// partida roda dentro do teste (o mesmo <see cref="MatchWorld"/> e
/// <see cref="ServerBody"/>), com a rede oscilando, e o lobo dá o especial
/// como o jogador: Q arma, a trava pega o inimigo no centro da tela, o
/// clique salta. Conta as correções e o quanto puxaram, e onde o lobo fica
/// no fim — no jogo e no servidor.
/// </summary>
public sealed class SpecialNetTest : WallNetTest
{
	public override string Name => "EspecialOnline";
	public override string Description => "online (autoritativo, rede oscilando): o especial travado voa no servidor como no jogo — o lobo cai colado no alvo nos dois, sem o jogo ser puxado para trás no ar nem no fim";

	public override void Prepare()
	{
		base.Prepare();
		RoomConfig.Members.Add(new RoomMember { Name = "Alvo", Team = Teams.Orange, IsBot = true });
	}

	public override async Task Run(RegressionBench b)
	{
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live && b.Bots().Length > 0, 30);
		var target = b.Bots()[0];
		RegressionBench.Freeze(target);
		await b.BecomeWolf();
		Player p = b.Player;
		Weapon w = b.Weapon;

		MatchWorld world = await StartServer(b);
		var server = new ServerBody(1, 0, world.SpawnBody("Servidor", Teams.Blue));
		server.ApplyLoadout(Profile.OwnedWolfUpgrades());
		var them = new ServerBody(2, 1, world.SpawnBody("Alvo", Teams.Orange));
		them.Health.Team = Teams.Other(b.Health.Team);
		target.GetNode<Health>("Health").Team = Teams.Other(b.Health.Team);
		server.Resolve = index => index == 1 ? them.Body : null;

		// Um lugar aberto do mapa com chão livre até 12 m numa direção.
		Vector3 origin = p.GlobalPosition, dir = Vector3.Zero;
		var starts = new List<Vector3> { p.GlobalPosition };
		starts.AddRange(SpawnPoints.PointsFor(b.Health.Team).Concat(SpawnPoints.PointsFor(Teams.Other(b.Health.Team))).Select(s => s + Vector3.Up * 0.9f));
		foreach (Vector3 start in starts)
		{
			foreach (Vector3 d in new[] { Vector3.Forward, Vector3.Back, Vector3.Left, Vector3.Right, new Vector3(1, 0, 1).Normalized(), new Vector3(-1, 0, 1).Normalized(), new Vector3(1, 0, -1).Normalized(), new Vector3(-1, 0, -1).Normalized() })
				if (OpenLine(p, start, start + d * 12f) && OpenLine(p, start, start + d * 8f)) { origin = start; dir = d; break; }
			if (dir != Vector3.Zero) break;
		}
		b.Check(dir != Vector3.Zero, "achou chão livre de 12 m para saltar");
		if (dir == Vector3.Zero) return;

		byte epoch = 0;
		_seq = _tick = _theirSeq = 0;
		// O terceiro caso é o alvo bot: na partida autoritativa o bot não tem
		// corpo no servidor (a máquina do dono o move, HostedMatch.AddBody só
		// cria o de quem tem conta); o servidor só sabe onde o jogo o via.
		foreach (var (distance, bot) in new[] { (8f, false), (12f, false), (8f, true) })
		{
			if (bot)
			{
				server.Resolve = _ => null;
				them.Body.GlobalPosition += Vector3.Up * 300f;
			}
			// O lobo e o alvo de frente um para o outro.
			Vector3 from = origin;
			Vector3 to = from + dir * distance;
			target.GlobalPosition = to;
			await b.Place(from, Mathf.RadToDeg(Mathf.Atan2(-(to - from).X, -(to - from).Z)), -6f, settle: 0.5);
			var health = target.GetNode<Health>("Health");
			health.SetPool(5000f, 0f, refill: true);
			await b.Until(() => !health.IsProtected, 4.0);
			w.SpecialFree = false;
			w.FillSpecial();
			// A carga que o servidor também deu (os abates que ele viu).
			server.Weapon.FillSpecial();
			epoch++;

			var r = await Leap(b, p, w, server, bot ? null : them, target, to, epoch);
			string what = bot ? $"alvo bot a {distance:0} m" : $"alvo a {distance:0} m";
			RegressionBench.Note($"{what}: {(r.Locked ? "travado" : "SEM TRAVA")}; correções no jogo {r.Fixes} (soma {RegressionBench.F(r.Pulled)} m, a maior {RegressionBench.F(r.Biggest)} m), {r.Back} quadros puxado para trás; no fim, o jogo a {RegressionBench.F(r.ClientGap)} m do alvo e o servidor a {RegressionBench.F(r.ServerGap)} m; servidor e jogo a {RegressionBench.F(r.Apart)} m um do outro; o servidor {(r.ServerFlew ? "voou" : "NÃO voou")}");
			b.Check(r.Locked, $"{what}: o jogo travou no alvo");
			b.Check(r.ServerFlew, $"{what}: o servidor também fez o salto");
			b.AtMost(1.5, r.ClientGap, $"{what}: no jogo, o lobo fica colado no alvo (m)");
			b.AtMost(1.5, r.ServerGap, $"{what}: no servidor também (m)");
			b.AtMost(0.25, r.Apart, $"{what}: no fim, jogo e servidor no mesmo lugar (m)");
			b.AtMost(0.15, r.Biggest, $"{what}: nenhuma correção grande no jogo (a maior, m)");
			b.Near(0, r.Back, 0.01, $"{what}: o jogo não é puxado para trás");
		}

		Player.NetQuantize = false;
		world.QueueFree();
	}

	private readonly record struct Result(bool Locked, bool ServerFlew, int Fixes, float Pulled, float Biggest, int Back, float ClientGap, float ServerGap, float Apart);

	/// <summary>Os números do comando seguem de um caso para o outro: o
	/// servidor descarta comando com número que já passou.</summary>
	private static uint _seq, _tick, _theirSeq;

	private static async Task<Result> Leap(RegressionBench b, Player p, Weapon w, ServerBody server, ServerBody them, CharacterBody3D target, Vector3 to, byte epoch)
	{
		Player.NetQuantize = true;
		var up = new JitterLink();
		var down = new JitterLink { DropOneIn = 0 };
		var prediction = new Prediction();
		var recent = new InputCommand[InputCommand.Redundancy];
		var theirs = new InputCommand[InputCommand.Redundancy];
		var buffer = new byte[256];
		Vector3 spawn = p.GlobalPosition;
		uint seq = _seq, tick = _tick, theirSeq = _theirSeq;
		bool confirmed = false, locked = false, serverFlew = false;
		int fixes = 0, back = 0;
		float pulled = 0f, biggest = 0f;
		Vector3 line = (to - p.GlobalPosition) with { Y = 0 };
		line = line.Normalized();
		Weapon serverWeapon = server.Weapon;
		ServerBody.AllowTeleport = true;

		const int Arm = 40, Click = 52, End = 52 + 150;
		for (int step = 0; step < End; step++)
		{
			if (step == Arm) RegressionBench.Press("pounce");
			if (step == Arm + 1) RegressionBench.Release("pounce");
			if (step == Click)
			{
				locked = p.LockTarget == target;
				RegressionBench.Press("attack");
			}
			if (step == Click + 1) RegressionBench.Release("attack");

			Vector3 before = p.GlobalPosition;
			await b.Frames(1);
			tick++;

			seq++;
			InputCommand mine = Command(p, seq, !confirmed, spawn);
			// Cada caso é uma vida nova: o servidor põe o corpo onde o jogo está.
			mine.Epoch = epoch;
			// O alvo travado vai pelo lugar dele na escalação (aqui, o 1).
			// (ESPECIAL_SEM_ALVO=1: como antes de 25/11, sem o alvo nem o ponto no comando.)
			if (OS.GetEnvironment("ESPECIAL_SEM_ALVO") != "1")
				NetCommands.Special(ref mine, w, body => body == target ? (byte)1 : NetCommands.NoSeat);
			recent[seq % InputCommand.Redundancy] = mine;
			prediction.Record(seq, p.GlobalPosition.ToNumerics(), p.Velocity.ToNumerics());
			up.Send(tick, NetCommands.InputPacket(recent, seq, buffer));

			// O alvo, parado onde está, como outro jogador mandaria.
			if (them != null)
			{
				theirSeq++;
				var still = new InputCommand { Seq = theirSeq, HasSpawn = true, Spawn = to.ToNumerics(), Epoch = epoch };
				theirs[theirSeq % InputCommand.Redundancy] = still;
				NetCommands.ReadInputPacket(NetCommands.InputPacket(theirs, theirSeq, buffer), them.Commands);
				them.Step(tick, NetRates.TickSeconds);
			}

			foreach (byte[] packet in up.Arrived(tick)) NetCommands.ReadInputPacket(packet, server.Commands);
			server.Step(tick, NetRates.TickSeconds);
			if (serverWeapon.IsPounceFlying) serverFlew = true;
			if (tick % 6 == 0) server.ApplyStatus(p.GetNode<Health>("Health").Hp, 0f, "", "", w.SpecialGuarded, w.SpecialCharge);

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
				if (step >= Click)
				{
					fixes++;
					pulled += f.Length();
					biggest = Mathf.Max(biggest, f.Length());
					if ((f with { Y = 0 }).Dot(line) < -0.05f) back++;
				}
			}
			if (step > Click && ((p.GlobalPosition - before) with { Y = 0 }).Dot(line) < -0.05f) back++;
		}
		RegressionBench.ReleaseAll();
		ServerBody.AllowTeleport = false;
		_seq = seq;
		_tick = tick;
		_theirSeq = theirSeq;
		await b.Until(() => !w.IsRooted, 2.0);

		return new Result(locked, serverFlew, fixes, pulled, biggest, back,
			RegressionBench.Flat(to - p.GlobalPosition), RegressionBench.Flat(to - server.Body.GlobalPosition),
			RegressionBench.Flat(p.GlobalPosition - server.Body.GlobalPosition));
	}

	/// <summary>Chão e caminho livres entre os dois pontos.</summary>
	private static bool OpenLine(Player p, Vector3 from, Vector3 to)
	{
		var space = p.GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(from + Vector3.Up * 0.3f, to + Vector3.Up * 0.3f);
		query.Exclude = new Godot.Collections.Array<Rid> { p.GetRid() };
		if (space.IntersectRay(query).Count > 0) return false;
		var down = PhysicsRayQueryParameters3D.Create(to + Vector3.Up, to + Vector3.Down * 3f);
		var floor = space.IntersectRay(down);
		return floor.Count > 0 && Mathf.Abs(((Vector3)floor["position"]).Y - (from.Y - 0.9f)) < 0.3f;
	}
}
