#!/usr/bin/env python3
"""
O marcador de acerto: o som que diz "pegou" só para quem atirou (pedido do Jhon,
18/11: "preciso de um feedback auditivo de quando acerto alguém, um pouco mais
alto; de headshot pode dar um tic").

    python3 tools/sons/marcador.py            # grava em assets/audio/

- marcador_1..3          acerto no corpo: um estalo seco e um "tuc" curto de
                         tom médio-agudo (900 a 1100 Hz, caindo). 80 ms: numa
                         rajada, cada bala tem o seu, sem embolar;
- marcador_cabeca_1..2   na cabeça: o mesmo "tuc" por baixo e, por cima, um
                         "tic" de metal — agudo, limpo, que corta qualquer
                         tiroteio. 180 ms.

Ficam acima dos antigos (acerto, acerto_cabeca: -29 e -23 dB): -15 e -13 dB.
São seus, não do mundo: tocam no SFX, sem posição e sem reverberação.

Só senoides e ruído filtrado (numpy e scipy); nenhuma gravação. Mono, 44,1 kHz.
"""
import os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
from raios import SR, faixa, alto, tempo, somar, acabar, gravar


def fase(f):
    return 2 * np.pi * np.cumsum(f) / SR

def estalo(rng, dur=0.012, lo=2500, hi=9000):
    t = tempo(dur)
    return faixa(rng.standard_normal(len(t)), lo, hi) * np.exp(-t / 0.0018)

def tuc(rng, f0, dur=0.08):
    """O corpo do acerto: tom que cai rápido e some em 25 ms."""
    t = tempo(dur)
    f = f0 * (0.68 + 0.32 * np.exp(-t / 0.012))
    x = np.sin(fase(f)) + 0.35 * np.sin(fase(f * 2.01))
    return x * np.minimum(t / 0.0008, 1) * np.exp(-t / 0.022)

def tic(rng, f0, dur=0.18):
    """O 'tic' da cabeça: uma pancadinha de metal (parciais fora de tom, o agudo some antes)."""
    t = tempo(dur)
    x = np.zeros(len(t))
    for mult, peso, decai in ((1.0, 1.0, 0.055), (2.76, 0.55, 0.028), (5.40, 0.3, 0.014), (1.5, 0.25, 0.04)):
        x += peso * np.sin(2 * np.pi * f0 * mult * t + rng.uniform(0, 6)) * np.exp(-t / decai)
    return x * np.minimum(t / 0.0005, 1)


if __name__ == "__main__":
    for i, (seed, f0) in enumerate(((3, 1050), (5, 960), (8, 1120)), 1):
        rng = np.random.default_rng(seed)
        x = somar(0.08, (0.0, estalo(rng), 0.9), (0.0, tuc(rng, f0), 1.0))
        gravar(f"marcador_{i}", acabar(x, -15.0, some=0.02))
    for i, (seed, f0) in enumerate(((13, 2650), (17, 2820)), 1):
        rng = np.random.default_rng(seed)
        x = somar(0.18,
                  (0.0, estalo(rng, 0.01, 4000, 12000), 1.0),
                  (0.0, tuc(rng, 1000), 0.55),
                  (0.0, alto(tic(rng, f0), 1200), 1.0))
        gravar(f"marcador_cabeca_{i}", acabar(x, -13.0, some=0.03))
