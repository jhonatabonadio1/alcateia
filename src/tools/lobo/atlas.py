"""O que as ferramentas do lobo dividem, só com numpy: ler um GLB com pele (malhas, ossos e
juntas) e levar a malha ao atlas (por texel: peça, triângulo, baricêntricas)."""
import numpy as np
from glb import Glb

# ------------------------------------------------------------------ leitura

def carregar(path):
    g = Glb(path)
    d = g.doc
    pecas = []
    for node in d["nodes"]:
        if "mesh" not in node:
            continue
        mesh = d["meshes"][node["mesh"]]
        for p in mesh["primitives"]:
            a = p["attributes"]
            pecas.append(dict(nome=mesh["name"], prim=p,
                              pos=g.accessor(a["POSITION"]).astype(np.float32),
                              nrm=g.accessor(a["NORMAL"]).astype(np.float32),
                              uv=g.accessor(a["TEXCOORD_0"]).astype(np.float32),
                              joints=g.accessor(a["JOINTS_0"]).astype(np.int32),
                              weights=g.accessor(a["WEIGHTS_0"]).astype(np.float32),
                              idx=g.accessor(p["indices"]).astype(np.int64).reshape(-1, 3)))
    ossos = [d["nodes"][j]["name"].replace("mixamorig:", "") for j in d["skins"][0]["joints"]]
    ibm = g.accessor(d["skins"][0]["inverseBindMatrices"]).reshape(-1, 4, 4)
    juntas = np.array([np.linalg.inv(M.T)[:3, 3] for M in ibm], np.float32)
    return g, pecas, ossos, juntas


def rasterizar(pecas, size):
    """Por texel: peça, triângulo e baricêntricas."""
    k2 = (size / 2048.0) ** 2            # o que é "triângulo miúdo" cresce com a grade
    peca = np.full((size, size), 255, np.uint8)
    tri_id = np.zeros((size, size), np.int32)
    bary = np.zeros((size, size, 3), np.float32)
    dentro = np.zeros((size, size), bool)      # o centro do texel cai dentro do triângulo dono
    for pi, p in enumerate(pecas):
        uv = p["uv"] * size
        for ti, tri in enumerate(p["idx"]):
            a, b, c = uv[tri]
            x0 = int(max(np.floor(min(a[0], b[0], c[0])) - 1, 0)); x1 = int(min(np.ceil(max(a[0], b[0], c[0])) + 1, size - 1))
            y0 = int(max(np.floor(min(a[1], b[1], c[1])) - 1, 0)); y1 = int(min(np.ceil(max(a[1], b[1], c[1])) + 1, size - 1))
            den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
            if x1 < x0 or y1 < y0 or abs(den) < 1e-9:
                continue
            xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            w0 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / den
            w1 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / den
            w2 = 1 - w0 - w1
            tol = 0.35 if abs(den) < 6 * k2 else 0.08
            inside = (w0 >= -tol) & (w1 >= -tol) & (w2 >= -tol)
            if not inside.any():
                continue
            # quem tem o centro do texel dentro manda; a folga só preenche o que sobrou
            certo = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
            sub = dentro[y0:y1 + 1, x0:x1 + 1]
            inside = certo | (inside & ~sub)
            yy, xx = np.nonzero(inside)
            Y = yy + y0; X = xx + x0
            peca[Y, X] = pi
            tri_id[Y, X] = ti
            bary[Y, X] = np.stack([w0[yy, xx], w1[yy, xx], w2[yy, xx]], 1)
            dentro[Y, X] = certo[yy, xx]
    return peca, tri_id, bary


def interpolar(pecas, peca, tri_id, bary, chave=None, valores=None):
    """Um atributo de vértice levado a cada texel."""
    n = (valores[0] if valores is not None else pecas[0][chave]).shape[1]
    out = np.zeros(peca.shape + (n,), np.float32)
    for pi, p in enumerate(pecas):
        k = peca == pi
        if not k.any():
            continue
        v = valores[pi] if valores is not None else p[chave]
        tri = p["idx"][tri_id[k]]
        w = np.clip(bary[k], 0, 1); w /= w.sum(1, keepdims=True)
        out[k] = (v[tri] * w[:, :, None]).sum(1)
    return out

# ------------------------------------------------------------------- ruído
