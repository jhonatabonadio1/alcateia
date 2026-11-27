"""
O acabamento do Setor Zero: o lugar. Tudo o que `setor_zero.montar()` não
precisa para o jogo funcionar — a arquitetura (pórticos, arcos, a cúpula,
a cobertura das cabines, as torres), a vegetação, a mobília, os
letreiros, as áreas de apoio do campus e a montanha: a rocha do cume, a
beira, o mar de nuvens e os picos gelados em volta.

Quase tudo aqui é **sem colisão**: as áreas de apoio ficam fora do
percurso (o tutorial anda dentro de paredes, e por cima delas há
barreira), então árvore, banco e quiosque são paisagem — vistos pelas
janelas da Sala de Ativação e do corredor, da beira da Linha de Fogo, da
galeria da arena e do terraço da Rua Ignição. Só colide o que está
dentro do percurso (armários e bancos das salas, as rochas e os
pinheirinhos da arena).

As peças são dos kits que já estão no jogo (docs/PACOTES.md): o
"Low Poly Sci-Fi City" (o do Distrito Neon: a torre de controle com as
antenas, quiosques, postes, bancos, jardineiras) e, num segundo desenho
(`setor_zero_mato.glb`), o "Low Poly FPS Map Vol.3" (pinheiro, árvores,
rochas). O que não é peça de kit é forma própria (setor_zero_formas.py).
O que falta está em docs/MAPAS.md, "Peças pendentes do Setor Zero".

Tudo o que se lê no mapa é em inglês (Jhon, 26/11).
As coordenadas são as da planta (x para a direita, y para baixo).
"""
import math

from .kits.montagem import Montagem
from . import setor_zero as SZ
from .setor_zero import P, bloco, girado, tira, sorte, invisivel
from .setor_zero_formas import W, Formas

SUL, LESTE, NORTE, OESTE = 0.0, 90.0, 180.0, -90.0          # para onde a frente da peça (ou do letreiro) olha
# As cores dos blocos do treino (as lâmpadas dos letreiros).
CIANO, AMBAR, VIOLETA, AZUL, LARANJA, VERDE = (0.13, 0.83, 0.93), (0.96, 0.62, 0.04), (0.65, 0.55, 0.98), (0.22, 0.74, 0.97), (0.98, 0.45, 0.09), (0.20, 0.83, 0.60)
ESCURO = "sz_metal_pintado_escuro"


def por(Mx, nome, x, y, yaw=0.0, base=0.0, colide=None, escala=1.0, planta=True):
    X, Z = P(x, y)
    return Mx.por(nome, X, Z, yaw=yaw, y=base, colide=colide, escala=escala, planta=planta)


# ------------------------------------------------------------------ letreiros

def placa(m, nome, imagem, x, y, altura, largura, olha, aceso=0.35, luz=None):
    """Um letreiro na parede: (x, y) em cima da face, `olha` para onde a frente dele dá. `luz`: a cor da lâmpada que
    ilumina o chão à frente (a do bloco do treino)."""
    X, Z = P(x, y)
    m.letreiro("Letreiros", nome, "setor_zero_" + imagem, (X, altura, Z), largura, olha, aceso=aceso)
    if luz:
        a = math.radians(olha)
        m.luz("Luzes", "Luz" + nome, (X + math.sin(a) * 1.6, altura - 0.6, Z + math.cos(a) * 1.6), luz, 1.6, 9.0)


def pintura(m, nome, imagem, x, y, largura, le="N", aceso=0.0, nivel=0.0):
    """Um letreiro pintado no piso (no `nivel` dele). `le`: para onde olha quem o lê de pé (N, S, L, O na planta)."""
    X, Z = P(x, y)
    m.letreiro("Letreiros", nome, "setor_zero_" + imagem, (X, nivel + 0.012, Z), largura, {"N": 0.0, "L": -90.0, "S": 180.0, "O": 90.0}[le], aceso=aceso, chao=True)


def totem(M, m, nome, imagem, x, y, olha, largura=4.0, luz=None):
    """Uma placa de setor em pé, fora do percurso: dois montantes e a chapa, com o letreiro na frente."""
    a = math.radians(olha); fx, fy = math.sin(a), math.cos(a)          # a frente, na planta
    lx, ly = fy, -fx                                                    # ao longo da chapa
    for s in (-1, 1):
        girado(M, ESCURO, x + lx * s * (largura / 2 - 0.1), y + ly * s * (largura / 2 - 0.1), 0.14, 0.14, 0.0, 3.1, 0.0, colide=None, planta=False)
    girado(M, ESCURO, x, y, largura + 0.3, 0.12, 1.9, 3.1, math.degrees(math.atan2(ly, lx)), colide=None, planta=False)
    placa(m, nome, imagem, x + fx * 0.062, y + fy * 0.062, 2.5, largura, olha, luz=luz)


# ------------------------------------------------------------------ vegetação e mobília

def arvore(M, V, x, y, r=2.2, tipo=None, colide=False):
    """Uma árvore de copa de `r` m de raio. Três modelos, repetidos (custo baixo): a do kit sci-fi e duas do kit 3."""
    tipo = tipo or ("scifi", "copa", "galhos")[int(sorte("arvore", x, y) * 3)]
    r = min((1.3, 1.5, 2.1, 2.5), key=lambda v: abs(v - r))          # quatro tamanhos: cada tamanho é uma malha no .glb
    yaw = sorte("giro", x, y) * 360
    if tipo == "scifi": por(M, "SM_Nature_Tree_01", x, y, yaw, escala=r / 2.6, planta=False)
    elif tipo == "copa": por(V, "Tree_04", x, y, yaw, escala=r / 2.2, planta=False)
    else: por(V, "Tree_03", x, y, yaw, escala=r / 3.0, planta=False)
    if colide:
        X, Z = P(x, y); M.barra(X, Z, largura=0.4, altura=3.0)


def pinheiro(V, M, x, y, altura, colide=False):
    altura = min((6.0, 12.0, 18.0, 21.5, 25.0), key=lambda v: abs(v - altura))          # poucos tamanhos (veja `arvore`)
    por(V, "Tree_01", x, y, sorte("pinho", x, y) * 360, escala=altura / 7.4, planta=False)
    if colide:
        X, Z = P(x, y); M.barra(X, Z, largura=0.35, altura=min(altura, 4.0))


def rocha(V, x, y, r=1.2, colide=None, base=0.0):
    nome = ("Rock_01", "Rock_02", "Rock_03")[int(sorte("rocha", x, y) * 3)]
    lado = {"Rock_01": 1.1, "Rock_02": 1.0, "Rock_03": 1.5}[nome]
    r = min((0.9, 1.2, 1.5, 3.0, 4.5), key=lambda v: abs(v - r))
    por(V, nome, x, y, sorte("gr", x, y) * 360, base=base, escala=2 * r / lado * (0.55 if nome == "Rock_03" else 1.0), colide=colide, planta=bool(colide))


def sebe(M, x, y, w, h, alto=0.9):
    bloco(M, "sz_folhagem_escura", x, y, x + w, y + h, 0.0, alto, colide=None, planta=False)


def banco(M, x, y, ao_x=True, olha=None, colide=None):
    """Um banco de 2,4 m no retângulo da planta (3 × 1 m com o canto em (x, y))."""
    cx, cy = (x + 1.5, y + 0.5) if ao_x else (x + 0.5, y + 1.5)
    yaw = olha if olha is not None else (SUL if ao_x else LESTE)
    por(M, ("SM_Props_Bench_01", "SM_Props_Bench_02")[int(sorte("banco", x, y) * 2)], cx, cy, yaw, escala=1.5, colide=colide)


def poste(M, x, y, olha=SUL, base=0.0):
    por(M, "SM_Floor_Light_01", x, y, olha + 90.0, base=base, planta=False)


def jardineira(M, x, y, ao_x=True):
    por(M, "SM_Nature_Flowerbed_04", x, y, SUL if ao_x else LESTE, planta=False)


def caminho(M, pontos, largura=2.2, mat="sz_brita"):
    for (x0, y0), (x1, y1) in zip(pontos, pontos[1:]):
        comp = math.hypot(x1 - x0, y1 - y0)
        girado(M, mat, (x0 + x1) / 2, (y0 + y1) / 2, comp + largura * 0.5, largura, 0.0, 0.025, math.degrees(math.atan2(y1 - y0, x1 - x0)), colide=None, planta=False)


def torre(F, x, y, altura, led, base=0.0, raio=1.1, flamula=None, para=(1.0, 0.0)):
    """Uma torre-marco: um fuste de oito lados que afina, o capitel, um anel de LED e a agulha — e a flâmula."""
    F.cilindro("sz_concreto", x, y, raio, base, altura * 0.86, 8, r_topo=raio * 0.62, giro=math.pi / 8)
    F.cilindro(ESCURO, x, y, raio * 1.18, base, base + 0.7, 8, r_topo=raio * 1.02, giro=math.pi / 8)
    F.cilindro(ESCURO, x, y, raio * 0.66, altura * 0.86, altura * 0.9, 8, r_topo=raio * 0.9, giro=math.pi / 8)
    F.cilindro(led, x, y, raio * 0.82, altura * 0.9, altura * 0.93, 8, giro=math.pi / 8)
    F.cilindro(ESCURO, x, y, raio * 0.9, altura * 0.93, altura, 8, r_topo=raio * 0.25, giro=math.pi / 8)
    F.viga(ESCURO, W(x, y, altura), W(x, y, altura + 3.2), 0.1)
    if flamula:
        F.flamula(ESCURO, flamula, x, y, altura + 0.4, 2.8, para)


def arco_de_luz(F, x, y0, y1, pe, flecha, led, grosso=0.26):
    """Um arco atravessado sobre um corredor que corre ao longo de x: dos dois pés (em y0 e y1, à altura `pe`) ao alto
    (`pe + flecha`), com uma linha de LED por baixo."""
    n = 12; meio = (y0 + y1) / 2; meia = (y1 - y0) / 2
    pts = [W(x, meio + meia * math.cos(math.pi * k / n), pe + flecha * math.sin(math.pi * k / n)) for k in range(n + 1)]
    F.tubo(ESCURO, pts, grosso)
    F.tubo(led, [p + [0.0, -grosso / 2 - 0.03, 0.0] for p in pts[1:-1]], 0.07)


# ------------------------------------------------------------------ o percurso

def percurso(m, M, V, F):
    # ---- Sala de Ativação: armários na parede oeste, bancos, vasos, o nome da sala e o do campo sobre a porta
    for k in range(12):
        por(M, "SM_Props_Furniture_04", 5.1, 41.75 + k * 1.5, LESTE, colide="caixa", escala=(1.4, 1.0, 1.0))
    banco(M, 8.0, 60.3, olha=NORTE, colide="caixa"); banco(M, 14.0, 60.3, olha=NORTE, colide="caixa")
    for x, y in ((5.3, 39.4), (18.7, 39.4), (18.7, 60.7)):
        por(M, "SM_Nature_Flowerbed_02", x, y, sorte("vaso", x) * 360, escala=1.3, colide="caixa")
    placa(m, "SalaDeAtivacao", "sala_de_ativacao", 4.4, 50.0, 3.7, 6.0, LESTE, luz=CIANO)
    placa(m, "SetorZero", "setor_zero", 19.6, 50.0, 4.5, 3.6, OESTE, aceso=0.5)
    tira(M, "sz_led_ciano", 4.4, 38.4, 4.52, 61.6, 4.6, 0.1); tira(M, "sz_led_ciano", 19.48, 38.4, 19.6, 47.0, 4.6, 0.1); tira(M, "sz_led_ciano", 19.48, 53.0, 19.6, 61.6, 4.6, 0.1)
    # o heliponto no teto do Ponto de Retorno (o que se vê da galeria e do terraço): o disco, o anel e um drone pousado
    F.cilindro("sz_asfalto", 17.0, 83.0, 7.4, 5.3, 5.42, 8, giro=math.pi / 8)
    F.anel("sz_faixa_amarela", 17.0, 83.0, 5.4, 5.8, 5.42, 5.44, 10.0)
    pintura(m, "Pad", "pad", 17.0, 83.0, 4.6, "N", nivel=5.44)
    por(M, "SM_Props_Drone_01", 17.0, 79.2, 30.0, base=5.44, escala=2.2, planta=False)

    # ---- Corredor de Calibragem: os arcos de luz (o corredor é aberto em cima), as marcas, os nomes das etapas
    F.portico(ESCURO, "sz_led_ciano", 20.3, 50.0, False, 6.0, 3.7)
    for x in (22.0, 26.3, 30.6, 37.6, 47.2, 51.5, 55.8, 60.1):
        arco_de_luz(F, x, 46.8, 53.2, 4.0, 1.5, "sz_led_ciano")
    placa(m, "CorredorDeCalibragem", "corredor_de_calibragem", 32.8, 47.0, 3.4, 5.6, SUL, luz=CIANO)
    placa(m, "NucleoDaIris", "nucleo_da_iris", 54.5, 47.0, 3.4, 4.0, SUL, luz=CIANO)
    for x in (24.0, 27.0):
        pintura(m, f"Marca{int(x)}", "marca", x, 50.0, 1.4, "L", aceso=0.6)
    pintura(m, "Vao", "vao", SZ.VAO_X0 - 2.0, 50.0, 1.4, "L")
    for y in (47.9, 52.1):
        pintura(m, f"VaoHachura{int(y)}", "hachura", SZ.VAO_X0 - 0.75, y, 0.9, "L")
    pintura(m, "Viga", "viga", SZ.VIGA_X[0] - 2.2, 50.0, 1.8, "L")
    pintura(m, "TrechoSilencioso", "trecho_silencioso", 47.6, 50.0, 3.6, "L")
    for x in (22, 26, 38, 46, 52, 60):                          # jardineiras por fora, ao pé das janelas
        jardineira(M, x + 1.2, 53.95); jardineira(M, x + 1.2, 46.05)

    # ---- Núcleo da Iris: quatro pilones por fora da parede, as nervuras que sobem deles e seguram o anel sobre o
    #      holograma; o holograma (um pilar de luz, sem rosto); os anéis do piso; os pórticos das quatro saídas
    cx, cy, n = 75.0, 50.0, SZ.N_NUCLEO
    for ang in (45, 135, 225, 315):
        a = math.radians(ang); px, py = cx + 14.3 * math.cos(a), cy + 14.3 * math.sin(a)
        F.cilindro("sz_concreto", px, py, 1.15, n, 9.0, 8, r_topo=0.6, giro=a)
        F.cilindro(ESCURO, px, py, 0.7, 9.0, 9.6, 8, r_topo=0.9, giro=a)
        pts = []
        for k in range(13):
            t = k / 12; r_ = (1 - t) ** 2 * 14.3 + 2 * (1 - t) * t * 9.5 + t * t * 4.5; h = (1 - t) ** 2 * 9.4 + 2 * (1 - t) * t * 12.4 + t * t * 7.4
            pts.append(W(cx + r_ * math.cos(a), cy + r_ * math.sin(a), h))
        F.tubo(ESCURO, pts, 0.3, 0.42)
        F.tubo("sz_led_ciano", [p + [0.0, -0.25, 0.0] for p in pts[2:-1]], 0.07)
    F.anel(ESCURO, cx, cy, 4.1, 4.7, 7.15, 7.5, 7.5)
    F.anel("sz_led_ciano", cx, cy, 4.2, 4.6, 7.07, 7.15, 7.5)
    F.anel(ESCURO, cx, cy, 1.5, 1.75, 7.9, 8.1, 15.0)
    for ang in (0, 90, 180, 270):
        a = math.radians(ang)
        F.viga(ESCURO, W(cx + 4.4 * math.cos(a), cy + 4.4 * math.sin(a), 7.4), W(cx + 1.6 * math.cos(a), cy + 1.6 * math.sin(a), 8.0), 0.1)
    F.cilindro("sz_led_ciano", cx, cy, 0.16, n + 1.1, n + 2.5, 8)
    F.cilindro("sz_led_ciano", cx, cy, 0.03, n + 2.6, n + 3.2, 4, r_topo=0.5)
    F.cilindro("sz_led_ciano", cx, cy, 0.5, n + 3.2, n + 4.1, 4, r_topo=0.03)
    F.cilindro("sz_led_branco", cx, cy, 0.05, n + 4.1, n + 7.0, 6, r_topo=0.02)
    X, Z = P(cx, cy)
    m.luz("Luzes", "Holograma", (X, n + 3.2, Z), CIANO, 2.8, 13.0)
    pintura(m, "AnelIris", "anel_iris", cx, cy, 8.4, "N", aceso=0.5, nivel=n)
    pintura(m, "AnelIrisFora", "anel_iris", cx, cy, 16.6, "N", aceso=0.25, nivel=n)
    F.portico(ESCURO, "sz_led_ciano", 62.75, 50.0, False, 6.0, 4.05)
    F.portico(ESCURO, "sz_led_laranja", 75.0, 37.75, True, 6.0, 4.05)
    F.portico(ESCURO, "sz_led_violeta", 87.25, 50.0, False, 6.0, 4.05)
    F.portico(ESCURO, "sz_led_azul", 75.0, 62.25, True, 6.0, 4.05)

    # ---- o salão e a Câmara de Metamorfose: o anel de energia e a cúpula de nervuras (aberta), com o óculo
    for y0 in (47.0, 52.85):
        tira(M, "sz_led_violeta", 87.6, y0, 97.95, y0 + 0.15)
    for x in (90.0, 94.5):
        arco_de_luz(F, x, 46.8, 53.2, 4.5, 1.3, "sz_led_violeta")
    placa(m, "CamaraDeMetamorfose", "camara_de_metamorfose", 93.0, 47.0, 3.4, 6.4, SUL, luz=VIOLETA)
    c2x, c2y, nc = 112.0, 50.0, SZ.N_CAMARA
    pintura(m, "AnelMetamorfose", "anel_metamorfose", c2x, c2y, 6.2, "L", aceso=0.6, nivel=nc)
    def cupula(t):          # um quarto de elipse: da parede (raio 9,25 a 7,5 m) ao óculo (raio 2,3 a 12,2 m)
        return 2.3 + (9.25 - 2.3) * math.cos(t * math.pi / 2), 7.5 + 4.7 * math.sin(t * math.pi / 2)
    for k in range(12):
        a = 2 * math.pi * (k + 0.5) / 12
        pts = [W(c2x + r_ * math.cos(a), c2y + r_ * math.sin(a), h) for r_, h in (cupula(i / 10) for i in range(11))]
        F.tubo(ESCURO, pts, 0.2, 0.3)
    for t, g in ((0.45, 0.14), (0.78, 0.14)):
        r_, h = cupula(t)
        F.anel(ESCURO, c2x, c2y, r_ - g / 2, r_ + g / 2, h - 0.1, h + 0.1, 15.0)
    F.anel(ESCURO, c2x, c2y, 2.1, 2.5, 12.0, 12.4, 15.0)
    F.anel("sz_led_violeta", c2x, c2y, 2.15, 2.45, 11.93, 12.0, 15.0)
    X, Z = P(c2x, c2y)
    m.foco("Luzes", "FocoCamara", (X, 11.6, Z), (0.86, 0.80, 1.0), 2.8, 13.0, 70.0)
    F.portico(ESCURO, "sz_led_laranja", 112.0, 35.9, True, 6.0, 4.3)

    # ---- Linha de Fogo (2,4 m abaixo, aberta para o abismo)
    n = SZ.N_FOGO
    placa(m, "LinhaDeFogo", "linha_de_fogo", 58.5, 34.0, n + 4.4, 9.0, NORTE, luz=AMBAR)
    placa(m, "LinhaDeFogo2", "linha_de_fogo", 91.5, 34.0, n + 4.4, 9.0, NORTE, luz=AMBAR)
    placa(m, "Arsenal", "arsenal", 45.0, 10.8, n + 3.1, 3.4, LESTE, luz=AMBAR)
    placa(m, "PocoDaGranada", "poco_da_granada", 99.0, 10.4, n + 1.5, 4.2, SUL)
    for k in range(8):
        pintura(m, f"FaixaTiro{k + 1}", "faixa_perigo", 53.2 + k * 6.4, 23.2, 6.4, "N", nivel=n)
    pintura(m, "LinhaDeTiro", "linha_de_tiro", 75.0, 20.2, 4.4, "N", nivel=n)
    for n_, x in enumerate((55, 65, 75, 85, 95), 1):
        pintura(m, f"Cabine{n_}", f"cabine_{n_}", x, 26.6, 2.4, "N", nivel=n)
    pintura(m, "Pistola", "pistola", 51.0, 15.4, 3.0, "N", nivel=n); pintura(m, "CorpoACorpo", "corpo_a_corpo", 50.0, 24.3, 2.6, "S", nivel=n)
    bloco(M, "sz_piso_claro", 46.0, 14.0, 56.0, 22.0, n, n + 0.015, colide=None, planta=False)          # a estação da pistola
    bloco(M, "sz_piso_claro", 46.0, 23.5, 54.0, 28.0, n, n + 0.015, colide=None, planta=False)          # o corpo a corpo
    # a cobertura das cabines: uma água inclinada sobre a linha de tiro, nas colunas e nas mãos-francesas
    # (baixa: o alto dela fica abaixo do olho de quem desce a escadaria, que vê o abismo por cima)
    F.tronco(ESCURO, [W(48.0, 20.4, n + 3.0), W(102.0, 20.4, n + 3.0), W(102.0, 26.2, n + 2.55), W(48.0, 26.2, n + 2.55)],
             [W(48.0, 20.4, n + 3.14), W(102.0, 20.4, n + 3.14), W(102.0, 26.2, n + 2.69), W(48.0, 26.2, n + 2.69)])
    F.viga("sz_led_laranja", W(48.0, 20.38, n + 3.02), W(102.0, 20.38, n + 3.02), 0.06, 0.1)
    for x in (50, 60, 70, 80, 90, 100):
        F.caixa("sz_concreto", x - 0.2, 25.3, x + 0.2, 25.7, n, n + 2.62, chanfro=0.07)
        F.viga(ESCURO, W(x, 25.4, n + 1.9), W(x, 21.2, n + 2.92), 0.12, 0.16)
        por(M, "SM_Props_Screen_01", x, 24.7, NORTE, base=n + 1.8, planta=False)          # o painel de cada baia
    # a beira: a faixa de perigo no piso, o corrimão sobre o parapeito, os avisos e os dois mastros com a flâmula
    tira(M, "sz_faixa_amarela", 45.0, -2.7, 105.0, -2.35, n, 0.02)
    for k in range(30):
        F.viga(ESCURO, W(46.0 + k * 2.0, -2.85, n + SZ.PARAPEITO), W(46.0 + k * 2.0, -2.85, n + SZ.PARAPEITO + 0.25), 0.05)
    F.viga(ESCURO, W(45.0, -2.85, n + SZ.PARAPEITO + 0.25), W(105.0, -2.85, n + SZ.PARAPEITO + 0.25), 0.06)
    for k, x in enumerate((56.0, 75.0, 94.0)):
        placa(m, f"PerigoQueda{k + 1}", "perigo_queda", x, -2.7, n + 0.45, 2.6, SUL, aceso=0.0)
    for x, para in ((45.7, (1.0, 0.0)), (104.3, (1.0, 0.0))):
        F.cilindro(ESCURO, x, -2.3, 0.22, n, n + 0.6, 8, r_topo=0.1)
        F.flamula(ESCURO, "sz_tecido_laranja", x, -2.3, n + 0.6, 8.4, para, 3.2)
    for y in (2, 12, 30):                                        # postes ao longo dos muros
        poste(M, 45.9, y, LESTE, base=n); poste(M, 104.1, y, OESTE, base=n)
    tira(M, "sz_led_laranja", 45.0, 33.9, 72.0, 34.0, 3.2, 0.12); tira(M, "sz_led_laranja", 78.0, 33.9, 105.0, 34.0, 3.2, 0.12)
    # o alto das muretas da escadaria, e o pórtico por onde se desce
    F.portico(ESCURO, "sz_led_laranja", 75.0, 34.2, True, 6.0, 4.2)

    # ---- Arena da Matilha
    placa(m, "ArenaDaMatilha", "arena_da_matilha", 92.0, 64.0, 9.5, 14.0, SUL, luz=AZUL)
    placa(m, "Galeria", "galeria", 58.0, 64.0, 7.0, 4.0, SUL)
    pintura(m, "ParedeDeCorrida", "parede_de_corrida", 100.0, 95.4, 6.0, "S")
    pintura(m, "AnelUivo", "anel_uivo", 106.0, 78.0, 6.2, "S", aceso=0.5)
    pintura(m, "CirculoDoUivo", "circulo_do_uivo", 106.0, 82.4, 5.0, "S")
    pintura(m, "RampaLonga", "rampa_longa", 87.0, 94.4, 4.6, "N")
    pintura(m, "Divisas", "divisas", 76.2, 90.5, 4.4, "N")
    pintura(m, "VaoDoSalto", "vao_do_salto", 56.6, 86.0, 4.0, "O")
    pintura(m, "RetaDeCorrida", "reta_de_corrida", 65.0, 73.4, 6.4, "N")
    for y in (68.4, 72.5):
        tira(M, "sz_faixa_branca", 44.0, y, 86.0, y + 0.12)
    # a base das paredes: rocha com musgo (menos a parede de corrida, que é lisa de cima a baixo)
    for (x0, y0, x1, y1) in ((44.0, 64.0, 72.0, 64.05), (78.0, 64.0, 104.0, 64.05), (40.0, 74.2, 40.05, 80.0), (40.0, 85.0, 40.05, 94.0), (119.95, 64.0, 120.0, 94.0)):
        bloco(M, "sz_rocha", x0, y0, x1, y1, 0.0, 1.3, colide=None, planta=False)
        bloco(M, "sz_folhagem_escura", x0, y0, x1 + 0.02 * (x1 - x0 < 1), y1 + 0.02 * (y1 - y0 < 1), 0.0, 0.18, colide=None, planta=False)
    for (x, y, r) in ((111, 70, 1.5), (115, 72, 1.2), (100, 70.5, 1.2), (47, 94.5, 1.2), (51, 96, 0.9), (108, 90, 1.5), (113, 88, 1.2), (116, 92, 1.2)):
        rocha(V, x, y, r, colide="caixa")
    for (x, y) in ((113, 67.8), (116, 69.4), (110, 67.4), (117.5, 72)):          # a mata baixa do canto
        pinheiro(V, M, x, y, 6.0, colide=True)
    # as plataformas: o soco escuro, os cunhais em talude e o friso de LED no alto
    for x0, x1, h in ((61.0, 67.0, SZ.PLATAFORMA_BAIXA), (72.0, 78.0, SZ.PLATAFORMA_ALTA)):
        F.caixa(ESCURO, x0 - 0.07, 81.93, x1 + 0.07, 88.07, 0.0, 0.4, chanfro=0.1)
        for (px, py, olha) in ((x0, 82.0, 135.0), (x1, 82.0, -135.0), (x0, 88.0, 45.0), (x1, 88.0, -45.0)):
            F.pilastra("sz_concreto_claro", px, py, olha + 180, 1.0, 0.36, 0.0, h, afina=0.35)
        for (a, b, c, d) in ((x0, 81.93, x1, 82.0), (x0, 88.0, x1, 88.07), (x0 - 0.07, 82.0, x0, 88.0), (x1, 82.0, x1 + 0.07, 88.0)):
            tira(M, "sz_led_azul", a, b, c, d, h - 0.16, 0.1)
    # os dois cantos cortados do sul: uma torre em cada um; e os pórticos altos das três entradas
    torre(F, 40.95, 97.05, 19.0, "sz_led_azul", flamula="sz_tecido_ciano")
    torre(F, 119.05, 97.05, 19.0, "sz_led_azul", flamula="sz_tecido_ciano")
    F.portico(ESCURO, "sz_led_azul", 75.0, 64.3, True, 6.0, 5.6, grosso=0.6, ombro=1.4)
    F.portico(ESCURO, "sz_led_azul", 112.0, 64.3, True, 6.0, 5.6, grosso=0.6, ombro=1.4)
    F.portico(ESCURO, "sz_led_verde", 40.3, 82.5, False, 5.0, 4.0)
    # os contrafortes das paredes leste e oeste (em talude, de 12 m), fora do caminho da corrida
    for y in (88.0, 92.0):
        F.pilastra("sz_concreto", 40.0, y, LESTE, 1.1, 0.5, 0.0, 12.0, afina=0.2)
    for y in (70.0, 80.0, 90.0):
        F.pilastra("sz_concreto", 120.0, y, OESTE, 1.1, 0.5, 0.0, 12.0, afina=0.2)

    # ---- Rua Ignição
    placa(m, "RuaIgnicao", "rua_ignicao", 126.0, 4.0, 4.6, 10.0, SUL, luz=LARANJA)
    for letra, x in (("a", 111.0), ("b", 126.0), ("c", 141.0)):
        pintura(m, f"Onda{letra.upper()}", f"onda_{letra}", x, 22.9, 2.6, "N")
        por(M, "SM_Floor_RoadPart_05", x, 20.2, SUL, escala=(1.7, 1.25, 1.0), planta=False)          # o aro de LED do portão
    placa(m, "KP", "kp", 145.3, 30.0, 4.3, 3.0, OESTE, aceso=0.8, luz=LARANJA)
    for x in (117.6, 133.6):                                     # as portas dos prédios (não se entra) e a marquise de cada uma
        por(M, "SM_Buildings_Door_01", x, 18.06, SUL, escala=(1.25, 1.0, 1.0), planta=False)
        tira(M, "sz_faixa_amarela", x - 0.9, 18.0, x + 0.9, 18.9)
        F.tronco(ESCURO, [W(x - 1.5, 18.0, 2.95), W(x + 1.5, 18.0, 2.95), W(x + 1.5, 19.7, 2.5), W(x - 1.5, 19.7, 2.5)],
                 [W(x - 1.5, 18.0, 3.07), W(x + 1.5, 18.0, 3.07), W(x + 1.5, 19.7, 2.62), W(x - 1.5, 19.7, 2.62)])
        for s in (-1.3, 1.3):
            F.viga(ESCURO, W(x + s, 18.02, 3.9), W(x + s, 19.5, 2.7), 0.05)
    # os cunhais dos prédios (pilastras em talude), a cimalha, e no terraço as antenas e as casas de máquina
    for x0, x1 in ((112.0, 124.0), (128.0, 140.0)):
        for (px, py, olha) in ((x0 + 0.5, 18.0, SUL), (x1 - 0.5, 18.0, SUL), ((x0 + x1) / 2, 18.0, SUL)):
            F.pilastra("sz_concreto", px, py, olha, 1.0, 0.34, 0.0, 6.0, afina=0.3)
        F.caixa(ESCURO, x0 - 0.12, 9.88, x1 + 0.12, 18.12, 5.72, 6.0, chanfro=0.2)
        F.caixa("sz_metal_pintado", x0 + 7.2, 11.4, x0 + 9.8, 13.4, 6.0, 7.5, chanfro=0.25, encolhe=0.12)
        por(M, "SM_Buildings_Part_29" if x0 < 120 else "SM_Buildings_Part_30", x0 + 10.4, 16.2, 15.0, base=6.0, escala=1.6, planta=False)
    por(M, "SM_Buildings_Part_32", 137.6, 12.0, -30.0, base=6.0, escala=1.5, planta=False)
    for y in (11.2, 16.6):                                       # os cabos entre os dois terraços
        pts = [W(123.8 + 4.4 * t, y, 7.6 - 1.1 * math.sin(math.pi * t)) for t in (i / 10 for i in range(11))]
        F.tubo("sz_borracha_escura", pts, 0.04)
        for x in (123.8, 128.2):
            F.viga(ESCURO, W(x, y, 6.0), W(x, y, 7.7), 0.07)
    # a treliça da passarela (as diagonais do guarda-corpo)
    for y in (12.7, 14.7):
        for k in range(4):
            F.viga(ESCURO, W(124.0 + k, y, 6.05 + (0.9 if k % 2 else 0.0)), W(125.0 + k, y, 6.05 + (0.0 if k % 2 else 0.9)), 0.05)
    for (x, y) in ((108.6, 6.6), (143.6, 6.8), (108.2, 33.4)):
        arvore(M, V, x, y, 1.3, "scifi", colide=True)
    for (x, y, o) in ((106.6, 14, LESTE), (106.6, 26, LESTE), (145.4, 22, OESTE), (145.4, 16, OESTE)):
        poste(M, x, y, o)
    tira(M, "sz_faixa_amarela", 106.0, 19.2, 146.0, 19.35)       # a linha dos portões de onda
    torre(F, 143.6, 33.6, 12.0, "sz_led_laranja", raio=0.8, flamula="sz_tecido_laranja")

    # ---- Ponto de Retorno
    placa(m, "PontoDeRetorno", "ponto_de_retorno", 17.0, 70.4, 3.8, 8.0, SUL, luz=VERDE)
    placa(m, "PortalJogar", "portal_jogar", 9.5, 94.8, 4.0, 3.4, NORTE, aceso=0.6)
    for n_, x in enumerate((8.0, 13.5, 19.0, 24.5), 1):
        pintura(m, f"Pedestal{n_}", f"pedestal_{n_}", x, 80.0, 3.6, "N", aceso=0.5)
        for (a, b, c, d) in ((x - 2, 78.0, x + 2, 78.08), (x - 2, 81.92, x + 2, 82.0), (x - 2, 78.0, x - 1.92, 82.0), (x + 1.92, 78.0, x + 2, 82.0)):
            tira(M, "sz_led_verde", a, b, c, d)
    pintura(m, "AnelLobo", "anel_lobo", 26.0, 90.0, 4.2, "N", aceso=0.5)
    banco(M, 8.0, 87.0, olha=NORTE, colide="caixa"); banco(M, 13.5, 87.0, olha=NORTE, colide="caixa")
    for x in (5.6, 28.4):
        por(M, "SM_Nature_Flowerbed_03", x, 74.6, sorte("vr", x) * 360, escala=1.3, colide="caixa")
    bloco(M, ESCURO, 4.4, 85.5, 4.5, 90.5, 1.5, 4.0, colide=None, planta=False)       # o telão de KP
    placa(m, "KPRetorno", "kp", 4.5, 88.0, 2.75, 2.4, LESTE, aceso=0.8)
    tira(M, "sz_led_verde", 4.4, 70.4, 4.52, 95.6, 4.6, 0.1); tira(M, "sz_led_verde", 29.48, 70.4, 29.6, 80.0, 4.6, 0.1); tira(M, "sz_led_verde", 29.48, 85.0, 29.6, 95.6, 4.6, 0.1)
    for y0 in (80.0, 84.85):
        tira(M, "sz_led_verde", 30.0, y0, 39.6, y0 + 0.15)
    for x in (33.0, 36.6):
        arco_de_luz(F, x, 79.8, 85.2, 4.0, 1.1, "sz_led_verde")


# ------------------------------------------------------------------ o terreno dos jardins
# O Jhon, vendo o jardim (26/11): "estou achando esses chãos muito simples, retos; natureza também; algumas árvores,
# postes em lugares aleatórios no caminho". Jardim não é chapa verde com peças espalhadas por coordenada: é um
# terreno — relevo suave, o caminho recortado nele (curvo), a grama em tons — e o que se planta obedece ao caminho:
# árvore longe dele, poste e banco à margem.

def curva(pontos, por_trecho=10):
    """Uma curva suave que passa pelos `pontos` (Catmull-Rom), em linha de pontos próximos."""
    P_ = [pontos[0]] + list(pontos) + [pontos[-1]]
    out = []
    for i in range(1, len(P_) - 2):
        p0, p1, p2, p3 = P_[i - 1], P_[i], P_[i + 1], P_[i + 2]
        for k in range(por_trecho):
            s = k / por_trecho
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * s + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * s * s
                                    + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * s ** 3) for j in (0, 1)))
    out.append(tuple(pontos[-1]))
    return out


class Terreno:
    """Um trecho de terreno: o retângulo, os caminhos (linhas de pontos), os lugares planos (círculos (x, y, raio) e
    retângulos) e o relevo. Sabe a distância de um ponto ao caminho e a altura do chão ali — é por ele que se planta."""

    def __init__(self, ret, caminhos, planos=(), amplitude=1.0, semente=5, largura=1.25, escala=9.0):
        import numpy as np
        from .setor_zero_formas import Ruido
        self.np, self.ret, self.caminhos, self.planos = np, ret, [np.array(c, float) for c in caminhos], planos
        self.amplitude, self.largura, self.escala = amplitude, largura, escala
        self.R, self.R2 = Ruido(semente), Ruido(semente + 31)

    def ao_caminho(self, X, Y):
        """A distância de cada ponto ao caminho mais próximo."""
        np = self.np; X, Y = np.asarray(X, float), np.asarray(Y, float); d = np.full(X.shape, 1e9)
        for c in self.caminhos:
            for (x0, y0), (x1, y1) in zip(c[:-1], c[1:]):
                dx, dy = x1 - x0, y1 - y0; L2 = dx * dx + dy * dy
                t_ = np.clip(((X - x0) * dx + (Y - y0) * dy) / max(L2, 1e-9), 0.0, 1.0)
                d = np.minimum(d, np.hypot(X - (x0 + dx * t_), Y - (y0 + dy * t_)))
        return d

    def ao_plano(self, X, Y):
        """A distância de cada ponto ao lugar plano mais próximo (negativa lá dentro)."""
        np = self.np; X, Y = np.asarray(X, float), np.asarray(Y, float); d = np.full(X.shape, 1e9)
        for pl in self.planos:
            if len(pl) == 3: d = np.minimum(d, np.hypot(X - pl[0], Y - pl[1]) - pl[2])
            else:
                x0, y0, x1, y1 = pl
                d = np.minimum(d, np.hypot(np.maximum(np.maximum(x0 - X, X - x1), 0.0), np.maximum(np.maximum(y0 - Y, Y - y1), 0.0))
                               - np.where((X > x0) & (X < x1) & (Y > y0) & (Y < y1), np.minimum(np.minimum(X - x0, x1 - X), np.minimum(Y - y0, y1 - Y)), 0.0))
        return d

    def altura(self, X, Y):
        """A altura do chão: o relevo, que morre no caminho, nos lugares planos e na borda do retângulo."""
        np = self.np; X, Y = np.asarray(X, float), np.asarray(Y, float)
        def degrau(a, b, v): t_ = np.clip((v - a) / (b - a), 0.0, 1.0); return t_ * t_ * (3 - 2 * t_)
        x0, y0, x1, y1 = self.ret
        borda = np.minimum(np.minimum(X - x0, x1 - X), np.minimum(Y - y0, y1 - Y))
        relevo = self.R.fractal(X / self.escala, Y / self.escala, 3) ** 1.4 + 0.25 * self.R2.fractal(X / 3.0, Y / 3.0, 2)
        return 0.035 + self.amplitude * relevo * degrau(self.largura, self.largura + 3.2, self.ao_caminho(X, Y)) * degrau(0.0, 2.4, self.ao_plano(X, Y)) * degrau(0.0, 2.2, borda)

    def h(self, x, y):
        return float(self.altura([x], [y])[0])

    def livre(self, x, y, folga):
        """O ponto fica a mais de `folga` do caminho e fora dos lugares planos."""
        return float(self.ao_caminho([x], [y])[0]) > folga and float(self.ao_plano([x], [y])[0]) > 0.6

    def desenhar(self, F, pinta, passo=1.0):
        np = self.np; x0, y0, x1, y1 = self.ret
        xs = np.linspace(x0, x1, int(round((x1 - x0) / passo)) + 1); ys = np.linspace(y0, y1, int(round((y1 - y0) / passo)) + 1)
        X, Y = np.meshgrid(xs, ys, indexing="ij")
        F.grade(np.stack([X - SZ.CX, self.altura(X, Y), Y - SZ.CY], axis=2), lambda meio, n: pinta(meio[:, 0] + SZ.CX, meio[:, 2] + SZ.CY, meio[:, 1], n), suave=True)

    def fita(self, F, caminho, mat="sz_brita", guia="sz_calcamento_claro"):
        """O caminho `caminho` como uma fita lisa por cima do terreno (as bordas acompanham a curva), com uma guia de
        pedra de cada lado."""
        np = self.np; c = self.caminhos[caminho]
        d = np.gradient(c, axis=0); d /= np.maximum(np.linalg.norm(d, axis=1, keepdims=True), 1e-9)
        lado = np.stack([-d[:, 1], d[:, 0]], axis=1)
        def tira_(de, ate, h, mat_):
            A, B = c + lado * de, c + lado * ate
            V_ = np.stack([np.stack([A[:, 0] - SZ.CX, np.full(len(c), h), A[:, 1] - SZ.CY], axis=1), np.stack([B[:, 0] - SZ.CX, np.full(len(c), h), B[:, 1] - SZ.CY], axis=1)], axis=1)
            F.grade(V_, lambda meio, n: np.full(len(meio), mat_, dtype=object), suave=True)
        tira_(-self.largura, self.largura, 0.055, mat)
        if guia:
            tira_(-self.largura - 0.28, -self.largura, 0.085, guia); tira_(self.largura, self.largura + 0.28, 0.085, guia)

    def ao_longo(self, caminho, passo, lado):
        """Pontos à margem do caminho `caminho` (o índice): a cada `passo` m, `lado` m para o lado (o sinal alterna).
        Devolve [(x, y, graus para onde olha o caminho dali)]."""
        c = self.caminhos[caminho]; out = []; andou = passo * 0.5; k = 0
        for (xa, ya), (xb, yb) in zip(c[:-1], c[1:]):
            L = math.hypot(xb - xa, yb - ya)
            while andou <= L and L > 1e-6:
                s = andou / L; ux, uy = (xb - xa) / L, (yb - ya) / L; sinal = 1 if k % 2 == 0 else -1
                px, py = xa + (xb - xa) * s - uy * lado * sinal, ya + (yb - ya) * s + ux * lado * sinal
                out.append((px, py, math.degrees(math.atan2(uy * sinal, -ux * sinal))))          # a frente (+Z) virada para o caminho
                andou += passo; k += 1
            andou -= L
        return out


def plantar(T, quantos, folga, semente, espaco, borda=1.5):
    """`quantos` pontos espalhados pelo terreno `T`, longe do caminho (`folga`) e uns dos outros (`espaco`)."""
    x0, y0, x1, y1 = T.ret; out = []
    k = 0
    while len(out) < quantos and k < quantos * 60:
        k += 1
        x = x0 + borda + (x1 - x0 - 2 * borda) * sorte("px", semente, k); y = y0 + borda + (y1 - y0 - 2 * borda) * sorte("py", semente, k)
        if not T.livre(x, y, folga): continue
        if any(math.hypot(x - a, y - b) < espaco for a, b in out): continue
        out.append((x, y))
    return out


def rasteira(M, V, T, quantos, semente):
    """A vegetação rasteira: tufos de capim, folhagens e flores do kit, em pequenos grupos, fora do caminho."""
    for (x, y) in plantar(T, quantos, T.largura + 0.5, semente, 1.6, 0.8):
        s = sorte("rt", semente, x, y)
        for j in range(1 + int(s * 3)):
            px, py = x + 0.7 * (sorte("rx", x, j) - 0.5), y + 0.7 * (sorte("ry", y, j) - 0.5)
            if not T.livre(px, py, T.largura + 0.3): continue
            h = T.h(px, py)
            tipo = sorte("rk", px, py)
            if tipo < 0.4: por(M, "SM_Nature_Grass_01", px, py, 360 * s, base=h, escala=(1.0, 1.3, 1.6)[int(sorte("re", px) * 3)], planta=False)
            elif tipo < 0.6: por(V, "Grass_01", px, py, 360 * s, base=h, escala=(0.9, 1.2)[int(sorte("re", px) * 2)], planta=False)
            elif tipo < 0.78: por(V, "Plant_01", px, py, 360 * s, base=h, escala=(0.55, 0.8)[int(sorte("re", px) * 2)], planta=False)
            elif tipo < 0.9: por(M, ("SM_Nature_Plant_01", "SM_Nature_Plant_02", "SM_Nature_Plant_04")[int(s * 3)], px, py, 360 * s, base=h, planta=False)
            else: por(M, "SM_Nature_Flower_01", px, py, 360 * s, base=h, escala=1.4, planta=False)


# ------------------------------------------------------------------ as áreas de apoio (fora do percurso)

def jardim(m, M, V, F):
    """O Jardim Suspenso: um terreno de relevo suave com o caminho curvo recortado nele — da janela da Sala de
    Ativação ao espelho d'água, à Torre de Controle e à saída —, a grama em tons, a vegetação rasteira, e as árvores,
    os postes e os bancos postos pelo caminho (árvore a mais de 3 m dele; poste e banco à margem)."""
    import numpy as np
    principal = curva([(12.0, 37.3), (11.6, 31.5), (14.6, 26.8), (19.6, 24.4), (24.4, 25.0), (29.4, 23.6), (32.6, 20.0), (33.4, 16.4)])
    ramal = curva([(29.4, 23.6), (33.4, 25.6), (36.8, 28.4), (40.2, 30.6), (41.0, 34.4)])
    fonte_ = curva([(14.6, 26.8), (12.6, 25.2), (11.8, 24.6)], 5)
    planos = [(24.0, 18.2, 5.5), (28.4, 5.4, 38.6, 15.6), (33.0, 29.4, 2.3), (41.5, 22.5, 2.5), (11.6, 23.4, 2.6)]
    T = Terreno((4.0, 3.0, 44.0, 35.0), [principal, ramal, fonte_], planos, amplitude=1.5, semente=7, largura=1.15)
    R = T.R2
    def pinta(X, Y, H, n):
        d = T.ao_caminho(X, Y); tom = R.fractal(X / 5.0, Y / 5.0, 3)
        nomes = np.where(tom < 0.4, "sz_folhagem_musgo", np.where(tom < 0.62, "sz_folhagem", "sz_folhagem_clara")).astype(object)
        return nomes
    T.desenhar(F, pinta, 0.8)
    for c in (0, 1, 2):
        T.fita(F, c)
    for (x, y, r) in ((11.6, 23.4, 2.9), (33.0, 29.4, 2.6)):          # as pracinhas da fonte e dos anéis
        F.cilindro("sz_calcamento_claro", x, y, r, 0.0, 0.06, 24); F.anel("sz_concreto_claro", x, y, r, r + 0.22, 0.0, 0.1, 15.0)

    # as árvores: longe do caminho e umas das outras; as copas grandes no fundo (norte e oeste), as pequenas na frente
    for k, (x, y) in enumerate(plantar(T, 16, 3.2, 11, 5.2, 2.2)):
        por_tipo = ("scifi", "copa", "galhos")[k % 3]
        r = (2.5, 2.1, 1.5)[int(sorte("jr", x, y) * 3)]
        tipo, yaw = por_tipo, sorte("giro", x, y) * 360
        if tipo == "scifi": por(M, "SM_Nature_Tree_01", x, y, yaw, base=T.h(x, y) - 0.05, escala=r / 2.6, planta=False)
        elif tipo == "copa": por(V, "Tree_04", x, y, yaw, base=T.h(x, y) - 0.05, escala=r / 2.2, planta=False)
        else: por(V, "Tree_03", x, y, yaw, base=T.h(x, y) - 0.05, escala=r / 3.0, planta=False)
    for (x, y) in plantar(T, 5, 2.6, 23, 6.0):
        pinheiro_em(V, x, y, 12.0, T.h(x, y))
    rasteira(M, V, T, 70, 3)
    for (x, y) in plantar(T, 9, 2.0, 41, 4.0):
        rocha(V, x, y, 0.9, base=T.h(x, y) - 0.25)
    # os postes e os bancos: à margem do caminho, de frente para ele
    for (x, y, olha) in T.ao_longo(0, 8.5, 1.9) + T.ao_longo(1, 8.5, 1.9):
        por(M, "SM_Floor_Light_01", x, y, olha + 90.0, base=T.h(x, y), planta=False)
    for k, (x, y, olha) in enumerate(T.ao_longo(0, 11.0, 2.0)[1:4] + T.ao_longo(1, 9.0, 2.0)[1:2]):
        por(M, ("SM_Props_Bench_01", "SM_Props_Bench_02")[k % 2], x - 0.0, y, olha, base=T.h(x, y), escala=1.5)
    sebe(M, 6, 33.2, 4.6, 1.2); sebe(M, 13.6, 33.2, 2.4, 1.2); sebe(M, 20, 33.2, 18, 1.2)
    # o espelho d'água: um disco de 4,6 m de raio, a borda de pedra em anel e uma ilha de rocha
    F.cilindro("sz_agua", 24.0, 18.2, 4.6, 0.0, 0.07, 28)
    F.anel("sz_concreto_claro", 24.0, 18.2, 4.6, 5.05, 0.0, 0.34, 10.0, r1_topo=4.95)
    rocha(V, 22.6, 17.4, 0.9); rocha(V, 25.4, 19.6, 0.9)
    # a Torre de Controle (a torre do kit, com a sala de observação envidraçada e as antenas) sobre um embasamento
    # de oito lados, com a antena parabólica grande ao lado, apontada para o céu
    F.caixa("sz_concreto_claro", 29.0, 6.0, 38.0, 15.0, 0.0, 0.5, chanfro=1.6)
    F.caixa(ESCURO, 28.8, 5.8, 38.2, 15.2, 0.0, 0.18, chanfro=1.7)
    por(M, "SM_BigFence_02", 32.6, 10.5, SUL, base=0.5, escala=(1.4, 1.05, 1.4))
    antena(F, 36.4, 8.0, 0.5, 2.3, (0.45, 0.82, -0.36))
    totem(M, m, "TorreDeControle", "torre_de_controle", 36.6, 16.3, SUL, 3.6, luz=CIANO)
    totem(M, m, "JardimSuspenso", "jardim_suspenso", 15.4, 34.6, SUL, 3.6)
    # a cisterna (o domo do kit, em meia escala)
    por(M, "SM_Nature_Bio_01", 41.5, 22.5, 0.0, escala=0.5)


def pinheiro_em(V, x, y, altura, base):
    altura = min((6.0, 12.0, 18.0, 21.5, 25.0), key=lambda v: abs(v - altura))
    por(V, "Tree_01", x, y, sorte("pinho", x, y) * 360, base=base - 0.1, escala=altura / 7.4, planta=False)


def antena(F, x, y, base, raio, eixo):
    """Uma antena parabólica: o pedestal, a forquilha e o prato (um cone raso) apontado na direção `eixo` (mundo)."""
    F.cilindro("sz_concreto", x, y, 0.9, base, base + 0.5, 8, r_topo=0.7)
    F.cilindro(ESCURO, x, y, 0.28, base + 0.5, base + 2.6, 8, r_topo=0.2)
    o = W(x, y, base + 2.9)
    F.revolucao("sz_concreto_claro", o, eixo, [(0.0, 0.25), (0.25, raio * 0.55), (0.6, raio)], 20)
    F.revolucao(ESCURO, o, eixo, [(-0.45, 0.16), (0.0, 0.3)], 8)
    e = [v / math.sqrt(sum(c * c for c in eixo)) for v in eixo]
    F.viga(ESCURO, o + [e[0] * 0.2, e[1] * 0.2, e[2] * 0.2], o + [e[0] * (raio * 0.95), e[1] * (raio * 0.95), e[2] * (raio * 0.95)], 0.06)
    F.revolucao("sz_led_branco", o + [e[0] * raio * 0.95, e[1] * raio * 0.95, e[2] * raio * 0.95], eixo, [(0.0, 0.12), (0.14, 0.12)], 6)


def meio(m, M, V, F):
    """O que fica entre os lugares do percurso: a Quadra de Agilidade, o Posto Médico, o Café do Setor, os canteiros
    e os bancos das passagens."""
    # ---- Quadra de Agilidade (borracha, linhas e balizas — ornamental: aquecimento, sem etapa)
    for (x0, y0, x1, y1) in ((22.0, 37.0, 31.0, 37.1), (22.0, 43.9, 31.0, 44.0), (22.0, 37.0, 22.1, 44.0), (30.9, 37.0, 31.0, 44.0), (26.45, 37.0, 26.55, 44.0)):
        tira(M, "sz_faixa_branca", x0, y0, x1, y1)
    for x, y in ((23.5, 38.5), (25, 40), (23.5, 41.5), (25, 43), (28, 38.5), (29.5, 40), (28, 41.5), (29.5, 43)):
        F.cilindro("sz_plastico_laranja", x, y, 0.2, 0.0, 0.6, 10, r_topo=0.035)          # um cone
        F.cilindro("sz_plastico_laranja", x, y, 0.27, 0.0, 0.04, 4, giro=math.pi / 4)
    totem(M, m, "QuadraDeAgilidade", "quadra_de_agilidade", 26.5, 36.3, SUL, 4.2)
    for (x, y) in ((36, 38), (41, 38.5), (38, 42)): arvore(M, V, x, y, 1.5)
    banco(M, 33.5, 40.5, ao_x=False)

    # ---- Posto Médico: cenário de descanso (não cura — não há regra nova). Um pavilhão de cantos chanfrados, com
    #      a cobertura em balanço e a cruz
    F.caixa("sz_concreto_claro", 46.0, 36.2, 54.5, 41.8, 0.0, 3.2, chanfro=0.8)
    F.caixa(ESCURO, 45.6, 35.8, 54.9, 42.6, 3.2, 3.45, chanfro=1.0)
    F.caixa(ESCURO, 45.9, 36.1, 54.6, 41.9, 0.0, 0.3, chanfro=0.85)
    for (x0, y0, x1, y1) in ((49.3, 37.4, 51.2, 40.6), (48.65, 38.05, 51.85, 39.95)):          # a cruz no telhado
        bloco(M, "sz_metal_pintado_verde", x0, y0, x1, y1, 3.45, 3.51, colide=None, planta=False)
    for (x0, x1, b, t_) in ((50.05, 50.45, 1.5, 2.7), (49.65, 50.85, 1.9, 2.3)):                # e na fachada sul
        bloco(M, "sz_metal_pintado_verde", x0, 41.8, x1, 41.85, b, t_, colide=None, planta=False)
    por(M, "SM_Buildings_Door_01", 47.6, 41.86, SUL, planta=False)
    placa(m, "PostoMedico", "posto_medico", 52.4, 41.8, 2.6, 2.6, SUL, luz=VERDE)
    arvore(M, V, 58, 40, 1.4); arvore(M, V, 60, 36.6, 1.2); banco(M, 56, 43.4); poste(M, 57, 38)

    # ---- Café do Setor: dois quiosques sob uma cobertura inclinada, três mesas (não dá nada)
    por(M, "SM_Props_CyberIceCream_01", 92.2, 38.6, SUL, escala=1.25); por(M, "SM_Props_CyberIceBurger_01", 94.9, 38.6, SUL, escala=1.25)
    por(M, "SM_Props_VendingMachine_01", 96.9, 38.4, SUL)
    F.tronco(ESCURO, [W(90.4, 37.0, 3.9), W(98.0, 37.0, 3.9), W(98.0, 41.2, 3.2), W(90.4, 41.2, 3.2)],
             [W(90.4, 37.0, 4.04), W(98.0, 37.0, 4.04), W(98.0, 41.2, 3.34), W(90.4, 41.2, 3.34)])
    for x in (90.7, 97.7):
        F.viga(ESCURO, W(x, 37.2, 0.0), W(x, 37.2, 3.9), 0.12); F.viga(ESCURO, W(x, 37.3, 2.6), W(x, 40.9, 3.26), 0.07)
    for (x, y) in ((100.5, 38.2), (102.3, 42.2), (98.3, 43.4)):
        por(M, "SM_Props_Cafe_01", x, y, sorte("mesa", x) * 90)
        for k in range(3):
            a = math.radians(k * 120 + 20 + 90 * sorte("cad", x))
            por(M, "SM_Props_Cafe_04", x + 1.0 * math.cos(a), y + 1.0 * math.sin(a), 90 - math.degrees(a), planta=False)
    totem(M, m, "CafeDoSetor", "cafe_do_setor", 93.5, 42.4, SUL, 3.6)
    arvore(M, V, 88, 43.5, 1.3); arvore(M, V, 104.3, 37.2, 1.3); poste(M, 95, 44.2); poste(M, 103.4, 40.2)
    arvore(M, V, 107, 39, 1.3); arvore(M, V, 119, 38, 1.3); sebe(M, 116, 40.3, 5, 0.8); sebe(M, 104.4, 40.3, 3.6, 0.8)

    # ---- as passagens ao sul da Sala de Ativação e do corredor
    for x in (8, 14, 20, 26): arvore(M, V, x, 65.6, 1.5)
    sebe(M, 5, 67.6, 24, 0.9); poste(M, 11, 66.6); poste(M, 23, 66.6)
    for x in (24, 34, 44, 54): arvore(M, V, x, 62.6, 1.3)
    for x in (28, 40, 50): banco(M, x, 59.7, olha=NORTE)
    for x in (31, 47, 60): poste(M, x, 61.4)
    for x in (22, 36, 58):
        jardineira(M, x + 1.2, 57.0)
    caminho(M, [(21, 58.4), (61.6, 58.4)], 1.8, "sz_calcamento_claro")
    caminho(M, [(21, 44.9), (44, 44.9)], 1.6, "sz_calcamento_claro")
    # ---- o terraço ao sul do Núcleo e da Câmara
    for (x, y) in ((92, 57), (96, 60.4), (99.4, 56.5)): arvore(M, V, x, y, 1.5)
    banco(M, 92.4, 59.6); banco(M, 97.6, 57.6, ao_x=False)
    sebe(M, 90, 62.2, 12, 0.9); sebe(M, 104, 62.4, 4.4, 0.8); sebe(M, 115.6, 62.4, 4.4, 0.8); poste(M, 104, 60.5); poste(M, 119.4, 60.5)
    caminho(M, [(90, 58.6), (104, 58.6)], 1.8, "sz_calcamento_claro")
    # ---- ao norte do corredor e do salão
    for x in (50, 60): jardineira(M, x, 43.6)
    arvore(M, V, 60.6, 40.8, 1.4); arvore(M, V, 88.6, 39.6, 1.3); poste(M, 66, 36.4); poste(M, 84, 36.4)
    caminho(M, [(45, 44.9), (61.6, 44.9)], 1.6, "sz_calcamento_claro")
    caminho(M, [(89.6, 44.6), (104, 44.6)], 1.6, "sz_calcamento_claro")


def patio(m, M, V, F):
    """O pátio técnico entre o Ponto de Retorno e a Arena: os contêineres de material (chanfrados, com as nervuras)."""
    for k, (x, y, w, h, cor) in enumerate(((31.2, 66, 7.4, 3.2, "laranja"), (31.2, 70.4, 7.4, 3.2, "laranja"), (31.2, 74.8, 3.2, 3.4, "azul"),
                                           (31.2, 87, 7.4, 3.2, "azul"), (31.2, 91.4, 7.4, 3.2, "laranja"))):
        F.caixa(f"sz_lataria_{cor}", x, y, x + w, y + h, 0.12, 2.6, chanfro=0.12)
        F.caixa(ESCURO, x - 0.03, y - 0.03, x + w + 0.03, y + h + 0.03, 0.0, 0.14, chanfro=0.12)
        F.caixa(ESCURO, x - 0.03, y - 0.03, x + w + 0.03, y + h + 0.03, 2.52, 2.64, chanfro=0.12)
        for i in range(1, int(w)):                               # as nervuras da chapa
            bloco(M, ESCURO, x + i - 0.03, y - 0.02, x + i + 0.03, y + h + 0.02, 0.14, 2.52, colide=None, planta=False)
    placa(m, "ConteinerProvisorio", "provisorio", 38.6, 68.0, 1.6, 1.6, LESTE, aceso=0.0)
    poste(M, 37.6, 77.6); poste(M, 33, 86.2); arvore(M, V, 37, 96.4, 1.3); banco(M, 35.4, 75.6, ao_x=False)


def praca(m, M, V, F):
    """A Praça do Uivo: redonda, com três degraus até o pedestal da estátua do lobo (a estátua é peça pendente),
    bancos em volta, árvores e lâmpadas."""
    F.anel("sz_calcamento_claro", 136, 51, 9.6, 10.2, 0.0, 0.05, 7.5)
    F.anel("sz_calcamento_claro", 136, 51, 5.0, 5.5, 0.0, 0.05, 7.5)
    for r, h0, h1 in ((3.6, 0.0, 0.2), (3.0, 0.2, 0.4), (2.4, 0.4, 0.6)):
        F.cilindro("sz_concreto_claro", 136, 51, r, h0, h1, 24)
    F.cilindro("sz_concreto", 136, 51, 1.5, 0.6, 1.5, 8, r_topo=1.25, giro=math.pi / 8)
    F.cilindro(ESCURO, 136, 51, 1.35, 1.5, 1.62, 8, giro=math.pi / 8)
    # a escultura do lobo uivando: uma silhueta recortada numa chapa de aço de 45 cm, de 4 m, sobre o pedestal
    F.perfil(ESCURO, LOBO, W(135.75, 51.0, 1.62), (1.0, 0.0, 0.0), 0.45)
    F.perfil("sz_led_azul", [(u * 1.0, v) for u, v in LOBO_OLHO], W(135.75, 51.0, 1.62), (1.0, 0.0, 0.0), 0.47)
    for (x, y) in ((126, 42), (146, 42), (126, 61), (146, 61)): arvore(M, V, x, y, 2.1)
    for ang in (30, 150, 210, 330):                              # os bancos, em roda, virados para o meio
        a = math.radians(ang)
        por(M, "SM_Props_Bench_01", 136 + 7.4 * math.cos(a), 51 + 7.4 * math.sin(a), 270.0 - ang, escala=1.5)
    for ang in (90, 270, 0, 180):
        a = math.radians(ang); poste(M, 136 + 8.6 * math.cos(a), 51 + 8.6 * math.sin(a), 270.0 - ang)
    sebe(M, 124, 40.3, 6, 1.2, 1.0); sebe(M, 142, 40.3, 6, 1.2, 1.0)
    totem(M, m, "PracaDoUivo", "praca_do_uivo", 136.0, 41.0, SUL, 4.4)


def pinhal(m, M, V, F):
    """O Pinhal da Matilha: um chão de mata em relevo, com a trilha curva recortada nele, pinheiros de 18 a 25 m
    (acima dos muros da arena, de 14 m, fechando o fundo dela), o sub-bosque e as pedras."""
    import numpy as np
    trilha = curva([(121.6, 87.0), (126.0, 84.0), (130.6, 82.4), (135.0, 84.6), (139.4, 86.6), (143.4, 83.4), (146.4, 79.6)])
    T = Terreno((121.0, 65.0, 147.2, 99.2), [trilha], [], amplitude=1.9, semente=13, largura=0.95, escala=7.5)
    R = T.R2
    def pinta(X, Y, H, n):
        d = T.ao_caminho(X, Y); tom = R.fractal(X / 4.0, Y / 4.0, 3)
        nomes = np.where(tom < 0.36, "sz_terra", np.where(tom < 0.56, "sz_folhagem_musgo", np.where(tom < 0.7, "sz_folhagem_escura", "sz_terra"))).astype(object)
        return nomes
    T.desenhar(F, pinta, 0.9)
    T.fita(F, 0, guia=None)
    # Os grandes ficam a 8 m ou mais do muro da arena: a saia do pinheiro tem até 7 m de raio e atravessaria a parede.
    Tg = Terreno((127.0, 65.0, 147.2, 99.2), [trilha], [], amplitude=0.0)
    for (x, y) in plantar(Tg, 17, 3.0, 5, 5.4, 1.6):
        pinheiro_em(V, x, y, 18.0 + 7.0 * sorte("pinhal", x, y), T.h(x, y))
    for (x, y) in plantar(T, 12, 2.0, 17, 3.2):
        pinheiro_em(V, x, y, 6.0, T.h(x, y))
    rasteira(M, V, T, 80, 9)
    for (x, y) in plantar(T, 12, 1.6, 29, 3.4):
        rocha(V, x, y, (0.9, 1.2, 1.5)[int(sorte("pr", x) * 3)], base=T.h(x, y) - 0.3)
    for (x, y) in plantar(T, 6, 1.6, 53, 5.0):
        por(M, "SM_Nature_Log_01", x, y, 360 * sorte("log", x), base=T.h(x, y), escala=1.6, planta=False)
    totem(M, m, "PinhalDaMatilha", "pinhal_da_matilha", 133.0, 65.6, NORTE, 4.2)


def bordas(m, M, V, F):
    """Os quatro lados do campo: a pista de serviço (norte, cortada pela Linha de Fogo, com as baias de drone), a
    alameda (oeste), o canteiro (leste) e a pista de aquecimento (sul); e uma torre de vigia em cada canto."""
    fora = lambda x: x < SZ.BEIRA[0] - 2 or x > SZ.BEIRA[1] + 2
    for x in range(0, 150, 11):
        if fora(x) and fora(x + 6): tira(M, "sz_faixa_amarela", x, -0.72, x + 6, -0.58)
    for x in range(2, 150, 10):
        if fora(x): poste(M, x, 0.6)
    for x in (8, 30, 114, 136):
        F.caixa(ESCURO, x, -2.3, x + 2.8, -0.8, 0.0, 0.12, chanfro=0.3)
        F.anel("sz_led_ciano", x + 1.4, -1.55, 0.55, 0.62, 0.12, 0.14, 30.0)
        por(M, "SM_Props_Drone_01", x + 1.4, -1.55, 20.0 * sorte("drone", x), base=0.14, planta=False)
    for x in (43.2, 106.8):                                      # onde a pista acaba na Linha de Fogo: barreiras
        for y in (-2.0, 0.2):
            por(M, "SM_Floor_RoadPart_01", x, y, LESTE, planta=False)
    # ---- oeste: alameda
    for y in range(4, 100, 9):
        arvore(M, V, 0.3, y, 1.4)
        if y + 4.5 < 99: poste(M, 2.6, y + 4.5, LESTE)
    # ---- leste: canteiro (sebe de 94 m e pinheiros)
    sebe(M, 147.3, 4, 2.2, 94, 1.0)
    for y in range(6, 98, 11):
        pinheiro(V, M, 151.2, y, 12.0)
        if y + 5 < 99: poste(M, 150.2, y + 5, OESTE)
    # ---- sul: pista de aquecimento, quatro raias
    for k in range(1, 4):
        tira(M, "sz_faixa_branca", 2.0, 99.3 + k * 0.8 - 0.04, 150.0, 99.3 + k * 0.8 + 0.04)
    for x in (2, 10, 60, 110, 140, 150):
        tira(M, "sz_faixa_branca", x - 0.06, 99.3, x + 0.06, 102.5)
    for k, x in enumerate((36, 86, 128)):
        pintura(m, f"PistaDeAquecimento{k + 1}", "pista_de_aquecimento", x, 100.9, 8.0, "N")
    # ---- as quatro torres de vigia dos cantos (a torre do kit) e a faixa de LED no alto do parapeito
    for (x, y, yaw) in ((-0.4, -0.4, 45.0), (150.4, -0.4, -45.0), (150.4, 100.4, -135.0), (-0.4, 100.4, 135.0)):
        por(M, "SM_BigFence_02", x, y, yaw, escala=1.15, planta=False)


# ------------------------------------------------------------------ a arquitetura
# O lobo uivando, de perfil, sentado, o focinho para o alto (u para a frente dele, v para cima; em metros).
LOBO = [(-1.7, 0.0), (-0.6, 0.0), (0.45, 0.0), (0.5, 0.22), (0.15, 0.3), (0.35, 0.9), (0.62, 0.55), (0.6, 0.0), (0.98, 0.0), (0.95, 0.5),
        (0.88, 1.5), (1.0, 2.15), (1.45, 3.2), (1.72, 3.72), (1.5, 3.85), (1.05, 3.3), (0.85, 3.45), (0.5, 3.95), (0.45, 3.2),
        (0.1, 2.75), (-0.3, 2.0), (-0.7, 1.3), (-1.0, 0.75), (-1.35, 0.42)]
LOBO_OLHO = [(1.02, 3.02), (1.16, 3.08), (1.1, 3.16)]


def revestir(F, x0, y0, x1, y1, olha, base, topo, passo=6.0, led=None, evitar=(), fundo=0.3, mat="sz_concreto"):
    """Veste uma parede lisa: ao longo da face de (x0, y0) a (x1, y1), que olha para `olha`, um soco em talude ao pé,
    um friso no alto (com uma linha de LED por baixo) e pilastras em talude a cada `passo` m — menos nos trechos
    `evitar` = [(de, até)], em metros a partir de (x0, y0): o letreiro, a estante, a porta. Sem colisão (30 cm)."""
    a = math.radians(olha); fx, fy = math.sin(a), math.cos(a)
    L = math.hypot(x1 - x0, y1 - y0); ux, uy = (x1 - x0) / L, (y1 - y0) / L
    def faixa(mat_, h0, h1, f0, f1):
        F.tronco(mat_, [W(x0, y0, h0), W(x1, y1, h0), W(x1 + fx * f0, y1 + fy * f0, h0), W(x0 + fx * f0, y0 + fy * f0, h0)],
                 [W(x0, y0, h1), W(x1, y1, h1), W(x1 + fx * f1, y1 + fy * f1, h1), W(x0 + fx * f1, y0 + fy * f1, h1)])
    faixa(ESCURO, base, base + 0.55, 0.16, 0.04)
    faixa(ESCURO, topo - 0.32, topo, 0.07, 0.12)
    if led:
        faixa(led, topo - 0.42, topo - 0.34, 0.05, 0.05)
    n = max(1, int(round(L / passo)))
    for i in range(n):
        s = (i + 0.5) * L / n
        if any(de - 0.6 <= s <= ate + 0.6 for de, ate in evitar): continue
        F.pilastra(mat, x0 + ux * s, y0 + uy * s, olha, 0.95, fundo, base, topo - 0.32, afina=0.35)
        F.pilastra(ESCURO, x0 + ux * s, y0 + uy * s, olha, 1.1, fundo + 0.06, base, base + 0.55, afina=0.8)


def vidraca(F, x0, y0, x1, y1, olha, base, topo, passo=1.5, vidro="sz_vidro_azul", travessa=True, salto=0.05):
    """Um pano de vidro com o caixilho: ao longo da face de (x0, y0) a (x1, y1), de `base` a `topo`. O vidro é o do
    jogo (reflete o céu: `superficie.gdshader`, material vidro); o caixilho é de aço escuro, com um montante a cada
    `passo` m e, com `travessa`, uma travessa a meia altura."""
    a = math.radians(olha); fx, fy = math.sin(a), math.cos(a)
    L = math.hypot(x1 - x0, y1 - y0); ux, uy = (x1 - x0) / L, (y1 - y0) / L
    def pt(s, f, h): return W(x0 + ux * s + fx * f, y0 + uy * s + fy * f, h)
    F.tronco(vidro, [pt(0, 0, base), pt(L, 0, base), pt(L, salto, base), pt(0, salto, base)], [pt(0, 0, topo), pt(L, 0, topo), pt(L, salto, topo), pt(0, salto, topo)])
    g = 0.09
    for h in ((base, topo, (base + topo) / 2) if travessa else (base, topo)):
        F.viga(ESCURO, pt(-g / 2, salto + 0.02, h), pt(L + g / 2, salto + 0.02, h), g, g + 0.02)
    n = max(1, int(round(L / passo)))
    for i in range(n + 1):
        F.viga(ESCURO, pt(L * i / n, salto + 0.02, base), pt(L * i / n, salto + 0.02, topo), g)


def respiro(F, x, y, olha, base, largura=1.3, altura=1.0):
    """Um respiro de parede: a moldura e as palhetas inclinadas (a veneziana de uma casa de máquinas)."""
    a = math.radians(olha); fx, fy = math.sin(a), math.cos(a); lx, ly = fy, -fx
    def pt(s, f, h): return W(x + lx * s + fx * f, y + ly * s + fy * f, h)
    F.tronco(ESCURO, [pt(-largura / 2, 0, base), pt(largura / 2, 0, base), pt(largura / 2, 0.07, base), pt(-largura / 2, 0.07, base)],
             [pt(-largura / 2, 0, base + altura), pt(largura / 2, 0, base + altura), pt(largura / 2, 0.07, base + altura), pt(-largura / 2, 0.07, base + altura)])
    n = max(3, int(altura / 0.17))
    for k in range(n):
        h = base + 0.1 + (altura - 0.2) * k / (n - 1)
        F.tronco("sz_metal_cru", [pt(-largura / 2 + 0.08, 0.07, h - 0.02), pt(largura / 2 - 0.08, 0.07, h - 0.02), pt(largura / 2 - 0.08, 0.15, h - 0.08), pt(-largura / 2 + 0.08, 0.15, h - 0.08)],
                 [pt(-largura / 2 + 0.08, 0.07, h + 0.01), pt(largura / 2 - 0.08, 0.07, h + 0.01), pt(largura / 2 - 0.08, 0.15, h - 0.05), pt(-largura / 2 + 0.08, 0.15, h - 0.05)])


def tubulacao(F, x0, y0, x1, y1, olha, altura, quantos=2, cor="sz_metal_cru_claro"):
    """Canos ao longo de uma parede, nos suportes: de (x0, y0) a (x1, y1), a `altura`, saindo 20 cm da face."""
    a = math.radians(olha); fx, fy = math.sin(a), math.cos(a)
    L = math.hypot(x1 - x0, y1 - y0)
    for k in range(quantos):
        h = altura + k * 0.22; f = 0.2 + 0.04 * k
        F.viga(cor if k == 0 else "sz_metal_pintado_laranja", W(x0 + fx * f, y0 + fy * f, h), W(x1 + fx * f, y1 + fy * f, h), 0.11)
    n = max(2, int(L / 3.0))
    for i in range(n + 1):
        px, py = x0 + (x1 - x0) * i / n, y0 + (y1 - y0) * i / n
        F.viga(ESCURO, W(px, py, altura - 0.12), W(px + fx * 0.3, py + fy * 0.3, altura - 0.12), 0.06, 0.05)
        F.viga(ESCURO, W(px + fx * 0.3, py + fy * 0.3, altura - 0.14), W(px + fx * 0.3, py + fy * 0.3, altura + 0.22 * quantos), 0.05)


def faixa_de_cor(F, x0, y0, x1, y1, olha, base, de=0.7, ate=1.7, cor="sz_metal_pintado_laranja"):
    """A faixa de cor pintada ao longo de uma parede (o lugar se lê pela cor)."""
    a = math.radians(olha); fx, fy = math.sin(a), math.cos(a)
    F.tronco(cor, [W(x0, y0, base + de), W(x1, y1, base + de), W(x1 + fx * 0.025, y1 + fy * 0.025, base + de), W(x0 + fx * 0.025, y0 + fy * 0.025, base + de)],
             [W(x0, y0, base + ate), W(x1, y1, base + ate), W(x1 + fx * 0.025, y1 + fy * 0.025, base + ate), W(x0 + fx * 0.025, y0 + fy * 0.025, base + ate)])


def caixas(M, x, y, base=0.0, quantas=3, giro=0.0):
    """Uma pilha de caixas de carga (a caixa de metal do kit), fora do caminho: no canto, ao pé de um muro."""
    lugares = [(0.0, 0.0, 0.0), (0.95, 0.1, 0.0), (0.45, 0.05, 0.8), (-0.9, 0.3, 0.0), (1.9, 0.0, 0.0)][:quantas]
    a = math.radians(giro)
    for k, (dx, dy, dh) in enumerate(lugares):
        px, py = x + dx * math.cos(a) - dy * math.sin(a), y + dx * math.sin(a) + dy * math.cos(a)
        por(M, ("SM_Props_MetalBox_01", "SM_Props_MetalBox_02", "SM_Props_MetalBox_03")[k % 3], px, py, giro + 12.0 * sorte("cx", x, k), base=base + dh, colide="caixa" if dh == 0.0 else None)


def detalhes(m, M, V, F):
    """O miúdo que dá vida às paredes (as referências do Jhon, 26/11: respiros, tubulação nos suportes, a faixa de
    cor, caixas de carga ao pé do muro)."""
    n = SZ.N_FOGO
    # ---- a faixa de cor de cada lugar
    faixa_de_cor(F, 45.0, 19.0, 45.0, 34.0, LESTE, n); faixa_de_cor(F, 105.0, 34.0, 105.0, -2.4, OESTE, n)
    faixa_de_cor(F, 105.4, 4.0, 146.0, 4.0, SUL, 0.0); faixa_de_cor(F, 146.0, 35.6, 115.0, 35.6, NORTE, 0.0)
    faixa_de_cor(F, 40.0, 85.0, 40.0, 94.0, LESTE, 0.0, cor="sz_lataria_azul"); faixa_de_cor(F, 120.0, 94.0, 120.0, 64.0, OESTE, 0.0, 1.35, 2.35, cor="sz_lataria_azul")
    # ---- a tubulação
    tubulacao(F, 105.0, 30.0, 105.0, 2.0, OESTE, n + 4.6)
    tubulacao(F, 106.0, 4.0, 145.0, 4.0, SUL, 5.0)
    tubulacao(F, 146.0, 6.0, 146.0, 24.5, OESTE, 5.0)
    tubulacao(F, 120.0, 66.0, 120.0, 92.0, OESTE, 3.3, cor="sz_metal_cru_claro")
    # ---- os respiros
    for y in (6.0, 12.0, 26.0):
        respiro(F, 105.0, y, OESTE, n + 2.4)
    for x in (110.5, 118.0, 134.0, 141.5):
        respiro(F, x, 4.0, SUL, 2.5)
    for x in (115.0, 121.0, 131.0, 137.0):
        respiro(F, x, 10.0, NORTE, 1.0, 1.1, 0.8)
    for x, y in ((112.0, 12.0), (112.0, 16.0)):
        respiro(F, x, y, OESTE, 1.0, 1.1, 0.8)
    for y in (68.5, 77.0, 86.5):
        respiro(F, 120.0, y, OESTE, 4.2, 1.5, 1.1)
    # ---- as caixas de carga, nos cantos
    caixas(M, 46.2, 30.6, n, 3, 0.0); caixas(M, 102.6, 26.6, n, 4, 180.0); caixas(M, 103.2, 0.4, n, 2, 90.0)
    caixas(M, 107.0, 34.4, 0.0, 3, 0.0); caixas(M, 143.4, 5.2, 0.0, 4, 180.0); caixas(M, 144.6, 24.0, 0.0, 2, 90.0)
    caixas(M, 116.8, 93.8, 0.0, 3, 200.0); caixas(M, 41.4, 76.4, 0.0, 2, 90.0)
    caixas(M, 27.6, 94.2, 0.0, 3, 180.0); caixas(M, 5.6, 71.6, 0.0, 2, 0.0)


def linhas_guia(m, M, V, F):
    """As linhas de energia no piso (Jhon, 26/11: "linhas luminosas nos caminhos do mapa de treinamento para mostrar
    os setores disponíveis para treino, uma energia LED azul"): do Núcleo saem quatro, uma para cada porta, e cada
    uma segue pelo caminho — desce a escadaria, sobe a da Câmara — até a entrada do setor, onde acaba em setas. Duas
    linhas finas lado a lado, 2 cm acima do piso, acompanhando a subida das escadas."""
    nn, nf, nc = SZ.N_NUCLEO, SZ.N_FOGO, SZ.N_CAMARA
    def linha(pontos, meia=0.16):
        """`pontos`: [(x, y, altura do piso)]. Duas fitas de LED paralelas ao longo deles."""
        for (x0, y0, h0), (x1, y1, h1) in zip(pontos, pontos[1:]):
            L = math.hypot(x1 - x0, y1 - y0)
            if L < 0.01: continue
            lx, ly = -(y1 - y0) / L, (x1 - x0) / L
            for s in (-1, 1):
                F.viga("sz_led_azul", W(x0 + lx * s * meia, y0 + ly * s * meia, h0 + 0.022), W(x1 + lx * s * meia, y1 + ly * s * meia, h1 + 0.022), 0.07, 0.018)
    def setas(nome, x, y, para, nivel=0.0):
        """Três setas no fim da linha, apontando para `para` ("N", "S", "L", "O")."""
        pintura(m, "Guia" + nome, "guia", x, y, 1.9, {"L": "N", "S": "L", "O": "S", "N": "O"}[para], aceso=0.9, nivel=nivel)
    # da Sala de Ativação ao Núcleo, pelo corredor (à direita do meio: o meio é dos obstáculos e das marcas)
    linha([(20.6, 51.7, 0.0), (SZ.VAO_X0 - 0.3, 51.7, 0.0)]); linha([(SZ.VAO_X0 + SZ.VAO_HUMANO + 0.3, 51.7, 0.0), (62.4, 51.7, 0.0)])
    setas("Nucleo", 60.6, 50.9, "L")
    # do meio do Núcleo às quatro portas: o piso do anfiteatro, os degraus e a beira
    for (dx, dy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        linha([(75 + dx * 2.6, 50 + dy * 2.6, nn), (75 + dx * 9.2, 50 + dy * 9.2, nn), (75 + dx * 12.0, 50 + dy * 12.0, 0.0), (75 + dx * 12.9, 50 + dy * 12.9, 0.0)])
    # norte: a passagem, a escadaria e a Linha de Fogo
    linha([(75.0, 37.1, 0.0), (75.0, 34.0, 0.0), (75.0, 28.6, nf), (75.0, 26.6, nf)])
    setas("LinhaDeFogo", 75.0, 27.6, "N", nf)
    # leste: o salão, a escadaria e a rotunda
    linha([(87.9, 50.0, 0.0), (97.95, 50.0, 0.0), (102.7, 50.0, nc), (108.6, 50.0, nc)])
    setas("Camara", 96.4, 50.0, "L")
    # da Câmara para a arena (sul) e para a Rua Ignição (norte)
    linha([(112.0, 53.4, nc), (112.0, 59.3, nc), (112.0, 63.5, 0.0), (112.0, 67.6, 0.0)]); setas("Arena", 112.0, 66.8, "S")
    linha([(112.0, 46.6, nc), (112.0, 40.7, nc), (112.0, 36.5, 0.0), (112.0, 32.6, 0.0)]); setas("Rua", 112.0, 33.4, "N")
    # sul: do Núcleo à arena
    linha([(75.0, 62.9, 0.0), (75.0, 67.6, 0.0)]); setas("ArenaNucleo", 75.0, 66.8, "S")
    # da arena ao Ponto de Retorno, pelo corredor oeste
    linha([(43.6, 82.5, 0.0), (28.6, 82.5, 0.0)]); setas("Retorno", 29.6, 82.5, "O")
    # e a galeria, o terraço: uma linha curta ao pé de cada escada que leva a um ponto alto
    linha([(41.2, 77.4, 0.0), (41.2, 74.4, 0.0)]); linha([(106.2, 77.4, 0.0), (106.2, 74.4, 0.0)]); linha([(127.0, 9.2, 0.0), (124.2, 9.2, 0.0)])


def arquitetura(m, M, V, F):
    """O projeto das construções (Jhon, 26/11: "o mapa em si está muito quadrado; faltam projetos arquitetônicos,
    janelas com reflexo, esculturas"): as paredes vestidas (soco, pilastras em talude, friso com LED), os panos de
    vidro com caixilho, o telhado em dente de serra da Sala de Ativação, a cabine de controle sobre a Linha de Fogo,
    o camarote envidraçado da arena — e as esculturas. Nada disto colide (o que barra continua sendo o que barrava)."""
    n = SZ.N_FOGO
    # ---- as paredes vestidas
    revestir(F, 45.0, -2.4, 45.0, 34.0, LESTE, n, 3.6, 7.0, "sz_led_laranja", evitar=[(9.0, 18.6)])
    revestir(F, 105.0, 34.0, 105.0, -2.4, OESTE, n, 6.0, 7.0, "sz_led_laranja")
    revestir(F, 72.0, 34.0, 45.0, 34.0, NORTE, n, 3.6, 6.5, None, evitar=[(8.0, 19.5)])
    revestir(F, 105.0, 34.0, 78.0, 34.0, NORTE, n, 3.6, 6.5, None, evitar=[(8.0, 19.5)])
    revestir(F, 105.4, 4.0, 146.0, 4.0, SUL, 0.0, 6.0, 7.0, "sz_led_laranja", evitar=[(15.0, 26.4)])
    revestir(F, 146.0, 4.0, 146.0, 35.6, OESTE, 0.0, 6.0, 7.0, "sz_led_laranja", evitar=[(21.0, 31.0)])
    revestir(F, 146.0, 35.6, 115.0, 35.6, NORTE, 0.0, 6.0, 7.0, None)
    revestir(F, 105.4, 36.0, 105.4, 4.0, LESTE, 0.0, 6.0, 7.0, None, evitar=[(0.0, 2.0)])
    revestir(F, 87.6, 47.0, 97.9, 47.0, SUL, 0.0, 4.5, 5.2, None, evitar=[(1.5, 9.0)], fundo=0.2)
    revestir(F, 97.9, 53.0, 87.6, 53.0, NORTE, 0.0, 4.5, 5.2, None, fundo=0.2)
    revestir(F, 4.4, 70.4, 4.4, 95.6, LESTE, 0.0, 5.0, 6.3, None, evitar=[(14.0, 21.0)], fundo=0.22)          # Ponto de Retorno, por dentro
    revestir(F, 30.0, 70.4, 4.4, 70.4, SUL, 0.0, 5.0, 6.4, None, evitar=[(8.0, 18.0)], fundo=0.22)
    revestir(F, 19.6, 61.6, 4.4, 61.6, NORTE, 0.0, 5.0, 5.2, None, fundo=0.22)                                  # Sala de Ativação
    # a parede norte da arena, acima da galeria: nervuras altas, e as vigas do teto das duas salas
    for x in range(48, 104, 8):
        if 82 <= x <= 100: continue
        F.pilastra("sz_concreto", x + 4.0, 64.0, SUL, 0.9, 0.42, 5.2, 13.6, afina=0.3)
    for y in (42.0, 46.0, 50.0, 54.0, 58.0):
        F.viga(ESCURO, W(4.4, y, 4.82), W(19.6, y, 4.82), 0.22, 0.36)
    for y in (74.0, 79.0, 87.0, 92.0):
        F.viga(ESCURO, W(4.4, y, 4.82), W(29.6, y, 4.82), 0.22, 0.36)

    # ---- os panos de vidro
    for x0, x1 in ((113.2, 117.3), (118.7, 122.8), (129.2, 133.3), (134.7, 138.8)):          # os prédios da Rua Ignição: o andar de cima
        vidraca(F, x0, 18.0, x1, 18.0, SUL, 3.3, 5.4, 1.37)
    for x0, x1 in ((113.2, 116.6), (119.0, 122.8), (129.2, 132.6), (134.8, 138.8)):          # e a vitrine do térreo
        vidraca(F, x0, 18.0, x1, 18.0, SUL, 0.7, 2.4, 1.7, vidro="sz_vidro", travessa=False)
    vidraca(F, 140.0, 17.4, 140.0, 10.6, LESTE, 3.3, 5.4, 1.7); vidraca(F, 112.0, 10.6, 112.0, 17.4, OESTE, 3.3, 5.4, 1.7)
    vidraca(F, 128.6, 10.0, 139.4, 10.0, NORTE, 3.3, 5.4, 1.8)
    vidraca(F, 19.6, 46.4, 19.6, 39.0, OESTE, 3.1, 4.4, 1.85, travessa=False); vidraca(F, 19.6, 61.0, 19.6, 53.6, OESTE, 3.1, 4.4, 1.85, travessa=False)
    vidraca(F, 29.6, 79.4, 29.6, 71.0, OESTE, 2.6, 4.3, 2.1, travessa=False); vidraca(F, 29.6, 95.0, 29.6, 85.6, OESTE, 2.6, 4.3, 2.35, travessa=False)
    vidraca(F, 29.0, 95.6, 15.0, 95.6, NORTE, 1.2, 4.2, 2.0)
    vidraca(F, 54.5, 41.0, 54.5, 37.0, LESTE, 1.0, 2.6, 1.0, vidro="sz_vidro", travessa=False)          # o Posto Médico
    # ---- as janelas do corredor e da sala: o caixilho (o vão é aberto: o vidro do jogo não deixa ver através)
    for y, olha in ((47.0, SUL), (53.0, NORTE)):
        for a_, b_ in SZ.JANELAS["corredor_norte"]:
            for h in (SZ.PEITORIL, SZ.VERGA):
                F.viga(ESCURO, W(a_ - 0.1, y, h), W(b_ + 0.1, y, h), 0.16, 0.14)
            k = max(2, int(round((b_ - a_) / 2.0)))
            for i in range(k + 1):
                F.viga(ESCURO, W(a_ + (b_ - a_) * i / k, y + (0.2 if olha == NORTE else -0.2), SZ.PEITORIL), W(a_ + (b_ - a_) * i / k, y + (0.2 if olha == NORTE else -0.2), SZ.VERGA), 0.1)
    for i in range(6):
        F.viga(ESCURO, W(7.0 + 2.0 * i, 38.2, SZ.PEITORIL), W(7.0 + 2.0 * i, 38.2, SZ.VERGA), 0.1)
    for h in (SZ.PEITORIL, SZ.VERGA):
        F.viga(ESCURO, W(6.9, 38.4, h), W(17.1, 38.4, h), 0.16, 0.14)

    # ---- o telhado da Sala de Ativação: quatro dentes de serra, com a claraboia de cada um virada para o norte
    for k in range(4):
        y0 = 38.0 + 6.0 * k
        F.tronco("sz_metal_pintado", [W(4.1, y0, 5.3), W(19.9, y0, 5.3), W(19.9, y0 + 6.0, 5.3), W(4.1, y0 + 6.0, 5.3)],
                 [W(4.1, y0, 7.2), W(19.9, y0, 7.2), W(19.9, y0 + 6.0, 5.36), W(4.1, y0 + 6.0, 5.36)])
        vidraca(F, 19.4, y0, 4.6, y0, NORTE, 5.55, 7.0, 1.85, travessa=False, salto=0.04)
        F.viga(ESCURO, W(4.0, y0, 7.24), W(20.0, y0, 7.24), 0.3, 0.12)

    # ---- a cabine de controle da Linha de Fogo: uma caixa envidraçada sobre o muro sul, a oeste da escadaria
    F.caixa("sz_metal_pintado", 60.6, 32.3, 70.4, 35.5, 3.9, 6.5, chanfro=0.35)
    F.tronco(ESCURO, [W(60.2, 31.9, 6.5), W(70.8, 31.9, 6.5), W(70.8, 35.9, 6.5), W(60.2, 35.9, 6.5)],
             [W(60.2, 31.9, 6.62), W(70.8, 31.9, 6.62), W(70.8, 35.9, 6.95), W(60.2, 35.9, 6.95)])
    vidraca(F, 70.0, 32.3, 61.0, 32.3, NORTE, 4.5, 6.1, 1.5, travessa=False)
    F.viga("sz_led_laranja", W(60.8, 32.26, 4.3), W(70.2, 32.26, 4.3), 0.05, 0.08)
    for x in (61.4, 69.6):
        for y in (32.9, 35.0):
            F.viga(ESCURO, W(x, y, 3.6 if y < 34.5 else 0.0), W(x, y, 3.9), 0.2)
        F.viga(ESCURO, W(x, 33.6, 3.9), W(x, 31.0, 3.25), 0.1)
    por(M, "SM_Buildings_Part_30", 68.6, 34.2, 20.0, base=6.8, escala=1.2, planta=False)

    # ---- o camarote da arena: uma caixa de vidro em balanço na parede norte, acima da galeria
    F.caixa(ESCURO, 84.0, 64.0, 98.0, 66.5, 8.3, 8.6, chanfro=0.3)
    F.caixa(ESCURO, 84.0, 64.0, 98.0, 66.6, 11.4, 11.75, chanfro=0.3)
    vidraca(F, 84.3, 66.4, 97.7, 66.4, SUL, 8.6, 11.4, 1.68)
    vidraca(F, 84.1, 64.1, 84.1, 66.3, OESTE, 8.6, 11.4, 1.1, travessa=False); vidraca(F, 97.9, 66.3, 97.9, 64.1, LESTE, 8.6, 11.4, 1.1, travessa=False)
    F.viga("sz_led_azul", W(84.2, 66.56, 8.36), W(97.8, 66.56, 8.36), 0.05, 0.1)
    for x in (85.0, 91.0, 97.0):
        F.viga(ESCURO, W(x, 64.05, 6.7), W(x, 66.3, 8.3), 0.14, 0.2)

    # ---- as esculturas
    # os anéis do jardim: três aros de aço em volta de um miolo de luz, sobre um disco de pedra
    c = W(33.0, 29.4, 2.3)
    F.cilindro("sz_concreto_claro", 33.0, 29.4, 1.6, 0.0, 0.3, 16, r_topo=1.4)
    for eixo, r_ in (((1.0, 0.35, 0.2), 1.9), ((0.2, 0.3, 1.0), 1.6), ((0.5, 1.0, -0.4), 1.3)):
        F.aro("sz_metal_cru_claro", c, r_, eixo, 0.13)
    F.cilindro("sz_led_ciano", 33.0, 29.4, 0.03, 1.95, 2.3, 4, r_topo=0.3); F.cilindro("sz_led_ciano", 33.0, 29.4, 0.3, 2.3, 2.65, 4, r_topo=0.03)
    F.viga(ESCURO, W(33.0, 29.4, 0.3), W(33.0, 29.4, 1.95), 0.08)
    # as lâminas: cinco pedras altas e finas, em leque, no canteiro ao sul do corredor (veem-se pelas janelas)
    for k, (dx, h) in enumerate(((-2.4, 3.2), (-1.2, 4.6), (0.0, 5.8), (1.3, 4.2), (2.5, 2.8))):
        x, y = 25.5 + dx, 56.6 + 0.35 * abs(dx)
        F.prisma("sz_rocha_escura" if k % 2 else "sz_rocha", [(x - 0.42, y - 0.3), (x + 0.42, y - 0.16), (x + 0.3, y + 0.3), (x - 0.36, y + 0.2)], 0.0, h, encolhe=0.72)
    F.caixa("sz_concreto_claro", 22.4, 55.9, 28.6, 58.0, 0.0, 0.18, chanfro=0.5)
    # a esfera do café: aros em volta de um eixo, como um globo
    c = W(100.2, 40.6, 1.9)
    for k in range(4):
        a_ = math.pi * k / 4
        F.aro("sz_metal_cru_claro", c, 1.25, (math.cos(a_), 0.0, math.sin(a_)), 0.08, 24)
    F.aro("sz_metal_cru_claro", c, 1.25, (0.0, 1.0, 0.0), 0.08, 24)
    F.cilindro("sz_concreto_claro", 100.2, 40.6, 0.7, 0.0, 0.65, 12, r_topo=0.45)
    # a fonte do jardim (a do kit)
    por(V, "Fountain_01", 11.6, 23.4, 0.0, planta=False)


# ------------------------------------------------------------------ a montanha

def montanha(m, M, V, F):
    """O cume: o campo é o topo de uma rocha cinza enorme, no meio de uma cordilheira gelada. Sem colisão: da beira
    para fora só se cai.

    Tudo aqui é **relevo**, não peça: grades de vértices deslocadas por ruído (`setor_zero_formas.Ruido`) e pintadas
    face a face. A primeira versão (26/11) eram cones de nove lados e placas grandes; o Jhon: "picos muito pontudos,
    textura mal feita, muito quadrados". O que mudou:
      - **a cordilheira** é um terreno só, em anel em volta do campo (de 250 a 1.000 m), com o ruído em cristas — serras
        contínuas, cumes arredondados, vales e selas; malha de ~17 m, luz lisa de face a face;
      - **a neve** é por altitude e inclinação (acima da linha de neve, no que não é a pique), com a rocha aparecendo
        nas faces íngremes; a rocha tem tons em estratos;
      - **nada do que fica longe leva o grão do material** (as cores `sz_liso_…`): a centenas de metros o grão vira um
        reticulado na tela — era a "textura mal feita";
      - **a base** (a encosta debaixo do campo) é um penhasco: malha de 4 m, sulcos de cima a baixo, estratos, dois
        patamares, e neve no que é quase plano; a beira em volta do campo é irregular, com pedras e neve;
      - lá embaixo não há chão: há nuvens (`nuvens`) e a névoa que adensa com a fundura."""
    import numpy as np
    from .setor_zero_formas import Ruido
    cx, cy = SZ.CX, SZ.CY
    R1, R2, R3 = Ruido(11), Ruido(23), Ruido(37)

    # ---------------- a cordilheira: um anel de terreno em volta do campo
    na, nr = 200, 44
    ang = np.linspace(0.0, 2 * np.pi, na, endpoint=False)[:, None]
    raio = (250.0 + (1000.0 - 250.0) * (np.linspace(0.0, 1.0, nr) ** 1.15))[None, :]
    X = raio * np.cos(ang); Z = raio * np.sin(ang)
    def degrau(a, b, v): t_ = np.clip((v - a) / (b - a), 0.0, 1.0); return t_ * t_ * (3 - 2 * t_)
    sobe = degrau(270.0, 520.0, raio)                                    # do mar de nuvens até a primeira serra
    macico = degrau(0.34, 0.62, R2.fractal(X / 560.0 + 11.0, Z / 560.0 + 5.0, 3))          # onde há serra e onde há vale
    serra = R1.cristas(X / 360.0, Z / 360.0, 5, 0.52)                    # as cumeadas
    miudo = R3.cristas(X / 95.0 + 3.0, Z / 95.0 + 8.0, 3)
    alto = -300.0 + sobe * (150.0 + macico * (470.0 * serra ** 1.25 + 60.0 * miudo * serra) + 55.0 * miudo * (1 - macico)
                            + 150.0 * degrau(680.0, 1000.0, raio))       # a serra de trás é mais alta: fecha o horizonte
    P_ = np.stack([np.broadcast_to(X, alto.shape), alto, np.broadcast_to(Z, alto.shape)], axis=2)
    linha_de_neve = lambda mx, mz: 55.0 + 85.0 * (R2.fractal(mx / 210.0, mz / 210.0, 3) - 0.5)
    def pinta_serra(meio, n):
        z, ingreme = meio[:, 1], 1.0 - n[:, 1]
        linha = linha_de_neve(meio[:, 0], meio[:, 2])
        tom = R3.fractal(meio[:, 0] / 70.0, (meio[:, 1] * 3.0 + meio[:, 2]) / 70.0, 2)          # estratos tortos
        nomes = np.where(tom < 0.42, "sz_liso_rocha_escura", np.where(tom < 0.62, "sz_liso_rocha", "sz_liso_rocha_azul")).astype(object)
        nomes[(ingreme > 0.5) & (tom > 0.55)] = "sz_liso_rocha_clara"
        neve = ((z > linha) & (ingreme < 0.46)) | ((z > linha + 70.0) & (ingreme < 0.62)) | (z > linha + 150.0)
        nomes[neve] = "sz_liso_neve"
        nomes[neve & (ingreme > 0.34)] = "sz_liso_neve_sombra"
        nomes[z < -150.0] = "sz_liso_rocha_escura"
        return nomes
    F.grade(P_, pinta_serra, suave=True, fecha=True)

    # ---------------- a base: o penhasco debaixo do campo e a beira em volta dele
    a0, a1 = SZ.BEIRA
    amostras = []          # (x, y, aberto) em volta do campo, a cada ~4 m
    def lado(ax, ay, bx, by, aberto=False, passo=4.0):
        L = math.hypot(bx - ax, by - ay); k = max(1, int(round(L / passo)))
        for i_ in range(k):
            t_ = i_ / k; amostras.append((ax + (bx - ax) * t_, ay + (by - ay) * t_, aberto))
    lado(-3.6, -3.6, a0 - 0.2, -3.6); amostras.append((a0 - 0.2, -3.6, False))
    lado(a0, -3.0, a1, -3.0, True); amostras.append((a1, -3.0, True))
    lado(a1 + 0.2, -3.6, 153.6, -3.6); lado(153.6, -3.6, 153.6, 103.6); lado(153.6, 103.6, -3.6, 103.6); lado(-3.6, 103.6, -3.6, -3.6)
    Q = np.array([(x, y) for x, y, _ in amostras]); aberto = np.array([ab for _, _, ab in amostras])
    n_ = len(Q)
    # para fora: do retângulo encolhido 28 m (as quinas saem redondas)
    dentro = np.stack([np.clip(Q[:, 0], 25.0, 125.0), np.clip(Q[:, 1], 25.0, 75.0)], axis=1)
    fora = Q - dentro; fora /= np.linalg.norm(fora, axis=1, keepdims=True)
    u = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.roll(Q, -1, axis=0) - Q, axis=1))[:-1]])          # o comprimento ao longo da beira
    # a beira: quanto a rocha sai para fora do campo, em cima (na Linha de Fogo, nada: o campo acaba no ar)
    beira = np.where(aberto, 0.16, 2.4 + 8.5 * R2.fractal(u / 26.0, 0.3 + 0 * u, 3) ** 1.3)
    topo = np.where(aberto, SZ.N_FOGO - 0.06, -0.03)
    fundos = np.array([0.0, -2.5, -6.0, -11.0, -18.0, -27.0, -38.0, -52.0, -70.0, -92.0, -120.0, -155.0, -200.0, -260.0])
    U, D = np.meshgrid(u, fundos, indexing="ij")
    sulco = R1.cristas(U / 11.0, D / 85.0 + 2.0, 3)                       # sulcos de cima a baixo
    bojo = R3.fractal(U / 34.0, D / 30.0, 3)
    desce = np.clip(-D / 14.0, 0.0, 1.0)                                  # no alto a rocha acompanha a beira; embaixo, solta
    # dois patamares (onde a neve pousa), cada um mais alto ou mais baixo conforme o lugar, e que some em alguns trechos
    onda1, onda2 = 9.0 * (R2.fractal(U / 60.0, 0.7 + 0 * U, 2) - 0.5), 16.0 * (R3.fractal(U / 85.0, 1.9 + 0 * U, 2) - 0.5)
    tem1, tem2 = degrau(0.38, 0.5, R1.fractal(U / 45.0, 5.0 + 0 * U, 2)), degrau(0.36, 0.5, R2.fractal(U / 70.0, 9.0 + 0 * U, 2))
    patamar = 5.5 * tem1 * degrau(26.0, 31.0, -D + onda1) + 9.0 * tem2 * degrau(84.0, 92.0, -D + onda2)
    sai = beira[:, None] + 0.24 * (-D) + 0.0011 * D * D + patamar + desce * (9.0 * (bojo - 0.45) - 6.5 * sulco * np.clip(-D / 30.0, 0.0, 1.0))
    PX = Q[:, 0][:, None] + fora[:, 0][:, None] * sai; PY = Q[:, 1][:, None] + fora[:, 1][:, None] * sai
    PH = np.where(D == 0.0, topo[:, None], D + 1.6 * (bojo - 0.5) * desce)
    penhasco = np.stack([PX - cx, PH, PY - cy], axis=2)
    def pinta_penhasco(meio, n):
        z, cima = meio[:, 1], n[:, 1]
        uu = np.arctan2(meio[:, 2], meio[:, 0]) * 60.0
        tom = (np.floor((z + 7.0 * R2.fractal(uu / 20.0, z / 22.0, 2)) / 9.0).astype(int) % 3)
        perto = z > -45.0
        nomes = np.where(tom == 0, "rocha_escura", np.where(tom == 1, "rocha", "rocha_clara")).astype(object)
        nomes = np.where(perto, "sz_", "sz_liso_") + nomes
        neve = (cima > 0.5) | ((cima > 0.3) & (R3.fractal(meio[:, 0] / 9.0, meio[:, 2] / 9.0, 2) > 0.55) & (z > -110.0))
        nomes[neve] = np.where(perto[neve], "sz_areia_neve", "sz_liso_neve")
        return nomes
    F.grade(penhasco, pinta_penhasco, suave=False, fecha=True, fora=lambda meio: meio * [1.0, 0.0, 1.0])
    # a beira em cima: do parapeito ao alto do penhasco, em quatro faixas, com a rocha subindo um pouco para fora
    faixas = np.linspace(0.0, 1.0, 5)
    BU, BT = np.meshgrid(u, faixas, indexing="ij")
    larga = beira[:, None] * BT
    BXp = Q[:, 0][:, None] + fora[:, 0][:, None] * larga; BYp = Q[:, 1][:, None] + fora[:, 1][:, None] * larga
    relevo = np.where(aberto[:, None], 0.0, (0.75 * R3.fractal(BXp / 3.4, BYp / 3.4, 3) ** 1.5 + 0.25 * R1.fractal(BXp / 9.0, BYp / 9.0, 2)) * np.sin(BT * np.pi) * np.clip(beira[:, None] / 6.0, 0.25, 1.0))
    BH = topo[:, None] + 1.25 * relevo
    def pinta_beira(meio, n):
        s = R2.fractal((meio[:, 0] + cx) / 7.0, (meio[:, 2] + cy) / 7.0, 3)
        nomes = np.where(s > 0.5, "sz_areia_neve", np.where(s > 0.4, "sz_rocha_clara", "sz_rocha")).astype(object)
        nomes[n[:, 1] < 0.72] = "sz_rocha_escura"
        return nomes
    F.grade(np.stack([BXp - cx, BH, BYp - cy], axis=2), pinta_beira, suave=False, fecha=True)
    # as pedras soltas da beira (as do kit)
    for i_ in range(0, n_, 5):
        if aberto[i_] or beira[i_] < 5.0: continue
        x = Q[i_, 0] + fora[i_, 0] * beira[i_] * (0.35 + 0.4 * sorte("pb", i_)); y = Q[i_, 1] + fora[i_, 1] * beira[i_] * (0.35 + 0.4 * sorte("pb", i_))
        rocha(V, x, y, 0.9 + 0.6 * sorte("pr", i_), base=0.1)
    # o horizonte pintado, bem atrás da cordilheira
    m.panorama("picos", 1500.0, altura=640.0, base=-250.0, repete=2)


def nuvens(m):
    """As nuvens (`src/assets/mapas/detalhe/nuvens.gdshader`: planos grandes em que a nuvem é desenhada por ruído,
    translúcida, andando com o vento). Abaixo do campo, quatro camadas empilhadas são o mar de nuvens do precipício
    (Jhon, 26/11: "o precipício deve ter aparência de nuvem, não algo sólido") — a de baixo quase fechada, as de cima
    em farrapos; e acima, duas são as nuvens do céu ("faltam nuvens no céu também")."""
    from .kit import xform
    m.cru_ext.append(("Shader", "res://src/assets/mapas/detalhe/nuvens.gdshader", "s_nuvens"))
    # (nome, altura, lado do plano, tamanho do desenho, cobertura, maciez, opacidade, vento, cor, sombra, some de/até)
    camadas = [
        ("NuvensFundo", -235.0, 9000.0, 520.0, 0.97, 0.30, 1.00, (2.0, 0.8), (0.95, 0.97, 1.0), (0.80, 0.86, 0.96), (3000.0, 4200.0)),
        ("NuvensMar", -150.0, 9000.0, 430.0, 0.80, 0.26, 0.96, (3.2, 1.3), (1.0, 1.0, 1.0), (0.78, 0.85, 0.96), (3000.0, 4200.0)),
        ("NuvensMeio", -95.0, 8000.0, 360.0, 0.56, 0.24, 0.85, (4.4, 1.9), (1.0, 1.0, 1.0), (0.80, 0.87, 0.97), (2600.0, 3800.0)),
        ("NuvensFarrapos", -42.0, 7000.0, 290.0, 0.30, 0.22, 0.62, (5.6, 2.6), (1.0, 1.0, 1.0), (0.84, 0.90, 0.98), (2200.0, 3400.0)),
        ("NuvensCeu", 620.0, 9000.0, 700.0, 0.40, 0.20, 0.90, (6.0, 2.0), (1.0, 1.0, 1.0), (0.76, 0.83, 0.95), (2400.0, 3800.0)),
        ("NuvensAltas", 1050.0, 9000.0, 1300.0, 0.34, 0.30, 0.55, (9.0, 3.5), (1.0, 1.0, 1.0), (0.86, 0.91, 0.99), (2400.0, 3800.0)),
    ]
    c3 = lambda c: f"Color({c[0]}, {c[1]}, {c[2]}, 1)"
    m.cru_nos += ['[node name="Nuvens" type="Node3D" parent="."]', ""]
    for k, (nome, altura, lado, tam, cob, mac, opa, vento, cor, sombra, some) in enumerate(camadas):
        m.cru_recursos += [f'[sub_resource type="ShaderMaterial" id="NuvemMat{k}"]', 'shader = ExtResource("s_nuvens")',
                           f"shader_parameter/cor = {c3(cor)}", f"shader_parameter/sombra = {c3(sombra)}",
                           f"shader_parameter/tamanho = {tam}", f"shader_parameter/cobertura = {cob}", f"shader_parameter/maciez = {mac}",
                           f"shader_parameter/opacidade = {opa}", f"shader_parameter/vento = Vector2({vento[0]}, {vento[1]})",
                           f"shader_parameter/some_de = {some[0]}", f"shader_parameter/some_ate = {some[1]}",
                           f"shader_parameter/semente = Vector2({3.7 * k}, {1.9 * k})", f"shader_parameter/encosto = {26.0 if altura < 0 else 60.0}",
                           f"shader_parameter/brilho = {2.0 if altura < 0 else 2.3}", "",
                           f'[sub_resource type="PlaneMesh" id="NuvemPlano{k}"]', f'material = SubResource("NuvemMat{k}")', f"size = Vector2({lado}, {lado})", ""]
        m.cru_nos += [f'[node name="{nome}" type="MeshInstance3D" parent="Nuvens"]', f"transform = {xform((0, altura, 0))}",
                      f'mesh = SubResource("NuvemPlano{k}")', "cast_shadow = 0", "extra_cull_margin = 16384.0", ""]


# ------------------------------------------------------------------ o piso trabalhado e o recheio (26/11)

def laje(F, mat, x0, y0, x1, y1, n=0.0, alto=0.005):
    """Uma placa fina por cima do piso (sem colisão: meio centímetro não se sente no pé)."""
    if x1 - x0 > 0.01 and y1 - y0 > 0.01:
        F.caixa(mat, x0, y0, x1, y1, n, n + alto)


def lajes(F, ret, n, lado, semente, tons, fora=(), junta=None):
    """Quebra um piso liso em placas de `lado` m: cada uma tem uma parte de chance (`tons` = [(material, parte)]) de
    sair noutro tom, e entre elas corre uma junta fina. `fora`: os retângulos onde não há piso (os vãos, a escada)."""
    x0, y0, x1, y1 = ret
    nx, ny = max(1, round((x1 - x0) / lado)), max(1, round((y1 - y0) / lado))
    dx, dy = (x1 - x0) / nx, (y1 - y0) / ny
    for i in range(nx):
        for j in range(ny):
            s = SZ.sorte(semente, i, j); mat = None; soma = 0.0
            for nome, parte in tons:
                soma += parte
                if s < soma: mat = nome; break
            if mat is None: continue
            for r in SZ.menos((x0 + i * dx + 0.03, y0 + j * dy + 0.03, x0 + (i + 1) * dx - 0.03, y0 + (j + 1) * dy - 0.03), list(fora)):
                laje(F, mat, *r, n)
    if junta:
        for i in range(1, nx):
            for r in SZ.menos((x0 + i * dx - 0.025, y0, x0 + i * dx + 0.025, y1), list(fora)): laje(F, junta, *r, n, 0.007)
        for j in range(1, ny):
            for r in SZ.menos((x0, y0 + j * dy - 0.025, x1, y0 + j * dy + 0.025), list(fora)): laje(F, junta, *r, n, 0.007)


def tracejado(F, mat, x0, y0, x1, y1, n, traco=1.2, vao=1.2, largura=0.08, alto=0.008):
    """Uma linha pintada aos traços, de (x0, y0) a (x1, y1) — reta, ao longo de x ou de y."""
    ao_x = abs(x1 - x0) >= abs(y1 - y0); L = abs(x1 - x0) if ao_x else abs(y1 - y0); s = 0.0
    while s < L - 0.05:
        f = min(s + traco, L)
        if ao_x: laje(F, mat, min(x0, x1) + s, y0 - largura / 2, min(x0, x1) + f, y0 + largura / 2, n, alto)
        else: laje(F, mat, x0 - largura / 2, min(y0, y1) + s, x0 + largura / 2, min(y0, y1) + f, n, alto)
        s += traco + vao


def bueiro(F, x, y, n=0.0, r=0.42):
    """Uma tampa de bueiro: o aro escuro, a tampa de aço e a cruz."""
    F.cilindro(ESCURO, x, y, r + 0.08, n, n + 0.005, 16); F.cilindro("sz_metal_cru", x, y, r, n, n + 0.008, 16)
    laje(F, ESCURO, x - r * 0.8, y - 0.025, x + r * 0.8, y + 0.025, n, 0.01); laje(F, ESCURO, x - 0.025, y - r * 0.8, x + 0.025, y + r * 0.8, n, 0.01)


def ralo(F, x, y, n=0.0, ao_x=True, comp=0.9):
    """Um ralo de grade ao pé do meio-fio."""
    a, b = (comp / 2, 0.16) if ao_x else (0.16, comp / 2)
    laje(F, ESCURO, x - a, y - b, x + a, y + b, n, 0.005)
    for k in range(6):
        s = -comp / 2 + comp * (k + 0.5) / 6
        if ao_x: laje(F, "sz_metal_cru_claro", x + s - 0.02, y - b, x + s + 0.02, y + b, n, 0.009)
        else: laje(F, "sz_metal_cru_claro", x - a, y + s - 0.02, x + a, y + s + 0.02, n, 0.009)


def mancha(F, mat, x, y, r, n=0.0, semente=1, alto=0.005, lados=9):
    """Uma mancha de contorno torto (a poça, o remendo do asfalto)."""
    pol = []
    for k in range(lados):
        a = 2 * math.pi * k / lados; rr = r * (0.62 + 0.5 * SZ.sorte("mancha", semente, k))
        pol.append((x + rr * math.cos(a) * 1.25, y + rr * math.sin(a)))
    F.prisma(mat, pol, n, n + alto)


def trilhos(F):
    """O trilho de cada robô, no piso: a faixa escura por onde ele anda, os dois frisos de aço e os traços de LED
    laranja (a cor do inimigo). Lê-se de longe por onde cada um passa."""
    for nome, (x, y), n, papel, (tipo, pontos), vel in SZ.ALVOS:
        pts = [(x, y)] + [(x + dx, y + dy) for dx, dy in pontos]
        if tipo == "ronda": pts.append((x, y))
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            x0, x1, y0, y1 = min(ax, bx), max(ax, bx), min(ay, by), max(ay, by)
            laje(F, "sz_borracha_escura", x0 - 0.36, y0 - 0.36, x1 + 0.36, y1 + 0.36, n, 0.008)          # borracha: o aço pintado espelhava o céu
            if x1 - x0 >= y1 - y0:
                for s in (-0.29, 0.29): laje(F, "sz_metal_cru_claro", x0 - 0.36, y0 + s - 0.025, x1 + 0.36, y0 + s + 0.025, n, 0.0105)
            else:
                for s in (-0.29, 0.29): laje(F, "sz_metal_cru_claro", x0 + s - 0.025, y0 - 0.36, x0 + s + 0.025, y1 + 0.36, n, 0.0105)
            tracejado(F, "sz_led_laranja", ax, ay, bx, by, n, 0.45, 1.1, 0.07, 0.011)
        for (px, py) in pts[:-1] if tipo == "ronda" else pts:
            F.cilindro("sz_borracha_escura", px, py, 0.55, n, n + 0.009, 12); F.cilindro("sz_led_laranja", px, py, 0.13, n, n + 0.0115, 8)


def pisos(m, M):
    """O piso trabalhado (Jhon, 26/11: "estou achando esse chão muito simples, reto", "chão muito liso"): cada lugar
    deixa de ser um plano de um tom só — placas em tons com a junta, as faixas e as linhas pintadas, o tapete de
    borracha de cada cabine, a grade do ralo, os trilhos dos robôs, a pista de corrida, o asfalto remendado com
    bueiros e poças, e os pisos redondos (o Núcleo, a Câmara) em setores e anéis. É tudo desenho, de meio a um
    centímetro de altura: nada muda na colisão. Vai numa forma à parte ("piso"), que não conta na conferência de
    quadrados vazios (um piso bonito não é o que enche um lugar)."""
    F = Formas(M, "piso")
    nf, nn, nc = SZ.N_FOGO, SZ.N_NUCLEO, SZ.N_CAMARA
    concreto = [("sz_concreto_claro", 0.2), ("sz_concreto_escuro", 0.1), ("sz_piso", 0.12)]
    liso = [("sz_piso", 0.18), ("sz_concreto_claro", 0.14), ("sz_concreto_escuro", 0.07)]

    # ---- a Linha de Fogo
    escada = (72.0, 28.6, 78.0, 34.0)
    lajes(F, (45.0, -2.4, 105.0, 34.0), nf, 5.0, "fogo", concreto, fora=[escada], junta="sz_concreto_escuro")
    for c in (55, 65, 75, 85, 95):                                           # o tapete de cada cabine e a faixa dela até o fundo
        laje(F, "sz_borracha_escura", c - 3.6, 21.9, c + 3.6, 24.7, nf, 0.008)
        for s in (-3.6, 3.6): laje(F, "sz_faixa_amarela", c + s - 0.04, 21.9, c + s + 0.04, 24.7, nf, 0.0095)
    for x in (50, 60, 70, 80, 90, 100):
        tracejado(F, "sz_faixa_branca", x, 1.0, x, 20.4, nf, 1.0, 1.4, 0.07)
    for y, mat in ((15.9, "sz_faixa_branca"), (7.9, "sz_faixa_branca"), (1.3, "sz_faixa_amarela")):      # as marcas de distância
        tracejado(F, mat, 46.0, y, 104.0, y, nf, 0.5, 0.5, 0.06)
    laje(F, ESCURO, 46.0, 20.55, 104.0, 20.95, nf, 0.006)                     # a canaleta do ralo, com a grade
    for k in range(116):
        laje(F, "sz_metal_cru_claro", 46.2 + 0.5 * k, 20.57, 46.24 + 0.5 * k, 20.93, nf, 0.0085)
    for k in range(100):                                                     # a hachura da beira do abismo
        laje(F, "sz_faixa_amarela" if k % 2 else ESCURO, 45.0 + 0.6 * k, -2.95, 45.6 + 0.6 * k, -2.45, nf, 0.008)
    for x, y in ((57.0, 29.0), (93.0, 30.5), (66.0, 3.2)): bueiro(F, x, y, nf)
    mancha(F, "sz_agua", 84.0, 30.8, 1.3, nf, 1, 0.006); mancha(F, "sz_agua", 60.5, 12.6, 0.9, nf, 2, 0.006)

    # ---- a Rua Ignição: a calçada dos prédios, o asfalto com as faixas, os remendos, os bueiros e as poças
    laje(F, "sz_concreto_claro", 105.6, 18.0, 145.4, 21.2, 0.0, 0.006)
    for x in range(108, 145, 4): laje(F, "sz_concreto_escuro", x - 0.025, 18.0, x + 0.025, 21.2, 0.0, 0.008)
    laje(F, "sz_concreto_escuro", 105.6, 21.2, 145.4, 21.38, 0.0, 0.008)       # o meio-fio
    for y in (24.9, 31.1):                                                   # o asfalto gasto onde se passa
        laje(F, "sz_asfalto_claro", 106.0, y - 0.55, 145.2, y + 0.55, 0.0, 0.003)
    for k, (x0, y0, x1, y1) in enumerate(((114.0, 26.2, 117.6, 28.5), (129.4, 32.2, 133.2, 34.7), (138.4, 22.3, 141.2, 24.1), (107.6, 31.4, 110.4, 34.2), (122.0, 33.0, 124.4, 35.0))):
        laje(F, "sz_asfalto_escuro", x0, y0, x1, y1, 0.0, 0.0045)
    mancha(F, "sz_asfalto_escuro", 135.6, 29.6, 1.6, 0.0, 5, 0.0045); mancha(F, "sz_asfalto_escuro", 118.4, 22.9, 1.2, 0.0, 6, 0.0045)
    tracejado(F, "sz_faixa_amarela", 107.0, 28.0, 144.6, 28.0, 0.0, 1.6, 1.6, 0.14)
    laje(F, "sz_faixa_branca", 106.0, 21.62, 145.2, 21.74, 0.0, 0.008); laje(F, "sz_faixa_branca", 106.0, 35.2, 145.2, 35.32, 0.0, 0.008)
    for k in range(14):                                                      # a faixa de pedestre da entrada
        laje(F, "sz_faixa_branca", 106.3, 22.2 + 0.92 * k, 108.3, 22.66 + 0.92 * k, 0.0, 0.008)
    for x, y in ((118.6, 33.2), (136.2, 26.2), (127.6, 23.0), (143.0, 33.4)): bueiro(F, x, y)
    for x in (113.5, 123.5, 133.5, 143.5): ralo(F, x, 21.56)
    mancha(F, "sz_agua", 124.2, 30.2, 1.2, 0.0, 3, 0.006); mancha(F, "sz_agua", 140.6, 34.0, 1.0, 0.0, 4, 0.006); mancha(F, "sz_agua", 110.4, 24.0, 0.8, 0.0, 7, 0.006)
    lajes(F, (105.6, 4.2, 146.0, 9.8), 0.0, 4.0, "viela", [("sz_asfalto_escuro", 0.2), ("sz_asfalto_claro", 0.15)])

    # ---- a Arena da Matilha: as placas, a pista de corrida, o tapete ao pé da parede de corrida, o quadro do especial
    lajes(F, (40.0, 67.3, 120.0, 98.0), 0.0, 5.0, "arena", concreto, fora=[SZ.VAO_ARENA, (40.0, 67.2, 42.4, 74.2), (105.0, 67.2, 107.4, 74.2)], junta="sz_concreto_escuro")
    laje(F, "sz_borracha", 45.6, 68.9, 84.4, 72.1, 0.0, 0.0065)
    for y in (68.9, 72.1): laje(F, "sz_faixa_branca", 45.6, y - 0.05, 84.4, y + 0.05, 0.0, 0.0085)
    for x0 in (45.6, 83.6):                                                  # a largada e a chegada, em xadrez
        for i in range(2):
            for j in range(8):
                if (i + j) % 2 == 0: laje(F, "sz_faixa_branca", x0 + 0.4 * i, 68.9 + 0.4 * j, x0 + 0.4 * (i + 1), 68.9 + 0.4 * (j + 1), 0.0, 0.0085)
    laje(F, "sz_borracha_escura", 84.0, 96.5, 116.0, 97.9, 0.0, 0.0065)
    for k in range(32): laje(F, "sz_led_azul" if k % 4 == 0 else "sz_faixa_amarela", 84.0 + k, 96.42, 84.5 + k, 96.5, 0.0, 0.0085)
    for (x0, y0, x1, y1) in ((86.4, 78.6, 101.2, 78.72), (86.4, 84.9, 101.2, 85.02), (86.4, 78.6, 86.52, 85.02), (101.08, 78.6, 101.2, 85.02)):
        laje(F, "sz_faixa_amarela", x0, y0, x1, y1, 0.0, 0.0085)
    for x, y in ((57.0, 76.0), (96.0, 73.0), (111.0, 93.0)): bueiro(F, x, y)

    # ---- o corredor, a Sala de Ativação e o Ponto de Retorno: placas em tons, a faixa escura ao pé das paredes
    lajes(F, (20.0, 47.0, 62.6, 53.0), 0.0, 3.05, "corredor", liso, fora=[SZ.VAO_CORREDOR, (49.0, 47.0, 61.0, 53.0)], junta="sz_concreto_escuro")
    for y0, y1 in ((47.0, 47.32), (52.68, 53.0)):
        for r in SZ.menos((20.0, y0, 62.6, y1), [SZ.VAO_CORREDOR]): laje(F, ESCURO, *r, 0.0, 0.0085)
    lajes(F, (4.0, 38.0, 20.0, 62.0), 0.0, 4.0, "ativacao", liso, junta="sz_concreto_escuro")
    lajes(F, (4.0, 70.0, 30.0, 96.0), 0.0, 4.3, "retorno", liso, junta="sz_concreto_escuro")
    lajes(F, (31.0, 64.0, 39.0, 80.0), 0.0, 4.0, "patio", concreto, junta="sz_concreto_escuro")

    # ---- o Núcleo: o piso do anfiteatro em doze setores, com os anéis e os raios de aço
    cx, cy = 75.0, 50.0
    for k in range(12):
        if k % 2 == 0: F.setor("sz_piso", cx, cy, 4.6, 8.85, 30 * k + 1.0, 30 * k + 29.0, nn, nn + 0.005)
        F.setor(ESCURO, cx, cy, 4.6, 8.9, 30 * k - 0.35, 30 * k + 0.35, nn, nn + 0.008, passo=1.0)
    for r0, r1, mat in ((2.24, 2.42, ESCURO), (4.42, 4.6, "sz_metal_cru_claro"), (8.9, 9.14, ESCURO)):
        F.anel(mat, cx, cy, r0, r1, nn, nn + 0.008, 7.5)
    # ---- a Câmara: dezesseis setores, o anel de LED e os de aço
    cx, cy = 112.0, 50.0
    for k in range(16):
        if k % 2 == 0: F.setor("sz_concreto_escuro", cx, cy, 3.7, 6.6, 22.5 * k + 1.0, 22.5 * k + 21.5, nc, nc + 0.005)
    F.anel("sz_led_violeta", cx, cy, 3.44, 3.52, nc, nc + 0.0095, 7.5)
    for r0, r1 in ((3.56, 3.7), (6.6, 6.82), (7.5, 7.58)):
        F.anel(ESCURO, cx, cy, r0, r1, nc, nc + 0.008, 7.5)

    trilhos(F)
    return F.fechar()


def capsula(m, F, nome, x, y, base, led="sz_led_violeta"):
    """Uma cápsula de metamorfose: a base, o anel de luz, o tubo de vidro e a tampa. Sólida."""
    F.cilindro(ESCURO, x, y, 0.64, base, base + 0.26, 12, r_topo=0.52)
    F.cilindro(led, x, y, 0.5, base + 0.26, base + 0.31, 12)
    F.cilindro("sz_vidro_azul", x, y, 0.44, base + 0.31, base + 2.45, 12)
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.5
        F.viga(ESCURO, W(x + 0.46 * math.cos(a), y + 0.46 * math.sin(a), base + 0.31), W(x + 0.46 * math.cos(a), y + 0.46 * math.sin(a), base + 2.45), 0.07)
    F.cilindro(led, x, y, 0.47, base + 2.45, base + 2.5, 12)
    F.cilindro(ESCURO, x, y, 0.52, base + 2.5, base + 2.78, 12, r_topo=0.34)
    X, Z = P(x, y)
    m.cilindro("Colisao", nome, (X, base + 1.4, Z), 0.62, 2.8, "kit_colisao", oculta=True)


def mesa_holo(m, F, nome, x, y, base, led="sz_led_ciano"):
    """Uma mesa holográfica: o pedestal de seis lados, o tampo de luz e dois aros a pairar por cima."""
    F.cilindro(ESCURO, x, y, 0.5, base, base + 0.12, 6); F.cilindro("sz_metal_pintado", x, y, 0.34, base + 0.12, base + 0.86, 6, r_topo=0.44)
    F.cilindro(ESCURO, x, y, 0.5, base + 0.86, base + 0.94, 6); F.cilindro(led, x, y, 0.4, base + 0.94, base + 0.96, 6)
    F.aro(led, W(x, y, base + 1.3), 0.3, (0.25, 1.0, 0.1), 0.025, 18); F.aro(led, W(x, y, base + 1.42), 0.2, (-0.2, 1.0, 0.3), 0.02, 14)
    X, Z = P(x, y)
    m.cilindro("Colisao", nome, (X, base + 0.48, Z), 0.5, 0.96, "kit_colisao", oculta=True)


def recheio(m, M, V, F):
    """O que enche os lugares que estavam vazios (Jhon, 26/11: "muito vazio"): na Câmara de Metamorfose, as cápsulas
    em roda, o relevo do lobo na parede do fundo e o lustre de aros sob a cúpula; no Núcleo, as mesas holográficas;
    na Linha de Fogo, na arena e na rua, bancos, barris, lixeiras, máquinas, carros estacionados e drones no ar. Tudo
    fora dos caminhos do treino e dos percursos dos robôs (os testes `SetorZero…` andam por eles)."""
    nf, nn, nc = SZ.N_FOGO, SZ.N_NUCLEO, SZ.N_CAMARA
    # ---- a Câmara: nove cápsulas ao pé da parede (fora das três portas), o lobo e o lustre
    cx, cy = 112.0, 50.0
    for k, ang in enumerate((-50, -25, 25, 50, 125, 145, 215, 235)):
        a = math.radians(ang)
        capsula(m, F, f"Capsula{k + 1}", cx + 7.75 * math.cos(a), cy + 7.75 * math.sin(a), nc)
    F.perfil("sz_led_violeta", [(u * 1.05, v * 1.03 - 0.06) for u, v in LOBO], W(120.52, 50.0, nc + 2.3), (0.0, 0.0, -1.0), 0.06)
    F.perfil(ESCURO, LOBO, W(120.36, 50.0, nc + 2.3), (0.0, 0.0, -1.0), 0.22)
    F.perfil("sz_led_violeta", LOBO_OLHO, W(120.34, 50.0, nc + 2.3), (0.0, 0.0, -1.0), 0.24)
    F.caixa(ESCURO, 120.0, 48.0, 120.8, 52.0, nc, nc + 0.5, chanfro=0.2)        # o soco do relevo
    F.caixa("sz_led_violeta", 119.96, 48.3, 120.0, 51.7, nc + 0.2, nc + 0.26)
    m.caixa("Colisao", "SocoLobo", (P(120.4, 50.0)[0], nc + 0.25, P(120.4, 50.0)[1]), (0.8, 0.5, 4.0), "kit_colisao", oculta=True)
    for r, h, led in ((3.2, nc + 6.3, "sz_led_violeta"), (2.2, nc + 6.9, "sz_led_branco"), (1.2, nc + 7.5, "sz_led_violeta")):
        F.aro(ESCURO, W(cx, cy, h), r, (0.0, 1.0, 0.0), 0.12, 32); F.aro(led, W(cx, cy, h - 0.07), r, (0.0, 1.0, 0.0), 0.05, 32)
    for k in range(4):
        a = math.pi / 4 + math.pi / 2 * k
        F.tubo(ESCURO, [W(cx + 3.2 * math.cos(a), cy + 3.2 * math.sin(a), nc + 6.3), W(cx + 2.2 * math.cos(a), cy + 2.2 * math.sin(a), nc + 6.9),
                        W(cx + 1.2 * math.cos(a), cy + 1.2 * math.sin(a), nc + 7.5), W(cx, cy, nc + 9.4)], 0.04)
    # ---- o Núcleo: quatro mesas holográficas nas diagonais do piso do anfiteatro
    for k, ang in enumerate((45, 135, 225, 315)):
        a = math.radians(ang)
        mesa_holo(m, F, f"MesaHolo{k + 1}", 75.0 + 6.9 * math.cos(a), 50.0 + 6.9 * math.sin(a), nn)
    # ---- a Linha de Fogo: atrás da linha de tiro, bancos, barris e a máquina; ao fundo, mais barris
    for x in (56.0, 64.0, 86.0, 94.0):
        por(M, "SM_Props_Bench_01", x, 32.2, 0.0, nf, colide="caixa", escala=1.4)
    for k, (x, y) in enumerate(((46.0, 20.2), (46.9, 20.5), (46.3, 21.2), (103.6, 21.4), (104.0, 22.4), (88.4, 33.0), (89.3, 33.2), (61.0, 33.2))):
        por(M, "SM_Props_Barrel_01", x, y, 40.0 * k, nf, colide="caixa")
    por(M, "SM_Props_VendingMachine_01", 68.6, 33.3, 180.0, nf, colide="caixa"); por(M, "SM_Props_VendingMachine_02", 70.0, 33.3, 180.0, nf, colide="caixa")
    por(M, "SM_Props_Trash_01", 79.2, 33.2, 0.0, nf, colide="caixa"); por(M, "SM_Props_Trash_01", 54.2, 33.2, 0.0, nf, colide="caixa")
    # ---- a Rua Ignição: carros estacionados, bancos e máquinas na calçada, lixeiras, barris
    por(M, "SM_Car_03", 107.9, 24.6, 4.0, 0.0, colide="caixa"); por(M, "SM_Car_06", 134.0, 6.4, 92.0, 0.0, colide="caixa")
    por(M, "SM_Car_01", 143.4, 12.5, 2.0, 0.0, colide="caixa")
    for x in (117.0, 133.0):
        por(M, "SM_Props_Bench_02", x, 18.5, 0.0, 0.0, colide="caixa", escala=1.4)
    por(M, "SM_Props_VendingMachine_01", 121.0, 18.5, 0.0, 0.0, colide="caixa"); por(M, "SM_Props_VendingMachine_02", 136.6, 18.5, 0.0, 0.0, colide="caixa")
    for x in (115.0, 130.6, 138.4): por(M, "SM_Props_Trash_01", x, 18.6, 0.0, 0.0, colide="caixa")
    for k, (x, y) in enumerate(((106.4, 35.0), (107.2, 35.1), (144.6, 35.0), (144.9, 21.9), (119.0, 5.0), (119.8, 5.3))):
        por(M, "SM_Props_Barrel_01", x, y, 55.0 * k, 0.0, colide="caixa")
    # ---- a Arena: bancos sob a galeria, barris ao pé dos pilares, lixeiras
    for x in (52.0, 60.0, 68.0, 84.0, 92.0):
        por(M, "SM_Props_Bench_01", x, 65.1, 0.0, 0.0, colide="caixa", escala=1.4)
    for k, (x, y) in enumerate(((55.6, 96.6), (60.4, 96.9), (65.5, 96.7), (70.6, 96.9), (75.4, 96.6), (80.6, 96.8), (44.6, 96.8), (45.4, 97.0), (117.0, 66.0), (117.8, 66.3))):
        por(M, "SM_Props_Barrel_01", x, y, 70.0 * k, 0.0, colide="caixa")
    por(M, "SM_Props_Trash_01", 44.8, 65.2, 0.0, 0.0, colide="caixa"); por(M, "SM_Props_Trash_01", 103.0, 65.2, 0.0, 0.0, colide="caixa")
    # ---- os drones de vigia, parados no ar por cima de cada setor (só desenho)
    for k, (x, y, h) in enumerate(((75.0, 12.0, nf + 8.5), (96.0, 22.0, nf + 7.0), (82.0, 80.0, 10.5), (108.0, 86.0, 8.0), (126.0, 27.0, 9.0), (75.0, 50.0, 8.5), (20.0, 20.0, 9.0), (136.0, 51.0, 9.5))):
        por(M, "SM_Props_Drone_01" if k % 2 == 0 else "SM_Props_Drone_02", x, y, 37.0 * k, h, planta=False)



def acabamento(m, M, F):
    """Põe o acabamento na montagem `M` (o kit sci-fi) e nas formas `F`, e devolve a montagem do mato (o kit 3:
    árvores, pinheiros e rochas), que vira um segundo desenho."""
    V = Montagem("fps3", lambda nome: None)
    percurso(m, M, V, F)
    jardim(m, M, V, F)
    meio(m, M, V, F)
    patio(m, M, V, F)
    praca(m, M, V, F)
    pinhal(m, M, V, F)
    bordas(m, M, V, F)
    arquitetura(m, M, V, F)
    detalhes(m, M, V, F)
    recheio(m, M, V, F)
    m._piso = pisos(m, M)
    linhas_guia(m, M, V, F)
    montanha(m, M, V, F)
    nuvens(m)
    return V
