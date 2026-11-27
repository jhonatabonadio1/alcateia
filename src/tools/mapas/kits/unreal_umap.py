#!/usr/bin/env python3
"""
Lê um pacote do Unreal 5.0 (``.umap`` / ``.uasset`` de editor, não cozido) e tira dele o que serve para remontar um mapa:
onde está cada peça (a matriz de cada StaticMeshComponent, já no eixo do Godot e em metros), as luzes, e a caixa de cada
malha (``ExtendedBounds``), que é com o que se confere o eixo de cada peça contra o kit em FBX.

    python3 src/tools/mapas/kits/unreal_umap.py <Map_Demo.umap> <saida.json> [<pasta Meshes do pacote>]

Só Python, sem o Unreal: o formato é o de sempre (tabela de nomes, de importados e de exportados; cada objeto é uma lista
de propriedades com nome, tipo e tamanho — o que não interessa pula-se pelo tamanho). Vale para a versão 1004 do UE5
(5.0, a do pacote "Low Poly FPS Bundle" na Fab): vetores em double, etiqueta de propriedade antiga.

O Unreal é de mão esquerda, Z para cima, em centímetros. O FBX das peças entra nele com Y e Z trocados, então:
posição no Godot = (x, z, y) / 100, e a matriz gira junto (``S @ M @ S``, com ``S`` trocando Y e Z).
"""
import json, math, os, struct, sys


class Pacote:
    def __init__(self, caminho):
        self.d = open(caminho, 'rb').read(); self.caminho = caminho; self.p = 0
        self._cabecalho(); self._nomes(); self._importados(); self._exportados()

    # leitura crua
    def i32(self): v = struct.unpack_from('<i', self.d, self.p)[0]; self.p += 4; return v
    def u32(self): v = struct.unpack_from('<I', self.d, self.p)[0]; self.p += 4; return v
    def i64(self): v = struct.unpack_from('<q', self.d, self.p)[0]; self.p += 8; return v
    def f64(self): v = struct.unpack_from('<d', self.d, self.p)[0]; self.p += 8; return v
    def f32(self): v = struct.unpack_from('<f', self.d, self.p)[0]; self.p += 4; return v

    def texto(self):
        n = self.i32()
        if n == 0: return ''
        if n < 0:
            s = self.d[self.p:self.p - 2 * n].decode('utf-16-le'); self.p -= 2 * n
        else:
            s = self.d[self.p:self.p + n].decode('latin-1'); self.p += n
        return s.rstrip('\0')

    def nome(self):
        i = self.i32(); n = self.i32(); s = self.nomes[i]
        return s if n == 0 else '%s_%d' % (s, n - 1)

    def _cabecalho(self):
        assert self.u32() == 0x9E2A83C1, 'não é um pacote do Unreal'
        legado = self.i32(); self.i32(); self.ue4 = self.i32(); self.ue5 = self.i32() if legado <= -8 else 0; self.i32()
        assert self.ue5 == 1004, 'feito para o UE 5.0 (versão 1004); este é %d' % self.ue5
        for _ in range(self.i32()): self.p += 20                             # versões de cada módulo
        self.i32(); self.texto(); self.u32()                                 # tamanho do cabeçalho, pasta, bandeiras
        self.n_nomes = self.i32(); self.o_nomes = self.i32()
        self.texto()                                                         # id de tradução
        self.i32(); self.i32()                                               # textos para traduzir
        self.n_exp = self.i32(); self.o_exp = self.i32(); self.n_imp = self.i32(); self.o_imp = self.i32(); self.o_dep = self.i32()

    def _nomes(self):
        self.p = self.o_nomes; self.nomes = []
        for _ in range(self.n_nomes):
            self.nomes.append(self.texto()); self.p += 4                     # os dois resumos do nome

    def _importados(self):
        self.p = self.o_imp; self.imp = []; passo = (self.o_exp - self.o_imp) // max(self.n_imp, 1)
        assert passo == 40, 'tamanho do importado: %d' % passo
        for _ in range(self.n_imp):
            ini = self.p; self.nome(); classe = self.nome(); fora = self.i32(); nome = self.nome()
            self.imp.append({'classe': classe, 'fora': fora, 'nome': nome}); self.p = ini + passo

    def _exportados(self):
        self.p = self.o_exp; self.exp = []; passo = (self.o_dep - self.o_exp) // max(self.n_exp, 1)
        assert passo == 108, 'tamanho do exportado: %d' % passo
        for _ in range(self.n_exp):
            ini = self.p; classe = self.i32(); self.i32(); self.i32(); fora = self.i32(); nome = self.nome(); self.u32()
            tam = self.i64(); onde = self.i64()
            self.exp.append({'classe': classe, 'fora': fora, 'nome': nome, 'tam': tam, 'onde': onde}); self.p = ini + passo

    def de(self, indice):
        """O nome do objeto de um índice de pacote (negativo: importado; positivo: exportado)."""
        if indice < 0: return self.imp[-indice - 1]['nome']
        if indice > 0: return self.exp[indice - 1]['nome']
        return None

    def classe(self, e): return self.de(e['classe'])

    def propriedades(self, e=None, fim=None):
        """As propriedades de um exportado: {nome: valor}. Só decifra o que os mapas usam (objeto, vetor, giro, número,
        booleano, cor, a caixa da malha); o resto fica como None. Sem ``e``, lê de onde está até ``fim`` (estrutura
        guardada como lista de propriedades, dentro de outra)."""
        if e is not None: self.p = e['onde']; fim = e['onde'] + e['tam']
        r = {}
        while self.p < fim:
            nome = self.nome()
            if nome == 'None': break
            tipo = self.nome(); tam = self.i32(); self.i32(); estrutura = None; valor = None
            if tipo == 'StructProperty': estrutura = self.nome(); self.p += 16
            elif tipo == 'BoolProperty': valor = self.d[self.p] != 0; self.p += 1
            elif tipo in ('ByteProperty', 'EnumProperty', 'ArrayProperty', 'SetProperty'): self.p += 8
            elif tipo == 'MapProperty': self.p += 16
            if self.d[self.p]: self.p += 16
            self.p += 1; ini = self.p
            if tipo == 'ObjectProperty': valor = self.i32()
            elif tipo == 'FloatProperty': valor = self.f32()
            elif tipo == 'DoubleProperty': valor = self.f64()
            elif tipo == 'IntProperty': valor = self.i32()
            elif tipo == 'StructProperty' and estrutura in ('Vector', 'Rotator') and tam == 24: valor = (self.f64(), self.f64(), self.f64())
            elif tipo == 'StructProperty' and estrutura == 'Color' and tam == 4: valor = tuple(self.d[self.p:self.p + 4])     # B, G, R, A
            elif tipo == 'StructProperty' and estrutura in ('Vector', 'Rotator') and tam == 12: valor = (self.f32(), self.f32(), self.f32())
            elif tipo == 'StructProperty' and estrutura == 'BoxSphereBounds':
                b = self.propriedades(fim=ini + tam); valor = list(b.get('Origin') or (0, 0, 0)) + list(b.get('BoxExtent') or (0, 0, 0)) + [b.get('SphereRadius')]     # o que é zero não é gravado
            self.p = ini + tam; r[nome] = valor
        self.fim_props = self.p
        return r


def matriz(local, giro, escala):
    """A matriz 3×4 (linhas) de um componente do Unreal, no eixo dele: giro (Pitch, Yaw, Roll em graus), escala, posição."""
    p, y, r = (math.radians(v) for v in giro)
    sp, cp, sy, cy, sr, cr = math.sin(p), math.cos(p), math.sin(y), math.cos(y), math.sin(r), math.cos(r)
    # FRotationMatrix: os eixos X, Y e Z do objeto (em colunas aqui)
    X = (cp * cy, cp * sy, sp); Y = (sr * sp * cy - cr * sy, sr * sp * sy + cr * cy, -sr * cp); Z = (-(cr * sp * cy + sr * sy), cy * sr - cr * sp * sy, cr * cp)
    return [[X[i] * escala[0], Y[i] * escala[1], Z[i] * escala[2], local[i]] for i in range(3)]


def vezes(A, B):
    """A @ B, as duas 3×4 (a última linha é 0 0 0 1)."""
    return [[sum(A[i][k] * B[k][j] for k in range(3)) + (A[i][3] if j == 3 else 0.0) for j in range(4)] for i in range(3)]


def godot(M):
    """A matriz do Unreal (cm, Z para cima, mão esquerda) no Godot (m, Y para cima): troca Y com Z nos dois lados."""
    t = (0, 2, 1); return [[M[t[i]][t[j]] if j < 3 else M[t[i]][3] / 100.0 for j in range(4)] for i in range(3)]


def cena(caminho):
    """As peças, as luzes e os efeitos (o fogo) de um mapa: ({'peca', 'matriz' (12 números, no Godot), 'ator'}, …), (luzes),
    (efeitos), {classe: quantos}."""
    P = Pacote(caminho); comp = {}; classes = {}
    for i, e in enumerate(P.exp):
        c = P.classe(e); classes[c] = classes.get(c, 0) + 1
        if c and (c.endswith('Component') or c.endswith('Component_C')): comp[i + 1] = (e, c, P.propriedades(e))

    def mundo(i, fundo=0):
        e, c, pr = comp[i]
        M = matriz(pr.get('RelativeLocation') or (0, 0, 0), pr.get('RelativeRotation') or (0, 0, 0), pr.get('RelativeScale3D') or (1, 1, 1))
        pai = pr.get('AttachParent')
        return vezes(mundo(pai, fundo + 1), M) if pai and pai in comp and fundo < 16 else M

    pecas = []; luzes = []; efeitos = []
    for i, (e, c, pr) in sorted(comp.items()):
        ator = P.de(e['fora'])
        malha = pr.get('StaticMesh') or pr.get('SkeletalMesh')          # a malha com esqueleto (porta, cancela) entra parada, na pose de descanso
        if malha:
            visivel = pr.get('bVisible') is not False and pr.get('bHiddenInGame') is not True
            pecas.append({'peca': P.de(malha), 'ator': ator, 'classe': c, 'visivel': visivel, 'matriz': [round(v, 5) for l in godot(mundo(i)) for v in l]})
        elif c == 'ParticleSystemComponent' and pr.get('Template'):
            G = godot(mundo(i)); efeitos.append({'efeito': P.de(pr['Template']), 'pos': [round(G[k][3], 3) for k in range(3)]})
        elif c in ('PointLightComponent', 'SpotLightComponent', 'RectLightComponent', 'DirectionalLightComponent', 'SkyLightComponent'):
            cor = pr.get('LightColor'); G = godot(mundo(i))
            luzes.append({'classe': c, 'ator': ator, 'pos': [G[0][3], G[1][3], G[2][3]], 'frente': [G[0][0], G[1][0], G[2][0]],
                          'cor': [cor[2] / 255, cor[1] / 255, cor[0] / 255] if cor else [1, 1, 1],
                          'forca': pr.get('Intensity'), 'alcance': (pr.get('AttenuationRadius') or 1000.0) / 100.0,
                          'cone': pr.get('OuterConeAngle'), 'sombra': pr.get('CastShadows') is not False})
    return pecas, luzes, efeitos, classes


def caixas(pasta):
    """A caixa de cada malha do pacote ({nome: (centro, meio tamanho)} no Godot, em metros): o ``ExtendedBounds``."""
    r = {}
    for raiz, _, arquivos in os.walk(pasta):
        for a in sorted(arquivos):
            if not a.endswith('.uasset'): continue
            try: P = Pacote(os.path.join(raiz, a))
            except AssertionError: continue
            for e in P.exp:
                if P.classe(e) != 'StaticMesh': continue
                b = P.propriedades(e).get('ExtendedBounds')
                if b: r[e['nome']] = ([b[0] / 100, b[2] / 100, b[1] / 100], [b[3] / 100, b[5] / 100, b[4] / 100])
    return r


if __name__ == '__main__':
    pecas, luzes, efeitos, classes = cena(sys.argv[1]); saida = {'origem': os.path.basename(sys.argv[1]), 'itens': pecas, 'luzes': luzes, 'efeitos': efeitos}
    if len(sys.argv) > 3: saida['caixas'] = {n: [[round(v, 3) for v in c], [round(v, 3) for v in m]] for n, (c, m) in caixas(sys.argv[3]).items()}
    json.dump(saida, open(sys.argv[2], 'w', encoding='utf-8'), separators=(',', ':'))
    print('%s: %d peças, %d luzes' % (os.path.basename(sys.argv[1]), len(pecas), len(luzes)))
    for c, n in sorted(classes.items(), key=lambda kv: -kv[1])[:40]: print('  %5d  %s' % (n, c))
