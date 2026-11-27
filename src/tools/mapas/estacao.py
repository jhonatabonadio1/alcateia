"""
Estação de Trem — mapa grande (105 × 65 m), a céu aberto, fim de tarde.

A estação do pacote "Low Poly FPS Map Vol.5 — Railway Station" como o autor montou: a cena de demonstração, lida do
``.umap`` do Unreal (``kits/unreal_umap.py`` → ``fps5_demo.json``). Pedido do Jhon, 27/11: "o mapa da estação também, o
mesmo da demo". (Até 26/11 a Estação era uma planta própria, de 120 × 95 m — a rua e a praça, o prédio de 97 m com
quatro torres de relógio, duas plataformas e a passarela —, desenhada em letras e depois mexida na cena; está no git.)

A cena é um pátio de estação fechado por paredes de 9 m: ao norte, três linhas com trens parados (a locomotiva, os
vagões de passageiros, os tanques, os de carga); no meio, o saguão coberto, com as caixas d'água; ao sul, mais duas
linhas; em volta, os prédios da estação, de dois pisos, com telhado de duas águas, e a ala leste. Duas escadas sobem
ao piso de cima (5,1 m), que dá a volta por dentro dos prédios. Tudo no nível zero, menos as plataformas (55 cm).

**No tamanho da cena.** O azul nasce do lado oeste, o laranja do leste; os dois lados não são iguais (a cena não é
simétrica).

A luz é de fim de tarde: sol baixo e quente, sombra comprida e fria, nuvens acesas por baixo; nas luminárias dos
prédios, lâmpadas quentes.

As quatro perguntas do teste de mapa:
  1. Caminho que só o lobo faz: por cima dos trens (4 m) de vagão em vagão, e do teto deles ao piso de cima dos
     prédios, sem a escada.
  2. Onde o humano vê o lobo chegando: o piso de cima (5,1 m), com os dois pátios à vista, e a ponta das linhas.
  3. Corredor que o humano deve evitar: entre dois trens parados (2 m de largura, 30 m de comprido) e o saguão coberto.
  4. Cair em cima de quem não olha: do teto dos vagões e do piso de cima para a plataforma.
"""
import os

import numpy as np

from .kit import Mapa, AZUL
from .kits.demo import Demo, por, barreiras, fechar_predios, fora_do_anel, alcancavel, nascer_em
from .kits.montagem import Montagem, Grade, fechar, espalhar, luz_de_jogo, lampadas

MEIO = (0.0, 11.0)             # na cena do pacote: o ponto que vira o centro do mapa
X, Z = 58.0, 38.0              # meia largura e meio fundo do mapa (o limite, por fora das paredes da estação)
TETO = 24.0
PISO_DE_CIMA = 5.1

# A colisão de cada peça é a da própria malha (os trens, as paredes com os arcos e as portas, os telhados, os bancos),
# menos o que é caixa de verdade
# (revisão de 27/11: as vidraças do piso de cima — o cubo esticado do Unreal — e a porta colidem: passava-se por elas e
# caía-se no pátio)
CAIXA = ('ATM', 'Box', 'PhoneBooth', 'VendingMachine', 'WallCorner', 'WallPart', 'Floor_', 'Door_', 'Cube')
SEM = ('Railway_', 'Light_', 'Clock1', 'Time_', 'Platform_', 'TrafficLight', 'HousePart', 'WallDecor', 'Window_')
ANEL = ('Building_',)          # os prédios fechados (o miolo deles é sólido; o que encosta no lado de fora é barrado até o teto)
# a escada de concreto, medida na peça: (o pé da linha das quinas, a quina do último degrau, os lados)
ESCADAS = {'Stairs_01': ((-0.48, 0.1), (7.52, 5.1), (-2.5, 2.5))}


# as placas de chão com degraus (4 degraus de ~15 cm até 55 cm): a malha, não a caixa — a caixa era uma parede de 55 cm
# no lugar dos degraus (Jhon, 27/11: "não consegui subir esses degraus na estação")
DEGRAUS = ('Floor_06', 'Floor_07', 'Floor_08')


def colisao(nome):
    if nome.startswith(DEGRAUS): return 'malha'
    return None if nome.startswith(SEM) else 'caixa' if nome.startswith(CAIXA) else 'malha'


def desenho():
    """A estação da cena."""
    D = Demo('fps5'); M = Montagem(D.kit, colisao); centro = np.array([MEIO[0], 0.0, MEIO[1]])
    for item in D.itens:
        p, G, E, t, nome = item; t = t - centro; lo, hi = D.caixa(item); colide = 'padrao'
        if nome.startswith('Floor_') and hi[1] < 0.1: colide = None                    # a placa do chão: o chão é o piso do mapa
        # as torres de fora não colidem (ninguém chega lá); as de cima do telhado, sim (revisão de 27/11: o lobo que
        # corria no telhado passava por dentro delas)
        if nome.startswith('Building_') and (abs((lo[0] + hi[0]) / 2 - MEIO[0]) > X or abs((lo[2] + hi[2]) / 2 - MEIO[1]) > Z): colide = None
        it = por(M, p, G, E, t, nome, colide=colide, planta=not nome.startswith(SEM) and not (nome.startswith(('Floor_', 'Roof_')) and hi[1] > 1.0))
        if nome in ESCADAS:
            pe, alto, lados = ESCADAS[nome]; M.rampa(it, pe, alto, lados, eixo=0, ate=PISO_DE_CIMA)
    return M


def montar():
    m = Mapa("estacao", "Estação de Trem", int(2 * X), int(2 * Z))
    # o chão por baixo das placas (onde a cena não tem placa): asfalto velho de fotografia
    m.material("chao", "asfalto", (0.70, 0.70, 0.72), 3.2)
    m.caixa("Chao", "Asfalto", (0, -0.27, 0), (520, 0.5, 520), "chao", colide=False)
    M = desenho()
    ocupado = M.ocupacao((-X, -Z), (X, Z))
    meio = ocupado.reshape(ocupado.shape[0] // 2, 2, ocupado.shape[1] // 2, 2).any((1, 3))                  # células de 50 cm
    fora = fora_do_anel(meio, 0.5)
    M.aplicar(m, "Estacao", "estacao")
    fechar(m, X, Z, altura=TETO)
    n = barreiras(m, ~fora, X, Z, TETO)
    barrados, miolos = fechar_predios(m, M, fora, X, Z, TETO, lambda nome: nome.startswith(ANEL), inicio=n); n += barrados
    m.panorama('cidade', X + 170.0, altura=130.0, base=-6.0, cor=(1.0, 0.76, 0.62))

    g = Grade(ocupado, (-X, -Z), 0.25, folga=0.55)
    g.livre &= ~np.kron(fora, np.ones((2, 2), bool))
    chao, perto = alcancavel(g, BASES[0])
    g.livre &= chao
    a = nascer_em(m, g, BASES[0], AZUL); nascer_em(m, g, BASES[1], 3 - AZUL)
    m._grade = g
    espalhar(m, g, a, quantos=16)          # os pontos do mata-mata: todos contra todos, espalhados pelo mapa
    def rota(*pts): return [(float(x), 0.5, float(z)) for x, z in g.rota([perto(*a)] + [perto(x, z) for x, z in pts])]
    for nome, pts in ROTAS.items(): m.rota(nome, rota(*pts))

    luz_de_jogo(m, 'tarde', 104, 26, alcance=150.0, nuvens=True)
    lampadas(m, [M], maximo=24)
    print(f"  estacao: {n} barreiras, {len(m.luzes)} luzes, {int(chao.sum() * g.cel ** 2)} m² de chão alcançável")
    if os.environ.get('MAPA_GRADE'):
        g.imagem(os.environ['MAPA_GRADE'], marcas=[(x, z, (40, 90, 255)) for x, _, z in m.spawns[AZUL]] + [(x, z, (255, 120, 30)) for x, _, z in m.spawns[3 - AZUL]] + [(x, z, (20, 160, 20)) for x, z in m.livres],
                 escala=2, rotas=[[(x, z) for x, _, z in r] for _, r in m.rotas])
    return m


# onde nasce cada time: o azul na ponta oeste do pátio norte, o laranja no largo da ala leste
BASES = ((-48.0, -8.0), (46.0, 18.0))
# as rotas dos bots, a partir de onde nasce o azul (cada ponto cai no chão livre mais perto)
ROTAS = dict(
    norte=[(-45, -30), (-20, -28), (0, -22), (20, -21), (31, -3), (45, 5), (46, 18)],
    linhas=[(-45, -2), (-20, -2), (0, -3), (20, -5), (31, -3), (45, 5), (46, 18)],
    saguao=[(-45, 5), (-12, 5), (0, 6), (20, 5), (28, 9), (45, 5), (46, 18)],
    sul=[(-48, 10), (-30, 12), (-10, 12), (0, 29), (30, 29), (46, 28), (46, 18)],
    patios=[(-45, -2), (0, -3), (8, 10), (0, 29), (-30, 29), (-48, 10)],
    fundo=[(-45, -30), (0, -22), (20, -21), (31, -3), (28, 9), (30, 29), (46, 28)],
)
