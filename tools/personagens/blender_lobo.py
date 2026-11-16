# Prepara o lobo de um personagem: tira objetos soltos e animações, leva
# a 1,66 m (a altura do lobo do jogo) e reduz as texturas a 2048 px.
# Chamado por tools/personagem.sh lobo; não rode direto.
#
# As texturas passam pelo sips do macOS: o JPEG que o Blender grava depois
# de reduzir a imagem é recusado pelo Godot ("Couldn't load image"),
# e o mesmo JPEG regravado pelo sips entra. Sem sips, ficam em PNG.
import bpy, sys, os, shutil, subprocess, tempfile

args = sys.argv[sys.argv.index('--') + 1:]
src, dst = args[0], args[1]
TARGET = 1.66
MAX_TEXTURE = 2048

bpy.ops.wm.read_factory_settings(use_empty=True)
if src.lower().endswith(('.glb', '.gltf')):
    bpy.ops.import_scene.gltf(filepath=src)
else:
    bpy.ops.import_scene.fbx(filepath=src)

arm = next((o for o in bpy.context.scene.objects if o.type == 'ARMATURE'), None)
if arm is None:
    sys.exit('PREPARO ERRO: o lobo não tem esqueleto (baixe o modelo rigado).')

# Só fica a malha presa ao esqueleto. O importador de glTF do Blender
# cria uma "Icosphere" para desenhar os ossos; fora, para não entrar na
# medida nem no arquivo.
for o in list(bpy.context.scene.objects):
    if o.type == 'MESH' and not any(m.type == 'ARMATURE' for m in o.modifiers) and o.parent != arm:
        print(f'PREPARO fora da malha: {o.name}')
        bpy.data.objects.remove(o, do_unlink=True)

# As animações do jogo são as do lobo_anim.glb.
for o in bpy.context.scene.objects:
    if o.animation_data:
        o.animation_data_clear()
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)
for pb in arm.pose.bones:
    pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0)
    pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
bpy.context.view_layer.update()

def box():
    dg = bpy.context.evaluated_depsgraph_get()
    lo, hi = [1e9] * 3, [-1e9] * 3
    for o in bpy.context.scene.objects:
        if o.type != 'MESH':
            continue
        e = o.evaluated_get(dg); m = e.to_mesh()
        for v in m.vertices:
            w = o.matrix_world @ v.co
            for i in range(3):
                lo[i] = min(lo[i], w[i]); hi[i] = max(hi[i], w[i])
        e.to_mesh_clear()
    return lo, hi

lo, hi = box()
k = TARGET / (hi[2] - lo[2])
arm.scale = tuple(s * k for s in arm.scale)
arm.location = (arm.location[0], arm.location[1], arm.location[2] - lo[2] * k)
bpy.context.view_layer.update()
lo, hi = box()
print(f'PREPARO altura {hi[2] - lo[2]:.3f} m, pé em {lo[2]:.3f}')

# A textura sai com os bytes de origem, e o sips reduz e grava o JPEG.
tmp = tempfile.mkdtemp()
sips = shutil.which('sips')
for im in list(bpy.data.images):
    if im.size[0] == 0:
        continue
    big = im.size[0] > MAX_TEXTURE or im.size[1] > MAX_TEXTURE
    if sips and im.packed_file is not None:
        ext = '.png' if im.packed_file.data[:4] == b'\x89PNG' else '.jpg'
        raw = os.path.join(tmp, f'{im.name}_cru{ext}')
        with open(raw, 'wb') as f:
            f.write(im.packed_file.data)
        jpg = os.path.join(tmp, f'{im.name}.jpg')
        cmd = [sips, '-s', 'format', 'jpeg', '-s', 'formatOptions', '90']
        if big:
            cmd += ['-Z', str(MAX_TEXTURE)]
        subprocess.run(cmd + [raw, '--out', jpg], check=True, capture_output=True)
        im.unpack(method='REMOVE')
        im.filepath = jpg
        im.source = 'FILE'
        im.reload()
    elif big:
        # Sem sips: reduz no Blender e exporta em PNG.
        im.scale(min(im.size[0], MAX_TEXTURE), min(im.size[1], MAX_TEXTURE))
    print(f'PREPARO textura {im.name} {im.size[0]}x{im.size[1]}')

bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', export_animations=False,
                          export_skins=True, export_image_format='AUTO')
print(f'PREPARO pronto: {dst}')
