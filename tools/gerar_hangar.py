#!/usr/bin/env python3
"""
Gera o mapa Hangar: scenes/maps/hangar.tscn e assets/textures/grid.png.

Mapa próprio no espírito do Arrival — arena retangular, duas bases nas
pontas, heliponto no centro, blocos com telhado nas laterais, pontes
por cima das alas. Tudo caixa cinza: nenhuma peça vem de jogo nenhum.

As medidas são de propósito, não de gosto. Cada número abaixo responde
a uma das quatro perguntas do teste de mapa (CLAUDE.md):

  1. Caminho que só o lobo faz: os becos de 3 m atrás dos blocos. Duas
     paredes paralelas são o que a corrida em parede precisa para subir
     ricocheteando até o telhado de 5 m. O humano chega lá em cima pelas
     rampas — pelo meio, exposto.
  2. Lugar onde o humano vê o lobo chegando: o heliponto, 2 m acima do
     chão, com parapeito, de frente para as alas abertas.
  3. Corredor que o humano deve evitar: os mesmos becos. Três metros é
     distância de garra, não de fuzil.
  4. Cair em cima de quem não olha: as pontes de 5 m sobre as alas, sem
     grade do lado de dentro.

Alturas conferidas contra a física atual: o humano pula 0,99 m (sobe
caixote de 0,8, não sobe contêiner); o Bruto pula 1,54 m (do caixote
alcança o contêiner de 2,2); ninguém pula até 5 m sem parede.

ATENÇÃO: rodar de novo SOBRESCREVE o .tscn. Se o mapa for editado no
editor do Godot, o .tscn passa a ser a fonte e este script vira só o
registro de como ele nasceu.

Uso:  python3 tools/gerar_hangar.py
"""
import math
import os
import struct
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCENE = os.path.join(ROOT, "scenes", "maps", "hangar.tscn")
GRID = os.path.join(ROOT, "assets", "textures", "grid.png")

BLUE, RED = 1, 2

# ----------------------------------------------------------------------
# Materiais. "grid" usa a textura de grade em projeção triplanar no
# espaço do mundo: um ladrilho a cada 2 m, subdivisão a cada 1 m. Em
# caixa cinza é o que dá noção de escala e de velocidade — parede lisa
# não mostra o quanto você andou.

MATERIALS = {
    "floor":   dict(color=(0.42, 0.44, 0.47), grid=True),
    "wall":    dict(color=(0.60, 0.61, 0.64), grid=True),
    "block":   dict(color=(0.72, 0.66, 0.54), grid=True),
    "metal":   dict(color=(0.40, 0.46, 0.53), grid=True, metallic=0.4, roughness=0.55),
    "ramp":    dict(color=(0.56, 0.58, 0.61), grid=True),
    "pad":     dict(color=(0.28, 0.29, 0.31), grid=True),
    "trim":    dict(color=(0.95, 0.74, 0.16), grid=False, roughness=0.6),
    "blue":    dict(color=(0.24, 0.44, 0.86), grid=True),
    "red":     dict(color=(0.84, 0.27, 0.23), grid=True),
    "bluepad": dict(color=(0.16, 0.30, 0.60), grid=True),
    "redpad":  dict(color=(0.58, 0.17, 0.15), grid=True),
    "orange":  dict(color=(0.86, 0.47, 0.16), grid=True),
    "teal":    dict(color=(0.16, 0.50, 0.50), grid=True),
    "green":   dict(color=(0.32, 0.50, 0.27), grid=True),
    "crate":   dict(color=(0.56, 0.43, 0.26), grid=True),
    "beacon":  dict(color=(0.20, 0.85, 1.00), grid=False, emission=(0.20, 0.85, 1.00), energy=2.2),
}

# ----------------------------------------------------------------------
# Peças

parts = []      # (grupo, nome, centro, tamanho, material, rotação|None, colide)
labels = []     # (grupo, nome, posição, texto, tamanho, cor, yaw_graus)
lights = []     # (grupo, nome, posição, cor, energia, alcance)


def box(group, name, c, s, mat, collide=True, rot=None):
    parts.append((group, name, c, s, mat, rot, collide))


def ramp(group, name, x_low, x_high, h, zc, width, mat="ramp", t=0.4):
    """Rampa ao longo de X: chão em x_low, altura h em x_high.

    A superfície de cima passa exatamente pelos dois pontos; a caixa
    se estende 0,4 m em cada ponta para dentro do chão e da plataforma,
    o que tira a fresta onde o pé enroscaria."""
    dx = x_high - x_low
    length = abs(dx)
    theta = math.copysign(math.atan2(h, length), dx)
    c, s_ = math.cos(theta), math.sin(theta)
    rows = ((c, -s_, 0.0), (s_, c, 0.0), (0.0, 0.0, 1.0))
    normal = (-s_, c, 0.0)
    mid = ((x_low + x_high) / 2.0, h / 2.0, zc)
    center = (mid[0] - normal[0] * t / 2, mid[1] - normal[1] * t / 2, zc)
    size = (math.hypot(length, h) + 0.8, t, width)
    box(group, name, center, size, mat, True, rows)


def label(group, name, pos, text, size, color, yaw):
    labels.append((group, name, pos, text, size, color, yaw))


def light(group, name, pos, color, energy, rng):
    lights.append((group, name, pos, color, energy, rng))


# --- Perímetro: 12 m de muro. O beco só tem duas paredes até 5 m de
#     altura, então o lobo sobe até o telhado e não além.
box("Perimetro", "Chao", (0, -0.5, 0), (86, 1, 50), "floor")
box("Perimetro", "MuroNorte", (0, 6, 24.5), (86, 12, 1), "wall")
box("Perimetro", "MuroSul", (0, 6, -24.5), (86, 12, 1), "wall")
box("Perimetro", "MuroLeste", (42.5, 6, 0), (1, 12, 48), "wall")
box("Perimetro", "MuroOeste", (-42.5, 6, 0), (1, 12, 48), "wall")

# --- Centro: heliponto de 2 m com parapeito de 1 m e duas aberturas,
#     uma de frente para cada base.
box("Centro", "Heliponto", (0, 1, 0), (12, 2, 12), "pad")
box("Centro", "Farol", (0, 4, 0), (1.2, 4, 1.2), "beacon")
box("Centro", "ParapeitoNorte", (0, 2.5, 5.75), (12, 1, 0.5), "trim")
box("Centro", "ParapeitoSul", (0, 2.5, -5.75), (12, 1, 0.5), "trim")
for sx, tag in ((1, "Leste"), (-1, "Oeste")):
    for sz, tz in ((1, "Norte"), (-1, "Sul")):
        box("Centro", f"Parapeito{tag}{tz}", (sx * 5.75, 2.5, sz * 4), (0.5, 1, 4), "trim")
ramp("Centro", "RampaHeliLeste", 13, 6, 2, 0, 4)
ramp("Centro", "RampaHeliOeste", -13, -6, 2, 0, 4)
light("Centro", "LuzFarol", (0, 7, 0), (0.3, 0.85, 1.0), 2.0, 14)

# --- Pontes: 28 m de vão a 5 m de altura, ligando os telhados dos
#     blocos. Grade só do lado do muro; do lado da ala, nada — é por
#     ali que se cai em cima de alguém.
for sz, tz in ((1, "Norte"), (-1, "Sul")):
    box("Pontes", f"Ponte{tz}", (0, 4.75, sz * 18.5), (28, 0.5, 3), "metal")
    box("Pontes", f"Ponte{tz}Grade", (0, 5.4, sz * 19.85), (28, 0.8, 0.3), "trim")

# --- Cada lado. s = +1 é o lado B (vermelho), s = −1 o lado A (azul).
for s, side in ((-1, "A"), (1, "B")):
    team_mat = "blue" if side == "A" else "red"
    pad_mat = "bluepad" if side == "A" else "redpad"
    g = f"Base{side}"

    # Base: galpão de 10 x 21 m com teto a 5 m, aberto para o centro.
    box(g, "ParedeNorte", (s * 37, 2.5, 10), (10, 5, 1), team_mat)
    box(g, "ParedeSul", (s * 37, 2.5, -10), (10, 5, 1), team_mat)
    box(g, "Teto", (s * 37, 5.25, 0), (10, 0.5, 21), team_mat)
    box(g, "PilarNorte", (s * 32.5, 2.5, 5), (1, 5, 1), team_mat)
    box(g, "PilarSul", (s * 32.5, 2.5, -5), (1, 5, 1), team_mat)
    box(g, "Piso", (s * 37, 0.012, 0), (10, 0.02, 19), pad_mat, collide=False)

    # Caixotes na boca da base: cobertura para sair, e alto o bastante
    # (0,8 m) para o humano subir.
    box(g, "CaixoteNorte", (s * 29, 0.4, 4), (2, 0.8, 2), "crate")
    box(g, "CaixoteSul", (s * 29, 0.4, -4), (2, 0.8, 2), "crate")

    # Contêineres empilhados entre a base e os blocos.
    for sz, tz in ((1, "Norte"), (-1, "Sul")):
        box(g, f"Conteiner{tz}", (s * 29, 1.1, sz * 15), (6, 2.2, 2.6), "orange")
        box(g, f"Conteiner{tz}Alto", (s * 29, 3.3, sz * 15), (4, 2.2, 2.6), "teal")
        box(g, f"Caixote{tz}Escada", (s * 29, 0.4, sz * 17.9), (1.6, 0.8, 1.6), "crate")

    yaw = 90 if side == "A" else -90
    color = (0.45, 0.65, 1.0) if side == "A" else (1.0, 0.45, 0.4)
    label(g, "Letra", (s * 31.8, 8.2, 0), side, 520, color, yaw)
    label(g, "Nome", (s * 31.8, 6.4, 0), "BASE AZUL" if side == "A" else "BASE VERMELHA",
          110, color, yaw)
    light(g, "Luz", (s * 37, 4.2, 0), color, 1.4, 12)

    # Blocos laterais, com mureta de cobertura no telhado e rampa vindo
    # do centro. Atrás de cada bloco fica o beco de 3 m (Z de 21 a 24).
    L = f"Lado{side}"
    for sz, tz in ((1, "Norte"), (-1, "Sul")):
        box(L, f"Bloco{tz}", (s * 20, 2.5, sz * 16.5), (12, 5, 9), "block")
        box(L, f"Bloco{tz}Mureta", (s * 20, 5.3, sz * 12.25), (12, 0.6, 0.5), "trim")
        ramp(L, f"Rampa{tz}", s * 4, s * 14, 5, sz * 14, 3)
        box(L, f"PilarPonte{tz}", (s * 7, 2.25, sz * 18.5), (1, 4.5, 1), "metal")

    # Cobertura no meio da ala central e caixotes soltos.
    box(L, "ConteinerMeio", (s * 20, 1.3, 0), (2.6, 2.6, 6), "green")
    box(L, "CaixoteMeioNorte", (s * 10, 0.4, 8.5), (1.8, 0.8, 1.8), "crate")
    box(L, "CaixoteMeioSul", (s * 10, 0.4, -8.5), (1.8, 0.8, 1.8), "crate")

# ----------------------------------------------------------------------
# Spawns: seis por base, sob o teto, com folga de pilar e de parede.

SPAWNS = {
    BLUE: [(-39, -6), (-39, -2), (-39, 2), (-39, 6), (-35.5, -7), (-35.5, 7)],
    RED:  [(39, -6), (39, -2), (39, 2), (39, 6), (35.5, -7), (35.5, 7)],
}

# Rotas de ronda: (nome, [(x, y, z)]). y é o piso onde o ponto está —
# a navegação procura o ponto mais perto em 3D, então um ponto de
# telhado com y de chão mandaria o bot para baixo do telhado.

ROUTES = [
    # O centro contorna o farol em vez de atravessá-lo: um ponto dentro
    # de sólido é um destino que a navegação nunca alcança, e o bot fica
    # empurrando a coluna até o detector de travamento desistir.
    ("centro", [(-28, 0.5, 0), (-16, 0.5, 0), (-3, 2.5, 2.5), (3, 2.5, -2.5), (16, 0.5, 0), (28, 0.5, 0)]),
    ("alaNorte", [(-28, 0.5, 8), (-14, 0.5, 10), (0, 0.5, 14), (14, 0.5, 10), (28, 0.5, 8)]),
    ("alaSul", [(-28, 0.5, -8), (-14, 0.5, -10), (0, 0.5, -14), (14, 0.5, -10), (28, 0.5, -8)]),
    ("telhadoNorte", [(-20, 5.5, 16.5), (-9, 5.5, 18.5), (9, 5.5, 18.5), (20, 5.5, 16.5)]),
    ("telhadoSul", [(-20, 5.5, -16.5), (-9, 5.5, -18.5), (9, 5.5, -18.5), (20, 5.5, -16.5)]),
    ("flancoNorte", [(-30, 0.5, 19.5), (-20, 0.5, 22.5), (-6, 0.5, 22), (6, 0.5, 22), (20, 0.5, 22.5), (30, 0.5, 19.5)]),
    ("flancoSul", [(-30, 0.5, -19.5), (-20, 0.5, -22.5), (-6, 0.5, -22), (6, 0.5, -22), (20, 0.5, -22.5), (30, 0.5, -19.5)]),
]

# ----------------------------------------------------------------------
# Textura de grade, escrita à mão para não depender de biblioteca.


def write_grid(path, size=256):
    base, major, minor = 214, 128, 178
    rows = []
    for y in range(size):
        row = bytearray([0])
        for x in range(size):
            v = base
            if x < 3 or y < 3:
                v = major
            elif abs(x - size // 2) < 1 or abs(y - size // 2) < 1:
                v = minor
            row += bytes((v, v, v))
        rows.append(bytes(row))
    raw = zlib.compress(b"".join(rows), 9)

    def chunk(kind, data):
        body = kind + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", raw)
    png += chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


# ----------------------------------------------------------------------
# Escrita do .tscn


def f(v):
    s = f"{v:.4f}".rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


def vec(v):
    return "Vector3(" + ", ".join(f(x) for x in v) + ")"


def color(c, a=1.0):
    return "Color(" + ", ".join(f(x) for x in (*c, a)) + ")"


def xform(pos, rows=None):
    if rows is None:
        rows = ((1, 0, 0), (0, 1, 0), (0, 0, 1))
    flat = [x for r in rows for x in r]
    return "Transform3D(" + ", ".join(f(x) for x in (*flat, *pos)) + ")"


def yaw_rows(deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return ((c, 0, s), (0, 1, 0), (-s, 0, c))


def build():
    shapes, meshes = {}, {}
    for (_, _, _, size, _, _, collide) in parts:
        key = tuple(round(x, 4) for x in size)
        meshes.setdefault(key, f"B{len(meshes) + 1}")
        if collide:
            shapes.setdefault(key, f"S{len(shapes) + 1}")

    ext = [
        ('Texture2D', 'res://assets/textures/grid.png', '1_grid'),
        ('Script', 'res://scripts/core/SpawnPoints.cs', '2_spawns'),
        ('Script', 'res://scripts/core/PatrolRoutes.cs', '3_routes'),
        ('Script', 'res://scripts/core/NavBaker.cs', '4_nav'),
    ]
    steps = 1 + len(ext) + len(MATERIALS) + len(shapes) + len(meshes)

    out = [f"[gd_scene load_steps={steps} format=3]", ""]
    for kind, path, rid in ext:
        out.append(f'[ext_resource type="{kind}" path="{path}" id="{rid}"]')
    out.append("")

    for name, m in MATERIALS.items():
        out.append(f'[sub_resource type="StandardMaterial3D" id="M_{name}"]')
        out.append(f"albedo_color = {color(m['color'])}")
        if m.get("grid"):
            out.append('albedo_texture = ExtResource("1_grid")')
            out.append("uv1_scale = Vector3(0.5, 0.5, 0.5)")
            out.append("uv1_triplanar = true")
            out.append("uv1_world_triplanar = true")
        out.append(f"roughness = {f(m.get('roughness', 0.9))}")
        if m.get("metallic"):
            out.append(f"metallic = {f(m['metallic'])}")
        if m.get("emission"):
            out.append("emission_enabled = true")
            out.append(f"emission = {color(m['emission'])}")
            out.append(f"emission_energy_multiplier = {f(m['energy'])}")
        out.append("")

    for key, rid in shapes.items():
        out += [f'[sub_resource type="BoxShape3D" id="{rid}"]', f"size = {vec(key)}", ""]
    for key, rid in meshes.items():
        out += [f'[sub_resource type="BoxMesh" id="{rid}"]', f"size = {vec(key)}", ""]

    out += ['[node name="Hangar" type="Node3D"]', ""]

    groups = []
    for item in parts + labels + lights:
        if item[0] not in groups:
            groups.append(item[0])
    for g in groups:
        out += [f'[node name="{g}" type="Node3D" parent="."]', ""]

    for (g, name, c, size, mat, rows, collide) in parts:
        key = tuple(round(x, 4) for x in size)
        if collide:
            out += [f'[node name="{name}" type="StaticBody3D" parent="{g}"]',
                    f"transform = {xform(c, rows)}", ""]
            out += [f'[node name="Colisao" type="CollisionShape3D" parent="{g}/{name}"]',
                    f'shape = SubResource("{shapes[key]}")', ""]
            out += [f'[node name="Malha" type="MeshInstance3D" parent="{g}/{name}"]',
                    f'mesh = SubResource("{meshes[key]}")',
                    f'material_override = SubResource("M_{mat}")', ""]
        else:
            out += [f'[node name="{name}" type="MeshInstance3D" parent="{g}"]',
                    f"transform = {xform(c, rows)}",
                    f'mesh = SubResource("{meshes[key]}")',
                    f'material_override = SubResource("M_{mat}")', ""]

    for (g, name, pos, text, size, col, yaw) in labels:
        out += [f'[node name="{name}" type="Label3D" parent="{g}"]',
                f"transform = {xform(pos, yaw_rows(yaw))}",
                "pixel_size = 0.01",
                f"modulate = {color(col)}",
                f'text = "{text}"',
                f"font_size = {size}",
                f"outline_size = {max(8, size // 14)}", ""]

    for (g, name, pos, col, energy, rng) in lights:
        out += [f'[node name="{name}" type="OmniLight3D" parent="{g}"]',
                f"transform = {xform(pos)}",
                f"light_color = {color(col)}",
                f"light_energy = {f(energy)}",
                f"omni_range = {f(rng)}", ""]

    for team, tag in ((BLUE, "A"), (RED, "B")):
        node = f"Spawn{tag}"
        out += [f'[node name="{node}" type="Node3D" parent="."]',
                'script = ExtResource("2_spawns")', f"Team = {team}", ""]
        for i, (x, z) in enumerate(SPAWNS[team], 1):
            out += [f'[node name="p{i}" type="Marker3D" parent="{node}"]',
                    f"transform = {xform((x, 1.2, z))}", ""]

    out += ['[node name="PatrolRoutes" type="Node3D" parent="."]',
            'script = ExtResource("3_routes")', ""]
    for name, points in ROUTES:
        out += [f'[node name="{name}" type="Node3D" parent="PatrolRoutes"]', ""]
        for i, p in enumerate(points, 1):
            out += [f'[node name="p{i}" type="Marker3D" parent="PatrolRoutes/{name}"]',
                    f"transform = {xform(p)}", ""]

    out += ['[node name="NavBaker" type="Node3D" parent="."]',
            'script = ExtResource("4_nav")', ""]

    return "\n".join(out)


if __name__ == "__main__":
    os.makedirs(os.path.dirname(SCENE), exist_ok=True)
    os.makedirs(os.path.dirname(GRID), exist_ok=True)
    write_grid(GRID)
    with open(SCENE, "w", encoding="utf-8") as fh:
        fh.write(build())
    print(f"{len(parts)} peças, {len(labels)} placas, {len(lights)} luzes, "
          f"{sum(len(v) for v in SPAWNS.values())} spawns, {len(ROUTES)} rotas")
    print(f"-> {os.path.relpath(SCENE, ROOT)}")
    print(f"-> {os.path.relpath(GRID, ROOT)}")
