using Godot;

/// <summary>
/// A arte de um item no cartão: silhueta desenhada por código, numa área
/// de 200×80 que escala mantendo a proporção.
///
/// Nesse gênero o inventário é parte da fantasia de poder — a arte ocupa
/// mais da metade do cartão. Enquanto não houver ilustração de verdade,
/// cada arma tem uma silhueta própria (alça de transporte no M16,
/// carregador curvo e madeira no AK, tubo e telha na M3) para o jogador
/// reconhecer a arma pela forma antes de ler o nome.
///
/// Quando a arte final existir, ela entra aqui (uma textura por id) e o
/// resto da interface não muda.
/// </summary>
public partial class WeaponArt : Control
{
	private string _id = "";

	public string ItemId
	{
		get => _id;
		set { _id = value ?? ""; QueueRedraw(); }
	}

	/// <summary>Acinzenta: item bloqueado, expirado ou indisponível.</summary>
	public bool Dim { get; set; }

	public static WeaponArt Make(string id, Vector2 size, bool dim = false) => new()
	{
		ItemId = id,
		CustomMinimumSize = size,
		Dim = dim,
		MouseFilter = MouseFilterEnum.Ignore,
		SizeFlagsHorizontal = SizeFlags.ExpandFill,
		SizeFlagsVertical = SizeFlags.ExpandFill
	};

	private static readonly Color Metal = new("c3c9d2");
	private static readonly Color Dark = new("7d8693");
	private static readonly Color Wood = new("a8744a");
	private static readonly Color Polymer = new("5f6773");

	private float _s;
	private Vector2 _o;
	private float _alpha = 1f;

	private Vector2 P(float x, float y) => _o + new Vector2(x, y) * _s;

	private void Poly(Color c, params float[] xy)
	{
		var pts = new Vector2[xy.Length / 2];
		for (int i = 0; i < pts.Length; i++) pts[i] = P(xy[i * 2], xy[i * 2 + 1]);
		DrawColoredPolygon(pts, new Color(c, c.A * _alpha));
	}

	private void Rect(Color c, float x, float y, float w, float h) =>
		DrawRect(new Rect2(P(x, y), new Vector2(w, h) * _s), new Color(c, c.A * _alpha));

	private void Line(Color c, float width, params float[] xy)
	{
		var pts = new Vector2[xy.Length / 2];
		for (int i = 0; i < pts.Length; i++) pts[i] = P(xy[i * 2], xy[i * 2 + 1]);
		DrawPolyline(pts, new Color(c, c.A * _alpha), Mathf.Max(1f, width * _s), true);
	}

	public override void _Draw()
	{
		if (Size.X <= 0 || Size.Y <= 0) return;

		_s = Mathf.Min(Size.X / 200f, Size.Y / 80f);
		_o = (Size - new Vector2(200, 80) * _s) / 2f;
		_alpha = Dim ? 0.35f : 1f;

		switch (_id)
		{
			case "m16a2": RifleM16(); break;
			case "ak47": RifleAk(); break;
			case "sg552": RifleSg(); break;
			case "ump45": Smg(); break;
			case "m3": Shotgun(); break;
			case "m9": Pistol(); break;
			case "faca": Knife(); break;
			case "choque": Grenade(); break;
			case "troca_nome": NameTag(); break;
			default:
				if (_id.StartsWith("tat_")) Tattoo();
				else if (_id.StartsWith("raca_")) WolfHead();
				break;
		}
	}

	// ------------------------------------------------------------------ armas

	private void RifleM16()
	{
		Poly(Dark, 8, 30, 50, 31, 50, 43, 12, 52, 7, 49);                 // coronha
		Rect(Metal, 50, 28, 62, 16);                                       // caixa
		Line(Metal, 4, 60, 28, 63, 19, 99, 19, 102, 28);                   // alça
		Poly(Dark, 112, 29, 152, 31, 152, 42, 112, 44);                    // guarda-mão
		Rect(Metal, 152, 34, 38, 4);                                       // cano
		Poly(Metal, 160, 34, 163, 24, 166, 24, 168, 34);                   // massa de mira
		Poly(Dark, 66, 44, 78, 44, 73, 62, 62, 60);                        // punho
		Poly(Metal, 84, 44, 98, 44, 97, 68, 85, 68);                       // carregador reto
		Line(Metal, 2.5f, 78, 44, 80, 52, 86, 52);                         // guarda-mato
	}

	private void RifleAk()
	{
		Poly(Wood, 6, 31, 50, 31, 50, 42, 10, 54, 5, 50);                  // coronha de madeira
		Rect(Metal, 50, 28, 58, 15);
		Poly(Wood, 108, 30, 146, 31, 146, 42, 108, 43);                    // guarda-mão de madeira
		Rect(Dark, 108, 25, 46, 4);                                        // tubo de gás
		Rect(Metal, 146, 33, 44, 4);
		Poly(Metal, 152, 33, 154, 26, 157, 26, 159, 33);
		Poly(Dark, 64, 43, 76, 43, 70, 62, 59, 60);
		Poly(Metal, 82, 43, 96, 43, 104, 60, 107, 70, 95, 73, 88, 60);    // carregador curvo
		Line(Metal, 2.5f, 76, 43, 78, 51, 83, 51);
	}

	private void RifleSg()
	{
		Line(Polymer, 4, 10, 30, 46, 30, 46, 44, 12, 50, 10, 30);          // coronha vazada
		Rect(Polymer, 46, 27, 74, 16);
		Rect(Dark, 54, 22, 40, 5);                                         // trilho
		Poly(Polymer, 120, 29, 150, 30, 150, 42, 120, 43);
		Rect(Metal, 150, 34, 26, 4);
		Poly(Dark, 64, 43, 76, 43, 71, 61, 60, 59);
		Poly(new Color(Metal, 0.65f), 84, 43, 97, 43, 99, 66, 86, 66);     // carregador translúcido
		Line(Metal, 2.5f, 76, 43, 78, 51, 84, 51);
	}

	private void Smg()
	{
		Line(Dark, 4, 8, 32, 40, 32, 40, 42, 10, 46, 8, 32);               // coronha rebatível
		Poly(Polymer, 40, 25, 132, 25, 132, 46, 40, 46);                    // caixa quadrada
		Rect(Dark, 48, 20, 60, 5);
		Rect(Metal, 132, 32, 22, 6);
		Poly(Dark, 58, 46, 70, 46, 66, 66, 55, 64);
		Poly(Polymer, 88, 46, 101, 46, 100, 72, 89, 72);
		Line(Metal, 2.5f, 70, 46, 72, 54, 78, 54);
	}

	private void Shotgun()
	{
		Poly(Polymer, 6, 32, 52, 33, 52, 44, 10, 55, 5, 51);
		Rect(Dark, 52, 29, 40, 15);
		Rect(Metal, 92, 30, 98, 5);                                        // cano
		Rect(Dark, 92, 37, 78, 5);                                         // tubo
		Poly(Polymer, 112, 35, 150, 35, 150, 46, 112, 46);                 // telha
		Poly(Dark, 60, 44, 72, 44, 67, 62, 56, 60);
		Line(Metal, 2.5f, 72, 44, 74, 52, 80, 52);
	}

	private void Pistol()
	{
		Rect(Metal, 58, 22, 86, 14);                                       // ferrolho
		Rect(Dark, 136, 26, 10, 6);
		Poly(Dark, 62, 36, 124, 36, 122, 42, 62, 42);                       // armação
		Poly(Polymer, 66, 40, 90, 40, 84, 72, 60, 70);                      // empunhadura
		Line(Metal, 2.5f, 92, 42, 94, 50, 104, 50, 106, 42);
		for (int i = 0; i < 4; i++) Rect(Dark, 64 + i * 12, 24, 2, 9);     // serrilhado
	}

	private void Knife()
	{
		Poly(Metal, 72, 36, 152, 28, 176, 38, 152, 46, 72, 46);
		Line(new Color(1, 1, 1, 0.5f), 1.5f, 80, 38, 160, 33);
		Rect(Dark, 64, 30, 8, 22);
		Poly(Polymer, 24, 36, 64, 35, 64, 47, 26, 49, 20, 43);
	}

	private void Grenade()
	{
		DrawEllipse(100, 48, 20, 24, Polymer);
		Rect(Dark, 90, 18, 20, 10);
		Poly(Metal, 108, 20, 132, 30, 128, 36, 106, 26);                   // colher
		Line(Metal, 3, 84, 22, 78, 16, 84, 10, 90, 16);                    // pino
		Rect(UiTokens.Warning, 82, 44, 36, 5);                             // faixa do choque
	}

	private void NameTag()
	{
		Poly(Metal, 50, 20, 150, 20, 150, 62, 50, 62);
		Rect(UiTokens.Accent, 50, 20, 100, 9);
		Rect(Dark, 62, 38, 60, 4);
		Rect(Dark, 62, 48, 40, 4);
		Line(Dark, 3, 136, 40, 156, 64);                                   // caneta
		Poly(Dark, 154, 61, 160, 70, 152, 66);
	}

	private void Tattoo()
	{
		Color ink = new("d9c7a3");
		DrawArcAt(100, 40, 30, ink);
		for (int i = 0; i < 3; i++)
		{
			float x = 84 + i * 14;
			Poly(ink, x, 20, x + 6, 20, x + 12, 44, x + 4, 62, x + 6, 44);
		}
	}

	private void WolfHead()
	{
		Poly(Metal, 66, 10, 84, 28, 116, 28, 134, 10, 136, 40, 116, 64, 100, 72, 84, 64, 64, 40);
		Poly(UiTokens.Bg, 84, 40, 92, 38, 92, 44);
		Poly(UiTokens.Bg, 116, 40, 108, 38, 108, 44);
		Poly(Dark, 94, 60, 106, 60, 100, 66);
	}

	// ------------------------------------------------------------------ apoio

	private void DrawEllipse(float cx, float cy, float rx, float ry, Color c)
	{
		var pts = new Vector2[24];
		for (int i = 0; i < pts.Length; i++)
		{
			float a = i * Mathf.Tau / pts.Length;
			pts[i] = P(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
		}
		DrawColoredPolygon(pts, new Color(c, c.A * _alpha));
	}

	private void DrawArcAt(float cx, float cy, float r, Color c) =>
		DrawArc(P(cx, cy), r * _s, 0, Mathf.Tau, 40, new Color(c, c.A * _alpha), Mathf.Max(1f, 2.5f * _s), true);
}
