using Godot;

/// <summary>
/// O brilho das bordas de nível alto (Jhon, 21/11: "coloque brilhos VFX
/// nas bordas de ouro, prata etc., nos níveis mais altos"): um reflexo que
/// varre o metal em diagonal de tempos em tempos e estrelinhas que piscam
/// nos pontos claros. Desenha a própria borda de novo, por cima, com um
/// shader que só soma luz. A força vem de <see cref="Ranks.Shine"/>: nada
/// no Iniciante e no começo do Bronze, cada vez mais (e mais rápido) até o
/// Mestre.
/// </summary>
public partial class FrameShine : Control
{
	private const string Code = @"
shader_type canvas_item;
render_mode blend_add;

// Brilho das bordas de nível alto: um reflexo que varre o metal em diagonal
// e estrelinhas que piscam nos pontos claros. Vai por cima da própria borda
// (mesma textura), somando luz.
uniform float strength = 1.0;                 // 0 nada .. 1 Mestre
uniform vec4 tint : source_color = vec4(1.0);
uniform float seed = 0.0;

float hash(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float lum = dot(tex.rgb, vec3(0.299, 0.587, 0.114));

	// reflexo: uma faixa clara e uma fina atrás, a cada poucos segundos
	float period = mix(5.0, 2.6, strength);
	float ph = fract(TIME / period + seed);
	float d = (UV.x * 0.75 + UV.y * 0.65) - (ph * 2.4 - 0.5);
	float sweep = smoothstep(0.13, 0.0, abs(d)) + 0.5 * smoothstep(0.04, 0.0, abs(d + 0.2));
	vec3 rgb = tint.rgb * sweep * (0.3 + lum * 1.5) * tex.a;

	// estrelinhas: uma por célula, só onde a borda é clara no meio da célula
	float cells = 7.0;
	vec2 g = UV * cells;
	vec2 id = floor(g);
	vec2 pos = (vec2(hash(id + seed), hash(id + seed + 7.3)) - 0.5) * 0.6;
	vec2 at = (id + 0.5 + pos) / cells;
	vec4 under = texture(TEXTURE, at);
	float bright = smoothstep(0.3, 0.6, dot(under.rgb, vec3(0.299, 0.587, 0.114))) * under.a;
	float chosen = step(1.0 - 0.65 * strength, hash(id + seed + 2.1));
	float tw = pow(max(0.0, sin(TIME * (1.6 + hash(id) * 2.2) + hash(id + 5.0) * 6.283)), 4.0);
	vec2 l = (g - id - 0.5 - pos);
	float arms = exp(-abs(l.x) * 30.0) * exp(-abs(l.y) * 4.0) + exp(-abs(l.y) * 30.0) * exp(-abs(l.x) * 4.0);
	float dot_ = exp(-dot(l, l) * 60.0);
	float star = (arms + dot_) * tw * bright * chosen;

	COLOR = vec4((rgb + mix(tint.rgb, vec3(1.0), 0.6) * star * 2.2) * (0.35 + 0.65 * strength), 1.0);
}
";

	private static Shader _shader;
	private readonly ShaderMaterial _material;
	private int _rank = Ranks.None;

	/// <summary>O lado da borda, em px. Zero: o do retrato (<see cref="RankBadge"/>).</summary>
	public float Side { get; set; }

	public int Rank
	{
		get => _rank;
		set
		{
			_rank = value < 0 ? Ranks.None : Ranks.Clamp(value);
			float strength = _rank < 0 ? 0f : Ranks.Shine(_rank);
			Visible = strength > 0f;
			if (!Visible) return;
			_material.SetShaderParameter("strength", strength);
			_material.SetShaderParameter("tint", Ranks.ColorOf(_rank).Lightened(0.55f));
			QueueRedraw();
		}
	}

	public FrameShine()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_shader ??= new Shader { Code = Code };
		_material = new ShaderMaterial { Shader = _shader };
		_material.SetShaderParameter("seed", GD.Randf() * 10f);   // cada borda brilha na sua hora
		Material = _material;
		Visible = false;
	}

	public override void _Draw()
	{
		if (_rank < 0 || RankBadge.FrameOf(_rank) is not Texture2D frame) return;
		float side = Side > 0f ? Side : Mathf.Min(Size.X, Size.Y) * RankBadge.Grow;
		DrawTextureRect(frame, new Rect2(Size / 2f - new Vector2(side, side) / 2f, new Vector2(side, side)), false);
	}
}
