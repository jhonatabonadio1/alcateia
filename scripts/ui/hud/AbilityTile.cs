using Godot;

/// <summary>
/// O ícone de uma habilidade do lobo, no estilo dos de Overwatch: um
/// quadrado com o desenho, a tecla num cantinho e o nome embaixo. Pronta,
/// ele fica aceso com a borda laranja. Em recarga, escurece, uma varredura
/// em círculo libera o ícone no sentido horário e o número grande no meio
/// conta os segundos. Ao voltar, pisca e dá um pulinho.
///
/// Quem manda o estado é o Hud, uma vez por quadro: <see cref="SetState"/>.
/// </summary>
public partial class AbilityTile : Control
{
	private const float Square = 72f;
	private const float CaptionHeight = 18f;

	public string Title { get; set; } = "";
	public string Key { get; set; } = "";
	public IconKind Kind { get; set; } = IconKind.Claw;

	private Icon _icon;
	private Control _cover;
	private float _left;
	private float _total = 1f;
	private float _flash;
	private bool _cooling;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(Square, Square + CaptionHeight);
		// O fundo é vidro (o mapa borrado), como os painéis do HUD.
		Material = HudStyle.Glass();

		_icon = Icon.Make(Kind, 38, UiTokens.TextPrimary);
		_icon.Position = new Vector2((Square - 38) / 2f, (Square - 38) / 2f - 2);
		_icon.Size = new Vector2(38, 38);
		AddChild(_icon);

		// Por cima do ícone: varredura, número, borda, tecla e o brilho.
		_cover = new Control
		{
			MouseFilter = MouseFilterEnum.Ignore,
			ClipContents = true,
			Position = Vector2.Zero,
			Size = new Vector2(Square, Square)
		};
		_cover.Draw += DrawCover;
		AddChild(_cover);
	}

	public override void _Process(double delta)
	{
		if (_flash <= 0) return;
		_flash = Mathf.Max(0, _flash - (float)delta * 3f);
		_cover.QueueRedraw();
	}

	public void SetState(float left, float total)
	{
		if (_cover == null) return;

		bool cooling = left > 0.0001f;
		if (_cooling && !cooling)
		{
			_flash = 1f;
			Motion.Pop(this, 1.15f);
		}

		bool changed = cooling != _cooling || Mathf.Abs(left - _left) > 0.0005f;
		_cooling = cooling;
		_left = left;
		_total = Mathf.Max(total, 0.01f);
		_icon.Modulate = cooling ? new Color(1, 1, 1, 0.4f) : Colors.White;
		if (changed)
		{
			_cover.QueueRedraw();
			QueueRedraw();
		}
	}

	/// <summary>O quadrado com dois cantos cortados (o facetado do HUD).</summary>
	private static Vector2[] Shape(float inset = 0f)
	{
		float c = HudStyle.Cut + 2, a = inset, b = Square - inset;
		return new Vector2[]
		{
			new(a + c, a), new(b, a), new(b, b - c), new(b - c, b), new(a, b), new(a, a + c)
		};
	}

	// Fundo e nome: por baixo do ícone.
	public override void _Draw()
	{
		DrawColoredPolygon(Shape(), HudStyle.GlassMark);

		Color color = _cooling ? UiTokens.TextSecondary : UiTokens.TextPrimary;
		Font font = HudStyle.Slanted(UiFonts.DisplaySemi);
		const int fontSize = 15;
		Vector2 size = font.GetStringSize(Title, HorizontalAlignment.Center, -1, fontSize);
		var at = new Vector2((Square - size.X) / 2f, Square + CaptionHeight - 3);
		DrawString(font, at + new Vector2(1, 1), Title, HorizontalAlignment.Left, -1, fontSize, new Color(0, 0, 0, 0.8f));
		DrawString(font, at, Title, HorizontalAlignment.Left, -1, fontSize, color);
	}

	private void DrawCover()
	{
		Vector2 center = new Vector2(Square, Square) / 2f;

		if (_cooling)
		{
			// O que ainda falta fica escuro; a parte já liberada abre no
			// sentido horário, a partir do topo.
			float remaining = Mathf.Clamp(_left / _total, 0f, 1f);
			if (remaining > 0.005f)
			{
				float start = -Mathf.Pi / 2f + Mathf.Tau * (1f - remaining);
				float end = -Mathf.Pi / 2f + Mathf.Tau;
				int steps = Mathf.Max(3, Mathf.CeilToInt(64f * remaining));
				var points = new Vector2[steps + 2];
				points[0] = center;
				for (int i = 0; i <= steps; i++)
				{
					float angle = Mathf.Lerp(start, end, i / (float)steps);
					points[i + 1] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Square;
				}
				_cover.DrawColoredPolygon(points, new Color(0.02f, 0.03f, 0.05f, 0.72f));
			}

			string number = Mathf.CeilToInt(_left).ToString();
			Font font = HudStyle.Slanted(UiFonts.NumericDisplay);
			const int fontSize = 38;
			Vector2 text = font.GetStringSize(number, HorizontalAlignment.Center, -1, fontSize);
			Vector2 at = new Vector2(center.X - text.X / 2f, center.Y + fontSize * 0.36f);
			_cover.DrawString(font, at + new Vector2(1.5f, 1.5f), number, HorizontalAlignment.Left, -1, fontSize, new Color(0, 0, 0, 0.7f));
			_cover.DrawString(font, at, number, HorizontalAlignment.Left, -1, fontSize, UiTokens.TextPrimary);
		}

		// Borda: laranja pronta, apagada em recarga.
		Color border = _cooling ? UiTokens.Surface3 : UiTokens.TeamOrange;
		Vector2[] edge = Shape(1f);
		var loop = new Vector2[edge.Length + 1];
		edge.CopyTo(loop, 0);
		loop[^1] = edge[0];
		_cover.DrawPolyline(loop, border, 2.5f);

		// Tecla, no canto de baixo.
		Vector2 key = UiFonts.Bold.GetStringSize(Key, HorizontalAlignment.Center, -1, 12);
		var chip = new Rect2(Square - key.X - 10, Square - 17, key.X + 8, 15);
		_cover.DrawRect(chip, new Color(UiTokens.Bg, 0.92f));
		_cover.DrawString(UiFonts.Bold, new Vector2(chip.Position.X + 4, chip.Position.Y + 12), Key,
			HorizontalAlignment.Left, -1, 12, _cooling ? UiTokens.TextSecondary : UiTokens.Accent);

		if (_flash > 0)
			_cover.DrawRect(new Rect2(0, 0, Square, Square), new Color(1, 1, 1, _flash * 0.5f));
	}
}
