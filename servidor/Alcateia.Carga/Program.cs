using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Alcateia.Carga;

/// <summary>
/// Teste de carga do servidor do Alcateia. Sobe, contra um servidor de pé:
///
/// 1. <c>--usuarios</c> contas novas (cadastro de verdade, com PBKDF2);
/// 2. <c>--frota</c> servidores de partida falsos, de <c>--lugares</c>
///    partidas cada, batendo a cada 2 s como o jogo bate;
/// 3. um WebSocket do saguão por jogador, todos ao mesmo tempo;
/// 4. <c>--salas</c> salas: o dono cria, os outros entram e ficam prontos,
///    o dono começa;
/// 5. cada servidor falso abre as partidas que recebe e, depois de
///    <c>--partida</c> segundos, manda o resultado; os jogadores recebem o
///    fim e o prêmio.
///
/// Enquanto isso, pings de jogadores sorteados medem quanto o saguão
/// demora para responder com tudo acontecendo. No fim: quantos de cada
/// coisa deram certo e os tempos (mediana, p95, p99, pior).
///
///     dotnet run -c Release --project servidor/Alcateia.Carga -- \
///       --url=http://127.0.0.1:8080 --chave=SEGREDO --usuarios=3000 --salas=1000 --frota=250 --lugares=4
/// </summary>
public static class Program
{
	private static string _url = "http://127.0.0.1:8080", _key = "";
	private static int _users = 3000, _rooms = 1000, _fleet = 250, _slots = 4;
	private static double _matchSeconds = 15;

	private static readonly HttpClient Http = new(new SocketsHttpHandler { MaxConnectionsPerServer = 256 }) { Timeout = TimeSpan.FromSeconds(60) };
	private static readonly ConcurrentDictionary<string, ConcurrentBag<double>> Times = new();
	private static readonly ConcurrentDictionary<string, int> Counts = new();
	private static readonly ConcurrentDictionary<string, int> Errors = new();

	public static async Task<int> Main(string[] args)
	{
		foreach (string a in args)
		{
			string Value() => a[(a.IndexOf('=') + 1)..];
			if (a.StartsWith("--url=")) _url = Value();
			else if (a.StartsWith("--chave=")) _key = Value();
			else if (a.StartsWith("--usuarios=")) _users = int.Parse(Value());
			else if (a.StartsWith("--salas=")) _rooms = int.Parse(Value());
			else if (a.StartsWith("--frota=")) _fleet = int.Parse(Value());
			else if (a.StartsWith("--lugares=")) _slots = int.Parse(Value());
			else if (a.StartsWith("--partida=")) _matchSeconds = double.Parse(Value(), System.Globalization.CultureInfo.InvariantCulture);
		}
		_rooms = Math.Min(_rooms, _users);
		ThreadPool.SetMinThreads(256, 256);

		Console.WriteLine($"Carga: {_users} jogadores, {_rooms} salas, {_fleet} servidores de partida × {_slots} lugares, contra {_url}");
		var clock = Stopwatch.StartNew();
		string run = Convert.ToString(DateTime.UtcNow.Ticks % 1_000_000, 16);

		// 1. Contas.
		var players = new Player[_users];
		await Parallel(_users, 64, async i =>
		{
			players[i] = await Player.Register($"C{run}{i}");
		});
		Console.WriteLine($"  contas: {players.Count(p => p != null)} em {clock.Elapsed.TotalSeconds:0.0} s");

		// 2. Frota.
		using var stop = new CancellationTokenSource();
		var servers = Enumerable.Range(0, _fleet).Select(i => new FakeGameServer($"carga-{run}-{i}", 20000 + i)).ToList();
		List<Task> beating = servers.Select(s => s.Run(stop.Token)).ToList();
		await Task.Delay(2500);

		// 3. Saguão.
		var connected = Stopwatch.StartNew();
		await Parallel(_users, 200, async i =>
		{
			if (players[i] != null) await players[i].Connect();
		});
		Player[] online = players.Where(p => p is { Online: true }).ToArray();
		Console.WriteLine($"  saguão: {online.Length} ligados em {connected.Elapsed.TotalSeconds:0.0} s");

		using var pings = new CancellationTokenSource();
		Task pinging = Ping(online, pings.Token);

		// 4. Salas: o dono cria, os outros entram e ficam prontos, o dono começa.
		Player[] hosts = online.Take(_rooms).ToArray();
		Player[] guests = online.Skip(_rooms).ToArray();
		var roomOf = new int[hosts.Length];
		await Parallel(hosts.Length, 200, async i =>
		{
			roomOf[i] = await hosts[i].CreateRoom();
		});
		Console.WriteLine($"  salas criadas: {roomOf.Count(r => r > 0)}");

		await Parallel(guests.Length, 200, async i =>
		{
			int host = i % hosts.Length;
			if (roomOf[host] > 0) await guests[i].JoinAndReady(roomOf[host]);
		});

		var started = new bool[hosts.Length];
		await Parallel(hosts.Length, 200, async i =>
		{
			int expected = guests.Length / hosts.Length + (i < guests.Length % hosts.Length ? 1 : 0);
			if (roomOf[i] > 0) started[i] = await hosts[i].StartWhenReady(expected);
		});
		int playing = started.Count(x => x);
		Console.WriteLine($"  partidas começadas: {playing}");

		// 5. O fim: cada um recebe o resultado.
		var ending = Stopwatch.StartNew();
		int ended = 0;
		await Parallel(online.Length, 500, async i =>
		{
			if (await online[i].WaitEnd(TimeSpan.FromSeconds(_matchSeconds + 40))) Interlocked.Increment(ref ended);
		});
		Console.WriteLine($"  fim da partida recebido por {ended} jogadores (esperado: os {online.Count(p => p.Started)} que começaram) em {ending.Elapsed.TotalSeconds:0.0} s");

		pings.Cancel();
		await pinging;
		stop.Cancel();
		await Task.WhenAll(beating);

		JsonNode health = JsonNode.Parse(await Http.GetStringAsync(_url + "/health"));
		foreach (Player p in online) await p.Close();

		// Resultado.
		Console.WriteLine();
		Console.WriteLine($"Total: {clock.Elapsed.TotalSeconds:0.0} s. Servidor no fim: {health?.ToJsonString()}");
		Console.WriteLine();
		Console.WriteLine($"{"o quê",-28} {"vezes",8} {"mediana",9} {"p95",9} {"p99",9} {"pior",9}  (ms)");
		foreach (var (what, bag) in Times.OrderBy(t => t.Key))
		{
			double[] t = bag.OrderBy(x => x).ToArray();
			double P(double q) => t.Length == 0 ? 0 : t[Math.Min(t.Length - 1, (int)(q * t.Length))];
			Console.WriteLine($"{what,-28} {t.Length,8} {P(0.5),9:0.0} {P(0.95),9:0.0} {P(0.99),9:0.0} {t.LastOrDefault(),9:0.0}");
		}
		foreach (var (what, n) in Counts.OrderBy(c => c.Key)) Console.WriteLine($"  {what}: {n}");
		if (Errors.IsEmpty) Console.WriteLine("Nenhum erro.");
		else foreach (var (code, n) in Errors.OrderByDescending(e => e.Value)) Console.WriteLine($"  ERRO {code}: {n}");

		bool ok = playing == hosts.Length && ended >= online.Count(p => p.Started) && Errors.IsEmpty;
		Console.WriteLine(ok ? "A carga passou." : "A carga NÃO passou.");
		return ok ? 0 : 1;
	}

	// ------------------------------------------------------------ medida

	public static void Time(string what, double ms) => Times.GetOrAdd(what, _ => new ConcurrentBag<double>()).Add(ms);
	public static void Count(string what) => Counts.AddOrUpdate(what, 1, (_, n) => n + 1);
	public static void Error(string code) => Errors.AddOrUpdate(code, 1, (_, n) => n + 1);

	private static async Task Parallel(int count, int width, Func<int, Task> work)
	{
		using var gate = new SemaphoreSlim(width);
		var tasks = new List<Task>(count);
		for (int i = 0; i < count; i++)
		{
			int index = i;
			await gate.WaitAsync();
			tasks.Add(Task.Run(async () =>
			{
				try { await work(index); }
				catch (Exception e) { Error(e.GetType().Name); }
				finally { gate.Release(); }
			}));
		}
		await Task.WhenAll(tasks);
	}

	/// <summary>Pings de jogadores sorteados, sem parar, enquanto o resto acontece.</summary>
	private static async Task Ping(Player[] online, CancellationToken stop)
	{
		var rng = new Random(7);
		while (!stop.IsCancellationRequested)
		{
			Player[] some = Enumerable.Range(0, 20).Select(_ => online[rng.Next(online.Length)]).ToArray();
			await Task.WhenAll(some.Select(p => p.Ping()));
			try { await Task.Delay(100, stop); } catch (OperationCanceledException) { }
		}
	}

	// ------------------------------------------------------------ um jogador

	private sealed class Player
	{
		public string Name, Token;
		public long Id;
		public bool Online => _socket?.State == WebSocketState.Open;
		public bool Started;

		private ClientWebSocket _socket;
		private bool _closing;
		private readonly SemaphoreSlim _sending = new(1, 1);
		private readonly List<(Func<JsonObject, bool> Match, TaskCompletionSource<JsonObject> Done)> _waiting = new();
		private readonly List<JsonObject> _kept = new();

		public static async Task<Player> Register(string name)
		{
			var sw = Stopwatch.StartNew();
			var body = new JsonObject { ["name"] = name, ["password"] = "senha-de-carga-123" };
			HttpResponseMessage r = await Http.PostAsync(_url + "/v1/accounts", new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
			string text = await r.Content.ReadAsStringAsync();
			Time("cadastro (HTTP)", sw.Elapsed.TotalMilliseconds);
			if (!r.IsSuccessStatusCode)
			{
				Error($"cadastro {(int)r.StatusCode}");
				return null;
			}
			JsonNode j = JsonNode.Parse(text);
			return new Player { Name = name, Token = j["token"].GetValue<string>(), Id = j["profile"]["id"].GetValue<long>() };
		}

		public async Task Connect()
		{
			var sw = Stopwatch.StartNew();
			_socket = new ClientWebSocket();
			Task<JsonObject> hello = Wait(m => Is(m, "rooms"), TimeSpan.FromSeconds(30));
			await _socket.ConnectAsync(new Uri(_url.Replace("http", "ws") + "/v1/lobby?token=" + Token), CancellationToken.None);
			_ = Receive();
			if (await hello != null) Time("entrar no saguão", sw.Elapsed.TotalMilliseconds);
		}

		public async Task<int> CreateRoom()
		{
			var sw = Stopwatch.StartNew();
			Task<JsonObject> room = Wait(m => Is(m, "room"), TimeSpan.FromSeconds(20));
			await Say(new JsonObject
			{
				["t"] = "room.create",
				["settings"] = new JsonObject { ["title"] = "Carga " + Name, ["teamSize"] = 4, ["map"] = "armazem", ["minutes"] = 3, ["scoreToWin"] = 40 }
			});
			JsonObject got = await room;
			if (got == null) return 0;
			Time("criar sala", sw.Elapsed.TotalMilliseconds);
			return got["id"].GetValue<int>();
		}

		public async Task JoinAndReady(int roomId)
		{
			var sw = Stopwatch.StartNew();
			Task<JsonObject> room = Wait(m => Is(m, "room") && m["id"].GetValue<int>() == roomId, TimeSpan.FromSeconds(20));
			await Say(new JsonObject { ["t"] = "room.join", ["id"] = roomId });
			if (await room == null) return;
			Time("entrar na sala", sw.Elapsed.TotalMilliseconds);

			sw.Restart();
			Task<JsonObject> ready = Wait(m => Is(m, "room") && Mine(m)?["ready"]?.GetValue<bool>() == true, TimeSpan.FromSeconds(20));
			await Say(new JsonObject { ["t"] = "room.ready", ["ready"] = true });
			if (await ready != null) Time("ficar pronto", sw.Elapsed.TotalMilliseconds);
		}

		/// <summary>Espera os convidados ficarem prontos e começa. Verdadeiro se veio o match.start.</summary>
		public async Task<bool> StartWhenReady(int guests)
		{
			bool AllReady(JsonObject m) => Is(m, "room")
				&& m["members"].AsArray().Count(x => !x["host"].GetValue<bool>() && x["ready"].GetValue<bool>()) >= guests;
			if (await Wait(AllReady, TimeSpan.FromSeconds(30), keep: true) == null) return false;

			var sw = Stopwatch.StartNew();
			Task<JsonObject> start = Wait(m => Is(m, "match.start"), TimeSpan.FromSeconds(30), keep: true);
			await Say(new JsonObject { ["t"] = "room.start" });
			if (await start == null) return false;
			Time("começar a partida", sw.Elapsed.TotalMilliseconds);
			return true;
		}

		public async Task<bool> WaitEnd(TimeSpan timeout)
		{
			// Todo jogador cuja sala começou recebeu o match.start (guardado).
			Started = Kept(m => Is(m, "match.start"));
			if (!Started) return false;
			JsonObject end = await Wait(m => Is(m, "match.end"), timeout, keep: true);
			if (end == null) return false;
			Count("prêmio recebido");
			return end["you"]?["xp"] != null;
		}

		public async Task Ping()
		{
			if (!Online) return;
			var sw = Stopwatch.StartNew();
			Task<JsonObject> pong = Wait(m => Is(m, "pong"), TimeSpan.FromSeconds(10));
			await Say(new JsonObject { ["t"] = "ping" });
			if (await pong != null) Time("ping do saguão", sw.Elapsed.TotalMilliseconds);
		}

		public async Task Close()
		{
			_closing = true;
			try
			{
				if (Online) await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
			}
			catch (Exception) { }
		}

		private JsonObject Mine(JsonObject room) =>
			room["members"].AsArray().FirstOrDefault(x => x["id"].GetValue<long>() == Id) as JsonObject;

		private static bool Is(JsonObject m, string type) => m["t"]?.GetValue<string>() == type;

		private async Task Say(JsonObject message)
		{
			await _sending.WaitAsync();
			try
			{
				await _socket.SendAsync(Encoding.UTF8.GetBytes(message.ToJsonString()), WebSocketMessageType.Text, true, CancellationToken.None);
			}
			catch (Exception e) when (e is WebSocketException or InvalidOperationException) { Error("enviar"); }
			finally { _sending.Release(); }
		}

		/// <summary>Espera uma mensagem que case. <paramref name="keep"/>:
		/// olha também as já guardadas (match.start, match.end e room chegam
		/// antes de alguém esperar).</summary>
		private Task<JsonObject> Wait(Func<JsonObject, bool> match, TimeSpan timeout, bool keep = false)
		{
			var done = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
			lock (_waiting)
			{
				if (keep)
				{
					JsonObject old = _kept.LastOrDefault(match);
					if (old != null) return Task.FromResult(old);
				}
				_waiting.Add((match, done));
			}
			_ = Task.Delay(timeout).ContinueWith(_ =>
			{
				if (done.TrySetResult(null)) Error("sem resposta");
			});
			return done.Task;
		}

		private bool Kept(Func<JsonObject, bool> match)
		{
			lock (_waiting) return _kept.Any(match);
		}

		private async Task Receive()
		{
			var buffer = new byte[64 * 1024];
			var text = new StringBuilder();
			try
			{
				while (_socket.State == WebSocketState.Open)
				{
					WebSocketReceiveResult r = await _socket.ReceiveAsync(buffer, CancellationToken.None);
					if (r.MessageType == WebSocketMessageType.Close) break;
					text.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
					if (!r.EndOfMessage) continue;

					var m = (JsonObject)JsonNode.Parse(text.ToString());
					text.Clear();
					string type = m["t"]?.GetValue<string>();
					Count("mensagem " + type);
					if (type == "error") Error(m["code"]?.GetValue<string>() ?? "?");

					lock (_waiting)
					{
						if (type is "room" or "match.start" or "match.end")
						{
							_kept.Add(m);
							if (_kept.Count > 50) _kept.RemoveAt(0);
						}
						foreach (var w in _waiting.Where(w => !w.Done.Task.IsCompleted && w.Match(m)).ToList())
						{
							w.Done.TrySetResult(m);
							_waiting.Remove(w);
						}
						_waiting.RemoveAll(w => w.Done.Task.IsCompleted);
					}
				}
			}
			catch (Exception e) when (e is WebSocketException or OperationCanceledException) { }
			if (!_closing) Count("saguão caiu");
		}
	}

	// ------------------------------------------------------------ um servidor de partida falso

	private sealed class FakeGameServer
	{
		private readonly string _id;
		private readonly int _port;
		private readonly ConcurrentDictionary<string, JsonObject> _open = new();

		public FakeGameServer(string id, int port)
		{
			_id = id;
			_port = port;
		}

		public async Task Run(CancellationToken stop)
		{
			var rng = new Random(_port);
			try { await Task.Delay(rng.Next(2000), stop); } catch (OperationCanceledException) { return; }

			while (!stop.IsCancellationRequested)
			{
				var sw = Stopwatch.StartNew();
				var open = new JsonArray();
				foreach (string key in _open.Keys) open.Add(key);
				var body = new JsonObject
				{
					["id"] = _id, ["address"] = "10.20.0.1", ["port"] = _port, ["players"] = 0, ["capacity"] = _slots, ["matches"] = open
				};

				try
				{
					var request = new HttpRequestMessage(HttpMethod.Post, _url + "/v1/fleet/heartbeat")
					{
						Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
					};
					request.Headers.Add("X-Fleet-Key", _key);
					HttpResponseMessage r = await Http.SendAsync(request, stop);
					string text = await r.Content.ReadAsStringAsync(stop);
					Time("batida da frota (HTTP)", sw.Elapsed.TotalMilliseconds);
					if (!r.IsSuccessStatusCode) Error($"batida {(int)r.StatusCode}");
					else
						foreach (JsonNode a in JsonNode.Parse(text)["assignments"]?.AsArray() ?? new JsonArray())
						{
							string key = a["matchKey"].GetValue<string>();
							_open[key] = (JsonObject)a;
							Count("partida entregue à frota");
							_ = Finish(key, (JsonObject)a, stop);
						}
				}
				catch (OperationCanceledException) { return; }
				catch (HttpRequestException) { Error("batida caiu"); }

				try { await Task.Delay(2000, stop); } catch (OperationCanceledException) { return; }
			}
		}

		/// <summary>A partida "joga" e o resultado vai como o jogo manda.</summary>
		private async Task Finish(string key, JsonObject assignment, CancellationToken stop)
		{
			var rng = new Random(key.GetHashCode());
			try { await Task.Delay(TimeSpan.FromSeconds(_matchSeconds * (0.8 + 0.4 * rng.NextDouble())), stop); }
			catch (OperationCanceledException) { return; }

			var players = new JsonArray();
			foreach (JsonNode p in assignment["players"].AsArray())
				players.Add(new JsonObject
				{
					["accountId"] = p["accountId"].GetValue<long>(), ["kills"] = rng.Next(20), ["deaths"] = rng.Next(20),
					["assists"] = rng.Next(5), ["headshots"] = rng.Next(5), ["points"] = rng.Next(300)
				});
			var body = new JsonObject
			{
				["matchKey"] = key, ["serverId"] = _id, ["scoreBlue"] = 40, ["scoreOrange"] = rng.Next(40), ["players"] = players
			};

			var sw = Stopwatch.StartNew();
			var request = new HttpRequestMessage(HttpMethod.Post, _url + "/v1/matches")
			{
				Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
			};
			request.Headers.Add("X-Fleet-Key", _key);
			HttpResponseMessage r = await Http.SendAsync(request);
			Time("resultado da partida (HTTP)", sw.Elapsed.TotalMilliseconds);
			if (!r.IsSuccessStatusCode) Error($"resultado {(int)r.StatusCode}");
			_open.TryRemove(key, out _);
		}
	}
}
