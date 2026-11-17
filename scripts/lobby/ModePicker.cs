using Godot;
using System;
using System.Linq;

/// <summary>
/// A escolha do modo de jogo, o primeiro passo de criar uma sala (decisão
/// do Jhon, 17/11): um cartão por modo, em dois grupos — contra jogadores
/// e contra bots —, com o ícone, o nome e uma linha do que é. O atual vem
/// marcado. Escolheu, segue para o resto da sala.
/// </summary>
public static class ModePicker
{
	public static void Ask(LobbyShell shell, string title, PlayModes.Info[] options, PlayMode current, Action<PlayMode> chosen)
	{
		Modal modal = null;
		VBoxContainer body = UiKit.Column(UiTokens.S3);

		void Group(string caption, PlayModes.Info[] modes)
		{
			if (modes.Length == 0) return;
			body.AddChild(UiKit.Caption(caption));
			GridContainer grid = new() { Columns = 2 };
			grid.AddThemeConstantOverride("h_separation", UiTokens.S2);
			grid.AddThemeConstantOverride("v_separation", UiTokens.S2);
			foreach (PlayModes.Info mode in modes)
				grid.AddChild(Card(mode, mode.Mode == current, () =>
				{
					modal?.Close();
					chosen(mode.Mode);
				}));
			body.AddChild(grid);
		}

		Group("Contra jogadores", options.Where(o => !o.VsBots).ToArray());
		Group("Contra bots", options.Where(o => o.VsBots).ToArray());

		modal = shell.Modal(title, body, new Modal.Choice("Cancelar", ButtonKind.Secondary, null));
	}

	private static Button Card(PlayModes.Info mode, bool current, Action pick)
	{
		var card = new Button
		{
			CustomMinimumSize = new Vector2(330, 64),
			FocusMode = Control.FocusModeEnum.All,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		card.AddThemeStyleboxOverride("normal", UiTheme.Box(UiTokens.Surface2, current ? UiTokens.Accent : UiTokens.Border, current ? 2 : 1, UiTokens.RadiusPanel, UiTokens.S2, UiTokens.S2));
		card.AddThemeStyleboxOverride("hover", UiTheme.Box(UiTokens.Surface3, UiTokens.Accent, 1, UiTokens.RadiusPanel, UiTokens.S2, UiTokens.S2));
		card.AddThemeStyleboxOverride("pressed", UiTheme.Box(UiTokens.Surface3, UiTokens.Accent, 2, UiTokens.RadiusPanel, UiTokens.S2, UiTokens.S2));
		card.AddThemeStyleboxOverride("focus", UiTheme.Box(UiTokens.Surface3, UiTokens.Accent, 2, UiTokens.RadiusPanel, UiTokens.S2, UiTokens.S2));

		HBoxContainer row = UiKit.Row(UiTokens.S3);
		row.MouseFilter = Control.MouseFilterEnum.Ignore;
		row.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		row.OffsetLeft = row.OffsetTop = UiTokens.S2;
		row.OffsetRight = row.OffsetBottom = -UiTokens.S2;

		Control icon = Icon.Make(mode.Icon, 26, mode.VsBots ? UiTokens.TextSecondary : UiTokens.Accent);
		icon.MouseFilter = Control.MouseFilterEnum.Ignore;
		icon.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		row.AddChild(icon);

		VBoxContainer text = UiKit.Column(2);
		text.MouseFilter = Control.MouseFilterEnum.Ignore;
		text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		text.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		Label name = UiKit.Text(mode.Name, "Subtitle");
		name.MouseFilter = Control.MouseFilterEnum.Ignore;
		text.AddChild(name);
		Label line = UiKit.Paragraph(mode.Line, "Muted");
		line.MouseFilter = Control.MouseFilterEnum.Ignore;
		line.AddThemeFontSizeOverride("font_size", UiTokens.Caption);
		text.AddChild(line);
		row.AddChild(text);

		card.AddChild(row);
		card.Pressed += pick;
		Sfx.Wire(card);
		return card;
	}
}
