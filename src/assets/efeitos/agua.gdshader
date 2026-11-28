// A superfície da água (a fonte, o poço): src/scripts/visual/fx/WaterFeature.cs.
//
// Pedido do Jhon, 27/11: "a fonte do mapa deserto teria que ter textura, barulho e movimentação de água...
// coloque uma água bem feita". Até então a água era a face azul lisa da peça do kit.
//
// - A cor vem da espessura de água entre a superfície e o que está atrás dela (a profundidade da cena): rasa,
//   verde-clara e transparente; funda, azul e turva.
// - O que está debaixo aparece torto (refração: a tela lida deslocada pela inclinação da onda), com cáusticas
//   claras correndo no fundo raso.
// - Duas camadas de onda miúda andando em sentidos cruzados, e os anéis que saem de onde a água cai.
// - Espuma onde a água encosta na pedra (pouca espessura) e no anel da queda.
// - Brilho de sol e de céu pelo material (liso), mais forte de raspão.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, specular_schlick_ggx, shadows_disabled;

uniform vec3 raso : source_color = vec3(0.34, 0.80, 0.76);
uniform vec3 fundo : source_color = vec3(0.05, 0.33, 0.48);
// com quantos metros de água o fundo some
uniform float turvo = 0.8;
uniform float refracao = 0.03;
uniform float ondas = 0.5;
// a faixa de espuma na beira, em metros de espessura de água
uniform float espuma_borda = 0.06;
// onde a água cai, no plano do nó (x, z), e o raio do anel (0: não cai)
uniform vec2 centro = vec2(0.0);
uniform float queda = 0.0;
uniform float agito = 1.0;
uniform sampler2D ruido : repeat_enable, filter_linear_mipmap;      // mapa de normais
uniform sampler2D manchas : repeat_enable, filter_linear_mipmap;    // ruído em cinza (espuma, cáusticas)
uniform sampler2D tela : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform sampler2D fundura : hint_depth_texture, repeat_disable, filter_nearest;

varying vec3 no_mundo;
varying vec2 no_plano;

void vertex() {
	no_plano = VERTEX.xz;
	no_mundo = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

// A espessura de água (m, ao longo do olhar) até o que a cena tem atrás, no ponto da tela.
float espessura_em(vec2 uv, float z_da_agua, mat4 inv_proj) {
	float d = texture(fundura, uv).r;
	vec4 p = inv_proj * vec4(uv * 2.0 - 1.0, d, 1.0);
	return z_da_agua - p.z / p.w;
}

void fragment() {
	vec2 w = no_mundo.xz;
	float t = TIME * agito;

	// as ondas miúdas: duas camadas cruzadas
	vec2 n1 = texture(ruido, w * 0.43 + vec2(0.021, 0.013) * t).rg * 2.0 - 1.0;
	vec2 n2 = texture(ruido, w * 0.97 - vec2(0.017, 0.029) * t + vec2(0.37, 0.11)).rg * 2.0 - 1.0;
	vec2 inclina = (n1 + n2 * 0.6) * ondas;

	// os anéis de onde a água cai, e a espuma em volta
	float espuma_queda = 0.0;
	if (queda > 0.0) {
		vec2 d = no_plano - centro;
		float r = length(d);
		float fora = max(r - queda, 0.0);
		float anel = sin(fora * 22.0 - t * 7.5) * exp(-fora * 1.7) * smoothstep(0.0, 0.07, fora);
		inclina += normalize(d + vec2(1e-5)) * anel * 0.85;
		float m = texture(manchas, w * 1.7 + vec2(0.0, t * 0.11)).r;
		espuma_queda = smoothstep(0.30, 0.0, abs(r - queda) + (m - 0.5) * 0.24);
	}
	vec3 normal_da_onda = normalize(vec3(inclina, 1.0));
	NORMAL_MAP = normal_da_onda * 0.5 + 0.5;
	NORMAL_MAP_DEPTH = 1.0;

	// quanto de água há entre a superfície e o fundo, aqui e no ponto refratado
	float reta = max(espessura_em(SCREEN_UV, VERTEX.z, INV_PROJECTION_MATRIX), 0.0);
	vec2 torto = SCREEN_UV + normal_da_onda.xy * refracao * clamp(reta * 3.0, 0.0, 1.0);
	float espessura = espessura_em(torto, VERTEX.z, INV_PROJECTION_MATRIX);
	if (espessura < 0.0) {          // o que está na frente da água não é puxado para dentro dela
		torto = SCREEN_UV;
		espessura = reta;
	}
	vec3 atras = texture(tela, torto).rgb;

	// cáusticas: onde dois ruídos se cruzam, uma linha clara no fundo raso
	float c1 = texture(manchas, w * 1.3 + normal_da_onda.xy * 0.12 + vec2(0.030, 0.020) * t).r;
	float c2 = texture(manchas, w * 1.9 - normal_da_onda.xy * 0.10 - vec2(0.020, 0.035) * t).r;
	float caustica = smoothstep(0.07, 0.0, abs(c1 - c2));

	float fundo_k = 1.0 - exp(-espessura / max(turvo, 0.01) * 2.3);          // 0 raso, 1 fundo
	vec3 cor = mix(raso, fundo, fundo_k);
	vec3 visto = atras * mix(vec3(1.0), raso * 1.2, clamp(fundo_k * 1.5 + 0.3, 0.0, 1.0));
	visto *= 1.0 + caustica * 0.6 * (1.0 - fundo_k);
	visto = mix(visto, cor, fundo_k * 0.85);

	// a espuma: na beira (pouca água) e no anel da queda
	float m2 = texture(manchas, w * 2.3 + vec2(t * 0.05, -t * 0.04)).r;
	float beira = smoothstep(espuma_borda, 0.0, reta - (m2 - 0.5) * espuma_borda);
	float espuma = clamp(max(beira * 0.8, espuma_queda * (0.5 + 0.5 * m2)), 0.0, 1.0);

	float raspao = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	ALBEDO = mix(cor * 0.22, vec3(0.93, 0.97, 1.0), espuma);
	EMISSION = visto * (1.0 - espuma) * (1.0 - raspao * 0.55);
	ROUGHNESS = mix(0.04, 0.6, espuma);
	SPECULAR = 0.7;
	METALLIC = 0.0;
}
