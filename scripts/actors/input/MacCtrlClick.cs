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
///
/// **18/11: mirar agachado no Ctrl** (Jhon: "agachar com o humano pode usar
/// a mira da arma com o botão direito, hoje não tá podendo"). O evento do
/// Godot é o mesmo nos dois casos — botão direito com Ctrl —, mas os
/// botões que estão de fato apertados no mouse não: no Ctrl-clique é o
/// esquerdo; no clique direito de verdade, o direito
/// (<see cref="DisplayServer.MouseGetButtonState"/>, que no Mac lê o
/// aparelho). Com o direito de verdade apertado, o clique fica como está:
/// mira. Se o sistema não souber dizer (nenhum dos dois), vale a regra
/// antiga: ataque. Agachar no C continua funcionando para tudo.
/// </summary>
public static class MacCtrlClick
{
	/// <summary>Ligado só no Mac. Os testes ligam à mão.</summary>
	public static bool Enabled = OS.GetName() == "macOS";

	private static bool _active;

	/// <summary>Os botões apertados no aparelho agora. Os testes trocam.</summary>
	public static System.Func<MouseButtonMask> Hardware = () => DisplayServer.MouseGetButtonState();

	public static void Translate(InputEvent @event)
	{
		if (!Enabled || @event is not InputEventMouseButton { ButtonIndex: MouseButton.Right } button) return;

		if (button.Pressed && button.CtrlPressed)
		{
			// O botão direito de verdade (e não o esquerdo com Ctrl): é a mira.
			MouseButtonMask held = Hardware();
			if ((held & MouseButtonMask.Right) != 0 && (held & MouseButtonMask.Left) == 0) return;

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
