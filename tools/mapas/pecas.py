"""
Peças prontas: o acabamento que faz um bloco virar um lugar.

Cada função põe no mapa um conjunto de caixas e formas — janela com
moldura e peitoril, porta, barril, caixote ripado, barreira de concreto,
poste de luz, cano, pedra, toldo. Quase tudo é enfeite (sem colisão); o
que serve de cobertura leva uma colisão simples por baixo (caixa ou
cilindro ocultos), para o tiro e o corpo baterem onde o olho vê.

`face` diz para onde a peça olha: "z+", "x+", "z-" ou "x-".
"""
import math

from . import formas as F
from .kit import giro_y

FACE = {"z+": 0, "x+": 90, "z-": 180, "x-": 270}


class Ref:
    """Um ponto de apoio: `base` no mundo e o giro em Y. Posições locais:
    x ao longo da parede, y para cima, z para fora dela."""

    def __init__(self, m, grupo, base, yaw):
        self.m, self.g, self.base, self.yaw = m, grupo, base, yaw
        a = math.radians(yaw)
        self.c, self.s = math.cos(a), math.sin(a)

    def p(self, l):
        return (self.base[0] + l[0] * self.c + l[2] * self.s, self.base[1] + l[1],
                self.base[2] - l[0] * self.s + l[2] * self.c)

    def caixa(self, nome, centro, tam, mat, colide=False, oculta=False):
        self.m.caixa(self.g, nome, self.p(centro), tam, mat, colide, giro_y(self.yaw) if self.yaw else None, oculta=oculta)

    def forma(self, nome, malha, pos, mat, yaw=0.0, colisao=None):
        self.m.forma(self.g, nome, malha, self.p(pos), mat, self.yaw + yaw, colisao)


def _ref(m, grupo, pos, face):
    return Ref(m, grupo, pos, FACE[face] if isinstance(face, str) else face)


# ------------------------------------------------------------- arquitetura

def janela(m, grupo, nome, pos, face, largura=1.2, altura=1.4, moldura="moldura", vidro="vidro",
           grade=0, persiana=None, peitoril=True, fundo=0.07):
    """Janela aplicada na parede: `pos` é o centro, encostado nela. São
    até quatro nós (vidro, moldura, grade, persianas), cada um uma malha."""
    r = _ref(m, grupo, pos, face)
    e = 0.09
    r.caixa(nome + "Vidro", (0, 0, 0.02), (largura, altura, 0.04), vidro)
    q = F.Malha()
    for sx in (-1, 1):
        q.juntar(F.caixa(e, altura + 2 * e, fundo, (sx * (largura / 2 + e / 2), 0, fundo / 2)))
    q.juntar(F.caixa(largura, e, fundo, (0, altura / 2 + e / 2, fundo / 2)))
    q.juntar(F.caixa(largura, e, fundo, (0, -altura / 2 - e / 2, fundo / 2)))
    if peitoril:
        q.juntar(F.caixa(largura + 2 * e + 0.12, 0.06, fundo + 0.12, (0, -altura / 2 - e - 0.03, fundo / 2 + 0.05)))
    # Pinázios: a cruz que divide o vidro.
    q.juntar(F.caixa(0.04, altura, 0.02, (0, 0, 0.045)))
    q.juntar(F.caixa(largura, 0.04, 0.02, (0, 0, 0.045)))
    r.forma(nome + "Moldura", q, (0, 0, 0), moldura)
    if grade:
        q = F.Malha()
        for i in range(grade):
            q.juntar(F.caixa(0.025, altura + 0.1, 0.025, (-largura / 2 + largura * (i + 0.5) / grade, 0, fundo + 0.03)))
        for y in (-altura * 0.3, altura * 0.3):
            q.juntar(F.caixa(largura + 0.1, 0.025, 0.025, (0, y, fundo + 0.03)))
        r.forma(nome + "Grade", q, (0, 0, 0), "ferro")
    if persiana:
        q = F.Malha()
        for sx in (-1, 1):
            cx = sx * (largura / 2 + e + largura / 4 + 0.02)
            q.juntar(F.caixa(largura / 2, altura, 0.035, (cx, 0, 0.025)))
            for k in range(7):          # as palhetas
                q.juntar(F.caixa(largura / 2 - 0.1, 0.035, 0.02, (cx, -altura / 2 + altura * (k + 0.5) / 7, 0.05)))
        r.forma(nome + "Persiana", q, (0, 0, 0), persiana)


def porta(m, grupo, nome, pos, face, largura=1.0, altura=2.1, moldura="moldura", folha="porta", arco=False):
    """Porta fechada aplicada na parede: `pos` é o meio da soleira."""
    r = _ref(m, grupo, pos, face)
    e = 0.11
    q = F.caixa(largura, altura, 0.06, (0, altura / 2, 0.03))
    for k in range(1, 4):               # as almofadas da folha
        q.juntar(F.caixa(largura * 0.7, altura * 0.2, 0.02, (0, altura * (0.05 + 0.3 * k - 0.1), 0.065)))
    if arco:
        q.juntar(F.extrusao([(largura / 2 * math.cos(math.pi * i / 10), largura / 2 * math.sin(math.pi * i / 10))
                             for i in range(11)], 0.05).mover((0, altura, 0.03)))
    r.forma(nome + "Folha", q, (0, 0, 0), folha)
    q = F.Malha()
    for sx in (-1, 1):
        q.juntar(F.caixa(e, altura + (0 if arco else e), 0.12, (sx * (largura / 2 + e / 2), (altura + (0 if arco else e)) / 2, 0.06)))
    if arco:
        q.juntar(F.arco_parede(largura + 2 * e, largura / 2 + e + 0.05, 0.12, largura, largura / 2).mover((0, altura, 0.06)))
    else:
        q.juntar(F.caixa(largura, e, 0.12, (0, altura + e / 2, 0.06)))
    q.juntar(F.caixa(largura + 2 * e + 0.1, 0.06, 0.3, (0, 0.03, 0.12)))
    r.forma(nome + "Moldura", q, (0, 0, 0), moldura)
    r.caixa(nome + "Macaneta", (largura / 2 - 0.12, altura * 0.48, 0.09), (0.04, 0.16, 0.05), "ferro")


def beiral(m, grupo, nome, centro_topo, tamanho, mat, aba=0.22, espessura=0.14):
    """Laje que sobra para fora do topo de um bloco: `centro_topo` é o
    meio da face de cima, `tamanho` (x, z) do bloco."""
    m.caixa(grupo, nome, (centro_topo[0], centro_topo[1] + espessura / 2 - 0.02, centro_topo[2]),
            (tamanho[0] + 2 * aba, espessura, tamanho[1] + 2 * aba), mat, colide=False)


def platibanda(m, grupo, nome, centro_topo, tamanho, mat, altura=0.5, espessura=0.2, capa=None, vaos=()):
    """Mureta em volta de um terraço, com a capa por cima. `vaos`: lados
    sem mureta ("x+", "z-"...) — onde chega escada ou rampa."""
    x, y, z = centro_topo
    sx, sz = tamanho
    lados = {"z+": ((x, z + sz / 2 - espessura / 2), (sx, espessura)), "z-": ((x, z - sz / 2 + espessura / 2), (sx, espessura)),
             "x+": ((x + sx / 2 - espessura / 2, z), (espessura, sz)), "x-": ((x - sx / 2 + espessura / 2, z), (espessura, sz))}
    for lado, ((cx, cz), (lx, lz)) in lados.items():
        if lado in vaos:
            continue
        m.caixa(grupo, f"{nome}{lado[0].upper()}{'P' if lado[1] == '+' else 'N'}", (cx, y + altura / 2, cz), (lx, altura, lz), mat)
        if capa:
            m.caixa(grupo, f"{nome}Capa{lado[0].upper()}{'P' if lado[1] == '+' else 'N'}", (cx, y + altura + 0.04, cz),
                    (lx + 0.1, 0.08, lz + 0.1), capa, colide=False)


def coluna(m, grupo, nome, base, altura, raio, mat, colide=True, lados=18):
    """Coluna redonda com base e capitel."""
    r = raio
    perfil = [(r * 1.35, 0), (r * 1.35, 0.12), (r * 1.12, 0.2), (r, 0.3), (r, altura - 0.34), (r * 1.14, altura - 0.24),
              (r * 1.4, altura - 0.12), (r * 1.4, altura)]
    m.forma(grupo, nome, F.revolucao(perfil, lados, suave=False), base, mat)
    if colide:
        m.cilindro(grupo, nome + "Colisao", (base[0], base[1] + altura / 2, base[2]), r * 1.1, altura, mat, oculta=True)


def tubulacao(m, grupo, nome, pontos, raio, mat, flange=None, cada=3.0):
    """Cano pelo caminho, com um anel (flange) a cada tanto nos trechos retos."""
    m.forma(grupo, nome, F.tubo(pontos, raio, 10), (0, 0, 0), mat)
    if not flange:
        return
    k = 0
    for a, b in zip(pontos[:-1], pontos[1:]):
        d = math.dist(a, b)
        n = int(d // cada)
        # O anel é a mesma forma em todos; só muda o eixo do trecho.
        eixo = max(range(3), key=lambda j: abs(b[j] - a[j]))
        ponta = [0.0, 0.0, 0.0]
        ponta[eixo] = 0.06
        anel = F.tubo([(0, 0, 0), tuple(ponta)], raio * 1.35, 10)
        for i in range(1, n + 1):
            t = i / (n + 1)
            m.forma(grupo, f"{nome}Flange{k}", anel, tuple(a[j] + (b[j] - a[j]) * t for j in range(3)), flange)
            k += 1


def cabo(m, grupo, nome, a, b, flecha, mat, raio=0.012, trechos=10):
    """Cabo pendurado entre dois pontos, com a barriga de `flecha` metros."""
    pts = []
    for i in range(trechos + 1):
        t = i / trechos
        pts.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t - flecha * 4 * t * (1 - t), a[2] + (b[2] - a[2]) * t))
    m.forma(grupo, nome, F.tubo(pts, raio, 5, tampas=False), (0, 0, 0), mat)


def corrimao(m, grupo, nome, pontos, mat, altura=1.0, raio=0.03, cada=1.4):
    """Corrimão de tubo: uma barra no alto, outra no meio e os montantes.
    `pontos` são os pés (no piso)."""
    for k, h in (("Alto", altura), ("Meio", altura * 0.5)):
        m.forma(grupo, nome + k, F.tubo([(p[0], p[1] + h, p[2]) for p in pontos], raio, 6), (0, 0, 0), mat)
    i = 0
    for a, b in zip(pontos[:-1], pontos[1:]):
        d = math.dist(a, b)
        n = max(int(d // cada), 1)
        for j in range(n + 1):
            t = j / n
            p = tuple(a[q] + (b[q] - a[q]) * t for q in range(3))
            m.forma(grupo, f"{nome}Pe{i}", F.tubo([(0, 0, 0), (0, altura, 0)], raio, 6), p, mat)
            i += 1


# --------------------------------------------------------------- cobertura

def barril(m, grupo, nome, pos, mat="barril", aro="ferro", raio=0.29, altura=0.88, deitado=False):
    """Tambor de 200 litros, com os dois frisos."""
    r, h = raio, altura
    perfil = [(0, 0), (r * 0.94, 0), (r, 0.03), (r, h * 0.3), (r * 1.035, h * 0.33), (r, h * 0.36), (r, h * 0.64),
              (r * 1.035, h * 0.67), (r, h * 0.7), (r, h - 0.03), (r * 0.94, h), (r * 0.9, h - 0.02), (0, h - 0.02)]
    m.forma(grupo, nome, F.revolucao(perfil, 18, suave=False), pos, mat)
    m.cilindro(grupo, nome + "Colisao", (pos[0], pos[1] + h / 2, pos[2]), r, h, mat, oculta=True)


def caixote(m, grupo, nome, centro, tamanho, mat="madeira", ripa="madeira_escura", colide=True):
    """Caixote de madeira com as cantoneiras e uma ripa em cada face."""
    sx, sy, sz = tamanho
    m.forma(grupo, nome, F.caixa_chanfrada(sx - 0.04, sy - 0.02, sz - 0.04, 0.015), centro, mat)
    if colide:
        m.caixa(grupo, nome + "Colisao", centro, tamanho, mat, oculta=True)
    b, t = 0.09, 0.025
    k = 0
    for ax in range(3):                 # as doze arestas, quatro por eixo
        dims = [sx, sy, sz]
        outros = [i for i in range(3) if i != ax]
        for s1 in (-1, 1):
            for s2 in (-1, 1):
                c = list(centro)
                c[outros[0]] += s1 * (dims[outros[0]] / 2 - b / 2)
                c[outros[1]] += s2 * (dims[outros[1]] / 2 - b / 2)
                tam = [b + t, b + t, b + t]
                tam[ax] = dims[ax] + t
                m.caixa(grupo, f"{nome}Ripa{k}", tuple(c), tuple(tam), ripa, colide=False)
                k += 1


def barreira(m, grupo, nome, pos, comprimento=2.4, yaw=0.0, mat="concreto"):
    """Barreira de concreto de estrada (perfil New Jersey), 0,82 m."""
    perfil = [(-0.3, 0), (0.3, 0), (0.3, 0.08), (0.17, 0.3), (0.09, 0.82), (-0.09, 0.82), (-0.17, 0.3), (-0.3, 0.08)]
    m.forma(grupo, nome, F.extrusao(perfil, comprimento), pos, mat, yaw=yaw + 90)
    r = Ref(m, grupo, pos, yaw)
    r.caixa(nome + "Colisao", (0, 0.41, 0), (comprimento, 0.82, 0.44), mat, colide=True, oculta=True)


def sacos(m, grupo, nome, pos, comprimento, fiadas=3, yaw=0.0, mat="saco"):
    """Muro de sacos de areia: fiadas desencontradas."""
    saco = F.pedra(0.34, 5, achatar=0.42, sub=1, rugosidade=0.08).escala(1.0, 1.0, 0.62)
    r = Ref(m, grupo, pos, yaw)
    n = max(int(comprimento / 0.62), 1)
    for f_ in range(fiadas):
        desloca = 0.31 if f_ % 2 else 0.0
        for i in range(n - (1 if f_ % 2 else 0)):
            x = -comprimento / 2 + 0.31 + i * 0.62 + desloca
            r.forma(f"{nome}F{f_}S{i}", saco, (x, f_ * 0.2, 0), mat, yaw=(i * 37 + f_ * 11) % 9 - 4)
    r.caixa(nome + "Colisao", (0, fiadas * 0.1 + 0.02, 0), (comprimento, fiadas * 0.2 + 0.04, 0.46), mat, colide=True, oculta=True)


def pedra(m, grupo, nome, pos, raio, semente=1, mat="rocha", colide=True, achatar=0.7, yaw=0.0):
    m.forma(grupo, nome, F.pedra(raio, semente, achatar), pos, mat, yaw=yaw, colisao="convexa" if colide else None)


def pneus(m, grupo, nome, pos, quantos=3, mat="borracha"):
    """Pilha de pneus."""
    for i in range(quantos):
        m.forma(grupo, f"{nome}{i}", F.toro(0.24, 0.11, 16, 8), (pos[0] + (i % 2) * 0.03, pos[1] + 0.11 + i * 0.21, pos[2]), mat)
    m.cilindro(grupo, nome + "Colisao", (pos[0], pos[1] + quantos * 0.105, pos[2]), 0.35, quantos * 0.21, mat, oculta=True)


def palete(m, grupo, nome, pos, yaw=0.0, mat="madeira"):
    """Palete de carga, 1,2 × 1,0 m."""
    r = Ref(m, grupo, pos, yaw)
    for i in range(5):
        r.caixa(f"{nome}T{i}", (-0.5 + i * 0.25, 0.132, 0), (0.14, 0.022, 1.0), mat)
    for i, z in enumerate((-0.44, 0, 0.44)):
        r.caixa(f"{nome}L{i}", (0, 0.06, z), (1.2, 0.1, 0.1), mat)


# -------------------------------------------------------------------- luz

def poste(m, grupo, nome, pos, altura=5.0, yaw=0.0, mat="ferro", lampada="lampada", cor=(1.0, 0.9, 0.7),
          energia=2.0, alcance=12.0, braco=1.2, colide=True, sombra=False):
    """Poste de rua: fuste que afina, braço curvo e a luminária."""
    fuste = [(0.11, 0), (0.11, 0.5), (0.075, 0.6), (0.055, altura)]
    m.forma(grupo, nome, F.revolucao(fuste, 10, suave=False), pos, mat)
    r = Ref(m, grupo, pos, yaw)
    curva = [(0, altura - 0.05, 0)] + [(braco * (1 - math.cos(math.pi / 2 * t)), altura + 0.35 * math.sin(math.pi / 2 * t), 0)
                                       for t in (0.25, 0.5, 0.75, 1.0)]
    r.forma(nome + "Braco", F.tubo(curva, 0.035, 8), (0, 0, 0), mat)
    r.caixa(nome + "Luminaria", (braco + 0.18, altura + 0.33, 0), (0.55, 0.1, 0.22), mat)
    r.caixa(nome + "Lampada", (braco + 0.18, altura + 0.27, 0), (0.42, 0.03, 0.16), lampada)
    m.luz(grupo, nome + "Luz", r.p((braco + 0.18, altura - 0.2, 0)), cor, energia, alcance, sombra=sombra)
    if colide:
        m.cilindro(grupo, nome + "Colisao", (pos[0], pos[1] + altura / 2, pos[2]), 0.11, altura, mat, oculta=True)


def arandela(m, grupo, nome, pos, face, cor=(1.0, 0.85, 0.6), energia=1.0, alcance=7.0, mat="ferro", lampada="lampada"):
    """Luminária de parede com grade."""
    r = _ref(m, grupo, pos, face)
    r.caixa(nome + "Base", (0, 0, 0.03), (0.16, 0.22, 0.06), mat)
    r.caixa(nome + "Lampada", (0, -0.02, 0.12), (0.11, 0.16, 0.11), lampada)
    r.caixa(nome + "Aba", (0, 0.11, 0.12), (0.2, 0.03, 0.2), mat)
    if energia > 0:
        m.luz(grupo, nome + "Luz", r.p((0, -0.1, 0.45)), cor, energia, alcance)


# ------------------------------------------------------------------- pano

def toldo(m, grupo, nome, pos, face, largura=3.0, avanco=1.6, queda=0.5, mat="lona", haste="ferro", listras=None):
    """Toldo de lona preso na parede: `pos` é o meio da barra de cima."""
    r = _ref(m, grupo, pos, face)
    # A lona faz barriga: perfil curvo, esticado na largura.
    n = 8
    cima = [(avanco * i / n, -queda * (i / n) - 0.09 * math.sin(math.pi * i / n)) for i in range(n + 1)]
    perfil = cima + [(x, y - 0.025) for x, y in reversed(cima)]
    faixas = listras or [mat]
    passo = largura / len(faixas)
    for i, cor in enumerate(faixas):
        r.forma(f"{nome}Lona{i}", F.extrusao(perfil, passo, suave=True), (-largura / 2 + passo * (i + 0.5), 0, 0), cor, yaw=-90)
    r.caixa(nome + "Barra", (0, -queda - 0.04, avanco), (largura + 0.06, 0.04, 0.04), haste)
    for sx, lado in ((-1, "E"), (1, "D")):
        r.forma(nome + "Haste" + lado, F.tubo([(0, -queda - 0.04, avanco), (0, -queda - 0.9, 0.02)], 0.018, 6),
                (sx * largura / 2, 0, 0), haste)


# Os objetos prontos (assets/mapas/pecas/<nome>.glb, do Poly Haven, CC0)
# e o tamanho de cada um — largura, altura, fundo — para a colisão.
# A tabela sai de tools/mapas/blender/pecas_polyhaven.py.
OBJETOS = {
    "ar_condicionado": (1.8, 0.93, 0.37), "barreira": (1.55, 0.83, 0.64), "barril_azul": (0.63, 0.93, 0.64),
    "barril_metal": (0.56, 0.88, 0.56), "barril_vinho": (0.74, 0.87, 0.76), "bau_ferramenta": (0.69, 0.65, 0.41),
    "botijao": (0.34, 0.55, 0.34), "caixa_militar": (1.82, 0.3, 0.98), "caixa_militar_madeira": (1.24, 0.46, 0.52),
    "caixa_municao": (0.09, 0.18, 0.26), "caixa_papelao": (0.39, 0.34, 0.52), "caixote_a": (0.83, 0.35, 0.41),
    "caixote_b": (0.53, 0.46, 1.17), "camera": (0.17, 0.29, 0.55), "carrinho": (0.59, 1.4, 0.69),
    "carro_coberto": (1.79, 1.41, 4.38), "carro_ferramenta": (1.27, 0.96, 0.75), "compressor": (0.6, 1.18, 1.68),
    "escada_madeira": (0.96, 1.33, 0.5), "estante_b": (0.59, 2.14, 0.5), "fogueira": (1.45, 0.39, 1.43),
    "galao_plastico": (0.27, 0.32, 0.34), "galao_verde": (0.36, 0.5, 0.17), "gerador": (0.82, 0.58, 0.56),
    "lixeira": (1.78, 0.91, 0.56), "luminaria_parede": (0.27, 0.43, 0.14), "mesa_aco": (2.0, 0.79, 0.95),
    "pedra_a": (2.53, 0.88, 1.23), "pedra_b": (2.41, 1.47, 3.07), "pedra_c": (1.27, 1.0, 1.83),
    "placa_piso": (0.3, 0.63, 0.36), "pneu": (0.6, 0.6, 0.16), "poste_rua": (0.7, 3.87, 0.39),
    "quadro_forca": (0.52, 1.12, 0.43), "saco_cimento": (0.46, 0.18, 0.7), "saco_lixo": (0.53, 0.57, 0.46),
}


def objeto(m, grupo, nome, qual, pos, yaw=0.0, colide=True, escala=1.0, som="ferro"):
    """Um objeto pronto, fotografado de coisa real. `pos` é o meio da
    base. Com `colide`, uma caixa do tamanho dele barra o caminho (e
    `som` é o material que se ouve ao pisar ou atirar nela); sem, é
    enfeite — só para o que é pequeno ou fica em cima de outra coisa."""
    m.modelo(grupo, nome, qual, pos, yaw, escala)
    if colide:
        w, h, d = (v * escala for v in OBJETOS[qual])
        m.caixa(grupo, nome + "Colisao", (pos[0], pos[1] + h / 2, pos[2]), (w, h, d), som, True,
                giro_y(yaw) if yaw else None, oculta=True)


def materiais(m):
    """Os materiais que as peças prontas usam, se o mapa não tiver os dele."""
    padrao = {
        "moldura": dict(textura=None, cor=(0.86, 0.85, 0.82), aspereza=0.7),
        "vidro": dict(textura=None, cor=(0.05, 0.07, 0.09), aspereza=0.08, metal=0.6),
        "vidro_aceso": dict(textura=None, cor=(0.3, 0.25, 0.15), aspereza=0.2, brilho=(1.0, 0.78, 0.45), energia=1.3),
        "ferro": dict(textura=None, cor=(0.13, 0.14, 0.15), aspereza=0.55, metal=0.6),
        "porta": dict(textura="madeira", cor=(0.55, 0.42, 0.32), ladrilho=1.4),
        "madeira_escura": dict(textura="madeira_escura", cor=(0.9, 0.86, 0.82), ladrilho=1.2),
        "barril": dict(textura="metal_pintado", cor=(0.25, 0.38, 0.5), ladrilho=1.0, metal=0.3, aspereza=0.7),
        "barril_vermelho": dict(textura="metal_pintado", cor=(0.62, 0.16, 0.12), ladrilho=1.0, metal=0.3, aspereza=0.7),
        "saco": dict(textura="cascalho", cor=(0.7, 0.66, 0.56), ladrilho=0.8),
        "borracha": dict(textura=None, cor=(0.04, 0.04, 0.045), aspereza=0.9),
        "lampada": dict(textura=None, cor=(1, 1, 1), brilho=(1.0, 0.95, 0.85), energia=3.0),
    }
    for nome, d in padrao.items():
        if nome not in m.materiais:
            m.material(nome, d.get("textura"), d.get("cor", (1, 1, 1)), d.get("ladrilho", 2.0), d.get("aspereza", 1.0),
                       d.get("metal", 0.0), 1.0, d.get("brilho"), d.get("energia", 1.0))
