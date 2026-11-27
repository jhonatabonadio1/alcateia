"""
Setor Zero — o centro de treinamento da WolfLeague: uma base no alto de
uma montanha de rocha, cercada de picos gelados, de dia. O campo tem
150 × 100 m e acaba num precipício. Não é mapa de partida: é o campo do
tutorial com a Iris (docs/TUTORIAL_ROTEIRO.md), fora da rotação das salas
(`Maps.Training`). Só abre pelo tutorial e por Jogar → Treinamento.

    python3 src/tools/mapas/kits/fundos.py picos      # o horizonte de picos (uma vez)
    python3 src/tools/mapas/letreiros.py setor_zero   # os letreiros (se mudar um)
    python3 src/tools/mapas/setor_zero.py             # a cena, os .glb e a miniatura
    python3 src/tools/mapas/setor_zero.py --vazios    # só confere se sobrou quadrado vazio

Precisa de numpy e Pillow.

A fonte do traçado é a planta v4 (docs/tutorial/planta_setor_zero.png,
de src/tools/tutorial/planta.py): 1 unidade = 1 m, origem no canto de
cima à esquerda, x para a direita e y para baixo. No Godot: X = x − 75,
Z = y − 50, Y para cima. Tudo aqui é escrito nas coordenadas da planta.

**O que o Jhon pediu depois das primeiras fotos (26/11)** e mudou o mapa:
"quero um mapa mais robusto, complexo, bonito, elegante. menos quadrado";
"ao invés de cidade, coloque o fundo de montanhas gelosas, como se o mapa
tivesse em cima de uma montanha, uma rocha cinza gigantesca; é um mapa
alto, o player pode pular e se matar, num precipício infinito"; "quero
pontos altos no mapa (com escadaria para subir), mapas baixos,
corredores"; "escritas no mapa sempre em inglês". Daí:

  - **os níveis**: a Linha de Fogo desce 2,4 m (uma escadaria larga) e
    fica aberta para o abismo, com um parapeito baixo; o Núcleo da Iris é
    um anfiteatro redondo, 1,2 m abaixo, com degraus em toda a volta; a
    Câmara de Metamorfose é uma rotunda 1,5 m acima, com três escadarias;
    a arena tem uma galeria a 4 m, com duas escadas; na Rua Ignição uma
    escada externa leva ao terraço dos prédios (6 m) e à passarela;
  - **as formas** (setor_zero_formas.py): parede curva de verdade,
    pilastra em talude, pórtico em trapézio, arco, cúpula de nervuras,
    cobertura inclinada, pilar redondo — o que o kit não tem;
  - **o lugar**: a rocha do cume, a beira, o mar de nuvens e os picos.

Duas camadas, como nos outros mapas:
  - `montar()`: o jogo — os pisos e os níveis, as paredes, as escadarias,
    os obstáculos, e o nó `Tutorial` da cena (zonas, marcas, portas de
    energia, bonecos). É o que os testes medem.
  - `acabamento()` (setor_zero_acabamento.py): o lugar — vegetação,
    mobília, letreiros, as áreas de apoio, a montanha.

Onde a planta foi corrigida pela física do jogo (docs/MEDIDAS.md e o
código; os números finais estão em docs/AJUSTES.md, "Setor Zero"):
  - o vão do humano tem 1,4 m (a planta dizia 4): o pulo andando avança
    ~2,0 m de centro a centro (medido no teste);
  - a viga deixa 1,62 m livres: a cápsula agachada do humano tem 1,5 m
    desde 26/11 e a de pé 1,75 — passa agachado, bate de pé;
  - as plataformas da arena têm 2,7 e 5 m (a planta dizia 3 e 6): o salto
    carregado pousa em cima do que tem até 3,2 m acima dos pés
    (`PounceMaxClear`); sobe-se na baixa do chão e na alta a partir dela;
  - o vão do lobo continua com 8 m (o salto alcança 12 m; a marca de
    saída fica a 1,2 m da borda);
  - toda escadaria é uma rampa de colisão de menos de 31° com os degraus
    desenhados por cima, meio degrau abaixo (o corpo sobe na velocidade
    de andar, sem tranco: `docs/MAPAS.md`, "Escadas");
  - na Linha de Fogo o alvo médio do meio e o distante saíram da fila do
    alvo próximo, e o posto fica em cima da linha de tiro.
"""
import math
import os
import sys

if __package__ in (None, ""):
    sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    __package__ = "mapas"

import numpy as np

from .kit import Mapa, xform, vec, giro_y
from .kits.montagem import Montagem, luz_de_jogo, lampadas
from .setor_zero_formas import Formas, W

ID, NOME = "setor_zero", "Setor Zero"
CX, CY = 75.0, 50.0                      # o centro da planta vira a origem do mundo

# ---- as medidas presas à física (docs/AJUSTES.md, "Setor Zero")
VAO_HUMANO = 1.4                         # largura do vão do corredor
VAO_X0 = 32.3                            # onde ele começa (x da planta)
VAO_FUNDO = 1.5                          # fundura dos vãos: sem dano de queda (humano 4 m, lobo 7 m) e sem subir de volta (1,1 m)
VIGA_LIVRE = 1.62                        # altura livre sob a viga: agachado (1,5 m) passa, de pé (1,75 m) bate
VIGA_X = (42.5, 43.5)
PLATAFORMA_BAIXA, PLATAFORMA_ALTA = 2.7, 5.0   # o salto pousa em cima do que tem até 3,2 m acima dos pés (PounceMaxClear)
ARENA_ALTURA = 14.0                      # o lobo não chega ao alto (a corrida na parede não sobe)
TETO = 30.0                              # o limite de cima
# ---- os níveis (o chão do campo é 0)
N_FOGO = -2.4                            # a Linha de Fogo, rebaixada, aberta para o abismo
N_NUCLEO = -1.2                          # o fundo do anfiteatro do Núcleo
N_CAMARA = 1.5                           # a rotunda da Câmara de Metamorfose
N_GALERIA = 4.0                          # a galeria da arena
N_TERRACO = 6.0                          # o terraço dos prédios da Rua Ignição
PARAPEITO = 0.9                          # o parapeito da beira: o humano passa pulando (é de propósito)
ABISMO = -9.0                            # daqui para baixo, quem cai morre (a zona `abismo`)
DEGRAU = 0.2

# ---- os vãos do chão (x0, y0, x1, y1)
VAO_CORREDOR = (VAO_X0, 47.0, VAO_X0 + VAO_HUMANO, 53.0)
VAO_ARENA = (46.0, 80.0, 54.0, 92.0)
VAOS = (VAO_CORREDOR, VAO_ARENA)
BEIRA = (45.0, 105.0)                    # o trecho da beira norte que fica aberto: a Linha de Fogo

# ---- as cores (chapas do kit; o nome diz o material — kits/detalhe.py: chapa_nomeada)
CORES = {
    "sz_piso": (0.46, 0.46, 0.46), "sz_piso_base": (0.42, 0.42, 0.42), "sz_piso_claro": (0.54, 0.54, 0.54), "sz_concreto": (0.50, 0.50, 0.50),
    "sz_concreto_claro": (0.63, 0.63, 0.63), "sz_concreto_escuro": (0.36, 0.37, 0.39), "sz_metal_pintado": (0.34, 0.38, 0.44),
    "sz_metal_pintado_escuro": (0.16, 0.18, 0.22), "sz_metal_pintado_verde": (0.25, 0.60, 0.40), "sz_metal_pintado_laranja": (0.95, 0.54, 0.18),
    "sz_metal_cru": (0.46, 0.46, 0.48), "sz_metal_cru_claro": (0.66, 0.67, 0.70), "sz_vidro": (0.52, 0.74, 0.86), "sz_vidro_azul": (0.28, 0.46, 0.62), "sz_folhagem": (0.34, 0.52, 0.22),
    "sz_folhagem_escura": (0.20, 0.36, 0.16), "sz_folhagem_clara": (0.40, 0.57, 0.24), "sz_folhagem_musgo": (0.29, 0.45, 0.20), "sz_borracha": (0.62, 0.30, 0.20), "sz_borracha_escura": (0.22, 0.23, 0.26),
    "sz_asfalto": (0.26, 0.27, 0.29), "sz_asfalto_escuro": (0.17, 0.18, 0.20), "sz_asfalto_claro": (0.33, 0.34, 0.36), "sz_calcamento": (0.70, 0.66, 0.60), "sz_calcamento_claro": (0.74, 0.68, 0.62),
    "sz_faixa_amarela": (0.95, 0.76, 0.05), "sz_faixa_branca": (0.93, 0.93, 0.93), "sz_agua": (0.22, 0.52, 0.74),
    "sz_rocha": (0.46, 0.46, 0.47), "sz_rocha_escura": (0.33, 0.34, 0.36), "sz_rocha_clara": (0.58, 0.58, 0.60), "sz_areia_neve": (0.91, 0.91, 0.91),
    "sz_terra": (0.40, 0.31, 0.22), "sz_brita": (0.55, 0.53, 0.50), "sz_madeira": (0.62, 0.42, 0.26),
    "sz_plastico_laranja": (0.95, 0.50, 0.10), "sz_tecido_laranja": (0.95, 0.50, 0.10), "sz_tecido_ciano": (0.25, 0.70, 0.80),
    "sz_tecido_branco": (0.92, 0.92, 0.90), "sz_lataria_laranja": (0.72, 0.34, 0.06), "sz_lataria_azul": (0.18, 0.36, 0.56),
    # o robô de treino (robo_treino.py): a blindagem clara, o grafite e o vidro fumê da viseira
    "sz_lataria_branca": (0.66, 0.67, 0.70), "sz_lataria_grafite": (0.13, 0.14, 0.17), "sz_vidro_escuro": (0.07, 0.10, 0.15),
    # o que só se vê de longe (os picos, as nuvens, o pé da encosta) é liso: a centenas de metros o grão do material
    # vira um reticulado na tela
    "sz_liso_neve": (0.91, 0.91, 0.91), "sz_liso_neve_sombra": (0.74, 0.80, 0.90), "sz_liso_nuvem": (0.82, 0.82, 0.82),
    "sz_liso_rocha": (0.40, 0.41, 0.43), "sz_liso_rocha_escura": (0.28, 0.29, 0.32), "sz_liso_rocha_clara": (0.52, 0.53, 0.56),
    "sz_liso_rocha_azul": (0.36, 0.40, 0.48),
}
# O LED: um quadrado aceso do atlas (a textura scifi_luz brilha ali). (u, v) no meio dele.
LEDS = {"sz_led_ciano": (0.7773, 0.5205), "sz_led_azul": (0.781, 0.781), "sz_led_violeta": (0.844, 0.781), "sz_led_verde": (0.906, 0.781),
        "sz_led_branco": (0.781, 0.844), "sz_led_vermelho": (0.844, 0.844), "sz_led_laranja": (0.781, 0.906)}


def P(x, y):
    """Da planta para o mundo (X, Z)."""
    return (x - CX, y - CY)


def sorte(*chave):
    """Um número de 0 a 1 que depende só da chave: o mesmo mapa a cada vez que se gera."""
    import zlib
    return (zlib.crc32(repr(chave).encode()) % 100000) / 100000.0


def da_paleta(K, rgb):
    """O ponto (u, v) do quadrante da paleta do atlas (o de cima à esquerda) de cor mais parecida com `rgb`, no meio de
    uma região lisa. `Kit.uv_da_cor` procura no atlas inteiro e às vezes cai num anúncio — que acende (`scifi_luz`)."""
    K.uv_da_cor(rgb)                                            # carrega a textura (256 px)
    t = K._tex[:128, :128]; d = ((t - np.array(rgb)) ** 2).sum(2)
    liso = sum(((np.roll(t, (dy, dx), (0, 1)) - t) ** 2).sum(2) for dy, dx in ((0, 2), (2, 0), (0, -2), (-2, 0)))
    y, x = np.unravel_index(np.argmin(d + liso * 4), d.shape)
    return ((x + 0.5) / 256, (y + 0.5) / 256)


def tinta(K, nome, rgb):
    """Cria a chapa `nome` (uma caixa de 1 m da cor `rgb` da paleta do kit; o nome diz o material)."""
    K.chapa(nome, rgb)
    K[nome].uv[:] = da_paleta(K, rgb)


def cores(K):
    for nome, rgb in CORES.items():
        tinta(K, nome, rgb)
    for nome, uv in LEDS.items():
        K.chapa(nome, (0.5, 0.5, 0.5))
        K[nome].uv[:] = uv


# ------------------------------------------------------------------ peças em coordenadas da planta

def bloco(M, mat, x0, y0, x1, y1, base, topo, colide="caixa", planta=True):
    """Uma caixa de (x0, y0) a (x1, y1) da planta, de `base` a `topo` de altura."""
    a, b = P(min(x0, x1), min(y0, y1))
    c, d = P(max(x0, x1), max(y0, y1))
    return M.chapa(mat, a, b, c, d, y=base, alto=topo - base, colide=colide, planta=planta)


def girado(M, mat, cx, cy, comp, fundo, base, topo, angulo, colide="caixa", planta=True):
    """Uma caixa de `comp` × `fundo` com o meio em (cx, cy), o comprimento na direção `angulo` (graus, na planta:
    0 = +x, 90 = +y)."""
    x, z = P(cx, cy)
    return M.por(mat, x, z, yaw=-angulo, y=base, escala=(comp, topo - base, fundo), colide=colide, planta=planta, pivo=True)


def tira(M, mat, x0, y0, x1, y1, y=0.0, alto=0.02):
    """Uma pintura ou faixa de LED rente ao chão (sem colisão)."""
    return bloco(M, mat, x0, y0, x1, y1, y, y + alto, colide=None, planta=False)


def menos(ret, buracos):
    """O retângulo menos os buracos, em retângulos."""
    out = [ret]
    for b in buracos:
        novo = []
        for (x0, y0, x1, y1) in out:
            bx0, by0, bx1, by1 = b
            if bx0 >= x1 or bx1 <= x0 or by0 >= y1 or by1 <= y0:
                novo.append((x0, y0, x1, y1)); continue
            if bx0 > x0: novo.append((x0, y0, bx0, y1))
            if bx1 < x1: novo.append((bx1, y0, x1, y1))
            ix0, ix1 = max(x0, bx0), min(x1, bx1)
            if by0 > y0: novo.append((ix0, y0, ix1, by0))
            if by1 < y1: novo.append((ix0, by1, ix1, y1))
        out = novo
    return [r for r in out if r[2] - r[0] > 0.01 and r[3] - r[1] > 0.01]


# ------------------------------------------------------------------ colisão que não é caixa reta

def _colisao(m):
    if "kit_colisao" not in m.materiais: m.material("kit_colisao", None, (0.5, 0.5, 0.5))


def invisivel(m, nome, x0, y0, x1, y1, base, topo, angulo=None, meio=None, tam=None):
    """Uma caixa só de colisão. Com `angulo`, girada: `meio` (x, y) e `tam` (comprimento, fundo)."""
    _colisao(m)
    if angulo is None:
        a, b = P(min(x0, x1), min(y0, y1)); c, d = P(max(x0, x1), max(y0, y1))
        m.caixa("Colisao", nome, ((a + c) / 2, (base + topo) / 2, (b + d) / 2), (c - a, topo - base, d - b), "kit_colisao", oculta=True, minimapa=False)
    else:
        X, Z = P(*meio)
        m.caixa("Colisao", nome, (X, (base + topo) / 2, Z), (tam[0], topo - base, tam[1]), "kit_colisao", rot=giro_y(-angulo), oculta=True, minimapa=False)


def tampa(m, x0, y0, x1, y1, de, ate=TETO, **k):
    """Uma parede invisível (`Barreira…`) de `de` a `ate` de altura: o lobo não corre nela (`Player.IsInvisibleLimit`)
    nem passa por ela."""
    m._tampas = getattr(m, "_tampas", 0) + 1
    invisivel(m, f"BarreiraParede{m._tampas}", x0, y0, x1, y1, de, ate, **k)


def solido(m, nome, pontos):
    """Um corpo de colisão convexo qualquer (o casco dos `pontos` do mundo): rampa, cunha, setor de anel."""
    k = getattr(m, "_convexos", 0) + 1
    m._convexos = k
    if k == 1:
        m.cru_nos += ['[node name="Convexos" type="Node3D" parent="."]', ""]
    pts = ", ".join(("0" if abs(v) < 5e-5 else f"{v:.4f}".rstrip("0").rstrip(".")) for p in pontos for v in p)
    m.cru_recursos += [f'[sub_resource type="ConvexPolygonShape3D" id="Cx{k}"]', f"points = PackedVector3Array({pts})", ""]
    m.cru_nos += [f'[node name="{nome}" type="StaticBody3D" parent="Convexos"]', "",
                  f'[node name="Colisao" type="CollisionShape3D" parent="Convexos/{nome}"]', f'shape = SubResource("Cx{k}")', ""]


def escadaria(m, F, nome, x0, y0, x1, y1, h0, h1, sobe, mat="sz_concreto_claro", guarda=(False, False), guarda_colide=False, fundo=0.3):
    """Uma escadaria no retângulo da planta, de `h0` (a ponta de baixo) a `h1`, subindo para `sobe` ("+x", "-x", "+y",
    "-y"). A colisão é uma cunha lisa (o corpo sobe na velocidade de andar); os degraus, de 20 cm, são desenhados meio
    degrau abaixo dela. `guarda`: corrimão do lado de menor e do de maior coordenada; `guarda_colide`: com colisão (a
    escada alta, de onde cair machuca)."""
    ao_x = sobe[1] == "x"; sinal = 1 if sobe[0] == "+" else -1
    a0, a1 = ((x0, x1) if sinal > 0 else (x1, x0)) if ao_x else ((y0, y1) if sinal > 0 else (y1, y0))      # de baixo para cima
    l0, l1 = (y0, y1) if ao_x else (x0, x1)
    def pt(a, l, h): return W(a, l, h) if ao_x else W(l, a, h)
    solido(m, nome, [pt(a0, l0, h0 - fundo), pt(a0, l1, h0 - fundo), pt(a1, l0, h0 - fundo), pt(a1, l1, h0 - fundo),
                     pt(a0, l0, h0), pt(a0, l1, h0), pt(a1, l0, h1), pt(a1, l1, h1)])
    n = max(2, int(round((h1 - h0) / DEGRAU))); dh = (h1 - h0) / n; da = (a1 - a0) / n
    for i in range(n):
        b0, b1 = a0 + da * i, a0 + da * (i + 1)
        topo = h0 + (i + 0.5) * dh
        F.tronco(mat, [pt(b0, l0, topo - dh), pt(b0, l1, topo - dh), pt(b1, l1, topo - dh), pt(b1, l0, topo - dh)],
                 [pt(b0, l0, topo), pt(b0, l1, topo), pt(b1, l1, topo), pt(b1, l0, topo)])
    # o miolo debaixo dos degraus (um só tronco: a face de cima acompanha a subida)
    F.tronco("sz_concreto", [pt(a0, l0, h0 - fundo), pt(a0, l1, h0 - fundo), pt(a1, l1, h0 - fundo), pt(a1, l0, h0 - fundo)],
             [pt(a0, l0, h0 - 0.02), pt(a0, l1, h0 - 0.02), pt(a1, l1, h1 - dh), pt(a1, l0, h1 - dh)])
    for lado, tem in zip((l0, l1), guarda):
        if not tem: continue
        dentro = 0.06 if lado == l0 else -0.06
        F.viga("sz_metal_pintado_escuro", pt(a0, lado + dentro, h0 + 0.95), pt(a1, lado + dentro, h1 + 0.95), 0.07)
        F.viga("sz_metal_pintado_escuro", pt(a0, lado + dentro, h0 + 0.5), pt(a1, lado + dentro, h1 + 0.5), 0.04)
        passos = max(2, int(abs(a1 - a0) / 1.5))
        for k in range(passos + 1):
            t = k / passos
            F.viga("sz_metal_pintado_escuro", pt(a0 + (a1 - a0) * t, lado + dentro, h0 + (h1 - h0) * t - 0.1), pt(a0 + (a1 - a0) * t, lado + dentro, h0 + (h1 - h0) * t + 0.95), 0.06)
        if guarda_colide:
            g0, g1 = (lado, lado + 0.12) if lado == l0 else (lado - 0.12, lado)
            solido(m, f"{nome}Guarda{'A' if lado == l0 else 'B'}", [pt(a, g, h) for a, hb in ((a0, h0), (a1, h1)) for g in (g0, g1) for h in (hb - 0.1, hb + 1.0)])


def guarda_corpo(m, F, nome, x0, y0, x1, y1, base, alto=1.0, colide=True):
    """Um guarda-corpo reto de (x0, y0) a (x1, y1): montantes, dois tubos e, com `colide`, uma chapa invisível."""
    L = math.hypot(x1 - x0, y1 - y0); n = max(1, int(round(L / 1.6)))
    for k in range(n + 1):
        t = k / n
        F.viga("sz_metal_pintado_escuro", W(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, base), W(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, base + alto), 0.06)
    for h, g in ((alto, 0.07), (alto * 0.55, 0.04)):
        F.viga("sz_metal_pintado_escuro", W(x0, y0, base + h), W(x1, y1, base + h), g)
    if colide:
        e = 0.05
        invisivel(m, nome, min(x0, x1) - (e if x0 == x1 else 0), min(y0, y1) - (e if y0 == y1 else 0), max(x0, x1) + (e if x0 == x1 else 0), max(y0, y1) + (e if y0 == y1 else 0), base, base + alto)


# ------------------------------------------------------------------ o chão
# Do mais importante para o menos: cada piso fica onde os de cima não estão. O resto é o pavimento de base.
# (material, retângulo, nível)
PISOS = [
    # o que fica abaixo do campo
    ("sz_concreto", (45.0, -3.0, 105.0, 34.0), N_FOGO),              # a Linha de Fogo, até a beira do abismo
    ("sz_piso_claro", (62.6, 37.6, 87.4, 62.4), N_NUCLEO),           # o fundo do anfiteatro do Núcleo (os degraus vão por cima)
    # o caminho do tutorial
    ("sz_borracha_escura", (49.0, 47.0, 61.0, 53.0), 0.0),           # trecho silencioso (piso de painel acústico)
    ("sz_piso_claro", (20.0, 47.0, 62.6, 53.0), 0.0),                # corredor
    ("sz_metal_pintado", (87.4, 47.0, 104.0, 53.0), 0.0),            # salão para a Câmara (grade de aço)
    ("sz_piso_claro", (102.0, 40.0, 122.0, 60.0), 0.0),              # debaixo da rotunda da Câmara
    ("sz_piso", (4.0, 38.0, 20.0, 62.0), 0.0),                       # Sala de Ativação
    ("sz_concreto", (72.0, 34.0, 78.0, 37.6), 0.0),                  # passagem norte
    ("sz_concreto", (72.0, 62.4, 78.0, 64.0), 0.0),                  # passagem sul
    ("sz_concreto", (109.0, 36.0, 115.0, 41.5), 0.0),                # passagens da Câmara
    ("sz_concreto", (109.0, 58.5, 115.0, 64.0), 0.0),
    ("sz_concreto", (40.0, 64.0, 120.0, 98.0), 0.0),                 # Arena da Matilha
    ("sz_asfalto", (105.4, 4.0, 146.0, 36.0), 0.0),                  # Rua Ignição
    ("sz_piso", (4.0, 70.0, 30.0, 96.0), 0.0),                       # Ponto de Retorno
    ("sz_piso", (30.0, 80.0, 40.0, 85.0), 0.0),                      # corredor oeste
    # o resto do campo
    ("sz_folhagem", (4.0, 3.0, 44.0, 35.0), 0.0),                    # Jardim Suspenso
    ("sz_borracha", (22.0, 37.0, 31.0, 44.0), 0.0),                  # Quadra de Agilidade
    ("sz_calcamento", (123.0, 39.0, 149.0, 63.0), 0.0),              # Praça do Uivo
    ("sz_terra", (121.0, 65.0, 147.0, 99.0), 0.0),                   # Pinhal da Matilha
    ("sz_concreto", (31.0, 64.0, 39.0, 98.0), 0.0),                  # pátio técnico
    ("sz_asfalto", (-3.0, -3.0, 153.0, 1.2), 0.0),                   # pista de serviço (norte; a Linha de Fogo a corta)
    ("sz_calcamento", (-3.0, 1.2, 3.4, 103.0), 0.0),                 # alameda (oeste)
    ("sz_folhagem_escura", (147.3, 1.2, 153.0, 99.3), 0.0),          # canteiro (leste)
    ("sz_borracha", (-3.0, 99.3, 153.0, 102.5), 0.0),                # pista de aquecimento (sul)
]
CAMPO = (-3.0, -3.0, 153.0, 103.0)


def chao(M, m):
    """O pavimento e os pisos de cada lugar, com colisão (2 m de grosso: as bordas dos vãos são a parede deles), e o
    fundo dos vãos. O campo acaba na beira da rocha: fora dele não há chão."""
    feitos = []
    for mat, ret, nivel in PISOS + [("sz_piso_base", CAMPO, 0.0)]:
        for r in menos(ret, list(VAOS) + feitos):
            bloco(M, mat, *r, nivel - 2.0, nivel, colide="caixa", planta=False)
        feitos.append(ret)
    for x0, y0, x1, y1 in VAOS:
        bloco(M, "sz_metal_pintado_escuro", x0, y0, x1, y1, -2.0, -VAO_FUNDO, colide="caixa", planta=False)


def limites(M, m):
    """As paredes invisíveis do mapa (`MapBounds`: `Limite…`), 15 m para fora da beira: quem pula cai, não bate. Em
    volta do campo, um parapeito de 1,1 m e, atrás da face dele, a barreira invisível — menos na beira da Linha de
    Fogo, que fica aberta (é dali que se cai)."""
    _colisao(m)
    x0, x1, z0, z1 = -18.0 - CX, 168.0 - CX, -18.0 - CY, 118.0 - CY
    L, H = x1 - x0, z1 - z0
    for nome, c, t in (("LimiteOeste", (x0 - 0.5, TETO / 2 - 20, 0), (1, TETO + 40, H + 2)), ("LimiteLeste", (x1 + 0.5, TETO / 2 - 20, 0), (1, TETO + 40, H + 2)),
                       ("LimiteNorte", (0, TETO / 2 - 20, z0 - 0.5), (L + 2, TETO + 40, 1)), ("LimiteSul", (0, TETO / 2 - 20, z1 + 0.5), (L + 2, TETO + 40, 1)),
                       ("LimiteTeto", (0, TETO + 0.5, 0), (L + 2, 1, H + 2))):
        m.caixa("Colisao", nome, c, t, "kit_colisao", oculta=True, minimapa=False)
    # (retângulo do parapeito, a barreira 6 cm atrás da face que dá para o campo — `Player.BackedByBarrier`)
    e = 0.06
    trechos = [((-3.6, -3.6, BEIRA[0] - 0.4, -3.0), (-3.6, -3.6, BEIRA[0] - 0.4, -3.0 - e)),
               ((BEIRA[1] + 0.4, -3.6, 153.6, -3.0), (BEIRA[1] + 0.4, -3.6, 153.6, -3.0 - e)),
               ((-3.6, 103.0, 153.6, 103.6), (-3.6, 103.0 + e, 153.6, 103.6)),
               ((-3.6, -3.0, -3.0, 103.0), (-3.6, -3.0, -3.0 - e, 103.0)),
               ((153.0, -3.0, 153.6, 103.0), (153.0 + e, -3.0, 153.6, 103.0))]
    for i, (muro, barra) in enumerate(trechos, 1):
        bloco(M, "sz_concreto", *muro, -0.4, 1.1)
        bloco(M, "sz_metal_pintado_escuro", muro[0] - 0.05, muro[1] - 0.05, muro[2] + 0.05, muro[3] + 0.05, 1.1, 1.22, colide=None, planta=False)
        invisivel(m, f"Barreira{i}", *barra, -0.4, TETO)


# ------------------------------------------------------------------ as paredes

# As janelas: aberturas sem vidro (o material de vidro do kit não deixa ver através), fechadas por uma barreira
# invisível — vê-se o jardim, não se passa. (de, até) ao longo da parede; do peitoril (1,1 m) à verga (3,1 m).
JANELAS = {
    "sala_norte": [(7.0, 17.0)],
    "corredor_norte": [(21.5, 29.5), (36.0, 41.0), (45.0, 48.0)],
    "corredor_sul": [(21.5, 29.5), (36.0, 41.0), (45.0, 48.0)],
}
PEITORIL, VERGA = 1.1, 3.1


def paredes(M, m):
    """As paredes retas de cada lugar. Em cima das que têm menos de 10 m vai uma barreira invisível até o teto: o lobo
    pula 2 m e ainda se pendura (a corrida na parede sobe ~1,9 m): sem ela, sairia do percurso por cima de um muro de
    4 m e, voltando a humano do lado de fora, não teria como voltar."""
    def p(x0, y0, x1, y1, h, mat="sz_concreto_claro", janelas=(), base=0.0):
        ao_x = (x1 - x0) > (y1 - y0)
        cursor = x0 if ao_x else y0
        for (a, b) in sorted(janelas):
            for (de, ate, de_h, ate_h) in ((cursor, a, base, h), (a, b, base, PEITORIL), (a, b, VERGA, h)):
                if ate - de > 0.01:
                    bloco(M, mat, *((de, y0, ate, y1) if ao_x else (x0, de, x1, ate)), de_h, ate_h)
            tampa(m, *((a, y0, b, y1) if ao_x else (x0, a, x1, b)), PEITORIL, VERGA)
            cursor = b
        fim = x1 if ao_x else y1
        if fim - cursor > 0.01:
            bloco(M, mat, *((cursor, y0, fim, y1) if ao_x else (x0, cursor, x1, fim)), base, h)
        if h < 10.0:
            tampa(m, x0, y0, x1, y1, h)

    # Sala de Ativação: 16 × 24 m, teto a 5 m, janela larga para o Jardim, porta do tamanho do corredor
    p(4.0, 38.0, 4.4, 62.0, 5.0)
    p(4.0, 61.6, 20.0, 62.0, 5.0)
    p(4.0, 38.0, 20.0, 38.4, 5.0, janelas=JANELAS["sala_norte"])
    p(19.6, 38.0, 20.0, 47.0, 5.0); p(19.6, 53.0, 20.0, 62.0, 5.0)

    # Corredor de Calibragem: 6 m de largura, paredes de 4 m, sem teto
    p(20.0, 46.6, 63.6, 47.0, 4.0, janelas=JANELAS["corredor_norte"])
    p(20.0, 53.0, 63.6, 53.4, 4.0, janelas=JANELAS["corredor_sul"])

    # Linha de Fogo: rebaixada 2,4 m; muros até 3,6 m (6 m por dentro); o norte é a beira, com parapeito
    h = 3.6
    p(44.6, -3.0, 45.0, 34.4, h, base=N_FOGO); p(105.0, -3.0, 105.4, 36.0, 6.0, base=N_FOGO)
    p(44.6, 34.0, 72.0, 34.4, h, base=N_FOGO); p(78.0, 34.0, 105.0, 34.4, h, base=N_FOGO)
    bloco(M, "sz_concreto", 45.0, -3.0, 105.0, -2.7, N_FOGO, N_FOGO + PARAPEITO)
    # as muretas da escadaria (1 m acima do alto dela) e as paredes da passagem até o Núcleo
    bloco(M, "sz_concreto_claro", 71.6, 28.6, 72.0, 34.0, N_FOGO, 1.0); bloco(M, "sz_concreto_claro", 78.0, 28.6, 78.4, 34.0, N_FOGO, 1.0)
    p(71.6, 34.4, 72.0, 38.8, 4.0); p(78.0, 34.4, 78.4, 38.8, 4.0)
    p(71.6, 61.2, 72.0, 64.0, 4.0); p(78.0, 61.2, 78.4, 64.0, 4.0)
    p(86.4, 46.6, 103.8, 47.0, 4.5); p(86.4, 53.0, 103.8, 53.4, 4.5)
    # passagens da Câmara (norte para a Rua Ignição, sul para a Arena)
    p(108.6, 35.6, 109.0, 41.8, 4.5); p(115.0, 35.6, 115.4, 41.8, 4.5)
    p(108.6, 58.2, 109.0, 64.0, 4.5); p(115.0, 58.2, 115.4, 64.0, 4.5)

    # Arena da Matilha: 80 × 34 m, paredes de 14 m (a do sul é a parede de corrida: lisa, sem janela nem quina)
    h = ARENA_ALTURA
    p(39.6, 63.6, 72.0, 64.0, h, "sz_concreto"); p(78.0, 63.6, 109.0, 64.0, h, "sz_concreto"); p(115.0, 63.6, 120.4, 64.0, h, "sz_concreto")
    p(39.6, 98.0, 120.4, 98.4, h, "sz_concreto")
    p(39.6, 64.0, 40.0, 80.0, h, "sz_concreto"); p(39.6, 85.0, 40.0, 98.0, h, "sz_concreto")
    p(120.0, 64.0, 120.4, 98.0, h, "sz_concreto")
    # por cima das três portas da arena, a parede continua (a verga): 14 m em toda a volta
    bloco(M, "sz_concreto", 72.0, 63.6, 78.0, 64.0, 6.6, h); bloco(M, "sz_concreto", 109.0, 63.6, 115.0, 64.0, 6.6, h)
    bloco(M, "sz_concreto", 39.6, 80.0, 40.0, 85.0, 4.0, h)
    for r in ((72.0, 63.6, 78.0, 64.0), (109.0, 63.6, 115.0, 64.0)):
        tampa(m, *r, 4.0, 6.6)
    # os dois cantos do sul, cortados a 45° (os do norte têm a galeria): a parede; a torre de cada um é do acabamento
    for (cx, cy, ang) in ((42.0, 96.0, 45.0), (118.0, 96.0, -45.0)):
        girado(M, "sz_concreto", cx, cy, 4.0 * math.sqrt(2) + 0.4, 0.4, 0.0, h, ang)
    # corredor oeste, para o Ponto de Retorno
    p(30.0, 79.6, 39.6, 80.0, 4.0); p(30.0, 85.0, 39.6, 85.4, 4.0)

    # Ponto de Retorno: 26 × 26 m, teto a 5 m
    p(4.0, 70.0, 30.0, 70.4, 5.0); p(4.0, 95.6, 30.0, 96.0, 5.0); p(4.0, 70.4, 4.4, 95.6, 5.0)
    p(29.6, 70.4, 30.0, 80.0, 5.0); p(29.6, 85.0, 30.0, 95.6, 5.0)

    # Rua Ignição: 40 × 32 m, muros de 6 m (o do oeste é o da Linha de Fogo)
    p(105.4, 3.6, 146.4, 4.0, 6.0); p(146.0, 4.0, 146.4, 36.0, 6.0)
    p(105.4, 35.6, 109.0, 36.0, 6.0); p(115.0, 35.6, 146.4, 36.0, 6.0)

    # vergas sobre as portas que dão para salas de teto alto
    bloco(M, "sz_concreto_claro", 19.6, 47.0, 20.0, 53.0, 4.0, 5.0)
    bloco(M, "sz_concreto_claro", 29.6, 80.0, 30.0, 85.0, 4.0, 5.0)


def parede_curva(m, F, mat, cx, cy, r, esp, base, topo, vaos, led=None):
    """Parede redonda de raio de dentro `r`, com as aberturas `vaos` = [(ângulo do meio, meia largura em m)]: o desenho
    é curvo de verdade (em talude: mais grossa ao pé), com rodapé e friso; a colisão são caixas escondidas a cada 7,5°,
    com a barreira invisível por cima até o teto."""
    cortes = sorted((a - math.degrees(math.asin(w / r)), a + math.degrees(math.asin(w / r))) for a, w in vaos)
    trechos = [(cortes[i][1], cortes[(i + 1) % len(cortes)][0] + (360 if i == len(cortes) - 1 else 0)) for i in range(len(cortes))]
    for a0, a1 in trechos:
        F.setor(mat, cx, cy, r, r + esp + 0.25, a0, a1, base, topo - 0.4, 4.0, r1_topo=r + esp)
        F.setor("sz_metal_pintado_escuro", cx, cy, r - 0.06, r + esp + 0.32, a0, a1, base, base + 0.45, 4.0, r0_topo=r - 0.01)
        F.setor("sz_metal_pintado_escuro", cx, cy, r - 0.08, r + esp + 0.08, a0, a1, topo - 0.4, topo, 4.0)
        if led:
            F.setor(led, cx, cy, r - 0.1, r - 0.02, a0, a1, topo - 0.52, topo - 0.42, 4.0)
        n = max(1, math.ceil((a1 - a0) / 7.5)); d = (a1 - a0) / n
        for k in range(n):
            mm = math.radians(a0 + (k + 0.5) * d); rm = r + esp / 2
            comp = 2 * (r + esp) * math.sin(math.radians(d) / 2) + 0.04
            meio = (cx + rm * math.cos(mm), cy + rm * math.sin(mm))
            m._curvas = getattr(m, "_curvas", 0) + 1
            invisivel(m, f"ParedeCurva{m._curvas}", 0, 0, 0, 0, base, topo, angulo=math.degrees(mm) + 90, meio=meio, tam=(comp, esp))
            tampa(m, 0, 0, 0, 0, topo, angulo=math.degrees(mm) + 90, meio=meio, tam=(comp, esp))


# ------------------------------------------------------------------ os lugares do percurso

def nucleo(M, m, F):
    """O Núcleo da Iris: um anfiteatro redondo. A beira (raio 12) fica no nível do campo; seis degraus descem em toda a
    volta até o piso (raio 9,2), 1,2 m abaixo, com o pedestal do holograma no meio. Quatro saídas de 6 m."""
    cx, cy, R, r = 75.0, 50.0, 12.0, 9.2
    parede_curva(m, F, "sz_concreto_claro", cx, cy, R, 0.5, 0.0, 5.0, [(0, 3.0), (90, 3.0), (180, 3.0), (270, 3.0)], led="sz_led_ciano")
    # a colisão dos degraus: 24 cunhas (cada uma convexa) — sobe-se por qualquer lado
    n = 24
    for k in range(n):
        t0, t1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        pts = []
        for t in (t0, t1):
            for rr, h in ((r, N_NUCLEO - 0.3), (13.3, N_NUCLEO - 0.3), (13.3, 0.0), (R, 0.0), (r, N_NUCLEO)):
                pts.append(W(cx + rr * math.cos(t), cy + rr * math.sin(t), h))
        solido(m, f"Anfiteatro{k + 1}", pts)
    # o desenho: a beira e os seis degraus, em anel
    F.anel("sz_piso_claro", cx, cy, R, 13.3, N_NUCLEO, 0.0, 5.0)
    passos = 6; dr = (R - r) / passos; dh = -N_NUCLEO / passos
    for k in range(passos):
        F.anel("sz_concreto_claro" if k % 2 else "sz_piso_claro", cx, cy, r + k * dr, R + 0.02, N_NUCLEO, N_NUCLEO + (k + 0.5) * dh, 5.0)
        if k in (0, 3):
            F.anel("sz_led_ciano", cx, cy, r + k * dr - 0.012, r + k * dr, N_NUCLEO + (k + 0.5) * dh - 0.06, N_NUCLEO + (k + 0.5) * dh - 0.02, 5.0)
    # o pedestal do holograma: dois degraus redondos e o plinto
    F.cilindro("sz_concreto_claro", cx, cy, 2.2, N_NUCLEO, N_NUCLEO + 0.2, 24)
    F.cilindro("sz_piso_claro", cx, cy, 1.6, N_NUCLEO + 0.2, N_NUCLEO + 0.4, 24)
    F.cilindro("sz_metal_pintado_escuro", cx, cy, 0.8, N_NUCLEO + 0.4, N_NUCLEO + 1.1, 12, r_topo=0.6)
    X, Z = P(cx, cy)
    m.cilindro("Colisao", "PedestalIris", (X, N_NUCLEO + 0.2, Z), 1.6, 0.4, "kit_colisao", oculta=True)
    m.cilindro("Colisao", "PlintoIris", (X, N_NUCLEO + 0.75, Z), 0.75, 0.7, "kit_colisao", oculta=True)
    # as quatro passagens: daí ao teto, barreira (as portas de energia têm 4 m)
    for nome, (x, y), comp, ao_x in PORTAS:
        if nome in ("sul", "camara_sul", "arena_oeste"): continue          # as da arena têm a parede de 14 m por cima
        tampa(m, *((x - comp / 2, y - 0.2, x + comp / 2, y + 0.2) if ao_x else (x - 0.2, y - comp / 2, x + 0.2, y + comp / 2)), 4.0)


def camara(M, m, F):
    """A Câmara de Metamorfose: uma rotunda de 18 m de diâmetro, 1,5 m acima do campo, com três escadarias (do salão,
    da arena e da Rua Ignição) e a parede até 7,5 m; a cúpula de nervuras é do acabamento."""
    cx, cy, R = 112.0, 50.0, 9.0
    parede_curva(m, F, "sz_concreto_claro", cx, cy, R, 0.5, 0.0, 7.5, [(90, 3.0), (180, 3.0), (270, 3.0)], led="sz_led_violeta")
    X, Z = P(cx, cy)
    m.cilindro("Colisao", "Rotunda", (X, N_CAMARA / 2 - 0.5, Z), R + 0.2, N_CAMARA + 1.0, "kit_colisao", oculta=True)
    F.cilindro("sz_piso_claro", cx, cy, R + 0.2, 0.0, N_CAMARA, 48)
    F.anel("sz_metal_pintado_escuro", cx, cy, R - 0.5, R + 0.02, N_CAMARA, N_CAMARA + 0.012, 5.0)
    # As escadarias acabam ANTES da lateral da rotunda (o cilindro de colisão tem 9,2 m de raio): a rampa que entrasse
    # nela encontrava a parede do cilindro abaixo do alto — um degrau de 15 a 30 cm (o teste `SetorZeroEscadarias`).
    # Entre o alto de cada escada e o círculo vai um patamar reto.
    escadaria(m, F, "EscadaCamaraOeste", 97.95, 47.0, 102.7, 53.0, 0.0, N_CAMARA, "+x")
    escadaria(m, F, "EscadaCamaraSul", 109.0, 59.3, 115.0, 63.5, 0.0, N_CAMARA, "-y")
    escadaria(m, F, "EscadaCamaraNorte", 109.0, 36.5, 115.0, 40.7, 0.0, N_CAMARA, "+y")
    for nome, (x0, y0, x1, y1) in (("O", (102.7, 47.0, 103.5, 53.0)), ("S", (109.0, 58.5, 115.0, 59.3)), ("N", (109.0, 40.7, 115.0, 41.5))):
        invisivel(m, f"PatamarCamara{nome}", x0, y0, x1, y1, N_CAMARA - 1.0, N_CAMARA)
        F.caixa("sz_piso_claro", x0, y0, x1, y1, 0.0, N_CAMARA)


def corredor(M, m, F):
    """As etapas físicas do bloco 1, em ordem: as marcas, o vão, a viga e o trecho silencioso."""
    # a viga: um pórtico de aço — a trave em 1,62 m e o painel até o alto da parede
    bloco(M, "sz_metal_pintado_escuro", VIGA_X[0], 47.0, VIGA_X[1], 53.0, VIGA_LIVRE, VIGA_LIVRE + 0.6)
    bloco(M, "sz_metal_pintado", VIGA_X[0] + 0.3, 47.0, VIGA_X[1] - 0.3, 53.0, VIGA_LIVRE + 0.6, 4.0)
    for y0 in (46.6, 52.6):                      # os montantes do pórtico, rente às paredes
        bloco(M, "sz_metal_pintado_escuro", VIGA_X[0] - 0.1, y0, VIGA_X[1] + 0.1, y0 + 0.8, 0.0, 4.2)
    tira(M, "sz_led_ciano", VIGA_X[0] - 0.02, 47.4, VIGA_X[0], 52.6, VIGA_LIVRE + 0.2, 0.08)
    # as faixas de LED do piso, rente às paredes, e o friso das bordas do vão
    for y0 in (47.0, 52.85):
        for x0, x1 in ((20.0, VAO_X0), (VAO_X0 + VAO_HUMANO, 62.6)):
            tira(M, "sz_led_ciano", x0, y0, x1, y0 + 0.15)
    for x in (VAO_X0 - 0.25, VAO_X0 + VAO_HUMANO):
        tira(M, "sz_faixa_amarela", x, 47.0, x + 0.25, 53.0)


def linha_de_fogo(M, m, F):
    """A Linha de Fogo: 60 × 37 m, 2,4 m abaixo do campo, aberta ao norte para o abismo. Desce-se pela escadaria da
    passagem do Núcleo (6 m de largura); a linha de tiro fica em y = 22 e os alvos, entre ela e a beira."""
    n = N_FOGO
    escadaria(m, F, "EscadaLinhaDeFogo", 72.0, 28.6, 78.0, 34.0, n, 0.0, "+y")
    # as cabines: divisórias na linha de tiro, a cada 10 m
    for x in (50, 60, 70, 80, 90, 100):
        bloco(M, "sz_metal_pintado", x - 0.06, 21.0, x + 0.06, 25.0, n, n + 1.8)
    # a bancada de recarga (de apoio, 0,9 m) e o arsenal (as estantes contra o muro oeste)
    bloco(M, "sz_metal_pintado_escuro", 46.6, 2.4, 54.6, 4.0, n, n + 0.9)
    bloco(M, "sz_metal_pintado_escuro", 46.6, 5.2, 50.0, 6.4, n, n + 0.9)
    bloco(M, "sz_metal_pintado_escuro", 51.2, 5.2, 54.6, 6.4, n, n + 0.9)
    for k in range(3):
        x, z = P(45.3, 8.4 + k * 2.4)
        M.por("SM_Props_05", x, z, yaw=90.0, y=n, colide="caixa")
    # coberturas baixas entre a linha e os alvos médios: 0,8 m (a altura do caixote; o humano sobe e se agacha atrás)
    for x in (62, 68, 82, 86):
        bloco(M, "sz_concreto", x, 9.6, x + 2.4, 10.3, n, n + 0.8)
    # o poço da granada: o muro de 2,4 m e, atrás dele, o alvo
    bloco(M, "sz_concreto", 94.0, 9.0, 104.0, 10.4, n, n + 2.4)
    # o depósito de alvos (de canto, fora da linha de tiro das cabines 1 a 4)
    bloco(M, "sz_metal_pintado", 95.5, 15.0, 103.0, 18.4, n, n + 2.2)
    # o trilho do alvo móvel (no piso) e os batentes das pontas
    tira(M, "sz_metal_cru", 78.0, 19.1, 100.0, 19.3, n, 0.05)
    for x in (77.8, 100.0):
        bloco(M, "sz_faixa_amarela", x, 18.7, x + 0.2, 19.7, n, n + 0.4)
    # as colunas da cobertura das cabines (a cobertura é do acabamento)
    for x in (50, 60, 70, 80, 90, 100):
        invisivel(m, f"ColunaCabine{x}", x - 0.2, 25.3, x + 0.2, 25.7, n, n + 2.6)


def arena(M, m, F):
    # as plataformas do bote: 6 × 6 m, de 2,7 e 5 m
    bloco(M, "sz_concreto", 61.0, 82.0, 67.0, 88.0, 0.0, PLATAFORMA_BAIXA)
    bloco(M, "sz_concreto", 72.0, 82.0, 78.0, 88.0, 0.0, PLATAFORMA_ALTA)
    # os pilares de 9 m (apoio do bote): redondos
    for x in (58, 63, 68, 73, 78):
        X, Z = P(x, 95.0)
        m.cilindro("Colisao", f"Pilar{x}", (X, 4.5, Z), 0.9, 9.0, "kit_colisao", oculta=True)
        F.cilindro("sz_concreto", x, 95.0, 0.95, 0.0, 9.0, 16, r_topo=0.8)
        F.cilindro("sz_metal_pintado_escuro", x, 95.0, 1.1, 0.0, 0.5, 16, r_topo=0.97)
        F.cilindro("sz_metal_pintado_escuro", x, 95.0, 0.86, 8.6, 9.0, 16, r_topo=1.05)
    # a rampa longa: 18°, de x 79 a 95, subindo para leste até o patamar de 5,2 m
    alto = (95.0 - 79.0) * math.tan(math.radians(18))
    solido(m, "RampaLonga", [W(x, y, h) for x, hh in ((79.0, 0.0), (95.0, alto)) for y in (87.6, 93.4) for h in (-0.3, hh)])
    F.tronco("sz_metal_pintado", [W(79.0, 87.6, -0.02), W(79.0, 93.4, -0.02), W(95.0, 93.4, -0.02), W(95.0, 87.6, -0.02)],
             [W(79.0, 87.6, 0.0), W(79.0, 93.4, 0.0), W(95.0, 93.4, alto), W(95.0, 87.6, alto)])
    for y in (87.6, 93.4):
        F.viga("sz_metal_pintado_escuro", W(79.0, y, 0.12), W(95.0, y, alto + 0.12), 0.18, 0.24)
    bloco(M, "sz_concreto", 95.0, 87.6, 99.0, 93.4, 0.0, alto)
    # a parede de corrida: o trecho da parede sul de x 84 a 116, com uma faixa de LED a cada metro (só desenho)
    for k in range(1, int(ARENA_ALTURA)):
        tira(M, "sz_led_azul", 84.0, 97.96, 116.0, 98.0, k - 0.03, 0.06)
    x0, y0, x1, y1 = VAO_ARENA
    for r in ((x0 - 0.25, y0 - 0.25, x1 + 0.25, y0), (x0 - 0.25, y1, x1 + 0.25, y1 + 0.25), (x0 - 0.25, y0, x0, y1), (x1, y0, x1 + 0.25, y1)):
        tira(M, "sz_faixa_amarela", *r)
    # a mata baixa do canto: a sebe ao longo da parede leste
    bloco(M, "sz_folhagem_escura", 118.0, 75.0, 119.4, 92.0, 0.0, 1.2)

    # ---- a galeria: uma passarela a 4 m ao longo da parede norte, com uma escada em cada ponta
    g = N_GALERIA
    bloco(M, "sz_concreto", 44.0, 64.0, 104.0, 66.6, g - 0.3, g)
    bloco(M, "sz_concreto", 40.0, 64.0, 44.0, 67.2, g - 0.3, g); bloco(M, "sz_concreto", 104.0, 64.0, 107.4, 67.2, g - 0.3, g)
    escadaria(m, F, "EscadaGaleriaOeste", 40.0, 67.2, 42.4, 74.2, 0.0, g, "-y", guarda=(False, True), guarda_colide=True)
    escadaria(m, F, "EscadaGaleriaLeste", 105.0, 67.2, 107.4, 74.2, 0.0, g, "-y", guarda=(True, True), guarda_colide=True)
    guarda_corpo(m, F, "GuardaGaleria", 44.0, 66.55, 104.0, 66.55, g)
    guarda_corpo(m, F, "GuardaGaleriaO", 42.4, 67.15, 44.0, 67.15, g); guarda_corpo(m, F, "GuardaGaleriaO2", 44.0, 66.6, 44.0, 67.2, g)
    guarda_corpo(m, F, "GuardaGaleriaL", 104.0, 67.15, 105.0, 67.15, g); guarda_corpo(m, F, "GuardaGaleriaL2", 104.0, 66.6, 104.0, 67.2, g)
    guarda_corpo(m, F, "GuardaGaleriaL3", 107.35, 64.0, 107.35, 67.2, g)
    # as colunas que a seguram, a cada 8 m, e a mão-francesa de cada uma
    for x in range(48, 104, 8):
        F.caixa("sz_concreto", x - 0.22, 66.1, x + 0.22, 66.54, 0.0, g - 0.3, chanfro=0.08)
        invisivel(m, f"ColunaGaleria{x}", x - 0.22, 66.1, x + 0.22, 66.54, 0.0, g - 0.3)
        F.viga("sz_metal_pintado_escuro", W(x, 64.05, g - 1.5), W(x, 66.2, g - 0.36), 0.14, 0.2)


def rua(M, m, F):
    t = N_TERRACO
    # os dois prédios (6 m) e a passarela entre eles; no terraço, um parapeito de 1 m (cair de 6 m machuca o humano)
    for x0, x1 in ((112.0, 124.0), (128.0, 140.0)):
        bloco(M, "sz_concreto_claro", x0, 10.0, x1, 18.0, 0.0, t)
        tira(M, "sz_led_laranja", x0, 17.98, x1, 18.06, t - 0.4, 0.12)
    e = 0.25
    for (x0, y0, x1, y1) in ((113.4, 10.0, 124.0, 10.25),                                                 # norte do oeste: aberto na escada
                             (112.0, 17.75, 124.0, 18.0), (112.0, 10.0, 112.0 + e, 17.75),
                             (124.0 - e, 10.25, 124.0, 12.6), (124.0 - e, 14.8, 124.0, 17.75),
                             (128.0, 10.0, 140.0, 10.25), (128.0, 17.75, 140.0, 18.0), (140.0 - e, 10.25, 140.0, 17.75),
                             (128.0, 10.25, 128.0 + e, 12.6), (128.0, 14.8, 128.0 + e, 17.75)):
        bloco(M, "sz_metal_pintado_escuro", x0, y0, x1, y1, t, t + 1.0)
    bloco(M, "sz_metal_pintado", 124.0, 12.6, 128.0, 14.8, t - 0.3, t)
    for y0 in (12.6, 14.6):
        bloco(M, "sz_metal_pintado_escuro", 124.0, y0, 128.0, y0 + 0.2, t, t + 1.0)
    # a escada externa do prédio oeste, ao longo da face norte, até o terraço
    bloco(M, "sz_concreto_claro", 112.0, 8.5, 113.4, 10.0, 0.0, t)
    escadaria(m, F, "EscadaTerraco", 113.4, 8.5, 124.0, 9.9, 0.0, t, "-x", guarda=(True, False), guarda_colide=True)
    guarda_corpo(m, F, "GuardaPatamarN", 112.0, 8.55, 113.4, 8.55, t); guarda_corpo(m, F, "GuardaPatamarO", 112.05, 8.5, 112.05, 10.0, t)
    # os portões de onda A, B e C (de onde saem os bonecos do bloco 4)
    for x in (111.0, 126.0, 141.0):
        for dx in (-1.8, 1.6):
            bloco(M, "sz_metal_pintado_escuro", x + dx, 19.5, x + dx + 0.2, 20.9, 0.0, 3.0)
        bloco(M, "sz_metal_pintado_escuro", x - 1.8, 19.5, x + 1.8, 20.9, 3.0, 3.3)
        tira(M, "sz_led_laranja", x - 1.6, 20.0, x + 1.6, 20.4, 3.0, 0.08)
    # coberturas (caixas de 1,2 m) e barricadas (a barreira de concreto do kit, em fila)
    for x, y in ((120, 27), (132, 26), (126, 31)):
        bloco(M, "sz_metal_pintado", x, y, x + 3.0, y + 1.6, 0.0, 1.2)
    for x, y, w in ((109, 29, 4), (113, 25.5, 4), (134, 24.5, 4), (120, 23, 4), (138, 32, 4)):
        for k in range(int(w / 2)):
            X, Z = P(x + 1.0 + 2.0 * k, y + 0.5)
            M.por("SM_Floor_RoadPart_01", X, Z, yaw=0.0, y=0.0, escala=(1.0, 1.2, 1.6), colide="caixa")
    # o painel do KP (o telão contra o muro leste)
    bloco(M, "sz_metal_pintado_escuro", 145.4, 26.0, 146.0, 34.0, 1.0, 5.6)
    bloco(M, "sz_led_laranja", 145.3, 26.4, 145.4, 33.6, 1.4, 5.2, colide=None, planta=False)


def retorno(M):
    # os tetos (salas fechadas) e o portal JOGAR contra a parede sul
    bloco(M, "sz_concreto_claro", 4.0, 70.0, 30.0, 96.0, 5.0, 5.3, planta=False)
    bloco(M, "sz_concreto_claro", 4.0, 38.0, 20.0, 62.0, 5.0, 5.3, planta=False)         # o teto da Sala de Ativação
    for x in (5.0, 13.6):
        bloco(M, "sz_metal_pintado_escuro", x, 94.8, x + 0.4, 95.6, 0.0, 3.6)
    bloco(M, "sz_metal_pintado_escuro", 5.0, 94.8, 14.0, 95.6, 3.6, 4.4)
    bloco(M, "sz_led_verde", 5.4, 95.5, 13.6, 95.6, 0.0, 3.6, colide=None, planta=False)


# ------------------------------------------------------------------ portas, zonas, marcas e bonecos

PORTAS = [   # id, centro (x, y), comprimento, ao longo de x?
    ("oeste", (63.2, 50.0), 6.0, False),      # Núcleo ← corredor (fim do bloco 1)
    ("norte", (75.0, 38.2), 6.0, True),       # Núcleo → Linha de Fogo (bloco 2)
    ("leste", (86.8, 50.0), 6.0, False),      # Núcleo → salão → Câmara (bloco 3)
    ("sul", (75.0, 61.8), 6.0, True),         # Núcleo → Arena (treino livre)
    ("camara_sul", (112.0, 63.8), 6.0, True),     # Câmara → Arena (bloco 3)
    ("camara_norte", (112.0, 36.2), 6.0, True),   # Câmara → Rua Ignição (bloco 4)
    ("arena_oeste", (39.8, 82.5), 5.0, False),    # Arena → Ponto de Retorno (bloco 5 / treino livre)
]

# id, (x, y), altura do chão, yaw do jogador (0 olha para −Z, −90 para +X — como RegressionBench.Place)
MARCAS = [
    ("nascer", (12.0, 50.0), 0.0, -90), ("marca_1", (25.5, 50.0), 0.0, -90), ("antes_vao", (30.0, 50.0), 0.0, -90),
    ("depois_vao", (35.8, 50.0), 0.0, -90), ("antes_viga", (40.0, 50.0), 0.0, -90), ("depois_viga", (45.5, 50.0), 0.0, -90),
    ("silencio", (49.5, 50.0), 0.0, -90), ("fim_corredor", (61.5, 50.0), 0.0, -90), ("nucleo", (75.0, 54.0), N_NUCLEO, 0),
    ("linha_de_tiro", (75.0, 21.9), N_FOGO, 0), ("pistola", (51.0, 21.0), N_FOGO, 0), ("corpo_a_corpo", (50.0, 24.0), N_FOGO, 180),
    ("granada", (99.0, 13.5), N_FOGO, 0), ("mirante", (75.0, -1.6), N_FOGO, 0), ("camara", (112.0, 50.0), N_CAMARA, 180),
    ("arena", (112.0, 67.0), 0.0, 180), ("galeria", (75.0, 65.3), N_GALERIA, 180),
    ("reta", (88.0, 70.5), 0.0, 90), ("salto", (55.2, 86.0), 0.0, 90), ("salto_alvo", (44.0, 86.0), 0.0, 90),
    ("plataforma_baixa", (64.0, 85.0), PLATAFORMA_BAIXA, 0), ("plataforma_alta", (75.0, 85.0), PLATAFORMA_ALTA, 0),
    ("parede", (114.0, 96.6), 0.0, 90), ("uivo", (106.0, 78.0), 0.0, 180), ("especial", (90.0, 76.5), 0.0, 180),
    ("rua", (112.0, 33.0), 0.0, 0), ("rua_centro", (126.0, 27.0), 0.0, 0), ("terraco", (118.0, 14.0), N_TERRACO, 180),
    ("retorno", (17.0, 88.0), 0.0, 0), ("portal", (9.5, 93.0), 0.0, 180),
]

# id, centro (x, y), base, (largura x, altura, fundura y), o que faz (None; o nome da marca para onde devolve; "mata")
ZONAS = [
    ("sala", (12.0, 50.0), 0.0, (15.0, 5.0, 23.0), None),
    ("marca_1", (25.5, 50.0), 0.0, (5.4, 3.0, 6.0), None),
    ("vao", (VAO_X0 + VAO_HUMANO / 2, 50.0), -VAO_FUNDO, (VAO_HUMANO, 1.0, 6.0), "antes_vao"),
    ("pouso_vao", (VAO_X0 + VAO_HUMANO + 1.6, 50.0), 0.0, (3.2, 3.0, 6.0), None),
    ("sob_viga", (43.0, 50.0), 0.0, (3.0, VIGA_LIVRE, 6.0), None),
    ("depois_viga", (45.5, 50.0), 0.0, (2.0, 3.0, 6.0), None),
    ("trecho_silencioso", (55.0, 50.0), 0.0, (12.0, 3.0, 6.0), None),
    ("fim_corredor", (62.2, 50.0), 0.0, (2.0, 3.0, 6.0), None),
    ("nucleo", (75.0, 50.0), N_NUCLEO, (20.0, 6.5, 20.0), None),
    ("linha_de_fogo", (75.0, 15.5), N_FOGO, (60.0, 6.0, 37.0), None),
    ("linha_de_tiro", (75.0, 23.0), N_FOGO, (60.0, 3.0, 4.0), None),
    ("pistola", (51.0, 21.0), N_FOGO, (10.0, 3.0, 4.0), None),
    ("corpo_a_corpo", (50.0, 25.7), N_FOGO, (8.0, 3.0, 4.5), None),
    ("granada", (99.0, 12.9), N_FOGO, (10.0, 3.0, 5.0), None),
    ("mirante", (75.0, -1.0), N_FOGO, (60.0, 3.0, 3.4), None),
    ("camara", (112.0, 50.0), N_CAMARA, (16.0, 6.0, 16.0), None),
    ("arena", (80.0, 81.0), 0.0, (80.0, ARENA_ALTURA, 34.0), None),
    ("vao_arena", (50.0, 86.0), -VAO_FUNDO, (8.0, 1.0, 12.0), "salto"),
    ("pouso_salto", (43.0, 86.0), 0.0, (6.0, 3.0, 12.0), None),
    ("reta", (65.0, 70.5), 0.0, (42.0, 3.0, 6.6), None),
    ("galeria", (74.0, 65.3), N_GALERIA, (60.0, 3.0, 2.6), None),
    ("parede", (100.0, 97.0), 0.0, (32.0, ARENA_ALTURA, 2.0), None),
    ("circulo_uivo", (106.0, 78.0), 0.0, (6.0, 3.0, 6.0), None),
    ("rua", (126.0, 20.0), 0.0, (40.0, 12.0, 32.0), None),
    ("terraco", (126.0, 14.0), N_TERRACO, (28.0, 3.0, 8.0), None),
    ("retorno", (17.0, 83.0), 0.0, (26.0, 5.0, 26.0), None),
    ("portal", (9.5, 93.0), 0.0, (9.0, 3.0, 4.0), None),
    ("abismo", (75.0, 50.0), ABISMO - 13.0, (420.0, 13.0, 420.0), "mata"),
]

# Os robôs de treino (`Dummy` com o desenho de `robo_treino.glb`): id, (x, y), nível do chão, papel, movimento.
# O papel (`metadata/estado`) é o que o tutorial vai fazer com cada um: `parado` (alvo fixo), `movel` (anda sempre),
# `onda` (sai do portão quando a etapa pede) e `ataca` (o que derruba o jogador no bloco 5). O movimento vale já,
# sem tutorial: None (parado), ("linha", (dx, dy), m/s) — vai e volta até (x + dx, y + dy) — ou ("volta", raio, m/s).
# Os robôs de treino: (id, lugar, nível, papel, percurso, velocidade em m/s). **Todo robô segue um percurso** (decisão
# do Jhon, 26/11: "os robôs são inimigos, não atiram, mas seguem sempre um percurso"): ("vai", [(dx, dy), …]) vai
# ponto a ponto até o fim, espera e volta pelo mesmo caminho; ("ronda", [...]) fecha a volta e roda sempre no mesmo
# sentido. Os pontos são contados de onde o robô nasce (o lugar dele é o começo do percurso — é de lá que as linhas de
# visada do roteiro são conferidas). O teste `SetorZeroRobos` anda cada percurso inteiro: nenhum atravessa parede,
# cobertura ou outro robô parado, e todos pisam no chão do começo ao fim.
ALVOS = [
    # ---- Linha de Fogo: os de perto e os do meio andam de lado, cada um na sua faixa; o de longe varre a beira
    ("proximo_1", (60.0, 15.0), N_FOGO, "parado", ("vai", [(4.0, 0.0)]), 1.1),
    ("proximo_2", (75.0, 15.0), N_FOGO, "parado", ("vai", [(-4.0, 0.0)]), 1.3),
    ("proximo_3", (90.0, 15.0), N_FOGO, "parado", ("vai", [(-4.0, 0.0)]), 1.0),
    ("medio_1", (58.0, 7.0), N_FOGO, "parado", ("vai", [(5.0, 0.0)]), 1.4),
    ("medio_2", (70.0, 7.0), N_FOGO, "parado", ("ronda", [(6.0, 0.0), (6.0, -3.0), (0.0, -3.0)]), 1.5),
    ("medio_3", (92.0, 7.0), N_FOGO, "parado", ("vai", [(-5.0, 0.0)]), 1.2),
    ("distante", (80.0, 0.4), N_FOGO, "parado", ("vai", [(-12.0, 0.0)]), 1.2),
    # a estação da pistola (afastam-se e voltam), o corpo a corpo (passeiam diante de quem bate) e o da granada (atrás do muro)
    ("pistola_1", (49.0, 18.5), N_FOGO, "parado", ("vai", [(0.0, -3.0)]), 0.9),
    ("pistola_2", (53.0, 18.5), N_FOGO, "parado", ("vai", [(0.0, -3.5)]), 1.2),
    ("corpo_1", (48.0, 26.2), N_FOGO, "parado", ("vai", [(0.0, 2.4)]), 0.8),
    ("corpo_2", (52.0, 26.2), N_FOGO, "parado", ("vai", [(3.0, 0.0)]), 0.8),
    ("granada", (99.0, 4.0), N_FOGO, "parado", ("vai", [(-3.5, 0.0)]), 0.9),
    # o do trilho (vai e volta ao longo dele) e dois que andam de lado atrás das coberturas baixas
    ("movel", (80.0, 19.2), N_FOGO, "movel", ("vai", [(18.5, 0.0)]), 2.4),
    ("lateral_1", (64.0, 11.6), N_FOGO, "movel", ("vai", [(6.0, 0.0)]), 1.4),
    ("lateral_2", (84.0, 11.6), N_FOGO, "movel", ("vai", [(-6.0, 0.0)]), 1.4),
    # ---- Arena da Matilha: os do bote (ao pé das plataformas), o do salto, o grupo do especial (andam juntos, em
    # formação), o que corre a reta (para perseguir) e a ronda do lado leste
    ("bote_1", (64.0, 78.8), 0.0, "parado", ("vai", [(4.0, 0.0)]), 1.0),
    ("bote_2", (75.0, 78.8), 0.0, "parado", ("vai", [(-4.0, 0.0)]), 1.0),
    ("salto", (43.2, 86.0), 0.0, "parado", ("vai", [(0.0, 3.5)]), 0.8),
    ("especial_1", (90.0, 80.0), 0.0, "parado", ("vai", [(7.0, 0.0)]), 1.0),
    ("especial_2", (92.2, 81.6), 0.0, "parado", ("vai", [(7.0, 0.0)]), 1.0),
    ("especial_3", (87.8, 81.6), 0.0, "parado", ("vai", [(7.0, 0.0)]), 1.0),
    ("especial_4", (90.0, 83.4), 0.0, "parado", ("vai", [(7.0, 0.0)]), 1.0),
    ("corredor", (48.0, 70.5), 0.0, "movel", ("vai", [(34.0, 0.0)]), 4.2),
    ("ronda_arena", (109.0, 76.5), 0.0, "movel", ("ronda", [(4.2, 0.0), (4.2, 7.0), (0.0, 7.0)]), 1.8),
    # ---- Rua Ignição: os três saem do portão de onda de cada um e voltam, o que "ataca" cruza a rua, uma patrulha
    # na rua e outra no terraço
    ("onda_a", (111.0, 21.5), 0.0, "onda", ("vai", [(0.0, 5.5)]), 1.4),
    ("onda_b", (126.0, 21.5), 0.0, "onda", ("vai", [(0.0, 4.0)]), 1.4),
    ("onda_c", (141.0, 21.5), 0.0, "onda", ("vai", [(0.0, 6.0)]), 1.4),
    ("ataca", (131.0, 24.0), 0.0, "ataca", ("vai", [(-3.4, 0.0)]), 1.6),
    ("patrulha_rua", (116.0, 30.0), 0.0, "movel", ("vai", [(20.0, 0.0)]), 2.0),
    ("patrulha_terraco", (114.4, 14.0), N_TERRACO, "movel", ("vai", [(7.6, 0.0)]), 1.5),
]


def tutorial(m):
    """O nó `Tutorial` da cena: as marcas, as zonas, as portas e os bonecos, para o roteiro achar pelo nome."""
    m.cru_ext += [("Script", "res://src/scripts/tutorial/TutorialZone.cs", "t_zona"),
                  ("PackedScene", "res://src/scenes/dummy.tscn", "t_boneco"),
                  ("PackedScene", "res://src/assets/mapas/pecas/robo_treino.glb", "t_robo")]
    # a porta de energia: um campo âmbar translúcido que brilha (vê-se o que há do outro lado)
    m.cru_recursos += ['[sub_resource type="StandardMaterial3D" id="PortaMat"]', "transparency = 1", "cull_mode = 2", "shading_mode = 0",
                       "albedo_color = Color(1, 0.72, 0.12, 0.26)", "emission_enabled = true", "emission = Color(1, 0.66, 0.08, 1)",
                       "emission_energy_multiplier = 1.4", "disable_receive_shadows = true", ""]
    n = ['[node name="Tutorial" type="Node3D" parent="."]', '',
         '[node name="Marcas" type="Node3D" parent="Tutorial"]', '']
    for nome, (x, y), h, yaw in MARCAS:
        X, Z = P(x, y)
        n += [f'[node name="{nome}" type="Marker3D" parent="Tutorial/Marcas"]', f"transform = {xform((X, h + 0.9, Z), giro_y(yaw))}", ""]

    n += ['[node name="Zonas" type="Node3D" parent="Tutorial"]', '']
    for nome, (x, y), base, (w, h, d), faz in ZONAS:
        X, Z = P(x, y)
        n += [f'[node name="{nome}" type="Node3D" parent="Tutorial/Zonas"]', f"transform = {xform((X, base + h / 2, Z))}",
              'script = ExtResource("t_zona")', f'Id = "{nome}"', f"Size = {vec((w, h, d))}"]
        if faz == "mata":
            n += ["Deadly = true"]
        elif faz:
            n += ["Reset = true", f'Back = NodePath("../../Marcas/{faz}")']
        n.append("")

    n += ['[node name="Portas" type="Node3D" parent="Tutorial"]', '']
    formas = {}
    for nome, (x, y), comp, ao_x in PORTAS:
        tam = (comp, 4.0, 0.2) if ao_x else (0.2, 4.0, comp)
        if tam not in formas:
            k = len(formas) + 1
            formas[tam] = k
            m.cru_recursos += [f'[sub_resource type="BoxShape3D" id="PortaForma{k}"]', f"size = {vec(tam)}", "",
                               f'[sub_resource type="BoxMesh" id="PortaMalha{k}"]', f"size = {vec(tam)}", ""]
        k = formas[tam]
        X, Z = P(x, y)
        caminho = f"Tutorial/Portas/{nome}"
        # Aberta por padrão (Jhon, 26/11: "o mapa deve estar todo aberto para andar, sem portas ou barreiras para
        # acessar os locais do treinamento"): sem tutorial para abrir uma por vez, a porta nasce sem colisão e
        # invisível. O nó continua aqui para o dia em que o tutorial existir e precisar fechar uma de cada vez.
        n += [f'[node name="{nome}" type="StaticBody3D" parent="Tutorial/Portas" groups=["porta_tutorial"]]',
              f"transform = {xform((X, 2.0, Z))}", f'metadata/porta = "{nome}"', "",
              f'[node name="Colisao" type="CollisionShape3D" parent="{caminho}"]', f'shape = SubResource("PortaForma{k}")', "disabled = true", "",
              f'[node name="Malha" type="MeshInstance3D" parent="{caminho}"]', f'mesh = SubResource("PortaMalha{k}")',
              'material_override = SubResource("PortaMat")', "cast_shadow = 0", "visible = false", ""]

    n += ['[node name="Alvos" type="Node3D" parent="Tutorial"]', '']
    for nome, (x, y), nivel, estado, (tipo, pontos), vel in ALVOS:
        X, Z = P(x, y)
        rota = ", ".join(f"{dx:g}, 0, {dy:g}" for dx, dy in pontos)
        n += [f'[node name="{nome}" parent="Tutorial/Alvos" instance=ExtResource("t_boneco")]', f"transform = {xform((X, nivel + 1.0, Z))}",
              'Model = ExtResource("t_robo")', "Move = 3", f"Route = PackedVector3Array({rota})", f"RouteLoop = {'true' if tipo == 'ronda' else 'false'}",
              f"MoveSpeed = {vel}", f'metadata/alvo = "{nome}"', f'metadata/estado = "{estado}"', ""]
    # o trilho do alvo móvel: de onde a onde ele vai
    for nome, x in (("trilho_de", 79.5), ("trilho_ate", 98.5)):
        X, Z = P(x, 19.2)
        n += [f'[node name="{nome}" type="Marker3D" parent="Tutorial/Alvos"]', f"transform = {xform((X, N_FOGO + 1.0, Z))}", ""]
    m.cru_nos += n


# ------------------------------------------------------------------ montar

def montar(com_acabamento=True):
    m = Mapa(ID, NOME, 160, 110)
    m.com_bots = False
    _colisao(m)

    M = Montagem("scifi", lambda nome: "caixa")
    cores(M.kit)
    F = Formas(M, "sz")
    chao(M, m)
    limites(M, m)
    paredes(M, m)
    nucleo(M, m, F)
    camara(M, m, F)
    corredor(M, m, F)
    linha_de_fogo(M, m, F)
    arena(M, m, F)
    rua(M, m, F)
    retorno(M)
    tutorial(m)

    # o único lugar de nascer: a Sala de Ativação, olhando para o corredor (o treino tem um jogador só)
    m.cru_ext.append(("Script", "res://src/scripts/core/SpawnPoints.cs", "t_nascer"))
    X, Z = P(12.0, 50.0)
    m.cru_nos += ['[node name="Nascer" type="Node3D" parent="."]', 'script = ExtResource("t_nascer")', "Debug = false", "Jitter = 0.0", "",
                  '[node name="p1" type="Marker3D" parent="Nascer"]', f"transform = {xform((X, 1.2, Z), giro_y(-90))}", ""]

    V = None
    if com_acabamento:
        from . import setor_zero_acabamento as A
        V = A.acabamento(m, M, F)

    m._triangulos = F.fechar()
    M.aplicar(m, "Setor", ID)
    if V is not None:
        V.aplicar(m, "Mato", ID + "_mato", no_planta="MatoPlanta", modelo="Mato")
        # uma lâmpada quente, sem sombra, nos postes mais espalhados (de dia quase não se nota ao sol; na sombra, sim)
        lampadas(m, [M], padrao=r"^SM_Floor_Light", maximo=24)

    # a luz: dia de montanha, sol alto e ar frio (o céu de foto gira com o sol: giro = yaw + 36, docs/MEDIDAS.md)
    sol = 140.0
    luz_de_jogo(m, "dia", sol, 52.0, giro_ceu=sol + 36.0, alcance=200.0,
                neblina=dict(cor=(0.86, 0.91, 1.0), densidade=0.00045, ceu=0.08, aerea=0.45, sol=0.2, altura=-70.0, altura_densidade=0.012), exposicao=0.86)
    # as salas fechadas: luminária de teto com sombra
    for x, y in ((9.0, 44.0), (15.0, 44.0), (9.0, 56.0), (15.0, 56.0), (10.0, 76.0), (23.0, 76.0), (10.0, 89.0), (23.0, 89.0)):
        X, Z = P(x, y)
        m.foco("Luzes", f"Teto{len(m.focos) + 1}", (X, 4.85, Z), (1.0, 0.94, 0.86), 2.4, 7.5, 70.0)
    m._montagem, m._mato, m._formas = M, V, F
    return m


# ------------------------------------------------------------------ conferência: nenhum quadrado vazio

def vazios(m, lado=5.0):
    """Os quadrados de `lado` m do campo onde não há nada além do pavimento de base: o Jhon odeia área vazia
    (docs/MAPAS.md, Setor Zero). Conta o piso de cada lugar, a construção, o prop, a vegetação, a pista, o letreiro e
    as formas próprias (menos as que cobrem tudo: a rocha, as nuvens, os picos)."""
    caixas = []
    M, V, F = m._montagem, m._mato, m._formas
    for Mn in (M, V):
        if Mn is None: continue
        for it in Mn.itens:
            if "_f_" in it.peca.nome: continue                               # as formas entram pela pegada de cada uma
            c = it.cantos()
            if it.peca.nome == "sz_piso_base" and c[:, 1].max() <= 0.01: continue
            if c[:, 0].max() - c[:, 0].min() > 150 or c[:, 2].max() - c[:, 2].min() > 100: continue
            caixas.append((c[:, 0].min() + CX, c[:, 2].min() + CY, c[:, 0].max() + CX, c[:, 2].max() + CY))
    caixas += [p for p in F.pegadas if p[2] - p[0] < 40 and p[3] - p[1] < 40]
    for (g, nome, c, tam, mat, rot, colide, forma) in m.pecas:
        if g == "Colisao": continue
        r = max(tam[0], tam[2]) / 2 if forma.startswith("cilindro") else None
        hx, hz = (r, r) if r else (tam[0] / 2, tam[2] / 2)
        caixas.append((c[0] - hx + CX, c[2] - hz + CY, c[0] + hx + CX, c[2] + hz + CY))
    for (g, nome, imagem, pos, largura, base, aceso) in m.letreiros:
        caixas.append((pos[0] - largura / 2 + CX, pos[2] - largura / 2 + CY, pos[0] + largura / 2 + CX, pos[2] + largura / 2 + CY))
    C = np.array(caixas)
    faltam = []
    for i in range(int(150 / lado)):
        for j in range(int(100 / lado)):
            x0, y0 = i * lado, j * lado
            dentro = (C[:, 0] < x0 + lado) & (C[:, 2] > x0) & (C[:, 1] < y0 + lado) & (C[:, 3] > y0)
            if not dentro.any():
                faltam.append((x0, y0))
    return faltam


def principal():
    m = montar(com_acabamento="--sem-acabamento" not in sys.argv)
    if "--vazios" not in sys.argv:
        m.salvar()
    falta = vazios(m)
    print(f"  {m._triangulos} triângulos de formas próprias; {len(falta)} quadrados de 5 m vazios"
          + (": " + ", ".join(f"({x:.0f},{y:.0f})" for x, y in falta[:30]) if falta else ""))
    sys.exit(1 if falta else 0)


if __name__ == "__main__":
    principal()
