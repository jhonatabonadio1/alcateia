using System.Collections.ObjectModel;
using System.Linq;
using Alcateia.Cliente.Core;
using Avalonia.Media;

namespace Alcateia.Cliente.ViewModels;

/// <summary>Um amigo (ou pedido) numa linha da lista.</summary>
public sealed class FriendItem
{
	public Friend Friend { get; init; }
	public string Name => Friend.Name;
	public string Tag => Friend.Tag.Length == 0 ? "" : "#" + Friend.Tag;
	public string Initial => Friend.Name.Length > 0 ? Friend.Name[..1].ToUpperInvariant() : "?";

	/// <summary>A linha de baixo: onde está, ou o que o pedido espera.</summary>
	public string Where { get; init; }

	/// <summary>A cor do ponto de presença.</summary>
	public IBrush Dot => Brush.Parse(Friend.Status switch
	{
		Presence.Online => "#4ADE80",
		Presence.Room => "#F3B950",
		Presence.Playing => "#5AA9FF",
		Presence.App => "#9DB0C4",
		_ => "#465568"
	});

	public bool Dim => Friend.Status == Presence.Offline;
	public bool IsRequest { get; init; }
	public bool IsSent { get; init; }
	public bool IsFriend => !IsRequest && !IsSent;

	/// <summary>Mensagens dele ainda não lidas (o número no retrato).</summary>
	public int Unread { get; init; }
	public bool HasUnread => Unread > 0;
	public string UnreadText => Unread > 9 ? "9+" : Unread.ToString();
	public Command Chat { get; init; }
	public Command Accept { get; init; }
	public Command Decline { get; init; }
	public Command Remove { get; init; }
}

/// <summary>
/// O painel de amigos, na direita: você com a sua tag (clicar copia), o
/// campo de adicionar pelo nome com a tag, os pedidos que chegaram, os
/// amigos por onde estão (no jogo, no aplicativo, desconectados) e os
/// pedidos que você mandou.
///
/// Convidar para sala e para grupo é de dentro do jogo; aqui se vê quem está
/// e se cuida da lista. Quem sabe dos amigos é o servidor (<see cref="FriendsLink"/>).
/// </summary>
public sealed class FriendsViewModel : Observable
{
	private readonly MainViewModel _main;
	private readonly FriendsLink _link;
	private string _addText = "";

	public FriendsViewModel(MainViewModel main, FriendsLink link)
	{
		_main = main;
		_link = link;
		_link.Changed += Rebuild;
		_link.Notice += text => _main.Toast(text);
		_link.Failed += text => _main.Toast(text, error: true);
		Add = new Command(Ask);
		_link.TalkChanged += id => { if (Chat?.Friend.Id == id) Chat.Reload(); };
		_link.Received += (friend, message) =>
		{
			if (Chat?.Friend.Id != friend.Id) _main.Toast(Tr.F("chat.toast", friend.Name, message.Text));
		};
	}

	// ------------------------------------------------------------------ a conversa aberta

	private ChatViewModel _chat;
	public ChatViewModel Chat { get => _chat; private set { Set(ref _chat, value); Raise(nameof(HasChat)); } }
	public bool HasChat => _chat != null;

	public void OpenChat(Friend friend)
	{
		Chat = new ChatViewModel(_link, friend, CloseChat);
		Chat.Update(friend, Where(friend));
		_link.Open = friend.Id;
		_link.LoadTalk(friend.Id);
		_link.MarkRead(friend.Id);
	}

	public void CloseChat()
	{
		_link.Open = 0;
		Chat = null;
	}

	public string AddText { get => _addText; set => Set(ref _addText, value); }
	public Command Add { get; }

	public ObservableCollection<FriendItem> Requests { get; } = new();
	public ObservableCollection<FriendItem> InGame { get; } = new();
	public ObservableCollection<FriendItem> InApp { get; } = new();
	public ObservableCollection<FriendItem> Offline { get; } = new();
	public ObservableCollection<FriendItem> Sent { get; } = new();

	public bool HasRequests => Requests.Count > 0;
	public bool HasInGame => InGame.Count > 0;
	public bool HasInApp => InApp.Count > 0;
	public bool HasOffline => Offline.Count > 0;
	public bool HasSent => Sent.Count > 0;
	public string RequestsTitle => Tr.F("friends.requests", Requests.Count);
	public string InGameTitle => Tr.F("friends.in_game", InGame.Count);
	public string InAppTitle => Tr.F("friends.in_app", InApp.Count);
	public string OfflineTitle => Tr.F("friends.offline", Offline.Count);
	public string SentTitle => Tr.F("friends.sent", Sent.Count);
	public string OnlineCount => (InGame.Count + InApp.Count).ToString();

	/// <summary>O recado no lugar da lista: ligando, caiu, ou ainda sem amigos.</summary>
	public string Note
	{
		get
		{
			if (_link.Replaced) return Tr.T("friends.replaced");
			if (!_link.Connected) return Tr.T("friends.connecting");
			if (!_link.Loaded) return Tr.T("friends.loading");
			if (_link.Friends.Count + _link.Requests.Count + _link.Sent.Count == 0)
				return Tr.T("friends.none");
			return "";
		}
	}

	public bool HasNote => Note.Length > 0;

	private void Ask()
	{
		string handle = (AddText ?? "").Trim();
		if (handle.Length == 0) return;
		if (!handle.Contains('#') || handle.EndsWith('#'))
		{
			_main.Toast(Tr.T("friends.bad_handle"), error: true);
			return;
		}
		if (!_link.Connected)
		{
			_main.Toast(Tr.T("common.no_link"), error: true);
			return;
		}
		// A resposta vem pela lista (o pedido aparece em "enviados") ou num aviso de erro.
		_link.Add(handle);
		AddText = "";
	}

	internal void Rebuild()
	{
		Requests.Clear();
		InGame.Clear();
		InApp.Clear();
		Offline.Clear();
		Sent.Clear();

		foreach (Friend f in _link.Requests)
			Requests.Add(new FriendItem
			{
				Friend = f, IsRequest = true, Where = Tr.T("friends.wants"),
				Accept = new Command(() => _link.Accept(f.Id)), Decline = new Command(() => _link.Decline(f.Id))
			});

		foreach (Friend f in _link.Friends)
		{
			var item = new FriendItem
			{
				Friend = f, Where = Where(f), Unread = _link.Unread(f.Id), Chat = new Command(() => OpenChat(f)),
				Remove = new Command(() => _main.Ask(Tr.T("friends.remove_title"), Tr.F("friends.remove_body", f.Name),
					Tr.T("friends.remove"), () => _link.Remove(f.Id), danger: true))
			};
			(f.InGame ? InGame : f.Status == Presence.App ? InApp : Offline).Add(item);
		}

		foreach (Friend f in _link.Sent)
			Sent.Add(new FriendItem { Friend = f, IsSent = true, Where = Tr.T("friends.waiting"), Decline = new Command(() => _link.Decline(f.Id)) });

		// A conversa aberta segue o amigo (o nome e onde ele está podem ter mudado); some se ele saiu da lista.
		if (_chat != null)
		{
			Friend now = null;
			foreach (Friend f in _link.Friends) if (f.Id == _chat.Friend.Id) now = f;
			if (now == null) CloseChat(); else _chat.Update(now, Where(now));
		}
		RaiseAll();
	}

	private static string Where(Friend f) => f.Status switch
	{
		// O launcher é de vários jogos: a linha diz em qual (Jhon, 27/11: "WolfLeague · Em partida").
		// Por enquanto a presença que o servidor manda é toda do WolfLeague.
		Presence.App => AppInfo.Name,
		Presence.Online => Games.WolfLeague.Name + " · " + Tr.T("friends.at_menu"),
		Presence.Room => Games.WolfLeague.Name + " · " + Tr.F("friends.at_room", f.Room),
		Presence.Playing => Games.WolfLeague.Name + " · " + Tr.T("friends.at_match"),
		_ => Tr.T("friends.at_offline")
	};
}

/// <summary>Uma linha da conversa.</summary>
public sealed class ChatLine
{
	public string Text { get; init; }
	public bool Mine { get; init; }
	public bool Theirs => !Mine;
	public bool Pending { get; init; }
	public string Time { get; init; }
}

/// <summary>
/// A conversa com um amigo, no canto da janela: as mensagens (as mais
/// antigas sob pedido), o campo de escrever e enviar. Quem guarda e manda é
/// o <see cref="FriendsLink"/>; aqui só se mostra.
/// </summary>
public sealed class ChatViewModel : Observable
{
	private readonly FriendsLink _link;
	private string _draft = "", _where = "";

	public ChatViewModel(FriendsLink link, Friend friend, System.Action close)
	{
		_link = link;
		Friend = friend;
		Send = new Command(DoSend);
		Close = new Command(close);
		Older = new Command(() => _link.LoadTalk(Friend.Id, older: true));
		Reload();
	}

	public Friend Friend { get; private set; }
	public string Name => Friend.Name;
	public string Tag => Friend.Tag.Length == 0 ? "" : "#" + Friend.Tag;
	public string Where { get => _where; private set => Set(ref _where, value); }

	public ObservableCollection<ChatLine> Lines { get; } = new();
	public bool IsEmpty => Lines.Count == 0;
	public bool HasOlder => _link.HasOlder(Friend.Id);

	public string Draft { get => _draft; set => Set(ref _draft, value); }
	public Command Send { get; }
	public Command Close { get; }
	public Command Older { get; }

	/// <summary>A lista mudou: a janela rola para o fim.</summary>
	public event System.Action Scrolled;

	internal void Update(Friend friend, string where)
	{
		Friend = friend;
		Where = where;
		Raise(nameof(Name));
	}

	internal void Reload()
	{
		Lines.Clear();
		foreach (DirectMessage m in _link.Talk(Friend.Id))
			Lines.Add(new ChatLine
			{
				Text = m.Text, Mine = m.From == _link.Me, Pending = m.Pending,
				Time = m.At.ToLocalTime().ToString("HH:mm")
			});
		Raise(nameof(IsEmpty));
		Raise(nameof(HasOlder));
		Scrolled?.Invoke();
	}

	private void DoSend()
	{
		string text = (Draft ?? "").Trim();
		if (text.Length == 0) return;
		_link.SendDm(Friend.Id, text);
		Draft = "";
	}
}
