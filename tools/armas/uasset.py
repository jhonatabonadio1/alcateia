#!/usr/bin/env python3
"""
Leitor mínimo de .uasset não cozido do Unreal 5.3, o bastante para tirar as
curvas de um AnimSequence (o modelo de dados de sequenciador: uma seção de
Control Rig com nove curvas por osso — translação, rotação em Euler e escala).

    python3 tools/armas/uasset.py A_FP_AssaultRifle_Reload.uasset

``ler(caminho)`` devolve ``{'duracao': s, 'ordem': [...], 'ossos': {nome_CONTROL:
{'Translation': [3 canais], 'Rotation': [3], 'Scale': [3]}}}``; cada canal é
``(quadros, valores, valor padrão, quadros por segundo)``.

``curvas(caminho)`` já entrega por osso ``(T[n,3], Q[n,4])``, um valor por
quadro (30/s), **nos eixos do .glb do jogo**: o Unreal é de mão esquerda, então
a posição vira ``(x, −y, z)`` e a rotação ``(−x, y, −z, w)`` (conferido com a
pelve e a coluna do AR-15, que batem com o .glb até a terceira casa). A rotação
do canal é Euler do Unreal: canal 0 = roll, 1 = pitch, 2 = yaw.
"""
import struct, sys
import numpy as np


class R:
    def __init__(s, d, o=0): s.d = d; s.o = o
    def i32(s): v = struct.unpack_from('<i', s.d, s.o)[0]; s.o += 4; return v
    def u32(s): v = struct.unpack_from('<I', s.d, s.o)[0]; s.o += 4; return v
    def i64(s): v = struct.unpack_from('<q', s.d, s.o)[0]; s.o += 8; return v
    def u16(s): v = struct.unpack_from('<H', s.d, s.o)[0]; s.o += 2; return v
    def u8(s): v = s.d[s.o]; s.o += 1; return v
    def f32(s): v = struct.unpack_from('<f', s.d, s.o)[0]; s.o += 4; return v
    def raw(s, n): v = s.d[s.o:s.o + n]; s.o += n; return v
    def fstr(s):
        n = s.i32()
        if n == 0: return ''
        if n < 0:
            v = s.raw(-n * 2).decode('utf-16-le'); return v.rstrip('\0')
        return s.raw(n).decode('latin-1').rstrip('\0')

class Pacote:
    def __init__(s, path):
        s.d = d = open(path, 'rb').read(); r = R(d)
        assert r.u32() == 0x9E2A83C1
        s.legacy = r.i32(); r.i32(); s.ue4 = r.i32(); s.ue5 = r.i32(); r.i32()
        n = r.i32(); s.custom = {}
        for _ in range(n):
            g = r.raw(16); s.custom[g.hex()] = r.i32()
        s.header = r.i32(); s.pasta = r.fstr(); s.flags = r.u32()
        nn, no = r.i32(), r.i32()
        if s.ue5 >= 1008: r.i32(); r.i32()
        r.fstr()                      # LocalizationId
        r.i32(); r.i32()              # gatherable
        ne, eo = r.i32(), r.i32()
        ni, io = r.i32(), r.i32()
        s.nomes = []
        q = R(d, no)
        for _ in range(nn):
            s.nomes.append(q.fstr()); q.u16(); q.u16()
        s.imports = []
        q = R(d, io)
        for _ in range(ni):
            cp = s.nome(q); cn = s.nome(q); outer = q.i32(); on = s.nome(q)
            pk = s.nome(q) if s.ue4 >= 520 else None
            opt = q.i32() if s.ue5 >= 1003 else 0
            s.imports.append(dict(classe=cn, outer=outer, nome=on))
        s.exports = []
        q = R(d, eo)
        for _ in range(ne):
            e = dict(classe=q.i32(), super=q.i32(), template=q.i32(), outer=q.i32(), nome=s.nome(q), oflags=q.u32(),
                     tam=q.i64(), off=q.i64())
            q.i32(); q.i32(); q.i32()
            if s.ue5 < 1005: q.raw(16)
            if s.ue5 >= 1006: q.i32()
            q.u32(); q.i32(); q.i32()
            if s.ue5 >= 1003: q.i32()
            q.i32(); q.i32(); q.i32(); q.i32(); q.i32()
            s.exports.append(e)
    def nome(s, r):
        i = r.i32(); num = r.i32()
        n = s.nomes[i]
        return n if num == 0 else f"{n}_{num - 1}"
    def classe(s, e):
        c = e['classe']
        if c < 0: return s.imports[-c - 1]['nome']
        if c > 0: return s.exports[c - 1]['nome']
        return 'Class'


def _tag(p, r):
    nome = p.nome(r)
    if nome == 'None': return None
    tipo = p.nome(r); tam = r.i32(); idx = r.i32(); extra = {}
    if tipo == 'StructProperty': extra['struct'] = p.nome(r); r.raw(16)
    elif tipo == 'BoolProperty': extra['bool'] = r.u8()
    elif tipo in ('ByteProperty', 'EnumProperty'): extra['enum'] = p.nome(r)
    elif tipo in ('ArrayProperty', 'SetProperty'): extra['inner'] = p.nome(r)
    elif tipo == 'MapProperty': extra['inner'] = p.nome(r); extra['value'] = p.nome(r)
    if r.u8(): r.raw(16)
    return nome, tipo, tam, idx, extra

def _canal(r, tam):
    ini = r.o
    r.u8(); r.u8()
    assert r.i32() == 4; n = r.i32()
    t = np.frombuffer(r.raw(4 * n), '<i4').astype(float)
    assert r.i32() == 28; m = r.i32(); assert m == n
    v = np.frombuffer(r.raw(28 * m), '<f4').reshape(m, 7)[:, 0].astype(float) if m else np.zeros(0)
    padrao = r.f32(); tem = r.i32(); num = r.i32(); den = r.i32()
    r.o = ini + tam
    return t, v, padrao if tem else None, num / den

def ler(path):
    p = Pacote(path)
    info = {}
    for e in p.exports:
        c = p.classe(e); r = R(p.d, e['off'])
        if c == 'AnimSequence':
            while True:
                t = _tag(p, r)
                if t is None: break
                if t[0] == 'SequenceLength': info['duracao'] = struct.unpack_from('<f', p.d, r.o)[0]
                r.o += t[2]
        if c != 'MovieSceneControlRigParameterSection': continue
        ossos = {}; ordem = []
        while True:
            t = _tag(p, r)
            if t is None: break
            nome, tipo, tam, idx, extra = t
            if nome == 'TransformParameterNamesAndCurves':
                q = R(p.d, r.o); n = q.i32(); _tag(p, q)
                for _ in range(n):
                    atual = None
                    while True:
                        t2 = _tag(p, q)
                        if t2 is None: break
                        if t2[0] == 'ParameterName':
                            atual = p.nome(R(p.d, q.o)); ossos[atual] = {'Translation': [None] * 3, 'Rotation': [None] * 3, 'Scale': [None] * 3}; ordem.append(atual); q.o += t2[2]
                        elif t2[0] in ('Translation', 'Rotation', 'Scale'):
                            ossos[atual][t2[0]][t2[3]] = _canal(q, t2[2])
                        else: q.o += t2[2]
            r.o += tam
        info['ossos'] = ossos; info['ordem'] = ordem
    return info


def quat_ue(roll, pitch, yaw):
    """FRotator -> quatérnio, como o Unreal faz."""
    sp, sy, sr = [np.sin(np.radians(a) / 2) for a in (pitch, yaw, roll)]
    cp, cy, cr = [np.cos(np.radians(a) / 2) for a in (pitch, yaw, roll)]
    return np.array([cr * sp * sy - sr * cp * cy, -cr * sp * cy - sr * cp * sy, cr * cp * sy - sr * sp * cy, cr * cp * cy + sr * sp * sy])


def curvas(path, fps=30.0):
    a = ler(path); n = int(round(a['duracao'] * fps)) + 1; out = {}
    for nome, o in a['ossos'].items():
        def canal(c):
            t, v, padrao, _ = c
            if len(t) == 0: return np.full(n, padrao or 0.0)
            return np.interp(np.arange(n), t, v)
        T = np.stack([canal(c) for c in o['Translation']], 1); E = np.stack([canal(c) for c in o['Rotation']], 1)
        Q = np.array([quat_ue(*e) for e in E])
        for i in range(1, n):
            if np.dot(Q[i], Q[i - 1]) < 0: Q[i] = -Q[i]
        out[nome.replace('_CONTROL', '')] = (T * [1, -1, 1], Q * [-1, 1, -1, 1])
    return out, n, fps


if __name__ == '__main__':
    a = ler(sys.argv[1])
    print(f"{sys.argv[1]}: {a.get('duracao'):.3f} s, {len(a['ossos'])} ossos")
    for nome in a['ordem']:
        o = a['ossos'][nome]
        print(f"  {nome:28s} chaves T {[len(c[0]) for c in o['Translation']]} R {[len(c[0]) for c in o['Rotation']]}")
