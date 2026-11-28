// A água caindo da fonte: uma cortina em volta do pilar, em fios que afinam e se partem em gotas conforme
// descem (src/scripts/visual/fx/WaterFeature.cs). O UV.y vai de 0 (em cima) a 1 (na bacia); o UV.x dá a volta.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, specular_schlick_ggx, shadows_disabled;

uniform float fios = 8.0;
uniform float velocidade = 1.5;
uniform vec3 cor : source_color = vec3(0.66, 0.88, 0.94);
uniform sampler2D manchas : repeat_enable, filter_linear_mipmap;

void fragment() {
	float t = TIME;
	float no_fio = abs(fract(UV.x * fios) - 0.5) * 2.0;            // 0 no meio de cada fio
	float largura = mix(0.46, 0.2, UV.y);                           // a água acelera e o fio afina
	// (a queda acelera: o desenho corre mais rápido embaixo)
	float desce = UV.y * (0.8 + 0.5 * UV.y);
	float n = texture(manchas, vec2(UV.x * fios * 0.5, desce * 0.9 - t * velocidade)).r;
	float n2 = texture(manchas, vec2(UV.x * fios * 1.3 + 0.3, desce * 2.1 - t * velocidade * 1.7)).r;
	float corpo = smoothstep(largura, largura - 0.2, no_fio + (n - 0.5) * 0.3);
	float partido = mix(1.0, smoothstep(0.28, 0.62, n2), UV.y * 0.85);
	float pontas = smoothstep(0.0, 0.05, UV.y) * (1.0 - smoothstep(0.94, 1.0, UV.y));
	// o miolo do fio é água (deixa ver o que está atrás); a beira e as gotas, claras
	float claro = smoothstep(0.35, 0.75, n) * 0.6 + UV.y * 0.25;
	ALBEDO = mix(cor, vec3(1.0), claro);
	ROUGHNESS = 0.18;
	SPECULAR = 0.7;
	ALPHA = corpo * partido * pontas * mix(0.38, 0.7, claro);
}
