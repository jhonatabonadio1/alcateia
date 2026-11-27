"""
As formas próprias do Setor Zero: o que não é caixa. Prisma chanfrado,
tronco (duas seções ligadas), cilindro e cone, viga entre dois pontos
quaisquer, tubo ao longo de uma linha (arco, nervura de cúpula, cabo),
setor de anel (parede curva de verdade) e sólido de revolução em torno de
um eixo qualquer (a antena parabólica inclinada).

Pedido do Jhon (26/11, depois das primeiras fotos): "quero um mapa mais
robusto, complexo, bonito, elegante. menos quadrado. crie elementos que
achar necessário". As peças do kit são boas, mas o que amarra um lugar é
a arquitetura — pilastra, talude, friso, pórtico, cobertura inclinada,
cúpula — e isso o kit não tem.

As formas são triângulos escritos direto nas coordenadas do mundo e
juntados **por material**: no fim (`fechar`) cada material vira uma peça
só do kit (`sz_<material>_f_<nome>`), posta na origem. Poucos nós e
poucas malhas, por mais formas que haja. O material sai do nome, como nas
chapas (`kits/detalhe.py: chapa_nomeada`), e a cor é a da chapa de mesmo
nome (`setor_zero.CORES`). **Não têm colisão**: o que barra o caminho
leva uma caixa escondida à parte (`setor_zero.solido`).

As medidas entram nas coordenadas da planta: (x, y) no chão e a altura.
"""
import math

import numpy as np

from .kits.kit_fbx import Peca

CX, CY = 75.0, 50.0


def W(x, y, h=0.0):
    """Da planta para o mundo."""
    return np.array([x - CX, h, y - CY], float)


class Ruido:
    """Ruído de valor em duas dimensões, sempre igual para a mesma semente (o mapa sai o mesmo a cada geração). Em
    `fractal`, oitavas somadas (relevo macio: nuvem, duna); em `cristas`, o ruído dobrado (1 − |2n − 1|)², que dá
    serras: linhas de cumeada contínuas com vales entre elas, em vez de cones soltos."""

    def __init__(self, semente):
        self.t = np.random.default_rng(semente).random((256, 256))

    def __call__(self, x, y):
        x, y = np.asarray(x, float), np.asarray(y, float)
        xi, yi = np.floor(x).astype(int), np.floor(y).astype(int)
        fx, fy = x - xi, y - yi
        u, v = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
        t = self.t
        a, b, c, d = t[xi % 256, yi % 256], t[(xi + 1) % 256, yi % 256], t[xi % 256, (yi + 1) % 256], t[(xi + 1) % 256, (yi + 1) % 256]
        return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v

    def fractal(self, x, y, oitavas=4, ganho=0.5):
        x, y = np.asarray(x, float), np.asarray(y, float)
        s, amp, tot = 0.0, 1.0, 0.0
        for k in range(oitavas):
            s = s + amp * self(x * 2 ** k + 17.3 * k, y * 2 ** k + 9.1 * k); tot += amp; amp *= ganho
        return s / tot

    def cristas(self, x, y, oitavas=4, ganho=0.5):
        x, y = np.asarray(x, float), np.asarray(y, float)
        s, amp, tot, peso = 0.0, 1.0, 0.0, 1.0
        for k in range(oitavas):
            n = (1.0 - np.abs(2.0 * self(x * 2 ** k + 31.7 * k, y * 2 ** k + 5.3 * k) - 1.0)) ** 2
            s = s + amp * n * peso; tot += amp
            peso = np.clip(n * 1.6, 0.0, 1.0); amp *= ganho          # o detalhe se junta nas cumeadas
        return s / tot


class Formas:
    def __init__(self, M, nome):
        self.M, self.nome = M, nome
        self.tris = {}          # material -> [T × 3 × 3]
        self.normais = {}       # material -> [T × 3 × 3 ou None (a normal da face)]
        self.pegadas = []       # (x0, y0, x1, y1) de cada forma, na planta: para a conferência de vazios

    # ------------------------------------------------------------ base
    def _junta(self, mat, T, N=None):
        T = np.asarray(T, float).reshape(-1, 3, 3)
        if not len(T): return
        self.tris.setdefault(mat, []).append(T)
        self.normais.setdefault(mat, []).append(None if N is None else np.asarray(N, float).reshape(-1, 3, 3))
        self.pegadas.append((T[:, :, 0].min() + CX, T[:, :, 2].min() + CY, T[:, :, 0].max() + CX, T[:, :, 2].max() + CY))

    def grade(self, V, material, suave=True, fecha=False, fora=None):
        """Uma superfície em grade: `V` são os vértices (I × J × 3, do mundo); cada célula vira dois triângulos.
        `material(meio, normal) -> nome`, face a face (`meio` e `normal`: N × 3) — a neve por altitude e inclinação,
        a rocha em estratos. `suave`: a luz corre lisa de uma face para a outra (a normal é do vértice: terreno,
        nuvem); sem isso, cada face tem a sua (penhasco). `fecha`: a última fileira de I emenda na primeira.
        `fora(meios) -> N × 3`: para que lado a frente das faces olha (padrão: para cima)."""
        V = np.asarray(V, float)
        if fecha: V = np.concatenate([V, V[:1]], axis=0)
        I, J = V.shape[:2]
        a, b, c, d = V[:-1, :-1], V[1:, :-1], V[1:, 1:], V[:-1, 1:]
        T = np.concatenate([np.stack([a, b, c], axis=2).reshape(-1, 3, 3), np.stack([a, c, d], axis=2).reshape(-1, 3, 3)])
        n = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]); n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
        meio = T.mean(1)
        ref = np.tile([0.0, 1.0, 0.0], (len(T), 1)) if fora is None else np.asarray(fora(meio), float)
        vira = (n * ref).sum(1) < 0
        T[vira] = T[vira][:, [0, 2, 1]]; n[vira] = -n[vira]
        N = None
        if suave:
            # a normal de cada vértice: a média das faces em volta dele
            soma = np.zeros((I, J, 3))
            nq = n.reshape(2, I - 1, J - 1, 3)
            for k in range(2):
                for di, dj in ((0, 0), (1, 0), (1, 1), (0, 1)):
                    soma[di:I - 1 + di, dj:J - 1 + dj] += nq[k]
            if fecha: soma[0] += soma[-1]; soma[-1] = soma[0]
            soma /= np.maximum(np.linalg.norm(soma, axis=2, keepdims=True), 1e-12)
            na, nb, nc, nd = soma[:-1, :-1], soma[1:, :-1], soma[1:, 1:], soma[:-1, 1:]
            N = np.concatenate([np.stack([na, nb, nc], axis=2).reshape(-1, 3, 3), np.stack([na, nc, nd], axis=2).reshape(-1, 3, 3)])
            N[vira] = N[vira][:, [0, 2, 1]]
        # a tinta é por célula (os dois triângulos de uma célula saem do mesmo material): pintando triângulo a
        # triângulo, a borda entre a rocha e a neve saía em dente de serra
        q = len(T) // 2
        mq = (meio[:q] + meio[q:]) / 2; nq_ = n[:q] + n[q:]; nq_ /= np.maximum(np.linalg.norm(nq_, axis=1, keepdims=True), 1e-12)
        nomes = np.tile(np.asarray(material(mq, nq_)), 2)
        for nome in np.unique(nomes):
            sel = nomes == nome
            self.tris.setdefault(str(nome), []).append(T[sel])
            self.normais.setdefault(str(nome), []).append(None if N is None else N[sel])

    def convexo(self, mat, faces):
        """Um sólido convexo dado pelas faces (cada uma, um polígono de pontos do mundo). Cada face sai virada para
        fora (a normal aponta para longe do meio do sólido)."""
        meio = np.mean([p for f in faces for p in f], axis=0)
        T = []
        for f in faces:
            f = [np.asarray(p, float) for p in f]
            n = np.zeros(3)
            for i in range(1, len(f) - 1): n += np.cross(f[i] - f[0], f[i + 1] - f[0])
            if np.linalg.norm(n) < 1e-10: continue
            if np.dot(n, np.mean(f, axis=0) - meio) < 0: f = f[::-1]
            for i in range(1, len(f) - 1): T.append([f[0], f[i], f[i + 1]])
        self._junta(mat, T)

    def tronco(self, mat, A, B, tampas=True):
        """Duas seções de mesmo número de pontos (do mundo), ligadas: a de baixo `A` e a de cima `B`."""
        n = len(A)
        faces = [[A[i], A[(i + 1) % n], B[(i + 1) % n], B[i]] for i in range(n)]
        if tampas: faces += [list(A), list(B)]
        self.convexo(mat, faces)

    # ------------------------------------------------------------ sólidos de pé
    def prisma(self, mat, poligono, base, topo, encolhe=0.0, topo_poligono=None):
        """O polígono da planta levantado de `base` a `topo`. `encolhe`: quanto a seção de cima encolhe para o meio
        (0,2 = 20 %: um tronco). `topo_poligono`: outra seção em cima."""
        P = np.array(poligono, float); meio = P.mean(0)
        Q = np.array(topo_poligono, float) if topo_poligono is not None else meio + (P - meio) * (1 - encolhe)
        self.tronco(mat, [W(x, y, base) for x, y in P], [W(x, y, topo) for x, y in Q])

    def caixa(self, mat, x0, y0, x1, y1, base, topo, chanfro=0.0, encolhe=0.0):
        """Uma caixa da planta com os quatro cantos chanfrados (um octógono) e, com `encolhe`, mais estreita em cima."""
        c = min(chanfro, (x1 - x0) / 2 - 0.01, (y1 - y0) / 2 - 0.01)
        pol = ([(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c), (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]
               if c > 0 else [(x0, y0), (x1, y0), (x1, y1), (x0, y1)])
        self.prisma(mat, pol, base, topo, encolhe)

    def cilindro(self, mat, x, y, r, base, topo, n=16, r_topo=None, giro=0.0):
        r_topo = r if r_topo is None else r_topo
        a = [giro + 2 * math.pi * i / n for i in range(n)]
        self.tronco(mat, [W(x + r * math.cos(t), y + r * math.sin(t), base) for t in a],
                    [W(x + r_topo * math.cos(t), y + r_topo * math.sin(t), topo) for t in a])

    def setor(self, mat, cx, cy, r0, r1, a0, a1, base, topo, passo=5.0, r0_topo=None, r1_topo=None):
        """Um setor de anel (parede curva) de raio `r0` a `r1`, do ângulo `a0` ao `a1` (graus, na planta: 0 = +x,
        90 = +y). `r0_topo`/`r1_topo`: os raios em cima (parede em talude)."""
        r0t = r0 if r0_topo is None else r0_topo; r1t = r1 if r1_topo is None else r1_topo
        n = max(1, math.ceil(abs(a1 - a0) / passo))
        for k in range(n):
            t0, t1 = (math.radians(a0 + (a1 - a0) * (k + j) / n) for j in (0, 1))
            A = [W(cx + r * math.cos(t), cy + r * math.sin(t), base) for r, t in ((r0, t0), (r1, t0), (r1, t1), (r0, t1))]
            B = [W(cx + r * math.cos(t), cy + r * math.sin(t), topo) for r, t in ((r0t, t0), (r1t, t0), (r1t, t1), (r0t, t1))]
            self.tronco(mat, A, B)

    def anel(self, mat, cx, cy, r0, r1, base, topo, passo=7.5, **k):
        self.setor(mat, cx, cy, r0, r1, 0.0, 360.0, base, topo, passo, **k)

    # ------------------------------------------------------------ vigas e tubos (pontos do mundo)
    def viga(self, mat, p0, p1, largura, altura=None, cima=(0.0, 1.0, 0.0)):
        """Uma viga de seção `largura` × `altura` de `p0` a `p1` (pontos do mundo: use `W`)."""
        altura = largura if altura is None else altura
        p0, p1 = np.asarray(p0, float), np.asarray(p1, float); d = p1 - p0; L = np.linalg.norm(d)
        if L < 1e-6: return
        d = d / L; up = np.asarray(cima, float)
        if abs(np.dot(d, up)) > 0.97: up = np.array([1.0, 0.0, 0.0])
        lado = np.cross(d, up); lado /= np.linalg.norm(lado); up = np.cross(lado, d)
        q = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
        self.tronco(mat, [p0 + lado * a * largura / 2 + up * b * altura / 2 for a, b in q],
                    [p1 + lado * a * largura / 2 + up * b * altura / 2 for a, b in q])

    def tubo(self, mat, pontos, largura, altura=None, cima=(0.0, 1.0, 0.0)):
        """Vigas emendadas ao longo de uma linha de pontos do mundo (arco, nervura, cabo)."""
        for a, b in zip(pontos, pontos[1:]):
            a, b = np.asarray(a, float), np.asarray(b, float); d = b - a; L = np.linalg.norm(d)
            if L < 1e-6: continue
            self.viga(mat, a - d / L * 0.01, b + d / L * 0.01, largura, altura, cima)

    def revolucao(self, mat, origem, eixo, perfil, n=20):
        """O perfil [(distância ao longo do eixo, raio)] girado em volta do eixo que sai de `origem` (mundo) na direção
        `eixo`: cilindro deitado, cone, prato de antena inclinado."""
        o = np.asarray(origem, float); e = np.asarray(eixo, float); e /= np.linalg.norm(e)
        u = np.cross(e, [0.0, 1.0, 0.0])
        if np.linalg.norm(u) < 1e-6: u = np.array([1.0, 0.0, 0.0])
        u /= np.linalg.norm(u); v = np.cross(e, u)
        aneis = [[o + e * d + (u * math.cos(2 * math.pi * i / n) + v * math.sin(2 * math.pi * i / n)) * r for i in range(n)] for d, r in perfil]
        for k, (A, B) in enumerate(zip(aneis, aneis[1:])):
            faces = [[A[i], A[(i + 1) % n], B[(i + 1) % n], B[i]] for i in range(n)]
            if k == 0: faces.append(list(A))
            if k == len(aneis) - 2: faces.append(list(B))
            self.convexo(mat, faces)

    def perfil(self, mat, pontos, origem, ao_longo, espessura):
        """Uma silhueta recortada numa chapa grossa, em pé: `pontos` = [(u, v)] em metros (u ao longo de `ao_longo`,
        um vetor do chão; v para cima), a partir de `origem` (mundo). A silhueta pode ser côncava (é cortada em
        triângulos pelas orelhas)."""
        P_ = [np.array(p, float) for p in pontos]
        area = sum(P_[i][0] * P_[(i + 1) % len(P_)][1] - P_[(i + 1) % len(P_)][0] * P_[i][1] for i in range(len(P_)))
        if area < 0: P_ = P_[::-1]                                # anti-horário
        o = np.asarray(origem, float); eu = np.asarray(ao_longo, float); eu = eu / np.linalg.norm(eu)
        up = np.array([0.0, 1.0, 0.0]); nn = np.cross(eu, up)
        def m3(p, lado): return o + eu * p[0] + up * p[1] + nn * lado * espessura / 2
        # as orelhas
        idx = list(range(len(P_))); tris = []
        def cruz(a_, b_, c_): return (b_[0] - a_[0]) * (c_[1] - a_[1]) - (b_[1] - a_[1]) * (c_[0] - a_[0])
        def dentro(pt, a_, b_, c_): return cruz(a_, b_, pt) > 1e-9 and cruz(b_, c_, pt) > 1e-9 and cruz(c_, a_, pt) > 1e-9
        guarda = 0
        while len(idx) > 3 and guarda < 10000:
            guarda += 1
            for k in range(len(idx)):
                i0, i1, i2 = idx[k - 1], idx[k], idx[(k + 1) % len(idx)]
                if cruz(P_[i0], P_[i1], P_[i2]) <= 1e-9: continue
                if any(dentro(P_[j], P_[i0], P_[i1], P_[i2]) for j in idx if j not in (i0, i1, i2)): continue
                tris.append((i0, i1, i2)); idx.pop(k); break
            else: break
        if len(idx) == 3: tris.append(tuple(idx))
        T = []
        for (a_, b_, c_) in tris:
            T.append([m3(P_[a_], 1), m3(P_[b_], 1), m3(P_[c_], 1)])          # a frente (a normal sai por +nn)
            T.append([m3(P_[a_], -1), m3(P_[c_], -1), m3(P_[b_], -1)])
        for i in range(len(P_)):
            a_, b_ = P_[i], P_[(i + 1) % len(P_)]
            T.append([m3(a_, 1), m3(a_, -1), m3(b_, -1)]); T.append([m3(a_, 1), m3(b_, -1), m3(b_, 1)])
        T = np.array(T)
        # (a ordem acima dá a normal certa quando nn = eu × cima aponta para a frente; conferida pela face da frente)
        n0 = np.cross(T[0][1] - T[0][0], T[0][2] - T[0][0])
        if np.dot(n0, nn) < 0: T = T[:, [0, 2, 1]]
        self._junta(mat, T)

    def aro(self, mat, centro, raio, eixo, grosso=0.12, n=28):
        """Um aro (um anel fino) de raio `raio` em volta de `centro` (mundo), no plano perpendicular a `eixo`."""
        c = np.asarray(centro, float); e = np.asarray(eixo, float); e = e / np.linalg.norm(e)
        u = np.cross(e, [0.0, 1.0, 0.0])
        if np.linalg.norm(u) < 1e-6: u = np.array([1.0, 0.0, 0.0])
        u /= np.linalg.norm(u); v = np.cross(e, u)
        pts = [c + (u * math.cos(2 * math.pi * k / n) + v * math.sin(2 * math.pi * k / n)) * raio for k in range(n + 1)]
        self.tubo(mat, pts, grosso, cima=e)

    def chapa(self, mat, pontos):
        """Um polígono plano solto (o pano de uma flâmula), visto dos dois lados."""
        f = [np.asarray(p, float) for p in pontos]
        self._junta(mat, [[f[0], f[i], f[i + 1]] for i in range(1, len(f) - 1)])

    # ------------------------------------------------------------ composições que se repetem
    def pilastra(self, mat, x, y, olha, largura, fundo, base, topo, afina=0.5, fundo_topo=None):
        """Uma pilastra encostada numa parede: em (x, y) na face, saindo `fundo` para o lado `olha` (graus do mundo:
        0 = +y da planta, 90 = +x, 180 = −y, −90 = −x), mais estreita e mais rasa em cima (talude)."""
        a = math.radians(olha); fx, fy = math.sin(a), math.cos(a); lx, ly = fy, -fx
        ft = fundo * afina if fundo_topo is None else fundo_topo; lt = largura * (0.55 + 0.45 * afina)
        A = [W(x + lx * s * largura / 2 + fx * f, y + ly * s * largura / 2 + fy * f, base) for s, f in ((-1, 0), (1, 0), (1, fundo), (-1, fundo))]
        B = [W(x + lx * s * lt / 2 + fx * f, y + ly * s * lt / 2 + fy * f, topo) for s, f in ((-1, 0), (1, 0), (1, ft), (-1, ft))]
        self.tronco(mat, A, B)

    def portico(self, mat, led, x, y, ao_x, vao, altura, grosso=0.5, ombro=1.2):
        """Um pórtico em trapézio sobre uma passagem de `vao` m centrada em (x, y): dois montantes inclinados para dentro,
        a verga e, por baixo dela, uma linha de LED. `ao_x`: a passagem corre ao longo de x (atravessa-se em y)."""
        def pt(s, t, h): return W(x + s, y + t, h) if ao_x else W(x + t, y + s, h)
        g = grosso / 2
        for lado in (-1, 1):
            fora, dentro = lado * (vao / 2 + 0.75), lado * (vao / 2 + 0.02)
            A = [pt(fora, -g, 0), pt(dentro, -g, 0), pt(dentro, g, 0), pt(fora, g, 0)]
            B = [pt(lado * (vao / 2 + 0.75 - 0.35), -g, altura), pt(lado * (vao / 2 - ombro), -g, altura), pt(lado * (vao / 2 - ombro), g, altura), pt(lado * (vao / 2 + 0.75 - 0.35), g, altura)]
            C = [pt(fora - lado * 0.12, -g, altura - 1.0), pt(dentro, -g, altura - 1.0), pt(dentro, g, altura - 1.0), pt(fora - lado * 0.12, g, altura - 1.0)]
            self.tronco(mat, A, C); self.tronco(mat, C, B)
        self.tronco(mat, [pt(-vao / 2 + ombro - 0.05, -g, altura - 0.02), pt(vao / 2 - ombro + 0.05, -g, altura - 0.02), pt(vao / 2 - ombro + 0.05, g, altura - 0.02), pt(-vao / 2 + ombro - 0.05, g, altura - 0.02)],
                    [pt(-vao / 2 + ombro - 0.4, -g, altura + 0.55), pt(vao / 2 - ombro + 0.4, -g, altura + 0.55), pt(vao / 2 - ombro + 0.4, g, altura + 0.55), pt(-vao / 2 + ombro - 0.4, g, altura + 0.55)])
        if led:
            self.viga(led, pt(-vao / 2 + ombro, 0, altura - 0.06), pt(vao / 2 - ombro, 0, altura - 0.06), grosso + 0.04, 0.07)

    def flamula(self, mastro, pano, x, y, base, altura, para=(1.0, 0.0), comprimento=2.6):
        """Um mastro com uma flâmula de duas pontas, lisa, na cor do bloco (sem emblema)."""
        self.viga(mastro, W(x, y, base), W(x, y, base + altura), 0.09)
        dx, dy = para; t = base + altura
        p = lambda s, h: W(x + dx * s, y + dy * s, h)
        self.chapa(pano, [p(0.05, t - 0.05), p(comprimento, t - 0.25), p(comprimento * 0.72, t - 0.7), p(comprimento, t - 1.15), p(0.05, t - 1.35)])

    # ------------------------------------------------------------ fim
    def fechar(self):
        """Cada material vira uma peça do kit, posta na origem. Devolve quantos triângulos."""
        total = 0
        for mat, listas in self.tris.items():
            partes = []
            for T_, N_ in zip(listas, self.normais[mat]):
                if N_ is None:
                    n = np.cross(T_[:, 1] - T_[:, 0], T_[:, 2] - T_[:, 0])
                    N_ = np.repeat((n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12))[:, None, :], 3, axis=1)
                partes.append(N_)
            T = np.concatenate(listas); N = np.concatenate(partes)
            nome = f"{mat}_f_{self.nome}"
            uv = self.M.kit[mat].uv[0, 0]
            self.M.kit.pecas[nome] = Peca(nome, T, N, np.tile(uv, (len(T), 3, 1)).astype(float))
            self.M.por(nome, 0.0, 0.0, y=0.0, colide=None, planta=False, pivo=True)
            total += len(T)
        self.tris, self.normais = {}, {}
        return total
