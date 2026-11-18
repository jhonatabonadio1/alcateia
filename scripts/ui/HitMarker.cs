using Godot;

/// <summary>
/// Marcador de acerto: quatro traços diagonais em volta da mira que
/// nascem grandes e encolhem. Tiro na cabeça sai vermelho e maior; o
/// abate segura um pouco mais. Junto com o som (acerto, acerto_cabeca,
/// abate), é o que diz "pegou" sem o jogador tirar o olho do alvo.
///
/// No abate, uma **caveira** pulsa no centro da mira (pedido do Jhon,
/// 18/11): estoura, assenta e some, em menos de meio segundo. O texto
/// "ELIMINOU" fica embaixo, acima do especial (<see cref="KillBanner"/>).
/// </summary>
public partial class HitMarker : Control
{
	[Export] public float Life = 0.22f;
	[Export] public float KillLife = 0.45f;

	private double _left;
	private double _total = 1;
	private Color _color = Colors.White;
	private float _size = 10f;

	/// <summary>Lado da caveira do abate, em pixels.</summary>
	[Export] public int SkullSide = 46;

	/// <summary>Quanto dura o pulso da caveira, do estouro ao sumiço.</summary>
	[Export] public float SkullLife = 0.42f;

	private static readonly Color SkullColor = new("ff4a4f");
	private Control _skull;
	private Tween _skullPulse;

	public HitMarker()
	{
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Ready()
	{
		// A caveira, com uma sombra escura por baixo (vermelho some em
		// parede vermelha), parada no centro e invisível até o abate.
		_skull = new Control { MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
		foreach (var (color, offset) in new[] { (new Color(0, 0, 0, 0.6f), new Vector2(2, 2.5f)), (SkullColor, Vector2.Zero) })
		{
			Icon layer = Icon.Make(IconKind.SkullSolid, SkullSide, color);
			layer.Position = offset;
			layer.Size = new Vector2(SkullSide, SkullSide);
			_skull.AddChild(layer);
		}
		AddChild(_skull);
	}

	/// <summary>O pulso: nasce pequena, estoura maior que o tamanho,
	/// assenta e some.</summary>
	private void PulseSkull()
	{
		if (_skull == null) return;
		var side = new Vector2(SkullSide, SkullSide);
		_skull.Size = side;
		_skull.PivotOffset = side / 2f;
		_skull.Position = (Size - side) / 2f;

		_skullPulse?.Kill();
		_skull.Scale = new Vector2(0.4f, 0.4f);
		_skull.Modulate = Colors.White;
		_skullPulse = _skull.CreateTween();
		_skullPulse.TweenProperty(_skull, "scale", new Vector2(1.3f, 1.3f), SkullLife * 0.22f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		_skullPulse.TweenProperty(_skull, "scale", Vector2.One, SkullLife * 0.2f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
		_skullPulse.TweenInterval(SkullLife * 0.23f);
		_skullPulse.TweenProperty(_skull, "modulate:a", 0f, SkullLife * 0.35f);
	}

	public void Flash(bool headshot, bool kill = false)
	{
		_total = _left = kill ? KillLife : Life;
		_color = headshot || kill ? UiTokens.Danger.Lightened(0.15f) : Colors.White;
		_size = headshot ? 14f : 10f;
		if (kill)
		{
			_size = 16f;
			PulseSkull();
		}
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (_left <= 0) return;
		_left -= delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_left <= 0) return;

		float t = (float)(_left / _total);       // 1 → 0
		float grow = 1f + 0.5f * t;              // começa maior e encolhe
		float inner = 7f * grow;
		float outer = inner + _size * grow * 0.6f;
		Vector2 c = Size / 2f;
		var color = new Color(_color, Mathf.Clamp(t * 1.6f, 0, 1));

		foreach (Vector2 dir in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
		{
			Vector2 d = dir.Normalized();
			DrawLine(c + d * inner, c + d * outer, new Color(0, 0, 0, color.A * 0.5f), 4f);
			DrawLine(c + d * inner, c + d * outer, color, 2f);
		}
	}
}
