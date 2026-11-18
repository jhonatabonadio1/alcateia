"""O rugido do lobo, refeito de corpo inteiro (pedido do Jhon, 18/11: "mais
agressivo e intimidador, com a mesma duração, fluido e natural").

Antes ele uivava para o céu, de braços abertos. Agora é um rugido na cara do
inimigo, em quatro tempos, nos mesmos 1,533 s:

1. arma (0 a 0,22 s): afunda, curva o tronco, recolhe os braços ao peito e
   abaixa a cabeça — puxa o ar;
2. explode (0,22 a 0,42 s): um pé dá um passo curto à frente e bate no chão,
   o quadril avança, o peito abre, os braços abrem para os lados com as
   garras à frente e a cabeça é jogada para cima e para a frente;
3. sustenta (0,42 a 1,08 s): inclinado para a frente, baixo, um pé à frente
   do outro e os dois chapados no chão, varrendo a cabeça de um lado ao
   outro, tremendo de força;
4. volta (1,08 a 1,53 s): recolhe e fica de pé, na pose em que começou.

O clipe é montado em cima do primeiro quadro do "Howl" de lobo_anim.glb (a
pose parada): tudo é diferença em relação a ele, então o primeiro e o último
quadro continuam os de sempre e a mistura com o parado não muda. Os números
do movimento estão logo abaixo. Rodar de novo dá o mesmo resultado.

uso: python3 rugido2.py entrada.glb saida.glb [--previa figura.png]
"""
import sys, numpy as np
from scipy.interpolate import PchipInterpolator
from fk import *

SRC, DST = sys.argv[1], sys.argv[2]
PREVIA = sys.argv[sys.argv.index('--previa') + 1] if '--previa' in sys.argv else None
r = Rig(SRC, 'Howl')
T = r.times
END = float(T[-1])
X = np.array([1.0, 0, 0]); Y = np.array([0, 1.0, 0]); Z = np.array([0, 0, 1.0])

def curve(keys):
    ts = [k[0] for k in keys]; vs = np.array([k[1] for k in keys], float)
    f = PchipInterpolator(ts, vs, axis=0)
    return lambda t: f(min(max(t, ts[0]), ts[-1]))

# ---- o desenho do movimento (s; metros; graus) ---------------------------
A, B_, S0, S1, R0 = 0.22, 0.42, 0.60, 1.08, 1.36          # arma, explode, sustenta (de/até), de pé
# Ajuste do Jhon (18/11, depois de ver no jogo): "desça um pouco a postura dele" — o quadril fica
# mais baixo do começo ao fim do rugido (era -0,125 no sustento) e avança menos, com o peso entre os pés.
hip_y = curve([(0, 0), (A, -0.190), (B_, -0.110), (S0, -0.180), (S1, -0.175), (R0, -0.03), (END, 0)])
hip_z = curve([(0, 0), (A, -0.070), (B_, 0.150), (S0, 0.130), (S1, 0.120), (R0, 0.02), (END, 0)])
hip_x = curve([(0, 0), (A, 0.020), (B_, -0.030), (S1, -0.030), (R0, 0), (END, 0)])
# tronco: + curva para a frente. Arma curvado, abre o peito na explosão, e ruge inclinado.
lean = curve([(0, 0), (A, 34), (B_ - 0.04, 8), (B_, 0), (S0, 25), (S1, 28), (R0, 4), (END, 0)])
# varredura: o tronco e a cabeça viram de um lado ao outro no rugido (+ = para a esquerda dele)
sweep = curve([(0, 0), (A, 0), (B_, -5), (S0, -11), (0.84, 2), (S1, 12), (R0, 2), (END, 0)])
# cabeça, em relação ao peito: + abaixa. Recolhe, é jogada para cima e fica à frente, de boca no inimigo.
head = curve([(0, 0), (A, 22), (B_ - 0.03, -30), (B_ + 0.03, -42), (S0, -33), (S1, -32), (R0, -4), (END, 0)])
# inclinação de lado da cabeça (rosnado): pende para o lado da varredura
tilt = curve([(0, 0), (A, 0), (B_, 5), (S0, 9), (0.84, -2), (S1, -9), (R0, 0), (END, 0)])

# braços: para onde aponta o braço e o antebraço, no espaço do peito (x para fora, y para cima, z para a frente)
arm = curve([(0, None), (A, (0.34, -0.70, -0.62)), (B_, (0.84, -0.36, -0.28)), (B_ + 0.07, (0.80, -0.50, -0.20)),
             (S1, (0.82, -0.46, -0.24)), (R0, None), (END, None)]) if False else None
ARM_KEYS = [(A, (0.34, -0.70, -0.62)), (B_, (0.88, -0.30, -0.34)), (B_ + 0.09, (0.80, -0.55, -0.16)), (S1, (0.82, -0.52, -0.20))]
FORE_KEYS = [(A, (-0.20, 0.30, 0.93)), (B_, (0.42, 0.05, 0.90)), (B_ + 0.09, (0.30, -0.42, 0.86)), (S1, (0.38, -0.48, 0.79))]
ARM_LAG = {1.0: 0.0, -1.0: 0.03}                          # o braço direito chega um instante depois

# o pé da frente (o direito: parado ele já está à frente): levanta, avança e BATE no chão junto com a explosão
LEAD = 'Right'
# Ajuste do Jhon (18/11): "a perna dele no uivo tá meio torta, só avance uma das pernas um pouco para
# frente". O que entortava era o pé de trás na ponta, virado para fora, com o joelho aberto. Agora um pé
# avança (um passo curto, quase reto) e os dois ficam chapados no chão, com os joelhos para a frente.
lead_z = curve([(0, 0), (A, 0), (B_, 0.27), (S1, 0.27), (R0, 0), (END, 0)])
lead_x = curve([(0, 0), (A, 0), (B_, 0.035), (S1, 0.035), (R0, 0), (END, 0)])
lead_y = curve([(0, 0), (A, 0.01), (0.32, 0.15), (B_ - 0.03, 0.07), (B_, 0), (S1, 0), (1.22, 0.06), (R0, 0), (END, 0)])
lead_pitch = curve([(0, 0), (A, 0), (0.33, -22), (B_, 0), (S1, 0), (1.22, 10), (R0, 0), (END, 0)])
# o pé de trás: fica e sobe na ponta, empurrando
rear_heel = curve([(0, 0), (A, 3), (B_ + 0.04, 12), (S1, 10), (R0, 0), (END, 0)])
rear_out = curve([(0, 0), (A, 0), (B_ + 0.04, 4), (S1, 4), (R0, 0), (END, 0)])
knee_out = curve([(0, 0), (A, 0.22), (B_, 0.10), (S1, 0.06), (R0, 0.02), (END, 0)])

def gate(t):
    """O auge do rugido: sobe em 0,06 s na explosão e some antes de voltar."""
    return max(0.0, min((t - B_ + 0.02) / 0.06, 1.0)) * max(0.0, min((S1 + 0.06 - t) / 0.14, 1.0))

def tremor(t, f, fase=0.0):
    return gate(t) * (0.65 * np.sin(2 * np.pi * f * t + fase) + 0.35 * np.sin(2 * np.pi * f * 1.63 * t + fase * 2.1))

def dir_at(keys, d0, t, lag):
    """Direção (no espaço do peito) no tempo t: parte da pose parada, passa pelas chaves e volta."""
    t = t - lag
    pts = [(0.0, d0)] + [(k, np.array(v, float) / np.linalg.norm(v)) for k, v in keys] + [(R0 + 0.02, d0), (END + 0.1, d0)]
    ts = [p[0] for p in pts]; vs = np.array([p[1] for p in pts])
    v = PchipInterpolator(ts, vs, axis=0)(min(max(t, 0.0), END + 0.1))
    return v / np.linalg.norm(v)

names = ['Hips', 'Spine', 'Spine1', 'Spine2', 'Neck', 'Head', 'HeadTop_End', 'LeftShoulder', 'LeftArm', 'LeftForeArm',
         'LeftHand', 'RightShoulder', 'RightArm', 'RightForeArm', 'RightHand', 'LeftUpLeg', 'LeftLeg', 'LeftFoot',
         'LeftToeBase', 'RightUpLeg', 'RightLeg', 'RightFoot', 'RightToeBase']
B = {k: r.idx(k) for k in names}
B['headfront'] = r.names.index('headfront')
W0 = {k: r.world(B[k], 0) for k in B}                       # a pose parada: tudo parte dela
P0 = {k: W0[k][0] for k in B}; Q0 = {k: W0[k][1] for k in B}
pp, pq, ps = r.world(r.parent[B['Hips']], 0)

newR = {B[k]: np.zeros((r.n, 4)) for k in B}
newT = np.zeros((r.n, 3))
frames = []

def rot(deg_x=0.0, deg_y=0.0, deg_z=0.0):
    q = np.array([0, 0, 0, 1.0])
    for ax, a in ((Y, deg_y), (X, deg_x), (Z, deg_z)):
        if a: q = qmul(q, qaxis(ax, np.radians(a)))
    return q

for f in range(r.n):
    t = float(T[f])
    Wq = {}; Wp = {}
    g = gate(t)

    # ---- tronco: a curva se divide do quadril ao peito; a varredura também
    L, Sw = float(lean(t)), float(sweep(t))
    C = {}
    C['Hips'] = rot(L * 0.30, Sw * 0.20)
    C['Spine'] = qmul(C['Hips'], rot(L * 0.25 + 0.5 * tremor(t, 11.0), Sw * 0.20))
    C['Spine1'] = qmul(C['Spine'], rot(L * 0.25, Sw * 0.25))
    C['Spine2'] = qmul(C['Spine1'], rot(L * 0.20 + 0.8 * tremor(t, 12.5, 1.0), Sw * 0.35, 0.8 * tremor(t, 9.0, 2.0)))
    Hd, Ti = float(head(t)), float(tilt(t))
    C['Neck'] = qmul(C['Spine2'], rot(Hd * 0.40, Sw * 0.45, Ti * 0.4))
    C['Head'] = qmul(C['Neck'], rot(Hd * 0.60 + 2.6 * tremor(t, 13.0, 0.5), Sw * 0.55 + 1.6 * tremor(t, 10.0, 1.7), Ti * 0.6 + 2.2 * tremor(t, 15.0, 2.9)))
    C['HeadTop_End'] = C['Head']; C['headfront'] = C['Head']

    hp = P0['Hips'] + np.array([float(hip_x(t)), float(hip_y(t)) + 0.006 * tremor(t, 8.0), float(hip_z(t))])
    Wp['Hips'] = hp
    # posição de cada junta do tronco: gira em volta da junta do pai
    chain = [('Hips', 'Spine'), ('Spine', 'Spine1'), ('Spine1', 'Spine2'), ('Spine2', 'Neck'), ('Neck', 'Head'),
             ('Head', 'HeadTop_End'), ('Head', 'headfront'), ('Spine2', 'LeftShoulder'), ('Spine2', 'RightShoulder')]
    for a, b in chain:
        Wp[b] = Wp[a] + qrot(C[a], P0[b] - P0[a])
    for k in ('Hips', 'Spine', 'Spine1', 'Spine2', 'Neck', 'Head', 'HeadTop_End', 'headfront'):
        Wq[k] = qnorm(qmul(C[k], Q0[k]))

    # ---- braços: ombro sobe um pouco no rugido; braço e antebraço apontam para os alvos
    chest = C['Spine2']
    for side, sign in (('Left', 1.0), ('Right', -1.0)):
        lag = ARM_LAG[sign]
        shrug = rot(0, 0, sign * 9.0 * g)
        Cs = qmul(chest, shrug)
        Wq[side + 'Shoulder'] = qnorm(qmul(Cs, Q0[side + 'Shoulder']))
        Wp[side + 'Arm'] = Wp[side + 'Shoulder'] + qrot(Cs, P0[side + 'Arm'] - P0[side + 'Shoulder'])

        m = np.array([sign, 1.0, 1.0])
        d0a = (P0[side + 'ForeArm'] - P0[side + 'Arm']); la = np.linalg.norm(d0a); d0a /= la
        d0f = (P0[side + 'Hand'] - P0[side + 'ForeArm']); lf = np.linalg.norm(d0f); d0f /= lf
        ja = 0.035 * tremor(t, 10.5, 1.0 + sign); jf = 0.05 * tremor(t, 14.0, 2.0 - sign)
        ta = dir_at([(k, np.array(v) * m) for k, v in ARM_KEYS], d0a, t, lag) + np.array([0, ja, ja * 0.5])
        tf = dir_at([(k, np.array(v) * m) for k, v in FORE_KEYS], d0f, t, lag) + np.array([jf * sign, jf, 0])
        ta = qrot(chest, ta / np.linalg.norm(ta)); tf = qrot(chest, tf / np.linalg.norm(tf))

        Ca = qmul(qfrom(qrot(Cs, d0a), ta), Cs)
        Wq[side + 'Arm'] = qnorm(qmul(Ca, Q0[side + 'Arm']))
        Wp[side + 'ForeArm'] = Wp[side + 'Arm'] + ta * la
        Cf = qmul(qfrom(qrot(Ca, d0f), tf), Ca)
        Wq[side + 'ForeArm'] = qnorm(qmul(Cf, Q0[side + 'ForeArm']))
        Wp[side + 'Hand'] = Wp[side + 'ForeArm'] + tf * lf
        Wq[side + 'Hand'] = qnorm(qmul(Cf, Q0[side + 'Hand']))

    # ---- pernas: o quadril manda, os pés ficam (ou batem) no chão
    hq = Wq['Hips']
    for side, sign in (('Left', 1.0), ('Right', -1.0)):
        up0, kn0, an0, toe0 = (P0[side + s] for s in ('UpLeg', 'Leg', 'Foot', 'ToeBase'))
        qup0, qkn0, qan0, qtoe0 = (Q0[side + s] for s in ('UpLeg', 'Leg', 'Foot', 'ToeBase'))
        L1 = np.linalg.norm(kn0 - up0); L2 = np.linalg.norm(an0 - kn0)
        H = hp + qrot(C['Hips'], up0 - P0['Hips'])
        if side == LEAD:
            turn = qaxis(X, np.radians(float(lead_pitch(t))))
            An = an0 + np.array([sign * float(lead_x(t)), float(lead_y(t)), float(lead_z(t))])
            toe_free = float(lead_y(t)) > 0.005
        else:
            turn = qmul(qaxis(Y, np.radians(sign * float(rear_out(t)))), qaxis(X, np.radians(float(rear_heel(t)))))
            An = toe0 + qrot(turn, an0 - toe0)
            toe_free = False
        qfoot = qnorm(qmul(turn, qan0))
        d_vec = An - H; d = np.linalg.norm(d_vec)
        reach = 0.995 * (L1 + L2)
        if d > reach:
            An = H + d_vec / d * reach; d_vec = An - H; d = reach
        n = d_vec / d
        # Para onde o joelho dobra: para a frente (como ele já dobra parado) e, no rugido, aberto para
        # fora. A direção de parado é tirada FORA da linha quadril-tornozelo; usar o joelho inteiro, como
        # antes, deixava o joelho sem lado certo quando o pé ia longe do quadril (dobrava para trás).
        n0 = (an0 - up0) / np.linalg.norm(an0 - up0)
        fwd = (kn0 - up0) - np.dot(kn0 - up0, n0) * n0; fwd /= np.linalg.norm(fwd)
        pole = fwd + float(knee_out(t)) * np.array([sign, 0, 0])
        p = pole - np.dot(pole, n) * n; p /= np.linalg.norm(p)
        a = (L1 * L1 - L2 * L2 + d * d) / (2 * d)
        h = np.sqrt(max(L1 * L1 - a * a, 0.0))
        K = H + a * n + h * p
        Wq[side + 'UpLeg'] = qnorm(qmul(qfrom(kn0 - up0, K - H), qup0))
        Wq[side + 'Leg'] = qnorm(qmul(qfrom(an0 - kn0, An - K), qkn0))
        Wq[side + 'Foot'] = qfoot
        Wq[side + 'ToeBase'] = qnorm(qmul(turn, qtoe0)) if toe_free else qtoe0
        Wp[side + 'UpLeg'] = H; Wp[side + 'Leg'] = K; Wp[side + 'Foot'] = An
        Wp[side + 'ToeBase'] = An + qrot(qmul(qfoot, qinv(qan0)), toe0 - an0)

    # ---- do mundo para o osso: a rotação de cada um em relação ao pai
    newT[f] = qrot(qinv(pq), hp - pp) / ps
    for k in B:
        par = r.parent[B[k]]
        pk = next((n_ for n_ in B if B[n_] == par), None)
        qparent = Wq[pk] if pk else pq
        newR[B[k]][f] = qnorm(qmul(qinv(qparent), Wq[k]))
    frames.append({k: v.copy() for k, v in Wp.items()})

# quaternions contínuos, e do mesmo lado dos originais no primeiro quadro
for node, arr in newR.items():
    for f in range(1, r.n):
        if np.dot(arr[f], arr[f - 1]) < 0: arr[f] = -arr[f]
    if np.dot(arr[0], r.R[node][0]) < 0: arr[:] = -arr

# o primeiro quadro tem de ser o de sempre
err = max(float(np.abs(newR[B[k]][0] - r.R[B[k]][0]).max()) for k in B)
errT = float(np.abs(newT[0] - r.T[B['Hips']][0]).max())
print(f"primeiro quadro: maior diferença de rotação {err:.5f}, de posição do quadril {errT:.4f}")
assert err < 2e-3 and errT < 0.05, "o primeiro quadro mudou"
errE = max(float(np.abs(newR[B[k]][-1] - newR[B[k]][0]).max()) for k in B)
print(f"último quadro igual ao primeiro: {errE:.5f}")

# ---- grava por cima dos mesmos dados (mesmo tamanho, mesmo lugar) --------
g = r.g; j = g.json
def overwrite(acc_index, arr):
    a = j['accessors'][acc_index]; bv = j['bufferViews'][a['bufferView']]
    off = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    data = np.asarray(arr, dtype='<f4').tobytes()
    assert a['componentType'] == 5126 and a['count'] == arr.shape[0] and 'sparse' not in a
    g.bin[off:off + len(data)] = data
    if 'min' in a: a['min'] = [float(v) for v in arr.min(axis=0)]; a['max'] = [float(v) for v in arr.max(axis=0)]

used = {}
for an in j['animations']:
    for s in an['samplers']: used[s['output']] = used.get(s['output'], 0) + 1
for node, arr in newR.items():
    s = r.chan[(node, 'rotation')]; assert used[s['output']] == 1
    overwrite(s['output'], arr)
s = r.chan[(B['Hips'], 'translation')]; assert used[s['output']] == 1
overwrite(s['output'], newT)
r.anim.setdefault('extras', {})['rugido'] = 2
g.save(DST)
print("gravado:", DST)

if PREVIA:
    import matplotlib; matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    ossos = [('Hips', 'Spine'), ('Spine', 'Spine1'), ('Spine1', 'Spine2'), ('Spine2', 'Neck'), ('Neck', 'Head'), ('Head', 'HeadTop_End'),
             ('Head', 'headfront'), ('Spine2', 'LeftShoulder'), ('Spine2', 'RightShoulder')]
    for sd in ('Left', 'Right'):
        ossos += [(sd + 'Shoulder', sd + 'Arm'), (sd + 'Arm', sd + 'ForeArm'), (sd + 'ForeArm', sd + 'Hand'),
                  ('Hips', sd + 'UpLeg'), (sd + 'UpLeg', sd + 'Leg'), (sd + 'Leg', sd + 'Foot'), (sd + 'Foot', sd + 'ToeBase')]
    tempos = [0.0, A, 0.33, B_, 0.52, 0.8, S1, 1.25]
    fig, axs = plt.subplots(2, len(tempos), figsize=(3.4 * len(tempos), 9.4), facecolor="#14161c")
    for c, tt in enumerate(tempos):
        fr = frames[int(round(tt * 30))]
        for row, (ia, ib, nome) in enumerate(((0, 1, "frente"), (2, 1, "lado"))):
            ax = axs[row][c]; ax.set_facecolor("#14161c")
            for a_, b_ in ossos:
                cor = "#ff7a2f" if a_.startswith('Left') or b_.startswith('Left') else "#4c8dff" if a_.startswith('Right') or b_.startswith('Right') else "#e8e8e8"
                ax.plot([fr[a_][ia], fr[b_][ia]], [fr[a_][ib], fr[b_][ib]], color=cor, lw=4)
            ax.plot([-0.9, 0.9], [0, 0], color="#555", lw=1)
            ax.set_xlim(-0.95, 0.95); ax.set_ylim(-0.05, 1.85); ax.set_aspect("equal"); ax.axis("off")
            if row == 0: ax.set_title(f"{tt:.2f} s", color="w", fontsize=10)
    plt.tight_layout(); plt.savefig(PREVIA, dpi=70, facecolor="#14161c")
