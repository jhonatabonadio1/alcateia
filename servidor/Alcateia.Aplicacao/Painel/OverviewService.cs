using System.Collections.Generic;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Frota;
using Alcateia.Aplicacao.Portas;
using Alcateia.Aplicacao.Saguao;
using Alcateia.Aplicacao.Seguranca;
using Alcateia.Dominio.Frota;
using Alcateia.Dominio.Painel;

namespace Alcateia.Aplicacao.Painel;

/// <summary>O servidor num relance: o que está acontecendo agora e os
/// números do banco.</summary>
public sealed record Overview(int Online, int Rooms, int RoomsPlaying, int FleetServers, int FleetSlots, int MatchesRunning,
	ServerNumbers Numbers, SecurityOverview Security, int IpBlocks, SecurityCounters Counters);

/// <summary>A visão geral do painel e a frota.</summary>
public sealed class OverviewService
{
	private readonly IPlayerRepository _players;
	private readonly LobbyService _lobby;
	private readonly FleetService _fleet;
	private readonly SecurityService _security;

	public OverviewService(IPlayerRepository players, LobbyService lobby, FleetService fleet, SecurityService security)
	{
		_players = players;
		_lobby = lobby;
		_fleet = fleet;
		_security = security;
	}

	public async Task<Overview> Now(AdminContext who)
	{
		who.Require(Permission.View);
		var (rooms, playing) = _lobby.RoomCount();
		var (servers, _, slots, running) = _fleet.Status();
		return new Overview(_lobby.Online, rooms, playing, servers, slots, running, await _players.Numbers(),
			await _security.Overview(24), _security.Blocks.Count, _security.Counters);
	}

	public List<ServerView> Fleet(AdminContext who)
	{
		who.Require(Permission.View);
		return _fleet.Servers();
	}
}
