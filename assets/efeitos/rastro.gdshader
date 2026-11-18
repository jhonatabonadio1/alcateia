// As garras em neon (scripts/visual/fx/ClawTrail.cs e NeonSlash.cs): três
// fios de luz ao longo da faixa — miolo quase branco, clarão na cor do
// lobo —, mais grossos perto da garra e afinando no rabo, com as pontas
// desencontradas (pedido do Jhon, 18/11: o neon polido, nas garras).
// UV.x: 0 no rabo do rastro, 1 na garra. UV.y: 0 na base, 1 na ponta da
// garra. A cor do vértice traz a idade (alfa) e a força do golpe.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

instance uniform vec4 trail_color : source_color = vec4(1.0, 0.8, 0.3, 1.0);
uniform float intensity = 2.6;

void fragment() {
	float head = UV.x;
	float thick = mix(0.035, 0.085, head);
	float glow = 0.0;
	float core = 0.0;
	for (int i = 0; i < 3; i++) {
		float at = 0.5 + 0.22 * float(i);
		float d = abs(UV.y - at) / thick;
		// Os de dentro nascem mais tarde: as três pontas não acabam juntas.
		float grown = smoothstep(0.0, 0.22 + 0.13 * float(2 - i), head);
		glow += exp(-d * d * 1.5) * grown;
		core += exp(-d * d * 9.0) * grown;
	}
	glow = clamp(glow, 0.0, 1.0);
	core = clamp(core, 0.0, 1.0);
	// Mistura (e não soma): aparece também contra fundo claro. A cor
	// passa de 1 (HDR): com o glow do mapa, brilha.
	ALBEDO = mix(trail_color.rgb * intensity, vec3(intensity + 1.4), core);
	ALPHA = clamp(COLOR.a * (glow * 0.7 + core), 0.0, 1.0);
}
