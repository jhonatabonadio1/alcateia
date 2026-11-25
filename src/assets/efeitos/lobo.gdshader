// O corpo do lobo (pedido do Jhon, 18/11: "mais AAA"): o material do
// modelo (cor, normal, oclusão/rugosidade/metal) mais três coisas que o
// material padrão não faz:
//
// - o pelo balança: os vértices das pontas dos tufos (cor de vértice R,
//   gravada por tools/lobo/polir.py) andam com um sopro que atravessa o
//   corpo e com o empurrão do movimento (fur_push: correu, o pelo deita
//   para trás; freou, vai para a frente);
// - o neon (olhos, braceletes, peitoral, joelheiras, costas) é emissão de
//   verdade, respira devagar e acende mais com o poder em uso (neon_boost:
//   rugido e especial);
// - um brilho de contorno no pelo (cor de vértice G), que é o que dá
//   volume ao pelo contra a luz.
//
// Quem monta e alimenta: scripts/visual/wolf/WolfSkin.cs e WolfFx.cs.
shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic;
uniform sampler2D orm_tex : hint_default_white, filter_linear_mipmap;
uniform sampler2D emission_tex : source_color, hint_default_black, filter_linear_mipmap;
uniform bool use_normal = true;
uniform bool use_orm = true;
uniform float roughness = 0.7;
uniform float metallic = 0.0;
uniform vec4 albedo : source_color = vec4(1.0);

// 1 quando a malha traz as máscaras na cor de vértice.
uniform float masks = 0.0;
// A força do neon (25/11, Jhon: "coloque os leds do equipamento brilhando bem
// forte", "quero o neon desse jeito no game"): era 2,6, um fio aceso sem halo.
// A 16 o miolo passa do branco e o glow dos mapas (limiar 1) faz o halo.
uniform float neon_energy = 16.0;
// Quanto o poder (neon_boost) multiplica o neon: era 1,6 sobre os 2,6 de antes;
// com o neon forte, o pico do rugido (escudo cheio) estourava a tela.
uniform float neon_boost_gain = 0.6;
// Todo LED acende igual (Jhon, 25/11: "os leds no equipamento do peitoral e das
// costas devem ter os mesmos brilhos"). A textura de emissão pintou o miolo do
// peitoral a ~0,4 e o das costas a ~0,75–0,8 do bracelete (0,98): cada texel
// aceso vai à força cheia, na cor dele; a borda da faixa continua suave e o que
// não é LED (abaixo de 0,04) continua apagado. 0 volta à textura como veio.
uniform float neon_even = 1.0;
uniform float neon_breath = 0.14;
uniform float sway = 0.014;
uniform float rim = 0.24;

// Índices fixos, depois dos da aura e do vento (0 a 3), que vão por cima
// na mesma malha.
instance uniform float neon_boost : hint_range(0.0, 1.0), instance_index(4) = 0.0;
instance uniform vec3 fur_push : instance_index(5) = vec3(0.0);
instance uniform float fur_gust : instance_index(6) = 0.0;

varying float fur;
varying float tone;

void vertex() {
	float tip = COLOR.r * masks;
	fur = COLOR.g * masks;
	// O tom dos tufos de pelo: B 0,5 é neutro (o corpo); abaixo escurece
	// (a raiz do tufo), acima clareia (a ponta).
	tone = mix(0.5, COLOR.b, masks);
	if (tip > 0.002) {
		vec3 p = VERTEX;
		// Dois sopros de ritmos diferentes atravessando o corpo: nenhum
		// tufo mexe igual ao vizinho.
		float a = sin(dot(p, vec3(3.1, 2.3, 4.7)) + TIME * 2.4);
		float b = sin(dot(p, vec3(-5.3, 1.7, 2.9)) + TIME * 3.9 + 1.3);
		float c = sin(dot(p, vec3(1.9, -4.1, 3.3)) + TIME * 6.3 + 0.7);
		vec3 breeze = vec3(a * 0.6 + b * 0.4, 0.3 * b, b * 0.5 - a * 0.35);
		// Com vento (correndo), um tremor curto e rápido por cima.
		breeze += vec3(c, 0.4 * c, -c) * 0.55 * fur_gust;
		VERTEX += (breeze * sway * (0.55 + 0.9 * fur_gust) + fur_push) * tip;
	}
}

void fragment() {
	vec4 base = texture(albedo_tex, UV) * albedo;
	ALBEDO = base.rgb * mix(0.76, 1.18, tone);

	if (use_orm) {
		vec3 orm = texture(orm_tex, UV).rgb;
		AO = orm.r;
		AO_LIGHT_AFFECT = 0.35;
		ROUGHNESS = orm.g * roughness;
		METALLIC = orm.b * metallic;
	} else {
		ROUGHNESS = roughness;
		METALLIC = metallic;
	}

	if (use_normal) {
		NORMAL_MAP = texture(normal_tex, UV).rgb;
	}

	// O neon respira (devagar, cada lobo no seu tempo de tela) e acende
	// com o poder.
	vec3 glow = texture(emission_tex, UV).rgb;
	float lit = max(glow.r, max(glow.g, glow.b));
	glow = mix(glow, glow / max(lit, 1e-4) * smoothstep(0.04, 0.4, lit), neon_even);
	float breath = 1.0 + neon_breath * sin(TIME * 2.1);
	EMISSION = glow * neon_energy * breath * (1.0 + neon_boost_gain * neon_boost);

	RIM = rim * fur;
	RIM_TINT = 0.65;
}
