#!/usr/bin/env python3
"""
A corrida do humano (Shift) em primeira pessoa: o clipe ``correr`` de cada conjunto
(pedido do Jhon, 26/11: "botão de sprint pro personagem humano... a animação de sprint
do AR-15, replicada nas outras armas; faca, granada e pistola: apenas abaixar e correr,
com a mesma movimentação").

    python3 src/tools/armas/correr.py                       # todos os .glb de src/assets/armas/fp
    python3 src/tools/armas/correr.py src/assets/armas/fp/faca.glb [outro.glb ...]
        [--ficha src/assets/armas/fp/bracos.json] [--sem-ficha]

A fonte é a corrida do tutorial da InfimaGames, ``A_FP_AssaultRifle_Run_Loop.uasset``
(0,667 s, um ciclo de dois passos; em ``src/assets/pacotes/infima/``, veja
``docs/PACOTES.md``), lida pelo ``uasset.py``. Dela sai **o que a arma faz a partir da
pose parada**: desce, o cano cai 23° e vira para a esquerda, a arma tomba 24° e balança
6 cm de um lado para o outro a cada ciclo.

Diferente do ``conjuntos.py``, este script **não refaz o conjunto**: abre o ``.glb`` que
existe, lê a pose parada dele (a arma, as mãos nela, os dedos, as peças) e grava um clipe
a mais. O acabamento de cada arma (malha, textura, pegada) fica como está, e rodar de
novo dá o mesmo arquivo.

- **Fuzis** (AR-15, M4A4, AK-74, SCAR-H, SVD e as pinturas): a arma faz o movimento da
  referência inteiro, com as duas mãos presas nela como no parado; os braços são
  recalculados (cinemática inversa, o cotovelo para o lado em que já dobrava).
- **Pistola, faca e granada**: a arma só abaixa (``BAIXA``, no espaço do olho) e leva o
  **balanço** da referência — o vai e vem em volta da pose média da corrida do fuzil, que
  é o mesmo para todas. A mão livre (a esquerda; a da pistola solta a arma para correr)
  abaixa junto e balança no passo contrário, como o braço de quem corre.

O pulso não quebra: depois de cada quadro o antebraço se alinha com a mão
(``antebraco_pela_mao``), e o script imprime a dobra do pulso de cada lado no pior
quadro, ao lado da do parado, e o que faltou de alcance.

A câmera balança junto (o osso ``interaction``, como nos outros clipes): o giro da cabeça
da referência, ``CAMERA`` vezes maior.
"""
import glob, json, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from esqueleto import *
import conjuntos, recarga

AQUI = os.path.dirname(os.path.abspath(__file__))
FP = conjuntos.FP
CLIPE = 'correr'
DURA = 2.0 / 3.0            # o ciclo da referência (s): dois passos. O jogo toca mais depressa, pela velocidade do corpo
CAMERA = 1.0                # quanto do giro da cabeça da referência vai para a câmera (lá é ±0,24°). Era 3,0:
                            # somado ao passo rápido, a tela sacudia demais (Jhon, 26/11: "parece o Sonic")
# O fuzil correndo, em relação ao parado, no espaço do olho e em volta da mão direita: (x, y, z) em metros e
# (pitch, yaw, roll) em graus, mais o balanço da referência. Não é mais a pose da referência: com ela (o cano 23° para
# baixo) a arma saía da tela, e puxada para o peito sobrava só a ponta da mira (Jhon, 26/11: "inclinar a arma
# horizontalmente", depois "tira um pouco do peito o fuzil" e "pode afastar mais um pouco as armas do peito").
FUZIL_BAIXA = dict(move=(0.100, -0.045, -0.020), giro=(0.0, 26.0, 18.0), balanco=(1.0, 0.8))
if os.environ.get('CORRER_FUZIL'):                                      # para experimentar: "x,y,z,pitch,yaw,roll"
    _v = [float(x) for x in os.environ['CORRER_FUZIL'].split(',')]; FUZIL_BAIXA = dict(move=tuple(_v[:3]), giro=tuple(_v[3:6]), balanco=(1.0, 0.8))
BRACO = 0.4                 # o peso de ``para_onde`` com pistola, faca e granada
ALTURA = -0.16              # a altura da mão direita correndo, com pistola, faca e granada (m, no espaço do olho; o
                            # olho é 0). A mesma nas três (Jhon, 26/11: "a altura da faca e da granada devem ser a
                            # mesma da pistola"); a da pistola parada é −0,15

# O tipo de cada conjunto, pelo começo do nome do arquivo.
TIPOS = (('deagle', 'pistola'), ('faca', 'faca'), ('choque', 'granada'), ('granada', 'granada'))

# A arma abaixada, em relação à parada, no espaço do olho: (x, z) em metros (a altura é ``ALTURA``)
# e (pitch, yaw, roll) em graus, girando em volta da mão direita. pitch − desce o cano, yaw +
# leva o cano para a esquerda, roll + tomba o topo para a esquerda.
# ``balanco``: quanto do vai e vem da corrida do fuzil a arma leva (posição, giro).
# ``livre``: a mão livre — quanto desce (m) e quanto do balanço leva, no passo contrário.
BAIXA = dict(
    # A mão esquerda solta a pistola e o braço esquerdo **desce inteiro**, do jeito que está no parado (``desce``: o
    # quanto, no espaço do olho; ``balanco``: quanto do vai e vem leva, no passo contrário). Só a posição do ombro
    # muda; nenhum osso do braço gira. Presa na arma, a mão era arrastada para baixo do braço direito; solta e levada
    # por cinemática inversa, o braço ficava embaixo mas **subia pela tela na troca de animação** (Jhon, 26/11: "ainda
    # estou conseguindo ver o braço esquerdo subindo ao correr com pistola"; "ele pode ir para baixo ao invés de
    # subir"): o Godot mistura duas animações pelo giro de cada osso a partir do repouso, e o braço de cima esquerdo
    # da pistola fica a 177° do repouso — qualquer giro novo cai do outro lado dos 180° e a mistura passa pela pose
    # de repouso, o braço aberto para cima (``conferir_troca`` mede isto).
    pistola=dict(move=(-0.020, 0.0), giro=(-14.0, 8.0, 8.0), balanco=(1.0, 0.8),
                 desce=dict(move=(-0.030, -0.160, 0.030), balanco=0.5)),
    faca=dict(move=(-0.020, 0.040), giro=(-20.0, 8.0, 10.0), balanco=(1.0, 0.7),
              livre=dict(move=(0.020, -0.075, 0.060), giro=(-18.0, -8.0, -10.0), balanco=1.0)),
    granada=dict(move=(-0.020, 0.040), giro=(-16.0, 10.0, 8.0), balanco=(1.0, 0.7),
                 livre=dict(move=(0.0, -0.05, 0.0), giro=(0.0, 0.0, 0.0), balanco=0.0)),
)

if os.environ.get('CORRER_PISTOLA'):                                    # para experimentar: "x,z,pitch,yaw,roll,altura"
    _v = [float(x) for x in os.environ['CORRER_PISTOLA'].split(',')]
    BAIXA['pistola'] = dict(move=(_v[0], _v[1]), giro=tuple(_v[2:5]), balanco=BAIXA['pistola']['balanco']); ALTURA_PISTOLA = _v[5]
else: ALTURA_PISTOLA = None


def tipo_de(path):
    nome = os.path.basename(path)
    for prefixo, tipo in TIPOS:
        if nome.startswith(prefixo): return tipo
    return 'fuzil'


class Corrida(conjuntos.Movimento):
    """A corrida da referência: o movimento da arma (como o ``Movimento`` dá para os outros
    clipes) e o balanço dela no espaço do olho, em volta da pose média."""
    ARQUIVOS = dict(correr='Run_Loop')

    def __init__(self, AR):
        super().__init__(AR)
        ds, _ = self.clipes[CLIPE]; self.AR = AR; self.G0 = G0 = AR.arma_de(AR.parado)
        olho = [AR.olho(G0 @ d) for d in ds[:-1]]                     # o último quadro repete o primeiro
        self.meio = np.mean([m[:3, 3] for m in olho], axis=0)
        # o giro médio: a soma dos quatérnios do mesmo hemisfério
        qs = [matq(rot(m)) for m in olho]; qs = [q if np.dot(q, qs[0]) >= 0 else -q for q in qs]
        qm = np.sum(qs, axis=0); self.giro_meio = qmat(qm / np.linalg.norm(qm))

    def balanco(self, t, pos=1.0, gira=1.0, espelho=False):
        """O vai e vem da arma no instante t, no espaço do olho: (deslocamento em m, giro 3×3),
        em volta da pose média da corrida. ``espelho``: o do outro braço — o passo contrário
        (meio ciclo depois), para o outro lado."""
        if espelho: t = t + self.duracao(CLIPE) / 2
        t = t % self.duracao(CLIPE)
        G = self.parada_em(t)
        d = (G[:3, 3] - self.meio) * pos
        R = rot(parte(_so_giro(rot(G) @ self.giro_meio.T), gira))
        return d, R

    def parada_em(self, t):
        """A arma do fuzil (o AR-15) no instante t, no espaço do olho."""
        return self.AR.olho(self.G0 @ self.arma(CLIPE, t))


def _so_giro(R):
    m = np.eye(4); m[:3, :3] = R; return m


def levar(E, G, move, giro_graus, d=(0.0, 0.0, 0.0), R=np.eye(3), pivo=None):
    """Uma 4×4 da raiz (a arma ou uma mão) abaixada e balançada: gira em volta de ``pivo`` (a mão
    que segura; sem ele, a origem dela) e anda, tudo no espaço do olho. Girar em volta da origem da
    arma, longe da mão, levantava a mão da pistola em vez de baixar."""
    O = E.olho(G); p = O[:3, 3].copy(); c = p if pivo is None else np.asarray(pivo, float)
    A = rot(_so_giro(R) @ giro(*giro_graus))
    N = np.eye(4); N[:3, :3] = A @ rot(O); N[:3, 3] = c + A @ (p - c) + np.asarray(move, float) + np.asarray(d, float)
    return E.raiz(N)


def _unit(v): return v / np.linalg.norm(v)


def _giro_puro(m): r = rot(m); return r / np.linalg.norm(r, axis=0, keepdims=True)


def antebraco_do_parado(E, P):
    """A direção de cada antebraço (do cotovelo para o pulso) no parado: no espaço da raiz e no da mão.
    Correndo, o antebraço volta para perto dela (``para_onde``): o braço fica montado como no parado e
    a mão anda com ele. Sem isto, a cinemática inversa escolhia outro cotovelo — com a pistola o
    esquerdo ia para trás da câmera e o braço passava pela tela (Jhon, 26/11)."""
    r = {}
    for l in 'rl':
        C = E.mundo(P, E.N(f'lowerarm_{l}'))[:3, 3]; Wh = E.mundo(P, E.N(f'hand_{l}')); f = _unit(Wh[:3, 3] - C)
        r[l] = (f, _giro_puro(Wh).T @ f)
    return r


def para_onde(E, pose, l, parado, peso):
    """O antebraço deste quadro: entre a direção do parado na tela (``peso`` 0: o braço só anda junto,
    e o pulso dobra o que a mão girou) e a do parado em relação à mão (1: o pulso fica como no parado,
    mas a mão abaixando o cano levanta o cotovelo)."""
    f, fm = parado; g = _giro_puro(E.mundo(pose, E.N(f'hand_{l}'))) @ fm
    v = (1 - peso) * f + peso * g; return _unit(v)


def dobra(E, pose, lado): return conjuntos.dobra_do_pulso(E, pose, lado)


def descer_braco(E, pose, lado, quanto):
    """O braço inteiro anda ``quanto`` (m, no espaço do olho) sem girar osso nenhum: só o ombro muda de lugar."""
    cl = E.N(f'clavicle_{lado}'); pai = E.mundo(pose, E.c.parent[cl])
    pose[cl] = (pose[cl][0] + rot(pai).T @ (E.O2R[:3, :3] @ np.asarray(quanto, float)), pose[cl][1])
    E.por_local(pose, f'ik_hand_{lado}', E.mundo(pose, E.mao[lado]))


def _repouso(E):
    """O giro de repouso de cada osso, como o Godot o lê do arquivo (o do nó)."""
    return {i: np.array(n.get('rotation', (0.0, 0.0, 0.0, 1.0)), float) for i, n in enumerate(E.j['nodes'])}


def _curto(q): return -q if q[3] < 0 else q


def mistura_do_godot(E, a, b, u, repouso):
    """Duas poses misturadas como o AnimationMixer do Godot 4 mistura: de cada animação entra o giro do osso **a partir do
    repouso**, na fração do peso dela, um em cima do outro. Não é o caminho curto de uma pose para a outra: num osso
    que está perto de 180° do repouso, duas poses parecidas podem cair cada uma de um lado, e no meio da troca o osso
    passa pelo repouso."""
    um = np.array([0.0, 0.0, 0.0, 1.0]); out = {}
    for i in a:
        q0 = repouso[i]; da = _curto(qmul(qinv(q0), a[i][1])); db = _curto(qmul(qinv(q0), b[i][1]))
        q = qmul(slerp(um, da, 1 - u), slerp(um, db, u)); out[i] = (a[i][0] * (1 - u) + b[i][0] * u, qmul(q0, q / np.linalg.norm(q)))
    return out


def conferir_troca(E, quadros, de=('parado', 'andar')):
    """Quanto cada mão sai do caminho (cm) no meio da troca entre o descanso e o clipe novo, com a mistura do Godot,
    contra a linha reta de uma pose à outra. Perto de zero, a troca é o que se espera; dezenas de centímetros, o
    braço atravessa a tela."""
    repouso = _repouso(E); pior = {'r': 0.0, 'l': 0.0}; nomes = [a['name'] for a in E.j['animations']]
    for nome in de:
        if nome not in nomes: continue
        a = E.pose_do_clipe(nome, E.duracao(nome) * 0.3)
        for b in quadros[::max(1, len(quadros) // 6)]:
            m = mistura_do_godot(E, a, b, 0.5, repouso)
            for l in 'rl':
                reta = (E.mundo(a, E.mao[l])[:3, 3] + E.mundo(b, E.mao[l])[:3, 3]) / 2
                pior[l] = max(pior[l], float(np.linalg.norm(E.olho(E.mundo(m, E.mao[l]))[:3, 3] - E.olho(_em(reta))[:3, 3])) * 100)
    return pior


def _em(p):
    m = np.eye(4); m[:3, 3] = p; return m


def fazer(path, corrida, ficha=None):
    E = Esqueleto(path); P = E.parado; G0 = E.arma_de(P); tipo = tipo_de(path)
    nome = os.path.splitext(os.path.basename(path))[0]
    mao = {l: E.mao_na_arma(P, l) for l in 'rl'}
    # a mão esquerda está na arma? (a faca e a granada se seguram com uma só, e a pistola corre numa só)
    duas = tipo == 'fuzil'
    L0 = E.mundo(P, E.mao['l'])
    k = corrida.duracao(CLIPE) / DURA

    h0 = E.olho(E.mundo(P, E.mao['r']))[:3, 3]                          # a mão direita parada, no olho
    braco0 = antebraco_do_parado(E, P)

    def quadro(t):
        cam = corrida.camera(CLIPE, t * k, CAMERA)
        if tipo == 'fuzil':
            B = FUZIL_BAIXA; d, R = corrida.balanco(t * k, *B['balanco'])
            return dict(G=levar(E, G0, B['move'], B['giro'], d, R, pivo=h0), pega=mao, cam=cam)
        B = BAIXA[tipo]; d, R = corrida.balanco(t * k, *B['balanco'])
        move = (B['move'][0], (ALTURA_PISTOLA if tipo == 'pistola' and ALTURA_PISTOLA is not None else ALTURA) - h0[1], B['move'][1])
        q = dict(G=levar(E, G0, move, B['giro'], d, R, pivo=h0), pega={'r': mao['r']}, cam=cam)
        if duas: q['pega'] = mao
        elif 'desce' in B:                                              # o braço esquerdo inteiro desce (a pistola): veja BAIXA
            d2, _ = corrida.balanco(t * k, B['desce']['balanco'], 0.0, espelho=True)
            q['desce'] = np.asarray(B['desce']['move'], float) + d2 * [-1.0, 1.0, 1.0]
        else:
            F = B['livre']; d2, _ = corrida.balanco(t * k, F['balanco'], F['balanco'], espelho=True)
            d2 = d2 * [-1.0, 1.0, 1.0]                                  # o outro braço: para o outro lado
            q['livre'] = {'l': levar(E, L0, F['move'], F['giro'], d2)}
        return q

    # O pulso não dobra mais do que já dobra no parado (ou 18°, o que for maior): passou disso,
    # o antebraço vem para a linha da mão e o cotovelo e o ombro, fora da tela, se ajeitam.
    teto = {l: max(18.0, dobra(E, P, l)) for l in 'rl'}

    # o clipe: 60 quadros por segundo, o último igual ao primeiro (laço)
    n = int(round(DURA * conjuntos.FPS)); quadros = []; falta = 0.0; pior = {'r': 0.0, 'l': 0.0}
    for i in range(n + 1):
        q = quadro((i % n) / conjuntos.FPS)
        pose, f = conjuntos.posar(E, q, P); falta = max(falta, f); quadros.append(pose)
        if 'desce' in q: descer_braco(E, pose, 'l', q['desce'])
        for l in 'rl':
            if l == 'l' and 'desce' in q: pass                         # o braço esquerdo é o do parado, só mais embaixo
            elif tipo == 'fuzil': conjuntos.antebraco_pela_mao(E, pose, l, maximo=teto[l])
            else: conjuntos.antebraco_pela_mao(E, pose, l, direcao=para_onde(E, pose, l, braco0[l], BRACO))
            pior[l] = max(pior[l], dobra(E, pose, l))
    E.gravar_clipe(CLIPE, quadros, DURA)
    E.salvar()
    fora = conferir_troca(E, quadros)
    print(f"{nome} ({tipo}): {CLIPE} {DURA:.3f} s, {n + 1} quadros; alcance que faltou {falta * 10:.1f} mm; "
          f"pulso dobrado até D {pior['r']:.0f}° E {pior['l']:.0f}° (parado: D {dobra(E, P, 'r'):.0f}° E {dobra(E, P, 'l'):.0f}°); "
          f"na troca de animação a mão sai do caminho até D {fora['r']:.0f} cm E {fora['l']:.0f} cm"
          + ("  <-- O BRAÇO VAI PASSAR PELA TELA" if max(fora.values()) > 6 else ""))
    if ficha: na_ficha(ficha, nome)


def na_ficha(ficha, nome):
    """Põe o clipe na ficha de todo conjunto que usa este arquivo, e o osso da câmera (o
    AR-15 não tinha: os clipes dele não mexem a câmera, e a trilha deles é zero)."""
    d = json.load(open(ficha, encoding='utf-8'))
    for e in d.values():
        if not e.get('arquivo', '').endswith(f'/{nome}.glb'): continue
        e['animacoes'][CLIPE] = {'clipe': CLIPE, 'de': 0.0, 'ate': round(DURA, 4)}
        e.setdefault('camera', conjuntos.CAMERA)
    json.dump(d, open(ficha, 'w', encoding='utf-8'), indent=1, ensure_ascii=False, sort_keys=True)
    open(ficha, 'a', encoding='utf-8').write('\n')


if __name__ == '__main__':
    a = sys.argv[1:]; ficha = os.path.join(FP, 'bracos.json')
    if '--ficha' in a:
        i = a.index('--ficha'); ficha = a[i + 1]; del a[i:i + 2]
    if '--sem-ficha' in a: a.remove('--sem-ficha'); ficha = None
    arquivos = a or sorted(glob.glob(os.path.join(FP, '*.glb')))
    corrida = Corrida(Esqueleto(conjuntos.FONTE))
    for p in arquivos: fazer(p, corrida, ficha)
