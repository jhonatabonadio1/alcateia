// A bolha de pressão do rugido (RoarBurst.cs, 27/11): uma esfera em volta do
// lobo que entorta o que está atrás dela na beira (Fresnel), como uma lente —
// o ar comprimido no estouro do rugido e na hora em que o escudo fecha. Não tem
// cor nenhuma: é só a dobra. Cresce depressa e some; o RoarBurst a mantém baixa
// (não passa da altura da luz do uivo).
//
// Lê a tela: só aparece nos dois estouros, da qualidade Alta para cima.
shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, shadows_disabled, fog_disabled;

uniform sampler2D tela : hint_screen_texture, filter_linear, repeat_disable;
// De 1 (acabou de estourar) a 0.
uniform float forca = 0.0;
uniform float dobra = 0.03;

void fragment() {
	float beira = 1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	// Uma faixa perto da beira, não a beira em si: a casca tem espessura.
	float faixa = smoothstep(0.2, 0.75, beira) * (1.0 - smoothstep(0.86, 1.0, beira));
	vec2 fora = normalize(NORMAL.xy + vec2(0.0001)) * vec2(1.0, -1.0);
	ALBEDO = texture(tela, SCREEN_UV + fora * faixa * forca * dobra).rgb;
	ALPHA = clamp(faixa * forca * 1.3, 0.0, 1.0);
}
