using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Os movimentos difíceis do lobo na partida autoritativa, um atrás do
/// outro, medindo quanto o servidor corrige o jogo em cada um (Jhon, 25/11:
/// "com movimentos complexos dá uma lagada, volta no tempo"): correr na
/// parede, saltar dela, correr de quatro, o bote, o especial sem alvo
/// (salta para o ponto mirado) e o rugido. Quem vê de outra máquina também
/// entra: o boneco tem de rugir o clipe inteiro (Jhon, 25/11: "a animação
/// do lobo rugindo inimigo não consigo ver, somente os efeitos dela").
///
/// Roda duas vezes: rede limpa (50 ms, nada perdido) e rede oscilando
/// (atraso de 50 a 183 ms, um pacote em cada cinco perdido).
/// </summary>
public sealed class MoveNetTest : WallNetTest
{
	public override string Name => "MovimentosOnline";
	public override string Description => "online (autoritativo): parede, salto da parede, de quatro, bote, especial e rugido — o servidor quase não corrige o jogo, e quem vê de fora vê o rugido inteiro";

	private sealed class Phase
	{
		public string Name;
		public int Fixes, WallApart;
		public float Pulled, Biggest;
		/// <summary>Passos em que o movimento da fase aconteceu, no jogo e no servidor.</summary>
		public int Client, Server;
	}

	private interface ILink
	{
		void Send(uint now, byte[] packet);
		IEnumerable<byte[]> Arrived(uint now);
	}

	private sealed class Clean : ILink
	{
		private readonly FakeLink _link = new() { Latency = 3, DropEvery = 0 };
		public void Send(uint now, byte[] packet) => _link.Send(now, packet);
		public IEnumerable<byte[]> Arrived(uint now) => _link.Arrived(now);
	}

	private sealed class Jitter : ILink
	{
		private readonly JitterLink _link;
		public Jitter(int dropOneIn) => _link = new JitterLink { DropOneIn = dropOneIn };
		public void Send(uint now, byte[] packet) => _link.Send(now, packet);
		public IEnumerable<byte[]> Arrived(uint now) => _link.Arrived(now);
	}

	// Os passos do roteiro (60 por segundo).
	private const int WallFrom = 35, WallJump = 100, Gallop = 184, Dive = 270, SpecialArm = 370, SpecialClick = 382, Howl = 530, End = 680;

	private static uint _seq, _tick;
	private static byte _epoch;

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

		var server = new ServerBody(1, 0, world.SpawnBody("Servidor", Teams.Blue));
		server.ApplyLoadout(Profile.OwnedTattoos());

		// O boneco de quem vê de outra máquina. Não colide: ocupa o mesmo
		// lugar do jogador deste teste.
		var puppet = GD.Load<PackedScene>("res://src/scenes/bot.tscn").Instantiate<Bot>();
		puppet.Name = "Boneco";
		puppet.Team = Teams.Orange;
		puppet.Remote = true;
		b.GetTree().CurrentScene.AddChild(puppet);
		await b.Frames(2);

		_seq = _tick = 0;
		_epoch = 0;
		foreach (bool jitter in new[] { false, true })
		{
			string net = jitter ? "rede oscilando" : "rede limpa";
			await b.Place(start, Mathf.RadToDeg(Mathf.Atan2(-along.X, -along.Z)), settle: 0.6);
			b.Weapon.FillSpecial();
			server.Weapon.FillSpecial();
			_epoch++;
			ILink up = jitter ? new Jitter(5) : new Clean();
			ILink down = jitter ? new Jitter(0) : new Clean();
			var (phases, howl) = await Script(b, p, server, puppet, up, down);

			foreach (Phase f in phases)
			{
				RegressionBench.Note($"{net}, {f.Name}: no jogo {f.Client} passos, no servidor {f.Server}; {f.Fixes} correções (soma {Cm(f.Pulled)} cm, a maior {Cm(f.Biggest)} cm); parede diferente em {f.WallApart} estados");
				b.AtLeast(8, f.Client, $"{net}, {f.Name}: o jogo fez o movimento (passos)");
				b.AtLeast(f.Client * 0.8, f.Server, $"{net}, {f.Name}: o servidor fez junto (passos)");
			}
			RegressionBench.Note($"{net}, rugido: servidor rugiu {howl.ServerSteps} passos ({howl.ServerRuns} vez(es)); o boneco recebeu o rugido em {howl.FlagSteps} passos, ligou e desligou {howl.Toggles} vez(es), clipe ativo {howl.ClipSteps} passos (o rugido inteiro: {howl.Full})");

			int total = phases.Sum(f => f.Fixes);
			float biggest = phases.Max(f => f.Biggest);
			// Limites com folga sobre o medido em 25/11 (veja o CHANGELOG).
			b.AtMost(jitter ? 25 : 6, total, $"{net}: correções no roteiro inteiro");
			b.AtMost(jitter ? 0.5 : 0.15, biggest, $"{net}: a maior correção (m)");
			b.AtLeast(howl.Full * 0.9, howl.ServerSteps, $"{net}: o servidor rugiu o rugido inteiro (passos)");
			b.AtMost(2, howl.Toggles, $"{net}: o rugido do boneco não pisca (liga e desliga uma vez)");
			b.AtLeast(howl.Full * 0.85, howl.ClipSteps, $"{net}: o boneco toca o clipe do rugido até o fim (passos)");
		}

		puppet.QueueFree();
		world.QueueFree();
	}

	private static string Cm(float meters) => (meters * 100f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

	private readonly record struct Howling(int ServerSteps, int ServerRuns, int FlagSteps, int Toggles, int ClipSteps, int Full);

	private static async Task<(List<Phase>, Howling)> Script(RegressionBench b, Player p, ServerBody server, Bot puppet, ILink up, ILink down)
	{
		Player.NetQuantize = true;
		ServerBody.AllowTeleport = true;
		var prediction = new Prediction();
		var recent = new InputCommand[InputCommand.Redundancy];
		var buffer = new byte[256];
		var wallAt = new Dictionary<uint, bool>();
		var phaseAt = new Dictionary<uint, Phase>();
		Vector3 spawn = p.GlobalPosition;
		uint seq = _seq, tick = _tick, syncSeq = 0;
		bool confirmed = false;
		Weapon w = p.GetNode<Weapon>("Weapon");
		var visual = puppet.GetNode<WolfVisual>("WolfVisual");
		var tree = visual.FindChildren("*", nameof(AnimationTree), true, false).OfType<AnimationTree>().FirstOrDefault();

		var phases = new List<Phase>
		{
			new() { Name = "parede" }, new() { Name = "salto da parede" }, new() { Name = "de quatro" },
			new() { Name = "bote" }, new() { Name = "especial sem alvo" }, new() { Name = "rugido" }
		};
		Phase PhaseOf(int step) => step < WallJump ? phases[0] : step < Gallop ? phases[1] : step < Dive ? phases[2]
			: step < SpecialClick ? phases[3] : step < Howl ? phases[4] : phases[5];

		int serverHowl = 0, serverRuns = 0, flagSteps = 0, toggles = 0, clip = 0;
		bool serverWas = false, flagWas = false;
		int full = Mathf.RoundToInt(Player.HowlDuration * 60f);

		for (int step = 0; step < End; step++)
		{
			// Corre na parede e salta dela.
			if (step == 30) RegressionBench.Press("move_forward");
			if (step == WallFrom - 2) RegressionBench.Press("jump");
			if (step == WallFrom) RegressionBench.Release("jump");
			if (step == WallJump) RegressionBench.Press("jump");
			if (step == WallJump + 3) RegressionBench.Release("jump");

			// Vira para trás e corre de quatro (W W).
			if (step == 170)
			{
				RegressionBench.Release("move_forward");
				p.RotationDegrees = new Vector3(0, 90f, 0);
			}
			if (step == 175) RegressionBench.Press("move_forward");
			if (step == 179) RegressionBench.Release("move_forward");
			if (step == Gallop) RegressionBench.Press("move_forward");

			// Para, pula e dá o bote no ar.
			if (step == Dive - 10) RegressionBench.Release("move_forward");
			if (step == Dive) RegressionBench.Press("jump");
			if (step == Dive + 3) RegressionBench.Release("jump");
			if (step == Dive + 17) RegressionBench.Press("special");
			if (step == Dive + 19) RegressionBench.Release("special");

			// Vira de novo e salta o especial para o chão mirado.
			if (step == SpecialArm - 5)
			{
				p.RotationDegrees = new Vector3(0, -90f, 0);
				p.GetNode<Node3D>("Head").Rotation = new Vector3(Mathf.DegToRad(-12f), 0, 0);
			}
			if (step == SpecialArm) RegressionBench.Press("pounce");
			if (step == SpecialArm + 1) RegressionBench.Release("pounce");
			if (step == SpecialClick) RegressionBench.Press("attack");
			if (step == SpecialClick + 1) RegressionBench.Release("attack");

			// Ruge, segurando R até o fim.
			if (step == Howl) RegressionBench.Press("howl");
			if (step == Howl + full + 20) RegressionBench.Release("howl");

			await b.Frames(1);
			tick++;
			seq++;
			Phase phase = PhaseOf(step);
			phaseAt[seq] = phase;

			InputCommand mine = Command(p, seq, !confirmed, spawn);
			mine.Epoch = _epoch;
			NetCommands.Special(ref mine, w, _ => NetCommands.NoSeat);
			recent[seq % InputCommand.Redundancy] = mine;
			prediction.Record(seq, p.GlobalPosition.ToNumerics(), p.Velocity.ToNumerics());
			wallAt[seq] = p.IsWallRunning;
			up.Send(tick, NetCommands.InputPacket(recent, seq, buffer));
			if (tick % 6 == 0) server.ApplyStatus(p.GetNode<Health>("Health").Hp, 0f, "", "", w.SpecialGuarded, w.SpecialCharge);

			foreach (byte[] packet in up.Arrived(tick)) NetCommands.ReadInputPacket(packet, server.Commands);
			server.Step(tick, NetRates.TickSeconds);

			// O movimento de cada fase, nos dois lados.
			Weapon sw = server.Weapon;
			(bool mineDid, bool serverDid) = phases.IndexOf(phase) switch
			{
				0 => (p.IsWallRunning, server.Body.IsWallRunning),
				1 => (p.JumpedFromWall && !p.IsOnFloor(), server.Body.JumpedFromWall && !server.Body.IsOnFloor()),
				2 => (p.IsSprinting && p.IsOnFloor(), server.Body.IsSprinting && server.Body.IsOnFloor()),
				3 => (w.IsDiving, sw.IsDiving),
				4 => (w.IsPounceFlying, sw.IsPounceFlying),
				_ => (p.HowlProgress > 0f, server.Body.HowlProgress > 0f)
			};
			if (mineDid) phase.Client++;
			if (serverDid) phase.Server++;

			bool serverHowling = server.Body.HowlProgress > 0f;
			if (serverHowling) serverHowl++;
			if (serverHowling && !serverWas) serverRuns++;
			serverWas = serverHowling;

			if (tick % NetRates.SnapshotEvery == 0 && server.Placed)
				down.Send(tick, Snapshot(server, server.Commands.LastProcessed));
			foreach (byte[] packet in down.Arrived(tick))
			{
				(uint acked, EntityState e) = ReadSnapshot(packet);
				confirmed = true;

				// Quem vê de fora (NetMatch.UpdatePuppets).
				Form form = Enum.IsDefined(typeof(Form), (int)e.Form) ? (Form)e.Form : Form.Human;
				puppet.RemoteSnapshot(e.Position.ToGodot(), e.Velocity.ToGodot(), Quant.Yaw(e.Yaw), Quant.Pitch(e.Pitch),
					e.Has(EntityFlags.Grounded), form, (Slot)e.Slot);
				puppet.RemoteWallRun = e.Has(EntityFlags.WallRunning);
				bool crouching = e.Has(EntityFlags.Crouching);
				puppet.RemoteMotion(e.Has(EntityFlags.Sprinting), crouching, e.Has(EntityFlags.Howling), crouching, false, e.Has(EntityFlags.Aiming));
				puppet.CollisionLayer = 0;

				if (acked == 0) continue;
				Phase at = phaseAt.GetValueOrDefault(acked, phase);
				if (wallAt.TryGetValue(acked, out bool clientWall) && clientWall != e.Has(EntityFlags.WallRunning)) at.WallApart++;

				// O que o NetMatch faz (Reconcile → SyncWall).
				if (e.Has(EntityFlags.WallRunning) && acked > syncSeq && wallAt.TryGetValue(acked, out bool wasWall) && !wasWall
					&& p.ResumeWallFromServer())
					syncSeq = seq;
				System.Numerics.Vector3 fix = prediction.Reconcile(acked, seq, e.Position);
				if (fix == System.Numerics.Vector3.Zero) continue;
				Vector3 f = fix.ToGodot();
				p.GlobalPosition += f;
				at.Fixes++;
				at.Pulled += f.Length();
				at.Biggest = Mathf.Max(at.Biggest, f.Length());
			}

			if (puppet.RemoteHowling) flagSteps++;
			if (puppet.RemoteHowling != flagWas) toggles++;
			flagWas = puppet.RemoteHowling;
			if (tree != null && tree.Get("parameters/Howl/active").AsBool()) clip++;
		}
		RegressionBench.ReleaseAll();
		Player.NetQuantize = false;
		ServerBody.AllowTeleport = false;
		_seq = seq;
		_tick = tick;
		await b.Until(() => !w.IsRooted && p.HowlProgress <= 0f, 2.0);
		return (phases, new Howling(serverHowl, serverRuns, flagSteps, toggles, clip, full));
	}
}
