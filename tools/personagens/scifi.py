"""
Monta o humano de um personagem a partir do pacote Stylized Sci-Fi
Characters (versão Unity, que já vem com o esqueleto de nomes do Mixamo).

    python3 tools/personagens/scifi.py -- --fbx SM_Sci-Fi_Male_Unity.fbx \
        --tex /tmp/sf/tex --prefixo m --partes head,Hair_01,Boots,Gloves,Pants,Upper \
        --saida assets/personagens/barut/humano.glb

O que faz:
  * fica só com as peças da roupa escolhida (a mesma que a versão Unreal do
    pacote monta) e um cabelo;
  * ossos com o prefixo "mixamorig:" e as três pontas que o Mixamo tem e o
    pacote não (HeadTop_End no alto do crânio, Toe_End na ponta da bota) —
    o jogo acha cabeça, mãos e pés por esses nomes;
  * escala para o alto do crânio ficar a 1,79 m, a régua de todo humano do
    jogo (a cabeça é o que leva tiro);
  * materiais com cor, normal, ORM (oclusão, aspereza, metal, montados da
    "Mask" do Unity: G oclusão, A lisura, R metal) e o brilho da roupa;
  * GLB com as texturas dentro.

O repouso continua o do pacote (T, com o eixo de cada osso como o Blender
importou). Os clipes do Mixamo entram por AnimationKit.RetargetPose, que
copia a direção de cada osso no mundo e não o giro local.

Precisa do módulo bpy (pip install bpy).
"""
import bpy, sys, os, math, mathutils, argparse

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument('--fbx', required=True)
ap.add_argument('--tex', required=True, help='pasta com <prefixo>_{Outfit,Body,Hair}_{color,normal,orm}')
ap.add_argument('--prefixo', required=True)
ap.add_argument('--partes', required=True, help='pedaços do nome das malhas que ficam')
ap.add_argument('--saida', required=True)
ap.add_argument('--topo', type=float, default=1.79, help='altura do alto do crânio, em metros')
a = ap.parse_args(argv)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=a.fbx)

arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
keep = [p.strip() for p in a.partes.split(',') if p.strip()]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and not any(k in o.name for k in keep):
        bpy.data.objects.remove(o, do_unlink=True)
for o in list(bpy.data.objects):
    if o.type not in ('MESH', 'ARMATURE'):
        bpy.data.objects.remove(o, do_unlink=True)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
print('malhas:', sorted(o.name for o in meshes))

# Tudo aplicado: armadura e malhas sem giro nem escala no objeto.
bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.context.view_layer.update()

head = next(o for o in meshes if 'Body_head' in o.name)
skull = max((head.matrix_world @ v.co).z for v in head.data.vertices)
floor = min((o.matrix_world @ v.co).z for o in meshes for v in o.data.vertices)
scale = a.topo / (skull - floor)
print(f'crânio {skull:.4f} chão {floor:.4f} escala {scale:.4f}')

# Escala a partir do chão (o pé fica no zero).
for o in [arm] + meshes:
    o.location.z -= floor
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
arm.scale = (scale, scale, scale)
for o in meshes:
    if o.parent != arm:
        o.scale = (scale, scale, scale)
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.context.view_layer.update()

# Ossos com o prefixo do Mixamo, e as pontas que faltam.
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones
for b in list(eb):
    if not b.name.startswith('mixamorig:'):
        b.name = 'mixamorig:' + b.name

def tip(parent, name, at):
    p = eb['mixamorig:' + parent]
    b = eb.new('mixamorig:' + name)
    b.head = at
    b.tail = at + (at - p.head).normalized() * 0.05 if (at - p.head).length > 1e-4 else at + mathutils.Vector((0, 0, 0.05))
    b.parent = p
    b.use_connect = False
    return b

head_bone = eb['mixamorig:Head']
skull_top = max((head.matrix_world @ v.co).z for v in head.data.vertices)
top = mathutils.Vector((head_bone.head.x, head_bone.head.y, skull_top))
tip('Head', 'HeadTop_End', top)
for side in ('Left', 'Right'):
    toe = eb[f'mixamorig:{side}ToeBase']
    d = (toe.tail - toe.head)
    d.z = 0
    d = d.normalized() if d.length > 1e-4 else mathutils.Vector((0, -1, 0))
    boots = [o for o in meshes if 'Boots' in o.name or 'feet' in o.name]
    # A ponta da bota: o vértice mais à frente do lado do pé.
    best = None
    for o in boots:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            if (w.x > 0) != (toe.head.x > 0): continue
            s = (w - toe.head).dot(d)
            if best is None or s > best[0]: best = (s, w)
    end = toe.head + d * (best[0] if best else 0.08)
    end.z = toe.head.z
    tip(f'{side}ToeBase', f'{side}Toe_End', end)
bpy.ops.object.mode_set(mode='OBJECT')

for o in meshes:
    for g in o.vertex_groups:
        if not g.name.startswith('mixamorig:'):
            g.name = 'mixamorig:' + g.name
    if o.parent != arm:
        o.parent = arm
    if not any(m.type == 'ARMATURE' for m in o.modifiers):
        m = o.modifiers.new('Armature', 'ARMATURE'); m.object = arm
    # Peso em no máximo 4 ossos, que é o que o jogo desenha.
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
    o.select_set(False)

# Materiais.
def image(name, data=False):
    path = os.path.join(a.tex, f'{a.prefixo}_{name}')
    img = bpy.data.images.load(path)
    if data: img.colorspace_settings.name = 'Non-Color'
    return img

def material(name, part, emissive=False):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes['Principled BSDF']
    c = nt.nodes.new('ShaderNodeTexImage'); c.image = image(f'{part}_color.jpg')
    nt.links.new(c.outputs['Color'], bsdf.inputs['Base Color'])
    n = nt.nodes.new('ShaderNodeTexImage'); n.image = image(f'{part}_normal.jpg', True)
    nm = nt.nodes.new('ShaderNodeNormalMap')
    nt.links.new(n.outputs['Color'], nm.inputs['Color'])
    nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    o = nt.nodes.new('ShaderNodeTexImage'); o.image = image(f'{part}_orm.jpg', True)
    sep = nt.nodes.new('ShaderNodeSeparateColor')
    nt.links.new(o.outputs['Color'], sep.inputs['Color'])
    nt.links.new(sep.outputs['Green'], bsdf.inputs['Roughness'])
    nt.links.new(sep.outputs['Blue'], bsdf.inputs['Metallic'])
    # Oclusão: o exportador do glTF lê do grupo "glTF Material Output".
    grp = bpy.data.node_groups.get('glTF Material Output')
    if grp is None:
        grp = bpy.data.node_groups.new('glTF Material Output', 'ShaderNodeTree')
        grp.interface.new_socket('Occlusion', in_out='INPUT', socket_type='NodeSocketFloat')
    g = nt.nodes.new('ShaderNodeGroup'); g.node_tree = grp
    nt.links.new(sep.outputs['Red'], g.inputs['Occlusion'])
    if emissive:
        e = nt.nodes.new('ShaderNodeTexImage'); e.image = image(f'{part}_emissive.jpg')
        nt.links.new(e.outputs['Color'], bsdf.inputs['Emission Color'])
        bsdf.inputs['Emission Strength'].default_value = 2.0
    return m

mats = {'Outfit': material('Roupa', 'Outfit', True), 'Body': material('Corpo', 'Body'), 'Hair': material('Cabelo', 'Hair')}
for o in meshes:
    kind = 'Hair' if 'Hair' in o.name else 'Body' if '_Body_' in o.name else 'Outfit'
    o.data.materials.clear()
    o.data.materials.append(mats[kind])

arm.name = 'Armature'
os.makedirs(os.path.dirname(os.path.abspath(a.saida)), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=a.saida, export_format='GLB', use_selection=False,
    export_animations=False, export_skins=True, export_yup=True, export_image_format='AUTO')
print('ok', a.saida, os.path.getsize(a.saida) // 1024, 'KB')
