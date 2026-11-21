# O som da vinheta da Initech Games (scripts/ui/StudioIntro.cs): sopro que sobe,
# impacto grave com brilho e um acorde que soa e some, com o impacto em 1,0 s.
# Tudo sintetizado, sem amostra de terceiros. Rodar da raiz: python3 tools/estudio/som.py
import numpy as np, wave
from scipy import signal
sr = 48000; dur = 4.2; N = int(sr * dur)
t = np.arange(N) / sr
rng = np.random.default_rng(7)
out = np.zeros((N, 2))

def at(t0, n):  # fatia a partir de t0
    i = int(t0 * sr); return slice(i, min(N, i + n))

def onepole_lp(x, fc):  # passa-baixa de um polo com corte variando por amostra
    y = np.zeros_like(x); s = 0.0
    a = 1 - np.exp(-2 * np.pi * fc / sr)
    for i in range(len(x)):
        s += a[i] * (x[i] - s); y[i] = s
    return y

# 1) sopro subindo até o impacto (0,30 → 1,00 s)
t0, t1 = 0.30, 1.00; n = int((t1 - t0) * sr); tt = np.linspace(0, 1, n)
fc = 350 * (6500 / 350) ** (tt ** 1.6)
env = tt ** 2.4
env[-int(0.012 * sr):] *= np.linspace(1, 0, int(0.012 * sr))
hp = signal.butter(2, 180, 'hp', fs=sr, output='sos')
for c in range(2):
    nz = rng.standard_normal(n)
    v = onepole_lp(nz, fc); v = onepole_lp(v, fc * 1.3)
    v = signal.sosfilt(hp, v)
    shimmer = signal.sosfilt(signal.butter(2, 4000, 'hp', fs=sr, output='sos'), rng.standard_normal(n)) * tt ** 4 * 0.25
    out[at(t0, n), c] += (v * 2.2 + shimmer) * env * 0.55
# um tom que sobe junto, bem baixo
f = 180 * (720 / 180) ** (tt ** 1.3); ph = 2 * np.pi * np.cumsum(f) / sr
out[at(t0, n), :] += (np.sin(ph) * env * 0.05)[:, None]

# 2) impacto em 1,00 s
T = 1.00; n = N - int(T * sr); tt = np.arange(n) / sr
f = 38 + 34 * np.exp(-tt / 0.11); ph = 2 * np.pi * np.cumsum(f) / sr
sub = np.tanh(1.6 * np.sin(ph)) * np.exp(-tt / 0.85) * 0.9
thump = np.sin(2 * np.pi * 112 * tt) * np.exp(-tt / 0.13) * 0.45
click = signal.sosfilt(signal.butter(2, 2600, 'lp', fs=sr, output='sos'), rng.standard_normal(n)) * np.exp(-tt / 0.009) * 0.9
hit = sub + thump + click
out[at(T, n), :] += hit[:, None]
for c in range(2):
    body = signal.sosfilt(signal.butter(2, 420, 'lp', fs=sr, output='sos'), rng.standard_normal(n)) * np.exp(-tt / 0.32) * 0.5
    out[at(T, n), c] += body

# 3) acorde que fica (lá menor com nona), brilho de sino por cima
mol = np.zeros((n, 2))
notas = [(110.0, .30, 1.9), (164.81, .22, 1.7), (220.0, .20, 1.6), (329.63, .14, 1.4), (493.88, .10, 1.2), (659.25, .08, 1.0)]
for k, (hz, amp, dec) in enumerate(notas):
    pan = 0.5 if k < 2 else 0.5 + (0.26 if k % 2 else -0.26)
    a = np.minimum(tt / 0.035, 1) * np.exp(-tt / dec)
    for det in (-0.0018, 0.0018):
        tom = np.sin(2 * np.pi * hz * (1 + det) * tt) + 0.18 * np.sin(2 * np.pi * 2 * hz * (1 + det) * tt)
        mol[:, 0] += tom * a * amp * (1 - pan) * 0.7
        mol[:, 1] += tom * a * amp * pan * 0.7
for hz, amp in ((1318.5, .05), (1318.5 * 2.76, .025), (1976.0, .03), (1976.0 * 2.76, .015)):
    a = np.exp(-tt / 0.7)
    mol += (np.sin(2 * np.pi * hz * tt) * a * amp)[:, None]

# 4) cintilado da faixa de luz (1,35 → 2,0 s)
cint = np.zeros((n, 2))
for _ in range(9):
    ts = rng.uniform(0.35, 1.0); hz = rng.uniform(2800, 6200); pan = rng.uniform(0.15, 0.85)
    m = int(0.35 * sr); i = int(ts * sr); u = np.arange(m) / sr
    g = np.sin(2 * np.pi * hz * u) * np.minimum(u / 0.003, 1) * np.exp(-u / 0.09) * rng.uniform(0.02, 0.04)
    m = min(m, n - i); g = g[:m]
    cint[i:i + m, 0] += g * (1 - pan); cint[i:i + m, 1] += g * pan

# 5) reverberação (resposta de ruído que decai), só no impacto, acorde e cintilado
seco = mol + cint
seco[:, 0] += hit * 0.35; seco[:, 1] += hit * 0.35
ir_n = int(2.3 * sr); u = np.arange(ir_n) / sr
ir = rng.standard_normal((ir_n, 2)) * np.exp(-u / 0.55)[:, None]
ir = signal.sosfilt(signal.butter(2, 6000, 'lp', fs=sr, output='sos'), ir, axis=0)
ir[: int(0.02 * sr)] = 0
ir /= np.sqrt((ir ** 2).sum(0))
mol_l = signal.fftconvolve(seco[:, 0], ir[:, 0])[:n]; mol_r = signal.fftconvolve(seco[:, 1], ir[:, 1])[:n]
out[at(T, n), 0] += mol[:, 0] + cint[:, 0] + mol_l * 0.9
out[at(T, n), 1] += mol[:, 1] + cint[:, 1] + mol_r * 0.9

# mestre: entra limpo, some no fim, pico em -2 dBFS
out[: int(0.02 * sr)] *= np.linspace(0, 1, int(0.02 * sr))[:, None]
fim = int(0.7 * sr); out[-fim:] *= (np.linspace(1, 0, fim) ** 1.5)[:, None]
out = np.tanh(out * 0.9) / np.tanh(0.9)
out *= 10 ** (-2 / 20) / np.abs(out).max()
pcm = (out * 32767).astype('<i2')
with wave.open('assets/audio/vinheta_initech.wav', 'wb') as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(sr); w.writeframes(pcm.tobytes())
rms = lambda x: 20 * np.log10(np.sqrt((x ** 2).mean()) + 1e-9)
for a_, b_ in ((0, .3), (.3, 1.0), (1.0, 1.5), (1.5, 2.5), (2.5, 3.5), (3.5, 4.2)):
    print(f"{a_:.1f}-{b_:.1f}s  {rms(out[int(a_*sr):int(b_*sr)]):6.1f} dBFS rms")
