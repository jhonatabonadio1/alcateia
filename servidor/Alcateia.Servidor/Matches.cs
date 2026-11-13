using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Servidor.Banco;

namespace Alcateia.Servidor;

/// <summary>
/// O fim de uma partida online. Quem conta é o servidor de partida
/// (o cliente não diz quanto ganhou): ele manda o placar e o que cada
/// um fez, e aqui se confere que a partida existia e era dele, calcula-se
/// o prêmio com a mesma regra do jogo (<see cref="Rewards"/>) e grava-se
/// tudo de uma vez — a partida, os jogadores e o perfil de cada um.
/// </summary>
public sealed class Matches
{
	private readonly PgPool _db;
	private readonly Fleet _fleet;
	private readonly Lobby _lobby;

	public Matches(PgPool db, Fleet fleet, Lobby lobby)
	{
		_db = db;
		_fleet = fleet;
		_lobby = lobby;
	}

	public async Task<JsonObject> Report(JsonObject body)
	{
		string matchKey = body["matchKey"]?.GetValue<string>() ?? "";
		string serverId = body["serverId"]?.GetValue<string>() ?? "";
		var active = _fleet.Find(matchKey);
		if (active == null || active.Value.ServerId != serverId)
			throw new ApiException(409, "partida_desconhecida", "Essa partida não está em curso neste servidor.");

		Assignment assignment = active.Value.Assignment;
		int scoreBlue = body["scoreBlue"]?.GetValue<int>() ?? 0, scoreOrange = body["scoreOrange"]?.GetValue<int>() ?? 0;
		int winner = scoreBlue == scoreOrange ? 0 : scoreBlue > scoreOrange ? 1 : 2;

		// Só entra quem estava escalado, uma vez cada, com números que cabem.
		var seats = assignment.Players.ToDictionary(p => p.AccountId);
		var lines = new List<(MatchSeat Seat, int Kills, int Deaths, int Assists, int Headshots, int Points)>();
		foreach (JsonNode node in body["players"] as JsonArray ?? new JsonArray())
		{
			long id = node["accountId"]?.GetValue<long>() ?? 0;
			if (!seats.TryGetValue(id, out MatchSeat seat) || lines.Any(l => l.Seat.AccountId == id)) continue;
			if (seat.IsBot) continue;   // bot não é conta: não ganha nada nem entra na partida gravada
			int N(string field, int max) => Math.Clamp(node[field]?.GetValue<int>() ?? 0, 0, max);
			lines.Add((seat, N("kills", Rewards.MaxKills), N("deaths", 999), N("assists", Rewards.MaxAssists), N("headshots", Rewards.MaxKills), N("points", 99999)));
		}

		long mvp = lines.Where(l => l.Points > 0).OrderByDescending(l => l.Points).Select(l => l.Seat.AccountId).FirstOrDefault();
		var gains = new Dictionary<long, JsonObject>();

		try
		{
			await _db.Transaction(async c =>
			{
				PgResult made = await c.Query(
					"insert into matches (match_key, server_id, room_title, map, mode, winner, score_blue, score_orange, started_at) " +
					"values ($1, $2, $3, $4, $5, $6, $7, $8, $9) returning id",
					matchKey, serverId, assignment.Settings["title"]?.GetValue<string>() ?? "", assignment.Settings["map"]?.GetValue<string>() ?? "",
					assignment.Settings["mode"]?.ToString() ?? "Equipe", winner, scoreBlue, scoreOrange, assignment.StartedAt);
				long matchId = made.Long(0, "id");

				foreach (var l in lines)
				{
					bool won = winner != 0 && l.Seat.Team == winner, draw = winner == 0, isMvp = l.Seat.AccountId == mvp;
					Rewards.Gain gain = Rewards.ForMatch(l.Kills, l.Assists, won, draw, isMvp);

					await c.Query(
						"insert into match_players (match_id, account_id, team, kills, deaths, assists, headshots, points, mvp, xp_gain, presas_gain) " +
						"values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)",
						matchId, l.Seat.AccountId, l.Seat.Team, l.Kills, l.Deaths, l.Assists, l.Headshots, l.Points, isMvp, gain.Xp, gain.Presas);

					await c.Query(
						"update profiles set xp = xp + $2, presas = presas + $3, matches = matches + 1, " +
						"wins = wins + $4, losses = losses + $5, draws = draws + $6, kills = kills + $7, deaths = deaths + $8, " +
						"assists = assists + $9, headshots = headshots + $10, mvps = mvps + $11, updated_at = now() where account_id = $1",
						l.Seat.AccountId, gain.Xp, gain.Presas, won ? 1 : 0, !won && !draw ? 1 : 0, draw ? 1 : 0,
						l.Kills, l.Deaths, l.Assists, l.Headshots, isMvp ? 1 : 0);

					gains[l.Seat.AccountId] = new JsonObject
					{
						["xp"] = gain.Xp, ["presas"] = gain.Presas, ["won"] = won, ["draw"] = draw, ["mvp"] = isMvp,
						["kills"] = l.Kills, ["deaths"] = l.Deaths, ["assists"] = l.Assists
					};
				}

				return matchId;
			});
		}
		catch (PgException e) when (e.IsUniqueViolation)
		{
			throw new ApiException(409, "partida_repetida", "Essa partida já foi registrada.");
		}

		_fleet.Release(matchKey);

		var summary = new JsonObject { ["matchKey"] = matchKey, ["winner"] = winner, ["scoreBlue"] = scoreBlue, ["scoreOrange"] = scoreOrange };
		await _lobby.MatchEnded(matchKey, summary, gains);

		var result = (JsonObject)summary.DeepClone();
		var players = new JsonObject();
		foreach (var (id, gain) in gains) players[id.ToString()] = gain.DeepClone();
		result["players"] = players;
		return result;
	}

	/// <summary>As últimas partidas de uma conta.</summary>
	public async Task<JsonArray> Recent(long accountId, int limit)
	{
		PgResult r = await _db.Query(
			"select m.map, m.winner, m.score_blue, m.score_orange, m.ended_at, p.team, p.kills, p.deaths, p.assists, p.mvp, p.xp_gain, p.presas_gain " +
			"from match_players p join matches m on m.id = p.match_id where p.account_id = $1 order by p.match_id desc limit $2",
			accountId, Math.Clamp(limit, 1, 50));

		var list = new JsonArray();
		for (int i = 0; i < r.Rows.Count; i++)
			list.Add(new JsonObject
			{
				["map"] = r.Text(i, "map"), ["winner"] = r.Int(i, "winner"), ["scoreBlue"] = r.Int(i, "score_blue"),
				["scoreOrange"] = r.Int(i, "score_orange"), ["endedAt"] = r.Text(i, "ended_at"), ["team"] = r.Int(i, "team"),
				["kills"] = r.Int(i, "kills"), ["deaths"] = r.Int(i, "deaths"), ["assists"] = r.Int(i, "assists"),
				["mvp"] = r.Bool(i, "mvp"), ["xp"] = r.Long(i, "xp_gain"), ["presas"] = r.Long(i, "presas_gain")
			});
		return list;
	}
}
