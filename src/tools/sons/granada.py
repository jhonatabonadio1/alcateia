#!/usr/bin/env python3
"""
Os sons da granada, feitos por código (Jhon, 26/11: "som de explosão de granada melhor
AAA+, com fuse perceptível"):

    python3 src/tools/sons/granada.py      # grava em src/assets/audio/

- granada_explosao_1..4            de perto: o estalo da onda de choque (milésimos), o soco
                                   grave que desce de 90 para 38 Hz, o bafo de ar, os
                                   estilhaços batendo em volta (cliques metálicos e terra
                                   caindo) e o ronco que rola até sumir, com dois ecos de
                                   fachada. Era o "Sci-Fi Sounds" da Kenney: um inchaço de
                                   250 ms sem estalo nenhum.
- granada_explosao_distante_1..3   longe (passou de uns 30 m): o ar come o agudo, o estalo
                                   vira um baque, o ronco e os ecos ficam mais longos.
- granada_explosao_dentro_1..3     debaixo de teto (casa, garagem, corredor): reflexões
                                   curtas e densas, o grave da sala tocando, cauda mais curta.
- granada_pavio                    o pavio queimando, em laço (chiado com estalinhos): toca
                                   preso à granada do arremesso até a explosão.
- granada_pavio_fim                os últimos 1,2 s (o pavio que conta depois do primeiro
                                   toque): o chiado sobe e cliques que se aceleram, para quem
                                   está perto saber que vai estourar agora.

Ruído filtrado, senoides e atrasos (numpy e scipy); nenhuma gravação, nada de terceiros.
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


def onda(rng, dur):
    """A onda de choque: um estalo largo de poucos milésimos e o tranco de pressão por baixo."""
    t = tempo(dur)
    estalo = alto(rng.standard_normal(len(t)), 1200, 2) * env(t, 0.0004, 0.0035)
    estalo += 0.6 * faixa(rng.standard_normal(len(t)), 2500, 12000) * env(t, 0.0002, 0.0015)
    # o tranco: meia onda positiva rápida e o vale mais lento (a onda N de uma explosão)
    pressao = np.where(t < 0.004, np.sin(np.pi * t / 0.004), -0.45 * np.sin(np.pi * np.clip((t - 0.004) / 0.012, 0, 1)))
    pressao *= (t < 0.016)
    return estalo * 0.9 + baixo(pressao, 900, 2) * 1.4


def soco(rng, dur, de=90.0, ate=38.0, cai=0.24):
    """O grave do peito: uma senoide que desce, com um pouco de harmônico, saturada."""
    t = tempo(dur)
    f = ate + (de - ate) * np.exp(-t / 0.12)
    fase = 2 * np.pi * np.cumsum(f) / SR
    x = np.sin(fase) + 0.28 * np.sin(2 * fase + 0.4) + 0.1 * np.sin(3 * fase + 1.1)
    x *= env(t, 0.0025, cai)
    return np.tanh(x * 1.8) / np.tanh(1.8)


def bafo(rng, dur):
    """O ar empurrado: ruído cheio que abre e fecha para o grave."""
    t = tempo(dur)
    r = rng.standard_normal(len(t))
    medio = baixo(r, 2600, 2) * env(t, 0.0015, 0.07)
    grave = baixo(r, 520, 2) * env(t, 0.004, 0.32)
    return medio * 0.8 + grave * 1.6


def estilhacos(rng, dur, quantos=70, metal=0.6, terra=8):
    """O que voa: cliques metálicos (estilhaço batendo em chapa, pedra) que rareiam, e
    terra caindo, baques surdos espalhados."""
    n = int(dur * SR)
    out = np.zeros(n)
    for _ in range(quantos):
        quando = 0.02 + rng.exponential(0.28)
        if quando > dur - 0.1: continue
        i = int(quando * SR)
        m = int(0.06 * SR)
        tt = np.arange(m) / SR
        if rng.random() < metal:
            f = rng.uniform(1800, 6200)
            x = np.sin(2 * np.pi * f * tt + rng.uniform(0, 6)) * np.exp(-tt / rng.uniform(0.006, 0.025))
            x += 0.5 * rng.standard_normal(m) * np.exp(-tt / 0.0008)
        else:
            x = faixa(rng.standard_normal(m), 900, 5000) * np.exp(-tt / rng.uniform(0.002, 0.01))
        ganho = rng.uniform(0.15, 1.0) * np.exp(-quando / 0.6)
        k = min(m, n - i); out[i:i + k] += x[:k] * ganho
    for _ in range(terra):
        quando = rng.uniform(0.35, min(1.9, dur - 0.3))
        i = int(quando * SR); m = int(0.25 * SR); tt = np.arange(m) / SR
        x = baixo(rng.standard_normal(m), 380, 2) * env(tt, 0.004, 0.05) * rng.uniform(0.3, 0.8)
        k = min(m, n - i); out[i:i + k] += x[:k]
    return out


def ronco(rng, dur, corte=170.0, cai=0.9, ataque=0.03):
    """O ronco que rola depois: grave, irregular, que demora a sumir."""
    t = tempo(dur)
    x = baixo(rng.standard_normal(len(t)), corte, 3)
    x *= 0.55 + 0.45 * lfo(rng, len(t), 0.5, 7)
    return x * env(t, ataque, cai)


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


def topo(x, pico_db):
    """Leva o pico a `pico_db` (o estouro é o mais alto do jogo; o resto acompanha)."""
    return x * (10 ** (pico_db / 20) / (np.abs(x).max() + 1e-9))


def explosao(seed, onde="perto"):
    rng = np.random.default_rng(seed)
    dur = {"perto": 3.2, "distante": 4.2, "dentro": 2.4}[onde]
    t = tempo(dur)
    # O golpe: o estalo da onda, o grave do peito (sub), o corpo (o "bum" que se ouve até
    # em fone e notebook, 80–250 Hz) e o bafo de ar. O sub sozinho sumia em alto-falante
    # pequeno: a primeira versão tinha 80% da energia abaixo de 80 Hz.
    golpe = somar(dur, (0.0, onda(rng, 0.05), 2.2),
                  (0.0, soco(rng, 0.9, de=rng.uniform(80, 100), ate=rng.uniform(36, 44)), 0.55),
                  (0.0, soco(rng, 0.6, de=rng.uniform(190, 230), ate=rng.uniform(78, 92), cai=0.12), 0.75),
                  (0.0005, bafo(rng, 1.4), 1.25))
    # O que fica: os estilhaços (cliques e terra) e o ronco, grave e médio.
    resto = somar(dur, (0.015, estilhacos(rng, 2.2, quantos=int(rng.integers(60, 100))), 0.95),
                  (0.0, ronco(rng, dur, cai=rng.uniform(0.75, 1.05)), 0.5),
                  (0.0, faixa(ronco(rng, dur, corte=900, cai=0.5, ataque=0.01), 160, 900), 0.6))

    if onde == "perto":
        x = golpe + resto
        x += ecos(golpe, rng, [rng.uniform(0.12, 0.17), rng.uniform(0.28, 0.36), rng.uniform(0.55, 0.7)], [0.32, 0.2, 0.12], [2600, 1600, 900])
        x += 0.22 * difusa(x, rng, 1.3, 2200)
        x = np.tanh(x * 1.25) / np.tanh(1.25)
        return topo(acabar(x, -15.5, pico_db=-1.0, some=0.25), -1.0)

    if onde == "distante":
        # o ar come o agudo: o estalo vira baque, sobra o grave e o eco
        x = baixo(golpe, 900, 2) * 0.9 + baixo(resto, 1200, 2) * 0.7 + ronco(rng, dur, corte=160, cai=1.4, ataque=0.08) * 0.9
        x = baixo(x, 1500, 2)
        x[:int(0.006 * SR)] *= np.linspace(0, 1, int(0.006 * SR))
        x += ecos(x, rng, [rng.uniform(0.35, 0.5), rng.uniform(0.8, 1.1), rng.uniform(1.4, 1.8)], [0.45, 0.3, 0.18], [700, 500, 380])
        x += 0.3 * difusa(x, rng, 2.2, 600)
        return acabar(x, -17.0, pico_db=-2.0, some=0.4)

    # dentro: reflexões curtas e densas, o grave da sala tocando, cauda curta
    x = golpe + resto * 0.8
    atrasos = np.sort(rng.uniform(0.006, 0.045, 9))
    x += ecos(golpe, rng, atrasos, list(np.linspace(0.55, 0.22, 9)), list(np.linspace(5000, 1800, 9)))
    modo = rng.uniform(95, 125)
    sala = np.sin(2 * np.pi * modo * t) * env(t, 0.01, 0.22) * 0.5
    x += sala
    x += 0.35 * difusa(x, rng, 0.7, 3500)
    x *= np.exp(-np.maximum(t - 0.4, 0) / 0.55) * 0.6 + 0.4 * np.exp(-t / 0.35)
    x = np.tanh(x * 1.35) / np.tanh(1.35)
    return topo(acabar(x, -15.0, pico_db=-1.0, some=0.2), -1.0)


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
    y *= 10 ** (-27.0 / 20) / (np.sqrt((y ** 2).mean()) + 1e-9)
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
    for i in range(4):
        gravar(f"granada_explosao_{i + 1}", explosao(110 + i, "perto"))
    for i in range(3):
        gravar(f"granada_explosao_distante_{i + 1}", explosao(210 + i, "distante"))
    for i in range(3):
        gravar(f"granada_explosao_dentro_{i + 1}", explosao(310 + i, "dentro"))
    gravar("granada_pavio", pavio(401))
    gravar("granada_pavio_fim", pavio_fim(402))
