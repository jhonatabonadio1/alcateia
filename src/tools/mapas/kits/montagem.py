"""
Monta um mapa com as peças de um kit (``kit_fbx.Kit``): põe cada peça no lugar, grava o desenho num .glb e entrega ao
``kit.Mapa`` o que ele precisa (o modelo, as caixas de colisão, a planta para o minimapa).

    M = Montagem('fps1')
    M.por('House_01', x=-20, z=14, yaw=90)            # o meio da peça (visto de cima) em (x, z), a base em y = 0
    M.por('Box_01', 3, 2, colide='caixa')             # colisão: 'malha' (a própria peça), 'caixa' (a caixa dela) ou None
    M.simetrico()                                      # tudo o que foi posto até aqui, girado 180° em volta do centro
    M.aplicar(m, 'Cidade', 'cidade')                  # m: kit.Mapa

**O desenho** vai em ``assets/mapas/pecas/<arquivo>.glb``: uma malha por peça diferente e um nó por peça posta (o Godot
desenha as iguais juntas). O material é um só, com a textura do kit embutida, desenhado dos dois lados (as peças têm
paredes de uma face só).

**A colisão**: a peça com ``colide='malha'`` leva o sufixo ``-col`` no nome do nó — o Godot, ao importar, cria o corpo e a
forma de colisão a partir da própria malha. A com ``colide='caixa'`` vira uma caixa escondida no .tscn
(``Mapa.caixa(oculta=True)``), com a cor média da peça: é o que o minimapa desenha (com ``planta=False`` ela fica fora
do minimapa: a laje de um andar de cima, o teto). Para as de malha o minimapa recebe uma
caixa invisível do tamanho da peça (nó ``Planta``; ``Minimap.Collect`` lê qualquer ``BoxMesh``).

**Onde se anda** (``ocupacao``, ``caminho``): uma grade vista de cima, tirada da colisão, para pôr os pontos de nascimento
e achar as rotas dos bots com folga das paredes.
"""
import heapq
import json
import math
import os
import struct

import numpy as np

from .kit_fbx import Kit, RAIZ


def giro(yaw):
    a = math.radians(yaw); c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]], float)


CAIXA = np.array([[x, y, z] for x in (0, 1) for y in (0, 1) for z in (0, 1)], float)
CAIXA_TRI = [(0, 1, 3), (0, 3, 2), (4, 6, 7), (4, 7, 5), (0, 4, 5), (0, 5, 1), (2, 3, 7), (2, 7, 6), (0, 2, 6), (0, 6, 4), (1, 5, 7), (1, 7, 3)]


class Item:
    def __init__(self, peca, G, D, t, colide, planta, nome):
        """``G``: o giro (rotação pura); ``D``: a escala por eixo da peça (negativa em x = espelhada); ``R`` = G · diag(D)."""
        self.peca, self.G, self.D, self.t, self.colide, self.planta, self.nome = peca, G, np.asarray(D, float), t, colide, planta, nome
        self.R = G @ np.diag(self.D)

    def pontos(self, P):
        return P @ self.R.T + self.t

    def cantos(self):
        """Os 8 cantos da caixa da peça, no mundo."""
        return self.pontos(self.peca.lo + CAIXA * self.peca.tam)


class Montagem:
    def __init__(self, kit, padrao=None):
        """``padrao``: função nome -> 'malha' | 'caixa' | None, para quando ``por`` não diz a colisão."""
        self.kit = kit if isinstance(kit, Kit) else Kit(kit)
        self.itens = []
        self.padrao = padrao or (lambda nome: 'malha')
        self._cor = {}
        self.cortes = []          # caixas (lo, hi) no mundo: triângulo de colisão todo dentro de uma sai (porta que se abre)
        self.barras = []          # caixas só de colisão: (centro, tamanho, yaw, cor) — o tronco da árvore, o poste

    # ------------------------------------------------------------ pôr
    def por(self, nome, x, z, yaw=0.0, y=0.0, colide='padrao', planta=True, pivo=False, escala=1.0, espelho=False):
        """``pivo=False``: (x, z) é o meio da caixa da peça já girada e ``y`` a base dela. ``pivo=True``: é a origem da peça."""
        p = self.kit[nome]; G = giro(yaw); D = np.broadcast_to(np.asarray(escala, float), (3,)).copy()          # escala: um número ou (x, y, z)
        if espelho: D[0] = -D[0]
        R = G @ np.diag(D)
        if pivo: t = np.array([x, y, z], float)
        else:
            q = (p.lo + CAIXA * p.tam) @ R.T; lo, hi = q.min(0), q.max(0)
            t = np.array([x - (lo[0] + hi[0]) / 2, y - lo[1], z - (lo[2] + hi[2]) / 2])
        it = Item(p, G, D, t, self.padrao(nome) if colide == 'padrao' else colide, planta, nome)
        self.itens.append(it); return it

    def chapa(self, nome, x0, z0, x1, z1, y=0.0, alto=0.02, colide=None, planta=False):
        """Uma peça de ``Kit.chapa`` esticada de (x0, z0) a (x1, z1), com ``alto`` de altura: o asfalto, a calçada, a faixa."""
        return self.por(nome, (x0 + x1) / 2, (z0 + z1) / 2, y=y, escala=(abs(x1 - x0), alto, abs(z1 - z0)), colide=colide, planta=planta, pivo=True)

    def encaixar(self, nome, x0, z0, x1, z1, yaw=0.0, y=0.0, alto=None, **k):
        """A peça esticada para a pegada dela (já girada) encher o retângulo (x0, z0)–(x1, z1); ``alto``: a altura que fica."""
        p = self.kit[nome]; lx, lz = abs(x1 - x0), abs(z1 - z0)
        if round(yaw / 90) % 2: lx, lz = lz, lx
        return self.por(nome, (x0 + x1) / 2, (z0 + z1) / 2, yaw=yaw, y=y, escala=(lx / p.tam[0], alto / p.tam[1] if alto else 1.0, lz / p.tam[2]), **k)

    def muro(self, nomes, xa, za, xb, zb, modulo, frente=None, y=0.0, alto=None, semente=0, grosso=None, recuo=0.0, **k):
        """Uma fileira de módulos de parede de (xa, za) a (xb, zb) (reta, ao longo de x ou de z), cada um esticado para a
        fileira fechar a conta. ``nomes``: um nome ou vários (vão alternando). ``frente``: o giro da face (0, 90, 180, −90).
        ``recuo``: quanto a fileira anda para trás da face (a parede de enfeite na frente de um maciço)."""
        if isinstance(nomes, str): nomes = [nomes]
        ao_x = abs(xb - xa) >= abs(zb - za); L = abs(xb - xa) if ao_x else abs(zb - za)
        if L < 0.05: return []
        n = max(1, int(round(L / modulo))); yaw = frente if frente is not None else (0.0 if ao_x else 90.0); out = []
        for i in range(n):
            f = (i + 0.5) / n; nome = nomes[(i * 5 + semente) % len(nomes)]; p = self.kit[nome]
            esc = ((L / n) / p.tam[0], alto / p.tam[1] if alto else 1.0, grosso / p.tam[2] if grosso else 1.0)
            rx, rz = -math.sin(math.radians(yaw)) * recuo, -math.cos(math.radians(yaw)) * recuo          # para trás da face
            out.append(self.por(nome, xa + (xb - xa) * f + rx, za + (zb - za) * f + rz, yaw=yaw, y=y, escala=esc, **k))
        return out

    def verga(self, nome, x, z, ao_x, largo, de, ate, grosso=0.3, **k):
        """Uma chapa atravessada num vão de parede (por cima da porta, por baixo da janela), de y = ``de`` a ``ate``."""
        hx, hz = (largo / 2, grosso / 2) if ao_x else (grosso / 2, largo / 2)
        return self.chapa(nome, x - hx, z - hz, x + hx, z + hz, y=de, alto=ate - de, **k)

    def encostar(self, nome, x, z, yaw, dist, y=0.0, folga=0.03, lado=0.0, **k):
        """A peça de costas para a parede que fica a ``dist`` de (x, z), olhando para ``yaw`` (o que ``Planta.beiras`` dá);
        ``lado``: quanto ela anda ao longo da parede."""
        p = self.kit[nome]; esc = np.broadcast_to(np.asarray(k.get('escala', 1.0), float), (3,)); a = math.radians(yaw)
        fx, fz = math.sin(a), math.cos(a); d = dist - p.tam[2] * abs(esc[2]) / 2 - folga
        return self.por(nome, x - fx * d + fz * lado, z - fz * d - fx * lado, yaw=yaw, y=y, **k)

    def _caixa_de(self, it):
        if not hasattr(it, '_aabb'): c = it.cantos(); it._aabb = (c.min(0), c.max(0))
        return it._aabb

    def tentar(self, por, encolhe=0.1):
        """Põe um enfeite (o que a função ``por`` puser) **se não trombar** com o que já está posto: a caixa de cada peça
        nova, encolhida de ``encolhe``, não pode entrar na de nenhuma peça de antes (o que é rente ao chão — tapete,
        bueiro, asfalto — não conta). Trombou, sai tudo o que a função pôs. Devolve se ficou."""
        n, nb = len(self.itens), len(self.barras)
        if getattr(self, '_n_cache', -1) != n or True:
            antes = [self._caixa_de(it) for it in self.itens]
            antes = [(lo, hi) for lo, hi in antes if hi[1] - lo[1] > 0.12 and (hi[0] - lo[0]) * (hi[2] - lo[2]) < 3000]
            A = (np.array([a[0] for a in antes]), np.array([a[1] for a in antes])) if antes else None
        por()
        if A is not None:
            for it in self.itens[n:]:
                lo, hi = self._caixa_de(it)
                if hi[1] - lo[1] <= 0.12: continue
                lo, hi = lo + encolhe, hi - encolhe
                if np.any(lo > hi): meio = (lo + hi) / 2; lo = hi = meio
                if np.any(np.all((A[0] < hi) & (A[1] > lo), axis=1)):
                    del self.itens[n:]; del self.barras[nb:]; return False
        return True

    def grupo(self, fim=False):
        """Para os laços que espalham enfeites: chamada no começo de cada volta (e com ``fim`` depois do laço), confere
        o que a volta anterior pôs como ``tentar`` e tira se trombou."""
        marca = getattr(self, '_grupo', None)
        if marca is not None:
            n, nb = marca; antes = [self._caixa_de(it) for it in self.itens[:n]]
            antes = [(lo, hi) for lo, hi in antes if hi[1] - lo[1] > 0.12 and (hi[0] - lo[0]) * (hi[2] - lo[2]) < 3000]
            if antes:
                A0, A1 = np.array([a[0] for a in antes]), np.array([a[1] for a in antes])
                for it in self.itens[n:]:
                    lo, hi = self._caixa_de(it)
                    if hi[1] - lo[1] <= 0.12: continue
                    lo, hi = lo + 0.1, hi - 0.1
                    if np.any(lo > hi): lo = hi = (lo + hi) / 2
                    if np.any(np.all((A0 < hi) & (A1 > lo), axis=1)):
                        del self.itens[n:]; del self.barras[nb:]; break
        self._grupo = None if fim else (len(self.itens), len(self.barras))

    def torre(self, cor, vidro, x0, z0, x1, z1, y0, y1, passo=3.6, faixa=2.0, colide='caixa'):
        """Um prédio fechado de (x0, z0) a (x1, z1), de y0 a y1: o maciço na chapa ``cor`` e uma faixa de janelas da
        chapa ``vidro`` por andar, 6 cm para fora das quatro faces."""
        self.chapa(cor, x0, z0, x1, z1, y=y0, alto=y1 - y0, colide=colide, planta=False)
        y = y0 + 0.9
        while y + faixa < y1 - 0.3:
            self.chapa(vidro, x0 - 0.06, z0 - 0.06, x1 + 0.06, z1 + 0.06, y=y, alto=faixa); y += passo

    def arredores(self, pecas, X, Z, folga=6.0, fileiras=2, passo=None, sal=0, y=0.0):
        """O bairro em volta do mapa (fora dele, sem colisão): ``fileiras`` de prédios das ``pecas`` em toda a volta, a
        fachada para o mapa, do mais baixo perto ao mais alto longe."""
        import zlib
        dist = folga
        for f in range(fileiras):
            fundo = max(self.kit[n].tam[2] for n in pecas); largo = passo or (sum(self.kit[n].tam[0] for n in pecas) / len(pecas) + 1.5)
            for lado, (ex, ez, yaw) in enumerate(((1, 0, 0.0), (0, 1, -90.0), (1, 0, 180.0), (0, 1, 90.0))):
                meio = (Z if ex else X) + dist + fundo / 2; comp = (X if ex else Z) + dist + fundo
                n = int(2 * comp / largo)
                for i in range(n):
                    t = -comp + (i + 0.5) * 2 * comp / n; h = zlib.crc32(repr((f, lado, i, sal)).encode())
                    nome = pecas[h % len(pecas)]; s = -1 if lado in (0, 3) else 1
                    x, z = (t, s * meio) if ex else (s * meio, t)
                    self.por(nome, x, z, yaw=yaw, y=y, escala=(1.0, 1.0 + 0.35 * f + (h >> 8) % 3 * 0.15, 1.0), colide=None, planta=False)
            dist += fundo + 5.0

    def rampa(self, it, pe, alto, lados, eixo=0, ate=None, grosso=0.3, folga=0.0):
        """Sobre os degraus da escada ``it`` (posta com ``colide='malha'``), uma chapa inclinada só de colisão, rente às
        quinas dos degraus: o corpo sobe e desce deslizando, na velocidade de andar, em vez de bater em cada espelho e
        ser posto em cima do degrau (o tranco que o Jhon viu, 20/11: "ele não sobe na mesma velocidade que anda, ele
        trava"). Medidas da peça, sem escala: ``pe`` = (s, y) onde a linha das quinas toca a base; ``alto`` = (s, y) da
        quina do último degrau; ``lados`` = (de, até) no outro eixo; ``eixo``: 0 se a escada sobe ao longo de x, 2 se de
        z. ``ate``: a altura (no mundo) do piso de cima — a chapa segue na mesma inclinação até lá. ``folga``: quanto a chapa
        fica acima da linha das quinas, em metros (a escada do Deserto, Stairs_01R, tem quinas que encostam na chapa: o
        corpo batia nelas e subia aos trancos, de 3 a 8 cm por quadro — o "tremor" de quatro na escada, 26/11; com 3 cm,
        5,7 cm em todo quadro)."""
        it.rampa = (pe, alto, lados, eixo, ate, grosso, folga); return it

    def _rampas(self):
        """As chapas das escadas, no mundo: (centro, base 3×3 com os eixos nas colunas, tamanho)."""
        out = []
        for it in self.itens:
            if not getattr(it, 'rampa', None): continue
            pe, alto, lados, eixo, ate, grosso, folga = it.rampa if len(it.rampa) == 7 else (*it.rampa, 0.0)
            def ponto(s_, y, l):
                q = np.zeros(3); q[eixo] = s_; q[1] = y; q[2 - eixo] = l; return it.pontos(q[None])[0]
            meio = (lados[0] + lados[1]) / 2; A, B = ponto(*pe, meio), ponto(*alto, meio)
            if ate is not None and abs(B[1] - A[1]) > 1e-6: B = A + (B - A) * (ate - A[1]) / (B[1] - A[1])
            u = (B - A) / np.linalg.norm(B - A); w = ponto(*pe, lados[1]) - ponto(*pe, lados[0]); largo = np.linalg.norm(w); w = w / largo
            n = np.cross(w, u)
            if n[1] < 0: n, w = -n, -w
            out.append(((A + B) / 2 - n * grosso / 2 + n * folga, np.column_stack([u, n, w]), (float(np.linalg.norm(B - A)), grosso, float(largo))))
        return out

    def fileira(self, nome, x0, z0, dx, dz, n, **k):
        return [self.por(nome, x0 + dx * i, z0 + dz * i, **k) for i in range(n)]

    def simetrico(self, de=0, ate=None):
        """Repete os itens [de:ate] (e as barras) girados 180° em volta do eixo Y do centro (o outro time vê o mesmo mapa)."""
        G = giro(180)
        for it in list(self.itens[de:ate]):
            self.itens.append(Item(it.peca, G @ it.G, it.D, G @ it.t, it.colide, it.planta, it.nome)); self.itens[-1].rampa = getattr(it, 'rampa', None)
        if de == 0 and ate is None:
            self.barras += [((-c[0], c[1], -c[2]), t, yaw + 180, cor) for c, t, yaw, cor in self.barras]

    def espelhar(self):
        """Repete tudo espelhado no plano x = 0 (o que está a oeste aparece a leste, o norte continua norte). A peça em si não
        é espelhada (o letreiro continua legível): entra a mesma, na posição e no giro espelhados."""
        F = np.diag([-1.0, 1, 1])
        for it in list(self.itens):
            # o meio da caixa da peça vai para o ponto espelhado; o giro troca de sinal
            G = F @ it.G @ F; R = G @ np.diag(it.D); meio = it.peca.lo + it.peca.tam / 2; alvo = F @ it.pontos(meio)
            self.itens.append(Item(it.peca, G, it.D, alvo - R @ meio, it.colide, it.planta, it.nome)); self.itens[-1].rampa = getattr(it, 'rampa', None)
        self.barras += [((-c[0], c[1], c[2]), t, -yaw, cor) for c, t, yaw, cor in self.barras]

    def barra(self, x, z, largura=0.4, altura=3.0, fundo=None, yaw=0.0, y=0.0, cor=(0.35, 0.3, 0.25)):
        """Uma caixa só de colisão (o tronco de uma árvore, um poste): o desenho é de uma peça posta com ``colide=None``."""
        self.barras.append(((x, y + altura / 2, z), (largura, altura, fundo or largura), yaw, cor))

    def cortar(self, lo, hi):
        """Abre um vão: os triângulos (do desenho e da colisão) inteiros dentro desta caixa do mundo somem."""
        self.cortes.append((np.array(lo, float), np.array(hi, float)))

    # ------------------------------------------------------------ geometria
    def _fora_dos_cortes(self, P):
        fica = np.ones(len(P), bool)
        for lo, hi in self.cortes:
            fica &= ~((P >= lo).all((1, 2)) & (P <= hi).all((1, 2)))
        return fica

    def colisao(self, so_malha=False):
        """Os triângulos de colisão, no mundo (T×3×3)."""
        out = []
        for it in self.itens:
            if it.colide == 'malha':
                P = it.pontos(it.peca.pos.reshape(-1, 3)).reshape(-1, 3, 3); out.append(P[self._fora_dos_cortes(P)])
            elif it.colide == 'caixa' and not so_malha:
                c = it.cantos(); out.append(np.array([[c[a], c[b], c[d]] for a, b, d in CAIXA_TRI]))
        if not so_malha:
            for centro, tam, yaw, _ in self.barras:
                c = (CAIXA - 0.5) * np.array(tam) @ giro(yaw).T + np.array(centro); out.append(np.array([[c[a], c[b], c[d]] for a, b, d in CAIXA_TRI]))
        return np.concatenate(out) if out else np.zeros((0, 3, 3))

    def cor_dos_lados(self, nome):
        """A cor que mais aparece nas faces em pé de uma peça (a do reboco de uma parede), para a chapa combinar."""
        from PIL import Image
        if not hasattr(self, '_tex'): self._tex = np.asarray(Image.open(self.kit.textura).convert('RGB'), float) / 255
        p = self.kit[nome]; H, W = self._tex.shape[:2]; em_pe = np.abs(p.normal.mean(1)[:, 1]) < 0.3
        area = np.linalg.norm(np.cross(p.pos[:, 1] - p.pos[:, 0], p.pos[:, 2] - p.pos[:, 0]), axis=1) * em_pe; uv = p.uv.mean(1)
        c = self._tex[np.clip((uv[:, 1] % 1 * H).astype(int), 0, H - 1), np.clip((uv[:, 0] % 1 * W).astype(int), 0, W - 1)]
        chave = (c * 32).astype(int); soma = {}
        for k, a_, cor_ in zip(map(tuple, chave), area, c):
            s_ = soma.setdefault(k, [0.0, np.zeros(3)]); s_[0] += a_; s_[1] += cor_ * a_
        a_, cor_ = max(soma.values(), key=lambda v: v[0]); return tuple(float(v) for v in cor_ / max(a_, 1e-9))

    def cor(self, peca):
        """A cor média da peça (para o minimapa), tirada da textura."""
        if peca.nome not in self._cor:
            from PIL import Image
            if not hasattr(self, '_tex'): self._tex = np.asarray(Image.open(self.kit.textura).convert('RGB'), float) / 255
            H, W = self._tex.shape[:2]; uv = peca.uv.mean(1); cima = peca.normal.mean(1)[:, 1] > 0.5
            if cima.sum() >= 2: uv = uv[cima]
            c = self._tex[np.clip((uv[:, 1] % 1 * H).astype(int), 0, H - 1), np.clip((uv[:, 0] % 1 * W).astype(int), 0, W - 1)].mean(0)
            self._cor[peca.nome] = tuple(round(float(v), 3) for v in c)
        return self._cor[peca.nome]

    # ------------------------------------------------------------ o .glb
    def glb(self, caminho):
        usados = []
        for it in self.itens:
            if it.peca.nome not in usados: usados.append(it.peca.nome)
        bin_ = bytearray(); views, accs, meshes = [], [], []

        def acc(dados, tipo, comp, alvo, minmax=False):
            while len(bin_) % 4: bin_.append(0)
            views.append(dict(buffer=0, byteOffset=len(bin_), byteLength=dados.nbytes, target=alvo)); bin_.extend(dados.tobytes())
            a = dict(bufferView=len(views) - 1, componentType=comp, count=len(dados), type=tipo)
            if minmax: a['min'] = dados.min(0).tolist(); a['max'] = dados.max(0).tolist()
            accs.append(a); return len(accs) - 1

        cortado = {}          # a peça com vão aberto é uma malha própria (por item)
        def malha(p, P=None, nome=None):
            pos = (p.pos if P is None else P).reshape(-1, 3); n = p.normal.reshape(-1, 3) if P is None else nome[1].reshape(-1, 3)
            uv = p.uv.reshape(-1, 2) if P is None else nome[2].reshape(-1, 2)
            v = np.concatenate([pos, n, uv], 1).astype(np.float32)
            _, primeiro, inv = np.unique(v.round(5), axis=0, return_index=True, return_inverse=True); v = v[primeiro]
            idx = inv.astype(np.uint32)
            prim = dict(attributes=dict(POSITION=acc(v[:, 0:3].copy(), 'VEC3', 5126, 34962, True), NORMAL=acc(v[:, 3:6].copy(), 'VEC3', 5126, 34962),
                                        TEXCOORD_0=acc(v[:, 6:8].copy(), 'VEC2', 5126, 34962)), indices=acc(idx, 'SCALAR', 5125, 34963), material=0, mode=4)
            meshes.append(dict(name=p.nome if P is None else nome[0], primitives=[prim])); return len(meshes) - 1

        de_peca = {}; esticadas = {}
        nos = []
        for k, it in enumerate(self.itens):
            pos, nor, uv = it.peca.pos, it.peca.normal, it.peca.uv; D = it.D; inteira = np.allclose(D, 1.0, atol=1e-4); chave = None
            if not inteira:
                # a escala entra na malha (e a espelhada troca a ordem dos vértices: a face continua de frente)
                pos = pos * D; nor = nor / D; nor = nor / np.maximum(np.linalg.norm(nor, axis=-1, keepdims=True), 1e-9)
                if np.prod(D) < 0: pos, nor, uv = pos[:, ::-1], nor[:, ::-1], uv[:, ::-1]
                chave = (it.peca.nome, tuple(np.round(D, 4)))
            fica = None
            if self.cortes:
                P = it.pontos(it.peca.pos.reshape(-1, 3)).reshape(-1, 3, 3); fica = self._fora_dos_cortes(P)
                if fica.all(): fica = None
                elif np.prod(D) < 0: fica = fica            # a ordem dos triângulos é a mesma
            if fica is not None:
                m = malha(it.peca, pos[fica], (f'{it.peca.nome}_aberta{k}', nor[fica], uv[fica]))
            elif inteira:
                if it.peca.nome not in de_peca: de_peca[it.peca.nome] = malha(it.peca)
                m = de_peca[it.peca.nome]
            else:
                if chave not in esticadas: esticadas[chave] = malha(it.peca, pos, (f'{it.peca.nome}_e{len(esticadas)}', nor, uv))
                m = esticadas[chave]
            M = np.eye(4); M[:3, :3] = it.G; M[:3, 3] = it.t
            nome = f"{it.peca.nome}_{k}" + ('-col' if it.colide == 'malha' else '')
            nos.append(dict(name=nome, mesh=m, matrix=[float(x) for x in M.T.reshape(-1)]))
        png = open(self.kit.textura, 'rb').read()
        while len(bin_) % 4: bin_.append(0)
        views.append(dict(buffer=0, byteOffset=len(bin_), byteLength=len(png))); bin_.extend(png)
        j = dict(asset=dict(version='2.0', generator='tools/mapas/kits/montagem.py'), scene=0, scenes=[dict(nodes=list(range(len(nos))))], nodes=nos,
                 meshes=meshes, accessors=accs, bufferViews=views, buffers=[dict(byteLength=len(bin_))],
                 images=[dict(bufferView=len(views) - 1, mimeType='image/png', name='kit_' + self.kit.nome)],
                 samplers=[dict(magFilter=9729, minFilter=9987, wrapS=10497, wrapT=10497)], textures=[dict(sampler=0, source=0)],
                 materials=[dict(name='kit_' + self.kit.nome, doubleSided=True,
                                 pbrMetallicRoughness=dict(baseColorTexture=dict(index=0), metallicFactor=0.0, roughnessFactor=0.9))])
        texto = json.dumps(j, separators=(',', ':')).encode()
        while len(texto) % 4: texto += b' '
        while len(bin_) % 4: bin_.append(0)
        with open(caminho, 'wb') as f:
            f.write(struct.pack('<4sII', b'glTF', 2, 12 + 8 + len(texto) + 8 + len(bin_)))
            f.write(struct.pack('<I4s', len(texto), b'JSON')); f.write(texto)
            f.write(struct.pack('<I4s', len(bin_), b'BIN\x00')); f.write(bytes(bin_))
        # o material de cada face (asfalto, vidro, madeira…) e o material de detalhe do kit: tools/mapas/kits/detalhe.py
        from . import detalhe
        detalhe.detalhar(caminho, self.kit.nome, quieto=True)
        return len(nos), len(meshes), os.path.getsize(caminho)

    # ------------------------------------------------------------ para o kit.Mapa
    def aplicar(self, m, grupo, arquivo, mapa_min=0.4, no_planta='Planta', modelo='Desenho'):
        """Grava o .glb e põe no ``Mapa``: o modelo, as caixas de colisão e a planta do minimapa."""
        destino = os.path.join(RAIZ, 'assets', 'mapas', 'pecas', arquivo + '.glb')
        nos, pecas, tamanho = self.glb(destino)
        m.modelo(grupo, modelo, arquivo, (0, 0, 0))
        if 'kit_colisao' not in m.materiais: m.material('kit_colisao', None, (0.5, 0.5, 0.5))
        caixas = {}; cores = {}; linhas = [f'[node name="{no_planta}" type="Node3D" parent="."]', '']; k = 0
        from .. import kit as K
        for i, it in enumerate(self.itens):
            if not it.colide: continue
            esc = np.linalg.norm(it.R, axis=0); tam = it.peca.tam * esc; centro = it.pontos(it.peca.lo + it.peca.tam / 2)
            esc = np.abs(it.D); tam = it.peca.tam * esc; base = tuple(tuple(float(v) for v in linha) for linha in it.G)
            if it.colide == 'caixa':
                m.caixa('Colisao', f'{no_planta[0].lower()}c{i}_{it.peca.nome}', tuple(float(v) for v in centro), tuple(float(max(v, 0.02)) for v in tam), 'kit_colisao',
                        rot=base, oculta=True, cor=self.cor(it.peca), minimapa=bool(it.planta))
            elif it.planta and tam[0] * tam[2] >= mapa_min and tam[0] * tam[2] < 1200:
                chave = tuple(round(float(v), 2) for v in tam)
                if chave not in caixas:
                    caixas[chave] = f'KM{len(caixas) + 1}'
                    m.cru_recursos += [f'[sub_resource type="BoxMesh" id="{caixas[chave]}"]', f'size = {K.vec(chave)}', '']
                c = self.cor(it.peca)
                if c not in cores:
                    cores[c] = f'KC{len(cores) + 1}'
                    m.cru_recursos += [f'[sub_resource type="StandardMaterial3D" id="{cores[c]}"]', f'albedo_color = {K.cor(c)}', '']
                k += 1
                linhas += [f'[node name="p{k}" type="MeshInstance3D" parent="{no_planta}"]', f'transform = {K.xform(tuple(float(v) for v in centro), base)}',
                           f'mesh = SubResource("{caixas[chave]}")', f'material_override = SubResource("{cores[c]}")', 'visible = false', '']
        for i, (centro, tam, yaw, cor_) in enumerate(self.barras):
            m.caixa('Colisao', f'{no_planta[0].lower()}b{i}', tuple(float(v) for v in centro), tuple(float(v) for v in tam), 'kit_colisao', rot=K.giro_y(yaw), oculta=True, cor=cor_)
        for i, (centro, base_, tam) in enumerate(self._rampas()):
            m.caixa('Colisao', f'{no_planta[0].lower()}r{i}', tuple(float(v) for v in centro), tam, 'kit_colisao',
                    rot=tuple(tuple(float(v) for v in linha) for linha in base_), oculta=True, minimapa=False)
        m.cru_nos += linhas
        # a miniatura do mapa sai daqui (a do kit só conhece as caixas); a do primeiro desenho, se houver dois
        if no_planta == 'Planta': m.miniatura = lambda caminho, tamanho=(512, 288): self.planta(caminho, (-m.largura / 2, -m.fundo / 2), (m.largura / 2, m.fundo / 2), tamanho)
        print(f"  {arquivo}.glb: {nos} peças postas ({pecas} diferentes), {tamanho / 1e6:.1f} MB; {k} caixas de planta")

    def planta(self, caminho, lo, hi, tamanho=(512, 288), fundo=(46, 50, 58)):
        """A miniatura do mapa vista de cima (a de reserva da escolha de mapa): a pegada de cada peça, na cor dela, a mais
        alta por cima — como o minimapa do jogo desenha."""
        from PIL import Image, ImageDraw
        W, H = tamanho; e = min(W / (hi[0] - lo[0]), H / (hi[1] - lo[1])) * 0.94
        ox = W / 2 - (lo[0] + hi[0]) / 2 * e; oz = H / 2 - (lo[1] + hi[1]) / 2 * e
        im = Image.new('RGB', (W * 2, H * 2), fundo); d = ImageDraw.Draw(im); pecas = []
        for it in self.itens:
            if not it.planta: continue
            c = it.cantos(); topo = c[:, 1].max(); tam = it.peca.tam * np.abs(it.D)
            if tam[0] * tam[2] > 1200 or topo > 60: continue
            q = c[[0, 1, 5, 4]] if True else c            # os quatro cantos de baixo, em volta
            cor_ = self.cor(it.peca); claro = 0.75 + 0.25 * min(topo / 10.0, 1.0)
            pecas.append((topo, [((p[0] * e + ox) * 2, (p[2] * e + oz) * 2) for p in q], tuple(int(255 * v * claro) for v in cor_)))
        for _, pol, cor_ in sorted(pecas, key=lambda p: p[0]):
            d.polygon(pol, fill=cor_, outline=tuple(int(v * 0.6) for v in cor_))
        im.resize((W, H), Image.LANCZOS).save(caminho)

    # ------------------------------------------------------------ onde se anda
    def ocupacao(self, lo, hi, cel=0.25, faixa=(0.45, 1.9), piso=None):
        """Grade vista de cima (True = ocupado) entre ``lo`` e ``hi`` (x, z): há colisão na faixa de altura acima do piso.
        ``piso``: função (x, z) -> y do piso ali (padrão 0)."""
        W = int(round((hi[0] - lo[0]) / cel)); H = int(round((hi[1] - lo[1]) / cel)); g = np.zeros((H, W), bool)
        T = self.colisao()
        if not len(T): return g
        y0, y1 = faixa
        sel = (T[:, :, 1].max(1) > y0 - 2) & (T[:, :, 1].min(1) < y1 + 12)
        for t in T[sel]:
            lado = max(np.linalg.norm(t[1] - t[0]), np.linalg.norm(t[2] - t[0]), np.linalg.norm(t[2] - t[1]))
            n = int(lado / (cel * 0.6)) + 2
            a, b = np.meshgrid(np.linspace(0, 1, n), np.linspace(0, 1, n)); mk = a + b <= 1.0001; a, b = a[mk], b[mk]
            pts = t[0] + a[:, None] * (t[1] - t[0]) + b[:, None] * (t[2] - t[0])
            base = 0.0 if piso is None else piso(pts[:, 0], pts[:, 2])
            pts = pts[(pts[:, 1] > base + y0) & (pts[:, 1] < base + y1)]
            if not len(pts): continue
            ix = ((pts[:, 0] - lo[0]) / cel).astype(int); iz = ((pts[:, 2] - lo[1]) / cel).astype(int)
            ok = (ix >= 0) & (ix < W) & (iz >= 0) & (iz < H); g[iz[ok], ix[ok]] = True
        # a caixa é maciça: o miolo dela também é ocupado (senão um ponto de rota cai "livre" dentro do chafariz)
        from PIL import Image, ImageDraw
        cheio = Image.new('1', (W, H), 0); d = ImageDraw.Draw(cheio)
        caixas = [it.cantos() for it in self.itens if it.colide == 'caixa']
        caixas += [(CAIXA - 0.5) * np.array(tam) @ giro(yaw).T + np.array(centro) for centro, tam, yaw, _ in self.barras]
        for c in caixas:
            base = 0.0 if piso is None else float(np.mean(piso(c[:, 0], c[:, 2])))
            if c[:, 1].min() < base + y1 and c[:, 1].max() > base + y0:
                d.polygon([((p[0] - lo[0]) / cel, (p[2] - lo[1]) / cel) for p in c[[0, 1, 5, 4]]], fill=1)
        return g | np.asarray(cheio, bool)


class Grade:
    """A ocupação com folga, para pontos e caminhos."""

    def __init__(self, ocupado, lo, cel, folga=0.5):
        self.lo, self.cel = np.array(lo, float), cel
        r = int(math.ceil(folga / cel)); g = ocupado.copy(); H, W = g.shape
        yy, xx = np.mgrid[-r:r + 1, -r:r + 1]; disco = (yy * yy + xx * xx) <= r * r
        cheio = np.zeros_like(g)
        for dy, dx in zip(yy[disco], xx[disco]):
            a = np.zeros_like(g)
            ys = slice(max(dy, 0), H + min(dy, 0)); xs = slice(max(dx, 0), W + min(dx, 0))
            yo = slice(max(-dy, 0), H + min(-dy, 0)); xo = slice(max(-dx, 0), W + min(-dx, 0))
            a[ys, xs] = g[yo, xo]; cheio |= a
        self.livre = ~cheio; self.cru = ocupado

    def celula(self, x, z):
        return int((z - self.lo[1]) / self.cel), int((x - self.lo[0]) / self.cel)

    def ponto(self, i, j):
        return (self.lo[0] + (j + 0.5) * self.cel, self.lo[1] + (i + 0.5) * self.cel)

    def anda(self, x, z):
        i, j = self.celula(x, z); H, W = self.livre.shape
        return 0 <= i < H and 0 <= j < W and bool(self.livre[i, j])

    def perto(self, x, z, raio=3.0):
        """O ponto livre mais perto de (x, z)."""
        if self.anda(x, z): return (x, z)
        i0, j0 = self.celula(x, z); r = int(raio / self.cel); H, W = self.livre.shape; melhor = None
        for i in range(max(i0 - r, 0), min(i0 + r + 1, H)):
            for j in range(max(j0 - r, 0), min(j0 + r + 1, W)):
                if self.livre[i, j]:
                    d = (i - i0) ** 2 + (j - j0) ** 2
                    if melhor is None or d < melhor[0]: melhor = (d, i, j)
        if melhor is None: raise ValueError(f'sem chão livre perto de ({x:.1f}, {z:.1f})')
        return self.ponto(melhor[1], melhor[2])

    def caminho(self, a, b):
        """A* de ``a`` a ``b`` (x, z), em 8 direções; devolve os pontos (a cada célula) ou None."""
        a = self.celula(*self.perto(*a)); b = self.celula(*self.perto(*b)); H, W = self.livre.shape
        aberto = [(0.0, a)]; veio = {a: None}; custo = {a: 0.0}
        viz = [(-1, 0, 1), (1, 0, 1), (0, -1, 1), (0, 1, 1), (-1, -1, 1.4142), (-1, 1, 1.4142), (1, -1, 1.4142), (1, 1, 1.4142)]
        while aberto:
            _, c = heapq.heappop(aberto)
            if c == b: break
            for di, dj, w in viz:
                n = (c[0] + di, c[1] + dj)
                if not (0 <= n[0] < H and 0 <= n[1] < W) or not self.livre[n]: continue
                if di and dj and not (self.livre[c[0] + di, c[1]] and self.livre[c[0], c[1] + dj]): continue
                g = custo[c] + w
                if g < custo.get(n, 1e18):
                    custo[n] = g; veio[n] = c
                    heapq.heappush(aberto, (g + math.hypot(n[0] - b[0], n[1] - b[1]), n))
        if b not in veio: return None
        out = []; c = b
        while c is not None: out.append(self.ponto(*c)); c = veio[c]
        return out[::-1]

    def rota(self, pontos, passo=7.0):
        """Uma rota de ronda passando pelos ``pontos`` (x, z): o caminho andável entre eles, com um ponto a cada ``passo`` m."""
        tudo = []
        for a, b in zip(pontos, pontos[1:]):
            c = self.caminho(a, b)
            if c is None: raise ValueError(f'sem caminho de {a} a {b}')
            tudo += c if not tudo else c[1:]
        out = [tudo[0]]; andou = 0.0
        for p, q in zip(tudo, tudo[1:]):
            andou += math.hypot(q[0] - p[0], q[1] - p[1])
            if andou >= passo: out.append(q); andou = 0.0
        if out[-1] != tudo[-1]: out.append(tudo[-1])
        return [(round(x, 1), round(z, 1)) for x, z in out]

    def imagem(self, caminho, marcas=(), rotas=(), escala=4):
        from PIL import Image, ImageDraw
        img = np.zeros((*self.livre.shape, 3), np.uint8); img[self.livre] = (235, 235, 235); img[~self.livre & ~self.cru] = (150, 150, 160); img[self.cru] = (20, 20, 20)
        im = Image.fromarray(img).resize((img.shape[1] * escala, img.shape[0] * escala), Image.NEAREST); d = ImageDraw.Draw(im)
        def px(x, z): return ((x - self.lo[0]) / self.cel * escala, (z - self.lo[1]) / self.cel * escala)
        for k, r in enumerate(rotas):
            c = [(230, 60, 60), (60, 160, 60), (60, 90, 230), (220, 150, 30), (160, 60, 200), (30, 170, 180), (120, 120, 40)][k % 7]
            d.line([px(*p) for p in r], fill=c, width=3)
            for p in r: x, y = px(*p); d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=c)
        for x, z, c in marcas: X, Y = px(x, z); d.rectangle([X - 5, Y - 5, X + 5, Y + 5], fill=c)
        im.save(caminho)


# ---------------------------------------------------------------- o que todo mapa de kit tem
def fechar(m, x, z, altura=22.0, margem=8.0, piso=0.0, paredes=True):
    """O chão e as paredes invisíveis de um mapa de −x a x e de −z a z (``MapBounds``: os corpos ``Limite…`` e ``Piso``).
    ``piso``: a altura do chão (abaixo de zero quando a rua é rebaixada e a calçada tem a colisão dela — a Cidade).
    ``margem``: quanto o chão passa da borda (negativa: o chão acaba antes dela). ``paredes=False``: a beira é de
    verdade e quem passa dela cai (a Obra, 27/11) — as ``Limite…`` vão assim mesmo, mas sem colidir com nada: só dizem
    à câmera do espectador até onde ela vai (Jhon, 27/11: "o espectador não pode sair dos limites do mapa")."""
    if 'kit_colisao' not in m.materiais: m.material('kit_colisao', None, (0.5, 0.5, 0.5))
    m.caixa('Colisao', 'Piso', (0, piso - 0.5, 0), (2 * x + 2 * margem, 1, 2 * z + 2 * margem), 'kit_colisao', oculta=True)
    for nome, c, t in (('LimiteOeste', (-x - 0.5, altura / 2, 0), (1, altura, 2 * z + 2)), ('LimiteLeste', (x + 0.5, altura / 2, 0), (1, altura, 2 * z + 2)),
                       ('LimiteSul', (0, altura / 2, -z - 0.5), (2 * x + 2, altura, 1)), ('LimiteNorte', (0, altura / 2, z + 0.5), (2 * x + 2, altura, 1)),
                       ('LimiteTeto', (0, altura + 0.5, 0), (2 * x + 2, 1, 2 * z + 2))):
        if paredes: m.caixa('Colisao', nome, c, t, 'kit_colisao', oculta=True)
        else: m.caixa('Colisao', nome, c, t, 'kit_colisao', oculta=True, minimapa=False, so_espectador=True)


def barrar(m, P, letras, altura):
    """O lobo corre na parede e, de salto em salto, chega ao alto dela: em cima do maciço que fecha o mapa (as ``letras`` que
    encostam na borda da planta) vai uma caixa invisível do chão ao teto — ninguém sobe ali nem passa para fora (pedido do
    Jhon, 20/11). Não se chamam ``Limite…``: a caixa do mapa (``MapBounds``) continua a ser a das quatro paredes de fora.
    O lobo não corre nelas (``Player.IsInvisibleLimit``: os corpos ``Limite…`` e ``Barreira…``)."""
    if 'kit_colisao' not in m.materiais: m.material('kit_colisao', None, (0.5, 0.5, 0.5))
    caixas = P.borda(letras); dentro = {(r, c) for c0, r0, c1, r1 in caixas for r in range(r0, r1) for c in range(c0, c1)}
    livre = lambda cels: all(0 <= r < P.R and 0 <= c < P.C and (r, c) not in dentro for r, c in cels)
    for i, (c0, r0, c1, r1) in enumerate(caixas):
        x0, z0, x1, z1 = P.mundo(c0, r0, c1, r1)
        # 6 cm para dentro da face que dá para o chão: embaixo, quem o lobo toca é o muro (a barreira não pode ser a
        # "parede da frente" de uma quina — ela solta a corrida); em cima, os 6 cm de beirada não seguram ninguém.
        # Entre duas caixas de barreira, nada (senão fica uma fresta)
        if livre([(r, c0 - 1) for r in range(r0, r1)]): x0 += 0.06
        if livre([(r, c1) for r in range(r0, r1)]): x1 -= 0.06
        if livre([(r0 - 1, c) for c in range(c0, c1)]): z0 += 0.06
        if livre([(r1, c) for c in range(c0, c1)]): z1 -= 0.06
        m.caixa('Colisao', f'Barreira{i + 1}', ((x0 + x1) / 2, altura / 2, (z0 + z1) / 2), (x1 - x0, altura, z1 - z0), 'kit_colisao', oculta=True, minimapa=False)


def nascer(m, grade, centro, time, passo=1.6, quantos=6, y=1.2, espelho=False):
    """``quantos`` pontos de nascimento do ``time`` em volta de ``centro`` (x, z), em chão livre; o outro time nasce no ponto
    oposto do mapa (girado 180°; com ``espelho``, espelhado em x = 0). Devolve os pontos."""
    sz = 1 if espelho else -1
    cx, cz = centro; cand = sorted(((i * passo, j * passo) for i in range(-4, 5) for j in range(-4, 5)), key=lambda d: d[0] ** 2 + d[1] ** 2)
    pontos = []
    for dx, dz in cand:
        x, z = cx + dx, cz + dz
        if grade.anda(x, z) and grade.anda(-x, sz * z): pontos.append((round(x, 2), round(z, 2)))
        if len(pontos) == quantos: break
    if len(pontos) < quantos: raise ValueError(f'só {len(pontos)} pontos livres em volta de {centro}')
    for x, z in pontos:
        m.spawn(time, x, z, y); m.spawn(3 - time, -x, sz * z, y)
    return pontos


def espalhar(m, grade, base, quantos=16, y=1.2, passo=2.0, folga=1.0):
    """Os pontos de nascimento do mata-mata (todos contra todos): ``quantos`` pontos de chão livre espalhados pelo mapa
    inteiro, o mais longe possível uns dos outros, todos alcançáveis a pé a partir de ``base`` (x, z). Ficam no nó
    ``NascerLivre`` da cena (``Marker3D`` ``livre1``…), que não é um ``SpawnPoints``: a partida em equipe não os vê —
    quem os usa é o modo mata-mata (pedido do Jhon, 20/11: "nascendo em pontos aleatórios dentro do mapa")."""
    L = grade.livre; H, W = L.shape; a = grade.celula(*grade.perto(*base))
    visto = np.zeros_like(L); visto[a] = True; fila = [a]
    while fila:
        i, j = fila.pop()
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (i + di, j + dj)
            if 0 <= n[0] < H and 0 <= n[1] < W and L[n] and not visto[n]: visto[n] = True; fila.append(n)
    r = int(round(folga / grade.cel)); k = max(1, int(round(passo / grade.cel))); cand = []
    for i in range(r, H - r, k):
        for j in range(r, W - r, k):
            if visto[i - r:i + r + 1, j - r:j + r + 1].all(): cand.append(grade.ponto(i, j))
    if len(cand) < quantos: raise ValueError(f'só {len(cand)} lugares livres para espalhar {quantos} pontos')
    C = np.array(cand); meio = C.mean(0); pontos = [int(np.argmax(((C - meio) ** 2).sum(1)))]
    d = ((C - C[pontos[0]]) ** 2).sum(1)
    while len(pontos) < quantos:
        p = int(np.argmax(d)); pontos.append(p); d = np.minimum(d, ((C - C[p]) ** 2).sum(1))
    from .. import kit as K
    m.cru_nos += ['[node name="NascerLivre" type="Node3D" parent="."]', '']
    out = [(round(float(C[p][0]), 2), round(float(C[p][1]), 2)) for p in pontos]
    for i, (x, z) in enumerate(out, 1):
        m.cru_nos += [f'[node name="livre{i}" type="Marker3D" parent="NascerLivre"]', f'transform = {K.xform((x, y, z))}', '']
    m.livres = out
    return out


# ---- a luz dos mapas de kit (pedido do Jhon, 20/11: "quero iluminação padrão AAA de grandes jogos, Overwatch, Valorant";
# "teste becos, locais mais escuros, tanto do mapa quanto da câmera, primeira e terceira pessoa")
# O que esses jogos têm em comum, e como cada coisa é feita aqui:
#   - um sol quente e forte e a sombra dele FRIA e clara, nunca preta nem cinza: o ambiente é azul-violeta, forte, e não
#     depende de o céu "enxergar" o lugar — o beco, a sala e o vão debaixo da laje ficam na mesma sombra legível
#     (é luz de jogo competitivo: o inimigo no escuro se vê);
#   - a luz que bate no chão ao sol e VOLTA quente, por baixo: uma luz direcional fraca, sem sombra, apontada para
#     cima na direção do sol (``preenchimento``) — acende o teto do vão, o queixo e o lado de baixo das coisas;
#   - contato: oclusão larga (SSAO) e a cor que vaza de uma face para a vizinha (SSIL);
#   - cor: ACES com o branco alto (o claro não estoura), saturação e contraste um pouco acima do neutro;
#   - ar: névoa leve que puxa o longe para a cor do céu e abre um clarão em volta do sol; brilho só no que é claro;
#   - sombra macia: o sol tem tamanho, a sombra abre com a distância.
# Sem luz rebatida de verdade (SDFGI): testada em foto Forward+ em 20/11, ela apagava tudo o que o céu não vê — a sala
# sem lâmpada ficava preta e a fachada na sombra quase — e custa caro em mapas de 100 m. O Armazém, fechado, usa.
# Os valores saíram de fotos Forward+ (o mapa com personagens parados no sol, no beco e debaixo de laje).
CLIMAS = {
    'dia':     dict(sol=(1.0, 0.90, 0.72), energia=2.8, sombra=(0.74, 0.80, 1.0), volta=(1.0, 0.86, 0.68), ar=(0.74, 0.84, 1.0)),
    'tarde':   dict(sol=(1.0, 0.72, 0.44), energia=2.6, sombra=(0.80, 0.74, 1.0), volta=(1.0, 0.72, 0.50), ar=(1.0, 0.80, 0.64)),
    'deserto': dict(sol=(1.0, 0.88, 0.66), energia=3.0, sombra=(0.82, 0.81, 0.98), volta=(1.0, 0.82, 0.58), ar=(0.98, 0.89, 0.74)),
}


# O céu calculado de cada clima (``assets/mapas/ceus/ceu_nuvens.gdshader``; os mapas de 27/11): as cores do ar e a
# camada de nuvens. ``dia``: cúmulos brancos num azul fundo; ``deserto``: o horizonte claro de poeira e nuvens altas,
# ralas e esfiapadas; ``tarde``: o sol baixo acende as nuvens por baixo, de laranja, contra um céu que escurece.
CEUS = {
    # (as cores do alto são mais fundas do que se quer ver: o mapeamento de cor ACES clareia e tira cor do azul)
    'dia':     dict(topo=(0.04, 0.17, 0.60), horizonte=(0.56, 0.74, 0.95), chao=(0.40, 0.42, 0.45), curva=0.55, clarao=0.4,
                    nuvens=dict(cobertura=0.46, maciez=0.20, densidade=1.7, escala=0.10, espessura=0.11, vento=(0.006, 0.0025),
                                nuvem_luz=(1.0, 0.98, 0.95), nuvem_sombra=(0.50, 0.58, 0.74), contra_luz=1.3)),
    # (o horizonte do deserto é claro e quase sem cor: o amarelo da poeira misturado ao azul do alto dava lilás)
    'deserto': dict(topo=(0.04, 0.22, 0.62), horizonte=(0.80, 0.85, 0.88), chao=(0.62, 0.52, 0.38), curva=0.5, clarao=0.55,
                    nuvens=dict(cobertura=0.30, maciez=0.30, densidade=1.2, escala=0.07, espessura=0.06, vento=(0.004, 0.0015),
                                nuvem_luz=(1.0, 0.97, 0.90), nuvem_sombra=(0.72, 0.70, 0.72), contra_luz=1.6)),
    'tarde':   dict(topo=(0.07, 0.12, 0.40), horizonte=(1.0, 0.60, 0.34), chao=(0.34, 0.30, 0.30), curva=0.6, clarao=0.8, disco=10.0,
                    nuvens=dict(cobertura=0.52, maciez=0.24, densidade=1.6, escala=0.09, espessura=0.13, vento=(0.005, 0.002),
                                nuvem_luz=(1.0, 0.84, 0.62), nuvem_sombra=(0.36, 0.30, 0.48), contra_luz=2.2)),
}


def luz_de_jogo(m, clima, yaw, queda, giro_ceu=74.0, alcance=170.0, nuvens=False, **mais):
    """A luz de um mapa de kit ao ar livre: ``clima`` é uma linha de ``CLIMAS``; ``yaw``/``queda`` o sol. ``mais``
    troca qualquer valor do ambiente (``exposicao=…``, ``neblina=…``). ``nuvens``: o céu calculado do clima
    (``CEUS``), com nuvens que andam, no lugar da fotografia parada."""
    c = CLIMAS[clima]
    m.ambiente = dict(ceu=dict(CEUS[clima], energia=1.0, ambiente=0.25) if nuvens else dict(hdri="dia_claro", giro=giro_ceu, energia=1.0, ambiente=0.25),
                      luz=c['sombra'], luz_energia=0.8, exposicao=0.92, branco=6.5,
                      brilho=0.7, brilho_bloom=0.12, brilho_limiar=1.0,
                      ssao=2.4, ssao_raio=1.6, ssao_potencia=1.5, ssao_luz=0.35, ssil=1.0,
                      contraste=1.06, saturacao=1.2,
                      neblina=dict(cor=c['ar'], densidade=0.0018, ceu=0.15, aerea=0.55, sol=0.25))
    # (o céu calculado já tem o ar do horizonte dele: a névoa do mapa não entra no céu — com ela o azul virava lilás)
    if nuvens: m.ambiente['neblina'] = dict(m.ambiente['neblina'], ceu=0.0)
    m.ambiente.update(mais)
    m.sol = dict(yaw=yaw, queda=queda, cor=c['sol'], energia=c['energia'], alcance=alcance, tamanho=0.8)
    m.preenchimento = dict(yaw=yaw, queda=-38, cor=c['volta'], energia=0.6)
    m.borrao_sombra = 1.2


def lampadas(m, montagens, padrao=r'^(Light|Lamp|Lantern|StreetLight)', cor=(1.0, 0.82, 0.58), energia=1.8, alcance=7.5, maximo=28, grupo='Luzes'):
    """Uma lâmpada quente em cada poste e lampião do kit (as peças cujo nome casa com ``padrao``), até ``maximo``, as
    mais espalhadas. De dia quase não se nota no sol; na sombra do beco e debaixo da marquise faz a poça de luz quente
    que os mapas de referência têm. Sem sombra (barata)."""
    import re as _re
    pts = []
    for M in montagens:
        for it in M.itens:
            if _re.match(padrao, it.peca.nome):
                c = it.cantos(); pts.append(((c[:, 0].min() + c[:, 0].max()) / 2, c[:, 1].max() - 0.35, (c[:, 2].min() + c[:, 2].max()) / 2))
    pts = sorted(set((round(x, 1), round(y, 1), round(z, 1)) for x, y, z in pts))
    if len(pts) > maximo:
        P_ = np.array(pts); esc = [0]; d = ((P_ - P_[0]) ** 2).sum(1)
        while len(esc) < maximo:
            k = int(np.argmax(d)); esc.append(k); d = np.minimum(d, ((P_ - P_[k]) ** 2).sum(1))
        pts = [pts[k] for k in sorted(esc)]
    for i, (x, y, z) in enumerate(pts, 1): m.luz(grupo, f'Poste{i}', (float(x), float(y), float(z)), cor, energia, alcance)
    return len(pts)
