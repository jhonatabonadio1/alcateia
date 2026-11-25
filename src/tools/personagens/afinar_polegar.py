#!/usr/bin/env python3
"""
Afina o polegar da malha de um personagem, direto no humano.glb (sem passar
pelo Blender: só as posições dos vértices mudam; o resto do arquivo fica byte
a byte).

Por quê (Jhon, 24/11: "o dedão do Barut parece maior, em ambas as mãos"):
o esqueleto da mão dele é o mesmo da Eve (polegar 7,5 cm, indicador 6,7), mas
a luva do kit tinha o polegar com o dobro da grossura do indicador (raio
médio da pele 20 mm contra 9,8; a Eve tem 13,5, e a mão da primeira pessoa,
1,2× o indicador). Na pistola, com o polegar por cima do da outra mão, ficava
um "salsichão".

Cada vértice preso aos ossos do polegar chega mais perto do osso (o segmento
de cada falange), na proporção do peso dele ali: as falanges (02, 03, 04)
pelo FATOR inteiro, a base (01, a polpa da palma) pela metade.

    python3 src/tools/personagens/afinar_polegar.py barut [--fator 0.6] [--seco]

--seco só mede. Rode uma vez: rodar de novo afina outra vez.
"""
import argparse
import json
import os
import struct

import numpy as np

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))   # src/

TIPOS = {5126: (np.float32, 4), 5121: (np.uint8, 1), 5123: (np.uint16, 2)}
QTD = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


def ler(caminho):
    d = open(caminho, 'rb').read()
    n = struct.unpack('<I', d[12:16])[0]
    j = json.loads(d[20:20 + n])
    inicio_bin = 20 + n + 8
    return d, j, inicio_bin


def acessor(d, j, inicio_bin, i):
    a = j['accessors'][i]
    bv = j['bufferViews'][a['bufferView']]
    tipo, tam = TIPOS[a['componentType']]
    k = QTD[a['type']]
    passo = bv.get('byteStride', tam * k)
    ini = inicio_bin + bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    out = np.zeros((a['count'], k), dtype=tipo)
    for v in range(a['count']):
        out[v] = np.frombuffer(d, dtype=tipo, count=k, offset=ini + v * passo)
    if a.get('normalized') and tipo != np.float32:
        out = out.astype(np.float32) / np.iinfo(tipo).max
    return out, ini, passo


def segmentos(d, j, ib, lado):
    """As falanges do polegar no espaço dos vértices: de cada junta à seguinte."""
    pele = j['skins'][0]
    ibm, _, _ = acessor(d, j, ib, pele['inverseBindMatrices'])
    juntas = [j['nodes'][i]['name'] for i in pele['joints']]
    pos = {}
    for k, nome in enumerate(juntas):
        m = ibm[k].reshape(4, 4).T          # glTF guarda por coluna
        pos[nome] = np.linalg.inv(m)[:3, 3]
    seg = {}
    for f in ('01', '02', '03', '04'):
        a = pos[f'thumb_{f}_{lado}']
        if f == '04':
            b = a + (a - pos[f'thumb_03_{lado}']) * 0.8
        else:
            b = pos[f'thumb_0{int(f) + 1}_{lado}']
        seg[juntas.index(f'thumb_{f}_{lado}')] = (a, b, f)
    return seg, juntas


def perto_do_segmento(p, a, b):
    ab = b - a
    t = np.clip(np.dot(p - a, ab) / np.dot(ab, ab), 0.0, 1.0)
    return a + ab * t


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('personagem')
    ap.add_argument('--fator', type=float, default=0.6)
    ap.add_argument('--seco', action='store_true')
    o = ap.parse_args()
    caminho = os.path.join(RAIZ, 'assets', 'personagens', o.personagem, 'humano.glb')
    d, j, ib = ler(caminho)
    novo = bytearray(d)
    segs = {}
    for lado in ('r', 'l'):
        s, juntas = segmentos(d, j, ib, lado)
        segs.update(s)
    mexidos = 0
    maior = 0.0
    raios = {'antes': [], 'depois': []}
    for m in j['meshes']:
        for p in m['primitives']:
            at = p['attributes']
            if 'JOINTS_0' not in at:
                continue
            pos, ini, passo = acessor(d, j, ib, at['POSITION'])
            jt, _, _ = acessor(d, j, ib, at['JOINTS_0'])
            wt, _, _ = acessor(d, j, ib, at['WEIGHTS_0'])
            nova = pos.copy()
            for v in range(len(pos)):
                desloca = np.zeros(3)
                for jj, w in zip(jt[v], wt[v]):
                    if w <= 0 or int(jj) not in segs:
                        continue
                    a, b, f = segs[int(jj)]
                    fator = 1.0 - (1.0 - o.fator) * (0.5 if f == '01' else 1.0)
                    c = perto_do_segmento(pos[v].astype(np.float64), a, b)
                    desloca += w * ((c + (pos[v] - c) * fator) - pos[v])
                    if f in ('02', '03') and w > 0.5:
                        raios['antes'].append(np.linalg.norm(pos[v] - c))
                        raios['depois'].append(np.linalg.norm(pos[v] + desloca - c))
                if np.any(desloca):
                    nova[v] = pos[v] + desloca
                    mexidos += 1
                    maior = max(maior, float(np.linalg.norm(desloca)))
            for v in range(len(pos)):
                if np.any(nova[v] != pos[v]):
                    novo[ini + v * passo: ini + v * passo + 12] = nova[v].astype(np.float32).tobytes()
            # o glTF exige o mínimo e o máximo das posições certos
            acc = j['accessors'][at['POSITION']]
            acc['min'] = [float(x) for x in nova.min(axis=0)]
            acc['max'] = [float(x) for x in nova.max(axis=0)]
    ra = np.mean(raios['antes']) * 1000 if raios['antes'] else 0
    rd = np.mean(raios['depois']) * 1000 if raios['depois'] else 0
    print(f'POLEGAR {o.personagem}: {mexidos} vértices, o que mais andou {maior * 1000:.1f} mm; '
          f'raio médio das falanges {ra:.1f} → {rd:.1f} mm (fator {o.fator})')
    if o.seco:
        return
    # o JSON com o mínimo e o máximo novos, do mesmo tamanho (completado com espaços)
    n = struct.unpack('<I', d[12:16])[0]
    texto = json.dumps(j, separators=(',', ':')).encode()
    if len(texto) > n:
        raise SystemExit(f'o JSON cresceu ({len(texto)} > {n}): não cabe no lugar')
    novo[20:20 + n] = texto + b' ' * (n - len(texto))
    open(caminho, 'wb').write(bytes(novo))
    print('GRAVADO', caminho)


if __name__ == '__main__':
    main()
