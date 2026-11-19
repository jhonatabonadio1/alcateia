#!/usr/bin/env python3
"""
As mãos de primeira pessoa em low poly (pedido do Jhon, 18/11: "retexturize
somente a mão, deixe ela mais low poly, na mesma pegada da geometria do lobo,
para combinar com a arma e o restante do mapa").

    python3 tools/armas/maos_lowpoly.py [--faces 3200] [--cores 8]

Lê as mãos originais (o manequim, 7.240 triângulos, pele fotográfica) de
tools/armas/fontes/ e grava em assets/armas/fp/maos/:

- maos.glb         a malha reduzida (uns 3.200 triângulos). Superfícies suaves
                   ficam lisas e só as quinas de verdade viram faceta (a
                   primeira versão, toda facetada com 2.000 triângulos, ficou
                   "quadriculada demais");
- maos_paleta.png  a "textura": uma tira com poucas cores chapadas. Cada face
                   usa uma cor só, tirada da média da pele (ou da luva) que
                   havia naquele pedaço da textura antiga, alisada com as
                   vizinhas para a mão não virar um xadrez de tons.

O esqueleto e os pesos são os mesmos (cada vértice novo herda os pesos do
vértice original mais próximo), então todas as animações seguem valendo.

Precisa de: numpy, scipy, scikit-learn, pillow e pyfqmr (pip install pyfqmr).
"""
import os, sys, struct
import numpy as np
from PIL import Image
from scipy.spatial import cKDTree
from sklearn.cluster import KMeans
import pyfqmr

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, '..', 'rugido'))
from glb import Glb

FONTE = os.path.join(AQUI, 'fontes', 'maos_alta.glb')
PELE = os.path.join(AQUI, 'fontes', 'maos_cor_alta.jpg')
SAIDA = os.path.join(AQUI, '..', '..', 'assets', 'armas', 'fp', 'maos')
CELULA = 8                     # lado de cada cor na tira, em pixels
ALISA = 4                      # passadas de alisar a cor entre faces vizinhas
VINCO = 50.0                   # ângulo (graus) a partir do qual a aresta vira quina


def arg(nome, padrao):
    return int(sys.argv[sys.argv.index(nome) + 1]) if nome in sys.argv else padrao


def por(g, arr, comp, tipo, alvo=None, minmax=False):
    """Um acessor novo no arquivo, com o tipo de dado certo."""
    arr = np.ascontiguousarray(arr, dtype={5121: '<u1', 5123: '<u2', 5125: '<u4', 5126: '<f4'}[comp])
    while len(g.bin) % 4: g.bin.append(0)
    off = len(g.bin); g.bin += arr.tobytes()
    bv = {'buffer': 0, 'byteOffset': off, 'byteLength': arr.nbytes}
    if alvo: bv['target'] = alvo
    g.json['bufferViews'].append(bv)
    a = {'bufferView': len(g.json['bufferViews']) - 1, 'componentType': comp, 'count': int(arr.shape[0]), 'type': tipo}
    if minmax: a['min'] = arr.reshape(len(arr), -1).min(0).tolist(); a['max'] = arr.reshape(len(arr), -1).max(0).tolist()
    g.json['accessors'].append(a); return len(g.json['accessors']) - 1


def main():
    faces_alvo, cores = arg('--faces', 3200), arg('--cores', 8)
    g = Glb(FONTE); j = g.json
    prim = j['meshes'][0]['primitives'][0]; at = prim['attributes']
    pos = g.acc(at['POSITION']).astype(float); uv = g.acc(at['TEXCOORD_0']).astype(float)
    jn = g.acc(at['JOINTS_0']).astype(int); wt = g.acc(at['WEIGHTS_0']).astype(float)
    idx = g.acc(prim['indices']).reshape(-1, 3).astype(int)

    # Solda os vértices repetidos (a costura da textura e das normais): a redução
    # precisa de uma malha fechada, senão abre buracos nas costuras.
    chave, primeiro, mapa = np.unique(np.round(pos, 4), axis=0, return_index=True, return_inverse=True)
    mapa = mapa.reshape(-1)
    V = pos[primeiro]; F = mapa[idx]
    F = F[(F[:, 0] != F[:, 1]) & (F[:, 1] != F[:, 2]) & (F[:, 0] != F[:, 2])]

    s = pyfqmr.Simplify(); s.setMesh(V, F)
    s.simplify_mesh(target_count=faces_alvo, aggressiveness=6, preserve_border=True, verbose=False)
    V2, F2, _ = s.getMesh(); V2 = np.asarray(V2, float); F2 = np.asarray(F2, int)

    # Os pesos: do vértice original mais próximo.
    perto = cKDTree(V).query(V2)[1]
    jn2 = jn[primeiro][perto]; wt2 = wt[primeiro][perto]

    # A cor de cada face nova: a média (pela área) da textura antiga nos triângulos
    # originais que caem nela.
    pele = np.asarray(Image.open(PELE).convert('RGB'), float) / 255.0
    H, W = pele.shape[:2]
    tri = pos[idx]; centro = tri.mean(1)
    area = np.linalg.norm(np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]), axis=1) / 2
    amostras = np.zeros((len(idx), 3))
    for a, b, c in ((1/3, 1/3, 1/3), (.6, .2, .2), (.2, .6, .2), (.2, .2, .6)):
        p = uv[idx[:, 0]] * a + uv[idx[:, 1]] * b + uv[idx[:, 2]] * c
        x = np.clip((p[:, 0] % 1.0) * W, 0, W - 1).astype(int); y = np.clip((p[:, 1] % 1.0) * H, 0, H - 1).astype(int)
        amostras += pele[y, x] ** 2.2 / 4                       # média em luz linear
    centro2 = V2[F2].mean(1)
    dona = cKDTree(centro2).query(centro)[1]
    soma = np.zeros((len(F2), 3)); peso = np.zeros(len(F2))
    np.add.at(soma, dona, amostras * area[:, None]); np.add.at(peso, dona, area)
    vazia = peso == 0
    if vazia.any():                                             # face sem triângulo original: copia da vizinha
        viz = cKDTree(centro2[~vazia]).query(centro2[vazia])[1]
        soma[vazia] = (soma[~vazia] / peso[~vazia, None])[viz]; peso[vazia] = 1
    cor = (soma / peso[:, None]) ** (1 / 2.2)

    # Alisa a cor entre faces vizinhas parecidas (pele com pele, luva com luva):
    # sem isto cada face caía num tom e a mão virava um xadrez ("deixe a mão menos
    # quadriculada, está exagerado" — Jhon, 18/11).
    viz = vizinhas(F2)
    for _ in range(ALISA):
        nova = cor.copy()
        for f, vs in enumerate(viz):
            perto_cor = [v for v in vs if np.abs(cor[v] - cor[f]).max() < 0.16]
            if perto_cor: nova[f] = 0.4 * cor[f] + 0.6 * cor[perto_cor].mean(0)
        cor = nova

    # Poucas cores chapadas: a paleta.
    km = KMeans(n_clusters=cores, n_init=8, random_state=3).fit(cor)
    paleta = km.cluster_centers_; qual = km.labels_
    ordem = np.argsort(paleta.sum(1)); posicao = np.argsort(ordem)   # do escuro ao claro
    paleta = paleta[ordem]; qual = posicao[qual]
    tira = np.zeros((CELULA, CELULA * cores, 3), np.uint8)
    for k in range(cores): tira[:, k * CELULA:(k + 1) * CELULA] = np.clip(paleta[k] * 255 + 0.5, 0, 255)
    Image.fromarray(tira).save(os.path.join(SAIDA, 'maos_paleta.png'))

    # Três vértices por face (uma cor só por face). A normal de cada canto é a
    # média das faces em volta daquele vértice que apontam para o mesmo lado (até
    # VINCO graus): superfícies suaves ficam lisas, e só as quinas de verdade
    # (nós dos dedos, bordas da luva) aparecem como faceta.
    P = V2[F2].reshape(-1, 3)
    n = np.cross(V2[F2[:, 1]] - V2[F2[:, 0]], V2[F2[:, 2]] - V2[F2[:, 0]])
    area2 = np.linalg.norm(n, axis=1, keepdims=True)
    n /= np.maximum(area2, 1e-12)
    em_volta = [[] for _ in range(len(V2))]
    for f, tri3 in enumerate(F2):
        for v in tri3: em_volta[v].append(f)
    limite = np.cos(np.radians(VINCO))
    N = np.zeros((len(F2), 3, 3))
    for f, tri3 in enumerate(F2):
        for k, v in enumerate(tri3):
            fs = np.array(em_volta[v]); ok = fs[n[fs] @ n[f] >= limite]
            m = (n[ok] * area2[ok]).sum(0)
            N[f, k] = m / max(np.linalg.norm(m), 1e-12)
    N = N.reshape(-1, 3)
    UV = np.repeat(np.stack([(qual + 0.5) / cores, np.full(len(F2), 0.5)], 1), 3, axis=0)
    J = jn2[F2].reshape(-1, 4); Wt = wt2[F2].reshape(-1, 4)
    Wt = Wt / np.maximum(Wt.sum(1, keepdims=True), 1e-9)
    I = np.arange(len(P))

    prim['attributes'] = {
        'POSITION': por(g, P, 5126, 'VEC3', 34962, minmax=True),
        'NORMAL': por(g, N, 5126, 'VEC3', 34962),
        'TEXCOORD_0': por(g, UV, 5126, 'VEC2', 34962),
        'JOINTS_0': por(g, J, 5121, 'VEC4', 34962),
        'WEIGHTS_0': por(g, Wt, 5126, 'VEC4', 34962),
    }
    prim['indices'] = por(g, I, 5123 if len(P) < 65536 else 5125, 'SCALAR', 34963)

    # Tira do arquivo os dados da malha antiga (o que não é mais usado).
    compactar(g)
    g.save(os.path.join(SAIDA, 'maos.glb'))
    print(f"maos.glb: {len(idx)} -> {len(F2)} triângulos, {cores} cores; {os.path.getsize(os.path.join(SAIDA, 'maos.glb')) // 1024} KB")
    print("paleta:", ' '.join('#%02x%02x%02x' % tuple(int(v) for v in np.clip(c * 255 + 0.5, 0, 255)) for c in paleta))


def vizinhas(F):
    """Para cada face, as faces que dividem uma aresta com ela."""
    dono = {}
    for f, (a, b, c) in enumerate(F):
        for e in ((a, b), (b, c), (c, a)): dono.setdefault((min(e), max(e)), []).append(f)
    viz = [[] for _ in range(len(F))]
    for fs in dono.values():
        for x in fs:
            for y in fs:
                if x != y: viz[x].append(y)
    return viz


def compactar(g):
    """Regrava o binário só com os acessores em uso (malha, pele e animações)."""
    j = g.json; usados = set()
    for m in j['meshes']:
        for p in m['primitives']:
            usados.update(p['attributes'].values())
            if 'indices' in p: usados.add(p['indices'])
    for sk in j.get('skins', []):
        if 'inverseBindMatrices' in sk: usados.add(sk['inverseBindMatrices'])
    for an in j.get('animations', []):
        for sm in an['samplers']: usados.update((sm['input'], sm['output']))
    novo = bytearray(); vistas = []; troca = {}
    comp = {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}; nc = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}
    acessores = []
    for i, a in enumerate(j['accessors']):
        if i not in usados: continue
        bv = j['bufferViews'][a['bufferView']]
        off = bv.get('byteOffset', 0) + a.get('byteOffset', 0); tam = a['count'] * nc[a['type']] * comp[a['componentType']]
        while len(novo) % 4: novo.append(0)
        nb = {'buffer': 0, 'byteOffset': len(novo), 'byteLength': tam}
        if 'target' in bv: nb['target'] = bv['target']
        novo += g.bin[off:off + tam]; vistas.append(nb)
        a = dict(a); a['bufferView'] = len(vistas) - 1; a.pop('byteOffset', None)
        troca[i] = len(acessores); acessores.append(a)
    for m in j['meshes']:
        for p in m['primitives']:
            p['attributes'] = {k: troca[v] for k, v in p['attributes'].items()}
            if 'indices' in p: p['indices'] = troca[p['indices']]
    for sk in j.get('skins', []):
        if 'inverseBindMatrices' in sk: sk['inverseBindMatrices'] = troca[sk['inverseBindMatrices']]
    for an in j.get('animations', []):
        for sm in an['samplers']: sm['input'] = troca[sm['input']]; sm['output'] = troca[sm['output']]
    j['accessors'] = acessores; j['bufferViews'] = vistas; g.bin = novo


if __name__ == '__main__':
    main()
