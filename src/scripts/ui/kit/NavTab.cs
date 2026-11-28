using Godot;

/// <summary>
/// Uma aba de texto do header (27/11): caixa alta com as letras afastadas,
/// apagada em repouso e clara com o mouse; a que está aberta leva um
/// losango embaixo. Pode levar um número (missões prontas para resgatar).
///
/// Desenhada aqui para o losango e a cor andarem (o tema só troca de
/// estilo de uma vez).
/// </summary>
public partial class NavTab : Button
{
	private bool _active;
	private int _badge;
	private float _glow, _mark;
	private Tween _tween;

	private const int FontSize = 14;

	public bool Active
	{
		get => _active;
		set
		{
			if (_active == value) return;
			_active = value;
			Animate();
		}
	}

	/// <summary>O número ao lado do nome; zero não aparece.</summary>
	public int Badge
	{
		get => _badge;
		set { _badge = value; QueueRedraw(); }
	}

	public static NavTab Make(string text, System.Action pressed)
	{
		var tab = new NavTab
		{
			Text = text,
			Flat = true,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand
		};
		float width = UiFonts.Tag.GetStringSize(Loc.Upper(text), HorizontalAlignment.Left, -1, FontSize).X;
		tab.CustomMinimumSize = new Vector2(width + UiTokens.S5 * 2, UiTokens.HeaderHeight);
		if (pressed != null) tab.Pressed += pressed;
		Sfx.Wire(tab);
		return tab;
	}

	public override void _Ready()
	{
		UiKit.Undrawn(this);
		MouseEntered += Animate;
		MouseExited += Animate;
		FocusEntered += QueueRedraw;
		FocusExited += QueueRedraw;
	}

	private void Animate()
	{
		if (!IsInsideTree()) { _mark = _active ? 1f : 0f; _glow = _mark; return; }
		bool lit = _active || IsHovered();
		_tween?.Kill();
		_tween = CreateTween().SetParallel();
		_tween.TweenMethod(Callable.From<float>(v => { _glow = v; QueueRedraw(); }), _glow, lit ? 1f : 0f, 0.16f);
		_tween.TweenMethod(Callable.From<float>(v => { _mark = v; QueueRedraw(); }), _mark, _active ? 1f : 0f, 0.24f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	public override void _Draw()
	{
		Vector2 s = Size;
		Font font = UiFonts.Tag;
		string text = Loc.Upper(Text);
		float width = font.GetStringSize(text, HorizontalAlignment.Left, -1, FontSize).X;
		Color ink = UiTokens.TextSecondary.Lerp(UiTokens.TextPrimary, _glow);
		float baseline = (s.Y + font.GetAscent(FontSize) - font.GetDescent(FontSize)) / 2f;
		DrawString(font, new Vector2((s.X - width) / 2f, baseline), text, HorizontalAlignment.Left, -1, FontSize, ink);

		// O losango da aba aberta, entre o texto e a beira do header.
		if (_mark > 0.01f)
		{
			float r = 4.5f * _mark;
			Vector2 c = new(s.X / 2f, s.Y - 11f);
			DrawColoredPolygon(new[] { c + new Vector2(0, -r), c + new Vector2(r, 0), c + new Vector2(0, r), c + new Vector2(-r, 0) }, UiTokens.Accent);
		}

		if (_badge > 0)
		{
			string n = _badge.ToString();
			float w = Mathf.Max(16f, font.GetStringSize(n, HorizontalAlignment.Left, -1, 12).X + 8f);
			var box = new Rect2((s.X + width) / 2f + 4f, baseline - FontSize - 3f, w, 16f);
			DrawRect(box, UiTokens.Accent);
			DrawString(font, new Vector2(box.Position.X, box.Position.Y + 12.5f), n, HorizontalAlignment.Center, w, 12, UiTokens.AccentText);
		}

		if (HasFocus()) DrawRect(new Rect2(4, 8, s.X - 8, s.Y - 16), new Color(UiTokens.Accent, 0.8f), false, 2f);
	}
}
