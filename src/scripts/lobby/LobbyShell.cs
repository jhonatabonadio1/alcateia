using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// O lobby: uma moldura fixa que nunca sai — o header em cima, o painel
/// social à direita — e um miolo que troca de conteúdo: início, jogar,
/// salas, sala, loja, inventário, perfil, missões, opções.
///
/// Loja e inventário não são cenas separadas no sentido de navegação: são
/// o miolo trocando, com a moldura intacta. É o modelo de lobby persistente
/// do gênero, e é o que deixa o jogador abrir a loja sem "sair" da sala.
///
/// Fluxo (27/11): login → **início** (banners e notícias) → **jogar** (jogo
/// rápido, treinamento ou salas) → sala → partida → resultado → de volta à
/// sala. O ciclo sala → partida → resultado → sala é o que se otimiza: cada
/// atrito ali custa partidas jogadas.
///
/// A moldura também cuida do que vale no lobby inteiro: a ligação com o
/// saguão (os amigos precisam dela em qualquer tela), a busca do jogo
/// rápido, os convites que chegam e a entrada das telas (deslizando; a
/// cortina fica para as trocas de lugar — <see cref="ScreenWipe"/>).
/// </summary>
public partial class LobbyShell : Control
{
	public static LobbyShell Instance { get; private set; }

	private LobbyHeader _header;
	private Control _content;
	private Control _overlay;
	private SocialDock _social;
	private MatchSearch _search;
	private ColorRect _veil;

	public LobbyScreen Current { get; private set; }

	public override void _EnterTree() => Instance = this;

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
		GetTree().NodeAdded -= OnNodeAdded;
		AccountSync.Promoted -= ShowPromotions;
		Backend.SessionChanged -= OnSessionChanged;
		Social.Invited -= OnInvited;
		Social.GroupInvited -= OnGroupInvited;
		Social.Notice -= OnSocialNotice;
		if (LobbyLink.Instance is LobbyLink link && IsInstanceValid(link))
		{
			link.Changed -= OnLobbyChanged;
			link.Failed -= OnLobbyFailed;
			link.MatchEnded -= OnMatchEnded;
		}
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

		// O fundo vivo, o mesmo no jogo inteiro (27/11): a malha de polígonos
		// que ondula, as brasas e a poeira, seguindo o mouse (LoginBackdrop).
		// Fora do login um véu escuro o acalma — ali quem fala é a tela.
		AddChild(new LoginBackdrop());
		_veil = UiKit.Fill(new ColorRect { Color = new Color(UiTokens.Bg, 0f), MouseFilter = MouseFilterEnum.Ignore });
		AddChild(_veil);

		// Os cartões são vidro fosco sobre esse fundo: antes de montar
		// qualquer painel.
		GetTree().NodeAdded += OnNodeAdded;

		VBoxContainer frame = UiKit.Fill(UiKit.Column(0));
		AddChild(frame);

		_header = new LobbyHeader();
		frame.AddChild(_header);

		HBoxContainer body = UiKit.Row(0);
		body.SizeFlagsVertical = SizeFlags.ExpandFill;
		frame.AddChild(body);

		_content = new Control
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ClipContents = true
		};
		body.AddChild(_content);

		_social = new SocialDock();
		body.AddChild(_social);

		_search = new MatchSearch();
		AddChild(_search);

		_overlay = UiKit.Fill(new Control { MouseFilter = MouseFilterEnum.Ignore });
		AddChild(_overlay);

		// O saguão é do lobby inteiro: a sala em que se entra (por convite,
		// pelo jogo rápido, pela lista) abre de qualquer tela, e os erros do
		// servidor aparecem onde o jogador estiver.
		LobbyLink.Instance.Changed += OnLobbyChanged;
		LobbyLink.Instance.Failed += OnLobbyFailed;
		LobbyLink.Instance.MatchEnded += OnMatchEnded;
		Social.Invited += OnInvited;
		Social.GroupInvited += OnGroupInvited;
		Social.Notice += OnSocialNotice;
		_wasInRoom = LobbyLink.InRoom;

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
	/// volta de uma partida, a sala; senão, o início.</summary>
	private void GoStart()
	{
		if (DevScreen()) return;

		if (!Backend.LoggedIn)
		{
			GoLogin();
			return;
		}

		_ = EnsureLink();
		ReturnTarget target = RoomConfig.ReturnTo;
		RoomConfig.ReturnTo = ReturnTarget.None;

		// De volta de uma partida online (ou expulso dela com um recado):
		// a sala online continua aberta no servidor.
		if (LobbyLink.InRoom) GoOnlineRoom();
		else if (OnlineMatch.Notice != null) GoRooms();
		else if (target == ReturnTarget.Room && RoomDirectory.Current != null) Go(new RoomScreen(RoomDirectory.Current));
		else GoHome();
	}

	// ------------------------------------------------------------------ a ligação com o saguão

	private System.Threading.Tasks.Task<string> _linking;
	private double _retry;
	private bool _wasInRoom;

	/// <summary>
	/// Confere o login guardado e abre a ligação do saguão (salas, chat,
	/// amigos). Uma tentativa de cada vez: quem pedir no meio espera a
	/// mesma. Devolve nulo se abriu, ou o que dizer ao jogador.
	/// </summary>
	public System.Threading.Tasks.Task<string> EnsureLink()
	{
		if (LobbyLink.Instance.Connected) return System.Threading.Tasks.Task.FromResult<string>(null);
		return _linking ??= Link();
	}

	private async System.Threading.Tasks.Task<string> Link()
	{
		string problem = null;
		try
		{
			Backend.Reply profile = await Backend.Resume();
			// O que o painel mudou (mapas em rotação, preços, aviso) vem junto.
			if (profile.Ok) await ServerData.Refresh(0);
			bool open = profile.Ok && await LobbyLink.Instance.Open();

			if (!profile.Ok && profile.Status == 401) problem = "A sua sessão venceu. Entre de novo.";
			else if (profile.Status == 403 || profile.Status == 503) problem = profile.Error;     // banido, ou em manutenção: o servidor diz por quê
			else if (!open) problem = ServerData.Maintenance ? "O servidor está em manutenção. Volte daqui a pouco." : Loc.F("Não consegui falar com {0}. O servidor está no ar?", Backend.Url);
		}
		finally
		{
			_linking = null;
			_retry = RetryEvery;
		}
		if (problem == null) _retries = Retries;
		return problem;
	}

	/// <summary>Quantas vezes a moldura tenta religar sozinha. Não é para
	/// sempre: a conta aberta em outro computador derruba esta ligação, e
	/// dois jogos religando sem parar se derrubariam um ao outro.</summary>
	private const int Retries = 3;
	private int _retries = Retries;

	/// <summary>Sem ligação, tenta de novo de tanto em tanto tempo, em segundos.</summary>
	private const double RetryEvery = 10;

	public override void _Process(double delta)
	{
		// Caiu a ligação e o jogador continua no lobby: os amigos e as salas
		// voltam sozinhos.
		if (!Backend.LoggedIn || LobbyLink.Instance.Connected || _linking != null || Current is LoginScreen) return;
		if (_retries <= 0) return;
		_retry -= delta;
		if (_retry > 0) return;
		_retries--;
		_ = EnsureLink();
	}

	/// <summary>Entrou numa sala (pela lista, pelo jogo rápido, por um
	/// convite): a tela da sala abre, esteja o jogador onde estiver. Só na
	/// entrada — depois ele passeia pela loja sem ser puxado de volta.</summary>
	private void OnLobbyChanged()
	{
		if (!IsInsideTree()) return;
		bool inRoom = LobbyLink.InRoom;
		if (inRoom && !_wasInRoom && !LobbyLink.InQuickRoom && Current is not RoomScreen && Current is not LoginScreen)
			Callable.From(() => { if (LobbyLink.InRoom && !LobbyLink.InQuickRoom && Current is not RoomScreen) GoOnlineRoom(); }).CallDeferred();
		_wasInRoom = inRoom;
	}

	private void OnLobbyFailed(string code, string message)
	{
		if (IsInsideTree() && Current is not LoginScreen) Toast(message, UiTokens.Danger);
	}

	/// <summary>A partida da sala acabou: o servidor diz o que cada um ganhou.</summary>
	private void OnMatchEnded(System.Text.Json.Nodes.JsonObject end)
	{
		if (!IsInsideTree()) return;
		if (end["you"] is System.Text.Json.Nodes.JsonNode you)
			Toast(Loc.F("Partida registrada: +{0} XP e +{1} Cash", you["xp"], you["presas"]), UiTokens.Success);
		_ = AccountSync.Refresh();
	}

	/// <summary>Sai da conta e volta ao login.</summary>
	public async void Logout()
	{
		_search.Cancel();
		LobbyLink.Instance.Close();
		await Backend.Logout();
		if (IsInsideTree() && Current is not LoginScreen) GoLogin();
	}

	// ------------------------------------------------------------------ social

	public void ToggleSocial()
	{
		_social.Open = !_social.Open;
		_socialChosen = _social.Open;
		Sfx.Ui(_social.Open ? "ui_abrir" : "ui_fechar");
	}

	/// <summary>Abre o painel social (a sala manda, para convidar).</summary>
	public void OpenSocial()
	{
		_social.Open = true;
		_socialChosen = true;
	}

	/// <summary>O que o jogador escolheu (aberto ou recolhido); nulo: ainda
	/// não mexeu, vale o padrão de cada tela.</summary>
	private bool? _socialChosen;

	private void OnInvited(Social.Invite invite)
	{
		if (!IsInsideTree() || Current is LoginScreen) return;
		Sfx.Ui(Sfx.First("ui_recompensa_1", "ui_abrir"));
		// O convite mora no painel social: com ele recolhido, abre.
		if (!_social.Open) _social.Open = true;
		Toast(Loc.F("{0} convidou você para a sala #{1}", invite.From.Name, invite.Room), UiTokens.Accent);
	}

	private void OnGroupInvited(Social.GroupInvite invite)
	{
		if (!IsInsideTree() || Current is LoginScreen) return;
		Sfx.Ui(Sfx.First("ui_recompensa_1", "ui_abrir"));
		if (!_social.Open) _social.Open = true;
		Toast(Loc.F("{0} chamou você para o grupo", invite.From.Name), UiTokens.TeamBlue);
	}

	private void OnSocialNotice(string text)
	{
		if (IsInsideTree() && Current is not LoginScreen) Toast(text, UiTokens.Accent);
	}

	// ------------------------------------------------------------------ jogo rápido

	public MatchSearch Search => _search;

	/// <summary>O treinamento: uma sala só sua, contra bots, sem servidor
	/// de partida e sem valer XP.</summary>
	public void GoTraining()
	{
		RoomListing room = RoomDirectory.Current ?? RoomDirectory.Create(new RoomSettings { Title = "Sala de treino" });
		GoRoom(room);
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
		bool first = Current == null;
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

		bool login = screen is LoginScreen;
		_header.Visible = !login;
		_header.SetActive(screen.NavIcon);
		// O painel social: fora no login; recolhido nas telas largas (loja,
		// inventário, opções: ali ele cobriria a coluna da direita) e, nas
		// outras, como o jogador deixou.
		_social.Visible = !login;
		if (!login) _social.Open = !screen.Wide && (_socialChosen ?? true);
		// O fundo vivo fala alto no login e baixo no resto.
		_veil.CreateTween().TweenProperty(_veil, "color:a", login ? 0f : 0.42f, 0.5f);

		// A tela entra deslizando (a primeira só aparece: o jogo acabou de abrir).
		if (first) Motion.FadeIn(screen, UiTokens.Screen);
		else Motion.Enter(screen);
	}

	/// <summary>Troca de lugar, com a cortina: do login para o jogo.</summary>
	public void GoWithWipe(Func<LobbyScreen> screen)
	{
		Sfx.Ui(Sfx.First("ui_transicao", "ui_abrir"));
		ScreenWipe.Play(this, () => Go(screen()));
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
	/// <summary>A lista de salas online.</summary>
	public void GoRooms() => Go(new OnlineScreen());

	/// <summary>O nome de antes da lista de salas (telas antigas e testes).</summary>
	public void GoOnline() => GoRooms();

	/// <summary>A tela inicial: banners e notícias.</summary>
	public void GoHome() => Go(new HomeScreen());

	/// <summary>A foto de perfil, numa tela só dela.</summary>
	public void GoPhotos() => Go(new PhotoScreen());

	/// <summary>A tela de login, com um recado (a sessão venceu, saiu da conta).</summary>
	public void GoLogin(string notice = null) => Go(new LoginScreen(notice));
	public void GoCredits() => Go(new CreditsScreen());

	/// <summary>A aba JOGAR: a sala em que o jogador está, se estiver numa;
	/// senão, a escolha — jogo rápido, treinamento ou salas.</summary>
	public void GoPlay()
	{
		if (LobbyLink.InRoom) GoOnlineRoom();
		else if (RoomDirectory.Current != null) GoRoom(RoomDirectory.Current);
		else Go(new PlayScreen());
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
	/// inicio, jogar, salas, sala, loja, inventario, perfil, fotos, missoes, opcoes, creditos, carregando.
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
			case "online" or "salas": GoRooms(); break;
			case "creditos": GoCredits(); break;
			case "jogar": Go(new PlayScreen()); break;
			case "fotos": GoPhotos(); break;
			case "carregando":
				// A tela de carregamento, com uma sala de treino cheia de bots.
				GoHome();
				RoomListing loading = RoomDirectory.Create(new RoomSettings { Title = "Sala do Jhon", Map = focus ?? "neon" });
				RoomConfig.Active = true;
				RoomConfig.Settings = loading.Settings.Clone();
				RoomConfig.Members.Clear();
				RoomConfig.Members.Add(new RoomMember { Name = Profile.PlayerName, Team = Teams.Blue, IsHost = true, Rank = Profile.Rank });
				foreach (var (name, team) in RoomConfig.DefaultBots())
					RoomConfig.Members.Add(new RoomMember { Name = name, Team = team, IsBot = true });
				LoadingScreen.Open(GetTree());
				break;
			default: GoHome(); break;
		}
		if (screen != "entrada") _ = EnsureLink();
		// --social=aberto|fechado, para fotografar o painel dos dois jeitos.
		string social = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--social="))?["--social=".Length..];
		if (social != null) { _socialChosen = social == "aberto"; _social.Open = _socialChosen.Value; }
		if (OS.GetCmdlineUserArgs().Contains("--buscar")) _search.Begin();   // a faixa da busca, para fotografar (o cartão está trancado)
		if (OS.GetCmdlineUserArgs().Contains("--amigos-de-mentira")) FakeFriends();

		Photograph();
		return true;
	}

	/// <summary>Uma lista de amigos de mentira, para fotografar o painel
	/// social sem servidor (<c>--amigos-de-mentira</c>): chega um pouco
	/// depois, por cima do que o servidor mandar.</summary>
	private async void FakeFriends()
	{
		await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
		const string json = """
			{"t":"friends","friends":[
			{"id":101,"name":"Lupina","xp":52000,"photo":"lua_cheia","status":"room","room":12},
			{"id":102,"name":"Barut_BR","xp":9100,"photo":"corvo","status":"online","room":null},
			{"id":103,"name":"NoiteFria","xp":310000,"photo":"coruja_piloto","status":"playing","room":7},
			{"id":104,"name":"Garra","xp":700,"photo":"filhote","status":"online","room":null},
			{"id":105,"name":"Sombra","xp":1500,"photo":"lobo_cinza","status":"offline","room":null},
			{"id":106,"name":"Vento","xp":40,"photo":"lobo_cinza","status":"offline","room":null}],
			"requests":[{"id":201,"name":"Uivante","xp":2400,"photo":"panda_vermelho"}],
			"sent":[{"id":301,"name":"Presa","xp":100,"photo":"geleia"}]}
			""";
		double now = Time.GetTicksMsec() / 1000.0;
		Social.Apply((System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(json), now);
		Social.Apply((System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(
			"""{"t":"friend.invited","from":{"id":101,"name":"Lupina","xp":52000,"photo":"lua_cheia"},"room":{"id":12,"title":"Sala da Lupina","map":"neon","mode":0,"players":5,"capacity":12}}"""), now);
	}

	/// <summary>
	/// <c>-- --tela=perfil --foto=/caminho.png</c>: espera a tela assentar
	/// (<c>--foto-apos=segundos</c>, 2 por padrão), grava a imagem e fecha o
	/// jogo. Para conferir uma tela sem clicar, com a janela atrás
	/// (<c>open -g</c>).
	/// </summary>
	private async void Photograph()
	{
		string[] args = OS.GetCmdlineUserArgs();
		string path = args.FirstOrDefault(a => a.StartsWith("--foto="))?["--foto=".Length..];
		if (path == null) return;
		string after = args.FirstOrDefault(a => a.StartsWith("--foto-apos="))?["--foto-apos=".Length..];
		double wait = double.TryParse(after, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double w) ? w : 2.0;

		await ToSignal(GetTree().CreateTimer(wait), SceneTreeTimer.SignalName.Timeout);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(path);
		GetTree().Quit();
	}
}
