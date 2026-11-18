#!/usr/bin/env python3
"""
Os sons de poder do uivo do lobo, feitos por código (pedido do Jhon, 18/11:
"faltam sons de brilho, energia para complementar" — e depois: "não é brilho,
literalmente; é um som com mais brilho, mais impactante, mais emocionante,
barulho de poder. O core do lobo é eletricidade").

    python3 tools/sons/uivo.py            # grava em assets/audio/

- lobo_uivo_energia_1..2   toca junto com o rugido, do começo ao fim (1,6 s): o
                           poder CARREGANDO. Um grave que sobe uma oitava, um
                           zunido de bobina que sobe duas e treme cada vez mais
                           rápido, estalos elétricos cada vez mais juntos e um
                           ar agudo que abre. Tudo cresce até o corte;
- lobo_uivo_escudo_1..2    toca quando o rugido termina e o escudo enche: o
                           poder SOLTO. Uma pancada grave que desce, um trovão
                           curto, o estalo e o zunido de um raio, o ar sendo
                           empurrado e o chiado do escudo ficando. Sem nota e
                           sem sino: o Jhon não gostou do "plin" no final.

O rugido (lobo_rugido) é só grave e médio (nada acima de 2 kHz): estes dois dão
o peso de baixo e o brilho de cima que faltavam, sem brigar com ele.

Senoides, serras e ruído filtrado (numpy e scipy); nenhuma gravação. Mono,
44,1 kHz. Cada variação tem a sua semente: rodar de novo dá os mesmos arquivos.
"""
import os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
from scipy import signal
from raios import SR, faixa, baixo, alto, tempo, somar, acabar, gravar, faiscas, zumbido, varredura, estalo

# Mi maior aberto, do grave ao agudo: soa limpo, "de cristal", sem tristeza.
MI = 659.26
NOTAS_ALTAS = [MI * 2 ** (n / 12) for n in (24, 26, 28, 31, 33, 36, 38, 40)]     # pentatônica de mi, duas oitavas acima


def fase(f):
    """Fase de uma frequência que muda no tempo."""
    return 2 * np.pi * np.cumsum(f) / SR

def sino(rng, f, dur, decai):
    """Uma nota de sino: a fundamental e dois parciais fora de tom que somem antes."""
    t = tempo(dur)
    x = np.sin(2 * np.pi * f * t) * np.exp(-t / decai)
    x += 0.35 * np.sin(2 * np.pi * f * 2.76 * t + rng.uniform(0, 6)) * np.exp(-t / (decai * 0.45))
    x += 0.15 * np.sin(2 * np.pi * f * 5.40 * t + rng.uniform(0, 6)) * np.exp(-t / (decai * 0.22))
    ataque = np.minimum(t / 0.003, 1.0)
    return x * ataque

def cintilar(rng, dur, quantas, peso=lambda u: u, decai=(0.05, 0.16)):
    """Cintilações: notas de sino curtas e agudas em instantes ao acaso. peso(u) diz
    onde elas se juntam (u de 0 a 1 ao longo do som)."""
    n = int(dur * SR)
    x = np.zeros(n)
    u = np.linspace(0, 1, 512)
    p = np.maximum(peso(u), 0); p = p / p.sum()
    for _ in range(quantas):
        quando = rng.choice(u, p=p) * (dur - 0.08)
        f = rng.choice(NOTAS_ALTAS)
        if f * 2 < 8500 and rng.random() < 0.33: f *= 2          # nada acima de 8,5 kHz: brilha sem furar o ouvido
        d = rng.uniform(*decai)
        nota = sino(rng, f, min(d * 6, dur - quando), d) * rng.uniform(0.3, 1.0)
        i = int(quando * SR)
        x[i:i + len(nota)] += nota[:n - i]
    return x


def serras(f, vozes=3, fora=0.006, rng=None):
    """Um punhado de serras quase iguais: cheio, largo, de máquina."""
    x = np.zeros(len(f))
    for k in range(vozes):
        d = 1 + fora * (k - (vozes - 1) / 2)
        x += signal.sawtooth(fase(f * d) + (rng.uniform(0, 6) if rng is not None else 0.0))
    return x / vozes

def energia(seed):
    rng = np.random.default_rng(seed)
    dur = 1.6
    t = tempo(dur)
    u = np.clip(t / 1.5, 0, 1)
    cresce = u ** 1.4                                              # do nada ao cheio, junto com o escudo

    # o peso: um grave que sobe uma oitava e incha
    grave = np.sin(fase(48 * 2 ** u)) + 0.5 * np.sin(fase(96 * 2 ** u))
    grave *= cresce

    # a bobina: serras que sobem duas oitavas, tremendo cada vez mais rápido (a pressa é a tensão)
    bobina = baixo(serras(110 * 4 ** u, rng=rng), 3400)
    treme = 0.62 + 0.38 * np.sin(fase(7 + 21 * u ** 1.5))
    bobina *= cresce * treme

    # a eletricidade: estalos cada vez mais juntos e o chiado da alta tensão
    estalos = faiscas(rng, dur, 320, forma=lambda tt: 0.1 + np.clip(tt / 1.5, 0, 1) ** 1.6)
    chiado = zumbido(rng, dur, 118) * cresce

    # o ar: ruído agudo que abre para cima
    ruido = rng.standard_normal(len(t))
    ar = (faixa(ruido, 1500, 4000) * (1 - u) + faixa(ruido, 4500, 10500) * u) * cresce

    # o brilho: o acorde de vidro, baixo, só para dar cor em cima
    sobe = 2 ** (2.0 * u / 12)
    vidro = np.zeros(len(t))
    for mult, peso in ((1, 1.0), (1.5, 0.8), (2, 0.7), (3, 0.45), (4, 0.3)):
        for fora in (-0.003, 0.003):
            vidro += peso * 0.5 * np.sin(fase(MI * mult * sobe * (1 + fora)) + rng.uniform(0, 6))
    vidro *= cresce

    x = somar(dur,
              (0.0, grave, 0.55),
              (0.0, bobina, 1.00),
              (0.0, estalos, 0.90),
              (0.0, chiado, 0.30),
              (0.0, ar, 0.60),
              (0.0, vidro, 0.25))
    # entra macio e corta seco no fim: quem responde é o som do escudo
    x *= np.minimum(t / 0.1, 1.0) * np.clip((dur - t) / 0.07, 0, 1)
    return acabar(x, -18.5, some=0.02)

def escudo(seed):
    """O fim do rugido, com o escudo cheio. Sem nota nem sino ("não gostei do plin no
    final"): é pancada, trovão e eletricidade — peso, não música."""
    rng = np.random.default_rng(seed)
    dur = 1.15
    t = tempo(dur)

    # a pancada: um grave que desce, com o estalo na frente
    pancada = np.sin(fase(40 + 86 * np.exp(-t / 0.075))) * np.exp(-t / 0.36)
    trovao = baixo(rng.standard_normal(len(t)), 230, 3) * np.exp(-t / 0.32)

    # o raio: o zunido que desce e os estalos que sobram
    zun = varredura(rng, 0.36, rng.uniform(6800, 8200), rng.uniform(420, 600)) * np.exp(-tempo(0.36) / 0.14)
    sobras = faiscas(rng, dur - 0.04, 260, decai=(0.002, 0.013), forma=lambda tt: np.exp(-tt / 0.32))

    # o empurrão de ar: ruído médio que fecha para o grave (o "vum" da energia assentando)
    ruido = rng.standard_normal(len(t))
    u = np.clip(t / 0.5, 0, 1)
    vum = (faixa(ruido, 500, 1400) * (1 - u) + faixa(ruido, 110, 420) * u) * np.minimum(t / 0.015, 1.0) * np.exp(-t / 0.3)

    # o escudo ficando: o chiado da alta tensão, sem tom, que treme e some
    campo = zumbido(rng, dur, 96) * (0.7 + 0.3 * np.sin(2 * np.pi * 15 * t)) * np.exp(-t / 0.42)

    x = somar(dur,
              (0.0, estalo(rng, 0.1), 1.3),
              (0.0, pancada, 1.3),
              (0.0, trovao, 1.2),
              (0.0, zun, 1.25),
              (0.04, sobras, 0.9),
              (0.0, vum, 1.7),
              (0.03, campo[:len(t) - int(0.03 * SR)], 0.4))
    return acabar(x, -15.0, some=0.3)


if __name__ == "__main__":
    for i, s in enumerate((101, 113), 1): gravar(f"lobo_uivo_energia_{i}", energia(s))
    for i, s in enumerate((127, 139), 1): gravar(f"lobo_uivo_escudo_{i}", escudo(s))
