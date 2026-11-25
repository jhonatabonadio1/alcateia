using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// A sala antes da partida, em três áreas:
///
/// - times à esquerda, Azul e Laranja lado a lado, com um botão de
///   trocar de lado entre os dois. Lugar vazio aparece listrado e
///   clicável; o mestre leva uma coroa; quem está pronto leva as divisas
///   verdes ».
/// - regras à direita, em formulário: rótulo à esquerda, valor à direita,
///   uma linha por regra — o jogador lê as regras de cima a baixo numa
///   varredura só. Embaixo, o mapa.
/// - faixa inferior com o chat, o painel de informações de quem foi
///   clicado e os botões Convidar, Pronto e Iniciar.
///
/// Iniciar abre uma contagem de 5 s num círculo grande sobre os times,
/// cancelável enquanto roda.
///
/// **O mestre não marca pronto**: quem inicia é ele, então o botão de
/// pronto some para ele. Ele inicia quando todos estão prontos — ou
/// quando os prontos já fecham times equilibrados, com no máximo um a
/// menos de um lado. Quem não estava pronto fica na sala e não joga.
///
/// A mesma tela serve à sala online (decisão do Jhon, 17/11: este visual
/// fica): com <see cref="RoomScreen(JsonObject)"/> ela lê a sala que o
/// servidor manda (<see cref="LobbyLink"/>) e cada ação vira um pedido a
/// ele — regras, bots, time, pronto, iniciar, sair e o chat. O servidor
/// confere tudo (só o dono muda regra, por exemplo) e manda a sala de
/// volta; a tela se refaz com o que veio.
///
/// Offline (sem servidor) só há um humano (você), sempre mestre.
/// </summary>
public partial class RoomScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Rooms;

	private const double StartCountdown = 5.0;

	private RoomListing _room;
	private RoomMember _selected;

	private VBoxContainer _chatLog;
	private ScrollContainer _chatScroll;
	private Control _userInfo;
	private Button _ready;
	private Button _start;
	private CountdownRing _ring;
	private double _countdown = -1;

	/// <summary>Histórico do chat da sala; sobrevive à partida.</summary>
	private static readonly Dictionary<int, List<(string Who, string Text, bool System)>> Chats = new();

	public RoomScreen() { }

	/// <summary>A sala online, como o servidor a descreve.</summary>
	public RoomScreen(JsonObject online)
	{
		_online = true;
		_room = FromOnline(online);
	}

	private readonly bool _online;
	private static LobbyLink Lobby => LobbyLink.Instance;

	/// <summary>A senha de cada sala online que o dono pôs nesta sessão: o
	/// servidor não a devolve, e mudar uma regra sem ela tiraria a senha.</summary>
	public static readonly Dictionary<int, string> OnlinePasswords = new();

	/// <summary>A senha escolhida ao criar a sala online, até o servidor dizer o número dela.</summary>
	public static string PendingPassword;

	private static RoomListing FromOnline(JsonObject room)
	{
		RoomSettings settings = OnlineMatch.SettingsFrom(room?["settings"] as JsonObject);
		settings.Number = room?["id"]?.GetValue<int>() ?? 0;
		var listing = new RoomListing
		{
			Settings = settings,
			State = room?["state"]?.GetValue<string>() == "waiting" ? RoomState.Waiting : RoomState.Playing,
			Closing = room?["closing"]?.GetValue<bool>() ?? false
		};
		foreach (JsonNode t in room?["enter"] as JsonArray ?? new JsonArray()) listing.EnterTeams.Add(t.GetValue<int>());

		foreach (JsonNode m in room?["members"] as JsonArray ?? new JsonArray())
		{
			bool bot = m["bot"]?.GetValue<bool>() ?? false;
			bool host = m["host"]?.GetValue<bool>() ?? false;
			long id = m["id"].GetValue<long>();
			listing.Members.Add(new RoomMember
			{
				Id = id,
				Name = m["name"]?.GetValue<string>() ?? "",
				Team = m["team"]?.GetValue<int>() ?? Teams.Blue,
				IsBot = bot,
				Ready = bot || (m["ready"]?.GetValue<bool>() ?? false),
				IsHost = host,
				InMatch = bot || (m["inMatch"]?.GetValue<bool>() ?? false),
				Rank = bot ? Ranks.None : Ranks.RankFor(m["xp"]?.GetValue<long>() ?? 0),
				Photo = bot ? null : m["photo"]?.GetValue<string>()
			});
			if (host && id == Backend.AccountId && PendingPassword != null)
			{
				OnlinePasswords[settings.Number] = PendingPassword;
				PendingPassword = null;
			}
		}

		settings.Password = OnlinePasswords.TryGetValue(settings.Number, out string password) ? password : "";
		return listing;
	}

	public override void _Ready()
	{
		base._Ready();
		if (!_online) return;
		Lobby.Changed += OnLobbyChanged;
		Lobby.ChatReceived += OnOnlineChat;
		Lobby.Failed += OnOnlineFailed;
	}

	public override void _ExitTree()
	{
		if (!_online) return;
		Lobby.Changed -= OnLobbyChanged;
		Lobby.ChatReceived -= OnOnlineChat;
		Lobby.Failed -= OnOnlineFailed;
	}

	private void OnLobbyChanged()
	{
		if (!IsInsideTree()) return;
		if (Lobby.Room == null || !Lobby.Connected)
		{
			Callable.From(() => Shell.GoOnline()).CallDeferred();
			return;
		}

		long selected = _selected?.Id ?? 0;
		_room = FromOnline(Lobby.Room);
		_selected = _room.Members.FirstOrDefault(m => m.Id == selected) ?? Me;
		Refresh();
	}

	private void OnOnlineChat(string from, string text)
	{
		if (!IsInsideTree() || _chatLog == null || !IsInstanceValid(_chatLog)) return;
		AddChatLine(from, text, false);
		ScrollToEnd();
	}

	private void OnOnlineFailed(string code, string message)
	{
		if (IsInsideTree()) Shell.Toast(message, UiTokens.Danger);
	}

	/// <summary>Manda as regras da tela para o servidor (só o dono pode).</summary>
	private void PushSettings() => Lobby.ChangeSettings(OnlineMatch.SettingsTo(S));

	public RoomScreen(RoomListing room)
	{
		_room = room;

		// Voltando de uma partida: a sala espera de novo, e ninguém está
		// pronto — prontidão vale para uma partida, não para a sala.
		// Times de 1 a 6: sala antiga (de 8) cabe no máximo de agora.
		if (_room != null) _room.Settings.TeamSize = Mathf.Clamp(_room.Settings.TeamSize, 1, RoomSettings.MaxTeamSize);

		if (_room != null && _room.State == RoomState.Playing)
		{
			_room.State = RoomState.Waiting;
			foreach (RoomMember m in _room.Members.Where(m => !m.IsBot)) m.Ready = false;
		}
	}

	private RoomSettings S => _room.Settings;
	private bool VsBots => PlayModes.VsBots(S.Play);
	private RoomMember Me => _online
		? _room.Members.FirstOrDefault(m => !m.IsBot && m.Id == Backend.AccountId)
		: _room.Members.FirstOrDefault(m => !m.IsBot);
	private bool IsHost => Me?.IsHost == true;

	/// <summary>Online, a partida da sala está em curso e eu estou de fora dela:
	/// entrei na sala depois (Jhon, 25/11: "se a partida tiver iniciado, ele
	/// entra na sala para poder escolher o time"). Escolho o lado e entro.</summary>
	private bool Outside => _online && _room.State == RoomState.Playing && Me != null && !Me.InMatch;

	/// <summary>Com a partida em curso, o que o time mostra: quem está jogando.</summary>
	private int Shown(int team) => _online && _room.State == RoomState.Playing
		? _room.Members.Count(m => m.Team == team && m.InMatch)
		: _room.Members.Count(m => m.Team == team);

	/// <summary>Quem entra na partida: o mestre, que não marca pronto, e
	/// quem está pronto.</summary>
	private List<RoomMember> Playing => _room.Members.Where(m => m.IsHost || m.Ready).ToList();

	private bool AllReady => _room.Members.All(m => m.IsHost || m.Ready);

	// ------------------------------------------------------------------ montagem

	protected override void Build()
	{
		if (_room == null)
		{
			Shell.GoOnline();
			return;
		}

		_selected ??= Me;
		if (!Chats.ContainsKey(S.Number))
		{
			Chats[S.Number] = new List<(string, string, bool)>();
			Log(Loc.F("{0} entrou na sala.", Profile.PlayerName), system: true);
		}

		VBoxContainer page = Page(UiTokens.S3);

		page.AddChild(Top());

		HBoxContainer middle = UiKit.Row(UiTokens.S4);
		middle.SizeFlagsVertical = SizeFlags.ExpandFill;
		page.AddChild(middle);

		middle.AddChild(TeamsArea());
		middle.AddChild(RulesPanel());

		page.AddChild(Bottom());

		RefreshActions();
	}

	private Control Top()
	{
		HBoxContainer row = UiKit.Row(UiTokens.S3);

		Label room = UiKit.Text("SALA", "Title");
		room.AddThemeFontOverride("font", UiFonts.Display);
		row.AddChild(room);

		Label number = UiKit.Text($"#{S.Number}", "Title", UiTokens.Accent);
		number.AddThemeFontOverride("font", UiFonts.NumericDisplay);
		row.AddChild(number);

		Label title = UiKit.Truncated(S.Title, "Subtitle", UiTokens.TextSecondary);
		title.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(title);

		Label channel = UiKit.Caption(_online ? "Online" : "Canal local");
		channel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(channel);

		row.AddChild(UiKit.Button("Sair da sala", ButtonKind.Tertiary, Leave));
		return row;
	}

	// ------------------------------------------------------------------ times

	private Control TeamsArea()
	{
		var holder = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };

		HBoxContainer teams = UiKit.Fill(UiKit.Row(UiTokens.S3));
		holder.AddChild(teams);

		teams.AddChild(TeamColumn(Teams.Blue));

		VBoxContainer swap = UiKit.Column(UiTokens.S2);
		swap.Alignment = BoxContainer.AlignmentMode.Center;
		Button swapButton = UiKit.IconButton(IconKind.Refresh, "Trocar de lado", SwapSide, 22);
		swap.AddChild(swapButton);
		teams.AddChild(swap);

		teams.AddChild(TeamColumn(Teams.Orange));

		// A contagem fica sobre os lugares: espaço morto, e impossível de
		// não ver.
		var center = UiKit.Fill(new CenterContainer { MouseFilter = MouseFilterEnum.Ignore });
		_ring = new CountdownRing { Total = StartCountdown, Caption = "A PARTIDA VAI COMEÇAR", Visible = _countdown >= 0 };
		_ring.CustomMinimumSize = new Vector2(220, 220);
		center.AddChild(_ring);
		holder.AddChild(center);

		return holder;
	}

	private Control TeamColumn(int team)
	{
		VBoxContainer column = UiKit.Column(UiTokens.S1 + 2);
		column.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		List<RoomMember> members = _room.Members.Where(m => m.Team == team).ToList();
		Color color = Teams.ColorOf(team);

		// Cabeçalho grande na cor do time, com a contagem ao lado.
		PanelContainer head = UiKit.Panel("Bare");
		StyleBoxFlat headStyle = UiTheme.Box(new Color(color, 0.1f), color, 0, 0, UiTokens.S3, UiTokens.S2);
		headStyle.BorderWidthBottom = 2;
		head.AddThemeStyleboxOverride("panel", headStyle);
		HBoxContainer headRow = UiKit.Row(UiTokens.S2);
		head.AddChild(headRow);

		Label name = UiKit.Text(Teams.NameOf(team), "Subtitle", color);
		name.AddThemeFontOverride("font", UiFonts.Display);
		name.AddThemeFontSizeOverride("font_size", 26);
		headRow.AddChild(name);
		headRow.AddChild(UiKit.Text($"{Shown(team)}/{S.TeamSize}", "Numeric", UiTokens.TextSecondary));
		headRow.AddChild(UiKit.Spacer());

		// Contra bots, o mestre põe quantos bots quiser em cada time.
		if (VsBots && IsHost && members.Count < S.TeamSize)
		{
			Button fill = UiKit.Button("Encher com bots", ButtonKind.Tertiary, () => AddBot(team, S.TeamSize));
			fill.CustomMinimumSize = new Vector2(0, 28);
			headRow.AddChild(fill);
		}

		column.AddChild(head);

		for (int i = 0; i < S.TeamSize; i++)
			column.AddChild(i < members.Count ? Slot(members[i]) : EmptySlot(team));

		return column;
	}

	private Control Slot(RoomMember member)
	{
		bool selected = member == _selected;
		var slot = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0, 40),
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.PointingHand
		};
		Color team = Teams.ColorOf(member.Team);
		StyleBoxFlat style = UiTheme.Box(selected ? UiTokens.Surface3 : UiTokens.Surface2,
			selected ? new Color(team, 0.7f) : UiTokens.Border, 1, UiTokens.RadiusSmall, UiTokens.S2, 0);
		style.BorderWidthLeft = 3;
		style.BorderColor = selected ? team : new Color(team, 0.5f);
		slot.AddThemeStyleboxOverride("panel", style);
		slot.GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			{
				_selected = member;
				Refresh();
			}
		};

		HBoxContainer row = UiKit.Row(UiTokens.S2);
		row.MouseFilter = MouseFilterEnum.Ignore;
		slot.AddChild(row);

		row.AddChild(PhotoTile.Make(PhotoOf(member), 30));

		Icon crown = Icon.Make(IconKind.Crown, 16, member.IsHost ? UiTokens.Accent : new Color(0, 0, 0, 0));
		row.AddChild(crown);

		// O nível como no cabeçalho ("32 ★"), na frente do nome. Bot não tem.
		if (!member.IsBot)
		{
			LevelChip level = LevelChip.Make(member.Rank, 18);
			level.MouseFilter = MouseFilterEnum.Ignore;
			row.AddChild(level);
		}

		Label name = UiKit.Truncated(member.Name, null, member.IsBot ? UiTokens.TextSecondary : UiTokens.TextPrimary);
		name.AddThemeFontOverride("font", member.IsBot ? UiFonts.Medium : UiFonts.SemiBold);
		row.AddChild(name);

		if (member.IsBot) row.AddChild(UiKit.Chip("bot", UiTokens.TextDisabled));

		// Com a partida em curso, o que conta é quem está nela e quem ainda
		// está na sala, escolhendo o lado.
		if (_online && _room.State == RoomState.Playing && !member.IsBot)
			row.AddChild(member.InMatch
				? UiKit.Text("jogando", "Caption", UiTokens.Warning)
				: UiKit.Text("na sala", "Caption", UiTokens.TextDisabled));
		// Pronto: divisas verdes. Quem não está pronto simplesmente não
		// tem as divisas — e o texto diz que está aguardando. O mestre não
		// marca pronto: a coroa já diz que é ele quem inicia.
		else if (member.Ready)
			row.AddChild(Icon.Make(IconKind.Chevrons, 18, UiTokens.Success));
		else if (!member.IsHost)
			row.AddChild(UiKit.Text("aguardando", "Caption", UiTokens.TextDisabled));

		row.AddChild(PingBars.Make(0));

		if (VsBots && IsHost && member.IsBot)
		{
			Button remove = UiKit.IconButton(IconKind.Close, Loc.F("Tirar {0}", member.Name), () => RemoveBot(member), 14);
			remove.CustomMinimumSize = new Vector2(28, 28);
			row.AddChild(remove);
		}
		else
		{
			row.AddChild(UiKit.Gap(28));
		}
		return slot;
	}

	private Control EmptySlot(int team)
	{
		var slot = new PanelContainer { CustomMinimumSize = new Vector2(0, 40) };
		slot.AddThemeStyleboxOverride("panel", UiTheme.Box(Colors.Transparent, UiTokens.Border, 1, UiTokens.RadiusSmall, 0, 0));
		slot.AddChild(new Striped());

		// Contra bots, o mestre põe um bot na vaga. Contra jogadores não há
		// bot: a vaga é de quem entrar.
		if (VsBots && IsHost)
		{
			Button add = UiKit.Button("+  Adicionar bot", ButtonKind.Tertiary, () => AddBot(team, 1));
			add.CustomMinimumSize = new Vector2(0, 38);
			add.Alignment = HorizontalAlignment.Left;
			slot.AddChild(add);
			return slot;
		}

		MarginContainer margin = UiKit.Margin(UiTokens.S3, 0, UiTokens.S2, 0);
		Label free = UiKit.Text("vaga", "Caption", UiTokens.TextDisabled);
		free.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		margin.AddChild(free);
		slot.AddChild(margin);
		return slot;
	}

	// ------------------------------------------------------------------ regras

	private Control RulesPanel()
	{
		PanelContainer panel = UiKit.Panel("Surface1");
		panel.CustomMinimumSize = new Vector2(500, 0);

		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		panel.AddChild(scroll);

		MarginContainer margin = UiKit.Margin(UiTokens.S4, UiTokens.S2, UiTokens.S4, UiTokens.S2);
		margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(margin);

		VBoxContainer form = UiKit.Column(UiTokens.S1);
		margin.AddChild(form);

		HBoxContainer head = UiKit.Row(UiTokens.S2);
		head.AddChild(UiKit.Caption("Regras da sala"));
		head.AddChild(UiKit.Spacer());
		if (!IsHost) head.AddChild(UiKit.Text("só o mestre altera", "Caption"));
		form.AddChild(head);

		LineEdit title = UiKit.Field(S.Title, "Título", 24);
		title.TextSubmitted += _ => SetTitle(title.Text);
		title.FocusExited += () => SetTitle(title.Text);
		form.AddChild(Row("Título", title));

		LineEdit pass = UiKit.Field(S.Password, "Sem senha", 8);
		pass.Secret = true;
		pass.TooltipText = "4 a 8 caracteres. Vale quando houver multijogador.";
		pass.TextSubmitted += _ => SetPassword(pass.Text);
		pass.FocusExited += () => SetPassword(pass.Text);
		form.AddChild(Row("Senha", pass));

		// O modo de jogo. Offline (sem outros jogadores) só há os contra bots.
		PlayModes.Info[] modes = _online ? PlayModes.All : PlayModes.VersusBots;
		OptionButton mode = Picker(modes.Select(m => m.Name).ToArray(), Mathf.Max(0, Array.FindIndex(modes, m => m.Mode == S.Play)),
			i => { Change("Modo", modes[i].Name, () => S.Play = modes[i].Mode); Refresh(); });
		mode.TooltipText = PlayModes.Of(S.Play).Line;
		form.AddChild(Row("Modo", mode));

		int[] sizes = RoomSettings.TeamSizes;
		form.AddChild(Row("Jogadores", Picker(sizes.Select(n => Loc.F("{0} contra {1}", n, n)).ToArray(),
			Mathf.Max(0, Array.IndexOf(sizes, S.TeamSize)), i => SetTeamSize(sizes[i]))));

		int[] scores = { 20, 40, 60, 100 };
		form.AddChild(Row("Objetivo", Picker(scores.Select(n => Loc.F("{0} pontos", n)).ToArray(),
			Mathf.Max(0, Array.IndexOf(scores, S.ScoreToWin)), i => Change("Objetivo", Loc.F("{0} pontos", scores[i]), () => S.ScoreToWin = scores[i]))));

		// Até 30 minutos (decisão do Jhon, 18/11).
		int[] minutes = { 5, 10, 15, 20, 25, 30 };
		form.AddChild(Row("Tempo", Picker(minutes.Select(n => Loc.F("{0} min", n)).ToArray(),
			Mathf.Max(0, Array.IndexOf(minutes, S.Minutes)), i => Change("Tempo", Loc.F("{0} min", minutes[i]), () => S.Minutes = minutes[i]))));

		form.AddChild(Row("Armas", Picker(RoomSettings.WeaponNames.Select((n, i) => RoomSettings.WeaponsAvailable((WeaponRule)i) ? n : Loc.F("{0} (em breve)", n)).ToArray(),
			(int)S.Weapons, i => Change("Armas", RoomSettings.WeaponNames[i], () => S.Weapons = (WeaponRule)i),
			RoomSettings.WeaponNames.Select((_, i) => !RoomSettings.WeaponsAvailable((WeaponRule)i)).ToArray())));

		form.AddChild(Row("Bots", Picker(new[] { "Fáceis", "Médios", "Difíceis" }, (int)S.Difficulty, SetDifficulty)));

		// Níveis de 1 a 3000: o filtro anda em degraus (o começo de cada
		// metal e algumas paradas no Iniciante), não nível a nível.
		string[] stepNames = LevelSteps.Select(r => Loc.F("Nível {0}", Ranks.LevelOf(r))).ToArray();
		HBoxContainer range = UiKit.Row(UiTokens.S2);
		OptionButton min = Picker(stepNames, StepOf(S.MinRank), i => SetRankRange(LevelSteps[i], S.MaxRank));
		OptionButton max = Picker(stepNames, StepOf(S.MaxRank), i => SetRankRange(S.MinRank, LevelSteps[i]));
		range.AddChild(min);
		range.AddChild(UiKit.Text("a", "Muted"));
		range.AddChild(max);
		range.TooltipText = "Vale quando houver multijogador.";
		form.AddChild(Row("Nível", range));

		HBoxContainer checks = UiKit.Row(UiTokens.S3);
		checks.AddChild(Check("Fogo amigo", S.FriendlyFire, v => Change("Fogo amigo", v ? "ligado" : "desligado", () => S.FriendlyFire = v)));
		CheckBox swapSides = Check("Trocar de lado", S.SwapSides, v => Change("Troca de lado", v ? "ligada" : "desligada", () => S.SwapSides = v));
		swapSides.Disabled = true;
		swapSides.TooltipText = "Em breve: a partida ainda não tem rodadas.";
		checks.AddChild(swapSides);
		CheckBox spectators = Check("Espectador", S.Spectators, v => Change("Espectador", v ? "permitido" : "proibido", () => S.Spectators = v));
		spectators.TooltipText = "Vale quando houver multijogador.";
		checks.AddChild(spectators);
		form.AddChild(checks);

		form.AddChild(UiKit.Divider());
		form.AddChild(MapBlock());

		return panel;
	}

	private Control MapBlock()
	{
		MapInfo map = Maps.Get(S.Map);
		VBoxContainer block = UiKit.Column(UiTokens.S2);

		HBoxContainer head = UiKit.Row(UiTokens.S2);
		head.AddChild(UiKit.Caption("Mapa"));
		head.AddChild(UiKit.Spacer());
		Button change = UiKit.Button("Trocar", ButtonKind.Tertiary, PickMap);
		change.CustomMinimumSize = new Vector2(0, 28);
		change.Disabled = !IsHost;
		change.TooltipText = IsHost ? "" : "Só o mestre troca o mapa";
		head.AddChild(change);
		block.AddChild(head);

		// Miniatura ao lado do texto: o formulário inteiro cabe sem
		// rolar, e a miniatura vale mais que o nome.
		HBoxContainer body = UiKit.Row(UiTokens.S3);
		var frame = new PanelContainer();
		frame.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Bg, UiTokens.Border, 1, UiTokens.RadiusSmall, 0, 0));
		TextureRect thumb = UiKit.Thumbnail(map.Picture, new Vector2(184, 104));
		frame.AddChild(thumb);
		body.AddChild(frame);

		VBoxContainer text = UiKit.Column(UiTokens.S1);
		text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		text.AddChild(UiKit.Text(map.Name, "Subtitle"));
		Label description = UiKit.Paragraph(map.Description, "Muted");
		description.AddThemeFontSizeOverride("font_size", UiTokens.Caption);
		text.AddChild(description);
		body.AddChild(text);
		block.AddChild(body);
		return block;
	}

	/// <summary>A escolha do mapa: um cartão por mapa, com a miniatura,
	/// o tamanho e a descrição. O atual vem marcado.</summary>
	private void PickMap()
	{
		if (!IsHost) return;

		Modal modal = null;
		GridContainer grid = new() { Columns = 2 };
		grid.AddThemeConstantOverride("h_separation", UiTokens.S3);
		grid.AddThemeConstantOverride("v_separation", UiTokens.S3);

		// Mapa que o painel tirou da rotação não aparece (o da sala, sim: é o que está marcado).
		foreach (MapInfo map in Maps.All.Where(m => ServerData.MapOn(m.Id) || m.Id == S.Map))
		{
			MapInfo chosen = map;
			bool current = map.Id == S.Map;
			var card = new Button
			{
				CustomMinimumSize = new Vector2(300, 0),
				ToggleMode = true,
				ButtonPressed = current,
				FocusMode = FocusModeEnum.All,
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			card.AddThemeStyleboxOverride("normal", UiTheme.Box(UiTokens.Surface2, current ? UiTokens.Accent : UiTokens.Border, current ? 2 : 1, UiTokens.RadiusPanel, UiTokens.S2, UiTokens.S2));
			card.AddThemeStyleboxOverride("hover", UiTheme.Box(UiTokens.Surface3, UiTokens.Accent, 1, UiTokens.RadiusPanel, UiTokens.S2, UiTokens.S2));
			card.AddThemeStyleboxOverride("pressed", UiTheme.Box(UiTokens.Surface3, UiTokens.Accent, 2, UiTokens.RadiusPanel, UiTokens.S2, UiTokens.S2));

			VBoxContainer inside = UiKit.Column(UiTokens.S1);
			inside.MouseFilter = MouseFilterEnum.Ignore;
			inside.SetAnchorsPreset(LayoutPreset.FullRect);
			inside.OffsetLeft = inside.OffsetTop = UiTokens.S2;
			inside.OffsetRight = inside.OffsetBottom = -UiTokens.S2;
			TextureRect thumb = UiKit.Thumbnail(map.Picture, new Vector2(284, 160));
			thumb.MouseFilter = MouseFilterEnum.Ignore;
			inside.AddChild(thumb);
			HBoxContainer line = UiKit.Row(UiTokens.S2);
			line.MouseFilter = MouseFilterEnum.Ignore;
			line.AddChild(UiKit.Text(map.Name, "Subtitle"));
			line.AddChild(UiKit.Spacer());
			line.AddChild(UiKit.Chip(map.Size, UiTokens.TextSecondary));
			inside.AddChild(line);
			card.CustomMinimumSize = new Vector2(300, 212);
			card.AddChild(inside);
			card.Pressed += () =>
			{
				Change("Mapa", chosen.Name, () => S.Map = chosen.Id);
				modal?.Close();
				Refresh();
			};
			Sfx.Wire(card);
			grid.AddChild(card);
		}

		modal = Shell.Modal("Escolha o mapa", grid, new Modal.Choice("Fechar", ButtonKind.Secondary, null));
	}

	private HBoxContainer Row(string label, Control value)
	{
		HBoxContainer row = UiKit.FormRow(label, value, 130);
		if (!IsHost) SetEditable(value, false);
		return row;
	}

	private static void SetEditable(Control control, bool on)
	{
		if (control is BaseButton b) b.Disabled = !on;
		if (control is LineEdit e) e.Editable = on;
		foreach (Node child in control.GetChildren())
			if (child is Control c) SetEditable(c, on);
	}

	private static OptionButton Picker(string[] labels, int selected, Action<int> chosen, bool[] disabled = null) =>
		UiKit.Picker(labels, selected, chosen, disabled);

	private CheckBox Check(string text, bool on, Action<bool> toggled)
	{
		CheckBox check = UiKit.Check(text, on, toggled);
		check.Disabled = !IsHost;
		return check;
	}

	// ------------------------------------------------------------------ faixa inferior

	private Control Bottom()
	{
		HBoxContainer row = UiKit.Row(UiTokens.S4);
		row.CustomMinimumSize = new Vector2(0, 184);

		row.AddChild(Chat());
		row.AddChild(UserInfo());
		row.AddChild(Actions());
		return row;
	}

	private Control Chat()
	{
		PanelContainer panel = UiKit.Panel("Surface1");
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		MarginContainer margin = UiKit.Margin(UiTokens.S3, UiTokens.S2, UiTokens.S3, UiTokens.S3);
		panel.AddChild(margin);
		VBoxContainer column = UiKit.Column(UiTokens.S2);
		margin.AddChild(column);

		column.AddChild(Tabs.Make(new[] { "Sala" }));

		_chatScroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		_chatLog = UiKit.Column(2);
		_chatLog.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_chatScroll.AddChild(_chatLog);
		column.AddChild(_chatScroll);

		if (_online)
			foreach ((string from, string text) in Lobby.Chat) AddChatLine(from, text, false);
		else
			foreach (var line in Chats[S.Number]) AddChatLine(line.Who, line.Text, line.System);

		HBoxContainer input = UiKit.Row(UiTokens.S2);
		LineEdit field = UiKit.Field("", "Escreva para a sala…", 120);
		field.TextSubmitted += text => { Say(text); field.Clear(); };
		input.AddChild(field);
		input.AddChild(UiKit.Button("Enviar", ButtonKind.Secondary, () => { Say(field.Text); field.Clear(); }));
		column.AddChild(input);

		ScrollToEnd();
		return panel;
	}

	private Control UserInfo()
	{
		PanelContainer panel = UiKit.Panel("Surface1");
		panel.CustomMinimumSize = new Vector2(340, 0);
		_userInfo = panel;

		MarginContainer margin = UiKit.Margin(UiTokens.S3);
		panel.AddChild(margin);
		VBoxContainer column = UiKit.Column(UiTokens.S2);
		margin.AddChild(column);

		column.AddChild(UiKit.Caption("Jogador"));

		RoomMember m = _selected ?? Me;
		if (m == null) return panel;

		// O retrato e os nomes no mesmo centro (Jhon, 21/11: "precisa alinhar a
		// moldura com os nomes"). Bot não tem nível nem foto: o ícone dele, no
		// mesmo lugar, para o nome não ficar solto.
		HBoxContainer who = UiKit.Row(UiTokens.S3);
		if (m.IsBot)
		{
			var botBox = new CenterContainer { CustomMinimumSize = new Vector2(64, 64) };
			botBox.AddChild(Icon.Make(IconKind.Bot, 40, UiTokens.TextDisabled));
			who.AddChild(botBox);
		}
		else
		{
			// a placa do nível passa um pouco da caixa: folga embaixo
			var portrait = new MarginContainer();
			portrait.AddThemeConstantOverride("margin_bottom", 10);
			portrait.AddChild(RankBadge.Make(m.Rank, 64, avatar: PhotoOf(m)));
			who.AddChild(portrait);
		}
		VBoxContainer names = UiKit.Column(2);
		names.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		names.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		names.AddChild(UiKit.Truncated(m.Name, "Subtitle", Teams.ColorOf(m.Team)));
		names.AddChild(UiKit.Text(Ranks.NameOf(m.Rank), "Caption", Ranks.InkOf(m.Rank)));
		who.AddChild(names);
		column.AddChild(who);

		if (m.IsBot)
		{
			column.AddChild(Info("Tipo", "Bot"));
			column.AddChild(Info("Dificuldade", new[] { "Fácil", "Médio", "Difícil" }[(int)S.Difficulty]));
			column.AddChild(Info("Arma", S.Weapons == WeaponRule.SoPistolas ? "M9" : "M16A2"));
		}
		else if (_online && m != Me)
		{
			column.AddChild(Info("Nível", Ranks.NameOf(m.Rank)));
			column.AddChild(Info("Situação", _online && _room.State == RoomState.Playing ? (m.InMatch ? "Jogando" : "Na sala")
				: m.IsHost ? "Dono da sala" : m.Ready ? "Pronto" : "Aguardando"));
		}
		else
		{
			LifetimeStats s = Profile.Stats;
			if (!string.IsNullOrEmpty(Profile.Title)) column.AddChild(Info("Título", Profile.Title));
			column.AddChild(Info("Partidas", Loc.F("{0}  ·  {1:0}% de vitórias", s.Matches, s.WinRate * 100)));
			column.AddChild(Info("Abates / mortes", $"{s.Kills} / {s.Deaths}  ·  {s.KillDeath:0.00}"));
			column.AddChild(UiKit.Button("Ver perfil", ButtonKind.Tertiary, () => Shell.GoProfile()));
		}

		return panel;
	}

	private static Control Info(string label, string value) => UiKit.Stat(label, value);

	private Control Actions()
	{
		VBoxContainer column = UiKit.Column(UiTokens.S2);
		column.CustomMinimumSize = new Vector2(240, 0);

		Button invite = UiKit.Button("Convidar", ButtonKind.Secondary);
		invite.Disabled = true;
		invite.TooltipText = _online ? Loc.F("Passe o número da sala: {0}", S.Number) : "Convidar amigos exige servidor";
		column.AddChild(invite);

		_ready = UiKit.Button("Pronto", ButtonKind.Secondary, ToggleReady);
		column.AddChild(_ready);

		column.AddChild(UiKit.Spacer());

		_hint = UiKit.Paragraph("A partida já começou. Escolha o lado e entre.");
		_hint.Visible = false;
		column.AddChild(_hint);

		// Primário, sólido, longe de qualquer coisa destrutiva.
		_start = UiKit.Button("Iniciar partida", ButtonKind.Primary, StartOrCancel);
		_start.CustomMinimumSize = new Vector2(0, 56);
		_start.AddThemeFontSizeOverride("font_size", UiTokens.Subtitle);
		column.AddChild(_start);

		return column;
	}

	// ------------------------------------------------------------------ estado e ações

	public override void _Process(double delta)
	{
		if (_countdown < 0) return;

		_countdown -= delta;
		if (_ring != null) _ring.Left = Mathf.Max(_countdown, 0);

		int second = Mathf.CeilToInt((float)_countdown);
		if (second != _lastSecond && second >= 1)
		{
			_lastSecond = second;
			Sfx.Ui("contagem");
		}

		if (_countdown <= 0)
		{
			_countdown = -1;
			Launch();
		}
	}

	private int _lastSecond = -1;

	/// <summary>
	/// O mestre inicia com todos prontos, ou com os prontos já em times
	/// equilibrados: um lado pode ter um a menos. Esperar o último
	/// indeciso trava a sala inteira; começar 5 contra 2 estraga a partida.
	/// </summary>
	private bool CanStart(out string why)
	{
		why = null;
		List<RoomMember> playing = Playing;
		int blue = playing.Count(m => m.Team == Teams.Blue);
		int orange = playing.Count(m => m.Team == Teams.Orange);

		if (!IsHost) why = "Só o mestre inicia";
		else if (_online && _room.State == RoomState.Playing) why = "A partida está em andamento";
		else if (_online && !AllReady) why = "Falta alguém marcar que está pronto";
		else if (VsBots) why = blue == 0 || orange == 0 ? "Os dois times precisam de alguém: ponha um bot no time vazio" : null;
		else if (blue == 0 || orange == 0)
			why = AllReady ? "Os dois times precisam de alguém" : "Falta alguém pronto num dos times";
		else if (!AllReady && Math.Abs(blue - orange) > 1)
			why = Loc.F("Prontos: {0} contra {1} — espere mais alguém ficar pronto", blue, orange);

		return why == null;
	}

	private Label _hint;

	/// <summary>De fora da partida em curso: dá para entrar pelo meu time agora?</summary>
	private bool CanEnter(out string why)
	{
		why = null;
		if (_room.Closing) why = "A partida está quase acabando: espere a próxima";
		else if (_room.EnterTeams.Count == 0) why = "A partida está cheia";
		else if (!_room.EnterTeams.Contains(Me.Team))
			why = Loc.F("O time {0} ficaria com dois a mais: troque de lado", Loc.T(Teams.TitleOf(Me.Team)));
		return why == null;
	}

	private void RefreshActions()
	{
		if (_ready == null || Me == null) return;

		if (_hint != null) _hint.Visible = Outside;
		if (Outside)
		{
			_ready.Visible = false;
			bool open = CanEnter(out string closed);
			_start.Text = "Entrar na partida";
			_start.Disabled = !open;
			_start.TooltipText = closed ?? "";
			if (_hint != null) _hint.Text = Loc.T(closed ?? "A partida já começou. Escolha o lado e entre.");
			return;
		}

		// Mesmo símbolo do lugar na lista: as divisas » querem dizer
		// "pronto" em toda a tela.
		_ready.Visible = !IsHost;
		_ready.Text = Me.Ready ? "Pronto  »" : "Marcar pronto";
		foreach (string state in new[] { "font_color", "font_hover_color", "font_focus_color", "font_pressed_color" })
			_ready.AddThemeColorOverride(state, Me.Ready ? UiTokens.Success : UiTokens.TextPrimary);
		_ready.Disabled = _countdown >= 0;

		if (_countdown >= 0)
		{
			_start.Text = "Cancelar";
			_start.Disabled = false;
			_start.TooltipText = "";
			return;
		}

		bool can = CanStart(out string why);
		_start.Text = "Iniciar partida";
		_start.Disabled = !can;
		_start.TooltipText = why ?? "";
	}

	private void ToggleReady()
	{
		if (Me == null || IsHost) return;
		if (_online)
		{
			Lobby.SetReady(!Me.Ready);
			return;
		}
		Me.Ready = !Me.Ready;
		Log(Me.Ready ? Loc.F("{0} está pronto.", Me.Name) : Loc.F("{0} não está mais pronto.", Me.Name), system: true);
		Refresh();
	}

	private void StartOrCancel()
	{
		if (Outside)
		{
			if (!CanEnter(out string closed))
			{
				Shell.Toast(closed, UiTokens.Danger);
				return;
			}
			Sfx.Ui("ui_jogar");
			Lobby.EnterMatch();     // o servidor manda o ingresso, e o jogo entra na partida
			return;
		}

		if (_countdown >= 0)
		{
			_countdown = -1;
			Log("Contagem cancelada.", system: true);
			Refresh();
			return;
		}

		if (!CanStart(out string why))
		{
			Shell.Toast(why, UiTokens.Danger);
			return;
		}

		Sfx.Ui("ui_jogar");

		// Online quem começa é o servidor, para todos ao mesmo tempo.
		if (_online)
		{
			Lobby.StartMatch();
			return;
		}

		_countdown = StartCountdown;
		_lastSecond = -1;
		Log("A partida começa em 5 segundos.", system: true);
		Refresh();
	}

	/// <summary>Leva a sala para a partida.</summary>
	private void Launch()
	{
		RoomConfig.Active = true;
		RoomConfig.Settings = S.Clone();
		// Quem não estava pronto fica na sala.
		foreach (RoomMember left in _room.Members.Where(m => !m.IsHost && !m.Ready))
			Log(Loc.F("{0} ficou de fora: não estava pronto.", left.Name), system: true);

		RoomConfig.Members.Clear();
		RoomConfig.Members.AddRange(Playing.Select(m => new RoomMember
		{
			Name = m.Name, Team = m.Team, IsBot = m.IsBot, Ready = m.Ready, IsHost = m.IsHost, Rank = m.Rank
		}));
		RoomConfig.ReturnTo = ReturnTarget.Room;

		Difficulty.Set(S.Difficulty);

		_room.State = RoomState.Playing;
		RoomDirectory.Current = _room;

		if (_room.Mine)
		{
			Profile.LastRoom = S.Clone();
			Profile.Save();
		}

		UiState.Reset();
		LoadingScreen.Open(GetTree());
	}

	private void Leave()
	{
		_countdown = -1;
		if (_online)
		{
			Lobby.LeaveRoom();     // o servidor responde, e a tela volta às salas
			return;
		}

		if (_room.Mine) RoomDirectory.Remove(_room);
		else _room.Members.RemoveAll(m => !m.IsBot);

		Chats.Remove(S.Number);
		RoomDirectory.Current = null;
		Shell.GoOnline();
	}

	private void SwapSide()
	{
		if (Me == null || _countdown >= 0) return;
		int other = Teams.Other(Me.Team);

		// De fora da partida em curso: só o lado por onde dá para entrar
		// (com lugar, e sem deixar um time com dois a mais).
		if (Outside)
		{
			if (!_room.EnterTeams.Contains(other))
			{
				Shell.Toast(Loc.F("O time {0} não tem lugar ou ficaria com dois a mais", Loc.T(Teams.TitleOf(other))), UiTokens.Danger);
				return;
			}
			Lobby.SetTeam(other);
			return;
		}

		// Time cheio só barra se for de gente: um bot dá o lugar (contra
		// bots, o jogador fica no time que quiser).
		RoomMember bot = _room.Members.LastOrDefault(m => m.Team == other && m.IsBot);
		if (_room.Members.Count(m => m.Team == other) >= S.TeamSize && bot == null)
		{
			Shell.Toast(Loc.F("O time {0} está cheio", Teams.TitleOf(other)), UiTokens.Danger);
			return;
		}

		if (_online)
		{
			Lobby.SetTeam(other);
			return;
		}

		if (_room.Members.Count(m => m.Team == other) >= S.TeamSize) _room.Members.Remove(bot);
		Me.Team = other;
		Log(Loc.F("{0} foi para o time {1}.", Me.Name, Loc.T(Teams.TitleOf(other))), system: true);
		Refresh();
	}

	private void AddBot(int team, int count)
	{
		if (!IsHost || !VsBots) return;
		if (_online)
		{
			int free = S.TeamSize - _room.Members.Count(m => m.Team == team);
			for (int i = 0; i < Math.Min(count, free); i++) Lobby.AddBot(team);
			return;
		}
		RoomDirectory.AddBots(_room, team, count);
		Refresh();
	}

	private void RemoveBot(RoomMember bot)
	{
		if (!IsHost || !bot.IsBot) return;
		if (_online)
		{
			Lobby.RemoveBot(bot.Id);
			return;
		}
		_room.Members.Remove(bot);
		if (_selected == bot) _selected = Me;
		Refresh();
	}

	// ------------------------------------------------------------------ regras: mudanças

	private void Change(string what, string value, Action apply)
	{
		if (!IsHost) return;
		apply();
		if (_online)
		{
			PushSettings();
			return;
		}
		Log($"{Loc.T(what)}: {Loc.T(value)}.", system: true);
		SaveRules();
		RefreshActions();
	}

	private void SaveRules()
	{
		if (!_room.Mine) return;
		Profile.LastRoom = S.Clone();
		Profile.Save();
	}

	private void SetTitle(string text)
	{
		text = (text ?? "").Trim();
		if (text.Length == 0 || text == S.Title) return;
		Change("Título", text, () => S.Title = text);
		Refresh();
	}

	private void SetPassword(string text)
	{
		text = (text ?? "").Trim();
		if (text == S.Password) return;

		if (text.Length > 0 && (text.Length < 4 || text.Length > 8))
		{
			Shell.Toast("A senha tem de 4 a 8 caracteres", UiTokens.Danger);
			return;
		}

		if (_online) OnlinePasswords[S.Number] = text;
		Change("Senha", text.Length == 0 ? "removida" : "definida", () => S.Password = text);
	}

	/// <summary>Diminuir o tamanho só se ninguém ocupa os lugares que
	/// sairiam — não se expulsa ninguém mudando uma regra.</summary>
	private void SetTeamSize(int size)
	{
		// Bot sai sozinho para caber; gente não se expulsa.
		foreach (int team in new[] { Teams.Blue, Teams.Orange })
			while (_room.Members.Count(m => m.Team == team) > size && _room.Members.LastOrDefault(m => m.Team == team && m.IsBot) is RoomMember extra)
				_room.Members.Remove(extra);

		if (_room.Members.Count(m => m.Team == Teams.Blue) > size || _room.Members.Count(m => m.Team == Teams.Orange) > size)
		{
			Shell.Toast("Tire jogadores antes de diminuir os times", UiTokens.Danger);
			Refresh();
			return;
		}

		Change("Jogadores", Loc.F("{0} contra {1}", size, size), () => S.TeamSize = size);
		Refresh();
	}

	private void SetDifficulty(int level)
	{
		Change("Bots", new[] { "fáceis", "médios", "difíceis" }[level], () => S.Difficulty = (BotLevel)level);
		Refresh();
	}

	/// <summary>A foto de um membro. A sua vem daqui mesmo, não do servidor:
	/// na sala offline não há servidor, e um servidor antigo não manda foto.</summary>
	private string PhotoOf(RoomMember m) => m == null || m.IsBot ? null : m.Photo ?? (m == Me ? Avatars.Selected : null);

	/// <summary>Os degraus do filtro de nível da sala (em rank: nível − 1).</summary>
	private static readonly int[] LevelSteps = { 0, 9, 24, 49, 99, 599, 1099, 1599, 2099, 2599, Ranks.Count - 1 };

	/// <summary>O degrau mais alto que não passa do rank (salas antigas guardam qualquer número).</summary>
	private static int StepOf(int rank)
	{
		int step = 0;
		for (int i = 0; i < LevelSteps.Length; i++) if (LevelSteps[i] <= rank) step = i;
		return step;
	}

	private void SetRankRange(int min, int max)
	{
		if (min > max) (min, max) = (max, min);
		Change("Nível", $"{Ranks.NameOf(min)} a {Ranks.NameOf(max)}", () => { S.MinRank = min; S.MaxRank = max; });
		Refresh();
	}

	// ------------------------------------------------------------------ chat

	private void Say(string text)
	{
		text = (text ?? "").Trim();
		if (text.Length == 0) return;
		if (_online)
		{
			Lobby.Say(text);       // volta pelo servidor, para todos
			return;
		}
		Log(text, system: false, who: Profile.PlayerName);
	}

	private void Log(string text, bool system, string who = null)
	{
		// Online, o que se conta na sala é o chat do servidor.
		if (_online) return;
		if (!Chats.TryGetValue(S.Number, out var lines)) return;
		lines.Add((who ?? "", text, system));
		if (lines.Count > 200) lines.RemoveAt(0);
		AddChatLine(who ?? "", text, system);
		ScrollToEnd();
	}

	private void AddChatLine(string who, string text, bool system)
	{
		if (_chatLog == null || !IsInstanceValid(_chatLog)) return;

		var line = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};

		string clean = text.Replace("[", "[lb]");
		line.Text = system
			? $"[color=#{UiTokens.Warning.ToHtml(false)}]» {clean}[/color]"
			: $"[color=#{Teams.ColorOf(Me?.Team ?? Teams.Blue).ToHtml(false)}][b]{who}[/b][/color]: {clean}";
		_chatLog.AddChild(line);
	}

	private void ScrollToEnd()
	{
		if (_chatScroll == null) return;
		Callable.From(() =>
		{
			if (IsInstanceValid(_chatScroll))
				_chatScroll.ScrollVertical = (int)_chatScroll.GetVScrollBar().MaxValue;
		}).CallDeferred();
	}
}
