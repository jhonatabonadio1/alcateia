"""
Armazém — converte o mapa pronto da Unity (pacote "Low Poly FPS Map 4")
para o Alcateia.

    pip install bpy numpy scipy
    python3 tools/mapas/blender/armazem.py <cena.fbx> <pacote.unitypackage>

O FBX exportado da cena da Unity chega SEM coordenada de textura (só
posição, normal e material): tudo ficaria cinza. As peças originais, com
a UV certa, estão no .unitypackage (Models/*.fbx). Este script casa cada
peça da cena com o modelo de origem (mesmo número de triângulos; o
encaixe é afim — posição, giro, escala e espelho — achado por ICP) e
copia a UV de lá, triângulo a triângulo.

Grava:
- assets/mapas/pecas/armazem.glb — o desenho, em seis pedaços (para o
  Godot recortar o que está fora da tela) e a casca do galpão (paredes,
  telhado, sacos de areia) como colisão de malha (`-colonly`);
- tools/mapas/armazem.json — a colisão do resto (uma caixa orientada
  por peça; o contêiner é um túnel de quatro caixas, porque as duas
  pontas são abertas e as portas são peças à parte), as luzes do teto,
  onde ficam as escadas de marinheiro das paredes (pé, direção, altura)
  e os vidros (que entram na cena à parte, sem sombra).

Eixos: o galpão comprido fica em X (o eixo dos times), Y para cima, o
chão em 0. Medidas em metros.
"""
import glob
import json
import os
import re
import shutil
import sys
import tarfile
import tempfile

import bpy  # antes do bmesh: é o bpy que o carrega
import bmesh
import numpy as np
from scipy.spatial import cKDTree

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
GLB = os.path.join(RAIZ, "assets", "mapas", "pecas", "armazem.glb")
JSON = os.path.join(RAIZ, "tools", "mapas", "armazem.json")

# Das coordenadas da malha (cm, Y para cima) para o jogo: metros, e o
# comprido do galpão (Z da Unity) passa a ser X.
GIRO = np.array([[0, 0, 1], [0, 1, 0], [-1, 0, 0]], float)

# O que vira colisão de malha exata, dentro do .glb.
CASCA = ("Wall_", "WallPart_", "WallDecor_01", "WallDecor_02", "WallDecor_03", "WallDecor_04",
         "WallDecor_05", "WallDecor_07", "WallDecor_08", "Roof_", "Sand_")
# O que não colide: luminárias, tampas, alvos finos, extintor pequeno, piso
# (o piso é uma caixa só, na cena).
SEM_COLISAO = ("Light_", "AmoBox_01_2", "ShootingTarget_02", "ShootingTarget_03",
               "FireExtinguisher_01_2", "Floor_")
CONTEINER = tuple(f"ShippingContainer_0{i}" for i in range(1, 7))   # as seis cores, duas medidas
PISO = {"ShippingContainer": "metal", "Barrel": "metal", "Rack": "metal", "FireExtinguisher": "metal",
        "Pallet": "madeira", "Box_": "madeira", "WoodenPlank": "madeira", "AmoBox": "madeira"}


# ------------------------------------------------------------------ leitura

def triangulos(o, com_uv=True):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    P = np.array([[l.vert.co[:] for l in f.loops] for f in bm.faces])
    U = None
    if com_uv and bm.loops.layers.uv:
        lay = bm.loops.layers.uv[0]
        U = np.array([[l[lay].uv[:] for l in f.loops] for f in bm.faces])
    bm.free()
    return P, U


def abrir_pacote(pacote, pasta):
    with tarfile.open(pacote, "r:gz") as tar:
        tar.extractall(pasta, filter="data")
    modelos, paleta, caminho_de, cenas = {}, None, {}, []
    for d in glob.glob(os.path.join(pasta, "*")):
        caminho = os.path.join(d, "pathname")
        if not os.path.exists(caminho) or not os.path.exists(os.path.join(d, "asset")):
            continue
        nome = open(caminho, encoding="utf-8").readline().strip()
        caminho_de[os.path.basename(d)] = (os.path.join(d, "asset"), nome)
        if nome.endswith(".unity"):
            cenas.append(os.path.basename(d))
        if nome.lower().endswith(".fbx") and "/Models/" in nome:
            destino = os.path.join(pasta, os.path.basename(nome))
            shutil.copy(os.path.join(d, "asset"), destino)
            modelos[os.path.basename(nome)[:-4]] = destino
        elif nome.endswith("Texture_FPSMap4_01.png"):
            paleta = os.path.join(pasta, "paleta.png")
            shutil.copy(os.path.join(d, "asset"), paleta)
    return modelos, paleta, caminho_de, cenas



# ------------------------------------------------------- cena da Unity

def _qm(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def _mat(pos, q, esc):
    M = np.eye(4)
    M[:3, :3] = _qm(q) * np.array(esc)
    M[:3, 3] = pos
    return M


def _vetor(txt, chave, n):
    m = re.search(chave + r": \{([^}]*)\}", txt)
    if not m:
        return None
    d = dict(kv.split(": ") for kv in m.group(1).split(", "))
    return [float(d[k]) for k in "xyzw"[:n]]


def _ler_yaml(arquivo, cache={}):
    """Transforms, PrefabInstances e MeshFilters de uma cena ou prefab da Unity."""
    if arquivo in cache:
        return cache[arquivo]
    T, PI, MF, GO2T = {}, {}, [], {}
    for d in re.split(r"\n--- ", open(arquivo, encoding="utf-8", errors="replace").read()):
        m = re.match(r"!u!(\d+) &(-?\d+)( stripped)?", d)
        if not m:
            continue
        tipo, fid, cortado = m.groups()
        if tipo == "4":
            pai = re.search(r"m_Father: \{fileID: (-?\d+)", d)
            inst = re.search(r"m_PrefabInstance: \{fileID: (-?\d+)", d)
            go = re.search(r"m_GameObject: \{fileID: (-?\d+)", d)
            T[fid] = dict(pos=_vetor(d, "m_LocalPosition", 3), rot=_vetor(d, "m_LocalRotation", 4),
                          esc=_vetor(d, "m_LocalScale", 3), pai=pai.group(1) if pai else "0",
                          inst=inst.group(1) if inst else "0", cortado=bool(cortado))
            if go:
                GO2T[go.group(1)] = fid
        elif tipo == "1001":
            fonte = re.search(r"m_SourcePrefab: \{fileID: -?\d+, guid: (\w+)", d).group(1)
            pai = re.search(r"m_TransformParent: \{fileID: (-?\d+)", d).group(1)
            mods = {}
            for mm in re.finditer(r"- target: \{fileID: (-?\d+), guid: \w+, type: 3\}\n\s+propertyPath: (m_Local\w+)\.(\w)\n\s+value: ([-\d.eE]+)", d):
                mods.setdefault(mm.group(1), {})[(mm.group(2), mm.group(3))] = float(mm.group(4))
            PI[fid] = dict(fonte=fonte, pai=pai, mods=mods)
        elif tipo == "33":
            go = re.search(r"m_GameObject: \{fileID: (-?\d+)", d)
            malha = re.search(r"m_Mesh: \{fileID: -?\d+, guid: (\w+)", d)
            if go and malha:
                MF.append((go.group(1), malha.group(1)))
    cache[arquivo] = (T, PI, MF, GO2T)
    return cache[arquivo]


def _local_da_instancia(p):
    for m in p["mods"].values():
        if ("m_LocalPosition", "x") in m:
            return _mat([m.get(("m_LocalPosition", a), 0) for a in "xyz"],
                        [m.get(("m_LocalRotation", a), 0 if a != "w" else 1) for a in "xyzw"],
                        [m.get(("m_LocalScale", a), 1) for a in "xyz"])
    return np.eye(4)


def _cadeia(dados, fid, e_cena, fundo=0):
    T, PI, _, _ = dados
    t = T.get(fid)
    if fid == "0" or t is None or fundo > 40:
        return np.eye(4)
    if t["cortado"]:
        p = PI[t["inst"]]
        return _cadeia(dados, p["pai"], e_cena, fundo + 1) @ _local_da_instancia(p)
    if t["pai"] == "0" and not e_cena:
        return np.eye(4)          # raiz do prefab: quem põe no lugar é a instância
    return _cadeia(dados, t["pai"], e_cena, fundo + 1) @ _mat(t["pos"], t["rot"], t["esc"])


def pecas_da_unity(cena, caminho_de):
    """Cada malha da cena da Unity (prefabs abertos até o fim): (modelo, posição do pivô)."""
    saida = []

    def abrir(guid, W, e_cena):
        dados = _ler_yaml(caminho_de[guid][0])
        T, PI, MF, GO2T = dados
        for p in PI.values():
            Wf = W @ _cadeia(dados, p["pai"], e_cena) @ _local_da_instancia(p)
            nome = caminho_de.get(p["fonte"], (None, ""))[1]
            if nome.lower().endswith(".fbx"):
                saida.append((os.path.basename(nome)[:-4], Wf[:3, 3]))
            elif nome.endswith(".prefab"):
                abrir(p["fonte"], Wf, False)
        for go, g in MF:
            if go in GO2T and caminho_de.get(g, (None, ""))[1].lower().endswith(".fbx"):
                Wf = W @ _cadeia(dados, GO2T[go], e_cena)
                saida.append((os.path.basename(caminho_de[g][1])[:-4], Wf[:3, 3]))

    abrir(cena, np.eye(4), True)
    return saida


# --------------------------------------------------------------- encaixe

def unicos(P):
    return np.unique(np.round(P.reshape(-1, 3), 3), axis=0)


def ajuste_afim(A, B):
    X = np.c_[A, np.ones(len(A))]
    M, *_ = np.linalg.lstsq(X, B, rcond=None)
    return M


def aplicar(M, A):
    return np.c_[A, np.ones(len(A))] @ M


def giro(eixo, a):
    c, s = np.cos(a), np.sin(a)
    if eixo == 1:
        return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
    if eixo == 0:
        return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def icp(A, B, tb, M):
    for _ in range(25):
        _, j = tb.query(aplicar(M, A))
        M = ajuste_afim(A, B[j])
    T = aplicar(M, A)
    return max(tb.query(T)[0].max(), cKDTree(T).query(B)[0].max()), M


def encaixar(P, candidatos):
    """Acha o modelo de origem e a transformação afim que o põe sobre a
    peça da cena. Devolve (erro em cm, nome, M)."""
    B = unicos(P)
    tb = cKDTree(B)
    melhor = (1e9, None, None)
    inicios = []
    for yaw in range(4):
        R = np.round(giro(1, yaw * np.pi / 2))
        inicios += [R, R @ np.diag([-1, 1, 1])]
    for nome, (S, _) in candidatos:
        A = unicos(S)
        # 1) giros retos, escala pela caixa
        for R in inicios:
            A1 = A @ R.T
            lo, hi = A1.min(0), A1.max(0)
            esc = np.where(hi - lo > 1e-3, (B.max(0) - B.min(0)) / np.maximum(hi - lo, 1e-9), 1.0)
            M = np.zeros((4, 3))
            M[:3] = R.T * esc
            M[3] = B.min(0) - lo * esc
            e, M = icp(A, B, tb, M)
            if e < melhor[0]:
                melhor = (e, nome, M)
            if e < 0.05:
                return melhor
    # 2) giro qualquer: varre o yaw (e as peças deitadas)
    for nome, (S, _) in candidatos:
        A = unicos(S)
        ca, cb = A.mean(0), B.mean(0)
        for deita in [np.eye(3), giro(0, np.pi / 2), giro(0, -np.pi / 2), giro(2, np.pi / 2), giro(2, -np.pi / 2), giro(0, np.pi)]:
            for espelho in (1, -1):
                for yaw in np.radians(np.arange(0, 360, 4)):
                    R = giro(1, yaw) @ np.diag([espelho, 1, 1]) @ deita
                    M = np.zeros((4, 3))
                    M[:3] = R.T
                    M[3] = cb - ca @ R.T
                    if np.median(tb.query(aplicar(M, A))[0]) > 15:
                        continue
                    e, M = icp(A, B, tb, M)
                    if e < melhor[0]:
                        melhor = (e, nome, M)
                    if e < 0.05:
                        return melhor
    return melhor


def baricentro(p, a, b, c):
    v0, v1, v2 = b - a, c - a, p - a
    d00, d01, d11 = (v0 * v0).sum(-1), (v0 * v1).sum(-1), (v1 * v1).sum(-1)
    d20, d21 = (v2 * v0).sum(-1), (v2 * v1).sum(-1)
    den = d00 * d11 - d01 * d01
    den = np.where(np.abs(den) < 1e-12, 1e-12, den)
    v = (d11 * d20 - d01 * d21) / den
    w = (d00 * d21 - d01 * d20) / den
    return np.stack([1 - v - w, v, w], -1)


def copiar_uv(P, S, U, M):
    """UV de cada canto da peça da cena, tirada do triângulo do modelo de
    origem (já posto no lugar) que está sob ela."""
    T = aplicar(M, S.reshape(-1, 3)).reshape(-1, 3, 3)
    nT = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
    nT /= np.linalg.norm(nT, axis=1, keepdims=True) + 1e-12
    nP = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0])
    nP /= np.linalg.norm(nP, axis=1, keepdims=True) + 1e-12
    k = min(12, len(T))
    _, ii = cKDTree(T.mean(1)).query(P.mean(1), k=k)
    ii = ii.reshape(len(P), -1)
    cP = P.mean(1)[:, None]
    a, b, c = T[ii, 0], T[ii, 1], T[ii, 2]
    bc = np.clip(baricentro(cP, a, b, c), 0, None)
    bc /= bc.sum(-1, keepdims=True)
    q = bc[..., :1] * a + bc[..., 1:2] * b + bc[..., 2:3] * c
    nota = np.linalg.norm(cP - q, axis=-1) + 5 * (1 - np.abs((nT[ii] * nP[:, None]).sum(-1)))
    j = ii[np.arange(len(P)), nota.argmin(1)]
    jj = np.repeat(j, 3)
    bc = baricentro(P.reshape(-1, 3), T[jj, 0], T[jj, 1], T[jj, 2])
    return (bc[:, :, None] * U[jj]).sum(1)


# ------------------------------------------------------------- colisão

def caixa_orientada(M, pontos_locais):
    """Caixa orientada (no jogo) dos pontos de origem levados por M."""
    L = M[:3].T                       # colunas: para onde vão os eixos da peça
    G = GIRO @ L * 0.01
    u, _, vt = np.linalg.svd(G)
    R = u @ vt
    if np.linalg.det(R) < 0:
        R[:, 0] *= -1
    pts = (aplicar(M, pontos_locais) * 0.01) @ GIRO.T
    proj = pts @ R
    lo, hi = proj.min(0), proj.max(0)
    centro = R @ ((lo + hi) / 2)
    return centro, hi - lo, R


def cantos(lo, hi):
    return np.array([[x, y, z] for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])])


def piso_de(nome):
    for k, v in PISO.items():
        if nome.startswith(k):
            return v
    return ""


# ---------------------------------------------------------------- main

def main(fbx, pacote):
    tmp = tempfile.mkdtemp(prefix="armazem_")
    modelos, paleta, caminho_de, cenas = abrir_pacote(pacote, tmp)
    if not modelos or not paleta:
        sys.exit("não achei Models/*.fbx e a paleta (Texture_FPSMap4_01.png) no pacote")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    # A cena primeiro: os nomes das peças não brigam com os dos modelos.
    bpy.ops.import_scene.fbx(filepath=fbx)
    cena = [o for o in bpy.data.objects if o.type == "MESH"]
    pecas = {o: triangulos(o, False)[0] for o in cena}

    origem = {}
    for nome, arquivo in sorted(modelos.items()):
        antes = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=arquivo)
        novos = [o for o in bpy.data.objects if o not in antes]
        for o in novos:
            if o.type == "MESH" and o.data.uv_layers:
                origem[nome] = triangulos(o)
        for o in novos:
            bpy.data.objects.remove(o, do_unlink=True)
    por_tamanho = {}
    for nome, v in origem.items():
        por_tamanho.setdefault(len(v[0]), []).append((nome, v))
    print(f"{len(cena)} peças na cena, {len(origem)} modelos de origem")

    vidro = [o for o in cena if o.active_material and "GLASS" in o.active_material.name]
    encaixe = {}
    for n, o in enumerate(cena):
        if o in vidro:
            continue
        P = pecas[o]
        e, nome, M = encaixar(P, por_tamanho.get(len(P), []))
        if nome is None:
            print(f"  {o.name}: sem modelo de origem, fica sem textura")
            continue
        if e > 1.0:
            print(f"  {o.name} ({nome}): encaixe com erro de {e:.1f} cm")
        encaixe[o] = (nome, M)
    print(f"{len(encaixe)} peças encaixadas")

    # Variantes de cor: ShippingContainer_01/02/03, Barrel_02/03/04, as
    # portas, os pisos… têm a MESMA malha e só mudam a UV. A forma não
    # diz qual é; a cena da Unity diz (a peça cujo pivô cai no mesmo
    # lugar). A Unity guarda Z invertido em relação ao FBX.
    melhor_cena, unity = None, []
    for c in cenas:
        lista = pecas_da_unity(c, caminho_de)
        if len(lista) > len(unity):
            melhor_cena, unity = c, lista
    if unity:
        pos = np.array([p for _, p in unity]) * np.array([1, 1, -1]) * 100
        arvore = cKDTree(pos)
        trocadas = 0
        for o, (nome, M) in list(encaixe.items()):
            perto = arvore.query_ball_point(M[3], 2.0)
            for i in perto:
                outro = unity[i][0]
                if outro == nome or outro not in origem or len(origem[outro][0]) != len(origem[nome][0]):
                    continue
                A = unicos(origem[outro][0])
                B = unicos(pecas[o])
                if cKDTree(B).query(aplicar(M, A))[0].max() < 0.05:
                    encaixe[o] = (outro, M)
                    trocadas += 1
                    break
                # Mesma peça, outro lado (porta esquerda × direita): encaixa de novo.
                e, _, M2 = encaixar(pecas[o], [(outro, origem[outro])])
                if e < 0.5:
                    encaixe[o] = (outro, M2)
                    trocadas += 1
                    break
        print(f"cena da Unity: {caminho_de[melhor_cena][1]}, {len(unity)} malhas; {trocadas} peças trocadas pela variante de cor certa")

    # UV
    for o, (nome, M) in encaixe.items():
        S, U = origem[nome]
        uv = copiar_uv(pecas[o], S, U, M)
        assert all(len(p.vertices) == 3 for p in o.data.polygons)
        lay = o.data.uv_layers.new(name="UVMap")
        lay.data.foreach_set("uv", uv.reshape(-1).astype(np.float32))

    # Colisão (caixas) e luzes, nas coordenadas do jogo.
    caixas, luzes, contagem, escadas = [], [], {}, []
    for o, (nome, M) in encaixe.items():
        if nome == "Wall_01":
            # A escada de marinheiro do meio da parede, do chão à passarela
            # (3,63 m). Nas medidas do modelo (cm): degraus em x ±49, a
            # frente deles em z 178 (a parede é z≈0, a sala fica em +z).
            # Sai o pé da escada e para onde ela olha; a escada em si (o
            # volume de subir e a chapa lisa no lugar dos degraus) é do
            # jogo, igual em todo mapa: kit.Mapa.escada e scripts/core/Ladder.cs.
            pe = (aplicar(M, np.array([[0.0, 0.0, 178.1]]))[0] * 0.01) @ GIRO.T
            fora = GIRO @ (M[:3].T @ np.array([0.0, 0.0, 1.0]))
            yaw = float(np.degrees(np.arctan2(fora[0], fora[2])))
            escadas.append(dict(nome=f"Escada{len(escadas) + 1}", pe=[round(x, 4) for x in pe],
                                yaw=round(yaw, 2), altura=3.63, largura=0.98))
        if nome.startswith(CASCA) or nome.startswith(SEM_COLISAO):
            if nome.startswith("Light_"):
                S = origem[nome][0].reshape(-1, 3)
                c, tam, R = caixa_orientada(M, cantos(S.min(0), S.max(0)))
                tipo = "teto" if nome in ("Light_03", "Light_04") else ("refletor" if nome in ("Light_01", "Light_02") else "arandela")
                pos = c - np.array([0, 0.35, 0]) if tipo == "teto" else c + np.array([0, tam[1] * 0.4, 0])
                luzes.append(dict(tipo=tipo, pos=[round(x, 3) for x in pos]))
            continue
        S = origem[nome][0].reshape(-1, 3)
        lo, hi = S.min(0), S.max(0)
        partes = [("", lo, hi)]
        if nome in CONTEINER:
            # Túnel: chão, teto e os dois lados (13,8 cm de chapa); as
            # pontas são abertas e as portas são peças à parte.
            e = 13.8
            partes = [("Chao", lo, np.array([hi[0], lo[1] + e, hi[2]])),
                      ("Teto", np.array([lo[0], hi[1] - e, lo[2]]), hi),
                      ("LadoA", lo, np.array([lo[0] + e, hi[1], hi[2]])),
                      ("LadoB", np.array([hi[0] - e, lo[1], lo[2]]), hi)]
        contagem[nome] = contagem.get(nome, 0) + 1
        for sufixo, a, b in partes:
            pts = S if not sufixo and nome not in CONTEINER else cantos(a, b)
            c, tam, R = caixa_orientada(M, pts)
            caixas.append(dict(nome=f"{nome}_{contagem[nome]}{sufixo}", centro=[round(x, 4) for x in c],
                               tamanho=[round(max(x, 0.02), 4) for x in tam],
                               base=[[round(x, 5) for x in linha] for linha in R], piso=piso_de(nome)))

    # Vidros: triângulos soltos (posição e normal), para a cena.
    vidros = []
    for o in vidro:
        P = (pecas[o].reshape(-1, 3) * 0.01) @ GIRO.T
        vidros += [[round(x, 4) for x in p] for p in P]

    # O desenho: tudo girado para o jogo, juntado em seis pedaços.
    gira = np.eye(4)
    gira[:3, :3] = np.array([[0, -1, 0], [1, 0, 0], [0, 0, 1]])   # +90° em Z do Blender = +90° em Y no jogo
    from mathutils import Matrix
    G = Matrix(gira.tolist())
    for o in cena:
        o.matrix_world = G @ o.matrix_world
    bpy.ops.object.select_all(action="SELECT")
    bpy.context.view_layer.objects.active = cena[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    img = bpy.data.images.load(paleta)
    img.pack()
    for m in bpy.data.materials:
        nt = m.node_tree
        b = nt.nodes.get("Principled BSDF")
        if "MAIN" in m.name:
            m.name = "armazem_paleta"
            t = nt.nodes.new("ShaderNodeTexImage")
            t.image = img
            nt.links.new(t.outputs[0], b.inputs["Base Color"])
            b.inputs["Roughness"].default_value = 0.85
            b.inputs["Metallic"].default_value = 0.0

    casca = []
    for o, (nome, M) in encaixe.items():
        if nome.startswith(CASCA):
            c = o.copy()
            c.data = o.data.copy()
            bpy.context.collection.objects.link(c)
            casca.append(c)
    desenho = [o for o in cena if o not in vidro]
    for o in vidro:
        bpy.data.objects.remove(o, do_unlink=True)

    def juntar(objs, nome):
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.object.join()
        # O FBX da Unity chega com cada canto de triângulo solto: solda os
        # iguais. A normal de cada canto (o facetado do low poly) é lida
        # antes e posta de volta depois: soldar sem isso deixa tudo com
        # sombreado suave, borrado.
        me = objs[0].data
        normais = np.zeros(len(me.loops) * 3, np.float32)
        me.corner_normals.foreach_get("vector", normais)
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
        bm.to_mesh(me)
        bm.free()
        if len(me.loops) * 3 == len(normais):
            me.normals_split_custom_set(normais.reshape(-1, 3).tolist())
        else:
            print(f"  {nome}: a solda mudou os cantos; normais recalculadas")
        objs[0].name = nome
        objs[0].data.name = nome
        return objs[0]

    pedacos = {}
    for o in desenho:
        cx = np.mean([v.co[0] for v in o.data.vertices])       # Blender X = X do jogo
        cy = np.mean([v.co[1] for v in o.data.vertices])       # Blender Y = −Z do jogo
        pedacos.setdefault((int(np.clip((cx + 23) // (46 / 3), 0, 2)), int(cy > 0)), []).append(o)
    for (i, j), objs in sorted(pedacos.items()):
        juntar(objs, f"armazem_{i}{j}")
    juntar(casca, "casca-colonly")

    os.makedirs(os.path.dirname(GLB), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=GLB, export_format="GLB", export_yup=True, export_apply=True,
                              export_materials="EXPORT", export_cameras=False, export_lights=False,
                              export_extras=False, export_animations=False)
    with open(JSON, "w", encoding="utf-8") as fh:
        json.dump(dict(origem="Low Poly FPS Map 4 (Unity), convertido por tools/mapas/blender/armazem.py",
                       caixas=caixas, luzes=luzes, escadas=escadas, vidros=vidros), fh, ensure_ascii=False, indent=0)
    print(f"{GLB}\n{JSON}: {len(caixas)} caixas, {len(luzes)} luzes, {len(escadas)} escadas, {len(vidros) // 3} triângulos de vidro")
    shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    if len(args) != 2:
        sys.exit(__doc__)
    main(*args)
