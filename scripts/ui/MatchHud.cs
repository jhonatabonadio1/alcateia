using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// O que a partida mostra: placar e relógio no topo, contagem no
/// centro, tabela no Tab e a tela de fim.
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

    private Match _match;
    private int _localTeam = Teams.Blue;

    private Control _root;
    private Label _blueScore;
    private Label _redScore;
    private Label _clock;
    private ColorRect _blueFill;
    private ColorRect _redFill;

    private Control _countdown;
    private Label _countdownNumber;
    private Label _countdownTitle;

    private Control _board;
    private GridContainer _blueRows;
    private GridContainer _redRows;
    private Label _blueHeader;
    private Label _redHeader;

    private Control _endScreen;
    private Label _endTitle;
    private Label _endScore;
    private Label _endMvp;

    private double _goTimer;
    private double _boardRefresh;

    private const float BarWidth = 150f;

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

        var local = GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<Health>("Health");
        if (local != null) _localTeam = local.Team;

        Build();

        _match.PhaseChanged += OnPhaseChanged;
        OnPhaseChanged((int)_match.Phase);
    }

    public override void _Process(double delta)
    {
        _blueScore.Text = _match.ScoreOf(Teams.Blue).ToString();
        _redScore.Text = _match.ScoreOf(Teams.Red).ToString();

        _blueFill.Size = new Vector2(BarWidth * Ratio(Teams.Blue), 4);
        _redFill.Size = new Vector2(BarWidth * Ratio(Teams.Red), 4);
        _redFill.Position = new Vector2(BarWidth - _redFill.Size.X, 0);

        double time = _match.TimeLeft;
        _clock.Text = Clock(time);
        _clock.AddThemeColorOverride("font_color",
            _match.Phase == MatchPhase.Live && time <= LastMinute
                ? new Color(1f, 0.35f, 0.3f)
                : Colors.White);

        UpdateCountdown(delta);

        bool showBoard = _match.Phase == MatchPhase.Ended || Input.IsActionPressed("scoreboard");
        _board.Visible = showBoard;

        if (showBoard)
        {
            _boardRefresh -= delta;
            if (_boardRefresh <= 0)
            {
                _boardRefresh = 0.25;
                RefreshBoard();
            }
        }
    }

    private float Ratio(int team) =>
        Mathf.Clamp(_match.ScoreOf(team) / (float)Mathf.Max(_match.ScoreToWin, 1), 0f, 1f);

    private static string Clock(double seconds)
    {
        int total = Mathf.CeilToInt((float)Mathf.Max(seconds, 0));
        return $"{total / 60:00}:{total % 60:00}";
    }

    // ------------------------------------------------------------------
    // Fases

    private void OnPhaseChanged(int phase)
    {
        var current = (MatchPhase)phase;

        _countdown.Visible = current == MatchPhase.Countdown;
        _endScreen.Visible = current == MatchPhase.Ended;

        if (current == MatchPhase.Live)
            _goTimer = GoFlash;

        if (current == MatchPhase.Ended)
            FillEndScreen();
    }

    private void UpdateCountdown(double delta)
    {
        if (_match.Phase == MatchPhase.Countdown)
        {
            _countdownTitle.Text = "A PARTIDA COMEÇA EM";
            _countdownNumber.Text = Mathf.CeilToInt((float)_match.CountdownLeft).ToString();
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
            _countdownNumber.Modulate = new Color(1, 1, 1, Mathf.Clamp((float)_goTimer / 0.5f, 0f, 1f));

            if (_goTimer <= 0)
            {
                _countdown.Visible = false;
                _countdownNumber.Modulate = Colors.White;
            }
        }
    }

    private void FillEndScreen()
    {
        int winner = _match.Winner;

        if (winner == 0)
        {
            _endTitle.Text = "EMPATE";
            _endTitle.AddThemeColorOverride("font_color", Colors.White);
        }
        else
        {
            bool won = winner == _localTeam;
            _endTitle.Text = won ? "VITÓRIA" : "DERROTA";
            _endTitle.AddThemeColorOverride("font_color",
                won ? Teams.ColorOf(_localTeam) : new Color(0.75f, 0.75f, 0.78f));
        }

        string how = _match.EndedByScore ? $"primeiro a {_match.ScoreToWin}" : "fim do tempo";
        _endScore.Text = $"AZUL  {_match.ScoreOf(Teams.Blue)}  x  {_match.ScoreOf(Teams.Red)}  VERMELHO      ({how})";

        CombatantStats mvp = _match.Combatants
            .OrderByDescending(c => c.Kills)
            .ThenBy(c => c.Deaths)
            .FirstOrDefault();

        _endMvp.Text = mvp != null && mvp.Kills > 0
            ? $"Destaque: {mvp.Name} — {mvp.Kills} abates, {mvp.Deaths} mortes"
            : "";

        RefreshBoard();
    }

    // ------------------------------------------------------------------
    // Tabela

    private void RefreshBoard()
    {
        _blueHeader.Text = $"AZUL  {_match.ScoreOf(Teams.Blue)}";
        _redHeader.Text = $"VERMELHO  {_match.ScoreOf(Teams.Red)}";

        Fill(_blueRows, Teams.Blue);
        Fill(_redRows, Teams.Red);
    }

    private void Fill(GridContainer grid, int team)
    {
        foreach (Node child in grid.GetChildren())
        {
            grid.RemoveChild(child);
            child.QueueFree();
        }

        AddCell(grid, "", 14, new Color(0.7f, 0.7f, 0.72f), HorizontalAlignment.Left);
        AddCell(grid, "ABATES", 12, new Color(0.7f, 0.7f, 0.72f), HorizontalAlignment.Right);
        AddCell(grid, "MORTES", 12, new Color(0.7f, 0.7f, 0.72f), HorizontalAlignment.Right);

        IEnumerable<CombatantStats> rows = _match.Combatants
            .Where(c => c.Team == team)
            .OrderByDescending(c => c.Kills)
            .ThenBy(c => c.Deaths);

        foreach (CombatantStats row in rows)
        {
            // Você em amarelo: numa tabela de oito nomes, a primeira
            // coisa que se procura é a própria linha.
            Color color = row.IsLocal ? new Color(1f, 0.85f, 0.3f) : Colors.White;

            AddCell(grid, row.Name, 18, color, HorizontalAlignment.Left);
            AddCell(grid, row.Kills.ToString(), 18, color, HorizontalAlignment.Right);
            AddCell(grid, row.Deaths.ToString(), 18, color, HorizontalAlignment.Right);
        }
    }

    private static void AddCell(GridContainer grid, string text, int size, Color color,
        HorizontalAlignment align)
    {
        var label = MakeLabel(text, size, color);
        label.HorizontalAlignment = align;
        label.CustomMinimumSize = new Vector2(align == HorizontalAlignment.Left ? 150 : 70, 0);
        grid.AddChild(label);
    }

    // ------------------------------------------------------------------
    // Montagem

    private void Build()
    {
        Layer = 8;

        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.AnchorRight = 1f;
        _root.AnchorBottom = 1f;
        AddChild(_root);

        BuildTopBar();
        BuildCountdown();
        BuildEndScreen();
        BuildBoard();
    }

    private void BuildTopBar()
    {
        var bar = new HBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            AnchorTop = 0f, AnchorBottom = 0f,
            OffsetTop = 12,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.End,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        bar.AddThemeConstantOverride("separation", 6);
        _root.AddChild(bar);

        bar.AddChild(TeamBox(Teams.Blue, out _blueScore, out _blueFill));

        var middle = MakePanel(new Color(0.06f, 0.07f, 0.09f, 0.82f));
        var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 0);
        middle.AddChild(stack);

        _clock = MakeLabel("10:00", 30, Colors.White);
        _clock.HorizontalAlignment = HorizontalAlignment.Center;
        _clock.CustomMinimumSize = new Vector2(110, 0);
        stack.AddChild(_clock);

        var goal = MakeLabel($"primeiro a {_match.ScoreToWin}", 12, new Color(0.7f, 0.7f, 0.72f));
        goal.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(goal);

        bar.AddChild(middle);
        bar.AddChild(TeamBox(Teams.Red, out _redScore, out _redFill));
    }

    /// <summary>Caixa de um time: nome, pontos e a barra até a vitória.
    /// A barra existe porque "27" não diz o quanto falta; um traço a dois
    /// terços do caminho diz.</summary>
    private Control TeamBox(int team, out Label score, out ColorRect fill)
    {
        Color color = Teams.ColorOf(team);

        var panel = MakePanel(new Color(color.R * 0.25f, color.G * 0.25f, color.B * 0.25f, 0.82f));
        var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 2);
        panel.AddChild(stack);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        stack.AddChild(row);

        var name = MakeLabel(Teams.NameOf(team), 14, color);
        name.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        score = MakeLabel("0", 32, Colors.White);
        score.CustomMinimumSize = new Vector2(48, 0);

        // Azul lê da esquerda para o centro, vermelho do centro para a
        // direita: os números ficam colados no relógio, que é para onde
        // o olho vai.
        if (team == Teams.Blue)
        {
            score.HorizontalAlignment = HorizontalAlignment.Right;
            row.AddChild(name);
            row.AddChild(score);
        }
        else
        {
            score.HorizontalAlignment = HorizontalAlignment.Left;
            row.AddChild(score);
            row.AddChild(name);
        }

        var track = new Control
        {
            CustomMinimumSize = new Vector2(BarWidth, 4),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        stack.AddChild(track);

        track.AddChild(new ColorRect
        {
            Color = new Color(1, 1, 1, 0.12f),
            Size = new Vector2(BarWidth, 4),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        fill = new ColorRect
        {
            Color = color,
            Size = new Vector2(0, 4),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        track.AddChild(fill);

        return panel;
    }

    private void BuildCountdown()
    {
        var box = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            AnchorTop = 0.32f, AnchorBottom = 0.32f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.AddChild(box);
        _countdown = box;

        _countdownTitle = MakeLabel("A PARTIDA COMEÇA EM", 24, new Color(0.85f, 0.85f, 0.88f));
        _countdownTitle.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(_countdownTitle);

        _countdownNumber = MakeLabel("10", 110, Colors.White);
        _countdownNumber.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(_countdownNumber);
    }

    private void BuildEndScreen()
    {
        var dim = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.62f),
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.AddChild(dim);
        _endScreen = dim;

        var box = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            AnchorTop = 0.16f, AnchorBottom = 0.16f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.End,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        box.AddThemeConstantOverride("separation", 6);
        dim.AddChild(box);

        _endTitle = MakeLabel("", 84, Colors.White);
        _endTitle.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(_endTitle);

        _endScore = MakeLabel("", 24, Colors.White);
        _endScore.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(_endScore);

        _endMvp = MakeLabel("", 18, new Color(1f, 0.85f, 0.3f));
        _endMvp.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(_endMvp);

        var hint = MakeLabel("ENTER para jogar de novo", 16, new Color(0.75f, 0.75f, 0.78f));
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.AnchorLeft = 0.5f;
        hint.AnchorRight = 0.5f;
        hint.AnchorTop = 0.9f;
        hint.AnchorBottom = 0.9f;
        hint.GrowHorizontal = Control.GrowDirection.Both;
        dim.AddChild(hint);
    }

    private void BuildBoard()
    {
        var holder = new HBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            AnchorTop = 0.5f, AnchorBottom = 0.5f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        holder.AddThemeConstantOverride("separation", 14);
        _root.AddChild(holder);
        _board = holder;

        holder.AddChild(TeamColumn(Teams.Blue, out _blueHeader, out _blueRows));
        holder.AddChild(TeamColumn(Teams.Red, out _redHeader, out _redRows));
    }

    private Control TeamColumn(int team, out Label header, out GridContainer rows)
    {
        Color color = Teams.ColorOf(team);

        var panel = MakePanel(new Color(0.05f, 0.06f, 0.08f, 0.88f));
        var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 8);
        panel.AddChild(stack);

        header = MakeLabel(Teams.NameOf(team), 22, color);
        stack.AddChild(header);

        stack.AddChild(new ColorRect
        {
            Color = color,
            CustomMinimumSize = new Vector2(0, 2),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        rows = new GridContainer { Columns = 3, MouseFilter = Control.MouseFilterEnum.Ignore };
        rows.AddThemeConstantOverride("h_separation", 12);
        rows.AddThemeConstantOverride("v_separation", 4);
        stack.AddChild(rows);

        return panel;
    }

    // ------------------------------------------------------------------

    private static PanelContainer MakePanel(Color background)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 6,
            ContentMarginBottom = 8
        };
        style.SetCornerRadiusAll(6);

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        label.AddThemeConstantOverride("outline_size", Mathf.Max(4, size / 8));
        return label;
    }
}
