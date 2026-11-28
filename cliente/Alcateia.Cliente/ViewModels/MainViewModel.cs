using System;
using System.Threading.Tasks;
using Alcateia.Cliente.Core;

namespace Alcateia.Cliente.ViewModels;

/// <summary>A página do miolo: um jogo da biblioteca ou as configurações do launcher.</summary>
public enum Page { Library, Settings }

/// <summary>Um jogo no menu da esquerda: o ícone, o nome e uma palavra de como ele está.</summary>
public sealed class LibraryItem : Observable
{
	public GameViewModel Game { get; init; }
	public Command Select { get; set; }
	public string Name => Game.Entry.Name;
	public Avalonia.Media.Imaging.Bitmap Icon => Game.Icon;
	public string Status => Game.ShortStatus;
	public bool Busy => Game.ProgressVisible;
	public double Progress => Game.ProgressValue;

	private bool _selected;
	public bool Selected { get => _selected; set => Set(ref _selected, value); }

	internal void Refresh() => RaiseAll();
}

/// <summary>Uma pergunta por cima da tela: confirmar ou voltar atrás.</summary>
public sealed class ModalViewModel
{
	public string Title { get; init; }
	public string Body { get; init; }
	public string ConfirmText { get; init; }
	public string DismissText { get; init; }
	public bool Danger { get; init; }
	public bool Normal => !Danger;
	public Command Confirm { get; init; }
	public Command Dismiss { get; init; }
}

/// <summary>
/// O cliente inteiro: quem está entrado, que página está aberta, e as peças
/// (entrada, jogo, amigos, configurações). A janela só mostra o que está
/// aqui; o que precisa da janela de verdade — área de transferência,
/// escolher pasta, sair da frente — entra pelos ganchos (<see cref="CopyText"/>,
/// <see cref="PickFolder"/>, <see cref="HideWindow"/>).
/// </summary>
public sealed class MainViewModel : Observable
{
	public Settings Settings { get; }
	public Api Api { get; } = new();
	public FriendsLink FriendsLink { get; } = new();
	public SelfUpdate SelfUpdate { get; } = new();

	/// <summary>Os jogos da biblioteca (<see cref="Games.All"/>), um serviço e uma página cada.</summary>
	public System.Collections.ObjectModel.ObservableCollection<LibraryItem> Library { get; } = new();

	/// <summary>O jogo escolhido no menu da esquerda.</summary>
	public GameViewModel Game { get => _game; private set { if (Set(ref _game, value)) Raise(nameof(GameService)); } }
	public GameService GameService => _game.Service;
	private GameViewModel _game;

	public LoginViewModel Login { get; }
	public FriendsViewModel Friends { get; }
	public SettingsViewModel Config { get; }

	/// <summary>Os ganchos da janela.</summary>
	public Func<string, Task> CopyText;
	public Func<string, Task<string>> PickFolder;
	public Action HideWindow;
	/// <summary>A janela está fora da vista (na bandeja ou minimizada)?</summary>
	public Func<bool> WindowAway;

	private Account _account;
	private Page _page = Page.Library;
	private ModalViewModel _modal;
	private string _toast = "";
	private bool _toastIsError;
	private int _toastTurn;

	public MainViewModel(Settings settings = null)
	{
		Settings = settings ?? Settings.Load();
		Tr.Set(Settings.Language);
		Login = new LoginViewModel(this);
		foreach (GameEntry entry in Games.All)
		{
			var service = new GameService(Settings, Api, entry);
			var game = new GameViewModel(this, service);
			var item = new LibraryItem { Game = game };
			item.Select = new Command(() => Open(item));
			Library.Add(item);
			service.Changed += () => OnGameChanged(service, item);
		}
		_game = Library[0].Game;
		Library[0].Selected = true;
		Friends = new FriendsViewModel(this, FriendsLink);
		Config = new SettingsViewModel(this);

		GoSettings = new Command(() => Page = Page.Settings);
		Logout = new Command(() => _ = LeaveAccount());
		CopyHandle = new Command(() => _ = CopyMine());
		ToggleTwoFactor = new Command(OpenTwoFactor);
		RestartNow = new Command(() =>
		{
			if (SelfUpdate.Restart()) Quit?.Invoke();
			else Toast(Tr.T("toast.restart_failed"), error: true);
		});

		// O idioma mudou: o que é montado em código se refaz.
		Tr.Changed += () =>
		{
			RaiseAll();
			foreach (LibraryItem item in Library) { item.Game.Refresh(); item.Refresh(); }
			Friends.Rebuild();
			Login.Reload();
			Config.Refresh();
		};
		SelfUpdate.Changed += () =>
		{
			Raise(nameof(UpdateBanner));
			Raise(nameof(HasUpdateBanner));
			RestartIfIdle();
		};
	}

	private readonly System.Collections.Generic.HashSet<GameService> _autoTried = new();

	private void OnGameChanged(GameService service, LibraryItem item)
	{
		Config.Refresh();
		item.Refresh();
		// Chegou versão nova e o jogador deixou no automático: baixa sem esperar o clique.
		if (!PreviewOnly && service.State == GameState.UpdateAvailable && Settings.AutoUpdate && service.Problem == "" && _autoTried.Add(service))
			_ = service.Install();
		if (service.State == GameState.Ready) _autoTried.Remove(service);
		// O jogo fechou ou o download acabou: a versão nova do launcher pode entrar.
		RestartIfIdle();
	}

	/// <summary>Abre a página de um jogo da biblioteca.</summary>
	private void Open(LibraryItem item)
	{
		foreach (LibraryItem other in Library) other.Selected = other == item;
		Game = item.Game;
		Page = Page.Library;
	}

	/// <summary>Quanto tempo depois de abrir a troca ainda é "ao abrir":
	/// o jogador mal começou a mexer, e reabrir é o que os clientes grandes
	/// fazem ao se atualizar.</summary>
	private static readonly TimeSpan RestartOnOpenWindow = TimeSpan.FromSeconds(30);
	private readonly DateTime _openedAt = DateTime.UtcNow;

	/// <summary>
	/// A versão nova do cliente já está no disco: reinicia sozinho quando não
	/// atrapalha (Jhon, 27/11: "o launcher deve se atualizar sozinho"). Antes
	/// esperava o clique ou a próxima vez que abrisse — e com o cliente na
	/// bandeja isso podia levar dias. Não troca com o jogo aberto, com o jogo
	/// baixando, no meio das duas etapas, nem com uma conta entrada que não
	/// ficou lembrada ou sem o entrar sozinho (o cliente novo não entraria nela). Com a janela
	/// na frente, só nos primeiros segundos depois de abrir; depois disso
	/// fica o aviso, e a troca acontece quando a janela for para a bandeja.
	/// </summary>
	public void RestartIfIdle()
	{
		if (PreviewOnly || SelfUpdate.Ready == "") return;
		foreach (LibraryItem item in Library)
			if (item.Game.Service.State is GameState.Working or GameState.Playing) return;
		if (TwoFactor != null || Modal != null) return;
		if (_account != null && (!Settings.AutoLogin || !Settings.Accounts.Exists(a => a.Id == _account.Id && a.Url == Api.Url))) return;
		bool away = WindowAway?.Invoke() ?? false;
		if (!away && DateTime.UtcNow - _openedAt > RestartOnOpenWindow) return;
		if (SelfUpdate.Restart(hidden: away)) Quit?.Invoke();
	}

	/// <summary>Fecha o programa (a janela liga).</summary>
	public Action Quit;

	// ------------------------------------------------------------------ a conta

	public Account Account => _account;
	public bool IsLoggedIn => _account != null;
	public bool IsLoggedOut => _account == null;
	public string MeName => _account?.Name ?? "";
	public string MeTag => string.IsNullOrEmpty(_account?.Tag) ? "" : "#" + _account.Tag;
	public string MeInitial => MeName.Length > 0 ? MeName[..1].ToUpperInvariant() : "?";
	public string MeLevel => _account == null ? "" : Tr.F("me.level", _account.Rank);

	// ---- verificação em duas etapas
	public bool TwoFactorOn => _account?.TwoFactor ?? false;
	public string TwoFactorState => Tr.T(TwoFactorOn ? "settings.2fa_on" : "settings.2fa_off");
	public string TwoFactorButton => Tr.T(TwoFactorOn ? "settings.2fa_disable" : "settings.2fa_enable");
	public Command ToggleTwoFactor { get; }

	private TwoFactorViewModel _twoFactor;
	public TwoFactorViewModel TwoFactor { get => _twoFactor; private set { Set(ref _twoFactor, value); Raise(nameof(HasTwoFactor)); } }
	public bool HasTwoFactor => _twoFactor != null;

	internal void OpenTwoFactor()
	{
		if (_account == null) return;
		TwoFactor = new TwoFactorViewModel(this, enable: !TwoFactorOn);
	}

	/// <summary>A tela das duas etapas fechou; <paramref name="on"/>: como ficou (null: não mudou).</summary>
	internal void CloseTwoFactor(bool? on)
	{
		TwoFactor = null;
		if (on == null || _account == null) return;
		_account = _account with { TwoFactor = on.Value };
		RaiseAll();
		Toast(Tr.T(on.Value ? "toast.2fa_on" : "toast.2fa_off"));
	}

	public Command Logout { get; }
	public Command CopyHandle { get; }

	/// <summary>Abre o cliente: entra na conta lembrada e confere o jogo.</summary>
	public async Task Start()
	{
		if (PreviewOnly) return;
		GameService first = Library[0].Game.Service;
		foreach (LibraryItem item in Library)
			if (item.Game.Service != first) _ = item.Game.Service.Refresh();
		_ = first.Refresh().ContinueWith(_ => SelfUpdate.Check(first.Downloads), TaskScheduler.FromCurrentSynchronizationContext());
		if (Settings.AutoLogin && Settings.Accounts.Count > 0)
			await Login.EnterSaved(Settings.Accounts[0], quiet: true);
	}

	/// <summary>De tempos em tempos (a janela chama): há versão nova do jogo ou do cliente?</summary>
	public async Task Recheck()
	{
		if (PreviewOnly) return;
		foreach (LibraryItem item in Library) await item.Game.Service.Refresh();
		await SelfUpdate.Check(Library[0].Game.Service.Downloads);
	}

	/// <summary>Entrou (pela senha ou por um acesso salvo).</summary>
	public void Entered(Account account, bool remember)
	{
		_account = account;
		if (remember) Settings.Remember(new SavedAccount(Api.Url, account.Id, account.Name, account.Tag, account.Token));
		Login.Reload();
		FriendsLink.Start(Api.LobbyUrl(account.Token), account.Id);
		Friends.Rebuild();
		Page = Page.Library;
		RaiseAll();
	}

	/// <summary>Só para fotografar as telas: nada de servidor nem de rede.</summary>
	internal bool PreviewOnly;

	/// <summary>Só para fotografar as telas: uma conta de mentira, sem ligar no servidor.</summary>
	internal void Preview(Account account)
	{
		PreviewOnly = true;
		_account = account;
		RaiseAll();
	}

	/// <summary>Sai da conta: o login deixa de valer no servidor e o acesso salvo dela some.</summary>
	private async Task LeaveAccount()
	{
		Account leaving = _account;
		if (leaving == null) return;
		FriendsLink.Stop();
		TwoFactor = null;
		_account = null;
		Settings.Forget(Api.Url, leaving.Id);
		Login.Reload();
		Friends.Rebuild();
		RaiseAll();
		await Api.Logout(leaving.Token);
	}

	private async Task CopyMine()
	{
		if (_account == null || CopyText == null) return;
		await CopyText(_account.Handle);
		Toast(Tr.F("toast.tag_copied", _account.Handle));
	}

	/// <summary>O jogo abriu: a janela sai da frente, se o jogador quer assim.</summary>
	public void GameOpened()
	{
		if (Settings.MinimizeOnPlay) HideWindow?.Invoke();
	}

	// ------------------------------------------------------------------ páginas

	public Page Page
	{
		get => _page;
		set
		{
			if (!Set(ref _page, value)) return;
			Raise(nameof(IsLibrary));
			Raise(nameof(IsSettings));
		}
	}

	public bool IsLibrary => _page == Page.Library;
	public bool IsSettings => _page == Page.Settings;
	public Command GoSettings { get; }

	// ------------------------------------------------------------------ avisos

	public string UpdateBanner => SelfUpdate.Ready == "" ? "" : Tr.F("banner.update_ready", SelfUpdate.Ready);
	public bool HasUpdateBanner => SelfUpdate.Ready != "";
	public Command RestartNow { get; }

	public ModalViewModel Modal { get => _modal; private set { Set(ref _modal, value); Raise(nameof(HasModal)); } }
	public bool HasModal => _modal != null;

	public void Ask(string title, string body, string confirm, Action onConfirm, bool danger = false)
	{
		Modal = new ModalViewModel
		{
			Title = title, Body = body, ConfirmText = confirm.ToUpperInvariant(), DismissText = Tr.T("common.back"), Danger = danger,
			Confirm = new Command(() => { Modal = null; onConfirm(); }),
			Dismiss = new Command(() => Modal = null)
		};
	}

	public string ToastText { get => _toast; private set { Set(ref _toast, value); Raise(nameof(HasToast)); } }
	public bool ToastIsError { get => _toastIsError; private set { Set(ref _toastIsError, value); Raise(nameof(ToastIsInfo)); } }
	public bool ToastIsInfo => !_toastIsError;
	public bool HasToast => _toast.Length > 0;

	/// <summary>Um aviso curto no pé da janela, que some sozinho.</summary>
	public async void Toast(string text, bool error = false)
	{
		int turn = ++_toastTurn;
		ToastIsError = error;
		ToastText = text ?? "";
		await Task.Delay(error ? 6000 : 4000);
		if (turn == _toastTurn) ToastText = "";
	}
}
