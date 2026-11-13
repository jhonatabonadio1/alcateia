using Godot;

/// <summary>
/// O Theme do projeto, montado por código a partir de <see cref="UiTokens"/>.
///
/// Por código e não num .tres: assim os tokens são a única fonte, e mudar
/// uma cor ou um tamanho em UiTokens muda o jogo inteiro sem editar
/// recurso nenhum.
///
/// Toda tela aplica o tema no próprio nó raiz com <see cref="Apply"/>.
/// Controle dentro de CanvasLayer não herda tema da janela — o
/// CanvasLayer quebra a corrente —, então quem vive numa camada (HUD,
/// menus da partida) aplica no Control de cima dela.
///
/// Variações disponíveis (ThemeTypeVariation):
/// botões — PrimaryButton, (Button = secundário), TertiaryButton,
/// DangerButton, TabButton, IconButton;
/// rótulos — Display, Title, Subtitle, (Label = corpo), Caption,
/// Numeric, NumericLarge, Muted;
/// painéis — Surface1, Surface2, Surface3, Card, HudPanel, Modal, Header.
/// </summary>
public static class UiTheme
{
	private static Theme _theme;

	public static Theme Get() => _theme ??= Build();

	/// <summary>Aplica o tema e devolve o próprio controle, para encadear.</summary>
	public static T Apply<T>(T control) where T : Control
	{
		control.Theme = Get();
		return control;
	}

	// ------------------------------------------------------------------

	public static StyleBoxFlat Box(Color bg, Color border = default, int borderWidth = 0,
		int radius = UiTokens.RadiusSmall, int padH = UiTokens.S3, int padV = UiTokens.S2)
	{
		var box = new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = border,
			ContentMarginLeft = padH,
			ContentMarginRight = padH,
			ContentMarginTop = padV,
			ContentMarginBottom = padV,
			AntiAliasing = false
		};
		box.SetBorderWidthAll(borderWidth);
		box.SetCornerRadiusAll(radius);
		return box;
	}

	private static StyleBoxFlat Focus()
	{
		// Foco: contorno de destaque, sem fundo. Sem estado de foco o
		// jogo não funciona com controle nem com Tab no teclado.
		var box = Box(Colors.Transparent, UiTokens.Accent, 2, UiTokens.RadiusSmall, 0, 0);
		box.DrawCenter = false;
		box.ExpandMarginLeft = box.ExpandMarginRight = 2;
		box.ExpandMarginTop = box.ExpandMarginBottom = 2;
		return box;
	}

	private static Theme Build()
	{
		var t = new Theme
		{
			DefaultFont = UiFonts.Regular,
			DefaultFontSize = UiTokens.Body
		};

		Buttons(t);
		Labels(t);
		Panels(t);
		Inputs(t);
		Lists(t);

		return t;
	}

	// ------------------------------------------------------------------ botões

	private static void ButtonStyle(Theme t, string type, Color bg, Color hover, Color pressed,
		Color text, Color border, int borderWidth, Font font, int fontSize = UiTokens.Body)
	{
		t.SetStylebox("normal", type, Box(bg, border, borderWidth));
		t.SetStylebox("hover", type, Box(hover, border.A > 0 ? border.Lightened(0.2f) : border, borderWidth));
		t.SetStylebox("pressed", type, Box(pressed, border, borderWidth));
		t.SetStylebox("hover_pressed", type, Box(pressed, border, borderWidth));
		// Desabilitado sempre tem contorno: sem ele, um primário
		// desabilitado some no painel e o jogador não sabe que o botão
		// existe.
		t.SetStylebox("disabled", type, Box(UiTokens.Surface2, UiTokens.Border, 1));
		t.SetStylebox("focus", type, Focus());

		t.SetColor("font_color", type, text);
		t.SetColor("font_hover_color", type, text);
		t.SetColor("font_pressed_color", type, text);
		t.SetColor("font_hover_pressed_color", type, text);
		t.SetColor("font_focus_color", type, text);
		t.SetColor("font_disabled_color", type, UiTokens.TextDisabled);
		t.SetColor("icon_normal_color", type, text);
		t.SetColor("icon_hover_color", type, text);
		t.SetColor("icon_pressed_color", type, text);
		t.SetColor("icon_disabled_color", type, UiTokens.TextDisabled);

		t.SetFont("font", type, font);
		t.SetFontSize("font_size", type, fontSize);
		t.SetConstant("h_separation", type, UiTokens.S2);
	}

	private static void Buttons(Theme t)
	{
		// Secundário é o padrão: contorno, fundo discreto. Primário só
		// existe um por tela.
		ButtonStyle(t, "Button",
			UiTokens.Surface2, UiTokens.Surface3, UiTokens.Surface1,
			UiTokens.TextPrimary, UiTokens.BorderStrong, 1, UiFonts.SemiBold);

		t.SetTypeVariation("PrimaryButton", "Button");
		ButtonStyle(t, "PrimaryButton",
			UiTokens.Accent, UiTokens.Accent.Lightened(0.12f), UiTokens.Accent.Darkened(0.15f),
			UiTokens.AccentText, Colors.Transparent, 0, UiFonts.Bold);

		t.SetTypeVariation("TertiaryButton", "Button");
		ButtonStyle(t, "TertiaryButton",
			Colors.Transparent, new Color(1, 1, 1, 0.05f), new Color(1, 1, 1, 0.08f),
			UiTokens.TextSecondary, Colors.Transparent, 0, UiFonts.Medium);
		t.SetColor("font_hover_color", "TertiaryButton", UiTokens.TextPrimary);
		t.SetStylebox("disabled", "TertiaryButton", Box(Colors.Transparent));

		t.SetTypeVariation("DangerButton", "Button");
		ButtonStyle(t, "DangerButton",
			Colors.Transparent, new Color(UiTokens.Danger, 0.12f), new Color(UiTokens.Danger, 0.2f),
			UiTokens.Danger, UiTokens.Danger, 1, UiFonts.SemiBold);

		// Aba: texto em caixa alta, sublinhado de destaque quando ativa.
		t.SetTypeVariation("TabButton", "Button");
		ButtonStyle(t, "TabButton",
			Colors.Transparent, new Color(1, 1, 1, 0.04f), Colors.Transparent,
			UiTokens.TextSecondary, Colors.Transparent, 0, UiFonts.Label, UiTokens.Caption);
		var active = Box(Colors.Transparent, UiTokens.Accent, 0, 0, UiTokens.S3, UiTokens.S2);
		active.BorderWidthBottom = 2;
		t.SetStylebox("pressed", "TabButton", active);
		t.SetStylebox("hover_pressed", "TabButton", active);
		t.SetColor("font_pressed_color", "TabButton", UiTokens.TextPrimary);
		t.SetColor("font_hover_pressed_color", "TabButton", UiTokens.TextPrimary);
		t.SetColor("font_hover_color", "TabButton", UiTokens.TextPrimary);

		t.SetTypeVariation("IconButton", "Button");
		ButtonStyle(t, "IconButton",
			Colors.Transparent, new Color(1, 1, 1, 0.06f), new Color(1, 1, 1, 0.1f),
			UiTokens.TextSecondary, Colors.Transparent, 0, UiFonts.Medium);
		foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
		{
			var box = (StyleBoxFlat)t.GetStylebox(state, "IconButton");
			box.ContentMarginLeft = box.ContentMarginRight = UiTokens.S2;
			box.ContentMarginTop = box.ContentMarginBottom = UiTokens.S2;
		}
		t.SetColor("font_hover_color", "IconButton", UiTokens.TextPrimary);

		// OptionButton é um Button por baixo, mas tem tipo próprio no tema.
		ButtonStyle(t, "OptionButton",
			UiTokens.Surface1, UiTokens.Surface2, UiTokens.Surface1,
			UiTokens.TextPrimary, UiTokens.BorderStrong, 1, UiFonts.Medium);
		t.SetConstant("arrow_margin", "OptionButton", UiTokens.S2);
	}

	// ------------------------------------------------------------------ texto

	private static void LabelVariant(Theme t, string name, Font font, int size, Color color)
	{
		t.SetTypeVariation(name, "Label");
		t.SetFont("font", name, font);
		t.SetFontSize("font_size", name, size);
		t.SetColor("font_color", name, color);
	}

	private static void Labels(Theme t)
	{
		t.SetFont("font", "Label", UiFonts.Regular);
		t.SetFontSize("font_size", "Label", UiTokens.Body);
		t.SetColor("font_color", "Label", UiTokens.TextPrimary);
		t.SetColor("font_shadow_color", "Label", Colors.Transparent);

		LabelVariant(t, "Display", UiFonts.Display, UiTokens.Display, UiTokens.TextPrimary);
		LabelVariant(t, "Title", UiFonts.Bold, UiTokens.Title, UiTokens.TextPrimary);
		LabelVariant(t, "Subtitle", UiFonts.SemiBold, UiTokens.Subtitle, UiTokens.TextPrimary);
		LabelVariant(t, "Caption", UiFonts.Label, UiTokens.Caption, UiTokens.TextSecondary);
		LabelVariant(t, "Muted", UiFonts.Regular, UiTokens.Body, UiTokens.TextSecondary);
		LabelVariant(t, "Numeric", UiFonts.Numeric, UiTokens.Body, UiTokens.TextPrimary);
		LabelVariant(t, "NumericLarge", UiFonts.NumericDisplay, UiTokens.Title, UiTokens.TextPrimary);

		t.SetFont("normal_font", "RichTextLabel", UiFonts.Regular);
		t.SetFont("bold_font", "RichTextLabel", UiFonts.Bold);
		t.SetFontSize("normal_font_size", "RichTextLabel", UiTokens.Body);
		t.SetFontSize("bold_font_size", "RichTextLabel", UiTokens.Body);
		t.SetColor("default_color", "RichTextLabel", UiTokens.TextPrimary);
	}

	// ------------------------------------------------------------------ painéis

	private static void PanelVariant(Theme t, string name, StyleBox box)
	{
		t.SetTypeVariation(name, "PanelContainer");
		t.SetStylebox("panel", name, box);
	}

	private static void Panels(Theme t)
	{
		t.SetStylebox("panel", "PanelContainer", Box(UiTokens.Surface1, default, 0, UiTokens.RadiusPanel, 0, 0));
		t.SetStylebox("panel", "Panel", Box(UiTokens.Surface1, default, 0, UiTokens.RadiusPanel, 0, 0));

		PanelVariant(t, "Surface1", Box(UiTokens.Surface1, UiTokens.Border, 1, UiTokens.RadiusPanel, UiTokens.S3, UiTokens.S3));
		PanelVariant(t, "Surface2", Box(UiTokens.Surface2, UiTokens.Border, 1, UiTokens.RadiusPanel, UiTokens.S3, UiTokens.S3));
		PanelVariant(t, "Surface3", Box(UiTokens.Surface3, UiTokens.Border, 1, UiTokens.RadiusPanel, UiTokens.S2, UiTokens.S2));
		PanelVariant(t, "Card", Box(UiTokens.Surface2, UiTokens.Border, 1, UiTokens.RadiusPanel, 0, 0));
		PanelVariant(t, "Bare", Box(Colors.Transparent, default, 0, 0, 0, 0));
		PanelVariant(t, "HudPanel", Box(new Color(UiTokens.Bg, 0.72f), new Color(1, 1, 1, 0.06f), 1, UiTokens.RadiusHud, UiTokens.S3, UiTokens.S2));
		PanelVariant(t, "Modal", Box(UiTokens.Surface2, UiTokens.BorderStrong, 1, UiTokens.RadiusPanel, UiTokens.S5, UiTokens.S4));

		var header = Box(UiTokens.Surface1, default, 0, 0, UiTokens.S3, 0);
		header.BorderColor = UiTokens.Border;
		header.BorderWidthBottom = 1;
		PanelVariant(t, "Header", header);

		var sep = new StyleBoxLine { Color = UiTokens.Border, Thickness = 1 };
		t.SetStylebox("separator", "HSeparator", sep);
		t.SetConstant("separation", "HSeparator", UiTokens.S3);
		var vsep = new StyleBoxLine { Color = UiTokens.Border, Thickness = 1, Vertical = true };
		t.SetStylebox("separator", "VSeparator", vsep);
		t.SetConstant("separation", "VSeparator", UiTokens.S3);

		t.SetConstant("separation", "HBoxContainer", UiTokens.S2);
		t.SetConstant("separation", "VBoxContainer", UiTokens.S2);
		t.SetConstant("h_separation", "GridContainer", UiTokens.S3);
		t.SetConstant("v_separation", "GridContainer", UiTokens.S3);

		t.SetStylebox("panel", "TooltipPanel", Box(UiTokens.Surface3, UiTokens.BorderStrong, 1, UiTokens.RadiusSmall, UiTokens.S2, UiTokens.S1));
		t.SetColor("font_color", "TooltipLabel", UiTokens.TextPrimary);
		t.SetFont("font", "TooltipLabel", UiFonts.Regular);
		t.SetFontSize("font_size", "TooltipLabel", UiTokens.Caption);
	}

	// ------------------------------------------------------------------ campos

	private static void Inputs(Theme t)
	{
		t.SetStylebox("normal", "LineEdit", Box(UiTokens.Surface1, UiTokens.BorderStrong, 1, UiTokens.RadiusSmall, UiTokens.S2, UiTokens.S1));
		t.SetStylebox("focus", "LineEdit", Focus());
		t.SetStylebox("read_only", "LineEdit", Box(UiTokens.Surface1, UiTokens.Border, 1, UiTokens.RadiusSmall, UiTokens.S2, UiTokens.S1));
		t.SetColor("font_color", "LineEdit", UiTokens.TextPrimary);
		t.SetColor("font_placeholder_color", "LineEdit", UiTokens.TextDisabled);
		t.SetColor("font_uneditable_color", "LineEdit", UiTokens.TextSecondary);
		t.SetColor("caret_color", "LineEdit", UiTokens.Accent);
		t.SetColor("selection_color", "LineEdit", new Color(UiTokens.Accent, 0.35f));
		t.SetFont("font", "LineEdit", UiFonts.Medium);
		t.SetFontSize("font_size", "LineEdit", UiTokens.Body);

		foreach (string type in new[] { "CheckBox", "CheckButton" })
		{
			t.SetStylebox("normal", type, Box(Colors.Transparent, default, 0, 0, UiTokens.S1, UiTokens.S1));
			t.SetStylebox("hover", type, Box(new Color(1, 1, 1, 0.04f), default, 0, UiTokens.RadiusSmall, UiTokens.S1, UiTokens.S1));
			t.SetStylebox("pressed", type, Box(Colors.Transparent, default, 0, 0, UiTokens.S1, UiTokens.S1));
			t.SetStylebox("hover_pressed", type, Box(new Color(1, 1, 1, 0.04f), default, 0, UiTokens.RadiusSmall, UiTokens.S1, UiTokens.S1));
			t.SetStylebox("disabled", type, Box(Colors.Transparent, default, 0, 0, UiTokens.S1, UiTokens.S1));
			t.SetStylebox("focus", type, Focus());
			t.SetColor("font_color", type, UiTokens.TextPrimary);
			t.SetColor("font_hover_color", type, UiTokens.TextPrimary);
			t.SetColor("font_pressed_color", type, UiTokens.TextPrimary);
			t.SetColor("font_hover_pressed_color", type, UiTokens.TextPrimary);
			t.SetColor("font_disabled_color", type, UiTokens.TextDisabled);
			t.SetFont("font", type, UiFonts.Medium);
			t.SetFontSize("font_size", type, UiTokens.Body);
			t.SetConstant("h_separation", type, UiTokens.S2);
		}

		// As caixas padrão do Godot somem no fundo escuro: desenhadas aqui.
		t.SetIcon("unchecked", "CheckBox", CheckIcon(false, false));
		t.SetIcon("checked", "CheckBox", CheckIcon(true, false));
		t.SetIcon("unchecked_disabled", "CheckBox", CheckIcon(false, true));
		t.SetIcon("checked_disabled", "CheckBox", CheckIcon(true, true));

		t.SetStylebox("slider", "HSlider", Box(UiTokens.Surface3, default, 0, UiTokens.RadiusSmall, 0, 2));
		var area = Box(UiTokens.Accent, default, 0, UiTokens.RadiusSmall, 0, 2);
		t.SetStylebox("grabber_area", "HSlider", area);
		t.SetStylebox("grabber_area_highlight", "HSlider", Box(UiTokens.Accent.Lightened(0.1f), default, 0, UiTokens.RadiusSmall, 0, 2));

		t.SetStylebox("background", "ProgressBar", Box(UiTokens.Surface3, default, 0, 0, 0, 0));
		t.SetStylebox("fill", "ProgressBar", Box(UiTokens.Accent, default, 0, 0, 0, 0));
		t.SetColor("font_color", "ProgressBar", UiTokens.TextPrimary);
	}

	/// <summary>Caixa de marcação 18×18: contorno quando vazia, preenchida
	/// com o destaque e um visto escuro quando marcada.</summary>
	private static Texture2D CheckIcon(bool on, bool disabled)
	{
		const int n = 18;
		Image image = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		Color edge = disabled ? UiTokens.TextDisabled : on ? UiTokens.Accent : UiTokens.TextSecondary;
		Color fill = on ? (disabled ? UiTokens.TextDisabled : UiTokens.Accent) : new Color(UiTokens.Surface1, 1f);

		for (int y = 1; y < n - 1; y++)
		for (int x = 1; x < n - 1; x++)
		{
			bool border = x <= 2 || y <= 2 || x >= n - 3 || y >= n - 3;
			image.SetPixel(x, y, border ? edge : fill);
		}

		if (on)
		{
			// Visto em duas pernas, com 2 px de espessura.
			Color ink = UiTokens.AccentText;
			void Stroke(int x0, int y0, int x1, int y1)
			{
				int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
				for (int i = 0; i <= steps; i++)
				{
					int x = x0 + (x1 - x0) * i / steps;
					int y = y0 + (y1 - y0) * i / steps;
					image.SetPixel(x, y, ink);
					image.SetPixel(x, y + 1, ink);
				}
			}
			Stroke(5, 9, 8, 12);
			Stroke(8, 12, 13, 6);
		}

		return ImageTexture.CreateFromImage(image);
	}

	// ------------------------------------------------------------------ listas

	private static void Lists(Theme t)
	{
		t.SetStylebox("panel", "PopupMenu", Box(UiTokens.Surface2, UiTokens.BorderStrong, 1, UiTokens.RadiusSmall, UiTokens.S1, UiTokens.S1));
		t.SetStylebox("hover", "PopupMenu", Box(UiTokens.Surface3, default, 0, UiTokens.RadiusSmall, UiTokens.S2, UiTokens.S1));
		t.SetColor("font_color", "PopupMenu", UiTokens.TextPrimary);
		t.SetColor("font_hover_color", "PopupMenu", UiTokens.TextPrimary);
		t.SetColor("font_disabled_color", "PopupMenu", UiTokens.TextDisabled);
		t.SetFont("font", "PopupMenu", UiFonts.Medium);
		t.SetFontSize("font_size", "PopupMenu", UiTokens.Body);
		t.SetConstant("v_separation", "PopupMenu", UiTokens.S2);

		t.SetStylebox("panel", "ScrollContainer", Box(Colors.Transparent, default, 0, 0, 0, 0));
		foreach (string type in new[] { "VScrollBar", "HScrollBar" })
		{
			t.SetStylebox("scroll", type, Box(new Color(1, 1, 1, 0.03f), default, 0, UiTokens.RadiusSmall, 3, 3));
			t.SetStylebox("grabber", type, Box(new Color(1, 1, 1, 0.16f), default, 0, UiTokens.RadiusSmall, 3, 3));
			t.SetStylebox("grabber_highlight", type, Box(new Color(1, 1, 1, 0.26f), default, 0, UiTokens.RadiusSmall, 3, 3));
			t.SetStylebox("grabber_pressed", type, Box(UiTokens.Accent, default, 0, UiTokens.RadiusSmall, 3, 3));
		}
	}
}
