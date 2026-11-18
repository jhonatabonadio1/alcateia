"""Prévia rápida do lobo: os texels como pontos, de frente, de costas e de perto."""
import sys
import numpy as np
from PIL import Image, ImageFilter


def vista(pos, nrm, cor, emis, yaw, altura=720, zoom=None, luz=(0.4, 0.7, 0.6), fundo=(26, 28, 36)):
    # nunca mais fino que 2,2 mm por pixel (senão os pontos não fecham): desenha menor e amplia
    y0_, y1_ = (-0.05, 1.75) if zoom is None else (zoom[2], zoom[3])
    real = min(altura, int((y1_ - y0_) / 0.0022))
    if real < altura:
        im = vista(pos, nrm, cor, emis, yaw, real, zoom, luz, fundo)
        return im.resize((int(im.width * altura / real), altura), Image.LANCZOS)
    c, s = np.cos(yaw), np.sin(yaw)
    x = pos[:, 0] * c + pos[:, 2] * s
    z = -pos[:, 0] * s + pos[:, 2] * c          # profundidade: maior = mais perto
    y = pos[:, 1]
    nx = nrm[:, 0] * c + nrm[:, 2] * s
    nz = -nrm[:, 0] * s + nrm[:, 2] * c
    ny = nrm[:, 1]
    if zoom is None:
        x0, x1, y0, y1 = -0.95, 0.95, -0.05, 1.75
    else:
        x0, x1, y0, y1 = zoom
    esc = altura / (y1 - y0)
    larg = int((x1 - x0) * esc)
    px = ((x - x0) * esc).astype(np.int32)
    py = ((y1 - y) * esc).astype(np.int32)
    ok = (px >= 0) & (px < larg) & (py >= 0) & (py < altura) & (nz > -0.15)
    px, py, z = px[ok], py[ok], z[ok]
    l = np.array(luz, np.float32); l /= np.linalg.norm(l)
    lamb = np.clip(nx[ok] * l[0] + ny[ok] * l[1] + nz[ok] * l[2], 0, 1)
    # ambiente frio + luz quente + contorno
    rim = np.clip(1 - nz[ok], 0, 1) ** 3
    amb = 0.36 + 0.14 * ny[ok]
    lin = (cor[ok].astype(np.float32) / 255) ** 2.2
    shade = lin * (amb[:, None] * np.array([0.75, 0.85, 1.1]) + lamb[:, None] * np.array([1.25, 1.15, 1.0]) * 0.95) + rim[:, None] * 0.05
    e = (emis[ok].astype(np.float32) / 255) ** 2.2
    out = shade + e * 2.2
    order = np.argsort(z)                      # o mais perto por último
    img = np.zeros((altura, larg, 3), np.float32)
    img[:] = (np.array(fundo) / 255) ** 2.2
    glow = np.zeros((altura, larg, 3), np.float32)
    img[py[order], px[order]] = out[order]
    glow[py[order], px[order]] = e[order]
    # tapa buracos de 1 px
    im8 = Image.fromarray((np.clip(img, 0, 1) ** (1 / 2.2) * 255).astype(np.uint8))
    g8 = Image.fromarray((np.clip(glow, 0, 1) ** (1 / 2.2) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(altura / 90))
    a = np.asarray(im8).astype(np.float32) + np.asarray(g8).astype(np.float32) * 1.3
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))


def folha(maps, cor, emis, saida, altura=720):
    m = maps["peca"] != 255
    pos = maps["pos"][m]; nrm = maps["nrm"][m]
    c = cor[m]; e = emis[m] if emis is not None else np.zeros_like(c)
    front = vista(pos, nrm, c, e, 0.0, altura)
    back = vista(pos, nrm, c, e, np.pi, altura)
    tq = vista(pos, nrm, c, e, 0.6, altura)
    head = vista(pos, nrm, c, e, 0.25, altura, zoom=(-0.42, 0.42, 1.0, 1.72))
    leg = vista(pos, nrm, c, e, 0.0, altura, zoom=(-0.5, 0.5, 0.0, 0.95))
    w = front.width + back.width + tq.width + head.width + leg.width
    s = Image.new("RGB", (w, altura))
    x = 0
    for im in (front, back, tq, head, leg):
        s.paste(im, (x, 0)); x += im.width
    s.save(saida, quality=90)
    return s


if __name__ == "__main__":
    maps = np.load(sys.argv[1])
    cor = maps["base"]
    emis = np.asarray(Image.open(sys.argv[3]).convert("RGB")) if len(sys.argv) > 3 else None
    folha(maps, cor, emis, sys.argv[2])
