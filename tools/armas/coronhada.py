#!/usr/bin/env python3
"""
A coronhada do AR-15 em primeira pessoa (pedido do Jhon, 18/11: "coronhada deve
ser uma animação de bater com a arma: ele vira a arma para a frente e bate com a
lateral dela").

    python3 tools/armas/coronhada.py assets/armas/fp/ar15.glb [outro.glb ...]
        [--ficha assets/armas/fp/bracos.json]

Um clipe novo no conjunto, `coronhada` (0,40 s): a arma recolhe um instante para
o peito já virando, atravessa na frente do rosto (o cano vai para a esquerda, a
arma fica de lado) e é socada para a frente, com a lateral; depois volta.

Diferente da mira (tools/armas/mira.py), aqui não dá para mexer o conjunto
inteiro: girando tudo junto, os cotovelos (os braços acabam neles) apareciam no
meio da tela. Então **só a arma se move, e os braços a acompanham**: para cada
quadro, as mãos ficam onde estavam na arma e o cotovelo é recalculado com o
ombro parado (cinemática inversa de dois ossos, o cotovelo dobrando para o mesmo
lado da pose parada). Os dedos ficam como estão.

Rodar de novo troca o clipe antigo pelo novo.
"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'rugido'))
import mira
from mira import Conjunto, euler, rigido, matq, FPS

NOME = 'coronhada'
DURA = 0.62
# tempo (s): (pitch, yaw, roll) em graus; (x, y, z) em metros, no espaço do olho
# (x direita, y cima, -z frente). yaw +: cano para a esquerda.
# Refeita no mesmo dia a pedido do Jhon ("mais rápida e durar mais: uma coronhada
# rápida e uma leve empurrada com a arma"): o golpe chega em 0,11 s, a arma
# segue empurrando um pouco, fica um instante e volta devagar.
CHAVES = [
    (0.00, (0, 0, 0), (0.00, 0.00, 0.00)),
    (0.05, (5, 26, -8), (-0.02, 0.015, 0.035)),      # recolhe, já virando
    (0.11, (-3, 74, -15), (-0.09, 0.045, -0.07)),    # o golpe, com a lateral
    (0.22, (-4, 78, -16), (-0.095, 0.05, -0.11)),    # a empurrada
    (0.32, (-3, 76, -15), (-0.09, 0.047, -0.10)),    # segura
    (0.62, (0, 0, 0), (0.00, 0.00, 0.00)),
]


def suave(u): return u * u * (3 - 2 * u)

def pose(t):
    for (t0, g0, m0), (t1, g1, m1) in zip(CHAVES, CHAVES[1:]):
        if t <= t1:
            u = suave((t - t0) / (t1 - t0))
            return (np.array(g0) * (1 - u) + np.array(g1) * u), (np.array(m0) * (1 - u) + np.array(m1) * u)
    return np.zeros(3), np.zeros(3)

def rot(m):
    """A rotação pura de uma 4×4 com escala uniforme."""
    r = m[:3, :3].copy()
    return r / np.linalg.norm(r, axis=0, keepdims=True)

def arco(a, b):
    """A menor rotação que leva a direção a na direção b."""
    a = a / np.linalg.norm(a); b = b / np.linalg.norm(b)
    v = np.cross(a, b); c = float(np.dot(a, b)); s = np.linalg.norm(v)
    if s < 1e-9: return np.eye(3)
    k = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]]) / s
    ang = np.arctan2(s, c)
    return np.eye(3) + np.sin(ang) * k + (1 - np.cos(ang)) * (k @ k)


def fazer(path, ficha=None):
    c = Conjunto(path, limpar=False); j = c.j
    j['animations'] = [a for a in j['animations'] if a['name'] != NOME]
    parado = c.clip('parado')
    N = lambda nome: c.names.index(nome)
    W = lambda nome: c.world(parado, N(nome), 0.0)

    gun = N('ik_hand_gun'); G0 = W('ik_hand_gun'); Wpai = c.world(parado, c.parent[gun], 0.0)
    pivo = W('Grip')[:3, 3]
    bracos = []
    for lado in ('l', 'r'):
        S = W(f'upperarm_{lado}')[:3, 3]; E0 = W(f'lowerarm_{lado}')[:3, 3]; H0 = W(f'hand_{lado}')
        bracos.append(dict(lado=lado, S=S, E0=E0, H0=H0, L1=np.linalg.norm(E0 - S), L2=np.linalg.norm(H0[:3, 3] - E0),
                           Rc=rot(W(f'clavicle_{lado}')), Ru0=rot(W(f'upperarm_{lado}')), Rl0=rot(W(f'lowerarm_{lado}')), Rh0=rot(H0)))

    n = int(round(DURA * FPS)) + 1
    ts = np.arange(n) / FPS; ts[-1] = DURA
    gt = np.zeros((n, 3)); gq = np.zeros((n, 4))
    quats = {(b['lado'], osso): np.zeros((n, 4)) for b in bracos for osso in ('upperarm', 'lowerarm', 'hand')}
    estica = 0.0
    for i, t in enumerate(ts):
        giro, move = pose(float(t))
        Rd = euler(*giro); D = rigido(move, Rd, pivo)
        L = np.linalg.inv(Wpai) @ D @ G0
        gt[i] = L[:3, 3]; gq[i] = matq(rot(L))
        for b in bracos:
            H = (D @ b['H0'])[:3, 3]; S = b['S']
            alcance = (b['L1'] + b['L2']) * 0.995
            d = np.linalg.norm(H - S); estica = max(estica, d / (b['L1'] + b['L2']))
            if d > alcance: H = S + (H - S) / d * alcance; d = alcance
            eixo = (H - S) / d
            a = (b['L1'] ** 2 - b['L2'] ** 2 + d * d) / (2 * d); h = np.sqrt(max(b['L1'] ** 2 - a * a, 0.0))
            polo = b['E0'] - S; polo = polo - np.dot(polo, eixo) * eixo; polo /= np.linalg.norm(polo)
            E = S + eixo * a + polo * h
            Ru = arco(b['E0'] - S, E - S) @ b['Ru0']
            Rl = arco(b['H0'][:3, 3] - b['E0'], H - E) @ b['Rl0']
            Rh = Rd @ b['Rh0']
            quats[(b['lado'], 'upperarm')][i] = matq(b['Rc'].T @ Ru)
            quats[(b['lado'], 'lowerarm')][i] = matq(Ru.T @ Rl)
            quats[(b['lado'], 'hand')][i] = matq(Rl.T @ Rh)
    for q in [gq] + list(quats.values()):
        for i in range(1, n):
            if np.dot(q[i], q[i - 1]) < 0: q[i] = -q[i]

    # O clipe: tudo parado como no começo do "parado", menos a arma e os braços.
    meus = {(gun, 'translation'), (gun, 'rotation')} | {(N(f'{osso}_{lado}'), 'rotation') for (lado, osso) in quats}
    t_in = c.add_times(ts); dois = c.add_times([0.0, DURA])
    samplers, channels = [], []
    def por(node, caminho, entrada, saida, interp):
        samplers.append({'input': entrada, 'output': saida, 'interpolation': interp})
        channels.append({'sampler': len(samplers) - 1, 'target': {'node': node, 'path': caminho}})
    for (node, caminho), ch in parado.items():
        if (node, caminho) in meus: continue
        tipo = {1: 'SCALAR', 3: 'VEC3', 4: 'VEC4'}[ch['v'].shape[1]]
        por(node, caminho, dois, c.g.add_acc(np.stack([ch['v'][0], ch['v'][0]]), tipo), 'STEP')
    por(gun, 'translation', t_in, c.g.add_acc(gt, 'VEC3'), 'LINEAR')
    por(gun, 'rotation', t_in, c.g.add_acc(gq, 'VEC4'), 'LINEAR')
    for (lado, osso), q in quats.items():
        por(N(f'{osso}_{lado}'), 'rotation', t_in, c.g.add_acc(q, 'VEC4'), 'LINEAR')
    j['animations'].append({'name': NOME, 'channels': channels, 'samplers': samplers, 'extras': {'coronhada': 1}})
    c.g.save(path)

    # Confere: no golpe as mãos seguem na arma.
    feito = Conjunto(path, limpar=False); clipe = feito.clip(NOME); erro = 0.0
    for t in (0.09, 0.17, 0.21):
        Gt = feito.world(clipe, gun, t)
        for lado in ('l', 'r'):
            mao = feito.world(clipe, N(f'hand_{lado}'), t)[:3, 3]
            devia = (Gt @ np.linalg.inv(G0) @ W(f'hand_{lado}'))[:3, 3]
            erro = max(erro, np.linalg.norm(mao - devia))
    print(f"{os.path.basename(path)}: {NOME} {DURA:.2f} s, {n} quadros; braço mais esticado a {estica * 100:.0f}% do alcance; mãos fora da arma no máximo {erro * 1000:.1f} mm")

    if ficha:
        dados = json.load(open(ficha, encoding='utf-8'))
        for chave, e in dados.items():
            if isinstance(e, dict) and e.get('arquivo', '').endswith(os.path.basename(path)):
                e['animacoes'][NOME] = {'clipe': NOME, 'de': 0.0, 'ate': DURA}
        json.dump(dados, open(ficha, 'w', encoding='utf-8'), indent=1, ensure_ascii=False, sort_keys=True); open(ficha, 'a', encoding='utf-8').write('\n')


if __name__ == '__main__':
    args = sys.argv[1:]; ficha = None
    if '--ficha' in args:
        k = args.index('--ficha'); ficha = args[k + 1]; del args[k:k + 2]
    for p in args: fazer(p, ficha)
