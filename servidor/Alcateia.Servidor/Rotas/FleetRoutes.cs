using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Alcateia.Dominio.Frota;
using Alcateia.Dominio.Partidas;
using Alcateia.Servidor.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Alcateia.Servidor.Rotas;

/// <summary>
/// A batida dos servidores de partida (<c>X-Fleet-Key</c>):
///
/// <c>{ id, address, port, players, capacity, matches: [chaves abertas] }</c>
/// → <c>{ ok, assignments: [partidas novas], assignment: a primeira }</c>.
///
/// Sem <c>capacity</c> vale uma partida; sem <c>matches</c> (servidor
/// antigo) nada é conferido. <c>assignment</c> fica pelos servidores
/// antigos, que só entendem uma.
/// </summary>
public static class FleetRoutes
{
	public static void Map(IEndpointRouteBuilder app, Services s)
	{
		app.MapPost("/v1/fleet/heartbeat", async ctx =>
		{
			Api.RequireGameServer(ctx, s.Config);
			JsonObject body = await Api.Body(ctx);

			string address = Api.Text(body, "address");
			if (string.IsNullOrEmpty(address)) address = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
			List<string> open = (body["matches"] as JsonArray)?.Select(n => n?.GetValue<string>()).Where(k => k != null).ToList();

			var beat = new Beat(Api.Text(body, "id"), address, Api.Int(body, "port"), Api.Int(body, "players"), Api.Int(body, "capacity", 1), open);
			List<MatchAssignment> deliver = s.Fleet.Heartbeat(beat);

			var assignments = new JsonArray();
			foreach (MatchAssignment a in deliver) assignments.Add(ToJson(a));
			var reply = new JsonObject { ["ok"] = true, ["assignments"] = assignments };
			if (deliver.Count > 0) reply["assignment"] = ToJson(deliver[0]);
			await Api.Send(ctx, reply);
		});

		// O servidor de partida ocioso pergunta se pode desligar
		// (ALCATEIA_IDLE_EXIT). "leave": true e ele já saiu da lista.
		app.MapPost("/v1/fleet/leave", async ctx =>
		{
			Api.RequireGameServer(ctx, s.Config);
			JsonObject body = await Api.Body(ctx);
			bool leave = s.Fleet.Retire(Api.Text(body, "id"));
			await Api.Send(ctx, new JsonObject { ["ok"] = true, ["leave"] = leave });
		});
	}

	private static JsonObject ToJson(MatchAssignment a)
	{
		var players = new JsonArray();
		foreach (MatchSeat p in a.Players)
			players.Add(new JsonObject { ["accountId"] = p.AccountId, ["name"] = p.Name, ["team"] = p.Team, ["owner"] = p.Owner });
		return new JsonObject { ["matchKey"] = a.MatchKey, ["settings"] = a.Settings.DeepClone(), ["players"] = players };
	}
}
