using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// O jogo online: entrar na conta, ver as salas dos outros, abrir a sua,
/// escolher o time, conversar e começar a partida.
///
/// Três estados, uma tela: sem conta (o formulário de entrada), no saguão
/// (a lista de salas) e dentro de uma sala (os dois times). O painel da
/// direita — ranking e chat — fica parado enquanto o da esquerda se
/// refaz a cada notícia do servidor, para não apagar o que o jogador
/// está digitando.
///
/// Quem fala com o servidor é o <see cref="Backend"/> (conta, ranking) e
/// o <see cref="LobbyLink"/> (salas e chat, ao vivo). Sem servidor a tela
/// diz isso e o resto do jogo segue offline, como sempre.
/// </summary>
public partial class OnlineScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Users;

	private static readonly int[] TeamSizes = { 4, 6, 8 };
	private static readonly int[] Scores = { 10, 20, 30, 40, 60, 80, 100 };
	private static readonly int[] Durations = { 3, 5, 10, 15, 20, 30 };

	private VBoxContainer _main;
	private VBoxContainer _chatLog;
	private ScrollContainer _chatScroll;
	private VBoxContainer _ranking;
	private string _status = "";
	private bool _busy, _connecting;

	private static LobbyLink Lobby => LobbyLink.Instance;

	public override void _Ready()
	{
		base._Ready();
		Lobby.Changed += OnLobbyChanged;
		Lobby.ChatReceived += OnChat;
		Lobby.Failed += OnFailed;
		Lobby.MatchEnded += OnMatchEnded;

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
		Lobby.Failed -= OnFailed;
		Lobby.MatchEnded -= OnMatchEnded;
	}

	protected override void Build()
	{
		_main = null;
		_chatLog = null;
		_ranking = null;

		if (!Backend.LoggedIn)
		{
			BuildLogin();
			return;
		}

		if (!Lobby.Connected)
		{
			BuildConnecting();
			if (!_connecting) _ = Connect();
			return;
		}

		HBoxContainer split = SplitPage();

		_main = UiKit.Column(UiTokens.S3);
		_main.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_main.SizeFlagsStretchRatio = 1.9f;
		split.AddChild(_main);

		VBoxContainer side = UiKit.Column(UiTokens.S3);
		side.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		side.CustomMinimumSize = new Vector2(320, 0);
		split.AddChild(side);
		side.AddChild(RankingCard());
		side.AddChild(ChatCard());

		FillMain();
		_ = LoadRanking();
	}

	// ------------------------------------------------------------------ conta

	private void BuildLogin()
	{
		var center = UiKit.Fill(new CenterContainer());
		AddChild(center);

		PanelContainer card = UiKit.Card(out VBoxContainer column, UiTokens.S5);
		card.CustomMinimumSize = new Vector2(460, 0);
		center.AddChild(card);

		column.AddChild(UiKit.Text("Jogar online", "Title"));
		column.AddChild(UiKit.Paragraph("Entre com a sua conta do servidor, ou crie uma agora. " +
			"A conta guarda a sua patente online e o seu lugar no ranking."));

		LineEdit name = UiKit.Field(Backend.AccountName.Length > 0 ? Backend.AccountName : Profile.PlayerName, "Nome", 16);
		LineEdit password = UiKit.Field("", "Senha (8 ou mais)", 64);
		password.Secret = true;
		LineEdit server = UiKit.Field(Backend.Url, Backend.DefaultUrl, 120);
		column.AddChild(UiKit.FormRow("Nome", name, 90));
		column.AddChild(UiKit.FormRow("Senha", password, 90));
		column.AddChild(UiKit.FormRow("Servidor", server, 90));

		Label status = UiKit.Paragraph(_status, null);
		status.AddThemeColorOverride("font_color", UiTokens.Warning);
		status.Visible = _status.Length > 0;
		column.AddChild(status);

		HBoxContainer buttons = UiKit.Row(UiTokens.S2);
		buttons.AddChild(UiKit.Spacer());
		Button create = UiKit.Button("Criar conta", ButtonKind.Secondary, () => Enter(server.Text, name.Text, password.Text, register: true));
		Button enter = UiKit.Button("Entrar", ButtonKind.Primary, () => Enter(server.Text, name.Text, password.Text, register: false));
		create.Disabled = enter.Disabled = _busy;
		buttons.AddChild(create);
		buttons.AddChild(enter);
		column.AddChild(buttons);

		password.TextSubmitted += _ => Enter(server.Text, name.Text, password.Text, register: false);
	}

	private async void Enter(string server, string name, string password, bool register)
	{
		if (_busy) return;
		_busy = true;
		_status = "Falando com o servidor…";
		Refresh();

		Backend.Url = server;
		Backend.Reply reply = register ? await Backend.Register(name.Trim(), password) : await Backend.Login(name.Trim(), password);

		_busy = false;
		_status = reply.Ok ? "" : reply.Status == 0
			? $"Não achei o servidor em {Backend.Url}. Ele está no ar? (docker compose up -d)"
			: reply.Error;
		if (reply.Ok) Sfx.Ui("abate");
		if (Alive) Refresh();
	}

	private void BuildConnecting()
	{
		var center = UiKit.Fill(new CenterContainer());
		AddChild(center);

		PanelContainer card = UiKit.Card(out VBoxContainer column, UiTokens.S5);
		card.CustomMinimumSize = new Vector2(460, 0);
		center.AddChild(card);

		column.AddChild(UiKit.Text(_connecting ? "Conectando…" : "Sem ligação com o servidor", "Subtitle"));
		column.AddChild(UiKit.Paragraph(_connecting
			? $"Entrando como {Backend.AccountName} em {Backend.Url}."
			: _status.Length > 0 ? _status : $"Não consegui falar com {Backend.Url}."));

		if (_connecting) return;

		HBoxContainer buttons = UiKit.Row(UiTokens.S2);
		buttons.AddChild(UiKit.Spacer());
		buttons.AddChild(UiKit.Button("Sair da conta", ButtonKind.Secondary, Logout));
		buttons.AddChild(UiKit.Button("Tentar de novo", ButtonKind.Primary, () => _ = Connect()));
		column.AddChild(buttons);
	}

	/// <summary>Confere o login guardado e abre a ligação do saguão.</summary>
	private async Task Connect()
	{
		_connecting = true;
		_status = "";
		Refresh();

		Backend.Reply profile = await Backend.Resume();
		bool open = profile.Ok && await Lobby.Open();

		_connecting = false;
		if (!profile.Ok && profile.Status == 401) _status = "A sua sessão venceu. Entre de novo.";
		else if (!open) _status = $"Não consegui falar com {Backend.Url}. O servidor está no ar?";
		if (Alive) Refresh();
	}

	private async void Logout()
	{
		Lobby.Close();
		await Backend.Logout();
		_status = "";
		if (Alive) Refresh();
	}

	private bool Alive => IsInstanceValid(this) && IsInsideTree();

	// ------------------------------------------------------------------ o painel da esquerda

	private void OnLobbyChanged()
	{
		if (!Alive) return;

		// Caiu a ligação (ou voltou): a tela inteira muda de estado.
		if (_main == null || !Lobby.Connected) Refresh();
		else Callable.From(FillMain).CallDeferred();
	}

	private void FillMain()
	{
		if (!Alive || _main == null) return;
		UiKit.Clear(_main);

		if (Lobby.Room != null) BuildRoom(Lobby.Room);
		else BuildRooms();
	}

	private void BuildRooms()
	{
		HBoxContainer head = Heading(_main, "Salas online", $"{Lobby.Rooms.Count} aberta(s) · você é {Backend.AccountName}");
		head.AddChild(UiKit.Button("Sair da conta", ButtonKind.Secondary, Logout));
		head.AddChild(UiKit.Button("Atualizar", ButtonKind.Secondary, Lobby.RefreshRooms));
		head.AddChild(UiKit.Button("Criar sala", ButtonKind.Primary, AskNewRoom));

		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S3, UiTokens.S2);
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		_main.AddChild(panel);

		if (Lobby.Rooms.Count == 0)
		{
			var empty = new CenterContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			VBoxContainer box = UiKit.Column(UiTokens.S2);
			box.AddChild(UiKit.Text("Nenhuma sala aberta agora", "Subtitle"));
			box.AddChild(UiKit.Paragraph("Crie a sua e chame alguém: a partida online é só entre jogadores, sem bots."));
			empty.AddChild(box);
			column.AddChild(empty);
			return;
		}

		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		VBoxContainer rows = UiKit.Column(UiTokens.S1);
		rows.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(rows);
		column.AddChild(scroll);

		foreach (JsonNode room in Lobby.Rooms) rows.AddChild(RoomLine(room));
	}

	private Control RoomLine(JsonNode room)
	{
		RoomSettings s = OnlineMatch.SettingsFrom(room["settings"] as JsonObject);
		bool playing = room["state"]?.GetValue<string>() != "waiting", locked = room["hasPassword"]?.GetValue<bool>() ?? false;
		int players = room["players"].GetValue<int>(), capacity = room["capacity"].GetValue<int>();
		int id = room["id"].GetValue<int>();

		PanelContainer panel = UiKit.Panel("Surface2");
		panel.CustomMinimumSize = new Vector2(0, UiTokens.RoomRowHeight);
		MarginContainer margin = UiKit.Margin(UiTokens.S3, UiTokens.S1, UiTokens.S3, UiTokens.S1);
		panel.AddChild(margin);
		HBoxContainer row = UiKit.Row(UiTokens.S3);
		margin.AddChild(row);

		Label number = UiKit.Text($"{id:000}", "Numeric", UiTokens.TextSecondary);
		number.CustomMinimumSize = new Vector2(40, 0);
		row.AddChild(number);
		if (locked) row.AddChild(Icon.Make(IconKind.Lock, 16, UiTokens.TextSecondary));

		Label title = UiKit.Truncated(s.Title, "Subtitle");
		title.AddThemeFontSizeOverride("font_size", 17);
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(title);

		row.AddChild(UiKit.Text(Maps.Get(s.Map).Name, "Muted"));
		row.AddChild(UiKit.Text($"até {s.ScoreToWin} · {s.Minutes} min", "Muted"));
		row.AddChild(UiKit.Text($"{players}/{capacity}", "Numeric", players >= capacity ? UiTokens.Danger : UiTokens.TextPrimary));
		row.AddChild(UiKit.Chip(playing ? "jogando" : "esperando", playing ? UiTokens.Warning : UiTokens.Success));

		Button join = UiKit.Button("Entrar", ButtonKind.Primary, () => Join(id, locked));
		join.Disabled = playing || players >= capacity;
		join.CustomMinimumSize = new Vector2(110, 34);
		row.AddChild(join);
		return panel;
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
		Modal modal = null;
		modal = Shell.Modal("Sala com senha", password,
			new Modal.Choice("Cancelar", ButtonKind.Secondary, null),
			new Modal.Choice("Entrar", ButtonKind.Primary, () => Lobby.JoinRoom(id, password.Text)));
		password.GrabFocus();
	}

	private const int MaxPassword = 32;

	private void AskNewRoom()
	{
		RoomSettings s = (Profile.LastRoom ?? new RoomSettings()).Clone();
		if (string.IsNullOrWhiteSpace(s.Title) || s.Title == "Sala de treino") s.Title = $"Sala de {Backend.AccountName}";
		s.Password = "";

		VBoxContainer form = UiKit.Column(UiTokens.S2);
		form.CustomMinimumSize = new Vector2(440, 0);

		LineEdit title = UiKit.Field(s.Title, "Nome da sala", 40);
		LineEdit password = UiKit.Field("", "Sem senha", MaxPassword);
		string[] maps = Maps.All.Select(m => m.Name).ToArray();

		form.AddChild(UiKit.FormRow("Nome", title, 110));
		form.AddChild(UiKit.FormRow("Mapa", UiKit.Picker(maps, Maps.All.FindIndex(m => m.Id == s.Map), i => s.Map = Maps.All[i].Id), 110));
		form.AddChild(UiKit.FormRow("Times", UiKit.Picker(TeamSizes.Select(n => $"{n} contra {n}").ToArray(),
			Array.IndexOf(TeamSizes, s.TeamSize), i => s.TeamSize = TeamSizes[i]), 110));
		form.AddChild(UiKit.FormRow("Pontos", UiKit.Picker(Scores.Select(n => $"{n} abates").ToArray(),
			Nearest(Scores, s.ScoreToWin), i => s.ScoreToWin = Scores[i]), 110));
		form.AddChild(UiKit.FormRow("Tempo", UiKit.Picker(Durations.Select(n => $"{n} minutos").ToArray(),
			Nearest(Durations, s.Minutes), i => s.Minutes = Durations[i]), 110));
		form.AddChild(UiKit.FormRow("Armas", UiKit.Picker(RoomSettings.WeaponNames, (int)s.Weapons, i => s.Weapons = (WeaponRule)i,
			Enum.GetValues<WeaponRule>().Select(w => !RoomSettings.WeaponsAvailable(w)).ToArray()), 110));
		form.AddChild(UiKit.FormRow("Lobo", UiKit.Picker(RoomSettings.TransformNames, (int)s.Transform, i => s.Transform = (TransformRule)i,
			Enum.GetValues<TransformRule>().Select(t => !RoomSettings.TransformAvailable(t)).ToArray()), 110));
		form.AddChild(UiKit.FormRow("Senha", password, 110));
		form.AddChild(UiKit.Check("Fogo amigo", s.FriendlyFire, on => s.FriendlyFire = on));

		// O valor dos seletores é o que estava mostrando, mesmo sem mexer.
		s.ScoreToWin = Scores[Nearest(Scores, s.ScoreToWin)];
		s.Minutes = Durations[Nearest(Durations, s.Minutes)];

		Shell.Modal("Criar sala online", form,
			new Modal.Choice("Cancelar", ButtonKind.Secondary, null),
			new Modal.Choice("Criar", ButtonKind.Primary, () =>
			{
				s.Title = title.Text;
				s.Password = password.Text;
				Lobby.CreateRoom(OnlineMatch.SettingsTo(s));
			}));
	}

	private static int Nearest(int[] options, int value)
	{
		int best = 0;
		for (int i = 1; i < options.Length; i++)
			if (Math.Abs(options[i] - value) < Math.Abs(options[best] - value)) best = i;
		return best;
	}

	// ------------------------------------------------------------------ a sala

	private void BuildRoom(JsonObject room)
	{
		RoomSettings s = OnlineMatch.SettingsFrom(room["settings"] as JsonObject);
		var members = room["members"] as JsonArray ?? new JsonArray();
		JsonNode me = members.FirstOrDefault(m => m["id"].GetValue<long>() == Backend.AccountId);
		bool host = me?["host"]?.GetValue<bool>() ?? false, ready = me?["ready"]?.GetValue<bool>() ?? false;
		bool playing = room["state"]?.GetValue<string>() != "waiting";

		HBoxContainer head = Heading(_main, s.Title, $"sala {room["id"]} · {Maps.Get(s.Map).Name}");
		if (room["hasPassword"]?.GetValue<bool>() ?? false) head.AddChild(UiKit.Chip("com senha", UiTokens.TextSecondary));
		head.AddChild(UiKit.Chip(playing ? "em partida" : "esperando", playing ? UiTokens.Warning : UiTokens.Success));

		HBoxContainer rules = UiKit.Row(UiTokens.S2);
		rules.AddChild(UiKit.Chip($"{s.TeamSize} contra {s.TeamSize}", UiTokens.Accent));
		rules.AddChild(UiKit.Chip($"até {s.ScoreToWin} abates", UiTokens.Accent));
		rules.AddChild(UiKit.Chip($"{s.Minutes} min", UiTokens.Accent));
		rules.AddChild(UiKit.Chip($"armas: {RoomSettings.WeaponNames[(int)s.Weapons]}", UiTokens.TextSecondary));
		rules.AddChild(UiKit.Chip($"lobo: {RoomSettings.TransformNames[(int)s.Transform]}", UiTokens.TextSecondary));
		if (s.FriendlyFire) rules.AddChild(UiKit.Chip("fogo amigo", UiTokens.Danger));
		_main.AddChild(rules);

		HBoxContainer teams = UiKit.Row(UiTokens.S3);
		teams.SizeFlagsVertical = SizeFlags.ExpandFill;
		teams.AddChild(TeamCard("Time azul", Teams.Blue, members, s.TeamSize));
		teams.AddChild(TeamCard("Time laranja", Teams.Orange, members, s.TeamSize));
		_main.AddChild(teams);

		HBoxContainer actions = UiKit.Row(UiTokens.S2);
		actions.AddChild(UiKit.Button("Sair da sala", ButtonKind.Secondary, Lobby.LeaveRoom));
		actions.AddChild(UiKit.Spacer());

		if (playing)
		{
			actions.AddChild(UiKit.Text("A partida está em andamento. A sala volta a esperar quando ela acabar.", "Muted"));
		}
		else
		{
			int team = me?["team"]?.GetValue<int>() ?? Teams.Blue;
			actions.AddChild(UiKit.Button("Trocar de time", ButtonKind.Secondary, () => Lobby.SetTeam(team == Teams.Blue ? Teams.Orange : Teams.Blue)));

			if (host)
			{
				bool everyone = members.All(m => m == me || m["ready"].GetValue<bool>());
				Button start = UiKit.Button("Iniciar partida", ButtonKind.Primary, Lobby.StartMatch);
				start.Disabled = !everyone;
				start.TooltipText = everyone ? "" : "Falta alguém marcar que está pronto.";
				actions.AddChild(start);
			}
			else
			{
				actions.AddChild(UiKit.Button(ready ? "Não estou pronto" : "Estou pronto",
					ready ? ButtonKind.Secondary : ButtonKind.Primary, () => Lobby.SetReady(!ready)));
			}
		}

		_main.AddChild(actions);
	}

	private static Control TeamCard(string title, int team, JsonArray members, int size)
	{
		Color color = Teams.ColorOf(team);
		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S3, UiTokens.S2);
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		var mine = members.Where(m => m["team"].GetValue<int>() == team).ToList();
		HBoxContainer head = UiKit.Row(UiTokens.S2);
		head.AddChild(UiKit.Text(title, "Subtitle", color));
		head.AddChild(UiKit.Spacer());
		head.AddChild(UiKit.Text($"{mine.Count}/{size}", "Numeric", UiTokens.TextSecondary));
		column.AddChild(head);
		column.AddChild(UiKit.Divider());

		foreach (JsonNode m in mine)
		{
			HBoxContainer row = UiKit.Row(UiTokens.S2);
			row.CustomMinimumSize = new Vector2(0, UiTokens.ScoreRowHeight);
			row.AddChild(RankBadge.Make(Ranks.RankFor(m["xp"]?.GetValue<long>() ?? 0), 24));

			Label name = UiKit.Truncated(m["name"].GetValue<string>(), null,
				m["id"].GetValue<long>() == Backend.AccountId ? UiTokens.Accent : (Color?)null);
			name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.AddChild(name);

			if (m["host"].GetValue<bool>())
			{
				row.AddChild(Icon.Make(IconKind.Crown, 16, UiTokens.Accent));
				row.AddChild(UiKit.Caption("dono"));
			}
			else
			{
				bool ready = m["ready"].GetValue<bool>();
				row.AddChild(UiKit.Chip(ready ? "pronto" : "esperando", ready ? UiTokens.Success : UiTokens.TextSecondary));
			}

			column.AddChild(row);
		}

		for (int i = mine.Count; i < size; i++)
		{
			Label empty = UiKit.Text("— vaga —", "Muted", UiTokens.TextDisabled);
			empty.CustomMinimumSize = new Vector2(0, UiTokens.ScoreRowHeight);
			empty.VerticalAlignment = VerticalAlignment.Center;
			column.AddChild(empty);
		}

		return panel;
	}

	// ------------------------------------------------------------------ ranking e chat

	private Control RankingCard()
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S3, UiTokens.S2);
		HBoxContainer head = UiKit.Row(UiTokens.S2);
		head.AddChild(Icon.Make(IconKind.Trophy, 18, UiTokens.Accent));
		head.AddChild(UiKit.Text("Ranking", "Subtitle"));
		column.AddChild(head);

		_ranking = UiKit.Column(UiTokens.S1);
		_ranking.AddChild(UiKit.Text("Carregando…", "Muted"));
		column.AddChild(_ranking);
		return panel;
	}

	private async Task LoadRanking()
	{
		Backend.Reply reply = await Backend.Ranking(8);
		if (!Alive || _ranking == null || !IsInstanceValid(_ranking)) return;

		UiKit.Clear(_ranking);
		var top = reply.Body?["ranking"] as JsonArray;
		if (!reply.Ok || top == null || top.Count == 0)
		{
			_ranking.AddChild(UiKit.Text(reply.Ok ? "Ninguém jogou ainda." : "Não consegui carregar.", "Muted"));
			return;
		}

		foreach (JsonNode line in top)
		{
			string name = line["name"].GetValue<string>();
			long xp = line["xp"].GetValue<long>();
			HBoxContainer row = UiKit.Row(UiTokens.S2);
			Label position = UiKit.Text($"{line["position"]}", "Numeric", UiTokens.TextSecondary);
			position.CustomMinimumSize = new Vector2(22, 0);
			row.AddChild(position);
			row.AddChild(RankBadge.Make(Ranks.RankFor(xp), 20));
			Label who = UiKit.Truncated(name, null, name == Backend.AccountName ? UiTokens.Accent : (Color?)null);
			who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.AddChild(who);
			row.AddChild(UiKit.Text($"{UiTokens.Thousands(xp)} XP", "Numeric", UiTokens.TextSecondary));
			_ranking.AddChild(row);
		}
	}

	private Control ChatCard()
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S3, UiTokens.S2);
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;

		HBoxContainer head = UiKit.Row(UiTokens.S2);
		head.AddChild(Icon.Make(IconKind.Chat, 18, UiTokens.Accent));
		head.AddChild(UiKit.Text("Conversa", "Subtitle"));
		column.AddChild(head);

		_chatScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		_chatLog = UiKit.Column(2);
		_chatLog.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_chatScroll.AddChild(_chatLog);
		column.AddChild(_chatScroll);

		foreach ((string from, string text) in Lobby.Chat) _chatLog.AddChild(ChatLine(from, text));
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

	private void OnChat(string from, string text)
	{
		if (!Alive || _chatLog == null || !IsInstanceValid(_chatLog)) return;
		_chatLog.AddChild(ChatLine(from, text));
		ScrollChat();
	}

	private async void ScrollChat()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (Alive && _chatScroll != null && IsInstanceValid(_chatScroll))
			_chatScroll.ScrollVertical = (int)_chatScroll.GetVScrollBar().MaxValue;
	}

	// ------------------------------------------------------------------ avisos

	private void OnFailed(string code, string message)
	{
		if (Alive) Shell.Toast(message, UiTokens.Danger);
	}

	/// <summary>A partida da sala acabou: o servidor diz o que cada um ganhou.</summary>
	private void OnMatchEnded(JsonObject end)
	{
		if (!Alive) return;

		JsonNode you = end["you"];
		if (you != null)
			Shell.Toast($"Partida registrada no servidor: +{you["xp"]} XP de patente online", UiTokens.Success);
		_ = LoadRanking();
	}
}
