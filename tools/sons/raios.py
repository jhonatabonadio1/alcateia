#!/usr/bin/env python3
"""
Os sons de raio do especial do lobo, feitos por código (pedido do Jhon, 18/11:
"barulho de raios na hora que usar o especial, que todos podem ouvir").

    python3 tools/sons/raios.py            # grava em assets/audio/

- lobo_especial_armar_1..2   o estalo de carga quando arma (Q) e o chiado elétrico em volta do lobo
- lobo_especial_raio_1..3    o raio do salto: estalo seco, zunido descendo e crepitar
- lobo_especial_queda_1..2   a pancada: trovão curto e faíscas pelo chão

Tudo ruído filtrado e senoides (numpy e scipy); nenhuma gravação. Mono, 44,1 kHz,
no volume dos outros sons do lobo. Cada variação tem a sua semente: rodar de novo
dá os mesmos arquivos.
"""
import os, wave
import numpy as np
from scipy import signal

SR = 44100
SAIDA = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "audio")


def faixa(x, lo, hi, ordem=2):
    b, a = signal.butter(ordem, [lo / (SR / 2), min(hi, SR / 2 - 100) / (SR / 2)], "band")
    return signal.lfilter(b, a, x)

def baixo(x, f, ordem=2):
    b, a = signal.butter(ordem, f / (SR / 2), "low"); return signal.lfilter(b, a, x)

def alto(x, f, ordem=2):
    b, a = signal.butter(ordem, f / (SR / 2), "high"); return signal.lfilter(b, a, x)

def tempo(s):
    return np.arange(int(s * SR)) / SR

def faiscas(rng, dur, por_segundo, decai=(0.002, 0.009), forma=None):
    """Estalos: impulsos ao acaso, cada um um sopro de ruído agudo que some em milésimos."""
    n = int(dur * SR)
    env = np.zeros(n)
    t = tempo(dur)
    taxa = por_segundo if forma is None else por_segundo * forma(t)
    onde = np.nonzero(rng.random(n) < np.broadcast_to(taxa, n) / SR)[0]
    for i in onde:
        d = rng.uniform(*decai)
        m = min(int(d * 6 * SR), n - i)
        env[i:i + m] = np.maximum(env[i:i + m], rng.uniform(0.35, 1.0) * np.exp(-np.arange(m) / (d * SR)))
    return env * faixa(rng.standard_normal(n), 1800, 9000)

def zumbido(rng, dur, f=118.0):
    """O chiado da alta tensão: uma serra grave, cheia de harmônicos, tremendo."""
    t = tempo(dur)
    serra = signal.sawtooth(2 * np.pi * (f * t + 0.6 * np.cumsum(rng.standard_normal(len(t))) / SR * 30))
    trem = 0.6 + 0.4 * np.abs(baixo(rng.standard_normal(len(t)), 28)) * 14
    return faixa(serra, 200, 5200) * np.clip(trem, 0, 1.6)

def varredura(rng, dur, de, ate, largura=0.5):
    """Ruído por um filtro que desce: o zunido do raio."""
    n = int(dur * SR)
    x = rng.standard_normal(n)
    out = np.zeros(n)
    bloco = 256
    zi = None
    for i in range(0, n, bloco):
        u = i / n
        fc = de * (ate / de) ** u
        b, a = signal.butter(2, [fc * (1 - largura / 2) / (SR / 2), min(fc * (1 + largura / 2), SR / 2 - 200) / (SR / 2)], "band")
        if zi is None: zi = signal.lfilter_zi(b, a) * 0
        seg, zi = signal.lfilter(b, a, x[i:i + bloco], zi=zi)
        out[i:i + bloco] = seg
    return out

def estalo(rng, dur=0.09):
    """O estalo seco do raio: ruído cheio que nasce na hora e some rápido."""
    t = tempo(dur)
    return alto(rng.standard_normal(len(t)), 700) * np.exp(-t / 0.018) + 0.5 * rng.standard_normal(len(t)) * np.exp(-t / 0.004)

def somar(dur, *partes):
    out = np.zeros(int(dur * SR))
    for inicio, x, ganho in partes:
        i = int(inicio * SR); m = min(len(x), len(out) - i)
        if m > 0: out[i:i + m] += x[:m] * ganho
    return out

def acabar(x, rms_db=-17.0, pico_db=-1.5, some=0.05):
    x = x - x.mean()
    n = int(some * SR)
    x[-n:] *= np.linspace(1, 0, n) ** 2
    x[:32] *= np.linspace(0, 1, 32)
    x *= 10 ** (rms_db / 20) / (np.sqrt((x ** 2).mean()) + 1e-9)
    # o pico não passa do teto: comprime macio em vez de cortar seco
    teto = 10 ** (pico_db / 20)
    x = np.tanh(x / teto * 1.1) * teto / np.tanh(1.1) if np.abs(x).max() > teto else x
    return np.clip(x, -teto, teto)

def gravar(nome, x):
    os.makedirs(SAIDA, exist_ok=True)
    caminho = os.path.join(SAIDA, nome + ".wav")
    with wave.open(caminho, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((x * 32767).astype("<i2").tobytes())
    rms = 20 * np.log10(np.sqrt((x ** 2).mean()) + 1e-9)
    print(f"{nome}.wav  {len(x) / SR:.2f} s  rms {rms:.1f} dB  pico {20 * np.log10(np.abs(x).max()):.1f} dB")


def armar(seed):
    rng = np.random.default_rng(seed)
    dur = 1.15
    t = tempo(dur)
    sobe = np.clip(t / 0.18, 0, 1)
    carga = np.sin(2 * np.pi * (140 * t + 420 * t ** 2)) * np.exp(-((t - 0.16) / 0.11) ** 2)     # o "vuuup" da carga
    x = somar(dur,
              (0.0, estalo(rng, 0.07), 0.9),
              (0.0, faixa(carga, 150, 3000), 0.5),
              (0.03, faiscas(rng, dur - 0.03, 150, forma=lambda tt: 0.35 + 1.2 * np.exp(-tt / 0.3)), 0.9),
              (0.0, zumbido(rng, dur) * sobe * (0.55 + 0.45 * np.exp(-t / 0.5)), 0.22))
    return acabar(x, -19.0, some=0.25)

def raio(seed):
    rng = np.random.default_rng(seed)
    dur = 1.05
    t = tempo(dur)
    zun = varredura(rng, 0.42, rng.uniform(6500, 8500), rng.uniform(350, 520)) * np.exp(-tempo(0.42) / 0.16)
    grave = np.sin(2 * np.pi * (78 * t - 26 * t ** 2)) * np.exp(-t / 0.22)
    x = somar(dur,
              (0.0, estalo(rng, 0.11), 1.6),
              (0.012, estalo(rng, 0.06), 0.8),
              (0.0, zun, 1.5),
              (0.0, grave, 0.9),
              (0.05, faiscas(rng, dur - 0.05, 260, decai=(0.002, 0.012), forma=lambda tt: np.exp(-tt / 0.28)), 1.1),
              (0.0, zumbido(rng, dur, 132) * np.exp(-t / 0.3), 0.3))
    return acabar(x, -15.5, some=0.2)

def queda(seed):
    rng = np.random.default_rng(seed)
    dur = 1.45
    t = tempo(dur)
    trovao = baixo(rng.standard_normal(len(t)), 240, 3) * np.exp(-t / 0.42) * (1 + 0.5 * np.sin(2 * np.pi * 9 * t) * np.exp(-t / 0.5))
    pancada = np.sin(2 * np.pi * (62 * t - 18 * t ** 2)) * np.exp(-t / 0.2)
    x = somar(dur,
              (0.0, estalo(rng, 0.13), 1.5),
              (0.0, trovao, 2.6),
              (0.0, pancada, 1.3),
              (0.02, varredura(rng, 0.3, 5200, 600) * np.exp(-tempo(0.3) / 0.12), 0.9),
              (0.06, faiscas(rng, dur - 0.06, 190, decai=(0.002, 0.014), forma=lambda tt: np.exp(-tt / 0.45)), 0.95))
    return acabar(x, -15.0, some=0.35)


if __name__ == "__main__":
    for i, s in enumerate((11, 23), 1): gravar(f"lobo_especial_armar_{i}", armar(s))
    for i, s in enumerate((31, 47, 59), 1): gravar(f"lobo_especial_raio_{i}", raio(s))
    for i, s in enumerate((71, 83), 1): gravar(f"lobo_especial_queda_{i}", queda(s))
