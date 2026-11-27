// O céu dos mapas ao ar livre feitos da cena de demonstração (Cidade, Cidade do Deserto e Obra, 27/11 — pedido do
// Jhon: "iluminação AAA+, shaders e nuvens"). No lugar da fotografia parada (dia_claro.hdr): a cor do ar calculada
// (o azul do alto, o claro do horizonte, o clarão em volta do sol e o disco dele) e uma camada de nuvens que anda
// com o vento, com volume — a base escura, o lado do sol aceso, a borda que brilha contra a luz.
//
// As nuvens são um teto a certa altura, percorrido em poucos passos ao longo do olhar (a espessura dá a paralaxe: a
// nuvem tem lado e barriga, não é um desenho colado no céu). A densidade sai de um ruído que se repete
// (nuvens_ruido.png: a forma grande no vermelho, a média no verde, o miúdo no azul; desenhado por
// src/tools/mapas/kits/fundos.py: nuvens). A luz de cada ponto é quanta nuvem há entre ele e o sol.
//
// Custo: as nuvens são desenhadas em meia resolução (use_half_res_pass) e só então juntadas ao céu; no mapa de
// reflexo e de luz ambiente (AT_CUBEMAP_PASS) vão com dois passos em vez de seis. Como o céu anda (TIME), o Godot
// refaz esse mapa todo quadro: o recurso Sky da cena fica em "tempo real" (process_mode = 3, 256 px).
//
// O sol é a primeira luz direcional da cena que entra no céu (LIGHT0): a luz de preenchimento dos mapas fica fora
// (sky_mode = só luz).
shader_type sky;
render_mode use_half_res_pass;

group_uniforms ar;
uniform vec3 topo : source_color = vec3(0.13, 0.33, 0.72);          // o azul do alto
uniform vec3 horizonte : source_color = vec3(0.62, 0.78, 0.95);     // o claro da linha do horizonte
uniform vec3 chao : source_color = vec3(0.42, 0.44, 0.47);          // abaixo do horizonte (o que o reflexo vê do chão)
uniform float curva = 0.45;                                         // menor: o claro do horizonte sobe mais
uniform float energia = 1.0;
uniform float clarao = 0.55;                                        // o clarão largo em volta do sol
uniform float disco = 8.0;                                          // o brilho do disco do sol

group_uniforms nuvens;
uniform sampler2D ruido : repeat_enable, filter_linear;
uniform float cobertura : hint_range(0.0, 1.0) = 0.45;              // quanto do céu é nuvem
uniform float maciez : hint_range(0.01, 0.6) = 0.22;                // a borda: baixa recorta, alta esfuma
uniform float densidade = 1.6;
uniform float escala = 0.11;                                        // o tamanho das nuvens (menor = maiores)
uniform float espessura = 0.10;                                     // o volume: quanto a camada tem de alto
uniform vec2 vento = vec2(0.006, 0.0025);                           // o quanto andam, por segundo
uniform vec3 nuvem_luz : source_color = vec3(1.0, 0.98, 0.95);      // o lado do sol
uniform vec3 nuvem_sombra : source_color = vec3(0.50, 0.57, 0.70);  // a barriga
uniform float contra_luz = 1.4;                                     // o brilho da borda contra o sol

// Quanta nuvem há no ponto p do teto, na altura h (0 a base, 1 o topo).
float nuvem(vec2 p, float h) {
	vec2 t = TIME * vento;
	float grande = texture(ruido, p * 0.5 + t).r;
	float media = texture(ruido, p * 1.7 + t * 1.6 + vec2(0.37, 0.11)).g;
	float miudo = texture(ruido, p * 5.3 + t * 2.4).b;
	float n = grande * 0.62 + media * 0.30 + miudo * 0.08;
	// (a soma de três ruídos junta os valores no meio: espalha de volta, para a cobertura ser a fração do céu com nuvem)
	n = smoothstep(0.24, 0.76, n);
	// a nuvem é mais larga embaixo e arredonda em cima; some na base e no topo
	float perfil = smoothstep(0.0, 0.25, h) * (1.0 - smoothstep(0.55, 1.0, h));
	float corte = 1.0 - cobertura + (1.0 - perfil) * 0.18;
	return smoothstep(corte, corte + maciez, n);
}

// As nuvens na direção do olhar: (cor, quanto tapam).
vec4 camada(vec3 olhar, vec3 sol, vec3 cor_sol, int passos) {
	if (olhar.y < 0.01) return vec4(0.0);
	// o teto é curvo: perto do horizonte as nuvens se juntam sem virar um risco
	float alcance = 1.0 / (olhar.y + 0.16);
	vec2 base = olhar.xz * alcance * escala;
	vec2 passo = olhar.xz * alcance * escala * espessura / float(passos);
	vec2 para_o_sol = normalize(sol.xz + vec2(0.0001)) * escala * 0.35 * (1.0 - 0.6 * sol.y);
	float tapa = 0.0;
	vec3 cor = vec3(0.0);
	for (int i = 0; i < passos; i++) {
		float h = (float(i) + 0.5) / float(passos);
		vec2 p = base + passo * float(i);
		float d = nuvem(p, h) * densidade / float(passos) * 2.2;
		if (d < 0.002) continue;
		// a luz que chega: quanta nuvem há do ponto até o sol, e quanto falta para o topo
		float sombra = nuvem(p + para_o_sol, min(h + 0.25, 0.9)) + nuvem(p + para_o_sol * 2.2, min(h + 0.45, 0.9)) * 0.6;
		float luz = exp(-sombra * 1.9) * mix(0.55, 1.0, h);
		vec3 c = mix(nuvem_sombra, nuvem_luz * cor_sol, luz);
		float a = (1.0 - tapa) * clamp(d, 0.0, 1.0);
		cor += c * a;
		tapa += a;
	}
	if (tapa < 0.001) return vec4(0.0);
	cor /= tapa;
	// contra o sol a borda fina acende
	float frente = max(dot(olhar, sol), 0.0);
	cor += cor_sol * pow(frente, 10.0) * contra_luz * (1.0 - tapa) * tapa * 2.0;
	// para o horizonte a nuvem toma a cor do ar e some
	float longe = smoothstep(0.0, 0.22, olhar.y);
	return vec4(cor, tapa * longe);
}

vec3 ar(vec3 olhar, vec3 sol, vec3 cor_sol, float tem_sol) {
	float alto = clamp(olhar.y, 0.0, 1.0);
	vec3 c = mix(horizonte, topo, pow(alto, curva));
	// abaixo do horizonte, o chão (com a linha esfumada)
	c = mix(c, chao, smoothstep(0.0, -0.12, olhar.y));
	float frente = max(dot(olhar, sol), 0.0);
	// o clarão: largo e fraco, e estreito e forte junto ao sol; mais forte perto do horizonte
	c += cor_sol * tem_sol * (pow(frente, 6.0) * 0.22 + pow(frente, 48.0) * 0.6) * clarao * (0.6 + 0.4 * (1.0 - alto));
	return c;
}

void sky() {
	vec3 sol = LIGHT0_ENABLED ? normalize(LIGHT0_DIRECTION) : vec3(0.3, 0.8, 0.5);
	vec3 cor_sol = LIGHT0_ENABLED ? LIGHT0_COLOR : vec3(1.0);
	float tem_sol = LIGHT0_ENABLED ? 1.0 : 0.0;
	if (AT_HALF_RES_PASS) {
		vec4 n = camada(EYEDIR, sol, cor_sol, 6);
		COLOR = n.rgb;
		ALPHA = n.a;
	} else {
		vec3 c = ar(EYEDIR, sol, cor_sol, tem_sol);
		// o disco do sol, com a beira macia
		float frente = dot(EYEDIR, sol);
		c += cor_sol * tem_sol * disco * smoothstep(0.99965, 0.99985, frente);
		vec4 n = AT_CUBEMAP_PASS ? camada(EYEDIR, sol, cor_sol, 2) : HALF_RES_COLOR;
		// a nuvem longe mistura-se com o ar do horizonte
		vec3 nuvem_cor = mix(horizonte, n.rgb, smoothstep(0.02, 0.30, EYEDIR.y) * 0.85 + 0.15);
		c = mix(c, nuvem_cor, n.a);
		COLOR = c * energia;
	}
}
