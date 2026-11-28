// O chão do rugido (RoarBurst.cs, 27/11): um quadrado deitado nos pés do lobo.
// Três coisas, da mais funda à mais rasa:
//
// - os **raios pelo chão**: saem do pé dele para todos os lados, quebrados
//   como raio de verdade, e avançam com a carga (`carga`, de 0 a 1), com pulsos
//   correndo por dentro — o chão conduz a eletricidade que ele puxa;
// - a **poça**: a luz que sobra nos pés, respirando;
// - as **ondas**: até quatro anéis de choque abrindo (`ondas`: raio em metros,
//   força, largura). A frente do anel é fina e quente; logo atrás fica um
//   rastro curto na cor do lobo, e os veios acendem quando o anel passa.
//
// Tudo por conta, sem textura. Soma luz (não escurece o chão) e some na borda
// do quadrado, para não aparecer o corte.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

uniform vec3 cor : source_color = vec3(0.35, 0.75, 1.0);
// Meio lado do quadrado, em metros: converte UV em distância de verdade.
uniform float raio = 3.6;
uniform float carga = 0.0;
uniform float brilho = 0.0;
// O clarão do escudo cheio, de 1 (agora) a 0.
uniform float estouro = 0.0;
uniform vec4 ondas[4];
uniform float semente = 0.0;

float hx(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float vn(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hx(i), hx(i + vec2(1.0, 0.0)), f.x), mix(hx(i + vec2(0.0, 1.0)), hx(i + vec2(1.0, 1.0)), f.x), f.y);
}

// Um raio pelo chão. Há `quantos` em volta do lobo, cada um com o seu número
// (`qual`). O caminho de cada um é quebrado como o de um raio de verdade — vai
// reto um pedaço, dobra, vai reto de novo — em dois tamanhos de dobra; a dobra
// miúda muda várias vezes por segundo, e o raio treme sem sair do lugar.
// Devolve o desvio para o lado, em metros, na distância `r`.
float desvio(float qual, float r) {
	float a = r * 3.1 + hx(vec2(qual, 3.0)) * 7.0;
	float grande = mix(hx(vec2(qual, floor(a))), hx(vec2(qual, floor(a) + 1.0)), fract(a)) - 0.5;
	float passo = floor(TIME * 14.0 + qual * 0.37);
	float b = r * 10.0 + hx(vec2(qual, 5.0)) * 9.0;
	float miudo = mix(hx(vec2(qual + passo, floor(b))), hx(vec2(qual + passo, floor(b) + 1.0)), fract(b)) - 0.5;
	// Preso no pé dele (todos saem do mesmo ponto) e solto para fora.
	return (grande * 0.5 + miudo * 0.09) * smoothstep(0.0, 0.7, r);
}

// A distância, em metros, até o raio mais perto de uma leva de `quantos`, e o
// número dele (para cada raio ter o seu alcance e a sua força).
vec2 raio_perto(float volta, float r, float quantos, float leva) {
	float casa = floor(volta * quantos);
	float perto = 99.0, dele = 0.0;
	for (int k = -1; k <= 1; k++) {
		float i = mod(casa + float(k), quantos);
		float qual = i + leva * 97.0 + semente;
		float centro = (i + 0.5) / quantos;
		// A volta fecha: a diferença de ângulo vai de −meia volta a meia volta.
		float falta = fract(volta - centro + 0.5) - 0.5;
		float d = abs(falta * 6.2831853 * r - desvio(qual, r));
		if (d < perto) { perto = d; dele = qual; }
	}
	return vec2(perto, dele);
}

void fragment() {
	vec2 p = (UV - 0.5) * 2.0 * raio;
	float r = length(p);
	float borda = 1.0 - smoothstep(0.78, 1.0, r / raio);

	// Duas levas: os troncos, que saem do pé dele, e os galhos, mais finos,
	// que só aparecem a certa distância. Cada raio vai até onde a sua sorte
	// deixa (uns morrem perto, outros correm longe) e afina até a ponta.
	float volta = atan(p.y, p.x) / 6.2831853 + 0.5;
	vec2 t = raio_perto(volta, r, 9.0, 1.0);
	vec2 g = raio_perto(volta + 0.031, r, 17.0, 2.0);
	float longe = raio * 0.58;
	float fim_t = longe * (0.55 + 0.6 * hx(vec2(t.y, 11.0)));
	float fim_g = longe * (0.5 + 0.7 * hx(vec2(g.y, 12.0)));
	float nasce_g = 0.35 + 0.9 * hx(vec2(g.y, 13.0));
	// Até onde a carga já levou cada um; no clarão do escudo cheio todos esticam.
	float solta = clamp(carga + estouro * 0.25, 0.0, 1.25);
	float ate_t = fim_t * solta, ate_g = fim_g * solta;
	float vivo_t = smoothstep(ate_t, ate_t - 0.35, r);
	float vivo_g = smoothstep(ate_g, ate_g - 0.3, r) * smoothstep(nasce_g, nasce_g + 0.25, r);
	float grosso = 0.03 * (1.0 + 1.2 * exp(-r * 1.8));
	float tronco = smoothstep(grosso, 0.0, t.x) * vivo_t;
	float galho = smoothstep(0.016, 0.0, g.x) * vivo_g * 0.8;
	float fino = clamp(tronco + galho, 0.0, 1.0);
	float halo = smoothstep(0.17, 0.0, t.x) * vivo_t * 0.22 + smoothstep(0.09, 0.0, g.x) * vivo_g * 0.1;
	// A ponta de cada raio brilha mais: é por onde ele avança.
	float frente = (smoothstep(0.4, 0.0, ate_t - r) * tronco + smoothstep(0.3, 0.0, ate_g - r) * galho) * step(0.02, carga);
	float dentro = 1.0;

	// A corrente passando: pulsos que correm para fora e um tremor rápido.
	float pulso = 0.55 + 0.45 * sin(r * 5.5 - TIME * 13.0 + vn(p * 2.0) * 6.0);
	float tremor = 0.75 + 0.25 * sin(TIME * 47.0 + vn(p * 3.0 + TIME * 2.0) * 12.0);
	float cai = exp(-r * 0.55);

	// As ondas de choque.
	float anel = 0.0, rastro = 0.0;
	for (int i = 0; i < 4; i++) {
		float d = r - ondas[i].x;
		float larg = max(ondas[i].z, 0.01);
		anel += ondas[i].y * smoothstep(larg * 0.11, 0.0, abs(d));
		// O rastro fica logo atrás do anel (por onde ele acabou de passar).
		rastro += ondas[i].y * smoothstep(-larg * 0.55, 0.0, d) * (1.0 - smoothstep(0.0, larg * 0.1, d)) * 0.5;
	}

	float veios = (fino + halo) * dentro * cai * (0.35 + 0.65 * pulso) * tremor * brilho;
	veios += fino * frente * 0.9 * brilho;
	// O anel acende o veio por onde passa; o clarão acende a rede perto dele.
	veios += (fino * 1.6 + halo) * (rastro * 1.6 + anel) * exp(-r * 0.25);
	veios += (fino + halo * 0.6) * estouro * exp(-r * 0.85) * 1.5;

	float poca = exp(-r * r * 1.55) * (0.42 + 0.08 * sin(TIME * 3.2)) * brilho + exp(-r * r * 1.8) * estouro * 0.22;

	vec3 quente = mix(cor, vec3(1.0, 0.97, 0.93), 0.72);
	vec3 c = cor * (veios * 1.25 + rastro * 0.3 + poca)
		+ quente * (fino * dentro * cai * brilho * 0.55 + fino * frente * brilho * 0.6 + anel * 1.2 + poca * 0.3);
	c *= borda;
	ALBEDO = c;
	ALPHA = clamp(max(max(c.r, c.g), c.b), 0.0, 1.0);
}
