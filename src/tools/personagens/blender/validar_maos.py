#!/usr/bin/env python3
"""
Validador das animações de terceira pessoa com arma, à parte da conta do
animacoes_armas.py: resolve cada clipe como o script resolve (parado e mira
primeiro), aplica a pose no Blender, pega a MALHA DEFORMADA por ele e mede.

    python3 validar.py <personagem> <arma> [--saida pasta] [--gravar] [--passo 3] [--clipes a,b]

- dedos: para cada vértice cujo maior peso é uma falange, o raio do eixo do
  osso (o pé limitado a 10%-90% do osso) até o vértice; Conjunto.corta diz
  quanto da pele fica do lado de lá da arma (mm).
- palma: sonda ao longo de -normal de cada vértice da mão.
- o maior salto de junta de dedo entre dois quadros (todos os quadros) e a
  emenda dos laços.
- o leque: o maior vão entre dois dedos vizinhos (indicador..mínimo), na junta
  da ponta e na ponta, descontada a grossura — aqui e na mão de lá.
Não usa o sinal da distância (erra em peça oca: o guarda-mão).

Com --gravar, grava também o .json do jogo (o mesmo que o animacoes_armas.py
grava: a mesma ordem de clipes). Guarda as poses resolvidas em
<saida>/<personagem>_<arma>.pkl (para as fotos) e as medidas em .json.
"""
import json
import math
import os
import pickle
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import animacoes_armas as aa  # noqa: E402
import poses_armas as pa  # noqa: E402

DEDOS = pa.DEDOS
SEGURANDO = ('parado', 'andar', 'tiro', 'mira', 'mira_andar', 'mira_tiro', 'armado')


class Malha:
    """Os vértices da mão de cada lado, classificados uma vez."""

    def __init__(self, corpo):
        self.corpo = corpo
        for lado in ('Right', 'Left'):
            aa.pele_palma(corpo, lado)  # enche corpo._sonda_vert
        self.itens = []
        for o in corpo.malhas:
            grupos = {g.index: g.name for g in o.vertex_groups}
            dedos, palma = [], []
            for v in o.data.vertices:
                if not v.groups:
                    continue
                g = max(v.groups, key=lambda g: g.weight)
                nome = grupos.get(g.group, '')
                for lado in ('Right', 'Left'):
                    if nome == corpo.n(lado + 'Hand'):
                        sonda = corpo._sonda_vert.get((o.name, v.index))
                        if sonda is None:
                            sonda = max(0.004, corpo.mao[lado]['espessura'] * 0.36)
                        palma.append((v.index, lado, sonda))
                    else:
                        for d in DEDOS:
                            for k in (1, 2, 3, 4):
                                if nome == corpo.n(f'{lado}Hand{d}{k}'):
                                    dedos.append((v.index, lado, d, min(k, 3)))
            if dedos or palma:
                self.itens.append((o, dedos, palma))

    def medir(self, rig):
        """A malha como o Blender a deformou agora: o quanto a arma entra
        na pele de cada dedo e da palma (m), por lado."""
        corpo = self.corpo
        arm = corpo.arm
        pb = arm.pose.bones
        mw = arm.matrix_world
        cab = {}
        for lado in ('Right', 'Left'):
            for d in DEDOS:
                for k in (1, 2, 3, 4):
                    cab[(lado, d, k)] = mw @ pb[corpo.n(f'{lado}Hand{d}{k}')].head
        dg = bpy.context.evaluated_depsgraph_get()
        dedo = {}
        palma = {'Right': 0.0, 'Left': 0.0}
        for o, dedos, pal in self.itens:
            e = o.evaluated_get(dg)
            m = e.to_mesh()
            mo = o.matrix_world
            r3 = mo.to_3x3()
            vs = m.vertices
            for i, lado, d, k in dedos:
                w = mo @ vs[i].co
                a, b = cab[(lado, d, k)], cab[(lado, d, k + 1)]
                eixo = b - a
                L = eixo.length
                eixo = eixo / L
                t = max(0.1 * L, min(0.9 * L, (w - a).dot(eixo)))
                fura = rig.corta(a + eixo * t, w)
                if fura is not None and fura > dedo.get((lado, d), 0.0):
                    dedo[(lado, d)] = fura
            for i, lado, sonda in pal:
                w = mo @ vs[i].co
                n = (r3 @ vs[i].normal).normalized()
                fura = rig.corta(w - n * sonda, w)
                if fura is not None and fura > palma[lado]:
                    palma[lado] = fura
            e.to_mesh_clear()
        # o leque: o vão entre vizinhos na junta da ponta e na ponta
        leque = {}
        for lado in ('Right', 'Left'):
            pior = 0.0
            for d1, d2 in (('Index', 'Middle'), ('Middle', 'Ring'), ('Ring', 'Pinky')):
                r = corpo.raio_dedo[lado + d1] + corpo.raio_dedo[lado + d2]
                for k in (3, 4):
                    pior = max(pior, (cab[(lado, d1, k)] - cab[(lado, d2, k)]).length - r)
            leque[lado] = pior
        return dedo, palma, leque


def leque_fp(rig):
    out = {}
    for lado in ('Right', 'Left'):
        fp = rig.mao(lado)
        js = fp['juntas']
        pior = 0.0
        for i in (1, 2, 3):
            for k in (2, 3):
                pior = max(pior, (js[i][k] - js[i + 1][k]).length)
        out[lado] = pior
    return out


def saltos(corpo, c, laco):
    """O maior giro de uma junta de dedo de um quadro para o outro (graus)
    e, em laço, entre o último e o primeiro."""
    nomes = [corpo.n(x) for x in pa.FALANGES]
    pior = (0.0, None, None)
    for k in range(1, len(c.locais)):
        for n in nomes:
            a, b = c.locais[k - 1].get(n), c.locais[k].get(n)
            if a is None or b is None:
                continue
            ang = math.degrees(a.rotation_difference(b).angle)
            ang = min(ang, 360.0 - ang)
            if ang > pior[0]:
                pior = (ang, k, n.replace(pa.PREFIXO, ''))
    emenda = 0.0
    if laco:
        for n in nomes:
            a, b = c.locais[0].get(n), c.locais[-1].get(n)
            if a is None or b is None:
                continue
            ang = math.degrees(a.rotation_difference(b).angle)
            emenda = max(emenda, min(ang, 360.0 - ang))
    return pior, emenda


def main():
    args = sys.argv[1:]

    def opcao(nome, padrao=None):
        if nome in args:
            v = args[args.index(nome) + 1]
            del args[args.index(nome):args.index(nome) + 2]
            return v
        return padrao

    saida = opcao('--saida', '.')
    passo = int(opcao('--passo', 3))
    so = opcao('--clipes')
    so = so.split(',') if so else None
    gravar = '--gravar' in args
    args = [a for a in args if a != '--gravar']
    personagem, arma = args[0], args[1]
    os.makedirs(saida, exist_ok=True)

    inicio = time.time()
    glb = os.path.join(pa.RAIZ, 'assets/personagens', personagem, 'humano.glb')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = aa.FPS
    corpo = aa.Corpo(glb)
    rig = aa.Conjunto(arma, aa.escala_arma(corpo, aa.ARMAS[arma]))
    plano = aa.Plano(corpo, rig, arma)
    malha = Malha(corpo)
    relato = {'personagem': personagem, 'arma': arma, 'escala': rig.escala, 'plano': plano.relato, 'clipes': {}}
    poses = {'escala': rig.escala, 'clipes': {}}
    clipes = []
    for clipe in sorted(aa.CLIPES[plano.tipo], key=lambda x: (x != 'parado', x != 'mira')):
        if clipe not in rig.clipes():
            continue
        if so and clipe not in so and clipe not in ('parado', 'mira'):
            continue
        c = aa.resolver_clipe(corpo, rig, plano, clipe)
        clipes.append(c)
        pior_s, onde_s = aa.resumo(c)
        amostras = []
        for k in list(range(0, c.quadros, passo)):
            rig.tocar(clipe, k)
            rig.colocar(c.colocacoes[k])
            corpo.aplicar(c.locais[k])
            dedo, palma, leque = malha.medir(rig)
            lf = leque_fp(rig)
            M = corpo.esq.fk(c.locais[k])
            coronha = None
            if plano.tipo == 'fuzil':
                coronha = (rig.coronha() - corpo.bolso(M)).length
            amostras.append(dict(q=k, dedo={f'{l[0]}{d}': round(v * 1000, 2) for (l, d), v in dedo.items()},
                                 palma={l[0]: round(v * 1000, 2) for l, v in palma.items()},
                                 leque={l[0]: round(v * 1000, 1) for l, v in leque.items()},
                                 leque_fp={l[0]: round(v * 1000, 1) for l, v in lf.items()},
                                 palma_longe={'R': round(c.medidas[k]['direita'].get('palma', 0) * 1000, 1),
                                              'L': round(c.medidas[k]['esquerda'].get('palma', 0) * 1000, 1)},
                                 coronha=None if coronha is None else round(coronha * 100, 1)))
        salto, emenda = saltos(corpo, c, clipe in aa.LACOS)
        d_max = max((max(a['dedo'].values(), default=0.0) for a in amostras), default=0.0)
        p_max = max((max(a['palma'].values()) for a in amostras), default=0.0)
        qual = ''
        for a in amostras:
            for nome, v in a['dedo'].items():
                if v == d_max and v > 0:
                    qual = f'{nome} q{a["q"]}'
        relato['clipes'][clipe] = dict(
            quadros=c.quadros, dedo=d_max, dedo_onde=qual, palma=p_max,
            dedo_acima3=sum(1 for a in amostras if max(a['dedo'].values(), default=0.0) > 3.0),
            palma_acima3=sum(1 for a in amostras if max(a['palma'].values()) > 3.0),
            n=len(amostras), salto=round(salto[0], 1), salto_onde=f'{salto[2]} q{salto[1]}', emenda=round(emenda, 2),
            leque_e=max(a['leque']['L'] for a in amostras), leque_d=max(a['leque']['R'] for a in amostras),
            leque_fp_e=max(a['leque_fp']['L'] for a in amostras),
            script=dict(dedo_dentro=round(pior_s['dedo_dentro'] * 1000, 2), palma_longe=round(pior_s['palma'] * 1000, 1),
                        palma_dentro=round(pior_s['palma_dentro'] * 1000, 2), mao=round(pior_s['mao'] * 100, 2),
                        punho=round(pior_s['punho'], 1)),
            amostras=amostras)
        r = relato['clipes'][clipe]
        print(f'VALIDA {personagem} {arma} {clipe:14s} dedo {r["dedo"]:5.1f} mm ({r["dedo_onde"]}) palma {r["palma"]:5.1f} mm '
              f'salto {r["salto"]:5.1f}° ({r["salto_onde"]}) emenda {r["emenda"]:.2f}° leque E {r["leque_e"]:.0f} mm', flush=True)
        poses['clipes'][clipe] = dict(
            locais=[{n: tuple(q) for n, q in l.items()} for l in c.locais],
            colocacoes=[[list(row) for row in m] for m in c.colocacoes])
    if gravar and not so:
        caminho = aa.gravar(personagem, arma, clipes, escala=rig.escala)
        print('GRAVADO', caminho)
    relato['segundos'] = round(time.time() - inicio)
    sufixo = '' if not so else '_parcial'
    with open(os.path.join(saida, f'{personagem}_{arma}{sufixo}.json'), 'w') as f:
        json.dump(relato, f, indent=1, ensure_ascii=False)
    with open(os.path.join(saida, f'{personagem}_{arma}{sufixo}.pkl'), 'wb') as f:
        pickle.dump(poses, f)
    print('FIM', personagem, arma, relato['segundos'], 's')


if __name__ == '__main__':
    main()
