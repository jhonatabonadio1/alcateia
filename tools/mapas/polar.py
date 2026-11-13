"""
Estação Polar — mapa grande (110 × 70 m), a céu aberto, neve e
crepúsculo.

Uma base de pesquisa no gelo: garagem central atravessável, alojamentos
de dois andares em fileiras, passarelas suspensas, tanques de combustível
e uma torre de rádio.

As quatro perguntas do teste de mapa:
  1. Caminho que só o lobo faz: os becos de 3 m entre as fileiras de
     alojamentos (paredes de 5 m até o teto) e o teto dos veículos e
     contêineres (2,2 m, do caixote).
  2. Onde o humano vê o lobo chegando: o teto da garagem (6,4 m, com
     parapeito) e as passarelas — os dois por rampa, expostos.
  3. Corredor que o humano deve evitar: os becos dos alojamentos e o
     interior da garagem.
  4. Cair em cima de quem não olha: as passarelas de 5 m sobre as alas e
     a beira do teto da garagem sobre os portões.
"""
import math
import random

from . import formas as F
from . import pecas as PC
from .kit import Mapa, AZUL, LARANJA, giro_y


PORTA = lambda meio: (meio, 1.6, 0.0, 2.2)
JANELA = lambda meio: (meio, 1.4, 1.0, 2.2)


def alojamento_aberto(m, L, s, sz, mat):
    """
    A fileira da frente de cada alojamento, por dentro: dois cômodos de
    7 × 4 m (laboratório e refeitório) sob o mesmo teto por onde passa o
    caminho alto.

    Cada cômodo tem porta para a ala e porta para o beco; o da ponta de
    dentro tem mais uma, virada para a garagem; uma porta liga os dois, e
    cada um tem janela para a ala. Dá para atravessar a fileira por dentro
    em vez de contorná-la — e quem está no teto não vê quem passa.
    """
    frente, fundo = sz * 15.75, sz * 20.25
    m.caixa(L, "Laje", (s * 28, 4.75, sz * 18), (16, 0.5, 5), mat)
    m.parede(L, "Frente", "x", frente, s * 20, s * 36, 4.5, mat, [PORTA(s * 23), JANELA(s * 26), JANELA(s * 30), PORTA(s * 33)])
    m.parede(L, "Fundo", "x", fundo, s * 20, s * 36, 4.5, mat, [PORTA(s * 25.5), PORTA(s * 30.5)])
    m.parede(L, "PontaGaragem", "z", s * 20.25, sz * 16, sz * 20, 4.5, mat, [PORTA(sz * 18)])
    m.parede(L, "PontaRampa", "z", s * 35.75, sz * 16, sz * 20, 4.5, mat)
    m.parede(L, "Divisa", "z", s * 28, sz * 16, sz * 20, 4.5, mat, [PORTA(sz * 18)], espessura=0.3)
    # Bancada do laboratório e mesa do refeitório.
    m.caixa(L, "Bancada", (s * 22.6, 0.45, sz * 19.5), (2.6, 0.9, 0.8), "aco")
    m.caixa(L, "Mesa", (s * 33.6, 0.4, sz * 19.3), (2.6, 0.8, 1.0), "madeira")
    m.caixa(L, "Armario", (s * 35.1, 1.0, sz * 16.6), (0.6, 2.0, 1.0), "aco")
    m.luz(L, "LuzLab", (s * 24, 3.6, sz * 18), (0.80, 0.92, 1.0), 1.5, 7)
    m.luz(L, "LuzRefeitorio", (s * 32, 3.6, sz * 18), (1.0, 0.84, 0.62), 1.5, 7)


def montar():
    m = Mapa("polar", "Polar", 110, 70)

    m.material("neve", "neve", (0.93, 0.95, 1.0), 6.0, relevo=1.3)
    m.material("gelo", "penhasco", (0.74, 0.84, 0.98), 5.0, aspereza=0.6)
    m.material("concreto", "concreto", (0.70, 0.72, 0.75), 4.0)
    m.material("garagem", "metal_trapezio", (0.66, 0.70, 0.76), 2.4, metal=0.25)
    m.material("modulo_laranja", "conteiner", (0.95, 0.46, 0.16), 2.6, metal=0.25)
    m.material("modulo_branco", "conteiner", (0.80, 0.83, 0.88), 2.6, metal=0.25)
    m.material("modulo_vermelho", "conteiner", (0.78, 0.18, 0.15), 2.6, metal=0.25)
    m.material("base_azul", "conteiner", (0.26, 0.46, 0.88), 2.6, metal=0.25)
    m.material("base_laranja", "conteiner", (0.97, 0.54, 0.2), 2.6, metal=0.25)
    m.material("chapa", "chapa_xadrez", (0.60, 0.63, 0.68), 1.0, metal=0.3)
    m.material("grade", None, (0.95, 0.74, 0.16), aspereza=0.5, metal=0.3)
    m.material("tanque", "metal_pintado", (0.90, 0.92, 0.94), 3.0, metal=0.6, aspereza=0.8)
    m.material("cano", "ferrugem", (1, 1, 1), 1.5, metal=0.25)
    m.material("veiculo", "metal_pintado", (0.90, 0.48, 0.10), 2.0, metal=0.25)
    m.material("esteira", "asfalto", (0.5, 0.5, 0.5), 1.0)
    m.material("vidro", None, (0.10, 0.16, 0.22), aspereza=0.08, metal=0.25)
    m.material("madeira", "madeira", (1, 1, 1), 1.6)
    m.material("aco", "metal_pintado", (0.40, 0.43, 0.48), 2.0, metal=0.3)
    m.material("lampada", None, (1, 0.8, 0.5), brilho=(1.0, 0.66, 0.28), energia=4.0)
    m.material("farol", None, (1, 0.1, 0.1), brilho=(1.0, 0.12, 0.08), energia=4.0)

    # --- Chão de neve e paredões de gelo de 11 m, com blocos tortos na
    #     frente para a borda não parecer régua.
    m.caixa("Casca", "Chao", (0, -0.5, 0), (112, 1, 72), "neve")
    m.caixa("Casca", "GeloNorte", (0, 5.5, 35.5), (112, 11, 1), "gelo")
    m.caixa("Casca", "GeloSul", (0, 5.5, -35.5), (112, 11, 1), "gelo")
    m.caixa("Casca", "GeloLeste", (55.5, 5.5, 0), (1, 11, 70), "gelo")
    m.caixa("Casca", "GeloOeste", (-55.5, 5.5, 0), (1, 11, 70), "gelo")
    blocos = ((-40, 33.2, 9, 7, 3, 12), (-12, 33.6, 14, 9, 3, -8), (22, 33.4, 10, 6, 3, 10), (44, 33.0, 8, 8, 4, -14))
    for i, (x, z, w, h, d, yaw) in enumerate(blocos, 1):
        # Pedra de gelo: a colisão é o casco da própria forma.
        bloco = F.pedra(1.0, 40 + i, 1.0, 2, 0.22).escala(w / 2 * 1.08, h / 1.5, d / 2 * 1.25)
        m.forma("Casca", f"BlocoGeloN{i}", bloco, (x, 0, z), "gelo", yaw=yaw, colisao="convexa")
        m.forma("Casca", f"BlocoGeloS{i}", bloco, (-x, 0, -z), "gelo", yaw=yaw + 180, colisao="convexa")

    # --- Garagem central: 26 × 16 m, portões abertos a leste e oeste,
    #     porta lateral ao norte e ao sul, teto a 6,4 m com rampas por fora.
    g = "Garagem"
    for sz, t in ((1, "Norte"), (-1, "Sul")):
        for sx, tx in ((1, "Leste"), (-1, "Oeste")):
            m.caixa(g, f"Parede{t}{tx}", (sx * 7.25, 3, sz * 8), (11.5, 6, 0.6), "garagem")
            m.caixa(g, f"Portao{tx}{t}", (sx * 13, 3, sz * 6), (0.6, 6, 4.6), "garagem")
        m.caixa(g, f"Verga{t}", (0, 4.6, sz * 8), (3, 2.8, 0.6), "garagem")
        m.parapeito(g, f"Parapeito{t}", (0, 6.9, sz * 8.1), 20)
        # Rampa por fora, colada na parede, e o patamar que liga ao teto.
        m.rampa(g, f"Rampa{t}", -sz * 12, sz * 1, 6.4, sz * 9.9, 2.6, "chapa")
        m.caixa(g, f"Patamar{t}", (sz * 3, 6.2, sz * 9.9), (4, 0.4, 3.0), "chapa")
    for sx, tx in ((1, "Leste"), (-1, "Oeste")):
        m.caixa(g, f"Verga{tx}", (sx * 13, 5.2, 0), (0.6, 1.6, 7.4), "garagem")
    m.caixa(g, "Teto", (0, 6.2, 0), (26.6, 0.4, 16.6), "garagem")
    m.caixa(g, "Piso", (0, 0.012, 0), (25.4, 0.02, 15.4), "concreto", colide=False)
    # Dois tratores de neve lá dentro: 2,2 m, cobertura e degrau do lobo.
    for s, t in ((1, "Leste"), (-1, "Oeste")):
        m.caixa(g, f"Trator{t}", (s * 5, 1.1, -s * 4.6), (5, 2.2, 2.6), "veiculo", oculta=True)
        PC.caixote(m, g, f"Caixote{t}", (s * 1.2, 0.4, -s * 4.6), (1.4, 0.8, 1.4))
        m.caixa(g, f"Bancada{t}", (s * 9, 0.4, s * 6.6), (4, 0.8, 1.2), "aco")
        m.luz(g, f"Luz{t}", (s * 6, 5.2, 0), (1.0, 0.86, 0.66), 2.2, 14)
        m.caixa(g, f"Lampada{t}", (s * 6, 5.9, 0), (2.4, 0.12, 0.4), "lampada", colide=False)

    # --- Tanques ao norte, torre de rádio ao sul.
    for sx, t in ((1, "Leste"), (-1, "Oeste")):
        m.cilindro("Tanques", f"Tanque{t}", (sx * 6, 3, 25), 3.2, 6, "tanque", oculta=True)
        m.caixa("Tanques", f"Cano{t}", (sx * 14, 0.55, 22), (10, 0.5, 0.5), "cano", oculta=True)
        m.caixa("Tanques", f"Suporte{t}", (sx * 14, 0.15, 22), (0.6, 0.3, 0.9), "aco", colide=False)
    m.caixa("Tanques", "CanoMeio", (0, 0.55, 19.2), (0.5, 0.5, 5), "cano", oculta=True)
    m.luz("Tanques", "Luz", (0, 6.5, 22), (1.0, 0.66, 0.30), 3.0, 18)
    m.caixa("Torre", "Base", (0, 0.4, -26), (5, 0.8, 5), "concreto")
    m.caixa("Torre", "Mastro", (0, 10, -26), (0.9, 18.4, 0.9), "aco", oculta=True)
    m.caixa("Torre", "Farol", (0, 19.4, -26), (0.5, 0.5, 0.5), "farol", colide=False)
    m.luz("Torre", "LuzFarol", (0, 18.5, -26), (1.0, 0.2, 0.15), 2.0, 14)
    for sx, t in ((1, "Leste"), (-1, "Oeste")):
        m.caixa("Torre", f"Gerador{t}", (sx * 8, 1.1, -22), (3.4, 2.2, 2.2), "modulo_vermelho")
        PC.caixote(m, "Torre", f"Caixote{t}", (sx * 5.4, 0.4, -21.4), (1.4, 0.8, 1.4))

    for s, lado in ((-1, "A"), (1, "B")):
        for sz, t in ((1, "Norte"), (-1, "Sul")):
            L = f"Alojamento{lado}{t}"
            # Duas fileiras de dois andares com 3 m de beco entre elas.
            alojamento_aberto(m, L, s, sz, "modulo_laranja" if sz > 0 else "modulo_branco")
            m.caixa(L, "Fileira2", (s * 28, 2.5, sz * 26), (16, 5, 5), "modulo_branco" if sz > 0 else "modulo_laranja")
            m.caixa(L, "Friso1", (s * 28, 2.5, sz * 15.45), (16, 0.25, 0.1), "aco", colide=False)
            m.caixa(L, "Ponte", (s * 30, 4.9, sz * 22), (2, 0.2, 3.6), "chapa")
            m.parapeito(L, "Mureta", (s * 28, 5.5, sz * 15.75), 10)
            m.rampa(L, "Rampa", s * 46, s * 36, 5, sz * 18, 3, "chapa")
            m.luz(L, "LuzBeco", (s * 28, 3.4, sz * 22), (1.0, 0.66, 0.30), 1.6, 10)
            # Contêiner e caixote entre os alojamentos e a base.
            m.caixa(L, "Conteiner", (s * 40, 1.1, sz * 9), (6, 2.2, 2.6), "modulo_vermelho")
            PC.caixote(m, L, "Caixote", (s * 40, 0.4, sz * 11.3), (1.6, 0.8, 1.6))
            # Montes de neve dura nas alas.
            m.forma(L, "Monte", F.pedra(1.0, 9, 1.0, 2, 0.12).escala(2.5, 0.72, 1.5), (s * 17, 0, sz * 12.5), "neve",
                    yaw=18 * s * sz, colisao="convexa")
            PC.caixote(m, L, "CaixoteAla", (s * 30, 0.4, sz * 6), (1.6, 0.8, 1.6))

        P = f"Passarela{lado}"
        # Passarela de 5 m ligando os tetos das fileiras norte e sul.
        m.caixa(P, "Piso", (s * 21.5, 4.85, 0), (2.4, 0.3, 31), "chapa")
        m.parapeito(P, "Grade", (s * 22.6, 5.5, 0), 31, "z")
        for i, z in enumerate((-10, 0, 10), 1):
            m.caixa(P, f"Pilar{i}", (s * 21.5, 2.35, z), (0.5, 4.7, 0.5), "aco")
        m.caixa(P, "Poste", (s * 21.5, 3.6, 0), (0.2, 0.2, 0.2), "lampada", colide=False)
        m.luz(P, "Luz", (s * 21.5, 3.4, 0), (1.0, 0.66, 0.30), 2.4, 16)

        g = f"Base{lado}"
        cor = (0.45, 0.65, 1.0) if s < 0 else (1.0, 0.58, 0.28)
        mat = "base_azul" if s < 0 else "base_laranja"
        m.caixa(g, "ParedeNorte", (s * 50, 2.5, 11), (10, 5, 1), mat)
        m.caixa(g, "ParedeSul", (s * 50, 2.5, -11), (10, 5, 1), mat)
        m.caixa(g, "Teto", (s * 50, 5.25, 0), (10, 0.5, 23), mat)
        m.caixa(g, "PilarNorte", (s * 45.5, 2.5, 5), (1, 5, 1), "aco")
        m.caixa(g, "PilarSul", (s * 45.5, 2.5, -5), (1, 5, 1), "aco")
        m.caixa(g, "Piso", (s * 50, 0.012, 0), (10, 0.02, 21), "concreto", colide=False)
        PC.caixote(m, g, "CaixoteNorte", (s * 43, 0.4, 4), (2, 0.8, 2))
        PC.caixote(m, g, "CaixoteSul", (s * 43, 0.4, -4), (2, 0.8, 2))
        # A letra do time, num painel sobre a beira do teto.
        m.caixa(g, "PainelLetra", (s * 45.2, 7.3, 0), (0.14, 3.3, 3.3), "aco", colide=False)
        for pz in (-1.2, 1.2):
            m.caixa(g, f"PainelPe{pz}", (s * 45.3, 5.6, pz), (0.12, 0.4, 0.12), "aco", colide=False)
        m.letreiro(g, "Letra", f"polar_base_{lado.lower()}", (s * 45.2 - s * 0.08, 7.3, 0), 2.9, 90 if s < 0 else -90)
        for sz in (1, -1):
            m.letreiro(g, f"Estacao{sz}", "polar_estacao", (s * 50, 3.7, sz * 11.52), 7.0, 0 if sz > 0 else 180)
        m.luz(g, "Luz", (s * 50, 4.2, 0), cor, 2.0, 14)
        time = AZUL if s < 0 else LARANJA
        for z in (-7.5, -2.5, 2.5, 7.5):
            m.spawn(time, s * 52, z)
        for z in (-6, -2, 2, 6):
            m.spawn(time, s * 48.5, z)

    m.rota("centro", [(-42, 0.5, 0), (-30, 0.5, 0), (-16, 0.5, 0), (-6, 0.5, 0.5), (6, 0.5, -0.5), (16, 0.5, 0), (30, 0.5, 0), (42, 0.5, 0)])
    m.rota("norte", [(-40, 0.5, 13.5), (-20, 0.5, 13), (-8, 0.5, 14), (8, 0.5, 14), (20, 0.5, 13), (40, 0.5, 13.5)])
    m.rota("sul", [(-40, 0.5, -13.5), (-20, 0.5, -13), (-8, 0.5, -14), (8, 0.5, -14), (20, 0.5, -13), (40, 0.5, -13.5)])
    m.rota("tanques", [(-18, 0.5, 30), (-6, 0.5, 30.5), (0, 0.5, 26), (6, 0.5, 30.5), (18, 0.5, 30)])
    m.rota("torre", [(-18, 0.5, -30), (-7, 0.5, -29), (0, 0.5, -31), (7, 0.5, -29), (18, 0.5, -30)])
    for s, lado in ((-1, "A"), (1, "B")):
        for sz, t in ((1, "Norte"), (-1, "Sul")):
            m.rota(f"dentro{lado}{t}", [(s * 17, 0.5, sz * 18), (s * 22, 0.5, sz * 18), (s * 26, 0.5, sz * 17.4), (s * 30, 0.5, sz * 18),
                                        (s * 33, 0.5, sz * 17), (s * 33, 0.5, sz * 13.5), (s * 40, 0.5, sz * 13)])
        m.rota(f"beco{lado}Norte", [(s * 17, 0.5, 22), (s * 28, 0.5, 22), (s * 39, 0.5, 22)])
        m.rota(f"beco{lado}Sul", [(s * 17, 0.5, -22), (s * 28, 0.5, -22), (s * 39, 0.5, -22)])
        m.rota(f"tetos{lado}", [(s * 45, 0.5, 18), (s * 40, 2.5, 18), (s * 30, 5.5, 18), (s * 21.5, 5.5, 10),
                                (s * 21.5, 5.5, -10), (s * 30, 5.5, -18), (s * 40, 2.5, -18), (s * 45, 0.5, -18)])
    m.rota("tetoGaragem", [(-12, 0.5, 9.9), (-4, 4.0, 9.9), (3, 6.9, 9.5), (0, 6.9, 0), (-3, 6.9, -9.5), (4, 4.0, -9.9), (12, 0.5, -9.9)])

    m.ambiente = dict(ceu=dict(hdri="neve_nublada", giro=180, energia=0.8, ambiente=0.8),
                      luz=(0.52, 0.62, 0.86), luz_energia=0.7, exposicao=0.85, brilho=0.8, contraste=1.05, saturacao=1.0,
                      neblina=dict(cor=(0.70, 0.76, 0.86), densidade=0.005, ceu=0.15))
    m.sol = dict(yaw=-42, queda=22, cor=(1.0, 0.82, 0.70), energia=0.8, alcance=140)
    acabamento(m)
    return m


def _lagarta(comprimento, altura, largura):
    """A esteira de um trator de neve: perfil de pista de corrida."""
    r = altura / 2
    meio = comprimento / 2 - r
    perfil = [(meio + r * math.cos(math.radians(a)), r + r * math.sin(math.radians(a))) for a in range(-90, 91, 30)] + \
             [(-meio + r * math.cos(math.radians(a)), r + r * math.sin(math.radians(a))) for a in range(90, 271, 30)]
    return F.extrusao(perfil, largura, suave=True)


def _trator(m, g, nome, cx, cz, frente):
    """Trator de neve (5 × 2,2 × 2,6 m, a colisão é a caixa oculta):
    esteiras, casco, cabine envidraçada, lâmina e caçamba."""
    r = PC.Ref(m, g, (cx, 0, cz), 0 if frente > 0 else 180)
    for k, dz in enumerate((-1.02, 1.02)):
        r.forma(f"{nome}Esteira{k}", _lagarta(5.2, 0.78, 0.55), (0, 0, dz), "esteira")
        rodas = F.Malha()
        for q in range(5):
            rodas.juntar(F.revolucao([(0, -0.3), (0.26, -0.3), (0.26, 0.3), (0, 0.3)], 12, suave=False).rodar("x", 90), (-1.9 + q * 0.95, 0.39, 0))
        r.forma(f"{nome}Rodas{k}", rodas, (0, 0, dz), "aco")
    r.forma(nome + "Casco", F.caixa_chanfrada(4.7, 0.62, 1.6, 0.06), (0, 0.86, 0), "veiculo")
    r.forma(nome + "Capo", F.caixa_chanfrada(1.5, 0.55, 2.2, 0.08), (1.55, 1.4, 0), "veiculo")
    r.forma(nome + "Cabine", F.caixa_chanfrada(1.9, 1.05, 2.3, 0.1), (-0.1, 1.67, 0), "veiculo")
    r.caixa(nome + "Parabrisa", (0.86, 1.78, 0), (0.04, 0.62, 1.9), "vidro")
    r.caixa(nome + "VidroTras", (-1.06, 1.78, 0), (0.04, 0.55, 1.7), "vidro")
    for k, dz in enumerate((-1.16, 1.16)):
        r.caixa(f"{nome}VidroLado{k}", (-0.1, 1.8, dz), (1.4, 0.55, 0.04), "vidro")
    r.caixa(nome + "BarraLuz", (0.4, 2.24, 0), (0.12, 0.09, 1.3), "lampada")
    r.caixa(nome + "Cacamba", (-1.75, 1.42, 0), (1.3, 0.5, 2.2), "aco")
    lamina = [(0.34 * math.cos(math.radians(a)) - 0.34, 0.55 + 0.55 * math.sin(math.radians(a))) for a in range(-80, 81, 20)]
    lamina = lamina + [(x - 0.05, y) for x, y in reversed(lamina)]
    r.forma(nome + "Lamina", F.extrusao(lamina, 2.9, suave=True), (2.95, 0.05, 0), "aco")
    for k, dz in enumerate((-0.8, 0.8)):
        r.forma(f"{nome}Braco{k}", F.tubo([(2.3, 0.75, dz), (2.75, 0.6, dz)], 0.06, 6), (0, 0, 0), "aco")
    r.forma(nome + "Escape", F.tubo([(1.4, 1.65, 0.8), (1.4, 2.35, 0.8)], 0.05, 8), (0, 0, 0), "ferro")


def _trelica(base, topo, altura, niveis):
    """Torre treliçada: quatro pernas que afinam e as diagonais."""
    m = F.Malha()
    canto = lambda k, i: ((base + (topo - base) * k / niveis) * (1 if i in (0, 1) else -1), altura * k / niveis,
                          (base + (topo - base) * k / niveis) * (1 if i in (0, 3) else -1))
    for i in range(4):
        m.juntar(F.tubo([canto(0, i), canto(niveis, i)], 0.07, 6))
    for k in range(niveis):
        for i in range(4):
            j = (i + 1) % 4
            m.juntar(F.tubo([canto(k, i), canto(k, j)], 0.035, 5, tampas=False))
            a, b = (canto(k, i), canto(k + 1, j)) if k % 2 == 0 else (canto(k, j), canto(k + 1, i))
            m.juntar(F.tubo([a, b], 0.03, 5, tampas=False))
    for i in range(4):
        m.juntar(F.tubo([canto(niveis, i), canto(niveis, (i + 1) % 4)], 0.035, 5, tampas=False))
    return m


def acabamento(m):
    """Da maquete à estação: paredões de gelo irregulares e serra ao fundo,
    neve nos telhados, sincelos, janelas acesas, portas, tratores de
    esteira, tanques com cúpula e escada, torre treliçada, canos com
    flange, refletores e neve caindo."""
    PC.materiais(m)
    rnd = random.Random(11)
    m.material("neve_telhado", "neve", (0.94, 0.96, 1.0), 3.0, relevo=1.2)
    m.material("serra", "neve", (0.70, 0.74, 0.82), 26.0, relevo=2.0)
    m.material("sincelo", None, (0.78, 0.88, 0.98), aspereza=0.15, metal=0.2)
    m.material("persiana", "persiana", (0.56, 0.6, 0.64), 1.6, metal=0.4, aspereza=0.6)
    m.material("moldura", None, (0.20, 0.22, 0.25), aspereza=0.6, metal=0.4)
    m.material("porta", "metal_pintado", (0.30, 0.34, 0.40), 1.2, metal=0.3)
    m.material("prato", None, (0.86, 0.88, 0.9), aspereza=0.4, metal=0.3, dupla=True)
    m.material("faixa_risco", None, (0.95, 0.74, 0.12), aspereza=0.6)
    m.material("bandeira_azul", None, (0.22, 0.42, 0.85), aspereza=0.9, dupla=True)
    m.material("bandeira_laranja", None, (0.95, 0.52, 0.16), aspereza=0.9, dupla=True)
    m.material("refletor", None, (1, 1, 1), brilho=(0.92, 0.96, 1.0), energia=4.0)
    m.nevasca()

    # --- Paredões: pedras de gelo na frente do muro reto, de alturas diferentes.
    k = 0
    for face, fixo, de, ate in (("z", 36.6, -54, 54), ("z", -36.6, -54, 54), ("x", 56.6, -34, 34), ("x", -56.6, -34, 34)):
        u = de
        while u < ate:
            larg = rnd.uniform(7, 12)
            alt = rnd.uniform(10.5, 16.5)
            pedra = F.pedra(1.0, 60 + k, 1.0, 2, 0.3).escala(larg * 0.62, alt / 1.45, rnd.uniform(2.0, 2.9))
            pos = (u + larg / 2, -0.3, fixo) if face == "z" else (fixo, -0.3, u + larg / 2)
            m.forma("Casca", f"Paredao{k}", pedra, pos, "gelo", yaw=(0 if face == "z" else 90) + rnd.uniform(-9, 9))
            u += larg * 0.82
            k += 1

    # --- A serra em volta: sobe atrás do paredão e fecha o horizonte.
    def altura(x, z):
        d = max(abs(x) - 58, abs(z) - 38, 0.0)
        if d <= 0:
            return -3.0
        sobe = min(d / 14.0, 1.0)
        longe = min(max(d - 8, 0) / 120.0, 1.0)
        ondas = 0.5 + 0.25 * math.sin(x * 0.031 + 1.7) * math.cos(z * 0.027) + 0.25 * math.sin(x * 0.011 - z * 0.017 + 0.6)
        crista = 0.5 + 0.5 * math.sin(x * 0.06 + z * 0.045)
        return -3.0 + 14.0 * sobe * sobe * (3 - 2 * sobe) + longe * (30 + 46 * ondas + 14 * crista * ondas)
    m.forma("Casca", "Serra", F.relevo(560, 500, 56, 50, altura), (0, 0, 0), "serra")

    # --- Garagem: tesouras, portões de enrolar recolhidos, placa, neve no teto, antena.
    g = "Garagem"
    for i, x in enumerate((-8.6, -4.3, 0, 4.3, 8.6)):
        m.caixa(g, f"Tesoura{i}", (x, 5.72, 0), (0.22, 0.5, 15.4), "aco", colide=False)
    for sx, tx in ((1, "Leste"), (-1, "Oeste")):
        m.caixa(g, f"Cortina{tx}", (sx * 13, 4.15, 0), (0.12, 0.5, 7.4), "persiana", colide=False)
        m.caixa(g, f"Rolo{tx}", (sx * 12.6, 4.75, 0), (0.5, 0.5, 7.6), "aco", colide=False)
        for k2, dz in enumerate((-3.75, 3.75)):
            m.caixa(g, f"Guia{tx}{k2}", (sx * 13.33, 2.2, dz), (0.08, 4.4, 0.16), "faixa_risco", colide=False)
        m.letreiro(g, f"Placa{tx}", "polar_garagem", (sx * 13.32, 5.25, 0), 4.6, 90 if sx > 0 else -90)
    m.forma(g, "NeveTeto", F.caixa_chanfrada(25.6, 0.14, 15.2, 0.06), (0, 6.46, 0), "neve_telhado")
    m.forma(g, "AntenaMastro", F.tubo([(-8, 6.4, 3), (-8, 8.6, 3)], 0.08, 8), (0, 0, 0), "aco")
    prato = F.revolucao([(0.02, 0), (0.45, 0.04), (0.95, 0.2), (1.35, 0.42)], 20, suave=True).rodar("x", 62)
    m.forma(g, "AntenaPrato", prato, (-8, 8.5, 3), "prato", yaw=35)
    _trator(m, g, "TratorLeste", 5, -4.6, 1)
    _trator(m, g, "TratorOeste", -5, 4.6, -1)
    for s2, t in ((1, "Leste"), (-1, "Oeste")):
        # Bancada: tampo, morsa, caixa de ferramentas e o painel na parede.
        m.caixa(g, f"BancadaTampo{t}", (s2 * 9, 0.83, s2 * 6.6), (4.1, 0.06, 1.3), "madeira", colide=False)
        m.caixa(g, f"Ferramentas{t}", (s2 * 10.2, 1.0, s2 * 6.7), (0.7, 0.3, 0.4), "modulo_vermelho", colide=False)
        m.caixa(g, f"Painel{t}", (s2 * 9, 1.9, s2 * 7.66), (3.2, 1.2, 0.04), "madeira", colide=False)
        m.caixa(g, f"Haste{t}", (s2 * 6, 6.0, 0), (0.04, 0.3, 0.04), "aco", colide=False)
    PC.barril(m, g, "Tambor1", (11.6, 0, 6.8), "barril")
    PC.barril(m, g, "Tambor2", (11.0, 0, 7.1), "barril_vermelho")
    PC.barril(m, g, "Tambor3", (-11.6, 0, -6.8), "barril")
    PC.pneus(m, g, "Pneus", (-11.4, 0, 6.6), 3)

    # --- Tanques: cúpula, cintas, escada de marinheiro, guarda-corpo, canos com flange.
    tanque = [(0, 0), (3.28, 0), (3.28, 0.22), (3.2, 0.3), (3.2, 1.9), (3.24, 1.92), (3.24, 2.04), (3.2, 2.06), (3.2, 3.9),
              (3.24, 3.92), (3.24, 4.04), (3.2, 4.06), (3.2, 5.3)] + \
             [(3.2 * math.cos(math.radians(a)), 5.3 + 0.95 * math.sin(math.radians(a))) for a in range(15, 90, 15)] + [(0, 6.25)]
    for sx, t in ((1, "Leste"), (-1, "Oeste")):
        x = sx * 6
        m.forma("Tanques", f"TanqueCasco{t}", F.revolucao(tanque, 28, suave=False), (x, 0, 25), "tanque")
        m.forma("Tanques", f"TanqueNeve{t}", F.revolucao([(0, 0.25)] + [(2.6 * math.sin(math.radians(a)), 0.25 * math.cos(math.radians(a)))
                                                                    for a in range(15, 91, 15)][::1], 20), (x, 5.98, 25), "neve_telhado")
        escada = F.Malha()
        for dx in (-0.22, 0.22):
            escada.juntar(F.tubo([(dx, 0, 0), (dx, 6.3, 0)], 0.025, 6))
        for q in range(18):
            escada.juntar(F.tubo([(-0.22, 0.3 + q * 0.33, 0), (0.22, 0.3 + q * 0.33, 0)], 0.018, 5, tampas=False))
        m.forma("Tanques", f"TanqueEscada{t}", escada, (x, 0, 25 - 3.34), "aco")
        m.caixa("Tanques", f"TanqueFaixa{t}", (x, 1.2, 25 - 3.21), (2.2, 0.5, 0.03), "faixa_risco", colide=False)
        m.letreiro("Tanques", f"TanqueNome{t}", "polar_diesel_01" if sx > 0 else "polar_diesel_02", (x, 3.0, 25 - 3.23), 2.6, 180)
        PC.tubulacao(m, "Tanques", f"CanoTubo{t}", [(x + sx * 3.1, 0.55, 23.6), (sx * 9.2, 0.55, 23.6), (sx * 9.2, 0.55, 22), (sx * 19, 0.55, 22),
                                                    (sx * 19.5, 0.55, 22), (sx * 19.5, -0.3, 22)], 0.24, "cano", flange="aco", cada=2.4)
        m.forma("Tanques", f"Valvula{t}", F.toro(0.24, 0.03, 14, 6).rodar("x", 90), (sx * 12, 0.95, 22), "modulo_vermelho")
        m.forma("Tanques", f"ValvulaHaste{t}", F.tubo([(sx * 12, 0.75, 22), (sx * 12, 0.95, 22)], 0.03, 6), (0, 0, 0), "aco")
    PC.tubulacao(m, "Tanques", "CanoMeioTubo", [(0, 0.55, 16.7), (0, 0.55, 21.7), (0, 0.55, 23.6), (2.9, 0.55, 23.6)], 0.24, "cano", flange="aco", cada=2.0)

    # --- Torre de rádio: treliça, estais, pratos e a luz de topo.
    m.forma("Torre", "Trelica", _trelica(1.45, 0.3, 18.4, 7), (0, 0.8, -26), "aco")
    m.forma("Torre", "Ponta", F.tubo([(0, 19.2, -26), (0, 22.5, -26)], 0.04, 6), (0, 0, 0), "aco")
    for i, (dx, dz) in enumerate(((1, 1), (-1, 1), (-1, -1), (1, -1))):
        PC.cabo(m, "Torre", f"Estai{i}", (dx * 0.4, 15.5, -26 + dz * 0.4), (dx * 8.5, 0.2, -26 + dz * 7.5), 0.15, "ferro", 0.014, 6)
    pratinho = F.revolucao([(0.02, 0), (0.3, 0.05), (0.55, 0.16)], 14).rodar("x", 90)
    m.forma("Torre", "Prato1", pratinho, (0.5, 13.5, -25.5), "prato", yaw=20)
    m.forma("Torre", "Prato2", pratinho, (-0.5, 11.2, -25.5), "prato", yaw=-30)
    for sx, t in ((1, "Leste"), (-1, "Oeste")):
        # Gerador: venezianas, escape com chapéu, painel e trenó.
        x = sx * 8
        vene = F.Malha()
        for q in range(9):
            vene.juntar(F.caixa(2.4, 0.07, 0.05, (0, 0.55 + q * 0.14, 0)).mover((0, 0, 0)))
        m.forma("Torre", f"GeradorVeneziana{t}", vene, (x, 0, -22 + 1.12), "ferro")
        m.forma("Torre", f"GeradorVeneziana2{t}", vene, (x, 0, -22 - 1.12), "ferro")
        m.forma("Torre", f"GeradorEscape{t}", F.tubo([(x + 1.1, 2.2, -22), (x + 1.1, 3.0, -22), (x + 1.35, 3.2, -22)], 0.07, 8), (0, 0, 0), "ferro")
        m.caixa("Torre", f"GeradorPainel{t}", (x - sx * 1.72, 1.4, -22), (0.05, 0.6, 0.8), "aco", colide=False)
        m.caixa("Torre", f"GeradorLuz{t}", (x - sx * 1.75, 1.55, -21.8), (0.03, 0.06, 0.06), "refletor", colide=False)
        m.forma("Torre", f"GeradorNeve{t}", F.caixa_chanfrada(3.5, 0.12, 2.3, 0.05), (x, 2.24, -22), "neve_telhado")
        for k2, dz in enumerate((-0.8, 0.8)):
            m.caixa("Torre", f"GeradorTreno{t}{k2}", (x, 0.06, -22 + dz), (3.7, 0.12, 0.18), "ferro", colide=False)

    # --- Alojamentos: dois andares de janelas, portas, costelas, neve e sincelos.
    sincelos = F.Malha()
    for q in range(44):
        comp = rnd.uniform(0.12, 0.55)
        sincelos.juntar(F.revolucao([(0, -comp), (0.035, 0)], 5, suave=False), (-7.9 + q * 0.367 + rnd.uniform(-0.08, 0.08), 0, 0))
    for s, lado in ((-1, "A"), (1, "B")):
        for sz, t in ((1, "Norte"), (-1, "Sul")):
            L = f"Alojamento{lado}{t}"
            frente = "z-" if sz > 0 else "z+"
            fundo = "z+" if sz > 0 else "z-"
            for n, zc in ((1, 18), (2, 26)):
                zf, zt = sz * (zc - 2.5), sz * (zc + 2.5)
                m.forma(L, f"Neve{n}", F.caixa_chanfrada(16.3, 0.13, 5.3, 0.06), (s * 28, 5.06, sz * zc), "neve_telhado")
                m.forma(L, f"Sincelos{n}", sincelos, (s * 28, 5.0, zf - sz * 0.08), "sincelo")
                m.forma(L, f"SincelosFundo{n}", sincelos, (s * 28, 5.0, zt + sz * 0.08), "sincelo", yaw=180)
                costelas = F.Malha()
                for q in range(5):
                    costelas.juntar(F.caixa(0.14, 5.0, 0.07, (-8 + q * 4, 2.5, 0)))
                m.forma(L, f"Costelas{n}", costelas, (s * 28, 0, zf - sz * 0.03), "aco")
                m.forma(L, f"CostelasFundo{n}", costelas, (s * 28, 0, zt + sz * 0.03), "aco")
                if n == 2:
                    # Só na fileira fechada: na que se entra, a sapata ficaria
                    # acima do piso de dentro, e os pés afundariam nela.
                    m.caixa(L, f"Sapata{n}", (s * 28, 0.12, sz * zc), (16.1, 0.24, 5.1), "aco", colide=False)
                for q, dx in enumerate((-6, -2, 2, 6)):
                    for andar, y in enumerate((1.55, 3.85)):
                        aceso = rnd.random() < 0.55
                        if n == 1 and andar == 0:
                            continue            # na fileira em que se entra, embaixo são portas e janelas de verdade
                        PC.janela(m, L, f"Janela{n}{q}{andar}", (s * 28 + dx, y, zf), frente, 1.3, 0.85, "moldura",
                                  "vidro_aceso" if aceso else "vidro", peitoril=False)
                # Porta para o beco entre as fileiras, com degrau e luz.
                lado_beco, face_beco = (zt, fundo) if n == 1 else (zf, frente)
                if n == 2:
                    PC.porta(m, L, f"Porta{n}", (s * 24, 0, lado_beco), face_beco, 0.95, 2.05, "moldura", "porta")
                PC.arandela(m, L, f"LuzPorta{n}", (s * 24, 2.45, lado_beco), face_beco, energia=0, mat="moldura")
            # A fileira aberta: marcos de aço nas portas e janelas, piso, e o que está escrito.
            de_frente = 180 if sz > 0 else 0
            for i, (x, larg, y0) in enumerate(((23, 1.6, 0), (26, 1.4, 1.0), (30, 1.4, 1.0), (33, 1.6, 0))):
                for k, dx in enumerate((-1, 1)):
                    m.caixa(L, f"Marco{i}{k}", (s * x + dx * (larg / 2 + 0.04), (y0 + 2.2) / 2, sz * 15.75), (0.08, 2.2 - y0, 0.6), "moldura", colide=False)
                m.caixa(L, f"MarcoVerga{i}", (s * x, 2.24, sz * 15.75), (larg + 0.16, 0.08, 0.6), "moldura", colide=False)
            m.caixa(L, "PisoDentro", (s * 28, 0.012, sz * 18), (15, 0.02, 4), "chapa", colide=False)
            m.caixa(L, "BancadaTampo", (s * 22.6, 0.92, sz * 19.5), (2.7, 0.04, 0.9), "prato", colide=False)
            m.letreiro(L, "Nome", f"polar_modulo_{lado}{'1' if sz > 0 else '2'}", (s * 28, 3.05, sz * 15.44), 2.6, de_frente)
            m.letreiro(L, "PlacaLab", "polar_lab", (s * 24.5, 2.55, sz * 15.44), 1.3, de_frente)
            m.letreiro(L, "PlacaRefeitorio", "polar_refeitorio", (s * 31.5, 2.55, sz * 15.44), 1.3, de_frente)
            m.letreiro(L, "PlacaDeposito", "polar_deposito", (s * 28, 2.55, sz * 20.56), 1.3, 180 - de_frente)
            # O que há nos cômodos e em volta (objetos do Poly Haven).
            PC.objeto(m, L, "BauFerramenta", "bau_ferramenta", (s * 22.6, 0.94, sz * 19.5), 0, colide=False)
            PC.objeto(m, L, "EstanteLab", "estante_b", (s * 27.4, 0, sz * 16.6), 90)
            PC.objeto(m, L, "CarroFerramenta", "carro_ferramenta", (s * 26, 0.02, sz * 16.6), 0)
            PC.objeto(m, L, "Municao", "caixa_municao", (s * 33.4, 0.8, sz * 19.3), 30, colide=False)
            PC.objeto(m, L, "Galao1", "galao_verde", (s * 34.5, 0.02, sz * 17.7), 20, colide=False)
            PC.objeto(m, L, "Galao2", "galao_verde", (s * 34.1, 0.02, sz * 17.5), -35, colide=False)
            PC.objeto(m, L, "CaixaMilitar1", "caixa_militar_madeira", (s * 41.8, 0, sz * 11.6), 0, som="madeira")
            PC.objeto(m, L, "CaixaMilitar2", "caixa_militar_madeira", (s * 41.8, 0.46, sz * 11.6), 8, colide=False)
            PC.objeto(m, L, "TamborAzul1", "barril_azul", (s * 36.2, 0, sz * 8.2))
            PC.objeto(m, L, "TamborAzul2", "barril_azul", (s * 36.25, 0, sz * 9.0), 50)
            PC.objeto(m, L, "Barreira", "barreira", (s * 17, 0, sz * 5.6), 90, som="concreto")
            PC.objeto(m, L, "Pedra", "pedra_c", (s * 49, 0, sz * 29), 30 * s, som="gelo")
            # A rampa de chapa: corrimão dos dois lados.
            for k2, dz in enumerate((-1.45, 1.45)):
                PC.corrimao(m, L, f"CorrimaoRampa{k2}", [(s * 45.6, 0.1, sz * 18 + dz), (s * 36.2, 5.0, sz * 18 + dz)], "grade", 1.0, 0.03, 1.9)
            # Contêiner: cantoneiras, barras da porta, neve em cima.
            cx, cz = s * 40, sz * 9
            cantos = F.Malha()
            for dx in (-3, 3):
                for dz in (-1.3, 1.3):
                    cantos.juntar(F.caixa(0.14, 2.2, 0.14, (dx, 1.1, dz)))
            m.forma(L, "ConteinerCantos", cantos, (cx, 0, cz), "aco")
            barras = F.Malha()
            for dz in (-0.9, -0.3, 0.3, 0.9):
                barras.juntar(F.tubo([(0, 0.15, dz), (0, 2.05, dz)], 0.022, 5))
            m.forma(L, "ConteinerBarras", barras, (cx - s * 3.03, 0, cz), "aco")
            m.forma(L, "ConteinerNeve", F.caixa_chanfrada(6.1, 0.12, 2.7, 0.05), (cx, 2.24, cz), "neve_telhado")
            PC.barril(m, L, "Tambor", (s * 36.6, 0, sz * 10.6), "barril_vermelho")

        P = f"Passarela{lado}"
        for i, z in enumerate((-10, 0, 10), 1):
            for k2, dz in enumerate((-1.6, 1.6)):
                m.forma(P, f"MaoFrancesa{i}{k2}", F.tubo([(s * 21.5, 3.2, z), (s * 21.5, 4.68, z + dz)], 0.05, 6), (0, 0, 0), "aco")
        m.forma(P, "NevePiso", F.caixa_chanfrada(2.2, 0.06, 30.6, 0.02), (s * 21.4, 5.02, 0), "neve_telhado")

        g = f"Base{lado}"
        bandeira = "bandeira_azul" if s < 0 else "bandeira_laranja"
        m.forma(g, "NeveTeto", F.caixa_chanfrada(10.2, 0.16, 23.2, 0.07), (s * 50, 5.56, 0), "neve_telhado")
        m.caixa(g, "Testeira", (s * 45.1, 5.0, 0), (0.2, 0.7, 23), "aco", colide=False)
        for i, z in enumerate((-8, 0, 8)):
            m.caixa(g, f"Refletor{i}", (s * 45.3, 4.55, z), (0.3, 0.2, 0.7), "refletor", colide=False)
            m.caixa(g, f"Bandeira{i}", (s * 44.95, 3.6, z + 3.9), (0.02, 1.7, 1.1), bandeira, colide=False)
        for i, z in enumerate((-9.5, -6.5, 6.5, 9.5)):
            m.caixa(g, f"Armario{i}", (s * 54.4, 1.0, z), (0.6, 2.0, 1.8), "aco", colide=False)
        PC.sacos(m, g, "SacosNorte", (s * 44.4, 0, 8.6), 3.1, 3, yaw=90)
        PC.sacos(m, g, "SacosSul", (s * 44.4, 0, -8.6), 3.1, 3, yaw=90)
        PC.poste(m, g, "PosteNorte", (s * 44.4, 0, 12.4), 6.0, 0 if s < 0 else 180, "aco", "refletor", (0.9, 0.95, 1.0), 1.8, 16)
        PC.poste(m, g, "PosteSul", (s * 44.4, 0, -12.4), 6.0, 0 if s < 0 else 180, "aco", "refletor", (0.9, 0.95, 1.0), 1.8, 16)

    PC.poste(m, "Garagem", "PosteNE", (14.6, 0, 11.6), 7.0, 0, "aco", "refletor", (0.9, 0.95, 1.0), 2.0, 18)
    PC.poste(m, "Garagem", "PosteSO", (-14.6, 0, -11.6), 7.0, 180, "aco", "refletor", (0.9, 0.95, 1.0), 2.0, 18)
