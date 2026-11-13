using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Alcateia.Servidor.Banco;
using Microsoft.AspNetCore.Builder;

namespace Alcateia.Servidor.Testes;

/// <summary>Uma verificação que não passou.</summary>
public sealed class Failed : Exception
{
	public Failed(string what) : base(what) { }
}

public static class Check
{
	public static int Count;

	public static void True(bool condition, string what)
	{
		Count++;
		if (!condition) throw new Failed(what);
	}

	public static void Equal<T>(T expected, T actual, string what)
	{
		Count++;
		if (!EqualityComparer<T>.Default.Equals(expected, actual))
			throw new Failed($"{what}: esperado {expected}, veio {actual}");
	}

	public static async Task Throws<T>(Func<Task> work, string what) where T : Exception
	{
		Count++;
		try { await work(); }
		catch (T) { return; }
		throw new Failed($"{what}: devia ter falhado");
	}
}

/// <summary>Um jogador de teste: fala HTTP com o token dele e, se
/// quiser, abre o WebSocket do saguão.</summary>
public sealed class Player : IAsyncDisposable
{
	public string Name, Token;
	public long Id;
	private readonly World _world;
	private ClientWebSocket _socket;
	private readonly List<JsonObject> _inbox = new();

	public Player(World world) => _world = world;

	public async Task<(int Status, JsonObject Body)> Call(HttpMethod method, string path, JsonObject body = null, string fleetKey = null)
	{
		var request = new HttpRequestMessage(method, _world.Url + path);
		if (Token != null) request.Headers.Add("Authorization", "Bearer " + Token);
		if (fleetKey != null) request.Headers.Add("X-Fleet-Key", fleetKey);
		if (body != null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

		HttpResponseMessage response = await _world.Http.SendAsync(request);
		string text = await response.Content.ReadAsStringAsync();
		return ((int)response.StatusCode, text.Length > 0 ? JsonNode.Parse(text) as JsonObject : null);
	}

	public async Task Connect()
	{
		_socket = new ClientWebSocket();
		await _socket.ConnectAsync(new Uri(_world.Url.Replace("http://", "ws://") + "/v1/lobby?token=" + Token), CancellationToken.None);
	}

	public Task Say(JsonObject message) =>
		_socket.SendAsync(Encoding.UTF8.GetBytes(message.ToJsonString()), WebSocketMessageType.Text, true, CancellationToken.None);

	/// <summary>Espera a próxima mensagem do tipo pedido (as outras ficam
	/// guardadas para quem as quiser depois).</summary>
	public async Task<JsonObject> Hear(string type, Func<JsonObject, bool> where = null, int timeoutMs = 4000)
	{
		var clock = Stopwatch.StartNew();
		while (true)
		{
			int at = _inbox.FindIndex(m => m["t"]?.GetValue<string>() == type && (where == null || where(m)));
			if (at >= 0)
			{
				JsonObject found = _inbox[at];
				_inbox.RemoveAt(at);
				return found;
			}

			int left = timeoutMs - (int)clock.ElapsedMilliseconds;
			if (left <= 0) throw new Failed($"{Name}: esperava '{type}' e não veio. Chegou: {string.Join(", ", _inbox.Select(m => m["t"]))}");

			var buffer = new byte[32 * 1024];
			using var cancel = new CancellationTokenSource(left);
			try
			{
				var text = new StringBuilder();
				WebSocketReceiveResult r;
				do
				{
					r = await _socket.ReceiveAsync(buffer, cancel.Token);
					if (r.MessageType == WebSocketMessageType.Close) throw new Failed($"{Name}: o servidor fechou a conexão esperando '{type}'");
					text.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
				} while (!r.EndOfMessage);
				_inbox.Add((JsonObject)JsonNode.Parse(text.ToString()));
			}
			catch (OperationCanceledException)
			{
				// O laço confere o tempo e reclama.
			}
		}
	}

	public void Forget() => _inbox.Clear();

	public bool Closed => _socket == null || _socket.State != WebSocketState.Open;

	public async ValueTask DisposeAsync()
	{
		if (_socket is { State: WebSocketState.Open })
		{
			try { await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
			catch (WebSocketException) { }
		}
		_socket?.Dispose();
	}
}

/// <summary>O servidor de pé para um teste, com o banco limpo.</summary>
public sealed class World : IAsyncDisposable
{
	public string Url;
	public Config Config;
	public HttpClient Http = new();
	public PgPool Db;
	private WebApplication _app;
	private static int _names;

	public static PgOptions Database() => new()
	{
		Host = Env("ALCATEIA_TEST_DB_HOST", "127.0.0.1"),
		Port = int.Parse(Env("ALCATEIA_TEST_DB_PORT", "55432")),
		User = Env("ALCATEIA_TEST_DB_USER", "alcateia"),
		Password = Env("ALCATEIA_TEST_DB_PASSWORD", "segredo-de-teste"),
		Database = Env("ALCATEIA_TEST_DB_NAME", "alcateia_teste")
	};

	public static async Task<World> Start(Action<Config> tweak = null)
	{
		var world = new World { Config = new Config { Database = Database(), Port = FreePort(), FleetKey = "chave-de-teste" } };
		world.Config.LoginsPerMinute = 1000;
		tweak?.Invoke(world.Config);

		world.Db = new PgPool(world.Config.Database);
		await world.Db.Use(async c =>
		{
			await c.Script("drop schema public cascade; create schema public;");
			return 0;
		});

		world._app = await App.Build(world.Config);
		await world._app.StartAsync();
		world.Url = $"http://127.0.0.1:{world.Config.Port}";
		return world;
	}

	/// <summary>Cria uma conta nova e devolve o jogador já logado.</summary>
	public async Task<Player> NewPlayer(string name = null)
	{
		var p = new Player(this) { Name = name ?? $"Lobo{Interlocked.Increment(ref _names)}" };
		var (status, body) = await p.Call(HttpMethod.Post, "/v1/accounts", new JsonObject { ["name"] = p.Name, ["password"] = "senha-boa-123" });
		Check.Equal(201, status, $"criar a conta {p.Name}");
		p.Token = body["token"].GetValue<string>();
		p.Id = body["profile"]["id"].GetValue<long>();
		return p;
	}

	public Player Server() => new(this);

	public async ValueTask DisposeAsync()
	{
		await _app.StopAsync();
		await _app.DisposeAsync();
		await Db.DisposeAsync();
		Http.Dispose();
	}

	private static int FreePort()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		int port = ((IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();
		return port;
	}

	private static string Env(string name, string fallback)
	{
		string value = Environment.GetEnvironmentVariable(name);
		return string.IsNullOrEmpty(value) ? fallback : value;
	}
}

public static class Program
{
	public static async Task<int> Main(string[] args)
	{
		string filter = args.FirstOrDefault(a => !a.StartsWith("--"));

		try
		{
			await using PgConnection probe = await PgConnection.Open(World.Database());
		}
		catch (Exception e) when (e is SocketException or PgException)
		{
			PgOptions o = World.Database();
			Console.WriteLine($"Não consegui falar com o PostgreSQL de teste em {o.Host}:{o.Port} ({e.Message}).");
			Console.WriteLine("Suba um e rode de novo — por exemplo:");
			Console.WriteLine($"  docker run --rm -d -p {o.Port}:5432 -e POSTGRES_USER={o.User} -e POSTGRES_PASSWORD={o.Password} -e POSTGRES_DB={o.Database} postgres:16");
			return 2;
		}

		var tests = new List<(string Name, Func<Task> Run)>();
		tests.AddRange(DatabaseTests.All);
		tests.AddRange(AccountTests.All);
		tests.AddRange(LobbyTests.All);
		tests.AddRange(MatchTests.All);

		int passed = 0, failed = 0;
		foreach (var (name, run) in tests)
		{
			if (filter != null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
			try
			{
				await run();
				passed++;
				Console.WriteLine($"  ok      {name}");
			}
			catch (Exception e)
			{
				failed++;
				Console.WriteLine($"  FALHOU  {name}");
				Console.WriteLine($"          {(e is Failed ? e.Message : e.ToString())}");
			}
		}

		Console.WriteLine($"{passed} de {passed + failed} testes do servidor passaram ({Check.Count} verificações).");
		return failed == 0 ? 0 : 1;
	}
}
