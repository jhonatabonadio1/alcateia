using Godot;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// A própria granada no pé (Jhon, 25/11: "a bomba não está me matando,
/// joguei ela no meu pé"; "quando eu me matei, não apareceu eu me matando e
/// a arma no feed de kills"). Offline: lançada para o chão, ela mata quem
/// lançou e o feed diz "de granada", com ele dos dois lados. Online, o jogo
/// conta ao servidor o que matou (<see cref="Health.LastSelfCause"/>; até
/// 25/11 ia sempre "queda") e o servidor — de repasse e autoritativo —
/// anuncia a morte a todos e a conta, sem pontuar ninguém (até 25/11 o juiz
/// só anunciava abate com autor, e a morte sozinho sumia).
/// </summary>
public sealed class SelfGrenadeTest : RegressionTest
{
	public override string Name => "GranadaNoPe";
	public override string Description => "a própria granada no pé mata e entra no feed com a granada; online, o servidor (repasse e autoritativo) anuncia a morte a todos e a conta, sem ponto para ninguém";

	private sealed class Link : HostedMatch.IMatchLink
	{
		public readonly List<(long Peer, byte[] Packet)> Sent = new();
		public void Send(long peer, byte[] packet, bool reliable) => Sent.Add((peer, packet));
		public void Kick(long peer) { }
		public void Report(HostedMatch match) { }
	}

	public override async Task Run(RegressionBench b)
	{
		// ---- a proteção de quem nasceu não vale contra o próprio dano (Jhon,
		// 25/11: a granada jogada aos pés logo depois de renascer explodia
		// dentro dos 3 s e não fazia nada): morre, renasce, e o dano dos
		// outros não passa mas o próprio, sim.
		b.RemoveBots();
		Health me = b.Health;
		me.RespawnDelay = 0.2f;
		me.TakeDamage(9999f);
		b.Check(await b.Until(() => me.IsDead, 1.0), "morreu para renascer");
		await b.Seconds(0.3);
		for (int i = 0; i < 30 && me.IsDead; i++)
		{
			b.CloseLoadout();
			await b.Seconds(0.3);
		}
		b.Check(!me.IsDead && me.IsProtected, "renasceu, protegido");
		float full = me.Hp;
		me.TakeDamage(50f);
		b.Check(me.Hp == full, "a proteção barra o dano dos outros");
		me.TakeDamage(50f, 0, default, false, ignoreProtection: true);
		b.Check(me.Hp < full, "e não barra o próprio (a granada que se lançou)");

		// ---- offline: lançada no chão, aos pés
		await b.Place(new Vector3(-15, 1, 13), -90f, -85f);
		await b.Until(() => !me.IsProtected, 5.0);
		me.SetPool(me.MaxHp, me.MaxShield, refill: true);
		b.Weapon.Equip(b.Weapon.PrimaryDef, b.Weapon.SecondaryDef, 2);
		await b.Tap("slot_5");
		await b.Seconds(0.6 + b.Weapon.PrimaryDef.HolsterTime);
		int feedBefore = KillFeed.Recent.Count;
		Vector3 feet = b.Player.GlobalPosition;
		RegressionBench.Press("attack");
		await b.Seconds(1.0);
		RegressionBench.Release("attack");
		bool died = await b.Until(() => me.IsDead, 6.0);
		RegressionBench.Note($"granada no pé: {(died ? "morreu" : $"NÃO morreu (vida {me.Hp:0}, escudo {me.Shield:0})")}");
		b.Check(died, "a própria granada no pé mata");
		Kill? last = KillFeed.Recent.Count > feedBefore ? KillFeed.Recent[^1].Kill : null;
		b.Check(last is Kill k && k.Killer == me.PublicName && k.Victim == me.PublicName && k.Cause == KillCause.Grenade && k.Weapon == Arsenal.GrenadeId,
			$"o feed diz: ele mesmo, de granada ({(last is Kill l ? $"{l.Killer} → {l.Victim}, {l.Cause}, '{l.Weapon}'" : "nada")})");
		b.Check(me.LastSelfCause == KillCause.Grenade && me.LastSelfWeapon == Arsenal.GrenadeId,
			"é isso que o jogo conta ao servidor online (a causa e a arma da morte sozinho)");

		// ---- online: o que o servidor faz com a morte que o jogo conta
		await Server(b, null);
		var world = new MatchWorld("armazem_estendido");
		b.AddChild(world);
		await b.Frames(2);
		await Server(b, world);
		world.QueueFree();
	}

	private const long Me = 1, Them = 2, PeerMe = 101, PeerThem = 102;

	private static async Task Server(RegressionBench b, MatchWorld world)
	{
		string mode = world == null ? "repasse" : "autoritativo";
		var referee = new Referee(new[] { (Me, "Eu", Referee.Blue, 0L), (Them, "Outro", Referee.Orange, 0L) }, 50, 600, false, countdown: 0);
		var link = new Link();
		var match = new HostedMatch("granada", referee, link, world);
		match.Admit(PeerMe, Me);
		match.Admit(PeerThem, Them);
		for (int i = 0; i < 10 && referee.Phase != RefPhase.Live; i++) referee.Tick(0.5);

		// Autoritativo: o corpo daqui no mundo (a morte de quem não nasceu não vale).
		if (world != null)
		{
			Vector3 home = world.SpawnsFor(Referee.Blue).First();
			var query = PhysicsRayQueryParameters3D.Create(home + Vector3.Up * 2f, home + Vector3.Down * 6f);
			var hit = world.Space.IntersectRay(query);
			Vector3 spawn = (hit.Count > 0 ? (Vector3)hit["position"] : home) + Vector3.Up * 1.1f;
			var recent = new InputCommand[InputCommand.Redundancy];
			for (uint seq = 1; seq <= 12; seq++)
			{
				recent[seq % InputCommand.Redundancy] = new InputCommand { Seq = seq, HasSpawn = true, Spawn = spawn.ToNumerics() };
				Receive(match, PeerMe, NetCommands.InputPacket(recent, seq, new byte[256]));
				match.PhysicsStep(seq, 1 / 60.0);
				await b.PhysicsFrame();
			}
			b.Check(match.BodyOf(Me)?.Alive == true, $"{mode}: o corpo de quem vai se matar está no mundo do servidor");
		}

		// O que o jogo manda quando se mata com a própria granada (NetMatch.OnLocalDied).
		BinaryWriter w = NetWire.Begin(NetWire.Died, out MemoryStream stream);
		w.Write(0L); w.Write((byte)KillCause.Grenade); w.Write(Arsenal.GrenadeId); w.Write(false); w.Write((byte)0);
		link.Sent.Clear();
		Receive(match, PeerMe, stream.ToArray());

		var kills = link.Sent.Where(s => s.Packet[0] == NetWire.Kill).ToList();
		bool told = kills.Select(s => s.Peer).Distinct().Count() == 2 && kills.All(s => SaysSelfGrenade(s.Packet));
		RegressionBench.Note($"{mode}: {kills.Count} aviso(s) de morte; mortes {referee.Find(Me).Deaths}; placar {referee.Score(Referee.Blue)} x {referee.Score(Referee.Orange)}");
		b.Check(told, $"{mode}: o servidor anuncia a todos a morte sozinho, com a granada");
		b.Check(referee.Find(Me).Deaths == 1 && referee.Find(Me).Kills == 0, $"{mode}: a morte conta, e não como abate");
		b.Check(referee.Score(Referee.Blue) == 0 && referee.Score(Referee.Orange) == 0, $"{mode}: ninguém pontua");
		if (world != null) b.Check(match.BodyOf(Me)?.Alive == false, $"{mode}: o corpo do servidor morre junto");
	}

	private static bool SaysSelfGrenade(byte[] packet)
	{
		using BinaryReader r = NetWire.Open(packet);
		r.ReadByte();
		long killer = r.ReadInt64(), victim = r.ReadInt64();
		var cause = (KillCause)r.ReadByte();
		string weapon = r.ReadString();
		return killer == Me && victim == Me && cause == KillCause.Grenade && weapon == Arsenal.GrenadeId;
	}

	private static void Receive(HostedMatch match, long peer, byte[] packet)
	{
		using BinaryReader r = NetWire.Open(packet);
		byte type = r.ReadByte();
		match.Receive(peer, type, packet, r);
	}
}
