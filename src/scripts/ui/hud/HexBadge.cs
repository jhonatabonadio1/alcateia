using Godot;

/// <summary>
/// O retrato ao lado da vida, no canto inferior esquerdo: a foto de perfil
/// num hexágono, o filete do time e, em volta, a borda do nível — a mesma
/// do perfil (<see cref="RankBadge"/>), com o brilho dos níveis altos
/// (<see cref="FrameShine"/>), a placa com o número e as estrelas
/// (<see cref="LevelPlate"/>, como no perfil; Jhon, 24/11: "as estrelas do
/// rank não aparecem na HUD do jogo") e o fogo quando o jogador está em chamas
/// (<see cref="FireAura"/>).
///
/// Era o ícone de humano ou lobo (Jhon, 21/11: "precisa de mais espaço e
/// exibir foto de perfil ao invés de human ou lobo"); a forma continua
/// escrita embaixo da vida. O ícone só aparece quando não há foto (um bot
/// sendo assistido). A caixa é maior que o hexágono para a borda caber sem
/// encostar na vida nem sair da tela.
/// </summary>
public partial class HexBadge : Control
{
	/// <summary>A cor do filete (o time).</summary>
	public Color Edge { get; set; } = UiTokens.TeamBlue;

	public Color Back { get; set; } = new(0.06f, 0.08f, 0.11f, 0.78f);

	/// <summary>O ícone da forma, para quando não há foto.</summary>
	public Icon Glyph { get; }

	/// <summary>O hexágono e a caixa que guarda lugar para a borda.</summary>
	private const float W = 84f, H = 96f;
	private static readonly Vector2 Box = new(156f, 138f);
	private const int GlyphSide = 40;

	/// <summary>O vértice do aro no desenho da borda: 292 de 1024 (tools/bordas.py).</summary>
	private const float FrameVertex = 292f / 1024f;
	private const float FrameSide = (H / 2f + 4f) / FrameVertex;

	private readonly FireAura _fire;
	private readonly FrameShine _shine;
	private readonly LevelPlate _plate;
	private int _rank = Ranks.None;
	private string _photo;

	/// <summary>O nível cuja borda vai em volta; <see cref="Ranks.None"/>, sem borda.</summary>
	public int Rank
	{
		get => _rank;
		set
		{
			int rank = value < 0 ? Ranks.None : Ranks.Clamp(value);
			if (rank == _rank) return;
			_rank = rank;
			_shine.Rank = rank;
			_plate.Rank = rank;
			QueueRedraw();
		}
	}

	/// <summary>A foto de perfil (<see cref="Avatars"/>); sem ela, o ícone da forma.</summary>
	public string Photo
	{
		get => _photo;
		set
		{
			if (_photo == value) return;
			_photo = value;
			Glyph.Visible = Avatars.PhotoOf(value) == null;
			QueueRedraw();
		}
	}

	/// <summary>Em chamas (rage): 0 apagado, até 1 no calor máximo.</summary>
	public float Fire
	{
		get => _fire.Intensity;
		set => _fire.Intensity = value;
	}

	public HexBadge()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = Box;
		TextureFilter = TextureFilterEnum.LinearWithMipmaps;   // a borda é 512 px desenhada a ~180

		// o brilho da borda (níveis altos), por cima dela
		_shine = new FrameShine { Side = FrameSide };
		_shine.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_shine);

		Glyph = Icon.Make(IconKind.Human, GlyphSide, UiTokens.TextPrimary);
		Glyph.AnchorLeft = Glyph.AnchorRight = Glyph.AnchorTop = Glyph.AnchorBottom = 0.5f;
		Glyph.OffsetLeft = Glyph.OffsetTop = -GlyphSide / 2f;
		Glyph.OffsetRight = Glyph.OffsetBottom = GlyphSide / 2f;
		AddChild(Glyph);

		// a placa do número e as estrelas, por cima do brilho e do ícone,
		// no vértice de baixo do hexágono (o mesmo lugar do perfil)
		_plate = new LevelPlate { Side = FrameSide / RankBadge.Grow };
		_plate.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_plate);

		_fire = FireAura.On(this, H * 0.52f);
	}

	private static Vector2[] Shape(Vector2 origin, float inset)
	{
		float l = inset, r = W - inset, t = inset, b = H - inset;
		float shoulder = H * 0.25f + inset * 0.4f;
		return new[]
		{
			origin + new Vector2(W / 2f, t), origin + new Vector2(r, shoulder), origin + new Vector2(r, H - shoulder),
			origin + new Vector2(W / 2f, b), origin + new Vector2(l, H - shoulder), origin + new Vector2(l, shoulder)
		};
	}

	public override void _Draw()
	{
		Vector2 center = Size / 2f;
		Vector2 origin = center - new Vector2(W, H) / 2f;
		Vector2[] s = Shape(origin, 1.5f);

		DrawColoredPolygon(s, Back);
		if (Avatars.PhotoOf(_photo) is Texture2D photo)
			RankBadge.DrawPortrait(this, center, H / 2f - 2f, photo);
		else
			DrawColoredPolygon(new[] { s[0], s[1], s[5] }, new Color(1, 1, 1, 0.07f));   // uma face mais clara: facetado

		var loop = new Vector2[s.Length + 1];
		s.CopyTo(loop, 0);
		loop[^1] = s[0];
		DrawPolyline(loop, new Color(0, 0, 0, 0.5f), 6f, true);
		DrawPolyline(loop, Edge, 3f, true);

		// o aro da borda casa com o hexágono: o vértice dele no vértice daqui
		if (_rank >= 0 && RankBadge.FrameOf(_rank) is Texture2D frame)
			DrawTextureRect(frame, new Rect2(center - new Vector2(FrameSide, FrameSide) / 2f, new Vector2(FrameSide, FrameSide)), false);
	}
}
