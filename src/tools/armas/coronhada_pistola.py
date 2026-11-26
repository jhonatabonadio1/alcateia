#!/usr/bin/env python3
"""
A coronhada da pistola em primeira pessoa, com uma mão só (pedido do Jhon, 26/11: "a
coronhada da pistola deve ser feita com uma das mãos, como se estivesse batendo com a
parte de baixo da pistola").

    python3 src/tools/armas/coronhada_pistola.py                 # deagle*.glb de src/assets/armas/fp
    python3 src/tools/armas/coronhada_pistola.py src/assets/armas/fp/deagle.glb [outro.glb ...]

Troca o clipe ``coronhada`` do conjunto (0,40 s, o mesmo tempo e o golpe no mesmo instante
de antes: 0,075 s, ``Weapon.ButtHitDelay``). Era a do fuzil, com as duas mãos: a arma
atravessada na frente do rosto, batendo com a lateral. Agora a mão direita levanta a
pistola (o cano sobe, a base do cabo vira para a frente) e desce o cabo como um martelo no
que está na frente; a esquerda solta a arma e sai da tela, e volta a segurar no fim.

Como o ``correr.py``, grava o clipe no ``.glb`` que existe, sem refazer o conjunto: o resto
fica como está. Os braços saem da mão (o antebraço volta para perto de onde está no
parado em relação a ela: o pulso não quebra), e o script imprime a dobra do pulso no pior
quadro e o alcance que faltou.
"""
import glob, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from esqueleto import *
import conjuntos, correr

CLIPE = 'coronhada'
DURA = 0.40
BRACO_D = 0.75              # o antebraço direito acompanha a mão (``correr.para_onde``): o cano sobe e o cotovelo desce

# tempo (s): (pitch, yaw, roll) da arma em graus, em volta da mão direita; (x, y, z) da mão em metros,
# no espaço do olho (x direita, y cima, −z frente); quanto a mão esquerda já soltou (0 a 1).
# pitch +: o cano sobe e a base do cabo vai para a frente.
CHAVES = [
    (0.000, (0, 0, 0), (0.000, 0.000, 0.000), 0.0),
    (0.040, (72, -6, -10), (0.040, 0.080, -0.020), 0.7),     # arma: a mão sobe ao lado do rosto, o cano para cima
    (0.075, (48, 6, -14), (-0.060, -0.010, -0.130), 1.0),   # o golpe: desce como martelo, a base do cabo na frente
    (0.140, (40, 7, -15), (-0.065, -0.035, -0.160), 1.0),   # a empurrada
    (0.210, (44, 6, -14), (-0.062, -0.025, -0.150), 1.0),   # segura
    (0.400, (0, 0, 0), (0.000, 0.000, 0.000), 0.0),
]
ESQUERDA = (-0.040, -0.170, 0.040)      # quanto o braço esquerdo desce quando solta a arma (m, no espaço do olho)
CAMERA = (-1.0, 0.5, 0.0)               # o tranco da câmera no golpe (pitch, yaw, roll em graus)


def suave(u): return u * u * (3 - 2 * u)


def em(t):
    for (t0, g0, m0, s0), (t1, g1, m1, s1) in zip(CHAVES, CHAVES[1:]):
        if t <= t1:
            u = suave((t - t0) / (t1 - t0))
            return np.array(g0) * (1 - u) + np.array(g1) * u, np.array(m0) * (1 - u) + np.array(m1) * u, s0 * (1 - u) + s1 * u
    return np.zeros(3), np.zeros(3), 0.0


def tranco(t):
    """0 → 1 no golpe → 0: o tranco da câmera."""
    if t < 0.05 or t > 0.30: return 0.0
    return suave((t - 0.05) / 0.04) if t < 0.09 else 1.0 - suave((t - 0.09) / 0.21)


def fazer(path):
    E = Esqueleto(path); P = E.parado; G0 = E.arma_de(P); nome = os.path.splitext(os.path.basename(path))[0]
    mao = E.mao_na_arma(P, 'r')
    h0 = E.olho(E.mundo(P, E.mao['r']))[:3, 3]
    braco0 = correr.antebraco_do_parado(E, P); peso = {'r': BRACO_D}

    def quadro(t):
        g, m, solta = em(t)
        # a esquerda não entra aqui: o braço dela desce inteiro, sem girar osso (``correr.descer_braco``; veja o
        # ``BAIXA`` da pistola no ``correr.py``: girando, a troca de animação do Godot o joga para cima)
        return dict(G=correr.levar(E, G0, m, g, pivo=h0), pega={'r': mao}, cam=np.array(CAMERA) * tranco(t)), np.array(ESQUERDA) * solta

    n = int(round(DURA * conjuntos.FPS)); quadros = []; falta = 0.0; pior = {'r': 0.0, 'l': 0.0}
    for i in range(n + 1):
        t = i / conjuntos.FPS
        q, desce = quadro(t)
        pose, f = conjuntos.posar(E, q, P); falta = max(falta, f)
        if 0 < i < n:                                                    # o primeiro e o último quadro são o parado, sem mexer
            correr.descer_braco(E, pose, 'l', desce)
            for l in 'r':
                u = min(1.0, t / 0.04, (DURA - t) / 0.10)                # o braço sai do parado e volta para ele sem tranco
                conjuntos.antebraco_pela_mao(E, pose, l, direcao=correr.para_onde(E, pose, l, braco0[l], peso[l] * u + (1 - u) * 0.0)
                                             if u >= 1.0 else _entre(E, pose, l, braco0[l], peso[l], u))
        else: pose = dict(P)
        for l in 'rl': pior[l] = max(pior[l], correr.dobra(E, pose, l))
        quadros.append(pose)
    E.gravar_clipe(CLIPE, quadros, DURA)
    E.salvar()
    # o clipe entra por cima do parado numa mistura de 0,04 s: só os primeiros quadros se misturam com ele
    fora = correr.conferir_troca(E, quadros[1:4], de=('parado',))
    print(f"{nome}: {CLIPE} {DURA:.2f} s, {n + 1} quadros; alcance que faltou {falta * 10:.1f} mm; "
          f"pulso dobrado até D {pior['r']:.0f}° E {pior['l']:.0f}° (parado: D {correr.dobra(E, P, 'r'):.0f}° E {correr.dobra(E, P, 'l'):.0f}°); "
          f"na entrada a mão sai do caminho até D {fora['r']:.0f} cm E {fora['l']:.0f} cm")


def _entre(E, pose, l, parado, peso, u):
    """Entre o antebraço como a cinemática inversa o deixou (u 0) e o de ``para_onde`` (u 1)."""
    C = E.mundo(pose, E.N(f'lowerarm_{l}'))[:3, 3]; H = E.mundo(pose, E.N(f'hand_{l}'))[:3, 3]
    a = (H - C) / np.linalg.norm(H - C); b = correr.para_onde(E, pose, l, parado, peso)
    v = a * (1 - u) + b * u; return v / np.linalg.norm(v)


if __name__ == '__main__':
    arquivos = sys.argv[1:] or sorted(glob.glob(os.path.join(conjuntos.FP, 'deagle*.glb')))
    for p in arquivos: fazer(p)
