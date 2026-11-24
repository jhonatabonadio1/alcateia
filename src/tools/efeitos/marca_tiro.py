#!/usr/bin/env python3
"""
A marca do tiro na parede e no chão (pedido do Jhon, 20/11: "ao atirar, o local que recebeu o tiro deve ficar
marcado e depois sumir"). Gera as duas texturas do decal que `scripts/visual/fx/BulletMarks.cs` projeta:

- assets/efeitos/marca_tiro.png    a cor: o furo escuro no meio, o anel lascado em volta (a tinta e o reboco que
                                   saltaram, mais claros e mais escuros que a parede), riscos curtos para fora e um
                                   pó fino de fuligem; o alfa recorta tudo isso;
- assets/efeitos/marca_tiro_n.png  o relevo: a cratera funda no meio e a borda levantada do anel.

Uma marca só (o decal do Godot não recorta folha): o jogo varia o giro e o tamanho. Só numpy e Pillow.

    python3 tools/efeitos/marca_tiro.py
"""
import os
import numpy as np
from PIL import Image

AQUI = os.path.dirname(os.path.abspath(__file__))
SAIDA = os.path.join(AQUI, '..', '..', 'assets', 'efeitos')
LADO = 128          # a marca, em pixels (no jogo ela tem uns 12 cm)
rng = np.random.default_rng(20)


def ruido(n, escala, sem):
    """Ruído suave (valor interpolado) em [0, 1]."""
    r = np.random.default_rng(sem)
    g = r.random((escala + 2, escala + 2))
    y, x = np.mgrid[0:n, 0:n] / n * escala
    i, j = y.astype(int), x.astype(int); fy, fx = y - i, x - j
    fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
    a = g[i, j] * (1 - fx) + g[i, j + 1] * fx; b = g[i + 1, j] * (1 - fx) + g[i + 1, j + 1] * fx
    return a * (1 - fy) + b * fy


def marca(sem):
    n = LADO; y, x = (np.mgrid[0:n, 0:n] + 0.5) / n * 2 - 1
    r = np.hypot(x, y); ang = np.arctan2(y, x)
    borda = 1 + 0.22 * (ruido(n, 6, sem) - 0.5) * 2 + 0.08 * np.sin(ang * rng.integers(5, 9) + sem)
    rr = r / borda                                                    # raio "deformado": a lasca não é um disco
    furo = np.clip((0.2 - rr) / 0.035, 0, 1)                         # o furo
    anel = np.clip((0.46 - rr) / 0.10, 0, 1) * (1 - furo)             # a lasca em volta
    riscos = np.zeros_like(r)
    for _ in range(rng.integers(5, 9)):                               # riscos curtos para fora
        a0 = rng.uniform(-np.pi, np.pi); L = rng.uniform(0.5, 0.85); w = rng.uniform(0.012, 0.03)
        d = np.abs(np.sin(ang - a0)) * r; ok = (np.cos(ang - a0) > 0) & (r < L)
        riscos = np.maximum(riscos, ok * np.clip(1 - d / w, 0, 1) * np.clip((L - r) / L, 0, 1))
    fuligem = np.clip((0.7 - rr) / 0.35, 0, 1) * 0.35 * ruido(n, 14, sem + 9)
    grao = ruido(n, 22, sem + 3)

    # cor: furo quase preto; lasca alternando claro (reboco exposto) e escuro (pó queimado); riscos e fuligem escuros
    claro = np.clip((grao - 0.45) * 3, 0, 1) * np.clip((rr - 0.2) / 0.12, 0, 1)
    v = 0.09 + 0.30 * claro + 0.10 * grao
    v = np.where(furo > 0, v * (1 - furo) + 0.03 * furo, v)
    cor = np.stack([v * 1.04, v * 1.0, v * 0.95], -1)
    alfa = np.clip(np.maximum.reduce([furo, anel * (0.75 + 0.25 * grao), riscos * 0.7, fuligem * 1.4]), 0, 1)

    # relevo: cratera (fundo no meio, sobe até a borda do anel) e o lábio levantado
    h = -1.0 * furo - 0.45 * anel * (1 - rr / 0.46) + 0.25 * np.exp(-((rr - 0.42) / 0.05) ** 2) + 0.08 * (grao - 0.5)
    gy, gx = np.gradient(h * 6.0)
    nrm = np.stack([-gx, gy, np.ones_like(h)], -1); nrm /= np.linalg.norm(nrm, axis=-1, keepdims=True)
    return cor, alfa, nrm


def main():
    c, a, n = marca(18)
    os.makedirs(SAIDA, exist_ok=True)
    cor = np.dstack([c, a]); nrm = np.dstack([n * 0.5 + 0.5, a])
    Image.fromarray((np.clip(cor, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGBA').save(os.path.join(SAIDA, 'marca_tiro.png'))
    Image.fromarray((np.clip(nrm, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGBA').save(os.path.join(SAIDA, 'marca_tiro_n.png'))
    print('marca_tiro.png e marca_tiro_n.png:', LADO, 'px')


if __name__ == '__main__':
    main()
