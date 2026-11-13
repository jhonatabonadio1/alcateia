using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A lista de salas: o hub, a tela onde o jogador passa mais tempo fora
/// da partida.
///
/// Grade de 2 colunas × 5 linhas por página, com paginação em vez de
/// rolagem infinita: a posição das salas fica estável entre
/// atualizações, e o olho varre a página inteira sem rolar.
///
/// "Criar sala" e "Jogo rápido" ficam em destaque no canto superior
/// direito — os dois botões primários da tela; o resto é secundário. Ao
/// lado, o painel com as missões do dia e as novidades.
/// </summary>
public partial class RoomListScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Rooms;

	/// <summary>Atualização automática enquanto a tela está aberta.</summary>
	private const double AutoRefresh = 5.0;

	private GridContainer _grid;
	private Label _pageLabel;
	private Label _updated;
	private Label _count;
	private int _page;
	private double _sinceRefresh;
	private double _refreshTimer;

	// Filtros. Estáticos: sobrevivem a sair e voltar para a tela.
	private static int _modeFilter;
	private static int _mapFilter;
	private static bool _onlyFree;
	private static bool _hidePlaying;
	private static bool _hidePassword;

	protected override void Build()
	{
		HBoxContainer split = UiKit.Fill(UiKit.Row(0));
		AddChild(split);

		MarginContainer mainMargin = UiKit.Margin(UiTokens.S5, UiTokens.S4, UiTokens.S4, UiTokens.S4);
		mainMargin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		split.AddChild(mainMargin);

		VBoxContainer main = UiKit.Column(UiTokens.S3);
		mainMargin.AddChild(main);

		main.AddChild(Top());
		main.AddChild(Filters());
		main.AddChild(List());
		main.AddChild(Pager());

		split.AddChild(SidePanel());

		RenderRooms();
	}

	public override void _Process(double delta)
	{
		_sinceRefresh += delta;
		_refreshTimer += delta;

		if (_updated != null)
			_updated.Text = _sinceRefresh < 1.5 ? "Atualizado agora" : $"Atualizado há {(int)_sinceRefresh} s";

		if (_refreshTimer >= AutoRefresh)
		{
			_refreshTimer = 0;
			RenderRooms();
		}
	}

	// ------------------------------------------------------------------ topo

	private Control Top()
	{
		HBoxContainer row = UiKit.Row(UiTokens.S3);

		VBoxContainer title = UiKit.Column(0);
		title.AddChild(UiKit.Text("Salas", "Title"));
		_count = UiKit.Text("", "Muted");
		title.AddChild(_count);
		row.AddChild(title);

		row.AddChild(UiKit.Spacer());

		Button quick = UiKit.Button("Jogo rápido", ButtonKind.Secondary, QuickPlay);
		quick.CustomMinimumSize = new Vector2(150, UiTokens.ButtonHeight);
		quick.TooltipText = "Entra na primeira sala aguardando com vaga, ou cria uma";
		row.AddChild(quick);

		Button create = UiKit.Button("Criar sala", ButtonKind.Primary, Create);
		create.CustomMinimumSize = new Vector2(150, UiTokens.ButtonHeight);
		row.AddChild(create);

		return row;
	}

	private Control Filters()
	{
		HBoxContainer row = UiKit.Row(UiTokens.S3);

		var modes = new List<string> { "Todos os modos" };
		modes.AddRange(RoomSettings.ModeNames);
		OptionButton mode = UiKit.Picker(modes.ToArray(), _modeFilter, i => { _modeFilter = i; _page = 0; RenderRooms(); });
		mode.CustomMinimumSize = new Vector2(170, UiTokens.FieldHeight);
		mode.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		row.AddChild(mode);

		var maps = new List<string> { "Todos os mapas" };
		maps.AddRange(Maps.All.Select(m => m.Name));
		OptionButton map = UiKit.Picker(maps.ToArray(), _mapFilter, i => { _mapFilter = i; _page = 0; RenderRooms(); });
		map.CustomMinimumSize = new Vector2(160, UiTokens.FieldHeight);
		map.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		row.AddChild(map);

		row.AddChild(UiKit.Check("Só com vaga", _onlyFree, v => { _onlyFree = v; _page = 0; RenderRooms(); }));
		row.AddChild(UiKit.Check("Esconder em jogo", _hidePlaying, v => { _hidePlaying = v; _page = 0; RenderRooms(); }));
		row.AddChild(UiKit.Check("Esconder com senha", _hidePassword, v => { _hidePassword = v; _page = 0; RenderRooms(); }));

		row.AddChild(UiKit.Spacer());

		// Ir direto para uma sala pelo número, para quem combinou fora.
		LineEdit number = UiKit.Field("", "nº", 4);
		number.CustomMinimumSize = new Vector2(64, UiTokens.FieldHeight);
		number.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		number.TextSubmitted += text => GoTo(text);
		row.AddChild(number);
		Button go = UiKit.Button("Ir para sala", ButtonKind.Secondary, () => GoTo(number.Text));
		go.CustomMinimumSize = new Vector2(0, UiTokens.FieldHeight);
		row.AddChild(go);

		Button refresh = UiKit.IconButton(IconKind.Refresh, "Atualizar a lista", () => { _refreshTimer = 0; RenderRooms(); }, 18);
		row.AddChild(refresh);
		_updated = UiKit.Text("", "Caption");
		_updated.CustomMinimumSize = new Vector2(120, 0);
		_updated.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(_updated);

		return row;
	}

	private Control List()
	{
		_grid = new GridContainer
		{
			Columns = 2,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		_grid.AddThemeConstantOverride("h_separation", UiTokens.S2);
		_grid.AddThemeConstantOverride("v_separation", UiTokens.S2);
		return _grid;
	}

	private Control Pager()
	{
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		row.Alignment = BoxContainer.AlignmentMode.Center;

		row.AddChild(UiKit.IconButton(IconKind.Back, "Página anterior", () => { _page--; RenderRooms(); }, 16));
		_pageLabel = UiKit.Text("", "Numeric", UiTokens.TextSecondary);
		_pageLabel.CustomMinimumSize = new Vector2(140, 0);
		_pageLabel.HorizontalAlignment = HorizontalAlignment.Center;
		row.AddChild(_pageLabel);
		row.AddChild(UiKit.IconButton(IconKind.Go, "Próxima página", () => { _page++; RenderRooms(); }, 16));
		return row;
	}

	// ------------------------------------------------------------------ lista

	private IEnumerable<RoomListing> Filtered()
	{
		IEnumerable<RoomListing> rooms = RoomDirectory.Rooms;

		if (_modeFilter > 0) rooms = rooms.Where(r => (int)r.Settings.Mode == _modeFilter - 1);
		if (_mapFilter > 0) rooms = rooms.Where(r => r.Settings.Map == Maps.All[_mapFilter - 1].Id);
		if (_onlyFree) rooms = rooms.Where(r => r.EffectiveState == RoomState.Waiting);
		if (_hidePlaying) rooms = rooms.Where(r => r.EffectiveState != RoomState.Playing);
		if (_hidePassword) rooms = rooms.Where(r => !r.HasPassword);

		// Aguardando primeiro, depois cheias, em jogo por último; dentro
		// de cada grupo, pelo número — ordem estável.
		return rooms.OrderBy(r => r.EffectiveState == RoomState.Waiting ? 0 : r.EffectiveState == RoomState.Full ? 1 : 2)
					.ThenBy(r => r.Settings.Number);
	}

	private void RenderRooms()
	{
		if (_grid == null) return;
		_sinceRefresh = 0;

		UiKit.Clear(_grid);
		List<RoomListing> rooms = Filtered().ToList();

		int pages = Mathf.Max(1, Mathf.CeilToInt(rooms.Count / (float)RoomDirectory.PageSize));
		_page = Mathf.Clamp(_page, 0, pages - 1);
		_pageLabel.Text = $"Página {_page + 1} de {pages}";
		_count.Text = $"Canal local · {RoomDirectory.Rooms.Count} salas · {rooms.Count(r => r.EffectiveState == RoomState.Waiting)} aguardando";

		if (rooms.Count == 0)
		{
			Label empty = UiKit.Paragraph("Nenhuma sala com esses filtros. Crie uma — você é o mestre, e os bots enchem os lugares.");
			_grid.AddChild(empty);
			return;
		}

		foreach (RoomListing room in rooms.Skip(_page * RoomDirectory.PageSize).Take(RoomDirectory.PageSize))
			_grid.AddChild(RoomRow.Make(room, Enter));
	}

	// ------------------------------------------------------------------ painel lateral

	private Control SidePanel()
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column);
		panel.CustomMinimumSize = new Vector2(380, 0);
		panel.AddThemeStyleboxOverride("panel", SideStyle());

		HBoxContainer head = UiKit.Row(UiTokens.S2);
		head.AddChild(UiKit.Caption("Missões do dia"));
		head.AddChild(UiKit.Spacer());
		TimeSpan left = MissionBook.UntilDailyReset(DateTime.Now);
		head.AddChild(UiKit.Text($"troca em {left.Hours} h {left.Minutes:00} min", "Caption"));
		column.AddChild(head);

		Profile.Missions.Refresh(DateTime.Now);
		foreach (MissionProgress p in Profile.Missions.Daily)
		{
			MissionDef def = MissionBook.Def(p.Id);
			if (def == null) continue;
			column.AddChild(MissionLine(def, p));
		}

		column.AddChild(UiKit.Button("Ver todas as missões", ButtonKind.Tertiary, () => Shell.GoMissions()));
		column.AddChild(UiKit.Divider());

		column.AddChild(UiKit.Caption("Canal local"));
		column.AddChild(UiKit.Paragraph(
			"Sem servidor, as salas são locais: você é sempre o mestre, e os outros lugares são bots. " +
			"Senha, faixa de patente e espectador ficam salvos e passam a valer quando houver multijogador."));

		column.AddChild(UiKit.Divider());
		column.AddChild(UiKit.Caption("Sua carreira"));
		LifetimeStats s = Profile.Stats;
		column.AddChild(Stat("Partidas", s.Matches.ToString()));
		column.AddChild(Stat("Vitórias", $"{s.Wins}  ({s.WinRate * 100:0}%)"));
		column.AddChild(Stat("Abates / mortes", $"{s.Kills} / {s.Deaths}"));

		return panel;
	}

	private static StyleBoxFlat SideStyle()
	{
		StyleBoxFlat box = UiTheme.Box(UiTokens.Surface1, UiTokens.Border, 0, 0, 0, 0);
		box.BorderWidthLeft = 1;
		return box;
	}

	public static Control MissionLine(MissionDef def, MissionProgress p)
	{
		VBoxContainer box = UiKit.Column(UiTokens.S1);
		HBoxContainer top = UiKit.Row(UiTokens.S2);
		bool done = p.Progress >= def.Target;
		top.AddChild(Icon.Make(p.Claimed ? IconKind.Check : IconKind.Missions, 14, done ? UiTokens.Success : UiTokens.TextSecondary));
		Label text = UiKit.Truncated(def.Text, null, p.Claimed ? UiTokens.TextDisabled : UiTokens.TextPrimary);
		top.AddChild(text);
		Label count = UiKit.Text($"{p.Progress}/{def.Target}", "Numeric", done ? UiTokens.Success : UiTokens.TextSecondary);
		top.AddChild(count);
		box.AddChild(top);

		var bar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0, 3),
			MaxValue = def.Target,
			Value = p.Progress,
			ShowPercentage = false
		};
		if (done) bar.AddThemeStyleboxOverride("fill", UiTheme.Box(UiTokens.Success, default, 0, 0, 0, 0));
		box.AddChild(bar);
		return box;
	}

	private static Control Stat(string label, string value) => UiKit.Stat(label, value);

	// ------------------------------------------------------------------ ações

	private void Create()
	{
		RoomSettings settings = Profile.LastRoom?.Clone() ?? new RoomSettings();
		if (string.IsNullOrWhiteSpace(settings.Title) || settings.Title == "Sala de treino")
			settings.Title = $"Sala de {Profile.PlayerName}";
		settings.Password = "";

		RoomListing room = RoomDirectory.Create(settings);
		RoomDirectory.Current = room;
		Shell.GoRoom(room);
	}

	private void QuickPlay()
	{
		RoomListing room = RoomDirectory.Rooms.FirstOrDefault(r =>
			r.EffectiveState == RoomState.Waiting && !r.HasPassword);

		if (room == null)
		{
			Create();
			return;
		}

		RoomDirectory.Join(room);
		RoomDirectory.Current = room;
		Shell.GoRoom(room);
	}

	private void GoTo(string text)
	{
		string digits = new string((text ?? "").Where(char.IsDigit).ToArray());
		if (!int.TryParse(digits, out int number) || RoomDirectory.Find(number) is not RoomListing room)
		{
			Shell.Toast($"Sala {text} não existe", UiTokens.Danger);
			Sfx.Ui("ui_negado");
			return;
		}

		Enter(room);
	}

	private void Enter(RoomListing room)
	{
		if (room.EffectiveState == RoomState.Playing)
		{
			Shell.Toast("A partida dessa sala já começou", UiTokens.Danger);
			return;
		}

		if (!room.HasPassword)
		{
			Join(room);
			return;
		}

		LineEdit field = UiKit.Field("", "Senha da sala", 8);
		field.Secret = true;
		Label error = UiKit.Text(" ", "Caption", UiTokens.Danger);
		VBoxContainer body = UiKit.Column(UiTokens.S2);
		body.AddChild(UiKit.Text($"#{room.Settings.Number} · {room.Settings.Title}", "Muted"));
		body.AddChild(field);
		body.AddChild(error);

		Modal modal = null;
		void Try()
		{
			if (field.Text == room.Settings.Password)
			{
				modal?.Close();
				Join(room);
			}
			else
			{
				error.Text = "Senha errada";
				Sfx.Ui("ui_negado");
			}
		}

		field.TextSubmitted += _ => Try();
		modal = Shell.Modal("Sala com senha", body,
			new Modal.Choice("Cancelar", ButtonKind.Secondary, null));

		// O botão Entrar não fecha a janela sozinho: senha errada mantém
		// a janela aberta com o aviso.
		Button enter = UiKit.Button("Entrar", ButtonKind.Primary, Try);
		enter.CustomMinimumSize = new Vector2(120, UiTokens.ButtonHeight);
		body.AddChild(enter);
		field.CallDeferred(Control.MethodName.GrabFocus);
	}

	private void Join(RoomListing room)
	{
		RoomDirectory.Join(room);
		RoomDirectory.Current = room;
		Shell.GoRoom(room);
	}
}
