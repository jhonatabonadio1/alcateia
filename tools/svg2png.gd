extends SceneTree
# uso: godot --headless --path tool -s svg2png.gd -- <dir_svg> <dir_png> <lado_px> [viewbox=256]
func _init() -> void:
	var a := OS.get_cmdline_user_args()
	var src: String = a[0]
	var dst: String = a[1]
	var side := float(a[2])
	var box := float(a[3]) if a.size() > 3 else 256.0
	var n := 0
	for f in DirAccess.get_files_at(src):
		if not f.ends_with(".svg"): continue
		var img := Image.new()
		var err := img.load_svg_from_string(FileAccess.get_file_as_string(src + "/" + f), side / box)
		if err != OK:
			push_error("falhou: " + f)
			continue
		img.save_png(dst + "/" + f.get_basename() + ".png")
		n += 1
	print("rasterizados: ", n)
	quit()
