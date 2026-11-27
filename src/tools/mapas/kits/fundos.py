"""
As imagens de fundo dos mapas de kit (``Mapa.panorama``): o horizonte recortado, com transparência, que vai em quatro
painéis em volta do mapa — a cidade ao longe (prédios em três planos, janelas, antenas, caixas d'água) e a serra do
deserto. Desenhadas aqui (Pillow), sem foto de ninguém: ``python3 tools/mapas/kits/fundos.py``.
"""
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
W, H = 4096, 1024


def cidade():
    rnd = random.Random(7); im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    # três planos: o de trás mais claro e azulado (a névoa), o da frente mais escuro e mais baixo
    for plano, (cor, alto, largo) in enumerate((((168, 184, 204), (0.45, 0.92), (60, 150)), ((128, 146, 170), (0.30, 0.70), (70, 170)), ((92, 108, 130), (0.16, 0.46), (80, 200)))):
        cam = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(cam); x = -rnd.randint(0, 60)
        while x < W:
            w = rnd.randint(*largo); h = int(H * rnd.uniform(*alto)); topo = H - h
            d.rectangle([x, topo, x + w, H], fill=cor + (255,))
            tipo = rnd.random()
            if tipo < 0.25: d.rectangle([x + w * 0.2, topo - h * 0.08, x + w * 0.8, topo], fill=cor + (255,))                      # o recuo do último andar
            elif tipo < 0.4: d.polygon([(x, topo), (x + w / 2, topo - w * 0.5), (x + w, topo)], fill=cor + (255,))                 # o telhado em ponta
            elif tipo < 0.55: d.line([(x + w / 2, topo), (x + w / 2, topo - rnd.randint(30, 90))], fill=cor + (255,), width=3)      # a antena
            elif tipo < 0.7: d.rectangle([x + w * 0.6, topo - 22, x + w * 0.85, topo], fill=cor + (255,))                           # a caixa d'água
            # as janelas: fileiras de pontos claros (de dia, o vidro reflete o céu) e algumas escuras
            pw, ph = (9, 13) if plano == 2 else (7, 10) if plano == 1 else (5, 8)
            vidro = tuple(min(255, int(c * 1.22 + 14)) for c in cor); sombra = tuple(int(c * 0.82) for c in cor)
            for wy in range(topo + 14, H - 8, ph + pw):
                for wx in range(x + 8, x + w - pw - 4, pw * 2):
                    r = rnd.random()
                    if r < 0.72: d.rectangle([wx, wy, wx + pw, wy + ph], fill=(vidro if r < 0.5 else sombra) + (255,))
            x += w + rnd.randint(-6, 26)
        im = Image.alpha_composite(im, cam)
    # a base some na névoa (o painel fica atrás do muro; por cima dele o que aparece é o alto dos prédios)
    im.paste((0, 0, 0, 0), (0, H - 6, W, H))          # a última linha não vaza para o alto do painel (a textura repete)
    im.save(os.path.join(RAIZ, 'assets', 'mapas', 'fundos', 'cidade.png'))


def serra():
    """O horizonte do deserto: dunas largas e redondas em três planos (Jhon, 20/11: "montes mais arredondados e largos"),
    a de trás clara e azulada pela distância, com a face de sombra de cada duna."""
    import math
    rnd = random.Random(11); im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    for plano, (cor, alto, ondas, sombra) in enumerate((((222, 200, 176), 0.62, (2, 3, 5), 0.93), ((206, 174, 136), 0.44, (3, 4, 7), 0.9), ((186, 148, 104), 0.27, (4, 5, 9), 0.86))):
        cam = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(cam)
        fases = [rnd.uniform(0, 2 * math.pi) for _ in ondas]
        def topo(x):          # senos de período inteiro: o painel emenda na volta
            t = 2 * math.pi * x / W; y = sum(math.sin(t * k + f) / (i + 1.4) for i, (k, f) in enumerate(zip(ondas, fases)))
            return H - int(H * alto * (0.72 + 0.2 * y))
        pts = [(x, topo(x)) for x in range(0, W + 1, 8)]
        d.polygon([(0, H)] + pts + [(W, H)], fill=cor + (255,))
        # a face de sombra: do alto de cada duna para a direita, uma faixa mais escura que acompanha a curva
        escuro = tuple(int(c * sombra) for c in cor)
        for i in range(1, len(pts) - 1):
            if pts[i][1] < pts[i - 1][1] or pts[i + 1][1] <= pts[i][1]: continue
            j = i
            while j < len(pts) - 1 and pts[j + 1][1] > pts[j][1]: j += 1
            if j - i > 6: d.polygon(pts[i:j + 1] + [(pts[j][0], H), (pts[i][0] + (pts[j][0] - pts[i][0]) // 3, H)], fill=escuro + (255,))
        im = Image.alpha_composite(im, cam.filter(ImageFilter.GaussianBlur(0.8)))
    im.paste((0, 0, 0, 0), (0, H - 6, W, H))
    im.save(os.path.join(RAIZ, 'assets', 'mapas', 'fundos', 'serra.png'))


def picos():
    """O horizonte do Setor Zero: uma cordilheira gelada em três planos, vista do alto de uma montanha (Jhon, 26/11: "ao
    invés de cidade, coloque o fundo de montanhas gelosas"). Rocha azulada pela distância, a neve do meio da encosta para
    cima e a face de sombra de cada pico; a base some na névoa."""
    import math
    rnd = random.Random(23); im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    for plano, (rocha, neve, alto, n, agudo) in enumerate((((176, 192, 214), (236, 242, 250), 0.80, 9, 0.55), ((134, 152, 180), (226, 234, 246), 0.62, 12, 0.7), ((96, 112, 138), (214, 224, 240), 0.44, 15, 0.85))):
        cam = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(cam)
        # os cumes: pontos de quebra num período inteiro (o painel emenda na volta), ligados por arestas serrilhadas
        xs = sorted(rnd.uniform(0, W) for _ in range(n)); hs = [rnd.uniform(0.55, 1.0) for _ in range(n)]
        nos = []
        for i in range(n):
            x0, h0 = xs[i], hs[i]; x1, h1 = (xs[(i + 1) % n] + (W if i == n - 1 else 0)), hs[(i + 1) % n]
            vale = min(h0, h1) * rnd.uniform(0.35, 0.6); meio = (x0 + x1) / 2 + rnd.uniform(-0.15, 0.15) * (x1 - x0)
            nos += [(x0, h0), ((x0 + meio) / 2, (h0 + vale) / 2 * rnd.uniform(0.9, 1.12)), (meio, vale), ((meio + x1) / 2, (vale + h1) / 2 * rnd.uniform(0.9, 1.12))]
        nos = [(x - W, h) for x, h in nos[-4:]] + nos + [(x + W, h) for x, h in nos[:4]]
        def topo(x):
            for (xa, ha), (xb, hb) in zip(nos, nos[1:]):
                if xa <= x <= xb:
                    t_ = (x - xa) / max(xb - xa, 1e-6); h = ha + (hb - ha) * t_
                    return H - int(H * alto * (h + 0.012 * agudo * math.sin(x * 0.11) + 0.008 * math.sin(x * 0.37 + plano)))
            return H
        pts = [(x, topo(x)) for x in range(0, W + 1, 4)]
        d.polygon([(0, H)] + pts + [(W, H)], fill=rocha + (255,))
        # a neve: de cada ponto da crista para baixo, mais funda nos cumes; a borda de baixo é serrilhada
        linha = [(x, y + int((H - y) * (0.34 + 0.16 * math.sin(x * 0.045 + plano * 2)) + 10 * math.sin(x * 0.21))) for x, y in pts]
        d.polygon(pts + linha[::-1], fill=neve + (255,))
        # a face de sombra: à direita de cada cume, a rocha e a neve escurecem
        sombra = Image.new('RGBA', (W, H), (0, 0, 0, 0)); ds = ImageDraw.Draw(sombra)
        for i in range(2, len(pts) - 2):
            if pts[i][1] <= pts[i - 1][1] and pts[i][1] < pts[i + 1][1]:
                j = i
                while j < len(pts) - 1 and pts[j + 1][1] >= pts[j][1]: j += 1
                if j - i > 8: ds.polygon(pts[i:j + 1] + [(pts[j][0], H), (pts[i][0] + (pts[j][0] - pts[i][0]) * 0.28, H)], fill=(40, 58, 96, 58))
        cam = Image.alpha_composite(cam, Image.composite(sombra, Image.new('RGBA', (W, H), (0, 0, 0, 0)), cam.getchannel('A')))
        im = Image.alpha_composite(im, cam.filter(ImageFilter.GaussianBlur(0.7)))
    im.paste((0, 0, 0, 0), (0, H - 6, W, H))
    im.save(os.path.join(RAIZ, 'assets', 'mapas', 'fundos', 'picos.png'))


def neon():
    """O horizonte do Neon (Jhon, 20/11: "prédios bem altos, com letreiros", como a cidade à noite das referências):
    arranha-céus em três planos contra a noite — os de trás roxos de névoa, os da frente quase pretos —, cada um com as
    janelas acesas na cor dele (ciano, magenta, âmbar), faixas de LED, letreiros em pé e a luz de aviso no alto."""
    rnd = random.Random(23); im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    luzes = [(60, 220, 255), (255, 70, 200), (255, 170, 60), (150, 110, 255), (90, 255, 170), (255, 255, 255)]
    planos = (((70, 44, 110), (0.55, 0.98), (90, 190), 0.55), ((40, 28, 78), (0.38, 0.82), (80, 200), 0.8), ((16, 14, 36), (0.20, 0.56), (90, 230), 1.0))
    for plano, (cor, alto, largo, brilho) in enumerate(planos):
        cam = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(cam); x = -rnd.randint(0, 80)
        while x < W:
            w = rnd.randint(*largo); h = int(H * rnd.uniform(*alto)); topo = H - h; luz = rnd.choice(luzes)
            luz = tuple(int(c * brilho + cor[i] * (1 - brilho)) for i, c in enumerate(luz))
            d.rectangle([x, topo, x + w, H], fill=cor + (255,))
            tipo = rnd.random()
            if tipo < 0.3:                                                   # a coroa em degraus e a agulha
                d.rectangle([x + w * 0.18, topo - h * 0.06, x + w * 0.82, topo], fill=cor + (255,)); d.rectangle([x + w * 0.38, topo - h * 0.11, x + w * 0.62, topo], fill=cor + (255,))
                d.line([(x + w / 2, topo - h * 0.11), (x + w / 2, topo - h * 0.11 - rnd.randint(40, 110))], fill=cor + (255,), width=3)
                d.ellipse([x + w / 2 - 4, topo - h * 0.11 - 8, x + w / 2 + 4, topo - h * 0.11], fill=(255, 60, 60, 255))
            elif tipo < 0.5: d.polygon([(x, topo), (x + w * 0.5, topo - w * 0.7), (x + w, topo)], fill=cor + (255,))
            elif tipo < 0.65: d.polygon([(x, topo), (x, topo - w * 0.35), (x + w, topo)], fill=cor + (255,))
            estilo = rnd.random()
            if estilo < 0.4:                                                 # janelas em grade, umas acesas
                pw, ph = (10, 14) if plano == 2 else (7, 10) if plano == 1 else (5, 7)
                for wy in range(topo + 16, H - 8, ph + pw - 2):
                    for wx in range(x + 8, x + w - pw - 4, pw + 6):
                        r = rnd.random()
                        if r < 0.5: d.rectangle([wx, wy, wx + pw, wy + ph], fill=tuple(int(c * (0.55 + 0.45 * rnd.random())) for c in luz) + (255,))
            elif estilo < 0.75:                                              # faixas de LED por andar
                passo = rnd.choice((14, 18, 24))
                for wy in range(topo + 12, H - 8, passo): d.rectangle([x + 6, wy, x + w - 6, wy + max(2, passo // 5)], fill=luz + (255,))
            else:                                                            # filetes em pé
                passo = rnd.choice((10, 14))
                for wx in range(x + 8, x + w - 6, passo): d.rectangle([wx, topo + 10, wx + 2, H], fill=luz + (255,))
            if rnd.random() < 0.5 and w > 110:                               # o letreiro em pé, com os "caracteres" em blocos
                lw = rnd.randint(26, 40); lh = int(h * rnd.uniform(0.25, 0.5)); lx = x + rnd.randint(8, w - lw - 8); ly = topo + rnd.randint(20, max(21, h // 4))
                cl = rnd.choice(luzes); d.rectangle([lx, ly, lx + lw, ly + lh], fill=tuple(int(c * 0.22) for c in cl) + (255,), outline=cl + (255,), width=3)
                for k in range(ly + 8, ly + lh - lw, lw):
                    for _ in range(3):
                        a, b = rnd.randint(4, lw - 12), rnd.randint(0, lw - 14); d.rectangle([lx + a, k + b, lx + a + rnd.randint(4, 10), k + b + rnd.randint(3, 8)], fill=cl + (255,))
            x += w + rnd.randint(-10, 30)
        if plano < 2: cam = cam.filter(ImageFilter.GaussianBlur(1.4 - 0.6 * plano))
        im = Image.alpha_composite(im, cam)
    # o clarão de cada luz (o ar úmido da noite) e a névoa roxa que sobe do chão
    clarao = im.filter(ImageFilter.GaussianBlur(9)); im = Image.alpha_composite(im, Image.blend(Image.new('RGBA', (W, H), (0, 0, 0, 0)), clarao, 0.55))
    nev = Image.new('RGBA', (W, H), (0, 0, 0, 0)); dn = ImageDraw.Draw(nev)
    for y in range(H // 2, H): dn.line([(0, y), (W, y)], fill=(120, 60, 170, int(150 * ((y - H / 2) / (H / 2)) ** 1.6)))
    im = Image.alpha_composite(im, nev)
    im.paste((0, 0, 0, 0), (0, H - 6, W, H))
    im.save(os.path.join(RAIZ, 'assets', 'mapas', 'fundos', 'neon.png'))


def ceu_noite():
    """O céu do Neon (``assets/mapas/ceus/noite_neon.hdr``, 2048 × 1024): noite azul-violeta, mais clara e magenta no
    horizonte (a luz da cidade no ar), a faixa da galáxia atravessando, estrelas — e escuro embaixo."""
    import numpy as np
    w, h = 2048, 1024; rnd = np.random.default_rng(31)
    v = (np.arange(h) + 0.5) / h; u = (np.arange(w) + 0.5) / w; lat = (0.5 - v) * np.pi; lon = (u - 0.5) * 2 * np.pi
    LA, LO = np.meshgrid(lat, lon, indexing='ij'); alt = np.clip(LA / (np.pi / 2), -1, 1)
    def rampa(x, a, b): return np.clip((x - a) / (b - a), 0, 1)
    topo = np.array([0.010, 0.012, 0.050]); meio = np.array([0.050, 0.030, 0.150]); horiz = np.array([0.42, 0.14, 0.50]); chao = np.array([0.020, 0.012, 0.035])
    t = rampa(alt, 0.0, 1.0)[..., None]
    ceu = horiz * (1 - rampa(alt, 0.0, 0.22)[..., None]) + meio * (rampa(alt, 0.0, 0.22) * (1 - rampa(alt, 0.22, 0.8)))[..., None] + topo * rampa(alt, 0.22, 0.8)[..., None]
    ceu += horiz * (np.exp(-(alt / 0.10) ** 2) * (0.6 + 0.4 * np.sin(LO * 2 + 1.0)))[..., None] * 0.5          # o clarão da cidade, mais forte de um lado
    # a galáxia: uma faixa inclinada, de nuvens claras
    D = np.stack([np.cos(LA) * np.sin(LO), np.sin(LA), np.cos(LA) * np.cos(LO)], -1); eixo = np.array([0.35, 0.55, 0.76]); eixo /= np.linalg.norm(eixo)
    dist = np.abs(D @ eixo)
    def ruido(escala, semente):
        r = np.random.default_rng(semente); p = r.random((h // escala + 2, w // escala + 2))
        p = np.kron(p, np.ones((escala, escala)))[:h, :w]
        from PIL import Image as _I, ImageFilter as _F
        return np.asarray(_I.fromarray(np.uint8(p * 255)).filter(_F.GaussianBlur(escala * 0.6)), float) / 255
    nuvem = 0.6 * ruido(64, 1) + 0.3 * ruido(24, 2) + 0.1 * ruido(8, 3)
    faixa = np.exp(-(dist / 0.16) ** 2) * rampa(nuvem, 0.35, 0.75) * rampa(alt, 0.02, 0.3)
    ceu += faixa[..., None] * np.array([0.30, 0.16, 0.50]) + (np.exp(-(dist / 0.05) ** 2) * rampa(alt, 0.05, 0.3))[..., None] * np.array([0.10, 0.10, 0.22])
    # as estrelas: mais onde a galáxia passa; umas poucas grandes
    n = 5200; ys = (np.arccos(rnd.uniform(0.02, 1, n)) / np.pi * h).astype(int).clip(0, h - 1); xs = rnd.integers(0, w, n)
    forca = rnd.uniform(0.3, 1.0, n) ** 3 * (1 + 3 * faixa[ys, xs]); cor = np.stack([rnd.uniform(0.75, 1, n), rnd.uniform(0.8, 1, n), np.ones(n)], 1)
    est = np.zeros((h, w, 3)); np.add.at(est, (ys, xs), cor * forca[:, None] * 2.2)
    grandes = rnd.integers(0, n, 60)
    for k in grandes:
        y, x = ys[k], xs[k]
        for dy, dx, g in ((0, 1, 0.5), (0, -1, 0.5), (1, 0, 0.5), (-1, 0, 0.5), (0, 2, 0.2), (0, -2, 0.2), (2, 0, 0.2), (-2, 0, 0.2)):
            if 0 <= y + dy < h: est[y + dy, (x + dx) % w] += cor[k] * g * 2.5
    ceu += est * rampa(alt, 0.03, 0.2)[..., None]
    ceu = np.where((alt < 0)[..., None], chao * (1 + 2 * np.exp(alt * 8))[..., None], ceu)
    # Radiance .hdr sem compressão (RGBE)
    m = np.maximum(ceu.max(2), 1e-9); e = np.ceil(np.log2(m)).astype(int); f = ceu / (2.0 ** e)[..., None]
    rgbe = np.zeros((h, w, 4), np.uint8); rgbe[..., :3] = np.clip(f * 256, 0, 255).astype(np.uint8); rgbe[..., 3] = np.clip(e + 128, 0, 255)
    um = (rgbe[..., 0] == 1) & (rgbe[..., 1] == 1) & (rgbe[..., 2] == 1); rgbe[um, 0] = 2          # (1, 1, 1, e) é repetição no formato antigo
    rgbe[(rgbe[..., 0] == 2) & (rgbe[..., 1] == 2), 1] = 3                                          # e (2, 2, …) abre uma linha comprimida
    with open(os.path.join(RAIZ, 'assets', 'mapas', 'ceus', 'noite_neon.hdr'), 'wb') as arq:
        arq.write(b"#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y %d +X %d\n" % (h, w)); arq.write(rgbe.tobytes())


if __name__ == '__main__':
    os.makedirs(os.path.join(RAIZ, 'assets', 'mapas', 'fundos'), exist_ok=True)
    os.makedirs(os.path.join(RAIZ, 'assets', 'mapas', 'ceus'), exist_ok=True)
    quais = sys.argv[1:] or ['cidade', 'serra', 'neon', 'ceu_noite', 'picos']
    for q in quais: globals()[q]()
    print('fundos:', ', '.join(quais))
