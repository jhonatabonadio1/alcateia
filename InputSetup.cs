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
        Key("howl", Godot.Key.F);
        Mouse("attack", MouseButton.Left);

        // Slots: o lobo é o 3, na mesma memória muscular das armas.
        Key("slot_1", Godot.Key.Key1);
        Key("slot_2", Godot.Key.Key2);
        Key("slot_3", Godot.Key.Key3);
        Key("slot_4", Godot.Key.Key4);
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
