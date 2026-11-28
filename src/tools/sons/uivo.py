#!/usr/bin/env python3
"""
Os sons de poder do uivo do lobo, feitos por código (pedido do Jhon, 18/11:
"faltam sons de brilho, energia para complementar" — e depois: "não é brilho,
literalmente; é um som com mais brilho, mais impactante, mais emocionante,
barulho de poder. O core do lobo é eletricidade").

    python3 tools/sons/uivo.py            # grava em assets/audio/

- lobo_uivo_energia_1..2   toca junto com o rugido, do começo ao fim (1,6 s): o ar
                           PUXADO, o ESTOURO e o poder CARREGANDO. Um grave que
                           sobe uma oitava, um zunido de bobina que sobe duas e
                           treme no passo da luz, estalos elétricos cada vez
                           mais juntos, um baque a cada onda do chão e um ar
                           agudo que abre. Tudo cresce até o corte;
- lobo_uivo_escudo_1..2    toca quando o rugido termina e o escudo enche: o
                           poder SOLTO. Uma pancada grave que desce, um trovão
                           curto, a trava da casca fechando, três raios saindo,
                           o ar empurrado e o chiado do escudo desacelerando. Sem nota e
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

# Os tempos do rugido que se vê (scripts/visual/fx/RoarBurst.cs, 27/11): o som bate neles.
PUXA = 0.12            # a antecipação: do R até o estouro (RoarBurst.IntakeTime)
CHEIO = 1.30           # quando o rugido já vale inteiro (85% de 1,53 s)
ONDAS = (0.62, 1.12)   # as ondas do chão da sustentação (uma a cada 0,5 s depois do estouro)
TREME = (7.0, 20.0)    # o tremor da luz, em vezes por segundo, do começo ao cheio (FlickerFrom, FlickerTo)


def pancada(dur, de, ate, cai, desce=0.06):
    """Um grave que desce de `de` a `ate` Hz e some: o peso de um golpe."""
    t = tempo(dur)
    return np.sin(fase(ate + (de - ate) * np.exp(-t / desce))) * np.exp(-t / cai)

def energia(seed):
    """Refeito em 27/11 com o visual (pedido do Jhon: "fx e vfx mais profissional"), nos
    mesmos tempos dele: o ar sendo PUXADO (0,12 s), o ESTOURO (a pancada e o sopro), a
    CARGA subindo — o zunido de bobina tremendo no passo da luz, os estalos cada vez
    mais juntos, um baque surdo a cada onda do chão — e o corte seco no fim, para o
    escudo responder. Sem o acorde de vidro de antes: é eletricidade, não brilho."""
    rng = np.random.default_rng(seed)
    dur = 1.6
    t = tempo(dur)
    u = np.clip((t - PUXA) / (CHEIO - PUXA), 0, 1)                 # a carga, de 0 (estouro) a 1 (cheio)
    depois = (t >= PUXA).astype(float)
    cresce = (0.2 + 0.8 * u ** 1.5) * depois

    # a puxada: ar e faíscas correndo para dentro — um sopro que sobe e corta no estouro
    tp = tempo(PUXA)
    puxa = varredura(rng, PUXA, 380, 4200, 0.9) * (tp / PUXA) ** 2.2

    # o estouro: a pancada no peito, o estalo e o ar empurrado
    golpe = pancada(0.5, 150, 46, 0.2)
    sopro = faixa(rng.standard_normal(int(0.4 * SR)), 180, 2600) * np.exp(-tempo(0.4) / 0.085)

    # o peso: um grave que sobe uma oitava e incha
    grave = (np.sin(fase(48 * 2 ** u)) + 0.5 * np.sin(fase(96 * 2 ** u))) * cresce

    # a bobina: serras que sobem duas oitavas, tremendo no passo do tremor da luz
    bobina = baixo(serras(110 * 4 ** u, rng=rng), 3400)
    treme = 0.58 + 0.42 * np.sin(fase(TREME[0] + (TREME[1] - TREME[0]) * u))
    bobina *= cresce * treme

    # a eletricidade: estalos cada vez mais juntos (os arcos) e o chiado da alta tensão
    estalos = faiscas(rng, dur, 380, forma=lambda tt: (tt >= PUXA) * (0.08 + np.clip((tt - PUXA) / (CHEIO - PUXA), 0, 1) ** 1.7))
    chiado = zumbido(rng, dur, 118) * cresce * (0.75 + 0.25 * treme)

    # o ar da boca: ruído que abre para cima com a carga
    ruido = rng.standard_normal(len(t))
    ar = (faixa(ruido, 1400, 3800) * (1 - u) + faixa(ruido, 4200, 10000) * u) * cresce

    partes = [(0.0, puxa, 0.55),
              (PUXA, estalo(rng, 0.07), 0.45),
              (PUXA, golpe, 0.85),
              (PUXA, sopro, 0.7),
              (0.0, grave, 0.6),
              (0.0, bobina, 1.0),
              (0.0, estalos, 0.95),
              (0.0, chiado, 0.32),
              (0.0, ar, 0.5)]
    # as ondas do chão: um baque surdo e curto, cada um mais forte
    for k, quando in enumerate(ONDAS):
        partes.append((quando, pancada(0.3, 110, 52, 0.11, 0.04), 0.7 + 0.25 * k))
        partes.append((quando, faixa(rng.standard_normal(int(0.2 * SR)), 250, 1400) * np.exp(-tempo(0.2) / 0.05), 0.35))
    x = somar(dur, *partes)
    # entra macio e corta seco no fim: quem responde é o som do escudo
    x *= np.minimum(t / 0.02, 1.0) * np.clip((dur - t) / 0.07, 0, 1)
    return acabar(x, -18.5, some=0.02)

def escudo(seed):
    """O fim do rugido, com o escudo cheio. Sem nota nem sino ("não gostei do plin no
    final"): é pancada, trovão e eletricidade — peso, não música. Refeito em 27/11 para
    bater no visual: a casca FECHA (um baque seco de trava), a descarga sai em três
    raios quase juntos, a bolha de ar empurra, e o campo do escudo fica zunindo e
    desacelera (o contrário da carga: a energia assenta)."""
    rng = np.random.default_rng(seed)
    dur = 1.3
    t = tempo(dur)

    # a pancada: um grave fundo que desce, com o trovão curto por baixo
    fundo = pancada(dur, 170, 38, 0.42, 0.07)
    trovao = baixo(rng.standard_normal(len(t)), 230, 3) * np.exp(-t / 0.34)

    # a trava: a casca fechando — um baque seco, médio, sem tom
    trava = faixa(rng.standard_normal(int(0.16 * SR)), 140, 620, 3) * np.exp(-tempo(0.16) / 0.03)

    # a descarga: três raios saindo, um atrás do outro, cada um com o seu zunido descendo
    raios = []
    for quando in (0.0, 0.035, 0.085):
        d = rng.uniform(0.26, 0.38)
        raios.append((quando, varredura(rng, d, rng.uniform(6200, 8600), rng.uniform(380, 640)) * np.exp(-tempo(d) / 0.12), rng.uniform(0.8, 1.2)))
        raios.append((quando, estalo(rng, 0.08), 0.9))
    sobras = faiscas(rng, dur - 0.05, 300, decai=(0.002, 0.013), forma=lambda tt: np.exp(-tt / 0.3))

    # a bolha: o ar empurrado — ruído médio que fecha para o grave (o "vum")
    ruido = rng.standard_normal(len(t))
    u = np.clip(t / 0.5, 0, 1)
    vum = (faixa(ruido, 500, 1500) * (1 - u) + faixa(ruido, 100, 420) * u) * np.minimum(t / 0.012, 1.0) * np.exp(-t / 0.28)

    # o campo: o chiado do escudo de pé, tremendo cada vez mais devagar até sumir
    passo = TREME[1] - (TREME[1] - TREME[0]) * np.clip(t / 0.9, 0, 1)
    campo = zumbido(rng, dur, 96) * (0.62 + 0.38 * np.sin(fase(passo))) * np.exp(-t / 0.48)

    x = somar(dur,
              (0.0, fundo, 1.45),
              (0.0, trovao, 1.2),
              (0.0, trava, 1.5),
              *[(q, r, g * 1.15) for q, r, g in raios],
              (0.05, sobras, 0.9),
              (0.0, vum, 1.7),
              (0.03, campo[:len(t) - int(0.03 * SR)], 0.5))
    return acabar(x, -15.0, some=0.35)


if __name__ == "__main__":
    for i, s in enumerate((101, 113), 1): gravar(f"lobo_uivo_energia_{i}", energia(s))
    for i, s in enumerate((127, 139), 1): gravar(f"lobo_uivo_escudo_{i}", escudo(s))
