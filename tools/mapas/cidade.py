"""
Cidade — mapa grande (120 × 80 m), a céu aberto, de dia.

Um bairro inteiro: três ruas de ponta a ponta (a do norte, a avenida no meio e a do sul), duas transversais e o bulevar
do centro, e dentro dos quarteirões os becos de 2,5 m — entre as lojas e as casas, em volta do prédio em obras (três
andares, com escada por dentro), atrás do banco. Cada time nasce num posto de gasolina, numa ponta da avenida.

Montado com as peças do kit "Low Poly FPS Map Vol.1 — City" (JustCreate; ``tools/mapas/kits``), com planta própria (o kit
vem sem o mapa montado). A planta é o desenho em letras aqui embaixo (``kits/planta.py``): cada letra, 2,5 m.

As quatro perguntas do teste de mapa:
  1. Caminho que só o lobo faz: do carro ou da caçamba para o teto das lojas (3,4 m) e dali, de teto em teto, por cima
     dos becos; as lajes do prédio em obras.
  2. Onde o humano vê o lobo chegando: as lajes do prédio em obras e as esquinas do bulevar, de frente para as três ruas.
  3. Corredor que o humano deve evitar: os becos de 2,5 m (duas paredes para o lobo ricochetear, sem para onde fugir) e a
     avenida de ponta a ponta, só com carro e barreira de cobertura.
  4. Cair em cima de quem não olha: do teto das lojas e da cobertura do posto sobre quem passa no beco ou nasce embaixo.

O leste é o oeste girado 180°: o azul nasce no posto de −X, o laranja no de +X, e os dois veem o mesmo mapa.
"""
import numpy as np

from .kit import Mapa, AZUL
from .kits.montagem import Montagem, Grade, fechar, nascer, barrar, espalhar, luz_de_jogo, lampadas
from .kits.planta import Planta

# h casa (prédio fechado)   l loja baixa (anda-se no teto)   T torre   B banco   I igreja   K prédio em obras   O escritório
# S oficina   q banca   c carro ao longo da rua   C carro atravessado   x barreira   b caixas   u caçamba   o tambores
# t árvore   p poste   = ! grade de ferro   1–9 por onde passam as rotas
OESTE = [
    "TTTTThhhhhhhhhhhhhhhhhhh",
    "TTTTThhhhhhhhhhhhhhhhhhh",
    "TTTTThhhhhhhhhhhhhhhhhhh",
    "TTTTT..t..p..c..t..t....",
    "TTTTT....1......x....2..",
    "hhhIIIII...p......C.....",
    "hhhIIIII..lll.KKK.OOOO..",
    "hhhIIIII..lll.KKK.OOOO.t",
    "hhhIIIII..lll.KKK.OOOO..",
    "hhhIIIII.....8KKK..b....",
    "hhhIIIII..hhh.KKK.SSSS..",
    "hhhIIIII..hhh.=b=.SSSS..",
    "hhhIIIII..hhh..u..SSSS.c",
    "hhh...t....p.....t...t..",
    "hhh......3..qq......c...",
    "hhh......x..qq.4.x.....5",
    "hhh.......c......x......",
    "hhh.............C....x..",
    "hhh....t..p....t....p...",
    "hhhTTTTT..BBBBB.hhhhhh.t",
    "hhhTTTTT..BBBBB.hhhhhh..",
    "hhhTTTTT..BBBBB.hhhhhh..",
    "hhhTTTTT..BBBBB9........",
    "hhhTTTTT..BBBBB.llllll..",
    "hhh.c.c.!.=u.=..llllll..",
    "hhh.....!..c..b.llllll.c",
    "hhh.c.c.!....t....p...t.",
    "hhh......6.....x....7...",
    "hhh..t...p...C..t...t...",
    "hhhhhhhhhhhhhhhhhhhhhhhh",
    "hhhhhhhhhhhhhhhhhhhhhhhh",
    "hhhhhhhhhhhhhhhhhhhhhhhh",
]
SOLIDOS = "hlTBIKOSq"
P = Planta(OESTE, 2.5, 'giro')
X, Z = P.X, P.Z            # o espaço jogável: de −X a X e de −Z a Z

CAIXA = ('AmoBox', 'ATM', 'Barrel', 'Box_', 'Car_', 'OldCar', 'Fan', 'FlowerBed', 'Garbage', 'Hydrant', 'Trash', 'Wheel', 'Rock', 'Pallet',
         'RoadPart', 'Barrier', 'Parking_01', 'House_01', 'House_02', 'House_03', 'House_04', 'Bank', 'Office', 'Fence_03')
SEM = ('BG_', 'Grass', 'Plant', 'Mud', 'Luke', 'PowerLines', 'RoadSign', 'Tree', 'Light', 'ParkingBG', 'asfalto', 'calcada', 'meiofio', 'branco', 'amarelo')
CARROS = ('OldCar_01', 'OldCar_02', 'OldCar_03', 'OldCar_04', 'OldCar_05', 'OldCar_06', 'Car_01')


# ---- as ruas e as calçadas (pedido do Jhon, 20/11: "coloque calçadas na cidade, não deixe asfalto e chão se misturarem")
# O asfalto fica MEIO_FIO abaixo da calçada, com o meio-fio na borda: são dois níveis de verdade, no desenho e na
# colisão, e um não fica por cima do outro (era uma chapa a 3 cm da outra, e de longe as duas brigavam). A calçada é
# o nível zero de sempre — os prédios, os becos e o posto não mudam; quem desce é a rua e o que está nela.
RUAS = ((5, 3, 45, 6), (3, 26, 43, 29), (3, 14, 45, 18), (8, 3, 10, 29), (38, 3, 40, 29), (22, 3, 26, 29), (3, 24, 8, 29), (40, 3, 45, 8))
MEIO_FIO = 0.14           # o degrau: o jogador (0,3 m) e o bot (0,5 m) sobem andando
CALCADA = 1.6             # a largura da calçada ao pé dos prédios (as árvores e os postes ficam nela)
GUIA = 0.2                # a largura do meio-fio
PATIO = 41.0              # daqui para fora, na avenida, é o pátio do posto (onde se nasce): calçada
PASSO = 0.1               # a malha da planta do asfalto


def planta_das_ruas():
    """Onde é asfalto, numa malha de PASSO vista de cima (linhas em z, colunas em x), de −X−12 a X+12: as ruas da planta,
    menos o pátio do posto, com a calçada tirada de cada borda."""
    fora = 12.0; nx, nz = int(round((2 * X + 2 * fora) / PASSO)), int(round((2 * Z + 2 * fora) / PASSO))
    col = lambda x: int(round((x + X + fora) / PASSO)); lin = lambda z: int(round((z + Z + fora) / PASSO))
    rua = np.zeros((nz, nx), bool)
    for c0, r0, c1, r1 in RUAS:
        x0, z0, x1, z1 = P.mundo(c0, r0, c1, r1); rua[lin(z0):lin(z1), col(x0):col(x1)] = True
    rua[lin(-7.5):lin(7.5), :col(-PATIO)] = False; rua[lin(-7.5):lin(7.5), col(PATIO):] = False
    k = int(round(CALCADA / PASSO)); e = rua.copy()
    for d in range(1, k + 1): e &= np.roll(rua, d, 1) & np.roll(rua, -d, 1)
    f = e.copy()
    for d in range(1, k + 1): f &= np.roll(e, d, 0) & np.roll(e, -d, 0)
    return f, (-X - fora, -Z - fora)


def retangulos(mascara, origem):
    """A máscara em retângulos (x0, z0, x1, z1), poucos: as faixas iguais de linhas seguidas viram um só."""
    out, abertos = [], {}
    for r in range(mascara.shape[0] + 1):
        linha = mascara[r] if r < mascara.shape[0] else np.zeros(mascara.shape[1], bool)
        d = np.diff(np.concatenate([[0], linha.astype(np.int8), [0]]))
        faixas = set(zip(np.flatnonzero(d == 1).tolist(), np.flatnonzero(d == -1).tolist()))
        for fx in [f_ for f_ in abertos if f_ not in faixas]:
            r0 = abertos.pop(fx); out.append((origem[0] + fx[0] * PASSO, origem[1] + r0 * PASSO, origem[0] + fx[1] * PASSO, origem[1] + r * PASSO))
        for fx in faixas: abertos.setdefault(fx, r)
    return out


def crescer(mascara, k):
    out = mascara.copy()
    for d in range(1, k + 1): out |= np.roll(mascara, d, 1) | np.roll(mascara, -d, 1)
    m2 = out.copy()
    for d in range(1, k + 1): out |= np.roll(m2, d, 0) | np.roll(m2, -d, 0)
    return out


def calcadas(M):
    """O chão em dois níveis e o que está na rua descendo com ela. Chamada no fim de ``desenho``, com tudo já posto.
    Devolve os retângulos da calçada (com o meio-fio), para a colisão."""
    rua, origem = planta_das_ruas(); guia = crescer(rua, int(round(GUIA / PASSO))) & ~rua
    for x0, z0, x1, z1 in retangulos(rua, origem): M.chapa('asfalto', x0, z0, x1, z1, y=-MEIO_FIO - 0.03, alto=0.03)
    for x0, z0, x1, z1 in retangulos(~rua & ~guia, origem): M.chapa('calcada', x0, z0, x1, z1, y=-MEIO_FIO - 0.03, alto=MEIO_FIO + 0.03)
    for x0, z0, x1, z1 in retangulos(guia, origem): M.chapa('meiofio', x0, z0, x1, z1, y=-MEIO_FIO - 0.03, alto=MEIO_FIO + 0.03)

    def na_rua(lo, hi):
        """Quanto da pegada (de lo a hi, vista de cima) está no asfalto: 0 a 1."""
        i0, i1 = int(np.floor((lo[2] - origem[1]) / PASSO)), int(np.ceil((hi[2] - origem[1]) / PASSO))
        j0, j1 = int(np.floor((lo[0] - origem[0]) / PASSO)), int(np.ceil((hi[0] - origem[0]) / PASSO))
        t = rua[max(i0, 0):max(i1, i0 + 1), max(j0, 0):max(j1, j0 + 1)]
        return float(t.mean()) if t.size else 0.0

    fica = []
    for it in M.itens:
        nome = it.peca.nome
        if nome in ('asfalto', 'calcada', 'meiofio'): fica.append(it); continue
        c = it.cantos(); lo, hi = c.min(0), c.max(0)
        if lo[1] > 0.06 or lo[1] < -0.01: fica.append(it); continue          # o que não está no chão (em cima de outra coisa)
        parte = na_rua(lo, hi)
        if nome in ('branco', 'amarelo'):                                    # a pintura: só a que cabe inteira no asfalto
            if parte > 0.999: it.t[1] -= MEIO_FIO; fica.append(it)
            continue
        if nome.startswith(('Tree', 'Light', 'PowerLines', 'RoadSign', 'Hydrant')):
            # o que tem pé (o tronco, o poste): vale onde o pé está, não a copa
            meio = (lo + hi) / 2; parte = na_rua(meio - 0.15, meio + 0.15)
            if parte > 0.5: it.t[1] -= MEIO_FIO
            if hasattr(it, '_aabb'): del it._aabb
            fica.append(it); continue
        if 0.0 < parte < 0.999 and max(hi[0] - lo[0], hi[2] - lo[2]) < 6.0:
            # a cavalo no meio-fio (o carro encostado no prédio, a caçamba na beira): vai inteiro para a rua ou inteiro
            # para a calçada, pelo caminho mais curto (no empate, para o lado do centro do mapa — o outro time vê o
            # mesmo). Ficando onde estava, metade dele flutuava 14 cm acima do asfalto (Jhon, 20/11).
            meio = (lo + hi) / 2; melhor = None
            for dist in np.arange(0.1, 2.6, 0.1):
                for dx, dz in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    d = np.array([dx * dist, 0.0, dz * dist]); onde = na_rua(lo + d, hi + d)
                    if onde > 0.999 or onde < 0.001:
                        nota = (round(float(dist), 2), float(dx * meio[0] + dz * meio[2]))
                        if melhor is None or nota < melhor[0]: melhor = (nota, d, 1.0 if onde > 0.5 else 0.0)
                if melhor: break
            if melhor: it.t = it.t + melhor[1]; parte = melhor[2]
            elif not it.colide: continue                                                       # enfeite que não cabe em lado nenhum: sai
        if parte > 0.999 or (parte >= 0.5 and not it.colide): it.t[1] -= MEIO_FIO
        if hasattr(it, '_aabb'): del it._aabb
        fica.append(it)
    M.itens[:] = fica
    return retangulos(~rua, origem)


def colisao(nome):
    return 'caixa' if nome.startswith(CAIXA) else None if nome.startswith(SEM) else 'malha'


def desenho():
    M = Montagem('fps1', colisao); K = M.kit
    for nome, cor in (('asfalto', (0.30, 0.31, 0.33)), ('calcada', (0.48, 0.49, 0.51)), ('meiofio', (0.66, 0.66, 0.66)), ('branco', (0.92, 0.92, 0.92)),
                      ('amarelo', (0.9, 0.75, 0.1))):
        K.chapa(nome, cor)

    # ---- o posto, onde o azul nasce (o do laranja é ele girado): a cobertura de 3,8 m, as bombas, caixas
    M.por('Gas_Station_01', -47.0, 0.0, yaw=90)
    M.por('AmoBox', -51.2, 5.4, yaw=20); M.por('AmoBox_01', -51.6, -5.6); M.por('Barrel_01', -51.9, -6.6); M.por('Barrel_02', -51.2, -6.9)
    M.por('Box_01', -51.7, 6.6); M.por('Box_02', -51.7, 6.6, y=0.9, yaw=15); M.por('Box_03', -50.7, 6.8, yaw=40)
    M.por('Trash_02', -51.8, 2.2, yaw=90); M.por('Wheel_01', -52.0, -1.2, yaw=90); M.por('Pallet_01', -44.2, -6.6, yaw=10)
    M.por('RoadPart_02', -41.6, -4.4); M.por('RoadPart_04', -41.6, 3.6); M.por('Car_01', -43.4, 5.9, yaw=-25)
    M.simetrico()

    # ---- o chão (a calçada, o asfalto e o meio-fio entram no fim, em ``calcadas``): a faixa tracejada no eixo das ruas
    for z in (-28.75, 0.0, 28.75):
        for i in range(-10, 10):
            if not (z == 0.0 and abs(i * 5 + 2.5) < 5): M.chapa('branco', i * 5 + 1.5, z - 0.08, i * 5 + 3.5, z + 0.08, y=0.0, alto=0.012)
    for i in range(-6, 6):
        if abs(i * 5 + 2.5) > 9: M.chapa('amarelo', -0.08, i * 5 + 1.5, 0.08, i * 5 + 3.5, y=0.0, alto=0.012)

    # ---- os prédios: um por retângulo de letras (os de casa e de loja, cortados de três em três células)
    # (os prédios de casa têm um corpo só, na altura do kit — 10, 12 e 15 m — e não repetem o vizinho. Os que fecham o
    # mapa eram dois corpos empilhados, com a emenda à vista: "não ficaram naturais", Jhon, 20/11. A altura para o lobo
    # não subir já não é deles: o muro de fora não segura mais a corrida — ``Player.BackedByBarrier``)
    for k, reg in enumerate(P.regioes(SOLIDOS)):
        L = reg[0]
        if L == 'h':
            for i, ret in enumerate(P.fatiar(reg, 3)):
                M.encaixar(('House_01', 'House_04', 'House_03')[(i + k) % 3], *ret, yaw=P.frente(ret, SOLIDOS))
        elif L == 'l':
            for ret in P.fatiar(reg, 3):
                x, z = (ret[0] + ret[2]) / 2, (ret[1] + ret[3]) / 2; nome = P.sorteio(x, z, ('House_05', 'House_06', 'House_07'))
                M.encaixar(nome, *ret, yaw=P.frente(ret, SOLIDOS), colide='malha')
        elif L == 'q':
            x0, z0, x1, z1 = P.mundo(*reg[1:]); M.por('House_08', (x0 + x1) / 2, (z0 + z1) / 2, yaw=P.frente((x0, z0, x1, z1), SOLIDOS))
        else:
            nome = dict(T='House_02', B='Bank_01', I='Church_01', K='Construction_01', O='Office_01', S='AutoService_01')[L]
            ret = P.mundo(*reg[1:]); p = K[nome]
            M.encaixar(nome, *ret, yaw=P.frente(ret, SOLIDOS, (p.tam[0], p.tam[2])))

    # ---- as grades de ferro
    for xa, za, xb, zb in P.paredes('=', '!', '*')[0]:
        M.muro('Fence_03', xa, za, xb, zb, 4.2)

    # ---- o que está na rua
    for L, r, c, x, z in P.celulas('cCxbuotp'):
        nx, nz = P.corrida(r, c, SOLIDOS + '=!'); ao_x = nx >= nz
        if L in 'cC':
            yaw = (90.0 if ao_x else 0.0) + (90.0 if L == 'C' else 0.0) + P.desvio(x, z, 14)
            M.por(P.sorteio(x, z, CARROS), x, z, yaw=yaw)
        elif L == 'x':
            yaw = (0.0 if ao_x else 90.0) + P.desvio(x, z, 8)
            M.por(P.sorteio(x, z, ('RoadPart_03', 'RoadPart_05')), x, z, yaw=yaw)
            a = 1.6 if ao_x else 0.0; M.por(P.sorteio(x, z, ('RoadPart_06', 'RoadPart_07', 'RoadPart_02'), 1), x + (0 if ao_x else 2.3), z + (2.3 if ao_x else 0), yaw=yaw)
        elif L == 'b':
            g = P.desvio(x, z, 30); M.por('Box_01', x - 0.5, z - 0.3, yaw=g); M.por('Box_02', x + 0.5, z + 0.2, yaw=g + 20); M.por('Box_03', x - 0.3, z - 0.1, y=0.9, yaw=g + 35)
        elif L == 'u':
            M.por(P.sorteio(x, z, ('Trash_01', 'Trash_02')), x, z, yaw=(0.0 if ao_x else 90.0)); M.por('Garbage_01', x + (1.6 if ao_x else 0), z + (0 if ao_x else 1.6))
        elif L == 'o':
            M.por('Barrel_01', x - 0.35, z); M.por('Barrel_02', x + 0.4, z + 0.3)
        elif L == 't':
            nome = P.sorteio(x, z, ('Tree_01', 'Tree_02')); M.por(nome, x, z, yaw=P.desvio(x, z, 180), colide=None); M.barra(x, z, 0.45, 3.0)
            for i, (dx, dz) in enumerate(((0.7, 0.3), (-0.5, 0.6), (0.1, -0.8), (-0.8, -0.3))): M.por('Grass_01', x + dx, z + dz, yaw=70.0 * i, colide=None)
            M.por('Plant_01', x + 0.9, z - 0.6, yaw=P.desvio(x, z, 90), colide=None)
        elif L == 'p':
            M.por('Light_01', x, z, yaw=(0.0 if ao_x else 90.0), colide=None); M.barra(x, z, 0.3, 4.6)

    # ---- o que dá vida à rua, sem colisão: canteiros, placas, lixo e ar-condicionado ao pé dos prédios; bueiros e poças
    for r, c, x, z, yaw, dist in P.beiras(SOLIDOS):
        M.grupo()
        s_ = P.sorte(x, z, 3 + int(yaw)); alto = P.letra(r - (yaw == 0) + (yaw == 180), c - (yaw == 90) + (yaw == -90)) in 'hTBIO'
        e = dict(colide=None, planta=False)
        if s_ < 0.07:
            for i in (-1, 0, 1): M.encostar('FlowerBed_01', x, z, yaw, dist, lado=0.65 * i, **e)
            M.encostar('Plant_01', x, z, yaw, dist, y=0.1, **e)
        elif s_ < 0.11: M.encostar('Hydrant', x, z, yaw, dist, folga=0.5, **e)
        elif s_ < 0.16: M.encostar('RoadSign_01', x, z, yaw, dist, folga=0.25, **e)
        elif s_ < 0.22: M.encostar('Garbage_01', x, z, yaw, dist, folga=0.1, **e); M.encostar('Wheel_02', x, z, yaw, dist, folga=0.1, lado=1.0, **e)
        elif s_ < 0.31:
            # o ar-condicionado só na parede fechada e alta: na frente do prédio em obras (aberto), da banca e da loja
            # baixa ele ficava pendurado no ar (Jhon, 20/11: "alguns objetos estão flutuando na cidade")
            if alto:
                M.encostar('Fan_01', x, z, yaw, dist, y=2.7, folga=0.0, lado=0.4, **e); M.encostar('Fan_01', x, z, yaw, dist, y=5.9, folga=0.0, lado=-0.6, **e)
            else:
                for i in range(3): M.encostar('Grass_01', x, z, yaw, dist, folga=0.05 + 0.2 * (i % 2), lado=-0.8 + 0.7 * i, **e)
        elif s_ < 0.35: M.encostar('Pallet_02', x, z, yaw, dist, folga=0.15, **e); M.encostar('Barrel_01', x, z, yaw, dist, folga=0.1, lado=1.05, **e)
        elif s_ < 0.44:
            for i in range(3): M.encostar('Grass_01', x, z, yaw, dist, folga=0.05 + 0.2 * (i % 2), lado=-0.8 + 0.7 * i, **e)
        elif s_ < 0.47: M.encostar('ATM', x, z, yaw, dist, folga=0.0, **e)
    M.grupo(fim=True)
    encostadas = {(r, c) for r, c, *_ in P.beiras(SOLIDOS)}
    for L, r, c, x, z in P.celulas('.'):
        if (r, c) in encostadas: continue
        s_ = P.sorte(x, z, 9)
        if s_ < 0.03: M.por('Luke_01', x + 0.4, z - 0.3, colide=None, planta=False)
        elif s_ < 0.06: M.por(P.sorteio(x, z, ('Mud_01', 'Mud_02'), 2), x, z, yaw=P.desvio(x, z, 180), colide=None, planta=False)
    # as faixas de pedestre nas esquinas da avenida e os postes de luz com os fios, na calçada de fora das ruas norte e sul
    for xs in (-34.5, -8.5, 6.0, 32.0):
        for k in range(-4, 5): M.chapa('branco', xs, k * 1.3 - 0.3, xs + 2.5, k * 1.3 + 0.3, y=0.0, alto=0.012)
    for sinal in (-1, 1):
        zf = sinal * (Z - 7.9)
        for i in range(-4, 5):
            M.por('PowerLines_01', i * 10.0 + 3.0, zf, yaw=90.0, colide=None, planta=False)
            if i < 4: M.por('PowerLines_02', i * 10.0 + 8.0, zf, y=5.1, escala=(1.25, 1.0, 1.0), colide=None, planta=False)

    # ---- o cruzamento do centro: dois carros e barreiras em cruz, bueiros
    M.por('OldCar_03', -2.2, 2.4, yaw=30); M.por('OldCar_05', 2.2, -2.4, yaw=210)
    M.por('RoadPart_03', -6.5, -3.0); M.por('RoadPart_03', 6.5, 3.0)
    M.por('Luke_01', 3.4, 4.0); M.por('Luke_01', -3.4, -4.0)
    # ---- a cidade continua para fora do mapa: duas fileiras de prédios em toda a volta (sem colisão)
    M.arredores(('House_02', 'Bank_01', 'House_04', 'Office_01', 'House_03', 'House_02'), X, Z, folga=3.0, fileiras=2)
    # ---- o chão em dois níveis: a calçada (o nível zero), o asfalto MEIO_FIO abaixo e o meio-fio entre os dois
    M.calcadas = calcadas(M)
    return M


def montar():
    m = Mapa("cidade", "Cidade", int(2 * X), int(2 * Z))
    M = desenho()
    M.aplicar(m, "Cidade", "cidade")
    fechar(m, X, Z, piso=-MEIO_FIO)          # o chão de baixo é o do asfalto; a calçada tem a colisão dela, até o zero
    for i, (x0, z0, x1, z1) in enumerate(M.calcadas):
        m.caixa('Colisao', f'Calcada{i + 1}', ((x0 + x1) / 2, -0.5, (z0 + z1) / 2), (x1 - x0, 1.0, z1 - z0), 'kit_colisao', oculta=True, minimapa=False)
    barrar(m, P, 'hT', 22.0)
    m.panorama('cidade', X + 170.0, altura=130.0, base=-6.0)

    g = Grade(M.ocupacao((-X, -Z), (X, Z)), (-X, -Z), 0.25, folga=0.55)
    a = nascer(m, g, (-47.5, 1.0), AZUL)[0]
    rotas = dict(
        ruaNorte=P.rota(a, '12', '76'), avenida=P.rota(a, '345'), ruaSul=P.rota(a, '67', '21'),
        becos=P.rota(a, '382', '79'), fundos=P.rota(a, '697', '283'), cruzadaNorte=P.rota(a, '12'), cruzadaSul=P.rota(a, '67'),
        obras=P.rota(a, '1845', '49'),
    )
    m._grade = g
    espalhar(m, g, a, quantos=16)          # os pontos do mata-mata: todos contra todos, espalhados pelo mapa
    for nome, pontos in rotas.items():
        m.rota(nome, [(x, 0.5, z) for x, z in g.rota(pontos)])

    luz_de_jogo(m, 'dia', 38, 42)
    lampadas(m, [M])
    return m
