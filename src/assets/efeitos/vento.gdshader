// O ar em volta do lobo em alta velocidade (de quatro, salto, bote): um
// brilho frio e fraco nas bordas do corpo (Fresnel). Os riscos de vento
// passando são partículas (scripts/visual/fx/WolfFx.cs, "Riscos"):
// riscos desenhados sobre a pele viravam manchas brancas, porque o corpo
// curvo estica qualquer padrão projetado nele.
// Encadeado no Next Pass da aura.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back;

uniform vec4 air_color : source_color = vec4(0.8, 0.9, 1.0, 1.0);
uniform float intensity = 0.9;
uniform float power = 2.5;
// Índices fixos, depois dos da aura (aura.gdshader: 0 e 1).
instance uniform float speed_amount : hint_range(0.0, 1.0), instance_index(2) = 0.0;
instance uniform vec3 move_dir : instance_index(3) = vec3(0.0, 0.0, -1.0);

void fragment() {
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), power);
	ALBEDO = air_color.rgb * intensity;
	ALPHA = clamp(rim * 0.18 * speed_amount, 0.0, 1.0);
}
