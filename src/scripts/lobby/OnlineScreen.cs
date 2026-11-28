using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// As salas online: ver as que estão abertas, entrar numa, abrir a sua e
/// conversar no saguão. É a terceira porta da tela JOGAR.
///
/// Refeita em 27/11 (Jhon: "refaça a HUD dos menus e da sala"): a lista é
/// uma tabela — número, título, mapa, modo, objetivo, lugares, estado —,
/// uma linha por sala, com a foto do mapa na ponta; a linha acende com o
/// mouse e o botão de entrar aparece nela. À direita, a conversa do
/// saguão. O ranking saiu (os amigos estão no painel social, na moldura) e
/// as missões do dia foram para o início.
///
/// O painel da conversa fica parado enquanto a lista se refaz a cada
/// notícia do servidor, para não apagar o que o jogador está digitando.
///
/// Quem fala com o servidor é o <see cref="LobbyLink"/> (salas e chat, ao
/// vivo); quem abre a ligação é a moldura (<see cref="LobbyShell.EnsureLink"/>).
/// </summary>
public partial class OnlineScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Rooms;

	private static readonly int[] Scores = { 10, 20, 30, 40, 60, 80, 100 };
	private static readonly int[] Durations = { 5, 10, 15, 20, 25, 30 };

	private VBoxContainer _rows;
	private Label _count;
	private VBoxContainer _chatLog;
	private ScrollContainer _chatScroll;
	private string _status = "";
	private bool _connecting, _shown;

	private static LobbyLink Lobby => LobbyLink.Instance;

	public override void _Ready()
	{
		base._Ready();
		Lobby.Changed += OnLobbyChanged;
		Lobby.ChatReceived += OnChat;

		if (OnlineMatch.Notice != null)
		{
			Shell.Toast(OnlineMatch.Notice, UiTokens.Warning);
			OnlineMatch.Notice = null;
		}
	}

	public override void _ExitTree()
	{
		Lobby.Changed -= OnLobbyChanged;
		Lobby.ChatReceived -= OnChat;
	}

	protected override void Build()
	{
		_rows = null;
		_chatLog = null;

		// Sem sessão (saiu, venceu): o login é a primeira tela do jogo.
		if (!Backend.LoggedIn)
		{
			Callable.From(() => Shell.GoLogin(_status.Length > 0 ? _status : null)).CallDeferred();
			return;
		}

		if (!Lobby.Connected)
		{
			BuildConnecting();
			if (!_connecting) Connect();
			return;
		}

		// Já numa sala (voltou de uma partida, abriu a lista por um atalho):
		// a tela é a da sala.
		if (Lobby.Room != null)
		{
			Callable.From(() => { if (Shell.Current == this) Shell.GoOnlineRoom(); }).CallDeferred();
			return;
		}

		VBoxContainer page = Page(UiTokens.S3);
		Control head = Head();
		page.AddChild(head);

		HBoxContainer split = UiKit.Row(UiTokens.S4);
		split.SizeFlagsVertical = SizeFlags.ExpandFill;
		page.AddChild(split);

		Control table = Table();
		split.AddChild(table);
		Control chat = ChatCard();
		split.AddChild(chat);

		FillRooms();
		Lobby.RefreshRooms();
		_ = ServerData.Refresh();

		if (_shown) return;
		_shown = true;
		Motion.Rise(head, 0f, new Vector2(-24, 0));
		Motion.Rise(table, 0.06f);
		Motion.Rise(chat, 0.12f, new Vector2(28, 0));
	}

	// ------------------------------------------------------------------ a ligação

	private void BuildConnecting()
	{
		var center = UiKit.Fill(new CenterContainer());
		AddChild(center);

		VBoxContainer column = UiKit.Column(UiTokens.S3);
		column.CustomMinimumSize = new Vector2(460, 0);
		center.AddChild(column);

		if (_connecting)
		{
			column.AddChild(Loader.Make(72));
			column.AddChild(UiKit.Gap(UiTokens.S2));
			Label title = Deco.Big("Conectando", 34);
			title.HorizontalAlignment = HorizontalAlignment.Center;
			column.AddChild(title);
			Label line = UiKit.Paragraph(Loc.F("Entrando como {0}.", Backend.AccountName));
			line.HorizontalAlignment = HorizontalAlignment.Center;
			column.AddChild(line);
			return;
		}

		Label failed = Deco.Big("Sem ligação com o servidor", 34);
		failed.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(failed);
		Label why = UiKit.Paragraph(_status.Length > 0 ? _status : Loc.F("Não consegui falar com {0}.", Backend.Url));
		why.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(why);

		HBoxContainer buttons = UiKit.Row(UiTokens.S2);
		buttons.Alignment = BoxContainer.AlignmentMode.Center;
		buttons.AddChild(CtaButton.Make("Sair da conta", CtaKind.Ghost, () => Shell.Logout(), 44, 17));
		buttons.AddChild(CtaButton.Make("Tentar de novo", CtaKind.Paper, Connect, 44, 17));
		column.AddChild(buttons);
	}

	private async void Connect()
	{
		_connecting = true;
		_status = "";
		Refresh();

		string problem = await Shell.EnsureLink();
		_connecting = false;
		_status = problem ?? "";
		if (Alive) Refresh();
	}

	private bool Alive => IsInstanceValid(this) && IsInsideTree();

	private void OnLobbyChanged()
	{
		if (!Alive) return;

		// Caiu a ligação (ou voltou): a tela inteira muda de estado. Entrar
		// numa sala quem trata é a moldura.
		if (_rows == null || !Lobby.Connected) Refresh();
		else if (Lobby.Room == null) Callable.From(FillRooms).CallDeferred();
	}

	// ------------------------------------------------------------------ a cabeça

	private Control Head()
	{
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		row.AddChild(Deco.Title("Salas", "Jogar"));

		_count = UiKit.Text("", "Caption", UiTokens.TextSecondary);
		_count.SizeFlagsVertical = SizeFlags.ShrinkEnd;
		var countPad = UiKit.Margin(UiTokens.S2, 0, 0, UiTokens.S2);
		countPad.AddChild(_count);
		row.AddChild(countPad);
		row.AddChild(UiKit.Spacer());

		HBoxContainer buttons = UiKit.Row(UiTokens.S2);
		buttons.SizeFlagsVertical = SizeFlags.ShrinkEnd;
		buttons.AddChild(CtaButton.Make("Voltar", CtaKind.Ghost, () => Shell.GoPlay(), 44, 17));
		buttons.AddChild(CtaButton.Make("Atualizar", CtaKind.Ghost, Lobby.RefreshRooms, 44, 17));
		CtaButton create = CtaButton.Make("Criar sala", CtaKind.Gold, AskNewRoom, 44, 19);
		create.Arrow = true;
		buttons.AddChild(create);
		row.AddChild(buttons);
		return row;
	}

	// ------------------------------------------------------------------ a tabela

	// As colunas: o nome e a largura (0: o que sobrar).
	private static readonly (string Name, int Width)[] Columns =
	{
		("#", 56), ("Sala", 0), ("Mapa", 170), ("Modo", 170), ("Objetivo", 150), ("Lugares", 96), ("Estado", 120), ("", 128)
	};

	private Control Table()
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column, 0, 0);
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		Deco.Ticks(panel);

		// O cabeçalho das colunas.
		var head = new PanelContainer();
		var box = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.28f), BorderColor = UiTokens.Border };
		box.BorderWidthBottom = 1;
		box.ContentMarginLeft = box.ContentMarginRight = UiTokens.S3;
		box.ContentMarginTop = box.ContentMarginBottom = UiTokens.S2;
		head.AddThemeStyleboxOverride("panel", box);
		HBoxContainer names = UiKit.Row(UiTokens.S3);
		head.AddChild(names);
		foreach (var (name, width) in Columns)
		{
			Label label = UiKit.Caption(name);
			label.AddThemeFontSizeOverride("font_size", 12);
			Size(label, width);
			names.AddChild(label);
		}
		column.AddChild(head);

		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		_rows = UiKit.Column(0);
		_rows.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_rows.SizeFlagsVertical = SizeFlags.ExpandFill;
		scroll.AddChild(_rows);
		column.AddChild(scroll);
		return panel;
	}

	private static void Size(Control cell, int width)
	{
		if (width > 0) cell.CustomMinimumSize = new Vector2(width, 0);
		else cell.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		cell.SizeFlagsVertical = SizeFlags.ShrinkCenter;
	}

	private void FillRooms()
	{
		if (!Alive || _rows == null || !IsInstanceValid(_rows)) return;
		UiKit.Clear(_rows);
		_count.Text = Loc.Upper(Loc.F("{0} aberta(s)", Lobby.Rooms.Count));

		if (Lobby.Rooms.Count == 0)
		{
			_rows.AddChild(Empty());
			return;
		}
		foreach (JsonNode room in Lobby.Rooms) _rows.AddChild(RoomLine(room));
	}

	private Control Empty()
	{
		var center = new CenterContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		VBoxContainer box = UiKit.Column(UiTokens.S2);
		box.CustomMinimumSize = new Vector2(440, 0);
		center.AddChild(box);

		Icon icon = Icon.Make(IconKind.Rooms, 44, new Color(UiTokens.Paper, 0.3f));
		icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		box.AddChild(icon);
		Label title = Deco.Big("Nenhuma sala aberta agora", 30);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		box.AddChild(title);
		Label line = UiKit.Paragraph("Crie a sua e chame alguém. Nos modos contra bots, o dono da sala põe os bots que quiser em cada time.");
		line.HorizontalAlignment = HorizontalAlignment.Center;
		box.AddChild(line);
		box.AddChild(UiKit.Gap(UiTokens.S2));
		CtaButton create = CtaButton.Make("Criar sala", CtaKind.Gold, AskNewRoom, 48, 19);
		create.Arrow = true;
		create.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		box.AddChild(create);
		return center;
	}

	private Control RoomLine(JsonNode room)
	{
		RoomSettings s = OnlineMatch.SettingsFrom(room["settings"] as JsonObject);
		bool playing = room["state"]?.GetValue<string>() != "waiting", locked = room["hasPassword"]?.GetValue<bool>() ?? false;
		int players = room["players"].GetValue<int>(), capacity = room["capacity"].GetValue<int>();
		int id = room["id"].GetValue<int>();
		MapInfo map = Maps.Get(s.Map);

		// Os bots contam nos lugares (Jhon, 25/11: "partida com bot deve
		// contabilizar aqui, e não +10 bots").
		int bots = room["bots"]?.GetValue<int>() ?? 0, taken = players + bots;
		bool closing = room["closing"]?.GetValue<bool>() ?? false;
		// "open" é do servidor (com a partida em curso, conta os bots dela, não
		// deixa um time com dois a mais e fecha quando ela está quase
		// acabando); sem ele, servidor antigo.
		bool open = room["open"]?.GetValue<bool>() ?? (!playing && players < capacity);

		var line = new PanelContainer { CustomMinimumSize = new Vector2(0, 60), MouseFilter = MouseFilterEnum.Stop, ClipContents = true };
		StyleBoxFlat Box(Color bg, Color edge)
		{
			var b = new StyleBoxFlat { BgColor = bg, BorderColor = edge };
			b.BorderWidthBottom = 1;
			b.BorderWidthLeft = 2;
			b.ContentMarginLeft = b.ContentMarginRight = UiTokens.S3;
			return b;
		}
		StyleBoxFlat idle = Box(Colors.Transparent, new Color(UiTokens.Paper, 0.07f));
		StyleBoxFlat hot = Box(new Color(UiTokens.Paper, 0.07f), UiTokens.Accent);
		line.AddThemeStyleboxOverride("panel", idle);

		// A foto do mapa, na ponta direita da linha, sumindo para a esquerda.
		Control photo = Deco.Photo(map.Picture, 1f, fromLeft: true, drift: 0f);
		photo.SetAnchorsAndOffsetsPreset(LayoutPreset.RightWide);
		photo.OffsetLeft = -360;
		photo.Modulate = new Color(1, 1, 1, 0.3f);
		var holder = new Control { MouseFilter = MouseFilterEnum.Ignore };
		holder.AddChild(photo);
		line.AddChild(holder);

		HBoxContainer row = UiKit.Row(UiTokens.S3);
		row.MouseFilter = MouseFilterEnum.Ignore;
		line.AddChild(row);

		Label number = UiKit.Text($"{id:000}", "Numeric", UiTokens.TextSecondary);
		Size(number, Columns[0].Width);
		row.AddChild(number);

		HBoxContainer name = UiKit.Row(UiTokens.S2);
		Size(name, 0);
		if (locked) name.AddChild(Icon.Make(IconKind.Lock, 16, UiTokens.Accent));
		Label title = UiKit.Truncated(s.Title, "Subtitle");
		title.AddThemeFontOverride("font", UiFonts.DisplaySemi);
		title.AddThemeFontSizeOverride("font_size", 22);
		name.AddChild(title);
		row.AddChild(name);

		Label Cell(string text, int width, Color? color = null)
		{
			Label cell = UiKit.Truncated(Loc.Upper(text), "Caption", color ?? UiTokens.TextPrimary);
			cell.SizeFlagsHorizontal = SizeFlags.Fill;
			Size(cell, width);
			return cell;
		}
		row.AddChild(Cell(map.Name, Columns[2].Width));
		row.AddChild(Cell(PlayModes.Of(s.Play).Name, Columns[3].Width, UiTokens.TextSecondary));
		row.AddChild(Cell(Loc.F("{0} pts · {1} min", s.ScoreToWin, s.Minutes), Columns[4].Width, UiTokens.TextSecondary));

		Label count = UiKit.Text($"{taken}/{capacity}", "Numeric", !open && taken >= capacity ? UiTokens.Danger : UiTokens.TextPrimary);
		count.AddThemeFontOverride("font", UiFonts.NumericDisplay);
		count.AddThemeFontSizeOverride("font_size", 22);
		count.MouseFilter = MouseFilterEnum.Pass;
		if (bots > 0)
			count.TooltipText = playing
				? Loc.F("{0} jogador(es) e {1} bot(s).", players, bots)
				: Loc.F("{0} jogador(es) e {1} bot(s). Quem entra toma o lugar de um bot.", players, bots);
		Size(count, Columns[5].Width);
		row.AddChild(count);

		var state = new HBoxContainer();
		Size(state, Columns[6].Width);
		state.AddChild(closing
			? Deco.Tag("Acabando", UiTokens.Danger, Colors.White)
			: playing ? Deco.Tag("Jogando", new Color(UiTokens.Warning, 0.9f), UiTokens.AccentText)
			: Deco.Tag("Esperando", new Color(UiTokens.Success, 0.9f), UiTokens.AccentText));
		row.AddChild(state);

		// Sala jogando com vaga: entra na sala para escolher o time, e de lá na
		// partida (Jhon, 25/11: "se a partida tiver iniciado, ele entra na sala
		// para poder escolher o time").
		CtaButton join = CtaButton.Make(open ? "Entrar" : "Cheia", CtaKind.Paper, () => Join(id, locked), 36, 16);
		join.Cut = 8;
		if (closing) join.TooltipText = Loc.T("A partida está quase acabando: espere a próxima");
		join.Disabled = !open;
		join.MinWidth = Columns[7].Width;
		join.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.MouseFilter = MouseFilterEnum.Pass;
		row.AddChild(join);

		line.MouseEntered += () =>
		{
			line.AddThemeStyleboxOverride("panel", hot);
			photo.CreateTween().TweenProperty(photo, "modulate:a", 0.75f, 0.2f);
		};
		line.MouseExited += () =>
		{
			if (line.GetGlobalRect().HasPoint(line.GetGlobalMousePosition())) return;
			line.AddThemeStyleboxOverride("panel", idle);
			photo.CreateTween().TweenProperty(photo, "modulate:a", 0.3f, 0.25f);
		};
		// Dois cliques na linha entram, como em toda lista de salas.
		line.GuiInput += e =>
		{
			if (open && e is InputEventMouseButton { Pressed: true, DoubleClick: true, ButtonIndex: MouseButton.Left }) Join(id, locked);
		};
		return line;
	}

	private void Join(int id, bool locked)
	{
		if (!locked)
		{
			Lobby.JoinRoom(id);
			return;
		}

		LineEdit password = UiKit.Field("", "Senha da sala", MaxPassword);
		password.Secret = true;
		Shell.Modal("Sala com senha", password,
			new Modal.Choice("Cancelar", ButtonKind.Secondary, null),
			new Modal.Choice("Entrar", ButtonKind.Primary, () => Lobby.JoinRoom(id, password.Text)));
		password.GrabFocus();
	}

	private const int MaxPassword = 32;

	/// <summary>
	/// Criar sala: vai direto para a sala, com as regras da última sala
	/// criada (ou as de sempre), e lá o dono muda o que quiser — modo,
	/// mapa, times, pontos, tempo, senha (decisão do Jhon, 18/11: "ao criar
	/// sala não precisa do modal, pode ir direto para a sala").
	/// </summary>
	private void AskNewRoom()
	{
		RoomScreen.PendingPassword = "";
		Lobby.CreateRoom(OnlineMatch.SettingsTo(NewRoom()));
	}

	/// <summary>As regras de uma sala nova: as da última que o jogador
	/// criou, ajustadas ao que vale hoje (mapas em rotação, opções da tela).</summary>
	public static RoomSettings NewRoom()
	{
		RoomSettings s = (Profile.LastRoom ?? new RoomSettings()).Clone();
		if (string.IsNullOrWhiteSpace(s.Title) || s.Title == "Sala de treino") s.Title = Loc.F("Sala de {0}", Backend.AccountName);
		s.Password = "";
		s.TeamSize = Math.Clamp(s.TeamSize, 1, RoomSettings.MaxTeamSize);
		// Mapa que saiu do jogo, ou que o painel tirou da rotação: o primeiro que está valendo.
		if (!Maps.All.Any(m => m.Id == s.Map) || !ServerData.MapOn(s.Map)) s.Map = ServerData.FirstMapOn();
		s.ScoreToWin = Scores[Nearest(Scores, s.ScoreToWin)];
		s.Minutes = Durations[Nearest(Durations, s.Minutes)];
		return s;
	}

	private static int Nearest(int[] options, int value)
	{
		int best = 0;
		for (int i = 1; i < options.Length; i++)
			if (Math.Abs(options[i] - value) < Math.Abs(options[best] - value)) best = i;
		return best;
	}

	// ------------------------------------------------------------------ a conversa

	private Control ChatCard()
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S3, UiTokens.S2);
		panel.CustomMinimumSize = new Vector2(340, 0);

		column.AddChild(Deco.Kicker("Conversa do saguão"));

		_chatScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		_chatLog = UiKit.Column(2);
		_chatLog.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_chatScroll.AddChild(_chatLog);
		column.AddChild(_chatScroll);

		// Só o que se falou na lista: o da sala é da tela da sala.
		foreach (LobbyLink.ChatLine line in Lobby.Chat)
			if (line.Room == 0) _chatLog.AddChild(ChatLine(line.From, line.Text));
		ScrollChat();

		LineEdit input = UiKit.Field("", "Escreva e aperte Enter", 200);
		input.TextSubmitted += text =>
		{
			if (text.Trim().Length == 0) return;
			Lobby.Say(text);
			input.Clear();
		};
		column.AddChild(input);
		return panel;
	}

	private static Control ChatLine(string from, string text)
	{
		Label line = UiKit.Paragraph($"{from}: {text}", null);
		line.AddThemeColorOverride("font_color", from == Backend.AccountName ? UiTokens.Accent : UiTokens.TextPrimary);
		return line;
	}

	private void OnChat(LobbyLink.ChatLine line)
	{
		if (!Alive || _chatLog == null || !IsInstanceValid(_chatLog) || line.Room != 0) return;
		_chatLog.AddChild(ChatLine(line.From, line.Text));
		ScrollChat();
	}

	private async void ScrollChat()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (Alive && _chatScroll != null && IsInstanceValid(_chatScroll))
			_chatScroll.ScrollVertical = (int)_chatScroll.GetVScrollBar().MaxValue;
	}
}
