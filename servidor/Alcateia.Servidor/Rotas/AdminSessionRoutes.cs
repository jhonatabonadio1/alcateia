using System.Linq;
using System.Text.Json.Nodes;
using Alcateia.Aplicacao.Painel;
using Alcateia.Dominio.Painel;
using Alcateia.Servidor.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Alcateia.Servidor.Rotas;

/// <summary>Painel: entrar, sair, a própria conta (senha, segunda etapa)
/// e os outros administradores.</summary>
public static class AdminSessionRoutes
{
	public static void Map(IEndpointRouteBuilder app, Services s)
	{
		app.MapPost(AdminApi.Prefix + "/login", async ctx =>
		{
			AdminApi.RequirePanel(ctx);
			s.Logins.Check(ctx);
			JsonObject body = await Api.Body(ctx);
			var (admin, token) = await s.AdminAuth.Login(Api.Text(body, "name"), Api.Text(body, "password"), Api.Text(body, "code"),
				ClientIp.Address(ctx), ClientIp.Of(ctx), ctx.Request.Headers.UserAgent);
			AdminApi.SetCookie(ctx, s.Config, token);
			await Api.Send(ctx, Me(admin, s));
		});

		app.MapPost(AdminApi.Prefix + "/logout", async ctx =>
		{
			AdminApi.RequirePanel(ctx);
			await s.AdminAuth.Logout(ctx.Request.Cookies[AdminApi.Cookie]);
			AdminApi.ClearCookie(ctx);
			await Api.Send(ctx, AdminApi.Ok());
		});

		app.MapGet(AdminApi.Prefix + "/me", async ctx =>
			await Api.Send(ctx, Me((await AdminApi.Require(ctx, s, evenWithoutTotp: true)).Admin, s)));

		app.MapPost(AdminApi.Prefix + "/me/password", async ctx =>
		{
			AdminContext who = await AdminApi.Require(ctx, s, evenWithoutTotp: true);
			JsonObject body = await Api.Body(ctx);
			await s.AdminAuth.ChangePassword(who, Api.Text(body, "current"), Api.Text(body, "next"));
			AdminApi.ClearCookie(ctx);
			await Api.Send(ctx, AdminApi.Ok());
		});

		app.MapPost(AdminApi.Prefix + "/me/totp/start", async ctx =>
		{
			AdminContext who = await AdminApi.Require(ctx, s, evenWithoutTotp: true);
			var (secret, uri) = await s.AdminAuth.StartTotp(who, "WolfLeague Painel");
			await Api.Send(ctx, new JsonObject { ["secret"] = secret, ["uri"] = uri });
		});

		app.MapPost(AdminApi.Prefix + "/me/totp/confirm", async ctx =>
		{
			AdminContext who = await AdminApi.Require(ctx, s, evenWithoutTotp: true);
			await s.AdminAuth.ConfirmTotp(who, Api.Text(await Api.Body(ctx), "code"));
			await Api.Send(ctx, AdminApi.Ok());
		});

		app.Post(s, "/me/totp/disable", async (ctx, who, body) =>
		{
			await s.AdminAuth.DisableTotp(who, Api.Text(body, "password"));
			return null;
		});

		app.Get(s, "/admins", async (ctx, who) =>
			new JsonObject { ["admins"] = new JsonArray((await s.AdminAuth.List(who)).Select(a => (JsonNode)AdminApi.ToJson(a)).ToArray()) });

		app.Post(s, "/admins", async (ctx, who, body) =>
			new JsonObject { ["id"] = await s.AdminAuth.Create(who, Api.Text(body, "name"), Api.Text(body, "password"), Api.Text(body, "role")) });

		app.Put(s, "/admins/{id}", async (ctx, who, body) =>
		{
			await s.AdminAuth.Update(who, AdminApi.RouteId(ctx), body["role"]?.GetValue<string>(), body["disabled"]?.GetValue<bool>(),
				body["password"]?.GetValue<string>());
			return null;
		});
	}

	private static JsonObject Me(AdminUser admin, Services s)
	{
		JsonObject me = AdminApi.ToJson(admin);
		me["permissions"] = new JsonArray(Roles.Of(admin.Role).Select(p => (JsonNode)p.ToString().ToLowerInvariant()).ToArray());
		me["mustEnableTotp"] = s.AdminAuth.RequireTotp && !admin.TotpEnabled;
		me["version"] = App.Version;
		return me;
	}
}
