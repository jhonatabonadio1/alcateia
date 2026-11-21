#!/usr/bin/env python3
"""
A faca e a granada redesenhadas (pedido do Jhon, 20/11: a granada "mais redonda, tá muito quadrada"; as facas "menos
quadradas e mais afiadas"). As duas eram malhas de poucas faces (a V40 do pacote, com a bola de 12 gomos, e a faca do
jogo); aqui são feitas por conta — torneadas e lofteadas —, nas mesmas medidas e no mesmo lugar das antigas (a mão e as
animações não mudam: ``tools/armas/conjuntos.py`` continua valendo).

    from formas_mao import faca, granada
    pecas = granada()      # [(arquivo, nó, dict(pos, normal, uv, parte))], no espaço da peça (m), como ``reais.arma``

``parte`` é o que cada triângulo é (``PARTES``): o desenho da pele pinta por ela (``tools/armas/peles.py``).
- a granada: a bola é uma esfera de 40 gomos com o colar e a espoleta torneados, a alavanca é uma chapa dobrada que
  acompanha a bola, o pino tem cabeça e olhal, a argola é um aro de arame redondo;
- a faca: a lâmina tem dorso, a face plana, o desbaste (o chanfro largo que desce até o fio) e o fio de espessura zero —
  afiada de verdade —, a ponta caída com o falso fio no dorso; a guarda e o cabo são ovais, o cabo com o bojo da palma e
  os sulcos dos dedos, e o pomo arredondado.
"""
import numpy as np

PARTES = dict(faca=['face', 'desbaste', 'dorso', 'guarda', 'cabo', 'pomo'], choque=['bola', 'espoleta', 'alavanca', 'pino', 'argola'])


# ---------------------------------------------------------------- ferramentas
def _malha(aneis, fecha=True, tampa0=False, tampa1=False):
    """Os triângulos entre anéis consecutivos (cada anel n × 3, o mesmo n), com as tampas (leque até o meio do anel)."""
    T = []
    for a, b in zip(aneis, aneis[1:]):
        n = len(a); fim = n if fecha else n - 1
        for i in range(fim):
            j = (i + 1) % n; T += [[a[i], a[j], b[i]], [a[j], b[j], b[i]]]
    for anel, liga in ((aneis[0], tampa0), (aneis[-1], tampa1)):
        if liga:
            c = anel.mean(0); n = len(anel)
            for i in range(n): T.append([c, anel[i], anel[(i + 1) % n]])
    T = np.array(T, float)
    area = np.linalg.norm(np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]), axis=1)
    return T[area > 1e-12]


def _para_fora(T):
    """Vira o sólido fechado para as faces olharem para fora (o volume com sinal positivo)."""
    v = np.einsum('ij,ij->i', T[:, 0], np.cross(T[:, 1], T[:, 2])).sum()
    return T if v > 0 else T[:, ::-1]


def _torno(perfil, eixo=(0.0, 0.0), gomos=40):
    """Sólido de revolução em volta do eixo vertical que passa por (x, z) = ``eixo``: ``perfil`` = [(raio, y)]."""
    ang = np.linspace(0, 2 * np.pi, gomos, endpoint=False)
    aneis = [np.stack([eixo[0] + r * np.cos(ang), np.full(gomos, y), eixo[1] + r * np.sin(ang)], 1) for r, y in perfil]
    return _malha(aneis), len(perfil) - 1


def _curva(pontos, por_trecho=6):
    """Uma curva lisa pelos pontos (Catmull-Rom), com ``por_trecho`` pontos entre cada dois."""
    P = np.array(pontos, float); P = np.concatenate([[2 * P[0] - P[1]], P, [2 * P[-1] - P[-2]]]); out = []
    for i in range(1, len(P) - 2):
        for t in np.linspace(0, 1, por_trecho, endpoint=False):
            out.append(0.5 * ((2 * P[i]) + (-P[i - 1] + P[i + 1]) * t + (2 * P[i - 1] - 5 * P[i] + 4 * P[i + 1] - P[i + 2]) * t * t + (-P[i - 1] + 3 * P[i] - 3 * P[i + 1] + P[i + 2]) * t ** 3))
    out.append(P[-2]); return np.array(out)


def _aro(centro, u, v, raio, fio, gomos=48, lados=10):
    """Um aro de arame redondo: o círculo de ``raio`` no plano (u, v), o arame de raio ``fio``."""
    n = np.cross(u, v); aneis = []
    for a in np.linspace(0, 2 * np.pi, gomos + 1):
        rad = np.cos(a) * u + np.sin(a) * v; c = centro + rad * raio
        aneis.append(np.array([c + fio * (np.cos(b) * rad + np.sin(b) * n) for b in np.linspace(0, 2 * np.pi, lados, endpoint=False)]))
    return _para_fora(_malha(aneis))


def _superelipse(a, b, n, lados, y0=0.0):
    """O contorno |x/a|^n + |y/b|^n = 1 (n = 2 é a elipse; maior, mais quadrado de canto redondo), em (x, y)."""
    t = np.linspace(0, 2 * np.pi, lados, endpoint=False); c, s = np.cos(t), np.sin(t)
    return np.stack([a * np.sign(c) * np.abs(c) ** (2 / n), y0 + b * np.sign(s) * np.abs(s) ** (2 / n)], 1)


def _peca(T, parte):
    n = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]); n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-20
    return dict(pos=T, normal=np.repeat(n[:, None, :], 3, 1), uv=np.zeros((len(T), 3, 2)), parte=np.asarray(parte, int))


# ---------------------------------------------------------------- a granada
BOLA_C, BOLA_R = np.array([-0.0066, -0.0103, 0.0]), 0.0297          # o meio e o raio da bola (os da V40 do pacote)


def granada():
    """A granada: (corpo — a bola, a espoleta e a alavanca —, nó ''), (pino — o pino e a argola, nesta ordem —, nó 'Pino')."""
    cx, cy, _ = BOLA_C; r = BOLA_R
    # ---- a bola, o colar e a espoleta: um perfil só, torneado
    rb = 0.0070; yb = cy - np.sqrt(r * r - rb * rb)
    perfil = [(0.0, yb + 0.0006), (rb * 0.6, yb), (rb, yb)]
    t0 = np.arcsin((yb - cy) / r); t1 = np.arcsin((0.0085 - cy) / r)
    perfil += [(r * np.cos(t), cy + r * np.sin(t)) for t in np.linspace(t0, t1, 22)[1:]]
    n_bola = len(perfil) - 1
    perfil += [(0.0240, 0.0092), (0.0240, 0.0122), (0.0230, 0.0132), (0.0150, 0.0146), (0.0122, 0.0158), (0.0112, 0.0170), (0.0112, 0.0326),
               (0.0120, 0.0334), (0.0128, 0.0342), (0.0128, 0.0356), (0.0120, 0.0366), (0.0060, 0.0368), (0.0, 0.0368)]
    T, _ = _torno(perfil, (cx, 0.0), 40); T = _para_fora(T)
    # (a parte de cada triângulo: pela altura do meio dele)
    parte = np.where(T[:, :, 1].mean(1) < 0.0088, 0, 1)

    # ---- a alavanca: uma chapa de 2,2 mm dobrada — o gancho de trás, o tampo sobre a espoleta, a dobra e a descida rente à bola
    R = r + 0.0023
    caminho = [(-0.0206, 0.0338), (-0.0222, 0.0362), (-0.0205, 0.0383), (-0.0120, 0.0388), (0.0030, 0.0388), (0.0105, 0.0380), (0.0160, 0.0350), (0.0190, 0.0300),
               (0.0203, 0.0235), (0.0207, 0.0165), (0.0204, 0.0110)]
    caminho += [(cx + R * np.cos(a), cy + R * np.sin(a)) for a in np.radians(np.arange(31.0, -63.0, -9.0))]
    caminho += [(cx + (R + 0.0008) * np.cos(np.radians(-68)), cy + (R + 0.0008) * np.sin(np.radians(-68))), (cx + (R + 0.0030) * np.cos(np.radians(-74)), cy + (R + 0.0030) * np.sin(np.radians(-74)))]
    C = _curva(caminho, 4); tan = np.gradient(C, axis=0); tan /= np.linalg.norm(tan, axis=1, keepdims=True); nor = np.stack([tan[:, 1], -tan[:, 0]], 1)   # para fora da bola
    h, c = 0.0011, 0.0006; aneis = []
    for k, (p, n_) in enumerate(zip(C, nor)):
        s = k / (len(C) - 1); w = 0.0098 if s < 0.3 else 0.0098 - 0.0015 * min((s - 0.3) / 0.15, 1.0)        # mais larga sobre a espoleta
        sec = [(-w + c, -h), (w - c, -h), (w, -h + c), (w, h - c), (w - c, h), (-w + c, h), (-w, h - c), (-w, -h + c)]
        aneis.append(np.array([[p[0] + n_[0] * b, p[1] + n_[1] * b, a] for a, b in sec]))
    Ta = _para_fora(_malha(aneis, tampa0=True, tampa1=True))
    corpo = _peca(np.concatenate([T, Ta]), np.concatenate([parte, np.full(len(Ta), 2)]))

    # ---- o pino: a haste que atravessa (ao longo de z), a cabeça de um lado e o olhal do outro; a argola passa no olhal
    px, py = 0.0110, 0.0296; ang = np.linspace(0, 2 * np.pi, 12, endpoint=False)
    def disco(z, raio): return np.stack([px + raio * np.cos(ang), py + raio * np.sin(ang), np.full(12, z)], 1)
    Tp = _para_fora(_malha([disco(-0.0146, 0.0016), disco(-0.0140, 0.0022), disco(-0.0128, 0.0022), disco(-0.0124, 0.0011), disco(0.0150, 0.0011)], tampa0=True, tampa1=True))
    olhal = _aro(np.array([px, py, 0.0150 + 0.0036]), np.array([0.0, 0.0, 1.0]), np.array([0.0, 1.0, 0.0]), 0.0036, 0.0009, 20, 8)
    Tp = np.concatenate([Tp, olhal])
    # (a argola: o aro da V40 — o meio, o plano e o raio dela)
    u = np.array([-0.471, 0.879, -0.080]); v = np.array([0.852, 0.429, -0.302]); u /= np.linalg.norm(u); v -= u * (u @ v); v /= np.linalg.norm(v)
    Tr = _aro(np.array([0.0088, 0.0004, 0.0209]), u, v, 0.0290, 0.0012, 56, 10)
    pino = _peca(np.concatenate([Tp, Tr]), np.concatenate([np.full(len(Tp), 3), np.full(len(Tr), 4)]))
    return [('corpo', '', corpo), ('pino', 'Pino', pino)]


# ---------------------------------------------------------------- a faca
Z_GUARDA, Z_PONTA, Z_POMO = -0.0520, -0.3100, 0.0640               # a guarda, a ponta da lâmina e o fim do pomo (os da faca do jogo)
DORSO, FIO, PONTA_Y = 0.0135, -0.0190, 0.0030                       # a altura do dorso, do fio e da ponta
ESPESSURA = 0.0027                                                  # meia espessura da lâmina
CABO_Y = -0.0035                                                    # a altura do eixo do cabo
# Na mão de primeira pessoa a faca vai rolada em volta do eixo do cabo: a câmera olha a faca de cima, pelo dorso, e só
# via um risco — rolada, mostra a face e o desbaste (o brilho da lâmina), como nas facas dos jogos de referência. O fio
# continua para fora e para baixo. No mundo (na mão dos outros) fica em pé.
GIRO_NA_MAO = -90.0


def rolar(pos, graus):
    """Os pontos girados em volta do eixo do cabo (z, em y = CABO_Y)."""
    a = np.radians(graus); c, s_ = np.cos(a), np.sin(a); p = np.array(pos, float); x, y = p[..., 0].copy(), p[..., 1] - CABO_Y
    p[..., 0] = c * x - s_ * y; p[..., 1] = s_ * x + c * y + CABO_Y; return p


def lamina_em(s):
    """A lâmina no ponto ``s`` (0 na guarda, 1 na ponta; aceita vetor): (z, dorso, fio, linha do desbaste, meia espessura,
    quanto do falso fio)."""
    s = np.asarray(s, float); z = Z_GUARDA - 0.002 + s * (Z_PONTA - Z_GUARDA + 0.002)
    tc = np.clip((s - 0.62) / 0.38, 0, 1); dorso = DORSO - (DORSO - PONTA_Y) * (0.35 * tc + 0.65 * tc * tc)        # a ponta caída
    tb = np.clip((s - 0.52) / 0.48, 0, 1); fio = FIO + (PONTA_Y - FIO) * (0.45 * tb * tb + 0.55 * (1 - np.sqrt(np.clip(1 - tb * tb, 0, 1))))   # a barriga
    ric = np.clip((s - 0.045) / 0.035, 0, 1)                                                                      # o talão (sem fio) e a subida do desbaste
    fio = np.where(s < 0.08, -0.0130 + (fio + 0.0130) * ric * ric * (3 - 2 * ric), fio)
    alto = dorso - fio; linha = fio + np.minimum(0.0150, 0.62 * alto) * ric + 0.0006 * (1 - ric)
    meia = ESPESSURA * (1 - 0.82 * np.clip((s - 0.55) / 0.45, 0, 1) ** 1.6)
    return z, dorso, fio, linha, meia, tc


def faca():
    """A faca, com a lâmina para −Z, o fio para baixo (−Y) e a guarda em z = −5,2 cm: uma peça só (nó '')."""
    # ---- a lâmina: 7 pontos por estação — o fio, a linha do desbaste dos dois lados, o canto do dorso e o dorso
    S = np.concatenate([np.linspace(0, 0.1, 9)[:-1], np.linspace(0.1, 0.55, 14)[:-1], 1 - (1 - np.linspace(0.55, 1, 30)) ** 1.0]); S[-1] = 1.0
    z, dorso, fio, linha, meia, tc = lamina_em(S); aneis = []
    for k in range(len(S)):
        t = meia[k]; topo = t * 0.55 * (1 - 0.92 * tc[k]); canto = dorso[k] - min(0.0013 + 0.004 * tc[k], 0.45 * (dorso[k] - linha[k]))      # o falso fio: o dorso afina na ponta caída
        if k == len(S) - 1: aneis.append(np.tile([0.0, PONTA_Y, z[k]], (7, 1))); continue
        aneis.append(np.array([[0.0, fio[k], z[k]], [t, linha[k], z[k]], [t, canto, z[k]], [topo, dorso[k], z[k]], [-topo, dorso[k], z[k]], [-t, canto, z[k]], [-t, linha[k], z[k]]]))
    T = []; P = []
    for a, b in zip(aneis, aneis[1:]):
        for i in range(7):
            j = (i + 1) % 7; tri = [[a[i], a[j], b[i]], [a[j], b[j], b[i]]]
            p = 1 if i in (0, 6) else 0 if i in (1, 5) else 2
            T += tri; P += [p, p]
    c = aneis[0].mean(0)
    for i in range(7): T.append([c, aneis[0][i], aneis[0][(i + 1) % 7]]); P.append(2)
    T = np.array(T, float); P = np.array(P); ok = np.linalg.norm(np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]), axis=1) > 1e-12; T, P = T[ok], P[ok]
    vol = np.einsum('ij,ij->i', T[:, 0], np.cross(T[:, 1], T[:, 2])).sum()
    if vol < 0: T = T[:, ::-1]
    partes = [T]; ids = [P]

    def tubo(estacoes, lados, parte):
        """Um sólido de seções ovais ao longo de z: estacoes = [(z, a, b, n, y0, f(ângulo) ou None)]."""
        aneis = []
        for zz, a, b, n, y0, mod in estacoes:
            xy = _superelipse(a, b, n, lados, y0)
            if mod is not None: xy = np.stack([xy[:, 0], y0 + (xy[:, 1] - y0) * mod(xy)], 1)
            aneis.append(np.c_[xy, np.full(lados, zz)])
        Tt = _para_fora(_malha(aneis, tampa0=True, tampa1=True)); partes.append(Tt); ids.append(np.full(len(Tt), parte))

    # ---- a guarda: uma chapa oval de 9 mm, de quina arredondada, um pouco mais comprida para baixo (protege os dedos)
    gy, ga, gb = -0.0030, 0.0122, 0.0288
    tubo([(-0.0572, ga * 0.80, gb * 0.90, 2.6, gy, None), (-0.0562, ga * 0.96, gb * 0.98, 2.6, gy, None), (-0.0550, ga, gb, 2.6, gy, None), (-0.0494, ga, gb, 2.6, gy, None),
          (-0.0482, ga * 0.96, gb * 0.98, 2.6, gy, None), (-0.0472, ga * 0.80, gb * 0.90, 2.6, gy, None)], 32, 3)
    # ---- o cabo: oval, com o bojo da palma no meio, três sulcos para os dedos embaixo e a boca mais larga nas pontas
    cy0, ca, cb = CABO_Y, 0.0101, 0.0127; est = []
    for u in np.linspace(0, 1, 34):
        zz = -0.0476 + u * (0.0522 + 0.0476); bojo = 1 + 0.075 * np.sin(np.pi * u) + 0.07 * (np.clip(1 - u / 0.07, 0, 1) ** 2 + np.clip(1 - (1 - u) / 0.07, 0, 1) ** 2)
        sulco = 0.085 * (0.5 - 0.5 * np.cos(2 * np.pi * 3 * np.clip((u - 0.06) / 0.88, 0, 1)))
        est.append((zz, ca * bojo, cb * bojo, 2.35, cy0, (lambda xy, s_=sulco, y_=cy0: np.where(xy[:, 1] < y_, 1 - s_, 1.0))))
    tubo(est, 28, 4)
    # ---- o pomo: a tampa de metal, arredondada
    tubo([(0.0518, ca * 1.10, cb * 1.08, 2.4, cy0, None), (0.0532, ca * 1.17, cb * 1.14, 2.4, cy0, None), (0.0596, ca * 1.17, cb * 1.14, 2.4, cy0, None), (0.0620, ca * 1.08, cb * 1.06, 2.3, cy0, None),
          (0.0634, ca * 0.90, cb * 0.88, 2.2, cy0, None), (0.0640, ca * 0.62, cb * 0.60, 2.0, cy0, None)], 28, 5)
    return [('faca', '', _peca(np.concatenate(partes), np.concatenate(ids)))]


FORMAS = dict(faca=faca, choque=granada)

if __name__ == '__main__':
    for nome, f in FORMAS.items():
        for arquivo, no, m in f():
            q = m['pos'].reshape(-1, 3); print(f"{nome}/{arquivo}: {len(m['pos'])} triângulos, de {q.min(0).round(4)} a {q.max(0).round(4)}; partes {np.bincount(m['parte']).tolist()}")
