#!/usr/bin/env python3
"""
A mira do AR-15 em primeira pessoa (pedido do Jhon, 18/11: "crie a animação da
AR-15 escopada, ou seja, do personagem mirando com ela; deixe natural").

    python3 tools/armas/mira.py assets/armas/fp/ar15.glb [outro.glb ...]
        [--ficha assets/armas/fp/bracos.json] [--previa pasta]

Até aqui, mirar (botão direito) só fechava a câmera: a arma ficava na cintura e
quem mirava era um desenho no meio da tela. Agora a arma sobe até o olho e a
mira é a dela: a alça (o anel de trás) e a massa (o poste da frente) ficam em
linha no centro da tela.

Cinco clipes novos no conjunto (o mesmo esqueleto, as mesmas mãos na arma):

- mirar       0,26 s  da cintura ao olho: a arma sobe primeiro e entra depois,
                      num arco, inclinada e com o cano um pouco para dentro;
                      nivela ao chegar, passa 2 mm do ponto e assenta;
- desmirar    0,24 s  do olho à cintura: solta, cai, passa um pouco e volta;
- mira        2,0 s   parada na mira, em laço: a respiração do "parado", com
                      um quarto do tamanho (a mira não sai do alvo);
- mira_andar  0,8 s   andando na mira, em laço: o balanço do "andar", com 35%;
- mira_tiro   0,53 s  o tiro na mira: o coice, o ferrolho e o dedo do "tiro",
                      com 60% do recuo.

Como é feito: nada nos braços muda. O conjunto inteiro (braços e arma) anda
junto pelo osso da raiz, então as mãos não saem do lugar na arma. A linha de
mira é medida na malha (o centro do anel da alça e o centro da massa, na pose
parada) e levada para o eixo do olho. Nos laços, o movimento que o clipe de
origem já dá à arma é desfeito em parte pela raiz — é o que deixa a respiração
pequena sem as mãos escorregarem.

Também grava na ficha (bracos.json) onde fica a boca do cano no osso da arma
("cano_osso"): com a arma mudando de lugar, o clarão e o traçante saem de onde
o cano está, não de onde ele ficava na cintura.

Rodar de novo num arquivo que já tem os clipes troca os antigos pelos novos.
"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "rugido"))
from glb import Glb

FPS = 60.0
NOVOS = ("mirar", "desmirar", "mira", "mira_andar", "mira_tiro")

# ---- o desenho (metros no espaço do olho: x direita, y cima, −z frente; graus) ----
OLHO = 0.062           # a que distância do olho fica a alça ao mirar (m); a câmera corta a 3 cm
FURO, ARO = 0.0046, 0.0017    # o furo da alça aberta (raio) e a espessura do aro (m)
ENTRAR, SAIR = 0.26, 0.24
RESPIRA, BALANCA, RECUA = 0.10, 0.16, 0.55      # quanto do movimento do clipe de origem fica na mira


# ---------------------------------------------------------------- contas
def qmat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])

def matq(m):
    t = np.trace(m)
    if t > 0:
        s = np.sqrt(t + 1.0) * 2; q = [(m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s, 0.25 * s]
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = np.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2; q = [0.25 * s, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s, (m[2, 1] - m[1, 2]) / s]
    elif m[1, 1] > m[2, 2]:
        s = np.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2; q = [(m[0, 1] + m[1, 0]) / s, 0.25 * s, (m[1, 2] + m[2, 1]) / s, (m[0, 2] - m[2, 0]) / s]
    else:
        s = np.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2; q = [(m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, 0.25 * s, (m[1, 0] - m[0, 1]) / s]
    q = np.array(q); return q / np.linalg.norm(q)

def trs(t, q, s=(1, 1, 1)):
    m = np.eye(4); m[:3, :3] = qmat(q) * np.asarray(s, float)[None, :]; m[:3, 3] = t; return m

def euler(pitch, yaw, roll):
    """Graus. pitch +: cano para cima; yaw +: cano para a esquerda; roll +: o topo da arma tomba para a esquerda."""
    a, b, c = np.radians([pitch, yaw, roll])
    rx = np.array([[1, 0, 0], [0, np.cos(a), -np.sin(a)], [0, np.sin(a), np.cos(a)]])
    ry = np.array([[np.cos(b), 0, np.sin(b)], [0, 1, 0], [-np.sin(b), 0, np.cos(b)]])
    rz = np.array([[np.cos(c), -np.sin(c), 0], [np.sin(c), np.cos(c), 0], [0, 0, 1]])
    return ry @ rx @ rz

def rigido(move, giro, pivo):
    """Gira em volta do pivô e desloca: 4×4 no espaço do olho."""
    m = np.eye(4); m[:3, :3] = giro; m[:3, 3] = pivo - giro @ pivo + move; return m

def mola(t, w=24.0, z=0.70):
    """De 0 a 1 como uma mola pouco amortecida: chega rápido, passa ~4% e assenta."""
    if t <= 0: return 0.0
    wd = w * np.sqrt(1 - z * z)
    return 1.0 - np.exp(-z * w * t) * (np.cos(wd * t) + z * w / wd * np.sin(wd * t))

def suave(x):
    x = min(max(x, 0.0), 1.0); return x * x * (3 - 2 * x)

def sino(x, forca=1.0):
    x = min(max(x, 0.0), 1.0); return np.sin(np.pi * x) ** forca


# ---------------------------------------------------------------- o arquivo
class Conjunto:
    def __init__(self, path, limpar=True):
        self.g = g = Glb(path); self.j = j = g.json
        if limpar: j['animations'] = [a for a in j['animations'] if a['name'] not in NOVOS]
        self.names = [n.get('name') for n in j['nodes']]
        self.parent = {}
        for i, n in enumerate(j['nodes']):
            for c in n.get('children', []): self.parent[c] = i
        self.root = self.names.index('root'); self.gun = self.names.index('ik_hand_gun')

    def clip(self, name):
        an = [a for a in self.j['animations'] if a['name'] == name][0]; out = {}
        for c in an['channels']:
            s = an['samplers'][c['sampler']]
            out[(c['target']['node'], c['target']['path'])] = dict(
                input=s['input'], output=s['output'], interp=s.get('interpolation', 'LINEAR'),
                t=self.g.acc(s['input'])[:, 0].astype(float), v=self.g.acc(s['output']).astype(float))
        return out

    @staticmethod
    def sample(ch, t):
        ts, vs = ch['t'], ch['v']
        if t <= ts[0]: return vs[0].copy()
        if t >= ts[-1]: return vs[-1].copy()
        i = int(np.searchsorted(ts, t, side='right')) - 1
        if ch['interp'] == 'STEP': return vs[i].copy()
        u = (t - ts[i]) / (ts[i + 1] - ts[i]); a, b = vs[i], vs[i + 1]
        if vs.shape[1] == 4:
            if np.dot(a, b) < 0: b = -b
            q = a * (1 - u) + b * u; return q / np.linalg.norm(q)
        return a * (1 - u) + b * u

    def local(self, clip, node, t):
        n = self.j['nodes'][node]
        get = lambda p, d: self.sample(clip[(node, p)], t) if (node, p) in clip else np.array(n.get(p, d), float)
        return trs(get('translation', [0, 0, 0]), get('rotation', [0, 0, 0, 1]), get('scale', [1, 1, 1]))

    def world(self, clip, node, t):
        chain = [node]
        while chain[-1] in self.parent: chain.append(self.parent[chain[-1]])
        m = np.eye(4)
        for i in reversed(chain): m = m @ self.local(clip, i, t)
        return m

    def skinned(self, clip, mesh_name, t):
        g, j = self.g, self.j
        node = [n for n in j['nodes'] if 'mesh' in n and n.get('name') == mesh_name][0]
        skin = j['skins'][node['skin']]
        ibm = g.acc(skin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
        cache = {}
        def w(i):
            if i not in cache: cache[i] = (w(self.parent[i]) if i in self.parent else np.eye(4)) @ self.local(clip, i, t)
            return cache[i]
        J = np.array([w(jn) @ ibm[k] for k, jn in enumerate(skin['joints'])])
        out = []
        for p in j['meshes'][node['mesh']]['primitives']:
            a = p['attributes']
            pos = g.acc(a['POSITION']).astype(float); jn = g.acc(a['JOINTS_0']).astype(int); wt = g.acc(a['WEIGHTS_0']).astype(float)
            ct = j['accessors'][a['WEIGHTS_0']]['componentType']
            wt /= {5121: 255.0, 5123: 65535.0}.get(ct, 1.0)
            ph = np.concatenate([pos, np.ones((len(pos), 1))], 1); v = np.zeros((len(pos), 3))
            for k in range(4): v += wt[:, k:k + 1] * np.einsum('nij,nj->ni', J[jn[:, k]], ph)[:, :3]
            out.append(v)
        return np.concatenate(out)

    def add_times(self, ts):
        return self.g.add_acc(np.asarray(ts, float).reshape(-1, 1), 'SCALAR', minmax=True)

    def add_clip(self, name, base, length, root_at, parado0=None):
        """Um clipe novo. Os canais são os de `base`; a raiz segue root_at(t) → 4×4 local.
        parado0: congela os outros ossos na pose do começo do clipe de base (as transições)."""
        j = self.j
        n = int(round(length * FPS)) + 1
        ts = np.arange(n) / FPS; ts[-1] = length
        T = np.zeros((n, 3)); R = np.zeros((n, 4))
        for i, t in enumerate(ts):
            m = root_at(float(t)); T[i] = m[:3, 3]; R[i] = matq(m[:3, :3])
            if i and np.dot(R[i], R[i - 1]) < 0: R[i] = -R[i]
        t_in = self.add_times(ts)
        samplers, channels = [], []
        def put(node, path, inp, outp, interp):
            samplers.append({'input': inp, 'output': outp, 'interpolation': interp})
            channels.append({'sampler': len(samplers) - 1, 'target': {'node': node, 'path': path}})
        two = self.add_times([0.0, length]) if parado0 else None
        for (node, path), ch in base.items():
            if node == self.root and path in ('translation', 'rotation'): continue
            if not parado0:
                put(node, path, ch['input'], ch['output'], ch['interp'])
            elif len(ch['t']) == 2 and np.abs(ch['v'][1] - ch['v'][0]).max() < 1e-7:
                put(node, path, two, ch['output'], 'STEP')
            else:
                kind = {1: 'SCALAR', 3: 'VEC3', 4: 'VEC4'}[ch['v'].shape[1]]
                put(node, path, two, self.g.add_acc(np.stack([ch['v'][0], ch['v'][0]]), kind), 'STEP')
        put(self.root, 'translation', t_in, self.g.add_acc(T, 'VEC3'), 'LINEAR')
        put(self.root, 'rotation', t_in, self.g.add_acc(R, 'VEC4'), 'LINEAR')
        j['animations'].append({'name': name, 'channels': channels, 'samplers': samplers, 'extras': {'mira': 1}})


def _miras(v):
    """Os vértices do topo de cada mira (só elas chegam tão alto): (os da alça, os da massa)."""
    topo = v[:, 1].max()
    alto = v[v[:, 1] > topo - 0.012]
    zs = np.sort(alto[:, 2]); corte = (zs[0] + zs[-1]) / 2
    perto = (v[:, 1] > topo - 0.024)
    tras = perto & (v[:, 2] > corte) & (v[:, 2] < zs[-1] + 0.01)
    frente = perto & (v[:, 2] < corte) & (v[:, 2] > zs[0] - 0.01)
    return tras, frente


def medir(c, parado):
    """
    A linha de mira, no espaço do olho: **o centro do furo da alça** (o anel de trás) e
    **a ponta do poste da massa** (a da frente). É por esses dois pontos que se mira; a
    primeira versão pegava o meio da caixa de cada peça, 5 mm abaixo do furo, e o olho
    ficava olhando para o metal (relato do Jhon, 18/11: "precisa pegar a mira de trás e a
    da frente e alinhar perfeitamente").
    Devolve (alça, massa, malha, raio de dentro do anel, raio de fora).
    """
    v = c.skinned(parado, 'fuzil', 0.0)
    tras, frente = _miras(v)
    a = v[tras]; topo_a = a[:, 1].max()
    largo = a[a[:, 1] > topo_a - 0.0075]                       # o anel na parte mais larga
    diam = np.ptp(largo[:, 0]); xa = (largo[:, 0].min() + largo[:, 0].max()) / 2
    alca = np.array([xa, topo_a - diam / 2, a[a[:, 1] > topo_a - diam][:, 2].mean()])
    r = np.hypot(a[:, 0] - alca[0], a[:, 1] - alca[1])
    dentro = float(r.min()); fora = diam / 2

    m = v[frente]; topo_m = m[:, 1].max()
    orelhas = m[m[:, 1] > topo_m - 0.004]
    xm = (orelhas[:, 0].min() + orelhas[:, 0].max()) / 2
    poste = m[np.abs(m[:, 0] - xm) < 0.0013]                   # o poste fino entre as duas orelhas
    ponta = poste[:, 1].max()
    massa = np.array([poste[poste[:, 1] > ponta - 0.001][:, 0].mean(), ponta, poste[poste[:, 1] > ponta - 0.003][:, 2].mean()])
    return alca, massa, v, dentro, fora


def abrir_alca(c, parado):
    """
    Abre o anel da alça (uma "ghost ring"): o furo de fábrica tem 2 mm de raio, e a
    6 cm do olho isso é um buraco de fechadura — não se vê o alvo. O furo vai a
    FURO de raio e o anel fica fino (ARO de espessura), para a massa e o alvo
    aparecerem inteiros lá dentro. Mexe na malha (os vértices do anel abrem em volta
    do centro do furo); marca o arquivo para não abrir duas vezes.
    """
    j, g = c.j, c.g
    node = [n for n in j['nodes'] if 'mesh' in n and n.get('name') == 'fuzil'][0]
    mesh = j['meshes'][node['mesh']]
    if mesh.get('extras', {}).get('alca_aberta'): return False
    alca, _, v, dentro, fora = medir(c, parado)
    tras, _ = _miras(v)
    skin = j['skins'][node['skin']]
    ibm = g.acc(skin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
    cache = {}
    def w(i):
        if i not in cache: cache[i] = (w(c.parent[i]) if i in c.parent else np.eye(4)) @ c.local(parado, i, 0.0)
        return cache[i]
    J = np.array([w(jn) @ ibm[k] for k, jn in enumerate(skin['joints'])])
    off = 0; mexidos = 0
    for p in mesh['primitives']:
        a = p['attributes']
        pos = g.acc(a['POSITION']).astype(float); jn = g.acc(a['JOINTS_0']).astype(int); wt = g.acc(a['WEIGHTS_0']).astype(float)
        wt /= {5121: 255.0, 5123: 65535.0}.get(j['accessors'][a['WEIGHTS_0']]['componentType'], 1.0)
        n = len(pos); vv = v[off:off + n]; sel = tras[off:off + n].copy(); off += n
        dx = vv[:, 0] - alca[0]; dy = vv[:, 1] - alca[1]; r = np.hypot(dx, dy)
        sel &= (r <= fora * 1.1) & (r > 1e-6)
        if not sel.any(): continue
        novo_r = FURO + (np.clip(r, dentro, fora) - dentro) / max(fora - dentro, 1e-6) * ARO
        d = np.zeros((n, 3)); k = np.where(sel)[0]
        d[k, 0] = dx[k] / r[k] * (novo_r[k] - r[k]); d[k, 1] = dy[k] / r[k] * (novo_r[k] - r[k])
        M = np.zeros((n, 3, 3))
        for q in range(4): M += wt[:, q, None, None] * J[jn[:, q]][:, :3, :3]
        pos[k] += np.einsum('nij,nj->ni', np.linalg.inv(M[k]), d[k])
        a['POSITION'] = g.add_acc(pos.astype(np.float32), 'VEC3')
        j['accessors'][a['POSITION']].update(min=pos.min(0).tolist(), max=pos.max(0).tolist())
        mexidos += len(k)
    mesh.setdefault('extras', {})['alca_aberta'] = 1
    print(f"   alça aberta: furo de {dentro * 1000:.1f} para {FURO * 1000:.1f} mm de raio, aro de {ARO * 1000:.1f} mm ({mexidos} vértices)")
    return True


def fazer(path, ficha=None, previa=None):
    c = Conjunto(path)
    parado, andar, tiro = c.clip('parado'), c.clip('andar'), c.clip('tiro')
    abrir_alca(c, parado)
    alca, massa, malha, furo, _ = medir(c, parado)
    print(f"{os.path.basename(path)}: alça {alca.round(4)}, massa {massa.round(4)} (m, do olho)")

    # A raiz parada e o que a leva ao espaço do olho.
    W0 = c.world(parado, c.root, 0.0)
    Wpai = c.world(parado, c.parent[c.root], 0.0)
    pivo = (c.world(parado, c.names.index('Grip'), 0.0))[:3, 3]

    # Mirando: a linha alça→massa no eixo do olho, e a arma mais perto.
    desce = np.degrees(np.arctan2(massa[1] - alca[1], alca[2] - massa[2]))      # se a linha não é paralela ao eixo
    vira = np.degrees(np.arctan2(massa[0] - alca[0], alca[2] - massa[2]))
    giro_mira = euler(-desce, vira, 0.0)
    alca_girada = pivo + giro_mira @ (alca - pivo)
    move_mira = np.array([-alca_girada[0], -alca_girada[1], -alca_girada[2] - OLHO])      # a alça a OLHO metros do olho

    def raiz(D):
        """A raiz local que põe o conjunto em D · (onde ele estava)."""
        return np.linalg.inv(Wpai) @ D @ W0

    def pose(p_sobe, p_entra, p_perto, pitch=0.0, yaw=0.0, roll=0.0, extra=(0, 0, 0)):
        move = np.array([move_mira[0] * p_entra, move_mira[1] * p_sobe, move_mira[2] * p_perto]) + np.asarray(extra, float)
        k = (p_sobe + p_entra) / 2
        giro = euler(-desce * k + pitch, vira * k + yaw, roll)
        return rigido(move, giro, pivo)

    # ---- da cintura ao olho
    def entrar(t):
        u = t / ENTRAR
        fim = suave((u - 0.72) / 0.28)                                 # garante o ponto exato no último quadro
        sobe = mola(t + 0.012) * (1 - fim) + fim                       # sobe primeiro…
        entra = mola(t - 0.012) * (1 - fim) + fim                      # …e entra depois: um arco
        perto = suave(u / 0.8)
        meio = sino(u / 0.85, 1.3)
        return raiz(pose(sobe, entra, perto, pitch=-1.6 * sino(u / 0.6), yaw=2.2 * meio, roll=6.5 * meio))

    # ---- do olho à cintura
    def sair(t):
        u = t / SAIR
        fim = suave((u - 0.7) / 0.3)
        cai = 1 - (mola(t + 0.008, 21.0, 0.74) * (1 - fim) + fim)
        solta = 1 - (mola(t - 0.010, 21.0, 0.74) * (1 - fim) + fim)
        perto = 1 - suave(u / 0.75)
        meio = sino(u / 0.9, 1.2)
        return raiz(pose(cai, solta, perto, pitch=1.2 * sino(u / 0.5), yaw=-1.0 * meio, roll=-4.0 * meio))

    # ---- na mira, sobre um clipe de origem: desfaz parte do movimento que ele dá à arma
    R0 = W0[:3, :3]                                       # do osso da arma (filho direto da raiz) ao olho, com a escala
    def sobre(clip, fica, ref):
        g0 = c.sample(parado[(c.gun, 'translation')], 0.0)
        def at(t):
            d = R0 @ (c.sample(clip[(c.gun, 'translation')], t) - g0)           # onde o clipe pôs a arma, em relação à pose medida
            alvo = fica * (d - ref(d_of=lambda tt: R0 @ (c.sample(clip[(c.gun, 'translation')], tt) - g0)))
            return raiz(rigido(move_mira + (alvo - d), giro_mira, pivo))
        return at
    zero = lambda d_of: d_of(0.0)                         # em relação ao começo do clipe
    def media(clip, length):
        m = None
        def f(d_of):
            nonlocal m
            if m is None: m = np.mean([d_of(i / FPS) for i in range(int(length * FPS))], axis=0)
            return m
        return f

    dur = lambda clip: max(ch['t'][-1] for ch in clip.values())
    c.add_clip('mirar', parado, ENTRAR, entrar, parado0=True)
    c.add_clip('desmirar', parado, SAIR, sair, parado0=True)
    c.add_clip('mira', parado, dur(parado), sobre(parado, RESPIRA, zero))
    c.add_clip('mira_andar', andar, dur(andar), sobre(andar, BALANCA, media(andar, dur(andar))))
    c.add_clip('mira_tiro', tiro, dur(tiro), sobre(tiro, RECUA, zero))
    c.g.save(path)

    # ---- confere: na mira, a alça e a massa no eixo do olho
    feito = Conjunto(path, limpar=False); mira = feito.clip('mira'); v = feito.skinned(mira, 'fuzil', 0.0)
    a2, m2, _, furo2, _ = medir(feito, mira)
    print(f"   na mira: alça {a2.round(4)}, massa {m2.round(4)}; fora do eixo {np.abs([a2[0], a2[1], m2[0], m2[1]]).max() * 1000:.2f} mm")
    assert np.abs([a2[0], a2[1], m2[0], m2[1]]).max() < 5e-4, "a mira não ficou no eixo do olho"
    for nome in ('mira', 'mira_andar', 'mira_tiro'):
        cl = feito.clip(nome); L = max(ch['t'][-1] for ch in cl.values())
        pts = np.array([(feito.world(cl, feito.gun, i / FPS))[:3, 3] for i in range(int(L * FPS) + 1)])
        print(f"   {nome}: {L:.2f} s, a arma anda {(np.ptp(pts, axis=0) * 1000).round(2)} mm (x, y, z)")
    fim_entrar = feito.world(feito.clip('mirar'), feito.gun, ENTRAR)[:3, 3]; ini_mira = feito.world(mira, feito.gun, 0.0)[:3, 3]
    fim_sair = feito.world(feito.clip('desmirar'), feito.gun, SAIR)[:3, 3]; ini_parado = c.world(parado, c.gun, 0.0)[:3, 3]
    print(f"   emendas: mirar→mira {np.linalg.norm(fim_entrar - ini_mira) * 1000:.3f} mm, desmirar→parado {np.linalg.norm(fim_sair - ini_parado) * 1000:.3f} mm")

    # ---- a ficha: os clipes e a boca do cano no osso da arma
    if ficha and os.path.exists(ficha):
        d = json.load(open(ficha, encoding='utf-8'))
        alvo = "res://" + os.path.relpath(os.path.abspath(path), os.path.abspath(os.path.join(os.path.dirname(ficha), "..", "..", "..", ".."))).replace(os.sep, "/")   # a raiz do projeto (a ficha está em src/assets/armas/fp)
        for nome, e in d.items():
            if e.get('arquivo') != alvo: continue
            for clipe in NOVOS:
                cl = feito.clip(clipe)
                e['animacoes'][clipe] = {'ate': round(float(max(ch['t'][-1] for ch in cl.values())), 4), 'clipe': clipe, 'de': 0.0}
            boca = np.array(e['cano'], float)
            Wg = c.world(parado, c.gun, 0.0)
            e['cano_osso'] = {'osso': 'ik_hand_gun', 'ponto': [round(float(x), 4) for x in (np.linalg.inv(Wg) @ np.append(boca, 1.0))[:3]]}
            print(f"   ficha: {nome} com {len(NOVOS)} clipes e a boca do cano no osso")
        json.dump(d, open(ficha, 'w', encoding='utf-8'), indent=1, ensure_ascii=False, sort_keys=True)
        open(ficha, 'a', encoding='utf-8').write("\n")

    if previa:
        import matplotlib; matplotlib.use("Agg"); import matplotlib.pyplot as plt
        os.makedirs(previa, exist_ok=True)
        cl_in, cl_out = feito.clip('mirar'), feito.clip('desmirar')
        fig, axs = plt.subplots(2, 6, figsize=(30, 10), facecolor="#14161c")
        for col, t in enumerate(np.linspace(0, ENTRAR, 6)):
            for row, (cl, tt) in enumerate(((cl_in, t), (cl_out, t * SAIR / ENTRAR))):
                ax = axs[row][col]; ax.set_facecolor("#14161c")
                f = feito.skinned(cl, 'fuzil', float(tt)); b = feito.skinned(cl, 'bracos', float(tt))
                for pts, cor, s in ((b, "#b98", 0.4), (f, "#ddd", 0.5)):
                    z = -pts[:, 2]; ok = z > 0.05
                    ax.scatter(pts[ok, 0] / z[ok], pts[ok, 1] / z[ok], s=s, c=cor)
                ax.plot([0], [0], "r+", ms=14); ax.set_xlim(-0.9, 0.9); ax.set_ylim(-0.62, 0.3); ax.set_aspect("equal"); ax.axis("off")
                ax.set_title(f"{'mirar' if row == 0 else 'desmirar'} {tt:.3f} s", color="w")
        plt.tight_layout(); plt.savefig(os.path.join(previa, os.path.splitext(os.path.basename(path))[0] + "_mira.png"), dpi=50, facecolor="#14161c")


if __name__ == "__main__":
    a = sys.argv[1:]
    ficha = a[a.index("--ficha") + 1] if "--ficha" in a else None
    previa = a[a.index("--previa") + 1] if "--previa" in a else None
    arquivos = [x for i, x in enumerate(a) if not x.startswith("--") and (i == 0 or a[i - 1] not in ("--ficha", "--previa"))]
    for p in arquivos: fazer(p, ficha, previa)
