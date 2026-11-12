using Godot;
using System;

/// <summary>Os três níveis de botão do guia: um primário por tela, o
/// resto secundário ou terciário.</summary>
public enum ButtonKind
{
    Primary,
    Secondary,
    Tertiary,
    Danger,
    Tab,
    Icon
}

/// <summary>
/// Fábrica dos controles básicos, já com o tema certo. As telas montam
/// a interface por código, então em vez de cenas-componente o kit
/// oferece funções: <c>UiKit.Button("Entrar", ButtonKind.Primary, Entrar)</c>.
///
/// Toda tela nova usa isto. Controle criado "na mão" com tamanho e cor
/// próprios é o caminho mais curto para a interface perder a grade.
/// </summary>
public static class UiKit
{
    // ------------------------------------------------------------------ texto

    /// <summary>Rótulo numa das variações do tema (Display, Title,
    /// Subtitle, Caption, Muted, Numeric, NumericLarge) ou corpo, se
    /// <paramref name="variation"/> vier vazio.</summary>
    public static Label Text(string text, string variation = null, Color? color = null)
    {
        var label = new Label { Text = text ?? "", ThemeTypeVariation = variation ?? "" };
        if (color.HasValue) label.AddThemeColorOverride("font_color", color.Value);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        return label;
    }

    /// <summary>Rótulo pequeno em caixa alta, para cabeçalho de coluna e
    /// nome de campo.</summary>
    public static Label Caption(string text, Color? color = null) =>
        Text((text ?? "").ToUpperInvariant(), "Caption", color);

    /// <summary>Texto que corta com reticências em vez de quebrar linha
    /// ou empurrar o resto.</summary>
    public static Label Truncated(string text, string variation = null, Color? color = null)
    {
        Label label = Text(text, variation, color);
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.ClipText = true;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return label;
    }

    /// <summary>Parágrafo que quebra linha dentro da largura que tiver.</summary>
    public static Label Paragraph(string text, string variation = "Muted")
    {
        Label label = Text(text, variation);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return label;
    }

    // ------------------------------------------------------------------ botões

    public static Button Button(string text, ButtonKind kind = ButtonKind.Secondary, Action pressed = null)
    {
        var button = new Button
        {
            Text = text,
            ThemeTypeVariation = kind switch
            {
                ButtonKind.Primary => "PrimaryButton",
                ButtonKind.Tertiary => "TertiaryButton",
                ButtonKind.Danger => "DangerButton",
                ButtonKind.Tab => "TabButton",
                ButtonKind.Icon => "IconButton",
                _ => ""
            },
            CustomMinimumSize = new Vector2(0, kind == ButtonKind.Tab ? 36 : UiTokens.ButtonHeight),
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };

        if (kind == ButtonKind.Tab) button.Text = text.ToUpperInvariant();
        if (pressed != null) button.Pressed += pressed;

        Sfx.Wire(button);
        return button;
    }

    /// <summary>Botão só com ícone, com dica ao passar o mouse.</summary>
    public static Button IconButton(IconKind icon, string tooltip, Action pressed = null, int size = 20)
    {
        Button button = Button("", ButtonKind.Icon, pressed);
        button.TooltipText = tooltip;
        button.CustomMinimumSize = new Vector2(size + UiTokens.S3, size + UiTokens.S3);

        var glyph = new Icon { Kind = icon, CustomMinimumSize = new Vector2(size, size) };
        glyph.SetAnchorsPreset(Control.LayoutPreset.Center);
        glyph.Position = -glyph.CustomMinimumSize / 2;
        glyph.MouseFilter = Control.MouseFilterEnum.Ignore;
        button.AddChild(glyph);

        // A cor do ícone acompanha o estado do botão.
        button.MouseEntered += () => glyph.Tint = UiTokens.TextPrimary;
        button.MouseExited += () => glyph.Tint = UiTokens.TextSecondary;
        glyph.Tint = UiTokens.TextSecondary;
        return button;
    }

    // ------------------------------------------------------------------ campos

    public static LineEdit Field(string text, string placeholder = "", int maxLength = 0)
    {
        var field = new LineEdit
        {
            Text = text ?? "",
            PlaceholderText = placeholder,
            MaxLength = maxLength,
            CustomMinimumSize = new Vector2(0, UiTokens.FieldHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CaretBlink = true
        };
        return field;
    }

    public static OptionButton Picker(string[] labels, int selected, Action<int> chosen, bool[] disabled = null)
    {
        var picker = new OptionButton
        {
            CustomMinimumSize = new Vector2(0, UiTokens.FieldHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All,
            FitToLongestItem = false
        };

        for (int i = 0; i < labels.Length; i++)
        {
            picker.AddItem(labels[i], i);
            if (disabled != null && i < disabled.Length && disabled[i])
                picker.SetItemDisabled(i, true);
        }

        if (labels.Length > 0)
            picker.Select(Mathf.Clamp(selected, 0, labels.Length - 1));

        if (chosen != null)
            picker.ItemSelected += index => chosen((int)index);

        Sfx.Wire(picker);
        return picker;
    }

    public static CheckBox Check(string text, bool on, Action<bool> toggled)
    {
        var check = new CheckBox
        {
            Text = text,
            ButtonPressed = on,
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        if (toggled != null) check.Toggled += value => toggled(value);
        Sfx.Wire(check);
        return check;
    }

    // ------------------------------------------------------------------ caixas

    public static PanelContainer Panel(string variation = "Surface1")
    {
        return new PanelContainer { ThemeTypeVariation = variation };
    }

    public static VBoxContainer Column(int separation = UiTokens.S2)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", separation);
        return box;
    }

    public static HBoxContainer Row(int separation = UiTokens.S2)
    {
        var box = new HBoxContainer();
        box.AddThemeConstantOverride("separation", separation);
        return box;
    }

    public static MarginContainer Margin(int all) => Margin(all, all, all, all);

    public static MarginContainer Margin(int left, int top, int right, int bottom)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", left);
        margin.AddThemeConstantOverride("margin_top", top);
        margin.AddThemeConstantOverride("margin_right", right);
        margin.AddThemeConstantOverride("margin_bottom", bottom);
        return margin;
    }

    /// <summary>Espaço elástico: empurra o que vem depois para a ponta.</summary>
    public static Control Spacer() => new Control
    {
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    /// <summary>Espaço fixo, em unidades da grade.</summary>
    public static Control Gap(int pixels) => new Control
    {
        CustomMinimumSize = new Vector2(pixels, pixels),
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    public static HSeparator Divider() => new HSeparator();

    /// <summary>Etiqueta pequena com fundo colorido: modo de jogo,
    /// estado, raridade.</summary>
    public static PanelContainer Chip(string text, Color color, bool solid = false)
    {
        var chip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        chip.AddThemeStyleboxOverride("panel", UiTheme.Box(
            solid ? color : new Color(color, 0.14f),
            new Color(color, solid ? 1f : 0.45f), 1, UiTokens.RadiusSmall, UiTokens.S2, 2));
        chip.AddChild(Text(text.ToUpperInvariant(), "Caption", solid ? UiTokens.AccentText : color));
        chip.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return chip;
    }

    /// <summary>Linha "rótulo à esquerda, valor à direita" — o formato
    /// de formulário do painel de regras da sala.</summary>
    public static HBoxContainer FormRow(string label, Control value, int labelWidth = 150)
    {
        HBoxContainer row = Row(UiTokens.S3);
        Label name = Caption(label);
        name.CustomMinimumSize = new Vector2(labelWidth, 0);
        name.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(name);
        value.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(value);
        return row;
    }

    /// <summary>Remove e libera todos os filhos.</summary>
    public static void Clear(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>Preenche o pai inteiro.</summary>
    public static T Fill<T>(T control) where T : Control
    {
        control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return control;
    }
}
