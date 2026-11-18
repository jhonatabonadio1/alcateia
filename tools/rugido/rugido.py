"""Rugido do lobo com as pernas trabalhando: agacha fundo, bate o pé da
frente, estica a de trás na ponta do pé e projeta o quadril.

Mexe só no clipe "Howl" de lobo_anim.glb, e nele só no quadril e nos
oito ossos das pernas — tronco, braços e cabeça ficam como o Jhon
animou. Primeiro e último quadros iguais aos originais.

Roda em cima do clipe original, uma vez só (o arquivo fica marcado). Se o
Howl for exportado de novo do Blender, é só rodar de novo no arquivo novo.
Os números do movimento estão logo abaixo, para ajustar.

uso: python3 rugido.py entrada.glb saida.glb      (precisa de numpy e scipy)
"""
import sys, numpy as np
from scipy.interpolate import PchipInterpolator
from fk import *

SRC, DST = sys.argv[1], sys.argv[2]
r = Rig(SRC, 'Howl')
MARK = 'pernas_do_rugido'
if r.anim.get('extras', {}).get(MARK):
    sys.exit('Este arquivo já passou por aqui: rodar de novo somaria o movimento ao que já está. '
             'Use o lobo_anim.glb exportado do Blender (ou o do git, antes de 18/11).')
T = r.times
END = float(T[-1])

def curve(keys):
    ts = [k[0] for k in keys]; vs = [k[1] for k in keys]
    f = PchipInterpolator(ts, vs)
    return lambda t: float(f(min(max(t, ts[0]), ts[-1])))

# ---- o desenho do movimento (tempo em s; metros; graus) -----------------
# quadril: afunda para armar, sobe e avança no rugido, volta
hip_y = curve([(0, 0), (0.27, -0.115), (0.50, -0.055), (0.72, -0.02), (1.00, -0.045), (1.25, -0.06), (END, 0)])
hip_z = curve([(0, 0), (0.27, -0.06), (0.52, 0.10), (0.85, 0.10), (1.15, 0.03), (END, 0)])
hip_x = curve([(0, 0), (0.27, -0.025), (0.52, 0.03), (0.9, 0.03), (1.2, 0), (END, 0)])
# tronco: curva para armar, peito para fora no rugido (graus, + = para a frente)
lean = curve([(0, 0), (0.27, 11), (0.55, -7), (0.90, -5), (1.2, 2), (END, 0)])

# pé esquerdo, o da frente: levanta, avança e BATE no chão
lead_z = curve([(0, 0), (0.25, 0), (0.50, 0.17), (1.08, 0.17), (1.34, 0), (END, 0)])
lead_x = curve([(0, 0), (0.25, 0), (0.50, 0.05), (1.08, 0.05), (1.34, 0), (END, 0)])
lead_y = curve([(0, 0), (0.25, 0), (0.37, 0.13), (0.45, 0.09), (0.50, 0), (1.08, 0), (1.21, 0.05), (1.34, 0), (END, 0)])
lead_pitch = curve([(0, 0), (0.25, 0), (0.38, -20), (0.50, 0), (1.08, 0), (1.2, 10), (1.34, 0), (END, 0)])

# pé direito, o de trás: fica no lugar e sobe na ponta, empurrando
rear_heel = curve([(0, 0), (0.27, 0), (0.55, 30), (0.92, 26), (1.22, 0), (END, 0)])
rear_out = curve([(0, 0), (0.27, 0), (0.55, 8), (0.92, 8), (1.22, 0), (END, 0)])   # calcanhar para fora

# joelhos abrem no agachamento
knee_out = curve([(0, 0), (0.27, 0.55), (0.55, 0.30), (0.95, 0.25), (1.3, 0.15), (END, 0)])

# tremor do rugido: curto e pequeno, só no auge
def shake(t):
    gate = max(0.0, min((t - 0.52) / 0.08, 1.0)) * max(0.0, min((1.0 - t) / 0.12, 1.0))
    return 0.006 * gate * np.sin(2 * np.pi * 7.5 * t)

B = {k: r.idx(k) for k in ['Hips', 'LeftUpLeg', 'LeftLeg', 'LeftFoot', 'LeftToeBase',
                           'RightUpLeg', 'RightLeg', 'RightFoot', 'RightToeBase']}
X = np.array([1.0, 0, 0]); Y = np.array([0, 1.0, 0])
newR = {B[k]: np.zeros((r.n, 4)) for k in B}
newT = np.zeros((r.n, 3))
report = []

for f in range(r.n):
    t = float(T[f])
    W = {k: r.world(B[k], f) for k in B}                    # pose original
    pp, pq, ps = r.world(r.parent[B['Hips']], f)            # pai do quadril

    # quadril novo
    hp = W['Hips'][0] + np.array([hip_x(t), hip_y(t) + shake(t), hip_z(t)])
    hq = qnorm(qmul(qaxis(X, np.radians(lean(t))), W['Hips'][1]))
    newT[f] = qrot(qinv(pq), hp - pp) / ps
    newR[B['Hips']][f] = qnorm(qmul(qinv(pq), hq))

    for side, sign in (('Left', 1.0), ('Right', -1.0)):
        up0, kn0, an0, toe0 = (W[side + s][0] for s in ('UpLeg', 'Leg', 'Foot', 'ToeBase'))
        qup0, qkn0, qan0, qtoe0 = (W[side + s][1] for s in ('UpLeg', 'Leg', 'Foot', 'ToeBase'))
        L1 = np.linalg.norm(kn0 - up0); L2 = np.linalg.norm(an0 - kn0)

        # articulação do quadril acompanha o quadril novo
        H = hp + qrot(qmul(hq, qinv(W['Hips'][1])), up0 - W['Hips'][0])

        # alvo do tornozelo e rotação do pé
        if side == 'Left':
            turn = qaxis(X, np.radians(lead_pitch(t)))
            A = an0 + np.array([lead_x(t), lead_y(t), lead_z(t)])
            toe_free = lead_y(t) > 0.005
        else:
            turn = qmul(qaxis(Y, np.radians(-rear_out(t))), qaxis(X, np.radians(rear_heel(t))))
            A = toe0 + qrot(turn, an0 - toe0)               # gira em volta da ponta do pé
            toe_free = False
        qfoot = qnorm(qmul(turn, qan0))

        # IK de dois ossos, joelho para onde já apontava, um pouco mais aberto
        d_vec = A - H; d = np.linalg.norm(d_vec)
        reach = 0.995 * (L1 + L2)
        if d > reach:
            A = H + d_vec / d * reach; d_vec = A - H; d = reach
        n = d_vec / d
        pole = (kn0 - up0) + knee_out(t) * np.array([sign * 0.25, 0, 0.05])
        p = pole - np.dot(pole, n) * n; p /= np.linalg.norm(p)
        a = (L1 * L1 - L2 * L2 + d * d) / (2 * d)
        h = np.sqrt(max(L1 * L1 - a * a, 0.0))
        K = H + a * n + h * p

        qup = qnorm(qmul(qfrom(kn0 - up0, K - H), qup0))
        qkn = qnorm(qmul(qfrom(an0 - kn0, A - K), qkn0))
        qtoe = qnorm(qmul(turn, qtoe0)) if toe_free else qtoe0   # no chão, os dedos ficam chapados

        newR[B[side + 'UpLeg']][f] = qnorm(qmul(qinv(hq), qup))
        newR[B[side + 'Leg']][f] = qnorm(qmul(qinv(qup), qkn))
        newR[B[side + 'Foot']][f] = qnorm(qmul(qinv(qkn), qfoot))
        newR[B[side + 'ToeBase']][f] = qnorm(qmul(qinv(qfoot), qtoe))

        ang = np.degrees(np.arccos(np.clip(np.dot(H - K, A - K) / L1 / L2, -1, 1)))
        report.append((f, side, ang, d / (L1 + L2)))

# quaternions contínuos (sem pular para o lado oposto entre quadros)
for node, arr in newR.items():
    for f in range(1, r.n):
        if np.dot(arr[f], arr[f - 1]) < 0: arr[f] = -arr[f]
    if np.dot(arr[0], r.R[node][0]) < 0: arr[:] = -arr

# ---- grava por cima dos mesmos dados (mesmo tamanho, mesmo lugar) -------
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
r.anim.setdefault('extras', {})[MARK] = 1
g.save(DST)

for side in ('Left', 'Right'):
    angs = [a for f, s, a, e in report if s == side]; ext = [e for f, s, a, e in report if s == side]
    print(f"{side}: joelho de {min(angs):.0f} a {max(angs):.0f} graus, perna esticada no máximo {max(ext)*100:.1f}%")
