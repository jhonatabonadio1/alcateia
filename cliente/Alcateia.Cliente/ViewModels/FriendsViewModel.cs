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
	public string RequestsTitle => $"PEDIDOS · {Requests.Count}";
	public string InGameTitle => $"NO JOGO · {InGame.Count}";
	public string InAppTitle => $"NO APLICATIVO · {InApp.Count}";
	public string OfflineTitle => $"DESCONECTADOS · {Offline.Count}";
	public string SentTitle => $"PEDIDOS ENVIADOS · {Sent.Count}";
	public string OnlineCount => (InGame.Count + InApp.Count).ToString();

	/// <summary>O recado no lugar da lista: ligando, caiu, ou ainda sem amigos.</summary>
	public string Note
	{
		get
		{
			if (_link.Replaced) return "A sua conta abriu o WolfLeague em outro computador. Reabra o cliente para ver os amigos aqui.";
			if (!_link.Connected) return "Ligando com o servidor…";
			if (!_link.Loaded) return "Carregando os amigos…";
			if (_link.Friends.Count + _link.Requests.Count + _link.Sent.Count == 0)
				return "Você ainda não tem amigos. Peça a tag de alguém (Nome#0000) e adicione aqui em cima — ou passe a sua.";
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
			_main.Toast("Escreva o nome com a tag, assim: Nome#0000.", error: true);
			return;
		}
		if (!_link.Connected)
		{
			_main.Toast("Sem ligação com o servidor. Tente de novo em instantes.", error: true);
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
				Friend = f, IsRequest = true, Where = "Quer ser seu amigo",
				Accept = new Command(() => _link.Accept(f.Id)), Decline = new Command(() => _link.Decline(f.Id))
			});

		foreach (Friend f in _link.Friends)
		{
			var item = new FriendItem
			{
				Friend = f, Where = Where(f),
				Remove = new Command(() => _main.Ask("Desfazer a amizade?", $"{f.Name} sai da sua lista, e você da dele.",
					"Desfazer", () => _link.Remove(f.Id), danger: true))
			};
			(f.InGame ? InGame : f.Status == Presence.App ? InApp : Offline).Add(item);
		}

		foreach (Friend f in _link.Sent)
			Sent.Add(new FriendItem { Friend = f, IsSent = true, Where = "Aguardando resposta", Decline = new Command(() => _link.Decline(f.Id)) });

		RaiseAll();
	}

	private static string Where(Friend f) => f.Status switch
	{
		Presence.App => "No aplicativo",
		Presence.Online => "No menu do jogo",
		Presence.Room => $"Na sala #{f.Room}",
		Presence.Playing => "Em partida",
		_ => "Desconectado"
	};
}
