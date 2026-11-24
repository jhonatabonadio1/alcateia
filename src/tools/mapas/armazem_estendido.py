"""
Armazém (estendido) — mapa médio (75 × 45 m), fechado, 13 m de pé-direito:
o Armazém no meio e duas alas novas, uma em cada ponta.

O desenho é montado por tools/mapas/blender/armazem_estendido.py com as
peças do mesmo pacote do Armazém ("Low Poly FPS Map 4", Unity Asset
Store): o miolo de 45 × 30 m é o do Armazém, peça por peça; as paredes
andam para fora (as compridas 7,5 m, as das pontas 15 m) e sobem de 9
para 13 m (a passarela e as escadas continuam a 3,63 m); o telhado sobe
junto. Ele grava o .glb (assets/mapas/pecas/armazem_estendido.glb) e
tools/mapas/armazem_estendido.json, no mesmo formato do Armazém. Este
arquivo só monta a cena em volta: colisão, luz, nascimentos e rotas.

O Armazém original continua igual (armazem.py).

As quatro perguntas do teste de mapa (o miolo responde como no Armazém):
  1. Caminho que só o lobo faz: a pirâmide de contêineres de cada ala
     (1-2-3-2-1, até 8,7 m), subindo degrau a degrau de 2,9 m, e a pilha
     de dois ao lado dela.
  2. Onde o humano vê o lobo chegando: os corredores novos junto às
     paredes compridas (de ponta a ponta, 75 m) e o vão entre a pirâmide
     e o miolo.
  3. Corredor que o humano deve evitar: os cinco túneis por baixo da
     pirâmide (contêineres de 2,5 m de largura, sem saída pelos lados).
  4. Cair em cima de quem não olha: do alto da pirâmide sobre o vão das
     alas, e da passarela sobre os corredores.

A ala oeste é a leste girada 180° em volta do centro: o azul nasce no
canto −X/+Z, o laranja no canto oposto, e os dois veem o mesmo mapa.
"""
import json
import os

from . import armazem
from . import formas as F
from .armazem import Armazem
from .kit import AZUL, LARANJA

AQUI = os.path.dirname(os.path.abspath(__file__))

# Cada time nasce junto, num grupo de 2 × 3 com 1,5 m entre um e outro,
# no canto da sua ala, onde o chão é livre a 1,5 m em volta. Um ponto por
# jogador: 6, o máximo de um time (RoomSettings.MaxTeamSize).
NASCER = {
    AZUL: [(-33.5, 18.5), (-33.5, 17.0), (-33.5, 15.5), (-35.0, 18.5), (-35.0, 17.0), (-35.0, 15.5)],
    LARANJA: [(33.5, -18.5), (33.5, -17.0), (33.5, -15.5), (35.0, -18.5), (35.0, -17.0), (35.0, -15.5)],
}
# No miolo, os pontos das rotas do Armazém; nas alas, achados por busca
# de caminho com 0,5 m de folga (fora da passarela e longe das escadas).
ROTAS = {
    "corredorNorte": [(-33.1, 17.5), (-27.8, 18.0), (-20.1, 17.7), (-11.3, 16.0), (-2.8, 15.7), (-0.1, 17.7), (2.7, 15.7), (11.4, 16.0), (19.9, 16.7), (28.7, 17.5), (32.9, 17.5)],
    "corredorSul": [(-33.1, -17.5), (-24.3, -17.5), (-20.1, -16.5), (-11.3, -16.3), (-2.6, -15.8), (-0.1, -17.5), (8.4, -19.8), (17.2, -19.8), (19.9, -17.5), (28.7, -17.5), (32.9, -17.5)],
    "sul": [(-33.1, -17.5), (-24.3, -17.5), (-17.6, -15.3), (-16.6, -12.5), (-18.1, -11.1), (-11.1, -11.1), (-5.1, -11.1), (-0.9, -11.6), (4.9, -11.1), (10.9, -11.1), (17.9, -11.1), (26.2, -7.8), (34.2, -8.0), (35.7, -9.0)],
    "meioSul": [(-35.6, -9.0), (-26.8, -7.8), (-18.6, -5.1), (-10.1, -4.6), (-4.9, -5.1), (-0.1, -5.1), (4.9, -5.1), (10.9, -2.6), (19.6, -5.1), (23.2, -7.5), (31.9, -7.5), (34.2, -7.0), (35.7, -0.5)],
    "centro": [(-35.6, 0.5), (-34.8, 6.5), (-25.3, 7.5), (-18.8, 1.7), (-18.4, 0.1), (-10.4, 0.4), (-5.1, -0.1), (1.4, -0.1), (4.9, -0.1), (10.6, -0.6), (16.1, -0.1), (16.9, 1.7), (24.9, 6.2), (29.7, 8.0), (34.2, 7.2), (35.7, -0.5)],
    "meioNorte": [(-35.6, 0.5), (-34.8, 6.5), (-25.3, 7.5), (-18.1, 4.9), (-11.1, 5.1), (-5.1, 4.9), (-0.1, 4.9), (4.9, 4.9), (10.9, 4.9), (17.9, 4.9), (26.4, 7.5), (35.2, 8.5), (35.7, 9.0)],
    "norte": [(-35.6, 9.0), (-32.3, 7.7), (-26.1, 8.2), (-22.8, 11.5), (-18.1, 10.9), (-11.1, 10.9), (-5.1, 10.9), (-0.1, 11.9), (4.9, 11.9), (10.9, 10.9), (17.9, 10.6), (19.2, 11.7), (20.4, 13.0), (21.7, 14.2), (25.2, 17.5), (32.9, 17.5)],
    "diagonalA": [(-33.1, -17.5), (-31.8, -16.3), (-27.6, -9.5), (-22.6, -7.0), (-20.1, -8.0), (-18.1, -10.1), (-9.1, -4.6), (1.4, -0.1), (8.9, 4.9), (17.9, 9.9), (20.7, 12.7), (21.7, 14.2), (25.2, 17.5), (32.9, 17.5)],
    "diagonalB": [(-33.1, 17.5), (-27.8, 18.0), (-26.1, 16.7), (-23.1, 13.7), (-18.1, 9.9), (-9.1, 5.1), (1.4, -0.1), (9.1, -2.6), (17.9, -10.1), (24.2, -16.3), (28.9, -17.5), (32.9, -17.5)],
}


def montar():
    m = Armazem("armazem_estendido", "Armazém", 75, 45)
    with open(os.path.join(AQUI, "armazem_estendido.json"), encoding="utf-8") as fh:
        dados = json.load(fh)

    m.material("concreto", None, (0.55, 0.55, 0.55))
    m.material("metal", None, (0.5, 0.2, 0.15), metal=1.0)
    m.material("madeira", "madeira", (1, 1, 1), 1.6)
    m.material("vidro", None, (0.40, 0.71, 1.0), aspereza=0.1, dupla=True)

    m.modelo("Galpao", "Armazem", "armazem_estendido", (0, 0, 0))
    # O material de cada face do galpão e o material de detalhe (o .glb vem
    # do Blender sem isso): tools/mapas/kits/detalhe.py. Pode rodar de novo.
    from .kits import detalhe
    detalhe.detalhar_armazem("armazem_estendido", quieto=True)

    m.caixa("Colisao", "Piso", (0, -0.5, 0), (79, 1, 49), "concreto", oculta=True)
    for nome, c, t in (("LimiteOeste", (-39.2, 8, 0), (1, 18, 49)), ("LimiteLeste", (39.2, 8, 0), (1, 18, 49)),
                       ("LimiteSul", (0, 8, -23.7), (80, 18, 1)), ("LimiteNorte", (0, 8, 23.7), (80, 18, 1)),
                       ("LimiteTeto", (0, 17.5, 0), (80, 1, 49))):
        m.caixa("Colisao", nome, c, t, "concreto", oculta=True)

    for c in dados["caixas"]:
        mat = {"metal": "metal", "madeira": "madeira"}.get(c["piso"], "concreto")
        m.caixa("Colisao", c["nome"], tuple(c["centro"]), tuple(c["tamanho"]), mat,
                rot=tuple(tuple(linha) for linha in c["base"]), oculta=True, cor=c.get("cor"))

    for e in dados["escadas"]:
        m.escada("Escadas", e["nome"], tuple(e["pe"]), e["yaw"], e["altura"], e["largura"])

    vidro = F.Malha()
    v = dados["vidros"]
    for i in range(0, len(v), 3):
        vidro.tri(tuple(v[i]), tuple(v[i + 1]), tuple(v[i + 2]))
    m.forma("Vidro", "Vidros", vidro, (0, 0, 0), "vidro")

    # As luminárias do teto estão a 12 m (no Armazém, a 8 m): o foco
    # precisa de mais alcance e mais força para chegar ao chão igual. São
    # 25 (no Armazém, 14): só as das fileiras ímpares (x = ±7,5 e ±22,5)
    # fazem sombra, para o custo da sombra não dobrar.
    tom = {"refletor": ((1.0, 0.86, 0.62), 1.0, 7), "arandela": ((1.0, 0.78, 0.5), 0.7, 4)}
    for i, l in enumerate(dados["luzes"], 1):
        if l["tipo"] == "teto":
            sombra = abs(round(l["pos"][0] / 7.5)) % 2 == 1
            m.foco("Luzes", f"Luz{i}", tuple(l["pos"]), (1.0, 0.92, 0.80), 10.0, 19, angulo=66, sombra=sombra)
        else:
            cor_, energia, alcance = tom[l["tipo"]]
            m.luz("Luzes", f"Luz{i}", tuple(l["pos"]), cor_, energia, alcance)

    for time, pontos in NASCER.items():
        for x, z in pontos:
            m.spawn(time, x, z)
    for nome, pontos in ROTAS.items():
        m.rota(nome, [(x, 0.5, z) for x, z in pontos])

    # A luz do Armazém, com o alcance da sombra, da névoa e do sol
    # esticados para o galpão maior.
    armazem.iluminar(m)
    m.ambiente["volumetrica"]["alcance"] = 72
    m.sol["alcance"] = 90
    return m
