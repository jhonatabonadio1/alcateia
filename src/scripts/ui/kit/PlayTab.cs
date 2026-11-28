using Godot;

/// <summary>
/// A aba JOGAR, no meio do header (Jhon, 27/11: "ao centro do header terá
/// um jogar"): uma placa de papel em trapézio, mais larga em cima, que
/// desce um pouco além da linha do header. É a única peça clara da moldura
/// — o olho cai nela.
///
/// Com o mouse, clareia; de tempos em tempos um reflexo atravessa a placa
/// sozinho (o convite). Numa sala, mostra embaixo o número dela: clicar
/// volta para lá.
/// </summary>
public partial class PlayTab : Button
{
	/// <summary>Quanto cada lado entra, de cima para baixo.</summary>
	[Export] public float Slant = 30f;

	/// <summary>Quanto a placa passa da linha de baixo do header.</summary>
	[Export] public float Drop = 7f;

	/// <summary>De quanto em quanto tempo o reflexo passa sozinho, em segundos.</summary>
	[Export] public float SheenEvery = 6f;

	private bool _active;
	private int _room;
	private float _hover;
	private double _time;
	private Tween _tween;

	public bool Active
	{
		get => _active;
		set { _active = value; QueueRedraw(); }
	}

	/// <summary>O número da sala em que o jogador está (0: nenhuma).</summary>
	public int Room
	{
		get => _room;
		set { _room = value; QueueRedraw(); }
	}

	public PlayTab()
	{
		Text = "Jogar";
		Flat = true;
		FocusMode = FocusModeEnum.All;
		MouseDefaultCursorShape = CursorShape.PointingHand;
	}

	public override void _Ready()
	{
		UiKit.Undrawn(this);
		MouseEntered += () => Hover(true);
		MouseExited += () => Hover(false);
	}

	private void Hover(bool on)
	{
		_tween?.Kill();
		_tween = CreateTween();
		_tween.TweenMethod(Callable.From<float>(v => { _hover = v; QueueRedraw(); }), _hover, on ? 1f : 0f, 0.16f);
	}

	public override void _Process(double delta)
	{
		if (!IsVisibleInTree()) return;
		_time += delta;
		// Só redesenha enquanto o reflexo passa.
		if (_time % SheenEvery < 0.8) QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 s = Size;
		float h = s.Y + Drop;
		Vector2[] plate = { new(0, 0), new(s.X, 0), new(s.X - Slant, h), new(Slant, h) };
		Color fill = UiTokens.Paper.Lerp(Colors.White, 0.55f * _hover);
		if (IsPressed()) fill = fill.Darkened(0.1f);
		DrawColoredPolygon(plate, fill);

		// O reflexo que passa sozinho.
		float t = (float)(_time % SheenEvery) / 0.8f;
		if (t < 1f)
		{
			float x = Mathf.Lerp(-60f, s.X + 60f, t * t * (3f - 2f * t)), w = 34f, lean = h * 0.45f;
			var band = Geometry2D.IntersectPolygons(plate, new Vector2[]
			{
				new(x + lean, 0), new(x + lean + w, 0), new(x + w - lean, h), new(x - lean, h)
			});
			if (band.Count > 0 && band[0].Length >= 3) DrawColoredPolygon(band[0], new Color(1, 1, 1, 0.55f));
		}

		Font font = UiFonts.Wide;
		string text = Loc.Upper(Text);
		const int size = 28;
		float width = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
		float baseline = (s.Y + font.GetAscent(size) - font.GetDescent(size)) / 2f - (_room > 0 ? 6f : 0f);
		DrawString(font, new Vector2((s.X - width) / 2f, baseline), text, HorizontalAlignment.Left, -1, size, UiTokens.PaperText);

		if (_room > 0)
		{
			Font small = UiFonts.Tag;
			string where = Loc.F("NA SALA #{0}", _room);
			float w = small.GetStringSize(where, HorizontalAlignment.Left, -1, 11).X;
			DrawString(small, new Vector2((s.X - w) / 2f, baseline + 15f), where, HorizontalAlignment.Left, -1, 11, new Color(UiTokens.PaperText, 0.72f));
		}

		// A aba aberta: o fio da marca na base da placa.
		if (_active)
			DrawColoredPolygon(new[]
			{
				new Vector2(Slant - 4f * (Slant / h), h - 4f), new Vector2(s.X - Slant + 4f * (Slant / h), h - 4f),
				new Vector2(s.X - Slant, h), new Vector2(Slant, h)
			}, UiTokens.Accent);

		if (HasFocus()) DrawPolyline(new[] { plate[0], plate[1], plate[2], plate[3], plate[0] }, UiTokens.Accent, 2f, true);
	}
}
