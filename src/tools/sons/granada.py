#!/usr/bin/env python3
"""
Os sons da granada, feitos por código (Jhon, 26/11: "som de explosão de granada melhor
AAA+, com fuse perceptível"):

    python3 src/tools/sons/granada.py      # grava em src/assets/audio/

- (as explosões saíram daqui em 27/11: vêm de uma gravação, por granada_explosao.py — a
  sintetizada soava "de tijolo", Jhon. Este roteiro guarda o eco e a sala que ela usa.)
- granada_pavio                    o pavio queimando, em laço (chiado com estalinhos): toca
                                   preso à granada do arremesso até a explosão.
- granada_pavio_fim                os últimos 1,2 s (o pavio que conta depois do primeiro
                                   toque): o chiado sobe e cliques que se aceleram, para quem
                                   está perto saber que vai estourar agora.

Ruído filtrado e senoides (numpy e scipy); nenhuma gravação, nada de terceiros.
Mono, 44,1 kHz. Cada variação tem a sua semente: rodar de novo dá os mesmos arquivos.
"""
import os, sys
import numpy as np
from scipy import signal
sys.path.insert(0, os.path.dirname(__file__))
from raios import SR, faixa, baixo, alto, tempo, somar, acabar, gravar


def env(t, ataque, decai):
    """Envelope: sobe em `ataque` s e cai exponencial com constante `decai` s."""
    return np.minimum(t / max(ataque, 1e-4), 1.0) * np.exp(-np.maximum(t - ataque, 0) / decai)


def lfo(rng, n, f_lo, f_hi):
    """Uma modulação lenta e irregular, entre 0 e 1 (o ronco rolando, o chiado tremendo)."""
    m = baixo(rng.standard_normal(n), f_hi, 2)
    m = alto(m, f_lo, 1) if f_lo > 0 else m
    m = m / (np.abs(m).max() + 1e-9)
    return 0.5 + 0.5 * m


def ecos(x, rng, atrasos, ganhos, cortes):
    """Reflexões: cópias atrasadas, mais escuras a cada volta."""
    out = np.zeros(len(x))
    for d, g, c in zip(atrasos, ganhos, cortes):
        i = int(d * SR)
        if i >= len(x): continue
        out[i:] += baixo(x[:len(x) - i], c, 2) * g
    return out


def difusa(x, rng, cauda, corte):
    """Uma reverberação difusa: convolução com ruído que decai (a rua, a sala)."""
    m = int(cauda * SR)
    tt = np.arange(m) / SR
    ir = baixo(rng.standard_normal(m), corte, 2) * np.exp(-tt / (cauda / 5))
    ir[:int(0.012 * SR)] = 0
    ir /= np.sqrt((ir ** 2).sum()) + 1e-9
    return signal.fftconvolve(x, ir)[:len(x)]


def chiado(rng, dur, forte=1.0):
    """O pavio queimando: chiado agudo que treme, com estalinhos."""
    t = tempo(dur)
    r = rng.standard_normal(len(t))
    chi = faixa(r, 2800, 9500) * (0.55 + 0.45 * lfo(rng, len(t), 8, 45))
    sopro = faixa(rng.standard_normal(len(t)), 700, 2200) * 0.25
    n = len(t)
    pipoca = np.zeros(n)
    onde = np.nonzero(rng.random(n) < 55 * forte / SR)[0]
    for i in onde:
        m = min(int(0.004 * SR), n - i)
        pipoca[i:i + m] += rng.uniform(0.4, 1.0) * np.exp(-np.arange(m) / (0.0009 * SR))
    pipoca = pipoca * faixa(rng.standard_normal(n), 2000, 10000) * 2.2
    return chi + sopro + pipoca


def pavio(seed):
    """Em laço: 1,0 s que emenda no começo sem estalo (cruzamento de 0,15 s)."""
    rng = np.random.default_rng(seed)
    laco, junta = 1.0, 0.15
    n, j = int(laco * SR), int(junta * SR)
    x = chiado(rng, laco + junta + 0.01)
    cruz = np.linspace(0, 1, j)
    y = x[:n].copy()
    y[:j] = x[n:n + j] * (1 - cruz) + x[:j] * cruz
    y = y - y.mean()
    # (−19 dB de média desde 27/11; era −27: "quase não consigo ouvir para correr e me esconder")
    y *= 10 ** (-19.0 / 20) / (np.sqrt((y ** 2).mean()) + 1e-9)
    return np.clip(y, -0.9, 0.9)


def pavio_fim(seed, dur=1.2):
    """Os últimos 1,2 s: o chiado sobe e cliques secos que se aceleram até o fim."""
    rng = np.random.default_rng(seed)
    t = tempo(dur)
    x = chiado(rng, dur, forte=1.6) * (0.6 + 0.9 * (t / dur) ** 2)
    # cliques: o intervalo encolhe de 0,32 s para 0,05 s
    quando, passo = 0.0, 0.32
    while quando < dur - 0.03:
        i = int(quando * SR); m = int(0.03 * SR); tt = np.arange(m) / SR
        c = np.sin(2 * np.pi * 3300 * tt) * np.exp(-tt / 0.004) + 0.7 * rng.standard_normal(m) * np.exp(-tt / 0.0006)
        c += 0.5 * np.sin(2 * np.pi * 1450 * tt) * np.exp(-tt / 0.007)
        k = min(m, len(x) - i); x[i:i + k] += c[:k] * 1.8 * (0.7 + 0.3 * quando / dur)
        quando += passo
        passo = max(0.05, passo * 0.72)
    x = x - x.mean()
    x *= 10 ** (-23.0 / 20) / (np.sqrt((x ** 2).mean()) + 1e-9)
    x[-int(0.01 * SR):] *= np.linspace(1, 0, int(0.01 * SR))
    return np.clip(x, -0.95, 0.95)


if __name__ == "__main__":
    gravar("granada_pavio", pavio(401))
    gravar("granada_pavio_fim", pavio_fim(402))
