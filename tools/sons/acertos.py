#!/usr/bin/env python3
"""
Os sons de acerto que o Jhon mandou (18/11: "tem que ter o barulho de sangue ao
atirar e acertar alguém, barulho de hit também"), preparados para o jogo.

    python3 tools/sons/acertos.py <pasta com os .wav>

Procura os arquivos pelo fim do nome (o envio põe um código na frente):

- Hit_Generic_5-1, Hit_Generic_2-1, Punch_2-1  ->  impacto_carne_1..3
      a bala (ou a garra) entrando no corpo: toca no ponto do acerto, para
      todo mundo que está perto;
- Stab_4-1  ->  sangue_1..2
      o molhado do sangue espirrando: só a parte do golpe (sem o gesto que vem
      antes), e uma segunda versão mais grave para as duas não soarem iguais.

Mono, 44,1 kHz, pico no nível dos outros impactos (assets/audio/impacto_*).
"""
import glob, os, sys
import numpy as np
from scipy import signal
sys.path.insert(0, os.path.dirname(__file__))
from pacote_armas import SR, ler, mono, cortar, pico, gravar


def achar(pasta, fim):
    lista = sorted(glob.glob(os.path.join(pasta, "*" + fim)))
    if not lista: sys.exit(f"não achei *{fim} em {pasta}")
    return mono(ler(lista[0]))

def aparar(a, limiar=0.02):
    """Começa 3 ms antes do primeiro som de verdade."""
    onde = np.nonzero(np.abs(a) > limiar * np.abs(a).max())[0]
    i = max(int(onde[0]) - int(0.003 * SR), 0)
    a = a[i:].copy()
    a[:64] *= np.linspace(0, 1, 64)
    return a

def tom(a, semitons):
    """Mais grave (ou agudo) e mais longo (ou curto), como fita."""
    fator = 2 ** (-semitons / 12)
    return signal.resample(a, int(len(a) * fator))


def main(pasta):
    gravar("impacto_carne_1", pico(cortar(aparar(achar(pasta, "Hit_Generic_5-1.wav")), 0.26, some=0.06), -5.0))
    gravar("impacto_carne_2", pico(cortar(aparar(achar(pasta, "Hit_Generic_2-1.wav")), 0.36, some=0.08), -5.0))
    gravar("impacto_carne_3", pico(cortar(aparar(achar(pasta, "Punch_2-1.wav")), 0.24, some=0.10), -4.0))

    facada = achar(pasta, "Stab_4-1.wav")
    molhado = cortar(facada, 0.34, some=0.10, inicio=0.185)       # o golpe: de 0,19 s em diante
    molhado[:64] *= np.linspace(0, 1, 64)
    gravar("sangue_1", pico(molhado, -6.0))
    grave = tom(molhado, -3.0)
    gravar("sangue_2", pico(cortar(grave, 0.40, some=0.12), -6.0))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "/mnt/user-data/uploads")
