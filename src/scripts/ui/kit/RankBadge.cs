using Godot;
using System.Collections.Generic;

/// <summary>
/// O retrato do jogador com a borda do nível (era a insígnia de patente;
/// Jhon, 21/11, no modelo do Overwatch): a foto de perfil num hexágono, a
/// borda do metal em volta (assets/ui/bordas/M_T.png, de tools/bordas.py),
/// e embaixo a placa com o número e as estrelas — "20 ★★★★★" é o nível 520.
///
/// Pequeno (menos de 40 px, linhas de lista) fica só a borda com a foto:
/// número e estrelas não se leem nesse tamanho, e a linha já diz o nível.
/// Sem foto, o miolo fica azul-noite. Bot: nada.
/// </summary>
public partial class RankBadge : Control
{
	private const string FrameDir = "res://src/assets/ui/bordas/";
	private static readonly Dictionary<string, Texture2D> Cache = new();

	/// <summary>Raio do vão da foto, em fração do lado da borda (o desenho tem
	/// o aro com raio 300 de 1024 e o vão começa uns 35 px para dentro).</summary>
	public const float Window = 0.262f;

	/// <summary>Quanto a borda passa do quadrado do retrato (os enfeites saem pelos lados).</summary>
	public const float Grow = 1.12f;

	private int _rank;
	private string _avatar;
	private bool _bare;

	// Por cima do retrato e da borda (desenhados aqui): o brilho da borda
	// (níveis altos) e, por cima dele, a placa do número com as estrelas.
	private readonly FrameShine _shine;
	private readonly LevelPlate _plate;

	public RankBadge()
	{
		_shine = new FrameShine();
		_shine.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_shine);
		_plate = new LevelPlate();
		_plate.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_plate);
	}

	/// <summary>Brilho e placa só no retrato grande (40 px ou mais); a placa some com <see cref="Bare"/>.</summary>
	private void Sync()
	{
		bool big = _rank >= 0 && Mathf.Min(Size.X, Size.Y) >= 40f;
		_shine.Rank = big ? _rank : Ranks.None;
		_plate.Rank = big && !_bare ? _rank : Ranks.None;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized) Sync();
	}

	public int Rank
	{
		get => _rank;
		set
		{
			_rank = value < 0 ? Ranks.None : Ranks.Clamp(value);
			TooltipText = _rank < 0 ? "Bot" : $"{Ranks.NameOf(_rank)} · {Ranks.FamilyName(_rank)}";
			Sync();
			QueueRedraw();
		}
	}

	/// <summary>A foto de perfil: o id de um item de foto (<see cref="Avatars"/>)
	/// ou nulo (miolo vazio).</summary>
	public string Avatar
	{
		get => _avatar;
		set { _avatar = value; QueueRedraw(); }
	}

	/// <summary>Sem placa nem estrelas, mesmo grande.</summary>
	public bool Bare
	{
		get => _bare;
		set { _bare = value; Sync(); }
	}

	public static RankBadge Make(int rank, int size = 32, bool bare = false, string avatar = null)
	{
		var badge = new RankBadge
		{
			CustomMinimumSize = new Vector2(size, size),
			Bare = bare,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Pass,
			TextureFilter = TextureFilterEnum.LinearWithMipmaps,
			_avatar = avatar
		};
		badge.Rank = rank;
		return badge;
	}

	public static Texture2D Load(string path)
	{
		if (string.IsNullOrEmpty(path)) return null;
		if (Cache.TryGetValue(path, out Texture2D texture)) return texture;
		texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		if (texture == null) GD.PrintErr($"RankBadge: not found: {path}");
		Cache[path] = texture;
		return texture;
	}

	public static Texture2D FrameOf(int rank) => Load($"{FrameDir}{Ranks.Family(rank)}_{Ranks.Tier(rank)}.png");

	/// <summary>O hexágono de ponta para cima, centrado em c.</summary>
	public static Vector2[] Hex(Vector2 c, float r)
	{
		var pts = new Vector2[6];
		for (int i = 0; i < 6; i++)
		{
			float a = Mathf.DegToRad(-90 + 60 * i);
			pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
		}
		return pts;
	}

	/// <summary>Pinta a foto recortada no hexágono (ou o miolo escuro).</summary>
	public static void DrawPortrait(CanvasItem on, Vector2 c, float r, Texture2D photo)
	{
		Vector2[] pts = Hex(c, r);
		if (photo == null)
		{
			on.DrawColoredPolygon(pts, new Color("0a1119"));
			return;
		}
		var uv = new Vector2[6];
		var colors = new Color[6];
		for (int i = 0; i < 6; i++)
		{
			// o hexágono é mais alto que largo: a foto quadrada cobre a altura
			uv[i] = new Vector2(0.5f + (pts[i].X - c.X) / (2f * r), 0.5f + (pts[i].Y - c.Y) / (2f * r));
			colors[i] = Colors.White;
		}
		on.DrawPolygon(pts, colors, uv, photo);
	}

	/// <summary>Estrela de cinco pontas.</summary>
	public static Vector2[] Star(Vector2 c, float r)
	{
		var pts = new Vector2[10];
		for (int i = 0; i < 10; i++)
		{
			float a = Mathf.DegToRad(-90 + 36 * i);
			pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (i % 2 == 0 ? r : r * 0.45f);
		}
		return pts;
	}

	public override void _Draw()
	{
		float side = Mathf.Min(Size.X, Size.Y);
		if (side <= 0 || _rank < 0) return;

		// Pequena, a borda passa do quadrado (os enfeites saem pelos lados);
		// grande, cabe inteira.
		bool small = side < 40;
		float grow = small ? 1.45f : Grow;
		float frame = side * grow;
		Vector2 c = Size / 2f;

		DrawPortrait(this, c, frame * Window, Avatars.PhotoOf(_avatar));
		if (FrameOf(_rank) is Texture2D art)
			DrawTextureRect(art, new Rect2(c - new Vector2(frame, frame) / 2f, new Vector2(frame, frame)), false);
		// a placa e o brilho são os filhos (LevelPlate, FrameShine)
	}

	/// <summary>A placa do número e, embaixo, as estrelas (quem chama é o <see cref="LevelPlate"/>).</summary>
	public static void DrawPlate(CanvasItem on, Vector2 top, float height, int rank, Font font)
	{
		Color metal = Ranks.ColorOf(rank);
		string number = Ranks.Shown(rank).ToString();
		int size = Mathf.Max(8, Mathf.RoundToInt(height * 0.78f));
		float width = Mathf.Max(height * 1.7f, font.GetStringSize(number, HorizontalAlignment.Left, -1, size).X + height * 0.8f);

		var plate = new Rect2(top.X - width / 2f, top.Y - height / 2f, width, height);
		var box = new StyleBoxFlat
		{
			BgColor = new Color("0a1119"), BorderColor = metal,
			CornerDetail = 3
		};
		box.SetBorderWidthAll(Mathf.Max(1, Mathf.RoundToInt(height * 0.09f)));
		box.SetCornerRadiusAll(Mathf.RoundToInt(height * 0.22f));
		on.DrawStyleBox(box, plate);
		Vector2 text = new(plate.Position.X, plate.Position.Y + height / 2f + font.GetAscent(size) * 0.36f);
		on.DrawString(font, text, number, HorizontalAlignment.Center, width, size, Ranks.InkOf(rank));

		int stars = Ranks.Stars(rank);
		if (stars == 0) return;
		float r = height * 0.36f;
		float step = r * 2.1f;
		float y = plate.End.Y + r * 1.15f;
		for (int i = 0; i < stars; i++)
		{
			Vector2 sc = new(top.X + (i - (stars - 1) / 2f) * step, y);
			on.DrawColoredPolygon(Star(sc + new Vector2(0, r * 0.12f), r * 1.12f), new Color(0, 0, 0, 0.55f));
			on.DrawColoredPolygon(Star(sc, r), metal.Lightened(0.15f));
		}
	}
}
