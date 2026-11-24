// O chão da explosão (Explosion.cs): a onda de choque — um anel fino de luz que corre até o raio — e a mancha de calor
// que fica acesa no meio. Um quadrado deitado; `vida` de 0 a 1.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;
uniform float vida = 0.0;
void fragment() {
	float r = length(UV - 0.5) * 2.0;
	float frente = 1.0 - pow(1.0 - clamp(vida * 2.6, 0.0, 1.0), 3.0);
	float anel = smoothstep(0.09, 0.0, abs(r - frente * 0.96)) * (1.0 - clamp(vida * 2.6, 0.0, 1.0)) * step(r, 1.0);
	float calor = smoothstep(0.42, 0.0, r) * pow(1.0 - vida, 2.0);
	vec3 c = vec3(1.0, 0.9, 0.7) * anel * 2.2 + mix(vec3(1.0, 0.25, 0.03), vec3(1.0, 0.75, 0.3), calor) * calor * 2.4;
	ALBEDO = c; ALPHA = clamp(anel + calor, 0.0, 1.0);
}
