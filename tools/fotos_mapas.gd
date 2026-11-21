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
## A capa tem cara de arte de mapa de jogo de herói (pedido do Jhon, 20/11:
## "melhore as screenshots, deixe-as com aspecto Valorant e Overwatch; veja
## nas thumbs o leve efeito de blur embaixo, nos cantos, bem sutil"): a
## câmera baixa e aberta, olhando um pouco para cima; a cor viva (mais
## saturação, o claro quente e a sombra fria, brilho nas luzes); e a imagem
## desfocada de leve na faixa de baixo e nos cantos, com uma vinheta fraca —
## o olho vai para o meio. É só na foto — o mapa do jogo não muda. (A capa de
## antes, de 20/11 cedo, era "de guerra": névoa de poeira, fumaça, cor dura.)
## "limpa" no fim da linha de comando tira a foto sem nada disso, e
## "rascunho" tira pequena e rápida (960 × 540), para acertar o ângulo.
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
		# Do meio da rua, na altura de quem joga, olhando um pouco para cima: a viatura, a barreira, a torre de
		# vidro, os prédios de tijolo e o centro ao fundo.
		"de": Vector3(-40.0, 2.0, 0.0),
		"para": Vector3(-10.0, 7.0, 0.0),
		"fov": 75.0,
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
	"neon": {
		"cena": "res://scenes/maps/neon.tscn",
		# Do meio da rua norte do quarteirão leste, baixo, olhando a rua, o hospital, as torres acesas e o céu.
		"de": Vector3(20.0, 1.7, -18.0),
		"para": Vector3(50.0, 5.0, -18.0),
		"fov": 78.0,
	},
}

const SIZE := Vector2i(1920, 1080)

## O acabamento da capa, por mapa: quanto de névoa colorida entra no ar (só
## nos abertos) e de que cor; "noite" não aquece o claro.
const CAPA := {
	"armazem_estendido": {"nevoa": 0.0, "ar": Color(0.70, 0.78, 1.0)},
	"armazem": {"nevoa": 0.0, "ar": Color(0.70, 0.78, 1.0)},
	"cidade": {"nevoa": 0.0035, "ar": Color(0.72, 0.84, 1.0)},
	"deserto": {"nevoa": 0.0035, "ar": Color(1.0, 0.86, 0.68)},
	"estacao": {"nevoa": 0.0045, "ar": Color(1.0, 0.78, 0.60)},
	"obra": {"nevoa": 0.0035, "ar": Color(0.74, 0.86, 1.0)},
	"neon": {"nevoa": 0.0, "ar": Color(0.5, 0.3, 0.8), "noite": true},
}

const GRADE := """
shader_type canvas_item;
uniform sampler2D tela : hint_screen_texture, filter_linear, repeat_disable;
uniform float forca = 1.0;
uniform float quente = 1.0;
uniform float raio = 6.5;          // o desfoque, em px da foto inteira (1920)
void fragment() {
	vec2 q = SCREEN_UV - 0.5;
	// onde desfoca: a faixa de baixo (de 68% para baixo) e os cantos; o meio fica nítido
	float baixo = smoothstep(0.66, 1.0, SCREEN_UV.y);
	float canto = smoothstep(0.52, 0.92, length(q * vec2(1.0, 1.25)));
	float f = clamp(max(baixo * 0.9, canto), 0.0, 1.0) * forca;
	vec2 px = SCREEN_PIXEL_SIZE * raio * f * (1.0 / SCREEN_PIXEL_SIZE.x) / 1920.0;
	vec3 c = texture(tela, SCREEN_UV).rgb;
	float peso = 1.0;
	for (int i = 0; i < 24; i++) {                                    // um disco de 24 pontos (espiral dourada)
		float a = float(i) * 2.39996; float r = sqrt((float(i) + 0.5) / 24.0);
		c += texture(tela, SCREEN_UV + vec2(cos(a), sin(a)) * r * px).rgb; peso += 1.0;
	}
	c /= peso;
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	c = mix(vec3(l), c, 1.0 + 0.16 * forca);                          // mais cor
	c = mix(c, c * c * (3.0 - 2.0 * c), 0.22 * forca);                 // um pouco mais de contraste
	c *= mix(vec3(1.0), mix(vec3(0.94, 0.98, 1.08), vec3(1.06, 1.0, 0.93), smoothstep(0.2, 0.8, l)), forca * quente);   // sombra fria, luz quente
	c *= 1.0 - 0.30 * forca * smoothstep(0.35, 1.0, length(q * vec2(1.0, 0.85)));   // vinheta fraca
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
	cam.far = 900.0
	viewport.add_child(cam)
	cam.look_at_from_position(f["de"], f["para"], Vector3.UP)
	cam.make_current()
	if not _limpa:
		_capa(viewport, map, CAPA.get(id, {}))

	# Uns quadros para sombras, sondas de reflexo e SSAO assentarem.
	for i in (8 if _rascunho else 30):
		await process_frame

	var image := viewport.get_texture().get_image()
	var out := "res://assets/ui/mapas/%s_foto.png" % id
	image.save_png(ProjectSettings.globalize_path(out))
	print("foto: ", out)
	viewport.queue_free()
	await process_frame


## O acabamento da capa: o ar do mapa com um pouco mais de névoa colorida e
## de brilho, e por cima da imagem a cor viva e o desfoque leve embaixo e nos
## cantos. Mexe numa cópia do ambiente — a cena do mapa fica como está.
func _capa(viewport: SubViewport, map: Node3D, g: Dictionary) -> void:
	for no in map.find_children("*", "WorldEnvironment", true, false):
		var we := no as WorldEnvironment
		if we.environment == null:
			continue
		var env: Environment = we.environment.duplicate()
		if float(g.get("nevoa", 0.0)) > 0.0:
			env.fog_enabled = true
			env.fog_light_color = g.get("ar", Color(0.75, 0.85, 1.0))
			env.fog_density = maxf(env.fog_density, float(g["nevoa"]))
			env.fog_aerial_perspective = maxf(env.fog_aerial_perspective, 0.6)
			env.fog_sun_scatter = maxf(env.fog_sun_scatter, 0.3)
		env.glow_enabled = true
		env.glow_intensity = maxf(env.glow_intensity, 0.8)
		env.glow_bloom = maxf(env.glow_bloom, 0.14)
		we.environment = env

	var camada := CanvasLayer.new()
	viewport.add_child(camada)
	var folha := ColorRect.new()
	folha.set_anchors_preset(Control.PRESET_FULL_RECT)
	var grade := Shader.new()
	grade.code = GRADE
	var gm := ShaderMaterial.new()
	gm.shader = grade
	gm.set_shader_parameter("quente", 0.0 if g.get("noite", false) else 1.0)
	folha.material = gm
	camada.add_child(folha)
