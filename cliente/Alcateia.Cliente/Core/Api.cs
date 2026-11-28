using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Alcateia.Cliente.Core;

/// <summary>A resposta de uma chamada ao servidor: o código HTTP (0 se ele
/// nem respondeu) e o corpo. Nunca lança.</summary>
public sealed record Reply(int Status, JsonObject Body)
{
	public bool Ok => Status is >= 200 and < 300;
	public string Code => Body?["error"]?["code"]?.GetValue<string>() ?? (Status == 0 ? "sem_servidor" : "");
	public string Error => Body?["error"]?["message"]?.GetValue<string>()
		?? (Status == 0 ? "Sem resposta do servidor. Confira a sua internet." : Ok ? "" : $"O servidor respondeu {Status}.");
}

/// <summary>A conta que está entrada no cliente.</summary>
public sealed record Account(long Id, string Name, string Tag, long Xp, int Rank, string Token)
{
	public string Handle => Tag.Length == 0 ? Name : $"{Name}#{Tag}";

	/// <summary>A conta pede o código do aplicativo autenticador para entrar.</summary>
	public bool TwoFactor { get; init; }
}

/// <summary>
/// O servidor do WolfLeague visto do cliente: conta e perfil (as mesmas
/// rotas que o jogo usa, servidor/LEIA-ME.md) e de onde baixar o jogo. Os
/// amigos ao vivo passam pelo <see cref="FriendsLink"/>.
/// </summary>
public sealed class Api
{
	private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

	public string Url { get; set; } = AppInfo.ServerUrl;

	/// <summary><paramref name="code"/>: o código do aplicativo autenticador,
	/// para a conta com verificação em duas etapas (o servidor responde
	/// <c>codigo_necessario</c> quando falta).</summary>
	public Task<Reply> Login(string name, string password, string code = null)
	{
		var body = new JsonObject { ["name"] = name, ["password"] = password };
		if (!string.IsNullOrWhiteSpace(code)) body["code"] = code.Trim();
		return Call(HttpMethod.Post, "/v1/sessions", null, body);
	}

	// ---- verificação em duas etapas
	/// <summary>Começa a ligar: devolve <c>secret</c> (a chave) e <c>uri</c> (o que vira QR code).</summary>
	public Task<Reply> TwoFactorStart(string token) => Call(HttpMethod.Post, "/v1/account/2fa", token, new JsonObject());

	public Task<Reply> TwoFactorConfirm(string token, string code) =>
		Call(HttpMethod.Post, "/v1/account/2fa/confirm", token, new JsonObject { ["code"] = (code ?? "").Trim() });

	public Task<Reply> TwoFactorDisable(string token, string password, string code) =>
		Call(HttpMethod.Post, "/v1/account/2fa/disable", token, new JsonObject { ["password"] = password, ["code"] = (code ?? "").Trim() });

	public Task<Reply> Register(string name, string password) =>
		Call(HttpMethod.Post, "/v1/accounts", null, new JsonObject { ["name"] = name, ["password"] = password });

	public Task<Reply> Profile(string token) => Call(HttpMethod.Get, "/v1/profile", token);

	public Task<Reply> Logout(string token) => Call(HttpMethod.Delete, "/v1/sessions", token);

	/// <summary>De onde baixar o jogo (<c>downloads</c>).</summary>
	public Task<Reply> ClientInfo() => Call(HttpMethod.Get, "/v1/client", null);

	/// <summary>A conta, do perfil que o servidor mandou.</summary>
	public static Account AccountOf(JsonObject profile, string token) => new(
		profile?["id"]?.GetValue<long>() ?? 0, profile?["name"]?.GetValue<string>() ?? "", profile?["tag"]?.GetValue<string>() ?? "",
		profile?["xp"]?.GetValue<long>() ?? 0, profile?["rank"]?.GetValue<int>() ?? 0, token)
	{
		TwoFactor = profile?["twoFactor"]?.GetValue<bool>() ?? false
	};

	public string LobbyUrl(string token) =>
		Url.Replace("https://", "wss://").Replace("http://", "ws://") + "/v1/lobby?client=app&token=" + Uri.EscapeDataString(token ?? "");

	private async Task<Reply> Call(HttpMethod method, string path, string token, JsonObject body = null)
	{
		try
		{
			var request = new HttpRequestMessage(method, Url + path);
			if (token != null) request.Headers.Add("Authorization", "Bearer " + token);
			if (body != null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
			using HttpResponseMessage response = await Http.SendAsync(request);
			string text = await response.Content.ReadAsStringAsync();
			JsonObject json = null;
			try { json = text.Length > 0 ? JsonNode.Parse(text) as JsonObject : null; }
			catch (JsonException) { }
			return new Reply((int)response.StatusCode, json);
		}
		catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException)
		{
			return new Reply(0, null);
		}
	}
}
