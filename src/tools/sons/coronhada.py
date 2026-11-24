#!/usr/bin/env python3
"""
O som da coronhada (pedido do Jhon, 18/11: "uma coronhada rápida e uma leve
empurrada com a arma, e fala 'uh...'; sem barulho de faca").

    python3 tools/sons/coronhada.py <pasta com os oc_*.ogg> assets/audio

Sai coronhada_1..3.wav, cada um com três camadas no tempo do golpe:
- o **equipamento** sacudindo (MovementWithGear) no recolher;
- o **ar** cortado (Swoosh, curto e grave) no avanço;
- a **voz**, um "uh" curto de esforço (o começo de um VOXMale Hurt, sem a
  cauda de dor), entrando junto com o golpe.
Tudo do Oculus Audio Pack 1 (CREDITOS.md). Hoje a voz é masculina nos dois
times; a feminina entra quando houver a gravação.
"""
import os, subprocess, sys
import numpy as np
from scipy import signal

SR = 44100


def ler(path):
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', path, '-f', 'f32le', '-ac', '1', '-ar', str(SR), '-'], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).astype(float)


def aparar(a, limiar=0.04):
    onde = np.nonzero(np.abs(a) > limiar * np.abs(a).max())[0]
    return a[max(onde[0] - 60, 0):] if len(onde) else a


def janela(a, dur, cauda=0.06):
    n = min(len(a), int(dur * SR)); a = a[:n].copy()
    m = min(int(cauda * SR), n)
    a[-m:] *= np.linspace(1, 0, m) ** 1.5
    a[:48] *= np.linspace(0, 1, 48)
    return a


def tom(a, semitons):
    if semitons == 0: return a
    fator = 2 ** (semitons / 12)
    return signal.resample(a, int(len(a) / fator))


def por(dest, a, em, ganho):
    i = int(em * SR); j = min(len(dest), i + len(a))
    dest[i:j] += a[:j - i] * ganho


def main():
    fonte, pasta = sys.argv[1], sys.argv[2]
    o = lambda nome: os.path.join(fonte, f'oc_{nome}.ogg')
    vozes = [('VOXMale_Male01Hurt_OS_03', 0), ('VOXMale_Male02Hurt_OS_02', 1), ('VOXMale_Male02Hurt_OS_03', -1)]
    for k, (voz, st) in enumerate(vozes, 1):
        x = np.zeros(int(0.7 * SR))
        gear = janela(aparar(ler(o(f'CLOTHMvmt_MovementWithGear_OS_0{k}'))), 0.22)
        swoosh = janela(tom(aparar(ler(o(f'SWSH_Swoosh_OS_0{k}'))), -3), 0.2, 0.08)
        uh = janela(tom(aparar(ler(o(voz))), st), 0.24, 0.1)
        por(x, gear / np.abs(gear).max(), 0.0, 0.45)
        por(x, swoosh / np.abs(swoosh).max(), 0.05, 0.6)
        por(x, uh / np.abs(uh).max(), 0.06, 0.85)
        x = x[:np.nonzero(np.abs(x) > 1e-4)[0][-1] + 1]
        x = np.tanh(x * 1.3) / np.tanh(1.3)
        x *= 0.9 / np.abs(x).max()
        import wave
        with wave.open(os.path.join(pasta, f'coronhada_{k}.wav'), 'wb') as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
            w.writeframes((x * 32767).astype('<i2').tobytes())
        print(f'coronhada_{k}.wav: {len(x) / SR:.2f} s')


if __name__ == '__main__':
    main()
