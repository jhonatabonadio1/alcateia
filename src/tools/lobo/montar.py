"""Monta o GLB do lobo: o esqueleto do lobo do jogo (mais os ossos do rabo), a malha nova
(malha.npz, de malha.py) e as texturas (pintar.py).
uso: montar.py <time> <lobo_do_jogo_original.glb> <malha.npz> <pasta_do_modelo> <saida.glb> [--ao ao.png] [--previa pasta]"""
import sys, io, os, glob, numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from glb import Glb
import pintar

Image.MAX_IMAGE_PIXELS = None


def trs(n):
    M = np.eye(4)
    if "matrix" in n:
        return np.array(n["matrix"], float).reshape(4, 4).T
    if "scale" in n:
        M = M @ np.diag(list(n["scale"]) + [1.0])
    if "rotation" in n:
        x, y, z, w = n["rotation"]
        R = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                      [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                      [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
        Rm = np.eye(4); Rm[:3, :3] = R; M = Rm @ M
    if "translation" in n:
        T = np.eye(4); T[:3, 3] = n["translation"]; M = T @ M
    return M


def quat(R):
    t = np.trace(R)
    if t > 0:
        s = np.sqrt(t + 1) * 2; w = 0.25 * s; x = (R[2, 1] - R[1, 2]) / s; y = (R[0, 2] - R[2, 0]) / s; z = (R[1, 0] - R[0, 1]) / s
    elif R[0, 0] > R[1, 1] and R[0, 0] > R[2, 2]:
        s = np.sqrt(1 + R[0, 0] - R[1, 1] - R[2, 2]) * 2; w = (R[2, 1] - R[1, 2]) / s; x = 0.25 * s; y = (R[0, 1] + R[1, 0]) / s; z = (R[0, 2] + R[2, 0]) / s
    elif R[1, 1] > R[2, 2]:
        s = np.sqrt(1 + R[1, 1] - R[0, 0] - R[2, 2]) * 2; w = (R[0, 2] - R[2, 0]) / s; x = (R[0, 1] + R[1, 0]) / s; y = 0.25 * s; z = (R[1, 2] + R[2, 1]) / s
    else:
        s = np.sqrt(1 + R[2, 2] - R[0, 0] - R[1, 1]) * 2; w = (R[1, 0] - R[0, 1]) / s; x = (R[0, 2] + R[2, 0]) / s; y = (R[1, 2] + R[2, 1]) / s; z = 0.25 * s
    return [float(x), float(y), float(z), float(w)]


def montar(time, src, malha_npz, pasta_ref, saida, ao=None, previa=None):
    g = Glb(src); d = g.doc
    m = np.load(malha_npz)
    nomes = [n.get("name") for n in d["nodes"]]
    skin = d["skins"][0]
    ibm = g.accessor(skin["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1).astype(np.float64)   # colunas do glTF -> matrizes
    ossos = [d["nodes"][j]["name"].replace("mixamorig:", "") for j in skin["joints"]]

    # ---------------------------------------------------------- o esqueleto: mais o rabo
    pais = {}
    for i, n in enumerate(d["nodes"]):
        for c in n.get("children", []):
            pais[c] = i
    def mundo(i):
        M = trs(d["nodes"][i])
        while i in pais:
            i = pais[i]; M = trs(d["nodes"][i]) @ M
        return M
    quadril = nomes.index("mixamorig:Hips")
    # Do espaço da malha para o da cena, no repouso (a armadura do Mixamo vem em centímetros,
    # com escala 0,01): o osso novo entra na cena onde a malha o espera.
    Mq = mundo(quadril) @ ibm[ossos.index("Hips")]
    rj = m["rabo_j"].astype(np.float64); nr = len(rj)
    armadura = nomes.index("Armature")
    # as malhas da roupa e do equipamento saem: a malha nova é uma só
    sobra = [i for i, n in enumerate(d["nodes"]) if "mesh" in n and n.get("name") != "corpo"]
    d["nodes"][armadura]["children"] = [c for c in d["nodes"][armadura]["children"] if c not in sobra]
    livres = list(sobra)
    pai, G_pai = quadril, mundo(quadril)
    unidade = np.linalg.norm(G_pai[:3, 0]) / np.linalg.norm(Mq[:3, 0])
    novas_ibm = []
    for i in range(nr):
        y = rj[min(i + 1, nr - 1)] - rj[min(i, nr - 2)]; y /= np.linalg.norm(y)       # o osso aponta para a junta seguinte
        x = np.array([1.0, 0, 0]); x -= y * (x @ y); x /= np.linalg.norm(x); z = np.cross(x, y)
        # Na unidade dos outros ossos (o esqueleto do Mixamo é em centímetros, dentro de uma
        # armadura de escala 0,01): com os eixos em metros o osso saía com escala 100 em
        # relação ao quadril, e a pose mexida pelo simulador do rabo dava solavancos.
        B = np.eye(4); B[:3, 0] = x * unidade; B[:3, 1] = y * unidade; B[:3, 2] = z * unidade; B[:3, 3] = rj[i]
        G = Mq @ B
        local = np.linalg.inv(G_pai) @ G
        esc = np.linalg.norm(local[:3, :3], axis=0)
        no = {"name": f"rabo{i + 1}", "translation": [float(v) for v in local[:3, 3]], "rotation": quat(local[:3, :3] / esc)}
        assert np.abs(esc - 1).max() < 1e-3, f"o osso do rabo saiu com escala {esc}"
        if livres:
            ni = livres.pop(0); d["nodes"][ni] = no
        else:
            d["nodes"].append(no); ni = len(d["nodes"]) - 1
        d["nodes"][pai].setdefault("children", []).append(ni)
        skin["joints"].append(ni); novas_ibm.append(np.linalg.inv(B))
        pai, G_pai = ni, G
    assert not livres
    assert [str(o) for o in m["ossos"]] == ossos + [f"rabo{i + 1}" for i in range(nr)], "a malha foi pesada para outro esqueleto"
    ibm = np.concatenate([ibm, np.array(novas_ibm)])

    # ---------------------------------------------------------- texturas
    # o atlas rasterizado é o mesmo para os dois times: guarda ao lado da malha
    tex = pintar.pintar(time, m, pasta_ref, ao=ao, previa=previa, cache=os.path.splitext(malha_npz)[0] + "_atlas.npz")

    # ---------------------------------------------------------- o arquivo, do zero
    d["accessors"] = []; d["bufferViews"] = []; g.views = []; d["images"] = []; d["textures"] = []
    d["meshes"] = [mm for mm in d["meshes"] if mm["name"] == "corpo"]
    d["nodes"][nomes.index("corpo")]["mesh"] = 0
    skin["inverseBindMatrices"] = g.add_accessor(ibm.transpose(0, 2, 1).reshape(-1, 16).astype(np.float32), "MAT4")
    d["bufferViews"][-1].pop("target", None)
    attrs = dict(POSITION=g.add_accessor(m["pos"], "VEC3"), NORMAL=g.add_accessor(m["nrm"], "VEC3"), TANGENT=g.add_accessor(m["tg"], "VEC4"),
                 TEXCOORD_0=g.add_accessor(m["uv"], "VEC2"), JOINTS_0=g.add_accessor(m["joints"].astype(np.uint8), "VEC4"),
                 WEIGHTS_0=g.add_accessor(m["weights"], "VEC4"), COLOR_0=g.add_accessor(tex["cor_vertice"], "VEC4", normalized=True))
    ap = d["accessors"][attrs["POSITION"]]; ap["min"] = m["pos"].min(0).tolist(); ap["max"] = m["pos"].max(0).tolist()
    iv = g.add_view(m["idx"].astype(np.uint32).tobytes(), 34963)
    d["accessors"].append({"bufferView": iv, "componentType": 5125, "count": int(m["idx"].size), "type": "SCALAR"})
    d["meshes"][0]["primitives"] = [{"attributes": attrs, "indices": len(d["accessors"]) - 1, "material": 0, "mode": 4}]

    def jpg(a, q):
        b = io.BytesIO(); Image.fromarray(a).save(b, "JPEG", quality=q, subsampling=0); return b.getvalue()
    for nome, q in (("base", 93), ("orm", 90), ("normal", 94), ("emissao", 92)):
        v = g.add_view(jpg(tex[nome], q))
        d["images"].append({"name": "lobo_" + nome, "mimeType": "image/jpeg", "bufferView": v})
        d["textures"].append({"sampler": 0, "source": len(d["images"]) - 1})
    d["materials"] = [{"name": "lobo", "doubleSided": True, "extras": {"polido": 2},
                       "pbrMetallicRoughness": {"baseColorTexture": {"index": 0}, "metallicRoughnessTexture": {"index": 1}},
                       "occlusionTexture": {"index": 1}, "normalTexture": {"index": 2},
                       "emissiveTexture": {"index": 3}, "emissiveFactor": [1.0, 1.0, 1.0]}]
    d["asset"]["generator"] = "alcateia lobo v3 (malha e textura Meshy, esqueleto do jogo, rabo com ossos)"
    g.save(saida)
    print(f"{time}: {len(m['pos'])} vértices, {len(m['idx'])} triângulos, {len(skin['joints'])} ossos, {os.path.getsize(saida) / 1e6:.1f} MB")


if __name__ == "__main__":
    a = sys.argv[1:]
    def opc(nome):
        return a[a.index(nome) + 1] if nome in a else None
    montar(a[0], a[1], a[2], a[3], a[4], ao=opc("--ao"), previa=opc("--previa"))
