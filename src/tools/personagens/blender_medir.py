# Mede um modelo no Blender: altura, quadril, ossos e texturas.
# Chamado por tools/personagem.sh medir; não rode direto.
import bpy, sys

path = sys.argv[sys.argv.index('--') + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
if path.lower().endswith(('.glb', '.gltf')):
    bpy.ops.import_scene.gltf(filepath=path)
else:
    bpy.ops.import_scene.fbx(filepath=path)
bpy.context.view_layer.update()

dg = bpy.context.evaluated_depsgraph_get()
lo, hi = [1e9] * 3, [-1e9] * 3
meshes = []
for o in bpy.context.scene.objects:
    # Só a malha presa ao esqueleto: o importador de glTF do Blender cria
    # uma "Icosphere" para desenhar os ossos, que não está no arquivo.
    if o.type != 'MESH' or not (any(m.type == 'ARMATURE' for m in o.modifiers) or (o.parent and o.parent.type == 'ARMATURE')):
        continue
    e = o.evaluated_get(dg)
    m = e.to_mesh()
    meshes.append((o.name, len(m.vertices)))
    for v in m.vertices:
        w = o.matrix_world @ v.co
        for i in range(3):
            lo[i] = min(lo[i], w[i]); hi[i] = max(hi[i], w[i])
    e.to_mesh_clear()

print(f'MEDIDA altura {hi[2] - lo[2]:.3f} m  (pé em {lo[2]:.3f}, topo em {hi[2]:.3f})')
for o in bpy.context.scene.objects:
    if o.type == 'ARMATURE':
        hips = o.pose.bones.get('mixamorig:Hips')
        names = [b.name for b in o.data.bones]
        print(f'MEDIDA ossos {len(names)}  quadril {(o.matrix_world @ hips.head)[2]:.3f} m' if hips else f'MEDIDA ossos {len(names)}  SEM mixamorig:Hips')
        for need in ('mixamorig:Head', 'mixamorig:HeadTop_End', 'mixamorig:LeftHand', 'mixamorig:RightHand', 'mixamorig:LeftFoot', 'mixamorig:RightFoot'):
            if need not in names:
                print(f'MEDIDA falta o osso {need}')
print(f'MEDIDA malhas {meshes}')
for im in bpy.data.images:
    print(f'MEDIDA textura {im.name} {im.size[0]}x{im.size[1]}')
print(f'MEDIDA animacoes {[a.name for a in bpy.data.actions]}')
