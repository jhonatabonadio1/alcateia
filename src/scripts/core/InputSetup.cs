using Godot;
using System.Collections.Generic;

/// <summary>
/// Registra as ações de input por código, se ainda não existirem.
///
/// Evita a configuração manual do Input Map — que é demorada, fácil de
/// errar e falha em silêncio quando a ação é criada sem tecla atribuída.
///
/// Ações definidas no editor têm prioridade: este código não sobrescreve
/// nada que já exista. A tabela abaixo é também o **padrão de fábrica** das
/// teclas que o jogador troca nas Opções → Controles (<see cref="KeyBindings"/>):
/// depois de criar as ações, as trocas salvas vão por cima.
/// </summary>
public static class InputSetup
{
	/// <summary>Cada ação e a tecla (ou botão) de fábrica, no texto curto do
	/// <see cref="KeyBindings.Encode"/>: "K:" e a tecla física, "M:" e o botão.</summary>
	private static readonly (string Action, string Default)[] Defaults =
	{
		("move_forward", K(Key.W)),
		("move_back", K(Key.S)),
		("move_left", K(Key.A)),
		("move_right", K(Key.D)),
		("jump", K(Key.Space)),
		("howl", K(Key.R)),
		// Só o Ctrl agacha (o C saiu a pedido do Jhon, 18/11). No Mac,
		// agachado no Ctrl, o botão direito de verdade mira (MacCtrlClick).
		("crouch", K(Key.Ctrl)),
		// Shift: o humano corre (26/11, pedido do Jhon; antes andava devagar
		// e em silêncio — quem quer silêncio agacha).
		("sprint", K(Key.Shift)),
		// Menu no Esc e placar no Tab, como em quase todo FPS — e como
		// no WolfTeam. Tab abria o menu de dificuldade porque não havia
		// placar quando ele nasceu. O Esc não se troca (KeyBindings.Reserved).
		("menu", K(Key.Escape)),
		("scoreboard", K(Key.Tab)),
		// G solta a arma da mão (decisão do Jhon; era o B do CS).
		("drop", K(Key.G)),
		("attack", M(MouseButton.Left)),
		("special", M(MouseButton.Right)),
		// Q: o especial do lobo, num toque (decisão do Jhon, 18/11; era
		// segurar o botão direito no chão para carregar).
		("pounce", K(Key.Q)),
		// O guia das habilidades: aparece enquanto se segura (18/11, "igual Overwatch").
		("guide", K(Key.E)),
		// Z: a lista dos calls de rádio do time (Radio, Hud.Radio).
		("radio", K(Key.Z)),
		// V: a coronhada (Weapon.Butt).
		("melee", K(Key.V)),
		// F, morto: solta a câmera de espectador (KillCam).
		// F, vivo, não faz mais nada (Jhon, 24/11: "tire o F"): olhava a arma
		// na mão. O começo do giro da faca virou a estocada, no botão direito.
		("spectate_free", K(Key.F)),
		// Slots: o lobo é o 3, na mesma memória muscular das armas.
		("slot_1", K(Key.Key1)),
		("slot_2", K(Key.Key2)),
		("slot_3", K(Key.Key3)),
		("slot_4", K(Key.Key4)),
		("slot_5", K(Key.Key5)),
	};

	public static void EnsureActions()
	{
		foreach (var (action, code) in Defaults)
		{
			if (InputMap.HasAction(action)) continue;
			InputMap.AddAction(action);
			if (KeyBindings.Decode(code) is InputEvent input) InputMap.ActionAddEvent(action, input);
		}
		// As teclas que o jogador trocou (Opções → Controles) por cima das de fábrica.
		KeyBindings.Apply();
	}

	/// <summary>A tecla de fábrica da ação, no texto curto (nulo se a ação não é daqui).</summary>
	public static string DefaultOf(string action)
	{
		foreach (var (name, code) in Defaults)
			if (name == action) return code;
		return null;
	}

	/// <summary>As ações registradas aqui, para os testes.</summary>
	public static IEnumerable<string> Actions
	{
		get { foreach (var (action, _) in Defaults) yield return action; }
	}

	private static string K(Key key) => $"K:{(long)key}";
	private static string M(MouseButton button) => $"M:{(long)button}";
}
