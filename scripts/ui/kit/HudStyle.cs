using Godot;
using System.Collections.Generic;

/// <summary>
/// O visual do HUD de combate e o vidro dos menus (pedidos do Jhon, 17/11).
///
/// - **HUD sem fundo**: em primeira pessoa nada de painel atrás da vida,
///   da munição e do especial — número grosso com contorno direto sobre
///   o jogo, como em Overwatch. Só o que é lista (feed de abates, placar)
///   leva uma placa chapada e translúcida (<see cref="Plate"/>), sem
///   borrar nada.
/// - **Facetado**: cantos cortados a 45° (sem arredondar) e placas
///   inclinadas, como paralelogramos (<see cref="Skew"/>).
/// - **Números inclinados**, grossos e grandes (<see cref="Slanted"/>).
/// - **Vidro fosco** (<see cref="Glass"/>): o que está atrás, borrado e
///   puxado para um tom. É o fundo dos cartões dos menus (claro, o
///   LobbyShell aplica). O painel pinta o fundo de magenta
///   (<see cref="GlassMark"/>) e o shader troca o magenta pelo fundo
///   borrado; o resto (borda, sombra, texto) passa como está.
///
/// Os menus por cima da partida (Esc, conjunto, resultado) usam o mapa
/// borrado de fundo (<see cref="Backdrop"/>).
/// </summary>
public static class HudStyle
{
	/// <summary>A cor que o shader de vidro troca pelo fundo borrado.</summary>
	public static readonly Color GlassMark = new(1, 0, 1);

	/// <summary>Inclinação das placas: quanto o topo anda para a direita
	/// em relação à base, por pixel de altura.</summary>
	public const float Skew = 0.16f;

	/// <summary>O corte dos cantos, em pixels.</summary>
	public const int Cut = 10;

	/// <summary>O tom das placas chapadas do HUD: escuro, frio, meio
	/// transparente.</summary>
	public static readonly Color Shade = new(0.03f, 0.04f, 0.06f, 0.5f);

	/// <summary>O tom do vidro fosco dos menus claros.</summary>
	public static readonly Color Frost = new(1f, 1f, 1f, 0.5f);

	// Quanto de magenta há no pixel (t) é quanto de vidro ele leva: na
	// beira suavizada entre a borda e o fundo a cor vem misturada, e um
	// teste de "é magenta ou não" deixava um fio rosa nos cantos cortados.
	// O borrão soma nove amostras de um mip baixo, para não aparecer o
	// quadriculado do mip; o grão é o que deixa fosco.
	private const string GlassCode = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap, repeat_disable;
uniform float blur = 3.5;
uniform float grain = 0.02;
uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 0.5);
void fragment() {
	vec4 c = COLOR;
	float t = clamp(min(c.r, c.b) - c.g, 0.0, 1.0);
	if (t > 0.01) {
		vec2 px = SCREEN_PIXEL_SIZE * exp2(blur);
		vec3 bg = textureLod(screen_tex, SCREEN_UV, blur).rgb * 0.2;
		bg += textureLod(screen_tex, SCREEN_UV + px * vec2(1.0, 0.0), blur).rgb * 0.125;
		bg += textureLod(screen_tex, SCREEN_UV + px * vec2(-1.0, 0.0), blur).rgb * 0.125;
		bg += textureLod(screen_tex, SCREEN_UV + px * vec2(0.0, 1.0), blur).rgb * 0.125;
		bg += textureLod(screen_tex, SCREEN_UV + px * vec2(0.0, -1.0), blur).rgb * 0.125;
		bg += textureLod(screen_tex, SCREEN_UV + px * vec2(0.7, 0.7), blur).rgb * 0.075;
		bg += textureLod(screen_tex, SCREEN_UV + px * vec2(-0.7, 0.7), blur).rgb * 0.075;
		bg += textureLod(screen_tex, SCREEN_UV + px * vec2(0.7, -0.7), blur).rgb * 0.075;
		bg += textureLod(screen_tex, SCREEN_UV + px * vec2(-0.7, -0.7), blur).rgb * 0.075;
		float n = fract(sin(dot(floor(FRAGCOORD.xy), vec2(12.9898, 78.233))) * 43758.5453) - 0.5;
		// Um brilho fraco no alto, como uma face de vidro inclinada.
		float sheen = 0.04 * (1.0 - clamp(UV.y * 1.6, 0.0, 1.0));
		vec3 glass = mix(bg, tint.rgb, tint.a) + sheen + n * grain;
		// O que não é magenta (a borda, na beira) fica como veio.
		vec3 rest = clamp((c.rgb - t * vec3(1.0, 0.0, 1.0)) / max(1.0 - t, 0.001), 0.0, 1.0);
		COLOR = vec4(mix(rest, glass, t), c.a);
	}
}";

	private const string BackdropCode = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap, repeat_disable;
uniform float blur = 4.5;
uniform float strength = 0.55;
void fragment() {
	vec3 bg = textureLod(screen_tex, SCREEN_UV, blur).rgb;
	COLOR = vec4(mix(bg, COLOR.rgb, strength), COLOR.a);
}";

	private static Shader _glassShader;
	private static Shader _backdropShader;
	private static readonly Dictionary<Color, ShaderMaterial> Glasses = new();

	/// <summary>O material de vidro fosco, com o tom (um por cor,
	/// reaproveitado). Quem usa pinta o fundo com <see cref="GlassMark"/>.</summary>
	public static ShaderMaterial Glass(Color? tint = null)
	{
		Color t = tint ?? Frost;
		if (Glasses.TryGetValue(t, out ShaderMaterial m)) return m;

		_glassShader ??= new Shader { Code = GlassCode };
		m = new ShaderMaterial { Shader = _glassShader };
		m.SetShaderParameter("tint", t);
		Glasses[t] = m;
		return m;
	}

	/// <summary>O fundo dos menus por cima da partida: o mapa borrado,
	/// puxado para a cor do ColorRect em <paramref name="strength"/>.
	/// Use num ColorRect de cor opaca.</summary>
	public static ShaderMaterial Backdrop(float strength = 0.55f)
	{
		_backdropShader ??= new Shader { Code = BackdropCode };
		var m = new ShaderMaterial { Shader = _backdropShader };
		m.SetShaderParameter("strength", strength);
		return m;
	}

	/// <summary>O desenho de uma placa do HUD: chapada e translúcida
	/// (<paramref name="bg"/>, por padrão <see cref="Shade"/>), dois cantos
	/// cortados (o de cima à esquerda e o de baixo à direita), inclinada,
	/// e uma linha de destaque embaixo se <paramref name="accent"/> tiver
	/// cor.</summary>
	public static StyleBoxFlat Plate(Color accent = default, bool skewed = true, float skew = Skew,
		int padH = UiTokens.S3, int padV = UiTokens.S2, int accentWidth = 3, Color? bg = null)
	{
		var box = new StyleBoxFlat
		{
			BgColor = bg ?? Shade,
			BorderColor = accent,
			CornerDetail = 1,
			AntiAliasing = true,
			ContentMarginLeft = padH,
			ContentMarginRight = padH,
			ContentMarginTop = padV,
			ContentMarginBottom = padV
		};
		box.CornerRadiusTopLeft = Cut;
		box.CornerRadiusBottomRight = Cut;
		if (accent.A > 0) box.BorderWidthBottom = accentWidth;
		if (skewed) box.Skew = new Vector2(skew, 0);
		return box;
	}

	/// <summary>Uma placa pronta (sem tema): o desenho de
	/// <see cref="Plate"/>, com o tom <paramref name="tint"/>.</summary>
	public static PanelContainer Panel(Color accent = default, bool skewed = true, float skew = Skew, Color? tint = null,
		int padH = UiTokens.S3, int padV = UiTokens.S2)
	{
		var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		panel.AddThemeStyleboxOverride("panel", Plate(accent, skewed, skew, padH, padV, bg: tint));
		return panel;
	}

	/// <summary>Caixa chapada facetada: fichas de tecla, barras.</summary>
	public static StyleBoxFlat Facet(Color bg, Color border = default, int borderWidth = 0, float skew = Skew, int cut = 5,
		int padH = 0, int padV = 0)
	{
		var box = new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = border,
			CornerDetail = 1,
			AntiAliasing = true,
			ContentMarginLeft = padH,
			ContentMarginRight = padH,
			ContentMarginTop = padV,
			ContentMarginBottom = padV,
			Skew = new Vector2(skew, 0)
		};
		box.SetBorderWidthAll(borderWidth);
		box.CornerRadiusTopLeft = cut;
		box.CornerRadiusBottomRight = cut;
		return box;
	}

	private static readonly Dictionary<Font, FontVariation> Slants = new();

	/// <summary>A fonte inclinada (itálico de mentira, por transformação; a
	/// forma da documentação do FontVariation: x' = x + 0,2·y):
	/// números e títulos do HUD.</summary>
	public static Font Slanted(Font font)
	{
		if (Slants.TryGetValue(font, out FontVariation v)) return v;
		v = new FontVariation { BaseFont = font, VariationTransform = new Transform2D(1f, 0.2f, 0f, 1f, 0f, 0f) };
		Slants[font] = v;
		return v;
	}

	/// <summary>Deixa um rótulo no estilo dos números do HUD: inclinado,
	/// grosso, com contorno escuro.</summary>
	public static T Number<T>(T label, int size, Color? color = null) where T : Label
	{
		label.AddThemeFontOverride("font", Slanted(UiFonts.Display));
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeConstantOverride("outline_size", Mathf.Max(4, size / 8));
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
		if (color is Color c) label.AddThemeColorOverride("font_color", c);
		return label;
	}

	/// <summary>Contorno escuro no texto solto sobre o mundo — sem painel
	/// atrás, texto claro some em parede clara.</summary>
	public static T Outlined<T>(T label, int size = 5) where T : Label
	{
		label.AddThemeConstantOverride("outline_size", size);
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.75f));
		return label;
	}

	/// <summary>Barra fina inclinada (recarga, uivo, força do salto).</summary>
	public static void SlantBar(ProgressBar bar, Color fill)
	{
		bar.AddThemeStyleboxOverride("background", Facet(new Color(0, 0, 0, 0.45f), new Color(1, 1, 1, 0.16f), 1, 0.9f, 0));
		bar.AddThemeStyleboxOverride("fill", Facet(fill, default, 0, 0.9f, 0));
	}
}
