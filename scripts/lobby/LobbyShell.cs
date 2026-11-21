using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// O lobby: uma moldura fixa (o header) que nunca sai, e um miolo que
/// troca de conteúdo — lista de salas, sala, loja, inventário, perfil,
/// missões, opções.
///
/// Loja e inventário não são cenas separadas no sentido de navegação: são
/// o miolo trocando, com o header intacto. É o modelo de lobby
/// persistente do gênero, e é o que deixa o jogador abrir a loja sem
/// "sair" do canal.
///
/// Fluxo: entrada (só no primeiro acesso) → lista de salas → sala →
/// partida → resultado → de volta à sala. O ciclo sala → partida →
/// resultado → sala é o que se otimiza: cada atrito ali custa partidas
/// jogadas.
/// </summary>
public partial class LobbyShell : Control
{
	public static LobbyShell Instance { get; private set; }

	private LobbyHeader _header;
	private Control _content;
	private Control _overlay;

	public LobbyScreen Current { get; private set; }

	public override void _EnterTree() => Instance = this;

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
		GetTree().NodeAdded -= OnNodeAdded;
		AccountSync.Promoted -= ShowPromotions;
		Backend.SessionChanged -= OnSessionChanged;
		// A partida volta à paleta e ao estilo do jogo (HUD, menu do Esc).
		UiTokens.Menu = false;
	}

	public override void _Ready()
	{
		// Os menus têm paleta e estilo próprios (UiTokens.Menu): antes de
		// montar qualquer coisa.
		UiTokens.Menu = true;
		UiTheme.Apply(this);
		UiKit.Fill(this);

		// Qualquer contagem de menu herdada da partida acaba aqui, e o
		// cursor é sempre do lobby.
		UiState.Reset();
		Input.MouseMode = Input.MouseModeEnum.Visible;
		InputSetup.EnsureActions();
		Settings.ApplyAudio();

		// O fundo: azul-noite com névoa e feixes de luz (MenuBackdrop).
		AddChild(new MenuBackdrop());

		// Os cartões são vidro fosco sobre esse fundo: antes de montar
		// qualquer painel.
		GetTree().NodeAdded += OnNodeAdded;

		VBoxContainer frame = UiKit.Fill(UiKit.Column(0));
		AddChild(frame);

		_header = new LobbyHeader();
		frame.AddChild(_header);

		_content = new Control
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ClipContents = true
		};
		frame.AddChild(_content);

		_overlay = UiKit.Fill(new Control { MouseFilter = MouseFilterEnum.Ignore });
		AddChild(_overlay);

		// A conta (carteira, itens, missões) mora no servidor: com um login
		// guardado, já vem ao abrir; e voltando de uma partida, vem de novo.
		AccountSync.Promoted += ShowPromotions;
		Backend.SessionChanged += OnSessionChanged;
		_ = AccountSync.Resume();
		ShowPromotions();

		GoStart();
	}

	/// <summary>A conta subiu de patente (resgatou missão, o servidor
	/// pagou uma partida): uma comemoração de cada vez.</summary>
	private void ShowPromotions()
	{
		if (!IsInsideTree() || _promoting || AccountSync.PendingPromotions.Count == 0) return;
		int rank = AccountSync.PendingPromotions[0];
		AccountSync.PendingPromotions.RemoveAt(0);
		// Nível comum só aparece na barra; a tela é para borda ou estrela nova.
		if (!Ranks.IsMilestone(rank)) { ShowPromotions(); return; }
		_promoting = true;
		PromotionOverlay.Open(_overlay, rank, () =>
		{
			_promoting = false;
			ShowPromotions();
		});
	}

	private bool _promoting;

	/// <summary>Saiu da conta (ou ela venceu, ou foi derrubada): volta ao login.</summary>
	private void OnSessionChanged()
	{
		if (Backend.LoggedIn || Current is LoginScreen || !IsInsideTree()) return;
		Callable.From(() => GoLogin()).CallDeferred();
	}

	/// <summary>A primeira tela: o login, sempre que o jogo abre (Jhon,
	/// 20/11: o acesso pode estar salvo, mas entrar é clicar na conta); de
	/// volta de uma partida, a sala; senão, a lista de salas.</summary>
	private void GoStart()
	{
		if (DevScreen()) return;

		if (!Backend.LoggedIn)
		{
			GoLogin();
			return;
		}

		ReturnTarget target = RoomConfig.ReturnTo;
		RoomConfig.ReturnTo = ReturnTarget.None;

		// De volta de uma partida online (ou expulso dela com um recado):
		// a sala online continua aberta no servidor.
		if (LobbyLink.InRoom || OnlineMatch.Notice != null)
		{
			GoOnline();
			return;
		}

		if (target == ReturnTarget.Room && RoomDirectory.Current != null)
			Go(new RoomScreen(RoomDirectory.Current));
		else
			GoOnline();
	}

	// ------------------------------------------------------------------ vidro fosco

	/// <summary>Os painéis do tema que viram vidro fosco nos menus
	/// (pedido do Jhon, 17/11: fundos "com blur, foscos"), com o tom de cada
	/// um — vidro escuro.</summary>
	private static readonly Dictionary<string, Color> Frosted = new()
	{
		["Surface1"] = HudStyle.Frost,
		["Surface2"] = new Color(0.075f, 0.115f, 0.17f, 0.5f),
		["Card"] = new Color(0.075f, 0.115f, 0.17f, 0.5f),
		["Header"] = new Color(0.025f, 0.045f, 0.075f, 0.74f)
	};

	private readonly Dictionary<string, StyleBoxFlat> _frostBoxes = new();

	// Todo painel que entra no lobby passa por aqui, venha de onde vier
	// (UiKit.Panel, uma tela, um modal). Adiado: quem monta o painel
	// costuma dar a variação e o estilo logo depois de criar.
	private void OnNodeAdded(Node node)
	{
		if (node is PanelContainer panel && IsAncestorOf(panel))
			Callable.From(() => Frost(panel)).CallDeferred();
	}

	/// <summary>Troca o fundo chapado de um painel do tema por vidro: o
	/// mesmo desenho (cantos, borda, sombra), com o fundo marcado para o
	/// shader (<see cref="HudStyle.Glass"/>). Painel com estilo ou
	/// material próprio fica como está.</summary>
	private void Frost(PanelContainer panel)
	{
		if (!IsInstanceValid(panel) || !panel.IsInsideTree()) return;
		if (panel.Material != null || panel.HasThemeStyleboxOverride("panel")) return;

		string kind = panel.ThemeTypeVariation.ToString();
		if (!Frosted.TryGetValue(kind, out Color tint)) return;

		if (!_frostBoxes.TryGetValue(kind, out StyleBoxFlat box))
		{
			if (panel.GetThemeStylebox("panel") is not StyleBoxFlat themed) return;
			box = (StyleBoxFlat)themed.Duplicate();
			box.BgColor = HudStyle.GlassMark;
			// A beira do vidro pega um fio de luz.
			box.BorderColor = new Color(0.75f, 0.88f, 1f, kind == "Header" ? 0.1f : 0.13f);
			_frostBoxes[kind] = box;
		}

		panel.AddThemeStyleboxOverride("panel", box);
		panel.Material = HudStyle.Glass(tint);
	}

	// ------------------------------------------------------------------ navegação

	public void Go(LobbyScreen screen)
	{
		bool wasPanel = IsPanel(Current);
		if (Current != null)
		{
			_content.RemoveChild(Current);
			Current.QueueFree();
		}

		// Loja, inventário, perfil e missões abrem como painel; voltar
		// deles fecha. Trocar entre dois painéis só abre.
		if (IsPanel(screen)) Sfx.Ui("ui_abrir");
		else if (wasPanel) Sfx.Ui("ui_fechar");

		// Uma música só no lobby inteiro: o tema da sala (19/11: "a música
		// da sala pode ser a do menu principal também; essa é bem melhor").
		// Navegar entre telas não recomeça o tema.
		PlayMusic("musica_sala");

		Current = screen;
		_content.AddChild(screen);
		_header.Visible = screen is not LoginScreen;
		_header.SetActive(screen.NavIcon);
		Motion.FadeIn(screen, UiTokens.Normal);
	}

	private static bool IsPanel(LobbyScreen screen) =>
		screen is ShopScreen or InventoryScreen or ProfileScreen or MissionsScreen;

	// ------------------------------------------------------------------ música

	/// <summary>Volume das músicas do lobby, em dB (o canal Música soma o
	/// das opções por cima).</summary>
	private const float MusicDb = -2f;

	/// <summary>Tempo da troca de uma música para a outra.</summary>
	private const float MusicFade = 1.2f;

	private AudioStreamPlayer _music;
	private string _musicName;

	/// <summary>
	/// Troca a música do lobby, uma saindo enquanto a outra entra. Pedir a
	/// que já toca não faz nada — navegar entre telas não recomeça o tema.
	/// </summary>
	private void PlayMusic(string name)
	{
		if (name == _musicName) return;
		_musicName = name;

		if (_music != null && IsInstanceValid(_music))
		{
			AudioStreamPlayer old = _music;
			Tween fade = old.CreateTween();
			fade.TweenProperty(old, "volume_db", -60f, MusicFade);
			fade.TweenCallback(Callable.From(old.QueueFree));
		}

		_music = Sfx.Music(this, name, -40f);
		if (_music == null) return;
		_music.CreateTween().TweenProperty(_music, "volume_db", MusicDb, MusicFade)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
	}

	public void GoRoom(RoomListing room) => Go(new RoomScreen(room));

	/// <summary>A sala online em que o jogador está, no visual da sala.</summary>
	public void GoOnlineRoom()
	{
		if (LobbyLink.InRoom) Go(new RoomScreen(LobbyLink.Instance.Room));
		else GoOnline();
	}
	public void GoShop(string focus = null) => Go(new ShopScreen(focus));
	public void GoInventory(string focus = null) => Go(new InventoryScreen(focus));
	public void GoProfile() => Go(new ProfileScreen());
	public void GoMissions() => Go(new MissionsScreen());
	public void GoOptions() => Go(new OptionsScreen());
	public void GoOnline() => Go(new OnlineScreen());

	/// <summary>A tela de login, com um recado (a sessão venceu, saiu da conta).</summary>
	public void GoLogin(string notice = null) => Go(new LoginScreen(notice));
	public void GoCredits() => Go(new CreditsScreen());

	/// <summary>A aba Salas: a sala em que o jogador está, se estiver numa;
	/// senão, as salas online.</summary>
	public void GoHome()
	{
		if (LobbyLink.InRoom) GoOnlineRoom();
		else if (RoomDirectory.Current != null) GoRoom(RoomDirectory.Current);
		else GoOnline();
	}

	// ------------------------------------------------------------------ avisos

	public void Toast(string text, Color? color = null) => global::Toast.Push(_overlay, text, color);

	public Modal Modal(string title, Control body, params Modal.Choice[] choices) =>
		global::Modal.Open(_overlay, title, body, choices);

	public Control Overlay => _overlay;

	// ------------------------------------------------------------------ desenvolvimento

	/// <summary>
	/// Abre uma tela direto pela linha de comando, para testar e
	/// fotografar sem clicar: <c>-- --tela=loja</c>. Telas: entrada (o login),
	/// salas, treino, sala, loja, inventario, perfil, missoes, opcoes, online, creditos.
	/// </summary>
	private bool DevScreen()
	{
		string arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--tela="));
		if (arg == null) return false;

		string screen = arg["--tela=".Length..];
		string focus = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--foco="))?["--foco=".Length..];
		if (!Profile.HasName && screen != "entrada") Profile.SetName("Jhon", out _);
		// Para fotografar com conta: entra com o primeiro acesso salvo.
		if (screen != "entrada" && !Backend.LoggedIn && Backend.Saved.Count > 0) _ = Backend.EnterSaved(Backend.Saved[0]);

		switch (screen)
		{
			case "entrada": GoLogin(); break;
			case "sala": GoRoom(RoomDirectory.Current ?? RoomDirectory.Create(new RoomSettings { Title = "Sala do Jhon" })); break;
			case "loja": GoShop(focus); break;
			case "inventario": GoInventory(focus); break;
			case "perfil": GoProfile(); break;
			case "missoes": GoMissions(); break;
			case "opcoes": GoOptions(); break;
			case "online": GoOnline(); break;
			case "creditos": GoCredits(); break;
			default: GoOnline(); break;
		}

		return true;
	}
}
