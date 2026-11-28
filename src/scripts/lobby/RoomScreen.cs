using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// A sala antes da partida. Refeita em 27/11 (Jhon: "refaça a HUD dos
/// menus e da sala, com Valorant de inspiração"), em três faixas:
///
/// - **no alto**, o nome da sala em letra de placa e o que se joga, em
///   etiquetas (modo, tamanho, objetivo), com o botão de sair;
/// - **no meio**, os times frente a frente — Vanguarda à esquerda,
///   Sindicato à direita, espelhados, com o "VS" e a troca de lado entre os
///   dois — e, à direita, o mapa (a foto grande) e as regras, duas por
///   linha, que cabem sem rolar. Lugar vazio aparece listrado; o mestre
///   leva a coroa; cada lugar diz se a pessoa está pronta;
/// - **embaixo**, a conversa, quem foi clicado e as ações: convidar
///   amigos (pelo painel social), marcar pronto e o botão de papel que
///   inicia.
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
	private CtaButton _ready;
	private CtaButton _start;
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
	}

	public override void _ExitTree()
	{
		if (!_online) return;
		Lobby.Changed -= OnLobbyChanged;
		Lobby.ChatReceived -= OnOnlineChat;
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

	private bool _shown;

	protected override void Build()
	{
		if (_room == null)
		{
			Shell.GoPlay();
			return;
		}

		_selected ??= Me;
		if (!Chats.ContainsKey(S.Number))
		{
			Chats[S.Number] = new List<(string, string, bool)>();
			Log(Loc.F("{0} entrou na sala.", Profile.PlayerName), system: true);
		}

		VBoxContainer page = Page(UiTokens.S3);

		Control top = Top();
		page.AddChild(top);

		HBoxContainer middle = UiKit.Row(UiTokens.S4);
		middle.SizeFlagsVertical = SizeFlags.ExpandFill;
		page.AddChild(middle);

		Control teams = TeamsArea();
		Control rules = RulesPanel();
		middle.AddChild(teams);
		middle.AddChild(rules);

		Control bottom = Bottom();
		page.AddChild(bottom);

		RefreshActions();

		// A entrada só na primeira vez: a sala se refaz a cada clique e a
		// cada notícia do servidor.
		if (_shown) return;
		_shown = true;
		Motion.Rise(top, 0f, new Vector2(-24, 0));
		Motion.Rise(teams, 0.05f);
		Motion.Rise(rules, 0.1f, new Vector2(28, 0));
		Motion.Rise(bottom, 0.15f);
	}

	private Control Top()
	{
		HBoxContainer row = UiKit.Row(UiTokens.S3);

		VBoxContainer title = UiKit.Column(0);
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		string where = _online ? Loc.F("Sala #{0} · online", S.Number) : Loc.T("Treinamento · offline");
		title.AddChild(Deco.Kicker(where));
		Label name = Deco.Big(S.Title, 44);
		name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		name.ClipText = true;
		title.AddChild(name);
		row.AddChild(title);

		// O que se joga, em etiquetas: lê-se de relance, sem abrir as regras.
		HBoxContainer tags = UiKit.Row(UiTokens.S2);
		tags.SizeFlagsVertical = SizeFlags.ShrinkEnd;
		// Offline só há os modos contra bots: o que vale é o que o seletor mostra.
		PlayModes.Info[] modes = _online ? PlayModes.All : PlayModes.VersusBots;
		tags.AddChild(Deco.Tag((modes.FirstOrDefault(m => m.Mode == S.Play) ?? modes[0]).Name, new Color(UiTokens.Paper, 0.12f), UiTokens.TextPrimary));
		tags.AddChild(Deco.Tag(Loc.F("{0} contra {1}", S.TeamSize, S.TeamSize), new Color(UiTokens.Paper, 0.12f), UiTokens.TextPrimary));
		tags.AddChild(Deco.Tag(Loc.F("{0} pontos · {1} min", S.ScoreToWin, S.Minutes), new Color(UiTokens.Paper, 0.12f), UiTokens.TextPrimary));
		if (S.Password.Length > 0) tags.AddChild(Deco.Tag("Com senha", UiTokens.Accent, UiTokens.AccentText));
		row.AddChild(tags);

		CtaButton leave = CtaButton.Make("Sair da sala", CtaKind.Ghost, Leave, 40, 17);
		leave.SizeFlagsVertical = SizeFlags.ShrinkEnd;
		row.AddChild(leave);
		return row;
	}

	// ------------------------------------------------------------------ times

	/// <summary>Altura de um lugar de time.</summary>
	private const int SlotHeight = 48;

	private Control TeamsArea()
	{
		var holder = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };

		HBoxContainer teams = UiKit.Fill(UiKit.Row(UiTokens.S2));
		holder.AddChild(teams);

		teams.AddChild(TeamColumn(Teams.Blue));

		// Entre os dois times: o "VS" e o botão de trocar de lado.
		VBoxContainer versus = UiKit.Column(UiTokens.S2);
		versus.CustomMinimumSize = new Vector2(56, 0);
		versus.AddChild(UiKit.Gap(UiTokens.S2));
		Label vs = UiKit.Text("VS", "Title", new Color(UiTokens.Paper, 0.5f));
		vs.AddThemeFontOverride("font", UiFonts.Wide);
		vs.AddThemeFontSizeOverride("font_size", 22);
		vs.HorizontalAlignment = HorizontalAlignment.Center;
		versus.AddChild(vs);
		Button swapButton = UiKit.IconButton(IconKind.Refresh, "Trocar de lado", SwapSide, 22);
		swapButton.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		swapButton.AddThemeStyleboxOverride("normal", UiTheme.Box(new Color(UiTokens.Paper, 0.06f), new Color(UiTokens.Paper, 0.3f), 1, 0, UiTokens.S2, UiTokens.S2));
		versus.AddChild(swapButton);
		teams.AddChild(versus);

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
		VBoxContainer column = UiKit.Column(UiTokens.S1);
		column.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		List<RoomMember> members = _room.Members.Where(m => m.Team == team).ToList();
		Color color = Teams.ColorOf(team);
		bool right = team == Teams.Orange;

		// A placa do time: o nome grande na cor dele, a contagem e um
		// degradê que some para o lado de dentro.
		var head = new PanelContainer { CustomMinimumSize = new Vector2(0, 64) };
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(color, right ? 0.02f : 0.34f));
		gradient.SetColor(1, new Color(color, right ? 0.34f : 0.02f));
		var plate = new StyleBoxTexture { Texture = new GradientTexture2D { Gradient = gradient, Width = 64, Height = 1 } };
		plate.ContentMarginLeft = plate.ContentMarginRight = UiTokens.S3;
		head.AddThemeStyleboxOverride("panel", plate);
		// O fio da cor do time na base, e a barra grossa na ponta de fora.
		head.Draw += () =>
		{
			head.DrawRect(new Rect2(0, head.Size.Y - 2, head.Size.X, 2), color);
			head.DrawRect(new Rect2(right ? head.Size.X - 6 : 0, 0, 6, head.Size.Y), color);
		};

		HBoxContainer headRow = UiKit.Row(UiTokens.S3);
		head.AddChild(headRow);

		Label name = Deco.Big(Teams.NameOf(team), 36, color.Lerp(Colors.White, 0.35f));
		Label count = UiKit.Text($"{Shown(team)}/{S.TeamSize}", "Numeric", UiTokens.TextPrimary);
		count.AddThemeFontOverride("font", UiFonts.NumericDisplay);
		count.AddThemeFontSizeOverride("font_size", 26);

		// Contra bots, o mestre põe quantos bots quiser em cada time.
		Button fill = null;
		if (VsBots && IsHost && members.Count < S.TeamSize)
		{
			fill = UiKit.Button("Encher com bots", ButtonKind.Tertiary, () => AddBot(team, S.TeamSize));
			fill.CustomMinimumSize = new Vector2(0, 28);
		}

		// O time da direita é o espelho do da esquerda.
		Control[] pieces = right
			? new Control[] { count, fill, UiKit.Spacer(), name }
			: new Control[] { name, UiKit.Spacer(), fill, count };
		headRow.AddChild(UiKit.Gap(UiTokens.S1));
		foreach (Control piece in pieces)
			if (piece != null) headRow.AddChild(piece);
		headRow.AddChild(UiKit.Gap(UiTokens.S1));

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
			CustomMinimumSize = new Vector2(0, SlotHeight),
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.PointingHand
		};
		Color team = Teams.ColorOf(member.Team);
		StyleBoxFlat Box(Color bg, Color edge)
		{
			var b = new StyleBoxFlat { BgColor = bg, BorderColor = edge };
			b.BorderWidthLeft = 3;
			b.ContentMarginLeft = UiTokens.S2 + 2;
			b.ContentMarginRight = UiTokens.S2;
			return b;
		}
		StyleBoxFlat idle = Box(selected ? new Color(UiTokens.Paper, 0.13f) : new Color(0.02f, 0.035f, 0.055f, 0.62f), selected ? UiTokens.Paper : new Color(team, 0.75f));
		StyleBoxFlat hot = Box(new Color(UiTokens.Paper, selected ? 0.16f : 0.08f), selected ? UiTokens.Paper : team);
		slot.AddThemeStyleboxOverride("panel", idle);
		slot.MouseEntered += () => slot.AddThemeStyleboxOverride("panel", hot);
		slot.MouseExited += () => slot.AddThemeStyleboxOverride("panel", idle);
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

		if (member.IsBot)
		{
			var botBox = new CenterContainer { CustomMinimumSize = new Vector2(36, 36), SizeFlagsVertical = SizeFlags.ShrinkCenter };
			botBox.AddChild(Icon.Make(IconKind.Bot, 22, UiTokens.TextDisabled));
			row.AddChild(botBox);
		}
		else
		{
			PhotoTile photo = PhotoTile.Make(PhotoOf(member), 36);
			photo.Edge = new Color(team, 0.8f);
			row.AddChild(photo);
		}

		if (member.IsHost) row.AddChild(Icon.Make(IconKind.Crown, 16, UiTokens.Accent));

		// O nível como no cabeçalho ("32 ★"), na frente do nome. Bot não tem.
		if (!member.IsBot)
		{
			LevelChip level = LevelChip.Make(member.Rank, 18);
			level.MouseFilter = MouseFilterEnum.Ignore;
			row.AddChild(level);
		}

		Label name = UiKit.Truncated(member.Name, null, member.IsBot ? UiTokens.TextSecondary : UiTokens.TextPrimary);
		name.AddThemeFontOverride("font", UiFonts.DisplaySemi);
		name.AddThemeFontSizeOverride("font_size", 21);
		row.AddChild(name);

		// Com a partida em curso, o que conta é quem está nela e quem ainda
		// está na sala, escolhendo o lado.
		if (_online && _room.State == RoomState.Playing && !member.IsBot)
			row.AddChild(member.InMatch
				? Deco.Tag("Jogando", new Color(UiTokens.Warning, 0.9f), UiTokens.AccentText)
				: Deco.Tag("Na sala", new Color(UiTokens.Paper, 0.12f), UiTokens.TextSecondary));
		// Pronto: a etiqueta verde. Quem não está pronto fica "aguardando".
		// O mestre não marca pronto: a coroa já diz que é ele quem inicia.
		else if (member.IsBot)
			row.AddChild(Deco.Tag("Bot", new Color(UiTokens.Paper, 0.08f), UiTokens.TextDisabled));
		else if (member.IsHost)
			row.AddChild(Deco.Tag("Mestre", new Color(UiTokens.Accent, 0.16f), UiTokens.Accent));
		else if (member.Ready)
			row.AddChild(Deco.Tag("Pronto", new Color(UiTokens.Success, 0.9f), UiTokens.AccentText));
		else
			row.AddChild(Deco.Tag("Aguardando", new Color(UiTokens.Paper, 0.08f), UiTokens.TextDisabled));

		if (VsBots && IsHost && member.IsBot)
		{
			Button remove = UiKit.IconButton(IconKind.Close, Loc.F("Tirar {0}", member.Name), () => RemoveBot(member), 14);
			remove.CustomMinimumSize = new Vector2(28, 28);
			remove.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			row.AddChild(remove);
		}
		return slot;
	}

	private Control EmptySlot(int team)
	{
		var slot = new PanelContainer { CustomMinimumSize = new Vector2(0, SlotHeight) };
		var box = new StyleBoxFlat { BgColor = new Color(0.02f, 0.035f, 0.055f, 0.3f), BorderColor = new Color(UiTokens.Paper, 0.1f) };
		box.SetBorderWidthAll(1);
		slot.AddThemeStyleboxOverride("panel", box);
		slot.AddChild(new Striped());

		// Contra bots, o mestre põe um bot na vaga. Contra jogadores não há
		// bot: a vaga é de quem entrar.
		if (VsBots && IsHost)
		{
			Button add = UiKit.Button("+  Adicionar bot", ButtonKind.Tertiary, () => AddBot(team, 1));
			add.CustomMinimumSize = new Vector2(0, SlotHeight - 2);
			add.Alignment = HorizontalAlignment.Left;
			slot.AddChild(add);
			return slot;
		}

		MarginContainer margin = UiKit.Margin(UiTokens.S3, 0, UiTokens.S2, 0);
		Label free = UiKit.Caption("Vaga aberta", UiTokens.TextDisabled);
		free.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		margin.AddChild(free);
		slot.AddChild(margin);
		return slot;
	}

	// ------------------------------------------------------------------ regras

	private Control RulesPanel()
	{
		VBoxContainer side = UiKit.Column(UiTokens.S3);
		side.CustomMinimumSize = new Vector2(460, 0);

		side.AddChild(MapBlock());

		PanelContainer panel = UiKit.Card(out VBoxContainer form, UiTokens.S3, UiTokens.S2);
		Deco.Ticks(panel);
		side.AddChild(panel);

		HBoxContainer head = UiKit.Row(UiTokens.S2);
		head.AddChild(Deco.Kicker("Regras da sala"));
		head.AddChild(UiKit.Spacer());
		if (!IsHost) head.AddChild(UiKit.Text(Loc.T("só o mestre altera"), "Caption"));
		form.AddChild(head);

		// Duas regras por linha, o rótulo em cima do valor: as nove cabem
		// sem rolar (antes era uma por linha, num painel que rolava).
		var grid = new GridContainer { Columns = 2 };
		grid.AddThemeConstantOverride("h_separation", UiTokens.S3);
		grid.AddThemeConstantOverride("v_separation", UiTokens.S2);
		form.AddChild(grid);

		LineEdit title = UiKit.Field(S.Title, "Título", 24);
		title.TextSubmitted += _ => SetTitle(title.Text);
		title.FocusExited += () => SetTitle(title.Text);
		grid.AddChild(Rule("Título", title));

		LineEdit pass = UiKit.Field(S.Password, "Sem senha", 8);
		pass.Secret = true;
		pass.TooltipText = "4 a 8 caracteres. Quem é convidado por um amigo entra sem ela.";
		pass.TextSubmitted += _ => SetPassword(pass.Text);
		pass.FocusExited += () => SetPassword(pass.Text);
		grid.AddChild(Rule("Senha", pass));

		// O modo de jogo. Offline (sem outros jogadores) só há os contra bots.
		PlayModes.Info[] modes = _online ? PlayModes.All : PlayModes.VersusBots;
		OptionButton mode = Picker(modes.Select(m => m.Name).ToArray(), Mathf.Max(0, Array.FindIndex(modes, m => m.Mode == S.Play)),
			i => { Change("Modo", modes[i].Name, () => S.Play = modes[i].Mode); Refresh(); });
		mode.TooltipText = PlayModes.Of(S.Play).Line;
		grid.AddChild(Rule("Modo", mode));

		int[] sizes = RoomSettings.TeamSizes;
		grid.AddChild(Rule("Jogadores", Picker(sizes.Select(n => Loc.F("{0} contra {1}", n, n)).ToArray(),
			Mathf.Max(0, Array.IndexOf(sizes, S.TeamSize)), i => SetTeamSize(sizes[i]))));

		int[] scores = { 20, 40, 60, 100 };
		grid.AddChild(Rule("Objetivo", Picker(scores.Select(n => Loc.F("{0} pontos", n)).ToArray(),
			Mathf.Max(0, Array.IndexOf(scores, S.ScoreToWin)), i => { Change("Objetivo", Loc.F("{0} pontos", scores[i]), () => S.ScoreToWin = scores[i]); Refresh(); })));

		// Até 30 minutos (decisão do Jhon, 18/11).
		int[] minutes = { 5, 10, 15, 20, 25, 30 };
		grid.AddChild(Rule("Tempo", Picker(minutes.Select(n => Loc.F("{0} min", n)).ToArray(),
			Mathf.Max(0, Array.IndexOf(minutes, S.Minutes)), i => { Change("Tempo", Loc.F("{0} min", minutes[i]), () => S.Minutes = minutes[i]); Refresh(); })));

		grid.AddChild(Rule("Armas", Picker(RoomSettings.WeaponNames.Select((n, i) => RoomSettings.WeaponsAvailable((WeaponRule)i) ? n : Loc.F("{0} (em breve)", n)).ToArray(),
			(int)S.Weapons, i => Change("Armas", RoomSettings.WeaponNames[i], () => S.Weapons = (WeaponRule)i),
			RoomSettings.WeaponNames.Select((_, i) => !RoomSettings.WeaponsAvailable((WeaponRule)i)).ToArray())));

		grid.AddChild(Rule("Bots", Picker(new[] { "Fáceis", "Médios", "Difíceis" }, (int)S.Difficulty, SetDifficulty)));

		// Níveis de 1 a 3000: o filtro anda em degraus (o começo de cada
		// metal e algumas paradas no Iniciante), não nível a nível.
		string[] stepNames = LevelSteps.Select(r => Loc.F("Nível {0}", Ranks.LevelOf(r))).ToArray();
		OptionButton min = Picker(stepNames, StepOf(S.MinRank), i => SetRankRange(LevelSteps[i], S.MaxRank));
		OptionButton max = Picker(stepNames, StepOf(S.MaxRank), i => SetRankRange(S.MinRank, LevelSteps[i]));
		min.TooltipText = max.TooltipText = "Vale quando houver multijogador.";
		grid.AddChild(Rule("Nível mínimo", min));
		grid.AddChild(Rule("Nível máximo", max));

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

		return side;
	}

	/// <summary>Uma regra: o rótulo pequeno em cima, o valor embaixo.</summary>
	private Control Rule(string label, Control value)
	{
		VBoxContainer box = UiKit.Column(2);
		box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		Label name = UiKit.Caption(label);
		name.AddThemeFontSizeOverride("font_size", 12);
		box.AddChild(name);
		value.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		box.AddChild(value);
		if (!IsHost) SetEditable(value, false);
		return box;
	}

	/// <summary>O mapa da sala: a foto grande, o nome por cima e o botão de
	/// trocar. É a primeira coisa que se olha numa sala.</summary>
	private Control MapBlock()
	{
		MapInfo map = Maps.Get(S.Map);
		// A foto fica com a altura que as regras deixarem.
		var frame = new PanelContainer { CustomMinimumSize = new Vector2(0, 190), ClipContents = true, SizeFlagsVertical = SizeFlags.ExpandFill };
		frame.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Bg, UiTokens.Border, 1, 0, 0, 0));
		Deco.Ticks(frame, new Color(UiTokens.Paper, 0.7f), 12f);
		frame.AddChild(Deco.Photo(map.Picture, 0.96f, fromLeft: false, drift: 0.05f));

		MarginContainer pad = UiKit.Margin(UiTokens.S3);
		pad.MouseFilter = MouseFilterEnum.Ignore;
		frame.AddChild(pad);
		VBoxContainer column = UiKit.Column(0);
		column.Alignment = BoxContainer.AlignmentMode.End;
		column.MouseFilter = MouseFilterEnum.Ignore;
		pad.AddChild(column);

		HBoxContainer row = UiKit.Row(UiTokens.S2);
		row.MouseFilter = MouseFilterEnum.Ignore;
		VBoxContainer text = UiKit.Column(0);
		text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		text.MouseFilter = MouseFilterEnum.Ignore;
		text.AddChild(Deco.Kicker(Loc.F("Mapa · {0}", map.Size)));
		text.AddChild(Deco.Big(map.Name, 46));
		row.AddChild(text);

		CtaButton change = CtaButton.Make("Trocar", CtaKind.Ghost, PickMap, 36, 16);
		change.Cut = 8;
		change.Disabled = !IsHost;
		change.TooltipText = IsHost ? map.Description : "Só o mestre troca o mapa";
		change.SizeFlagsVertical = SizeFlags.ShrinkEnd;
		row.AddChild(change);
		column.AddChild(row);
		return frame;
	}

	/// <summary>A escolha do mapa: um cartão por mapa, com a foto, o
	/// tamanho e o nome. O atual vem marcado.</summary>
	private void PickMap()
	{
		if (!IsHost) return;

		Modal modal = null;
		GridContainer grid = new() { Columns = 3 };
		grid.AddThemeConstantOverride("h_separation", UiTokens.S3);
		grid.AddThemeConstantOverride("v_separation", UiTokens.S3);

		// Mapa que o painel tirou da rotação não aparece (o da sala, sim: é o que está marcado).
		foreach (MapInfo map in Maps.All.Where(m => ServerData.MapOn(m.Id) || m.Id == S.Map))
		{
			MapInfo chosen = map;
			bool current = map.Id == S.Map;
			var card = new Button
			{
				CustomMinimumSize = new Vector2(300, 190),
				FocusMode = FocusModeEnum.All,
				MouseDefaultCursorShape = CursorShape.PointingHand,
				ClipContents = true,
				TooltipText = map.Description
			};
			card.AddThemeStyleboxOverride("normal", UiTheme.Box(UiTokens.Bg, current ? UiTokens.Accent : UiTokens.Border, current ? 2 : 1, 0, 0, 0));
			card.AddThemeStyleboxOverride("hover", UiTheme.Box(UiTokens.Bg, UiTokens.Paper, 2, 0, 0, 0));
			card.AddThemeStyleboxOverride("pressed", UiTheme.Box(UiTokens.Bg, UiTokens.Accent, 2, 0, 0, 0));

			Control photo = UiKit.Fill(Deco.Photo(map.Picture, 0.95f, fromLeft: false, drift: 0f));
			photo.OffsetLeft = photo.OffsetTop = 2;
			photo.OffsetRight = photo.OffsetBottom = -2;
			card.AddChild(photo);

			VBoxContainer inside = UiKit.Column(0);
			inside.MouseFilter = MouseFilterEnum.Ignore;
			inside.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
			inside.GrowVertical = GrowDirection.Begin;
			inside.OffsetLeft = UiTokens.S3;
			inside.OffsetRight = -UiTokens.S3;
			inside.OffsetBottom = -UiTokens.S2;
			inside.AddChild(Deco.Kicker(current ? Loc.F("{0} · na sala", map.Size) : map.Size, current ? UiTokens.Accent : UiTokens.TextSecondary));
			inside.AddChild(Deco.Big(map.Name, 32));
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
		row.CustomMinimumSize = new Vector2(0, 196);

		row.AddChild(Chat());
		row.AddChild(UserInfo());
		row.AddChild(Actions());
		return row;
	}

	private Control Chat()
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S3, UiTokens.S2);
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		column.AddChild(Deco.Kicker("Conversa da sala"));

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
		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S3, UiTokens.S2);
		panel.CustomMinimumSize = new Vector2(340, 0);
		_userInfo = panel;

		column.AddChild(Deco.Kicker("Jogador"));

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
		Label name = UiKit.Truncated(m.Name, "Subtitle", Teams.ColorOf(m.Team).Lerp(Colors.White, 0.3f));
		name.AddThemeFontOverride("font", UiFonts.Display);
		name.AddThemeFontSizeOverride("font_size", 28);
		names.AddChild(name);
		names.AddChild(UiKit.Text(Ranks.NameOf(m.Rank), "Caption", Ranks.InkOf(m.Rank)));
		who.AddChild(names);
		column.AddChild(who);

		if (m.IsBot)
		{
			column.AddChild(Info("Dificuldade", new[] { "Fácil", "Médio", "Difícil" }[(int)S.Difficulty]));
			column.AddChild(Info("Arma", S.Weapons == WeaponRule.SoPistolas ? "M9" : "M16A2"));
		}
		else if (_online && m != Me)
		{
			column.AddChild(Info("Situação", _online && _room.State == RoomState.Playing ? (m.InMatch ? "Jogando" : "Na sala")
				: m.IsHost ? "Dono da sala" : m.Ready ? "Pronto" : "Aguardando"));
			// Quem ainda não é amigo: o pedido sai daqui.
			if (Social.Friends.All(f => f.Id != m.Id) && Social.Sent.All(f => f.Id != m.Id))
			{
				string who2 = m.Name;
				column.AddChild(UiKit.Button("Adicionar aos amigos", ButtonKind.Tertiary, () => Social.Add(who2)));
			}
		}
		else
		{
			LifetimeStats s = Profile.Stats;
			column.AddChild(Info("Partidas", Loc.F("{0}  ·  {1:0}% de vitórias", s.Matches, s.WinRate * 100)));
			column.AddChild(Info("Abates / mortes", $"{s.Kills} / {s.Deaths}  ·  {s.KillDeath:0.00}"));
		}

		return panel;
	}

	private static Control Info(string label, string value) => UiKit.Stat(label, value);

	private Control Actions()
	{
		VBoxContainer column = UiKit.Column(UiTokens.S2);
		column.CustomMinimumSize = new Vector2(300, 0);

		// Convidar: os amigos estão no painel social, com o + ao lado de
		// quem está conectado.
		CtaButton invite = CtaButton.Make("Convidar amigos", CtaKind.Ghost, () =>
		{
			Shell.OpenSocial();
			Shell.Toast("Clique no + ao lado de um amigo conectado para convidar");
		}, 40, 17);
		invite.Disabled = !_online;
		invite.TooltipText = _online ? Loc.F("Ou passe o número da sala: {0}", S.Number) : "O treinamento é só seu: convide numa sala online";
		column.AddChild(invite);

		_ready = CtaButton.Make("Marcar pronto", CtaKind.Ghost, ToggleReady, 40, 17);
		column.AddChild(_ready);

		column.AddChild(UiKit.Spacer());

		_hint = UiKit.Paragraph("A partida já começou. Escolha o lado e entre.", "Caption");
		_hint.Visible = false;
		column.AddChild(_hint);

		// A peça de papel da tela, longe de qualquer coisa destrutiva.
		_start = CtaButton.Make("Iniciar partida", CtaKind.Paper, StartOrCancel, 68, 30);
		_start.Arrow = true;
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
		_ready.Text = Me.Ready ? "Pronto" : "Marcar pronto";
		_ready.Kind = Me.Ready ? CtaKind.Gold : CtaKind.Ghost;
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
		Shell.GoPlay();
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
