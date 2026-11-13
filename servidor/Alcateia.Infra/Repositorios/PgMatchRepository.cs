using System.Collections.Generic;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio.Partidas;
using Alcateia.Infra.Banco;

namespace Alcateia.Infra.Repositorios;

/// <summary>Partidas jogadas no PostgreSQL (tabelas matches e
/// match_players) e o prêmio somado no perfil de cada um.</summary>
public sealed class PgMatchRepository : IMatchRepository
{
	private readonly PgPool _db;

	public PgMatchRepository(PgPool db) => _db = db;

	public async Task<bool> TrySave(SettledMatch m)
	{
		MatchAssignment a = m.Assignment;
		try
		{
			await _db.Transaction(async c =>
			{
				PgResult made = await c.Query(
					"insert into matches (match_key, server_id, room_title, map, mode, winner, score_blue, score_orange, started_at) " +
					"values ($1, $2, $3, $4, $5, $6, $7, $8, $9) returning id",
					a.MatchKey, m.ServerId, a.Title, a.Map, a.Mode, m.Winner, m.ScoreBlue, m.ScoreOrange, a.StartedAt);
				long matchId = made.Long(0, "id");

				foreach (SettledLine l in m.Lines)
				{
					await c.Query(
						"insert into match_players (match_id, account_id, team, kills, deaths, assists, headshots, points, mvp, xp_gain, presas_gain) " +
						"values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)",
						matchId, l.Seat.AccountId, l.Seat.Team, l.Kills, l.Deaths, l.Assists, l.Headshots, l.Points, l.Mvp, l.Gain.Xp, l.Gain.Presas);

					await c.Query(
						"update profiles set xp = xp + $2, presas = presas + $3, matches = matches + 1, " +
						"wins = wins + $4, losses = losses + $5, draws = draws + $6, kills = kills + $7, deaths = deaths + $8, " +
						"assists = assists + $9, headshots = headshots + $10, mvps = mvps + $11, updated_at = now() where account_id = $1",
						l.Seat.AccountId, l.Gain.Xp, l.Gain.Presas, l.Won ? 1 : 0, !l.Won && !l.Draw ? 1 : 0, l.Draw ? 1 : 0,
						l.Kills, l.Deaths, l.Assists, l.Headshots, l.Mvp ? 1 : 0);
				}
				return matchId;
			});
			return true;
		}
		catch (PgException e) when (e.IsUniqueViolation)
		{
			return false;
		}
	}

	public async Task<IReadOnlyList<MatchHistoryEntry>> Recent(long accountId, int limit)
	{
		PgResult r = await _db.Query(
			"select m.map, m.winner, m.score_blue, m.score_orange, m.ended_at, p.team, p.kills, p.deaths, p.assists, p.mvp, p.xp_gain, p.presas_gain " +
			"from match_players p join matches m on m.id = p.match_id where p.account_id = $1 order by p.match_id desc limit $2",
			accountId, limit);

		var list = new List<MatchHistoryEntry>();
		for (int i = 0; i < r.Rows.Count; i++)
			list.Add(new MatchHistoryEntry(r.Text(i, "map"), r.Int(i, "winner"), r.Int(i, "score_blue"), r.Int(i, "score_orange"),
				r.Text(i, "ended_at"), r.Int(i, "team"), r.Int(i, "kills"), r.Int(i, "deaths"), r.Int(i, "assists"),
				r.Bool(i, "mvp"), r.Long(i, "xp_gain"), r.Long(i, "presas_gain")));
		return list;
	}
}
