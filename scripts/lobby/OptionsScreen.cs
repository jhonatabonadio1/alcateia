using Godot;
using System;

/// <summary>
/// Opções: volume por canal, sensibilidade do mouse, a tabela de
/// controles e recomeçar o perfil. Tudo aplica na hora e salva em
/// user://opcoes.json.
/// </summary>
public partial class OptionsScreen : LobbyScreen
{
    public override IconKind NavIcon => IconKind.Options;

    protected override void Build()
    {
        MarginContainer margin = UiKit.Fill(UiKit.Margin(UiTokens.S5, UiTokens.S4, UiTokens.S5, UiTokens.S4));
        AddChild(margin);
        VBoxContainer page = UiKit.Column(UiTokens.S3);
        margin.AddChild(page);

        page.AddChild(UiKit.Text("Opções", "Title"));

        HBoxContainer columns = UiKit.Row(UiTokens.S4);
        columns.SizeFlagsVertical = SizeFlags.ExpandFill;
        page.AddChild(columns);

        VBoxContainer left = UiKit.Column(UiTokens.S4);
        left.CustomMinimumSize = new Vector2(560, 0);
        columns.AddChild(left);
        left.AddChild(AudioPanel());
        left.AddChild(MousePanel());
        left.AddChild(ProfilePanel());

        columns.AddChild(ControlsPanel());
    }

    // ------------------------------------------------------------------ painéis

    public static Control AudioPanel()
    {
        (PanelContainer panel, VBoxContainer column) = Section("Áudio");
        Settings.Data v = Settings.Values;

        column.AddChild(Slider("Geral", v.Master, x => { v.Master = x; Changed(); }));
        column.AddChild(Slider("Efeitos", v.Sfx, x => { v.Sfx = x; Changed(); }));
        column.AddChild(Slider("Interface", v.Interface, x => { v.Interface = x; Changed(); }));
        column.AddChild(Slider("Música e ambiente", v.Music, x => { v.Music = x; Changed(); }));
        return panel;

        static void Changed()
        {
            Settings.ApplyAudio();
            Settings.Save();
        }
    }

    public static Control MousePanel()
    {
        (PanelContainer panel, VBoxContainer column) = Section("Mouse");
        Settings.Data v = Settings.Values;
        column.AddChild(Slider("Sensibilidade", v.Sensitivity, x => { v.Sensitivity = x; Settings.Save(); },
            0.25f, 3f, x => $"{x:0.00}×"));
        column.AddChild(UiKit.Paragraph("Vale também no meio da partida.", "Caption"));
        return panel;
    }

    private Control ProfilePanel()
    {
        (PanelContainer panel, VBoxContainer column) = Section("Perfil");
        column.AddChild(UiKit.Paragraph("O perfil fica salvo neste computador. Recomeçar apaga patente, XP, Presas, inventário, conjuntos e missões."));

        Button reset = UiKit.Button("Recomeçar perfil", ButtonKind.Danger, ConfirmReset);
        reset.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        column.AddChild(reset);
        return panel;
    }

    private static Control ControlsPanel()
    {
        (PanelContainer panel, VBoxContainer column) = Section("Controles");
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        column.AddChild(ControlsTable.Build());
        return panel;
    }

    private void ConfirmReset()
    {
        Label body = UiKit.Paragraph("Tudo volta ao começo: Filhote I, 3.000 Presas, só o equipamento inicial. Não dá para desfazer.");
        body.CustomMinimumSize = new Vector2(400, 0);

        Shell.Modal("Recomeçar perfil?", body,
            new Modal.Choice("Recomeçar", ButtonKind.Danger, () =>
            {
                Profile.Reset();
                RoomDirectory.Current = null;
                Shell.Go(new WelcomeScreen());
            }),
            new Modal.Choice("Cancelar", ButtonKind.Primary, null));
    }

    // ------------------------------------------------------------------ peças

    private static (PanelContainer, VBoxContainer) Section(string title)
    {
        PanelContainer panel = UiKit.Panel("Surface1");
        MarginContainer margin = UiKit.Margin(UiTokens.S4);
        panel.AddChild(margin);
        VBoxContainer column = UiKit.Column(UiTokens.S3);
        margin.AddChild(column);
        column.AddChild(UiKit.Caption(title));
        return (panel, column);
    }

    private static Control Slider(string label, float value, Action<float> changed,
        float min = 0f, float max = 1f, Func<float, string> format = null)
    {
        format ??= x => $"{x * 100:0}%";

        HBoxContainer row = UiKit.Row(UiTokens.S3);
        Label name = UiKit.Text(label, "Muted");
        name.CustomMinimumSize = new Vector2(170, 0);
        row.AddChild(name);

        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = 0.01,
            Value = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(0, 20),
            FocusMode = FocusModeEnum.All
        };
        row.AddChild(slider);

        Label shown = UiKit.Text(format(value), "Numeric");
        shown.CustomMinimumSize = new Vector2(64, 0);
        shown.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(shown);

        slider.ValueChanged += x =>
        {
            shown.Text = format((float)x);
            changed((float)x);
        };
        slider.DragEnded += _ => Sfx.Ui("ui_clique");
        return row;
    }
}
