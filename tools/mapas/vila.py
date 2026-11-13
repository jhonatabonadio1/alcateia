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
import math
import random

from . import formas as F
from . import pecas as P
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
    m.cilindro("Praca", "Chafariz", (0, 0.45, 0), 3.0, 0.9, "pedra", oculta=True)
    m.cilindro("Praca", "Agua", (0, 0.74, 0), 2.6, 0.08, "agua", colide=False)
    m.cilindro("Praca", "Coluna", (0, 1.5, 0), 0.5, 3.0, "pedra", oculta=True)
    toldos = ("toldo_vermelho", "toldo_azul", "toldo_areia", "toldo_vermelho")
    for i, (sx, sz) in enumerate(((1, 1), (-1, 1), (-1, -1), (1, -1))):
        g = f"Banca{i + 1}"
        m.caixa("Praca", f"{g}Mesa", (sx * 7.5, 0.4, sz * 7.5), (2.6, 0.8, 1.3), "madeira")
        m.caixa("Praca", f"{g}Toldo", (sx * 7.5, 2.24, sz * 7.5), (3.4, 0.12, 2.6), toldos[i], oculta=True)
        for px, pz in ((-1.55, -1.15), (1.55, -1.15), (-1.55, 1.15), (1.55, 1.15)):
            m.caixa("Praca", f"{g}Poste{px}{pz}", (sx * 7.5 + px, 1.1, sz * 7.5 + pz), (0.12, 2.2, 0.12), "madeira_escura", colide=False)
    m.luz("Praca", "LuzChafariz", (0, 3.6, 0), (0.6, 0.9, 1.0), 1.0, 9)

    # --- Torres de sino, no meio dos muros norte e sul.
    for sz, t in ((1, "Norte"), (-1, "Sul")):
        m.caixa("Torres", f"Torre{t}", (0, 6.5, sz * 24), (4.4, 13, 4), "branco")

    for s, lado in ((-1, "A"), (1, "B")):
        for sz, t in ((1, "Norte"), (-1, "Sul")):
            g = f"Lado{lado}{t}"
            # Casa grande: terraço a 5 m com mureta para a praça, escada
            # vindo do meio, viela de 3 m atrás (até o muro).
            m.caixa(g, "CasaGrande", (s * 17, 2.5, sz * 17), (12, 5, 12), "creme" if sz > 0 else "branco")
            m.caixa(g, "Mureta", (s * 17, 5.3, sz * 11.25), (12, 0.6, 0.5), "terracota")
            m.caixa(g, "MuretaLado", (s * 11.25, 5.3, sz * 13.5), (0.5, 0.6, 4), "terracota")
            m.rampa(g, "Escada", s * 1.6, s * 11, 5, sz * 18.5, 2.6, "pedra", degraus=28)
            # Casa da ponta: viela de 3 m entre ela e a casa grande, e
            # outra entre ela e o muro da base.
            m.caixa(g, "CasaPonta", (s * 29.5, 2.5, sz * 19), (7, 5, 8), "terracota" if sz > 0 else "creme")
            m.caixa(g, "MuretaPonta", (s * 29.5, 5.3, sz * 15.25), (7, 0.6, 0.5), "branco")
            m.caixa(g, "Prancha", (s * 24.5, 4.9, sz * 19), (4.2, 0.2, 1.6), "madeira")
            m.caixa(g, "Arco", (s * 24.5, 3.6, sz * 15.3), (3, 0.9, 0.6), "branco", oculta=True)
            m.luz(g, "LuzViela", (s * 24.5, 2.6, sz * 17.5), (1.0, 0.72, 0.40), 1.6, 8)
            m.luz(g, "LuzBeco", (s * 17, 2.6, sz * 24.5), (1.0, 0.72, 0.40), 1.2, 9)
            # Cobertura solta.
            P.caixote(m, g, "Caixote", (s * 12.5, 0.4, sz * 9.6), (1.6, 0.8, 1.6))
            m.cilindro(g, "Barril1", (s * 22, 0.5, sz * 9.8), 0.45, 1.0, "madeira_escura", oculta=True)
            m.cilindro(g, "Barril2", (s * 22.9, 0.5, sz * 9.9), 0.45, 1.0, "madeira_escura", oculta=True)
            P.caixote(m, g, "CaixoteViela", (s * 34.4, 0.4, sz * 24), (1.4, 0.8, 1.4))

        g = f"Base{lado}"
        cor = (0.45, 0.65, 1.0) if s < 0 else (1.0, 0.58, 0.28)
        time_mat = "anil" if s < 0 else "ocre"
        # Alpendre no meio da ala: teto baixo sobre quatro pilares.
        m.caixa(g, "Alpendre", (s * 18, 3.3, 0), (7, 0.4, 5), "madeira")
        for px in (-3, 3):
            for pz in (-2, 2):
                m.caixa(g, f"Pilar{px}{pz}", (s * 18 + px, 1.55, pz), (0.6, 3.1, 0.6), "branco")
        m.caixa(g, "Carroca", (s * 18, 0.55, 0.3), (2.6, 1.1, 1.4), "madeira_escura", oculta=True)
        # A base: pátio com muretas de 0,8 m e pérgola de sombra.
        for sz, t in ((1, "Norte"), (-1, "Sul")):
            m.caixa(g, f"Mureta{t}", (s * 28, 0.4, sz * 7), (0.6, 0.8, 7), time_mat)
            m.caixa(g, f"Muro{t}", (s * 32, 2.0, sz * 13.5), (8, 4, 0.6), time_mat)
            m.caixa(g, f"Faixa{t}", (s * 35.9, 4.6, sz * 6), (0.1, 3.2, 1.6), "toldo_azul" if s < 0 else "toldo_vermelho", colide=False)
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
    m.sol = dict(yaw=38, queda=30, cor=(1.0, 0.84, 0.64), energia=1.2, alcance=110)
    acabamento(m)
    return m


def _folha_palmeira(comprimento=3.2, largura=0.7, trechos=7):
    """Uma folha: fita que sai do topo, sobe um pouco e cai, afinando."""
    m = F.Malha()
    linha = []
    for i in range(trechos + 1):
        t = i / trechos
        x = comprimento * t
        y = 0.9 * math.sin(t * math.pi * 0.62) - 1.5 * t * t
        w = largura * (0.35 + 0.65 * math.sin(math.pi * min(t * 1.25, 1.0))) * (1 - 0.8 * t ** 3) * 0.5
        linha.append((x, y, w))
    for (x0, y0, w0), (x1, y1, w1) in zip(linha[:-1], linha[1:]):
        # Dobrada no meio, em V raso: pega luz dos dois lados.
        for lado in (-1, 1):
            a, b = (x0, y0, 0), (x1, y1, 0)
            c, d = (x1, y1 - 0.12 * w1, lado * w1), (x0, y0 - 0.12 * w0, lado * w0)
            if lado > 0: m.quad(a, d, c, b)
            else: m.quad(a, b, c, d)
    return m


def _palmeira(m, grupo, nome, pos, altura, semente, colide=False):
    rnd = random.Random(semente)
    inclina = rnd.uniform(-0.8, 0.8), rnd.uniform(-0.8, 0.8)
    tronco = [(inclina[0] * (t ** 2), altura * t, inclina[1] * (t ** 2)) for t in (0, 0.2, 0.4, 0.6, 0.8, 1.0)]
    m.forma(grupo, nome, F.tubo(tronco, 0.2, 8), pos, "tronco")
    topo = (pos[0] + inclina[0], pos[1] + altura, pos[2] + inclina[1])
    folha = _folha_palmeira()
    copa = F.Malha()
    for i in range(11):
        copa.juntar(folha.rodar("z", rnd.uniform(-18, 28)), yaw=i * 360 / 11 + rnd.uniform(-12, 12))
    m.forma(grupo, nome + "Copa", copa, topo, "folha")
    if colide:
        m.cilindro(grupo, nome + "Colisao", (pos[0], pos[1] + altura / 2, pos[2]), 0.22, altura, "tronco", oculta=True)


def _pipa(m, grupo, nome, pos):
    """Barril de madeira com os aros de ferro."""
    perfil = [(0, 0), (0.36, 0), (0.42, 0.25), (0.45, 0.5), (0.42, 0.75), (0.36, 1.0), (0.3, 0.97), (0, 0.97)]
    m.forma(grupo, nome, F.revolucao(perfil, 16), pos, "madeira_escura")
    aros = F.Malha()
    for y, r in ((0.12, 0.395), (0.36, 0.44), (0.64, 0.44), (0.88, 0.395)):
        aros.juntar(F.revolucao([(r, y - 0.025), (r + 0.012, y - 0.025), (r + 0.012, y + 0.025), (r, y + 0.025)], 16, suave=False))
    m.forma(grupo, nome + "Aros", aros, pos, "ferro")


def _fachada(m, grupo, nome, face, meio, largura, altura, cor, rnd, portas=1, niveis=(2.5, 5.9), base=0.0):
    """Veste um trecho de parede como frente de casa: o reboco colorido, o
    rodapé, janelas em fiadas (umas acesas, umas com veneziana), porta e
    beiral. `meio` é o ponto do meio do trecho, no chão, encostado na
    parede."""
    r = P.Ref(m, grupo, meio, P.FACE[face])
    r.caixa(nome + "Reboco", (0, base + altura / 2, 0.05), (largura - 0.06, altura, 0.1), cor)
    r.caixa(nome + "Rodape", (0, base + 0.25, 0.085), (largura - 0.04, 0.5, 0.17), "pedra")
    r.caixa(nome + "Beiral", (0, base + altura + 0.07, 0.2), (largura + 0.1, 0.14, 0.5), "telha")
    vaos = max(int(largura // 3.2), 1)
    passo = largura / vaos
    porta_em = set(rnd.sample(range(vaos), min(portas, vaos)))
    for i in range(vaos):
        x = -largura / 2 + passo * (i + 0.5)
        for k, y in enumerate(niveis):
            if y + 0.8 > altura:
                continue
            if k == 0 and i in porta_em:
                P.porta(m, grupo, f"{nome}Porta{i}", r.p((x, base, 0.1)), r.yaw, 1.1, 2.15, "pedra_clara",
                        rnd.choice(("porta", "veneziana", "madeira_escura")), arco=rnd.random() < 0.6)
                continue
            aceso = rnd.random() < 0.3
            P.janela(m, grupo, f"{nome}J{i}{k}", r.p((x, base + y, 0.1)), r.yaw, 1.0, 1.3, "pedra_clara",
                     "vidro_aceso" if aceso else "vidro", grade=4 if k == 0 else 0,
                     persiana="veneziana" if k > 0 and rnd.random() < 0.7 else None)
    # As pontas das vigas de madeira, logo abaixo do beiral.
    vigas = F.Malha()
    n = max(int(largura // 0.9), 2)
    for i in range(n):
        vigas.juntar(F.caixa(0.14, 0.14, 0.34, (-largura / 2 + largura * (i + 0.5) / n, base + altura - 0.35, 0.25)))
    r.forma(nome + "Vigas", vigas, (0, 0, 0), "madeira_escura")


def acabamento(m):
    """Da caixa à vila: os muros viram frentes de casa, as casas ganham
    porta em arco, janela com veneziana, vigas e beiral; a praça tem
    chafariz torneado, bancas com lona e mercadoria, varal de luzes;
    palmeiras e torres para além do muro fecham o horizonte."""
    P.materiais(m)
    rnd = random.Random(7)
    m.material("pedra_clara", "rocha", (1.0, 0.96, 0.88), 1.2)
    m.material("telha", "tijolo", (0.82, 0.42, 0.28), 0.9, relevo=1.2)
    m.material("veneziana", "madeira", (0.30, 0.52, 0.52), 1.2)
    m.material("tronco", "madeira", (0.62, 0.52, 0.42), 0.6, relevo=1.4)
    m.material("folha", None, (0.20, 0.36, 0.14), aspereza=0.8, dupla=True)
    m.material("lona_vermelha", None, (0.72, 0.16, 0.12), aspereza=0.95, dupla=True)
    m.material("lona_azul", None, (0.16, 0.36, 0.62), aspereza=0.95, dupla=True)
    m.material("lona_areia", None, (0.90, 0.80, 0.55), aspereza=0.95, dupla=True)
    m.material("barro", None, (0.70, 0.40, 0.26), aspereza=0.85)
    m.material("fruta_laranja", None, (0.95, 0.55, 0.12), aspereza=0.6)
    m.material("fruta_verde", None, (0.35, 0.60, 0.18), aspereza=0.6)
    m.material("bulbo", None, (1, 0.9, 0.6), brilho=(1.0, 0.80, 0.45), energia=2.6)
    m.material("lajota", "pedra_calcada", (0.74, 0.50, 0.38), 2.0)
    m.material("rosa", "reboco", (0.90, 0.66, 0.60), 3.0)
    m.material("areia_reboco", "reboco", (0.84, 0.72, 0.52), 3.0)

    # --- Chafariz: bacia com borda, fuste e duas taças.
    bacia = [(0, 0), (3.0, 0), (3.0, 0.62), (3.1, 0.7), (3.1, 0.9), (2.62, 0.9), (2.62, 0.3), (0, 0.3)]
    m.forma("Praca", "Bacia", F.revolucao(bacia, 32, suave=False), (0, 0, 0), "pedra")
    fuste = [(0, 0.3), (0.62, 0.3), (0.62, 0.5), (0.42, 0.62), (0.34, 1.6), (0.46, 1.72), (1.25, 1.95), (1.3, 2.05),
             (0.3, 2.0), (0.24, 2.7), (0.34, 2.8), (0.72, 2.92), (0.74, 3.0), (0.16, 2.98), (0.12, 3.4), (0, 3.45)]
    m.forma("Praca", "Fuste", F.revolucao(fuste, 20), (0, 0, 0), "pedra")
    m.cilindro("Praca", "AguaTaca", (0, 2.03, 0), 1.2, 0.03, "agua", colide=False)
    for i in range(8):                 # o meio-fio em volta da praça calçada
        pass
    m.forma("Praca", "MeioFio", F.extrusao([(-11.15, 0), (-11.0, 0), (-11.0, 0.07), (-11.15, 0.07)], 22.3), (0, 0, 0), "pedra_clara")
    m.forma("Praca", "MeioFio2", F.extrusao([(11.0, 0), (11.15, 0), (11.15, 0.07), (11.0, 0.07)], 22.3), (0, 0, 0), "pedra_clara")
    m.forma("Praca", "MeioFio3", F.extrusao([(-11.15, 0), (-11.0, 0), (-11.0, 0.07), (-11.15, 0.07)], 22.3), (0, 0, 0), "pedra_clara", yaw=90)
    m.forma("Praca", "MeioFio4", F.extrusao([(11.0, 0), (11.15, 0), (11.15, 0.07), (11.0, 0.07)], 22.3), (0, 0, 0), "pedra_clara", yaw=90)

    # --- Bancas: lona com barriga em três panos, mercadoria na mesa.
    lonas = (("lona_vermelha", "lona_areia"), ("lona_azul", "lona_areia"), ("lona_areia", "lona_vermelha"), ("lona_vermelha", "lona_areia"))
    n = 8
    for i, (sx, sz) in enumerate(((1, 1), (-1, 1), (-1, -1), (1, -1))):
        g, cx, cz = f"Banca{i + 1}", sx * 7.5, sz * 7.5
        cima = [(-1.4 + 2.8 * k / n, 0.16 * math.cos(math.pi * (k / n - 0.5)) ** 2 + 0.05) for k in range(n + 1)]
        perfil = cima + [(x, y - 0.03) for x, y in reversed(cima)]
        for k in range(5):
            m.forma("Praca", f"{g}Lona{k}", F.extrusao(perfil, 0.72, suave=True), (cx - 1.44 + 0.72 * k, 2.28, cz), lonas[i][k % 2])
            m.caixa("Praca", f"{g}Saia{k}", (cx - 1.44 + 0.72 * k, 2.2, cz + 1.39), (0.7, 0.22, 0.02), lonas[i][k % 2], colide=False)
        # Caixas de fruta, potes e sacos.
        for k, (dx, mat) in enumerate(((-0.85, "fruta_laranja"), (0.0, "fruta_verde"), (0.85, "fruta_laranja"))):
            m.caixa("Praca", f"{g}Bandeja{k}", (cx + dx, 0.86, cz), (0.7, 0.12, 0.9), "madeira_escura", colide=False)
            monte = F.Malha()
            for q in range(9):
                monte.juntar(F.pedra(0.085, q + 1, 1.0, 1, 0.05, False), ((q % 3 - 1) * 0.19, 0.0, (q // 3 - 1) * 0.24))
            m.forma("Praca", f"{g}Fruta{k}", monte, (cx + dx, 0.9, cz), mat)
        pote = [(0, 0), (0.16, 0), (0.26, 0.2), (0.24, 0.42), (0.12, 0.55), (0.15, 0.62), (0.1, 0.62), (0, 0.5)]
        for k, (dx, dz) in enumerate(((1.75, 0.5), (1.95, -0.2), (-1.8, -0.4))):
            m.forma("Praca", f"{g}Pote{k}", F.revolucao(pote, 12), (cx + dx, 0, cz + dz), "barro")

    # --- Varal de luzes do chafariz às bancas.
    for i, (sx, sz) in enumerate(((1, 1), (-1, 1), (-1, -1), (1, -1))):
        a, b = (0, 3.4, 0), (sx * 7.5, 2.5, sz * 7.5)
        P.cabo(m, "Praca", f"Varal{i}", a, b, 0.5, "ferro", 0.012, 8)
        bulbos = F.Malha()
        for k in range(1, 8):
            t = k / 8
            bulbos.juntar(F.pedra(0.07, 3, 1.0, 1, 0.0, False),
                          (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t - 0.5 * 4 * t * (1 - t) - 0.12, a[2] + (b[2] - a[2]) * t))
        m.forma("Praca", f"Bulbos{i}", bulbos, (0, 0, 0), "bulbo")
    m.luz("Praca", "LuzVaral", (0, 3.0, 0), (1.0, 0.78, 0.45), 1.3, 14)

    # --- Torres: cornija, quatro arcos de sineira, sino e cúpula.
    for sz, t in ((1, "Norte"), (-1, "Sul")):
        z = sz * 24
        m.caixa("Torres", f"Cornija{t}", (0, 13.08, z), (4.9, 0.16, 4.5), "pedra_clara", colide=False)
        for k, (yaw, dx, dz, larg) in enumerate(((0, 0, 1.85, 4.4), (0, 0, -1.85, 4.4), (90, 2.05, 0, 4.0), (90, -2.05, 0, 4.0))):
            m.forma("Torres", f"Sineira{t}{k}", F.arco_parede(larg, 2.6, 0.3, larg * 0.5, 2.0), (dx, 13.15, z + dz), "branco", yaw=yaw)
        m.caixa("Torres", f"Laje{t}", (0, 15.83, z), (4.9, 0.16, 4.5), "pedra_clara", colide=False)
        cupula = [(2.1, 0)] + [(2.1 * math.cos(math.radians(a)), 2.3 * math.sin(math.radians(a))) for a in range(10, 90, 10)] + [(0.12, 2.3), (0.08, 2.9), (0, 2.95)]
        m.forma("Torres", f"Cupula{t}", F.revolucao(cupula, 24), (0, 15.9, z), "telha")
        sino = [(0, 0.75), (0.1, 0.75), (0.2, 0.6), (0.26, 0.2), (0.4, 0.0), (0.36, 0.0), (0, 0.5)]
        m.forma("Torres", f"Sino{t}", F.revolucao(sino, 14), (0, 14.3, z), "sino")
        P.porta(m, "Torres", f"PortaTorre{t}", (0, 0, sz * 22), "z-" if sz > 0 else "z+", 1.6, 2.5, "pedra_clara", "madeira_escura", arco=True)
        for k, y in enumerate((5.0, 8.4)):
            P.janela(m, "Torres", f"Seteira{t}{k}", (0, y, sz * 22), "z-" if sz > 0 else "z+", 0.6, 1.5, "pedra_clara", "vidro", peitoril=False)
        m.caixa("Torres", f"Rodape{t}", (0, 0.3, z), (4.56, 0.6, 4.16), "pedra", colide=False)

    # --- As casas.
    for s, lado in ((-1, "A"), (1, "B")):
        for sz, t in ((1, "Norte"), (-1, "Sul")):
            g = f"Lado{lado}{t}"
            cor_grande = "creme" if sz > 0 else "branco"
            cor_ponta = "terracota" if sz > 0 else "creme"
            para_praca = "z-" if sz > 0 else "z+"
            para_fundo = "z+" if sz > 0 else "z-"
            para_meio = "x+" if s < 0 else "x-"
            para_base = "x-" if s < 0 else "x+"
            # Casa grande (12 × 12, 5 m): frente para a praça e para o meio, vielas atrás e do lado.
            _fachada(m, g, "FrentePraca", para_praca, (s * 17, 0, sz * 11), 12, 5, cor_grande, rnd, portas=1, niveis=(2.2, 3.5))
            _fachada(m, g, "FrenteMeio", para_meio, (s * 11, 0, sz * 17), 12, 5, cor_grande, rnd, portas=0, niveis=(2.3, 9))
            _fachada(m, g, "FrenteFundo", para_fundo, (s * 17, 0, sz * 23), 12, 5, cor_grande, rnd, portas=1, niveis=(2.4, 9))
            _fachada(m, g, "FrenteViela", para_base, (s * 23, 0, sz * 17), 12, 5, cor_grande, rnd, portas=0, niveis=(2.4, 9))
            # Casa da ponta (7 × 8).
            _fachada(m, g, "PontaPraca", para_praca, (s * 29.5, 0, sz * 15), 7, 5, cor_ponta, rnd, portas=1, niveis=(2.2, 3.5))
            _fachada(m, g, "PontaViela", para_meio, (s * 26, 0, sz * 19), 8, 5, cor_ponta, rnd, portas=0, niveis=(2.4, 9))
            _fachada(m, g, "PontaFundo", para_fundo, (s * 29.5, 0, sz * 23), 7, 5, cor_ponta, rnd, portas=0, niveis=(2.4, 9))
            _fachada(m, g, "PontaBase", para_base, (s * 33, 0, sz * 19), 8, 5, cor_ponta, rnd, portas=1, niveis=(2.4, 9))
            # Terraço de lajota (o reboco claro no sol estoura) e as capas das muretas.
            m.caixa(g, "Lajota", (s * 17, 5.012, sz * 17), (11.9, 0.02, 11.9), "lajota", colide=False)
            m.caixa(g, "LajotaPonta", (s * 29.5, 5.012, sz * 19), (6.9, 0.02, 7.9), "lajota", colide=False)
            m.caixa(g, "MuretaCapa", (s * 17, 5.63, sz * 11.25), (12.1, 0.07, 0.62), "pedra_clara", colide=False)
            m.caixa(g, "MuretaLadoCapa", (s * 11.25, 5.63, sz * 13.5), (0.62, 0.07, 4.1), "pedra_clara", colide=False)
            m.caixa(g, "MuretaPontaCapa", (s * 29.5, 5.63, sz * 15.25), (7.1, 0.07, 0.62), "pedra_clara", colide=False)
            vaso = [(0, 0), (0.14, 0), (0.2, 0.3), (0.23, 0.34), (0.17, 0.34), (0, 0.3)]
            for k, (vx, vz) in enumerate(((s * 22.4, sz * 22.4), (s * 11.9, sz * 22.4), (s * 32.4, sz * 22.4))):
                m.forma(g, f"Vaso{k}", F.revolucao(vaso, 10, suave=False), (vx, 5.0, vz), "barro")
                tufo = F.Malha()
                for q in range(6):
                    tufo.juntar(_folha_palmeira(0.6, 0.16, 4), yaw=q * 60 + 15)
                m.forma(g, f"Planta{k}", tufo, (vx, 5.42, vz), "folha")
            # O arco da viela (a colisão é a peça de cima) e a lanterna.
            m.forma(g, "ArcoViela", F.arco_parede(3.0, 4.05, 0.6, 2.5, 3.2), (s * 24.5, 0, sz * 15.3), "branco")
            m.caixa(g, "ArcoCapa", (s * 24.5, 4.09, sz * 15.3), (3.1, 0.08, 0.72), "telha", colide=False)
            P.arandela(m, g, "LanternaViela", (s * 23.0, 2.9, sz * 16.6), "x+" if s > 0 else "x-", energia=0)
            P.arandela(m, g, "LanternaBeco", (s * 17, 2.9, sz * 23.0), para_fundo, energia=0)
            P.arandela(m, g, "LanternaPraca", (s * 15.2, 2.8, sz * 11.0), para_praca, energia=0)
            # Escada de pedra: corrimão de madeira nos dois lados.
            for k, dz in enumerate((-1.2, 1.2)):
                P.corrimao(m, g, f"CorrimaoEscada{k}", [(s * 1.9, 0, sz * 18.5 + dz), (s * 10.8, 5.0, sz * 18.5 + dz)], "madeira_escura", 0.95, 0.04, 1.6)
            # Pipas de vinho (a colisão é o cilindro de cima).
            _pipa(m, g, "Pipa1", (s * 22, 0, sz * 9.8))
            _pipa(m, g, "Pipa2", (s * 22.9, 0, sz * 9.9))
            # Tábuas da ponte entre os terraços e as cordas.
            for k, dz in enumerate((-0.82, 0.82)):
                P.cabo(m, g, f"CordaPonte{k}", (s * 22.4, 5.9, sz * 19 + dz), (s * 26.6, 5.9, sz * 19 + dz), 0.25, "madeira_escura", 0.02, 6)

        g = f"Base{lado}"
        # Carroça: caixa de tábuas, duas rodas, varais.
        cx = s * 18
        m.caixa(g, "CarrocaLeito", (cx, 0.72, 0.3), (2.5, 0.1, 1.3), "madeira", colide=False)
        for k, dz in enumerate((-0.62, 0.62)):
            m.caixa(g, f"CarrocaLado{k}", (cx, 0.95, 0.3 + dz), (2.5, 0.38, 0.05), "madeira_escura", colide=False)
            roda = F.toro(0.5, 0.045, 20, 6).rodar("x", 90)
            raios = F.Malha()
            for q in range(6):
                raios.juntar(F.caixa(0.98, 0.05, 0.04).rodar("z", q * 30))
            m.forma(g, f"CarrocaRoda{k}", roda.juntar(raios), (cx - 0.2, 0.55, 0.3 + dz * 1.22), "madeira_escura")
        for k, dx in enumerate((-1.25, 1.25)):
            m.caixa(g, f"CarrocaTesta{k}", (cx + dx, 0.95, 0.3), (0.05, 0.38, 1.25), "madeira_escura", colide=False)
        for k, dz in enumerate((-0.45, 0.45)):
            m.forma(g, f"CarrocaVaral{k}", F.tubo([(cx + 1.2, 0.7, 0.3 + dz), (cx + 2.6, 0.28, 0.3 + dz)], 0.035, 6), (0, 0, 0), "madeira_escura")
        for k in range(3):
            m.forma(g, f"CarrocaSaco{k}", F.pedra(0.34, k + 2, 0.55, 1, 0.1), (cx - 0.7 + k * 0.6, 0.77, 0.3 + (k % 2) * 0.2 - 0.1), "saco")
        # Alpendre: vigas por baixo, telha por cima.
        for k, dx in enumerate((-3, 0, 3)):
            m.caixa(g, f"AlpendreViga{k}", (s * 18 + dx, 3.02, 0), (0.18, 0.2, 5.3), "madeira_escura", colide=False)
        m.caixa(g, "AlpendreTelha", (s * 18, 3.54, 0), (7.3, 0.1, 5.3), "telha", colide=False)
        # Pérgola: longarinas e caibros, com sombra de ripa no chão.
        for k, dx in enumerate((-2.6, 0, 2.6)):
            m.caixa(g, f"PergolaLongarina{k}", (s * 32.8 + dx, 3.38, 0), (0.16, 0.22, 15.2), "madeira_escura", colide=False)
        caibros = F.Malha()
        for k in range(22):
            caibros.juntar(F.caixa(5.9, 0.12, 0.09, (0, 0, -7.35 + k * 0.7)))
        m.forma(g, "PergolaCaibros", caibros, (s * 32.8, 3.55, 0), "madeira_escura")
        # Sacos de areia na frente da base e duas palmeiras no pátio.
        P.sacos(m, g, "SacosNorte", (s * 27.2, 0, 3.2), 2.6, 3, yaw=90)
        P.sacos(m, g, "SacosSul", (s * 27.2, 0, -3.2), 2.6, 3, yaw=90)
        _palmeira(m, g, "PalmeiraN", (s * 34.6, 0, 11.6), 7.5, 11 + s, colide=True)
        _palmeira(m, g, "PalmeiraS", (s * 34.6, 0, -11.6), 8.2, 21 + s, colide=True)

    # --- Os muros viram frentes de casa: trechos de cores e alturas diferentes.
    cores = ("creme", "branco", "terracota", "rosa", "areia_reboco", "ocre", "anil")
    def rua(nome, face, fixo, de, ate, pular=()):
        x = de
        k = 0
        while x < ate - 0.5:
            larg = min(rnd.choice((6.0, 7.0, 8.5, 10.0)), ate - x)
            if ate - (x + larg) < 4.0:
                larg = ate - x
            meio = x + larg / 2
            if not any(a < meio < b for a, b in pular):
                alt = 9.0 + rnd.choice((0.0, 0.6, 1.2, 1.9))
                pos = (meio, 0, fixo) if face[0] == "z" else (fixo, 0, meio)
                _fachada(m, "Rua", f"{nome}{k}", face, pos, larg, alt, rnd.choice(cores), rnd, portas=1, niveis=(2.5, 5.6, 8.0))
            x += larg
            k += 1
    rua("Norte", "z-", 26.0, -36, 36, pular=((-2.4, 2.4),))
    rua("Sul", "z+", -26.0, -36, 36, pular=((-2.4, 2.4),))
    rua("Leste", "x-", 36.0, -26, 26, pular=((-9.5, 9.5),))
    rua("Oeste", "x+", -36.0, -26, 26, pular=((-9.5, 9.5),))

    # --- Para além do muro: palmeiras, um minarete e os telhados de trás.
    for k, (x, z, alt) in enumerate(((-30, 31, 13.5), (-12, 30, 12.5), (14, 31, 14), (31, 30, 12.8), (-31, -31, 13),
                                     (-10, -30, 13.8), (12, -31, 12.6), (30, -30, 14.2), (41, 14, 13.5), (41, -15, 12.8),
                                     (-41, 15, 13), (-41, -14, 14))):
        _palmeira(m, "Horizonte", f"Palmeira{k}", (x, 0, z), alt, 100 + k)
    for k, (x, z, lx, lz, alt, cor) in enumerate(((-22, 33, 9, 6, 12.5, "creme"), (22, 34, 11, 7, 13.5, "terracota"), (0, 36, 8, 6, 12, "branco"),
                                                  (-20, -34, 10, 7, 13, "rosa"), (21, -33, 9, 6, 12.2, "areia_reboco"),
                                                  (43, 0, 6, 12, 13, "creme"), (-43, 0, 6, 12, 12.6, "branco"))):
        m.caixa("Horizonte", f"Predio{k}", (x, alt / 2, z), (lx, alt, lz), cor, colide=False)
        m.caixa("Horizonte", f"PredioBeiral{k}", (x, alt + 0.08, z), (lx + 0.6, 0.16, lz + 0.6), "telha", colide=False)
    minarete = [(1.3, 0), (1.3, 15), (1.7, 15.2), (1.7, 15.5), (1.0, 15.6), (1.0, 18), (1.25, 18.2)] + \
               [(1.25 * math.cos(math.radians(a)), 18.2 + 1.5 * math.sin(math.radians(a))) for a in range(15, 90, 15)] + [(0.08, 19.7), (0.05, 21), (0, 21)]
    m.forma("Horizonte", "Minarete", F.revolucao(minarete, 16), (34, 0, 37), "branco")
    m.forma("Horizonte", "Minarete2", F.revolucao(minarete, 16), (-35, 0, -36), "creme")
