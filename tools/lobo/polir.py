"""
Polimento do lobo (pedido do Jhon, 18/11): pelo pintado, cores mais vivas,
oclusão assada, neon de verdade (emissão) nos olhos, braceletes, peitoral,
joelheiras e costas, e as máscaras de vértice para o pelo balançar.

    python3 tools/lobo/polir.py <time> <entrada.glb> <ao.png> <saida.glb> [--previa pasta]

Só mexe nas texturas e soma a cor de vértice: malha, ossos e pesos saem
como entraram.
"""
import io, sys, os
import numpy as np
from PIL import Image
from matplotlib.colors import rgb_to_hsv, hsv_to_rgb
from scipy import ndimage
from glb import Glb

SIZE = 2048

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


def rasterizar(pecas, size=SIZE):
    """Por texel: peça, triângulo e baricêntricas."""
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
            tol = 0.35 if abs(den) < 6 else 0.08
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

def _hash(ix, iy, iz, seed):
    h = (ix.astype(np.uint64) * np.uint64(374761393) + iy.astype(np.uint64) * np.uint64(668265263)
         + iz.astype(np.uint64) * np.uint64(2246822519) + np.uint64(seed * 3266489917 % (1 << 32))) & np.uint64(0xFFFFFFFF)
    h = ((h ^ (h >> np.uint64(13))) * np.uint64(1274126177)) & np.uint64(0xFFFFFFFF)
    h = h ^ (h >> np.uint64(16))
    return (h & np.uint64(0xFFFFFF)).astype(np.float32) / float(0xFFFFFF)


def ruido(q, seed=0):
    """Ruído de valor 3D, de 0 a 1."""
    i = np.floor(q).astype(np.int64)
    f = (q - i).astype(np.float32)
    u = f * f * (3 - 2 * f)
    out = np.zeros(len(q), np.float32)
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (u[:, 0] if dx else 1 - u[:, 0]) * (u[:, 1] if dy else 1 - u[:, 1]) * (u[:, 2] if dz else 1 - u[:, 2])
                out += w * _hash(i[:, 0] + dx, i[:, 1] + dy, i[:, 2] + dz, seed)
    return out


def fios(p, d, fino, comprido, seed):
    """Ruído esticado ao longo de d: fios de 'fino' metros de largura e 'comprido' de comprimento."""
    d = d / np.linalg.norm(d)
    ao_longo = p @ d
    q = p / fino + np.outer(ao_longo, d) * (1.0 / comprido - 1.0 / fino)
    return ruido(q, seed)

# ------------------------------------------------------------------ o lobo

GRUPOS = {
    "tronco": ["Hips", "Spine", "Spine1", "Spine2", "Neck", "LeftShoulder", "RightShoulder",
               "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase"],
    "cabeca": ["Head", "HeadTop_End", "headfront"],
    "braco_e": ["LeftArm", "LeftForeArm", "LeftHand"],
    "braco_d": ["RightArm", "RightForeArm", "RightHand"],
}

TIMES = {
    # neon: a cor da luz; pelo: como reconhecer pelo pela cor; grade: saturação e valor
    "azul": dict(neon=(0.25, 0.85, 1.0), ponta=(0.60, 0.69, 0.98), geada=0.5, sat=1.22, val=1.10, equip_val=1.38, roupa_val=1.35),
    "vermelho": dict(neon=(1.0, 0.16, 0.12), ponta=(1.0, 0.36, 0.30), geada=0.3, sat=1.14, val=1.04, equip_val=1.30, roupa_val=1.15),
}


def suave(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def protuberancia(pecas):
    """Quanto cada vértice sai para fora do corpo alisado (m): as pontas dos tufos."""
    saida = []
    for p in pecas:
        pos = p["pos"]
        chave = np.round(pos * 5000).astype(np.int64)
        _, inv = np.unique(chave, axis=0, return_inverse=True)
        inv = inv.reshape(-1)
        n = inv.max() + 1
        w = np.zeros((n, 3), np.float64); cnt = np.zeros(n)
        np.add.at(w, inv, pos); np.add.at(cnt, inv, 1)
        w /= cnt[:, None]
        nrm = np.zeros((n, 3)); np.add.at(nrm, inv, p["nrm"])
        nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-6)
        e = inv[p["idx"]]
        a = np.concatenate([e[:, 0], e[:, 1], e[:, 2], e[:, 1], e[:, 2], e[:, 0]])
        b = np.concatenate([e[:, 1], e[:, 2], e[:, 0], e[:, 0], e[:, 1], e[:, 2]])
        s = w.copy()
        deg = np.zeros(n); np.add.at(deg, a, 1)
        for _ in range(12):
            acc = np.zeros((n, 3)); np.add.at(acc, a, s[b])
            s = 0.5 * s + 0.5 * acc / np.maximum(deg, 1)[:, None]
        fora = ((w - s) * nrm).sum(1)
        saida.append(np.clip(fora, 0, None)[inv].astype(np.float32)[:, None])
    return saida



def gerar_pelos(time, pecas, CORPO, pelo_tex, juba_tex, cobertos, juntas, ossos, seed=7):
    """Tufos pontudos de pelo, em malha (pedido do Jhon, 18/11: "os pelos não devem ser
    quadrados, devem ser pontudos; crie mais pelos, mais detalhados").

    Espalha pirâmides finas de três lados sobre o pelo do corpo — compridas e curvas na
    juba e nas costas, curtas no resto —, deitadas no sentido do pelo. Cada tufo copia os
    pesos do ponto onde nasce (anda com o corpo) e o UV dele (tem a cor do pelo dali). A
    cor de vértice diz ao material o que é raiz e o que é ponta: R balança (0 na raiz, 1 na
    ponta), B clareia (raiz escura, ponta clara).
    """
    rng = np.random.default_rng(seed)
    p = pecas[CORPO]
    P, N, UV, J, Wt, I = p["pos"], p["nrm"], p["uv"], p["joints"], p["weights"], p["idx"]
    a, b, c = P[I[:, 0]], P[I[:, 1]], P[I[:, 2]]
    fn = np.cross(b - a, c - a)
    area = np.linalg.norm(fn, axis=1) * 0.5
    fn = fn / np.maximum(np.linalg.norm(fn, axis=1, keepdims=True), 1e-9)
    # a normal da face tem de apontar para fora: confere com a dos vértices
    vn = (N[I[:, 0]] + N[I[:, 1]] + N[I[:, 2]])
    fn = np.where((fn * vn).sum(1, keepdims=True) < 0, -fn, fn)
    cen = (a + b + c) / 3
    uvc = (UV[I[:, 0]] + UV[I[:, 1]] + UV[I[:, 2]]) / 3
    size = pelo_tex.shape[0]
    tx = np.clip((uvc[:, 0] * size).astype(int), 0, size - 1); ty = np.clip((uvc[:, 1] * size).astype(int), 0, size - 1)
    pelo = pelo_tex[ty, tx]; juba = juba_tex[ty, tx]

    ids = lambda lista: [ossos.index(n) for n in lista]
    def peso(lista):
        w = np.where(np.isin(J, ids(lista)), Wt, 0).sum(1)
        return (w[I[:, 0]] + w[I[:, 1]] + w[I[:, 2]]) / 3
    w_maos = peso(["LeftHand", "RightHand", "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase"])
    w_cab = peso(["Head", "HeadTop_End", "headfront"])
    w_be = peso(["LeftArm", "LeftForeArm", "LeftHand"]); w_bd = peso(["RightArm", "RightForeArm", "RightHand"])

    # onde nasce tufo: no pelo, fora de mãos e pés, fora do rosto, e não embaixo de roupa ou armadura
    rosto = w_cab * (cen[:, 2] > 0.045) * (cen[:, 1] < 1.56)
    livre = np.ones(len(I), bool)
    if cobertos is not None:
        d, _ = cobertos.query(cen + fn * 0.01, distance_upper_bound=0.05)
        livre = d > 0.026
    # dentro da boca e das orelhas a normal aponta para dentro do corpo: fora
    # Poucos (pedido do Jhon, 18/11: "use o original e coloque só mais alguns"): a
    # pelagem do modelo continua sendo a de sempre; os tufos só quebram o contorno.
    # O vermelho já tem a quantidade certa de pelo ("o ideal", Jhon): não ganha tufo.
    dens = np.where(juba > 0.35, 70.0, 30.0) * pelo * (1.0 if time == "azul" else 0.0)
    # Na cabeça não entra tufo: lascas finas ali ficaram estranhas (Jhon, 18/11); a juba é a do modelo.
    dens = dens * (w_cab < 0.08) * (cen[:, 1] < 1.34) * (1 - np.clip(w_maos * 2.5, 0, 1)) * (1 - np.clip(rosto * 2.0, 0, 1)) * livre
    # por trás e por cima um pouco mais (é o que a câmera vê); barriga e parte de dentro das pernas, menos
    dens *= 1.0 + 0.35 * np.clip(-fn[:, 2], 0, 1) + 0.2 * np.clip(fn[:, 1], 0, 1) - 0.45 * np.clip(-fn[:, 1], 0, 1)
    n = rng.poisson(np.clip(dens, 0, None) * area)
    tri = np.repeat(np.arange(len(I)), n)
    m = len(tri)
    r1, r2 = rng.random(m), rng.random(m)
    s1 = np.sqrt(r1)
    bw = np.stack([1 - s1, s1 * (1 - r2), s1 * r2], 1).astype(np.float32)
    pt = (bw[:, :1] * a[tri] + bw[:, 1:2] * b[tri] + bw[:, 2:] * c[tri])
    # o UV vem puxado para o meio do triângulo: longe da costura da ilha
    bwc = bw * 0.55 + 0.15
    uv = bwc[:, :1] * UV[I[tri, 0]] + bwc[:, 1:2] * UV[I[tri, 1]] + bwc[:, 2:] * UV[I[tri, 2]]
    dono = I[tri, bw.argmax(1)]
    nrm = fn[tri]
    jb = juba[tri]

    # o sentido do pelo: para baixo no tronco e nas pernas, para trás na cabeça, ao longo do braço
    fluxo = np.tile(np.array([0.0, -1.0, -0.18], np.float32), (m, 1))
    cab = w_cab[tri] > 0.5
    fluxo[cab] = np.array([0.0, -0.15, -1.0])
    # no alto da cabeça a juba levanta um pouco: para trás e para cima
    topo = cab & (pt[:, 1] > 1.5)
    fluxo[topo] = np.array([0.0, 0.32, -0.95])
    # nuca e alto das costas: para baixo e para trás
    nuca = (jb > 0.35) & ~cab
    fluxo[nuca] = np.array([0.0, -0.85, -0.5])
    for lado, w_ in ((1, w_be), (-1, w_bd)):
        ee = juntas[ossos.index("LeftHand" if lado == 1 else "RightHand")] - juntas[ossos.index("LeftArm" if lado == 1 else "RightArm")]
        fluxo[w_[tri] > 0.5] = ee / np.linalg.norm(ee)
    fluxo /= np.linalg.norm(fluxo, axis=1, keepdims=True)

    comp = np.where(jb > 0.35, rng.uniform(0.06, 0.095, m), rng.uniform(0.04, 0.065, m)).astype(np.float32)
    comp *= 0.85 + 0.3 * rng.random(m)
    saida = np.where(jb > 0.35, 0.30, 0.18)[:, None]          # quanto sai do corpo (o resto deita no sentido do pelo)
    d = nrm * saida + fluxo * (1 - saida) + rng.normal(0, 0.09, (m, 3))
    # nunca para dentro
    dentro = (d * nrm).sum(1)
    d += nrm * np.clip(0.16 - dentro, 0, None)[:, None]
    d /= np.linalg.norm(d, axis=1, keepdims=True)
    raio = np.clip(comp * rng.uniform(0.30, 0.40, m), 0.012, 0.038)

    ref = np.where(np.abs(d[:, 1:2]) < 0.9, np.array([[0.0, 1.0, 0.0]]), np.array([[1.0, 0.0, 0.0]]))
    t1 = np.cross(d, ref); t1 /= np.linalg.norm(t1, axis=1, keepdims=True)
    t2 = np.cross(d, t1)
    giro = rng.random(m) * 2 * np.pi
    base = pt - nrm * 0.006
    # a ponta cai um pouco no sentido do pelo: o tufo é curvo
    curva = fluxo * comp[:, None] * np.where(jb > 0.35, 0.30, 0.18)[:, None]
    meio = base + d * comp[:, None] * 0.5 + curva * 0.25
    ponta = base + d * comp[:, None] + curva
    ang = giro[:, None] + np.array([0, 2 * np.pi / 3, 4 * np.pi / 3])[None, :]
    anel0 = base[:, None, :] + raio[:, None, None] * (np.cos(ang)[..., None] * t1[:, None, :] + np.sin(ang)[..., None] * t2[:, None, :])
    anel1 = meio[:, None, :] + (raio * 0.56)[:, None, None] * (np.cos(ang)[..., None] * t1[:, None, :] + np.sin(ang)[..., None] * t2[:, None, :])

    # faces: três lados, cada um com um quadrilátero (base→meio) e um triângulo (meio→ponta); faces planas
    pos_l, cor_l = [], []
    R0, R1, R2 = 0.0, 0.42, 1.0          # balanço: raiz, meio, ponta
    B0, B1, B2 = 0.30, 0.58, 0.98        # tom: raiz escura, ponta clara
    for k in range(3):
        k2 = (k + 1) % 3
        A0, A1 = anel0[:, k], anel0[:, k2]
        M0, M1 = anel1[:, k], anel1[:, k2]
        for tri_v, tri_c in (((A0, A1, M1), (0, 0, 1)), ((A0, M1, M0), (0, 1, 1)), ((M0, M1, ponta), (1, 1, 2))):
            pos_l.append(np.stack(tri_v, 1))
            cor_l.append(tri_c)
    pos_t = np.concatenate(pos_l, 0)                                  # (9m, 3, 3)
    nt = len(pos_t)
    fnn = np.cross(pos_t[:, 1] - pos_t[:, 0], pos_t[:, 2] - pos_t[:, 0])
    fnn /= np.maximum(np.linalg.norm(fnn, axis=1, keepdims=True), 1e-9)
    # para fora do eixo do tufo
    eixo_c = np.tile(meio, (9, 1))
    fora = (pos_t.mean(1) - eixo_c)
    vira = (fnn * fora).sum(1) < 0
    pos_t[vira] = pos_t[vira][:, [0, 2, 1]]
    fnn[vira] *= -1
    cor_idx = np.concatenate([np.tile(np.array(cc)[None, :], (m, 1)) for cc in cor_l], 0)
    cor_idx[vira] = cor_idx[vira][:, [0, 2, 1]]
    Rv = np.array([R0, R1, R2])[cor_idx]; Bv = np.array([B0, B1, B2])[cor_idx]

    V = pos_t.reshape(-1, 3).astype(np.float32)
    NN = np.repeat(fnn, 3, 0).astype(np.float32)
    rep = lambda x: np.repeat(np.tile(x, (9,) + (1,) * (x.ndim - 1)), 3, 0)
    UVv = rep(uv).astype(np.float32)
    Jv = rep(J[dono]).astype(np.uint8)
    Wv = rep(Wt[dono]).astype(np.float32)
    cor = np.stack([Rv.reshape(-1), np.ones(nt * 3), Bv.reshape(-1), np.ones(nt * 3)], 1)
    cor = np.clip(cor * 255 + 0.5, 0, 255).astype(np.uint8)
    idx = np.arange(nt * 3, dtype=np.uint32)
    # tangente qualquer, perpendicular à normal (o mapa de normal ali é quase liso)
    tg = np.cross(NN, np.array([0.0, 1.0, 0.0])); z = np.linalg.norm(tg, axis=1) < 1e-4
    tg[z] = np.array([1.0, 0.0, 0.0]); tg /= np.linalg.norm(tg, axis=1, keepdims=True)
    TG = np.concatenate([tg, np.ones((len(tg), 1))], 1).astype(np.float32)
    print(f"   pelos: {m} tufos ({int((jb > 0.35).sum())} na juba), {nt} triângulos")
    return dict(pos=V, nrm=NN, uv=UVv, joints=Jv, weights=Wv, cor=cor, idx=idx, tg=TG)


def polir(time, src, ao_path, out_glb, previa=None):
    T = TIMES[time]
    g, pecas, ossos, juntas = carregar(src)
    if g.doc["materials"][0].get("extras", {}).get("polido"):
        sys.exit("Este arquivo já foi polido: rodar de novo aplicaria a cor duas vezes. Use o GLB original "
                 "(o do Meshy, ou o do git antes de 18/11: git show 147a358:assets/characters/wolf/lobo_<time>.glb).")
    nomes = [p["nome"] for p in pecas]
    CORPO, ROUPA, EQUIP = nomes.index("corpo"), nomes.index("roupa"), nomes.index("equipamento")
    peca, tri_id, bary = rasterizar(pecas)
    ok = peca != 255
    pos = interpolar(pecas, peca, tri_id, bary, "pos")
    nrm = interpolar(pecas, peca, tri_id, bary, "nrm")
    nrm /= np.maximum(np.linalg.norm(nrm, axis=2, keepdims=True), 1e-6)

    # pesos por grupo de osso e por osso que interessa
    def peso_de(lista):
        ids = [ossos.index(n) for n in lista]
        return [np.where(np.isin(p["joints"], ids), p["weights"], 0).sum(1, keepdims=True).astype(np.float32) for p in pecas]
    gw = {k: interpolar(pecas, peca, tri_id, bary, valores=peso_de(v))[..., 0] for k, v in GRUPOS.items()}
    w_maos = interpolar(pecas, peca, tri_id, bary, valores=peso_de(["LeftHand", "RightHand", "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase"]))[..., 0]
    w_cabeca = gw["cabeca"]
    w_ante = interpolar(pecas, peca, tri_id, bary, valores=peso_de(["LeftForeArm", "RightForeArm"]))[..., 0]
    w_perna = interpolar(pecas, peca, tri_id, bary, valores=peso_de(["LeftLeg", "RightLeg", "LeftUpLeg", "RightUpLeg"]))[..., 0]
    w_ombro = interpolar(pecas, peca, tri_id, bary, valores=peso_de(["LeftShoulder", "RightShoulder", "LeftArm", "RightArm"]))[..., 0]
    w_tronco = interpolar(pecas, peca, tri_id, bary, valores=peso_de(["Spine", "Spine1", "Spine2", "Hips", "LeftShoulder", "RightShoulder", "Neck"]))[..., 0]
    prot_v = protuberancia(pecas)
    prot = interpolar(pecas, peca, tri_id, bary, valores=prot_v)[..., 0]

    base = np.asarray(Image.open(io.BytesIO(g.image_bytes(0))).convert("RGB")).astype(np.float32) / 255
    orm = np.asarray(Image.open(io.BytesIO(g.image_bytes(1))).convert("RGB")).astype(np.float32) / 255
    ao = np.asarray(Image.open(ao_path).convert("L")).astype(np.float32) / 255
    hsv = rgb_to_hsv(base)
    H, S, V = hsv[..., 0] * 360, hsv[..., 1], hsv[..., 2]

    # ------------------------------------------------------------ neon que já existe
    if time == "azul":
        ciano = (H > 160) & (H < 213) & (S > 0.38) & (V > 0.58)
        neon = ciano & (peca == EQUIP)
        # Os olhos do azul: só o miolo ciano de cada olho, numa caixinha em volta dele. Com o
        # limiar largo entrava pelo claro em volta e o olho virava um borrão de luz ("olho do
        # azul tá estranho", Jhon, 18/11).
        forte = (H > 165) & (H < 208) & (S > 0.45) & (V > 0.68) & (peca == CORPO) & (w_cabeca > 0.5) & (pos[..., 2] > 0.15)
        olhos = np.zeros_like(forte)
        for lado in (-1, 1):
            k = forte & ((pos[..., 0] - 0.013) * lado > 0)
            if k.sum() < 30:
                continue
            c = np.median(pos[k], 0)
            olhos |= k & (np.abs(pos - c) < np.array([0.017, 0.016, 0.028])).all(-1)
    else:
        verm = ((H < 16) | (H > 344))
        neon = verm & (peca == EQUIP) & (((S > 0.6) & (V > 0.78)) | ((V > 0.9) & (S > 0.2)))
        olhos = verm & (peca == CORPO) & (w_cabeca > 0.5) & (S > 0.6) & (V > 0.72) & (pos[..., 2] > 0.1) & (pos[..., 1] > 1.36) & (pos[..., 1] < 1.52) & (np.abs(pos[..., 0]) > 0.015) & (np.abs(pos[..., 0]) < 0.09)
    # tira pontos soltos, e fecha os buracos da faixa
    neon = ndimage.binary_opening(neon, iterations=1)
    neon = ndimage.binary_dilation(neon, iterations=1) & (peca == EQUIP)
    if time == "azul":
        olhos = ndimage.binary_dilation(olhos, iterations=1) & (peca == CORPO) & (w_cabeca > 0.5)
    else:
        olhos = ndimage.binary_dilation(ndimage.binary_opening(olhos, iterations=1), iterations=2) & (peca == CORPO) & (w_cabeca > 0.5)

    # ------------------------------------------------------------ neon novo
    novo = np.zeros_like(neon)
    frente = nrm[..., 2] > 0.45
    costas = nrm[..., 2] < -0.45
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    eq = peca == EQUIP
    info = {}

    def placa(mask):
        """Centro e tamanho de uma placa (texels da máscara)."""
        p = pos[mask]
        return np.median(p, 0), p.min(0), p.max(0)

    # joelheiras: uma faixa em pé no meio de cada uma (o vermelho já tem)
    for lado in (-1, 1):
        j = eq & frente & (w_perna > 0.5) & (y > 0.36) & (y < 0.62) & (x * lado > 0.05)
        if j.sum() < 400:
            continue
        c, lo, hi = placa(j)
        ja = (neon & j).sum()
        info[f"joelho{lado}"] = (j.sum(), ja, c.round(3), lo.round(3), hi.round(3))
        if ja < 80:
            alt = hi[1] - lo[1]
            novo |= j & (np.abs(x - c[0]) < 0.0065) & (y > lo[1] + alt * 0.28) & (y < hi[1] - alt * 0.2)

    # tornozeleiras: um anel fino em volta
    for lado in (-1, 1):
        t = eq & (y > 0.08) & (y < 0.30) & (x * lado > 0.05)
        if t.sum() < 400:
            continue
        c, lo, hi = placa(t)
        info[f"tornozelo{lado}"] = (t.sum(), c.round(3), lo.round(3), hi.round(3))
        novo |= t & (np.abs(y - (lo[1] + hi[1]) / 2) < 0.006) & (nrm[..., 1] > -0.5) & (np.abs(nrm[..., 1]) < 0.6)

    # costas: o LED fica por baixo da armadura e vaza pelas bordas da placa
    # (pedido do Jhon, 18/11; os riscos de neon em cima da placa incomodavam).
    # A luz cai no que está em volta da placa, mais forte colado nela.
    from scipy.spatial import cKDTree
    vaza = np.zeros(peca.shape, np.float32)
    if time == "azul":
        fontes = [eq & (np.abs(x) < 0.13) & (y > 1.02) & (y < 1.33) & (z < -0.10)]
    else:
        fontes = [eq & (w_ombro > 0.5) & (y > 1.22) & (x * lado > 0.1) for lado in (-1, 1)]
    for fonte in fontes:
        if fonte.sum() < 300:
            continue
        pf = pos[fonte]
        lo, hi = pf.min(0) - 0.05, pf.max(0) + 0.05
        cand = ok & ~eq & (pos >= lo).all(-1) & (pos <= hi).all(-1)
        if time != "azul":
            cand &= z < -0.02                       # o vermelho: só por trás, o da frente já tem faixa
        dist, _ = cKDTree(pf[::2]).query(pos[cand], distance_upper_bound=0.05)
        luz_ = np.clip(1 - (np.minimum(dist, 0.05) - 0.007) / 0.026, 0, 1) ** 1.7
        vaza[cand] = np.maximum(vaza[cand], luz_.astype(np.float32))
    info["vaza"] = int((vaza > 0.05).sum())

    # braceletes e ombreiras, por trás: uma faixa em cada placa que ainda não tem
    for lado in (-1, 1):
        for nome_, w_, (ya, yb) in (("bracelete", w_ante, (0.80, 1.12)), ("ombreira", w_ombro, (1.20, 1.50))):
            b = eq & costas & (w_ > 0.5) & (x * lado > 0.1) & (y > ya) & (y < yb)
            if b.sum() < 600:
                continue
            c, lo, hi = placa(b)
            ja = ((neon | novo) & b).sum()
            info[f"{nome_}_tras{lado}"] = (int(b.sum()), int(ja), c.round(3))
            if ja < 120:
                # ao longo do osso: a faixa acompanha a placa
                eixo = juntas[ossos.index(("Left" if lado == 1 else "Right") + ("Hand" if nome_ == "bracelete" else "Arm"))] - juntas[ossos.index(("Left" if lado == 1 else "Right") + ("ForeArm" if nome_ == "bracelete" else "Shoulder"))]
                eixo = eixo / np.linalg.norm(eixo)
                rel = pos - c
                ao_longo = rel @ eixo
                fora_ = np.linalg.norm(np.cross(rel, eixo), axis=-1)
                meio = np.percentile(ao_longo[b], [20, 80])
                novo |= b & (fora_ < 0.012) & (ao_longo > meio[0]) & (ao_longo < meio[1]) & (np.abs((rel - ao_longo[..., None] * eixo) @ np.array([0, 1.0, 0])) < 0.0065)

    olho_proprio = olhos if time == "azul" else np.zeros_like(olhos)      # acende na cor da própria textura
    emis_mask = (neon | (olhos & ~olho_proprio) | novo).astype(np.float32)
    # borda macia de 1 texel
    emis_soft = np.clip(ndimage.gaussian_filter(emis_mask, 0.8) * 1.6, 0, 1) * ok

    # ------------------------------------------------------------ cor
    lin = base ** 2.2
    eh_pelo = (peca == CORPO) & (V > 0.16) & ~((S < 0.16) & (V > 0.75))
    pelo = eh_pelo.astype(np.float32) * (1 - np.clip(w_maos * 1.2 - 0.3, 0, 1) * 0.6)
    pelo = ndimage.gaussian_filter(pelo, 1.0)

    # grade de cor: mais saturação e um pouco mais de luz
    hsv2 = hsv.copy()
    sat = np.where(peca == CORPO, T["sat"], np.where(peca == ROUPA, 1.08, 1.05))
    hsv2[..., 1] = np.clip(S * sat, 0, 1)
    val = np.where(peca == CORPO, T["val"], np.where(peca == EQUIP, T["equip_val"], T["roupa_val"]))
    hsv2[..., 2] = np.clip(V * val, 0, 1)
    if time == "azul":
        # o pelo puxa um fio para o anil
        hsv2[..., 0] = np.where((peca == CORPO) & (H > 200) & (H < 250), (H + 3) / 360, hsv2[..., 0])
    lin = hsv_to_rgb(hsv2) ** 2.2

    # fios de pelo, contínuos no corpo inteiro (ruído 3D esticado ao longo do pelo)
    P = pos[ok]
    dirs = {"tronco": np.array([0.0, -1.0, 0.12]), "cabeca": np.array([0.0, -0.45, -1.0])}
    for lado, nome_ in ((1, "braco_e"), (-1, "braco_d")):
        a = juntas[ossos.index("LeftArm" if lado == 1 else "RightArm")]
        b = juntas[ossos.index("LeftHand" if lado == 1 else "RightHand")]
        dirs[nome_] = (b - a).astype(np.float64)
    fio = np.zeros(len(P), np.float32); tufo = np.zeros(len(P), np.float32); soma = np.zeros(len(P), np.float32)
    for i, (nome_, d) in enumerate(dirs.items()):
        w = gw[nome_][ok]
        k = w > 0.01
        if not k.any():
            continue
        f = 0.6 * fios(P[k], d, 0.0058, 0.085, 11 + i) + 0.4 * fios(P[k], d, 0.0030, 0.045, 31 + i)
        t = fios(P[k], d, 0.022, 0.14, 51 + i)
        fio[k] += w[k] * f; tufo[k] += w[k] * t; soma[k] += w[k]
    fio = np.where(soma > 0, fio / np.maximum(soma, 1e-3), 0.5)
    tufo = np.where(soma > 0, tufo / np.maximum(soma, 1e-3), 0.5)
    # contraste: o ruído de valor fica perto de 0,5
    fio = np.clip((fio - 0.5) * 3.2, -1, 1)
    tufo = np.clip((tufo - 0.5) * 2.6, -1, 1)
    F = np.zeros(peca.shape, np.float32); F[ok] = fio
    Tu = np.zeros(peca.shape, np.float32); Tu[ok] = tufo

    ponta = suave(prot, 0.002, 0.022)                    # 0 na raiz, 1 na ponta do tufo
    # A juba e as costas são o que o jogador vê o tempo todo (câmera por trás), e
    # era onde a pelagem estava "muito simples" (Jhon, 18/11): ali o desenho é mais forte.
    juba = np.clip(np.maximum(w_cabeca, suave(y, 1.05, 1.3) * suave(-z, -0.05, 0.08)), 0, 1) * (peca == CORPO)
    mecha = np.zeros(peca.shape, np.float32)
    mecha[ok] = np.clip((fios(P, np.array([0.0, -1.0, -0.25]), 0.013, 0.16, 91) - 0.5) * 3.0, -1, 1)
    forca = 1.0 + 0.7 * juba
    # na juba os espinhos são compridos: o degradê vai da raiz à ponta do espinho inteiro
    ponta = ponta * (1 - juba) + suave(prot, 0.008, 0.075) * juba
    sombra = 1.0 + pelo[..., None] * forca[..., None] * (0.26 * F[..., None] + 0.14 * Tu[..., None] + 0.16 * mecha[..., None])
    # raiz escura, ponta clara
    # o vermelho já estava no ponto ("o ideal", Jhon): nele o degradê e a geada entram bem menos
    quanto = T.get("geada", 1.0)
    grad = (ponta * (0.34 + 0.34 * juba) - (0.10 + 0.14 * juba)) * (0.45 + 0.55 * quanto)
    sombra *= 1.0 + pelo[..., None] * grad[..., None]
    # faixa escura descendo a espinha, da nuca à cintura
    espinha = suave(0.11 - np.abs(x), 0.0, 0.08) * suave(-z, 0.0, 0.08) * suave(y, 0.85, 1.05) * (1 - ponta * 0.7)
    sombra *= 1.0 - 0.30 * (pelo * espinha)[..., None]
    # fio claro: um toque da cor mais clara nos fios altos (brilho de pelo)
    brilho = pelo * np.clip(F - 0.45, 0, 1) * 0.35
    lin = lin * sombra + brilho[..., None] * (lin * 0.6 + 0.02)
    # ponta do tufo puxa para a cor clara (pelo "geado")
    geada = (pelo * ponta * (0.16 + 0.34 * juba) * quanto)[..., None]
    clara = np.array(T["ponta"], np.float32) ** 2.2
    lin = lin * (1 - geada) + (lin * 0.35 + clara * 0.65) * geada

    # A órbita do olho: no vermelho o olho aceso fica dentro de uma sombra escura, e é isso que
    # dá a cara de bravo ("o sombreado no olho do vermelho tá melhor", Jhon, 18/11). No azul o
    # pelo em volta do olho é claro: aqui ele escurece, mais em cima (a sobrancelha).
    if time == "azul" and olhos.any():
        sombra_olho = np.zeros(peca.shape, np.float32)
        cabeca = (peca == CORPO) & (w_cabeca > 0.5) & (z > 0.12)
        for lado in (-1, 1):
            k = olhos & ((x - 0.013) * lado > 0)
            if k.sum() < 20:
                continue
            c = np.median(pos[k], 0)
            rel = pos - c
            # mais larga para os lados e para cima do que para baixo
            dist = np.sqrt((rel[..., 0] / 0.034) ** 2 + (np.where(rel[..., 1] > 0, rel[..., 1] / 0.026, rel[..., 1] / 0.016)) ** 2 + (rel[..., 2] / 0.05) ** 2)
            sombra_olho = np.maximum(sombra_olho, np.exp(-dist ** 2) * cabeca)
        sombra_olho *= ~olhos
        escuro = np.array([0.05, 0.07, 0.20], np.float32) ** 2.2
        lin = lin * (1 - 0.72 * sombra_olho[..., None]) + escuro * (0.5 * sombra_olho[..., None])

    # roupa e equipamento: grão fino, para não ficar chapado
    grao = np.zeros(peca.shape, np.float32)
    grao[ok] = ruido(P / 0.006, 77) - 0.5
    lin *= 1.0 + np.where(peca == CORPO, 0.0, 0.10)[..., None] * grao[..., None]

    # oclusão assada e um degradê de cima para baixo (volume pintado). O que o
    # Cycles assa tem um fio preto em toda aresta (as faces do Meshy são soltas e
    # o raio nasce em cima da vizinha): aqui a oclusão vira um valor por vértice
    # — a média do miolo dos triângulos em volta — e volta ao texel interpolada.
    oc = np.zeros(peca.shape, np.float32)
    for pi, p in enumerate(pecas):
        k = peca == pi
        nt = len(p["idx"])
        t = tri_id[k]; a_ = ao[k]; miolo = bary[k].min(1) > 0.12
        soma_m = np.bincount(t[miolo], a_[miolo], nt); cont_m = np.bincount(t[miolo], minlength=nt)
        soma_t = np.bincount(t, a_, nt); cont_t = np.bincount(t, minlength=nt)
        por_tri = np.where(cont_m > 0, soma_m / np.maximum(cont_m, 1), np.where(cont_t > 0, soma_t / np.maximum(cont_t, 1), 1.0))
        chave = np.round(p["pos"] * 5000).astype(np.int64)
        _, inv = np.unique(chave, axis=0, return_inverse=True); inv = inv.reshape(-1)
        nv = inv.max() + 1
        acc = np.zeros(nv); cnt = np.zeros(nv)
        for c in range(3):
            np.add.at(acc, inv[p["idx"][:, c]], por_tri); np.add.at(cnt, inv[p["idx"][:, c]], 1)
        por_vert = (acc / np.maximum(cnt, 1))[inv].astype(np.float32)
        w = np.clip(bary[k], 0, 1); w /= w.sum(1, keepdims=True)
        oc[k] = (por_vert[p["idx"][t]] * w).sum(1)
    oc = np.clip(oc, 0, 1) ** 1.8
    lin *= (0.42 + 0.58 * oc)[..., None]
    lin *= (0.94 + 0.10 * nrm[..., 1])[..., None]

    # neon: o miolo quase branco na cor da luz
    cor_neon = np.array(T["neon"], np.float32)
    nucleo = (cor_neon * 0.55 + 0.45) ** 2.2
    lin = lin * (1 - emis_soft[..., None]) + nucleo * emis_soft[..., None]
    # e um halo fraco da cor na placa em volta
    halo = np.clip(ndimage.gaussian_filter(emis_mask, 5.0) * 2.2, 0, 1) * ok * (1 - emis_soft)
    lin += halo[..., None] * (cor_neon ** 2.2) * 0.22

    nova = np.clip(lin, 0, 1) ** (1 / 2.2)
    lin += vaza[..., None] * (cor_neon ** 2.2) * 0.55
    nova = np.clip(lin, 0, 1) ** (1 / 2.2)
    olho_soft = np.clip(ndimage.gaussian_filter(olho_proprio.astype(np.float32), 0.7) * 1.5, 0, 1) * ok
    emis = np.clip(olho_soft[..., None] * np.clip(base * 1.15, 0, 1) + emis_soft[..., None] * (cor_neon * 0.8 + 0.2) + halo[..., None] * cor_neon * 0.10
                   + vaza[..., None] * cor_neon * 0.75, 0, 1)

    # margem: cada texel vazio copia o vizinho válido mais próximo (não vaza no mip)
    _, (iy, ix) = ndimage.distance_transform_edt(~ok, return_indices=True)
    nova = nova[iy, ix]; emis = emis[iy, ix]

    # ORM: a oclusão entra no canal R; o pelo fica um pouco mais fosco, o neon liso
    orm2 = orm.copy()
    orm2[..., 0] = np.clip(0.35 + 0.65 * oc, 0, 1)[iy, ix]
    orm2[..., 1] = np.clip(orm[..., 1] + pelo * 0.10 - emis_soft * 0.3, 0.05, 1)

    # ------------------------------------------------------------ cor de vértice
    # R: quanto o vértice balança (pontas dos tufos; mãos, pés e focinho não)
    # G: pelo (para o brilho de contorno);  B: neon (para o pulso)
    cores = []
    for pi, p in enumerate(pecas):
        v = p["pos"]
        fora = prot_v[pi][:, 0]
        ids = lambda lista: [ossos.index(n) for n in lista]
        wm = np.where(np.isin(p["joints"], ids(["LeftHand", "RightHand", "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase"])), p["weights"], 0).sum(1)
        wc = np.where(np.isin(p["joints"], ids(GRUPOS["cabeca"])), p["weights"], 0).sum(1)
        r = suave(fora, 0.008, 0.030)
        r *= 1 - np.clip(wm * 1.5, 0, 1)
        # rosto: do focinho para a frente não mexe (dentes, olhos)
        rosto = wc * suave(v[:, 2], 0.03, 0.10) * (1 - suave(v[:, 1], 1.50, 1.56))
        r *= 1 - np.clip(rosto * 1.4, 0, 1)
        if pi == EQUIP:
            r *= 0.0
        elif pi == ROUPA:
            r *= 0.6
        gq = np.full(len(v), 1.0 if pi == CORPO else 0.0, np.float32) * (1 - np.clip(wm, 0, 1) * 0.5)
        c = np.stack([r, gq, np.full(len(v), 0.5), np.ones(len(v))], 1)      # B 0,5: tom neutro (veja os tufos)
        cores.append(np.clip(c * 255 + 0.5, 0, 255).astype(np.uint8))

    # ------------------------------------------------------------ tufos de pelo
    from scipy.spatial import cKDTree as _KD
    cob = [pp["pos"] for i_, pp in enumerate(pecas) if i_ != CORPO]
    # mais denso que os vértices: os pontos dos texels da roupa e do equipamento
    cob.append(pos[(peca != CORPO) & ok][::6])
    juba_tex = np.clip(np.maximum(w_cabeca, suave(y, 1.05, 1.3) * suave(-z, -0.05, 0.08)), 0, 1)
    tufos = gerar_pelos(time, pecas, CORPO, eh_pelo.astype(np.float32), juba_tex, _KD(np.concatenate(cob, 0)), juntas, ossos)

    # ------------------------------------------------------------ grava
    def jpg(a, q=93):
        b = io.BytesIO(); Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)).save(b, "JPEG", quality=q, subsampling=0); return b.getvalue()
    def png(a):
        b = io.BytesIO(); Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)).save(b, "PNG", optimize=True); return b.getvalue()

    if out_glb:
        g.set_image(0, jpg(nova), "image/jpeg")
        g.set_image(1, jpg(orm2, 90), "image/jpeg")
        d = g.doc
        if len(d["images"]) < 4:
            tex = g.add_image("lobo_emissao", jpg(emis, 92), "image/jpeg")
        else:
            g.set_image(3, jpg(emis, 92), "image/jpeg"); tex = 3
        mat = d["materials"][0]
        mat.setdefault("extras", {})["polido"] = 1
        mat["emissiveTexture"] = {"index": tex}
        mat["emissiveFactor"] = [1.0, 1.0, 1.0]
        mat["pbrMetallicRoughness"].setdefault("metallicRoughnessTexture", {"index": 1})
        mat["occlusionTexture"] = {"index": 1}
        for pi, p in enumerate(pecas):
            if "COLOR_0" in p["prim"]["attributes"]:
                acc = d["accessors"][p["prim"]["attributes"]["COLOR_0"]]
                g.views[acc["bufferView"]] = cores[pi].tobytes()
            else:
                p["prim"]["attributes"]["COLOR_0"] = g.add_accessor(cores[pi], "VEC4", normalized=True)
        # a malha dos tufos: um nó novo ("pelo") ao lado do corpo, com a mesma pele e o mesmo material
        nomes_nos = [n.get("name") for n in d["nodes"]]
        if len(tufos["pos"]) == 0:
            g.save(out_glb)
            for k, v in info.items():
                print("  ", k, v)
            print(f"{time}: sem tufos; neon {int(neon.sum())} texels, olhos {int(olhos.sum())}, novo {int(novo.sum())}")
            return
        attrs = dict(POSITION=g.add_accessor(tufos["pos"], "VEC3"), NORMAL=g.add_accessor(tufos["nrm"], "VEC3"),
                     TEXCOORD_0=g.add_accessor(tufos["uv"], "VEC2"), TANGENT=g.add_accessor(tufos["tg"], "VEC4"),
                     JOINTS_0=g.add_accessor(tufos["joints"], "VEC4"), WEIGHTS_0=g.add_accessor(tufos["weights"], "VEC4"),
                     COLOR_0=g.add_accessor(tufos["cor"], "VEC4", normalized=True))
        accp = d["accessors"][attrs["POSITION"]]
        accp["min"] = tufos["pos"].min(0).tolist(); accp["max"] = tufos["pos"].max(0).tolist()
        iv = g.add_view(tufos["idx"].tobytes(), 34963)
        d["accessors"].append({"bufferView": iv, "componentType": 5125, "count": int(len(tufos["idx"])), "type": "SCALAR"})
        prim = {"attributes": attrs, "indices": len(d["accessors"]) - 1, "material": 0, "mode": 4}
        if "pelo" in nomes_nos:
            d["meshes"][d["nodes"][nomes_nos.index("pelo")]["mesh"]]["primitives"] = [prim]
        else:
            d["meshes"].append({"name": "pelo", "primitives": [prim]})
            corpo_no = nomes_nos.index("corpo")
            d["nodes"].append({"mesh": len(d["meshes"]) - 1, "name": "pelo", "skin": d["nodes"][corpo_no].get("skin", 0)})
            novo_no = len(d["nodes"]) - 1
            pai = next((n for n in d["nodes"] if corpo_no in n.get("children", [])), None)
            if pai is not None:
                pai["children"].append(novo_no)
            else:
                d["scenes"][0]["nodes"].append(novo_no)
        g.save(out_glb)

    for k, v in info.items():
        print("  ", k, v)
    print(f"{time}: neon {int(neon.sum())} texels, olhos {int(olhos.sum())}, novo {int(novo.sum())}; balanço médio {np.mean([c[:, 0].mean() for c in cores]) / 255:.3f}")

    if previa:
        from previa import folha, vista
        maps = dict(peca=peca, pos=pos, nrm=nrm)
        n8 = (nova * 255).astype(np.uint8); e8 = (emis * 255).astype(np.uint8)
        folha(maps, n8, e8, os.path.join(previa, f"p_{time}.jpg"))
        m = ok
        back = vista(pos[m], nrm[m], n8[m], e8[m], np.pi, 720, zoom=(-0.5, 0.5, 0.8, 1.7))
        back.save(os.path.join(previa, f"p_{time}_costas.jpg"), quality=90)
        # o balanço, em falsa cor
        sw = np.zeros(peca.shape + (3,), np.uint8)
        swv = interpolar(pecas, peca, tri_id, bary, valores=[c[:, :1].astype(np.float32) / 255 for c in cores])[..., 0]
        sw[..., 0] = (swv * 255).astype(np.uint8); sw[..., 2] = 60
        vista(pos[m], nrm[m], sw[m], np.zeros_like(sw[m]), 0.5, 720).save(os.path.join(previa, f"p_{time}_balanco.jpg"), quality=88)
        Image.fromarray(n8).resize((768, 768)).save(os.path.join(previa, f"t_{time}_base.jpg"), quality=88)
        Image.fromarray(e8).resize((768, 768)).save(os.path.join(previa, f"t_{time}_emis.jpg"), quality=88)


if __name__ == "__main__":
    a = sys.argv[1:]
    previa = a[a.index("--previa") + 1] if "--previa" in a else None
    polir(a[0], a[1], a[2], a[3] if a[3] != "-" else None, previa)
