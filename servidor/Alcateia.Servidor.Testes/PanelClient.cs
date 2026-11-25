using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Alcateia.Servidor.Testes;

/// <summary>Um administrador de teste: fala com a porta do painel e
/// guarda o cookie de sessão, como o navegador.</summary>
public sealed class PanelClient : IDisposable
{
	private readonly HttpClient _http;
	private readonly World _world;
	public readonly CookieContainer Cookies = new();

	/// <summary>Manda o cabeçalho que o painel de verdade manda.</summary>
	public bool SendHeader = true;

	/// <summary>A origem que o navegador manda (nula: nenhuma).</summary>
	public string Origin;

	public PanelClient(World world)
	{
		_world = world;
		_http = new HttpClient(new HttpClientHandler { CookieContainer = Cookies });
	}

	public async Task<(int Status, JsonObject Body)> Call(HttpMethod method, string path, JsonObject body = null)
	{
		var request = new HttpRequestMessage(method, _world.AdminUrl + "/admin/v1" + path);
		if (SendHeader) request.Headers.Add("X-Painel", "1");
		if (Origin != null) request.Headers.Add("Origin", Origin);
		if (body != null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
		HttpResponseMessage response = await _http.SendAsync(request);
		string text = await response.Content.ReadAsStringAsync();
		return ((int)response.StatusCode, text.Length > 0 ? JsonNode.Parse(text) as JsonObject : null);
	}

	public Task<(int Status, JsonObject Body)> Get(string path) => Call(HttpMethod.Get, path);
	public Task<(int Status, JsonObject Body)> Post(string path, JsonObject body = null) => Call(HttpMethod.Post, path, body ?? new JsonObject());
	public Task<(int Status, JsonObject Body)> Put(string path, JsonObject body) => Call(HttpMethod.Put, path, body);
	public Task<(int Status, JsonObject Body)> Delete(string path) => Call(HttpMethod.Delete, path);

	public async Task<JsonObject> Login(string name = World.AdminName, string password = World.AdminPassword, string code = null)
	{
		var (status, body) = await Post("/login", new JsonObject { ["name"] = name, ["password"] = password, ["code"] = code });
		Check.Equal(200, status, $"{name} entra no painel");
		return body;
	}

	/// <summary>Um administrador novo, com esse papel, já dentro do painel.</summary>
	public async Task<PanelClient> NewAdmin(string name, string role)
	{
		var (status, _) = await Post("/admins", new JsonObject { ["name"] = name, ["password"] = "senha-do-" + name + "-123", ["role"] = role });
		Check.Equal(200, status, $"criar o administrador {name}");
		var other = new PanelClient(_world);
		await other.Login(name, "senha-do-" + name + "-123");
		return other;
	}

	public void Dispose() => _http.Dispose();
}
