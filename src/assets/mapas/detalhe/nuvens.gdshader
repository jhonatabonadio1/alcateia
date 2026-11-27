// Uma camada de nuvens: um plano grande, deitado, em que a nuvem é desenhada por ruído — onde o ruído passa da
// cobertura há nuvem, e a borda dela se desfaz. Sem textura (o desenho é conta) e sem luz (a nuvem tem a cor dela:
// clara em cima, azulada onde é mais rala). Anda devagar com o vento.
//
// Feita para o Setor Zero (26/11), o mapa no alto da montanha: várias camadas empilhadas abaixo do campo são o mar
// de nuvens do precipício (pedido do Jhon: "o precipício deve ter aparência de nuvem, não algo sólido"), e duas bem
// acima são as nuvens do céu ("faltam nuvens no céu também"). Lá, quem diz a altura, o tamanho e a cobertura de cada
// camada é o mapa (src/tools/mapas/setor_zero_acabamento.py: `nuvens`).
//
// Nos outros mapas a céu aberto (pedido do Jhon no mesmo dia: "coloque esses shaders de nuvens nos mapas de dia e de
// noite também") as camadas são postas quando o mapa carrega, pelo clima que ele declara em Maps.cs
// (src/scripts/visual/SkyClouds.cs).
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled, blend_mix;

// A cor do alto da nuvem e a da parte rala (a sombra dela).
uniform vec4 cor : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 sombra : source_color = vec4(0.72, 0.80, 0.92, 1.0);
// Metros por volta do desenho grande (os bancos de nuvem).
uniform float tamanho = 420.0;
// Quanto do céu a camada cobre: 0 = nada, 1 = tudo.
uniform float cobertura : hint_range(0.0, 1.0) = 0.5;
// A largura da borda que se desfaz (0 = recortada, 0.4 = bem macia).
uniform float maciez : hint_range(0.01, 0.6) = 0.22;
// O mais opaca que a camada fica.
uniform float opacidade : hint_range(0.0, 1.0) = 0.9;
// Para onde e quão depressa anda (metros por segundo).
uniform vec2 vento = vec2(3.0, 1.2);
// A camada some de `some_de` a `some_ate` metros do meio do mapa (não se vê a borda do plano).
uniform float some_de = 2600.0;
uniform float some_ate = 3600.0;
// Um deslocamento do desenho, para duas camadas não saírem iguais.
uniform vec2 semente = vec2(0.0, 0.0);
// Perto de onde a camada encosta numa coisa sólida (a encosta, um pico), ela se desfaz em vez de cortar reto.
uniform float encosto = 26.0;
// O brilho da nuvem. Ela não leva luz nem névoa (a névoa de um mapa de névoa forte, como a noite do Neon, a
// engolia), então a claridade é a dela: de dia, perto de 2,3 — o céu de foto é bem mais claro que o branco "1", e
// com 1 a nuvem saía cinza-escura contra ele; de noite, menos de 1.
uniform float brilho = 2.3;
uniform sampler2D profundidade : hint_depth_texture, filter_linear_mipmap;

varying vec3 mundo;

float sorte(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float ruido(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(sorte(i), sorte(i + vec2(1.0, 0.0)), u.x), mix(sorte(i + vec2(0.0, 1.0)), sorte(i + vec2(1.0, 1.0)), u.x), u.y);
}

float camadas(vec2 p) {
	float s = 0.0, a = 0.5;
	for (int k = 0; k < 5; k++) {
		s += a * ruido(p);
		p = p * 2.03 + vec2(17.1, 9.7);
		a *= 0.5;
	}
	return s / 0.96875;
}

void vertex() {
	mundo = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 p = (mundo.xz + TIME * vento) / tamanho + semente;
	// O desenho grande torce o miúdo: as bordas saem em tufos, não em bolhas.
	float grande = camadas(p);
	float miudo = camadas(p * 3.1 + vec2(grande * 1.7, -grande * 1.3));
	float n = grande * 0.72 + miudo * 0.28;
	float corte = 1.0 - cobertura;
	float d = smoothstep(corte - maciez, corte + maciez, n);

	ALBEDO = mix(sombra.rgb, cor.rgb, smoothstep(0.15, 0.85, d * 0.6 + miudo * 0.55)) * brilho;

	float longe = length(mundo.xz);
	float a = d * opacidade * (1.0 - smoothstep(some_de, some_ate, longe));

	// Onde há algo sólido logo atrás da nuvem, ela afina (a encosta entra na nuvem, não é cortada por ela).
	float z = texture(profundidade, SCREEN_UV).r;
	vec4 atras = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, z, 1.0);
	float folga = -atras.z / atras.w + VERTEX.z;
	a *= clamp(folga / encosto, 0.0, 1.0);
	// E bem em cima da câmera ela também afina: atravessar a camada não é bater numa parede branca.
	a *= clamp(length(VERTEX) / 30.0, 0.0, 1.0);
	ALPHA = a;
	FOG = vec4(0.0);
}
