using Godot;
using System.Collections.Generic;

/// <summary>
/// O visual do HUD de combate (pedido do Jhon, 17/11): mais low poly e
/// mais Overwatch.
///
/// - **Vidro**: o fundo dos painéis é o próprio mapa, desfocado e
///   escurecido (<see cref="Glass"/>), em vez de uma caixa preta chapada.
///   O painel pinta o fundo de magenta (<see cref="GlassMark"/>) e o
///   shader troca o magenta pelo mapa borrado; o resto (borda, texto)
///   passa como está.
/// - **Facetado**: cantos cortados a 45° (sem arredondar) e painéis
///   inclinados, como paralelogramos (<see cref="Skew"/>). A borda
///   suavizada continua magenta (só a transparência cai), então o vidro
///   chega liso até a beira inclinada.
/// - **Números inclinados**, grossos e grandes (<see cref="Slanted"/>).
///
/// Os menus por cima da partida (Esc, conjunto, resultado) usam o mapa
/// borrado de fundo (<see cref="Backdrop"/>).
/// </summary>
public static class HudStyle
{
	/// <summary>A cor que o shader de vidro troca pelo mapa borrado.</summary>
	public static readonly Color GlassMark = new(1, 0, 1);

	/// <summary>Inclinação dos painéis: quanto o topo anda para a direita
	/// em relação à base, por pixel de altura.</summary>
	public const float Skew = 0.16f;

	/// <summary>O corte dos cantos, em pixels.</summary>
	public const int Cut = 10;

	/// <summary>O tom do vidro: escuro e frio, como o painel de antes.</summary>
	public static readonly Color Tint = new(0.035f, 0.05f, 0.075f, 0.58f);

	private const string GlassCode = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap, repeat_disable;
uniform float blur = 3.2;
uniform vec4 tint : source_color = vec4(0.035, 0.05, 0.075, 0.58);
void fragment() {
	vec4 c = COLOR;
	float glass = step(0.97, c.r) * step(0.97, c.b) * (1.0 - step(0.03, c.g));
	if (glass > 0.5) {
		vec3 bg = textureLod(screen_tex, SCREEN_UV, blur).rgb;
		// Um brilho fraco no alto, como uma face de vidro inclinada.
		float sheen = 0.05 * (1.0 - clamp(UV.y * 1.6, 0.0, 1.0));
		COLOR = vec4(mix(bg, tint.rgb, tint.a) + sheen, c.a);
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

	/// <summary>O material de vidro, com o tom (um por cor, reaproveitado).</summary>
	public static ShaderMaterial Glass(Color? tint = null)
	{
		Color t = tint ?? Tint;
		if (Glasses.TryGetValue(t, out ShaderMaterial m)) return m;

		_glassShader ??= new Shader { Code = GlassCode };
		m = new ShaderMaterial { Shader = _glassShader };
		m.SetShaderParameter("tint", t);
		Glasses[t] = m;
		return m;
	}

	/// <summary>O fundo dos menus por cima da partida: o mapa borrado,
	/// puxado para <paramref name="color"/> em <paramref name="strength"/>.
	/// Use num ColorRect de cor opaca.</summary>
	public static ShaderMaterial Backdrop(float strength = 0.55f)
	{
		_backdropShader ??= new Shader { Code = BackdropCode };
		var m = new ShaderMaterial { Shader = _backdropShader };
		m.SetShaderParameter("strength", strength);
		return m;
	}

	/// <summary>O desenho de um painel: vidro, dois cantos cortados (o de
	/// cima à esquerda e o de baixo à direita), inclinado, e uma linha de
	/// destaque embaixo se <paramref name="accent"/> tiver cor.</summary>
	public static StyleBoxFlat Plate(Color accent = default, bool skewed = true, float skew = Skew,
		int padH = UiTokens.S3, int padV = UiTokens.S2, int accentWidth = 3)
	{
		var box = new StyleBoxFlat
		{
			BgColor = GlassMark,
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

	/// <summary>Um painel de vidro pronto (sem tema): o material e o
	/// desenho de <see cref="Plate"/>.</summary>
	public static PanelContainer Panel(Color accent = default, bool skewed = true, float skew = Skew, Color? tint = null,
		int padH = UiTokens.S3, int padV = UiTokens.S2)
	{
		var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Material = Glass(tint) };
		panel.AddThemeStyleboxOverride("panel", Plate(accent, skewed, skew, padH, padV));
		return panel;
	}

	/// <summary>Caixa chapada facetada (sem vidro): fichas de tecla,
	/// barras.</summary>
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
		label.AddThemeConstantOverride("outline_size", Mathf.Max(4, size / 10));
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.55f));
		if (color is Color c) label.AddThemeColorOverride("font_color", c);
		return label;
	}

	/// <summary>Barra fina inclinada (recarga, uivo, força do salto).</summary>
	public static void SlantBar(ProgressBar bar, Color fill)
	{
		bar.AddThemeStyleboxOverride("background", Facet(new Color(1, 1, 1, 0.12f), default, 0, 0.9f, 0));
		bar.AddThemeStyleboxOverride("fill", Facet(fill, default, 0, 0.9f, 0));
	}
}
