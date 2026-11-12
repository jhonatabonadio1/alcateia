using Godot;
using System.Collections.Generic;

/// <summary>
/// Tela de respawn: escolher um dos quatro conjuntos e pagar.
///
/// Só seleciona. Montar conjunto é coisa do editor, fora da partida
/// (docs/CONJUNTOS.md): se desse para montar aqui, a decisão perderia o
/// custo e viraria otimização sem fim no meio da luta.
///
/// Cinco segundos para decidir, depois entra o último conjunto usado —
/// ou o padrão, se o último não couber no WP. Nunca trava esperando:
/// quem está morto precisa voltar à luta.
///
/// Os conjuntos que não cabem no WP aparecem acinzentados mas visíveis,
/// com quanto falta. Ver o que não se pode pagar é o que dá sentido ao
/// próximo abate.
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

    private Control _root;
    private Label _wpLabel;
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
        // tiver escolhido o conjunto.
        _health.AutoRespawn = false;
        _health.Died += _ => Open();

        Build();
        Open();
    }

    public override void _Process(double delta)
    {
        if (!_open) return;

        // O relógio só corre quando esta é a tela de cima.
        if (!UiState.IsAlone) return;

        _remaining -= delta;
        RefreshTimer();

        if (_remaining > 0) return;

        Choose(Fallback(), timedOut: true);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_open) return;

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

    /// <summary>O último usado, se couber; senão o padrão.</summary>
    private int Fallback()
    {
        int last = Profile.LastUsed;
        LoadoutSet set = Profile.Sets[last];

        return set.IsDefault || set.IsAffordable(_form.Wp) ? last : 0;
    }

    private void Open()
    {
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

    private void Choose(int index, bool timedOut)
    {
        if (!_open || index < 0 || index >= Profile.Sets.Count) return;

        LoadoutSet set = Profile.Sets[index];

        // Clique num conjunto que não cabe não faz nada — ele está ali
        // para ser visto, não escolhido. No estouro do tempo isso não
        // acontece, porque o reserva já é sempre pagável.
        if (!set.IsDefault && !set.IsAffordable(_form.Wp))
        {
            if (!timedOut) return;

            index = 0;
            set = Profile.Sets[0];
        }

        if (set.IsDefault) _form.AddWp(DefaultBonus);
        else _form.TrySpend(set.TotalCost());

        _weapon.Equip(set);

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
        _root = new ColorRect
        {
            Color = new Color(0.02f, 0.03f, 0.05f, 0.78f),
            AnchorRight = 1f,
            AnchorBottom = 1f
        };
        AddChild(_root);

        var box = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            AnchorTop = 0.5f, AnchorBottom = 0.5f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both
        };
        box.AddThemeConstantOverride("separation", 14);
        _root.AddChild(box);

        var title = MakeLabel("ESCOLHA O CONJUNTO", 30, Colors.White);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);

        _wpLabel = MakeLabel("", 22, new Color(1f, 0.85f, 0.3f));
        _wpLabel.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(_wpLabel);

        _cards = new HBoxContainer();
        _cards.AddThemeConstantOverride("separation", 12);
        box.AddChild(_cards);

        _timer = MakeLabel("", 16, new Color(0.8f, 0.8f, 0.83f));
        _timer.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(_timer);

        var wolf = MakeLabel("Lobo: grátis, a qualquer hora — tecla 3", 14, new Color(0.65f, 0.65f, 0.7f));
        wolf.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(wolf);

        _root.Visible = false;
    }

    private void Refresh()
    {
        _wpLabel.Text = $"WP disponível: {_form.Wp:0}";

        foreach (Node child in _cards.GetChildren())
        {
            _cards.RemoveChild(child);
            child.QueueFree();
        }

        for (int i = 0; i < Profile.Sets.Count; i++)
            _cards.AddChild(Card(i));

        RefreshTimer();
    }

    private void RefreshTimer()
    {
        int fallback = Fallback();
        _timer.Text = $"entra em {Mathf.CeilToInt((float)Mathf.Max(_remaining, 0))} s: " +
                      Profile.Sets[fallback].Name;
    }

    private Control Card(int index)
    {
        LoadoutSet set = Profile.Sets[index];
        int cost = set.TotalCost();
        bool affordable = set.IsDefault || set.IsAffordable(_form.Wp);
        bool last = index == Profile.LastUsed;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.09f, 0.1f, 0.13f, 0.97f),
            BorderColor = last ? new Color(1f, 0.85f, 0.3f) : new Color(1, 1, 1, 0.12f),
            ContentMarginLeft = 16, ContentMarginRight = 16,
            ContentMarginTop = 14, ContentMarginBottom = 14
        };
        style.SetBorderWidthAll(last ? 3 : 1);
        style.SetCornerRadiusAll(8);

        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(210, 230),
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = affordable ? Control.CursorShape.PointingHand : Control.CursorShape.Forbidden,
            Modulate = affordable ? Colors.White : new Color(1, 1, 1, 0.45f)
        };
        card.AddThemeStyleboxOverride("panel", style);

        // O cartão inteiro é o botão: clicar nele escolhe.
        card.GuiInput += e =>
        {
            if (e is InputEventMouseButton click && click.Pressed && click.ButtonIndex == MouseButton.Left)
                Choose(index, timedOut: false);
        };

        var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 8);
        card.AddChild(stack);

        stack.AddChild(MakeLabel($"[{index + 1}]  {set.Name}", 20, Colors.White));

        var items = MakeLabel(set.Describe(), 15, new Color(0.82f, 0.84f, 0.88f));
        items.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        stack.AddChild(items);

        string price = set.IsDefault ? $"GRÁTIS  (+{DefaultBonus} WP)" : $"{cost} WP";
        stack.AddChild(MakeLabel(price, 20, set.IsDefault ? new Color(0.5f, 0.95f, 0.5f) : new Color(1f, 0.85f, 0.3f)));

        string status = affordable ? "✓ disponível" : $"faltam {cost - _form.Wp:0}";
        stack.AddChild(MakeLabel(status, 14,
            affordable ? new Color(0.5f, 0.95f, 0.5f) : new Color(1f, 0.45f, 0.4f)));

        return card;
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
