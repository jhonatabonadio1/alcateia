using Godot;

/// <summary>
/// A silhueta vermelha de quem te matou, como
/// <see cref="GeometryInstance3D.MaterialOverlay"/> — uma segunda passada
/// por cima do material dele, que acompanha o esqueleto. Forte e **sem**
/// teste de profundidade: visto através de tudo, só enquanto você está
/// morto (<see cref="KillCam"/>).
///
/// Até 15/11 havia também uma versão leve, só na borda, em todo inimigo;
/// o Jhon trocou por um brilho no ombro (<see cref="ShoulderMark"/>).
/// </summary>
public static class Silhouette
{
	private static ShaderMaterial _killer;

	public static ShaderMaterial Killer => _killer ??= Make(true, 0.28f, 0.6f, 10);

	private static ShaderMaterial _ally, _enemy;

	/// <summary>Aliado: um contorno branco suave, só em volta do corpo.</summary>
	public static ShaderMaterial Ally => _ally ??= Outline(new Color(1f, 1f, 1f), 0.55f, 7);

	/// <summary>Inimigo: um contorno vermelho suave e leve.</summary>
	public static ShaderMaterial Enemy => _enemy ??= Outline(new Color(1f, 0.22f, 0.2f), 0.6f, 6);

	/// <summary>
	/// O contorno dos times (pedido do Jhon, 19/11: "a borda deve ser só ao
	/// redor dos personagens, não pode sobrepor a minha arma, sem borda no
	/// esqueleto"): a casca do corpo **virada do avesso** e um pouco maior
	/// — só aparece onde passa da beirada dele. Com teste de profundidade:
	/// a arma na mão, as paredes e o próprio corpo ficam por cima, então não
	/// há linha nenhuma por dentro dele nem através de nada. A espessura
	/// cresce com a distância, para a linha ter a mesma largura na tela.
	/// </summary>
	private static ShaderMaterial Outline(Color color, float alpha, int priority)
	{
		var shader = new Shader
		{
			Code = """
				shader_type spatial;
				render_mode unshaded, world_vertex_coords, depth_draw_never, cull_front, blend_mix, shadows_disabled;

				uniform vec4 color : source_color = vec4(1.0);
				uniform float alpha = 0.5;

				void vertex() {
					float far = length(VERTEX - CAMERA_POSITION_WORLD);
					VERTEX += normalize(NORMAL) * clamp(far * 0.0022, 0.006, 0.06);
				}

				void fragment() {
					ALBEDO = color.rgb;
					ALPHA = alpha;
				}
				"""
		};
		var material = new ShaderMaterial { Shader = shader, RenderPriority = priority };
		material.SetShaderParameter("color", color);
		material.SetShaderParameter("alpha", alpha);
		return material;
	}

	private static ShaderMaterial Make(bool throughWalls, float fill, float rim, int priority)
	{
		string depth = throughWalls ? "depth_test_disabled, " : "";
		var shader = new Shader
		{
			Code = $$"""
				shader_type spatial;
				render_mode unshaded, world_vertex_coords, {{depth}}depth_draw_never, cull_back, blend_mix;

				uniform vec4 color : source_color = vec4(1.0, 0.08, 0.05, 1.0);

				// Uma casca 4 mm para fora do corpo. Na mesma superfície,
				// com teste de profundidade, a borda brigava com a pele
				// pelo mesmo pixel e piscava com o boneco andando.
				void vertex() {
					VERTEX += normalize(NORMAL) * 0.004;
				}

				void fragment() {
					float edge = 1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0);
					ALBEDO = color.rgb;
					ALPHA = {{F(fill)}} + {{F(rim)}} * edge * edge;
				}
				"""
		};

		return new ShaderMaterial { Shader = shader, RenderPriority = priority };
	}

	/// <summary>Número com ponto decimal sempre: "0" seria inteiro no
	/// shader, e int + float não compila.</summary>
	private static string F(float value) => value.ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture);
}
