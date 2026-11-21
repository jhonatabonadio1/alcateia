using Godot;

/// <summary>
/// A chama azul do "em chamas" (rage, <see cref="Rage"/>) em volta de um
/// retrato, no TAB e no HUD, como no Overwatch (Jhon, 21/11: "é uma chama
/// mesmo, pegando fogo").
///
/// Um shader de fogo (ruído subindo, três labaredas, faíscas e um clarão),
/// somado à cena, em duas camadas: a de trás, atrás do retrato, e a da
/// frente, bem leve, só com as pontas que passam por cima da borda.
/// <see cref="Intensity"/> vai de 0 a 1 e acompanha o calor: a chama
/// encolhe enquanto o rage esfria.
/// </summary>
public partial class FireAura : Control
{
	/// <summary>O retângulo da chama, em raios do retrato, e onde fica o
	/// centro do retrato nele (a chama sobe: sobra mais em cima).</summary>
	private const float Wide = 4.6f, Tall = 6.4f, CenterY = 0.66f;

	private const string Code = @"
shader_type canvas_item;
render_mode blend_add;

// Chama azul subindo em volta de um retrato (o 'em chamas').
// O retângulo é mais alto que largo; 'center' é o centro do retrato em UV
// e 'radius' o raio dele em fração da altura do retângulo.
uniform vec2 center = vec2(0.5, 0.66);
uniform float radius = 0.2;
uniform float aspect = 0.8;          // largura / altura do retângulo
uniform float intensity = 1.0;       // 0 apagado .. 1 no máximo
uniform float seed = 0.0;

float hash(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 4; i++) { v += a * noise(p); p = p * 2.03 + vec2(17.1, 9.2); a *= 0.5; }
	return v;
}

uniform float front = 0.0;           // 1: a camada da frente (só as pontas, por cima do retrato)

// Uma labareda: nasce em volta de (cx, 0), sobe até 'tall' e afina.
float lick(vec2 p, float cx, float tall, float wide, float t, float s) {
	float h = -p.y;
	float sway = (fbm(vec2(p.x * 1.1 + s, h * 0.8 - t * 1.3)) - 0.5) * 1.2 * smoothstep(-0.4, 2.2, h);
	float x = p.x - cx + sway;
	float w = mix(wide, 0.08, pow(smoothstep(0.0, tall, h), 0.75));
	// embaixo, fecha em arco (abraça o retrato); em cima, afina
	float body = h < 0.0 ? 1.0 - length(vec2(x, h * 1.22)) / wide : 1.0 - abs(x) / w;
	float f1 = fbm(vec2(x * 2.0 + s, h * 1.2 - t * 2.6));
	float f2 = fbm(vec2(x * 4.6 + 3.0 + s, h * 2.3 - t * 4.4));
	float v = body * 1.1 + (f1 - 0.5) * 1.25 + (f2 - 0.5) * 0.4;
	v -= smoothstep(tall * 0.25, tall, h) * 0.6;        // rasga as pontas
	return v * (1.0 - smoothstep(tall * 0.8, tall * 1.05, h));
}

void fragment() {
	vec2 p = (UV - center) * vec2(aspect, 1.0) / radius;   // em raios do retrato
	float h = -p.y;
	float t = TIME * 1.15 + seed;
	float k = 0.5 + 0.5 * intensity;                       // a chama encolhe ao esfriar

	// três labaredas: a do meio alta, duas menores dos lados
	float flame = lick(p, 0.0, 3.0 * k, 1.55, t, seed);
	flame = max(flame, lick(p, -0.95, 1.9 * k, 0.95, t * 1.13, seed + 4.0));
	flame = max(flame, lick(p, 0.9, 2.2 * k, 1.0, t * 0.91, seed + 9.0));

	float v = clamp(flame, 0.0, 1.0);
	vec3 outer = vec3(0.03, 0.2, 1.0);
	vec3 mid = vec3(0.14, 0.6, 1.0);
	vec3 core = vec3(0.86, 0.97, 1.0);
	vec3 col = mix(outer, mid, smoothstep(0.12, 0.45, v));
	col = mix(col, core, smoothstep(0.55, 0.95, v));
	float a = smoothstep(0.03, 0.26, v);

	// faíscas subindo
	vec2 g = vec2(p.x * 3.2, h * 3.2 - t * 3.4);
	vec2 cell = floor(g);
	float r = hash(cell + seed);
	vec2 off = vec2(hash(cell + 3.7), hash(cell + 9.1)) - 0.5;
	float spark = step(0.88, r) * smoothstep(0.17, 0.0, length(fract(g) - 0.5 - off * 0.5));
	spark *= smoothstep(-0.2, 0.8, h) * (1.0 - smoothstep(2.6 * k, 3.6 * k, h)) * step(abs(p.x), 1.6);

	// clarão em volta
	vec2 gq = p * vec2(0.72, 0.6) + vec2(0.0, 0.15);
	float glow = exp(-dot(gq, gq)) * 0.32;

	vec3 rgb = col * a + vec3(0.55, 0.85, 1.0) * spark + vec3(0.08, 0.3, 1.0) * glow;
	// a camada da frente: só o que passa da borda de cima, bem leve
	float layer = mix(1.0, smoothstep(0.1, 1.1, h) * 0.3, front);
	vec2 e = smoothstep(vec2(0.0), vec2(0.06), UV) * smoothstep(vec2(0.0), vec2(0.06), 1.0 - UV);
	COLOR = vec4(rgb * intensity * layer * e.x * e.y, 1.0);
}
";

	private static Shader _shader;

	private readonly ColorRect _back, _front;
	private readonly ShaderMaterial _backMaterial, _frontMaterial;
	private float _radius = 40f;
	private float _intensity;

	/// <summary>Raio do retrato que a chama abraça, em px.</summary>
	public float Radius
	{
		get => _radius;
		set { _radius = value; Layout(); }
	}

	public float Intensity
	{
		get => _intensity;
		set
		{
			value = Mathf.Clamp(value, 0f, 1f);
			bool on = value > 0.01f;
			if (on != _back.Visible) _back.Visible = _front.Visible = on;
			if (Mathf.IsEqualApprox(value, _intensity)) return;
			_intensity = value;
			_backMaterial.SetShaderParameter("intensity", value);
			_frontMaterial.SetShaderParameter("intensity", value);
		}
	}

	public FireAura()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		// sem tamanho, no centro de quem hospeda
		AnchorLeft = AnchorRight = AnchorTop = AnchorBottom = 0.5f;

		_shader ??= new Shader { Code = Code };
		float seed = GD.Randf() * 20f;   // cada chama dança no seu ritmo
		_backMaterial = Make(seed, front: false);
		_frontMaterial = Make(seed, front: true);

		_back = new ColorRect { Material = _backMaterial, ShowBehindParent = true, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
		_back.AnchorLeft = _back.AnchorRight = _back.AnchorTop = _back.AnchorBottom = 0.5f;
		_front = new ColorRect { Material = _frontMaterial, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
		AddChild(_front);
		Layout();
	}

	private static ShaderMaterial Make(float seed, bool front)
	{
		var material = new ShaderMaterial { Shader = _shader };
		material.SetShaderParameter("center", new Vector2(0.5f, CenterY));
		material.SetShaderParameter("radius", 1f / Tall);
		material.SetShaderParameter("aspect", Wide / Tall);
		material.SetShaderParameter("intensity", 0f);
		material.SetShaderParameter("seed", seed);
		material.SetShaderParameter("front", front ? 1f : 0f);
		return material;
	}

	/// <summary>Põe a chama em <paramref name="host"/>, centrada nele: a
	/// camada de trás atrás dele, a da frente por cima.</summary>
	public static FireAura On(Control host, float radius)
	{
		var aura = new FireAura { Radius = radius };
		host.AddChild(aura._back);
		host.AddChild(aura);
		return aura;
	}

	private void Layout()
	{
		float w = _radius * Wide, h = _radius * Tall;
		// a de trás é filha de quem hospeda, ancorada no centro dele
		_back.OffsetLeft = -w / 2f;
		_back.OffsetRight = w / 2f;
		_back.OffsetTop = -h * CenterY;
		_back.OffsetBottom = h * (1f - CenterY);
		// a da frente é filha daqui (um ponto no centro)
		_front.Position = new Vector2(-w / 2f, -h * CenterY);
		_front.Size = new Vector2(w, h);
	}
}
