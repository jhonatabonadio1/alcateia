"""
As texturas de detalhe dos mapas de paleta (os kits "Low Poly FPS" e o Armazém).

Esses mapas são pintados por uma paleta: cada face tem uma cor lisa. O material
``assets/mapas/detalhe/superficie.gdshader`` mantém essa cor como a tinta de base e
desenha por cima o material de verdade — o tijolo com a junta, a laje com a emenda, a
tábua, a chapa com rebite, a telha, o calçamento de pedra. Este script desenha essas
texturas, uma camada por material, no estilo dos jogos de referência do Jhon (20/11:
"nível Riot, Overwatch", "tirar o aspecto de Roblox"): forma grande e limpa, quina
chanfrada, cada peça de um tom, pouco ruído miúdo.

    assets/mapas/detalhe/detalhe_n.jpg   R, G: o relevo (normal, convenção OpenGL); B: oclusão (a junta, a fresta)
    assets/mapas/detalhe/detalhe_m.jpg   R: claro-escuro (0,5 = neutro); G: aspereza (0..1); B: altura
    assets/mapas/detalhe/detalhe_c.jpg   a tinta: cor que multiplica a da paleta (0,5 = neutro; o tijolo mais
                                         alaranjado, o musgo na junta, a ferrugem no risco)
    assets/mapas/detalhe/macro.png       manchas largas (variação de tom de metro em metro)

Cada arquivo é um mosaico de 5 × 4 camadas de 1024 px, importado pelo Godot como
Texture2DArray (o ``.import`` ao lado diz as fatias). A ordem das camadas é a de
``CAMADAS`` — a mesma das tabelas do shader. Tudo é desenhado aqui (ruído e
geometria), sem foto: não depende de ``assets/textures/pbr``.

    python3 tools/mapas/detalhe_texturas.py        # precisa de numpy e Pillow
"""
import os

import numpy as np
from PIL import Image

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DESTINO = os.path.join(RAIZ, "assets", "mapas", "detalhe")
N = 1024

# ---------------------------------------------------------------- ruído que se repete (sem emenda)
def _freq(n=N):
    f = np.fft.fftfreq(n) * n
    return np.meshgrid(f, f)          # fx (colunas), fy (linhas), em ciclos por imagem


def ruido(semente, de, ate, queda=1.0, esticar=(1.0, 1.0), n=N):
    """Ruído de faixa: frequências de ``de`` a ``ate`` ciclos por imagem, caindo com 1/f^queda. ``esticar`` (x, y)
    alonga o grão num eixo (o escovado do metal). Sai em média 0 e desvio 1, e emenda nas bordas."""
    rng = np.random.default_rng(semente)
    fx, fy = _freq(n); r = np.hypot(fx * esticar[0], fy * esticar[1])
    amp = np.where((r >= de) & (r <= ate), 1.0 / np.maximum(r, 1e-6) ** queda, 0.0)
    campo = np.fft.ifft2(amp * np.exp(2j * np.pi * rng.random((n, n)))).real
    return (campo - campo.mean()) / max(campo.std(), 1e-12)


def borrar(img, sigma):
    """Desfoque gaussiano que dá a volta nas bordas (pela FFT)."""
    fx, fy = _freq(img.shape[0])
    return np.fft.ifft2(np.fft.fft2(img) * np.exp(-2 * (np.pi * sigma / img.shape[0]) ** 2 * (fx ** 2 + fy ** 2))).real


def relevo(altura, forca):
    """A normal (x, y em −1..1, convenção OpenGL: o verde aponta para o alto da imagem) de um campo de altura."""
    dx = (np.roll(altura, -1, 1) - np.roll(altura, 1, 1)) * 0.5 * forca
    dy = (np.roll(altura, -1, 0) - np.roll(altura, 1, 0)) * 0.5 * forca
    z = 1.0 / np.sqrt(dx * dx + dy * dy + 1.0)
    return -dx * z, dy * z


def faixa(v, lo=2, hi=98):
    a, b = np.percentile(v, lo), np.percentile(v, hi)
    return np.clip((v - a) / max(b - a, 1e-6), 0, 1)


# ---------------------------------------------------------------- as peças do desenho
def S(x, a, b):
    """Degrau suave de a a b."""
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)


def blocos(linhas, colunas, semente, desloc=0.5, var_larg=0.0, var_alt=0.0, n=N):
    """Uma parede de blocos (tijolo, pedra, tábua, laje) que se repete sem emenda: ``linhas`` fiadas de ``colunas``
    peças, cada fiada deslocada de ``desloc`` da peça (junta amarrada); ``var_larg`` e ``var_alt`` desigualam as
    larguras e as alturas. Devolve, por ponto: o número da peça, a distância à borda dela em x e em y (fração da
    imagem) e a posição dentro dela (0..1 em x e em y)."""
    rng = np.random.default_rng(semente)
    alt = 1 + var_alt * rng.uniform(-1, 1, linhas); alt /= alt.sum(); by = np.concatenate([[0], np.cumsum(alt)]); by[-1] = 1.0
    q = (np.arange(n) + 0.5) / n; lin = np.clip(np.searchsorted(by, q, side='right') - 1, 0, linhas - 1)
    ident = np.zeros((n, n), np.int32); du = np.zeros((n, n)); dv = np.zeros((n, n)); fu = np.zeros((n, n)); fv = np.zeros((n, n))
    for r in range(linhas):
        rows = np.flatnonzero(lin == r)
        larg = 1 + var_larg * rng.uniform(-1, 1, colunas); larg /= larg.sum(); bx = np.concatenate([[0], np.cumsum(larg)]); bx[-1] = 1.0
        fora = desloc * (r % 2) / colunas + (rng.uniform(-0.2, 0.2) / colunas if var_larg else 0.0)
        uu = (q + fora) % 1.0; c = np.clip(np.searchsorted(bx, uu, side='right') - 1, 0, colunas - 1)
        x0, x1 = bx[c], bx[c + 1]; y0, y1 = by[r], by[r + 1]; vv = q[rows]
        ident[rows] = (r * colunas + c)[None, :]; du[rows] = np.minimum(uu - x0, x1 - uu)[None, :]; dv[rows] = np.minimum(vv - y0, y1 - vv)[:, None]
        fu[rows] = ((uu - x0) / (x1 - x0))[None, :]; fv[rows] = ((vv - y0) / (y1 - y0))[:, None]
    return ident, du, dv, fu, fv


def chanfro(du, dv, junta, raio):
    """A altura de uma peça de quina arredondada: 0 na junta, 1 no plano de cima."""
    a = np.clip(du - junta, 0, None); b = np.clip(dv - junta, 0, None)
    d = raio - np.sqrt(np.clip(raio - a, 0, None) ** 2 + np.clip(raio - b, 0, None) ** 2)
    return S(d / raio, 0.0, 1.0)


def por_peca(ident, semente, lo=-1.0, hi=1.0):
    """Um sorteio por peça (o tom de cada tijolo)."""
    return np.random.default_rng(semente).uniform(lo, hi, int(ident.max()) + 1)[ident]


def trincas(semente, de=2, ate=9, fino=0.012):
    """Trincas: o risco fino onde um ruído largo passa por zero (0 fora, 1 no meio da trinca)."""
    r = ruido(semente, de, ate, 1.2) + ruido(semente + 1, 12, 60, 1.0) * 0.18
    return 1.0 - S(np.abs(r), 0.0, fino * 6)


def tinta(r=0.0, g=0.0, b=0.0):
    """A camada de tinta (N × N × 3) em volta do neutro: r, g, b são o desvio de cada canal (escalar ou imagem)."""
    return np.clip(np.stack([0.5 + r + np.zeros((N, N)), 0.5 + g + np.zeros((N, N)), 0.5 + b + np.zeros((N, N))], -1), 0, 1)


def pronta(h, forca, claro, cor=None, asp=None, ao=None):
    """Fecha uma camada: (nx, ny, oclusão, claro-escuro, aspereza, altura, tinta) a partir da altura ``h`` (0..1)."""
    nx, ny = relevo(h * 6.0, forca)
    if ao is None: ao = np.clip(0.6 + 0.4 * S(h, 0.0, 0.55) - np.clip(borrar(h, 5) - h, 0, 1) * 0.5, 0, 1)
    if asp is None: asp = faixa(0.6 - h * 0.3 + ruido(977, 3, 40, 1.0) * 0.15)
    return nx, ny, ao, np.clip(claro, 0, 1), np.clip(asp, 0, 1), np.clip(h, 0, 1), (tinta() if cor is None else cor)


# ---------------------------------------------------------------- as camadas
def asfalto():
    grao = ruido(101, 90, 420, 0.2); pedra = S(ruido(102, 40, 160, 0.4), 0.9, 1.6); tr = trincas(103, 1, 4, 0.008) * S(ruido(106, 1, 3, 1.0), 0.2, 0.9); remendo = S(ruido(104, 1, 5, 1.6), 0.55, 0.62)
    h = 0.55 + grao * 0.07 + pedra * 0.12 - tr * 0.5 + remendo * 0.04
    claro = 0.5 + grao * 0.025 + pedra * 0.05 - tr * 0.22 - remendo * 0.06 + ruido(105, 2, 12, 1.4) * 0.03
    return pronta(h, 0.9, claro, tinta(pedra * 0.01, 0, -pedra * 0.01), asp=faixa(0.8 + grao * 0.1 - remendo * 0.25))


def laje():
    # piso de concreto em placas de 2 × 2 por repetição, com a junta serrada, uma ou outra trinca e a mancha de uso
    ident, du, dv, fu, fv = blocos(2, 2, 111, desloc=0.0)
    h = S(np.minimum(du, dv), 0.0015, 0.006); tr = trincas(112, 1, 4, 0.008) * S(ruido(113, 1, 3, 1.0), 0.3, 0.9); poro = np.maximum(ruido(114, 120, 500, 0.0) - 2.2, 0)
    tom = por_peca(ident, 115); liso = ruido(116, 3, 30, 1.3)
    alt = h * (0.9 + liso * 0.02) - tr * 0.35 - poro * 0.15
    claro = 0.5 + tom * 0.035 * h - (1 - h) * 0.2 - tr * 0.16 + liso * 0.03 - poro * 0.05
    return pronta(alt, 0.8, claro, tinta(tom * 0.006, 0, -tom * 0.008), asp=faixa(0.7 + liso * 0.25 - tom * 0.1))


def concreto():
    # parede de concreto aparente: painéis de fôrma, os furos dos tirantes, o escorrido da chuva
    ident, du, dv, fu, fv = blocos(2, 2, 121, desloc=0.0)
    h = S(np.minimum(du, dv), 0.001, 0.005)
    furo = np.zeros((N, N))
    for cx in (0.2, 0.8):
        for cy in (0.25, 0.75): furo = np.maximum(furo, 1 - S(np.hypot(fu - cx, fv - cy), 0.012, 0.022))
    escorrido = S(borrar(np.repeat(ruido(122, 6, 60, 0.8)[:1], N, 0) * 1.0 + ruido(123, 2, 20, 1.2) * 0.5, 2), 0.7, 1.6) * (1 - fv * 0.6)
    liso = ruido(124, 4, 50, 1.2); poro = np.maximum(ruido(125, 150, 500, 0.0) - 2.3, 0)
    alt = h * 0.9 - furo * 0.6 - poro * 0.2 + liso * 0.02
    claro = 0.5 + por_peca(ident, 126) * 0.03 * h - (1 - h) * 0.14 - furo * 0.2 - escorrido * 0.07 + liso * 0.03
    return pronta(alt, 0.8, claro, tinta(-escorrido * 0.01, -escorrido * 0.005, 0), asp=faixa(0.75 + liso * 0.2 - escorrido * 0.2))


def _descascado(semente, quanto):
    """Onde o reboco caiu (1) e a borda do que ficou (para o degrau da casca)."""
    r = ruido(semente, 1.5, 7, 1.3) + ruido(semente + 1, 8, 40, 1.0) * 0.22
    return S(r, quanto, quanto + 0.06), S(r, quanto - 0.25, quanto)


def reboco():
    # reboco pintado: a mão da desempenadeira, trincas finas e uns pedaços caídos com o tijolo aparecendo
    caiu, beira = _descascado(131, 1.25)
    ident, du, dv, fu, fv = blocos(14, 5, 132, var_larg=0.1); tij = chanfro(du, dv, 0.004, 0.008)
    mao = ruido(133, 2, 26, 1.5, esticar=(0.6, 1.0)); tr = trincas(134, 2, 6, 0.006) * (1 - caiu)
    h = (1 - caiu) * (0.8 + mao * 0.03 - tr * 0.3) + caiu * (0.25 + tij * 0.3)
    claro = 0.5 + (1 - caiu) * (mao * 0.035 - tr * 0.14 + beira * (1 - caiu) * 0.03) + caiu * (-0.13 + tij * 0.08 + por_peca(ident, 135) * 0.05 * tij)
    cor = tinta(caiu * (0.07 + tij * 0.03), -caiu * 0.02, -caiu * 0.08)
    return pronta(h, 1.0, claro, cor, asp=faixa(0.8 + mao * 0.1 + caiu * 0.1))


def tijolo():
    # tijolo aparente: fiadas amarradas, cada tijolo de um tom, quina gasta, uns lascados e uns afundados
    ident, du, dv, fu, fv = blocos(14, 5, 141, var_larg=0.14)
    lasca = ruido(142, 5, 50, 1.0) * 0.0035
    h0 = chanfro(du + lasca, dv + lasca * 0.6, 0.0045, 0.011)
    tom = por_peca(ident, 143); cor_ = por_peca(ident, 144); fundo = por_peca(ident, 145) > 0.84; face = ruido(146, 20, 200, 0.9)
    h = h0 * (1.0 - 0.3 * fundo) * (0.92 + face * 0.03 + (fu - 0.5) * por_peca(ident, 147) * 0.06)
    quina = S(h0, 0.35, 0.8) * (1 - S(h0, 0.8, 1.0))
    claro = 0.5 + h0 * (tom * 0.085 - 0.02 * fundo) - (1 - h0) * 0.12 + quina * 0.05 + face * 0.02 * h0
    cor = tinta(h0 * cor_ * 0.05 - (1 - h0) * 0.03, h0 * cor_ * 0.012, -h0 * cor_ * 0.05 + (1 - h0) * 0.03)
    return pronta(h, 1.0, claro, cor, asp=faixa(0.75 + face * 0.1 - h0 * 0.1))


def metal_pintado():
    # chapa pintada: painéis com a emenda, rebites, riscos e a tinta lascada com ferrugem
    ident, du, dv, fu, fv = blocos(2, 2, 151, desloc=0.5)
    h = S(np.minimum(du, dv), 0.001, 0.006); reb = np.zeros((N, N))
    for k in np.linspace(0.08, 0.92, 7):
        for (a, b) in ((fu - k, fv - 0.06), (fu - k, fv - 0.94), (fu - 0.04, fv - k), (fu - 0.96, fv - k)):
            reb = np.maximum(reb, 1 - S(np.hypot(a, b * 1.0), 0.010, 0.019))
    risco = np.maximum(ruido(152, 4, 200, 0.3, esticar=(0.02, 1.0)) - 2.5, 0) + np.maximum(ruido(153, 4, 200, 0.3, esticar=(1.0, 0.04)) - 2.8, 0)
    lasc = S(ruido(154, 3, 30, 1.1) + (1 - S(np.minimum(du, dv), 0.0, 0.03)) * 0.9, 1.35, 1.5)
    amass = ruido(155, 1, 6, 1.6)
    alt = h * 0.8 + reb * 0.25 - lasc * 0.12 - risco * 0.05 + amass * 0.03
    claro = 0.5 + por_peca(ident, 156) * 0.02 - (1 - h) * 0.16 + reb * 0.04 - lasc * 0.14 + np.clip(risco, 0, 1) * 0.07 + amass * 0.02
    cor = tinta(lasc * 0.07, -lasc * 0.01, -lasc * 0.07)
    return pronta(alt, 0.7, claro, cor, asp=faixa(0.35 + lasc * 0.5 + risco * 0.2 + amass * 0.05))


def metal_cru():
    escova = ruido(11, 40, 500, 0.6, esticar=(0.04, 1.0))
    riscos = np.maximum(ruido(12, 6, 220, 0.3, esticar=(0.02, 1.0)) - 2.3, 0) + np.maximum(ruido(13, 6, 220, 0.3, esticar=(1.0, 0.03)) - 2.6, 0)
    gasto = ruido(15, 2, 14, 1.2); ferr = S(ruido(16, 3, 40, 1.1), 1.2, 1.7)
    h = 0.6 + escova * 0.03 - riscos * 0.1 - ferr * 0.05
    claro = 0.5 + escova * 0.04 + gasto * 0.05 - riscos * 0.06 - ferr * 0.1
    return pronta(h, 0.6, claro, tinta(ferr * 0.09, 0, -ferr * 0.08), asp=faixa(gasto * 0.7 + riscos * 2.0 + escova * 0.25 + ferr * 2.0), ao=np.ones((N, N)))


def vidro():
    marcas = ruido(22, 3, 60, 1.1, esticar=(0.25, 1.0)); po = ruido(23, 150, 500, 0.0); onda = borrar(ruido(21, 1, 5, 1.5), 6)
    suj = np.clip(marcas * 0.5 + 0.2, 0, None) + np.maximum(po - 2.0, 0) * 0.6
    return pronta(0.5 + onda * 0.4, 0.5, 0.5 - suj * 0.03, asp=faixa(suj), ao=np.ones((N, N)))


def madeira():
    # tábuas: cada uma de um tom, o veio ao comprido, a emenda de topo, os pregos e a quina gasta
    ident, du, dv, fu, fv = blocos(8, 2, 161, desloc=0.37, var_larg=0.5, var_alt=0.12)
    h0 = chanfro(du, dv, 0.002, 0.006)
    veio = ruido(162, 3, 260, 0.7, esticar=(0.035, 1.0)); no = S(ruido(163, 6, 22, 1.0), 1.9, 2.3)
    tom = por_peca(ident, 164); prego = np.maximum(1 - S(np.hypot((fu - 0.04) * 0.5, fv - 0.3), 0.0, 0.03), 1 - S(np.hypot((fu - 0.96) * 0.5, fv - 0.7), 0.0, 0.03))
    h = h0 * (0.85 + veio * 0.035 - no * 0.1) - prego * 0.1
    claro = 0.5 + h0 * (tom * 0.075 + veio * 0.035 - no * 0.12) - (1 - h0) * 0.2 - prego * 0.12
    cor = tinta(h0 * tom * 0.03, h0 * tom * 0.008, -h0 * tom * 0.03)
    return pronta(h, 0.9, claro, cor, asp=faixa(0.7 + veio * 0.1 - tom * 0.05))


def folhagem():
    a = ruido(31, 10, 40, 0.8); b = ruido(32, 40, 160, 0.6)
    h = np.abs(a) * 0.5 + np.abs(b) * 0.3
    return pronta(borrar(h, 1.2) * 0.5, 1.0, 0.5 + (h - h.mean()) * 0.11 / h.std(), tinta(a * 0.006, a * 0.01, -a * 0.008), asp=faixa(-h), ao=np.clip(0.75 + h * 0.2, 0, 1))


def areia():
    # areia: as ondas do vento (largas, tortas), o grão e umas pedrinhas
    torto = ruido(171, 1, 5, 1.5) * 0.5; yy = (np.arange(N)[:, None] / N + torto * 0.05)
    onda = np.sin(yy * 2 * np.pi * 18 + ruido(172, 2, 9, 1.3) * 1.6) * S(ruido(173, 1, 4, 1.5), -0.6, 0.8)
    grao = ruido(174, 150, 500, 0.0); pedra = S(ruido(175, 30, 110, 0.5), 1.7, 2.2); duna = ruido(176, 1, 6, 1.6)
    h = 0.5 + onda * 0.06 + grao * 0.012 + pedra * 0.2 + duna * 0.05
    claro = 0.5 + onda * 0.03 + pedra * 0.05 + duna * 0.035 + grao * 0.012
    return pronta(h, 0.9, claro, tinta(duna * 0.012, 0, -duna * 0.015 - pedra * 0.02), asp=faixa(0.9 - pedra * 0.3))


def rocha():
    # rocha: camadas largas, facetas e fendas
    cam = ruido(181, 2, 10, 1.3, esticar=(0.35, 1.0)); fac = ruido(182, 6, 40, 1.4); tr = trincas(183, 2, 8, 0.01) * S(ruido(184, 1, 4, 1.0), 0.0, 0.8)
    veio = np.sin((np.arange(N)[:, None] / N) * 2 * np.pi * 7 + cam * 1.8)          # as camadas da pedra, deitadas e tortas
    h = 0.5 + cam * 0.08 + veio * 0.07 + fac * 0.05 - tr * 0.3
    claro = 0.5 + veio * 0.03 + cam * 0.03 + fac * 0.035 - tr * 0.15
    return pronta(h, 1.0, claro, tinta(fac * 0.012, 0, -fac * 0.015), asp=faixa(0.8 + fac * 0.1))


def tecido():
    x = np.arange(N) * 2 * np.pi / N; fios = 96
    u = np.sin(x * fios)[None, :]; v = np.sin(x * fios)[:, None]
    cruza = np.sign(np.sin(x * fios / 2)[None, :] * np.sin(x * fios / 2)[:, None])
    h = np.where(cruza > 0, np.abs(u), np.abs(v)) * 0.25 + ruido(41, 20, 300, 0.5) * 0.03; dobra = ruido(42, 1, 6, 1.5)
    return pronta(borrar(h, 0.8) + 0.4 + dobra * 0.08, 1.0, 0.5 + (h - h.mean()) * 0.2 + dobra * 0.05, asp=faixa(-h), ao=np.clip(0.8 + h, 0, 1))


def fino():
    h = ruido(51, 60, 500, 0.4) * 0.02 + ruido(52, 4, 40, 1.0) * 0.02
    return pronta(0.5 + h, 0.5, 0.5 + h * 1.6, asp=faixa(ruido(53, 3, 60, 1.0)), ao=np.ones((N, N)))


def adobe():
    # a parede do deserto: reboco de barro grosso, com panos grandes caídos e o bloco de adobe aparecendo
    caiu, beira = _descascado(191, 0.55)
    ident, du, dv, fu, fv = blocos(10, 3, 192, var_larg=0.18, var_alt=0.1); blo = chanfro(du + ruido(193, 5, 40, 1.0) * 0.004, dv, 0.006, 0.016)
    mao = ruido(194, 2, 20, 1.5); palha = np.maximum(ruido(195, 30, 300, 0.4, esticar=(0.1, 1.0)) - 1.8, 0); tr = trincas(196, 2, 6, 0.007) * (1 - caiu)
    h = (1 - caiu) * (0.82 + mao * 0.04 - tr * 0.3 - palha * 0.03) + caiu * (0.2 + blo * 0.38)
    claro = 0.5 + (1 - caiu) * (mao * 0.04 - tr * 0.13 + beira * 0.035) + caiu * (-0.1 + blo * 0.09 + por_peca(ident, 197) * 0.055 * blo)
    cor = tinta(caiu * (0.035 + blo * 0.02), -caiu * 0.01, -caiu * 0.06)
    return pronta(h, 1.0, claro, cor, asp=faixa(0.85 + mao * 0.1))


def calcamento():
    # calçamento de pedra: blocos desiguais de quina redonda, cada um de um tom, a junta funda (com musgo, se o shader quiser)
    ident, du, dv, fu, fv = blocos(6, 4, 201, desloc=0.45, var_larg=0.45, var_alt=0.25)
    lasca = ruido(202, 4, 40, 1.0) * 0.006
    h0 = chanfro(du + lasca, dv + lasca, 0.006, 0.024)
    tom = por_peca(ident, 203); cor_ = por_peca(ident, 204); face = ruido(205, 8, 80, 1.2); incl = (fu - 0.5) * por_peca(ident, 206) + (fv - 0.5) * por_peca(ident, 207)
    h = h0 * (0.85 + face * 0.03 + incl * 0.1 + por_peca(ident, 208) * 0.06)
    quina = S(h0, 0.3, 0.75) * (1 - S(h0, 0.75, 1.0))
    claro = 0.5 + h0 * (tom * 0.09 + face * 0.025) - (1 - h0) * 0.13 + quina * 0.05
    cor = tinta(h0 * cor_ * 0.03 - (1 - h0) * 0.05, h0 * cor_ * 0.005 + (1 - h0) * 0.02, -h0 * cor_ * 0.035 - (1 - h0) * 0.06)
    return pronta(h, 1.0, claro, cor, asp=faixa(0.6 + face * 0.15 - tom * 0.1 + (1 - h0) * 0.3))


def terra():
    # terra batida: o barro rachado, pedrinhas e a marca de pisada
    tr = trincas(211, 3, 10, 0.012); torrao = ruido(212, 4, 30, 1.3); pedra = S(ruido(213, 25, 90, 0.5), 1.5, 2.0); grao = ruido(214, 120, 500, 0.0)
    h = 0.5 + torrao * 0.07 - tr * 0.35 + pedra * 0.22 + grao * 0.012
    claro = 0.5 + torrao * 0.045 - tr * 0.17 + pedra * 0.06 + grao * 0.01
    return pronta(h, 0.95, claro, tinta(torrao * 0.015, 0, -torrao * 0.02 - pedra * 0.03), asp=faixa(0.9 - pedra * 0.3))


def telha():
    # telha de barro: fiadas de canal e capa, cada telha de um tom, a de baixo coberta pela de cima
    ident, du, dv, fu, fv = blocos(6, 8, 221, desloc=0.0)
    onda = np.sin(fu * np.pi) ** 0.8; degrau = fv ** 1.5          # a capa redonda; a telha sobe até a ponta de baixo (v cresce para baixo)
    h = 0.15 + onda * 0.5 + degrau * 0.3; tom = por_peca(ident, 222); cor_ = por_peca(ident, 223); gasto = ruido(224, 6, 60, 1.1)
    sombra = S(fv, 0.0, 0.12)                                     # a sombra da telha de cima no começo da de baixo
    claro = 0.5 + tom * 0.09 - (1 - sombra) * 0.2 - (1 - onda) * 0.1 + gasto * 0.025
    cor = tinta(tom * 0.02 + cor_ * 0.04, cor_ * 0.01, -cor_ * 0.04)
    ao = np.clip(0.5 + 0.5 * sombra * (0.5 + 0.5 * onda), 0, 1)
    return pronta(h, 1.0, claro, cor, asp=faixa(0.7 + gasto * 0.2), ao=ao)


def brita():
    # brita (o lastro da linha do trem): pedras miúdas, cada uma de um tom
    a = ruido(231, 18, 60, 0.3); b = ruido(232, 40, 140, 0.3)
    h = np.clip(np.maximum(a, b * 0.8) * 0.25 + 0.4, 0, 1); tom = ruido(233, 14, 50, 0.3)
    claro = 0.5 + (h - 0.45) * 0.45 + tom * 0.03
    return pronta(h, 1.0, claro, tinta(tom * 0.012, 0, -tom * 0.012), asp=faixa(0.9 - h * 0.2), ao=np.clip(0.3 + h * 1.1, 0, 1))


# A ordem é o índice da camada (o mesmo das tabelas do shader).
CAMADAS = [asfalto, laje, concreto, reboco, tijolo, metal_pintado, metal_cru, vidro, madeira, folhagem,
           areia, rocha, tecido, fino, adobe, calcamento, terra, telha, brita, fino]
COLUNAS, LINHAS = 5, 4

IMPORT = """[remap]

importer="2d_array_texture"
type="CompressedTexture2DArray"

[params]

compress/mode=2
compress/high_quality={alta}
compress/lossy_quality=0.7
compress/hdr_compression=1
compress/channel_pack=1
mipmaps/generate=true
mipmaps/limit=-1
slices/horizontal={colunas}
slices/vertical={linhas}
"""


def montar():
    os.makedirs(DESTINO, exist_ok=True)
    mn = np.zeros((LINHAS * N, COLUNAS * N, 3), np.uint8); mm = np.zeros_like(mn); mc = np.zeros_like(mn)
    for k, camada in enumerate(CAMADAS):
        nx, ny, ao, claro, asp, alt, cor = camada()
        y, x = (k // COLUNAS) * N, (k % COLUNAS) * N; b8 = lambda v: np.clip(v * 255 + 0.5, 0, 255).astype(np.uint8)
        mn[y:y + N, x:x + N] = b8(np.stack([nx * 0.5 + 0.5, ny * 0.5 + 0.5, ao], -1)); mm[y:y + N, x:x + N] = b8(np.stack([claro, asp, alt], -1)); mc[y:y + N, x:x + N] = b8(cor)
        print(f"  camada {k:2d}: {camada.__name__}")
    Image.fromarray(mn).save(os.path.join(DESTINO, "detalhe_n.jpg"), quality=90, subsampling=0)
    Image.fromarray(mm).save(os.path.join(DESTINO, "detalhe_m.jpg"), quality=88, subsampling=0)
    Image.fromarray(mc).save(os.path.join(DESTINO, "detalhe_c.jpg"), quality=88)
    # as manchas largas: dois ruídos de tamanhos diferentes (R e G), para o shader misturar em duas escalas
    macro = np.stack([faixa(ruido(61, 1, 24, 1.4, n=512), 1, 99), faixa(ruido(62, 2, 48, 1.1, n=512), 1, 99), faixa(ruido(63, 1, 10, 1.6, n=512), 1, 99)], -1)
    Image.fromarray((macro * 255 + 0.5).astype(np.uint8)).save(os.path.join(DESTINO, "macro.png"))
    # o .import: as fatias do mosaico (o Godot acrescenta o uid e os caminhos dele ao importar; se as fatias mudaram, refaz)
    for arq, alta in (("detalhe_n.jpg", "true"), ("detalhe_m.jpg", "false"), ("detalhe_c.jpg", "false")):
        imp = os.path.join(DESTINO, arq + ".import"); velho = open(imp, encoding="utf-8").read() if os.path.exists(imp) else ""
        if f"slices/horizontal={COLUNAS}\n" not in velho or f"slices/vertical={LINHAS}\n" not in velho:
            import re as _re
            uid = _re.search(r'^uid="[^"]+"\n', velho, _re.M); novo = IMPORT.format(alta=alta, colunas=COLUNAS, linhas=LINHAS)
            if uid: novo = novo.replace('type="CompressedTexture2DArray"\n', 'type="CompressedTexture2DArray"\n' + uid.group(0))
            with open(imp, "w", encoding="utf-8") as fh: fh.write(novo)
    print(f"  -> {os.path.relpath(DESTINO, RAIZ)}/detalhe_n.jpg, detalhe_m.jpg, detalhe_c.jpg, macro.png")


if __name__ == "__main__":
    montar()
