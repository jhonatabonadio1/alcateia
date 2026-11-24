using Godot;

/// <summary>
/// O fundo dos menus (17/11, terceira volta: "no modo mais escuro mesmo",
/// com Valorant e League of Legends de referência): azul-noite profundo,
/// uma névoa fria que se move devagar, um clarão azulado no alto, uma
/// brasa quente embaixo à direita (a cor do lobo), feixes de luz largos
/// na diagonal e um mosaico de triângulos quase invisível (o low poly).
/// Tudo desenhado por shader, sem imagem.
///
/// Os cartões das telas são vidro fosco escuro por cima disto
/// (LobbyShell): é este fundo, borrado, que aparece dentro deles.
/// </summary>
public partial class MenuBackdrop : Control
{
	/// <summary>Cor do alto.</summary>
	[Export] public Color Top = new("0b1522");

	/// <summary>Cor de baixo.</summary>
	[Export] public Color Bottom = new("070c13");

	/// <summary>O clarão frio do alto e a névoa.</summary>
	[Export] public Color Cool = new("2f6f96");

	/// <summary>A brasa de baixo, à direita.</summary>
	[Export] public Color Warm = new("b3541c");

	/// <summary>Força do mosaico de triângulos.</summary>
	[Export] public float Facets = 0.012f;

	private const string Code = @"
shader_type canvas_item;
uniform vec4 top_color : source_color = vec4(0.043, 0.082, 0.133, 1.0);
uniform vec4 bottom_color : source_color = vec4(0.027, 0.047, 0.075, 1.0);
uniform vec4 cool : source_color = vec4(0.184, 0.435, 0.588, 1.0);
uniform vec4 warm : source_color = vec4(0.702, 0.329, 0.11, 1.0);
uniform float facet = 0.012;
uniform float cell = 150.0;
uniform float aspect = 1.777;
uniform float drift = 0.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	vec2 uv = UV;
	vec2 wide = uv * vec2(aspect, 1.0);
	vec3 col = mix(top_color.rgb, bottom_color.rgb, clamp(uv.y * 1.1, 0.0, 1.0));

	// Clarão frio no alto, no meio; brasa embaixo, à direita.
	float sky = exp(-distance(wide, vec2(0.5 * aspect, -0.25)) * 1.25);
	col += cool.rgb * sky * 0.55;
	float ember = exp(-distance(wide, vec2(0.95 * aspect, 1.15)) * 1.7);
	col += warm.rgb * ember * 0.5;

	// Névoa: ruído largo, em duas oitavas, andando devagar.
	float fog = vnoise(wide * 1.6 + vec2(TIME * 0.012, TIME * 0.006)) * 0.65
		+ vnoise(wide * 3.7 - vec2(TIME * 0.02, 0.0)) * 0.35;
	col += cool.rgb * (fog - 0.45) * 0.13 * (1.0 - uv.y * 0.5);

	// Feixes de luz na diagonal, de beira larga.
	float d = uv.x * aspect * 0.55 - uv.y + drift;
	float beam = smoothstep(0.5, 0.62, d) * (1.0 - smoothstep(0.78, 0.9, d));
	float beam2 = smoothstep(-0.36, -0.28, d) * (1.0 - smoothstep(-0.16, -0.08, d));
	col += cool.rgb * (beam * 0.07 + beam2 * 0.045);

	// Mosaico: quadrados cortados na diagonal, cada triângulo um pouco
	// mais claro ou escuro.
	vec2 p = FRAGCOORD.xy / cell;
	vec2 id = floor(p);
	vec2 f = fract(p);
	float half_ = step(f.x, f.y);
	col += (hash(id + half_ * vec2(17.0, 31.0)) - 0.5) * facet;

	// Vinheta: as bordas caem para o preto.
	vec2 q = uv - 0.5;
	col *= 1.0 - dot(q, q) * 0.85;
	COLOR = vec4(max(col, vec3(0.0)), 1.0);
}";

	private ShaderMaterial _material;
	private float _time;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		UiKit.Fill(this);

		_material = new ShaderMaterial { Shader = new Shader { Code = Code } };
		_material.SetShaderParameter("top_color", Top);
		_material.SetShaderParameter("bottom_color", Bottom);
		_material.SetShaderParameter("cool", Cool);
		_material.SetShaderParameter("warm", Warm);
		_material.SetShaderParameter("facet", Facets);

		var fill = UiKit.Fill(new ColorRect { Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore, Material = _material });
		AddChild(fill);
		Resized += () => _material.SetShaderParameter("aspect", Size.Y > 0 ? Size.X / Size.Y : 1.777f);
	}

	public override void _Process(double delta)
	{
		// Os feixes vão e voltam bem devagar: o menu parece vivo sem distrair.
		_time += (float)delta;
		_material?.SetShaderParameter("drift", Mathf.Sin(_time * 0.12f) * 0.04f);
	}
}
