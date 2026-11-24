"""
Assa as animações de pistola que o Jhon trouxe (19/11: pistol-43-tactical-fps-animations, Glock_Anim.fbx) em
tools/armas/dados/glock.npz, para o conjunto da pistola (tools/armas/conjuntos.py, RefPistola).

    blender -b -P tools/armas/blender/glock.py -- <Glock_Anim.fbx> tools/armas/dados/glock.npz
"""
import bpy, sys, numpy as np
from mathutils import Matrix
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=sys.argv[-2])
sc = bpy.context.scene; arm = bpy.data.objects['Hands']; cam = bpy.data.objects['Camera']; glock = bpy.data.objects['Glock']
print('camera pai', cam.parent.name, cam.parent_type, cam.parent_bone, 'glock pai', glock.parent_type, glock.parent_bone, [g.name for g in glock.vertex_groups][:12], [m.type for m in glock.modifiers])
ossos = ['Wpn_body', 'Wpn_body2', 'Slide', 'Magazine', 'Barrel', 'r_wrist', 'l_wrist', 'r_forearm', 'l_forearm'] + [f'{l}_{d}_low' for l in 'lr' for d in ('index', 'middle', 'ring', 'pinky', 'thumb')]
dados = {}
for nome in ('Shot', 'Reloat_t', 'Reloat_f', 'Walk', 'Run', 'Inspect', 'none'):
    a = bpy.data.actions.get(f'Hands|Hands|{nome}'); c = bpy.data.actions.get(f'Camera|Hands|{nome}')
    arm.animation_data.action = a
    if cam.animation_data is None: cam.animation_data_create()
    cam.animation_data.action = c
    f0, f1 = int(a.frame_range[0]), int(a.frame_range[1]); Cs, Bs = [], []
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        Cs.append(np.array(cam.matrix_world)); Bs.append([np.array(arm.matrix_world @ arm.pose.bones[o].matrix) for o in ossos])
    dados[nome + '_cam'] = np.array(Cs, np.float32); dados[nome + '_ossos'] = np.array(Bs, np.float32)
    print(nome, f1 - f0 + 1, 'quadros')
# a pistola em repouso, no espaço do osso Wpn_body (para achar os eixos dela)
arm.animation_data.action = bpy.data.actions['Hands|Hands|none']; sc.frame_set(1)
dg = bpy.context.evaluated_depsgraph_get(); ev = glock.evaluated_get(dg); me = ev.to_mesh()
W = np.array(arm.matrix_world @ arm.pose.bones['Wpn_body'].matrix); Wi = np.linalg.inv(W)
v = np.array([tuple(glock.matrix_world @ x.co) for x in me.vertices]); vl = (np.c_[v, np.ones(len(v))] @ Wi.T)[:, :3]
dados['glock_local'] = vl.astype(np.float32); dados['ossos'] = np.array(ossos)
print('glock no Wpn_body: min', vl.min(0).round(3), 'max', vl.max(0).round(3))
np.savez_compressed(sys.argv[-1], **dados)
