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

	/// <summary>Aliado: uma borda branca suave, vista também através das
	/// paredes (achar o time é o ponto). Só a borda, sem preencher.</summary>
	public static ShaderMaterial Ally => _ally ??= Tint(Make(true, 0f, 0.26f, 6), new Color(1f, 1f, 1f));

	/// <summary>Inimigo: uma borda vermelha suave e leve, só onde ele está
	/// à vista (através da parede seria entregar posição).</summary>
	public static ShaderMaterial Enemy => _enemy ??= Tint(Make(false, 0f, 0.3f, 6), new Color(1f, 0.22f, 0.2f));

	private static ShaderMaterial Tint(ShaderMaterial material, Color color)
	{
		material.SetShaderParameter("color", color);
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
