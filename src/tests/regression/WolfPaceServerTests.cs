using Godot;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O teto do clique em rajada do lobo no servidor autoritativo (alpha
/// fechado, 26/11: "clique repetido nos ataques do lobo parece não ter limite
/// de resposta"). Um jogo que diz que o lobo dele bateu no mesmo alvo a cada
/// 0,1 s (a garrada) ou deu um bote a cada 0,5 s: o servidor aceita no máximo
/// um golpe por garrada encadeada (0,34 s) e um bote por espera do bote
/// (1 s). Até 26/11 o balde de dano do lobo aceitava 2.000 por segundo: a
/// rajada passava inteira.
/// </summary>
public sealed class WolfPaceServerTest : RegressionTest
{
	public override string Name => "LoboSemSpamNoServidor";
	public override string Description => "no servidor autoritativo, golpes do lobo em rajada no mesmo alvo valem no máximo um a cada 0,34 s, e botes um por segundo";

	private sealed class Link : HostedMatch.IMatchLink
	{
		public void Send(long peer, byte[] packet, bool reliable) { }
		public void Kick(long peer) { }
		public void Report(HostedMatch match) { }
	}

	private const long Wolf = 1, Prey = 2;
	private HostedMatch _match;
	private uint _tick;
	private readonly Dictionary<long, (uint Seq, InputCommand[] Recent, Vector3 Spawn, byte Form)> _players = new();

	public override async Task Run(RegressionBench b)
	{
		var world = new MatchWorld("armazem_estendido");
		b.AddChild(world);
		await b.Frames(2);
		Vector3 home = Ground(world.Space, world.SpawnsFor(Referee.Blue).First());
		Vector3 near = Ground(world.Space, home + new Vector3(1.8f, 0, 0));
		if (near == Vector3.Inf || Mathf.Abs(near.Y - home.Y) > 0.5f) near = Ground(world.Space, home + new Vector3(0, 0, 1.8f));
		b.Check(home != Vector3.Inf && near != Vector3.Inf, "achou chão para os dois");

		var referee = new Referee(new[] { (Wolf, "Lobo", Referee.Blue, 0L), (Prey, "Presa", Referee.Orange, 0L) }, 50, 600, false, countdown: 0);
		_match = new HostedMatch("lobo", referee, new Link(), world);
		_match.Admit(Wolf + 100, Wolf);
		_match.Admit(Prey + 100, Prey);
		for (int i = 0; i < 10 && referee.Phase != RefPhase.Live; i++) referee.Tick(0.5);
		_players[Wolf] = (0, new InputCommand[InputCommand.Redundancy], home + Vector3.Up * 0.2f, (byte)Form.Wolf);
		_players[Prey] = (0, new InputCommand[InputCommand.Redundancy], near + Vector3.Up * 0.2f, (byte)Form.Human);

		// O lobo vira lobo no servidor e a presa perde a proteção de quem nasceu.
		await Steps(b, 220);
		ServerBody wolf = _match.BodyOf(Wolf), prey = _match.BodyOf(Prey);
		b.Check(wolf != null && wolf.Form.IsWolf, "o corpo do lobo está de lobo no servidor");
		b.Check(prey != null && prey.Alive && !prey.Health.IsProtected, "a presa está no mundo, sem proteção");
		if (wolf == null || prey == null) return;
		prey.Health.SetPool(100000f, 0f, true);

		float strike = wolf.Weapon.StrikeSpacing;
		foreach (var (move, every, label) in new[] { (Kill.WolfClawId, 6, "garrada a cada 0,1 s"), (Kill.WolfDiveId, 30, "bote a cada 0,5 s") })
		{
			int before = _match.HitsAccepted, sent = 0;
			int tooFast = _match.HitsRefused.GetValueOrDefault(HitVerdict.TooFast);
			for (int step = 0; step < 240; step++)
			{
				if (step % every == 0)
				{
					Hit(Wolf, Prey, move == Kill.WolfDiveId ? 300f : 120f, wolf.Body.GetNode<Node3D>("Head").GlobalPosition, move);
					sent++;
				}
				await Steps(b, 1);
			}
			await Steps(b, 30);
			int accepted = _match.HitsAccepted - before;
			int refused = _match.HitsRefused.GetValueOrDefault(HitVerdict.TooFast) - tooFast;
			double limit = move == Kill.WolfDiveId ? wolf.Weapon.LeapCooldown : strike;
			RegressionBench.Note($"{label} por 4 s: {sent} mandados, {accepted} aceitos, {refused} recusados por rápido demais (teto: um a cada {RegressionBench.F(limit)} s)");
			b.AtMost(System.Math.Floor(4.0 / limit) + 1, accepted, $"{label}: aceitos em 4 s");
			b.AtLeast(System.Math.Floor(4.0 / limit) - 1, accepted, $"{label}: no ritmo do desenho, valem");
			b.Check(accepted + refused == sent, $"{label}: o que não valeu foi recusado por rápido demais");
		}
		world.QueueFree();
	}

	private async Task Steps(RegressionBench b, int count)
	{
		for (int i = 0; i < count; i++)
		{
			foreach (long account in _players.Keys.ToList())
			{
				var p = _players[account];
				p.Seq++;
				var command = new InputCommand
				{
					Seq = p.Seq, Form = p.Form, HeldSlot = (byte)(p.Form == (byte)Form.Wolf ? Slot.Wolf : Slot.Primary),
					HasSpawn = true, Spawn = p.Spawn.ToNumerics()
				};
				p.Recent[p.Seq % InputCommand.Redundancy] = command;
				_players[account] = p;
				Deliver(account + 100, NetCommands.InputPacket(p.Recent, p.Seq, new byte[256]));
			}
			_match.PhysicsStep(++_tick, 1 / 60.0);
			_match.Tick(1 / 60.0);
			await b.PhysicsFrame();
		}
	}

	/// <summary>O acerto que o jogo do lobo manda, com o comando deste passo.</summary>
	private void Hit(long shooter, long target, float amount, Vector3 from, string move)
	{
		BinaryWriter w = NetWire.Begin(NetWire.Hit, out MemoryStream stream);
		w.Write(target);
		w.Write(amount);
		w.Write(false);
		w.Write((byte)KillCause.Wolf);
		w.Write(move);
		w.Write(from);
		w.Write(_players[shooter].Seq + 1);
		w.Write((double)_tick - NetRates.InterpolationDelay * NetRates.TickRate);
		Deliver(shooter + 100, stream.ToArray());
	}

	private void Deliver(long peer, byte[] packet)
	{
		using BinaryReader r = NetWire.Open(packet);
		byte type = r.ReadByte();
		_match.Receive(peer, type, packet, r);
	}

	private static Vector3 Ground(PhysicsDirectSpaceState3D space, Vector3 at)
	{
		var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(at + Vector3.Up * 1.2f, at + Vector3.Down * 6f));
		return hit.Count == 0 ? Vector3.Inf : (Vector3)hit["position"] + Vector3.Up * 0.9f;
	}
}
