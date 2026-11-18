using Godot;

/// <summary>
/// Barra de vida em gomos inclinados, como a de Overwatch: um gomo por
/// <see cref="PerSegment"/> pontos, cada um um paralelogramo. Cheio, o
/// gomo é claro; vazio, um vulto; o último, pela metade. Quando a vida
/// cai, o pedaço perdido fica um instante em vermelho e escorre
/// (<see cref="Trail"/>) — é o que mostra o tamanho do tiro que levou.
/// </summary>
public partial class SegmentBar : Control
{
	/// <summary>Pontos de vida por gomo.</summary>
	public float PerSegment { get; set; } = 25f;

	/// <summary>Cor dos gomos cheios.</summary>
	public Color Fill { get; set; } = UiTokens.TextPrimary;

	/// <summary>Cor do que acabou de ser perdido.</summary>
	public Color Trail { get; set; } = UiTokens.Danger;

	public Color Empty { get; set; } = new(1, 1, 1, 0.13f);

	/// <summary>Inclinação de cada gomo, em fração da altura.</summary>
	public float Lean { get; set; } = 0.45f;

	public float Gap { get; set; } = 3f;

	private float _value = 1f;
	private float _trail = 1f;
	private float _max = 200f;
	private double _trailHold;

	public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

	/// <summary>Vida atual e máxima.</summary>
	public void Set(float value, float max)
	{
		max = Mathf.Max(max, 1f);
		float v = Mathf.Clamp(value / max, 0f, 1f);
		if (v < _value) _trailHold = 0.45;
		else _trail = Mathf.Max(_trail, v);
		if (!Mathf.IsEqualApprox(v, _value) || !Mathf.IsEqualApprox(max, _max)) QueueRedraw();
		_value = v;
		_max = max;
	}

	public override void _Process(double delta)
	{
		if (_trail <= _value) { _trail = _value; return; }
		if (_trailHold > 0) { _trailHold -= delta; return; }
		_trail = Mathf.MoveToward(_trail, _value, (float)delta * 0.9f);
		QueueRedraw();
	}

	public override void _Draw()
	{
		int count = Mathf.Clamp(Mathf.CeilToInt(_max / PerSegment), 1, 24);
		float h = Size.Y;
		float lean = h * Lean;
		float width = (Size.X - lean - Gap * (count - 1)) / count;
		if (width <= 1f) return;

		for (int i = 0; i < count; i++)
		{
			float x = i * (width + Gap);
			Piece(x, width, 1f, Empty);
			float trail = Mathf.Clamp(_trail * count - i, 0f, 1f);
			if (trail > 0) Piece(x, width, trail, Trail);
			float fill = Mathf.Clamp(_value * count - i, 0f, 1f);
			if (fill > 0) Piece(x, width, fill, Fill);
		}

		void Piece(float x, float w, float part, Color color)
		{
			float right = x + w * part;
			Vector2[] points =
			{
				new(x + lean, 0), new(right + lean, 0), new(right, h), new(x, h)
			};
			DrawColoredPolygon(points, color);
		}
	}
}
