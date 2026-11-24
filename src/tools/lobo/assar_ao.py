"""Assa a oclusão ambiente do lobo na textura (Cycles, CPU). uso: assar_ao.py modelo.glb saida.png [amostras]"""
import bpy, sys
src, out = sys.argv[1], sys.argv[2]
samples = int(sys.argv[3]) if len(sys.argv) > 3 else 24
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = samples
scene.cycles.use_denoising = False
img = bpy.data.images.new("ao", 2048, 2048, alpha=False)
objs = [o for o in bpy.data.objects if o.type == 'MESH' and o.material_slots and o.material_slots[0].material]
for m in bpy.data.materials:
    if not m.node_tree: continue
    n = m.node_tree.nodes.new("ShaderNodeTexImage")
    n.image = img
    m.node_tree.nodes.active = n
for o in bpy.data.objects: o.select_set(False)
for o in objs: o.select_set(True)
bpy.context.view_layer.objects.active = objs[0]
scene.render.bake.margin = 4
try:
    scene.world = bpy.data.worlds.new("w")
    scene.world.light_settings.distance = 0.5
except Exception as e:
    print("world", e)
bpy.ops.object.bake(type='AO')
img.filepath_raw = out
img.file_format = 'PNG'
img.save()
print("AO ok", out)
