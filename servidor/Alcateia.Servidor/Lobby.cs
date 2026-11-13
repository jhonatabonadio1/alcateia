using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Alcateia.Servidor;

/// <summary>
/// O saguão: as salas, quem está em cada uma e o chat, por WebSocket.
///
/// Mensagens em JSON com um campo <c>t</c> dizendo o que são. O cliente
/// manda <c>rooms.list</c>, <c>room.create</c>, <c>room.join</c>,
/// <c>room.leave</c>, <c>room.team</c>, <c>room.ready</c>,
/// <c>room.settings</c>, <c>room.bot.add</c>, <c>room.bot.remove</c>,
/// <c>room.start</c>, <c>chat</c> e <c>ping</c>; o
/// servidor responde com <c>hello</c>, <c>rooms</c>, <c>room</c>,
/// <c>left</c>, <c>chat</c>, <c>match.start</c>, <c>match.end</c>,
/// <c>pong</c> e <c>error</c>. O formato de cada uma está em
/// servidor/LEIA-ME.md.
///
/// As salas vivem em memória: sala é coisa de agora, e se o servidor
/// reiniciar os jogadores voltam para a lista. O que fica no banco é a
/// conta, o perfil e as partidas jogadas.
/// </summary>
public sealed class Lobby
{
	public const int MaxTitle = 40, MaxPassword = 32, MaxChat = 200, MaxRooms = 500;

	private sealed class Client
	{
		public Account Account;
		public WebSocket Socket;
		public Room Room;
		public long Xp;
		public readonly SemaphoreSlim Sending = new(1, 1);
		public readonly Queue<DateTime> Chats = new();
	}

	private sealed class Member
	{
		public Client Client;
		public int Team;
		public bool Ready;
	}

	/// <summary>Um bot posto na sala pelo dono. Número negativo, único na
	/// sala; sem conta, sem XP, sem patente.</summary>
	private sealed class BotSeat
	{
		public long Id;
		public string Name;
		public int Team;
	}

	private sealed class Room
	{
		public int Id;
		public JsonObject Settings;
		public string Password = "";
		public long HostId;
		public readonly List<Member> Members = new();
		public readonly List<BotSeat> Bots = new();
		public long NextBot = -1;
		public string State = "waiting";
		public string MatchKey;

		public int TeamSize => Settings["teamSize"]?.GetValue<int>() ?? 8;
		public int Capacity => TeamSize * 2;

		/// <summary>Quantos lugares o time tem ocupados, gente e bots.</summary>
		public int Taken(int team, Member except = null) =>
			Members.Count(m => m.Team == team && m != except) + Bots.Count(b => b.Team == team);
	}

	/// <summary>Os nomes dos bots, na ordem em que entram.</summary>
	private static readonly string[] BotNames =
		{ "Fenrir", "Lobo-Guará", "Presa", "Uivo", "Sombra", "Garra", "Alfa", "Matilha", "Caçador", "Lua", "Breu", "Rastro", "Faro", "Vulto", "Trovão", "Cinza" };

	private readonly Fleet _fleet;
	private readonly Accounts _accounts;
	private readonly Config _config;
	private readonly object _lock = new();
	private readonly Dictionary<long, Client> _clients = new();
	private readonly Dictionary<int, Room> _rooms = new();
	private int _nextRoom = 1;

	public Lobby(Fleet fleet, Accounts accounts, Config config)
	{
		_fleet = fleet;
		_accounts = accounts;
		_config = config;
		_fleet.Lost += OnServerLost;
	}

	public int Online { get { lock (_lock) return _clients.Count; } }

	/// <summary>A lista de salas, como vai para o cliente (sem senha).</summary>
	public JsonArray Rooms()
	{
		lock (_lock) return RoomList();
	}

	/// <summary>Atende uma conexão do começo ao fim.</summary>
	public async Task Run(Account account, WebSocket socket)
	{
		var client = new Client { Account = account, Socket = socket };
		try
		{
			JsonObject profile = await _accounts.Profile(account.Id);
			client.Xp = profile["xp"].GetValue<long>();
		}
		catch (ApiException)
		{
			// Conta sem perfil ainda entra; só aparece sem patente.
		}

		Client old;
		List<(Client, string)> out1 = new();
		lock (_lock)
		{
			// Uma conexão por conta: a nova derruba a velha.
			_clients.TryGetValue(account.Id, out old);
			if (old != null) Leave(old, out1);
			_clients[account.Id] = client;
			out1.Add((client, Json(new JsonObject { ["t"] = "hello", ["id"] = account.Id, ["name"] = account.Name, ["online"] = _clients.Count })));
			out1.Add((client, Json(new JsonObject { ["t"] = "rooms", ["rooms"] = RoomList() })));
		}

		await Deliver(out1);
		if (old != null) _ = Close(old, "outra conexão da mesma conta");

		var buffer = new byte[8 * 1024];
		try
		{
			while (socket.State == WebSocketState.Open)
			{
				string text = await Receive(socket, buffer);
				if (text == null) break;

				var outbox = new List<(Client, string)>();
				try
				{
					if (JsonNode.Parse(text) is not JsonObject message) throw new ApiException(400, "mensagem_invalida", "Mensagem inválida.");
					Handle(client, message, outbox);
				}
				catch (ApiException e)
				{
					outbox.Add((client, Error(e.Code, e.Message)));
				}
				catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
				{
					outbox.Add((client, Error("mensagem_invalida", "Mensagem inválida.")));
				}

				await Deliver(outbox);
			}
		}
		catch (WebSocketException)
		{
			// Caiu a conexão: sai como quem fechou.
		}
		finally
		{
			var outbox = new List<(Client, string)>();
			lock (_lock)
			{
				if (_clients.TryGetValue(account.Id, out Client current) && current == client)
				{
					Leave(client, outbox);
					_clients.Remove(account.Id);
				}
			}
			await Deliver(outbox);
		}
	}

	/// <summary>A partida de uma sala terminou: ela volta a esperar e
	/// cada um recebe o que ganhou.</summary>
	public async Task MatchEnded(string matchKey, JsonObject summary, Dictionary<long, JsonObject> gains)
	{
		var outbox = new List<(Client, string)>();
		lock (_lock)
		{
			Room room = _rooms.Values.FirstOrDefault(r => r.MatchKey == matchKey);
			if (room != null)
			{
				room.State = "waiting";
				room.MatchKey = null;
				foreach (Member m in room.Members)
				{
					m.Ready = false;
					var end = new JsonObject { ["t"] = "match.end", ["match"] = summary.DeepClone() };
					if (gains.TryGetValue(m.Client.Account.Id, out JsonObject gain))
					{
						end["you"] = gain.DeepClone();
						m.Client.Xp += gain["xp"].GetValue<long>();
					}
					outbox.Add((m.Client, Json(end)));
				}
				Broadcast(room, outbox);
				BroadcastList(outbox);
			}
		}
		await Deliver(outbox);
	}

	// ------------------------------------------------------------ mensagens

	private void Handle(Client c, JsonObject m, List<(Client, string)> outbox)
	{
		string type = m["t"]?.GetValue<string>() ?? "";
		lock (_lock)
		{
			switch (type)
			{
				case "ping":
					outbox.Add((c, Json(new JsonObject { ["t"] = "pong" })));
					break;

				case "rooms.list":
					outbox.Add((c, Json(new JsonObject { ["t"] = "rooms", ["rooms"] = RoomList() })));
					break;

				case "room.create":
					Create(c, m["settings"] as JsonObject, outbox);
					break;

				case "room.join":
					Join(c, m["id"]?.GetValue<int>() ?? 0, m["password"]?.GetValue<string>() ?? "", outbox);
					break;

				case "room.leave":
					Leave(c, outbox);
					outbox.Add((c, Json(new JsonObject { ["t"] = "rooms", ["rooms"] = RoomList() })));
					break;

				case "room.team":
					Team(c, m["team"]?.GetValue<int>() ?? 0, outbox);
					break;

				case "room.ready":
					Seat(c).Ready = m["ready"]?.GetValue<bool>() ?? false;
					Broadcast(c.Room, outbox);
					break;

				case "room.settings":
					Host(c);
					if (c.Room.State != "waiting") throw new ApiException(409, "em_partida", "A sala está em partida.");
					ApplySettings(c.Room, m["settings"] as JsonObject);
					Broadcast(c.Room, outbox);
					BroadcastList(outbox);
					break;

				case "room.bot.add":
					AddBot(c, m["team"]?.GetValue<int>() ?? 0, outbox);
					break;

				case "room.bot.remove":
					RemoveBot(c, m["id"]?.GetValue<long>() ?? 0, outbox);
					break;

				case "room.start":
					Start(c, outbox);
					break;

				case "chat":
					Chat(c, m["text"]?.GetValue<string>() ?? "", outbox);
					break;

				default:
					throw new ApiException(400, "mensagem_desconhecida", $"Não conheço '{type}'.");
			}
		}
	}

	private void Create(Client c, JsonObject settings, List<(Client, string)> outbox)
	{
		if (_rooms.Count >= MaxRooms) throw new ApiException(503, "cheio", "O servidor está com salas demais.");
		Leave(c, outbox);

		var room = new Room { Id = _nextRoom++, HostId = c.Account.Id, Settings = new JsonObject() };
		ApplySettings(room, settings ?? new JsonObject());
		room.Members.Add(new Member { Client = c, Team = 1 });
		_rooms[room.Id] = room;
		c.Room = room;

		Broadcast(room, outbox);
		BroadcastList(outbox);
	}

	private void Join(Client c, int id, string password, List<(Client, string)> outbox)
	{
		if (!_rooms.TryGetValue(id, out Room room)) throw new ApiException(404, "sala_sumiu", "Essa sala não existe mais.");
		if (c.Room == room) return;
		if (room.State != "waiting") throw new ApiException(409, "em_partida", "A sala está em partida.");
		if (room.Members.Count >= room.Capacity) throw new ApiException(409, "sala_cheia", "A sala está cheia.");
		if (room.Password != "" && !Passwords.SameSecret(room.Password, password))
			throw new ApiException(403, "senha_errada", "Senha da sala incorreta.");

		Leave(c, outbox);

		// Vai para o time com menos gente. Se o time está cheio de bots,
		// um bot dá o lugar: gente tem preferência.
		int blue = room.Members.Count(m => m.Team == 1), orange = room.Members.Count(m => m.Team == 2);
		int team = blue <= orange ? 1 : 2;
		MakeRoom(room, team);
		room.Members.Add(new Member { Client = c, Team = team });
		c.Room = room;

		Broadcast(room, outbox);
		BroadcastList(outbox);
	}

	/// <summary>Tira o cliente da sala em que está. Sala vazia some; se
	/// quem saiu era o dono, o mais antigo assume.</summary>
	private void Leave(Client c, List<(Client, string)> outbox)
	{
		Room room = c.Room;
		if (room == null) return;

		room.Members.RemoveAll(m => m.Client == c);
		c.Room = null;
		outbox.Add((c, Json(new JsonObject { ["t"] = "left", ["id"] = room.Id })));

		if (room.Members.Count == 0) _rooms.Remove(room.Id);   // os bots vão junto
		else
		{
			if (room.HostId == c.Account.Id) room.HostId = room.Members[0].Client.Account.Id;
			Broadcast(room, outbox);
		}

		BroadcastList(outbox);
	}

	private void Team(Client c, int team, List<(Client, string)> outbox)
	{
		Member seat = Seat(c);
		if (team is not (1 or 2)) throw new ApiException(400, "time_invalido", "Time inválido.");
		if (c.Room.State != "waiting") throw new ApiException(409, "em_partida", "A sala está em partida.");
		if (c.Room.Members.Count(m => m.Team == team && m != seat) >= c.Room.TeamSize)
			throw new ApiException(409, "time_cheio", "Esse time está cheio.");

		MakeRoom(c.Room, team, seat);
		seat.Team = team;
		seat.Ready = false;
		Broadcast(c.Room, outbox);
	}

	/// <summary>Abre um lugar no time para uma pessoa, tirando um bot se
	/// o time estiver lotado.</summary>
	private static void MakeRoom(Room room, int team, Member coming = null)
	{
		while (room.Taken(team, coming) >= room.TeamSize)
		{
			BotSeat last = room.Bots.LastOrDefault(b => b.Team == team);
			if (last == null) return;
			room.Bots.Remove(last);
		}
	}

	private void AddBot(Client c, int team, List<(Client, string)> outbox)
	{
		Host(c);
		Room room = c.Room;
		if (room.State != "waiting") throw new ApiException(409, "em_partida", "A sala está em partida.");
		if (team is not (1 or 2)) throw new ApiException(400, "time_invalido", "Time inválido.");
		if (room.Taken(team) >= room.TeamSize) throw new ApiException(409, "time_cheio", "Esse time está cheio.");

		string name = BotNames.FirstOrDefault(n => room.Bots.All(b => b.Name != n)) ?? $"Bot {-room.NextBot}";
		room.Bots.Add(new BotSeat { Id = room.NextBot--, Name = name, Team = team });
		Broadcast(room, outbox);
		BroadcastList(outbox);
	}

	private void RemoveBot(Client c, long id, List<(Client, string)> outbox)
	{
		Host(c);
		Room room = c.Room;
		if (room.State != "waiting") throw new ApiException(409, "em_partida", "A sala está em partida.");
		if (room.Bots.RemoveAll(b => b.Id == id) == 0) return;

		Broadcast(room, outbox);
		BroadcastList(outbox);
	}

	private void Start(Client c, List<(Client, string)> outbox)
	{
		Host(c);
		Room room = c.Room;
		if (room.State != "waiting") throw new ApiException(409, "em_partida", "A partida já começou.");
		if (room.Members.Any(m => !m.Ready && m.Client.Account.Id != room.HostId))
			throw new ApiException(409, "nem_todos_prontos", "Nem todos estão prontos.");

		var assignment = new Assignment
		{
			MatchKey = Guid.NewGuid().ToString("N"),
			RoomId = room.Id,
			Settings = (JsonObject)room.Settings.DeepClone(),
			// Os bots vão na escalação com o dono da sala como dono: é a
			// máquina dele que os move durante a partida.
			Players = room.Members.Select(m => new MatchSeat(m.Client.Account.Id, m.Client.Account.Name, m.Team))
				.Concat(room.Bots.Select(b => new MatchSeat(b.Id, b.Name, b.Team, room.HostId))).ToList()
		};

		(string Address, int Port)? server = _fleet.Allocate(assignment);
		if (server == null) throw new ApiException(503, "sem_servidor", "Não há servidor de partida livre agora. Tente de novo em instantes.");

		room.State = "playing";
		room.MatchKey = assignment.MatchKey;
		long expires = DateTimeOffset.UtcNow.Add(_config.TicketLife).ToUnixTimeSeconds();

		foreach (Member m in room.Members)
			outbox.Add((m.Client, Json(new JsonObject
			{
				["t"] = "match.start",
				["address"] = server.Value.Address,
				["port"] = server.Value.Port,
				["matchKey"] = assignment.MatchKey,
				["accountId"] = m.Client.Account.Id,
				["expires"] = expires,
				["ticket"] = _fleet.Ticket(assignment.MatchKey, m.Client.Account.Id, expires),
				["settings"] = room.Settings.DeepClone()
			})));

		Broadcast(room, outbox);
		BroadcastList(outbox);
	}

	private void Chat(Client c, string text, List<(Client, string)> outbox)
	{
		text = text.Trim();
		if (text.Length == 0) return;
		if (text.Length > MaxChat) text = text[..MaxChat];

		// Cinco mensagens a cada cinco segundos: o bastante para conversar.
		DateTime now = DateTime.UtcNow;
		while (c.Chats.Count > 0 && now - c.Chats.Peek() > TimeSpan.FromSeconds(5)) c.Chats.Dequeue();
		if (c.Chats.Count >= 5) throw new ApiException(429, "devagar", "Devagar com as mensagens.");
		c.Chats.Enqueue(now);

		string line = Json(new JsonObject
		{
			["t"] = "chat",
			["from"] = c.Account.Name,
			["room"] = c.Room?.Id,
			["text"] = text,
			["at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		});

		// Na sala, fala com a sala; fora dela, com quem está na lista.
		IEnumerable<Client> to = c.Room != null ? c.Room.Members.Select(m => m.Client) : _clients.Values.Where(x => x.Room == null);
		foreach (Client other in to) outbox.Add((other, line));
	}

	private void OnServerLost(Assignment assignment)
	{
		var outbox = new List<(Client, string)>();
		lock (_lock)
		{
			if (!_rooms.TryGetValue(assignment.RoomId, out Room room) || room.MatchKey != assignment.MatchKey) return;

			room.State = "waiting";
			room.MatchKey = null;
			foreach (Member m in room.Members)
			{
				m.Ready = false;
				outbox.Add((m.Client, Error("servidor_caiu", "O servidor da partida caiu. A sala voltou a esperar.")));
			}
			Broadcast(room, outbox);
			BroadcastList(outbox);
		}
		_ = Deliver(outbox);
	}

	// ------------------------------------------------------------ regras

	private static Member Seat(Client c)
	{
		if (c.Room == null) throw new ApiException(409, "fora_de_sala", "Você não está numa sala.");
		return c.Room.Members.First(m => m.Client == c);
	}

	private static void Host(Client c)
	{
		Seat(c);
		if (c.Room.HostId != c.Account.Id) throw new ApiException(403, "nao_e_dono", "Só o dono da sala pode fazer isso.");
	}

	/// <summary>Confere e guarda as regras que o dono mandou. O que o
	/// servidor não entende passa adiante como veio (é o servidor de
	/// partida que aplica), desde que caiba.</summary>
	private static void ApplySettings(Room room, JsonObject wanted)
	{
		if (wanted == null) throw new ApiException(400, "regras_invalidas", "Faltam as regras da sala.");
		if (wanted.ToJsonString().Length > 2048) throw new ApiException(413, "grande_demais", "Regras grandes demais.");

		var s = (JsonObject)wanted.DeepClone();
		string title = (s["title"]?.GetValue<string>() ?? "").Trim();
		if (title.Length == 0) title = "Sala";
		if (title.Length > MaxTitle) title = title[..MaxTitle];
		s["title"] = title;

		int teamSize = s["teamSize"]?.GetValue<int>() ?? 8;
		if (teamSize is not (4 or 6 or 8)) throw new ApiException(400, "regras_invalidas", "Times de 4, 6 ou 8.");
		if (room.Members.Count > teamSize * 2) throw new ApiException(409, "regras_invalidas", "Há mais gente na sala do que cabe nesses times.");
		s["teamSize"] = teamSize;

		// Time menor: os bots que não cabem mais saem.
		foreach (int team in new[] { 1, 2 })
			while (room.Bots.Any(b => b.Team == team) && room.Taken(team) > teamSize)
				room.Bots.Remove(room.Bots.Last(b => b.Team == team));

		s["minutes"] = Math.Clamp(s["minutes"]?.GetValue<int>() ?? 10, 3, 30);

		// Dificuldade dos bots: 0 fáceis, 1 médios, 2 difíceis.
		s["difficulty"] = Math.Clamp(s["difficulty"]?.GetValue<int>() ?? 1, 0, 2);
		s["scoreToWin"] = Math.Clamp(s["scoreToWin"]?.GetValue<int>() ?? 40, 10, 200);

		string map = s["map"]?.GetValue<string>() ?? "hangar";
		if (map.Length is 0 or > 24) throw new ApiException(400, "regras_invalidas", "Mapa inválido.");
		s["map"] = map;

		string password = s["password"]?.GetValue<string>() ?? "";
		if (password.Length > MaxPassword) throw new ApiException(400, "regras_invalidas", "Senha longa demais.");
		s.Remove("password");
		s.Remove("number");

		room.Password = password;
		room.Settings = s;
	}

	// ------------------------------------------------------------ saída

	private JsonArray RoomList()
	{
		var list = new JsonArray();
		foreach (Room r in _rooms.Values.OrderByDescending(r => r.Id))
			list.Add(new JsonObject
			{
				["id"] = r.Id,
				["settings"] = r.Settings.DeepClone(),
				["hasPassword"] = r.Password != "",
				["players"] = r.Members.Count,
				["bots"] = r.Bots.Count,
				["capacity"] = r.Capacity,
				["state"] = r.State,
				["host"] = r.Members.FirstOrDefault(m => m.Client.Account.Id == r.HostId)?.Client.Account.Name ?? ""
			});
		return list;
	}

	/// <summary>O estado inteiro da sala, para todos nela.</summary>
	private static void Broadcast(Room room, List<(Client, string)> outbox)
	{
		var members = new JsonArray();
		foreach (Member m in room.Members)
			members.Add(new JsonObject
			{
				["id"] = m.Client.Account.Id,
				["name"] = m.Client.Account.Name,
				["team"] = m.Team,
				["ready"] = m.Ready,
				["host"] = m.Client.Account.Id == room.HostId,
				["xp"] = m.Client.Xp
			});

		// Os bots entram na mesma lista, marcados: sempre prontos, sem XP
		// (bot não tem patente), movidos pela máquina do dono da sala.
		foreach (BotSeat b in room.Bots)
			members.Add(new JsonObject
			{
				["id"] = b.Id,
				["name"] = b.Name,
				["team"] = b.Team,
				["ready"] = true,
				["host"] = false,
				["bot"] = true,
				["owner"] = room.HostId
			});

		string state = Json(new JsonObject
		{
			["t"] = "room",
			["id"] = room.Id,
			["settings"] = room.Settings.DeepClone(),
			["hasPassword"] = room.Password != "",
			["state"] = room.State,
			["members"] = members
		});
		foreach (Member m in room.Members) outbox.Add((m.Client, state));
	}

	/// <summary>A lista de salas, para quem está olhando para ela.</summary>
	private void BroadcastList(List<(Client, string)> outbox)
	{
		string list = Json(new JsonObject { ["t"] = "rooms", ["rooms"] = RoomList() });
		foreach (Client c in _clients.Values.Where(c => c.Room == null)) outbox.Add((c, list));
	}

	private static string Error(string code, string message) =>
		Json(new JsonObject { ["t"] = "error", ["code"] = code, ["message"] = message });

	private static string Json(JsonObject o) => o.ToJsonString();

	private static async Task Deliver(List<(Client Client, string Text)> outbox)
	{
		foreach ((Client client, string text) in outbox)
		{
			if (client.Socket.State != WebSocketState.Open) continue;
			await client.Sending.WaitAsync();
			try
			{
				await client.Socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);
			}
			catch (Exception e) when (e is WebSocketException or ObjectDisposedException or InvalidOperationException)
			{
				// Quem caiu no meio é varrido pelo próprio laço de leitura.
			}
			finally
			{
				client.Sending.Release();
			}
		}
	}

	private static async Task<string> Receive(WebSocket socket, byte[] buffer)
	{
		var text = new StringBuilder();
		while (true)
		{
			WebSocketReceiveResult r = await socket.ReceiveAsync(buffer, CancellationToken.None);
			if (r.MessageType == WebSocketMessageType.Close) return null;
			text.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
			if (text.Length > 16 * 1024) throw new WebSocketException("mensagem grande demais");
			if (r.EndOfMessage) return text.ToString();
		}
	}

	private static async Task Close(Client c, string why)
	{
		try
		{
			// Avisa que fechou e, se o outro lado não responder logo, corta:
			// cliente travado não pode segurar a conexão aberta.
			if (c.Socket.State == WebSocketState.Open)
			{
				using var soon = new CancellationTokenSource(TimeSpan.FromSeconds(2));
				await c.Socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, why, soon.Token);
			}
			await Task.Delay(500);
			c.Socket.Abort();
		}
		catch (Exception e) when (e is WebSocketException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
		{
			c.Socket.Abort();
		}
	}
}
