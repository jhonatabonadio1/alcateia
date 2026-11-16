// Contorno de luz no corpo do lobo (Fresnel), por cima do material dele
// (Material Overlay). A cor e o quanto aparece são de cada lobo
// (instance uniform): dois lobos com o mesmo material não se afetam.
// Quem liga e desliga: scripts/visual/fx/WolfFx.cs.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back;

// Índices fixos (instance_index): a aura e o vento ficam na mesma malha, e
// sem eles os dois shaders disputam as mesmas posições — o vento lia a
// cor da aura e o lobo inteiro ficava rosa.
instance uniform vec4 aura_color : source_color, instance_index(0) = vec4(0.2, 0.6, 1.0, 1.0);
instance uniform float aura_amount : hint_range(0.0, 1.0), instance_index(1) = 0.0;
uniform float intensity = 3.0;
uniform float power = 3.0;

void fragment() {
	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), power);
	float pulse = 0.85 + 0.15 * sin(TIME * 4.0);
	ALBEDO = aura_color.rgb * intensity * pulse;
	ALPHA = clamp(fresnel * aura_amount, 0.0, 1.0);
}
