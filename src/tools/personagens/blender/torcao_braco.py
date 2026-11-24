"""
Um osso de torção no braço de cima de cada lado (`mixamorig:RightArmTwist`,
`mixamorig:LeftArmTwist`), para a pele do braço não torcer em espiral quando o
úmero gira em volta de si (Jhon, 23/11: "braço da granada torcendo aqui": a
granada engatilhada gira o braço de cima 130°, e sem osso de torção todo esse
giro aparecia de uma vez logo abaixo do ombro).

O osso nasce no ombro, com os eixos do braço (filho dele, sem ligar). A pele
que o braço levava passa para o osso de torção conforme a altura no braço:
toda perto do ombro, nada perto do cotovelo. O mao_manny_anim.py dá ao osso de
torção, a cada quadro, o giro contrário ao do braço em volta do próprio eixo:
perto do ombro a pele só balança (não gira), e o giro vai entrando aos poucos
até o cotovelo. Sem animação nele (andar, lobby), o osso fica em repouso e o
braço deforma como antes.

    bpy python torcao_braco.py <personagem> [--aplicar]

Lê assets/personagens/<personagem>/humano.glb, guarda a cópia em
.synctmp/terceira_pessoa_2311/humano_<personagem>_antes_torcao.glb e grava
.synctmp/terceira_pessoa_2311/tpose/<personagem>_humano_torcao.glb; com
--aplicar, copia para o humano.glb do personagem.
"""
import os
import shutil
import sys

import bpy
from mathutils import Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, '..', '..', '..'))
COPIAS = os.path.join(RAIZ, '..', '.synctmp', 'terceira_pessoa_2311')   # RAIZ é src/; a .synctmp fica na raiz do repositório
PREFIXO = 'mixamorig:'
LADOS = ('Right', 'Left')


def nome_torcao(lado):
    return f'{PREFIXO}{lado}ArmTwist'


def suave(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def main():
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    quem = args[0]
    aplicar = '--aplicar' in args
    origem = os.path.join(RAIZ, 'assets/personagens', quem, 'humano.glb')
    os.makedirs(os.path.join(COPIAS, 'tpose'), exist_ok=True)
    copia = os.path.join(COPIAS, f'humano_{quem}_antes_torcao.glb')
    if not os.path.exists(copia):
        shutil.copy(origem, copia)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=origem)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    malhas = [o for o in bpy.data.objects if o.type == 'MESH' and o.parent == arm]
    if nome_torcao('Right') in arm.data.bones:
        print('TORCAO já tem os ossos de torção:', quem)
        return

    # 1) os ossos: no ombro, com os eixos do braço, metade do comprimento dele
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    ed = arm.data.edit_bones
    for lado in LADOS:
        braco = ed[f'{PREFIXO}{lado}Arm']
        t = ed.new(nome_torcao(lado))
        t.head = braco.head.copy()
        t.tail = braco.head + (braco.tail - braco.head) * 0.5
        t.roll = braco.roll
        t.parent = braco
        t.use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')

    # 2) a pele: do braço para o osso de torção, toda perto do ombro, nada perto do cotovelo
    mw = arm.matrix_world
    for lado in LADOS:
        b = arm.data.bones[f'{PREFIXO}{lado}Arm']
        ombro, cotovelo = mw @ b.head_local, mw @ b.tail_local
        eixo = cotovelo - ombro
        L2 = eixo.length_squared
        n = 0
        for o in malhas:
            grupos = {g.name: g for g in o.vertex_groups}
            g_braco = grupos.get(f'{PREFIXO}{lado}Arm')
            if g_braco is None:
                continue
            g_torcao = grupos.get(nome_torcao(lado)) or o.vertex_groups.new(name=nome_torcao(lado))
            for v in o.data.vertices:
                w = next((x.weight for x in v.groups if x.group == g_braco.index), 0.0)
                if w <= 0.0:
                    continue
                p = o.matrix_world @ v.co
                s = (p - ombro).dot(eixo) / L2
                # 0 no ombro, 1 no cotovelo: a parte que fica com o osso de torção cai suave
                f = 1.0 - suave((s - 0.05) / 0.9)
                if f <= 1e-3:
                    continue
                g_torcao.add([v.index], w * f, 'REPLACE')
                g_braco.add([v.index], w * (1.0 - f), 'REPLACE')
                n += 1
        print('TORCAO', quem, lado, ':', n, 'vértices divididos entre o braço e o osso de torção')

    destino = os.path.join(COPIAS, 'tpose', f'{quem}_humano_torcao.glb')
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    for m in malhas:
        m.select_set(True)
    bpy.ops.export_scene.gltf(filepath=destino, export_format='GLB', use_selection=True, export_animations=False)
    print('GLB', destino)
    if aplicar:
        shutil.copy(destino, origem)
        print('APLICADO em', origem)


if __name__ == '__main__':
    main()
