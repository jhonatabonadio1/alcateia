using Godot;
using System;

/// <summary>
/// O botão de uma tecla nas Opções → Controles: mostra a tecla da ação; clicado,
/// espera a próxima tecla ou botão do mouse e a entrega a <see cref="Captured"/>.
/// O Esc cancela. Enquanto espera, a tecla não chega a mais ninguém (nem ao
/// menu do Esc, nem ao jogador): a captura lê no <c>_Input</c>, que vem antes da
/// interface e do jogo, e marca o evento como tratado.
///
/// Uma captura por vez: começar outra cancela a que estava esperando.
/// </summary>
public partial class KeyCapture : Button
{
	/// <summary>A captura esperando agora (nula se nenhuma). Para os testes.</summary>
	public static KeyCapture Listening { get; private set; }

	public string Action { get; }

	/// <summary>A tecla nova (só tecla física ou botão do mouse, nunca o Esc).</summary>
	public Action<InputEvent> Captured;

	/// <summary>O quadro em que a captura começou: o clique que a abriu não conta.</summary>
	private ulong _since;

	public KeyCapture() : this("") { }

	public KeyCapture(string action)
	{
		Action = action;
		FocusMode = FocusModeEnum.All;
		MouseDefaultCursorShape = CursorShape.PointingHand;
		ClipText = true;
		Pressed += Begin;
		Sfx.Wire(this);
		Refresh();
	}

	public bool IsListening => Listening == this;

	/// <summary>Mostra a tecla da ação (a trocada ou a de fábrica).</summary>
	public void Refresh()
	{
		if (IsListening) return;
		Text = KeyBindings.Label(Action);
		RemoveThemeColorOverride("font_color");
		RemoveThemeColorOverride("font_hover_color");
	}

	/// <summary>Mostra uma tecla que ainda espera confirmação (o conflito).</summary>
	public void Pending(InputEvent input)
	{
		Text = Loc.F("{0} ?", KeyBindings.Name(input));
		AddThemeColorOverride("font_color", UiTokens.Warning);
		AddThemeColorOverride("font_hover_color", UiTokens.Warning);
	}

	public void Begin()
	{
		if (Listening != null && Listening != this) Listening.Cancel();
		Listening = this;
		_since = Engine.GetProcessFrames();
		Text = Loc.T("Aperte uma tecla…");
		AddThemeColorOverride("font_color", UiTokens.Accent);
		AddThemeColorOverride("font_hover_color", UiTokens.Accent);
	}

	public void Cancel()
	{
		if (Listening == this) Listening = null;
		Refresh();
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsListening) return;

		InputEvent chosen = null;
		switch (@event)
		{
			case InputEventKey { Pressed: true, Echo: false } key:
				Key physical = key.PhysicalKeycode != Godot.Key.None ? key.PhysicalKeycode : key.Keycode;
				if (physical == Godot.Key.Escape)
				{
					GetViewport().SetInputAsHandled();
					Cancel();
					return;
				}
				chosen = new InputEventKey { PhysicalKeycode = physical };
				break;
			case InputEventMouseButton { Pressed: true } mouse:
				// O clique que abriu a captura chega no mesmo quadro (o botão age ao soltar).
				if (Engine.GetProcessFrames() <= _since) return;
				chosen = new InputEventMouseButton { ButtonIndex = mouse.ButtonIndex };
				break;
			case InputEventKey or InputEventMouseButton:
				// Soltar a tecla ou o botão: não é escolha, mas também não passa adiante.
				GetViewport().SetInputAsHandled();
				return;
			default:
				return;
		}

		GetViewport().SetInputAsHandled();
		Listening = null;
		Refresh();
		Captured?.Invoke(chosen);
	}

	public override void _ExitTree()
	{
		if (Listening == this) Listening = null;
	}
}
