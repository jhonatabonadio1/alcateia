"""
Vila — mapa médio (72 × 52 m), a céu aberto, fim de tarde.

Uma vila de deserto: praça com chafariz e feira no meio, casas de
reboco com terraço, vielas estreitas, torres de sino nas pontas.

As quatro perguntas do teste de mapa:
  1. Caminho que só o lobo faz: as vielas de 3 m entre as casas e atrás
     delas (duas paredes de 5 m até o terraço), e os toldos da feira
     (2,3 m, a partir da banca).
  2. Onde o humano vê o lobo chegando: os terraços, com mureta, de
     frente para a praça. Sobe-se pela escada que sai do meio — exposto.
  3. Corredor que o humano deve evitar: as vielas.
  4. Cair em cima de quem não olha: as pranchas entre os terraços, por
     cima das vielas, e a beira do terraço sobre a ala.
"""
from .kit import Mapa, AZUL, LARANJA


def montar():
    m = Mapa("vila", "Vila", 72, 52)

    m.material("areia", "areia", (1.0, 0.96, 0.9), 4.0)
    m.material("calcada", "pedra_calcada", (1.0, 0.95, 0.88), 3.0)
    m.material("muro", "tijolo", (1.0, 0.92, 0.85), 2.4)
    m.material("creme", "reboco", (0.96, 0.87, 0.70), 3.0)
    m.material("terracota", "reboco", (0.86, 0.56, 0.40), 3.0)
    m.material("branco", "reboco", (0.86, 0.83, 0.77), 3.0)
    m.material("anil", "reboco", (0.42, 0.58, 0.86), 3.0)
    m.material("ocre", "reboco", (0.96, 0.62, 0.30), 3.0)
    m.material("pedra", "rocha", (0.95, 0.90, 0.82), 2.0)
    m.material("madeira", "madeira", (1, 1, 1), 1.6)
    m.material("madeira_escura", "madeira", (0.55, 0.45, 0.38), 1.6)
    m.material("toldo_vermelho", None, (0.72, 0.16, 0.12), aspereza=0.95)
    m.material("toldo_azul", None, (0.16, 0.36, 0.62), aspereza=0.95)
    m.material("toldo_areia", None, (0.90, 0.80, 0.55), aspereza=0.95)
    m.material("agua", None, (0.20, 0.52, 0.62), aspereza=0.08, metal=0.2, brilho=(0.10, 0.32, 0.40), energia=0.5)
    m.material("lanterna", None, (1, 0.8, 0.5), brilho=(1.0, 0.72, 0.36), energia=3.0)
    m.material("sino", None, (0.75, 0.58, 0.22), aspereza=0.35, metal=0.9)

    # --- Chão, muro de tijolo de 9 m e o calçamento da praça.
    m.caixa("Casca", "Chao", (0, -0.5, 0), (74, 1, 54), "areia")
    m.caixa("Casca", "MuroNorte", (0, 4.5, 26.5), (74, 9, 1), "muro")
    m.caixa("Casca", "MuroSul", (0, 4.5, -26.5), (74, 9, 1), "muro")
    m.caixa("Casca", "MuroLeste", (36.5, 4.5, 0), (1, 9, 52), "muro")
    m.caixa("Casca", "MuroOeste", (-36.5, 4.5, 0), (1, 9, 52), "muro")
    m.caixa("Casca", "Calcada", (0, 0.012, 0), (22, 0.02, 22), "calcada", colide=False)
    m.caixa("Casca", "CalcadaLeste", (25, 0.012, 0), (6, 0.02, 8), "calcada", colide=False)
    m.caixa("Casca", "CalcadaOeste", (-25, 0.012, 0), (6, 0.02, 8), "calcada", colide=False)

    # --- Praça: chafariz no centro e quatro bancas de feira com toldo.
    m.cilindro("Praca", "Chafariz", (0, 0.45, 0), 3.0, 0.9, "pedra")
    m.cilindro("Praca", "Agua", (0, 0.88, 0), 2.6, 0.08, "agua", colide=False)
    m.cilindro("Praca", "Coluna", (0, 1.5, 0), 0.5, 3.0, "pedra")
    m.cilindro("Praca", "Taca", (0, 2.9, 0), 1.1, 0.25, "pedra", colide=False)
    toldos = ("toldo_vermelho", "toldo_azul", "toldo_areia", "toldo_vermelho")
    for i, (sx, sz) in enumerate(((1, 1), (-1, 1), (-1, -1), (1, -1))):
        g = f"Banca{i + 1}"
        m.caixa("Praca", f"{g}Mesa", (sx * 7.5, 0.4, sz * 7.5), (2.6, 0.8, 1.3), "madeira")
        m.caixa("Praca", f"{g}Toldo", (sx * 7.5, 2.24, sz * 7.5), (3.4, 0.12, 2.6), toldos[i])
        for px, pz in ((-1.55, -1.15), (1.55, -1.15), (-1.55, 1.15), (1.55, 1.15)):
            m.caixa("Praca", f"{g}Poste{px}{pz}", (sx * 7.5 + px, 1.1, sz * 7.5 + pz), (0.12, 2.2, 0.12), "madeira_escura", colide=False)
    m.luz("Praca", "LuzChafariz", (0, 3.6, 0), (0.6, 0.9, 1.0), 1.0, 9)

    # --- Torres de sino, no meio dos muros norte e sul.
    for sz, t in ((1, "Norte"), (-1, "Sul")):
        m.caixa("Torres", f"Torre{t}", (0, 6.5, sz * 24), (4.4, 13, 4), "branco")
        m.caixa("Torres", f"Cupula{t}", (0, 13.6, sz * 24), (5.2, 1.2, 4.8), "terracota", colide=False)
        m.caixa("Torres", f"Sino{t}", (0, 11.2, sz * 21.85), (1.0, 1.2, 0.3), "sino", colide=False)
        m.caixa("Torres", f"Porta{t}", (0, 1.3, sz * 21.95), (1.6, 2.6, 0.12), "madeira_escura", colide=False)

    for s, lado in ((-1, "A"), (1, "B")):
        for sz, t in ((1, "Norte"), (-1, "Sul")):
            g = f"Lado{lado}{t}"
            # Casa grande: terraço a 5 m com mureta para a praça, escada
            # vindo do meio, viela de 3 m atrás (até o muro).
            m.caixa(g, "CasaGrande", (s * 17, 2.5, sz * 17), (12, 5, 12), "creme" if sz > 0 else "branco")
            m.caixa(g, "Mureta", (s * 17, 5.3, sz * 11.25), (12, 0.6, 0.5), "terracota")
            m.caixa(g, "MuretaLado", (s * 11.25, 5.3, sz * 13.5), (0.5, 0.6, 4), "terracota")
            m.rampa(g, "Escada", s * 1.6, s * 11, 5, sz * 18.5, 2.6, "pedra")
            m.caixa(g, "Porta", (s * 17, 1.2, sz * 10.95), (1.4, 2.4, 0.12), "madeira_escura", colide=False)
            m.caixa(g, "Janela1", (s * 14, 3.4, sz * 10.95), (1.0, 1.0, 0.12), "anil", colide=False)
            m.caixa(g, "Janela2", (s * 20, 3.4, sz * 10.95), (1.0, 1.0, 0.12), "anil", colide=False)
            # Casa da ponta: viela de 3 m entre ela e a casa grande, e
            # outra entre ela e o muro da base.
            m.caixa(g, "CasaPonta", (s * 29.5, 2.5, sz * 19), (7, 5, 8), "terracota" if sz > 0 else "creme")
            m.caixa(g, "MuretaPonta", (s * 29.5, 5.3, sz * 15.25), (7, 0.6, 0.5), "branco")
            m.caixa(g, "Prancha", (s * 24.5, 4.9, sz * 19), (4.2, 0.2, 1.6), "madeira")
            m.caixa(g, "Arco", (s * 24.5, 3.6, sz * 15.3), (3, 0.9, 0.6), "branco")
            m.caixa(g, "Lanterna", (s * 24.5, 3.0, sz * 15.3), (0.3, 0.3, 0.3), "lanterna", colide=False)
            m.luz(g, "LuzViela", (s * 24.5, 2.6, sz * 17.5), (1.0, 0.72, 0.40), 1.6, 8)
            m.luz(g, "LuzBeco", (s * 17, 2.6, sz * 24.5), (1.0, 0.72, 0.40), 1.2, 9)
            # Cobertura solta.
            m.caixa(g, "Caixote", (s * 12.5, 0.4, sz * 9.6), (1.6, 0.8, 1.6), "madeira")
            m.cilindro(g, "Barril1", (s * 22, 0.5, sz * 9.8), 0.45, 1.0, "madeira_escura")
            m.cilindro(g, "Barril2", (s * 22.9, 0.5, sz * 9.9), 0.45, 1.0, "madeira_escura")
            m.caixa(g, "CaixoteViela", (s * 34.4, 0.4, sz * 24), (1.4, 0.8, 1.4), "madeira")

        g = f"Base{lado}"
        cor = (0.45, 0.65, 1.0) if s < 0 else (1.0, 0.58, 0.28)
        time_mat = "anil" if s < 0 else "ocre"
        # Alpendre no meio da ala: teto baixo sobre quatro pilares.
        m.caixa(g, "Alpendre", (s * 18, 3.3, 0), (7, 0.4, 5), "madeira")
        for px in (-3, 3):
            for pz in (-2, 2):
                m.caixa(g, f"Pilar{px}{pz}", (s * 18 + px, 1.55, pz), (0.6, 3.1, 0.6), "branco")
        m.caixa(g, "Carroca", (s * 18, 0.55, 0.3), (2.6, 1.1, 1.4), "madeira_escura")
        # A base: pátio com muretas de 0,8 m e pérgola de sombra.
        for sz, t in ((1, "Norte"), (-1, "Sul")):
            m.caixa(g, f"Mureta{t}", (s * 28, 0.4, sz * 7), (0.6, 0.8, 7), time_mat)
            m.caixa(g, f"Muro{t}", (s * 32, 2.0, sz * 13.5), (8, 4, 0.6), time_mat)
            m.caixa(g, f"Faixa{t}", (s * 35.9, 4.6, sz * 6), (0.1, 3.2, 1.6), "toldo_azul" if s < 0 else "toldo_vermelho", colide=False)
        m.caixa(g, "Pergola", (s * 32.8, 3.4, 0), (5.6, 0.25, 15), "madeira_escura", colide=False)
        for pz in (-7, 0, 7):
            m.caixa(g, f"PostePergola{pz}", (s * 30.2, 1.7, pz), (0.3, 3.4, 0.3), "madeira_escura")
        m.placa(g, "Letra", (s * 35.9, 6.6, 0), lado, 460, cor, 90 if s < 0 else -90)
        m.luz(g, "Luz", (s * 32.5, 2.8, 0), cor, 1.6, 12)
        time = AZUL if s < 0 else LARANJA
        for z in (-7.5, -2.5, 2.5, 7.5):
            m.spawn(time, s * 34, z)
        for z in (-5, -1.8, 1.8, 5):
            m.spawn(time, s * 31.5, z)

    m.rota("praca", [(-27, 0.5, 0), (-13, 0.5, 3), (-5, 0.5, 5), (5, 0.5, -5), (13, 0.5, -3), (27, 0.5, 0)])
    m.rota("alaNorte", [(-27, 0.5, 9), (-14, 0.5, 9), (0, 0.5, 9.5), (14, 0.5, 9), (27, 0.5, 9)])
    m.rota("alaSul", [(-27, 0.5, -9), (-14, 0.5, -9), (0, 0.5, -9.5), (14, 0.5, -9), (27, 0.5, -9)])
    m.rota("terracoNorte", [(-17, 5.5, 17), (-6, 2.5, 18.5), (0, 0.5, 15), (6, 2.5, 18.5), (17, 5.5, 17)])
    m.rota("terracoSul", [(-17, 5.5, -17), (-6, 2.5, -18.5), (0, 0.5, -15), (6, 2.5, -18.5), (17, 5.5, -17)])
    m.rota("becoNorte", [(-30, 0.5, 24.5), (-17, 0.5, 24.5), (-6, 0.5, 22), (0, 0.5, 20.5), (6, 0.5, 22), (17, 0.5, 24.5), (30, 0.5, 24.5)])
    m.rota("becoSul", [(-30, 0.5, -24.5), (-17, 0.5, -24.5), (-6, 0.5, -22), (0, 0.5, -20.5), (6, 0.5, -22), (17, 0.5, -24.5), (30, 0.5, -24.5)])
    m.rota("vielaA", [(-24.5, 0.5, -24.5), (-24.5, 0.5, -13), (-26, 0.5, 0), (-24.5, 0.5, 13), (-24.5, 0.5, 24.5)])
    m.rota("vielaB", [(24.5, 0.5, -24.5), (24.5, 0.5, -13), (26, 0.5, 0), (24.5, 0.5, 13), (24.5, 0.5, 24.5)])

    m.ambiente = dict(ceu=dict(topo=(0.24, 0.42, 0.74), horizonte=(0.98, 0.76, 0.52), chao=(0.45, 0.36, 0.28), energia=1.0),
                      luz=(0.85, 0.74, 0.66), luz_energia=0.55, exposicao=1.0, brilho=0.5, contraste=1.08, saturacao=1.12,
                      neblina=dict(cor=(0.98, 0.80, 0.60), densidade=0.0035, ceu=0.4))
    m.sol = dict(yaw=38, queda=30, cor=(1.0, 0.84, 0.64), energia=1.35, alcance=110)
    return m
