"""
A skin do AR-15 Ultimate: roxo, com flames, no estilo de arma de Valorant.

    /Applications/Blender.app/Contents/MacOS/Blender -b -P tools/armas/blender/ar15_ultimate.py

Parte do ``assets/armas/fp/ar15.glb`` (o fuzil e o carregador do Jhon, pintados
por paleta) e grava:

- ``assets/armas/fp/ar15_ultimate.glb``: o mesmo esqueleto, os mesmos braços e as
  mesmas animações do ar15.glb. Só mudam a malha do fuzil e a do carregador (o UV
  novo, num atlas só) e o material delas (cor e emissão);
- ``assets/armas/ar15_ultimate/``: a cor (``T_AR15_Ultimate_D.png``), a emissão
  (``T_AR15_Ultimate_E.png``) e os .obj do fuzil, do ferrolho e do carregador
  com o mesmo UV (a arma na mão dos outros e no chão: ``WeaponModel``).

O ar15.glb não é tocado. Por que não dá para só trocar a textura: a paleta pinta
cada peça inteira com um quadrado de cor, então desenho nenhum aparece nela. Aqui
o fuzil ganha um UV de verdade (Smart UV) e a textura é pintada direto sobre a
malha, a partir da cor que cada face tinha na paleta:

- base: roxo e índigo por peça (o que era bege vira roxo; o preto vira índigo
  escuro; as peças pequenas de metal claro viram dourado);
- flames de lado, nos dois lados da arma, projetadas de perfil (comprimento e
  altura do fuzil), três camadas e um contorno escuro;
- brilho nas quinas e sombra nos cantos fundos (a aresta vem da malha);
- padrão hexagonal discreto;
- a emissão (as flames e as quinas) vai numa textura própria.

O desenho das flames está em ``FLAMES`` (posição y/z em centímetros, no espaço do
fuzil: cano para −y, z para cima). Mexeu, rode de novo.

    --tamanho 1024    teste rápido (padrão 2048)
    --raiz <pasta>    raiz do projeto (padrão: três níveis acima deste arquivo)
"""
import json
import math
import os
import struct
import sys
import zlib

import bpy
import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def opcao(nome, padrao):
    return ARGS[ARGS.index(nome) + 1] if nome in ARGS else padrao


RAIZ = os.path.abspath(opcao("--raiz", os.path.dirname(os.path.dirname(os.path.dirname(AQUI)))))
TAMANHO = int(opcao("--tamanho", 2048))
GLB = os.path.join(RAIZ, "assets", "armas", "fp", "ar15.glb")
PALETA = os.path.join(RAIZ, "assets", "armas", "ar15", "T_AssaultRifle_D.png")
PASTA_OBJ = os.path.join(RAIZ, "assets", "armas", "ar15")
SAIDA_GLB = os.path.join(RAIZ, "assets", "armas", "fp", "ar15_ultimate.glb")
SAIDA_TEX = os.path.join(RAIZ, "assets", "armas", "ar15_ultimate")

PECAS = ("fuzil", "carregador")
MATERIAL = "M_AssaultRifle"
# Que índice cada peça leva na pintura (0 fuzil, 2 carregador: o das cápsulas é 1).
CLASSE_PECA = {"fuzil": 0, "carregador": 2}

# Do espaço do .glb (metros, como o Blender o importa) para o espaço do fuzil
# (centímetros, cano para −y, z para cima). Os triângulos do ar15.glb são os do
# SK_AssaultRifle do Jhon, na mesma ordem: esta é a transformação entre os dois.
R_FUZIL = np.array([[-0.457765, -0.622477, 0.634802],
                    [0.645508, 0.258284, 0.718755],
                    [-0.611367, 0.738791, 0.283581]])
T_FUZIL = np.array([68.1164, 7.0449, 34.5694])
# Caixa do fuzil nesse espaço, para conferir que o .glb é o esperado (cm).
CAIXA_ESPERADA = (np.array([-2.41, -49.13, -11.48]), np.array([2.93, 27.04, 10.29]))

# (y, z da raiz, altura, meia-largura, inclinação, fase). A raiz é a base da flame: no
# fundo da peça. Cada uma que não é do silenciador ganha duas vizinhas menores.
FLAMES = [
    (-45.0, 1.15, 3.9, 1.4, 1.5, 0.2), (-39.5, 1.15, 3.3, 1.25, 1.2, 1.4), (-34.0, 1.15, 3.7, 1.35, 1.4, 2.3),    # silenciador
    (-27.0, -0.6, 6.3, 2.1, 2.5, 0.6), (-20.5, -0.6, 5.4, 1.8, 2.0, 1.9), (-14.0, -0.6, 6.2, 2.0, 2.4, 0.9),      # guarda-mão
    (-5.5, -4.3, 12.4, 3.9, 5.6, 0.9), (3.0, -4.3, 10.0, 3.2, 4.4, 2.1), (11.0, -4.3, 8.6, 2.8, 3.6, 1.6),        # caixa
]
COM_VIZINHAS = range(3, len(FLAMES))


def falhar(texto):
    print(f"ULTIMATE ERRO: {texto}")
    sys.exit(1)


# ------------------------------------------------------------------ glb

def ler_glb(caminho):
    raw = open(caminho, "rb").read()
    tam = struct.unpack("<I", raw[12:16])[0]
    doc = json.loads(raw[20:20 + tam])
    btam = struct.unpack("<I", raw[20 + tam:24 + tam])[0]
    return doc, raw[28 + tam:28 + tam + btam]


COMPONENTES = {5120: "b", 5121: "B", 5122: "h", 5123: "H", 5125: "I", 5126: "f"}
LARGURA = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def bytes_por_vertice(doc, binario, indice):
    a = doc["accessors"][indice]
    bv = doc["bufferViews"][a["bufferView"]]
    elem = struct.calcsize("<" + COMPONENTES[a["componentType"]]) * LARGURA[a["type"]]
    passo = bv.get("byteStride", elem)
    ini = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    return [binario[ini + i * passo: ini + i * passo + elem] for i in range(a["count"])]


def valores(doc, binario, indice):
    a = doc["accessors"][indice]
    fmt = "<" + COMPONENTES[a["componentType"]] * LARGURA[a["type"]]
    return [struct.unpack(fmt, b) for b in bytes_por_vertice(doc, binario, indice)]


def malha_do_no(doc, nome):
    no = next((x for x in doc["nodes"] if x.get("name") == nome and "mesh" in x), None)
    if no is None:
        falhar(f"o .glb não tem o nó '{nome}'")
    return doc["meshes"][no["mesh"]]


# ------------------------------------------------------------------ Blender

def importar():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=GLB, merge_vertices=False)
    objs = {n: bpy.data.objects.get(n) for n in PECAS}
    for n, o in objs.items():
        if o is None or o.type != "MESH":
            falhar(f"não achei a malha '{n}' no {GLB}")
    for o in bpy.data.objects:
        if o.type == "ARMATURE":
            o.data.pose_position = "REST"
    bpy.context.view_layer.update()
    return objs


def conferir_vertices(doc, binario, objs):
    """O Blender leu a malha 1 para 1 (sem juntar vértices)? É o que deixa
    trocar o UV no .glb sem mexer no resto do vértice."""
    for n, o in objs.items():
        prim = malha_do_no(doc, n)["primitives"][0]
        pos = valores(doc, binario, prim["attributes"]["POSITION"])
        if len(pos) != len(o.data.vertices):
            falhar(f"{n}: {len(o.data.vertices)} vértices no Blender, {len(pos)} no .glb")
        mundo = o.matrix_world
        for v, p in zip(o.data.vertices, pos):
            w = mundo @ v.co
            if abs(w.x - p[0]) > 1e-3 or abs(w.y + p[2]) > 1e-3 or abs(w.z - p[1]) > 1e-3:
                falhar(f"{n}: o vértice {v.index} não bate com o do .glb")


def abrir_uv(objs):
    for o in bpy.data.objects:
        o.select_set(False)
    for o in objs.values():
        velho = o.data.uv_layers[0]
        velho.name = "paleta"
        novo = o.data.uv_layers.new(name="detalhe")
        o.data.uv_layers.active = novo
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs["fuzil"]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.003,
                             area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
    bpy.ops.object.mode_set(mode="OBJECT")


def ler_paleta():
    img = bpy.data.images.load(PALETA)
    img.colorspace_settings.name = "Non-Color"      # os valores como estão no arquivo
    larg, alt = img.size
    px = np.empty(larg * alt * 4, np.float32)
    img.pixels.foreach_get(px)
    return px.reshape(alt, larg, 4)[::-1, :, :3] * 255.0     # o Blender guarda de baixo para cima


# ------------------------------------------------------------------ triângulos

def triangulos(objs, paleta):
    """Tudo o que a pintura precisa, por triângulo: UV novo, posição no espaço
    do fuzil, aresta viva de cada lado, normal e a cor que a face tinha."""
    alt, larg = paleta.shape[:2]
    uv_, pos_, edge_, nrm_, col_, obj_ = [], [], [], [], [], []
    ordem = {}
    for nome, o in objs.items():
        m = o.data
        m.calc_loop_triangles()
        mw = np.array(o.matrix_world)
        m3 = mw[:3, :3]
        co = np.array([(mw @ np.array([*v.co, 1.0]))[:3] for v in m.vertices])
        pos = 100.0 * (co @ R_FUZIL.T) + T_FUZIL
        uvn = m.uv_layers["detalhe"].data
        uvp = m.uv_layers["paleta"].data
        nrm = np.array([m3 @ np.array(p.normal) for p in m.polygons])
        nrm /= np.linalg.norm(nrm, axis=1, keepdims=True)
        nrm = nrm @ R_FUZIL.T
        chave = {tuple(p): i for i, p in enumerate(map(tuple, np.round(pos, 3)))}
        solda = np.array([chave[tuple(p)] for p in map(tuple, np.round(pos, 3))])   # vértices iguais viram um

        arestas = {}
        for t in m.loop_triangles:
            vs = [solda[v] for v in t.vertices]
            for i in range(3):
                a, b = vs[(i + 1) % 3], vs[(i + 2) % 3]
                arestas.setdefault((min(a, b), max(a, b)), []).append(t.index)
        centro = np.array([pos[list(t.vertices)].mean(0) for t in m.loop_triangles])
        classe = {}
        for k, ts in arestas.items():
            if len(ts) == 2:
                na, nb = nrm[m.loop_triangles[ts[0]].polygon_index], nrm[m.loop_triangles[ts[1]].polygon_index]
                ang = math.degrees(math.acos(max(-1.0, min(1.0, float(na @ nb)))))
                if ang > 28:
                    convexa = float(na @ (centro[ts[1]] - centro[ts[0]])) < 0
                    classe[k] = 1 if convexa else 2
            elif len(ts) == 1:
                classe[k] = 1

        for t in m.loop_triangles:
            vs = list(t.vertices)
            uv_.append([uvn[l].uv[:] for l in t.loops])
            pos_.append(pos[vs])
            e = []
            for i in range(3):
                a, b = solda[vs[(i + 1) % 3]], solda[vs[(i + 2) % 3]]
                e.append(classe.get((min(a, b), max(a, b)), 0))
            edge_.append(e)
            nrm_.append(nrm[t.polygon_index])
            c = np.mean([uvp[l].uv[:] for l in t.loops], axis=0)
            col_.append(paleta[min(alt - 1, max(0, int((1 - c[1]) * alt))), min(larg - 1, max(0, int(c[0] * larg)))])
            obj_.append(CLASSE_PECA[nome])
        ordem[nome] = len(m.loop_triangles)
    return (np.array(uv_, np.float32), np.array(pos_, np.float32), np.array(edge_, np.int8),
            np.array(nrm_, np.float32), np.array(col_, np.float32), np.array(obj_, np.int8))


def conferir_espaco(pos, obj):
    p = pos[obj == 0].reshape(-1, 3)
    lo, hi = CAIXA_ESPERADA
    if np.abs(p.min(0) - lo).max() > 0.3 or np.abs(p.max(0) - hi).max() > 0.3:
        falhar(f"o fuzil do .glb não está onde a skin espera (caixa {p.min(0).round(2)} .. {p.max(0).round(2)}, "
               f"esperada {lo} .. {hi}). O ar15.glb mudou? Ajuste R_FUZIL/T_FUZIL.")


# ------------------------------------------------------------------ pintura

def hexc(h):
    return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], np.float32) / 255.0


def mix(a, b, t):
    t = np.clip(t, 0, 1)[:, None]
    return a * (1 - t) + b * t


def ss(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def rasterizar(UV, POS, EDGE, NRM, COL, OBJ, N):
    P = np.zeros((N, N, 3), np.float32)
    NR = np.zeros((N, N, 3), np.float32)
    CO = np.zeros((N, N, 3), np.float32)
    DX = np.full((N, N), 9.0, np.float32)
    DV = np.full((N, N), 9.0, np.float32)
    OB = np.zeros((N, N), np.int8)
    FILL = np.zeros((N, N), bool)
    for t in range(len(UV)):
        uv = UV[t].astype(np.float64)
        px = np.stack([uv[:, 0] * N, (1 - uv[:, 1]) * N], 1)
        x0, y0 = np.floor(px.min(0)).astype(int)
        x1, y1 = np.ceil(px.max(0)).astype(int)
        x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, N - 1), min(y1, N - 1)
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        a, b, c = px
        den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(den) < 1e-9:
            continue

        def bary(xs, ys):
            l0 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / den
            l1 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / den
            return l0, l1, 1 - l0 - l1

        l0, l1, l2 = bary(xs, ys)
        m = (l0 >= -0.02) & (l1 >= -0.02) & (l2 >= -0.02)
        if not m.any():                       # triângulo menor que um pixel: o pixel do centro
            cx, cy = px.mean(0)
            xi, yi = int(cx), int(cy)
            if not (0 <= xi < N and 0 <= yi < N):
                continue
            l0, l1, l2 = bary(np.array([[xi + .5]]), np.array([[yi + .5]]))
            m = np.ones((1, 1), bool)
            x0, y0 = xi, yi
        L = np.stack([l0[m], l1[m], l2[m]], 1).clip(0, 1)
        rr, cc = np.nonzero(m)
        rr, cc = rr + y0, cc + x0
        pw = POS[t].astype(np.float64)
        P[rr, cc] = L @ pw
        NR[rr, cc] = NRM[t]
        CO[rr, cc] = COL[t]
        OB[rr, cc] = OBJ[t]
        FILL[rr, cc] = True
        area2 = np.linalg.norm(np.cross(pw[1] - pw[0], pw[2] - pw[0]))
        for i in range(3):
            cls = EDGE[t][i]
            if cls == 0:
                continue
            e = np.linalg.norm(pw[(i + 2) % 3] - pw[(i + 1) % 3])
            dist = L[:, i] * area2 / max(e, 1e-9)
            alvo = DX if cls == 1 else DV
            alvo[rr, cc] = np.minimum(alvo[rr, cc], dist)
    return P, NR, CO, DX, DV, OB, FILL


def flame(Y, Z, y0, z0, H, W, fase, grow=0.0, eps=0.06):
    """Língua de fogo esguia em S, nascendo em (y0, z0); a ponta varre para +y."""
    v = (Z - z0) / H
    vc = np.clip(v, 0, 1)
    lean = 0.30 * H
    cen = y0 + lean * vc ** 2.3 - 0.07 * H * np.sin(np.pi * vc * 1.1)
    hw = W * (1 - vc) ** 0.95 * (0.30 + 0.70 * (1 - np.exp(-vc * 9.0))) * (1 + 0.08 * np.sin(6.0 * vc + fase)) + grow
    dentro = np.clip((hw - np.abs(Y - cen)) / eps + 0.5, 0, 1)
    vert = np.clip((v * H + grow) / eps + 0.5, 0, 1) * np.clip(((1 - v) * H + grow * 0.5) / eps + 0.5, 0, 1)
    return dentro * vert, vc


def padrao_hex(u, v, s):
    """Distância ao centro da célula hexagonal mais próxima (duas grades)."""
    h = s * 0.8660254

    def dist(du, dv):
        a = (u / s + du) % 1.0 - 0.5
        b = (v / (2 * h) + dv) % 1.0 - 0.5
        return np.hypot(a * s, b * 2 * h)

    return np.minimum(dist(0.0, 0.0), dist(0.5, 0.5)) / s


def pintar(UV, POS, EDGE, NRM, COL, OBJ, N):
    P, NR, CO, DX, DV, OB, FILL = rasterizar(UV, POS, EDGE, NRM, COL, OBJ, N)
    idx = np.nonzero(FILL)
    y, z, x = P[idx][:, 1], P[idx][:, 2], P[idx][:, 0]
    nx, nz = NR[idx][:, 0], NR[idx][:, 2]
    col = CO[idx] / 255.0
    ob, dx, dv = OB[idx], DX[idx], DV[idx]
    n = len(y)

    mx, mn = col.max(1), col.min(1)
    sat = (mx - mn) / np.maximum(mx, 1e-6)
    corpo = (ob == 0) & (sat >= 0.2)                     # o que era bege
    metal = (ob == 0) & ~corpo & (mx > 0.285)            # peças pequenas de metal claro
    latao = (ob == 2) & (sat >= 0.2)
    escuro = ~(corpo | metal | latao)

    rgb = np.zeros((n, 3), np.float32)
    tz = ss(-5, 9, z)
    rgb[corpo] = mix(hexc('#1b0542'), hexc('#4b1fa6'), tz[corpo] * 0.85 + 0.1 * ss(-50, 25, y[corpo]))
    rgb[escuro] = mix(hexc('#0a0618'), hexc('#221247'), 0.25 * tz[escuro] + 0.33 * ss(-49, -40, -y[escuro]))
    rgb[metal] = mix(hexc('#8a5a12'), hexc('#fde68a'), tz[metal])
    rgb[latao] = mix(hexc('#a21caf'), hexc('#f0abfc'), tz[latao])
    rgb *= (0.92 + 0.14 * np.clip(nz, -1, 1))[:, None]

    lado = ss(0.35, 0.7, np.abs(nx))
    hexlinha = ss(0.40, 0.48, padrao_hex(y, z, 1.5))
    a = (0.14 * hexlinha * lado) * (escuro | metal)
    rgb = rgb * (1 - a[:, None]) + hexc('#7c3aed') * a[:, None]

    # flames
    portao = ss(0.30, 0.62, np.abs(nx))
    f_out, f_mid, f_in, f_edge, f_halo, fa = (np.zeros(n, np.float32) for _ in range(6))
    lista = []
    for i, (y0, z0, H, W, lean, ph) in enumerate(FLAMES):
        if i in COM_VIZINHAS:
            lista.append((y0 - 1.15 * W, z0, H * 0.50, W * 0.55, lean * 0.5, ph + 1))
            lista.append((y0 + 1.2 * W, z0, H * 0.6, W * 0.6, lean * 0.6, ph + 2))
        lista.append((y0, z0, H, W, lean, ph))
    for (y0, z0, H, W, lean, ph) in lista:
        mo, a_ = flame(y, z, y0, z0, H, W, ph)
        me, _ = flame(y, z, y0, z0, H, W, ph, grow=0.26)
        mh, _ = flame(y, z, y0, z0, H, W, ph, grow=0.7, eps=0.6)
        mm, _ = flame(y, z, y0 + 0.10 * lean, z0 + 0.02 * H, H * 0.72, W * 0.62, ph + 0.5)
        mi, _ = flame(y, z, y0 + 0.18 * lean, z0 + 0.04 * H, H * 0.45, W * 0.34, ph + 1.0)
        fa = np.where(mo > 0.5, a_, fa)
        k = mo > 0.02
        f_out = np.where(k, np.maximum(f_out * (1 - mo), mo), f_out)
        f_mid = np.where(k, mm, f_mid)
        f_in = np.where(k, mi, f_in)
        f_edge = np.maximum(f_edge, me)
        f_halo = np.maximum(f_halo, mh)
    f_out, f_edge, f_mid, f_in, f_halo = (f * portao for f in (f_out, f_edge, f_mid, f_in, f_halo))

    brilho = hexc('#c026d3')
    rgb = rgb * (1 - 0.35 * f_halo[:, None]) + brilho * 0.35 * f_halo[:, None]
    k = np.clip(f_edge - f_out, 0, 1) * 0.95
    rgb = rgb * (1 - k[:, None]) + hexc('#0a0414') * k[:, None]
    cor_out = mix(np.tile(hexc('#7a1fe0'), (n, 1)), hexc('#f03cff'), ss(0.0, 0.9, fa))
    cor_mid = mix(np.tile(hexc('#ff5ff0'), (n, 1)), hexc('#ffd6fb'), ss(0.0, 1.0, fa))
    cor_in = mix(np.tile(hexc('#f5d0fe'), (n, 1)), hexc('#ffffff'), ss(0.0, 1.0, fa))
    for f, c in ((f_out, cor_out), (f_mid, cor_mid), (f_in, cor_in)):
        rgb = rgb * (1 - f[:, None]) + c * f[:, None]

    # quinas e cantos
    rim = 1 - ss(0.05, 0.17, dx)
    cor_rim = np.where(escuro[:, None], hexc('#8b5cf6'), hexc('#e9d5ff'))
    ra = rim * np.where(escuro, 0.55, 0.8) * (1 - 0.85 * f_out)
    rgb = rgb * (1 - ra[:, None]) + cor_rim * ra[:, None]
    cav = (1 - ss(0.04, 0.22, dv)) * 0.55
    rgb = rgb * (1 - cav[:, None]) + hexc('#05010d') * cav[:, None]

    # emissão: só o que brilha
    em = brilho * 0.10 * f_halo[:, None]
    for f, c, forca in ((f_out, cor_out, 0.85), (f_mid, cor_mid, 1.0), (f_in, cor_in, 1.0)):
        em = em * (1 - f[:, None]) + c * forca * f[:, None]
    em = (em + cor_rim * (ra * 0.30 * (1 - f_out))[:, None]) * 0.65

    cor_img = np.zeros((N, N, 3), np.float32)
    em_img = np.zeros((N, N, 3), np.float32)
    cor_img[idx] = np.clip(rgb, 0, 1)
    em_img[idx] = np.clip(em, 0, 1)
    return sangrar(cor_img, FILL, hexc('#12081f'), N), sangrar(em_img, FILL, np.zeros(3, np.float32), N)


def sangrar(img, fill, fundo, N, passos=10):
    """Estende as cores das bordas das ilhas para fora (sem listra preta no filtro)."""
    img = img.copy()
    fill = fill.copy()
    for _ in range(passos):
        acc = np.zeros_like(img)
        cnt = np.zeros((N, N), np.float32)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy == 0 and dx == 0:
                    continue
                f = np.roll(np.roll(fill, dy, 0), dx, 1)
                acc += np.roll(np.roll(img, dy, 0), dx, 1) * f[..., None]
                cnt += f
        novo = (~fill) & (cnt > 0)
        img[novo] = acc[novo] / cnt[novo][:, None]
        fill |= novo
    img[~fill] = fundo
    return img


def png_bytes(img):
    """PNG RGB de 8 bits, sem depender de nada (img float 0..1, linhas de cima para baixo)."""
    a = (np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)
    h, w = a.shape[:2]
    cru = b"".join(b"\x00" + a[i].tobytes() for i in range(h))

    def bloco(tipo, dados):
        c = struct.pack(">I", len(dados)) + tipo + dados
        return c + struct.pack(">I", zlib.crc32(tipo + dados) & 0xffffffff)

    return (b"\x89PNG\r\n\x1a\n" + bloco(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
            + bloco(b"IDAT", zlib.compress(cru, 9)) + bloco(b"IEND", b""))


# ------------------------------------------------------------------ regravar o .glb

def nova_primitiva(doc, binario, prim, o):
    """Os triângulos do Blender com o UV novo: um vértice por par (vértice
    do .glb, UV). O resto do vértice (posição, normal, ossos) é copiado."""
    attrs = {k: bytes_por_vertice(doc, binario, v) for k, v in prim["attributes"].items()
             if k not in ("TEXCOORD_0", "TEXCOORD_1", "COLOR_0", "COLOR_1", "TANGENT")}
    uv = o.data.uv_layers["detalhe"].data
    chave, ordem, indices = {}, [], []
    for poli in o.data.polygons:
        if len(poli.loop_indices) != 3:
            falhar(f"{o.name}: face com {len(poli.loop_indices)} lados (o .glb só tem triângulos)")
        for li in poli.loop_indices:
            v = o.data.loops[li].vertex_index
            u, w = uv[li].uv
            k = (v, round(u, 6), round(w, 6))
            if k not in chave:
                chave[k] = len(ordem)
                ordem.append((v, u, 1.0 - w))
            indices.append(chave[k])
    saida = {nome: b"".join(lista[v] for v, _, _ in ordem) for nome, lista in attrs.items()}
    saida["TEXCOORD_0"] = b"".join(struct.pack("<2f", u, w) for _, u, w in ordem)
    return saida, indices, len(ordem) > 65535, len(ordem)


def regravar(doc, binario, objs, imagens):
    """Troca só a malha do fuzil e a do carregador e o material delas. O resto
    do arquivo (ossos, animações, braços e suas texturas) é copiado."""
    blocos, novos_views = [], []

    def anexar(dados, alvo=None):
        novos_views.append({"buffer": 0, "byteLength": len(dados), **({"target": alvo} if alvo else {})})
        blocos.append(dados)
        return len(novos_views) - 1

    descartar = ("TEXCOORD_0", "TEXCOORD_1", "COLOR_0", "COLOR_1", "TANGENT")
    usados = set()
    for m in doc["meshes"]:
        for p in m["primitives"]:
            usados.update(p["attributes"].values())
            if "indices" in p:
                usados.add(p["indices"])
    for s in doc.get("skins", []):
        if "inverseBindMatrices" in s:
            usados.add(s["inverseBindMatrices"])
    for a in doc.get("animations", []):
        for s in a["samplers"]:
            usados.update((s["input"], s["output"]))

    novas = {}
    for n, o in objs.items():
        prim = malha_do_no(doc, n)["primitives"][0]
        novas[id(prim)] = nova_primitiva(doc, binario, prim, o)
        for k, v in prim["attributes"].items():
            usados.discard(v)
        usados.discard(prim.get("indices"))

    remap, acessores = {}, []
    for i in sorted(u for u in usados if u is not None):
        a = dict(doc["accessors"][i])
        a.pop("byteOffset", None)
        a["bufferView"] = anexar(b"".join(bytes_por_vertice(doc, binario, i)), doc["bufferViews"][a["bufferView"]].get("target"))
        remap[i] = len(acessores)
        acessores.append(a)

    def novo_acessor(dados, comp, tipo, count, alvo, extra=None):
        acessores.append({"bufferView": anexar(dados, alvo), "componentType": comp, "type": tipo,
                          "count": count, **(extra or {})})
        return len(acessores) - 1

    for m in doc["meshes"]:
        for p in m["primitives"]:
            if id(p) in novas:
                saida, indices, grande, count = novas[id(p)]
                velhos, novos = p["attributes"], {}
                for nome, dados in saida.items():
                    base = doc["accessors"][velhos[nome]] if nome in velhos else None
                    if nome == "TEXCOORD_0":
                        novos[nome] = novo_acessor(dados, 5126, "VEC2", count, 34962)
                    else:
                        extra = {k: base[k] for k in ("normalized", "min", "max") if k in base}
                        novos[nome] = novo_acessor(dados, base["componentType"], base["type"], count, 34962, extra)
                fmt = "<%d%s" % (len(indices), "I" if grande else "H")
                p["indices"] = novo_acessor(struct.pack(fmt, *indices), 5125 if grande else 5123, "SCALAR", len(indices), 34963)
                p["attributes"] = novos
            else:
                p["attributes"] = {k: remap[v] for k, v in p["attributes"].items()}
                if "indices" in p:
                    p["indices"] = remap[p["indices"]]
    for s in doc.get("skins", []):
        if "inverseBindMatrices" in s:
            s["inverseBindMatrices"] = remap[s["inverseBindMatrices"]]
    for a in doc.get("animations", []):
        for s in a["samplers"]:
            s["input"], s["output"] = remap[s["input"]], remap[s["output"]]

    # As imagens que já estavam (braços) seguem; as duas novas entram no fim.
    antigas = [(im, bytes_por_vertice_view(doc, binario, im)) for im in doc.get("images", [])]
    doc["images"] = []
    doc["samplers"] = [{"magFilter": 9729, "minFilter": 9987, "wrapS": 10497, "wrapT": 10497}]
    for im, dados in antigas:
        novo = {k: v for k, v in im.items() if k != "bufferView"}
        novo["bufferView"] = anexar(dados)
        doc["images"].append(novo)
    tex = {}
    for nome, dados in imagens.items():
        doc["images"].append({"name": f"ar15_ultimate_{nome}", "mimeType": "image/png", "bufferView": anexar(dados)})
        doc["textures"].append({"sampler": 0, "source": len(doc["images"]) - 1})
        tex[nome] = len(doc["textures"]) - 1
    for m in doc["materials"]:
        if m["name"].startswith(MATERIAL):
            m["pbrMetallicRoughness"] = {"baseColorFactor": [1, 1, 1, 1], "baseColorTexture": {"index": tex["cor"]},
                                         "metallicFactor": 0.35, "roughnessFactor": 0.38}
            m["emissiveTexture"] = {"index": tex["emissiva"]}
            m["emissiveFactor"] = [1.0, 1.0, 1.0]

    corpo = bytearray()
    for view, dados in zip(novos_views, blocos):
        while len(corpo) % 4:
            corpo.append(0)
        view["byteOffset"] = len(corpo)
        corpo += dados
    while len(corpo) % 4:
        corpo.append(0)
    doc["bufferViews"], doc["accessors"], doc["buffers"] = novos_views, acessores, [{"byteLength": len(corpo)}]
    js = json.dumps(doc, separators=(",", ":")).encode()
    while len(js) % 4:
        js += b" "
    total = 12 + 8 + len(js) + 8 + len(corpo)
    return (struct.pack("<4sII", b"glTF", 2, total) + struct.pack("<I4s", len(js), b"JSON") + js
            + struct.pack("<I4s", len(corpo), b"BIN\0") + bytes(corpo))


def bytes_por_vertice_view(doc, binario, imagem):
    bv = doc["bufferViews"][imagem["bufferView"]]
    ini = bv.get("byteOffset", 0)
    return binario[ini:ini + bv["byteLength"]]


# ------------------------------------------------------------------ .obj (mão dos outros e chão)

def ler_obj(caminho):
    linhas = open(caminho, encoding="utf-8").read().splitlines()
    v = np.array([[float(t) for t in l.split()[1:4]] for l in linhas if l.startswith("v ")])
    return linhas, v


def uv_do_obj(caminho, pos_tri, uv_tri):
    """O .obj tem um vértice por canto de face (v, vt e f em sequência). Para cada
    face acha o triângulo do .glb que ocupa o mesmo lugar e copia o UV, canto a
    canto. Devolve as linhas do arquivo com os ``vt`` trocados."""
    linhas, v = ler_obj(caminho)
    # O .obj está no espaço da arma do jogo: metros, cano para −z, y para cima.
    cantos = v.reshape(-1, 3, 3)
    no_fuzil = np.stack([-cantos[..., 0], cantos[..., 2], cantos[..., 1]], -1) * 100.0
    centro_obj = no_fuzil.mean(1)
    centro_tri = pos_tri.mean(1)
    novo_vt = np.zeros((len(cantos), 3, 2), np.float32)
    pior = 0.0
    for ini in range(0, len(cantos), 400):
        d = np.linalg.norm(centro_obj[ini:ini + 400, None, :] - centro_tri[None, :, :], axis=2)
        achou = d.argmin(1)
        pior = max(pior, float(d.min(1).max()))
        for k, t in enumerate(achou):
            for c in range(3):
                dc = np.linalg.norm(pos_tri[t] - no_fuzil[ini + k, c], axis=1)
                novo_vt[ini + k, c] = uv_tri[t, dc.argmin()]
    if pior > 0.02:
        falhar(f"{os.path.basename(caminho)}: uma face não achou o triângulo ({pior:.3f} cm)")
    it = iter(novo_vt.reshape(-1, 2))
    saida = []
    for l in linhas:
        if l.startswith("vt "):
            u, w = next(it)
            saida.append(f"vt {u:.6f} {w:.6f}")
        else:
            saida.append(l)
    return "\n".join(saida) + "\n"


# ------------------------------------------------------------------ principal

def main():
    if not os.path.exists(GLB):
        falhar(f"falta {GLB}")
    doc, binario = ler_glb(GLB)
    for m in doc["materials"]:
        if m["name"].startswith(MATERIAL) and "emissiveTexture" in m:
            falhar("o ar15.glb já tem skin: rode o infima.py antes (ele grava o fuzil com a paleta).")
    objs = importar()
    conferir_vertices(doc, binario, objs)
    paleta = ler_paleta()
    abrir_uv(objs)
    UV, POS, EDGE, NRM, COL, OBJ = triangulos(objs, paleta)
    conferir_espaco(POS, OBJ)
    print(f"ULTIMATE {len(UV)} triângulos; pintando {TAMANHO} px…", flush=True)
    cor, emissao = pintar(UV, POS, EDGE, NRM, COL, OBJ, TAMANHO)
    png_cor, png_em = png_bytes(cor), png_bytes(emissao)

    os.makedirs(SAIDA_TEX, exist_ok=True)
    open(os.path.join(SAIDA_TEX, "T_AR15_Ultimate_D.png"), "wb").write(png_cor)
    open(os.path.join(SAIDA_TEX, "T_AR15_Ultimate_E.png"), "wb").write(png_em)

    novo = regravar(doc, binario, objs, {"cor": png_cor, "emissiva": png_em})
    with open(SAIDA_GLB, "wb") as fh:
        fh.write(novo)
    print(f"ULTIMATE {SAIDA_GLB} ({len(novo) / 1e6:.1f} MB)")

    # O UV por triângulo, na ordem do fuzil e do carregador, para os .obj.
    nf = len(objs["fuzil"].data.loop_triangles)
    partes = {"corpo_ar15": (0, nf), "ferrolho_ar15": (0, nf), "magazine_ar15": (nf, len(UV))}
    for nome, (a, b) in partes.items():
        caminho = os.path.join(PASTA_OBJ, nome + ".obj")
        texto = uv_do_obj(caminho, POS[a:b], UV[a:b])
        destino = os.path.join(SAIDA_TEX, nome.replace("_ar15", "_ar15_ultimate") + ".obj")
        open(destino, "w", encoding="utf-8").write(texto)
        print(f"ULTIMATE {destino}")


if __name__ == "__main__":
    main()
