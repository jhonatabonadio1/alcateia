using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;

namespace Alcateia.Infra.Fly;

/// <summary>
/// Liga servidores de partida sob demanda no Fly.io. A frota é um app de
/// máquinas criadas paradas (tools/fly/frota.py), cada uma com a sua porta
/// UDP; aqui só se liga uma delas pela API de máquinas. Quem desliga é a
/// própria máquina: ociosa, ela pede para sair da frota e encerra o processo
/// (<c>ALCATEIA_IDLE_EXIT</c>), e a máquina para — parada, não cobra CPU.
/// Veja docs/FLY.md.
/// </summary>
public sealed class FlyMachines : IFleetScaler
{
	private readonly HttpClient _http;
	private readonly string _app;

	public FlyMachines(string api, string app, string token)
	{
		_app = app;
		_http = new HttpClient { BaseAddress = new Uri(api.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(20) };
		// Os tokens novos ("fly tokens create") já vêm com o esquema: "FlyV1 fm2_...".
		_http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization",
			token.StartsWith("FlyV1 ", StringComparison.Ordinal) ? token : "Bearer " + token);
	}

	public async Task<string> Wake()
	{
		var machines = JsonNode.Parse(await _http.GetStringAsync($"v1/apps/{_app}/machines")) as JsonArray;

		// Suspensa volta mais rápido que parada.
		JsonObject parked = machines?.OfType<JsonObject>()
			.Where(m => m["state"]?.GetValue<string>() is "suspended" or "stopped")
			.OrderBy(m => m["state"]?.GetValue<string>() == "suspended" ? 0 : 1)
			.FirstOrDefault();
		if (parked == null)
		{
			Console.WriteLine($"Frota: falta lugar e não há máquina parada em {_app} (todas ligadas). Crie mais com tools/fly/frota.py criar.");
			return null;
		}

		string id = parked["id"]!.GetValue<string>();
		using HttpResponseMessage reply = await _http.PostAsync($"v1/apps/{_app}/machines/{id}/start", null);
		if (!reply.IsSuccessStatusCode)
			throw new InvalidOperationException($"a API do Fly recusou ligar {id}: {(int)reply.StatusCode} {await reply.Content.ReadAsStringAsync()}");

		Console.WriteLine($"Frota: liguei a máquina {id} de {_app}.");
		return id;
	}
}
