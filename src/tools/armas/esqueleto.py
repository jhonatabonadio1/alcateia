#!/usr/bin/env python3
"""
O esqueleto dos braços de primeira pessoa (o do AR-15, 94 ossos) para quem
escreve animação por código: poses, cinemática direta e inversa, mãos presas na
arma, e a gravação de clipes e de malhas no .glb.

Usado por ``conjuntos.py`` (as armas do pacote Weapons Lite). As contas são as
mesmas de ``recarga.py`` e ``coronhada.py``, juntas num lugar.

Convenções:

- uma **pose** é ``{nó: (T[3], Q[4])}``, o local de cada osso abaixo de ``root``;
- o **espaço da raiz** é o do osso ``root``, em cm (o conjunto tem escala 0,01);
- o **espaço do olho** é o da câmera do jogo, em metros (x direita, y cima, −z
  frente): ``olho(m)`` leva uma 4×4 da raiz para lá e ``raiz(m)`` traz de volta;
- o **espaço da arma** é o do ``WeaponModel`` (origem no punho, cano para −Z, Y
  para cima, metros). ``ARMA`` leva dele para o do osso ``ik_hand_gun`` (cm,
  cano para −Y, Z para cima, X para a esquerda).
"""
import os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'rugido'))
from mira import Conjunto, matq, qmat, trs
from coronhada import rot, arco

UM = np.array([0.0, 0.0, 0.0, 1.0])
# espaço da arma (m) -> osso da arma (cm): (x, y, z) -> (−100x, 100z, 100y)
ARMA = np.array([[-100.0, 0, 0, 0], [0, 0, 100.0, 0], [0, 100.0, 0, 0], [0, 0, 0, 1]])
DEDOS = ('thumb', 'index', 'middle', 'ring', 'pinky')


# ---------------------------------------------------------------- contas
def suave(u):
    u = min(max(u, 0.0), 1.0)
    return u * u * u * (u * (u * 6 - 15) + 10)

def qmul(a, b):
    x1, y1, z1, w1 = a; x2, y2, z2, w2 = b
    return np.array([w1 * x2 + x1 * w2 + y1 * z2 - z1 * y2, w1 * y2 - x1 * z2 + y1 * w2 + z1 * x2,
                     w1 * z2 + x1 * y2 - y1 * x2 + z1 * w2, w1 * w2 - x1 * x2 - y1 * y2 - z1 * z2])

def qinv(q): return np.array([-q[0], -q[1], -q[2], q[3]])

def slerp(a, b, u):
    if np.dot(a, b) < 0: b = -b
    d = float(np.clip(np.dot(a, b), -1, 1))
    if d > 0.9995:
        q = a + (b - a) * u; return q / np.linalg.norm(q)
    th = np.arccos(d)
    return (np.sin((1 - u) * th) * a + np.sin(u * th) * b) / np.sin(th)

def rigida(m):
    """A 4×4 sem escala."""
    o = np.array(m, float).copy(); o[:3, :3] = rot(o); return o

def entre(a, b, u):
    """Entre duas 4×4 rígidas: a posição em linha reta, o giro pelo caminho mais curto."""
    return trs(a[:3, 3] * (1 - u) + b[:3, 3] * u, slerp(matq(rot(a)), matq(rot(b)), u))

def parte(m, u):
    """A 4×4 rígida m aplicada só em parte (0 = nada, 1 = inteira)."""
    return entre(np.eye(4), m, u)

def eixo_angulo(eixo, graus):
    e = np.asarray(eixo, float); e = e / np.linalg.norm(e); a = np.radians(graus) / 2
    return np.append(e * np.sin(a), np.cos(a))

def giro(pitch=0.0, yaw=0.0, roll=0.0):
    """4×4 no espaço do olho ou da arma (graus): pitch + levanta o cano, yaw + leva o cano para a esquerda, roll + tomba o topo para a esquerda."""
    a, b, c = np.radians([pitch, yaw, roll])
    rx = np.array([[1, 0, 0], [0, np.cos(a), -np.sin(a)], [0, np.sin(a), np.cos(a)]])
    ry = np.array([[np.cos(b), 0, np.sin(b)], [0, 1, 0], [-np.sin(b), 0, np.cos(b)]])
    rz = np.array([[np.cos(c), -np.sin(c), 0], [np.sin(c), np.cos(c), 0], [0, 0, 1]])
    m = np.eye(4); m[:3, :3] = ry @ rx @ rz; return m

def mover(x=0.0, y=0.0, z=0.0):
    m = np.eye(4); m[:3, 3] = (x, y, z); return m

def em_torno(pivo, m):
    """A 4×4 m (um giro) em torno do ponto pivo."""
    return mover(*pivo) @ m @ mover(*(-np.asarray(pivo, float)))


class Chaves:
    """Uma curva por chaves ``(tempo, valor)``, com entrada e saída suaves entre elas
    (``facil``) ou em linha reta. O valor pode ser número ou vetor."""
    def __init__(self, *chaves, facil=True):
        self.t = np.array([c[0] for c in chaves], float); self.v = np.array([c[1] for c in chaves], float); self.facil = facil
    def __call__(self, t):
        if t <= self.t[0]: return self.v[0]
        if t >= self.t[-1]: return self.v[-1]
        i = int(np.searchsorted(self.t, t, side='right')) - 1
        u = (t - self.t[i]) / (self.t[i + 1] - self.t[i])
        if self.facil: u = u * u * (3 - 2 * u)
        return self.v[i] * (1 - u) + self.v[i + 1] * u


class Curva:
    """Como ``Chaves``, mas **sem parar em cada chave**: passa por elas com a velocidade contínua (uma spline de Hermite
    com as tangentes de Catmull-Rom, limitadas para não passar do ponto). É o que deixa um gesto de várias chaves fluido:
    com ``Chaves``, cada chave é uma parada. Nas pontas chega e parte parada. O valor pode ser número ou vetor."""
    def __init__(self, *chaves):
        t = self.t = np.array([c[0] for c in chaves], float); v = self.v = np.array([np.atleast_1d(np.asarray(c[1], float)) for c in chaves])
        self.escalar = np.ndim(chaves[0][1]) == 0; d = (v[1:] - v[:-1]) / (t[1:] - t[:-1])[:, None]; m = np.zeros_like(v)
        for i in range(1, len(t) - 1):
            cr = (v[i + 1] - v[i - 1]) / (t[i + 1] - t[i - 1]); teto = 3 * np.minimum(np.abs(d[i - 1]), np.abs(d[i]))
            m[i] = np.where(d[i - 1] * d[i] > 0, np.clip(cr, -teto, teto), 0.0)
        self.m = m
    def __call__(self, t):
        T, v, m = self.t, self.v, self.m
        if t <= T[0]: out = v[0]
        elif t >= T[-1]: out = v[-1]
        else:
            i = int(np.searchsorted(T, t, side='right')) - 1; h = T[i + 1] - T[i]; u = (t - T[i]) / h
            out = (2 * u ** 3 - 3 * u ** 2 + 1) * v[i] + (u ** 3 - 2 * u ** 2 + u) * h * m[i] + (-2 * u ** 3 + 3 * u ** 2) * v[i + 1] + (u ** 3 - u ** 2) * h * m[i + 1]
        return float(out[0]) if self.escalar else out


def mola(t, freq=3.2, amort=0.5):
    """Resposta de uma mola a um empurrão em t=0: sobe depressa, passa do ponto e assenta (0 em t≤0)."""
    if t <= 0: return 0.0
    w = 2 * np.pi * freq
    return float(np.exp(-amort * w * t) * np.sin(w * np.sqrt(max(1 - amort * amort, 1e-4)) * t))


# ---------------------------------------------------------------- o esqueleto
class Esqueleto:
    def __init__(self, path):
        self.path = path
        self.c = c = Conjunto(path, limpar=False); self.j = c.j; self.g = c.g
        self.N = c.names.index; self.raiz_no = self.N('root')
        self.filhos = {}
        for filho, pai in c.parent.items(): self.filhos.setdefault(pai, []).append(filho)
        self.ossos = [i for i in self.abaixo(self.raiz_no) if i != self.raiz_no]
        self.nome = {i: c.names[i] for i in self.ossos}
        self.clipes = {}
        self.parado = self.pose_do_clipe('parado', 0.0)
        # raiz (cm) -> olho (m): o conjunto já vem com o olho na origem
        self.R2O = c.world(self.clipe('parado'), self.raiz_no, 0.0)
        self.O2R = np.linalg.inv(self.R2O)
        self.arma = self.N('ik_hand_gun')
        self.mao = {'r': self.N('hand_r'), 'l': self.N('hand_l')}
        self.lado = {'r': set(self.abaixo(self.N('clavicle_r'))), 'l': set(self.abaixo(self.N('clavicle_l')))}
        self.dedos = {l: [i for i in self.abaixo(self.mao[l]) if i != self.mao[l]] for l in 'rl'}
        self._espelho = None

    def abaixo(self, i):
        out = [i]
        for f in self.filhos.get(i, []): out += self.abaixo(f)
        return out

    def clipe(self, nome):
        if nome not in self.clipes: self.clipes[nome] = self.c.clip(nome)
        return self.clipes[nome]

    def duracao(self, nome): return float(max(ch['t'][-1] for ch in self.clipe(nome).values()))

    def pose_do_clipe(self, nome, t):
        cl = self.clipe(nome); pose = {}
        for i in self.ossos:
            n = self.j['nodes'][i]
            T = self.c.sample(cl[(i, 'translation')], t) if (i, 'translation') in cl else np.array(n.get('translation', [0, 0, 0]), float)
            Q = self.c.sample(cl[(i, 'rotation')], t) if (i, 'rotation') in cl else np.array(n.get('rotation', [0, 0, 0, 1]), float)
            pose[i] = (np.array(T, float), np.array(Q, float))
        return pose

    def mundo(self, pose, i):
        """O nó no espaço da raiz (cm)."""
        if isinstance(i, str): i = self.N(i)
        m = np.eye(4)
        while i != self.raiz_no:
            m = trs(*pose[i]) @ m; i = self.c.parent[i]
        return m

    def por_local(self, pose, i, m):
        """Põe o nó na 4×4 m do espaço da raiz (mexendo só no local dele)."""
        if isinstance(i, str): i = self.N(i)
        loc = np.linalg.inv(self.mundo(pose, self.c.parent[i])) @ m if self.c.parent[i] != self.raiz_no else m
        pose[i] = (loc[:3, 3].copy(), matq(rot(loc)))

    def olho(self, m): return rigida(self.R2O @ m)
    def raiz(self, m):
        o = self.O2R @ m; s = np.linalg.norm(o[:3, 0]); o[:3, :3] /= s; return o

    # ---- a arma e as mãos
    def arma_de(self, pose): return self.mundo(pose, self.arma)

    def por_arma(self, pose, G): self.por_local(pose, self.arma, G)

    def mao_na_arma(self, pose, lado):
        """A mão no espaço do osso da arma (cm)."""
        return np.linalg.inv(self.arma_de(pose)) @ self.mundo(pose, self.mao[lado])

    def braco(self, pose, lado, alvo, antes=None):
        """Leva a mão ao alvo (4×4 no espaço da raiz) recalculando o braço, com o ombro
        onde está. O cotovelo dobra para o lado em que dobrava em ``antes`` (uma pose;
        por omissão a própria). Devolve o que faltou alcançar (cm)."""
        cl, ua, la, hd = (self.N(f'{o}_{lado}') for o in ('clavicle', 'upperarm', 'lowerarm', 'hand'))
        # Se a mão não alcança o alvo, o ombro vai junto (fica fora da tela): a mão não larga a arma.
        S = self.mundo(pose, ua)[:3, 3]; alcance = (np.linalg.norm(pose[la][0]) + np.linalg.norm(pose[hd][0])) * 0.985
        d = np.linalg.norm(alvo[:3, 3] - S)
        if d > alcance:
            pai = self.mundo(pose, self.c.parent[cl]); passo = (alvo[:3, 3] - S) / d * (d - alcance)
            pose[cl] = (pose[cl][0] + rot(pai).T @ passo, pose[cl][1])
        Wc, Wu, Wl, Wh = (self.mundo(pose, i) for i in (cl, ua, la, hd))
        S, E0, H0 = Wu[:3, 3], Wl[:3, 3], Wh[:3, 3]
        dobra = E0 if antes is None else S + (self.mundo(antes, la)[:3, 3] - self.mundo(antes, ua)[:3, 3])
        L1, L2 = np.linalg.norm(E0 - S), np.linalg.norm(H0 - E0)
        H = alvo[:3, 3].copy(); d = np.linalg.norm(H - S); falta = max(0.0, d - (L1 + L2) * 0.999)
        if falta > 0: H = S + (H - S) / d * (L1 + L2) * 0.999; d = (L1 + L2) * 0.999
        eixo = (H - S) / d
        a = (L1 ** 2 - L2 ** 2 + d * d) / (2 * d); h = np.sqrt(max(L1 ** 2 - a * a, 0.0))
        polo = dobra - S; polo = polo - np.dot(polo, eixo) * eixo
        if np.linalg.norm(polo) < 1e-6: polo = np.cross(eixo, [0.0, 0.0, 1.0])
        polo /= np.linalg.norm(polo)
        E = S + eixo * a + polo * h
        Ru = arco(E0 - S, E - S) @ rot(Wu); Rl = arco(H0 - E0, H - E) @ rot(Wl)
        pose[ua] = (pose[ua][0], matq(rot(Wc).T @ Ru))
        pose[la] = (pose[la][0], matq(Ru.T @ Rl))
        pose[hd] = (pose[hd][0], matq(Rl.T @ rot(alvo)))
        return falta

    def prender(self, pose, G, pega, antes=None):
        """A arma em G e as mãos nela: ``pega = {'r': 4×4, 'l': 4×4}``, cada mão no espaço
        do osso da arma (ou ausente: a mão fica onde a pose a pôs). Acerta também os ossos
        ``ik_hand_*``. Devolve o que faltou alcançar."""
        self.por_arma(pose, G); falta = 0.0
        for lado in 'rl':
            if lado in pega and pega[lado] is not None: falta = max(falta, self.braco(pose, lado, G @ pega[lado], antes))
            ik = f'ik_hand_{lado}'
            if ik in self.c.names: self.por_local(pose, ik, self.mundo(pose, self.mao[lado]))
        return falta

    # ---- dedos
    def dedos_de(self, pose, lado): return {i: pose[i] for i in self.dedos[lado]}

    def por_dedos(self, pose, dedos, peso=1.0):
        for i, (T, Q) in dedos.items():
            pose[i] = (pose[i][0] * (1 - peso) + T * peso, slerp(pose[i][1], Q, peso))

    def espelhar_dedos(self, dedos, para):
        """A pose de dedos de uma mão na outra. No manequim os ossos dos dois lados são
        espelho um do outro: o giro local é o mesmo e a translação troca de sinal."""
        de = 'r' if para == 'l' else 'l'; out = {}
        for i, (T, Q) in dedos.items():
            out[self.N(self.nome[i][:-2] + '_' + para)] = (-np.asarray(T), np.array(Q))
        return out

    def fechar_indicador(self, pose, lado, peso=1.0):
        """O indicador dobra como o dedo médio (para segurar um cabo: faca, granada)."""
        for k in ('01', '02', '03'):
            a, b = self.N(f'index_{k}_{lado}'), self.N(f'middle_{k}_{lado}')
            pose[a] = (pose[a][0], slerp(pose[a][1], pose[b][1], peso))

    # ---- gravar
    def gravar_clipe(self, nome, quadros, duracao, fps=60.0, extras=None):
        """Um clipe novo (ou no lugar do antigo) a partir de uma lista de poses, uma por quadro."""
        j = self.j; n = len(quadros)
        ts = np.arange(n) / fps; ts[-1] = duracao
        base = self.clipe('parado')
        t_in = self.c.add_times(ts); dois = self.c.add_times([0.0, duracao])
        samplers, channels = [], []
        def por(node, caminho, entrada, saida, interp):
            samplers.append({'input': entrada, 'output': saida, 'interpolation': interp})
            channels.append({'sampler': len(samplers) - 1, 'target': {'node': node, 'path': caminho}})
        for (node, caminho), ch in base.items():
            v = None
            if node in quadros[0] and caminho in ('translation', 'rotation'):
                v = np.array([q[node][0 if caminho == 'translation' else 1] for q in quadros], float)
                if caminho == 'rotation':
                    for i in range(1, n):
                        if np.dot(v[i], v[i - 1]) < 0: v[i] = -v[i]
                if np.abs(v - v[0]).max() < 1e-7: v = None
            if v is None:
                fixo = quadros[0][node][0 if caminho == 'translation' else 1] if node in quadros[0] and caminho != 'scale' else ch['v'][0]
                tipo = {1: 'SCALAR', 3: 'VEC3', 4: 'VEC4'}[len(fixo)]
                por(node, caminho, dois, self.g.add_acc(np.stack([fixo, fixo]), tipo), 'STEP')
            else:
                por(node, caminho, t_in, self.g.add_acc(v, 'VEC3' if caminho == 'translation' else 'VEC4'), 'LINEAR')
        j['animations'] = [a for a in j['animations'] if a['name'] != nome]
        an = {'name': nome, 'channels': channels, 'samplers': samplers}
        if extras: an['extras'] = extras
        j['animations'].append(an)
        self.clipes.pop(nome, None)

    # ---- malhas
    def _cru(self, arr, componente, tipo, alvo=None):
        """Um accessor de qualquer tipo (o ``add_acc`` do Glb só grava float)."""
        g = self.g; arr = np.ascontiguousarray(arr)
        while len(g.bin) % 4: g.bin.append(0)
        off = len(g.bin); g.bin += arr.tobytes()
        bv = {'buffer': 0, 'byteOffset': off, 'byteLength': arr.nbytes}
        if alvo: bv['target'] = alvo
        self.j['bufferViews'].append(bv)
        a = {'bufferView': len(self.j['bufferViews']) - 1, 'componentType': componente, 'count': int(arr.shape[0]), 'type': tipo}
        if tipo == 'VEC3' and componente == 5126: a['min'] = arr.min(0).tolist(); a['max'] = arr.max(0).tolist()
        self.j['accessors'].append(a); return len(self.j['accessors']) - 1

    def material_paleta(self, png, nome='paleta', metal=0.25, rugosidade=0.6):
        """Um material com a paleta (um .png) embutida; cada face aponta para uma célula dela."""
        j = self.j; dados = open(png, 'rb').read()
        while len(self.g.bin) % 4: self.g.bin.append(0)
        off = len(self.g.bin); self.g.bin += dados
        j['bufferViews'].append({'buffer': 0, 'byteOffset': off, 'byteLength': len(dados)})
        j.setdefault('images', []).append({'bufferView': len(j['bufferViews']) - 1, 'mimeType': 'image/png', 'name': nome})
        j.setdefault('samplers', []).append({'magFilter': 9728, 'minFilter': 9728, 'wrapS': 33071, 'wrapT': 33071})
        j.setdefault('textures', []).append({'sampler': len(j['samplers']) - 1, 'source': len(j['images']) - 1})
        j['materials'].append({'name': nome, 'pbrMetallicRoughness': {'baseColorTexture': {'index': len(j['textures']) - 1}, 'metallicFactor': metal, 'roughnessFactor': rugosidade}})
        return len(j['materials']) - 1

    def ligacao(self, osso):
        """A 4×4 de repouso (da malha) de um osso, tirada da pele da arma."""
        no = [n for n in self.j['nodes'] if 'mesh' in n and 'skin' in n][0]; pele = self.j['skins'][no['skin']]
        ibm = self.g.acc(pele['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1).astype(float)
        k = pele['joints'].index(self.N(osso)); return np.linalg.inv(ibm[k]), k

    def por_malha(self, no_nome, partes, material, novo_nome=None):
        """Troca a malha do nó por peças rígidas: ``partes = [(malha, osso)]``, cada malha com
        ``pos``/``normal``/``uv`` por triângulo, no espaço da arma (m); cada peça segue um osso."""
        j = self.j; no = [n for n in j['nodes'] if n.get('name') == no_nome][0]
        Bg, _ = self.ligacao('ik_hand_gun'); M = Bg @ ARMA; Rn = rot(M)
        P, Nn, UV, J = [], [], [], []
        for m, osso in partes:
            _, k = self.ligacao(osso); p = m['pos'].reshape(-1, 3)
            P.append((np.c_[p, np.ones(len(p))] @ M.T)[:, :3]); Nn.append(m['normal'].reshape(-1, 3) @ Rn.T)
            uv = m['uv'].reshape(-1, 2).copy(); uv[:, 1] = 1 - uv[:, 1]; UV.append(uv)
            jj = np.zeros((len(p), 4), np.uint8); jj[:, 0] = k; J.append(jj)
        P, Nn, UV, J = (np.concatenate(x) for x in (P, Nn, UV, J))
        Nn /= np.linalg.norm(Nn, axis=1, keepdims=True) + 1e-12
        W = np.zeros((len(P), 4), np.float32); W[:, 0] = 1
        prim = {'attributes': {'POSITION': self._cru(P.astype(np.float32), 5126, 'VEC3', 34962), 'NORMAL': self._cru(Nn.astype(np.float32), 5126, 'VEC3', 34962),
                               'TEXCOORD_0': self._cru(UV.astype(np.float32), 5126, 'VEC2', 34962), 'JOINTS_0': self._cru(J, 5121, 'VEC4', 34962),
                               'WEIGHTS_0': self._cru(W, 5126, 'VEC4', 34962)}, 'material': material, 'mode': 4}
        j['meshes'][no['mesh']] = {'name': novo_nome or no_nome, 'primitives': [prim]}
        if novo_nome: no['name'] = novo_nome; self.c.names[j['nodes'].index(no)] = novo_nome

    def tirar_no(self, no_nome):
        """Tira um nó de malha da cena (a malha dele some na poda)."""
        k = [i for i, n in enumerate(self.j['nodes']) if n.get('name') == no_nome][0]
        for n in self.j['nodes']:
            if k in n.get('children', []): n['children'].remove(k)
        self.j['nodes'][k].pop('mesh', None); self.j['nodes'][k].pop('skin', None)

    def podar(self):
        """Tira as malhas, materiais, texturas e imagens que nada mais usa, e os dados soltos."""
        from recarga import compactar
        j = self.j
        def refaz(lista, usados):
            mapa = {v: k for k, v in enumerate(sorted(usados))}; return [lista[i] for i in sorted(usados)], mapa
        j['meshes'], mm = refaz(j['meshes'], {n['mesh'] for n in j['nodes'] if 'mesh' in n})
        for n in j['nodes']:
            if 'mesh' in n: n['mesh'] = mm[n['mesh']]
        j['materials'], ma = refaz(j['materials'], {p['material'] for m in j['meshes'] for p in m['primitives'] if 'material' in p})
        for m in j['meshes']:
            for p in m['primitives']:
                if 'material' in p: p['material'] = ma[p['material']]
        def texturas(o):
            if isinstance(o, dict):
                for k, v in o.items():
                    if isinstance(v, dict) and 'index' in v and k.endswith('Texture'): yield v
                    else: yield from texturas(v)
            elif isinstance(o, list):
                for v in o: yield from texturas(v)
        refs = list(texturas(j['materials']))
        j['textures'], mt = refaz(j['textures'], {r['index'] for r in refs})
        for r in refs: r['index'] = mt[r['index']]
        j['images'], mi = refaz(j['images'], {t['source'] for t in j['textures']})
        for t in j['textures']: t['source'] = mi[t['source']]
        compactar(self.g)

    def salvar(self, path=None):
        self.podar(); self.g.save(path or self.path)
