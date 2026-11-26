using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Vida e acerto no servidor de partida (decisão 0016): uma partida
/// autoritativa de verdade (<see cref="HostedMatch"/> com o
/// <see cref="MatchWorld"/> do Armazém), três jogadores movidos por
/// comandos, e acertos mandados como o jogo manda — os honestos e os de
/// trapaça. O servidor aplica o dano no corpo dele, decide a morte, não
/// deixa o jogo subir a própria vida nem renascer antes da hora ou fora
/// da base.
/// </summary>
public sealed class ServerHealthTest : RegressionTest
{
	public override string Name => "VidaNoServidor";
	public override string Description => "o servidor confere o acerto (parede, alcance, mira, ritmo, arma, origem), aplica o dano, decide a morte; o jogo não sobe a vida nem renasce antes da hora";

	private sealed class Link : HostedMatch.IMatchLink
	{
		public readonly List<(long Peer, byte[] Packet)> Sent = new();
		public void Send(long peer, byte[] packet, bool reliable) => Sent.Add((peer, packet));
		public void Kick(long peer) { }
		public void Report(HostedMatch match) { }
		public int Count(long peer, byte type) => Sent.Count(s => s.Peer == peer && s.Packet[0] == type);
	}

	private const long Shooter = 1, Clear = 2, Hidden = 3;
	private const long PeerShooter = 101, PeerClear = 102, PeerHidden = 103;

	private HostedMatch _match;
	private double _worstStepMs;
	private uint _tick;
	private readonly Dictionary<long, (uint Seq, InputCommand[] Recent, byte Epoch, Vector3 Spawn, float Yaw, float Pitch)> _input = new();

	public override async Task Run(RegressionBench b)
	{
		var world = new MatchWorld("armazem_estendido");
		b.AddChild(world);
		await b.Frames(2);
		PhysicsDirectSpaceState3D space = world.Space;

		// Onde cada um fica: o atirador numa base, um alvo à vista dele e
		// outro atrás de alguma coisa sólida (o servidor acha os dois).
		Vector3 a = Floor(space, world.SpawnsFor(Referee.Blue).First());
		b.Check(world.Spawns.Count > 0, $"o mundo do servidor guardou os pontos de nascimento ({world.Spawns.Count})");
		Vector3 eye = a + Vector3.Up * 0.6f;
		Vector3? seen = null, behind = null;
		for (int ring = 6; ring <= 22 && (seen == null || behind == null); ring += 4)
			for (int i = 0; i < 24; i++)
			{
				float angle = Mathf.Tau * i / 24f;
				Vector3 at = a + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * ring;
				Vector3 floor = Floor(space, at);
				if (floor == Vector3.Inf || Mathf.Abs(floor.Y - a.Y) > 0.6f) continue;
				bool clear = Ray(space, eye, floor + Vector3.Up * 0.3f) && Ray(space, eye, floor + Vector3.Up * 0.75f);
				bool blocked = !Ray(space, eye, floor + Vector3.Up * 0.75f) && !Ray(space, eye, floor) && !Ray(space, eye, floor + Vector3.Up * 0.3f)
					&& !Ray(space, eye + Vector3.Up * 0.3f, floor + Vector3.Up * 0.6f);
				if (clear) seen ??= floor;
				else if (blocked) behind ??= floor;
			}
		b.Check(seen != null && behind != null, $"achou um lugar à vista e um atrás da parede ({seen}, {behind})");
		if (seen == null || behind == null) return;

		var referee = new Referee(new[]
		{
			(Shooter, "Atirador", Referee.Blue, 0L), (Clear, "Alvo", Referee.Orange, 0L), (Hidden, "Escondido", Referee.Orange, 0L)
		}, 50, 600, false, countdown: 0);
		var link = new Link();
		_match = new HostedMatch("teste", referee, link, world);
		_match.Admit(PeerShooter, Shooter);
		_match.Admit(PeerClear, Clear);
		_match.Admit(PeerHidden, Hidden);
		for (int i = 0; i < 10 && referee.Phase != RefPhase.Live; i++) referee.Tick(0.5);
		b.Check(referee.Phase == RefPhase.Live, "a partida está valendo");

		Aim(Shooter, a, seen.Value);
		Aim(Clear, seen.Value, a);
		Aim(Hidden, behind.Value, a);
		Status(PeerShooter, 200, "m4a4");
		Status(PeerClear, 200, "m4a4");
		Status(PeerHidden, 200, "m4a4");
		await Steps(b, 40);

		ServerBody target = Body(world, Clear), hidden = Body(world, Hidden);
		b.Check(target.Alive && hidden.Alive, "os três estão no mundo do servidor");
		WeaponDefinition m4 = Arsenal.Get("m4a4");
		Vector3 from = Eye(world, Shooter);
		GD.Print($"VidaNoServidor: atirador em {a}, alvo em {seen}, escondido em {behind}; nascimentos: " +
			string.Join(" ", world.Spawns.Select(sp => $"{sp.Team}:{sp.Point}")));

		// 1. Honesto: à vista, mirando, a arma na mão.
		// O jogo manda o dano como o calculou: com a escala das armas de fogo (CombatRules.Damage).
		float before = target.Health.Hp;
		float honest = m4.Damage * CombatRules.FirearmDamageScale;
		Hit(Shooter, Clear, honest, from, "m4a4");
		await Steps(b, 3);
		b.Check(_match.HitsAccepted == 1 && target.Health.Hp < before, $"o acerto honesto valeu: a vida caiu no servidor ({before} → {target.Health.Hp})");
		// Até 25/11 o teto do servidor esquecia a escala e cortava a bala honesta (54 → 29 no AR-15).
		b.Check(Mathf.Abs(before - target.Health.Hp - honest) < 0.5f, $"e valeu inteiro, como no jogo ({before - target.Health.Hp:0.#} de {honest:0.#})");
		b.Check(link.Count(PeerClear, NetWire.Damage) == 1, "e a vítima recebeu como ficou (Damage)");

		// 2. Através da parede: mira no escondido, que está atrás de algo sólido.
		var onClear = _input[Shooter];
		Aim(Shooter, a, behind.Value);
		_input[Shooter] = onClear with { Yaw = _input[Shooter].Yaw, Pitch = _input[Shooter].Pitch };
		await Steps(b, 3);
		Hit(Shooter, Hidden, m4.Damage, Eye(world, Shooter), "m4a4");
		await Steps(b, 15);
		b.Check(Refused(HitVerdict.Blocked) == 1 && hidden.Health.Hp >= hidden.Health.MaxHp, "através da parede: recusado, e a vida do escondido não mudou");
		_input[Shooter] = _input[Shooter] with { Yaw = onClear.Yaw, Pitch = onClear.Pitch };
		await Steps(b, 3);

		// 2b. A granada: parede protege no servidor também (Jhon, 26/11: "área de
		// efeito da granada atravessa paredes"). Até 26/11 o servidor não conferia
		// visão para a explosão. Uma explosão perto do escondido, do outro lado de
		// algo sólido: recusada; uma perto do alvo à vista: vale.
		Vector3? blockedAt = GrenadeSpot(space, hidden.Body.GlobalPosition, from, wantClear: false);
		Vector3? clearAt = GrenadeSpot(space, target.Body.GlobalPosition, from, wantClear: true);
		b.Check(blockedAt != null && clearAt != null, $"achou onde estourar: atrás de algo, perto do escondido ({blockedAt}), e à vista do alvo ({clearAt})");
		if (blockedAt != null && clearAt != null)
		{
			int blockedBefore = Refused(HitVerdict.Blocked), acceptedBefore = _match.HitsAccepted;
			float hiddenHp = hidden.Health.Hp;
			Hit(Shooter, Hidden, 100, blockedAt.Value, "choque", cause: KillCause.Grenade);
			await Steps(b, 15);
			b.Check(Refused(HitVerdict.Blocked) == blockedBefore + 1 && hidden.Health.Hp >= hiddenHp,
				$"granada do outro lado da parede: recusada, a vida do escondido não mudou ({hiddenHp:0} → {hidden.Health.Hp:0})");
			float targetHp = target.Health.Hp;
			Hit(Shooter, Clear, 40, clearAt.Value, "choque", cause: KillCause.Grenade);
			await Steps(b, 15);
			b.Check(_match.HitsAccepted == acceptedBefore + 1 && target.Health.Hp < targetHp,
				$"granada à vista do alvo: vale ({targetHp:0} → {target.Health.Hp:0})");
		}

		// 3. De onde o corpo não estava.
		Hit(Shooter, Clear, m4.Damage, from + new Vector3(0, 40, 0), "m4a4");
		await Steps(b, 15);
		b.Check(Refused(HitVerdict.BadOrigin) == 1, "o tiro saindo de 40 m acima do corpo: recusado");

		// 4. Arma que não estava na mão (a mais forte).
		Hit(Shooter, Clear, 90, from, "svd");
		await Steps(b, 15);
		b.Check(Refused(HitVerdict.WrongWeapon) == 1, "com a SVD, segurando a M4A4: recusado");

		// 5. Olhando para o outro lado.
		float realYaw = _input[Shooter].Yaw;
		_input[Shooter] = _input[Shooter] with { Yaw = realYaw + Mathf.Pi };
		await Steps(b, 2);
		Hit(Shooter, Clear, m4.Damage, Eye(world, Shooter), "m4a4");
		await Steps(b, 15);
		b.Check(Refused(HitVerdict.BadAim) == 1, "de costas para o alvo: recusado");
		_input[Shooter] = _input[Shooter] with { Yaw = realYaw };
		await Steps(b, 3);

		// 6. Dano acima do que a arma dá: vale o máximo dela.
		float hp = target.Health.Hp;
		Hit(Shooter, Clear, 900, Eye(world, Shooter), "m4a4", headshot: false);
		await Steps(b, 15);
		float dealt = hp - target.Health.Hp;
		b.Check(dealt > 0 && dealt <= (m4.Damage * CombatRules.FirearmDamageScale + 1) * 1.06f, $"900 de dano com a M4A4 valem só o máximo da arma ({dealt:0.#})");

		// 7. O jogo não sobe a própria vida: diz que está cheio, o servidor não aceita.
		hp = target.Health.Hp;
		Status(PeerClear, 200, "m4a4");
		b.Check(Mathf.IsEqualApprox(target.Health.Hp, hp), $"o jogo dizendo vida cheia não cura no servidor ({target.Health.Hp:0.#})");
		Status(PeerClear, hp - 20, "m4a4");
		b.Check(target.Health.Hp <= hp - 19.9f, "mas a vida que desce (uma queda que só ele viu) vale");

		// 8. Mais rápido que a arma: uma rajada de 40 acertos no mesmo instante
		// (com vida de sobra, para o alvo não cair antes do teto).
		target.Health.Mirror(10000, 10000, 0, 0, false);
		int refusedFast = Refused(HitVerdict.TooFast);
		_worstStepMs = 0;
		for (int i = 0; i < 40; i++) Hit(Shooter, Clear, m4.Damage, Eye(world, Shooter), "m4a4");
		await Steps(b, 15);
		GD.Print($"VidaNoServidor: o passo que conferiu 40 acertos levou {_worstStepMs:0.00} ms");
		b.AtMost(8, _worstStepMs, "conferir 40 acertos de uma vez cabe num passo (ms)");
		b.Check(Refused(HitVerdict.TooFast) - refusedFast >= 30, $"40 tiros da M4A4 no mesmo instante: o ritmo recusa quase todos ({Refused(HitVerdict.TooFast) - refusedFast} recusados)");
		target.Health.Mirror(5, 200, 0, 0, false);

		// 9. A morte é do servidor.
		await Steps(b, 40);   // o balde enche de novo
		Hit(Shooter, Clear, m4.Damage, Eye(world, Shooter), "m4a4");
		await Steps(b, 15);
		b.Check(target.Health.IsDead, "o tiro que zerou a vida matou no servidor");
		b.Check(referee.Find(Shooter).Kills == 1 && referee.Find(Clear).Deaths == 1, "o abate é do atirador");
		b.Check(link.Count(PeerShooter, NetWire.Kill) == 1 && link.Count(PeerClear, NetWire.Kill) == 1, "e foi anunciado a todos");
		DiedPacket(PeerClear, Shooter);
		b.Check(referee.Find(Clear).Deaths == 1, "o jogo da vítima contando a morte de novo não conta duas vezes");

		// 10. Renascer: não antes da hora, não fora da base.
		byte epoch = (byte)(_input[Clear].Epoch + 1);
		Vector3 home = Floor(space, world.SpawnsFor(Referee.Orange).First()) + Vector3.Up * 0.2f;
		_input[Clear] = _input[Clear] with { Epoch = epoch, Spawn = home };
		await Steps(b, 10);
		b.Check(target.Health.IsDead && target.RefusedSpawns > 0, "renascer logo depois de morrer: recusado");

		// Os comandos da tentativa acima que ainda estão na fila saem antes
		// da espera acabar (voltam à vida em que morreu).
		_input[Clear] = _input[Clear] with { Epoch = (byte)(epoch - 1) };
		await Steps(b, 6);
		await b.Seconds(target.Health.RespawnWait - 0.5);
		// Longe de todo ponto de nascimento do time dele.
		Vector3 away = new[] { a, seen.Value, behind.Value }
			.OrderByDescending(p => world.SpawnsFor(Referee.Orange).Min(s => s.DistanceTo(p))).First();
		float awayDistance = world.SpawnsFor(Referee.Orange).Min(s => s.DistanceTo(away));
		_input[Clear] = _input[Clear] with { Epoch = (byte)(epoch + 1), Spawn = away };
		await Steps(b, 10);
		b.Check(awayDistance < ServerBody.SpawnReach + 1 || target.Health.IsDead,
			$"passada a espera, renascer a {awayDistance:0} m da base do time: recusado (pontos do time {target.SpawnPoints.Count}, recusas {target.RefusedSpawns}, espera {target.Health.RespawnIn:0.00}, época {target.Epoch})");

		_input[Clear] = _input[Clear] with { Epoch = (byte)(epoch + 2), Spawn = home };
		await Steps(b, 10);
		b.Check(target.Alive && target.Health.IsProtected, "na base do próprio time: renasce, protegido");
		b.Check(referee.Find(Shooter).RejectedHits >= 4, $"as recusas contam contra o atirador ({referee.Find(Shooter).RejectedHits})");
		b.Check(_match.Result("teste")["players"].AsArray().Any(p => p["rejectedHits"]?.GetValue<int>() > 0), "e vão no resultado da partida");

		world.QueueFree();
	}

	// ------------------------------------------------------------ o "jogo" de cada um

	private void Aim(long account, Vector3 at, Vector3 lookAt)
	{
		Vector3 to = lookAt + Vector3.Up * 0.75f - (at + Vector3.Up * 0.6f);
		float yaw = Mathf.Atan2(-to.X, -to.Z);
		float pitch = Mathf.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
		_input[account] = (0, new InputCommand[InputCommand.Redundancy], 0, at + Vector3.Up * 0.2f, yaw, pitch);
	}

	private async Task Steps(RegressionBench b, int count)
	{
		for (int i = 0; i < count; i++)
		{
			foreach (long account in _input.Keys.ToList())
			{
				var s = _input[account];
				s.Seq++;
				var c = new InputCommand
				{
					Seq = s.Seq, Yaw = Quant.Yaw(s.Yaw), Pitch = Quant.Pitch(s.Pitch), Epoch = s.Epoch,
					HeldSlot = (byte)Slot.Primary, HasSpawn = true, Spawn = s.Spawn.ToNumerics()
				};
				s.Recent[s.Seq % InputCommand.Redundancy] = c;
				_input[account] = s;
				byte[] packet = NetCommands.InputPacket(s.Recent, s.Seq, new byte[256]);
				Receive(PeerOf(account), packet);
			}
			var watch = System.Diagnostics.Stopwatch.StartNew();
			_match.PhysicsStep(++_tick, 1 / 60.0);
			_worstStepMs = Math.Max(_worstStepMs, watch.Elapsed.TotalMilliseconds);
			_match.Tick(1 / 60.0);
			await b.PhysicsFrame();
		}
	}

	private void Status(long peer, float hp, string primary)
	{
		BinaryWriter w = NetWire.Begin(NetWire.Status, out MemoryStream stream);
		w.Write(hp); w.Write(200f); w.Write(0f); w.Write(0f); w.Write(false);
		w.Write(primary); w.Write("deagle");
		w.Write((short)30); w.Write(false); w.Write((byte)1); w.Write(0f); w.Write(0f);
		w.Write(false);
		Receive(peer, stream.ToArray());
	}

	private void Hit(long shooter, long target, float amount, Vector3 from, string weapon, bool headshot = false, KillCause? cause = null)
	{
		BinaryWriter w = NetWire.Begin(NetWire.Hit, out MemoryStream stream);
		w.Write(target);
		w.Write(amount);
		w.Write(headshot);
		w.Write((byte)(cause ?? (headshot ? KillCause.Headshot : KillCause.Gun)));
		w.Write(weapon);
		w.Write(from);
		w.Write(_input[shooter].Seq + 1);
		w.Write((double)_tick - NetRates.InterpolationDelay * NetRates.TickRate);
		Receive(PeerOf(shooter), stream.ToArray());
	}

	private void DiedPacket(long peer, long killer)
	{
		BinaryWriter w = NetWire.Begin(NetWire.Died, out MemoryStream stream);
		w.Write(killer); w.Write((byte)KillCause.Gun); w.Write("m4a4"); w.Write(false); w.Write((byte)0);
		Receive(peer, stream.ToArray());
	}

	private void Receive(long peer, byte[] packet)
	{
		using BinaryReader r = NetWire.Open(packet);
		byte type = r.ReadByte();
		_match.Receive(peer, type, packet, r);
	}

	private int Refused(HitVerdict v) => _match.HitsRefused.GetValueOrDefault(v);

	private static long PeerOf(long account) => account + 100;

	private ServerBody Body(MatchWorld world, long account) => _match.BodyOf(account);

	private static Vector3 Eye(MatchWorld world, long account) =>
		world.GetNode<Player>($"Jogador{account}").GetNode<Node3D>("Head").GlobalPosition;

	private static Vector3 Floor(PhysicsDirectSpaceState3D space, Vector3 at)
	{
		var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * 1.2f, at + Vector3.Down * 6f);
		Godot.Collections.Dictionary hit = space.IntersectRay(query);
		return hit.Count == 0 ? Vector3.Inf : (Vector3)hit["position"] + Vector3.Up * 0.9f;
	}

	private static bool Ray(PhysicsDirectSpaceState3D space, Vector3 from, Vector3 to) =>
		space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to)).Count == 0;

	/// <summary>Um ponto do chão perto do corpo (2 a 7 m) onde a explosão
	/// enxerga (<paramref name="wantClear"/>) ou não enxerga nenhum dos pontos
	/// que o servidor confere (<see cref="HitRules.TargetPoints"/>), saindo de
	/// onde o servidor sai (<see cref="Grenade.SightLift"/> acima do ponto).
	/// Perto do atirador (um arremesso). Os corpos não contam (o servidor os
	/// exclui da conferência).</summary>
	private static Vector3? GrenadeSpot(PhysicsDirectSpaceState3D space, Vector3 body, Vector3 thrower, bool wantClear)
	{
		Span<System.Numerics.Vector3> points = stackalloc System.Numerics.Vector3[6];
		for (float ring = 2f; ring <= 7f; ring += 1f)
			for (int i = 0; i < 16; i++)
			{
				float angle = Mathf.Tau * i / 16f;
				Vector3 floor = Floor(space, body + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * ring);
				if (floor == Vector3.Inf || Mathf.Abs(floor.Y - body.Y) > 1.5f) continue;
				Vector3 spot = floor - Vector3.Up * 0.75f;
				if (spot.DistanceTo(thrower) > 40f || spot.DistanceTo(body) > 8.5f) continue;
				Vector3 origin = spot + Vector3.Up * Grenade.SightLift;
				int count = HitRules.TargetPoints(body.ToNumerics(), 1.75f, origin.ToNumerics(), points);
				int seen = 0;
				for (int k = 0; k < count; k++)
					if (RayNoBodies(space, origin, new Vector3(points[k].X, points[k].Y, points[k].Z))) seen++;
				if (wantClear ? seen == count : seen == 0) return spot;
			}
		return null;
	}

	/// <summary>A linha, sem contar corpo nenhum (como o servidor: só o mapa).</summary>
	private static bool RayNoBodies(PhysicsDirectSpaceState3D space, Vector3 from, Vector3 to)
	{
		var exclude = new Godot.Collections.Array<Rid>();
		for (int tries = 0; tries < 6; tries++)
		{
			var query = PhysicsRayQueryParameters3D.Create(from, to);
			query.Exclude = exclude;
			Godot.Collections.Dictionary hit = space.IntersectRay(query);
			if (hit.Count == 0) return true;
			if (hit["collider"].Obj is CharacterBody3D) { exclude.Add((Rid)hit["rid"]); continue; }
			return false;
		}
		return false;
	}
}
