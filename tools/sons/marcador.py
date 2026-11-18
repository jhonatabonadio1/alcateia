#!/usr/bin/env python3
"""
O marcador de acerto: o som que diz "pegou" só para quem atirou (pedidos do
Jhon, 18/11).

    python3 tools/sons/marcador.py <pasta com Hit_Generic_2-1.wav>

- marcador_1         acerto em QUALQUER parte do corpo, a cada bala: o
                     "Hit Generic 2-1" que o Jhon escolheu ("esse é o barulho
                     de quando acerto qualquer parte do corpo, devo ouvir a
                     cada bala acertada"). Aparado para 0,20 s: numa rajada
                     de 10 tiros por segundo, cada bala tem o seu;
- marcador_cabeca_1..2   SÓ na cabeça, a cada headshot, por cima do de cima:
                     o "plin" — uma pancadinha de metal aguda e limpa,
                     sintetizada (senoides; nenhuma gravação).

São seus, não do mundo: tocam no SFX, sem posição e sem reverberação.
Mono, 44,1 kHz.
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
    import glob
    from pacote_armas import ler, mono, cortar, pico, gravar as gravar_pico
    pasta = sys.argv[1] if len(sys.argv) > 1 else "/mnt/user-data/uploads"
    achado = sorted(glob.glob(os.path.join(pasta, "*Hit_Generic_2-1.wav")) + glob.glob(os.path.join(pasta, "Hit Generic 2-1.wav")))
    if not achado: sys.exit("não achei o Hit Generic 2-1 em " + pasta)
    corpo = mono(ler(achado[0]))
    gravar_pico("marcador_1", pico(cortar(corpo, 0.20, some=0.07), -2.0))

    for i, (seed, f0) in enumerate(((13, 2650), (17, 2820)), 1):
        rng = np.random.default_rng(seed)
        x = somar(0.2,
                  (0.0, estalo(rng, 0.008, 5000, 12000), 0.5),
                  (0.0, alto(tic(rng, f0, 0.2), 1200), 1.0))
        gravar(f"marcador_cabeca_{i}", acabar(x, -13.0, some=0.03))
