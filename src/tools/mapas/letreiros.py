#!/usr/bin/env python3
"""
Os letreiros dos mapas: placas, pinturas de piso e de parede, painéis e
marcações, desenhados aqui com letra de verdade e gravados em
src/assets/mapas/letreiros/<nome>.png. O mapa só diz onde vai cada um
(`m.letreiro(grupo, nome, "<nome>", pos, largura, yaw)`).

    python3 src/tools/mapas/letreiros.py              # todos
    python3 src/tools/mapas/letreiros.py setor_zero   # só os que começam assim

Precisa do Pillow (`pip install pillow`).

Este arquivo saiu do repositório em 24/11, junto com a Vila e a Estação
Polar (os únicos mapas que tinham letreiro), e voltou em 26/11 para o
Setor Zero, o centro de treinamento: voltaram só as três letras que ele
usa e a lista dele (a da Vila e a da Polar estão no histórico do git).

As letras (src/tools/mapas/fontes/, licenças em fontes/licencas/) são de
uso livre (SIL OFL 1.1): Overpass (sinalização — a de estrada e de
metrô), Bebas Neue (título de painel) e Saira Stencil One (pintura
industrial com máscara).

Os jeitos de letreiro:

- `placa`     chapa com fundo, borda e texto: sinalização de setor.
- `pintura`   só a letra, pintada direto na parede ou no chão.
- `faixa`     listras de perigo.
- `anel`      círculo pintado no piso (a marca de onde parar).
- `divisa`    setas em "V" pintadas no piso (o sentido da rampa).

O jogo recorta o letreiro pelo alfa (sem meio-termo), então o desgaste é
feito de buracos, não de transparência. O Setor Zero é uma academia
limpa: os letreiros dele saem novos, sem desgaste nem sujeira.
"""
import math
import os
import random
import sys
from PIL import Image, ImageDraw, ImageFont, ImageChops

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(AQUI))
DESTINO = os.path.join(RAIZ, "assets", "mapas", "letreiros")

FONTES = {
    "overpass": ("Overpass.ttf", "Bold"), "overpass_preta": ("Overpass.ttf", "Black"),
    "overpass_fina": ("Overpass.ttf", "Regular"),
    "bebas": ("BebasNeue-Regular.ttf", None),
    "saira": ("SairaStencilOne-Regular.ttf", None),
}

AMARELO, PRETO, BRANCO = (244, 196, 24), (16, 17, 19), (240, 240, 236)
ACO = (22, 28, 38)
# A cor de cada bloco do treino (a mesma da planta, docs/tutorial/planta_setor_zero.png).
CIANO, AMBAR, VIOLETA, AZUL, LARANJA, VERDE = (34, 211, 238), (245, 158, 11), (167, 139, 250), (56, 189, 248), (249, 115, 22), (52, 211, 153)
ROSA, VERMELHO = (240, 171, 252), (251, 113, 133)


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


def placa(texto, letra="overpass", px=150, cor=BRANCO, fundo=ACO, borda=None, sub=None, sujeira=0.0, semente=1):
    """Chapa de sinalização. `sub`: uma linha menor embaixo."""
    f = fonte(letra, px)
    fs = fonte("overpass_fina", int(px * 0.38)) if sub else None
    tw, th, _, _ = _medir(texto, f)
    sw = _medir(sub, fs)[0] if sub else 0
    margem = int(px * 0.45)
    largura = max(tw, sw) + margem * 2
    altura = int(px * (1.9 if sub else 1.55))
    img = Image.new("RGBA", (largura, altura), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    raio = int(px * 0.16)
    d.rounded_rectangle((0, 0, largura - 1, altura - 1), raio, fill=fundo + (255,))
    if borda:
        e = max(3, px // 22)
        d.rounded_rectangle((e * 2, e * 2, largura - 1 - e * 2, altura - 1 - e * 2), raio, outline=borda + (255,), width=e)
    meio = altura // 2 - (int(px * 0.16) if sub else 0)
    d.text((margem, meio), texto, font=f, fill=cor, anchor="lm")
    if sub:
        d.text((margem, altura - int(px * 0.42)), sub, font=fs, fill=cor, anchor="lm")
    return _sujar(img, sujeira, semente)


def pintura(texto, letra="saira", px=220, cor=BRANCO, desgaste=0.0, semente=1, linhas=None):
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
        d.text((largura // 2, px // 6 + passo * i + passo // 2), t, font=f, fill=cor + (255,), anchor="mm")
    return _gastar(img, desgaste, semente)


def faixa(largura=1024, altura=96, a=AMARELO, b=PRETO, desgaste=0.0, semente=1):
    """Listras de perigo, em diagonal."""
    img = Image.new("RGBA", (largura, altura), a + (255,))
    d = ImageDraw.Draw(img)
    passo = altura
    for x in range(-altura, largura + altura, passo * 2):
        d.polygon([(x, altura), (x + passo, altura), (x + passo + altura, 0), (x + altura, 0)], fill=b + (255,))
    return _gastar(img, desgaste, semente)


def anel(lado=1024, cor=CIANO, tracos=24, grossura=0.035, miolo=None):
    """Um círculo pintado no piso: a linha de fora cheia e outra tracejada
    por dentro. `miolo`: uma letra ou algarismo no meio."""
    img = Image.new("RGBA", (lado, lado), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = lado / 2
    d.ellipse((c - 0.48 * lado, c - 0.48 * lado, c + 0.48 * lado, c + 0.48 * lado), outline=cor + (255,), width=int(grossura * lado))
    passo = 360 / tracos
    for i in range(0, tracos, 2):
        d.arc((c - 0.36 * lado, c - 0.36 * lado, c + 0.36 * lado, c + 0.36 * lado), i * passo, (i + 1) * passo, fill=cor + (255,), width=int(grossura * 0.7 * lado))
    if miolo:
        d.text((c, c), miolo, font=fonte("overpass_preta", int(lado * 0.34)), fill=cor + (255,), anchor="mm")
    return img


def divisa(quantas=5, cor=AZUL, lado=256):
    """Setas em "V" deitado, uma atrás da outra, apontando para a direita:
    o sentido de subida da rampa."""
    img = Image.new("RGBA", (int(lado * 0.62 * quantas + lado * 0.5), lado), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    g = lado * 0.2
    for i in range(quantas):
        x = i * lado * 0.62 + lado * 0.08
        d.polygon([(x, lado * 0.08), (x + g, lado * 0.08), (x + g + lado * 0.36, lado / 2), (x + g, lado * 0.92), (x, lado * 0.92),
                   (x + lado * 0.36, lado / 2)], fill=cor + (255,))
    return img


def marca(lado=512, cor=CIANO):
    """A marca de onde pisar: quatro cantos vivos e um losango no meio."""
    img = Image.new("RGBA", (lado, lado), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    e, canto = int(lado * 0.06), int(lado * 0.28)
    for sx in (0, 1):
        for sy in (0, 1):
            x0 = 0 if sx == 0 else lado - canto
            y0 = 0 if sy == 0 else lado - e
            d.rectangle((x0, y0, x0 + canto - 1, y0 + e - 1), fill=cor + (255,))
            x0 = 0 if sx == 0 else lado - e
            y0 = 0 if sy == 0 else lado - canto
            d.rectangle((x0, y0, x0 + e - 1, y0 + canto - 1), fill=cor + (255,))
    c, r = lado / 2, lado * 0.2
    d.polygon([(c, c - r), (c + r, c), (c, c + r), (c - r, c)], fill=cor + (255,))
    return img


def hachura(largura=512, altura=768, cor=VERMELHO, passo=64):
    """Listras finas em diagonal com moldura: a beira de um vão."""
    img = Image.new("RGBA", (largura, altura), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for x in range(-altura, largura + altura, passo):
        d.line((x, altura, x + altura, 0), fill=cor + (255,), width=passo // 4)
    d.rectangle((0, 0, largura - 1, altura - 1), outline=cor + (255,), width=passo // 4)
    return img


# ------------------------------------------------------------------ a lista

def _lista():
    L = {}

    # ---- Setor Zero (o centro de treinamento; src/tools/mapas/setor_zero.py)
    sz = "setor_zero_"
    # Tudo o que se lê no mapa é em inglês (Jhon, 26/11: "escritas no mapa sempre em inglês"), como as falas da Iris. Os
    # nomes dos arquivos continuam os dos lugares, em português (são os do código e da planta).
    # O nome de cada setor, numa chapa de aço com a letra na cor do bloco do treino.
    for nome, texto, cor in (
            ("setor_zero", "SECTOR ZERO", BRANCO), ("nucleo_da_iris", "IRIS CORE", CIANO),
            ("sala_de_ativacao", "ACTIVATION ROOM", CIANO), ("corredor_de_calibragem", "CALIBRATION CORRIDOR", CIANO),
            ("linha_de_fogo", "FIRING RANGE", AMBAR), ("arsenal", "ARMORY", AMBAR), ("poco_da_granada", "GRENADE PIT", AMBAR),
            ("camara_de_metamorfose", "METAMORPHOSIS CHAMBER", VIOLETA), ("arena_da_matilha", "PACK ARENA", AZUL),
            ("parede_de_corrida", "WALL RUN", AZUL), ("rua_ignicao", "IGNITION STREET", LARANJA),
            ("ponto_de_retorno", "RETURN POINT", VERDE), ("portal_jogar", "PORTAL · PLAY", VERDE),
            ("praca_do_uivo", "HOWL SQUARE", AMARELO), ("cafe_do_setor", "SECTOR CAFÉ", ROSA),
            ("posto_medico", "MED BAY", VERDE), ("torre_de_controle", "CONTROL TOWER", CIANO),
            ("jardim_suspenso", "HANGING GARDEN", VERDE), ("quadra_de_agilidade", "AGILITY COURT", CIANO),
            ("pinhal_da_matilha", "PACK PINEWOOD", VERDE), ("galeria", "GALLERY", AZUL), ("mirante", "OVERLOOK", AMBAR),
            ("heliponto", "LANDING PAD", CIANO)):
        L[sz + nome] = (lambda t=texto, c=cor: placa(t, "overpass", 150, cor=c, fundo=ACO, borda=c))
    # A peça que ainda não existe (docs/MAPAS.md, "Peças pendentes do Setor Zero"): o substituto leva este aviso.
    L[sz + "provisorio"] = lambda: placa("PLACEHOLDER", "overpass_preta", 130, cor=PRETO, fundo=AMARELO, borda=PRETO)
    L[sz + "perigo_queda"] = lambda: placa("DANGER · SHEER DROP", "overpass_preta", 120, cor=PRETO, fundo=AMARELO, borda=PRETO)
    # O que se pinta no piso, com máscara.
    for nome, texto, cor in (
            ("vao", "GAP", VERMELHO), ("viga", "BEAM", CIANO), ("trecho_silencioso", "QUIET ZONE", CIANO),
            ("circulo_do_uivo", "HOWL CIRCLE", AZUL), ("rampa_longa", "LONG RAMP", AZUL), ("vao_do_salto", "LEAP GAP", VERMELHO),
            ("reta_de_corrida", "SPRINT LANE", AZUL), ("pista_de_aquecimento", "WARM-UP TRACK", BRANCO),
            ("onda_a", "WAVE A", AMARELO), ("onda_b", "WAVE B", AMARELO), ("onda_c", "WAVE C", AMARELO),
            ("linha_de_tiro", "FIRING LINE", AMBAR), ("pistola", "SIDEARM", AMBAR), ("corpo_a_corpo", "MELEE", AMBAR),
            ("kp", "KP", LARANJA), ("pad", "PAD 01", AMARELO)):
        L[sz + nome] = (lambda t=texto, c=cor: pintura(t, "saira", 220, c))
    for n in range(1, 6):
        L[f"{sz}cabine_{n}"] = (lambda n=n: pintura(f"LANE {n}", "saira", 200, AMBAR))
    for n in range(1, 5):
        L[f"{sz}pedestal_{n}"] = (lambda n=n: anel(768, VERDE, miolo=str(n)))
    L[sz + "anel_iris"] = lambda: anel(1024, CIANO, tracos=36)
    L[sz + "anel_metamorfose"] = lambda: anel(1024, VIOLETA, miolo="3")
    L[sz + "anel_uivo"] = lambda: anel(1024, AZUL, miolo="R")
    L[sz + "anel_lobo"] = lambda: anel(768, VERDE, miolo="L")
    L[sz + "marca"] = lambda: marca(512, CIANO)
    L[sz + "divisas"] = lambda: divisa(5, AZUL)
    L[sz + "guia"] = lambda: divisa(3, (60, 150, 255))          # as setas do fim de cada linha-guia (LED azul)
    L[sz + "hachura"] = lambda: hachura()
    L[sz + "faixa_perigo"] = lambda: faixa()
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
    print(f"{feitos} letreiros em src/assets/mapas/letreiros/")


if __name__ == "__main__":
    main()
