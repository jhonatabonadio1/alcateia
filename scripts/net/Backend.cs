using Godot;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// O servidor do Alcateia, visto do jogo: contas, perfil, ranking. As
/// salas e o chat ao vivo passam pelo <see cref="LobbyLink"/>.
///
/// Nada aqui trava o jogo: toda chamada é assíncrona e devolve uma
/// <see cref="Reply"/> — nunca lança. Sem servidor, <c>Ok</c> é falso e
/// <c>Error</c> diz o que houve; o jogo continua jogável offline.
///
/// O endereço e o login ficam em <c>user://online.cfg</c>; a variável de
/// ambiente <c>ALCATEIA_API</c> passa por cima do endereço (é como o
/// servidor de partida e os testes o apontam).
/// </summary>
public static class Backend
{
	public const string DefaultUrl = "http://127.0.0.1:8080";
	private const string FilePath = "user://online.cfg";

	/// <summary>A resposta de uma chamada: o código HTTP (0 se o
	/// servidor nem respondeu) e o corpo.</summary>
	public sealed record Reply(int Status, JsonObject Body)
	{
		public bool Ok => Status is >= 200 and < 300;
		public string Code => Body?["error"]?["code"]?.GetValue<string>() ?? (Status == 0 ? "sem_servidor" : "");
		public string Error => Body?["error"]?["message"]?.GetValue<string>()
			?? (Status == 0 ? "Sem resposta do servidor." : Ok ? "" : $"O servidor respondeu {Status}.");
	}

	private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
	private static bool _loaded;
	private static string _url = DefaultUrl;

	// O login guardado vale desde a primeira pergunta, seja ela qual for.
	static Backend() => Load();

	public static string Token { get; private set; }
	public static long AccountId { get; private set; }
	public static string AccountName { get; private set; } = "";
	public static bool LoggedIn => !string.IsNullOrEmpty(Token);

	/// <summary>Disparado quando se entra ou se sai da conta.</summary>
	public static event Action SessionChanged;

	public static string Url
	{
		get
		{
			Load();
			string env = OS.GetEnvironment("ALCATEIA_API");
			return string.IsNullOrEmpty(env) ? _url : env.TrimEnd('/');
		}
		set
		{
			Load();
			_url = string.IsNullOrWhiteSpace(value) ? DefaultUrl : value.Trim().TrimEnd('/');
			Save();
		}
	}

	/// <summary>O endereço do WebSocket do lobby, já com o token.</summary>
	public static string LobbyUrl =>
		Url.Replace("https://", "wss://").Replace("http://", "ws://") + "/v1/lobby?token=" + Uri.EscapeDataString(Token ?? "");

	public static Task<Reply> Health() => Call(HttpMethod.Get, "/health");

	public static Task<Reply> Register(string name, string password) => Enter("/v1/accounts", name, password);

	public static Task<Reply> Login(string name, string password) => Enter("/v1/sessions", name, password);

	/// <summary>Confere se o login guardado ainda vale; se não, esquece.</summary>
	public static async Task<Reply> Resume()
	{
		Load();
		if (!LoggedIn) return new Reply(401, null);

		Reply profile = await Profile();
		if (profile.Ok) Remember(Token, profile.Body);
		else if (profile.Status == 401) Forget();
		return profile;
	}

	public static async Task Logout()
	{
		if (LoggedIn) await Call(HttpMethod.Delete, "/v1/sessions");
		Forget();
	}

	public static Task<Reply> Profile() => Call(HttpMethod.Get, "/v1/profile");

	public static Task<Reply> Ranking(int limit = 10) => Call(HttpMethod.Get, $"/v1/ranking?limit={limit}");

	/// <summary>Uma chamada em nome de um servidor de partida.</summary>
	public static Task<Reply> AsGameServer(string path, JsonObject body, string fleetKey) =>
		Call(HttpMethod.Post, path, body, fleetKey);

	// ------------------------------------------------------------------

	private static async Task<Reply> Enter(string path, string name, string password)
	{
		Reply reply = await Call(HttpMethod.Post, path, new JsonObject { ["name"] = name, ["password"] = password });
		if (reply.Ok) Remember(reply.Body["token"].GetValue<string>(), reply.Body["profile"] as JsonObject);
		return reply;
	}

	private static async Task<Reply> Call(HttpMethod method, string path, JsonObject body = null, string fleetKey = null)
	{
		try
		{
			var request = new HttpRequestMessage(method, Url + path);
			if (fleetKey != null) request.Headers.Add("X-Fleet-Key", fleetKey);
			else if (LoggedIn) request.Headers.Add("Authorization", "Bearer " + Token);
			if (body != null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

			using HttpResponseMessage response = await Http.SendAsync(request);
			string text = await response.Content.ReadAsStringAsync();
			return new Reply((int)response.StatusCode, text.Length > 0 ? JsonNode.Parse(text) as JsonObject : null);
		}
		catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or UriFormatException)
		{
			return new Reply(0, null);
		}
	}

	private static void Remember(string token, JsonObject profile)
	{
		Token = token;
		AccountId = profile?["id"]?.GetValue<long>() ?? 0;
		AccountName = profile?["name"]?.GetValue<string>() ?? "";
		Save();
		SessionChanged?.Invoke();
	}

	private static void Forget()
	{
		Token = null;
		AccountId = 0;
		AccountName = "";
		Save();
		SessionChanged?.Invoke();
	}

	private static void Load()
	{
		if (_loaded) return;
		_loaded = true;

		var file = new ConfigFile();
		if (file.Load(FilePath) != Godot.Error.Ok) return;

		_url = file.GetValue("servidor", "url", DefaultUrl).AsString();
		Token = file.GetValue("conta", "token", "").AsString();
		AccountName = file.GetValue("conta", "nome", "").AsString();
		AccountId = file.GetValue("conta", "id", 0).AsInt64();
		if (Token == "") Token = null;
	}

	private static void Save()
	{
		var file = new ConfigFile();
		file.SetValue("servidor", "url", _url);
		file.SetValue("conta", "token", Token ?? "");
		file.SetValue("conta", "nome", AccountName);
		file.SetValue("conta", "id", AccountId);
		file.Save(FilePath);
	}
}
