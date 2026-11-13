#!/usr/bin/env python3
"""
Gera as texturas dos mapas em assets/textures/pbr/: para cada material,
cor (_cor.jpg), relevo (_normal.jpg) e aspereza (_asp.jpg), 512 px, que
se repetem sem emenda.

Tudo é desenhado por conta: ruído, padrões e relevo calculados aqui, sem
foto nem arquivo de ninguém — não há licença de terceiro para cumprir.
Os tons claros e neutros (reboco, metal pintado, metal ondulado) existem
para o mapa tingir com a cor do material.

Precisa de numpy e Pillow:  pip install numpy pillow
Uso:  python3 tools/gerar_texturas.py
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "assets", "textures", "pbr")
N = 512


# ---------------------------------------------------------------- ruído

def fbm(beta, seed, stretch=(1.0, 1.0)):
    """Ruído que se repete sem emenda, de 0 a 1: espectro 1/f^beta pela
    transformada de Fourier (periódica por construção). beta alto dá
    manchas largas; baixo, grão fino. stretch estica num eixo (veio)."""
    rng = np.random.default_rng(seed)
    fy = np.fft.fftfreq(N)[:, None] * stretch[0]
    fx = np.fft.fftfreq(N)[None, :] * stretch[1]
    f = np.sqrt(fx * fx + fy * fy)
    f[0, 0] = 1.0
    spectrum = (rng.normal(size=(N, N)) + 1j * rng.normal(size=(N, N))) / f ** (beta / 2.0)
    spectrum[0, 0] = 0
    out = np.fft.ifft2(spectrum).real
    out -= out.min()
    return out / out.max()


def contrast(x, amount):
    return np.clip((x - 0.5) * amount + 0.5, 0, 1)


def grid():
    v, u = np.mgrid[0:N, 0:N] / float(N)
    return u, v


def tiles(nx, ny, gap, offset_rows=False, seed=0):
    """Ladrilhos ou tijolos: devolve (junta 0..1, id aleatório por peça,
    distância à borda da peça). offset_rows desloca fileiras alternadas
    meio tijolo."""
    u, v = grid()
    row = np.floor(v * ny)
    uu = u * nx + (0.5 * (row % 2) if offset_rows else 0)
    col = np.floor(uu) % nx
    fu, fv = uu - np.floor(uu), v * ny - row
    edge = np.minimum(np.minimum(fu, 1 - fu) / nx * ny if False else np.minimum(fu, 1 - fu) * (1.0 / nx) * N,
                      np.minimum(fv, 1 - fv) * (1.0 / ny) * N)
    joint = np.clip(1.0 - edge / gap, 0, 1)
    rng = np.random.default_rng(seed)
    table = rng.random((ny, nx))
    ident = table[row.astype(int) % ny, col.astype(int)]
    return joint, ident, edge


# ------------------------------------------------------------- escrita

def normal_from(height, strength):
    dx = np.roll(height, -1, 1) - np.roll(height, 1, 1)
    dy = np.roll(height, -1, 0) - np.roll(height, 1, 0)
    nx, ny, nz = -dx * strength, dy * strength, np.ones_like(height)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([nx / length, ny / length, nz / length], -1) * 0.5 + 0.5


def save(name, albedo, height, rough, bump=6.0):
    os.makedirs(OUT, exist_ok=True)
    to8 = lambda a: (np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)
    Image.fromarray(to8(albedo), "RGB").save(os.path.join(OUT, f"{name}_cor.jpg"), quality=90)
    Image.fromarray(to8(normal_from(height, bump)), "RGB").save(os.path.join(OUT, f"{name}_normal.jpg"), quality=94)
    Image.fromarray(to8(rough), "L").save(os.path.join(OUT, f"{name}_asp.jpg"), quality=88)
    print(f"  {name}")


def rgb(gray, tint):
    return gray[..., None] * np.array(tint)[None, None, :]


# ------------------------------------------------------------ materiais

def concreto():
    stains, grain = fbm(2.4, 1), fbm(0.6, 2)
    pores = (fbm(0.2, 3) > 0.985).astype(float)
    u, v = grid()
    seam = np.clip(1 - np.minimum(np.abs((v * 2) % 1 - 0.0), 1 - (v * 2) % 1) * N / 2 / 2.0, 0, 1)
    g = 0.56 + (stains - 0.5) * 0.20 + (grain - 0.5) * 0.07 - pores * 0.18 - seam * 0.10
    h = 0.5 + (stains - 0.5) * 0.15 + (grain - 0.5) * 0.10 - pores * 0.5 - seam * 0.4
    save("concreto", rgb(g, (1.0, 0.99, 0.96)), h, 0.82 + (grain - 0.5) * 0.2 - pores * 0.1, 5)


def reboco():
    trowel, grain, crack = fbm(2.0, 11), fbm(0.8, 12), fbm(1.6, 13)
    cracks = np.clip(1 - np.abs(crack - 0.5) * 90, 0, 1) * (fbm(2.6, 14) > 0.62)
    g = 0.84 + (trowel - 0.5) * 0.12 + (grain - 0.5) * 0.05 - cracks * 0.25
    h = 0.5 + (trowel - 0.5) * 0.5 + (grain - 0.5) * 0.12 - cracks * 0.6
    save("reboco", rgb(g, (1.0, 0.985, 0.95)), h, 0.9 + (grain - 0.5) * 0.1, 4)


def tijolo():
    joint, ident, _ = tiles(8, 20, 2.2, True, 21)
    face, grain = fbm(1.6, 22), fbm(0.5, 23)
    brick = 0.42 + (ident - 0.5) * 0.22 + (face - 0.5) * 0.14 + (grain - 0.5) * 0.06
    col = rgb(brick, (1.55, 0.78, 0.56)) + rgb(ident * 0.06, (0.3, 0.2, 0.0))
    mortar = rgb(0.62 + (grain - 0.5) * 0.1, (1.0, 0.97, 0.9))
    j = joint[..., None]
    save("tijolo", col * (1 - j) + mortar * j, (1 - joint) * (0.75 + (face - 0.5) * 0.3), 0.8 + joint * 0.15, 7)


def bloco():
    joint, ident, _ = tiles(4, 8, 2.6, True, 31)
    face, grain = fbm(1.4, 32), fbm(0.3, 33)
    g = 0.55 + (ident - 0.5) * 0.08 + (face - 0.5) * 0.12 + (grain - 0.5) * 0.10
    g = g * (1 - joint) + 0.40 * joint
    save("bloco", rgb(g, (1.0, 1.0, 0.99)), (1 - joint) * (0.7 + (grain - 0.5) * 0.4), 0.9 + (grain - 0.5) * 0.1, 6)


def asfalto():
    stains, grit, crack = fbm(2.6, 41), fbm(0.15, 42), fbm(1.5, 43)
    stones = (grit > 0.72).astype(float)
    cracks = np.clip(1 - np.abs(crack - 0.5) * 110, 0, 1) * (fbm(2.4, 44) > 0.55)
    g = 0.17 + (stains - 0.5) * 0.07 + stones * 0.10 - cracks * 0.10
    save("asfalto", rgb(g, (1.0, 1.0, 1.04)), 0.5 + stones * 0.35 - cracks * 0.6, 0.92 - stones * 0.15, 5)


def azulejo():
    joint, ident, _ = tiles(8, 8, 1.6, False, 51)
    dirt = fbm(2.6, 52)
    u, v = grid()
    streak = contrast(fbm(2.2, 53, (0.15, 1.0)), 2.2) * (1 - v) * 0.10
    g = 0.90 + (ident - 0.5) * 0.05 - (dirt - 0.5) * 0.06 - streak
    g = g * (1 - joint) + 0.33 * joint
    save("azulejo", rgb(g, (1.0, 1.0, 0.98)), (1 - joint) * 0.9, 0.22 + joint * 0.65 + dirt * 0.12, 6)


def piso():
    joint, ident, _ = tiles(4, 4, 1.4, False, 61)
    speck, stains = fbm(0.1, 62), fbm(2.4, 63)
    g = 0.60 + (ident - 0.5) * 0.10 + (speck - 0.5) * 0.16 + (stains - 0.5) * 0.08
    g = g * (1 - joint) + 0.25 * joint
    save("piso", rgb(g, (1.0, 0.98, 0.95)), (1 - joint) * 0.9 + (speck - 0.5) * 0.04, 0.42 + joint * 0.4 + stains * 0.2, 5)


def metal_pintado():
    u, v = grid()
    seam = np.clip(1 - np.minimum(np.minimum(u * 2 % 1, 1 - u * 2 % 1), np.minimum(v % 1, 1 - v % 1)) * N / 2 / 2.0, 0, 1)
    rivets = np.zeros((N, N))
    for cx in (0.03, 0.47, 0.53, 0.97):
        for cy in np.arange(0.0625, 1, 0.125):
            rivets += np.exp(-(((u - cx) * N) ** 2 + ((v - cy) * N) ** 2) / 14.0)
    scratch = contrast(fbm(1.2, 71, (1.0, 0.04)), 3.0)
    wear = fbm(2.6, 72)
    g = 0.80 + (wear - 0.5) * 0.10 - (scratch > 0.93) * 0.10 - seam * 0.25 + rivets * 0.06
    save("metal_pintado", rgb(g, (1, 1, 1)), 0.5 - seam * 0.5 + rivets * 0.5, 0.42 + (wear - 0.5) * 0.25 + (scratch > 0.93) * 0.2, 5)


def metal_ondulado():
    u, v = grid()
    wave = 0.5 + 0.5 * np.sin(u * 2 * np.pi * 16)
    streak = contrast(fbm(2.4, 81, (0.08, 1.0)), 2.0)
    wear = fbm(2.6, 82)
    g = 0.74 + (wave - 0.5) * 0.16 - (streak - 0.5) * 0.14 + (wear - 0.5) * 0.08
    save("metal_ondulado", rgb(g, (1, 1, 1)), wave, 0.48 + (streak - 0.5) * 0.3, 9)


def ferrugem():
    a, b, grain = fbm(2.2, 91), fbm(1.4, 92), fbm(0.4, 93)
    mix = contrast(a, 2.4)[..., None]
    col = rgb(0.45 + (b - 0.5) * 0.3, (1.5, 0.72, 0.36)) * mix + rgb(0.22 + (grain - 0.5) * 0.1, (1.1, 0.9, 0.8)) * (1 - mix)
    save("ferrugem", col, 0.5 + (b - 0.5) * 0.5 + (grain - 0.5) * 0.2, 0.9 + (grain - 0.5) * 0.1, 6)


def madeira():
    joint, ident, _ = tiles(8, 1, 1.6, False, 101)
    veio = fbm(1.8, 102, (0.03, 1.0))
    fino = fbm(0.8, 103, (0.02, 1.0))
    g = 0.50 + (ident - 0.5) * 0.18 + (veio - 0.5) * 0.30 + (fino - 0.5) * 0.10
    col = rgb(g, (1.35, 0.95, 0.60))
    col = col * (1 - joint[..., None] * 0.75)
    save("madeira", col, (1 - joint) * (0.7 + (veio - 0.5) * 0.35), 0.72 + (fino - 0.5) * 0.2, 6)


def areia():
    dunes, ripple, grain = fbm(2.8, 111), fbm(1.6, 112, (1.0, 0.25)), fbm(0.2, 113)
    g = 0.70 + (dunes - 0.5) * 0.14 + (ripple - 0.5) * 0.10 + (grain - 0.5) * 0.10
    save("areia", rgb(g, (1.12, 0.96, 0.72)), 0.5 + (dunes - 0.5) * 0.3 + (ripple - 0.5) * 0.5 + (grain - 0.5) * 0.15, 0.95 + (grain - 0.5) * 0.08, 5)


def neve():
    drift, bump, sparkle = fbm(2.8, 121), fbm(1.7, 122), fbm(0.05, 123)
    g = 0.90 + (drift - 0.5) * 0.08 + (bump - 0.5) * 0.05 + (sparkle > 0.992) * 0.1
    col = rgb(g, (0.96, 0.98, 1.04))
    save("neve", col, 0.5 + (drift - 0.5) * 0.5 + (bump - 0.5) * 0.4, 0.62 + (bump - 0.5) * 0.2 - (sparkle > 0.992) * 0.4, 4)


def chapa_xadrez():
    u, v = grid()
    k = 16
    a, b = (u + v) * k % 1, (u - v) * k % 1
    diamond = np.clip(1 - (np.abs(a - 0.5) * 6 + np.abs(b - 0.5) * 1.4), 0, 1) + np.clip(1 - (np.abs(a - 0.5) * 1.4 + np.abs(b - 0.5) * 6), 0, 1) * (np.floor((u + v) * k) % 2)
    diamond = np.clip(diamond * 2.5, 0, 1)
    wear = fbm(2.4, 131)
    g = 0.62 + diamond * 0.12 + (wear - 0.5) * 0.12
    save("chapa_xadrez", rgb(g, (1, 1, 1.02)), diamond, 0.40 + (wear - 0.5) * 0.3 - diamond * 0.1, 7)


def rocha():
    big, ridge, grain = fbm(2.6, 141), fbm(1.9, 142), fbm(0.6, 143)
    ridged = 1 - np.abs(ridge - 0.5) * 2
    g = 0.46 + (big - 0.5) * 0.22 + (ridged - 0.5) * 0.20 + (grain - 0.5) * 0.08
    save("rocha", rgb(g, (1.0, 0.98, 0.94)), 0.5 + (big - 0.5) * 0.5 + (ridged - 0.5) * 0.6, 0.88 + (grain - 0.5) * 0.15, 8)


def pedra_calcada():
    joint, ident, _ = tiles(6, 12, 3.2, True, 151)
    face, grain = fbm(1.8, 152), fbm(0.4, 153)
    g = 0.50 + (ident - 0.5) * 0.20 + (face - 0.5) * 0.16 + (grain - 0.5) * 0.08
    g = g * (1 - joint) + 0.22 * joint
    save("pedra_calcada", rgb(g, (1.05, 0.98, 0.88)), (1 - joint) ** 0.5 * (0.7 + (face - 0.5) * 0.4), 0.78 + joint * 0.15 + (grain - 0.5) * 0.1, 8)


if __name__ == "__main__":
    print("texturas ->", os.path.relpath(OUT, ROOT))
    for make in (concreto, reboco, tijolo, bloco, asfalto, azulejo, piso, metal_pintado, metal_ondulado,
                 ferrugem, madeira, areia, neve, chapa_xadrez, rocha, pedra_calcada):
        make()
