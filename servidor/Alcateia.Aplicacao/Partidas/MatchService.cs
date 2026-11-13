using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Frota;
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

	public MatchService(IMatchRepository matches, FleetService fleet, LobbyService lobby)
	{
		_matches = matches;
		_fleet = fleet;
		_lobby = lobby;
	}

	public async Task<SettledMatch> Report(MatchReport report)
	{
		var active = _fleet.Find(report.MatchKey);
		if (active == null || active.Value.ServerId != report.ServerId)
			throw new Failure(FailureKind.Conflict, "partida_desconhecida", "Essa partida não está em curso neste servidor.");

		SettledMatch settled = MatchSettlement.Settle(active.Value.Assignment, report);
		if (!await _matches.TrySave(settled))
			throw new Failure(FailureKind.Conflict, "partida_repetida", "Essa partida já foi registrada.");

		_fleet.Release(report.MatchKey);
		await _lobby.MatchEnded(settled);
		return settled;
	}

	public Task<IReadOnlyList<MatchHistoryEntry>> Recent(long accountId, int limit) =>
		_matches.Recent(accountId, Math.Clamp(limit, 1, 50));
}
