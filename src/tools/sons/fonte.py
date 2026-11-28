#!/usr/bin/env python3
"""
O som da fonte (pedido do Jhon, 27/11: "a fonte do mapa deserto teria que ter textura, barulho e movimentação de
água").

    python3 src/tools/sons/fonte.py src/assets/audio

Sai fonte.wav: 8 s de água caindo numa bacia rasa, em laço sem emenda (o começo e o fim são o mesmo som), mono
(toca num ponto do mapa, WaterFeature). É feito aqui, sem gravação de ninguém — numpy e scipy:

- a **queda**: ruído filtrado entre 250 e 900 Hz, que é o corpo da água batendo na água, com o volume oscilando
  devagar (a queda nunca é igual);
- o **chiado**: ruído entre 1,5 e 7 kHz, o respingo fino;
- as **borbulhas**: umas 140 por segundo, cada uma um seno curto que sobe de tom enquanto some (a bolha de ar
  fechando: 0,5 a 3,5 kHz, 8 a 30 ms) — é o que faz o ouvido dizer "água" e não "chuva" ou "vento";
- os **goles**: umas 7 bolhas grandes por segundo (250 a 600 Hz, 40 a 90 ms), o glub da bacia.

O laço: o ruído é feito com 1 s a mais e o fim é fundido no começo; as bolhas que passam do fim continuam no
começo. A semente é fixa: rodar de novo dá o mesmo arquivo.
"""
import os, sys, wave
import numpy as np
from scipy import signal

SR = 44100
DUR = 8.0
SEMENTE = 20261127


def faixa(x, lo, hi, ordem=2):
    b, a = signal.butter(ordem, [lo / (SR / 2), hi / (SR / 2)], btype='band')
    return signal.lfilter(b, a, x)


def lenta(rng, n, hz, fundo=0.55):
    """Uma curva de volume que oscila devagar (até ``hz``), entre ``fundo`` e 1."""
    k = int(n * hz * 2 / SR) + 4
    pontos = rng.random(k)
    curva = signal.resample(np.concatenate([pontos, pontos[:2]]), n + int(2 * n / k))[:n]
    curva = (curva - curva.min()) / max(curva.max() - curva.min(), 1e-9)
    return fundo + (1 - fundo) * curva


def em_laco(x, n):
    """Funde o que passa de ``n`` no começo (potência igual): o fim emenda no começo."""
    sobra = len(x) - n
    t = np.linspace(0, np.pi / 2, sobra)
    out = x[:n].copy()
    out[:sobra] = out[:sobra] * np.sin(t) + x[n:] * np.cos(t)
    return out


def bolhas(rng, n, por_segundo, f_lo, f_hi, d_lo, d_hi, sobe):
    out = np.zeros(n)
    for _ in range(int(por_segundo * n / SR)):
        f0 = np.exp(rng.uniform(np.log(f_lo), np.log(f_hi)))
        dur = rng.uniform(d_lo, d_hi); m = int(dur * SR); t = np.arange(m) / SR
        # a bolha fechando: o tom sobe, o som some
        fase = 2 * np.pi * f0 * (t + sobe * t * t / (2 * dur))
        tom = np.sin(fase) * np.exp(-t / (dur * 0.28)) * np.minimum(t / 0.0008, 1.0)
        amp = rng.random() ** 2.2            # muitas pequenas, poucas grandes
        i = rng.integers(0, n)
        idx = (i + np.arange(m)) % n         # a que passa do fim continua no começo
        out[idx] += tom * amp
    return out


def fazer():
    rng = np.random.default_rng(SEMENTE)
    n = int(DUR * SR); mais = n + SR

    queda = em_laco(faixa(rng.standard_normal(mais), 250, 900), n) * lenta(rng, n, 5.0)
    chiado = em_laco(faixa(rng.standard_normal(mais), 1500, 7000), n) * lenta(rng, n, 9.0, fundo=0.4)
    finas = bolhas(rng, n, 140, 500, 3500, 0.008, 0.030, sobe=0.35)
    goles = bolhas(rng, n, 7, 250, 600, 0.040, 0.090, sobe=0.5)

    def nivel(x, alvo):          # cada camada no volume dela (RMS)
        return x * (alvo / max(np.sqrt(np.mean(x * x)), 1e-9))

    x = nivel(queda, 0.20) + nivel(chiado, 0.085) + nivel(finas, 0.16) + nivel(goles, 0.07)
    b, a = signal.butter(2, 110 / (SR / 2), btype='high'); x = signal.filtfilt(b, a, np.tile(x, 3))[n:2 * n]
    b, a = signal.butter(2, 9500 / (SR / 2), btype='low'); x = signal.filtfilt(b, a, np.tile(x, 3))[n:2 * n]
    x = x * (10 ** (-17 / 20) / np.sqrt(np.mean(x * x)))          # −17 dB de média: é fundo, não frente
    pico = np.abs(x).max()
    if pico > 0.89: x = np.tanh(x / pico * 1.6) / np.tanh(1.6) * 0.89
    return x


if __name__ == '__main__':
    pasta = sys.argv[1] if len(sys.argv) > 1 else 'src/assets/audio'
    x = fazer()
    with wave.open(os.path.join(pasta, 'fonte.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype('<i2').tobytes())
    emenda = abs(x[0] - x[-1])
    print(f'fonte.wav: {len(x) / SR:.1f} s, média {20 * np.log10(np.sqrt(np.mean(x * x))):.1f} dB, pico {np.abs(x).max():.2f}, emenda {emenda:.3f}')
