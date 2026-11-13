using Godot;

public enum IconKind
{
	None,
	Rooms, Shop, Inventory, Profile, Missions, Options, Collapse, Expand,
	Lock, Crown, Wolf, Human, Refresh, Go, Back, Close, Check, Chevrons,
	Presa, Sangue, Star, Clock, Skull, Knife, Grenade, Claw, Fall, Headshot,
	Plus, Minus, Trophy, Chat, Play, Users, Map, Bot, Info, Warning, Gun, Key, Power
}

/// <summary>
/// Ícones vetoriais desenhados por código, numa grade de 24×24 que
/// escala para o tamanho do controle.
///
/// Desenhados aqui, e não importados de um pacote, por dois motivos: o
/// projeto não pode usar ícone de outro jogo, e traço fino de cor única
/// combina com o canto duro do resto da interface. Ícone novo entra no
/// enum e ganha um <c>case</c> em <see cref="DrawKind"/>.
/// </summary>
public partial class Icon : Control
{
	private IconKind _kind;
	private Color _tint = UiTokens.TextPrimary;

	public IconKind Kind
	{
		get => _kind;
		set { _kind = value; QueueRedraw(); }
	}

	public Color Tint
	{
		get => _tint;
		set { _tint = value; QueueRedraw(); }
	}

	private float _scale;
	private Vector2 _origin;
	private float _stroke;

	public Icon()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(20, 20);
	}

	public static Icon Make(IconKind kind, int size = 20, Color? tint = null) => new()
	{
		Kind = kind,
		CustomMinimumSize = new Vector2(size, size),
		Tint = tint ?? UiTokens.TextPrimary,
		SizeFlagsVertical = SizeFlags.ShrinkCenter
	};

	public override void _Draw()
	{
		float side = Mathf.Min(Size.X, Size.Y);
		if (side <= 0 || _kind == IconKind.None) return;

		_scale = side / 24f;
		_origin = (Size - new Vector2(side, side)) / 2f;
		_stroke = Mathf.Max(1.5f, 2f * _scale);
		DrawKind();
	}

	// ------------------------------------------------------------------ traço

	private Vector2 P(float x, float y) => _origin + new Vector2(x, y) * _scale;

	private void L(float x1, float y1, float x2, float y2) =>
		DrawLine(P(x1, y1), P(x2, y2), _tint, _stroke, true);

	private void Poly(bool closed, params float[] xy)
	{
		int count = xy.Length / 2 + (closed ? 1 : 0);
		var points = new Vector2[count];
		for (int i = 0; i < xy.Length / 2; i++) points[i] = P(xy[i * 2], xy[i * 2 + 1]);
		if (closed) points[count - 1] = points[0];
		DrawPolyline(points, _tint, _stroke, true);
	}

	private void Fill(params float[] xy)
	{
		var points = new Vector2[xy.Length / 2];
		for (int i = 0; i < points.Length; i++) points[i] = P(xy[i * 2], xy[i * 2 + 1]);
		DrawColoredPolygon(points, _tint);
	}

	private void Ring(float x, float y, float r) =>
		DrawArc(P(x, y), r * _scale, 0, Mathf.Tau, 32, _tint, _stroke, true);

	private void Arc(float x, float y, float r, float fromDeg, float toDeg) =>
		DrawArc(P(x, y), r * _scale, Mathf.DegToRad(fromDeg), Mathf.DegToRad(toDeg), 24, _tint, _stroke, true);

	private void Dot(float x, float y, float r) => DrawCircle(P(x, y), r * _scale, _tint);

	private void Box(float x, float y, float w, float h) => Poly(true, x, y, x + w, y, x + w, y + h, x, y + h);

	// ------------------------------------------------------------------ ícones

	private void DrawKind()
	{
		switch (_kind)
		{
			case IconKind.Rooms:
				for (int i = 0; i < 3; i++)
				{
					float y = 6 + i * 6;
					Dot(5, y, 1.6f);
					L(9, y, 20, y);
				}
				break;

			case IconKind.Shop:
				Poly(true, 4, 9, 20, 9, 18.5f, 21, 5.5f, 21);
				Arc(12, 9, 4, 180, 360);
				break;

			case IconKind.Inventory:
				Box(4, 7, 16, 13);
				L(4, 12, 20, 12);
				Poly(false, 9, 7, 9, 4, 15, 4, 15, 7);
				L(11, 12, 11, 15); L(13, 12, 13, 15);
				break;

			case IconKind.Profile:
			case IconKind.Human:
				Ring(12, 8, 4);
				Arc(12, 22, 8, 200, 340);
				break;

			case IconKind.Users:
				Ring(9, 9, 3.2f);
				Arc(9, 21, 6.5f, 200, 340);
				Arc(16, 9, 3, -90, 90);
				Arc(17, 20, 5, 250, 340);
				break;

			case IconKind.Missions:
				Poly(false, 6, 21, 6, 3);
				Poly(true, 6, 4, 18, 4, 15, 8, 18, 12, 6, 12);
				break;

			case IconKind.Options:
				Ring(12, 12, 3.2f);
				for (int i = 0; i < 8; i++)
				{
					float a = i * Mathf.Tau / 8f;
					var inner = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6.5f;
					var outer = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 9.5f;
					L(12 + inner.X, 12 + inner.Y, 12 + outer.X, 12 + outer.Y);
				}
				Ring(12, 12, 6.5f);
				break;

			case IconKind.Collapse:
				Poly(false, 12, 6, 6, 12, 12, 18);
				Poly(false, 18, 6, 12, 12, 18, 18);
				break;

			case IconKind.Expand:
			case IconKind.Chevrons:
				Poly(false, 6, 6, 12, 12, 6, 18);
				Poly(false, 12, 6, 18, 12, 12, 18);
				break;

			case IconKind.Lock:
				Box(5, 11, 14, 10);
				Arc(12, 11, 4.5f, 180, 360);
				L(7.5f, 11, 7.5f, 9); L(16.5f, 11, 16.5f, 9);
				Dot(12, 16, 1.4f);
				break;

			case IconKind.Key:
				Ring(8, 12, 4);
				L(12, 12, 21, 12);
				L(18, 12, 18, 16); L(21, 12, 21, 15);
				break;

			case IconKind.Crown:
				Fill(3, 18, 5, 7, 9.5f, 12, 12, 5, 14.5f, 12, 19, 7, 21, 18);
				L(3, 21, 21, 21);
				break;

			case IconKind.Wolf:
				// Cabeça de lobo de frente: orelhas, testa, focinho.
				Poly(true, 4, 3, 9, 8, 15, 8, 20, 3, 20, 12, 15, 19, 12, 21, 9, 19, 4, 12);
				Dot(9, 12, 1.2f);
				Dot(15, 12, 1.2f);
				Poly(false, 10.5f, 17, 12, 18.5f, 13.5f, 17);
				break;

			case IconKind.Refresh:
				Arc(12, 12, 7.5f, -40, 250);
				Fill(17, 3, 20, 8, 14.5f, 8.5f);
				break;

			case IconKind.Go:
				L(4, 12, 19, 12);
				Poly(false, 13, 6, 19, 12, 13, 18);
				break;

			case IconKind.Back:
				L(5, 12, 20, 12);
				Poly(false, 11, 6, 5, 12, 11, 18);
				break;

			case IconKind.Close:
				L(6, 6, 18, 18); L(18, 6, 6, 18);
				break;

			case IconKind.Power:
				// Círculo aberto em cima, com o traço atravessando a abertura.
				DrawArc(P(12, 13), 7.5f * _scale, Mathf.DegToRad(-55), Mathf.DegToRad(235), 24, _tint, _stroke, true);
				L(12, 3.5f, 12, 12);
				break;

			case IconKind.Check:
				Poly(false, 4, 12.5f, 9.5f, 18, 20, 6);
				break;

			case IconKind.Plus:
				L(12, 5, 12, 19); L(5, 12, 19, 12);
				break;

			case IconKind.Minus:
				L(5, 12, 19, 12);
				break;

			case IconKind.Presa:
				// Presa: um dente longo e curvo. É a moeda ganha jogando.
				Fill(7, 4, 17, 4, 15.5f, 11, 13, 20, 12, 21, 11, 20, 8.5f, 11);
				break;

			case IconKind.Sangue:
				Fill(12, 3, 14, 7, 17.5f, 12.5f, 17.5f, 16, 15.5f, 19.5f, 12, 21, 8.5f, 19.5f, 6.5f, 16, 6.5f, 12.5f, 10, 7);
				break;

			case IconKind.Star:
				{
					var pts = new float[20];
					for (int i = 0; i < 10; i++)
					{
						float r = i % 2 == 0 ? 9.5f : 4f;
						float a = -Mathf.Pi / 2 + i * Mathf.Pi / 5;
						pts[i * 2] = 12 + Mathf.Cos(a) * r;
						pts[i * 2 + 1] = 12.5f + Mathf.Sin(a) * r;
					}
					Fill(pts);
					break;
				}

			case IconKind.Clock:
				Ring(12, 12, 8.5f);
				Poly(false, 12, 7, 12, 12, 16, 14);
				break;

			case IconKind.Skull:
				Arc(12, 11, 7.5f, 160, 380);
				Poly(false, 5, 13.5f, 8, 17, 8, 21, 16, 21, 16, 17, 19, 13.5f);
				Dot(9, 12, 1.8f); Dot(15, 12, 1.8f);
				break;

			case IconKind.Knife:
				Fill(3, 19, 15, 7, 21, 3, 17, 9, 5, 21);
				L(14, 14, 10, 10);
				break;

			case IconKind.Grenade:
				Ring(12, 14, 6.5f);
				Box(10, 4, 4, 3.5f);
				Poly(false, 14, 5, 18, 3);
				break;

			case IconKind.Claw:
				for (int i = 0; i < 3; i++)
				{
					float x = 6 + i * 5;
					Poly(false, x, 4, x + 3, 12, x + 1, 20);
				}
				break;

			case IconKind.Fall:
				L(12, 3, 12, 17);
				Poly(false, 6, 12, 12, 18, 18, 12);
				L(4, 21, 20, 21);
				break;

			case IconKind.Headshot:
				Ring(12, 12, 7);
				L(12, 2, 12, 7); L(12, 17, 12, 22); L(2, 12, 7, 12); L(17, 12, 22, 12);
				Dot(12, 12, 1.6f);
				break;

			case IconKind.Gun:
				Fill(2, 9, 19, 9, 19, 8, 22, 8, 22, 12, 11, 12, 9, 18, 5, 18, 6, 12, 2, 12);
				break;

			case IconKind.Trophy:
				Poly(true, 7, 4, 17, 4, 16, 11, 12, 14, 8, 11);
				Arc(6, 7.5f, 2.5f, 90, 270); Arc(18, 7.5f, 2.5f, -90, 90);
				L(12, 14, 12, 18);
				L(8, 20, 16, 20);
				break;

			case IconKind.Chat:
				Poly(true, 4, 5, 20, 5, 20, 16, 11, 16, 7, 20, 7, 16, 4, 16);
				break;

			case IconKind.Play:
				Fill(8, 5, 19, 12, 8, 19);
				break;

			case IconKind.Map:
				Poly(true, 3, 6, 9, 4, 15, 6, 21, 4, 21, 18, 15, 20, 9, 18, 3, 20);
				L(9, 4, 9, 18); L(15, 6, 15, 20);
				break;

			case IconKind.Bot:
				Box(5, 8, 14, 11);
				L(12, 8, 12, 4); Dot(12, 4, 1.5f);
				Dot(9, 13, 1.5f); Dot(15, 13, 1.5f);
				L(9, 16.5f, 15, 16.5f);
				break;

			case IconKind.Info:
				Ring(12, 12, 9);
				Dot(12, 7.5f, 1.4f);
				L(12, 11, 12, 17);
				break;

			case IconKind.Warning:
				Poly(true, 12, 3, 22, 20, 2, 20);
				L(12, 9, 12, 14);
				Dot(12, 17, 1.3f);
				break;
		}
	}
}
