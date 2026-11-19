using Godot;

/// <summary>
/// As bordas dos corpos (pedidos do Jhon, 18 e 19/11): **só a borda**, sem
/// pintar o corpo por dentro e sem linhas por dentro dele, e nunca por
/// cima da arma na mão.
///
/// Como: duas passadas por cima da malha (<see cref="BodyOverlay"/>).
/// A primeira (<see cref="Mask"/>) não pinta nada; só marca no *stencil*
/// os pixels do corpo, através de tudo. A segunda é a casca do corpo
/// virada do avesso e um pouco maior, que só pinta onde **não** está
/// marcado — sobra a beirada. A arma na mão marca o stencil também
/// (<see cref="ViewModel"/>), então nenhuma borda passa por cima dela.
///
/// - <see cref="Ally"/>: branca, vista em qualquer parte do mapa.
/// - <see cref="Enemy"/>: vermelha suave, só onde ele está à vista.
/// - <see cref="Killer"/>: vermelha forte, através de tudo, de quem te
///   matou, enquanto você está morto (<see cref="KillCam"/>).
/// </summary>
public static class Silhouette
{
	private const int StencilRef = 1;

	private static ShaderMaterial _mask, _killer, _ally, _enemy;

	/// <summary>Só marca o stencil (o corpo, a arma na mão): onde está
	/// marcado, borda nenhuma pinta.</summary>
	public static ShaderMaterial Mask => _mask ??= MakeMask();

	public static ShaderMaterial Killer => _killer ??= Edge(new Color(1f, 0.12f, 0.08f), 0.95f, throughWalls: true, width: 1.4f);

	public static ShaderMaterial Ally => _ally ??= Edge(new Color(1f, 1f, 1f), 0.8f, throughWalls: true, width: 1f);

	public static ShaderMaterial Enemy => _enemy ??= Edge(new Color(1f, 0.22f, 0.2f), 0.75f, throughWalls: false, width: 1f);

	private static ShaderMaterial MakeMask()
	{
		var shader = new Shader
		{
			Code = $$"""
				shader_type spatial;
				render_mode unshaded, depth_test_disabled, depth_draw_never, cull_back, blend_mix, shadows_disabled;
				stencil_mode write, compare_always, {{StencilRef}};

				void fragment() {
					ALBEDO = vec3(0.0);
					ALPHA = 0.0;
				}
				"""
		};
		// Antes de qualquer borda.
		return new ShaderMaterial { Shader = shader, RenderPriority = -40 };
	}

	/// <summary>A marca do corpo seguida da casca de fora. A espessura
	/// cresce com a distância, para a linha ter a mesma largura na tela.</summary>
	private static ShaderMaterial Edge(Color color, float alpha, bool throughWalls, float width)
	{
		string depth = throughWalls ? "depth_test_disabled, " : "";
		var shader = new Shader
		{
			Code = $$"""
				shader_type spatial;
				render_mode unshaded, world_vertex_coords, {{depth}}depth_draw_never, cull_front, blend_mix, shadows_disabled;
				stencil_mode read, compare_not_equal, {{StencilRef}};

				uniform vec4 color : source_color = vec4(1.0);
				uniform float alpha = 0.8;
				uniform float width = 1.0;

				void vertex() {
					float far = length(VERTEX - CAMERA_POSITION_WORLD);
					VERTEX += normalize(NORMAL) * clamp(far * 0.0024 * width, 0.005, 0.15);
				}

				void fragment() {
					ALBEDO = color.rgb;
					ALPHA = alpha;
				}
				"""
		};
		var edge = new ShaderMaterial { Shader = shader, RenderPriority = 30 };
		edge.SetShaderParameter("color", color);
		edge.SetShaderParameter("alpha", alpha);
		edge.SetShaderParameter("width", width);

		var mask = new ShaderMaterial { Shader = Mask.Shader, RenderPriority = Mask.RenderPriority, NextPass = edge };
		return mask;
	}
}
