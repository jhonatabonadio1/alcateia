using Godot;

/// <summary>
/// O nível escrito como no Overwatch (Jhon, 21/11: "no header e em locais
/// onde eu mostro a patente, mostre assim"): uma ficha com o número da
/// placa e as estrelas, na cor do metal — o nível 520 é "20 ★★★★★" em
/// bronze; cada estrela é 100 níveis, a cor diz o metal.
/// </summary>
public partial class LevelChip : Control
{
	private int _rank;

	public int Rank
	{
		get => _rank;
		set
		{
			_rank = value < 0 ? Ranks.None : Ranks.Clamp(value);
			TooltipText = _rank < 0 ? "Bot" : $"{Ranks.NameOf(_rank)} · {Ranks.FamilyName(_rank)}";
			UpdateMinimumSize();
			QueueRedraw();
		}
	}

	/// <summary>Altura da ficha; a largura sai do número e das estrelas.</summary>
	public float Height { get; set; } = 20f;

	public static LevelChip Make(int rank, float height = 20f)
	{
		var chip = new LevelChip { Height = height, MouseFilter = MouseFilterEnum.Pass, SizeFlagsVertical = SizeFlags.ShrinkCenter };
		chip.Rank = rank;
		return chip;
	}

	private Font Font => GetThemeFont("font", "Label");
	private int FontSize => Mathf.RoundToInt(Height * 0.72f);
	private float StarR => Height * 0.27f;

	private float NumberWidth => Font.GetStringSize(Ranks.Shown(_rank).ToString(), HorizontalAlignment.Left, -1, FontSize).X;

	public override Vector2 _GetMinimumSize()
	{
		if (_rank < 0) return new Vector2(0, Height);
		int stars = Ranks.Stars(_rank);
		float pad = Height * 0.35f;
		float numberBox = NumberWidth + pad * 2f;
		float starsBox = stars == 0 ? 0 : stars * StarR * 2.1f + pad * 1.4f;
		return new Vector2(numberBox + starsBox, Height);
	}

	public override void _Draw()
	{
		if (_rank < 0) return;
		Color metal = Ranks.ColorOf(_rank);
		float pad = Height * 0.35f;
		float numberBox = NumberWidth + pad * 2f;
		Vector2 size = _GetMinimumSize();

		// fundo escuro, número numa caixa na cor do metal, estrelas ao lado
		var bg = new StyleBoxFlat { BgColor = new Color(0.04f, 0.07f, 0.1f, 0.85f), BorderColor = metal.Darkened(0.25f) };
		bg.SetBorderWidthAll(1);
		bg.SetCornerRadiusAll(Mathf.RoundToInt(Height * 0.2f));
		DrawStyleBox(bg, new Rect2(Vector2.Zero, size));

		var num = new StyleBoxFlat { BgColor = metal.Darkened(0.55f), BorderColor = metal };
		num.SetBorderWidthAll(1);
		num.SetCornerRadiusAll(Mathf.RoundToInt(Height * 0.2f));
		DrawStyleBox(num, new Rect2(Vector2.Zero, new Vector2(numberBox, Height)));
		DrawString(Font, new Vector2(0, Height / 2f + Font.GetAscent(FontSize) * 0.36f), Ranks.Shown(_rank).ToString(),
			HorizontalAlignment.Center, numberBox, FontSize, Colors.White);

		int stars = Ranks.Stars(_rank);
		float r = StarR;
		for (int i = 0; i < stars; i++)
		{
			Vector2 c = new(numberBox + pad * 0.7f + r + i * r * 2.1f, Height / 2f);
			DrawColoredPolygon(RankBadge.Star(c, r), metal.Lightened(0.2f));
		}
	}
}
