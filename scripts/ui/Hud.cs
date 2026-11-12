using Godot;

/// <summary>
/// HUD mínimo: vida, escudo, WP e slot atual.
///
/// A barra de WP é o item mais importante aqui. O documento é explícito:
/// a decisão de transformar só existe se o jogador enxerga o recurso o
/// tempo todo.
///
/// Construído por código de propósito — é uma peça descartável que será
/// refeita com arte na Fase 3, e montá-la por GUI agora seria trabalho
/// jogado fora.
/// </summary>
public partial class Hud : CanvasLayer
{
    [Export] public Color HpColor = new Color(1f, 0.22f, 0.12f);
    [Export] public Color ShieldColor = new Color(1f, 0.42f, 0.18f);
    [Export] public Color WpColor = new Color(0.95f, 0.75f, 0.2f);
    [Export] public Color WpReadyColor = new Color(0.45f, 0.95f, 0.4f);

    [Export] public Color HitColor = new Color(1f, 1f, 1f, 0.95f);
    [Export] public Color HeadshotColor = new Color(1f, 0.35f, 0.3f, 1f);
    [Export] public float HitMarkerLife = 0.18f;

    [Export] public Color HurtColor = new Color(0.8f, 0.05f, 0.05f);
    [Export] public float HurtLife = 0.45f;
    [Export] public float HurtBand = 90f;

    private Health _health;
    private FormController _form;
    private Weapon _weapon;

    private Label _hpLabel;
    private Label _shieldLabel;
    private ColorRect _wpFill;
    private Label _wpLabel;
    private Label _slotLabel;
    private Label _grenadeLabel;
    private Label _warning;
    private Label _death;
    private readonly ColorRect[] _hitMarks = new ColorRect[4];
    private readonly ColorRect[] _edges = new ColorRect[4];
    private double _hitTimer;
    private double _hurtTimer;
    private float _lastHp = -1f;

    private double _warningTimer;

    private const int BarWidth = 260;
    private const int BarHeight = 16;
    private const int Margin = 24;

    public override void _Ready()
    {
        // O grupo é o caminho normal. A varredura é plano B: cena
        // montada à mão esquece o grupo com facilidade, e o HUD sumir
        // inteiro por causa disso custa caro para diagnosticar.
        Node player = GetTree().GetFirstNodeInGroup("player")
            ?? FindPlayer(GetTree().CurrentScene);

        if (player == null)
        {
            GD.PrintErr("Hud: nenhum Player encontrado na cena.");
            return;
        }

        _health = player.GetNode<Health>("Health");
        _form = player.GetNode<FormController>("FormController");
        _weapon = player.GetNode<Weapon>("Weapon");

        Build();

        _health.Changed += OnHealthChanged;
        _health.Died += OnDied;
        _health.Respawned += OnRespawned;
        _form.WpChanged += OnWpChanged;
        _form.FormChanged += OnFormChanged;
        _form.TransformDenied += OnTransformDenied;
        _weapon.SlotChanged += OnSlotChanged;
        _weapon.GrenadesChanged += OnGrenadesChanged;
        _weapon.HitConfirmed += OnHitConfirmed;

        OnHealthChanged(_health.Hp, _health.MaxHp, _health.Shield);
        OnWpChanged(_form.Wp, _form.MaxWp);
        OnSlotChanged((int)_weapon.Current);
        OnGrenadesChanged(_weapon.Grenades);
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

    public override void _Process(double delta)
    {
        if (_death != null && _health != null && _health.IsDead)
            _death.Text = $"ABATIDO\nvolta em {_health.RespawnIn:0.0}s";

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

        if (_warningTimer <= 0) return;

        _warningTimer -= delta;
        if (_warningTimer <= 0 && _warning != null)
            _warning.Text = "";
    }

    // ------------------------------------------------------------------

    private void Build()
    {
        // Vida (canto inferior esquerdo)
        MakeBar(2, "WP", WpColor, out _wpFill);
        BuildVitals();

        _wpLabel = new Label
        {
            Position = new Vector2(Margin + BarWidth + 12, BarY(2) - 4),
            Modulate = new Color(1, 1, 1, 0.85f)
        };
        AddChild(_wpLabel);

        _slotLabel = new Label
        {
            Position = new Vector2(Margin, BarY(2) - 36),
            Modulate = new Color(1, 1, 1, 0.9f)
        };
        AddChild(_slotLabel);

        _grenadeLabel = new Label
        {
            Position = new Vector2(Margin, BarY(2) - 58),
            Modulate = new Color(1, 1, 1, 0.75f)
        };
        AddChild(_grenadeLabel);

        BuildHitMarker();
        BuildHurtEdges();

        _death = new Label
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.42f,
            AnchorBottom = 0.42f,
            GrowHorizontal = Control.GrowDirection.Both,
            Modulate = new Color(1f, 0.3f, 0.25f),
            Text = "",
            Visible = false
        };
        _death.AddThemeFontSizeOverride("font_size", 30);
        AddChild(_death);

        _warning = new Label
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.62f,
            AnchorBottom = 0.62f,
            GrowHorizontal = Control.GrowDirection.Both,
            Modulate = new Color(1f, 0.55f, 0.35f),
            Text = ""
        };
        _warning.AddThemeFontSizeOverride("font_size", 18);
        AddChild(_warning);
    }

    /// <summary>Quatro riscos em volta do centro. Aparecem no acerto
    /// e somem — sem isso não se sabe se o tiro pegou.</summary>
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

    /// <summary>Faixas vermelhas nas bordas ao levar dano.</summary>
    private void BuildHurtEdges()
    {
        for (int i = 0; i < 4; i++)
        {
            var edge = new ColorRect
            {
                Color = new Color(HurtColor, 0f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };

            // 0 topo, 1 base, 2 esquerda, 3 direita.
            edge.AnchorLeft = 0f;
            edge.AnchorRight = 1f;
            edge.AnchorTop = 0f;
            edge.AnchorBottom = 1f;

            if (i == 0) { edge.AnchorBottom = 0f; edge.OffsetBottom = HurtBand; }
            if (i == 1) { edge.AnchorTop = 1f; edge.OffsetTop = -HurtBand; }
            if (i == 2) { edge.AnchorRight = 0f; edge.OffsetRight = HurtBand; }
            if (i == 3) { edge.AnchorLeft = 1f; edge.OffsetLeft = -HurtBand; }

            AddChild(edge);
            _edges[i] = edge;
        }
    }

    /// <summary>
    /// Vida em número grande, escudo em número menor logo abaixo.
    ///
    /// Barra não serve aqui: o escudo do lobo escorre o tempo todo e o
    /// jogador precisa ler quanto resta, não estimar por comprimento.
    /// </summary>
    private void BuildVitals()
    {
        _hpLabel = new Label
        {
            AnchorTop = 1f, AnchorBottom = 1f,
            GrowVertical = Control.GrowDirection.Begin,
            Position = new Vector2(Margin, -148),
            Modulate = HpColor
        };
        _hpLabel.AddThemeFontSizeOverride("font_size", 54);
        AddChild(_hpLabel);

        _shieldLabel = new Label
        {
            AnchorTop = 1f, AnchorBottom = 1f,
            GrowVertical = Control.GrowDirection.Begin,
            Position = new Vector2(Margin + 10, -100),
            Modulate = ShieldColor
        };
        _shieldLabel.AddThemeFontSizeOverride("font_size", 32);
        AddChild(_shieldLabel);
    }

    private int BarY(int index) => -(Margin + (BarHeight + 10) * (3 - index));

    private void MakeBar(int index, string caption, Color color, out ColorRect fill)
    {
        int y = BarY(index);

        var label = new Label
        {
            Position = new Vector2(Margin, y - 20),
            AnchorTop = 1f,
            AnchorBottom = 1f,
            GrowVertical = Control.GrowDirection.Begin,
            Modulate = new Color(1, 1, 1, 0.55f),
            Text = caption
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        AddChild(label);

        var back = new ColorRect
        {
            Position = new Vector2(Margin, y),
            Size = new Vector2(BarWidth, BarHeight),
            AnchorTop = 1f,
            AnchorBottom = 1f,
            GrowVertical = Control.GrowDirection.Begin,
            Color = new Color(0, 0, 0, 0.45f)
        };
        AddChild(back);

        fill = new ColorRect
        {
            Position = new Vector2(Margin, y),
            Size = new Vector2(BarWidth, BarHeight),
            AnchorTop = 1f,
            AnchorBottom = 1f,
            GrowVertical = Control.GrowDirection.Begin,
            Color = color
        };
        AddChild(fill);
    }

    // ------------------------------------------------------------------

    private void OnHealthChanged(float hp, float maxHp, float shield)
    {
        if (_hpLabel == null) return;

        if (_lastHp >= 0 && hp < _lastHp && !_health.IsDead)
            _hurtTimer = HurtLife;

        _lastHp = hp;

        _hpLabel.Text = $"{Mathf.Ceil(hp):0}";

        // Sem escudo o número some, em vez de mostrar zero: humano não
        // tem escudo, e um zero permanente na tela vira ruído.
        _shieldLabel.Text = shield > 0 ? $"{Mathf.Ceil(shield):0}" : "";
    }

    private void OnWpChanged(float wp, float max)
    {
        if (_wpFill == null) return;

        _wpFill.Size = new Vector2(BarWidth * Mathf.Clamp(wp / max, 0, 1), BarHeight);

        float cost = _form.Breed?.WpCost ?? 0;
        bool ready = !_form.IsWolf && wp >= cost;

        _wpFill.Color = ready ? WpReadyColor : WpColor;
        _wpLabel.Text = _form.IsWolf
            ? $"{wp:0}"
            : $"{wp:0} / {cost:0}";
    }

    private void OnFormChanged(int form)
    {
        OnWpChanged(_form.Wp, _form.MaxWp);
        OnSlotChanged((int)_weapon.Current);
    }

    private void OnSlotChanged(int slot)
    {
        if (_slotLabel == null) return;

        _slotLabel.Text = (Slot)slot switch
        {
            Slot.Primary => "1 · PRINCIPAL",
            Slot.Secondary => "2 · SECUNDÁRIA",
            Slot.Wolf => $"3 · {_form.Breed?.DisplayName ?? "LOBO"}".ToUpper(),
            Slot.Grenade => "4 · GRANADA",
            _ => ""
        };
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

    private void OnGrenadesChanged(int left)
    {
        if (_grenadeLabel == null) return;

        _grenadeLabel.Text = $"4 · GRANADAS  {left}";

        if (left == 0)
        {
            _warning.Text = "sem granadas";
            _warningTimer = 1.2;
        }
    }

    private void OnTransformDenied(float missing)
    {
        if (_warning == null) return;

        _warning.Text = $"faltam {missing:0} WP";
        _warningTimer = 1.5;
    }
}
