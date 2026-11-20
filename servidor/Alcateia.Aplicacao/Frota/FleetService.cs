using System;
using System.Collections.Generic;
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

	/// <summary>Uma partida em curso acabou sem resultado (o servidor caiu,
	/// ou fechou a partida vazia): a sala dela volta a esperar.</summary>
	public event Action<LostMatch> Lost;

	public FleetService(TimeSpan timeout, ITicketSigner signer, IClock clock)
	{
		_fleet = new Fleet(timeout);
		_signer = signer;
		_clock = clock;
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
		lock (_lock)
		{
			lost = _fleet.Sweep(_clock.UtcNow);
			place = _fleet.Allocate(assignment, _clock.UtcNow);
		}
		Announce(lost);
		return place;
	}

	public (MatchAssignment Assignment, string ServerId)? Find(string matchKey)
	{
		lock (_lock) return _fleet.Find(matchKey);
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
		lock (_lock)
		{
			lost = _fleet.Sweep(_clock.UtcNow);
			status = _fleet.Status();
		}
		Announce(lost);
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
