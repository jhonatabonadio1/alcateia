#!/usr/bin/env python3
"""
Os sons das armas de 19/11 (M4A4, AK-74, SCAR-H, SVD, Desert Eagle), feitos dos que o
jogo já tinha — não há gravação delas.

    python3 tools/sons/reais.py

- **Desert Eagle**: os da pistola antiga (Walther PPQ, ver CREDITOS.md), mais graves, e
  a recarga com os dois ruídos (soltar o carregador, encaixar o novo) nos tempos da
  animação nova.
- **M4A4**: os do AR-15, um nada mais agudos.
- **AK-74, SCAR-H, SVD**: os tiros do AR-15 mais graves e mais longos (o SVD com mais
  cauda), e a recarga do AR-15 com os três ruídos (tirar, pegar, encaixar) nos tempos da
  animação de cada uma, que é a do AR-15 mais lenta.

São provisórios de propósito: bons para jogar e fáceis de trocar por gravação (é só pôr
o arquivo com o mesmo nome em assets/audio/). Os da M9 saem: a arma não existe mais.
"""
import os, wave
import numpy as np

AUDIO = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'assets', 'audio')


def ler(nome):
    w = wave.open(os.path.join(AUDIO, nome + '.wav'))
    a = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(float) / 32768
    return a.reshape(-1, w.getnchannels()), w.getframerate()


def gravar(nome, a, sr):
    a = np.clip(a, -1, 1)
    w = wave.open(os.path.join(AUDIO, nome + '.wav'), 'w'); w.setnchannels(a.shape[1]); w.setsampwidth(2); w.setframerate(sr)
    w.writeframes((a * 32767).astype(np.int16).tobytes()); w.close()


def grave(a, tom):
    """Mais grave (e mais longo na mesma razão): reamostra."""
    n = int(len(a) / tom); x = np.arange(n) * tom
    return np.stack([np.interp(x, np.arange(len(a)), a[:, c]) for c in range(a.shape[1])], 1)


def passa_baixa(a, k):
    out = np.zeros_like(a); y = np.zeros(a.shape[1])
    for i in range(len(a)): y = y + k * (a[i] - y); out[i] = y
    return out


def tiro(origem, destino, tom, corpo, cauda):
    a, sr = ler(origem); pico = np.abs(a).max()
    b = grave(a, tom); b = b + corpo * passa_baixa(b, 0.06)                       # mais corpo nos graves
    if cauda > 0:                                                                # a cauda: ecos curtos, cada vez mais abafados
        longo = np.zeros((len(b) + int(sr * 0.5), b.shape[1])); longo[:len(b)] = b; eco = b
        for atraso, ganho in ((0.045, 0.34), (0.11, 0.24), (0.19, 0.16), (0.29, 0.1)):
            eco = passa_baixa(eco, 0.35); k = int(sr * atraso); longo[k:k + len(eco)] += cauda * ganho * eco
        b = longo
    b = np.tanh(b * 1.6) / np.tanh(1.6)
    fim = int(sr * 0.06); b[-fim:] *= np.linspace(1, 0, fim)[:, None]
    gravar(destino, b / np.abs(b).max() * pico, sr)


def recarga(origem, destino, trechos, total, tom=1.0):
    """Monta a recarga: ``trechos`` = [(de, até, onde começa)] em segundos."""
    a, sr = ler(origem); out = np.zeros((int(total * sr), a.shape[1]))
    for de, ate, onde in trechos:
        t = grave(a[int(de * sr):int(ate * sr)], tom); b = int(sr * 0.008)
        t[:b] *= np.linspace(0, 1, b)[:, None]; t[-b:] *= np.linspace(1, 0, b)[:, None]
        k = int(onde * sr); t = t[:max(0, len(out) - k)]; out[k:k + len(t)] += t
    gravar(destino, out, sr)


if __name__ == '__main__':
    # ---- Desert Eagle: da pistola antiga (só enquanto os arquivos dela existem)
    if os.path.exists(os.path.join(AUDIO, 'recarga_m9.wav')):
        for k in (1, 2, 3):
            tiro(f'tiro_m9_{k}', f'tiro_deagle_{k}', 0.78, 0.8, 0.25); tiro(f'tiro_proprio_m9_{k}', f'tiro_proprio_deagle_{k}', 0.78, 0.8, 0.25)
        tiro('tiro_distante_m9', 'tiro_distante_deagle', 0.78, 0.4, 0.15)
        # a animação tem 1,9 s e o jogo a toca em 2,4 s: o carregador sai aos 0,25 s e o novo entra a 1,40 s
        recarga('recarga_m9', 'recarga_deagle', [(0.0, 0.30, 0.24), (0.96, 1.30, 1.36)], 1.9, tom=0.9)
        a, sr = ler('recarga_fim_m9'); gravar('recarga_fim_deagle', grave(a, 0.9), sr)
        for nome in [f for f in os.listdir(AUDIO) if '_m9' in f]: os.remove(os.path.join(AUDIO, nome))

    # ---- os fuzis: do AR-15
    for id_, tom, corpo, cauda, dur in (('m4a4', 1.04, 0.0, 0.0, 2.3), ('ak74', 0.86, 0.5, 0.12, 2.5), ('scar', 0.76, 0.7, 0.3, 2.9), ('svd', 0.64, 0.9, 0.9, 3.0)):
        for k in (1, 2, 3):
            tiro(f'tiro_ar15_{k}', f'tiro_{id_}_{k}', tom, corpo, cauda)
            tiro(f'tiro_proprio_ar15_{k}', f'tiro_proprio_{id_}_{k}', tom, corpo, cauda)
        tiro('tiro_distante_ar15', f'tiro_distante_{id_}', tom, corpo * 0.5, cauda * 0.5)
        r = dur / 2.3                                                             # a animação é a do AR-15 (2,3 s) mais lenta
        recarga('recarga_ar15', f'recarga_{id_}', [(0.08, 0.40, 0.08 * r), (0.72, 1.00, 0.72 * r), (1.22, 1.52, 1.22 * r)], 1.6 * r + 0.2, tom=min(1.0, tom + 0.12))
        a, sr = ler('recarga_fim_ar15'); gravar(f'recarga_fim_{id_}', grave(a, min(1.0, tom + 0.12)), sr)
    print(len([f for f in os.listdir(AUDIO) if f.endswith('.wav') and any(x in f for x in ('m4a4', 'ak74', 'scar', 'svd', 'deagle'))]), 'arquivos')
