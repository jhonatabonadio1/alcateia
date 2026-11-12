using Godot;

/// <summary>
/// Entrada: só no primeiro acesso (ou perfil novo). Pede o nickname e
/// mostra as novidades da versão.
///
/// Sem servidor não há login de verdade: o perfil é local, e o nome é a
/// identidade. O nickname é fixo depois daqui — trocar exige o item
/// "Troca de nome" da loja.
/// </summary>
public partial class WelcomeScreen : LobbyScreen
{
    private LineEdit _field;
    private Label _error;
    private Button _enter;

    /// <summary>Notas da versão, da mais nova para a mais velha. O texto
    /// completo está em docs/CHANGELOG.md.</summary>
    private static readonly (string Version, string[] Lines)[] News =
    {
        ("v0.24", new[]
        {
            "Lobby novo: salas, loja, inventário, perfil, missões e opções.",
            "Patentes, XP e Presas: cada partida rende progresso.",
            "Times azul e laranja.",
            "HUD de combate e placar refeitos.",
        }),
        ("v0.23", new[] { "Sons para tudo: armas, passos, lobo, interface." }),
        ("v0.21", new[] { "Arsenal, conjuntos e a economia de WP." }),
    };

    protected override void Build()
    {
        var center = UiKit.Fill(new CenterContainer());
        AddChild(center);

        HBoxContainer row = UiKit.Row(UiTokens.S5);
        center.AddChild(row);

        row.AddChild(Form());
        row.AddChild(NewsPanel());

        _field.CallDeferred(Control.MethodName.GrabFocus);
    }

    private Control Form()
    {
        PanelContainer panel = UiKit.Panel("Surface1");
        panel.CustomMinimumSize = new Vector2(460, 420);

        VBoxContainer column = UiKit.Column(UiTokens.S3);
        panel.AddChild(UiKit.Margin(UiTokens.S4));
        panel.GetChild(0).AddChild(column);

        HBoxContainer brand = UiKit.Row(UiTokens.S3);
        brand.AddChild(Icon.Make(IconKind.Wolf, 48, UiTokens.Accent));
        Label title = UiKit.Text("ALCATEIA", "Display");
        brand.AddChild(title);
        column.AddChild(brand);

        column.AddChild(UiKit.Paragraph("Humano contra lobo. Escolha o nome que vai aparecer no placar, no feed e para quem estiver na sua sala."));
        column.AddChild(UiKit.Gap(UiTokens.S2));

        column.AddChild(UiKit.Caption("Nickname"));
        _field = UiKit.Field("", "3 a 16 caracteres", Profile.NameMax);
        _field.TextChanged += _ => Validate();
        _field.TextSubmitted += _ => Enter();
        column.AddChild(_field);

        _error = UiKit.Text(" ", "Caption", UiTokens.Danger);
        column.AddChild(_error);

        column.AddChild(UiKit.Paragraph("Letras, números, _ e -. O nome é fixo: trocar depois exige o item Troca de nome, da loja.", "Caption"));
        column.AddChild(UiKit.Spacer());

        _enter = UiKit.Button("Entrar", ButtonKind.Primary, Enter);
        _enter.Disabled = true;
        column.AddChild(_enter);

        return panel;
    }

    private static Control NewsPanel()
    {
        PanelContainer panel = UiKit.Panel("Surface1");
        panel.CustomMinimumSize = new Vector2(420, 420);

        MarginContainer margin = UiKit.Margin(UiTokens.S4);
        panel.AddChild(margin);
        VBoxContainer column = UiKit.Column(UiTokens.S3);
        margin.AddChild(column);

        column.AddChild(UiKit.Caption("Novidades"));

        foreach (var (version, lines) in News)
        {
            HBoxContainer head = UiKit.Row(UiTokens.S2);
            head.AddChild(UiKit.Chip(version, UiTokens.Accent));
            column.AddChild(head);

            foreach (string line in lines)
            {
                HBoxContainer item = UiKit.Row(UiTokens.S2);
                item.AddChild(Icon.Make(IconKind.Chevrons, 12, UiTokens.TextDisabled));
                item.AddChild(UiKit.Paragraph(line, "Muted"));
                column.AddChild(item);
            }
        }

        return panel;
    }

    private void Validate()
    {
        string error = string.IsNullOrWhiteSpace(_field.Text) ? null : Profile.ValidateName(_field.Text);
        _error.Text = error ?? " ";
        _enter.Disabled = string.IsNullOrWhiteSpace(_field.Text) || error != null;
    }

    private void Enter()
    {
        if (!Profile.SetName(_field.Text, out string error))
        {
            _error.Text = error;
            Sfx.Ui("ui_negado");
            return;
        }

        Shell.GoRooms();
    }
}
