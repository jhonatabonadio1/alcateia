"""
Cidade do Deserto — mapa grande (110 × 75 m), a céu aberto, sol alto.

Uma cidadela no deserto, talhada dentro da muralha: três vielas de ponta a ponta (a do norte, a rua da feira no meio, de
10 m, e a do sul), ligadas por becos de 2,5 m entre as casas e pela rua do centro, que abre na praça do chafariz. Cada
time nasce no pátio da caravana, atrás do portão, com três saídas: o portão e um beco para cada viela. As casas têm laje
de 3,5 m (sobe-se por escadas de pedra) e por cima da muralha (5,4 m) anda-se de ponta a ponta — quem chega lá é o lobo.

Montado com as peças do kit "Low Poly FPS Map Vol.2 — Desert City" (JustCreate; ``tools/mapas/kits``), com planta própria.
A planta é o desenho em letras aqui embaixo (``kits/planta.py``): cada letra, 2,5 m.

As quatro perguntas do teste de mapa:
  1. Caminho que só o lobo faz: da laje das casas (3,5 m) para cima da muralha (5,4 m) e por ela, por cima dos becos,
     de uma viela à outra.
  2. Onde o humano vê o lobo chegando: a laje da casa da escada, de frente para a viela e para a rua do centro; as
     bancas da feira, com a rua de 10 m inteira à vista.
  3. Corredor que o humano deve evitar: os becos de 2,5 m entre as vielas (30 m sem porta, muralha dos dois lados).
  4. Cair em cima de quem não olha: da muralha e das lajes sobre os becos e sobre a rua da feira.

O leste é o oeste girado 180°.
"""
from .kit import Mapa, AZUL
import math

from .kits.montagem import Montagem, Grade, fechar, nascer, barrar, espalhar, luz_de_jogo, lampadas
from .kits.planta import Planta

# # muralha (maciço de 5,4 m, anda-se por cima)   T torreão   a e casa baixa (laje de 3,5 m)   A E casa alta (7 m)
# s escada de pedra (sobe para a casa encostada na ponta)   m banca da feira   n tenda   w poço   p palmeira   r pedras
# b caixote coberto (2 m)   c caixotes e sacas   j potes   k tapete   1–9 por onde passam as rotas
OESTE = [
    "######################",
    "#####aaa#eeee###AAA###",
    "#####aaa#eeee.w.AAA###",
    "#####aaa#eeee...AAA###",
    "###...p..1....b...p.2.",
    "###..j..........r.....",
    "###.#eee#aaaa.###aaa..",
    "###.#eee#aaaa.sssaaa..",
    "###.#eee#aaaa.###aaa..",
    "###.#########8######..",
    "###.##AAAeeee.aaa..p..",
    "#.n..TAAAeeee.aaa.m...",
    "#...c#AAAeeee.aaa....p",
    "#..p..j.....m....p....",
    "#.......3.b.......k..5",
    "#..n.......m...4.b....",
    "#.c...j...........p...",
    "#.p..#eee.AAA#aaaa.j..",
    "#..n.Teee.AAA#aaaa....",
    "###.##eee.AAA#aaaa..b.",
    "###.#####9##########..",
    "###.#aaa#.#eeeesss##..",
    "###.#aaa#.#eeee.p.##..",
    "###.#aaa#.#eeee...##..",
    "###..p...6....r..p..7.",
    "###.....b........j....",
    "#####AAA#eeee#aaa#EEE#",
    "#####AAA#eeee#aaa#EEE#",
    "#####AAA#eeee#aaa#EEE#",
    "######################",
]
SOLIDOS = "#TaeAEs"
P = Planta(OESTE, 2.5, 'giro')
X, Z = P.X, P.Z
MURALHA = 5.4          # a altura do maciço; a ameia da parede (6 m) passa por cima
MURALHA_FORA = 7.6     # a muralha que fecha o mapa (era 14 m; Jhon, 20/11: "pode ser metade do que já é" — o lobo não corre nos limites invisíveis, cai)

CAIXA = ('Bag_', 'Barrel', 'Box_', 'Pallet', 'Pot_', 'Rock_', 'Furniture', 'Table', 'Jug_', 'Well', 'muralha')
SEM = ('Block_', 'Book', 'Carpet', 'Cup', 'Desk', 'Food', 'Plate', 'Scroll', 'Flag', 'Dryer', 'Plant', 'PalmTree', 'PowerLines', 'Tile', 'Hill', 'Mountain',
       'Garbage', 'Door', 'WindowPart', 'FlowerBed', 'pedra')
# a casa de cada tamanho de retângulo (em células, o lado maior primeiro): baixas e altas
BAIXAS = {(3, 3): 'House_01', (3, 2): 'House_02', (4, 3): 'House_03', (4, 2): 'House_03', (2, 2): 'House_01'}
ALTAS = {(3, 3): 'House_05', (2, 2): 'House_05', (4, 2): 'House_04L', (4, 3): 'House_04R', (3, 2): 'House_05'}


def colisao(nome):
    return 'caixa' if nome.startswith(CAIXA) else None if nome.startswith(SEM) else 'malha'


def desenho():
    M = Montagem('fps2', colisao); K = M.kit
    K.chapa('muralha', M.cor_dos_lados('Wall_01')); K.chapa('pedra', (0.62, 0.54, 0.46))

    # ---- a muralha: o maciço (uma caixa por retângulo) e, em cada face que dá para a rua, os panos com ameia
    # (a de dentro, entre as vielas, tem 5,4 m e o lobo anda por cima; a de fora, que fecha o mapa, 14 m: dois panos,
    # a ameia só no de cima — ninguém chega lá)
    Pm = P.marcada(P.borda('#T'), '@')
    for reg in Pm.regioes('#'):
        M.chapa('muralha', *P.mundo(*reg[1:]), alto=MURALHA, colide='caixa', planta=True)
    for xa, za, xb, zb, yaw in Pm.contorno('#', fora=False):
        M.muro(('Wall_01', 'Wall_02'), xa, za, xb, zb, 7.0, frente=yaw, recuo=0.27, semente=int(abs(xa * 3 + za * 7)), colide=None, planta=False)
    for reg in Pm.regioes('@'):
        M.chapa('muralha', *P.mundo(*reg[1:]), alto=MURALHA_FORA, colide='caixa', planta=True)
    for xa, za, xb, zb, yaw in Pm.contorno('@'):
        k = dict(frente=yaw, recuo=0.27, semente=int(abs(xa * 3 + za * 7)), colide=None, planta=False)
        M.muro(('Wall_02', 'Wall_01'), xa, za, xb, zb, 7.0, alto=MURALHA_FORA - 5.2, **k)
        M.muro(('Wall_01', 'Wall_02'), xa, za, xb, zb, 7.0, y=MURALHA_FORA - 5.4, **k)
    for _, r, c, x, z in P.celulas('T'):
        M.por('Wall_04', x, z, escala=1.25)

    # ---- as casas: uma por retângulo de letras, a porta para o lado de mais chão livre
    for reg in P.regioes('aeAE'):
        L, c0, r0, c1, r1 = reg; ret = P.mundo(c0, r0, c1, r1); lados = tuple(sorted((c1 - c0, r1 - r0), reverse=True))
        nome = (BAIXAS if L in 'ae' else ALTAS)[lados]; p = K[nome]; yaw = P.frente(ret, SOLIDOS, (p.tam[0], p.tam[2]))
        M.encaixar(nome, *ret, yaw=yaw, colide='malha')
        # por dentro (a porta fica aberta): o tapete no meio, a mesa posta e o armário no fundo, potes e sacas num canto
        cx, cz = (ret[0] + ret[2]) / 2, (ret[1] + ret[3]) / 2; a = math.radians(yaw); fx, fz = math.sin(a), math.cos(a); dx, dz = fz, -fx
        hf = (abs(ret[3] - ret[1]) if round(yaw / 90) % 2 == 0 else abs(ret[2] - ret[0])) / 2; hl = (abs(ret[2] - ret[0]) if round(yaw / 90) % 2 == 0 else abs(ret[3] - ret[1])) / 2
        def dentro(frente, lado): return (cx + fx * frente + dx * lado, cz + fz * frente + dz * lado)
        M.por(P.sorteio(cx, cz, ('Carpet_01', 'Carpet_02', 'Carpet_03', 'Carpet_04')), *dentro(0.2, 0.0), y=0.22, yaw=yaw + 90, escala=1.3)
        tx, tz = dentro(-(hf - 1.5), hl - 1.7); M.por('Table_01', tx, tz, y=0.2, yaw=yaw + 90)
        for i, n in enumerate(('Jug_01', 'Plate_03', 'Food_04', 'Cup_01')): M.por(n, tx + dx * 0.1 + fx * (-0.6 + 0.4 * i), tz + dz * 0.1 + fz * (-0.6 + 0.4 * i), y=0.93)
        M.por(P.sorteio(cx, cz, ('Furniture_02', 'Furniture_01'), 1), *dentro(-(hf - 0.85), -(hl - 1.6)), y=0.2, yaw=yaw)
        px, pz = dentro(hf - 1.3, -(hl - 1.1)); M.por('Jug_03', px, pz, y=0.2); M.por('Jug_02', px + dx * 0.5, pz + dz * 0.5, y=0.2); M.por('Bag_01', px + fx * -0.7, pz + fz * -0.7, y=0.2)
    # as escadas: o alto fica na ponta encostada numa casa baixa
    for _, c0, r0, c1, r1 in P.regioes('s'):
        ret = P.mundo(c0, r0, c1, r1)
        if c1 - c0 > r1 - r0: yaw = 90.0 if P.letra(r0, c0 - 1) in 'ae' else -90.0
        else: yaw = 0.0 if P.letra(r0 - 1, c0) in 'ae' else 180.0
        M.rampa(M.encaixar('Stairs_01R', *ret, yaw=yaw, colide='malha'), (3.64, 0.0), (-1.79, 3.49), (-1.17, 1.17), eixo=2)

    # ---- o que está na rua
    for L, r, c, x, z in P.celulas('mnwprbcjk'):
        nx, nz = P.corrida(r, c, SOLIDOS); ao_x = nx >= nz; g = P.desvio(x, z, 25)
        if L == 'm':
            yaw = 0.0 if ao_x else 90.0
            M.por(P.sorteio(x, z, ('Market_01', 'Market_02')), x, z, yaw=yaw); M.por(P.sorteio(x, z, ('Table_01', 'Table_02'), 1), x, z, yaw=yaw)
            M.por(P.sorteio(x, z, ('Jug_01', 'Jug_03', 'Food_04'), 2), x + 0.3, z, y=0.75)
        elif L == 'n':
            M.por(P.sorteio(x, z, ('Tent_01', 'Tent_02', 'Tent_03')), x, z, yaw=90.0 + g, colide='malha', planta=False)
            M.por('Carpet_01', x, z, yaw=90.0 + g); M.por('Bag_01', x + 1.4, z + 0.9); M.por('Bag_03', x + 1.5, z + 0.2)
        elif L == 'w':
            M.por('Box_02', x, z, yaw=g); M.por('Jug_01', x + 1.2, z + 0.4); M.por('Jug_03', x - 1.1, z - 0.3)          # era o poço (sem água no deserto; Jhon, 20/11)
        elif L == 'p':
            k_ = int(P.sorte(x, z, 21) * 3)          # sem vegetação no deserto (Jhon, 20/11): pedras, potes ou carga no lugar da palmeira
            if k_ == 0: M.por('Rock_02', x, z, yaw=g * 2); M.por('Rock_01', x + 0.8, z + 0.5, yaw=g)
            elif k_ == 1: M.por('Jug_03', x, z); M.por('Jug_01', x + 0.6, z - 0.4); M.por('Jug_02', x - 0.5, z + 0.5)
            else: M.por('Box_01', x, z, yaw=g); M.por('Bag_02', x + 0.8, z + 0.3); M.por('Bag_01', x - 0.2, z + 0.9)
            pass
        elif L == 'r':
            M.por('Rock_04', x, z, yaw=g); M.por('Rock_02', x + 1.0, z + 0.6, yaw=g * 2); M.por('Rock_01', x - 0.7, z + 0.9)
        elif L == 'b':
            M.por('Box_02', x, z, yaw=g * 0.5); M.por('Barrel_01', x + 1.5, z - 0.3)
        elif L == 'c':
            M.por('Box_01', x - 0.4, z, yaw=g); M.por('Box_01', x + 0.6, z + 0.3, yaw=g + 30); M.por('Bag_02', x + 0.2, z - 0.9); M.por('Pallet_02', x - 0.5, z + 1.1)
        elif L == 'j':
            M.por('Jug_01', x - 0.4, z); M.por('Jug_03', x + 0.3, z + 0.4); M.por('Jug_02', x + 0.2, z - 0.5)
        elif L == 'k':
            M.por('Carpet_02', x, z, yaw=g); M.por('Carpet_04', x + 1.3, z + 0.8, yaw=g + 60)

    # ---- o que dá vida às vielas, sem colisão: potes, plantas, canteiros, tapetes, sacas, bandeiras e entulho ao pé das paredes
    e = dict(colide=None, planta=False)
    for r, c, x, z, yaw, dist in P.beiras(SOLIDOS):
        M.grupo()
        s_ = P.sorte(x, z, 5 + int(yaw)); g = P.desvio(x, z, 40, 6)
        if s_ < 0.07:
            M.encostar('Jug_01', x, z, yaw, dist, folga=0.1, **e); M.encostar('Jug_02', x, z, yaw, dist, folga=0.15, lado=0.6, **e); M.encostar('Jug_02', x, z, yaw, dist, folga=0.6, lado=0.3, **e)
        elif s_ < 0.13: M.encostar('Jug_01', x, z, yaw, dist, folga=0.1, **e); M.encostar('Jug_03', x, z, yaw, dist, folga=0.2, lado=0.5, **e); M.encostar('Block_02', x, z, yaw, dist, folga=0.3, lado=-0.6, **e)
        elif s_ < 0.18: M.encostar('Rock_03', x, z, yaw, dist, folga=0.05, **e); M.encostar('Block_01', x, z, yaw, dist, folga=0.2, lado=0.7, **e)
        elif s_ < 0.23: M.encostar(P.sorteio(x, z, ('Carpet_01', 'Carpet_03', 'Carpet_04'), 8), x, z, yaw, dist, folga=0.15, **e); M.encostar('Jug_03', x, z, yaw, dist, folga=0.2, lado=0.9, **e)
        elif s_ < 0.28: M.encostar('Bag_01', x, z, yaw, dist, folga=0.05, **e); M.encostar('Bag_02', x, z, yaw, dist, folga=0.1, lado=0.65, **e); M.encostar('Bag_03', x, z, yaw, dist, y=0.6, folga=0.08, lado=0.3, **e)
        elif s_ < 0.32: M.encostar(P.sorteio(x, z, ('Furniture_04', 'Furniture_05'), 9), x, z, yaw, dist, folga=0.05, **e)
        elif s_ < 0.37: M.encostar(P.sorteio(x, z, ('Flag_01', 'Flag_02'), 10), x, z, yaw, dist, y=3.2, folga=0.0, **e)
        elif s_ < 0.41:
            M.encostar('Garbage_02', x, z, yaw, dist, folga=0.1, **e)
            for i in range(3): M.encostar(('Block_01', 'Block_02', 'Block_03')[i], x, z, yaw, dist, folga=0.15 + 0.25 * i, lado=0.8 - 0.3 * i, **e)
        elif s_ < 0.47: M.encostar(P.sorteio(x, z, ('Tile_01', 'Tile_02', 'Tile_03'), 11), x, z, yaw, dist, folga=0.1, **e); M.encostar('Tile_04', x, z, yaw, dist, folga=0.5, lado=0.7, **e)
        # os toldos da rua da feira, presos na parede
        if r in (13, 16) and P.sorte(x, z, 12) < 0.4:
            a = math.radians(yaw); M.por(P.sorteio(x, z, ('Tent_01', 'Tent_02', 'Tent_03'), 13), x - math.sin(a) * dist, z - math.cos(a) * dist, yaw=yaw, pivo=True, **dict(e, colide='malha'))
    M.grupo(fim=True)
    encostadas = {(r, c) for r, c, *_ in P.beiras(SOLIDOS)}
    for L, r, c, x, z in P.celulas('.'):
        M.grupo()
        norte_sul = P.letra(r - 1, c) in SOLIDOS and P.letra(r + 1, c) in SOLIDOS; leste_oeste = P.letra(r, c - 1) in SOLIDOS and P.letra(r, c + 1) in SOLIDOS
        if (norte_sul or leste_oeste) and P.sorte(x, z, 14) < 0.3:          # o varal atravessado no beco
            M.por('Dryer_01', x, z, y=3.0, yaw=90.0 if norte_sul else 0.0, escala=(2.45 / 2.7, 1.0, 1.0), **e)
        if (r, c) in encostadas: continue
        s_ = P.sorte(x, z, 15)
        if s_ < 0.09: M.por(P.sorteio(x, z, ('Tile_01', 'Tile_02', 'Tile_03'), 16), x, z, yaw=P.desvio(x, z, 20), **e); M.por('Tile_04', x + 1.0, z + 0.4, yaw=30.0, **e)
        elif s_ < 0.13: M.por('Block_03', x - 0.4, z + 0.2, yaw=P.desvio(x, z, 180), **e); M.por('Block_01', x + 0.5, z - 0.3, yaw=P.desvio(x, z, 90, 3), **e)
        elif s_ < 0.17: M.por('Rock_03', x, z, yaw=P.desvio(x, z, 180), **e)

    M.grupo(fim=True)
    # ---- a praça: o calçamento e o chafariz, no meio
    M.chapa('pedra', *P.mundo(17, 10, 27, 20), y=0.0, alto=0.03)
    # no meio, um estrado de pedra com a carga da feira em cima (era o chafariz: sem água no deserto; Jhon, 20/11)
    M.chapa('muralha', -2.4, -2.4, 2.4, 2.4, y=0.0, alto=0.9, colide='caixa', planta=True)
    M.por('Box_02', -0.6, 0.3, y=0.9, yaw=20.0); M.por('Barrel_01', 1.1, -0.7, y=0.9); M.por('Bag_01', 0.9, 1.1, y=0.9); M.por('Jug_01', -1.4, -1.3, y=0.9)
    # a rua da feira calçada de ponta a ponta (o resto é areia): o caminho se lê no chão
    M.chapa('pedra', -X + 2.5, -5.0, X - 2.5, 5.0, y=0.0, alto=0.025)
    # ---- o deserto em volta (fora da muralha): dunas e serra, sem colisão
    # (por cima da muralha de 14 m só se vê o que é alto: a serra em volta inteira, as dunas grandes atrás)
    import math as _m
    # dunas largas e redondas, em três fiadas (Jhon, 20/11: "montes mais arredondados e largos"; saiu a montanha de pico)
    for fiada, (dist, quantas, largo, alto, fundo) in enumerate(((96.0, 12, 1.6, 0.75, -1.5), (130.0, 10, 2.4, 1.1, -2.0), (178.0, 9, 3.4, 1.6, -3.0))):
        for i in range(quantas):
            a = i * 2 * _m.pi / quantas + 0.31 * fiada; d = dist + 7 * (i % 2)
            M.por(('Hill_01', 'Hill_02', 'Hill_03', 'Hill_04', 'Hill_05')[(i + fiada) % 5], _m.cos(a) * d * 1.2, _m.sin(a) * d, yaw=i * 41.0 + fiada * 17.0, y=fundo,
                  escala=(largo, alto, largo * 0.8), colide=None, planta=False)
    return M


def montar():
    m = Mapa("deserto", "Cidade do Deserto", int(2 * X), int(2 * Z))
    m.material("areia", "areia", (1.0, 0.93, 0.80), 3.0)
    m.caixa("Chao", "Areia", (0, -0.25, 0), (420, 0.5, 420), "areia", colide=False)
    M = desenho()
    M.aplicar(m, "Deserto", "deserto")
    fechar(m, X, Z, altura=18.0)
    barrar(m, P, '#T', 18.0)
    m.panorama('serra', X + 215.0, altura=120.0, base=-8.0)

    g = Grade(M.ocupacao((-X, -Z), (X, Z)), (-X, -Z), 0.25, folga=0.55)
    a = nascer(m, g, (-48.5, 0.0), AZUL)[0]
    rotas = dict(
        vielaNorte=P.rota(a, '12', '76'), feira=P.rota(a, '345'), vielaSul=P.rota(a, '67', '21'),
        becoNorte=P.rota(a, '382', '543'), becoSul=P.rota(a, '3967', '54'), cruzadaNorte=P.rota(a, '12'), cruzadaSul=P.rota(a, '67'),
        praca=P.rota(a, '1845', '49'),
    )
    m._grade = g
    espalhar(m, g, a, quantos=16)          # os pontos do mata-mata: todos contra todos, espalhados pelo mapa
    for nome, pontos in rotas.items():
        m.rota(nome, [(x, 0.5, z) for x, z in g.rota(pontos)])

    # sol alto de deserto: sombra curta e dura, ar quente e claro
    luz_de_jogo(m, 'deserto', 38, 52, alcance=160.0)
    lampadas(m, [M], padrao=r'^(Light|Lamp|Lantern|Torch)')
    return m
