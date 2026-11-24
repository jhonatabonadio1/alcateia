"""
Armazém — mapa pequeno (46 × 30 m), fechado: um galpão de contêineres.

Não é feito por código: o desenho é o mapa pronto do pacote
"Low Poly FPS Map 4" (Unity Asset Store), convertido por
tools/mapas/blender/armazem.py, que grava o .glb
(assets/mapas/pecas/armazem.glb, com a casca do galpão já como colisão
de malha) e tools/mapas/armazem.json (uma caixa de colisão por peça,
as luzes do teto e os vidros). Este arquivo só monta a cena em volta:
colisão, luz, céu das claraboias, nascimentos e rotas.

Os contêineres são túneis: as duas pontas são abertas e as portas são
peças à parte (umas abertas, outras fechadas). Em cima deles, a 2,9 m,
há caixotes, barris e pranchas que ligam uma pilha à outra; a segunda
fileira empilhada fica a 5,8 m. O teto, a 9 m, tem claraboias.

As quatro perguntas do teste de mapa:
  1. Caminho que só o lobo faz: o teto dos contêineres (2,9 m, e 5,8 m
     na pilha de dois), ligado pelas pranchas por cima dos corredores.
  2. Onde o humano vê o lobo chegando: os corredores compridos junto às
     paredes do galpão (norte e sul), abertos de ponta a ponta.
  3. Corredor que o humano deve evitar: o miolo dos contêineres-túnel
     (2,5 m de largura, sem saída pelos lados).
  4. Cair em cima de quem não olha: da boca dos túneis e da beira das
     pilhas sobre o corredor do meio.
"""
import json
import os
import re

from . import formas as F
from .kit import Mapa, AZUL, LARANJA

AQUI = os.path.dirname(os.path.abspath(__file__))

# Cada time nasce junto, num grupo de 2 × 3 com 1,5 m entre um e outro,
# no canto que o Jhon marcou na planta: o azul em −X/+Z, o laranja no
# canto oposto (+X/−Z). Um ponto por jogador: 6, o máximo de um time
# (RoomSettings.MaxTeamSize). O grupo está onde o chão é livre a 1,5 m
# em volta, até 2,6 m de altura (tirado do .json). Mudou o .json?
# Confira (tools/testar.sh MapaArmazem).
NASCER = {
    AZUL: [(-18.75, 8.5), (-18.75, 10.0), (-18.75, 11.5),
           (-17.25, 8.5), (-17.25, 10.0), (-17.25, 11.5)],
    LARANJA: [(17.25, -11.5), (17.25, -10.0), (17.25, -8.5),
              (18.75, -11.5), (18.75, -10.0), (18.75, -8.5)],
}
ROTAS = {
    "sul": [(-18.1, -11.1), (-11.1, -11.1), (-5.1, -11.1), (-0.9, -11.6), (4.9, -11.1), (10.9, -11.1), (17.9, -11.1)],
    "meioSul": [(-18.6, -5.1), (-10.1, -4.6), (-4.9, -5.1), (-0.1, -5.1), (4.9, -5.1), (10.9, -2.6), (19.6, -5.1)],
    "centro": [(-18.4, 0.1), (-10.4, 0.4), (-5.1, -0.1), (1.4, -0.1), (4.9, -0.1), (10.6, -0.6), (16.1, -0.1)],
    "meioNorte": [(-18.1, 4.9), (-11.1, 5.1), (-5.1, 4.9), (-0.1, 4.9), (4.9, 4.9), (10.9, 4.9), (17.9, 4.9)],
    "norte": [(-18.1, 10.9), (-11.1, 10.9), (-5.1, 10.9), (-0.1, 11.9), (4.9, 11.9), (10.9, 10.9), (17.9, 10.6)],
    "diagonalA": [(-18.1, -10.1), (-9.1, -4.6), (1.4, -0.1), (8.9, 4.9), (17.9, 9.9)],
    "diagonalB": [(-18.1, 9.9), (-9.1, 5.1), (1.4, -0.1), (9.1, -2.6), (17.9, -10.1)],
}


class Armazem(Mapa):
    def tscn(self):
        texto = super().tscn()
        # O vidro das janelas e claraboias: transparente e sem sombra
        # (com sombra, o sol não entraria pelas claraboias).
        texto = re.sub(r'(\[sub_resource type="StandardMaterial3D" id="M_vidro"\]\n)albedo_color = Color\(([^)]*), 1\)',
                       r'\1transparency = 1\nalbedo_color = Color(\2, 0.35)', texto)
        texto = re.sub(r'(\[node name="Vidros" type="MeshInstance3D" parent="Vidro"\]\n(?:.*\n)*?material_override = SubResource\("M_vidro"\)\n)',
                       r'\1cast_shadow = 0\n', texto)
        return texto


def iluminar(m):
    """A luz do galpão (céu das claraboias, sol, ambiente, preenchimento).
    O Armazém (estendido) usa a mesma, com os alcances esticados."""
    # Galpão fechado: o céu só aparece pelas claraboias, e o sol entra por
    # elas em manchas no chão. O resto é a luz das luminárias e um
    # ambiente baixo, frio.
    # Perto da foto do mapa: a luz do sol e das luminárias rebate na parede
    # cor de areia e no chão (SDFGI) e esquenta o galpão; o ambiente plano
    # fica baixo, para haver contraste e sombra. AgX é o mapeamento de cor
    # da foto; a névoa fraca mostra o raio de sol das claraboias. O
    # ambiente a 0,25 deixava a sombra preta (dentro dos contêineres não se
    # via nada, print do Jhon de 17/11): 0,6, com a exposição mais alta.
    # Ainda escuro (17/11, 20h): ambiente 0,85, exposição 1,45, oclusão
    # 1,6 (era 2,5: escurecia todo canto) e uma luz de preenchimento sem
    # sombra, fria, do lado oposto ao sol.
    # "Não está profissional" (Jhon, 18/11, com referências estilizadas):
    # o AgX com exposição 1,45, ambiente 0,85 e GI 1,5 lavava tudo de
    # cinza — chão estourado, sombra sem peso. Testado em fotos Forward+
    # (tests/regression, FotoLuz, só na nuvem): ACES com branco 6 e
    # exposição 0,95; sol mais forte e quente contra ambiente frio e baixo
    # (0,38: a 0,25 a sombra ficou preta em 17/11); oclusão forte e larga
    # (2,5, raio 2, potência 2,2) que também escurece o que está no sol; GI
    # 0,7; névoa um pouco mais densa, com o sol pesando nela: o feixe das
    # claraboias aparece; brilho nas luminárias.
    m.ambiente = dict(ceu=dict(topo=(0.36, 0.52, 0.78), horizonte=(0.72, 0.78, 0.86), chao=(0.25, 0.24, 0.23),
                               energia=1.0),
                      luz=(0.52, 0.62, 0.95), luz_energia=0.38, exposicao=0.95, branco=6.0,
                      brilho=0.7, brilho_bloom=0.1, brilho_limiar=1.0,
                      ssao=2.5, ssao_raio=2.0, ssao_potencia=2.2, ssao_luz=0.2,
                      contraste=1.1, saturacao=1.2,
                      sdfgi=dict(energia=0.7, rebote=0.6, celula=0.2), ssil=1.0,
                      volumetrica=dict(densidade=0.009, cor=(1.0, 0.92, 0.8), anisotropia=0.55, alcance=48, ambiente=0.1))
    m.sol = dict(yaw=28, queda=52, cor=(1.0, 0.8, 0.58), energia=3.4, alcance=60, neblina=1.6)
    m.preenchimento = dict(yaw=208, queda=58, cor=(0.6, 0.72, 1.0), energia=0.15)
    # Sombra mais marcada sem mexer na luz (pedido do Jhon, 17/11, 22h):
    # borda nítida (era 1,5). O contraste subiu para 1,12 junto e escureceu
    # demais: voltou a 1,02.
    m.borrao_sombra = 0.5


# O Armazém original (montar(), o armazem.json e o pecas/armazem.glb) saiu do
# jogo em 24/11; daqui o Armazém de hoje (armazem_estendido) só usa a classe e a luz.
