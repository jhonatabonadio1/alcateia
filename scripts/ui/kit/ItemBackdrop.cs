using Godot;

/// <summary>
/// O fundo da vitrine de um item (cartão da loja e do inventário, painel de
/// detalhe): escuro, com um clarão na cor da raridade atrás da peça, feixes
/// de luz em diagonal, uma linha de "palco" embaixo, vinheta e, do épico
/// para cima, faíscas piscando. Pedido do Jhon (18/11): "mais premium,
/// precisa cativar o usuário a comprar".
///
/// É um shader só (um retângulo por cartão); nada de textura. A força
/// (<see cref="Power"/>) vem da raridade: comum quase só o fundo escuro,
/// lendário com tudo.
/// </summary>
public partial class ItemBackdrop : ColorRect
{
	private const string Code = @"
shader_type canvas_item;

uniform vec4 tint : source_color = vec4(0.64, 0.42, 1.0, 1.0);
uniform float power = 1.0;
uniform vec2 size = vec2(296.0, 116.0);
uniform float radius = 3.0;
uniform float seed = 0.0;
uniform float dim = 0.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7)) + seed) * 43758.5453);
}

void fragment() {
	vec2 px = UV * size;
	vec2 c = (UV - vec2(0.5, 0.54)) * vec2(size.x / size.y, 1.0);

	// Fundo: quase preto, um fio da cor da raridade, mais escuro embaixo.
	vec3 base = mix(vec3(0.050, 0.055, 0.085), vec3(0.018, 0.020, 0.032), UV.y);
	base = mix(base, tint.rgb * 0.16, 0.35 * power);

	// Clarão atrás da peça: uma elipse deitada.
	float d = length(c * vec2(0.62, 1.25));
	float glow = exp(-d * d * 5.5);
	vec3 col = base + tint.rgb * glow * (0.20 + 0.42 * power);
	// Miolo mais claro, quase branco na cor.
	col += mix(tint.rgb, vec3(1.0), 0.45) * exp(-d * d * 26.0) * 0.16 * power;

	// Feixes em diagonal, andando devagar.
	float along = c.x * 0.82 + c.y * 0.57;
	float rays = 0.5 + 0.5 * sin(along * 9.0 + TIME * 0.35 + seed);
	rays *= 0.5 + 0.5 * sin(along * 23.0 - TIME * 0.22 + seed * 2.0);
	col += tint.rgb * rays * glow * 0.20 * power;

	// Trama fina: linhas em diagonal, quase invisíveis.
	float weave = step(0.5, fract((px.x + px.y) / 7.0));
	col += vec3(weave) * 0.012;

	// O palco: uma linha clara embaixo, com reflexo.
	float stage = 0.86;
	float line = exp(-pow((UV.y - stage) * size.y / 1.2, 2.0));
	float span = smoothstep(0.52, 0.05, abs(UV.x - 0.5));
	col += mix(tint.rgb, vec3(1.0), 0.35) * line * span * (0.25 + 0.5 * power);
	col *= UV.y > stage ? 0.72 : 1.0;
	col += tint.rgb * smoothstep(0.16, 0.0, UV.y - stage) * step(stage, UV.y) * span * 0.10 * power;

	// Faíscas: do épico para cima.
	if (power > 0.75) {
		vec2 cell = floor(px / 14.0);
		float r = hash(cell);
		if (r > 0.93) {
			vec2 at = (cell + vec2(hash(cell + 3.1), hash(cell + 7.7)) * 0.8 + 0.1) * 14.0;
			float twinkle = pow(0.5 + 0.5 * sin(TIME * (1.2 + r * 3.0) + r * 40.0), 6.0);
			float spark = exp(-dot(px - at, px - at) / 1.6);
			col += mix(tint.rgb, vec3(1.0), 0.6) * spark * twinkle * 0.9 * glow;
		}
	}

	// Vinheta.
	vec2 v = UV - 0.5;
	col *= 1.0 - dot(v, v) * 1.25;

	// Bloqueado ou expirado: apaga.
	float grey = dot(col, vec3(0.299, 0.587, 0.114));
	col = mix(col, vec3(grey) * 0.75, dim);

	// Cantos de cima arredondados, para caber na moldura do cartão.
	vec2 q = abs(px - size * 0.5) - (size * 0.5 - vec2(radius));
	float corner = length(max(q, vec2(0.0))) - radius;
	float alpha = 1.0 - smoothstep(-0.5, 0.5, corner);

	COLOR = vec4(col, alpha);
}";

	private static Shader _shader;

	/// <summary>Quanto do efeito aparece, por raridade (0 comum … 4 lendário).</summary>
	public static float PowerOf(int rarity) => Mathf.Clamp(rarity, 0, 4) switch
	{
		0 => 0.25f,
		1 => 0.4f,
		2 => 0.6f,
		3 => 0.9f,
		_ => 1f
	};

	public static ItemBackdrop Make(Color tint, int rarity, bool dim = false, string seedFrom = "")
	{
		_shader ??= new Shader { Code = Code };
		var material = new ShaderMaterial { Shader = _shader };
		material.SetShaderParameter("tint", tint);
		material.SetShaderParameter("power", PowerOf(rarity));
		material.SetShaderParameter("seed", (seedFrom ?? "").Hash() % 1000 / 37f);
		material.SetShaderParameter("dim", dim ? 1f : 0f);

		var backdrop = new ItemBackdrop
		{
			Color = Colors.White,
			Material = material,
			MouseFilter = MouseFilterEnum.Ignore
		};
		backdrop.Resized += backdrop.Fit;
		return backdrop;
	}

	/// <summary>Raio dos cantos, em pixels (o da moldura menos a borda).</summary>
	public float Radius { get; set; } = 3f;

	public override void _Ready() => Fit();

	private void Fit()
	{
		if (Material is not ShaderMaterial material || Size.X <= 0 || Size.Y <= 0) return;
		material.SetShaderParameter("size", Size);
		material.SetShaderParameter("radius", Radius);
	}
}
