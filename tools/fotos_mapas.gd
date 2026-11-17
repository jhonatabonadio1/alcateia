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
}

const SIZE := Vector2i(1920, 1080)

func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://assets/ui/mapas"))
	_run.call_deferred()

func _run() -> void:
	var pedidos := OS.get_cmdline_user_args()
	for id in FOTOS:
		if pedidos.is_empty() or pedidos.has(id):
			await _foto(id, FOTOS[id])
	quit()

func _foto(id: String, f: Dictionary) -> void:
	var viewport := SubViewport.new()
	viewport.size = SIZE
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport.own_world_3d = true
	viewport.msaa_3d = Viewport.MSAA_4X
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

	# Uns quadros para sombras, sondas de reflexo e SSAO assentarem.
	for i in 30:
		await process_frame

	var image := viewport.get_texture().get_image()
	var out := "res://assets/ui/mapas/%s_foto.png" % id
	image.save_png(ProjectSettings.globalize_path(out))
	print("foto: ", out)
	viewport.queue_free()
	await process_frame
