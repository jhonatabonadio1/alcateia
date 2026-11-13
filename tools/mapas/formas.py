"""
Formas que não são caixa: o que tira o mapa do "quadradão".

Cada função devolve uma `Malha` — triângulos com normal — que o kit grava
como .obj e põe na cena (`Mapa.malha`). Tudo em metros, Y para cima, com
a origem no centro da base, salvo aviso.

- extrusao: um perfil 2D esticado (barreira de concreto, viga, degraus,
  meio-fio, parede com arco, toldo).
- revolucao: um perfil girado em torno de Y (barril, tanque, coluna,
  poste, cúpula, chafariz).
- tubo: um círculo varrido por um caminho (cano, cabo, corrimão).
- relevo: uma grade de alturas (dunas, montes de neve, serra ao longe).
- pedra: uma esfera amassada por ruído.
- caixa_chanfrada: a caixa com as quinas quebradas, que pega luz.
- abobada: o teto curvo de um túnel, visto por dentro.
"""
import math
import random


def _sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def _add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def _mul(a, k): return (a[0] * k, a[1] * k, a[2] * k)
def _dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def _cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _norm(a):
    n = math.sqrt(_dot(a, a))
    return (0.0, 1.0, 0.0) if n < 1e-9 else (a[0] / n, a[1] / n, a[2] / n)


class Malha:
    """Triângulos soltos: cada canto tem posição e normal. A frente do
    triângulo é o lado de onde os cantos aparecem em sentido anti-horário."""

    def __init__(self):
        self.tris = []

    def tri(self, a, b, c, na=None, nb=None, nc=None):
        if na is None:
            na = nb = nc = _norm(_cross(_sub(b, a), _sub(c, a)))
        self.tris.append(((a, na), (b, nb), (c, nc)))

    def quad(self, a, b, c, d, na=None, nb=None, nc=None, nd=None):
        """a, b, c, d em volta, anti-horário visto de fora."""
        if na is None:
            na = nb = nc = nd = _norm(_cross(_sub(b, a), _sub(d, a)))
        self.tri(a, b, c, na, nb, nc)
        self.tri(a, c, d, na, nc, nd)

    def juntar(self, outra, desloca=(0, 0, 0), yaw=0.0):
        a = math.radians(yaw)
        c, s = math.cos(a), math.sin(a)

        def gira(v):
            return (v[0] * c + v[2] * s, v[1], -v[0] * s + v[2] * c)

        for t in outra.tris:
            self.tris.append(tuple((_add(gira(p), desloca), gira(n)) for p, n in t))
        return self

    def mover(self, desloca=(0, 0, 0), yaw=0.0):
        return Malha().juntar(self, desloca, yaw)

    def rodar(self, eixo, graus):
        """Gira em torno de X ou Z (para Y, use `mover(yaw=...)`): a roda
        que fica em pé, o cano que deita."""
        a = math.radians(graus)
        c, s = math.cos(a), math.sin(a)
        if eixo == "x":
            g = lambda v: (v[0], v[1] * c - v[2] * s, v[1] * s + v[2] * c)
        else:
            g = lambda v: (v[0] * c - v[1] * s, v[0] * s + v[1] * c, v[2])
        m = Malha()
        m.tris = [tuple((g(p), g(n)) for p, n in t) for t in self.tris]
        return m

    def escala(self, sx, sy, sz):
        m = Malha()
        for t in self.tris:
            m.tris.append(tuple(((p[0] * sx, p[1] * sy, p[2] * sz), _norm((n[0] / sx, n[1] / sy, n[2] / sz))) for p, n in t))
        return m

    def limites(self):
        ps = [p for t in self.tris for p, _ in t]
        return tuple(min(p[i] for p in ps) for i in range(3)), tuple(max(p[i] for p in ps) for i in range(3))

    def pontos(self):
        vistos, out = set(), []
        for t in self.tris:
            for p, _ in t:
                k = tuple(round(x, 3) for x in p)
                if k not in vistos:
                    vistos.add(k)
                    out.append(k)
        return out

    def obj(self):
        vs, ns, vi, ni, faces = [], [], {}, {}, []
        for t in self.tris:
            face = []
            for p, n in t:
                kp = tuple(round(x, 4) for x in p)
                kn = tuple(round(x, 3) for x in n)
                if kp not in vi:
                    vi[kp] = len(vs) + 1
                    vs.append(kp)
                if kn not in ni:
                    ni[kn] = len(ns) + 1
                    ns.append(kn)
                face.append(f"{vi[kp]}//{ni[kn]}")
            faces.append("f " + " ".join(face))
        fmt = lambda v: " ".join(f"{x:.4f}".rstrip("0").rstrip(".") or "0" for x in v)
        return "\n".join(["o forma"] + ["v " + fmt(v) for v in vs] + ["vn " + fmt(n) for n in ns] + faces) + "\n"


# --------------------------------------------------------------- polígonos

def _area(poly):
    return sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1]
               for i in range(len(poly))) / 2.0


def triangular(poly):
    """Corta um polígono simples (pode ser côncavo) em triângulos, por
    orelhas. Devolve índices; o polígono deve estar em sentido anti-horário."""
    idx = list(range(len(poly)))
    out = []

    def convexo(a, b, c):
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]) > 1e-12

    def dentro(p, a, b, c):
        d1 = (p[0] - b[0]) * (a[1] - b[1]) - (a[0] - b[0]) * (p[1] - b[1])
        d2 = (p[0] - c[0]) * (b[1] - c[1]) - (b[0] - c[0]) * (p[1] - c[1])
        d3 = (p[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (p[1] - a[1])
        return not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0))

    guarda = 0
    while len(idx) > 3 and guarda < 10000:
        guarda += 1
        achou = False
        for k in range(len(idx)):
            i0, i1, i2 = idx[k - 1], idx[k], idx[(k + 1) % len(idx)]
            a, b, c = poly[i0], poly[i1], poly[i2]
            if not convexo(a, b, c):
                continue
            if any(dentro(poly[j], a, b, c) for j in idx if j not in (i0, i1, i2)):
                continue
            out.append((i0, i1, i2))
            idx.pop(k)
            achou = True
            break
        if not achou:      # degenerado: fecha em leque
            break
    for k in range(1, len(idx) - 1):
        out.append((idx[0], idx[k], idx[k + 1]))
    return out


# ------------------------------------------------------------------ formas

def extrusao(perfil, comprimento, tampas=True, suave=False):
    """Estica um perfil 2D (x, y) ao longo de Z, centrado em Z. O perfil
    pode vir em qualquer sentido. `suave` alisa as normais onde o contorno
    dobra pouco (curvas: lona, casco, arco) e deixa as quinas vivas."""
    if _area(perfil) < 0:
        perfil = perfil[::-1]
    n = len(perfil)
    h = comprimento / 2.0
    m = Malha()

    def lado(i):
        a, b = perfil[i % n], perfil[(i + 1) % n]
        return _norm((b[1] - a[1], -(b[0] - a[0]), 0))

    def no_canto(i, deste):
        """Normal no ponto i, vista do lado `deste`: média com o vizinho
        se a dobra for suave (menos de ~40 graus)."""
        outro = lado(i - 1) if deste == i else lado(i)
        meu = lado(deste)
        return _norm(_add(meu, outro)) if suave and _dot(meu, outro) > 0.77 else meu

    for i in range(n):
        a, b = perfil[i], perfil[(i + 1) % n]
        na, nb = no_canto(i, i), no_canto((i + 1) % n, i)
        m.quad((a[0], a[1], h), (a[0], a[1], -h), (b[0], b[1], -h), (b[0], b[1], h), na, na, nb, nb)
    if tampas:
        for (i, j, k) in triangular(perfil):
            a, b, c = perfil[i], perfil[j], perfil[k]
            m.tri((a[0], a[1], h), (b[0], b[1], h), (c[0], c[1], h))
            m.tri((a[0], a[1], -h), (c[0], c[1], -h), (b[0], b[1], -h))
    return m


def revolucao(perfil, lados=20, suave=True, arco=360.0):
    """Gira um perfil (raio, altura), de baixo para cima, em torno de Y.
    Raio 0 nas pontas fecha a forma. `suave` alisa também ao longo do
    perfil (cúpula, barril); sem ele, cada trecho tem a sua quina."""
    m = Malha()
    k = len(perfil)

    def normal_trecho(i):
        (r0, y0), (r1, y1) = perfil[i], perfil[i + 1]
        return _norm((y1 - y0, -(r1 - r0), 0))      # no plano (raio, altura)

    trechos = [normal_trecho(i) for i in range(k - 1)]
    passo = math.radians(arco) / lados
    for s in range(lados):
        a0, a1 = s * passo, (s + 1) * passo
        for i in range(k - 1):
            (r0, y0), (r1, y1) = perfil[i], perfil[i + 1]
            if suave:
                n0 = trechos[i] if i == 0 else _norm(_add(trechos[i - 1], trechos[i]))
                n1 = trechos[i] if i == k - 2 else _norm(_add(trechos[i], trechos[i + 1]))
            else:
                n0 = n1 = trechos[i]

            def ponto(r, y, a): return (r * math.cos(a), y, r * math.sin(a))
            def normal(n, a): return _norm((n[0] * math.cos(a), n[1], n[0] * math.sin(a)))

            p00, p01 = ponto(r0, y0, a0), ponto(r0, y0, a1)
            p10, p11 = ponto(r1, y1, a0), ponto(r1, y1, a1)
            if r0 > 1e-6 and r1 > 1e-6:
                m.quad(p00, p10, p11, p01, normal(n0, a0), normal(n1, a0), normal(n1, a1), normal(n0, a1))
            elif r1 > 1e-6:
                m.tri(p00, p10, p11, normal(n0, a0), normal(n1, a0), normal(n1, a1))
            elif r0 > 1e-6:
                m.tri(p00, p10, p01, normal(n0, a0), normal(n1, a0), normal(n0, a1))
    return m


def tubo(pontos, raio, lados=10, tampas=True):
    """Um cano pelo caminho `pontos` (lista de (x, y, z))."""
    m = Malha()
    aneis = []
    cima = (0.0, 1.0, 0.0)
    for i, p in enumerate(pontos):
        if i == 0:
            d = _norm(_sub(pontos[1], p))
        elif i == len(pontos) - 1:
            d = _norm(_sub(p, pontos[i - 1]))
        else:
            d = _norm(_add(_norm(_sub(p, pontos[i - 1])), _norm(_sub(pontos[i + 1], p))))
        ref = cima if abs(_dot(d, cima)) < 0.95 else (1.0, 0.0, 0.0)
        u = _norm(_cross(ref, d))
        v = _cross(d, u)
        # Na curva o anel é a bissetriz: alarga para o cano não afinar.
        larga = 1.0
        if 0 < i < len(pontos) - 1:
            cosm = _dot(d, _norm(_sub(pontos[i + 1], p)))
            larga = 1.0 / max(cosm, 0.5)
        anel = []
        for s in range(lados):
            a = 2 * math.pi * s / lados
            n = _add(_mul(u, math.cos(a)), _mul(v, math.sin(a)))
            anel.append((_add(p, _mul(n, raio * larga)), n))
        aneis.append(anel)
    for i in range(len(aneis) - 1):
        for s in range(lados):
            (a, na), (b, nb) = aneis[i][s], aneis[i][(s + 1) % lados]
            (c, nc), (d, nd) = aneis[i + 1][(s + 1) % lados], aneis[i + 1][s]
            m.quad(a, b, c, d, na, nb, nc, nd)
    if tampas:
        for anel, sentido in ((aneis[0], -1), (aneis[-1], 1)):
            centro = _mul([sum(p[i] for p, _ in anel) for i in range(3)], 1.0 / lados)
            for s in range(lados):
                a, b = anel[s][0], anel[(s + 1) % lados][0]
                if sentido > 0: m.tri(centro, a, b)
                else: m.tri(centro, b, a)
    return m


def relevo(largura, fundo, nx, nz, altura):
    """Uma grade de `nx` × `nz` quadros com a altura dada por
    `altura(x, z)`. Centrada na origem; a normal é alisada."""
    m = Malha()
    dx, dz = largura / nx, fundo / nz

    def p(i, j):
        x, z = -largura / 2 + i * dx, -fundo / 2 + j * dz
        return (x, altura(x, z), z)

    def n(i, j):
        x, z = -largura / 2 + i * dx, -fundo / 2 + j * dz
        e = 0.5 * min(dx, dz)
        return _norm((altura(x - e, z) - altura(x + e, z), 2 * e, altura(x, z - e) - altura(x, z + e)))

    for i in range(nx):
        for j in range(nz):
            a, b, c, d = p(i, j), p(i, j + 1), p(i + 1, j + 1), p(i + 1, j)
            m.quad(a, b, c, d, n(i, j), n(i, j + 1), n(i + 1, j + 1), n(i + 1, j))
    return m


def _icosfera(sub):
    t = (1 + 5 ** 0.5) / 2
    vs = [_norm(v) for v in ((-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t),
                             (0, -1, -t), (0, 1, -t), (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1))]
    fs = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
          (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10),
          (8, 6, 7), (9, 8, 1)]
    for _ in range(sub):
        meio, novo = {}, []

        def m(a, b):
            k = (min(a, b), max(a, b))
            if k not in meio:
                vs.append(_norm(_add(vs[a], vs[b])))
                meio[k] = len(vs) - 1
            return meio[k]

        for a, b, c in fs:
            ab, bc, ca = m(a, b), m(b, c), m(c, a)
            novo += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        fs = novo
    return vs, fs


def pedra(raio, semente=1, achatar=0.7, sub=2, rugosidade=0.28, base_plana=True):
    """Pedra: esfera amassada por ruído, com a base no chão (y = 0)."""
    rnd = random.Random(semente)
    ondas = [(_norm((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))), rnd.uniform(1.2, 3.4),
              rnd.uniform(0, 6.28), rnd.uniform(0.4, 1.0)) for _ in range(7)]
    vs, fs = _icosfera(sub)

    def desloca(v):
        k = 1.0 + rugosidade * sum(amp * math.sin(freq * _dot(v, eixo) * 3.0 + fase) for eixo, freq, fase, amp in ondas) / 3.5
        p = (v[0] * raio * k, v[1] * raio * k * achatar, v[2] * raio * k)
        if base_plana and p[1] < -raio * achatar * 0.45:
            p = (p[0], -raio * achatar * 0.45, p[2])
        return (p[0], p[1] + raio * achatar * 0.45, p[2])

    ps = [desloca(v) for v in vs]
    normais = [[0.0, 0.0, 0.0] for _ in ps]
    for a, b, c in fs:
        n = _cross(_sub(ps[b], ps[a]), _sub(ps[c], ps[a]))
        for i in (a, b, c):
            normais[i] = [normais[i][k] + n[k] for k in range(3)]
    normais = [_norm(n) for n in normais]
    m = Malha()
    for a, b, c in fs:
        # Meio-termo entre facetado e liso: pedra tem quina, mas não é dado.
        nf = _norm(_cross(_sub(ps[b], ps[a]), _sub(ps[c], ps[a])))
        mix = lambda i: _norm(_add(_mul(normais[i], 0.55), _mul(nf, 0.45)))
        m.tri(ps[a], ps[b], ps[c], mix(a), mix(b), mix(c))
    return m


def caixa(sx, sy, sz, centro=(0, 0, 0)):
    """Caixa simples, para juntar várias numa malha só (uma moldura de
    janela é um nó em vez de seis)."""
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    cx, cy, cz = centro
    v = lambda x, y, z: (cx + x * hx, cy + y * hy, cz + z * hz)
    m = Malha()
    m.quad(v(1, -1, -1), v(1, 1, -1), v(1, 1, 1), v(1, -1, 1))
    m.quad(v(-1, -1, 1), v(-1, 1, 1), v(-1, 1, -1), v(-1, -1, -1))
    m.quad(v(-1, 1, -1), v(-1, 1, 1), v(1, 1, 1), v(1, 1, -1))
    m.quad(v(-1, -1, 1), v(-1, -1, -1), v(1, -1, -1), v(1, -1, 1))
    m.quad(v(-1, -1, 1), v(1, -1, 1), v(1, 1, 1), v(-1, 1, 1))
    m.quad(v(1, -1, -1), v(-1, -1, -1), v(-1, 1, -1), v(1, 1, -1))
    return m


def caixa_chanfrada(sx, sy, sz, chanfro=0.04):
    """Caixa com as doze quinas quebradas, centrada na origem."""
    c = min(chanfro, sx / 2.01, sy / 2.01, sz / 2.01)
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    m = Malha()
    # As seis faces, recolhidas pelo chanfro.
    faces = [((1, 0, 0), (0, 1, 0), (0, 0, 1)), ((-1, 0, 0), (0, 0, 1), (0, 1, 0)),
             ((0, 1, 0), (0, 0, 1), (1, 0, 0)), ((0, -1, 0), (1, 0, 0), (0, 0, 1)),
             ((0, 0, 1), (1, 0, 0), (0, 1, 0)), ((0, 0, -1), (0, 1, 0), (1, 0, 0))]
    meia = lambda v: abs(v[0]) * hx + abs(v[1]) * hy + abs(v[2]) * hz
    cantos = {}
    for n, u, v in faces:
        hu, hv, hn = meia(u) - c, meia(v) - c, meia(n)
        q = []
        for su, sv in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            p = _add(_add(_mul(n, hn), _mul(u, su * hu)), _mul(v, sv * hv))
            q.append(p)
            sinal = tuple(int(round(n[i] + su * u[i] + sv * v[i])) for i in range(3))
            cantos.setdefault(sinal, {})[n] = p
        m.quad(q[0], q[1], q[2], q[3])
    # Os doze chanfros: entre cada par de faces vizinhas.
    for i, (n1, _, _) in enumerate(faces):
        for n2, _, _ in faces[i + 1:]:
            if _dot(n1, n2) != 0:
                continue
            eixo = _cross(n1, n2)
            pts = []
            for s in (-1, 1):
                sinal = tuple(int(round(n1[k] + n2[k] + s * eixo[k])) for k in range(3))
                pts.append((cantos[sinal][n1], cantos[sinal][n2]))
            (a1, a2), (b1, b2) = pts
            nn = _norm(_add(n1, n2))
            if _dot(_cross(_sub(b1, a1), _sub(a2, a1)), nn) > 0:
                m.quad(a1, b1, b2, a2, nn, nn, nn, nn)
            else:
                m.quad(a1, a2, b2, b1, nn, nn, nn, nn)
    # Os oito cantos.
    for sinal, por_face in cantos.items():
        a, b, cc = list(por_face.values())
        nn = _norm(sinal)
        if _dot(_cross(_sub(b, a), _sub(cc, a)), nn) > 0:
            m.tri(a, b, cc, nn, nn, nn)
        else:
            m.tri(a, cc, b, nn, nn, nn)
    return m


def abobada(raio, comprimento, lados=16, de=0.0, ate=180.0, por_dentro=True):
    """Casca curva ao longo de Z: o teto de um túnel. Ângulos em graus a
    partir do +X, subindo. `por_dentro`: a frente é a face de dentro."""
    m = Malha()
    h = comprimento / 2.0
    for s in range(lados):
        a0 = math.radians(de + (ate - de) * s / lados)
        a1 = math.radians(de + (ate - de) * (s + 1) / lados)
        p0, p1 = (raio * math.cos(a0), raio * math.sin(a0)), (raio * math.cos(a1), raio * math.sin(a1))
        k = -1.0 if por_dentro else 1.0
        n0, n1 = (k * math.cos(a0), k * math.sin(a0), 0), (k * math.cos(a1), k * math.sin(a1), 0)
        a, b, c, d = (p0[0], p0[1], h), (p0[0], p0[1], -h), (p1[0], p1[1], -h), (p1[0], p1[1], h)
        if por_dentro: m.quad(a, d, c, b, n0, n1, n1, n0)
        else: m.quad(a, b, c, d, n0, n0, n1, n1)
    return m


def arco_parede(largura, altura, espessura, vao, vao_altura, lados=12):
    """Parede com um vão de arco pleno no meio: largura × altura, o vão
    com `vao` de largura e `vao_altura` até o topo do arco. Base em y=0,
    espessura ao longo de Z."""
    r = vao / 2.0
    reto = max(vao_altura - r, 0.0)
    perfil = [(-largura / 2, 0), (-r, 0), (-r, reto)]
    for s in range(1, lados):
        a = math.pi - math.pi * s / lados
        perfil.append((r * math.cos(a), reto + r * math.sin(a)))
    perfil += [(r, reto), (r, 0), (largura / 2, 0), (largura / 2, altura), (-largura / 2, altura)]
    return extrusao(perfil, espessura)


def escada(largura, altura, comprimento, degraus, espessura=0.3):
    """Degraus subindo ao longo de +X, de y=0 a `altura`, centrados em Z.
    É uma laje inclinada (dá para passar por baixo), não um bloco."""
    dx, dy = comprimento / degraus, altura / degraus
    queda = espessura / math.cos(math.atan2(altura, comprimento))
    perfil = [(0, 0)]
    for i in range(degraus):
        perfil += [(i * dx, (i + 1) * dy), ((i + 1) * dx, (i + 1) * dy)]
    perfil += [(comprimento, altura - queda), (0, -queda)]
    return extrusao(perfil, largura)


def setor(raio_fora, raio_dentro, de, ate, profundidade, lados=16):
    """Um arco grosso (nervura de abóbada, aro): a fatia de anel entre dois
    raios e dois ângulos, esticada em Z."""
    fora = [(raio_fora * math.cos(math.radians(de + (ate - de) * i / lados)),
             raio_fora * math.sin(math.radians(de + (ate - de) * i / lados))) for i in range(lados + 1)]
    dentro = [(raio_dentro * math.cos(math.radians(de + (ate - de) * i / lados)),
               raio_dentro * math.sin(math.radians(de + (ate - de) * i / lados))) for i in range(lados + 1)]
    return extrusao(fora + dentro[::-1], profundidade, suave=True)


def toro(raio, grossura, lados=18, aneis=10):
    """Rosca deitada (pneu, aro), centrada na origem."""
    m = Malha()
    for i in range(lados):
        a0, a1 = 2 * math.pi * i / lados, 2 * math.pi * (i + 1) / lados
        for j in range(aneis):
            b0, b1 = 2 * math.pi * j / aneis, 2 * math.pi * (j + 1) / aneis

            def pn(a, b):
                n = (math.cos(a) * math.cos(b), math.sin(b), math.sin(a) * math.cos(b))
                return ((raio + grossura * math.cos(b)) * math.cos(a), grossura * math.sin(b),
                        (raio + grossura * math.cos(b)) * math.sin(a)), n

            (p00, n00), (p01, n01), (p11, n11), (p10, n10) = pn(a0, b0), pn(a0, b1), pn(a1, b1), pn(a1, b0)
            m.quad(p00, p01, p11, p10, n00, n01, n11, n10)
    return m
