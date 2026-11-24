"""
Tira de um clipe de arremesso do Mixamo só o braço que lança (o direito):
a clavícula, o cotovelo, o punho e a mão, quadro a quadro, no referencial do
peito (x direita, y frente, z cima) e em comprimentos de braço a partir do
ombro. O tronco, as pernas e o outro braço do clipe ficam de fora: quem os
anima é o mao_manny_anim.py (Copia.lance), sincronizados com este braço.

    bpy python lance_extrair.py "Grenade Throw.fbx" lance_referencia.json

O .fbx do Mixamo não vai para o repositório (a licença não deixa redistribuir
o arquivo cru); estes números, sim.
"""
import json
import sys

import bpy
from mathutils import Matrix

entrada, saida = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=entrada)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
acao = arm.animation_data.action
P = 'mixamorig:'
mw = arm.matrix_world
mw3 = mw.to_3x3().normalized()
# o personagem do Mixamo olha para -Y, com a direita em -X: as colunas são direita, frente, cima
B = Matrix(((-1, 0, 0), (0, -1, 0), (0, 0, 1))).transposed()

# o peito no repouso
arm.animation_data.action = None
for pb in arm.pose.bones:
    pb.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()
peito0 = arm.pose.bones[P + 'Spine2'].matrix.to_3x3().normalized()


def junta(n):
    return mw @ arm.pose.bones[P + n].head


L = (junta('RightForeArm') - junta('RightArm')).length + (junta('RightHand') - junta('RightForeArm')).length
arm.animation_data.action = acao
quadros = []
f0, f1 = int(acao.frame_range[0]), int(acao.frame_range[1])
for f in range(f0, f1 + 1):
    bpy.context.scene.frame_set(f)
    peito = arm.pose.bones[P + 'Spine2'].matrix.to_3x3().normalized()
    C = (mw3 @ peito @ peito0.inverted() @ mw3.inverted() @ B).normalized()
    Ct = C.transposed()
    ombro, clav = junta('RightArm'), junta('RightShoulder')
    cot, pulso = junta('RightForeArm'), junta('RightHand')
    dedos = (junta('RightHandMiddle1') - pulso).normalized()
    # a palma olha para o lado de lá do dorso: (indicador - mínimo) x dedos, na mão direita
    palma = (junta('RightHandIndex1') - junta('RightHandPinky1')).cross(dedos).normalized()

    def v(x):
        return [round(c, 5) for c in (Ct @ x)]
    quadros.append(dict(q=f, clavicula=v((ombro - clav).normalized()), cotovelo=v((cot - ombro) / L),
                        punho=v((pulso - ombro) / L), dedos=v(dedos), palma=v(palma)))
with open(saida, 'w') as arq:
    json.dump(dict(fps=round(bpy.context.scene.render.fps / bpy.context.scene.render.fps_base),
                   origem='Mixamo "Grenade Throw": só o braço direito, no referencial do peito '
                          '(x direita, y frente, z cima); posições a partir do ombro, em comprimentos de braço',
                   quadros=quadros), arq, indent=0)
print('REFERENCIA', len(quadros), 'quadros ->', saida)
