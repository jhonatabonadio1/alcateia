"""Prévia do GLB com o Cycles: frente, costas e três quartos. uso: render_glb.py modelo.glb saida.png [amostras]"""
import bpy, sys, math
from mathutils import Vector
src, out = sys.argv[1], sys.argv[2]
samples = int(sys.argv[3]) if len(sys.argv) > 3 else 12
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
# a cor de vértice é máscara (balanço, tom): na prévia não entra na cor
for me in bpy.data.meshes:
    for ca in me.color_attributes:
        n = len(ca.data)
        ca.data.foreach_set("color", [1.0] * (n * 4))
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = samples
sc.cycles.use_denoising = False
sc.render.resolution_x = 560; sc.render.resolution_y = 720
sc.render.film_transparent = False
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.035, 0.04, 0.06, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 1.0
for o in list(bpy.data.objects):
    if o.type == 'MESH' and not o.material_slots: bpy.data.objects.remove(o)
def luz(nome, loc, energia, cor):
    l = bpy.data.lights.new(nome, 'AREA'); l.energy = energia; l.color = cor; l.size = 2.5
    o = bpy.data.objects.new(nome, l); sc.collection.objects.link(o); o.location = loc
    o.rotation_euler = (Vector((0, 0, 1.0)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
luz("k", (2.5, -3.5, 3.2), 900, (1, 0.93, 0.85)); luz("f", (-3, -2, 1.5), 260, (0.6, 0.75, 1)); luz("r", (0, 3.5, 2.6), 800, (0.8, 0.9, 1)); luz("b", (2.5, 3.0, 1.0), 350, (1, 0.9, 0.8))
cam = bpy.data.cameras.new("c"); cam.type = 'ORTHO'
co = bpy.data.objects.new("c", cam); sc.collection.objects.link(co); sc.camera = co
views = [("frente", 0, 1.95, 0.83), ("costas", 180, 1.95, 0.83), ("costas_perto", 180, 1.0, 1.28), ("lado", 115, 1.95, 0.83)]
which = sys.argv[4].split(",") if len(sys.argv) > 4 else [v[0] for v in views]
for nome, ang, escala, alt in views:
    if nome not in which: continue
    a = math.radians(ang)
    co.location = (6 * math.sin(a), -6 * math.cos(a), alt)
    co.rotation_euler = (math.radians(90), 0, a)
    cam.ortho_scale = escala
    sc.render.filepath = out.replace(".png", f"_{nome}.png")
    bpy.ops.render.render(write_still=True)
print("render ok")
