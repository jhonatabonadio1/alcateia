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

		/// <summary>Abates feitos como lobo (os desafios "como lobo" e
		/// "como humano" do servidor contam por aqui).</summary>
		public int KillsWolf;

		/// <summary>Acertos que o servidor de partida recusou (decisão 0016):
		/// vai no resultado, e o servidor central desconfia de quem tem muitos.</summary>
		public int RejectedHits;

		/// <summary>O rage (em chamas): o tempo em chamas vai no resultado e vira XP.</summary>
		public readonly Rage Rage = new();
		public int Points => Kills * PointsPerKill + Assists * PointsPerAssist;

		internal readonly Dictionary<long, double> HitBy = new();
		internal double DiedAt = double.NegativeInfinity;
	}

	/// <summary>Espera por todos este tempo depois do primeiro chegar.</summary>
	public double StartAfter = 20;

	/// <summary>Sem ninguém por este tempo, a partida acaba (e o lugar no
	/// servidor de partida vaga). Curto: quem saiu de todo não volta, e a
	/// sala precisa poder jogar de novo logo; dá para cair e voltar.</summary>
	public double EmptyTimeout = 20;

	public RefPhase Phase { get; private set; } = RefPhase.Waiting;
	public double CountdownLeft { get; private set; }
	public double TimeLeft { get; private set; }
	/// <summary>Quanto a partida dura, inteira (segundos).</summary>
	public double Duration { get; }
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
		TimeLeft = Duration = seconds;
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

	/// <summary>Um lugar novo com a partida em curso (alguém entrou na sala
	/// com ela jogando; Jhon, 24/11). Começa zerado. Quem jogou e saiu volta
	/// para o mesmo lugar, com o que já tinha feito: falso, nada muda — a não
	/// ser que tenha escolhido o outro time na sala (25/11); aí, desligado, o
	/// lugar muda de time (os abates dele ficam com ele). Falso também se a
	/// partida acabou.</summary>
	public bool AddSeat(long id, string name, int team, long owner = 0)
	{
		if (Phase == RefPhase.Ended) return false;
		if (_seats.TryGetValue(id, out Seat old))
		{
			if (old.Connected || old.IsBot || team is not (Blue or Orange) || old.Team == team) return false;
			old.Team = team;
			return true;
		}
		_seats[id] = new Seat { Id = id, Name = name, Team = team is Blue or Orange ? team : Blue, Owner = owner };
		return true;
	}

	/// <summary>
	/// Quem sai deixa os bots dele com o próximo jogador ligado, na ordem da
	/// escalação (Jhon, 24/11: "se eu sair da partida, o mestre passa a ser o
	/// próximo jogador"; é a máquina do mestre que move os bots). Devolve o
	/// novo dono (0 se não há bot dele ou ninguém para ficar com eles: aí eles
	/// saem junto, como antes).
	/// </summary>
	public long HandBots(long from)
	{
		List<Seat> bots = BotsOf(from).ToList();
		if (bots.Count == 0) return 0;
		Seat next = _seats.Values.FirstOrDefault(s => !s.IsBot && s.Connected && s.Id != from);
		if (next == null) return 0;
		foreach (Seat bot in bots)
		{
			bot.Owner = next.Id;
			bot.Connected = bot.EverConnected = true;
		}
		return next.Id;
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
				foreach (Seat s in _seats.Values) s.Rage.Tick((float)delta);
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
		if (!CanHit(attackerId, victimId, amount)) return false;
		Find(victimId).HitBy[attackerId] = _now;
		return true;
	}

	/// <summary>O acerto pode valer (partida valendo, os dois ligados,
	/// inimigos ou fogo amigo ligado, dano possível) — sem anotar nada.</summary>
	public bool CanHit(long attackerId, long victimId, float amount)
	{
		if (Phase != RefPhase.Live || attackerId == victimId) return false;
		if (!(amount > 0f) || amount > MaxHit) return false;

		Seat attacker = Find(attackerId), victim = Find(victimId);
		if (attacker == null || victim == null || !attacker.Connected || !victim.Connected) return false;
		return attacker.Team != victim.Team || FriendlyFire;
	}

	/// <summary>
	/// Alguém diz que morreu, e por quem. O abate só vale se o autor
	/// acertou a vítima há pouco; quem ajudou, idem. Morte sem autor
	/// (queda, a própria granada) não pontua ninguém: é a
	/// <see cref="SelfDeath"/>. Nulo se não houve abate.
	/// </summary>
	public RefKill? Death(long victimId, long killerId, bool headshot, IEnumerable<long> assisters, bool killerWasWolf = false)
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
		if (killerWasWolf) killer.KillsWolf++;
		if (headshot) killer.Headshots++;
		foreach (long id in helpers) Find(id).Assists++;
		if (killer.Team != victim.Team)
		{
			killer.Rage.Kill();
			foreach (long id in helpers) Find(id).Rage.Assist();
		}

		// Abate em aliado (fogo amigo ligado) conta para quem matou, mas
		// não pontua o time.
		if (killer.Team != victim.Team)
		{
			_score[killer.Team]++;
			if (_score[killer.Team] >= ScoreToWin) End();
		}

		return new RefKill(killerId, victimId, headshot, helpers);
	}

	/// <summary>
	/// Alguém morreu sozinho (a própria granada, a queda): a morte conta para
	/// ele, sem abate nem ponto para ninguém, como no jogo offline (Jhon,
	/// 25/11). Falso se não vale: partida parada, ou já morreu neste segundo.
	/// </summary>
	public bool SelfDeath(long victimId)
	{
		Seat victim = Find(victimId);
		if (Phase != RefPhase.Live || victim == null || !victim.Connected) return false;
		if (_now - victim.DiedAt < 1.0) return false;
		victim.DiedAt = _now;
		victim.HitBy.Clear();
		victim.Deaths++;
		return true;
	}

	/// <summary>Quem acertou a vítima há pouco (a janela do crédito): é a
	/// lista de assistências quando é o servidor que decide a morte.</summary>
	public long[] RecentHitters(long victimId)
	{
		Seat victim = Find(victimId);
		return victim == null ? Array.Empty<long>()
			: victim.HitBy.Where(e => _now - e.Value <= CreditWindow).Select(e => e.Key).ToArray();
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
