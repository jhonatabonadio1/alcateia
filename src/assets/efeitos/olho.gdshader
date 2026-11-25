// O brilho dos olhos do lobo (Jhon, 25/11: "coloque os leds do equipamento
// brilhando bem forte", "e o olho também", "quero o neon desse jeito no game").
//
// O olho do modelo é emissão, mas é pequeno (uma amêndoa de 2,7 cm): mesmo
// forte, o glow do mapa fazia dele um pontinho. Por cima de cada olho vai um
// disco de luz virado para a câmera (scripts/visual/fx/EyeGlow.cs), preso ao
// osso da cabeça: soma luz, passa do branco no miolo (o glow do mapa faz o
// halo) e some perto da pele, para não cortar reto onde encosta na cara.
//
// A cor e o quanto acende são por lobo (instance uniform): o WolfFx põe a cor
// do personagem e sobe junto com o neon do corpo no poder.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

/** A força do miolo (passa de 1: é o que o glow do mapa pega). */
uniform float energy = 4.0;
/** A distância (m) em que o disco some ao encostar na pele. */
uniform float fade_distance = 0.03;
/** Quanto o olho respira, no ritmo do neon do corpo (lobo.gdshader). */
uniform float breath = 0.14;
uniform sampler2D depth_texture : hint_depth_texture, filter_linear;

instance uniform vec4 tint : source_color = vec4(0.35, 0.75, 1.0, 1.0);
instance uniform float level = 1.0;

void vertex() {
	// Virado para a câmera, com a escala do nó (o esqueleto do lobo é em cm).
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(MAIN_CAM_INV_VIEW_MATRIX[0], MAIN_CAM_INV_VIEW_MATRIX[1], MAIN_CAM_INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
		vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
		vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
		vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
		vec4(0.0, 0.0, 0.0, 1.0));
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
	float r = length(UV * 2.0 - 1.0);
	// O miolo quente e o halo largo, que somem antes da borda do disco.
	float core = exp(-r * r * 22.0);
	float halo = exp(-r * r * 4.5) * 0.3;
	float edge = 1.0 - smoothstep(0.8, 1.0, r);

	// Some onde o disco entra na pele (o mesmo cálculo do proximity fade do Godot).
	float depth = textureLod(depth_texture, SCREEN_UV, 0.0).r;
	vec4 behind = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
	behind.xyz /= behind.w;
	float fade = clamp(1.0 - smoothstep(behind.z + fade_distance, behind.z, VERTEX.z), 0.0, 1.0);

	float pulse = 1.0 + breath * sin(TIME * 2.1);
	// O miolo vai para o branco; o halo fica na cor.
	vec3 hot = mix(tint.rgb, vec3(1.0), 0.55);
	ALBEDO = (hot * core * 2.2 + tint.rgb * halo) * energy * level * pulse;
	ALPHA = clamp((core + halo) * edge * fade, 0.0, 1.0);
}
