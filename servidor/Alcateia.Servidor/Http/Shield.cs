using System;
using System.Net;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Seguranca;
using Alcateia.Dominio.Seguranca;
using Alcateia.Infra.Seguranca;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Alcateia.Servidor.Http;

/// <summary>
/// O escudo: a primeira coisa por que todo pedido passa, e a última
/// antes de a resposta sair.
///
/// 1. descobre o endereço de verdade (<see cref="ClientIp"/>);
/// 2. endereço na blacklist: 403 na hora, sem tocar em mais nada;
/// 3. o painel só existe na porta dele, e só para quem está na lista;
/// 4. injeção no endereço: 400; rota que só scanner pede: 404 — as duas
///    registradas, e poucas viram bloqueio;
/// 5. freio de pedidos por minuto, por endereço: 429;
/// 6. cabeçalhos de segurança em toda resposta;
/// 7. na volta, conta as recusas (quem coleciona 401/403/404 está
///    adivinhando) e registra os erros do servidor.
///
/// Tudo em memória: são umas buscas em dicionário por pedido.
/// </summary>
public static class Shield
{
	private const string AdminKey = "alcateia.painel";

	/// <summary>O pedido chegou pela porta do painel.</summary>
	public static bool OnAdminPort(HttpContext ctx) => ctx.Items.ContainsKey(AdminKey);

	public static void UseShield(this WebApplication app, Config config, SecurityService security) => app.Use(async (ctx, next) =>
	{
		IPAddress address = ClientIp.Resolve(ctx, config.TrustedProxies);
		string ip = address?.ToString() ?? "?";
		string path = ctx.Request.Path.Value ?? "/";
		bool admin = config.AdminPort != 0 && ctx.Connection.LocalPort == config.AdminPort;
		if (admin) ctx.Items[AdminKey] = true;

		IHeaderDictionary headers = ctx.Response.Headers;
		headers["X-Content-Type-Options"] = "nosniff";
		headers["X-Frame-Options"] = "DENY";
		headers["Referrer-Policy"] = "no-referrer";
		headers["Cross-Origin-Resource-Policy"] = "same-origin";
		headers["Cache-Control"] = "no-store";
		if (ClientIp.Https(ctx, config.TrustedProxies)) headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

		if (security.IsBlocked(address))
		{
			security.Refused(ip, path);
			await Api.Fail(ctx, 403, "bloqueado", "Acesso bloqueado.");
			return;
		}

		if (admin && config.AdminAllow.Count > 0 && !Allowed(address, config))
		{
			security.Record(EventKind.AdminDenied, Severity.High, ip, path: path, detail: "endereço fora da lista do painel");
			await Api.Fail(ctx, 404, "nao_encontrado", "Não encontrado.");
			return;
		}

		string query = ctx.Request.QueryString.HasValue ? Unescape(ctx.Request.QueryString.Value) : "";
		if (ThreatRules.LooksLikeInjection(path + query))
		{
			security.Suspicious(address, ip, EventKind.Injection, path + Mask(query));
			await Api.Fail(ctx, 400, "pedido_invalido", "Pedido inválido.");
			return;
		}

		// O painel na porta do jogo não existe: quem o procura ali está sondando.
		if (ThreatRules.LooksLikeScanner(path) || (!admin && path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)))
		{
			security.Suspicious(address, ip, EventKind.Scanner, path);
			await Api.Fail(ctx, 404, "nao_encontrado", "Não encontrado.");
			return;
		}

		// Servidor de partida (com a chave da frota) não entra no freio: uma
		// máquina com vinte partidas bate vinte vezes mais.
		bool fleet = ctx.Request.Headers.ContainsKey("X-Fleet-Key") && Passwords.SameSecret(ctx.Request.Headers["X-Fleet-Key"], config.FleetKey);
		if (!fleet && !security.AllowRequest(address, ip, path))
		{
			headers["Retry-After"] = "60";
			await Api.Fail(ctx, 429, "devagar", "Pedidos demais. Espere um minuto.");
			return;
		}

		await next();

		int status = ctx.Response.StatusCode;
		if (status is 401 or 403 or 404) security.Denied(address, ip, path, status);
		else if (status >= 500) security.Record(EventKind.ServerError, Severity.Warning, ip, path: path, detail: status.ToString());
	});

	private static bool Allowed(IPAddress address, Config config)
	{
		if (address == null) return false;
		foreach (IpRange range in config.AdminAllow)
			if (range.Contains(address)) return true;
		return false;
	}

	private static string Unescape(string query)
	{
		try { return Uri.UnescapeDataString(query.Replace('+', ' ')); }
		catch (UriFormatException) { return query; }
	}

	/// <summary>O token do saguão vai na consulta: não entra no registro.</summary>
	private static string Mask(string query)
	{
		int at = query.IndexOf("token=", StringComparison.OrdinalIgnoreCase);
		return at < 0 ? query : query[..(at + 6)] + "***";
	}
}
