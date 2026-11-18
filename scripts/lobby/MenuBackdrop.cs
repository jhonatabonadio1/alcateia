using Godot;

/// <summary>
/// O fundo dos menus (pedido do Jhon, 17/11): claro, como o de Overwatch,
/// com o corte de Valorant — um degradê frio quase branco, uma luz quente
/// no alto à direita e um tom lilás embaixo à esquerda, um mosaico de
/// triângulos fraco (o low poly), faixas largas na diagonal e um fio
/// laranja cruzando a tela. Tudo desenhado por shader, sem imagem.
///
/// Os cartões das telas são vidro fosco por cima disto (LobbyShell): é
/// este fundo, borrado, que aparece dentro deles — por isso ele tem luz,
/// cor e forma, e não é um cinza liso.
/// </summary>
public partial class MenuBackdrop : Control
{
	/// <summary>Cor do alto, à esquerda.</summary>
	[Export] public Color Top = new("eef2f7");

	/// <summary>Cor de baixo, à direita.</summary>
	[Export] public Color Bottom = new("bfcbdb");

	/// <summary>A luz do alto, à direita.</summary>
	[Export] public Color Warm = new("ffe0c8");

	/// <summary>O tom de baixo, à esquerda.</summary>
	[Export] public Color Cool = new("b3c0ea");

	/// <summary>O fio da diagonal.</summary>
	[Export] public Color Accent = UiTokens.TeamOrange;

	/// <summary>Força do mosaico de triângulos.</summary>
	[Export] public float Facets = 0.032f;

	private const string Code = @"
shader_type canvas_item;
uniform vec4 top_color : source_color = vec4(0.933, 0.949, 0.969, 1.0);
uniform vec4 bottom_color : source_color = vec4(0.749, 0.796, 0.859, 1.0);
uniform vec4 warm : source_color = vec4(1.0, 0.878, 0.784, 1.0);
uniform vec4 cool : source_color = vec4(0.702, 0.753, 0.918, 1.0);
uniform vec4 accent : source_color = vec4(1.0, 0.478, 0.184, 1.0);
uniform float facet = 0.032;
uniform float cell = 140.0;
uniform float aspect = 1.777;
uniform float drift = 0.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
	vec2 uv = UV;
	vec2 wide = uv * vec2(aspect, 1.0);
	vec3 col = mix(top_color.rgb, bottom_color.rgb, clamp(uv.y * 0.75 + uv.x * 0.35, 0.0, 1.0));

	// Luz quente no alto à direita, tom frio embaixo à esquerda.
	float sun = exp(-distance(wide, vec2(0.84 * aspect, 0.05)) * 1.5);
	col = mix(col, warm.rgb, sun * 0.6);
	float shade = exp(-distance(wide, vec2(0.08 * aspect, 0.98)) * 1.4);
	col = mix(col, cool.rgb, shade * 0.5);

	// Mosaico: quadrados cortados na diagonal, cada triângulo um pouco
	// mais claro ou escuro.
	vec2 p = FRAGCOORD.xy / cell;
	vec2 id = floor(p);
	vec2 f = fract(p);
	float half_ = step(f.x, f.y);
	col += (hash(id + half_ * vec2(17.0, 31.0)) - 0.5) * facet;

	// Faixas diagonais (o corte de Valorant): duas largas, mais escuras,
	// e o fio laranja entre elas. Andam devagar.
	float d = uv.x * aspect * 0.55 - uv.y + drift;
	float slab = smoothstep(0.58, 0.585, d) * (1.0 - smoothstep(0.86, 0.865, d));
	float slab2 = smoothstep(1.02, 1.025, d) * (1.0 - smoothstep(1.12, 1.125, d));
	float slab3 = smoothstep(-0.32, -0.315, d) * (1.0 - smoothstep(-0.12, -0.115, d));
	col *= 1.0 - 0.09 * slab - 0.06 * slab2;
	col += 0.05 * slab3;
	float wire = smoothstep(0.935, 0.937, d) * (1.0 - smoothstep(0.943, 0.945, d));
	col = mix(col, accent.rgb, wire * 0.9);

	// Vinheta leve nas bordas.
	vec2 q = uv - 0.5;
	col *= 1.0 - dot(q, q) * 0.22;
	COLOR = vec4(col, 1.0);
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
		_material.SetShaderParameter("warm", Warm);
		_material.SetShaderParameter("cool", Cool);
		_material.SetShaderParameter("accent", Accent);
		_material.SetShaderParameter("facet", Facets);

		var fill = UiKit.Fill(new ColorRect { Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore, Material = _material });
		AddChild(fill);
		Resized += () => _material.SetShaderParameter("aspect", Size.Y > 0 ? Size.X / Size.Y : 1.777f);
	}

	public override void _Process(double delta)
	{
		// As faixas vão e voltam bem devagar: o menu parece vivo sem distrair.
		_time += (float)delta;
		_material?.SetShaderParameter("drift", Mathf.Sin(_time * 0.12f) * 0.04f);
	}
}
