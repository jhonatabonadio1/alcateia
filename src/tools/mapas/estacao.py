"""
Estação de Trem — mapa grande (120 × 95 m), no centro da cidade, fim de tarde.

A estação fica entre os prédios do centro. Ao norte, a rua e a calçada da praça, com carros, árvores e postes; depois o
prédio (97 × 20 m): o saguão no meio, mais alto, entre quatro torres de relógio, e duas alas com corredor de 30 m e duas
salas cada, telhado de duas águas, portas para a praça e para a plataforma. Depois a plataforma 1, a linha 1 (vagões de
passageiros), a plataforma do meio — onde cada time nasce, numa ponta —, a linha 2 (gôndolas e tanques), o corredor
entre dois trens, a linha 3 e, ao sul, o pátio de carga. No centro, a passarela: seis escadas sobem a 5 m e ela cruza as
três linhas por cima dos trens.

Montado com as peças do kit "Low Poly FPS Map Vol.5 — Railway Station" (JustCreate; ``tools/mapas/kits``); a cidade em
volta (os prédios que fecham o mapa, a rua, os carros, as árvores) é do kit "Vol.1 — City", num desenho à parte
(``cidade_em_volta``), e o capim, do "Vol.3 — Office" (``mato``). A planta é própria: o desenho em letras aqui embaixo
(``kits/planta.py``), cada letra 2,5 m.

As quatro perguntas do teste de mapa:
  1. Caminho que só o lobo faz: por cima dos trens (a prancha a 2 m, os vagões a 4,3 m) e dali para a passarela; por
     cima das divisórias de 5 m do prédio (o teto fica a 10 m).
  2. Onde o humano vê o lobo chegando: a passarela (5 m, o mapa inteiro à vista, mas sem cobertura) e as portas do
     saguão, de frente para a plataforma e para a praça.
  3. Corredor que o humano deve evitar: o vão entre o trem da linha 2 e o da linha 3, e o corredor de serviço do prédio.
  4. Cair em cima de quem não olha: do teto dos vagões e da passarela sobre as plataformas e o corredor dos trens.

O leste é o oeste **espelhado** (o prédio fica ao norte para os dois times): o azul nasce na ponta de −X.
"""
import numpy as np

from .kit import Mapa, AZUL
from .kits.montagem import Montagem, Grade, fechar, nascer, barrar, espalhar, luz_de_jogo, lampadas
from .kits.planta import Planta
from .cidade import colisao as colisao_da_cidade, CARROS

# M muro de arrimo (10 m: a cidade fica em cima)   U túnel (a linha entra nele 5 m, no escuro)   r a rua, que continua para fora do mapa
# h prédio do centro (fecha o mapa)   - | + parede do prédio (10 m)   D porta   J janela   = ! * divisória (5 m)   d porta
# L vagão de passageiros   G gôndola   Q tanque   F prancha (2 m: o lobo sobe)   o vagão de toras   H galpão
# p pilar   n banco   v máquina   k telefone   b caixotes   g gerador   x barreira   c cobertura   j truque de vagão
# B sacada (o corpo que sai da fachada)   a carro   t árvore   i poste   u caçamba   q banca
# 0–9 @ % & por onde passam as rotas
OESTE = [
    "hhhhhhhhhhhhhhhhhhhhhhhh",
    "hhhhhhhhhhhhhhhhhhhhhhhh",
    "hhhhhhhhhhhhhhhhhhhhhhhh",
    "rrr..a.....a......a.....",
    "rrr......%.......&......",
    "rrr...a.......a.....a...",
    "hhh.t..i..t..i.....i..t.",
    "hhh.....u..q............",
    "hhh.+-JD-J--JD-J--DD--DD",
    "hhh.|.b.b.!.nn..!.v.p..k",
    "hhh.|b....!.....!..n....",
    "hhh.|..b..d.nn..d..p..nn",
    "hhh.|.b...!.....!...@...",
    "hhh.|=d===*==d==*..p..nn",
    "hhh.D....b......d.......",
    "hhh.|n...0...n..!...p.v.",
    "hhh.+-D--J--D--J--DD--DD",
    "MMM..n....v..B......B.n.",
    "MMM......1.......2......",
    "MMM.x....b....x.........",
    "MUULLLLLLLL..LLLLLLLL...",
    "MUULLLLLLLL..LLLLLLLL...",
    "MMM...c.....c....c......",
    "MMM......3......4......5",
    "MMM..n...b.v.......n....",
    "MUU...GGGGGGGG.QQQQQQQQ.",
    "MUU...GGGGGGGG.QQQQQQQQ.",
    "MMM....6.........7......",
    "MUUFFFFFFFF..LLLLLLLL...",
    "MUUFFFFFFFF..LLLLLLLL...",
    "MMM...j.........j.......",
    "MMM.HHHH..b.oooo.9......",
    "MMM.HHHH.8..oooo........",
    "MMM......g...b.....b....",
    "MMM..b.j......x.....b...",
    "hhhhhhhhhhhhhhhhhhhhhhhh",
    "hhhhhhhhhhhhhhhhhhhhhhhh",
    "hhhhhhhhhhhhhhhhhhhhhhhh",
]
SOLIDOS = "hMLGQFHoB-|+=!*"
P = Planta(OESTE, 2.5, 'espelho')
X, Z = P.X, P.Z
TETO, DIVISORIA, PASSARELA = 10.0, 5.0, 5.0          # a passarela na altura do último degrau da escada do kit (5 m)
NORTE, SUL = 8, 16                                   # as fileiras das fachadas do prédio
LINHAS = (20, 25, 28)                                # a primeira fileira de cada linha de trem
SAGUAO = 18.75                                       # o saguão vai de −SAGUAO a +SAGUAO; as alas, daí até ALA
ALA = 48.75

# (os vagões, o galpão e a guarita têm a colisão da própria malha: a caixa deles pegava o teto curvo, o beiral e o vão debaixo
# da sacada, e o tiro de cima batia nela, invisível — Jhon, 20/11)
CAIXA = ('ATM', 'Bench', 'Box', 'Generator', 'PhoneBooth', 'VendingMachine', 'RailwayPart', 'Barrier', 'Wall', 'muro', 'parede')
SEM = ('Railway_', 'Light_', 'Clock1', 'Time_', 'Platform_', 'TrafficLight', 'HousePart', 'WallDecor', 'Window_', 'Door_', 'Building_',
       'concreto', 'piso', 'amarelo', 'escuro', 'brita', 'tunel_concreto', 'Wall_08T')
VAGOES = dict(L=('Train_01_C1', 'Train_02_C1', 'Train_01_C2', 'Train_02_C2'), G=('TrainPart_05', 'TrainPart_06'), Q=('TrainPart_04',),
              F=('Train_03_C1', 'Train_03_C2'), o=('TrainPart_03',))


def linha_z(r):
    """O z de cima da fileira r."""
    return -Z + r * 2.5


def colisao(nome):
    return 'caixa' if nome.startswith(CAIXA) else None if nome.startswith(SEM) else 'malha'


def desenho():
    M = Montagem('fps5', colisao); K = M.kit
    K.chapa('muro', M.cor_dos_lados('Wall2_01')); K.chapa('parede', M.cor_dos_lados('Wall_01')); K.chapa('concreto', M.cor(K['Floor_01']))
    K.chapa('piso', (0.72, 0.68, 0.62)); K.chapa('amarelo', (0.93, 0.75, 0.05)); K.chapa('escuro', (0.2, 0.2, 0.22)); K.chapa('brita', (0.36, 0.33, 0.31))
    zN, zS = linha_z(NORTE) + 1.25, linha_z(SUL) + 1.25; zM = (zN + zS) / 2

    # ---- o chão: o piso do prédio, as plataformas (com a faixa amarela na beira), o leito de brita e os trilhos
    M.chapa('piso', -ALA, zN, ALA, zS, y=0.0, alto=0.03)
    for r0, r1 in ((SUL + 1, LINHAS[0]), (LINHAS[0] + 2, LINHAS[1])):
        M.chapa('concreto', -52.5, linha_z(r0) - (1.25 if r0 == SUL + 1 else 0), 52.5, linha_z(r1), y=0.0, alto=0.04)
    for z in (linha_z(LINHAS[0]) - 0.5, linha_z(LINHAS[0] + 2) + 0.5, linha_z(LINHAS[1]) - 0.5):
        M.chapa('amarelo', -52.5, z - 0.08, 52.5, z + 0.08, y=0.03, alto=0.02)
    for r in LINHAS:
        M.chapa('brita', -57.5, linha_z(r) + 0.3, 57.5, linha_z(r) + 4.7, y=-0.01, alto=0.03)
        for i in range(12):
            M.por('Railway_01', -55 + 10 * i, linha_z(r) + 2.5, yaw=90, colide=None, planta=False)

    # ---- as pontas das linhas: o muro de arrimo de 10 m (a cidade passa por cima) e, em cada linha, a boca do túnel — a
    # parede de arco do kit (``Wall_08`` tem dois arcos; aqui vai um só, ``recorte``), com a caixa preta por trás: entra-se
    # 5 m no escuro e acaba ("coloque os túneis por onde o trem passa e liga a cidade", Jhon, 20/11)
    K.recorte('Wall_08T', 'Wall_08', 0, ate=0.0); pt = K['Wall_08T']
    K.chapa('tunel_concreto', (0.02, 0.02, 0.03))
    for reg in P.regioes('M'):
        M.chapa('parede', *P.mundo(*reg[1:]), alto=TETO, colide='caixa', planta=True)
    for reg in P.regioes('U'):
        x0, z0, x1, z1 = P.mundo(*reg[1:]); lado = -1 if x0 < 0 else 1; boca = x1 if lado < 0 else x0; fundo = x0 if lado < 0 else x1
        M.chapa('parede', x0, z0, x1, z1, y=5.45, alto=TETO - 5.45, colide='caixa', planta=True)
        M.por('Wall_08T', (boca - lado * 0.12 + fundo) / 2, (z0 + z1) / 2, yaw=90.0 if lado < 0 else -90.0, colide=None, planta=False,
              escala=((z1 - z0) / pt.tam[0], 1.0, (abs(boca - fundo) + 0.12) / pt.tam[2]))
        # (a caixa do kit é marrom-escura e o sol baixo entra pela boca do lado oeste: por dentro vai um forro quase
        # preto — o fundo, os dois lados e o teto —, para o túnel sumir no escuro)
        xa, xb = sorted((fundo + lado * -0.3, boca + lado * 0.5))
        M.chapa('tunel_concreto', xa, z0 + 0.3, xb, z0 + 0.34, alto=5.3); M.chapa('tunel_concreto', xa, z1 - 0.34, xb, z1 - 0.3, alto=5.3)
        M.chapa('tunel_concreto', xa, z0 + 0.3, xb, z1 - 0.3, y=5.3, alto=0.04)
        M.chapa('tunel_concreto', fundo - lado * 0.3 - 0.02, z0 + 0.3, fundo - lado * 0.3 + 0.02, z1 - 0.3, alto=5.3)
    for lado in (-1, 1):
        z0 = linha_z(SUL + 1)
        for zt in [linha_z(r) for r in LINHAS] + [linha_z(35)]:
            if zt - z0 > 0.1:
                M.muro('Wall_01', lado * 52.5, z0, lado * 52.5, zt, 5.0, frente=90.0 if lado < 0 else -90.0, recuo=0.1, colide=None, planta=False)
                for z in (z0, zt): M.por('WallPart_01', lado * (52.5 - 0.3), z, colide=None, planta=False)          # a pilastra em cada ponta
            z0 = zt + 5.0

    # ---- o prédio: as paredes de fora (10 m; as das alas de outra cor que as do saguão), as divisórias (5 m), as portas e
    # janelas, o teto
    muros, vaos, cantos = P.paredes('-', '|', '+', 'DJ', sobra=0.2)
    for xa, za, xb, zb in muros:
        ao_x = abs(xb - xa) > abs(zb - za); ala = abs(xa + xb) / 2 > SAGUAO
        frente = (180.0 if za < zM else 0.0) if ao_x else (-90.0 if xa < 0 else 90.0)
        M.muro(('Wall2_01', 'Wall2_03', 'Wall2_01') if ala else ('Wall_01', 'Wall_03', 'Wall_01'), xa, za, xb, zb, 5.0, frente=frente, alto=TETO, colide='caixa')
    for L, x, z, ao_x in vaos:
        cor = 'muro' if abs(x) > SAGUAO else 'parede'
        M.verga(cor, x, z, ao_x, 2.5, 2.6 if L == 'D' else 2.4, TETO, grosso=0.4, colide='caixa', planta=True)
        if L == 'J': M.verga(cor, x, z, ao_x, 2.5, 0.0, 1.0, grosso=0.4, colide='caixa', planta=True)
    for x, z in cantos:
        M.por('WallCorner_01', x, z, colide='caixa')
    muros, vaos, _ = P.paredes('=', '!', '*|-+', 'd', sobra=0.0)
    for xa, za, xb, zb in muros:
        M.muro('Wall_15', xa, za, xb, zb, 5.0, colide='caixa')
    for L, x, z, ao_x in vaos:
        M.verga('parede', x, z, ao_x, 2.5, 2.6, DIVISORIA, grosso=0.4, colide='caixa', planta=True)
    M.chapa('escuro', -ALA - 0.2, zN - 0.2, ALA + 0.2, zS + 0.2, y=TETO, alto=0.3, colide='caixa', planta=False)

    # ---- o que tira o prédio da caixa: o telhado de duas águas das alas, o corpo do saguão 2,5 m mais alto com o telhado
    # azul, as quatro torres de relógio nos cantos dele, as sacadas e a cobertura de telha ao longo da plataforma
    def telhado(nome, xa, xb, y):
        n = max(1, int(round((xb - xa) / 5.0))); sx = (xb - xa) / n / 5.0; sz = ((zS - zN) / 2 + 0.9) / 6.0
        for i in range(n):
            x = xa + (i + 0.5) * (xb - xa) / n
            # com colisão (Jhon, 20/11: "os telhados de todos os mapas precisam ter colisão" — escalando chegava-se
            # ao telhado e ele não segurava ninguém)
            M.por(nome, x, zM - 5 * sz, yaw=180.0, y=y, escala=(sx, 1.0, sz), pivo=True, colide='malha', planta=False)
            M.por(nome, x, zM + 5 * sz, yaw=0.0, y=y, escala=(sx, 1.0, sz), pivo=True, colide='malha', planta=False)
    telhado('Roof_01', -ALA - 0.5, -SAGUAO, 0.0); telhado('Roof_01', SAGUAO, ALA + 0.5, 0.0); telhado('Roof_04', -SAGUAO - 0.4, SAGUAO + 0.4, 2.5)
    for x0, z0, x1, z1 in ((-SAGUAO, zN - 0.3, SAGUAO, zN + 0.3), (-SAGUAO, zS - 0.3, SAGUAO, zS + 0.3), (-SAGUAO - 0.3, zN, -SAGUAO + 0.3, zS), (SAGUAO - 0.3, zN, SAGUAO + 0.3, zS)):
        M.chapa('parede', x0, z0, x1, z1, y=TETO, alto=2.6, colide='caixa', planta=False)
    # (as torres de relógio: eram quatro, enfiadas pela metade nos cantos do saguão — "ficaram mal feitas", Jhon, 20/11.
    # São duas, inteiras, de pé na calçada, uma de cada lado da entrada do saguão, encostadas na fachada)
    tz = K['Building_07'].tam[2]
    for lado in (-1, 1):
        M.por('Building_07', lado * (SAGUAO + 0.4), zN - 0.2 - tz / 2, yaw=180.0, colide='malha')          # (a caixa pegava a ponta do telhado)
        for i in range(5):                  # a cobertura da plataforma, presa na fachada das alas
            x = lado * (SAGUAO + 6.5 + 5.0 * i)
            if not any(abs(x - (bx + (1.25 if bx < 0 else -1.25))) < 5.4 for _, _, _, bx, _ in P.celulas('B')):
                M.por('SmallRoof_02', x, zS + 0.25, y=1.2, pivo=True, colide='malha', planta=False)

    # (o ático do saguão era uma parede lisa de 2,5 m: ganha uma fileira de janelas e o relógio no meio, dos dois lados)
    for z, yaw in ((zN - 0.32, 180.0), (zS + 0.32, 0.0)):
        for i in range(-6, 7):
            if i: M.por('Window_03', i * 2.7, z, yaw=yaw, y=TETO + 0.75, colide=None, planta=False)
        M.por('Clock1_C1', 0.0, z, yaw=yaw, y=TETO + 0.45, escala=1.8, colide=None, planta=False)

    # ---- os trens e os galpões: um por retângulo de letras, sem esticar
    for reg in P.regioes('LGQFo'):
        x0, z0, x1, z1 = P.mundo(*reg[1:]); x, z = (x0 + x1) / 2, (z0 + z1) / 2
        M.por(P.sorteio(x, z, VAGOES[reg[0]]), x, z, yaw=90.0 if x < 0 else -90.0)
    for reg in P.regioes('H'):
        ret = P.mundo(*reg[1:]); x, z = (ret[0] + ret[2]) / 2, (ret[1] + ret[3]) / 2
        M.por('Building_05', x, z, yaw=P.frente(ret, SOLIDOS), colide='malha')

    # ---- o que está nas plataformas, no prédio e no pátio
    for L, r, c, x, z in P.celulas('pnvkbgxcjB'):
        nx, nz = P.corrida(r, c, SOLIDOS); ao_x = nx >= nz; g = P.desvio(x, z, 20)
        if L == 'p': M.por('WallCorner_01', x, z, colide='caixa')
        elif L == 'n': M.por(P.sorteio(x, z, ('Bench_01', 'Bench_02')), x, z, yaw=0.0 if ao_x else 90.0)
        elif L == 'v': M.por('VendingMachine_01', x, z, yaw=g); M.por('PhoneBooth_01', x + 1.1, z + 0.1, yaw=g)
        elif L == 'k': M.por('PhoneBooth_01', x, z); M.por('ATM_01', x - 0.9, z)
        elif L == 'b': M.por('Box_01', x - 0.5, z - 0.3, yaw=g); M.por('Box_01', x + 0.5, z + 0.2, yaw=g + 20); M.por('Box_01', x - 0.2, z, y=0.9, yaw=g + 35)
        elif L == 'g': M.por('Generator_01', x, z, yaw=90.0)
        elif L == 'x': M.por('Barrier_01', x, z - 0.8, yaw=90.0 + g); M.por('Barrier_01', x + 0.2, z + 0.9, yaw=90.0 - g)
        elif L == 'c':
            M.por('SmallRoof_01', x, z, colide='malha', planta=False)          # (a colisão é a da malha; a caixa a mais saiu)
            M.por(P.sorteio(x, z, ('Platform_01', 'Platform_02')), x, z + 0.5, y=2.3, colide=None); M.por('Light_02', x + 1.2, z, y=2.75, colide=None); M.por('Clock1_C1', x - 1.4, z + 0.3, y=1.9, colide=None)
        elif L == 'j': M.por(P.sorteio(x, z, ('TrainPart_01', 'TrainPart_02')), x, z, yaw=90.0 + g); M.por('Box_01', x + 1.6, z + 0.4, yaw=g)
        elif L == 'B':
            # a guarita com sacada: é um módulo de parede do kit (o corpo sai 3 m da fachada, com o telhadinho e a escada
            # de mão). Vai no plano da fachada, 4 cm à frente dela, da família e da altura da parede em que está (a da
            # ala tem 9 m no kit e é esticada para os 10 m, como as vizinhas) — antes ficava solta na frente da parede,
            # mais baixa que ela ("essas torres ficaram mal feitas", Jhon, 20/11)
            xb = x + (1.25 if x < 0 else -1.25); ala = abs(xb) > SAGUAO; nome = 'Wall2_05' if ala else 'Wall_05'; p = K[nome]
            M.por(nome, xb - (p.lo[0] + p.hi[0]) / 2, zS + 0.04, yaw=0.0, pivo=True, escala=(1.0, TETO / p.tam[1], 1.0), colide='malha')

    # ---- o que dá vida ao prédio, sem colisão: relógios, quadros de horários, luminárias, caixas de ar e janelas internas
    # nas paredes; os sinais na ponta das linhas
    e = dict(colide=None, planta=False)
    for r, c, x, z, yaw, dist in P.beiras('', '-|+=!*', grosso=0.45):
        M.grupo()
        if not (NORTE < r < SUL): continue
        s_ = P.sorte(x, z, 21 + int(yaw)); fina = P.letra(r - (yaw == 0) + (yaw == 180), c - (yaw == 90) + (yaw == -90)) in '=!*'
        if s_ < 0.07: M.encostar('Clock1_C1', x, z, yaw, dist, y=3.4, folga=0.0, **e)
        elif s_ < 0.15: M.encostar('Time_01', x, z, yaw, dist, y=2.5, folga=0.0, **e)
        elif s_ < 0.32: M.encostar('Light_01', x, z, yaw, dist, y=4.3, folga=0.0, **e)
        elif s_ < 0.38: M.encostar('HousePart_02', x, z, yaw, dist, y=3.4, folga=0.0, **e)
        elif s_ < 0.47 and not fina: M.encostar('WallDecor_02', x, z, yaw, dist, y=0.0, folga=0.0, **e)
        elif s_ < 0.52: M.encostar('ATM_01', x, z, yaw, dist, folga=0.0, **e)
        elif s_ < 0.60 and fina: M.encostar(P.sorteio(x, z, ('Window_01', 'Window_02', 'Window_03'), 22), x, z, yaw, dist, y=1.5, folga=0.0, **e)
    M.grupo(fim=True)
    for r in LINHAS:
        for lado in (-1, 1):
            M.por('TrafficLight_01', lado * 51.5, linha_z(r) + 4.3, **e); M.por('TrafficLight_01', lado * 3.6, linha_z(r) + 0.6, **e)

    # ---- a passarela, no centro: a laje a 5 m por cima das três linhas, seis escadas, mureta e pilares
    za, zb = linha_z(SUL + 2), linha_z(33)
    M.chapa('concreto', -2.5, za, 2.5, zb, y=PASSARELA - 0.3, alto=0.3, colide='caixa', planta=True)
    escadas = (linha_z(SUL + 2), linha_z(LINHAS[0] + 2), linha_z(31))
    for z in escadas:
        for lado in (-1, 1):
            M.rampa(M.por('Stairs_01', lado * (2.5 + K['Stairs_01'].tam[0] / 2), z + 2.5, yaw=0.0 if lado < 0 else 180.0, colide='malha'),
                    (-0.48, 0.1), (7.52, 5.1), (-2.5, 2.5), ate=PASSARELA)
    for lado in (-1, 1):
        z0 = za
        for z in escadas + (zb,):
            if z - z0 > 0.1: M.chapa('escuro', lado * 2.5 - 0.08, z0, lado * 2.5 + 0.08, z, y=PASSARELA, alto=1.1, colide='caixa')
            z0 = z + 5.0
    for z in (za, zb):
        M.chapa('escuro', -2.5, z - 0.08, 2.5, z + 0.08, y=PASSARELA, alto=1.1, colide='caixa')
    for z in (linha_z(LINHAS[0]) - 0.6, linha_z(LINHAS[1]) - 0.6, linha_z(27) + 1.25, linha_z(30) + 0.6):
        for lado in (-1, 1):
            M.por('WallCorner_01', lado * 2.2, z, escala=(1.0, (PASSARELA - 0.3) / 10.0, 1.0), colide='caixa')
    return M


def cidade_em_volta():
    """O centro da cidade em que a estação fica (peças do kit da Cidade, num desenho à parte, com colisão): os prédios de
    dois corpos que fecham o mapa, a rua e a calçada da praça ao norte com carros, árvores, postes e a banca, e, para
    fora do mapa, mais duas fileiras de prédios."""
    C = Montagem('fps1', colisao_da_cidade); K = C.kit
    for nome, cor in (('asfalto', (0.30, 0.31, 0.33)), ('calcada', (0.48, 0.49, 0.51)), ('branco', (0.92, 0.92, 0.92))): K.chapa(nome, cor)
    # (os prédios que fecham o mapa: um corpo só, na altura que o kit lhes deu — 10, 12 e 15 m —, sem repetir o vizinho.
    # Eram dois corpos empilhados, com a emenda à vista, e o mesmo prédio várias vezes seguidas: "não ficaram naturais",
    # Jhon, 20/11. A altura para o lobo não subir já não é deles: o muro de fora não segura mais a corrida)
    for k, reg in enumerate(P.regioes('h')):
        for i, ret in enumerate(P.fatiar(reg, 3)):
            nome = ('House_01', 'House_04', 'House_03')[(i + k) % 3]
            C.encaixar(nome, *ret, yaw=P.frente(ret, 'hMUr'), colide='caixa')
    # a rua (asfalto, faixa tracejada), a calçada da praça e o beco ao lado do prédio
    C.chapa('asfalto', -125.0, linha_z(3), 125.0, linha_z(6), y=-0.01, alto=0.025)          # a rua segue para fora do mapa
    for lado in (-1, 1):                    # onde o mapa acaba: a rua interditada (por trás, a barreira invisível)
        for i, dz in enumerate((0.9, 2.8, 4.7, 6.6)):
            C.por('Barrier_01', lado * (51.4 - 0.3 * (i % 2)), linha_z(3) + dz, yaw=90.0 + (8.0 if i % 2 else -6.0), colide='caixa')
        C.por('RoadSign_01', lado * 50.4, linha_z(3) + 0.5, yaw=-90.0 * lado, colide=None)
    C.chapa('calcada', -52.5, linha_z(6), 52.5, linha_z(NORTE) + 1.25, y=0.0, alto=0.04)
    for lado in (-1, 1): C.chapa('calcada', lado * 52.5, linha_z(NORTE), lado * 48.75, linha_z(SUL + 1), y=0.0, alto=0.04)
    for i in range(-20, 20): C.chapa('branco', i * 5 + 1.5, linha_z(4) + 1.17, i * 5 + 3.5, linha_z(4) + 1.33, y=0.0, alto=0.025)
    for xs in (-3.0, ):
        for k in range(-3, 3): C.chapa('branco', -1.25 + k * 1.0 + 0.2, linha_z(3) + 0.4, -1.25 + k * 1.0 + 0.7, linha_z(6) - 0.4, y=0.0, alto=0.025)
    for L, r, c, x, z in P.celulas('atiuq'):
        if L == 'a': C.por(P.sorteio(x, z, CARROS), x, z, yaw=90.0 + P.desvio(x, z, 10))
        elif L == 't':
            C.por(P.sorteio(x, z, ('Tree_01', 'Tree_02')), x, z, yaw=P.desvio(x, z, 180), colide=None); C.barra(x, z, 0.28, 3.0)          # (o tronco: 0,24 m; a caixa de 0,45 segurava tiro de raspão)
            for i, (dx, dz) in enumerate(((0.7, 0.3), (-0.5, 0.6), (0.1, -0.8))): C.por('Grass_01', x + dx, z + dz, yaw=70.0 * i, colide=None)
        elif L == 'i':
            # (a caixa do poste vai no poste: a peça tem o braço de 2,4 m para um lado e era centrada pela caixa dela, com o
            # poste 1,1 m ao lado — a caixa ficava no ar, invisível, e segurava tiro; 20/11)
            pe = C.por('Light_01', x, z, colide=None).pontos(np.zeros((1, 3)))[0]; C.barra(pe[0], pe[2], 0.12, 4.6)          # (o poste: 0,11 m)
        elif L == 'u': C.por('Trash_01', x, z); C.por('Garbage_01', x + 1.6, z, colide=None)
        elif L == 'q': C.por('House_08', x, z, yaw=180.0); C.por('FlowerBed_01', x + 2.6, z, colide=None); C.por('FlowerBed_01', x - 2.6, z, colide=None)
    for lado in (-1, 1):
        for i in range(4): C.por('PowerLines_01', lado * (8.0 + 12.0 * i), linha_z(3) + 0.5, yaw=90.0, colide=None, planta=False)
    n0 = len(C.itens)
    C.arredores(('House_02', 'Bank_01', 'House_04', 'Office_01', 'House_03', 'House_02'), X, Z, folga=3.0, fileiras=2)
    # a rua continua entre os prédios de fora: saem os que ficariam em cima dela
    def na_rua(it):
        c = it.cantos(); return abs(c[:, 0]).min() > X and c[:, 2].max() > linha_z(3) - 0.5 and c[:, 2].min() < linha_z(6) + 0.5
    C.itens[n0:] = [it for it in C.itens[n0:] if not na_rua(it)]
    return C


def mato():
    """O mato que cresce no leito das linhas (o kit da estação não tem planta: estas são as do kit do Escritório, num desenho à parte,
    sem colisão): capim entre os trilhos e ao pé do muro, moitas e tocos no pátio."""
    V = Montagem('fps3', lambda nome: None)
    for L, r, c, x, z in P.celulas('.'):
        s_ = P.sorte(x, z, 31)
        if r in (20, 21, 25, 26, 27, 28, 29) and s_ < 0.3:
            for i in range(1 + int(s_ * 20) % 3): V.por(('Grass_01', 'Grass_02')[i % 2], x - 0.8 + 0.7 * i, z + (0.9 if i % 2 else -0.7), yaw=50.0 * i, escala=(1.0, 1.3, 1.6)[int(P.sorte(x, z, i) * 3)], planta=False)
        elif r >= 30 and s_ < 0.07:
            V.por(P.sorteio(x, z, ('Grass_01', 'Plant_01', 'Grass_02', 'Stump_01', 'Log_01', 'Grass_01', 'Mushroom_01'), 32), x + 0.5, z - 0.4, yaw=P.desvio(x, z, 180), planta=False)
    for r, c, x, z, yaw, dist in P.beiras('h'):
        if r < 20: continue
        s_ = P.sorte(x, z, 33 + int(yaw))
        if s_ < 0.3:
            for i in range(2 + int(s_ * 10) % 2): V.encostar(('Grass_02', 'Grass_01')[i % 2], x, z, yaw, dist, folga=0.1 + 0.2 * (i % 2), lado=-0.8 + 0.7 * i, escala=(1.0, 1.3, 1.6)[int(P.sorte(x, z, i + 5) * 3)], planta=False)
        elif s_ < 0.36: V.encostar('Plant_01', x, z, yaw, dist, folga=0.1, escala=0.8, planta=False)
    return V


def montar():
    m = Mapa("estacao", "Estação de Trem", int(2 * X), int(2 * Z))
    m.material("patio", "asfalto", (0.80, 0.80, 0.82), 3.0)
    m.caixa("Chao", "Asfalto", (0, -0.25, 0), (420, 0.5, 420), "patio", colide=False)
    M = desenho(); C = cidade_em_volta()
    M.aplicar(m, "Estacao", "estacao")
    C.aplicar(m, "Estacao", "estacao_cidade", no_planta='Cidade', modelo='Cidade')
    mato().aplicar(m, "Estacao", "estacao_mato", no_planta='Mato', modelo='Mato')
    fechar(m, X, Z, altura=22.0)
    barrar(m, P, 'hMr', 22.0)
    # por cima da boca de cada túnel, a barreira também (a boca fica aberta até 5,45 m)
    for i, reg in enumerate(P.regioes('U')):
        x0, z0, x1, z1 = P.mundo(*reg[1:])
        if x0 < 0: x0 -= 0.1; x1 -= 0.06
        else: x0 += 0.06; x1 += 0.1
        m.caixa('Colisao', f'BarreiraTunel{i + 1}', ((x0 + x1) / 2, (5.51 + 22.0) / 2, (z0 + z1) / 2), (x1 - x0, 22.0 - 5.51, z1 - z0 + 0.2), 'kit_colisao', oculta=True, minimapa=False)
    m.panorama('cidade', X + 170.0, altura=130.0, base=-6.0, cor=(1.0, 0.84, 0.70))

    oc = M.ocupacao((-X, -Z), (X, Z)) | C.ocupacao((-X, -Z), (X, Z))
    for reg in P.regioes('r'):                  # a rua para fora do mapa é barrada: ninguém nasce nem passa ali
        x0, z0, x1, z1 = P.mundo(*reg[1:]); oc[int((z0 + Z) / 0.25):int((z1 + Z) / 0.25), int((x0 + X) / 0.25):int((x1 + X) / 0.25)] = True
    g = Grade(oc, (-X, -Z), 0.25, folga=0.55)
    a = nascer(m, g, (-49.0, linha_z(23) + 1.0), AZUL, espelho=True)[0]
    rotas = dict(
        plataforma=P.rota(a, '12'), ilha=P.rota(a, '345'), entreTrens=P.rota(a, '67'), patio=P.rota(a, '689'),
        saguao=P.rota(a, '10@'), praca=P.rota(a, '1%&'), cruzada=P.rota(a, '342', '987'), fundos=P.rota(a, '6897', '4@0'),
    )
    m._grade = g
    espalhar(m, g, a, quantos=16)          # os pontos do mata-mata
    for nome, pontos in rotas.items():
        m.rota(nome, [(x, 0.5, z) for x, z in g.rota(pontos)])

    # as lâmpadas do prédio (o teto fecha o sol): o saguão e as alas
    for i, x in enumerate(range(-40, 41, 10)):
        m.luz("Luzes", f"Predio{i}", (float(x), 8.0, linha_z(12) + 1.25), (1.0, 0.84, 0.62), 3.0, 16)
    # fim de tarde: sol baixo e quente, sombra comprida dos trens e violeta
    luz_de_jogo(m, 'tarde', 104, 26, giro_ceu=140.0, alcance=190.0)
    lampadas(m, [M, C])
    return m
