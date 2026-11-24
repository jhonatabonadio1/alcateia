#!/usr/bin/env python3
"""
As mãos de primeira pessoa, desenhadas (pedido do Jhon, 20/11: "deixe também a mão mais bem desenhada, com textura
melhor e qualidade, assim como fez no mapa" e, depois, "coloque um bracelete sci-fi metálico com led azul ou vermelho
dependendo do time").

    python3 tools/armas/maos.py

Lê as mãos originais (o manequim, 7.240 triângulos, com a pele fotográfica) de tools/armas/fontes/ e grava em
assets/armas/fp/maos/:

- maos.glb          a malha inteira, lisa (a de 18/11 era reduzida e de cor chapada por face), mais o bracelete nos
                    dois pulsos — uma cinta de metal com o canal do LED no meio; no esquerdo, o módulo com a tela;
- maos_cor.png      a cor: a manga de tecido tático (trama, dobras), a luva de couro (costuras, painéis), a pele dos
                    dedos — tirada da foto, limpa e com o contraste refeito — e, na faixa de baixo, o metal do bracelete;
- maos_relevo.png   o relevo (dobras do tecido, costura, grão do couro, as juntas dos dedos, os frisos do bracelete);
- maos_orm.png      oclusão, rugosidade e metal;
- maos_led.png      onde acende: o LED do bracelete. A cor quem dá é o jogo, pelo time (o parâmetro ``led`` do
                    material — scripts/ui/ViewModel.Maos.cs): azul na Vanguarda, vermelho no Sindicato;
- maos.gdshader     o material das mãos (padrao.tres aponta para ele).

O esqueleto e os pesos são os da malha original; o bracelete acompanha o antebraço (os pesos da manga onde ele está).

Precisa de: numpy, scipy e pillow.
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage as ndi

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, '..', 'rugido'))
from glb import Glb

FONTE = os.path.join(AQUI, 'fontes', 'maos_alta.glb')
PELE = os.path.join(AQUI, 'fontes', 'maos_cor_alta.jpg')
SAIDA = os.path.join(AQUI, '..', '..', 'assets', 'armas', 'fp', 'maos')
L = 2048                       # lado das texturas
FAIXA = 320                    # as linhas de baixo, do bracelete (a mão fica com as de cima)
ALT = L - FAIXA
V_MAO = ALT / L                # o v da mão encolhe para caber em cima da faixa
SEG = 48                       # gomos da cinta em volta do braço

# a faixa do bracelete na textura (px): a cinta desenrolada, um retalho de metal liso, o tampo e o lado do módulo
R_CINTA = (0, ALT + 8, L, ALT + 158)
R_METAL = (8, ALT + 172, 248, ALT + 312)
R_TAMPO = (300, ALT + 172, 812, ALT + 312)
R_LADO = (860, ALT + 172, 1160, ALT + 312)


def lin(h):
    c = np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)]) / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def suave(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def por(g, arr, comp, tipo, alvo=None, minmax=False):
    arr = np.ascontiguousarray(arr, dtype={5121: '<u1', 5123: '<u2', 5125: '<u4', 5126: '<f4'}[comp])
    while len(g.bin) % 4: g.bin.append(0)
    off = len(g.bin); g.bin += arr.tobytes()
    bv = {'buffer': 0, 'byteOffset': off, 'byteLength': arr.nbytes}
    if alvo: bv['target'] = alvo
    g.json['bufferViews'].append(bv)
    a = {'bufferView': len(g.json['bufferViews']) - 1, 'componentType': comp, 'count': int(arr.shape[0]), 'type': tipo}
    if minmax: a['min'] = arr.reshape(len(arr), -1).min(0).tolist(); a['max'] = arr.reshape(len(arr), -1).max(0).tolist()
    g.json['accessors'].append(a); return len(g.json['accessors']) - 1


def compactar(g):
    """Regrava o binário só com os acessores em uso."""
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


# ---------------------------------------------------------------- as ilhas da textura (o que é manga, luva e pele)
def ilhas(n, idx):
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components
    r = np.r_[idx[:, 0], idx[:, 1], idx[:, 2]]; c = np.r_[idx[:, 1], idx[:, 2], idx[:, 0]]
    return connected_components(coo_matrix((np.ones(len(r)), (r, c)), shape=(n, n)), directed=False)[1]


def classe_do_vertice(uv, lab):
    """0 = manga, 1 = luva, 2 = pele — pela caixa da ilha na textura original (as duas mãos usam as mesmas ilhas)."""
    cls = np.zeros(len(uv), int)
    for k in np.unique(lab):
        m = lab == k; lo, hi = uv[m].min(0), uv[m].max(0); c = (lo + hi) / 2
        if c[0] < 0.5 and c[1] > 0.66: cls[m] = 2                      # os dedos (embaixo, à esquerda)
        elif 0.44 < c[0] < 0.67 and c[1] > 0.86: cls[m] = 2            # o polegar
        elif c[0] > 0.52 and hi[0] > 0.6: cls[m] = 1                   # a luva (à direita) e os retalhos dela
        else: cls[m] = 0
    return cls


def rasterizar(uv, idx, valores):
    """Pinta no atlas (ALT × L), por triângulo, o valor de cada vértice interpolado. Devolve (imagem, cheio)."""
    C = valores.shape[1]; img = np.zeros((ALT, L, C), np.float32); cheio = np.zeros((ALT, L), bool)
    px = uv * np.array([L, ALT])
    for t in idx:
        a, b, c = px[t]; lo = np.floor(np.minimum(np.minimum(a, b), c)).astype(int) - 1; hi = np.ceil(np.maximum(np.maximum(a, b), c)).astype(int) + 1
        x0, y0 = max(lo[0], 0), max(lo[1], 0); x1, y1 = min(hi[0], L - 1), min(hi[1], ALT - 1)
        if x1 < x0 or y1 < y0: continue
        X, Y = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(d) < 1e-9: continue
        w0 = ((b[1] - c[1]) * (X - c[0]) + (c[0] - b[0]) * (Y - c[1])) / d
        w1 = ((c[1] - a[1]) * (X - c[0]) + (a[0] - c[0]) * (Y - c[1])) / d
        w2 = 1 - w0 - w1; dentro = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
        if not dentro.any(): continue
        v = w0[..., None] * valores[t[0]] + w1[..., None] * valores[t[1]] + w2[..., None] * valores[t[2]]
        sub = img[y0:y1 + 1, x0:x1 + 1]; sub[dentro] = v[dentro]; cheio[y0:y1 + 1, x0:x1 + 1] |= dentro
    return img, cheio


def ruido(shape, escala, semente):
    """Ruído liso (0..1) de período ``escala`` px."""
    r = np.random.default_rng(semente); h, w = shape
    p = r.random((h // escala + 3, w // escala + 3)).astype(np.float32)
    return ndi.zoom(p, escala, order=3)[:h, :w]


# ---------------------------------------------------------------- a pintura da mão
def pintar_mao(uv, idx, pos, cls):
    foto = Image.open(PELE).convert('RGB').resize((L, ALT), Image.LANCZOS).filter(ImageFilter.MedianFilter(5))
    foto = (np.asarray(foto, np.float32) / 255.0) ** 2.2
    lum = foto @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    val, cheio = rasterizar(uv * np.array([1.0, 1.0]), idx, np.c_[np.eye(3)[cls], pos].astype(np.float32))
    # fora das ilhas vale a ilha mais perto (a costura não mostra o fundo)
    longe, (iy, ix) = ndi.distance_transform_edt(~cheio, return_indices=True)
    val = val[iy, ix]; qual = val[..., :3].argmax(2); P = val[..., 3:]
    manga, luva, pele = qual == 0, qual == 1, qual == 2
    g = ndi.gaussian_filter
    Y, X = np.mgrid[0:ALT, 0:L].astype(np.float32)

    cor = np.zeros((ALT, L, 3), np.float32); rug = np.zeros((ALT, L), np.float32); alt = np.zeros((ALT, L), np.float32)
    # --- a manga: tecido tático grafite; as dobras vêm da foto (a luz dela), a trama é desenhada
    dobra = g(lum, 2.0) / (g(lum, 26.0) + 0.004); dobra = np.clip(dobra, 0.45, 1.9)
    n1 = ruido((ALT, L), 96, 1); n2 = ruido((ALT, L), 7, 2)
    trama = 0.5 + 0.5 * np.sin((X + Y) * 2 * np.pi / 5.0) * np.sin((X - Y) * 2 * np.pi / 5.0)       # sarja
    grade = np.maximum(suave(0.9, 1.0, np.cos(X * 2 * np.pi / 11.0)), suave(0.9, 1.0, np.cos(Y * 2 * np.pi / 11.0)))   # o fio do ripstop
    base = lin('#2b3038')[None, None, :] * (0.82 + 0.36 * n1)[..., None]
    c_manga = base * (0.42 + 0.62 * dobra)[..., None] * (0.93 + 0.14 * trama + 0.10 * (n2 - 0.5))[..., None]
    c_manga = c_manga * (1 + 0.07 * grade)[..., None]
    h_manga = 0.9 * (g(lum, 2.5) - g(lum, 22.0)) / (g(lum, 22.0) + 0.01) * 0.06 + 0.004 * trama + 0.003 * grade
    cor[manga] = c_manga[manga]; rug[manga] = (0.86 + 0.08 * n2)[manga]; alt[manga] = h_manga[manga]
    # --- a luva: couro grafite; os painéis e as costuras são os da foto, com os níveis refeitos (era quase preta)
    ll = g(lum, 1.2); lo, hi = np.percentile(ll[luva & cheio], (3, 99.2)); t = np.clip((ll - lo) / (hi - lo), 0, 1) ** 0.72
    grao = ruido((ALT, L), 4, 3)
    c_luva = (lin('#101114')[None, None, :] * (1 - t[..., None]) + lin('#5a5f69')[None, None, :] * t[..., None]) * (0.9 + 0.2 * grao)[..., None]
    c_luva = c_luva * (0.9 + 0.2 * n1)[..., None]
    h_luva = (g(lum, 1.2) - g(lum, 9.0)) / (g(lum, 9.0) + 0.01) * 0.035 + 0.006 * (grao - 0.5)
    cor[luva] = c_luva[luva]; rug[luva] = (0.62 - 0.22 * t + 0.08 * grao)[luva]; alt[luva] = h_luva[luva]
    # --- a pele: a da foto, limpa (sem o grão da câmera), um pouco mais quente e com a sombra das juntas mais marcada
    fs = np.stack([g(foto[..., k], 1.6) for k in range(3)], 2); ls = fs @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    sombra = np.clip(g(ls, 3.0) / (g(ls, 28.0) + 0.004), 0.6, 1.25)
    c_pele = fs * (0.55 + 0.5 * sombra)[..., None] * np.array([1.06, 1.0, 0.95], np.float32)
    cinza = c_pele.mean(2, keepdims=True); c_pele = cinza + (c_pele - cinza) * 1.12
    h_pele = (g(ls, 2.0) - g(ls, 14.0)) / (g(ls, 14.0) + 0.01) * 0.03
    sat = (fs.max(2) - fs.min(2)) / (fs.max(2) + 1e-4); unha = pele & (sat < 0.33) & (ls > 0.42)
    cor[pele] = c_pele[pele]; rug[pele] = np.where(unha, 0.28, 0.5 + 0.1 * (1 - sombra))[pele]; alt[pele] = h_pele[pele]

    oclusao = np.clip(0.55 + 0.5 * np.clip(g(lum, 4.0) / (g(lum, 40.0) + 0.004), 0, 1.2), 0.45, 1.0)
    return cor, rug, alt, oclusao, cheio, P, qual


# ---------------------------------------------------------------- a faixa do bracelete
def pintar_bracelete():
    """As linhas de baixo das quatro texturas: (cor linear, rugosidade, metal, altura, led)."""
    H = FAIXA; cor = np.tile(lin('#30343b').astype(np.float32), (H, L, 1)); rug = np.full((H, L), 0.34, np.float32)
    met = np.ones((H, L), np.float32); alt = np.zeros((H, L), np.float32); led = np.zeros((H, L), np.float32)
    Y, X = np.mgrid[0:H, 0:L].astype(np.float32)
    escova = ndi.gaussian_filter(np.random.default_rng(5).random((H, L)).astype(np.float32), (0.6, 22.0)); escova = (escova - escova.min()) / np.ptp(escova)
    largo = ruido((H, L), 128, 6)
    cor *= (0.78 + 0.34 * escova + 0.18 * (largo - 0.5))[..., None]; rug += 0.12 * (escova - 0.5)
    alt += 0.002 * escova

    # a cinta: de cima a baixo — a borda chanfrada, o corpo, o canal do LED no meio, o corpo, a borda
    x0, y0, x1, y1 = R_CINTA; hh = y1 - y0; yy = (Y - (y0 - ALT)) / hh; na_cinta = (yy >= 0) & (yy <= 1)
    c = np.abs(yy - 0.5)                                                   # 0 no meio, 0,5 na borda
    borda = suave(0.42, 0.5, c); cor[na_cinta] = (cor * (1 + 0.55 * borda)[..., None])[na_cinta]; alt += np.where(na_cinta, -0.02 * borda, 0)
    gomo = (X / L * 8) % 1.0                                               # oito placas em volta
    junta = (np.minimum(gomo, 1 - gomo) < 0.012) & na_cinta & (c > 0.13)
    cor[junta] *= 0.25; alt[junta] -= 0.03; rug[junta] = 0.6
    # um friso rebaixado de cada lado do canal e os parafusos junto às juntas
    friso = na_cinta & (np.abs(c - 0.30) < 0.018); cor[friso] *= 0.55; alt[friso] -= 0.015
    for k in range(8):
        for lado in (-1, 1):
            for dx in (-0.045, 0.045):
                cx = ((k + dx) % 8) / 8 * L; cy = (y0 - ALT) + hh * (0.5 + lado * 0.385)
                d = np.hypot(X - cx, Y - cy); p = d < 5.0
                cor[p] = lin('#8a919c') * 0.9; alt[p] = 0.012 - 0.02 * (np.abs(X - cx)[p] < 1.2); rug[p] = 0.25
    # listras de aviso pintadas numa das placas (o detalhe "sci-fi" que não é só metal)
    placa = na_cinta & (gomo > 0.14) & (gomo < 0.86) & ((X / L * 8).astype(int) == 5) & (c > 0.17) & (c < 0.26)
    listra = ((X + Y) / 9.0).astype(int) % 2 == 0
    cor[placa & listra] = lin('#d8a516'); cor[placa & ~listra] = lin('#15161a'); met[placa] = 0.2; rug[placa] = 0.5
    # o canal do LED: vidro preto, com o filete aceso em traços compridos
    canal = na_cinta & (c < 0.115); cor[canal] = lin('#06070a'); met[canal] = 0.0; rug[canal] = 0.08; alt[canal] = -0.03
    traco = (X / L * 24) % 1.0
    aceso = canal & (c < 0.07) & (traco > 0.08) & (traco < 0.92)
    led[aceso] = 1.0; led = np.maximum(led, np.where(canal, 0.25 * suave(0.115, 0.05, c), 0))
    cor[aceso] = lin('#e8f0ff')

    # o tampo do módulo: moldura de metal, tela de vidro com o mostrador
    x0, y0, x1, y1 = R_TAMPO; y0 -= ALT; y1 -= ALT
    u = (X - x0) / (x1 - x0); v = (Y - y0) / (y1 - y0); no = (u >= 0) & (u <= 1) & (v >= 0) & (v <= 1)
    beira = np.minimum(np.minimum(u, 1 - u) * (x1 - x0), np.minimum(v, 1 - v) * (y1 - y0))
    cor[no & (beira < 6)] *= 1.5
    tela = no & (u > 0.10) & (u < 0.90) & (v > 0.16) & (v < 0.84)
    cor[tela] = lin('#05070b'); met[tela] = 0.0; rug[tela] = 0.06; alt[tela] = -0.02
    img = Image.new('L', (x1 - x0, y1 - y0), 0); d = ImageDraw.Draw(img); w, h = img.size
    # o mostrador: barra de carga em gomos, um anel, dois números de blocos e uma linha de pulso
    for k in range(9):
        if k < 7: d.rectangle([int(w * (0.16 + k * 0.045)), int(h * 0.26), int(w * (0.16 + k * 0.045 + 0.032)), int(h * 0.44)], fill=255)
        else: d.rectangle([int(w * (0.16 + k * 0.045)), int(h * 0.26), int(w * (0.16 + k * 0.045 + 0.032)), int(h * 0.44)], outline=120)
    d.ellipse([int(w * 0.70), int(h * 0.24), int(w * 0.70 + h * 0.52), int(h * 0.76)], outline=255, width=4)
    d.pieslice([int(w * 0.70) + 9, int(h * 0.24) + 9, int(w * 0.70 + h * 0.52) - 9, int(h * 0.76) - 9], -90, 140, fill=200)
    pts = [(0.16, 0.66), (0.24, 0.66), (0.27, 0.54), (0.31, 0.76), (0.35, 0.60), (0.38, 0.66), (0.50, 0.66), (0.53, 0.58), (0.56, 0.72), (0.59, 0.66), (0.64, 0.66)]
    d.line([(int(w * a), int(h * b)) for a, b in pts], fill=255, width=3)
    for k in range(3): d.rectangle([int(w * (0.60 + k * 0.022)), int(h * 0.27), int(w * (0.60 + k * 0.022 + 0.012)), int(h * 0.43)], fill=180)
    m = np.asarray(img.filter(ImageFilter.GaussianBlur(0.8)), np.float32) / 255.0
    sub = led[y0:y1, x0:x1]; np.maximum(sub, m, out=sub)
    led[y0:y1, x0:x1] = np.maximum(led[y0:y1, x0:x1], 0.10 * tela[y0:y1, x0:x1])         # a tela acesa por trás, de leve
    # o lado do módulo: metal com três rasgos de ventilação e um botão
    x0, y0, x1, y1 = R_LADO; y0 -= ALT; y1 -= ALT
    u = (X - x0) / (x1 - x0); v = (Y - y0) / (y1 - y0); no = (u >= 0) & (u <= 1) & (v >= 0) & (v <= 1)
    rasgo = no & (u > 0.12) & (u < 0.55) & (np.abs(((v * 5) % 1.0) - 0.5) < 0.14) & (v > 0.2) & (v < 0.8)
    cor[rasgo] *= 0.22; alt[rasgo] -= 0.03
    botao = no & (np.hypot((u - 0.78) * (x1 - x0), (v - 0.5) * (y1 - y0)) < 16)
    cor[botao] = lin('#9aa1ac'); alt[botao] += 0.02
    return cor, rug, met, alt, led


# ---------------------------------------------------------------- a malha do bracelete
def bracelete(pos, cls, jn, wt, ossos, lado, modulo):
    """A cinta em volta do antebraço ``lado`` ('l' ou 'r'): (P, N, UV, J, W, I). Medidas em cm (o espaço da malha)."""
    pulso, cotovelo = ossos['hand_' + lado], ossos['lowerarm_' + lado]
    a = cotovelo - pulso; comp = np.linalg.norm(a); a /= comp
    do_lado = (pos[:, 0] > 0) == (pulso[0] > 0)
    t = (pos - pulso) @ a
    larg = 3.3 if modulo else 2.5
    fim_da_manga = t[do_lado & (cls == 0)].min()                          # onde a manga acaba (a luva começa)
    tc = max(fim_da_manga, 0.0) + larg / 2 + 1.6
    # o corte do braço ali: o raio por ângulo
    ref = np.array([0.0, 1.0, 0.0]); u = np.cross(a, ref); u /= np.linalg.norm(u); v = np.cross(a, u)
    perto = do_lado & (np.abs(t - tc) < larg / 2 + 0.6)
    eixo = pulso + a * tc; centro = pos[perto].mean(0); centro = eixo + (centro - eixo) - a * ((centro - eixo) @ a)   # o miolo do braço no corte
    d = pos[perto] - centro; ang = np.arctan2(d @ v, d @ u); raio = np.hypot(d @ u, d @ v)
    caixas = 24; h2 = larg / 2; s_ = d @ a
    k = ((ang + np.pi) / (2 * np.pi) * caixas).astype(int) % caixas
    def corte(de, ate):
        """O raio do braço por ângulo numa fatia (o antebraço afina para o pulso: a cinta acompanha)."""
        m = (s_ >= de) & (s_ <= ate); r = np.full(caixas, np.nan)
        for c in range(caixas):
            if (m & (k == c)).any(): r[c] = raio[m & (k == c)].max()
        r = np.where(np.isnan(r), np.nanmean(r), r)
        return ndi.uniform_filter1d(ndi.maximum_filter1d(r, 3, mode='wrap'), 3, mode='wrap') + 0.06
    r_a, r_b = corte(-h2 - 0.5, -h2 + 0.9), corte(h2 - 0.9, h2 + 0.5)
    def R(th, s=0.0):
        x = ((th + np.pi) / (2 * np.pi) * caixas - 0.5) % caixas; f = np.clip((s + h2) / larg, 0, 1)
        return (1 - f) * np.interp(x, np.arange(caixas + 1), np.r_[r_a, r_a[0]]) + f * np.interp(x, np.arange(caixas + 1), np.r_[r_b, r_b[0]])
    # os pesos: os da manga naquele corte (a cinta inteira com os mesmos — ela não dobra)
    soma = {}
    for jj, ww in zip(jn[perto].reshape(-1), wt[perto].reshape(-1)): soma[jj] = soma.get(jj, 0.0) + ww
    top = sorted(soma.items(), key=lambda kv: -kv[1])[:4]; tot = sum(w for _, w in top)
    J4 = np.array([j for j, _ in top] + [0] * (4 - len(top))); W4 = np.array([w / tot for _, w in top] + [0.0] * (4 - len(top)))

    E = 0.5                                                                # a espessura da cinta
    # o perfil ao comprido do braço: (s, altura sobre o tecido, v na cinta 0..1)
    perfil = [(-h2, -0.35, 0.0), (-h2, E * 0.7, 0.03), (-h2 + 0.16, E, 0.08), (-0.40, E, 0.385), (-0.33, E - 0.13, 0.40), (0.33, E - 0.13, 0.60),
              (0.40, E, 0.615), (h2 - 0.16, E, 0.92), (h2, E * 0.7, 0.97), (h2, -0.35, 1.0)]
    P, N, UV, I = [], [], [], []
    x0, y0, x1, y1 = R_CINTA
    ths = np.linspace(-np.pi, np.pi, SEG + 1)
    def ponto(th, s, h):
        return centro + a * s + (np.cos(th) * u + np.sin(th) * v) * (R(th, s) + h)
    for k in range(len(perfil) - 1):
        (s0, h0, v0), (s1, h1, v1) = perfil[k], perfil[k + 1]
        b0 = len(P)
        for th in ths:
            rad = np.cos(th) * u + np.sin(th) * v
            ds, dh = s1 - s0, h1 - h0; n = rad * ds - a * dh; n /= np.linalg.norm(n)            # a normal do trecho do perfil
            for s, h, vv in ((s0, h0, v0), (s1, h1, v1)):
                P.append(ponto(th, s, h)); N.append(n)
                UV.append(((th + np.pi) / (2 * np.pi) * (x1 - x0) + x0, y0 + vv * (y1 - y0)))
        for q in range(SEG):
            i0 = b0 + q * 2; I += [(i0, i0 + 2, i0 + 1), (i0 + 1, i0 + 2, i0 + 3)]
    if modulo is not None:
        # o módulo: uma caixa de tampo chanfrado, deitada sobre a cinta no ângulo ``modulo``
        th = np.radians(modulo); rad = np.cos(th) * u + np.sin(th) * v; tan = -np.sin(th) * u + np.cos(th) * v
        base = centro + rad * (R(th) + E * 0.55); cx, cy, alt_ = 2.3, 1.75, 0.95       # meio comprimento, meia largura, altura
        def q(s, w, h): return base + a * s + tan * w + rad * h
        cantos_b = [q(-cx, -cy, 0), q(cx, -cy, 0), q(cx, cy, 0), q(-cx, cy, 0)]
        cantos_m = [q(-cx, -cy, alt_ * 0.6), q(cx, -cy, alt_ * 0.6), q(cx, cy, alt_ * 0.6), q(-cx, cy, alt_ * 0.6)]
        ch = 0.32
        cantos_t = [q(-cx + ch, -cy + ch, alt_), q(cx - ch, -cy + ch, alt_), q(cx - ch, cy - ch, alt_), q(-cx + ch, cy - ch, alt_)]
        def face(pts, ret, uvs=((0, 0), (1, 0), (1, 1), (0, 1))):
            b0 = len(P); n = np.cross(pts[1] - pts[0], pts[3] - pts[0]); n /= np.linalg.norm(n)
            if n @ (np.mean(pts, 0) - base) < 0: n = -n
            for p_, (uu, vv) in zip(pts, uvs):
                P.append(p_); N.append(n); UV.append((ret[0] + uu * (ret[2] - ret[0]), ret[1] + vv * (ret[3] - ret[1])))
            I.extend([(b0, b0 + 1, b0 + 2), (b0, b0 + 2, b0 + 3)])
        face(cantos_t, R_TAMPO)
        for k in range(4):
            k2 = (k + 1) % 4
            face([cantos_b[k], cantos_b[k2], cantos_m[k2], cantos_m[k]], R_LADO, ((0, 1), (1, 1), (1, 0.25), (0, 0.25)))
            face([cantos_m[k], cantos_m[k2], cantos_t[k2], cantos_t[k]], R_METAL)
    P = np.array(P); N = np.array(N); UV = np.array(UV) / L; I = np.array(I)
    # o lado de fora de cada triângulo: o da normal
    tri = P[I]; fn = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]); virar = (fn * N[I[:, 0]]).sum(1) < 0
    I[virar] = I[virar][:, [0, 2, 1]]
    return P, N, UV, np.tile(J4, (len(P), 1)), np.tile(W4, (len(P), 1)), I


SHADER = """// As mãos de primeira pessoa (tools/armas/maos.py). O LED do bracelete acende na cor do time: o jogo põe ``led`` por
// instância (scripts/ui/ViewModel.Maos.cs); sem isso fica o azul.
shader_type spatial;
render_mode cull_back;
uniform sampler2D cor : source_color, filter_linear_mipmap_anisotropic;
uniform sampler2D orm : hint_default_white, filter_linear_mipmap_anisotropic;
uniform sampler2D relevo : hint_normal, filter_linear_mipmap_anisotropic;
uniform sampler2D luz : hint_default_black, filter_linear_mipmap;
uniform float relevo_forca = 1.0;
uniform float led_forca = 3.2;
instance uniform vec3 led : source_color = vec3(0.10, 0.50, 1.0);

void fragment() {
	vec3 c = texture(cor, UV).rgb;
	vec3 o = texture(orm, UV).rgb;
	float aceso = texture(luz, UV).r;
	ALBEDO = mix(c, led, aceso * 0.7);
	AO = o.r;
	AO_LIGHT_AFFECT = 0.35;
	ROUGHNESS = o.g;
	METALLIC = o.b;
	NORMAL_MAP = texture(relevo, UV).rgb;
	NORMAL_MAP_DEPTH = relevo_forca;
	EMISSION = led * aceso * led_forca;
	RIM = 0.12 * (1.0 - o.b);
	RIM_TINT = 0.6;
}
"""

MATERIAL = """[gd_resource type="ShaderMaterial" load_steps=6 format=3]

[ext_resource type="Shader" path="res://src/assets/armas/fp/maos/maos.gdshader" id="1_sh"]
[ext_resource type="Texture2D" path="res://src/assets/armas/fp/maos/maos_cor.png" id="2_cor"]
[ext_resource type="Texture2D" path="res://src/assets/armas/fp/maos/maos_orm.png" id="3_orm"]
[ext_resource type="Texture2D" path="res://src/assets/armas/fp/maos/maos_relevo.png" id="4_rel"]
[ext_resource type="Texture2D" path="res://src/assets/armas/fp/maos/maos_led.png" id="5_led"]

[resource]
resource_name = "maos"
shader = ExtResource("1_sh")
shader_parameter/cor = ExtResource("2_cor")
shader_parameter/orm = ExtResource("3_orm")
shader_parameter/relevo = ExtResource("4_rel")
shader_parameter/luz = ExtResource("5_led")
shader_parameter/relevo_forca = 1.0
shader_parameter/led_forca = 3.2
"""

IMPORT = """[remap]

importer="texture"
type="CompressedTexture2D"

[params]

compress/mode=2
compress/high_quality=%s
compress/normal_map=%d
mipmaps/generate=true
"""


def gravar(nome, img, relevo=False):
    c = os.path.join(SAIDA, nome); Image.fromarray(img).save(c, optimize=True)
    if not os.path.exists(c + '.import'):
        open(c + '.import', 'w').write(IMPORT % ('true', 1 if relevo else 0))


def main():
    g = Glb(FONTE); j = g.json
    prim = j['meshes'][0]['primitives'][0]; at = prim['attributes']
    pos = g.acc(at['POSITION']).astype(float); nor = g.acc(at['NORMAL']).astype(float); uv = g.acc(at['TEXCOORD_0']).astype(float)
    jn = g.acc(at['JOINTS_0']).astype(int); wt = g.acc(at['WEIGHTS_0']).astype(float)
    idx = g.acc(prim['indices']).reshape(-1, 3).astype(int)
    sk = j['skins'][0]; ibm = g.acc(sk['inverseBindMatrices']).reshape(-1, 4, 4)
    ossos = {j['nodes'][k]['name']: np.linalg.inv(ibm[i].T)[:3, 3] for i, k in enumerate(sk['joints'])}
    cls = classe_do_vertice(uv, ilhas(len(pos), idx))

    # ---- as texturas
    esq = idx[(pos[idx[:, 0], 0] > 0)]                                    # as duas mãos usam as mesmas ilhas: pinta-se pela esquerda
    cor, rug, alt, ocl, cheio, P3, qual = pintar_mao(uv, esq, pos, cls)
    bc, br, bm, ba, bl = pintar_bracelete()
    COR = np.concatenate([cor, bc], 0); RUG = np.concatenate([rug, br], 0); MET = np.concatenate([np.zeros_like(rug), bm], 0)
    ALTURA = np.concatenate([alt, ba], 0); OCL = np.concatenate([ocl, np.ones_like(br)], 0); LED = np.concatenate([np.zeros_like(rug), bl], 0)
    # o relevo, da altura (verde para cima, como o Godot quer)
    forca = 26.0
    gx = ndi.sobel(ALTURA, axis=1) / 8.0 * forca; gy = ndi.sobel(ALTURA, axis=0) / 8.0 * forca
    n = np.stack([-gx, gy, np.ones_like(gx)], 2); n /= np.linalg.norm(n, axis=2, keepdims=True)
    u8 = lambda v: np.round(np.clip(v, 0, 1) * 255).astype(np.uint8)
    os.makedirs(SAIDA, exist_ok=True)
    gravar('maos_cor.png', u8(srgb(COR)))
    gravar('maos_orm.png', u8(np.stack([OCL, np.clip(RUG, 0.04, 1), MET], 2)))
    gravar('maos_relevo.png', u8(n * 0.5 + 0.5), relevo=True)
    gravar('maos_led.png', u8(np.stack([LED] * 3, 2)))
    open(os.path.join(SAIDA, 'maos.gdshader'), 'w').write(SHADER)
    open(os.path.join(SAIDA, 'padrao.tres'), 'w').write(MATERIAL)

    # ---- a malha: a original inteira (o v encolhido para a faixa do bracelete caber) mais os dois braceletes
    uv2 = uv * np.array([1.0, V_MAO])
    Ps, Ns, UVs, Js, Ws, Is = [pos], [nor / np.maximum(np.linalg.norm(nor, axis=1, keepdims=True), 1e-9)], [uv2], [jn], [wt], [idx]
    base = len(pos)
    for lado, modulo in (('l', MODULO_E), ('r', None)):
        P, N, UV, J, W, I = bracelete(pos, cls, jn, wt, ossos, lado, modulo)
        Ps.append(P); Ns.append(N); UVs.append(UV); Js.append(J); Ws.append(W); Is.append(I + base); base += len(P)
    P = np.concatenate(Ps); N = np.concatenate(Ns); UV = np.concatenate(UVs); J = np.concatenate(Js); W = np.concatenate(Ws); I = np.concatenate(Is)
    W = W / np.maximum(W.sum(1, keepdims=True), 1e-9)
    prim['attributes'] = {
        'POSITION': por(g, P, 5126, 'VEC3', 34962, minmax=True), 'NORMAL': por(g, N, 5126, 'VEC3', 34962),
        'TEXCOORD_0': por(g, UV, 5126, 'VEC2', 34962), 'JOINTS_0': por(g, J, 5121, 'VEC4', 34962), 'WEIGHTS_0': por(g, W, 5126, 'VEC4', 34962),
    }
    prim['indices'] = por(g, I.reshape(-1), 5123 if len(P) < 65536 else 5125, 'SCALAR', 34963)
    prim.pop('material', None)
    compactar(g); g.save(os.path.join(SAIDA, 'maos.glb'))
    print(f"maos.glb: {len(I)} triângulos ({len(idx)} das mãos, {len(I) - len(idx)} dos braceletes); texturas {L} px em {os.path.relpath(SAIDA)}")


MODULO_E = 90.0                # em que ângulo, em volta do braço esquerdo, fica o módulo com a tela

if __name__ == '__main__':
    if '--modulo' in sys.argv: MODULO_E = float(sys.argv[sys.argv.index('--modulo') + 1])
    main()
