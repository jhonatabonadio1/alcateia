extends SceneTree
## Tira a foto de cada mapa no próprio jogo — a luz, o céu e as sombras do
## Godot — de um ângulo bom, para a escolha do mapa, a sala e a tela de
## carregamento (scripts/meta/Maps.cs, MapInfo.Cover). Precisa de
## renderização (não roda com --headless):
##
##     godot --path . -s tools/fotos_mapas.gd            # todos
##     godot --path . -s tools/fotos_mapas.gd -- armazem # um só
##
## Saída: assets/ui/mapas/<id>_foto.png, 1920 × 1080.
##
## A capa tem clima de guerra (pedido do Jhon, 20/11: "arrume as thumbnails,
## deixe-as mais com aspecto de guerra"): névoa de poeira, colunas de fumaça
## ao longe, o céu mais baixo e a cor mais dura (menos saturada, mais
## contraste, vinheta). É só na foto — o mapa do jogo não muda. O quanto de
## cada coisa está em GUERRA, logo abaixo de FOTOS; "limpa" no fim da linha de
## comando tira a foto sem nada disso, e "rascunho" tira pequena e rápida
## (960 × 540), para acertar o ângulo.
##
## Mapa novo: uma linha em FOTOS, com a cena, de onde a câmera olha e para
## onde (coordenadas do jogo: X dos times, Y para cima), e o campo de visão
## vertical, em graus.
## Para achar o ângulo, ande no mapa e anote a posição.

const FOTOS := {
	"armazem": {
		"cena": "res://scenes/maps/armazem.tscn",
		# Do canto do time laranja, no alto, olhando o galpão inteiro.
		"de": Vector3(21.5, 7.0, -13.5),
		"para": Vector3(-2.0, 0.5, 2.0),
		"fov": 59.0,
	},
	"armazem_estendido": {
		"cena": "res://scenes/maps/armazem_estendido.tscn",
		# Do canto do time laranja, no alto, junto à passarela sul.
		"de": Vector3(36.2, 9.6, -19.6),
		"para": Vector3(-4.0, 1.0, 4.0),
		"fov": 59.0,
	},
	"cidade": {
		"cena": "res://scenes/maps/cidade.tscn",
		# Do alto da esquina da transversal oeste, olhando a avenida, a banca, a obra e o bulevar.
		"de": Vector3(-36.0, 9.5, -5.5),
		"para": Vector3(-6.0, 1.5, 2.5),
		"fov": 64.0,
	},
	"deserto": {
		"cena": "res://scenes/maps/deserto.tscn",
		# De cima da casa da praça, olhando o chafariz, as palmeiras, a muralha e a serra.
		"de": Vector3(-14.0, 7.0, -6.0),
		"para": Vector3(6.0, 0.5, 2.0),
		"fov": 65.0,
	},
	"estacao": {
		"cena": "res://scenes/maps/estacao.tscn",
		# De cima da passarela, olhando a plataforma, os trens, as torres de relógio e os prédios do centro.
		"de": Vector3(-2.0, 8.2, 10.5),
		"para": Vector3(-52.0, 3.0, 12.5),
		"fov": 72.0,
	},
	"obra": {
		"cena": "res://scenes/maps/obra.tscn",
		# De cima da laje mais alta, olhando o prédio aberto (as muretas, o material, as colunas), o canteiro, as
		# gruas, o muro e os prédios da cidade.
		"de": Vector3(30.0, 10.5, -19.0),
		"para": Vector3(-12.0, 1.5, 6.0),
		"fov": 68.0,
	},
}

const SIZE := Vector2i(1920, 1080)

## O clima de guerra de cada capa: a névoa (densidade), quantas colunas de
## fumaça ao longe e a cor da poeira. Mapa fechado não tem fumaça nem céu.
const GUERRA := {
	"armazem": {"nevoa": 0.006, "fumaca": 0, "poeira": Color(0.60, 0.52, 0.44)},
	"armazem_estendido": {"nevoa": 0.005, "fumaca": 0, "poeira": Color(0.60, 0.52, 0.44)},
	"cidade": {"nevoa": 0.011, "fumaca": 4, "poeira": Color(0.56, 0.52, 0.48)},
	"deserto": {"nevoa": 0.010, "fumaca": 3, "poeira": Color(0.72, 0.58, 0.42)},
	"estacao": {"nevoa": 0.011, "fumaca": 4, "poeira": Color(0.60, 0.52, 0.45)},
	"obra": {"nevoa": 0.011, "fumaca": 4, "poeira": Color(0.58, 0.52, 0.47)},
}

const FUMACA := """
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, fog_disabled;
uniform float semente = 0.0;
uniform vec3 cor : source_color = vec3(0.10, 0.09, 0.085);
uniform vec3 brasa : source_color = vec3(0.85, 0.36, 0.10);
float h(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7)) + semente * 17.0) * 43758.5453); }
float n(vec2 p) { vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h(i), h(i + vec2(1.0, 0.0)), f.x), mix(h(i + vec2(0.0, 1.0)), h(i + vec2(1.0, 1.0)), f.x), f.y); }
float fbm(vec2 p) { float a = 0.5; float s = 0.0; for (int i = 0; i < 5; i++) { s += a * n(p); p *= 2.03; a *= 0.5; } return s; }
void fragment() {
	float y = 1.0 - UV.y;                                             // 0 no pé, 1 no alto
	float vento = (fbm(vec2(y * 3.0, semente)) - 0.5) * 0.45 * y + 0.2 * y * y;
	float largura = mix(0.05, 0.36, pow(y, 0.75));
	float d = abs(UV.x - 0.42 - vento) / largura;
	float corpo = smoothstep(1.0, 0.15, d);
	float rolos = fbm(vec2(UV.x * 7.0, y * 10.0) + semente * 3.1);
	float a = corpo * smoothstep(0.22, 0.7, rolos + 0.4 * corpo) * smoothstep(1.0, 0.7, y) * smoothstep(0.0, 0.03, y);
	ALBEDO = mix(cor * (0.7 + 1.5 * rolos), brasa, smoothstep(0.12, 0.0, y) * 0.55);
	ALPHA = a * 0.82;
}
"""

const GRADE := """
shader_type canvas_item;
uniform sampler2D tela : hint_screen_texture, filter_linear;
uniform float forca = 1.0;
float h(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
void fragment() {
	vec3 c = texture(tela, SCREEN_UV).rgb;
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	c = mix(vec3(l), c, 1.0 - 0.26 * forca);                          // menos cor
	c = mix(c, c * c * (3.0 - 2.0 * c), 0.45 * forca);                 // mais contraste
	c *= mix(vec3(1.0), mix(vec3(0.90, 1.0, 1.06), vec3(1.08, 1.0, 0.90), smoothstep(0.15, 0.75, l)), forca);   // sombra fria, luz quente
	vec2 q = SCREEN_UV - 0.5;
	c *= 1.0 - 0.62 * forca * smoothstep(0.28, 0.95, length(q * vec2(1.0, 0.82)));   // vinheta
	c += (h(SCREEN_UV * 917.0) - 0.5) * 0.035 * forca;                // grão
	COLOR = vec4(c, 1.0);
}
"""

var _limpa := false
var _rascunho := false

func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://assets/ui/mapas"))
	_run.call_deferred()

func _run() -> void:
	var pedidos := Array(OS.get_cmdline_user_args())
	_limpa = pedidos.has("limpa"); pedidos.erase("limpa")
	_rascunho = pedidos.has("rascunho"); pedidos.erase("rascunho")
	for id in FOTOS:
		if pedidos.is_empty() or pedidos.has(id):
			await _foto(id, FOTOS[id])
	quit()

func _foto(id: String, f: Dictionary) -> void:
	var viewport := SubViewport.new()
	viewport.size = SIZE / 2 if _rascunho else SIZE
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport.own_world_3d = true
	viewport.msaa_3d = Viewport.MSAA_DISABLED if _rascunho else Viewport.MSAA_4X
	viewport.screen_space_aa = Viewport.SCREEN_SPACE_AA_FXAA
	root.add_child(viewport)

	# O mapa traz o próprio céu, o sol e as luzes: é a foto do jogo.
	var map: Node3D = load(f["cena"]).instantiate()
	viewport.add_child(map)

	var cam := Camera3D.new()
	cam.fov = f.get("fov", 60.0)
	cam.far = 300.0
	viewport.add_child(cam)
	cam.look_at_from_position(f["de"], f["para"], Vector3.UP)
	cam.make_current()
	if not _limpa and GUERRA.has(id):
		_guerra(viewport, map, cam, GUERRA[id])

	# Uns quadros para sombras, sondas de reflexo e SSAO assentarem.
	for i in (8 if _rascunho else 30):
		await process_frame

	var image := viewport.get_texture().get_image()
	var out := "res://assets/ui/mapas/%s_foto.png" % id
	image.save_png(ProjectSettings.globalize_path(out))
	print("foto: ", out)
	viewport.queue_free()
	await process_frame


## O clima de guerra da capa: névoa de poeira no ar do mapa, colunas de
## fumaça ao longe (atrás do que fecha o mapa, viradas para a câmera) e a
## cor mais dura por cima da imagem. Mexe numa cópia do ambiente — a cena do
## mapa fica como está.
func _guerra(viewport: SubViewport, map: Node3D, cam: Camera3D, g: Dictionary) -> void:
	for no in map.find_children("*", "WorldEnvironment", true, false):
		var we := no as WorldEnvironment
		if we.environment == null:
			continue
		var env: Environment = we.environment.duplicate()
		var poeira: Color = g["poeira"]
		env.fog_enabled = true
		env.fog_light_color = poeira
		env.fog_density = maxf(env.fog_density, float(g["nevoa"]))
		env.fog_sky_affect = 0.62
		env.fog_sun_scatter = 0.25
		env.background_energy_multiplier *= 0.78
		we.environment = env

	var frente := -cam.global_transform.basis.z
	frente.y = 0.0
	frente = frente.normalized()
	var lado := frente.cross(Vector3.UP)
	var shader := Shader.new()
	shader.code = FUMACA
	for i in int(g["fumaca"]):
		var quad := QuadMesh.new()
		var altura: float = 80.0 + 24.0 * float(i % 3)
		quad.size = Vector2(altura * 0.8, altura)
		var mat := ShaderMaterial.new()
		mat.shader = shader
		mat.set_shader_parameter("semente", 1.7 + 2.3 * i)
		quad.material = mat
		var mi := MeshInstance3D.new()
		mi.mesh = quad
		mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		viewport.add_child(mi)
		var a: float = (float(i) - (float(g["fumaca"]) - 1.0) * 0.5) * 0.42 + 0.11 * float(i % 2)
		var onde: Vector3 = cam.global_position + (frente * cos(a) + lado * sin(a)) * (105.0 + 30.0 * float(i % 2))
		mi.global_position = Vector3(onde.x, altura * 0.5 - 4.0, onde.z)
		mi.look_at(Vector3(cam.global_position.x, mi.global_position.y, cam.global_position.z), Vector3.UP, true)

	var camada := CanvasLayer.new()
	viewport.add_child(camada)
	var folha := ColorRect.new()
	folha.set_anchors_preset(Control.PRESET_FULL_RECT)
	var grade := Shader.new()
	grade.code = GRADE
	var gm := ShaderMaterial.new()
	gm.shader = grade
	folha.material = gm
	camada.add_child(folha)
