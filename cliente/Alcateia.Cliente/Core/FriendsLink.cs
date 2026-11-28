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

/// <summary>Uma mensagem direta entre amigos. <c>Pending</c>: mandada daqui e
/// ainda sem o eco do servidor (que é a confirmação).</summary>
public sealed record DirectMessage(long Id, long From, long To, string Text, DateTimeOffset At, string Ref, bool Pending);

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

	// ------------------------------------------------------------------ mensagens diretas
	// (Jhon, 27/11: "coloque chat também no initech launcher [...] e entre amigos".
	// O formato é o do saguão: dm.send, dm.history, dm.read, dm.unread — servidor/LEIA-ME.md.)

	/// <summary>A conta desta ligação (para saber quais mensagens são minhas).</summary>
	public long Me { get; private set; }

	private readonly Dictionary<long, List<DirectMessage>> _talks = new();
	private readonly Dictionary<long, int> _unread = new();
	private readonly HashSet<long> _more = new();

	/// <summary>A conversa com um amigo, da mais antiga para a mais nova (o que já chegou).</summary>
	public IReadOnlyList<DirectMessage> Talk(long friend) =>
		_talks.TryGetValue(friend, out List<DirectMessage> list) ? list : Array.Empty<DirectMessage>();

	/// <summary>Há mensagens mais antigas no servidor do que as que chegaram.</summary>
	public bool HasOlder(long friend) => _more.Contains(friend);

	public int Unread(long friend) => _unread.GetValueOrDefault(friend);
	public int UnreadTotal { get; private set; }

	/// <summary>A conversa aberta na tela: o que chega dela já entra como lido.</summary>
	public long Open { get; set; }

	/// <summary>Uma conversa mudou (chegou, confirmou ou carregou mensagem). O id é o do amigo.</summary>
	public event Action<long> TalkChanged;

	/// <summary>Chegou mensagem de um amigo com a conversa dele fechada.</summary>
	public event Action<Friend, DirectMessage> Received;

	public void SendDm(long to, string text)
	{
		text = (text ?? "").Trim();
		if (text.Length == 0 || Me == 0) return;
		if (text.Length > 500) text = text[..500];
		string reference = Guid.NewGuid().ToString("N")[..12];
		if (!Send(false, "dm.send", ("to", to), ("text", text), ("ref", reference))) return;
		Add(to, new DirectMessage(0, Me, to, text, DateTimeOffset.UtcNow, reference, true));
	}

	/// <summary>Pede as 50 anteriores à mais antiga que já chegou (ou as 50 mais recentes).</summary>
	public void LoadTalk(long with, bool older = false)
	{
		DirectMessage first = older ? Talk(with).FirstOrDefaultConfirmed() : null;
		if (first != null) Send(true, "dm.history", ("with", with), ("before", first.Id));
		else Send(true, "dm.history", ("with", with));
	}

	public void MarkRead(long with)
	{
		if (Unread(with) == 0) return;
		Send(true, "dm.read", ("with", with));
	}

	/// <summary>Algo para avisar: "Fulano quer ser seu amigo".</summary>
	public event Action<string> Notice;

	/// <summary>Um pedido que o servidor recusou, com o porquê.</summary>
	public event Action<string> Failed;

	private ClientWebSocket _socket;
	private CancellationTokenSource _cancel;
	private SynchronizationContext _ui;
	private readonly SemaphoreSlim _sending = new(1, 1);

	public void Start(string url, long me)
	{
		Stop();
		Me = me;
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
		_talks.Clear();
		_unread.Clear();
		_more.Clear();
		UnreadTotal = 0;
		Open = 0;
	}

	public void Add(string handle) => Send("friend.add", ("name", (handle ?? "").Trim()));
	public void Accept(long id) => Send("friend.accept", ("id", id));
	public void Decline(long id) => Send("friend.decline", ("id", id));
	public void Remove(long id) => Send("friend.remove", ("id", id));

	private void Send(string type, params (string Key, JsonNode Value)[] fields) => Send(false, type, fields);

	/// <summary><paramref name="quiet"/>: sem ligação, não avisa (o que a tela pede sozinha, como o histórico).</summary>
	private bool Send(bool quiet, string type, params (string Key, JsonNode Value)[] fields)
	{
		ClientWebSocket socket = _socket;
		if (socket is not { State: WebSocketState.Open })
		{
			if (!quiet) Failed?.Invoke(Tr.T("common.no_link"));
			return false;
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
		return true;
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
				if (Person(m["from"]) is Friend asked) Notice?.Invoke(Tr.F("friends.notice_request", asked.Name));
				break;
			case "friend.accepted":
				if (Person(m["by"]) is Friend accepted) Notice?.Invoke(Tr.F("friends.notice_accepted", accepted.Name));
				break;
			case "dm":
				OnDm(m);
				break;
			case "dm.history":
				OnHistory(m);
				break;
			case "dm.unread":
				_unread.Clear();
				foreach (JsonNode node in m["unread"] as JsonArray ?? new JsonArray())
					if (node?["id"] != null) _unread[node["id"].GetValue<long>()] = node["count"]?.GetValue<int>() ?? 0;
				UnreadTotal = m["total"]?.GetValue<int>() ?? 0;
				Changed?.Invoke();
				break;
			case "error":
				Failed?.Invoke(m["message"]?.GetValue<string>() ?? Tr.T("friends.refused"));
				// A mensagem que o servidor recusou não fica como se fosse sair.
				foreach (long id in _talks.Keys)
					if (_talks[id].RemoveAll(x => x.Pending) > 0) TalkChanged?.Invoke(id);
				break;
		}
	}

	private void OnDm(JsonObject m)
	{
		DirectMessage message = Message(m);
		if (message == null) return;
		long other = message.From == Me ? message.To : message.From;
		Add(other, message);
		if (message.From == Me) return;
		if (Open == other) Send(true, "dm.read", ("with", other));
		else
		{
			_unread[other] = Unread(other) + 1;
			UnreadTotal++;
			Changed?.Invoke();
			Friend friend = null;
			foreach (Friend f in Friends) if (f.Id == other) friend = f;
			if (friend != null) Received?.Invoke(friend, message);
		}
	}

	private void OnHistory(JsonObject m)
	{
		long with = m["with"]?.GetValue<long>() ?? 0;
		if (with == 0) return;
		List<DirectMessage> list = _talks.TryGetValue(with, out List<DirectMessage> known) ? known : _talks[with] = new List<DirectMessage>();
		foreach (JsonNode node in m["messages"] as JsonArray ?? new JsonArray())
			if (Message(node) is DirectMessage message && !list.Exists(x => x.Id == message.Id)) list.Add(message);
		list.Sort((a, b) => a.Pending == b.Pending ? a.Id.CompareTo(b.Id) : a.Pending ? 1 : -1);
		if (m["more"]?.GetValue<bool>() == true) _more.Add(with); else _more.Remove(with);
		TalkChanged?.Invoke(with);
	}

	/// <summary>Põe a mensagem na conversa; o eco de uma que saiu daqui troca a pendente.</summary>
	private void Add(long friend, DirectMessage message)
	{
		if (!_talks.TryGetValue(friend, out List<DirectMessage> list)) _talks[friend] = list = new List<DirectMessage>();
		int pending = message.Ref == null || message.Pending ? -1 : list.FindIndex(x => x.Pending && x.Ref == message.Ref);
		if (pending >= 0) list[pending] = message;
		else if (message.Id == 0 || !list.Exists(x => x.Id == message.Id)) list.Add(message);
		if (list.Count > 300) list.RemoveRange(0, list.Count - 300);
		TalkChanged?.Invoke(friend);
	}

	private static DirectMessage Message(JsonNode n)
	{
		if (n?["id"] == null || n["from"] == null) return null;
		long at = n["at"]?.GetValue<long>() ?? 0;
		return new DirectMessage(n["id"].GetValue<long>(), n["from"].GetValue<long>(), n["to"]?.GetValue<long>() ?? 0,
			n["text"]?.GetValue<string>() ?? "", DateTimeOffset.FromUnixTimeSeconds(at), n["ref"]?.GetValue<string>(), false);
	}

	/// <summary>Só para fotografar as telas: uma conversa de mentira.</summary>
	internal void PreviewTalk(long me, long friend, params (bool Mine, string Text)[] lines)
	{
		Me = me;
		long id = 1;
		DateTimeOffset at = DateTimeOffset.UtcNow.AddMinutes(-lines.Length * 2);
		foreach (var (mine, text) in lines)
		{
			Add(friend, new DirectMessage(id++, mine ? me : friend, mine ? friend : me, text, at, null, false));
			at = at.AddMinutes(2);
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

internal static class TalkExtensions
{
	/// <summary>A mais antiga já confirmada pelo servidor (as pendentes não têm id).</summary>
	public static DirectMessage FirstOrDefaultConfirmed(this IReadOnlyList<DirectMessage> list)
	{
		foreach (DirectMessage m in list) if (!m.Pending) return m;
		return null;
	}
}
