"""
O robô de treino da WolfLeague: o inimigo em que se atira no Setor Zero.

    python3 src/tools/mapas/robo_treino.py     # grava src/assets/mapas/pecas/robo_treino.glb

Precisa de numpy e Pillow. Desenho próprio (pedidos do Jhon, 26/11: "robôs
de treinamento em áreas planejadas, com movimentação", e depois "robôs com
textura melhor também, com acabamento legal"): um drone que paira — o jato
e o bocal de aço, a saia do propulsor, um anel de energia que gira em
volta dela, o tronco arredondado de blindagem clara com o cinto, as juntas
de aço dos ombros com uma ombreira laranja e uma lâmina emissora de cada
lado, a mochila do propulsor nas costas, e a cabeça em capacete com a
viseira de vidro fumê de lado a lado (a luz dos olhos por trás), duas
aletas (as orelhas do lobo) e a antena. As referências de robô que o Jhon
mandou são de outro jogo: serviram para a ideia (um alvo simpático, que
paira e se lê de longe), não para a forma — nada dali foi copiado.

Cabe na caixa de colisão do `Dummy` (1 × 2 × 1 m): 0,98 m de ombro a
ombro, 1,97 m do chão à ponta da antena, com os pés (o jato) no chão. A
frente é +Z. A cabeça vai de 1,40 a 1,80 m (o `Dummy.RobotHeadFrom`).

As peças redondas têm a luz lisa (a normal é do vértice); as chapas, a
quina viva. As cores são da paleta do mapa (setor_zero.py: CORES); o
material é o `robo_treino.tres` — o do kit, com o desenho do material
grudado na peça (no do mapa ele é projetado pelo mundo, e escorregaria
por cima do robô andando) e mais miúdo. O que gira (o anel) sai em peças
à parte, de nome `…_f_gira`: quem gira, põe em cena, move e faz piscar é
o `src/scripts/debug/Dummy.cs`.
"""
import math
import os
import sys

import numpy as np

if __package__ in (None, ""):
    sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    __package__ = "mapas"

from .kits.montagem import Montagem
from .kits.kit_fbx import RAIZ
from .kits import detalhe
from . import setor_zero as SZ
from .setor_zero_formas import Formas, W

BRANCO, GRAFITE, ACENTO, ACO, VIDRO = "sz_lataria_branca", "sz_lataria_grafite", "sz_lataria_laranja", "sz_metal_cru_claro", "sz_vidro_escuro"
ESCURO = "sz_metal_pintado_escuro"
MATERIAL = "res://src/assets/mapas/detalhe/robo_treino.tres"


def L(x, z, h):
    """Do robô (x para a direita dele, z para a frente, h para cima; a origem nos pés) para o mundo das formas."""
    return W(SZ.CX + x, SZ.CY + z, h)


def torno(F, mat, perfil, n=28, sx=1.0, sz=1.0, cx=0.0, cz=0.0, de=0.0, ate=360.0, tampas=True):
    """Um corpo de revolução de pé, de luz lisa: `perfil` é [(altura, raio)], de baixo para cima; `sx`/`sz` achatam a
    seção (uma elipse); `de`/`ate` (graus; 0 = a frente, +Z, crescendo para a direita do robô) fazem só uma fatia."""
    a0, a1 = math.radians(de), math.radians(ate)
    inteiro = abs(ate - de) >= 359.9
    passos = n if inteiro else max(2, int(round(n * abs(ate - de) / 360.0)))
    ang = [a0 + (a1 - a0) * i / passos for i in range(passos + 1)]

    def ponto(h, r, t): return L(cx + r * sx * math.sin(t), cz + r * sz * math.cos(t), h)

    def normal(k, t):
        # a inclinação do perfil no anel k (a média dos dois trechos) e a elipse
        (h0, r0), (h1, r1) = perfil[max(k - 1, 0)], perfil[min(k + 1, len(perfil) - 1)]
        dh, dr = h1 - h0, r1 - r0
        nx, nz, ny = math.sin(t) / max(sx, 1e-6), math.cos(t) / max(sz, 1e-6), 0.0
        comp = math.hypot(nx, nz); nx, nz = nx / comp, nz / comp
        m = math.hypot(dh, dr) or 1.0
        v = np.array([nx * dh / m, -dr / m, nz * dh / m])
        return v / (np.linalg.norm(v) or 1.0)

    T, N = [], []
    for k in range(len(perfil) - 1):
        (h0, r0), (h1, r1) = perfil[k], perfil[k + 1]
        for i in range(passos):
            t0, t1 = ang[i], ang[i + 1]
            a, b, c, d = ponto(h0, r0, t0), ponto(h0, r0, t1), ponto(h1, r1, t1), ponto(h1, r1, t0)
            na, nb, nc, nd = normal(k, t0), normal(k, t1), normal(k + 1, t1), normal(k + 1, t0)
            T += [[a, b, c], [a, c, d]]; N += [[na, nb, nc], [na, nc, nd]]          # a face olha para fora (regra da mão direita, como no glTF)
    F._junta(mat, T, N)
    if tampas and inteiro:
        for (h, r), sentido in ((perfil[0], -1.0), (perfil[-1], 1.0)):
            if r < 1e-4: continue
            c = L(cx, cz, h); aro = [ponto(h, r, t) for t in ang]
            tris = [[c, aro[i + 1], aro[i]] if sentido < 0 else [c, aro[i], aro[i + 1]] for i in range(passos)]
            F._junta(mat, tris, [[[0.0, sentido, 0.0]] * 3] * passos)


def bola(F, mat, x, z, h, r, n=14):
    """Uma bola (a junta do ombro, a ponta da antena)."""
    torno(F, mat, [(h + r * math.sin(a), r * math.cos(a)) for a in np.linspace(-math.pi / 2, math.pi / 2, 9)], n=n, cx=x, cz=z, tampas=False)


def montar():
    M = Montagem("scifi", lambda nome: None)
    SZ.cores(M.kit)
    F = Formas(M, "robo")
    G = Formas(M, "gira")          # o que o Dummy faz girar

    # ---- o propulsor: o jato, o bocal de aço e a saia
    torno(F, "sz_led_laranja", [(0.02, 0.03), (0.1, 0.1), (0.2, 0.13)], n=12)
    torno(F, ACO, [(0.18, 0.17), (0.22, 0.2), (0.3, 0.18), (0.34, 0.15)], n=16)
    torno(F, ESCURO, [(0.2, 0.14), (0.2, 0.165)], n=16, tampas=False)
    torno(F, GRAFITE, [(0.32, 0.17), (0.38, 0.26), (0.5, 0.33), (0.58, 0.345), (0.63, 0.3)], n=24, sz=0.9)
    for k in range(6):                                                       # as aletas da saia
        a = math.radians(60 * k + 30)
        F.viga(ACO, L(0.19 * math.sin(a), 0.19 * math.cos(a) * 0.9, 0.36), L(0.335 * math.sin(a), 0.335 * math.cos(a) * 0.9, 0.56), 0.035, 0.05)
    torno(F, "sz_led_ciano", [(0.585, 0.352), (0.612, 0.345)], n=24, sz=0.9, tampas=False)

    # ---- o anel de energia, em volta da saia: gira (três arcos de aço com a luz por dentro)
    for k in range(3):
        d0 = 120 * k + 8
        torno(G, ACO, [(0.43, 0.455), (0.43, 0.49), (0.475, 0.49), (0.475, 0.455), (0.43, 0.455)], n=30, de=d0, ate=d0 + 92, tampas=False)
        torno(G, "sz_led_ciano", [(0.44, 0.452), (0.465, 0.452)], n=30, de=d0 + 6, ate=d0 + 86, tampas=False)
        for d in (d0, d0 + 92):                                              # as pontas de cada arco, fechadas
            a = math.radians(d)
            G.viga(ESCURO, L(0.45 * math.sin(a), 0.45 * math.cos(a), 0.4525), L(0.495 * math.sin(a), 0.495 * math.cos(a), 0.4525), 0.02, 0.06)

    # ---- o tronco: o cinto, a blindagem clara abrindo para os ombros, a gola
    torno(F, ESCURO, [(0.61, 0.29), (0.66, 0.275), (0.7, 0.285)], n=24, sz=0.86)
    torno(F, BRANCO, [(0.69, 0.285), (0.8, 0.33), (0.95, 0.385), (1.1, 0.41), (1.2, 0.4), (1.27, 0.33), (1.3, 0.2)], n=28, sz=0.8)
    for h, r in ((0.86, 0.357), (1.165, 0.409)):                           # as emendas da blindagem
        torno(F, GRAFITE, [(h - 0.008, r), (h + 0.008, r + 0.002)], n=28, sz=0.8, tampas=False)
    torno(F, ACO, [(1.29, 0.2), (1.31, 0.16), (1.36, 0.15), (1.4, 0.17)], n=16)                   # o pescoço
    torno(F, ESCURO, [(1.33, 0.156), (1.345, 0.156)], n=16, tampas=False)
    # o peito: a placa de grafite e o emblema (um "V" de luz, a ponta para baixo)
    F.tronco(GRAFITE, [L(-0.15, 0.27, 0.8), L(0.15, 0.27, 0.8), L(0.15, 0.2, 0.8), L(-0.15, 0.2, 0.8)],
             [L(-0.24, 0.345, 1.15), L(0.24, 0.345, 1.15), L(0.24, 0.26, 1.15), L(-0.24, 0.26, 1.15)])
    for s in (-1, 1):
        F.viga("sz_led_ciano", L(s * 0.15, 0.342, 1.1), L(0.0, 0.3, 0.88), 0.042, 0.03)
        F.viga(ACO, L(s * 0.2, 0.34, 1.13), L(s * 0.125, 0.283, 0.82), 0.014, 0.02)                # o friso da placa
    F.caixa("sz_led_branco", SZ.CX - 0.03, SZ.CY + 0.255, SZ.CX + 0.03, SZ.CY + 0.275, 0.72, 0.76)      # a luz de estado
    # as costas: a mochila do propulsor, dois escapes e a luz do núcleo
    F.tronco(GRAFITE, [L(-0.17, -0.2, 0.76), L(0.17, -0.2, 0.76), L(0.17, -0.33, 0.8), L(-0.17, -0.33, 0.8)],
             [L(-0.2, -0.28, 1.18), L(0.2, -0.28, 1.18), L(0.2, -0.42, 1.12), L(-0.2, -0.42, 1.12)])
    for s in (-1, 1):
        F.revolucao(ACO, L(s * 0.1, -0.33, 0.84), (0.0, -0.5, -1.0), [(0.0, 0.05), (0.09, 0.062)], n=10)
        F.revolucao("sz_led_laranja", L(s * 0.1, -0.345, 0.81), (0.0, -0.5, -1.0), [(0.0, 0.03), (0.05, 0.04)], n=8)
    F.viga("sz_led_ciano", L(0.0, -0.405, 1.0), L(0.0, -0.385, 1.1), 0.05, 0.02, cima=(0.0, 0.0, -1.0))

    # ---- os ombros: a junta de aço, a ombreira laranja (com o friso claro) e a lâmina emissora
    for s in (-1, 1):
        bola(F, ACO, s * 0.395, 0.0, 1.13, 0.085)
        F.viga(ESCURO, L(s * 0.3, 0.0, 1.13), L(s * 0.4, 0.0, 1.13), 0.1)
        F.tronco(ACENTO, [L(s * 0.415, -0.16, 0.98), L(s * 0.415, 0.16, 0.98), L(s * 0.47, 0.16, 0.98), L(s * 0.47, -0.16, 0.98)],
                 [L(s * 0.4, -0.2, 1.27), L(s * 0.4, 0.2, 1.27), L(s * 0.49, 0.2, 1.27), L(s * 0.49, -0.2, 1.27)])
        F.tronco(ACENTO, [L(s * 0.4, -0.2, 1.27), L(s * 0.4, 0.2, 1.27), L(s * 0.49, 0.2, 1.27), L(s * 0.49, -0.2, 1.27)],
                 [L(s * 0.36, -0.15, 1.32), L(s * 0.36, 0.15, 1.32), L(s * 0.44, 0.15, 1.32), L(s * 0.44, -0.15, 1.32)])
        F.viga("sz_led_branco", L(s * 0.482, -0.14, 1.2), L(s * 0.482, 0.14, 1.2), 0.016, 0.035)
        F.viga(GRAFITE, L(s * 0.476, -0.17, 1.05), L(s * 0.476, 0.17, 1.05), 0.012, 0.05)
        # a lâmina: do ombro para baixo, afinando, com a ponta de luz
        F.tronco(GRAFITE, [L(s * 0.42, -0.05, 0.66), L(s * 0.42, 0.07, 0.66), L(s * 0.455, 0.07, 0.66), L(s * 0.455, -0.05, 0.66)],
                 [L(s * 0.41, -0.11, 0.98), L(s * 0.41, 0.12, 0.98), L(s * 0.465, 0.12, 0.98), L(s * 0.465, -0.11, 0.98)])
        F.tronco("sz_led_ciano", [L(s * 0.432, -0.01, 0.6), L(s * 0.432, 0.03, 0.6), L(s * 0.444, 0.03, 0.6), L(s * 0.444, -0.01, 0.6)],
                 [L(s * 0.425, -0.045, 0.66), L(s * 0.425, 0.065, 0.66), L(s * 0.45, 0.065, 0.66), L(s * 0.45, -0.045, 0.66)])
        F.viga(ACO, L(s * 0.468, 0.0, 0.7), L(s * 0.468, 0.0, 0.95), 0.012, 0.03)

    # ---- a cabeça: o capacete, a viseira de vidro fumê com a luz dos olhos, o queixo, as aletas e a antena
    capacete = [(1.4, 0.15), (1.44, 0.2), (1.52, 0.232), (1.62, 0.24), (1.7, 0.225), (1.76, 0.18), (1.795, 0.1), (1.8, 0.0)]
    torno(F, BRANCO, capacete, n=28, sz=0.95, cz=-0.01)
    torno(F, VIDRO, [(1.535, 0.241), (1.56, 0.25), (1.65, 0.25), (1.675, 0.238)], n=36, sz=0.95, cz=-0.01, de=-76, ate=76, tampas=False)
    torno(F, "sz_led_ciano", [(1.588, 0.2525), (1.622, 0.2525)], n=36, sz=0.95, cz=-0.01, de=-52, ate=52, tampas=False)
    for d in (-76, 76):                                                      # o batente da viseira, de cada lado
        a = math.radians(d)
        F.viga(GRAFITE, L(0.246 * math.sin(a), 0.246 * 0.95 * math.cos(a) - 0.01, 1.53), L(0.246 * math.sin(a), 0.246 * 0.95 * math.cos(a) - 0.01, 1.68), 0.03, 0.025)
    torno(F, GRAFITE, [(1.43, 0.2), (1.47, 0.226), (1.53, 0.243)], n=28, sz=0.95, cz=-0.01, de=-70, ate=70, tampas=False)      # o queixo
    torno(F, GRAFITE, [(1.69, 0.232), (1.705, 0.227)], n=28, sz=0.95, cz=-0.01, tampas=False)                               # a emenda do alto
    for s in (-1, 1):                                                        # as aletas (as orelhas)
        F.tronco(ACENTO, [L(s * 0.1, -0.11, 1.74), L(s * 0.2, -0.11, 1.7), L(s * 0.2, 0.04, 1.7), L(s * 0.1, 0.04, 1.74)],
                 [L(s * 0.19, -0.14, 1.93), L(s * 0.215, -0.14, 1.93), L(s * 0.215, -0.1, 1.93), L(s * 0.19, -0.1, 1.93)])
        F.viga(GRAFITE, L(s * 0.212, -0.1, 1.72), L(s * 0.216, -0.125, 1.9), 0.01, 0.03)
    F.viga(ACO, L(0.0, -0.17, 1.7), L(0.0, -0.2, 1.94), 0.014)               # a antena
    bola(F, "sz_led_vermelho", 0.0, -0.2, 1.95, 0.022, n=8)

    F.fechar(); G.fechar()
    destino = os.path.join(RAIZ, "assets", "mapas", "pecas", "robo_treino.glb")
    nos, pecas, tamanho = M.glb(destino)
    detalhe.ligar(destino, MATERIAL)
    print(f"robo_treino.glb: {nos} peças, {tamanho / 1e3:.0f} kB")
    return M


if __name__ == "__main__":
    montar()
