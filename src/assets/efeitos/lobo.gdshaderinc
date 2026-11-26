// O miolo do material do lobo (uniformes, vértice e fragmento), dividido por
// três arquivos: lobo.gdshader (o lobo vivo), lobo_sumindo.gdshader (a cor do
// corpo morto sumindo) e lobo_profundidade.gdshader (a profundidade do corpo
// morto, escrita antes da cor). O pelo balança igual nos três: a profundidade e
// a cor caem no mesmo lugar, tufo por tufo (26/11, CorpseFade).
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

#ifdef LOBO_SUMINDO
// O corpo morto sumindo (CorpseFade): a cor some por igual, por cima da
// profundidade que a passada LOBO_PROFUNDIDADE escreveu antes.
uniform float sumindo : hint_range(0.0, 1.0) = 1.0;
#endif
#ifdef LOBO_PROFUNDIDADE
// Quanto a profundidade fica atrás da pele (m): a cor da própria pele passa no
// teste; o que está por dentro (olhos, boca, forro), mais fundo, não.
uniform float push_back = 0.0008;
#endif

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
#ifdef LOBO_PROFUNDIDADE
	vec4 view = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	view.xyz += normalize(view.xyz) * push_back;
	POSITION = PROJECTION_MATRIX * view;
#endif
}

void fragment() {
#ifdef LOBO_PROFUNDIDADE
	// Só a profundidade: nada de cor (ALPHA 0), o resto da conta não roda.
	ALBEDO = vec3(0.0);
	ALPHA = 0.0;
#else
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
#ifdef LOBO_SUMINDO
	ALPHA = sumindo;
#endif
#endif
}
