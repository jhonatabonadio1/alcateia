using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// Uma partida dentro do servidor de partida (<see cref="MatchServer"/>):
/// o juiz, quem está ligado nela e o que vai e volta entre eles. O
/// servidor de partida aguenta várias destas ao mesmo tempo, cada uma
/// com os seus jogadores — um pacote de uma nunca chega na outra.
///
/// Não fala com a rede diretamente: recebe do servidor de partida o que
/// os seus jogadores mandam (<see cref="Receive"/>) e devolve por
/// <see cref="IMatchLink"/>.
///
/// **Autoritativa** (decisão 0011): com um <see cref="World"/>, a partida
/// tem o mapa e um corpo por jogador (<see cref="ServerBody"/>), movidos
/// pelos comandos que chegam, a cada passo de física
/// (<see cref="PhysicsStep"/>); a cada dois passos, todos recebem o estado
/// do mundo, cada um com o número do último comando seu já simulado.
///
/// **Vida e acerto** (decisão 0016, só na autoritativa): o acerto que um
/// jogo diz que deu entra numa fila, espera o servidor simular o comando
/// em que o tiro saiu e é conferido (<see cref="HitCheck"/>). Valendo, o
/// dano é aplicado no corpo daqui, a vítima recebe como ficou
/// (<c>Damage</c>) e, se caiu, o abate é anunciado — quem decide a morte é
/// o servidor. Recusado, não acontece nada, e a recusa conta contra quem
/// atirou (vai no resultado).
/// </summary>
public sealed class HostedMatch
{
	/// <summary>Acabou: tanto tempo para os jogadores verem o placar antes
	/// de a partida fechar.</summary>
	public const double LingerAfterEnd = 8;

	/// <summary>O que a partida precisa do servidor de partida.</summary>
	public interface IMatchLink
	{
		void Send(long peer, byte[] packet, bool reliable);
		void Kick(long peer);
		void Report(HostedMatch match);
	}

	private const double ClockEvery = 0.5;

	public string Key { get; }
	public Referee Referee { get; }

	/// <summary>O mundo da partida autoritativa (null no modo de repasse).
	/// Quem põe na árvore é o servidor de partida.</summary>
	public MatchWorld World { get; }
	public bool Authoritative => World != null;

	private readonly Dictionary<long, ServerBody> _bodies = new();
	private readonly List<ServerBody> _bodyList = new();
	private readonly Dictionary<long, byte> _indexOf = new();
	private EntityState[] _states = new EntityState[16];
	private byte[] _snapshotBody = new byte[1 + 16 * EntityState.Size];
	private readonly byte[] _packet = new byte[2048];

	/// <summary>Quanto um acerto espera o comando do tiro ser simulado, em passos.</summary>
	public const int HitWait = 12;

	private readonly HitCheck _hits;
	private readonly List<PendingHit> _pending = new();
	private readonly Dictionary<long, (Vector3 Position, uint Tick)> _botPoses = new();
	private uint _tick;
	private double _now, _vitals;

	private struct PendingHit
	{
		public ClaimedHit Hit;
		public bool ByBot;
		public uint Arrived;
	}

	/// <summary>Acertos conferidos: quantos valeram e quantos foram recusados, por motivo.</summary>
	public int HitsAccepted { get; private set; }
	public Dictionary<HitVerdict, int> HitsRefused { get; } = new();

	/// <summary>O corpo de um jogador no mundo da partida (null fora da autoritativa).</summary>
	public ServerBody BodyOf(long account) => _bodies.GetValueOrDefault(account);

	/// <summary>Corpos simulados agora.</summary>
	public int Bodies => _bodyList.Count;

	/// <summary>Acabou e o placar já ficou na tela o bastante: pode fechar.</summary>
	public bool Finished => _reported && _linger <= 0;

	/// <summary>Quantos jogadores (gente) estão ligados agora.</summary>
	public int Players => _accountOf.Count;

	private readonly IMatchLink _link;
	private readonly Dictionary<long, long> _accountOf = new();   // ligação → conta
	private readonly Dictionary<long, long> _peerOf = new();      // conta → ligação
	private readonly List<long> _left = new();
	private double _clock, _linger;
	private bool _reported;

	public HostedMatch(string key, Referee referee, IMatchLink link, MatchWorld world = null)
	{
		Key = key;
		Referee = referee;
		_link = link;
		World = world;
		if (world != null) _hits = new HitCheck(world, BodyRids);

		// O número de cada corpo no estado do mundo: o lugar na escalação.
		byte index = 0;
		foreach (Referee.Seat seat in referee.Seats) _indexOf[seat.Id] = index++;
	}

	/// <summary>Abre a partida que o servidor do Alcateia mandou.</summary>
	public static HostedMatch FromAssignment(JsonObject assignment, IMatchLink link, double? seconds = null, double? countdown = null,
		bool authoritative = false)
	{
		JsonObject settings = assignment["settings"] as JsonObject ?? new JsonObject();
		var seats = new List<(long, string, int, long)>();
		foreach (JsonNode p in assignment["players"] as JsonArray ?? new JsonArray())
			seats.Add((p["accountId"].GetValue<long>(), p["name"].GetValue<string>(), p["team"].GetValue<int>(), p["owner"]?.GetValue<long>() ?? 0));

		var referee = new Referee(seats,
			settings["scoreToWin"]?.GetValue<int>() ?? 40,
			seconds ?? (settings["minutes"]?.GetValue<int>() ?? 10) * 60.0,
			settings["friendlyFire"]?.GetValue<bool>() ?? false,
			countdown ?? 10);

		string map = settings["map"]?.GetValue<string>() ?? Maps.All[0].Id;
		var match = new HostedMatch(assignment["matchKey"].GetValue<string>(), referee, link,
			authoritative ? new MatchWorld(map) : null);
		int bots = seats.Count(s => s.Item4 != 0);
		GD.Print($"Partida {match.Key}: {seats.Count - bots} jogador(es) e {bots} bot(s), mapa {map}, até {referee.ScoreToWin} pontos" +
			(authoritative ? ", autoritativa." : "."));
		return match;
	}

	public bool Has(long peer) => _accountOf.ContainsKey(peer);

	/// <summary>Passa o tempo: o juiz, o relógio para os jogadores e o fim.</summary>
	public void Tick(double delta)
	{
		_now += delta;
		if (_left.Count > 0) AnnounceGone();

		// A vida de cada um, como o servidor tem, para o jogo dele: se lá
		// está mais alta, vale a daqui.
		_vitals -= delta;
		if (Authoritative && _vitals <= 0)
		{
			_vitals = ClockEvery;
			SendVitals();
		}

		RefPhase before = Referee.Phase;
		Referee.Tick(delta);

		_clock -= delta;
		if (Referee.Phase != before || _clock <= 0)
		{
			_clock = ClockEvery;
			Broadcast(ClockPacket(NetWire.Clock), reliable: Referee.Phase != before);
		}

		if (Referee.Phase != RefPhase.Ended) return;

		if (!_reported)
		{
			_reported = true;
			_linger = LingerAfterEnd;
			_link.Report(this);
		}
		_linger -= delta;
	}

	/// <summary>Um jogador mostrou o ingresso (já conferido). Falso se o
	/// juiz não o tem na escalação.</summary>
	public bool Admit(long peer, long account)
	{
		if (!Referee.Join(account)) return false;

		// Uma ligação por conta: a nova vale, a velha cai.
		if (_peerOf.TryGetValue(account, out long old) && old != peer)
		{
			_accountOf.Remove(old);
			_link.Kick(old);
		}

		_accountOf[peer] = account;
		_peerOf[account] = peer;
		if (Authoritative && !_bodies.ContainsKey(account)) AddBody(account);
		_link.Send(peer, WelcomePacket(), true);
		GD.Print($"Entrou em {Key}: {Referee.Find(account).Name} ({Referee.ConnectedCount} de {Referee.Seats.Count(s => !s.IsBot)}).");
		return true;
	}

	/// <summary>A ligação caiu.</summary>
	public void Drop(long peer)
	{
		if (!_accountOf.Remove(peer, out long account)) return;
		if (_peerOf.TryGetValue(account, out long current) && current == peer) _peerOf.Remove(account);

		// O aviso sai no quadro seguinte: quando dois saem juntos, o
		// segundo ainda consta aqui mas já não recebe nada. Os bots de
		// quem saiu saem junto: não há mais máquina para movê-los.
		_left.Add(account);
		_left.AddRange(Referee.BotsOf(account).Select(b => b.Id));
		Referee.Leave(account);
		RemoveBody(account);
	}

	/// <summary>Solta todo mundo.</summary>
	public void Close()
	{
		foreach (long peer in _accountOf.Keys.ToList()) _link.Kick(peer);
		_accountOf.Clear();
		_peerOf.Clear();
	}

	/// <summary>O resultado, como vai para o servidor do Alcateia.</summary>
	public JsonObject Result(string serverId)
	{
		var players = new JsonArray();
		// Bot não é conta: não entra no resultado (não ganha XP nem patente).
		foreach (Referee.Seat s in Referee.Seats.Where(s => s.EverConnected && !s.IsBot))
			players.Add(new JsonObject
			{
				["accountId"] = s.Id, ["kills"] = s.Kills, ["deaths"] = s.Deaths, ["assists"] = s.Assists,
				["headshots"] = s.Headshots, ["points"] = s.Points,
				["killsWolf"] = s.KillsWolf, ["rejectedHits"] = s.RejectedHits
			});

		return new JsonObject
		{
			["matchKey"] = Key, ["serverId"] = serverId,
			["scoreBlue"] = Referee.Score(Referee.Blue), ["scoreOrange"] = Referee.Score(Referee.Orange), ["players"] = players
		};
	}

	// ------------------------------------------------------------ o mundo (autoritativa)

	private void AddBody(long account)
	{
		Referee.Seat seat = Referee.Find(account);
		Player body = World.SpawnBody($"Jogador{account}", seat?.Team ?? Referee.Blue);
		var server = new ServerBody(account, _indexOf.GetValueOrDefault(account), body);
		server.SpawnPoints.AddRange(World.SpawnsFor(seat?.Team ?? Referee.Blue));
		_bodies[account] = server;
		_bodyList.Add(server);
	}

	private void RemoveBody(long account)
	{
		if (!_bodies.Remove(account, out ServerBody body)) return;
		_bodyList.Remove(body);
		body.Body.QueueFree();
	}

	/// <summary>Um passo de física: cada corpo consome o comando dele; a
	/// cada <see cref="NetRates.SnapshotEvery"/> passos, o estado do mundo
	/// vai para todos.</summary>
	public void PhysicsStep(uint tick, double delta)
	{
		if (!Authoritative) return;

		_tick = tick;
		foreach (ServerBody body in _bodyList) body.Step(tick, delta);
		ResolveHits(tick);
		if (tick % NetRates.SnapshotEvery == 0) SendSnapshots(tick);
	}

	private IEnumerable<Rid> BodyRids()
	{
		foreach (ServerBody body in _bodyList) yield return body.Body.GetRid();
	}

	/// <summary>O estado do mundo: o corpo comum é escrito uma vez; cada
	/// jogador recebe o cabeçalho dele (o último comando seu simulado).</summary>
	private void SendSnapshots(uint tick)
	{
		if (_accountOf.Count == 0) return;

		int count = 0;
		if (_states.Length < _bodyList.Count) _states = new EntityState[_bodyList.Count * 2];
		foreach (ServerBody body in _bodyList)
			if (body.Placed) _states[count++] = body.State();

		int size = 1 + count * EntityState.Size;
		if (_snapshotBody.Length < size) _snapshotBody = new byte[size * 2];
		int bodyLength = SnapshotWire.WriteBody(_snapshotBody, _states.AsSpan(0, count));
		ReadOnlySpan<byte> shared = _snapshotBody.AsSpan(0, bodyLength);

		foreach (var (peer, account) in _accountOf)
		{
			uint ack = _bodies.TryGetValue(account, out ServerBody mine) ? mine.Commands.LastProcessed : 0;
			int length = SnapshotWire.Compose(_packet, NetWire.Snapshot, tick, ack, shared);
			_link.Send(peer, _packet.AsSpan(0, length).ToArray(), false);
		}
	}

	/// <summary>O Welcome: o relógio, o modo e, na autoritativa, o número
	/// de cada jogador no estado do mundo.</summary>
	private byte[] WelcomePacket()
	{
		BinaryWriter w = NetWire.Begin(NetWire.Welcome, out MemoryStream stream);
		WriteClock(w);
		w.Write(Authoritative ? NetWire.ModeAuthoritative : NetWire.ModeRelay);
		if (Authoritative)
		{
			w.Write((byte)NetRates.TickRate);
			var humans = Referee.Seats.Where(s => !s.IsBot).ToList();
			w.Write((byte)humans.Count);
			foreach (Referee.Seat s in humans)
			{
				w.Write(_indexOf[s.Id]);
				w.Write(s.Id);
			}
		}
		return stream.ToArray();
	}

	// ------------------------------------------------------------ pacotes

	/// <summary>Um pacote de um jogador desta partida.</summary>
	public void Receive(long peer, byte type, byte[] packet, BinaryReader r)
	{
		if (!_accountOf.TryGetValue(peer, out long account)) return;

		switch (type)
		{
			case NetWire.State:
				if (!Authoritative) Broadcast(NetWire.Relay(packet, account), reliable: false, except: peer);
				break;

			case NetWire.Input:
				if (Authoritative && _bodies.TryGetValue(account, out ServerBody moving)) NetCommands.ReadInputPacket(packet, moving.Commands);
				break;

			case NetWire.Status:
				if (!Authoritative) break;
				ReadStatus(peer, account, packet, r);
				break;

			case NetWire.Loadout:
				if (!Authoritative || !_bodies.TryGetValue(account, out ServerBody loading)) break;
				string breed = r.ReadString();
				var tattoos = new List<string>();
				for (int n = Math.Min((int)r.ReadByte(), 8); n > 0; n--) tattoos.Add(r.ReadString());
				loading.ApplyLoadout(breed, tattoos);
				break;

			case NetWire.Attack:
				Broadcast(NetWire.Relay(packet, account), except: peer);
				break;

			// O call de rádio: vai a todos; cada máquina toca se for do time.
			case NetWire.Radio:
				Broadcast(NetWire.Relay(packet, account), except: peer);
				break;

			case NetWire.Hit:
				if (Authoritative) Claim(account, byBot: false, r);
				else ForwardHit(account, packet, r, 1);
				break;

			case NetWire.Died:
				if (Authoritative) ReadOwnDeath(account, r);
				else ReadDeath(account, r);
				break;

			// O dono falando por um bot dele: confere que é dele e trata
			// como se o bot fosse um jogador.
			case NetWire.BotState:
			case NetWire.BotAttack:
				long speaker = r.ReadInt64();
				if (!Referee.Speaks(account, speaker)) break;
				// Onde o bot está: é contra isso que se confere o acerto nele.
				if (type == NetWire.BotState) _botPoses[speaker] = (r.ReadVector(), _tick);
				Broadcast(NetWire.AsPlayer(packet, type == NetWire.BotState ? NetWire.State : NetWire.Attack),
					reliable: type != NetWire.BotState, except: peer);
				break;

			case NetWire.BotHit:
				long shooter = r.ReadInt64();
				if (!Referee.Speaks(account, shooter)) break;
				if (Authoritative) Claim(shooter, byBot: true, r);
				else ForwardHit(shooter, packet, r, 9);
				break;

			case NetWire.BotDied:
				long fallen = r.ReadInt64();
				if (Referee.Speaks(account, fallen)) ReadDeath(fallen, r);
				break;

			case NetWire.Touch:
				// Acerto entre dois corpos da mesma máquina: já foi
				// aplicado lá. Só a autoria fica anotada, para o abate valer.
				long author = r.ReadInt64(), touched = r.ReadInt64();
				if (Referee.Speaks(account, author) && Referee.Speaks(account, touched))
					Referee.Hit(author, touched, r.ReadSingle());
				break;
		}
	}

	/// <summary>Um acerto de <paramref name="attacker"/>: se o juiz
	/// aceita, segue para a máquina que manda no corpo atingido — a da
	/// vítima, ou a do dono se a vítima é um bot. <paramref name="at"/> é
	/// onde, no pacote, está a conta do alvo.</summary>
	private void ForwardHit(long attacker, byte[] packet, BinaryReader r, int at)
	{
		long target = r.ReadInt64();
		float amount = r.ReadSingle();
		if (!Referee.Hit(attacker, target, amount)) return;

		Referee.Seat seat = Referee.Find(target);
		if (!_peerOf.TryGetValue(seat.IsBot ? seat.Owner : target, out long victim)) return;

		// O resto do acerto (dano, cabeça, causa, arma, de onde veio) vai como veio.
		int rest = at + 8;
		using var stream = new MemoryStream(packet.Length + 8);
		using var w = new BinaryWriter(stream);
		w.Write(seat.IsBot ? NetWire.HitBot : NetWire.Hit);
		w.Write(attacker);
		if (seat.IsBot) w.Write(target);
		w.Write(packet, rest, packet.Length - rest);
		_link.Send(victim, stream.ToArray(), true);
	}

	private void ReadDeath(long victim, BinaryReader r)
	{
		long killer = r.ReadInt64();
		byte cause = r.ReadByte();
		string weapon = r.ReadString();
		bool headshot = r.ReadBoolean();
		var assisters = new long[r.ReadByte()];
		for (int i = 0; i < assisters.Length; i++) assisters[i] = r.ReadInt64();

		if (Referee.Death(victim, killer, headshot, assisters, killerWasWolf: cause == (byte)KillCause.Wolf) is RefKill kill)
			Announce(kill, cause, weapon);
	}

	// ------------------------------------------------------------ vida e acerto (autoritativa, decisão 0016)

	/// <summary>
	/// O jogo conta que morreu. Na autoritativa quem decide a morte por
	/// acerto de outra máquina é o servidor; daqui só vale a morte sem
	/// autor (queda, a própria granada) ou por um bot da própria máquina.
	/// </summary>
	private void ReadOwnDeath(long account, BinaryReader r)
	{
		long killer = r.ReadInt64();
		if (killer != 0 && !(Referee.Find(killer) is { IsBot: true } bot && bot.Owner == account)) return;

		r.BaseStream.Position = 1;
		_bodies.TryGetValue(account, out ServerBody body);
		bool wasAlive = body?.Alive ?? true;
		body?.SelfDeath();
		if (wasAlive) ReadDeath(account, r);
	}

	/// <summary>Vida, escudo e armas do jogo: daqui só vale o que desce. Os
	/// outros recebem a vida que o servidor tem.</summary>
	private void ReadStatus(long peer, long account, byte[] packet, BinaryReader r)
	{
		float hp = r.ReadSingle();
		r.ReadSingle();
		float shield = r.ReadSingle();
		r.ReadSingle();
		r.ReadBoolean();
		string primary = r.ReadString(), secondary = r.ReadString();
		bool guard = false;
		long left = r.BaseStream.Length - r.BaseStream.Position;
		if (left >= 2 + 1 + 1 + 4 + 4 + 1)
		{
			r.BaseStream.Position += 2 + 1 + 1 + 4 + 4;
			guard = r.ReadBoolean();
		}

		byte[] relayed = NetWire.Relay(packet, account);
		if (!_bodies.TryGetValue(account, out ServerBody body) || !body.Placed)
		{
			body?.ApplyStatus(hp, shield, primary, secondary, guard);
			Broadcast(relayed, reliable: false, except: peer);
			return;
		}
		body.ApplyStatus(hp, shield, primary, secondary, guard);

		Health h = body.Health;
		BitConverter.TryWriteBytes(relayed.AsSpan(9, 4), h.Hp);
		BitConverter.TryWriteBytes(relayed.AsSpan(13, 4), h.MaxHp);
		BitConverter.TryWriteBytes(relayed.AsSpan(17, 4), h.Shield);
		BitConverter.TryWriteBytes(relayed.AsSpan(21, 4), h.MaxShield);
		relayed[25] = (byte)(h.IsDead ? 1 : 0);
		Broadcast(relayed, reliable: false, except: peer);
	}

	/// <summary>A vida que o servidor tem de cada um, para o jogo dele.</summary>
	private void SendVitals()
	{
		foreach (var (peer, account) in _accountOf)
		{
			if (!_bodies.TryGetValue(account, out ServerBody body) || !body.Placed) continue;
			BinaryWriter w = NetWire.Begin(NetWire.Vitals, out MemoryStream stream);
			w.Write(body.Health.Hp);
			w.Write(body.Health.Shield);
			w.Write(body.Health.IsDead);
			_link.Send(peer, stream.ToArray(), false);
		}
	}

	/// <summary>Um acerto que um jogo diz que deu entra na fila.</summary>
	private void Claim(long shooter, bool byBot, BinaryReader r)
	{
		var hit = new ClaimedHit
		{
			Shooter = shooter,
			Target = r.ReadInt64(),
			Amount = r.ReadSingle(),
			Headshot = r.ReadBoolean(),
			Cause = (KillCause)r.ReadByte(),
			Weapon = r.ReadString(),
			From = r.ReadVector(),
			ViewTick = double.NaN
		};
		if (r.BaseStream.Length - r.BaseStream.Position >= 4 + 8)
		{
			hit.Seq = r.ReadUInt32();
			hit.ViewTick = r.ReadDouble();
		}
		if (!float.IsFinite(hit.Amount) || !hit.From.IsFinite()) return;
		_pending.Add(new PendingHit { Hit = hit, ByBot = byBot, Arrived = _tick });
	}

	/// <summary>Confere os acertos cujo comando do tiro já foi simulado
	/// (ou que esperaram demais).</summary>
	private void ResolveHits(uint tick)
	{
		for (int i = 0; i < _pending.Count;)
		{
			PendingHit p = _pending[i];
			if (!p.ByBot && _bodies.TryGetValue(p.Hit.Shooter, out ServerBody waiting)
				&& waiting.Commands.LastProcessed < p.Hit.Seq && tick - p.Arrived < HitWait)
			{
				i++;
				continue;
			}
			_pending.RemoveAt(i);
			Resolve(p, tick);
		}
	}

	private void Resolve(in PendingHit p, uint tick)
	{
		ClaimedHit hit = p.Hit;
		Referee.Seat target = Referee.Find(hit.Target), shooterSeat = Referee.Find(hit.Shooter);
		// Aliado, partida parada, alguém que saiu: não é trapaça, só não vale.
		if (target == null || shooterSeat == null || !Referee.CanHit(hit.Shooter, hit.Target, Math.Min(hit.Amount, Referee.MaxHit))) return;

		ServerBody victim = null;
		if (!target.IsBot && !_bodies.TryGetValue(hit.Target, out victim)) return;

		HitVerdict verdict;
		float amount;
		bool headshot;
		if (p.ByBot)
			verdict = CheckBotShot(hit, victim, tick, out amount, out headshot);
		else
		{
			if (!_bodies.TryGetValue(hit.Shooter, out ServerBody shooter)) return;
			ServerBody.ShotPose shot = shooter.ShotAt(hit.Seq);
			double first, last;
			if (target.IsBot)
			{
				first = last = tick;
			}
			else
			{
				double rewind = HitRules.RewindTick(hit.ViewTick, shot.Tick);
				first = double.IsNaN(hit.ViewTick) ? rewind : rewind - HitRules.RewindSlackTicks;
				last = double.IsNaN(hit.ViewTick) ? shot.Tick : Math.Min(rewind + HitRules.RewindSlackTicks, shot.Tick);
			}
			verdict = _hits.Check(hit, shot, shooter, PoseOf(hit.Target, target.IsBot), first, last, _now, out amount, out headshot);
		}

		if (verdict != HitVerdict.Ok)
		{
			Refuse(hit, shooterSeat, verdict);
			return;
		}

		// Valeu, mas o alvo já caiu (ou acabou de renascer, protegido): não
		// é trapaça — os dois atiraram quase juntos.
		if (victim != null && (!victim.Alive || victim.Health.IsProtected)) return;

		HitsAccepted++;
		if (target.IsBot) SendHitToOwner(hit, target, amount, headshot);
		else ApplyHit(hit, victim, amount, headshot);
	}

	/// <summary>O tiro de um bot (a máquina do dono o move): confere de onde
	/// saiu e a parede no caminho, contra onde o servidor tinha o alvo no
	/// último instante que o dono podia estar vendo.</summary>
	private HitVerdict CheckBotShot(in ClaimedHit hit, ServerBody victim, uint tick, out float amount, out bool headshot)
	{
		amount = Math.Min(hit.Amount, Referee.MaxHit);
		headshot = hit.Headshot;
		if (!_botPoses.TryGetValue(hit.Shooter, out var bot)) return HitVerdict.ShooterGone;
		if (hit.Cause != KillCause.Grenade && hit.From.DistanceTo(bot.Position) > HitRules.OriginSlack + 2.5f) return HitVerdict.BadOrigin;
		if (victim == null) return HitVerdict.Ok;   // bot em bot é da mesma máquina (Touch)

		return _hits.CheckLine(hit, PoseOf(hit.Target, false), (double)tick - HitRules.MaxRewindTicks, tick);
	}

	/// <summary>Onde o alvo estava num passo: o corpo daqui (com a
	/// história), ou o bot onde o dono disse por último.</summary>
	private HitCheck.PoseAt PoseOf(long target, bool bot)
	{
		if (bot)
			return (double _, out Vector3 center, out float height) =>
			{
				height = 1.75f;
				bool known = _botPoses.TryGetValue(target, out var pose);
				center = known ? pose.Position : default;
				return known;
			};

		ServerBody body = _bodies.GetValueOrDefault(target);
		return (double tick, out Vector3 center, out float height) =>
		{
			center = default;
			height = 1.75f;
			if (body == null || !body.History.Sample(tick, out PoseSample pose)) return false;
			center = pose.Position.ToGodot();
			height = pose.Height;
			return true;
		};
	}

	private void Refuse(in ClaimedHit hit, Referee.Seat shooter, HitVerdict verdict)
	{
		HitsRefused[verdict] = HitsRefused.GetValueOrDefault(verdict) + 1;
		if (!HitCheck.Suspicious(verdict)) return;

		shooter.RejectedHits++;
		if (shooter.RejectedHits <= 5 || shooter.RejectedHits % 25 == 0)
			GD.Print($"Acerto recusado em {Key}: {shooter.Name} em {Referee.Find(hit.Target)?.Name} ({hit.WeaponId}): " +
				$"{HitCheck.Describe(verdict)} ({shooter.RejectedHits} até agora).");
	}

	/// <summary>O acerto valeu num jogador: o dano é aplicado aqui, a vítima
	/// recebe como ficou e, se caiu, o servidor anuncia o abate.</summary>
	private void ApplyHit(in ClaimedHit hit, ServerBody victim, float amount, bool headshot)
	{
		if (!Referee.Hit(hit.Shooter, hit.Target, amount)) return;

		Health h = victim.Health;
		float before = h.Hp + h.Shield;
		h.TakeDamage(amount, 0, hit.From, headshot);
		float dealt = before - (h.Hp + h.Shield);
		if (dealt <= 0) return;

		KillCause cause = hit.Cause == KillCause.Headshot && !headshot ? KillCause.Gun
			: hit.Cause == KillCause.Gun && headshot ? KillCause.Headshot
			: hit.Cause;

		if (_peerOf.TryGetValue(hit.Target, out long peer))
		{
			BinaryWriter w = NetWire.Begin(NetWire.Damage, out MemoryStream stream);
			w.Write(hit.Shooter);
			w.Write(dealt);
			w.Write(headshot);
			w.Write((byte)cause);
			w.Write(hit.Weapon ?? "");
			w.Write(hit.From);
			w.Write(h.Hp);
			w.Write(h.Shield);
			_link.Send(peer, stream.ToArray(), true);
		}

		if (!h.IsDead) return;
		if (Referee.Death(hit.Target, hit.Shooter, headshot, Referee.RecentHitters(hit.Target), killerWasWolf: cause == KillCause.Wolf) is RefKill kill)
			Announce(kill, (byte)cause, hit.Weapon ?? "");
	}

	/// <summary>O acerto valeu num bot: segue para a máquina do dono, que o move.</summary>
	private void SendHitToOwner(in ClaimedHit hit, Referee.Seat bot, float amount, bool headshot)
	{
		if (!Referee.Hit(hit.Shooter, hit.Target, amount) || !_peerOf.TryGetValue(bot.Owner, out long owner)) return;

		BinaryWriter w = NetWire.Begin(NetWire.HitBot, out MemoryStream stream);
		w.Write(hit.Shooter);
		w.Write(hit.Target);
		w.Write(amount);
		w.Write(headshot);
		w.Write((byte)hit.Cause);
		w.Write(hit.Weapon ?? "");
		w.Write(hit.From);
		_link.Send(owner, stream.ToArray(), true);
	}

	private void AnnounceGone()
	{
		foreach (long account in _left)
		{
			BinaryWriter w = NetWire.Begin(NetWire.Gone, out MemoryStream stream);
			w.Write(account);
			Broadcast(stream.ToArray());
		}
		_left.Clear();
	}

	private void Announce(RefKill kill, byte cause, string weapon)
	{
		BinaryWriter w = NetWire.Begin(NetWire.Kill, out MemoryStream stream);
		w.Write(kill.Killer);
		w.Write(kill.Victim);
		w.Write(cause);
		w.Write(weapon);
		w.Write((byte)kill.Assisters.Length);
		foreach (long id in kill.Assisters) w.Write(id);
		w.Write(Referee.Score(Referee.Blue));
		w.Write(Referee.Score(Referee.Orange));
		Broadcast(stream.ToArray());
	}

	private byte[] ClockPacket(byte type)
	{
		BinaryWriter w = NetWire.Begin(type, out MemoryStream stream);
		WriteClock(w);
		return stream.ToArray();
	}

	private void WriteClock(BinaryWriter w)
	{
		w.Write((byte)Referee.Phase);
		w.Write((float)Referee.CountdownLeft);
		w.Write((float)Referee.TimeLeft);
		w.Write(Referee.Score(Referee.Blue));
		w.Write(Referee.Score(Referee.Orange));
	}

	private void Broadcast(byte[] packet, bool reliable = true, long except = 0)
	{
		foreach (long peer in _accountOf.Keys)
			if (peer != except) _link.Send(peer, packet, reliable);
	}
}
