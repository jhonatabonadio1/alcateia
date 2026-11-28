#!/usr/bin/env python3
"""
A recarga da Desert Eagle nos instantes dos clipes da mão (27/11; Jhon: "às vezes sinto a recarga
ir mais rápido e às vezes mais lenta, aí o som não acompanha").

    python3 src/tools/sons/recarga_deagle.py

A recarga toca a 1× desde 27/11 (`ViewModel.Rig`), no tempo do clipe: a tática em 1,667 s e a de
pente vazio, com o ferrolho voltando no fim, em 2,167 s (`recarga` e `recarga_vazia` de
`fp/deagle.glb`, que vêm da P9). O som antigo (`fontes/recarga_deagle_original.wav`, o da
pistola antiga mais grave; CREDITOS.md) era de uma animação de 1,9 s. Aqui os ruídos dele são
recortados e postos onde o clipe mexe a peça, medido no osso:

- tática: o carregador sai aos 0,35 s e o novo encaixa aos 1,27 s;
- vazia: sai aos 0,47 s, encaixa aos 1,66 s e o ferrolho volta aos 1,83 s.

Só a biblioteca padrão.
"""
import os, struct, wave

AQUI = os.path.dirname(os.path.abspath(__file__))
AUDIO = os.path.join(AQUI, '..', '..', 'assets', 'audio')
FONTE = os.path.join(AQUI, 'fontes', 'recarga_deagle_original.wav')

# os ruídos da fonte: (de, até, o pico) em segundos
SOLTA, ENCAIXA, FERROLHO = (0.06, 0.38, 0.30), (1.08, 1.38, 1.20), (1.72, 1.86, 1.80)


def ler():
    w = wave.open(FONTE); sr = w.getframerate(); d = w.readframes(w.getnframes())
    return list(struct.unpack('<%dh' % (len(d) // 2), d)), sr


def montar(destino, total, pecas):
    """``pecas`` = [(ruído, onde o pico cai)]."""
    a, sr = ler(); out = [0.0] * int(total * sr); borda = int(sr * 0.008)
    for (de, ate, pico), onde in pecas:
        t = a[int(de * sr):int(ate * sr)]; k = int((onde - (pico - de)) * sr)
        for i, v in enumerate(t):
            g = min(1.0, i / borda, (len(t) - 1 - i) / borda)
            if 0 <= k + i < len(out): out[k + i] += v * g
    w = wave.open(os.path.join(AUDIO, destino + '.wav'), 'w'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
    w.writeframes(struct.pack('<%dh' % len(out), *[max(-32768, min(32767, int(v))) for v in out])); w.close()


if __name__ == '__main__':
    montar('recarga_deagle', 1.667, [(SOLTA, 0.35), (ENCAIXA, 1.27)])
    montar('recarga_vazia_deagle', 2.167, [(SOLTA, 0.47), (ENCAIXA, 1.66), (FERROLHO, 1.83)])
    print('recarga_deagle, recarga_vazia_deagle')
