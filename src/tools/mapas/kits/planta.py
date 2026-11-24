"""
A planta de um mapa de kit desenhada em letras: cada letra é uma célula de ``cel`` metros (2,5 m: a largura de um beco),
vista de cima, com x para a direita e z para baixo (como o minimapa e as imagens de ``Grade.imagem``).

    OESTE = ["hhhhhh....",          # desenha-se só a metade oeste: a leste sai dela, girada 180° ('giro') ou
             "hhh.......",          # espelhada ('espelho'), e os dois times veem o mesmo mapa
             ...]
    P = Planta(OESTE, simetria='giro')

O que cada letra quer dizer é o mapa que diz (as casas, os muros, os carros). A planta só sabe:

* **regiões** (``regioes``): os retângulos de uma mesma letra — um prédio por retângulo, ou vários, cortados no tamanho
  da peça (``fatiar``);
* **paredes finas** (``paredes``): ``-`` e ``|`` são parede no meio da célula, ``+`` é o canto ou o encontro, e uma letra
  de vão (a porta, a janela) abre a parede naquela célula;
* **marcas** (``marca``): os algarismos, por onde passam as rotas dos bots; a marca do lado leste é a imagem da oeste;
* **para onde olha** (``frente``): o lado do retângulo com mais chão livre — a fachada do prédio.
"""
import math
import zlib


class Planta:
    def __init__(self, oeste, cel=2.5, simetria='giro', setas=False):
        """``setas``: as letras ``< > ^ v`` são direções (a escada que sobe para lá) e viram do outro lado do mapa."""
        R = len(oeste); W = len(oeste[0])
        for i, l in enumerate(oeste):
            if len(l) != W: raise ValueError(f'linha {i} da planta com {len(l)} letras (as outras têm {W})')
        vira = str.maketrans('<>^v', '><v^' if simetria == 'giro' else '><^v') if setas else {}
        if simetria == 'giro': g = [oeste[r] + oeste[R - 1 - r][::-1].translate(vira) for r in range(R)]
        elif simetria == 'espelho': g = [l + l[::-1].translate(vira) for l in oeste]
        else: g = list(oeste)
        self.g, self.R, self.C, self.cel, self.simetria = g, R, len(g[0]), cel, simetria
        self.X, self.Z = self.C * cel / 2, R * cel / 2

    # ---- onde fica
    def xz(self, r, c):
        return (-self.X + (c + 0.5) * self.cel, -self.Z + (r + 0.5) * self.cel)

    def mundo(self, c0, r0, c1, r1):
        """O retângulo das células [c0, c1) × [r0, r1), em metros: (x0, z0, x1, z1)."""
        return (-self.X + c0 * self.cel, -self.Z + r0 * self.cel, -self.X + c1 * self.cel, -self.Z + r1 * self.cel)

    def letra(self, r, c, fora='#'):
        return self.g[r][c] if 0 <= r < self.R and 0 <= c < self.C else fora

    def oposto(self, x, z):
        """O ponto do outro time."""
        return (-x, -z) if self.simetria == 'giro' else (-x, z)

    # ---- regiões
    def regioes(self, letras):
        """Os retângulos de células seguidas da mesma letra: [(letra, c0, r0, c1, r1)], de cima para baixo."""
        visto = [[False] * self.C for _ in range(self.R)]; out = []
        for r in range(self.R):
            for c in range(self.C):
                L = self.g[r][c]
                if L not in letras or visto[r][c]: continue
                c1 = c
                while c1 < self.C and self.g[r][c1] == L and not visto[r][c1]: c1 += 1
                r1 = r + 1
                while r1 < self.R and all(self.g[r1][k] == L and not visto[r1][k] for k in range(c, c1)): r1 += 1
                for i in range(r, r1):
                    for k in range(c, c1): visto[i][k] = True
                out.append((L, c, r, c1, r1))
        return out

    def blocos(self, letras):
        """Como ``regioes``, mas sem distinguir as letras entre si (a laje de um andar: tudo o que tem piso)."""
        guarda = self.g; self.g = [''.join('\x01' if L in letras else ' ' for L in linha) for linha in guarda]
        try: return [reg[1:] for reg in self.regioes('\x01')]
        finally: self.g = guarda

    def marcada(self, retangulos, letra):
        """Outra planta igual, com as células dos ``retangulos`` (os de ``borda``) trocadas pela ``letra``: para tratar o
        maciço de fora (alto, onde ninguém chega) diferente do de dentro."""
        g = [list(l) for l in self.g]
        for c0, r0, c1, r1 in retangulos:
            for r in range(r0, r1):
                for c in range(c0, c1): g[r][c] = letra
        P = Planta.__new__(Planta); P.__dict__.update(self.__dict__); P.g = [''.join(l) for l in g]; return P

    def fatiar(self, reg, largo, fundo=None):
        """Corta o retângulo em pedaços de mais ou menos ``largo`` × ``fundo`` células (em metros: (x0, z0, x1, z1))."""
        _, c0, r0, c1, r1 = reg; fundo = fundo or largo
        nx = max(1, int(round((c1 - c0) / largo))); nz = max(1, int(round((r1 - r0) / fundo)))
        x0, z0, x1, z1 = self.mundo(c0, r0, c1, r1); out = []
        for j in range(nz):
            for i in range(nx):
                out.append((x0 + (x1 - x0) * i / nx, z0 + (z1 - z0) * j / nz, x0 + (x1 - x0) * (i + 1) / nx, z0 + (z1 - z0) * (j + 1) / nz))
        return out

    def celulas(self, letras):
        return [(self.g[r][c], r, c) + self.xz(r, c) for r in range(self.R) for c in range(self.C) if self.g[r][c] in letras]

    def livres(self, x0, z0, x1, z1, solidos):
        """Quantas células de chão livre encostam em cada lado do retângulo (em metros): {giro da fachada: quantas}."""
        c0 = int(round((x0 + self.X) / self.cel)); c1 = int(round((x1 + self.X) / self.cel))
        r0 = int(round((z0 + self.Z) / self.cel)); r1 = int(round((z1 + self.Z) / self.cel))
        livre = lambda r, c: self.letra(r, c) not in solidos
        return {0.0: sum(livre(r1, c) for c in range(c0, c1)), 180.0: sum(livre(r0 - 1, c) for c in range(c0, c1)),
                90.0: sum(livre(r, c1) for r in range(r0, r1)), -90.0: sum(livre(r, c0 - 1) for r in range(r0, r1))}

    def frente(self, ret, solidos, tam=None, folga=1.3):
        """O giro da fachada de um prédio no retângulo ``ret`` (metros): o lado com mais chão livre. Com ``tam`` (largura,
        fundo da peça), só entre os giros em que a peça estica menos (até ``folga`` vezes mais que o melhor)."""
        x0, z0, x1, z1 = ret; n = self.livres(x0, z0, x1, z1, solidos); cand = list(n)
        if tam is not None:
            def torce(yaw):
                lx, lz = (x1 - x0, z1 - z0) if yaw in (0.0, 180.0) else (z1 - z0, x1 - x0)
                a = (lx / tam[0]) / (lz / tam[1]); return max(a, 1 / a)
            melhor = min(torce(y) for y in cand); cand = [y for y in cand if torce(y) <= melhor * folga]
        # no empate, a fachada olha para o meio do mapa
        cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
        def meio(yaw): return {0.0: -cz, 180.0: cz, 90.0: -cx, -90.0: cx}[yaw]
        return max(cand, key=lambda y: (n[y], meio(y)))

    def contorno(self, letras, fora=True):
        """As faces de um maciço (as células das ``letras``) que dão para o resto: [(xa, za, xb, zb, giro da face)], retas
        e já emendadas. ``fora``: a borda da planta conta como maciço (não ganha face)."""
        dentro = lambda r, c: (self.g[r][c] in letras) if 0 <= r < self.R and 0 <= c < self.C else fora
        e = self.cel; out = []
        for r in range(self.R + 1):                     # faces ao longo de x, na linha entre as fileiras r−1 e r
            for yaw, de_cima in ((0.0, True), (180.0, False)):
                c = 0
                while c < self.C:
                    tem = lambda k: (dentro(r - 1, k) and not dentro(r, k) and r < self.R) if de_cima else (dentro(r, k) and not dentro(r - 1, k) and r > 0)
                    if not tem(c): c += 1; continue
                    ini = c
                    while c < self.C and tem(c): c += 1
                    out.append((ini * e - self.X, r * e - self.Z, c * e - self.X, r * e - self.Z, yaw))
        for c in range(self.C + 1):                     # faces ao longo de z, na linha entre as colunas c−1 e c
            for yaw, da_esq in ((90.0, True), (-90.0, False)):
                r = 0
                while r < self.R:
                    tem = lambda k: (dentro(k, c - 1) and not dentro(k, c) and c < self.C) if da_esq else (dentro(k, c) and not dentro(k, c - 1) and c > 0)
                    if not tem(r): r += 1; continue
                    ini = r
                    while r < self.R and tem(r): r += 1
                    out.append((c * e - self.X, ini * e - self.Z, c * e - self.X, r * e - self.Z, yaw))
        return out

    # ---- paredes finas
    def paredes(self, h='-', v='|', canto='+', vaos='', sobra=0.0):
        """As paredes finas: ``muros`` [(xa, za, xb, zb)] (retas, já emendadas), ``vaos`` [(letra, x, z, ao_x)] (a célula
        aberta na parede: a porta, a janela) e ``cantos`` [(x, z)].

        ``h`` e ``v`` são as letras da parede ao longo de x e de z; a parede acaba na borda da célula, ou no meio da
        célula vizinha quando ela é um ``canto`` (pode-se dar como canto as letras de outra família de parede: a divisória
        que encosta na parede de fora). ``sobra``: quanto a ponta passa do meio do canto (meia espessura fecha a quina)."""
        e = self.cel; muros, abertos = [], []
        def vao_ao_x(r, c):
            # o vão está numa parede ao longo de x se, passando pelos vãos vizinhos, dá numa parede dessas (de um lado ou do outro)
            for passo in (-1, 1):
                k = c + passo
                while self.letra(r, k, '') != '' and self.letra(r, k, '') in vaos: k += passo
                L = self.letra(r, k, '')
                if L != '' and L in h + canto: return True
            return False
        def serve(r, c, ao_x):
            L = self.g[r][c]
            return L in (h if ao_x else v) or (L in vaos and vao_ao_x(r, c) == ao_x)
        for ao_x in (True, False):
            n_a, n_b = (self.R, self.C) if ao_x else (self.C, self.R)
            for a in range(n_a):
                cel = (lambda b: (a, b)) if ao_x else (lambda b: (b, a))
                eh_canto = lambda b: 0 <= b < n_b and self.g[cel(b)[0]][cel(b)[1]] in canto
                b = 0
                while b < n_b:
                    if not serve(*cel(b), ao_x): b += 1; continue
                    ini = fim = b; k = b + 1
                    while k < n_b:
                        if serve(*cel(k), ao_x): fim = k; k += 1
                        elif eh_canto(k) and k + 1 < n_b and serve(*cel(k + 1), ao_x): k += 1
                        else: break
                    eixo = (a + 0.5) * e
                    p0 = (ini - 0.5) * e - sobra if eh_canto(ini - 1) else ini * e
                    final = (fim + 1.5) * e + sobra if eh_canto(fim + 1) else (fim + 1) * e
                    for q in range(ini, fim + 1):
                        L = self.g[cel(q)[0]][cel(q)[1]]
                        if L in vaos:
                            if q * e - p0 > 0.01: muros.append((p0, q * e, eixo, ao_x))
                            abertos.append((L, (q + 0.5) * e, eixo, ao_x)); p0 = (q + 1) * e
                    if final - p0 > 0.01: muros.append((p0, final, eixo, ao_x))
                    b = fim + 1
        def m(a, b, eixo, ao_x):
            return (a - self.X, eixo - self.Z, b - self.X, eixo - self.Z) if ao_x else (eixo - self.X, a - self.Z, eixo - self.X, b - self.Z)
        out_v = [(L, p - self.X, eixo - self.Z, True) if ao_x else (L, eixo - self.X, p - self.Z, False) for L, p, eixo, ao_x in abertos]
        return [m(*x) for x in muros], out_v, [(x, z) for _, r, c, x, z in self.celulas(canto)]

    # ---- marcas e rotas
    def marca(self, ch, leste=False):
        """O ponto (x, z) da marca ``ch`` na metade oeste; ``leste``: a imagem dela do outro lado."""
        for r in range(self.R):
            c = self.g[r].find(ch, 0, self.C // 2 if self.simetria else self.C)
            if c >= 0:
                x, z = self.xz(r, c); return self.oposto(x, z) if leste else (x, z)
        raise KeyError(f'marca {ch!r} fora da planta')

    def rota(self, a, ida, volta=None):
        """Os pontos de uma rota de ronda de base a base: sai de ``a``, passa pelas marcas de ``ida`` (no oeste) e chega à
        outra base pelas de ``volta`` (as do leste, de trás para a frente; por omissão, as mesmas da ida)."""
        volta = ida if volta is None else volta
        return [a] + [self.marca(ch) for ch in ida] + [self.marca(ch, True) for ch in reversed(volta)] + [self.oposto(*a)]

    # ---- o que varia de um lugar para outro, igual dos dois lados do mapa
    def chave(self, x, z, sal=0):
        """Um número tirado do lugar, o mesmo no ponto do outro time (para sortear a peça sem desigualar os lados)."""
        a = (round(x * z, 1), round(x * x + z * z, 1)) if self.simetria == 'giro' else (round(abs(x), 1), round(z, 1))
        return zlib.crc32(repr((a, sal)).encode())

    def sorteio(self, x, z, opcoes, sal=0):
        return opcoes[self.chave(x, z, sal) % len(opcoes)]

    def desvio(self, x, z, amplitude, sal=0):
        """Um giro de −amplitude a +amplitude graus, simétrico (no espelho, o leste gira ao contrário)."""
        d = (self.chave(x, z, sal) % 2001 / 1000.0 - 1.0) * amplitude
        return -d if self.simetria == 'espelho' and x > 0 else d

    def sorte(self, x, z, sal=0):
        """Um número de 0 a 1 tirado do lugar (o mesmo no ponto do outro time): para espalhar enfeites."""
        return (self.chave(x, z, sal) % 100003) / 100003.0

    def beiras(self, solidos, finas='', onde='.', grosso=0.3):
        """As células de chão livre (as letras de ``onde``) encostadas numa parede: [(r, c, x, z, giro, dist)], uma por
        parede vizinha. ``giro``: para onde olha quem está de costas para a parede; ``dist``: do meio da célula à face
        dela (a parede fina, das letras de ``finas``, fica no meio da célula vizinha)."""
        out = []
        for r in range(self.R):
            for c in range(self.C):
                if self.g[r][c] not in onde: continue
                x, z = self.xz(r, c)
                for dr, dc, yaw in ((-1, 0, 0.0), (1, 0, 180.0), (0, -1, 90.0), (0, 1, -90.0)):
                    L = self.letra(r + dr, c + dc)
                    if L in finas:
                        # só a parede que passa de frente (a ponta e o canto dela não servem de encosto)
                        if L in ('-=' if dr else '|!'): out.append((r, c, x, z, yaw, self.cel - grosso / 2))
                    elif L in solidos: out.append((r, c, x, z, yaw, self.cel / 2))
        return out

    def borda(self, letras):
        """Os retângulos (em células) do maciço das ``letras`` que encosta na borda da planta — o lado de fora do mapa,
        onde ninguém pode subir (``montagem.barrar``)."""
        dentro = [[False] * self.C for _ in range(self.R)]
        pilha = [(r, c) for r in range(self.R) for c in range(self.C) if (r in (0, self.R - 1) or c in (0, self.C - 1)) and self.g[r][c] in letras]
        while pilha:
            r, c = pilha.pop()
            if not (0 <= r < self.R and 0 <= c < self.C) or dentro[r][c] or self.g[r][c] not in letras: continue
            dentro[r][c] = True; pilha += [(r - 1, c), (r + 1, c), (r, c - 1), (r, c + 1)]
        guarda = self.g; self.g = [''.join('\x01' if v else ' ' for v in linha) for linha in dentro]
        try: return [reg[1:] for reg in self.regioes('\x01')]
        finally: self.g = guarda

    def corrida(self, r, c, solidos):
        """Quantas células livres seguidas passam por (r, c), ao longo de x e de z: (nx, nz) — a rua corre no maior."""
        def conta(dr, dc):
            n = 0; i, k = r + dr, c + dc
            while self.letra(i, k) not in solidos and n < 40: n += 1; i += dr; k += dc
            return n
        return conta(0, 1) + conta(0, -1) + 1, conta(1, 0) + conta(-1, 0) + 1

    def texto(self):
        return '\n'.join(self.g)
