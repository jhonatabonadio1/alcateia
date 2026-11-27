"""
A cena de demonstração de um kit do "Low Poly FPS Bundle" — o mapa que o autor montou, lido do ``.umap`` do pacote do Unreal
por ``unreal_umap.py`` (``src/tools/mapas/fps<N>_demo.json``: a matriz de cada peça, já no eixo do Godot) — e o que é comum
aos mapas feitos dela (Cidade, Cidade do Deserto e Obra, 27/11): pôr as peças numa ``Montagem``, dobrar o mapa (a cópia
girada 180° em volta de um ponto), fechar o que fica fora com paredes invisíveis e desenhar a planta para conferir.

As peças são as do kit em FBX que o projeto já tinha (``<kit>.npz``): o nome no Unreal é o do kit com ``SM_`` e a pasta na
frente (``SM_Props_Box_01`` → ``Box_01``), e a caixa de cada malha bate com a do kit (conferido peça a peça pelo
``ExtendedBounds``; as exceções estão em ``ESCALAS``).
"""
import json
import os
import re

import numpy as np

from .kit_fbx import Kit, Peca
from .montagem import Montagem, Item, CAIXA

AQUI = os.path.dirname(os.path.abspath(__file__))
GIRO = np.diag([-1.0, 1.0, -1.0])          # meia volta em torno de Y

# As malhas com esqueleto do Unreal (a casa com a porta que abre, a cancela, a caixa de munição) entram paradas: a peça do kit
ESQUELETO = {'SK_House_07': 'House_07', 'SK_Parking_01': 'Parking_01', 'SK_AmoBox': 'AmoBox_01'}
CUBO = (0.62, 0.78, 0.90)          # a cor do cubo do Unreal: na Estação são as vidraças do piso de cima (chapas de 3 × 2,7 m)
# Onde a malha do Unreal não tem o tamanho da do kit em FBX (medido pela caixa das duas)
ESCALAS = {'fps6': {'House_04': 1.223}}


# Onde o nome no Unreal não é o do FBX: na Estação as paredes ``Wall_NN`` viraram ``Wall1_NN`` e os telhados foram
# numerados de novo, em fila (no FBX há ``Roof_01_2``, ``Roof_04_2`` e ``Roof_07_2``; no Unreal, de 01 a 12). Conferido
# pela caixa de cada malha.
TELHADOS = ['Roof_01', 'Roof_01_2', 'Roof_02', 'Roof_03', 'Roof_04', 'Roof_04_2', 'Roof_05', 'Roof_06', 'Roof_07', 'Roof_07_2', 'Roof_08', 'Roof_09']
NOMES = {'fps5': dict({f'Roof_{i + 1:02d}': n for i, n in enumerate(TELHADOS)}, **{f'Wall1_{i:02d}': f'Wall_{i:02d}' for i in range(1, 16)})}


def nome_no_kit(nome, kit=None):
    if nome in ESQUELETO: return ESQUELETO[nome]
    nome = re.sub(r'^SM_', '', nome)
    nome = re.sub(r'^(Buildings|Props|Nature|Walls|Trains)_', '', nome)
    return NOMES.get(kit, {}).get(nome, nome)


def caixote(nome, uv, caixas):
    """Uma peça feita de caixas lisas ((lo, hi), …) da cor do ponto ``uv`` da textura: o que o kit em FBX não trouxe."""
    pos, nor = [], []
    faces = [((2, 3, 7), (2, 7, 6), (0, 1, 0)), ((0, 4, 6), (0, 6, 2), (0, 0, -1)), ((1, 3, 7), (1, 7, 5), (0, 0, 1)),
             ((0, 2, 3), (0, 3, 1), (-1, 0, 0)), ((4, 5, 7), (4, 7, 6), (1, 0, 0)), ((0, 4, 5), (0, 5, 1), (0, -1, 0))]
    for lo, hi in caixas:
        c = np.asarray(lo, float) + CAIXA * (np.asarray(hi, float) - np.asarray(lo, float))
        for t1, t2, n in faces:
            for t in (t1, t2):
                p = c[list(t)]
                if np.dot(np.cross(p[1] - p[0], p[2] - p[0]), n) < 0: p = p[[0, 2, 1]]
                pos.append(p); nor.append([n] * 3)
    pos = np.array(pos, float)
    return Peca(nome, pos, np.array(nor, float), np.tile(uv, (len(pos), 3, 1)).astype(float))


def palete(K, nome='Pallet_02', camadas=4, topo=1.0, folga=0.01):
    """O palete carregado de blocos, ``Pallet_02`` (o Unreal tem, o FBX não): 1,21 × 1,0 × 1,18 m, a caixa que o
    ``.umap`` dá para ele. É empilhável — a demo da Obra põe um em cima do outro, e latas, pincéis, tijolos, o martelo e
    o carretel em cima. Até 27/11 era um palete vazio de 15 cm, e tudo o que a demo põe em cima dele ficava flutuando
    85 cm (Jhon: "alguns pontos do mapa construção estão com madeiras voando").

    Feito das peças do kit: o palete vazio (``Pallet_01``, 15,7 cm) e, em cima, ``camadas`` camadas de blocos
    (``Brick_01`` e ``Brick_02``, 45 × 23 × 26 cm, um pouco ajustados para fechar a conta), cada camada atravessada em
    relação à de baixo, como se empilha bloco na obra."""
    base = K['Pallet_01']; pos, nor, uv = [base.pos], [base.normal], [base.uv]
    largo, fundo = base.tam[0], base.tam[2]; alto = (topo - base.hi[1]) / camadas
    for c in range(camadas):
        y0 = base.hi[1] + c * alto
        de_lado = c % 2 == 1                                                    # a camada de cima atravessada
        nx, nz = (4, 3) if de_lado else (3, 4)
        for i in range(nx):
            for k in range(nz):
                tijolo = K['Brick_01' if (i + k + c) % 2 else 'Brick_02']
                cx = -largo / 2 + (i + 0.5) * largo / nx; cz = -fundo / 2 + (k + 0.5) * fundo / nz
                comprido, curto = (fundo / nz, largo / nx) if de_lado else (largo / nx, fundo / nz)
                esc = np.array([(comprido - folga) / tijolo.tam[0], (alto - folga) / tijolo.tam[1], (curto - folga) / tijolo.tam[2]])
                G = np.array([[0.0, 0, 1], [0, 1, 0], [-1, 0, 0]]) if de_lado else np.eye(3)
                meio = (tijolo.lo + tijolo.hi) / 2
                p = ((tijolo.pos - meio) * esc) @ G.T + np.array([cx, y0 + alto / 2, cz])
                n = (tijolo.normal / esc) @ G.T; n = n / np.maximum(np.linalg.norm(n, axis=-1, keepdims=True), 1e-9)
                pos.append(p); nor.append(n); uv.append(tijolo.uv)
    K.pecas[nome] = Peca(nome, np.concatenate(pos), np.concatenate(nor), np.concatenate(uv)); return K.pecas[nome]


class Demo:
    """As peças da cena de demonstração de um kit: ``itens`` = [(peça, giro, escala por eixo, posição, nome)]."""

    def __init__(self, kit, arquivo=None):
        self.kit = Kit(kit) if isinstance(kit, str) else kit
        C = json.load(open(arquivo or os.path.join(os.path.dirname(AQUI), self.kit.nome + '_demo.json'), encoding='utf-8'))
        self.efeitos, self.luzes, self.itens, self.faltam = C.get('efeitos', []), C.get('luzes', []), [], {}
        esc = ESCALAS.get(self.kit.nome, {})
        if 'Cube' not in self.kit and any(it['peca'] == 'Cube' for it in C['itens']):          # o cubo do Unreal (1 m, o meio na origem), esticado na cena para fazer maciços
            (u, v), _ = self.kit.uv_da_cor(CUBO); self.kit.pecas['Cube'] = caixote('Cube', (u, v), [((-0.5, -0.5, -0.5), (0.5, 0.5, 0.5))])
        for it in C['itens']:
            nome = nome_no_kit(it['peca'], self.kit.nome)
            if nome not in self.kit:
                self.faltam[nome] = self.faltam.get(nome, 0) + 1; continue
            A = np.array(it['matriz'], float).reshape(3, 4); R = A[:, :3]; D = np.linalg.norm(R, axis=0); G = R / D
            if np.linalg.det(G) < 0: D[0] = -D[0]; G[:, 0] = -G[:, 0]              # peça espelhada: a escala em x fica negativa
            self.itens.append((self.kit[nome], G, D * esc.get(nome, 1.0), A[:, 3].copy(), nome))

    def caixa(self, item):
        """A caixa (lo, hi) de um item no mundo."""
        p, G, D, t, _ = item; q = (p.lo + CAIXA * p.tam) @ (G @ np.diag(D)).T + t
        return q.min(0), q.max(0)


def girar(G, t, pivo):
    """O giro e a posição de uma peça depois da meia volta em torno do ponto ``pivo`` (x, z)."""
    p = np.array([pivo[0], 0.0, pivo[1]]); return GIRO @ G, GIRO @ (t - p) + p


def por(M, peca, G, D, t, nome, colide='padrao', planta=True):
    it = Item(peca, G, np.asarray(D, float).copy(), np.asarray(t, float).copy(), M.padrao(nome) if colide == 'padrao' else colide, planta, nome)
    M.itens.append(it); return it


# ---------------------------------------------------------------- o que fica fora
def dentro_do_poligono(pontos, X, Z, cel=0.5):
    """A mancha (bool, Z por X células de ``cel`` m, de −X a X e de −Z a Z) de dentro do polígono ``pontos`` [(x, z), …]."""
    from PIL import Image, ImageDraw
    W, H = int(round(2 * X / cel)), int(round(2 * Z / cel)); im = Image.new('1', (W, H), 0)
    ImageDraw.Draw(im).polygon([((x + X) / cel, (z + Z) / cel) for x, z in pontos], fill=1)
    return np.asarray(im, bool)


def barreiras(m, dentro, X, Z, altura, cel=0.5, nome='Barreira'):
    """Fora da mancha ``dentro`` (o chão do mapa, visto de cima) vai parede invisível do chão ao teto — ninguém sobe no
    que fecha o mapa nem sai por cima (os corpos ``Barreira…``: o lobo não corre neles, ``Player.IsInvisibleLimit``).
    As células de fora são juntadas em caixas (fileiras iguais empilhadas). Devolve quantas caixas."""
    if 'kit_colisao' not in m.materiais: m.material('kit_colisao', None, (0.5, 0.5, 0.5))
    fora = ~dentro; H, W = fora.shape; feitas = np.zeros_like(fora); n = 0
    for i in range(H):
        j = 0
        while j < W:
            if not fora[i, j] or feitas[i, j]: j += 1; continue
            j1 = j
            while j1 < W and fora[i, j1] and not feitas[i, j1]: j1 += 1
            i1 = i + 1
            while i1 < H and fora[i1, j:j1].all() and not feitas[i1, j:j1].any(): i1 += 1
            feitas[i:i1, j:j1] = True; n += 1
            x0, x1, z0, z1 = j * cel - X, j1 * cel - X, i * cel - Z, i1 * cel - Z
            m.caixa('Colisao', f'{nome}{n}', ((x0 + x1) / 2, altura / 2, (z0 + z1) / 2), (x1 - x0, altura, z1 - z0), 'kit_colisao', oculta=True, minimapa=False)
            j = j1
    return n


def _raios(T, origens, direcao, longe):
    """A que distância cada raio (das ``origens``, todos na mesma ``direcao``) bate nos triângulos ``T``; sem bater, ``longe``."""
    d = np.asarray(direcao, float); e1 = T[:, 1] - T[:, 0]; e2 = T[:, 2] - T[:, 0]; p = np.cross(d, e2); det = (e1 * p).sum(1); ok = np.abs(det) > 1e-9
    inv = np.where(ok, 1.0 / np.where(ok, det, 1.0), 0.0); out = np.full(len(origens), float(longe))
    for k, o in enumerate(np.asarray(origens, float)):
        s_ = o - T[:, 0]; u = (s_ * p).sum(1) * inv; q = np.cross(s_, e1); v = (q @ d) * inv; t = (e2 * q).sum(1) * inv
        bate = ok & (u >= -1e-6) & (v >= -1e-6) & (u + v <= 1 + 1e-6) & (t > 1e-4) & (t < longe)
        if bate.any(): out[k] = t[bate].min()
    return out


def miolo_da_peca(p, margem=0.6, fundo=1.2):
    """Se a peça é um prédio **fechado**, a caixa do miolo dela: (lo, hi). Senão, None.

    O corpo do prédio é a caixa da peça menos o que sobra para fora de cada lado (a escada da frente, o toldo, a sacada,
    o pórtico): de cada lado, a profundidade a que a maioria dos raios, na altura de quem anda, acha a parede. É fechado
    se nenhum raio que aponta para o corpo passa mais de ``fundo`` m dessa parede (não há porta nem vão) e por cima há
    telhado. O miolo fica ``margem`` m para dentro de cada parede e meio metro abaixo do ponto mais baixo do telhado.
    (A casa do kit é uma casca: sem isto o lado de dentro dela é um cômodo fantasma, com o chão do mapa por piso.)"""
    if hasattr(p, '_miolo'): return p._miolo
    p._miolo = None; lo, hi, T = p.lo, p.hi, p.pos
    if len(T) == 0 or hi[0] - lo[0] < 3.0 or hi[2] - lo[2] < 3.0 or hi[1] - lo[1] < 2.5: return None
    medidas = {}
    for eixo, outro in ((0, 2), (2, 0)):
        ao_longo = np.arange(lo[outro] + 0.25, hi[outro] - 0.24, 0.5)
        for sentido, face in ((1.0, lo[eixo] - 0.5), (-1.0, hi[eixo] + 0.5)):
            origens = np.zeros((len(ao_longo) * 3, 3)); origens[:, eixo] = face; origens[:, outro] = np.repeat(ao_longo, 3); origens[:, 1] = np.tile([lo[1] + 0.6, lo[1] + 1.2, lo[1] + 1.8], len(ao_longo))
            d = np.zeros(3); d[eixo] = sentido; t = _raios(T, origens, d, 60.0) - 0.5
            medidas[(eixo, sentido)] = (np.repeat(ao_longo, 3), t, float(np.median(t)))
    c_lo = np.array([lo[0] + medidas[(0, 1.0)][2], lo[1], lo[2] + medidas[(2, 1.0)][2]]); c_hi = np.array([hi[0] - medidas[(0, -1.0)][2], hi[1], hi[2] - medidas[(2, -1.0)][2]])
    if c_hi[0] - c_lo[0] < 2.5 or c_hi[2] - c_lo[2] < 2.5: return None
    for (eixo, sentido), (pos, t, parede) in medidas.items():
        outro = 2 - eixo; no_corpo = (pos > c_lo[outro] + 0.3) & (pos < c_hi[outro] - 0.3)
        if not no_corpo.any() or (t[no_corpo] > parede + fundo).any(): return None            # um vão: dá para entrar
    xs = np.arange(c_lo[0] + margem, c_hi[0] - margem + 1e-6, 0.75); zs = np.arange(c_lo[2] + margem, c_hi[2] - margem + 1e-6, 0.75)
    if len(xs) < 2 or len(zs) < 2: return None
    origens = np.array([[x, hi[1] + 1.0, z] for x in xs for z in zs]); t = _raios(T, origens, (0.0, -1.0, 0.0), 500.0)
    if (t > 400.0).any(): return None                                                         # sem telhado em algum ponto
    topo = float((hi[1] + 1.0 - t).min()) - 0.5
    if topo - lo[1] < 1.5: return None
    p._miolo = (np.array([c_lo[0] + margem, lo[1], c_lo[2] + margem]), np.array([c_hi[0] - margem, topo, c_hi[2] - margem]))
    p._corpo = (np.array([c_lo[0], lo[1], c_lo[2]]), np.array([c_hi[0], topo, c_hi[2]]))          # as paredes do corpo, sem o recuo
    return p._miolo


def fechar_predios(m, M, fora, X, Z, altura, quais, cel=0.5, toca=0.03, inicio=0):
    """O miolo dos prédios fechados (as peças que ``quais(nome)`` aceita e ``miolo_da_peca`` confirma): uma caixa de
    colisão escondida por dentro da casca, 60 cm para dentro de cada parede (``miolo…``) — quem olha de cima não acha
    "rua" dentro do prédio, e nada atravessa a casa por dentro. No prédio que **fecha o mapa** (o que encosta no lado
    de fora, ``fora``) vai ainda, do telhado para cima até o teto, uma ``Barreira…`` do tamanho da peça: ninguém
    fica em cima nem passa por cima dele para fora (a regra de ``montagem.barrar``, de 20/11). Ela começa meio metro
    abaixo do ponto mais baixo do telhado: a fachada, a sacada, a escada e o beiral ficam livres. Muro e parede fina
    não entram aqui: quem os fecha é a mancha de fora (``barreiras``). Devolve (quantas barreiras, quantos miolos)."""
    import math
    if 'kit_colisao' not in m.materiais: m.material('kit_colisao', None, (0.5, 0.5, 0.5))
    H, W = fora.shape; nb = nm = 0

    def caixa(it, lo, hi):
        """(centro no chão, largura, fundo, base, topo, giro) da caixa (lo, hi) da peça, no mundo."""
        c = it.pontos(lo + CAIXA * (hi - lo)); yaw = math.atan2(-it.G[2, 0], it.G[0, 0])
        ex = np.array([math.cos(yaw), 0.0, -math.sin(yaw)]); ez = np.array([math.sin(yaw), 0.0, math.cos(yaw)]); u, v = c @ ex, c @ ez
        centro = ex * (u.max() + u.min()) / 2 + ez * (v.max() + v.min()) / 2
        rot = ((math.cos(yaw), 0.0, math.sin(yaw)), (0.0, 1.0, 0.0), (-math.sin(yaw), 0.0, math.cos(yaw)))
        return centro, float(u.max() - u.min()), float(v.max() - v.min()), float(c[:, 1].min()), float(c[:, 1].max()), rot

    for it in M.itens:
        if it.colide != 'malha' or not quais(it.nome): continue
        dentro = miolo_da_peca(it.peca)
        if dentro is None: continue
        centro, lu, lv, base, topo, rot = caixa(it, *dentro)
        if lu < 1.0 or lv < 1.0 or topo - base < 1.5 or base > 1.0: continue
        base = max(base, 0.05); nm += 1
        m.caixa('Colisao', f'miolo{nm}', (float(centro[0]), (base + topo) / 2, float(centro[2])), (lu, topo - base, lv), 'kit_colisao', rot=rot, oculta=True, minimapa=False)
        q = it.cantos(); i0, i1 = max(int((q[:, 2].min() + Z) / cel), 0), min(int((q[:, 2].max() + Z) / cel) + 1, H); j0, j1 = max(int((q[:, 0].min() + X) / cel), 0), min(int((q[:, 0].max() + X) / cel) + 1, W)
        if i1 > i0 and j1 > j0 and fora[i0:i1, j0:j1].mean() >= toca and topo < altura - 1.0:
            centro, lu, lv, _, _, rot = caixa(it, it.peca.lo, it.peca.hi); nb += 1          # (acima do telhado, a caixa inteira da peça: o beiral e a cornija também)
            m.caixa('Colisao', f'Barreira{inicio + nb}', (float(centro[0]), (topo + altura) / 2, float(centro[2])), (lu, altura - topo, lv), 'kit_colisao', rot=rot, oculta=True, minimapa=False)
    return nb, nm


def bolsoes(fora, ocupado, base, lo, cel=0.25):
    """Os bolsões: o chão livre a que ninguém chega a pé (nem espremido: 25 cm de folga) e que encosta no lado de fora —
    o fundo de um beco fechado por cerca, a faixa entre uma casa da beira e o muro. São lado de fora também: a mancha
    ``fora`` (células de 50 cm) volta com eles. Sem isto ficava ali um vão que se vê e onde não se entra, com a parede
    invisível passando pelo meio do ar (o teste TiroDeCima). ``ocupado``: a grade de 25 cm; ``base``: de onde se anda.
    Serve ao mapa fechado por **prédios** (a Cidade: o bolsão é o lado de dentro da casca das casas da beira). No mapa
    fechado por parede fina (a Estação, o Deserto) não se usa: ali o chão a que só o lobo chega, atrás de um trem,
    também encosta na parede de fora, e viraria barreira."""
    from .montagem import Grade
    g = Grade(ocupado, lo, cel, folga=0.25); H, W = fora.shape
    chao, _ = alcancavel(g, base); anda = chao.reshape(H, 2, W, 2).any((1, 3))
    livre = ~ocupado.reshape(H, 2, W, 2).all((1, 3)) & ~fora
    resto = livre & ~cresce(anda, 1); perto = cresce(fora, 2); novo = fora.copy(); rot = np.zeros((H, W), bool)
    for i0, j0 in zip(*np.nonzero(resto & perto)):
        if rot[i0, j0]: continue
        comp = [(i0, j0)]; rot[i0, j0] = True; k = 0
        while k < len(comp):
            i, j = comp[k]; k += 1
            for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                a_, b_ = i + di, j + dj
                if 0 <= a_ < H and 0 <= b_ < W and resto[a_, b_] and not rot[a_, b_]: rot[a_, b_] = True; comp.append((a_, b_))
        for i, j in comp: novo[i, j] = True
    return novo


def assentar(M, quais, chao=0.0, ate=0.5, cel=0.25):
    """Baixa até o que está embaixo as peças (``quais``: começo do nome) que flutuam até ``ate`` m: na demo do Unreal elas
    estavam num terreno com relevo (o *landscape*, que não vem para cá) e aqui o chão é plano — a palmeira, a pedra e a
    planta em cima de um montinho ficavam no ar (Deserto, 27/11). O apoio é a superfície mais alta debaixo dos pontos
    mais baixos da peça: outra peça ou o ``chao``. Devolve quantas desceram."""
    tris = [(k, it.pontos(it.peca.pos.reshape(-1, 3)).reshape(-1, 3, 3)) for k, it in enumerate(M.itens) if len(it.peca.pos)]
    T = np.concatenate([t for _, t in tris]); D = np.concatenate([np.full(len(t), k) for k, t in tris]); lo = T.min(1); hi = T.max(1)

    def apoio(o, ignora):
        sel = (lo[:, 0] <= o[0]) & (hi[:, 0] >= o[0]) & (lo[:, 2] <= o[2]) & (hi[:, 2] >= o[2]) & (lo[:, 1] < o[1]) & (D != ignora)
        if not sel.any(): return chao
        t = T[sel]; d = np.array([0.0, -1.0, 0.0]); e1 = t[:, 1] - t[:, 0]; e2 = t[:, 2] - t[:, 0]; p = np.cross(d, e2); det = (e1 * p).sum(1); ok = np.abs(det) > 1e-12
        inv = np.where(ok, 1 / np.where(ok, det, 1), 0); s_ = o - t[:, 0]; u = (s_ * p).sum(1) * inv; q = np.cross(s_, e1); v = (q @ d) * inv; tt = (e2 * q).sum(1) * inv
        bate = ok & (u >= -1e-4) & (v >= -1e-4) & (u + v <= 1.0001) & (tt > 0)
        return max(chao, float(o[1] - tt[bate].min())) if bate.any() else chao

    n = 0
    for k, it in enumerate(M.itens):
        if not it.nome.startswith(quais) or not len(it.peca.pos): continue
        V = it.pontos(it.peca.pos.reshape(-1, 3)); base = V[:, 1].min()
        pes = V[V[:, 1] < base + 0.03]; pes = pes[np.unique(np.linspace(0, len(pes) - 1, min(len(pes), 9)).astype(int))]
        folga = base - max(apoio(q + np.array([0.0, 0.01, 0.0]), k) for q in pes)
        if 0.03 < folga <= ate:
            it.t = it.t - np.array([0.0, folga, 0.0]); n += 1
            if hasattr(it, '_aabb'): del it._aabb
    return n


def cresce(mancha, raio):
    """A mancha (bool) crescida de ``raio`` células para todo lado (quadrado)."""
    from PIL import Image, ImageFilter
    if raio <= 0: return mancha.copy()
    return np.asarray(Image.fromarray(mancha.astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(2 * int(raio) + 1))) > 0


def alaga(semente, onde):
    """Tudo de ``onde`` que se alcança a partir de ``semente`` andando de célula em célula (sem diagonal)."""
    v = semente & onde
    while True:
        n = v.copy(); n[1:] |= v[:-1]; n[:-1] |= v[1:]; n[:, 1:] |= v[:, :-1]; n[:, :-1] |= v[:, 1:]; n &= onde
        if n.sum() == v.sum(): return v
        v = n


def fora_do_anel(ocupado, cel, vao=2.5, entra=0.4, borda=2.0):
    """O lado de fora de um mapa fechado por um anel de prédios, muros e cercas, com frestas entre eles: o que se alcança
    da beira da grade ``ocupado`` (True = há colisão na altura de quem anda) sem passar por fresta de menos de ``2 × vao``
    metros. A mancha entra ``entra`` m no que fecha (a parede invisível fica rente à face de fora do prédio: a de dentro,
    a que dá para o chão do mapa, continua sendo a do prédio) e toma sempre a faixa de ``borda`` m junto ao limite."""
    r = int(round(vao / cel)); H, W = ocupado.shape
    beira = np.zeros_like(ocupado); beira[0, :] = beira[-1, :] = beira[:, 0] = beira[:, -1] = True
    fora = cresce(alaga(beira, ~cresce(ocupado, r)), r + int(round(entra / cel)))
    b = max(1, int(round(borda / cel))); fora[:b, :] = fora[-b:, :] = True; fora[:, :b] = fora[:, -b:] = True
    return fora


def nascer_em(m, grade, centro, time, quantos=8, passo=1.6, y=1.2):
    """``quantos`` pontos de nascimento do ``time`` em volta de ``centro`` (x, z), em chão livre — para o mapa que não é
    simétrico (cada time tem o seu lugar; ``montagem.nascer`` põe o outro time no ponto oposto). Devolve o primeiro."""
    cx, cz = centro; cand = sorted(((i * passo, j * passo) for i in range(-5, 6) for j in range(-5, 6)), key=lambda d: d[0] ** 2 + d[1] ** 2)
    pontos = [(round(cx + dx, 2), round(cz + dz, 2)) for dx, dz in cand if grade.anda(cx + dx, cz + dz)][:quantos]
    if len(pontos) < quantos: raise ValueError(f'só {len(pontos)} pontos livres em volta de {centro}')
    for x, z in pontos: m.spawn(time, x, z, y)
    return pontos[0]


def alcancavel(g, de):
    """O chão que se alcança a pé de ``de`` (x, z) na grade ``g``: (a mancha, a função que dá o ponto dela mais perto)."""
    L = g.livre; H, W = L.shape; a = g.celula(*g.perto(*de)); visto = np.zeros_like(L); visto[a] = True; fila = [a]
    while fila:
        i, j = fila.pop()
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (i + di, j + dj)
            if 0 <= n[0] < H and 0 <= n[1] < W and L[n] and not visto[n]: visto[n] = True; fila.append(n)
    ii, jj = np.nonzero(visto)

    def perto(x, z):
        i, j = g.celula(x, z); k = int(np.argmin((ii - i) ** 2 + (jj - j) ** 2)); return g.ponto(int(ii[k]), int(jj[k]))
    return visto, perto


# ---------------------------------------------------------------- a planta para conferir
def vista_de_cima(M, caminho, area, e=8.0, ate=1e9, de=-1e9, marcas=(), linhas=(), grade=10):
    """A montagem vista de cima, na cor de cada face (a mais alta por cima), com a régua em metros: para escolher onde
    cortar, onde nasce cada time e por onde passam as rotas. ``area`` = (x0, z0, x1, z1); ``e``: pontos por metro;
    ``de``/``ate``: só o que está nessa faixa de altura (um andar de cada vez)."""
    from PIL import Image, ImageDraw
    tex = np.asarray(Image.open(M.kit.textura).convert('RGB'), float); TH, TW = tex.shape[:2]
    x0, z0, x1, z1 = area; W, H = int((x1 - x0) * e), int((z1 - z0) * e)
    img = np.full((H, W, 3), 40.0); alto = np.full((H, W), -1e9)
    for it in M.itens:
        p = it.peca
        if not len(p.pos): continue
        pos = it.pontos(p.pos.reshape(-1, 3)).reshape(-1, 3, 3)
        lado = np.maximum.reduce([np.linalg.norm(pos[:, 1] - pos[:, 0], axis=1), np.linalg.norm(pos[:, 2] - pos[:, 0], axis=1), np.linalg.norm(pos[:, 2] - pos[:, 1], axis=1)])
        passos = np.minimum((lado * e * 1.5).astype(int) + 2, 500)
        c = p.uv.mean(1); cor = tex[np.clip((c[:, 1] % 1 * TH).astype(int), 0, TH - 1), np.clip((c[:, 0] % 1 * TW).astype(int), 0, TW - 1)]
        nrm = np.cross(pos[:, 1] - pos[:, 0], pos[:, 2] - pos[:, 0]); nrm = nrm / np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-9)
        cor = cor * (0.55 + 0.45 * np.abs(nrm[:, 1]))[:, None]
        for n in sorted(set(passos)):
            sel = passos == n; a, b = np.meshgrid(np.linspace(0, 1, n), np.linspace(0, 1, n)); mk = a + b <= 1.0001; a, b = a[mk], b[mk]
            q = (pos[sel][:, None, 0] + a[None, :, None] * (pos[sel][:, None, 1] - pos[sel][:, None, 0]) + b[None, :, None] * (pos[sel][:, None, 2] - pos[sel][:, None, 0])).reshape(-1, 3)
            cc = np.repeat(cor[sel], len(a), axis=0)
            ix = ((q[:, 0] - x0) * e).astype(int); iz = ((q[:, 2] - z0) * e).astype(int)
            ok = (ix >= 0) & (ix < W) & (iz >= 0) & (iz < H) & (q[:, 1] <= ate) & (q[:, 1] >= de)
            ix, iz, y, cc = ix[ok], iz[ok], q[ok, 1], cc[ok]
            o = np.argsort(y, kind='stable'); ix, iz, y, cc = ix[o], iz[o], y[o], cc[o]
            mk = y >= alto[iz, ix]; alto[iz[mk], ix[mk]] = y[mk]; img[iz[mk], ix[mk]] = cc[mk]
    im = Image.fromarray(img.clip(0, 255).astype(np.uint8)); d = ImageDraw.Draw(im)
    def px(x, z): return ((x - x0) * e, (z - z0) * e)
    for x in range(int(np.ceil(x0 / grade)) * grade, int(x1) + 1, grade):
        d.line([px(x, z0), px(x, z1)], fill=(255, 255, 255) if x == 0 else (110, 110, 110)); d.text((px(x, z0)[0] + 2, 2), str(x), fill=(255, 255, 0))
    for z in range(int(np.ceil(z0 / grade)) * grade, int(z1) + 1, grade):
        d.line([px(x0, z), px(x1, z)], fill=(255, 255, 255) if z == 0 else (110, 110, 110)); d.text((2, px(x0, z)[1] + 2), str(z), fill=(255, 255, 0))
    for k, linha in enumerate(linhas):
        cor_ = [(255, 60, 60), (60, 220, 60), (80, 140, 255), (255, 190, 40), (220, 90, 255), (40, 220, 220), (255, 255, 255), (255, 120, 180)][k % 8]
        d.line([px(*p) for p in linha], fill=cor_, width=2)
    for x, z, cor_ in marcas:
        X_, Y_ = px(x, z); d.ellipse([X_ - 4, Y_ - 4, X_ + 4, Y_ + 4], fill=cor_, outline=(0, 0, 0))
    im.save(caminho); return alto
