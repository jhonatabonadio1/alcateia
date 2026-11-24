#!/usr/bin/env python3
"""
A mão de cada personagem com os ossos, as proporções e o tamanho da mão da
primeira pessoa (Manny), em pose T (Jhon, 23/11: "vamos usar o fbx pose T da
eve: 1 - ossos do manny na eve; 2 - aproximar as raízes dos dedos; 3 - deixar
tamanhos dos dedos e da mão iguais a do manny, barut e eve").

    python3 tools/personagens/blender/mao_manny_tpose.py eve
    python3 tools/personagens/blender/mao_manny_tpose.py barut

Parte do `assets/personagens/<p>/humano.glb` (em pose T, a cópia de antes em
`.synctmp/terceira_pessoa_2311/`) e faz, só na mão:

1. OSSOS: os dedos ganham os nomes do Manny (`index_01_r`…) e entram os
   metacarpos dele (`index_metacarpal_r`… entre a mão e cada dedo). Os pesos
   dos metacarpos saem da própria malha: a pele da palma que a mão levava
   passa a ir com o metacarpo da faixa do dedo, mais perto dos nós.
2. RAÍZES: a distância entre as raízes dos dedos acompanha a grossura dos
   dedos (vizinhos se encostam, como no Manny).
3. TAMANHO: cada junta de cada osso da mão vai onde a do Manny fica (pulso
   ancorado), no comprimento dela, com a grossura dela; a palma ganha o
   comprimento e a largura da do Manny. A malha segue pelos pesos.

O Blender importa e exporta este .glb sem mexer nos ossos do corpo (conferido:
rotação e translação de todos os nós iguais). Grava o .blend
(`poses/<p>_tpose_manny.blend`, fora do git), o .glb em pose T e as imagens em
`.synctmp/terceira_pessoa_2311/tpose/`; o do jogo só muda com `--aplicar`.
"""
import math
import os
import shutil
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from poses_armas import RAIZ, orient  # noqa: E402

COPIAS = os.path.join(RAIZ, '.synctmp/terceira_pessoa_2311')
SAIDA = os.path.join(RAIZ, '.synctmp/terceira_pessoa_2311/tpose')
DEDOS4 = ('index', 'middle', 'ring', 'pinky')
TODOS = DEDOS4 + ('thumb',)
LIMITE_GROSSURA = (0.5, 1.4)


def osso_cadeia(lado):
    """Os ossos da mão na ordem pai → filho: nome → (filho principal ou None)."""
    s = lado
    c = {f'hand_{s}': None}
    for d in DEDOS4:
        c[f'{d}_metacarpal_{s}'] = f'{d}_01_{s}'
        c[f'{d}_01_{s}'] = f'{d}_02_{s}'
        c[f'{d}_02_{s}'] = f'{d}_03_{s}'
        c[f'{d}_03_{s}'] = None
    c[f'thumb_01_{s}'] = f'thumb_02_{s}'
    c[f'thumb_02_{s}'] = f'thumb_03_{s}'
    c[f'thumb_03_{s}'] = None
    return c


def importar_manny():
    """A mão da primeira pessoa (maos.glb), em metros: a posição da cabeça de
    cada osso e a ponta de cada dedo (do pulso, no espaço dela) e a grossura
    (raio) de cada dedo."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(RAIZ, 'assets/armas/fp/maos/maos.glb'))
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    malha = next(o for o in bpy.data.objects if o.type == 'MESH' and o.vertex_groups and 'maos' in o.name.lower())
    arm.animation_data_clear()
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    em = malha.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
    grupos = {g.index: g.name for g in malha.vertex_groups}
    pts = {}
    for v, ve in zip(malha.data.vertices, em.vertices):
        if v.groups:
            g = max(v.groups, key=lambda g: g.weight)
            pts.setdefault(grupos[g.group], []).append(malha.matrix_world @ ve.co)
    cab = {pb.name: arm.matrix_world @ pb.head for pb in arm.pose.bones}
    eixo_z = {b.name: (arm.matrix_world.to_3x3() @ b.matrix_local.to_3x3() @ Vector((0, 0, 1))).normalized() for b in arm.data.bones}
    eixo_y = {b.name: (arm.matrix_world.to_3x3() @ b.matrix_local.to_3x3() @ Vector((0, 1, 0))).normalized() for b in arm.data.bones}
    # o maos.glb vem em centímetros: o fator leva ao tamanho do conjunto (pulso→médio)
    fator = 0.0952 / (cab['middle_01_r'] - cab['hand_r']).length
    out = dict(cab={k: v * fator for k, v in cab.items()}, ponta={}, raio={}, fator=fator, eixo_z=eixo_z, eixo_y=eixo_y)
    for s in ('r', 'l'):
        for d in TODOS:
            nome = f'{d}_03_{s}'
            a = cab[nome]
            p = pts.get(nome, [])
            centro = sum(p, Vector()) / max(len(p), 1)
            eixo = (centro - a).normalized()
            out['ponta'][nome] = (a + eixo * max(((x - a).dot(eixo) for x in p), default=0.02)) * fator
            for k in (1, 2, 3):
                n = f'{d}_0{k}_{s}'
                a0 = cab[n]
                b0 = cab[f'{d}_0{k + 1}_{s}'] if k < 3 else a + eixo * max(((x - a).dot(eixo) for x in p), default=0.02)
                ax = (b0 - a0)
                L = ax.length
                ax.normalize()
                rs = sorted(((x - a0) - ax * (x - a0).dot(ax)).length for x in pts.get(n, []) if 0.15 * L < (x - a0).dot(ax) < 0.85 * L)
                out['raio'][n] = (rs[len(rs) // 2] if rs else 0.008) * fator
    return out



# ------------------------------------------------------------------ o personagem

LADOS = (('r', 'Right'), ('l', 'Left'))
NOMES_DEDO = {'Thumb': 'thumb', 'Index': 'index', 'Middle': 'middle', 'Ring': 'ring', 'Pinky': 'pinky'}


def mao_do_osso(s):
    return f'mixamorig:{"Right" if s == "r" else "Left"}Hand'


def importar_personagem(quem):
    """O humano.glb de antes (pose T), com os dedos já com os nomes do Manny
    e os metacarpos dele no esqueleto."""
    origem = os.path.join(COPIAS, f'humano_{quem}_antes_manny.glb')
    if not os.path.exists(origem):
        os.makedirs(COPIAS, exist_ok=True)
        import subprocess
        dados = subprocess.run(['git', '-C', RAIZ, 'show', f'HEAD:assets/personagens/{quem}/humano.glb'], capture_output=True).stdout
        open(origem, 'wb').write(dados)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=origem)
    for o in list(bpy.data.objects):
        if o.type == 'MESH' and o.parent is None:
            bpy.data.objects.remove(o)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    return arm, [o for o in bpy.data.objects if o.type == 'MESH'], origem


def renomear_dedos(arm):
    """RightHandIndex1 → index_01_r (e o resto): o osso e o grupo de vértices."""
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    ed = arm.data.edit_bones
    for s, lado in LADOS:
        for velho, novo in NOMES_DEDO.items():
            for k in (1, 2, 3, 4):
                ed[f'mixamorig:{lado}Hand{velho}{k}'].name = f'{novo}_0{k}_{s}'
    bpy.ops.object.mode_set(mode='OBJECT')


def criar_metacarpos(arm, manny):
    """Um osso por dedo de quatro (index, middle, ring, pinky), entre a mão e
    a primeira falange, na fração do caminho em que o do Manny fica."""
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    ed = arm.data.edit_bones
    for s, lado in LADOS:
        mao = ed[mao_do_osso(s)]
        for d in DEDOS4:
            m = manny['cab']
            frac = (m[f'{d}_metacarpal_{s}'] - m[f'hand_{s}']).length / (m[f'{d}_01_{s}'] - m[f'hand_{s}']).length
            f1 = ed[f'{d}_01_{s}']
            e = ed.new(f'{d}_metacarpal_{s}')
            e.head = mao.head + (f1.head - mao.head) * frac
            e.tail = f1.head
            e.roll = f1.roll
            e.parent = mao
            e.use_connect = False
            f1.parent = e
            f1.use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')


def pesos_dos_metacarpos(arm, malhas):
    """A pele da palma que a mão levava passa em parte para o metacarpo da
    faixa do dedo: nada na altura do pulso, quase tudo junto dos nós; entre
    dois dedos, dividido; do lado do polegar, fica na mão."""
    cab = {b.name: arm.matrix_world @ b.head_local for b in arm.data.bones}
    for s, lado in LADOS:
        mao = cab[mao_do_osso(s)]
        ao = (cab[f'middle_01_{s}'] - mao)
        L = ao.length
        ao.normalize()
        tr = (cab[f'index_01_{s}'] - cab[f'pinky_01_{s}'])
        tr.normalize()
        faixa = {d: (cab[f'{d}_01_{s}'] - mao).dot(tr) for d in DEDOS4}
        passo = abs(faixa['index'] - faixa['pinky']) / 3.0
        polegar = (cab[f'thumb_01_{s}'] - mao).dot(tr)
        n = 0
        for o in malhas:
            vg = {g.name: g for g in o.vertex_groups}
            if mao_do_osso(s) not in vg:
                continue
            gm = vg[mao_do_osso(s)]
            for d in DEDOS4:
                if f'{d}_metacarpal_{s}' not in vg:
                    o.vertex_groups.new(name=f'{d}_metacarpal_{s}')
            vg = {g.name: g for g in o.vertex_groups}
            for v in o.data.vertices:
                w = next((x.weight for x in v.groups if x.group == gm.index), 0.0)
                if w <= 0.0:
                    continue
                p = o.matrix_world @ v.co
                t = (p - mao).dot(ao) / L
                if t <= 0.1:
                    continue
                u = (p - mao).dot(tr)
                # só a palma: dentro da largura dos dedos, sem a faixa do polegar
                if min(faixa.values()) - 1.6 * passo > u or u > max(faixa.values()) + 1.6 * passo:
                    continue
                if abs(u - polegar) < abs(u - faixa['index']) and abs(polegar - faixa['index']) < 2.5 * passo:
                    continue
                suave = max(0.0, min(1.0, (t - 0.1) / 0.8))
                suave = suave * suave * (3 - 2 * suave)
                lan = {d: math.exp(-((u - faixa[d]) / (0.9 * passo)) ** 2) for d in DEDOS4}
                tot = sum(lan.values())
                if tot < 1e-6:
                    continue
                for d in DEDOS4:
                    wd = w * suave * lan[d] / tot
                    if wd > 0.002:
                        vg[f'{d}_metacarpal_{s}'].add([v.index], wd, 'REPLACE')
                        n += 1
                restante = w * (1.0 - suave * sum(lan.values()) / tot)
                gm.add([v.index], max(0.0, restante), 'REPLACE')
        print('MAO pesos de metacarpo', s, ':', n, 'vértices-osso')


def raio_do_osso(malhas, cab, nome, filho):
    """A grossura do dedo: do eixo do osso à pele dos vértices que ele leva mais."""
    if filho not in cab:
        return None
    a, b = cab[nome], cab[filho]
    eixo = b - a
    L = eixo.length
    eixo.normalize()
    rs = []
    for o in malhas:
        g = {x.index: x.name for x in o.vertex_groups}
        for v in o.data.vertices:
            if not v.groups or g[max(v.groups, key=lambda x: x.weight).group] != nome:
                continue
            p = o.matrix_world @ v.co
            t = (p - a).dot(eixo)
            if 0.15 * L < t < 0.85 * L:
                rs.append(((p - a) - eixo * t).length)
    rs.sort()
    return rs[len(rs) // 2] if rs else None


def ponta_do_dedo(malhas, nome, cab, quarto):
    """A ponta do dedo: a pele mais longe do osso do fim (o `_04`, que tem os
    vértices da ponta) ao longo da direção do dedo."""
    a = cab[nome]
    p = []
    for o in malhas:
        g = {x.index: x.name for x in o.vertex_groups}
        for v in o.data.vertices:
            if v.groups and g[max(v.groups, key=lambda y: y.weight).group] in (nome, quarto):
                p.append(o.matrix_world @ v.co)
    centro = sum(p, Vector()) / max(len(p), 1)
    eixo = (centro - a).normalized()
    return a + eixo * max(((x - a).dot(eixo) for x in p), default=0.02)


def fazer(quem):
    manny = importar_manny()
    arm, malhas, origem = importar_personagem(quem)
    renomear_dedos(arm)
    criar_metacarpos(arm, manny)
    pesos_dos_metacarpos(arm, malhas)
    bpy.context.view_layer.update()
    cab = {b.name: arm.matrix_world @ b.head_local for b in arm.data.bones}
    for s, _ in LADOS:
        cab[f'hand_{s}'] = cab[mao_do_osso(s)]
    relato, novo_cab, novo_ponta, afins, Qes = {}, {}, {}, {}, {}
    for s, _ in LADOS:
        cadeia = osso_cadeia(s)

        def quadro_mao(c):
            ao_longo = c[f'middle_01_{s}'] - c[f'hand_{s}']
            travessa = c[f'index_01_{s}'] - c[f'pinky_01_{s}']
            dentro = travessa.cross(ao_longo) if s == 'r' else ao_longo.cross(travessa)
            return orient(ao_longo, dentro), ao_longo, dentro
        Qa, ao_a, den_a = quadro_mao(cab)
        Qm, _, _ = quadro_mao(manny['cab'])
        pulso = cab[f'hand_{s}']
        Qe = Matrix.Translation(pulso) @ (Qa @ Qm.inverted()).to_4x4() @ Matrix.Translation(-manny['cab'][f'hand_{s}'])
        Qes[s] = Qe
        T = {n: Qe @ manny['cab'][n] for n in cadeia}
        tips = {f'{d}_03_{s}': Qe @ manny['ponta'][f'{d}_03_{s}'] for d in TODOS}
        gross = {}
        for d in TODOS:
            n = f'{d}_01_{s}'
            r_a = raio_do_osso(malhas, cab, n, f'{d}_02_{s}')
            r_m = manny['raio'][n]
            gross[d] = max(LIMITE_GROSSURA[0], min(LIMITE_GROSSURA[1], r_m / r_a)) if r_a else 1.0
            relato[f'{d}_{s}'] = dict(raio_aqui_mm=round((r_a or 0) * 1000, 1), raio_manny_mm=round(r_m * 1000, 1), fator=round(gross[d], 2))
        # 2) as raízes: o que a grossura não fechou (pelo limite), a distância
        # entre os dedos fecha: cada dedo anda para o médio
        C = (T[f'index_01_{s}'] - T[f'pinky_01_{s}']).normalized()
        u_med = T[f'middle_01_{s}'].dot(C)
        for d in DEDOS4:
            r_a = relato[f'{d}_{s}']['raio_aqui_mm'] / 1000.0
            k = min(1.0, (r_a * gross[d]) / manny['raio'][f'{d}_01_{s}']) if r_a else 1.0
            if k < 0.999:
                for n in cadeia:
                    if n.startswith(d):
                        T[n] = T[n] + C * ((T[n].dot(C) - u_med) * (k - 1.0))
            relato[f'{d}_{s}']['aproxima'] = round(k, 3)
        for n in cadeia:
            novo_cab[n] = T[n]
        novo_ponta.update(tips)
        for n, filho in cadeia.items():
            d = n.split('_')[0]
            if n.startswith('hand_'):
                s_len = (T[f'middle_01_{s}'] - pulso).length / (cab[f'middle_01_{s}'] - pulso).length
                s_lar = (T[f'index_01_{s}'] - T[f'pinky_01_{s}']).length / (cab[f'index_01_{s}'] - cab[f'pinky_01_{s}']).length
                A = ao_a.normalized()
                N = den_a.normalized()
                B = Matrix((A, N, A.cross(N).normalized())).transposed()
                afins[mao_do_osso(s)] = (pulso, pulso, B @ Matrix.Diagonal((s_len, 1.0, s_lar)) @ B.transposed())
                relato[f'palma_{s}'] = dict(comprimento=round(s_len, 3), largura=round(s_lar, 3))
                continue
            a_ = cab[n]
            quarto = n.replace('_03_', '_04_')
            b_ = cab[filho] if filho else ponta_do_dedo(malhas, n, cab, quarto)
            tb = T[filho] if filho else tips[n]
            vo, vn = b_ - a_, tb - T[n]
            u = vo.normalized()
            t = gross[d]
            S = Matrix.Identity(3) * t + Matrix([[u[i] * u[j] * (vn.length / vo.length - t) for j in range(3)] for i in range(3)])
            D = u.rotation_difference(vn.normalized()).to_matrix() @ S
            afins[n] = (a_, T[n], D)
            if filho is None:
                afins[quarto] = afins[n]   # a ponta leva o mesmo que a última falange

    mexidos = 0
    for o in malhas:
        g = {x.index: x.name for x in o.vertex_groups}
        M, Mi = o.matrix_world.copy(), o.matrix_world.inverted()
        for v in o.data.vertices:
            soma, novo, tem = 0.0, Vector(), False
            p = M @ v.co
            for x in v.groups:
                nome = g[x.group]
                soma += x.weight
                if nome in afins:
                    H, Tn, D = afins[nome]
                    novo += (Tn + D @ (p - H)) * x.weight
                    tem = True
                else:
                    novo += p * x.weight
            if tem and soma > 1e-6:
                v.co = Mi @ (novo / soma)
                mexidos += 1
        o.data.update()

    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    ed = arm.data.edit_bones
    nomes_mao = {}
    for s, _ in LADOS:
        for n in osso_cadeia(s):
            nomes_mao[n] = mao_do_osso(s) if n.startswith('hand_') else n
        for d in TODOS:
            nomes_mao[f'{d}_04_{s}'] = f'{d}_04_{s}'
    zs = {}
    for s_, _ in LADOS:
        Qe_ = Qes[s_]
        for n in nomes_mao:
            if n.endswith('_' + s_) and not n.startswith('hand_') and n in manny['eixo_z']:
                zs[n] = (Qe_.to_3x3() @ manny['eixo_z'][n]).normalized()
    for nm in nomes_mao.values():
        if nm in ed:
            ed[nm].use_connect = False
    for s, _ in LADOS:
        for n, filho in osso_cadeia(s).items():
            if n.startswith('hand_'):
                continue   # a mão fica exatamente como era (a rotação do osso vai para o jogo)
            # Os ossos do Manny têm o eixo Y do osso na direção que o animador deu
            # (perpendicular à falange, como no UE), e a rotação local de um é a
            # do outro só se os eixos forem os mesmos: cabeça onde a falange fica,
            # eixos (Y e Z) os dele.
            e = ed[nomes_mao[n]]
            e.head = novo_cab[n]
            comp = ((novo_cab[filho] if filho else novo_ponta[n]) - novo_cab[n]).length
            e.tail = novo_cab[n] + (Qes[s].to_3x3() @ manny['eixo_y'][n]).normalized() * comp
            e.align_roll(zs[n])
        for d in TODOS:
            e4 = ed[f'{d}_04_{s}']
            e4.head = novo_ponta[f'{d}_03_{s}']
            e4.tail = e4.head + (Qes[s].to_3x3() @ manny['eixo_y'][f'{d}_03_{s}']).normalized() * 0.012
            if f'{d}_03_{s}' in zs:
                e4.align_roll(zs[f'{d}_03_{s}'])
    bpy.ops.object.mode_set(mode='OBJECT')
    print('MAO', quem, 'vértices remodelados:', mexidos)
    for k, v in relato.items():
        print('MAO', quem, k, v)
    return arm, malhas, relato, manny, Qes, origem


# ------------------------------------------------------------------ fotos

def preparar_cena():
    import poses_armas as pa
    pa.preparar_fotos()
    for o in bpy.data.objects:
        if o.name.startswith('Plane'):
            o.hide_render = True
    c = bpy.context.scene
    c.cycles.samples = 24
    c.render.resolution_x = c.render.resolution_y = 520
    return pa


def fotos_da_mao(pa, arm, nome_base, mao_nome='mixamorig:RightHand', medio='middle_01_r', indice='index_01_r', minimo='pinky_01_r'):
    b = arm.data.bones
    w = lambda n: arm.matrix_world @ b[n].head_local
    pulso = w(mao_nome)
    ao = (w(medio) - pulso).normalized()
    N = (w(indice) - w(minimo)).cross(ao).normalized()
    C = ao.cross(N).normalized()
    pivo = pulso + ao * 0.085
    out = []
    for nome, d in (('dorso', N), ('palma', -N), ('lado', C)):
        c = f'/private/tmp/_{nome_base}_{nome}.png'
        pa.foto(c, pivo + d * 0.45, pivo, 38)
        out.append(c)
    return out


def montar(linhas, caminho, titulos):
    from PIL import Image, ImageDraw
    W = 520
    folha = Image.new('RGB', (W * len(linhas[0]), (W + 18) * len(linhas)), (20, 20, 20))
    d = ImageDraw.Draw(folha)
    for i, (fotos, t) in enumerate(zip(linhas, titulos)):
        d.text((6, i * (W + 18) + 3), t, fill=(255, 255, 255))
        for j, f in enumerate(fotos):
            folha.paste(Image.open(f).convert('RGB'), (j * W, i * (W + 18) + 18))
            os.remove(f)
    folha.save(caminho, quality=90)


def main():
    args = sys.argv[1:]
    quem = args[0]
    aplicar = '--aplicar' in args
    os.makedirs(SAIDA, exist_ok=True)
    # as fotos de "antes" precisam do personagem como veio: abre, fotografa, e refaz
    arm0, _, _ = importar_personagem(quem)
    pa = preparar_cena()
    antes = fotos_da_mao(pa, arm0, f'{quem}_antes', medio='mixamorig:RightHandMiddle1', indice='mixamorig:RightHandIndex1', minimo='mixamorig:RightHandPinky1')
    arm, malhas, relato, manny, Qes, origem = fazer(quem)
    pa = preparar_cena()
    depois = fotos_da_mao(pa, arm, f'{quem}_depois')
    # o Manny, na mesma mão
    antes_objs = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(RAIZ, 'assets/armas/fp/maos/maos.glb'))
    novos = [o for o in bpy.data.objects if o not in antes_objs]
    for o in novos:
        if o.type == 'ARMATURE' and o.parent is None:
            o.matrix_world = Qes['r'] @ Matrix.Scale(manny['fator'], 4) @ o.matrix_world
    raizes = novos
    for o in malhas:
        o.hide_render = True
    ref = fotos_da_mao(pa, arm, f'{quem}_manny')
    for o in malhas:
        o.hide_render = False
    for o in raizes:
        o.hide_render = True
        o.hide_viewport = True
    # o corpo inteiro em pose T
    c = f'/private/tmp/_{quem}_corpo.png'
    cena = bpy.context.scene
    cena.render.resolution_x, cena.render.resolution_y = 1100, 640
    centro = Vector((0, 0, 0.92))
    pa.foto(c, centro + Vector((0, -4.0, 0.15)), centro, 35)
    from PIL import Image
    Image.open(c).convert('RGB').save(os.path.join(SAIDA, f'{quem}_tpose_corpo.jpg'), quality=90)
    os.remove(c)
    montar([antes, depois, ref], os.path.join(SAIDA, f'{quem}_mao_antes_depois_manny.jpg'),
           [f'{quem}: ANTES (humano.glb de antes)', f'{quem}: DEPOIS (ossos, raízes e tamanho do Manny)', 'MANNY (a mão da primeira pessoa, no mesmo lugar)'])
    caminho_blend = os.path.join(AQUI, 'poses', f'{quem}_tpose_manny.blend')
    bpy.ops.wm.save_as_mainfile(filepath=caminho_blend)
    print('BLEND', caminho_blend)
    # o .glb: só o personagem
    destino = os.path.join(SAIDA, f'{quem}_humano_manny_tpose.glb')
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    for m in malhas:
        m.select_set(True)
    bpy.ops.export_scene.gltf(filepath=destino, export_format='GLB', use_selection=True, export_animations=False)
    print('GLB', destino)
    if aplicar:
        shutil.copy(destino, os.path.join(RAIZ, 'assets/personagens', quem, 'humano.glb'))
        print('APLICADO em assets/personagens/', quem)


if __name__ == '__main__':
    main()
