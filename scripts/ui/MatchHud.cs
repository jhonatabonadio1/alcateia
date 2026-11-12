using Godot;

/// <summary>
/// O que a partida mostra: placar, relógio e modo no topo; a contagem no
/// centro; o placar completo no Tab; e, no fim, a tela de resultado
/// (<see cref="ResultScreen"/>).
///
/// Separado do Hud de propósito. O Hud fala do corpo — vida, munição,
/// forma, quem te acertou. Este fala da partida, e some inteiro numa
/// cena sem partida, como o treino.
///
/// Só lê. Quem conta pontos e tempo é o <see cref="Match"/>; se esta
/// tela sumisse, a partida continuaria igual.
/// </summary>
public partial class MatchHud : CanvasLayer
{
    /// <summary>Quanto tempo o "VALENDO!" fica na tela.</summary>
    [Export] public float GoFlash = 1.4f;

    /// <summary>Abaixo disto o relógio fica vermelho.</summary>
    [Export] public float LastMinute = 60f;

    private const float BarWidth = 180f;

    private Match _match;
    private int _localTeam = Teams.Blue;

    private Control _root;
    private Label _blueScore;
    private Label _orangeScore;
    private ProgressBar _blueBar;
    private ProgressBar _orangeBar;
    private Label _clock;

    private Control _countdown;
    private Label _countdownTitle;
    private Label _countdownNumber;
    private int _lastCount = -1;

    private Control _board;
    private Scoreboard _scoreboard;
    private Label _boardInfo;

    private double _goTimer;
    private double _boardRefresh;

    /// <summary>Desenvolvimento: <c>-- --placar</c> deixa o Tab aberto,
    /// para fotografar.</summary>
    private readonly bool _forceBoard = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--placar") >= 0;

    public override void _Ready()
    {
        _match = Match.Current;

        // Sem partida na cena, não há o que mostrar.
        if (_match == null)
        {
            Visible = false;
            SetProcess(false);
            return;
        }

        Layer = 8;

        var local = GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<Health>("Health");
        if (local != null) _localTeam = local.Team;

        _root = UiTheme.Apply(UiKit.Fill(new Control { MouseFilter = Control.MouseFilterEnum.Ignore }));
        AddChild(_root);

        BuildTopBar();
        BuildCountdown();
        BuildBoard();

        _match.PhaseChanged += OnPhaseChanged;
        _match.ScoreChanged += OnScoreChanged;
        OnScoreChanged(0, 0);
        OnPhaseChanged((int)_match.Phase);
    }

    public override void _Process(double delta)
    {
        double time = _match.TimeLeft;
        _clock.Text = Clock(time);
        bool late = _match.Phase == MatchPhase.Live && time <= LastMinute;
        _clock.AddThemeColorOverride("font_color", late ? UiTokens.Danger : UiTokens.TextPrimary);

        UpdateCountdown(delta);

        bool showBoard = _match.Phase != MatchPhase.Ended && (Input.IsActionPressed("scoreboard") || _forceBoard);
        if (showBoard && !_board.Visible) _boardRefresh = 0;
        _board.Visible = showBoard;

        if (showBoard)
        {
            _boardRefresh -= delta;
            if (_boardRefresh <= 0)
            {
                _boardRefresh = 0.25;
                _scoreboard.Refresh(_match);
                _boardInfo.Text = $"{Maps.Get(RoomConfig.Rules.Map).Name}  ·  {RoomSettings.ModeName(RoomConfig.Rules.Mode)}  ·  primeiro a {_match.ScoreToWin}  ·  {Clock(time)}";
            }
        }
    }

    private static string Clock(double seconds)
    {
        int total = Mathf.CeilToInt((float)Mathf.Max(seconds, 0));
        return $"{total / 60:00}:{total % 60:00}";
    }

    // ================================================================== eventos

    private void OnScoreChanged(int blue, int orange)
    {
        blue = _match.ScoreOf(Teams.Blue);
        orange = _match.ScoreOf(Teams.Orange);

        if (_blueScore.Text != blue.ToString() && _blueScore.Text != "") Motion.Pop(_blueScore, 1.25f);
        if (_orangeScore.Text != orange.ToString() && _orangeScore.Text != "") Motion.Pop(_orangeScore, 1.25f);

        _blueScore.Text = blue.ToString();
        _orangeScore.Text = orange.ToString();

        float goal = Mathf.Max(_match.ScoreToWin, 1);
        _blueBar.Value = blue / goal;
        _orangeBar.Value = orange / goal;
    }

    private void OnPhaseChanged(int phase)
    {
        var current = (MatchPhase)phase;

        _countdown.Visible = current == MatchPhase.Countdown;

        if (current == MatchPhase.Live)
            _goTimer = GoFlash;

        if (current == MatchPhase.Ended)
        {
            _root.Visible = false;
            ResultScreen.Open(this, _match, _localTeam);
        }
    }

    private void UpdateCountdown(double delta)
    {
        if (_match.Phase == MatchPhase.Countdown)
        {
            // Com a escolha de conjunto aberta, ela tem o próprio relógio;
            // o número grande atrás dos cartões só atrapalharia a leitura.
            _countdown.Visible = !UiState.AnyOpen;

            int n = Mathf.CeilToInt((float)_match.CountdownLeft);
            _countdownTitle.Text = "A PARTIDA COMEÇA EM";
            _countdownNumber.Text = n.ToString();
            _countdownNumber.AddThemeColorOverride("font_color", n <= 3 ? UiTokens.Accent : UiTokens.TextPrimary);
            if (n != _lastCount && n <= 3) Motion.Pop(_countdownNumber, 1.2f);
            _lastCount = n;
            return;
        }

        // Um instante de "VALENDO!" depois do zero. Sem ele a passagem
        // da contagem para o jogo não tem marca nenhuma, e o primeiro
        // segundo de partida é gasto descobrindo que já pode andar.
        if (_goTimer > 0)
        {
            _goTimer -= delta;
            _countdown.Visible = true;
            _countdownTitle.Text = "";
            _countdownNumber.Text = "VALENDO!";
            _countdownNumber.AddThemeColorOverride("font_color", UiTokens.Accent);
            _countdownNumber.Modulate = new Color(1, 1, 1, Mathf.Clamp((float)_goTimer / 0.5f, 0f, 1f));

            if (_goTimer <= 0)
            {
                _countdown.Visible = false;
                _countdownNumber.Modulate = Colors.White;
            }
        }
    }

    // ================================================================== montagem

    private void BuildTopBar()
    {
        HBoxContainer bar = UiKit.Row(UiTokens.S1);
        bar.MouseFilter = Control.MouseFilterEnum.Ignore;
        bar.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        bar.GrowHorizontal = Control.GrowDirection.Both;
        bar.OffsetTop = UiTokens.S3;
        _root.AddChild(bar);

        bar.AddChild(TeamBox(Teams.Blue, out _blueScore, out _blueBar));

        var middle = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        middle.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(UiTokens.Bg, 0.8f), new Color(1, 1, 1, 0.08f), 1, 0, UiTokens.S3, UiTokens.S1));
        VBoxContainer stack = UiKit.Column(0);
        middle.AddChild(stack);

        _clock = UiKit.Text("10:00", "NumericLarge");
        _clock.AddThemeFontSizeOverride("font_size", 34);
        _clock.HorizontalAlignment = HorizontalAlignment.Center;
        _clock.CustomMinimumSize = new Vector2(120, 0);
        stack.AddChild(_clock);

        Label mode = UiKit.Caption($"{RoomSettings.ModeName(RoomConfig.Rules.Mode)} · {_match.ScoreToWin}");
        mode.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(mode);

        bar.AddChild(middle);
        bar.AddChild(TeamBox(Teams.Orange, out _orangeScore, out _orangeBar));
    }

    /// <summary>Caixa de um time: nome, pontos e a barra até a vitória.
    /// A barra existe porque "27" não diz o quanto falta; um traço a dois
    /// terços do caminho diz.</summary>
    private Control TeamBox(int team, out Label score, out ProgressBar fill)
    {
        Color color = Teams.ColorOf(team);
        bool mine = team == _localTeam;

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        StyleBoxFlat style = UiTheme.Box(new Color(color.Darkened(0.7f), 0.85f), mine ? color : new Color(color, 0.3f), 1, 0, UiTokens.S3, UiTokens.S1);
        panel.AddThemeStyleboxOverride("panel", style);

        VBoxContainer stack = UiKit.Column(2);
        panel.AddChild(stack);

        HBoxContainer row = UiKit.Row(UiTokens.S2);
        stack.AddChild(row);

        Label name = UiKit.Text(Teams.NameOf(team) + (mine ? " · VOCÊ" : ""), "Caption", color);
        name.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        score = UiKit.Text("", "NumericLarge");
        score.AddThemeFontSizeOverride("font_size", 34);
        score.CustomMinimumSize = new Vector2(52, 0);
        score.PivotOffset = new Vector2(26, 20);

        // Azul lê da esquerda para o centro, laranja do centro para a
        // direita: os números ficam colados no relógio, que é para onde o
        // olho vai.
        if (team == Teams.Blue)
        {
            score.HorizontalAlignment = HorizontalAlignment.Right;
            row.AddChild(name);
            row.AddChild(score);
        }
        else
        {
            score.HorizontalAlignment = HorizontalAlignment.Left;
            name.HorizontalAlignment = HorizontalAlignment.Right;
            row.AddChild(score);
            row.AddChild(name);
        }

        fill = new ProgressBar
        {
            CustomMinimumSize = new Vector2(BarWidth, 4),
            MaxValue = 1,
            Step = 0.001,
            ShowPercentage = false,
            FillMode = team == Teams.Blue ? (int)ProgressBar.FillModeEnum.EndToBegin : (int)ProgressBar.FillModeEnum.BeginToEnd,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        fill.AddThemeStyleboxOverride("background", UiTheme.Box(new Color(1, 1, 1, 0.12f), default, 0, 0, 0, 0));
        fill.AddThemeStyleboxOverride("fill", UiTheme.Box(color, default, 0, 0, 0, 0));
        stack.AddChild(fill);

        return panel;
    }

    private void BuildCountdown()
    {
        VBoxContainer box = UiKit.Column(0);
        box.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        box.AnchorTop = box.AnchorBottom = 0.28f;
        box.GrowHorizontal = Control.GrowDirection.Both;
        box.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(box);
        _countdown = box;

        _countdownTitle = UiKit.Text("A PARTIDA COMEÇA EM", "Subtitle", UiTokens.TextSecondary);
        _countdownTitle.HorizontalAlignment = HorizontalAlignment.Center;
        Outline(_countdownTitle, 6);
        box.AddChild(_countdownTitle);

        _countdownNumber = UiKit.Text("10", "Display");
        _countdownNumber.AddThemeFontSizeOverride("font_size", 120);
        _countdownNumber.HorizontalAlignment = HorizontalAlignment.Center;
        _countdownNumber.PivotOffset = new Vector2(150, 70);
        Outline(_countdownNumber, 10);
        box.AddChild(_countdownNumber);

        Label hint = UiKit.Text("Segure Tab para ver o placar", "Muted");
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        Outline(hint, 5);
        box.AddChild(hint);
    }

    private void BuildBoard()
    {
        var dim = UiKit.Fill(new ColorRect { Color = new Color(UiTokens.Bg, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore });
        _root.AddChild(dim);
        _board = dim;
        _board.Visible = false;

        var center = UiKit.Fill(new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore });
        dim.AddChild(center);

        VBoxContainer column = UiKit.Column(UiTokens.S3);
        column.MouseFilter = Control.MouseFilterEnum.Ignore;
        center.AddChild(column);

        _boardInfo = UiKit.Caption("", UiTokens.TextPrimary);
        _boardInfo.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_boardInfo);

        _scoreboard = new Scoreboard();
        column.AddChild(_scoreboard);
    }

    private static void Outline(Label label, int size)
    {
        label.AddThemeConstantOverride("outline_size", size);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
    }
}
