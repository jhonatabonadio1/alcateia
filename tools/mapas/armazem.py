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


def montar():
    m = Armazem("armazem", "Armazém", 46, 30)
    with open(os.path.join(AQUI, "armazem.json"), encoding="utf-8") as fh:
        dados = json.load(fh)

    # Materiais só da colisão (o desenho é o do .glb): dizem o som do
    # passo e do tiro (kit.SUPERFICIES). Sem textura, é concreto.
    m.material("concreto", None, (0.55, 0.55, 0.55))
    m.material("metal", None, (0.5, 0.2, 0.15), metal=1.0)
    m.material("madeira", "madeira", (1, 1, 1), 1.6)
    m.material("vidro", None, (0.40, 0.71, 1.0), aspereza=0.1, dupla=True)

    m.modelo("Galpao", "Armazem", "armazem", (0, 0, 0))

    # Piso: uma laje só, no lugar das 54 placas do desenho.
    m.caixa("Colisao", "Piso", (0, -0.5, 0), (48, 1, 32), "concreto", oculta=True)
    # Paredes invisíveis por fora das do galpão (ninguém sai pela janela).
    for nome, c, t in (("LimiteOeste", (-24.2, 6, 0), (1, 14, 34)), ("LimiteLeste", (24.2, 6, 0), (1, 14, 34)),
                       ("LimiteSul", (0, 6, -16.2), (50, 14, 1)), ("LimiteNorte", (0, 6, 16.2), (50, 14, 1)),
                       ("LimiteTeto", (0, 13.5, 0), (50, 1, 34))):
        m.caixa("Colisao", nome, c, t, "concreto", oculta=True)

    for c in dados["caixas"]:
        mat = {"metal": "metal", "madeira": "madeira"}.get(c["piso"], "concreto")
        m.caixa("Colisao", c["nome"], tuple(c["centro"]), tuple(c["tamanho"]), mat,
                rot=tuple(tuple(linha) for linha in c["base"]), oculta=True, cor=c.get("cor"))

    # As escadas de marinheiro das paredes compridas, até as passarelas
    # (3,63 m). A escada é do jogo, igual em todo mapa (kit.escada).
    for e in dados["escadas"]:
        m.escada("Escadas", e["nome"], tuple(e["pe"]), e["yaw"], e["altura"], e["largura"])

    vidro = F.Malha()
    v = dados["vidros"]
    for i in range(0, len(v), 3):
        vidro.tri(tuple(v[i]), tuple(v[i + 1]), tuple(v[i + 2]))
    m.forma("Vidro", "Vidros", vidro, (0, 0, 0), "vidro")

    # Luz: as luminárias do teto são focos para baixo, com sombra (é o que
    # põe sombra embaixo de quem passa); os refletores de tripé e as
    # arandelas são luz de preenchimento, sem sombra.
    tom = {"refletor": ((1.0, 0.86, 0.62), 1.0, 7), "arandela": ((1.0, 0.78, 0.5), 0.7, 4)}
    for i, l in enumerate(dados["luzes"], 1):
        if l["tipo"] == "teto":
            m.foco("Luzes", f"Luz{i}", tuple(l["pos"]), (1.0, 0.92, 0.80), 6.0, 14, angulo=66)
        else:
            cor_, energia, alcance = tom[l["tipo"]]
            m.luz("Luzes", f"Luz{i}", tuple(l["pos"]), cor_, energia, alcance)

    for time, pontos in NASCER.items():
        for x, z in pontos:
            m.spawn(time, x, z)
    for nome, pontos in ROTAS.items():
        m.rota(nome, [(x, 0.5, z) for x, z in pontos])

    # Galpão fechado: o céu só aparece pelas claraboias, e o sol entra por
    # elas em manchas no chão. O resto é a luz das luminárias e um
    # ambiente baixo, frio.
    # Perto da foto do mapa: a luz do sol e das luminárias rebate na parede
    # cor de areia e no chão (SDFGI) e esquenta o galpão; o ambiente plano
    # fica baixo, para haver contraste e sombra. AgX é o mapeamento de cor
    # da foto; a névoa fraca mostra o raio de sol das claraboias. O
    # ambiente a 0,25 deixava a sombra preta (dentro dos contêineres não se
    # via nada, print do Jhon de 17/11): 0,6, com a exposição mais alta.
    m.ambiente = dict(ceu=dict(topo=(0.36, 0.52, 0.78), horizonte=(0.72, 0.78, 0.86), chao=(0.25, 0.24, 0.23),
                               energia=1.0),
                      luz=(0.84, 0.87, 0.96), luz_energia=0.6, exposicao=1.3, brilho=0.35,
                      contraste=1.02, saturacao=1.12, agx=True,
                      sdfgi=dict(energia=1.5, rebote=0.6, celula=0.2), ssil=0.9,
                      volumetrica=dict(densidade=0.006, cor=(1.0, 0.95, 0.88), anisotropia=0.55, alcance=48))
    m.sol = dict(yaw=28, queda=52, cor=(1.0, 0.88, 0.70), energia=2.4, alcance=60)
    return m
