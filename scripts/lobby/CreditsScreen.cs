using Godot;

/// <summary>
/// A tela de créditos: quem fez o jogo e de onde veio cada material de
/// terceiros, com a licença. O conteúdo mora em <see cref="Credits"/>;
/// aqui só se desenha.
/// </summary>
public partial class CreditsScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Options;

	protected override void Build()
	{
		VBoxContainer page = Page();

		HBoxContainer head = Heading(page, "Créditos", "quem fez e de onde veio cada coisa");
		head.AddChild(UiKit.Button("Voltar às opções", ButtonKind.Secondary, Shell.GoOptions));

		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		page.AddChild(scroll);

		VBoxContainer list = UiKit.Column(UiTokens.S3);
		list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(list);

		foreach (Credits.Section section in Credits.All)
		{
			PanelContainer card = UiKit.Card(out VBoxContainer column, UiTokens.S4, UiTokens.S2);
			column.AddChild(UiKit.Text(section.Title, "Subtitle", UiTokens.Accent));
			column.AddChild(UiKit.Divider());

			foreach (Credits.Line line in section.Lines)
			{
				HBoxContainer row = UiKit.Row(UiTokens.S3);

				Label what = UiKit.Paragraph(line.What, null);
				what.CustomMinimumSize = new Vector2(560, 0);
				what.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
				what.AddThemeColorOverride("font_color", UiTokens.TextSecondary);
				row.AddChild(what);

				Label who = UiKit.Paragraph(line.Who, null);
				who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
				row.AddChild(who);

				if (line.License.Length > 0)
					row.AddChild(UiKit.Chip(line.License, line.Required ? UiTokens.Accent : UiTokens.TextSecondary));

				column.AddChild(row);
			}

			list.AddChild(card);
		}

		list.AddChild(UiKit.Text("As licenças completas e a origem de cada arquivo estão na pasta do jogo: " +
			"assets/audio/CREDITOS.md, assets/fonts/OFL.txt e docs/PACOTES.md.", "Muted"));
	}
}
