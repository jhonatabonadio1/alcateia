"""
As versões épicas e lendárias das armas novas (Jhon, 19/11: "crie as armas lendárias e épicas (com variação de
texturas)"; uma épica e uma lendária de cada: M4A4, AK-74, SCAR-H, SVD, Desert Eagle e faca).

    python3 tools/armas/peles.py                 # todas
    python3 tools/armas/peles.py m4a4_neon       # só esta
    python3 tools/armas/peles.py --tamanho 512   # teste rápido (o padrão de cada pele está em PELES)

Precisa de numpy e Pillow. Não usa o Blender.

Por que não dá para só trocar a textura: as armas novas são pintadas por **paleta** (``reais.py``): cada face aponta para
um quadrado de uma cor, e desenho nenhum aparece nela. Aqui cada arma ganha um **UV de verdade** (um atlas):

1. as faces viram cartas: faces vizinhas da mesma peça viradas para o mesmo lado (±x, ±y, ±z) ficam juntas e são
   projetadas no plano delas (em metros, sem distorção);
2. as cartas são empacotadas num quadrado (``TAMANHO`` px, com margem), na maior escala que cabe;
3. cada texel sabe de que triângulo e de que ponto dele veio: a posição 3D na arma, a normal, a cor que a paleta dava
   (o que era preto, metal ou claro) e a distância até a quina viva mais perto;
4. o desenho é **calculado em 3D** (no espaço da arma), então passa de uma carta para outra sem emenda.

Grava, por pele (``<id>`` = ``<arma>_<tema>``):

- ``assets/armas/<id>/T_<Id>_D.png`` (cor), ``_E.png`` (o que acende), ``_ORM.png`` (oclusão, rugosidade, metal);
- ``assets/armas/<id>/<peça>_<id>.obj``: a mesma malha da arma de origem, com o UV novo (a arma no mundo:
  ``WeaponModel``, pela entrada ``<id>`` de ``armas.json``, também gravada aqui);
- ``assets/armas/fp/<id>.glb``: o conjunto da primeira pessoa da arma de origem (esqueleto, mãos, animações) com o UV e
  o material novos; a entrada ``<id>`` de ``fp/bracos.json`` é a da arma de origem com outro arquivo.

O conjunto de origem tem de estar feito (``conjuntos.py``). Os desenhos estão em ``ESTILOS``; cada um recebe os texels
(``Texels``) e devolve cor, emissão, rugosidade e metal.
"""
import json
import os
import struct
import sys
import zlib

import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import reais  # noqa: E402

RAIZ = os.path.dirname(os.path.dirname(AQUI))
ARMAS = os.path.join(RAIZ, 'assets', 'armas')
FP = os.path.join(ARMAS, 'fp')

# As peles: a arma de origem, o desenho, o nome e o tamanho do atlas (px).
PELES = dict(
    m4a4_neon=dict(base='m4a4', estilo='circuito', nome='M4A4 Neon', tamanho=2048),
    m4a4_dragao=dict(base='m4a4', estilo='dragao', nome='M4A4 Dragão', tamanho=2048),
    ak74_tigre=dict(base='ak74', estilo='tigre', nome='AK-74 Tigre', tamanho=2048),
    ak74_imperial=dict(base='ak74', estilo='imperial', nome='AK-74 Imperial', tamanho=2048),
    scar_artico=dict(base='scar', estilo='artico', nome='SCAR-H Ártico', tamanho=2048),
    scar_galaxia=dict(base='scar', estilo='galaxia', nome='SCAR-H Galáxia', tamanho=2048),
    svd_carbono=dict(base='svd', estilo='carbono', nome='SVD Carbono', tamanho=2048),
    svd_tempestade=dict(base='svd', estilo='tempestade', nome='SVD Tempestade', tamanho=2048),
    deagle_cobra=dict(base='deagle', estilo='cobra', nome='Desert Eagle Cobra', tamanho=1024),
    deagle_fenix=dict(base='deagle', estilo='fenix', nome='Desert Eagle Fênix', tamanho=1024),
    faca_damasco=dict(base='faca', estilo='damasco', nome='Faca Damasco', tamanho=1024),
    faca_obsidiana=dict(base='faca', estilo='obsidiana', nome='Faca Obsidiana', tamanho=1024),
)
MARGEM = 2            # px em volta de cada carta (o filtro da textura não puxa a vizinha)
METAL = 0.5
QUINA = np.cos(np.radians(35))      # faces vizinhas com mais que isso de dobra fazem uma quina viva


# ---------------------------------------------------------------- cores
def lin(hexa):
    """'#rrggbb' (sRGB) -> RGB linear."""
    c = np.array([int(hexa[i:i + 2], 16) for i in (1, 3, 5)], float) / 255
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def mix(a, b, t):
    t = np.asarray(t, float)[..., None] if np.ndim(t) else t
    return a * (1 - t) + b * t


def suave(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------- ruído (numpy, por lote)
def _h(*ints, s=0):
    """Hash de inteiros -> [0, 1)."""
    h = np.uint64((0x9E3779B97F4A7C15 + s * 0x632BE59BD9B4E019) & 0xFFFFFFFFFFFFFFFF)
    with np.errstate(over='ignore'):
        for k, v in enumerate(ints):
            h = h ^ (np.asarray(v).astype(np.int64).astype(np.uint64) * np.uint64((0x85EBCA77C2B2AE63 + k * 0x27D4EB2F165667C5) & 0xFFFFFFFFFFFFFFFF))
            h = (h ^ (h >> np.uint64(29))) * np.uint64(0xBF58476D1CE4E5B9)
        h = h ^ (h >> np.uint64(32))
    return (h & np.uint64(0xFFFFFF)).astype(np.float64) / 16777216.0


def valor(p, s=0):
    """Ruído de valor 3D (p: n×3), em [0, 1]."""
    i = np.floor(p).astype(np.int64); f = p - i; u = f * f * (3 - 2 * f); out = 0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (u[:, 0] if dx else 1 - u[:, 0]) * (u[:, 1] if dy else 1 - u[:, 1]) * (u[:, 2] if dz else 1 - u[:, 2])
                out = out + w * _h(i[:, 0] + dx, i[:, 1] + dy, i[:, 2] + dz, s=s)
    return out


def fbm(p, oitavas=4, s=0):
    """Soma de oitavas de ``valor``, em [0, 1]."""
    tot, amp, norma = 0, 0.5, 0
    for o in range(oitavas):
        tot = tot + amp * valor(p * (2.0 ** o), s=s + o); norma += amp; amp *= 0.5
    return tot / norma


def worley(p, s=0, lote=300_000):
    """Células de Voronoi 3D: (F1, F2, id da célula mais perto, ponto dela). p em unidades de célula."""
    n = len(p); F1 = np.empty(n); F2 = np.empty(n); ID = np.empty(n); C = np.empty((n, 3))
    for a in range(0, n, lote):
        q = p[a:a + lote]; i = np.floor(q).astype(np.int64)
        f1 = np.full(len(q), 9.0); f2 = np.full(len(q), 9.0); idd = np.zeros(len(q)); cc = np.zeros((len(q), 3))
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    c = i + np.array([dx, dy, dz])
                    pt = c + np.stack([_h(c[:, 0], c[:, 1], c[:, 2], s=s + k) for k in (1, 2, 3)], 1)
                    d = np.linalg.norm(pt - q, axis=1)
                    menor = d < f1
                    f2 = np.where(menor, f1, np.minimum(f2, d))
                    idd = np.where(menor, _h(c[:, 0], c[:, 1], c[:, 2], s=s + 7), idd)
                    cc = np.where(menor[:, None], pt, cc); f1 = np.where(menor, d, f1)
        F1[a:a + lote], F2[a:a + lote], ID[a:a + lote], C[a:a + lote] = f1, f2, idd, cc
    return F1, F2, ID, C


def worley2(p, s=0, lote=400_000):
    """Voronoi 2D (p: n×2, em unidades de célula): (F1, F2, id, ponto)."""
    n = len(p); F1 = np.empty(n); F2 = np.empty(n); ID = np.empty(n); C = np.empty((n, 2))
    for a in range(0, n, lote):
        q = p[a:a + lote]; i = np.floor(q).astype(np.int64)
        f1 = np.full(len(q), 9.0); f2 = np.full(len(q), 9.0); idd = np.zeros(len(q)); cc = np.zeros((len(q), 2))
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                c = i + np.array([dx, dy])
                pt = c + 0.15 + 0.7 * np.stack([_h(c[:, 0], c[:, 1], s=s + k) for k in (1, 2)], 1)
                d = np.linalg.norm(pt - q, axis=1); menor = d < f1
                f2 = np.where(menor, f1, np.minimum(f2, d)); idd = np.where(menor, _h(c[:, 0], c[:, 1], s=s + 7), idd)
                cc = np.where(menor[:, None], pt, cc); f1 = np.where(menor, d, f1)
        F1[a:a + lote], F2[a:a + lote], ID[a:a + lote], C[a:a + lote] = f1, f2, idd, cc
    return F1, F2, ID, C


# ---------------------------------------------------------------- o atlas
class Atlas:
    """O UV novo de uma arma. ``tri``: os triângulos (T×3×3, m, espaço da arma); ``peca``: de que peça cada um é."""

    def __init__(self, pecas, tamanho):
        self.tamanho = L = tamanho
        P = np.concatenate([m['pos'] for _, _, m in pecas]).astype(float); T = len(P)
        self.P = P; self.uv0 = np.concatenate([m['uv'] for _, _, m in pecas]).astype(float)
        self.peca = np.concatenate([np.full(len(m['pos']), k) for k, (_, _, m) in enumerate(pecas)])
        fn = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0]); dobro = np.linalg.norm(fn, axis=1); self.fn = fn / (dobro[:, None] + 1e-20)
        eixo = np.abs(self.fn).argmax(1); lado = np.take_along_axis(self.fn, eixo[:, None], 1)[:, 0] > 0
        direcao = eixo * 2 + lado; self.eixo = eixo
        # vizinhos pelas arestas (vértices iguais a 0,05 mm)
        _, vid = np.unique(np.round(P.reshape(-1, 3) * 2e4).astype(np.int64), axis=0, return_inverse=True); vid = vid.reshape(T, 3)
        A = np.stack([vid[:, [0, 1]], vid[:, [1, 2]], vid[:, [2, 0]]], 1); A.sort(axis=2)
        chave = (A[..., 0] * (vid.max() + 1) + A[..., 1]).ravel(); dono = np.repeat(np.arange(T), 3); local = np.tile(np.arange(3), T)
        o = np.argsort(chave, kind='stable'); ch, do, lo = chave[o], dono[o], local[o]
        par = np.nonzero(ch[1:] == ch[:-1])[0]; a, b = do[par], do[par + 1]
        # a quina viva: dobra grande com a vizinha, ou aresta sem vizinha (a borda de uma peça)
        self.viva = np.ones((T, 3), bool)
        liso = (self.fn[a] * self.fn[b]).sum(1) > QUINA
        self.viva[a[liso], lo[par][liso]] = False; self.viva[b[liso], lo[par + 1][liso]] = False
        # as cartas: vizinhas da mesma peça viradas para o mesmo lado
        pai = np.arange(T)
        def raiz(x):
            while pai[x] != x:
                pai[x] = pai[pai[x]]; x = pai[x]
            return x
        for x, y in zip(a[(direcao[a] == direcao[b]) & (self.peca[a] == self.peca[b])], b[(direcao[a] == direcao[b]) & (self.peca[a] == self.peca[b])]):
            rx, ry = raiz(x), raiz(y)
            if rx != ry: pai[ry] = rx
        carta = np.array([raiz(x) for x in range(T)]); _, carta = np.unique(carta, return_inverse=True); self.carta = carta
        # cada triângulo no plano da carta (m): x -> (z, y), y -> (z, x), z -> (x, y)
        eixos2 = np.array([[2, 1], [2, 0], [0, 1]])[eixo]
        Q = np.empty((T, 3, 2))
        for i in range(2): Q[:, :, i] = np.take_along_axis(P.transpose(0, 2, 1), eixos2[:, i][:, None, None].repeat(3, 2), 1)[:, 0, :]
        k = carta.max() + 1; lo2 = np.full((k, 2), np.inf); hi2 = np.full((k, 2), -np.inf)
        np.minimum.at(lo2, carta, Q.min(1)); np.maximum.at(hi2, carta, Q.max(1))
        caixa = hi2 - lo2
        # a maior escala (px/m) em que as cartas cabem no quadrado
        def cabe(s):
            wh = np.ceil(caixa * s).astype(int) + 2 * MARGEM
            if (wh[:, 0] > L).any(): return None
            off = np.zeros((k, 2), int); x = y = alt = 0
            for c in np.argsort(-wh[:, 1], kind='stable'):
                w, h = wh[c]
                if x + w > L: x = 0; y += alt; alt = 0
                off[c] = (x, y); x += w; alt = max(alt, h)
            return off if y + alt <= L else None
        a_, b_ = 50.0, 50000.0
        for _ in range(28):
            m = (a_ * b_) ** 0.5
            if cabe(m) is None: b_ = m
            else: a_ = m
        self.escala = a_; off = cabe(a_)
        self.px = (Q - lo2[carta][:, None, :]) * a_ + off[carta][:, None, :] + MARGEM          # T×3×2, em px (x, y de cima)
        self.uv = np.stack([self.px[..., 0] / L, 1 - self.px[..., 1] / L], -1)                   # como os .obj (v de baixo para cima)
        self._rasterizar()

    def _rasterizar(self):
        L = self.tamanho; T = len(self.P); tri = np.full(L * L, -1, np.int32)
        for t in range(T):
            (ax, ay), (bx, by), (cx, cy) = self.px[t]
            x0, x1 = int(np.floor(min(ax, bx, cx))), int(np.ceil(max(ax, bx, cx))); y0, y1 = int(np.floor(min(ay, by, cy))), int(np.ceil(max(ay, by, cy)))
            x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, L), min(y1, L)
            if x1 <= x0 or y1 <= y0: continue
            den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(den) < 1e-12: continue
            X, Y = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
            l0 = ((by - cy) * (X - cx) + (cx - bx) * (Y - cy)) / den; l1 = ((cy - ay) * (X - cx) + (ax - cx) * (Y - cy)) / den
            dentro = (l0 >= -1e-4) & (l1 >= -1e-4) & (1 - l0 - l1 >= -1e-4)
            tri[(Y[dentro].astype(int)) * L + X[dentro].astype(int)] = t
        # a margem: cada texel vazio ao lado de um cheio fica com o triângulo do vizinho (o desenho continua por ele)
        img = tri.reshape(L, L)
        for _ in range(MARGEM + 2):
            vazio = img < 0
            for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
                viz = np.roll(img, (dy, dx), (0, 1)); usar = vazio & (viz >= 0) & (img < 0)
                img[usar] = viz[usar]
        self.tri = img.ravel()
        cheio = np.nonzero(self.tri >= 0)[0]; t = self.tri[cheio]
        X = cheio % L + 0.5; Y = cheio // L + 0.5; A, B, C = (self.px[t, i] for i in range(3))
        den = (B[:, 1] - C[:, 1]) * (A[:, 0] - C[:, 0]) + (C[:, 0] - B[:, 0]) * (A[:, 1] - C[:, 1])
        den = np.where(np.abs(den) < 1e-12, 1e-12, den)
        l0 = ((B[:, 1] - C[:, 1]) * (X - C[:, 0]) + (C[:, 0] - B[:, 0]) * (Y - C[:, 1])) / den
        l1 = ((C[:, 1] - A[:, 1]) * (X - C[:, 0]) + (A[:, 0] - C[:, 0]) * (Y - C[:, 1])) / den
        self.cheio = cheio; self.bary = np.stack([l0, l1, 1 - l0 - l1], 1)
        print(f"    atlas {L}px: {self.carta.max() + 1} cartas, {self.escala / 1000:.2f} px/mm, {len(cheio) / L / L:.0%} do quadrado usado")


class Texels:
    """O que o desenho recebe, um valor por texel usado do atlas."""

    def __init__(self, at, cor_base):
        t = at.tri[at.cheio]; b = at.bary; P = at.P[t]
        self.P = (b[:, :, None] * P).sum(1)                       # posição 3D (m, espaço da arma)
        self.N = at.fn[t]; self.eixo = at.eixo[t]; self.peca = at.peca[t]
        uv = (b[:, :, None] * at.uv0[t]).sum(1); self.base = cor_base(uv, t)       # a cor de antes (linear)
        self.lum = self.base @ np.array([0.2126, 0.7152, 0.0722])
        mx, mn = self.base.max(1), self.base.min(1); self.sat = (mx - mn) / (mx + 1e-6)
        # distância (m) até a quina viva mais perto dentro do triângulo
        A, B, C = P[:, 0], P[:, 1], P[:, 2]; area2 = np.linalg.norm(np.cross(B - A, C - A), axis=1) + 1e-12
        alturas = np.stack([area2 / (np.linalg.norm(C - B, axis=1) + 1e-12), area2 / (np.linalg.norm(A - C, axis=1) + 1e-12), area2 / (np.linalg.norm(B - A, axis=1) + 1e-12)], 1)
        # aresta oposta ao vértice i: (1,2), (2,0), (0,1) -> as quinas são guardadas por aresta local (0: 0-1, 1: 1-2, 2: 2-0)
        viva = at.viva[t][:, [1, 2, 0]]
        d = np.where(viva, np.clip(b, 0, None) * alturas, 9.0); self.quina = d.min(1)
        tudo = at.P.reshape(-1, 3); lo, hi = tudo.min(0), tudo.max(0)
        self.u = (self.P[:, 2] - lo[2]) / (hi[2] - lo[2])          # 0 na boca do cano, 1 na coronha
        self.alt = (self.P[:, 1] - lo[1]) / (hi[1] - lo[1])
        ix = np.array([[2, 1], [2, 0], [0, 1]])[self.eixo]           # o plano da face: lado (z, y), cima (z, x), frente (x, y)
        self.plano = np.stack([np.take_along_axis(self.P, ix[:, [0]], 1)[:, 0], np.take_along_axis(self.P, ix[:, [1]], 1)[:, 0]], 1)
        self.lado = self.eixo == 0
        self.n = len(self.P)


# ---------------------------------------------------------------- os desenhos
def classes(X):
    """O que cada texel era: preto (polímero), claro, e metal (o resto)."""
    escuro = X.lum < 0.035; claro = X.lum > 0.22
    return escuro, claro, ~escuro & ~claro


def quinas(X, larg=0.0010):
    """1 na quina viva, 0 a meia ``larg`` dela (o desgaste e o brilho das arestas)."""
    return 1 - suave(0.0, larg * 0.5, X.quina)


def circuito(X):
    """M4A4 Neon (épica): grafite anodizado com trilhas de circuito ciano acesas nas laterais e no topo."""
    escuro, claro, metal = classes(X)
    cor = np.where(escuro[:, None], lin('#15181e'), np.where(claro[:, None], lin('#4a5466'), lin('#2a303a')))
    cor = cor * (0.85 + 0.3 * fbm(X.P * 60, 3, s=3))[:, None]
    c = 0.011; q = X.plano / c; i = np.floor(q).astype(int); f = q - i - 0.5
    w = 0.0006 / c
    seg_h = np.floor(i[:, 0] / 3 + _h(i[:, 1], s=1) * 3); ativo_h = _h(i[:, 1], seg_h, X.eixo, s=2) < 0.42
    seg_v = np.floor(i[:, 1] / 2 + _h(i[:, 0], s=4) * 2); ativo_v = _h(i[:, 0], seg_v, X.eixo, s=5) < 0.28
    dh = np.where(ativo_h, np.abs(f[:, 1]), 9); dv = np.where(ativo_v, np.abs(f[:, 0]), 9)
    pad = (_h(i[:, 0], i[:, 1], X.eixo, s=6) < 0.16) & (ativo_h | ativo_v)
    dp = np.where(pad, np.abs(np.linalg.norm(f, axis=1) - 0.17), 9)
    d = np.minimum(np.minimum(dh, dv), dp)
    visivel = X.eixo != 2
    linha = (1 - suave(w * 0.6, w, d)) * visivel; borda = (1 - suave(w, w * 2.4, d)) * visivel * (1 - linha)
    ciano = lin('#22e4ff')
    cor = mix(cor, lin('#05080c'), borda * 0.8); cor = mix(cor, mix(lin('#8ff4ff'), ciano, 0.5), linha)
    q_ = quinas(X, 0.0006)
    emis = ciano[None, :] * (linha * 1.0)[:, None] + ciano[None, :] * (q_ * 0.12 * (X.eixo != 2))[:, None]
    cor = mix(cor, lin('#6fd6ff'), q_ * 0.2)
    return cor, emis, np.where(linha > 0.5, 0.25, 0.42), np.full(X.n, 0.35)


def dragao(X):
    """M4A4 Dragão (lendária): escamas vermelhas de borda dourada, brasa entre elas; o metal vira ouro gravado."""
    escuro, claro, metal = classes(X)
    q = X.plano / np.array([0.021, 0.015]); q[:, 0] += 0.5 * (np.floor(q[:, 1]) % 2)
    F1, F2, ID, _ = worley2(q, s=11)
    borda = 1 - suave(0.05, 0.13, F2 - F1)
    dentro = suave(0.0, 0.75, F1)
    vermelho = mix(lin('#d8301c'), lin('#6e0a10'), dentro * 0.8)[:, :] * (0.75 + 0.5 * ID)[:, None]
    ouro = lin('#e8b54a') * (0.8 + 0.4 * fbm(X.P * 120, 3, s=12))[:, None]
    cor = mix(vermelho, ouro, borda)
    gravado = fbm(X.P * 90, 4, s=13)
    ouro_m = mix(lin('#b8862a'), lin('#ffd77a'), suave(0.35, 0.7, gravado))
    cor = np.where(metal[:, None] & (X.peca == 1)[:, None], ouro_m, cor)            # o ferrolho: ouro
    cor = np.where(claro[:, None], ouro_m, cor)
    q_ = quinas(X, 0.0009); cor = mix(cor, lin('#ffe6a0'), q_ * 0.8)
    brasa = (1 - suave(0.0, 0.06, F2 - F1)) * (X.eixo != 2) * (~claro) * suave(0.35, 0.65, fbm(X.P * 30, 3, s=14))
    emis = lin('#ff5a14')[None, :] * (brasa * 0.9)[:, None]
    metalico = np.where((borda > 0.5) | claro | (X.peca == 1), 1.0, 0.55)
    return cor, emis, np.where(metalico > 0.9, 0.24, 0.32), metalico


def tigre(X):
    """AK-74 Tigre (épica): laranja com listras pretas de tigre, a barriga mais clara."""
    escuro, claro, metal = classes(X)
    laranja = mix(lin('#f6c562'), lin('#e8670f'), suave(0.15, 0.85, X.alt))
    s = np.sin(X.P[:, 2] * 2 * np.pi / 0.062 + 3.6 * fbm(X.P * 16, 4, s=21) + 0.8 * np.sin(X.P[:, 1] * 45))
    larg = 0.45 + 0.3 * fbm(X.P * 40, 3, s=22)
    listra = suave(larg, larg + 0.08, s)
    cor = mix(laranja, lin('#120b06'), listra)
    cor = np.where((metal & (X.lum < 0.1))[:, None], mix(lin('#2a2725'), lin('#0d0b0a'), listra), cor)
    cor = cor * (0.9 + 0.2 * fbm(X.P * 140, 2, s=23))[:, None]
    q_ = quinas(X, 0.0009); cor = mix(cor, lin('#ffdcab'), q_ * 0.45)
    return cor, np.zeros((X.n, 3)), np.full(X.n, 0.55), np.full(X.n, 0.05)


def imperial(X):
    """AK-74 Imperial (lendária): laca preta com filigrana de ouro (arabescos em cada célula, ramos entre elas)."""
    escuro, claro, metal = classes(X)
    c = 0.03; q = X.plano / c
    F1, F2, ID, C = worley2(q, s=31)
    v = q - C; r = np.linalg.norm(v, axis=1); th = np.arctan2(v[:, 1], v[:, 0]) * np.where(ID > 0.5, 1, -1)
    espiral = np.abs(np.sin(r * 2 * np.pi * 2.2 - th)) < 0.3
    espiral = espiral & (r < 0.46)
    ramo = (F2 - F1) < 0.02
    folha = (np.abs(np.sin(th * 3 + ID * 6)) < 0.25) & (np.abs(r - 0.47) < 0.035)
    fil = (espiral | ramo | folha).astype(float)
    fil = fil * (X.eixo != 2) + (X.eixo == 2) * ramo
    laca = lin('#0b0b0e') * (0.8 + 0.4 * fbm(X.P * 50, 3, s=32))[:, None]
    ouro = mix(lin('#a8781f'), lin('#f2cd6b'), fbm(X.P * 160, 3, s=33))
    cor = mix(laca, ouro, fil)
    cor = np.where((metal & (X.peca != 2))[:, None], mix(ouro, lin('#3a2a0c'), fil * 0.8), cor)
    q_ = quinas(X, 0.0008); cor = mix(cor, lin('#ffe08a'), q_ * 0.85)
    eh_ouro = (fil > 0.5) | (metal & (X.peca != 2)) | (q_ > 0.5)
    emis = lin('#ffc04a')[None, :] * (fil * 0.22 * (X.eixo != 2))[:, None]
    return cor, emis, np.where(eh_ouro, 0.26, 0.14), np.where(eh_ouro, 1.0, 0.15)


def artico(X):
    """SCAR-H Ártico (épica): camuflagem digital de neve (branco, gelo e azul-acinzentado)."""
    escuro, claro, metal = classes(X)
    pix = 0.0048; qa = np.floor(X.plano / pix) * pix; qb = np.floor(X.plano / (pix * 2.2)) * pix * 2.2
    z = X.eixo[:, None] * 0.37
    v = 0.6 * fbm(np.c_[qa * 34, z], 3, s=41) + 0.4 * fbm(np.c_[qb * 18, z + 5], 3, s=42)
    cores = [lin('#f4f8fb'), lin('#cfdbe6'), lin('#93abc2'), lin('#53687d')]
    cor = np.where((v < 0.42)[:, None], cores[0], np.where((v < 0.52)[:, None], cores[1], np.where((v < 0.6)[:, None], cores[2], cores[3])))
    cor = np.where((metal & (X.peca == 1))[:, None], lin('#b7c2cc'), cor)
    q_ = quinas(X, 0.0008); cor = mix(cor, lin('#ffffff'), q_ * 0.5)
    return cor, np.zeros((X.n, 3)), np.full(X.n, 0.62), np.where(X.peca == 1, 0.7, 0.0)


def galaxia(X):
    """SCAR-H Galáxia (lendária): nebulosa roxa, rosa e ciano com estrelas acesas; quinas violeta."""
    escuro, claro, metal = classes(X)
    n1 = fbm(X.P * 22, 5, s=51); n2 = fbm(X.P * 9 + 3.1, 4, s=52); n3 = fbm(X.P * 40 + 7.7, 3, s=53)
    cor = mix(lin('#05061a'), lin('#2a0f5c'), suave(0.35, 0.7, n1))
    cor = mix(cor, lin('#d42d8c'), suave(0.55, 0.8, n2) * 0.8)
    cor = mix(cor, lin('#22b8ff'), suave(0.6, 0.85, n1 * 0.6 + n3 * 0.4) * 0.7)
    # estrelas: um ponto por célula de 3 mm, só em algumas
    F1, _, ID, _ = worley(X.P / 0.0032, s=54)
    estrela = (ID > 0.82) * (1 - suave(0.04, 0.16 * (0.6 + ID), F1))
    grande = (ID > 0.985) * (1 - suave(0.05, 0.42, F1)) * 0.7
    brilho = np.clip(estrela + grande, 0, 1)
    cor = mix(cor, lin('#ffffff'), brilho)
    cor = np.where((metal & (X.peca == 1))[:, None], lin('#16141f'), cor)
    q_ = quinas(X, 0.0006)
    neb = cor * suave(0.55, 0.85, n2)[:, None] * 0.25
    emis = lin('#ffffff')[None, :] * brilho[:, None] + neb + lin('#9b5cff')[None, :] * (q_ * 0.7 * (X.eixo != 2))[:, None]
    cor = mix(cor, lin('#b48cff'), q_ * 0.5)
    return cor, emis, np.full(X.n, 0.22), np.where(X.peca == 1, 0.85, 0.25)


def carbono(X):
    """SVD Carbono (épica): fibra de carbono em sarja no que era madeira e polímero; aço escuro e detalhes vermelhos."""
    escuro, claro, metal = classes(X)
    w = 0.0024; q = X.plano / w; i = np.floor(q).astype(int); f = q - i
    horiz = ((i[:, 0] + (i[:, 1] % 4)) % 4) < 2
    ondula = np.where(horiz, np.sin(f[:, 1] * np.pi), np.sin(f[:, 0] * np.pi))
    brilho = np.where(horiz, 0.55 + 0.45 * np.cos((f[:, 0] - 0.5) * np.pi), 0.35 + 0.35 * np.cos((f[:, 1] - 0.5) * np.pi))
    fibra = mix(lin('#0f1012'), lin('#4a4e56'), (ondula * brilho * 0.8))
    madeira = (X.sat > 0.18) & (X.lum > 0.02)
    cor = np.where((escuro | madeira)[:, None], fibra, lin('#33363d'))
    vermelho = lin('#c81d28')
    risca = X.lado & (np.abs(X.alt - 0.58) < 0.006)
    fundo_mag = (X.peca == 2) & (X.P[:, 1] < np.percentile(X.P[X.peca == 2, 1], 8)) if (X.peca == 2).any() else np.zeros(X.n, bool)
    cor = np.where((claro | fundo_mag)[:, None], vermelho, cor)
    cor = np.where(risca[:, None], vermelho, cor)
    q_ = quinas(X, 0.0008); cor = mix(cor, lin('#8a8f99'), q_ * 0.45)
    eh_fibra = escuro | madeira
    return cor, np.zeros((X.n, 3)), np.where(eh_fibra, 0.22, 0.4), np.where(eh_fibra, 0.0, 0.85)


def tempestade(X):
    """SVD Tempestade (lendária): nuvens de tempestade azul-escuras cortadas por raios violeta e ciano acesos."""
    escuro, claro, metal = classes(X)
    nuvem = fbm(X.P * 14, 5, s=61)
    cor = mix(lin('#03050c'), lin('#1d2747'), suave(0.3, 0.75, nuvem))
    cor = cor * (0.8 + 0.4 * fbm(X.P * 70, 3, s=62))[:, None]
    def raio(esc, s):
        r = np.abs(fbm(X.P * esc, 4, s=s) - 0.5) * 2
        return 1 - suave(0.0, 0.06, r), 1 - suave(0.0, 0.2, r)
    a, ga = raio(7.0, 63); b, gb = raio(11.0, 64)
    mascara = suave(0.4, 0.6, fbm(X.P * 5, 2, s=65))
    nucleo = np.clip(a * mascara + b * (1 - mascara) * 0.8, 0, 1); halo = np.clip(ga * mascara + gb * (1 - mascara), 0, 1) * 0.35
    violeta, ciano = lin('#a77bff'), lin('#9df3ff')
    cor_raio = mix(violeta, ciano, mascara)
    cor = mix(cor, mix(cor_raio, lin('#ffffff'), 0.5), nucleo); cor = cor + cor_raio * halo[:, None] * 0.3
    cor = np.where((metal & (X.peca == 1))[:, None], lin('#1b2033'), cor)
    q_ = quinas(X, 0.0006)
    emis = cor_raio * (nucleo * 1.0 + halo * 0.5)[:, None] + ciano[None, :] * (q_ * 0.45 * (X.eixo != 2))[:, None]
    cor = mix(cor, lin('#8fb6ff'), q_ * 0.4)
    return cor, emis, np.full(X.n, 0.3), np.where(X.peca == 1, 0.8, 0.35)


def cobra(X):
    """Desert Eagle Cobra (épica): escamas de cobra verde-esmeralda, a barriga clara, o metal dourado."""
    escuro, claro, metal = classes(X)
    s = 0.0042; q = X.plano / s
    # grade hexagonal: duas grades deslocadas, a célula é o centro mais perto
    a = np.round(q / np.array([1.0, 0.866])) * np.array([1.0, 0.866])
    b = (np.floor(q / np.array([1.0, 0.866])) + 0.5) * np.array([1.0, 0.866])
    da, db = np.linalg.norm(q - a, axis=1), np.linalg.norm(q - b, axis=1)
    c = np.where((da < db)[:, None], a, b); d = np.minimum(da, db)
    idc = _h(np.round(c[:, 0] * 2), np.round(c[:, 1] * 2), X.eixo, s=71)
    rim = suave(0.38, 0.5, d)
    barriga = X.N[:, 1] < -0.4
    verde = mix(lin('#2fc46c'), lin('#0c5a2e'), suave(0.0, 0.45, d)) * (0.75 + 0.45 * idc)[:, None]
    amarelo = mix(lin('#efe08a'), lin('#9d8a2f'), suave(0.0, 0.45, d))
    escama = np.where(barriga[:, None], amarelo, verde)
    cor = mix(escama, lin('#04180c'), rim * 0.85)
    ouro = mix(lin('#a8801c'), lin('#f0cb5a'), fbm(X.P * 150, 3, s=72))
    cor = np.where((claro | (metal & (X.lum > 0.12)))[:, None], ouro, cor)
    q_ = quinas(X, 0.0006); cor = mix(cor, lin('#c9ffd9'), q_ * 0.3)
    eh_ouro = claro | (metal & (X.lum > 0.12))
    return cor, np.zeros((X.n, 3)), np.where(eh_ouro, 0.28, 0.38), np.where(eh_ouro, 1.0, 0.35)


def fenix(X):
    """Desert Eagle Fênix (lendária): cromo preto com chamas que nascem atrás e lambem o cano, acesas."""
    escuro, claro, metal = classes(X)
    cor = mix(lin('#0a0a0c'), lin('#2a2a30'), fbm(X.P * 80, 3, s=81))
    t = X.u + 0.32 * (fbm(X.P * 26 + np.array([0, 0, 0.0]), 4, s=82) - 0.5) + 0.12 * np.sin(X.P[:, 1] * 160 + X.P[:, 2] * 40)
    altura = np.abs(X.alt - 0.62)
    chama = t - altura * 0.55
    fora, meio, dentro = suave(0.5, 0.54, chama), suave(0.64, 0.68, chama), suave(0.78, 0.82, chama)
    cor = mix(cor, lin('#b5120a'), fora); cor = mix(cor, lin('#ff6a00'), meio); cor = mix(cor, lin('#ffd23a'), dentro)
    contorno = (suave(0.44, 0.5, chama) - fora) * 0.8
    cor = mix(cor, lin('#000000'), np.clip(contorno, 0, 1))
    ouro = mix(lin('#b8862a'), lin('#ffd77a'), fbm(X.P * 140, 3, s=83))
    cor = np.where(claro[:, None], ouro, cor)
    emis = lin('#ff3a0a')[None, :] * (fora * 0.5)[:, None] + lin('#ff8a1a')[None, :] * (meio * 0.5)[:, None] + lin('#ffe07a')[None, :] * (dentro * 0.6)[:, None]
    emis = emis * (X.eixo != 2)[:, None]
    q_ = quinas(X, 0.0006); cor = mix(cor, lin('#ffb36a'), q_ * 0.35)
    return cor, emis, np.where(fora > 0.5, 0.3, 0.18), np.where(fora > 0.5, 0.3, 0.9)


def damasco(X):
    """Faca Damasco (épica): aço damasco em ondas na lâmina; cabo de madeira escura com pomo de cobre."""
    lamina = X.P[:, 2] < -0.052
    ondas = np.sin(2 * np.pi * (X.P[:, 1] * 170 + X.P[:, 2] * 45 + 2.4 * fbm(X.P * 70, 4, s=91)))
    aco = mix(lin('#4d525b'), lin('#d2d6dc'), suave(-0.3, 0.6, ondas))
    madeira = mix(lin('#2a1609'), lin('#5a3418'), suave(0.3, 0.7, fbm(X.P * np.array([260, 260, 25]), 4, s=92)))
    cobre = mix(lin('#7a3b18'), lin('#d9894d'), fbm(X.P * 200, 3, s=93))
    cabo = np.where((X.lum > 0.12)[:, None], cobre, madeira)
    cor = np.where(lamina[:, None], aco, cabo)
    q_ = quinas(X, 0.0004); cor = mix(cor, lin('#f2f4f7'), q_ * 0.5 * lamina)
    metalico = np.where(lamina, 1.0, np.where(X.lum > 0.12, 1.0, 0.0))
    return cor, np.zeros((X.n, 3)), np.where(lamina, 0.28, np.where(X.lum > 0.12, 0.35, 0.7)), metalico


def obsidiana(X):
    """Faca Obsidiana (lendária): lâmina de vidro vulcânico preto com veios roxos acesos; o fio brilha."""
    lamina = X.P[:, 2] < -0.052
    F1, F2, ID, _ = worley(X.P / 0.0065, s=101)
    veio = 1 - suave(0.0, 0.05, F2 - F1); halo = (1 - suave(0.0, 0.16, F2 - F1)) * 0.3
    vidro = mix(lin('#050407'), lin('#1a1424'), fbm(X.P * 120, 3, s=102))
    roxo = lin('#b54dff')
    cor_l = mix(vidro, mix(roxo, lin('#ffffff'), 0.25), veio) + roxo * halo[:, None] * 0.4
    cabo = mix(lin('#0c0b0f'), lin('#241b30'), suave(0.4, 0.6, fbm(X.P * np.array([40, 40, 400]), 3, s=103)))
    anel = (np.abs(np.sin(X.P[:, 2] * 2 * np.pi / 0.012)) < 0.12) & ~lamina
    cabo = np.where(anel[:, None], roxo * 0.8, cabo)
    cor = np.where(lamina[:, None], cor_l, cabo)
    q_ = quinas(X, 0.0004) * lamina
    emis = roxo[None, :] * ((veio + halo) * lamina + anel * 0.6 + q_ * 0.8)[:, None]
    cor = mix(cor, lin('#e0b5ff'), q_ * 0.6)
    return cor, emis, np.where(lamina, 0.06, 0.45), np.where(lamina, 0.1, 0.2)


ESTILOS = dict(circuito=circuito, dragao=dragao, tigre=tigre, imperial=imperial, artico=artico, galaxia=galaxia,
               carbono=carbono, tempestade=tempestade, cobra=cobra, fenix=fenix, damasco=damasco, obsidiana=obsidiana)


# ---------------------------------------------------------------- gravar
def png(caminho, img):
    """img: L×L×3 (uint8)."""
    def bloco(tipo, dados): return struct.pack('>I', len(dados)) + tipo + dados + struct.pack('>I', zlib.crc32(tipo + dados) & 0xffffffff)
    linhas = b''.join(b'\x00' + img[y].tobytes() for y in range(img.shape[0]))
    dados = b'\x89PNG\r\n\x1a\n' + bloco(b'IHDR', struct.pack('>IIBBBBB', img.shape[1], img.shape[0], 8, 2, 0, 0, 0)) + bloco(b'IDAT', zlib.compress(linhas, 6)) + bloco(b'IEND', b'')
    open(caminho, 'wb').write(dados); return dados


def cor_da_paleta(at):
    """A cor (linear) que cada triângulo tinha na paleta."""
    cores = np.array([c for _, c in reais._cores()])
    uv = at.uv0[:, 0]; col = np.clip((uv[:, 0] * reais.LADO).astype(int), 0, reais.LADO - 1); lin_ = np.clip(((1 - uv[:, 1]) * reais.LADO).astype(int), 0, reais.LADO - 1)
    por_tri = cores[np.clip(lin_ * reais.LADO + col, 0, len(cores) - 1)]
    return lambda uv, t: por_tri[t]


def cor_da_faca():
    from PIL import Image
    img = np.asarray(Image.open(os.path.join(AQUI, 'dados', 'knifeColor.jpg')).convert('RGB'), float) / 255
    img = np.where(img <= 0.04045, img / 12.92, ((img + 0.055) / 1.055) ** 2.4); H, W = img.shape[:2]
    def f(uv, t):
        x = np.clip((uv[:, 0] % 1) * W, 0, W - 1).astype(int); y = np.clip((1 - uv[:, 1] % 1) * H, 0, H - 1).astype(int)
        return img[y, x]
    return f


def fazer(id_, tamanho=None):
    P_ = PELES[id_]; base = P_['base']; L = tamanho or P_['tamanho']
    print(f"  {id_} ({P_['nome']}, {P_['estilo']}):")
    A = reais.arma(base); pecas = A['pecas']
    at = Atlas(pecas, L)
    X = Texels(at, cor_da_faca() if base == 'faca' else cor_da_paleta(at))
    cor, emis, rug, met = ESTILOS[P_['estilo']](X)
    met = np.asarray(met, float) * METAL          # metal puro, sem céu para refletir (a mão na tela, a vitrine), fica preto
    def imagem(v, s=True):
        img = np.zeros((L * L, 3), np.uint8); img[at.cheio] = np.round((srgb(v) if s else np.clip(v, 0, 1)) * 255); return img.reshape(L, L, 3)
    pasta = os.path.join(ARMAS, id_); os.makedirs(pasta, exist_ok=True)
    nome = 'T_' + ''.join(p.capitalize() for p in id_.split('_'))
    caminhos = {k: os.path.join(pasta, f'{nome}_{k}.png') for k in ('D', 'E', 'ORM')}
    dados = {}
    dados['D'] = png(caminhos['D'], imagem(cor))
    dados['E'] = png(caminhos['E'], imagem(emis))
    orm = np.stack([np.ones(X.n), np.clip(rug, 0.03, 1), np.clip(met, 0, 1)], 1)
    dados['ORM'] = png(caminhos['ORM'], imagem(orm, s=False))
    print(f"    texturas: {', '.join(os.path.basename(c) for c in caminhos.values())}; acende {np.mean(emis.max(1) > 0.05):.1%} da pele")
    # as peças no mundo: a mesma malha, com o UV novo
    fichas = json.load(open(os.path.join(ARMAS, 'armas.json'), encoding='utf-8'))
    entrada = json.loads(json.dumps(fichas['armas'][base])); entrada.pop('externo', None); partes = []; k = 0
    for velho in os.listdir(pasta):
        if velho.endswith('.obj'): os.remove(os.path.join(pasta, velho))
    nos = {}
    for arquivo, no, m in pecas:
        n = len(m['pos']); uv = at.uv[k:k + n]; k += n
        nos.setdefault(no, []).append((arquivo, dict(pos=m['pos'], normal=m['normal'], uv=uv)))
    for no, lista in nos.items():
        for arquivo, m in lista:
            if base == 'faca':
                # no mundo a faca fica de pé na mão (a antiga: lâmina para +Y, o pomo em y = −8,4 cm); a nova tem a lâmina para −Z
                G = np.array([[1.0, 0, 0], [0, 0, -1], [0, 1, 0]]); pos = m['pos'] @ G.T; pos[..., 1] += -0.084 - pos[..., 1].min()
                m = dict(pos=pos, normal=m['normal'] @ G.T, uv=m['uv'])
            nomeobj = f'{arquivo}_{id_}.obj'; reais._obj(os.path.join(pasta, nomeobj), m)
            partes.append(dict(malha=f'res://assets/armas/{id_}/{nomeobj}', material=f'res://assets/armas/{id_}/{nome}_D.png', no=no))
    entrada['partes'] = partes; fichas['armas'][id_] = entrada
    json.dump(fichas, open(os.path.join(ARMAS, 'armas.json'), 'w', encoding='utf-8'), indent=1, ensure_ascii=False, sort_keys=True)
    open(os.path.join(ARMAS, 'armas.json'), 'a', encoding='utf-8').write('\n')
    # a primeira pessoa: o conjunto da arma de origem com o UV e o material novos
    na_mao(id_, base, at, dados)
    return at, X


def na_mao(id_, base, at, dados):
    import shutil
    from esqueleto import Esqueleto
    origem = os.path.join(FP, base + '.glb'); destino = os.path.join(FP, id_ + '.glb')
    shutil.copy(origem, destino); E = Esqueleto(destino); j = E.j
    def textura(png_, nome):
        while len(E.g.bin) % 4: E.g.bin.append(0)
        off = len(E.g.bin); E.g.bin += png_
        j['bufferViews'].append({'buffer': 0, 'byteOffset': off, 'byteLength': len(png_)})
        j.setdefault('images', []).append({'bufferView': len(j['bufferViews']) - 1, 'mimeType': 'image/png', 'name': nome})
        j.setdefault('samplers', []).append({'magFilter': 9729, 'minFilter': 9987, 'wrapS': 33071, 'wrapT': 33071})
        j.setdefault('textures', []).append({'sampler': len(j['samplers']) - 1, 'source': len(j['images']) - 1})
        return len(j['textures']) - 1
    j['materials'].append({'name': id_, 'pbrMetallicRoughness': {'baseColorTexture': {'index': textura(dados['D'], id_ + '_D')},
                                                                'metallicRoughnessTexture': {'index': textura(dados['ORM'], id_ + '_ORM')},
                                                                'metallicFactor': 1.0, 'roughnessFactor': 1.0},
                           'emissiveTexture': {'index': textura(dados['E'], id_ + '_E')}, 'emissiveFactor': [1.0, 1.0, 1.0]})
    mat = len(j['materials']) - 1
    # o UV, na ordem em que conjuntos.abrir pôs as peças: o fuzil (tudo menos o carregador) e o carregador
    pecas = reais.arma(base)['pecas']; k = 0; por_no = {}
    for arquivo, no, m in pecas:
        n = len(m['pos']); por_no.setdefault('carregador' if no == 'Magazine' else 'corpo', []).append(at.uv[k:k + n]); k += n
    uv_corpo = np.concatenate(por_no['corpo']).reshape(-1, 2); uv_mag = np.concatenate(por_no['carregador']).reshape(-1, 2) if 'carregador' in por_no else None
    trocados = 0
    for no in j['nodes']:
        if 'mesh' not in no: continue
        for p in j['meshes'][no['mesh']]['primitives']:
            cont = j['accessors'][p['attributes']['POSITION']]['count']
            uv = uv_mag if no.get('name') == 'carregador' else uv_corpo
            if uv is None or len(uv) != cont: raise SystemExit(f"PELE ERRO: {id_}: a malha {no.get('name')} tem {cont} vértices, o atlas {None if uv is None else len(uv)}")
            g = uv.copy(); g[:, 1] = 1 - g[:, 1]
            p['attributes']['TEXCOORD_0'] = E._cru(g.astype(np.float32), 5126, 'VEC2', 34962); p['material'] = mat; trocados += 1
    E.podar(); E.salvar()
    print(f"    primeira pessoa: fp/{id_}.glb ({trocados} malhas com o UV e o material novos)")


def ficha_fp(ids):
    """As entradas ``<id>`` de ``fp/bracos.json``: a da arma de origem, com o arquivo novo."""
    caminho = os.path.join(FP, 'bracos.json'); d = json.load(open(caminho, encoding='utf-8'))
    for id_ in ids:
        base = PELES[id_]['base']; chave = [k for k, v in d.items() if base in v.get('armas', [])][0]
        e = json.loads(json.dumps(d[chave])); e['armas'] = [id_]; e['arquivo'] = f'res://assets/armas/fp/{id_}.glb'; d[id_] = e
    json.dump(d, open(caminho, 'w', encoding='utf-8'), indent=1, ensure_ascii=False, sort_keys=True); open(caminho, 'a', encoding='utf-8').write('\n')


if __name__ == '__main__':
    args = sys.argv[1:]; tam = None
    if '--tamanho' in args: i = args.index('--tamanho'); tam = int(args[i + 1]); del args[i:i + 2]
    ids = args or list(PELES)
    for id_ in ids: fazer(id_, tam)
    ficha_fp(ids)
