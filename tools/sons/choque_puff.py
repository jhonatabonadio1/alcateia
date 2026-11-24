#!/usr/bin/env python3
"""
Dois sons do lobo feitos por código (Jhon, 23/11):

    python3 tools/sons/choque_puff.py      # grava em assets/audio/

- lobo_uivo_choque_1..2   os choques do rugido (R): "o barulho do choque deve ser apenas do
                          choque, sem esse som subindo, zuuuum". Só estalos elétricos e arcos
                          secos, sem nenhuma nota, grave ou ar que sobe (o lobo_uivo_energia
                          tinha um grave e uma bobina subindo duas oitavas: era o "zuuum").
                          A densidade é quase igual do começo ao fim. Toca junto da voz do
                          rugido (lobo_rugido), 1,6 s.
- forma_puff_1..3         o "puff" de virar lobo e de voltar a humano (tecla 3): um sopro de
                          fumaça, ruído abafado que nasce na hora e se desfaz, com um baque
                          grave por baixo. Sem voz: o rugido da troca saiu ("tirar o som ou
                          trocar o rugido do lobo ao apertar 3"), e sem nada a troca ficou muda.

Ruído filtrado (numpy e scipy); nenhuma gravação. Mono, 44,1 kHz. Cada variação tem a sua
semente: rodar de novo dá os mesmos arquivos.
"""
import os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
from raios import SR, faixa, baixo, alto, tempo, somar, acabar, gravar, faiscas, estalo


def choques(seed):
    rng = np.random.default_rng(seed)
    dur = 1.6
    t = tempo(dur)
    # o crepitar: estalos miúdos, quase constantes (um pouco mais juntos no meio)
    miudos = faiscas(rng, dur, 260, decai=(0.0015, 0.007), forma=lambda tt: 0.75 + 0.25 * np.sin(np.pi * np.clip(tt / dur, 0, 1)))
    # os arcos: de tempos em tempos um choque maior, seco, com um rabo curto de faíscas
    arcos = np.zeros(len(t))
    quando = 0.03
    while quando < dur - 0.12:
        e = estalo(rng, 0.08) * rng.uniform(0.5, 1.0)
        rabo = faiscas(rng, 0.10, 900, decai=(0.001, 0.004), forma=lambda tt: np.exp(-tt / 0.03))
        i = int(quando * SR)
        for x in (e, rabo * 0.7):
            m = min(len(x), len(arcos) - i); arcos[i:i + m] += x[:m]
        quando += rng.uniform(0.07, 0.19)
    arcos = alto(arcos, 900)                                          # sem baque: o choque é seco, em cima
    x = somar(dur, (0.0, miudos, 0.9), (0.0, arcos, 1.0))
    x *= np.minimum(t / 0.04, 1.0) * np.clip((dur - t) / 0.12, 0, 1)    # entra e sai macio, sem estalo de corte
    return acabar(x, -19.0, some=0.04)


def puff(seed, grave):
    rng = np.random.default_rng(seed)
    dur = 0.62
    t = tempo(dur)
    ruido = rng.standard_normal(len(t))
    # o sopro: abre médio e fecha para o grave enquanto a fumaça se espalha
    u = np.clip(t / 0.35, 0, 1)
    sopro = baixo(ruido, 1700 * grave, 2) * (1 - u) + baixo(ruido, 520 * grave, 2) * u
    sopro *= np.minimum(t / 0.006, 1.0) * np.exp(-t / 0.15)
    # o ar se desfazendo: um chiado fraco, mais longo
    cauda = faixa(rng.standard_normal(len(t)), 300, 2400) * np.minimum(t / 0.03, 1.0) * np.exp(-t / 0.26)
    # o baque por baixo
    baque = np.sin(2 * np.pi * np.cumsum(55 * grave + 70 * grave * np.exp(-t / 0.04)) / SR) * np.exp(-t / 0.09)
    x = somar(dur, (0.0, sopro, 1.0), (0.0, cauda, 0.22), (0.0, baque, 0.55))
    return acabar(x, -17.0, some=0.15)


if __name__ == "__main__":
    for i, s in enumerate((211, 223), 1): gravar(f"lobo_uivo_choque_{i}", choques(s))
    for i, (s, g) in enumerate(((301, 0.85), (313, 1.0), (327, 1.15)), 1): gravar(f"forma_puff_{i}", puff(s, g))
