using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio.Frota;
using Alcateia.Dominio.Partidas;

namespace Alcateia.Aplicacao.Frota;

/// <summary>
/// A frota em uso: os servidores de partida batem, as salas pedem lugar,
/// o resultado libera. Fica em memória — servidor de partida é coisa de
/// agora; se este processo reiniciar, eles voltam a bater e a lista se
/// refaz. A regra é a do domínio (<see cref="Fleet"/>); aqui ficam a
/// trava, o relógio e o aviso de partida perdida.
/// </summary>
public sealed class FleetService
{
	private readonly Fleet _fleet;
	private readonly ITicketSigner _signer;
	private readonly IClock _clock;
	private readonly object _lock = new();
	private readonly IFleetScaler _scaler;
	private readonly int _reserve;
	private DateTime _wokeAt = DateTime.MinValue;

	/// <summary>Depois de ligar um servidor, espera isto antes de ligar
	/// outro: é o tempo de a máquina subir e bater ponto.</summary>
	public static readonly TimeSpan WakeEvery = TimeSpan.FromSeconds(45);

	/// <summary>Uma partida em curso acabou sem resultado (o servidor caiu,
	/// ou fechou a partida vazia): a sala dela volta a esperar.</summary>
	public event Action<LostMatch> Lost;

	/// <param name="scaler">Quem liga servidores sob demanda; null, nenhum.</param>
	/// <param name="reserve">Quantos lugares livres a frota tenta manter
	/// (para a próxima sala não esperar a máquina subir). Zero: só liga
	/// quando uma sala não acha lugar.</param>
	public FleetService(TimeSpan timeout, ITicketSigner signer, IClock clock, IFleetScaler scaler = null, int reserve = 0)
	{
		_fleet = new Fleet(timeout);
		_signer = signer;
		_clock = clock;
		_scaler = scaler;
		_reserve = Math.Max(reserve, 0);
	}

	/// <summary>Liga servidores sozinha quando falta lugar.</summary>
	public bool Scales => _scaler != null;

	/// <summary>Um servidor ocioso quer desligar: sim, se não faz falta
	/// (veja <see cref="Fleet.Retire"/>).</summary>
	public bool Retire(string serverId)
	{
		lock (_lock) return _fleet.Retire(serverId, _reserve, _clock.UtcNow);
	}

	// Sob a trava: decide se é hora de ligar mais um.
	private bool ShouldWake(bool full)
	{
		if (_scaler == null) return false;
		DateTime now = _clock.UtcNow;
		if (!full && _fleet.FreeSlots(now) >= Math.Max(_reserve, 0)) return false;
		if (now - _wokeAt < WakeEvery) return false;
		_wokeAt = now;
		return true;
	}

	// Fora da trava, sem esperar: quem pediu lugar não fica preso na API do Fly.
	private void Wake()
	{
		IFleetScaler scaler = _scaler;
		_ = Task.Run(async () =>
		{
			try { await scaler.Wake(); }
			catch (Exception e) { Console.Error.WriteLine($"Frota: não consegui ligar servidor ({e.Message})"); }
		});
	}

	/// <summary>Batida de um servidor de partida: devolve as partidas que ele deve abrir.</summary>
	public List<MatchAssignment> Heartbeat(Beat beat)
	{
		List<MatchAssignment> deliver;
		List<LostMatch> lost;
		lock (_lock) (deliver, lost) = _fleet.Heartbeat(beat, _clock.UtcNow);
		Announce(lost);
		return deliver;
	}

	/// <summary>Reserva lugar para a partida. Null se a frota está cheia.</summary>
	public (string Address, int Port)? Allocate(MatchAssignment assignment)
	{
		(string, int)? place;
		List<LostMatch> lost;
		bool wake;
		lock (_lock)
		{
			lost = _fleet.Sweep(_clock.UtcNow);
			place = _fleet.Allocate(assignment, _clock.UtcNow);
			wake = ShouldWake(full: place == null);
		}
		Announce(lost);
		if (wake) Wake();
		return place;
	}

	public (MatchAssignment Assignment, string ServerId)? Find(string matchKey)
	{
		lock (_lock) return _fleet.Find(matchKey);
	}

	/// <summary>Um lugar a mais numa partida em curso (<see cref="Fleet.AddSeat"/>).</summary>
	public bool AddSeat(string matchKey, MatchSeat seat)
	{
		lock (_lock) return _fleet.AddSeat(matchKey, seat);
	}

	/// <summary>Onde a partida está (<see cref="Fleet.Progress"/>).</summary>
	public MatchProgress Progress(string matchKey)
	{
		lock (_lock) return _fleet.Progress(matchKey);
	}

	/// <summary>O time de quem já está na escalação (0 se não está).</summary>
	public int SeatTeam(string matchKey, long accountId)
	{
		lock (_lock) return _fleet.SeatTeam(matchKey, accountId);
	}

	public void Release(string matchKey)
	{
		lock (_lock) _fleet.Release(matchKey);
	}

	/// <summary>Os servidores de partida e o que cada um está rodando.</summary>
	public List<ServerView> Servers()
	{
		lock (_lock) return _fleet.Servers();
	}

	public (int Servers, int Busy, int Capacity, int Running) Status()
	{
		(int, int, int, int) status;
		List<LostMatch> lost;
		bool wake;
		lock (_lock)
		{
			lost = _fleet.Sweep(_clock.UtcNow);
			status = _fleet.Status();
			wake = ShouldWake(full: false);
		}
		Announce(lost);
		if (wake) Wake();
		return status;
	}

	/// <summary>O ingresso de um jogador para uma partida: só quem tem o
	/// segredo da frota consegue fazer ou conferir.</summary>
	public string Ticket(string matchKey, long accountId, long expiresUnix) =>
		_signer.Sign($"{matchKey}|{accountId}|{expiresUnix}");

	// Fora da trava: quem ouve (o saguão) tem a trava dele.
	private void Announce(List<LostMatch> lost)
	{
		foreach (LostMatch l in lost) Lost?.Invoke(l);
	}
}
