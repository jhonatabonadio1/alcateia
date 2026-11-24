"""
Assa as animações da P9 que o Jhon trouxe (23/11: assets/p9_manny_fps_animations.glb, "para você ter ideia de
como fazer a mão segurando, a recarga da pistola e o draw") em tools/armas/dados/p9.npz, para o conjunto da pistola
(tools/armas/conjuntos.py, RefP9). Só três clipes: o saque (WEP_Draw), o parado (WEP_Idle) e a recarga
(WEP_Reload_01) — "Fire e Walk não precisa".

    python tools/armas/blender/p9.py -- assets/pacotes/sketchfab/p9-manny-fps-animations/p9_manny_fps_animations.glb tools/armas/dados/p9.npz

(O Jhon pôs o arquivo em assets/; ele mora em assets/pacotes/sketchfab/, fora do Godot e do git: é o pacote cru, de
licença Sketchfab Standard — o jogo só leva o movimento assado.)

O esqueleto é o Manny do UE5, o mesmo da nossa primeira pessoa (os nomes vêm com dois números no fim, que saem).
Grava, por quadro (24 por segundo) e no espaço da armadura (cm), a 4×4 de cada osso que interessa, e a pose local
(em relação ao pai) dos dedos; e a pistola e o carregador em repouso no espaço do Grip_Bone.
"""
import bpy, sys, re, numpy as np
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=sys.argv[-2])
sc = bpy.context.scene
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
limpo = lambda n: re.sub(r'_\d+_\d+$', '', n)
pb = {limpo(b.name): b for b in arm.pose.bones}
DEDOS = [f'{d}_{k}_{l}' for l in 'lr' for d in ('thumb', 'index', 'middle', 'ring', 'pinky') for k in ('01', '02', '03')] \
      + [f'{d}_metacarpal_{l}' for l in 'lr' for d in ('index', 'middle', 'ring', 'pinky')]
OSSOS = ['root', 'pelvis', 'spine_05', 'neck_01', 'head', 'ik_hand_root', 'ik_hand_gun', 'ik_hand_r', 'ik_hand_l', 'interaction',
         'Grip_Bone', 'Slide_Bone', 'Trigger_Bone', 'Clip_Bone', 'Barrel_Bone', 'Ammo'] \
      + [f'{p}_{l}' for l in 'lr' for p in ('clavicle', 'upperarm', 'lowerarm', 'hand')] + DEDOS
falta = [o for o in OSSOS if o not in pb]
print('faltam', falta); OSSOS = [o for o in OSSOS if o in pb]
S = np.array(arm.matrix_world)
dados = {}
def mundo(o): return np.array(pb[o].matrix)                   # no espaço da armadura (cm)
def local(o):
    b = pb[o]; return np.array(b.parent.matrix.inverted() @ b.matrix) if b.parent else np.array(b.matrix)
acoes = {a.name: a for a in bpy.data.actions}
print('ações', list(acoes))
for nome, chave in (('WEP_Draw', 'draw'), ('WEP_Idle', 'idle'), ('WEP_Reload_01', 'reload')):
    a = acoes[nome]; arm.animation_data.action = a
    f0, f1 = int(round(a.frame_range[0])), int(round(a.frame_range[1])); W, L = [], []
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        W.append([mundo(o) for o in OSSOS]); L.append([local(o) for o in DEDOS if o in pb])
    dados[chave + '_mundo'] = np.array(W, np.float32); dados[chave + '_dedos'] = np.array(L, np.float32)
    print(chave, f1 - f0 + 1, 'quadros')
# a pose de referência (as juntas locais de descanso dos dedos, para comparar com o nosso esqueleto)
dados['repouso_dedos'] = np.array([np.array((b.parent.bone.matrix_local.inverted() @ b.bone.matrix_local) if b.parent else b.bone.matrix_local)
                                   for b in (pb[o] for o in DEDOS if o in pb)], np.float32)
# a pistola e o carregador no espaço do Grip_Bone, no primeiro quadro do parado
arm.animation_data.action = acoes['WEP_Idle']; sc.frame_set(0)
dg = bpy.context.evaluated_depsgraph_get(); Gi = np.linalg.inv(S @ mundo('Grip_Bone')); Ci = np.linalg.inv(S @ mundo('Clip_Bone'))
for o in bpy.data.objects:
    if o.type != 'MESH' or not o.data.materials: continue
    mat = o.data.materials[0].name
    if mat not in ('p220', 'p220_mag'): continue
    me = o.evaluated_get(dg).to_mesh(); v = np.array([tuple(o.matrix_world @ x.co) for x in me.vertices])
    vl = (np.c_[v, np.ones(len(v))] @ Gi.T)[:, :3]; dados[mat + '_no_punho'] = vl.astype(np.float32)
    print(mat, 'no Grip_Bone: min', vl.min(0).round(2), 'max', vl.max(0).round(2))
    if mat == 'p220_mag':
        vc = (np.c_[v, np.ones(len(v))] @ Ci.T)[:, :3]; dados['p220_mag_no_clip'] = vc.astype(np.float32)
        print(mat, 'no Clip_Bone: min', vc.min(0).round(2), 'max', vc.max(0).round(2))
dados['ossos'] = np.array(OSSOS); dados['dedos'] = np.array([o for o in DEDOS if o in pb]); dados['fps'] = np.array(sc.render.fps / sc.render.fps_base)
np.savez_compressed(sys.argv[-1], **dados)
