using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Alcateia.Dominio.Frota;
using Alcateia.Dominio.Partidas;
using Alcateia.Dominio.Seguranca;
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
///
/// A batida também pode trazer <c>events: [{ kind, severity, match,
/// account, detail }]</c>: o que deu errado no servidor de partida (jogador
/// recusado, ligação que caiu, partida vazia, resultado que não saiu...).
/// Vão para o registro de segurança, e o painel mostra em "Segurança →
/// Partidas".
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
			RecordIncidents(s, beat.Id, address, body["events"] as JsonArray);
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
			string serverId = Api.Text(body, "id");
			bool leave = s.Fleet.Retire(serverId);
			if (leave) s.Security.Record(EventKind.MatchServerStopped, Severity.Info, ClientIp.Of(ctx), actor: serverId, detail: "ocioso e a frota não precisa dele: desligou");
			await Api.Send(ctx, new JsonObject { ["ok"] = true, ["leave"] = leave });
		});
	}

	/// <summary>Quantos incidentes uma batida pode trazer; o excesso fica só
	/// no servidor de partida (que guarda uma fila pequena).</summary>
	public const int MaxIncidentsPerBeat = 20;

	/// <summary>Põe no registro de segurança os incidentes que o servidor de
	/// partida contou. Só entram os tipos que o painel conhece. Nunca recusa a
	/// batida por causa deles: um evento torto é pulado — se a batida falhasse,
	/// o servidor de partida repetiria o mesmo evento para sempre.</summary>
	private static void RecordIncidents(Services s, string serverId, string address, JsonArray events)
	{
		if (events == null) return;

		foreach (JsonNode node in events.Take(MaxIncidentsPerBeat))
		{
			try
			{
				if (node is not JsonObject e) continue;
				string kind = e["kind"]?.GetValue<string>() ?? "";
				if (Array.IndexOf(EventKind.MatchReported, kind) < 0) continue;

				int severity = Math.Clamp(e["severity"]?.GetValue<int>() ?? Severity.Warning, Severity.Info, Severity.Critical);
				long account = e["account"]?.GetValue<long>() ?? 0;
				string match = e["match"]?.GetValue<string>() ?? "";
				string detail = e["detail"]?.GetValue<string>() ?? "";
				s.Security.Record(kind, severity, address, account > 0 ? (long?)account : null, serverId, match, detail);
			}
			catch (Exception ex) when (ex is InvalidOperationException or FormatException)
			{
				// Evento torto: pula.
			}
		}
	}

	private static JsonObject ToJson(MatchAssignment a)
	{
		var players = new JsonArray();
		foreach (MatchSeat p in a.Players)
			players.Add(new JsonObject { ["accountId"] = p.AccountId, ["name"] = p.Name, ["team"] = p.Team, ["owner"] = p.Owner });
		var json = new JsonObject { ["matchKey"] = a.MatchKey, ["settings"] = a.Settings.DeepClone(), ["players"] = players };
		if (a.Update) json["update"] = true;
		return json;
	}
}
