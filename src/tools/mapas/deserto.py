"""
Cidade do Deserto — mapa médio (64 × 64 m), a céu aberto, sol alto.

A cidadela do pacote "Low Poly FPS Map Vol.2 — Desert City" como o autor montou: a cena de demonstração, lida do
``.umap`` do Unreal (``kits/unreal_umap.py`` → ``fps2_demo.json``). Pedido do Jhon, 27/11: "use o que tem lá de demo".
(Até 26/11 o Deserto era uma planta própria, desenhada em letras com as peças soltas do kit; está no git.)

A cena é uma cidadela de 64 × 64 m dentro da muralha (6 m): casas de adobe de um e dois andares, de laje, com escadas
de pedra por fora, toldos, bancas de feira, a praça do chafariz, o largo da feira, palmeiras, e vielas de 3 a 6 m entre
os blocos de casas. Em volta, só dunas (as montanhas de pico branco da cena saíram: deserto não tem pico; e em 27/11
um lado de fora com bairros, torres e oásis chegou a ser montado e saiu no mesmo dia — Jhon: "pode tirar essa cidade ao
redor do deserto, só os montes e a cor de areia tá bom").

**No tamanho da cena** (Jhon, 27/11: "ajuste somente o da obra, para não ficar exagerado"): não foi dobrada como o
Distrito Neon. O azul nasce do lado oeste (o da praça do chafariz), o laranja do lado leste (o do largo da feira); os
dois lados não são iguais (a cena não é simétrica).

A luz é de deserto ao meio-dia: sol forte e quente, sombra curta, o ar claro de poeira, e nuvens altas e ralas
(``assets/mapas/ceus/ceu_nuvens.gdshader``).

As quatro perguntas do teste de mapa:
  1. Caminho que só o lobo faz: de laje em laje (3,5 m) pelos toldos e caixotes, e por cima das casas de dois andares
     (7 m) ricocheteando nas vielas de 3 m; o alto da muralha é barrado.
  2. Onde o humano vê o lobo chegando: as lajes das casas baixas (sobe-se pela escada de pedra), a praça do chafariz e
     o largo da feira.
  3. Corredor que o humano deve evitar: as vielas entre os blocos de casas, com laje dos dois lados.
  4. Cair em cima de quem não olha: das lajes para a viela e para a praça do chafariz.
"""
import os

import numpy as np

from .kit import Mapa, AZUL
from .kits.demo import Demo, por, barreiras, fora_do_anel, alcancavel, nascer_em, assentar
from .kits.kit_fbx import Peca
from .kits.montagem import Montagem, Grade, fechar, espalhar, luz_de_jogo

MEIO = (0.35, 0.5)             # na cena do pacote: o meio da cidadela (vira o centro do mapa)
CHAO = 1.0                     # a altura do chão da cena (o terreno do Unreal)
X, Z = 35.0, 35.0              # meia largura e meio fundo do mapa (o limite, por fora da muralha)
TETO = 18.0
MIOLO = (31.0, 31.0)           # por dentro da muralha: nenhuma duna entra aqui
DUNA = 0.65                    # a altura das dunas da cena, para ficarem baixas e redondas

# A colisão de cada peça é a da própria malha, menos o que é caixa de verdade: a caixa de um pote, de um saco, de um
# barril ou de uma mesa segurava o tiro de cima no ar, sem nada que se visse (o teste TiroDeCimaDeserto)
CAIXA = ('Pallet', 'Door', 'Fence_01')
SEM = ('Block_', 'Book', 'Carpet', 'Cup', 'Desk', 'Food', 'Plate', 'Scroll', 'Flag', 'Dryer', 'Plant', 'PalmTree', 'PowerLines', 'Tile', 'Hill', 'Mountain',
       'Garbage', 'WindowPart', 'FlowerBed', 'Jug_')
LONGE = ('Hill', 'Mountain')
# as escadas de pedra, medidas na peça: (o pé da linha das quinas, a quina do último degrau, os lados)
ESCADAS = {'Stairs_01L': ((3.64, 0.0), (-1.79, 3.49), (-1.17, 1.17)), 'Stairs_01R': ((3.64, 0.0), (-1.79, 3.49), (-1.17, 1.17)),
           # as casas de escada (a escada é por dentro, do alpendre de 19 cm à laje de 3,49 m, em degraus de 17 × 30 cm): sem
           # a chapa o humano subia aos trancos e descia voando três degraus, e o lobo, com a verga da porta em cima, nem
           # entrava no primeiro degrau (Jhon, 27/11: "não consigo subir escadarias em algum lugar, lobo não reconhece")
           'House_06L': ((3.32, 0.19), (-2.39, 3.49), (-1.15, 1.15)), 'House_06R': ((3.32, 0.19), (-2.39, 3.49), (-1.15, 1.15))}
# a água: a bacia do chafariz (o espelho a 35 cm, de 1,9 m de raio; o pilar tem 2,42 m, com a base de 69 cm de raio) e a
# boca do poço (a 65 cm, 69 cm de raio). Jhon, 27/11: "a fonte do mapa deserto teria que ter textura, barulho e
# movimentação de água, não colisão... coloque uma água bem feita"
FONTE = dict(nivel=0.35, raio=1.9, alto=2.42, sai=0.53, bate=0.74, filme=0.37)
POCO = dict(nivel=0.655, raio=0.68)
POCO_MEIO = np.array([0.008, 0.0, 0.014])          # o meio da boca do poço, na peça


def colisao(nome):
    return 'caixa' if nome.startswith(CAIXA) else None if nome.startswith(SEM) else 'malha'


def sem_agua(p, nivel, raio):
    """A peça sem a face da água (as faces viradas para cima, na altura ``nivel``, a menos de ``raio`` do meio): a água é
    posta por cima, de verdade (``Mapa.agua``), e a face lisa do kit, que era também colisão — andava-se em cima da água
    do chafariz —, sai do desenho e da colisão. Entra-se na bacia: o fundo é o chão do mapa."""
    P = p.pos; n = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0]); n = n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
    c = P.mean(1); agua = (n[:, 1] > 0.9) & (np.abs(c[:, 1] - nivel) < 0.03) & (np.linalg.norm(c[:, [0, 2]], axis=1) < raio)
    return Peca(p.nome + '_seca', P[~agua], p.normal[~agua], p.uv[~agua]), int(agua.sum())


def desenho():
    """A cidadela da cena e as dunas em volta."""
    D = Demo('fps2'); M = Montagem(D.kit, colisao); centro = np.array([MEIO[0], CHAO, MEIO[1]]); M.dunas = 0
    M.fontes, M.pocos = [], []; seca = None
    for item in D.itens:
        p, G, E, t, nome = item; t = t - centro
        if nome.startswith('Fountain'):
            if seca is None: seca, M.agua_tirada = sem_agua(p, FONTE['nivel'], FONTE['raio'] + 0.02)
            p = seca; M.fontes.append(t.copy())
        if nome.startswith('Well'): M.pocos.append((t.copy(), G))
        # Só morros de areia em volta (Jhon, 27/11: "montanha com gelo no deserto não faz sentido"; "é um deserto,
        # deserto não tem montanha nem picos"): as montanhas de pico branco da cena saem, e as dunas entram mais
        # baixas (a 65% da altura), que é como ficam redondas
        if nome.startswith('Mountain'): continue
        if nome.startswith(LONGE):
            E = E * np.array([1.0, DUNA, 1.0])
            # (a duna que na cena passa por cima da muralha e entra na cidadela não entra)
            q = p.pos.reshape(-1, 3) @ (G @ np.diag(E)).T + t
            if ((np.abs(q[:, 0]) < MIOLO[0]) & (np.abs(q[:, 2]) < MIOLO[1]) & (q[:, 1] > 0.3)).any(): continue
            por(M, p, G, E, t, nome, colide=None, planta=False); M.dunas += 1; continue
        it = por(M, p, G, E, t, nome, planta=not nome.startswith(SEM))
        if nome in ESCADAS:
            pe, alto, lados = ESCADAS[nome]; M.rampa(it, pe, alto, lados, eixo=2, folga=0.03)
        if nome.startswith('PalmTree'):
            pe = it.pontos(np.zeros((1, 3)))[0]; M.barra(float(pe[0]), float(pe[2]), 0.3, 2.2)          # o tronco
    # (o que estava num montinho do terreno da demo desce até o chão plano daqui: 27/11)
    # (as pedras primeiro: há planta em cima de pedra)
    M.assentadas = assentar(M, ('Rock',), ate=0.7) + assentar(M, ('Plant', 'PalmTree', 'FlowerBed'), ate=1.0)
    return M


def montar():
    m = Mapa("deserto", "Cidade do Deserto", int(2 * X), int(2 * Z))
    m.material("areia", "areia", (1.0, 0.93, 0.80), 3.0)
    m.caixa("Chao", "Areia", (0, -0.25, 0), (520, 0.5, 520), "areia", colide=False)
    M = desenho()
    ocupado = M.ocupacao((-X, -Z), (X, Z))
    meio = ocupado.reshape(ocupado.shape[0] // 2, 2, ocupado.shape[1] // 2, 2).any((1, 3))                  # células de 50 cm
    fora = fora_do_anel(meio, 0.5, vao=1.5)
    M.aplicar(m, "Deserto", "deserto")
    fechar(m, X, Z, altura=TETO)
    n = barreiras(m, ~fora, X, Z, TETO)
    # (o painel das dunas ao longe, na proporção do desenho: mais alto que isto as dunas redondas dele viravam picos)
    m.panorama('serra', X + 215.0, altura=62.0, base=-10.0)

    g = Grade(ocupado, (-X, -Z), 0.25, folga=0.55)
    g.livre &= ~np.kron(fora, np.ones((2, 2), bool))
    chao, perto = alcancavel(g, BASES[0])
    g.livre &= chao
    a = nascer_em(m, g, BASES[0], AZUL); nascer_em(m, g, BASES[1], 3 - AZUL)
    m._grade = g
    espalhar(m, g, a, quantos=16)          # os pontos do mata-mata: todos contra todos, espalhados pelo mapa
    def rota(*pts): return [(float(x), 0.5, float(z)) for x, z in g.rota([perto(*a)] + [perto(x, z) for x, z in pts])]
    for nome, pts in ROTAS.items(): m.rota(nome, rota(*pts))

    # a água de verdade: o chafariz (com a queda e o som) e a boca dos poços
    for i, t in enumerate(M.fontes, 1):
        m.agua(f"Chafariz{i}" if len(M.fontes) > 1 else "Chafariz", (float(t[0]), float(t[1]) + FONTE['nivel'], float(t[2])), FONTE['raio'],
               queda=FONTE['alto'] - FONTE['nivel'], sai=FONTE['sai'], bate=FONTE['bate'], fios=8, alto=FONTE['filme'], turvo=1.2,
               som="fonte", alcance=24.0, volume=-2.0)
    for i, (t, G) in enumerate(M.pocos, 1):
        # (a boca do poço não é no meio da peça: o meio das faces da água, levado ao mundo)
        q = G @ POCO_MEIO + t
        m.agua(f"Poco{i}", (float(q[0]), float(t[1]) + POCO['nivel'] + 0.012, float(q[2])), POCO['raio'], agito=0.35, turvo=0.45, fundo=(0.03, 0.2, 0.3))

    # sol alto de deserto: sombra curta e dura, ar quente e claro
    luz_de_jogo(m, 'deserto', 38, 52, alcance=120.0, nuvens=True)
    print(f"  deserto: {n} barreiras, {M.dunas} dunas, {M.assentadas} plantas e pedras assentadas, {int(chao.sum() * g.cel ** 2)} m² de chão alcançável")
    print(f"  deserto: a face da água saiu do chafariz ({M.agua_tirada} triângulos); {len(M.fontes)} chafariz, {len(M.pocos)} poços")
    if os.environ.get('MAPA_GRADE'):
        g.imagem(os.environ['MAPA_GRADE'], marcas=[(x, z, (40, 90, 255)) for x, _, z in m.spawns[AZUL]] + [(x, z, (255, 120, 30)) for x, _, z in m.spawns[3 - AZUL]] + [(x, z, (20, 160, 20)) for x, z in m.livres],
                 escala=5, rotas=[[(x, z) for x, _, z in r] for _, r in m.rotas])
    return m


# onde nasce cada time: em cantos opostos da cidadela (70 m um do outro, o mais longe que ela dá) — o azul a noroeste,
# o laranja a sudeste. Entre os dois, a rua norte, a rua sul e as vielas do meio
BASES = ((-26.0, -24.0), (23.0, 21.0))
# as rotas dos bots, a partir de onde nasce o azul (cada ponto cai no chão livre mais perto)
ROTAS = dict(
    norte=[(-12, -26), (5, -27), (20, -26), (28, -15), (28, -2), (24, 5), (27, 12), (23, 21)],
    sul=[(-26, -14), (-24, -3), (-24, 8), (-27, 13), (-25, 26), (-8, 26), (5, 27), (15, 25), (23, 21)],
    meio=[(-22, -6), (-10, -2), (2, 2), (2, 10), (12, 12), (18, 12), (23, 21)],
    chafariz=[(-26, -14), (-22, -6), (-12, -4), (-12, -14), (-5, -22), (5, -27)],
    feira=[(-12, -26), (5, -27), (8, -16), (10, -5), (16, 2), (24, 5), (23, 21)],
    vielas=[(-22, -6), (-10, -2), (-5, 12), (-18, 13), (-25, 26), (5, 27), (8, 14), (2, 2)],
)
