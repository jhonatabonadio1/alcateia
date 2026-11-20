"""
As imagens de fundo dos mapas de kit (``Mapa.panorama``): o horizonte recortado, com transparência, que vai em quatro
painéis em volta do mapa — a cidade ao longe (prédios em três planos, janelas, antenas, caixas d'água) e a serra do
deserto. Desenhadas aqui (Pillow), sem foto de ninguém: ``python3 tools/mapas/kits/fundos.py``.
"""
import os
import random

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


if __name__ == '__main__':
    os.makedirs(os.path.join(RAIZ, 'assets', 'mapas', 'fundos'), exist_ok=True)
    cidade(); serra(); print('fundos: cidade.png, serra.png')
