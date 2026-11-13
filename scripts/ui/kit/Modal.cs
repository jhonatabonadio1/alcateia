using Godot;
using System;

/// <summary>
/// Janela modal: escurece o resto e mostra um painel no centro com
/// título, conteúdo e botões.
///
/// Os botões vêm na ordem dada, da esquerda para a direita. Quem chama
/// decide onde fica o primário — a confirmação de compra, por exemplo,
/// põe "Confirmar" num lugar diferente de onde estava "Comprar", para
/// ninguém comprar por inércia de clique.
/// </summary>
public partial class Modal : Control
{
	public readonly record struct Choice(string Text, ButtonKind Kind, Action Action, bool Disabled = false);

	public event Action Closed;

	private PanelContainer _panel;
	private Button _focus;

	public static Modal Open(Node host, string title, Control body, params Choice[] choices)
	{
		var modal = new Modal();
		UiKit.Fill(modal);
		modal.MouseFilter = MouseFilterEnum.Stop;
		modal.ZIndex = 50;

		var dim = UiKit.Fill(new ColorRect { Color = new Color(0, 0, 0, 0.62f) });
		modal.AddChild(dim);

		var center = UiKit.Fill(new CenterContainer());
		modal.AddChild(center);

		modal._panel = UiKit.Panel("Modal");
		modal._panel.CustomMinimumSize = new Vector2(440, 0);
		center.AddChild(modal._panel);

		VBoxContainer column = UiKit.Column(UiTokens.S3);
		modal._panel.AddChild(column);

		column.AddChild(UiKit.Text(title, "Subtitle"));
		if (body != null) column.AddChild(body);

		HBoxContainer buttons = UiKit.Row(UiTokens.S2);
		buttons.Alignment = BoxContainer.AlignmentMode.End;
		column.AddChild(UiKit.Gap(UiTokens.S1));
		column.AddChild(buttons);

		foreach (Choice choice in choices)
		{
			Choice c = choice;
			Button button = UiKit.Button(c.Text, c.Kind, () =>
			{
				c.Action?.Invoke();
				modal.Close();
			});
			button.CustomMinimumSize = new Vector2(120, UiTokens.ButtonHeight);
			button.Disabled = c.Disabled;
			buttons.AddChild(button);

			if (c.Kind == ButtonKind.Primary && !c.Disabled) modal._focus = button;
		}

		host.AddChild(modal);
		Motion.FadeIn(modal, UiTokens.Fast);
		modal._focus?.CallDeferred(Control.MethodName.GrabFocus);
		return modal;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			GetViewport().SetInputAsHandled();
			Close();
		}
	}

	public void Close()
	{
		if (IsQueuedForDeletion()) return;
		Closed?.Invoke();
		QueueFree();
	}
}
