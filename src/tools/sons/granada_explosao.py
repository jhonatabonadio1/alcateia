#!/usr/bin/env python3
"""
A explosão da granada a partir de uma gravação (Jhon, 27/11: "esse barulho de explosão da
granada tem que ter mais barulho de bomba, tá de tijolo atualmente"). A gravação foi gerada
pelo Jhon num gerador de som por IA e está em fontes/granada_explosao_ia.wav (a pasta tem
.gdignore: o Godot não a importa).

    python3 src/tools/sons/granada_explosao.py     # grava em src/assets/audio/

Até 26/11 a explosão era sintetizada (granada.py): estalo, soco grave e estilhaços batendo,
8 a 10% da energia acima de 1 kHz — o "tijolo". A gravação é um estrondo: quase 80% da
energia abaixo de 250 Hz.

- granada_explosao_1..4            de perto: a gravação, cada variação um pouco mais grave ou
                                   mais aguda (tocada mais devagar ou mais depressa), para
                                   duas granadas seguidas não soarem iguais.
- granada_explosao_distante_1..3   longe (passou de uns 30 m): o ar come o agudo, o golpe vira
                                   baque, a rua devolve ecos longos.
- granada_explosao_dentro_1..3     debaixo de teto: reflexões curtas e densas, o grave da sala,
                                   cauda mais curta.

Mono, 44,1 kHz (o jogo põe o som no lugar 3D). O volume do estouro (o primeiro 1,5 s) é o
dos sons sintetizados de antes, família por família (NIVEL), então a mistura do jogo não muda. Cada variação tem a sua semente:
rodar de novo dá os mesmos arquivos. numpy e scipy.
"""
import os, sys
from fractions import Fraction
import numpy as np
from scipy import signal
from scipy.io import wavfile
sys.path.insert(0, os.path.dirname(__file__))
from raios import SR, baixo, tempo, acabar, gravar
from granada import env, ecos, difusa

FONTE = os.path.join(os.path.dirname(__file__), "fontes", "granada_explosao_ia.wav")

# Volume do estouro (dB rms no primeiro 1,5 s), medido nos sons sintetizados que estes trocam.
NIVEL = {"perto": -10.2, "distante": -11.1, "dentro": -10.1}


def nivelar(x, rms_db, pico_db):
    """O estouro no volume `rms_db`, com o pico preso em `pico_db` por compressão macia (nada de corte seco)."""
    teto = 10 ** (pico_db / 20)
    n = int(1.5 * SR)
    for _ in range(6):
        x = x * 10 ** (rms_db / 20) / (np.sqrt((x[:n] ** 2).mean()) + 1e-9)
        if np.abs(x).max() > teto:
            x = teto * np.tanh(x / teto)
    return x


def ler(caminho):
    """A gravação em mono, na taxa do jogo, começando no primeiro som (nada de atraso no estouro)."""
    sr, bruto = wavfile.read(caminho)
    x = bruto.astype(np.float64)
    if np.issubdtype(bruto.dtype, np.integer):
        x /= 32768.0
    if x.ndim > 1:
        x = x.mean(axis=1)
    if sr != SR:
        r = Fraction(SR, sr).limit_denominator(1000)
        x = signal.resample_poly(x, r.numerator, r.denominator)
    inicio = int(np.argmax(np.abs(x) > np.abs(x).max() * 10 ** (-40 / 20)))
    return x[inicio:]


def tocar(x, fator):
    """A gravação tocada `fator` vezes mais depressa: mais aguda e mais curta (abaixo de 1, o contrário)."""
    if abs(fator - 1.0) < 1e-6:
        return x.copy()
    r = Fraction(fator).limit_denominator(100)
    return signal.resample_poly(x, r.denominator, r.numerator)


def caber(x, dur):
    """Corta (ou completa com silêncio) em `dur` segundos."""
    n = int(dur * SR)
    return x[:n] if len(x) >= n else np.concatenate([x, np.zeros(n - len(x))])


def perto(fonte, fator):
    x = caber(tocar(fonte, fator), 3.6)
    # Um pouco de saturação: o estrondo ganha corpo em alto-falante pequeno sem passar do pico.
    x = np.tanh(x * 1.6) / np.tanh(1.6)
    return nivelar(acabar(x, -15.5, pico_db=-1.0, some=0.35), NIVEL["perto"], -1.0)


def distante(fonte, fator, seed):
    rng = np.random.default_rng(seed)
    dur = 4.2
    x = caber(tocar(fonte, fator), dur)
    # o ar come o agudo: o golpe vira baque, sobra o grave; a rua devolve ecos longos
    x = baixo(x, 700, 2)
    x[:int(0.006 * SR)] *= np.linspace(0, 1, int(0.006 * SR))
    x += ecos(x, rng, [rng.uniform(0.35, 0.5), rng.uniform(0.8, 1.1), rng.uniform(1.4, 1.8)], [0.45, 0.3, 0.18], [600, 450, 350])
    x += 0.3 * difusa(x, rng, 2.2, 500)
    return nivelar(acabar(x, -17.0, pico_db=-2.0, some=0.4), NIVEL["distante"], -2.0)


def dentro(fonte, fator, seed):
    rng = np.random.default_rng(seed)
    dur = 2.4
    t = tempo(dur)
    x = caber(tocar(fonte, fator), dur)
    # reflexões curtas e densas, o grave da sala tocando, cauda curta
    atrasos = np.sort(rng.uniform(0.006, 0.045, 9))
    x += ecos(x, rng, atrasos, list(np.linspace(0.5, 0.2, 9)), list(np.linspace(4000, 1500, 9)))
    modo = rng.uniform(95, 125)
    x += np.sin(2 * np.pi * modo * t) * env(t, 0.01, 0.22) * 0.25 * np.abs(x).max()
    x += 0.35 * difusa(x, rng, 0.7, 3000)
    x *= np.exp(-np.maximum(t - 0.4, 0) / 0.55) * 0.6 + 0.4 * np.exp(-t / 0.35)
    x = np.tanh(x / (np.abs(x).max() + 1e-9) * 1.35) / np.tanh(1.35)
    return nivelar(acabar(x, -15.0, pico_db=-1.0, some=0.2), NIVEL["dentro"], -1.0)


if __name__ == "__main__":
    fonte = ler(sys.argv[1] if len(sys.argv) > 1 else FONTE)
    for i, fator in enumerate((1.0, 0.95, 1.05, 0.91)):
        gravar(f"granada_explosao_{i + 1}", perto(fonte, fator))
    for i, fator in enumerate((1.0, 0.94, 1.06)):
        gravar(f"granada_explosao_distante_{i + 1}", distante(fonte, fator, 210 + i))
    for i, fator in enumerate((1.0, 0.96, 1.04)):
        gravar(f"granada_explosao_dentro_{i + 1}", dentro(fonte, fator, 310 + i))
