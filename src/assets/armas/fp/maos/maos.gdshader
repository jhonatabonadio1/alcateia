// As mãos de primeira pessoa (tools/armas/maos.py). O LED do bracelete acende na cor do time: o jogo põe ``led`` por
// instância (scripts/ui/ViewModel.Maos.cs); sem isso fica o azul.
shader_type spatial;
render_mode cull_back;
uniform sampler2D cor : source_color, filter_linear_mipmap_anisotropic;
uniform sampler2D orm : hint_default_white, filter_linear_mipmap_anisotropic;
uniform sampler2D relevo : hint_normal, filter_linear_mipmap_anisotropic;
uniform sampler2D luz : hint_default_black, filter_linear_mipmap;
uniform float relevo_forca = 1.0;
uniform float led_forca = 3.2;
instance uniform vec3 led : source_color = vec3(0.10, 0.50, 1.0);

void fragment() {
	vec3 c = texture(cor, UV).rgb;
	vec3 o = texture(orm, UV).rgb;
	float aceso = texture(luz, UV).r;
	ALBEDO = mix(c, led, aceso * 0.7);
	AO = o.r;
	AO_LIGHT_AFFECT = 0.35;
	ROUGHNESS = o.g;
	METALLIC = o.b;
	NORMAL_MAP = texture(relevo, UV).rgb;
	NORMAL_MAP_DEPTH = relevo_forca;
	EMISSION = led * aceso * led_forca;
	RIM = 0.12 * (1.0 - o.b);
	RIM_TINT = 0.6;
}
