using Godot;

/// <summary>
/// A arte de um item no cartão, numa área que escala mantendo a proporção.
///
/// **As armas são fotos do próprio modelo do jogo** (pedido do Jhon,
/// 17/11: "as armas que são utilizadas mesmo"): assets/ui/armas/&lt;id&gt;.png,
/// tiradas por scenes/tools/fotos_armas.tscn (<see cref="WeaponPhotos"/>)
/// do mesmo <see cref="WeaponModel"/> que vai na mão. A foto aparece
/// clareada (<see cref="BleachCode"/>): as armas são quase pretas, e
/// sobre os painéis escuros e sobre o jogo sumiriam.
///
/// O que não tem foto (melhoria, classe de lobo, serviço, ou uma arma
/// nova antes de fotografar) continua na silhueta desenhada por código,
/// na grade de 200×80.
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

	/// <summary>Na vitrine (cartão e detalhe da loja e do inventário): a
	/// foto é recortada no que a arma ocupa, para ela encher a área ("a
	/// arma pode estar mais próxima", Jhon, 18/11), e um brilho passa por
	/// ela de tempos em tempos. No HUD e no feed, não.</summary>
	public bool Showcase { get; set; }

	public static WeaponArt Make(string id, Vector2 size, bool dim = false, bool showcase = false) => new()
	{
		ItemId = id,
		CustomMinimumSize = size,
		Dim = dim,
		Showcase = showcase,
		MouseFilter = MouseFilterEnum.Ignore,
		SizeFlagsHorizontal = SizeFlags.ExpandFill,
		SizeFlagsVertical = SizeFlags.ExpandFill,
		// A foto tem 768 px e no feed aparece com 70.
		TextureFilter = TextureFilterEnum.LinearWithMipmaps,
		// A foto clareada: o jogo inteiro é escuro, e a arma também. A
		// skin colorida (o AR-15 Ultimate) não: clareada, o roxo virava
		// lilás lavado ("muito branca no inventário", Jhon, 18/11).
		// Na vitrine (loja e inventário), a foto como ela é, nítida, sem
		// clarear e sem o brilho que passava ("nenhuma arma do inventário
		// pode ter esse brilho branco; tem que ser nítido", Jhon, 18/11).
		Material = showcase || Vivid(id) ? null : Bleach()
	};

	private const string PhotoDir = "res://assets/ui/armas/";
	private static readonly System.Collections.Generic.Dictionary<string, Texture2D> Photos = new();

	/// <summary>A foto do modelo, ou null se o item não tem.</summary>
	public static Texture2D PhotoOf(string id)
	{
		if (string.IsNullOrEmpty(id)) return null;
		if (Photos.TryGetValue(id, out Texture2D photo)) return photo;
		// Foto de perfil (foto_<x>): a imagem dela, em assets/ui/avatares/.
		string path = id.StartsWith("foto_") ? $"res://assets/ui/avatares/{id[5..]}.png" : PhotoDir + id + ".png";
		photo = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		// Personagem: a imagem dele (assets/personagens/<pasta>/thumb.jpg).
		photo ??= Catalog.Get(id)?.Character?.Thumbnail;
		Photos[id] = photo;
		return photo;
	}

	private static readonly System.Collections.Generic.Dictionary<string, (Rect2 Used, bool Vivid)> Infos = new();

	/// <summary>O que a foto tem dentro: o retângulo que a arma ocupa (o
	/// resto é transparente) e se ela é colorida (skin) ou cinza (as armas
	/// de sempre). Medido uma vez por foto.</summary>
	private static (Rect2 Used, bool Vivid) InfoOf(string id)
	{
		if (Infos.TryGetValue(id ?? "", out var info)) return info;

		info = (new Rect2(), false);
		if (PhotoOf(id) is Texture2D photo && photo.GetImage() is Image image)
		{
			if (image.IsCompressed()) image.Decompress();
			Rect2I used = image.GetUsedRect();
			if (used.Size.X > 0 && used.Size.Y > 0)
			{
				double saturation = 0;
				int count = 0;
				for (int y = used.Position.Y; y < used.End.Y; y += 6)
				{
					for (int x = used.Position.X; x < used.End.X; x += 6)
					{
						Color c = image.GetPixel(x, y);
						if (c.A < 0.6f) continue;
						saturation += c.S;
						count++;
					}
				}
				info = (new Rect2(used.Position, used.Size), count > 0 && saturation / count > 0.32);
			}
		}

		Infos[id ?? ""] = info;
		return info;
	}

	/// <summary>A foto é de uma skin colorida: não se clareia.</summary>
	public static bool Vivid(string id) => InfoOf(id).Vivid;

	// A skin na vitrine: a cor como é, um pouco mais viva, e um brilho que
	// atravessa a arma em diagonal a cada poucos segundos.
	private const string ShineCode = @"
shader_type canvas_item;
varying vec4 tint;
void vertex() {
	tint = COLOR;
}
void fragment() {
	vec4 c = texture(TEXTURE, UV);
	float grey = dot(c.rgb, vec3(0.299, 0.587, 0.114));
	vec3 lit = mix(vec3(grey), c.rgb, 1.18);
	float sweep = fract(TIME / 3.6);
	float band = (UV.x + UV.y * 0.35) - (sweep * 2.2 - 0.6);
	float shine = exp(-band * band * 180.0);
	lit += vec3(1.0, 0.95, 1.0) * shine * 0.55;
	COLOR = vec4(clamp(lit, 0.0, 1.0), c.a) * tint;
}";

	private static ShaderMaterial _shine;

	private static ShaderMaterial Shine() =>
		_shine ??= new ShaderMaterial { Shader = new Shader { Code = ShineCode } };

	// Clareia a foto para ela se destacar no escuro: o metal quase preto
	// vira cinza, e a madeira, o verde e o bege continuam com a cor.
	private const string BleachCode = @"
shader_type canvas_item;
varying vec4 tint;
void vertex() {
	tint = COLOR;
}
void fragment() {
	// COLOR já chega multiplicado pela textura: a cor do desenho (o
	// esmaecido de item bloqueado) vem pelo vértice.
	vec4 c = texture(TEXTURE, UV);
	vec3 lit = c.rgb + (1.0 - c.rgb) * 0.3;
	COLOR = vec4(lit, c.a) * tint;
}";

	private static ShaderMaterial _bleach;

	private static ShaderMaterial Bleach() =>
		_bleach ??= new ShaderMaterial { Shader = new Shader { Code = BleachCode } };

	// No claro (menus), o desenho é escuro: claro sobre o cartão branco sumia.
	private static Color Metal => new("c3c9d2");
	private static Color Dark => new("7d8693");
	private static Color Wood => new("a8744a");
	private static Color Polymer => new("5f6773");

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

		if (PhotoOf(_id) is Texture2D photo)
		{
			// Na vitrine, só o que a arma ocupa na foto, para ela encher a
			// área; fora dela, a foto inteira. Sem distorcer, no meio.
			Rect2 used = Showcase ? InfoOf(_id).Used : new Rect2();
			if (used.Size.X > 0 && used.Size.Y > 0)
			{
				used = used.Grow(4f).Intersection(new Rect2(Vector2.Zero, photo.GetSize()));
				float close = Mathf.Min(Size.X / used.Size.X, Size.Y / used.Size.Y);
				Vector2 shown = used.Size * close;
				DrawTextureRectRegion(photo, new Rect2((Size - shown) / 2f, shown), used, new Color(1, 1, 1, _alpha));
				return;
			}

			Vector2 native = photo.GetSize();
			float fit = Mathf.Min(Size.X / native.X, Size.Y / native.Y);
			Vector2 size = native * fit;
			DrawTextureRect(photo, new Rect2((Size - size) / 2f, size), false, new Color(1, 1, 1, _alpha));
			return;
		}

		switch (_id)
		{
			case "ar15":
			case "ar15_ultimate":
			case "ar15_ouro":
			case "ar15_diamante":
			case "ar15_8bit":
			case "ar15_lava": RifleAr15(); break;
			case "m4a4":
			case "ak74":
			case "scar":
			case "svd":
			case "m4a4_neon":
			case "m4a4_dragao":
			case "ak74_tigre":
			case "ak74_imperial":
			case "scar_artico":
			case "scar_galaxia":
			case "svd_carbono":
			case "svd_tempestade": RifleAr15(); break;
			case "deagle":
			case "deagle_cobra":
			case "deagle_fenix": Pistol(); break;
			case Arsenal.KnifeId:
			case "faca_damasco":
			case "faca_obsidiana": Knife(); break;
			case Arsenal.GrenadeId:
			case "choque_carbono":
			case "choque_neon":
			case "choque_magma":
			case "choque_ouro": Grenade(); break;
			case "troca_nome": NameTag(); break;
			default:
				if (_id.StartsWith("tat_")) Tattoo();
				else if (_id.StartsWith("raca_")) WolfHead();
				else if (_id.StartsWith("pers_")) Person();
				break;
		}
	}

	// ------------------------------------------------------------------ armas

	private void RifleAr15()
	{
		Poly(Dark, 10, 31, 28, 31, 28, 46, 14, 52, 10, 50);                // coronha retrátil
		Rect(Dark, 28, 33, 22, 4);                                         // tubo da coronha
		Rect(Metal, 50, 28, 60, 16);                                       // caixa
		Rect(Dark, 52, 24, 58, 4);                                         // trilho
		Rect(Dark, 74, 15, 14, 9);                                         // mira red dot
		Poly(Dark, 110, 28, 140, 28, 140, 42, 110, 42);                    // guarda-mão
		Rect(Dark, 140, 30, 52, 10);                                       // silenciador
		Poly(Dark, 64, 44, 76, 44, 71, 62, 60, 60);                        // punho
		Poly(Metal, 84, 44, 98, 44, 98, 60, 85, 60);                       // carregador curto
		Line(Metal, 2.5f, 76, 44, 78, 52, 84, 52);                         // guarda-mato
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
		Color ink = new Color("d9c7a3");
		DrawArcAt(100, 40, 30, ink);
		for (int i = 0; i < 3; i++)
		{
			float x = 84 + i * 14;
			Poly(ink, x, 20, x + 6, 20, x + 12, 44, x + 4, 62, x + 6, 44);
		}
	}

	private static Texture2D _wolfMark;

	/// <summary>A cabeça do lobo do ícone do jogo (assets/ui/icones/wolf.png,
	/// de tools/icones/wolf.svg), com os olhos e o nariz vazados.</summary>
	private static Texture2D WolfMark => _wolfMark ??= GD.Load<Texture2D>("res://assets/ui/icones/wolf.png");

	/// <summary>A cabeça do lobo centrada em (cx, cy), com <paramref name="side"/> de lado, na grade.</summary>
	private void WolfAt(float cx, float cy, float side, Color c)
	{
		if (WolfMark == null) return;
		DrawTextureRect(WolfMark, new Rect2(P(cx - side / 2f, cy - side / 2f), new Vector2(side, side) * _s), false, new Color(c, c.A * _alpha));
	}

	/// <summary>Classe de lobo (Jhon, 21/11: "essas classes também estão mal
	/// feitas"): a cabeça do lobo do ícone do jogo, e o que distingue a
	/// classe atrás dela — o Bruto, um escudo (aguenta mais); o Ligeiro,
	/// riscos de velocidade (corre e pula mais).</summary>
	private void WolfHead()
	{
		if (_id == "raca_ligeiro")
		{
			for (int i = 0; i < 3; i++)
			{
				float y = 30 + i * 12, x = 42 + (i == 1 ? 0 : 10);
				Poly(Dark, x, y, x + 34, y, x + 30, y + 5, x - 4, y + 5);
			}
		}
		else
		{
			// escudo hexagonal atrás da cabeça
			Poly(new Color(Dark, 0.55f), 100, 0, 135, 18, 135, 58, 100, 78, 65, 58, 65, 18);
		}
		WolfAt(100, 40, 66, Metal);
	}

	/// <summary>Personagem: o busto do humano e, atrás dele, a cabeça do
	/// lobo que vem junto.</summary>
	private void Person()
	{
		WolfAt(128, 36, 52, Dark);                                                           // lobo atrás
		DrawEllipse(84, 26, 13, 15, Metal);                                                    // cabeça
		Poly(Metal, 76, 40, 92, 40, 94, 46, 116, 52, 122, 76, 46, 76, 52, 52, 74, 46);         // ombros
		Rect(UiTokens.Accent, 64, 56, 40, 3);                                                  // faixa do time
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
