#!/usr/bin/env python3
"""Arte do instalador e da capa da Release, a partir da arte do próprio jogo.

Uso:  python3 instalador/gerar_arte.py   (de qualquer pasta)

Lê  src/assets/personagens/{eve,barut}/thumb.jpg, a fonte Kanit do jogo e
    instalador/icone.svg (precisa do `rsvg-convert`: brew install librsvg)
Grava em instalador/:
    lateral_inicio.bmp   painel esquerdo da tela de boas-vindas (Eve, a capa)
    lateral_fim.bmp      painel esquerdo da tela final (Barut)
    cabecalho.bmp        faixa de cima das telas do meio
    WolfLeague.ico       o ícone (o mesmo do jogo, icon.svg)
    capa.png             a capa da Release (Eve)

As imagens prontas ficam no repositório: o pipeline não roda este script,
só você, quando mudar a arte. Cores: as do menu do jogo (UiTokens.Menu).
"""
import argparse
import io
import os
import subprocess

from PIL import Image, ImageDraw, ImageFont

AZUL_NOITE = (10, 17, 25)      # MenuBg #0a1119
SUPERFICIE = (22, 33, 47)      # MenuSurface2 #16212f
OURO = (243, 185, 80)          # MenuAccent #f3b950
TEXTO = (237, 242, 247)        # MenuTextPrimary #edf2f7
TEXTO_2 = (157, 176, 196)      # MenuTextSecondary #9db0c4


def cobrir(img, largura, altura, cx=0.5, cy=0.5):
    """Recorta e escala a imagem para ocupar largura x altura, centrada em (cx, cy)."""
    f = max(largura / img.width, altura / img.height)
    g = img.resize((round(img.width * f), round(img.height * f)), Image.LANCZOS)
    x = min(max(round(cx * g.width - largura / 2), 0), g.width - largura)
    y = min(max(round(cy * g.height - altura / 2), 0), g.height - altura)
    return g.crop((x, y, x + largura, y + altura))


def degrade(tamanho, cor, de, ate, vertical=True, inicio=0.0, fim=1.0):
    """Camada da cor `cor` cuja opacidade vai de `de` a `ate` entre inicio e fim (0..1)."""
    w, h = tamanho
    n = h if vertical else w
    a = Image.new("L", (1, n))
    for i in range(n):
        t = i / max(n - 1, 1)
        t = min(max((t - inicio) / max(fim - inicio, 1e-6), 0), 1)
        a.putpixel((0, i), round(255 * (de + (ate - de) * t)))
    a = a.resize((w, h)) if vertical else a.rotate(90, expand=True).transpose(Image.FLIP_TOP_BOTTOM).resize((w, h))
    camada = Image.new("RGBA", tamanho, cor + (255,))
    camada.putalpha(a)
    return camada


def marca(base, x, y, altura, fonte):
    """WOLF branco e LEAGUE ouro, empilhados, na fonte do título do jogo."""
    d = ImageDraw.Draw(base)
    f = ImageFont.truetype(fonte, altura)
    passo = round(altura * 0.92)
    for i, (txt, cor) in enumerate((("WOLF", TEXTO), ("LEAGUE", OURO))):
        d.text((x + 2, y + i * passo + 2), txt, font=f, fill=(0, 0, 0, 150))
        d.text((x, y + i * passo), txt, font=f, fill=cor)
    return y + 2 * passo


def lateral(thumb, saida, cx, fonte):
    """164x314, o tamanho do painel das telas de boas-vindas e final do NSIS."""
    w, h = 164, 314
    base = cobrir(thumb, w, h, cx=cx).convert("RGBA")
    base = Image.alpha_composite(base, degrade((w, h), AZUL_NOITE, 0.0, 0.96, inicio=0.45, fim=0.88))
    base = Image.alpha_composite(base, degrade((w, h), AZUL_NOITE, 0.55, 0.0, inicio=0.0, fim=0.22))
    d = ImageDraw.Draw(base)
    d.rectangle((14, 222, 14 + 30, 225), fill=OURO)
    marca(base, 13, 230, 34, fonte)
    d.line((w - 1, 0, w - 1, h), fill=SUPERFICIE)
    base.convert("RGB").save(saida, "BMP")


def icone(raiz_instalador, tamanho):
    png = subprocess.run(
        ["rsvg-convert", "-w", str(tamanho), "-h", str(tamanho), os.path.join(raiz_instalador, "icone.svg")],
        capture_output=True, check=True).stdout
    return Image.open(io.BytesIO(png)).convert("RGBA")


def cabecalho(inst, saida):
    """150x57: fundo azul-noite, o lobo à direita e um fio ouro embaixo."""
    w, h = 150, 57
    base = Image.new("RGBA", (w, h), AZUL_NOITE + (255,))
    lobo = icone(inst, 46)
    base.alpha_composite(lobo, (w - 46 - 8, 5))
    ImageDraw.Draw(base).rectangle((0, h - 2, w, h), fill=OURO)
    base.convert("RGB").save(saida, "BMP")


def ico(inst, saida):
    tam = [16, 24, 32, 48, 64, 128, 256]
    imgs = [icone(inst, t) for t in tam]
    imgs[-1].save(saida, format="ICO", sizes=[(t, t) for t in tam], append_images=imgs[:-1])


def capa(eve, inst, saida, fonte, fonte_texto):
    """960x480 para a Release: a Eve à direita, o nome à esquerda."""
    w, h = 960, 480
    base = Image.new("RGBA", (w, h), AZUL_NOITE + (255,))
    arte = cobrir(eve, 860, h, cx=0.5, cy=0.46).convert("RGBA")
    base.alpha_composite(arte, (w - 860, 0))
    base = Image.alpha_composite(base, degrade((w, h), AZUL_NOITE, 1.0, 0.0, vertical=False, inicio=0.12, fim=0.62))
    base = Image.alpha_composite(base, degrade((w, h), AZUL_NOITE, 0.0, 0.85, inicio=0.62, fim=1.0))
    d = ImageDraw.Draw(base)
    base.alpha_composite(icone(inst, 72), (56, 52))
    d.rectangle((58, 150, 58 + 56, 154), fill=OURO)
    y = marca(base, 56, 160, 92, fonte)
    ft = ImageFont.truetype(fonte_texto, 26)
    d.text((58, y + 16), "Online FPS  ·  Humans × Wolves", font=ft, fill=TEXTO)
    # etiqueta do teste
    fa = ImageFont.truetype(fonte, 24)
    d.rectangle((58, h - 74, 58 + 150, h - 34), fill=OURO)
    d.text((58 + 75, h - 54), "ALPHA TEST", font=fa, fill=(26, 18, 6), anchor="mm")
    base.convert("RGB").save(saida, "PNG", optimize=True)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--raiz", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."), help="raiz do projeto (padrão: a pasta acima de instalador/)")
    p.add_argument("--saida", default=None, help="pasta de saída (padrão: instalador/ na raiz)")
    a = p.parse_args()
    inst = os.path.dirname(os.path.abspath(__file__))   # onde estão icone.svg e o resto
    saida = a.saida or inst
    fonte = os.path.join(a.raiz, "src/assets/fonts/Kanit-BoldItalic.ttf")
    fonte_texto = os.path.join(a.raiz, "src/assets/fonts/Kanit-SemiBold.ttf")
    eve = Image.open(os.path.join(a.raiz, "src/assets/personagens/eve/thumb.jpg")).convert("RGB")
    barut = Image.open(os.path.join(a.raiz, "src/assets/personagens/barut/thumb.jpg")).convert("RGB")
    os.makedirs(saida, exist_ok=True)
    lateral(eve, os.path.join(saida, "lateral_inicio.bmp"), 0.5, fonte)
    lateral(barut, os.path.join(saida, "lateral_fim.bmp"), 0.60, fonte)
    cabecalho(inst, os.path.join(saida, "cabecalho.bmp"))
    ico(inst, os.path.join(saida, "WolfLeague.ico"))
    capa(eve, inst, os.path.join(saida, "capa.png"), fonte, fonte_texto)
    print("arte gerada em", saida)


if __name__ == "__main__":
    main()
