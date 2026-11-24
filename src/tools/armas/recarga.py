#!/usr/bin/env python3
"""
A recarga do AR-15 em primeira pessoa, a partir da recarga de referência do
tutorial da InfimaGames (pedido do Jhon, 18/11: "a mão que puxa o cartucho de
balas deve vir pela frente"; 19/11: "aqui está o projeto com animações de
recarga mais precisas... deixe a animação de recarga perfeita").

    python3 tools/armas/recarga.py assets/armas/fp/ar15.glb [outro.glb ...]
        [--ficha assets/armas/fp/bracos.json] [--fonte pasta]

A fonte são dois arquivos do projeto de Unreal do tutorial ("UE5.3 Source
Files"), procurados pelo nome em ``assets/pacotes/infima/`` (fora do git) ou em
``--fonte``: ``A_FP_AssaultRifle_Reload.uasset`` (braços, 3,13 s) e
``A_FP_WEP_AssaultRifle_Reload.uasset`` (o carregador). Quem lê é o
``uasset.py``.

A referência não entra crua, porque a pose parada do Jhon não é a do tutorial
(a arma dele fica 2,9 cm mais à frente, a mão direita pega o punho 1,7 cm
adiante e a esquerda segura o guarda-mão girada 68°). O que este script faz:

- **a arma** faz o movimento da referência a partir da pose parada do Jhon;
- **o corpo e a mão direita** são os do Jhon: a mão direita fica presa no punho
  exatamente como no parado, a recarga inteira (o indicador sai do gatilho como
  na referência), e o braço é recalculado até ela (cinemática inversa de dois
  ossos, como na coronhada);
- **a mão esquerda** sai da pegada do Jhon no guarda-mão, faz o movimento da
  referência — vai ao carregador pela frente, por baixo da arma, tira, some da
  tela, traz o novo por baixo e encaixa — e pousa de volta na pegada do Jhon.
  A diferença entre as duas pegadas some enquanto a mão vai e reaparece
  enquanto ela volta (``SAIDA`` e ``VOLTA``), nunca com a mão parada; o ombro
  e o cotovelo trocam de lugar em linha reta, mais devagar (``SAIDA_BRACO``);
- **o carregador** segue a referência no espaço do fuzil, com o encaixe do jogo;
- **o tempo** é o da recarga do jogo (2,3 s, ``DURA``): o fim morto da referência
  (depois de 2,8 s) sai, e os dois momentos que fazem barulho ficam em cima das
  batidas do som ``recarga_ar15`` (``TEMPOS``: o carregador sai aos 0,32 s e o
  novo encaixa aos 1,42 s).

O clipe começa e termina exatamente na pose de ``parado``. Tudo sai de
``parado`` + a referência, então rodar de novo dá o mesmo arquivo.
"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'rugido'))
from mira import Conjunto, matq, qmat, trs
from coronhada import rot, arco
import uasset

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(AQUI))
FONTE = os.path.join(RAIZ, 'assets', 'pacotes', 'infima')
BRACOS = 'A_FP_AssaultRifle_Reload.uasset'
FUZIL = 'A_FP_WEP_AssaultRifle_Reload.uasset'

CLIPE = 'recarga'
FPS = 60.0
DURA = 2.3
# (tempo na referência, tempo no jogo): começo, o carregador sai, o novo
# encaixa, fim (o resto da referência é a arma parada).
TEMPOS = [(0.0, 0.0), (0.42, 0.32), (1.90, 1.42), (2.80, DURA)]
# Tempo da referência em que a mão esquerda vai do guarda-mão ao carregador, e
# em que volta dele ao guarda-mão.
SAIDA = (0.0, 0.24)
VOLTA = (2.28, 2.52)
# O mesmo para o ombro e o cotovelo esquerdos, que trocam de lugar mais devagar
# (o ombro do Jhon fica a 8 cm do da referência).
SAIDA_BRACO = (0.0, 0.44)
VOLTA_BRACO = (2.06, 2.54)
# Ossos que a referência mexe e o jogo não usa (a câmera do jogo é fixa).
FORA = ('root', 'head', 'ik_hand_gun', 'ik_hand_l', 'ik_hand_r')


# ---------------------------------------------------------------- contas
def suave(u):
    u = min(max(u, 0.0), 1.0)
    return u * u * u * (u * (u * 6 - 15) + 10)

def qmul(a, b):
    x1, y1, z1, w1 = a; x2, y2, z2, w2 = b
    return np.array([w1 * x2 + x1 * w2 + y1 * z2 - z1 * y2, w1 * y2 - x1 * z2 + y1 * w2 + z1 * x2,
                     w1 * z2 + x1 * y2 - y1 * x2 + z1 * w2, w1 * w2 - x1 * x2 - y1 * y2 - z1 * z2])

def qinv(q): return np.array([-q[0], -q[1], -q[2], q[3]])

def slerp(a, b, u):
    if np.dot(a, b) < 0: b = -b
    d = float(np.clip(np.dot(a, b), -1, 1))
    if d > 0.9995:
        q = a + (b - a) * u; return q / np.linalg.norm(q)
    th = np.arccos(d)
    return (np.sin((1 - u) * th) * a + np.sin(u * th) * b) / np.sin(th)

UM = np.array([0.0, 0.0, 0.0, 1.0])

def mistura(m, u):
    """A 4×4 rígida m aplicada só em parte (0 = nada, 1 = inteira)."""
    return trs(m[:3, 3] * u, slerp(UM, matq(m[:3, :3]), u))

def relogio(t):
    """Tempo do jogo -> tempo da referência: cúbica monótona pelos TEMPOS."""
    xs = np.array([b for _, b in TEMPOS]); ys = np.array([a for a, _ in TEMPOS])
    h = np.diff(xs); d = np.diff(ys) / h
    m = np.zeros(len(xs)); m[0], m[-1] = d[0], d[-1]
    for i in range(1, len(xs) - 1):
        m[i] = 0.0 if d[i - 1] * d[i] <= 0 else 3 * (h[i - 1] + h[i]) / ((2 * h[i] + h[i - 1]) / d[i - 1] + (h[i] + 2 * h[i - 1]) / d[i])
    i = int(np.clip(np.searchsorted(xs, t, side='right') - 1, 0, len(xs) - 2))
    u = (t - xs[i]) / h[i]
    return ((2 * u ** 3 - 3 * u ** 2 + 1) * ys[i] + (u ** 3 - 2 * u ** 2 + u) * h[i] * m[i]
            + (-2 * u ** 3 + 3 * u ** 2) * ys[i + 1] + (u ** 3 - u ** 2) * h[i] * m[i + 1])

def achar(nome, fonte):
    for base, _, arquivos in os.walk(fonte):
        for a in arquivos:
            if a.lower() == nome.lower(): return os.path.join(base, a)
    raise SystemExit(f"falta {nome} em {fonte} (veja docs/PACOTES.md)")


def compactar(g):
    """Tira do .glb os dados que nada mais usa (as chaves do clipe trocado),
    para o arquivo não inchar a cada vez que o script roda."""
    j = g.json
    usados = set()
    for m in j.get('meshes', []):
        for p in m['primitives']:
            usados.update(p['attributes'].values())
            if 'indices' in p: usados.add(p['indices'])
            for alvo in p.get('targets', []): usados.update(alvo.values())
    for sk in j.get('skins', []):
        if 'inverseBindMatrices' in sk: usados.add(sk['inverseBindMatrices'])
    for a in j.get('animations', []):
        for sm in a['samplers']: usados.update((sm['input'], sm['output']))
    assert not any('sparse' in j['accessors'][i] for i in usados)
    novo_acc = {v: k for k, v in enumerate(sorted(usados))}
    vistas = sorted({j['accessors'][i]['bufferView'] for i in usados} | {im['bufferView'] for im in j.get('images', []) if 'bufferView' in im})
    novo_bv = {v: k for k, v in enumerate(vistas)}
    bin_ = bytearray(); bvs = []
    for v in vistas:
        bv = dict(j['bufferViews'][v]); o = bv.get('byteOffset', 0)
        while len(bin_) % 4: bin_.append(0)
        bv['byteOffset'] = len(bin_); bin_ += g.bin[o:o + bv['byteLength']]; bvs.append(bv)
    accs = []
    for i in sorted(usados):
        a = dict(j['accessors'][i]); a['bufferView'] = novo_bv[a['bufferView']]; accs.append(a)
    for m in j.get('meshes', []):
        for p in m['primitives']:
            p['attributes'] = {k: novo_acc[v] for k, v in p['attributes'].items()}
            if 'indices' in p: p['indices'] = novo_acc[p['indices']]
            if 'targets' in p: p['targets'] = [{k: novo_acc[v] for k, v in alvo.items()} for alvo in p['targets']]
    for sk in j.get('skins', []):
        if 'inverseBindMatrices' in sk: sk['inverseBindMatrices'] = novo_acc[sk['inverseBindMatrices']]
    for a in j.get('animations', []):
        for sm in a['samplers']: sm['input'] = novo_acc[sm['input']]; sm['output'] = novo_acc[sm['output']]
    for im in j.get('images', []):
        if 'bufferView' in im: im['bufferView'] = novo_bv[im['bufferView']]
    j['accessors'] = accs; j['bufferViews'] = bvs; g.bin = bin_


class Referencia:
    """A recarga do tutorial: por osso, a posição e a rotação locais em qualquer instante."""
    def __init__(self, fonte):
        self.ossos, self.n, self.fps = uasset.curvas(achar(BRACOS, fonte))
        arma, n, _ = uasset.curvas(achar(FUZIL, fonte)); assert n == self.n
        self.carregador = arma['Magazine']

    def em(self, curva, tau):
        T, Q = curva; x = min(max(tau * self.fps, 0.0), self.n - 1.0)
        i = min(int(x), self.n - 2); u = x - i
        return T[i] * (1 - u) + T[i + 1] * u, slerp(Q[i], Q[i + 1], u)

    def local(self, nome, tau): return self.em(self.ossos[nome], tau)


def fazer(path, ref, ficha=None):
    c = Conjunto(path, limpar=False); j = c.j
    parado = c.clip('parado'); N = c.names.index
    raiz = N('root')
    filhos = {}
    for filho, pai in c.parent.items(): filhos.setdefault(pai, []).append(filho)
    def abaixo(i):
        out = [i]
        for f in filhos.get(i, []): out += abaixo(f)
        return out
    esquerda = set(abaixo(N('clavicle_l')))
    torcao = esquerda - set(abaixo(N('hand_l')))          # o braço e os ossos de torção, sem a mão

    # A pose parada do Jhon (o começo de "parado"): local de cada nó.
    def do_parado(i):
        n = j['nodes'][i]
        T = parado[(i, 'translation')]['v'][0] if (i, 'translation') in parado else np.array(n.get('translation', [0, 0, 0]), float)
        Q = parado[(i, 'rotation')]['v'][0] if (i, 'rotation') in parado else np.array(n.get('rotation', [0, 0, 0, 1]), float)
        return np.array(T, float), np.array(Q, float)
    P = {i: do_parado(i) for i in abaixo(raiz) if i != raiz}

    def mundo(pose, i):
        """O nó no espaço da raiz do esqueleto (cm), com a pose dada."""
        m = np.eye(4)
        while i != raiz:
            m = trs(*pose[i]) @ m; i = c.parent[i]
        return m

    arma, mao_d, mao_e = N('ik_hand_gun'), N('hand_r'), N('hand_l')
    Gu = mundo(P, arma)
    # A referência no começo, e o deslocamento rígido que põe a arma dela na do Jhon.
    def pose_ref(tau):
        pose = dict(P)
        for nome in ref.ossos:
            if nome != 'root' and nome in c.names: pose[N(nome)] = ref.local(nome, tau)
        return pose
    R0 = pose_ref(0.0); Ge0 = mundo(R0, arma)
    delta = Gu @ np.linalg.inv(Ge0)
    pega_d = np.linalg.inv(Gu) @ mundo(P, mao_d)                       # a mão direita do Jhon, no espaço da arma
    pega_e = np.linalg.inv(Gu) @ mundo(P, mao_e)                       # a esquerda do Jhon no guarda-mão
    pega_e_ref = np.linalg.inv(Ge0) @ mundo(R0, mao_e)                 # a esquerda da referência no guarda-mão
    ajuste_e = np.linalg.inv(pega_e_ref) @ pega_e                      # de uma pegada para a outra, no espaço da mão

    carregador = N('Magazine')
    encaixe = np.linalg.inv(trs(ref.carregador[0][0], ref.carregador[1][0])) @ trs(*P[carregador])

    def peso(tau, saida, volta):
        """Quanto da pose do Jhon vale para o lado esquerdo: 1 nas pontas, 0 no meio."""
        return max(1 - suave((tau - saida[0]) / (saida[1] - saida[0])), suave((tau - volta[0]) / (volta[1] - volta[0])))

    ossos_braco = [N(f'{o}_l') for o in ('clavicle', 'upperarm', 'lowerarm')]
    def braco_esquerdo(pose, Rt, k, alvo):
        """O ombro, o cotovelo e a orientação do braço esquerdo entre o do Jhon
        (k = 1) e o da referência (k = 0), em linha reta — misturar as rotações
        da clavícula (150° de uma para a outra) joga o ombro por um arco."""
        cl, ua, la = ossos_braco; hd = mao_e
        A = dict(pose); B = dict(pose)
        for i in (cl, ua, la, hd): A[i] = Rt[i]; B[i] = P[i]
        WA = [mundo(A, i) for i in (cl, ua, la, hd)]; WB = [mundo(B, i) for i in (cl, ua, la, hd)]
        mq = lambda a, b: qmat(slerp(matq(rot(a)), matq(rot(b)), k))
        Rc, Ru, Rl = (mq(WA[n], WB[n]) for n in range(3))
        S = WA[1][:3, 3] * (1 - k) + WB[1][:3, 3] * k
        E = WA[2][:3, 3] * (1 - k) + WB[2][:3, 3] * k
        # Se a mão não alcança o alvo deste ombro, o ombro vai junto.
        alcance = (np.linalg.norm(P[la][0]) + np.linalg.norm(P[hd][0])) * 0.995
        d = np.linalg.norm(alvo[:3, 3] - S)
        if d > alcance: S = S + (alvo[:3, 3] - S) / d * (d - alcance)
        pai = mundo(pose, c.parent[cl])
        Tc = np.linalg.inv(pai) @ np.append(S - Rc @ P[ua][0], 1.0)
        pose[cl] = (Tc[:3], matq(rot(pai).T @ Rc))
        pose[ua] = (P[ua][0], matq(Rc.T @ Ru))
        pose[la] = (P[la][0], matq(Ru.T @ Rl))
        # O cotovelo e o pulso de partida para o cálculo do braço: os misturados.
        return (E,)

    def braco(pose, lado, alvo, partida=None):
        """Leva a mão ao alvo (4×4 no espaço da raiz) recalculando o braço; devolve o que faltou alcançar."""
        cl, ua, la, hd = (N(f'{o}_{lado}') for o in ('clavicle', 'upperarm', 'lowerarm', 'hand'))
        Wc, Wu, Wl, Wh = (mundo(pose, i) for i in (cl, ua, la, hd))
        S, E0, H0 = Wu[:3, 3], Wl[:3, 3], Wh[:3, 3]
        if partida is not None:
            # A orientação misturada aponta o osso para um lado; o cotovelo
            # misturado dá o lado para onde o braço dobra.
            E0 = S + rot(Wu) @ P[la][0]; H0 = E0 + rot(Wl) @ P[hd][0]
            dobra = partida[0]
        else: dobra = E0
        L1, L2 = np.linalg.norm(E0 - S), np.linalg.norm(H0 - E0)
        H = alvo[:3, 3].copy(); d = np.linalg.norm(H - S); falta = max(0.0, d - (L1 + L2) * 0.999)
        if falta > 0: H = S + (H - S) / d * (L1 + L2) * 0.999; d = (L1 + L2) * 0.999
        eixo = (H - S) / d
        a = (L1 ** 2 - L2 ** 2 + d * d) / (2 * d); h = np.sqrt(max(L1 ** 2 - a * a, 0.0))
        polo = dobra - S; polo = polo - np.dot(polo, eixo) * eixo
        if np.linalg.norm(polo) < 1e-6: polo = np.cross(eixo, [0.0, 0.0, 1.0])
        polo /= np.linalg.norm(polo)
        E = S + eixo * a + polo * h
        Ru = arco(E0 - S, E - S) @ rot(Wu); Rl = arco(H0 - E0, H - E) @ rot(Wl)
        pose[ua] = (pose[ua][0], matq(rot(Wc).T @ Ru))
        pose[la] = (pose[la][0], matq(Ru.T @ Rl))
        pose[hd] = (pose[hd][0], matq(Rl.T @ rot(alvo)))
        return falta

    n = int(round(DURA * FPS)) + 1
    ts = np.arange(n) / FPS; ts[-1] = DURA
    quadros = []; falta_max = 0.0
    for t in ts:
        tau = relogio(float(t)); w = peso(tau, SAIDA, VOLTA); wb = peso(tau, SAIDA_BRACO, VOLTA_BRACO)
        Rt = pose_ref(tau)
        pose = dict(P)
        # 1. Cada osso: o do Jhon mais o que a referência mexe; a esquerda, a da
        #    referência mais (em parte) a diferença para a do Jhon.
        for nome in ref.ossos:
            if nome in FORA or nome not in c.names: continue
            i = N(nome); (Te, Qe), (Te0, Qe0), (Tp, Qp) = Rt[i], R0[i], P[i]
            k = 1.0 if i not in esquerda else wb if i in torcao else w
            pose[i] = (Te + k * (Tp - Te0), qmul(Qe, slerp(UM, qmul(qinv(Qe0), Qp), k)))
        # 2. A arma: o movimento da referência, a partir da arma do Jhon.
        Ge = mundo(Rt, arma); G = delta @ Ge
        m = np.linalg.inv(mundo(pose, c.parent[arma])) @ G
        pose[arma] = (m[:3, 3], matq(rot(m)))
        # 3. As mãos, no espaço da arma.
        alvo_d = G @ pega_d
        alvo_e = G @ (np.linalg.inv(Ge) @ mundo(Rt, mao_e)) @ mistura(ajuste_e, w)
        partida = braco_esquerdo(pose, Rt, wb, alvo_e)
        falta_max = max(falta_max, braco(pose, 'r', alvo_d), braco(pose, 'l', alvo_e, partida))
        for lado, mao in (('l', mao_e), ('r', mao_d)):
            if f'ik_hand_{lado}' in c.names:
                m = np.linalg.inv(G) @ mundo(pose, mao); pose[N(f'ik_hand_{lado}')] = (m[:3, 3], matq(rot(m)))
        # 4. O carregador.
        m = trs(*ref.em(ref.carregador, tau)) @ encaixe
        pose[carregador] = (m[:3, 3], matq(rot(m)))
        quadros.append(pose)

    # As pontas são a pose parada, exata (a referência acaba a 0,3° dela).
    for k, t in enumerate(ts):
        u = max(1 - suave(t / 0.05), suave((t - (DURA - 0.2)) / 0.2))
        if u <= 0: continue
        for i, (T, Q) in quadros[k].items():
            Tp, Qp = P[i]; quadros[k][i] = (T * (1 - u) + Tp * u, slerp(Q, Qp, u))

    # Grava: os mesmos canais de "parado"; o que não muda fica parado.
    an = [a for a in j['animations'] if a['name'] == CLIPE][0]
    t_in = c.add_times(ts); dois = c.add_times([0.0, DURA])
    samplers, channels = [], []
    def por(node, caminho, entrada, saida, interp):
        samplers.append({'input': entrada, 'output': saida, 'interpolation': interp})
        channels.append({'sampler': len(samplers) - 1, 'target': {'node': node, 'path': caminho}})
    for (node, caminho), ch in parado.items():
        v = None
        if node in P and caminho in ('translation', 'rotation'):
            v = np.array([q[node][0 if caminho == 'translation' else 1] for q in quadros], float)
            if caminho == 'rotation':
                for i in range(1, n):
                    if np.dot(v[i], v[i - 1]) < 0: v[i] = -v[i]
            if np.abs(v - v[0]).max() < 1e-7: v = None
        if v is None:
            tipo = {1: 'SCALAR', 3: 'VEC3', 4: 'VEC4'}[ch['v'].shape[1]]
            por(node, caminho, dois, c.g.add_acc(np.stack([ch['v'][0], ch['v'][0]]), tipo), 'STEP')
        else:
            por(node, caminho, t_in, c.g.add_acc(v, 'VEC3' if caminho == 'translation' else 'VEC4'), 'LINEAR')
    an['channels'] = channels; an['samplers'] = samplers
    an['extras'] = {'recarga': 'referencia_infima', 'duracao': DURA}
    compactar(c.g)
    c.g.save(path)

    # Confere, lendo o arquivo gravado.
    f = Conjunto(path, limpar=False); novo = f.clip(CLIPE); W = lambda i, t: f.world(novo, i, t)
    def uni(m):
        o = m.copy(); o[:3, :3] = rot(m); return o
    pontas = max(min(np.abs(novo[k]['v'][e] - parado[k]['v'][0]).max(), np.abs(novo[k]['v'][e] + parado[k]['v'][0]).max() if k[1] == 'rotation' else 9.0)
                 for k in novo for e in (0, -1))
    g0 = np.linalg.inv(uni(W(arma, 0))) @ uni(W(mao_d, 0))
    direita = max(np.linalg.norm((np.linalg.inv(uni(W(arma, t))) @ uni(W(mao_d, t)))[:3, 3] - g0[:3, 3]) for t in ts)
    segura = [np.linalg.norm(W(carregador, t)[:3, 3] - W(mao_e, t)[:3, 3]) for t in ts if 0.47 <= relogio(float(t)) <= 1.9]
    passo = max(np.linalg.norm(W(mao_e, b)[:3, 3] - W(mao_e, a)[:3, 3]) for a, b in zip(ts, ts[1:]))
    print(f"{os.path.basename(path)}: recarga {DURA:.1f} s, {n} quadros; pontas a {pontas:.5f} do parado; "
          f"mão direita sai do punho {direita * 1000:.2f} mm; carregador na mão esquerda varia {(max(segura) - min(segura)) * 1000:.2f} mm; "
          f"alcance que faltou {falta_max * 10:.1f} mm; maior passo da mão esquerda {passo * 100:.1f} cm/quadro")

    if ficha:
        dados = json.load(open(ficha, encoding='utf-8'))
        for chave, e in dados.items():
            if isinstance(e, dict) and e.get('arquivo', '').endswith('/' + os.path.basename(path)):
                e['animacoes'][CLIPE] = {'clipe': CLIPE, 'de': 0.0, 'ate': DURA}
        json.dump(dados, open(ficha, 'w', encoding='utf-8'), indent=1, ensure_ascii=False, sort_keys=True); open(ficha, 'a', encoding='utf-8').write('\n')


if __name__ == '__main__':
    args = sys.argv[1:]; ficha = None; fonte = FONTE
    if '--ficha' in args:
        k = args.index('--ficha'); ficha = args[k + 1]; del args[k:k + 2]
    if '--fonte' in args:
        k = args.index('--fonte'); fonte = args[k + 1]; del args[k:k + 2]
    ref = Referencia(fonte)
    for p in args: fazer(p, ref, ficha)
