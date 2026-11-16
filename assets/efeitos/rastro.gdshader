// O feixe de luz que a garra deixa no golpe (scripts/visual/fx/ClawTrail.cs).
// UV.y: 0 na base da garra, 1 na ponta: mais forte na ponta. A cor do
// vértice traz a idade (alfa) e a força do golpe naquele ponto.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

instance uniform vec4 trail_color : source_color = vec4(1.0, 0.8, 0.3, 1.0);
uniform float intensity = 1.6;

void fragment() {
	float tipward = pow(UV.y, 1.2);
	// Mistura (e não soma): o feixe aparece também contra fundo claro.
	// A cor passa de 1 (HDR): com o glow do mapa, ele brilha.
	ALBEDO = mix(trail_color.rgb, vec3(1.0), 0.35 * tipward) * intensity;
	ALPHA = clamp(COLOR.a * tipward * 0.9, 0.0, 1.0);
}
