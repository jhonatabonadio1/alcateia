using Godot;

/// <summary>
/// Os detalhes de moldura dos menus (27/11, com o Valorant de referência):
/// cantoneiras nos cartões, fios finos, etiquetas chapadas, o losango das
/// abas e o título de tela com as letras afastadas. São eles que fazem um
/// painel escuro parecer uma peça de equipamento e não uma caixa.
///
/// Tudo desenhado por código, na cor que a tela pedir.
/// </summary>
public static class Deco
{
	/// <summary>
	/// Cantoneiras num painel: um "L" curto no canto de cima à esquerda e
	/// outro no de baixo à direita, desenhados por cima do próprio painel
	/// (pelo sinal Draw: não entra filho, o layout não muda).
	/// </summary>
	public static T Ticks<T>(T panel, Color? color = null, float arm = 10f) where T : Control
	{
		Color c = color ?? new Color(UiTokens.Paper, 0.42f);
		panel.Draw += () =>
		{
			Vector2 s = panel.Size;
			if (s.X < arm * 3 || s.Y < arm * 3) return;
			panel.DrawLine(new Vector2(0, 0.5f), new Vector2(arm, 0.5f), c, 1f);
			panel.DrawLine(new Vector2(0.5f, 0), new Vector2(0.5f, arm), c, 1f);
			panel.DrawLine(new Vector2(s.X - arm, s.Y - 0.5f), new Vector2(s.X, s.Y - 0.5f), c, 1f);
			panel.DrawLine(new Vector2(s.X - 0.5f, s.Y - arm), new Vector2(s.X - 0.5f, s.Y), c, 1f);
		};
		return panel;
	}

	/// <summary>Um fio de 1 px que some para a direita: separa sem fechar.</summary>
	public static Control Hairline(Color? color = null, int height = 1)
	{
		Color c = color ?? new Color(UiTokens.Paper, 0.22f);
		var gradient = new Gradient();
		gradient.SetColor(0, c);
		gradient.SetColor(1, new Color(c, 0f));
		return new TextureRect
		{
			Texture = new GradientTexture2D { Gradient = gradient, Width = 64, Height = 1 },
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			CustomMinimumSize = new Vector2(0, height),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
	}

	/// <summary>Etiqueta chapada, reta, em caixa alta: "NOVO", "EVENTO", "AO VIVO".</summary>
	public static PanelContainer Tag(string text, Color? bg = null, Color? ink = null)
	{
		var tag = new PanelContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		var box = new StyleBoxFlat { BgColor = bg ?? UiTokens.Paper };
		box.ContentMarginLeft = box.ContentMarginRight = UiTokens.S2;
		box.ContentMarginTop = 1;
		box.ContentMarginBottom = 2;
		tag.AddThemeStyleboxOverride("panel", box);
		Label label = UiKit.Text(Loc.Upper(text), "Caption", ink ?? UiTokens.PaperText);
		label.AddThemeFontOverride("font", UiFonts.Tag);
		label.AddThemeFontSizeOverride("font_size", 12);
		tag.AddChild(label);
		return tag;
	}

	/// <summary>Rótulo pequeno em caixa alta com as letras afastadas, com
	/// duas barras na frente: "// MISSÕES DO DIA".</summary>
	public static Label Kicker(string text, Color? color = null)
	{
		Label label = UiKit.Text("// " + Loc.Upper(text), "Caption", color ?? UiTokens.Accent);
		label.AddThemeFontOverride("font", UiFonts.Tag);
		label.AddThemeFontSizeOverride("font_size", 12);
		return label;
	}

	/// <summary>
	/// O título de uma tela: o rótulo pequeno em cima (onde estou) e o nome
	/// grande, com as letras afastadas.
	/// </summary>
	public static VBoxContainer Title(string title, string kicker = null, int size = 44)
	{
		VBoxContainer box = UiKit.Column(0);
		if (!string.IsNullOrEmpty(kicker)) box.AddChild(Kicker(kicker));
		box.AddChild(Big(title, size));
		return box;
	}

	/// <summary>Os tamanhos de <see cref="Big"/> foram pensados para uma letra
	/// condensada; a Kanit, que ficou, é mais larga: a mesma largura sai com
	/// três quartos da altura.</summary>
	private const float BigScale = 0.74f;

	/// <summary>Texto grande de título (a Kanit itálica pesada), em caixa alta.</summary>
	public static Label Big(string text, int size = 44, Color? color = null)
	{
		Label label = UiKit.Text(Loc.Upper(text), "Title", color ?? UiTokens.TextPrimary);
		label.AddThemeFontOverride("font", UiFonts.Wide);
		label.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(size * BigScale));
		return label;
	}

	/// <summary>Um losango pequeno (o marcador das abas, dos itens de lista).</summary>
	public static Control Diamond(float size, Color color)
	{
		var mark = new Control
		{
			CustomMinimumSize = new Vector2(size, size),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		mark.Draw += () =>
		{
			Vector2 c = mark.Size / 2f;
			float r = Mathf.Min(mark.Size.X, mark.Size.Y) / 2f;
			mark.DrawColoredPolygon(new[] { c + new Vector2(0, -r), c + new Vector2(r, 0), c + new Vector2(0, r), c + new Vector2(-r, 0) }, color);
		};
		return mark;
	}

	/// <summary>
	/// Uma imagem que cobre o espaço, com um degradê escuro por cima vindo
	/// de um lado (para o texto que vai em cima dela) e um zoom lento, que
	/// vai e volta: a foto de um mapa num banner, num cartão de modo.
	/// </summary>
	public static Control Photo(string path, float shadeFrom = 0.92f, bool fromLeft = false, float drift = 0.05f)
	{
		var frame = new Control { ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore };
		var image = new TextureRect
		{
			Texture = !string.IsNullOrEmpty(path) && ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Name = "Imagem"
		};
		UiKit.Fill(image);
		frame.AddChild(image);

		if (drift > 0f)
		{
			// O zoom lento: a foto parada parece papel de parede; andando, parece um lugar.
			frame.Resized += () => image.PivotOffset = frame.Size / 2f;
			Tween tween = image.CreateTween().SetLoops();
			tween.TweenProperty(image, "scale", Vector2.One * (1f + drift), 9f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			tween.TweenProperty(image, "scale", Vector2.One, 9f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		}

		if (shadeFrom > 0f)
		{
			var gradient = new Gradient();
			gradient.SetColor(0, new Color(UiTokens.Bg, shadeFrom));
			gradient.SetColor(1, new Color(UiTokens.Bg, 0f));
			gradient.SetOffset(1, 0.72f);
			var shade = new TextureRect
			{
				Texture = new GradientTexture2D
				{
					Gradient = gradient,
					FillFrom = fromLeft ? new Vector2(0, 0.5f) : new Vector2(0, 1),
					FillTo = fromLeft ? new Vector2(1, 0.5f) : new Vector2(0, 0)
				},
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			UiKit.Fill(shade);
			frame.AddChild(shade);
		}
		return frame;
	}
}
