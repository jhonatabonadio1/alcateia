#!/usr/bin/env python3
"""
O som ambiente do Armazém (pedido do Jhon, 18/11: "os mapas devem ter um som
ambiente, tipo esse do Ambient Wind Loop, mas bem mais sutil, para não ficar um
silêncio absoluto na sala").

    python3 tools/sons/ambiente.py <pasta com Ambient_Wind_Loop_1.wav>

O Armazém já tinha um fundo (ambiente_hangar), mas ele é um zumbido de 60 a
200 Hz: em caixa de notebook e fone pequeno não se ouve nada — daí o "silêncio
absoluto". O ambiente novo é o vento do pacote entrando pelo galpão:

- o vento que o Jhon mandou, sem o ronco abaixo de 70 Hz e com a faixa de
  300 Hz a 2 kHz levantada (é a que caixa pequena toca);
- por baixo, o zumbido antigo do galpão, bem fraco, para dar o tamanho da sala;
- 60 s, em estéreo, emendado sem costura (o fim cruza com o começo).

Fica baixo de propósito (-29 dB no arquivo, e o mapa ainda toca a -12): é para
se notar só quando ninguém atira. Saída: assets/audio/ambiente_armazem.ogg.
"""
import glob, os, subprocess, sys, tempfile, wave
import numpy as np
from scipy import signal
sys.path.insert(0, os.path.dirname(__file__))
from pacote_armas import SR, ler

AUDIO = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "audio")
DUR, CRUZA = 60.0, 5.0

def filtro(x, tipo, f, ordem=2):
    corte = np.array(f, dtype=float) / (SR / 2)
    b, a = signal.butter(ordem, corte if corte.ndim else float(corte), tipo)
    return signal.lfilter(b, a, x, axis=0)

def emendar(x):
    """Os últimos CRUZA segundos cruzam com os primeiros: o laço não tem costura."""
    n, c = int(DUR * SR), int(CRUZA * SR)
    x = x[:n + c]
    sobe = np.sin(np.linspace(0, np.pi / 2, c))[:, None] ** 2
    corpo = x[:n].copy()
    corpo[:c] = x[:c] * sobe + x[n:n + c] * (1 - sobe)
    return corpo

def main(pasta):
    achado = sorted(glob.glob(os.path.join(pasta, "*Ambient*Wind*Loop*1.wav")))
    if not achado: sys.exit("não achei o Ambient Wind Loop 1 em " + pasta)
    vento = ler(achado[0])
    if vento.shape[1] == 1: vento = np.repeat(vento, 2, axis=1)
    vento = vento[int(8 * SR):]                                   # pula a entrada
    vento = filtro(vento, "high", 70)
    presenca = filtro(vento, "band", [300, 2000])
    vento = vento + presenca * 2.2
    vento = emendar(vento)
    # as rajadas do original sobem 15 dB acima do fundo: aqui sobem metade disso (é fundo, não evento)
    forca = np.sqrt(filtro((vento ** 2).mean(1), "low", 0.6, 1).clip(1e-12))
    forca = np.concatenate([forca[-SR:], forca, forca[:SR]])            # alisa por cima da costura
    forca = signal.filtfilt(*signal.butter(1, 0.5 / (SR / 2), "low"), forca)[SR:-SR].clip(1e-6)
    vento = vento * ((np.median(forca) / forca) ** 0.5)[:, None]

    # o zumbido antigo, em mono, repetido e cruzado até dar os 60 s
    tmp = os.path.join(tempfile.mkdtemp(), "hangar.wav")
    subprocess.run(["ffmpeg", "-v", "quiet", "-y", "-i", os.path.join(AUDIO, "ambiente_hangar.ogg"), "-ac", "1", "-ar", str(SR), tmp], check=True)
    w = wave.open(tmp); zum = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64) / 32768
    n = int(DUR * SR); c = int(1.5 * SR)
    fita = np.zeros(n + len(zum)); onde = 0
    rampa = np.linspace(0, 1, c)
    while onde < n:
        p = zum.copy(); p[:c] *= rampa; p[-c:] *= rampa[::-1]
        fita[onde:onde + len(p)] += p; onde += len(p) - c
    zum = emendar(np.repeat(fita[:n + int(CRUZA * SR), None], 2, axis=1))

    def rms(a): return np.sqrt((a ** 2).mean())
    x = vento / rms(vento) + zum / rms(zum) * 10 ** (-14 / 20)
    # respira devagar, cada lado no seu tempo: vento não é constante
    t = np.arange(len(x)) / SR
    x[:, 0] *= 1 + 0.18 * np.sin(2 * np.pi * t / 20.0)          # 60 s = 3 voltas: fecha no laço
    x[:, 1] *= 1 + 0.18 * np.sin(2 * np.pi * t / 15.0 + 1.3)    # 4 voltas
    x *= 10 ** (-29 / 20) / rms(x)

    saida = os.path.join(AUDIO, "ambiente_armazem.ogg")
    bruto = os.path.join(tempfile.mkdtemp(), "amb.wav")
    with wave.open(bruto, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())
    subprocess.run(["ffmpeg", "-v", "quiet", "-y", "-i", bruto, "-c:a", "libvorbis", "-q:a", "4", saida], check=True)
    print(f"ambiente_armazem.ogg  {DUR:.0f} s  estéreo  rms {20 * np.log10(rms(x)):.1f} dB  pico {20 * np.log10(np.abs(x).max()):.1f} dB  {os.path.getsize(saida) // 1024} kB")

if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "/mnt/user-data/uploads")
