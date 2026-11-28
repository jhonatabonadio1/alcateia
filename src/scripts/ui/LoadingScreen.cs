using Godot;
using System.Linq;

/// <summary>
/// A tela de carregamento antes da partida (decisão do Jhon, 17/11;
/// refeita em 27/11, "efeitos de loading personalizados e profissionais"):
/// a foto do mapa em tela cheia chegando perto devagar, o nome dele em
/// letra de placa no alto, as regras em etiquetas, os dois times à direita
/// (quem entra com quem) e, no pé, o carregador girando, o que está
/// acontecendo, uma dica que troca e a barra com a porcentagem.
///
/// Ela cobre o tempo de verdade da entrada: o mapa e a cena da partida
/// carregam em segundo plano (a barra é o progresso real do
/// <see cref="ResourceLoader"/>), a cena troca, o <see cref="MatchSetup"/>
/// põe cada um no seu ponto e a navegação dos bots é assada. Só então ela
/// some. Enquanto está na tela, a contagem da partida offline espera
/// (<see cref="Busy"/>).
///
/// Fica pendurada na raiz da árvore, fora da cena, para sobreviver à troca.
/// Quando o servidor passar a servir o mapa e as posições, é aqui que se
/// espera por eles.
///
/// Uso: <c>LoadingScreen.Open(GetTree())</c> no lugar de trocar para
/// scenes/main.tscn, com <see cref="RoomConfig"/> já preenchido.
/// </summary>
public partial class LoadingScreen : CanvasLayer
{
	public const string MatchScene = "res://src/scenes/main.tscn";

	/// <summary>Tempo mínimo na tela, para a foto e o nome serem lidos.</summary>
	private const double MinShown = 2.0;

	/// <summary>Há uma tela de carregamento aberta: a partida ainda não começou.</summary>
	public static bool Busy { get; private set; }

	private enum Step { Loading, Switching, Settling, Fading }

	private string _mapScene;
	private Step _step;
	private double _shown;
	private int _settleFrames;
	private ProgressBar _bar;
	private Label _status;
	private Control _root;
	private readonly Godot.Collections.Array _progress = new();

	public static void Open(SceneTree tree)
	{
		var screen = new LoadingScreen { Layer = 128, Name = "Carregamento" };
		tree.Root.AddChild(screen);
	}

	public override void _Ready()
	{
		Busy = true;
		ProcessMode = ProcessModeEnum.Always;

		RoomSettings rules = RoomConfig.Rules;
		MapInfo map = Maps.Get(rules.Map);
		_mapScene = map.Scene;
		Build(map, rules);

		ResourceLoader.LoadThreadedRequest(_mapScene);
		ResourceLoader.LoadThreadedRequest(MatchScene);
		_step = Step.Loading;
	}

	public override void _ExitTree() => Busy = false;

	public override void _Process(double delta)
	{
		_shown += delta;

		// A dica troca sozinha.
		_tipHeld += delta;
		if (_tipHeld >= TipEvery && _tip != null)
		{
			_tipHeld = 0;
			_tipIndex = (_tipIndex + 1) % Tips.Length;
			_tip.Text = Loc.T(Tips[_tipIndex]);
			Motion.FadeIn(_tip, 0.4f);
		}

		switch (_step)
		{
			case Step.Loading:
			{
				float map = Progress(_mapScene, out bool mapDone);
				float match = Progress(MatchScene, out bool matchDone);
				SetBar(0.85f * (0.8f * map + 0.2f * match), "Carregando o mapa…");
				if (mapDone && matchDone)
				{
					// O mapa fica no cache: o MapSlot da partida o pega pronto.
					ResourceLoader.LoadThreadedGet(_mapScene);
					var scene = (PackedScene)ResourceLoader.LoadThreadedGet(MatchScene);
					SetBar(0.88f, "Posicionando os jogadores…");
					_step = Step.Switching;
					GetTree().ChangeSceneToPacked(scene);
				}
				break;
			}

			case Step.Switching:
				// A cena nova entra no quadro seguinte; o MatchSetup distribui
				// os jogadores e o NavBaker assa a navegação no _Ready deles.
				if (Match.Current != null)
				{
					_step = Step.Settling;
					_settleFrames = 0;
					SetBar(0.95f, "Preparando a partida…");
				}
				break;

			case Step.Settling:
				// Uns quadros para a primeira imagem (sombras, materiais) sair
				// por baixo, e o tempo mínimo para a tela ser lida.
				if (++_settleFrames >= 5 && _shown >= MinShown)
				{
					SetBar(1f, "Pronto");
					_step = Step.Fading;
					Tween fade = CreateTween();
					fade.TweenProperty(_root, "modulate:a", 0f, 0.35f);
					fade.TweenCallback(Callable.From(QueueFree));
					Busy = false;
				}
				break;
		}
	}

	private float Progress(string path, out bool done)
	{
		ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(path, _progress);
		done = status == ResourceLoader.ThreadLoadStatus.Loaded;
		if (status is ResourceLoader.ThreadLoadStatus.Failed or ResourceLoader.ThreadLoadStatus.InvalidResource)
		{
			GD.PushError($"Loading: could not load {path}");
			done = true;
		}
		return done ? 1f : _progress.Count > 0 ? (float)_progress[0] : 0f;
	}

	private void SetBar(float value, string status)
	{
		if (_bar.Value < value * 100) _bar.Value = value * 100;
		_percent.Text = $"{(int)_bar.Value}%";
		_status.Text = Loc.T(status);
	}

	// ------------------------------------------------------------------ montagem

	/// <summary>As dicas que passam embaixo, uma por vez.</summary>
	private static readonly string[] Tips =
	{
		"Virar lobo não custa nada, mas dura 30 segundos: escolha a hora.",
		"O lobo mata em duas garradas e tem só 100 de escudo. Chegue por cima ou pelas costas.",
		"Segure o rugido até o fim e o escudo volta inteiro. Ele entrega onde você está.",
		"O Shift corre por quatro segundos. Sem fôlego, você fica 1,5 s sem correr.",
		"Agachado você não faz barulho de passo. Os bots escutam.",
		"Inimigo só aparece no minimapa enquanto alguém do seu time o enxerga.",
		"O lobo corre na parede: pule contra ela e segure para a frente.",
		"Tiro na cabeça atravessa a resistência do lobo.",
	};

	/// <summary>De quanto em quanto tempo a dica troca, em segundos.</summary>
	private const double TipEvery = 4.5;

	private Label _percent, _tip;
	private int _tipIndex;
	private double _tipHeld;

	private void Build(MapInfo map, RoomSettings rules)
	{
		// O estilo dos menus (letra de placa, papel e ouro), venha a tela de
		// onde vier — da sala ou do "jogar de novo" da partida.
		using var _ = UiTokens.MenuScope();

		_root = new Control { MouseFilter = Control.MouseFilterEnum.Stop, ClipContents = true };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		UiTheme.Apply(_root);
		AddChild(_root);

		var bg = new ColorRect { Color = UiTokens.Bg };
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(bg);

		// A foto em tela cheia, chegando perto devagar: escurecida de baixo
		// (onde fica a barra) e da esquerda (onde fica o nome).
		Control photo = UiKit.Fill(Deco.Photo(map.Picture, 0.9f, fromLeft: true, drift: 0.09f));
		_root.AddChild(photo);
		_root.AddChild(UiKit.Fill(Deco.Photo(null, 0.96f, fromLeft: false, drift: 0f)));

		// O nome do lugar, no alto à esquerda.
		var text = new VBoxContainer();
		text.AddThemeConstantOverride("separation", 0);
		text.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
		text.OffsetLeft = UiTokens.S7 + UiTokens.S3;
		text.OffsetTop = UiTokens.S7;
		_root.AddChild(text);

		PlayModes.Info mode = PlayModes.Of(rules.Play);
		string game = rules.Mode == GameMode.Equipe ? "Mata-mata em equipe" : RoomSettings.ModeName(rules.Mode);
		text.AddChild(Deco.Kicker($"{Loc.T(game)} · {Loc.T(mode.Name)}"));
		Label name = Deco.Big(map.Name, 132);
		text.AddChild(name);
		Control underline = Deco.Hairline(UiTokens.Accent, 3);
		underline.CustomMinimumSize = new Vector2(420, 3);
		underline.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		text.AddChild(underline);
		text.AddChild(UiKit.Gap(UiTokens.S3));

		HBoxContainer tags = UiKit.Row(UiTokens.S2);
		foreach (string tag in new[] { Loc.F("{0} contra {1}", rules.TeamSize, rules.TeamSize), Loc.F("{0} pontos", rules.ScoreToWin), Loc.F("{0} min", rules.Minutes), map.Size })
			tags.AddChild(Deco.Tag(tag, new Color(UiTokens.Paper, 0.14f), UiTokens.TextPrimary));
		text.AddChild(tags);
		text.AddChild(UiKit.Gap(UiTokens.S3));

		Label description = UiKit.Paragraph(map.Description, "Muted");
		description.CustomMinimumSize = new Vector2(560, 0);
		description.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		description.AddThemeColorOverride("font_color", new Color(UiTokens.TextPrimary, 0.8f));
		text.AddChild(description);

		// Os dois times, à direita: quem entra com quem.
		Control rosters = Rosters();
		if (rosters != null) _root.AddChild(rosters);

		// O pé: o carregador, o que está acontecendo, a dica e a barra.
		var foot = new VBoxContainer();
		foot.AddThemeConstantOverride("separation", UiTokens.S2);
		foot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
		foot.GrowVertical = Control.GrowDirection.Begin;
		foot.OffsetLeft = UiTokens.S7 + UiTokens.S3;
		foot.OffsetRight = -UiTokens.S7 - UiTokens.S3;
		foot.OffsetBottom = -UiTokens.S6;
		_root.AddChild(foot);

		HBoxContainer line = UiKit.Row(UiTokens.S3);
		line.AddChild(Loader.Make(40));
		VBoxContainer state = UiKit.Column(0);
		state.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		Label loading = Deco.Big("Carregando", 26);
		state.AddChild(loading);
		_status = UiKit.Text("Carregando o mapa…", "Caption", UiTokens.TextSecondary);
		state.AddChild(_status);
		line.AddChild(state);

		_tip = UiKit.Text(Loc.T(Tips[0]), null, new Color(UiTokens.TextPrimary, 0.85f));
		_tip.HorizontalAlignment = HorizontalAlignment.Right;
		_tip.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
		_tipIndex = (int)(Time.GetTicksMsec() % (ulong)Tips.Length);
		_tip.Text = Loc.T(Tips[_tipIndex]);
		line.AddChild(_tip);

		_percent = UiKit.Text("0%", "Numeric", UiTokens.Accent);
		_percent.AddThemeFontOverride("font", UiFonts.NumericDisplay);
		_percent.AddThemeFontSizeOverride("font_size", 34);
		_percent.CustomMinimumSize = new Vector2(84, 0);
		_percent.HorizontalAlignment = HorizontalAlignment.Right;
		_percent.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
		line.AddChild(_percent);
		foot.AddChild(line);

		_bar = new ProgressBar { MinValue = 0, MaxValue = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 4) };
		_bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(UiTokens.Paper, 0.16f) });
		_bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = UiTokens.Accent });
		foot.AddChild(_bar);

		// A entrada: a tela vem do escuro, o nome e o pé deslizam.
		Motion.FadeIn(_root, 0.35f);
		Motion.SlideIn(text, new Vector2(-60, 0), 0.6f, 0.1f);
		Motion.FadeIn(foot, 0.5f, 0.3f);
	}

	/// <summary>Os membros da sala em duas colunas, cada time com a sua cor.
	/// Sem sala montada (a cena aberta direto), não aparece.</summary>
	private static Control Rosters()
	{
		if (RoomConfig.Members.Count == 0) return null;

		HBoxContainer row = UiKit.Row(UiTokens.S4);
		row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
		row.GrowHorizontal = Control.GrowDirection.Begin;
		row.OffsetRight = -UiTokens.S7 - UiTokens.S3;
		row.OffsetTop = UiTokens.S7 + UiTokens.S3;

		foreach (int team in new[] { Teams.Blue, Teams.Orange })
		{
			Color color = Teams.ColorOf(team);
			VBoxContainer column = UiKit.Column(UiTokens.S1);
			column.CustomMinimumSize = new Vector2(250, 0);
			column.AddChild(Deco.Big(Teams.NameOf(team), 26, color.Lerp(Colors.White, 0.35f)));
			var bar = new ColorRect { Color = color, CustomMinimumSize = new Vector2(0, 2) };
			column.AddChild(bar);

			int index = 0;
			foreach (RoomMember member in RoomConfig.Members)
			{
				if (member.Team != team) continue;
				var plate = new PanelContainer { CustomMinimumSize = new Vector2(0, 38) };
				var box = new StyleBoxFlat { BgColor = new Color(0.02f, 0.035f, 0.055f, 0.7f), BorderColor = color };
				box.BorderWidthLeft = 3;
				box.ContentMarginLeft = UiTokens.S2 + 2;
				box.ContentMarginRight = UiTokens.S2;
				plate.AddThemeStyleboxOverride("panel", box);
				HBoxContainer line = UiKit.Row(UiTokens.S2);
				plate.AddChild(line);
				if (member.IsBot) line.AddChild(Icon.Make(IconKind.Bot, 18, UiTokens.TextDisabled));
				else if (member.Rank >= 0) line.AddChild(LevelChip.Make(member.Rank, 18));
				Label who = UiKit.Truncated(member.Name, null, member.IsBot ? UiTokens.TextSecondary : UiTokens.TextPrimary);
				who.AddThemeFontOverride("font", UiFonts.DisplaySemi);
				who.AddThemeFontSizeOverride("font_size", 20);
				line.AddChild(who);
				column.AddChild(plate);
				Motion.FadeIn(plate, 0.3f, 0.35f + 0.05f * index++);
			}
			row.AddChild(column);
		}
		return row;
	}
}
