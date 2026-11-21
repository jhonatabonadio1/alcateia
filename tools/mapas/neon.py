"""
Distrito Neon — mapa grande (124 × 100 m), a céu aberto, de noite.

A cidade do futuro do pacote "Low Poly Sci-Fi City" (o .unitypackage que o Jhon mandou em 20/11: "implemente esse mapa
no jogo, refaça a luz e as texturas e deixe ele maior; use LED para iluminar as partes que representam LED; de fundo,
prédios bem altos, com letreiros"). O pacote traz uma cena pronta — um quarteirão de 62 × 85 m com o anel de ruas, a
praça do parque ao norte, o miolo de lojas com o bar, a feira coberta e o prédio de três lajes com escada por fora —,
lida do próprio arquivo do Unity (``kits/unity_kit.py``: cada peça no lugar em que o autor pôs).

"Maior": o quarteirão entra duas vezes. Tira-se dele a fileira de prédios do lado oeste e encosta-se ali uma cópia
girada 180° — a rua oeste das duas vira uma avenida de 14 m no meio do mapa, e a rua norte de um emenda na rua sul do
outro. São dois quarteirões inteiros, um de cada time, ligados pela avenida e pelas duas ruas que a cruzam. Em volta, o
anel de torres do pacote (até 105 m), as torres de fundo e, atrás de tudo, o painel dos arranha-céus com letreiros.

A luz é a do lugar: a noite azul-violeta (``kits/fundos.py: ceu_noite``), uma lua fria fazendo sombra, e os LEDs — as
faixas, os anúncios e as janelas acendem de verdade (a textura ``scifi_luz`` no material de detalhe) e, ao pé de cada
letreiro grande, uma lâmpada da cor dele ilumina a rua.

As quatro perguntas do teste de mapa:
  1. Caminho que só o lobo faz: pelos toldos e marquises do miolo (2,5 a 3,4 m) até as lajes do prédio, sem a escada; e
     de laje em laje pelas pontes do alto.
  2. Onde o humano vê o lobo chegando: as lajes abertas do prédio (3,3, 6,6 e 9,9 m), com a avenida e as duas ruas à vista.
  3. Corredor que o humano deve evitar: a feira coberta e o bar, baixos e cheios de bancas — a visão não passa de 8 m.
  4. Cair em cima de quem não olha: das lajes e das pontes sobre a rua de dentro do miolo.

O leste é o oeste girado 180°.
"""
import json, os, re
import numpy as np
from .kit import Mapa, AZUL
from .kits.montagem import Montagem, Item, Grade, fechar, nascer, espalhar

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(AQUI))
CENA = json.load(open(os.path.join(AQUI, 'LP_SciFiCity_DEMO.json'), encoding='utf-8'))
CORTE = -27.0                  # na cena do pacote: o que fica a oeste disto sai (a beira oeste da rua oeste)
MEIO = (-27.0, 2.5)            # o ponto da cena que vira o centro do mapa (em volta dele gira a cópia)
X, Z = 63.0, 51.0              # meia largura e meio fundo do mapa
PISO = 0.05                    # o chão do pacote: ladrilho a 5 cm, asfalto a 6
GIRO = np.diag([-1.0, 1.0, -1.0])

CAIXA = ('SM_Props_Box', 'SM_Props_MetalBox', 'SM_Props_VendingMachine', 'SM_Props_Barrel', 'SM_Props_Trash', 'SM_Props_Garbage', 'SM_Car_', 'SM_Props_Cafe_0', 'SM_Props_Bench',
         'SM_Props_Market', 'SM_Props_Furniture', 'SM_Props_BarProps_03', 'SM_Props_0', 'SM_Nature_Flowerbed', 'SM_Nature_Rock', 'SM_Nature_Log', 'SM_Floor_Light', 'SM_Props_Screen',
         'SM_Props_CyberIce')
SEM = ('SM_Food', 'SM_Nature_Grass', 'SM_Nature_Flower_', 'SM_Nature_Mushoom', 'SM_Nature_Plant', 'SM_Props_Ads', 'SM_Props_Plate', 'SM_Props_Drone', 'SM_BG_', 'SM_Props_Camera', 'SM_Props_Cellphone',
       'SM_Props_BarProps_01', 'SM_Props_BarProps_02', 'SM_Nature_Bio_01_Text', 'SM_Floor_Border')
# as escadas do pacote, medidas na peça: (o pé da linha das quinas, a quina do último degrau, os lados)
ESCADAS = {'SM_Buildings_Stairs_01': ((0.286, 0.0), (-4.22, 3.3), (-1.25, 1.25)), 'SM_Buildings_Stairs_02': ((0.286, 0.0), (-4.22, 3.3), (-1.25, 1.25)),
           'SM_Buildings_Stairs_03': ((0.286, 0.0), (-1.96, 1.65), (-0.97, 0.97)), 'SM_Buildings_Stairs_04': ((0.286, 0.0), (-1.96, 1.65), (-0.97, 0.97))}


def colisao(nome, y=0.0):
    if nome.startswith('SM_Floor_T') or nome.startswith('SM_Floor_Road_'): return 'caixa' if y > 1.0 else None      # o chão é o piso do mapa; a laje do prédio, não
    if nome.startswith(SEM): return None
    return 'caixa' if nome.startswith(CAIXA) else 'malha'


def desenho():
    """Os dois quarteirões: o da cena (leste) e a cópia girada (oeste). Devolve a montagem e os pontos de luz de LED."""
    M = Montagem('scifi', colisao); K = M.kit; metade = []
    for it in CENA['itens']:
        A = np.array(it['matriz']).reshape(3, 4); G = A[:, :3]; D = np.linalg.norm(G, axis=0); R = G / D; t = A[:, 3].copy(); nome = it['peca']
        if np.linalg.det(R) < 0: D[0] = -D[0]; R[:, 0] = -R[:, 0]
        p = K[nome]; c = (p.lo + p.tam / 2) @ (R @ np.diag(D)).T + t
        if c[0] < CORTE: continue                                           # a fileira oeste e o que há atrás dela
        longe = nome.startswith(('SM_BigBuilding', 'SM_BG_'))
        if longe and c[0] < CORTE + 14.0 and c[2] > 30.0: continue          # a torre que cairia em cima da rua do vizinho
        t = t - np.array([MEIO[0], 0.0, MEIO[1]])
        metade.append((p, R, D, t, nome))
    for copia in (False, True):
        for p, R, D, t, nome in metade:
            G = GIRO @ R if copia else R; tt = GIRO @ t if copia else t.copy()
            chao = nome.startswith(('SM_Floor_T', 'SM_Floor_Road_')) and tt[1] < 1.0
            if copia and chao: tt = tt - np.array([0.0, 0.004, 0.0])        # onde as duas ruas se sobrepõem, uma fica 4 mm abaixo (não pisca)
            it = Item(p, G, D.copy(), tt, colisao(nome, float(tt[1])), not chao and not nome.startswith(SEM), nome)
            M.itens.append(it)
            if nome in ESCADAS:
                pe, alto, lados = ESCADAS[nome]; M.rampa(it, pe, alto, lados, eixo=2)
    return M


def leds(M, maximo=44, perto=8.0):
    """Onde pôr as lâmpadas dos LEDs: as peças com mais área acesa (a textura ``scifi_luz``), na cor que acendem, as mais
    fortes primeiro e sem duas a menos de ``perto`` metros. Devolve [(pos, cor, energia, alcance)]."""
    from PIL import Image
    luz = np.asarray(Image.open(os.path.join(RAIZ, 'assets', 'mapas', 'kits', 'scifi_luz.png')).convert('RGB'), float) / 255; L = luz.shape[0]
    da_peca = {}; cand = []
    for it in M.itens:
        p = it.peca
        if p.nome not in da_peca:
            c = p.uv.mean(1); cor = luz[np.clip((c[:, 1] * L).astype(int), 0, L - 1), np.clip((c[:, 0] * L).astype(int), 0, L - 1)]
            n = np.cross(p.pos[:, 1] - p.pos[:, 0], p.pos[:, 2] - p.pos[:, 0]); area = np.linalg.norm(n, axis=1) / 2; ac = cor.max(1) > 0.25
            if ac.any() and area[ac].sum() > 0.5:
                w = area[ac]; nm = (n[ac] / np.maximum(2 * area[ac, None], 1e-9) * w[:, None]).sum(0)
                da_peca[p.nome] = (w.sum(), (p.pos[ac].mean(1) * w[:, None]).sum(0) / w.sum(), (cor[ac] * w[:, None]).sum(0) / w.sum(), nm / max(np.linalg.norm(nm), 1e-9) if np.linalg.norm(nm) > 0.3 * w.sum() else np.zeros(3))
            else: da_peca[p.nome] = None
        d = da_peca[p.nome]
        if d is None: continue
        area, centro, cor, nm = d; pos = centro * it.D @ it.G.T + it.t + (it.G @ nm) * 0.9
        if abs(pos[0]) > X or abs(pos[2]) > Z or pos[1] > 12.0 or pos[1] < 0.3: continue
        cand.append((area * float(np.prod(np.abs(it.D))) ** (2 / 3), pos, cor / max(cor.max(), 1e-6)))
    cand.sort(key=lambda c: -c[0]); postas = []
    for area, pos, cor in cand:
        if len(postas) >= maximo: break
        if any(np.linalg.norm(pos - q[0]) < perto for q in postas): continue
        cor = 0.25 + 0.75 * cor                                             # um pouco de branco: a cor pura não ilumina nada que não seja da cor dela
        postas.append((pos, tuple(float(v) for v in cor), float(np.clip(1.3 + 0.9 * np.sqrt(area), 1.6, 4.2)), float(np.clip(6.0 + 2.2 * np.sqrt(area), 7.0, 15.0))))
    return postas


def _cresce(a, vezes=1, quina=True):
    """A mancha ``a`` (bool) crescida uma célula para cada lado (e na diagonal, com ``quina``), ``vezes`` vezes."""
    for _ in range(vezes):
        b = a.copy(); b[1:] |= a[:-1]; b[:-1] |= a[1:]; b[:, 1:] |= a[:, :-1]; b[:, :-1] |= a[:, 1:]
        if quina: b[1:, 1:] |= a[:-1, :-1]; b[1:, :-1] |= a[:-1, 1:]; b[:-1, 1:] |= a[1:, :-1]; b[:-1, :-1] |= a[1:, 1:]
        a = b
    return a


def _alaga(semente, onde, quina=True):
    """Tudo de ``onde`` que se alcança a partir de ``semente`` andando de célula em célula."""
    v = semente & onde
    while True:
        n = _cresce(v, 1, quina) & onde
        if n.sum() == v.sum(): return v
        v = n


def barreiras(m, M, g, base, cel=1.0):
    """O pacote não fecha o quarteirão em toda a volta: entre uma torre e outra do anel dá para sair do piso. Onde o chão
    livre encosta no vazio de fora (o que se alcança da borda do mapa sem pisar em ladrilho nem atravessar parede) vai
    uma parede invisível — ``Barreira…``, como as de ``barrar``."""
    from PIL import Image, ImageDraw
    W, H = int(2 * X / cel), int(2 * Z / cel); piso = Image.new('1', (W, H), 0); d = ImageDraw.Draw(piso)
    for it in M.itens:
        if it.peca.nome.startswith(('SM_Floor_T', 'SM_Floor_Road_')) and it.t[1] < 1.0:
            c = it.cantos(); d.polygon([((p[0] + X) / cel, (p[2] + Z) / cel) for p in c[[0, 1, 5, 4]]], fill=1)
    piso = _cresce(np.asarray(piso, bool), 1, quina=False)
    k = int(round(cel / g.cel)); livre = g.livre[:H * k, :W * k].reshape(H, k, W, k).any((1, 3))        # a célula tem por onde passar
    fora = ~piso & livre; borda = np.zeros_like(fora); borda[0, :] = borda[-1, :] = borda[:, 0] = borda[:, -1] = True
    vazio = _alaga(borda, fora)
    parede = vazio & _cresce(piso & livre, 2)
    # junta em caixas: fileiras iguais empilhadas
    feitas = np.zeros_like(parede); n = 0
    for i in range(H):
        j = 0
        while j < W:
            if not parede[i, j] or feitas[i, j]: j += 1; continue
            j1 = j
            while j1 < W and parede[i, j1] and not feitas[i, j1]: j1 += 1
            i1 = i + 1
            while i1 < H and parede[i1, j:j1].all() and not feitas[i1, j:j1].any(): i1 += 1
            feitas[i:i1, j:j1] = True; n += 1
            x0, x1, z0, z1 = j * cel - X, j1 * cel - X, i * cel - Z, i1 * cel - Z
            m.caixa('Colisao', f'Barreira{n}', ((x0 + x1) / 2, 15.0, (z0 + z1) / 2), (x1 - x0, 30.0, z1 - z0), 'kit_colisao', oculta=True, minimapa=False)
            j = j1
    return n, parede


def montar():
    m = Mapa("neon", "Distrito Neon", int(2 * X), int(2 * Z))
    m.material("terra", "cascalho", (0.10, 0.10, 0.13), 2.4)
    m.caixa("Chao", "Terra", (0, -0.27, 0), (700, 0.5, 700), "terra", colide=False)
    M = desenho()
    M.aplicar(m, "Neon", "neon")
    fechar(m, X, Z, altura=30.0, piso=PISO)
    m.panorama('neon', 330.0, altura=300.0, base=-20.0, repete=3)

    g = Grade(M.ocupacao((-X, -Z), (X, Z), piso=lambda x, z: PISO), (-X, -Z), 0.25, folga=0.5)
    base = (-51.0, 2.5)                                                    # a rua de fora do quarteirão oeste: onde nasce o time azul
    n, parede = barreiras(m, M, g, base)
    g.livre &= ~np.kron(parede, np.ones((4, 4), bool))                     # quem nasce e as rotas ficam do lado de dentro
    a = nascer(m, g, base, AZUL)[0]
    m._grade = g
    espalhar(m, g, a, quantos=16, y=1.2 + PISO)
    # as rotas (na cena do pacote, mais MEIO): a volta pelas ruas, a avenida, o corte por dentro do miolo e a praça
    semente = np.zeros_like(g.livre); semente[g.celula(*g.perto(*a))] = True
    dentro = _alaga(semente, g.livre, quina=False); ii, jj = np.nonzero(dentro)                              # o chão que se alcança a pé de onde se nasce
    def perto(x, z):
        i, j = g.celula(x, z); k = int(np.argmin((ii - i) ** 2 + (jj - j) ** 2)); return g.ponto(int(ii[k]), int(jj[k]))
    def rota(*pts): return [(float(x), 0.5, float(z)) for x, z in g.rota([perto(*a)] + [perto(x, z) for x, z in pts])]
    m.rota('ruaNorte', rota((-45.0, 19.0), (-20.0, 19.0), (-4.0, 19.0), (4.0, -19.5), (30.0, -19.5), (49.0, -17.0), (49.0, 0.0)))
    m.rota('ruaSul', rota((-49.0, -17.0), (-30.0, -19.5), (-4.0, -19.5), (4.0, 19.0), (30.0, 19.0), (49.0, 17.0), (49.0, 0.0)))
    m.rota('avenida', rota((-45.0, 19.0), (-4.0, 19.0), (-3.5, 0.0), (3.5, 0.0), (4.0, -19.5), (45.0, -19.5)))
    m.rota('porDentro', rota((-40.0, 2.0), (-27.0, 2.0), (-12.0, 2.0), (-3.5, 2.0), (3.5, -2.0), (12.0, -2.0), (27.0, -2.0), (40.0, -2.0)))
    m.rota('pracas', rota((-45.0, 19.0), (-30.0, 30.0), (-12.0, 27.0), (-4.0, 19.0), (4.0, -19.5), (12.0, -27.0), (30.0, -30.0), (45.0, -19.5)))

    # ---- a luz: noite. O céu ilumina pouco e frio; a lua faz a sombra; o resto é LED
    m.ambiente = dict(ceu=dict(hdri="noite_neon", giro=30.0, energia=1.6, ambiente=0.35),
                      luz=(0.50, 0.46, 0.95), luz_energia=1.25, exposicao=1.05, branco=5.0,
                      brilho=0.95, brilho_bloom=0.22, brilho_limiar=0.9,
                      ssao=2.2, ssao_raio=1.6, ssao_potencia=1.5, ssao_luz=0.25, ssil=1.2,
                      contraste=1.08, saturacao=1.22,
                      neblina=dict(cor=(0.42, 0.22, 0.62), densidade=0.0045, ceu=0.25, aerea=0.35, sol=0.1))
    m.sol = dict(yaw=148, queda=52, cor=(0.62, 0.72, 1.0), energia=1.15, alcance=190.0, tamanho=1.2)
    m.preenchimento = dict(yaw=-30, queda=-35, cor=(0.95, 0.45, 0.85), energia=0.45)
    m.borrao_sombra = 1.4
    for i, (pos, cor, energia, alcance) in enumerate(leds(M), 1):
        m.luz("Luzes", f"Led{i}", tuple(float(v) for v in pos), cor, energia, alcance)
    # os postes (27 em cada quarteirão): os 22 mais espalhados, brancos-azulados
    postes = sorted({(round(float(c[:, 0].mean()), 1), round(float(c[:, 1].max()) - 0.4, 1), round(float(c[:, 2].mean()), 1)) for c in (it.cantos() for it in M.itens if it.peca.nome == 'SM_Floor_Light_01')})
    P_ = np.array(postes); esc = [0]; d = ((P_ - P_[0]) ** 2).sum(1)
    while len(esc) < min(22, len(postes)):
        k = int(np.argmax(d)); esc.append(k); d = np.minimum(d, ((P_ - P_[k]) ** 2).sum(1))
    for i, k in enumerate(sorted(esc), 1): m.luz("Luzes", f"Poste{i}", tuple(float(v) for v in P_[k]), (0.72, 0.86, 1.0), 2.4, 9.0)
    print(f"  neon: {n} barreiras, {len(m.luzes)} luzes, {int(dentro.sum() * g.cel ** 2)} m² de chão alcançável")
    if os.environ.get('NEON_GRADE'): g.imagem(os.environ['NEON_GRADE'], marcas=[(x, z, (40, 90, 255)) for x, _, z in m.spawns[AZUL]] + [(x, z, (255, 120, 30)) for x, _, z in m.spawns[3 - AZUL]] + [(x, z, (20, 20, 20)) for x, z in m.livres], escala=2, rotas=[[(x, z) for x, _, z in r] for _, r in m.rotas])
    return m
