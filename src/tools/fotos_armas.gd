extends SceneTree
## A foto de cada arma para a interface (loja, inventário, HUD), em GDScript:
## o mesmo que scripts/tools/WeaponPhotos.cs, para rodar sem compilar o C#.
## Monta a arma como o WeaponModel (assets/armas/armas.json: malha e
## textura _D/_E/_ORM/_N de cada peça), de lado com o cano para a direita,
## luz de estúdio, fundo transparente. Grava assets/ui/armas/<id>.png (768×384).
##
##     godot --path . -s tools/fotos_armas.gd -- faca choque choque_ouro
##
## Sem ids, tira todas as de armas.json. Precisa de vídeo (sem --headless).

const W := 1536
const H := 768

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var folha: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://src/assets/armas/armas.json"))
	var armas: Dictionary = folha["armas"]
	var ids: Array = Array(OS.get_cmdline_user_args())
	if ids.is_empty(): ids = armas.keys()
	var saida := ProjectSettings.globalize_path("res://src/assets/ui/armas/")
	DirAccess.make_dir_recursive_absolute(saida)
	for id in ids:
		if not armas.has(id):
			push_error("arma desconhecida: %s" % id); continue
		var palco := _palco(id, armas[id])
		root.add_child(palco)
		for i in 4: await process_frame
		await RenderingServer.frame_post_draw
		var foto := palco.get_texture().get_image()
		foto.resize(W / 2, H / 2, Image.INTERPOLATE_LANCZOS)
		foto.save_png(saida + id + ".png")
		print("foto: ", id)
		root.remove_child(palco)
		palco.queue_free()
	quit()

func _palco(id: String, arma: Dictionary) -> SubViewport:
	var palco := SubViewport.new()
	palco.size = Vector2i(W, H)
	palco.transparent_bg = true
	palco.own_world_3d = true
	palco.msaa_3d = Viewport.MSAA_4X
	palco.render_target_update_mode = SubViewport.UPDATE_ALWAYS

	var ceu := ProceduralSkyMaterial.new()
	ceu.sky_top_color = Color(0.82, 0.86, 0.92)
	ceu.sky_horizon_color = Color(0.95, 0.95, 0.96)
	ceu.ground_horizon_color = Color(0.7, 0.72, 0.76)
	ceu.ground_bottom_color = Color(0.32, 0.34, 0.38)
	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	env.sky = Sky.new(); env.sky.sky_material = ceu
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_energy = 1.1
	env.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	var we := WorldEnvironment.new(); we.environment = env
	palco.add_child(we)

	palco.add_child(_luz(Vector3(-48, -28, 0), 2.4, Color(1, 0.97, 0.92)))
	palco.add_child(_luz(Vector3(20, -150, 0), 0.7, Color(0.8, 0.88, 1)))
	palco.add_child(_luz(Vector3(-30, 165, 0), 1.5, Color.WHITE))

	var modelo := Node3D.new()
	var caixa := AABB()
	var primeira := true
	for parte in arma["partes"]:
		var m := MeshInstance3D.new()
		m.mesh = load(parte["malha"])
		m.material_override = _material(parte["material"])
		modelo.add_child(m)
		var b := m.mesh.get_aabb()
		caixa = b if primeira else caixa.merge(b)
		primeira = false
	var pivo := Node3D.new()
	pivo.add_child(modelo)
	palco.add_child(pivo)
	modelo.position = -caixa.get_center()
	# A pose de cada tipo (a mesma do WeaponPhotos.cs).
	pivo.basis = _pose(id)
	if pivo.basis != Basis.IDENTITY:
		caixa = _caixa(pivo)
		pivo.position = -caixa.get_center()

	# A câmera olha de +X: o cano (−Z) aponta para a direita da foto.
	var tamanho: float = max(caixa.size.y * 1.3, caixa.size.z * 1.12 / (float(W) / H))
	var cam := Camera3D.new()
	cam.projection = Camera3D.PROJECTION_ORTHOGONAL
	cam.size = tamanho
	cam.near = 0.05
	cam.far = 10.0
	palco.add_child(cam)
	cam.look_at_from_position(Vector3(2.6, 0.42, 0.5), Vector3.ZERO, Vector3.UP)
	cam.current = true
	return palco

func _luz(graus: Vector3, forca: float, cor: Color) -> DirectionalLight3D:
	var l := DirectionalLight3D.new()
	l.rotation_degrees = graus
	l.light_energy = forca
	l.light_color = cor
	return l

# O mesmo material do WeaponModel.Material para as texturas de peles.py.
func _material(nome: String) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	if not nome.begins_with("res://"):
		m.albedo_color = Color(0.5, 0.5, 0.5); return m
	m.albedo_texture = load(nome)
	m.roughness = 0.6
	m.metallic_specular = 0.5
	var base := nome.substr(0, nome.length() - 6) if nome.ends_with("_D.png") else ""
	if base != "" and ResourceLoader.exists(base + "_E.png"):
		m.emission_enabled = true
		m.emission_operator = BaseMaterial3D.EMISSION_OP_MULTIPLY
		m.emission = Color.WHITE
		m.emission_energy_multiplier = 1.6
		m.emission_texture = load(base + "_E.png")
		m.roughness = 0.38
		m.metallic = 0.35
	if base != "" and ResourceLoader.exists(base + "_ORM.png"):
		var orm: Texture2D = load(base + "_ORM.png")
		m.roughness_texture = orm
		m.roughness_texture_channel = BaseMaterial3D.TEXTURE_CHANNEL_GREEN
		m.metallic_texture = orm
		m.metallic_texture_channel = BaseMaterial3D.TEXTURE_CHANNEL_BLUE
		m.roughness = 1.0
		m.metallic = 1.0
	if base != "" and ResourceLoader.exists(base + "_N.png"):
		m.normal_enabled = true
		m.normal_texture = load(base + "_N.png")
	return m

# Faca: com a face da lâmina para a câmera, a ponta subindo para a direita.
# Granada: girada para a argola ficar na frente e a alavanca no perfil.
# (A mesma pose do WeaponPhotos.cs.)
func _pose(id: String) -> Basis:
	if id.begins_with("faca"):
		return Basis(Vector3.RIGHT, deg_to_rad(-35.0))
	if id.begins_with("choque"):
		return Basis(Vector3.UP, deg_to_rad(120.0))
	return Basis.IDENTITY

func _caixa(n: Node, pai := Transform3D.IDENTITY) -> AABB:
	var aqui: Transform3D = pai * n.transform if n is Node3D else pai
	var total := AABB()
	var vazio := true
	if n is MeshInstance3D and n.mesh:
		total = aqui * n.mesh.get_aabb(); vazio = false
	for c in n.get_children():
		var b := _caixa(c, aqui)
		if b.size == Vector3.ZERO: continue
		total = b if vazio else total.merge(b); vazio = false
	return total
