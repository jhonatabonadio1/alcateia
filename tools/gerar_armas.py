#!/usr/bin/env python3
"""Gera os modelos das armas e dos braços em primeira pessoa.

    python3 tools/gerar_armas.py

Escreve as malhas em assets/armas/<id>/<parte>.obj e a ficha de todas em
assets/armas/armas.json (o que o jogo lê: scripts/visual/WeaponModel.cs).
Depois de rodar, abra o projeto no Godot (ou `godot --headless --import`)
para ele importar os .obj.

Cada arma é desenhada de lado, em metros, com a origem no punho:
x para a frente (para a boca do cano), y para cima. As chapas (caixa da
culatra, coronha, punho, carregador) são um perfil de lado com uma
espessura; canos, guarda-mãos e tubos são peças redondas ao longo do
cano. No jogo o cano aponta para −Z.

Os braços são um par de luvas com manga, em poses fixas (empunhando,
apoiando, fechada, aberta); quem anima é o jogo, movendo a mão inteira.
"""
import json
import math
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mapas import formas as F

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "assets", "armas")


class Arma:
    def __init__(self, ident):
        self.id = ident
        self.partes = {}      # (nó, material) -> Malha
        self.pontos = {}      # nome -> (x, y, z) no espaço do jogo
        self.maos = {}

    def _junta(self, no, material, malha):
        self.partes.setdefault((no, material), F.Malha()).juntar(malha)

    def chapa(self, material, perfil, espessura, lado=0.0, no=""):
        """Perfil de lado (x para a frente, y para cima), com espessura.
        `lado`: deslocamento para a direita do atirador."""
        self._junta(no, material, F.extrusao(perfil, espessura).mover(yaw=90).mover((lado, 0, 0)))

    def redondo(self, material, de, ate, raio, y, lado=0.0, lados=14, no="", perfil=None):
        """Peça redonda ao longo do cano, de x=`de` a x=`ate`. `perfil`:
        lista de (fração do comprimento, raio) para anéis e degraus."""
        comp = ate - de
        pts = perfil or [(0, raio), (1, raio)]
        casca = [(0, 0)] + [(r, t * comp) for t, r in pts] + [(0, comp)]
        self._junta(no, material, F.revolucao(casca, lados, suave=False).rodar("x", -90).mover((lado, y, -de)))

    def barra(self, material, secao, de, ate, y, lado=0.0, no=""):
        """Seção (direita, cima) esticada ao longo do cano: guarda-mão
        oitavado, trilho, coronha arredondada."""
        self._junta(no, material, F.extrusao(secao, ate - de).mover((lado, y, -(de + ate) / 2)))

    def caixa(self, material, de, ate, baixo, cima, espessura, lado=0.0, no=""):
        self._junta(no, material, F.caixa(espessura, cima - baixo, ate - de, (lado, (baixo + cima) / 2, -(de + ate) / 2)))

    def inclinar(self, graus):
        """Levanta a ponta da frente: a faca que aponta para cima na mão."""
        self.partes = {k: m.rodar("x", graus) for k, m in self.partes.items()}

    def ponto(self, nome, x, y, lado=0.0):
        self.pontos[nome] = (lado, y, -x)

    def mao(self, qual, pose, alvo, dedos, palma):
        """Onde vai uma mão. Tudo em (frente, cima, direita):
        `alvo` é o ponto da arma onde a palma encosta; `dedos`, para onde
        apontam os dedos esticados (do pulso para os nós); `palma`, para
        onde a palma olha. O jogo monta a mão a partir disso."""
        v = lambda t: [round(t[2], 4), round(t[1], 4), round(-t[0], 4)]
        self.maos[qual] = dict(pose=pose, alvo=v(alvo), dedos=v(dedos), palma=v(palma))


def arredondado(larg, alt, canto, n=3):
    """Retângulo de cantos arredondados (seção de guarda-mão, coronha)."""
    pts = []
    for cx, cy, a0 in ((larg / 2 - canto, alt / 2 - canto, 0), (-larg / 2 + canto, alt / 2 - canto, 90),
                       (-larg / 2 + canto, -alt / 2 + canto, 180), (larg / 2 - canto, -alt / 2 + canto, 270)):
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            pts.append((cx + canto * math.cos(a), cy + canto * math.sin(a)))
    return pts


def estrias(n, raio, fundo, de=0.0, ate=1.0):
    """Perfil com `n` gomos (guarda-mão do M16, bomba da escopeta)."""
    pts = []
    for i in range(n):
        a, b = de + (ate - de) * i / n, de + (ate - de) * (i + 1) / n
        m = (b - a)
        pts += [(a, fundo), (a + m * 0.15, raio), (b - m * 0.15, raio), (b, fundo)]
    return pts


# ====================================================================== armas

def m16a2():
    a = Arma("m16a2")
    eixo = 0.048
    a.chapa("metal", [(-0.06, -0.005), (-0.02, -0.03), (0.075, -0.03), (0.085, -0.045), (0.15, -0.045), (0.155, -0.02),
                      (0.215, -0.02), (0.225, 0.0), (0.225, 0.074), (-0.045, 0.074), (-0.06, 0.05)], 0.044)
    a.chapa("metal", [(-0.035, 0.074), (-0.005, 0.138), (0.165, 0.138), (0.19, 0.074), (0.165, 0.074), (0.15, 0.118),
                      (0.012, 0.118), (-0.003, 0.074)], 0.022)                      # alça de transporte
    a.caixa("metal", 0.005, 0.03, 0.138, 0.152, 0.03)                               # alça de mira
    a.chapa("polimero", [(-0.06, 0.058), (-0.06, 0.0), (-0.315, -0.048), (-0.335, -0.05), (-0.335, 0.05), (-0.32, 0.062)], 0.04)
    a.chapa("borracha", [(-0.335, -0.052), (-0.347, -0.05), (-0.347, 0.05), (-0.335, 0.052)], 0.044)
    a.chapa("polimero", [(-0.028, -0.03), (0.016, -0.03), (-0.006, -0.128), (-0.052, -0.12), (-0.04, -0.07)], 0.03)
    a.chapa("metal", [(0.016, -0.03), (0.075, -0.03), (0.075, -0.036), (0.068, -0.064), (0.012, -0.064), (0.01, -0.058),
                      (0.06, -0.058), (0.067, -0.036), (0.016, -0.036)], 0.012)     # guarda-mato
    a.chapa("metal", [(0.034, -0.03), (0.042, -0.03), (0.036, -0.052), (0.03, -0.05)], 0.006)   # gatilho
    a.redondo("metal", 0.222, 0.245, 0.034, eixo)                                    # anel delta
    a.redondo("polimero", 0.245, 0.535, 0.0, eixo, lados=16, perfil=estrias(9, 0.031, 0.027))
    a.redondo("metal", 0.535, 0.705, 0.0085, eixo, lados=10)
    a.chapa("metal", [(0.535, eixo - 0.01), (0.575, eixo - 0.01), (0.566, 0.118), (0.558, 0.142), (0.55, 0.142), (0.545, 0.118)], 0.014)
    a.redondo("metal", 0.705, 0.752, 0.0, eixo, lados=10, perfil=[(0, 0.0115), (0.2, 0.0115), (0.25, 0.0095), (0.4, 0.0095),
                                                                  (0.45, 0.0115), (0.6, 0.0115), (0.65, 0.0095), (0.8, 0.0095), (0.85, 0.0115), (1, 0.0115)])
    a.caixa("metal", 0.07, 0.15, 0.02, 0.05, 0.004, lado=0.0235)                     # janela de ejeção
    a.redondo("metal", 0.04, 0.062, 0.008, 0.04, lado=0.028, lados=8)                # auxílio de fechamento
    a.caixa("metal", -0.05, -0.035, 0.066, 0.08, 0.05, no="Ferrolho")                # alavanca de manejo
    a.chapa("metal_claro", [(0.088, -0.04), (0.148, -0.04), (0.168, -0.205), (0.108, -0.215)], 0.024, no="Magazine")
    a.ponto("Muzzle", 0.752, eixo)
    a.ponto("Ejecao", 0.11, 0.035, 0.03)
    a.mao("direita", "punho", (-0.012, -0.078, 0.017), (1, -0.3, 0.3), (0.35, 0, -1))
    a.mao("esquerda", "apoio", (0.36, 0.014, 0.0), (0.62, 0.1, 0.75), (0, 1, 0))
    return a


def ak47():
    a = Arma("ak47")
    eixo = 0.032
    a.chapa("metal", [(-0.05, 0.0), (-0.03, -0.026), (0.19, -0.026), (0.2, 0.0), (0.2, 0.046), (-0.05, 0.046)], 0.042)
    a.chapa("metal", [(-0.046, 0.046), (0.165, 0.046), (0.165, 0.068), (0.15, 0.079), (-0.03, 0.079), (-0.046, 0.066)], 0.038)
    a.chapa("metal", [(0.165, 0.046), (0.215, 0.046), (0.215, 0.07), (0.2, 0.086), (0.17, 0.086)], 0.03)      # bloco da alça
    a.chapa("madeira", [(-0.05, 0.04), (-0.05, 0.0), (-0.295, -0.07), (-0.312, -0.076), (-0.318, 0.018), (-0.3, 0.03)], 0.038)
    a.chapa("metal", [(-0.312, -0.08), (-0.324, -0.078), (-0.329, 0.02), (-0.318, 0.022)], 0.04)             # soleira
    a.chapa("madeira_escura", [(-0.022, -0.026), (0.02, -0.026), (0.0, -0.122), (-0.048, -0.116), (-0.036, -0.07)], 0.03)
    a.chapa("metal", [(0.02, -0.026), (0.078, -0.026), (0.078, -0.032), (0.07, -0.06), (0.016, -0.06), (0.014, -0.054),
                      (0.062, -0.054), (0.07, -0.032), (0.02, -0.032)], 0.012)
    a.chapa("metal", [(0.04, -0.026), (0.048, -0.026), (0.04, -0.05), (0.034, -0.048)], 0.006)
    a.barra("madeira", arredondado(0.05, 0.046, 0.012), 0.215, 0.4, 0.018)                                    # guarda-mão de baixo
    a.redondo("madeira", 0.225, 0.385, 0.018, 0.064, lados=12)                                               # guarda-mão de cima
    a.redondo("metal", 0.385, 0.505, 0.011, 0.064, lados=10)                                                 # tubo de gás
    a.redondo("metal", 0.4, 0.665, 0.0088, eixo, lados=10)
    a.chapa("metal", [(0.495, eixo - 0.012), (0.525, eixo - 0.012), (0.525, 0.078), (0.495, 0.078)], 0.024)   # bloco de gás
    a.chapa("metal", [(0.6, eixo - 0.012), (0.626, eixo - 0.012), (0.622, 0.088), (0.617, 0.104), (0.609, 0.104), (0.604, 0.088)], 0.014)
    a.chapa("metal", [(0.609, 0.098), (0.617, 0.098), (0.617, 0.118), (0.609, 0.118)], 0.026)                 # protetor da massa
    a.redondo("metal", 0.665, 0.7, 0.0, eixo, lados=10, perfil=[(0, 0.012), (0.75, 0.012), (1, 0.009)])
    a.chapa("metal", [(0.175, 0.086), (0.2, 0.086), (0.195, 0.098), (0.18, 0.098)], 0.02)                     # alça de mira
    a.caixa("metal", 0.03, 0.16, 0.012, 0.022, 0.004, lado=0.0225)                                            # seletor
    a.redondo("metal", 0.1, 0.118, 0.006, 0.058, lado=0.03, lados=8, no="Ferrolho")
    a.caixa("metal", 0.1, 0.118, 0.05, 0.066, 0.02, lado=0.024, no="Ferrolho")
    curva = [(0.072, -0.026), (0.132, -0.026), (0.15, -0.075), (0.186, -0.15), (0.226, -0.212), (0.176, -0.24),
             (0.136, -0.175), (0.1, -0.1), (0.082, -0.06)]
    a.chapa("metal", curva, 0.028, no="Magazine")
    for x, y in ((0.118, -0.075), (0.15, -0.14)):                                                           # nervuras do carregador
        a.chapa("metal", [(x - 0.02, y + 0.01), (x + 0.03, y - 0.004), (x + 0.03, y - 0.012), (x - 0.02, y + 0.002)], 0.032, no="Magazine")
    a.ponto("Muzzle", 0.7, eixo)
    a.ponto("Ejecao", 0.11, 0.05, 0.03)
    a.mao("direita", "punho", (-0.01, -0.072, 0.017), (1, -0.3, 0.3), (0.35, 0, -1))
    a.mao("esquerda", "apoio", (0.31, -0.008, 0.0), (0.62, 0.1, 0.75), (0, 1, 0))
    return a


def sg552():
    a = Arma("sg552")
    eixo = 0.04
    a.chapa("polimero_verde", [(-0.055, 0.0), (-0.03, -0.028), (0.07, -0.028), (0.08, -0.04), (0.145, -0.04), (0.15, -0.02),
                               (0.2, -0.02), (0.2, 0.066), (-0.04, 0.066), (-0.055, 0.05)], 0.044)
    a.caixa("metal", -0.03, 0.2, 0.066, 0.074, 0.022)                                                         # trilho
    a.chapa("metal", [(-0.02, 0.074), (0.01, 0.074), (0.01, 0.1), (-0.02, 0.094)], 0.03)                      # tambor da alça
    # Coronha rebatível, vazada: um C aberto para baixo.
    a.chapa("polimero_verde", [(-0.055, 0.055), (-0.055, 0.025), (-0.24, 0.02), (-0.255, -0.05), (-0.285, -0.055), (-0.285, 0.05),
                               (-0.27, 0.062)], 0.03)
    a.chapa("polimero_verde", [(-0.055, 0.0), (-0.055, -0.02), (-0.2, -0.04), (-0.255, -0.05), (-0.24, -0.02), (-0.2, -0.015)], 0.026)
    a.chapa("borracha", [(-0.285, -0.057), (-0.296, -0.055), (-0.296, 0.05), (-0.285, 0.052)], 0.034)
    a.chapa("polimero_verde", [(-0.026, -0.028), (0.018, -0.028), (0.0, -0.125), (-0.05, -0.118), (-0.038, -0.07)], 0.03)
    a.chapa("metal", [(0.018, -0.028), (0.07, -0.028), (0.07, -0.034), (0.064, -0.062), (0.014, -0.062), (0.012, -0.056),
                      (0.056, -0.056), (0.063, -0.034), (0.018, -0.034)], 0.012)
    a.chapa("metal", [(0.036, -0.028), (0.044, -0.028), (0.038, -0.05), (0.032, -0.048)], 0.006)
    a.barra("polimero_verde", arredondado(0.052, 0.062, 0.014), 0.2, 0.39, eixo - 0.002)
    for i in range(4):                                                                                       # respiros do guarda-mão
        a.caixa("metal", 0.225 + i * 0.04, 0.245 + i * 0.04, eixo + 0.004, eixo + 0.014, 0.056)
    a.redondo("metal", 0.39, 0.485, 0.009, eixo, lados=10)
    a.chapa("metal", [(0.39, eixo - 0.012), (0.42, eixo - 0.012), (0.42, 0.082), (0.412, 0.1), (0.398, 0.1), (0.39, 0.082)], 0.02)
    a.redondo("metal", 0.4, 0.412, 0.014, 0.1, lados=10)                                                      # túnel da massa
    a.redondo("metal", 0.485, 0.53, 0.0, eixo, lados=10, perfil=[(0, 0.0125), (0.3, 0.0125), (0.35, 0.01), (1, 0.011)])
    a.redondo("metal", 0.085, 0.1, 0.006, 0.05, lado=0.03, lados=8, no="Ferrolho")
    a.chapa("ambar", [(0.085, -0.036), (0.142, -0.036), (0.166, -0.19), (0.108, -0.202)], 0.026, no="Magazine")
    a.chapa("polimero_verde", [(0.104, -0.196), (0.17, -0.184), (0.172, -0.2), (0.106, -0.212)], 0.03, no="Magazine")
    a.ponto("Muzzle", 0.53, eixo)
    a.ponto("Ejecao", 0.1, 0.04, 0.03)
    a.mao("direita", "punho", (-0.012, -0.075, 0.017), (1, -0.3, 0.3), (0.35, 0, -1))
    a.mao("esquerda", "apoio", (0.29, 0.004, 0.0), (0.62, 0.1, 0.75), (0, 1, 0))
    return a


def ump45():
    a = Arma("ump45")
    eixo = 0.038
    a.chapa("polimero", [(-0.06, 0.0), (-0.035, -0.03), (0.06, -0.03), (0.07, -0.042), (0.14, -0.042), (0.145, -0.022),
                         (0.26, -0.022), (0.27, 0.0), (0.27, 0.064), (-0.045, 0.064), (-0.06, 0.05)], 0.048)
    a.caixa("metal", -0.04, 0.26, 0.064, 0.072, 0.022)                                                        # trilho de cima
    a.chapa("metal", [(-0.035, 0.072), (-0.01, 0.072), (-0.012, 0.092), (-0.033, 0.092)], 0.026)              # alça
    a.chapa("metal", [(0.225, 0.072), (0.25, 0.072), (0.246, 0.094), (0.229, 0.094)], 0.02)                   # massa
    a.chapa("polimero", [(-0.06, 0.052), (-0.06, 0.03), (-0.235, 0.03), (-0.245, -0.062), (-0.27, -0.066), (-0.27, 0.046),
                         (-0.255, 0.058)], 0.028)                                                             # coronha vazada
    a.chapa("borracha", [(-0.27, -0.068), (-0.28, -0.066), (-0.28, 0.046), (-0.27, 0.048)], 0.032)
    a.chapa("polimero", [(-0.03, -0.03), (0.016, -0.03), (-0.002, -0.128), (-0.052, -0.12), (-0.04, -0.07)], 0.032)
    a.chapa("polimero", [(0.016, -0.03), (0.07, -0.03), (0.07, -0.036), (0.064, -0.064), (0.012, -0.064), (0.01, -0.058),
                         (0.056, -0.058), (0.063, -0.036), (0.016, -0.036)], 0.014)
    a.chapa("metal", [(0.034, -0.03), (0.042, -0.03), (0.036, -0.052), (0.03, -0.05)], 0.006)
    a.caixa("polimero", 0.15, 0.26, -0.034, -0.022, 0.03)                                                     # trilho de baixo
    a.redondo("metal", 0.27, 0.335, 0.011, eixo, lados=10)
    a.redondo("metal", 0.335, 0.35, 0.013, eixo, lados=10)
    a.redondo("metal", 0.2, 0.218, 0.006, 0.048, lado=-0.032, lados=8, no="Ferrolho")                          # alavanca, à esquerda
    a.chapa("polimero", [(0.078, -0.042), (0.132, -0.042), (0.14, -0.225), (0.086, -0.225)], 0.03, no="Magazine")
    a.chapa("metal", [(0.086, -0.225), (0.14, -0.225), (0.14, -0.232), (0.086, -0.232)], 0.034, no="Magazine")
    a.ponto("Muzzle", 0.35, eixo)
    a.ponto("Ejecao", 0.1, 0.04, 0.03)
    a.mao("direita", "punho", (-0.012, -0.078, 0.018), (1, -0.3, 0.3), (0.35, 0, -1))
    a.mao("esquerda", "apoio", (0.21, -0.036, 0.0), (0.62, 0.1, 0.75), (0, 1, 0))
    return a


def m3():
    a = Arma("m3")
    eixo = 0.05
    a.chapa("metal", [(-0.05, 0.0), (-0.02, -0.022), (0.2, -0.022), (0.215, 0.0), (0.215, 0.07), (0.2, 0.078), (-0.03, 0.078),
                      (-0.05, 0.06)], 0.044)
    a.caixa("metal", 0.06, 0.17, -0.024, -0.02, 0.03)                                                         # janela de carga
    a.chapa("polimero", [(-0.05, 0.058), (-0.05, 0.0), (-0.02, -0.022), (-0.03, -0.085), (-0.07, -0.12), (-0.105, -0.11),
                         (-0.075, -0.04), (-0.3, -0.07), (-0.33, -0.072), (-0.33, 0.03), (-0.31, 0.044)], 0.04)   # coronha com punho
    a.chapa("borracha", [(-0.33, -0.074), (-0.345, -0.072), (-0.345, 0.03), (-0.33, 0.032)], 0.044)
    a.chapa("metal", [(-0.005, -0.022), (0.06, -0.022), (0.06, -0.028), (0.052, -0.056), (0.0, -0.056), (-0.002, -0.05),
                      (0.044, -0.05), (0.052, -0.028), (-0.005, -0.028)], 0.012)
    a.chapa("metal", [(0.02, -0.022), (0.028, -0.022), (0.022, -0.044), (0.016, -0.042)], 0.006)
    a.redondo("metal", 0.215, 0.74, 0.0115, eixo, lados=12)                                                   # cano
    a.redondo("metal", 0.215, 0.66, 0.013, eixo - 0.03, lados=12)                                             # tubo de munição
    a.redondo("metal", 0.66, 0.675, 0.015, eixo - 0.03, lados=12)
    a.caixa("metal", 0.655, 0.67, eixo - 0.03, eixo, 0.012)                                                   # abraçadeira
    a.redondo("polimero", 0.3, 0.5, 0.0, eixo - 0.03, lados=14, perfil=estrias(8, 0.027, 0.023), no="Bomba")
    a.caixa("metal", 0.725, 0.735, eixo + 0.011, eixo + 0.024, 0.006)                                         # massa
    a.chapa("metal", [(0.0, 0.078), (0.03, 0.078), (0.028, 0.094), (0.004, 0.094)], 0.028)                    # alça
    a.redondo("metal", 0.1, 0.116, 0.006, 0.052, lado=0.03, lados=8, no="Ferrolho")
    a.ponto("Muzzle", 0.74, eixo)
    a.ponto("Ejecao", 0.12, 0.045, 0.03)
    a.ponto("Carga", 0.115, -0.03)
    a.mao("direita", "punho", (-0.062, -0.07, 0.022), (1, -0.3, 0.3), (0.35, 0, -1))
    a.mao("esquerda", "apoio", (0.4, -0.01, 0.0), (0.62, 0.1, 0.75), (0, 1, 0))
    return a


def m9():
    a = Arma("m9")
    eixo = 0.045
    a.chapa("metal", [(-0.045, 0.028), (0.165, 0.028), (0.172, 0.036), (0.172, 0.058), (0.165, 0.064), (-0.04, 0.064),
                      (-0.05, 0.054)], 0.027, no="Ferrolho")                                                  # ferrolho
    a.caixa("metal", 0.03, 0.12, 0.056, 0.066, 0.02, no="Ferrolho")                                           # corte do cano
    a.caixa("metal_claro", 0.035, 0.115, 0.058, 0.0665, 0.014, no="Ferrolho")                                 # cano à mostra
    a.chapa("metal", [(-0.04, 0.064), (-0.032, 0.064), (-0.034, 0.07), (-0.04, 0.07)], 0.012, no="Ferrolho")   # alça
    a.chapa("metal", [(0.15, 0.064), (0.158, 0.064), (0.156, 0.07), (0.151, 0.07)], 0.006, no="Ferrolho")      # massa
    a.redondo("metal", 0.172, 0.182, 0.0075, eixo, lados=10)
    a.chapa("metal", [(-0.05, 0.028), (0.16, 0.028), (0.16, 0.012), (0.07, 0.008), (0.062, -0.004), (-0.02, -0.004),
                      (-0.035, 0.004), (-0.058, 0.012), (-0.06, 0.028)], 0.029)                                # armação
    a.chapa("metal", [(0.062, -0.004), (0.07, 0.008), (0.078, 0.004), (0.076, -0.03), (0.022, -0.034), (0.02, -0.028),
                      (0.068, -0.025), (0.068, -0.004)], 0.012)                                               # guarda-mato
    a.chapa("metal", [(0.036, -0.004), (0.044, -0.004), (0.04, -0.024), (0.034, -0.022)], 0.006)
    a.chapa("polimero", [(-0.035, 0.004), (-0.02, -0.004), (0.022, -0.004), (0.0, -0.112), (-0.05, -0.105), (-0.04, -0.05)], 0.03)
    a.chapa("borracha", [(-0.03, -0.012), (0.012, -0.012), (-0.004, -0.098), (-0.042, -0.092)], 0.035)        # talas
    a.chapa("metal", [(-0.058, 0.03), (-0.05, 0.03), (-0.056, 0.05), (-0.066, 0.046)], 0.008)                 # cão
    a.chapa("metal_claro", [(-0.03, -0.01), (0.008, -0.01), (-0.01, -0.112), (-0.044, -0.106)], 0.02, no="Magazine")
    a.chapa("polimero", [(-0.046, -0.105), (-0.006, -0.112), (-0.008, -0.12), (-0.048, -0.113)], 0.03, no="Magazine")
    a.ponto("Muzzle", 0.182, eixo)
    a.ponto("Ejecao", 0.06, 0.06, 0.02)
    a.mao("direita", "punho", (-0.012, -0.055, 0.018), (1, -0.3, 0.3), (0.35, 0, -1))
    a.mao("esquerda", "apoio", (-0.012, -0.118, 0.004), (0.5, 0.25, 0.83), (0.2, 1, -0.2))
    return a


def faca():
    a = Arma("faca")
    # Lâmina para a frente e um pouco para cima; fio embaixo.
    a.chapa("aco", [(0.045, -0.014), (0.21, -0.012), (0.245, 0.006), (0.255, 0.016), (0.2, 0.019), (0.045, 0.018)], 0.004)
    a.chapa("aco", [(0.045, 0.012), (0.2, 0.013), (0.2, 0.019), (0.045, 0.018)], 0.006)                       # lombo
    a.chapa("metal", [(0.034, -0.03), (0.046, -0.03), (0.046, 0.03), (0.034, 0.03)], 0.016)                   # guarda
    a.barra("borracha", arredondado(0.024, 0.032, 0.009), -0.075, 0.034, 0.0)
    for i in range(4):
        a.barra("polimero", arredondado(0.027, 0.035, 0.01), -0.06 + i * 0.024, -0.05 + i * 0.024, 0.0)
    a.barra("metal", arredondado(0.026, 0.034, 0.009), -0.088, -0.075, 0.0)                                   # pomo
    a.inclinar(58)
    a.mao("direita", "fechada", (-0.012, -0.02, 0.013), (1, 0.15, 0.1), (0.1, 0, -1))
    a.mao("esquerda", "aberta", (0.05, -0.05, -0.3), (1, 0.35, 0.3), (0, -1, 0.5))
    return a


def choque():
    a = Arma("choque")
    # Em pé na mão: desenhada deitada e levantada depois.
    corpo = [(0, 0), (0.026, 0), (0.03, 0.004), (0.03, 0.012), (0.024, 0.014), (0.024, 0.1), (0.03, 0.102), (0.03, 0.11),
             (0.026, 0.114), (0.012, 0.114), (0.012, 0.128), (0, 0.128)]
    a._junta("", "oliva", F.revolucao(corpo, 6, suave=False).mover((0, -0.05, 0)))
    for y in (0.025, 0.05, 0.075):                                                                           # furos (faixas escuras)
        a._junta("", "metal", F.revolucao([(0.0245, y), (0.0245, y + 0.012)], 12, suave=False).mover((0, -0.05, 0)))
    a._junta("", "metal", F.revolucao([(0, 0.128), (0.011, 0.128), (0.011, 0.148), (0, 0.148)], 10, suave=False).mover((0, -0.05, 0)))
    a._junta("", "metal_claro", F.caixa(0.012, 0.115, 0.003, (0, 0.035, -0.031)))                              # alavanca
    a._junta("", "metal_claro", F.caixa(0.012, 0.003, 0.03, (0, 0.092, -0.018)))
    a._junta("Pino", "metal_claro", F.toro(0.012, 0.0018, 14, 5).rodar("x", 90).mover((0.022, 0.085, 0)))
    a.mao("direita", "fechada", (0.0, 0.0, 0.026), (1, 0.1, 0.1), (0.1, 0, -1))
    a.mao("esquerda", "aberta", (0.05, -0.05, -0.3), (1, 0.35, 0.3), (0, -1, 0.5))
    return a


# ===================================================================== braços

def _dedo(base, comprimentos, largura, grossura, curvas, abre=0.0):
    """Um dedo: três falanges dobrando para a palma (−Y), a partir de
    `base`. Devolve a malha no espaço da mão."""
    m = F.Malha()
    pos, ang = base, 0.0
    for comp, curva in zip(comprimentos, curvas):
        ang += curva
        falange = F.caixa_chanfrada(largura, grossura, comp + 0.006, 0.0045).mover((0, 0, -comp / 2))
        m.juntar(falange.euler(rx=-ang, ry=abre, desloca=pos))
        a, b = math.radians(ang), math.radians(abre)
        # Direção do dedo depois de dobrar: para a frente (−Z) e para a palma (−Y).
        d = (-math.sin(b) * math.cos(a), -math.sin(a), -math.cos(b) * math.cos(a))
        pos = (pos[0] + d[0] * comp, pos[1] + d[1] * comp, pos[2] + d[2] * comp)
        grossura *= 0.93
        largura *= 0.95
    return m


def mao_direita(dedos, polegar, pulso=(0, 0, 0)):
    """A mão direita de luva com a manga, no espaço dela: pulso na origem,
    dedos para −Z, costas da mão para +Y, polegar do lado −X.

    dedos: quatro trios de dobra (graus) — indicador, médio, anelar, mínimo.
    polegar: (rx, ry, rz, dobra). pulso: (rx, ry, rz) do antebraço em
    relação à mão.

    Devolve {material: Malha}."""
    luva = F.caixa_chanfrada(0.088, 0.03, 0.1, 0.01).mover((0, 0, -0.05))
    comps = ((0.042, 0.026, 0.022), (0.046, 0.03, 0.024), (0.043, 0.028, 0.023), (0.034, 0.021, 0.02))
    for i, curvas in enumerate(dedos):
        x = -0.032 + i * 0.0215
        luva.juntar(_dedo((x, -0.002, -0.098), comps[i], 0.0195 if i < 3 else 0.017, 0.019, curvas, abre=(i - 1.5) * -3))
    rx, ry, rz, dobra = polegar
    base = F.caixa_chanfrada(0.026, 0.024, 0.046, 0.006).mover((0, 0, -0.02))
    ponta = F.caixa_chanfrada(0.022, 0.02, 0.036, 0.005).mover((0, 0, -0.016)).euler(rx=-dobra, desloca=(0, 0, -0.04))
    luva.juntar(base.juntar(ponta).euler(rx, ry, rz, (-0.04, -0.006, -0.03)))

    reforco = F.caixa_chanfrada(0.08, 0.012, 0.034, 0.005).mover((0, 0.019, -0.084))        # proteção dos nós
    reforco.juntar(F.caixa_chanfrada(0.06, 0.008, 0.04, 0.004).mover((0, 0.017, -0.04)))
    punho = F.revolucao([(0, 0), (0.037, 0), (0.039, 0.02), (0.036, 0.05), (0, 0.05)], 12, suave=False).rodar("x", 90)
    reforco.juntar(punho.escala(1.18, 0.82, 1).mover((0, 0, -0.012)))                       # o punho da luva

    manga = F.revolucao([(0, 0), (0.04, 0), (0.043, 0.015), (0.045, 0.1), (0.056, 0.27), (0.06, 0.36), (0, 0.36)], 14).rodar("x", 90)
    manga = manga.escala(1.12, 0.9, 1).euler(*pulso).mover((0, 0, 0.03))
    return {"luva": luva, "luva_reforco": reforco, "manga": manga}


POSES = {
    # Empunhando: indicador no gatilho, os outros três fechados no punho, polegar por cima.
    "punho": dict(dedos=((28, 38, 20), (72, 88, 45), (76, 90, 45), (80, 90, 45)), polegar=(-15, 62, 0, 30), pulso=(-30, 24, 0)),
    # Apoiando o guarda-mão por baixo: mão em concha.
    "apoio": dict(dedos=((48, 55, 30), (52, 58, 30), (52, 58, 30), (50, 55, 30)), polegar=(10, 48, 0, 12), pulso=(-22, 10, 0)),
    # Fechada: faca, granada.
    "fechada": dict(dedos=((80, 95, 55), (82, 96, 55), (84, 96, 55), (86, 96, 55)), polegar=(-10, 78, 0, 40), pulso=(-28, 26, 0)),
    # Aberta e solta: indo buscar o carregador.
    "aberta": dict(dedos=((22, 28, 18), (26, 30, 18), (28, 32, 18), (30, 34, 18)), polegar=(5, 40, 0, 10), pulso=(-12, 6, 0)),
}


# ===================================================================== saída

def gravar(pasta, nome, malha):
    os.makedirs(pasta, exist_ok=True)
    with open(os.path.join(pasta, nome + ".obj"), "w", encoding="utf-8") as fh:
        fh.write(malha.obj())


def main():
    # Só o que este script gera fica na pasta; os .import são do Godot.
    if os.path.isdir(OUT):
        for base, _, nomes in os.walk(OUT):
            for n in nomes:
                if n.endswith(".obj"):
                    os.remove(os.path.join(base, n))
    ficha = {"armas": {}, "maos": {}}
    for fazer in (m16a2, ak47, sg552, ump45, m3, m9, faca, choque):
        a = fazer()
        partes = []
        for i, ((no, material), malha) in enumerate(sorted(a.partes.items())):
            nome = f"{no.lower() or 'corpo'}_{material}"
            gravar(os.path.join(OUT, a.id), nome, malha)
            partes.append(dict(malha=f"res://assets/armas/{a.id}/{nome}.obj", material=material, no=no))
        lo = [min(p[i] for (_, _), m in a.partes.items() for t in m.tris for p, _ in t) for i in range(3)]
        hi = [max(p[i] for (_, _), m in a.partes.items() for t in m.tris for p, _ in t) for i in range(3)]
        ficha["armas"][a.id] = dict(partes=partes, pontos={k: [round(x, 4) for x in v] for k, v in a.pontos.items()},
                                    maos=a.maos, limites=[[round(x, 3) for x in lo], [round(x, 3) for x in hi]])
        print(f"{a.id}: {len(partes)} peças, {sum(len(m.tris) for m in a.partes.values())} triângulos, "
              f"{hi[2] - lo[2]:.2f} m de comprimento")
    for pose, p in POSES.items():
        direita = mao_direita(**p)
        ficha["maos"][pose] = {}
        for lado, espelha in (("direita", False), ("esquerda", True)):
            pecas = []
            for material, malha in direita.items():
                nome = f"{pose}_{lado}_{material}"
                gravar(os.path.join(OUT, "bracos"), nome, malha.espelhar_x() if espelha else malha)
                pecas.append(dict(malha=f"res://assets/armas/bracos/{nome}.obj", material=material))
            ficha["maos"][pose][lado] = pecas
    with open(os.path.join(OUT, "armas.json"), "w", encoding="utf-8") as fh:
        json.dump(ficha, fh, ensure_ascii=False, indent=1, sort_keys=True)
    print("ficha: assets/armas/armas.json")


if __name__ == "__main__":
    main()
