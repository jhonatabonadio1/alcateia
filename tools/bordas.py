#!/usr/bin/env python3
"""Gera as bordas de nível (assets/ui/bordas/M_T.png), no estilo do Overwatch.

    python3 tools/bordas.py              # grava em assets/ui/bordas/
    python3 tools/bordas.py --prancha    # e a prancha de conferência em /tmp

M é o metal (0 Iniciante, 1 Bronze, 2 Prata, 3 Ouro, 4 Platina, 5 Diamante,
6 Mestre) e T o desenho dentro do metal (0 a 9, um a cada 10 níveis):

    0 aro liso          5 + penas nos lados
    1 aro duplo         6 + penas em cima e joia
    2 + divisa embaixo  7 asas grandes (quatro penas) e lados maiores
    3 + asas pequenas   8 louros no lugar das asas
    4 + asas de 3 penas 9 louros cheios e coroa

Cada peça é pintada sozinha como metal polido (relevo por mapa de altura,
luz de cima à esquerda, brilho especular) e ganha um filete escuro em volta,
então pena sobre pena continua legível. O miolo do hexágono é vazado: é onde
o jogo desenha a foto de perfil (RankBadge.cs); número e estrelas o jogo
desenha por cima. 512 px, desenhado a 1024 e reduzido.

Precisa de: pip install pillow numpy
"""
import argparse
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

S = 1024
C = S / 2
R = 292          # raio do aro (vértice)
OUT = 512

# escuro, médio, claro, brilho
METAIS = [
    ((34, 38, 46), (92, 99, 110), (150, 158, 170), (206, 212, 222)),        # Iniciante (ferro)
    ((84, 42, 22), (176, 104, 62), (236, 168, 120), (255, 230, 204)),       # Bronze
    ((74, 84, 100), (164, 176, 192), (228, 234, 242), (255, 255, 255)),     # Prata
    ((112, 66, 8), (214, 156, 40), (255, 222, 120), (255, 250, 220)),       # Ouro
    ((18, 74, 96), (78, 178, 204), (176, 240, 250), (240, 255, 255)),       # Platina
    ((48, 26, 120), (136, 104, 236), (204, 188, 255), (248, 244, 255)),     # Diamante
    ((110, 18, 10), (232, 84, 40), (255, 176, 110), (255, 240, 214)),       # Mestre
]
LUZ = np.array([-0.45, -0.7, 0.55]); LUZ /= np.linalg.norm(LUZ)
MEIO = LUZ + np.array([0, 0, 1.0]); MEIO /= np.linalg.norm(MEIO)


# ---------------------------------------------------------------- formas

def hexagono(r, cx=C, cy=C):
    return [(cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a))) for a in range(-90, 270, 60)]


def pena(x, y, ang, comp, larg, curva=0.18):
    """Pena curva: sai de (x, y) no ângulo `ang` (graus, y para baixo), larga
    perto da base e afinando até a ponta, com a espinha arqueada por `curva`."""
    a = math.radians(ang)
    ux, uy = math.cos(a), math.sin(a)
    px, py = -uy, ux
    esq, dir_ = [], []
    n = 22
    for i in range(n + 1):
        t = i / n
        cx = x + ux * comp * t + px * comp * curva * math.sin(math.pi * t)
        cy = y + uy * comp * t + py * comp * curva * math.sin(math.pi * t)
        w = larg * (math.sin(math.pi * t / 0.7) if t < 0.35 else (1 - (t - 0.35) / 0.65) ** 0.8)
        w = max(w, 0)
        esq.append((cx + px * w * 0.55, cy + py * w * 0.55))
        dir_.append((cx - px * w, cy - py * w))
    return esq + dir_[::-1]


def folha(x, y, ang, comp, larg):
    """Folha de louro: ponta dos dois lados, simétrica."""
    a = math.radians(ang)
    ux, uy = math.cos(a), math.sin(a)
    px, py = -uy, ux
    pts_a, pts_b = [], []
    for i in range(19):
        t = i / 18
        w = larg * math.sin(math.pi * t) ** 0.9
        cx, cy = x + ux * comp * t, y + uy * comp * t
        pts_a.append((cx + px * w, cy + py * w))
        pts_b.append((cx - px * w, cy - py * w))
    return pts_a + pts_b[::-1]


def espelho(forma):
    return [(2 * C - x, y) for x, y in forma]


def par(forma):
    """A forma do lado direito e a espelhada à esquerda."""
    return [forma, espelho(forma)]


# ---------------------------------------------------------------- pintura

def _mascara(forma_ou_img):
    if isinstance(forma_ou_img, Image.Image):
        return forma_ou_img
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).polygon(forma_ou_img, fill=255)
    return m


def metal(mascara, m, relevo=7.0, altura=9, tom=1.0):
    """Pinta a máscara como metal polido. Devolve RGBA do tamanho da tela."""
    caixa = mascara.getbbox()
    if not caixa:
        return Image.new("RGBA", (S, S))
    pad = 30
    x0, y0 = max(caixa[0] - pad, 0), max(caixa[1] - pad, 0)
    x1, y1 = min(caixa[2] + pad, S), min(caixa[3] + pad, S)
    crop = mascara.crop((x0, y0, x1, y1))
    a = np.asarray(crop, dtype=np.float32) / 255.0
    h = np.asarray(crop.filter(ImageFilter.GaussianBlur(altura)), dtype=np.float32) / 255.0
    gy, gx = np.gradient(h)
    nx, ny, nz = -gx * relevo * 10, -gy * relevo * 10, np.ones_like(h)
    norma = np.sqrt(nx * nx + ny * ny + nz * nz)
    nx, ny, nz = nx / norma, ny / norma, nz / norma
    dif = np.clip(nx * LUZ[0] + ny * LUZ[1] + nz * LUZ[2], 0, 1)
    spec = np.clip(nx * MEIO[0] + ny * MEIO[1] + nz * MEIO[2], 0, 1) ** 36
    # reflexo do ambiente: mais claro em cima, com uma faixa de céu
    yy = (np.arange(y0, y1, dtype=np.float32)[:, None] / S)
    amb = 1.05 - 0.45 * yy + 0.18 * np.exp(-((yy - 0.36) ** 2) / 0.004)
    v = np.clip((0.22 + 0.9 * dif) * amb * tom, 0, 1.25)
    escuro, medio, claro, brilho = (np.array(c, dtype=np.float32) for c in METAIS[m])
    paradas = [(0.0, escuro * 0.55), (0.38, escuro), (0.66, medio), (0.92, claro), (1.25, brilho)]
    cor = np.zeros(v.shape + (3,), dtype=np.float32)
    for (t0, c0), (t1, c1) in zip(paradas, paradas[1:]):
        k = np.clip((v - t0) / (t1 - t0), 0, 1)[..., None]
        dentro = ((v >= t0) & (v <= t1))[..., None]
        cor = np.where(dentro, c0 + (c1 - c0) * k, cor)
    cor = np.clip(cor + spec[..., None] * 255 * 0.85, 0, 255)
    rgba = np.dstack([cor, a * 255]).astype(np.uint8)
    out = Image.new("RGBA", (S, S))
    out.paste(Image.fromarray(rgba, "RGBA"), (x0, y0))
    return out


def peca(tela, forma, m, contorno=5, sombra=True, **kw):
    """Uma peça: sombra, filete escuro em volta e o metal por cima."""
    mas = _mascara(forma)
    escuro = METAIS[m][0]
    if sombra:
        s = mas.filter(ImageFilter.GaussianBlur(9))
        cam = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        cam.paste(Image.new("RGBA", (S, S), (0, 0, 0, 140)), (6, 10), s)
        tela.alpha_composite(cam)
    if contorno:
        borda_ = mas.filter(ImageFilter.MaxFilter(contorno * 2 + 1))
        cam = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        cam.paste(Image.new("RGBA", (S, S), tuple(int(c * 0.45) for c in escuro) + (255,)), (0, 0), borda_)
        tela.alpha_composite(cam)
    tela.alpha_composite(metal(mas, m, **kw))


def anel(r_fora, r_dentro):
    m = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(m)
    d.polygon(hexagono(r_fora), fill=255)
    d.polygon(hexagono(r_dentro), fill=0)
    return m


# ---------------------------------------------------------------- desenhos

def asa(n, x, y, ang0, passo, comp, larg, encurta=0.13, curva=0.16):
    """Leque de n penas do lado direito; a de trás é a maior."""
    formas = []
    for i in range(n):
        formas.append(pena(x + i * 6, y - i * 14, ang0 + i * passo, comp * (1 - encurta * i), larg * (1 - 0.08 * i), curva=curva))
    return formas


def enfeites_atras(t):
    """Peças atrás do aro, na ordem de pintura (de trás para a frente)."""
    f = []
    if t >= 8:   # louros: o ramo e as folhas pelos lados, de baixo para cima
        ramo = Image.new("L", (S, S), 0)
        caixa = (C - R - 58, C - R - 58, C + R + 58, C + R + 58)
        ImageDraw.Draw(ramo).arc(caixa, -48, 82, fill=255, width=14)
        ImageDraw.Draw(ramo).arc(caixa, 98, 228, fill=255, width=14)
        f.append(ramo)
        for i in range(9):
            th = 78 - i * 15.5
            rr = R + 58
            x = C + rr * math.cos(math.radians(th))
            y = C + rr * math.sin(math.radians(th))
            comp = 150 - i * 5
            f += par(folha(x, y, th - 90 + 42, comp, comp * 0.27))
    if t >= 9:   # coroa: pontas em leque em cima
        for i in sorted(range(-3, 4), key=abs, reverse=True):
            comp = 150 - abs(i) * 22
            f.append(pena(C + i * 22, C - R + 28, -90 + i * 13, comp, 30, curva=0.0))
    if t in (5, 6):   # penas nos lados
        for fo in asa(2, C + R * 0.80, C - R * 0.42, -20, -22, 180, 42, curva=-0.12):
            f += par(fo)
    if t == 7:
        for fo in asa(3, C + R * 0.80, C - R * 0.40, -14, -21, 220, 46, curva=-0.12):
            f += par(fo)
    if t >= 6:   # penas em cima
        for fo in asa(2, C + 100, C - R * 0.72, -52, -16, 150, 34, curva=-0.12):
            f += par(fo)
    if 3 <= t <= 7:   # asas: nascem no vértice de baixo e abrem para fora, subindo
        vx, vy = C + R * 0.80, C + R * 0.56
        if t == 3:
            fan = asa(2, vx, vy, 12, -22, 170, 42, curva=-0.14)
        elif t <= 6:
            fan = asa(3, vx, vy, 18, -21, 225, 48, curva=-0.15)
        else:
            fan = asa(4, vx - 6, vy + 6, 24, -19, 280, 52, curva=-0.16)
        for fo in fan:
            f += par(fo)
    return f


def enfeites_frente(t):
    f = []
    if t >= 2:   # divisa embaixo, por cima do aro
        f.append([(C - 74, C + R - 30), (C, C + R + 22), (C + 74, C + R - 30), (C + 74, C + R + 4), (C, C + R + 58), (C - 74, C + R + 4)])
    if t >= 6:   # joia em cima
        top = C - R
        f.append([(C, top - 40), (C + 30, top + 2), (C, top + 40), (C - 30, top + 2)])
    return f


def borda(m, t):
    tela = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    for forma in enfeites_atras(t):
        peca(tela, forma, m, contorno=4, relevo=5.5, altura=7)

    grosso = 40 + (10 if t >= 1 else 0)
    peca(tela, anel(R + grosso / 2, R - grosso / 2), m, contorno=5, relevo=8.0, altura=11, tom=1.08)
    lip = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    if t >= 1:   # aro duplo
        peca(lip, anel(R - grosso / 2 - 4, R - grosso / 2 - 20), m, contorno=3, sombra=False, relevo=6.0, altura=4, tom=0.85)
    else:
        peca(lip, anel(R - grosso / 2 - 2, R - grosso / 2 - 10), m, contorno=2, sombra=False, relevo=5.0, altura=3, tom=0.8)
    tela.alpha_composite(lip)
    friso = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    h = hexagono(R + 2)
    ImageDraw.Draw(friso).line(h + [h[0]], fill=tuple(int(c * 0.5) for c in METAIS[m][0]) + (150,), width=3, joint="curve")
    tela.alpha_composite(friso)

    for forma in enfeites_frente(t):
        peca(tela, forma, m, contorno=4, relevo=6.0, altura=7, tom=1.1)

    if m == 6:   # Mestre: brasa em volta
        alfa = tela.split()[3].filter(ImageFilter.GaussianBlur(28))
        brasa = Image.new("RGBA", (S, S), (255, 96, 30, 0))
        brasa.putalpha(alfa.point(lambda v: int(v * 0.6)))
        brasa.alpha_composite(tela)
        tela = brasa
    return tela.resize((OUT, OUT), Image.LANCZOS)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--saida", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "assets", "ui", "bordas"))
    p.add_argument("--prancha", action="store_true")
    p.add_argument("--so", help="só um metal (0-6), para testar")
    a = p.parse_args()
    os.makedirs(a.saida, exist_ok=True)
    metais = [int(a.so)] if a.so else range(len(METAIS))
    todas = {}
    for m in metais:
        for t in range(10):
            img = borda(m, t)
            img.save(os.path.join(a.saida, f"{m}_{t}.png"), optimize=True)
            todas[(m, t)] = img
    print(f"{len(todas)} bordas em {os.path.abspath(a.saida)}")
    if a.prancha:
        cel = 160
        linhas = sorted({m for m, _ in todas})
        pr = Image.new("RGBA", (cel * 10, cel * len(linhas)), (10, 17, 25, 255))
        for (m, t), img in todas.items():
            pr.alpha_composite(img.resize((cel, cel), Image.LANCZOS), (t * cel, linhas.index(m) * cel))
        pr.save("/tmp/prancha_bordas.png")
        print("prancha em /tmp/prancha_bordas.png")


if __name__ == "__main__":
    main()
