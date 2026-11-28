// A onda de choque do rugido no chão, como distorção (RoarBurst.cs, 27/11): o
// mesmo quadrado deitado do `rugido_chao`, mas em vez de somar luz ele entorta o
// que está atrás — o ar empurrado dobra a imagem do chão quando o anel passa.
//
// A dobra sai da inclinação do perfil do anel na tela (dFdx/dFdy): sem matriz
// nenhuma, e na direção certa de qualquer ângulo de câmera. Só o que está na
// faixa do anel é redesenhado; o resto do quadrado fica transparente.
//
// Lê a tela (custa uma cópia dela por quadro): o RoarBurst só mostra esta malha
// enquanto há onda forte, e só da qualidade Alta para cima.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

uniform sampler2D tela : hint_screen_texture, filter_linear, repeat_disable;
uniform float raio = 3.6;
uniform vec4 ondas[4];
// Quanto a imagem anda, em fração da tela por unidade de inclinação.
uniform float forca = 0.3;

void fragment() {
	vec2 p = (UV - 0.5) * 2.0 * raio;
	float r = length(p);
	float h = 0.0;
	for (int i = 0; i < 4; i++) {
		float d = (r - ondas[i].x) / max(ondas[i].z * 0.42, 0.01);
		h += ondas[i].y * exp(-d * d);
	}
	h *= 1.0 - smoothstep(0.8, 1.0, r / raio);
	vec2 dobra = vec2(dFdx(h), dFdy(h)) * forca;
	// O limite: de muito perto a inclinação por pixel é pequena, de longe é grande.
	dobra = clamp(dobra, vec2(-0.03), vec2(0.03));
	ALBEDO = texture(tela, SCREEN_UV + dobra).rgb;
	ALPHA = clamp(h * 3.0, 0.0, 1.0);
}
