using System.Linq;
using System.Text.Json.Nodes;
using Alcateia.Aplicacao.Painel;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio.Painel;
using Alcateia.Dominio.Seguranca;
using Alcateia.Servidor.Http;
using Microsoft.AspNetCore.Routing;

namespace Alcateia.Servidor.Rotas;

/// <summary>Painel: a segurança — o resumo, os eventos, a blacklist de
/// IP, os banimentos e a auditoria do próprio painel.</summary>
public static class AdminSecurityRoutes
{
	public static void Map(IEndpointRouteBuilder app, Services s)
	{
		app.Get(s, "/security/overview", async (ctx, who) =>
		{
			who.Require(Permission.View);
			int.TryParse(ctx.Request.Query["hours"], out int hours);
			return ToJson(await s.Security.Overview(hours == 0 ? 24 : hours));
		});

		app.Get(s, "/security/events", async (ctx, who) =>
		{
			who.Require(Permission.View);
			int.TryParse(ctx.Request.Query["severity"], out int severity);
			long.TryParse(ctx.Request.Query["before"], out long before);
			var events = await s.Security.Events(new EventFilter(ctx.Request.Query["kind"], ctx.Request.Query["ip"], severity, before, 100));
			return new JsonObject { ["events"] = new JsonArray(events.Select(e => (JsonNode)ToJson(e)).ToArray()) };
		});

		app.Get(s, "/security/blocks", (ctx, who) =>
		{
			who.Require(Permission.View);
			return System.Threading.Tasks.Task.FromResult<JsonNode>(new JsonObject
			{
				["blocks"] = new JsonArray(s.Security.Blocks.Select(b => (JsonNode)ToJson(b)).ToArray())
			});
		});

		app.Post(s, "/security/blocks", async (ctx, who, body) =>
		{
			who.Require(Permission.Moderate);
			IpBlock block = await s.Security.Block(Api.Text(body, "cidr"), Api.Text(body, "reason"), AdminApi.Number(body, "hours"), "admin", who.Admin.Name);
			await s.Audit.Note(who, "ip.bloqueou", block.Cidr, $"{(block.Until == null ? "para sempre" : "até " + Api.Date(block.Until))}; motivo: {block.Reason}");
			return ToJson(block);
		});

		app.Delete(s, "/security/blocks/{id}", async (ctx, who) =>
		{
			who.Require(Permission.Moderate);
			IpBlock removed = await s.Security.Unblock(AdminApi.RouteId(ctx));
			if (removed != null) await s.Audit.Note(who, "ip.desbloqueou", removed.Cidr, $"era: {removed.Reason} ({removed.Source})");
			return null;
		});

		app.Get(s, "/security/bans", async (ctx, who) =>
		{
			who.Require(Permission.View);
			var bans = await s.Security.Bans(ctx.Request.Query["all"] != "1");
			return new JsonObject { ["bans"] = new JsonArray(bans.Select(b => (JsonNode)ToJson(b)).ToArray()) };
		});

		app.Get(s, "/audit", async (ctx, who) =>
		{
			who.Require(Permission.View);
			long.TryParse(ctx.Request.Query["before"], out long before);
			var log = await s.Audit.Log(100, before, ctx.Request.Query["q"]);
			return new JsonObject
			{
				["entries"] = new JsonArray(log.Select(a => (JsonNode)new JsonObject
				{
					["id"] = a.Id, ["at"] = Api.Date(a.At), ["admin"] = a.AdminName, ["action"] = a.Action, ["target"] = a.Target,
					["detail"] = a.Detail, ["ip"] = a.Ip
				}).ToArray())
			};
		});
	}

	public static JsonObject ToJson(SecurityOverview o) => new()
	{
		["byHour"] = new JsonArray(o.ByHour.Select(h => (JsonNode)new JsonObject
		{
			["hour"] = Api.Date(h.Hour), ["info"] = h.Info, ["warning"] = h.Warning, ["high"] = h.High, ["critical"] = h.Critical
		}).ToArray()),
		["byKind"] = new JsonArray(o.ByKind.Select(k => (JsonNode)new JsonObject { ["kind"] = k.Kind, ["count"] = k.Count }).ToArray()),
		["topIps"] = new JsonArray(o.TopIps.Select(t => (JsonNode)new JsonObject { ["ip"] = t.Ip, ["count"] = t.Count, ["worst"] = t.Worst }).ToArray())
	};

	public static JsonObject ToJson(SecurityEvent e) => new()
	{
		["id"] = e.Id, ["at"] = Api.Date(e.At), ["kind"] = e.Kind, ["severity"] = e.Severity, ["ip"] = e.Ip, ["accountId"] = e.AccountId,
		["actor"] = e.Actor, ["path"] = e.Path, ["detail"] = e.Detail
	};

	public static JsonObject ToJson(IpBlock b) => new()
	{
		["id"] = b.Id, ["cidr"] = b.Cidr, ["reason"] = b.Reason, ["source"] = b.Source, ["createdAt"] = Api.Date(b.CreatedAt),
		["until"] = Api.Date(b.Until), ["admin"] = b.AdminName
	};

	public static JsonObject ToJson(Ban b) => new()
	{
		["id"] = b.Id, ["accountId"] = b.AccountId, ["account"] = b.AccountName, ["reason"] = b.Reason, ["createdAt"] = Api.Date(b.CreatedAt),
		["until"] = Api.Date(b.Until), ["admin"] = b.AdminName, ["liftedAt"] = Api.Date(b.LiftedAt), ["liftedBy"] = b.LiftedBy,
		["active"] = b.ActiveAt(System.DateTime.UtcNow)
	};
}
