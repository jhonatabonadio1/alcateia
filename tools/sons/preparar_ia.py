"""Converte os sons que o Jhon gera com IA (.opus, nomeados pelo prompt) para o jogo: corta o
silêncio do começo, encerra o fim com fade, iguala o volume a um alvo
por tipo e fecha o laço das músicas com um crossfade.

uso: python3 tools/sons/preparar_ia.py pasta_dos_opus assets/audio

A tabela MAP liga o começo do nome do arquivo (o prompt) ao nome que o
jogo procura. Lote novo: acrescente as linhas dele. Os alvos de volume são
medidos contra os sons que já estão no jogo (clique bem abaixo da música).
"""
import os
import subprocess
import sys

import numpy as np

SRC, DST = sys.argv[1], sys.argv[2]
SR = 48000

# prefixo do arquivo -> (nome no jogo, tipo, alvo de volume em dB rms, laço em s)
MAP = [
    ("An epic magical item reveal. Starts with a sharp,-2", "ui_recompensa_2", "sfx", -15, 0),
    ("An epic magical item reveal. Starts with a sharp,", "ui_recompensa_1", "sfx", -15, 0),
    ("An epic rank promotion reveal", "ui_patente", "sfx", -14, 0),
    ("Calm post-match results music", "musica_resultado", "musica", -25, 1.5),
    ("Dark fantasy UI confirm click", "ui_clique", "sfx", -27, 0),
    ("Dark fantasy UI error", "ui_negado", "sfx", -26, 0),
    ("Dark fantasy UI panel close", "ui_fechar", "sfx", -26, 0),
    ("Dark fantasy UI panel open", "ui_abrir", "sfx", -26, 0),
    ("Dark fantasy equip sound", "ui_equipar", "sfx", -23, 0),
    ("Defeat stinger, the heroic theme slowed in a minor-2", "derrota_2", "vinheta", -16, 0),
    ("Defeat stinger, the heroic theme slowed in a minor", "derrota_1", "vinheta", -16, 0),
    ("Draw stinger", "empate", "vinheta", -16, 0),
    ("Epic dark fantasy play button", "ui_jogar", "sfx", -16, 0),
    ("Epic dark fantasy purchase", "ui_compra", "sfx", -17, 0),
    ("Epic dark orchestral main menu theme", "musica_menu", "musica", -21, 2.0),
    ("Final minute tension music", "musica_fim_proximo", "musica", -25, 1.0),
    ("Short battle start orchestral stinger", "partida_inicio", "vinheta", -16, 0),
    ("Subtle dark fantasy UI hover", "ui_passar", "sfx", -42, 0),
    ("Tense pre-battle lobby music", "musica_sala", "musica", -24, 2.0),
    ("Victory fanfare", "vitoria", "vinheta", -15, 0),
    ("XP bar complete", "ui_xp_fim", "sfx", -22, 0),
]


def load(path):
    raw = subprocess.run(
        ["ffmpeg", "-v", "error", "-i", path, "-ar", str(SR), "-ac", "2", "-f", "f32le", "-"],
        capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2).astype(np.float64)


def rms_max(x):
    w = int(0.4 * SR)
    return max(20 * np.log10(np.sqrt((x[i:i + w] ** 2).mean()) + 1e-12)
               for i in range(0, max(len(x) - w, 1), w // 2))


def save(x, name, kind):
    x = np.clip(x, -1, 1).astype("<f4")
    ext = ".wav" if kind == "sfx" else ".ogg"
    codec = ["-c:a", "pcm_s16le"] if ext == ".wav" else ["-c:a", "libvorbis", "-q:a", "6"]
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", "-",
                    *codec, os.path.join(DST, name + ext)], input=x.tobytes(), check=True)
    return name + ext


files = sorted(f for f in os.listdir(SRC) if f.endswith(".opus"))
used = set()
for prefix, name, kind, target, loop in MAP:
    f = next(f for f in files if f.startswith(prefix) and f not in used)
    used.add(f)
    x = load(os.path.join(SRC, f))
    level = 20 * np.log10(np.maximum(np.abs(x).max(axis=1), 1e-9))
    peak = level.max()

    if loop:
        # Laço sem emenda: o fim se funde ao começo.
        c = int(loop * SR)
        fade = np.linspace(0, 1, c)[:, None]
        head = x[:c] * np.sqrt(fade) + x[-c:] * np.sqrt(1 - fade)
        x = np.concatenate([head, x[c:-c]])
    else:
        on = max(int(np.argmax(level > peak - 40)) - int(0.004 * SR), 0)
        end = min(int(np.where(level > peak - 50)[0][-1] + 0.2 * SR), len(x))
        x = x[on:end].copy()
        out_fade = min(int(0.2 * SR), len(x) // 4)
        x[-out_fade:] *= np.linspace(1, 0, out_fade)[:, None]
        in_fade = int(0.002 * SR)
        x[:in_fade] *= np.linspace(0, 1, in_fade)[:, None]

    gain = target - rms_max(x)
    gain = min(gain, -1.0 - 20 * np.log10(np.abs(x).max() + 1e-12))
    x = x * 10 ** (gain / 20)
    out = save(x, name, kind)
    print(f"{out:26s} <- {f[:44]:44s} {len(x) / SR:5.2f}s ganho {gain:+5.1f} dB, "
          f"rms {rms_max(x):5.1f}, pico {20 * np.log10(np.abs(x).max()):5.1f}{'  laço' if loop else ''}")

print(len(used), "de", len(files), "arquivos usados")
