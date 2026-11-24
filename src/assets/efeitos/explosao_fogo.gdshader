// A bola de fogo da explosão (Explosion.cs): uma esfera de superfície revolta que nasce branca de tão quente, vira
// amarela, laranja e vermelho-escura enquanto incha, e se desfaz em buracos. `vida` vai de 0 a 1.
shader_type spatial;
render_mode unshaded, blend_add, cull_back, depth_draw_never, shadows_disabled, fog_disabled;
uniform float vida = 0.0;
uniform float semente = 0.0;
uniform float forca = 1.0;
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
	float n = fbm(dir * 2.1 + semente + vec3(0.0, -vida * 1.6, 0.0));
	VERTEX *= 0.72 + 0.56 * n;
}
void fragment() {
	float n = fbm(dir * 3.6 + semente * 1.7 + vec3(0.0, -vida * 2.4, 0.0));
	float frente = pow(clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 0.7);
	float calor = clamp(n * 1.3 + frente * 0.6 - vida * 1.25, 0.0, 1.0);
	vec3 c = mix(vec3(0.45, 0.04, 0.0), vec3(1.0, 0.36, 0.04), smoothstep(0.0, 0.35, calor));
	c = mix(c, vec3(1.0, 0.80, 0.34), smoothstep(0.3, 0.62, calor));
	c = mix(c, vec3(1.0, 0.97, 0.88), smoothstep(0.58, 0.92, calor));
	float a = smoothstep(0.0, 0.22, calor) * frente * (1.0 - smoothstep(0.7, 1.0, vida));
	ALBEDO = c * (1.2 + 4.5 * calor) * forca;
	ALPHA = a;
}
