using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Painel;
using Alcateia.Dominio;
using Alcateia.Dominio.Painel;
using Alcateia.Dominio.Seguranca;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Alcateia.Servidor.Http;

/// <summary>
/// O que toda rota do painel usa. As rotas do painel só existem na porta
/// do painel; a sessão vem num cookie que o JavaScript não lê
/// (<c>HttpOnly</c>) e que o navegador não manda de outro site
/// (<c>SameSite=Strict</c>); e todo pedido que muda algo precisa do
/// cabeçalho <c>X-Painel</c>, que um formulário de outro site não
/// consegue mandar.
/// </summary>
public static class AdminApi
{
	public const string Cookie = "wl_painel", Prefix = "/admin/v1";

	/// <summary>Quem está pedindo — ou a recusa.</summary>
	public static async Task<AdminContext> Require(HttpContext ctx, Services s, bool evenWithoutTotp = false)
	{
		RequirePanel(ctx);
		AdminUser admin = await s.AdminAuth.FromToken(ctx.Request.Cookies[Cookie])
			?? throw new Failure(FailureKind.Unauthorized, "sem_login", "Entre no painel primeiro.");
		if (s.AdminAuth.RequireTotp && !admin.TotpEnabled && !evenWithoutTotp)
			throw new Failure(FailureKind.Forbidden, "ative_2fa", "Ligue a verificação em duas etapas para usar o painel.");
		return new AdminContext(admin, ClientIp.Of(ctx));
	}

	/// <summary>O pedido veio pela porta do painel e, se muda algo, de dentro do painel.</summary>
	public static void RequirePanel(HttpContext ctx)
	{
		if (!Shield.OnAdminPort(ctx)) throw new Failure(FailureKind.NotFound, "nao_encontrado", "Não encontrado.");
		if (HttpMethods.IsGet(ctx.Request.Method)) return;

		string origin = ctx.Request.Headers.Origin;
		bool sameOrigin = string.IsNullOrEmpty(origin) || (Uri.TryCreate(origin, UriKind.Absolute, out Uri from) && from.Authority == ctx.Request.Host.Value);
		if (ctx.Request.Headers["X-Painel"] != "1" || !sameOrigin)
			throw new Failure(FailureKind.Forbidden, "pedido_recusado", "Pedido recusado.");
	}

	public static void SetCookie(HttpContext ctx, Config config, string token) => ctx.Response.Cookies.Append(Cookie, token, new CookieOptions
	{
		HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = ClientIp.Https(ctx, config.TrustedProxies), Path = "/",
		MaxAge = AdminRules.SessionLife, IsEssential = true
	});

	public static void ClearCookie(HttpContext ctx) => ctx.Response.Cookies.Delete(Cookie, new CookieOptions { Path = "/" });

	public static JsonObject ToJson(AdminUser a) => new()
	{
		["id"] = a.Id, ["name"] = a.Name, ["role"] = a.Role, ["totp"] = a.TotpEnabled, ["disabled"] = a.Disabled,
		["createdAt"] = Api.Date(a.CreatedAt), ["lastLoginAt"] = Api.Date(a.LastLoginAt), ["lastIp"] = a.LastIp
	};

	// ------------------------------------------------------------ as rotas, sem repetição

	public delegate Task<JsonNode> Read(HttpContext ctx, AdminContext who);
	public delegate Task<JsonNode> Write(HttpContext ctx, AdminContext who, JsonObject body);

	public static void Get(this IEndpointRouteBuilder app, Services s, string path, Read handler) =>
		app.MapGet(Prefix + path, async ctx => await Api.Send(ctx, await handler(ctx, await Require(ctx, s)) ?? Ok()));

	public static void Post(this IEndpointRouteBuilder app, Services s, string path, Write handler) =>
		app.MapPost(Prefix + path, async ctx => await Run(ctx, s, handler));

	public static void Put(this IEndpointRouteBuilder app, Services s, string path, Write handler) =>
		app.MapPut(Prefix + path, async ctx => await Run(ctx, s, handler));

	public static void Delete(this IEndpointRouteBuilder app, Services s, string path, Read handler) =>
		app.MapDelete(Prefix + path, async ctx => await Api.Send(ctx, await handler(ctx, await Require(ctx, s)) ?? Ok()));

	private static async Task Run(HttpContext ctx, Services s, Write handler)
	{
		AdminContext who = await Require(ctx, s);
		JsonObject body = ctx.Request.ContentLength is null or 0 ? new JsonObject() : await Api.Body(ctx);
		await Api.Send(ctx, await handler(ctx, who, body) ?? Ok());
	}

	public static JsonObject Ok() => new() { ["ok"] = true };

	public static string Route(HttpContext ctx, string name) => (string)ctx.Request.RouteValues[name];

	public static long RouteId(HttpContext ctx, string name = "id") =>
		long.TryParse(Route(ctx, name), out long id) ? id : throw new Failure(FailureKind.Invalid, "id_invalido", "Identificador inválido.");

	public static bool Flag(JsonObject body, string field, bool fallback = false) => body[field]?.GetValue<bool>() ?? fallback;

	public static double Number(JsonObject body, string field, double fallback = 0) => body[field]?.GetValue<double>() ?? fallback;

	public static DateTime When(JsonObject body, string field) =>
		DateTime.TryParse(Api.Text(body, field), null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime at)
			? at : throw new Failure(FailureKind.Invalid, "data_invalida", $"Data inválida em '{field}'.");

	public static DateTime? WhenOrNull(JsonObject body, string field) => string.IsNullOrEmpty(body[field]?.GetValue<string>()) ? null : When(body, field);
}
