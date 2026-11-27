"""
O detalhe das superfícies dos mapas de paleta: diz de que material é cada face e grava isso no .glb.

Os kits "Low Poly FPS" (e o Armazém) pintam tudo com uma paleta: a face tem uma cor lisa e nada mais. O material
``assets/mapas/detalhe/superficie.gdshader`` mantém a cor e soma o grão do material (relevo, aspereza, um claro-escuro
leve, metal e vidro com reflexo). Para isso ele precisa saber **de que material é cada face** — a paleta não diz: o
mesmo cinza é asfalto, parede e lataria. Este arquivo decide, face a face, por três coisas:

  1. **a peça** (o nome dela no kit: ``House_01`` é prédio, ``Car_01`` é veículo, ``Pallet_02`` é madeira…);
  2. **a cor** da face na paleta (num prédio, o azul-claro é vidro; o marrom que não é a parede é madeira…);
  3. **para onde a face olha** (num prédio, o escuro virado para cima é telhado; em pé, é esquadria de metal).

O número do material vai no vermelho da cor do vértice (``COLOR_0``, material ÷ 255) — a lista é ``CLASSES``, a mesma
ordem das tabelas do shader. ``remendar(glb)`` faz isso num .glb já pronto (os mapas de kit chamam no fim de
``Montagem.glb``; o Armazém, que vem do Blender, em ``tools/mapas/armazem*.py``), e ``ligar(glb, material)`` escreve no
``.glb.import`` que o material do modelo é o ``.tres`` de ``assets/mapas/detalhe``.

    python3 tools/mapas/detalhar.py            # todos os mapas de paleta que estão no projeto
    python3 tools/mapas/detalhar.py cidade     # um só

Para corrigir uma peça que saiu com o material errado: uma linha em ``PECAS`` (a primeira regra que casa com o nome
vale) ou, se for só daquela peça, em ``FIXAS``. Para ver o que foi decidido, ligue ``ver_classes`` no material
(``assets/mapas/detalhe/<kit>.tres``): cada material aparece de uma cor.
"""
import io
import json
import os
import re
import struct

import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(AQUI)))

# A ordem é o número do material (o mesmo das tabelas de assets/mapas/detalhe/superficie.gdshader).
CLASSES = ['liso', 'asfalto', 'piso', 'concreto', 'reboco', 'tijolo', 'metal_pintado', 'metal_cru', 'vidro', 'madeira',
           'folhagem', 'areia', 'rocha', 'tecido', 'plastico', 'adobe', 'terra', 'louca', 'papel', 'borracha',
           'telhado', 'faixa', 'lataria', 'agua', 'calcamento', 'brita', 'tronco']
C = {n: i for i, n in enumerate(CLASSES)}


# ---------------------------------------------------------------- as faces de uma peça
class Faces:
    """As faces (triângulos) de uma peça, com o que as regras olham: a cor na paleta (h em graus, s e v de 0 a 1), para
    onde olham e o tamanho."""
    def __init__(self, cor, normal, pos):
        self.n = len(cor); mx, mn = cor.max(1), cor.min(1); d = mx - mn
        r, g, b = cor[:, 0], cor[:, 1], cor[:, 2]; h = np.zeros(self.n); ok = d > 1e-6
        with np.errstate(divide='ignore', invalid='ignore'):
            hr = ((g - b) / d) % 6; hg = (b - r) / d + 2; hb = (r - g) / d + 4
        h = np.where(mx == r, hr, np.where(mx == g, hg, hb)); self.h = np.where(ok, h * 60.0, 0.0)
        self.s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0.0); self.v = mx; self.cor = cor
        ny = normal[:, 1]; self.cima = ny > 0.6; self.baixo = ny < -0.6; self.pe = np.abs(ny) < 0.4
        self.area = np.linalg.norm(np.cross(pos[:, 1] - pos[:, 0], pos[:, 2] - pos[:, 0]), axis=1) / 2
        self.y = pos[:, :, 1].mean(1); self.meio = pos.mean(1)
        # as famílias de cor
        self.cinza = self.s < 0.13
        self.vidro = (self.h >= 165) & (self.h <= 232) & (self.s >= 0.14) & (self.s <= 0.5) & (self.v >= 0.58)
        self.marrom = (self.h >= 10) & (self.h <= 44) & (self.s >= 0.33) & (self.v >= 0.1) & (self.v <= 0.72)
        self.bege = (self.h >= 12) & (self.h <= 45) & (self.s >= 0.1) & (self.s < 0.33) & (self.v >= 0.4)
        self.verde = (self.h >= 55) & (self.h <= 165) & (self.s >= 0.28)
        self.escuro = (self.v < 0.33) & (self.s < 0.35)
        self.preto = (self.v < 0.2) & (self.s < 0.4)
        self.claro = self.cinza & (self.v > 0.7)
        self.luz = (self.h >= 32) & (self.h <= 52) & (self.s > 0.9) & (self.v > 0.95)          # o amarelo aceso das lâmpadas

    def dominante(self, onde=None):
        """As faces da cor que mais cobre (em área) entre as de ``onde`` (padrão: as em pé): a parede de um prédio."""
        onde = self.pe if onde is None else onde
        if not onde.any(): onde = np.ones(self.n, bool)
        chave = (self.cor * 24).astype(int) @ np.array([1, 64, 4096]); soma = {}
        for k, a in zip(chave[onde], self.area[onde]): soma[k] = soma.get(k, 0.0) + a
        return chave == max(soma, key=soma.get)


def _tudo(f, classe):
    return np.full(f.n, C[classe])


# ---------------------------------------------------------------- as regras por tipo de peça
def predio(f, parede=None):
    """Prédio, casa, parede com janela: a cor que mais cobre as faces em pé é a parede (tijolo se for de barro, concreto se
    for cinza, reboco no resto); o azul-claro em pé é vidro; o marrom que sobra é madeira (porta, caixilho); o escuro é
    telhado (para cima) ou esquadria de metal (em pé)."""
    out = _tudo(f, 'reboco'); dom = f.dominante()
    if parede is None:
        i = int(np.argmax(dom)); h, s, v = f.h[i], f.s[i], f.v[i]
        parede = 'tijolo' if (h <= 45 and s >= 0.3) else ('concreto' if s < 0.13 and v < 0.66 else 'reboco')
    out[f.cima] = C['piso']                                 # laje, marquise, peitoril
    out[f.cinza & ~f.claro & ~f.cima] = C['concreto']
    out[f.cinza & f.cima & (f.v < 0.5)] = C['telhado']
    out[f.escuro & ~f.cima] = C['metal_pintado']
    out[f.escuro & f.cima] = C['telhado']
    out[~f.cinza & ~f.bege & f.cima] = C['telhado']
    resto = f.marrom & ~dom
    if f.area[resto].sum() > 0.2 * f.area.sum():          # marrom demais para ser porta e caixilho: outro pano de tijolo, ou o telhado
        out[resto] = C['tijolo']; out[resto & f.cima] = C['telhado']
    else: out[resto] = C['madeira']
    out[f.verde] = C['folhagem']
    out[dom] = C[parede]
    out[dom & f.cima & f.escuro] = C['telhado']
    out[f.vidro & ~f.cima & ~f.baixo] = C['vidro']
    return out


def predio_da_estacao(f):
    """As paredes da estação: a faixa escura da parede é pintura (reboco), não esquadria de metal."""
    out = predio(f); out[f.escuro & f.pe & ~f.vidro] = C['reboco']; return out


def casa_de_barro(f):
    """As casas, os muros e as escadas da cidade do deserto: barro (adobe) em tudo o que é bege; madeira no marrom."""
    out = _tudo(f, 'adobe')
    out[f.marrom & (f.s >= 0.5)] = C['madeira']
    out[f.escuro] = C['metal_pintado']
    out[f.vidro & f.cima] = C['agua']          # a água do poço
    out[f.verde] = C['folhagem']
    return out


def veiculo(f):
    out = _tudo(f, 'lataria')
    out[f.preto] = C['borracha']
    out[f.cinza & (f.v >= 0.4) & (f.v < 0.7)] = C['metal_cru']
    out[f.vidro] = C['vidro']
    return out


def trem(f):
    out = _tudo(f, 'metal_pintado')
    out[f.preto] = C['metal_cru']
    out[f.vidro] = C['vidro']
    return out


def madeira(f):
    """Caixote, palete, banco, mesa, barril de aduelas: madeira; o cinza e o preto são ferragem."""
    out = _tudo(f, 'madeira')
    out[f.cinza | f.escuro] = C['metal_pintado']
    out[f.cinza & (f.v >= 0.4) & (f.v < 0.7)] = C['metal_cru']
    out[(f.v > 0.75) & (f.s > 0.35) & ~f.marrom] = C['tecido']          # a lona de cor viva por cima do caixote
    return out


def banca(f):
    """Barraca de feira, tenda: madeira na armação, pano no que é de cor viva ou claro."""
    out = madeira(f)
    out[~f.marrom & ~f.cinza & ~f.escuro] = C['tecido']
    out[f.claro] = C['tecido']
    return out


def metal(f):
    """Poste, placa, grade, andaime, máquina: pintado no que tem cor, cru no cinza; o azul-claro é visor ou vidro."""
    out = _tudo(f, 'metal_pintado')
    out[f.cinza & (f.v >= 0.38) & (f.v < 0.7)] = C['metal_cru']
    out[f.marrom & (f.h >= 18) & (f.s >= 0.45) & (f.v < 0.66)] = C['madeira']
    out[f.vidro & ~f.cima] = C['vidro']
    return out


def luminaria(f):
    out = metal(f); out[f.luz] = C['liso']; return out          # a lâmpada fica como está


def poste_de_madeira(f):
    out = _tudo(f, 'madeira'); out[f.cinza | f.escuro] = C['metal_pintado']; return out


def planta(f):
    out = _tudo(f, 'folhagem')
    out[f.marrom] = C['tronco']
    out[f.cinza | f.bege] = C['concreto']
    out[f.marrom & f.cima & (f.y < 0.4)] = C['terra']
    return out


def canteiro(f):
    out = _tudo(f, 'concreto'); out[f.marrom] = C['terra']; out[f.verde] = C['folhagem']; return out


def canteiro_de_barro(f):
    out = _tudo(f, 'adobe'); out[f.marrom & f.cima] = C['terra']; out[f.verde] = C['folhagem']; return out


def vaso(f):
    out = _tudo(f, 'louca'); out[f.verde] = C['folhagem']; out[f.marrom & f.cima] = C['terra']; return out


def pano(f):
    return _tudo(f, 'tecido')


def roda(f):
    out = _tudo(f, 'borracha'); out[~f.preto] = C['metal_cru']; return out


def trilho(f):
    out = _tudo(f, 'metal_cru'); out[f.marrom] = C['madeira']; return out


def morro(f):
    out = _tudo(f, 'areia'); out[~f.cima] = C['rocha']; out[f.claro] = C['rocha']; return out


def fonte(f):
    out = _tudo(f, 'rocha'); out[f.vidro] = C['agua']; return out


def lixo(f):
    out = _tudo(f, 'plastico'); out[f.marrom] = C['papel']; out[f.cinza & (f.v > 0.4)] = C['metal_cru']; return out


def cacamba(f):
    out = _tudo(f, 'metal_pintado'); out[f.marrom] = C['papel']; out[f.preto] = C['plastico']; return out


def saco_de_cimento(f):
    return _tudo(f, 'papel')


def lata(f):
    out = _tudo(f, 'metal_pintado'); out[f.cinza] = C['metal_cru']; return out


def ferramenta(f):
    out = _tudo(f, 'madeira'); out[f.cinza | f.escuro] = C['metal_cru']; out[f.bege | f.claro] = C['plastico']; return out


def bobina(f):
    out = _tudo(f, 'madeira'); out[f.preto] = C['borracha']; return out


def posto(f):
    """O posto de gasolina: a cobertura, os pilares e as bombas são de chapa pintada (20/11: as bombas saíam de tijolo,
    porque a cor que mais cobre o posto é o vermelho-barro da marca); a ilha de concreto, o visor de vidro."""
    out = _tudo(f, 'metal_pintado')
    out[f.cinza & (f.v >= 0.38) & f.cima] = C['piso']
    out[f.cinza & (f.v >= 0.38) & (f.v < 0.7) & ~f.cima] = C['metal_cru']
    out[f.vidro & ~f.cima] = C['vidro']
    return out


def escada_de_concreto(f):
    return _tudo(f, 'concreto')


def qualquer(f):
    """Peça que nenhuma regra conhece: pela cor."""
    out = _tudo(f, 'reboco')
    out[f.cinza] = C['concreto']; out[f.escuro] = C['metal_pintado']; out[f.marrom] = C['madeira']; out[f.verde] = C['folhagem']
    out[f.vidro & f.pe] = C['vidro']
    return out


def so(classe):
    return lambda f: _tudo(f, classe)


def predio_de(parede):
    return lambda f: predio(f, parede)



def scifi(f):
    """O kit da cidade do futuro (o pacote do Unity, ``unity_kit.py``): a textura é um atlas de quatro quadrantes — a
    paleta, os anúncios, o piso e os prédios de fundo com os LEDs —, e o quadrante diz o que a face é. O que acende
    (anúncio, LED, janela ao longe) fica liso: a luz é da textura ``_luz``."""
    u, v = f.uv[:, 0] % 1, f.uv[:, 1] % 1; cl = np.full(f.n, C['metal_pintado']); n = f.nome
    paleta = (u < 0.5) & (v < 0.5); piso = (u < 0.5) & (v >= 0.5)
    cl[~paleta & ~piso] = C['liso']
    cl[piso] = np.where(f.s[piso] > 0.45, C['faixa'], np.where(f.v[piso] < 0.40, C['asfalto'], C['piso']))
    cl[(u >= 0.8125) & (u < 0.875) & (v >= 0.875) & (v < 0.9375)] = C['vidro']                 # o material Glass
    cl[(u >= 0.875) & (u < 0.9375) & (v >= 0.875) & (v < 0.9375)] = C['concreto']              # o cinza dos prédios de fundo
    if 'Nature' in n:
        cl[paleta] = np.where(f.verde, C['folhagem'], np.where(f.marrom, C['tronco'], np.where(f.cinza, C['rocha'], C['terra'])))[paleta]
        if 'Flowerbed' in n or 'Bio' in n: cl[paleta & f.cinza] = C['concreto']
    elif 'Car_' in n or 'Drone' in n:
        cl[paleta] = np.where(f.preto, C['borracha'], np.where(f.vidro | ((f.h > 190) & (f.h < 250) & (f.v < 0.45)), C['vidro'], C['lataria']))[paleta]
    elif 'Floor' in n:
        cl[paleta] = np.where(f.cinza & (f.v < 0.4), C['asfalto'], C['piso'])[paleta]
    elif re.match(r'SM_(Building|BigBuilding|BG_|BigFence|Buildings_)', n):
        cl[paleta] = np.where(f.vidro, C['vidro'], np.where(f.cinza & (f.v > 0.5), C['concreto'], np.where(f.marrom, C['madeira'], C['metal_pintado'])))[paleta]
    else:
        cl[paleta] = np.where(f.marrom, C['madeira'], np.where(f.cinza & (f.v < 0.45), C['metal_cru'], np.where(f.vidro, C['vidro'], np.where(f.cinza, C['metal_pintado'], C['plastico']))))[paleta]
        if 'Food' in n: cl[paleta] = C['plastico']
    return cl


def chapa_nomeada(f):
    """As chapas do Setor Zero (``src/tools/mapas/setor_zero.py``): o nome diz o material — ``sz_<classe>`` ou
    ``sz_<classe>_<variante>`` (``sz_concreto_claro``, ``sz_metal_pintado_escuro``). O LED (``sz_led_…``) fica liso: quem
    acende é a textura ``scifi_luz``."""
    resto = f.nome[3:]
    classe = max((c for c in CLASSES if resto == c or resto.startswith(c + '_')), key=len, default='liso')
    return np.full(f.n, C[classe])


# A primeira regra que casa com o começo do nome vale. (kit, expressão, regra); kit None: todos.
PECAS = [
    ('scifi', r'sz_', chapa_nomeada),
    ('scifi', r'', scifi),
    # ---- as chapas que os mapas criam (Kit.chapa): o nome é o próprio material
    (None, r'asfalto$', so('asfalto')), (None, r'calcada$', so('piso')), (None, r'meiofio$', so('concreto')), (None, r'(branco|amarelo)$', so('faixa')),
    (None, r'poca_', so('agua')), (None, r'lama_', so('terra')), (None, r'monte_areia', so('areia')), (None, r'monte_brita', so('brita')),
    (None, r'(piso|concreto|laje|chao)$', so('piso')), (None, r'brita$', so('brita')), (None, r'escuro$', so('metal_pintado')),
    ('fps2', r'(muralha)$', so('adobe')), ('fps2', r'pedra$', so('calcamento')),
    ('fps6', r'muro$', so('tijolo')), ('fps6', r'parede$', so('concreto')), (None, r'(muro|parede)$', so('reboco')),
    # ---- deserto (fps2)
    ('fps2', r'(House|Wall|Stairs|Well|Tile)', casa_de_barro), ('fps2', r'FlowerBed', canteiro_de_barro),
    ('fps2', r'(Hill|Mountain)', morro), ('fps2', r'Rock', so('rocha')), ('fps2', r'Fountain', fonte),
    ('fps2', r'(Carpet|Bag|Flag|Dryer)', pano), ('fps2', r'(Tent|Market)', banca),
    ('fps2', r'Pot', vaso), ('fps2', r'(Jug|Cup|Plate)', so('louca')), ('fps2', r'Food', so('plastico')), ('fps2', r'Block', so('adobe')),
    ('fps2', r'Garbage', so('papel')), ('fps2', r'Barrel', madeira),
    # ---- obra (fps6)
    ('fps6', r'House', predio_de('concreto')), ('fps6', r'Wall_0[6789]', so('tijolo')), ('fps6', r'Wall', predio_de('concreto')),
    ('fps6', r'Stairs', escada_de_concreto), ('fps6', r'Brick', so('tijolo')), ('fps6', r'Cement', saco_de_cimento),
    ('fps6', r'(Dye)', lata), ('fps6', r'(Brush|Hammer)', ferramenta), ('fps6', r'CableReel', bobina),
    ('fps6', r'WC', so('plastico')), ('fps6', r'Door', madeira), ('fps6', r'ConstructionMat', so('metal_cru')),
    ('fps6', r'MetalTile', so('metal_pintado')),
    # ---- estação (fps5)
    ('fps5', r'Train', trem), ('fps5', r'Railway', trilho), ('fps5', r'(Roof|SmallRoof)', so('telhado')),
    ('fps5', r'Wall2?_|WallCorner|Building|HousePart|WallDecor|Window', predio_da_estacao), ('fps5', r'Stairs', escada_de_concreto),
    ('fps5', r'Platform', metal), ('fps5', r'Barrier', so('concreto')),
    # ---- mato (fps3) e o que vale para todos
    (None, r'(Tree|PalmTree|Plant|Grass|Mushroom)', planta), (None, r'Log', so('madeira')), (None, r'FlowerBed', canteiro),
    (None, r'Rock', so('rocha')),
    (None, r'Gas_Station', posto),
    (None, r'(House|Office|Bank|Church|AutoService|Building|Construction_)', predio),
    (None, r'(Car_|OldCar)', veiculo), (None, r'Wheel', roda),
    (None, r'RoadPart', so('concreto')), (None, r'Mud', so('terra')), (None, r'Luke', so('metal_cru')),
    (None, r'PowerLines_01', poste_de_madeira), (None, r'PowerLines', so('borracha')),
    (None, r'(Box|Pallet|WoodenPlank|Bench|Table|Furniture)', madeira),
    (None, r'Garbage', lixo), (None, r'Trash', cacamba),
    (None, r'Light', luminaria),
    (None, r'(ATM|AmoBox|Barrel|Fan|Fence|Hydrant|RoadSign|TrafficLight|Generator|VendingMachine|PhoneBooth|Scaffolding|TowerCrane|Clock|Time|Sign|ConstructionStairs)', metal),
]
# Peça por peça, quando a regra do tipo não serve: (kit, nome) -> regra.
FIXAS = {}


def regra_de(kit, nome):
    if (kit, nome) in FIXAS: return FIXAS[(kit, nome)]
    for k, exp, regra in PECAS:
        if (k is None or k == kit) and re.match(exp, nome): return regra
    return qualquer


def classificar(kit, nome, cor, normal, pos, uv=None):
    """O material de cada face de uma peça. ``cor`` (T × 3, a cor da paleta no meio da face), ``normal`` (T × 3, a da
    face), ``pos`` (T × 3 × 3), ``uv`` (T × 2, o meio da face na textura — o kit de atlas olha onde ela cai)."""
    if not len(cor): return np.zeros(0, int)
    f = Faces(cor, normal, pos); f.uv = uv; f.nome = nome
    return np.asarray(regra_de(kit, nome)(f), int)


# ---------------------------------------------------------------- o Armazém (um modelo só, do Blender)
def armazem(dados):
    """A regra do Armazém. O modelo vem em pedaços grandes, sem o nome das peças; o ``.json`` do mapa (``dados``) tem a
    caixa de cada peça solta (barril, pneu, palete, caixa de papelão, contêiner): a face dentro de uma caixa é da peça;
    fora de todas, é o galpão — o piso, as paredes, o telhado, as vigas e os canos."""
    caixas = []
    for c in dados.get('caixas', []):
        n = c['nome']
        tipo = ('conteiner' if n.startswith('ShippingContainer') else 'barril' if n.startswith(('Barrel', 'FireExt', 'AmoBox', 'Rack', 'ShootingTarget')) else
                'pneu' if n.startswith('Wheel') else 'papel' if n.startswith('PostBox') else 'madeira' if n.startswith(('Pallet', 'WoodenPlank', 'Box')) else None)
        if tipo: caixas.append((np.array(c['centro']), np.array(c['base']), np.array(c['tamanho']) / 2 + 0.06, tipo))

    def regra(f):
        out = _tudo(f, 'reboco')                                   # as paredes e o forro, pintados
        out[f.cinza] = C['metal_pintado']                          # vigas, canos, passarelas, caixilhos
        out[f.cinza & (f.v > 0.4) & (f.cima | f.baixo)] = C['piso']
        out[f.cinza & (f.v > 0.4) & f.pe & (f.y < 0.6)] = C['concreto']
        out[f.marrom & (f.s >= 0.5)] = C['madeira']
        out[(f.h > 20) & (f.h < 45) & (f.s > 0.3) & (f.s < 0.5) & (f.v > 0.7)] = C['tecido']          # sacos de areia, lona
        out[(f.s >= 0.55) & ~f.marrom] = C['metal_pintado']         # contêiner e barril de cor viva
        out[(f.h > 30) & (f.h < 45) & (f.s > 0.5) & f.cima & (f.y < 0.1)] = C['faixa']                # a faixa pintada no piso
        out[f.vidro] = C['vidro']
        tipo_de = np.full(f.n, '', object)
        for centro, base, meio, tipo in caixas:
            dentro = np.all(np.abs((f.meio - centro) @ base) <= meio, axis=1)          # (p − c) · base: o ponto nos eixos da caixa
            tipo_de[dentro & (tipo_de == '')] = tipo
        for tipo, fn in (('barril', lambda g: np.where(g.cinza & (g.v > 0.38), C['metal_cru'], C['metal_pintado'])),
                         ('pneu', lambda g: np.where(g.preto | g.escuro, C['borracha'], C['metal_cru'])),
                         ('papel', lambda g: np.full(g.n, C['papel'])),
                         ('madeira', lambda g: np.where(g.cinza | g.escuro, C['metal_pintado'], C['madeira'])),
                         ('conteiner', lambda g: np.full(g.n, C['metal_pintado']))):
            m = tipo_de == tipo
            if m.any(): out[m] = fn(f)[m]
        return out
    return regra


# ---------------------------------------------------------------- o .glb
_CT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
_NC = dict(SCALAR=1, VEC2=2, VEC3=3, VEC4=4, MAT4=16)


def _ler(caminho):
    d = open(caminho, 'rb').read(); _, _, total = struct.unpack('<4sII', d[:12]); o = 12; j = None; b = b''
    while o < total:
        n, t = struct.unpack('<I4s', d[o:o + 8]); c = d[o + 8:o + 8 + n]; o += 8 + n
        if t == b'JSON': j = json.loads(c)
        elif t.startswith(b'BIN'): b = c
    return j, b


def _acc(j, b, i):
    a = j['accessors'][i]; v = j['bufferViews'][a['bufferView']]; dt = np.dtype(_CT[a['componentType']]); n = _NC[a['type']]
    off = v.get('byteOffset', 0) + a.get('byteOffset', 0); passo = v.get('byteStride') or dt.itemsize * n
    if passo == dt.itemsize * n: return np.frombuffer(b, dt, a['count'] * n, off).reshape(a['count'], n)
    bruto = np.frombuffer(b, np.uint8, passo * (a['count'] - 1) + dt.itemsize * n, off)
    idx = np.arange(a['count'])[:, None] * passo + np.arange(dt.itemsize * n)[None, :]
    return np.frombuffer(bruto[idx].tobytes(), dt).reshape(a['count'], n)


def _paleta(j, b):
    from PIL import Image
    v = j['bufferViews'][j['images'][0]['bufferView']]; o = v.get('byteOffset', 0)
    return np.asarray(Image.open(io.BytesIO(b[o:o + v['byteLength']])).convert('RGB'), float) / 255


def remendar(caminho, kit, regra=None, quieto=False):
    """Grava em ``caminho`` (.glb de um mapa de paleta) o material de cada face, no ``COLOR_0``. Pode rodar de novo:
    refaz a conta do zero. ``kit``: o nome do kit (escolhe as regras de ``PECAS``); ``regra``: uma função única para o
    modelo inteiro (o Armazém). Devolve a área de cada material, em m² (do desenho, sem contar as cópias)."""
    j, b = _ler(caminho); tex = _paleta(j, b); H, W = tex.shape[:2]
    bin_ = bytearray(); views, accs = [], []

    def acc(dados, tipo, comp, alvo=None, minmax=False, normalizado=False):
        while len(bin_) % 4: bin_.append(0)
        v = dict(buffer=0, byteOffset=len(bin_), byteLength=dados.nbytes)
        if alvo: v['target'] = alvo
        views.append(v); bin_.extend(dados.tobytes())
        a = dict(bufferView=len(views) - 1, componentType=comp, count=len(dados), type=tipo)
        if minmax: a['min'] = dados.min(0).tolist(); a['max'] = dados.max(0).tolist()
        if normalizado: a['normalized'] = True
        accs.append(a); return len(accs) - 1

    areas = np.zeros(len(CLASSES))
    for me in j['meshes']:
        nome = re.sub(r'_(e|aberta)\d+$', '', me.get('name', ''))
        for p in me['primitives']:
            at = p['attributes']; pos = _acc(j, b, at['POSITION']).astype(np.float32); nor = _acc(j, b, at['NORMAL']).astype(np.float32)
            uv = _acc(j, b, at['TEXCOORD_0']).astype(np.float32) if 'TEXCOORD_0' in at else np.zeros((len(pos), 2), np.float32)
            idx = (_acc(j, b, p['indices']).reshape(-1) if 'indices' in p else np.arange(len(pos))).astype(np.int64).reshape(-1, 3)
            P, Nn, U = pos[idx].astype(float), nor[idx].astype(float), uv[idx].astype(float)
            # a normal da face: a geométrica, virada para o lado da normal dos vértices
            g = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0]); comp = np.linalg.norm(g, axis=1, keepdims=True); g = g / np.maximum(comp, 1e-12)
            g = np.where((g * Nn.sum(1)).sum(1, keepdims=True) < 0, -g, g); g = np.where(comp < 1e-10, Nn.mean(1), g)
            c = U.mean(1); cor = tex[np.clip((c[:, 1] % 1 * H).astype(int), 0, H - 1), np.clip((c[:, 0] % 1 * W).astype(int), 0, W - 1)]
            if p.get('material') is None and regra is None: cl = np.zeros(len(idx), int)
            else: cl = np.asarray(regra(Faces(cor, g, P)), int) if regra else classificar(kit, nome, cor, g, P, c)
            # No Armazém toda face tem a paleta. Uma peça que o Blender exportou sem material (o bloco do fundo leste, 447 m²)
            # o Godot pinta de branco liso, sem paleta nem detalhe: era a parede branca do galpão (Jhon, 25/11).
            if regra and p.get('material') is None and j.get('materials'): p['material'] = 0
            np.add.at(areas, cl, comp[:, 0] / 2)
            # um vértice por (posição, normal, uv, material): a face inteira tem um material só
            v = np.concatenate([pos[idx.reshape(-1)], nor[idx.reshape(-1)], uv[idx.reshape(-1)], np.repeat(cl, 3)[:, None].astype(np.float32)], 1)
            _, primeiro, inv = np.unique(v, axis=0, return_index=True, return_inverse=True); v = v[primeiro]
            cor_v = np.zeros((len(v), 4), np.uint8); cor_v[:, 0] = v[:, 8].astype(np.uint8); cor_v[:, 3] = 255
            p['attributes'] = dict(POSITION=acc(v[:, 0:3].copy(), 'VEC3', 5126, 34962, True), NORMAL=acc(v[:, 3:6].copy(), 'VEC3', 5126, 34962),
                                   TEXCOORD_0=acc(v[:, 6:8].copy(), 'VEC2', 5126, 34962), COLOR_0=acc(cor_v, 'VEC4', 5121, 34962, normalizado=True))
            p['indices'] = acc(inv.reshape(-1).astype(np.uint32), 'SCALAR', 5125, 34963)
    for im in j.get('images', []):          # as imagens embutidas vão junto
        v = j['bufferViews'][im['bufferView']]; o = v.get('byteOffset', 0); dados = b[o:o + v['byteLength']]
        while len(bin_) % 4: bin_.append(0)
        views.append(dict(buffer=0, byteOffset=len(bin_), byteLength=len(dados))); bin_.extend(dados); im['bufferView'] = len(views) - 1
    j['bufferViews'], j['accessors'] = views, accs
    while len(bin_) % 4: bin_.append(0)
    j['buffers'] = [dict(byteLength=len(bin_))]
    texto = json.dumps(j, separators=(',', ':')).encode()
    while len(texto) % 4: texto += b' '
    with open(caminho, 'wb') as f:
        f.write(struct.pack('<4sII', b'glTF', 2, 12 + 8 + len(texto) + 8 + len(bin_)))
        f.write(struct.pack('<I4s', len(texto), b'JSON')); f.write(texto)
        f.write(struct.pack('<I4s', len(bin_), b'BIN\x00')); f.write(bytes(bin_))
    if not quieto:
        tot = max(areas.sum(), 1e-9)
        print(f"  {os.path.basename(caminho)}: " + ', '.join(f"{CLASSES[i]} {100 * areas[i] / tot:.0f}%" for i in np.argsort(-areas) if areas[i] / tot >= 0.02))
    return {CLASSES[i]: float(a) for i, a in enumerate(areas) if a > 0}


# ---------------------------------------------------------------- o material
TRES = """[gd_resource type="ShaderMaterial" format=3]

[ext_resource type="Shader" path="res://src/assets/mapas/detalhe/superficie.gdshader" id="1"]
[ext_resource type="Texture2D" path="{paleta}" id="2"]
[ext_resource type="CompressedTexture2DArray" path="res://src/assets/mapas/detalhe/detalhe_n.jpg" id="3"]
[ext_resource type="CompressedTexture2DArray" path="res://src/assets/mapas/detalhe/detalhe_m.jpg" id="4"]
[ext_resource type="Texture2D" path="res://src/assets/mapas/detalhe/macro.png" id="5"]
[ext_resource type="CompressedTexture2DArray" path="res://src/assets/mapas/detalhe/detalhe_c.jpg" id="6"]

[resource]
render_priority = 0
shader = ExtResource("1")
shader_parameter/paleta = ExtResource("2")
shader_parameter/detalhe_n = ExtResource("3")
shader_parameter/detalhe_m = ExtResource("4")
shader_parameter/macro = ExtResource("5")
shader_parameter/detalhe_c = ExtResource("6")
"""


def material(nome, paleta):
    """Cria (se ainda não existe) ``assets/mapas/detalhe/<nome>.tres``: o material de detalhe com a paleta ``paleta``
    (caminho ``res://``). Devolve o caminho ``res://`` dele."""
    destino = os.path.join(RAIZ, 'assets', 'mapas', 'detalhe', nome + '.tres')
    if not os.path.exists(destino) or 'detalhe_c' not in open(destino, encoding='utf-8').read():          # o de antes da tinta é refeito
        os.makedirs(os.path.dirname(destino), exist_ok=True)
        with open(destino, 'w', encoding='utf-8') as fh: fh.write(TRES.format(paleta=paleta))
    return f'res://src/assets/mapas/detalhe/{nome}.tres'


def ligar(caminho, tres):
    """Escreve no ``.import`` do .glb que o material dele é o ``tres`` (o Godot troca ao importar). Se o ``.import``
    ainda não existe (mapa novo, o Godot nunca abriu), cria um só com isso — o Godot completa o resto."""
    j, _ = _ler(caminho); nomes = [m.get('name') for m in j.get('materials', []) if m.get('name')]
    sub = '{\n"materials": {\n' + ',\n'.join(f'"{n}": {{\n"use_external/enabled": true,\n"use_external/path": "{tres}"\n}}' for n in nomes) + '\n}\n}'
    imp = caminho + '.import'
    if os.path.exists(imp):
        texto = open(imp, encoding='utf-8').read()
        novo, n = re.subn(r'_subresources=\{.*?\n?\}(?=\n[a-z_/]+=|\n*\Z)', lambda _: '_subresources=' + sub, texto, flags=re.S)
        if n == 0: novo = texto.rstrip('\n') + '\n_subresources=' + sub + '\n'
    else:
        novo = '[remap]\n\nimporter="scene"\nimporter_version=1\ntype="PackedScene"\n\n[params]\n\n_subresources=' + sub + '\n'
    if not os.path.exists(imp) or novo != open(imp, encoding='utf-8').read():
        with open(imp, 'w', encoding='utf-8') as fh: fh.write(novo)


def detalhar_armazem(nome, quieto=False):
    """O Armazém (``armazem`` ou ``armazem_estendido``): um modelo só, com a regra de ``armazem`` e a paleta que o Godot
    tirou do .glb (``assets/mapas/pecas/<nome>_paleta.png``)."""
    with open(os.path.join(RAIZ, 'tools', 'mapas', nome + '.json'), encoding='utf-8') as fh: dados = json.load(fh)
    caminho = os.path.join(RAIZ, 'assets', 'mapas', 'pecas', nome + '.glb')
    if not os.path.exists(caminho): return None
    areas = remendar(caminho, 'armazem', armazem(dados), quieto)
    ligar(caminho, material(nome, f'res://src/assets/mapas/pecas/{nome}_paleta.png'))
    return areas


def detalhar(caminho, kit, paleta=None, regra=None, quieto=False):
    """Tudo de uma vez para um .glb: o material de cada face, o ``.tres`` do kit e a ligação no ``.import``."""
    areas = remendar(caminho, kit, regra, quieto)
    ligar(caminho, material(kit, paleta or f'res://src/assets/mapas/kits/{kit}.png'))
    return areas
