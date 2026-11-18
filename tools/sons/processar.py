"""Transforma os arquivos brutos nos sons do jogo, com os nomes que o
Sfx procura.

Da raiz do projeto (precisa de ffmpeg e numpy):

    python3 tools/sons/baixar.py /tmp/sons_brutos
    python3 tools/sons/processar.py /tmp/sons_brutos assets/audio

Sobrescreve só os arquivos que gera; o resto da pasta fica. Para mudar
um som, mude a linha dele aqui e rode de novo — não edite o .wav à mão,
senão a próxima geração apaga a edição. Fontes e licenças em
assets/audio/CREDITOS.md."""
import os, re, sys, subprocess, tempfile, shutil
import numpy as np

RAW, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
TMP = tempfile.mkdtemp()
SR = 44100

def ff(args):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y"] + args, capture_output=True, text=True)
    if r.returncode: raise RuntimeError(r.stderr + " ".join(args))

def peak_db(path):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-i", path, "-af", "volumedetect", "-f", "null", "-"], capture_output=True, text=True)
    m = re.search(r"max_volume: ([\-\d\.]+) dB", r.stderr)
    return float(m.group(1)) if m else 0.0

def dur(path):
    r = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path], capture_output=True, text=True)
    return float(r.stdout.strip())

_n = [0]
def tmp(ext="wav"):
    _n[0] += 1
    return os.path.join(TMP, f"t{_n[0]}.{ext}")

def clip(src, *, start=None, maxlen=None, fade=0.06, pitch=0.0, lowpass=None, highpass=None,
         gate=-42, reverse=False, mono=True):
    """Corta o silêncio do começo, aplica tom e filtros, limita a duração."""
    src = os.path.join(RAW, src) if not os.path.isabs(src) else src
    af = []
    if start is not None:
        af.append(f"atrim=start={start}")
        af.append("asetpts=PTS-STARTPTS")
    af.append(f"silenceremove=start_periods=1:start_threshold={gate}dB:start_silence=0.002")
    if pitch:
        ratio = 2 ** (pitch / 12)
        af.append(f"aresample={SR},asetrate={int(SR*ratio)},aresample={SR}")
    if highpass: af.append(f"highpass=f={highpass}")
    if lowpass: af.append(f"lowpass=f={lowpass}")
    if maxlen:
        af.append(f"atrim=0:{maxlen}")
        af.append(f"afade=t=out:st={max(maxlen-fade,0)}:d={fade}")
    # corta a cauda silenciosa
    af.append("areverse,silenceremove=start_periods=1:start_threshold=-60dB:start_silence=0.01,areverse")
    if reverse: af.append("areverse")
    out = tmp()
    ff(["-i", src, "-af", ",".join(af), "-ac", "1" if mono else "2", "-ar", str(SR), "-c:a", "pcm_s16le", out])
    return out

def mix(parts, mono=True):
    """parts: lista de (arquivo, atraso_s, ganho_db)."""
    out = tmp()
    args, filt = [], []
    for i, (p, delay, gain) in enumerate(parts):
        args += ["-i", p]
        ms = int(delay * 1000)
        filt.append(f"[{i}]adelay={ms}|{ms},volume={gain}dB[a{i}]")
    filt.append("".join(f"[a{i}]" for i in range(len(parts))) + f"amix=inputs={len(parts)}:normalize=0:duration=longest[o]")
    ff(args + ["-filter_complex", ";".join(filt), "-map", "[o]", "-ac", "1" if mono else "2", "-ar", str(SR), "-c:a", "pcm_s16le", out])
    return out

def save(src, name, peak=-3.0, ogg=None, fade_in=0.0):
    """Normaliza o pico e grava em OUT/name (.wav, ou .ogg se longo)."""
    gain = peak - peak_db(src)
    d = dur(src)
    use_ogg = ogg if ogg is not None else d > 2.5
    ext = "ogg" if use_ogg else "wav"
    dst = os.path.join(OUT, f"{name}.{ext}")
    af = f"volume={gain:.2f}dB"
    if fade_in: af += f",afade=t=in:d={fade_in}"
    codec = ["-c:a", "libvorbis", "-q:a", "5"] if use_ogg else ["-c:a", "pcm_s16le"]
    ff(["-i", src, "-af", af] + codec + [dst])
    return dst

def series(name, sources, peak, **kw):
    for i, s in enumerate(sources, 1):
        save(clip(s, **kw), f"{name}_{i}", peak)

oc = lambda n: f"oc_{n}.ogg"
kn = lambda n: f"kn_{n}"
fw = lambda n: f"ff_{n}.wav"

# ---------------------------------------------------------- passos e pouso
# Um conjunto por superfície. "passo" (concreto) é o padrão; os outros
# tocam quando o chão diz o que é (metadado "piso" — veja WorldAudio).
# Lobo: a mesma gravação 5 semitons abaixo, sem agudo: pesado e abafado.
SURFACES = {"": "Concrete", "_metal": "Metal", "_neve": "Snow", "_madeira": "Wood", "_terra": "Dirt"}
for suffix, surface in SURFACES.items():
    run = [oc(f"FEETHmn_Shoe{surface}Run_OS_0{i}") for i in range(1, 6)]
    land = [oc(f"FEETHmn_Shoe{surface}Land_OS_0{i}") for i in (1, 2)]
    series(f"passo{suffix}", run, -3, maxlen=0.35)
    series(f"pouso{suffix}", land, -2, maxlen=0.5)
    # O lobo no concreto pisa com a gravação de terra, como sempre foi:
    # sapato em concreto estala, e pata não estala.
    wolf_run = [oc(f"FEETHmn_ShoeDirtRun_OS_0{i}") for i in range(1, 6)] if not suffix else run
    wolf_land = [oc(f"FEETHmn_ShoeDirtLand_OS_0{i}") for i in (1, 2)] if not suffix else land
    series(f"lobo_passo{suffix}", wolf_run, -2, maxlen=0.4, pitch=-5, lowpass=3200)
    for i, src in enumerate(wolf_land, 1):
        a = clip(src, pitch=-5, lowpass=3000, maxlen=0.6)
        b = clip(kn(f"impact_soft_heavy_00{i}.ogg"), maxlen=0.5)
        save(mix([(a, 0, 0), (b, 0, -4)]), f"lobo_pouso{suffix}_{i}", -1)

# ---------------------------------------------------------- tiros
def onsets(src, min_gap=0.6, rel=0.3):
    """Onde começa cada tiro de uma gravação com vários, em segundos."""
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", os.path.join(RAW, src), "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"],
                         capture_output=True).stdout
    x = np.abs(np.frombuffer(raw, dtype=np.float32))
    win = int(SR * 0.004)
    env = np.convolve(x, np.ones(win) / win, mode="same")
    hot = np.flatnonzero(env > rel * env.max())
    if not len(hot): return []
    starts = [hot[0]] + [b for a, b in zip(hot[:-1], hot[1:]) if b - a > min_gap * SR]
    return [max(t / SR - 0.004, 0) for t in starts]

def takes(sources, count):
    """Até `count` tiros, alternando entre as gravações."""
    found = [[(src, t) for t in onsets(fw(src))] for src in sources]
    out = []
    while len(out) < count and any(found):
        for group in found:
            if group and len(out) < count: out.append(group.pop(0))
    return out

# Cada arma tem a gravação de perto (com dois a quatro tiros dentro) e a
# de meia distância. Cada tiro da gravação vira uma variação: rajada
# com um arquivo só soa como máquina de costura. São as versões
# "Prepared" da biblioteca, já masterizadas — as "Master" são cruas, e
# o tiro cru de microfone colado some em 50 ms.
#
# O tiro é montado em camadas (18/11, pedido do Jhon: "melhore o tiro").
# Medido antes: 93 a 99% da energia do tiro ficava nos primeiros 30 ms,
# e menos de 3% abaixo de 150 Hz (fora o M16) — um estalo, sem corpo e
# sem cauda. As camadas, como se faz em jogo de tiro:
#
#   estalo  a gravação de perto: o ataque, o caráter da arma
#   soco    uma varredura grave sintetizada (o peito do tiro)
#   cauda   a gravação de meia distância da mesma arma, abafada e um
#           pouco atrasada: é a sala respondendo
#   ferro   um clique metálico baixinho logo depois (o mecanismo)
#
# e por fim um limitador, que junta as camadas e sobe o volume médio.
#
# tiro_<arma>_N          mono: o tiro dos outros, num ponto do mapa
# tiro_proprio_<arma>_N  estéreo, mais grave e com a cauda aberta: o seu
def thump(f0, f1, length, decay):
    """O soco: seno que desce de f0 a f1 Hz, morrendo em `decay` s."""
    t = np.arange(int(SR * length)) / SR
    freq = f1 + (f0 - f1) * np.exp(-t / (decay * 0.6))
    wave = np.sin(2 * np.pi * np.cumsum(freq) / SR) * np.exp(-t / decay)
    wave[:int(SR * 0.002)] *= np.linspace(0, 1, int(SR * 0.002))
    out = tmp("raw")
    (wave * 0.9).astype(np.float32).tofile(out)
    wav = tmp()
    ff(["-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", out, "-c:a", "pcm_s16le", wav])
    return wav

def widen(src, ms=11):
    """Mono para estéreo com o lado direito um nada atrasado: abre a cauda."""
    out = tmp()
    ff(["-i", src, "-af", f"pan=stereo|c0=c0|c1=c0,adelay=0|{ms}", "-c:a", "pcm_s16le", out])
    return out

def glue(src, drive, mono):
    """Limitador: empurra `drive` dB contra o teto e junta as camadas."""
    out = tmp()
    ff(["-i", src, "-af", f"volume={drive}dB,alimiter=limit=0.9:attack=2:release=70:level=disabled",
        "-ac", "1" if mono else "2", "-c:a", "pcm_s16le", out])
    return out

# arma: (perto, meia distância, duração, soco [f0, f1, s, dB], cauda dB, ferro dB)
guns = {"m16a2": ("D_32P", "D_24P", 0.95, (150, 52, 0.05, -5), -9, -17),
        "sg552": ("U_14P", "U_19P", 0.95, (160, 50, 0.055, -10), -9, -17),
        "ak47": ("C_28P", "C_31P", 1.0, (140, 45, 0.065, -10), -8, -16),
        "ump45": ("G_31P", "G_20P", 0.8, (170, 60, 0.045, -10), -10, -15),
        "m9": ("X_39P", "X_31P", 0.7, (190, 70, 0.035, -12), -11, -14),
        "m3": ("O_21P", "O_17P", 1.1, (130, 40, 0.085, 0), -7, None)}
iron = clip(kn("metal_click.ogg"), maxlen=0.12, highpass=1200, gate=-45)
for gid, (near, far, ln, (f0, f1, decay, punch), tail_db, iron_db) in guns.items():
    for old in os.listdir(OUT):
        if re.fullmatch(rf"tiro_(proprio_)?{gid}(_\d+)?\.wav", old): os.remove(os.path.join(OUT, old))
    found = takes([near], 4)
    tail = clip(fw(far), maxlen=ln, fade=ln * 0.6, gate=-34, lowpass=4200)
    soco = thump(f0, f1, 0.26, decay)
    for i, (src, t) in enumerate(found, 1):
        for name, mono in ((f"tiro_{gid}_{i}", True), (f"tiro_proprio_{gid}_{i}", False)):
            shot = clip(fw(src), start=t, maxlen=ln, fade=0.35, gate=-30, mono=mono)
            # O dos outros leva menos soco e menos cauda: a sala de
            # verdade (a reverberação do mapa) já entra por cima dele.
            less = 3 if mono else 0
            parts = [(shot, 0, 0), (soco, 0.001, punch - less), (tail if mono else widen(tail), 0.022, tail_db - less)]
            if iron_db is not None: parts.append((iron, 0.045, iron_db))
            # M3: a bombeada logo depois (a cadência é de 0,86 s).
            if gid == "m3":
                parts.append((clip(fw("O_6"), maxlen=0.5, gate=-40, mono=mono), 0.38, -6))
            shot = glue(mix(parts, mono=mono), 3 if mono else 4, mono)
            save(shot, name, -1)
    save(clip(fw(far), maxlen=1.4, fade=0.5, gate=-34, lowpass=5000), f"tiro_distante_{gid}", -4)
    print(f"tiro {gid}: {len(found)} variações")
# Genéricos: tiro = M16; tiro_distante = AK longe.
shutil.copy(os.path.join(OUT, "tiro_m16a2_1.wav"), os.path.join(OUT, "tiro.wav"))
shutil.copy(os.path.join(OUT, "tiro_distante_ak47.wav"), os.path.join(OUT, "tiro_distante.wav"))

# Cápsula no chão, um instante depois do tiro.
series("capsula", [oc(f"BLLTShel_ShellCasingConcrete_OS_0{i}") for i in (1, 2, 3)], -14, maxlen=0.6)

# Bala na parede: o estalo de onde ela bateu. Concreto/alvenaria e metal.
series("impacto", [kn(f"impact_mining_00{i}.ogg") for i in range(3)] + [kn(f"impact_generic_light_00{i}.ogg") for i in range(3)],
       -6, maxlen=0.3, highpass=300)
series("impacto_metal", [kn(f"impact_plate_light_00{i}.ogg") for i in range(3)] + [oc(f"METLImpt_MetalHitSurface_OS_0{i}") for i in (1, 2, 3)],
       -6, maxlen=0.35, highpass=400)

# ---------------------------------------------------------- recarga
def reload(gid, out_src, out_pitch, in_src, in_at, in_pitch=0):
    a = clip(fw(out_src), maxlen=0.6, pitch=out_pitch, gate=-40)
    b = clip(fw(in_src), maxlen=0.6, pitch=in_pitch, gate=-40)
    save(mix([(a, 0, 0), (b, in_at, 0)]), f"recarga_{gid}", -6)
reload("m16a2", "D_5", 0, "D_4", 1.25)
reload("sg552", "D_5", 1, "D_4", 1.4, 1)
reload("ak47", "D_5", -1.5, "C_10", 1.35)
reload("ump45", "D_5", 1.5, "G_8", 1.1)
reload("m9", "X_5", 0, "X_4", 1.0)
save(clip(kn("cloth_1.ogg"), maxlen=0.4), "recarga_m3", -14)
shutil.copy(os.path.join(OUT, "recarga_m16a2.wav"), os.path.join(OUT, "recarga.wav"))
for gid, src in {"m16a2": "D_10", "sg552": "D_13", "ak47": "C_13", "ump45": "G_10", "m9": "X_17", "m3": "O_6"}.items():
    save(clip(fw(src), maxlen=0.5, gate=-26), f"recarga_fim_{gid}", -6)
shutil.copy(os.path.join(OUT, "recarga_fim_m16a2.wav"), os.path.join(OUT, "recarga_fim.wav"))

# Cartucho: cada cartucho entrando no tubo, recortado da gravação de
# carregar a Nova inteira.
src = os.path.abspath(os.path.join(RAW, fw("O_3")))
r = subprocess.run(["ffmpeg", "-hide_banner", "-i", src, "-af", "silencedetect=n=-38dB:d=0.12", "-f", "null", "-"], capture_output=True, text=True)
ends = [float(x) for x in re.findall(r"silence_end: ([\d\.]+)", r.stderr)]
count = 0
for t in ends:
    if count >= 5: break
    c = clip(src, start=max(t - 0.02, 0), maxlen=0.18, fade=0.04, gate=-24)
    if dur(c) < 0.08: continue
    count += 1
    save(c, f"cartucho_{count}", -8)
print("cartuchos:", count)

# ---------------------------------------------------------- corpo
series("pulo", [oc(f"CLOTHMvmt_MovementWithGear_OS_0{i}") for i in range(1, 5)], -12, maxlen=0.4)
series("lobo_pulo", [oc("CREAHmn_TrollEffort_OS_01"), oc("CREAHmn_OgreEffort_OS_01")], -6, pitch=-3, maxlen=0.6)
series("lobo_garra", [oc(f"SWSH_Swoosh_OS_0{i}") for i in range(1, 7)], -5, maxlen=0.45)
series("lobo_bote", [oc(f"WHSH_WhooshMedium_OS_0{i}") for i in range(1, 4)], -4, pitch=-3, maxlen=0.7)
series("lobo_garra_acerto", [oc(f"FGHTImpt_PunchHarder_OS_0{i}") for i in range(1, 5)], -4, maxlen=0.4, lowpass=6000)
series("faca", [kn("knife_slice.ogg"), kn("knife_slice_2.ogg")], -6, maxlen=0.4)
series("granada_lancar", [oc(f"WHSH_WhooshSmall_OS_0{i}") for i in range(1, 4)], -8, maxlen=0.4)
for i, (crunch, low) in enumerate([("000", "000"), ("001", "001"), ("002", "000"), ("003", "001"), ("004", "000")], 1):
    a = clip(kn(f"explosion_crunch_{crunch}.ogg"), maxlen=2.0, fade=0.6)
    b = clip(kn(f"low_frequency_explosion_{low}.ogg"), maxlen=2.0, fade=0.6)
    save(mix([(a, 0, 0), (b, 0, -2)]), f"granada_explosao_{i}", -1, ogg=False)
for i, (a, b) in enumerate([("metal_click", "cloth_belt"), ("metal_latch", "cloth_belt_2")], 1):
    save(mix([(clip(kn(f"{b}.ogg"), maxlen=0.4), 0, -4), (clip(kn(f"{a}.ogg"), maxlen=0.3), 0.12, 0)]), f"trocar_arma_{i}", -10)

series("dor", [oc(f"VOXMale_Male0{v}Hurt_OS_0{i}") for v in (1, 2) for i in (1, 2, 3)], -6, maxlen=0.8)
series("morte", [oc(f"VOXMale_Male0{v}Death_OS_0{i}") for v in (1, 2) for i in (1, 2, 3)], -5, maxlen=1.6)
series("lobo_dor", [oc(f"CREAHmn_{c}Hurt_OS_0{i}") for c in ("Troll", "Ogre") for i in (1, 2, 3)], -5, pitch=-2, maxlen=0.9)
series("lobo_morte", [oc(f"CREAHmn_OgreDeath_OS_0{i}") for i in (1, 2, 3)] + [oc(f"CREAHmn_TrollDeath_OS_0{i}") for i in (1, 2)], -4, pitch=-2, maxlen=2.0)

# Transformação: osso quebrando + rosnado de criatura + respiração.
breath = clip(oc("HMNBrth_MaleBreathing_OS_01"), maxlen=1.2, pitch=-3)
for i in (1, 2, 3):
    bones = clip(oc(f"GOREBone_BonesBreaking_OS_0{i}"), maxlen=1.1)
    growl = clip(oc(f"CREAMnstr_CreatureCreepy_OS_0{i}"), maxlen=1.4, pitch=-3, fade=0.4)
    save(mix([(bones, 0, 0), (growl, 0.15, -1), (breath, 0, -12)]), f"lobo_transformar_{i}", -3)
    # Volta: osso + o rosnado ao contrário, subindo — o lobo saindo.
    back = clip(oc(f"CREAMnstr_CreatureCreepy_OS_0{i}"), maxlen=1.0, pitch=2, reverse=True)
    save(mix([(bones, 0.2, -2), (back, 0, -3)]), f"lobo_destransformar_{i}", -4)

# O uivo (R, de lobo) usa o som da transformação — decisão do Jhon. Não
# há arquivo próprio; veja WorldAudio.HowlSound.

# ---------------------------------------------------------- só para você
# Marcador de acerto: o tique da Kenney sozinho dura 20 ms e some; com
# um toque de impacto leve por baixo ele vira um "toc" que se percebe.
for i, (tick, body) in enumerate([("tick_001", "000"), ("tick_002", "001"), ("tick_004", "002")], 1):
    a = clip(kn(f"{tick}.wav"))
    b = clip(kn(f"impact_generic_light_{body}.ogg"), maxlen=0.12, highpass=500)
    save(mix([(a, 0, 0), (b, 0, -3)]), f"acerto_{i}", -8)
series("acerto_cabeca", [kn(f"impact_metal_light_00{i}.ogg") for i in range(3)], -6, maxlen=0.3, highpass=900)
save(clip(kn("confirmation_002.wav"), mono=False), "abate", -8)
save(clip(oc("UIMisc_BelltreeUp_OS_01"), mono=False, maxlen=2.0, fade=0.8), "respawn", -12)
save(clip(kn("bong_001.wav"), mono=False), "contagem", -10)
# partida_inicio, vitoria, derrota e empate: desde 18/11 são os gerados
# pelo Jhon (tools/sons/preparar_ia.py). Não refazer aqui.

# ---------------------------------------------------------- locutor
# A narração da partida (18/11): Voiceover Pack Fighter, da Kenney (CC0),
# gravado por um ator. Aqui fica um pouco mais grave e mais lento,
# comprimido e com uma sala curta — voz de arena, que corta a mistura.
# As chamadas de sequência (double kill, triple kill…) têm arquivo
# próprio quando houver gravação (docs/AUDIO.md); até lá, todas usam
# voz_multi_kill.
kv = lambda n: f"kv_{n}.ogg"
VOZES = {"voz_prepare": "prepare_yourself", "voz_lutar": "fight", "voz_3": "3", "voz_2": "2", "voz_1": "1",
         "voz_vitoria": "you_win", "voz_derrota": "you_lose", "voz_empate": "it's_a_tie",
         "voz_multi_kill": "multi_kill", "voz_tempo": "time"}
for name, src in VOZES.items():
    dry = clip(kv(src), gate=-45)
    wet = tmp()
    ff(["-i", dry, "-af", f"asetrate={int(SR * 0.94)},aresample={SR},highpass=f=90,"
        "acompressor=threshold=-20dB:ratio=3:attack=4:release=120:makeup=4dB,"
        "aecho=0.85:0.6:48|97:0.16|0.09,alimiter=limit=0.9:level=disabled",
        "-ac", "1", "-c:a", "pcm_s16le", wet])
    save(wet, name, -3, ogg=False)

# ---------------------------------------------------------- interface
# ui_passar, ui_clique e ui_negado: desde 18/11 são os gerados pelo Jhon
# (tools/sons/preparar_ia.py). Não refazer aqui — as variações _1, _2, _3
# daqui tocariam sorteadas junto com os novos.
# Ambiente noturno de floresta como fundo do menu: é loop pronto.
# musica_menu: desde 18/11 é o tema gerado pelo Jhon (tools/sons/preparar_ia.py).

# ---------------------------------------------------------- arma vazia
def dry_click(seed=5):
    """Clique seco do gatilho sem bala: um estalo curto de metal (dois
    modos ressonantes e um pouco de ruído) e o retorno da mola, mais
    fraco, logo atrás. Sintetizado — não tem fonte externa."""
    rng = np.random.default_rng(seed)
    n = int(SR * 0.16)
    t = np.arange(n) / SR
    def tick(at, gain, f1, f2, decay):
        x = np.zeros(n)
        k = t >= at
        u = t[k] - at
        env = np.exp(-u / decay)
        x[k] = (np.sin(2 * np.pi * f1 * u) * 0.6 + np.sin(2 * np.pi * f2 * u) * 0.4 + rng.normal(0, 0.5, k.sum())) * env * gain
        return x
    x = tick(0.0, 1.0, 3100, 5300, 0.004) + tick(0.055, 0.45, 2300, 4100, 0.006)
    pcm = (x / np.abs(x).max() * 0.8 * 32767).astype("<i2")
    out = tmp()
    import wave
    with wave.open(out, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    return out

save(dry_click(), "arma_vazia", -9)

# ---------------------------------------------------------- ambiente
def noise_loop(seconds, low, high, seed, gusts=0.5, tilt=1.0):
    """Vento sintetizado que emenda sem costura: ruído montado na
    frequência (periódico por construção), numa faixa, com rajadas
    lentas também periódicas. Estéreo, um canal de cada sorteio."""
    n = int(SR * seconds)
    freqs = np.fft.rfftfreq(n, 1 / SR)
    chans = []
    for c in (0, 1):
        rng = np.random.default_rng(seed + c)
        band = np.exp(-0.5 * ((np.log(np.maximum(freqs, 1)) - np.log(np.sqrt(low * high))) / (np.log(high / low) / 2.5)) ** 2)
        spec = band / np.maximum(freqs, 20) ** (0.5 * tilt) * np.exp(1j * rng.uniform(0, 2 * np.pi, len(freqs)))
        x = np.fft.irfft(spec, n)
        t = np.arange(n) / SR
        env = np.ones(n)
        for k in range(1, 7):
            env += gusts / k * np.sin(2 * np.pi * k * t / seconds + rng.uniform(0, 2 * np.pi))
        x *= np.maximum(env, 0.15)
        chans.append(x / np.abs(x).max())
    pcm = (np.stack(chans, 1) * 0.8 * 32767).astype("<i2")
    out = tmp()
    import wave
    with wave.open(out, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    return out

# Um fundo por mapa (laço) — baixo, para dar ar ao lugar sem cobrir passo.
save(noise_loop(24, 180, 1400, 11, gusts=0.7), "ambiente_polar", -12, ogg=True)
save(noise_loop(24, 90, 600, 23, gusts=0.5, tilt=1.4), "ambiente_vila", -16, ogg=True)
shutil.copy(os.path.join(RAW, oc("AMBInd_RoomToneMachineRoom_LOOP_01")), os.path.join(OUT, "ambiente_metro.ogg"))
shutil.copy(os.path.join(RAW, oc("MACHHvac_HVAC_LOOP_01")), os.path.join(OUT, "ambiente_hangar.ogg"))
# O trem que passa no túnel ao lado, de vez em quando: chega e vai embora.
a = clip(oc("TRNSbwy_SubwayPassbyEnter_OS_01"), mono=False, lowpass=2500, gate=-60)
b = clip(oc("TRNSbwy_SubwayPassbyExit_OS_01"), mono=False, lowpass=2500, gate=-60)
save(mix([(a, 0, 0), (b, max(dur(a) - 1.0, 0), 0)], mono=False), "metro_trem", -8, ogg=True, fade_in=1.5)

shutil.rmtree(TMP)
files = sorted(f for f in os.listdir(OUT) if f.endswith((".wav", ".ogg")))
print(len(files), "sons,", sum(os.path.getsize(os.path.join(OUT, f)) for f in files) // 1024, "KB")
