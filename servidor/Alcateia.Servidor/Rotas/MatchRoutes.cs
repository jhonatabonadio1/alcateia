using System.Collections.Generic;
using System.Text.Json.Nodes;
using Alcateia.Aplicacao.Saguao;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Partidas;
using Alcateia.Servidor.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Alcateia.Servidor.Rotas;

/// <summary>O resultado de uma partida (servidor de partida) e o histórico (jogador).</summary>
public static class MatchRoutes
{
	public static void Map(IEndpointRouteBuilder app, Services s)
	{
		app.MapPost("/v1/matches", async ctx =>
		{
			Api.RequireGameServer(ctx, s.Config);
			SettledMatch settled = await s.Matches.Report(ReadReport(await Api.Body(ctx)));

			var players = new JsonObject();
			foreach (SettledLine l in settled.Lines) players[l.Seat.AccountId.ToString()] = LobbyService.Gain(l);
			await Api.Send(ctx, new JsonObject
			{
				["matchKey"] = settled.Assignment.MatchKey, ["winner"] = settled.Winner,
				["scoreBlue"] = settled.ScoreBlue, ["scoreOrange"] = settled.ScoreOrange, ["players"] = players
			});
		});

		app.MapGet("/v1/matches", async ctx =>
		{
			Account account = await s.Accounts.Require(Api.Token(ctx));
			var list = new JsonArray();
			foreach (MatchHistoryEntry m in await s.Matches.Recent(account.Id, 20))
				list.Add(new JsonObject
				{
					["map"] = m.Map, ["winner"] = m.Winner, ["scoreBlue"] = m.ScoreBlue, ["scoreOrange"] = m.ScoreOrange,
					["endedAt"] = m.EndedAt, ["team"] = m.Team, ["kills"] = m.Kills, ["deaths"] = m.Deaths,
					["assists"] = m.Assists, ["mvp"] = m.Mvp, ["xp"] = m.Xp, ["presas"] = m.Presas
				});
			await Api.Send(ctx, new JsonObject { ["matches"] = list });
		});
	}

	private static MatchReport ReadReport(JsonObject body)
	{
		var lines = new List<ReportedLine>();
		foreach (JsonNode node in body["players"] as JsonArray ?? new JsonArray())
		{
			int N(string field) => node[field]?.GetValue<int>() ?? 0;
			lines.Add(new ReportedLine(node["accountId"]?.GetValue<long>() ?? 0, N("kills"), N("deaths"), N("assists"), N("headshots"), N("points"))
			{
				KillsWolf = N("killsWolf"),
				RejectedHits = N("rejectedHits"),
				AimHits = N("aimHits"),
				AimSnaps = N("aimSnaps"),
				RefusedForms = N("refusedForms"),
				FireSeconds = N("fire")
			});
		}
		return new MatchReport(Api.Text(body, "matchKey"), Api.Text(body, "serverId"), Api.Int(body, "scoreBlue"), Api.Int(body, "scoreOrange"), lines);
	}
}
