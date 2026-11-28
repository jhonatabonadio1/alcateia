using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Alcateia.Cliente.Core;

/// <summary>Onde um amigo está. <c>App</c>: com o jogo fechado e o cliente aberto.</summary>
public enum Presence { Offline, App, Online, Room, Playing }

public sealed record Friend(long Id, string Name, string Tag, long Xp, Presence Status, int Room)
{
	public string Handle => Tag.Length == 0 ? Name : $"{Name}#{Tag}";
	public bool InGame => Status is Presence.Online or Presence.Room or Presence.Playing;
}

/// <summary>
/// Os amigos ao vivo: a ligação do cliente com o saguão do servidor
/// (<c>/v1/lobby?client=app</c>). Por ela chegam a lista de amigos, os
/// pedidos e onde cada um está, e saem pedir, aceitar, recusar e remover.
///
/// É uma ligação à parte da do jogo: abrir o jogo não a derruba, e enquanto
/// o jogo está fechado os amigos veem a conta "no aplicativo". Não entra em
/// sala nem em grupo — isso é de dentro do jogo. O formato das mensagens
/// está em servidor/LEIA-ME.md ("Amigos").
///
/// Caiu, tenta de novo sozinha, com espera crescente. Os eventos saem no fio
/// da interface (o contexto de quem chamou <see cref="Start"/>).
/// </summary>
public sealed class FriendsLink
{
	public IReadOnlyList<Friend> Friends { get; private set; } = Array.Empty<Friend>();
	public IReadOnlyList<Friend> Requests { get; private set; } = Array.Empty<Friend>();
	public IReadOnlyList<Friend> Sent { get; private set; } = Array.Empty<Friend>();

	public bool Connected { get; private set; }

	/// <summary>A lista já chegou nesta ligação.</summary>
	public bool Loaded { get; private set; }

	/// <summary>A mesma conta abriu o cliente em outro lugar: esta ligação
	/// para de tentar (as duas ficariam se derrubando).</summary>
	public bool Replaced { get; private set; }

	public event Action Changed;

	/// <summary>Algo para avisar: "Fulano quer ser seu amigo".</summary>
	public event Action<string> Notice;

	/// <summary>Um pedido que o servidor recusou, com o porquê.</summary>
	public event Action<string> Failed;

	private ClientWebSocket _socket;
	private CancellationTokenSource _cancel;
	private SynchronizationContext _ui;
	private readonly SemaphoreSlim _sending = new(1, 1);

	public void Start(string url)
	{
		Stop();
		_ui = SynchronizationContext.Current;
		_cancel = new CancellationTokenSource();
		Replaced = false;
		_ = Task.Run(() => Run(url, _cancel.Token));
	}

	public void Stop()
	{
		_cancel?.Cancel();
		_cancel = null;
		_socket?.Abort();
		_socket = null;
		Connected = Loaded = false;
		Friends = Requests = Sent = Array.Empty<Friend>();
	}

	public void Add(string handle) => Send("friend.add", ("name", (handle ?? "").Trim()));
	public void Accept(long id) => Send("friend.accept", ("id", id));
	public void Decline(long id) => Send("friend.decline", ("id", id));
	public void Remove(long id) => Send("friend.remove", ("id", id));

	private void Send(string type, params (string Key, JsonNode Value)[] fields)
	{
		ClientWebSocket socket = _socket;
		if (socket is not { State: WebSocketState.Open })
		{
			Failed?.Invoke("Sem ligação com o servidor. Tente de novo em instantes.");
			return;
		}
		var message = new JsonObject { ["t"] = type };
		foreach (var (key, value) in fields) message[key] = value;
		byte[] bytes = Encoding.UTF8.GetBytes(message.ToJsonString());

		_ = Task.Run(async () =>
		{
			await _sending.WaitAsync();
			try
			{
				await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
			}
			catch (Exception e) when (e is WebSocketException or ObjectDisposedException or InvalidOperationException)
			{
				// A leitura percebe a queda e reconecta.
			}
			finally
			{
				_sending.Release();
			}
		});
	}

	private async Task Run(string url, CancellationToken cancel)
	{
		int wait = 2;
		while (!cancel.IsCancellationRequested)
		{
			var socket = new ClientWebSocket();
			string closed = null;
			try
			{
				using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
				{
					timeout.CancelAfter(TimeSpan.FromSeconds(10));
					await socket.ConnectAsync(new Uri(url), timeout.Token);
				}
				_socket = socket;
				wait = 2;
				Post(() => { Connected = true; Changed?.Invoke(); });
				closed = await Read(socket, cancel);
			}
			catch (Exception e) when (e is WebSocketException or OperationCanceledException or UriFormatException or InvalidOperationException)
			{
			}
			finally
			{
				socket.Dispose();
			}
			if (cancel.IsCancellationRequested) return;

			bool replaced = closed != null && closed.Contains("outro aplicativo");
			Post(() =>
			{
				Connected = Loaded = false;
				Replaced = replaced;
				Changed?.Invoke();
			});
			if (replaced) return;

			try { await Task.Delay(TimeSpan.FromSeconds(wait), cancel); }
			catch (OperationCanceledException) { return; }
			wait = Math.Min(wait * 2, 30);
		}
	}

	/// <summary>Lê até a ligação fechar. Devolve o motivo que o servidor deu, se deu.</summary>
	private async Task<string> Read(ClientWebSocket socket, CancellationToken cancel)
	{
		var buffer = new byte[16 * 1024];
		var text = new StringBuilder();
		while (socket.State == WebSocketState.Open && !cancel.IsCancellationRequested)
		{
			WebSocketReceiveResult r = await socket.ReceiveAsync(buffer, cancel);
			if (r.MessageType == WebSocketMessageType.Close) return r.CloseStatusDescription ?? "";
			text.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
			if (!r.EndOfMessage) continue;
			try
			{
				if (JsonNode.Parse(text.ToString()) is JsonObject message) Post(() => Apply(message));
			}
			catch (JsonException)
			{
			}
			text.Clear();
		}
		return null;
	}

	private void Post(Action action)
	{
		if (_ui != null) _ui.Post(_ => action(), null);
		else action();
	}

	/// <summary>Só para fotografar as telas (cliente/Alcateia.Cliente.Fotos): uma lista de mentira, sem servidor.</summary>
	internal void Preview(JsonObject friends)
	{
		Connected = true;
		Apply(friends);
	}

	private void Apply(JsonObject m)
	{
		switch (m["t"]?.GetValue<string>())
		{
			case "friends":
				Friends = People(m["friends"]);
				Requests = People(m["requests"]);
				Sent = People(m["sent"]);
				Loaded = true;
				Changed?.Invoke();
				break;
			case "friend.request":
				if (Person(m["from"]) is Friend asked) Notice?.Invoke($"{asked.Name} quer ser seu amigo");
				break;
			case "friend.accepted":
				if (Person(m["by"]) is Friend accepted) Notice?.Invoke($"{accepted.Name} aceitou o seu pedido de amizade");
				break;
			case "error":
				Failed?.Invoke(m["message"]?.GetValue<string>() ?? "O servidor recusou o pedido.");
				break;
		}
	}

	private static List<Friend> People(JsonNode list)
	{
		var people = new List<Friend>();
		foreach (JsonNode node in list as JsonArray ?? new JsonArray())
			if (Person(node) is Friend friend) people.Add(friend);
		return people;
	}

	private static Friend Person(JsonNode node)
	{
		if (node?["id"] == null) return null;
		Presence status = node["status"]?.GetValue<string>() switch
		{
			"app" => Presence.App,
			"online" => Presence.Online,
			"room" => Presence.Room,
			"playing" => Presence.Playing,
			_ => Presence.Offline
		};
		return new Friend(node["id"].GetValue<long>(), node["name"]?.GetValue<string>() ?? "", node["tag"]?.GetValue<string>() ?? "",
			node["xp"]?.GetValue<long>() ?? 0, status, node["room"]?.GetValue<int?>() ?? 0);
	}
}
