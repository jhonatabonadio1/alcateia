using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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
/// Sem servidor só há um humano (você), sempre mestre. As regras de
/// pronto e de mestre já são as do multijogador: na Fase 2 muda de onde
/// vêm os jogadores, não como a sala funciona.
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

	public RoomScreen(RoomListing room)
	{
		_room = room;

		// Voltando de uma partida: a sala espera de novo, e ninguém está
		// pronto — prontidão vale para uma partida, não para a sala.
		if (_room != null && _room.State == RoomState.Playing)
		{
			_room.State = RoomState.Waiting;
			foreach (RoomMember m in _room.Members.Where(m => !m.IsBot)) m.Ready = false;
		}
	}

	private RoomSettings S => _room.Settings;
	private RoomMember Me => _room.Members.FirstOrDefault(m => !m.IsBot);
	private bool IsHost => Me?.IsHost == true;

	/// <summary>Quem entra na partida: o mestre, que não marca pronto, e
	/// quem está pronto.</summary>
	private List<RoomMember> Playing => _room.Members.Where(m => m.IsHost || m.Ready).ToList();

	private bool AllReady => _room.Members.All(m => m.IsHost || m.Ready);

	// ------------------------------------------------------------------ montagem

	protected override void Build()
	{
		if (_room == null)
		{
			Shell.GoRooms();
			return;
		}

		_selected ??= Me;
		if (!Chats.ContainsKey(S.Number))
		{
			Chats[S.Number] = new List<(string, string, bool)>();
			Log($"{Profile.PlayerName} entrou na sala.", system: true);
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

		Label channel = UiKit.Caption("Canal local");
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
		headRow.AddChild(UiKit.Text($"{members.Count}/{S.TeamSize}", "Numeric", UiTokens.TextSecondary));
		headRow.AddChild(UiKit.Spacer());

		if (IsHost && members.Count < S.TeamSize)
		{
			Button fill = UiKit.Button("Encher com bots", ButtonKind.Tertiary, () => { AddBot(team, S.TeamSize); });
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

		RankBadge badge = RankBadge.Make(member.Rank, 28);
		badge.MouseFilter = MouseFilterEnum.Ignore;
		row.AddChild(badge);

		Icon crown = Icon.Make(IconKind.Crown, 16, member.IsHost ? UiTokens.Accent : new Color(0, 0, 0, 0));
		row.AddChild(crown);

		Label name = UiKit.Truncated(member.Name, null, member.IsBot ? UiTokens.TextSecondary : UiTokens.TextPrimary);
		name.AddThemeFontOverride("font", member.IsBot ? UiFonts.Medium : UiFonts.SemiBold);
		row.AddChild(name);

		if (member.IsBot) row.AddChild(UiKit.Chip("bot", UiTokens.TextDisabled));

		// Pronto: divisas verdes. Quem não está pronto simplesmente não
		// tem as divisas — e o texto diz que está aguardando. O mestre não
		// marca pronto: a coroa já diz que é ele quem inicia.
		if (member.Ready)
			row.AddChild(Icon.Make(IconKind.Chevrons, 18, UiTokens.Success));
		else if (!member.IsHost)
			row.AddChild(UiKit.Text("aguardando", "Caption", UiTokens.TextDisabled));

		row.AddChild(PingBars.Make(0));

		if (IsHost && member.IsBot)
		{
			Button remove = UiKit.IconButton(IconKind.Close, $"Tirar {member.Name}", () => RemoveBot(member), 14);
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

		if (IsHost)
		{
			Button add = UiKit.Button("+  Adicionar bot", ButtonKind.Tertiary, () => AddBot(team, 1));
			add.CustomMinimumSize = new Vector2(0, 38);
			add.Alignment = HorizontalAlignment.Left;
			slot.AddChild(add);
		}

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

		form.AddChild(Row("Modo", Picker(RoomSettings.ModeNames.Select((n, i) => RoomSettings.ModeAvailable((GameMode)i) ? n : $"{n} (em breve)").ToArray(),
			(int)S.Mode, i => Change("Modo", RoomSettings.ModeNames[i], () => S.Mode = (GameMode)i),
			RoomSettings.ModeNames.Select((_, i) => !RoomSettings.ModeAvailable((GameMode)i)).ToArray())));

		int[] sizes = { 4, 6, 8 };
		form.AddChild(Row("Jogadores", Picker(sizes.Select(n => $"{n} contra {n}").ToArray(),
			Array.IndexOf(sizes, S.TeamSize), i => SetTeamSize(sizes[i]))));

		int[] scores = { 20, 40, 60, 100 };
		form.AddChild(Row("Objetivo", Picker(scores.Select(n => $"{n} pontos").ToArray(),
			Mathf.Max(0, Array.IndexOf(scores, S.ScoreToWin)), i => Change("Objetivo", $"{scores[i]} pontos", () => S.ScoreToWin = scores[i]))));

		int[] minutes = { 5, 10, 15, 20 };
		form.AddChild(Row("Tempo", Picker(minutes.Select(n => $"{n} min").ToArray(),
			Mathf.Max(0, Array.IndexOf(minutes, S.Minutes)), i => Change("Tempo", $"{minutes[i]} min", () => S.Minutes = minutes[i]))));

		form.AddChild(Row("Armas", Picker(RoomSettings.WeaponNames.Select((n, i) => RoomSettings.WeaponsAvailable((WeaponRule)i) ? n : $"{n} (em breve)").ToArray(),
			(int)S.Weapons, i => Change("Armas", RoomSettings.WeaponNames[i], () => S.Weapons = (WeaponRule)i),
			RoomSettings.WeaponNames.Select((_, i) => !RoomSettings.WeaponsAvailable((WeaponRule)i)).ToArray())));

		form.AddChild(Row("Transformação", Picker(RoomSettings.TransformNames.Select((n, i) => RoomSettings.TransformAvailable((TransformRule)i) ? n : $"{n} (em breve)").ToArray(),
			(int)S.Transform, i => Change("Transformação", RoomSettings.TransformNames[i], () => S.Transform = (TransformRule)i),
			RoomSettings.TransformNames.Select((_, i) => !RoomSettings.TransformAvailable((TransformRule)i)).ToArray())));

		form.AddChild(Row("Bots", Picker(new[] { "Fáceis", "Médios", "Difíceis" }, (int)S.Difficulty, SetDifficulty)));

		string[] rankNames = Enumerable.Range(0, Ranks.Count).Select(Ranks.NameOf).ToArray();
		HBoxContainer range = UiKit.Row(UiTokens.S2);
		OptionButton min = Picker(rankNames, S.MinRank, i => SetRankRange(i, S.MaxRank));
		OptionButton max = Picker(rankNames, S.MaxRank, i => SetRankRange(S.MinRank, i));
		range.AddChild(min);
		range.AddChild(UiKit.Text("a", "Muted"));
		range.AddChild(max);
		range.TooltipText = "Vale quando houver multijogador.";
		form.AddChild(Row("Patente", range));

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
		TextureRect thumb = UiKit.Thumbnail(map.Thumbnail, new Vector2(184, 104));
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

		foreach (MapInfo map in Maps.All)
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
			TextureRect thumb = UiKit.Thumbnail(map.Thumbnail, new Vector2(284, 160));
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

		HBoxContainer who = UiKit.Row(UiTokens.S3);
		who.AddChild(RankBadge.Make(m.Rank, 48));
		VBoxContainer names = UiKit.Column(0);
		names.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		names.AddChild(UiKit.Truncated(m.Name, "Subtitle", Teams.ColorOf(m.Team)));
		names.AddChild(UiKit.Text(Ranks.NameOf(m.Rank), "Caption", Ranks.ColorOf(m.Rank)));
		who.AddChild(names);
		column.AddChild(who);

		if (m.IsBot)
		{
			column.AddChild(Info("Tipo", "Bot"));
			column.AddChild(Info("Dificuldade", new[] { "Fácil", "Médio", "Difícil" }[(int)S.Difficulty]));
			column.AddChild(Info("Arma", S.Weapons == WeaponRule.SoPistolas ? "M9" : "M16A2"));
		}
		else
		{
			LifetimeStats s = Profile.Stats;
			if (!string.IsNullOrEmpty(Profile.Title)) column.AddChild(Info("Título", Profile.Title));
			column.AddChild(Info("Partidas", $"{s.Matches}  ·  {s.WinRate * 100:0}% de vitórias"));
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
		invite.TooltipText = "Convidar amigos exige servidor";
		column.AddChild(invite);

		_ready = UiKit.Button("Pronto", ButtonKind.Secondary, ToggleReady);
		column.AddChild(_ready);

		column.AddChild(UiKit.Spacer());

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
		else if (blue == 0 || orange == 0)
			why = AllReady ? "Os dois times precisam de alguém" : "Falta alguém pronto num dos times";
		else if (!AllReady && Math.Abs(blue - orange) > 1)
			why = $"Prontos: {blue} contra {orange} — espere mais alguém ficar pronto";

		return why == null;
	}

	private void RefreshActions()
	{
		if (_ready == null || Me == null) return;

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
		Me.Ready = !Me.Ready;
		Log(Me.Ready ? $"{Me.Name} está pronto." : $"{Me.Name} não está mais pronto.", system: true);
		Refresh();
	}

	private void StartOrCancel()
	{
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
			Log($"{left.Name} ficou de fora: não estava pronto.", system: true);

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
		GetTree().ChangeSceneToFile("res://scenes/main.tscn");
	}

	private void Leave()
	{
		_countdown = -1;
		if (_room.Mine) RoomDirectory.Remove(_room);
		else _room.Members.RemoveAll(m => !m.IsBot);

		Chats.Remove(S.Number);
		RoomDirectory.Current = null;
		Shell.GoRooms();
	}

	private void SwapSide()
	{
		if (Me == null || _countdown >= 0) return;
		int other = Teams.Other(Me.Team);

		if (_room.Members.Count(m => m.Team == other) >= S.TeamSize)
		{
			Shell.Toast($"O time {Teams.TitleOf(other)} está cheio", UiTokens.Danger);
			return;
		}

		Me.Team = other;
		Log($"{Me.Name} foi para o time {Teams.TitleOf(other)}.", system: true);
		Refresh();
	}

	private void AddBot(int team, int count)
	{
		if (!IsHost) return;
		RoomDirectory.AddBots(_room, team, count);
		Refresh();
	}

	private void RemoveBot(RoomMember bot)
	{
		if (!IsHost || !bot.IsBot) return;
		_room.Members.Remove(bot);
		if (_selected == bot) _selected = Me;
		Refresh();
	}

	// ------------------------------------------------------------------ regras: mudanças

	private void Change(string what, string value, Action apply)
	{
		if (!IsHost) return;
		apply();
		Log($"{what}: {value}.", system: true);
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

		Change("Senha", text.Length == 0 ? "removida" : "definida", () => S.Password = text);
	}

	/// <summary>Diminuir o tamanho só se ninguém ocupa os lugares que
	/// sairiam — não se expulsa ninguém mudando uma regra.</summary>
	private void SetTeamSize(int size)
	{
		if (_room.Members.Count(m => m.Team == Teams.Blue) > size || _room.Members.Count(m => m.Team == Teams.Orange) > size)
		{
			Shell.Toast("Tire jogadores antes de diminuir os times", UiTokens.Danger);
			Refresh();
			return;
		}

		Change("Jogadores", $"{size} contra {size}", () => S.TeamSize = size);
		Refresh();
	}

	private void SetDifficulty(int level)
	{
		Change("Bots", new[] { "fáceis", "médios", "difíceis" }[level], () => S.Difficulty = (BotLevel)level);
		Refresh();
	}

	private void SetRankRange(int min, int max)
	{
		if (min > max) (min, max) = (max, min);
		Change("Patente", $"{Ranks.NameOf(min)} a {Ranks.NameOf(max)}", () => { S.MinRank = min; S.MaxRank = max; });
		Refresh();
	}

	// ------------------------------------------------------------------ chat

	private void Say(string text)
	{
		text = (text ?? "").Trim();
		if (text.Length == 0) return;
		Log(text, system: false, who: Profile.PlayerName);
	}

	private void Log(string text, bool system, string who = null)
	{
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
