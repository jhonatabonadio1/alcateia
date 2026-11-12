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
    [Export] public Color HpColor = new Color(0.85f, 0.25f, 0.25f);
    [Export] public Color ShieldColor = new Color(0.35f, 0.7f, 0.95f);
    [Export] public Color WpColor = new Color(0.95f, 0.75f, 0.2f);
    [Export] public Color WpReadyColor = new Color(0.45f, 0.95f, 0.4f);

    private Health _health;
    private FormController _form;
    private Weapon _weapon;

    private ColorRect _hpFill;
    private ColorRect _shieldFill;
    private ColorRect _wpFill;
    private Label _wpLabel;
    private Label _slotLabel;
    private Label _grenadeLabel;
    private Label _warning;
    private Label _death;

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

        if (_warningTimer <= 0) return;

        _warningTimer -= delta;
        if (_warningTimer <= 0 && _warning != null)
            _warning.Text = "";
    }

    // ------------------------------------------------------------------

    private void Build()
    {
        // Vida (canto inferior esquerdo)
        MakeBar(0, "VIDA", HpColor, out _hpFill);
        MakeBar(1, "ESCUDO", ShieldColor, out _shieldFill);
        MakeBar(2, "WP", WpColor, out _wpFill);

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
        if (_hpFill == null) return;

        float hpRatio = maxHp > 0 ? hp / maxHp : 0;
        _hpFill.Size = new Vector2(BarWidth * Mathf.Clamp(hpRatio, 0, 1), BarHeight);

        float shieldRatio = _health.MaxShield > 0 ? shield / _health.MaxShield : 0;
        _shieldFill.Size = new Vector2(BarWidth * Mathf.Clamp(shieldRatio, 0, 1), BarHeight);
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
