using Godot;

/// <summary>A tabela de controles, igual na tela de opções e no menu da
/// partida. A fonte é o CLAUDE.md ("Controles"); mudou uma tecla em
/// InputSetup, muda aqui também.</summary>
public static class ControlsTable
{
    private static readonly (string Key, string Action)[] Rows =
    {
        ("W A S D", "Andar"),
        ("Espaço", "Pular · saltar da parede"),
        ("Shift", "Andar devagar e em silêncio (humano)"),
        ("Ctrl", "Agachar"),
        ("Clique esquerdo", "Atacar"),
        ("Clique direito", "Bote aéreo (lobo, no ar)"),
        ("1", "Arma principal"),
        ("2", "Arma secundária"),
        ("3", "Virar lobo"),
        ("4", "Faca"),
        ("5", "Equipar granada"),
        ("R", "Recarregar (humano) · uivar segurando (lobo)"),
        ("B", "Soltar a arma da mão (cai à frente)"),
        ("W segurado", "Sustenta a corrida em parede"),
        ("Tab", "Placar (segurar)"),
        ("Esc", "Menu da partida — não pausa"),
    };

    public static Control Build()
    {
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", UiTokens.S4);
        grid.AddThemeConstantOverride("v_separation", UiTokens.S1 + 2);

        foreach (var (key, action) in Rows)
        {
            var keyBox = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
            keyBox.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Surface3, UiTokens.BorderStrong, 1, UiTokens.RadiusSmall, UiTokens.S2, 2));
            Label keyLabel = UiKit.Text(key, "Caption", UiTokens.TextPrimary);
            keyBox.AddChild(keyLabel);
            keyBox.CustomMinimumSize = new Vector2(150, 0);
            grid.AddChild(keyBox);
            grid.AddChild(UiKit.Text(action, "Muted"));
        }

        return grid;
    }
}
