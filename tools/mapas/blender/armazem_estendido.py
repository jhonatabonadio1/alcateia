"""
Armazém (estendido) — o mesmo galpão, maior e mais alto, montado com as
peças do pacote "Low Poly FPS Map 4" (Unity Asset Store).

    pip install bpy numpy scipy
    python3 tools/mapas/blender/armazem_estendido.py <cena.fbx> <pacote.unitypackage>
    python3 tools/gerar_mapas.py armazem_estendido

O Armazém original (armazem.py) fica como está. Este script usa a mesma
leitura (armazem.carregar: cada peça da cena com a UV e a cor certas) e
remonta as peças:

- O miolo do galpão, 45 × 30 m, é o do Armazém, peça por peça, no mesmo
  lugar.
- As paredes andam para fora: as compridas 7,5 m (o galpão vai de 30 para
  45 m de fundo) e as das pontas 15 m (de 45 para 75 m de comprido). Cada
  parede é um módulo de 15 m do pacote (com a passarela, a escada, as
  janelas e o que está encostado nela: prateleiras, extintor, caixas na
  passarela), repetido; os pilares vão em cada emenda.
- Mais alto: o teto sobe de 9 para 13 m. A parede estica só acima de 5 m
  (acima da passarela e do corrimão): as janelas ficam mais altas, a
  passarela e a escada continuam iguais. O telhado (com as claraboias e
  as vigas) sobe inteiro e se alarga para os 45 m de fundo.
- Nas alas novas (os 15 m de cada ponta) há contêineres empilhados até
  três (8,7 m): uma pirâmide 1-2-3-2-1 no meio da ala, uma pilha de dois
  e contêineres soltos. As faixas novas junto às paredes compridas
  ficam como corredor, com pouca cobertura.
- A ala oeste é a leste girada 180° em volta do centro: o time azul nasce
  no canto −X/+Z, o laranja no oposto, e os dois veem o mesmo mapa.

Grava assets/mapas/pecas/armazem_estendido.glb e
tools/mapas/armazem_estendido.json, no mesmo formato do Armazém.
"""
import json
import os
import random
import shutil
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import armazem as A  # noqa: E402  (o bpy vem com ele)
import bpy  # noqa: E402

GLB = os.path.join(A.RAIZ, "assets", "mapas", "pecas", "armazem_estendido.glb")
JSON = os.path.join(A.RAIZ, "tools", "mapas", "armazem_estendido.json")

# Medidas do Armazém (m, no jogo): o miolo vai de x −22,5 a 22,5 e de z −15
# a 15; as paredes compridas ficam em z ±15, as das pontas em x ±22,6.
ALTURA_ANTES, ALTURA = 9.0, 13.0
CORTE = 5.0                                          # abaixo disso a parede não muda
ESTICA = (ALTURA - CORTE) / (ALTURA_ANTES - CORTE)
SOBE = ALTURA - ALTURA_ANTES
DX, DZ = 15.0, 7.5                                   # quanto cada parede anda para fora
LARGO = 45.0 + 2 * DX                                # 75 m
FUNDO = 30.0 + 2 * DZ                                # 45 m
ALARGA_TETO = FUNDO / 30.0
NIVEL = 2.9                                          # altura de um contêiner

# O que é da casca (anda com a parede ou com o teto), o que é do chão.
PAREDE = ("Wall_", "WallDecor_01", "WallDecor_02", "WallDecor_03", "WallDecor_04", "WallDecor_05")
TETO = ("Roof_", "WallDecor_07", "WallDecor_08")
PILAR = "WallPart_"
PISO = "Floor_"
# Encostado na parede (anda com ela, sem esticar): prateleiras e o que
# está nelas, os canos de parede, as arandelas e os extintores.
ENCOSTADO = ("Rack_", "WallDecor_06", "Light_05", "FireExtinguisher_")
# O que pode entrar num grupo de enfeites copiado para as áreas novas.
ENFEITE = ("Barrel_", "Pallet_", "Wheel_", "PostBox_", "Box_", "AmoBox_", "Sand_01", "WoodenPlank_", "Light_01")

# Ala leste (a oeste sai girando 180°): (comprimento, x, z, nível, yaw).
# "C" é o contêiner de 6 m, "L" o de 12 m (nenhum: comprido demais, fecha
# a passagem da ala); yaw 0 deita o comprido em X.
CONTEINERES = [
    # A pirâmide no meio da ala: 1-2-3-2-1 de z −5,1 a 5,1. Por fora, o
    # lobo sobe degrau a degrau até 8,7 m; por baixo, cinco túneis.
    ("C", 31.0, 0.0, 0, 0), ("C", 31.0, 0.0, 1, 0), ("C", 31.0, 0.0, 2, 0),
    ("C", 31.0, 2.55, 0, 0), ("C", 31.0, 2.55, 1, 0),
    ("C", 31.0, -2.55, 0, 0), ("C", 31.0, -2.55, 1, 0),
    ("C", 31.0, 5.1, 0, 0), ("C", 31.0, -5.1, 0, 0),
    # Ao norte: uma pilha de dois e um solto, com corredor dos dois lados.
    ("C", 25.5, 12.5, 0, 90), ("C", 25.5, 12.5, 1, 90),
    ("C", 33.5, 13.0, 0, 90),
    # Ao sul, na frente de quem nasce: um solto, de cobertura.
    ("C", 27.5, -13.0, 0, 90),
]

# Grupos de enfeites tirados do Armazém (retângulo x0, x1, z0, z1, só o
# que está no chão) e onde vão: (grupo, x, z, yaw). Ala leste e faixa
# norte; o resto sai pela simetria.
GRUPOS = {
    "barris_paletes": (-20.3, -17.0, -14.6, -13.1),
    "pneus": (-3.8, -2.7, -14.8, -13.8),
    "pneus_parede": (-22.6, -21.5, -2.9, -0.3),
    "barris_norte": (5.4, 7.2, 13.3, 14.9),
    "caixas_paletes": (21.2, 22.6, 9.3, 12.0),
    "caixote": (15.4, 18.9, 13.2, 14.9),
    "municao": (-22.5, -21.4, -8.7, -7.7),
    "sacos": (17.4, 19.1, -4.6, -0.8),
    "refletor": (20.9, 21.9, 8.1, 9.0),
}
ENFEITES = [
    ("caixote", 35.4, 18.9, 0),
    ("barris_paletes", 11.0, -18.0, 0),
    ("pneus_parede", 24.2, -3.0, 0),
    ("refletor", 36.6, 7.0, 0),
    ("refletor", 36.6, -9.0, 0),
    ("municao", 34.8, -11.0, 0),
    ("sacos", 31.0, -9.6, 90),
    ("barris_norte", -6.0, 17.4, 0),
    ("pneus", 9.5, 17.8, 0),
    ("sacos", 4.5, 17.6, 90),
    ("caixas_paletes", 20.6, 18.4, 90),
]


# ----------------------------------------------------------- geometria

def giro_y(graus):
    a = np.radians(graus)
    c, s = np.cos(a), np.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])


def esticar(g):
    g = g.copy()
    alto = g[:, 1] > CORTE
    g[alto, 1] = CORTE + (g[alto, 1] - CORTE) * ESTICA
    return g


class Colocada:
    """Uma cópia de uma peça: a função que leva os pontos dela (no jogo)
    para o lugar novo e, se for rígida, o giro e o deslocamento (para a
    caixa de colisão, a luz e a escada)."""
    def __init__(self, o, f, R=None, t=None):
        self.o, self.f, self.R, self.t = o, f, R, t


def rigida(o, graus, p, q):
    R = giro_y(graus)
    p, q = np.asarray(p, float), np.asarray(q, float)
    t = q - R @ p
    return Colocada(o, lambda g: g @ R.T + t, R, t)


def jogo(o):
    """Os pontos da peça nas coordenadas do jogo (m, Y para cima)."""
    V = np.empty(len(o.data.vertices) * 3)
    o.data.vertices.foreach_get("co", V)
    return (V.reshape(-1, 3) * 0.01) @ A.GIRO.T


def para_blender(g):
    # glTF com Y para cima: o X do jogo é o X do Blender, o Y é o Z e o Z
    # é o −Y (o exportador desfaz isso).
    return np.c_[g[:, 0], -g[:, 2], g[:, 1]]


# ---------------------------------------------------------------- main

def main(fbx, pacote):
    est = A.carregar(fbx, pacote)
    cena, pecas, origem, vidro, encaixe, cor_de = (
        est[k] for k in ("cena", "pecas", "origem", "vidro", "encaixe", "cor_de"))
    modelo = {o: encaixe[o][0] for o in encaixe}
    for o in vidro:
        modelo[o] = "VIDRO"
    pontos = {o: jogo(o) for o in modelo}
    lo = {o: p.min(0) for o, p in pontos.items()}
    hi = {o: p.max(0) for o, p in pontos.items()}
    centro = {o: (lo[o] + hi[o]) / 2 for o in modelo}

    # Confere a troca de eixos com a do conversor (o .glb do Armazém).
    o0 = next(iter(encaixe))
    from mathutils import Matrix
    gira = Matrix(((0, -1, 0, 0), (1, 0, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1)))
    w = gira @ o0.matrix_world @ o0.data.vertices[0].co
    assert np.allclose(np.array(w[:]), para_blender(pontos[o0][:1])[0], atol=1e-3), "eixos trocados"

    def e(o, *prefixos):
        return modelo[o].startswith(prefixos)

    # ------------------------------------------------ separa as peças
    teto, parede, pilar, piso, encostado, miolo = [], [], [], [], [], []
    racks = [o for o in modelo if e(o, "Rack_")]
    for o in modelo:
        c = centro[o]
        if e(o, *TETO) or (e(o, "VIDRO") and c[1] > 8.8) or (e(o, "Light_03", "Light_04") and c[1] > 7.5):
            teto.append(o)
        elif e(o, *PAREDE) or e(o, "VIDRO"):
            parede.append(o)
        elif e(o, PILAR):
            pilar.append(o)
        elif e(o, PISO):
            piso.append(o)
        elif (e(o, *ENCOSTADO)
              or (lo[o][1] > 3.4 and abs(c[2]) > 13.2)          # em cima da passarela
              or any(np.all(np.abs(c[[0, 2]] - centro[r][[0, 2]]) < (hi[r] - lo[r])[[0, 2]] / 2 + 0.15)
                     and c[1] < 2.6 for r in racks)):          # nas prateleiras
            encostado.append(o)
        else:
            miolo.append(o)

    def lado(o):
        """A parede a que a peça pertence: N, S, L (leste) ou O (oeste)."""
        c = centro[o]
        d = {"N": 15.0 - c[2], "S": c[2] + 15.0, "L": 22.6 - c[0], "O": c[0] + 22.6}
        return min(d, key=lambda k: abs(d[k]))

    def trecho(x):
        return "O" if x < -7.5 else ("L" if x > 7.5 else "M")

    colocadas = []

    # O miolo fica onde está.
    for o in miolo:
        colocadas.append(rigida(o, 0, (0, 0, 0), (0, 0, 0)))

    # As paredes compridas: cinco módulos de 15 m de cada lado, tirados
    # dos três do Armazém. As pontas usam o módulo da ponta (o canto fica
    # igual); o do meio, o do meio.
    MODULOS = [(-30.0, "O"), (-15.0, "L"), (0.0, "M"), (15.0, "O"), (30.0, "L")]
    MEIO = {"O": -15.0, "M": 0.0, "L": 15.0}
    for o in parede + encostado:
        ld = lado(o)
        if ld not in "NS":
            continue
        dz = DZ if ld == "N" else -DZ
        for x_novo, de in MODULOS:
            if trecho(centro[o][0]) != de:
                continue
            d = np.array([x_novo - MEIO[de], 0.0, dz])
            if o in encostado:
                colocadas.append(rigida(o, 0, (0, 0, 0), d))
            else:
                colocadas.append(Colocada(o, lambda g, d=d: esticar(g + d), np.eye(3), d))
    # As das pontas: três módulos de 15 m de cada lado, tirados dos dois.
    PONTAS = [(-15.0, "S", -DZ), (0.0, "N", -DZ), (15.0, "N", DZ)]
    for o in parede + encostado:
        ld = lado(o)
        if ld in "NS":
            continue
        dx = DX if ld == "L" else -DX
        metade = "N" if centro[o][2] >= 0 else "S"
        for _, de, dz in PONTAS:
            if metade != de:
                continue
            d = np.array([dx, 0.0, dz])
            if o in encostado:
                colocadas.append(rigida(o, 0, (0, 0, 0), d))
            else:
                colocadas.append(Colocada(o, lambda g, d=d: esticar(g + d), np.eye(3), d))
    # Os pilares, em cada emenda.
    for o in pilar:
        c, ld = centro[o], lado(o)
        if ld in "NS":
            if c[0] < 0:
                continue                      # um modelo por lado basta
            dz = DZ if ld == "N" else -DZ
            alvos = [np.array([x - c[0], 0.0, dz]) for x in (-22.5, -7.5, 7.5, 22.5)]
        else:
            dx = DX if ld == "L" else -DX
            alvos = [np.array([dx, 0.0, z - c[2]]) for z in (-7.5, 7.5)]
        for d in alvos:
            colocadas.append(Colocada(o, lambda g, d=d: esticar(g + d), np.eye(3), d))

    # O teto: cinco módulos de 15 m, que sobem 4 m e se alargam em Z.
    TELHADO = [(-30.0, "O"), (-15.0, "O"), (0.0, "M"), (15.0, "L"), (30.0, "L")]
    for o in teto:
        de_o = trecho(centro[o][0])
        for x_novo, de in TELHADO:
            if de != de_o:
                continue
            dx = x_novo - MEIO[de]
            if e(o, "Light_"):
                c = centro[o]
                d = np.array([dx, SOBE, c[2] * (ALARGA_TETO - 1)])
                colocadas.append(rigida(o, 0, (0, 0, 0), d))
            else:
                colocadas.append(Colocada(
                    o, lambda g, dx=dx: np.c_[g[:, 0] + dx, g[:, 1] + SOBE, g[:, 2] * ALARGA_TETO]))

    # O chão: placas de 5 m. As do miolo ficam; as novas, sorteadas entre
    # as do Armazém (sempre o mesmo sorteio).
    sorteio = random.Random(7)
    # As placas do Armazém têm o meio em x múltiplo de 5 e em z a 2,5 de
    # um múltiplo de 5; as novas seguem a mesma grade (as da beira passam
    # por baixo da parede).
    ocupado = {(int(round(centro[o][0] / 5)), int(np.floor(centro[o][2] / 5))) for o in piso}
    for o in piso:
        colocadas.append(rigida(o, 0, (0, 0, 0), (0, 0, 0)))
    for i in range(-int(LARGO // 10), int(LARGO // 10) + 1):
        for j in range(-int(FUNDO // 10) - 1, int(FUNDO // 10) + 1):
            if (i, j) in ocupado:
                continue
            x, z = i * 5.0, j * 5.0 + 2.5
            o = sorteio.choice(piso)
            c = centro[o]
            colocadas.append(rigida(o, sorteio.choice((0, 90, 180, 270)), (c[0], 0, c[2]), (x, 0, z)))

    # ------------------------------------- contêineres das alas novas
    # Cada porta é do contêiner cuja ponta está mais perto dela (no mesmo
    # nível, a menos de 1,2 m).
    portas = [o for o in miolo if e(o, "ShippingContainer_Door")]
    pontas = []
    for o in miolo:
        if modelo[o] not in A.CONTEINER:
            continue
        tam = hi[o] - lo[o]
        eixo = 0 if tam[0] > tam[2] else 2
        for s in (-1, 1):
            p = centro[o].copy()
            p[eixo] += s * tam[eixo] / 2
            pontas.append((o, p))
    dono = {}
    for d in portas:
        dist, o = min((np.linalg.norm(centro[d] - p), o) for o, p in pontas)
        if dist < 1.2:
            dono.setdefault(o, []).append(d)
    conjuntos = {"C": [], "L": []}
    for o in miolo:
        if modelo[o] not in A.CONTEINER:
            continue
        tam = hi[o] - lo[o]
        if abs(min(tam[0], tam[2]) - 2.5) > 0.05:
            continue                                           # torto: não serve de modelo
        comprido = 0 if tam[0] > tam[2] else 2
        tipo = "L" if tam[comprido] > 9 else "C"
        conjuntos[tipo].append(dict(o=o, c=centro[o], yaw=0 if comprido == 0 else 90, portas=dono.get(o, []),
                                    base=round(lo[o][1] / NIVEL)))
    for tipo, lista in conjuntos.items():
        # Os de porta fechada nas duas pontas (quatro portas) primeiro, e as
        # cores alternadas.
        lista.sort(key=lambda k: (-len(k["portas"]), modelo[k["o"]]))
        vez = {}
        for k in lista:
            vez[modelo[k["o"]]] = vez.get(modelo[k["o"]], -1) + 1
            k["vez"] = vez[modelo[k["o"]]]
        lista.sort(key=lambda k: (len(k["portas"]) < 4, k["vez"], modelo[k["o"]]))
        print(f"contêineres {tipo}: " + ", ".join(f"{modelo[k['o']]}({len(k['portas'])})" for k in lista))

    def conteiner(tipo, x, z, nivel, yaw, n):
        k = conjuntos[tipo][n % len(conjuntos[tipo])]
        p = (k["c"][0], k["base"] * NIVEL, k["c"][2])
        q = (x, nivel * NIVEL, z)
        for o in [k["o"]] + k["portas"]:
            colocadas.append(rigida(o, yaw - k["yaw"], p, q))

    contagem = {"C": 0, "L": 0}
    for tipo, x, z, nivel, yaw in CONTEINERES:
        for sinal in (1, -1):                                  # leste, e a oeste girada
            conteiner(tipo, sinal * x, sinal * z, nivel, yaw + (0 if sinal > 0 else 180), contagem[tipo])
            contagem[tipo] += 1

    # ------------------------------------------------------ enfeites
    for nome, (x0, x1, z0, z1) in GRUPOS.items():
        membros = [o for o in miolo + encostado
                   if e(o, *ENFEITE) and x0 <= centro[o][0] <= x1 and z0 <= centro[o][2] <= z1 and lo[o][1] < 2.0]
        GRUPOS[nome] = (membros, ((x0 + x1) / 2, 0, (z0 + z1) / 2))
        print(f"grupo {nome}: {len(membros)} peças ({', '.join(sorted({modelo[o] for o in membros}))})")
    for nome, x, z, yaw in ENFEITES:
        membros, p = GRUPOS[nome]
        for sinal in (1, -1):
            for o in membros:
                colocadas.append(rigida(o, yaw + (0 if sinal > 0 else 180), p, (sinal * x, 0, sinal * z)))

    # ---------------------------------------- colisão, luzes, escadas
    caixas, luzes, escadas, vidros, conta = [], [], [], [], {}
    for k in colocadas:
        o, nome = k.o, modelo[k.o]
        if nome == "VIDRO":
            P = (pecas[o].reshape(-1, 3) * 0.01) @ A.GIRO.T
            vidros += [[round(x, 4) for x in p] for p in k.f(P)]
            continue
        M = encaixe[o][1]
        if nome == "Wall_01":
            esc = A.escada_da_parede(M, len(escadas) + 1)
            esc["pe"] = [round(x, 4) for x in k.f(np.array([esc["pe"]]))[0]]
            escadas.append(esc)
        if nome.startswith(A.CASCA) or nome.startswith(A.SEM_COLISAO):
            if nome.startswith("Light_"):
                luz = A.luz_da_peca(nome, M, origem)
                luz["pos"] = [round(x, 3) for x in k.f(np.array([luz["pos"]]))[0]]
                luzes.append(luz)
            continue
        assert k.R is not None, nome
        conta[nome] = conta.get(nome, 0) + 1
        for sufixo, c, tam, R in A.caixas_da_peca(nome, M, origem, cor_de[nome]):
            caixas.append(A.caixa_json(f"{nome}_{conta[nome]}{sufixo}", k.R @ c + k.t, tam, k.R @ R,
                                       A.piso_de(nome), cor_de[nome]))

    # ------------------------------------------------------- desenho
    cola = bpy.context.collection
    casca, pedacos = [], {}
    for n, k in enumerate(colocadas):
        o = k.o
        if modelo[o] == "VIDRO":
            continue
        g = k.f(pontos[o])
        c = o.copy()
        c.data = o.data.copy()
        c.parent = None
        c.matrix_world = Matrix.Identity(4)
        c.data.vertices.foreach_set("co", para_blender(g).reshape(-1).astype(np.float32))
        c.data.update()
        cola.objects.link(c)
        if modelo[o].startswith(A.CASCA):
            d = c.copy()
            d.data = c.data.copy()
            cola.objects.link(d)
            casca.append(d)
        meio = (g.min(0) + g.max(0)) / 2
        i = int(np.clip((meio[0] + LARGO / 2) // 15, 0, LARGO // 15 - 1))
        j = int(np.clip((meio[2] + FUNDO / 2) // 15, 0, FUNDO // 15 - 1))
        pedacos.setdefault((i, j), []).append(c)
    for o in cena:
        bpy.data.objects.remove(o, do_unlink=True)

    A.pintar(est["paleta"])
    for (i, j), objs in sorted(pedacos.items()):
        A.juntar(objs, f"estendido_{i}{j}")
    A.juntar(casca, "casca-colonly")
    A.exportar(GLB)

    with open(JSON, "w", encoding="utf-8") as fh:
        json.dump(dict(origem="Low Poly FPS Map 4 (Unity), remontado por tools/mapas/blender/armazem_estendido.py",
                       caixas=caixas, luzes=luzes, escadas=escadas, vidros=vidros), fh, ensure_ascii=False, indent=0)
    print(f"{GLB}\n{JSON}: {len(caixas)} caixas, {len(luzes)} luzes, {len(escadas)} escadas, "
          f"{len(vidros) // 3} triângulos de vidro, {len(colocadas)} peças")
    shutil.rmtree(est["tmp"], ignore_errors=True)


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    if len(args) != 2:
        sys.exit(__doc__)
    main(*args)
