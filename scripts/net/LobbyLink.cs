using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A ligação ao vivo com o saguão do servidor: salas, times, chat e o
/// aviso de que a partida começou.
///
/// Um nó só, pendurado na raiz — sobrevive à troca de cena, então a sala
/// continua aberta enquanto se joga a partida e está lá na volta. A
/// conexão lê num fio à parte e entrega as mensagens no quadro seguinte,
/// pelo <see cref="_Process"/>: quem escuta os eventos está sempre no fio
/// do jogo.
///
/// O formato das mensagens está em servidor/LEIA-ME.md.
/// </summary>
public partial class LobbyLink : Node
{
	private static LobbyLink _instance;

	/// <summary>A ligação, criada na primeira vez que alguém pede.</summary>
	public static LobbyLink Instance
	{
		get
		{
			if (_instance != null && IsInstanceValid(_instance)) return _instance;

			_instance = new LobbyLink { Name = "LobbyLink", ProcessMode = ProcessModeEnum.Always };
			// Adiado: quem pede pode estar no meio da montagem da cena.
			((SceneTree)Engine.GetMainLoop()).Root.CallDeferred(Node.MethodName.AddChild, _instance);
			return _instance;
		}
	}

	public bool Connected => _socket is { State: WebSocketState.Open };

	/// <summary>Há uma sala aberta nesta sessão (sem criar a ligação só para perguntar).</summary>
	public static bool InRoom => _instance != null && IsInstanceValid(_instance) && _instance.Room != null;

	/// <summary>As últimas mensagens do chat, para a tela que abrir depois.</summary>
	public List<(string From, string Text)> Chat { get; } = new();
	private const int ChatKept = 80;

	/// <summary>A última lista de salas e a sala em que se está (ou null).</summary>
	public JsonArray Rooms { get; private set; } = new();
	public JsonObject Room { get; private set; }

	/// <summary>Algo mudou (lista, sala, conexão): a tela se redesenha.</summary>
	public event Action Changed;
	public event Action<string, string> ChatReceived;
	public event Action<string, string> Failed;
	public event Action<JsonObject> MatchStarting;
	public event Action<JsonObject> MatchEnded;

	private ClientWebSocket _socket;
	private CancellationTokenSource _cancel;
	private readonly ConcurrentQueue<JsonObject> _inbox = new();
	private readonly SemaphoreSlim _sending = new(1, 1);

	/// <summary>Abre a ligação (se ainda não está aberta). Falso se não deu.</summary>
	public async Task<bool> Open()
	{
		if (Connected) return true;
		if (!Backend.LoggedIn) return false;

		Close();
		_socket = new ClientWebSocket();
		_cancel = new CancellationTokenSource();
		try
		{
			using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
			await _socket.ConnectAsync(new Uri(Backend.LobbyUrl), timeout.Token);
		}
		catch (Exception e) when (e is WebSocketException or OperationCanceledException or UriFormatException)
		{
			_socket = null;
			return false;
		}

		_ = Task.Run(() => ReadLoop(_socket, _cancel.Token));
		return true;
	}

	public void Close()
	{
		_cancel?.Cancel();
		_socket?.Abort();
		_socket = null;
		Room = null;
		Rooms = new JsonArray();
	}

	public void CreateRoom(JsonObject settings) => Send("room.create", ("settings", settings));
	public void JoinRoom(int id, string password = "") => Send("room.join", ("id", id), ("password", password));
	public void LeaveRoom() => Send("room.leave");
	public void SetTeam(int team) => Send("room.team", ("team", team));
	public void SetReady(bool ready) => Send("room.ready", ("ready", ready));
	public void StartMatch() => Send("room.start");

	/// <summary>Muda as regras da sala (só o dono, com a sala esperando).</summary>
	public void ChangeSettings(JsonObject settings) => Send("room.settings", ("settings", settings));

	/// <summary>Bots na sala (só o dono): entram num time, saem pelo número.</summary>
	public void AddBot(int team) => Send("room.bot.add", ("team", team));
	public void RemoveBot(long id) => Send("room.bot.remove", ("id", id));
	public void Say(string text) => Send("chat", ("text", text));
	public void RefreshRooms() => Send("rooms.list");

	private void Send(string type, params (string Key, JsonNode Value)[] fields)
	{
		if (!Connected) return;

		var message = new JsonObject { ["t"] = type };
		foreach (var (key, value) in fields) message[key] = value;
		byte[] bytes = Encoding.UTF8.GetBytes(message.ToJsonString());
		ClientWebSocket socket = _socket;

		_ = Task.Run(async () =>
		{
			await _sending.WaitAsync();
			try
			{
				await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
			}
			catch (Exception e) when (e is WebSocketException or ObjectDisposedException or InvalidOperationException)
			{
				// A leitura percebe a queda e avisa.
			}
			finally
			{
				_sending.Release();
			}
		});
	}

	private async Task ReadLoop(ClientWebSocket socket, CancellationToken cancel)
	{
		var buffer = new byte[16 * 1024];
		var text = new StringBuilder();
		try
		{
			while (socket.State == WebSocketState.Open && !cancel.IsCancellationRequested)
			{
				WebSocketReceiveResult r = await socket.ReceiveAsync(buffer, cancel);
				if (r.MessageType == WebSocketMessageType.Close) break;

				text.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
				if (!r.EndOfMessage) continue;

				if (JsonNode.Parse(text.ToString()) is JsonObject message) _inbox.Enqueue(message);
				text.Clear();
			}
		}
		catch (Exception e) when (e is WebSocketException or OperationCanceledException or JsonException or ObjectDisposedException)
		{
			// Caiu: o aviso abaixo cobre.
		}

		if (!cancel.IsCancellationRequested)
			_inbox.Enqueue(new JsonObject { ["t"] = "closed" });
	}

	public override void _Process(double delta)
	{
		while (_inbox.TryDequeue(out JsonObject m))
		{
			switch (m["t"]?.GetValue<string>())
			{
				case "rooms":
					Rooms = (JsonArray)m["rooms"].DeepClone();
					Changed?.Invoke();
					break;
				case "room":
					Room = m;
					Changed?.Invoke();
					break;
				case "left":
					Room = null;
					Changed?.Invoke();
					break;
				case "chat":
					(string from, string text) = (m["from"]?.GetValue<string>() ?? "", m["text"]?.GetValue<string>() ?? "");
					Chat.Add((from, text));
					if (Chat.Count > ChatKept) Chat.RemoveAt(0);
					ChatReceived?.Invoke(from, text);
					break;
				case "error":
					Failed?.Invoke(m["code"]?.GetValue<string>() ?? "", m["message"]?.GetValue<string>() ?? "");
					break;
				case "match.start":
					// Onde quer que o jogador esteja no menu, a partida
					// começou: vai todo mundo junto.
					MatchStarting?.Invoke(m);
					OnlineMatch.Launch(GetTree(), m, Room);
					break;
				case "match.end":
					MatchEnded?.Invoke(m);
					break;
				case "closed":
					Close();
					Failed?.Invoke("desconectado", "A ligação com o servidor caiu.");
					Changed?.Invoke();
					break;
			}
		}
	}

	public override void _ExitTree()
	{
		Close();
		if (_instance == this) _instance = null;
	}
}
