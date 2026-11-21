using System.Collections.Generic;
using System.Net;
using Alcateia.Dominio.Seguranca;
using Microsoft.AspNetCore.Http;

namespace Alcateia.Servidor.Http;

/// <summary>
/// O endereço de verdade de quem pede. Atrás de um proxy (Caddy,
/// Cloudflare, Fly.io) a conexão chega com o endereço do proxy, e o do
/// cliente vem num cabeçalho — que só vale se quem o escreveu foi um proxy
/// de confiança (<c>ALCATEIA_TRUSTED_PROXIES</c>). De um desconhecido, o
/// cabeçalho é ignorado: senão bastava mandar um <c>X-Forwarded-For</c>
/// falso para fugir da blacklist (lição do CT).
/// </summary>
public static class ClientIp
{
	private const string Key = "alcateia.ip";

	public static IPAddress Resolve(HttpContext ctx, IReadOnlyList<IpRange> trusted)
	{
		IPAddress remote = IpRange.Normalize(ctx.Connection.RemoteIpAddress);
		IPAddress client = remote;

		if (remote != null && Trusted(remote, trusted))
		{
			// O Cloudflare diz o cliente num cabeçalho só dele.
			string cloudflare = ctx.Request.Headers["CF-Connecting-IP"];
			// O Fly.io também (Fly-Client-IP: o endereço de quem abriu a conexão
			// com o proxy dele). No X-Forwarded-For do Fly o último endereço é o
			// IP do próprio app, não o do cliente — por isso este vem antes.
			string fly = ctx.Request.Headers["Fly-Client-IP"];
			if (!string.IsNullOrEmpty(cloudflare) && IPAddress.TryParse(cloudflare.Trim(), out IPAddress fromCloudflare))
				client = IpRange.Normalize(fromCloudflare);
			else if (!string.IsNullOrEmpty(fly) && IPAddress.TryParse(fly.Trim(), out IPAddress fromFly))
				client = IpRange.Normalize(fromFly);
			else
			{
				// Da direita para a esquerda: o primeiro que não é proxy de
				// confiança é o cliente. O que vem antes dele, ele pode ter inventado.
				string forwarded = ctx.Request.Headers["X-Forwarded-For"];
				if (!string.IsNullOrEmpty(forwarded))
				{
					string[] hops = forwarded.Split(',');
					for (int i = hops.Length - 1; i >= 0; i--)
					{
						if (!IPAddress.TryParse(hops[i].Trim(), out IPAddress hop)) break;
						client = IpRange.Normalize(hop);
						if (!Trusted(client, trusted)) break;
					}
				}
			}
		}

		ctx.Items[Key] = client;
		return client;
	}

	/// <summary>O pedido chegou por HTTPS (direto, ou no proxy de confiança).</summary>
	public static bool Https(HttpContext ctx, IReadOnlyList<IpRange> trusted)
	{
		if (ctx.Request.IsHttps) return true;
		IPAddress remote = IpRange.Normalize(ctx.Connection.RemoteIpAddress);
		if (remote == null || !Trusted(remote, trusted)) return false;
		// O Fly repassa o X-Forwarded-Proto que o cliente mandar; o dele é o Fly-Forwarded-Proto.
		string fly = ctx.Request.Headers["Fly-Forwarded-Proto"];
		return !string.IsNullOrEmpty(fly) ? fly == "https" : ctx.Request.Headers["X-Forwarded-Proto"] == "https";
	}

	public static IPAddress Address(HttpContext ctx) =>
		ctx.Items.TryGetValue(Key, out object ip) ? ip as IPAddress : IpRange.Normalize(ctx.Connection.RemoteIpAddress);

	public static string Of(HttpContext ctx) => Address(ctx)?.ToString() ?? "?";

	private static bool Trusted(IPAddress address, IReadOnlyList<IpRange> trusted)
	{
		foreach (IpRange range in trusted)
			if (range.Contains(address)) return true;
		return false;
	}
}
