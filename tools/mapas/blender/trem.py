#!/usr/bin/env python3
"""
O vagão do Metrô, modelado no Blender: carroceria de inox com janelas e
borracha, portas de correr abertas, cabine nas pontas com para-brisa e
faróis, teto com frisos, chassi com caixas de equipamento, dois truques
com rodas de friso em cima do trilho, e o salão por dentro (piso,
bancos, balaústres, corrimão com alças, luminárias).

    python3 tools/mapas/blender/trem.py     ->  assets/mapas/pecas/trem_metro.glb

**As medidas são as do jogo** (tools/mapas/metro.py, o grupo "Trem"): o
piso em y = 0, as paredes de z = ±2,6 a ±2,8 até y = 2, o teto de 2 a
2,2, o vagão de x = −9 a 9, as portas laterais de x = ±3,5 a ±5,5 e a
passagem das pontas em |z| < 0,8. A colisão continua sendo as caixas de
lá; isto é só o que se vê. Mudou a medida lá, mude aqui.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from malha import Modelo  # noqa: E402

COMP, FORA, DENTRO, ALTO, TETO = 9.0, 2.8, 2.6, 2.0, 2.2
PORTAS = ((3.5, 5.5), (-5.5, -3.5))
TRECHOS = ((-9.0, -5.5), (-3.5, 3.5), (5.5, 9.0))       # as paredes entre as portas
PEITORIL, VERGA = 0.86, 1.72                              # a janela vai de um ao outro
TRILHO_Y, BITOLA, RODA = -1.04, 0.75, 0.42


def materiais(m):
    m.material("inox", (0.70, 0.72, 0.74), aspereza=0.36, metal=0.85)
    m.material("teto", (0.52, 0.54, 0.56), aspereza=0.5, metal=0.7)
    m.material("faixa", (0.05, 0.11, 0.25), aspereza=0.45, metal=0.3)
    m.material("filete", (0.93, 0.47, 0.12), aspereza=0.5)
    m.material("vidro", (0.04, 0.06, 0.07), aspereza=0.05, metal=0.2, alfa=0.38)
    m.material("borracha", (0.015, 0.015, 0.018), aspereza=0.9)
    m.material("chassi", (0.075, 0.078, 0.085), aspereza=0.7, metal=0.5)
    m.material("aco", (0.34, 0.35, 0.36), aspereza=0.42, metal=0.9)
    m.material("piso", (0.15, 0.16, 0.18), aspereza=0.82)
    m.material("forro", (0.84, 0.83, 0.78), aspereza=0.62)
    m.material("assento", (0.10, 0.27, 0.46), aspereza=0.5)
    m.material("cromado", (0.82, 0.83, 0.84), aspereza=0.18, metal=1.0)
    m.material("amarelo", (0.95, 0.74, 0.10), aspereza=0.6)
    m.material("luz", (1, 1, 1), brilho=(1.0, 0.96, 0.88), energia=2.6)
    m.material("farol", (1, 1, 1), brilho=(1.0, 0.97, 0.86), energia=5.0)
    m.material("lanterna", (0.5, 0.02, 0.02), brilho=(1.0, 0.08, 0.05), energia=2.5)


def janelas_do_trecho(x0, x1):
    """Onde ficam as janelas num trecho de parede: (x do meio, largura)."""
    comp = x1 - x0
    if comp < 4:
        return [((x0 + x1) / 2, 2.2)]
    larg, n = 1.7, 3
    coluna = (comp - larg * n) / (n + 1)
    return [(x0 + coluna * (i + 1) + larg * (i + 0.5), larg) for i in range(n)]


def parede_lateral(m, lado):
    z = lado * (FORA + DENTRO) / 2
    esp = FORA - DENTRO
    for x0, x1 in TRECHOS:
        meio, comp = (x0 + x1) / 2, x1 - x0
        m.caixa((meio, PEITORIL / 2, z), (comp, PEITORIL, esp), "inox", 0.012)
        m.caixa((meio, (VERGA + ALTO) / 2, z), (comp, ALTO - VERGA, esp), "inox", 0.012)
        # A cinta azul com o filete, do lado de fora, e o forro por dentro.
        m.caixa((meio, 0.60, lado * (FORA + 0.004)), (comp - 0.02, 0.34, 0.008), "faixa", 0)
        m.caixa((meio, 0.80, lado * (FORA + 0.005)), (comp - 0.02, 0.035, 0.01), "filete", 0)
        m.caixa((meio, 0.2, lado * (FORA + 0.003)), (comp - 0.02, 0.012, 0.006), "aco", 0)      # friso de baixo
        m.caixa((meio, PEITORIL / 2 + 0.02, lado * (DENTRO - 0.006)), (comp - 0.04, PEITORIL - 0.1, 0.012), "forro", 0.004)
        m.caixa((meio, (VERGA + ALTO) / 2, lado * (DENTRO - 0.006)), (comp - 0.04, ALTO - VERGA - 0.04, 0.012), "forro", 0.004)

        # As colunas entre as janelas, o vidro e a borracha em volta.
        janelas = janelas_do_trecho(x0, x1)
        bordas = [x0] + [v for cx, lg in janelas for v in (cx - lg / 2, cx + lg / 2)] + [x1]
        for a, c in zip(bordas[0::2], bordas[1::2]):
            m.caixa(((a + c) / 2, (PEITORIL + VERGA) / 2, z), (c - a, VERGA - PEITORIL, esp), "inox", 0.012)
        for cx, lg in janelas:
            cy, ah = (PEITORIL + VERGA) / 2, VERGA - PEITORIL
            m.caixa((cx, cy, z), (lg, ah, 0.018), "vidro", 0)
            for dy in (-1, 1):
                m.caixa((cx, cy + dy * (ah / 2 - 0.02), lado * (FORA - 0.01)), (lg, 0.04, 0.03), "borracha", 0.006)
            for dx in (-1, 1):
                m.caixa((cx + dx * (lg / 2 - 0.02), cy, lado * (FORA - 0.01)), (0.04, ah, 0.03), "borracha", 0.006)
            # A janela basculante de cima.
            m.caixa((cx, VERGA - 0.26, lado * (FORA - 0.004)), (lg - 0.08, 0.02, 0.02), "aco", 0)


def portas(m, lado):
    """As portas de correr, abertas: cada folha estacionada por fora da
    parede, o trilho por cima, a soleira amarela e os batentes."""
    for x0, x1 in PORTAS:
        meio = (x0 + x1) / 2
        m.caixa((meio, ALTO - 0.03, lado * (FORA + 0.035)), (x1 - x0 + 2.3, 0.1, 0.07), "aco", 0.01)      # trilho
        m.caixa((meio, 0.008, lado * (FORA - 0.13)), (x1 - x0, 0.016, 0.3), "amarelo", 0.003)              # soleira
        m.caixa((meio, -0.03, lado * (FORA + 0.04)), (x1 - x0 + 0.1, 0.04, 0.1), "aco", 0.008)             # estribo
        for borda, sentido in ((x0, -1), (x1, 1)):
            cx = borda + sentido * 0.52
            zf = lado * (FORA + 0.035)
            m.caixa((cx, 0.98, zf), (0.98, 1.9, 0.04), "inox", 0.012)
            m.caixa((cx, 1.32, zf + lado * 0.012), (0.62, 0.78, 0.03), "vidro", 0)
            m.caixa((cx, 0.60, zf + lado * 0.022), (0.96, 0.34, 0.006), "faixa", 0)
            m.caixa((borda + sentido * 0.025, 0.98, zf), (0.05, 1.9, 0.05), "borracha", 0.01)              # a borda da folha
            m.caixa((borda, ALTO / 2, lado * (FORA + DENTRO) / 2), (0.07, ALTO, FORA - DENTRO + 0.02), "amarelo", 0.008)  # batente
            # Balaústre ao lado da porta, por dentro.
            m.tubo([(borda + sentido * 0.14, 0.0, lado * (DENTRO - 0.12)), (borda + sentido * 0.14, ALTO, lado * (DENTRO - 0.12))], 0.018, "cromado")
        m.caixa((meio, ALTO - 0.012, lado * (DENTRO - 0.2)), (1.7, 0.02, 0.09), "luz", 0)                 # a luz da porta


def cabine(m, ponta):
    """A testeira: dois painéis com para-brisa, um de cada lado da
    passagem, os faróis, o para-choque e o engate."""
    x = ponta * (COMP - 0.1)
    for lado in (-1, 1):
        z = lado * 1.8
        m.caixa((x, 0.45, z), (0.2, 0.9, 2.0), "inox", 0.03)
        m.caixa((x, (1.7 + ALTO) / 2, z), (0.2, ALTO - 1.7, 2.0), "inox", 0.03)
        for dz in (-0.92, 0.92):
            m.caixa((x, 1.3, z + dz), (0.2, 0.8, 0.16), "inox", 0.02)
        m.caixa((x, 1.3, z), (0.03, 0.8, 1.7), "vidro", 0)
        fora = ponta * (COMP + 0.005)
        m.caixa((fora, 0.92, z), (0.03, 0.04, 1.72), "borracha", 0.006)
        m.caixa((fora, 1.68, z), (0.03, 0.04, 1.72), "borracha", 0.006)
        m.caixa((fora, 0.55, z), (0.012, 0.34, 1.96), "faixa", 0)
        m.caixa((ponta * (COMP + 0.012), 0.75, z), (0.014, 0.035, 1.96), "filete", 0)
        # Limpador.
        m.caixa((ponta * (COMP + 0.03), 1.12, z + lado * 0.2), (0.02, 0.5, 0.025), "borracha", 0.004, giro_y=0)
        # Farol e lanterna, embutidos.
        m.cilindro((ponta * (COMP + 0.012), 0.28, z + lado * 0.45), 0.105, 0.05, "aco", "x")
        m.cilindro((ponta * (COMP + 0.03), 0.28, z + lado * 0.45), 0.085, 0.03, "farol", "x")
        m.cilindro((ponta * (COMP + 0.012), 0.28, z + lado * 0.2), 0.07, 0.05, "aco", "x")
        m.cilindro((ponta * (COMP + 0.03), 0.28, z + lado * 0.2), 0.052, 0.03, "lanterna", "x")
        # O batente da passagem.
        m.caixa((x, ALTO / 2, lado * 0.8), (0.24, ALTO, 0.07), "amarelo", 0.008)
    # Para-choque com frisos e o engate.
    for i in range(3):
        m.caixa((ponta * (COMP + 0.07), -0.08 - i * 0.07, 0), (0.14, 0.035, 5.2), "aco", 0.01)
    m.caixa((ponta * (COMP + 0.03), -0.15, 0), (0.08, 0.26, 5.3), "chassi", 0.02)
    m.caixa((ponta * (COMP + 0.25), -0.52, 0), (0.6, 0.2, 0.3), "chassi", 0.03)
    m.cilindro((ponta * (COMP + 0.6), -0.52, 0), 0.13, 0.16, "aco", "x")
    for lado in (-1, 1):                                    # mangueiras de ar
        m.tubo([(ponta * (COMP + 0.05), -0.4, lado * 0.45), (ponta * (COMP + 0.3), -0.62, lado * 0.4)], 0.022, "borracha")


def teto(m):
    """O teto: ombros arredondados, frisos ao comprido e as caixas baixas
    do ar-condicionado (quem anda aqui em cima é o lobo: nada alto)."""
    r, n = 0.2, 6
    arco = lambda cz, a0, a1: [(cz + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)),
                                ALTO + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]
    contorno = arco(-(FORA - r), 180, 90) + arco(FORA - r, 90, 0) + [(FORA, ALTO - 0.04), (-FORA, ALTO - 0.04)]
    m.perfil(contorno, -COMP, COMP, "teto")
    for z in (-1.9, -0.95, 0.95, 1.9):
        m.caixa((0, TETO + 0.008, z), (COMP * 2 - 0.4, 0.016, 0.05), "aco", 0.004)
    for x in (-4.5, 4.5):
        m.caixa((x, TETO + 0.025, 0), (3.2, 0.05, 1.5), "teto", 0.02)
        for i in range(5):
            m.caixa((x - 1.2 + i * 0.6, TETO + 0.052, 0), (0.36, 0.006, 1.2), "chassi", 0)
    # O forro por dentro, as luminárias e a grade de ventilação.
    m.caixa((0, ALTO - 0.045, 0), (COMP * 2 - 0.42, 0.01, DENTRO * 2 - 0.04), "forro", 0)
    for z in (-1.05, 1.05):
        m.caixa((0, ALTO - 0.058, z), (COMP * 2 - 1.2, 0.02, 0.16), "luz", 0)
        m.caixa((0, ALTO - 0.056, z), (COMP * 2 - 1.1, 0.012, 0.22), "aco", 0)
    m.caixa((0, ALTO - 0.054, 0), (COMP * 2 - 2.0, 0.008, 0.34), "chassi", 0)


def chassi(m):
    m.caixa((0, -0.07, 0), (COMP * 2, 0.14, FORA * 2), "chassi", 0.015)
    m.caixa((0, 0.003, 0), (COMP * 2 - 0.42, 0.006, DENTRO * 2 - 0.02), "piso", 0)
    for lado in (-1, 1):
        for x0, x1 in ((-8.7, -7.5), (-4.1, 4.1), (7.5, 8.7)):        # a saia, aberta sobre os truques
            m.caixa(((x0 + x1) / 2, -0.3, lado * (FORA - 0.03)), (x1 - x0, 0.32, 0.04), "chassi", 0.01)
    # Equipamento pendurado entre os truques: caixas, reservatórios de ar.
    for x, larg, alt, z in ((-2.6, 1.5, 0.5, 1.2), (-0.7, 1.9, 0.56, -1.1), (1.5, 1.3, 0.44, 1.25), (2.9, 1.0, 0.5, -1.2)):
        m.caixa((x, -0.14 - alt / 2, z), (larg, alt, 1.3), "chassi", 0.025)
        m.caixa((x, -0.14 - alt / 2, z + math.copysign(0.655, z)), (larg - 0.2, alt - 0.14, 0.012), "aco", 0)
    for z in (-0.3, 0.3):
        m.cilindro((0.4, -0.42, z), 0.17, 2.4, "aco", "x", 16)


def truque(m, cx):
    """Um truque: longarinas, travessas, dois eixos, quatro rodas de
    friso apoiadas no trilho, molas e sapatas de freio."""
    eixo_y = TRILHO_Y + RODA
    for lado in (-1, 1):
        m.caixa((cx, eixo_y + 0.12, lado * 1.02), (2.7, 0.16, 0.13), "chassi", 0.02)
        for dx in (-0.9, 0.9):
            m.caixa((cx + dx, eixo_y, lado * 1.02), (0.3, 0.3, 0.17), "chassi", 0.03)            # caixa de graxa
            m.cilindro((cx + dx, eixo_y + 0.3, lado * 1.02), 0.075, 0.26, "aco", "y", 12)        # mola
        m.cilindro((cx, eixo_y + 0.36, lado * 0.86), 0.12, 0.3, "borracha", "y", 16)             # bolsa de ar
    for dx in (-0.9, 0.9):
        m.cilindro((cx + dx, eixo_y, 0), 0.075, 2.2, "aco", "z", 16)
        for lado in (-1, 1):
            m.cilindro((cx + dx, eixo_y, lado * BITOLA), RODA, 0.13, "aco", "z", 32)
            m.cilindro((cx + dx, eixo_y, lado * (BITOLA - 0.075)), RODA + 0.035, 0.03, "aco", "z", 32)    # o friso
            m.cilindro((cx + dx, eixo_y, lado * (BITOLA + 0.07)), RODA * 0.55, 0.03, "chassi", "z", 24)   # o cubo
            m.caixa((cx + dx + math.copysign(0.5, dx), eixo_y + 0.05, lado * BITOLA), (0.1, 0.24, 0.12), "chassi", 0.02)
        m.cilindro((cx + dx, eixo_y, 0.25), 0.26, 0.05, "aco", "z", 24)                         # disco de freio
    for dx in (-0.45, 0.45):
        m.caixa((cx + dx, eixo_y + 0.12, 0), (0.16, 0.2, 2.0), "chassi", 0.02)
    m.caixa((cx, eixo_y + 0.2, 0), (0.5, 0.3, 0.7), "chassi", 0.03)                              # motor de tração
    m.cilindro((cx, eixo_y + 0.38, 0), 0.2, 0.12, "aco", "y", 20)                                # pino central


def salao(m):
    """Por dentro: os bancos ao comprido, os anteparos, os balaústres e o
    corrimão do teto com as alças."""
    for lado in (-1, 1):
        z = lado * 2.25
        m.caixa((0, 0.2, z), (5.0, 0.4, 0.5), "forro", 0.012)                                    # a base
        for i in range(10):
            x = -2.25 + i * 0.5
            m.caixa((x, 0.445, z - lado * 0.02), (0.46, 0.09, 0.56), "assento", 0.03)
            m.caixa((x, 0.74, lado * 2.52), (0.46, 0.42, 0.07), "assento", 0.03)
        for x in (-2.52, 2.52):                                                                  # anteparo e balaústre
            m.caixa((x, 0.62, z), (0.03, 1.2, 0.62), "vidro", 0)
            m.caixa((x, 0.2, z), (0.04, 0.4, 0.64), "aco", 0.008)
            m.tubo([(x, 0.0, lado * 1.93), (x, ALTO - 0.04, lado * 1.93)], 0.018, "cromado")
        # Corrimão do teto, com os suportes e as alças.
        zc = lado * 1.35
        m.tubo([(-8.3, 1.86, zc), (8.3, 1.86, zc)], 0.016, "cromado")
        for i in range(12):
            x = -8.25 + i * 1.5
            m.tubo([(x, 1.86, zc), (x, ALTO - 0.04, zc)], 0.012, "cromado", 8)
        for i in range(9):
            x = -2.0 + i * 0.5
            m.caixa((x, 1.76, zc), (0.02, 0.18, 0.008), "borracha", 0)
            m.caixa((x, 1.655, zc), (0.11, 0.035, 0.02), "forro", 0.008)
    # Bancos de canto, de costas para a cabine.
    for ponta in (-1, 1):
        for lado in (-1, 1):
            x, z = ponta * 8.45, lado * 1.75
            m.caixa((x, 0.2, z), (0.5, 0.4, 1.5), "forro", 0.012)
            for i in range(3):
                m.caixa((x - ponta * 0.02, 0.445, z - 0.5 + i * 0.5), (0.56, 0.09, 0.46), "assento", 0.03)
                m.caixa((ponta * 8.74, 0.74, z - 0.5 + i * 0.5), (0.07, 0.42, 0.46), "assento", 0.03)


def main():
    m = Modelo("trem_metro")
    materiais(m)
    for lado in (-1, 1):
        parede_lateral(m, lado)
        portas(m, lado)
    for ponta in (-1, 1):
        cabine(m, ponta)
    teto(m)
    chassi(m)
    for cx in (-5.8, 5.8):
        truque(m, cx)
    salao(m)
    m.salvar()


if __name__ == "__main__":
    main()
