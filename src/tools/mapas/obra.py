"""
Obra — mapa pequeno (40 × 40 m, três pisos), a céu aberto, no alto de um prédio, de dia (era fim de tarde até 27/11).

O canteiro do pacote "Low Poly FPS Map Vol.6 — Construction" como o autor montou: a cena de demonstração, lida do
``.umap`` do Unreal (``kits/unreal_umap.py`` → ``fps6_demo.json``). Pedido do Jhon, 27/11: "use o que tem lá de demo".
(Até 26/11 a Obra era uma planta própria, um prédio de 60 × 40 m no chão, desenhada em letras com as peças soltas do
kit; está no git.)

A cena é o topo de um prédio de 50 m, no meio do centro da cidade, onde se levantam mais andares: 40 × 40 m de laje
com paredes de concreto pela metade, três escadas para o segundo piso (3,8 m), um pedaço de terceiro piso (7,6 m), duas
gruas, andaimes, paletes, sacos de cimento, bobinas, banheiros químicos.

**Acima das nuvens** (Jhon, 27/11: "ao invés de cidade ao redor do prédio, será um prédio alto acima das nuvens; o
precipício será uma grande nuvem"): as torres e as ruas da demo saem; o prédio desce, andar sobre andar, até sumir num
mar de nuvens (as camadas de ``nuvens.gdshader``, as do precipício do Setor Zero, no tom do fim de tarde), e a névoa
adensa para baixo. A beira da laje continua fechada pela parede invisível: ninguém cai.

**Igual à cena, no tamanho dela** (Jhon, 27/11: "deixa o mapa da construção igual o da demo mesmo" — uma primeira
versão, do mesmo dia, punha dois canteiros lado a lado, 80 × 40 m). O azul nasce do lado oeste, o laranja do leste; os
dois lados não são iguais (a cena não é simétrica). Para 6 contra 6 é apertado: 40 × 40 m.

A única coisa que muda na cena são os módulos de parede que saem para o térreo ser um chão só (``abrir``, abaixo).

A luz é de fim de tarde: sol baixo e alaranjado rente ao mar de nuvens, sombra comprida e fria, nuvens acesas por baixo.

As quatro perguntas do teste de mapa:
  1. Caminho que só o lobo faz: do térreo ao segundo piso pelos paletes, andaimes e muretas, sem a escada; e ao
     terceiro piso (7,6 m), a que nenhuma escada chega.
  2. Onde o humano vê o lobo chegando: as lajes abertas do segundo piso, com o térreo à vista.
  3. Corredor que o humano deve evitar: os corredores do térreo debaixo da laje, entre paredes, com vão para cima.
  4. Cair em cima de quem não olha: das bordas das lajes e dos vãos de escada para o térreo.
"""
import os

import numpy as np

from .kit import Mapa, AZUL
from .kits.kit_fbx import Kit
from .kits.demo import Demo, por, barreiras, alcancavel, nascer_em, palete
from .kits.montagem import Montagem, Grade, fechar, espalhar, luz_de_jogo, lampadas
from .kits.montagem import CAIXA as CAIXA8

CHAO = 50.2                    # a altura do piso do canteiro na cena (o prédio tem 50 m; a placa do piso, 20 cm)
LAJE = 0.2                     # a placa do piso: onde não há placa, pisa-se na laje do prédio, 20 cm abaixo
ANDAR = 3.81                   # o segundo piso
X, Z = 22.0, 22.0              # meia largura e meio fundo do mapa: a laje (20 × 20) e 2 m de parede invisível por fora
BEIRA = (20.0, 20.0)           # a beira da laje
TETO = 24.0

# A colisão de cada peça é a da própria malha, menos o que é caixa de verdade (a caixa da bobina, do saco de cimento e do
# gerador segurava o tiro de cima no ar: o teste TiroDeCimaObra)
# (revisão de 27/11: o palete carregado, 1 m de blocos, a pilha de barras de aço e a escadinha de abrir colidem — o
# corpo e o tiro passavam por dentro delas; as barras e a escadinha na forma delas)
CAIXA = ('Floor_', 'WallCorner', 'WallPart', 'Door_', 'Box_', 'WC_01', 'Pallet_02')
SEM = ('Brick', 'Brush', 'Dye', 'Hammer', 'Sign', 'WallDecor', 'Road', 'House_', 'Light_', 'Pipe',
       'TowerCrane_01_2', 'TowerCrane_02_2', 'WC_01_2')
LONGE = ('House_01', 'House_02', 'House_03', 'House_04')
ANDARES_ABAIXO = 5             # quantas vezes o prédio da demo (50 m) se empilha embaixo da laje: até 250 m, dentro das nuvens

# O mar de nuvens embaixo (``nuvens.gdshader``, o do precipício do Setor Zero), no tom do fim de tarde:
# (nome, altura, lado do plano, tamanho do desenho, cobertura, maciez, opacidade, vento, cor, sombra, some de/até)
NUVENS = [
    # (de dia desde 27/11 — Jhon: "mapa da construção pode ser de dia, não em pôr do sol": nuvem branca, sombra azulada)
    ("NuvensFundo", -200.0, 9000.0, 520.0, 0.97, 0.30, 1.00, (2.0, 0.8), (1.0, 0.99, 0.97), (0.62, 0.70, 0.86), (3000.0, 4200.0)),
    ("NuvensMar", -120.0, 9000.0, 430.0, 0.78, 0.22, 0.97, (3.2, 1.3), (1.0, 0.99, 0.97), (0.66, 0.73, 0.88), (3000.0, 4200.0)),
    ("NuvensMeio", -70.0, 8000.0, 360.0, 0.50, 0.20, 0.88, (4.4, 1.9), (1.0, 1.0, 0.98), (0.70, 0.77, 0.90), (2600.0, 3800.0)),
    ("NuvensFarrapos", -32.0, 7000.0, 290.0, 0.26, 0.20, 0.65, (5.6, 2.6), (1.0, 1.0, 0.99), (0.76, 0.82, 0.93), (2200.0, 3400.0)),
]


def nuvens(m):
    """O mar de nuvens embaixo do prédio: as camadas de ``nuvens.gdshader`` (planos grandes em que a nuvem é desenhada
    por ruído, translúcida, andando com o vento), como o precipício do Setor Zero — a de baixo quase fechada, as de
    cima em farrapos."""
    from .kit import xform
    m.cru_ext.append(("Shader", "res://src/assets/mapas/detalhe/nuvens.gdshader", "s_nuvens"))
    c3 = lambda c: f"Color({c[0]}, {c[1]}, {c[2]}, 1)"
    m.cru_nos += ['[node name="Nuvens" type="Node3D" parent="."]', ""]
    for k, (nome, altura, lado, tam, cob, mac, opa, vento, cor, sombra, some) in enumerate(NUVENS):
        m.cru_recursos += [f'[sub_resource type="ShaderMaterial" id="NuvemMat{k}"]', 'shader = ExtResource("s_nuvens")',
                           f"shader_parameter/cor = {c3(cor)}", f"shader_parameter/sombra = {c3(sombra)}",
                           f"shader_parameter/tamanho = {tam}", f"shader_parameter/cobertura = {cob}", f"shader_parameter/maciez = {mac}",
                           f"shader_parameter/opacidade = {opa}", f"shader_parameter/vento = Vector2({vento[0]}, {vento[1]})",
                           f"shader_parameter/some_de = {some[0]}", f"shader_parameter/some_ate = {some[1]}",
                           f"shader_parameter/semente = Vector2({3.7 * k}, {1.9 * k})", "shader_parameter/encosto = 26.0",
                           "shader_parameter/brilho = 1.05", "",
                           f'[sub_resource type="PlaneMesh" id="NuvemPlano{k}"]', f'material = SubResource("NuvemMat{k}")', f"size = Vector2({lado}, {lado})", ""]
        m.cru_nos += [f'[node name="{nome}" type="MeshInstance3D" parent="Nuvens"]', f"transform = {xform((0, altura, 0))}",
                      f'mesh = SubResource("NuvemPlano{k}")', "cast_shadow = 0", "extra_cull_margin = 16384.0", ""]
# as escadas de concreto, medidas na peça: (o pé da linha das quinas, a quina do último degrau, os lados)
ESCADAS = {'Stairs_01': ((-0.452, 0.0), (5.28, 3.8), (-1.19, 1.19)), 'Stairs_02': ((-0.452, 0.0), (5.28, 3.8), (-1.19, 1.19))}


# As tábuas (``WoodenPlank``) colidem na forma delas (Jhon, 27/11, fim do dia: "esses pontos do mapa estão sem
# colisão"): a ponte de tábuas do segundo piso e as de cima da viga da grua eram desenho só, e se atravessava. Só a tábua
# deitada no chão (até 6 cm de alto) fica sem: lá ela é um degrau à toa no pé de quem passa.
TABUA = 'WoodenPlank'
TABUA_NO_CHAO = 0.06


def colisao(nome):
    return None if nome.startswith(SEM) else 'caixa' if nome.startswith(CAIXA) else 'malha'


def desenho(abertas=None):
    """O canteiro da cena, o prédio debaixo dele, as torres da cidade em volta e as ruas lá embaixo. ``abertas``: os
    módulos de parede que saem (``abrir``); sem dizer, calcula."""
    K = Kit('fps6'); palete(K)                                              # (o palete carregado de blocos: o Unreal tem, o FBX não)
    D = Demo(K)
    if abertas is None: abertas = abrir(D)
    M = Montagem(K, colisao); centro = np.array([0.0, CHAO, 0.0]); M.paredes = []
    for k, item in enumerate(D.itens):
        p, G, E, t, nome = item; t = t - centro
        if nome.startswith(('Road', 'House_')):
            # acima das nuvens (27/11): sem a cidade em volta; o prédio de baixo desce, andar sobre andar, até as nuvens
            if nome == 'House_Main':
                for k in range(ANDARES_ABAIXO):
                    por(M, p, G, E, t - np.array([0.0, k * p.tam[1], 0.0]), nome, colide=None, planta=False)
            continue
        if k in abertas: continue
        colide = 'padrao'
        if nome.startswith(TABUA):
            lo, hi = D.caixa(item)
            if hi[1] - CHAO < TABUA_NO_CHAO: colide = None
        it = por(M, p, G, E, t, nome, colide=colide, planta=not nome.startswith(SEM) and not (nome.startswith('Floor_') and t[1] > 1.0))
        if nome in ESCADAS:
            pe, alto, lados = ESCADAS[nome]; M.rampa(it, pe, alto, lados, eixo=0, ate=float(t[1]) + ANDAR)
        if nome.startswith('Wall_0') and abs(t[1]) < 0.3: M.paredes.append((k, it))
    M.abertas = sorted(abertas)
    return M


# ---- as paredes que saem
# Na cena, o térreo é uma fileira de cômodos fechados: paredes cegas, paredes de janela (peitoril de 50 cm) e algumas com
# porta de 1 m. A navegação dos bots anda com meio metro de raio (``NavBaker``): por porta de 1 m eles não passam, e o
# térreo fica partido em pedaços sem ligação — quem nascesse num deles não saía. Como é obra, a parede que falta "ainda
# não foi levantada": saem os módulos de parede (5 m cada) que ligam um cômodo ao vizinho, **só o mínimo** para todo o
# térreo ser um chão só (o resto fica igual à cena), de preferência a parede que já tinha porta, depois a quebrada,
# depois a de janela. As paredes da beira da laje ficam. ``ATALHOS`` abre mais alguns, onde a volta a pé para chegar ao
# outro lado da parede é comprida demais; está em zero.
PREFERENCIA = {'Wall_04': 0, 'Wall_08': 1, 'Wall_09': 1, 'Wall_07': 1, 'Wall_05': 2}
ATALHOS = 0                    # quantos módulos saem além do mínimo
VOLTA_LONGA = 28.0             # só se a volta a pé de um lado ao outro da parede passar disto, em metros


def _rotulos(g):
    """Cada pedaço de chão ligado da grade com um número: (a matriz de rótulos, quantos)."""
    L = g.livre; H, W = L.shape; rot = np.zeros((H, W), int); n = 0
    for i0, j0 in zip(*np.nonzero(L)):
        if rot[i0, j0]: continue
        n += 1; rot[i0, j0] = n; fila = [(i0, j0)]
        while fila:
            i, j = fila.pop()
            for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                a, b = i + di, j + dj
                if 0 <= a < H and 0 <= b < W and L[a, b] and not rot[a, b]: rot[a, b] = n; fila.append((a, b))
    return rot, n


def abrir(D):
    """Os módulos de parede do térreo que saem: os índices deles em ``D.itens``."""
    M = desenho(abertas=frozenset()); oc = M.ocupacao((-X, -Z), (X, Z)); cel = 0.25
    f = np.kron(fora(M, oc[::2, ::2]), np.ones((2, 2), bool))

    def grade(ocupado):
        g = Grade(ocupado, (-X, -Z), cel, folga=0.5); g.livre &= ~f; return g

    def lados(it):
        """Um ponto de cada lado da parede, a 1,2 m do meio dela."""
        c = it.pontos((it.peca.lo + it.peca.tam / 2)[None])[0]; n = it.G[:, 2]
        return (c[0] + n[0] * 1.2, c[2] + n[2] * 1.2), (c[0] - n[0] * 1.2, c[2] - n[2] * 1.2)

    def apagar(ocupado, it):
        """Tira da grade a parede (sem os 40 cm de cada ponta, onde ficam os pilares)."""
        q = it.pontos(it.peca.lo + CAIXA8 * it.peca.tam); lo, hi = q.min(0), q.max(0); ao_x = (hi[0] - lo[0]) > (hi[2] - lo[2])
        x0, x1, z0, z1 = (lo[0] + 0.4, hi[0] - 0.4, lo[2] - 0.15, hi[2] + 0.15) if ao_x else (lo[0] - 0.15, hi[0] + 0.15, lo[2] + 0.4, hi[2] - 0.4)
        ocupado[int((z0 + Z) / cel):int((z1 + Z) / cel) + 1, int((x0 + X) / cel):int((x1 + X) / cel) + 1] = False

    # as paredes de dentro (a da beira da laje não sai)
    par = {}
    for k, it in M.paredes:
        c = it.pontos((it.peca.lo + it.peca.tam / 2)[None])[0]
        if abs(c[0]) > BEIRA[0] - 0.6 or abs(c[2]) > BEIRA[1] - 0.6: continue
        par[k] = [it]
    ordem = sorted(par, key=lambda k: (PREFERENCIA.get(D.itens[k][4], 3), k))

    # 1. o mínimo para ligar tudo
    g = grade(oc); rot, n = _rotulos(g); pai = list(range(n + 1)); area = np.bincount(rot.ravel(), minlength=n + 1)
    def raiz(a):
        while pai[a] != a: pai[a] = pai[pai[a]]; a = pai[a]
        return a
    def rotulo(p):
        if not g.anda(*p):
            try: p = g.perto(*p, raio=1.0)
            except ValueError: return 0
        return int(rot[g.celula(*p)])
    saem = []
    for k in ordem:
        juntou = False; pares = []
        for it in par[k]:
            a, b = (rotulo(p) for p in lados(it))
            if a and b and min(area[a], area[b]) >= 8 * 16: pares.append((a, b)); juntou |= raiz(a) != raiz(b)       # (pedaço de menos de 8 m² não conta: o vão de uma pilha)
        if juntou:
            saem.append(k)
            for a, b in pares: pai[raiz(a)] = raiz(b)
    for k in saem:
        for it in par[k]: apagar(oc, it)

    # 2. os atalhos: a parede que obriga à volta mais comprida, uma de cada vez
    for _ in range(ATALHOS):
        g = grade(oc); melhor = None
        for k in ordem:
            if k in saem: continue
            a, b = lados(par[k][0])
            if not (g.anda(*a) and g.anda(*b)): continue
            cam = g.caminho(a, b)
            if cam is None: continue
            volta = (len(cam) - 1) * cel * 1.1
            if volta > VOLTA_LONGA and (melhor is None or volta > melhor[0]): melhor = (volta, k)
        if melhor is None: break
        saem.append(melhor[1])
        for it in par[melhor[1]]: apagar(oc, it)
    return frozenset(saem)


def fora(M, celulas):
    """O lado de fora: tudo o que passa da beira da laje (dois metros de parede invisível em toda a volta)."""
    H, W = celulas.shape; f = np.ones((H, W), bool); cel = 2 * X / W
    f[int(round((Z - BEIRA[1]) / cel)):int(round((Z + BEIRA[1]) / cel)), int(round((X - BEIRA[0]) / cel)):int(round((X + BEIRA[0]) / cel))] = False
    return f


def montar():
    m = Mapa("obra", "Obra", int(2 * X), int(2 * Z))
    M = desenho()
    ocupado = M.ocupacao((-X, -Z), (X, Z))
    meio = ocupado.reshape(ocupado.shape[0] // 2, 2, ocupado.shape[1] // 2, 2).any((1, 3))
    f = fora(M, meio)
    M.aplicar(m, "Obra", "obra")
    # Sem parede invisível na beira (Jhon, 27/11: "no mapa da construção eu posso cair, não precisa ter a colisão nos
    # limites do mapa"): o chão vai só até a beira da laje, e quem passa dela cai nas nuvens e morre (Player.FallLimit).
    # As Limite… vão assim mesmo, sem colidir: só seguram a câmera do espectador dentro do mapa (MapBounds, 27/11).
    fechar(m, X, Z, altura=TETO, piso=-LAJE, margem=BEIRA[0] - X, paredes=False)
    n = 0
    # (o centro ao longe, na cor do fim de tarde: o painel não recebe luz, a cor é a dele)
    nuvens(m)

    g = Grade(ocupado, (-X, -Z), 0.25, folga=0.5)
    g.livre &= ~np.kron(f, np.ones((2, 2), bool))
    chao, perto = alcancavel(g, BASES[0])
    g.livre &= chao
    a = nascer_em(m, g, BASES[0], AZUL, passo=1.3); nascer_em(m, g, BASES[1], 3 - AZUL, passo=1.3)
    m._grade = g
    espalhar(m, g, a, quantos=16)          # os pontos do mata-mata: todos contra todos, espalhados pelo mapa
    def rota(*pts): return [(float(x), 0.5, float(z)) for x, z in g.rota([perto(*a)] + [perto(x, z) for x, z in pts])]
    for nome, pts in ROTAS.items(): m.rota(nome, rota(*pts))

    # (de dia, o sol alto: era fim de tarde, 62°/24° — Jhon, 27/11)
    luz_de_jogo(m, 'dia', 62, 48, alcance=110.0, nuvens=True)
    # (para baixo da laje a névoa adensa: o prédio some na nuvem)
    m.ambiente['neblina'] = dict(m.ambiente['neblina'], altura=-90.0, altura_densidade=0.003)
    lampadas(m, [M], maximo=16)
    print(f"  obra: {n} barreiras, {len(M.abertas)} módulos de parede abertos, {len(m.luzes)} luzes, {int(chao.sum() * g.cel ** 2)} m² de térreo alcançável")
    if os.environ.get('MAPA_GRADE'):
        g.imagem(os.environ['MAPA_GRADE'], marcas=[(x, z, (40, 90, 255)) for x, _, z in m.spawns[AZUL]] + [(x, z, (255, 120, 30)) for x, _, z in m.spawns[3 - AZUL]] + [(x, z, (20, 160, 20)) for x, z in m.livres],
                 escala=6, rotas=[[(x, z) for x, _, z in r] for _, r in m.rotas])
    return m


# onde nasce cada time: o azul no corredor da beira oeste, o laranja no da beira leste
BASES = ((-17.5, -8.0), (17.5, 8.0))
# as rotas dos bots, a partir de onde nasce o azul (cada ponto cai no chão livre mais perto)
ROTAS = dict(
    norte=[(-17, -17), (-5, -15), (10, -15), (12, -8), (17, 0), (17.5, 8)],
    sul=[(-17, 2), (-15, 15), (-3, 17), (5, 17), (17.5, 15), (17.5, 8)],
    meio=[(-17, 2), (-8, 2), (0, -3), (10, -2), (17, 0), (17.5, 8)],
    cruzada=[(-17, 2), (-15, 15), (-3, 17), (0, -3), (10, -15), (17, 0), (17.5, 8)],
    ladoOeste=[(-17, -17), (-5, -15), (-8, -3), (-8, 2), (-15, 15), (-17, 2)],
    ladoLeste=[(0, -3), (10, -2), (17, 0), (17.5, 15), (5, 17), (0, -3)],
)
