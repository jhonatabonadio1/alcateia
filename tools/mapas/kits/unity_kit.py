#!/usr/bin/env python3
"""
Um pacote do Unity (.unitypackage) como kit do gerador de mapas — o "Low Poly Sci-Fi City" que o Jhon mandou em 20/11
("implemente esse mapa no jogo").

    python3 tools/mapas/kits/unity_pacote.py <pasta do .unitypackage aberto (tar)> <pasta kit>      # põe cada arquivo no caminho dele
    python3 tools/mapas/kits/unity_fbx.py <pasta kit>/Assets/.../Models <pasta malhas>               # precisa do Blender (pip install bpy)
    python3 tools/mapas/kits/unity_kit.py <pasta kit> <pasta malhas> scifi LP_SciFiCity_DEMO

Grava:
- tools/mapas/kits/<kit>.npz        as peças (uma por prefab: a malha do .fbx e os filhos, já no lugar), como os outros kits;
- assets/mapas/kits/<kit>.png       a textura única: as quatro texturas do pacote e as cores dos materiais lisos num atlas
                                    de 2048 px (a paleta, os anúncios, o piso, os prédios de fundo e os LEDs);
- assets/mapas/kits/<kit>_luz.png   o que acende (os LEDs, os anúncios, as janelas dos prédios de fundo), na cor que acende;
- tools/mapas/<cena>.json           a cena de demonstração do pacote: onde está cada peça (a matriz, já no espaço do
                                    Godot) e as luzes.

O Unity é de mão esquerda (x para o outro lado): a posição vira (−x, y, z) e a rotação (x, −y, −z, w). A malha do .fbx
já é de mão direita, em centímetros: entra como está, vezes 0,01.
"""
import json, os, re, sys
import numpy as np
from PIL import Image

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(AQUI)))
L = 2048

# onde cada material mora no atlas: (x, y, lado) em px. Os de textura ocupam um quadrante; os lisos, um quadradinho.
QUADRO = dict(main=(0, 0, 1024), tex2=(1024, 0, 1024), piso=(0, 1024, 1024), bg1=(1024, 1024, 512), bg2=(1536, 1024, 512), bg3=(1024, 1536, 512))
LISOS = ['Emissive_01_Blue', 'Emissive_02_Pink', 'Emissive_03_Green', 'Emissive_04_Pink2', 'Emissive_05_White', 'Emissive_06_Red', 'Emissive_07_Purple',
         'Emissive_08_Blue', 'Emissive_09_Orange', 'Glass', 'SciFiCity_BG', 'Warning', 'Skybox']
def liso(k): return (1536 + (k % 4) * 128, 1536 + (k // 4) * 128, 128)


def docs(caminho):
    txt = open(caminho, encoding='utf-8', errors='replace').read()
    return [(int(m.group(1)), m.group(2), bool(m.group(3)), m.group(4))
            for m in re.finditer(r'--- !u!(\d+) &(-?\d+)( stripped)?\n(.*?)(?=\n--- !u!|\Z)', txt, re.S)]


def vetor(corpo, campo, padrao):
    m = re.search(campo + r': \{([^}]*)\}', corpo)
    if not m: return dict(padrao)
    d = dict(padrao); d.update({k: float(v) for k, v in re.findall(r'(\w+): ([-\d.e]+)', m.group(1))}); return d


def matriz(p, q, s):
    """A matriz do Godot para a posição, a rotação (quaternion) e a escala do Unity."""
    x, y, z, w = q['x'], -q['y'], -q['z'], q['w']; n = np.sqrt(x * x + y * y + z * z + w * w) or 1.0; x, y, z, w = x / n, y / n, z / n, w / n
    R = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)], [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                  [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
    M = np.eye(4); M[:3, :3] = R @ np.diag([s['x'], s['y'], s['z']]); M[:3, 3] = (-p['x'], p['y'], p['z']); return M


P0, Q0, S0 = dict(x=0, y=0, z=0), dict(x=0, y=0, z=0, w=1), dict(x=1, y=1, z=1)


def trs(corpo):
    return vetor(corpo, 'm_LocalPosition', P0), vetor(corpo, 'm_LocalRotation', Q0), vetor(corpo, 'm_LocalScale', S0)


def cubo():
    """O cubo do Unity (1 m, no meio): T × 3 × 8, com a textura de 0 a 1 em cada face."""
    tris = []
    for eixo in range(3):
        for sinal in (-1, 1):
            a, b = [(1, 2), (2, 0), (0, 1)][eixo]; n = np.zeros(3); n[eixo] = sinal
            q = []
            for u, v in ((0, 0), (1, 0), (1, 1), (0, 1)):
                p = np.zeros(3); p[eixo] = 0.5 * sinal; p[a] = u - 0.5; p[b] = v - 0.5; q.append(np.r_[p, n, u, v])
            f = [[q[0], q[1], q[2]], [q[0], q[2], q[3]]]
            for t in f:
                t = np.array(t)
                if np.dot(np.cross(t[1, :3] - t[0, :3], t[2, :3] - t[0, :3]), n) < 0: t = t[[0, 2, 1]]
                tris.append(t)
    return np.array(tris, np.float32) * np.r_[100, 100, 100, 1, 1, 1, 1, 1].astype(np.float32), np.zeros(12, np.int32)


def main(kit_dir, malhas_dir, kit, cena):
    guid = json.load(open(os.path.join(kit_dir, 'guids.json')))
    base = os.path.join(kit_dir, [v for v in guid.values() if v.endswith('/Materials')][0].rsplit('/', 1)[0])
    indice = json.load(open(os.path.join(malhas_dir, 'indice.json')))
    tex = lambda *p: Image.open(os.path.join(base, 'Textures', *p)).convert('RGB')

    # ---- os materiais: a cor, o que acende e o lugar no atlas
    mats = {}
    for g, caminho in guid.items():
        if not caminho.endswith('.mat'): continue
        t = open(os.path.join(kit_dir, caminho), encoding='utf-8', errors='replace').read(); nome = os.path.basename(caminho)[:-4].replace('M_LP_SciFi_', '').replace('M_LP_', '')
        cor = vetor(t, '- _BaseColor', dict(r=1, g=1, b=1, a=1)); luz = vetor(t, '- _EmissionColor', dict(r=0, g=0, b=0, a=1))
        mats[g] = dict(nome=nome, cor=(cor['r'], cor['g'], cor['b']), luz=(luz['r'], luz['g'], luz['b']))
    atlas = Image.new('RGB', (L, L), (128, 128, 128)); luz = Image.new('RGB', (L, L), (0, 0, 0))
    atlas.paste(tex('T_Texture_Sci-Fi_01.png').resize((1024, 1024), Image.NEAREST), (0, 0))
    t2 = tex('T_Texture_Sci-Fi_02.png'); atlas.paste(t2, (1024, 0)); atlas.paste(tex('Floor', 'T_Floor_Base.png'), (0, 1024))
    bgb = np.asarray(tex('BgBuilding', 'T_BgBuilding_Base.png'), float) / 255; bge = np.asarray(tex('BgBuilding', 'T_BgBuilding_Emissive.png'), float) / 255
    por_nome = {m['nome']: m for m in mats.values()}
    for k in (1, 2, 3):
        m = por_nome[f'BgBuilding_0{k}']; x, y, _ = QUADRO[f'bg{k}']
        atlas.paste(Image.fromarray(np.uint8(np.clip(bgb * np.array(m['cor']), 0, 1) * 255)), (x, y))
        e = np.array(m['luz']); luz.paste(Image.fromarray(np.uint8(np.clip(bge * e / max(e.max(), 1.0), 0, 1) * 255)), (x, y))
    for k, n in enumerate(LISOS):
        if n not in por_nome: continue
        m = por_nome[n]; x, y, s = liso(k); c = np.array(m['cor'])
        if n == 'Glass': c = np.array([0.36, 0.52, 0.66])
        atlas.paste(tuple(int(v) for v in np.clip(c, 0, 1) * 255), (x, y, x + s, y + s))
        if n.startswith('Emissive') or n == 'Warning':
            e = np.array(m['luz']); luz.paste(tuple(int(v) for v in np.clip(e / max(e.max(), 1.0), 0, 1) * 255), (x, y, x + s, y + s))
    t2a = np.asarray(t2, np.uint8); luz_px = np.asarray(luz, np.uint8).copy()

    def lugar(nome):
        """O retângulo do material no atlas e se a face acende com a própria textura (os anúncios)."""
        if nome == 'Main': return QUADRO['main'], False
        if nome in ('Ads',): return QUADRO['tex2'], True
        if nome == 'Food': return QUADRO['tex2'], False
        if nome == 'Floor': return QUADRO['piso'], False
        if nome.startswith('BgBuilding_0'): return QUADRO['bg' + nome[-1]], False
        if nome in LISOS: return liso(LISOS.index(nome)), False
        return QUADRO['main'], False

    # ---- as peças: um prefab = uma peça (a malha e os filhos, cada um na matriz dele)
    def malha_de(g, fid, nome_no):
        if g.startswith('0000000000000000e'): return cubo() if fid == '10202' else None
        arq = os.path.splitext(os.path.basename(guid.get(g, '')))[0]
        if arq not in indice: return None
        ms = indice[arq]['malhas']
        m = ms[0] if len(ms) == 1 else next((x for x in ms if x['objeto'] == nome_no or x['malha'] == nome_no), min(ms, key=lambda x: -x['tris']))
        d = np.load(os.path.join(malhas_dir, m['arquivo'] + '.npz')); return d['tri'], d['mat']

    pecas = {}; raiz_de = {}; sem = set()
    for g, caminho in guid.items():
        if not caminho.endswith('.prefab'): continue
        D = docs(os.path.join(kit_dir, caminho)); obj = {i: b for t, i, s, b in D if t == 1 and not s}
        tr = {}; do_obj = {}
        for t, i, s, b in D:
            if t != 4 or s: continue
            go = re.search(r'm_GameObject: \{fileID: (-?\d+)', b).group(1); pai = re.search(r'm_Father: \{fileID: (-?\d+)', b).group(1)
            tr[i] = (go, pai, trs(b)); do_obj[go] = i
        def mundo(i, raiz_identidade=True):
            go, pai, (p, q, s) = tr[i]
            if pai == '0' or pai not in tr: return np.eye(4)          # a raiz: quem põe a posição, a rotação e a escala é a cena
            return mundo(pai) @ matriz(p, q, s)
        raiz = next((i for i, (go, pai, _) in tr.items() if pai == '0'), None)
        if raiz is None: continue
        raiz_de[g] = dict(id=raiz, escala=tr[raiz][2][2], filhos={i: mundo(i) for i in tr})
        filtros = {re.search(r'm_GameObject: \{fileID: (-?\d+)', b).group(1): re.search(r'm_Mesh: \{fileID: (-?\d+), guid: ([0-9a-f]+)', b) for t, i, s, b in D if t == 33 and not s}
        rend = {re.search(r'm_GameObject: \{fileID: (-?\d+)', b).group(1): re.findall(r'- \{fileID: \d+, guid: ([0-9a-f]+)', b.split('m_Materials:')[1].split('m_StaticBatchInfo')[0])
                for t, i, s, b in D if t == 23 and not s and 'm_Materials:' in b}
        partes = []
        for go, mf in filtros.items():
            if mf is None or go not in rend or go not in do_obj: continue
            nome_no = re.search(r'm_Name: (.*)', obj[go]).group(1).strip()
            if re.search(r'm_IsActive: 0', obj[go]): continue
            mm = malha_de(mf.group(2), mf.group(1), nome_no)
            if mm is None: sem.add(nome_no); continue
            tri, mat = mm; M = mundo(do_obj[go]); tri = tri.astype(float).copy()
            pos = tri[:, :, :3] * 0.01 @ M[:3, :3].T + M[:3, 3]
            N = np.linalg.inv(M[:3, :3]).T; nor = tri[:, :, 3:6] @ N.T; nor /= np.maximum(np.linalg.norm(nor, axis=-1, keepdims=True), 1e-9)
            uv = tri[:, :, 6:8].copy(); uv[:, :, 1] = 1 - uv[:, :, 1]                    # o v do Blender sobe; o da imagem desce
            fica = np.ones(len(tri), bool)
            for k in np.unique(mat):
                gm = rend[go][min(int(k), len(rend[go]) - 1)] if rend[go] else None; m = mats.get(gm, dict(nome='Main'))
                sel = mat == k
                if m['nome'] in ('Warning', 'Skybox'): fica &= ~sel; continue          # a fita de aviso é um holograma transparente: fica de fora
                (x, y, s), acende = lugar(m['nome'])
                if s == 128: uv[sel] = (np.array([x, y]) + 64.0) / L                    # material liso: o meio do quadradinho
                else:
                    u_ = uv[sel] % 1.0 if uv[sel].min() < -0.001 or uv[sel].max() > 1.001 else uv[sel]
                    if acende:                                                           # o anúncio aceso: a parte da textura que ele usa passa para a luz
                        lo = np.floor(u_.reshape(-1, 2).min(0) * 1024).astype(int); hi = np.ceil(u_.reshape(-1, 2).max(0) * 1024).astype(int)
                        luz_px[y + lo[1]:y + hi[1], x + lo[0]:x + hi[0]] = t2a[lo[1]:hi[1], lo[0]:hi[0]]
                    uv[sel] = (np.array([x, y]) + u_ * s) / L
            t = np.concatenate([pos, nor, uv], 2)[fica]
            if np.linalg.det(M[:3, :3]) < 0: t = t[:, ::-1]
            partes.append(t)
        if partes: pecas[os.path.basename(caminho)[:-7]] = np.concatenate(partes).astype(np.float32)
    nomes = sorted(pecas); inicio = np.cumsum([0] + [len(pecas[n]) for n in nomes])
    np.savez_compressed(os.path.join(AQUI, kit + '.npz'), nomes=np.array(nomes), inicio=inicio, tri=np.concatenate([pecas[n] for n in nomes]))
    destino = os.path.join(RAIZ, 'assets', 'mapas', 'kits'); os.makedirs(destino, exist_ok=True)
    atlas.save(os.path.join(destino, kit + '.png')); Image.fromarray(luz_px).save(os.path.join(destino, kit + '_luz.png'))
    print(f"  {kit}: {len(nomes)} peças, {inicio[-1]} triângulos; sem malha: {sorted(sem)[:8]}")

    # ---- a cena de demonstração: onde está cada peça e as luzes
    D = docs(os.path.join(base, 'Scenes', cena + '.unity'))
    tr = {}; inst = {}; faixa = {}
    for t, i, s, b in D:
        if t == 4 and not s:
            tr[i] = (re.search(r'm_Father: \{fileID: (-?\d+)', b).group(1), matriz(*trs(b)), re.search(r'm_GameObject: \{fileID: (-?\d+)', b).group(1))
        elif t == 4 and s:
            faixa[i] = re.search(r'm_PrefabInstance: \{fileID: (-?\d+)', b).group(1)
        elif t == 1001:
            g = re.search(r'm_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]+)', b).group(1)
            mods = {}
            for alvo, prop, valor in re.findall(r'- target: \{fileID: (-?\d+)[^}]*\}\n\s+propertyPath: (\S+)\n\s+value: (\S*)', b):
                mods[(alvo, prop)] = valor
            inst[i] = (g, re.search(r'm_TransformParent: \{fileID: (-?\d+)', b).group(1), mods)
    def do_transform(i):
        if i == '0': return np.eye(4)
        if i in tr: return do_transform(tr[i][0]) @ tr[i][1]
        if i in faixa and faixa[i] in inst: return da_instancia(faixa[i])
        return np.eye(4)
    feito = {}
    def da_instancia(i):
        if i in feito: return feito[i]
        g, pai, mods = inst[i]; r = raiz_de.get(g)
        p, q, s = dict(P0), dict(Q0), dict(r['escala'] if r else S0)
        if r:
            for (alvo, prop), valor in mods.items():
                if alvo != r['id'] or not valor: continue
                campo, _, eixo = prop.partition('.')
                if campo == 'm_LocalPosition': p[eixo] = float(valor)
                elif campo == 'm_LocalRotation': q[eixo] = float(valor)
                elif campo == 'm_LocalScale': s[eixo] = float(valor)
        feito[i] = do_transform(pai) @ matriz(p, q, s); return feito[i]
    itens = []; fora = {}
    for i, (g, pai, mods) in inst.items():
        nome = os.path.basename(guid.get(g, '?'))[:-7]
        if nome not in pecas: fora[nome] = fora.get(nome, 0) + 1; continue
        if any(prop == 'm_IsActive' and valor == '0' for (alvo, prop), valor in mods.items()): continue
        itens.append(dict(peca=nome, matriz=[round(float(v), 5) for v in da_instancia(i)[:3].reshape(-1)]))
    luzes = []
    for t, i, s, b in D:
        if t != 108: continue
        go = re.search(r'm_GameObject: \{fileID: (-?\d+)', b).group(1); dono = next((k for k, v in tr.items() if v[2] == go), None)
        if dono is None: continue
        c = vetor(b, 'm_Color', dict(r=1, g=1, b=1, a=1)); M = do_transform(dono)
        luzes.append(dict(tipo=int(re.search(r'm_Type: (\d+)', b).group(1)), cor=[round(c[k], 3) for k in 'rgb'], forca=float(re.search(r'm_Intensity: ([-\d.e]+)', b).group(1)),
                          alcance=float(re.search(r'm_Range: ([-\d.e]+)', b).group(1)), pos=[round(float(v), 3) for v in M[:3, 3]], frente=[round(float(v), 4) for v in M[:3, 2]]))
    json.dump(dict(itens=itens, luzes=luzes), open(os.path.join(os.path.dirname(AQUI), cena + '.json'), 'w'), separators=(',', ':'))
    print(f"  {cena}: {len(itens)} peças postas, {len(luzes)} luzes; sem peça: {fora}")


if __name__ == '__main__':
    main(*sys.argv[1:5])
