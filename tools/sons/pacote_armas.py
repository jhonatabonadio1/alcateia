#!/usr/bin/env python3
"""
Os sons do pacote que o Jhon mandou (18/11), preparados para o jogo.

    python3 tools/sons/pacote_armas.py <pasta FreeWeaponSFX_v1.1> <pasta "Free Sounds Pack">

O AR-15 era a única arma sem voz própria (caía no tiro e na recarga genéricos):
ganha tiro (o seu, em estéreo e com mais grave; o dos outros; o distante), recarga
montada dos passos (tirar o pente, pegar o novo, encaixar) e o golpe do ferrolho
no fim. Do outro pacote: o rugido do lobo (era o som da transformação), a faca
entrando e duas variações de trocar de arma.

Saída em assets/audio/, mono 44,1 kHz (o tiro próprio em estéreo), no volume dos
sons equivalentes que já existem.
"""
import os, sys, wave
import numpy as np
from scipy import signal

SR = 44100
SAIDA = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "audio")

def ler(p):
    w = wave.open(p); n = w.getnframes(); sw = w.getsampwidth(); ch = w.getnchannels(); sr = w.getframerate()
    raw = w.readframes(n)
    if sw == 2: a = np.frombuffer(raw, dtype="<i2").astype(np.float64) / 32768
    elif sw == 3:
        b = np.frombuffer(raw, dtype=np.uint8).reshape(-1, 3)
        a = (b[:, 0].astype(np.int32) | (b[:, 1].astype(np.int32) << 8) | (b[:, 2].astype(np.int8).astype(np.int32) << 16)).astype(np.float64) / 8388608
    else: a = np.frombuffer(raw, dtype="<i4").astype(np.float64) / 2147483648
    a = a.reshape(-1, ch)
    if sr != SR: a = signal.resample_poly(a, SR, sr, axis=0)
    return a

def mono(a): return a.mean(1)
def db(x): return 10 ** (x / 20)

def cortar(a, dur, some=0.12, inicio=0.0):
    i = int(inicio * SR); n = int(dur * SR)
    a = a[i:i + n].copy()
    if len(a) < n: a = np.concatenate([a, np.zeros((n - len(a),) + a.shape[1:])])
    f = int(some * SR)
    rampa = np.linspace(1, 0, f) ** 2
    a[-f:] *= rampa if a.ndim == 1 else rampa[:, None]
    return a

def pico(a, alvo_db):
    return a * db(alvo_db) / (np.abs(a).max() + 1e-9)

def volume(a, rms_db, teto_db=-1.0):
    """No volume (rms) dos tiros que já existem, sem passar do teto."""
    m = a if a.ndim == 1 else a.mean(1)
    a = a * db(rms_db) / (np.sqrt((m ** 2).mean()) + 1e-9)
    p = np.abs(a).max()
    return a * min(1.0, db(teto_db) / p)

def baixo(a, f):
    b, c = signal.butter(2, f / (SR / 2), "low"); return signal.lfilter(b, c, a, axis=0)

def gravar(nome, a):
    a = np.clip(a, -1, 1)
    ch = 1 if a.ndim == 1 else a.shape[1]
    with wave.open(os.path.join(SAIDA, nome + ".wav"), "wb") as w:
        w.setnchannels(ch); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((a * 32767).astype("<i2").tobytes())
    m = a if a.ndim == 1 else a.mean(1)
    print(f"{nome}.wav  {len(a) / SR:.2f} s  {ch} canal(is)  rms {20 * np.log10(np.sqrt((m ** 2).mean()) + 1e-9):.1f}  pico {20 * np.log10(np.abs(a).max() + 1e-9):.1f}")

def main(armas, geral):
    ar = os.path.join(armas, "AssaultRifle")
    caudas = [ler(os.path.join(ar, "Gunshots", f"sfx_wpn_ar_tail_0{i}.wav")) for i in (1, 2)]
    for i in (1, 2, 3):
        tiro = ler(os.path.join(ar, "Gunshots", f"sfx_wpn_ar_gunshot_0{i}.wav"))
        cauda = caudas[i % 2]
        n = int(0.95 * SR)
        # a cauda vem bem baixa no pacote: sobe para dar corpo ao estampido
        mix = tiro[:n] + cauda[:n] * db(9)
        # o seu: estéreo, com o grave reforçado
        proprio = mix + baixo(mix, 220) * 0.9
        gravar(f"tiro_proprio_ar15_{i}", volume(cortar(proprio, 0.95), -22.5))
        # o dos outros: mono, um pouco menos de agudo (o estalo do pacote é bem claro)
        outro = mono(mix); outro = outro * 0.55 + baixo(outro, 3800) * 0.6
        gravar(f"tiro_ar15_{i}", volume(cortar(outro, 0.93), -22.5))
    gravar("tiro_distante_ar15", volume(cortar(baixo(mono(caudas[0]), 2400), 1.34, some=0.5), -29.5))

    # recarga: os três passos, espaçados como a mão faz
    passos = [("sfx_wpn_ar_reload_01_magOut", 0.10, 0.0), ("sfx_wpn_ar_reload_02_magDraw", 0.72, 8.0), ("sfx_wpn_ar_reload_03_magIn", 1.22, 2.0)]
    rec = np.zeros(int(1.9 * SR))
    for nome, quando, ganho in passos:
        x = mono(ler(os.path.join(ar, "Reload", nome + ".wav"))) * db(ganho)
        i = int(quando * SR); m = min(len(x), len(rec) - i)
        rec[i:i + m] += x[:m]
    gravar("recarga_ar15", pico(cortar(rec, 1.9, some=0.1), -6.0))
    gravar("recarga_fim_ar15", pico(cortar(mono(ler(os.path.join(ar, "Reload", "sfx_wpn_ar_reload_04_charge.wav"))), 0.6, some=0.15), -10.0))
    for i, n in ((3, 1), (4, 2)):
        gravar(f"trocar_arma_{i}", pico(cortar(mono(ler(os.path.join(ar, "Foley", f"sfx_wpn_ar_foley_equip_0{n}.wav"))), 0.55, some=0.12), -10.0))

    if geral:
        rug = mono(ler(os.path.join(geral, "Creature 1-21.wav")))
        rug = cortar(rug, 1.75, some=0.4)
        rug *= db(-17.0) / (np.sqrt((rug ** 2).mean()) + 1e-9)
        teto = db(-1.5)
        gravar("lobo_rugido_1", np.tanh(rug / teto) * teto)
        gravar("faca_acerto_1", pico(cortar(mono(ler(os.path.join(geral, "Stab 4-1.wav"))), 0.6, some=0.2), -4.0))

if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)
