using Godot;

/// <summary>Ping em três barras e cor — não em número. O jogador quer
/// saber "está bom?", não "são 47 ms".</summary>
public partial class PingBars : Control
{
	private int _ms;

	/// <summary>Latência em ms. Zero ou menos = local (sem rede): três
	/// barras verdes.</summary>
	public int Ms
	{
		get => _ms;
		set
		{
			_ms = value;
			TooltipText = value <= 0 ? "Local" : $"{value} ms";
			QueueRedraw();
		}
	}

	public static PingBars Make(int ms = 0) => new()
	{
		Ms = ms,
		CustomMinimumSize = new Vector2(18, 14),
		SizeFlagsVertical = SizeFlags.ShrinkCenter,
		MouseFilter = MouseFilterEnum.Pass
	};

	public override void _Draw()
	{
		int bars = _ms <= 80 ? 3 : _ms <= 150 ? 2 : 1;
		Color on = bars == 3 ? UiTokens.Success : bars == 2 ? UiTokens.Warning : UiTokens.Danger;

		float w = Size.X / 4f;
		for (int i = 0; i < 3; i++)
		{
			float h = Size.Y * (0.4f + 0.3f * i);
			var rect = new Rect2(i * w * 1.4f, Size.Y - h, w, h);
			DrawRect(rect, i < bars ? on : UiTokens.Surface3);
		}
	}
}
