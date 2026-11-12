extends SceneTree
## Mede os golpes do lobo nas animações: para cada clipe, onde está cada
## mão quadro a quadro, quanto ela anda e quando ela está mais à frente.
## O golpe é o pico de velocidade da mão que bate.
##
## Também mede o corpo: o trecho em que o clipe se mexe de verdade (para
## cortar a parte parada do começo e do fim), o passo do quadril (o jogo
## prende o quadril e anda o corpo no lugar dele) e, nos saltos, o quadro
## em que os pés voltam ao chão.
##
## Uso, da raiz do projeto:
##   godot --headless --path . -s res://tools/analisar_golpes.gd
##
## Os números vão para scripts/forms/WolfMoves.cs (Start, End, Impact,
## StepFrom, StepTo, Step).
## A posição das mãos é reconstruída somando osso por osso (repouso do
## esqueleto + trilhas do clipe), sem tocar a animação — no modo sem
## janela o esqueleto não atualiza a pose.

const MODELO := "res://assets/characters/wolf/mutant.fbx"
const CLIPES := ["claw", "heavy", "leap", "howl", "transform", "wallrun"]

var skeleton: Skeleton3D


func _init() -> void:
	var model: Node = load(MODELO).instantiate()
	skeleton = _find_skeleton(model)

	for clip in CLIPES:
		var path := "res://assets/characters/wolf/%s.fbx" % clip
		if not ResourceLoader.exists(path):
			continue

		var source: Node = load(path).instantiate()
		var anim := _take(source)
		source.free()
		if anim == null:
			continue

		print("== %s  (%.2f s)" % [clip, anim.length])
		var hips := skeleton.find_bone("mixamorig_Hips")

		for side in ["Left", "Right"]:
			var hand := skeleton.find_bone("mixamorig_%sHand" % side)
			var previous = null
			var front := -99.0
			var front_at := 0.0
			var fastest := 0.0
			var fastest_at := 0.0
			var t := 0.0

			while t <= anim.length + 0.001:
				var at: Vector3 = _global(anim, hand, t).origin
				var center: Vector3 = _global(anim, hips, t).origin
				var forward := at.z - center.z
				if previous != null:
					var speed: float = (at - previous).length() * 30.0
					if speed > fastest:
						fastest = speed
						fastest_at = t
				previous = at
				if forward > front:
					front = forward
					front_at = t
				t += 1.0 / 30.0

			print("  mão %s: mais à frente %.2f m em %.2f s · mais rápida %.1f m/s em %.2f s"
				% [side, front, front_at, fastest, fastest_at])

		_body(anim, hips)

	model.free()
	quit()


## Movimento, passo do quadril e aterrissagem de um clipe.
func _body(anim: Animation, hips: int) -> void:
	var parts := ["Head", "LeftHand", "RightHand", "LeftFoot", "RightFoot"]
	var feet := [skeleton.find_bone("mixamorig_LeftFoot"), skeleton.find_bone("mixamorig_RightFoot")]
	var previous := {}
	var first_move := -1.0
	var last_move := 0.0
	var z_max := -INF
	var times: Array[float] = []
	var zs: Array[float] = []
	var foot_rest := minf(_global(anim, feet[0], 0.0).origin.y, _global(anim, feet[1], 0.0).origin.y)
	var foot_top := foot_rest
	var foot_top_at := 0.0
	var touch := -1.0
	var t := 0.0

	while t <= anim.length + 0.001:
		var center: Vector3 = _global(anim, hips, t).origin
		var motion := 0.0
		for part in parts:
			var at: Vector3 = _global(anim, skeleton.find_bone("mixamorig_" + part), t).origin - center
			if previous.has(part):
				motion += (at - previous[part]).length() * 30.0
			previous[part] = at
		if motion > 1.5:
			if first_move < 0.0:
				first_move = t
			last_move = t

		var z: float = _local(anim, hips, t).origin.z
		times.append(t)
		zs.append(z)
		z_max = maxf(z_max, z)

		var low := minf(_global(anim, feet[0], t).origin.y, _global(anim, feet[1], t).origin.y)
		if low > foot_top:
			foot_top = low
			foot_top_at = t
		if touch < 0.0 and foot_top - foot_rest > 0.3 and t > foot_top_at and low <= foot_rest + 0.05:
			touch = t
		t += 1.0 / 30.0

	print("  movimento: de %.2f a %.2f s" % [maxf(first_move, 0.0), last_move])

	# O passo: do ponto mais atrás antes do mais à frente até ele, do
	# primeiro ao último quadro em que o quadril anda (5% e 95% do total).
	var top := zs.find(z_max)
	var low_at := 0
	for i in top:
		if zs[i] < zs[low_at]:
			low_at = i
	var travel := z_max - zs[low_at]
	if travel > 0.1:
		var from := times[low_at]
		var to := times[top]
		for i in range(low_at, top + 1):
			if zs[i] <= zs[low_at] + travel * 0.05:
				from = times[i]
			if zs[i] >= zs[low_at] + travel * 0.95:
				to = times[i]
				break
		print("  quadril: passo de %.2f m para a frente, de %.2f a %.2f s" % [travel, from, to])

	if touch >= 0.0:
		print("  pés: sobem %.2f m (topo em %.2f s) e tocam o chão em %.2f s" % [foot_top - foot_rest, foot_top_at, touch])


func _take(source: Node) -> Animation:
	var player := _find_player(source)
	if player == null:
		return null
	var best: Animation = null
	for name in player.get_animation_list():
		if best == null or String(name).contains("mixamo"):
			best = player.get_animation(name)
	return best


func _local(anim: Animation, bone: int, t: float) -> Transform3D:
	var rest := skeleton.get_bone_rest(bone)
	var position := rest.origin
	var rotation := rest.basis.get_rotation_quaternion()
	var scale := rest.basis.get_scale()
	var path := "Skeleton3D:" + skeleton.get_bone_name(bone)

	for track in anim.get_track_count():
		if String(anim.track_get_path(track)) != path:
			continue
		match anim.track_get_type(track):
			Animation.TYPE_POSITION_3D:
				position = anim.position_track_interpolate(track, t)
			Animation.TYPE_ROTATION_3D:
				rotation = anim.rotation_track_interpolate(track, t)
			Animation.TYPE_SCALE_3D:
				scale = anim.scale_track_interpolate(track, t)

	return Transform3D(Basis(rotation).scaled(scale), position)


func _global(anim: Animation, bone: int, t: float) -> Transform3D:
	var result := _local(anim, bone, t)
	var parent := skeleton.get_bone_parent(bone)
	while parent >= 0:
		result = _local(anim, parent, t) * result
		parent = skeleton.get_bone_parent(parent)
	return result


func _find_skeleton(node: Node) -> Skeleton3D:
	if node is Skeleton3D:
		return node
	for child in node.get_children():
		var found := _find_skeleton(child)
		if found:
			return found
	return null


func _find_player(node: Node) -> AnimationPlayer:
	if node is AnimationPlayer:
		return node
	for child in node.get_children():
		var found := _find_player(child)
		if found:
			return found
	return null
