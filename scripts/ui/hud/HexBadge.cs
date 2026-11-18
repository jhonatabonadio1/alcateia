using Godot;

/// <summary>
/// A insígnia ao lado da vida: um hexágono (lados em pé, pontas em cima e
/// embaixo) com a borda da cor do time e, dentro, o ícone da forma atual
/// — humano ou lobo. É o lugar do retrato nos HUDs do gênero; aqui diz
/// "o que eu sou agora", que neste jogo muda no meio da partida.
/// </summary>
public partial class HexBadge : Control
{
	/// <summary>A cor da borda (o time).</summary>
	public Color Edge { get; set; } = UiTokens.TeamBlue;

	public Color Back { get; set; } = new(0.06f, 0.08f, 0.11f, 0.78f);

	/// <summary>O ícone de dentro.</summary>
	public Icon Glyph { get; }

	private const float W = 70f;
	private const float H = 80f;
	private const int GlyphSide = 36;

	public HexBadge()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(W, H);

		Glyph = Icon.Make(IconKind.Human, GlyphSide, UiTokens.TextPrimary);
		Glyph.Position = new Vector2((W - GlyphSide) / 2f, (H - GlyphSide) / 2f);
		Glyph.Size = new Vector2(GlyphSide, GlyphSide);
		AddChild(Glyph);
	}

	private static Vector2[] Shape(float inset)
	{
		float l = inset, r = W - inset, t = inset, b = H - inset;
		float shoulder = H * 0.25f + inset * 0.4f;
		return new Vector2[]
		{
			new(W / 2f, t), new(r, shoulder), new(r, H - shoulder), new(W / 2f, b), new(l, H - shoulder), new(l, shoulder)
		};
	}

	public override void _Draw()
	{
		DrawColoredPolygon(Shape(1.5f), Back);

		// Uma face mais clara na metade de cima: facetado.
		Vector2[] s = Shape(1.5f);
		DrawColoredPolygon(new[] { s[0], s[1], s[5] }, new Color(1, 1, 1, 0.07f));

		Vector2[] edge = Shape(1.5f);
		var loop = new Vector2[edge.Length + 1];
		edge.CopyTo(loop, 0);
		loop[^1] = edge[0];
		DrawPolyline(loop, new Color(0, 0, 0, 0.5f), 6f, true);
		DrawPolyline(loop, Edge, 3f, true);
	}
}
