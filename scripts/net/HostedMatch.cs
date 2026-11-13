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

	public HostedMatch(string key, Referee referee, IMatchLink link)
	{
		Key = key;
		Referee = referee;
		_link = link;
	}

	/// <summary>Abre a partida que o servidor do Alcateia mandou.</summary>
	public static HostedMatch FromAssignment(JsonObject assignment, IMatchLink link, double? seconds = null, double? countdown = null)
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

		var match = new HostedMatch(assignment["matchKey"].GetValue<string>(), referee, link);
		int bots = seats.Count(s => s.Item4 != 0);
		GD.Print($"Partida {match.Key}: {seats.Count - bots} jogador(es) e {bots} bot(s), mapa {settings["map"]}, até {referee.ScoreToWin} pontos.");
		return match;
	}

	public bool Has(long peer) => _accountOf.ContainsKey(peer);

	/// <summary>Passa o tempo: o juiz, o relógio para os jogadores e o fim.</summary>
	public void Tick(double delta)
	{
		if (_left.Count > 0) AnnounceGone();

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
		_link.Send(peer, ClockPacket(NetWire.Welcome), true);
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
				["headshots"] = s.Headshots, ["points"] = s.Points
			});

		return new JsonObject
		{
			["matchKey"] = Key, ["serverId"] = serverId,
			["scoreBlue"] = Referee.Score(Referee.Blue), ["scoreOrange"] = Referee.Score(Referee.Orange), ["players"] = players
		};
	}

	// ------------------------------------------------------------ pacotes

	/// <summary>Um pacote de um jogador desta partida.</summary>
	public void Receive(long peer, byte type, byte[] packet, BinaryReader r)
	{
		if (!_accountOf.TryGetValue(peer, out long account)) return;

		switch (type)
		{
			case NetWire.State:
				Broadcast(NetWire.Relay(packet, account), reliable: false, except: peer);
				break;

			case NetWire.Attack:
				Broadcast(NetWire.Relay(packet, account), except: peer);
				break;

			case NetWire.Hit:
				ForwardHit(account, packet, r, 1);
				break;

			case NetWire.Died:
				ReadDeath(account, r);
				break;

			// O dono falando por um bot dele: confere que é dele e trata
			// como se o bot fosse um jogador.
			case NetWire.BotState:
			case NetWire.BotAttack:
				if (!Referee.Speaks(account, r.ReadInt64())) break;
				Broadcast(NetWire.AsPlayer(packet, type == NetWire.BotState ? NetWire.State : NetWire.Attack),
					reliable: type != NetWire.BotState, except: peer);
				break;

			case NetWire.BotHit:
				long shooter = r.ReadInt64();
				if (Referee.Speaks(account, shooter)) ForwardHit(shooter, packet, r, 9);
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

		if (Referee.Death(victim, killer, headshot, assisters) is RefKill kill) Announce(kill, cause, weapon);
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
		w.Write((byte)Referee.Phase);
		w.Write((float)Referee.CountdownLeft);
		w.Write((float)Referee.TimeLeft);
		w.Write(Referee.Score(Referee.Blue));
		w.Write(Referee.Score(Referee.Orange));
		return stream.ToArray();
	}

	private void Broadcast(byte[] packet, bool reliable = true, long except = 0)
	{
		foreach (long peer in _accountOf.Keys)
			if (peer != except) _link.Send(peer, packet, reliable);
	}
}
