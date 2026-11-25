"""A malha do lobo pronta para o GLB, a partir do modelo do Meshy (FBX com UV e textura).

O modelo vem sem esqueleto. Aqui ele é levado ao espaço do lobo do jogo (mesma altura, pés
no chão, mesma frente), ganha as tangentes do Blender (MikkTSpace, as que o mapa de normal
do Meshy espera), os pesos do lobo do jogo (do ponto mais perto da malha antiga) e a cadeia
do rabo, que o lobo do jogo não tinha.

uso: malha.py <lobo_do_jogo_original.glb> <pasta_do_modelo> <saida.npz>
Precisa do Blender como módulo (pip install bpy), além de numpy e scipy."""
import sys, os, glob
import numpy as np
import bpy
from scipy.spatial import cKDTree
from scipy.sparse import coo_matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from atlas import carregar

RABO = 5              # juntas do rabo, da raiz à ponta (a última é só a ponta)
TOPO_DO_RABO = 0.93   # altura (m) onde a cauda nasce, medida no modelo: abaixo é cauda, acima é a franja das costas


def para_blender(P):      # nosso (x, y para cima, z para a frente) -> Blender (x, -z, y)
    return np.stack([P[:, 0], -P[:, 2], P[:, 1]], 1)


def do_blender(P):
    return np.stack([P[:, 0], P[:, 2], -P[:, 1]], 1)


def ler_modelo(pasta):
    """O FBX do Meshy: posições (soldadas), e por canto de triângulo o vértice, a UV e a normal."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=glob.glob(os.path.join(pasta, "*.fbx"))[0])
    o = next(o for o in bpy.data.objects if o.type == 'MESH'); me = o.data
    assert all(p.loop_total == 3 for p in me.polygons), "o modelo tem de vir em triângulos"
    M = np.array(o.matrix_world)
    V = np.zeros(len(me.vertices) * 3, np.float32); me.vertices.foreach_get("co", V)
    V = do_blender(V.reshape(-1, 3) @ M[:3, :3].T + M[:3, 3]).astype(np.float64)
    L = np.zeros(len(me.loops), np.int32); me.loops.foreach_get("vertex_index", L)
    UV = np.zeros(len(me.loops) * 2, np.float32); me.uv_layers[0].data.foreach_get("uv", UV)
    N = np.zeros(len(me.loops) * 3, np.float32); me.corner_normals.foreach_get("vector", N)
    N = do_blender(N.reshape(-1, 3) @ np.linalg.inv(M[:3, :3])); N /= np.linalg.norm(N, axis=1, keepdims=True)
    return V, L.reshape(-1, 3).astype(np.int64), UV.reshape(-1, 2), N


def alinhar(V, P):
    """Ao espaço do lobo do jogo: a mesma altura, os pés no chão, o meio no meio, e para a
    frente e para trás onde as duas malhas mais se encostam."""
    s = (P[:, 1].max() - P[:, 1].min()) / (V[:, 1].max() - V[:, 1].min())
    Q = V * s
    Q[:, 1] -= Q[:, 1].min()
    Q[:, 0] -= (Q[:, 0].max() + Q[:, 0].min()) / 2
    Q[:, 2] -= Q[:, 2].mean() - P[:, 2].mean()
    arv = cKDTree(P); dz = 0.0
    for _ in range(30):
        d, i = arv.query(Q + [0, 0, dz]); k = d < np.percentile(d, 80)
        dz += (P[i[k], 2] - (Q[k, 2] + dz)).mean()
    Q[:, 2] += dz
    return Q, s


def soltar_rabo(V, F, UV, N, pasta):
    """O rabo vira uma peça solta, presa só na raiz.

    No modelo do Meshy o rabo é fundido na calça por uns 30 cm (as superfícies que se
    encostam viram uma só). Com ossos no rabo, os triângulos da emenda esticavam por cima
    da calça a cada balanço (Jhon, 25/11: "a bunda do lobo continua se movendo e com
    glitches ao movimentar o rabo; somente o rabo deve mexer"). Aqui a malha é cortada na
    divisa do pelo do rabo com o resto, e cada lado ganha uma tampa: a do corpo na cor da
    calça, a do rabo na do pelo escuro. Devolve a malha nova, quem é rabo, e para cada
    vértice do rabo que nasceu no corte, o gêmeo que ficou no corpo."""
    from PIL import Image
    from matplotlib.colors import rgb_to_hsv
    from scipy.sparse.csgraph import connected_components
    Image.MAX_IMAGE_PIXELS = None
    def abrir(fim, modo):
        f = [x for x in glob.glob(os.path.join(pasta, "*.png")) if x.endswith(fim)]
        assert len(f) == 1, (fim, f)
        return np.asarray(Image.open(f[0]).convert(modo)).astype(np.float32) / 255
    base = abrir("_tri.png", "RGB"); metal = abrir("_tri_metallic.png", "L")
    T = base.shape[0]
    # A cor de cada triângulo: três amostras entre o centro e os cantos (longe da borda da ilha).
    uvc = UV.reshape(-1, 3, 2)
    am = uvc.mean(1, keepdims=True) * 0.6 + uvc * 0.4
    px = np.clip((am[..., 0] * T).astype(int), 0, T - 1); py = np.clip(((1 - am[..., 1]) * T).astype(int), 0, T - 1)
    hsv = rgb_to_hsv(base[py, px])
    H = np.median(hsv[..., 0], 1) * 360; S = np.median(hsv[..., 1], 1); Vv = np.median(hsv[..., 2], 1); mt = np.median(metal[py, px], 1)
    # O pelo é castanho (matiz 23 a 38, até no escuro) ou claro; a calça é fria ou sem cor.
    quente = (H > 14) & (H < 52) & (S > 0.12)
    couro = (H > 4) & (H < 32) & (S > 0.40) & (Vv < 0.5) & (mt < 0.35)
    pelo = (mt < 0.35) & ~couro & (quente | (Vv > 0.42))
    pano = (mt < 0.35) & ~pelo & ~couro
    c = V[F].mean(1)
    # Até 0,93 m: dali para cima o pelo alarga de 16 para 30 cm, e já é a franja das costas
    # embaixo do cinto, não a cauda (Jhon, 25/11: "ainda pegando resquícios das costas").
    zona = (c[:, 1] > 0.25) & (c[:, 1] < TOPO_DO_RABO) & (np.abs(c[:, 0]) < 0.26) & (c[:, 2] < -0.08)
    cand = zona & pelo
    # O rabo: o pelo ligado (aresta com aresta) ao que está bem para trás do corpo.
    n = len(V)
    E = np.concatenate([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]]); E.sort(1)
    chave = E[:, 0] * n + E[:, 1]; tri = np.tile(np.arange(len(F)), 3)
    o = np.argsort(chave, kind="stable"); ks = chave[o]; ts = tri[o]
    igual = ks[1:] == ks[:-1]
    ta, tb = ts[:-1][igual], ts[1:][igual]
    k = cand[ta] & cand[tb]
    _, rotulo = connected_components(coo_matrix((np.ones(k.sum()), (ta[k], tb[k])), shape=(len(F), len(F))), directed=False)
    semente = cand & (c[:, 2] < -0.30)
    rabo_t = cand & np.isin(rotulo, np.unique(rotulo[semente]))
    # Um triângulo solto de outra cor no meio do rabo (uma lasca escura sem o castanho)
    # é rabo também: o corpo é o maior pedaço do resto; o que sobrar ilhado vai com o rabo.
    k = ~rabo_t[ta] & ~rabo_t[tb]
    _, resto = connected_components(coo_matrix((np.ones(k.sum()), (ta[k], tb[k])), shape=(len(F), len(F))), directed=False)
    maior = np.bincount(resto[~rabo_t]).argmax()
    rabo_t |= resto != maior
    assert 300 < rabo_t.sum() < 1500 and np.abs(V[F[rabo_t]][..., 0]).max() < 0.3, "o rabo achado não parece um rabo"

    # O corte: o vértice que o rabo divide com o resto ganha um gêmeo, que fica com o rabo.
    de_rabo = np.zeros(n, bool); de_rabo[F[rabo_t]] = True
    de_corpo = np.zeros(n, bool); de_corpo[F[~rabo_t]] = True
    comuns = np.nonzero(de_rabo & de_corpo)[0]
    novo = np.full(n, -1); novo[comuns] = n + np.arange(len(comuns))
    V = np.concatenate([V, V[comuns]])
    F = F.copy(); Fr = F[rabo_t]; troca = novo[Fr] >= 0; Fr[troca] = novo[Fr][troca]; F[rabo_t] = Fr
    gemeo = np.full(len(V), -1); gemeo[n:] = comuns

    # A borda do rabo: arestas (no sentido do triângulo do rabo) sem volta dentro do rabo.
    Fr = F[rabo_t]
    idas = np.concatenate([Fr[:, [0, 1]], Fr[:, [1, 2]], Fr[:, [2, 0]]])
    # (contadas sem sentido: o Meshy tem triângulo virado, e a aresta de dois triângulos do
    # rabo com o mesmo sentido não é borda)
    from collections import Counter
    vezes = Counter((min(a, b), max(a, b)) for a, b in idas.tolist())
    borda = [(a, b) for a, b in idas.tolist() if vezes[(min(a, b), max(a, b))] == 1]
    seguinte = {}
    for a, b in borda:
        seguinte.setdefault(a, []).append(b)
    voltas = []
    while seguinte:
        inicio = next(iter(seguinte)); volta = [inicio]; atual = inicio
        while True:
            prox = seguinte[atual].pop()
            if not seguinte[atual]: del seguinte[atual]
            if prox == inicio: break
            volta.append(prox); atual = prox
            if atual not in seguinte: break
        voltas.append(volta)

    # As tampas: um leque do meio de cada volta. Cada uma numa cor só (um ponto da textura):
    # a do corpo, o pano da calça mais perto; a do rabo, um pelo escuro dele.
    area_uv = np.abs(np.cross(uvc[:, 1] - uvc[:, 0], uvc[:, 2] - uvc[:, 0]))
    def ponto(mascara, perto):
        dd = np.linalg.norm(c - perto, axis=1)
        k = np.nonzero(mascara & (dd < np.sort(dd[mascara])[min(40, mascara.sum() - 1)] + 1e-9))[0]
        return uvc[k[np.argmax(area_uv[k])]].mean(0)
    Vn, Fn, UVn, Nn, rabo_novo = [], [], [], [], []
    prox_v = len(V)
    for volta in voltas:
        if len(volta) < 3: continue
        P = V[volta]; meio = P.mean(0)
        uv_pano = ponto(~rabo_t & pano, meio)
        uv_pelo = ponto(rabo_t & (Vv < np.percentile(Vv[rabo_t], 35)), meio)
        cr, cc = prox_v, prox_v + 1; prox_v += 2
        Vn += [meio, meio]
        for i in range(len(volta)):
            a, b = volta[i], volta[(i + 1) % len(volta)]
            for tri3, uv1, e_rabo in (((b, a, cr), uv_pelo, True), ((gemeo[a], gemeo[b], cc), uv_pano, False)):
                # borda que já era buraco no modelo (sem corpo do outro lado): só a tampa do rabo
                if min(tri3) < 0: continue
                p0, p1, p2 = (V[tri3[0]], V[tri3[1]], meio)
                nrm = np.cross(p1 - p0, p2 - p0); comp = np.linalg.norm(nrm)
                if comp < 1e-12: continue
                Fn.append(tri3); UVn += [uv1] * 3; Nn += [nrm / comp] * 3; rabo_novo.append(e_rabo)
        print("   tampa: volta de %d arestas em %s" % (len(volta), meio.round(3)))
    assert Fn, "o rabo não tem borda para tampar"
    V = np.concatenate([V, np.array(Vn)])
    gemeo = np.concatenate([gemeo, np.full(len(Vn), -1)])
    F = np.concatenate([F, np.array(Fn, np.int64)])
    UV = np.concatenate([UV, np.array(UVn, np.float32)]); N = np.concatenate([N, np.array(Nn, np.float32)])
    rabo_t = np.concatenate([rabo_t, np.array(rabo_novo)])
    rabo_v = np.zeros(len(V), bool); rabo_v[F[rabo_t]] = True
    assert not (rabo_v & np.isin(np.arange(len(V)), F[~rabo_t])).any(), "sobrou vértice dividido entre o rabo e o corpo"
    print("rabo solto: %d triângulos, %d vértices, %d nasceram no corte" % (rabo_t.sum(), rabo_v.sum(), len(comuns)))
    return V, F, UV, N, rabo_v, gemeo


def tangentes(V, F, UV, N):
    """As do Blender (MikkTSpace), com as normais do arquivo."""
    nl = len(F) * 3
    me = bpy.data.meshes.new("m")
    me.vertices.add(len(V)); me.vertices.foreach_set("co", para_blender(V).astype(np.float32).ravel())
    me.loops.add(nl); me.loops.foreach_set("vertex_index", F.astype(np.int32).ravel())
    me.polygons.add(len(F)); me.polygons.foreach_set("loop_start", np.arange(0, nl, 3, dtype=np.int32)); me.polygons.foreach_set("loop_total", np.full(len(F), 3, np.int32))
    me.update()
    assert not me.validate(), "a malha mudou na validação: os cantos não batem mais"
    me.uv_layers.new(name="UVMap").data.foreach_set("uv", UV.astype(np.float32).ravel())
    me.polygons.foreach_set("use_smooth", np.ones(len(F), bool))
    me.normals_split_custom_set([tuple(n) for n in para_blender(N)])
    me.calc_tangents(uvmap="UVMap")
    T = np.zeros(nl * 3, np.float32); me.loops.foreach_get("tangent", T)
    S = np.zeros(nl, np.float32); me.loops.foreach_get("bitangent_sign", S)
    return do_blender(T.reshape(-1, 3)), S


def pesos(V, F, pecas, ossos, rabo, gemeo):
    """Os do lobo do jogo, do ponto mais perto da malha dele; e o rabo (a peça solta por
    soltar_rabo), que ele não tinha, nos ossos novos."""
    nb = len(ossos)
    rng = np.random.default_rng(5)
    pts, pw = [], []
    for p in pecas:
        P = p["pos"].astype(np.float64); I = p["idx"]
        W = np.zeros((len(P), nb))
        for k in range(4):
            np.add.at(W, (np.arange(len(W)), p["joints"][:, k]), p["weights"][:, k])
        W /= W.sum(1, keepdims=True)
        a = np.linalg.norm(np.cross(P[I[:, 1]] - P[I[:, 0]], P[I[:, 2]] - P[I[:, 0]]), axis=1)
        n = int(a.sum() * 250000)                                  # 25 pontos por cm²
        i = rng.choice(len(I), n, p=a / a.sum()); u = rng.random((n, 2)); k = u.sum(1) > 1; u[k] = 1 - u[k]
        b = np.stack([1 - u.sum(1), u[:, 0], u[:, 1]], 1)
        pts.append((P[I[i]] * b[:, :, None]).sum(1)); pw.append((W[I[i]] * b[:, :, None]).sum(1))
    pts = np.concatenate(pts); pw = np.concatenate(pw)
    dist, ix = cKDTree(pts).query(V, k=8)
    peso = 1.0 / (dist + 0.004) ** 2
    W = (pw[ix] * peso[:, :, None]).sum(1) / peso.sum(1, keepdims=True)
    print("vértice novo -> lobo do jogo: mediana %.1f cm, p95 %.1f cm" % (np.median(dist[:, 0]) * 100, np.percentile(dist[:, 0], 95) * 100))

    o = {n: i for i, n in enumerate(ossos)}
    x, y, z = V[:, 0], V[:, 1], V[:, 2]
    viz = coo_matrix((np.ones(len(F) * 6), (np.concatenate([F[:, 0], F[:, 1], F[:, 2], F[:, 1], F[:, 2], F[:, 0]]),
                                            np.concatenate([F[:, 1], F[:, 2], F[:, 0], F[:, 0], F[:, 1], F[:, 2]]))), shape=(len(V), len(V))).tocsr()
    viz.data[:] = 1
    W0 = W.copy()                                                 # o que o corpo daria a cada vértice
    # A cadeia: RABO juntas pela linha do meio, da raiz à ponta, a distâncias iguais.
    R = V[rabo]
    topo = R[R[:, 1] > np.percentile(R[:, 1], 88)].mean(0); ponta = R[np.argsort(R[:, 1])[:6]].mean(0)
    corda = ponta - topo; tc = ((R - topo) @ corda) / (corda @ corda)
    cortes = np.linspace(tc.min(), tc.max(), 10)
    meio = np.array([R[(tc >= a_) & (tc <= b_)].mean(0) for a_, b_ in zip(cortes[:-1], cortes[1:])])
    meio[:, 0] = 0.0                                              # o rabo nasce no meio do corpo
    meio = np.concatenate([[meio[0] + (meio[0] - meio[1]) * 0.5], meio, [ponta * [0, 1, 1]]])
    comp = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(meio, axis=0), axis=1))])
    juntas = np.stack([np.interp(np.linspace(0, comp[-1], RABO), comp, meio[:, k]) for k in range(3)], 1)
    print("rabo: %d vértices, %.2f m, juntas\n" % (rabo.sum(), comp[-1]), juntas.round(3))

    # onde cada vértice do rabo está ao longo da cadeia (0 a RABO-1)
    seg = juntas[1:] - juntas[:-1]; sl = (seg ** 2).sum(1)
    melhor = np.full(len(R), 1e9); u = np.zeros(len(R))
    for i in range(RABO - 1):
        f = np.clip(((R - juntas[i]) @ seg[i]) / sl[i], 0, 1)
        dd = np.linalg.norm(R - (juntas[i] + f[:, None] * seg[i]), axis=1)
        k = dd < melhor; melhor[k] = dd[k]; u[k] = i + f[k]
    # o osso i manda no trecho [i, i+1]; a troca de um para o outro é suave em volta da junta
    W = np.concatenate([W, np.zeros((len(V), RABO))], 1)
    ri = np.nonzero(rabo)[0]
    W[ri] = 0
    for i in range(RABO - 1):
        c = i + 0.5
        W[ri, nb + i] = np.clip(1 - np.abs(u - c), 0, 1) if 0 < i < RABO - 2 else (np.clip(1 - (u - c), 0, 1) if i == 0 else np.clip(1 - (c - u), 0, 1))
    W[ri] /= W[ri].sum(1, keepdims=True)

    # alisa pelos vizinhos da malha nova (o mais-perto dá degraus), sem mexer no rabo
    grau = np.maximum(np.asarray(viz.sum(1)).ravel(), 1)          # (o meio de uma tampa sem uso não tem vizinho)
    for _ in range(4):
        W2 = 0.5 * W + 0.5 * (viz @ W) / grau[:, None]
        W2[rabo] = W[rabo]
        W = W2
    # Nenhum peso de rabo fora do rabo (a peça é solta: o alisado não tem por onde levar).
    assert W[~rabo, nb:].max() < 1e-9
    # A raiz é o único lugar onde o rabo segue o corpo: nos primeiros 6 cm da cadeia ele
    # passa dos ossos dele para os pesos que o corpo tem ali (os do gêmeo do outro lado do
    # corte, para a emenda da raiz não abrir quando o tronco dobra).
    raiz = np.clip(1 - u / 0.35, 0, 1) ** 1.5
    corpo = np.concatenate([W0, np.zeros((len(V), RABO))], 1)[ri]
    tem = gemeo[ri] >= 0
    corpo[tem] = W[gemeo[ri][tem]]
    W[ri] = W[ri] * (1 - raiz)[:, None] + corpo * raiz[:, None]
    # quatro ossos por vértice
    ordem = np.argsort(-W, axis=1)[:, :4]
    Wq = np.take_along_axis(W, ordem, 1)
    Wq[Wq < 0.01] = 0
    Wq /= Wq.sum(1, keepdims=True)
    ordem[Wq == 0] = 0
    return ordem, Wq, juntas, rabo, ossos + [f"rabo{i + 1}" for i in range(RABO)]


def fazer(src, pasta, saida):
    g, pecas, ossos, _ = carregar(src)
    assert not any(n.startswith("rabo") for n in ossos), "use o lobo original (sem rabo) como esqueleto"
    P = np.concatenate([p["pos"] for p in pecas]).astype(np.float64)
    V, F, UV, N = ler_modelo(pasta)
    V, escala = alinhar(V, P)
    print("modelo: %d posições, %d triângulos; escala %.4f; caixa %s a %s" % (len(V), len(F), escala, V.min(0).round(3), V.max(0).round(3)))
    V, F, UV, N, rabo, gemeo = soltar_rabo(V, F, UV, N, pasta)
    T, S = tangentes(V, F, UV, N)
    ordem, Wq, juntas, rabo, nomes = pesos(V, F, pecas, ossos, rabo, gemeo)

    # um vértice do arquivo por canto diferente (posição, UV, normal e tangente)
    L = F.ravel()
    uvg = np.stack([UV[:, 0], 1.0 - UV[:, 1]], 1)                  # glTF: v para baixo
    chave = np.concatenate([L[:, None].astype(np.float64), np.round(uvg, 6), np.round(N, 4), np.round(T, 3), S[:, None]], 1)
    _, prim, inv = np.unique(chave, axis=0, return_index=True, return_inverse=True)
    tn = T[prim]; nn = N[prim]
    tn = tn - nn * (tn * nn).sum(1, keepdims=True); tn /= np.maximum(np.linalg.norm(tn, axis=1, keepdims=True), 1e-9)
    print("vértices no arquivo:", len(prim))
    np.savez(saida, pos=V[L[prim]].astype(np.float32), nrm=nn.astype(np.float32), tg=np.concatenate([tn, S[prim][:, None]], 1).astype(np.float32),
             uv=uvg[prim].astype(np.float32), joints=ordem[L[prim]].astype(np.uint16), weights=Wq[L[prim]].astype(np.float32),
             idx=inv.ravel().reshape(-1, 3).astype(np.uint32), de=L[prim].astype(np.int32),
             rabo_j=juntas.astype(np.float32), ossos=np.array(nomes))


if __name__ == "__main__":
    fazer(*sys.argv[1:4])
