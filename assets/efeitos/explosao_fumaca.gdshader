// A fumaça que fica no lugar do fogo (Explosion.cs): a mesma esfera revolta, escura, que sobe, abre e some.
shader_type spatial;
render_mode unshaded, blend_mix, cull_back, depth_draw_never, shadows_disabled;
uniform float vida = 0.0;
uniform float semente = 0.0;
varying vec3 dir;
float h3(vec3 p) { p = fract(p * 0.3183099 + 0.1); p *= 17.0; return fract(p.x * p.y * p.z * (p.x + p.y + p.z)); }
float ruido(vec3 x) {
	vec3 i = floor(x); vec3 f = fract(x); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(h3(i), h3(i + vec3(1, 0, 0)), f.x), mix(h3(i + vec3(0, 1, 0)), h3(i + vec3(1, 1, 0)), f.x), f.y),
		mix(mix(h3(i + vec3(0, 0, 1)), h3(i + vec3(1, 0, 1)), f.x), mix(h3(i + vec3(0, 1, 1)), h3(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}
float fbm(vec3 p) { float a = 0.5; float s = 0.0; for (int i = 0; i < 4; i++) { s += a * ruido(p); p *= 2.03; a *= 0.5; } return s; }
void vertex() {
	dir = normalize(VERTEX);
	VERTEX *= 0.7 + 0.6 * fbm(dir * 1.9 + semente + vec3(0.0, -vida * 0.8, 0.0));
}
void fragment() {
	float n = fbm(dir * 3.2 + semente + vec3(0.0, -vida * 1.2, 0.0));
	float frente = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
	float brasa = smoothstep(0.62, 0.9, n) * (1.0 - smoothstep(0.0, 0.45, vida));
	ALBEDO = mix(vec3(0.045, 0.04, 0.038) * (0.6 + 0.9 * n), vec3(1.6, 0.5, 0.08), brasa);
	ALPHA = smoothstep(0.12 + 0.5 * vida, 0.5 + 0.4 * vida, n + 0.25) * pow(frente, 0.8) * smoothstep(0.0, 0.12, vida) * (1.0 - smoothstep(0.55, 1.0, vida)) * 0.9;
}
