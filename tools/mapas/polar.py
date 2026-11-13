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
from .kit import Mapa, AZUL, LARANJA


def montar():
    m = Mapa("polar", "Polar", 110, 70)

    m.material("neve", "neve", (0.80, 0.83, 0.88), 5.0, relevo=1.6)
    m.material("gelo", "rocha", (0.70, 0.80, 0.94), 5.0, aspereza=0.7)
    m.material("concreto", "concreto", (0.70, 0.72, 0.75), 4.0)
    m.material("garagem", "metal_ondulado", (0.62, 0.66, 0.72), 2.0, metal=0.25)
    m.material("modulo_laranja", "metal_ondulado", (0.92, 0.42, 0.14), 2.0, metal=0.25)
    m.material("modulo_branco", "metal_ondulado", (0.74, 0.77, 0.82), 2.0, metal=0.25)
    m.material("modulo_vermelho", "metal_ondulado", (0.72, 0.16, 0.14), 2.0, metal=0.25)
    m.material("base_azul", "metal_ondulado", (0.24, 0.44, 0.86), 2.0, metal=0.25)
    m.material("base_laranja", "metal_ondulado", (0.95, 0.52, 0.18), 2.0, metal=0.25)
    m.material("chapa", "chapa_xadrez", (0.60, 0.63, 0.68), 1.0, metal=0.3)
    m.material("grade", None, (0.95, 0.74, 0.16), aspereza=0.5, metal=0.3)
    m.material("tanque", "metal_pintado", (0.90, 0.92, 0.94), 3.0, metal=0.6, aspereza=0.8)
    m.material("cano", "ferrugem", (1, 1, 1), 1.5, metal=0.25)
    m.material("veiculo", "metal_pintado", (0.90, 0.48, 0.10), 2.0, metal=0.25)
    m.material("esteira", "asfalto", (1, 1, 1), 1.0)
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
    from .kit import giro_y
    blocos = ((-40, 33.2, 9, 7, 3, 12), (-12, 33.6, 14, 9, 3, -8), (22, 33.4, 10, 6, 3, 10), (44, 33.0, 8, 8, 4, -14))
    for i, (x, z, w, h, d, yaw) in enumerate(blocos, 1):
        m.caixa("Casca", f"BlocoGeloN{i}", (x, h / 2, z), (w, h, d), "gelo", rot=giro_y(yaw))
        m.caixa("Casca", f"BlocoGeloS{i}", (-x, h / 2, -z), (w, h, d), "gelo", rot=giro_y(yaw))

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
        m.caixa(g, f"Trator{t}", (s * 5, 1.1, -s * 4.6), (5, 2.2, 2.6), "veiculo")
        m.caixa(g, f"Trator{t}Cabine", (s * 6.2, 1.5, -s * 4.6), (1.8, 1.0, 2.7), "vidro", colide=False)
        m.caixa(g, f"Trator{t}Esteira", (s * 5, 0.3, -s * 4.6), (5.4, 0.6, 3.0), "esteira", colide=False)
        m.caixa(g, f"Caixote{t}", (s * 1.2, 0.4, -s * 4.6), (1.4, 0.8, 1.4), "madeira")
        m.caixa(g, f"Bancada{t}", (s * 9, 0.4, s * 6.6), (4, 0.8, 1.2), "aco")
        m.luz(g, f"Luz{t}", (s * 6, 5.2, 0), (1.0, 0.86, 0.66), 2.2, 14)
        m.caixa(g, f"Lampada{t}", (s * 6, 5.9, 0), (2.4, 0.12, 0.4), "lampada", colide=False)
    m.cilindro(g, "Antena", (-8, 7.6, 3), 0.25, 2.4, "aco", colide=False)
    m.cilindro(g, "Prato", (-8, 8.9, 3), 1.4, 0.2, "tanque", colide=False)

    # --- Tanques ao norte, torre de rádio ao sul.
    for sx, t in ((1, "Leste"), (-1, "Oeste")):
        m.cilindro("Tanques", f"Tanque{t}", (sx * 6, 3, 25), 3.2, 6, "tanque")
        m.caixa("Tanques", f"Cano{t}", (sx * 14, 0.55, 22), (10, 0.5, 0.5), "cano")
        m.caixa("Tanques", f"Suporte{t}", (sx * 14, 0.15, 22), (0.6, 0.3, 0.9), "aco", colide=False)
    m.caixa("Tanques", "CanoMeio", (0, 0.55, 19.2), (0.5, 0.5, 5), "cano")
    m.luz("Tanques", "Luz", (0, 6.5, 22), (1.0, 0.66, 0.30), 3.0, 18)
    m.caixa("Torre", "Base", (0, 0.4, -26), (5, 0.8, 5), "concreto")
    m.caixa("Torre", "Mastro", (0, 10, -26), (0.9, 18.4, 0.9), "aco")
    m.caixa("Torre", "Travessa1", (0, 6, -26), (3.2, 0.2, 0.2), "aco", colide=False)
    m.caixa("Torre", "Travessa2", (0, 12, -26), (2.2, 0.2, 0.2), "aco", colide=False)
    m.caixa("Torre", "Farol", (0, 19.4, -26), (0.5, 0.5, 0.5), "farol", colide=False)
    m.luz("Torre", "LuzFarol", (0, 18.5, -26), (1.0, 0.2, 0.15), 2.0, 14)
    for sx, t in ((1, "Leste"), (-1, "Oeste")):
        m.caixa("Torre", f"Gerador{t}", (sx * 8, 1.1, -22), (3.4, 2.2, 2.2), "modulo_vermelho")
        m.caixa("Torre", f"Caixote{t}", (sx * 5.4, 0.4, -21.4), (1.4, 0.8, 1.4), "madeira")

    for s, lado in ((-1, "A"), (1, "B")):
        for sz, t in ((1, "Norte"), (-1, "Sul")):
            L = f"Alojamento{lado}{t}"
            # Duas fileiras de dois andares com 3 m de beco entre elas.
            m.caixa(L, "Fileira1", (s * 28, 2.5, sz * 18), (16, 5, 5), "modulo_laranja" if sz > 0 else "modulo_branco")
            m.caixa(L, "Fileira2", (s * 28, 2.5, sz * 26), (16, 5, 5), "modulo_branco" if sz > 0 else "modulo_laranja")
            m.caixa(L, "Friso1", (s * 28, 2.5, sz * 15.45), (16, 0.25, 0.1), "aco", colide=False)
            m.caixa(L, "Ponte", (s * 30, 4.9, sz * 22), (2, 0.2, 3.6), "chapa")
            m.parapeito(L, "Mureta", (s * 28, 5.5, sz * 15.75), 10)
            m.rampa(L, "Rampa", s * 46, s * 36, 5, sz * 18, 3, "chapa")
            m.luz(L, "LuzBeco", (s * 28, 3.4, sz * 22), (1.0, 0.66, 0.30), 1.6, 10)
            for i, x in enumerate((22, 28, 34), 1):
                m.caixa(L, f"Janela{i}", (s * x, 1.6, sz * 15.44), (1.4, 0.8, 0.1), "lampada", colide=False)
            # Contêiner e caixote entre os alojamentos e a base.
            m.caixa(L, "Conteiner", (s * 40, 1.1, sz * 9), (6, 2.2, 2.6), "modulo_vermelho")
            m.caixa(L, "Caixote", (s * 40, 0.4, sz * 11.3), (1.6, 0.8, 1.6), "madeira")
            # Montes de neve dura nas alas.
            m.caixa(L, "Monte", (s * 17, 0.5, sz * 12.5), (4.5, 1.0, 2.4), "gelo", rot=giro_y(18 * s * sz))
            m.caixa(L, "CaixoteAla", (s * 30, 0.4, sz * 6), (1.6, 0.8, 1.6), "madeira")

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
        m.caixa(g, "CaixoteNorte", (s * 43, 0.4, 4), (2, 0.8, 2), "madeira")
        m.caixa(g, "CaixoteSul", (s * 43, 0.4, -4), (2, 0.8, 2), "madeira")
        m.placa(g, "Letra", (s * 44.8, 7.6, 0), lado, 520, cor, 90 if s < 0 else -90)
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
        m.rota(f"beco{lado}Norte", [(s * 17, 0.5, 22), (s * 28, 0.5, 22), (s * 39, 0.5, 22)])
        m.rota(f"beco{lado}Sul", [(s * 17, 0.5, -22), (s * 28, 0.5, -22), (s * 39, 0.5, -22)])
        m.rota(f"tetos{lado}", [(s * 45, 0.5, 18), (s * 40, 2.5, 18), (s * 30, 5.5, 18), (s * 21.5, 5.5, 10),
                                (s * 21.5, 5.5, -10), (s * 30, 5.5, -18), (s * 40, 2.5, -18), (s * 45, 0.5, -18)])
    m.rota("tetoGaragem", [(-12, 0.5, 9.9), (-4, 4.0, 9.9), (3, 6.9, 9.5), (0, 6.9, 0), (-3, 6.9, -9.5), (4, 4.0, -9.9), (12, 0.5, -9.9)])

    m.ambiente = dict(ceu=dict(topo=(0.08, 0.14, 0.30), horizonte=(0.62, 0.66, 0.78), chao=(0.50, 0.55, 0.62), energia=0.9),
                      luz=(0.52, 0.62, 0.86), luz_energia=0.45, exposicao=0.85, brilho=0.8, contraste=1.05, saturacao=1.0,
                      neblina=dict(cor=(0.62, 0.70, 0.82), densidade=0.006, ceu=0.5))
    m.sol = dict(yaw=-42, queda=22, cor=(1.0, 0.82, 0.70), energia=0.8, alcance=140)
    return m
