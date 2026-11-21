#!/usr/bin/env python3
"""
O som ambiente de cada mapa (pedido do Jhon, 20/11: "quero o som de ambiente em todos os mapas, pássaros voando,
buzinas de carros e etc. (deserto não tem carro), mais rico em detalhes").

    python3 tools/sons/ambiente_mapas.py <pasta com os .ogg do Oculus Audio Pack> [mapa ...]

Até aqui a Cidade, o Deserto, a Estação e a Obra tocavam todos o mesmo vento sintetizado (ambiente_vila). Agora cada
um tem o fundo dele, 60 s em estéreo emendado sem costura, feito de gravações do Oculus Audio Pack (o mesmo pacote
da Meta que o jogo já usa — tools/sons/baixar.py; a pasta é corpora/oculus-audio-pack/ogg) em camadas:

- cidade    a cidade ao longe (o trânsito, vozes), pássaros nas árvores da praça e buzinas distantes, uma de cada lado;
- deserto   vento seco, sem carro nenhum; pássaros poucos e longe;
- estacao   a cidade europeia de dia, o zumbido dos trilhos e pombos/pássaros sob a cobertura;
- obra      o canteiro (máquina ao longe, batidas), vento e pássaros;
- neon      a cidade do futuro: o rumor da cidade abafado lá embaixo, o zumbido dos letreiros e os carros voadores
            passando (o motor elétrico subindo e descendo de tom).

E um som de vez em quando por mapa (``EventSound`` em scripts/meta/Maps.cs): evento_passaros (uma revoada),
evento_buzina (duas buzinadas longe), evento_trem (o trem passando ao longe), evento_obra (a serra) e evento_drone.

Fica baixo de propósito (-29 dB no arquivo, e o mapa ainda toca a uns -13): nota-se quando ninguém atira.
Precisa de: numpy, scipy e ffmpeg.
"""
import os, subprocess, sys, tempfile, wave
import numpy as np
from scipy import signal

SR = 44100
AUDIO = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "assets", "audio")
DUR, CRUZA = 60.0, 5.0
N = int(DUR * SR)


def ler(caminho):
    tmp = os.path.join(tempfile.mkdtemp(), "a.wav")
    subprocess.run(["ffmpeg", "-v", "quiet", "-y", "-i", caminho, "-ac", "2", "-ar", str(SR), tmp], check=True)
    w = wave.open(tmp)
    return np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64).reshape(-1, 2) / 32768


def filtro(x, tipo, f, ordem=2):
    corte = np.array(f, dtype=float) / (SR / 2)
    b, a = signal.butter(ordem, corte if corte.ndim else float(corte), tipo)
    return signal.lfilter(b, a, x, axis=0)


def presenca(x, corte=90.0, quanto=1.6):
    """Tira o ronco que caixa pequena não toca e levanta a faixa de 300 Hz a 2 kHz (a que ela toca)."""
    x = filtro(x, "high", corte); return x + quanto * filtro(x, "band", [300, 2000])


def rms(a): return float(np.sqrt((a ** 2).mean()) + 1e-12)


def laco(x, pula=2.0):
    """60 s do som, com o fim cruzado no começo (sem costura). Som mais curto é repetido com cruzamento."""
    x = x[int(pula * SR):]; c = int(CRUZA * SR)
    while len(x) < N + c:
        k = int(1.5 * SR); r = np.linspace(0, 1, k)[:, None]
        x = np.concatenate([x[:-k], x[-k:] * (1 - r) + x[:k] * r, x[k:]])
    x = x[:N + c]; sobe = np.sin(np.linspace(0, np.pi / 2, c))[:, None] ** 2
    corpo = x[:N].copy(); corpo[:c] = x[:c] * sobe + x[N:N + c] * (1 - sobe)
    return corpo


def nivelar(x, quanto=0.5, janela=0.6):
    """Amansa o que sobe muito acima do fundo (é ambiente, não evento)."""
    forca = np.sqrt(filtro((x ** 2).mean(1), "low", janela, 1).clip(1e-12))
    forca = np.concatenate([forca[-SR:], forca, forca[:SR]])
    forca = signal.filtfilt(*signal.butter(1, 0.5 / (SR / 2), "low"), forca)[SR:-SR].clip(1e-6)
    return x * ((np.median(forca) / forca) ** quanto)[:, None]


def eco(x, tempo=1.6, molhado=0.35, semente=1):
    """Uma reverberação simples (ruído que decai): o som fica longe."""
    r = np.random.default_rng(semente); n = int(tempo * SR)
    cauda = r.standard_normal((n, 2)) * np.exp(-np.arange(n) / (tempo * SR / 5.5))[:, None]
    cauda = filtro(cauda, "low", 3500); cauda /= np.sqrt((cauda ** 2).sum(0))
    y = np.stack([signal.fftconvolve(x[:, k], cauda[:, k]) for k in (0, 1)], 1)
    saida = np.zeros((len(y), 2)); saida[:len(x)] = x * (1 - molhado); return saida + y * molhado


def por(fita, som, quando, ganho=1.0, lado=0.0):
    """Soma ``som`` na fita aos ``quando`` segundos (dá a volta no fim: o laço continua fechado). lado: -1 esquerda … 1 direita."""
    som = som * ganho * np.array([np.cos((lado + 1) * np.pi / 4), np.sin((lado + 1) * np.pi / 4)]) * np.sqrt(2)
    i = int(quando * SR) % N; idx = (i + np.arange(len(som))) % N
    np.add.at(fita, idx, som)


def bordas(x, entra=0.05, sai=0.3):
    a, b = int(entra * SR), int(sai * SR); x = x.copy()
    x[:a] *= np.linspace(0, 1, a)[:, None]; x[-b:] *= np.linspace(1, 0, b)[:, None]; return x


def buzina(f0=415.0, f1=520.0, dur=0.42, semente=0):
    """Uma buzina de carro (dois tons, com os harmônicos de uma corneta), ouvida de longe."""
    t = np.arange(int(dur * SR)) / SR; r = np.random.default_rng(semente)
    x = np.zeros_like(t)
    for f in (f0, f1):
        for h in range(1, 9): x += np.sin(2 * np.pi * f * h * t * (1 + 0.002 * r.standard_normal())) / h ** 0.9
    env = np.minimum(1, t / 0.012) * np.minimum(1, (dur - t) / 0.05)
    x = filtro((x * env)[:, None].repeat(2, 1), "band", [300, 2600])
    return x / np.abs(x).max()


def duas_buzinas(semente=0, grave=1.0):
    r = np.random.default_rng(semente)
    a = buzina(415 * grave, 520 * grave, 0.22 + 0.1 * r.random(), semente); b = buzina(415 * grave, 520 * grave, 0.45 + 0.3 * r.random(), semente + 1)
    x = np.zeros((len(a) + len(b) + int(0.16 * SR), 2)); x[:len(a)] = a; x[-len(b):] += b
    return filtro(eco(x, 1.8, 0.5, semente), "low", 2200)


def carro_voador(dur=7.0, f0=95.0, semente=0):
    """O carro voador passando: um motor elétrico (tons que sobem ao chegar e caem ao ir embora) e o ar deslocado."""
    t = np.arange(int(dur * SR)) / SR; r = np.random.default_rng(semente); m = dur / 2
    perto = 1 / np.sqrt(1 + ((t - m) / 0.9) ** 2)                                   # 1 quando passa
    tom = f0 * (1 + 0.16 * np.tanh(-(t - m) / 0.8))                                 # o efeito Doppler
    fase = 2 * np.pi * np.cumsum(tom) / SR
    x = sum(np.sin(fase * h + r.random() * 6) / h for h in (1, 2, 3, 5, 8)) * 0.5
    x += 0.35 * np.sin(fase * 11.03) * perto                                        # o chiado agudo da turbina
    ar = filtro(r.standard_normal(len(t)), "band", [180, 1400]) * 2.5
    x = (x + ar * perto) * perto ** 1.4
    lado = np.tanh((t - m) / 1.2)                                                   # cruza de um lado ao outro
    x = np.stack([x * np.sqrt((1 - lado) / 2), x * np.sqrt((1 + lado) / 2)], 1)
    return bordas(x / np.abs(x).max(), 0.5, 0.8)


def neon(semente=3):
    """O zumbido dos letreiros: 120 Hz e harmônicos, tremendo de leve."""
    t = np.arange(N) / SR
    x = sum(np.sin(2 * np.pi * 60 * h * t + h) / h ** 1.2 for h in (2, 4, 6, 8, 12, 16)) * (1 + 0.1 * np.sin(2 * np.pi * t / 3.0) * np.sin(2 * np.pi * t * 7 / 60.0))
    return np.stack([x, np.roll(x, 211)], 1)


def gravar(nome, x, db=-29.0, pico=0.5):
    x = x * 10 ** (db / 20) / rms(x)
    if np.abs(x).max() > pico: x = np.tanh(x / pico) * pico
    saida = os.path.join(AUDIO, nome + ".ogg"); bruto = os.path.join(tempfile.mkdtemp(), "amb.wav")
    with wave.open(bruto, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())
    subprocess.run(["ffmpeg", "-v", "quiet", "-y", "-i", bruto, "-c:a", "libvorbis", "-q:a", "4", saida], check=True)
    print(f"{nome}.ogg  {len(x) / SR:.0f} s  rms {20 * np.log10(rms(x)):.1f} dB  pico {20 * np.log10(np.abs(x).max()):.1f} dB  {os.path.getsize(saida) // 1024} kB")


def main(pasta, quais):
    f = lambda n: ler(os.path.join(pasta, n + ".ogg"))
    passaros = [bordas(f("AMBForst_ForestBirds_OS_0%d" % k), 0.05, 0.4) for k in (1, 2, 3)]
    for p in passaros: p /= rms(p)
    def camada(x): return x / rms(x)
    def revoadas(fita, quantas, ganho, semente):
        r = np.random.default_rng(semente)
        for k in range(quantas):
            por(fita, filtro(passaros[k % 3], "high", 900), (k + r.random() * 0.7) * DUR / quantas, ganho * (0.6 + 0.6 * r.random()), r.uniform(-0.9, 0.9))
    vento_arvores = camada(nivelar(laco(f("WINDVege_LightWindTreesBirds_LOOP_01"))))
    feitos = {}

    if "cidade" in quais:
        x = camada(nivelar(laco(presenca(f("AMBUrbn_CityAmerican_LOOP_01"))), 0.4)) + 0.45 * vento_arvores
        revoadas(x, 7, 0.5, 11)
        for k, (quando, lado, grave) in enumerate(((7.0, -0.8, 1.0), (23.5, 0.7, 0.84), (41.0, -0.3, 1.12), (52.0, 0.9, 0.94))):
            por(x, duas_buzinas(20 + k, grave), quando, 0.9 * rms(x) * 6, lado)
        feitos["ambiente_cidade"] = x
    if "deserto" in quais:
        r = np.random.default_rng(5); t = np.arange(N) / SR
        ruido = r.standard_normal((N + int(CRUZA * SR) + 2 * SR, 2))
        seco = laco(filtro(ruido, "band", [250, 1500]) + 0.5 * filtro(ruido, "band", [1800, 5200]), 0)
        rajada = 1 + 0.55 * np.sin(2 * np.pi * t / 20.0) * np.sin(2 * np.pi * t / 12.0 + 1) + 0.25 * np.sin(2 * np.pi * t / 7.5 + 2)
        x = camada(seco * rajada[:, None]) + 0.35 * camada(filtro(vento_arvores, "low", 1200))
        revoadas(x, 3, 0.22, 12)                                                    # poucos, e longe
        feitos["ambiente_deserto"] = x
    if "estacao" in quais:
        x = camada(nivelar(laco(presenca(f("AMBUrbn_CityEuroDaytime_LOOP_01"))), 0.4)) + 0.2 * camada(laco(filtro(f("MACHMisc_MachineHum_LOOP_02"), "band", [90, 500])))
        x += 0.3 * vento_arvores; revoadas(x, 5, 0.4, 13)
        por(x, duas_buzinas(31, 0.9), 34.0, rms(x) * 4, -0.6)
        feitos["ambiente_estacao"] = x
    if "obra" in quais:
        x = camada(nivelar(laco(filtro(filtro(f("AMBCnst_ConstructionSiteMedium_LOOP_01"), "high", 80), "low", 7500)), 0.6)) + 0.55 * vento_arvores
        revoadas(x, 6, 0.45, 14)
        feitos["ambiente_obra"] = x
    if "neon" in quais:
        x = camada(nivelar(laco(filtro(presenca(f("AMBUrbn_CityEuroDaytime_LOOP_01"), 90, 1.0), "low", 1400)), 0.4)) + 0.2 * camada(neon()) + 0.3 * camada(laco(filtro(f("MACHMisc_MachineHum_LOOP_01"), "high", 90)))
        for k, (quando, f0) in enumerate(((4.0, 95.0), (19.0, 70.0), (33.0, 120.0), (47.0, 82.0))):
            c = eco(carro_voador(6.0 + k, f0, 40 + k), 1.2, 0.3, 40 + k); por(x, c if k % 2 else c[:, ::-1], quando, rms(x) * 5)
        feitos["ambiente_neon"] = x
    for nome, x in feitos.items(): gravar(nome, x)

    # os sons de vez em quando (tocam por cima do fundo, no volume do ambiente)
    if "eventos" in quais:
        rev = np.zeros((int(9 * SR), 2)); r = np.random.default_rng(2)
        for k in range(5):
            p = filtro(passaros[k % 3], "high", 900); i = int(r.uniform(0, 4.5) * SR); m = min(len(p), len(rev) - i)
            lado = r.uniform(-1, 1); rev[i:i + m] += p[:m] * np.array([1 - max(lado, 0), 1 + min(lado, 0)]) * r.uniform(0.5, 1.0)
        gravar("evento_passaros", bordas(rev, 0.2, 1.5), -24.0)
        gravar("evento_buzina", bordas(duas_buzinas(77), 0.01, 0.6), -24.0)
        trem = f("TRNElec_TrainPassby_OS_01"); gravar("evento_trem", bordas(filtro(trem, "low", 2500), 2.0, 4.0), -26.0)
        gravar("evento_drone", carro_voador(8.0, 88.0, 9), -24.0)
        cao = f("ANMLDog_DogBark_OS_01"); latidos = np.zeros((int(3.2 * SR), 2))
        for i, g in ((0.0, 1.0), (0.55, 0.8), (1.5, 0.9)): latidos[int(i * SR):int(i * SR) + len(cao)] += cao * g
        gravar("evento_cachorro", bordas(filtro(eco(latidos, 1.4, 0.55, 8), "low", 2400), 0.01, 0.5), -27.0)


if __name__ == "__main__":
    pasta = sys.argv[1] if len(sys.argv) > 1 else "."
    main(pasta, sys.argv[2:] or ["cidade", "deserto", "estacao", "obra", "neon", "eventos"])
