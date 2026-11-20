"""
As peças de um kit (.fbx) para o gerador de mapas, sem Godot nem Blender na hora de gerar.

``extrair.gd`` (roda no Godot, uma vez por kit) grava a geometria de cada peça em ``<kit>.json`` + ``<kit>.bin``;
``python3 tools/mapas/kits/kit_fbx.py <kit> <pasta com .json/.bin> <textura.png>`` junta os dois em
``tools/mapas/kits/<kit>.npz`` (o que os mapas leem) e copia a textura para ``assets/mapas/kits/<kit>.png``.

    from mapas.kits.kit_fbx import Kit
    k = Kit('fps1'); p = k['House_01']      # p.pos (T×3×3, m), p.normal, p.uv, p.lo, p.hi, p.tam
"""
import json
import os
import sys

import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(AQUI)))


# Peças que vêm do .fbx em centímetros (cem vezes maiores que as outras do mesmo kit) e as que vêm deitadas (o alto
# delas em −Z). Vistas uma a uma nas miniaturas (kit por kit); o resto vem em metros e em pé.
EM_CM = dict(
    fps1=('Grass_01', 'RoadSign_02'),
    fps2=('Block_', 'Book_', 'Cup_', 'Food_', 'Jug_', 'Plate_', 'Scroll_'),
    fps3=('Bin_01', 'Clock_01', 'CoffeeCup_01', 'DeskLamp_01', 'Fountain_01', 'Light_01', 'Mushroom_01', 'Notebook_', 'Office_01', 'PC_',
          'Phone_01', 'RoadSign_01', 'TeaCup_01'),
    fps5=('Light_02', 'Light_03', 'HousePart_02_2', 'HousePart_03', 'WallDecor_01_2'),
    fps6=('Brick_', 'Brush_', 'Dye_', 'Hammer_01', 'Sign_01'),
)
DEITADA = dict(fps3=('Office_01', 'Fountain_01'))


class Peca:
    def __init__(self, nome, pos, normal, uv):
        self.nome, self.pos, self.normal, self.uv = nome, pos, normal, uv
        q = pos.reshape(-1, 3)
        self.lo, self.hi = (q.min(0), q.max(0)) if len(q) else (np.zeros(3), np.zeros(3))
        self.tam = self.hi - self.lo

    def __repr__(self):
        return f"<{self.nome}: {len(self.pos)} tri, {self.tam.round(2)}>"


class Kit:
    def __init__(self, nome):
        self.nome = nome
        d = np.load(os.path.join(AQUI, nome + '.npz'), allow_pickle=False)
        nomes = [str(n) for n in d['nomes']]; ini = d['inicio']; tri = d['tri'].astype(np.float32)
        self.pecas = {}
        for k, n in enumerate(nomes):
            t = tri[ini[k]:ini[k + 1]]
            self.pecas[n] = Peca(n, t[:, :, 0:3].astype(float), t[:, :, 3:6].astype(float), t[:, :, 6:8].astype(float))
        self.textura = os.path.join(RAIZ, 'assets', 'mapas', 'kits', nome + '.png')

    def __getitem__(self, nome):
        return self.pecas[nome]

    def recorte(self, novo, nome, eixo, de=-1e9, ate=1e9):
        """Cria a peça ``novo``: a ``nome`` cortada, só o que fica entre ``de`` e ``ate`` no ``eixo`` (0 = x, 1 = y,
        2 = z). O triângulo que atravessa o corte é partido (a posição, a normal e a textura acompanham). Para usar um
        arco só da parede de dois arcos do túnel, por exemplo."""
        p = self.pecas[nome]; V = np.concatenate([p.pos, p.normal, p.uv], axis=2)          # T × 3 × 8
        for lim, sinal in ((de, 1.0), (ate, -1.0)):
            if abs(lim) > 1e8: continue
            out = []
            for t in V:
                d = (t[:, eixo] - lim) * sinal; pol = []
                for i in range(3):
                    a_, b_ = t[i], t[(i + 1) % 3]; da, db = d[i], d[(i + 1) % 3]
                    if da >= 0: pol.append(a_)
                    if (da >= 0) != (db >= 0): pol.append(a_ + (b_ - a_) * (da / (da - db)))
                for i in range(1, len(pol) - 1): out.append(np.array([pol[0], pol[i], pol[i + 1]]))
            V = np.array(out) if out else np.zeros((0, 3, 8))
        self.pecas[novo] = Peca(novo, V[:, :, 0:3].copy(), V[:, :, 3:6].copy(), V[:, :, 6:8].copy())
        return self.pecas[novo]

    # ---- peças feitas aqui, da cor de um ponto da textura do kit (o asfalto, a calçada, a faixa pintada)
    def uv_da_cor(self, rgb):
        """O ponto da textura (u, v) de cor mais parecida com ``rgb`` (0..1), no meio de uma região lisa."""
        from PIL import Image
        if not hasattr(self, '_tex'): self._tex = np.asarray(Image.open(self.textura).convert('RGB').resize((256, 256), Image.BOX), float) / 255
        t = self._tex; d = ((t - np.array(rgb)) ** 2).sum(2)
        liso = sum(((np.roll(t, (dy, dx), (0, 1)) - t) ** 2).sum(2) for dy, dx in ((0, 2), (2, 0), (0, -2), (-2, 0)))
        y, x = np.unravel_index(np.argmin(d + liso * 4), d.shape)
        return ((x + 0.5) / 256, (y + 0.5) / 256), tuple(t[y, x].round(3))

    def chapa(self, nome, rgb):
        """Cria a peça ``nome``: uma caixa de 1 × 1 × 1 m (a base em y = 0, o meio em x = z = 0), sem fundo, toda da cor
        ``rgb`` da textura. Põe-se com ``Montagem.chapa`` (esticada para o tamanho que for)."""
        (u, v), achada = self.uv_da_cor(rgb)
        c = np.array([[x, y, z] for x in (-0.5, 0.5) for y in (0.0, 1.0) for z in (-0.5, 0.5)])
        faces = [((2, 3, 7), (2, 7, 6), (0, 1, 0)), ((0, 4, 6), (0, 6, 2), (0, 0, -1)), ((1, 3, 7), (1, 7, 5), (0, 0, 1)),
                 ((0, 2, 3), (0, 3, 1), (-1, 0, 0)), ((4, 5, 7), (4, 7, 6), (1, 0, 0))]
        pos, nor = [], []
        for t1, t2, n in faces:
            for t in (t1, t2):
                p = c[list(t)]
                if np.dot(np.cross(p[1] - p[0], p[2] - p[0]), n) < 0: p = p[[0, 2, 1]]
                pos.append(p); nor.append([n] * 3)
        pos = np.array(pos, float); self.pecas[nome] = Peca(nome, pos, np.array(nor, float), np.tile([u, v], (len(pos), 3, 1)).astype(float))
        return achada

    def __contains__(self, nome):
        return nome in self.pecas

    def nomes(self):
        return sorted(self.pecas)


def converter(nome, pasta, textura):
    indice = json.load(open(os.path.join(pasta, nome + '.json'), encoding='utf-8'))
    dados = np.fromfile(os.path.join(pasta, nome + '.bin'), np.float32).reshape(-1, 3, 8)
    nomes, inicio, tris = [], [0], []
    for p in indice:
        partes = [dados[s['inicio'] // 24:s['inicio'] // 24 + s['vertices'] // 3] for s in p['superficies']]
        t = np.concatenate(partes) if partes else np.zeros((0, 3, 8), np.float32)
        q = t[:, :, :3].reshape(-1, 3)
        curto = os.path.basename(p['nome'])
        # a unidade: o Godot lê alguns arquivos cem vezes menores (do tamanho de uma moeda) e outros cem vezes maiores
        if len(q) and np.ptp(q, axis=0).max() < 0.5:
            t = t.copy(); t[:, :, :3] *= 100.0
        if curto.startswith(EM_CM.get(nome, ())):
            t = t.copy(); t[:, :, :3] *= 0.01
        if curto in DEITADA.get(nome, ()):                     # o alto em −Z: põe em pé (x, y, z) -> (x, −z, y)
            t = t.copy()
            for a_ in (0, 3): t[:, :, a_:a_ + 3] = np.stack([t[:, :, a_], -t[:, :, a_ + 2], t[:, :, a_ + 1]], -1)
        # a frente do triângulo: a normal geométrica (regra da mão direita) para o lado da normal do vértice
        if len(t):
            g = np.cross(t[:, 1, :3] - t[:, 0, :3], t[:, 2, :3] - t[:, 0, :3]); vira = (g * t[:, :, 3:6].sum(1)).sum(1) < 0
            t = t.copy(); t[vira] = t[vira][:, [0, 2, 1]]
        # o nome sem a pasta; repetido (as peças com ossos do kit 1), com ela
        nomes.append(curto if curto not in nomes else p['nome'].replace('/', '_')); inicio.append(inicio[-1] + len(t)); tris.append(t)
    np.savez_compressed(os.path.join(AQUI, nome + '.npz'), nomes=np.array(nomes), inicio=np.array(inicio), tri=np.concatenate(tris).astype(np.float32))
    destino = os.path.join(RAIZ, 'assets', 'mapas', 'kits'); os.makedirs(destino, exist_ok=True)
    import shutil; shutil.copyfile(textura, os.path.join(destino, nome + '.png'))
    print(f"  {nome}: {len(nomes)} peças, {inicio[-1]} triângulos -> tools/mapas/kits/{nome}.npz")


if __name__ == '__main__':
    converter(*sys.argv[1:4])
