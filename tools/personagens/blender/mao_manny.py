#!/usr/bin/env python3
"""
A mão do personagem com o esqueleto e as proporções da mão da primeira pessoa
(Manny), aproveitando a luva dele (Jhon, 23/11: "readaptarmos a eve e barut,
braços, ombros, mãos e dedos para se comportarem exatamente como a do Manny").

    python3 tools/personagens/blender/mao_manny.py eve ar15              # remodela a luva e gera o ar15.json
    python3 tools/personagens/blender/mao_manny.py eve ar15 --so-blend   # só refaz o .blend de ajuste
    python3 tools/personagens/blender/mao_manny.py eve ar15 --do-blend   # regrava o .json a partir do .blend

O que faz, em ordem:

1. Parte do `humano.glb` de ANTES (a cópia em `.synctmp/terceira_pessoa_2311/`,
   feita na primeira vez), mede a mão da primeira pessoa
   (`assets/armas/fp/maos/maos.glb`) e põe cada junta de cada dedo onde a do
   Manny fica (alinhada pela palma): mesmo comprimento de falange, mesma
   ponta. A grossura é a da luva do personagem.
2. Grava o `assets/personagens/<p>/humano.glb` novo remendando o arquivo de
   antes, sem passar pelo exportador do Blender (que giraria os ossos do
   corpo inteiro e quebraria os clipes do Mixamo): só mudam a posição dos
   vértices e das normais que dedo leva, a matriz de ligação (IBM) e a
   translação local dos nós dos dedos. Os nomes e as rotações de repouso de
   todos os ossos ficam iguais.
3. Gera `assets/personagens/<p>/animacoes/<arma>.json` com a mão copiando a
   da primeira pessoa osso por osso (sem a procura de encaixe do
   animacoes_armas.py), o braço por IK até o pulso, e um alívio mínimo da
   base dos dedos que entram na arma (a mão de lá é mais grossa).
4. Grava `tools/personagens/blender/poses/<p>_<arma>_manny.blend`: o personagem
   com a mão nova, a arma, uma ação por clipe e caixas de controle (as formas
   dos ossos viram caixas, como no rig do Manny). Mexa nos ossos no modo de
   pose e, com `--do-blend`, o .json é regravado a partir das ações.

Precisa do módulo `bpy` (`pip install bpy`).
"""
import base64  # noqa: F401
import json
import math
import os
import shutil
import struct
import sys

import numpy as np
from scipy.optimize import least_squares

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)

import bpy  # noqa: E402
from mathutils import Matrix, Quaternion, Vector  # noqa: E402

import animacoes_armas as aa  # noqa: E402
import poses_armas as pa  # noqa: E402
import validar_maos  # noqa: E402
from poses_armas import CIMA, DEDOS, DEDOS_FP, EIXOS, RAIZ, orient  # noqa: E402

COPIAS = os.path.join(RAIZ, '.synctmp/terceira_pessoa_2311')
E3 = EIXOS.to_3x3()
GROSSURA = float(os.environ.get('GROSSURA', 1.0))
# O custo (mm por rad) de uma falange sair da rotação copiada da primeira pessoa.
REG_COPIA = float(os.environ.get('REG_COPIA', 8.0))


def quadro(m):
    """A matriz sem a escala."""
    return Matrix.Translation(m.translation) @ m.to_3x3().normalized().to_4x4()


# ------------------------------------------------------------------ a mão de lá

def medir_manny():
    """Da mão da primeira pessoa (maos.glb), em metros: a ponta de cada dedo
    (no quadro da última falange) e o comprimento pulso→médio."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(RAIZ, 'assets/armas/fp/maos/maos.glb'))
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    malha = next(o for o in bpy.data.objects if o.type == 'MESH' and o.vertex_groups and 'maos' in o.name.lower())
    grupos = {g.index: g.name for g in malha.vertex_groups}
    arm.animation_data_clear()
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    em = malha.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
    por_osso = {}
    for v, ve in zip(malha.data.vertices, em.vertices):
        if v.groups:
            g = max(v.groups, key=lambda g: g.weight)
            por_osso.setdefault(grupos[g.group], []).append(malha.matrix_world @ ve.co)
    W = {pb.name: quadro(arm.matrix_world @ pb.matrix) for pb in arm.pose.bones}
    ponta = {}
    for s in ('r', 'l'):
        for d in DEDOS_FP:
            nome = f'{d}_03_{s}'
            a = W[nome].translation
            pts = por_osso.get(nome, [])
            centro = sum(pts, Vector()) / max(len(pts), 1)
            eixo = (centro - a).normalized()
            b = a + eixo * max(((p - a).dot(eixo) for p in pts), default=0.02)
            ponta[nome] = W[nome].inverted() @ b
    return ponta, (W['middle_01_r'].translation - W['hand_r'].translation).length


class MaoManny:
    """A mão de um lado do personagem refeita nas proporções da de lá."""

    def __init__(self, corpo, rig, lado, ponta_fp):
        self.corpo, self.lado = corpo, lado
        s = 'r' if lado == 'Right' else 'l'
        self.s = s
        eb = corpo.arm.data.bones
        Wr = {x.name: quadro(rig.arm.matrix_world @ x.matrix_local) for x in rig.arm.data.bones}
        pw, pm = Wr[f'hand_{s}'].translation, Wr[f'middle_01_{s}'].translation
        ao_longo = pm - pw
        travessa = Wr[f'index_01_{s}'].translation - Wr[f'pinky_01_{s}'].translation
        dentro = travessa.cross(ao_longo) if lado == 'Right' else ao_longo.cross(travessa)
        m = corpo.mao[lado]
        E = eb[corpo.n(lado + 'Hand')].matrix_local.copy()
        E3_ = E.to_3x3()
        Q = orient(E3_ @ m['dedos'], E3_ @ m['palma']) @ orient(ao_longo, dentro).inverted()
        ancora_e = E.translation + E3_ @ (m['nos'] * aa.ALINHA)
        ancora_m = pw.lerp(pm, aa.ALINHA)
        self.Qe = Matrix.Translation(ancora_e) @ Q.to_4x4() @ Matrix.Translation(-ancora_m)
        self.Wr = Wr
        self.alvos, self.velhos = {}, {}
        for d, df in zip(DEDOS, DEDOS_FP):
            T = [self.Qe @ Wr[f'{df}_0{k}_{s}'].translation for k in (1, 2, 3)]
            T.append(self.Qe @ (Wr[f'{df}_03_{s}'] @ ponta_fp[f'{df}_03_{s}']))
            self.alvos[d] = T
            self.velhos[d] = [eb[corpo.n(f'{lado}Hand{d}{k}')].head_local.copy() for k in (1, 2, 3, 4)]

    def afins(self):
        """Por osso de dedo (nome): (H, T, D), com p -> T + D (p - H), no espaço do Blender."""
        out = {}
        for d in DEDOS:
            T, H = self.alvos[d], self.velhos[d]
            for k in range(3):
                vo, vn = H[k + 1] - H[k], T[k + 1] - T[k]
                u = vo.normalized()
                estica = vn.length / vo.length
                S = Matrix.Identity(3) * GROSSURA + Matrix([[u[i] * u[j] * (estica - GROSSURA) for j in range(3)] for i in range(3)])
                D = u.rotation_difference(vn.normalized()).to_matrix() @ S
                out[self.corpo.n(f'{self.lado}Hand{d}{k + 1}')] = (H[k], T[k], D)
            out[self.corpo.n(f'{self.lado}Hand{d}4')] = out[self.corpo.n(f'{self.lado}Hand{d}3')]
        return out


# ------------------------------------------------------------------ remendar o .glb

_COMP = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
_NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


def acessor(j, binario, i):
    a = j['accessors'][i]
    bv = j['bufferViews'][a['bufferView']]
    dt = np.dtype(_COMP[a['componentType']])
    nc = _NC[a['type']]
    assert not bv.get('byteStride') or bv['byteStride'] == nc * dt.itemsize, 'acessor intercalado'
    return np.frombuffer(binario, dt, a['count'] * nc, bv.get('byteOffset', 0) + a.get('byteOffset', 0)).reshape(a['count'], nc), a


def remendar_glb(origem, destino, maos):
    """Grava `destino` igual a `origem`, com os dedos remodelados (veja o topo)."""
    dados = open(origem, 'rb').read()
    n = struct.unpack('<I', dados[12:16])[0]
    j = json.loads(dados[20:20 + n])
    m_bin = 20 + n
    n_bin = struct.unpack('<I', dados[m_bin:m_bin + 4])[0]
    binario = bytearray(dados[m_bin + 8:m_bin + 8 + n_bin])
    nos = j['nodes']
    juntas = j['skins'][0]['joints']
    indice_junta = {nos[i]['name']: k for k, i in enumerate(juntas)}
    pai_no = {}
    for i, no in enumerate(nos):
        for c in no.get('children', []):
            pai_no[c] = i
    repouso = pa.gltf_repouso(origem)  # nome -> (mundo, nome do pai)

    afins = {}
    for mm in maos:
        afins.update(mm.afins())
    # espaço do glTF
    G = {}
    for nome, (H, T, D) in afins.items():
        G[nome] = (EIXOS @ H, EIXOS @ T, E3 @ D @ E3.inverted())
    # os nós dos dedos: onde cada junta (as quatro do dedo) estava e onde fica
    junta_nova = {}
    for mm in maos:
        for d in DEDOS:
            for k in range(4):
                junta_nova[mm.corpo.n(f'{mm.lado}Hand{d}{k + 1}')] = (EIXOS @ mm.velhos[d][k], EIXOS @ mm.alvos[d][k])
    # confere: a cabeça do osso do Blender é o nó do glTF
    pior = max((H - repouso[nome][0].translation).length for nome, (H, T) in junta_nova.items())
    assert pior < 1e-4, f'a cabeça do osso não bate com o nó do glTF ({pior})'

    # nós: o mundo novo (a rotação de antes, a posição nova) e a translação local
    mundo_novo = {}
    for nome, (H, T) in junta_nova.items():
        W = repouso[nome][0].copy()
        W.translation = T
        mundo_novo[nome] = W
    for nome in junta_nova:
        pai = repouso[nome][1]
        Wp = mundo_novo.get(pai, repouso[pai][0] if pai in repouso else Matrix.Identity(4))
        L = Wp.inverted() @ mundo_novo[nome]
        i = next(i for i in juntas if nos[i]['name'] == nome)
        nos[i]['translation'] = [float(x) for x in L.translation]

    # matrizes de ligação
    ibm, a_ibm = acessor(j, binario, j['skins'][0]['inverseBindMatrices'])
    for nome, W in mundo_novo.items():
        k = indice_junta[nome]
        velho = np.array(repouso[nome][0].inverted().transposed()).reshape(16)
        assert np.abs(ibm[k] - velho).max() < 5e-3, f'a matriz de ligação de {nome} não é a inversa do repouso'
        ibm[k] = np.array(W.inverted().transposed()).reshape(16).astype(np.float32)

    # vértices e normais
    nj = len(juntas)
    eh = np.zeros(nj, bool)
    Tm, Hm, Dm = np.zeros((nj, 3)), np.zeros((nj, 3)), np.tile(np.eye(3), (nj, 1, 1))
    for nome, (H, T, D) in G.items():
        k = indice_junta[nome]
        eh[k] = True
        Tm[k], Hm[k], Dm[k] = np.array(T), np.array(H), np.array(D)
    Dinv_t = np.array([np.linalg.inv(x).T for x in Dm])
    mexidos = 0
    for mesh in j['meshes']:
        for prim in mesh['primitives']:
            at = prim['attributes']
            pos, a_pos = acessor(j, binario, at['POSITION'])
            nor, _ = acessor(j, binario, at['NORMAL'])
            jo, _ = acessor(j, binario, at['JOINTS_0'])
            we, a_we = acessor(j, binario, at['WEIGHTS_0'])
            w = we.astype(np.float64)
            if a_we['componentType'] != 5126:
                w = w / np.iinfo(_COMP[a_we['componentType']]).max
            jo = jo.astype(np.int64)
            tem = (eh[jo] & (w > 0)).any(axis=1)
            if not tem.any():
                continue
            p0 = pos.astype(np.float64)
            n0 = nor.astype(np.float64)
            soma = w.sum(axis=1, keepdims=True)
            novo_p = np.zeros_like(p0)
            novo_n = np.zeros_like(n0)
            for c in range(4):
                k = jo[:, c]
                f = eh[k][:, None]
                p_c = np.where(f, Tm[k] + np.einsum('nij,nj->ni', Dm[k], p0 - Hm[k]), p0)
                n_c = np.where(f, np.einsum('nij,nj->ni', Dinv_t[k], n0), n0)
                novo_p += w[:, c:c + 1] * p_c
                novo_n += w[:, c:c + 1] * n_c
            novo_p = novo_p / np.maximum(soma, 1e-9)
            nn = np.linalg.norm(novo_n, axis=1, keepdims=True)
            novo_n = novo_n / np.maximum(nn, 1e-9)
            pos[tem] = novo_p[tem].astype(np.float32)
            nor[tem] = novo_n[tem].astype(np.float32)
            a_pos['min'] = [float(x) for x in pos.min(axis=0)]
            a_pos['max'] = [float(x) for x in pos.max(axis=0)]
            mexidos += int(tem.sum())

    novo_json = json.dumps(j, separators=(',', ':')).encode('utf-8')
    novo_json += b' ' * ((4 - len(novo_json) % 4) % 4)
    total = 12 + 8 + len(novo_json) + 8 + len(binario)
    saida = struct.pack('<4sII', b'glTF', 2, total) + struct.pack('<I4s', len(novo_json), b'JSON') + novo_json
    saida += struct.pack('<I4s', len(binario), b'BIN\x00') + bytes(binario)
    open(destino, 'wb').write(saida)
    print(f'GLB {destino}: {mexidos} vértices e normais remodelados, {len(G)} ossos de dedo, {len(saida) / 1e6:.2f} MB', flush=True)


# ------------------------------------------------------------------ os quadros

def bases_do_clipe(corpo, rig, plano, clipe):
    """A postura e o lugar do conjunto em cada quadro, como o animacoes_armas
    faz (a cabeça do animador incluída)."""
    n = rig.quadros(clipe)
    trans = aa.TRANSICOES.get(clipe)
    progresso = aa.progresso_transicao(rig, clipe, n) if trans else None
    estado = plano.estado(clipe)
    quadros = []
    for k in range(n + 1):
        if trans and trans[0] in plano.posturas and trans[1] in plano.posturas:
            w = aa.suave(progresso[k])
            base = aa.misturar_locais(plano.posturas[trans[0]], plano.posturas[trans[1]], w)
            col = aa.misturar_matriz(plano.colocacoes[trans[0]], plano.colocacoes[trans[1]], w)
            est = trans[1] if w > 0.5 else trans[0]
        else:
            base, col, est = plano.posturas[estado], plano.colocacoes[estado], estado
        locais = dict(base)
        rig.tocar(clipe, k)
        cam = rig.camera()
        if cam.length > 0.01:
            q = (Quaternion(CIMA, aa.rad(cam.y)) @ Quaternion(pa.DIREITA, aa.rad(cam.x)) @ Quaternion(pa.FRENTE, aa.rad(-cam.z)))
            meia = Quaternion().slerp(q, 0.5)
            M0 = corpo.esq.fk(locais)
            for nome in (corpo.n('Neck'), corpo.n('Head')):
                corpo.esq.por(M0, locais, nome, meia.to_matrix() @ M0[nome].to_3x3())
        w_ = aa.suave(progresso[k]) if (trans and trans[0] in plano.posturas and trans[1] in plano.posturas) else None
        # quanto do quadro é a postura da mira (0 a 1; no mirar e no desmirar, o caminho)
        w_mira = (w_ if trans[1] == 'mira' else 1.0 - w_) if w_ is not None else (1.0 if est == 'mira' else 0.0)
        quadros.append(dict(locais=locais, col=col, est=est, w=w_, w_mira=w_mira))
    return n, quadros


class Gerador:
    def __init__(self, corpo, rig, plano, Qe, ponta_fp, malha):
        self.corpo, self.rig, self.plano, self.malha = corpo, rig, plano, malha
        self.ponta_fp = ponta_fp
        eb = corpo.arm.data.bones
        rig.colocar(Matrix.Identity(4))
        self.K = {}
        for lado in ('Right', 'Left'):
            s = 'r' if lado == 'Right' else 'l'
            Wr = {x.name: quadro(rig.arm.matrix_world @ x.matrix_local) for x in rig.arm.data.bones}
            self.K[(lado, 'Hand')] = (Qe[lado] @ Wr[f'hand_{s}']).inverted() @ eb[corpo.n(lado + 'Hand')].matrix_local
            for d, df in zip(DEDOS, DEDOS_FP):
                for k in (1, 2, 3):
                    self.K[(lado, d, k)] = (Qe[lado] @ Wr[f'{df}_0{k}_{s}']).inverted() @ eb[corpo.n(f'{lado}Hand{d}{k}')].matrix_local
        self.braco = {'Right': aa.Braco(corpo, 'Right'), 'Left': aa.Braco(corpo, 'Left')}
        self.alivio = {}   # (lado, dedo) -> (eixo no quadro da mão, graus)
        self.erros = []
        self.por_dedo = {}

    def ajustar_dedo(self, M, lado, d, rots, alvos):
        """O dedo copia as rotações de lá, mas a base dele não está onde a de lá
        está: na primeira pessoa a mão que segura a arma fecha a palma (os
        metacarpos dela se mexem, até 10 mm na raiz dos dedos e 25 no polegar)
        e o esqueleto do personagem não tem metacarpos. As rotações de cada
        falange são acertadas, o mínimo, para a junta do meio, a da ponta e a
        ponta ficarem onde as de lá ficam (mínimos quadrados, com um custo
        para sair da cópia)."""
        esq, corpo = self.corpo.esq, self.corpo
        ossos = [corpo.n(f'{lado}Hand{d}{k}') for k in (1, 2, 3, 4)]
        b = esq.base(ossos[0], M).translation
        t = [esq.desvio[ossos[k]].translation for k in (1, 2, 3)]
        alvo = np.array([[v[i] for i in range(3)] for v in alvos]) * 1000.0
        R0 = [np.array(r) for r in rots]
        bn = np.array(b) * 1000.0
        tn = [np.array(v) * 1000.0 for v in t]

        def exp(v):
            ang = np.linalg.norm(v)
            if ang < 1e-9:
                return np.eye(3)
            k = v / ang
            K = np.array([[0, -k[2], k[1]], [k[2], 0, -k[0]], [-k[1], k[0], 0]])
            return np.eye(3) + math.sin(ang) * K + (1 - math.cos(ang)) * (K @ K)

        def pos(x):
            R = [exp(x[3 * i:3 * i + 3]) @ R0[i] for i in range(3)]
            p1 = bn
            p2 = p1 + R[0] @ tn[0]
            p3 = p2 + R[1] @ tn[1]
            p4 = p3 + R[2] @ tn[2]
            return R, (p2, p3, p4)

        def res(x):
            _, (p2, p3, p4) = pos(x)
            return np.concatenate([p2 - alvo[0], p3 - alvo[1], p4 - alvo[2], x * REG_COPIA])

        antes = res(np.zeros(9))[:9]
        sol = least_squares(res, np.zeros(9), method='lm')
        R, _ = pos(sol.x)
        erro = np.abs(sol.fun[:9]).max()
        self.por_dedo[(lado, d)] = max(self.por_dedo.get((lado, d), 0.0), erro)
        return [Matrix(r.tolist()) for r in R], float(np.abs(antes).max()), float(erro)

    def mao_e_dedos(self, M, locais, q, clipe_k, com_alivio=True):
        """Os braços por IK até o pulso de lá e os dedos copiando os de lá."""
        corpo, rig = self.corpo, self.rig
        des = self.plano.desenhos[q['est']]
        medidas = {}
        for lado, pref in (('Right', des['cotovelo_d']), ('Left', des['cotovelo_e'])):
            s = 'r' if lado == 'Right' else 'l'
            fp = rig.mao(lado)
            alvo = quadro(rig.m(f'hand_{s}')) @ self.K[(lado, 'Hand')]
            r = self.braco[lado].resolver(M, locais, alvo.translation, alvo.to_3x3(), pref, corpo.mao[lado],
                                          cotovelo_max_z=0.03, peso_tempo=1.0 if clipe_k else 0.0, alvo=(fp['dobra'], fp['lado']))
            leva3 = (M[corpo.n(lado + 'Hand')] @ alvo.inverted()).to_3x3().normalized()
            Rh = M[corpo.n(lado + 'Hand')].to_3x3().normalized()
            medidas[lado] = r['falta']
            for d, df in zip(DEDOS, DEDOS_FP):
                rots = [leva3 @ quadro(rig.m(f'{df}_0{k}_{s}')).to_3x3() @ self.K[(lado, d, k)].to_3x3() for k in (1, 2, 3)]
                alvos = [quadro(rig.m(f'{df}_0{k}_{s}')).translation for k in (2, 3)]
                alvos.append(quadro(rig.m(f'{df}_03_{s}')) @ self.ponta_fp[f'{df}_03_{s}'])
                rots, e0, e1 = self.ajustar_dedo(M, lado, d, rots, alvos)
                self.erros.append((e0, e1))
                if com_alivio and (lado, d) in self.alivio:
                    eixo_l, ang = self.alivio[(lado, d)]
                    R = Quaternion(Rh @ eixo_l, math.radians(ang)).to_matrix()
                    rots = [R @ r_ for r_ in rots]
                for k in (1, 2, 3):
                    corpo.esq.por(M, locais, corpo.n(f'{lado}Hand{d}{k}'), rots[k - 1])
        return medidas

    def achar_alivio(self, clipe='parado'):
        """A base de cada dedo que entra na arma abre o mínimo (até 15°) até sair:
        a mão de lá é mais grossa, e afundava a polpa no cabo."""
        corpo, rig = self.corpo, self.rig
        n, quadros = bases_do_clipe(corpo, rig, self.plano, clipe)
        q = quadros[0]
        rig.tocar(clipe, 0)
        rig.colocar(q['col'])
        locais = dict(q['locais'])
        M = corpo.esq.fk(locais)
        self.mao_e_dedos(M, locais, q, 0, com_alivio=False)
        corpo.aplicar(locais)
        bpy.context.view_layer.update()
        dedo, _, _ = self.malha.medir(rig)
        print('ALIVIO antes, a arma entra (mm):', {f'{l[0]}{d}': round(v * 1000, 1) for (l, d), v in dedo.items() if v > 0.0003}, flush=True)
        pbs = corpo.arm.pose.bones
        for lado in ('Right', 'Left'):
            Rh = pbs[corpo.n(lado + 'Hand')].matrix.to_3x3().normalized()
            N = (Rh @ corpo.mao[lado]['palma']).normalized()
            for d in DEDOS:
                v0 = dedo.get((lado, d), 0.0)
                if v0 <= 0.001:
                    continue
                pb = pbs[corpo.n(f'{lado}Hand{d}1')]
                base = pb.matrix.copy()
                p = base.translation.copy()
                y = (base.to_3x3() @ Vector((0, 1, 0))).normalized()
                e1 = y.cross(N).normalized()
                melhor = (v0, base, None, 0.0)
                for ang in (2, 4, 6, 8, 10, 12, 15):
                    for eixo_ in (e1, N):
                        for sinal in (1.0, -1.0):
                            giro = Quaternion(eixo_, math.radians(sinal * ang)).to_matrix().to_4x4()
                            pb.matrix = Matrix.Translation(p) @ giro @ Matrix.Translation(-p) @ base
                            bpy.context.view_layer.update()
                            v = self.malha.medir(rig)[0].get((lado, d), 0.0)
                            if v < melhor[0] - 1e-5:
                                melhor = (v, pb.matrix.copy(), Rh.inverted() @ eixo_, sinal * ang)
                    if melhor[0] <= 0.0005:
                        break
                pb.matrix = melhor[1]
                bpy.context.view_layer.update()
                if melhor[2] is not None:
                    self.alivio[(lado, d)] = (melhor[2], melhor[3])
                print(f'ALIVIO {lado} {d}: {v0 * 1000:.1f} -> {melhor[0] * 1000:.1f} mm com {melhor[3]:.0f}°', flush=True)

    def clipe(self, nome):
        corpo, rig = self.corpo, self.rig
        n, quadros = bases_do_clipe(corpo, rig, self.plano, nome)
        out = aa.Clipe(nome, n + 1)
        pior_falta, pior_ponta = 0.0, 0.0
        self.erros = []
        for k, q in enumerate(quadros):
            locais = dict(q['locais'])
            M = corpo.esq.fk(locais)
            rig.tocar(nome, k)
            rig.colocar(q['col'])
            falta = self.mao_e_dedos(M, locais, q, k)
            pior_falta = max(pior_falta, falta['Right'], falta['Left'])
            # a ponta de cada dedo daqui (só pela rotação, como o jogo toca) e a de lá
            for lado in ('Right', 'Left'):
                s = 'r' if lado == 'Right' else 'l'
                for d, df in zip(DEDOS, DEDOS_FP):
                    if (lado, d) in self.alivio:
                        continue
                    la = quadro(rig.m(f'{df}_03_{s}')) @ self.ponta_fp[f'{df}_03_{s}']
                    pior_ponta = max(pior_ponta, (M[corpo.n(f'{lado}Hand{d}4')].translation - la).length)
            jogo, mundo = corpo.no_jogo(M)
            out.ossos.append({n_: jogo[corpo.n(n_)] for n_ in aa.OSSOS if corpo.n(n_) in jogo})
            conj = EIXOS @ q['col'] @ EIXOS.inverted()
            out.conjunto.append(mundo[corpo.n('Spine2')].inverted() @ conj)
            out.locais.append(dict(locais))
            out.colocacoes.append(q['col'].copy())
        e0 = max(e[0] for e in self.erros)
        e1 = max(e[1] for e in self.erros)
        print(f'CLIPE {nome:12s} {out.quadros:3d} q: mão a {pior_falta * 1000:.1f} mm do lugar; dedos: a copia pura erra '
              f'até {e0:.1f} mm (juntas e ponta), depois do ajuste {e1:.1f} mm', flush=True)
        return out


# ------------------------------------------------------------------ o .blend de ajuste

def caixa_de_controle():
    """A forma dos controles: uma caixa vazada (como no rig do Manny)."""
    malha = bpy.data.meshes.new('caixa_controle')
    v = [(x, y, z) for x in (-0.5, 0.5) for y in (0.0, 1.0) for z in (-0.5, 0.5)]
    malha.from_pydata(v, [(0, 1), (1, 3), (3, 2), (2, 0), (4, 5), (5, 7), (7, 6), (6, 4), (0, 4), (1, 5), (2, 6), (3, 7)], [])
    obj = bpy.data.objects.new('caixa_controle', malha)
    colecao = bpy.data.collections.new('controles')
    bpy.context.scene.collection.children.link(colecao)
    colecao.objects.link(obj)
    colecao.hide_viewport = True
    colecao.hide_render = True
    return obj


def montar_blend(corpo, rig, clipes, caminho, personagem, arma):
    arm = corpo.arm
    forma = caixa_de_controle()
    larg = {'Shoulder': 0.5, 'Arm': 0.4, 'ForeArm': 0.35, 'Hand': 0.55}
    for pb in arm.pose.bones:
        curto = pb.name.replace(pa.PREFIXO, '')
        if not any(t in curto for t in ('Arm', 'Shoulder', 'Hand', 'Spine', 'Neck', 'Head')):
            continue
        pb.custom_shape = forma
        w = 0.5
        for t, v in larg.items():
            if curto.endswith(t):
                w = v
        if 'HandThumb' in curto or any(f'Hand{d}' in curto for d in DEDOS):
            w = 0.55
        pb.custom_shape_scale_xyz = (w, 1.0, w)
        pb.use_custom_shape_bone_size = True
        pb.bone.show_wire = True
        pb.color.palette = 'THEME04' if curto.startswith('Left') else ('THEME01' if curto.startswith('Right') else 'THEME09')
    arm.data.display_type = 'OCTAHEDRAL'
    arm.show_in_front = True
    cena = bpy.context.scene
    cena.render.fps = aa.FPS
    # uma ação por clipe, uma chave por quadro, na rotação local de cada osso
    arm.animation_data_create()
    cols = {}
    for c in clipes:
        acao = bpy.data.actions.new(f'{personagem}_{arma}_{c.nome}')
        arm.animation_data.action = acao
        for k in range(c.quadros):
            for pb in arm.pose.bones:
                q = c.locais[k].get(pb.name)
                pb.rotation_mode = 'QUATERNION'
                pb.rotation_quaternion = q if q is not None else Quaternion()
                pb.keyframe_insert('rotation_quaternion', frame=k + 1)
        acao.use_fake_user = True
        cols[c.nome] = [[list(r) for r in m] for m in c.colocacoes]
    cena['manny_colocacoes'] = json.dumps(cols)
    cena['manny_arma'] = arma
    cena['manny_personagem'] = personagem
    arm.animation_data.action = bpy.data.actions[f'{personagem}_{arma}_parado'] if f'{personagem}_{arma}_parado' in bpy.data.actions else None
    cena.frame_start, cena.frame_end = 1, max(c.quadros for c in clipes)
    cena.frame_set(1)
    for o in rig.bracos:
        o.hide_viewport = True
        o.hide_render = True
    rig.tocar('parado', 0)
    rig.colocar(clipes[0].colocacoes[0])
    os.makedirs(os.path.dirname(caminho), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=caminho)
    print('BLEND', caminho, f'{os.path.getsize(caminho) / 1e6:.1f} MB', flush=True)


def regravar_do_blend(caminho, glb, personagem, arma):
    bpy.ops.wm.open_mainfile(filepath=caminho)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE' and o.name != 'Bracos' and o.pose.bones.get(pa.PREFIXO + 'Hips'))
    corpo = aa.Corpo.__new__(aa.Corpo)
    pa.Corpo.__init__(corpo, glb, arm)
    corpo.esq = aa.Esqueleto(arm)
    corpo.nome = personagem
    cols = json.loads(bpy.context.scene['manny_colocacoes'])
    clipes = []
    for nome, mats in cols.items():
        acao = bpy.data.actions[f'{personagem}_{arma}_{nome}']
        arm.animation_data.action = acao
        if hasattr(arm.animation_data, 'action_slot') and acao.slots:
            arm.animation_data.action_slot = acao.slots[0]
        c = aa.Clipe(nome, len(mats))
        for k, m in enumerate(mats):
            bpy.context.scene.frame_set(k + 1)
            locais = {pb.name: pb.rotation_quaternion.copy() for pb in arm.pose.bones}
            M = corpo.esq.fk(locais)
            jogo, mundo = corpo.no_jogo(M)
            col = Matrix(m)
            c.ossos.append({n_: jogo[corpo.n(n_)] for n_ in aa.OSSOS if corpo.n(n_) in jogo})
            c.conjunto.append(mundo[corpo.n('Spine2')].inverted() @ (EIXOS @ col @ EIXOS.inverted()))
            c.locais.append(locais)
            c.colocacoes.append(col)
        clipes.append(c)
    caminho_json = aa.gravar(personagem, arma, clipes, escala=1.0)
    print('GRAVADO', caminho_json)


# ------------------------------------------------------------------ main

def main():
    args = sys.argv[1:]
    so_blend = '--so-blend' in args
    do_blend = '--do-blend' in args
    args = [a for a in args if not a.startswith('--')]
    personagem, arma = args[0], args[1]
    pasta = os.path.join(RAIZ, 'assets/personagens', personagem)
    destino = os.path.join(pasta, 'humano.glb')
    original = os.path.join(COPIAS, f'humano_{personagem}_antes_manny.glb')
    caminho_blend = os.path.join(AQUI, 'poses', f'{personagem}_{arma}_manny.blend')
    if do_blend:
        regravar_do_blend(caminho_blend, destino, personagem, arma)
        return
    os.makedirs(COPIAS, exist_ok=True)
    if not os.path.exists(original):
        shutil.copy(destino, original)
        print('COPIA', original)

    ponta_fp, palma_maos = medir_manny()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = aa.FPS
    corpo0 = aa.Corpo(original)
    rig0 = aa.Conjunto(arma, 1.0)
    rig0.colocar(Matrix.Identity(4))
    f = ((rig0.arm.matrix_world @ rig0.arm.data.bones['middle_01_r'].matrix_local).translation
         - (rig0.arm.matrix_world @ rig0.arm.data.bones['hand_r'].matrix_local).translation).length / palma_maos
    ponta_fp = {k: v * f for k, v in ponta_fp.items()}   # o maos.glb vem em outra unidade
    maos = [MaoManny(corpo0, rig0, lado, ponta_fp) for lado in ('Right', 'Left')]
    Qe = {mm.lado: mm.Qe.copy() for mm in maos}
    if not so_blend:
        remendar_glb(original, destino, maos)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = aa.FPS
    corpo = aa.Corpo(destino)
    rig = aa.Conjunto(arma, 1.0)
    plano = aa.Plano(corpo, rig, arma)
    malha = validar_maos.Malha(corpo)
    ger = Gerador(corpo, rig, plano, Qe, ponta_fp, malha)
    ger.achar_alivio('parado')
    clipes = []
    for nome in sorted(aa.CLIPES[plano.tipo], key=lambda x: (x != 'parado', x != 'mira')):
        if nome in rig.clipes():
            clipes.append(ger.clipe(nome))
    if not so_blend:
        print('GRAVADO', aa.gravar(personagem, arma, clipes, escala=1.0))
    montar_blend(corpo, rig, clipes, caminho_blend, personagem, arma)


if __name__ == '__main__':
    main()
