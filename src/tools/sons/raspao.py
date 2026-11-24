#!/usr/bin/env python3
"""
A bala passando de raspão (pedido do Jhon, 18/11: "se eu levar tiro de raspão,
ou se nem pegar mas passar bem próximo, devo ouvir um barulho de 'vuum'").

    python3 tools/sons/raspao.py assets/audio

Sai bala_raspao_1..4.wav, sintetizados: um sopro de ar que chega e passa em
~0,22 s — ruído numa faixa que desce de tom (o efeito Doppler de algo passando
rápido), com um zumbido grave por baixo e o estalo curto do ar na passagem.
Cada variação tem tom e duração um pouco diferentes.
"""
import sys, os, wave
import numpy as np
from scipy import signal

SR = 44100


def passar(rng, dur, tom):
    n = int(dur * SR); t = np.arange(n) / SR; u = t / dur
    ruido = rng.standard_normal(n)
    # A faixa do sopro desce de tom ao passar: filtra em pedaços curtos.
    saida = np.zeros(n); passo = 256
    for i in range(0, n, passo):
        centro = tom * (1.9 - 1.25 * (i / n) ** 0.7)
        sos = signal.butter(2, [centro * 0.6, min(centro * 1.5, SR * 0.45)], 'bandpass', fs=SR, output='sos')
        a = max(i - passo, 0); b = min(i + 2 * passo, n)
        saida[i:i + passo] = signal.sosfilt(sos, ruido[a:b])[i - a:i - a + passo][:min(passo, n - i)]
    # O zumbido: um tom que cai (o "vuum").
    fase = 2 * np.pi * np.cumsum(tom * 0.22 * (1.6 - 0.9 * u)) / SR
    zum = np.sin(fase) + 0.4 * np.sin(2 * fase)
    # Chega rápido, passa (o pico a 35%) e some.
    env = np.exp(-((u - 0.35) / 0.2) ** 2)
    x = saida / np.abs(saida).max() * env + 0.45 * zum * env ** 1.5
    # O estalo do ar na passagem.
    k = int(0.35 * n); m = int(0.004 * SR)
    x[k:k + m] += rng.standard_normal(m) * np.linspace(0.5, 0, m)
    x[:64] *= np.linspace(0, 1, 64); x[-256:] *= np.linspace(1, 0, 256)
    return x / np.abs(x).max() * 0.85


def main():
    pasta = sys.argv[1]; rng = np.random.default_rng(11)
    for i, (dur, tom) in enumerate(((0.22, 1500), (0.26, 1250), (0.19, 1800), (0.24, 1050)), 1):
        x = passar(rng, dur, tom)
        with wave.open(os.path.join(pasta, f'bala_raspao_{i}.wav'), 'wb') as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
            w.writeframes((x * 32767).astype('<i2').tobytes())
        print(f'bala_raspao_{i}.wav: {dur:.2f} s')


if __name__ == '__main__':
    main()
