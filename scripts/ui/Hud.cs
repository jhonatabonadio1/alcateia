using Godot;

/// <summary>
/// HUD do jogador.
///
/// Dois cantos, dois assuntos. **Esquerda é o corpo**: vida e escudo.
/// **Direita é o equipamento**: WP, munição e forma atual. Separar os
/// recursos no espaço evita que o jogador leia o errado no meio de uma
/// luta — e no meio de uma luta ninguém lê, só reconhece a posição.
///
/// Tudo em número, nada em barra. Vida e escudo caem continuamente, e
/// munição se conta: estimar por comprimento de barra não serve para
/// nenhum dos dois.
///
/// Peça descartável. Quando houver arte, isto some inteiro — o que
/// precisa sobreviver é a decisão de o que mostrar, não o desenho.
/// </summary>
public partial class Hud : CanvasLayer
{
    [Export] public Color HpColor = new Color(1f, 0.22f, 0.12f);
    [Export] public Color ShieldColor = new Color(1f, 0.42f, 0.18f);
    [Export] public Color WpColor = new Color(0.95f, 0.8f, 0.35f);

    [Export] public Color HitColor = new Color(1f, 1f, 1f, 0.95f);
    [Export] public Color HeadshotColor = new Color(1f, 0.35f, 0.3f, 1f);
    [Export] public float HitMarkerLife = 0.18f;

    [Export] public Color HurtColor = new Color(0.8f, 0.05f, 0.05f);
    [Export] public float HurtLife = 0.45f;
    [Export] public float HurtBand = 90f;

    /// <summary>Tempo que a barra de slots fica na tela depois da
    /// troca. Ela existe para o jogador conferir o que tem na mochila
    /// sem abrir menu — então aparece no gesto e sai sozinha.</summary>
    [Export] public float SlotBarLife = 1.8f;

    private const int Margin = 26;
    private const int MinimapSide = 168;

    private Health _health;
    private FormController _form;
    private Weapon _weapon;

    private Label _hpLabel;
    private Label _shieldLabel;

    private Label _wpLabel;
    private Label _ammoLabel;
    private Label _formLabel;
    private Label _grenadeLabel;

    private Label _warning;
    private Label _death;
    private VBoxContainer _feed;

    private readonly ColorRect[] _hitMarks = new ColorRect[4];
    private readonly ColorRect[] _edges = new ColorRect[4];

    private double _hitTimer;
    private double _hurtTimer;
    private double _warningTimer;

    private HBoxContainer _slotBar;
    private PanelContainer[] _slotCells;
    private double _slotBarTimer;

    public override void _Ready()
    {
        Node player = GetTree().GetFirstNodeInGroup("player") ?? FindPlayer(GetTree().CurrentScene);

        if (player == null)
        {
            GD.PrintErr("Hud: nenhum Player encontrado na cena.");
            return;
        }

        _health = player.GetNode<Health>("Health");
        _form = player.GetNode<FormController>("FormController");
        _weapon = player.GetNode<Weapon>("Weapon");

        BuildVitals();
        BuildEquipment();
        BuildSlotBar();
        BuildMinimap();
        BuildFeed();
        BuildHitMarker();
        BuildHurtEdges();
        BuildOverlays();

        _health.Changed += OnHealthChanged;
        _health.Damaged += OnDamaged;
        _health.Died += OnDied;
        _health.Respawned += OnRespawned;

        _form.WpChanged += OnWpChanged;
        _form.FormChanged += OnFormChanged;
        _form.TransformDenied += OnTransformDenied;

        _weapon.SlotChanged += OnSlotChanged;
        _weapon.GrenadesChanged += OnGrenadesChanged;
        _weapon.HitConfirmed += OnHitConfirmed;
        _weapon.AmmoChanged += OnAmmoChanged;

        OnHealthChanged(_health.Hp, _health.MaxHp, _health.Shield);
        OnWpChanged(_form.Wp, _form.MaxWp);
        OnSlotChanged((int)_weapon.CurrentSlot);
        OnGrenadesChanged(_weapon.Grenades);
        OnAmmoChanged(_weapon.PrimaryAmmo, _weapon.PrimaryMag, false);
    }

    public override void _ExitTree() => KillFeed.Reported -= OnKill;

    public override void _Process(double delta)
    {
        if (_death != null && _health != null && _health.IsDead)
            _death.Text = $"ABATIDO\n{_health.RespawnIn:0.0}";

        if (_hitTimer > 0)
        {
            _hitTimer -= delta;

            if (_hitTimer <= 0)
                foreach (ColorRect mark in _hitMarks) mark.Visible = false;
        }

        if (_hurtTimer > 0)
        {
            _hurtTimer -= delta;

            float alpha = (float)Mathf.Max(_hurtTimer, 0) / HurtLife * 0.55f;
            foreach (ColorRect edge in _edges) edge.Color = new Color(HurtColor, alpha);
        }

        if (_slotBarTimer > 0)
        {
            _slotBarTimer -= delta;

            // Some esmaecendo no último meio segundo.
            float alpha = (float)Mathf.Clamp(_slotBarTimer / 0.5, 0, 1);
            _slotBar.Modulate = new Color(1, 1, 1, alpha);
        }

        if (_warningTimer > 0)
        {
            _warningTimer -= delta;

            if (_warningTimer <= 0)
                _warning.Text = "";
        }
    }

    // ------------------------------------------------------------------
    // CONSTRUÇÃO

    /// <summary>Canto inferior esquerdo: o corpo.</summary>
    private void BuildVitals()
    {
        _hpLabel = Corner(false, -150, 54, HpColor);
        _shieldLabel = Corner(false, -98, 32, ShieldColor);
    }

    /// <summary>Canto inferior direito: o equipamento.</summary>
    private void BuildEquipment()
    {
        _wpLabel = Corner(true, -168, 30, WpColor);
        _ammoLabel = Corner(true, -118, 40, new Color(1, 1, 1, 0.92f));
        _formLabel = Corner(true, -68, 20, new Color(1, 1, 1, 0.7f));
        _grenadeLabel = Corner(true, -42, 14, new Color(1, 1, 1, 0.6f));
    }

    private Label Corner(bool right, int y, int size, Color color)
    {
        var label = new Label
        {
            AnchorTop = 1f, AnchorBottom = 1f,
            GrowVertical = Control.GrowDirection.Begin,
            Modulate = color,
            Size = new Vector2(300, size + 6)
        };

        if (right)
        {
            label.AnchorLeft = 1f;
            label.AnchorRight = 1f;
            label.GrowHorizontal = Control.GrowDirection.Begin;
            label.Position = new Vector2(-Margin - 300, y);
            label.HorizontalAlignment = HorizontalAlignment.Right;
        }
        else
        {
            label.Position = new Vector2(Margin, y);
        }

        label.AddThemeFontSizeOverride("font_size", size);
        AddChild(label);

        return label;
    }

    private void BuildOverlays()
    {
        _death = Centered(0.42f, 30, new Color(1f, 0.3f, 0.25f));
        _death.Visible = false;

        _warning = Centered(0.6f, 18, new Color(1f, 0.55f, 0.35f));
    }

    private Label Centered(float anchor, int size, Color color)
    {
        var label = new Label
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            AnchorTop = anchor, AnchorBottom = anchor,
            GrowHorizontal = Control.GrowDirection.Both,
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = color,
            Text = ""
        };

        label.AddThemeFontSizeOverride("font_size", size);
        AddChild(label);

        return label;
    }

    /// <summary>
    /// Barra de slots no rodapé. Aparece ao trocar de arma e some
    /// sozinha: serve para o jogador ver o que tem sem abrir menu, e
    /// uma barra permanente ocuparia tela o tempo todo para informar
    /// algo que ele já sabe na maior parte do jogo.
    /// </summary>
    private void BuildSlotBar()
    {
        _slotBar = new HBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            AnchorTop = 1f, AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Begin,
            Position = new Vector2(0, -Margin - 56),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0)
        };
        _slotBar.AddThemeConstantOverride("separation", 6);
        AddChild(_slotBar);

        Slot[] slots = { Slot.Primary, Slot.Secondary, Slot.Knife, Slot.Grenade };
        _slotCells = new PanelContainer[slots.Length];

        for (int i = 0; i < slots.Length; i++)
        {
            _slotCells[i] = SlotCell(slots[i]);
            _slotBar.AddChild(_slotCells[i]);
        }
    }

    private PanelContainer SlotCell(Slot slot)
    {
        var cell = new PanelContainer
        {
            CustomMinimumSize = new Vector2(104, 52),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        cell.AddChild(box);

        var number = new Label
        {
            Text = ((int)slot).ToString(),
            Modulate = new Color(1, 1, 1, 0.5f)
        };
        number.AddThemeFontSizeOverride("font_size", 11);
        box.AddChild(number);

        var name = new Label { Name = "SlotName" };
        name.AddThemeFontSizeOverride("font_size", 14);
        box.AddChild(name);

        return cell;
    }

    private void RefreshSlotBar(Slot active)
    {
        if (_slotCells == null) return;

        Slot[] slots = { Slot.Primary, Slot.Secondary, Slot.Knife, Slot.Grenade };

        for (int i = 0; i < _slotCells.Length; i++)
        {
            bool selected = slots[i] == active && !_form.IsWolf;

            var style = new StyleBoxFlat
            {
                BgColor = selected
                    ? new Color(0.16f, 0.16f, 0.18f, 0.95f)
                    : new Color(0.06f, 0.06f, 0.08f, 0.7f),
                BorderColor = selected
                    ? new Color(0.95f, 0.8f, 0.35f)
                    : new Color(1, 1, 1, 0.12f),
                ContentMarginLeft = 10, ContentMarginRight = 10,
                ContentMarginTop = 6, ContentMarginBottom = 6
            };
            style.SetBorderWidthAll(selected ? 2 : 1);
            _slotCells[i].AddThemeStyleboxOverride("panel", style);

            var box = _slotCells[i].GetChild<VBoxContainer>(0);
            var name = box.GetNode<Label>("SlotName");

            name.Text = NameOf(slots[i]);
            name.Modulate = selected ? Colors.White : new Color(1, 1, 1, 0.45f);
        }

        _slotBar.Modulate = new Color(1, 1, 1, 1);
        _slotBarTimer = SlotBarLife;
    }

    private string NameOf(Slot slot) => slot switch
    {
        Slot.Primary => _weapon.Current?.PrimaryName ?? "PRINCIPAL",
        Slot.Secondary => _weapon.Current?.SecondaryName ?? "SECUNDÁRIA",
        Slot.Knife => "FACA",
        Slot.Grenade => $"GRANADA {_weapon.Grenades}",
        _ => ""
    };

    private void BuildMinimap()
    {
        var map = new Minimap
        {
            AnchorLeft = 1f, AnchorRight = 1f,
            GrowHorizontal = Control.GrowDirection.Begin,
            Position = new Vector2(-Margin - MinimapSide, Margin),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        map.Side = MinimapSide;

        AddChild(map);
    }

    /// <summary>
    /// Feed de abates, canto superior direito. Não é enfeite: é como o
    /// jogador entende o que aconteceu fora do campo de visão e,
    /// principalmente, de que ele morreu. Morrer sem saber a causa é a
    /// forma mais rápida de um jogo parecer injusto.
    /// </summary>
    private void BuildFeed()
    {
        _feed = new VBoxContainer
        {
            AnchorLeft = 1f, AnchorRight = 1f,
            GrowHorizontal = Control.GrowDirection.Begin,
            Position = new Vector2(-Margin - 420, Margin + MinimapSide + 14),
            CustomMinimumSize = new Vector2(420, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _feed.AddThemeConstantOverride("separation", 6);
        AddChild(_feed);

        KillFeed.Reported += OnKill;
    }

    /// <summary>Quatro riscos em volta do centro, no acerto.</summary>
    private void BuildHitMarker()
    {
        var offsets = new[]
        {
            new Vector2(-14, -14), new Vector2(8, -14),
            new Vector2(-14, 8), new Vector2(8, 8)
        };

        for (int i = 0; i < 4; i++)
        {
            var mark = new ColorRect
            {
                AnchorLeft = 0.5f, AnchorRight = 0.5f,
                AnchorTop = 0.5f, AnchorBottom = 0.5f,
                GrowHorizontal = Control.GrowDirection.Both,
                GrowVertical = Control.GrowDirection.Both,
                Position = offsets[i],
                Size = new Vector2(6, 6),
                Color = HitColor,
                Visible = false
            };

            AddChild(mark);
            _hitMarks[i] = mark;
        }
    }

    private void BuildHurtEdges()
    {
        for (int i = 0; i < 4; i++)
        {
            var edge = new ColorRect
            {
                Color = new Color(HurtColor, 0f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorLeft = 0f, AnchorRight = 1f,
                AnchorTop = 0f, AnchorBottom = 1f
            };

            if (i == 0) { edge.AnchorBottom = 0f; edge.OffsetBottom = HurtBand; }
            if (i == 1) { edge.AnchorTop = 1f; edge.OffsetTop = -HurtBand; }
            if (i == 2) { edge.AnchorRight = 0f; edge.OffsetRight = HurtBand; }
            if (i == 3) { edge.AnchorLeft = 1f; edge.OffsetLeft = -HurtBand; }

            AddChild(edge);
            _edges[i] = edge;
        }
    }

    private static Node FindPlayer(Node from)
    {
        if (from == null) return null;
        if (from is Player) return from;

        foreach (Node child in from.GetChildren())
        {
            Node found = FindPlayer(child);
            if (found != null) return found;
        }

        return null;
    }

    // ------------------------------------------------------------------
    // ATUALIZAÇÃO

    private void OnHealthChanged(float hp, float maxHp, float shield)
    {
        if (_hpLabel == null) return;

        _hpLabel.Text = $"{Mathf.Ceil(hp):0}";

        // Sem escudo o número some: humano não tem escudo, e um zero
        // permanente na tela vira ruído.
        _shieldLabel.Text = shield > 0 ? $"{Mathf.Ceil(shield):0}" : "";
    }

    private void OnWpChanged(float wp, float max)
    {
        if (_wpLabel == null) return;

        _wpLabel.Text = $"WP  {Mathf.Floor(wp):0}";
    }

    private void OnAmmoChanged(int inMag, int magSize, bool reloading)
    {
        if (_ammoLabel == null) return;

        if (_form.IsWolf)
        {
            _ammoLabel.Text = "";
            return;
        }

        _ammoLabel.Text = reloading ? "RECARREGANDO" : $"{inMag} / {magSize}";
        _ammoLabel.AddThemeFontSizeOverride("font_size", reloading ? 20 : 40);

        _ammoLabel.Modulate = inMag == 0 && !reloading
            ? new Color(1f, 0.3f, 0.25f)
            : new Color(1, 1, 1, 0.92f);
    }

    private void OnFormChanged(int form)
    {
        OnWpChanged(_form.Wp, _form.MaxWp);
        OnSlotChanged((int)_weapon.CurrentSlot);
        OnAmmoChanged(_weapon.PrimaryAmmo, _weapon.PrimaryMag, false);
    }

    private void OnSlotChanged(int slot)
    {
        if (_formLabel == null) return;

        _formLabel.Text = _form.IsWolf
            ? (_form.Breed?.DisplayName ?? "LOBO").ToUpper()
            : (Slot)slot switch
            {
                Slot.Primary => "PRINCIPAL",
                Slot.Secondary => "SECUNDÁRIA",
                Slot.Knife => "FACA",
                Slot.Grenade => "GRANADA",
                _ => "HUMANO"
            };

        _formLabel.Modulate = _form.IsWolf
            ? new Color(1f, 0.55f, 0.2f)
            : new Color(1, 1, 1, 0.7f);

        RefreshSlotBar((Slot)slot);
    }

    private void OnGrenadesChanged(int left)
    {
        if (_grenadeLabel == null) return;

        _grenadeLabel.Text = $"GRANADAS {left}";
    }

    private void OnHitConfirmed(bool headshot)
    {
        if (_hitMarks[0] == null) return;

        foreach (ColorRect mark in _hitMarks)
        {
            mark.Color = headshot ? HeadshotColor : HitColor;
            mark.Size = headshot ? new Vector2(8, 8) : new Vector2(6, 6);
            mark.Visible = true;
        }

        _hitTimer = HitMarkerLife;
    }

    /// <summary>
    /// O alerta vermelho vem do sinal de dano, não de ver a vida
    /// baixar: trocar de forma troca o pool de vida, e o número cai
    /// sem ninguém ter atirado.
    /// </summary>
    private void OnDamaged(float amount, Vector3 from)
    {
        if (_health.IsDead) return;

        _hurtTimer = HurtLife;
    }

    private void OnTransformDenied(float missing)
    {
        if (_warning == null) return;

        _warning.Text = $"faltam {missing:0} WP";
        _warningTimer = 1.5;
    }

    private void OnDied(int killerId)
    {
        if (_death == null) return;

        _death.Visible = true;
        _warning.Text = "";
        _warningTimer = 0;
    }

    private void OnRespawned()
    {
        if (_death == null) return;

        _death.Visible = false;
        _death.Text = "";
    }

    private void OnKill(Kill kill)
    {
        if (_feed == null) return;

        var line = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.Off,
            CustomMinimumSize = new Vector2(420, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        line.AddThemeFontSizeOverride("normal_font_size", 18);

        string killer = Teams.ColorOf(kill.KillerTeam).ToHtml(false);
        string victim = Teams.ColorOf(kill.VictimTeam).ToHtml(false);

        line.Text = "[right]"
            + $"[color=#{killer}]{kill.Killer}[/color]"
            + $"  [color=#9aa0a6]{KillFeed.Describe(kill.Cause)}[/color]  "
            + $"[color=#{victim}]{kill.Victim}[/color]"
            + "[/right]";

        _feed.AddChild(line);

        // Só as cinco últimas: um feed que cresce vira parede de texto.
        while (_feed.GetChildCount() > 5)
            _feed.GetChild(0).QueueFree();

        SceneTreeTimer timer = GetTree().CreateTimer(6.0);
        timer.Timeout += () => { if (IsInstanceValid(line)) line.QueueFree(); };
    }
}
