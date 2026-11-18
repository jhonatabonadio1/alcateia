#!/usr/bin/env python3
"""Gera as insígnias das 30 patentes (dez famílias × três graus) em SVG.

    python3 tools/patentes.py <pasta de saída>

Cada insígnia é um brasão facetado (low poly): metade esquerda mais clara,
metade direita mais escura, miolo de esmalte escuro e, no meio, o emblema
da família em metal. O grau são os losangos de cima (um, dois ou três).
O brasão cresce com a família: escudo simples, escudo com ombros, asas
curtas, asas longas, e por fim coroa.

Os SVG viram PNG de 256 px (assets/ui/patentes/NN.png) pelo próprio Godot
(tools/svg2png.gd), que é quem desenha no jogo.
"""
import sys, os, colorsys

FAMILIAS = ["Filhote", "Batedor", "Rastreador", "Caçador", "Sentinela",
            "Uivador", "Presa de Ferro", "Lua Cheia", "Ancião", "Alfa"]
# cor do metal de cada família
CORES = ["a9794f", "c8834a", "aeb8c4", "8fa3b8", "e0b445",
         "f5c84a", "5f9fd6", "b9cdf2", "a77bf5", "ff6a3d"]
CX = 128


def rgb(h):
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def hexa(c):
    return "#" + "".join(f"{max(0, min(255, round(v * 255))):02x}" for v in c)


def claro(h, t):
    r, g, b = rgb(h)
    return hexa((r + (1 - r) * t, g + (1 - g) * t, b + (1 - b) * t))


def escuro(h, t):
    r, g, b = rgb(h)
    return hexa((r * (1 - t), g * (1 - t), b * (1 - t)))


def pts(p):
    return " ".join(f"{x:.1f},{y:.1f}" for x, y in p)


def espelho(p):
    return [(2 * CX - x, y) for x, y in p]


class Svg:
    def __init__(self):
        self.defs, self.corpo, self.n = [], [], 0

    def grad(self, a, b, x1=0, y1=0, x2=0, y2=1):
        self.n += 1
        self.defs.append(
            f'<linearGradient id="g{self.n}" x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}">'
            f'<stop offset="0" stop-color="{a}"/><stop offset="1" stop-color="{b}"/></linearGradient>')
        return f"url(#g{self.n})"

    def poly(self, p, fill, extra=""):
        self.corpo.append(f'<polygon points="{pts(p)}" fill="{fill}" {extra}/>')

    def raw(self, s):
        self.corpo.append(s)

    def texto(self):
        return ('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">'
                f'<defs>{"".join(self.defs)}</defs>{"".join(self.corpo)}</svg>')


# metade direita de cada brasão, do alto (no eixo) até embaixo (no eixo)
BRASOES = [
    [(128, 30), (194, 46), (194, 130), (128, 214)],                       # escudo simples
    [(128, 26), (162, 40), (200, 32), (202, 126), (128, 220)],            # com ombros
    [(128, 22), (188, 48), (188, 136), (128, 220)],                       # hexagonal (asas curtas)
    [(128, 14), (152, 40), (190, 48), (190, 138), (128, 226)],            # ponta em cima (asas longas)
    [(128, 40), (160, 40), (190, 52), (190, 140), (128, 228)],            # sob a coroa
]
# penas da asa direita, por grupo
ASAS = [
    [],
    [],
    [[(192, 66), (236, 50), (226, 90), (192, 98)],
     [(192, 104), (228, 98), (216, 130), (192, 134)]],
    [[(194, 58), (248, 34), (238, 80), (194, 90)],
     [(194, 96), (240, 86), (228, 122), (194, 128)],
     [(194, 134), (228, 128), (214, 158), (190, 160)]],
    [[(194, 60), (252, 26), (242, 78), (194, 92)],
     [(194, 98), (244, 84), (232, 124), (194, 132)],
     [(194, 138), (232, 130), (216, 164), (188, 166)]],
]
COROA = [(92, 44), (96, 10), (112, 28), (128, 4), (144, 28), (160, 10), (164, 44)]


def encolher(p, d):
    """Encolhe o contorno em direção ao centro do brasão (aproximação boa
    para estas formas convexas)."""
    cx, cy = CX, 118
    out = []
    for x, y in p:
        dx, dy = x - cx, y - cy
        n = (dx * dx + dy * dy) ** 0.5 or 1
        out.append((x - dx / n * d, y - dy / n * d))
    return out


def losango(s, x, y, r, fill):
    s.poly([(x, y - r), (x + r * 0.72, y), (x, y + r), (x - r * 0.72, y)], fill)


def emblema(s, fam, metal, sombra):
    """O desenho do meio, em torno de (128, 104)."""
    f = lambda p: s.poly(p, metal)
    if fam == 0:      # pata
        s.raw(f'<ellipse cx="128" cy="122" rx="24" ry="19" fill="{metal}"/>')
        for x, y, r in [(96, 96, 10), (116, 78, 11), (140, 78, 11), (160, 96, 10)]:
            s.raw(f'<ellipse cx="{x}" cy="{y}" rx="{r}" ry="{r * 1.25:.1f}" fill="{metal}"/>')
    elif fam == 1:    # divisas duplas
        for y in (0, 30):
            f([(128, 66 + y), (166, 100 + y), (166, 120 + y), (128, 88 + y), (90, 120 + y), (90, 100 + y)])
    elif fam == 2:    # mira de rastreio
        s.raw(f'<circle cx="128" cy="104" r="34" fill="none" stroke="{metal}" stroke-width="9"/>')
        for a, b in [((128, 56), (128, 76)), ((128, 132), (128, 152)), ((80, 104), (100, 104)), ((156, 104), (176, 104))]:
            s.raw(f'<line x1="{a[0]}" y1="{a[1]}" x2="{b[0]}" y2="{b[1]}" stroke="{metal}" stroke-width="9" stroke-linecap="round"/>')
        losango(s, 128, 104, 13, metal)
    elif fam == 3:    # ponta de flecha
        f([(128, 56), (168, 142), (128, 120), (88, 142)])
        s.poly([(128, 56), (168, 142), (128, 120)], sombra)
    elif fam == 4:    # torre
        f([(98, 66), (112, 66), (112, 78), (121, 78), (121, 66), (135, 66), (135, 78), (144, 78), (144, 66), (158, 66),
           (158, 88), (150, 96), (150, 132), (160, 146), (96, 146), (106, 132), (106, 96), (98, 88)])
        s.poly([(120, 146), (120, 122), (128, 114), (136, 122), (136, 146)], sombra)
    elif fam == 5:    # lua crescente
        s.raw(f'<path d="M150,60 A46,46 0 1 0 166,138 A38,38 0 1 1 150,60 Z" fill="{metal}"/>')
    elif fam == 6:    # presas
        f([(92, 62), (164, 62), (158, 78), (98, 78)])
        f([(100, 82), (124, 82), (118, 124), (108, 150)])
        f([(156, 82), (132, 82), (138, 124), (148, 150)])
    elif fam == 7:    # lua cheia
        s.raw(f'<circle cx="128" cy="104" r="44" fill="none" stroke="{metal}" stroke-width="6"/>')
        s.raw(f'<circle cx="128" cy="104" r="31" fill="{metal}"/>')
        for x, y, r in [(118, 94, 7), (140, 108, 5), (124, 118, 4)]:
            s.raw(f'<circle cx="{x}" cy="{y}" r="{r}" fill="{sombra}"/>')
    elif fam == 8:    # estrela de quatro pontas
        f([(128, 52), (140, 92), (180, 104), (140, 116), (128, 156), (116, 116), (76, 104), (116, 92)])
        s.poly([(128, 52), (140, 92), (180, 104), (128, 104)], sombra)
        s.poly([(128, 156), (116, 116), (76, 104), (128, 104)], sombra)
    else:             # cabeça de lobo
        f([(128, 156), (106, 138), (90, 108), (86, 56), (110, 82), (128, 76), (146, 82), (170, 56), (166, 108), (150, 138)])
        s.poly([(128, 76), (146, 82), (170, 56), (166, 108), (150, 138), (128, 156)], sombra)
        for p in ([(102, 104), (120, 112), (108, 118)],):
            s.poly(p, "#0d131b")
            s.poly(espelho(p), "#0d131b")
        s.poly([(121, 142), (135, 142), (128, 151)], "#0d131b")


def insignia(patente):
    fam, grau = patente // 3, patente % 3
    grupo = fam // 2
    cor = CORES[fam]
    s = Svg()

    metade = BRASOES[grupo]
    contorno = metade + espelho(metade)[::-1][1:-1]
    esq = [metade[0]] + espelho(metade)[1:-1] + [metade[-1]]
    dirt = metade

    luz = s.grad(claro(cor, 0.55), claro(cor, 0.05))
    meio = s.grad(claro(cor, 0.12), escuro(cor, 0.38))
    fundo = s.grad(escuro(cor, 0.25), escuro(cor, 0.6))
    esm1 = s.grad("#263243", "#141c27")
    esm2 = s.grad("#1b2532", "#0d131b")
    emb = s.grad(claro(cor, 0.75), claro(cor, 0.15))
    beira = 'stroke="#0b1118" stroke-opacity="0.55" stroke-width="3" stroke-linejoin="round"'

    # asas (atrás do corpo)
    for i, pena in enumerate(ASAS[grupo]):
        for lado, tom in ((pena, meio), (espelho(pena), luz)):
            s.poly(lado, tom, beira)
        risco = [pena[0], pena[1], ((pena[1][0] + pena[2][0]) / 2, (pena[1][1] + pena[2][1]) / 2)]
        s.poly(risco, claro(cor, 0.35), 'opacity="0.55"')
        s.poly(espelho(risco), claro(cor, 0.7), 'opacity="0.55"')

    # coroa
    if grupo == 4:
        s.poly(COROA, s.grad(claro("f5c84a", 0.45), escuro("f5c84a", 0.2)), beira)
        s.poly([COROA[3], COROA[4], COROA[5], COROA[6], (128, 44)], escuro("f5c84a", 0.22), 'opacity="0.6"')
        for x in (96, 128, 160):
            losango(s, x, 10 if x != 128 else 4, 5, "#fff6d8")

    # corpo: contorno, metades, miolo
    s.poly(contorno, fundo, beira)
    s.poly(esq, luz)
    s.poly(dirt, meio)
    dentro = encolher(metade, 13)
    dentro[0] = (CX, dentro[0][1]); dentro[-1] = (CX, dentro[-1][1])
    s.poly([dentro[0]] + espelho(dentro)[1:-1] + [dentro[-1]], esm1)
    s.poly(dentro, esm2)
    # fio de luz na beira de cima do miolo
    s.raw(f'<polyline points="{pts(espelho(dentro)[1:3][::-1] + dentro[:3])}" fill="none" '
          f'stroke="{claro(cor, 0.6)}" stroke-opacity="0.5" stroke-width="2"/>')

    # o emblema, um pouco menor e mais para baixo: em cima dele vai o grau
    s.raw('<g transform="translate(128,116) scale(0.86) translate(-128,-104)">')
    emblema(s, fam, emb, escuro(cor, 0.1))
    s.raw('</g>')

    # grau: losangos em cima, onde o brasão é largo
    y = dentro[0][1] + (30 if grupo != 4 else 22)
    for i in range(grau + 1):
        x = CX + (i - grau / 2) * 25
        losango(s, x, y, 13, "#0b1118")
        losango(s, x, y, 10, claro(cor, 0.7))
    return s.texto()


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out, exist_ok=True)
    for p in range(30):
        open(os.path.join(out, f"{p:02d}.svg"), "w").write(insignia(p))
    print("30 insígnias em", out)
