extends SceneTree
## Gera a miniatura de cada mapa vista de cima, para a lista de salas e a
## tela de sala. Precisa de renderização (não roda com --headless):
##
##     godot --path . --rendering-driver opengl3 -s tools/gerar_miniaturas.gd
##
## Saída: assets/ui/mapas/<id>.png, 512 × 288.

## As miniaturas dos mapas gerados (metro, vila, polar) saem do próprio
## gerador (tools/gerar_mapas.py); esta ferramenta fica para o Hangar e
## para mapas feitos à mão.
const MAPS := {
	"hangar": "res://scenes/maps/hangar.tscn",
}

const SIZE := Vector2i(512, 288)

func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://assets/ui/mapas"))
	_run.call_deferred()

func _run() -> void:
	for id in MAPS:
		await _render(id, MAPS[id])
	quit()

func _render(id: String, path: String) -> void:
	var viewport := SubViewport.new()
	viewport.size = SIZE
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport.own_world_3d = true
	root.add_child(viewport)

	var map: Node3D = load(path).instantiate()
	viewport.add_child(map)

	var env := WorldEnvironment.new()
	env.environment = Environment.new()
	env.environment.background_mode = Environment.BG_COLOR
	env.environment.background_color = Color("0f1318")
	env.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.environment.ambient_light_color = Color(1, 1, 1)
	env.environment.ambient_light_energy = 0.3
	viewport.add_child(env)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-62, 30, 0)
	sun.light_energy = 0.85
	sun.shadow_enabled = true
	viewport.add_child(sun)

	# Câmera ortográfica de cima, levemente inclinada para as alturas
	# aparecerem: miniatura chapada não mostra o que só o lobo alcança.
	var cam := Camera3D.new()
	cam.projection = Camera3D.PROJECTION_ORTHOGONAL
	cam.size = 52.0
	cam.position = Vector3(0, 60, 22)
	cam.rotation_degrees = Vector3(-70, 0, 0)
	cam.far = 200.0
	viewport.add_child(cam)
	cam.make_current()

	for i in 8:
		await process_frame

	var image := viewport.get_texture().get_image()
	var out := "res://assets/ui/mapas/%s.png" % id
	image.save_png(ProjectSettings.globalize_path(out))
	print("miniatura: ", out)
	viewport.queue_free()
