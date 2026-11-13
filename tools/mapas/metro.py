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


# Onde a parede de fora de cada plataforma abre para a galeria de lojas.
PORTAS_GALERIA = (-15, 0, 15)


def galeria(m, sz, t):
    """
    A galeria de lojas atrás de cada plataforma: um salão de 44 × 8 m,
    com o pé-direito baixo (4,5 m), que se alcança pelo corredor de
    serviço por três portas. É o terceiro caminho de cada lado — e o
    lugar do tiroteio de perto.

    Do que é feito, e por quê:
    - três portas, como toda área que dá para defender: quem segura uma
      não segura as outras;
    - a sala de controle, no meio do fundo: mureta de 1 m na frente (atira-se
      por cima, como numa janela), uma porta de cada lado;
    - dois balcões (café a oeste, banca a leste) com um corredor atrás:
      cobertura de frente, flanco por trás;
    - colunas e bancos quebrando a linha comprida do salão.
    Igual para os dois times: o que há a oeste há a leste.
    """
    g = f"Galeria{t}"
    m.caixa(g, "Piso", (0, -0.5, sz * 19.75), (46, 1, 9.5), "piso_galeria")
    m.caixa(g, "Fundo", (0, 2.5, sz * 24.5), (46, 5, 1), "azulejo_galeria")
    m.caixa(g, "Teto", (0, 4.75, sz * 20), (46, 0.5, 9.5), "teto")
    for s, lado in ((-1, "Oeste"), (1, "Leste")):
        m.caixa(g, f"Lateral{lado}", (s * 22.5, 2.5, sz * 20), (1, 5, 8), "azulejo_galeria")
        # O balcão, com o corredor atrás, e a estante no fundo.
        m.caixa(g, f"Balcao{lado}", (s * 13.5, 0.55, sz * 21.4), (7, 1.1, 0.7), "balcao")
        m.caixa(g, f"Estante{lado}", (s * 13.5, 1.1, sz * 23.75), (6, 2.2, 0.5), "madeira")
        # Colunas e bancos pelo salão; a máquina encostada na parede da estação.
        for i, x in enumerate((9, 18)):
            m.caixa(g, f"Coluna{lado}{i}", (s * x, 2.25, sz * 18.2), (0.8, 4.5, 0.8), "concreto")
        m.caixa(g, f"Banco{lado}", (s * 13.5, 0.25, sz * 18.2), (2.6, 0.5, 0.7), "banco")
        m.caixa(g, f"Maquina{lado}", (s * 6.5, 1.0, sz * 16.45), (1.1, 2.0, 0.8), "maquina")
        m.caixa(g, f"Vitrine{lado}", (s * 6.5, 1.15, sz * 16.87), (0.8, 1.2, 0.04), "vitrine", colide=False)
        # A sala de controle: parede lateral com a porta junto à frente.
        m.caixa(g, f"Controle{lado}", (s * 4, 2.25, sz * 22.95), (0.3, 4.5, 2.1), "azulejo_galeria")
        m.caixa(g, f"ControleVerga{lado}", (s * 4, 3.55, sz * 21.2), (0.3, 1.9, 1.4), "azulejo_galeria")
        m.caixa(g, f"ControleQuina{lado}", (s * 4, 2.25, sz * 20.5), (0.4, 4.5, 0.4), "concreto")
    m.caixa(g, "ControleMureta", (0, 0.5, sz * 20.5), (7.6, 1.0, 0.3), "azulejo_galeria")
    m.caixa(g, "ControleTesta", (0, 3.55, sz * 20.5), (7.6, 1.9, 0.3), "azulejo_galeria")
    m.caixa(g, "ControleMesa", (0, 0.45, sz * 23.4), (5.0, 0.9, 0.9), "maquina")
    for i, x in enumerate((-16, -8, 8, 16)):
        m.luz(g, f"Luz{i}", (x, 3.8, sz * 19.2), (1.0, 0.93, 0.8), 1.6, 10)
    m.luz(g, "LuzControle", (0, 3.6, sz * 22.4), (0.55, 0.8, 1.0), 1.4, 7)


def montar():
    m = Mapa("metro", "Metro", 48, 50)

    m.material("azulejo", "azulejo", (0.92, 0.94, 0.93), 1.6, relevo=0.8)
    m.material("azulejo_azul", "azulejo", (0.42, 0.60, 0.95), 1.6, relevo=0.8)
    m.material("azulejo_laranja", "azulejo", (1.0, 0.62, 0.34), 1.6, relevo=0.8)
    m.material("piso", "granilite", (0.86, 0.86, 0.88), 2.4)
    m.material("concreto", "concreto", (0.72, 0.72, 0.74), 4.0)
    m.material("teto", "concreto_laje", (0.42, 0.42, 0.44), 4.0)
    m.material("lastro", "brita", (0.9, 0.86, 0.82), 2.0)
    m.material("trilho", None, (0.32, 0.30, 0.29), aspereza=0.35, metal=0.9)
    m.material("faixa", None, (0.98, 0.80, 0.10), aspereza=0.6, brilho=(0.98, 0.80, 0.10), energia=0.35)
    m.material("vagao", "metal_pintado", (0.78, 0.81, 0.84), 2.0, aspereza=0.8, metal=0.3)
    m.material("vagao_faixa", "metal_pintado", (0.80, 0.14, 0.12), 2.0, aspereza=0.8, metal=0.4)
    m.material("chapa", "chapa_xadrez", (0.62, 0.64, 0.68), 1.0, metal=0.3)
    m.material("aco", "metal_pintado", (0.30, 0.33, 0.37), 2.0, metal=0.3)
    m.material("grade", None, (0.95, 0.74, 0.16), aspereza=0.5, metal=0.3)
    m.material("madeira", "madeira", (1, 1, 1), 1.6)
    m.material("banco", "compensado", (0.9, 0.8, 0.7), 1.2)
    m.material("catraca", "metal_pintado", (0.70, 0.72, 0.74), 1.0, metal=0.3, aspereza=0.6)
    m.material("lampada", None, (1, 1, 1), brilho=(1.0, 0.96, 0.86), energia=3.0)
    m.material("placa", None, (0.05, 0.25, 0.12), brilho=(0.2, 1.0, 0.45), energia=1.6)
    m.material("maquina", "metal_pintado", (0.16, 0.18, 0.22), 1.0, metal=0.2)
    m.material("vitrine", None, (0.1, 0.1, 0.12), brilho=(0.25, 0.75, 1.0), energia=1.4)

    # --- Casca: plataformas (2 m de espessura, a via fica 1,2 m abaixo),
    #     saguões nas pontas, paredes e teto a 9 m.
    for sz, t in ((1, "Norte"), (-1, "Sul")):
        m.caixa("Casca", f"Plataforma{t}", (0, -1, sz * 9), (48, 2, 12), "piso")
        # A parede de fora tem três portas para a galeria de lojas.
        for i, (cx, comp) in enumerate(((-20.65, 8.7), (-7.5, 12.4), (7.5, 12.4), (20.65, 8.7))):
            m.caixa("Casca", f"Parede{t}{i}", (cx, 4.5, sz * 15.5), (comp, 9, 1), "azulejo")
        for i, cx in enumerate(PORTAS_GALERIA):
            m.caixa("Casca", f"Verga{t}{i}", (cx, 6.0, sz * 15.5), (2.6, 6, 1), "azulejo")
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
    #     Aqui é só a colisão: o que se vê é o modelo do Blender
    #     (tools/mapas/blender/trem.py), com estas mesmas medidas.
    m.caixa("Trem", "Piso", (0, -0.6, 0), (18, 1.2, 5.6), "vagao", oculta=True)
    m.caixa("Trem", "Teto", (0, 2.1, 0), (18, 0.2, 5.6), "vagao", oculta=True)
    for sz, t in ((1, "Norte"), (-1, "Sul")):
        for cx, comp, nome in ((-7.25, 3.5, "Oeste"), (0, 7, "Meio"), (7.25, 3.5, "Leste")):
            m.caixa("Trem", f"Lado{t}{nome}", (cx, 1.0, sz * 2.7), (comp, 2.0, 0.2), "vagao", oculta=True)
        m.caixa("Trem", f"Banco{t}", (0, 0.25, sz * 2.25), (5, 0.5, 0.6), "banco", oculta=True)
    for s, t in ((1, "Leste"), (-1, "Oeste")):
        for sz, tz in ((1, "Norte"), (-1, "Sul")):
            m.caixa("Trem", f"Ponta{t}{tz}", (s * 8.9, 1.0, sz * 1.8), (0.2, 2.0, 2.0), "vagao", oculta=True)
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
        for i, x in enumerate((-12, -4, 4, 12), 1):
            m.caixa(g, f"Lampada{i}", (x, 7.2, sz * 5.2), (2.4, 0.07, 0.22), "lampada", colide=False)
            m.caixa(g, f"Calha{i}", (x, 7.27, sz * 5.2), (2.6, 0.08, 0.34), "aco", colide=False)
            for k, dx in enumerate((-1.0, 1.0)):
                m.caixa(g, f"Haste{i}{k}", (x + dx, 7.95, sz * 5.2), (0.03, 1.3, 0.03), "aco", colide=False)
            m.luz(g, f"Luz{i}", (x, 6.7, sz * 5.2), (1.0, 0.95, 0.84), 2.2, 13)
        for i, x in enumerate((-9, 0, 9), 1):
            m.luz(g, f"LuzGaleria{i}", (x, 4.1, sz * 9.4), (1.0, 0.9, 0.72), 1.0, 7)
            m.luz(g, f"LuzServico{i}", (x, 3.6, sz * 13.4), (0.55, 1.0, 0.7), 0.9, 8)

    # --- As galerias de lojas, uma atrás de cada plataforma.
    m.material("piso_galeria", "piso", (0.78, 0.76, 0.72), 2.0)
    m.material("azulejo_galeria", "tijolo_pintado", (0.86, 0.84, 0.78), 2.2)
    m.material("balcao", "compensado", (0.62, 0.5, 0.4), 1.4)
    for sz, t in ((1, "Norte"), (-1, "Sul")):
        galeria(m, sz, t)

    # --- Saguões (as bases): escada para a galeria, catracas, bilheteria.
    for s, lado, cor in ((-1, "A", (0.45, 0.65, 1.0)), (1, "B", (1.0, 0.58, 0.28))):
        g = f"Base{lado}"
        for sz, t in ((1, "Norte"), (-1, "Sul")):
            m.rampa(g, f"Escada{t}", s * 23.4, s * 14, 5, sz * 9.5, 2.6, "chapa", degraus=28)
            m.caixa(g, f"Catraca{t}1", (s * 17.6, 0.5, sz * 5), (0.4, 1.0, 1.6), "catraca")
            m.caixa(g, f"Catraca{t}2", (s * 17.6, 0.5, sz * 7.2), (0.4, 1.0, 1.2), "catraca")
            m.caixa(g, f"Bilheteria{t}", (s * 21.5, 1.3, sz * 13.2), (3.0, 2.6, 2.6), "azulejo_azul" if s < 0 else "azulejo_laranja")
            m.caixa(g, f"Saida{t}", (s * 23.9, 3.0, sz * 4), (0.1, 0.4, 1.4), "placa", colide=False)
        m.letreiro(g, "Letra", f"metro_base_{lado.lower()}", (s * 23.93, 5.6, 0), 2.4, 90 if s < 0 else -90)
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
    for sz, t in ((1, "Norte"), (-1, "Sul")):
        m.rota(f"lojas{t}", [(-18, 0.5, sz * 13.4), (-15, 0.5, sz * 17.4), (-9, 0.5, sz * 19.6), (0, 0.5, sz * 18.4),
                             (9, 0.5, sz * 19.6), (15, 0.5, sz * 17.4), (18, 0.5, sz * 13.4)])
        m.rota(f"balcoes{t}", [(-15, 0.5, sz * 17), (-18.5, 0.5, sz * 22.6), (-9, 0.5, sz * 22.6), (-5, 0.5, sz * 21.2),
                               (0, 0.5, sz * 22), (5, 0.5, sz * 21.2), (9, 0.5, sz * 22.6), (18.5, 0.5, sz * 22.6), (15, 0.5, sz * 17)])
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
    m.material("nervura", "concreto_riscado", (0.62, 0.62, 0.64), 3.0)
    m.material("azulejo_faixa", "azulejo", (0.16, 0.30, 0.52), 1.6, relevo=0.8)
    m.material("borda", "concreto", (0.86, 0.86, 0.84), 2.0)
    m.material("persiana", "persiana", (0.62, 0.65, 0.68), 1.6, metal=0.4, aspereza=0.6)
    m.material("duto", "metal_pintado", (0.62, 0.64, 0.66), 2.0, metal=0.5, aspereza=0.5)
    m.material("cano_vermelho", None, (0.62, 0.10, 0.08), aspereza=0.5, metal=0.3)
    m.material("escuro", None, (0.02, 0.02, 0.025), aspereza=1.0)
    m.material("luz_vagao", None, (1, 1, 1), brilho=(1.0, 0.96, 0.88), energia=1.2)
    m.material("farol", None, (1, 1, 1), brilho=(1.0, 0.97, 0.85), energia=4.0)
    m.material("anuncio2", None, (0.08, 0.2, 0.25), brilho=(0.30, 0.85, 1.0), energia=1.1)
    m.material("loja_cafe", None, (0.36, 0.16, 0.10), aspereza=0.6)
    m.material("loja_banca", None, (0.62, 0.10, 0.11), aspereza=0.6)

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

    # --- O trem: o vagão modelado no Blender, e o que vai escrito nele.
    m.modelo("Trem", "Vagao", "trem_metro", (0, 0, 0))
    for sz in (1, -1):
        frente = 0 if sz > 0 else 180
        m.letreiro("Trem", f"Marca{sz}", "metro_trem_marca", (0, 0.6, sz * 2.815), 1.0, frente)
        for i, x in enumerate((-7.9, 7.9)):
            m.letreiro("Trem", f"Numero{sz}{i}", "metro_trem_numero", (x, 1.86, sz * 2.803), 0.42, frente)
        for i, x in enumerate((-2.55, 2.55)):                 # o mapa da linha, por dentro, ao lado das portas
            m.letreiro("Trem", f"MapaLinha{sz}{i}", "metro_mapa_linha", (x, 1.86, sz * 2.585), 1.45, 180 - frente)
    for s in (1, -1):
        for i, z in enumerate((-1.8, 1.8)):
            m.letreiro("Trem", f"Destino{s}{i}", "metro_trem_destino", (s * 9.004, 1.85, z), 0.95, 90 if s > 0 else -90, aceso=1.6)

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
        de_frente = 180 if sz > 0 else 0           # para quem está na plataforma, olhando a parede
        for i, (x, qual) in enumerate(((-11, "passe"), (-5, "radio"), (5, "feira"), (11, "passe"))):
            m.caixa(g, f"AnuncioMoldura{i}", (x, 2.45, sz * 11.235), (1.46, 2.02, 0.04), "aco", colide=False)
            m.letreiro(g, f"Anuncio{i}", f"metro_anuncio_{qual}", (x, 2.45, sz * 11.205), 1.36, de_frente, aceso=0.55)
        m.caixa(g, "PlacaEstacao", (0, 3.4, sz * 11.32), (4.4, 1.05, 0.12), "aco", colide=False)
        m.letreiro(g, "Nome", "metro_estacao", (0, 3.4, sz * 11.255), 4.2, de_frente)
        for i, x in enumerate((-9.5, 9.5)):
            m.letreiro(g, f"NomeParede{i}", "metro_estacao", (x, 4.1, sz * 11.24), 3.0, de_frente)
        # Placas penduradas da galeria: a saída (para a base mais perto) e a plataforma.
        for i, x in enumerate((-13, 13)):
            m.caixa(g, f"PlacaSaida{i}", (x, 3.72, sz * 6.2), (1.7, 0.56, 0.08), "aco", colide=False)
            m.letreiro(g, f"SaidaFrente{i}", "metro_saida_dir" if x > 0 else "metro_saida_esq", (x, 3.72, sz * 6.2 + 0.045), 1.6, 0, aceso=0.5)
            m.letreiro(g, f"SaidaVerso{i}", "metro_saida_esq" if x > 0 else "metro_saida_dir", (x, 3.72, sz * 6.2 - 0.045), 1.6, 180, aceso=0.5)
            for k, dx in enumerate((-0.7, 0.7)):
                m.caixa(g, f"HastePlaca{i}{k}", (x + dx, 4.25, sz * 6.2), (0.025, 0.6, 0.025), "aco", colide=False)
        qual = "metro_plataforma_1" if sz > 0 else "metro_plataforma_2"
        m.caixa(g, "PlacaPlataforma", (0, 3.72, sz * 6.2), (2.5, 0.66, 0.08), "aco", colide=False)
        m.letreiro(g, "PlataformaFrente", qual, (0, 3.72, sz * 6.2 + 0.045), 2.4, 0)
        m.letreiro(g, "PlataformaVerso", qual, (0, 3.72, sz * 6.2 - 0.045), 2.4, 180)
        for k, dx in enumerate((-1.0, 1.0)):
            m.caixa(g, f"HastePlataforma{k}", (dx, 4.3, sz * 6.2), (0.025, 0.6, 0.025), "aco", colide=False)
        # Pintado no piso, na frente das portas do trem.
        for i, x in enumerate((-4.5, 4.5)):
            m.letreiro(g, f"Vao{i}", "metro_vao", (x, 0.012, sz * 3.75), 2.4, 0 if sz > 0 else 180, chao=True)
        m.letreiro(g, "FaixaAviso", "metro_faixa", (-11.5, 0.012, sz * 3.72), 3.6, 0 if sz > 0 else 180, chao=True)
        m.letreiro(g, "Picho", "metro_picho" if sz > 0 else "metro_picho2", (sz * 11.5, 1.9, sz * 11.76), 1.8, 0 if sz > 0 else 180)
        # Banco com encosto e pés; lixeiras junto às colunas.
        m.caixa(g, "BancoEncosto", (3 * sz, 0.72, sz * 10.9), (2.6, 0.5, 0.07), "banco", colide=False)
        for i, x in enumerate((-9, 9)):
            lixo = [(0, 0), (0.2, 0), (0.24, 0.72), (0.26, 0.74), (0.2, 0.78), (0, 0.78)]
            m.forma(g, f"Lixeira{i}", F.revolucao(lixo, 14, suave=False), (x, 0, sz * 6.75), "duto")
            m.cilindro(g, f"LixeiraColisao{i}", (x, 0.39, sz * 6.75), 0.25, 0.78, "duto", oculta=True)
        # Corredor de serviço: canos na parede de fora, quadros de força, luminárias com grade.
        P.tubulacao(m, g, "CanoIncendio", [(-23, 3.45, sz * 14.86), (23, 3.45, sz * 14.86)], 0.07, "cano_vermelho", flange="ferro", cada=4.0)
        P.tubulacao(m, g, "CanoAgua", [(-23, 3.75, sz * 14.84), (23, 3.75, sz * 14.84)], 0.09, "duto", flange="aco", cada=4.0)
        for i in range(3):
            P.tubulacao(m, g, f"Eletroduto{i}", [(-23.8, 5.9 + i * 0.14, sz * 14.9), (23.8, 5.9 + i * 0.14, sz * 14.9)], 0.035, "ferro")
        for i, x in enumerate((-10.5, 4.5, 12)):
            m.caixa(g, f"Quadro{i}", (x, 1.5, sz * 14.84), (0.8, 1.3, 0.3), "duto", colide=False)
            m.caixa(g, f"QuadroPorta{i}", (x, 1.5, sz * 14.68), (0.7, 1.2, 0.02), "aco", colide=False)
        for i, x in enumerate((-9, 0, 9)):
            P.arandela(m, g, f"Arandela{i}", (x, 4.3, sz * 15.0), "z-" if sz > 0 else "z+", cor=(0.55, 1.0, 0.7), energia=0.0, alcance=1.0)
        # As portas da galeria: moldura de aço e o que há do outro lado.
        para_corredor = 180 if sz > 0 else 0
        for i, (x, placa) in enumerate(zip(PORTAS_GALERIA, ("metro_cafe", "metro_galeria", "metro_banca"))):
            for k, dx in enumerate((-1.36, 1.36)):
                m.caixa(g, f"PortaGaleriaGuia{i}{k}", (x + dx, 1.5, sz * 15.5), (0.12, 3.0, 1.08), "aco", colide=False)
            m.caixa(g, f"PortaGaleriaVerga{i}", (x, 3.06, sz * 15.5), (2.84, 0.12, 1.08), "aco", colide=False)
            m.letreiro(g, f"PortaGaleriaPlaca{i}", placa, (x, 3.34 if i != 1 else 3.3, sz * 14.985), 2.3 if i != 1 else 1.9, para_corredor)

        # --- A galeria de lojas por dentro.
        L = f"Galeria{t}"
        para_salao = 180 if sz > 0 else 0          # na parede do fundo, olhando o salão
        for s, lado, loja, cor_loja in ((-1, "Oeste", "metro_cafe", "loja_cafe"), (1, "Leste", "metro_banca", "loja_banca")):
            m.caixa(L, f"Tampo{lado}", (s * 13.5, 1.12, sz * 21.4), (7.2, 0.05, 0.86), "duto", colide=False)
            m.caixa(L, f"Frente{lado}", (s * 13.5, 0.55, sz * 21.04), (7.0, 0.9, 0.03), cor_loja, colide=False)
            m.caixa(L, f"Testeira{lado}", (s * 13.5, 3.3, sz * 23.9), (7.4, 0.9, 0.22), cor_loja, colide=False)
            m.letreiro(L, f"Nome{lado}", loja, (s * 13.5, 3.3, sz * 23.78), 4.6, para_salao, aceso=0.4)
            for i in range(4):                                  # o que há na estante
                m.caixa(L, f"Prateleira{lado}{i}", (s * 13.5, 0.5 + i * 0.5, sz * 23.5), (5.8, 0.04, 0.42), "madeira", colide=False)
            # A loja fechada na parede do lado: portão de enrolar e o nome.
            m.caixa(L, f"Porta{lado}", (s * 21.97, 1.5, sz * 20), (0.06, 3.0, 4.6), "persiana", colide=False)
            m.caixa(L, f"PortaCaixa{lado}", (s * 21.9, 3.15, sz * 20), (0.26, 0.34, 4.9), "aco", colide=False)
            m.letreiro(L, f"Farmacia{lado}", "metro_farmacia", (s * 21.76, 3.75, sz * 20), 2.8, -90 if s > 0 else 90)
            m.letreiro(L, f"Tecnica{lado}", "metro_tecnica", (s * 4.17, 2.0, sz * 23.0), 1.3, 90 if s > 0 else -90)
            m.caixa(L, f"BancoEncosto{lado}", (s * 13.5, 0.72, sz * 18.5), (2.6, 0.5, 0.07), "banco", colide=False)
            for i, x in enumerate((-6, 6)):
                m.caixa(L, f"Lampada{lado}{i}", (s * 13 + x, 4.46, sz * 19.2), (2.4, 0.06, 0.3), "lampada", colide=False)
            m.letreiro(L, f"Anuncio{lado}", "metro_anuncio_feira" if s < 0 else "metro_anuncio_radio", (s * 19.6, 2.3, sz * 23.985), 1.36, para_salao, aceso=0.5)
        # A sala de controle: a janela aberta sobre a mureta, o painel, a placa.
        m.caixa(L, "ControleTampo", (0, 1.02, sz * 20.5), (7.7, 0.05, 0.42), "duto", colide=False)
        m.letreiro(L, "ControlePlaca", "metro_controle", (0, 3.4, sz * 20.34), 3.4, para_salao)
        m.caixa(L, "ControlePainel", (0, 1.9, sz * 23.95), (5.6, 1.5, 0.08), "vitrine", colide=False)
        for i, x in enumerate((-1.6, 0, 1.6)):
            m.caixa(L, f"ControleTela{i}", (x, 1.25, sz * 23.2), (0.9, 0.55, 0.05), "vitrine", colide=False)
        m.letreiro(L, "Achados", "metro_achados", (-7.2 * sz, 2.5, sz * 23.985), 2.0, para_salao)
        m.letreiro(L, "PichoGaleria", "metro_picho2" if sz > 0 else "metro_picho", (7.4 * sz, 1.7, sz * 23.985), 1.9, para_salao)
        m.caixa(L, "Rodape", (0, 0.09, sz * 23.97), (44, 0.18, 0.05), "nervura", colide=False)
        # O que ficou pela galeria (objetos do Poly Haven).
        for s, lado in ((-1, "Oeste"), (1, "Leste")):
            P.objeto(m, L, f"EstanteControle{lado}", "estante_b", (s * 3.3, 0, sz * 23.6), colide=True)
            P.objeto(m, L, f"Caixas{lado}1", "caixa_papelao", (s * 10.6, 0, sz * 23.2), 15, colide=False)
            P.objeto(m, L, f"Caixas{lado}2", "caixa_papelao", (s * 11.2, 0, sz * 23.3), -25, colide=False)
            P.objeto(m, L, f"Caixas{lado}3", "caixa_papelao", (s * 10.85, 0.34, sz * 23.25), 40, colide=False)
            P.objeto(m, L, f"Carrinho{lado}", "carrinho", (s * 17.4, 0, sz * 22.6), 30 * s)
            P.objeto(m, L, f"PlacaPiso{lado}", "placa_piso", (s * 14.3, 0, sz * 17.0), 20, colide=False)
            P.objeto(m, L, f"Lixo{lado}1", "saco_lixo", (s * 18.7, 0, sz * 18.9), 0, colide=False)
            P.objeto(m, L, f"Lixo{lado}2", "saco_lixo", (s * 18.2, 0, sz * 19.2), 70, colide=False)
            P.objeto(m, L, f"Quadro{lado}", "quadro_forca", (s * 21.6, 0, sz * 16.9), 90 if s < 0 else -90)

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
            m.letreiro(g, f"Bilhetes{t}", "metro_bilhetes", (s * 21.5, 2.36, bz - sz * 0.02), 1.5, 180 if sz > 0 else 0)
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
