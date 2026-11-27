"""
Cidade — mapa grande (120 × 100 m), a céu aberto, de dia.

O bairro do pacote "Low Poly FPS Map Vol.1 — City" como o autor montou: a cena de demonstração, lida do ``.umap`` do
Unreal (``kits/unreal_umap.py`` → ``fps1_demo.json``: cada peça no lugar em que ele pôs). Pedido do Jhon, 27/11: "use o
que tem lá de demo". (Até 26/11 a Cidade era uma planta própria, desenhada em letras com as peças soltas do kit — o
pacote em FBX não trazia o mapa montado; está no git.)

A cena é uma avenida de 80 m entre a prefeitura e a torre do heliponto, com casas de três e quatro andares dos dois
lados, o estacionamento, a praça, a oficina e, de um lado, o pátio do prédio em obras; por trás das casas da avenida,
os becos e os quintais, fechados pelo anel de casas de fora.

**No tamanho da cena** (Jhon, 27/11: "ajuste somente o da obra, para não ficar exagerado"): com 86 × 92 m de miolo ela
já é dos maiores mapas do jogo, e não foi dobrada como o Distrito Neon. Só foi girada 90° — a avenida corre no eixo dos
times: o azul nasce no largo da prefeitura (−X), o laranja no fim da avenida, ao pé da torre (+X). Os dois lados não
são iguais (a cena não é simétrica).

A luz é de fim de manhã: sol quente, sombra fria, e o céu com nuvens que andam (``assets/mapas/ceus/ceu_nuvens.gdshader``).

As quatro perguntas do teste de mapa:
  1. Caminho que só o lobo faz: pelos toldos e pelas casas baixas (3 a 3,5 m) até as lajes do prédio em obras (3,6 e
     7,2 m), sem a escada; e de um telhado baixo ao outro por cima dos becos.
  2. Onde o humano vê o lobo chegando: as lajes abertas do prédio em obras e o meio da avenida, com 80 m de visão.
  3. Corredor que o humano deve evitar: os becos entre as casas do lado de fora (2 a 3 m de largura, janelas e
     telhados baixos por cima) e o pátio de obras, cheio de material.
  4. Cair em cima de quem não olha: das lajes da obra e dos telhados baixos para o beco e para a calçada da avenida.
"""
import os

import numpy as np

from .kit import Mapa, AZUL
from .kits.demo import Demo, por, barreiras, bolsoes, fechar_predios, fora_do_anel, alcancavel, nascer_em
from .kits.montagem import Montagem, Grade, giro, fechar, espalhar, luz_de_jogo, lampadas

MEIO = (7.0, 26.0)             # na cena do pacote: o ponto que vira o centro do mapa
VOLTA = giro(90)               # a cena girada 90°: a avenida (norte–sul na cena) fica no eixo dos times
CHAO = 0.13                    # a altura do chão da cena (os planos de fundo); a rua fica 2 cm acima
X, Z = 64.0, 54.0              # meia largura e meio fundo do mapa (o limite; o que se joga é o miolo, dentro do anel de casas)
TETO = 24.0

# A colisão de cada peça é a da própria malha, menos o que é caixa de verdade: a caixa de um prédio pegava a sacada, a
# escada de incêndio e a marquise, e a de um barril, de uma lixeira ou de uma barreira (redondos, de tampa torta, de
# perfil em cunha) segurava o tiro de cima no ar, sem nada que se visse (Jhon, 20/11; o teste TiroDeCimaCidade)
CAIXA = ('AmoBox', 'ATM', 'Box_', 'FlowerBed', 'Pallet', 'Parking_01', 'Fence_')
# (revisão de 27/11: os montes de lama, até 65 cm, colidem na forma deles; a placa de rua de 1,8 m, pela haste)
SEM = ('BG_', 'Grass', 'Plant', 'Luke', 'PowerLines', 'RoadSign', 'Tree', 'Light', 'ParkingBG', 'Road_', 'Stairs_01')
ANEL = ('House_', 'Bank_', 'Church_', 'CityHall_', 'Office_')          # os prédios fechados (o miolo deles é sólido; o que encosta no lado de fora é barrado até o teto)
# A escada do prédio em obras (``Construction_01``), medida na peça: degraus de 21 × 29 cm do térreo (0,40) ao primeiro
# andar (3,40), ao longo de z. Ganha a chapa de subida das escadas dos outros mapas (``Montagem.rampa``), 3 cm acima das
# quinas: sem ela, descendo, o corpo escorregava pela quina arredondada e voava três degraus de cada vez (27/11).
OBRA_ESCADA = ((0.03, 0.40), (3.89, 3.19), (2.03, 3.65), 3.40)

POSTES = {'Tree_01': (0.28, 3.0), 'Tree_02': (0.32, 3.4), 'Light_01': (0.12, 4.6), 'PowerLines_01': (0.2, 5.4), 'RoadSign_01': (0.08, 1.8)}      # o tronco e o poste: (largura, altura) da barra de colisão


# Na cena, uma fileira de barreiras de concreto (1,1 m: o humano não pula) fecha a avenida de calçada a calçada a 17 m do
# fim, e o trecho atrás dela fica sem uso. Aqui é onde nasce o laranja: saem as barreiras das duas pontas da fileira
# (uma passagem de 3 m junto à cerca da praça, outra de 2 m na calçada sul) e os carros do meio ficam de cobertura.
ABERTO = ((26.0, 0.8, 27.6, 5.6), (25.0, 9.8, 26.8, 13.2))


def colisao(nome):
    return 'caixa' if nome.startswith(CAIXA) else None if nome.startswith(SEM) else 'malha'


def desenho():
    """A cena inteira, girada. Devolve a montagem e os pontos de fogo (os tambores acesos)."""
    D = Demo('fps1'); M = Montagem(D.kit, colisao); centro = np.array([MEIO[0], CHAO, MEIO[1]])
    for item in D.itens:
        p, G, E, t, nome = item
        if nome.startswith('BG_'): continue                                   # o chão de fundo: o mapa tem o dele
        q = VOLTA @ (t - centro)
        if nome == 'Barrier_01' and any(x0 < q[0] < x1 and z0 < q[2] < z1 for x0, z0, x1, z1 in ABERTO): continue
        it = por(M, p, VOLTA @ G, E, q, nome, planta=not nome.startswith(SEM))
        if nome == 'Construction_01':
            pe, alto, lados, topo = OBRA_ESCADA; M.rampa(it, pe, alto, lados, eixo=2, ate=float(q[1]) + topo, folga=0.03)
        if nome in POSTES:
            largo, alto = POSTES[nome]; pe = it.pontos(np.array([[(p.lo[0] + p.hi[0]) / 2, 0.0, (p.lo[2] + p.hi[2]) / 2]]))[0] if nome != 'Light_01' else it.pontos(np.zeros((1, 3)))[0]
            M.barra(float(pe[0]), float(pe[2]), largo, alto)
    return M, [VOLTA @ (np.array(e['pos'], float) - centro) for e in D.efeitos]


def montar():
    m = Mapa("cidade", "Cidade", int(2 * X), int(2 * Z))
    # o chão: asfalto velho de fotografia por baixo de tudo, com o topo 2 cm abaixo do zero, como na Estação. A calçada
    # do kit fica no zero (o chão da cena, ``CHAO``): com o topo da caixa também no zero, as duas brigavam pelo mesmo
    # plano e a calçada aparecia em cacos claros sobre o asfalto (Jhon, 27/11: "as calçadas da cidade estão com
    # glitches"). A pista do kit, 12 cm abaixo, continua por baixo dele; quem pisa é o ``Piso``, no zero.
    m.material("chao", "asfalto", (0.78, 0.78, 0.80), 3.2)
    m.caixa("Chao", "Asfalto", (0, -0.27, 0), (520, 0.5, 520), "chao", colide=False)
    M, fogos = desenho()
    ocupado = M.ocupacao((-X, -Z), (X, Z))
    meio = ocupado.reshape(ocupado.shape[0] // 2, 2, ocupado.shape[1] // 2, 2).any((1, 3))                  # células de 50 cm
    fora = fora_do_anel(meio, 0.5)
    fora = bolsoes(fora, ocupado, BASES[0], (-X, -Z))          # o beco sem saída colado à beira é lado de fora também
    # o que fica inteiro do lado de fora não precisa de colisão (ninguém chega lá)
    for it in M.itens:
        if not it.colide: continue
        c = it.cantos(); i0, i1 = int((c[:, 2].min() + Z) / 0.5), int((c[:, 2].max() + Z) / 0.5) + 1; j0, j1 = int((c[:, 0].min() + X) / 0.5), int((c[:, 0].max() + X) / 0.5) + 1
        if i0 < 0 or j0 < 0 or i1 > fora.shape[0] or j1 > fora.shape[1] or fora[i0:i1, j0:j1].all(): it.colide = None
    M.aplicar(m, "Cidade", "cidade")
    fechar(m, X, Z, altura=TETO)
    n = barreiras(m, ~fora, X, Z, TETO)
    barrados, miolos = fechar_predios(m, M, fora, X, Z, TETO, lambda nome: nome.startswith(ANEL), inicio=n); n += barrados
    m.panorama('cidade', X + 170.0, altura=130.0, base=-6.0)

    g = Grade(ocupado, (-X, -Z), 0.25, folga=0.55)
    g.livre &= ~np.kron(fora, np.ones((2, 2), bool))
    chao, perto = alcancavel(g, BASES[0])
    g.livre &= chao
    a = nascer_em(m, g, BASES[0], AZUL); nascer_em(m, g, BASES[1], 3 - AZUL)
    m._grade = g
    espalhar(m, g, a, quantos=16)          # os pontos do mata-mata: todos contra todos, espalhados pelo mapa
    def rota(*pts): return [(float(x), 0.5, float(z)) for x, z in g.rota([perto(*a)] + [perto(x, z) for x, z in pts])]
    for nome, pts in ROTAS.items(): m.rota(nome, rota(*pts))

    luz_de_jogo(m, 'dia', 38, 42, alcance=150.0, nuvens=True)
    lampadas(m, [M])
    for i, q in enumerate(fogos, 1): m.luz("Luzes", f"Fogo{i}", (float(q[0]), float(q[1]) + 0.35, float(q[2])), (1.0, 0.55, 0.22), 2.2, 6.0)
    print(f"  cidade: {n} barreiras, {len(m.luzes)} luzes, {int(chao.sum() * g.cel ** 2)} m² de chão alcançável")
    if os.environ.get('MAPA_GRADE'):
        g.imagem(os.environ['MAPA_GRADE'], marcas=[(x, z, (40, 90, 255)) for x, _, z in m.spawns[AZUL]] + [(x, z, (255, 120, 30)) for x, _, z in m.spawns[3 - AZUL]] + [(x, z, (20, 160, 20)) for x, z in m.livres],
                 escala=2, rotas=[[(x, z) for x, _, z in r] for _, r in m.rotas])
    return m


# onde nasce cada time, fora da linha de tiro da avenida: o azul debaixo da cobertura do estacionamento, junto à
# prefeitura; o laranja no fim da avenida, atrás da fileira de barreiras, ao pé da torre
BASES = ((-27.0, -8.0), (37.0, 6.0))
# as rotas dos bots, a partir de onde nasce o azul (cada ponto cai no chão livre mais perto)
ROTAS = dict(
    avenida=[(-30, 6), (-10, 6), (10, 6), (30, 6), (37, 6)],
    norte=[(-25, -30), (-5, -31), (8, -25), (18, -15), (22, 3), (37, 6)],
    sul=[(-30, 6), (-28, 20), (-15, 22), (-8, 33), (-5, 12), (10, 6), (37, 6)],
    estacionamento=[(-10, 6), (0, -5), (12, -5), (22, -10), (22, 3), (37, 6)],
    patio=[(-30, 6), (-33, 25), (-25, 34), (-8, 34), (-3, 20), (-12, 12), (-30, 6)],
    volta=[(-25, -30), (-5, -31), (-2, -15), (5, 6), (37, 6)],
)
