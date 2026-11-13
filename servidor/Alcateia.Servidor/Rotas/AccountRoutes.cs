using System.Linq;
using System.Text.Json.Nodes;
using Alcateia.Dominio.Contas;
using Alcateia.Servidor.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Alcateia.Servidor.Rotas;

/// <summary>Conta, login, perfil e ranking.</summary>
public static class AccountRoutes
{
	public static void Map(IEndpointRouteBuilder app, Services s)
	{
		app.MapPost("/v1/accounts", async ctx =>
		{
			s.Logins.Check(ctx);
			JsonObject body = await Api.Body(ctx);
			var (account, token) = await s.Accounts.Register(Api.Text(body, "name"), Api.Text(body, "password"));
			ctx.Response.StatusCode = 201;
			await Api.Send(ctx, new JsonObject { ["token"] = token, ["profile"] = ToJson(await s.Accounts.Profile(account.Id)) });
		});

		app.MapPost("/v1/sessions", async ctx =>
		{
			s.Logins.Check(ctx);
			JsonObject body = await Api.Body(ctx);
			var (account, token) = await s.Accounts.Login(Api.Text(body, "name"), Api.Text(body, "password"));
			await Api.Send(ctx, new JsonObject { ["token"] = token, ["profile"] = ToJson(await s.Accounts.Profile(account.Id)) });
		});

		app.MapDelete("/v1/sessions", async ctx =>
		{
			await s.Accounts.Require(Api.Token(ctx));
			await s.Accounts.Logout(Api.Token(ctx));
			ctx.Response.StatusCode = 204;
		});

		app.MapGet("/v1/profile", async ctx =>
		{
			Account account = await s.Accounts.Require(Api.Token(ctx));
			await Api.Send(ctx, ToJson(await s.Accounts.Profile(account.Id)));
		});

		app.MapPut("/v1/profile/data", async ctx =>
		{
			Account account = await s.Accounts.Require(Api.Token(ctx));
			await s.Accounts.SaveData(account.Id, (await Api.Body(ctx))["data"]?.ToJsonString());
			ctx.Response.StatusCode = 204;
		});

		app.MapGet("/v1/ranking", async ctx =>
		{
			int.TryParse(ctx.Request.Query["limit"], out int limit);
			var list = new JsonArray();
			foreach (RankingEntry r in await s.Accounts.Ranking(limit == 0 ? 50 : limit))
				list.Add(new JsonObject
				{
					["position"] = r.Position, ["name"] = r.Name, ["xp"] = r.Xp, ["kills"] = r.Kills,
					["deaths"] = r.Deaths, ["wins"] = r.Wins, ["matches"] = r.Matches
				});
			await Api.Send(ctx, new JsonObject { ["ranking"] = list });
		});
	}

	/// <summary>O perfil como vai para o cliente.</summary>
	public static JsonObject ToJson(Profile p)
	{
		var stats = new JsonObject();
		foreach (string name in Profile.StatNames) stats[name] = p.Stats[name];

		return new JsonObject
		{
			["id"] = p.Id,
			["name"] = p.Name,
			["createdAt"] = p.CreatedAt,
			["xp"] = p.Xp,
			["presas"] = p.Presas,
			["stats"] = stats,
			["data"] = JsonNode.Parse(p.DataJson)
		};
	}
}
