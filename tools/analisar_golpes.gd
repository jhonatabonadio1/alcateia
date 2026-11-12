extends SceneTree
## Mede os golpes do lobo nas animações: para cada clipe, onde está cada
## mão quadro a quadro, quanto ela anda e quando ela está mais à frente.
## O golpe é o pico de velocidade da mão que bate.
##
## Uso, da raiz do projeto:
##   godot --headless --path . -s res://tools/analisar_golpes.gd
##
## Os números vão para scripts/forms/WolfMoves.cs (Start, End, Impact).
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

	model.free()
	quit()


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
