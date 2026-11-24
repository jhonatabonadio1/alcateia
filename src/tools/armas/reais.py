#!/usr/bin/env python3
"""
As armas que o Jhon trouxe em 19/11 (``Archive 2.zip``: "essas são as armas que você deve
animar") prontas para o jogo: cada uma no lugar do punho, do tamanho de verdade, separada
nas peças que a animação mexe e pintada.

    python3 tools/armas/reais.py            # grava assets/armas/<id>/ e a ficha armas.json

Os arquivos (cinco .fbx e um .blend, cada um com a malha e os ossos das peças) passam antes
por ``tools/armas/blender/reais.py`` (Blender), que deixa um .npz por arma em
``assets/pacotes/reais/`` (fora do git); daqui em diante é só Python.

==========  ================  =====================================================
id          arquivo           o que é no jogo
==========  ================  =====================================================
``m4a4``    M4a4.fbx          fuzil de assalto
``ak74``    AK-74.fbx         fuzil de impacto
``scar``    FN Scar H.fbx     a arma pesada (fuzil de batalha 7,62)
``svd``     SVD Dragunov.fbx  fuzil de precisão, com a luneta que veio no arquivo
``deagle``  Deagle.fbx        a pistola padrão
``choque``  V40 Mini.blend    a granada (a única do arquivo)
``faca``    —                 não veio faca: é a do jogo (``dados/faca.npz``, tirada
                              do ``faca.glb`` de antes), agora no esqueleto das outras
==========  ================  =====================================================

Convenção do ``WeaponModel``: origem no punho, cano para −Z, Y para cima, metros — a mesma
do osso da arma do AR-15, então a mão direita do AR-15 fecha em todas. Cada arma fica onde
essa mão a segura de verdade: a frente do punho na dobra dos dedos, o guarda-mato logo
acima do dedo médio e o gatilho ao alcance do indicador (``gatilho`` é onde o dedo encosta).

Três fuzis (AK-74, SCAR-H, SVD) vieram sem cor: todos os materiais cinza. As cores de
``CORES`` são as das armas de verdade (madeira e baquelite no AK e no SVD, areia no SCAR).
Cada face aponta para uma célula de ``assets/armas/reais/paleta.png``, feita aqui.
"""
import json, os, shutil, struct, sys, zlib
import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(AQUI))
PACOTE = os.path.join(RAIZ, 'assets', 'pacotes', 'reais')
DESTINO = os.path.join(RAIZ, 'assets', 'armas')
PALETA = os.path.join(DESTINO, 'reais', 'paleta.png')
TEXTURA = 'res://src/assets/armas/reais/paleta.png'
DADOS = os.path.join(AQUI, 'dados')

# cores (lineares): o que cada material é
PRETO, ACO, ACO_CLARO, POLIMERO = (0.022, 0.023, 0.025), (0.045, 0.047, 0.05), (0.30, 0.31, 0.33), (0.016, 0.016, 0.018)
MADEIRA, MADEIRA_ESCURA, BAQUELITE, AREIA, AREIA_ESCURA = (0.30, 0.125, 0.04), (0.17, 0.065, 0.022), (0.22, 0.05, 0.02), (0.36, 0.26, 0.15), (0.29, 0.205, 0.115)
LATAO, COBRE, ESPOLETA, LENTE, OLIVA = (0.55, 0.38, 0.12), (0.45, 0.2, 0.09), (0.35, 0.3, 0.2), (0.06, 0.16, 0.30), (0.075, 0.095, 0.055)

ARMAS = dict(
    m4a4=dict(nome='M4A4', comprimento=0.84, t=(0.0, -0.019, -0.102), nos=dict(mag='Magazine', bullet='Magazine', bullets='Magazine', bolt='Ferrolho'),
              cores={5: ESPOLETA, 6: COBRE, 7: LATAO}, minimo=0.016),
    ak74=dict(nome='AK-74', comprimento=0.943, t=(0.0, -0.055, -0.122), nos=dict(mag='Magazine', bullet='Magazine', bullets='Magazine', bolt='Ferrolho'),
              cores={0: PRETO, 1: LATAO, 2: ESPOLETA, 3: COBRE, 4: ACO, 5: BAQUELITE, 6: ACO_CLARO, 7: PRETO, 8: ACO_CLARO, 9: ACO, 10: ACO, 11: ACO,
                     12: BAQUELITE, 13: MADEIRA}),
    scar=dict(nome='SCAR-H', comprimento=0.96, t=(0.0, -0.017, -0.123), nos=dict(mag='Magazine', bullet='Magazine', bullets='Magazine', bolt='Ferrolho'),
              cores={0: PRETO, 1: COBRE, 2: ESPOLETA, 3: LATAO, 4: ACO, 5: PRETO, 6: AREIA, 7: AREIA_ESCURA, 8: PRETO, 9: ACO, 10: POLIMERO}),
    svd=dict(nome='SVD', comprimento=1.225, t=(0.0, 0.007, -0.285), nos=dict(mag='Magazine', bullet='Magazine', bullets='Magazine', bolt='Ferrolho'),
             cores={0: MADEIRA, 1: PRETO, 2: ACO, 3: PRETO, 4: ESPOLETA, 5: LATAO, 6: COBRE, 7: ACO, 8: ACO, 9: ACO_CLARO, 10: ACO, 11: ACO, 12: ACO, 13: POLIMERO,
                    14: PRETO, 15: ACO, 16: PRETO, 17: ACO, 18: ACO, 19: LENTE, 20: PRETO, 21: ACO, 22: POLIMERO, 23: ACO}),
    deagle=dict(nome='Desert Eagle', comprimento=0.246, t=(0.0, -0.0496, -0.024), nos=dict(mag='Magazine', bullet='Magazine', bullets='Magazine', slide='Ferrolho', safety='Ferrolho'),
                cores={1: ACO, 4: ACO, 10: ACO}, minimo=0.016),
    choque=dict(nome='Granada', arquivo='v40', escala=0.019, eixos='em_pe', t=(0.0, 0.0, 0.0), nos=dict(pinpull='Pino', pinmove='Pino'), cores={0: OLIVA}, minimo=0.02),
)
FOGO = ('m4a4', 'ak74', 'scar', 'svd', 'deagle')


def _npz(id_):
    caminho = os.path.join(PACOTE, ARMAS[id_].get('arquivo', id_) + '.npz')
    if not os.path.exists(caminho):
        raise SystemExit(f"falta {caminho}: rode tools/armas/blender/reais.py no Blender (veja docs/PACOTES.md)")
    return np.load(caminho)


# ---------------------------------------------------------------- a paleta
def _cores():
    """Todas as cores usadas, na ordem (arma, material): a célula de cada uma."""
    out = []
    for id_, a in ARMAS.items():
        d = _npz(id_)
        for k in range(len(d['materiais'])):
            c = np.array(a['cores'].get(k, d['cores'][k][:3]), float)
            out.append(((id_, k), np.maximum(c, a.get('minimo', 0.0))))
    return out


LADO, CELULA = 16, 8


def _uv(celula):
    return np.array([(celula % LADO + 0.5) / LADO, 1 - (celula // LADO + 0.5) / LADO])       # v de baixo para cima (como no Blender)


def paleta():
    """Grava a paleta (.png, sRGB) e devolve o caminho."""
    cores = _cores(); img = np.zeros((LADO * CELULA, LADO * CELULA, 3), np.uint8)
    for n, (_, c) in enumerate(cores):
        s = np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)
        y, x = divmod(n, LADO); img[y * CELULA:(y + 1) * CELULA, x * CELULA:(x + 1) * CELULA] = np.round(np.clip(s, 0, 1) * 255)
    def bloco(tipo, dados): return struct.pack('>I', len(dados)) + tipo + dados + struct.pack('>I', zlib.crc32(tipo + dados) & 0xffffffff)
    linhas = b''.join(b'\x00' + img[y].tobytes() for y in range(img.shape[0]))
    png = b'\x89PNG\r\n\x1a\n' + bloco(b'IHDR', struct.pack('>IIBBBBB', img.shape[1], img.shape[0], 8, 2, 0, 0, 0)) + bloco(b'IDAT', zlib.compress(linhas, 9)) + bloco(b'IEND', b'')
    os.makedirs(os.path.dirname(PALETA), exist_ok=True)
    if not os.path.exists(PALETA) or open(PALETA, 'rb').read() != png: open(PALETA, 'wb').write(png)
    return PALETA


# ---------------------------------------------------------------- as peças
DE_LADO = np.array([[0, -1, 0], [0, 0, 1], [-1, 0, 0]], float)      # do Blender (cano +X, Z para cima) para o jogo (x direita, y cima, z trás)
EM_PE = np.array([[1, 0, 0], [0, 0, 1], [0, -1, 0]], float)         # a granada: em pé, como veio


def _juntar(*ms):
    return {k: np.concatenate([m[k] for m in ms]) for k in ('pos', 'normal', 'uv')}


def _montar(id_):
    a = ARMAS[id_]; d = _npz(id_); P = d['pos'].astype(float); N = d['normal'].astype(float)
    M = EM_PE if a.get('eixos') == 'em_pe' else DE_LADO
    escala = a.get('escala') or a['comprimento'] / np.ptp(P.reshape(-1, 3)[:, 0])
    pos = P @ M.T * escala + np.array(a['t']); normal = N @ M.T
    celulas = {chave: n for n, (chave, _) in enumerate(_cores())}
    uv = np.array([_uv(celulas[(id_, int(m))]) for m in d['mat']])[:, None, :].repeat(3, 1)
    ossos = [str(o) for o in d['ossos']]; no = np.array([a['nos'].get(ossos[k], '') for k in d['osso']])
    pecas = []
    for arquivo, nome in (('corpo', ''), ('ferrolho', 'Ferrolho'), ('magazine', 'Magazine'), ('pino', 'Pino')):
        s = no == nome
        if s.any(): pecas.append((arquivo, nome, dict(pos=pos[s], normal=normal[s], uv=uv[s])))
    return a, pecas, escala


def por_osso(id_):
    """Os triângulos de uma arma separados pelos ossos do arquivo de origem (no espaço da peça, m, como em ``pecas``).
    Na granada: ``grenade`` (a bola e a espoleta), ``handle`` (a alavanca), ``pinpull`` (o pino), ``pinmove`` (a argola)."""
    a = ARMAS[id_]; d = _npz(id_); P = d['pos'].astype(float)
    M = EM_PE if a.get('eixos') == 'em_pe' else DE_LADO
    escala = a.get('escala') or a['comprimento'] / np.ptp(P.reshape(-1, 3)[:, 0])
    pos = P @ M.T * escala + np.array(a['t']); ossos = [str(o) for o in d['ossos']]; dono = np.array([ossos[k] for k in d['osso']])
    return {n: pos[dono == n] for n in ossos}


def _faca():
    """A faca do jogo, com a lâmina para −Z, o fio para baixo e a guarda em z = −5,2 cm."""
    d = np.load(os.path.join(DADOS, 'faca.npz')); s = 0.0068
    M = np.array([[0, -1, 0], [1, 0, 0], [0, 0, 1]], float)
    pos = d['pos'].astype(float) @ M.T * s + np.array([0.0, 0.0, -0.0112]); uv = d['uv'].astype(float).copy(); uv[:, :, 1] = 1 - uv[:, :, 1]
    return dict(nome='Faca', pecas=[('faca', '', dict(pos=pos, normal=d['normal'].astype(float) @ M.T, uv=uv))], texturas=DADOS)


# Onde medir em cada arma (cm, no espaço do punho): o gatilho (a altura e a faixa de z em que ele
# está), a alça e a massa (a faixa de z de cada uma e quanto o ponto de mira fica abaixo do topo).
ONDE = dict(
    # M4A4: a alça de anel é um bloco grande; com a linha de mira pelo furo dela, o bloco tapava o alvo (Jhon, 19/11: "tá
    # tampando e ficando ruim de enxergar o oponente"). A linha passa 2 mm acima da alça e 2,5 mm abaixo das pontas das
    # orelhas da massa: o centro da tela fica livre, entre as duas orelhas, com a alça inteira embaixo.
    # M4A4: a linha de mira passa 1,5 mm acima das orelhas da alça e na ponta do pino da massa (que fica 5 mm abaixo das
    # orelhas dela): o pino no centro da tela, por cima da alça.
    m4a4=dict(gatilho=(-3.5, -3.0, 0.5), alca=(0.5, 2.9, -0.15), massa=(-41.5, -39.5, 0.57), ejecao=(0.022, 0.035, -0.075)),
    # AK-74: o entalhe da alça tem 1,5 mm e o pino da massa fica 3 mm abaixo das orelhas dela. A linha de mira passa 2 mm
    # acima da alça e na ponta do pino: o pino aparece no meio da tela, por cima da alça (Jhon, 19/11: "a mira da AK deve
    # poder enxergar a mira pequena da frente"). Antes passava pelo topo das orelhas e o pino ficava escondido atrás da alça.
    ak74=dict(gatilho=(-3.5, -3.0, 0.4), alca=(-17.5, -15.5, -0.2), massa=(-53.5, -51.5, 0.30), ejecao=(0.020, 0.040, -0.110)),
    scar=dict(gatilho=(-3.4, -3.2, -0.6), alca=(4.5, 6.9, 0.35), massa=(-38.5, -35.5, 0.3), ejecao=(0.022, 0.050, -0.100)),
    svd=dict(gatilho=(-3.5, -2.5, 0.6), ejecao=(0.020, 0.035, -0.130)),
    deagle=dict(gatilho=(-3.6, -2.0, 1.5), alca=(4.0, 7.5, 0.05), massa=(-15.5, -12.5, 0.0), ejecao=(0.014, 0.020, -0.030), cano_abaixo=0.9),
)


def _medir(id_, pecas, d=None):
    """Mede na malha (cm → m): a boca do cano, onde o indicador encosta no gatilho, a linha de mira."""
    o = ONDE[id_]; tudo = np.vstack([m['pos'].reshape(-1, 3) for _, _, m in pecas]) * 100; out = {}
    def denso(P, n=7):
        q = [P.reshape(-1, 3)]
        for u in np.linspace(0.12, 0.88, n):
            for a, b in ((0, 1), (1, 2), (2, 0)): q.append(P[:, a] * (1 - u) + P[:, b] * u)
        return np.vstack(q)
    frente = tudo[tudo[:, 2] < tudo[:, 2].min() + 0.6]
    y = frente[:, 1].max() - o['cano_abaixo'] if 'cano_abaixo' in o else (frente[:, 1].min() + frente[:, 1].max()) / 2
    out['cano'] = (0.0, round(float(y) / 100, 4), round(float(tudo[:, 2].min()) / 100, 4))
    corpo = [m for _, no, m in pecas if no == ''][0]['pos'] * 100; fino = denso(corpo[np.abs(corpo[:, :, 0]).max(1) < 0.75])
    gy, z0, z1 = o['gatilho']; g = fino[(np.abs(fino[:, 1] - gy) < 0.25) & (fino[:, 2] > z0) & (fino[:, 2] < z1)]
    out['gatilho'] = (-0.003, gy / 100, round(float(g[:, 2].min() - 0.55) / 100, 4))          # o osso do dedo fica meio centímetro à frente da face do gatilho
    out['gatilho_face'] = round(float(g[:, 2].min()) / 100, 4)
    meio = tudo[np.abs(tudo[:, 0]) < 0.9]
    if 'alca' in o:
        def topo(z0, z1, abaixo):
            q = meio[(meio[:, 2] >= z0) & (meio[:, 2] <= z1)]; alto = q[q[:, 1] > q[:, 1].max() - 0.15]
            return (0.0, round(float(q[:, 1].max() - abaixo) / 100, 4), round(float(alto[:, 2].mean()) / 100, 4))
        out['mira'] = dict(alca=topo(*o['alca']), massa=topo(*o['massa']))
    if id_ == 'svd':                                                                         # a luneta: o eixo passa pelo centro das duas lentes
        a = ARMAS[id_]; lentes = d['pos'][d['mat'] == 19].astype(float) @ DE_LADO.T * (a['comprimento'] / np.ptp(d['pos'].reshape(-1, 3)[:, 0])) + np.array(a['t'])
        q = lentes.reshape(-1, 3); tras = q[q[:, 2] > q[:, 2].mean()]; frente_l = q[q[:, 2] <= q[:, 2].mean()]
        c = (tras.min(0) + tras.max(0)) / 2; f = (frente_l.min(0) + frente_l.max(0)) / 2
        out['luneta'] = dict(tras=tuple(round(float(x), 4) for x in c), frente=tuple(round(float(x), 4) for x in f))
    out['ejecao'] = o['ejecao']
    return out


def arma(id_):
    """A arma inteira: as peças e o que se mediu nela (m): ``cano``, ``gatilho``, ``mira`` ou ``luneta``, ``ejecao``."""
    if id_ == 'faca': return _faca()
    a, pecas, escala = _montar(id_)
    return dict(nome=a['nome'], pecas=pecas, escala=escala, **(_medir(id_, pecas, _npz(id_)) if id_ in ONDE else {}))


def pecas(id_):
    """[(arquivo, nó, malha)] da arma, no espaço do punho (m)."""
    return arma(id_)['pecas']


# ---------------------------------------------------------------- o modelo de mundo (terceira pessoa, chão, vitrine)
_PUNHO = dict(alvo=[0.017, -0.078, 0.012], dedos=[0.3, -0.3, -1], palma=[-1, 0, -0.35], pose='punho')       # a do AR-15: o punho está no mesmo lugar
_SOLTA = dict(alvo=[-0.3, -0.05, -0.05], dedos=[0.3, 0.35, -1], palma=[0.5, -1, 0], pose='aberta')
def _apoio(y, z): return dict(alvo=[0.0, y, z], dedos=[0.75, 0.1, -0.62], palma=[0, 1, 0], pose='apoio')
MAOS = dict(
    m4a4=dict(direita=_PUNHO, esquerda=_apoio(0.0, -0.27)), ak74=dict(direita=_PUNHO, esquerda=_apoio(-0.008, -0.27)),
    scar=dict(direita=_PUNHO, esquerda=_apoio(0.0, -0.27)), svd=dict(direita=_PUNHO, esquerda=_apoio(0.0, -0.34)),
    deagle=dict(direita=_PUNHO, esquerda=dict(alvo=[0.004, -0.135, 0.035], dedos=[0.83, 0.25, -0.5], palma=[-0.2, 1, -0.2], pose='apoio')),
    choque=dict(direita=dict(alvo=[0.026, 0.0, -0.0], dedos=[0.1, 0.1, -1], palma=[-1, 0, -0.1], pose='fechada'), esquerda=_SOLTA),
)


def _obj(caminho, m):
    """Grava a malha (triângulos soltos, com UV e normal) num .obj."""
    pos = m['pos'].reshape(-1, 3); n = m['normal'].reshape(-1, 3); uv = m['uv'].reshape(-1, 2)
    with open(caminho, 'w') as f:
        f.write('# tools/armas/reais.py\n')
        f.writelines(f'v {a:.5f} {b:.5f} {c:.5f}\n' for a, b, c in pos)
        f.writelines(f'vt {a:.5f} {b:.5f}\n' for a, b in uv)
        f.writelines(f'vn {a:.4f} {b:.4f} {c:.4f}\n' for a, b, c in n)
        f.writelines(f'f {k}/{k}/{k} {k + 1}/{k + 1}/{k + 1} {k + 2}/{k + 2}/{k + 2}\n' for k in range(1, len(pos) + 1, 3))


def gravar(ids):
    """As peças de cada arma em ``assets/armas/<id>/`` e a entrada dela em ``armas.json`` (a faca do jogo não muda)."""
    caminho = os.path.join(DESTINO, 'armas.json'); ficha = json.load(open(caminho, encoding='utf-8')); paleta()
    for id_ in ids:
        a = arma(id_); pasta = os.path.join(DESTINO, id_); os.makedirs(pasta, exist_ok=True)
        for velho in os.listdir(pasta):
            if velho.endswith(('.obj', '.obj.import', '.mtl')): os.remove(os.path.join(pasta, velho))
        partes = []; tudo = []
        for arquivo, no, m in a['pecas']:
            _obj(os.path.join(pasta, f'{arquivo}.obj'), m); tudo.append(m['pos'].reshape(-1, 3))
            partes.append(dict(malha=f'res://src/assets/armas/{id_}/{arquivo}.obj', material=TEXTURA, no=no))
            print(f"{id_:7s} {arquivo:9s} nó {no or '-':9s} {len(m['pos']):5d} triângulos")
        tudo = np.vstack(tudo); pontos = {}
        if 'cano' in a: pontos = dict(Muzzle=list(a['cano']), Ejecao=list(a['ejecao']))
        ficha['armas'][id_] = dict(limites=[[round(float(x), 3) for x in tudo.min(0)], [round(float(x), 3) for x in tudo.max(0)]],
                                   maos=MAOS[id_], partes=partes, pontos=pontos)
    json.dump(ficha, open(caminho, 'w', encoding='utf-8'), indent=1, ensure_ascii=False, sort_keys=True); open(caminho, 'a', encoding='utf-8').write('\n')


if __name__ == '__main__':
    gravar(sys.argv[1:] or list(ARMAS))
