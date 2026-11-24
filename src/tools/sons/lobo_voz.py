#!/usr/bin/env python3
"""
A voz do lobo ao pular e ao morrer (pedido do Jhon, 18/11: "revise os barulhos
de morte do lobo e do pulo, às vezes sai o barulho do humano").

    python3 tools/sons/lobo_voz.py <pasta "Free Sounds Pack">

Não era o som trocado: os de antes (lobo_pulo, lobo_morte) vinham de gravações
de "troll" e "ogro" do pacote da Oculus — gente fazendo voz de criatura. Com
duas ou cinco variações sorteadas, algumas soavam como uma pessoa gemendo. Os
novos saem do mesmo bicho do rugido (Creature 1-21 e Zombie 1 do pacote), que é
rosnado de animal:

- lobo_pulo_1..3    o esforço do salto: um rosnado curto e seco (0,42 s), que
                    estoura e some, com um sopro de ar;
- lobo_morte_1..3   a morte: um ganido rasgado na pancada e o rosnado caindo de
                    tom e de força até acabar em dois arquejos (1,5 s).

Mono, 44,1 kHz, no volume dos outros sons do lobo.
"""
import os, sys
import numpy as np
from scipy import signal
sys.path.insert(0, os.path.dirname(__file__))
from pacote_armas import SR, ler, mono, gravar
from rugido import faixa, baixo, tom, encaixar, cair, normal


def aspero(rng, n, hz=30.0):
    ritmo = hz + 5 * np.cumsum(rng.standard_normal(n)) / SR * 14
    return 0.62 + 0.38 * signal.sawtooth(2 * np.pi * np.cumsum(ritmo) / SR, 0.35)

def acabar(x, rms_db, some=0.06):
    x = x - x.mean()
    f = int(some * SR); x[-f:] *= np.linspace(1, 0, f) ** 2; x[:64] *= np.linspace(0, 1, 64)
    x *= 10 ** (rms_db / 20) / np.sqrt((x ** 2).mean())
    teto = 10 ** (-1.5 / 20)
    return np.tanh(x / teto) * teto


def pulo(criatura, seed, inicio, semitons):
    rng = np.random.default_rng(seed)
    n = int(0.42 * SR); t = np.arange(n) / SR
    corpo = normal(encaixar(tom(criatura, semitons), n, int(inicio * SR))) * aspero(rng, n, 34)
    ar = faixa(rng.standard_normal(n), 900, 3200) * 0.16
    x = np.tanh((corpo + ar) * 2.2)
    x = x + faixa(np.tanh(x * 4), 1500, 4500) * 0.35
    env = np.interp(t, [0, 0.025, 0.09, 0.2, 0.42], [0, 1.0, 0.85, 0.3, 0])
    x = encaixar(cair(x * env, 1.12, 0.84), n)
    return acabar(x, -19.0)

def morte(criatura, zumbi, seed, inicio, semitons):
    rng = np.random.default_rng(seed)
    n = int(1.5 * SR); t = np.arange(n) / SR
    corpo = normal(encaixar(tom(criatura, semitons), int(n * 1.3), int(inicio * SR)))
    corpo = encaixar(cair(corpo, 1.2, 0.55), n)                       # o tom despenca: o bicho se esvazia
    corpo *= aspero(rng, n, 26)
    # o ganido da pancada: o zumbi, grave, só no começo
    ganido = np.zeros(n)
    g = normal(tom(zumbi[:int(0.5 * SR)], semitons - 3)); m = min(len(g), int(0.45 * SR))
    ganido[:m] = g[:m] * np.linspace(1, 0, m) ** 1.5
    x = np.tanh((corpo + ganido * 0.9) * 1.8)
    x = x + faixa(np.tanh(x * 4), 1400, 4200) * 0.3
    # estoura, cai, e dois arquejos no fim
    env = np.interp(t, [0, 0.03, 0.25, 0.7, 0.86, 0.98, 1.12, 1.26, 1.5], [0, 1.0, 0.7, 0.34, 0.12, 0.3, 0.08, 0.18, 0])
    return acabar(x * env, -20.0, some=0.15)


def main(pasta):
    criatura = mono(ler(os.path.join(pasta, "Creature 1-21.wav")))
    zumbi = mono(ler(os.path.join(pasta, "Zombie 1 - Short 1-01.wav")))
    for i, (seed, inicio, st) in enumerate(((1, 0.15, -3), (2, 0.9, -5), (3, 1.7, -2)), 1):
        gravar(f"lobo_pulo_{i}", pulo(criatura, seed, inicio, st))
    for i, (seed, inicio, st) in enumerate(((11, 0.15, -5), (12, 0.8, -7), (13, 0.5, -4)), 1):
        gravar(f"lobo_morte_{i}", morte(criatura, zumbi, seed, inicio, st))

if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else ".")
