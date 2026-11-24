using Godot;

/// <summary>
/// O aviso de "isto é da conta": loja, missões e tudo o que vale alguma
/// coisa moram no servidor (decisão 0015), então sem conta a tela mostra
/// este cartão, com o caminho para entrar.
/// </summary>
public static class AccountGate
{
	/// <summary>Há um login guardado, mas a conta ainda não chegou do servidor.</summary>
	public static bool Loading => Backend.LoggedIn && !Profile.HasAccount;

	public static Control Card(LobbyShell shell, string title, string what)
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S5, UiTokens.S3);
		panel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		panel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		panel.CustomMinimumSize = new Vector2(460, 0);

		column.AddChild(UiKit.Text(title, "Subtitle"));
		Label text = UiKit.Paragraph(Loading
			? "Carregando a sua conta… Se o servidor não responder, confira a conexão em Online."
			: what);
		text.CustomMinimumSize = new Vector2(420, 0);
		column.AddChild(text);

		Button enter = UiKit.Button(Loading ? "Abrir Online" : "Entrar na conta", ButtonKind.Primary, () => shell.GoOnline());
		enter.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		column.AddChild(enter);
		return panel;
	}
}
