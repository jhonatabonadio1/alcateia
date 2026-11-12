using Godot;
using System.Collections.Generic;

/// <summary>
/// O HUD de combate, pela regra dos cantos (docs/UI.md, "HUD de combate"):
///
/// | Canto | O quê |
/// |---|---|
/// | Superior esquerdo | Minimapa e nome do mapa |
/// | Superior centro | Placar e relógio (MatchHud) |
/// | Superior direito | Feed de abates |
/// | Centro | Mira, marcador de acerto, dano direcional, avisos |
/// | Inferior esquerdo | Vida, escudo e a forma atual |
/// | Inferior centro | Barra de transformação |
/// | Inferior direito | Arma, munição, granadas, slots e WP — ou, no lobo, as recargas dos ataques |
///
/// O centro fica livre: só mira e avisos críticos.
///
/// **O HUD troca de forma.** Humano mostra munição, pente e granadas;
/// lobo mostra a recarga da garrada, do salto, do bote e o uivo, e
/// nenhuma munição. São dois painéis distintos com transição, não dez
/// nós ligando e desligando.
///
/// Tudo chega por sinal (vida, munição, WP, forma, abates). Só o que é
/// contínuo por natureza — andamento da transformação, recarga de ataque
/// do lobo, uivo, relógio de renascer — é lido a cada quadro.
///
/// Nada aqui escala com o FOV nem balança com a câmera: CanvasLayer.
/// </summary>
public partial class Hud : CanvasLayer
{
    /// <summary>Abaixo desta fração de vida, a barra pulsa e a vinheta
    /// vermelha entra.</summary>
    [Export] public float LowHealth = 0.3f;

    /// <summary>Quanto tempo uma linha fica no feed.</summary>
    [Export] public float FeedLife = 5f;
    [Export] public int FeedLines = 5;

    private const int Edge = UiTokens.S5;
    private const int MinimapSide = 200;

    private Health _health;
    private FormController _form;
    private Weapon _weapon;
    private Player _player;
    private Control _root;

    // vida
    private Icon _formIcon;
    private Label _formLabel;
    private Label _hpNumber;
    private Label _hpMax;
    private ProgressBar _hpBar;
    private ProgressBar _shieldBar;
    private Label _shieldNumber;
    private HBoxContainer _shieldRow;
    private Tween _lowPulse;
    private bool? _lowShown;

    private static readonly StyleBoxFlat HpFill = UiTheme.Box(UiTokens.TextPrimary, default, 0, 0, 0, 0);
    private static readonly StyleBoxFlat HpFillLow = UiTheme.Box(UiTokens.Danger, default, 0, 0, 0, 0);
    private static readonly StyleBoxFlat SlotOn = UiTheme.Box(new Color(UiTokens.Accent, 0.2f), UiTokens.Accent, 1, 0, 0, 0);
    private static readonly StyleBoxFlat SlotOff = UiTheme.Box(new Color(1, 1, 1, 0.04f), new Color(1, 1, 1, 0.1f), 1, 0, 0, 0);
    private TextureRect _vignette;
    private float _hurtFlash;

    // equipamento
    private Label _wp;
    private VBoxContainer _human;
    private VBoxContainer _wolf;
    private Label _weaponName;
    private WeaponArt _weaponArt;
    private Label _mag;
    private Label _reserve;
    private Label _reloading;
    private Label _grenades;
    private readonly Dictionary<Slot, PanelContainer> _slots = new();
    private Label _breedName;
    private readonly ProgressBar[] _cooldownBars = new ProgressBar[3];
    private readonly float[] _cooldownLeft = new float[3];
    private readonly float[] _cooldownTotal = { 1, 1, 1 };
    private ProgressBar _howlBar;
    private Label _howlLabel;

    // transformação
    private Label _shiftKey;
    private Label _shiftText;
    private Label _shiftState;
    private ProgressBar _shiftBar;
    private Tween _shiftPulse;
    private PanelContainer _shiftPanel;

    // centro
    private Crosshair _crosshair;
    private HitMarker _hitMarker;
    private Label _warning;
    private double _warningTimer;
    private VBoxContainer _death;
    private Label _deathBy;
    private Label _deathTimer;

    // feed
    private VBoxContainer _feed;

    public override void _Ready()
    {
        Node player = GetTree().GetFirstNodeInGroup("player") ?? FindPlayer(GetTree().CurrentScene);
        if (player == null)
        {
            GD.PrintErr("Hud: nenhum Player encontrado na cena.");
            return;
        }

        _player = player as Player;
        _health = player.GetNode<Health>("Health");
        _form = player.GetNode<FormController>("FormController");
        _weapon = player.GetNode<Weapon>("Weapon");

        _root = UiTheme.Apply(UiKit.Fill(new Control { MouseFilter = Control.MouseFilterEnum.Ignore }));
        AddChild(_root);
        // O DamageIndicator da cena fica por cima do resto.
        MoveChild(_root, 0);

        BuildVignette();
        BuildCenter();
        BuildMinimap();
        BuildFeed();
        BuildVitals();
        BuildEquipment();
        BuildTransform();

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
        _weapon.AttackCooldown += OnAttackCooldown;
        _weapon.PickedUp += id => Warn($"Pegou: {Arsenal.NameOf(id)}", 1.6f, UiTokens.Success);
        _weapon.Dropped += id =>
        {
            if (!_health.IsDead) Warn($"Soltou: {Arsenal.NameOf(id)}", 1.2f, UiTokens.TextSecondary);
        };
        KillFeed.Reported += OnKill;

        // No fim da partida a tela de resultado assume a tela inteira.
        if (Match.Current != null)
            Match.Current.PhaseChanged += phase => Visible = (MatchPhase)phase != MatchPhase.Ended;

        OnHealthChanged(_health.Hp, _health.MaxHp, _health.Shield);
        OnWpChanged(_form.Wp, _form.MaxWp);
        OnSlotChanged((int)_weapon.CurrentSlot);
        OnGrenadesChanged(_weapon.Grenades);
        OnAmmoChanged(_weapon.PrimaryAmmo, _weapon.PrimaryMag, false);
        OnFormChanged((int)_form.Current, animate: false);
    }

    public override void _ExitTree() => KillFeed.Reported -= OnKill;

    public override void _Process(double delta)
    {
        if (_health == null) return;

        // Morto com a escolha de conjunto aberta: ela já diz "Abatido" e
        // de quê. Aqui só aparece depois de escolher, enquanto o tempo
        // mínimo para renascer não passou.
        _death.Visible = _health.IsDead && !UiState.AnyOpen;
        if (_death.Visible)
            _deathTimer.Text = _health.RespawnIn > 0.05f ? $"Renasce em {_health.RespawnIn:0.0} s" : "Escolha o conjunto";

        if (_warningTimer > 0)
        {
            _warningTimer -= delta;
            if (_warningTimer <= 0) _warning.Text = "";
        }

        if (_hurtFlash > 0)
        {
            _hurtFlash = Mathf.Max(0, _hurtFlash - (float)delta * 2.2f);
            UpdateVignette();
        }

        UpdateTransform();
        UpdateWolf((float)delta);
    }

    // ================================================================== montagem

    private T Corner<T>(T control, Control.LayoutPreset preset) where T : Control
    {
        control.SetAnchorsPreset(preset);
        control.GrowHorizontal = preset is Control.LayoutPreset.TopRight or Control.LayoutPreset.BottomRight
            ? Control.GrowDirection.Begin
            : preset is Control.LayoutPreset.CenterBottom or Control.LayoutPreset.CenterTop ? Control.GrowDirection.Both
            : Control.GrowDirection.End;
        control.GrowVertical = preset is Control.LayoutPreset.BottomLeft or Control.LayoutPreset.BottomRight or Control.LayoutPreset.CenterBottom
            ? Control.GrowDirection.Begin
            : Control.GrowDirection.End;
        control.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(control);
        return control;
    }

    private void BuildVignette()
    {
        // Vinheta radial: transparente no meio, vermelha nas bordas.
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.7f, 0.02f, 0.02f, 0f));
        gradient.SetColor(1, new Color(0.7f, 0.02f, 0.02f, 0.85f));
        gradient.SetOffset(0, 0.55f);
        gradient.SetOffset(1, 1f);

        var texture = new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1.05f, 1.05f),
            Width = 256,
            Height = 256
        };

        _vignette = UiKit.Fill(new TextureRect
        {
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0)
        });
        _root.AddChild(_vignette);
    }

    private void BuildCenter()
    {
        _crosshair = UiKit.Fill(new Crosshair { Weapon = _weapon, Form = _form });
        _root.AddChild(_crosshair);

        _hitMarker = UiKit.Fill(new HitMarker());
        _root.AddChild(_hitMarker);

        _warning = UiKit.Text("", "Subtitle", UiTokens.Warning);
        _warning.HorizontalAlignment = HorizontalAlignment.Center;
        _warning.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _warning.AnchorTop = _warning.AnchorBottom = 0.62f;
        _warning.GrowHorizontal = Control.GrowDirection.Both;
        _warning.AddThemeConstantOverride("outline_size", 6);
        _warning.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        _root.AddChild(_warning);

        _death = UiKit.Column(UiTokens.S1);
        _death.Alignment = BoxContainer.AlignmentMode.Center;
        _death.SetAnchorsPreset(Control.LayoutPreset.Center);
        _death.AnchorTop = _death.AnchorBottom = 0.36f;
        _death.GrowHorizontal = Control.GrowDirection.Both;
        _death.GrowVertical = Control.GrowDirection.Both;
        _death.MouseFilter = Control.MouseFilterEnum.Ignore;
        _death.Visible = false;

        Label title = UiKit.Text("ABATIDO", "Display", UiTokens.Danger);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _death.AddChild(title);
        _deathBy = UiKit.Text("", "Subtitle");
        _deathBy.HorizontalAlignment = HorizontalAlignment.Center;
        _death.AddChild(_deathBy);
        _deathTimer = UiKit.Text("", "Muted");
        _deathTimer.HorizontalAlignment = HorizontalAlignment.Center;
        _death.AddChild(_deathTimer);
        _root.AddChild(_death);
    }

    private void BuildMinimap()
    {
        VBoxContainer box = Corner(UiKit.Column(UiTokens.S1), Control.LayoutPreset.TopLeft);
        box.Position = new Vector2(Edge, Edge);

        var frame = new PanelContainer { ThemeTypeVariation = "HudPanel", MouseFilter = Control.MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(UiTokens.Bg, 0.55f), new Color(1, 1, 1, 0.08f), 1, 0, 0, 0));
        var map = new Minimap { MouseFilter = Control.MouseFilterEnum.Ignore };
        map.Side = MinimapSide;
        frame.AddChild(map);
        box.AddChild(frame);

        MapInfo info = Maps.Get(RoomConfig.Rules.Map);
        string mode = RoomSettings.ModeName(RoomConfig.Rules.Mode);
        box.AddChild(Outlined(UiKit.Caption($"{info.Name}  ·  {mode}", UiTokens.TextPrimary)));
    }

    private void BuildFeed()
    {
        _feed = Corner(UiKit.Column(UiTokens.S1), Control.LayoutPreset.TopRight);
        _feed.Position = new Vector2(-Edge, Edge);
        _feed.CustomMinimumSize = new Vector2(440, 0);
        _feed.OffsetLeft = -Edge - 440;
        _feed.OffsetRight = -Edge;
    }

    private void BuildVitals()
    {
        var panel = new PanelContainer { ThemeTypeVariation = "HudPanel", CustomMinimumSize = new Vector2(360, 0) };
        Corner(panel, Control.LayoutPreset.BottomLeft);
        panel.OffsetLeft = Edge;
        panel.OffsetBottom = -Edge;

        VBoxContainer column = UiKit.Column(UiTokens.S1 + 2);
        panel.AddChild(column);

        // Forma sempre visível, ao lado da vida. Parece redundante; em
        // partida rápida não é.
        HBoxContainer form = UiKit.Row(UiTokens.S2);
        _formIcon = Icon.Make(IconKind.Human, 22, UiTokens.TextPrimary);
        form.AddChild(_formIcon);
        _formLabel = UiKit.Caption("Humano", UiTokens.TextPrimary);
        form.AddChild(_formLabel);
        column.AddChild(form);

        _shieldRow = UiKit.Row(UiTokens.S2);
        _shieldBar = Bar(UiTokens.TeamOrange.Lightened(0.15f), 6);
        _shieldRow.AddChild(_shieldBar);
        _shieldNumber = UiKit.Text("", "Numeric", UiTokens.TeamOrange.Lightened(0.25f));
        _shieldNumber.CustomMinimumSize = new Vector2(44, 0);
        _shieldNumber.HorizontalAlignment = HorizontalAlignment.Right;
        _shieldRow.AddChild(_shieldNumber);
        column.AddChild(_shieldRow);

        HBoxContainer hp = UiKit.Row(UiTokens.S2);
        _hpNumber = UiKit.Text("200", "NumericLarge");
        _hpNumber.AddThemeFontSizeOverride("font_size", UiTokens.Display);
        hp.AddChild(_hpNumber);
        _hpMax = UiKit.Text("/ 200", "Numeric", UiTokens.TextSecondary);
        _hpMax.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        hp.AddChild(_hpMax);
        column.AddChild(hp);

        _hpBar = Bar(UiTokens.TextPrimary, 10);
        column.AddChild(_hpBar);
    }

    private void BuildEquipment()
    {
        VBoxContainer side = Corner(UiKit.Column(UiTokens.S2), Control.LayoutPreset.BottomRight);
        side.OffsetRight = -Edge;
        side.OffsetBottom = -Edge;
        side.CustomMinimumSize = new Vector2(380, 0);
        side.OffsetLeft = -Edge - 380;

        // WP, perto da munição: é o que se gasta ao renascer.
        HBoxContainer wpRow = UiKit.Row(UiTokens.S2);
        wpRow.Alignment = BoxContainer.AlignmentMode.End;
        wpRow.AddChild(Outlined(UiKit.Caption("WP", UiTokens.Accent)));
        _wp = Outlined(UiKit.Text("0", "NumericLarge", UiTokens.Accent));
        wpRow.AddChild(_wp);
        side.AddChild(wpRow);

        var panel = new PanelContainer { ThemeTypeVariation = "HudPanel" };
        side.AddChild(panel);
        var stack = new Control { CustomMinimumSize = new Vector2(0, 150), MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(stack);

        _human = UiKit.Fill(HumanPanel());
        stack.AddChild(_human);
        _wolf = UiKit.Fill(WolfPanel());
        stack.AddChild(_wolf);
    }

    private VBoxContainer HumanPanel()
    {
        VBoxContainer column = UiKit.Column(UiTokens.S1);
        column.MouseFilter = Control.MouseFilterEnum.Ignore;

        HBoxContainer top = UiKit.Row(UiTokens.S2);
        _weaponArt = WeaponArt.Make("m16a2", new Vector2(120, 44));
        _weaponArt.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        top.AddChild(_weaponArt);

        VBoxContainer names = UiKit.Column(0);
        names.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _weaponName = UiKit.Text("", "Subtitle");
        _weaponName.HorizontalAlignment = HorizontalAlignment.Right;
        names.AddChild(_weaponName);
        _reloading = UiKit.Caption("", UiTokens.Warning);
        _reloading.HorizontalAlignment = HorizontalAlignment.Right;
        names.AddChild(_reloading);
        top.AddChild(names);
        column.AddChild(top);

        HBoxContainer ammo = UiKit.Row(UiTokens.S2);
        ammo.Alignment = BoxContainer.AlignmentMode.End;

        _grenades = UiKit.Text("", "Numeric", UiTokens.TextSecondary);
        HBoxContainer grenade = UiKit.Row(UiTokens.S1);
        grenade.AddChild(Icon.Make(IconKind.Grenade, 16, UiTokens.TextSecondary));
        grenade.AddChild(_grenades);
        grenade.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        ammo.AddChild(grenade);
        ammo.AddChild(UiKit.Spacer());

        _mag = UiKit.Text("30", "NumericLarge");
        _mag.AddThemeFontSizeOverride("font_size", 52);
        ammo.AddChild(_mag);
        // Reserva infinita: o custo de atirar é o tempo de recarga.
        _reserve = UiKit.Text("/ ∞", "Subtitle", UiTokens.TextSecondary);
        _reserve.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        ammo.AddChild(_reserve);
        column.AddChild(ammo);

        HBoxContainer slots = UiKit.Row(UiTokens.S1);
        slots.Alignment = BoxContainer.AlignmentMode.End;
        foreach (var (slot, label) in new[] { (Slot.Primary, "1"), (Slot.Secondary, "2"), (Slot.Wolf, "3"), (Slot.Knife, "4"), (Slot.Grenade, "5") })
        {
            var chip = new PanelContainer { CustomMinimumSize = new Vector2(28, 22), MouseFilter = Control.MouseFilterEnum.Ignore };
            Label text = UiKit.Text(label, "Numeric");
            text.HorizontalAlignment = HorizontalAlignment.Center;
            chip.AddChild(text);
            _slots[slot] = chip;
            slots.AddChild(chip);
        }
        column.AddChild(slots);
        return column;
    }

    private VBoxContainer WolfPanel()
    {
        VBoxContainer column = UiKit.Column(UiTokens.S1 + 2);
        column.MouseFilter = Control.MouseFilterEnum.Ignore;

        HBoxContainer head = UiKit.Row(UiTokens.S2);
        head.AddChild(Icon.Make(IconKind.Wolf, 22, UiTokens.TeamOrange));
        _breedName = UiKit.Text("", "Subtitle", UiTokens.TextPrimary);
        head.AddChild(_breedName);
        head.AddChild(UiKit.Spacer());
        head.AddChild(UiKit.Caption("sem munição", UiTokens.TextDisabled));
        column.AddChild(head);

        string[] names = { "Garrada · clique", "Salto · direito no chão", "Bote · direito no ar" };
        for (int i = 0; i < 3; i++)
        {
            HBoxContainer row = UiKit.Row(UiTokens.S2);
            Label label = UiKit.Caption(names[i], UiTokens.TextPrimary);
            label.CustomMinimumSize = new Vector2(190, 0);
            row.AddChild(label);
            _cooldownBars[i] = Bar(UiTokens.TeamOrange.Lightened(0.2f), 6);
            _cooldownBars[i].Value = 1;
            row.AddChild(_cooldownBars[i]);
            column.AddChild(row);
        }

        HBoxContainer howl = UiKit.Row(UiTokens.S2);
        _howlLabel = UiKit.Caption("Uivo · R", UiTokens.TextPrimary);
        _howlLabel.CustomMinimumSize = new Vector2(190, 0);
        howl.AddChild(_howlLabel);
        _howlBar = Bar(UiTokens.Accent, 6);
        howl.AddChild(_howlBar);
        column.AddChild(howl);

        return column;
    }

    private void BuildTransform()
    {
        _shiftPanel = new PanelContainer { ThemeTypeVariation = "HudPanel", CustomMinimumSize = new Vector2(380, 0) };
        Corner(_shiftPanel, Control.LayoutPreset.CenterBottom);
        _shiftPanel.OffsetBottom = -Edge;
        _shiftPanel.OffsetLeft = -190;
        _shiftPanel.OffsetRight = 190;

        VBoxContainer column = UiKit.Column(UiTokens.S1);
        _shiftPanel.AddChild(column);

        HBoxContainer row = UiKit.Row(UiTokens.S2);
        var key = new PanelContainer { CustomMinimumSize = new Vector2(24, 22) };
        key.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Surface3, UiTokens.BorderStrong, 1, UiTokens.RadiusSmall, 6, 0));
        _shiftKey = UiKit.Text("3", "Numeric");
        _shiftKey.HorizontalAlignment = HorizontalAlignment.Center;
        key.AddChild(_shiftKey);
        row.AddChild(key);
        _shiftText = UiKit.Caption("Virar lobo", UiTokens.TextPrimary);
        row.AddChild(_shiftText);
        row.AddChild(UiKit.Spacer());
        _shiftState = UiKit.Caption("PRONTO", UiTokens.Success);
        row.AddChild(_shiftState);
        column.AddChild(row);

        _shiftBar = Bar(UiTokens.TeamOrange, 6);
        _shiftBar.Value = 1;
        column.AddChild(_shiftBar);
    }

    // ================================================================== peças

    private static ProgressBar Bar(Color fill, int height)
    {
        var bar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(0, height),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MaxValue = 1,
            Step = 0.001,
            ShowPercentage = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        bar.AddThemeStyleboxOverride("background", UiTheme.Box(new Color(1, 1, 1, 0.1f), default, 0, 0, 0, 0));
        bar.AddThemeStyleboxOverride("fill", UiTheme.Box(fill, default, 0, 0, 0, 0));
        return bar;
    }

    /// <summary>Contorno escuro no texto solto sobre o mundo — sem painel
    /// atrás, texto claro some em parede clara.</summary>
    private static T Outlined<T>(T label) where T : Label
    {
        label.AddThemeConstantOverride("outline_size", 5);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.75f));
        return label;
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

    private void Warn(string text, float seconds = 1.6f, Color? color = null)
    {
        _warning.Text = text;
        _warning.AddThemeColorOverride("font_color", color ?? UiTokens.Warning);
        _warningTimer = seconds;
    }

    // ================================================================== vida

    private void OnHealthChanged(float hp, float maxHp, float shield)
    {
        if (_hpNumber == null) return;

        _hpNumber.Text = $"{Mathf.Ceil(Mathf.Max(hp, 0)):0}";
        _hpMax.Text = $"/ {maxHp:0}";
        _hpBar.Value = maxHp > 0 ? hp / maxHp : 0;

        _shieldRow.Visible = _health.MaxShield > 0;
        _shieldBar.Value = _health.MaxShield > 0 ? shield / _health.MaxShield : 0;
        _shieldNumber.Text = $"{Mathf.Ceil(shield):0}";

        // Vida baixa: a barra pulsa e a vinheta entra.
        bool low = !_health.IsDead && maxHp > 0 && hp / maxHp < LowHealth;
        // Os estilos são criados uma vez: a vida muda a cada quadro (o
        // escudo do lobo escorre), e criar um estilo por quadro acumulava
        // objetos do lado C# que derrubavam o jogo ao fechar.
        if (low != _lowShown)
        {
            _lowShown = low;
            _hpBar.AddThemeStyleboxOverride("fill", low ? HpFillLow : HpFill);
            _hpNumber.AddThemeColorOverride("font_color", low ? UiTokens.Danger : UiTokens.TextPrimary);
        }

        if (low && _lowPulse == null) _lowPulse = Motion.Pulse(_hpBar, 0.35f, 0.7f);
        if (!low && _lowPulse != null)
        {
            _lowPulse.Kill();
            _lowPulse = null;
            _hpBar.Modulate = Colors.White;
        }

        UpdateVignette();
    }

    private void OnDamaged(float amount, Vector3 from)
    {
        if (_health.IsDead) return;
        _hurtFlash = Mathf.Min(1f, _hurtFlash + 0.45f);
        UpdateVignette();
    }

    private void UpdateVignette()
    {
        if (_vignette == null) return;
        bool low = !_health.IsDead && _health.MaxHp > 0 && _health.Hp / _health.MaxHp < LowHealth;
        float lowAlpha = low ? 0.6f + 0.2f * Mathf.Sin(Time.GetTicksMsec() / 180f) : 0f;
        _vignette.Modulate = new Color(1, 1, 1, Mathf.Clamp(Mathf.Max(lowAlpha, _hurtFlash * 0.55f), 0, 1));
    }

    private void OnDied(int killerId)
    {
        _death.Visible = true;
        _warning.Text = "";
        _warningTimer = 0;
        _crosshair.Visible = false;
        Motion.FadeIn(_death, UiTokens.Normal);
    }

    private void OnRespawned()
    {
        _death.Visible = false;
        _crosshair.Visible = true;
        _hurtFlash = 0;
        UpdateVignette();
    }

    // ================================================================== equipamento

    private void OnWpChanged(float wp, float max)
    {
        if (_wp == null) return;
        string text = $"{Mathf.Floor(wp):0}";
        bool changed = _wp.Text != text && _wp.Text != "0" || wp > 0 && _wp.Text == "0";
        _wp.Text = text;
        if (changed && IsInsideTree()) Motion.Pop(_wp, 1.25f);
    }

    private void OnAmmoChanged(int inMag, int magSize, bool reloading)
    {
        if (_mag == null || _form.IsWolf) return;

        _mag.Text = inMag.ToString();
        _mag.AddThemeColorOverride("font_color", inMag == 0 && !reloading ? UiTokens.Danger : UiTokens.TextPrimary);
        _reloading.Text = reloading ? "RECARREGANDO" : inMag <= magSize / 4 && magSize > 0 ? "R · RECARREGAR" : "";
    }

    private void OnSlotChanged(int slot)
    {
        if (_weaponName == null) return;

        WeaponDefinition held = _weapon.Held;
        Slot current = _weapon.CurrentSlot;

        string id = current switch
        {
            Slot.Knife => "faca",
            Slot.Grenade => "choque",
            _ => held?.Id ?? ""
        };
        _weaponArt.ItemId = id;
        _weaponName.Text = current switch
        {
            Slot.Knife => "FACA",
            Slot.Grenade => "GRANADA DE CHOQUE",
            _ => (held?.DisplayName ?? "").ToUpperInvariant()
        };

        bool showAmmo = current is Slot.Primary or Slot.Secondary;
        _mag.Visible = _reserve.Visible = showAmmo;

        // Espaço sem arma (soltou com B) fica apagado.
        _slots[Slot.Primary].Modulate = _weapon.HasPrimary ? Colors.White : new Color(1, 1, 1, 0.3f);
        _slots[Slot.Secondary].Modulate = _weapon.HasSecondary ? Colors.White : new Color(1, 1, 1, 0.3f);

        foreach (var (s, chip) in _slots)
        {
            bool on = s == current;
            chip.AddThemeStyleboxOverride("panel", on ? SlotOn : SlotOff);
            chip.GetChild<Label>(0).AddThemeColorOverride("font_color", on ? UiTokens.Accent : UiTokens.TextSecondary);
        }

        OnAmmoChanged(_weapon.AmmoInMag, _weapon.MagSize, _weapon.IsReloading);
    }

    private void OnGrenadesChanged(int left)
    {
        if (_grenades == null) return;
        _grenades.Text = left.ToString();
        _slots[Slot.Grenade].Modulate = left > 0 ? Colors.White : new Color(1, 1, 1, 0.35f);
    }

    private void OnHitConfirmed(bool headshot)
    {
        _hitMarker?.Flash(headshot);
    }

    private void OnAttackCooldown(int kind, float seconds)
    {
        if (kind < 0 || kind > 2) return;
        _cooldownTotal[kind] = Mathf.Max(seconds, 0.01f);
        _cooldownLeft[kind] = seconds;
    }

    private void UpdateWolf(float delta)
    {
        if (!_wolf.Visible) return;

        for (int i = 0; i < 3; i++)
        {
            _cooldownLeft[i] = Mathf.Max(0, _cooldownLeft[i] - delta);
            _cooldownBars[i].Value = 1f - _cooldownLeft[i] / _cooldownTotal[i];
        }

        float howl = _player?.HowlProgress ?? 0f;
        _howlBar.Value = howl;
        _howlLabel.Text = howl > 0 ? "UIVANDO · ESCUDO NO FIM" : "UIVO · R";
    }

    // ================================================================== forma

    private void OnFormChanged(int form) => OnFormChanged(form, animate: true);

    private void OnFormChanged(int form, bool animate)
    {
        bool wolf = _form.IsWolf;

        _formIcon.Kind = wolf ? IconKind.Wolf : IconKind.Human;
        _formIcon.Tint = wolf ? UiTokens.TeamOrange : UiTokens.TextPrimary;
        _formLabel.Text = (Form)form switch
        {
            Form.ShiftingToWolf => "VIRANDO LOBO",
            Form.ShiftingToHuman => "VOLTANDO A HUMANO",
            Form.Wolf => $"LOBO · {(_form.Breed?.DisplayName ?? "").ToUpperInvariant()}",
            _ => "HUMANO"
        };
        _breedName.Text = (_form.Breed?.DisplayName ?? "Lobo").ToUpperInvariant();

        // Dois painéis, um sai e o outro entra.
        VBoxContainer show = wolf ? _wolf : _human;
        VBoxContainer hide = wolf ? _human : _wolf;
        hide.Visible = false;
        if (!show.Visible || !animate)
        {
            show.Visible = true;
            if (animate) Motion.FadeIn(show, UiTokens.Normal);
        }

        if (!wolf) OnSlotChanged((int)_weapon.CurrentSlot);
    }

    private void UpdateTransform()
    {
        if (_shiftBar == null) return;

        bool blocked = RoomConfig.Rules.Transform == TransformRule.Bloqueada;

        if (_form.IsShifting)
        {
            float total = Mathf.Max(_form.ShiftTotal, 0.01f);
            _shiftBar.Value = 1f - _form.ShiftLeft / total;
            _shiftText.Text = _form.Current == Form.ShiftingToWolf ? "TRANSFORMANDO" : "VOLTANDO";
            _shiftState.Text = $"{_form.ShiftLeft:0.0} S  ·  VULNERÁVEL";
            _shiftState.AddThemeColorOverride("font_color", UiTokens.Danger);
            // O momento de maior risco da partida pulsa.
            _shiftPulse ??= Motion.Pulse(_shiftPanel, 0.55f, 0.35f);
            return;
        }

        if (_shiftPulse != null)
        {
            _shiftPulse.Kill();
            _shiftPulse = null;
            _shiftPanel.Modulate = Colors.White;
        }

        _shiftBar.Value = blocked ? 0 : 1;

        if (_form.IsWolf)
        {
            _shiftKey.Text = "1";
            _shiftText.Text = "VOLTAR A HUMANO";
            _shiftState.Text = "OU 2 · 4 · 5";
            _shiftState.AddThemeColorOverride("font_color", UiTokens.TextSecondary);
        }
        else
        {
            _shiftKey.Text = "3";
            _shiftText.Text = "VIRAR LOBO";
            _shiftState.Text = blocked ? "BLOQUEADO NESTA SALA" : "PRONTO";
            _shiftState.AddThemeColorOverride("font_color", blocked ? UiTokens.TextDisabled : UiTokens.Success);
        }
    }

    private void OnTransformDenied(float missing)
    {
        if (missing < 0) Warn("Lobo bloqueado nesta sala", 1.6f, UiTokens.TextSecondary);
        else Warn($"Faltam {missing:0} WP para transformar");
    }

    // ================================================================== feed

    private void OnKill(Kill kill)
    {
        if (_feed == null) return;

        string me = _health?.PublicName;
        bool mine = kill.Killer == me;
        bool died = kill.Victim == me;

        if (mine && kill.Victim != me) _hitMarker?.Flash(kill.Cause == KillCause.Headshot, kill: true);

        if (died) _deathBy.Text = KillFeed.HowDied(kill);

        // A linha em que você aparece leva borda dourada: é a que importa.
        var line = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        line.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(UiTokens.Bg, 0.66f),
            mine || died ? UiTokens.Accent : new Color(1, 1, 1, 0.06f), 1, 0, UiTokens.S2, 3));

        HBoxContainer row = UiKit.Row(UiTokens.S2);
        line.AddChild(row);

        if (kill.KillerTeam != 0 && kill.Killer != kill.Victim)
            row.AddChild(TeamName(kill.Killer, kill.KillerTeam));

        row.AddChild(Glyph(kill));
        if (kill.Cause == KillCause.Headshot) row.AddChild(Icon.Make(IconKind.Headshot, 16, UiTokens.Danger.Lightened(0.2f)));
        row.AddChild(TeamName(kill.Victim, kill.VictimTeam));

        _feed.AddChild(line);
        Motion.FadeIn(line, UiTokens.Fast);

        // Só as cinco últimas: um feed que cresce vira parede de texto.
        while (_feed.GetChildCount() > FeedLines)
        {
            Node old = _feed.GetChild(0);
            _feed.RemoveChild(old);
            old.QueueFree();
        }

        Tween life = line.CreateTween();
        life.TweenInterval(FeedLife - 0.4f);
        life.TweenProperty(line, "modulate:a", 0f, 0.4f);
        life.TweenCallback(Callable.From(line.QueueFree));
    }

    private static Label TeamName(string name, int team)
    {
        Label label = UiKit.Text(name, null, Teams.ColorOf(team));
        label.AddThemeFontOverride("font", UiFonts.SemiBold);
        return label;
    }

    /// <summary>Ícone da arma no meio da linha: silhueta pequena para arma
    /// de fogo, garra para lobo, faca, granada, queda.</summary>
    private static Control Glyph(Kill kill)
    {
        switch (kill.Cause)
        {
            case KillCause.Wolf: return Icon.Make(IconKind.Claw, 18, UiTokens.TeamOrange);
            case KillCause.Knife: return Icon.Make(IconKind.Knife, 18, UiTokens.TextPrimary);
            case KillCause.Grenade: return Icon.Make(IconKind.Grenade, 18, UiTokens.TextPrimary);
            case KillCause.Fall: return Icon.Make(IconKind.Fall, 18, UiTokens.TextSecondary);
        }

        if (Catalog.Get(kill.Weapon) == null) return Icon.Make(IconKind.Gun, 18, UiTokens.TextPrimary);
        WeaponArt art = WeaponArt.Make(kill.Weapon, new Vector2(58, 22));
        art.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        art.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return art;
    }
}
