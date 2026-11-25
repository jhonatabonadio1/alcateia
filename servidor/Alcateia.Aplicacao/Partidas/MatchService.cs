using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Alcateia.Aplicacao.Frota;
using Alcateia.Aplicacao.Jogo;
using Alcateia.Aplicacao.Seguranca;
using Alcateia.Dominio.Seguranca;
using Alcateia.Aplicacao.Portas;
using Alcateia.Aplicacao.Saguao;
using Alcateia.Dominio;
using Alcateia.Dominio.Partidas;

namespace Alcateia.Aplicacao.Partidas;

/// <summary>
/// O fim de uma partida online. Quem conta é o servidor de partida: aqui
/// se confere que a partida existia e era dele, fecha-se o resultado com
/// a regra do domínio (<see cref="MatchSettlement"/>), grava-se tudo de
/// uma vez, libera-se o lugar na frota e a sala volta a esperar, com cada
/// um sabendo o que ganhou.
/// </summary>
public sealed class MatchService
{
	private readonly IMatchRepository _matches;
	private readonly FleetService _fleet;
	private readonly LobbyService _lobby;
	private readonly GameDataService _data;
	private readonly ChallengeService _challenges;
	private readonly SecurityService _security;
	private readonly IClock _clock;

	public MatchService(IMatchRepository matches, FleetService fleet, LobbyService lobby, GameDataService data,
		ChallengeService challenges, SecurityService security, IClock clock)
	{
		_matches = matches;
		_fleet = fleet;
		_lobby = lobby;
		_data = data;
		_challenges = challenges;
		_security = security;
		_clock = clock;
	}

	public async Task<SettledMatch> Report(MatchReport report)
	{
		var active = _fleet.Find(report.MatchKey);
		if (active == null || active.Value.ServerId != report.ServerId)
			throw new Failure(FailureKind.Conflict, "partida_desconhecida", "Essa partida não está em curso neste servidor.");

		DateTime now = _clock.UtcNow;
		var (xpBonus, presasBonus) = _data.Current.Bonus(now);
		SettledMatch settled = MatchSettlement.Settle(active.Value.Assignment, report, xpBonus, presasBonus, _data.Current.Cash());
		if (!await _matches.TrySave(settled))
			throw new Failure(FailureKind.Conflict, "partida_repetida", "Essa partida já foi registrada.");

		_fleet.Release(report.MatchKey);
		await _lobby.MatchEnded(settled);

		// Depois de gravado: nada daqui para baixo desfaz a partida.
		var seated = settled.Lines.Select(l => l.Seat.AccountId).ToHashSet();
		foreach (var (accountId, what) in Plausibility.Flags(report, (now - active.Value.Assignment.StartedAt).TotalMinutes))
			if (seated.Contains(accountId))
				_security.Record(EventKind.Cheat, Severity.High, "", accountId, report.ServerId, "/v1/matches", $"{what} (partida {report.MatchKey})");

		await _challenges.Apply(settled);
		return settled;
	}

	public Task<IReadOnlyList<MatchHistoryEntry>> Recent(long accountId, int limit) =>
		_matches.Recent(accountId, Math.Clamp(limit, 1, 50));
}
