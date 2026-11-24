#!/usr/bin/env python3
"""
Confere um conjunto de primeira pessoa pelos números, antes de ir para o jogo
(``docs/ANIMACAO_ARMAS.md`` diz o que cada número tem de dar):

    python3 tools/armas/conferir.py m4a4 ak74 scar svd deagle faca choque

Por conjunto:

- **pulso**: quanto cada pulso dobra, no pior quadro de cada clipe (graus);
- **vulto**: em quantos quadros o braço esquerdo, do cotovelo para cima, aparece
  na tela a menos de 15 cm da câmera (é o "vulto" que passa na frente dela numa
  recarga mal resolvida);
- **corte**: na mira, quantos pontos da arma ficam aquém do plano de corte da
  câmera e dentro da tela (é quando se vê a arma por dentro).

As contas usam a pele das mãos do jogo (``pegada.Pele``) e a câmera do jogo: o
conjunto entra na cena com escala 0,45 e a câmera corta a 1,5 cm, isto é, a
3,3 cm no espaço do conjunto; a abertura do conjunto é 63,09° na vertical.
"""
import os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from esqueleto import *
import pegada, reais

FP = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), 'assets', 'armas', 'fp')
CORTE = 0.015 / 0.45; TG = np.tan(np.radians(63.09 / 2)); ASPECTO = 1.6
ARQUIVO = dict(granada='choque')
PERTO_DO_ROSTO = 0.15       # o braço acima do cotovelo só é "vulto" a menos disto da câmera (m); mais longe é braço esticado, normal


def na_tela(q, margem=0.0):
    """Os pontos (no olho, m) que caem dentro da tela."""
    return (q[:, 2] < -margem) & (np.abs(q[:, 1]) < TG * -q[:, 2]) & (np.abs(q[:, 0]) < TG * ASPECTO * -q[:, 2])


def dobra(E, pose, lado):
    la, hd = E.N(f'lowerarm_{lado}'), E.N(f'hand_{lado}'); Wl, Wh = E.mundo(pose, la), E.mundo(pose, hd); f = Wh[:3, 3] - Wl[:3, 3]; f /= np.linalg.norm(f)
    Bh, _ = E.ligacao(f'hand_{lado}'); Bl, _ = E.ligacao(f'lowerarm_{lado}'); Rh = rot(Bh); Rh = Rh / np.linalg.norm(Rh, axis=0, keepdims=True); d = Bh[:3, 3] - Bl[:3, 3]
    Rw = rot(Wh); Rw = Rw / np.linalg.norm(Rw, axis=0, keepdims=True)
    return float(np.degrees(np.arccos(np.clip(np.dot(f, Rw @ (Rh.T @ (d / np.linalg.norm(d)))), -1.0, 1.0))))


def pontos(tri, passo=0.004):
    out = [tri.reshape(-1, 3)]
    for t in tri:
        n = int(max(np.linalg.norm(t[1] - t[0]), np.linalg.norm(t[2] - t[0]), np.linalg.norm(t[2] - t[1])) / passo)
        if n > 1:
            u, v = np.meshgrid(np.arange(n + 1) / n, np.arange(n + 1) / n); s = (u + v) <= 1; u, v = u[s], v[s]
            out.append(t[0] + u[:, None] * (t[1] - t[0]) + v[:, None] * (t[2] - t[0]))
    return np.vstack(out)


def conferir(id_, pele):
    E = Esqueleto(os.path.join(FP, ARQUIVO.get(id_, id_) + '.glb')); clipes = [a['name'] for a in E.j['animations']]
    acima = np.array([n.startswith('upperarm') and n.endswith('_l') for n in pele.dono]); print(id_)
    for nome in clipes:
        ts = np.arange(0.0, E.duracao(nome) + 1e-6, 1 / 60); r, l, vulto = [], [], 0
        for t in ts:
            p = E.pose_do_clipe(nome, t); r.append(dobra(E, p, 'r')); l.append(dobra(E, p, 'l'))
            q = pele.em(E, p, E.R2O, acima); vulto += bool((na_tela(q) & (q[:, 2] > -PERTO_DO_ROSTO)).any())
        # a mão esquerda fora da tela (guardada) não conta: só interessa o pulso que se vê
        vis_l = [d for t, d in zip(ts, l) if na_tela(pele.em(E, E.pose_do_clipe(nome, t), E.R2O, np.array([n == 'hand_l' for n in pele.dono]))).any()]
        print(f"  {nome:14s} {E.duracao(nome):5.2f} s | pulso direito {max(r):3.0f}°, esquerdo (na tela) {max(vis_l, default=0):3.0f}° | braço esquerdo passando perto da câmera: {vulto} quadros")
    if 'mira' in clipes:
        from mira import Conjunto
        c = Conjunto(E.path, limpar=False); W = c.world(c.clip('mira'), c.gun, 0.0) @ ARMA
        q = pontos(np.vstack([m['pos'] for _, _, m in reais.arma(id_)['pecas']])); q = q @ W[:3, :3].T + W[:3, 3]
        cortados = na_tela(q, 0.004) & (q[:, 2] > -CORTE)
        print(f"  na mira: {int(cortados.sum())} pontos da arma aquém do plano de corte e dentro da tela (tem de dar 0)")


if __name__ == '__main__':
    pele = pegada.Pele()
    for id_ in sys.argv[1:] or ['m4a4', 'ak74', 'scar', 'svd', 'deagle', 'faca', 'choque']: conferir(id_, pele)
