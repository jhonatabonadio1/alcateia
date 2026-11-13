using System;
using System.Text.Json.Nodes;
using Alcateia.Infra.Banco;
using Alcateia.Servidor.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Alcateia.Servidor.Rotas;

/// <summary>Está de pé? O banco responde? Quantos online, como está a frota.</summary>
public static class HealthRoutes
{
	public static void Map(IEndpointRouteBuilder app, Services s)
	{
		app.MapGet("/health", async ctx =>
		{
			bool dbOk = true;
			try { await s.Db.Query("select 1"); }
			catch (Exception) { dbOk = false; }

			var (servers, busy, capacity, running) = s.Fleet.Status();
			ctx.Response.StatusCode = dbOk ? 200 : 503;
			await Api.Send(ctx, new JsonObject
			{
				["status"] = dbOk ? "ok" : "sem_banco", ["version"] = App.Version, ["schema"] = Schema.Latest,
				["online"] = s.Lobby.Online,
				["fleet"] = new JsonObject { ["servers"] = servers, ["busy"] = busy, ["slots"] = capacity, ["matches"] = running }
			});
		});
	}
}
