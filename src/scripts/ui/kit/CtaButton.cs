using Godot;

/// <summary>Os desenhos do botão de ação.</summary>
public enum CtaKind
{
	/// <summary>A placa de papel: a ação que leva adiante (jogar, entrar, iniciar).</summary>
	Paper,
	/// <summary>A placa de ouro: a ação da marca (criar, comprar).</summary>
	Gold,
	/// <summary>Só o contorno: a ação do lado (voltar, cancelar).</summary>
	Ghost
}

/// <summary>
/// O botão de ação dos menus (27/11): uma placa reta com o canto de baixo
/// cortado, o texto em caixa alta com as letras afastadas e, ao passar o
/// mouse, um reflexo que varre a placa uma vez.
///
/// É um <see cref="Button"/> como os outros — foco, teclado, sinal
/// <c>Pressed</c>, som — só que se desenha: o tema não corta canto nem
/// anima. O <c>Text</c> fica como foi escrito (quem procura o botão pelo
/// texto continua achando); a caixa alta é só no desenho.
/// </summary>
public partial class CtaButton : Button
{
	public CtaKind Kind { get; set; } = CtaKind.Paper;

	/// <summary>Tamanho da letra (pensado para uma condensada; a Kanit, mais
	/// larga, sai com 85% dele).</summary>
	public int FontSize
	{
		get => _fontSize;
		set => _fontSize = Mathf.RoundToInt(value * 0.85f);
	}

	private int _fontSize = 17;

	/// <summary>O corte do canto de baixo à direita, em pixels.</summary>
	[Export] public float Cut = 12f;

	/// <summary>Uma seta » depois do texto.</summary>
	public bool Arrow { get; set; }

	/// <summary>A altura, e a largura mínima (o texto pode pedir mais).</summary>
	public float Height { get; set; } = 52f;
	public float MinWidth { get; set; }

	private float _fit = -1f;

	private float _hover, _sheen = 2f;
	private Tween _tween;

	public static CtaButton Make(string text, CtaKind kind = CtaKind.Paper, System.Action pressed = null, int height = 52, int fontSize = 20)
	{
		var button = new CtaButton
		{
			Text = text,
			Kind = kind,
			FontSize = fontSize,
			Height = height,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand
		};
		if (pressed != null) button.Pressed += pressed;
		Sfx.Wire(button);
		return button;
	}

	public override void _Ready()
	{
		// O Godot não desenha nada: nem caixa, nem letra.
		UiKit.Undrawn(this);
		AddThemeFontOverride("font", UiFonts.Display);
		AddThemeFontSizeOverride("font_size", FontSize);

		Fit();
		MouseEntered += () => Hover(true);
		MouseExited += () => Hover(false);
		FocusEntered += QueueRedraw;
		FocusExited += QueueRedraw;
	}

	/// <summary>A largura que o texto pede. O Button mede o texto como foi
	/// escrito, sem margem; o desenho é em caixa alta e com folga dos lados —
	/// então o mínimo se põe aqui, sempre que o texto muda (o Button se
	/// redesenha quando muda).</summary>
	private void Fit()
	{
		float text = UiFonts.Display.GetStringSize(Loc.Upper(Text), HorizontalAlignment.Left, -1, FontSize).X;
		float need = Mathf.Max(MinWidth, Mathf.Ceil(text + UiTokens.S4 * 2 + (Arrow ? FontSize : 0)));
		if (Mathf.IsEqualApprox(need, _fit) && Mathf.IsEqualApprox(CustomMinimumSize.Y, Height)) return;
		_fit = need;
		CustomMinimumSize = new Vector2(need, Height);
	}

	private void Hover(bool on)
	{
		if (Disabled) return;
		_tween?.Kill();
		_tween = CreateTween().SetParallel();
		_tween.TweenMethod(Callable.From<float>(v => { _hover = v; QueueRedraw(); }), _hover, on ? 1f : 0f, on ? 0.14f : 0.22f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		// O reflexo só na entrada: uma passada, da esquerda para a direita.
		if (on)
			_tween.TweenMethod(Callable.From<float>(v => { _sheen = v; QueueRedraw(); }), -0.3f, 1.3f, 0.5f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	public override void _Draw()
	{
		// O texto mudou: a largura acompanha (fora do desenho).
		Callable.From(Fit).CallDeferred();

		Vector2 s = Size;
		if (s.X < 4 || s.Y < 4) return;
		float cut = Mathf.Min(Cut, s.Y * 0.5f);
		bool down = ButtonPressed || IsPressed();

		Color fill, ink, edge;
		switch (Kind)
		{
			case CtaKind.Gold:
				fill = UiTokens.Accent.Lerp(Colors.White, 0.16f * _hover);
				ink = UiTokens.AccentText;
				edge = Colors.Transparent;
				break;
			case CtaKind.Ghost:
				fill = new Color(UiTokens.Paper, 0.04f + 0.1f * _hover);
				ink = UiTokens.TextPrimary;
				edge = new Color(UiTokens.Paper, 0.28f + 0.4f * _hover);
				break;
			default:
				fill = UiTokens.Paper.Lerp(Colors.White, 0.5f * _hover);
				ink = UiTokens.PaperText;
				edge = Colors.Transparent;
				break;
		}
		if (down) fill = fill.Darkened(0.14f);
		if (Disabled)
		{
			fill = new Color(UiTokens.Paper, 0.035f);
			ink = UiTokens.TextDisabled;
			edge = new Color(UiTokens.Paper, 0.12f);
		}

		Vector2[] plate =
		{
			Vector2.Zero, new(s.X, 0), new(s.X, s.Y - cut), new(s.X - cut, s.Y), new(0, s.Y)
		};
		DrawColoredPolygon(plate, fill);

		// O reflexo: uma faixa clara inclinada, cortada pela placa.
		if (!Disabled && _sheen > -0.3f && _sheen < 1.3f)
		{
			float x = _sheen * s.X, w = s.Y * 0.9f, lean = s.Y * 0.5f;
			Vector2[] band = Geometry2D.IntersectPolygons(plate, new Vector2[]
			{
				new(x + lean, 0), new(x + lean + w, 0), new(x + w - lean, s.Y), new(x - lean, s.Y)
			}) is { Count: > 0 } cutBand ? cutBand[0] : null;
			if (band is { Length: >= 3 })
				DrawColoredPolygon(band, new Color(1, 1, 1, Kind == CtaKind.Ghost ? 0.1f : 0.38f));
		}

		if (edge.A > 0f)
		{
			var outline = new Vector2[plate.Length + 1];
			for (int i = 0; i < plate.Length; i++) outline[i] = plate[i] + new Vector2(i is 1 or 2 ? -0.5f : 0.5f, i >= 2 ? -0.5f : 0.5f);
			outline[^1] = outline[0];
			DrawPolyline(outline, edge, 1f, true);
		}

		// O fio da marca na beira esquerda cresce com o mouse (no contorno).
		if (Kind == CtaKind.Ghost && !Disabled && _hover > 0.01f)
			DrawRect(new Rect2(0, 0, 2, s.Y), new Color(UiTokens.Accent, _hover));

		Font font = UiFonts.Display;
		string text = Loc.Upper(Text);
		float textWidth = font.GetStringSize(text, HorizontalAlignment.Left, -1, FontSize).X;
		float total = textWidth + (Arrow ? FontSize * 0.9f : 0f);
		float baseline = (s.Y + font.GetAscent(FontSize) - font.GetDescent(FontSize)) / 2f;
		float left = (s.X - total) / 2f + (Arrow ? -2f * _hover : 0f);
		DrawString(font, new Vector2(left, baseline), text, HorizontalAlignment.Left, -1, FontSize, ink);

		if (Arrow)
		{
			// Duas divisas, que avançam com o mouse.
			float ax = left + textWidth + FontSize * 0.35f + 4f * _hover, ay = s.Y / 2f, h = FontSize * 0.26f;
			for (int i = 0; i < 2; i++)
			{
				float x0 = ax + i * h * 1.1f;
				DrawPolyline(new[] { new Vector2(x0, ay - h), new Vector2(x0 + h, ay), new Vector2(x0, ay + h) }, ink, 2f, true);
			}
		}

		if (HasFocus() && !Disabled)
			DrawRect(new Rect2(-3, -3, s.X + 6, s.Y + 6), new Color(UiTokens.Accent, 0.9f), false, 2f);
	}
}
