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
import formas_mao  # noqa: E402

RAIZ = os.path.dirname(os.path.dirname(AQUI))
ARMAS = os.path.join(RAIZ, 'assets', 'armas')
FP = os.path.join(ARMAS, 'fp')

# As peles: a arma de origem, o desenho, o nome e o tamanho do atlas (px).
PELES = dict(
    # as armas sem pele: o acabamento de fábrica (a base é ela mesma: grava por cima do conjunto e da ficha dela)
    ar15=dict(base='ar15', estilo='fabrica', nome='AR-15', tamanho=2048, textura='T_AR15'),
    m4a4=dict(base='m4a4', estilo='fabrica', nome='M4A4', tamanho=2048),
    ak74=dict(base='ak74', estilo='fabrica', nome='AK-74', tamanho=2048),
    scar=dict(base='scar', estilo='fabrica', nome='SCAR-H', tamanho=2048),
    svd=dict(base='svd', estilo='fabrica', nome='SVD', tamanho=2048),
    deagle=dict(base='deagle', estilo='fabrica', nome='Desert Eagle', tamanho=1024),
    # as peles do AR-15 (eram do Blender, tools/armas/blender/ar15_ultimate.py; aqui o atlas é mais denso e o relevo entra)
    ar15_ultimate=dict(base='ar15', estilo='chamas', nome='AR-15 Ultimate', tamanho=2048, textura='T_AR15_Ultimate'),
    ar15_ouro=dict(base='ar15', estilo='ouro', nome='AR-15 Ouro', tamanho=2048, textura='T_AR15_Ouro', metal=0.8),
    ar15_diamante=dict(base='ar15', estilo='diamante', nome='AR-15 Diamante', tamanho=2048, textura='T_AR15_Diamante', metal=1.0),
    ar15_8bit=dict(base='ar15', estilo='pixel', nome='AR-15 8 Bit', tamanho=2048, textura='T_AR15_8Bit', plano=True, amostras=1),
    ar15_lava=dict(base='ar15', estilo='lava', nome='AR-15 Lava', tamanho=2048, textura='T_AR15_Lava'),
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
    # a faca e a granada, com a forma nova (formas_mao.py, 20/11): a de todo mundo e as peles
    faca=dict(base='faca', estilo='aco', nome='Faca', tamanho=1024, metal=0.9),
    faca_damasco=dict(base='faca', estilo='damasco', nome='Faca Damasco', tamanho=1024, metal=0.9),
    faca_obsidiana=dict(base='faca', estilo='obsidiana', nome='Faca Obsidiana', tamanho=1024, metal=0.9),
    choque=dict(base='choque', estilo='granada', nome='Granada', tamanho=1024, metal=0.7),
    choque_carbono=dict(base='choque', estilo='granada_carbono', nome='Granada Carbono', tamanho=1024, metal=0.7),
    choque_neon=dict(base='choque', estilo='granada_neon', nome='Granada Neon', tamanho=1024, metal=0.7),
    choque_magma=dict(base='choque', estilo='granada_magma', nome='Granada Magma', tamanho=1024),
    choque_ouro=dict(base='choque', estilo='granada_ouro', nome='Granada Ouro', tamanho=1024, metal=0.85),
)
MARGEM = 2            # px em volta de cada carta (o filtro da textura não puxa a vizinha)
METAL = 0.5           # quanto do "metal" do desenho vai para a textura (metal puro, sem céu para refletir, fica preto)
AMOSTRAS = 2          # o desenho é feito num atlas 2× maior e reduzido: a linha fina sai sem serrilhado
LOTE = 1_200_000      # texels por vez (o que cabe folgado na memória)
QUINA = np.cos(np.radians(47))      # faces vizinhas com mais que isso de dobra fazem uma quina viva; com menos, a normal é lisa (o cano de oito lados fica redondo)
RAIO_QUINA, RAIO_CANTO = 0.0030, 0.0045   # até que distância (m) se mede a quina saliente e o canto fundo
BISEL, FILETE = 0.0012, 0.0010            # quanto da face (m) o relevo arredonda junto à quina e ao canto


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
    """O UV novo de uma arma. ``pecas``: [(arquivo, nó, malha)] com ``pos`` (T×3×3, m, espaço da arma).

    ``amostras`` (1 ou 2): o desenho é calculado num atlas ``amostras`` vezes maior e reduzido no fim — a linha fina
    (a quina, a estrela, a filigrana) sai lisa, sem serrilhado. O UV é o mesmo nos dois tamanhos."""

    def __init__(self, pecas, tamanho, amostras=1):
        self.saida = tamanho; self.amostras = amostras; self.tamanho = L = tamanho * amostras; self.margem = MARGEM * amostras
        P = np.concatenate([m['pos'] for _, _, m in pecas]).astype(float); T = len(P)
        self.P = P; self.uv0 = np.concatenate([m['uv'] for _, _, m in pecas]).astype(float)
        self.peca = np.concatenate([np.full(len(m['pos']), k) for k, (_, _, m) in enumerate(pecas)])
        fn = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0]); dobro = np.linalg.norm(fn, axis=1); self.fn = fn / (dobro[:, None] + 1e-20)
        self.area = dobro / 2
        eixo = np.abs(self.fn).argmax(1); lado = np.take_along_axis(self.fn, eixo[:, None], 1)[:, 0] > 0
        direcao = eixo * 2 + lado; self.eixo = eixo
        # vizinhos pelas arestas (vértices iguais a 0,05 mm); a aresta local k vai do vértice k ao k + 1
        _, vid = np.unique(np.round(P.reshape(-1, 3) * 2e4).astype(np.int64), axis=0, return_inverse=True); vid = vid.reshape(T, 3)
        A = np.stack([vid[:, [0, 1]], vid[:, [1, 2]], vid[:, [2, 0]]], 1); A.sort(axis=2)
        chave = (A[..., 0] * (vid.max() + 1) + A[..., 1]).ravel(); dono = np.repeat(np.arange(T), 3); local = np.tile(np.arange(3), T)
        o = np.argsort(chave, kind='stable'); ch, do, lo = chave[o], dono[o], local[o]
        par = np.nonzero(ch[1:] == ch[:-1])[0]; a, b = do[par], do[par + 1]; la, lb = lo[par], lo[par + 1]
        mesma = self.peca[a] == self.peca[b]
        # a quina viva: dobra grande com a vizinha, ou aresta sem vizinha (a borda de uma peça)
        self.viva = np.ones((T, 3), bool)
        liso = ((self.fn[a] * self.fn[b]).sum(1) > QUINA) & mesma
        self.viva[a[liso], la[liso]] = False; self.viva[b[liso], lb[liso]] = False
        # para onde a superfície continua do outro lado de cada aresta: a normal da face vizinha; na borda sem vizinha,
        # para fora da face, no plano dela. E se a quina é saliente (convexa) ou reentrante (o canto fundo).
        k3 = np.arange(3)
        e = P[:, (k3 + 1) % 3] - P[:, k3]; fora = np.cross(e, self.fn[:, None, :]); fora /= np.linalg.norm(fora, axis=2, keepdims=True) + 1e-20
        vira = (fora * (P[:, (k3 + 2) % 3] - P[:, k3])).sum(2) > 0; fora[vira] *= -1
        self.viz = fora; self.convexa = np.ones((T, 3), bool)
        s = (self.fn[a] * (P[b, (lb + 2) % 3] - P[a, la])).sum(1)
        self.viz[a, la] = self.fn[b]; self.viz[b, lb] = self.fn[a]
        self.convexa[a, la] = s < 0; self.convexa[b, lb] = s < 0
        # as normais lisas: em cada vértice, as faces ligadas por aresta lisa dividem a mesma normal (a média pela área
        # e pelo ângulo do canto) — o cano de oito lados vira redondo, a quina de verdade continua quina
        pai = np.arange(3 * T)
        def raiz(x):
            while pai[x] != x:
                pai[x] = pai[pai[x]]; x = pai[x]
            return x
        for ta, ka, tb, kb in zip(a[liso].tolist(), la[liso].tolist(), b[liso].tolist(), lb[liso].tolist()):
            for ia in (ka, (ka + 1) % 3):
                for ib in (kb, (kb + 1) % 3):
                    if vid[ta, ia] == vid[tb, ib]:
                        ra, rb = raiz(ta * 3 + ia), raiz(tb * 3 + ib)
                        if ra != rb: pai[rb] = ra
        leque = np.array([raiz(x) for x in range(3 * T)]); _, leque = np.unique(leque, return_inverse=True)
        u1 = P[:, (k3 + 1) % 3] - P[:, k3]; u2 = P[:, (k3 + 2) % 3] - P[:, k3]
        cosang = (u1 * u2).sum(2) / (np.linalg.norm(u1, axis=2) * np.linalg.norm(u2, axis=2) + 1e-20)
        peso = (np.arccos(np.clip(cosang, -1, 1)) * self.area[:, None]).ravel()
        soma = np.zeros((leque.max() + 1, 3)); np.add.at(soma, leque, np.repeat(self.fn, 3, axis=0) * peso[:, None])
        vn = soma[leque]; vn /= np.linalg.norm(vn, axis=1, keepdims=True) + 1e-20
        self.vn = vn.reshape(T, 3, 3); self.leque = leque.reshape(T, 3); self.peso_canto = peso.reshape(T, 3)
        # as cartas: vizinhas da mesma peça viradas para o mesmo lado
        pai = np.arange(T)
        junta = (direcao[a] == direcao[b]) & mesma
        for x, y in zip(a[junta].tolist(), b[junta].tolist()):
            rx, ry = raiz(x), raiz(y)
            if rx != ry: pai[ry] = rx
        carta = np.array([raiz(x) for x in range(T)]); _, carta = np.unique(carta, return_inverse=True); self.carta = carta
        # cada triângulo no plano da carta (m): x -> (z, y), y -> (z, x), z -> (x, y)
        eixos2 = np.array([[2, 1], [2, 0], [0, 1]])[eixo]
        Q = np.empty((T, 3, 2))
        for i in range(2): Q[:, :, i] = np.take_along_axis(P.transpose(0, 2, 1), eixos2[:, i][:, None, None].repeat(3, 2), 1)[:, 0, :]
        k = carta.max() + 1; lo2 = np.full((k, 2), np.inf); hi2 = np.full((k, 2), -np.inf)
        np.minimum.at(lo2, carta, Q.min(1)); np.maximum.at(hi2, carta, Q.max(1))
        caixa = hi2 - lo2; M2 = self.margem
        # a maior escala (px/m) em que as cartas cabem no quadrado (a conta é feita no tamanho de saída: o atlas maior é
        # o mesmo, ampliado)
        Ls = tamanho
        def cabe(s):
            wh = np.ceil(caixa * s).astype(int) + 2 * MARGEM
            if (wh[:, 0] > Ls).any(): return None
            off = np.zeros((k, 2), int); x = y = alt = 0
            for c in np.argsort(-wh[:, 1], kind='stable'):
                w, h = wh[c]
                if x + w > Ls: x = 0; y += alt; alt = 0
                off[c] = (x, y); x += w; alt = max(alt, h)
            return off if y + alt <= Ls else None
        a_, b_ = 50.0, 50000.0
        for _ in range(28):
            m = (a_ * b_) ** 0.5
            if cabe(m) is None: b_ = m
            else: a_ = m
        off = cabe(a_); self.escala = a_ * amostras                                             # px/m no atlas de trabalho
        self.px = ((Q - lo2[carta][:, None, :]) * a_ + off[carta][:, None, :] + MARGEM) * amostras   # T×3×2, em px (x, y de cima)
        self.uv = np.stack([self.px[..., 0] / L, 1 - self.px[..., 1] / L], -1)                   # como os .obj (v de baixo para cima)
        # a base de cada face no desenho: para onde o u cresce e para onde é "para cima" na imagem (m, espaço da arma)
        ex = np.eye(3)
        def no_plano(ax):
            dom = np.take_along_axis(self.fn, eixo[:, None], 1)[:, 0]; dom = np.where(np.abs(dom) < 1e-9, 1.0, dom)
            v = ex[ax] - ((self.fn * ex[ax]).sum(1) / dom)[:, None] * ex[eixo]
            return v / (np.linalg.norm(v, axis=1, keepdims=True) + 1e-20)
        self.Tu = no_plano(eixos2[:, 0]); self.Tv = -no_plano(eixos2[:, 1])
        self._rasterizar()
        self._campos()

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
        for _ in range(self.margem + 2):
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
        self.cheio = cheio; self.bary = np.stack([l0, l1, 1 - l0 - l1], 1).astype(np.float32)
        print(f"    atlas {self.saida}px{'' if self.amostras == 1 else f' (desenhado em {L})'}: {self.carta.max() + 1} cartas, "
              f"{self.escala / self.amostras / 1000:.2f} px/mm, {len(cheio) / L / L:.0%} do quadrado usado")

    def _campo(self, quais, raio):
        """A distância (px) de cada texel à aresta mais perto entre ``quais`` (T×3) — só às da carta dele — e qual é:
        (dist, triângulo, aresta local), no atlas inteiro. Fora do ``raio``, infinito e −1."""
        L = self.tamanho; ts, ks = np.nonzero(quais)
        dist = np.full(L * L, np.inf, np.float32); eid = np.full(L * L, -1, np.int32)
        if not len(ts): return dist, eid, ts, ks
        A = self.px[ts, ks]; B = self.px[ts, (ks + 1) % 3]; AB = B - A; comp2 = (AB * AB).sum(1) + 1e-12
        carta_e = self.carta[ts]; carta_tex = self._carta_tex

        def relaxar(alvo, ed):
            ok = carta_tex[alvo] == carta_e[ed]; alvo, ed = alvo[ok], ed[ok]
            x = alvo % L + 0.5; y = alvo // L + 0.5
            t = np.clip(((x - A[ed, 0]) * AB[ed, 0] + (y - A[ed, 1]) * AB[ed, 1]) / comp2[ed], 0, 1)
            d = np.hypot(x - A[ed, 0] - t * AB[ed, 0], y - A[ed, 1] - t * AB[ed, 1]).astype(np.float32)
            m = (d < dist[alvo] - 1e-3) & (d <= raio); alvo, ed, d = alvo[m], ed[m], d[m]
            o = np.argsort(d, kind='stable'); alvo, ed, d = alvo[o], ed[o], d[o]
            alvo, primeiro = np.unique(alvo, return_index=True)                 # de cada texel, a candidata mais perto
            dist[alvo] = d[primeiro]; eid[alvo] = ed[primeiro]
            return alvo

        def vizinhos(flat, dy, dx):
            x = flat % L + dx; y = flat // L + dy; ok = (x >= 0) & (x < L) & (y >= 0) & (y < L)
            return (y * L + x)[ok], ok

        # as sementes: pontos de meio em meio texel ao longo de cada aresta, nos nove texels em volta
        n = np.maximum(2, np.ceil(np.sqrt(comp2) / 0.5).astype(int) + 1)
        e = np.repeat(np.arange(len(ts)), n); u = (np.arange(n.sum()) - np.repeat(np.cumsum(n) - n, n)) / np.repeat(n - 1, n)
        pt = A[e] + AB[e] * u[:, None]; base = np.floor(pt[:, 1]).astype(np.int64) * L + np.floor(pt[:, 0]).astype(np.int64)
        dentro = (pt[:, 0] >= 0) & (pt[:, 0] < L) & (pt[:, 1] >= 0) & (pt[:, 1] < L); base, e = base[dentro], e[dentro]
        frentes = []
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                alvo, ok = vizinhos(base, dy, dx); frentes.append(relaxar(alvo, e[ok]))
        frente = np.unique(np.concatenate(frentes))
        while len(frente):
            frentes = []
            for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (1, -1), (-1, 1), (-1, -1)):
                alvo, ok = vizinhos(frente, dy, dx); frentes.append(relaxar(alvo, eid[frente[ok]]))
            frente = np.unique(np.concatenate(frentes))
        return dist, eid, ts, ks

    def _campos(self):
        """Por texel cheio: a distância (m) à quina saliente e ao canto fundo mais perto, e para onde a superfície
        continua do outro lado (para arredondar a quina no relevo)."""
        self._carta_tex = np.where(self.tri >= 0, self.carta[np.maximum(self.tri, 0)], -1)
        c = self.cheio
        for nome, quais, raio in (('quina', self.viva & self.convexa, RAIO_QUINA), ('canto', self.viva & ~self.convexa, RAIO_CANTO)):
            dist, eid, ts, ks = self._campo(quais, raio * self.escala)
            d = dist[c] / self.escala; e = eid[c]; tem = e >= 0
            viz = np.zeros((len(c), 3), np.float32)
            if len(ts): viz[tem] = self.viz[ts[e[tem]], ks[e[tem]]]
            setattr(self, 'd_' + nome, np.where(tem, d, 9.0).astype(np.float32)); setattr(self, 'viz_' + nome, viz)
        del self._carta_tex


class Texels:
    """O que o desenho recebe, um valor por texel usado do atlas (ou de um pedaço dele: ``parte`` é uma fatia)."""

    def __init__(self, at, cor_base, parte=slice(None)):
        t = at.tri[at.cheio[parte]]; b = at.bary[parte].astype(float); P = at.P[t]
        self.P = (b[:, :, None] * P).sum(1)                       # posição 3D (m, espaço da arma)
        self.N = at.fn[t]; self.eixo = at.eixo[t]; self.peca = at.peca[t]
        Ns = (b[:, :, None] * at.vn[t]).sum(1); self.Ns = Ns / (np.linalg.norm(Ns, axis=1, keepdims=True) + 1e-20)   # a normal lisa
        uv = (b[:, :, None] * at.uv0[t]).sum(1); self.base, self.mat = cor_base(uv, t)       # a cor de antes (linear) e o material dela
        self.lum = self.base @ np.array([0.2126, 0.7152, 0.0722])
        mx, mn = self.base.max(1), self.base.min(1); self.sat = (mx - mn) / (mx + 1e-6)
        self.quina = at.d_quina[parte].astype(float)              # distância (m) até a quina saliente mais perto
        self.canto = at.d_canto[parte].astype(float)              # e até o canto fundo
        tudo = at.P.reshape(-1, 3); lo, hi = tudo.min(0), tudo.max(0); self.lo, self.hi = lo, hi
        self.u = (self.P[:, 2] - lo[2]) / (hi[2] - lo[2])          # 0 na boca do cano, 1 na coronha
        self.alt = (self.P[:, 1] - lo[1]) / (hi[1] - lo[1])
        ix = np.array([[2, 1], [2, 0], [0, 1]])[self.eixo]           # o plano da face: lado (z, y), cima (z, x), frente (x, y)
        self.plano = np.stack([np.take_along_axis(self.P, ix[:, [0]], 1)[:, 0], np.take_along_axis(self.P, ix[:, [1]], 1)[:, 0]], 1)
        self.lado = self.eixo == 0
        self.n = len(self.P)
        self._tri = t


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


# ---------------------------------------------------------------- a faca (a forma nova: formas_mao.faca)
def _faca(X):
    """As partes da faca e onde cada texel está na lâmina: (máscaras, s de 0 na guarda a 1 na ponta, e de 0 no fio a 1 na
    linha do desbaste, v de 0 na linha do desbaste a 1 no dorso)."""
    m = X.mat; F = dict(face=m == 0, desbaste=m == 1, dorso=m == 2, guarda=m == 3, cabo=m == 4, pomo=m == 5, lamina=m <= 2)
    s = np.clip((X.P[:, 2] - (formas_mao.Z_GUARDA - 0.002)) / (formas_mao.Z_PONTA - formas_mao.Z_GUARDA + 0.002), 0, 1)
    _, dorso, fio, linha, _, _ = formas_mao.lamina_em(s)
    e = np.clip((X.P[:, 1] - fio) / np.maximum(linha - fio, 1e-5), 0, 1); v = np.clip((X.P[:, 1] - linha) / np.maximum(dorso - linha, 1e-5), 0, 1)
    return F, s, e, v


def _lamina(X, F, s, e, v, face, polido, brilho, fio_cor, sulco=True):
    """O que toda lâmina tem (Jhon, 20/11: "mais afiada", "mais brilho na lâmina"): a face com a cor dada e o sulco ao
    comprido; o desbaste polido como espelho, riscado de pedra na direção do fio e mais claro quanto mais perto dele; o
    fio, uma linha de luz que não apaga na sombra. Devolve (cor, emissão, rugosidade, altura) só da lâmina."""
    n = X.n; pedra = fbm(X.P * np.array([1500.0, 60.0, 1700.0]), 2, s=771)          # os riscos da pedra, atravessados ao fio
    canal = suave(0.17, 0.10, np.abs(v - 0.56)) * suave(0.07, 0.11, s) * suave(0.60, 0.54, s) * F['face'] if sulco else np.zeros(n)
    cor = face * (1 - 0.28 * canal)[:, None]
    esp = mix(polido, lin('#ffffff'), (1 - e) ** 1.5 * 0.75) * (0.90 + 0.14 * pedra)[:, None]
    cor = np.where(F['desbaste'][:, None], esp, cor)
    cor = np.where(F['dorso'][:, None], mix(face, polido, 0.35), cor)
    micro = suave(0.10, 0.04, e) * F['desbaste']                                    # o microchanfro: os últimos décimos antes do fio
    gume = np.maximum(quinas(X, 0.0007) * F['lamina'], micro)
    cor = mix(cor, fio_cor, gume * 0.9)
    rug = np.where(F['desbaste'], 0.05 + 0.07 * pedra, 0.26 + 0.12 * fbm(X.P * np.array([1500.0, 1500.0, 30.0]), 2, s=772)) * (1 - 0.5 * gume)
    emis = fio_cor[None, :] * (brilho * (0.55 * gume + 0.07 * F['desbaste'] * (1 - e)))[:, None]
    alt = -0.00045 * canal
    return cor, emis, rug, alt


def _cabo(X, F):
    """Onde o texel está no cabo: (u de 0 na guarda a 1 no pomo, o ângulo em volta dele)."""
    u = np.clip((X.P[:, 2] + 0.0476) / (0.0522 + 0.0476), 0, 1); a = np.arctan2(X.P[:, 1] + 0.0035, X.P[:, 0])
    return u, a


def aco(X):
    """Faca (a de todo mundo): aço acetinado escuro na face, desbaste polido, guarda e pomo de aço escovado, cabo de
    borracha preta recartilhada com dois anéis de aço."""
    F, s, e, v = _faca(X); n = X.n; largo = fbm(X.P * 25, 3, s=702); escova = fbm(X.P * np.array([1500.0, 1500.0, 30.0]), 3, s=701)
    face = mix(lin('#333840'), lin('#5d646f'), 0.25 + 0.45 * escova + 0.3 * largo)
    cor, emis, rug, alt = _lamina(X, F, s, e, v, face, lin('#c3cbd6'), 1.0, lin('#f4f9ff'))
    met = np.ones(n)
    aco_ = mix(lin('#6f7681'), lin('#aab1bb'), 0.3 + 0.5 * fbm(X.P * np.array([1800.0, 60.0, 1800.0]), 2, s=703) + 0.2 * largo)
    ferragem = F['guarda'] | F['pomo']; cor = np.where(ferragem[:, None], aco_, cor); rug = np.where(ferragem, 0.24 + 0.1 * largo, rug)
    u, a = _cabo(X, F); losango = np.abs(np.sin(u * 46.0 + a * 4.0)) * np.abs(np.sin(u * 46.0 - a * 4.0)); grao = suave(0.10, 0.30, losango)
    miolo = suave(0.08, 0.12, u) * suave(0.92, 0.88, u)                              # o recartilhado fica no meio; as pontas são lisas
    borracha = mix(lin('#0d0e11'), lin('#22242a'), 0.35 * largo + 0.65 * grao * miolo)
    anel = ((np.abs(u - 0.055) < 0.012) | (np.abs(u - 0.945) < 0.012)) & F['cabo']
    cor = np.where(F['cabo'][:, None], np.where(anel[:, None], aco_, borracha), cor)
    rug = np.where(F['cabo'], np.where(anel, 0.22, 0.62 + 0.1 * grao), rug); met = np.where(F['cabo'] & ~anel, 0.0, met)
    alt = alt + F['cabo'] * (0.00028 * grao * miolo - 0.0002 * anel)
    furo = F['pomo'] & (np.hypot(X.P[:, 1] + 0.0035, X.P[:, 2] - 0.0572) < 0.0026) & (np.abs(X.N[:, 0]) > 0.5)      # o furo do fiel
    cor = np.where(furo[:, None], lin('#08090b'), cor); alt = alt - 0.0006 * furo; met = np.where(furo, 0.0, met)
    cor = mix(cor, np.clip(cor * 1.8 + 0.03, 0, 1), (quinas(X, 0.0008) * ~F['lamina'] * 0.5))
    return cor, emis, np.clip(rug, 0.04, 1), met, alt


def damasco(X):
    """Faca Damasco (épica): aço damasco em ondas na face, desbaste polido que ainda mostra as camadas; guarda e pomo de
    cobre, cabo de madeira escura envernizada com dois rebites."""
    F, s, e, v = _faca(X); n = X.n
    ondas = np.sin(2 * np.pi * (X.P[:, 1] * 170 + X.P[:, 2] * 45 + 2.4 * fbm(X.P * 70, 4, s=91))); camada = suave(-0.3, 0.6, ondas)
    face = mix(lin('#3f444d'), lin('#c9ced6'), camada)
    cor, emis, rug, alt = _lamina(X, F, s, e, v, face, mix(lin('#aeb5c0'), lin('#e9edf2'), camada), 1.0, lin('#fff6e6'), sulco=False)
    alt = alt + 0.00006 * camada * F['face']; met = np.ones(n)
    cobre = mix(lin('#8a4520'), lin('#e39a5c'), 0.25 + 0.75 * fbm(X.P * np.array([1500.0, 80.0, 1500.0]), 3, s=93))
    ferragem = F['guarda'] | F['pomo']; cor = np.where(ferragem[:, None], cobre, cor); rug = np.where(ferragem, 0.2, rug)
    veio = fbm(X.P * np.array([260.0, 260.0, 22.0]) + 2.0 * fbm(X.P * 30, 2, s=94)[:, None], 4, s=92); faixa = suave(0.35, 0.65, np.abs(np.sin(veio * 20.0)))
    madeira = mix(mix(lin('#3a1d0b'), lin('#6b3a18'), suave(0.3, 0.7, veio)), lin('#1f0f05'), 0.5 * faixa)
    rebite = F['cabo'] & (np.abs(X.N[:, 0]) > 0.55) & ((np.hypot(X.P[:, 1] + 0.003, X.P[:, 2] + 0.022) < 0.0032) | (np.hypot(X.P[:, 1] + 0.003, X.P[:, 2] - 0.026) < 0.0032))
    cor = np.where(F['cabo'][:, None], np.where(rebite[:, None], cobre, madeira), cor)
    rug = np.where(F['cabo'], np.where(rebite, 0.2, 0.3 + 0.2 * faixa), rug); met = np.where(F['cabo'] & ~rebite, 0.0, met)
    alt = alt + F['cabo'] * (-0.00006 * faixa + 0.0003 * rebite)
    cor = mix(cor, lin('#ffe2c2'), quinas(X, 0.0008) * ferragem * 0.5)
    return cor, emis, np.clip(rug, 0.04, 1), met, alt


def obsidiana(X):
    """Faca Obsidiana (lendária): lâmina de vidro vulcânico preto, lascada, com veios roxos acesos; o desbaste é vidro
    roxo translúcido que acende até o fio; guarda e pomo de cromo escuro, cabo preto com anéis acesos."""
    F, s, e, v = _faca(X); n = X.n; roxo = lin('#b54dff')
    F1, F2, ID, _ = worley(X.P / 0.0065, s=101); veio = 1 - suave(0.0, 0.05, F2 - F1); halo = (1 - suave(0.0, 0.16, F2 - F1)) * 0.3
    vidro = mix(lin('#050407'), lin('#1a1424'), fbm(X.P * 120, 3, s=102))
    face = mix(vidro, mix(roxo, lin('#ffffff'), 0.25), veio * 0.9) + roxo * halo[:, None] * 0.4
    cor, emis, rug, alt = _lamina(X, F, s, e, v, face, mix(lin('#3a1466'), lin('#c98bff'), (1 - e) ** 1.2), 1.6, lin('#f0d6ff'), sulco=False)
    rug = np.where(F['lamina'], 0.05, rug); alt = alt + 0.0002 * (ID - 0.5) * F['face'] * (1 - veio)                 # as lascas: cada célula num plano
    emis = emis + roxo[None, :] * ((veio + halo) * F['face'] + 0.35 * F['desbaste'] * (1 - e) ** 2)[:, None]
    met = np.where(F['lamina'], 0.15, 1.0)
    cromo = mix(lin('#1c1824'), lin('#4a4160'), fbm(X.P * 90, 3, s=104)); ferragem = F['guarda'] | F['pomo']
    cor = np.where(ferragem[:, None], cromo, cor); rug = np.where(ferragem, 0.14, rug)
    u, a = _cabo(X, F); anel = (np.abs(np.sin(u * np.pi * 7.0)) < 0.10) & F['cabo'] & (u > 0.08) & (u < 0.92)
    cabo = mix(lin('#0c0b0f'), lin('#241b30'), suave(0.4, 0.6, fbm(X.P * np.array([40.0, 40.0, 400.0]), 3, s=103)))
    cor = np.where(F['cabo'][:, None], np.where(anel[:, None], roxo * 0.8, cabo), cor); rug = np.where(F['cabo'], 0.42, rug); met = np.where(F['cabo'], 0.2, met)
    emis = emis + roxo[None, :] * (anel * 0.7 + quinas(X, 0.0008) * ferragem * 0.25)[:, None]; alt = alt - 0.00025 * anel
    return cor, emis, np.clip(rug, 0.04, 1), met, alt


# ---------------------------------------------------------------- a granada (a forma nova: formas_mao.granada)
def _granada(X):
    """As partes da granada e onde cada texel está na bola: (máscaras, a latitude −1…1, a longitude em radianos)."""
    m = X.mat; G = dict(bola=m == 0, espoleta=m == 1, alavanca=m == 2, pino=m == 3, argola=m == 4, arame=m >= 3)
    d = (X.P - formas_mao.BOLA_C) / formas_mao.BOLA_R; d /= np.linalg.norm(d, axis=1, keepdims=True) + 1e-9
    return G, d[:, 1], np.arctan2(d[:, 2], d[:, 0]), d


def _ferragem(X, G, cor, rug, met, alt, espoleta, alavanca, arame=None):
    """A espoleta torneada (os anéis do torno e o topo), a alavanca e o arame do pino e da argola, por cima da bola."""
    torno = 0.5 + 0.5 * np.sin(X.P[:, 1] * 2 * np.pi / 0.0011); escova = fbm(X.P * np.array([60.0, 1500.0, 1500.0]), 2, s=781)
    e_ = np.asarray(espoleta); a_ = np.asarray(alavanca); w_ = lin('#c9ced6') if arame is None else np.asarray(arame)
    cor = np.where(G['espoleta'][:, None], e_ * (0.82 + 0.26 * torno)[:, None] if e_.ndim == 1 else e_, cor)
    cor = np.where(G['alavanca'][:, None], a_ * (0.88 + 0.2 * escova)[:, None] if a_.ndim == 1 else a_, cor)
    cor = np.where(G['arame'][:, None], w_ * (0.85 + 0.25 * escova)[:, None], cor)
    alt = alt + 0.00005 * torno * G['espoleta']
    return cor, rug, met, alt


def granada(X):
    """Granada (a de todo mundo): aço pintado de verde-oliva, com a casca de laranja da tinta, a costura do meio, a faixa
    amarela e as letras de estêncil; a tinta lascada mostra o aço; espoleta de zinco torneado, alavanca verde, arame claro."""
    G, lat, lon, d = _granada(X); n = X.n; largo = fbm(X.P * 40, 3, s=721); fino = fbm(X.P * 1100, 2, s=722)
    oliva = mix(lin('#2f3b20'), lin('#4a5c31'), 0.25 + 0.5 * largo) * (0.90 + 0.18 * lat)[:, None]
    faixa = suave(0.40, 0.43, lat) * suave(0.60, 0.57, lat)
    cor = mix(oliva, lin('#d7a614'), faixa * 0.95)
    # o estêncil: duas linhas de "letras" (blocos) de um lado e um losango do outro
    col = np.floor(lon * 34.0); lin_ = np.floor(lat * 22.0); letra = (_h(col, lin_, s=723) > 0.30) & (np.abs((lon * 34.0) % 1 - 0.5) < 0.34) & (np.abs((lat * 22.0) % 1 - 0.5) < 0.28)
    letra = letra & (lat > 0.10) & (lat < 0.28) & (np.abs(lon + 1.6) < 0.62)
    cor = np.where((letra & G['bola'])[:, None], mix(oliva, lin('#c9a23a'), 0.8), cor)
    costura = suave(0.035, 0.012, np.abs(lat + 0.02))
    lasca = suave(0.70, 0.76, fbm(X.P * 85, 3, s=724)) * (0.4 + 0.6 * suave(0.3, 0.7, fbm(X.P * 20, 2, s=725)))            # a tinta lascada
    lasca = np.maximum(lasca, quinas(X, 0.0012) * suave(0.35, 0.6, fbm(X.P * 60, 2, s=726))) * ~G['arame']
    rug = 0.48 + 0.12 * fino; met = np.zeros(n); alt = 0.00007 * fino + 0.0004 * costura * G['bola']
    cor, rug, met, alt = _ferragem(X, G, cor, rug, met, alt, lin('#8d939b'), mix(lin('#36431f'), lin('#4e6032'), largo))
    aco_ = mix(lin('#7b828c'), lin('#b9c0c9'), fbm(X.P * 300, 2, s=727))
    cor = mix(cor, aco_, lasca * (G['bola'] | G['alavanca']) * 0.9)
    metal = G['espoleta'] | G['arame'] | (lasca > 0.5)
    rug = np.where(G['espoleta'], 0.3, np.where(G['arame'], 0.2, rug - 0.15 * lasca)); met = np.where(metal, 1.0, 0.0)
    alt = alt - 0.00005 * lasca
    return cor, np.zeros((n, 3)), np.clip(rug, 0.04, 1), met, alt


def granada_carbono(X):
    """Granada Carbono (rara): casca de fibra de carbono em sarja sob verniz, faixa vermelha; espoleta anodizada preta,
    alavanca anodizada vermelha, arame escuro."""
    G, lat, lon, d = _granada(X); n = X.n
    a, b = lon * 22.0, lat * 26.0; trama = ((np.floor(a) + np.floor(b)) % 4 < 2); fio = np.where(trama, np.abs(np.sin(a * np.pi)), np.abs(np.sin(b * np.pi)))
    fibra = mix(lin('#0b0c0f'), lin('#30343c'), 0.25 + 0.75 * fio ** 0.6) * (0.9 + 0.2 * lat)[:, None]
    faixa = suave(0.40, 0.43, lat) * suave(0.60, 0.57, lat); vermelho = lin('#c2261a')
    cor = mix(fibra, vermelho, faixa); rug = np.full(n, 0.16); met = np.full(n, 0.35); alt = 0.00004 * fio * (1 - faixa) * G['bola']
    cor, rug, met, alt = _ferragem(X, G, cor, rug, met, alt, lin('#15161a'), mix(lin('#8f1810'), lin('#e0382a'), fbm(X.P * 30, 2, s=731)), lin('#3a3d44'))
    liso = G['espoleta'] | G['alavanca'] | G['arame']; rug = np.where(liso, 0.26, rug); met = np.where(liso, 1.0, met)
    q_ = quinas(X, 0.0008) * liso; cor = mix(cor, np.clip(cor * 2.0 + 0.05, 0, 1), q_ * 0.6)
    emis = vermelho[None, :] * (0.25 * faixa * G['bola'])[:, None]
    return cor, emis, rug, met, alt


def granada_neon(X):
    """Granada Neon (épica): casca preta fosca em placas hexagonais, as juntas e a faixa do meio acesas em ciano; um anel
    aceso na espoleta e um filete ao comprido da alavanca."""
    G, lat, lon, d = _granada(X); n = X.n; ciano = lin('#3fe6ff')
    F1, F2, ID, _ = worley(d * 3.4, s=741); junta = 1 - suave(0.02, 0.07, F2 - F1); halo = 1 - suave(0.05, 0.3, F2 - F1)
    placa = mix(lin('#0c0e12'), lin('#1f232b'), ID) * (0.9 + 0.2 * lat)[:, None]
    cinta = suave(0.075, 0.05, np.abs(lat + 0.02)); miolo = suave(0.045, 0.03, np.abs(lat + 0.02))
    cor = mix(placa, lin('#07080a'), np.maximum(junta, cinta)); acende = np.maximum(junta * 0.8, miolo) * G['bola']
    rug = np.where(junta > 0.5, 0.3, 0.55); met = np.full(n, 0.3); alt = (-0.0003 * junta - 0.0003 * cinta + 0.0002 * miolo) * G['bola']
    cor, rug, met, alt = _ferragem(X, G, cor, rug, met, alt, lin('#23262d'), lin('#1a1d23'), lin('#aeb6c2'))
    anel = G['espoleta'] & (np.abs(X.P[:, 1] - 0.0250) < 0.0016); filete = G['alavanca'] & (np.abs(X.P[:, 2]) < 0.0016) & (X.P[:, 1] < 0.03)
    acende = np.maximum(acende, np.maximum(anel, filete) * 1.0); liso = G['espoleta'] | G['alavanca'] | G['arame']
    rug = np.where(liso, 0.3, rug); met = np.where(liso, 1.0, met)
    cor = mix(cor, mix(ciano, lin('#ffffff'), 0.4), np.clip(acende, 0, 1))
    emis = ciano[None, :] * (acende + 0.12 * halo * G['bola'])[:, None]
    return cor, emis, rug, met, alt


def granada_magma(X):
    """Granada Magma (épica): placas de basalto com a lava acesa nas rachaduras; a ferragem é ferro queimado."""
    G, lat, lon, d = _granada(X); n = X.n
    F1, F2, ID, _ = worley(X.P / 0.013 + 0.35 * fbm(X.P * 90, 3, s=751)[:, None], s=752); g = F2 - F1
    racha = 1 - suave(0.03, 0.13, g); quente = 1 - suave(0.0, 0.045, g); halo = 1 - suave(0.05, 0.32, g); grao = fbm(X.P * 700, 3, s=753)
    rocha = mix(lin('#0e0a09'), lin('#3a2c26'), 0.2 + 0.5 * ID + 0.3 * (grao - 0.5)) * (0.9 + 0.2 * lat)[:, None]
    pulso = suave(0.25, 0.7, fbm(X.P * 30, 3, s=754))
    fogo = mix(mix(lin('#7a0f02'), lin('#ff5a10'), suave(0.0, 0.6, quente + 0.4 * racha)), lin('#ffe27a'), quente)
    so = G['bola']; cor = mix(rocha, fogo, racha * (0.55 + 0.45 * pulso) * so)
    rug = np.where(racha > 0.5, 0.35, 0.8); met = np.full(n, 0.05); alt = (0.0005 * suave(0.0, 0.16, g) + 0.00005 * grao) * so
    ferro = mix(lin('#1c1512'), lin('#4a372c'), fbm(X.P * 200, 3, s=755))
    cor, rug, met, alt = _ferragem(X, G, cor, rug, met, alt, ferro, ferro * 1.2, lin('#6b4a30'))
    liso = G['espoleta'] | G['alavanca'] | G['arame']; rug = np.where(liso, 0.4, rug); met = np.where(liso, 0.9, met)
    brasa = (G['espoleta'] & (np.abs(X.P[:, 1] - 0.0250) < 0.0012)) | (quinas(X, 0.0008) * G['alavanca'] > 0.5)
    emis = fogo * (racha * (0.4 + 0.6 * pulso) * so)[:, None] + lin('#ff3a08')[None, :] * (halo * 0.14 * pulso * so + 0.5 * brasa)[:, None]
    return cor, emis, rug, met, alt


def granada_ouro(X):
    """Granada Ouro (lendária): ouro polido com uma treliça de losangos gravada e frisos lisos; espoleta de ônix com anel
    de ouro, alavanca de ouro branco, arame dourado."""
    G, lat, lon, d = _granada(X); n = X.n; largo = fbm(X.P * 30, 3, s=761)
    t = np.clip(0.5 + 0.38 * lat + 0.3 * (largo - 0.5), 0, 1)
    au = mix(mix(lin('#8a4a08'), lin('#e09a18'), suave(0.0, 0.5, t)), lin('#ffe58a'), suave(0.5, 1.0, t))
    zona = suave(0.06, 0.10, np.abs(lat + 0.02)) * suave(0.80, 0.74, np.abs(lat + 0.02))               # a gravação fica entre o friso do meio e os polos
    tr = np.minimum(np.abs(np.sin(lon * 9.0 + lat * 7.0)), np.abs(np.sin(lon * 9.0 - lat * 7.0))); traco = suave(0.14, 0.06, tr) * zona
    flor = suave(0.90, 0.97, np.abs(np.sin(lon * 9.0 + lat * 7.0)) * np.abs(np.sin(lon * 9.0 - lat * 7.0))) * zona            # um ponto no meio de cada losango
    friso = suave(0.06, 0.045, np.abs(lat + 0.02)) + suave(0.045, 0.03, np.abs(np.abs(lat + 0.02) - 0.80))
    cor = mix(au, lin('#4a2804'), np.clip(traco + flor, 0, 1) * 0.85); cor = mix(cor, lin('#fff3b0'), np.clip(friso, 0, 1) * 0.5)
    rug = 0.13 + 0.3 * traco * G['bola']; met = np.ones(n); alt = (-0.0002 * traco - 0.00015 * flor + 0.00025 * np.clip(friso, 0, 1)) * G['bola']
    branco = mix(lin('#cfc7b0'), lin('#fff8e0'), 0.4 + 0.6 * largo)
    cor, rug, met, alt = _ferragem(X, G, cor, rug, met, alt, lin('#0b0b0f'), branco, lin('#e8b93a'))
    anel = G['espoleta'] & ((np.abs(X.P[:, 1] - 0.0250) < 0.0018) | (X.P[:, 1] > 0.0336) | (X.P[:, 1] < 0.0136))
    cor = np.where(anel[:, None], au, cor); onix = G['espoleta'] & ~anel
    rug = np.where(onix, 0.08, np.where(G['alavanca'] | G['arame'] | anel, 0.16, rug)); met = np.where(onix, 0.25, 1.0)
    q_ = quinas(X, 0.0008) * ~onix; cor = mix(cor, lin('#fff6c8'), q_ * 0.6)
    emis = cor * (0.10 * ~onix)[:, None]
    return cor, emis, rug, met, alt


# ---------------------------------------------------------------- o acabamento de fábrica (as armas sem pele)
# De que material é cada cor da paleta (reais.py; o AR-15 tem a paleta dele): o mais parecido desta lista.
MATERIAIS = dict(
    polimero=(0.016, 0.016, 0.018), oxidado=(0.023, 0.024, 0.026), aco=(0.045, 0.047, 0.05), aco_medio=(0.11, 0.115, 0.12), aco_claro=(0.30, 0.31, 0.33),
    madeira=(0.30, 0.125, 0.04), madeira_escura=(0.17, 0.065, 0.022), baquelite=(0.22, 0.05, 0.02), areia=(0.36, 0.26, 0.15), areia_escura=(0.25, 0.18, 0.10),
    latao=(0.55, 0.38, 0.12), cobre=(0.45, 0.2, 0.09), espoleta=(0.35, 0.3, 0.2), lente=(0.06, 0.16, 0.30), oliva=(0.075, 0.095, 0.055),
)
_NOMES_MAT = list(MATERIAIS)


def material(X):
    """O material de cada texel (índice em ``MATERIAIS``): a cor da lista mais parecida com a que a paleta dava."""
    ref = np.log(np.array([MATERIAIS[n] for n in _NOMES_MAT]) + 0.004); c = np.log(X.base + 0.004)
    return ((c[:, None, :] - ref[None, :, :]) ** 2).sum(2).argmin(1)


def desgaste(X, larg=0.0011, s=0):
    """A tinta gasta na quina saliente: 1 na quina, 0 a ``larg`` dela, roída por um ruído (não é uma linha de régua)."""
    r = fbm(X.P * 260, 3, s=900 + s)
    return (1 - suave(0.0, larg * (0.35 + 0.9 * r), X.quina)) * suave(0.50, 0.72, fbm(X.P * 42, 3, s=901 + s))


def fabrica(X):
    """A arma como sai da fábrica, no desenho dos jogos de referência (Jhon, 20/11: "parecidas com as do Overwatch e do
    Valorant, com mais qualidade e detalhes"): cada material com a cor, o brilho e o grão dele — o polímero fosco e
    pontilhado, o aço oxidado azulado, o aço escovado ao comprido, a madeira com veio, a baquelite lustrosa —, a luz de
    cima, a quina gasta até o metal e o canto fundo mais escuro."""
    m = material(X); M = {n: m == k for k, n in enumerate(_NOMES_MAT)}; n = X.n
    cor = np.zeros((n, 3)); rug = np.full(n, 0.5); met = np.zeros(n); alt = np.zeros(n); gasto = np.tile(lin('#9aa3ad'), (n, 1)); forca = np.full(n, 0.5)
    largo = fbm(X.P * 22, 3, s=801)                                       # a variação larga de tom (um palmo)
    fino = fbm(X.P * 900, 2, s=802)                                       # o grão (1 mm)
    escova = fbm(X.P * np.array([1400.0, 1400.0, 26.0]), 3, s=803)        # riscos ao comprido do cano

    def por(sel, c, r, me, a=0.0, g=None, f=None):
        cor[sel] = c if np.ndim(c) == 1 else c[sel]; rug[sel] = r if np.ndim(r) == 0 else r[sel]; met[sel] = me
        alt[sel] = a if np.ndim(a) == 0 else a[sel]
        if g is not None: gasto[sel] = g
        if f is not None: forca[sel] = f

    por(M['polimero'], mix(lin('#17181c'), lin('#24262c'), largo), 0.58 + 0.1 * fino, 0.0, 0.00004 * fino, lin('#4a4d55'), 0.35)
    ox = mix(lin('#1d2027'), lin('#343945'), 0.25 + 0.5 * largo + 0.2 * (escova - 0.5))
    por(M['oxidado'], ox, 0.36 + 0.14 * escova, 0.85, 0.000012 * escova, lin('#aab3be'), 0.75)
    aco = mix(lin('#2c3038'), lin('#4c525e'), 0.2 + 0.5 * largo + 0.4 * (escova - 0.5))
    por(M['aco'], aco, 0.32 + 0.16 * escova, 0.95, 0.000014 * escova, lin('#c2c9d2'), 0.7)
    por(M['aco_medio'], mix(lin('#5b616c'), lin('#8a919d'), 0.35 + 0.4 * escova + 0.25 * largo), 0.3 + 0.14 * escova, 1.0, 0.000014 * escova, lin('#dfe4ea'), 0.5)
    por(M['aco_claro'], mix(lin('#8f96a1'), lin('#d6dbe2'), 0.35 + 0.35 * escova + 0.3 * largo), 0.22 + 0.16 * escova, 1.0, 0.000016 * escova, lin('#ffffff'), 0.4)
    # a madeira: o veio corre ao comprido, com nós largos; verniz
    veio = fbm(X.P * np.array([240.0, 240.0, 9.0]) + 2.5 * fbm(X.P * 14, 2, s=805)[:, None], 4, s=804)
    faixa = suave(0.35, 0.65, np.abs(np.sin(veio * 22.0)))
    por(M['madeira'], mix(mix(lin('#8a4a1c'), lin('#b8702e'), largo), lin('#5a2c10'), 0.55 * faixa), 0.34 + 0.2 * faixa, 0.0, -0.00005 * faixa, lin('#d9a066'), 0.45)
    por(M['madeira_escura'], mix(mix(lin('#5e3013'), lin('#7c4520'), largo), lin('#3a1c0a'), 0.55 * faixa), 0.36 + 0.2 * faixa, 0.0, -0.00005 * faixa, lin('#b98452'), 0.4)
    nuvem = fbm(X.P * 70 + 3.0 * fbm(X.P * 30, 2, s=807)[:, None], 3, s=806)
    por(M['baquelite'], mix(lin('#6b1d0c'), lin('#a8421a'), suave(0.3, 0.7, nuvem)), 0.2 + 0.1 * nuvem, 0.0, 0.0, lin('#d98a55'), 0.35)
    por(M['areia'], mix(lin('#a8875a'), lin('#c9a777'), 0.2 + 0.6 * largo), 0.5 + 0.12 * fino, 0.15, 0.00003 * fino, lin('#e9d6b4'), 0.5)
    por(M['areia_escura'], mix(lin('#8a6c45'), lin('#a98a5e'), 0.2 + 0.6 * largo), 0.5 + 0.12 * fino, 0.15, 0.00003 * fino, lin('#dcc7a2'), 0.5)
    por(M['latao'], mix(lin('#b88a2e'), lin('#f0cc72'), escova), 0.24, 1.0, 0.0, lin('#fff0c0'), 0.4)
    por(M['cobre'], mix(lin('#a8562c'), lin('#e09060'), escova), 0.26, 1.0, 0.0, lin('#ffd2b0'), 0.4)
    por(M['espoleta'], mix(lin('#8f7a52'), lin('#c9b488'), escova), 0.3, 1.0, 0.0, lin('#f5e6c6'), 0.3)
    por(M['lente'], mix(lin('#0a1830'), lin('#2f6fb0'), suave(0.2, 0.9, X.alt)), 0.05, 0.6, 0.0, lin('#9fd0ff'), 0.2)
    por(M['oliva'], mix(lin('#3a4a2a'), lin('#56683c'), largo), 0.5 + 0.1 * fino, 0.1, 0.00003 * fino, lin('#a9b58c'), 0.5)
    # a luz de cima pintada na cor (o que olha para o céu é mais claro, a barriga mais escura) e um degradê pela altura
    cor = cor * (0.90 + 0.20 * X.Ns[:, 1] + 0.10 * (X.alt - 0.5))[:, None]
    # manchas largas de uso: mais lustroso onde a mão pega, um pouco mais claro
    uso = suave(0.55, 0.8, fbm(X.P * 34, 3, s=810)); rug = rug - 0.08 * uso; cor = cor * (1 + 0.10 * uso)[:, None]
    # riscos finos, poucos, na diagonal (só no metal e na tinta)
    risco = fbm(X.P @ np.array([[900.0, 30.0, 0.0], [-30.0, 700.0, 60.0], [0.0, -60.0, 24.0]]), 2, s=811)
    risco = suave(0.84, 0.90, risco) * (met > 0.1) * 0.22 * suave(0.45, 0.7, fbm(X.P * 30, 2, s=812))
    d = np.maximum(desgaste(X) * forca, risco * forca)
    cor = mix(cor, gasto, d * 0.7); rug = rug - 0.1 * d; met = np.maximum(met, 0.9 * d * (met > 0.5))
    fio = quinas(X, 0.0009)                                               # o fio de luz na quina: a cor do próprio material, mais clara
    claro = np.clip(cor * 1.7 + 0.02, 0, 1)
    cor = mix(cor, claro, fio * 0.5)
    return cor, np.zeros((n, 3)), np.clip(rug, 0.04, 1), met, alt


# ---------------------------------------------------------------- as peles do AR-15
def _ar15(X):
    """As partes do AR-15 pela cor da paleta dele: (o corpo bege, as peças pequenas de metal claro, os cartuchos, o resto escuro)."""
    carregador = X.peca == 2
    corpo = ~carregador & (X.sat >= 0.2); metal = ~carregador & ~corpo & (X.lum > 0.07); latao = carregador & (X.sat >= 0.2)
    return corpo, metal, latao, ~(corpo | metal | latao)


def _filigrana(X, celula=0.026, s=31):
    """Arabescos (uma espiral por célula, ramos entre elas) nas faces de lado e de cima: (o traço 0..1, a distância ao traço)."""
    q = X.plano / celula; F1, F2, ID, C = worley2(q, s=s)
    v = q - C; r = np.linalg.norm(v, axis=1); th = np.arctan2(v[:, 1], v[:, 0]) * np.where(ID > 0.5, 1, -1)
    esp = np.abs(np.sin(r * 2 * np.pi * 2.0 - th)); esp = np.where(r < 0.44, esp, 1.0)
    folha = np.where(np.abs(r - 0.45) < 0.05, np.abs(np.sin(th * 3 + ID * 6)) * 1.4, 1.0)
    ramo = (F2 - F1) * 6.0
    d = np.minimum(np.minimum(esp, folha), ramo)                          # 0 no meio do traço
    return 1 - suave(0.16, 0.30, d), d


def ouro(X):
    """AR-15 Ouro (lendária): ouro polido de joalheria no corpo, com arabescos finos gravados no meio de cada face e
    a moldura lisa em volta; o que era preto vira ônix lustroso com friso de ouro na quina; as peças pequenas, ouro
    branco. (Era ouro por igual, com a quina larga e clara: de perto parecia bloco amarelo — Jhon, 20/11.)"""
    corpo, metal, latao, escuro = _ar15(X); n = X.n
    largo = fbm(X.P * 18, 3, s=611); escova = fbm(X.P * np.array([1200.0, 1200.0, 20.0]), 3, s=612)
    # o ouro: do âmbar fundo ao amarelo claro, pela luz de cima e por uma variação larga (não é uma cor só)
    t = np.clip(0.45 + 0.38 * X.Ns[:, 1] + 0.30 * (largo - 0.5) + 0.10 * (escova - 0.5) + 0.18 * (X.alt - 0.5), 0, 1)
    au = mix(mix(lin('#8a4a08'), lin('#e09a18'), suave(0.0, 0.5, t)), lin('#ffe58a'), suave(0.5, 1.0, t))
    traco, d = _filigrana(X, 0.017)
    miolo = suave(0.0016, 0.0032, np.minimum(X.quina, X.canto)) * (X.eixo != 2) * corpo     # a gravação fica no meio da face; a beira é moldura lisa
    traco = traco * miolo
    fundo = suave(0.5, 0.2, fbm(X.P * 420, 2, s=613)) * miolo * (1 - traco)             # o fundo da gravação, pontilhado fosco
    cor = mix(au, au * 0.80, fundo * 0.6)
    cor = mix(cor, lin('#4a2804'), traco * 0.85)                          # o sulco escuro
    borda = (suave(0.30, 0.36, d) - suave(0.36, 0.48, d)) * miolo
    cor = mix(cor, lin('#fff3b0'), borda * 0.6)                           # a borda polida do sulco
    moldura = (suave(0.0012, 0.0016, np.minimum(X.quina, X.canto)) - suave(0.0016, 0.0022, np.minimum(X.quina, X.canto))) * (X.eixo != 2) * corpo
    cor = mix(cor, lin('#5a3306'), moldura * 0.7)                         # o filete que fecha a moldura
    onix = mix(lin('#07070a'), lin('#1a1922'), largo) * (0.9 + 0.25 * X.Ns[:, 1])[:, None]
    cor = np.where(escuro[:, None], onix, cor)
    branco = mix(lin('#cfc7b0'), lin('#fff8e0'), 0.4 + 0.3 * escova + 0.3 * largo)
    cor = np.where(metal[:, None], branco, cor)
    cor = np.where(latao[:, None], mix(lin('#b88a2e'), lin('#f0cc72'), escova), cor)
    q_ = quinas(X, 0.0008); friso = quinas(X, 0.0013) * escuro
    cor = mix(cor, lin('#f2c14a'), friso * 0.9)                           # o friso de ouro no ônix
    cor = mix(cor, lin('#fff6c8'), q_ * 0.6)
    eh_ouro = ~escuro | (friso > 0.4)
    rug = np.where(eh_ouro, 0.14 + 0.08 * escova + 0.3 * traco + 0.22 * fundo, 0.08 + 0.06 * largo)
    met = np.where(eh_ouro, 1.0, 0.25)
    emis = cor * (0.10 * eh_ouro)[:, None] + lin('#ffd060')[None, :] * (q_ * 0.10 * eh_ouro)[:, None]     # o ouro não apaga na sombra
    alt = -0.00020 * traco - 0.00004 * fundo + 0.000008 * escova * eh_ouro
    return cor, emis, rug, met, alt


def diamante(X):
    """AR-15 Diamante (lendária): cristal lapidado — facetas grandes, cada uma virada um pouco para um lado (o reflexo
    muda de faceta em faceta) e de um tom de gelo diferente, a aresta entre elas fina e clara, um fogo de cores fraco
    dentro; o que era preto é safira lapidada do mesmo jeito."""
    corpo, metal, latao, escuro = _ar15(X); n = X.n
    cel = 0.021
    F1, F2, ID, C = worley(X.P / cel, s=621)
    aresta = 1 - suave(0.010, 0.045, F2 - F1)
    # a faceta: um plano inclinado sorteado (o relevo) e um tom
    ang = ID * 2 * np.pi * 7.0; incl = np.stack([np.cos(ang), np.sin(ang * 1.7), np.sin(ang)], 1)
    rampa = ((X.P / cel - C) * incl).sum(1)                                # −1..1 dentro da faceta
    tom = _h(np.floor(ID * 4099), s=624)
    gelo = mix(mix(lin('#5fb4ee'), lin('#bfe6ff'), suave(0.0, 0.5, tom)), lin('#ffffff'), suave(0.45, 1.0, tom))
    gelo = mix(gelo, lin('#cdbcff'), suave(0.8, 0.95, _h(np.floor(ID * 997), s=622)) * 0.5)
    gelo = gelo * (0.84 + 0.26 * rampa)[:, None]
    # o fogo: um arco-íris fraco que muda com a faceta e com o lado para onde ela olha
    fase = ID * 6.0 + X.Ns[:, 0] * 2.0 + X.Ns[:, 1] * 1.3 + rampa * 0.8
    fogo = 0.5 + 0.5 * np.stack([np.cos(fase * 2 * np.pi), np.cos(fase * 2 * np.pi + 2.094), np.cos(fase * 2 * np.pi + 4.188)], 1)
    cor = mix(gelo, fogo, 0.10)
    safira = mix(mix(lin('#04102c'), lin('#0f3f9c'), suave(0.0, 0.6, tom)), lin('#3f8cf0'), suave(0.6, 1.0, tom)) * (0.8 + 0.35 * rampa)[:, None]
    cor = np.where(escuro[:, None], mix(safira, fogo, 0.03), cor)
    cor = np.where(metal[:, None], mix(lin('#dfe8f2'), lin('#ffffff'), tom), cor)
    cor = np.where(latao[:, None], mix(lin('#9fc4e8'), lin('#e8f4ff'), tom), cor)
    cor = mix(cor, np.where(escuro[:, None], lin('#9fd0ff'), lin('#ffffff')), aresta * 0.6)
    q_ = quinas(X, 0.0008); cor = mix(cor, lin('#ffffff'), q_ * 0.7)
    # as cintilações: uma estrela de quatro pontas em algumas facetas
    S1, _, SID, SC = worley(X.P / 0.008, s=623)
    v = X.P / 0.008 - SC; e1 = np.abs(v @ np.array([0.0, 0.7071, 0.7071])); e2 = np.abs(v @ np.array([0.7071, 0.5, -0.5]))
    estrela = np.maximum(1 - suave(0.0, 0.07, np.minimum(e1, e2)) , 1 - suave(0.04, 0.16, S1)) * (1 - suave(0.12, 0.42, S1))
    cint = (SID > 0.93) * estrela
    cor = mix(cor, lin('#ffffff'), cint)
    emis = lin('#dff3ff')[None, :] * (0.16 * aresta * ~escuro + 0.9 * cint + 0.25 * q_)[:, None] + lin('#3f8cff')[None, :] * (0.25 * aresta * escuro)[:, None]
    rug = np.where(aresta > 0.5, 0.2, 0.04 + 0.05 * tom)
    alt = 0.0006 * rampa * (1 - aresta)                                    # a faceta inclinada
    return cor, emis, rug, np.full(n, 0.6), alt


def lava(X):
    """AR-15 Lava (épica): placas de basalto com a lava acesa nas rachaduras (amarela no meio, laranja, vermelho na beira)."""
    corpo, metal, latao, escuro = _ar15(X); n = X.n
    F1, F2, ID, _ = worley(X.P / 0.023 + 0.35 * fbm(X.P * 60, 3, s=631)[:, None], s=632)
    g = F2 - F1
    racha = 1 - suave(0.02, 0.11, g); quente = 1 - suave(0.0, 0.035, g); halo = 1 - suave(0.05, 0.3, g)
    grao = fbm(X.P * 500, 3, s=633)
    rocha = mix(lin('#0e0a09'), lin('#3a2c26'), 0.2 + 0.5 * ID + 0.3 * (grao - 0.5)) * (0.9 + 0.2 * X.Ns[:, 1])[:, None]
    rocha = np.where(metal[:, None], mix(lin('#3b2410'), lin('#9a6a2c'), ID), rocha)
    pulso = suave(0.3, 0.75, fbm(X.P * 14, 3, s=634))                      # a lava não acende por igual
    fogo = mix(mix(lin('#7a0f02'), lin('#ff5a10'), suave(0.0, 0.6, quente + 0.4 * racha)), lin('#ffe27a'), quente)
    cor = mix(rocha, fogo, racha * (0.55 + 0.45 * pulso))
    cor = cor + lin('#ff3a08')[None, :] * (halo * 0.10 * pulso)[:, None]
    q_ = quinas(X, 0.0008); cor = mix(cor, lin('#5a463c'), q_ * 0.5 * (1 - racha))
    emis = fogo * (racha * (0.35 + 0.65 * pulso))[:, None] + lin('#ff3a08')[None, :] * (halo * 0.12 * pulso)[:, None]
    alt = 0.00045 * suave(0.0, 0.16, g) + 0.00004 * grao                   # a placa mais alta que a rachadura
    return cor, emis, np.where(racha > 0.5, 0.35, 0.78 + 0.12 * grao), np.full(n, 0.05), alt


def pixel(X):
    """AR-15 8 Bit (épica): blocos de 1,3 cm, a luz em três degraus e contorno preto — guarda-mão, coronha e carregador
    laranja, punho creme, caixa e cano pretos, as peças pequenas amarelas."""
    corpo, metal, latao, escuro = _ar15(X); n = X.n
    y, z, x = X.P[:, 2] * 100, X.P[:, 1] * 100, -X.P[:, 0] * 100
    tam = 1.3; qy, qz, qx = np.floor(y / tam), np.floor(z / tam), np.floor(x / tam)
    sorteio = _h(qy, qz, qx, s=641)
    tons = dict(laranja=('#8f3410', '#d4501c', '#f0702e'), creme=('#b8730f', '#f0a92e', '#ffd06b'), preto=('#141414', '#2a2a2a', '#454545'), amarelo=('#a88600', '#f2cf1d', '#fff27a'))
    silenciador = y < -32; guarda = (y >= -32) & (y < -6); coronha = y >= 13; punho = (y >= 0) & (y < 13) & (z < -3.5)
    laranjas = (guarda | coronha | latao) & ~metal; cremes = punho & ~metal & ~latao
    degrau = np.where(X.N[:, 1] > 0.3, 2, np.where(X.N[:, 1] < -0.3, 0, 1)); degrau = np.clip(degrau + (sorteio > 0.88) - (sorteio < 0.06), 0, 2)
    cor = np.zeros((n, 3))
    for k in range(3):
        sel = degrau == k
        cor[sel] = lin(tons['preto'][k]); cor[sel & laranjas] = lin(tons['laranja'][k]); cor[sel & cremes] = lin(tons['creme'][k])
        cor[sel & metal & ~silenciador] = lin(tons['amarelo'][k])
    cor[laranjas & (X.N[:, 1] > 0.6) & (np.mod(qy, 4) == 0)] = lin('#ffb27a')
    contorno = (X.quina < 0.0016) | (X.canto < 0.0010)
    cor = np.where(contorno[:, None], lin('#050505'), cor)
    return cor, np.zeros((n, 3)), np.full(n, 0.9), np.zeros(n)


FLAMES = [
    (-45.0, 1.15, 3.9, 1.4, 1.5, 0.2), (-39.5, 1.15, 3.3, 1.25, 1.2, 1.4), (-34.0, 1.15, 3.7, 1.35, 1.4, 2.3),    # silenciador
    (-27.0, -0.6, 6.3, 2.1, 2.5, 0.6), (-20.5, -0.6, 5.4, 1.8, 2.0, 1.9), (-14.0, -0.6, 6.2, 2.0, 2.4, 0.9),      # guarda-mão
    (-5.5, -4.3, 12.4, 3.9, 5.6, 0.9), (3.0, -4.3, 10.0, 3.2, 4.4, 2.1), (11.0, -4.3, 8.6, 2.8, 3.6, 1.6),        # caixa
]


def _flame(Y, Z, y0, z0, H, W, fase, grow=0.0, eps=0.06):
    """Língua de fogo esguia em S, nascendo em (y0, z0) (cm); a ponta varre para +y (a coronha)."""
    v = (Z - z0) / H; vc = np.clip(v, 0, 1); lean = 0.30 * H
    cen = y0 + lean * vc ** 2.3 - 0.07 * H * np.sin(np.pi * vc * 1.1)
    hw = W * (1 - vc) ** 0.95 * (0.30 + 0.70 * (1 - np.exp(-vc * 9.0))) * (1 + 0.08 * np.sin(6.0 * vc + fase)) + grow
    dentro = np.clip((hw - np.abs(Y - cen)) / eps + 0.5, 0, 1)
    vert = np.clip((v * H + grow) / eps + 0.5, 0, 1) * np.clip(((1 - v) * H + grow * 0.5) / eps + 0.5, 0, 1)
    return dentro * vert, vc


def _hexagonos(u, v, s):
    """Distância ao centro da célula hexagonal mais próxima (duas grades), em células."""
    h = s * 0.8660254
    def dist(du, dv):
        a = (u / s + du) % 1.0 - 0.5; b = (v / (2 * h) + dv) % 1.0 - 0.5
        return np.hypot(a * s, b * 2 * h)
    return np.minimum(dist(0.0, 0.0), dist(0.5, 0.5)) / s


def chamas(X):
    """AR-15 Ultimate (lendária): roxo e índigo com flames acesas de lado (três camadas e contorno escuro), colmeia
    discreta no que é escuro, quina clara. O desenho das flames está em ``FLAMES`` (y ao comprido, z a altura, em cm)."""
    corpo, metal, latao, escuro = _ar15(X); n = X.n
    y, z = X.P[:, 2] * 100, X.P[:, 1] * 100; nx = np.abs(X.N[:, 0])
    tz = suave(-5, 9, z)
    cor = np.zeros((n, 3))
    cor[corpo] = mix(lin('#1b0542'), lin('#4b1fa6'), tz[corpo] * 0.85 + 0.1 * suave(-50, 25, y[corpo]))
    cor[escuro] = mix(lin('#0a0618'), lin('#221247'), 0.25 * tz[escuro] + 0.33 * suave(-49, -40, -y[escuro]))
    cor[metal] = mix(lin('#8a5a12'), lin('#fde68a'), tz[metal]); cor[latao] = mix(lin('#a21caf'), lin('#f0abfc'), tz[latao])
    cor = cor * (0.92 + 0.14 * X.Ns[:, 1])[:, None]
    lado = suave(0.35, 0.7, nx)
    colmeia = suave(0.40, 0.48, _hexagonos(y, z, 1.5)) * lado * (escuro | metal)
    cor = mix(cor, lin('#7c3aed'), 0.2 * colmeia)
    portao = suave(0.30, 0.62, nx)
    f_out, f_mid, f_in, f_edge, f_halo, fa = (np.zeros(n) for _ in range(6)); lista = []
    for i, (y0, z0, H, W, lean, ph) in enumerate(FLAMES):
        if i >= 3:
            lista.append((y0 - 1.15 * W, z0, H * 0.50, W * 0.55, lean * 0.5, ph + 1)); lista.append((y0 + 1.2 * W, z0, H * 0.6, W * 0.6, lean * 0.6, ph + 2))
        lista.append((y0, z0, H, W, lean, ph))
    for (y0, z0, H, W, lean, ph) in lista:
        mo, a_ = _flame(y, z, y0, z0, H, W, ph); me, _ = _flame(y, z, y0, z0, H, W, ph, grow=0.26); mh, _ = _flame(y, z, y0, z0, H, W, ph, grow=0.7, eps=0.6)
        mm, _ = _flame(y, z, y0 + 0.10 * lean, z0 + 0.02 * H, H * 0.72, W * 0.62, ph + 0.5); mi, _ = _flame(y, z, y0 + 0.18 * lean, z0 + 0.04 * H, H * 0.45, W * 0.34, ph + 1.0)
        fa = np.where(mo > 0.5, a_, fa); k = mo > 0.02
        f_out = np.where(k, np.maximum(f_out * (1 - mo), mo), f_out); f_mid = np.where(k, mm, f_mid); f_in = np.where(k, mi, f_in)
        f_edge = np.maximum(f_edge, me); f_halo = np.maximum(f_halo, mh)
    f_out, f_edge, f_mid, f_in, f_halo = (f * portao for f in (f_out, f_edge, f_mid, f_in, f_halo))
    brilho = lin('#c026d3')
    cor = mix(cor, brilho, 0.35 * f_halo)
    cor = mix(cor, lin('#0a0414'), np.clip(f_edge - f_out, 0, 1) * 0.95)
    c_out = mix(lin('#7a1fe0'), lin('#f03cff'), suave(0.0, 0.9, fa)); c_mid = mix(lin('#ff5ff0'), lin('#ffd6fb'), fa); c_in = mix(lin('#f5d0fe'), lin('#ffffff'), fa)
    for f, c in ((f_out, c_out), (f_mid, c_mid), (f_in, c_in)): cor = mix(cor, c, f)
    rim = quinas(X, 0.0022); cor_rim = np.where(escuro[:, None], lin('#8b5cf6'), lin('#e9d5ff'))
    ra = rim * np.where(escuro, 0.55, 0.8) * (1 - 0.85 * f_out)
    cor = mix(cor, cor_rim, ra)
    emis = brilho[None, :] * (0.10 * f_halo)[:, None]
    for f, c, forca in ((f_out, c_out, 0.85), (f_mid, c_mid, 1.0), (f_in, c_in, 1.0)): emis = mix(emis, c * forca, f)
    emis = (emis + cor_rim * (ra * 0.30 * (1 - f_out))[:, None]) * 0.65
    fogo = np.clip(f_out + f_mid, 0, 1)
    return cor, emis, np.where(fogo > 0.3, 0.22, 0.34), np.where(metal | latao, 0.9, 0.55), 0.00012 * f_out


ESTILOS = dict(aco=aco, granada=granada, granada_carbono=granada_carbono, granada_neon=granada_neon, granada_magma=granada_magma, granada_ouro=granada_ouro, fabrica=fabrica, circuito=circuito, dragao=dragao, tigre=tigre, imperial=imperial, artico=artico, galaxia=galaxia,
               carbono=carbono, tempestade=tempestade, cobra=cobra, fenix=fenix, damasco=damasco, obsidiana=obsidiana,
               ouro=ouro, diamante=diamante, lava=lava, pixel=pixel, chamas=chamas)


# ---------------------------------------------------------------- gravar
def png(caminho, img):
    """img: L×L×3 (uint8)."""
    def bloco(tipo, dados): return struct.pack('>I', len(dados)) + tipo + dados + struct.pack('>I', zlib.crc32(tipo + dados) & 0xffffffff)
    linhas = b''.join(b'\x00' + img[y].tobytes() for y in range(img.shape[0]))
    dados = b'\x89PNG\r\n\x1a\n' + bloco(b'IHDR', struct.pack('>IIBBBBB', img.shape[1], img.shape[0], 8, 2, 0, 0, 0)) + bloco(b'IDAT', zlib.compress(linhas, 6)) + bloco(b'IEND', b'')
    open(caminho, 'wb').write(dados); return dados


IMPORT = """[remap]

importer="texture"
type="CompressedTexture2D"
{uid}
[params]

compress/mode=2
compress/high_quality={alta}
compress/normal_map={relevo}
mipmaps/generate=true
detect_3d/compress_to=0
"""


def importacao(caminho, relevo=False, alta=True):
    """O ``.import`` da textura: comprimida na placa com a compressão boa (a comum deixa o degradê do ouro em quadradinhos
    — era parte do "pixelado"), com mipmaps (a arma longe não cintila). É o mínimo: o Godot completa o resto e, por
    faltarem os caminhos de destino, importa de novo. O uid de antes fica."""
    imp = caminho + '.import'; uid = ''
    if os.path.exists(imp):
        for linha in open(imp, encoding='utf-8'):
            if linha.startswith('uid='): uid = linha
    with open(imp, 'w', encoding='utf-8') as fh:
        fh.write(IMPORT.format(uid=uid, alta='true' if alta else 'false', relevo=1 if relevo else 0))


def pecas_de(base):
    """[(arquivo, nó, malha)] da arma de origem, no espaço do punho (m). O AR-15 é o do Jhon (``dados/ar15.npz``: as
    peças e a paleta dele, guardadas de ``assets/armas/ar15`` antes de ganharem o UV novo)."""
    if base in formas_mao.FORMAS: return formas_mao.FORMAS[base]()          # a faca e a granada redesenhadas (20/11)
    if base != 'ar15': return reais.arma(base)['pecas']
    d = np.load(os.path.join(AQUI, 'dados', 'ar15.npz')); out = []
    for arquivo, no in (('corpo', ''), ('ferrolho', 'Ferrolho'), ('magazine', 'Magazine')):
        pos = d[arquivo + '_pos'].astype(float); fn = np.cross(pos[:, 1] - pos[:, 0], pos[:, 2] - pos[:, 0]); fn /= np.linalg.norm(fn, axis=1, keepdims=True) + 1e-20
        out.append((arquivo, no, dict(pos=pos, normal=np.repeat(fn[:, None, :], 3, 1), uv=d[arquivo + '_uv'].astype(float))))
    return out


def cor_da_paleta(at):
    """A cor (linear) que cada triângulo tinha na paleta, e a célula dela."""
    cores = np.array([c for _, c in reais._cores()])
    uv = at.uv0[:, 0]; col = np.clip((uv[:, 0] * reais.LADO).astype(int), 0, reais.LADO - 1); lin_ = np.clip(((1 - uv[:, 1]) * reais.LADO).astype(int), 0, reais.LADO - 1)
    cel = np.clip(lin_ * reais.LADO + col, 0, len(cores) - 1); por_tri = cores[cel]
    return lambda uv, t: (por_tri[t], cel[t])


def cor_das_partes(pecas):
    """Nas peças de ``formas_mao`` não há paleta: o "material" de cada texel é a parte da peça (a lâmina, o cabo, a bola,
    a alavanca… — ``formas_mao.PARTES``) e a cor de antes, um cinza."""
    parte = np.concatenate([m['parte'] for _, _, m in pecas])
    return lambda uv, t: (np.full((len(t), 3), 0.2), parte[t])


def cor_do_ar15(at):
    """O mesmo para o AR-15: a paleta dele (256 px) no meio do UV de cada triângulo."""
    img = np.load(os.path.join(AQUI, 'dados', 'ar15.npz'))['paleta'].astype(float) / 255
    img = np.where(img <= 0.04045, img / 12.92, ((img + 0.055) / 1.055) ** 2.4); H, W = img.shape[:2]
    uv = at.uv0.mean(1); x = np.clip((uv[:, 0] % 1 * W).astype(int), 0, W - 1); y = np.clip(((1 - uv[:, 1] % 1) * H).astype(int), 0, H - 1)
    por_tri = img[y, x]; cel = y * W + x
    return lambda uv, t: (por_tri[t], cel[t])


def cor_da_faca():
    from PIL import Image
    img = np.asarray(Image.open(os.path.join(AQUI, 'dados', 'knifeColor.jpg')).convert('RGB'), float) / 255
    img = np.where(img <= 0.04045, img / 12.92, ((img + 0.055) / 1.055) ** 2.4); H, W = img.shape[:2]
    def f(uv, t):
        x = np.clip((uv[:, 0] % 1) * W, 0, W - 1).astype(int); y = np.clip((1 - uv[:, 1] % 1) * H, 0, H - 1).astype(int)
        return img[y, x], np.zeros(len(t), int)
    return f


def _vazar(img, cheio, passos=8):
    """Espalha a cor das cartas para os texels vazios em volta (o mipmap e o filtro não puxam preto para a borda)."""
    img = img.copy(); cheio = cheio.copy()
    for _ in range(passos):
        soma = np.zeros_like(img); n = np.zeros(cheio.shape, np.float32)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            c = np.roll(cheio, (dy, dx), (0, 1)); soma += np.roll(img, (dy, dx), (0, 1)) * c[..., None]; n += c
        novo = ~cheio & (n > 0); img[novo] = soma[novo] / n[novo][:, None]; cheio |= novo
    return img


def pintar(at, estilo, cor_base, metal=METAL, plano=False):
    """Roda o desenho em todos os texels (em lotes, para a memória não passar de uns centos de MB) e devolve as quatro
    imagens no tamanho de saída: cor (sRGB), emissão (sRGB), ORM e relevo (lineares), em uint8, e quanto acende.

    Por cima do que o desenho devolve entra o acabamento que é igual em toda arma: o canto fundo escurece (na cor e na
    oclusão) e o relevo arredonda a quina — a normal de cada face vira para a da vizinha perto da aresta —, mais o
    relevo do próprio desenho (``altura``, em metros: a gravação, a faceta, o veio)."""
    L = at.tamanho; Ls = at.saida; ss = at.amostras; n = len(at.cheio)
    saida = (at.cheio // L // ss) * Ls + (at.cheio % L) // ss
    peso = np.bincount(saida, minlength=Ls * Ls).astype(np.float32)
    def somar(dest, v, parte):
        for k in range(v.shape[1]): dest[:, k] += np.bincount(saida[parte], weights=v[:, k], minlength=Ls * Ls)
    cor_s, emis_s, orm_s, nrm_s = (np.zeros((Ls * Ls, 3)) for _ in range(4))
    H = np.zeros(L * L, np.float32); acende = 0
    for a in range(0, n, LOTE):
        parte = slice(a, min(a + LOTE, n)); X = Texels(at, cor_base, parte)
        cor, emis, rug, met, *resto = estilo(X)
        if resto: H[at.cheio[parte]] = resto[0]
        fundo = np.exp(-(X.canto / 0.0013) ** 2)                               # o canto fundo
        if not plano: cor = cor * ((1 - 0.42 * fundo) * (1 - 0.16 * np.exp(-X.canto / 0.0035)))[:, None]
        oclusao = 1 - 0.6 * np.exp(-X.canto / 0.002)
        somar(cor_s, np.clip(cor, 0, 1), parte); somar(emis_s, np.clip(emis, 0, 1), parte)
        somar(orm_s, np.stack([oclusao, np.clip(rug, 0.03, 1), np.clip(np.asarray(met, float) * metal, 0, 1)], 1), parte)
        acende += int((emis.max(1) > 0.05).sum())
    # o relevo: a inclinação da altura, medida na imagem (só entre texels da mesma carta)
    Himg = H.reshape(L, L); carta = np.where(at.tri >= 0, at.carta[np.maximum(at.tri, 0)], -1).reshape(L, L)
    def inclinacao(eixo):
        soma = np.zeros((L, L), np.float32); cont = np.zeros((L, L), np.float32)
        for passo in (1, -1):
            igual = np.roll(carta, -passo, eixo) == carta
            soma += np.where(igual, (np.roll(Himg, -passo, eixo) - Himg) * passo, 0); cont += igual
        return (soma / np.maximum(cont, 1)).ravel()
    gx = inclinacao(1) * at.escala; gy = inclinacao(0) * at.escala; del Himg, carta
    for a in range(0, n, LOTE):
        parte = slice(a, min(a + LOTE, n)); c = at.cheio[parte]; t = at.tri[c]; b = at.bary[parte].astype(float)
        Ns = (b[:, :, None] * at.vn[t]).sum(1); Ns /= np.linalg.norm(Ns, axis=1, keepdims=True) + 1e-20
        Tg = at.Tu[t] - Ns * (Ns * at.Tu[t]).sum(1, keepdims=True); Tg /= np.linalg.norm(Tg, axis=1, keepdims=True) + 1e-20
        Bt = np.cross(Ns, Tg); Bt *= np.sign((Bt * at.Tv[t]).sum(1, keepdims=True) + 1e-20)
        wq = 0.5 * (1 - np.clip(at.d_quina[parte] / BISEL, 0, 1)) ** 2; wc = 0.5 * (1 - np.clip(at.d_canto[parte] / FILETE, 0, 1)) ** 2
        N3 = Ns + wq[:, None] * (at.viz_quina[parte] - Ns) + wc[:, None] * (at.viz_canto[parte] - Ns)
        N3 /= np.linalg.norm(N3, axis=1, keepdims=True) + 1e-20
        nt = np.stack([(N3 * Tg).sum(1) - gx[c], (N3 * Bt).sum(1) + gy[c], np.maximum((N3 * Ns).sum(1), 0.05)], 1)
        nt /= np.linalg.norm(nt, axis=1, keepdims=True)
        somar(nrm_s, nt, parte)
    cheio = (peso > 0).reshape(Ls, Ls); p = np.maximum(peso, 1)[:, None]
    def imagem(soma, como):
        v = (soma / p).reshape(Ls, Ls, 3).astype(np.float32)
        if como == 'relevo':
            v[~cheio] = (0, 0, 1); v = _vazar(v, cheio); v /= np.linalg.norm(v, axis=2, keepdims=True) + 1e-12; v = v * 0.5 + 0.5
        else:
            v = _vazar(v, cheio)
            if como == 'cor': v = srgb(v)
        return np.round(np.clip(v, 0, 1) * 255).astype(np.uint8)
    return dict(D=imagem(cor_s, 'cor'), E=imagem(emis_s, 'cor'), ORM=imagem(orm_s, 'dado'), N=imagem(nrm_s, 'relevo')), acende / max(n, 1)


def fazer(id_, tamanho=None, amostras=None):
    P_ = PELES[id_]; base = P_['base']; L = tamanho or P_['tamanho']; ss = amostras or P_.get('amostras', AMOSTRAS)
    print(f"  {id_} ({P_['nome']}, {P_['estilo']}):")
    pecas = pecas_de(base)
    at = Atlas(pecas, L, ss)
    cor_base = cor_das_partes(pecas) if base in formas_mao.FORMAS else cor_do_ar15(at) if base == 'ar15' else cor_da_paleta(at)
    imagens, acende = pintar(at, ESTILOS[P_['estilo']], cor_base, P_.get('metal', METAL), P_.get('plano', False))
    pasta = os.path.join(ARMAS, id_); os.makedirs(pasta, exist_ok=True)
    nome = P_.get('textura') or 'T_' + ''.join(p.capitalize() for p in id_.split('_'))
    for k, img in imagens.items():
        caminho = os.path.join(pasta, f'{nome}_{k}.png'); png(caminho, img); importacao(caminho, relevo=k == 'N')
    print(f"    texturas: {nome}_D, _E, _ORM e _N ({L} px); acende {acende:.1%} da pele")
    # as peças no mundo: a mesma malha, com o UV novo e as normais lisas
    fichas = json.load(open(os.path.join(ARMAS, 'armas.json'), encoding='utf-8'))
    entrada = json.loads(json.dumps(fichas['armas'][base])); partes = []; k = 0
    # (a pele herda o 'externo' da arma de origem: tools/gerar_armas.py guarda as fichas externas quando refaz o armas.json)
    for velho in os.listdir(pasta):
        if velho.endswith('.obj') and id_ != base:
            try: os.remove(os.path.join(pasta, velho))
            except OSError: pass                      # sem permissão de apagar (a pasta ligada ao Claude): o arquivo de mesmo nome é regravado logo abaixo
    for arquivo, no, m in pecas:
        n = len(m['pos']); m = dict(pos=m['pos'], normal=at.vn[k:k + n], uv=at.uv[k:k + n]); k += n
        if base == 'faca':
            # no mundo a faca fica de pé na mão (a antiga: lâmina para +Y, o pomo em y = −8,4 cm); a nova tem a lâmina para −Z
            G = np.array([[1.0, 0, 0], [0, 0, -1], [0, 1, 0]]); pos = m['pos'] @ G.T; pos[..., 1] += -0.084 - pos[..., 1].min()
            m = dict(pos=pos, normal=m['normal'] @ G.T, uv=m['uv'])
        nomeobj = f'{arquivo}_{id_}.obj'; reais._obj(os.path.join(pasta, nomeobj), m)
        partes.append(dict(malha=f'res://assets/armas/{id_}/{nomeobj}', material=f'res://assets/armas/{id_}/{nome}_D.png', no=no))
    entrada['partes'] = partes; fichas['armas'][id_] = entrada
    json.dump(fichas, open(os.path.join(ARMAS, 'armas.json'), 'w', encoding='utf-8'), indent=1, ensure_ascii=False, sort_keys=True)
    open(os.path.join(ARMAS, 'armas.json'), 'a', encoding='utf-8').write('\n')
    # a primeira pessoa: o conjunto da arma de origem com o UV, as normais e o material novos
    na_mao(id_, base, pecas, at, nome)
    return at


def _sopa_do_ar15(E, pecas):
    """O ``ar15.glb`` do Jhon tem o fuzil com vértices divididos entre os triângulos; as outras armas (``conjuntos.py``)
    têm três vértices por triângulo, na ordem das peças. Aqui o fuzil e o carregador dele passam a ser assim também:
    os triângulos das peças de ``assets/armas/ar15`` (os mesmos, no espaço do punho), cada um preso ao osso que o
    triângulo de mesmo lugar tinha (o punho, o ferrolho, o gatilho, o carregador)."""
    from esqueleto import ARMA
    j = E.j; Bg, _ = E.ligacao('ik_hand_gun'); M = Bg @ ARMA; Mi = np.linalg.inv(M)
    grupos = dict(fuzil=[m for _, no, m in pecas if no != 'Magazine'], carregador=[m for _, no, m in pecas if no == 'Magazine'])
    for nome, malhas in grupos.items():
        no = [x for x in j['nodes'] if x.get('name') == nome][0]; prim = j['meshes'][no['mesh']]['primitives'][0]
        if 'indices' not in prim: continue
        pos = E.g.acc(prim['attributes']['POSITION']).astype(float); idx = E.g.acc(prim['indices']).reshape(-1, 3).astype(int)
        juntas = E.g.acc(prim['attributes']['JOINTS_0'])[:, 0][idx[:, 0]]
        meio = ((np.c_[pos, np.ones(len(pos))] @ Mi.T)[:, :3])[idx].mean(1)
        P = np.concatenate([m['pos'] for m in malhas]); alvo = P.mean(1); osso = np.zeros(len(P), np.uint8); longe = 0.0
        for a in range(0, len(P), 400):
            d = np.linalg.norm(alvo[a:a + 400, None, :] - meio[None, :, :], axis=2); k = d.argmin(1); osso[a:a + 400] = juntas[k]; longe = max(longe, d.min(1).max())
        if longe > 0.004: raise SystemExit(f"PELE ERRO: o {nome} do ar15.glb não é o de assets/armas/ar15 (triângulo a {longe * 1000:.1f} mm do mais perto)")
        p = P.reshape(-1, 3); J = np.zeros((len(p), 4), np.uint8); J[:, 0] = np.repeat(osso, 3); W = np.zeros((len(p), 4), np.float32); W[:, 0] = 1
        fn = np.zeros((len(p), 3), np.float32); fn[:, 2] = 1
        novo = {'attributes': {'POSITION': E._cru(((np.c_[p, np.ones(len(p))] @ M.T)[:, :3]).astype(np.float32), 5126, 'VEC3', 34962),
                               'NORMAL': E._cru(fn, 5126, 'VEC3', 34962), 'TEXCOORD_0': E._cru(np.zeros((len(p), 2), np.float32), 5126, 'VEC2', 34962),
                               'JOINTS_0': E._cru(J, 5121, 'VEC4', 34962), 'WEIGHTS_0': E._cru(W, 5126, 'VEC4', 34962)}, 'mode': 4}
        if 'material' in prim: novo['material'] = prim['material']
        j['meshes'][no['mesh']] = {'name': nome, 'primitives': [novo]}


def _forma_nova(E, base, pecas):
    """Põe no conjunto a malha de ``formas_mao`` no lugar da antiga. Cada pedaço vai para o osso dele com a matriz que
    levava a peça antiga ao lugar dela na mão (``dados/na_mao.json``, medida no conjunto de ``conjuntos.py`` antes da
    troca: a faca inteira no osso Bolt; na granada, o corpo no Bolt, o pino no Trigger e a argola, já aberta, no
    Magazine). As animações são as mesmas."""
    ficha = json.load(open(os.path.join(AQUI, 'dados', 'na_mao.json'), encoding='utf-8'))[base]; j = E.j
    no = [n for n in j['nodes'] if n.get('name') == ficha['no']][0]; M = {o['osso']: np.array(o['matriz']) for o in ficha['ossos']}
    P, J = [], []
    for arquivo, no_peca, m in pecas:
        osso = np.where(m['parte'] == 4, 'Magazine', 'Trigger') if no_peca == 'Pino' else np.full(len(m['pos']), 'Bolt')
        for nome_osso in ('Bolt', 'Trigger', 'Magazine'):
            if not (osso == nome_osso).any(): continue
            sel = osso == nome_osso; p = m['pos'][sel].reshape(-1, 3); A = M[nome_osso]; _, k = E.ligacao(nome_osso)
            if base == 'faca': p = formas_mao.rolar(p, float(os.environ.get('FACA_GIRO', formas_mao.GIRO_NA_MAO)))
            P.append(p @ A[:, :3].T + A[:, 3]); jj = np.zeros((len(p), 4), np.uint8); jj[:, 0] = k; J.append(jj)
        assert (np.diff((osso == 'Magazine').astype(int)) >= 0).all()          # a argola vem depois do pino: a ordem do atlas é a da malha
    P = np.concatenate(P); J = np.concatenate(J); W = np.zeros((len(P), 4), np.float32); W[:, 0] = 1
    prim = j['meshes'][no['mesh']]['primitives'][0]; prim.pop('indices', None)
    prim['attributes'] = {'POSITION': E._cru(P.astype(np.float32), 5126, 'VEC3', 34962), 'JOINTS_0': E._cru(J, 5121, 'VEC4', 34962), 'WEIGHTS_0': E._cru(W, 5126, 'VEC4', 34962)}
    j['meshes'][no['mesh']]['primitives'] = [prim]


def na_mao(id_, base, pecas, at, nome):
    import shutil
    from esqueleto import Esqueleto
    origem = os.path.join(FP, base + '.glb'); destino = os.path.join(FP, id_ + '.glb')
    if id_ != base: shutil.copy(origem, destino)
    E = Esqueleto(destino); j = E.j
    if base == 'ar15': _sopa_do_ar15(E, pecas)
    if base in formas_mao.FORMAS: _forma_nova(E, base, pecas)
    # as texturas não vão dentro do .glb: ele aponta para as de assets/armas/<id>/ (as mesmas da arma no mundo — uma
    # cópia só no projeto, com a mesma importação)
    def textura(sufixo):
        j.setdefault('images', []).append({'uri': f'../{id_}/{nome}_{sufixo}.png', 'name': f'{nome}_{sufixo}'})
        j.setdefault('samplers', []).append({'magFilter': 9729, 'minFilter': 9987, 'wrapS': 33071, 'wrapT': 33071})
        j.setdefault('textures', []).append({'sampler': len(j['samplers']) - 1, 'source': len(j['images']) - 1})
        return len(j['textures']) - 1
    orm = textura('ORM')
    j['materials'].append({'name': id_, 'pbrMetallicRoughness': {'baseColorTexture': {'index': textura('D')}, 'metallicRoughnessTexture': {'index': orm},
                                                                'metallicFactor': 1.0, 'roughnessFactor': 1.0},
                           'normalTexture': {'index': textura('N')}, 'occlusionTexture': {'index': orm},
                           'emissiveTexture': {'index': textura('E')}, 'emissiveFactor': [1.0, 1.0, 1.0]})
    mat = len(j['materials']) - 1
    # os triângulos de cada malha, na ordem em que conjuntos.abrir pôs as peças: o fuzil (tudo menos o carregador) e o carregador
    k = 0; por_no = {'corpo': [], 'carregador': []}
    for arquivo, no, m in pecas:
        n = len(m['pos']); por_no['carregador' if no == 'Magazine' else 'corpo'].append(np.arange(k, k + n)); k += n
    trocados = 0
    for no in j['nodes']:
        if 'mesh' not in no or no.get('name') == 'bracos': continue
        tris = por_no['carregador' if no.get('name') == 'carregador' else 'corpo']
        for p in j['meshes'][no['mesh']]['primitives']:
            pos = E.g.acc(p['attributes']['POSITION']).astype(float).reshape(-1, 3, 3)
            if not tris or len(np.concatenate(tris)) != len(pos):
                raise SystemExit(f"PELE ERRO: {id_}: a malha {no.get('name')} tem {len(pos)} triângulos, o atlas {len(np.concatenate(tris)) if tris else 0}")
            t = np.concatenate(tris)
            uv = at.uv[t].copy(); uv[..., 1] = 1 - uv[..., 1]                                    # o v do glTF é de cima para baixo
            # as normais lisas, no espaço da malha: a mesma conta do atlas (os leques de faces), com as faces daqui
            fn = np.cross(pos[:, 1] - pos[:, 0], pos[:, 2] - pos[:, 0]); fn /= np.linalg.norm(fn, axis=1, keepdims=True) + 1e-20
            leque = at.leque[t]; _, lq = np.unique(leque, return_inverse=True); lq = lq.reshape(-1)
            soma = np.zeros((lq.max() + 1, 3)); np.add.at(soma, lq, np.repeat(fn, 3, axis=0) * at.peso_canto[t].reshape(-1, 1))
            vn = soma[lq]; vn /= np.linalg.norm(vn, axis=1, keepdims=True) + 1e-20
            # a tangente: para onde o u cresce na face; o sinal diz para que lado fica o "para cima" da textura
            d1 = pos[:, 1] - pos[:, 0]; d2 = pos[:, 2] - pos[:, 0]; u1 = uv[:, 1] - uv[:, 0]; u2 = uv[:, 2] - uv[:, 0]
            det = u1[:, 0] * u2[:, 1] - u2[:, 0] * u1[:, 1]; det = np.where(np.abs(det) < 1e-12, 1e-12, det)
            Tf = (d1 * u2[:, [1]] - d2 * u1[:, [1]]) / det[:, None]; Bf = -(d2 * u1[:, [0]] - d1 * u2[:, [0]]) / det[:, None]
            Tc = np.repeat(Tf, 3, axis=0); Tc = Tc - vn * (vn * Tc).sum(1, keepdims=True); mod = np.linalg.norm(Tc, axis=1, keepdims=True)
            Tc = np.where(mod > 1e-9, Tc / np.maximum(mod, 1e-20), np.cross(vn, [0.0, 1.0, 0.0]) + 1e-6)
            Tc /= np.linalg.norm(Tc, axis=1, keepdims=True) + 1e-20
            w = np.where((np.cross(vn, Tc) * np.repeat(Bf, 3, axis=0)).sum(1) < 0, -1.0, 1.0)
            p['attributes']['TEXCOORD_0'] = E._cru(uv.reshape(-1, 2).astype(np.float32), 5126, 'VEC2', 34962)
            p['attributes']['NORMAL'] = E._cru(vn.astype(np.float32), 5126, 'VEC3', 34962)
            p['attributes']['TANGENT'] = E._cru(np.c_[Tc, w].astype(np.float32), 5126, 'VEC4', 34962)
            for sobra in ('COLOR_0', 'COLOR_1'): p['attributes'].pop(sobra, None)
            p['material'] = mat; trocados += 1
    E.podar(); E.salvar()
    print(f"    primeira pessoa: fp/{id_}.glb ({trocados} malhas com o UV, as normais e o material novos)")


def ficha_fp(ids):
    """As entradas ``<id>`` de ``fp/bracos.json``: a da arma de origem, com o arquivo novo."""
    caminho = os.path.join(FP, 'bracos.json'); d = json.load(open(caminho, encoding='utf-8'))
    for id_ in ids:
        base = PELES[id_]['base']
        if id_ == base: continue
        chave = [k for k, v in d.items() if base in v.get('armas', [])][0]
        e = json.loads(json.dumps(d[chave])); e['armas'] = [id_]; e['arquivo'] = f'res://assets/armas/fp/{id_}.glb'; d[id_] = e
    json.dump(d, open(caminho, 'w', encoding='utf-8'), indent=1, ensure_ascii=False, sort_keys=True); open(caminho, 'a', encoding='utf-8').write('\n')


if __name__ == '__main__':
    args = sys.argv[1:]; tam = am = None
    if '--tamanho' in args: i = args.index('--tamanho'); tam = int(args[i + 1]); del args[i:i + 2]
    if '--amostras' in args: i = args.index('--amostras'); am = int(args[i + 1]); del args[i:i + 2]
    # as armas de origem primeiro: a pele parte do conjunto da arma dela
    ids = sorted(args or list(PELES), key=lambda i: (PELES[i]['base'] != i, list(PELES).index(i)))
    for id_ in ids: fazer(id_, tam, am)
    ficha_fp(ids)
