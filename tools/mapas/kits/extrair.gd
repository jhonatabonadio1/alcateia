extends SceneTree
# Tira a geometria de um kit de peças (.fbx importados pelo Godot) para o gerador de mapas (tools/mapas/kits):
#
#     godot --headless --path . -s tools/mapas/kits/extrair.gd -- res://assets/pacotes/fps_bundle/fps1 /caminho/de/saida/fps1
#
# Grava <saida>.json (o índice: cada peça, as superfícies dela, o material) e <saida>.bin (float32: por vértice de
# triângulo, posição, normal e UV, já no espaço da peça — com as transformações dos nós de dentro do .fbx aplicadas).
# O conversor em Python (tools/mapas/kits/kit_fbx.py) junta os dois num .npz.

func _init():
	var a = OS.get_cmdline_user_args(); var pasta = a[0]; var saida = a[1]
	var arqs = []; _listar(pasta, arqs); arqs.sort()
	var dados = PackedFloat32Array(); var indice = []
	for f in arqs:
		var cena = load(f)
		if cena == null:
			print("SEM CENA ", f); continue
		var n = cena.instantiate(); root.add_child(n)
		var peca = {"nome": f.trim_prefix(pasta + "/").trim_suffix(".fbx"), "superficies": []}
		for m in n.find_children("*", "MeshInstance3D", true, false) + ([n] if n is MeshInstance3D else []):
			if m.mesh == null: continue
			var xf = m.global_transform; var nb = xf.basis.inverse().transposed()
			for s in m.mesh.get_surface_count():
				var arr = m.mesh.surface_get_arrays(s)
				var v = arr[Mesh.ARRAY_VERTEX]; var nn = arr[Mesh.ARRAY_NORMAL]; var uv = arr[Mesh.ARRAY_TEX_UV]; var idx = arr[Mesh.ARRAY_INDEX]
				if v == null or v.size() == 0: continue
				var mat = m.get_active_material(s)
				var info = {"no": str(m.name), "inicio": dados.size(), "material": "", "cor": [1, 1, 1, 1], "textura": "", "transparente": false}
				if mat != null:
					info["material"] = mat.resource_name
					if mat is BaseMaterial3D:
						info["cor"] = [mat.albedo_color.r, mat.albedo_color.g, mat.albedo_color.b, mat.albedo_color.a]
						info["textura"] = mat.albedo_texture.resource_path if mat.albedo_texture != null else ""
						info["transparente"] = mat.transparency != BaseMaterial3D.TRANSPARENCY_DISABLED
				var total = idx.size() if idx != null and idx.size() > 0 else v.size()
				var bloco = PackedFloat32Array(); bloco.resize(total * 8)
				for k in total:
					var i = idx[k] if idx != null and idx.size() > 0 else k
					var p = xf * v[i]; var q = (nb * nn[i]).normalized() if nn != null and nn.size() > i else Vector3.UP
					var t = uv[i] if uv != null and uv.size() > i else Vector2.ZERO
					bloco[k * 8] = p.x; bloco[k * 8 + 1] = p.y; bloco[k * 8 + 2] = p.z
					bloco[k * 8 + 3] = q.x; bloco[k * 8 + 4] = q.y; bloco[k * 8 + 5] = q.z
					bloco[k * 8 + 6] = t.x; bloco[k * 8 + 7] = t.y
				dados.append_array(bloco); info["vertices"] = total
				peca["superficies"].append(info)
		indice.append(peca)
		root.remove_child(n); n.free()
	var fj = FileAccess.open(saida + ".json", FileAccess.WRITE); fj.store_string(JSON.stringify(indice)); fj.close()
	var fb = FileAccess.open(saida + ".bin", FileAccess.WRITE); fb.store_buffer(dados.to_byte_array()); fb.close()
	print("KIT ", pasta, ": ", indice.size(), " peças, ", dados.size() / 24, " triângulos")
	quit()

func _listar(p, out):
	var d = DirAccess.open(p)
	for f in d.get_files():
		if f.ends_with(".fbx"): out.append(p + "/" + f)
	for s in d.get_directories(): _listar(p + "/" + s, out)
