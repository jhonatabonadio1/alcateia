using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Em que pé a partida online está.</summary>
public enum RefPhase
{
	/// <summary>Esperando os jogadores chegarem.</summary>
	Waiting,
	Countdown,
	Live,
	Ended
}

/// <summary>Um abate que o juiz aceitou.</summary>
public readonly record struct RefKill(long Killer, long Victim, bool Headshot, long[] Assisters);

/// <summary>
/// O juiz de uma partida online: quem está nela, o relógio, o placar e o
/// que vale como abate. Roda no servidor de partida.
///
/// C# puro, sem Godot e sem rede: recebe o que os jogadores contam
/// (<see cref="Hit"/>, <see cref="Death"/>) e decide o que aceita. A
/// vida de cada um mora na máquina de cada um — o juiz não sabe quanto
/// HP ninguém tem. O que ele garante é o que não dá para um cliente
/// inventar sozinho: abate só de quem acertou a vítima há pouco, só de
/// inimigo, só com a partida valendo, e o placar e o resultado são os
/// dele. Trapaça de mira ou de dano exagerado não é barrada aqui (veja
/// docs/decisoes/0008).
/// </summary>
public sealed class Referee
{
	public const int Blue = 1, Orange = 2;
	public const int PointsPerKill = 10, PointsPerAssist = 5;

	/// <summary>Um acerto vale como autoria do abate por este tempo.</summary>
	public const double CreditWindow = 10;

	/// <summary>Dano máximo de um acerto só. Acima disso é mentira.</summary>
	public const float MaxHit = 1000f;

	public sealed class Seat
	{
		public long Id;
		public string Name;
		public int Team;

		/// <summary>Bot: a conta do jogador cuja máquina o move. Zero em
		/// gente. Um bot está "ligado" enquanto o dono estiver.</summary>
		public long Owner;
		public bool IsBot => Owner != 0;
		public bool Connected, EverConnected;
		public int Kills, Deaths, Assists, Headshots;
		public int Points => Kills * PointsPerKill + Assists * PointsPerAssist;

		internal readonly Dictionary<long, double> HitBy = new();
		internal double DiedAt = double.NegativeInfinity;
	}

	/// <summary>Espera por todos este tempo depois do primeiro chegar.</summary>
	public double StartAfter = 20;

	/// <summary>Sem ninguém por este tempo, a partida acaba.</summary>
	public double EmptyTimeout = 45;

	public RefPhase Phase { get; private set; } = RefPhase.Waiting;
	public double CountdownLeft { get; private set; }
	public double TimeLeft { get; private set; }
	public int ScoreToWin { get; }
	public bool FriendlyFire { get; }
	public int Winner { get; private set; }
	public IReadOnlyCollection<Seat> Seats => _seats.Values;

	public event Action<RefPhase> PhaseChanged;

	private readonly Dictionary<long, Seat> _seats = new();
	private readonly int[] _score = new int[3];
	private double _now, _firstJoin = -1, _emptySince;

	public Referee(IEnumerable<(long Id, string Name, int Team)> seats, int scoreToWin, double seconds,
		bool friendlyFire = false, double countdown = 10)
		: this(seats.Select(s => (s.Id, s.Name, s.Team, 0L)), scoreToWin, seconds, friendlyFire, countdown) { }

	/// <summary>Com bots: <c>Owner</c> é a conta do dono de cada bot (0 em gente).</summary>
	public Referee(IEnumerable<(long Id, string Name, int Team, long Owner)> seats, int scoreToWin, double seconds,
		bool friendlyFire = false, double countdown = 10)
	{
		foreach (var (id, name, team, owner) in seats)
			_seats[id] = new Seat { Id = id, Name = name, Team = team is Blue or Orange ? team : Blue, Owner = owner };

		ScoreToWin = Math.Max(scoreToWin, 1);
		FriendlyFire = friendlyFire;
		TimeLeft = seconds;
		CountdownLeft = countdown;
	}

	public int Score(int team) => team is Blue or Orange ? _score[team] : 0;
	public Seat Find(long id) => _seats.TryGetValue(id, out Seat s) ? s : null;
	/// <summary>Gente ligada. Bot não conta: sala só de bots está vazia.</summary>
	public int ConnectedCount => _seats.Values.Count(s => s.Connected && !s.IsBot);

	/// <summary>Os bots que a máquina desta conta move.</summary>
	public IEnumerable<Seat> BotsOf(long owner) => _seats.Values.Where(s => s.IsBot && s.Owner == owner);

	/// <summary>Esta conta fala por este lugar? Por si mesma, ou pelos bots dela.</summary>
	public bool Speaks(long account, long seatId) =>
		account == seatId || (Find(seatId) is { IsBot: true } bot && bot.Owner == account);

	/// <summary>Um jogador escalado chegou. Falso se não é desta partida.</summary>
	public bool Join(long id)
	{
		Seat seat = Find(id);
		if (seat == null || seat.IsBot || Phase == RefPhase.Ended) return false;

		seat.Connected = seat.EverConnected = true;
		foreach (Seat bot in BotsOf(id)) bot.Connected = bot.EverConnected = true;   // os bots chegam com o dono
		if (_firstJoin < 0) _firstJoin = _now;
		return true;
	}

	public void Leave(long id)
	{
		Seat seat = Find(id);
		if (seat == null || seat.IsBot) return;

		seat.Connected = false;
		foreach (Seat bot in BotsOf(id)) bot.Connected = false;   // e vão embora com ele
	}

	/// <summary>Passa o tempo: começa quando todos chegam (ou a espera
	/// vence), conta o relógio, e encerra se a sala esvaziar.</summary>
	public void Tick(double delta)
	{
		_now += delta;
		if (Phase == RefPhase.Ended) return;

		if (ConnectedCount > 0) _emptySince = _now;
		else if (_now - _emptySince >= EmptyTimeout)
		{
			End();
			return;
		}

		switch (Phase)
		{
			case RefPhase.Waiting:
				bool everyone = _seats.Values.All(s => s.Connected);
				if (everyone || (_firstJoin >= 0 && _now - _firstJoin >= StartAfter)) SetPhase(RefPhase.Countdown);
				break;

			case RefPhase.Countdown:
				CountdownLeft -= delta;
				if (CountdownLeft <= 0)
				{
					CountdownLeft = 0;
					SetPhase(RefPhase.Live);
				}
				break;

			case RefPhase.Live:
				TimeLeft -= delta;
				if (TimeLeft <= 0)
				{
					TimeLeft = 0;
					End();
				}
				break;
		}
	}

	/// <summary>Alguém diz que acertou alguém. Verdadeiro se o acerto
	/// deve seguir para a vítima.</summary>
	public bool Hit(long attackerId, long victimId, float amount)
	{
		if (Phase != RefPhase.Live || attackerId == victimId) return false;
		if (!(amount > 0f) || amount > MaxHit) return false;

		Seat attacker = Find(attackerId), victim = Find(victimId);
		if (attacker == null || victim == null || !attacker.Connected || !victim.Connected) return false;
		if (attacker.Team == victim.Team && !FriendlyFire) return false;

		victim.HitBy[attackerId] = _now;
		return true;
	}

	/// <summary>
	/// Alguém diz que morreu, e por quem. O abate só vale se o autor
	/// acertou a vítima há pouco; quem ajudou, idem. Morte sem autor
	/// (queda, a própria granada) não entra no placar, como no jogo
	/// offline. Nulo se não houve abate.
	/// </summary>
	public RefKill? Death(long victimId, long killerId, bool headshot, IEnumerable<long> assisters)
	{
		Seat victim = Find(victimId);
		if (Phase != RefPhase.Live || victim == null || !victim.Connected) return null;

		// Ninguém morre duas vezes no mesmo segundo.
		if (_now - victim.DiedAt < 1.0) return null;
		victim.DiedAt = _now;

		Seat killer = Find(killerId);
		bool credited = killer != null && killer != victim && Recent(victim, killerId)
			&& (killer.Team != victim.Team || FriendlyFire);

		long[] helpers = credited
			? (assisters ?? Array.Empty<long>()).Distinct()
				.Where(id => id != killerId && id != victimId && Recent(victim, id) && Find(id)?.Team == killer.Team)
				.ToArray()
			: Array.Empty<long>();

		victim.HitBy.Clear();
		if (!credited) return null;

		victim.Deaths++;
		killer.Kills++;
		if (headshot) killer.Headshots++;
		foreach (long id in helpers) Find(id).Assists++;

		// Abate em aliado (fogo amigo ligado) conta para quem matou, mas
		// não pontua o time.
		if (killer.Team != victim.Team)
		{
			_score[killer.Team]++;
			if (_score[killer.Team] >= ScoreToWin) End();
		}

		return new RefKill(killerId, victimId, headshot, helpers);
	}

	private bool Recent(Seat victim, long attackerId) =>
		victim.HitBy.TryGetValue(attackerId, out double at) && _now - at <= CreditWindow;

	private void End()
	{
		Winner = _score[Blue] > _score[Orange] ? Blue : _score[Orange] > _score[Blue] ? Orange : 0;
		SetPhase(RefPhase.Ended);
	}

	private void SetPhase(RefPhase phase)
	{
		if (Phase == phase) return;
		Phase = phase;
		PhaseChanged?.Invoke(phase);
	}
}
