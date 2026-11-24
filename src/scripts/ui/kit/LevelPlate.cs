using Godot;

/// <summary>
/// A placa do número e as estrelas do retrato (<see cref="RankBadge"/>),
/// num nó à parte para ficar por cima do brilho da borda
/// (<see cref="FrameShine"/>).
/// </summary>
public partial class LevelPlate : Control
{
	private int _rank = Ranks.None;

	public int Rank
	{
		get => _rank;
		set
		{
			_rank = value < 0 ? Ranks.None : Ranks.Clamp(value);
			Visible = _rank >= 0;
			QueueRedraw();
		}
	}

	/// <summary>O lado do retrato em volta do qual a placa vai; 0, o do próprio
	/// nó (o <see cref="RankBadge"/>). O retrato do HUD (<see cref="HexBadge"/>)
	/// tem a caixa maior que o retrato e diz o lado.</summary>
	public float Side
	{
		get => _side;
		set { _side = value; QueueRedraw(); }
	}

	private float _side;

	public LevelPlate()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Visible = false;
	}

	public override void _Draw()
	{
		float side = _side > 0 ? _side : Mathf.Min(Size.X, Size.Y);
		if (_rank < 0 || side <= 0) return;
		Vector2 c = Size / 2f;
		RankBadge.DrawPlate(this, new Vector2(c.X, c.Y + side * RankBadge.Grow * RankBadge.Window * 0.98f), side * 0.13f, _rank, GetThemeFont("font", "Label"));
	}
}
