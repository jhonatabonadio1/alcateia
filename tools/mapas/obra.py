"""
Obra — mapa grande (100 × 70 m), a céu aberto, de dia.

Um prédio de concreto em construção (60 × 40 m) ocupa o meio do canteiro: no térreo, um corredor de 60 m de ponta a
ponta, quatro salas de canto e dois salões com pilares, tudo ligado por portas; duas escadas sobem à laje do primeiro
piso (4 m), aberta dos lados, com muretas de tijolo; outras duas, à laje de cima (8 m), só no miolo. Em volta, o
canteiro: material empilhado, muretas, andaimes encostados no prédio, as gruas. Cada time nasce numa ponta do canteiro.

Montado com as peças do kit "Low Poly FPS Map Vol.6 — Construction" (JustCreate; ``tools/mapas/kits``), com planta
própria. A planta é o desenho em letras aqui embaixo (``kits/planta.py``), um por piso: cada letra, 2,5 m.

As quatro perguntas do teste de mapa:
  1. Caminho que só o lobo faz: do andaime (2,5 m) para a laje do primeiro piso, e de laje em laje pelo lado de fora,
     sem passar pelas escadas.
  2. Onde o humano vê o lobo chegando: a laje de cima (8 m), com o canteiro inteiro à vista; as janelas das salas de
     canto, de frente para o pátio.
  3. Corredor que o humano deve evitar: o corredor do térreo, 60 m com porta dos dois lados.
  4. Cair em cima de quem não olha: das lajes, pelos vãos das escadas e pelas beiradas sem mureta, sobre quem passa
     embaixo.

O leste é o oeste girado 180°.
"""
from .kit import Mapa, AZUL
from .kits.montagem import Montagem, Grade, fechar, nascer, barrar, espalhar, luz_de_jogo, lampadas
from .kits.planta import Planta
from .kits.kit_fbx import Peca
import numpy as np

# # muro do canteiro   - | + parede de concreto (3,8 m)   D porta   J janela (vão)   = ! * mureta de tijolo
# > < ^ v escada (sobe para onde aponta)   _ laje   s andaime   t grua   b caixotes   r bobinas   c cimento   g gerador
# w banheiros   m cavalete de material   k tijolos   l refletor de obra   a monte de areia   p monte de brita
# 1–9 por onde passam as rotas
# (as colunas não se desenham: vão na malha de 7,5 m do prédio, em todos os pisos — ``colunas``)
TERREO = [
    "####################",
    "#....a....c.....b...",
    "#...r........=====..",
    "#.......1.........2.",
    "#..b...p....t....b..",
    "#.....l...s.....s...",
    "#.......+--D--+-JJ--",
    "#..m....|.c...|.....",
    "#.......|..b..|..r..",
    "#....k..D.7...D.9...",
    "#...a...|...l.|...m.",
    "#.w.....|.....|.....",
    "#.w...!.+--D--+--D--",
    "#.....!.D...b.......",
    "#.......D.....3..c.4",
    "#..g..!.+-D---+-D---",
    "#.....!.|..c..|.l...",
    "#.......|.....|.....",
    "#....k..D...b8|.>>>.",
    "#....p..|.....D.....",
    "#..c....|.r...|..b..",
    "#.......+-JJ--+--D--",
    "#.....r...s.....s...",
    "#..b......a....b....",
    "#.......5.........6.",
    "#....=====...c....l.",
    "#.m.......k....a....",
    "####################",
]
VAZIO = "...................."
PISO1 = [VAZIO] * 6 + [
    "........+--JJ-+=====",
    "........|_____!_____",
    "........|_b_______c_",
    "........J_____!>>>__",
    "........|___________",
    "........|__k__!___l_",
    "........!___________",
    "........!___c_______",
    "........!_________b_",
    "........!___________",
    "........|_____!_____",
    "........|__r_____b__",
    "........J_____!_..._",
    "........|_________l_",
    "........|_c___!_____",
    "........+-JJ--+=====",
] + [VAZIO] * 6
PISO2 = [VAZIO] * 7 + [
    "...............*====",
    "...............!____",
    "..................__",
    "...............!_b__",
    "...............!____",
    "...............!___c",
    "..............._____",
    "..............._____",
    "...............!_k__",
    "...............!____",
    "...............!__c_",
    "...............!____",
    "...............!_l__",
    "...............*====",
] + [VAZIO] * 7
SOLIDOS = "#-|+=!*><^vst"
LAJE = "_-|+DJ=!*><^vbcrklgm"
COLUNAS_C, COLUNAS_R = (8, 11, 14, 17), (6, 9, 12, 15, 18, 21)          # a malha de colunas do prédio: de 7,5 em 7,5 m
P = Planta(TERREO, 2.5, 'giro', setas=True)
P1, P2 = Planta(PISO1, 2.5, 'giro', setas=True), Planta(PISO2, 2.5, 'giro', setas=True)
X, Z = P.X, P.Z
ANDAR = 4.0          # de um piso ao outro: 3,8 m de parede e 20 cm de laje
MURO = 3.8           # o muro do canteiro (era 16 m, com a empena dos vizinhos por cima; Jhon, 20/11: "um muro baixo, de obra mesmo")

CAIXA = ('Box_', 'CableReel', 'Cement', 'Generator', 'WC_', 'Pallet', 'Wall', 'TowerCrane_01', 'muro', 'parede', 'laje')
SEM = ('Brick', 'Brush', 'Dye', 'Hammer', 'Sign', 'Door_', 'ConstructionStairs', 'ConstructionMat_01', 'MetalTile', 'WoodenPlank', 'WallDecor', 'Road_', 'House_', 'Light_', 'Pipe', 'TowerCrane_01_2', 'TowerCrane_02_2', 'chao')


def colisao(nome):
    return 'caixa' if nome.startswith(CAIXA) else None if nome.startswith(SEM) else 'malha'


def colunas(M, Pk, y, acima):
    """As colunas do piso, na malha do prédio: inteiras (3,8 m) onde há laje por cima; onde o piso de cima ainda não foi
    feito, o toco com a ferragem de espera. Não vai coluna no vão de porta nem na escada."""
    pontos = [(r, c, None) for r in COLUNAS_R for c in COLUNAS_C + tuple(Pk.C - 1 - c for c in COLUNAS_C)] + [(r, Pk.C // 2 - 1, 0.0) for r in COLUNAS_R]
    for r, c, no_eixo in pontos:
        if Pk.letra(r, c) in 'DJ<>^v' or (y > 0 and Pk.letra(r, c) not in LAJE): continue
        x, z = Pk.xz(r, c); x = x if no_eixo is None else no_eixo
        coberta = acima is not None and acima.letra(r, c) in LAJE
        if y == 0 and not coberta: continue
        nome = 'WallCorner_01' if coberta else Pk.sorteio(x, z, ('WallCorner_03', 'WallCorner_02', 'WallCorner_03'), 4)
        M.por(nome, x, z, y=y, colide='caixa' if coberta else 'malha', planta=y == 0)          # (o toco com a ferragem: a da malha — a caixa pegava o ferro e segurava tiro; 20/11)


def objetos(M, Pk, y):
    """O material espalhado num piso (as letras da planta dele)."""
    for L, r, c, x, z in Pk.celulas('stbrcgwmkl'):
        nx, nz = Pk.corrida(r, c, SOLIDOS); ao_x = nx >= nz; g = Pk.desvio(x, z, 25)
        if L == 's':                    # o andaime, encostado na parede do prédio (a que estiver mais perto, ao norte ou ao sul)
            perto = 1 if Pk.letra(r + 1, c) in '-+DJ' else -1
            for dx in (-3.2, 0.0, 3.2): M.por('Scaffolding_01', x + dx, z + perto * 0.45, colide='malha')
            M.por('WoodenPlank_01', x - 3.0, z + perto * 0.4, y=2.52, yaw=90.0 + g); M.por('Dye_01', x + 0.4, z + perto * 0.5, y=2.5); M.por('Brush_01', x + 0.8, z + perto * 0.4, y=2.5)
        elif L == 't':
            M.por('TowerCrane_01', x, z, colide='malha')          # (a treliça: o tiro passa pelo vão; 20/11)
            M.por(Pk.sorteio(x, z, ('TowerCrane_01_2', 'TowerCrane_02_2')), x, z, y=16.0, yaw=Pk.desvio(x, z, 40) + (25 if x < 0 else 205), pivo=True, colide=None, planta=False)
            for dx, dz in ((1.9, 1.7), (-1.9, 1.8), (1.8, -1.9)): M.por('Sign_01', x + dx, z + dz)
        elif L == 'b':
            M.por('Pallet_01', x, z, y=y, yaw=g); M.por('Box_01', x - 0.1, z, y=y + 0.15, yaw=g); M.por('Box_01', x + 1.1, z + 0.3, y=y, yaw=g + 30); M.por('Box_01', x + 0.1, z - 1.0, y=y, yaw=g - 15)
            M.por('Hammer_01', x + 1.1, z + 0.3, y=y + 0.9, yaw=g)
        elif L == 'r':
            M.por('CableReel_01', x, z, y=y, yaw=g); M.por('CableReel_02', x + 1.9, z + 0.4, y=y, yaw=g + 50)
        elif L == 'c':
            for i, (dx, dz) in enumerate(((0, 0), (0, 0.8), (1.7, 0.2))): M.por(('CementC1_01', 'CementC2_01')[i % 2], x + dx, z + dz, y=y, yaw=g)
            M.por('CementC1_01', x, z + 0.4, y=y + 0.7, yaw=g); M.por('CementC1_02', x - 1.0, z - 0.4, y=y, yaw=g + 40); M.por('CementC2_03', x - 1.2, z + 0.5, y=y, yaw=g - 30)
        elif L == 'g':
            M.por('Generator_01', x, z, y=y, yaw=g); M.por('Light_01', x + 1.4, z + 1.0, y=y, colide=None)
        elif L == 'w':
            M.por('WC_01', x, z, y=y, yaw=90.0 if x < 0 else -90.0)
        elif L == 'm':
            M.por('ConstructionMat_02', x, z, y=y, yaw=90.0 + g, colide='caixa'); M.por('ConstructionMat_01', x + 0.9, z, y=y, yaw=90.0 + g); M.por('ConstructionMat_01', x + 1.3, z + 0.2, y=y, yaw=84.0 + g)
        elif L == 'k':
            M.por('Wall_09', x, z, y=y, yaw=g + (0.0 if ao_x else 90.0), colide='caixa')
            for i, (dx, dz) in enumerate(((0.9, 0.6), (1.2, 0.4), (1.0, 0.9), (-1.0, 0.7), (-1.3, 0.6))): M.por(('Brick_01', 'Brick_02', 'Brick_03')[i % 3], x + dx, z + dz, y=y, yaw=40.0 * i)
        elif L == 'l':                  # o refletor de obra, com a lata e o cabo ao pé
            M.por('Light_02', x, z, y=y, yaw=g * 3, colide='caixa'); M.por('Dye_02', x + 0.7, z + 0.3, y=y); M.por('Sign_01', x - 0.7, z - 0.4, y=y)


def miudezas(M, Pk, y, sal):
    """O que fica largado ao pé das paredes: cones, tábuas, chapas, tijolos, latas de tinta, escadas de mão, placas."""
    for r, c, x, z, yaw, dist in Pk.beiras('#' if y == 0 else '', '-|+' if y == 0 else '-|+=!*', onde='.' if y == 0 else '_'):
        M.grupo()
        s_ = Pk.sorte(x, z, sal + int(yaw)); g = Pk.desvio(x, z, 30, sal)
        if s_ < 0.06:
            M.encostar('Sign_01', x, z, yaw, dist, y=y, folga=0.3); M.encostar('Sign_01', x, z, yaw, dist, y=y, folga=0.5, lado=0.6)
        elif s_ < 0.12:
            for i in range(3): M.encostar(('WoodenPlank_01', 'WoodenPlank_03', 'WoodenPlank_05')[i], x, z, yaw, dist, y=y + 0.06 * i, folga=0.25 + 0.2 * i, lado=0.1 * i)
        elif s_ < 0.16:
            M.encostar('MetalTile_01', x, z, yaw, dist, y=y, folga=0.2); M.encostar('MetalTile_02', x, z, yaw, dist, y=y + 0.07, folga=0.3, lado=0.2)
        elif s_ < 0.21:
            for i in range(4): M.encostar(('Brick_01', 'Brick_02', 'Brick_03', 'Brick_01')[i], x, z, yaw, dist, y=y + (0.2 if i == 3 else 0), folga=0.2 + 0.1 * (i % 2), lado=-0.6 + 0.55 * (i % 3))
        elif s_ < 0.25:
            M.encostar('Dye_01', x, z, yaw, dist, y=y, folga=0.2); M.encostar('Dye_03', x, z, yaw, dist, y=y, folga=0.25, lado=0.45); M.encostar('Brush_02', x, z, yaw, dist, y=y, folga=0.6, lado=0.2)
        elif s_ < 0.29:
            M.encostar(Pk.sorteio(x, z, ('SignA_01', 'SignB_01', 'SignA_02', 'SignB_02'), sal), x, z, yaw, dist, y=y + 1.7, folga=0.02)
        elif s_ < 0.33:
            M.encostar('ConstructionStairs_01', x, z, yaw, dist, y=y, folga=0.05)
        elif s_ < 0.36:
            M.encostar('ConstructionMat_01', x, z, yaw + 90.0, dist, y=y, folga=0.6)
        elif s_ < 0.385:
            M.encostar('Door_01', x, z, yaw, dist, y=y, folga=0.06)
    M.grupo(fim=True)


# ---------------------------------------------------------------- o chão do canteiro (Jhon, 20/11: "poderia ter algumas
# poças de água, morros de areia e etc.; madeira espalhada no chão — barro, ao redor da construção central")
def _disco(K, nome, rgb, semente, lados=18, irregular=0.3):
    """Uma mancha deitada no chão (raio 1 m, de beira torta): a lama, a poça."""
    (u, v), _ = K.uv_da_cor(rgb); r = np.random.default_rng(semente)
    ang = np.linspace(0, 2 * np.pi, lados, endpoint=False); f = r.uniform(0, 6.28, 3)
    raio = 1 + irregular * (0.6 * np.sin(2 * ang + f[0]) + 0.3 * np.sin(3 * ang + f[1]) + 0.25 * np.sin(5 * ang + f[2]))
    b = np.stack([np.cos(ang) * raio, np.zeros(lados), np.sin(ang) * raio], 1)
    pos = np.array([[np.zeros(3), b[(i + 1) % lados], b[i]] for i in range(lados)])
    vira = np.cross(pos[:, 1] - pos[:, 0], pos[:, 2] - pos[:, 0])[:, 1] < 0; pos[vira] = pos[vira][:, [0, 2, 1]]
    K.pecas[nome] = Peca(nome, pos, np.tile([0.0, 1.0, 0.0], (lados, 3, 1)), np.tile([u, v], (lados, 3, 1)).astype(float))


def _monte(K, nome, rgb, semente, lados=14):
    """Um monte de material despejado (raio 1 m, 1 m de alto, facetado como as peças do kit): areia, brita."""
    (u, v), _ = K.uv_da_cor(rgb); r = np.random.default_rng(semente)
    aneis = [(1.0, 0.0), (0.80, 0.30), (0.56, 0.62), (0.30, 0.86), (0.10, 0.98)]
    ang = np.linspace(0, 2 * np.pi, lados, endpoint=False); f = r.uniform(0, 6.28, 2)
    borda = 1 + 0.16 * np.sin(2 * ang + f[0]) + 0.10 * np.sin(3 * ang + f[1])
    V = np.array([[(np.cos(a) * rr * borda[i] * (1 + 0.07 * r.standard_normal()), hh * (1 + 0.06 * r.standard_normal() * (k > 0)), np.sin(a) * rr * borda[i] * (1 + 0.07 * r.standard_normal()))
                   for i, a in enumerate(ang + 0.22 * k)] for k, (rr, hh) in enumerate(aneis)])
    topo = np.array([0.03, 1.0, -0.02]); tris = []
    for k in range(len(aneis) - 1):
        for i in range(lados):
            j = (i + 1) % lados; tris += [[V[k, i], V[k + 1, i], V[k, j]], [V[k, j], V[k + 1, i], V[k + 1, j]]]
    for i in range(lados): tris.append([V[-1, i], topo, V[-1, (i + 1) % lados]])
    pos = np.array(tris); n = np.cross(pos[:, 1] - pos[:, 0], pos[:, 2] - pos[:, 0])
    vira = (n * pos.mean(1) * np.array([1.0, 0.0, 1.0])).sum(1) + n[:, 1] * 0.5 < 0; pos[vira] = pos[vira][:, [0, 2, 1]]
    n = np.cross(pos[:, 1] - pos[:, 0], pos[:, 2] - pos[:, 0]); n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-9)
    K.pecas[nome] = Peca(nome, pos, np.repeat(n[:, None, :], 3, 1), np.tile([u, v], (len(pos), 3, 1)).astype(float))


def canteiro(M):
    """O chão do canteiro: os montes de areia e de brita (as letras ``a`` e ``p``; sobe-se neles, e dão cobertura), o
    barro pisado em volta do prédio com as tábuas jogadas por cima para passar, e as poças — no barro e no pátio."""
    K = M.kit
    for k in range(3):
        _disco(K, f'lama_{k}', (0.30, 0.22, 0.15), 40 + k, irregular=0.34); _disco(K, f'poca_{k}', (0.27, 0.33, 0.38), 50 + k, irregular=0.2)
    for k in range(2):
        _monte(K, f'monte_areia_{k}', (0.80, 0.67, 0.44), 60 + k); _monte(K, f'monte_brita_{k}', (0.55, 0.55, 0.57), 70 + k)
    for L, r, c, x, z in P.celulas('ap'):
        s_ = P.sorte(x, z, 61); raio = 2.0 + 0.7 * s_; alto = 1.25 + 0.5 * P.sorte(x, z, 62)
        M.por(('monte_areia_' if L == 'a' else 'monte_brita_') + str(int(s_ * 2) % 2), x, z, yaw=P.desvio(x, z, 180, 63), escala=(raio, alto, raio * (0.85 + 0.3 * P.sorte(x, z, 64))), colide='malha')
        # o que escorreu do monte, e a pá de quem estava mexendo nele
        M.por('lama_0', x + 0.4, z + 0.3, y=0.012, yaw=P.desvio(x, z, 180, 65), escala=(raio * 1.35, 1.0, raio * 1.25), colide=None, planta=False)

    x0, z0, _, _ = P.mundo(8, 6, 9, 7); bx, bz = -(x0 + 1.25), -(z0 + 1.25)                # meia largura e meio fundo do prédio
    def dos_dois(nome, x, z, yaw, **k):
        M.por(nome, x, z, yaw=yaw, **k); M.por(nome, -x, -z, yaw=yaw + 180.0, **k)
    # ---- o barro em volta do prédio: de 4,5 em 4,5 m ao longo das quatro paredes (só meia volta: a outra é o giro)
    pontos = [(x, -bz, 0.0, -1.0) for x in np.arange(-bx - 2.0, bx + 2.1, 4.5)] + [(-bx, z, -1.0, 0.0) for z in np.arange(-bz + 3.0, bz - 2.9, 4.5)]
    for k, (px, pz, nx, nz) in enumerate(pontos):
        s_ = P.sorte(px, pz, 81)
        if s_ > 0.82: continue
        d = 1.9 + 2.2 * P.sorte(px, pz, 82); x, z = px + nx * d + P.desvio(px, pz, 0.9, 83) * abs(nz), pz + nz * d + P.desvio(px, pz, 0.9, 84) * abs(nx)
        ao_longo = 0.0 if nz else 90.0; g = P.desvio(px, pz, 25, 85)
        larg, fundo = 2.2 + 1.4 * P.sorte(px, pz, 86), 1.3 + 0.9 * P.sorte(px, pz, 87)
        dos_dois(f'lama_{k % 3}', x, z, ao_longo + g, y=0.014, escala=(larg, 1.0, fundo), colide=None, planta=False)
        if s_ < 0.36:                                                    # a água empoçada no barro
            dos_dois(f'poca_{k % 3}', x + 0.3 * nz, z + 0.3 * nx, ao_longo - g, y=0.026, escala=(larg * 0.55, 1.0, fundo * 0.6), colide=None, planta=False)
        n_tabuas = 1 + int(P.sorte(px, pz, 88) * 3.4)                    # as tábuas jogadas por cima, para passar
        for i in range(n_tabuas):
            t = (i - (n_tabuas - 1) / 2) * 0.55
            dos_dois(('WoodenPlank_01', 'WoodenPlank_03', 'WoodenPlank_05')[(k + i) % 3], x + t * abs(nx) + 0.25 * i * abs(nz), z + t * abs(nz) + 0.25 * i * abs(nx),
                     ao_longo + 90.0 + P.desvio(px + i, pz, 28, 89), y=0.03 + 0.012 * i, colide=None, planta=False)
    # ---- as poças do pátio (onde a chuva ficou) e madeira largada
    for L, r, c, x, z in P.celulas('.'):
        if x > 0 or (abs(x) < bx + 1.5 and abs(z) < bz + 1.5): continue
        s_ = P.sorte(x, z, 91)
        if s_ < 0.07:
            g = P.desvio(x, z, 180, 92); raio = 0.9 + 1.5 * P.sorte(x, z, 93)
            dos_dois(f'lama_{int(s_ * 100) % 3}', x, z, g, y=0.014, escala=(raio * 1.5, 1.0, raio * 1.15), colide=None, planta=False)
            dos_dois(f'poca_{int(s_ * 300) % 3}', x + 0.1, z - 0.1, g + 40.0, y=0.026, escala=(raio, 1.0, raio * 0.7), colide=None, planta=False)
        elif s_ < 0.11:
            g = P.desvio(x, z, 180, 94)
            for i in range(2 + int(P.sorte(x, z, 95) * 3)):
                dos_dois(('WoodenPlank_03', 'WoodenPlank_01', 'WoodenPlank_05')[i % 3], x + 0.35 * i, z + 0.2 * (i % 2), g + 17.0 * i, y=0.03 + 0.03 * i, colide=None, planta=False)


def piso(M, Pk, y, paredes):
    """Um piso do prédio: a laje (de y − 20 cm a y), as paredes, os vãos, as muretas e as escadas que sobem dele."""
    if y > 0:
        for c0, r0, c1, r1 in Pk.blocos(LAJE):
            M.chapa('laje', *Pk.mundo(c0, r0, c1, r1), y=y - 0.2, alto=0.2, colide='caixa', planta=False)
    muros, vaos, cantos = Pk.paredes('-', '|', '+', 'DJ', sobra=0.1)
    for xa, za, xb, zb in muros:
        M.muro(paredes, xa, za, xb, zb, 5.0, y=y, semente=int(abs(xa * 2 + za)), colide='caixa', planta=y == 0)
    for L, x, z, ao_x in vaos:
        M.verga('parede', x, z, ao_x, 2.5, y + 2.6, y + 3.8, grosso=0.22, colide='caixa', planta=y == 0)
        if L == 'J': M.verga('parede', x, z, ao_x, 2.5, y, y + 1.0, grosso=0.22, colide='caixa', planta=y == 0)
    for xa, za, xb, zb in Pk.paredes('=', '!', '*+|-', '')[0]:
        # (colisão da própria peça: a mureta quebrada tem um vão de 1,4 m no meio, e por ele se passa — com a caixa
        # inteira ninguém atravessava, Jhon, 20/11; de cada quatro módulos, três são da quebrada)
        M.muro(('Wall_08', 'Wall_08', 'Wall_07', 'Wall_08'), xa, za, xb, zb, 5.0, y=y, semente=int(abs(xa + za * 3)), colide='malha', planta=y == 0)
    for L, c0, r0, c1, r1 in Pk.regioes('<>^v'):
        # (o último degrau do kit fica a 3,8 m: a escada é esticada para ele chegar à laje, a 4 m)
        M.rampa(M.encaixar('Stairs_01', *Pk.mundo(c0, r0, c1, r1), yaw={'>': 0.0, '<': 180.0, '^': 90.0, 'v': -90.0}[L], y=y, alto=M.kit['Stairs_01'].tam[1] * ANDAR / 3.8,
                           colide='malha', planta=y == 0), (-0.452, 0.0), (5.28, 3.8), (-1.19, 1.19), ate=y + ANDAR)


def desenho():
    M = Montagem('fps6', colisao); K = M.kit
    K.chapa('parede', M.cor_dos_lados('Wall_01')); K.chapa('muro', M.cor_dos_lados('Wall_06')); K.chapa('laje', M.cor(K['Floor_01']), fundo=True)
    K.chapa('chao', (0.50, 0.50, 0.52))

    # ---- o muro do canteiro: maciço de 6,2 m, de tijolo na face (uma fiada inteira e outra mais baixa por cima) e uma
    # cinta de concreto no topo. Por cima dele só a barreira invisível; atrás, os prédios vizinhos
    for reg in P.regioes('#'):
        M.chapa('muro', *P.mundo(*reg[1:]), alto=MURO, colide='caixa', planta=True)
    # (muro de obra: o pano de tijolo, um pilar de concreto a cada 5 m e a capa de concreto por cima. Por cima dele o
    # limite é invisível — ``barrar`` —, e o lobo não corre em parede invisível: cai)
    import math as _m
    for xa, za, xb, zb, yaw in P.contorno('#'):
        M.muro('Wall_06', xa, za, xb, zb, 5.0, frente=yaw, recuo=0.08, colide=None, planta=False)
        n = max(1, int(round(_m.hypot(xb - xa, zb - za) / 5.0))); fx, fz = _m.sin(_m.radians(yaw)), _m.cos(_m.radians(yaw))
        for i in range(n + 1):
            M.por('WallCorner_01', xa + (xb - xa) * i / n + fx * 0.12, za + (zb - za) * i / n + fz * 0.12, escala=(1.0, (MURO + 0.22) / 3.8, 1.0), colide=None, planta=False)
    for reg in P.regioes('#'):
        x0, z0, x1, z1 = P.mundo(*reg[1:]); M.chapa('laje', min(x0, x1) - 0.16, min(z0, z1) - 0.16, max(x0, x1) + 0.16, max(z0, z1) + 0.16, y=MURO, alto=0.16, colide=None, planta=False)

    # ---- o prédio: o contrapiso, o térreo e as duas lajes
    x0, z0, _, _ = P.mundo(8, 6, 9, 7); M.chapa('chao', x0 + 1.25, z0 + 1.25, -x0 - 1.25, -z0 - 1.25, y=0.0, alto=0.03)
    piso(M, P, 0.0, ('Wall_01', 'Wall_02', 'Wall_03'))
    piso(M, P1, ANDAR, ('Wall_06', 'Wall_01', 'Wall_06'))
    piso(M, P2, 2 * ANDAR, ('Wall_01',))
    for k, (Pk, acima) in enumerate(((P, P1), (P1, P2), (P2, None))):
        colunas(M, Pk, k * ANDAR, acima); objetos(M, Pk, k * ANDAR); miudezas(M, Pk, k * ANDAR, 10 * k)
    canteiro(M)

    # ---- a cidade em volta (fora do mapa), sem colisão
    M.arredores(('House_02', 'House_04', 'House_03', 'House_04', 'House_02'), X, Z, folga=8.0, fileiras=2, passo=27.0)
    return M


def mato():
    """O mato do canteiro (o kit da obra não tem planta: estas são as do kit do Escritório, num desenho à parte, sem
    colisão): capim ao pé do muro e moitas nos cantos."""
    V = Montagem('fps3', lambda nome: None)
    for r, c, x, z, yaw, dist in P.beiras('#'):
        s_ = P.sorte(x, z, 77 + int(yaw))
        if s_ < 0.45:
            for i in range(2 + int(s_ * 10) % 3): V.encostar(('Grass_01', 'Grass_02')[i % 2], x, z, yaw, dist, folga=0.1 + 0.25 * (i % 2), lado=-0.9 + 0.6 * i, escala=(1.0, 1.3, 1.6)[int(P.sorte(x, z, i) * 3)])
        if 0.45 < s_ < 0.53: V.encostar('Plant_01', x, z, yaw, dist, folga=0.1, escala=0.8)
        elif 0.53 < s_ < 0.57: V.encostar(P.sorteio(x, z, ('Stump_01', 'Log_01', 'Rock_02', 'Mushroom_01')), x, z, yaw, dist, folga=0.3)
    return V


def montar():
    m = Mapa("obra", "Obra", int(2 * X), int(2 * Z))
    m.material("terra", "cascalho", (0.62, 0.54, 0.44), 2.4)
    m.caixa("Chao", "Terra", (0, -0.25, 0), (400, 0.5, 400), "terra", colide=False)
    M = desenho()
    M.aplicar(m, "Obra", "obra")
    mato().aplicar(m, "Obra", "obra_mato", no_planta='Mato', modelo='Mato')
    fechar(m, X, Z, altura=22.0)
    barrar(m, P, '#', 22.0)
    m.panorama('cidade', X + 230.0, altura=170.0, base=-6.0)

    g = Grade(M.ocupacao((-X, -Z), (X, Z)), (-X, -Z), 0.25, folga=0.55)
    a = nascer(m, g, (-43.5, 0.0), AZUL)[0]
    rotas = dict(
        patioNorte=P.rota(a, '12', '65'), corredor=P.rota(a, '34'), patioSul=P.rota(a, '56', '21'),
        salas=P.rota(a, '79', '34'), porDentro=P.rota(a, '38', '97'), cruzadaNorte=P.rota(a, '12'), cruzadaSul=P.rota(a, '56'),
    )
    m._grade = g
    espalhar(m, g, a, quantos=16)          # o mata-mata: todos contra todos, cada um num canto do canteiro
    for nome, pontos in rotas.items():
        m.rota(nome, [(x, 0.5, z) for x, z in g.rota(pontos)])

    # ---- a luz, trabalhada como a do Armazém (pedido do Jhon, 20/11: "trabalhe na iluminação igual você fez no
    # armazém"; os personagens ficavam com a aparência inconsistente — pretos na sombra do muro, estourados no sol).
    # Sol quente e forte contra ambiente frio, ACES com branco 6, oclusão larga e um preenchimento frio, sem sombra, do
    # lado oposto ao sol — é ele que dá volume a quem está na sombra e debaixo da laje. Sem luz rebatida (SDFGI): testada
    # em foto Forward+, ela apagava o céu debaixo da laje e o personagem ficava preto ao lado da parede clara.
    k = 0
    for x in (-24, -12, 0, 12, 24):
        for z in (-12.5, 0.0, 12.5):
            k += 1; m.luz("Luzes", f"Terreo{k}", (float(x), 3.3, z), (1.0, 0.84, 0.6), 2.6, 12)
    luz_de_jogo(m, 'dia', 52, 44)
    lampadas(m, [M], maximo=16)
    return m
