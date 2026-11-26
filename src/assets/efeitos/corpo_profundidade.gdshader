// O corpo morto sumindo: a profundidade, sem cor, antes da cor (CorpseFade;
// Jhon, 26/11: "deve ficar transparente por igual, sem poder ver o interior do
// corpo"). Para os materiais comuns (a Eve, o Barut, qualquer personagem); o
// lobo tem a dele (lobo_profundidade.gdshader), com o pelo que balança.
//
// Por que existe: o "AlphaDepthPrePass" do Godot só escreve a profundidade onde
// o alfa passa de 0,99. Desbotando, o alfa cai abaixo disso no primeiro quadro,
// a profundidade some, e os pedaços do mesmo corpo se desenham fora de ordem:
// os olhos, os dentes, o cabelo de baixo e o forro apareciam por cima da pele.
// Esta passada escreve a profundidade do corpo inteiro (ALPHA 0, nada de cor),
// e a cor que vem depois (render_priority maior) só passa na pele de fora.
shader_type spatial;
render_mode cull_disabled, unshaded, depth_draw_always, shadows_disabled, ambient_light_disabled, fog_disabled;

// O recorte da textura (cabelo em cartão, cílios): sem ele, a profundidade do
// cartão inteiro tapava o que está atrás da parte vazia.
uniform sampler2D albedo_tex : source_color, filter_linear_mipmap;
uniform bool use_cutout = false;
uniform float cutout = 0.5;
uniform vec4 albedo : source_color = vec4(1.0);
uniform vec3 uv1_scale = vec3(1.0);
uniform vec3 uv1_offset = vec3(0.0);
// Quanto a profundidade fica atrás da pele (m): a cor da própria pele passa no
// teste; o que está mais fundo (olhos, boca, forro) não.
uniform float push_back = 0.0008;

void vertex() {
	UV = UV * uv1_scale.xy + uv1_offset.xy;
	vec4 view = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	view.xyz += normalize(view.xyz) * push_back;
	POSITION = PROJECTION_MATRIX * view;
}

void fragment() {
	if (use_cutout && texture(albedo_tex, UV).a * albedo.a < cutout) {
		discard;
	}
	ALBEDO = vec3(0.0);
	ALPHA = 0.0;
}
