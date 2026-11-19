#!/usr/bin/env python3
"""
Os calls de rádio (pedido do Jhon, 18/11): dois áudios do ElevenLabs, cada um
com as dez falas em sequência — a Iris (voz feminina, time azul) e o Adam (voz
masculina, time vermelho).

    python3 tools/sons/radio.py <iris.mp3> <adam.mp3> assets/audio

Corta cada arquivo nas nove pausas entre as falas e grava radio_f_1..10.ogg e
radio_m_1..10.ogg, na ordem da lista do jogo (Radio.Calls):

    1 Enemy Spotted!   2 Roger That!     3 Follow Me!     4 Cover Me!
    5 Go, Go, Go!      6 Fall Back!      7 Hold Position! 8 Need Backup!
    9 Sector Clear!   10 Nice Shot!

Cada fala sai aparada, no mesmo volume, com som de rádio: faixa de voz de
rádio (350 a 3.600 Hz), um pouco de saturação e o chiado curto de soltar o
botão no fim.
"""
import subprocess, sys, os
import numpy as np
from scipy import signal

SR = 44100
FALAS = 10


def ler(path):
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', path, '-f', 'f32le', '-ac', '1', '-ar', str(SR), '-'], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).astype(float)


def pausas(x):
    """As pausas entre as falas: trechos de 60 ms ou mais abaixo de -30 dB, fora das pontas."""
    w = int(SR * 0.03); env = np.sqrt(np.convolve(x ** 2, np.ones(w) / w, 'same'))
    hop = int(SR * 0.01); db = 20 * np.log10(env[::hop] / env.max() + 1e-6)
    calmo = db < -30; achadas = []; i = 0
    while i < len(calmo):
        if calmo[i]:
            j = i
            while j < len(calmo) and calmo[j]: j += 1
            if j - i >= 6 and i > 10 and j < len(calmo) - 10: achadas.append(((i + j) / 2 * 0.01, (j - i) * 0.01))
            i = j
        else: i += 1
    # As nove mais longas, na ordem do tempo.
    achadas = sorted(sorted(achadas, key=lambda p: -p[1])[:FALAS - 1])
    return [p[0] for p in achadas]


def radio(a, rng):
    limiar = 0.03 * np.abs(a).max()
    onde = np.nonzero(np.abs(a) > limiar)[0]
    a = a[max(onde[0] - int(0.01 * SR), 0): min(onde[-1] + int(0.05 * SR), len(a))].copy()
    sos = signal.butter(4, [350, 3600], 'bandpass', fs=SR, output='sos')
    a = signal.sosfilt(sos, a)
    a = np.tanh(a / np.abs(a).max() * 2.2) / np.tanh(2.2)             # satura de leve
    a[:200] *= np.linspace(0, 1, 200); a[-400:] *= np.linspace(1, 0, 400)
    # O chiado de soltar o botão.
    n = int(0.07 * SR); chiado = signal.sosfilt(signal.butter(2, [1200, 5000], 'bandpass', fs=SR, output='sos'), rng.standard_normal(n))
    chiado *= np.exp(-np.linspace(0, 5, n)) * 0.16 / np.abs(chiado).max()
    return np.concatenate([a * 0.9, chiado])


def main():
    iris, adam, pasta = sys.argv[1:4]
    rng = np.random.default_rng(5)
    for tag, path in (('f', iris), ('m', adam)):
        x = ler(path); cortes = pausas(x)
        assert len(cortes) == FALAS - 1, f"{path}: achei {len(cortes) + 1} falas, esperava {FALAS}"
        bordas = [0.0] + cortes + [len(x) / SR]
        for k in range(FALAS):
            fala = radio(x[int(bordas[k] * SR): int(bordas[k + 1] * SR)], rng)
            saida = os.path.join(pasta, f'radio_{tag}_{k + 1}.ogg')
            p = subprocess.Popen(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '1', '-i', '-', '-c:a', 'libvorbis', '-q:a', '5', saida], stdin=subprocess.PIPE)
            p.communicate(fala.astype(np.float32).tobytes())
            print(f'{os.path.basename(saida)}: {len(fala) / SR:.2f} s (de {bordas[k]:.2f} a {bordas[k + 1]:.2f})')


if __name__ == '__main__':
    main()
