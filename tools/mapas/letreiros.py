#!/usr/bin/env python3
"""
Os letreiros dos mapas: placas, pinturas de parede, cartazes e
marcações de piso, desenhados aqui com letra de verdade e gravados em
assets/mapas/letreiros/<nome>.png. O mapa só diz onde vai cada um
(`m.letreiro(grupo, nome, "<nome>", pos, largura, yaw)`).

    python3 tools/mapas/letreiros.py            # todos
    python3 tools/mapas/letreiros.py vila       # só os que começam assim

As letras (tools/mapas/fontes/, licenças em fontes/licencas/) são de uso
livre: Overpass (sinalização — a de estrada e de metrô), Bebas Neue e
Archivo Black (cartaz), Saira Stencil One e Stardos Stencil (pintura
industrial com máscara), Alfa Slab One (letreiro de loja), Permanent
Marker (pichação), Share Tech Mono (painel de pontos).

Quatro jeitos de letreiro:

- `placa`     chapa com fundo, borda, texto, e opcionalmente uma bola de
              linha e uma seta: sinalização.
- `pintura`   só a letra, pintada direto na parede ou no chão, com
              desgaste: estêncil de galpão, nome de loja, pichação.
- `cartaz`    anúncio: blocos de cor e de texto.
- `faixa`     listras de perigo.

O jogo recorta o letreiro pelo alfa (sem meio-termo), então o desgaste é
feito de buracos, não de transparência.
"""
import os
import random
import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(AQUI))
DESTINO = os.path.join(RAIZ, "assets", "mapas", "letreiros")

FONTES = {
    "overpass": ("Overpass.ttf", "Bold"), "overpass_preta": ("Overpass.ttf", "Black"),
    "overpass_fina": ("Overpass.ttf", "Regular"),
    "bebas": ("BebasNeue-Regular.ttf", None), "archivo": ("ArchivoBlack-Regular.ttf", None),
    "saira": ("SairaStencilOne-Regular.ttf", None), "stardos": ("StardosStencil-Bold.ttf", None),
    "alfa": ("AlfaSlabOne-Regular.ttf", None), "marcador": ("PermanentMarker-Regular.ttf", None),
    "pontos": ("ShareTechMono-Regular.ttf", None),
}

AMARELO, PRETO, BRANCO = (244, 196, 24), (16, 17, 19), (240, 240, 236)
AZUL_TIME, LARANJA_TIME = (70, 140, 255), (255, 128, 50)


def fonte(nome, px):
    arquivo, peso = FONTES[nome]
    f = ImageFont.truetype(os.path.join(AQUI, "fontes", arquivo), px)
    if peso:
        f.set_variation_by_name(peso)
    return f


def _medir(texto, f):
    x0, y0, x1, y1 = f.getbbox(texto)
    return x1 - x0, y1 - y0, x0, y0


def _ruido(tamanho, grao, semente):
    """Manchas: ruído grosso e fino misturados, de 0 a 255."""
    r = random.Random(semente)
    w, h = tamanho
    grosso = Image.new("L", (max(2, w // grao), max(2, h // grao)))
    grosso.putdata([r.randint(0, 255) for _ in range(grosso.width * grosso.height)])
    fino = Image.new("L", (max(2, w // 3), max(2, h // 3)))
    fino.putdata([r.randint(0, 255) for _ in range(fino.width * fino.height)])
    return Image.blend(grosso.resize(tamanho, Image.BICUBIC), fino.resize(tamanho, Image.BICUBIC), 0.35)


def _gastar(img, quanto, semente):
    """Abre buracos na tinta: `quanto` de 0 (nova) a 1 (quase sumida)."""
    if quanto <= 0:
        return img
    corte = int(255 * (1 - quanto * 0.55))
    buracos = _ruido(img.size, 28, semente).point(lambda v: 255 if v < corte else 0)
    a = ImageChops.multiply(img.getchannel("A"), buracos)
    img.putalpha(a)
    return img


def _sujar(img, quanto, semente):
    """Escurece em manchas, sem mexer no recorte."""
    if quanto <= 0:
        return img
    mancha = _ruido(img.size, 40, semente + 7).point(lambda v: int(255 - v * quanto * 0.5))
    rgb = ImageChops.multiply(img.convert("RGB"), Image.merge("RGB", (mancha,) * 3))
    rgb.putalpha(img.getchannel("A"))
    return rgb


def _seta(d, cx, cy, lado, para, cor):
    """Seta de sinalização: haste e ponta, apontando para 'dir', 'esq', 'cima' ou 'baixo'."""
    m = lado / 2
    pts = [(-m, -m * 0.22), (m * 0.1, -m * 0.22), (m * 0.1, -m * 0.62), (m, 0),
           (m * 0.1, m * 0.62), (m * 0.1, m * 0.22), (-m, m * 0.22)]
    giro = {"dir": lambda x, y: (x, y), "esq": lambda x, y: (-x, y),
            "cima": lambda x, y: (y, -x), "baixo": lambda x, y: (y, x)}[para]
    d.polygon([(cx + giro(x, y)[0], cy + giro(x, y)[1]) for x, y in pts], fill=cor)


def placa(texto, letra="overpass", px=150, cor=BRANCO, fundo=(18, 44, 96), borda=None, sub=None,
          bola=None, seta=None, sujeira=0.25, semente=1):
    """Chapa de sinalização. `bola`: (cor, texto) da linha, à esquerda.
    `seta`: 'dir', 'esq', 'cima', 'baixo'. `sub`: uma linha menor embaixo."""
    f = fonte(letra, px)
    fs = fonte("overpass_fina", int(px * 0.38)) if sub else None
    tw, th, _, _ = _medir(texto, f)
    sw = _medir(sub, fs)[0] if sub else 0
    margem = int(px * 0.45)
    extra = int(px * 1.25)
    largura = max(tw, sw) + margem * 2 + (extra if bola else 0) + (extra if seta else 0)
    altura = int(px * (1.9 if sub else 1.55))
    img = Image.new("RGBA", (largura, altura), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    raio = int(px * 0.16)
    d.rounded_rectangle((0, 0, largura - 1, altura - 1), raio, fill=fundo + (255,))
    if borda:
        e = max(3, px // 22)
        d.rounded_rectangle((e * 2, e * 2, largura - 1 - e * 2, altura - 1 - e * 2), raio, outline=borda + (255,), width=e)

    x = margem
    meio = altura // 2 - (int(px * 0.16) if sub else 0)
    if seta == "esq":
        _seta(d, x + extra * 0.4, altura / 2, px * 0.95, "esq", cor)
        x += extra
    if bola:
        r = int(px * 0.5)
        d.ellipse((x, altura // 2 - r, x + 2 * r, altura // 2 + r), fill=bola[0] + (255,))
        fb = fonte("overpass_preta", int(px * 0.62))
        bw, bh, bx, by = _medir(bola[1], fb)
        d.text((x + r - bw / 2 - bx, altura / 2 - bh / 2 - by), bola[1], font=fb, fill=BRANCO)
        x += extra
    d.text((x, meio), texto, font=f, fill=cor, anchor="lm")
    if sub:
        d.text((x, altura - int(px * 0.42)), sub, font=fs, fill=cor, anchor="lm")
    if seta and seta != "esq":
        _seta(d, largura - margem - extra * 0.4, altura / 2, px * 0.95, seta, cor)
    return _sujar(img, sujeira, semente)


def pintura(texto, letra="saira", px=220, cor=BRANCO, desgaste=0.3, semente=1, contorno=None, linhas=None):
    """Só a letra, sobre nada: pintada direto na superfície."""
    f = fonte(letra, px)
    partes = linhas or [texto]
    medidas = [_medir(t, f) for t in partes]
    largura = max(m[0] for m in medidas) + px // 2
    passo = int(px * 1.05)
    altura = passo * len(partes) + px // 3
    img = Image.new("RGBA", (largura, altura), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for i, t in enumerate(partes):
        y = px // 6 + passo * i + passo // 2
        if contorno:
            d.text((largura // 2, y), t, font=f, fill=contorno + (255,), anchor="mm", stroke_width=max(3, px // 16),
                   stroke_fill=contorno + (255,))
        d.text((largura // 2, y), t, font=f, fill=cor + (255,), anchor="mm")
    return _sujar(_gastar(img, desgaste, semente), 0.3, semente)


def cartaz(tamanho, fundo, blocos, moldura=(30, 30, 34), sujeira=0.3, semente=1):
    """Anúncio. `blocos`: ("texto", letra, px, cor, y de 0 a 1) ou
    ("faixa", cor, y0, y1) ou ("bola", cor, x, y, raio) — em fração do cartaz."""
    w, h = tamanho
    img = Image.new("RGBA", (w, h), fundo + (255,))
    d = ImageDraw.Draw(img)
    for b in blocos:
        if b[0] == "faixa":
            d.rectangle((0, int(b[2] * h), w, int(b[3] * h)), fill=b[1] + (255,))
        elif b[0] == "bola":
            r = int(b[4] * w)
            d.ellipse((int(b[2] * w) - r, int(b[3] * h) - r, int(b[2] * w) + r, int(b[3] * h) + r), fill=b[1] + (255,))
        else:
            _, texto, letra, px, cor, y = b
            d.text((w // 2, int(y * h)), texto, font=fonte(letra, px), fill=cor + (255,), anchor="mm")
    e = max(6, w // 60)
    d.rectangle((0, 0, w - 1, h - 1), outline=moldura + (255,), width=e)
    return _sujar(img, sujeira, semente)


def faixa(largura=1024, altura=96, a=AMARELO, b=PRETO, desgaste=0.2, semente=1):
    """Listras de perigo, em diagonal."""
    img = Image.new("RGBA", (largura, altura), a + (255,))
    d = ImageDraw.Draw(img)
    passo = altura
    for x in range(-altura, largura + altura, passo * 2):
        d.polygon([(x, altura), (x + passo, altura), (x + passo + altura, 0), (x + altura, 0)], fill=b + (255,))
    return _sujar(_gastar(img, desgaste, semente), 0.3, semente)


def anel(lado=1024, cor=AMARELO, desgaste=0.35, semente=5):
    """O anel pintado da plataforma de manobra: um círculo cheio, um
    tracejado por dentro e quatro setas apontando para fora."""
    import math
    img = Image.new("RGBA", (lado, lado), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = lado / 2
    for raio, grossura in ((0.48, 0.04),):
        d.ellipse((c - raio * lado, c - raio * lado, c + raio * lado, c + raio * lado), outline=cor + (255,), width=int(grossura * lado))
    for i in range(24):
        if i % 2:
            continue
        a0, a1 = i * 15, i * 15 + 15
        d.arc((c - 0.36 * lado, c - 0.36 * lado, c + 0.36 * lado, c + 0.36 * lado), a0, a1, fill=cor + (255,), width=int(0.022 * lado))
    for para, (dx, dy) in (("dir", (1, 0)), ("esq", (-1, 0)), ("cima", (0, -1)), ("baixo", (0, 1))):
        _seta(d, c + dx * 0.24 * lado, c + dy * 0.24 * lado, 0.11 * lado, para, cor + (255,))
    return _sujar(_gastar(img, desgaste, semente), 0.3, semente)


def painel(texto, cor=(255, 170, 40), px=110, fundo=(8, 8, 9)):
    """Painel de pontos luminosos: o letreiro de destino do trem."""
    f = fonte("pontos", px)
    tw, th, _, _ = _medir(texto, f)
    img = Image.new("RGBA", (tw + px, int(px * 1.5)), fundo + (255,))
    d = ImageDraw.Draw(img)
    d.text((img.width // 2, img.height // 2), texto, font=f, fill=cor + (255,), anchor="mm")
    # A grade que separa os pontos.
    for x in range(0, img.width, 6):
        d.line((x, 0, x, img.height), fill=fundo + (255,), width=2)
    for y in range(0, img.height, 6):
        d.line((0, y, img.width, y), fill=fundo + (255,), width=2)
    return img


# ------------------------------------------------------------------ a lista

def _lista():
    L = {}

    # ---- Vila
    for nome, texto, letra, cor in (
            ("padaria", "PADARIA", "alfa", (120, 50, 28)), ("mercado", "MERCADO", "alfa", (26, 70, 110)),
            ("cafe", "CAFÉ DO SINO", "alfa", (70, 40, 24)), ("barbearia", "BARBEARIA", "bebas", (150, 30, 30)),
            ("oficina", "OFICINA", "saira", (40, 44, 50)), ("pousada", "POUSADA DA FONTE", "alfa", (30, 90, 80)),
            ("correio", "CORREIO", "overpass_preta", (170, 120, 20)), ("armazem", "ARMAZÉM", "stardos", (60, 40, 30)),
            ("tapetes", "TAPETES", "alfa", (130, 40, 60)), ("ferragens", "FERRAGENS", "bebas", (40, 60, 50))):
        L[f"vila_{nome}"] = (lambda t=texto, l=letra, c=cor, n=nome: pintura(t, l, 200, c, desgaste=0.32, semente=len(n) * 7))
    for nome, texto in (("rua_fonte", "Rua da Fonte"), ("beco_sino", "Beco do Sino"), ("praca", "Praça Velha"),
                        ("rua_mercado", "Rua do Mercado"), ("travessa", "Travessa das Palmeiras")):
        L[f"vila_{nome}"] = (lambda t=texto: placa(t, "overpass", 120, cor=BRANCO, fundo=(24, 60, 120), borda=BRANCO, sujeira=0.35))
    L["vila_picho"] = lambda: pintura("aqui é lobo", "marcador", 200, (30, 30, 34), desgaste=0.2, semente=13)

    # ---- Estação Polar
    L["polar_estacao"] = lambda: pintura("ESTAÇÃO BOREAL-3", "saira", 230, (236, 238, 242), desgaste=0.3, semente=2)
    L["polar_garagem"] = lambda: pintura("GARAGEM 01", "saira", 230, (236, 238, 242), desgaste=0.35, semente=3)
    for n in ("01", "02"):
        L[f"polar_diesel_{n}"] = (lambda n=n: pintura(f"DIESEL {n}", "stardos", 220, (34, 38, 44), desgaste=0.3, semente=int(n) + 4))
    for cod in ("A1", "A2", "B1", "B2"):
        L[f"polar_modulo_{cod}"] = (lambda c=cod: pintura(f"MÓDULO {c}", "saira", 200, (236, 238, 242), desgaste=0.3, semente=ord(c[0]) + int(c[1])))
    for nome, texto in (("lab", "LABORATÓRIO"), ("radio", "RÁDIO"), ("enfermaria", "ENFERMARIA"), ("refeitorio", "REFEITÓRIO"),
                        ("gerador", "GERADOR"), ("oficina", "OFICINA"), ("deposito", "DEPÓSITO")):
        L[f"polar_{nome}"] = (lambda t=texto: placa(t, "overpass", 120, cor=BRANCO, fundo=(30, 34, 40), borda=(220, 222, 226), sujeira=0.3))
    L["polar_tensao"] = lambda: placa("PERIGO · ALTA TENSÃO", px=110, cor=PRETO, fundo=AMARELO, borda=PRETO)
    L["polar_gelo"] = lambda: placa("CUIDADO · GELO NA PISTA", px=110, cor=PRETO, fundo=AMARELO, borda=PRETO)
    L["polar_pouso"] = lambda: pintura("H", "overpass_preta", 420, (240, 200, 40), desgaste=0.4, semente=6)


    # ---- De todos: a letra da base de cada time e a faixa de perigo.
    for mapa in ("vila", "polar"):
        L[f"{mapa}_base_a"] = lambda: pintura("A", "overpass_preta", 520, AZUL_TIME, desgaste=0.18, semente=21, contorno=(12, 20, 40))
        L[f"{mapa}_base_b"] = lambda: pintura("B", "overpass_preta", 520, LARANJA_TIME, desgaste=0.18, semente=22, contorno=(40, 20, 8))
    L["faixa_perigo"] = lambda: faixa()
    return L


def main():
    filtro = sys.argv[1] if len(sys.argv) > 1 else ""
    os.makedirs(DESTINO, exist_ok=True)
    feitos = 0
    for nome, desenhar in sorted(_lista().items()):
        if not nome.startswith(filtro):
            continue
        img = desenhar()
        # Nada maior que 1536 de lado: letreiro não precisa de mais.
        if max(img.size) > 1536:
            k = 1536 / max(img.size)
            img = img.resize((int(img.width * k), int(img.height * k)), Image.LANCZOS)
        img.save(os.path.join(DESTINO, nome + ".png"), optimize=True)
        feitos += 1
    print(f"{feitos} letreiros em assets/mapas/letreiros/")


if __name__ == "__main__":
    main()
