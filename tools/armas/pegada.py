#!/usr/bin/env python3
"""
A mão fechando em volta de um objeto, como mão de gente: usada pela granada (``conjuntos.choque``).

A pose de punho do AR-15 serve para cabo de arma (3,5 cm). A granada é uma bola de 6 cm com a
alavanca de um lado: fechada como num cabo, a mão atravessava a bola e engolia a alavanca (Jhon,
19/11: "o gatilho dela tá entrando dentro da mão"). Aqui a pegada é calculada:

1. o objeto é dado como uma soma de sólidos convexos (a bola com a espoleta, a alavanca);
2. ele encosta na palma: anda numa reta até a primeira pele da palma tocar nele;
3. cada dedo fecha junta por junta, da base para a ponta, até a pele dele encostar — é como uma
   mão se fecha em volta de uma coisa: a primeira falange para quando toca, as outras continuam.

A pele é a malha das mãos padrão do jogo (``assets/armas/fp/maos/maos.glb``), levada pelos ossos
do conjunto: o que se mede aqui é o que aparece na tela.
"""
import os, sys
import numpy as np
from scipy.spatial import ConvexHull
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from esqueleto import *
from glb import Glb

MAOS = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), 'assets', 'armas', 'fp', 'maos', 'maos.glb')
FALANGES = ('thumb', 'index', 'middle', 'ring', 'pinky')


class Pele:
    """A malha das mãos padrão e os pesos dela."""
    def __init__(self, caminho=MAOS):
        g = Glb(caminho); j = g.json
        no = [n for n in j['nodes'] if 'mesh' in n and 'skin' in n][0]; pele = j['skins'][no['skin']]
        self.nomes = [j['nodes'][k]['name'] for k in pele['joints']]
        self.ibm = g.acc(pele['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1).astype(float)
        V, J, W, T = [], [], [], []; n0 = 0
        for p in j['meshes'][no['mesh']]['primitives']:
            a = p['attributes']; V.append(g.acc(a['POSITION']).astype(float)); J.append(g.acc(a['JOINTS_0']).astype(int)); w = g.acc(a['WEIGHTS_0']).astype(float)
            W.append(w / {5121: 255.0, 5123: 65535.0}.get(j['accessors'][a['WEIGHTS_0']]['componentType'], 1.0))
            T.append((g.acc(p['indices']).astype(int).reshape(-1, 3) if 'indices' in p else np.arange(len(V[-1])).reshape(-1, 3)) + n0); n0 += len(V[-1])
        self.V, self.J, self.W, self.tri = np.vstack(V), np.vstack(J), np.vstack(W), np.vstack(T); self.W /= self.W.sum(1, keepdims=True)
        self.dono = np.array([self.nomes[k] for k in self.J[np.arange(len(self.V)), self.W.argmax(1)]])       # o osso que mais pesa em cada vértice

    def em(self, E, pose, para, quais=None):
        """Os vértices (todos ou a máscara ``quais``) com o esqueleto na pose dada, levados por ``para`` (4×4 a partir da raiz)."""
        V, J, W = (self.V, self.J, self.W) if quais is None else (self.V[quais], self.J[quais], self.W[quais])
        M = np.zeros((len(self.nomes), 4, 4))
        for k in np.unique(J[W > 0]):
            n = self.nomes[k]; M[k] = para @ E.mundo(pose, E.N(n)) @ self.ibm[k] if n in E.c.names else np.eye(4)
        S = np.zeros((len(V), 4, 4))
        for q in range(4): S += W[:, q, None, None] * M[J[:, q]]
        return np.einsum('nij,nj->ni', S, np.c_[V, np.ones(len(V))])[:, :3]


    def triangulos(self, P, quais):
        """Os triângulos (n×3×3) cujos três vértices estão na máscara ``quais``, com os vértices P (de ``em``, todos)."""
        return P[self.tri[quais[self.tri].all(1)]]


def amostrar(tri, passo=0.004):
    """Pontos sobre os triângulos (n×3×3), a cada ``passo`` m."""
    tri = np.asarray(tri, float).reshape(-1, 3, 3); out = [tri.reshape(-1, 3)]
    for t in tri:
        n = int(max(np.linalg.norm(t[1] - t[0]), np.linalg.norm(t[2] - t[0]), np.linalg.norm(t[2] - t[1])) / passo)
        if n > 1:
            u, v = np.meshgrid(np.arange(n + 1) / n, np.arange(n + 1) / n); k = (u + v) <= 1; u, v = u[k], v[k]
            out.append(t[0] + u[:, None] * (t[1] - t[0]) + v[:, None] * (t[2] - t[0]))
    return np.vstack(out)


def fora_da_malha(pontos, tri):
    """A que distância cada ponto fica de uma malha de triângulos (n×3×3), com sinal: negativo = do lado de dentro."""
    a, b, c = tri[:, 0], tri[:, 1], tri[:, 2]; ab, ac = b - a, c - a; n = np.cross(ab, ac); n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-12
    out = np.empty(len(pontos))
    def div(x, y): return x / np.where(np.abs(y) < 1e-18, 1e-18, y)
    for s in range(0, len(pontos), 256):
        p = pontos[s:s + 256]; x = p[:, None, :]; ap = x - a; bp = x - b; cp = x - c
        d1 = (ab * ap).sum(-1); d2 = (ac * ap).sum(-1); d3 = (ab * bp).sum(-1); d4 = (ac * bp).sum(-1); d5 = (ab * cp).sum(-1); d6 = (ac * cp).sum(-1)
        va = d3 * d6 - d5 * d4; vb = d5 * d2 - d1 * d6; vc = d1 * d4 - d3 * d2; den = va + vb + vc
        q = a + ab * div(vb, den)[..., None] + ac * div(vc, den)[..., None]
        for cond, q0, e, t in (((vc <= 0) & (d1 >= 0) & (d3 <= 0), a, ab, div(d1, d1 - d3)), ((vb <= 0) & (d2 >= 0) & (d6 <= 0), a, ac, div(d2, d2 - d6)),
                               ((va <= 0) & ((d4 - d3) >= 0) & ((d5 - d6) >= 0), b, c - b, div(d4 - d3, (d4 - d3) + (d5 - d6)))):
            q = np.where(cond[..., None], q0 + e * np.clip(t, 0, 1)[..., None], q)
        q = np.where(((d1 <= 0) & (d2 <= 0))[..., None], a, q); q = np.where(((d3 >= 0) & (d4 <= d3))[..., None], b, q); q = np.where(((d6 >= 0) & (d5 <= d6))[..., None], c, q)
        dd = np.linalg.norm(x - q, axis=-1); k = dd.argmin(1); r = np.arange(len(k)); sinal = np.sign(((p - q[r, k]) * n[k]).sum(-1)); sinal[sinal == 0] = 1
        out[s:s + 256] = dd[r, k] * sinal
    return out


class Solido:
    """Uma soma de sólidos convexos (cada um dado pelos pontos dele)."""
    def __init__(self, *partes): self.eq = [ConvexHull(np.asarray(p, float).reshape(-1, 3), qhull_options='QJ').equations for p in partes]

    @staticmethod
    def de_malha(tri, de, ate, fatia=0.025, passo=0.004, eixo=2, acima=None):
        """Uma arma (triângulos n×3×3, m) entre ``de`` e ``ate`` no eixo dado, como uma soma de fatias convexas: cada fatia
        é a casca convexa do que a malha tem ali. Serve para guarda-mão e punho, que são quase convexos em corte; os vãos
        (o do guarda-mato, o que fica entre o guarda-mão e o tubo de gás) ficam tapados, e os dedos pousam por fora deles."""
        pts = amostrar(tri, passo); partes = []
        if acima is not None: pts = pts[pts[:, 1] >= acima]              # sem o que desce dali para baixo (o carregador, o punho)
        for a in np.arange(de, ate, fatia):
            q = pts[(pts[:, eixo] >= a - 0.002) & (pts[:, eixo] <= a + fatia + 0.002)]
            if len(q) >= 8 and np.ptp(q, axis=0).min() > 1e-4: partes.append(q)
        return Solido(*partes)
    def mais(self, outro):
        """Este sólido somado a outro."""
        s = Solido.__new__(Solido); s.eq = self.eq + outro.eq; return s

    def fora(self, P):
        """Quanto cada ponto está fora do sólido (m; negativo = dentro)."""
        return np.min([(P @ e[:, :3].T + e[:, 3]).max(1) for e in self.eq], axis=0)


def pegar(E, base, lado, solido, onde, aberta, fechada, reta=(0.8, 0.0, 0.6), folga=0.0006, pele=None, faixa=(-0.45, 1.2), junto=False):
    """
    Fecha a mão ``lado`` em volta de ``solido``, que está em ``onde`` (4×4: do espaço do sólido para o da arma, m).

    ``aberta`` e ``fechada`` são as duas poses de dedos entre as quais cada junta gira (pode passar um pouco das duas:
    ``faixa``). ``reta``: a direção (espaço da arma) em que o objeto anda até encostar na palma.
    ``junto``: as três juntas de cada dedo dobram juntas, a partir de ``aberta``, até o dedo encostar — para quando
    ``aberta`` já é uma pegada boa (a mão do AR-15 no guarda-mão) e só falta ajustá-la ao tamanho desta arma.
    Devolve (onde — já encostado na palma —, dedos, medidas).
    """
    pele = pele or Pele(); pose = dict(base); E.por_dedos(pose, aberta)
    A = np.linalg.inv(E.arma_de(pose) @ ARMA)                              # raiz -> arma (m)
    da_mao = np.array([n.endswith('_' + lado) and n.startswith(('hand',) + FALANGES) for n in pele.dono])
    palma = da_mao & np.array([n.startswith(('hand', 'thumb_01')) for n in pele.dono])       # a palma e a base do polegar
    def dist(mascara, p, M): return solido.fora(np.c_[pele.em(E, p, A, mascara), np.ones(int(mascara.sum()))] @ np.linalg.inv(M).T[:, :3])

    # 2. encosta na palma: parte de trás na reta e avança até a pele da palma tocar
    d = np.asarray(reta, float); d /= np.linalg.norm(d); a, b = -0.12, 0.03
    livre = lambda s: dist(palma, pose, mover(*(d * s)) @ onde).min() > folga
    for _ in range(26):
        m = (a + b) / 2
        if livre(m): a = m
        else: b = m
    onde = mover(*(d * a)) @ onde

    # 3. cada dedo: de todas as dobras das três juntas que não atravessam o objeto, a que mais encosta as três falanges
    dedos = {i: (T, np.array(Q)) for i, (T, Q) in aberta.items()}; pesos = {}
    for dedo in FALANGES:
        p = dict(pose); juntas = [E.N(f'{dedo}_{k}_{lado}') for k in ('01', '02', '03')]
        segs = [da_mao & (pele.dono == f'{dedo}_{k}_{lado}') for k in ('01', '02', '03')]; todos = np.any(segs, axis=0); fatias = [s[todos] for s in segs]
        def custo(w):
            for i, wk in zip(juntas, w): p[i] = (aberta[i][0], slerp(aberta[i][1], fechada[i][1], wk))
            f = dist(todos, p, onde)
            if f.min() < folga: return None
            toque = [f[s].min() for s in fatias]                           # a que distância cada falange fica
            return 1e6 * (toque[0] ** 2 * (0.3 if dedo == 'thumb' else 1.0) + toque[1] ** 2 + 1.5 * toque[2] ** 2) + 0.02 * ((w[1] - w[2]) ** 2 + 0.3 * (w[0] - w[1]) ** 2)
        if junto and custo((0.0, 0.0, 0.0)) is not None:                  # solto, não toca: dobra inteiro até encostar
            w = 0.0
            while w + 0.02 <= faixa[1] and custo((w + 0.02,) * 3) is not None: w += 0.02
            custo((w, w, w)); pesos[dedo] = [round(float(w), 2)] * 3
            for i in juntas: dedos[i] = p[i]
            continue
        # (ou: solto já atravessa — então procura, junta por junta, a dobra que encosta sem atravessar)
        melhor, wm = None, None
        def varrer(grades):
            nonlocal melhor, wm
            for w0 in grades[0]:
                for w1 in grades[1]:
                    for w2 in grades[2]:
                        c = custo((w0, w1, w2))
                        if c is not None and (melhor is None or c < melhor): melhor, wm = c, (w0, w1, w2)
        g = np.arange(faixa[0], faixa[1] + 1e-9, 0.15); varrer((g, g, g))
        if wm is not None:
            for passo in (0.05, 0.0167): varrer([np.clip(np.array([x - 2 * passo, x - passo, x, x + passo, x + 2 * passo]), *faixa) for x in wm])
        else: wm = (faixa[0],) * 3
        custo(wm); pesos[dedo] = [round(float(x), 2) for x in wm]
        for i in juntas: dedos[i] = p[i]
    E.por_dedos(pose, dedos)
    medidas = {dedo: round(float(dist(da_mao & np.array([n.startswith(dedo) for n in pele.dono]), pose, onde).min()) * 1000, 1) for dedo in FALANGES}
    medidas['palma'] = round(float(dist(palma, pose, onde).min()) * 1000, 1)
    return onde, dedos, dict(mm=medidas, juntas=pesos)


def distancias(E, pose, lado, solido, pele=None):
    """A que distância do sólido (dado no espaço da arma) fica cada parte da mão ``lado``: a palma e cada falange de
    cada dedo (m; negativo = a pele entra nele). É o retrato de uma pegada, para repeti-la noutra arma (``pegar_como``)."""
    pele = pele or Pele(); A = np.linalg.inv(E.arma_de(pose) @ ARMA); out = {}
    def d(mascara): return float(solido.fora(pele.em(E, pose, A, mascara)).min())
    out['palma'] = d(np.array([n.startswith(('hand', 'thumb_01')) and n.endswith('_' + lado) for n in pele.dono]))
    for dedo in FALANGES:
        for k in ('01', '02', '03'): out[(dedo, k)] = d(pele.dono == f'{dedo}_{k}_{lado}')
    return out


def pegar_como(E, base, lado, solido, ref, solta, fechada, reta, pele=None, faixa=(-0.5, 1.0), fundo=0.06):
    """
    A mão ``lado`` na arma do jeito que ``ref`` descreve (as ``distancias`` de uma pegada aprovada noutra arma): a arma
    anda na ``reta`` até a palma ficar tão encostada quanto lá, e cada dedo dobra — as três juntas, em volta da pose
    ``solta`` (peso 0) rumo a ``fechada`` (peso 1) — até cada falange ficar à distância que tinha lá. Entre as dobras que
    dão o mesmo encosto, fica a mais parecida com a pose ``solta``.
    Devolve (quanto a arma andou na reta — vetor, m —, os dedos, as distâncias que ficaram).
    """
    pele = pele or Pele(); pose = dict(base); E.por_dedos(pose, solta); A = np.linalg.inv(E.arma_de(pose) @ ARMA)
    palma = np.array([n.startswith(('hand', 'thumb_01')) and n.endswith('_' + lado) for n in pele.dono])
    d = np.asarray(reta, float); d /= np.linalg.norm(d)
    def dist(mascara, p, s): return solido.fora(pele.em(E, p, A, mascara) - d * s)       # a arma andou s na reta = a mão andou -s
    a, b = -0.10, fundo                                                   # a: longe da palma; b: bem dentro dela
    for _ in range(26):
        m = (a + b) / 2
        if dist(palma, pose, m).min() > ref['palma']: a = m
        else: b = m
    s = a; dedos = {i: (T, np.array(Q)) for i, (T, Q) in solta.items()}
    for dedo in FALANGES:
        p = dict(pose); juntas = [E.N(f'{dedo}_{k}_{lado}') for k in ('01', '02', '03')]
        segs = [pele.dono == f'{dedo}_{k}_{lado}' for k in ('01', '02', '03')]; todos = np.any(segs, axis=0); fatias = [x[todos] for x in segs]
        alvo = [ref[(dedo, k)] for k in ('01', '02', '03')]
        def custo(w):
            for i, wk in zip(juntas, w): p[i] = (solta[i][0], slerp(solta[i][1], fechada[i][1], wk))
            f = dist(todos, p, s); e = [f[x].min() - t for x, t in zip(fatias, alvo)]
            return 1e6 * (0.5 * e[0] ** 2 + e[1] ** 2 + 2.0 * e[2] ** 2) + 3.0 * (w[0] ** 2 + w[1] ** 2 + w[2] ** 2) + 2.0 * ((w[0] - w[1]) ** 2 + (w[1] - w[2]) ** 2)
        g = np.arange(faixa[0], faixa[1] + 1e-9, 0.125); melhor = min(((custo((x, y, z)), (x, y, z)) for x in g for y in g for z in g), key=lambda t: t[0])
        for passo in (0.04, 0.013):
            wm = melhor[1]; viz = [np.clip(np.array([x - 2 * passo, x - passo, x, x + passo, x + 2 * passo]), *faixa) for x in wm]
            melhor = min([melhor] + [(custo((x, y, z)), (x, y, z)) for x in viz[0] for y in viz[1] for z in viz[2]], key=lambda t: t[0])
        custo(melhor[1])
        for i in juntas: dedos[i] = p[i]
    E.por_dedos(pose, dedos)
    ficou = {k: float(v) for k, v in ((k, dist(palma if k == 'palma' else pele.dono == f'{k[0]}_{k[1]}_{lado}', pose, s).min()) for k in ref)}
    return d * s, dedos, ficou
