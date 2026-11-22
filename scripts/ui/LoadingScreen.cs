using Godot;
using System.Linq;

/// <summary>
/// A tela de carregamento antes da partida (decisão do Jhon, 17/11): a foto
/// do mapa em tela cheia, o nome dele, uma linha com o modo, o tempo, os
/// pontos e o tamanho dos times, a descrição curta e a barra.
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
	public const string MatchScene = "res://scenes/main.tscn";

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
		_status.Text = status;
	}

	// ------------------------------------------------------------------ montagem

	private void Build(MapInfo map, RoomSettings rules)
	{
		_root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		var bg = new ColorRect { Color = UiTokens.Bg };
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(bg);

		// A foto em tela cheia.
		string picture = map.Picture;
		var photo = new TextureRect
		{
			Texture = ResourceLoader.Exists(picture) ? GD.Load<Texture2D>(picture) : null,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
		};
		photo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(photo);

		// Escurece de baixo para cima, onde fica o texto.
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(UiTokens.Bg, 0.95f));
		gradient.SetColor(1, new Color(UiTokens.Bg, 0f));
		gradient.SetOffset(1, 0.6f);
		var shade = new TextureRect
		{
			Texture = new GradientTexture2D { Gradient = gradient, FillFrom = new Vector2(0, 1), FillTo = new Vector2(0, 0) },
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale
		};
		shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(shade);

		// O texto, embaixo à esquerda.
		var text = new VBoxContainer();
		text.AddThemeConstantOverride("separation", UiTokens.S2);
		text.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
		text.GrowVertical = Control.GrowDirection.Begin;
		text.OffsetLeft = UiTokens.S7;
		text.OffsetBottom = -UiTokens.S7 - UiTokens.S4;
		text.CustomMinimumSize = new Vector2(760, 0);
		_root.AddChild(text);

		Label name = UiKit.Text(Loc.Upper(map.Name), "Title", UiTokens.TextPrimary);
		name.AddThemeFontOverride("font", UiFonts.Display);
		name.AddThemeFontSizeOverride("font_size", 72);
		text.AddChild(name);

		PlayModes.Info mode = PlayModes.Of(rules.Play);
		string game = rules.Mode == GameMode.Equipe ? "Mata-mata em equipe" : RoomSettings.ModeName(rules.Mode);
		string line = string.Join("  ·  ", new[]
		{
			Loc.T(game), Loc.T(mode.Name), Loc.F("{0} min", rules.Minutes), Loc.F("{0} pontos", rules.ScoreToWin), Loc.F("{0} contra {1}", rules.TeamSize, rules.TeamSize)
		}.Where(s => !string.IsNullOrEmpty(s)));
		Label rulesLine = UiKit.Text(line.ToUpperInvariant(), "Subtitle", UiTokens.Accent);
		rulesLine.AddThemeFontOverride("font", UiFonts.DisplaySemi);
		rulesLine.AddThemeFontSizeOverride("font_size", 24);
		text.AddChild(rulesLine);

		Label description = UiKit.Paragraph(map.Description, "Muted");
		description.CustomMinimumSize = new Vector2(620, 0);
		description.AddThemeColorOverride("font_color", UiTokens.TextSecondary);
		text.AddChild(description);

		// A barra, de ponta a ponta embaixo, com o que está acontecendo.
		var foot = new VBoxContainer();
		foot.AddThemeConstantOverride("separation", UiTokens.S1);
		foot.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		foot.GrowVertical = Control.GrowDirection.Begin;
		foot.OffsetLeft = UiTokens.S7;
		foot.OffsetRight = -UiTokens.S7;
		foot.OffsetBottom = -UiTokens.S5;
		_root.AddChild(foot);

		_status = UiKit.Text("Carregando o mapa…", "Caption", UiTokens.TextSecondary);
		foot.AddChild(_status);

		_bar = new ProgressBar { MinValue = 0, MaxValue = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 4) };
		_bar.AddThemeStyleboxOverride("background", UiTheme.Box(new Color(1, 1, 1, 0.12f), default, 0, 0, 0, 0));
		_bar.AddThemeStyleboxOverride("fill", UiTheme.Box(UiTokens.Accent, default, 0, 0, 0, 0));
		foot.AddChild(_bar);
	}
}
