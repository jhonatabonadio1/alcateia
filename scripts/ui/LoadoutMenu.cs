using Godot;

/// <summary>
/// Tela de respawn: escolher um dos quatro conjuntos e pagar.
///
/// Só seleciona. Montar conjunto é coisa do inventário, fora da partida
/// (docs/CONJUNTOS.md): se desse para montar aqui, a decisão perderia o
/// custo e viraria otimização sem fim no meio da luta.
///
/// Cinco segundos para decidir, depois entra o último conjunto usado —
/// ou o padrão, se o último não couber no WP. Nunca trava esperando:
/// quem está morto precisa voltar à luta.
///
/// Os conjuntos que não cabem no WP aparecem acinzentados mas visíveis,
/// com quanto falta. Ver o que não se pode pagar é o que dá sentido ao
/// próximo abate. Conjunto com item vencido ou proibido pela regra da
/// sala também aparece, com o motivo.
///
/// O pagamento acontece aqui, no renascer, e não antes. O equipamento
/// comprado some na morte; o WP acumulado fica.
///
/// Não pausa a árvore: o mundo segue enquanto você escolhe.
/// </summary>
public partial class LoadoutMenu : CanvasLayer
{
    [Export] public float ChooseTime = 5f;

    /// <summary>WP concedido ao escolher o padrão. É a rede de segurança:
    /// o padrão não só é grátis, ele paga — quem está sem pontos tem
    /// caminho de volta e a partida não trava para quem está perdendo.</summary>
    [Export] public int DefaultBonus = 50;

    private const int CardWidth = 280;

    private Control _root;
    private HBoxContainer _death;
    private Label _deathText;
    private string _howDied = "";
    private Label _wpLabel;
    private Label _timer;
    private CountdownRing _ring;
    private HBoxContainer _cards;

    private Health _health;
    private Weapon _weapon;
    private FormController _form;

    private double _remaining;
    private bool _open;

    public override void _Ready()
    {
        Layer = 9;
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
        // tiver escolhido o conjunto.
        _health.AutoRespawn = false;
        _health.Died += _ => Open();
        KillFeed.Reported += OnKill;

        Build();
        Open();
    }

    public override void _ExitTree() => KillFeed.Reported -= OnKill;

    /// <summary>Guarda como o jogador morreu: a tela de escolha é a
    /// primeira coisa que ele vê depois, e tem que dizer de quê.</summary>
    private void OnKill(Kill kill)
    {
        if (_health != null && kill.Victim == _health.PublicName)
            _howDied = KillFeed.HowDied(kill);
    }

    public override void _Process(double delta)
    {
        if (!_open) return;

        // Acabou a partida: a tela de resultado assume.
        if (Match.Current?.Phase == MatchPhase.Ended) return;

        // O relógio só corre quando esta é a tela de cima.
        if (!UiState.IsAlone) return;

        _remaining -= delta;
        RefreshTimer();

        if (_remaining > 0) return;

        Choose(Fallback(), timedOut: true);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_open || !UiState.IsAlone) return;

        // 1 a 4 escolhem direto, como os números dos cartões.
        string[] keys = { "slot_1", "slot_2", "slot_3", "slot_4" };
        for (int i = 0; i < keys.Length; i++)
        {
            if (!@event.IsActionPressed(keys[i])) continue;

            Choose(i, timedOut: false);
            GetViewport().SetInputAsHandled();
            return;
        }
    }

    // ------------------------------------------------------------------

    /// <summary>Por que o conjunto não pode entrar agora, ou nulo se pode.
    /// Na ordem em que o jogador resolveria: primeiro o que não depende
    /// dele nesta partida (regra da sala, item vencido), depois o WP.</summary>
    private string Blocker(LoadoutSet set)
    {
        if (set.IsDefault) return null;
        if (!RoomRules.Allows(set, out string why)) return why;
        if (!set.Validate(out string error)) return error == "Item fora do inventário" ? "Item vencido ou fora do inventário" : error;
        if (!set.IsAffordable(_form.Wp)) return $"Faltam {set.TotalCost() - _form.Wp:0} WP";
        return null;
    }

    /// <summary>O último usado, se puder; senão o padrão.</summary>
    private int Fallback()
    {
        int last = Mathf.Clamp(Profile.LastUsed, 0, Profile.Sets.Count - 1);
        return Blocker(Profile.Sets[last]) == null ? last : 0;
    }

    private void Open()
    {
        if (_root == null) return;

        _open = true;
        _remaining = ChooseTime;

        _root.Visible = true;
        _root.MouseFilter = Control.MouseFilterEnum.Stop;
        UiState.Push();

        // O abate chega antes do Died, mas não custa garantir a ordem:
        // morto sem causa conhecida mostra só "Abatido".
        _death.Visible = _health.IsDead;
        _deathText.Text = _howDied;

        Refresh();
        Motion.FadeIn(_root, UiTokens.Fast);
    }

    private void Close()
    {
        _open = false;
        _howDied = "";
        _root.Visible = false;
        _root.MouseFilter = Control.MouseFilterEnum.Ignore;
        UiState.Pop();
    }

    private void Choose(int index, bool timedOut)
    {
        if (!_open || index < 0 || index >= Profile.Sets.Count) return;

        LoadoutSet set = Profile.Sets[index];

        // Clique num conjunto bloqueado não faz nada — ele está ali para
        // ser visto, não escolhido. No estouro do tempo isso não
        // acontece, porque o padrão sempre pode.
        if (Blocker(set) != null)
        {
            if (!timedOut)
            {
                Sfx.Ui("ui_negado");
                return;
            }

            index = 0;
            set = Profile.Sets[0];
        }

        if (set.IsDefault) _form.AddWp(DefaultBonus);
        else _form.TrySpend(set.TotalCost());

        RoomRules.Equip(_weapon, set);
        if (!timedOut) Sfx.Ui("ui_clique");

        Profile.LastUsed = index;
        Profile.Save();

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
    // Montagem

    private void Build()
    {
        _root = UiTheme.Apply(UiKit.Fill(new ColorRect { Color = new Color(UiTokens.Bg, 0.82f) }));
        AddChild(_root);

        var center = UiKit.Fill(new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore });
        _root.AddChild(center);

        VBoxContainer box = UiKit.Column(UiTokens.S4);
        center.AddChild(box);

        _death = UiKit.Row(UiTokens.S3);
        _death.AddChild(Icon.Make(IconKind.Skull, 28, UiTokens.Danger));
        Label dead = UiKit.Text("ABATIDO", "Display", UiTokens.Danger);
        dead.AddThemeFontSizeOverride("font_size", 44);
        _death.AddChild(dead);
        _deathText = UiKit.Text("", "Subtitle");
        _deathText.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _death.AddChild(_deathText);
        box.AddChild(_death);

        // Título à esquerda, WP à direita: o número que decide a tela
        // inteira fica grande.
        HBoxContainer head = UiKit.Row(UiTokens.S4);
        VBoxContainer titles = UiKit.Column(0);
        titles.AddChild(UiKit.Text("Escolha o conjunto", "Title"));
        titles.AddChild(UiKit.Text("1 a 4 ou clique. O equipamento se paga ao renascer; o WP fica.", "Muted"));
        head.AddChild(titles);
        head.AddChild(UiKit.Spacer());

        VBoxContainer wp = UiKit.Column(0);
        Label wpCaption = UiKit.Caption("WP DISPONÍVEL", UiTokens.Accent);
        wpCaption.HorizontalAlignment = HorizontalAlignment.Right;
        wp.AddChild(wpCaption);
        _wpLabel = UiKit.Text("0", "NumericLarge", UiTokens.Accent);
        _wpLabel.AddThemeFontSizeOverride("font_size", 44);
        _wpLabel.HorizontalAlignment = HorizontalAlignment.Right;
        wp.AddChild(_wpLabel);
        head.AddChild(wp);
        box.AddChild(head);

        string notice = RoomRules.Notice();
        if (notice != null)
        {
            HBoxContainer rule = UiKit.Row(UiTokens.S2);
            rule.AddChild(Icon.Make(IconKind.Warning, 16, UiTokens.Warning));
            rule.AddChild(UiKit.Text(notice, null, UiTokens.Warning));
            box.AddChild(rule);
        }

        _cards = UiKit.Row(UiTokens.S3);
        box.AddChild(_cards);

        HBoxContainer foot = UiKit.Row(UiTokens.S3);
        _ring = new CountdownRing { Total = ChooseTime, Left = ChooseTime, CustomMinimumSize = new Vector2(64, 64) };
        foot.AddChild(_ring);
        _timer = UiKit.Text("", "Subtitle");
        _timer.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        foot.AddChild(_timer);
        foot.AddChild(UiKit.Spacer());

        HBoxContainer wolf = UiKit.Row(UiTokens.S2);
        wolf.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        wolf.AddChild(Icon.Make(IconKind.Wolf, 18, UiTokens.TeamOrange));
        bool blocked = RoomConfig.Rules.Transform == TransformRule.Bloqueada;
        wolf.AddChild(UiKit.Text(blocked ? "Lobo bloqueado nesta sala" : "Lobo: grátis, a qualquer hora — tecla 3", "Muted"));
        foot.AddChild(wolf);
        box.AddChild(foot);

        _root.Visible = false;
    }

    private void Refresh()
    {
        _wpLabel.Text = $"{Mathf.Floor(_form.Wp):0}";

        UiKit.Clear(_cards);
        for (int i = 0; i < Profile.Sets.Count; i++)
            _cards.AddChild(Card(i));

        RefreshTimer();
    }

    private void RefreshTimer()
    {
        int fallback = Fallback();
        double left = Mathf.Max(_remaining, 0);
        _ring.Total = ChooseTime;
        _ring.Left = left;
        _timer.Text = $"Entra em {Mathf.CeilToInt((float)left)} s: {Profile.Sets[fallback].Name}";
    }

    private Control Card(int index)
    {
        LoadoutSet set = Profile.Sets[index];
        string blocker = Blocker(set);
        bool usable = blocker == null;
        bool last = index == Profile.LastUsed;

        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(CardWidth, 0),
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = usable ? Control.CursorShape.PointingHand : Control.CursorShape.Forbidden,
            Modulate = usable ? Colors.White : new Color(1, 1, 1, 0.55f)
        };
        StyleBoxFlat rest = UiTheme.Box(UiTokens.Surface1, last ? UiTokens.Accent : UiTokens.Border, last ? 2 : 1, UiTokens.RadiusPanel, UiTokens.S3, UiTokens.S3);
        StyleBoxFlat hover = UiTheme.Box(UiTokens.Surface2, usable ? UiTokens.Accent : UiTokens.Border, 2, UiTokens.RadiusPanel, UiTokens.S3, UiTokens.S3);
        card.AddThemeStyleboxOverride("panel", rest);

        // O cartão inteiro é o botão: clicar nele escolhe.
        card.GuiInput += e =>
        {
            if (e is InputEventMouseButton click && click.Pressed && click.ButtonIndex == MouseButton.Left)
                Choose(index, timedOut: false);
        };
        card.MouseEntered += () =>
        {
            card.AddThemeStyleboxOverride("panel", hover);
            if (usable) Sfx.Ui("ui_passar");
        };
        card.MouseExited += () => card.AddThemeStyleboxOverride("panel", rest);

        VBoxContainer stack = UiKit.Column(UiTokens.S2);
        stack.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(stack);

        HBoxContainer top = UiKit.Row(UiTokens.S2);
        var key = new PanelContainer { CustomMinimumSize = new Vector2(26, 24), MouseFilter = Control.MouseFilterEnum.Ignore };
        key.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Surface3, UiTokens.BorderStrong, 1, UiTokens.RadiusSmall, 6, 0));
        Label keyText = UiKit.Text((index + 1).ToString(), "Numeric");
        keyText.HorizontalAlignment = HorizontalAlignment.Center;
        key.AddChild(keyText);
        top.AddChild(key);
        Label name = UiKit.Truncated(set.Name, "Subtitle");
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(name);
        if (last) top.AddChild(UiKit.Chip("ÚLTIMO", UiTokens.Accent));
        stack.AddChild(top);

        // Na sala só de pistolas, o que entra é a pistola, não a primária.
        bool pistols = RoomConfig.Rules.Weapons == WeaponRule.SoPistolas;
        string art = pistols ? set.Secondary : set.Primary;
        WeaponArt primary = WeaponArt.Make(art, new Vector2(CardWidth - 2 * UiTokens.S3, 72), dim: !usable);
        primary.MouseFilter = Control.MouseFilterEnum.Ignore;
        stack.AddChild(primary);

        VBoxContainer items = UiKit.Column(2);
        items.AddChild(Item(IconKind.Gun, pistols ? $"{Arsenal.NameOf(set.Secondary)} (as duas mãos)" : Arsenal.NameOf(set.Primary)));
        if (!pistols) items.AddChild(Item(IconKind.Gun, Arsenal.NameOf(set.Secondary)));
        if (!pistols)
            foreach (string id in set.Utilities)
                items.AddChild(Item(IconKind.Grenade, Arsenal.NameOf(id)));
        stack.AddChild(items);

        stack.AddChild(UiKit.Divider());

        HBoxContainer price = UiKit.Row(UiTokens.S2);
        if (set.IsDefault)
        {
            price.AddChild(UiKit.Text("GRÁTIS", "Subtitle", UiTokens.Success));
            price.AddChild(UiKit.Spacer());
            price.AddChild(UiKit.Text($"+{DefaultBonus} WP", "Numeric", UiTokens.Success));
        }
        else
        {
            price.AddChild(UiKit.Text($"{set.TotalCost()} WP", "Subtitle", UiTokens.Accent));
            price.AddChild(UiKit.Spacer());
        }
        stack.AddChild(price);

        HBoxContainer status = UiKit.Row(UiTokens.S1);
        status.AddChild(Icon.Make(usable ? IconKind.Check : IconKind.Lock, 14, usable ? UiTokens.Success : UiTokens.Danger));
        Label statusText = UiKit.Caption(usable ? "disponível" : blocker, usable ? UiTokens.Success : UiTokens.Danger);
        statusText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        statusText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        status.AddChild(statusText);
        stack.AddChild(status);

        return card;
    }

    private static Control Item(IconKind icon, string text)
    {
        HBoxContainer row = UiKit.Row(UiTokens.S2);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(Icon.Make(icon, 14, UiTokens.TextSecondary));
        Label label = UiKit.Truncated(text, null, UiTokens.TextPrimary);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);
        return row;
    }
}
