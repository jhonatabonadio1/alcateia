#!/usr/bin/env python3
"""
A música da câmera de abate (pedido do Jhon, 18/11: "tem uma música de fundo
nessa visão de abate").

    python3 tools/sons/camera_abate.py assets/audio

Sai `musica_camera_abate.ogg`: 8 s em laço, em ré menor, sintetizados aqui —
um grave que pulsa como coração (ré, duas batidas por compasso), um acorde
escuro de cordas sintéticas por cima (ré, fá, lá, com o dó entrando no fim),
um sino frio a cada compasso e um sopro de ruído que sobe. Tom de "você
perdeu esse duelo", sem melodia que compita com o jogo. Para trocar por uma
música de verdade, basta um arquivo com o mesmo nome em assets/audio.
"""
import os, subprocess, sys
import numpy as np
from scipy import signal

SR = 44100
BPM = 90
DUR = 8.0


def env(n, ataque, queda):
    t = np.arange(n) / SR
    return np.minimum(t / max(ataque, 1e-4), 1.0) * np.exp(-t / queda)


def tom(freq, n, fases=None):
    t = np.arange(n) / SR
    return np.sin(2 * np.pi * freq * t + (0 if fases is None else fases))


def main():
    pasta = sys.argv[1]
    n = int(DUR * SR); t = np.arange(n) / SR
    rng = np.random.default_rng(4)
    mix = np.zeros(n)
    batida = 60 / BPM

    # O coração: ré grave (73,4 Hz) com o sub, duas batidas por tempo forte.
    for k in range(int(DUR / batida)):
        for atraso, forca in ((0.0, 1.0), (0.22, 0.6)):
            i = int((k * batida + atraso) * SR); m = int(0.45 * SR)
            if i + m > n: continue
            g = env(m, 0.005, 0.12)
            mix[i:i + m] += 0.55 * forca * g * (tom(73.4, m) + 0.6 * tom(36.7, m))

    # O acorde escuro: ré, fá, lá (e o dó no fim), cordas de serra filtradas, com vibrato.
    notas = [146.8, 174.6, 220.0]
    sos = signal.butter(2, 900, 'lowpass', fs=SR, output='sos')
    cordas = np.zeros(n)
    for f in notas + [261.6]:
        vib = 2 * np.pi * 0.35 * f * 0.003 * np.sin(2 * np.pi * 5.2 * t) / (2 * np.pi * 5.2)
        onda = signal.sawtooth(2 * np.pi * f * t + vib) + signal.sawtooth(2 * np.pi * f * 1.004 * t)
        entra = np.clip((t - (5.0 if f > 250 else 0.0)) / 1.5, 0, 1)
        cordas += 0.06 * onda * entra
    cordas = signal.sosfilt(sos, cordas) * (0.6 + 0.4 * np.clip(t / 2.0, 0, 1))
    mix += cordas

    # O sino frio: lá agudo com parciais inarmônicos, a cada compasso.
    for k in range(int(DUR / (batida * 4)) + 1):
        i = int(k * batida * 4 * SR + 0.05 * SR); m = int(1.8 * SR)
        if i + m > n: m = n - i
        if m <= 0: continue
        g = env(m, 0.002, 0.6)
        sino = tom(880, m) + 0.5 * tom(880 * 2.76, m) + 0.25 * tom(880 * 5.4, m)
        mix[i:i + m] += 0.07 * g * sino

    # O sopro que sobe no fim.
    ruido = signal.sosfilt(signal.butter(2, [400, 3500], 'bandpass', fs=SR, output='sos'), rng.standard_normal(n))
    mix += 0.05 * ruido * np.clip((t - 4.5) / 3.5, 0, 1) ** 2

    # Fecha em laço: emenda de 0,3 s.
    m = int(0.3 * SR); rampa = np.linspace(0, 1, m)
    mix[:m] = mix[:m] * rampa + mix[-m:] * (1 - rampa)
    mix = mix[:-m]
    mix = np.tanh(mix * 1.4) / np.tanh(1.4)
    mix *= 0.85 / np.abs(mix).max()
    est = np.stack([mix, np.roll(mix, int(0.011 * SR))], 1)

    saida = os.path.join(pasta, 'musica_camera_abate.ogg')
    p = subprocess.Popen(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '2', '-i', '-', '-c:a', 'libvorbis', '-q:a', '5', saida], stdin=subprocess.PIPE)
    p.communicate(est.astype(np.float32).tobytes())
    print(f'{saida}: {len(mix) / SR:.2f} s')


if __name__ == '__main__':
    main()
