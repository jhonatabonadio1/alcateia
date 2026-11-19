#!/usr/bin/env python3
"""
A respiração ofegante de quem está quase morrendo (pedido do Jhon, 18/11:
"quero um barulho de respiração com falta de ar").

    python3 tools/sons/respiracao.py <oc_HMNBrth_MaleBreathing_OS_01.ogg> assets/audio

Sai `respiracao_ofegante.ogg`, um laço de ~3,4 s. A origem é a respiração
masculina do Oculus Audio Pack 1 (a mesma licença do resto do pacote, ver
CREDITOS.md): dez segundos de alguém recuperando o fôlego. Só os quatro
primeiros ciclos servem — os mais fortes e curtos; daí em diante ela acalma.
O trecho é acelerado em 12% (falta de ar é respiração curta), ganha um pouco
de agudo (o ar passando) e fecha em laço com uma emenda de 90 ms.
"""
import subprocess, sys, os
import numpy as np

SR = 44100
DE, ATE = 0.04, 3.86      # o trecho forte, de silêncio a silêncio
RITMO = 1.12
EMENDA = 0.09


def main():
    origem, pasta = sys.argv[1], sys.argv[2]
    filtro = f"atrim={DE}:{ATE},asetpts=PTS-STARTPTS,atempo={RITMO},highshelf=f=3500:g=4,highpass=f=120"
    bruto = subprocess.run(['ffmpeg', '-v', 'error', '-i', origem, '-af', filtro, '-f', 'f32le', '-ac', '1', '-ar', str(SR), '-'],
                           capture_output=True, check=True).stdout
    x = np.frombuffer(bruto, np.float32).astype(np.float64)

    # Laço: o fim se mistura ao começo.
    n = int(EMENDA * SR)
    rampa = np.linspace(0, 1, n)
    corpo = x[:-n].copy()
    corpo[:n] = corpo[:n] * rampa + x[-n:] * (1 - rampa)
    corpo *= 0.85 / np.abs(corpo).max()

    saida = os.path.join(pasta, 'respiracao_ofegante.ogg')
    p = subprocess.Popen(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '1', '-i', '-', '-c:a', 'libvorbis', '-q:a', '5', saida],
                         stdin=subprocess.PIPE)
    p.communicate(corpo.astype(np.float32).tobytes())
    print(f'{saida}: {len(corpo) / SR:.2f} s')


if __name__ == '__main__':
    main()
