#!/usr/bin/env python3
"""
O rugido do lobo, mais agressivo (pedido do Jhon, 18/11, com uma referência de
"rugido de monstro agressivo"): o mesmo bicho do pacote, montado em camadas como
se monta rugido de monstro.

    python3 tools/sons/rugido.py <pasta "Free Sounds Pack">

A gravação do pacote (Creature 1-21) é um rosnado médio e abafado: quase nada
acima de 2 kHz, que é onde mora a agressividade. O rugido novo soma:

- o corpo: a criatura 4 semitons abaixo (maior, mais pesada);
- o fundo: a mesma uma oitava abaixo, só o grave;
- a garganta: o zumbi do pacote (Zombie 1 - Short), 7 semitons abaixo e
  esticado — é o rasgado que faltava;
- a aspereza: tudo tremendo a ~30 vezes por segundo (o "rrrr" do rosnado);
- os dentes: a mistura distorcida, só a faixa de 1,5 a 5 kHz, por cima.

E a forma segue a animação: puxa o ar rosnando baixo (0 a 0,2 s), explode junto
com o pé batendo no chão (0,22 s), sustenta e cai de tom no fim.

Saída: assets/audio/lobo_rugido_1.wav (mono, 44,1 kHz, 1,8 s).
"""
import os, sys
import numpy as np
from scipy import signal
sys.path.insert(0, os.path.dirname(__file__))
from pacote_armas import SR, ler, mono, gravar

def faixa(x, lo, hi, ordem=2):
    b, a = signal.butter(ordem, [lo / (SR / 2), hi / (SR / 2)], "band"); return signal.lfilter(b, a, x)
def baixo(x, f, ordem=2):
    b, a = signal.butter(ordem, f / (SR / 2), "low"); return signal.lfilter(b, a, x)
def alto(x, f, ordem=2):
    b, a = signal.butter(ordem, f / (SR / 2), "high"); return signal.lfilter(b, a, x)

def tom(x, semitons):
    """Mais grave e mais longo (ou o contrário), como fita."""
    return signal.resample(x, int(len(x) * 2 ** (-semitons / 12)))

def encaixar(x, n, inicio=0):
    x = x[inicio:inicio + n]
    return np.concatenate([x, np.zeros(n - len(x))]) if len(x) < n else x

def cair(x, de, ate):
    """O tom escorrega de `de` para `ate` (fator de velocidade) ao longo do som."""
    fator = np.linspace(de, ate, len(x))
    pos = np.cumsum(fator); pos = pos[pos < len(x) - 1]
    return np.interp(pos, np.arange(len(x)), x)

def normal(x): return x / (np.abs(x).max() + 1e-9)


def main(pasta):
    dur = 1.8
    n = int(dur * SR)
    t = np.arange(n) / SR
    rng = np.random.default_rng(7)

    criatura = mono(ler(os.path.join(pasta, "Creature 1-21.wav")))
    zumbi = mono(ler(os.path.join(pasta, "Zombie 1 - Short 1-01.wav")))

    corpo = normal(encaixar(tom(criatura, -4), n, int(0.12 * SR)))
    fundo = normal(baixo(encaixar(tom(criatura, -12), n, int(0.2 * SR)), 320, 3))
    garganta = tom(zumbi[:int(0.62 * SR)], -7)                       # só a parte cheia do zumbi
    # a garganta dura o rugido inteiro: três pedaços emendados com cruzamento, cada um num tom
    pedacos = [garganta, tom(zumbi[int(0.1 * SR):int(0.6 * SR)], -9), tom(zumbi[int(0.05 * SR):int(0.62 * SR)], -6)]
    rasgo = np.zeros(n); onde = int(0.16 * SR); cruza = int(0.09 * SR)
    for p in pedacos:
        p = p.copy(); p[:cruza] *= np.linspace(0, 1, cruza); p[-cruza:] *= np.linspace(1, 0, cruza)
        m = min(len(p), n - onde)
        if m <= 0: break
        rasgo[onde:onde + m] += p[:m]; onde += len(p) - cruza
    rasgo = normal(rasgo)

    # a aspereza: um tremor de ~30 Hz que não é regular (cada rosnado é um pouco diferente)
    ritmo = 30 + 5 * np.cumsum(rng.standard_normal(n)) / SR * 14
    aspero = 0.62 + 0.38 * signal.sawtooth(2 * np.pi * np.cumsum(ritmo) / SR, 0.35)

    mistura = (corpo * 1.0 + rasgo * 1.05) * aspero + fundo * 0.4
    dentes = faixa(np.tanh(mistura * 5.0), 1500, 5200) * 0.62            # o que a gravação não tinha
    x = np.tanh((mistura + dentes) * 1.6)

    # a forma: rosna baixo puxando o ar, explode, sustenta tremendo, cai
    env = np.interp(t, [0, 0.05, 0.2, 0.26, 0.45, 1.05, 1.35, dur], [0, 0.22, 0.34, 1.0, 0.95, 0.85, 0.5, 0])
    env *= 1 + 0.06 * np.sin(2 * np.pi * 9 * t) * (t > 0.3)
    x *= env
    x = cair(x, 1.05, 0.86)                                              # o tom cai no fim: bicho grande soltando o ar
    x = encaixar(x, n)
    x[-int(0.08 * SR):] *= np.linspace(1, 0, int(0.08 * SR))

    x = x - x.mean()
    x *= 10 ** (-15.5 / 20) / np.sqrt((x ** 2).mean())                   # um pouco acima do antigo (-17,4): é o som principal
    teto = 10 ** (-1.0 / 20)
    gravar("lobo_rugido_1", np.tanh(x / teto) * teto)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else ".")
