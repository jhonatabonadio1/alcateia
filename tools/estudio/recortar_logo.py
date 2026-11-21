#!/usr/bin/env python3
"""Recorta a logo da Initech Games (tools/estudio/initech_games.svg) nas três
partes que a vinheta anima (scripts/ui/StudioIntro.cs) e faz o brilho do
emblema. Rodar da raiz: python3 tools/estudio/recortar_logo.py
Precisa do rsvg-convert (librsvg) e do Pillow.

As frações impressas no fim são as das constantes EmblemRect, NameRect,
GamesRect e GlowRect da vinheta: mudou a logo, atualize lá.
"""
import subprocess, tempfile, os
import numpy as np
from PIL import Image, ImageFilter

SVG = "tools/estudio/initech_games.svg"
SAIDA = "assets/ui/estudio"
LARGURA = 1800

with tempfile.TemporaryDirectory() as d:
    png = os.path.join(d, "logo.png")
    subprocess.run(["rsvg-convert", "-w", str(LARGURA), SVG, "-o", png], check=True)
    im = Image.open(png).convert("RGBA")
a = np.asarray(im)[..., 3]
W, H = im.size

# As três faixas com desenho, de cima para baixo: emblema, INITECH, GAMES.
linhas = (a > 10).any(1)
faixas, ini = [], None
for y, tem in enumerate(linhas):
    if tem and ini is None: ini = y
    if not tem and ini is not None: faixas.append((ini, y)); ini = None
if ini is not None: faixas.append((ini, H))
assert len(faixas) == 3, faixas

caixas = {}
for nome, (y0, y1) in zip(("emblema", "initech", "games"), faixas):
    cols = np.where((a[y0:y1] > 10).any(0))[0]
    p = 8
    caixa = (max(0, cols[0] - p), max(0, y0 - p), min(W, cols[-1] + 1 + p), min(H, y1 + p))
    im.crop(caixa).save(f"{SAIDA}/{nome}.png", optimize=True)
    caixas[nome] = caixa

# O brilho: o emblema desfocado, em ouro, com sobra para o desfoque; metade do tamanho.
x0, y0, x1, y1 = caixas["emblema"]
e = np.asarray(Image.open(f"{SAIDA}/emblema.png"))[..., 3]
pad = 140
alfa = np.zeros((e.shape[0] + 2 * pad, e.shape[1] + 2 * pad), np.uint8)
alfa[pad:pad + e.shape[0], pad:pad + e.shape[1]] = e
alfa = np.clip(np.asarray(Image.fromarray(alfa).filter(ImageFilter.GaussianBlur(38))).astype(float) * 1.6, 0, 255).astype(np.uint8)
brilho = np.zeros(alfa.shape + (4,), np.uint8)
brilho[..., 0], brilho[..., 1], brilho[..., 2], brilho[..., 3] = 254, 200, 3, alfa
Image.fromarray(brilho).resize((alfa.shape[1] // 2, alfa.shape[0] // 2), Image.LANCZOS).save(f"{SAIDA}/emblema_brilho.png", optimize=True)
caixas["emblema_brilho"] = (x0 - pad, y0 - pad, x1 + pad, y1 + pad)

for nome, (x0, y0, x1, y1) in caixas.items():
    print(f"{nome:15} new({x0 / W:.5f}f, {y0 / H:.5f}f, {(x1 - x0) / W:.5f}f, {(y1 - y0) / H:.5f}f)")
