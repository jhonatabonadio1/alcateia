using Godot;

/// <summary>
/// Escolha de lote, no início e a cada morte.
///
/// Dez segundos para decidir. O relógio existe para a escolha ser uma
/// leitura rápida da situação, não um planejamento: quem está morto
/// precisa voltar à luta, e deixar o jogador parado no menu é tirá-lo
/// do jogo duas vezes.
///
/// Estourado o tempo, entra o lote padrão. Nunca trava esperando
/// decisão — num jogo de ritmo rápido, indecisão não pode custar a
/// partida inteira.
///
/// Não pausa a árvore: o mundo segue enquanto você escolhe, como no
/// jogo que serviu de referência.
/// </summary>
public partial class LoadoutMenu : CanvasLayer
{
    [Export] public float ChooseTime = 10f;

    [Export] public Loadout[] Loadouts = System.Array.Empty<Loadout>();

    private Control _root;
    private Label _timer;
    private HBoxContainer _cards;

    private Health _health;
    private Weapon _weapon;
    private FormController _form;

    private double _remaining;
    private bool _open;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        Node player = GetTree().GetFirstNodeInGroup("player");

        if (player == null)
        {
            GD.PrintErr("LoadoutMenu: nenhum jogador na cena.");
            return;
        }

        _health = player.GetNode<Health>("Health");
        _weapon = player.GetNode<Weapon>("Weapon");
        _form = player.GetNode<FormController>("FormController");

        // O jogador controla o próprio renascimento: só volta quando
        // tiver escolhido o lote.
        _health.AutoRespawn = false;
        _health.Died += _ => Open();

        Build();
        Open();
    }

    public override void _Process(double delta)
    {
        if (!_open) return;

        // O relógio só corre quando esta é a tela de cima. Sem isto o
        // tempo escorria por trás do menu de dificuldade e o lote era
        // escolhido sozinho antes do jogador ver a tela.
        if (!UiState.IsAlone) return;

        _remaining -= delta;
        _timer.Text = $"{Mathf.Max(_remaining, 0):0}";

        if (_remaining > 0) return;

        // Tempo esgotado: o padrão entra sozinho.
        Choose(Fallback());
    }

    // ------------------------------------------------------------------

    private Loadout Fallback() => Loadouts.Length > 0 ? Loadouts[0] : null;

    private void Open()
    {
        if (Loadouts.Length == 0) return;

        _open = true;
        _remaining = ChooseTime;

        _root.Visible = true;
        _root.MouseFilter = Control.MouseFilterEnum.Stop;
        UiState.Push();

        Refresh();
    }

    private void Close()
    {
        _open = false;
        _root.Visible = false;
        _root.MouseFilter = Control.MouseFilterEnum.Ignore;
        UiState.Pop();
    }

    private void Choose(Loadout loadout)
    {
        if (loadout == null)
        {
            Close();
            return;
        }

        // Sem WP para o lote escolhido, cai no padrão em vez de
        // bloquear: ficar sem poder renascer seria punição dupla.
        if (!_form.TrySpend(loadout.WpCost))
        {
            loadout = Fallback();
            _form.TrySpend(loadout.WpCost);
        }

        _weapon.ApplyLoadout(loadout);

        Close();

        // Morto, volta assim que o tempo mínimo tiver passado.
        if (_health.IsDead && !_health.TryRespawn())
            WaitAndRespawn();
    }

    private async void WaitAndRespawn()
    {
        while (_health.IsDead)
        {
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);

            if (!IsInstanceValid(this)) return;

            _health.TryRespawn();
        }
    }

    // ------------------------------------------------------------------

    private void Build()
    {
        // Fundo que intercepta o clique: sem ele o jogador atira no
        // mundo ao tentar escolher um cartão.
        _root = new Control
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        AddChild(_root);

        var backdrop = new ColorRect
        {
            AnchorRight = 1f, AnchorBottom = 1f,
            Color = new Color(0.03f, 0.03f, 0.05f, 0.35f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _root.AddChild(backdrop);

        var column = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            AnchorTop = 0.18f, AnchorBottom = 0.18f,
            GrowHorizontal = Control.GrowDirection.Both,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        column.AddThemeConstantOverride("separation", 14);
        _root.AddChild(column);

        var header = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        column.AddChild(header);

        var title = new Label
        {
            Text = "ESCOLHA O LOTE DESTA VIDA   ",
            Modulate = new Color(0.95f, 0.78f, 0.35f)
        };
        title.AddThemeFontSizeOverride("font_size", 20);
        header.AddChild(title);

        _timer = new Label { Modulate = new Color(1f, 0.9f, 0.5f) };
        _timer.AddThemeFontSizeOverride("font_size", 28);
        header.AddChild(_timer);

        _cards = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _cards.AddThemeConstantOverride("separation", 12);
        column.AddChild(_cards);
    }

    /// <summary>Redesenha os cartões: o custo e a disponibilidade mudam
    /// a cada morte, conforme o WP do jogador.</summary>
    private void Refresh()
    {
        foreach (Node child in _cards.GetChildren())
            child.QueueFree();

        foreach (Loadout loadout in Loadouts)
            _cards.AddChild(Card(loadout));
    }

    private Control Card(Loadout loadout)
    {
        bool grants = loadout.WpCost < 0;
        bool affordable = grants || _form.Wp >= loadout.WpCost;

        // O cartão inteiro é o botão. Um botão dentro do cartão
        // cria um alvo pequeno dentro de um alvo grande, e o jogador
        // clica no cartão esperando que funcione.
        var panel = new Button
        {
            CustomMinimumSize = new Vector2(230, 150),
            Disabled = !affordable,
            Text = ""
        };

        panel.AddThemeStyleboxOverride("normal", CardStyle(affordable, false));
        panel.AddThemeStyleboxOverride("hover", CardStyle(affordable, true));
        panel.AddThemeStyleboxOverride("pressed", CardStyle(affordable, true));
        panel.AddThemeStyleboxOverride("disabled", CardStyle(false, false));
        panel.AddThemeStyleboxOverride("focus", CardStyle(affordable, false));

        panel.Pressed += () => Choose(loadout);

        // O conteúdo não intercepta o clique: quem recebe é o cartão.
        var box = new VBoxContainer
        {
            AnchorRight = 1f, AnchorBottom = 1f,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddChild(box);

        var head = new HBoxContainer();
        box.AddChild(head);

        var name = new Label
        {
            Text = loadout.DisplayName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Modulate = affordable ? Colors.White : new Color(1, 1, 1, 0.4f)
        };
        name.AddThemeFontSizeOverride("font_size", 15);
        head.AddChild(name);

        var cost = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = grants ? $"+{-loadout.WpCost}" : $"{loadout.WpCost}",
            Modulate = grants
                ? new Color(0.45f, 0.95f, 0.45f)
                : affordable ? new Color(0.95f, 0.8f, 0.35f) : new Color(0.9f, 0.35f, 0.3f)
        };
        cost.AddThemeFontSizeOverride("font_size", 15);
        head.AddChild(cost);

        box.AddChild(new HSeparator());

        var detail = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = $"{loadout.PrimaryName}\n{loadout.SecondaryName}\nFACA\n{loadout.Grenades} GRANADAS",
            Modulate = new Color(1, 1, 1, affordable ? 0.75f : 0.35f)
        };
        detail.AddThemeFontSizeOverride("font_size", 12);
        box.AddChild(detail);

        if (!affordable)
        {
            var warn = new Label
            {
                Text = "SEM WP",
                Modulate = new Color(0.9f, 0.35f, 0.3f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            warn.AddThemeFontSizeOverride("font_size", 12);
            box.AddChild(warn);
        }

        return panel;
    }

    private StyleBoxFlat CardStyle(bool affordable, bool hover)
    {
        var style = new StyleBoxFlat
        {
            BgColor = hover
                ? new Color(0.13f, 0.13f, 0.15f, 0.96f)
                : new Color(0.06f, 0.06f, 0.07f, 0.92f),
            BorderColor = affordable
                ? new Color(0.85f, 0.65f, 0.25f, hover ? 1f : 0.75f)
                : new Color(0.3f, 0.3f, 0.32f),
            ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop = 10, ContentMarginBottom = 10
        };

        style.SetBorderWidthAll(hover ? 2 : 1);
        return style;
    }
}
