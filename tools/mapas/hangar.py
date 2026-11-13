"""
Hangar — mapa médio (84 × 48 m), a céu aberto, manhã clara.

O pátio de uma base aérea: a plataforma de manobra no centro, quatro
oficinas de tijolo com telhado nas laterais, pontes de serviço por cima
das alas, contêineres, e os galpões dos dois times nas pontas.

As medidas são de propósito, não de gosto. As quatro perguntas do teste
de mapa:
  1. Caminho que só o lobo faz: os becos de 3 m atrás das oficinas. Duas
     paredes paralelas são o que a corrida em parede precisa para subir
     ricocheteando até o telhado de 5 m. O humano chega lá em cima pelas
     rampas — pelo meio, exposto.
  2. Onde o humano vê o lobo chegando: a plataforma do centro, 2 m acima
     do chão, com parapeito, de frente para as alas abertas.
  3. Corredor que o humano deve evitar: os mesmos becos. Três metros é
     distância de garra, não de fuzil.
  4. Cair em cima de quem não olha: as pontes de 5 m sobre as alas, sem
     grade do lado de dentro.

Alturas conferidas contra a física: o humano pula 0,99 m (sobe caixote
de 0,8, não sobe contêiner); o Bruto pula 1,54 m (do caixote alcança o
contêiner de 2,2); ninguém pula até 5 m sem parede.

Foi o primeiro mapa, nascido em caixa cinza (tools/gerar_hangar.py, que
não existe mais). A colisão de hoje é a daquele, peça por peça — com uma
diferença: as oficinas, que eram bloco maciço, agora se entra nelas.
"""
import math

from . import formas as F
from . import pecas as P
from .kit import Mapa, AZUL, LARANJA

PORTA = lambda meio: (meio, 1.6, 0.0, 2.2)
JANELA = lambda meio: (meio, 1.4, 1.0, 2.2)

# O que funciona em cada oficina: (lado, norte/sul) -> letreiro.
OFICINAS = {(-1, 1): "hangar_oficina", (1, 1): "hangar_almoxarifado", (-1, -1): "hangar_ferramentaria", (1, -1): "hangar_pintura"}


def oficina(m, g, s, sz, t):
    """
    Uma oficina (12 × 9 m, telhado a 5 m), por dentro: a sala da frente,
    com porta e duas janelas para a ala, e o depósito dos fundos, com uma
    porta para o lado do centro e outra para o lado da base. Uma porta
    liga os dois.

    A parede de trás fica inteira: é uma das duas paredes do beco, e o
    lobo precisa dela para subir. O telhado e a mureta são os de sempre.
    """
    m.caixa(g, f"Bloco{t}Laje", (s * 20, 4.75, sz * 16.5), (12, 0.5, 9), "tijolo")
    m.parede(g, f"Bloco{t}Frente", "x", sz * 12.25, s * 14, s * 26, 4.5, "tijolo", [PORTA(s * 20), JANELA(s * 16.8), JANELA(s * 23.2)])
    m.parede(g, f"Bloco{t}Fundo", "x", sz * 20.75, s * 14, s * 26, 4.5, "tijolo")
    m.parede(g, f"Bloco{t}Centro", "z", s * 14.25, sz * 12.5, sz * 20.5, 4.5, "tijolo", [PORTA(sz * 18.6)])
    m.parede(g, f"Bloco{t}Base", "z", s * 25.75, sz * 12.5, sz * 20.5, 4.5, "tijolo", [PORTA(sz * 18.6)])
    m.parede(g, f"Bloco{t}Divisa", "x", sz * 16.6, s * 14.5, s * 25.5, 4.5, "tijolo", [PORTA(s * 17.2)], espessura=0.3)
    m.caixa(g, f"Bloco{t}Bancada", (s * 23, 0.45, sz * 15.9), (3.4, 0.9, 0.8), "aco")
    m.caixa(g, f"Bloco{t}Armario", (s * 24.9, 1.0, sz * 13.4), (0.7, 2.0, 1.2), "aco")
    m.luz(g, f"Bloco{t}LuzSala", (s * 20, 3.6, sz * 14.4), (1.0, 0.92, 0.78), 1.5, 7)
    m.luz(g, f"Bloco{t}LuzDeposito", (s * 20, 3.6, sz * 18.7), (1.0, 0.86, 0.66), 1.4, 7)


def montar():
    m = Mapa("hangar", "Hangar", 84, 48)

    m.material("piso", "piso_concreto", (0.92, 0.92, 0.94), 4.0)
    m.material("muro", "concreto_laje", (0.80, 0.80, 0.82), 4.0)
    m.material("tijolo", "tijolo_fabrica", (1.0, 0.98, 0.96), 2.4)
    m.material("aco", "metal_pintado", (0.42, 0.47, 0.54), 2.0, metal=0.4, aspereza=0.6)
    m.material("rampa", "chapa_xadrez", (0.66, 0.68, 0.72), 1.0, metal=0.3)
    m.material("plataforma", "asfalto", (0.9, 0.9, 0.92), 3.0)
    m.material("amarelo", "metal_pintado", (0.95, 0.74, 0.16), 2.0, aspereza=0.6, metal=0.2)
    m.material("base_azul", "conteiner", (0.26, 0.46, 0.88), 2.6, metal=0.25)
    m.material("base_laranja", "conteiner", (0.97, 0.52, 0.2), 2.6, metal=0.25)
    m.material("piso_azul", "piso_concreto", (0.42, 0.56, 0.86), 4.0)
    m.material("piso_laranja", "piso_concreto", (0.92, 0.62, 0.42), 4.0)
    m.material("conteiner_verde", "conteiner", (0.34, 0.52, 0.30), 2.6, metal=0.25)
    m.material("conteiner_azul", "conteiner", (0.18, 0.50, 0.52), 2.6, metal=0.25)
    m.material("madeira", "madeira", (1, 1, 1), 1.6)
    m.material("farol", None, (0.20, 0.85, 1.00), brilho=(0.20, 0.85, 1.00), energia=2.2)

    # --- Perímetro: 12 m de muro. O beco só tem duas paredes até 5 m de
    #     altura, então o lobo sobe até o telhado e não além.
    m.caixa("Perimetro", "Chao", (0, -0.5, 0), (86, 1, 50), "piso")
    m.caixa("Perimetro", "MuroNorte", (0, 6, 24.5), (86, 12, 1), "muro")
    m.caixa("Perimetro", "MuroSul", (0, 6, -24.5), (86, 12, 1), "muro")
    m.caixa("Perimetro", "MuroLeste", (42.5, 6, 0), (1, 12, 48), "muro")
    m.caixa("Perimetro", "MuroOeste", (-42.5, 6, 0), (1, 12, 48), "muro")

    # --- Centro: plataforma de 2 m com parapeito de 1 m e duas aberturas,
    #     uma de frente para cada base, e o farol no meio.
    m.caixa("Centro", "Heliponto", (0, 1, 0), (12, 2, 12), "plataforma")
    m.caixa("Centro", "Farol", (0, 4, 0), (1.2, 4, 1.2), "farol", oculta=True)
    m.caixa("Centro", "ParapeitoNorte", (0, 2.5, 5.75), (12, 1, 0.5), "amarelo")
    m.caixa("Centro", "ParapeitoSul", (0, 2.5, -5.75), (12, 1, 0.5), "amarelo")
    for sx, tag in ((1, "Leste"), (-1, "Oeste")):
        for sz, tz in ((1, "Norte"), (-1, "Sul")):
            m.caixa("Centro", f"Parapeito{tag}{tz}", (sx * 5.75, 2.5, sz * 4), (0.5, 1, 4), "amarelo")
    m.rampa("Centro", "RampaHeliLeste", 13, 6, 2, 0, 4, "rampa")
    m.rampa("Centro", "RampaHeliOeste", -13, -6, 2, 0, 4, "rampa")
    m.luz("Centro", "LuzFarol", (0, 7, 0), (0.3, 0.85, 1.0), 2.0, 14)

    # --- Pontes: 28 m de vão a 5 m de altura, ligando os telhados das
    #     oficinas. Grade só do lado do muro; do lado da ala, nada — é
    #     por ali que se cai em cima de alguém.
    for sz, tz in ((1, "Norte"), (-1, "Sul")):
        m.caixa("Pontes", f"Ponte{tz}", (0, 4.75, sz * 18.5), (28, 0.5, 3), "rampa")
        m.caixa("Pontes", f"Ponte{tz}Grade", (0, 5.4, sz * 19.85), (28, 0.8, 0.3), "amarelo")

    # --- Cada lado. s = +1 é o lado B (laranja), s = −1 o lado A (azul).
    for s, lado in ((-1, "A"), (1, "B")):
        mat = "base_azul" if s < 0 else "base_laranja"
        g = f"Base{lado}"

        # Base: galpão de 10 × 21 m com teto a 5 m, aberto para o centro.
        m.caixa(g, "ParedeNorte", (s * 37, 2.5, 10), (10, 5, 1), mat)
        m.caixa(g, "ParedeSul", (s * 37, 2.5, -10), (10, 5, 1), mat)
        m.caixa(g, "Teto", (s * 37, 5.25, 0), (10, 0.5, 21), mat)
        m.caixa(g, "PilarNorte", (s * 32.5, 2.5, 5), (1, 5, 1), "aco")
        m.caixa(g, "PilarSul", (s * 32.5, 2.5, -5), (1, 5, 1), "aco")
        m.caixa(g, "Piso", (s * 37, 0.012, 0), (10, 0.02, 19), "piso_azul" if s < 0 else "piso_laranja", colide=False)

        # Caixotes na boca da base: cobertura para sair, e alto o bastante
        # (0,8 m) para o humano subir.
        P.caixote(m, g, "CaixoteNorte", (s * 29, 0.4, 4), (2, 0.8, 2))
        P.caixote(m, g, "CaixoteSul", (s * 29, 0.4, -4), (2, 0.8, 2))

        # Contêineres empilhados entre a base e as oficinas.
        for sz, tz in ((1, "Norte"), (-1, "Sul")):
            m.caixa(g, f"Conteiner{tz}", (s * 29, 1.1, sz * 15), (6, 2.2, 2.6), "conteiner_verde")
            m.caixa(g, f"Conteiner{tz}Alto", (s * 29, 3.3, sz * 15), (4, 2.2, 2.6), "conteiner_azul")
            P.caixote(m, g, f"Caixote{tz}Escada", (s * 29, 0.4, sz * 17.9), (1.6, 0.8, 1.6))

        cor = (0.45, 0.65, 1.0) if s < 0 else (1.0, 0.58, 0.28)
        m.luz(g, "Luz", (s * 37, 4.2, 0), cor, 1.4, 12)
        time = AZUL if s < 0 else LARANJA
        for x, z in ((39, -6), (39, -2), (39, 2), (39, 6), (35.5, -7), (35.5, 7)):
            m.spawn(time, s * x, z)

        # Oficinas, com mureta de cobertura no telhado e rampa vindo do
        # centro. Atrás de cada uma fica o beco de 3 m (Z de 21 a 24).
        L = f"Lado{lado}"
        for sz, tz in ((1, "Norte"), (-1, "Sul")):
            oficina(m, L, s, sz, tz)
            m.caixa(L, f"Bloco{tz}Mureta", (s * 20, 5.3, sz * 12.25), (12, 0.6, 0.5), "amarelo")
            m.rampa(L, f"Rampa{tz}", s * 4, s * 14, 5, sz * 14, 3, "rampa")
            m.caixa(L, f"PilarPonte{tz}", (s * 7, 2.25, sz * 18.5), (1, 4.5, 1), "aco")

        # Cobertura no meio da ala central e caixotes soltos.
        m.caixa(L, "ConteinerMeio", (s * 20, 1.3, 0), (2.6, 2.6, 6), "conteiner_verde")
        P.caixote(m, L, "CaixoteMeioNorte", (s * 10, 0.4, 8.5), (1.8, 0.8, 1.8))
        P.caixote(m, L, "CaixoteMeioSul", (s * 10, 0.4, -8.5), (1.8, 0.8, 1.8))

    # Rotas de ronda. O centro contorna o farol em vez de atravessá-lo: um
    # ponto dentro de sólido é um destino que a navegação nunca alcança.
    m.rota("centro", [(-28, 0.5, 0), (-16, 0.5, 0), (-3, 2.5, 2.5), (3, 2.5, -2.5), (16, 0.5, 0), (28, 0.5, 0)])
    m.rota("alaNorte", [(-28, 0.5, 8), (-14, 0.5, 10), (0, 0.5, 14), (14, 0.5, 10), (28, 0.5, 8)])
    m.rota("alaSul", [(-28, 0.5, -8), (-14, 0.5, -10), (0, 0.5, -14), (14, 0.5, -10), (28, 0.5, -8)])
    m.rota("telhadoNorte", [(-20, 5.5, 16.5), (-9, 5.5, 18.5), (9, 5.5, 18.5), (20, 5.5, 16.5)])
    m.rota("telhadoSul", [(-20, 5.5, -16.5), (-9, 5.5, -18.5), (9, 5.5, -18.5), (20, 5.5, -16.5)])
    m.rota("flancoNorte", [(-30, 0.5, 19.5), (-20, 0.5, 22.5), (-6, 0.5, 22), (6, 0.5, 22), (20, 0.5, 22.5), (30, 0.5, 19.5)])
    m.rota("flancoSul", [(-30, 0.5, -19.5), (-20, 0.5, -22.5), (-6, 0.5, -22), (6, 0.5, -22), (20, 0.5, -22.5), (30, 0.5, -19.5)])
    for sz, tz in ((1, "Norte"), (-1, "Sul")):
        m.rota(f"oficinas{tz}", [(-28, 0.5, sz * 9), (-20, 0.5, sz * 10.8), (-20, 0.5, sz * 14.4), (-17.2, 0.5, sz * 16.6), (-16, 0.5, sz * 18.6),
                                 (-12, 0.5, sz * 18.6), (0, 0.5, sz * 15), (12, 0.5, sz * 18.6), (16, 0.5, sz * 18.6), (17.2, 0.5, sz * 16.6),
                                 (20, 0.5, sz * 14.4), (20, 0.5, sz * 10.8), (28, 0.5, sz * 9)])

    # O céu é uma fotografia (Poly Haven, CC0); o sol do mapa fica onde está o da foto.
    m.ambiente = dict(ceu=dict(hdri="dia_claro", giro=178, energia=1.7, ambiente=0.8),
                      luz=(0.80, 0.86, 0.95), luz_energia=1.0, exposicao=1.0, brilho=0.5, contraste=1.06, saturacao=1.04,
                      neblina=dict(cor=(0.80, 0.86, 0.94), densidade=0.0015, ceu=0.05))
    m.sol = dict(yaw=-38, queda=33, cor=(1.0, 0.95, 0.86), energia=1.15, alcance=110)
    acabamento(m)
    return m


def _conteiner(m, g, nome, centro, tamanho):
    """Cantoneiras nos quatro cantos e as barras da porta numa ponta."""
    (cx, cy, cz), (sx, sy, sz) = centro, tamanho
    cantos = F.Malha()
    for dx in (-sx / 2, sx / 2):
        for dz in (-sz / 2, sz / 2):
            cantos.juntar(F.caixa(0.14, sy, 0.14, (dx, 0, dz)))
    m.forma(g, nome + "Cantos", cantos, (cx, cy, cz), "aco")
    barras = F.Malha()
    ao_longo_x = sx >= sz
    for d in (-0.9, -0.3, 0.3, 0.9):
        a, b = (0, -sy / 2 + 0.15, d), (0, sy / 2 - 0.15, d)
        if not ao_longo_x:
            a, b = (d, a[1], 0), (d, b[1], 0)
        barras.juntar(F.tubo([a, b], 0.022, 5))
    ponta = (cx + sx / 2 + 0.03, cy, cz) if ao_longo_x else (cx, cy, cz + sz / 2 + 0.03)
    m.forma(g, nome + "Barras", barras, ponta, "aco")


def acabamento(m):
    """Do pátio de caixa cinza à base aérea: galpões de chapa por cima do
    muro, portões de enrolar com número, oficinas de tijolo com porta e
    janela de aço, faixas pintadas no piso, o farol da plataforma,
    contêineres com cantoneira e os letreiros."""
    P.materiais(m)
    m.material("chapa_galpao", "metal_trapezio", (0.70, 0.74, 0.80), 2.4, metal=0.3)
    m.material("persiana", "persiana", (0.56, 0.6, 0.64), 1.6, metal=0.4, aspereza=0.6)
    m.material("faixa", None, (0.95, 0.76, 0.12), aspereza=0.7)
    m.material("vidro_galpao", None, (0.10, 0.14, 0.18), aspereza=0.1, metal=0.5)
    m.material("escuro", None, (0.03, 0.03, 0.035), aspereza=1.0)
    m.material("prato", None, (0.86, 0.88, 0.9), aspereza=0.4, metal=0.3)

    # --- Os galpões em volta: do muro de concreto (5 m) para cima é chapa,
    #     com uma fita de janelas e a cumeeira. Nada disto colide: o beco
    #     continua com a parede lisa de que o lobo precisa.
    for nome, eixo, fixo, comp in (("Norte", "x", 23.96, 84), ("Sul", "x", -23.96, 84), ("Leste", "z", 41.96, 48), ("Oeste", "z", -41.96, 48)):
        def peca(sufixo, y, alt, esp, mat, ao_longo=comp):
            tam = (ao_longo, alt, esp) if eixo == "x" else (esp, alt, ao_longo)
            centro = (0, y, fixo) if eixo == "x" else (fixo, y, 0)
            m.caixa("Galpoes", f"{nome}{sufixo}", centro, tam, mat, colide=False)
        peca("Chapa", 8.6, 6.8, 0.06, "chapa_galpao")
        peca("Rufo", 5.2, 0.16, 0.14, "aco")
        peca("Janelas", 9.6, 1.3, 0.09, "vidro_galpao")
        peca("Cumeeira", 12.06, 0.2, 0.3, "aco")
        for i in range(int(comp // 6) + 1):                     # as colunas de aço, só acima do muro
            d = -comp / 2 + 0.3 + i * (comp - 0.6) / int(comp // 6)
            centro = (d, 8.6, fixo) if eixo == "x" else (fixo, 8.6, d)
            m.caixa("Galpoes", f"{nome}Coluna{i}", centro, (0.24, 6.8, 0.2) if eixo == "x" else (0.2, 6.8, 0.24), "aco", colide=False)

    # Portões de enrolar no meio dos lados norte e sul, com o número em cima.
    for sz, n in ((1, "1"), (-1, "2")):
        z = sz * 23.97
        de_frente = 180 if sz > 0 else 0
        m.caixa("Galpoes", f"Portao{n}", (0, 3.4, z), (11, 6.8, 0.06), "persiana", colide=False)
        m.caixa("Galpoes", f"Portao{n}Caixa", (0, 7.0, z - sz * 0.12), (11.6, 0.5, 0.3), "aco", colide=False)
        for k, dx in enumerate((-5.65, 5.65)):
            m.caixa("Galpoes", f"Portao{n}Guia{k}", (dx, 3.4, z - sz * 0.02), (0.3, 6.8, 0.12), "faixa", colide=False)
        m.letreiro("Galpoes", f"Portao{n}Numero", f"hangar_n{n}", (0, 9.5, sz * 23.9), 2.2, de_frente)
        m.letreiro("Galpoes", f"Portao{n}Aviso", "hangar_aviso", (-8.6 * sz, 2.4, sz * 23.985), 2.6, de_frente)
        m.letreiro("Galpoes", f"Portao{n}Faixa", "faixa_perigo", (0, 0.35, sz * 23.93), 10.4, de_frente)
    for s, n in ((-1, "3"), (1, "4")):
        m.letreiro("Galpoes", f"Nome{n}", "hangar_nome", (s * 41.9, 9.4, 0), 9.0, 90 if s < 0 else -90)

    # --- A plataforma do centro: anel pintado, borda, o farol com a gaiola.
    m.letreiro("Centro", "Anel", "hangar_anel", (0, 2.012, 0), 9.6, 0, chao=True)
    for sz in (1, -1):
        m.caixa("Centro", f"BordaPlataforma{sz}", (0, 1.0, sz * 6.02), (12.04, 2.0, 0.05), "muro", colide=False)
        m.letreiro("Centro", f"FaixaPlataforma{sz}", "faixa_perigo", (0, 1.75, sz * 6.05), 11.6, 0 if sz > 0 else 180)
    m.forma("Centro", "FarolMastro", F.tubo([(0, 2, 0), (0, 5.2, 0)], 0.14, 10), (0, 0, 0), "aco")
    m.caixa("Centro", "FarolBase", (0, 2.2, 0), (1.2, 0.4, 1.2), "aco", colide=False)
    m.forma("Centro", "FarolLanterna", F.revolucao([(0, 5.2), (0.42, 5.2), (0.42, 5.9), (0.3, 6.0), (0, 6.0)], 14, suave=False), (0, 0, 0), "farol")
    m.forma("Centro", "FarolChapeu", F.revolucao([(0, 6.25), (0.56, 6.0), (0.56, 5.96), (0, 5.96)], 14, suave=False), (0, 0, 0), "aco")
    for i in range(4):
        a = math.radians(45 + i * 90)
        m.forma("Centro", f"FarolEscora{i}", F.tubo([(0.5 * math.cos(a), 2.4, 0.5 * math.sin(a)), (0.1 * math.cos(a), 4.6, 0.1 * math.sin(a))], 0.04, 6), (0, 0, 0), "aco")

    # --- Faixas no piso: a borda das alas e a linha de taxiamento até a plataforma.
    for sz in (1, -1):
        m.caixa("Piso", f"FaixaAla{sz}", (0, 0.011, sz * 11.2), (56, 0.012, 0.18), "faixa", colide=False)
    for s in (1, -1):
        m.caixa("Piso", f"FaixaBase{s}", (s * 31.9, 0.011, 0), (0.22, 0.012, 19), "faixa", colide=False)

    # --- Pontes: longarinas, mãos-francesas no pilar e o corrimão do lado do muro.
    for sz, tz in ((1, "Norte"), (-1, "Sul")):
        for k, dz in enumerate((-1.4, 1.4)):
            m.caixa("Pontes", f"Longarina{tz}{k}", (0, 4.36, sz * 18.5 + dz), (28, 0.3, 0.14), "aco", colide=False)
        for i in range(8):
            m.caixa("Pontes", f"Travessa{tz}{i}", (-12.25 + i * 3.5, 4.4, sz * 18.5), (0.12, 0.2, 2.9), "aco", colide=False)

    for s, lado in ((-1, "A"), (1, "B")):
        g = f"Base{lado}"
        # A letra do time num painel sobre a beira do teto, e o nome do galpão.
        m.caixa(g, "PainelLetra", (s * 32.2, 7.3, 0), (0.14, 3.3, 3.3), "aco", colide=False)
        for pz in (-1.2, 1.2):
            m.caixa(g, f"PainelPe{pz}", (s * 32.3, 5.6, pz), (0.12, 0.4, 0.12), "aco", colide=False)
        m.letreiro(g, "Letra", f"hangar_base_{lado.lower()}", (s * 32.2 - s * 0.08, 7.3, 0), 2.9, 90 if s < 0 else -90)
        for k, pz in enumerate((-6, 0, 6)):
            m.caixa(g, f"Lampada{k}", (s * 37, 4.94, pz), (2.4, 0.08, 0.3), "lampada", colide=False)
        m.caixa(g, "Rufo", (s * 32.02, 5.25, 0), (0.08, 0.56, 21.1), "aco", colide=False)
        for sz, tz in ((1, "Norte"), (-1, "Sul")):
            _conteiner(m, g, f"Conteiner{tz}", (s * 29, 1.1, sz * 15), (6, 2.2, 2.6))
            _conteiner(m, g, f"Conteiner{tz}Alto", (s * 29, 3.3, sz * 15), (4, 2.2, 2.6))

        L = f"Lado{lado}"
        _conteiner(m, L, "ConteinerMeio", (s * 20, 1.3, 0), (2.6, 2.6, 6))
        for sz, tz in ((1, "Norte"), (-1, "Sul")):
            de_frente = 180 if sz > 0 else 0
            # A oficina: marcos de aço nas portas e janelas, beiral, piso, letreiro.
            for i, (x, larg, y0) in enumerate(((20, 1.6, 0), (16.8, 1.4, 1.0), (23.2, 1.4, 1.0))):
                for k, dx in enumerate((-1, 1)):
                    m.caixa(L, f"Marco{tz}{i}{k}", (s * x + dx * (larg / 2 + 0.04), (y0 + 2.2) / 2, sz * 12.25), (0.08, 2.2 - y0, 0.6), "ferro", colide=False)
                m.caixa(L, f"MarcoVerga{tz}{i}", (s * x, 2.24, sz * 12.25), (larg + 0.16, 0.08, 0.6), "ferro", colide=False)
            for k, x in enumerate((14.25, 25.75)):
                for q, dz in enumerate((-0.84, 0.84)):
                    m.caixa(L, f"MarcoLado{tz}{k}{q}", (s * x, 1.1, sz * 18.6 + dz), (0.6, 2.2, 0.08), "ferro", colide=False)
                m.caixa(L, f"MarcoLadoVerga{tz}{k}", (s * x, 2.24, sz * 18.6), (0.6, 0.08, 1.76), "ferro", colide=False)
            m.caixa(L, f"Beiral{tz}", (s * 20, 5.03, sz * 16.5), (12.3, 0.08, 9.3), "aco", colide=False)
            m.caixa(L, f"PisoOficina{tz}", (s * 20, 0.012, sz * 16.5), (11, 0.02, 8), "rampa", colide=False)
            m.caixa(L, f"BancadaTampo{tz}", (s * 23, 0.92, sz * 15.9), (3.5, 0.04, 0.9), "prato", colide=False)
            m.letreiro(L, f"Letreiro{tz}", OFICINAS[(s, sz)], (s * 20, 3.2, sz * 11.985), 4.4, de_frente)
            m.letreiro(L, f"Fumar{tz}", "hangar_fumar", (s * 25.2, 2.7, sz * 11.985), 1.2, de_frente)
            # O que há lá dentro (objetos do Poly Haven).
            P.objeto(m, L, f"Bau{tz}", "bau_ferramenta", (s * 22.4, 0.94, sz * 15.9), 0, colide=False)
            P.objeto(m, L, f"CarroFerramenta{tz}", "carro_ferramenta", (s * 16.8, 0, sz * 13.3), 0)
            P.objeto(m, L, f"Estante{tz}", "estante_b", (s * 24.9, 0, sz * 20.1), 0)
            P.objeto(m, L, f"Compressor{tz}", "compressor", (s * 21.8, 0, sz * 19.9), 90)
            P.objeto(m, L, f"Galao{tz}1", "galao_verde", (s * 15.1, 0, sz * 20.2), 20, colide=False)
            P.objeto(m, L, f"Galao{tz}2", "galao_plastico", (s * 15.6, 0, sz * 20.1), -30, colide=False)
            P.objeto(m, L, f"Pneu{tz}", "pneu", (s * 19.4, 0, sz * 20.3), 0, colide=False)
            # A rampa: corrimão dos dois lados.
            for k, dz in enumerate((-1.45, 1.45)):
                P.corrimao(m, L, f"CorrimaoRampa{tz}{k}", [(s * 4.4, 0.1, sz * 14 + dz), (s * 13.8, 5.0, sz * 14 + dz)], "amarelo", 1.0, 0.03, 1.9)
