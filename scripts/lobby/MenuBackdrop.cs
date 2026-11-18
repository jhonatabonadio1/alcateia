using Godot;

/// <summary>
/// O fundo dos menus (pedido do Jhon, 17/11): claro, como o de Overwatch,
/// com o corte de Valorant — um degradê frio quase branco, um mosaico de
/// triângulos bem fraco (o low poly), duas faixas largas na diagonal um
/// pouco mais escuras e um fio laranja cruzando a tela. Tudo desenhado
/// por shader, sem imagem.
///
/// O conteúdo das telas fica num palco escuro por cima (LobbyShell): o
/// texto do jogo é claro, e direto no fundo claro ele sumiria.
/// </summary>
public partial class MenuBackdrop : Control
{
	/// <summary>Cor do alto, à esquerda.</summary>
	[Export] public Color Top = new("eef2f7");

	/// <summary>Cor de baixo, à direita.</summary>
	[Export] public Color Bottom = new("c9d3df");

	/// <summary>O fio da diagonal.</summary>
	[Export] public Color Accent = UiTokens.TeamOrange;

	/// <summary>Força do mosaico de triângulos.</summary>
	[Export] public float Facets = 0.035f;

	private const string Code = @"
shader_type canvas_item;
uniform vec4 top_color : source_color = vec4(0.933, 0.949, 0.969, 1.0);
uniform vec4 bottom_color : source_color = vec4(0.788, 0.827, 0.875, 1.0);
uniform vec4 accent : source_color = vec4(1.0, 0.478, 0.184, 1.0);
uniform float facet = 0.035;
uniform float cell = 140.0;
uniform float aspect = 1.777;
uniform float drift = 0.0;
void fragment() {
	vec2 uv = UV;
	vec3 col = mix(top_color.rgb, bottom_color.rgb, clamp(uv.y * 0.75 + uv.x * 0.35, 0.0, 1.0));

	// Mosaico: quadrados cortados na diagonal, cada triângulo um pouco
	// mais claro ou escuro.
	vec2 p = FRAGCOORD.xy / cell;
	vec2 id = floor(p);
	vec2 f = fract(p);
	float half_ = step(f.x, f.y);
	float h = fract(sin(dot(id + half_ * vec2(17.0, 31.0), vec2(12.9898, 78.233))) * 43758.5453);
	col += (h - 0.5) * facet;

	// Faixas diagonais (o corte de Valorant): duas largas, levemente mais
	// escuras, e o fio laranja entre elas. Andam devagar.
	float d = uv.x * aspect * 0.55 - uv.y + drift;
	float slab = smoothstep(0.58, 0.585, d) * (1.0 - smoothstep(0.86, 0.865, d));
	float slab2 = smoothstep(1.02, 1.025, d) * (1.0 - smoothstep(1.12, 1.125, d));
	col *= 1.0 - 0.06 * slab - 0.04 * slab2;
	float wire = smoothstep(0.935, 0.937, d) * (1.0 - smoothstep(0.943, 0.945, d));
	col = mix(col, accent.rgb, wire * 0.9);

	// Vinheta leve nas bordas.
	vec2 q = uv - 0.5;
	col *= 1.0 - dot(q, q) * 0.25;
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
