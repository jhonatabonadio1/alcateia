using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Conversas;

namespace Alcateia.Aplicacao.Saguao;

/// <summary>
/// As mensagens diretas entre amigos (Jhon, 27/11: "coloque chat também no
/// [...] launcher e no game [...] e entre amigos"): uma conversa de um para
/// um, que vale do jogo e do cliente de instalação.
///
/// O jogo e o aplicativo mandam <c>dm.send</c>, <c>dm.history</c>,
/// <c>dm.read</c> e <c>dm.unread</c>; o servidor entrega <c>dm</c> (a
/// mensagem, para todas as ligações das duas contas: o eco é a confirmação
/// de quem mandou), <c>dm.history</c> e <c>dm.unread</c> (as não lidas, por
/// amigo: vem ao ligar e depois de cada leitura). O formato está em
/// servidor/LEIA-ME.md.
///
/// **Onde mora cada coisa.** A mensagem é do banco
/// (<see cref="IDirectMessageRepository"/>): quem estava desligado acha a
/// conversa quando entrar, e o aviso de não lida também sai de lá. Quem pode
/// falar com quem (só amigos) é a lista em memória da ligação
/// (<see cref="Session.Links"/>), a mesma que os convites usam. Gravar e ler
/// vão ao banco fora da trava do saguão.
/// </summary>
public sealed partial class LobbyService
{
	/// <summary>Onde as mensagens diretas ficam guardadas. Sem isso (um
	/// saguão montado sem banco), as conversas respondem que estão fora do ar.</summary>
	public IDirectMessageRepository DirectStore { get; set; }

	private static bool IsDirect(string type) => type is "dm.send" or "dm.history" or "dm.read" or "dm.unread";

	/// <summary>Uma mensagem de conversa: confere e gravam fora da trava, a
	/// entrega entra nela.</summary>
	private async Task Direct(Session c, string type, JsonObject m, Outbox outbox)
	{
		IDirectMessageRepository store = DirectStore ?? throw ChatRules.Unavailable();
		long me = c.Account.Id;
		try
		{
			switch (type)
			{
				case "dm.send":
				{
					long to = m["to"]?.GetValue<long>() ?? 0;
					string text = ChatRules.Clean(m["text"]?.GetValue<string>(), ChatRules.DirectMax);
					string reference = m["ref"]?.GetValue<string>();
					if (reference != null && reference.Length > ChatRules.RefMax) reference = reference[..ChatRules.RefMax];
					lock (_lock)
					{
						RequireFriend(c, to);
						if (text.Length == 0) throw ChatRules.Empty();
						Throttle(c.DirectMinute, ChatRules.DirectPerMinute, TimeSpan.FromMinutes(1), "Devagar com as mensagens. Espere um minuto.");
						Throttle(c.DirectBurst, ChatRules.DirectBurst, ChatRules.DirectBurstWindow, "Devagar com as mensagens.");
					}

					DirectMessage saved = await store.Save(me, to, text);
					lock (_lock)
					{
						outbox.To(Live(to), DirectNode(saved));
						JsonObject echo = DirectNode(saved);
						if (!string.IsNullOrEmpty(reference)) echo["ref"] = reference;
						outbox.To(Live(me), echo);
					}
					break;
				}

				case "dm.history":
				{
					long with = m["with"]?.GetValue<long>() ?? 0;
					long before = Math.Max(0, m["before"]?.GetValue<long>() ?? 0);
					lock (_lock) RequireFriend(c, with);

					// Uma a mais que a página diz se ainda há mais antigas.
					List<DirectMessage> page = (await store.History(me, with, before, ChatRules.HistoryPage + 1)).ToList();
					bool more = page.Count > ChatRules.HistoryPage;
					if (more) page.RemoveAt(0);

					var messages = new JsonArray();
					foreach (DirectMessage d in page)
					{
						JsonObject node = Fields(d);
						node["read"] = d.Read;
						messages.Add(node);
					}
					outbox.To(c, new JsonObject { ["t"] = "dm.history", ["with"] = with, ["messages"] = messages, ["more"] = more });
					break;
				}

				case "dm.read":
				{
					long with = m["with"]?.GetValue<long>() ?? 0;
					await store.MarkRead(me, with);
					JsonObject unread = await UnreadMessage(me);
					// O jogo e o aplicativo da mesma conta ficam com a mesma contagem.
					lock (_lock) outbox.To(Live(me), unread);
					break;
				}

				default:   // dm.unread
					outbox.To(c, await UnreadMessage(me));
					break;
			}
		}
		catch (Exception e) when (e is not Failure and not InvalidOperationException and not FormatException)
		{
			// O banco não respondeu: quem pediu fica sabendo, e a ligação segue.
			throw ChatRules.Unavailable();
		}
	}

	/// <summary>Só se conversa com amigo (o pedido aceito).</summary>
	private static void RequireFriend(Session c, long other)
	{
		if (!c.Links.TryGetValue(other, out FriendLink link) || link.Kind != FriendKind.Friend) throw FriendRules.NotFriends();
	}

	private static JsonObject Fields(DirectMessage d) => new()
	{
		["id"] = d.Id,
		["from"] = d.From,
		["to"] = d.To,
		["text"] = d.Text,
		["at"] = new DateTimeOffset(DateTime.SpecifyKind(d.At, DateTimeKind.Utc)).ToUnixTimeSeconds()
	};

	private static JsonObject DirectNode(DirectMessage d)
	{
		JsonObject node = Fields(d);
		node["t"] = "dm";
		return node;
	}

	/// <summary>As não lidas da conta, por amigo, como vão para o cliente.</summary>
	private async Task<JsonObject> UnreadMessage(long accountId)
	{
		var unread = new JsonArray();
		int total = 0;
		foreach ((long from, int count) in await DirectStore.Unread(accountId))
		{
			unread.Add(new JsonObject { ["id"] = from, ["count"] = count });
			total += count;
		}
		return new JsonObject { ["t"] = "dm.unread", ["unread"] = unread, ["total"] = total };
	}

	/// <summary>Ao ligar, o jogo e o aplicativo recebem as não lidas (o que
	/// chegou com a conta desligada). Banco fora: não vem, e a ligação segue.</summary>
	private async Task PushUnread(Session s)
	{
		if (DirectStore == null) return;
		try
		{
			JsonObject unread = await UnreadMessage(s.Account.Id);
			if (s.Connection.IsOpen) await s.Connection.Send(unread.ToJsonString());
		}
		catch (Exception e) when (e is not OutOfMemoryException)
		{
		}
	}
}
