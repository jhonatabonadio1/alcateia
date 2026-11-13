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
    m.caixa("Trem", "Teto", (0, 2.1, 0), (18, 0.2, 5.6), "vagao")
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
            m.caixa(g, f"Pilar{i}", (x, 4.5, sz * 7.6), (0.8, 9, 0.8), "concreto")
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
            m.caixa(g, f"Lampada{i}", (x, 8.9, sz * 5.2), (2.4, 0.12, 0.3), "lampada", colide=False)
            m.luz(g, f"Luz{i}", (x, 7.6, sz * 5.2), (1.0, 0.95, 0.84), 2.2, 13)
        for i, x in enumerate((-9, 0, 9), 1):
            m.luz(g, f"LuzGaleria{i}", (x, 4.1, sz * 9.4), (1.0, 0.9, 0.72), 1.0, 7)
            m.luz(g, f"LuzServico{i}", (x, 3.6, sz * 13.4), (0.55, 1.0, 0.7), 0.9, 8)

    # --- Saguões (as bases): escada para a galeria, catracas, bilheteria.
    for s, lado, cor in ((-1, "A", (0.45, 0.65, 1.0)), (1, "B", (1.0, 0.58, 0.28))):
        g = f"Base{lado}"
        for sz, t in ((1, "Norte"), (-1, "Sul")):
            m.rampa(g, f"Escada{t}", s * 23.4, s * 14, 5, sz * 9.5, 2.6, "chapa")
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
    return m
