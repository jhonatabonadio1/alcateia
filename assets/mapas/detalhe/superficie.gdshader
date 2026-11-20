// O material das superfícies dos mapas de paleta (os kits "Low Poly FPS" e o Armazém).
//
// A cor da paleta é a tinta de base — é o desenho do mapa. Por cima dela este material desenha o material de verdade de
// cada face: o tijolo com a junta, a laje com a emenda, a tábua, a chapa com rebite, a telha, o calçamento. Vem das
// texturas de tools/mapas/detalhe_texturas.py (relevo, oclusão, claro-escuro, aspereza e a tinta que puxa o tom de cada
// peça), projetadas pelo mundo (de cima e dos lados): não dependem da UV da peça e emendam de uma peça para a outra.
// O metal é metal, o vidro reflete, a telha segue a queda do telhado.
//
// De que material é a face vem no vermelho da cor do vértice (material ÷ 255), gravado por tools/mapas/kits/detalhe.py
// — a ordem das tabelas abaixo é a de CLASSES, lá.
//
// Para regular: `intensidade` (0 = o mapa liso, só a paleta; 1 = o padrão) no material, ou as tabelas aqui embaixo.
shader_type spatial;
render_mode cull_disabled;

uniform sampler2D paleta : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2DArray detalhe_n : filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2DArray detalhe_m : filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2DArray detalhe_c : filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D macro : filter_linear_mipmap, repeat_enable;

/** Quanto do detalhe entra: 0 é o mapa liso (só a paleta), 1 o padrão. */
uniform float intensidade : hint_range(0.0, 2.0) = 1.0;
/** A força do relevo, por cima da de cada material. */
uniform float relevo : hint_range(0.0, 3.0) = 1.0;
/** A força das manchas largas (a variação de tom de metro em metro). */
uniform float manchas : hint_range(0.0, 3.0) = 1.0;
/** Quanto a tinta do material puxa a cor da paleta (o tijolo mais alaranjado, a ferrugem no risco). */
uniform float tinta : hint_range(0.0, 2.0) = 1.0;
/** Pinta cada material de uma cor, para conferir o que tools/mapas/kits/detalhe.py decidiu. */
uniform bool ver_classes = false;

const int N = 27;
const float CAMADA_TELHA = 17.0;
const int TELHADO = 20;
// camada, metros por repetição, relevo, claro-escuro
const vec4 T1[N] = {
	vec4(13.0, 1.0, 0.0, 0.0),   //  0 liso (lâmpada, o que não se sabe)
	vec4(0.0, 4.0, 0.9, 0.9),    //  1 asfalto
	vec4(1.0, 5.0, 0.8, 1.0),    //  2 piso (placas de concreto: calçada, laje, galpão)
	vec4(2.0, 4.0, 0.9, 1.0),    //  3 concreto (parede, mureta: painel de fôrma)
	vec4(3.0, 4.5, 1.0, 1.0),    //  4 reboco (com o tijolo aparecendo onde caiu)
	vec4(4.0, 1.8, 1.0, 1.0),    //  5 tijolo
	vec4(5.0, 2.4, 0.8, 0.9),    //  6 metal pintado (chapa com emenda e rebite)
	vec4(6.0, 0.9, 0.8, 0.9),    //  7 metal cru
	vec4(7.0, 2.2, 0.03, 0.5),   //  8 vidro
	vec4(8.0, 1.6, 1.0, 1.0),    //  9 madeira (tábuas)
	vec4(9.0, 1.3, 0.6, 1.0),    // 10 folhagem
	vec4(10.0, 3.0, 0.9, 0.9),   // 11 areia
	vec4(11.0, 3.2, 1.0, 0.9),   // 12 rocha
	vec4(12.0, 0.9, 0.9, 0.8),   // 13 tecido
	vec4(13.0, 0.6, 0.6, 0.5),   // 14 plástico
	vec4(14.0, 4.0, 1.0, 1.0),   // 15 adobe (o barro das casas do deserto, com o bloco aparecendo)
	vec4(16.0, 3.0, 1.0, 0.9),   // 16 terra batida
	vec4(13.0, 0.5, 0.3, 0.4),   // 17 louça
	vec4(13.0, 0.4, 0.5, 0.7),   // 18 papel, papelão
	vec4(13.0, 0.35, 0.9, 0.6),  // 19 borracha
	vec4(0.0, 1.6, 0.7, 0.8),    // 20 telhado (o plano: manta; o inclinado usa a telha, veja fragment)
	vec4(0.0, 4.0, 0.8, 0.7),    // 21 faixa pintada no piso
	vec4(13.0, 1.2, 0.2, 0.3),   // 22 lataria (pintura de carro)
	vec4(7.0, 1.5, 0.6, 0.3),    // 23 água parada
	vec4(15.0, 2.4, 1.0, 1.0),   // 24 calçamento de pedra
	vec4(18.0, 1.5, 1.0, 1.0),   // 25 brita
	vec4(16.0, 1.1, 1.2, 1.0)    // 26 tronco (a casca: o rachado da terra, miúdo)
};
// aspereza mínima, aspereza máxima, metal, especular
const vec4 T2[N] = {
	vec4(0.9, 0.9, 0.0, 0.5),
	vec4(0.75, 0.97, 0.0, 0.5),
	vec4(0.55, 0.92, 0.0, 0.5),
	vec4(0.7, 0.95, 0.0, 0.5),
	vec4(0.8, 0.97, 0.0, 0.5),
	vec4(0.75, 0.97, 0.0, 0.5),
	vec4(0.3, 0.62, 0.0, 0.55),
	vec4(0.28, 0.6, 0.9, 0.5),
	vec4(0.03, 0.2, 0.0, 1.0),
	vec4(0.55, 0.9, 0.0, 0.5),
	vec4(0.6, 0.9, 0.0, 0.4),
	vec4(0.85, 1.0, 0.0, 0.4),
	vec4(0.75, 0.98, 0.0, 0.5),
	vec4(0.85, 1.0, 0.0, 0.35),
	vec4(0.4, 0.65, 0.0, 0.5),
	vec4(0.85, 1.0, 0.0, 0.45),
	vec4(0.82, 1.0, 0.0, 0.45),
	vec4(0.18, 0.4, 0.0, 0.6),
	vec4(0.85, 0.98, 0.0, 0.4),
	vec4(0.7, 0.9, 0.0, 0.4),
	vec4(0.8, 0.98, 0.0, 0.45),
	vec4(0.6, 0.9, 0.0, 0.5),
	vec4(0.16, 0.34, 0.25, 0.7),
	vec4(0.02, 0.1, 0.0, 1.0),
	vec4(0.55, 0.9, 0.0, 0.5),
	vec4(0.85, 1.0, 0.0, 0.45),
	vec4(0.8, 0.98, 0.0, 0.4)
};
// as manchas largas (quanto o tom varia de metro em metro) e quanto a tinta do material entra
const vec2 T3[N] = {
	vec2(0.0, 0.0), vec2(1.0, 0.6), vec2(1.0, 1.0), vec2(1.0, 1.0), vec2(0.9, 1.0), vec2(0.7, 1.0), vec2(0.5, 1.0), vec2(0.5, 1.0),
	vec2(0.15, 0.0), vec2(0.6, 1.0), vec2(0.8, 1.0), vec2(1.0, 1.0), vec2(1.0, 1.0), vec2(0.5, 0.0), vec2(0.3, 0.0), vec2(1.0, 1.0),
	vec2(1.0, 1.0), vec2(0.2, 0.0), vec2(0.5, 0.0), vec2(0.3, 0.0), vec2(0.9, 0.5), vec2(0.6, 0.3), vec2(0.25, 0.0), vec2(0.3, 0.0),
	vec2(0.8, 1.0), vec2(0.6, 1.0), vec2(0.5, 0.5)
};

varying flat int material_da_face;
varying vec3 pos_mundo;
varying vec3 normal_mundo;

void vertex() {
	material_da_face = int(COLOR.r * 255.0 + 0.5);
	pos_mundo = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	normal_mundo = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

vec3 cor_da_classe(int c) {
	float h = fract(float(c) * 0.381966);
	return clamp(abs(fract(h + vec3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0, 0.0, 1.0) * (c % 2 == 0 ? 1.0 : 0.6);
}

void fragment() {
	vec3 base = texture(paleta, UV).rgb;
	int c = clamp(material_da_face, 0, N - 1);
	vec4 t1 = T1[c];
	vec4 t2 = T2[c];
	vec2 t3 = T3[c];
	float quanto = clamp(intensidade, 0.0, 1.0);

	vec3 n = normalize(normal_mundo);
	if (!FRONT_FACING) {
		n = -n;
	}
	// o peso de cada projeção (de lado em x, de cima, de lado em z): quase sempre uma só
	vec3 w = pow(abs(n), vec3(8.0));
	w /= (w.x + w.y + w.z);

	vec3 desvio = vec3(0.0);   // quanto o relevo entorta a normal, no mundo
	vec3 m = vec3(0.0);        // claro-escuro, aspereza, altura
	vec3 cor_mat = vec3(0.0);  // a tinta do material (0,5 = neutro)
	float oclusao = 0.0;

	// o telhado inclinado: a telha corre ao longo do beiral e desce com a queda, qualquer que seja o lado da água
	vec2 queda = n.xz / max(length(n.xz), 1e-4);
	vec3 ao_longo = vec3(-queda.y, 0.0, queda.x);
	vec2 uv_telha = vec2(dot(pos_mundo, ao_longo), dot(pos_mundo.xz, queda) / max(n.y, 0.25)) / 1.8;
	vec2 tx = dFdx(uv_telha);
	vec2 ty = dFdy(uv_telha);
	bool telha = c == TELHADO && n.y > 0.2 && n.y < 0.97;

	vec3 p = pos_mundo / t1.y;
	vec3 dx = dFdx(p);
	vec3 dy = dFdy(p);
	if (telha) {
		vec3 tn = textureGrad(detalhe_n, vec3(uv_telha, CAMADA_TELHA), tx, ty).rgb;
		vec3 sobe = normalize(cross(ao_longo, n));
		if (sobe.y < 0.0) {
			sobe = -sobe;
		}
		desvio = (tn.r * 2.0 - 1.0) * ao_longo + (tn.g * 2.0 - 1.0) * sobe;
		oclusao = tn.b;
		m = textureGrad(detalhe_m, vec3(uv_telha, CAMADA_TELHA), tx, ty).rgb;
		cor_mat = textureGrad(detalhe_c, vec3(uv_telha, CAMADA_TELHA), tx, ty).rgb;
		t1 = vec4(CAMADA_TELHA, 1.8, 1.0, 1.0);
		t3 = vec2(0.6, 1.0);
	} else {
		if (w.x > 0.01) {
			vec3 q = vec3(p.z, -p.y, t1.x);
			vec2 gx = vec2(dx.z, -dx.y);
			vec2 gy = vec2(dy.z, -dy.y);
			vec3 tn = textureGrad(detalhe_n, q, gx, gy).rgb;
			desvio += w.x * vec3(0.0, tn.g * 2.0 - 1.0, tn.r * 2.0 - 1.0);
			oclusao += w.x * tn.b;
			m += w.x * textureGrad(detalhe_m, q, gx, gy).rgb;
			cor_mat += w.x * textureGrad(detalhe_c, q, gx, gy).rgb;
		}
		if (w.y > 0.01) {
			vec3 q = vec3(p.x, p.z, t1.x);
			vec3 tn = textureGrad(detalhe_n, q, dx.xz, dy.xz).rgb;
			desvio += w.y * vec3(tn.r * 2.0 - 1.0, 0.0, -(tn.g * 2.0 - 1.0));
			oclusao += w.y * tn.b;
			m += w.y * textureGrad(detalhe_m, q, dx.xz, dy.xz).rgb;
			cor_mat += w.y * textureGrad(detalhe_c, q, dx.xz, dy.xz).rgb;
		}
		if (w.z > 0.01) {
			vec3 q = vec3(p.x, -p.y, t1.x);
			vec2 gx = vec2(dx.x, -dx.y);
			vec2 gy = vec2(dy.x, -dy.y);
			vec3 tn = textureGrad(detalhe_n, q, gx, gy).rgb;
			desvio += w.z * vec3(tn.r * 2.0 - 1.0, tn.g * 2.0 - 1.0, 0.0);
			oclusao += w.z * tn.b;
			m += w.z * textureGrad(detalhe_m, q, gx, gy).rgb;
			cor_mat += w.z * textureGrad(detalhe_c, q, gx, gy).rgb;
		}
	}

	// as manchas largas: no chão pela planta; na parede, ao longo dela e pela altura (emenda na quina)
	vec2 uv_chao = pos_mundo.xz;
	vec2 uv_parede = vec2(pos_mundo.x + pos_mundo.z, pos_mundo.y);
	vec2 uv_mancha = mix(uv_parede, uv_chao, step(0.5, w.y));
	float mancha = texture(macro, uv_mancha / 17.0).r * 0.6 + texture(macro, uv_mancha / 5.3 + 0.37).g * 0.4 - 0.5;
	mancha *= t3.x * manchas * intensidade;

	float claro = (m.r - 0.5) * 2.0 * t1.w * intensidade;
	vec3 cor = base * (1.0 + claro * 0.6) * (1.0 + mancha * 0.22);
	// a tinta do material puxa o tom (multiplica em volta do neutro)
	cor *= mix(vec3(1.0), cor_mat * 2.0, clamp(t3.y * tinta * intensidade, 0.0, 2.0));
	// a fresta e o poro escurecem um pouco
	cor *= mix(1.0, oclusao, 0.45 * quanto);

	if (ver_classes) {
		ALBEDO = cor_da_classe(c);
		ROUGHNESS = 1.0;
	} else {
		ALBEDO = cor;
		ROUGHNESS = mix(0.9, clamp(mix(t2.x, t2.y, m.g) + mancha * 0.12, 0.02, 1.0), quanto);
		METALLIC = t2.z * quanto;
		SPECULAR = mix(0.5, t2.w, quanto);
		AO = mix(1.0, oclusao, 0.8 * quanto);
		AO_LIGHT_AFFECT = 0.3;
		vec3 nm = normalize(n + desvio * t1.z * relevo * intensidade);
		NORMAL = normalize((VIEW_MATRIX * vec4(nm, 0.0)).xyz);
	}
}
