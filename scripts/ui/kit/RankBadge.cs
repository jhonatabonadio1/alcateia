using Godot;

/// <summary>
/// Insígnia de patente em moldura quadrada. Nunca texto: com trinta
/// patentes, a forma e a cor dizem a família, e o número de marcas diz o
/// grau.
///
/// Famílias, na ordem de <see cref="Ranks"/>: divisas, barras, divisa
/// com barra, losangos, escudo, lua crescente, presas cruzadas, lua
/// cheia, estrelas, coroa.
/// </summary>
public partial class RankBadge : Control
{
	private int _rank;

	public int Rank
	{
		get => _rank;
		set
		{
			// Sem patente (bot): o espaço fica, a insígnia não.
			_rank = value < 0 ? Ranks.None : Ranks.Clamp(value);
			TooltipText = _rank < 0 ? "Bot — sem patente" : Ranks.NameOf(_rank);
			QueueRedraw();
		}
	}

	/// <summary>Sem moldura: só a marca, para caber em linha de placar.</summary>
	public bool Bare { get; set; }

	public static RankBadge Make(int rank, int size = 32, bool bare = false)
	{
		var badge = new RankBadge
		{
			CustomMinimumSize = new Vector2(size, size),
			Bare = bare,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Pass
		};
		badge.Rank = rank;
		return badge;
	}

	private float _s;
	private Vector2 _o;
	private Color _c;

	private Vector2 P(float x, float y) => _o + new Vector2(x, y) * _s;

	public override void _Draw()
	{
		float side = Mathf.Min(Size.X, Size.Y);
		if (side <= 0 || _rank < 0) return;

		_s = side / 32f;
		_o = (Size - new Vector2(side, side)) / 2f;
		_c = Ranks.ColorOf(_rank);

		if (!Bare)
		{
			var frame = new Rect2(_o, new Vector2(side, side));
			DrawRect(frame, new Color(UiTokens.Bg, 0.85f));
			DrawRect(frame.Grow(-0.5f), new Color(_c, 0.55f), false, 1f);
		}

		int tier = Ranks.Tier(_rank) + 1;
		float w = Mathf.Max(1.6f, 2.2f * _s);

		switch (Ranks.Family(_rank))
		{
			case 0: // divisas
				for (int i = 0; i < tier; i++)
				{
					float y = 21 - i * 5;
					DrawPolyline(new[] { P(9, y), P(16, y - 5), P(23, y) }, _c, w, true);
				}
				break;

			case 1: // barras
				for (int i = 0; i < tier; i++)
					DrawRect(new Rect2(P(9, 20 - i * 5.5f), new Vector2(14, 3) * _s), _c);
				break;

			case 2: // divisa + barras
				DrawPolyline(new[] { P(8, 13), P(16, 7), P(24, 13) }, _c, w, true);
				for (int i = 0; i < tier; i++)
					DrawRect(new Rect2(P(10, 16 + i * 4.5f), new Vector2(12, 2.5f) * _s), _c);
				break;

			case 3: // losangos
				for (int i = 0; i < tier; i++)
				{
					float x = 16 + (i - (tier - 1) / 2f) * 7;
					DrawColoredPolygon(new[] { P(x, 10), P(x + 3.2f, 16), P(x, 22), P(x - 3.2f, 16) }, _c);
				}
				break;

			case 4: // escudo com barras
				DrawPolyline(new[] { P(8, 7), P(24, 7), P(24, 16), P(16, 25), P(8, 16), P(8, 7) }, _c, w, true);
				for (int i = 0; i < tier; i++)
					DrawRect(new Rect2(P(11.5f, 10 + i * 3.5f), new Vector2(9, 1.8f) * _s), _c);
				break;

			case 5: // lua crescente e pontos
				Crescent(15, 14, 7.5f);
				for (int i = 0; i < tier; i++)
					DrawCircle(P(10 + i * 6, 25), 1.6f * _s, _c);
				break;

			case 6: // presas cruzadas
				DrawColoredPolygon(new[] { P(8, 7), P(12, 7), P(22, 22), P(19, 24) }, _c);
				DrawColoredPolygon(new[] { P(24, 7), P(20, 7), P(10, 22), P(13, 24) }, _c);
				for (int i = 0; i < tier; i++)
					DrawRect(new Rect2(P(12 + i * 3.2f, 26), new Vector2(2, 2) * _s), _c);
				break;

			case 7: // lua cheia
				DrawArc(P(16, 14), 7.5f * _s, 0, Mathf.Tau, 32, _c, w, true);
				DrawCircle(P(16, 14), 4.5f * _s, _c);
				for (int i = 0; i < tier; i++)
					DrawRect(new Rect2(P(11 + i * 4, 25), new Vector2(2.5f, 2) * _s), _c);
				break;

			case 8: // estrelas
				for (int i = 0; i < tier; i++)
					Star(16 + (i - (tier - 1) / 2f) * 8, 16, tier == 1 ? 8 : 4.6f);
				break;

			default: // coroa
				DrawColoredPolygon(new[] { P(7, 22), P(8.5f, 10), P(12.5f, 15), P(16, 8), P(19.5f, 15), P(23.5f, 10), P(25, 22) }, _c);
				for (int i = 0; i < tier; i++)
					DrawCircle(P(16 + (i - (tier - 1) / 2f) * 5, 25.5f), 1.6f * _s, _c);
				break;
		}
	}

	/// <summary>Lua crescente como polígono: arco externo de um lado,
	/// arco interno deslocado de volta. Recortar com um círculo da cor
	/// do fundo não serve — a insígnia aparece sobre fundos diferentes.</summary>
	private void Crescent(float cx, float cy, float r)
	{
		const int steps = 16;
		var pts = new Vector2[steps * 2];
		for (int i = 0; i < steps; i++)
		{
			float a = Mathf.Lerp(0.35f * Mathf.Pi, 1.65f * Mathf.Pi, i / (float)(steps - 1));
			pts[i] = P(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
			float b = Mathf.Lerp(1.6f * Mathf.Pi, 0.4f * Mathf.Pi, i / (float)(steps - 1));
			pts[steps + i] = P(cx + 3.2f + Mathf.Cos(b) * r * 0.82f, cy + Mathf.Sin(b) * r * 0.82f);
		}
		DrawColoredPolygon(pts, _c);
	}

	private void Star(float cx, float cy, float r)
	{
		var pts = new Vector2[10];
		for (int i = 0; i < 10; i++)
		{
			float radius = i % 2 == 0 ? r : r * 0.42f;
			float a = -Mathf.Pi / 2 + i * Mathf.Pi / 5;
			pts[i] = P(cx + Mathf.Cos(a) * radius, cy + Mathf.Sin(a) * radius);
		}
		DrawColoredPolygon(pts, _c);
	}
}
