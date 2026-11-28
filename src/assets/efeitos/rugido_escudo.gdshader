// O escudo se formando no rugido (RoarBurst.cs, 27/11). Rugir enche o escudo, e
// só enche se ele rugir até o fim: aqui o jogador VÊ isso. Uma casca de energia
// em volta do lobo, que se fecha de baixo para cima no ritmo do rugido:
//
// - abaixo da linha (`nivel`, de 0 nos pés a 1 na cabeça) a casca já existe: a
//   beira acesa (Fresnel) e uma malha de hexágonos bem fraca, com a corrente
//   subindo por ela;
// - na linha, o fio quente que sobe — trêmulo, elétrico;
// - acima da linha, nada ainda;
// - **escudo cheio** (`estalo`, de 1 a 0): a casca inteira acende de uma vez e
//   uma faixa de luz varre dos pés à cabeça (`varre`, de 0 a 1).
//
// Soltou o rugido no meio: o RoarBurst baixa `forca` e a casca se desfaz sem
// fechar. Só soma luz; some atrás de parede como o resto do lobo.
shader_type spatial;
render_mode unshaded, blend_add, cull_back, depth_draw_never, shadows_disabled, fog_disabled;

uniform vec3 cor : source_color = vec3(0.35, 0.75, 1.0);
uniform float nivel = 0.0;
uniform float forca = 0.0;
uniform float estalo = 0.0;
uniform float varre = -1.0;

varying float altura;
varying vec3 local;

float hx(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }

float vn(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hx(i), hx(i + vec2(1.0, 0.0)), f.x), mix(hx(i + vec2(0.0, 1.0)), hx(i + vec2(1.0, 1.0)), f.x), f.y);
}

// Distância até a beira do hexágono mais perto (0 na beira, 0,5 no meio).
float hexa(vec2 p) {
	const vec2 s = vec2(1.0, 1.7320508);
	vec4 c = floor(vec4(p, p - vec2(0.5, 1.0)) / s.xyxy) + 0.5;
	vec4 h = vec4(p - c.xy * s, p - (c.zw + 0.5) * s);
	vec2 q = abs(dot(h.xy, h.xy) < dot(h.zw, h.zw) ? h.xy : h.zw);
	return 0.5 - max(dot(q, s * 0.5), q.x);
}

void vertex() {
	altura = VERTEX.y * 0.5 + 0.5;
	local = VERTEX;
}

void fragment() {
	float beira = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.4);
	float volta = atan(local.z, local.x);

	// A malha: hexágonos pela volta e pela altura da casca.
	float d = hexa(vec2(volta * 4.4, altura * 9.5));
	// Perto do topo e da base os hexágonos se apertam (a volta fecha): lá a malha some.
	float polo = smoothstep(0.02, 0.14, altura) * (1.0 - smoothstep(0.84, 0.97, altura));
	float fio = smoothstep(0.04, 0.0, d) * polo;
	float miolo = smoothstep(0.5, 0.1, d) * polo;

	// A linha sobe tremendo: a beira de cima da casca é elétrica, não reta.
	float linha = nivel + (vn(vec2(volta * 9.0, TIME * 11.0)) - 0.5) * 0.028 * step(0.01, nivel) * step(nivel, 0.99);
	float cheio = smoothstep(linha, linha - 0.02, altura);
	// O fio é fino e nítido, com um clarão curto logo abaixo dele.
	float topo = (smoothstep(0.012, 0.0, abs(altura - linha)) + 0.35 * smoothstep(0.07, 0.0, linha - altura) * step(altura, linha)) * step(0.005, nivel) * (1.0 - step(0.995, nivel));

	// A corrente sobe pela malha em ondas.
	float sobe = 0.5 + 0.5 * sin(altura * 16.0 - TIME * 9.0 + vn(vec2(volta * 3.0, altura * 4.0)) * 5.0);
	float tremor = 0.8 + 0.2 * sin(TIME * 41.0 + volta * 7.0);

	float casca = cheio * (beira * 0.55 + fio * (0.07 + 0.2 * sobe) * (0.35 + beira)) * tremor;
	casca += topo * (0.5 + 0.8 * beira);
	casca *= forca;

	// Escudo cheio: tudo acende, e a faixa varre para cima.
	float faixa = varre >= 0.0 ? smoothstep(0.16, 0.0, abs(altura - varre)) : 0.0;
	float cheia = estalo * (beira * 1.5 + fio * 0.34 + miolo * 0.04) + faixa * (0.45 + fio * 0.7 + beira * 1.2) * (0.35 + 0.65 * estalo);

	float tudo = casca + cheia;
	vec3 quente = mix(cor, vec3(1.0, 0.97, 0.93), 0.75);
	ALBEDO = mix(cor, quente, clamp(topo * forca + faixa + estalo * 0.6, 0.0, 1.0)) * tudo;
	ALPHA = clamp(tudo, 0.0, 1.0);
}
