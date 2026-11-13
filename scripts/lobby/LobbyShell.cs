using Godot;
using System;
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
	}

	public override void _Ready()
	{
		UiTheme.Apply(this);
		UiKit.Fill(this);

		// Qualquer contagem de menu herdada da partida acaba aqui, e o
		// cursor é sempre do lobby.
		UiState.Reset();
		Input.MouseMode = Input.MouseModeEnum.Visible;
		InputSetup.EnsureActions();
		Settings.ApplyAudio();

		AddChild(UiKit.Fill(new ColorRect { Color = UiTokens.Bg, MouseFilter = MouseFilterEnum.Ignore }));

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

		Sfx.Music(this, "musica_menu", -10f);

		GoStart();
	}

	/// <summary>A primeira tela: entrada se ainda não há nome; a sala se
	/// voltamos de uma partida; senão, a lista de salas.</summary>
	private void GoStart()
	{
		if (DevScreen()) return;

		if (!Profile.HasName)
		{
			Go(new WelcomeScreen());
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
			GoRooms();
	}

	// ------------------------------------------------------------------ navegação

	public void Go(LobbyScreen screen)
	{
		if (Current != null)
		{
			_content.RemoveChild(Current);
			Current.QueueFree();
		}

		Current = screen;
		_content.AddChild(screen);
		_header.Visible = screen is not WelcomeScreen;
		_header.SetActive(screen.NavIcon);
		Motion.FadeIn(screen, UiTokens.Normal);
	}

	public void GoRooms() => Go(new RoomListScreen());
	public void GoRoom(RoomListing room) => Go(new RoomScreen(room));
	public void GoShop(string focus = null) => Go(new ShopScreen(focus));
	public void GoInventory(string focus = null) => Go(new InventoryScreen(focus));
	public void GoProfile() => Go(new ProfileScreen());
	public void GoMissions() => Go(new MissionsScreen());
	public void GoOptions() => Go(new OptionsScreen());
	public void GoOnline() => Go(new OnlineScreen());
	public void GoCredits() => Go(new CreditsScreen());

	/// <summary>Sair da sala volta à lista; o resto das abas volta para
	/// onde o jogador estava — a sala, se estiver numa.</summary>
	public void GoHome()
	{
		if (RoomDirectory.Current != null) GoRoom(RoomDirectory.Current);
		else GoRooms();
	}

	// ------------------------------------------------------------------ avisos

	public void Toast(string text, Color? color = null) => global::Toast.Push(_overlay, text, color);

	public Modal Modal(string title, Control body, params Modal.Choice[] choices) =>
		global::Modal.Open(_overlay, title, body, choices);

	public Control Overlay => _overlay;

	// ------------------------------------------------------------------ desenvolvimento

	/// <summary>
	/// Abre uma tela direto pela linha de comando, para testar e
	/// fotografar sem clicar: <c>-- --tela=loja</c>. Telas: entrada,
	/// salas, sala, loja, inventario, perfil, missoes, opcoes, online, creditos.
	/// </summary>
	private bool DevScreen()
	{
		string arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--tela="));
		if (arg == null) return false;

		string screen = arg["--tela=".Length..];
		string focus = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--foco="))?["--foco=".Length..];
		if (!Profile.HasName && screen != "entrada") Profile.SetName("Jhon", out _);

		switch (screen)
		{
			case "entrada": Go(new WelcomeScreen()); break;
			case "sala": GoRoom(RoomDirectory.Current ?? RoomDirectory.Create(new RoomSettings { Title = "Sala do Jhon" })); break;
			case "loja": GoShop(focus); break;
			case "inventario": GoInventory(focus); break;
			case "perfil": GoProfile(); break;
			case "missoes": GoMissions(); break;
			case "opcoes": GoOptions(); break;
			case "online": GoOnline(); break;
			case "creditos": GoCredits(); break;
			default: GoRooms(); break;
		}

		return true;
	}
}
