using System;
using System.Collections.Concurrent;
using Alcateia.Dominio;
using Microsoft.AspNetCore.Http;

namespace Alcateia.Servidor.Http;

/// <summary>
/// Freio de tentativas por endereço: tantas por minuto, e depois 429.
/// Em memória — some ao reiniciar, e está certo: é freio, não registro.
/// </summary>
public sealed class RateLimit
{
	private readonly int _perMinute;
	private readonly ConcurrentDictionary<string, (int Count, DateTime Window)> _hits = new();

	public RateLimit(int perMinute) => _perMinute = perMinute;

	public bool Allow(string key)
	{
		DateTime now = DateTime.UtcNow;
		var entry = _hits.AddOrUpdate(key, _ => (1, now),
			(_, old) => now - old.Window > TimeSpan.FromMinutes(1) ? (1, now) : (old.Count + 1, old.Window));

		if (_hits.Count > 10_000)
			foreach (var (k, v) in _hits)
				if (now - v.Window > TimeSpan.FromMinutes(1)) _hits.TryRemove(k, out _);

		return entry.Count <= _perMinute;
	}

	/// <summary>Recusa o pedido se este endereço já tentou demais.</summary>
	public void Check(HttpContext ctx)
	{
		if (!Allow(ctx.Connection.RemoteIpAddress?.ToString() ?? "?"))
			throw new Failure(FailureKind.TooMany, "devagar", "Muitas tentativas. Espere um minuto.");
	}
}
