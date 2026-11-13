"""
Metrô — mapa pequeno (48 × 30 m), fechado.

Uma estação de metrô com um trem parado no meio. Duas plataformas, uma
de cada lado da via; as bases são os saguões das pontas.

As quatro perguntas do teste de mapa:
  1. Caminho que só o lobo faz: o teto do vagão (2,2 m, do caixote) e os
     corredores de serviço de 3 m atrás das plataformas — duas paredes de
     5 m para subir ricocheteando até a galeria.
  2. Onde o humano vê o lobo chegando: a galeria, 5 m acima da
     plataforma, de frente para a via. Sobe-se pela escada do saguão.
  3. Corredor que o humano deve evitar: os de serviço e o vagão por
     dentro — distância de garra.
  4. Cair em cima de quem não olha: da galeria (o parapeito tem vãos) e
     do teto do vagão.

A vala da via, nas pontas do trem, tem 1,2 m: dá cobertura agachado, sai
pela rampa do saguão ou pelo degrau de 0,6 m.
"""
import math

from . import formas as F
from . import pecas as P
from .kit import Mapa, AZUL, LARANJA


def montar():
    m = Mapa("metro", "Metro", 48, 30)

    m.material("azulejo", "azulejo", (0.92, 0.94, 0.93), 1.6, relevo=0.8)
    m.material("azulejo_azul", "azulejo", (0.42, 0.60, 0.95), 1.6, relevo=0.8)
    m.material("azulejo_laranja", "azulejo", (1.0, 0.62, 0.34), 1.6, relevo=0.8)
    m.material("piso", "piso", (0.80, 0.80, 0.82), 3.0)
    m.material("concreto", "concreto", (0.72, 0.72, 0.74), 4.0)
    m.material("teto", "concreto", (0.36, 0.36, 0.38), 4.0)
    m.material("lastro", "asfalto", (1.0, 0.95, 0.9), 2.0)
    m.material("trilho", None, (0.32, 0.30, 0.29), aspereza=0.35, metal=0.9)
    m.material("faixa", None, (0.98, 0.80, 0.10), aspereza=0.6, brilho=(0.98, 0.80, 0.10), energia=0.35)
    m.material("vagao", "metal_pintado", (0.78, 0.81, 0.84), 2.0, aspereza=0.8, metal=0.3)
    m.material("vagao_faixa", "metal_pintado", (0.80, 0.14, 0.12), 2.0, aspereza=0.8, metal=0.4)
    m.material("chapa", "chapa_xadrez", (0.62, 0.64, 0.68), 1.0, metal=0.3)
    m.material("aco", "metal_pintado", (0.30, 0.33, 0.37), 2.0, metal=0.3)
    m.material("grade", None, (0.95, 0.74, 0.16), aspereza=0.5, metal=0.3)
    m.material("madeira", "madeira", (1, 1, 1), 1.6)
    m.material("banco", "madeira", (0.75, 0.6, 0.5), 1.2)
    m.material("catraca", "metal_pintado", (0.70, 0.72, 0.74), 1.0, metal=0.3, aspereza=0.6)
    m.material("lampada", None, (1, 1, 1), brilho=(1.0, 0.96, 0.86), energia=3.0)
    m.material("placa", None, (0.05, 0.25, 0.12), brilho=(0.2, 1.0, 0.45), energia=1.6)
    m.material("maquina", "metal_pintado", (0.16, 0.18, 0.22), 1.0, metal=0.2)
    m.material("vitrine", None, (0.1, 0.1, 0.12), brilho=(0.25, 0.75, 1.0), energia=1.4)

    # --- Casca: plataformas (2 m de espessura, a via fica 1,2 m abaixo),
    #     saguões nas pontas, paredes e teto a 9 m.
    for sz, t in ((1, "Norte"), (-1, "Sul")):
        m.caixa("Casca", f"Plataforma{t}", (0, -1, sz * 9), (48, 2, 12), "piso")
        m.caixa("Casca", f"Parede{t}", (0, 4.5, sz * 15.5), (50, 9, 1), "azulejo")
        m.caixa("Casca", f"Faixa{t}", (0, 0.011, sz * 3.4), (34, 0.02, 0.4), "faixa", colide=False)
    for s, t in ((1, "Leste"), (-1, "Oeste")):
        m.caixa("Casca", f"Saguao{t}", (s * 20.5, -1, 0), (7, 2, 6), "piso")
        m.caixa("Casca", f"Parede{t}", (s * 24.5, 4.5, 0), (1, 9, 30), "azulejo_laranja" if s > 0 else "azulejo_azul")
        m.rampa("Casca", f"RampaVala{t}", s * 12, s * 17, 1.2, 0, 6, "concreto", base=-1.2)
        m.caixa("Casca", f"DegrauVala{t}", (s * 10, -0.9, 2.3), (1.4, 0.6, 1.4), "concreto")
    m.caixa("Casca", "Via", (0, -1.7, 0), (34, 1, 6), "lastro")
    m.caixa("Casca", "Teto", (0, 9.25, 0), (50, 0.5, 32), "teto")
    for sz, t in ((1, "N"), (-1, "S")):
        m.caixa("Casca", f"Trilho{t}", (0, -1.12, sz * 0.75), (34, 0.16, 0.12), "trilho", colide=False)

    # --- O trem: piso na altura da plataforma, duas portas de cada lado,
    #     portas nas pontas (que dão na vala), teto a 2,2 m.
    m.caixa("Trem", "Piso", (0, -0.6, 0), (18, 1.2, 5.6), "vagao")
    m.caixa("Trem", "Teto", (0, 2.1, 0), (18, 0.2, 5.6), "vagao", oculta=True)
    for sz, t in ((1, "Norte"), (-1, "Sul")):
        for cx, comp, nome in ((-7.25, 3.5, "Oeste"), (0, 7, "Meio"), (7.25, 3.5, "Leste")):
            m.caixa("Trem", f"Lado{t}{nome}", (cx, 1.0, sz * 2.7), (comp, 2.0, 0.2), "vagao")
            m.caixa("Trem", f"Listra{t}{nome}", (cx, 0.9, sz * 2.81), (comp, 0.35, 0.02), "vagao_faixa", colide=False)
        m.caixa("Trem", f"Banco{t}", (0, 0.25, sz * 2.25), (5, 0.5, 0.6), "banco")
    for s, t in ((1, "Leste"), (-1, "Oeste")):
        for sz, tz in ((1, "Norte"), (-1, "Sul")):
            m.caixa("Trem", f"Ponta{t}{tz}", (s * 8.9, 1.0, sz * 1.8), (0.2, 2.0, 2.0), "vagao")
        m.luz("Trem", f"Luz{t}", (s * 4.5, 1.7, 0), (1.0, 0.95, 0.85), 1.0, 6)

    # --- Plataformas: pilares, galeria a 5 m, corredor de serviço atrás.
    for sz, t in ((1, "Norte"), (-1, "Sul")):
        g = f"Plataforma{t}"
        for i, x in enumerate((-12, -6, 0, 6, 12), 1):
            m.caixa(g, f"Pilar{i}", (x, 4.5, sz * 7.6), (0.8, 9, 0.8), "concreto", oculta=True)
        m.caixa(g, "Galeria", (0, 4.8, sz * 9.225), (28, 0.4, 4.05), "chapa")
        for cx, comp, nome in ((-11, 6, "Oeste"), (0, 6, "Meio"), (11, 6, "Leste")):
            m.parapeito(g, f"Parapeito{nome}", (cx, 5.5, sz * 7.35), comp)
        for cx, nome in ((-7.75, "Oeste"), (7.75, "Leste")):
            m.caixa(g, f"ParedeServico{nome}", (cx, 2.5, sz * 11.5), (12.5, 5, 0.5), "azulejo")
        # Caixotes: o do meio leva o lobo ao teto do vagão.
        m.caixa(g, "CaixoteTrem", (7.5 * sz, 0.4, sz * 3.9), (1.2, 0.8, 1.2), "madeira")
        m.caixa(g, "CaixotePilar", (-3 * sz, 0.4, sz * 6.4), (1.4, 0.8, 1.4), "madeira")
        m.caixa(g, "Banco", (3 * sz, 0.25, sz * 10.6), (2.6, 0.5, 0.7), "banco")
        m.caixa(g, "Maquina", (-9 * sz, 1.0, sz * 10.8), (1.1, 2.0, 0.8), "maquina")
        m.caixa(g, "Vitrine", (-9 * sz, 1.15, sz * 10.38), (0.8, 1.2, 0.04), "vitrine", colide=False)
        m.placa(g, "Nome", (0, 3.4, sz * 11.2), "LINHA 7  ·  ESTAÇÃO ALCATEIA", 90, (0.9, 0.95, 1.0), 180 if sz > 0 else 0)
        for i, x in enumerate((-12, -4, 4, 12), 1):
            m.caixa(g, f"Lampada{i}", (x, 7.2, sz * 5.2), (2.4, 0.07, 0.22), "lampada", colide=False)
            m.caixa(g, f"Calha{i}", (x, 7.27, sz * 5.2), (2.6, 0.08, 0.34), "aco", colide=False)
            for k, dx in enumerate((-1.0, 1.0)):
                m.caixa(g, f"Haste{i}{k}", (x + dx, 7.95, sz * 5.2), (0.03, 1.3, 0.03), "aco", colide=False)
            m.luz(g, f"Luz{i}", (x, 6.7, sz * 5.2), (1.0, 0.95, 0.84), 2.2, 13)
        for i, x in enumerate((-9, 0, 9), 1):
            m.luz(g, f"LuzGaleria{i}", (x, 4.1, sz * 9.4), (1.0, 0.9, 0.72), 1.0, 7)
            m.luz(g, f"LuzServico{i}", (x, 3.6, sz * 13.4), (0.55, 1.0, 0.7), 0.9, 8)

    # --- Saguões (as bases): escada para a galeria, catracas, bilheteria.
    for s, lado, cor in ((-1, "A", (0.45, 0.65, 1.0)), (1, "B", (1.0, 0.58, 0.28))):
        g = f"Base{lado}"
        for sz, t in ((1, "Norte"), (-1, "Sul")):
            m.rampa(g, f"Escada{t}", s * 23.4, s * 14, 5, sz * 9.5, 2.6, "chapa", degraus=28)
            m.caixa(g, f"Catraca{t}1", (s * 17.6, 0.5, sz * 5), (0.4, 1.0, 1.6), "catraca")
            m.caixa(g, f"Catraca{t}2", (s * 17.6, 0.5, sz * 7.2), (0.4, 1.0, 1.2), "catraca")
            m.caixa(g, f"Bilheteria{t}", (s * 21.5, 1.3, sz * 13.2), (3.0, 2.6, 2.6), "azulejo_azul" if s < 0 else "azulejo_laranja")
            m.caixa(g, f"Saida{t}", (s * 23.9, 3.0, sz * 4), (0.1, 0.4, 1.4), "placa", colide=False)
        m.placa(g, "Letra", (s * 23.9, 5.6, 0), lado, 420, cor, 90 if s < 0 else -90)
        m.luz(g, "Luz", (s * 20.5, 5.5, 0), cor, 2.4, 14)
        m.luz(g, "LuzTeto", (s * 20.5, 8, 0), (1.0, 0.96, 0.86), 1.6, 14)
        time = AZUL if s < 0 else LARANJA
        for z in (-6, -2, 2, 6):
            m.spawn(time, s * 22.5, z)
        for z in (-5, -1.7, 1.7, 5):
            m.spawn(time, s * 20, z)

    m.rota("plataformaNorte", [(-20, 0.5, 6), (-10, 0.5, 5.5), (0, 0.5, 5.5), (10, 0.5, 5.5), (20, 0.5, 6)])
    m.rota("plataformaSul", [(-20, 0.5, -6), (-10, 0.5, -5.5), (0, 0.5, -5.5), (10, 0.5, -5.5), (20, 0.5, -6)])
    m.rota("peloTrem", [(-12, 0.5, 5), (-4.5, 0.5, 3.6), (-4.5, 0.5, 0), (4.5, 0.5, 0), (4.5, 0.5, -3.6), (12, 0.5, -5)])
    m.rota("peloTrem2", [(-12, 0.5, -5), (-4.5, 0.5, -3.6), (-4.5, 0.5, 0), (4.5, 0.5, 0), (4.5, 0.5, 3.6), (12, 0.5, 5)])
    m.rota("galeriaNorte", [(-22, 0.5, 9.5), (-12, 5.5, 9.5), (0, 5.5, 9.5), (12, 5.5, 9.5), (22, 0.5, 9.5)])
    m.rota("galeriaSul", [(-22, 0.5, -9.5), (-12, 5.5, -9.5), (0, 5.5, -9.5), (12, 5.5, -9.5), (22, 0.5, -9.5)])
    m.rota("servicoNorte", [(-18, 0.5, 13.4), (-8, 0.5, 13.4), (0, 0.5, 13.4), (8, 0.5, 13.4), (18, 0.5, 13.4)])
    m.rota("servicoSul", [(-18, 0.5, -13.4), (-8, 0.5, -13.4), (0, 0.5, -13.4), (8, 0.5, -13.4), (18, 0.5, -13.4)])

    m.ambiente = dict(fundo=(0.01, 0.012, 0.015), luz=(0.62, 0.70, 0.85), luz_energia=0.32, exposicao=1.1,
                      brilho=0.9, contraste=1.08, saturacao=1.05,
                      neblina=dict(cor=(0.30, 0.33, 0.36), densidade=0.006, ceu=0.0))
    acabamento(m)
    return m


def acabamento(m):
    """O que faz a casca virar estação: abóbada com nervuras, colunas
    redondas, o trem com teto curvo e janelas, escadas de degrau,
    corrimãos, dutos, anúncios, bilheteria, portões de enrolar. Quase tudo
    sem colisão — o desenho do jogo (o que cobre, o que se sobe) é o das
    peças de cima."""
    P.materiais(m)
    m.material("abobada", "azulejo", (0.80, 0.80, 0.77), 1.6, relevo=0.7)
    m.material("nervura", "concreto", (0.55, 0.55, 0.57), 3.0)
    m.material("azulejo_faixa", "azulejo", (0.16, 0.30, 0.52), 1.6, relevo=0.8)
    m.material("borda", "concreto", (0.86, 0.86, 0.84), 2.0)
    m.material("persiana", "metal_ondulado", (0.52, 0.55, 0.58), 1.2, metal=0.4, aspereza=0.6)
    m.material("duto", "metal_pintado", (0.62, 0.64, 0.66), 2.0, metal=0.5, aspereza=0.5)
    m.material("cano_vermelho", None, (0.62, 0.10, 0.08), aspereza=0.5, metal=0.3)
    m.material("escuro", None, (0.02, 0.02, 0.025), aspereza=1.0)
    m.material("luz_vagao", None, (1, 1, 1), brilho=(1.0, 0.96, 0.88), energia=1.2)
    m.material("farol", None, (1, 1, 1), brilho=(1.0, 0.97, 0.85), energia=4.0)
    for nome, cor in (("anuncio1", (1.0, 0.72, 0.30)), ("anuncio2", (0.30, 0.85, 1.0)), ("anuncio3", (0.95, 0.35, 0.55))):
        m.material(nome, None, tuple(c * 0.25 for c in cor), brilho=cor, energia=1.1)

    # --- Abóbada: nasce a 6,5 m nas paredes e fecha a 9 m no meio.
    raio, centro = 46.25, -37.25
    de = math.degrees(math.acos(15.0 / raio))
    m.forma("Casca", "Abobada", F.abobada(raio, 48, 24, de, 180 - de), (0, centro, 0), "abobada", yaw=90)
    for i, x in enumerate(range(-21, 22, 6)):
        m.forma("Casca", f"Nervura{i}", F.setor(raio - 0.02, raio - 0.4, de, 180 - de, 0.5, 24), (x, centro, 0), "nervura", yaw=90)
    # Duto de ventilação pelo cume, com as bocas.
    P.tubulacao(m, "Casca", "Duto", [(-23.5, 8.15, 0), (23.5, 8.15, 0)], 0.38, "duto", flange="aco", cada=2.9)
    for i, x in enumerate((-15, -9, 9, 15)):
        m.caixa("Casca", f"BocaDuto{i}", (x, 7.68, 0), (0.9, 0.2, 0.6), "aco", colide=False)

    # --- Via: dormentes onde a vala aparece e os para-choques do fim da linha.
    for s in (-1, 1):
        for i in range(5):
            m.caixa("Casca", f"Dormente{'L' if s > 0 else 'O'}{i}", (s * (9.4 + i * 0.7), -1.15, 0), (0.24, 0.1, 2.5), "madeira_escura", colide=False)

    # --- Trem: teto curvo, janelas, faróis, saia, portas marcadas, balaústres.
    arco = lambda cx, a0, a1: [(cx + 0.52 * math.cos(math.radians(a0 + (a1 - a0) * i / 6)), 1.7 + 0.52 * math.sin(math.radians(a0 + (a1 - a0) * i / 6))) for i in range(7)]
    teto = [(-2.82, 1.7)] + arco(-2.3, 180, 90) + arco(2.3, 90, 0) + [(2.82, 1.7), (2.62, 1.7), (2.62, 1.98), (-2.62, 1.98), (-2.62, 1.7)]
    m.forma("Trem", "TetoCurvo", F.extrusao(teto, 18.1, suave=True), (0, 0, 0), "vagao", yaw=90)
    for sz, t in ((1, "N"), (-1, "S")):
        for i, (cx, larg) in enumerate(((-7.25, 1.6), (-2.2, 1.7), (0, 1.7), (2.2, 1.7), (7.25, 1.6))):
            m.caixa("Trem", f"Janela{t}{i}", (cx, 1.3, sz * 2.7), (larg, 0.75, 0.24), "vidro", colide=False)
        m.caixa("Trem", f"Saia{t}", (0, -0.2, sz * 2.82), (18, 0.4, 0.04), "aco", colide=False)
        for i, cx in enumerate((-5.5, -3.5, 3.5, 5.5)):           # batentes das portas
            m.caixa("Trem", f"Batente{t}{i}", (cx, 1.0, sz * 2.7), (0.09, 2.0, 0.26), "grade", colide=False)
        for i, cx in enumerate((-4.5, 4.5)):                       # a soleira amarela e a luz da porta
            m.caixa("Trem", f"Soleira{t}{i}", (cx, 0.012, sz * 2.75), (2.0, 0.025, 0.3), "faixa", colide=False)
            m.caixa("Trem", f"LuzPorta{t}{i}", (cx, 1.96, sz * 2.72), (1.6, 0.05, 0.08), "lampada", colide=False)
        m.caixa("Trem", f"Encosto{t}", (0, 0.75, sz * 2.52), (5, 0.6, 0.08), "banco", colide=False)
    for i, x in enumerate((-6.6, -2.0, 2.0, 6.6)):
        m.forma("Trem", f"Balaustre{i}", F.tubo([(x, 0, 0), (x, 1.98, 0)], 0.025, 8), (0, 0, 0), "grade")
    for i, x in enumerate((-4.5, 0, 4.5)):
        m.caixa("Trem", f"LuzTeto{i}", (x, 1.96, 0), (2.6, 0.04, 0.25), "luz_vagao", colide=False)
    for s, t in ((1, "L"), (-1, "O")):
        for sz, tz in ((1, "N"), (-1, "S")):
            m.caixa("Trem", f"Parabrisa{t}{tz}", (s * 8.9, 1.35, sz * 1.8), (0.24, 0.7, 1.5), "vidro", colide=False)
            m.caixa("Trem", f"Farol{t}{tz}", (s * 9.01, 0.45, sz * 2.0), (0.03, 0.16, 0.34), "farol", colide=False)
        m.caixa("Trem", f"Letreiro{t}", (s * 9.01, 1.84, 0), (0.03, 0.22, 1.5), "placa", colide=False)
        m.caixa("Trem", f"Engate{t}", (s * 9.2, -0.5, 0), (0.5, 0.3, 0.5), "aco", colide=False)

    # --- Plataformas.
    for sz, t in ((1, "Norte"), (-1, "Sul")):
        g = f"Plataforma{t}"
        m.caixa(g, "Borda", (0, 0.006, sz * 3.12), (34, 0.012, 0.24), "borda", colide=False)
        for i, x in enumerate((-12, -6, 0, 6, 12), 1):
            P.coluna(m, g, f"Coluna{i}", (x, 0, sz * 7.6), 8.45, 0.4, "azulejo", colide=False)
            m.caixa(g, f"Anel{i}", (x, 1.25, sz * 7.6), (0.84, 0.25, 0.84), "azulejo_faixa", colide=False)
        # A galeria: viga de borda, mãos-francesas e o forro.
        m.caixa(g, "VigaGaleria", (0, 4.5, sz * 7.24), (28, 0.5, 0.16), "aco", colide=False)
        for i, x in enumerate((-13.5, -9, -3, 3, 9, 13.5)):
            m.caixa(g, f"VigaTransversal{i}", (x, 4.5, sz * 9.25), (0.16, 0.24, 4.0), "aco", colide=False)
        # Parede de serviço: faixa de azulejo, rodapé, anúncios acesos, placa da estação.
        for cx, nome in ((-7.75, "Oeste"), (7.75, "Leste")):
            m.caixa(g, f"Faixa{nome}", (cx, 1.25, sz * 11.24), (12.5, 0.25, 0.03), "azulejo_faixa", colide=False)
            m.caixa(g, f"Rodape{nome}", (cx, 0.09, sz * 11.23), (12.5, 0.18, 0.05), "nervura", colide=False)
            m.caixa(g, f"Topo{nome}", (cx, 5.04, sz * 11.5), (12.6, 0.08, 0.6), "borda", colide=False)
        for i, (x, mat) in enumerate(((-11, "anuncio1"), (-5, "anuncio2"), (5, "anuncio3"), (11, "anuncio1"))):
            m.caixa(g, f"Anuncio{i}", (x, 2.55, sz * 11.22), (2.2, 1.3, 0.05), mat, colide=False)
            m.caixa(g, f"AnuncioMoldura{i}", (x, 2.55, sz * 11.235), (2.36, 1.46, 0.04), "aco", colide=False)
        m.caixa(g, "PlacaEstacao", (0, 3.4, sz * 11.32), (9.6, 0.95, 0.12), "azulejo_faixa", colide=False)
        # Placas de saída penduradas da galeria.
        for i, x in enumerate((-13, 13)):
            m.caixa(g, f"PlacaSaida{i}", (x, 3.75, sz * 6.2), (1.8, 0.4, 0.08), "placa", colide=False)
            for k, dx in enumerate((-0.7, 0.7)):
                m.caixa(g, f"HastePlaca{i}{k}", (x + dx, 4.25, sz * 6.2), (0.025, 0.6, 0.025), "aco", colide=False)
        # Banco com encosto e pés; lixeiras junto às colunas.
        m.caixa(g, "BancoEncosto", (3 * sz, 0.72, sz * 10.9), (2.6, 0.5, 0.07), "banco", colide=False)
        for i, x in enumerate((-9, 9)):
            lixo = [(0, 0), (0.2, 0), (0.24, 0.72), (0.26, 0.74), (0.2, 0.78), (0, 0.78)]
            m.forma(g, f"Lixeira{i}", F.revolucao(lixo, 14, suave=False), (x, 0, sz * 6.75), "duto")
            m.cilindro(g, f"LixeiraColisao{i}", (x, 0.39, sz * 6.75), 0.25, 0.78, "duto", oculta=True)
        # Corredor de serviço: canos na parede de fora, quadros de força, luminárias com grade.
        P.tubulacao(m, g, "CanoIncendio", [(-23, 2.7, sz * 14.86), (23, 2.7, sz * 14.86)], 0.07, "cano_vermelho", flange="ferro", cada=4.0)
        P.tubulacao(m, g, "CanoAgua", [(-23, 0.35, sz * 14.84), (-2, 0.35, sz * 14.84), (-2, 3.3, sz * 14.84), (23, 3.3, sz * 14.84)], 0.09, "duto", flange="aco", cada=4.0)
        for i in range(3):
            P.tubulacao(m, g, f"Eletroduto{i}", [(-23.8, 5.9 + i * 0.14, sz * 14.9), (23.8, 5.9 + i * 0.14, sz * 14.9)], 0.035, "ferro")
        for i, x in enumerate((-10.5, 4.5, 12)):
            m.caixa(g, f"Quadro{i}", (x, 1.5, sz * 14.84), (0.8, 1.3, 0.3), "duto", colide=False)
            m.caixa(g, f"QuadroPorta{i}", (x, 1.5, sz * 14.68), (0.7, 1.2, 0.02), "aco", colide=False)
        for i, x in enumerate((-9, 0, 9)):
            P.arandela(m, g, f"Arandela{i}", (x, 3.9, sz * 15.0), "z-" if sz > 0 else "z+", cor=(0.55, 1.0, 0.7), energia=0.0, alcance=1.0)

    # --- Saguões.
    for s, lado in ((-1, "A"), (1, "B")):
        g = f"Base{lado}"
        frente = "x+" if s < 0 else "x-"
        for sz, t in ((1, "Norte"), (-1, "Sul")):
            for k, dz in enumerate((-1.24, 1.24)):
                P.corrimao(m, g, f"Corrimao{t}{k}", [(s * 23.3, 0.0, sz * 9.5 + dz), (s * 14.1, 5.0, sz * 9.5 + dz)], "grade", cada=1.55)
            # Catracas: tampo de aço, braços, luz de liberado.
            for k, (cz, comp) in enumerate(((5, 1.6), (7.2, 1.2))):
                m.caixa(g, f"CatracaTampo{t}{k}", (s * 17.6, 1.02, sz * cz), (0.46, 0.05, comp + 0.04), "duto", colide=False)
                m.caixa(g, f"CatracaLuz{t}{k}", (s * 17.6 - s * 0.24, 0.8, sz * cz), (0.02, 0.1, 0.3), "placa", colide=False)
            # Bilheteria: guichê aceso, balcão, toldo de chapa e a placa.
            bz = sz * (13.2 - 1.3)
            P.janela(m, g, f"Guiche{t}", (s * 21.5, 1.5, bz), "z-" if sz > 0 else "z+", 1.9, 0.9, "duto", "vidro_aceso", peitoril=False)
            m.caixa(g, f"Balcao{t}", (s * 21.5, 0.98, bz - sz * 0.14), (2.2, 0.06, 0.34), "duto", colide=False)
            m.caixa(g, f"TestaBilheteria{t}", (s * 21.5, 2.72, sz * 13.2), (3.2, 0.24, 2.8), "nervura", colide=False)
            m.placa(g, f"Bilhetes{t}", (s * 21.5, 2.32, bz - sz * 0.03), "BILHETES", 70, (1.0, 0.95, 0.8), 180 if sz > 0 else 0)
        # A saída para a rua: fechada com portão de enrolar, sob a letra.
        m.caixa(g, "PortaoFundo", (s * 23.97, 1.7, 0), (0.06, 3.4, 4.4), "escuro", colide=False)
        m.caixa(g, "Portao", (s * 23.93, 1.6, 0), (0.06, 3.2, 4.0), "persiana", colide=False)
        for k, dz in enumerate((-2.1, 2.1)):
            m.caixa(g, f"PortaoGuia{k}", (s * 23.9, 1.7, dz), (0.14, 3.4, 0.16), "aco", colide=False)
        m.caixa(g, "PortaoCaixa", (s * 23.86, 3.5, 0), (0.3, 0.36, 4.4), "aco", colide=False)
        # Mapa da linha, aceso, ao lado do portão; máquinas de bilhete.
        for k, dz in enumerate((-3.6, 3.6)):
            m.caixa(g, f"MapaLinha{k}", (s * 23.95, 1.7, dz), (0.05, 1.5, 0.9), "anuncio2", colide=False)
            m.caixa(g, f"MapaMoldura{k}", (s * 23.97, 1.7, dz), (0.04, 1.62, 1.02), "aco", colide=False)
        # Borda dos degraus do saguão para a vala e rodapés.
        m.caixa(g, "BordaSaguao", (s * 17.1, 0.006, 0), (0.2, 0.012, 6), "faixa", colide=False)
