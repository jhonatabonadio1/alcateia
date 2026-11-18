using Godot;

/// <summary>
/// O fundo dos menus (pedido do Jhon, 17/11): a foto do mapa, borrada e
/// escurecida, com um mosaico de triângulos bem fraco por cima (o low
/// poly) e um zoom lento que não para — o menu parece vivo sem distrair.
/// Os painéis do tema são translúcidos: em cima deste fundo, viram vidro.
/// </summary>
public partial class MenuBackdrop : Control
{
	/// <summary>Raio do borrão, em fração da largura da foto.</summary>
	[Export] public float Blur = 0.0055f;

	/// <summary>Quanto o fundo puxa para a cor escura do tema (0 = a foto,
	/// 1 = só a cor).</summary>
	[Export] public float Darken = 0.58f;

	/// <summary>Força do mosaico de triângulos.</summary>
	[Export] public float Facets = 0.045f;

	private const string Code = @"
shader_type canvas_item;
uniform float radius = 0.0055;
uniform vec4 tint : source_color = vec4(0.06, 0.075, 0.095, 1.0);
uniform float darken = 0.58;
uniform float facet = 0.045;
uniform float cell = 120.0;
void fragment() {
	vec3 acc = vec3(0.0);
	float total = 0.0;
	for (int x = -3; x <= 3; x++) {
		for (int y = -3; y <= 3; y++) {
			float k = exp(-float(x * x + y * y) / 8.0);
			acc += texture(TEXTURE, UV + vec2(float(x), float(y)) * radius).rgb * k;
			total += k;
		}
	}
	vec3 col = mix(acc / total, tint.rgb, darken);

	// Mosaico: a tela em quadrados cortados na diagonal, cada triângulo
	// um pouco mais claro ou escuro.
	vec2 p = FRAGCOORD.xy / cell;
	vec2 id = floor(p);
	vec2 f = fract(p);
	float half_ = step(f.x, f.y);
	float h = fract(sin(dot(id + half_ * vec2(17.0, 31.0), vec2(12.9898, 78.233))) * 43758.5453);
	col += (h - 0.5) * facet;

	// Vinheta: as bordas mais escuras, o olho vai para o meio.
	vec2 q = UV - 0.5;
	col *= 1.0 - dot(q, q) * 0.9;
	COLOR = vec4(col, 1.0);
}";

	private TextureRect _photo;
	private Tween _drift;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		ClipContents = true;
		UiKit.Fill(this);
		AddChild(UiKit.Fill(new ColorRect { Color = UiTokens.Bg, MouseFilter = MouseFilterEnum.Ignore }));

		string cover = Maps.All.Count > 0 ? Maps.All[0].Picture : null;
		if (string.IsNullOrEmpty(cover) || !ResourceLoader.Exists(cover)) return;

		var material = new ShaderMaterial { Shader = new Shader { Code = Code } };
		material.SetShaderParameter("radius", Blur);
		material.SetShaderParameter("darken", Darken);
		material.SetShaderParameter("facet", Facets);
		material.SetShaderParameter("tint", UiTokens.Bg);

		_photo = UiKit.Fill(new TextureRect
		{
			Texture = GD.Load<Texture2D>(cover),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = MouseFilterEnum.Ignore,
			Material = material
		});
		AddChild(_photo);

		Resized += Center;
		Center();
		Drift();
	}

	private void Center() { if (_photo != null) _photo.PivotOffset = Size / 2f; }

	/// <summary>Zoom e deslize lentos, ida e volta, sem fim.</summary>
	private void Drift()
	{
		_drift = CreateTween().SetLoops();
		_drift.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_drift.TweenProperty(_photo, "scale", new Vector2(1.08f, 1.08f), 18.0);
		_drift.Parallel().TweenProperty(_photo, "position", new Vector2(-24, -10), 18.0);
		_drift.TweenProperty(_photo, "scale", Vector2.One, 18.0);
		_drift.Parallel().TweenProperty(_photo, "position", Vector2.Zero, 18.0);
	}
}
