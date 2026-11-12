using Godot;

/// <summary>
/// Registra as ações de input por código, se ainda não existirem.
///
/// Evita a configuração manual do Input Map — que é demorada, fácil de
/// errar e falha em silêncio quando a ação é criada sem tecla atribuída.
///
/// Ações definidas no editor têm prioridade: este código não sobrescreve
/// nada que já exista.
/// </summary>
public static class InputSetup
{
    public static void EnsureActions()
    {
        Key("move_forward", Godot.Key.W);
        Key("move_back", Godot.Key.S);
        Key("move_left", Godot.Key.A);
        Key("move_right", Godot.Key.D);
        Key("jump", Godot.Key.Space);
        Key("howl", Godot.Key.R);
        Key("crouch", Godot.Key.Ctrl);
        Key("walk", Godot.Key.Shift);
        // Menu no Esc e placar no Tab, como em quase todo FPS — e como
        // no WolfTeam. Tab abria o menu de dificuldade porque não havia
        // placar quando ele nasceu.
        Key("menu", Godot.Key.Escape);
        Key("scoreboard", Godot.Key.Tab);
        // B solta a arma da mão, como no CS.
        Key("drop", Godot.Key.B);
        Mouse("attack", MouseButton.Left);
        Mouse("special", MouseButton.Right);

        // Slots: o lobo é o 3, na mesma memória muscular das armas.
        Key("slot_1", Godot.Key.Key1);
        Key("slot_2", Godot.Key.Key2);
        Key("slot_3", Godot.Key.Key3);
        Key("slot_4", Godot.Key.Key4);
        Key("slot_5", Godot.Key.Key5);
    }

    private static void Key(string action, Key key)
    {
        if (InputMap.HasAction(action)) return;

        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }

    private static void Mouse(string action, MouseButton button)
    {
        if (InputMap.HasAction(action)) return;

        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
    }
}
