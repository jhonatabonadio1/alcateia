using Godot;

/// <summary>
/// As duas marcas vermelhas sobre um corpo, como
/// <see cref="GeometryInstance3D.MaterialOverlay"/> — uma segunda passada
/// por cima do material dele, que acompanha o esqueleto.
///
/// - <see cref="Enemy"/>: leve, só na borda, **com** teste de
///   profundidade. Separa inimigo de aliado à primeira vista (decisão do
///   Jhon) e não atravessa parede — através de parede seria ver quem está
///   escondido.
/// - <see cref="Killer"/>: forte e **sem** teste de profundidade: quem te
///   matou, visto através de tudo, só enquanto você está morto
///   (<see cref="KillCam"/>).
/// </summary>
public static class Silhouette
{
	private static ShaderMaterial _enemy;
	private static ShaderMaterial _killer;

	public static ShaderMaterial Enemy => _enemy ??= Make(false, 0.0f, 0.38f, 5);
	public static ShaderMaterial Killer => _killer ??= Make(true, 0.28f, 0.6f, 10);

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
