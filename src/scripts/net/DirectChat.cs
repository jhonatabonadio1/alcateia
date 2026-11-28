using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// As conversas com os amigos (Jhon, 27/11: "coloque chat também no [...]
/// launcher e no game [...] e entre amigos"): uma de um para um com cada
/// amigo, que vale no jogo e no cliente de instalação — o servidor guarda as
/// últimas de cada conversa e entrega o que chegou com o jogo fechado.
///
/// Como o <see cref="Social"/>, aqui fica só o espelho do que veio pelo
/// saguão (<see cref="LobbyLink"/>: <c>dm</c>, <c>dm.history</c>,
/// <c>dm.unread</c>) e os pedidos que as telas fazem. O contrato das
/// mensagens está em servidor/LEIA-ME.md ("Conversas com os amigos").
///
/// **Não lidas.** O servidor conta ao ligar e depois de cada leitura; no
/// meio, cada mensagem que chega de um amigo soma uma aqui — menos a da
/// conversa que está aberta na tela (<see cref="Open"/>), que já vai lida.
/// </summary>
public static class DirectChat
{
	/// <summary>Uma mensagem: quem mandou, para quem, o texto e quando (segundos Unix).</summary>
	public sealed record Line(long Id, long From, long To, string Text, long At)
	{
		public bool Mine => From == Backend.AccountId;
	}

	/// <summary>O que o servidor aceita numa mensagem (o resto ele corta).</summary>
	public const int MaxLength = 500;

	/// <summary>Quantas mensagens cada conversa guarda aqui.</summary>
	private const int Kept = 200;

	private static readonly Dictionary<long, List<Line>> _talks = new();
	private static readonly HashSet<long> _loaded = new();
	private static readonly Dictionary<long, bool> _more = new();
	private static readonly Dictionary<long, int> _unread = new();

	/// <summary>A conversa que está aberta na tela (0: nenhuma).</summary>
	public static long Open { get; private set; }

	/// <summary>O servidor desta conexão é de antes das conversas.</summary>
	public static bool Unsupported { get; private set; }

	/// <summary>Uma conversa mudou (chegou mensagem, veio o histórico): o número é o do amigo.</summary>
	public static event Action<long> Changed;

	/// <summary>Chegou uma mensagem de um amigo (não as minhas).</summary>
	public static event Action<Line> Received;

	/// <summary>As contagens de não lidas mudaram.</summary>
	public static event Action UnreadChanged;

	public static int Unread(long friend) => _unread.GetValueOrDefault(friend);
	public static int TotalUnread => _unread.Values.Sum();

	/// <summary>As mensagens com um amigo que já estão aqui, da mais antiga para a mais nova.</summary>
	public static IReadOnlyList<Line> With(long friend) =>
		_talks.TryGetValue(friend, out List<Line> lines) ? lines : Array.Empty<Line>();

	/// <summary>O histórico com o amigo já veio do servidor.</summary>
	public static bool Loaded(long friend) => _loaded.Contains(friend);

	/// <summary>Há mensagens mais antigas no servidor.</summary>
	public static bool More(long friend) => _more.GetValueOrDefault(friend);

	// ------------------------------------------------------------------ pedidos ao servidor

	private static LobbyLink Link => LobbyLink.Instance;

	public static void Send(long friend, string text)
	{
		text = (text ?? "").Trim();
		if (text.Length == 0 || friend == 0) return;
		if (text.Length > MaxLength) text = text[..MaxLength];
		Link.SendDirect("dm.send", ("to", friend), ("text", text));
	}

	/// <summary>A tela abriu a conversa: o histórico vem (se ainda não veio) e
	/// as não lidas dela viram lidas.</summary>
	public static void OpenWith(long friend)
	{
		Open = friend;
		if (!_loaded.Contains(friend)) Link.SendDirect("dm.history", ("with", friend));
		if (Unread(friend) > 0) MarkRead(friend);
	}

	public static void Close() => Open = 0;

	/// <summary>As mais antigas que as que já estão aqui.</summary>
	public static void LoadOlder(long friend)
	{
		long first = With(friend).FirstOrDefault()?.Id ?? 0;
		if (first > 0) Link.SendDirect("dm.history", ("with", friend), ("before", first));
	}

	private static void MarkRead(long friend)
	{
		// Já zera aqui; o servidor confirma com a contagem nova.
		if (_unread.Remove(friend)) UnreadChanged?.Invoke();
		Link.SendDirect("dm.read", ("with", friend));
	}

	// ------------------------------------------------------------------ o que chega

	/// <summary>Uma mensagem do saguão. Verdadeiro se era das conversas.</summary>
	public static bool Apply(JsonObject m)
	{
		switch (m["t"]?.GetValue<string>())
		{
			case "dm":
			{
				Line line = LineOf(m);
				if (line == null) return true;
				long friend = line.Mine ? line.To : line.From;
				List<Line> talk = Talk(friend);
				if (talk.All(l => l.Id != line.Id))
				{
					talk.Add(line);
					if (talk.Count > Kept) talk.RemoveAt(0);
				}
				if (!line.Mine)
				{
					if (Open == friend) Link.SendDirect("dm.read", ("with", friend));
					else
					{
						_unread[friend] = Unread(friend) + 1;
						UnreadChanged?.Invoke();
					}
					Received?.Invoke(line);
				}
				Changed?.Invoke(friend);
				return true;
			}

			case "dm.history":
			{
				long friend = m["with"]?.GetValue<long>() ?? 0;
				List<Line> talk = Talk(friend);
				var known = talk.Select(l => l.Id).ToHashSet();
				var older = new List<Line>();
				foreach (JsonNode node in m["messages"] as JsonArray ?? new JsonArray())
					if (LineOf(node) is Line line && !known.Contains(line.Id)) older.Add(line);
				talk.AddRange(older);
				talk.Sort((a, b) => a.Id.CompareTo(b.Id));
				// Primeira página: o "há mais" vale; depois, só se pediu mais antigas e veio cheia.
				_more[friend] = m["more"]?.GetValue<bool>() ?? false;
				_loaded.Add(friend);
				Changed?.Invoke(friend);
				return true;
			}

			case "dm.unread":
				_unread.Clear();
				foreach (JsonNode node in m["unread"] as JsonArray ?? new JsonArray())
				{
					long id = node?["id"]?.GetValue<long>() ?? 0;
					int count = node?["count"]?.GetValue<int>() ?? 0;
					// A conversa aberta já está sendo lida.
					if (id != 0 && count > 0 && id != Open) _unread[id] = count;
				}
				UnreadChanged?.Invoke();
				return true;
		}
		return false;
	}

	/// <summary>O servidor não conhece as conversas (versão antiga).</summary>
	public static void MarkUnsupported()
	{
		if (Unsupported) return;
		Unsupported = true;
		UnreadChanged?.Invoke();
	}

	/// <summary>Caiu a ligação, saiu da conta: tudo vem de novo na próxima.</summary>
	public static void Reset()
	{
		_talks.Clear();
		_loaded.Clear();
		_more.Clear();
		_unread.Clear();
		Unsupported = false;
		UnreadChanged?.Invoke();
	}

	private static List<Line> Talk(long friend)
	{
		if (!_talks.TryGetValue(friend, out List<Line> talk)) _talks[friend] = talk = new List<Line>();
		return talk;
	}

	private static Line LineOf(JsonNode node)
	{
		if (node?["id"] == null) return null;
		return new Line(node["id"].GetValue<long>(), node["from"]?.GetValue<long>() ?? 0, node["to"]?.GetValue<long>() ?? 0,
			node["text"]?.GetValue<string>() ?? "", node["at"]?.GetValue<long>() ?? 0);
	}
}
