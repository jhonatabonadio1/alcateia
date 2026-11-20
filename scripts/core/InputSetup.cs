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
		// Só o Ctrl agacha (o C saiu a pedido do Jhon, 18/11). No Mac,
		// agachado no Ctrl, o botão direito de verdade mira (MacCtrlClick).
		Key("walk", Godot.Key.Shift);
		// Menu no Esc e placar no Tab, como em quase todo FPS — e como
		// no WolfTeam. Tab abria o menu de dificuldade porque não havia
		// placar quando ele nasceu.
		Key("menu", Godot.Key.Escape);
		Key("scoreboard", Godot.Key.Tab);
		// G solta a arma da mão (decisão do Jhon; era o B do CS).
		Key("drop", Godot.Key.G);
		Mouse("attack", MouseButton.Left);
		Mouse("special", MouseButton.Right);
		// Q: o especial do lobo, num toque (decisão do Jhon, 18/11; era
		// segurar o botão direito no chão para carregar).
		Key("pounce", Godot.Key.Q);
		// O guia das habilidades: aparece enquanto se segura (18/11, "igual Overwatch").
		Key("guide", Godot.Key.E);
		// Z: a lista dos calls de rádio do time (Radio, Hud.Radio).
		Key("radio", Godot.Key.Z);
		// V: a coronhada (Weapon.Butt).
		Key("melee", Godot.Key.V);
		// F, morto: solta a câmera de espectador (KillCam).
		Key("spectate_free", Godot.Key.F);
		// F, vivo: olha a arma na mão (a faca gira; a pistola é conferida) —
		// a animação "inspecionar" do conjunto (ViewModel.Rig).
		Key("inspect", Godot.Key.F);

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

	/// <summary>Segunda tecla para uma ação que já existe.</summary>
	private static void Also(string action, Key key)
	{
		var extra = new InputEventKey { PhysicalKeycode = key };
		foreach (InputEvent existing in InputMap.ActionGetEvents(action))
			if (existing is InputEventKey k && k.PhysicalKeycode == key) return;
		InputMap.ActionAddEvent(action, extra);
	}

	private static void Mouse(string action, MouseButton button)
	{
		if (InputMap.HasAction(action)) return;

		InputMap.AddAction(action);
		InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
	}
}
