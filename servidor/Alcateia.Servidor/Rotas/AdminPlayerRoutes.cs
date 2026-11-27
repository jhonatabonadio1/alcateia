using System.Linq;
using System.Text.Json.Nodes;
using Alcateia.Aplicacao.Painel;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Loja;
using Alcateia.Dominio.Partidas;
using Alcateia.Dominio.Seguranca;
using Alcateia.Servidor.Http;
using Microsoft.AspNetCore.Routing;

namespace Alcateia.Servidor.Rotas;

/// <summary>Painel: os jogadores — procurar, a ficha, banir, derrubar,
/// dar e tirar moeda e item.</summary>
public static class AdminPlayerRoutes
{
	public static void Map(IEndpointRouteBuilder app, Services s)
	{
		app.Get(s, "/players", async (ctx, who) =>
		{
			var found = await s.Players.Search(who, ctx.Request.Query["q"], ctx.Request.Query["banned"] == "1");
			return new JsonObject { ["players"] = new JsonArray(found.Select(p => (JsonNode)ToJson(p, s)).ToArray()) };
		});

		app.Get(s, "/players/{id}", async (ctx, who) =>
		{
			PlayerFile f = await s.Players.File(who, AdminApi.RouteId(ctx));
			JsonObject player = ToJson(f.Player.Summary, s);
			player["online"] = f.Online;
			player["lastIp"] = f.Player.LastIp;
			player["banReason"] = f.Player.BanReason;
			var stats = new JsonObject();
			foreach (string name in Profile.StatNames) stats[name] = f.Player.Stats[name];
			player["stats"] = stats;

			return new JsonObject
			{
				["player"] = player,
				["inventory"] = new JsonArray(f.Inventory.Select(i => (JsonNode)GameRoutes.ToJson(i)).ToArray()),
				["ledger"] = new JsonArray(f.Ledger.Select(l => (JsonNode)new JsonObject
				{
					["id"] = l.Id, ["at"] = Api.Date(l.At), ["currency"] = l.Currency, ["amount"] = l.Amount, ["balance"] = l.BalanceAfter,
					["reason"] = l.Reason, ["ref"] = l.Ref
				}).ToArray()),
				["bans"] = new JsonArray(f.Bans.Select(b => (JsonNode)AdminSecurityRoutes.ToJson(b)).ToArray()),
				["matches"] = new JsonArray(f.Matches.Select(m => (JsonNode)new JsonObject
				{
					["map"] = m.Map, ["winner"] = m.Winner, ["scoreBlue"] = m.ScoreBlue, ["scoreOrange"] = m.ScoreOrange, ["endedAt"] = m.EndedAt,
					["team"] = m.Team, ["kills"] = m.Kills, ["deaths"] = m.Deaths, ["assists"] = m.Assists, ["mvp"] = m.Mvp, ["xp"] = m.Xp, ["presas"] = m.Presas
				}).ToArray()),
				["events"] = new JsonArray(f.Events.Select(e => (JsonNode)AdminSecurityRoutes.ToJson(e)).ToArray()),
				["sameIp"] = new JsonArray(f.SameIp.Where(a => a.Id != f.Player.Summary.Id)
					.Select(a => (JsonNode)new JsonObject { ["id"] = a.Id, ["name"] = a.Name }).ToArray())
			};
		});

		app.Post(s, "/players/{id}/ban", async (ctx, who, body) =>
			AdminSecurityRoutes.ToJson(await s.Players.Ban(who, AdminApi.RouteId(ctx), Api.Text(body, "reason"), AdminApi.Number(body, "hours"))));

		app.Post(s, "/players/{id}/unban", async (ctx, who, body) =>
		{
			await s.Players.Unban(who, AdminApi.RouteId(ctx));
			return null;
		});

		app.Post(s, "/players/{id}/kick", async (ctx, who, body) =>
		{
			await s.Players.Kick(who, AdminApi.RouteId(ctx));
			return null;
		});

		app.Post(s, "/players/{id}/currency", async (ctx, who, body) => new JsonObject
		{
			["balance"] = await s.Players.Adjust(who, AdminApi.RouteId(ctx), Api.Text(body, "currency"), body["amount"]?.GetValue<long>() ?? 0, Api.Text(body, "reason"))
		});

		app.Post(s, "/players/{id}/level", async (ctx, who, body) =>
		{
			var (level, xp) = await s.Players.SetLevel(who, AdminApi.RouteId(ctx), Api.Int(body, "level"), Api.Text(body, "reason"));
			return new JsonObject { ["level"] = level, ["xp"] = xp };
		});

		app.Post(s, "/players/{id}/items", async (ctx, who, body) =>
		{
			await s.Players.GrantItem(who, AdminApi.RouteId(ctx), Api.Text(body, "itemId"), Api.Int(body, "days"));
			return null;
		});

		app.Delete(s, "/players/{id}/items/{item}", async (ctx, who) =>
		{
			await s.Players.RevokeItem(who, AdminApi.RouteId(ctx), AdminApi.Route(ctx, "item"));
			return null;
		});
	}

	private static JsonObject ToJson(PlayerSummary p, Services s)
	{
		// O nível e onde ele começa e acaba, já contados aqui: o painel não tem a conta (RankRule) própria.
		int rank = RankRule.RankFor(p.Xp);
		var json = Summary(p, s);
		json["rank"] = rank;
		json["level"] = RankRule.LevelOf(rank);
		json["levelXp"] = RankRule.XpFor(rank);
		json["nextXp"] = rank < RankRule.Count - 1 ? RankRule.XpFor(rank + 1) : null;
		return json;
	}

	private static JsonObject Summary(PlayerSummary p, Services s) => new()
	{
		["id"] = p.Id, ["name"] = p.Name, ["xp"] = p.Xp, ["presas"] = p.Presas, ["sangue"] = p.Sangue,
		["matches"] = p.Matches, ["createdAt"] = Api.Date(p.CreatedAt), ["lastLoginAt"] = Api.Date(p.LastLoginAt),
		["banned"] = p.Banned, ["bannedUntil"] = Api.Date(p.BannedUntil), ["online"] = s.Lobby.IsOnline(p.Id)
	};
}
