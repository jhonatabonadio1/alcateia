using Godot;

/// <summary>
/// No macOS, Ctrl + clique esquerdo chega ao jogo como clique direito: é
/// a convenção do Mac (Ctrl-clique = menu de contexto), feita pelo próprio
/// Godot, sem opção para desligar. Com o agachar no Ctrl, agachar e atirar
/// virava o especial — o humano não atirava e o lobo saltava em vez de
/// arranhar.
///
/// Aqui o clique direito com Ctrl vira ataque: solta o "special" que o
/// Godot apertou, aperta o "attack", e solta o ataque quando o botão sobe.
/// Como não dá para separar o Ctrl-clique de um clique direito de verdade
/// com Ctrl apertado, no Mac o especial agachado sai agachando com o C
/// (o Ctrl continua agachando, e atirando).
/// </summary>
public static class MacCtrlClick
{
	/// <summary>Ligado só no Mac. Os testes ligam à mão.</summary>
	public static bool Enabled = OS.GetName() == "macOS";

	private static bool _active;

	public static void Translate(InputEvent @event)
	{
		if (!Enabled || @event is not InputEventMouseButton { ButtonIndex: MouseButton.Right } button) return;

		if (button.Pressed && button.CtrlPressed)
		{
			_active = true;
			Input.ActionRelease("special");
			Input.ActionPress("attack");
		}
		else if (!button.Pressed && _active)
		{
			_active = false;
			Input.ActionRelease("attack");
		}
	}
}
