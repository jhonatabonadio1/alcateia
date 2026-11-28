using System.Linq;
using System.Text.Json.Nodes;
using Alcateia.Aplicacao.Jogo;
using Alcateia.Dominio;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Seguranca;
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
			RequireOpen(s);
			JsonObject body = await Api.Body(ctx);
			var (account, token) = await s.Accounts.Register(Api.Text(body, "name"), Api.Text(body, "password"));
			string ip = ClientIp.Of(ctx);
			s.Security.Record(EventKind.Register, Severity.Info, ip, account.Id, account.Name, "/v1/accounts");
			await s.PlayerStore.NoteIp(account.Id, ip);
			ctx.Response.StatusCode = 201;
			await Api.Send(ctx, new JsonObject { ["token"] = token, ["profile"] = ToJson(await s.Accounts.Profile(account.Id)) });
		});

		app.MapPost("/v1/sessions", async ctx =>
		{
			s.Logins.Check(ctx);
			RequireOpen(s);
			JsonObject body = await Api.Body(ctx);
			string name = Api.Text(body, "name"), ip = ClientIp.Of(ctx);
			Account account;
			string token;
			try
			{
				(account, token) = await s.Accounts.Login(name, Api.Text(body, "password"), Api.Text(body, "code"));
			}
			// Código da segunda etapa errado conta como login errado: quem
			// chuta códigos esbarra no mesmo freio de quem chuta senhas.
			catch (Failure f) when (f.Code is "login_invalido" or "codigo_invalido")
			{
				s.Security.LoginFailed(ClientIp.Address(ctx), ip, name, "/v1/sessions");
				throw;
			}
			catch (Failure f) when (f.Code == "conta_banida")
			{
				s.Security.Record(EventKind.LoginBanned, Severity.Warning, ip, actor: name, path: "/v1/sessions");
				throw;
			}
			s.Security.LoginOk(ip, account.Id, account.Name, "/v1/sessions");
			await s.PlayerStore.NoteIp(account.Id, ip);
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

		// ---- verificação em duas etapas (opcional, por conta)
		app.MapPost("/v1/account/2fa", async ctx =>
		{
			Account account = await s.Accounts.Require(Api.Token(ctx));
			(string secret, string uri) = await s.Accounts.StartTwoFactor(account);
			await Api.Send(ctx, new JsonObject { ["secret"] = secret, ["uri"] = uri });
		});

		app.MapPost("/v1/account/2fa/confirm", async ctx =>
		{
			s.Logins.Check(ctx);
			Account account = await s.Accounts.Require(Api.Token(ctx));
			await s.Accounts.ConfirmTwoFactor(account.Id, Api.Text(await Api.Body(ctx), "code"));
			s.Security.Record(EventKind.LoginOk, Severity.Info, ClientIp.Of(ctx), account.Id, account.Name, "/v1/account/2fa/confirm", "ligou a verificação em duas etapas");
			ctx.Response.StatusCode = 204;
		});

		app.MapPost("/v1/account/2fa/disable", async ctx =>
		{
			s.Logins.Check(ctx);
			Account account = await s.Accounts.Require(Api.Token(ctx));
			JsonObject body = await Api.Body(ctx);
			try
			{
				await s.Accounts.DisableTwoFactor(account.Id, Api.Text(body, "password"), Api.Text(body, "code"));
			}
			catch (Failure f) when (f.Code is "senha_errada" or "codigo_invalido")
			{
				s.Security.LoginFailed(ClientIp.Address(ctx), ClientIp.Of(ctx), account.Name, "/v1/account/2fa/disable");
				throw;
			}
			s.Security.Record(EventKind.LoginOk, Severity.Warning, ClientIp.Of(ctx), account.Id, account.Name, "/v1/account/2fa/disable", "desligou a verificação em duas etapas");
			ctx.Response.StatusCode = 204;
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

	/// <summary>Em manutenção ninguém entra nem cria conta.</summary>
	private static void RequireOpen(Services s)
	{
		GameData data = s.GameData.Current;
		if (data.Maintenance)
			throw new Failure(FailureKind.Unavailable, "manutencao",
				string.IsNullOrWhiteSpace(data.MaintenanceMessage) ? "O servidor está em manutenção. Volte daqui a pouco." : data.MaintenanceMessage);
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
			// A tag já escrita (quatro algarismos): quem mostra não precisa saber completar com zeros.
			["tag"] = AccountRules.TagText(p.Tag),
			["twoFactor"] = p.TwoFactor,
			["createdAt"] = p.CreatedAt,
			["xp"] = p.Xp,
			["presas"] = p.Presas,
			["sangue"] = p.Sangue,
			["rank"] = p.Rank,
			["stats"] = stats,
			["data"] = JsonNode.Parse(p.DataJson)
		};
	}
}
