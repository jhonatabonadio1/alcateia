// O sopro do rugido (RoarBurst.cs, 27/11): o ar que sai da boca, como volume e
// não só como riscos. Um cone aberto para a frente, com fios de ar correndo ao
// longo dele — só ao comprido: nada de faixa atravessada, que viraria o anel na
// frente da boca que o Jhon mandou tirar (18/11). Os riscos de luz nítidos por
// cima continuam sendo fitas (RoarBurst).
//
// Some na borda do cone (de lado ele não tem contorno) e com a distância.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

uniform vec3 cor : source_color = vec3(0.35, 0.75, 1.0);
uniform float forca = 0.0;
// O estouro do começo: o sopro sai mais cheio e mais comprido.
uniform float golpe = 0.0;

varying float ao_longo;

float hx(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }

// Ruído que fecha na volta do cone (x de 0 a `volta` repete).
float vn(vec2 p, float volta) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float x0 = mod(i.x, volta), x1 = mod(i.x + 1.0, volta);
	return mix(mix(hx(vec2(x0, i.y)), hx(vec2(x1, i.y)), f.x), mix(hx(vec2(x0, i.y + 1.0)), hx(vec2(x1, i.y + 1.0)), f.x), f.y);
}

void vertex() {
	// A boca é a ponta de baixo do cone (y = −0,5); o fim, a de cima.
	ao_longo = VERTEX.y + 0.5;
}

void fragment() {
	// Preso de 0 a 1: na beira a conta passa um fio de 1, e potência de número negativo é um ponto preto na tela.
	float s = clamp(ao_longo, 0.0, 1.0);
	// Fios compridos: muito detalhe em volta, pouco ao longo, correndo para fora.
	float a = vn(vec2(UV.x * 22.0, s * 1.4 - TIME * 3.6), 22.0);
	float b = vn(vec2(UV.x * 46.0, s * 2.6 - TIME * 6.5 + 7.0), 46.0);
	float fios = smoothstep(0.62, 0.9, a) + smoothstep(0.7, 0.95, b) * 0.6;
	float nevoa = 0.1 + 0.16 * a;

	float de_frente = pow(abs(dot(NORMAL, VIEW)), 0.8);
	float nasce = smoothstep(0.0, 0.14, s);
	float some = pow(1.0 - s, 1.3 - 0.5 * golpe);
	float x = (fios * 0.55 + nevoa) * de_frente * nasce * some * (forca + golpe * 0.9);

	ALBEDO = mix(vec3(1.0, 0.97, 0.94), cor, clamp(s * 1.4, 0.0, 1.0)) * x;
	ALPHA = clamp(x, 0.0, 1.0);
}
