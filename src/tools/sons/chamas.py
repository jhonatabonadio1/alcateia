#!/usr/bin/env python3
"""
Os sons do "em chamas" (o rage), feitos por código (pedido do Jhon, 21/11:
"ao ficar em chamas, deve ter um barulho de fogo e mais efeitos especiais no
som, somente para o jogador que está em chamas").

    python3 tools/sons/chamas.py           # grava em assets/audio/

- chamas_acender       o fogo pega: sopro subindo, brilho que sobe, o "fuuum" grave e as faíscas
- chamas_laco          o fogo ardendo em volta, em laço (4 s, emenda sem estalo), estéreo
- chamas_explosao_1..2 abate ou assistência em chamas: uma lufada de fogo com faíscas
- chamas_apagar        o fogo apaga: chiado descendo e as últimas faíscas

Só o jogador em chamas ouve (FireSound.cs); nenhum som é do mundo. Tudo ruído
filtrado e senoides (numpy e scipy), sem gravação. Estéreo, 44,1 kHz. Cada som
tem a sua semente: rodar de novo dá os mesmos arquivos.
"""
import os, wave
import numpy as np
from scipy import signal

SR = 44100
SAIDA = os.environ.get("SAIDA") or os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "assets", "audio")


def tempo(s):
    return np.arange(int(s * SR)) / SR


def faixa(x, lo, hi, ordem=2):
    sos = signal.butter(ordem, [lo / (SR / 2), min(hi, SR / 2 - 200) / (SR / 2)], "band", output="sos")
    return signal.sosfilt(sos, x)


def baixo(x, f, ordem=2):
    return signal.sosfilt(signal.butter(ordem, f / (SR / 2), "low", output="sos"), x)


def alto(x, f, ordem=2):
    return signal.sosfilt(signal.butter(ordem, f / (SR / 2), "high", output="sos"), x)


def lfo(rng, n, taxa):
    """Um tremor lento e irregular, de 0 a 1: ruído passado num passa-baixas bem baixo."""
    v = baixo(rng.standard_normal(n + SR), taxa, 1)[SR:]
    v = (v - v.min()) / (v.max() - v.min() + 1e-9)
    return v


def varredura(rng, n, de, ate, q=0.35, curva=1.0):
    """Ruído num passa-faixa cujo centro anda de `de` a `ate` (Hz): o sopro que sobe ou desce."""
    x = rng.standard_normal(n)
    out = np.zeros(n)
    bloco = 512
    zi = None
    for i in range(0, n, bloco):
        t = (i / n) ** curva
        f = de * (ate / de) ** t
        lo, hi = f * (1 - q), f * (1 + q)
        sos = signal.butter(2, [max(lo, 30) / (SR / 2), min(hi, SR / 2 - 200) / (SR / 2)], "band", output="sos")
        if zi is None:
            zi = signal.sosfilt_zi(sos) * 0
        out[i:i + bloco], zi = signal.sosfilt(sos, x[i:i + bloco], zi=zi)
    return out


def faiscas(rng, n, por_segundo, forma=None, decai=(0.0015, 0.007), grave=0.0):
    """Estalos de lenha: impulsos ao acaso, cada um um sopro de ruído agudo que some em
    milésimos; `grave` põe em alguns um "toc" de madeira por baixo."""
    env = np.zeros(n)
    taxa = np.full(n, por_segundo) if forma is None else por_segundo * forma
    onde = np.nonzero(rng.random(n) < taxa / SR)[0]
    toc = np.zeros(n)
    for i in onde:
        d = rng.uniform(*decai)
        m = min(int(d * 7 * SR), n - i)
        a = rng.uniform(0.25, 1.0) ** 1.6
        env[i:i + m] = np.maximum(env[i:i + m], a * np.exp(-np.arange(m) / (d * SR)))
        if grave and rng.random() < grave:
            m2 = min(int(0.03 * SR), n - i)
            f = rng.uniform(140, 320)
            toc[i:i + m2] += a * 0.8 * np.sin(2 * np.pi * f * np.arange(m2) / SR) * np.exp(-np.arange(m2) / (0.006 * SR))
    return env * faixa(rng.standard_normal(n), 1600, 11000) + toc


def ronco(rng, n, corte=520, tremor=5.0):
    """O corpo do fogo: ruído grave, marrom, respirando em lufadas irregulares."""
    marrom = np.cumsum(rng.standard_normal(n)) / 60
    marrom = alto(marrom, 35)
    corpo = baixo(marrom, corte, 2)
    lufadas = 0.35 + 0.65 * lfo(rng, n, tremor) ** 1.5
    return corpo * lufadas


def chiado(rng, n, lo=1500, hi=5500, tremor=9.0):
    """O sopro agudo das labaredas."""
    return faixa(rng.standard_normal(n), lo, hi) * (0.3 + 0.7 * lfo(rng, n, tremor))


def estereo(rng, mono_fn, n, abertura=0.35):
    """Dois lados com ruídos diferentes (o fogo em volta, não num ponto)."""
    a = mono_fn(np.random.default_rng(rng.integers(1 << 30)), n)
    b = mono_fn(np.random.default_rng(rng.integers(1 << 30)), n)
    meio = (a + b) / 2
    return np.stack([meio + (a - meio) * (1 + abertura), meio + (b - meio) * (1 + abertura)], axis=1)


def env_ad(n, ataque, queda, total=None):
    t = np.arange(n) / SR
    sobe = np.clip(t / max(ataque, 1e-4), 0, 1) ** 2
    desce = np.exp(-np.maximum(t - ataque, 0) / queda)
    return sobe * desce


def acabar(x, rms_db, pico_db=-1.5, some=0.05, entra=0.003):
    x = x - x.mean(axis=0)
    n = int(some * SR)
    if n:
        x[-n:] *= (np.linspace(1, 0, n) ** 2)[:, None]
    m = max(int(entra * SR), 16)
    x[:m] *= np.linspace(0, 1, m)[:, None]
    x *= 10 ** (rms_db / 20) / (np.sqrt((x ** 2).mean()) + 1e-9)
    teto = 10 ** (pico_db / 20)
    if np.abs(x).max() > teto:
        x = np.tanh(x / teto * 1.1) * teto / np.tanh(1.1)
    return np.clip(x, -teto, teto)


def gravar(nome, x):
    os.makedirs(SAIDA, exist_ok=True)
    caminho = os.path.join(SAIDA, nome + ".wav")
    with wave.open(caminho, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((x * 32767).astype("<i2").tobytes())
    rms = 20 * np.log10(np.sqrt((x ** 2).mean()) + 1e-9)
    grave = np.sum(np.abs(np.fft.rfft(x.mean(axis=1)))[:int(200 * len(x) / SR)] ** 2) / np.sum(np.abs(np.fft.rfft(x.mean(axis=1))) ** 2)
    print(f"{nome}.wav  {len(x) / SR:.2f} s  rms {rms:.1f} dB  pico {20 * np.log10(np.abs(x).max()):.1f} dB  grave {grave * 100:.0f}%")


# ------------------------------------------------------------------ os sons

def acender(seed=11):
    """1,8 s: um sopro que sobe e um brilho que sobe juntos (0 a 0,38 s), o fogo pega
    num "fuuum" grave com estalo, e as labaredas ardem e baixam até o laço assumir."""
    rng = np.random.default_rng(seed)
    dur = 1.8
    n = int(dur * SR)
    t = tempo(dur)
    pega = 0.38
    i = int(pega * SR)

    # o sopro que sobe, com o brilho
    sobe = np.zeros(n)
    m = int(0.42 * SR)
    sopro = varredura(rng, m, 260, 3800, q=0.45, curva=1.6) * np.linspace(0, 1, m) ** 2.2
    sobe[:m] = sopro
    brilho = np.zeros(n)
    tb = t[:m]
    for k, (f0, f1) in enumerate([(420, 1300), (630, 1950), (840, 2600)]):
        fase = 2 * np.pi * np.cumsum(f0 * (f1 / f0) ** (tb / tb[-1])) / SR
        brilho[:m] += np.sin(fase + k) * (0.5 / (k + 1))
    brilho[:m] *= np.linspace(0, 1, m) ** 3 * (0.75 + 0.25 * np.sin(2 * np.pi * 22 * tb))

    # o fogo pega: grave descendo e uma lufada de ruído
    grave = np.zeros(n)
    tg = t[: n - i]
    f = 82 * (38 / 82) ** np.clip(tg / 0.55, 0, 1)
    grave[i:] = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tg / 0.32)
    lufada = np.zeros(n)
    lufada[i:] = baixo(rng.standard_normal(n - i), 900) * env_ad(n - i, 0.012, 0.28)

    # depois, as labaredas e as faíscas, baixando
    cauda = np.clip((t - pega) / 0.05, 0, 1) * np.exp(-np.maximum(t - pega, 0) / 0.75)
    corpo = ronco(rng, n, 700, 7) * cauda
    sopros = chiado(rng, n, 1400, 6000, 11) * cauda
    estalos = faiscas(rng, n, 46, forma=cauda * 1.5, grave=0.3)

    def lado(r, _n):
        return (sobe * 1.0 + brilho * 0.25 + grave * 0.35 + lufada * 0.55
                + corpo * 0.6 + sopros * 0.55 + faiscas(r, n, 30, forma=cauda) * 0.7 + estalos * 0.8)
    x = estereo(rng, lado, n, 0.5)
    return acabar(x, -17.0, some=0.25)


def laco(seed=23):
    """4 s em laço: o ronco respirando, o sopro das labaredas e a lenha estalando.
    Gerado com meio segundo a mais e emendado em cruz: o fim cai no começo sem estalo."""
    rng = np.random.default_rng(seed)
    dur, emenda = 4.0, 0.5
    n = int((dur + emenda) * SR)
    t = tempo(dur + emenda)

    def lado(r, _n):
        corpo = ronco(r, n, 560, 4.5) * 1.0
        sopro = chiado(r, n, 1200, 5200, 7.5) * 0.3
        estalos = faiscas(r, n, 12, grave=0.35) * 0.55
        estouros = faiscas(r, n, 1.2, decai=(0.004, 0.012), grave=0.9) * 0.8
        return corpo + sopro + estalos + estouros
    x = estereo(rng, lado, n, 0.6)

    m, f = int(dur * SR), int(emenda * SR)
    out = x[:m].copy()
    a = np.sqrt(np.linspace(0, 1, f))[:, None]
    out[:f] = x[:f] * a + x[m:m + f] * np.sqrt(1 - a ** 2)
    return acabar(out, -23.0, some=0, entra=0)


def explosao(seed):
    """0,7 s: a lufada de um abate em chamas — soco grave, sopro abrindo e faíscas espirrando."""
    rng = np.random.default_rng(seed)
    dur = 0.75
    n = int(dur * SR)
    t = tempo(dur)
    soco = np.sin(2 * np.pi * np.cumsum(70 * (42 / 70) ** np.clip(t / 0.25, 0, 1)) / SR) * np.exp(-t / 0.14)
    sopro = varredura(rng, n, 500, 2400, q=0.6, curva=0.5) * env_ad(n, 0.01, 0.2)
    corpo = baixo(rng.standard_normal(n), 1100) * env_ad(n, 0.004, 0.12)
    espirro = np.exp(-t / 0.22)

    def lado(r, _n):
        return soco * 0.4 + sopro * 1.4 + corpo * 0.5 + faiscas(r, n, 70, forma=espirro, grave=0.4) * 1.1
    return acabar(estereo(rng, lado, n, 0.5), -19.5, some=0.12)


def apagar(seed=41):
    """1,2 s: o chiado descendo (a chama perdendo força), um sopro abafado e as últimas faíscas."""
    rng = np.random.default_rng(seed)
    dur = 1.2
    n = int(dur * SR)
    t = tempo(dur)
    chio = varredura(rng, n, 4200, 500, q=0.5, curva=0.6) * np.exp(-t / 0.42)
    abafa = baixo(rng.standard_normal(n), 380) * env_ad(n, 0.03, 0.25)
    poucas = np.exp(-t / 0.35)

    def lado(r, _n):
        return chio * 0.9 + abafa * 0.8 + ronco(r, n, 400, 6) * np.exp(-t / 0.3) * 0.8 + faiscas(r, n, 14, forma=poucas) * 0.5
    return acabar(estereo(rng, lado, n, 0.5), -21.0, some=0.3)


def main():
    gravar("chamas_acender", acender())
    gravar("chamas_laco", laco())
    gravar("chamas_explosao_1", explosao(51))
    gravar("chamas_explosao_2", explosao(57))
    gravar("chamas_apagar", apagar())


if __name__ == "__main__":
    main()
