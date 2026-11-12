using Godot;

/// <summary>
/// Menu do Esc dentro da partida: continuar, reiniciar, voltar ao menu
/// ou sair do jogo.
///
/// **Não pausa.** A partida continua correndo, os bots continuam
/// atirando e o relógio continua descendo. É assim em jogo multijogador
/// — não existe pausa quando há outras pessoas na sala — e treinar com
/// a regra certa desde agora evita o hábito de abrir o menu para fugir
/// de uma luta.
///
/// O corpo do jogador fica parado enquanto o menu está aberto (quem
/// segura isso é o Player, lendo o UiState): o mouse é do menu, e uma
/// tecla esquecida não pode levar o personagem andando sozinho.
/// </summary>
public partial class GameMenu : CanvasLayer
{
    public const string MenuScene = "res://scenes/menu/menu.tscn";

    private Control _root;
    private bool _open;

    public override void _Ready()
    {
        Layer = 10;
        ProcessMode = ProcessModeEnum.Always;
        Build();
        _root.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (!@event.IsActionPressed("menu")) return;

        if (_open) Close();
        else Open();

        GetViewport().SetInputAsHandled();
    }

    private void Open()
    {
        _open = true;
        _root.Visible = true;
        UiState.Push();
    }

    private void Close()
    {
        if (!_open) return;

        _open = false;
        _root.Visible = false;
        UiState.Pop();
    }

    private void Restart()
    {
        // O contador de menus sobrevive à troca de cena; zerar antes
        // evita que a cena nova nasça achando que há menus abertos.
        UiState.Reset();
        GetTree().ReloadCurrentScene();
    }

    private void BackToMenu()
    {
        UiState.Reset();
        GetTree().ChangeSceneToFile(MenuScene);
    }

    // ------------------------------------------------------------------

    private void Build()
    {
        _root = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.45f),
            AnchorRight = 1f,
            AnchorBottom = 1f
        };
        AddChild(_root);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.08f, 0.1f, 0.95f),
            ContentMarginLeft = 28, ContentMarginRight = 28,
            ContentMarginTop = 22, ContentMarginBottom = 24
        };
        style.SetCornerRadiusAll(8);

        var panel = new PanelContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            AnchorTop = 0.5f, AnchorBottom = 0.5f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both
        };
        panel.AddThemeStyleboxOverride("panel", style);
        _root.AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 10);
        panel.AddChild(stack);

        var title = new Label { Text = "MENU", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 26);
        stack.AddChild(title);

        var note = new Label
        {
            Text = "a partida continua rodando",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        note.AddThemeFontSizeOverride("font_size", 13);
        note.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.45f));
        stack.AddChild(note);

        stack.AddChild(MakeButton("Continuar", Close));
        stack.AddChild(MakeButton("Reiniciar partida", Restart));
        stack.AddChild(MakeButton("Sair para o menu", BackToMenu));
        stack.AddChild(MakeButton("Sair do jogo", () => GetTree().Quit()));
    }

    private static Button MakeButton(string text, System.Action action)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(260, 44),
            FocusMode = Control.FocusModeEnum.None
        };
        button.AddThemeFontSizeOverride("font_size", 18);
        button.Pressed += action;
        return button;
    }
}
