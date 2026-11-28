using System;
using System.Threading.Tasks;
using Alcateia.Cliente.Core;

namespace Alcateia.Cliente.ViewModels;

public enum Page { Game, News, Settings }

/// <summary>Uma pergunta por cima da tela: confirmar ou voltar atrás.</summary>
public sealed class ModalViewModel
{
	public string Title { get; init; }
	public string Body { get; init; }
	public string ConfirmText { get; init; }
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
	public GameService GameService { get; }
	public SelfUpdate SelfUpdate { get; } = new();

	public LoginViewModel Login { get; }
	public GameViewModel Game { get; }
	public FriendsViewModel Friends { get; }
	public SettingsViewModel Config { get; }

	/// <summary>Os ganchos da janela.</summary>
	public Func<string, Task> CopyText;
	public Func<string, Task<string>> PickFolder;
	public Action HideWindow;

	private Account _account;
	private Page _page = Page.Game;
	private ModalViewModel _modal;
	private string _toast = "";
	private bool _toastIsError;
	private int _toastTurn;

	public MainViewModel(Settings settings = null)
	{
		Settings = settings ?? Settings.Load();
		GameService = new GameService(Settings, Api);
		Login = new LoginViewModel(this);
		Game = new GameViewModel(this, GameService);
		Friends = new FriendsViewModel(this, FriendsLink);
		Config = new SettingsViewModel(this);

		GoGame = new Command(() => Page = Page.Game);
		GoNews = new Command(() => Page = Page.News);
		GoSettings = new Command(() => Page = Page.Settings);
		Logout = new Command(() => _ = LeaveAccount());
		CopyHandle = new Command(() => _ = CopyMine());
		ToggleTwoFactor = new Command(OpenTwoFactor);
		RestartNow = new Command(() =>
		{
			if (SelfUpdate.Restart()) Quit?.Invoke();
			else Toast("Não consegui reabrir sozinho. Feche e abra o cliente.", error: true);
		});

		GameService.Changed += () =>
		{
			Config.Refresh();
			// Chegou versão nova e o jogador deixou no automático: baixa sem esperar o clique.
			if (GameService.State == GameState.UpdateAvailable && Settings.AutoUpdate && GameService.Problem == "" && !_autoTried)
			{
				_autoTried = true;
				_ = GameService.Install();
			}
			if (GameService.State == GameState.Ready) _autoTried = false;
		};
		SelfUpdate.Changed += () =>
		{
			Raise(nameof(UpdateBanner));
			Raise(nameof(HasUpdateBanner));
		};
	}

	private bool _autoTried;

	/// <summary>Fecha o programa (a janela liga).</summary>
	public Action Quit;

	// ------------------------------------------------------------------ a conta

	public Account Account => _account;
	public bool IsLoggedIn => _account != null;
	public bool IsLoggedOut => _account == null;
	public string MeName => _account?.Name ?? "";
	public string MeTag => string.IsNullOrEmpty(_account?.Tag) ? "" : "#" + _account.Tag;
	public string MeInitial => MeName.Length > 0 ? MeName[..1].ToUpperInvariant() : "?";
	public string MeLevel => _account == null ? "" : $"Nível {_account.Rank}";

	// ---- verificação em duas etapas
	public bool TwoFactorOn => _account?.TwoFactor ?? false;
	public string TwoFactorState => TwoFactorOn
		? "Ligada: entrar com a senha pede também o código do aplicativo autenticador."
		: "Desligada. Ligue para que só a senha não baste para entrar na sua conta.";
	public string TwoFactorButton => TwoFactorOn ? "DESATIVAR" : "ATIVAR";
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
		Toast(on.Value ? "Verificação em duas etapas ligada." : "Verificação em duas etapas desligada.");
	}

	public Command Logout { get; }
	public Command CopyHandle { get; }

	/// <summary>Abre o cliente: entra na conta lembrada e confere o jogo.</summary>
	public async Task Start()
	{
		if (PreviewOnly) return;
		_ = GameService.Refresh().ContinueWith(_ => SelfUpdate.Check(GameService.Downloads), TaskScheduler.FromCurrentSynchronizationContext());
		if (Settings.AutoLogin && Settings.Accounts.Count > 0)
			await Login.EnterSaved(Settings.Accounts[0], quiet: true);
	}

	/// <summary>De tempos em tempos (a janela chama): há versão nova do jogo ou do cliente?</summary>
	public async Task Recheck()
	{
		if (PreviewOnly) return;
		await GameService.Refresh();
		await SelfUpdate.Check(GameService.Downloads);
	}

	/// <summary>Entrou (pela senha ou por um acesso salvo).</summary>
	public void Entered(Account account, bool remember)
	{
		_account = account;
		if (remember) Settings.Remember(new SavedAccount(Api.Url, account.Id, account.Name, account.Tag, account.Token));
		Login.Reload();
		FriendsLink.Start(Api.LobbyUrl(account.Token));
		Friends.Rebuild();
		Page = Page.Game;
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
		Toast($"{_account.Handle} copiado: passe para um amigo adicionar você");
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
			Raise(nameof(IsGame));
			Raise(nameof(IsNews));
			Raise(nameof(IsSettings));
		}
	}

	public bool IsGame => _page == Page.Game;
	public bool IsNews => _page == Page.News;
	public bool IsSettings => _page == Page.Settings;
	public Command GoGame { get; }
	public Command GoNews { get; }
	public Command GoSettings { get; }

	// ------------------------------------------------------------------ avisos

	public string UpdateBanner => SelfUpdate.Ready == "" ? "" : $"A versão {SelfUpdate.Ready} do cliente já foi baixada.";
	public bool HasUpdateBanner => SelfUpdate.Ready != "";
	public Command RestartNow { get; }

	public ModalViewModel Modal { get => _modal; private set { Set(ref _modal, value); Raise(nameof(HasModal)); } }
	public bool HasModal => _modal != null;

	public void Ask(string title, string body, string confirm, Action onConfirm, bool danger = false)
	{
		Modal = new ModalViewModel
		{
			Title = title, Body = body, ConfirmText = confirm.ToUpperInvariant(), Danger = danger,
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
