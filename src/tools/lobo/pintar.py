"""As texturas do lobo a partir das do modelo do Meshy: a pelagem e os detalhes ficam como
vieram (pedido do Jhon, 25/11: "quero algo bem próximo daquilo"); o que muda é o que o jogo
pede: olhos inteiros de luz, neon na cor do time, a roupa na cor do personagem, o lobo do
Barut mais escuro, e as máscaras na cor de vértice (o pelo que balança).

Tudo é decidido pela posição do texel no corpo (o atlas do Meshy é picado em centenas de
ilhas: vizinho na imagem não é vizinho no lobo)."""
import os, glob, sys, numpy as np
from PIL import Image
from scipy import ndimage as ndi
from scipy.spatial import cKDTree
from matplotlib.colors import rgb_to_hsv, hsv_to_rgb
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from atlas import rasterizar, interpolar

TAM = 4096
MENOR = 2048

# neon/olho: cor da luz;  roupa: (matiz em graus, saturação, valor mediano) — a do personagem;
# pelo: (ganho, gama, tinta, saturação) sobre a pelagem da referência.
TIMES = {
    "azul": dict(neon=(0.20, 0.80, 1.0), olho=(0.25, 0.82, 1.0), miolo=(0.86, 0.98, 1.0),
                 roupa=(212, 0.34, 0.20), pelo=(1.0, 1.0, (1.0, 1.0, 1.0), 1.0)),
    "vermelho": dict(neon=(1.0, 0.16, 0.10), olho=(1.0, 0.25, 0.08), miolo=(1.0, 0.90, 0.72),
                     roupa=(357, 0.72, 0.22), pelo=(0.50, 1.30, (1.04, 0.97, 0.92), 0.85)),
}


def suave(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def abrir(pasta, fim, modo="RGB"):
    f = [x for x in glob.glob(os.path.join(pasta, "*.png")) if x.endswith(fim)]
    assert len(f) == 1, (fim, f)
    return np.asarray(Image.open(f[0]).convert(modo))


def mapas(m, cache):
    """Por texel: dentro de algum triângulo, posição e normal no espaço do lobo."""
    if cache and os.path.exists(cache):
        c = np.load(cache)
        return c["ok"], c["pos"], c["nrm"], c["tri"]
    pecas = [dict(uv=m["uv"], idx=m["idx"].astype(np.int64), pos=m["pos"], nrm=m["nrm"])]
    peca, tri_id, bary = rasterizar(pecas, TAM)
    ok = peca == 0
    pos = interpolar(pecas, peca, tri_id, bary, "pos"); nrm = interpolar(pecas, peca, tri_id, bary, "nrm")
    if cache:
        np.savez(cache, ok=ok, pos=pos, nrm=nrm, tri=tri_id)
    return ok, pos, nrm, tri_id


def classes(base, metal, ok, pos):
    """O que é cada texel, pela cor da referência e pelo lugar no corpo."""
    hsv = rgb_to_hsv(base); H = hsv[..., 0] * 360; S = hsv[..., 1]; V = hsv[..., 2]
    x, y, z = pos[..., 0], pos[..., 1], pos[..., 2]
    azul = suave(S, 0.22, 0.40) * suave(H, 180, 192) * (1 - suave(H, 232, 245)) * suave(V, 0.10, 0.20) * ok
    cabeca = (y > 1.40) & (z > 0.10) & (np.abs(x - 0.02) < 0.12)
    c = {}
    c["olho0"] = (azul > 0.5) & cabeca
    c["neon"] = azul * ~cabeca
    verm = ((H < 14) | (H > 346)) & (S > 0.50) & (V > 0.22) & ok
    c["boca"] = verm & (y > 1.30) & (y < 1.45) & (z > 0.08)
    c["orelha"] = verm & (y > 1.47)
    # O equipamento é metal; mas o mapa de metal do Meshy também acende em lascas de pelo
    # (cabeça, mãos, pés, rabo), que no jogo viravam cacos espelhados. Ali não há chapa nenhuma.
    sem_chapa = (y > 1.36) | ((np.abs(x) > 0.52) & (y < 0.80)) | (y < 0.13) | ((z < -0.22) & (y < 0.98) & (np.abs(x) < 0.22))
    c["metal"] = (metal > 0.35) & ok & ~sem_chapa
    c["couro"] = (H > 4) & (H < 32) & (S > 0.33) & (V > 0.10) & (V < 0.50) & (y > 0.55) & (y < 0.80) & (np.abs(x) < 0.36) & (metal < 0.35) & ok & ~verm
    # a roupa: o pano escuro do quadril ao joelho (o rabo e o tufo do meio são pelo claro)
    zona = (y > 0.44) & (y < 0.915) & (np.abs(x) < 0.40) & (z > -0.21) & (metal < 0.35) & ok & ~c["couro"]
    # (o pelo escuro do rabo, que encosta na calça, é castanho; o pano é frio ou sem cor)
    castanho = (H > 14) & (H < 52) & (S > 0.12)
    certo = zona & (V < 0.27) & (S < 0.40) & ~castanho
    # o vinco e a linha clara do pano (cinza sem cor) são roupa também, se estão colados no pano escuro
    talvez = zona & ~certo & (V < 0.62) & (S < 0.16) & ~castanho
    perto = np.zeros(ok.shape, bool)
    perto[talvez] = cKDTree(pos[certo][::4]).query(pos[talvez], distance_upper_bound=0.012)[0] < 0.012
    c["roupa"] = certo | perto
    return c, azul


def estender(mask, idx):
    """Leva uma máscara (ou valor) dos texels de dentro à margem em volta das ilhas."""
    return mask[idx[0], idx[1]]


def olhos(c, ok, pos, nrm, info):
    """Os dois olhos: cada um é a amêndoa azul da referência, um pouco maior, inteira de luz.
    Devolve, por texel: o quanto é olho (0-1), o miolo (0 na borda, 1 no centro) e o clarão em volta."""
    P0 = pos[c["olho0"]]
    meio = np.median(P0[:, 0])
    olho = np.zeros(ok.shape, np.float32); miolo = np.zeros(ok.shape, np.float32); halo = np.zeros(ok.shape, np.float32)
    for lado in (-1, 1):
        E = P0[(P0[:, 0] - meio) * lado > 0]
        centro = E.mean(0)
        perto = ok & (np.linalg.norm(pos - centro, axis=-1) < 0.045)
        Pn = pos[perto]
        d_fora = cKDTree(E).query(Pn)[0]                       # 0 dentro da amêndoa
        dentro0 = d_fora < 0.0006
        CRESCE = 0.0022                                        # a luz toma o contorno escuro: +2 mm
        eh = d_fora < CRESCE
        # miolo: distância à borda, por dentro
        d_borda = cKDTree(Pn[~eh]).query(Pn)[0] if (~eh).any() else np.full(len(Pn), 0.01)
        raio = np.percentile(d_borda[eh], 97)
        o = suave(d_borda, 0.0, 0.0009) * eh
        mi = np.clip(d_borda / max(raio, 1e-4), 0, 1) * eh
        ha = np.exp(-np.maximum(d_fora - CRESCE, 0) / 0.0045) * (~eh)
        olho[perto] = np.maximum(olho[perto], o); miolo[perto] = np.maximum(miolo[perto], mi); halo[perto] = np.maximum(halo[perto], ha)
        larg = np.ptp(Pn[eh] @ np.linalg.svd(Pn[eh] - Pn[eh].mean(0), full_matrices=False)[2][0])
        info.append(f"olho {'esq' if lado > 0 else 'dir'}: centro {centro.round(3)}, {int(dentro0.sum())} -> {int(eh.sum())} texels, {larg * 1000:.0f} mm de comprido")
    return olho, miolo, halo


def pintar(time, m, pasta, ao=None, previa=None, cache=None):
    T = TIMES[time]
    info = []
    base8 = abrir(pasta, "_tri.png")
    assert base8.shape[0] == TAM
    base = base8.astype(np.float32) / 255
    normal = abrir(pasta, "_normal.png")
    rug = abrir(pasta, "_tri_roughness.png", "L").astype(np.float32) / 255
    metal = abrir(pasta, "_tri_metallic.png", "L").astype(np.float32) / 255
    ok, pos, nrm, tri = mapas(m, cache)
    # a margem em volta das ilhas vale o texel de dentro mais perto
    idx = ndi.distance_transform_edt(~ok, return_distances=False, return_indices=True)
    c, azul = classes(base, metal, ok, pos)

    # ------------------------------------------------------------ pelo (só o time que escurece)
    ganho, gama, tinta, satp = T["pelo"]
    nova = base.copy()
    nao_pelo = c["metal"] | c["roupa"] | c["couro"] | c["boca"] | c["orelha"] | (azul > 0.2)
    # dentes: o branco dentro da boca fica branco
    boca_p = pos[c["boca"]]
    if len(boca_p):
        cx = boca_p.mean(0)
        perto_boca = ok & (np.linalg.norm((pos - cx) * [1, 1, 0.8], axis=-1) < 0.075) & (pos[..., 2] > cx[2] - 0.05)
        db = np.full(ok.shape, 1.0, np.float32)
        db[perto_boca] = cKDTree(boca_p[::3]).query(pos[perto_boca])[0]
        dentes = perto_boca & (db < 0.012)
    else:
        dentes = np.zeros_like(ok)
    nao_pelo |= dentes
    pelo = ok & ~nao_pelo
    if (ganho, gama, satp) != (1.0, 1.0, 1.0) or tinta != (1.0, 1.0, 1.0):
        hsv = rgb_to_hsv(base)
        v2 = ganho * hsv[..., 2] ** gama
        cor2 = hsv_to_rgb(np.stack([hsv[..., 0], np.clip(hsv[..., 1] * satp, 0, 1), np.clip(v2, 0, 1)], -1)) * np.array(tinta, np.float32)
        k = estender(pelo, idx)
        nova[k] = cor2[k]
    info.append(f"pelo {pelo.sum() / ok.sum() * 100:.0f}% | metal {c['metal'].sum() / ok.sum() * 100:.0f}% | roupa {c['roupa'].sum() / ok.sum() * 100:.1f}% | couro {c['couro'].sum() / ok.sum() * 100:.1f}% | boca {int(c['boca'].sum())} | orelha {int(c['orelha'].sum())} | dentes {int(dentes.sum())}")

    # ------------------------------------------------------------ roupa na cor do personagem
    hr, sr, vr = T["roupa"]
    k = estender(c["roupa"], idx)
    vv = rgb_to_hsv(base)[..., 2]
    med = np.median(vv[c["roupa"]])
    v2 = np.clip(vv * (vr / med), 0, 1)
    # o pano mais claro (o vinco, a costura) perde um pouco de cor, como tecido de verdade
    # e a linha clara da costura não vira risco colorido: fica mais baixa e quase sem cor
    v2 = np.where(v2 > vr * 1.5, vr * 1.5 + (v2 - vr * 1.5) * 0.45, v2)
    s2 = sr * (1 - 0.80 * suave(v2, vr * 1.15, vr * 2.0))
    nova[k] = hsv_to_rgb(np.stack([np.full(ok.shape, hr / 360.0), s2, v2], -1))[k]

    # ------------------------------------------------------------ neon do equipamento
    neon = np.array(T["neon"], np.float32)
    nm = estender(c["neon"].astype(np.float32), idx) * estender(ok, idx)
    quente = suave(nm, 0.55, 1.0)
    cor_neon = neon * 0.55 + (1 - neon * 0.55) * quente[..., None] * 0.35       # o miolo da faixa mais claro
    nova = nova * (1 - nm[..., None]) + cor_neon * nm[..., None]
    emis = (neon * (0.55 + 0.45 * quente[..., None])) * (c["neon"] ** 1.3)[..., None]

    # ------------------------------------------------------------ olhos
    olho, miolo, halo = olhos(c, ok, pos, nrm, info)
    co = np.array(T["olho"], np.float32); cm = np.array(T["miolo"], np.float32)
    q = suave(miolo, 0.10, 0.75)[..., None]
    luz_olho = co * (1 - q) + cm * q
    # a borda mais funda (a cor cheia do time), o miolo quase branco: "inteiro de luz", sem pupila
    emis = emis * (1 - olho[..., None]) + luz_olho * (0.72 + 0.28 * q) * olho[..., None]
    emis += co * (halo * 0.20 * ok)[..., None]
    nova = nova * (1 - olho[..., None]) + (luz_olho * 0.85) * olho[..., None]
    # o contorno escuro pega um pouco da cor da luz
    nova = nova * (1 - 0.30 * (halo * ok)[..., None]) + co * 0.30 * (halo * ok)[..., None] * 0.6
    rug = np.where(olho > 0.5, 0.20, rug); metal = np.where(olho > 0.5, 0.0, metal)
    # pelo não é chapa nem verniz: sem metal, e fosco
    pelo_m = estender(pelo | c["roupa"], idx)
    metal = np.where(pelo_m, 0.0, metal)
    rug = np.where(estender(pelo, idx), np.maximum(rug, 0.74), rug)
    # faixa de neon: lisa, sem metal (é luz, não chapa)
    rug = rug * (1 - nm) + 0.30 * nm; metal = metal * (1 - nm)

    # ------------------------------------------------------------ oclusão
    if ao:
        oc = np.asarray(Image.open(ao).convert("L")).astype(np.float32) / 255
        assert oc.shape[0] == TAM, "a oclusão tem de ser assada no tamanho da textura"
        oc = oc[idx[0], idx[1]]                       # a margem vale o de dentro: sem borda preta nas emendas
    else:
        oc = np.ones((TAM, TAM), np.float32)
    oc = np.where(olho > 0.3, 1.0, oc)
    orm = np.stack([oc, rug, metal], -1)

    # ------------------------------------------------------------ cor de vértice
    # R: quanto o vértice balança (as pontas do pelo);  G: pelo (o brilho de contorno);  B: 0,5 (tom neutro)
    P = m["pos"].astype(np.float64); de = m["de"]; I = m["idx"].astype(np.int64)
    npos = int(de.max()) + 1
    Pp = np.zeros((npos, 3)); Pp[de] = P
    Fp = de[I]
    from scipy.sparse import coo_matrix
    A = coo_matrix((np.ones(len(Fp) * 6), (np.concatenate([Fp[:, 0], Fp[:, 1], Fp[:, 2], Fp[:, 1], Fp[:, 2], Fp[:, 0]]), np.concatenate([Fp[:, 1], Fp[:, 2], Fp[:, 0], Fp[:, 0], Fp[:, 1], Fp[:, 2]]))), shape=(npos, npos)).tocsr()
    A.data[:] = 1; grau = np.maximum(np.asarray(A.sum(1)).ravel(), 1)
    Ps = Pp.copy()
    for _ in range(12):
        Ps = 0.5 * Ps + 0.5 * (A @ Ps) / grau[:, None]
    fn = np.cross(Pp[Fp[:, 1]] - Pp[Fp[:, 0]], Pp[Fp[:, 2]] - Pp[Fp[:, 0]])
    vn = np.zeros((npos, 3)); np.add.at(vn, Fp.ravel(), np.repeat(fn, 3, 0)); vn /= np.maximum(np.linalg.norm(vn, axis=1, keepdims=True), 1e-12)
    fora = ((Pp - Ps) * vn).sum(1)
    # o que é pelo em cada vértice: a classe do texel onde o vértice cai
    px = np.clip((m["uv"][:, 0] * TAM).astype(int), 0, TAM - 1); py = np.clip((m["uv"][:, 1] * TAM).astype(int), 0, TAM - 1)
    pelo_v = estender(pelo, idx)[py, px].astype(np.float32)
    pelo_p = np.ones(npos); np.minimum.at(pelo_p, de, pelo_v)            # um canto no equipamento: não é pelo
    # vizinho de equipamento também não balança (a ponta do pelo colada na chapa abriria fresta)
    pelo_p = np.minimum(pelo_p, (A @ pelo_p) / grau > 0.99)
    ossos = [str(o) for o in m["ossos"]]
    def peso_de(nomes):
        ids = [ossos.index(n) for n in nomes]
        return np.where(np.isin(m["joints"], ids), m["weights"], 0).sum(1)
    w_ext = np.zeros(npos); np.maximum.at(w_ext, de, peso_de(["LeftHand", "RightHand", "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase"]))
    w_cab = np.zeros(npos); np.maximum.at(w_cab, de, peso_de(["Head", "Neck", "HeadTop_End", "headfront"]))
    r = suave(fora, 0.006, 0.030) * pelo_p * (1 - np.clip(w_ext * 1.5, 0, 1))
    rosto = w_cab * suave(Pp[:, 2], 0.05, 0.12) * (1 - suave(Pp[:, 1], 1.50, 1.56))
    r *= 1 - np.clip(rosto * 1.4, 0, 1)
    cor = np.stack([r[de], pelo_v * (1 - 0.5 * np.clip(w_ext[de], 0, 1)), np.full(len(de), 0.5), np.ones(len(de))], 1)
    cor = np.clip(cor * 255 + 0.5, 0, 255).astype(np.uint8)
    info.append(f"balanço: {int((r > 0.3).sum())} pontas de {npos} vértices")

    for linha in info:
        print("  ", linha)
    u8 = lambda a: (np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)
    menor = lambda a: np.asarray(Image.fromarray(a).resize((MENOR, MENOR), Image.LANCZOS))
    if previa:
        from previa import vista
        os.makedirs(previa, exist_ok=True)
        cls = np.zeros(ok.shape + (3,), np.uint8); cls[ok] = (150, 150, 150)
        for nome, corc in (("metal", (90, 110, 160)), ("roupa", (40, 200, 80)), ("couro", (160, 90, 30)), ("boca", (255, 60, 60)), ("orelha", (255, 150, 150))):
            cls[c[nome]] = corc
        cls[dentes] = (255, 255, 255); cls[c["neon"] > 0.3] = (0, 255, 255); cls[olho > 0.3] = (255, 255, 0)
        kk = ok
        for nome, ang in (("frente", 0.0), ("costas", np.pi), ("lado", np.pi / 2)):
            vista(pos[kk], nrm[kk], cls[kk], np.zeros_like(cls[kk]), ang, 900).save(os.path.join(previa, f"classes_{time}_{nome}.jpg"), quality=88)
    return dict(base=u8(nova), orm=menor(u8(orm)), normal=normal, emissao=u8(emis), cor_vertice=cor)
