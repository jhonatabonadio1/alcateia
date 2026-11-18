"""O lobo agachado e andando agachado, refeitos (pedido do Jhon, 18/11: "tá
muito robótico os braços, e distorcendo o short").

O que os clipes de antes mediam (Crouch_Idle_Loop e Crouch_Walk_Loop):

- as mãos andavam de 1 a 4 cm no ciclo inteiro do andar: braços congelados;
- o quadril ficava a 0,49 m do chão (em pé, 0,93), sem subir nem descer um
  milímetro, e os pés varriam 0,91 m: coxas abertas na horizontal, que é o que
  esticava o short;
- parado, era um joelho no chão com o pé virado para cima.

Agora é a espreita de um bicho: quadril a 0,63 m, tronco deitado para a frente
(o que mantém a cabeça baixa, na mesma altura de antes), cabeça erguida olhando
para a frente, braços soltos à frente do corpo com as garras perto do chão.

- andando (1,0 s, o mesmo tamanho): passos curtos (0,84 m; o pé no chão anda a 1,5 m/s), o quadril sobe e
  desce a cada passo e balança para a perna de apoio, a bacia gira com a perna
  e o peito contra-gira, e os braços balançam ao contrário das pernas, com o
  antebraço atrasado e a mão pendendo;
- parado (2,0 s): a mesma pose com um pé à frente, respirando, o peso passando
  de um lado ao outro e a cabeça procurando devagar.

Tudo parte da pose em pé (primeiro quadro de Idle_Pose_Loop): tronco por
rotações no mundo, pernas por IK de dois ossos (pés no chão), braços apontando
para alvos. Rodar de novo dá o mesmo resultado.

uso: python3 agachar.py entrada.glb saida.glb [--previa figura.png]
"""
import sys, numpy as np
from fk import *

SRC, DST = sys.argv[1], sys.argv[2]
PREVIA = sys.argv[sys.argv.index('--previa') + 1] if '--previa' in sys.argv else None
X = np.array([1.0, 0, 0]); Y = np.array([0, 1.0, 0]); Z = np.array([0, 0, 1.0])
TAU = 2 * np.pi

# ---- o desenho (metros; graus) -------------------------------------------
QUADRIL = -0.300        # quanto o quadril desce da pose em pé
RECUO = -0.13           # e recua, para o peso ficar sobre os pés com o tronco deitado
DEITA = 60.0            # o tronco para a frente
CABECA = -56.0          # a cabeça ergue (em relação ao peito) para olhar à frente
PASSO = 0.84            # comprimento do passo: com o ciclo de 1 s, é a velocidade do clipe (m/s)
APOIO = 0.56            # fração do ciclo com o pé no chão
ABERTURA = 0.035        # cada pé um pouco mais para fora que em pé

ref = Rig(SRC, 'Idle_Pose_Loop')
names = ['Hips', 'Spine', 'Spine1', 'Spine2', 'Neck', 'Head', 'HeadTop_End', 'LeftShoulder', 'LeftArm', 'LeftForeArm',
         'LeftHand', 'RightShoulder', 'RightArm', 'RightForeArm', 'RightHand', 'LeftUpLeg', 'LeftLeg', 'LeftFoot',
         'LeftToeBase', 'RightUpLeg', 'RightLeg', 'RightFoot', 'RightToeBase']
B = {k: ref.idx(k) for k in names}
B['headfront'] = ref.names.index('headfront')
W0 = {k: ref.world(B[k], 0) for k in B}
P0 = {k: W0[k][0] for k in B}; Q0 = {k: W0[k][1] for k in B}
pp, pq, ps = ref.world(ref.parent[B['Hips']], 0)
CHAO = min(P0['LeftToeBase'][1], P0['RightToeBase'][1])

def rot(deg_x=0.0, deg_y=0.0, deg_z=0.0):
    q = np.array([0, 0, 0, 1.0])
    for ax, a in ((Y, deg_y), (X, deg_x), (Z, deg_z)):
        if a: q = qmul(q, qaxis(ax, np.radians(a)))
    return q

def suave(x):
    x = min(max(x, 0.0), 1.0); return x * x * (3 - 2 * x)

def pose(hip, lean, twist_hip, twist_chest, head_pitch, head_yaw, head_tilt, arms, feet, knee_out=0.10):
    """Monta o corpo: devolve as rotações e posições no mundo de cada osso.
    arms[lado] = (direção do braço, direção do antebraço, giro da mão em graus), no mundo;
    feet[lado] = (posição do tornozelo, inclinação do pé em graus, pé no ar?)."""
    Wq, Wp, C = {}, {}, {}
    C['Hips'] = rot(lean * 0.22, twist_hip)
    C['Spine'] = qmul(rot(0, twist_chest * 0.3 - twist_hip * 0.3), qmul(C['Hips'], rot(lean * 0.26)))
    C['Spine1'] = qmul(rot(0, twist_chest * 0.3 - twist_hip * 0.3), qmul(C['Spine'], rot(lean * 0.26)))
    C['Spine2'] = qmul(rot(0, twist_chest * 0.4 - twist_hip * 0.4), qmul(C['Spine1'], rot(lean * 0.26)))
    C['Neck'] = qmul(C['Spine2'], rot(head_pitch * 0.45, head_yaw * 0.4, head_tilt * 0.4))
    C['Head'] = qmul(C['Neck'], rot(head_pitch * 0.55, head_yaw * 0.6, head_tilt * 0.6))
    C['HeadTop_End'] = C['Head']; C['headfront'] = C['Head']
    Wp['Hips'] = hip
    for a, b in (('Hips', 'Spine'), ('Spine', 'Spine1'), ('Spine1', 'Spine2'), ('Spine2', 'Neck'), ('Neck', 'Head'),
                 ('Head', 'HeadTop_End'), ('Head', 'headfront'), ('Spine2', 'LeftShoulder'), ('Spine2', 'RightShoulder')):
        Wp[b] = Wp[a] + qrot(C[a], P0[b] - P0[a])
    for k in ('Hips', 'Spine', 'Spine1', 'Spine2', 'Neck', 'Head', 'HeadTop_End', 'headfront'):
        Wq[k] = qnorm(qmul(C[k], Q0[k]))

    chest = C['Spine2']
    for side in ('Left', 'Right'):
        ta, tf, wrist = arms[side]
        ta = ta / np.linalg.norm(ta); tf = tf / np.linalg.norm(tf)
        Wq[side + 'Shoulder'] = qnorm(qmul(chest, Q0[side + 'Shoulder']))
        Wp[side + 'Arm'] = Wp[side + 'Shoulder'] + qrot(chest, P0[side + 'Arm'] - P0[side + 'Shoulder'])
        d0a = P0[side + 'ForeArm'] - P0[side + 'Arm']; la = np.linalg.norm(d0a); d0a /= la
        d0f = P0[side + 'Hand'] - P0[side + 'ForeArm']; lf = np.linalg.norm(d0f); d0f /= lf
        Ca = qmul(qfrom(qrot(chest, d0a), ta), chest)
        Wq[side + 'Arm'] = qnorm(qmul(Ca, Q0[side + 'Arm']))
        Wp[side + 'ForeArm'] = Wp[side + 'Arm'] + ta * la
        Cf = qmul(qfrom(qrot(Ca, d0f), tf), Ca)
        Wq[side + 'ForeArm'] = qnorm(qmul(Cf, Q0[side + 'ForeArm']))
        Wp[side + 'Hand'] = Wp[side + 'ForeArm'] + tf * lf
        # a mão pende do pulso: gira em volta do eixo de lado
        eixo = np.cross(tf, Y); eixo = eixo / max(np.linalg.norm(eixo), 1e-6)
        Wq[side + 'Hand'] = qnorm(qmul(qmul(qaxis(eixo, np.radians(wrist)), Cf), Q0[side + 'Hand']))

    for side, sign in (('Left', 1.0), ('Right', -1.0)):
        up0, kn0, an0, toe0 = (P0[side + s] for s in ('UpLeg', 'Leg', 'Foot', 'ToeBase'))
        qup0, qkn0, qan0, qtoe0 = (Q0[side + s] for s in ('UpLeg', 'Leg', 'Foot', 'ToeBase'))
        L1 = np.linalg.norm(kn0 - up0); L2 = np.linalg.norm(an0 - kn0)
        H = hip + qrot(C['Hips'], up0 - P0['Hips'])
        An, pitch, _ = feet[side]
        turn = qaxis(X, np.radians(pitch))
        qfoot = qnorm(qmul(turn, qan0))
        d_vec = An - H; d = np.linalg.norm(d_vec)
        reach = 0.985 * (L1 + L2)
        if d > reach:
            An = H + d_vec / d * reach; d_vec = An - H; d = reach
        n = d_vec / d
        # o joelho dobra para a FRENTE do mundo, com um fio para fora: coxas juntas (é a coxa
        # aberta para o lado que esticava o short)
        pole = Z + knee_out * np.array([sign, 0, 0])
        p = pole - np.dot(pole, n) * n; p /= np.linalg.norm(p)
        a = (L1 * L1 - L2 * L2 + d * d) / (2 * d)
        h = np.sqrt(max(L1 * L1 - a * a, 0.0))
        K = H + a * n + h * p
        Wq[side + 'UpLeg'] = qnorm(qmul(qfrom(kn0 - up0, K - H), qup0))
        Wq[side + 'Leg'] = qnorm(qmul(qfrom(an0 - kn0, An - K), qkn0))
        Wq[side + 'Foot'] = qfoot
        Wq[side + 'ToeBase'] = qtoe0 if pitch >= 0 else qnorm(qmul(turn, qtoe0))
        Wp[side + 'UpLeg'] = H; Wp[side + 'Leg'] = K; Wp[side + 'Foot'] = An
        Wp[side + 'ToeBase'] = An + qrot(qmul(qfoot, qinv(qan0)), toe0 - an0)
    return Wq, Wp

def braco(side, balanco, atraso, respiro=0.0):
    """O braço solto à frente do corpo. balanco: -1 (atrás) a +1 (à frente)."""
    sign = 1.0 if side == 'Left' else -1.0
    a = np.radians(16.0 * balanco)                      # o braço vai e volta em volta do ombro
    b = np.radians(22.0 * atraso)                       # o antebraço chega depois e dobra mais
    ta = np.array([sign * (0.30 + 0.03 * respiro), -0.92 * np.cos(a) + 0.0, 0.26 + 0.92 * np.sin(a)])
    tf = np.array([sign * -0.06, -0.72 * np.cos(b) + 0.10 * np.sin(b), 0.66 + 0.5 * np.sin(b)])
    pulso = 18.0 - 14.0 * atraso                        # a mão pende mais quando o braço vem para trás
    return ta, tf, pulso

def pe(side, u):
    """O tornozelo no ciclo (u de 0 a 1): no chão indo para trás, depois no ar voltando."""
    sign = 1.0 if side == 'Left' else -1.0
    an0 = P0[side + 'Foot']
    base = np.array([an0[0] + sign * ABERTURA, an0[1], RECUO + 0.02])
    if u < APOIO:                                        # apoio: anda para trás em linha, na velocidade do clipe
        s = u / APOIO
        return base + Z * PASSO * (0.5 - s), 0.0, False
    s = (u - APOIO) / (1 - APOIO)                        # no ar: volta para a frente, levantando
    z = PASSO * (-0.5 + suave(s))
    y = 0.085 * np.sin(np.pi * s) ** 0.9
    pitch = 26.0 * np.sin(np.pi * min(s * 1.6, 1.0)) * (1 - s) - 9.0 * suave((s - 0.6) / 0.4) * (1 - suave((s - 0.92) / 0.08))
    return base + Z * z + Y * y, pitch, True

def andar(t, dur):
    u = (t / dur) % 1.0
    fl, fr = pe('Left', u), pe('Right', (u + 0.5) % 1.0)
    f = TAU * u
    # o quadril: desce no meio do apoio de cada perna (duas vezes por ciclo) e vai para a perna de apoio
    hip = P0['Hips'] + np.array([0.022 * np.sin(f + 0.9), QUADRIL - 0.014 * np.cos(2 * f + 0.6), RECUO + 0.012 * np.sin(2 * f)])
    gira = 7.0 * np.cos(f)                               # a bacia acompanha a perna que vai à frente (a esquerda em u=0)
    arms = {'Left': braco('Left', -np.cos(f), -np.cos(f - 0.55)),          # o braço esquerdo vai com a perna direita
            'Right': braco('Right', np.cos(f), np.cos(f - 0.55))}
    return pose(hip, DEITA + 1.8 * np.cos(2 * f), gira, -gira * 0.7, CABECA - 1.5 * np.cos(2 * f + 0.4),
                -gira * 0.25, 2.0 * np.sin(f), arms, {'Left': fl, 'Right': fr})

def parado(t, dur):
    f = TAU * t / dur
    sopro = np.sin(f)                                    # um fôlego em 2 s
    peso = np.sin(f / 1.0 + 1.3)                         # o peso passa de um lado ao outro
    hip = P0['Hips'] + np.array([0.010 * peso, QUADRIL - 0.012 + 0.007 * sopro, RECUO])
    feet = {}
    for side, z in (('Left', 0.13), ('Right', -0.11)):
        sign = 1.0 if side == 'Left' else -1.0
        an0 = P0[side + 'Foot']
        feet[side] = (np.array([an0[0] + sign * ABERTURA, an0[1], RECUO + 0.02 + z]), 0.0, False)
    arms = {'Left': braco('Left', 0.10 + 0.05 * sopro, 0.05 + 0.08 * np.sin(f - 0.6), sopro),
            'Right': braco('Right', -0.12 + 0.05 * sopro, -0.05 + 0.08 * np.sin(f - 0.9), sopro)}
    return pose(hip, DEITA + 1.3 * sopro, 2.0 * peso, -1.0 * peso, CABECA - 1.0 * sopro, 6.0 * np.sin(f + 0.5), 1.5 * peso, arms, feet)

frames_all = {}
from glb import Glb
g = Glb(SRC); j = g.json                                   # um arquivo de saída só, para os dois clipes
for clip, fn in (('Crouch_Walk_Loop', andar), ('Crouch_Idle_Loop', parado)):
    r = Rig(SRC, clip)                                     # o original, para ler tempos e canais
    anim = [a for a in j['animations'] if a['name'] == clip][0]
    T = r.times; dur = float(T[-1])
    newR = {B[k]: np.zeros((r.n, 4)) for k in B}
    newT = np.zeros((r.n, 3)); frames = []
    for f in range(r.n):
        Wq, Wp = fn(float(T[f]) if f < r.n - 1 else 0.0, dur)                 # o último quadro é o primeiro: laço fechado
        newT[f] = qrot(qinv(pq), Wp['Hips'] - pp) / ps
        for k in B:
            par = r.parent[B[k]]
            pk = next((n_ for n_ in B if B[n_] == par), None)
            newR[B[k]][f] = qnorm(qmul(qinv(Wq[pk] if pk else pq), Wq[k]))
        frames.append({k: v.copy() for k, v in Wp.items()})
    for node, arr in newR.items():
        if np.dot(arr[0], r.R[node][0]) < 0: arr[0] = -arr[0]
        for f in range(1, r.n):
            if np.dot(arr[f], arr[f - 1]) < 0: arr[f] = -arr[f]
        if np.dot(arr[-1], arr[0]) < 0: arr[-1] = -arr[-1]

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
        s = r.chan[(node, 'rotation')]; assert used[s['output']] == 1, (clip, r.names[node])
        overwrite(s['output'], arr)
    s = r.chan[(B['Hips'], 'translation')]; assert used[s['output']] == 1
    overwrite(s['output'], newT)
    anim.setdefault('extras', {})['agachar'] = 2
    frames_all[clip] = frames

    P = lambda k: np.array([fr[k] for fr in frames])
    print(f"{clip}: quadril {P('Hips')[:,1].min():.2f}-{P('Hips')[:,1].max():.2f} m, topo da cabeça {P('HeadTop_End')[:,1].max():.2f} m, "
          f"mãos andam {np.ptp(P('LeftHand'), axis=0).round(2)} m, pés z {np.ptp(P('LeftFoot')[:,2]):.2f} m, "
          f"sola mais baixa {min(P('LeftToeBase')[:,1].min(), P('RightToeBase')[:,1].min()) - CHAO:+.3f} m, mão mais baixa {min(P('LeftHand')[:,1].min(), P('RightHand')[:,1].min()):.2f} m")
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
    fr_w = frames_all['Crouch_Walk_Loop']; quadros = [0, 4, 8, 12, 15, 19, 23, 27]
    cols = [fr_w[q] for q in quadros] + [frames_all['Crouch_Idle_Loop'][0]]
    fig, axs = plt.subplots(2, len(cols), figsize=(3.2 * len(cols), 7.4), facecolor="#14161c")
    for c, fr in enumerate(cols):
        for row, (ia, ib) in enumerate(((0, 1), (2, 1))):
            ax = axs[row][c]; ax.set_facecolor("#14161c")
            for a_, b_ in ossos:
                cor = "#ff7a2f" if a_.startswith('Left') or b_.startswith('Left') else "#4c8dff" if a_.startswith('Right') or b_.startswith('Right') else "#e8e8e8"
                ax.plot([fr[a_][ia], fr[b_][ia]], [fr[a_][ib], fr[b_][ib]], color=cor, lw=4)
            ax.plot([-0.9, 0.9], [CHAO, CHAO], color="#555", lw=1)
            ax.set_xlim(-0.8, 0.9); ax.set_ylim(-0.05, 1.45); ax.set_aspect("equal"); ax.axis("off")
            if row == 0: ax.set_title("parado" if c == len(cols) - 1 else f"andar {quadros[c] / 30:.2f} s", color="w", fontsize=10)
    plt.tight_layout(); plt.savefig(PREVIA, dpi=70, facecolor="#14161c")
