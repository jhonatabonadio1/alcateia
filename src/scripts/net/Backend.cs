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
/// O endereço e os acessos salvos ficam em <c>user://online.cfg</c>; a
/// variável de ambiente <c>ALCATEIA_API</c> passa por cima do endereço (é
/// como o servidor de partida e os testes o apontam).
///
/// **Acesso salvo não entra sozinho** (Jhon, 20/11): o jogo abre na tela
/// de login (<see cref="LoginScreen"/>), com as contas salvas para clicar.
/// A sessão (<see cref="Token"/>) só existe depois do clique, ou do login,
/// e vale até fechar o jogo ou sair da conta.
/// </summary>
public static class Backend
{
	public const string LocalUrl = "http://127.0.0.1:8080";

	/// <summary>
	/// O servidor quando ninguém escolheu outro. No editor (e nos testes,
	/// que rodam no binário do editor), o da própria máquina. No jogo
	/// exportado — o que vai para os testadores —, o de produção, que fica
	/// no project.godot (<c>wolfleague/rede/servidor</c>): trocou o
	/// endereço do servidor, troca lá e exporta de novo.
	/// </summary>
	public static string DefaultUrl => OS.HasFeature("editor")
		? LocalUrl
		: ProjectSettings.GetSetting("wolfleague/rede/servidor", LocalUrl).AsString().TrimEnd('/');
	private static string FilePath => UserFiles.OfAccount(UserFiles.Online);

	/// <summary>A resposta de uma chamada: o código HTTP (0 se o
	/// servidor nem respondeu) e o corpo.</summary>
	public sealed record Reply(int Status, JsonObject Body)
	{
		public bool Ok => Status is >= 200 and < 300;
		public string Code => Body?["error"]?["code"]?.GetValue<string>() ?? (Status == 0 ? "sem_servidor" : "");
		public string Error => Body?["error"]?["message"]?.GetValue<string>()
			?? (Status == 0 ? "Sem resposta do servidor." : Ok ? "" : Loc.F("O servidor respondeu {0}.", Status));
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

	/// <summary>Um acesso guardado neste computador: a conta, o servidor e
	/// o login dela (o token, nunca a senha).</summary>
	public sealed record SavedAccess(string Url, long Id, string Name, string Token, string SavedAt);

	private static readonly System.Collections.Generic.List<SavedAccess> _saved = new();

	/// <summary>Quantas contas ficam salvas neste computador (Jhon, 25/11);
	/// a mais nova entra e a mais antiga sai.</summary>
	public const int MaxSaved = 3;

	/// <summary>Os acessos salvos, o mais recente primeiro.</summary>
	public static System.Collections.Generic.IReadOnlyList<SavedAccess> Saved
	{
		get
		{
			Load();
			return _saved;
		}
	}

	/// <summary>Guardar o acesso da próxima entrada (o "lembrar" da tela de login).</summary>
	public static bool KeepAccess { get; set; } = true;

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

	/// <summary>
	/// Entra com um acesso salvo (o clique na conta, na tela de login):
	/// confere no servidor se o login ainda vale. Não vale mais (venceu,
	/// saiu em outro lugar), o acesso sai da lista e a tela pede a senha.
	/// </summary>
	public static async Task<Reply> EnterSaved(SavedAccess access)
	{
		Load();
		_url = access.Url;
		Token = access.Token;
		AccountId = access.Id;
		AccountName = access.Name;
		KeepAccess = true;

		Reply profile = await Profile();
		if (profile.Ok) Remember(Token, profile.Body);
		else
		{
			if (profile.Status == 401) Drop(access.Url, access.Id);
			Token = null;
			AccountId = 0;
			AccountName = "";
			Save();
		}
		return profile;
	}

	/// <summary>Fecha a sessão sem sair da conta — o que acontece ao fechar
	/// o jogo: o acesso salvo fica, e a próxima entrada é o clique nele. Os
	/// testes usam para "abrir o jogo de novo".</summary>
	public static void EndSession()
	{
		Token = null;
		AccountId = 0;
		AccountName = "";
		AccountSync.SessionEnded();
		SessionChanged?.Invoke();
	}

	/// <summary>Tira um acesso da lista (o "esquecer" da tela de login).</summary>
	public static void ForgetSaved(SavedAccess access)
	{
		Drop(access.Url, access.Id);
		Save();
	}

	/// <summary>Confere se o login desta sessão ainda vale; se não, esquece.</summary>
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

	/// <summary>O que o servidor tem na conta: itens, carteira e patente.</summary>
	public static Task<Reply> Inventory() => Call(HttpMethod.Get, "/v1/inventory");

	/// <summary>Compra na loja do servidor. Ele confere preço, patente e
	/// saldo: o jogo só diz o item e a duração. O número do pedido (um por
	/// clique) faz o mesmo pedido mandado de novo não comprar duas vezes.</summary>
	public static Task<Reply> Buy(string itemId, Duration duration, string requestId) =>
		Call(HttpMethod.Post, "/v1/shop/buy", new JsonObject { ["itemId"] = itemId, ["duration"] = (int)duration, ["requestId"] = requestId });

	/// <summary>Joga fora um item do inventário da conta.</summary>
	public static Task<Reply> Discard(string itemId) => Call(HttpMethod.Delete, $"/v1/inventory/{Uri.EscapeDataString(itemId)}");

	/// <summary>Troca o nome da conta (gasta um item Troca de nome).</summary>
	public static Task<Reply> Rename(string name) => Call(HttpMethod.Post, "/v1/account/name", new JsonObject { ["name"] = name });

	/// <summary>Os desafios valendo e o progresso da conta em cada um.</summary>
	public static Task<Reply> Challenges() => Call(HttpMethod.Get, "/v1/challenges");

	/// <summary>Recebe o prêmio de um desafio completo.</summary>
	public static Task<Reply> ClaimChallenge(string id) => Call(HttpMethod.Post, $"/v1/challenges/{Uri.EscapeDataString(id)}/claim", new JsonObject());

	/// <summary>Os dados do jogo que moram no servidor (<see cref="ServerData"/>).
	/// Com a etiqueta da última resposta, o servidor responde 304 e sem
	/// corpo quando nada mudou. Status 0: sem servidor.</summary>
	public static async Task<(int Status, string ETag, JsonObject Body)> GameData(string etag)
	{
		try
		{
			var request = new HttpRequestMessage(HttpMethod.Get, Url + "/v1/gamedata");
			if (!string.IsNullOrEmpty(etag)) request.Headers.TryAddWithoutValidation("If-None-Match", etag);
			using HttpResponseMessage response = await Http.SendAsync(request);
			if ((int)response.StatusCode != 200) return ((int)response.StatusCode, etag, null);
			return (200, response.Headers.ETag?.Tag, JsonNode.Parse(await response.Content.ReadAsStringAsync()) as JsonObject);
		}
		catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or UriFormatException)
		{
			return (0, etag, null);
		}
	}

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
			// Sem isto, "sem conexão" não diz se foi recusa, certificado, DNS
			// ou tempo esgotado: o log do jogo fica mudo.
			GD.PushWarning($"Backend: {method} {Url}{path} falhou: {e.GetType().Name}: {e.Message}"
				+ (e.InnerException != null ? $" ({e.InnerException.Message})" : ""));
			return new Reply(0, null);
		}
	}

	private static void Remember(string token, JsonObject profile)
	{
		Token = token;
		AccountId = profile?["id"]?.GetValue<long>() ?? 0;
		AccountName = profile?["name"]?.GetValue<string>() ?? "";
		Drop(Url, AccountId);
		if (KeepAccess) _saved.Insert(0, new SavedAccess(Url, AccountId, AccountName, Token, DateTime.UtcNow.ToString("o")));
		Save();
		AccountSync.SessionStarted(profile);
		SessionChanged?.Invoke();
	}

	/// <summary>O servidor trocou o nome da conta.</summary>
	public static void Renamed(string name)
	{
		AccountName = name ?? AccountName;
		int at = _saved.FindIndex(a => a.Id == AccountId && a.Url == Url);
		if (at >= 0)
		{
			_saved[at] = _saved[at] with { Name = AccountName };
			Save();
		}
		SessionChanged?.Invoke();
	}

	/// <summary>O servidor disse que o login não vale mais (venceu, a conta
	/// foi banida ou derrubada pela moderação).</summary>
	public static void SessionExpired() => Forget();

	/// <summary>Sai da sessão. O login desta conta não vale mais (saiu,
	/// venceu, foi derrubada): o acesso salvo dela sai junto.</summary>
	private static void Forget()
	{
		if (AccountId != 0) Drop(Url, AccountId);
		Token = null;
		AccountId = 0;
		AccountName = "";
		Save();
		AccountSync.SessionEnded();
		SessionChanged?.Invoke();
	}

	private static void Drop(string url, long id) => _saved.RemoveAll(a => a.Id == id && a.Url == url);

	private static void Load()
	{
		if (_loaded) return;
		_loaded = true;

		var file = new ConfigFile();
		if (file.Load(FilePath) != Godot.Error.Ok) return;

		// [server] e [accounts] (até 21/11, [servidor] e [acessos]).
		_url = file.GetValue("server", "url", file.GetValue("servidor", "url", DefaultUrl)).AsString();

		string list = file.GetValue("accounts", "list", file.GetValue("acessos", "lista", "")).AsString();
		if (list.Length > 0)
		{
			try
			{
				if (JsonNode.Parse(list) is JsonArray array)
					foreach (JsonNode a in array)
					{
						string token = a?["token"]?.GetValue<string>() ?? "";
						if (token.Length == 0) continue;
						_saved.Add(new SavedAccess(a["url"]?.GetValue<string>() ?? DefaultUrl, a["id"]?.GetValue<long>() ?? 0,
							a["name"]?.GetValue<string>() ?? "", token, a["savedAt"]?.GetValue<string>() ?? ""));
					}
			}
			catch (JsonException) { }
		}

		// O arquivo de antes (um login só, que entrava sozinho) vira um
		// acesso salvo: a conta aparece na tela de login para clicar.
		string old = file.GetValue("conta", "token", "").AsString();
		if (old.Length > 0)
		{
			long id = file.GetValue("conta", "id", 0).AsInt64();
			if (!_saved.Exists(a => a.Id == id && a.Url == _url))
				_saved.Insert(0, new SavedAccess(_url, id, file.GetValue("conta", "nome", "").AsString(), old, ""));
		}
		// Quem já tinha mais que o máximo guardado fica com as mais recentes.
		if (_saved.Count > MaxSaved) _saved.RemoveRange(MaxSaved, _saved.Count - MaxSaved);
	}

	private static void Save()
	{
		if (_saved.Count > MaxSaved) _saved.RemoveRange(MaxSaved, _saved.Count - MaxSaved);
		var array = new JsonArray();
		foreach (SavedAccess a in _saved)
			array.Add(new JsonObject { ["url"] = a.Url, ["id"] = a.Id, ["name"] = a.Name, ["token"] = a.Token, ["savedAt"] = a.SavedAt });

		var file = new ConfigFile();
		file.SetValue("server", "url", _url);
		file.SetValue("accounts", "list", array.ToJsonString());
		file.Save(FilePath);
	}
}
