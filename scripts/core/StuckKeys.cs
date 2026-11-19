using Godot;

/// <summary>
/// Shift e Ctrl que "ficam apertados" (pedido do Jhon, 18/11: "em alguns
/// momentos eu aperto e solto e ele continua andando lento; isso também
/// não pode acontecer").
///
/// O andar devagar (Shift) e o agachar (Ctrl) seguem a ação do Godot, que
/// só solta quando chega o evento de tecla solta. Tecla modificadora é a
/// que o sistema mais engole: soltar o Shift no meio de um clique, de um
/// atalho do sistema ou com a janela trocando de foco, e o "soltou" não
/// chega — a ação fica apertada e o humano segue andando devagar até
/// apertar e soltar de novo.
///
/// O conserto não depende de saber por que o evento sumiu: todo evento
/// de mouse e de teclado traz junto o estado dos modificadores naquele
/// instante (<see cref="InputEventWithModifiers"/>), e num FPS o mouse
/// manda dezenas por segundo. Se a ação foi apertada pelo teclado e chega
/// um evento dizendo que o Shift (ou o Ctrl) não está apertado, ela é
/// solta aqui. Quem aperta por código (os testes, o controle) não é
/// tocado: só vale para o que o teclado apertou.
/// </summary>
public static class StuckKeys
{
	private static bool _shiftDown, _ctrlDown;

	/// <summary>Quantas vezes soltou uma tecla presa. Para os testes.</summary>
	public static int Freed { get; private set; }

	public static void Watch(InputEvent @event)
	{
		if (@event is InputEventKey key && !key.Echo)
		{
			if (key.PhysicalKeycode == Key.Shift || key.Keycode == Key.Shift) { _shiftDown = key.Pressed; return; }
			if (key.PhysicalKeycode == Key.Ctrl || key.Keycode == Key.Ctrl) { _ctrlDown = key.Pressed; return; }
		}

		if (@event is not InputEventWithModifiers now) return;

		if (_shiftDown && !now.ShiftPressed)
		{
			_shiftDown = false;
			Free("walk");
		}
		if (_ctrlDown && !now.CtrlPressed)
		{
			_ctrlDown = false;
			Free("crouch");
		}
	}

	private static void Free(string action)
	{
		if (!InputMap.HasAction(action) || !Input.IsActionPressed(action)) return;
		Input.ActionRelease(action);
		Freed++;
	}
}
