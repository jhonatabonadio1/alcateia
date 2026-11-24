#!/usr/bin/env python3
"""
As animações de terceira pessoa com arma, feitas no Blender a partir das da
primeira pessoa (Jhon, 22/11: "vamos copiar exatamente as animações dos
braços, tronco e etc e vamos adaptá-las nos personagens, vamos usar o rig de
cada arma para isso"; "cada arma tem sua posição de segurar também").

    python3 tools/personagens/blender/animacoes_armas.py                   # todos, todas as armas
    python3 tools/personagens/blender/animacoes_armas.py barut ar15        # um personagem, uma arma
    python3 tools/personagens/blender/animacoes_armas.py barut ar15 --clipes parado,recarga --fotos /tmp/fotos

Para cada personagem (toda pasta de assets/personagens/ com humano.glb), cada
conjunto da primeira pessoa (ak74, ar15, m4a4, scar, svd, deagle, faca,
granada: as variantes de pintura usam o do modelo) e cada animação que o
corpo toca, faz a animação de terceira pessoa quadro a quadro (30 por
segundo):

- A arma vai no corpo do jeito de verdade (POSTURA): o fuzil com a coronha no
  ombro, a pistola com os braços quase esticados, a mira com o olho na linha
  da mira. Na primeira pessoa ela é posta para a câmera (o fuzil parado
  encosta a coronha na bochecha, a pistola mirando fica a 10 cm do olho) e
  copiar isso entortava o corpo. Daí em diante a arma se mexe como na
  primeira pessoa (o mesmo movimento, só que posto no corpo).
- As mãos vão na arma onde as da primeira pessoa vão, em relação a ela: a
  palma no cabo, o indicador no gatilho, a mão de apoio no guarda-mão, a mão
  no carregador na recarga. Cada dedo aponta para onde o de lá aponta e
  fecha até encostar na arma (o personagem tem a mão maior); nenhuma junta
  passa do que a mão de uma pessoa dobra (a ponta não vira para trás, os
  dedos não abrem).
- O cotovelo é escolhido para o punho ficar reto, e metade do giro do punho
  vai para o antebraço (o corpo não tem osso de torção).
- A cabeça segue o movimento da cabeça do animador (o osso "interaction":
  olhar o carregador na recarga, o tranco do tiro).
- A faca e a granada usam o corpo inteiro (o tronco gira no golpe e no
  lance; o braço livre faz a guarda ou aponta o alvo).

Confere cada quadro (MEDIDAS) e escreve o pior caso de cada clipe: a mão
longe da arma, a dobra do punho, as juntas dos dedos e a arma dentro do
corpo.

Grava, por personagem, `assets/personagens/<p>/animacoes/<arma>.json`: para
cada clipe, a rotação local de cada osso da cintura para cima (no espaço do
osso do jogo) e onde o conjunto da primeira pessoa fica em relação ao peito
(Spine2), quadro a quadro, em inteiros de 16 bits (base64). O jogo toca o
clipe junto com o da arma (`HoldClips`, `HeldWeapon`).

Precisa do módulo `bpy` (`pip install bpy`).
"""
import base64
import json
import math
import os
import struct
import sys
import time

import bpy
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import poses_armas as pa  # noqa: E402
from poses_armas import CIMA, DEDOS, DEDOS_FP, DIREITA, EIXOS, FICHA, FRENTE, PREFIXO, RAIZ, orient  # noqa: E402

FPS = 30

# O tipo de cada conjunto (as variantes de pintura usam o do modelo: a mesma
# malha e as mesmas animações).
ARMAS = {
    'ak74': 'fuzil', 'ar15': 'fuzil', 'm4a4': 'fuzil', 'scar': 'fuzil', 'svd': 'fuzil',
    'deagle': 'pistola', 'faca': 'faca', 'granada': 'granada',
}

# Os clipes que o corpo toca (HeldRig): o descanso, as ações e a mira.
CLIPES = {
    'fuzil': ('parado', 'andar', 'tiro', 'recarga', 'sacar',
              'mirar', 'mira', 'mira_andar', 'mira_tiro', 'desmirar'),
    'pistola': ('parado', 'andar', 'tiro', 'recarga', 'recarga_vazia', 'sacar',
                'mirar', 'mira', 'mira_andar', 'mira_tiro', 'desmirar'),
    'faca': ('parado', 'andar', 'golpe', 'golpe2', 'golpe3', 'golpe4', 'sacar'),
    'granada': ('parado', 'andar', 'sacar', 'armar', 'armado', 'arremesso'),
}

# Os que se repetem (o jogo toca em laço).
LACOS = ('parado', 'andar', 'mira', 'mira_andar', 'armado')

# Em que postura a arma fica em cada clipe; mirar e desmirar passam de uma
# para a outra no ritmo em que a arma da primeira pessoa passa.
NA_MIRA = ('mira', 'mira_andar', 'mira_tiro')
TRANSICOES = {'mirar': ('quadril', 'mira'), 'desmirar': ('mira', 'quadril')}

# Os ossos que a animação leva (da cintura para cima), na ordem do arquivo.
OSSOS = pa.OSSOS

# ------------------------------------------------------------------ a postura de cada tipo
#
# tronco: giro (+ vira para a esquerda dele), inclinação (+ para a frente) e
# caimento (+ para a direita), em graus, repartidos pelas vértebras.
# cabeca: inclinação (+ para baixo) e caimento (+ para a direita). ombro_d /
# ombro_e: a clavícula sobe e vai para a frente (graus).
#
# O lugar da arma:
# - fuzil, quadril: a coronha no ombro (o "bolso" do ombro, na pele do peito
#   na frente da junta), `coronha` (m, x para a direita dele, y para a frente,
#   z para cima) a partir dali; o cano para a frente, `cai` graus para baixo.
# - fuzil e pistola, mira: o olho direito na linha da mira (a câmera da
#   primeira pessoa mirando é essa linha), a arma `longe` metros para a
#   frente do olho (o fuzil: até a coronha chegar no ombro).
# - pistola, quadril; faca; granada: o cabo (o osso da arma) em `cabo`, a
#   partir do meio dos ombros, e o cano `cai` graus para baixo.
#
# cotovelo_d / cotovelo_e: para onde o cotovelo prefere apontar (fora, cima,
# trás); o cotovelo gira em volta dali até o punho ficar reto.
POSTURA = {
    'fuzil': {
        'quadril': dict(tronco=(-22, 5, 0), cabeca=(3, 0), ombro_d=(4, 6), ombro_e=(0, 12),
                        coronha=(0.015, 0.0, -0.035), cai=4.0,
                        cotovelo_d=(0.65, -0.75, 0.15), cotovelo_e=(-0.2, -1.0, 0.0)),
        'mira': dict(tronco=(-22, 8, 0), cabeca=(12, 10), ombro_d=(8, 8), ombro_e=(0, 14),
                     longe=None,
                     cotovelo_d=(0.75, -0.6, 0.15), cotovelo_e=(-0.2, -1.0, 0.0)),
    },
    'pistola': {
        'quadril': dict(tronco=(-4, 8, 0), cabeca=(4, 0), ombro_d=(2, 16), ombro_e=(2, 16),
                        cabo=(0.02, 0.44, -0.09), cai=6.0,
                        cotovelo_d=(0.7, -0.7, 0.0), cotovelo_e=(-0.7, -0.7, 0.0)),
        'mira': dict(tronco=(-4, 10, 0), cabeca=(6, 2), ombro_d=(4, 20), ombro_e=(4, 20),
                     longe=0.36,
                     cotovelo_d=(0.7, -0.7, 0.0), cotovelo_e=(-0.7, -0.7, 0.0)),
    },
    'faca': {
        'quadril': dict(tronco=(-12, 12, 0), cabeca=(6, 0), ombro_d=(0, 10), ombro_e=(4, 10),
                        cabo=(0.10, 0.40, -0.16), cai=0.0,
                        cotovelo_d=(0.55, -0.85, 0.1), cotovelo_e=(-0.4, -0.9, 0.0)),
    },
    'granada': {
        'quadril': dict(tronco=(-6, 5, 0), cabeca=(5, 0), ombro_d=(0, 6), ombro_e=(0, 4),
                        cabo=(0.10, 0.34, -0.20), cai=0.0,
                        cotovelo_d=(0.6, -0.8, 0.0), cotovelo_e=(-0.3, -1.0, 0.1)),
    },
}

# A pistola parada: quanto do alcance do braço o pulso usa (1 = esticado) e quanto
# o cabo sobe (m) em relação ao desenho de POSTURA.
PISTOLA_ESTICADO = float(os.environ.get('PISTOLA_ESTICADO', 0.93))
PISTOLA_ALTURA = float(os.environ.get('PISTOLA_ALTURA', 0.07))

# Quanto o tronco pode virar a mais com o fuzil (graus, do menos para o
# mais) para a mão de apoio chegar no guarda-mão, e até quanto ela pode
# deslizar para trás (m) sem precisar virar mais.
GIRO_A_MAIS = (0.0, -5.0, -10.0, -14.0, -18.0)
DESLIZA_BOM = 0.04
# Deslizando para trás, a palma para a esta fração da largura da mão antes
# da frente do carregador.
MAO_NO_CARREGADOR = 0.4
# quanto custa (por grau) a mão, na mira, girar na arma para fora do jeito do quadril
PERTO_DO_QUADRIL = float(os.environ.get('PERTO_DO_QUADRIL', 1.2))
# Quanto a mão de apoio pode girar deitada no guarda-mão (graus, em volta da
# normal da palma) para aliviar o punho. Era ±50, e a procura escolhia 20 a
# 30 em todo fuzil: a fileira dos nós ficava na diagonal do cano, o nó do
# indicador ia parar embaixo do guarda-mão (o dedo saía reto para a frente,
# sem ter como subir pelo lado) e o do mínimo a 3 cm da arma, no ar — os
# dedos em leque (Jhon, 23/11). Na primeira pessoa a fileira dos nós fica ao
# longo do cano e os quatro dedos sobem juntos pelo lado; e o punho de lá já
# dobra de lado 15° a 22° nessa mão, então o daqui (20° a 33°) fica perto.
GUINA_APOIO = int(os.environ.get('GUINA_APOIO', 0))
# Quanto a mão do gatilho pode girar em volta do cabo para aliviar o punho
# (graus). Era ±50, e na pistola a procura girava 25° a 30°: a mão saía de
# trás do cabo para o lado dele, o nó do indicador recuava e o dedo ficava a
# 12-21 mm do gatilho, sem alcançar. Com ±10 a polpa chega (0,6 a 3 mm) e o
# punho continua dentro do confortável (dobra e lado até ~15°).
GIRO_CABO = int(os.environ.get('GIRO_CABO', 10))

# O bolso do ombro (onde a coronha encosta): a fração da largura até a
# junta do ombro e quanto abaixo dela (m).
BOLSO = (0.62, -0.025)

# Mirando o fuzil, quanto a linha da mira pode passar abaixo do olho (m):
# a cabeça deita na coronha, mas o personagem tem o pescoço comprido e a
# coronha tem de chegar no ombro.
DESCE_MAX = 0.10

# Onde a palma encosta: o ponto entre o pulso e o começo do dedo médio
# (fração) que vai no mesmo lugar nas duas mãos (a do personagem é maior).
ALINHA = 0.8

# Os limites de uma mão de verdade (graus). Juntas dos dedos: a base (MCP),
# o meio (PIP) e a ponta (DIP) — o meio quase não estica para trás, a ponta
# no máximo um pouco (Jhon: "ponta dos dedos viradas para trás").
LIMITE_DEDO = {'mcp': (-12.0, 90.0), 'pip': (0.0, 105.0), 'dip': (-5.0, 80.0)}
LIMITE_POLEGAR = {'mcp': (-15.0, 60.0), 'pip': (-10.0, 60.0), 'dip': (-10.0, 80.0)}
# O punho: dobra para a palma/para trás e para os lados que ainda parece
# confortável (o uso funcional da mão fica bem dentro dos 60°/40° máximos).
PUNHO_DOBRA = 25.0
PUNHO_LADO = 15.0
PUNHO_GIRO = 70.0
# Além disso nem a primeira pessoa justifica (dobra, lado).
PUNHO_MAX = (40.0, 28.0)

# A arma do tamanho da mão: o comprimento da mão da primeira pessoa (m, do
# pulso à ponta do médio) e entre quanto a arma pode crescer no personagem.
MAO_FP = 0.199
# O braço da primeira pessoa (m, do ombro ao pulso).
BRACO_FP = 0.551
ESCALA_MIN, ESCALA_MAX = 1.0, 1.3

# Quanto a mão pode girar em volta do cabo de um quadro para outro, além
# do jeito de pegar da postura (graus).
GIRA_NA_ARMA = (-8, 0, 8)

# O encaixe dos dedos na arma (Dedos._encaixar). A pele do dedo fica a FOLGA
# (m) da arma, no mínimo, e conta como encostada a menos de TOQUE. A falange
# de lá "encosta" se a linha do osso dela passa a menos de PERTO_FP (m;
# polegar, dedos) da arma, ou (o terceiro) da linha de um dedo da outra mão. PELE_PONTOS: quantos pontos da malha de cada
# falange entram na conta; TETO_FOLGA: até onde se procura a arma (m).
FOLGA = 0.0008
TOQUE = 0.003
TETO_FOLGA = 0.03
TETO_POLEGAR = 0.08
PERTO_FP = (0.019, 0.016, 0.024)
PELE_PONTOS = int(os.environ.get('PELE_PONTOS', 32))
# A procura: o passo da grade no alcance inteiro da junta e em volta do
# quadro anterior (rad), e quantas dobras de base e meio, no máximo, vão até a ponta.
PASSO_GRADE = math.radians(8.5)
PASSO_PERTO = math.radians(6.0)
CANDIDATOS = 60
# O custo de cada dobra: a falange que devia encostar longe da arma (por m),
# a junta longe do ângulo de lá (por rad; a de quem segura e a de quem só
# copia), e a ponta fora da proporção do meio (PONTA_DO_MEIO, por rad).
PESO_TOQUE = 120.0
PESO_LA = 0.6
PESO_COPIA = 2.5
PESO_PONTA = 0.35
PONTA_DO_MEIO = 0.6
# Entrar na arma custa caro (um degrau e mais por metro) mas não é proibido:
# num gesto rápido o dedo do personagem, mais grosso, às vezes não cabe onde
# o de lá passa, e proibir fazia o dedo pular para uma dobra sem nada a ver.
# E mudar de um quadro para o outro também custa (por rad).
PESO_DENTRO = 400.0
PESO_TEMPO = 0.4
# Os dedos juntos: cada falange dobra, além da de lá, o tanto que a do dedo
# vizinho dobrou além da dele (por rad de diferença). Sem isso cada dedo
# achava a arma por conta própria, e o que não a alcançava (o mínimo do
# Barut, com o nó para fora do guarda-mão) ficava esticado no ar ao lado dos
# outros fechados.
PESO_JUNTO = float(os.environ.get('PESO_JUNTO', 1.2))
# O punho fechado (faca, granada): o polegar fecha por cima dos dedos. A
# ponta dele longe da pele dos dedos custa (por m): sem isso o polegar da
# Eve achava a espoleta da granada antes e ficava em pé encostado nela, de
# "joinha" (Jhon, 23/11), em vez de fechar em volta como o do Barut.
PESO_FECHA = float(os.environ.get('PESO_FECHA', 40.0))
# O indicador no gatilho (Dedos._no_gatilho): onde, ao longo da última
# falange (fração a partir da junta), fica a polpa que aperta. E o plano B
# de antes (o indicador estendido fora do guarda-mato), desligado.
GATILHO_POLPA = float(os.environ.get('GATILHO_POLPA', 0.5))
INDICADOR_FORA = bool(int(os.environ.get('INDICADOR_FORA', 0)))


def deg(r):
    return math.degrees(r)


def rad(d):
    return math.radians(d)


def assentar_rot(y_mundo, h_mundo, h_local):
    """A orientação (3x3) de um osso com o eixo dele (Y) em y_mundo e o eixo
    de dobra dele (h_local, no espaço do osso) em h_mundo."""
    yl = Vector((0, 1, 0))
    hl = (h_local - yl * h_local.dot(yl)).normalized()
    Y = y_mundo.normalized()
    H = (h_mundo - Y * h_mundo.dot(Y))
    if H.length < 1e-6:
        H = Y.orthogonal()
    H.normalize()
    ml = Matrix((yl, hl, yl.cross(hl))).transposed()
    mw = Matrix((Y, H, Y.cross(H))).transposed()
    return mw @ ml.inverted()


def ang_em_torno(a, b, eixo):
    """O ângulo de a até b em torno do eixo (rad), no plano dele."""
    pa_ = a - eixo * a.dot(eixo)
    pb_ = b - eixo * b.dot(eixo)
    if pa_.length < 1e-8 or pb_.length < 1e-8:
        return 0.0
    return math.atan2(eixo.dot(pa_.cross(pb_)), pa_.dot(pb_))


def girar_v(v, eixo, angulo):
    if abs(angulo) < 1e-9:
        return v.copy()
    return Quaternion(eixo, angulo) @ v


def suave(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


# ------------------------------------------------------------------ o esqueleto em conta própria

class Esqueleto:
    """O esqueleto do personagem em conta própria: a rotação local de cada
    osso → a matriz de cada um no espaço da armadura, como o Blender faria,
    sem esperar ele recalcular (milhares de quadros)."""

    def __init__(self, arm):
        ossos = arm.data.bones
        self.nomes = [b.name for b in sorted(ossos, key=lambda b: len(b.parent_recursive))]
        self.pai = {b.name: (b.parent.name if b.parent else None) for b in ossos}
        self.filhos = {n: [] for n in self.nomes}
        for n, p in self.pai.items():
            if p:
                self.filhos[p].append(n)
        self.repouso = {b.name: b.matrix_local.copy() for b in ossos}
        self.desvio = {}
        for n in self.nomes:
            p = self.pai[n]
            self.desvio[n] = self.repouso[p].inverted() @ self.repouso[n] if p else self.repouso[n].copy()

    def fk(self, locais):
        M = {}
        for n in self.nomes:
            p = self.pai[n]
            base = M[p] @ self.desvio[n] if p else self.desvio[n].copy()
            q = locais.get(n)
            M[n] = base @ q.to_matrix().to_4x4() if q is not None else base
        return M

    def refazer(self, M, locais, n):
        """Refaz o osso n e os que vêm depois dele."""
        pilha = [n]
        while pilha:
            x = pilha.pop()
            p = self.pai[x]
            base = M[p] @ self.desvio[x] if p else self.desvio[x].copy()
            q = locais.get(x)
            M[x] = base @ q.to_matrix().to_4x4() if q is not None else base
            pilha.extend(self.filhos[x])

    def base(self, n, M):
        p = self.pai[n]
        return M[p] @ self.desvio[n] if p else self.desvio[n].copy()

    def por(self, M, locais, n, rot):
        """Deixa o osso n com a orientação rot (3x3, armadura) e refaz os de baixo."""
        b = self.base(n, M)
        q = (b.to_3x3().inverted() @ rot).to_quaternion().normalized()
        locais[n] = q
        self.refazer(M, locais, n)
        return q


# ------------------------------------------------------------------ o personagem

class Corpo(pa.Corpo):
    def __init__(self, caminho):
        super().__init__(caminho)
        self.esq = Esqueleto(self.arm)
        self.nome = os.path.basename(os.path.dirname(caminho))
        for o in self.malhas:
            o.hide_render = False
        self._medir_mao()
        self._medir_rosto()

    def n(self, nome):
        return PREFIXO + nome

    def _medir_mao(self):
        """A espessura da palma e de cada dedo (da linha dos ossos à pele) e
        o comprimento de cada falange."""
        pesos = {}
        for o in self.malhas:
            grupos = {g.index: g.name for g in o.vertex_groups}
            for v in o.data.vertices:
                if not v.groups:
                    continue
                g = max(v.groups, key=lambda g: g.weight)
                nome = grupos.get(g.group, '')
                if 'Hand' in nome:
                    pesos.setdefault(nome, []).append(o.matrix_world @ v.co)
        self.raio_dedo = {}
        for lado in ('Left', 'Right'):
            for d in DEDOS:
                r = []
                for k in (1, 2, 3):
                    nome = self.n(f'{lado}Hand{d}{k}')
                    a = self.arm.data.bones[nome].head_local
                    b = self.arm.data.bones[self.n(f'{lado}Hand{d}{k + 1}')].head_local
                    eixo = (b - a)
                    L = eixo.length
                    eixo.normalize()
                    for p in pesos.get(nome, []):
                        t = (p - a).dot(eixo)
                        if 0.15 * L < t < 0.85 * L:
                            r.append(((p - a) - eixo * t).length)
                r.sort()
                self.raio_dedo[lado + d] = r[len(r) // 2] if r else 0.009
            # A palma: dos pontos da mão, a distância até a linha pulso→médio,
            # do lado da palma.
            mao = self.arm.data.bones[self.n(lado + 'Hand')]
            a = mao.head_local
            b = self.arm.data.bones[self.n(lado + 'HandMiddle1')].head_local
            eixo = (b - a).normalized()
            dentro = mao.matrix_local.to_3x3() @ self.mao[lado]['palma']
            esp = []
            for p in pesos.get(self.n(lado + 'Hand'), []):
                t = (p - a).dot(eixo)
                if 0.3 < t / (b - a).length < 0.9:
                    s = (p - a - eixo * t).dot(dentro)
                    if s > 0:
                        esp.append(s)
            esp.sort()
            self.mao[lado]['espessura'] = esp[int(len(esp) * 0.8)] if esp else 0.02
        for lado in ('Left', 'Right'):
            b = self.arm.data.bones
            volta = b[self.n(lado + 'Hand')].matrix_local.to_3x3().inverted()
            tr = b[self.n(lado + 'HandIndex1')].head_local - b[self.n(lado + 'HandPinky1')].head_local
            self.mao[lado]['largura'] = tr.length
            self.mao[lado]['travessa'] = (volta @ tr).normalized()
        self.comprimento_falange = {}
        for lado in ('Left', 'Right'):
            for d in DEDOS:
                comp = []
                for k in (1, 2, 3):
                    a = self.arm.data.bones[self.n(f'{lado}Hand{d}{k}')].head_local
                    b = self.arm.data.bones[self.n(f'{lado}Hand{d}{k + 1}')].head_local
                    comp.append((b - a).length)
                self.comprimento_falange[lado + d] = comp

    def _medir_rosto(self):
        """O olho direito (no espaço do osso da cabeça) e o bolso do ombro
        direito (no espaço da clavícula direita), achados na malha."""
        cab = self.arm.data.bones[self.n('Head')]
        topo = self.arm.data.bones[self.n('HeadTop_End')].head_local
        z = cab.head_local.z + 0.45 * (topo.z - cab.head_local.z)
        pts = []
        largura = []
        for o in self.malhas:
            if 'head' not in o.name.lower():
                continue
            for v in o.data.vertices:
                p = o.matrix_world @ v.co
                if abs(p.z - z) < 0.012:
                    largura.append(p.x)
                    pts.append(p)
        meia = (max(largura) - min(largura)) * 0.5 if largura else 0.08
        x = -0.40 * meia  # o olho direito (a direita dele é −X)
        frente = [p.y for p in pts if abs(p.x - x) < 0.015]
        y = (min(frente) if frente else cab.head_local.y - 0.09) + 0.025
        olho = Vector((x, y, z))
        self.olho_local = cab.matrix_local.inverted() @ olho
        self.olho_repouso = olho

        # O bolso do ombro: da frente do peito, na altura logo abaixo da
        # junta do ombro, entre ela e o meio do peito.
        junta = self.arm.data.bones[self.n('RightArm')].head_local
        alvo = Vector((junta.x * BOLSO[0], -1.0, junta.z + BOLSO[1]))
        melhor = None
        for o in self.malhas:
            nome = o.name.lower()
            if 'upper' not in nome and 'body' not in nome:
                continue
            dg = bpy.context.evaluated_depsgraph_get()
            mw = o.matrix_world
            ok, loc, nor, idx = o.ray_cast(mw.inverted() @ alvo, (mw.inverted().to_3x3() @ Vector((0, 1, 0))).normalized(), depsgraph=dg)
            if ok:
                p = mw @ loc
                if melhor is None or p.y < melhor.y:
                    melhor = p
        if melhor is None:
            melhor = Vector((alvo.x, -0.12, alvo.z))
        self.bolso_repouso = melhor
        cla = self.arm.data.bones[self.n('RightShoulder')]
        self.bolso_local = cla.matrix_local.inverted() @ melhor

    def olho(self, M):
        return M[self.n('Head')] @ self.olho_local

    def bolso(self, M):
        return M[self.n('RightShoulder')] @ self.bolso_local

    # -------------------------------------------------------- a postura base

    def postura_base(self, des):
        """As rotações locais da postura (tronco, cabeça, clavículas) de um
        desenho de POSTURA, feitas com as ferramentas do poses_armas."""
        self.repousar()
        self.postura(*des['tronco'])
        self.clavicula('Right', *des['ombro_d'])
        self.clavicula('Left', *des['ombro_e'])
        self.olhar(*des['cabeca'])
        locais = {}
        for nome in ('Spine', 'Spine1', 'Spine2', 'Neck', 'Head', 'LeftShoulder', 'RightShoulder'):
            pb = self.b(nome)
            locais[pb.name] = pb.rotation_quaternion.copy()
        self.repousar()
        return locais

    # -------------------------------------------------------- escrever no Blender

    def aplicar(self, locais):
        for pb in self.pb:
            pb.rotation_mode = 'QUATERNION'
            q = locais.get(pb.name)
            pb.rotation_quaternion = q if q is not None else Quaternion()
            pb.location = Vector()
            pb.scale = Vector((1, 1, 1))
        bpy.context.view_layer.update()

    def no_jogo(self, M):
        """De cada osso da pose: a rotação local no espaço do glTF (o do jogo)."""
        mundo = {}
        for nome, ac in self.acerto.items():
            if nome in M:
                mundo[nome] = EIXOS @ M[nome] @ ac
        local = {}
        for nome, m in mundo.items():
            pai = self.repouso[nome][1]
            l = mundo[pai].inverted() @ m if pai in mundo else m
            local[nome] = l.to_quaternion().normalized()
        return local, mundo


# ------------------------------------------------------------------ o conjunto da primeira pessoa

class Conjunto:
    """O conjunto da primeira pessoa (braços, arma e animações), posto no
    corpo: a arma é a de lá, as mãos de lá dizem onde as do corpo vão."""

    def __init__(self, chave, escala=1.0):
        self.chave = chave
        self.escala = escala
        self.ficha = FICHA[chave]
        caminho = os.path.join(RAIZ, self.ficha['arquivo'].replace('res://', ''))
        antes_obj, antes_acao = set(bpy.data.objects), set(bpy.data.actions)
        bpy.ops.import_scene.gltf(filepath=caminho)
        novos = [o for o in bpy.data.objects if o not in antes_obj]
        self.acoes = {a.name.split('.')[0]: a for a in bpy.data.actions if a not in antes_acao}
        self.objetos = novos
        self.raiz = next(o for o in novos if o.type == 'EMPTY' and o.name.startswith('Conjunto'))
        self.arm = next(o for o in novos if o.type == 'ARMATURE')
        self.bracos = []
        for o in novos:
            if o.type == 'MESH' and (o.parent is None or o.name.lower().startswith('bracos')):
                o.hide_render = True
                o.hide_viewport = True
                if o.name.lower().startswith('bracos'):
                    self.bracos.append(o)
        self.armas = [o for o in novos if o.type == 'MESH' and not o.hide_render]
        self.ad = self.arm.animation_data or self.arm.animation_data_create()
        for trilha in self.ad.nla_tracks:
            trilha.mute = True
        # A arma do tamanho da mão do personagem (ESCALA_ARMA): o conjunto
        # inteiro cresce em volta do olho de lá (a origem), e com ele os
        # pontos onde as mãos vão.
        if abs(escala - 1.0) > 1e-4:
            self.raiz.matrix_world = Matrix.Scale(escala, 4) @ self.raiz.matrix_world
            bpy.context.view_layer.update()
        self.base_raiz = self.raiz.matrix_world.copy()
        self.colocacao = Matrix.Identity(4)
        self.pb = self.arm.pose.bones
        self._cache = {}
        self._pecas()
        self.gatilho = {pc['osso'] for pc in self.pecas if 'trigger' in pc['osso'].lower() or 'gatilho' in pc['osso'].lower()}
        self.tocar('parado', 0)
        self.butt_local = None
        self._medir_mao_fp()

    # -------------------------------------------------------- tocar

    def clipes(self):
        return self.ficha['animacoes']

    def quadros(self, clipe):
        a = self.ficha['animacoes'][clipe]
        return int(round((a['ate'] - a['de']) * FPS))

    def tocar(self, clipe, quadro):
        a = self.ficha['animacoes'][clipe]
        acao = self.acoes[a['clipe']]
        if self.ad.action != acao:
            self.ad.action = acao
            if acao.slots:
                self.ad.action_slot = acao.slots[0]
        t = a['de'] * FPS + quadro
        bpy.context.scene.frame_set(int(math.floor(t)), subframe=t - math.floor(t))
        self.congelar()

    def colocar(self, mundo):
        self.colocacao = mundo
        self.raiz.matrix_world = mundo @ self.base_raiz
        bpy.context.view_layer.update()
        self.congelar()

    def m(self, osso):
        return self.arm.matrix_world @ self.pb[osso].matrix

    def p(self, osso):
        return self.m(osso).translation

    def rot(self, osso):
        return self.m(osso).to_3x3().normalized()

    def camera(self):
        """A cabeça do animador neste quadro: (inclina, gira, rola) em graus,
        como a primeira pessoa lê (ViewModel.UpdateCameraTrack)."""
        osso = self.ficha.get('camera')
        if not osso or osso not in self.pb:
            return Vector()
        return self.pb[osso].location.copy()

    # -------------------------------------------------------- a arma: peças rígidas

    def _pecas(self):
        """Cada peça da arma (cano e corpo, ferrolho, gatilho, carregador…)
        presa num osso só: uma árvore de busca por peça, no espaço do osso,
        para medir a distância de qualquer ponto à arma em qualquer quadro."""
        self.pecas = []
        mw_arm = self.arm.matrix_world
        for o in self.armas:
            grupos = {g.index: g.name for g in o.vertex_groups}
            por_osso = {}
            for v in o.data.vertices:
                if not v.groups:
                    continue
                g = max(v.groups, key=lambda g: g.weight)
                por_osso.setdefault(grupos[g.group], set()).add(v.index)
            for osso, idx in por_osso.items():
                if osso not in self.arm.data.bones:
                    continue
                bind = (mw_arm @ self.arm.data.bones[osso].matrix_local).inverted() @ o.matrix_world
                mapa = {}
                verts = []
                for i in sorted(idx):
                    mapa[i] = len(verts)
                    verts.append(bind @ o.data.vertices[i].co)
                faces = [[mapa[i] for i in pol.vertices] for pol in o.data.polygons if all(i in mapa for i in pol.vertices)]
                if not faces:
                    continue
                arvore = BVHTree.FromPolygons(verts, faces)
                # alguns pontos da peça, para ver se ela entra no corpo
                passo = max(1, len(verts) // 160)
                amostra = verts[::passo]
                centro = sum(verts, Vector()) / len(verts)
                self.pecas.append(dict(osso=osso, arvore=arvore, amostra=amostra, malha=o.name, centro=centro,
                                       raio=max((v - centro).length for v in verts)))

    def congelar(self):
        """Guarda as matrizes das peças neste quadro (a busca da distância
        roda milhares de vezes por quadro)."""
        self._cache = {}
        for pc in self.pecas:
            m = self.m(pc['osso'])
            esc = m.to_scale().x
            self._cache[pc['osso']] = (m, m.inverted(), m.inverted().transposed().to_3x3(), esc)
            # a esfera da peça, para nem procurar nas que estão longe
            pc['esfera'] = (m @ pc['centro'], pc['raio'] * esc)

    def distancia(self, p, so=None, teto=None):
        """A distância (m, com sinal: negativa dentro) do ponto p à arma, e
        a normal da superfície mais perto. Com `teto` (m), só procura até
        ali (bem mais rápido) e devolve o teto se não há nada."""
        melhor = None
        for pc in self.pecas:
            if so is not None and pc['osso'] not in so:
                continue
            m, mi, nt, esc = self._cache[pc['osso']]
            if teto is None:
                r = pc['arvore'].find_nearest(mi @ p)
            else:
                c, raio = pc['esfera']
                if (p - c).length - raio > teto:
                    continue
                r = pc['arvore'].find_nearest(mi @ p, teto / esc)
            if r[0] is None:
                continue
            loc = m @ r[0]
            nor = (nt @ r[1]).normalized()
            d = (p - loc).length
            s = d if (p - loc).dot(nor) >= 0 else -d
            # longe da superfície, a normal do lado de lá engana (peça fina,
            # quina): um dedo não entra 2 cm numa arma sem os vizinhos
            # entrarem junto
            if s < -0.02:
                s = d
            if melhor is None or abs(s) < abs(melhor[0]):
                melhor = (s, nor, loc)
        return melhor if melhor else (1.0 if teto is None else teto, Vector((0, 0, 1)), p)

    def raio(self, p, direcao, alcance=0.06):
        """Quanto falta (m) do ponto p, andando na direção dada, até
        bater na arma; None se não bate até `alcance`."""
        melhor = None
        for pc in self.pecas:
            m, mi, nt, _ = self._cache[pc['osso']]
            o = mi @ p
            d = (mi.to_3x3() @ direcao)
            escala = d.length
            if escala < 1e-9:
                continue
            r = pc['arvore'].ray_cast(o, d / escala, alcance * escala)
            if r[0] is None:
                continue
            dist = (m @ r[0] - p).length
            if melhor is None or dist < melhor:
                melhor = dist
        return melhor

    def firme(self, forma=None):
        """A arma é uma peça só fechada no punho (a faca)? Aí a mão do
        personagem fica presa na própria peça: na primeira pessoa a faca
        anda até 1,7 cm dentro da mão nos golpes e no saque (o ik_hand_gun
        fica, a lâmina vai), e o personagem a segura firme."""
        return forma == 'punho' and len(self.pecas) == 1

    def ref_da_mao(self, forma=None):
        """O quadro (matriz no mundo) em relação ao qual a mão segura a
        arma: o ik_hand_gun, ou a própria peça (veja firme)."""
        return self.m(self.pecas[0]['osso'] if self.firme(forma) else 'ik_hand_gun')

    def corta(self, a, b):
        """A arma atravessa o segmento de a até b? Devolve quanto de b (m)
        fica do lado de lá da superfície que ele fura; None se não fura. É
        como se vê que a arma entra num dedo (do osso até a pele): o sinal
        da distância engana nas peças ocas e vazadas (o guarda-mão)."""
        d = b - a
        comp = d.length
        if comp < 1e-7:
            return None
        meio = (a + b) * 0.5
        fundo = None
        for pc in self.pecas:
            c, raio = pc['esfera']
            if (meio - c).length - raio > comp * 0.5:
                continue
            m, mi, nt, esc = self._cache[pc['osso']]
            o = mi @ a
            dl = mi.to_3x3() @ d
            r = pc['arvore'].ray_cast(o, dl.normalized(), dl.length)
            if r[0] is None:
                continue
            resto = comp - (m @ r[0] - a).length
            if fundo is None or resto > fundo:
                fundo = resto
        return fundo

    def ate_o_carregador(self, ponto):
        """Quanto falta (m), andando para trás ao longo do cano, de `ponto`
        até a frente do carregador (na altura do ponto, não a ponta de
        baixo, que nos curvos fica mais à frente); None se a arma não tem."""
        o, frente, _, cima = self.eixos_arma()
        z = (ponto - o).dot(cima)
        ts = []
        for pc in self.pecas:
            if 'mag' not in pc['osso'].lower() and 'carregador' not in pc['malha'].lower():
                continue
            m = self.m(pc['osso'])
            for v in pc['amostra']:
                w = m @ v - o
                if w.dot(cima) > z - 0.05:
                    ts.append(w.dot(frente))
        if not ts:
            return None
        return (ponto - o).dot(frente) - max(ts)

    def pontos_arma(self):
        """Pontos da arma no mundo (amostra), para ver se entra no corpo."""
        pts = []
        for pc in self.pecas:
            m = self.m(pc['osso'])
            pts.extend(m @ v for v in pc['amostra'])
        return pts

    # -------------------------------------------------------- a arma: eixos e pontos

    def eixos_arma(self):
        """A frente (o cano), o lado e o cima da arma agora, e a origem (o
        osso da arma)."""
        g = self.m('ik_hand_gun')
        o = g.translation.copy()
        r = g.to_3x3().normalized()
        if 'cano' in self.ficha and self.arm_cano_local is not None:
            boca = g @ self.arm_cano_local
            frente = (boca - o).normalized()
        else:
            frente = self.colocacao.to_3x3().normalized() @ Vector((0, 1, 0))
        cima = r @ self.cima_local
        cima = (cima - frente * cima.dot(frente)).normalized()
        return o, frente, frente.cross(cima), cima

    def preparar_eixos(self):
        """No "parado" (o conjunto sem colocar): o cano ("cano" da ficha,
        no espaço do olho do jogo: x, y, z → x, −z, y) e o cima da arma, no
        espaço do osso da arma; e a coronha (o fim de trás)."""
        self.colocar(Matrix.Identity(4))
        self.tocar('parado', 0)
        bpy.context.view_layer.update()
        g = self.m('ik_hand_gun')
        self.arm_cano_local = None
        if 'cano' in self.ficha:
            c = self.ficha['cano']
            boca = Vector((c[0], -c[2], c[1])) * self.escala
            if (boca - g.translation).length > 0.05:
                self.arm_cano_local = g.inverted() @ boca
        o = g.translation
        frente = (g @ self.arm_cano_local - o).normalized() if self.arm_cano_local is not None else Vector((0, 1, 0))
        eixos = [(g.to_3x3().normalized() @ v) for v in (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))]
        eixos += [-e for e in eixos]
        cima = max((e for e in eixos if abs(e.dot(frente)) < 0.5), key=lambda e: e.dot(Vector((0, 0, 1))))
        self.cima_local = g.to_3x3().normalized().inverted() @ cima
        # a coronha: o fim de trás da arma (os pontos mais para trás), no
        # espaço do osso que a leva
        pts = []
        for pc in self.pecas:
            m = self.m(pc['osso'])
            pts.extend((m @ v, pc['osso']) for v in pc['amostra'])
        ts = [((p - o).dot(frente), p, osso) for p, osso in pts]
        fim = min(t for t, _, _ in ts)
        atras = [(p, osso) for t, p, osso in ts if t < fim + 0.02]
        osso = max(set(x for _, x in atras), key=lambda x: sum(1 for _, y in atras if y == x))
        centro = sum((p for p, x in atras if x == osso), Vector()) / sum(1 for _, x in atras if x == osso)
        self.coronha_osso = osso
        self.coronha_local = self.m(osso).inverted() @ centro

    def coronha(self):
        return self.m(self.coronha_osso) @ self.coronha_local

    # -------------------------------------------------------- as mãos de lá

    def _medir_mao_fp(self):
        """A espessura da palma da mão da primeira pessoa (da linha dos
        ossos à pele), para a mão do personagem (mais grossa) encostar no
        mesmo lugar; e a ponta de cada dedo de lá (o meio da pele da última
        falange, no espaço do osso dela: o eixo do osso da ponta, no .glb,
        não diz para onde a falange vai)."""
        self.espessura = {'Right': 0.012, 'Left': 0.012}
        self.ponta_local = {}
        if not self.bracos:
            return
        o = self.bracos[0]
        grupos = {g.index: g.name for g in o.vertex_groups}
        mw_arm = self.arm.matrix_world
        somas = {}
        for v in o.data.vertices:
            if not v.groups:
                continue
            g = max(v.groups, key=lambda g: g.weight)
            nome = grupos.get(g.group, '')
            if nome.endswith(('_03_l', '_03_r')) and g.weight > 0.5:
                bind = (mw_arm @ self.arm.data.bones[nome].matrix_local).inverted() @ o.matrix_world
                p = bind @ v.co
                s = somas.setdefault(nome, [Vector(), 0, []])
                s[0] += p
                s[1] += 1
                s[2].append(p)
        for nome, (soma, n, pts) in somas.items():
            centro = soma / n
            # a ponta: o mais longe da junta, na direção do meio da pele
            eixo = centro.normalized() if centro.length > 1e-6 else Vector((0, 1, 0))
            longe = max(p.dot(eixo) for p in pts)
            self.ponta_local[nome] = eixo * longe * 0.8
        o = self.bracos[0]
        dg = bpy.context.evaluated_depsgraph_get()
        e = o.evaluated_get(dg)
        malha = e.to_mesh()
        grupos = {g.index: g.name for g in o.vertex_groups}
        for lado, s in (('Right', 'r'), ('Left', 'l')):
            mao = self.mao(lado)
            a, b = mao['pulso'], mao['meio']
            eixo = (b - a).normalized()
            esp = []
            for v, vo in zip(malha.vertices, o.data.vertices):
                if not vo.groups:
                    continue
                g = max(vo.groups, key=lambda g: g.weight)
                if grupos.get(g.group) != f'hand_{s}':
                    continue
                p = o.matrix_world @ v.co
                t = (p - a).dot(eixo)
                if 0.3 < t / (b - a).length < 0.9:
                    d = (p - a - eixo * t).dot(mao['dentro'])
                    if d > 0:
                        esp.append(d)
            esp.sort()
            if esp:
                self.espessura[lado] = esp[int(len(esp) * 0.8)]
        e.to_mesh_clear()

    def mao(self, lado):
        """A mão de lá no mundo: pulso, começo do médio, dedos, palma e as
        falanges (a direção de cada uma)."""
        s = 'r' if lado == 'Right' else 'l'
        pulso = self.p(f'hand_{s}')
        meio = self.p(f'middle_01_{s}')
        indic = self.p(f'index_01_{s}')
        minimo = self.p(f'pinky_01_{s}')
        ao_longo = meio - pulso
        travessa = indic - minimo
        dentro = travessa.cross(ao_longo) if lado == 'Right' else ao_longo.cross(travessa)
        dedos = []
        juntas = []
        for d in DEDOS_FP:
            a, b, c = (self.m(f'{d}_0{n}_{s}') for n in (1, 2, 3))
            pl = self.ponta_local.get(f'{d}_03_{s}')
            if pl is not None:
                ultimo = ((c @ pl) - c.translation).normalized()
            else:
                ultimo = (c.to_3x3() @ Vector((0, 1, 0))).normalized()
            dedos.append([(b.translation - a.translation).normalized(), (c.translation - b.translation).normalized(), ultimo])
            juntas.append([a.translation.copy(), b.translation.copy(), c.translation.copy(),
                           c.translation + ultimo * (c.translation - b.translation).length * 0.85])
        # o punho de lá: o antebraço em relação à mão (graus)
        A, N = ao_longo.normalized(), dentro.normalized()
        Cx = A.cross(N)
        Yf = (pulso - self.p(f'lowerarm_{s}')).normalized()
        dobra = deg(math.atan2(Yf.dot(N), Yf.dot(A)))
        lado_ = deg(math.atan2(Yf.dot(Cx), Yf.dot(A)))
        return dict(pulso=pulso, meio=meio, ao_longo=A, dentro=N,
                    travessa=travessa.normalized(), dedos=dedos, juntas=juntas, comprimento=ao_longo.length,
                    palma=pulso.lerp(meio, ALINHA), dobra=dobra, lado=lado_)


# ------------------------------------------------------------------ a mão do corpo no lugar da de lá

def alvo_mao(corpo, rig, lado, fp):
    """O pulso e a orientação da mão do corpo (armadura) para a palma dela
    ficar onde a de lá está: os dedos e a palma para o mesmo lado, o começo
    dos dedos no mesmo ponto, a pele da palma na mesma altura (a do
    personagem é mais grossa)."""
    m = corpo.mao[lado]
    rot = orient(fp['ao_longo'], fp['dentro']) @ orient(m['dedos'], m['palma']).inverted()
    ponto = fp['pulso'].lerp(fp['meio'], ALINHA)
    # a pele da palma: a do personagem é mais grossa, a mão sai da arma
    ponto = ponto - fp['dentro'] * (m['espessura'] - rig.espessura[lado])
    pulso = ponto - rot @ (m['nos'] * ALINHA)
    return pulso, rot


def pele_palma(corpo, lado):
    """A pele da mão sem os dedos (os pontos da malha que o osso da mão
    leva, e o começo de cada dedo), no espaço do osso: cada ponto, a normal
    dele e até onde se olha para dentro (a sonda: menos da metade da
    grossura da mão ali — mais que isso sairia do outro lado e acharia a
    arma de fora)."""
    if not hasattr(corpo, '_pele_palma'):
        corpo._pele_palma = {}
        corpo._sonda_vert = {}
    if lado not in corpo._pele_palma:
        osso = corpo.arm.data.bones[corpo.n(lado + 'Hand')]
        volta = osso.matrix_local.inverted()
        volta3 = volta.to_3x3()
        ossos = corpo.arm.data.bones
        nos = {corpo.n(f'{lado}Hand{d}1'): (ossos[corpo.n(f'{lado}Hand{d}1')].head_local, ossos[corpo.n(f'{lado}Hand{d}2')].head_local)
               for d in DEDOS}
        teto = corpo.mao[lado]['espessura'] * 0.8
        pts = []
        for o in corpo.malhas:
            grupos = {g.index: g.name for g in o.vertex_groups}
            if osso.name not in grupos.values():
                continue
            mw = o.matrix_world
            r3 = mw.to_3x3()
            arvore = BVHTree.FromPolygons([mw @ v.co for v in o.data.vertices], [list(pol.vertices) for pol in o.data.polygons])
            for v in o.data.vertices:
                if not v.groups:
                    continue
                g = max(v.groups, key=lambda g: g.weight)
                nome = grupos.get(g.group)
                w = mw @ v.co
                if nome in nos:
                    # o começo de cada dedo (o nó) vai com a mão: se a mão
                    # fica onde o nó entra na arma, nenhuma dobra do dedo o tira
                    a_, b_ = nos[nome]
                    if (w - a_).dot(b_ - a_) / max((b_ - a_).length_squared, 1e-10) >= 0.45:
                        continue
                elif nome != osso.name:
                    continue
                nw = (r3 @ v.normal).normalized()
                do_outro_lado = arvore.ray_cast(w - nw * 0.001, -nw, 0.2)
                grossura = do_outro_lado[3] if do_outro_lado[0] is not None else 2 * teto
                sonda = max(0.004, min(teto, grossura * 0.45))
                pts.append((volta @ w, (volta3 @ nw).normalized(), sonda))
                corpo._sonda_vert[(o.name, v.index)] = sonda
        corpo._pele_palma[lado] = pts
    return corpo._pele_palma[lado]


def palma_dentro(corpo, rig, lado, pulso, rot):
    """O ponto da pele da mão (sem os dedos) que mais entra na arma: quanto
    entra (m) e a normal dele no mundo; (0, None) se nenhum entra. Olha-se
    da carne para a pele: a arma passa logo abaixo da pele?"""
    pior = (0.0, None)
    for p, n, sonda in pele_palma(corpo, lado):
        w = pulso + rot @ p
        nw = rot @ n
        fura = rig.corta(w - nw * sonda, w)
        if fura is not None and fura > pior[0]:
            pior = (fura, nw)
    return pior


def afastar_palma(corpo, rig, lado, pulso, rot, fp=None, encostar=False, aninhar=False, apoio=False):
    """A palma do personagem na arma (Jhon, 22/11: "não tá encostando a arma
    na palma da mão, tá segurando com os dedos"; "nada pode atravessar
    outros materiais"): a pele da mão — a malha dela, não uma grade — não
    entra na arma: sai, ponto por ponto, na direção de cada um que entra,
    até nenhum entrar. E, quando a palma de lá está na arma, também não
    fica longe: desce até encostar. Os dedos fecham depois."""
    m = corpo.mao[lado]
    n = (rot @ m['palma']).normalized()
    if aninhar:
        # Um cabo fino (a faca) fica na raiz dos dedos, onde a mão fecha em
        # volta dele — no meio da palma os dedos fechavam no ar, com um vão
        # entre eles e o cabo (Jhon: "da faca pode grudar mais"). A mão anda
        # ao longo dos dedos até o cabo ficar logo antes dos nós.
        ao_longo = (rot @ m['dedos']).normalized()
        comp = m['nos'].length
        meio_palma = pulso + rot @ (m['nos'] * ALINHA) + n * m['espessura']
        cabo = rig.distancia(meio_palma)[2]
        # ...e ao longo do cabo (a linha dos nós) até o indicador ficar
        # logo atrás da guarda: a mão ficava em cima dela, fechada em volta
        # do disco e não do cabo.
        tr = (rot @ m['travessa']).normalized()
        perfil = []
        for k in range(-30, 31):
            c = cabo + tr * (k * 0.004)
            raio = None
            for dv in (n, -n, ao_longo, -ao_longo):
                h = rig.raio(c + dv * 0.06, -dv, 0.06)
                if h is not None and (raio is None or 0.06 - h > raio):
                    raio = 0.06 - h
            perfil.append(raio)
        perto = sorted(r for r in perfil[25:36] if r is not None)
        if perto:
            fino = perto[len(perto) // 2]
            guarda = None
            for k in range(30, 61):
                if perfil[k] is not None and perfil[k] > fino + 0.008:
                    guarda = (k - 30) * 0.004
                    break
            if guarda is not None:
                ossos = corpo.arm.data.bones
                borda = (ossos[corpo.n(lado + 'HandIndex1')].head_local - ossos[corpo.n(lado + 'HandMiddle1')].head_local).length \
                    + corpo.raio_dedo[lado + 'Index']
                pulso = pulso + tr * max(-0.08, min(0.08, guarda - 0.004 - borda))
                meio_palma = pulso + rot @ (m['nos'] * ALINHA) + n * m['espessura']
                cabo = rig.distancia(meio_palma)[2]
        anda = max(-0.03, min(0.03, (cabo - pulso).dot(ao_longo) - comp * 0.93))
        pulso = pulso + ao_longo * anda
    # Enquanto a mão de lá está no mesmo lugar da arma (parado, andando,
    # mirando), o acerto é o do primeiro quadro: decidido de novo a cada
    # quadro, a mão tremia.
    guardado = None
    if fp is not None:
        if not hasattr(corpo, '_palma_guardada'):
            corpo._palma_guardada = {}
        na_arma = rig.m('ik_hand_gun').inverted() @ fp['palma']
        g = corpo._palma_guardada.get(lado)
        if g is not None and (g[0] - na_arma).length < 0.004 * rig.escala:
            guardado = g
            tentativa = pulso + rot @ g[1]
            if palma_dentro(corpo, rig, lado, tentativa, rot)[1] is None:
                return tentativa
    andou = Vector()
    for _ in range(10):
        fundo, normal = palma_dentro(corpo, rig, lado, pulso + andou, rot)
        if normal is None or andou.length > 0.05:
            break
        andou -= normal * (fundo + 0.0012)
    antes = pulso
    if palma_dentro(corpo, rig, lado, pulso + andou, rot)[1] is not None:
        # os pulos não tiraram: sai reto, pela palma, até nada entrar
        for passo in range(1, 15):
            if palma_dentro(corpo, rig, lado, pulso - n * (passo * 0.003), rot)[1] is None:
                andou = -n * (passo * 0.003)
                break
    if andou.length > 1e-6:
        # saiu aos pulos, um ponto de cada vez: volta o que der pelo mesmo
        # caminho (a mão não fica no ar, longe da arma)
        a_, b_ = 0.0, 1.0
        for _ in range(6):
            meio = (a_ + b_) * 0.5
            if palma_dentro(corpo, rig, lado, pulso + andou * meio, rot)[1] is None:
                b_ = meio
            else:
                a_ = meio
        andou = andou * b_
    pulso = pulso + andou
    if fp is not None:
        pele = fp['palma'] + fp['dentro'] * rig.espessura[lado]
        # (encostar: a mão fechada num cabo — faca, granada — sempre desce
        # até a palma encostar nele)
        # (apoio: a mão de apoio no guarda-mão. Lá o meio da palma fica a
        # 2-3 cm do fuzil — ele assenta na raiz dos dedos, de través — e
        # aqui, com a mão girada para o braço do personagem, ficava a palma
        # inteira a 1-2 cm, a arma segura só com a ponta dos dedos (Jhon:
        # "não tá encostando a arma na palma da mão"). Desce sempre que a mão
        # de lá está no guarda-mão; saindo dele (a recarga), cada vez menos.)
        longe_fp = abs(rig.distancia(pele)[0])
        peso_desce = 1.0 if (encostar or longe_fp < 0.02) else (
            max(0.0, min(1.0, (0.055 - longe_fp) / 0.02)) if apoio else 0.0)
        inicio_desce = pulso.copy()
        if peso_desce > 0.0:
            # Desce aos poucos até a palma encostar. Se no caminho outro
            # ponto da mão (o lado dela, o nó de um dedo) bate na arma, a
            # mão escorrega de lado para longe dele e continua descendo —
            # parava ali, e a mão de apoio da Eve ficava 3 cm abaixo do
            # guarda-mão do SCAR, com os dedos no ar.
            frente_palma = [p for p, nl, _ in pele_palma(corpo, lado) if nl.dot(m['palma']) > 0.6]
            escorregou = 0.0
            for _ in range(30):
                perto = [d for d in (rig.raio(pulso + rot @ p, n, 0.07) for p in frente_palma) if d is not None]
                if not perto or min(perto) <= 0.002:
                    break
                tent = pulso + n * min(min(perto) - 0.0015, 0.004)
                fundo, normal = palma_dentro(corpo, rig, lado, tent, rot)
                if normal is None:
                    pulso = tent
                    continue
                de_lado = normal - n * normal.dot(n)
                if de_lado.length < 0.35 or escorregou > 0.03:
                    break
                tent = tent - de_lado.normalized() * (fundo + 0.001)
                if palma_dentro(corpo, rig, lado, tent, rot)[1] is not None:
                    break
                escorregou += fundo + 0.001
                pulso = tent
            if peso_desce < 1.0:
                pulso = inicio_desce.lerp(pulso, peso_desce)
    if fp is not None:
        corpo._palma_guardada[lado] = (na_arma if guardado is None else guardado[0], rot.inverted() @ (pulso - antes))
    return pulso


def afastar_da_outra(corpo, rig, lado, pulso, rot, capsulas, folga=0.001):
    """A mão que abraça a outra (a de apoio na pistola) com a palma fora
    dos dedos dela: a mão do personagem é mais grossa que a de lá, e posta
    onde a de lá fica a palma entrava nos dedos da mão do gatilho — os dedos
    desta, sem ter como fechar por dentro dos outros, ficavam abertos no ar
    (Jhon, 23/11: "não ficou bom"). Recua pela normal da palma até a pele
    dela sair dos dedos já postos (`capsulas`: Dedos.feitos)."""
    m = corpo.mao[lado]
    n = (rot @ m['palma']).normalized()
    pts = [p for p, _, _ in pele_palma(corpo, lado)]
    andou = 0.0
    for _ in range(8):
        pior = 0.0
        for p in pts:
            w = pulso + rot @ p - n * andou
            for a_, ab, ab2, r in capsulas:
                t = max(0.0, min(1.0, (w - a_).dot(ab) / ab2))
                dentro = r - (w - a_ - ab * t).length
                if dentro > pior:
                    pior = dentro
        if pior <= 0.0 or andou > 0.04:
            break
        andou += pior + folga
    pulso = pulso - n * andou

    def livre(p_):
        for p in pts:
            w = p_ + rot @ p
            for a_, ab, ab2, r in capsulas:
                t = max(0.0, min(1.0, (w - a_).dot(ab) / ab2))
                if (w - a_ - ab * t).length < r + folga:
                    return False
        return palma_dentro(corpo, rig, lado, p_, rot)[1] is None

    # ...e encosta: a palma vem pela normal dela até tocar os dedos da outra
    # mão ou o cabo (a de lá fica onde os dedos mais finos de lá deixam; a
    # daqui ficava a 2-3 cm, com os dedos fechando no ar ao lado da pistola)
    if andou <= 0.0:
        perto = 0.0
        for passo in range(1, 41):
            if not livre(pulso + n * (passo * 0.002)):
                break
            perto = passo * 0.002
        if perto < 0.08:
            a_, b_ = perto, perto + 0.002
            for _ in range(3):
                meio = (a_ + b_) * 0.5
                if livre(pulso + n * meio):
                    a_ = meio
                else:
                    b_ = meio
            perto = a_
        else:
            perto = 0.0   # nada no caminho: fica onde a de lá está
        pulso = pulso + n * perto
        andou = -perto
    return pulso, andou


def folga_palma(corpo, rig, lado, M):
    """A pele da mão (sem os dedos) na arma: o quanto entra (m, negativo) ou
    a que distância a palma fica dela."""
    m = corpo.mao[lado]
    rot = M[corpo.n(lado + 'Hand')].to_3x3().normalized()
    pulso = M[corpo.n(lado + 'Hand')].translation
    fundo, normal = palma_dentro(corpo, rig, lado, pulso, rot)
    if normal is not None:
        return -fundo
    return min((abs(rig.distancia(pulso + rot @ p, teto=0.06)[0]) for p, nl, _ in pele_palma(corpo, lado) if nl.dot(m['palma']) > 0.6),
               default=0.06)


# ------------------------------------------------------------------ o jeito de pegar

class Pegada:
    """Como a mão se ajeita na arma para o punho ficar reto no corpo do
    personagem (os ombros dele não estão onde os da primeira pessoa estão):
    a mão gira em volta do que segura — o cabo (a linha dos nós dos dedos)
    ou o guarda-mão (a linha do cano) — e, no guarda-mão, desliza para trás.
    Feito uma vez por postura; nos clipes vale enquanto a mão de lá está
    onde estava em relação à arma (na recarga a esquerda sai, e o ajuste
    some aos poucos)."""

    def __init__(self, rig, lado, fp, jeito):
        self.lado = lado
        self.jeito = jeito
        g = rig.m('ik_hand_gun')
        gi = g.inverted()
        P = fp['palma']
        if jeito == 'guarda':
            o, frente, _, _ = rig.eixos_arma()
            boca = g @ rig.arm_cano_local if rig.arm_cano_local is not None else o + frente
            centro = boca + frente * (P - boca).dot(frente)
            eixo = frente
        else:
            centro = P + fp['dentro'] * (rig.espessura[lado] + 0.018)
            eixo = fp['ao_longo'].cross(fp['dentro']).normalized()
        self.centro_local = gi @ centro
        self.eixo_local = (gi.to_3x3() @ eixo).normalized()
        self.palma_local = gi @ P
        self.angulo = 0.0
        self.guina = 0.0
        self.desliza = 0.0

    def agora(self, rig):
        g = rig.m('ik_hand_gun')
        centro = g @ self.centro_local
        eixo = (g.to_3x3() @ self.eixo_local).normalized()
        return centro, eixo, g

    def peso(self, rig, fp):
        """1 enquanto a mão de lá está onde estava na arma; some até 9 cm."""
        g = rig.m('ik_hand_gun')
        d = (g @ self.palma_local - fp['palma']).length
        return 1.0 - suave((d - 0.03) / 0.06)

    def aplicar(self, rig, fp, pulso, rot, angulo=None, desliza=None, peso=None, guina=None, mao=None):
        ang = self.angulo if angulo is None else angulo
        s = self.desliza if desliza is None else desliza
        gu = self.guina if guina is None else guina
        w = self.peso(rig, fp) if peso is None else peso
        if w <= 1e-4 or (abs(ang) < 1e-6 and abs(s) < 1e-6 and abs(gu) < 1e-6):
            return pulso, rot
        centro, eixo, g = self.agora(rig)
        q = Quaternion(eixo, ang * w)
        pulso = centro + q @ (pulso - centro)
        rot = q.to_matrix() @ rot
        if abs(gu) > 1e-6 and mao is not None:
            # a mão gira deitada na arma, em volta da normal da palma, no
            # ponto em que a palma encosta
            n = (rot @ mao['palma']).normalized()
            ponto = pulso + rot @ (mao['nos'] * ALINHA)
            qg = Quaternion(n, gu * w)
            pulso = ponto + qg @ (pulso - ponto)
            rot = qg.to_matrix() @ rot
        if s:
            frente = eixo if self.jeito == 'guarda' else rig.eixos_arma()[1]
            pulso = pulso - frente * s * w
        return pulso, rot


def ajustar_pegada(corpo, braco, rig, pegada, M, fp, pref, mao_info, cotovelo_max_z, deslizar, deslize_fixo=None, perto=None):
    """Procura o giro (e o deslize) da mão na arma que deixa o braço do
    personagem mais natural (punho reto, cotovelo no lugar, a mão alcança)."""
    pulso0, rot0 = alvo_mao(corpo, rig, braco.lado, fp)
    melhor = None
    if pegada.jeito == 'guarda':
        angulos = [rad(a) for a in range(-40, 41, 10)]
        guinas = [rad(a) for a in range(-GUINA_APOIO, GUINA_APOIO + 1, 10)]
        deslizes = [x * 0.02 for x in range(-2, 6)] if deslizar else [0.0]
        if deslizar:
            # para trás só até o carregador: a mão fica no guarda-mão, não
            # em cima (nem embaixo) do carregador
            ate = rig.ate_o_carregador(fp['palma'])
            if ate is not None:
                ate = max(0.0, ate - mao_info['largura'] * MAO_NO_CARREGADOR)
                deslizes = [x for x in deslizes if x <= ate + 1e-6] or [0.0]
                if ate not in deslizes and ate < 0.1:
                    deslizes.append(ate)
            if deslize_fixo is not None:
                deslizes = [deslize_fixo]
    else:
        angulos = [rad(a) for a in range(-GIRO_CABO, GIRO_CABO + 1, 5)]
        guinas = [rad(a) for a in range(-15, 16, 5)]
        deslizes = [0.0]
    achados = []
    for s in deslizes:
        for a in angulos:
            for gu in guinas:
                pulso, rot = pegada.aplicar(rig, fp, pulso0, rot0, a, s, 1.0, gu, mao_info)
                r = braco.avaliar(M, pulso, rot, pref, mao_info, cotovelo_max_z, 0.0, passo=7.5)
                custo = r['custo'] + abs(deg(a)) * 0.10 + abs(deg(gu)) * 0.12 + abs(s) * 40
                if perto is not None:
                    # na mira a mão segura a arma como segurava no quadril (na
                    # primeira pessoa os dedos não saem do lugar ao mirar):
                    # só muda de jeito se o braço não der mesmo
                    custo += abs(deg(a - perto[0])) * PERTO_DO_QUADRIL + abs(deg(gu - perto[1])) * PERTO_DO_QUADRIL
                achados.append((custo, a, s, gu, r, pulso, rot))
    # Do melhor para o pior, mais o que a mão entra na arma naquele lugar (a
    # mão de apoio do Barut, deslizada para trás, ficava com o lado da palma
    # dentro da boca do carregador do SCAR); para quando nenhum dos que
    # faltam ganharia.
    achados.sort(key=lambda x: x[0])
    n_palma = mao_info['palma']
    frente_palma = [p for p, nl, _ in pele_palma(corpo, braco.lado) if nl.dot(n_palma) > 0.6]
    for custo, a, s, gu, r, pulso, rot in achados[:400]:
        if melhor is not None and custo >= melhor[0]:
            break
        limpo = custo
        custo += palma_dentro(corpo, rig, braco.lado, pulso, rot)[0] * 300.0
        if pegada.jeito == 'guarda':
            # e a palma tem de poder assentar ali: se uma ponta dela já
            # encosta na arma (a caixa do gatilho, mais baixa que o
            # guarda-mão) e o resto fica longe, a mão ficava no ar
            n = (rot @ n_palma).normalized()
            ds = sorted(d for d in (rig.raio(pulso + rot @ p - n * 0.03, n, 0.10) for p in frente_palma) if d is not None)
            if len(ds) >= 4:
                custo += max(0.0, ds[len(ds) // 2] - ds[0] - 0.008) * 90.0
            else:
                custo += 2.0
        if melhor is None or custo < melhor[0]:
            melhor = (custo, a, s, gu, r, custo - limpo)
    _, a, s, gu, r, assento = melhor
    pegada.angulo, pegada.desliza, pegada.guina = a, s, gu
    return dict(angulo=deg(a), guina=deg(gu), desliza=s, dobra=r['dobra'], lado=r['lado'], giro=r['giro'] * 0.5, longe=r['longe'],
                assento=assento)


# ------------------------------------------------------------------ o braço

class Braco:
    """O braço de um lado, resolvido em conta: a clavícula (se a mão está
    longe), o cotovelo (em volta da linha ombro–pulso, onde o punho fica mais
    reto e o cotovelo num lugar de gente), o antebraço com metade do giro do
    punho, e a mão."""

    def __init__(self, corpo, lado):
        self.corpo = corpo
        self.lado = lado
        self.up = corpo.n(lado + 'Arm')
        self.lo = corpo.n(lado + 'ForeArm')
        self.ha = corpo.n(lado + 'Hand')
        self.cla = corpo.n(lado + 'Shoulder')
        b = corpo.arm.data.bones
        self.a = (b[self.lo].head_local - b[self.up].head_local).length
        self.b = (b[self.ha].head_local - b[self.lo].head_local).length
        self.h_up = corpo.dobra_local[lado + 'Arm']
        self.h_lo = corpo.dobra_local[lado + 'ForeArm']
        rf0 = b[self.lo].matrix_local.to_3x3()
        rh0 = b[self.ha].matrix_local.to_3x3()
        self.rel0 = rf0.inverted() @ rh0
        self.theta = None
        # Quanto custa (por grau) o braço de cima girar em volta do próprio eixo em
        # relação ao repouso (a pose T): a pele do ombro do personagem não tem osso
        # de torção e dobra quando o úmero gira (Jhon, 23/11: "imagem 1 torcida").
        # 0 = não conta (o animacoes_armas.py); o mao_manny_anim.py liga.
        self.peso_torcao = 0.0
        self.torcao = 0.0
        # quanto da torção não custa (graus): o úmero gira de verdade até uns 70°
        self.torcao_livre = 0.0
        # quanto custa (por grau) o cotovelo longe do lado preferido (pref)
        self.peso_cotovelo = 0.08

    def torcao_do_ombro(self, M, Yu, H):
        """O quanto (graus) o braço de cima, com o eixo Yu e a dobra do cotovelo
        em H, está girado em volta de si em relação a só ter balançado do repouso."""
        base = self.corpo.esq.base(self.up, M).to_3x3().normalized()
        yb = (base @ Vector((0, 1, 0))).normalized()
        balanco = yb.rotation_difference(Yu).to_matrix() @ base
        agora = assentar_rot(Yu, H, self.h_up)
        d = (balanco.inverted() @ agora).to_quaternion()
        ang = deg(2 * math.atan2(d.y, d.w))
        return (ang + 180.0) % 360.0 - 180.0

    def alcance(self):
        return self.a + self.b

    def avaliar(self, M, pulso, rot_mao, pref, mao_info, cotovelo_max_z=0.03, peso_tempo=1.0, theta=None, passo=3.75,
                alvo=(0.0, 0.0)):
        """O melhor cotovelo para a mão em (pulso, rot_mao) e quanto custa
        (punho torto, cotovelo fora de lugar, mão que não alcança)."""
        corpo = self.corpo
        S = M[self.up].translation.copy()
        a, b = self.a, self.b
        d = pulso - S
        longe = max(0.0, d.length - (a + b) * 0.995)
        dist = min(d.length, (a + b) * 0.9995)
        dist = max(dist, abs(a - b) + 1e-3)
        direc = d.normalized()
        cos_a = (a * a + dist * dist - b * b) / (2 * a * dist)
        alfa = math.acos(max(-1.0, min(1.0, cos_a)))
        C = S + direc * math.cos(alfa) * a
        raio = a * math.sin(alfa)
        fora = DIREITA if self.lado == 'Right' else -DIREITA
        prefv = fora * pref[0] + CIMA * pref[1] - FRENTE * pref[2]
        u = prefv - direc * prefv.dot(direc)
        if u.length < 1e-4:
            u = CIMA.cross(direc)
        u.normalize()
        v = direc.cross(u)
        W = S + direc * dist
        A = (rot_mao @ mao_info['dedos']).normalized()
        N = (rot_mao @ mao_info['palma']).normalized()
        Cx = A.cross(N)
        meio_tronco = M[corpo.n('Spine1')].translation
        topo_tronco = M[corpo.n('Neck')].translation

        def metr(E):
            Yu = (E - S).normalized()
            Yf = (W - E).normalized()
            dobra = math.atan2(Yf.dot(N), Yf.dot(A))
            lado_ = math.atan2(Yf.dot(Cx), Yf.dot(A))
            return Yu, Yf, dobra, lado_

        melhor = None
        n_passos = int(round(180 / passo))
        for i in range(-n_passos, n_passos + 1):
            th = i * passo
            E = C + (u * math.cos(rad(th)) + v * math.sin(rad(th))) * raio
            Yu, Yf, dobra, lado_ = metr(E)
            # o punho como o da primeira pessoa (o animador deixou o punho
            # como a ação pede: o pulso dobrado ao empurrar o carregador),
            # e nunca além do que é confortável
            dd, dl = abs(deg(dobra) - alvo[0]), abs(deg(lado_) - alvo[1])
            custo = max(0.0, dd - 8) ** 2 * 0.06 + max(0.0, dl - 6) ** 2 * 0.08 + abs(th) * self.peso_cotovelo
            custo += max(0.0, abs(deg(dobra)) - PUNHO_MAX[0]) ** 2 * 0.3 + max(0.0, abs(deg(lado_)) - PUNHO_MAX[1]) ** 2 * 0.4
            # o giro do punho que sobra (metade vai para o antebraço)
            H = Yu.cross(Yf)
            if H.length < 1e-5:
                H = Yu.cross(u)
            rf = assentar_rot(Yf, H, self.h_lo)
            rel = rf.inverted() @ rot_mao
            delta = (rel @ self.rel0.inverted()).to_quaternion()
            if delta.w < 0:
                delta.negate()
            giro = deg(2 * math.atan2(delta.y, delta.w))
            if giro > 180:
                giro -= 360
            if giro < -180:
                giro += 360
            custo += max(0.0, abs(giro) - 50) ** 2 * 0.02
            if self.peso_torcao:
                custo += max(0.0, abs(self.torcao_do_ombro(M, Yu, H)) - self.torcao_livre) * self.peso_torcao
            # cotovelo acima do ombro
            if E.z > S.z + cotovelo_max_z:
                custo += (E.z - S.z - cotovelo_max_z) * 900
            # cotovelo dentro do tronco
            eixo_t = (topo_tronco - meio_tronco)
            tt = max(0.0, min(1.0, (E - meio_tronco).dot(eixo_t) / eixo_t.length_squared))
            perto = (E - (meio_tronco + eixo_t * tt)).length
            if perto < 0.19:
                custo += (0.19 - perto) * 1500
            # muito para trás das costas
            if E.y > S.y + 0.12:
                custo += (E.y - S.y - 0.12) * 500
            antes = theta if theta is not None else self.theta
            if antes is not None:
                custo += ((th - antes) ** 2) * 0.02 * peso_tempo
            if melhor is None or custo < melhor[0]:
                melhor = (custo, th, E, giro)
        custo, th, E, giro = melhor
        Yu, Yf, dobra, lado_ = metr(E)
        H = Yu.cross(Yf)
        if H.length < 1e-5:
            H = Yu.cross(u)
        return dict(custo=custo + longe * 4000, th=th, E=E, giro=giro, Yu=Yu, Yf=Yf, H=H,
                    dobra=deg(dobra), lado=deg(lado_), longe=longe)

    def resolver(self, M, locais, pulso, rot_mao, pref, mao_info, cotovelo_max_z=0.03, peso_tempo=1.0, alvo=(0.0, 0.0)):
        esq = self.corpo.esq
        r = self.avaliar(M, pulso, rot_mao, pref, mao_info, cotovelo_max_z, peso_tempo, alvo=alvo)
        self.theta = r['th']
        Yu, Yf, H, giro = r['Yu'], r['Yf'], r['H'], r['giro']
        esq.por(M, locais, self.up, assentar_rot(Yu, H, self.h_up))
        rf = assentar_rot(Yf, H, self.h_lo)
        esq.por(M, locais, self.lo, rf)
        # metade do giro do punho no antebraço
        rf2 = Quaternion(Yf, rad(giro) * 0.5).to_matrix() @ rf
        esq.por(M, locais, self.lo, rf2)
        esq.por(M, locais, self.ha, rot_mao)
        falta = (M[self.ha].translation - pulso).length
        return dict(dobra=r['dobra'], lado=r['lado'], giro=giro * 0.5, falta=falta, cotovelo=r['E'], custo=r['custo'])


# O punho de uma mão sem arma (ou com a granada, que é uma bola na palma):
# a mão gira em volta do pulso até o punho ficar no que ele dobra (graus;
# o giro é o que sobra entre o antebraço e a mão, metade vai no antebraço).
LIMITE_PUNHO = dict(dobra=(-60.0, 60.0), lado=(-25.0, 25.0), giro=80.0)


def limitar_punho(br, M, pulso, rot, pref, mao_info, alto, peso_tempo, alvo, lim=LIMITE_PUNHO):
    for _ in range(5):
        r = br.avaliar(M, pulso, rot, pref, mao_info, alto, peso_tempo, passo=7.5, alvo=alvo)
        d, l, g = r['dobra'], r['lado'], r['giro']
        dd = d - max(lim['dobra'][0], min(lim['dobra'][1], d))
        dl = l - max(lim['lado'][0], min(lim['lado'][1], l))
        dg = g - max(-lim['giro'], min(lim['giro'], g))
        if abs(dd) < 0.5 and abs(dl) < 0.5 and abs(dg) < 0.5:
            break
        A = (rot @ mao_info['dedos']).normalized()
        N = (rot @ mao_info['palma']).normalized()
        Cx = A.cross(N).normalized()
        # dobrar em volta do eixo do lado da mão tira `dd` da dobra; em volta
        # da normal da palma, `dl` do lado; em volta do antebraço, `dg` do giro
        R = Quaternion(Cx, rad(dd)) @ Quaternion(N, rad(-dl))
        if abs(dd) < 0.5 and abs(dl) < 0.5:
            R = Quaternion(r['Yf'], rad(-dg))
        rot = R.to_matrix() @ rot
    return rot


# ------------------------------------------------------------------ os dedos

class Dedos:
    """Os dedos de uma mão. O esqueleto do personagem não é o de lá (lá
    cada dedo tem o metacarpo e três falanges; aqui, três falanges e a
    ponta, com outros comprimentos), então não se copia osso por osso: cada
    falange parte da direção da de lá (a mão do corpo está alinhada com a de
    lá) e encaixa na arma — encosta onde a de lá encosta, sem entrar
    (_encaixar). As juntas ficam no que uma mão dobra."""

    def __init__(self, corpo, lado):
        self.corpo = corpo
        self.lado = lado
        b = corpo.arm.data.bones
        self.ossos = {d: [corpo.n(f'{lado}Hand{d}{k}') for k in (1, 2, 3, 4)] for d in DEDOS}
        # a direção de repouso de cada falange proximal, no espaço da mão
        mao = b[corpo.n(lado + 'Hand')].matrix_local.to_3x3()
        self.repouso_base = {}
        for d in DEDOS:
            a = b[self.ossos[d][0]].head_local
            c = b[self.ossos[d][1]].head_local
            self.repouso_base[d] = mao.inverted() @ (c - a).normalized()
        self._preparar_pele()
        # o encaixe de cada dedo no quadro anterior (Dedos.resolver)
        self.memoria = {}

    def forma(self, M, nome):
        """Uma forma de mão feita aqui (não a de lá), na mão como ela está:
        "punho" (fechada para segurar um cabo: os dedos juntos, curvando
        por igual nas três juntas, e o contato fecha o resto), "relaxada"
        (a guarda aberta da faca e o braço solto) e "aberta" (os dedos
        esticados e juntos, apontando)."""
        corpo = self.corpo
        lado = self.lado
        rot_mao = M[corpo.n(lado + 'Hand')].to_3x3().normalized()
        A = (rot_mao @ corpo.mao[lado]['dedos']).normalized()
        N = (rot_mao @ corpo.mao[lado]['palma']).normalized()
        curva = {'punho': (40.0, 55.0, 25.0), 'relaxada': (18.0, 32.0, 16.0), 'aberta': (4.0, 6.0, 3.0)}[nome]
        b = corpo.arm.data.bones
        mao0 = b[corpo.n(lado + 'Hand')].matrix_local.to_3x3()
        out = []
        for i, d in enumerate(DEDOS):
            ossos = self.ossos[d]
            pts = [b[o].head_local for o in ossos]
            segs = [(rot_mao @ (mao0.inverted() @ (pts[k + 1] - pts[k]))).normalized() for k in range(3)]
            if i == 0:
                # o polegar: deita na frente dos dedos (punho) ou fica ao
                # lado (aberta, relaxada)
                eixo = A.cross(N) if lado == 'Right' else N.cross(A)
                fecha = {'punho': 0.9, 'relaxada': 0.35, 'aberta': 0.1}[nome]
                segs = [girar_v(s, A, (fecha if lado == 'Right' else -fecha) * (0.6 + 0.2 * k)) for k, s in enumerate(segs)]
                eixo2 = segs[0].cross(N).normalized() if segs[0].cross(N).length > 0.1 else eixo
                segs = [girar_v(s, eixo2, rad(curva[1] * 0.4) * (k + 1) / 3) for k, s in enumerate(segs)]
                out.append(segs)
                continue
            eixo = segs[0].cross(N)
            eixo = eixo.normalized() if eixo.length > 0.1 else A.cross(N).normalized()
            acum = 0.0
            novos = []
            for k in range(3):
                acum += rad(curva[k])
                novos.append(girar_v(segs[k], eixo, acum))
            out.append(novos)
        return out

    # -------------------------------------------------------- a pele de cada falange

    def _preparar_pele(self):
        """A pele de cada falange, no espaço do osso dela: alguns pontos da
        malha (os que o osso leva), a linha do osso e o raio. É com eles que
        se vê se o dedo encosta ou entra na arma — a malha de verdade, e não
        um cilindro: o dedo do personagem não tem a forma do de lá (o Barut
        tem a ponta do dedo duas vezes mais comprida e o dedo 4 mm mais
        grosso)."""
        corpo = self.corpo
        b = corpo.arm.data.bones
        if not hasattr(corpo, '_pele_falange'):
            nomes = {corpo.n(f'{l}Hand{d}{k}') for l in ('Left', 'Right') for d in DEDOS for k in (1, 2, 3, 4)}
            pele = {}
            for o in corpo.malhas:
                grupos = {g.index: g.name for g in o.vertex_groups}
                if not nomes & set(grupos.values()):
                    continue
                mw = o.matrix_world
                for v in o.data.vertices:
                    if not v.groups:
                        continue
                    g = max(v.groups, key=lambda g: g.weight)
                    nome = grupos.get(g.group)
                    if nome in nomes:
                        pele.setdefault(nome, []).append((mw @ v.co, {grupos[x.group]: x.weight for x in v.groups}))
            corpo._pele_falange = pele
        self.pele = {}
        for d in DEDOS:
            ossos = self.ossos[d]
            falanges = []
            adiante = {}
            for k in range(3):
                volta = b[ossos[k]].matrix_local.inverted()
                fim = volta @ b[ossos[k + 1]].head_local
                L = fim.length
                eixo = fim / L
                # O osso de trás (a mão, para a primeira falange): a pele
                # perto da junta vai em parte com ele (os pesos da malha) —
                # contada só com o osso dela, a base do polegar e dos dedos
                # entrava na arma sem a conta ver.
                pai = b[ossos[k - 1]] if k else b[corpo.n(self.lado + 'Hand')]
                volta_pai = pai.matrix_local.inverted()
                brutos = list(corpo._pele_falange.get(ossos[k], []))
                # a ponta do dedo vai com a última falange
                if k == 2:
                    brutos += corpo._pele_falange.get(ossos[3], [])
                pts = []
                todos = []
                tras = {}
                volta_filho = b[ossos[k + 1]].matrix_local.inverted() if k < 2 else None
                for p, pesos in brutos:
                    pl = volta @ p
                    pl.freeze()
                    todos.append(pl)
                    filho = pesos.get(ossos[k + 1], 0.0) if k < 2 else 0.0
                    if filho >= 0.12:
                        # A pele em cima da junta vai em boa parte com a
                        # falange seguinte: é com a dobra dela que se vê onde
                        # fica (contada reta, o nó do polegar do Barut entrava
                        # 1 cm na caixa do SCAR sem a conta ver). Vai conferida
                        # junto com a falange seguinte (_folga).
                        adiante.setdefault(k + 1, []).append(
                            (volta_filho @ p, pl, filho / max(sum(pesos.values()), 1e-6),
                             eixo * max(0.1 * L, min(0.9 * L, pl.dot(eixo)))))
                        continue
                    eu = pesos.get(ossos[k], 0.0) + (pesos.get(ossos[3], 0.0) if k == 2 else 0.0) + filho
                    dele = sum(w for nome, w in pesos.items() if nome not in ossos) if k == 0 else pesos.get(ossos[k - 1], 0.0)
                    pts.append(pl)
                    tras[pl] = (volta_pai @ p, dele / max(eu + dele, 1e-6))
                raios = sorted((p - eixo * p.dot(eixo)).length for p in todos if 0.15 * L < p.dot(eixo) < 0.85 * L)
                raio = raios[len(raios) // 2] if raios else corpo.raio_dedo[self.lado + d]
                # espalhados (o mais longe dos já escolhidos)
                escolhidos = []
                if pts:
                    centro = sum(pts, Vector()) / len(pts)
                    escolhidos = [max(pts, key=lambda p: (p - centro).length)]
                    dist = [(p - escolhidos[0]).length for p in pts]
                    # (a ponta leva o dobro: é a falange mais comprida — a do
                    # Barut tem 4,6 cm — e a que mais encosta)
                    while len(escolhidos) < min(PELE_PONTOS * (2 if k == 2 else 1), len(pts)):
                        j = max(range(len(pts)), key=lambda x: dist[x])
                        if dist[j] < 1e-5:
                            break
                        escolhidos.append(pts[j])
                        dist = [min(dist[x], (pts[x] - pts[j]).length) for x in range(len(pts))]
                # o miolo: o que é fino na arma (o guarda-mato) não passa
                # pelo meio do dedo entre dois pontos da pele
                miolo = [(eixo * (L * t), raio * 0.55) for t in ((0.3, 0.65, 0.95) if k == 2 else (0.3, 0.7))]
                # o pé de cada ponto na linha do osso (de onde se olha se a
                # arma passa entre o osso e a pele)
                pes = [eixo * max(0.1 * L, min(0.9 * L, p.dot(eixo))) for p in escolhidos]
                falanges.append(dict(pts=escolhidos, pes=pes, tras=[tras[p] for p in escolhidos], miolo=miolo,
                                     eixo=eixo, L=L, raio=raio, do_pai=()))
            for k, lista in adiante.items():
                falanges[k]['do_pai'] = tuple(lista)
            self.pele[d] = falanges

    def _folga(self, rig, m, falange, outros, m_tras=None, teto=TETO_FOLGA):
        """A menor folga (m) da pele de uma falange, posta em m, até a arma
        e até os dedos já postos; negativa (o quanto entra) se a arma passa
        entre o osso e a pele, ou pelo osso."""
        a = m.translation
        fura = rig.corta(a, m @ (falange['eixo'] * falange['L']))
        if fura is not None:
            return -fura - falange['raio']
        menor = teto
        for p, pe, (pt, wt) in zip(falange['pts'], falange['pes'], falange['tras']):
            w = m @ p
            if m_tras is not None and wt > 0.03:
                w = w * (1.0 - wt) + (m_tras @ pt) * wt
            fura = rig.corta(m @ pe, w)
            if fura is not None:
                if -fura < menor:
                    menor = -fura
                continue
            if menor < 0.0:
                continue
            x = abs(rig.distancia(w, teto=teto)[0])
            if x < menor:
                menor = x
            for a_, ab, ab2, r in outros:
                t = max(0.0, min(1.0, (w - a_).dot(ab) / ab2))
                x = (w - a_ - ab * t).length - r
                if x < menor:
                    menor = x
        if m_tras is not None:
            # a pele da junta de trás, levada pelas duas falanges
            for pc, pp, wc, pe_p in falange['do_pai']:
                fura = rig.corta(m_tras @ pe_p, (m_tras @ pp) * (1.0 - wc) + (m @ pc) * wc)
                if fura is not None and -fura < menor:
                    menor = -fura
        if menor < 0.0:
            return menor
        for p, r in falange['miolo']:
            x = abs(rig.distancia(m @ p, teto=teto)[0]) - r
            if x < menor:
                menor = x
        return menor

    def _encaixar(self, rig, d, T, eixo, base_dir, limite, base0, quer, outros, antes=None, peso_la=None, junto=None):
        """Encaixa um dedo na arma. As três juntas (base, meio, ponta) dobram
        em volta de `eixo` a partir das direções T (as da primeira pessoa), e
        procura-se a dobra de cada uma que dá o mesmo resultado de lá no
        dedo do personagem, tenha ele o tamanho que tiver:

        - nada entra na arma (a pele de cada falange fica a FOLGA dela, no
          mínimo) nem nos dedos já postos — é o que mais pesa;
        - a falange que lá encosta na arma (quer[k]) encosta aqui também;
        - a junta de uma falange que lá não encosta fica com o ângulo de lá,
          e as outras ficam o mais perto dele que der;
        - a ponta dobra na proporção do meio, como num dedo de verdade (sem
          ponta em gancho com o meio reto).

        Primeiro uma grade em todo o alcance das juntas, depois o acerto fino.
        Com `antes` (os desvios do quadro anterior) procura só em volta
        deles: mais rápido, e o dedo não pula de um quadro para o outro.
        Devolve as direções, as folgas, os desvios e o custo."""
        nomes = ('mcp', 'pip', 'dip')
        esq = self.corpo.esq
        ossos = self.ossos[d]
        pele = self.pele[d]
        local = [pele[k]['eixo'] for k in range(3)]
        aT = []
        anterior = base_dir
        for k in range(3):
            aT.append(ang_em_torno(anterior, T[k], eixo))
            anterior = T[k]
        lo = [min(0.0, rad(limite[nomes[k]][0]) - aT[k]) for k in range(3)]
        hi = [max(0.0, rad(limite[nomes[k]][1]) - aT[k]) for k in range(3)]
        segura = any(quer)

        def falange(b, k, ac):
            r0 = b.to_3x3()
            r = (r0 @ local[k]).normalized().rotation_difference(girar_v(T[k], eixo, ac)).to_matrix() @ r0
            return Matrix.Translation(b.translation) @ r.to_4x4()

        ante = antes['e'] if antes is not None else None
        # o polegar parte de mais longe da arma que os dedos: procura até mais longe
        teto_ = TETO_POLEGAR if d == 'Thumb' else TETO_FOLGA
        # o punho fechado num cabo (faca, granada): a forma de partida é
        # inventada, então fechar mais não custa quase nada e o dedo só
        # conta como encostado bem rente (Jhon: "da faca pode grudar mais")
        PESO_LA_K = PESO_LA if peso_la is None else peso_la
        TOQUE_K = TOQUE if peso_la is None else 0.0015

        # `junto`: (as direções do vizinho como ficou, as de lá dele). O
        # quanto cada falange dele girou além da de lá, em volta deste eixo.
        com = None
        if junto is not None and PESO_JUNTO > 0.0:
            com = [ang_em_torno(junto[1][k], junto[0][k], eixo) for k in range(3)]

        def custo_junta(k, ek, fk, ac=None):
            c = PESO_TEMPO * abs(ek - ante[k]) if ante is not None else 0.0
            if com is not None and ac is not None:
                c += PESO_JUNTO * abs(ac - com[k])
            if fk < FOLGA:
                c += 0.6 + PESO_DENTRO * (FOLGA - fk)
            if quer[k]:
                return c + PESO_TOQUE * max(0.0, fk - TOQUE_K) + PESO_LA_K * abs(ek)
            return c + PESO_COPIA * abs(ek)

        def custo_ponta(e):
            if not (segura and quer[2]):
                return 0.0
            return PESO_PONTA * abs((aT[2] + e[2]) - PONTA_DO_MEIO * (aT[1] + e[1]))

        m_mao = base0 @ esq.desvio[ossos[0]].inverted()

        def avaliar(e):
            """As folgas e o custo de uma dobra."""
            b = base0
            ac = 0.0
            f = []
            c = 0.0
            tras_ = m_mao
            for k in range(3):
                ac += e[k]
                mk = falange(b, k, ac)
                fk = self._folga(rig, mk, pele[k], outros, tras_, teto_)
                f.append(fk)
                c += custo_junta(k, e[k], fk, ac)
                tras_ = mk
                if k < 2:
                    b = mk @ esq.desvio[ossos[k + 1]]
            return c + custo_ponta(e), f

        def grade(k, centro=None):
            if centro is None:
                n = max(2, int(round((hi[k] - lo[k]) / PASSO_GRADE)) + 1)
                return [lo[k] + (hi[k] - lo[k]) * i / (n - 1) for i in range(n)] + [0.0]
            return sorted({max(lo[k], min(hi[k], centro + x * PASSO_PERTO)) for x in (-2, -1, 0, 1, 2)})

        def procurar(centro):
            achados = []
            g0, g1, g2 = (grade(k, None if centro is None else centro[k]) for k in range(3))
            for e0 in g0:
                m0 = falange(base0, 0, e0)
                f0 = self._folga(rig, m0, pele[0], outros, m_mao, teto_)
                c0 = custo_junta(0, e0, f0, e0)
                b1 = m0 @ esq.desvio[ossos[1]]
                for e1 in g1:
                    m1 = falange(b1, 1, e0 + e1)
                    f1 = self._folga(rig, m1, pele[1], outros, m0, teto_)
                    achados.append((c0 + custo_junta(1, e1, f1, e0 + e1), e0, e1, m1, f0, f1))
            achados.sort(key=lambda x: x[0])
            melhor = None
            for c01, e0, e1, m1, f0, f1 in achados[:CANDIDATOS]:
                # as dobras de base e meio, da melhor para a pior, vão até a
                # ponta; para quando nem a ponta perfeita ganharia da melhor
                # já achada (a procura na grade é exata)
                if melhor is not None and c01 >= melhor[0]:
                    break
                b2 = m1 @ esq.desvio[ossos[2]]
                for e2 in g2:
                    f2 = self._folga(rig, falange(b2, 2, e0 + e1 + e2), pele[2], outros, m1, teto_)
                    e = [e0, e1, e2]
                    c = c01 + custo_junta(2, e2, f2, e0 + e1 + e2) + custo_ponta(e)
                    if melhor is None or c < melhor[0]:
                        melhor = (c, e, [f0, f1, f2])
            return melhor

        if antes is not None:
            melhor = procurar(ante)
            if antes.get('mudou') or melhor[0] > antes['custo'] + 0.35:
                # a mão passou a fazer outra coisa, ou perto não ficou bom:
                # olha o alcance todo, e só troca se for bem melhor (o dedo
                # não pula por pouco)
                outro = procurar(None)
                if outro[0] < melhor[0] - 0.3:
                    melhor = outro
        else:
            melhor = procurar(None)
        c, e, f = melhor
        # o acerto fino: cada junta, da ponta para a base
        for passo in (rad(3.0), rad(1.5), rad(0.75)):
            for k in (2, 1, 0, 2, 1):
                for sinal in (1.0, -1.0):
                    n = list(e)
                    n[k] = max(lo[k], min(hi[k], e[k] + sinal * passo))
                    if n[k] == e[k]:
                        continue
                    r = avaliar(n)
                    if r[0] < c - 1e-6:
                        c, f = r
                        e = n
                        break
        if ante is not None:
            # o custo sem a parte do tempo (o que se compara no quadro seguinte)
            c -= sum(PESO_TEMPO * abs(e[k] - ante[k]) for k in range(3))
        return [girar_v(T[k], eixo, sum(e[:k + 1])) for k in range(3)], f, e, c

    def _matrizes(self, d, base0, dirs):
        """As matrizes das três falanges de um dedo com estas direções."""
        esq = self.corpo.esq
        ossos = self.ossos[d]
        out = []
        b = base0
        for k in range(3):
            r0 = b.to_3x3()
            r = (r0 @ self.pele[d][k]['eixo']).normalized().rotation_difference(dirs[k]).to_matrix() @ r0
            mk = Matrix.Translation(b.translation) @ r.to_4x4()
            out.append(mk)
            if k < 2:
                b = mk @ esq.desvio[ossos[k + 1]]
        return out

    def _folgas_de(self, rig, d, dirs, base0, outros):
        """A folga da pele de cada falange (m; negativa dentro) com o dedo
        nestas direções."""
        ms = self._matrizes(d, base0, dirs)
        tras = base0 @ self.corpo.esq.desvio[self.ossos[d][0]].inverted()
        out = []
        for k in range(3):
            out.append(self._folga(rig, ms[k], self.pele[d][k], outros, tras, TETO_FOLGA))
            tras = ms[k]
        return out

    def _no_gatilho(self, rig, d, fp, base0, rot_mao, A, N, outros):
        """O indicador no gatilho (Jhon, 23/11: "encaixe perfeitamente os
        dedos no gatilho"). A polpa da ponta do dedo — o meio da última
        falange, do lado da palma — vai no ponto do gatilho em que o dedo de
        lá encosta, e o dedo chega ali como um dedo chega: a base abre pouco
        para o lado (até 18°), as três juntas dobram dentro do que dobram, a
        da ponta perto de dois terços da do meio (as duas são puxadas pelo
        mesmo tendão), e nada atravessa o guarda-mato. Procura em tudo o
        que a mão alcança e depois acerta fino. Devolve as direções, as
        folgas e o quanto a polpa ficou do ponto (m)."""
        corpo = self.corpo
        lado = self.lado
        pele = self.pele[d]
        L = [pele[k]['L'] for k in range(3)]
        js = fp['juntas'][1]
        meio_fp = js[2].lerp(js[3], 0.5)
        C = rig.distancia(meio_fp, so=(rig.gatilho or None), teto=0.05)[2]
        para = C - meio_fp
        if para.length < 1e-5:
            para = -A
        para.normalize()
        Q = C - para * (pele[2]['raio'] + FOLGA)
        dfp = (js[3] - js[2]).normalized()
        reto = [(rot_mao @ v).normalized() for v in corpo.falange_repouso[lado + d]]
        eixo0 = reto[0].cross(N)
        eixo0 = eixo0.normalized() if eixo0.length > 0.2 else A.cross(N).normalized()
        p0 = base0.translation

        def dedo(b, a0, a1, a2):
            qb = Quaternion(N, rad(b))
            e = qb @ eixo0
            return [girar_v(qb @ reto[0], e, rad(a0)), girar_v(qb @ reto[1], e, rad(a0 + a1)),
                    girar_v(qb @ reto[2], e, rad(a0 + a1 + a2))]

        def longe(dirs):
            polpa = p0 + dirs[0] * L[0] + dirs[1] * L[1] + dirs[2] * (L[2] * GATILHO_POLPA)
            return (polpa - Q).length

        def custo_livre(b, a0, a1, a2, dirs):
            return (longe(dirs) * 1000.0 + 8.0 * (1.0 - dirs[2].dot(dfp)) + 0.02 * abs(a2 - 0.67 * a1) + 0.03 * abs(b)
                    + 0.004 * a0)

        def custo(b, a0, a1, a2):
            dirs = dedo(b, a0, a1, a2)
            f = self._folgas_de(rig, d, dirs, base0, outros)
            return custo_livre(b, a0, a1, a2, dirs) + sum(max(0.0, FOLGA - x) for x in f) * 3000.0, dirs, f

        lim = LIMITE_DEDO
        achados = []
        for b in range(-18, 19, 6):
            for a0 in range(int(lim['mcp'][0]), int(lim['mcp'][1]) + 1, 6):
                for a1 in range(0, int(lim['pip'][1]) + 1, 7):
                    for a2 in range(0, int(lim['dip'][1]) + 1, 8):
                        achados.append((custo_livre(b, a0, a1, a2, dedo(b, a0, a1, a2)), b, a0, a1, a2))
        achados.sort(key=lambda x: x[0])
        melhor = None
        for c0, b, a0, a1, a2 in achados[:50]:
            if melhor is not None and c0 >= melhor[0]:
                break
            c, dirs, f = custo(b, a0, a1, a2)
            if melhor is None or c < melhor[0]:
                melhor = (c, [b, a0, a1, a2], dirs, f)
        c, x, dirs, f = melhor
        faixa = ((-18.0, 18.0), lim['mcp'], lim['pip'], lim['dip'])
        for passo in (3.0, 1.5, 0.75, 0.4):
            for _ in range(2):
                for j in (3, 2, 1, 0):
                    for sinal in (1.0, -1.0):
                        n = list(x)
                        n[j] = max(faixa[j][0], min(faixa[j][1], x[j] + sinal * passo))
                        if n[j] == x[j]:
                            continue
                        r = custo(*n)
                        if r[0] < c - 1e-6:
                            c, dirs, f = r
                            x = n
                            break
        self.gatilho_juntas = x
        return dirs, f, longe(dirs)

    @staticmethod
    def _vao(pi_, ri, pv, rv):
        """O menor vão (m; negativo dentro) entre a pele de dois dedos, cada
        um dado pelas juntas e pelos raios das falanges."""
        menor = 1.0
        for k in range(3):
            for t in (0.0, 0.5, 1.0):
                a_ = pi_[k].lerp(pi_[k + 1], t)
                for kv in range(3):
                    b0 = pv[kv]
                    ab = pv[kv + 1] - b0
                    s_ = max(0.0, min(1.0, (a_ - b0).dot(ab) / max(ab.length_squared, 1e-10)))
                    menor = min(menor, (a_ - b0 - ab * s_).length - (ri[k] + rv[kv]))
        return menor

    def _juntas(self, d, base0, dirs):
        """Onde ficam as juntas e a ponta de um dedo com estas direções."""
        pts = [base0.translation.copy()]
        for k in range(3):
            pts.append(pts[-1] + dirs[k] * self.pele[d][k]['L'])
        return pts

    def resolver(self, M, locais, rig, fp, contato=True, segura=None, mao_info=None, forma=None, outra=None, fp_outra=None):
        """`outra`: os dedos já postos da outra mão (Dedos.feitos), que estes
        não atravessam; `fp_outra`: a outra mão de lá — a falange de lá que
        encosta nos dedos dela (a esquerda abraçando a direita na pistola)
        encosta aqui também."""
        corpo, esq = self.corpo, self.corpo.esq
        outra = tuple(outra) if outra else ()
        lado = self.lado
        rot_mao = M[corpo.n(lado + 'Hand')].to_3x3().normalized()
        A = (rot_mao @ corpo.mao[lado]['dedos']).normalized()
        N = (rot_mao @ corpo.mao[lado]['palma']).normalized()
        medidas = {}
        # Onde a mão de lá encosta na arma, falange por falange (a linha do
        # osso de lá a menos de PERTO_FP da arma): essas encostam aqui
        # também; as outras copiam o ângulo de lá. O dedo no gatilho copia
        # (não aperta).
        no_gatilho = False
        if rig is None or not contato:
            quer_contato = [[False] * 3 for _ in range(5)]
        elif forma is not None:
            quer_contato = [[forma == 'punho'] * 3 for _ in range(5)]
        else:
            quer_contato = []
            for i in range(5):
                js = fp['juntas'][i]
                perto = PERTO_FP[0 if i == 0 else 1] * rig.escala
                # quem já encostava continua encostando até se afastar um
                # pouco mais (não fica ligando e desligando na beirada)
                era = self.memoria.get('quer')
                q = []
                for k in range(3):
                    longe = min(abs(rig.distancia(js[k].lerp(js[k + 1], t), teto=0.05)[0]) for t in (0.25, 0.6, 0.95))
                    q.append(longe < perto * (1.35 if era and era[i][k] else 1.0))
                # O dedo do gatilho: o indicador da mão direita numa arma de
                # fogo, enquanto a ponta do de lá está no guarda-mato. (Só o
                # AR-15 tem o gatilho como peça à parte; nas outras o dedo era
                # tratado como mais um a encostar na arma, e saía de lá.)
                if i == 1 and lado == 'Right' and ARMAS.get(rig.chave) in ('fuzil', 'pistola') and abs(
                        rig.distancia(js[2].lerp(js[3], 0.6), so=(rig.gatilho or None), teto=0.05)[0]) < perto:
                    q = [False] * 3
                    no_gatilho = True
                if fp_outra is not None:
                    for k in range(3):
                        if q[k]:
                            continue
                        for t in (0.25, 0.6, 0.95):
                            p_ = js[k].lerp(js[k + 1], t)
                            for jo in fp_outra['juntas']:
                                for ko in range(3):
                                    ab = jo[ko + 1] - jo[ko]
                                    s_ = max(0.0, min(1.0, (p_ - jo[ko]).dot(ab) / max(ab.length_squared, 1e-10)))
                                    if (p_ - jo[ko] - ab * s_).length < PERTO_FP[2] * rig.escala:
                                        q[k] = True
                quer_contato.append(q)
        if forma is not None:
            # a forma feita aqui no lugar da de lá
            fp = dict(fp)
            fp['dedos'] = self.forma(M, forma)
        elif 'ao_longo' in fp:
            # A mão do personagem não fica igual à de lá na arma: a pegada a
            # gira (até 40° em volta do cano e 50° deitada) para o braço dele
            # servir. As direções dos dedos de lá vão junto com esse giro —
            # copiadas como estavam no mundo, os dedos ficavam tortos em
            # relação à palma (o indicador reto para cima e os outros
            # enrolados embaixo do guarda-mão).
            mi = corpo.mao[lado]
            alinhada = orient(fp['ao_longo'], fp['dentro']) @ orient(mi['dedos'], mi['palma']).inverted()
            giro_ = rot_mao @ alinhada.inverted()
            fp = dict(fp)
            fp['dedos'] = [[(giro_ @ v).normalized() for v in dedo] for dedo in fp['dedos']]
        if segura is None:
            segura = [any(q) for q in quer_contato]
        if not self.memoria and rig is not None and forma is None:
            # O clipe começa de onde a pegada de base mais perto parou (a do
            # quadril, para a da mira): começando do nada, cada uma assentava
            # de um jeito (o polegar de um lado e do outro do guarda-mão) e
            # no mirar o dedo pulava 90° de uma para a outra.
            todas = getattr(corpo, '_pegada_dedos', {}).get(lado, [])
            if todas:
                g_ = rig.ref_da_mao(forma)
                gi_ = (Matrix.Translation(g_.translation) @ g_.to_3x3().normalized().to_4x4()).inverted()
                mao_ = (gi_ @ M[corpo.n(lado + 'Hand')]).translation
                perto_ = min(todas, key=lambda b_: (b_['mao'].translation - mao_).length)
                if perto_.get('memoria'):
                    self.memoria = {k_: (dict(v_) if isinstance(v_, dict) else v_) for k_, v_ in perto_['memoria'].items()}
        # o que valeu no quadro anterior (some se a mão passou a fazer outra coisa)
        chave = (forma, tuple(tuple(q) for q in quer_contato))
        mudou = self.memoria.get('chave') != chave
        self.memoria['chave'] = chave
        self.memoria['quer'] = quer_contato if forma is None else None
        ordem = (2, 1, 3, 4, 0)
        vizinho = {1: 2, 3: 2, 4: 3}
        aperta = 0.08 if forma == 'punho' else None
        feitos = []
        postos = {}
        # A pegada de base (Dedos.fixar): na primeira pessoa os dedos não se
        # mexem na arma em quase nenhum clipe (parado, andar, sacar, mirar,
        # mira: a mão inteira vai com a arma). Enquanto o dedo de lá está
        # onde estava em relação à arma, o daqui fica colado no encaixe de
        # base — o mesmo em todos os clipes — e só se ajeita se entrar nela.
        base = None
        if rig is not None:
            g_arma = rig.ref_da_mao(forma)
            g3 = g_arma.to_3x3().normalized()
            gi_arma = (Matrix.Translation(g_arma.translation) @ g3.to_4x4()).inverted()
            if forma in (None, 'punho'):
                # (o punho fechado no cabo da faca e na granada também: lá a
                # mão não mexe na arma, e aqui os dedos se rearrumavam a cada
                # quadro — o laço da faca parada pulava 17° na emenda)
                base = self._base(gi_arma @ M[corpo.n(lado + 'Hand')])
        self.ultimo = {}
        for i in ordem:
            d = DEDOS[i]
            ossos = self.ossos[d]
            quer = [v.copy() for v in fp['dedos'][i]]
            limite = LIMITE_POLEGAR if i == 0 else LIMITE_DEDO
            colado = False
            if base and d in base:
                la, dirs_arma = base[d]
                js = fp['juntas'][i]
                # (o indicador que ficou fora do guarda-mato não acompanha o
                # aperto do gatilho de lá)
                if rig.firme(forma) or max((gi_arma @ js[k] - la[k]).length for k in range(4)) < 0.004 * rig.escala or (
                        i == 1 and no_gatilho):
                    colado = True
                    quer = [(g3 @ v).normalized() for v in dirs_arma]
            # o eixo em que o dedo fecha: perpendicular à falange de base e à
            # palma (no polegar, o plano das falanges de lá)
            if i == 0:
                eixo = quer[0].cross(quer[1]) + quer[1].cross(quer[2])
                if eixo.length < 0.05:
                    eixo = A.cross(N)
                eixo.normalize()
                base_dir = quer[0]
            else:
                eixo = quer[0].cross(N)
                if eixo.length < 0.2:
                    eixo = A.cross(N)
                eixo.normalize()
                base_dir = (rot_mao @ self.repouso_base[d]).normalized()
            fora = getattr(corpo, '_indicador_fora', set())
            if INDICADOR_FORA and i == 1 and no_gatilho and lado in fora and not colado:
                # O indicador do personagem não cabe no guarda-mato (viu-se
                # no primeiro quadro: lá embaixo). Fica como quem não está
                # atirando: estendido, encostado no corpo da arma, fora do
                # gatilho — e não atravessando o guarda-mato.
                reto_ = [(rot_mao @ v).normalized() for v in corpo.falange_repouso[lado + d]]
                eixo = reto_[0].cross(N)
                eixo = eixo.normalized() if eixo.length > 0.2 else A.cross(N).normalized()
                quer = [girar_v(reto_[k], eixo, rad(a_)) for k, a_ in enumerate((8.0, 18.0, 26.0))]
                # a base procura a arma; o meio e a ponta ficam quase retos
                quer_contato[i] = [True, False, False]
            # as juntas no que uma mão dobra (base, meio, ponta)
            quer = self._limitar(quer, base_dir, eixo, limite)
            base0 = esq.base(ossos[0], M)
            quer_i = quer_contato[i]
            folgas = None
            desvio = (0.0, 0.0, 0.0)
            if colado:
                obst = (tuple(feitos) if i == 0 else ()) + outra
                if i in vizinho:
                    pv, rv = postos[vizinho[i]]
                    obst = obst + tuple((pv[k], pv[k + 1] - pv[k], max((pv[k + 1] - pv[k]).length_squared, 1e-10), rv[k] * 0.88)
                                        for k in range(3))
                if i == 1 and no_gatilho:
                    # o dedo do gatilho fica como foi posto nele (_no_gatilho)
                    colado_ = (quer, self._folgas_de(rig, d, quer, base0, obst), (0.0, 0.0, 0.0), 0.0)
                else:
                    colado_ = self._encaixar(rig, d, quer, eixo, base_dir, limite, base0, [False] * 3, obst,
                                             dict(e=[0.0, 0.0, 0.0], custo=0.0))
                if min(colado_[1]) < FOLGA - 0.0015 and not rig.firme(forma) and not (i == 1 and no_gatilho):
                    # Colado na pegada de base o dedo entra na arma (a mão na
                    # mira fica girada uns graus a mais no cabo que no
                    # quadril, e o indicador do Barut, colado, ia 6,5 mm para
                    # dentro da caixa do AR-15): encaixa de novo, solto, a
                    # partir das direções da base.
                    colado = False
                else:
                    quer, folgas, desvio, _ = colado_
                    quer_i = [False] * 3
            if not colado and rig is not None and i == 1 and no_gatilho and not INDICADOR_FORA:
                obst = outra
                if i in vizinho:
                    pv, rv = postos[vizinho[i]]
                    obst = obst + tuple((pv[k], pv[k + 1] - pv[k], max((pv[k + 1] - pv[k]).length_squared, 1e-10), rv[k] * 0.88)
                                        for k in range(3))
                quer, folgas, erro_ = self._no_gatilho(rig, d, fp, base0, rot_mao, A, N, obst)
                desvio = (0.0, 0.0, 0.0)
                quer_i = [False] * 3
                self.gatilho_erro = erro_
                print(f'GATILHO {corpo.nome} {rig.chave}: polpa a {erro_ * 1000:.1f} mm do ponto, juntas (lado, base, meio, ponta) '
                      f'{[round(v, 1) for v in self.gatilho_juntas]}, folgas {[round(v * 1000, 1) for v in folgas]} mm', flush=True)
                self.memoria[d] = dict(e=[0.0, 0.0, 0.0], custo=0.0, lado=0.0, varrido=True, chato=False, eixo_lado=None)
            elif not colado and rig is not None:
                mem = self.memoria.get(d)
                if mem and mudou:
                    mem = dict(mem, mudou=True)
                # o polegar não atravessa os dedos já postos
                obst = (tuple(feitos) if i == 0 else ()) + outra
                de_lado = mem['lado'] if mem else 0.0
                chato = bool(mem and mem.get('chato'))
                eixo_lado = None
                u = None

                def virado(angulo, chato_=False):
                    """T, o eixo e a base virados de lado `angulo` (em volta de
                    eixo_lado); ou, com chato_, cada falange com o mesmo
                    lado da do vizinho (os dois dedos em planos paralelos)."""
                    if chato_:
                        pv_ = postos[vizinho[i]][0]
                        dv = [(pv_[k + 1] - pv_[k]).normalized() for k in range(3)]
                        t2 = []
                        for k in range(3):
                            v_ = quer[k] - u * quer[k].dot(u) + u * dv[k].dot(u)
                            t2.append(v_.normalized() if v_.length > 1e-6 else quer[k])
                        bd = base_dir - u * base_dir.dot(u) + u * dv[0].dot(u)
                        bd = bd.normalized() if bd.length > 1e-6 else base_dir
                        ex = t2[0].cross(N)
                        ex = ex.normalized() if ex.length > 0.2 else eixo
                        return self._limitar(t2, bd, ex, limite), ex, bd
                    if eixo_lado is None or abs(angulo) < 1e-6:
                        return quer, eixo, base_dir
                    return ([girar_v(q, eixo_lado, angulo) for q in quer], girar_v(eixo, eixo_lado, angulo),
                            girar_v(base_dir, eixo_lado, angulo))

                if i in vizinho:
                    # o lado: a linha dos nós dos dedos (para o vizinho)
                    u = postos[vizinho[i]][0][0] - base0.translation
                    if u.length > 1e-4:
                        u.normalize()
                        frente = quer[0] - u * quer[0].dot(u)
                        if frente.length > 0.2:
                            eixo_lado = frente.cross(u).normalized()
                    else:
                        u = None
                else:
                    # o polegar: o lado é o movimento que a dobra não faz (a
                    # base dele vira nos dois sentidos: fecha em volta de
                    # `eixo` e vai de lado em volta deste). Era "para a arma"
                    # (quer[0] x o ponto mais perto dela): quando a arma mais
                    # perto ficava no plano da dobra, os dois eixos eram o
                    # mesmo e o polegar não tinha como sair de baixo da caixa
                    # do SCAR e do AK (atravessava 2-3 cm).
                    eixo_lado = quer[0].cross(eixo)
                    if eixo_lado.length > 1e-6:
                        eixo_lado.normalize()
                        meio_ = base0.translation + quer[0] * self.pele[d][0]['L'] + quer[1] * self.pele[d][1]['L'] * 0.5
                        para = rig.distancia(meio_)[2] - meio_
                        if mem and mem.get('eixo_lado') is not None:
                            # o mesmo sentido do quadro anterior
                            if eixo_lado.dot(rot_mao @ mem['eixo_lado']) < 0.0:
                                eixo_lado = -eixo_lado
                        elif eixo_lado.cross(quer[0]).dot(para) < 0.0:
                            # positivo: para a arma
                            eixo_lado = -eixo_lado
                    else:
                        eixo_lado = None
                chato = chato and u is not None
                if i in vizinho:
                    # o dedo também não atravessa o vizinho (a pele dele, com
                    # uma folga: dois dedos juntos se apertam um pouco)
                    pv, rv = postos[vizinho[i]]
                    obst = obst + tuple((pv[k], pv[k + 1] - pv[k], max((pv[k + 1] - pv[k]).length_squared, 1e-10), rv[k] * 0.88)
                                        for k in range(3))
                def aberto(dirs_):
                    """O polegar do punho fechado longe dos dedos (custo)."""
                    if not (forma == 'punho' and i == 0 and feitos):
                        return 0.0
                    ponta_ = self._juntas(d, base0, dirs_)[3]
                    rp_ = self.pele[d][2]['raio']
                    return PESO_FECHA * max(0.0, min(
                        (ponta_ - a_ - ab_ * max(0.0, min(1.0, (ponta_ - a_).dot(ab_) / ab2_))).length - r_ - rp_
                        for a_, ab_, ab2_, r_ in feitos))

                junto = None
                if forma is None and i in vizinho and segura[i] and segura[vizinho[i]]:
                    pv_ = postos[vizinho[i]][0]
                    junto = ([(pv_[k + 1] - pv_[k]).normalized() for k in range(3)], fp['dedos'][vizinho[i]])
                t_, ex_, bd_ = virado(de_lado, chato)
                dirs, folgas, desvio, custo = self._encaixar(rig, d, t_, ex_, bd_, limite, base0, quer_i, obst, mem, peso_la=aperta,
                                                             junto=junto)
                custo += aberto(dirs)
                if i in vizinho and u is not None:
                    # Juntos, e sem se atravessar: a mão do personagem não tem
                    # a largura da de lá. Se a pele do dedo entra na do
                    # vizinho, tenta os dois em planos paralelos e o dedo
                    # virado de lado para longe; se os dois seguram a arma e
                    # sobra vão entre as pontas, virado até encostar no
                    # vizinho. Fica o que custar menos (entrar no vizinho
                    # custa como entrar na arma).
                    v = vizinho[i]
                    ri = [self.pele[d][k]['raio'] for k in range(3)]
                    pi_ = self._juntas(d, base0, dirs)
                    entre_ = self._vao(pi_, ri, pv, rv)
                    opcoes = []
                    if entre_ < -0.001:
                        if not chato:
                            opcoes.append((0.0, True))
                        if eixo_lado is not None:
                            opcoes += [(de_lado - rad(6), False), (de_lado - rad(12), False)]
                    elif not chato and eixo_lado is not None and segura[i] and segura[v]:
                        vao, braco = None, None
                        for a_, b_, k in ((pi_[2].lerp(pi_[3], 0.5), pv[2].lerp(pv[3], 0.5), 2), (pi_[2], pv[2], 1)):
                            g = (b_ - a_).dot(u) - (ri[k] + rv[k])
                            if vao is None or g < vao:
                                vao = g
                                braco = a_ - pi_[0]
                        braco = (braco - u * braco.dot(u)).length
                        if braco > 0.02 and vao > 0.001:
                            novo = max(-rad(12), min(de_lado + vao / braco, rad(14)))
                            if abs(novo - de_lado) > rad(0.5):
                                opcoes.append((novo, False))
                    melhor = (custo, dirs, folgas, desvio, de_lado, chato)
                    for lado_o, chato_o in opcoes:
                        t_, ex_, bd_ = virado(lado_o, chato_o)
                        d2, f2, e2, c2 = self._encaixar(rig, d, t_, ex_, bd_, limite, base0, quer_i, obst, peso_la=aperta, junto=junto)
                        if entre_ >= -0.001 and self._vao(self._juntas(d, base0, d2), ri, pv, rv) < -0.001:
                            continue
                        if c2 < melhor[0] - 0.05:
                            melhor = (c2, d2, f2, e2, lado_o, chato_o)
                    custo, dirs, folgas, desvio, de_lado, chato = melhor
                varrido = bool(mem and mem.get('varrido'))
                if INDICADOR_FORA and i == 1 and no_gatilho and lado not in fora and min(folgas) < FOLGA - 0.001:
                    # não coube: daqui em diante, estendido (acima)
                    if not hasattr(corpo, '_indicador_fora'):
                        corpo._indicador_fora = set()
                    corpo._indicador_fora.add(lado)
                    self.memoria.pop(d, None)
                    return self.resolver(M, locais, rig, fp, contato, None, mao_info, forma, outra, fp_outra)
                # (já varrido, só de novo se entrar na arma de verdade: a mão
                # na mira fica girada de outro jeito no cabo, e o lado que
                # servia no quadril deixava o indicador do Barut 6,5 mm dentro
                # da caixa do AR-15)
                if INDICADOR_FORA and i == 1 and eixo_lado is not None and not chato and (
                        min(folgas) < FOLGA - 0.0015 or (not varrido and (min(folgas) < FOLGA or custo > 1.0))):
                    # o indicador não cabe onde o de lá fica (o dedo do
                    # personagem é mais grosso que o guarda-mato): sobe de
                    # lado, para longe do médio, e fica estendido no corpo
                    # da arma, fora do gatilho
                    melhor = (custo, dirs, folgas, desvio, de_lado)
                    for passo in (1, 2, 3, 4, 5, 6, -1, -2, -3):
                        giro = de_lado - passo * rad(5)
                        t_, ex_, bd_ = virado(giro)
                        d2, f2, e2, c2 = self._encaixar(rig, d, t_, ex_, bd_, limite, base0, quer_i, obst, peso_la=aperta, junto=junto)
                        c2 += PESO_COPIA * 0.3 * abs(giro)
                        if c2 < melhor[0] - 1e-5:
                            melhor = (c2, d2, f2, e2, giro)
                    custo, dirs, folgas, desvio, de_lado = melhor
                    varrido = True
                if i == 0 and eixo_lado is not None and (min(folgas) < FOLGA or (custo > 0.5 and not varrido)):
                    # o polegar entra na arma, ou o de lá encosta nela e o
                    # daqui ficou no ar (a palma do personagem é mais larga):
                    # vai de lado, para longe ou na direção da arma
                    # (dentro da arma não dá para saber para que lado é
                    # "longe": tenta os dois, de 5 em 5 graus)
                    passos = [x * sg for x in range(1, 13 if aperta else 10) for sg in (1.0, -1.0)]
                    melhor = (custo, dirs, folgas, desvio, de_lado)
                    for passo in passos:
                        giro = de_lado + passo * rad(5)
                        t_, ex_, bd_ = virado(giro)
                        d2, f2, e2, c2 = self._encaixar(rig, d, t_, ex_, bd_, limite, base0, quer_i, obst, peso_la=aperta, junto=junto)
                        c2 += PESO_COPIA * 0.3 * abs(giro) + aberto(d2)
                        if c2 < melhor[0] - 1e-5:
                            melhor = (c2, d2, f2, e2, giro)
                        if melhor[0] <= 0.5:
                            break
                    custo, dirs, folgas, desvio, de_lado = melhor
                    varrido = True
                self.memoria[d] = dict(e=list(desvio), custo=custo, lado=de_lado, varrido=varrido, chato=chato,
                                       eixo_lado=(rot_mao.inverted() @ eixo_lado) if (i == 0 and eixo_lado is not None) else None)
                quer = dirs
            for k in range(3):
                osso = ossos[k]
                b = esq.base(osso, M)
                r0 = b.to_3x3()
                q = (r0 @ self.pele[d][k]['eixo']).normalized().rotation_difference(quer[k])
                esq.por(M, locais, osso, q.to_matrix() @ r0)
            # medir
            juntas = [M[ossos[k]].translation.copy() for k in range(4)]
            segs = [(juntas[k + 1] - juntas[k]).normalized() for k in range(3)]
            ang = [deg(ang_em_torno(base_dir, segs[0], eixo)), deg(ang_em_torno(segs[0], segs[1], eixo)),
                   deg(ang_em_torno(segs[1], segs[2], eixo))]
            raios = [self.pele[d][k]['raio'] for k in range(3)]
            self.ultimo[d] = segs
            postos[i] = (juntas, raios)
            for k in range(3):
                a, c = juntas[k], juntas[k + 1]
                feitos.append((a, c - a, max((c - a).length_squared, 1e-10), raios[k]))
            medidas[d] = dict(juntas=ang, folga=min(folgas) if folgas else None, fecha=deg(sum(desvio)),
                              falanges=folgas, quer=quer_i)
        # os dedos entre si (a pele de um dentro da do vizinho, m; negativo dentro)
        entre = min(self._vao(postos[i][0], postos[i][1], postos[v][0], postos[v][1]) for i, v in vizinho.items())
        medidas['_entre'] = entre
        self.feitos = feitos
        return medidas

    def dentro(self, M, rig):
        """O quanto (m, negativo) a pele dos dedos, como estão em M, entra
        na arma; 0 se não entra."""
        pior = 0.0
        m_mao = M[self.corpo.n(self.lado + 'Hand')]
        for d in DEDOS:
            tras = m_mao
            for k in range(3):
                mk = M[self.ossos[d][k]]
                f = self._folga(rig, mk, self.pele[d][k], (), tras, 0.003)
                if f < pior:
                    pior = f
                tras = mk
        return pior

    def _bases(self, mao_na_arma):
        """As pegadas de base que valem com a mão do personagem neste lugar
        da arma (a menos de 3 cm e 35° de onde estava quando cada uma foi
        fixada), da mais perto para a mais longe: no quadril e na mira a mão
        fica em lugares diferentes da arma, e cada lugar tem o seu encaixe."""
        perto = []
        for b in getattr(self.corpo, '_pegada_dedos', {}).get(self.lado, []):
            longe = (mao_na_arma.translation - b['mao'].translation).length
            giro = b['mao'].to_quaternion().rotation_difference(mao_na_arma.to_quaternion()).angle
            giro = min(giro, 2 * math.pi - giro)
            if longe < 0.03 and giro < rad(35):
                perto.append((longe, b['dedos']))
        perto.sort(key=lambda x: x[0])
        return perto

    def _base(self, mao_na_arma):
        """A pegada de base para a mão neste lugar; entre duas (passando do
        quadril para a mira), a mistura delas, pela distância a cada uma."""
        perto = self._bases(mao_na_arma)
        if not perto:
            return None
        if len(perto) == 1:
            return perto[0][1]
        (l1, b1), (l2, b2) = perto[0], perto[1]
        w = l2 / max(l1 + l2, 1e-9)
        out = {}
        for d in b1:
            if d not in b2:
                out[d] = b1[d]
                continue
            dirs = []
            for v1, v2 in zip(b1[d][1], b2[d][1]):
                v = v1 * w + v2 * (1.0 - w)
                dirs.append(v.normalized() if v.length > 1e-6 else v1)
            out[d] = (b1[d][0], dirs)
        return out

    def fixar(self, M, rig, fp, forma=None, atualizar=False):
        """Guarda o encaixe que acabou de ser feito como pegada de base
        desta mão neste lugar da arma: onde cada dedo de lá está em relação
        à arma, e para onde cada falange daqui aponta, também em relação a
        ela. Chamado no primeiro quadro de cada clipe; só guarda se ainda não
        há base para este lugar (devolve True), ou, com `atualizar`, troca as
        direções da que há pelas de agora."""
        corpo = self.corpo
        if forma not in (None, 'punho') or rig is None or not getattr(self, 'ultimo', None):
            return False
        g = rig.ref_da_mao(forma)
        g3 = g.to_3x3().normalized()
        gi = (Matrix.Translation(g.translation) @ g3.to_4x4()).inverted()
        mao = gi @ M[corpo.n(self.lado + 'Hand')]
        volta = g3.inverted()
        perto = self._bases(mao)
        ha = perto[0][1] if perto else None
        if ha is not None:
            if atualizar:
                for d in list(ha):
                    if d in self.ultimo:
                        ha[d] = (ha[d][0], [volta @ v for v in self.ultimo[d]])
                for b_ in corpo._pegada_dedos.get(self.lado, []):
                    if b_['dedos'] is ha:
                        b_['memoria'] = {k_: (dict(v_) if isinstance(v_, dict) else v_) for k_, v_ in self.memoria.items()}
            return False
        if not hasattr(corpo, '_pegada_dedos'):
            corpo._pegada_dedos = {}
        corpo._pegada_dedos.setdefault(self.lado, []).append(dict(mao=mao, dedos={
            d: ([gi @ p for p in fp['juntas'][i]], [volta @ v for v in self.ultimo[d]])
            for i, d in enumerate(DEDOS) if d in self.ultimo},
            memoria={k_: (dict(v_) if isinstance(v_, dict) else v_) for k_, v_ in self.memoria.items()}))
        return True

    @staticmethod
    def _limitar(quer, base_dir, eixo, limite):
        out = [q.copy() for q in quer]
        nomes = ('mcp', 'pip', 'dip')
        anterior = base_dir
        for k in range(3):
            a = ang_em_torno(anterior, out[k], eixo)
            lo, hi = limite[nomes[k]]
            alvo = max(rad(lo), min(rad(hi), a))
            if abs(alvo - a) > 1e-6:
                for j in range(k, 3):
                    out[j] = girar_v(out[j], eixo, alvo - a)
            anterior = out[k]
        return out


# ------------------------------------------------------------------ a postura e o lugar da arma

def quadro_mira(corpo):
    """Os eixos da mira no corpo (o personagem olha para a frente, para a
    mira, com o cano na horizontal): x para a direita dele, y para a frente,
    z para cima."""
    return Matrix((DIREITA, FRENTE, CIMA)).transposed()


def colocacao_fuzil_quadril(corpo, rig, des, M):
    """O fuzil no quadril: a coronha no bolso do ombro, o cano para a frente
    e um pouco para baixo, sem girar de lado."""
    rig.colocar(Matrix.Identity(4))
    rig.tocar('parado', 0)
    o, frente, lado, cima = rig.eixos_arma()
    coronha = rig.coronha()
    base = Matrix((lado, frente, cima)).transposed()
    f = Quaternion(DIREITA, -rad(des['cai'])) @ FRENTE
    c = (CIMA - f * CIMA.dot(f)).normalized()
    quer = Matrix((f.cross(c), f, c)).transposed()
    giro = quer @ base.inverted()
    alvo = corpo.bolso(M) + quadro_mira(corpo) @ Vector(des['coronha'])
    return Matrix.Translation(alvo) @ giro.to_4x4() @ Matrix.Translation(-coronha)


def colocacao_mira(corpo, rig, des, M, tipo):
    """Mirando: a câmera da primeira pessoa (a linha da mira) no olho direito
    do personagem, olhando para a frente; a arma `longe` à frente do olho, ou
    (fuzil) até a coronha chegar no ombro."""
    rig.colocar(Matrix.Identity(4))
    rig.tocar('mira', 0)
    olho = corpo.olho(M)
    R = quadro_mira(corpo).to_4x4()
    if des.get('longe') is not None:
        d = des['longe']
    else:
        coronha = rig.coronha()
        bolso = corpo.bolso(M)
        # a coronha (no espaço do olho de lá) e o bolso (no do olho de cá),
        # ao longo da mira
        d = (bolso - olho).dot(FRENTE) - coronha.y + 0.01
    # a linha da mira um pouco abaixo do olho (de fora não se vê; a coronha
    # no ombro se vê): `desce` (x direita, y frente, z cima)
    desce = quadro_mira(corpo) @ Vector(des.get('desce', (0.0, 0.0, 0.0)))
    return Matrix.Translation(olho + FRENTE * d + desce) @ R


def colocacao_cabo(corpo, rig, des, M):
    """Pistola no quadril, faca, granada: o cabo (o osso da arma) num ponto
    na frente do peito; o conjunto com os eixos da câmera nos do corpo,
    inclinado `cai` graus para baixo (a mão e a arma como o animador
    deixou)."""
    rig.colocar(Matrix.Identity(4))
    rig.tocar('parado', 0)
    g = rig.p('ik_hand_gun')
    d, e = M[corpo.n('RightArm')].translation, M[corpo.n('LeftArm')].translation
    centro = (d + e) * 0.5
    alvo = centro + quadro_mira(corpo) @ Vector(des['cabo'])
    R = (Quaternion(DIREITA, -rad(des['cai'])).to_matrix() @ quadro_mira(corpo)).to_4x4()
    return Matrix.Translation(alvo) @ R @ Matrix.Translation(-g)


class Plano:
    """Tudo o que vale para uma arma num personagem: as posturas (as
    rotações locais do tronco, da cabeça e das clavículas) e onde o conjunto
    fica em cada uma."""

    def __init__(self, corpo, rig, arma):
        self.arma = arma
        self.tipo = ARMAS[arma]
        self.posturas = {}
        self.colocacoes = {}
        self.desenhos = {}
        self.pegadas = {}
        self.relato = {}
        rig.preparar_eixos()
        # O fuzil: o tronco vira mais (o ombro esquerdo vai para a frente) até
        # a mão de apoio chegar onde a da primeira pessoa segura, sem deslizar
        # para trás no guarda-mão (GIRO_A_MAIS). O Barut tem o braço curto e o
        # ombro largo: com o giro de base a mão dele ficava na boca do
        # carregador.
        giros = GIRO_A_MAIS if self.tipo == 'fuzil' else (0.0,)
        melhor = None
        for giro in giros:
            custo = self._montar(corpo, rig, giro)
            if melhor is None or custo < melhor[0] - 1e-6:
                melhor = (custo, giro)
            if custo <= 0.0:
                break
        if melhor[1] != giro:
            self._montar(corpo, rig, melhor[1])
        if self.tipo == 'fuzil':
            self.relato['giro_a_mais'] = melhor[1]

    def _montar(self, corpo, rig, giro):
        """As posturas e as pegadas com o tronco virado `giro` graus a mais;
        devolve o quanto a mão de apoio fica devendo (o que falta para
        chegar e o que deslizou além de DESLIZA_BOM), 0 se está bom."""
        self.posturas, self.colocacoes, self.desenhos, self.pegadas = {}, {}, {}, {}
        self.relato = {}
        for estado, des in POSTURA[self.tipo].items():
            des = dict(des)
            if giro:
                t = des['tronco']
                des['tronco'] = (t[0] + giro, t[1], t[2])
            if estado == 'mira' and self.tipo == 'fuzil':
                des = self._mira_fuzil(corpo, rig, des)
            if estado == 'mira' and self.tipo == 'pistola':
                des = self._mira_pistola(corpo, rig, des)
            if estado == 'quadril' and self.tipo == 'pistola':
                des = self._esticar_pistola(corpo, rig, des)
            locais = corpo.postura_base(des)
            M = corpo.esq.fk(locais)
            col = self._colocar(corpo, rig, des, M, estado)
            self.posturas[estado] = locais
            self.colocacoes[estado] = col
            self.desenhos[estado] = des
            self.pegadas[estado] = self._pegadas(corpo, rig, des, M, col, estado)
        custo = 0.0
        for estado in self.posturas:
            r = self.relato.get(f'{estado}_esquerda')
            if r:
                # ...e o quanto a palma fica mal assentada naquele lugar da arma
                custo += r['longe'] * 10 + max(0.0, r['desliza'] - DESLIZA_BOM) + max(0.0, r.get('assento', 0.0) - 0.3) * 0.1
        return custo

    def _colocar(self, corpo, rig, des, M, estado):
        if self.tipo == 'fuzil' and estado == 'quadril':
            return colocacao_fuzil_quadril(corpo, rig, des, M)
        if estado == 'mira':
            return colocacao_mira(corpo, rig, des, M, self.tipo)
        return colocacao_cabo(corpo, rig, des, M)

    def _mira_fuzil(self, corpo, rig, des):
        """Mirando o fuzil, o olho na linha da mira e a coronha no ombro ao
        mesmo tempo: a cabeça desce e deita na coronha, o ombro direito sobe
        — procura o tanto de cada um que encosta a coronha no bolso do ombro."""
        melhor = None
        for hp in (6, 10, 14, 18, 22):
            for hr in (0, 4, 8, 12, 16):
                for ce in (2, 6, 10, 14):
                    d2 = dict(des)
                    d2['cabeca'] = (hp, hr)
                    d2['ombro_d'] = (ce, des['ombro_d'][1])
                    locais = corpo.postura_base(d2)
                    M = corpo.esq.fk(locais)
                    d2['desce'] = (0.0, 0.0, 0.0)
                    rig.colocar(colocacao_mira(corpo, rig, d2, M, 'fuzil'))
                    rig.tocar('mira', 0)
                    erro = corpo.bolso(M) - rig.coronha()
                    erro = erro - FRENTE * erro.dot(FRENTE)
                    # a arma desce (até DESCE_MAX) e vai de lado (até 3 cm)
                    # para a coronha chegar; o que sobrar é erro
                    z = max(-DESCE_MAX, min(0.0, erro.dot(CIMA)))
                    x = max(-0.04, min(0.04, erro.dot(DIREITA)))
                    sobra = erro - CIMA * z - DIREITA * x
                    custo = sobra.length * 100 + abs(z) * 25 + abs(x) * 25 + hp * 0.04 + hr * 0.06 + ce * 0.05
                    if melhor is None or custo < melhor[0]:
                        d2['desce'] = (x, 0.0, z)
                        melhor = (custo, d2, sobra.length, z)
        self.relato['coronha_mira_cm'] = round(melhor[2] * 100, 1)
        self.relato['mira_abaixo_do_olho_cm'] = round(-melhor[3] * 100, 1)
        self.relato['cabeca_mira'] = melhor[1]['cabeca']
        self.relato['ombro_mira'] = melhor[1]['ombro_d']
        return melhor[1]

    def _esticar_pistola(self, corpo, rig, des):
        """A pistola parada com os braços esticados para a frente (Jhon, 23/11: "a
        pistola deve esticar o braço da eve até na frente"): o cabo o mais longe do
        peito que as duas mãos alcançam, com o cotovelo um pouco dobrado, na altura
        em que estava."""
        locais = corpo.postura_base(des)
        M = corpo.esq.fk(locais)
        melhor = None
        for i in range(0, 41):
            y = 0.30 + i * 0.01
            d2 = dict(des)
            d2['cabo'] = (des['cabo'][0], y, des['cabo'][2] + PISTOLA_ALTURA)
            rig.colocar(colocacao_cabo(corpo, rig, d2, M))
            rig.tocar('parado', 0)
            esticado = 0.0
            for lado in ('Right', 'Left'):
                fp = rig.mao(lado)
                pulso, _ = alvo_mao(corpo, rig, lado, fp)
                br = Braco(corpo, lado)
                esticado = max(esticado, (pulso - M[br.up].translation).length / br.alcance())
            if esticado <= PISTOLA_ESTICADO:
                melhor = d2
        self.relato['pistola_parada_cabo_y'] = melhor['cabo'][1] if melhor else des['cabo'][1]
        return melhor or des

    def _mira_pistola(self, corpo, rig, des):
        """Mirando a pistola: os braços quase esticados (o mais longe do olho
        que as duas mãos alcançam com o cotovelo um pouco dobrado)."""
        locais = corpo.postura_base(des)
        M = corpo.esq.fk(locais)
        melhor = None
        for i in range(0, 15):
            d = 0.22 + i * 0.02
            d2 = dict(des)
            d2['longe'] = d
            rig.colocar(colocacao_mira(corpo, rig, d2, M, 'pistola'))
            rig.tocar('mira', 0)
            esticado = 0.0
            for lado in ('Right', 'Left'):
                fp = rig.mao(lado)
                pulso, _ = alvo_mao(corpo, rig, lado, fp)
                br = Braco(corpo, lado)
                esticado = max(esticado, (pulso - M[br.up].translation).length / br.alcance())
            if esticado <= 0.93:
                melhor = d2
        self.relato['pistola_longe'] = melhor['longe'] if melhor else des['longe']
        return melhor or des

    def _pegadas(self, corpo, rig, des, M, col, estado):
        """O giro (e o deslize) de cada mão na arma, no quadro de base da
        postura."""
        clipe = 'mira' if estado == 'mira' else 'parado'
        rig.colocar(col)
        rig.tocar(clipe, 0)
        out = {}
        fpd, fpe = rig.mao('Right'), rig.mao('Left')
        br_d, br_e = Braco(corpo, 'Right'), Braco(corpo, 'Left')
        alto = 0.03 if self.tipo != 'granada' else 0.15
        pd = Pegada(rig, 'Right', fpd, 'cabo')
        antes = self.pegadas.get('quadril', {}) if estado == 'mira' else {}
        perto_d = (antes['Right'].angulo, antes['Right'].guina) if 'Right' in antes else None
        perto_e = (antes['Left'].angulo, antes['Left'].guina) if 'Left' in antes else None
        rd = ajustar_pegada(corpo, br_d, rig, pd, M, fpd, des['cotovelo_d'], corpo.mao['Right'], alto, False, perto=perto_d)
        out['Right'] = pd
        self.relato[f'{estado}_direita'] = {k: round(v, 3) for k, v in rd.items()}
        if self.tipo == 'fuzil':
            pe = Pegada(rig, 'Left', fpe, 'guarda')
            re_ = None
            if estado == 'mira' and 'Left' in self.pegadas.get('quadril', {}):
                # na mira a mão de apoio fica onde estava no quadril, se
                # alcança: não anda no guarda-mão quando se mira
                pe.desliza = 0.0
                re_ = ajustar_pegada(corpo, br_e, rig, pe, M, fpe, des['cotovelo_e'], corpo.mao['Left'], 0.03, True,
                                     deslize_fixo=self.pegadas['quadril']['Left'].desliza, perto=perto_e)
                if re_['longe'] > 0.005:
                    re_ = None
            if re_ is None:
                re_ = ajustar_pegada(corpo, br_e, rig, pe, M, fpe, des['cotovelo_e'], corpo.mao['Left'], 0.03, True, perto=perto_e)
            out['Left'] = pe
            self.relato[f'{estado}_esquerda'] = {k: round(v, 3) for k, v in re_.items()}
        elif self.tipo == 'pistola':
            # a esquerda abraça a direita: gira junto com ela, no mesmo eixo
            pe = Pegada(rig, 'Left', fpe, 'cabo')
            pe.centro_local, pe.eixo_local = pd.centro_local.copy(), pd.eixo_local.copy()
            pe.angulo = pd.angulo
            out['Left'] = pe
        return out

    def estado(self, clipe):
        if clipe in NA_MIRA and 'mira' in self.posturas:
            return 'mira'
        return 'quadril'


# ------------------------------------------------------------------ faca e granada: o corpo inteiro

# A faca: o golpe da primeira pessoa é um movimento só de mão e pulso (a
# câmera não deixa ver mais); no corpo o tronco gira com a mão, inclina no
# golpe e o caminho da mão cresce (AMPLIA). A esquerda fica na guarda, alta,
# na frente do peito (os fuzileiros: "the left hand vertically as a shield
# to protect the ribs, head and neck").
FACA = dict(amplia=1.35, giro_por_m=150.0, giro_max=35.0, inclina_por_m=50.0, guarda=(0.0, 0.04, 0.13))

# A granada, no peito (x direita, y frente, z cima, a partir do meio dos
# ombros): a mão armada atrás da orelha, soltando na frente acima da cabeça,
# o acompanhamento embaixo à esquerda; a esquerda aponta o alvo e, no lance,
# vai para o quadril. O tronco gira para trás ao armar e para a frente no
# lance (o manual: de lado para o alvo, o braço livre a 45° apontando, o
# lance por cima e o braço seguindo depois de soltar).
GRANADA = dict(
    # a palma direita (com a granada) em cada ponto do lance
    armado=(0.28, -0.22, 0.16), arco=(0.32, 0.08, 0.30), solta=(0.18, 0.44, 0.36), segue=(-0.30, 0.32, -0.42),
    # depois de soltar a mão segue para a frente e para baixo, e só então
    # cruza o corpo (não passa na frente do rosto)
    desce=(0.16, 0.66, -0.12),
    # a esquerda: aponta o alvo armada; no lance puxa para o lado do quadril
    # (o cotovelo vai para trás e ajuda o tronco a girar)
    aponta=(-0.10, 0.56, 0.08), quadril=(-0.36, -0.10, -0.26),
    # o tronco (graus; giro + para a esquerda dele, inclina + para a frente):
    # armado, de lado para o alvo (o ombro direito para trás); no lance gira
    # para a frente e passa do alvo, e se dobra no fim
    giro_armado=-45.0, giro_lance=42.0, inclina_armado=-6.0, inclina_lance=20.0,
    # para onde o cotovelo aponta (fora, cima, trás): armado, para o lado e
    # um pouco para trás; no lance ele vai na frente da mão
    cotovelo_armado=(0.75, 0.10, 0.65), cotovelo_solta=(0.70, 0.35, -0.20),
    cotovelo_quadril_e=(0.55, -0.35, 0.75),
    # cada mão em cada ponto: para onde vão os dedos e para onde a palma
    # olha (x direita, y frente, z cima)
    mao_armado=((-0.15, -0.35, 0.75), (0.0, 0.9, 0.25)),
    mao_solta=((0.0, 0.8, 0.6), (0.0, 0.6, -0.8)),
    mao_segue=((-0.3, 0.4, -0.85), (0.9, -0.3, -0.3)),
    mao_aponta_e=((0.2, 0.9, 0.15), (0.6, 0.0, -0.7)),
    mao_quadril_e=((0.10, 0.45, -0.88), (0.95, -0.25, 0.0)),
    # a esquerda solta ao lado do corpo
    solta_e=(-0.24, 0.04, -0.46), mao_solta_e=((0.05, 0.25, -1.0), (1.0, 0.0, 0.0)))
# o lance (segundos da primeira pessoa): solta a granada, termina o
# acompanhamento, volta ao descanso
LANCE = (0.11, 0.30, 0.55)


def girar_tronco(corpo, locais, giro, inclina, cabeca=0.7, cabeca_inclina=0.0):
    """Gira o tronco (graus: + para a esquerda dele) e inclina (+ para a
    frente), repartido pelas três vértebras; a cabeça desfaz `cabeca` do
    giro (ela continua olhando para o alvo)."""
    if abs(giro) < 1e-3 and abs(inclina) < 1e-3:
        return
    M = corpo.esq.fk(locais)
    q = Quaternion(CIMA, rad(giro / 3)) @ Quaternion(DIREITA, rad(-inclina / 3))
    for nome in ('Spine', 'Spine1', 'Spine2'):
        n_ = corpo.n(nome)
        corpo.esq.por(M, locais, n_, q.to_matrix() @ M[n_].to_3x3())
    volta = Quaternion(CIMA, rad(-giro * cabeca / 2)) @ Quaternion(DIREITA, rad(inclina * cabeca_inclina / 2))
    for nome in ('Neck', 'Head'):
        n_ = corpo.n(nome)
        corpo.esq.por(M, locais, n_, volta.to_matrix() @ M[n_].to_3x3())


def no_peito(corpo, locais, v):
    """Um ponto dado no peito (x direita, y frente, z cima, do meio dos
    ombros na postura de base) no mundo."""
    M = corpo.esq.fk(locais)
    centro = (M[corpo.n('RightArm')].translation + M[corpo.n('LeftArm')].translation) * 0.5
    return centro + quadro_mira(corpo) @ Vector(v)


def quadro_mao(corpo, locais, ponto, eixos):
    """A palma (ponto e orientação) num ponto do peito com os dedos e a
    palma nas direções dadas (x direita, y frente, z cima)."""
    Q = quadro_mira(corpo)
    rot = orient(Q @ Vector(eixos[0]), Q @ Vector(eixos[1]))
    return no_peito(corpo, locais, ponto), rot


def coreografia(corpo, rig, plano, clipe, k, n, q, info):
    """O que a faca e a granada fazem além da primeira pessoa (veja FACA e
    GRANADA). Mexe na postura do quadro, onde o conjunto vai, e diz a forma
    de cada mão e para onde a esquerda vai."""
    tipo = plano.tipo
    t = k / FPS
    if tipo == 'faca':
        q['forma_d'], q['forma_e'] = 'punho', 'relaxada'
        rig.tocar(clipe, k)
        rig.colocar(q['col'])
        delta = rig.mao('Right')['palma'] - info['palma0']
        q['col'] = Matrix.Translation(delta * (FACA['amplia'] - 1)) @ q['col']
        delta = delta * FACA['amplia']
        giro = max(-FACA['giro_max'], min(FACA['giro_max'], -delta.dot(DIREITA) * FACA['giro_por_m']))
        inclina = max(-4.0, min(8.0, delta.dot(FRENTE) * FACA['inclina_por_m']))
        girar_tronco(corpo, q['locais'], giro, inclina)
        q['esquerda'] = ('desloca', quadro_mira(corpo) @ Vector(FACA['guarda']))
        return
    if tipo != 'granada':
        return
    q['forma_d'] = 'punho'
    q['forma_e'] = None
    G = GRANADA
    rig.tocar(clipe, k)
    rig.colocar(q['col'])
    fp = rig.mao('Right')
    palma = fp['palma']
    rot_fp = orient(fp['ao_longo'], fp['dentro'])
    L = q['locais']
    alvo = None    # (ponto, orientação, peso)
    giro = 0.0
    inclina = 0.0
    solta_e = quadro_mao(corpo, L, G['solta_e'], G['mao_solta_e'])
    esquerda = ('quadro', solta_e[0], 1.0, solta_e[1])
    forma_e = 'relaxada'
    def mistura(a, b, u):
        return a[0].lerp(b[0], u), a[1].to_quaternion().slerp(b[1].to_quaternion(), u).to_matrix()

    def entre(a, b, u):
        return tuple(x + (y - x) * u for x, y in zip(a, b))
    aponta_e = quadro_mao(corpo, L, G['aponta'], G['mao_aponta_e'])
    quadril_e = quadro_mao(corpo, L, G['quadril'], G['mao_quadril_e'])
    if clipe == 'armar':
        w = suave((t - 0.36) / 0.26)
        p_, r_ = quadro_mao(corpo, L, G['armado'], G['mao_armado'])
        alvo = (p_, r_, w)
        giro = G['giro_armado'] * w
        inclina = G['inclina_armado'] * w
        q['cotovelo_d'] = G['cotovelo_armado'] if w > 0.3 else None
        # até ~0.36 s a esquerda puxa o pino (a mão de lá, na granada);
        # depois aponta o alvo
        wa = suave((t - 0.24) / 0.30)
        wp = 1.0 - suave((t - 0.02) / 0.10)
        if wa > 0:
            esquerda = ('quadro', aponta_e[0], wa, aponta_e[1])
            forma_e = 'aberta' if wa > 0.5 else None
        elif wp > 0:
            esquerda = ('quadro', solta_e[0], wp, solta_e[1])
            forma_e = 'relaxada' if wp > 0.5 else None
        else:
            esquerda = None
            forma_e = None
    elif clipe == 'armado':
        p_, r_ = quadro_mao(corpo, L, G['armado'], G['mao_armado'])
        alvo = (p_ + (palma - info['palma_armado0']), r_, 1.0)
        giro = G['giro_armado']
        inclina = G['inclina_armado']
        q['cotovelo_d'] = G['cotovelo_armado']
        esquerda = ('quadro', aponta_e[0], 1.0, aponta_e[1])
        forma_e = 'aberta'
    elif clipe == 'arremesso':
        T1, T2, T3 = LANCE
        pa_, ra_ = quadro_mao(corpo, L, G['armado'], G['mao_armado'])
        ps_, rs_ = quadro_mao(corpo, L, G['solta'], G['mao_solta'])
        pg_, rg_ = quadro_mao(corpo, L, G['segue'], G['mao_segue'])
        pf_, rf_ = info['palma_parado'], info['rot_parado']
        # o tronco puxa o lance: sai rápido e chega no fim do acompanhamento
        # (na soltura já passou do meio); a mão vem atrás e chicoteia
        if t <= T2:
            f = suave(min(1.0, t / T2) ** 0.5)
            giro = G['giro_armado'] + (G['giro_lance'] - G['giro_armado']) * f
            inclina = G['inclina_armado'] + (G['inclina_lance'] - G['inclina_armado']) * f
        else:
            u = suave((t - T2) / (T3 - T2))
            giro = G['giro_lance'] * (1 - u)
            inclina = G['inclina_lance'] * (1 - u)
        if t <= T1:
            # por cima do ombro, ao lado da cabeça, até a frente
            u = (t / T1) ** 1.6
            arco = no_peito(corpo, L, G['arco'])
            p_ = pa_ * (1 - u) ** 2 + arco * (2 * u * (1 - u)) + ps_ * u * u
            r_ = mistura((pa_, ra_), (ps_, rs_), u)[1]
            q['cotovelo_d'] = entre(G['cotovelo_armado'], G['cotovelo_solta'], suave(t / T1))
        elif t <= T2:
            # o acompanhamento: rápido na saída, freando, cruzando o corpo
            x = (t - T1) / (T2 - T1)
            u = 1 - (1 - x) ** 2
            desce = no_peito(corpo, L, G['desce'])
            p_ = ps_ * (1 - u) ** 2 + desce * (2 * u * (1 - u)) + pg_ * u * u
            r_ = mistura((ps_, rs_), (pg_, rg_), u)[1]
            q['cotovelo_d'] = G['cotovelo_solta'] if x < 0.35 else None
        else:
            u = suave((t - T2) / (T3 - T2))
            p_, r_ = mistura((pg_, rg_), (pf_, rf_), u)
        alvo = (p_, r_, 1.0)
        # a esquerda puxa junto com o tronco e fica do lado; depois solta
        if t <= T2:
            w = suave(min(1.0, t / 0.16))
            e_ = mistura(aponta_e, quadril_e, w)
            esquerda = ('quadro', e_[0], 1.0, e_[1])
            forma_e = 'aberta' if w < 0.4 else 'relaxada'
            if w > 0.3:
                q['cotovelo_e'] = G['cotovelo_quadril_e']
        else:
            w = suave((t - T2) / (T3 - T2))
            e_ = mistura(quadril_e, solta_e, w)
            esquerda = ('quadro', e_[0], 1.0, e_[1])
            if w < 0.6:
                q['cotovelo_e'] = G['cotovelo_quadril_e']
    girar_tronco(corpo, L, giro, inclina, cabeca=0.85, cabeca_inclina=0.6)
    if alvo is not None and alvo[2] > 1e-4:
        # o conjunto vai junto: a palma de lá cai no quadro pedido (a
        # granada continua na palma)
        p_, r_, w = alvo
        p_ = palma.lerp(p_, w)
        r_ = rot_fp.to_quaternion().slerp(r_.to_quaternion(), w).to_matrix()
        F_t = Matrix.Translation(p_) @ r_.to_4x4()
        F_fp = Matrix.Translation(palma) @ rot_fp.to_4x4()
        q['col'] = F_t @ F_fp.inverted() @ q['col']
    q['esquerda'] = esquerda
    q['forma_e'] = forma_e


def misturar_locais(a, b, w):
    out = {}
    for n in set(a) | set(b):
        qa, qb = a.get(n, Quaternion()), b.get(n, Quaternion())
        out[n] = qa.slerp(qb, w)
    return out


def misturar_matriz(a, b, w):
    ta, tb = a.translation, b.translation
    qa, qb = a.to_quaternion(), b.to_quaternion()
    return Matrix.Translation(ta.lerp(tb, w)) @ qa.slerp(qb, w).to_matrix().to_4x4()


# ------------------------------------------------------------------ um clipe

class Clipe:
    def __init__(self, nome, quadros):
        self.nome = nome
        self.quadros = quadros
        self.ossos = []      # por quadro: {nome: Quaternion (jogo)}
        self.conjunto = []   # por quadro: Matrix (jogo, no espaço do Spine2)
        self.medidas = []
        self.locais = []     # por quadro: {osso: Quaternion} (Blender), para o .blend
        self.colocacoes = []  # por quadro: onde o conjunto ficou (Blender)


def progresso_transicao(rig, clipe, n):
    """Quanto a arma já andou de uma postura para a outra em cada quadro
    (0 a 1), pelo caminho da arma da primeira pessoa."""
    pos = []
    for k in range(n + 1):
        rig.tocar(clipe, k)
        pos.append(rig.p('ik_hand_gun').copy())
    total = (pos[-1] - pos[0]).length
    if total < 1e-4:
        return [suave(k / max(1, n)) for k in range(n + 1)]
    ida = [min(1.0, max(0.0, (p - pos[0]).dot(pos[-1] - pos[0]) / (total * total))) for p in pos]
    return ida


def resolver_clipe(corpo, rig, plano, clipe, medir_penetracao=False):
    n = rig.quadros(clipe)
    tipo = plano.tipo
    braco_d, braco_e = Braco(corpo, 'Right'), Braco(corpo, 'Left')
    dedos_d, dedos_e = Dedos(corpo, 'Right'), Dedos(corpo, 'Left')
    trans = TRANSICOES.get(clipe)
    progresso = progresso_transicao(rig, clipe, n) if trans else None
    estado = plano.estado(clipe)
    out = Clipe(clipe, n + 1)
    alto_d = 0.03 if tipo != 'granada' else 0.24

    # 1) a base de cada quadro: a postura, onde o conjunto fica, a pegada
    quadros = []
    for k in range(n + 1):
        if trans and trans[0] in plano.posturas and trans[1] in plano.posturas:
            w = suave(progresso[k])
            base = misturar_locais(plano.posturas[trans[0]], plano.posturas[trans[1]], w)
            col = misturar_matriz(plano.colocacoes[trans[0]], plano.colocacoes[trans[1]], w)
            est = trans[1] if w > 0.5 else trans[0]
            w_mira = w if trans[1] == 'mira' else (1.0 - w if trans[0] == 'mira' else 0.0)
        else:
            base = plano.posturas[estado]
            col = plano.colocacoes[estado]
            est = estado
            w_mira = 1.0 if estado == 'mira' else 0.0
        locais = dict(base)
        rig.tocar(clipe, k)
        # a cabeça do animador: inclina (x), gira (y) e rola (z), em graus
        cam = rig.camera()
        if cam.length > 0.01:
            q = (Quaternion(CIMA, rad(cam.y)) @ Quaternion(DIREITA, rad(cam.x)) @ Quaternion(FRENTE, rad(-cam.z)))
            meia = Quaternion().slerp(q, 0.5)
            M0 = corpo.esq.fk(locais)
            for nome in (corpo.n('Neck'), corpo.n('Head')):
                corpo.esq.por(M0, locais, nome, meia.to_matrix() @ M0[nome].to_3x3())
        quadros.append(dict(locais=locais, col=col, est=est, w_mira=w_mira, forma_d=None, forma_e=None, esquerda=None))

    # faca e granada: o corpo inteiro
    if tipo in ('faca', 'granada'):
        info = {}
        rig.colocar(plano.colocacoes['quadril'])
        rig.tocar('parado', 0)
        fp0 = rig.mao('Right')
        info['palma0'] = fp0['palma'].copy()
        info['palma_parado'] = info['palma0']
        info['rot_parado'] = orient(fp0['ao_longo'], fp0['dentro'])
        if 'armado' in rig.clipes():
            rig.tocar('armado', 0)
            info['palma_armado0'] = rig.mao('Right')['palma'].copy()
        for k, q in enumerate(quadros):
            coreografia(corpo, rig, plano, clipe, k, n, q, info)

    # 2) a arma chega perto do corpo quando um braço não alcança a mão de lá
    # (o personagem tem o braço mais curto: na recarga ele traz o fuzil para
    # perto, como manda o manual — "draw the weapon in close")
    falta = []
    for k, q in enumerate(quadros):
        M = corpo.esq.fk(q['locais'])
        rig.tocar(clipe, k)
        rig.colocar(q['col'])
        empurra = Vector()
        for lado, br in (('Right', braco_d), ('Left', braco_e)):
            fp = rig.mao(lado)
            pulso, rot = alvo_mao(corpo, rig, lado, fp)
            peg = plano.pegadas[q['est']].get(lado)
            if peg is not None:
                pulso, rot = peg.aplicar(rig, fp, pulso, rot, mao=corpo.mao[lado])
            S = M[br.up].translation
            d = (pulso - S).length - br.alcance() * 0.965
            if d > 0:
                v = (S - pulso).normalized() * d
                if v.length > empurra.length:
                    empurra = v
        falta.append(empurra)
    # o máximo na janela, depois suave (sem tranco)
    janela = 5
    picos = []
    for k in range(len(falta)):
        viz = falta[max(0, k - janela):k + janela + 1]
        picos.append(max(viz, key=lambda v: v.length))
    suaves = []
    for k in range(len(picos)):
        soma, peso = Vector(), 0.0
        for j in range(max(0, k - 4), min(len(picos), k + 5)):
            w_ = math.exp(-((j - k) / 2.5) ** 2)
            soma += picos[j] * w_
            peso += w_
        suaves.append(soma / peso)
    for q, s in zip(quadros, suaves):
        if s.length > 1e-5:
            q['col'] = Matrix.Translation(s) @ q['col']

    # 3) cada quadro
    escolha = {'Right': None, 'Left': None}
    for k, q in enumerate(quadros):
        locais = q['locais']
        col = q['col']
        M = corpo.esq.fk(locais)
        rig.tocar(clipe, k)
        rig.colocar(col)
        pegadas = plano.pegadas[q['est']]
        des_k = plano.desenhos[q['est']]
        medidas = {}
        fps_ = {}
        for lado, br, pref, alto in (('Right', braco_d, q.get('cotovelo_d') or des_k['cotovelo_d'], alto_d),
                                      ('Left', braco_e, q.get('cotovelo_e') or des_k['cotovelo_e'], 0.03)):
            fp = rig.mao(lado)
            fps_[lado] = fp
            pulso, rot = alvo_mao(corpo, rig, lado, fp)
            peg = pegadas.get(lado)
            if peg is not None:
                w = peg.peso(rig, fp)
                if w > 0.3:
                    # o jeito de pegar, quadro a quadro, perto do da postura
                    # (a arma gira na recarga; a mão gira em volta do cabo).
                    # Pouco (GIRA_NA_ARMA) e só se o punho pedir muito: na
                    # primeira pessoa a mão não sai do lugar na arma, e aqui
                    # ela escorregava até 5 cm e 30° no saque — os dedos não
                    # tinham como ficar encaixados.
                    melhor = None
                    # (a faca e a granada não giram na mão: lá a mão não sai
                    # do lugar nelas nem 1,5 mm, em clipe nenhum)
                    for da in ((0,) if (lado == 'Right' and q.get('forma_d') == 'punho') else GIRA_NA_ARMA):
                        for dg in (0,):
                            a = peg.angulo + rad(da)
                            gu = peg.guina + rad(dg)
                            p2, r2 = peg.aplicar(rig, fp, pulso, rot, a, None, None, gu, corpo.mao[lado])
                            r = br.avaliar(M, p2, r2, pref, corpo.mao[lado], alto, 1.0 if k else 0.0,
                                           passo=7.5, alvo=(fp['dobra'], fp['lado']))
                            c = r['custo'] + abs(da) * 0.3 + abs(dg) * 0.05
                            if escolha[lado] is not None:
                                c += (abs(deg(a) - escolha[lado][0]) + abs(deg(gu) - escolha[lado][1])) * 0.25
                            if melhor is None or c < melhor[0]:
                                melhor = (c, a, gu)
                    _, a, gu = melhor
                    escolha[lado] = (deg(a), deg(gu))
                    pulso, rot = peg.aplicar(rig, fp, pulso, rot, a, None, None, gu, corpo.mao[lado])
                else:
                    pulso, rot = peg.aplicar(rig, fp, pulso, rot, mao=corpo.mao[lado])
            if lado == 'Left' and q.get('esquerda'):
                e = q['esquerda']
                if e[0] == 'desloca':
                    pulso = pulso + e[1]
                elif e[0] == 'quadro':
                    mi = corpo.mao['Left']
                    quer = e[3] @ orient(mi['dedos'], mi['palma']).inverted()
                    p_alvo = e[1] - quer @ (mi['nos'] * ALINHA)
                    w = e[2]
                    pulso = pulso.lerp(p_alvo, w)
                    rot = Quaternion().slerp((quer @ rot.inverted()).to_quaternion(), w).to_matrix() @ rot
                elif e[0] == 'alvo':
                    # a palma da esquerda num ponto dado, a mão para a frente
                    # (dedos para o alvo, palma para baixo e para dentro)
                    mi = corpo.mao['Left']
                    quer = orient((FRENTE * 0.9 + CIMA * 0.15 + DIREITA * 0.2).normalized(),
                                  (-CIMA * 0.7 + DIREITA * 0.6).normalized()) @ orient(mi['dedos'], mi['palma']).inverted()
                    p_alvo = e[1] - quer @ (mi['nos'] * ALINHA)
                    w = e[2]
                    pulso = pulso.lerp(p_alvo, w)
                    rot = Quaternion().slerp((quer @ rot.inverted()).to_quaternion(), w).to_matrix() @ rot
            # a granada e a mão livre: o punho que a primeira pessoa tem não
            # serve (a mão foi posta à mão); reto, e nunca além do que dobra
            livre = tipo == 'granada' or (tipo == 'faca' and lado == 'Left')
            alvo_p = (0.0, 0.0) if livre else (fp['dobra'], fp['lado'])
            if livre:
                antes = rot
                rot = limitar_punho(br, M, pulso, rot, pref, corpo.mao[lado], alto, 1.0 if k else 0.0, alvo_p)
                if lado == 'Right':
                    # a granada vai junto com a palma
                    F_novo = Matrix.Translation(pulso) @ rot.to_4x4()
                    F_velho = Matrix.Translation(pulso) @ antes.to_4x4()
                    col = F_novo @ F_velho.inverted() @ col
                    q['col'] = col
                    rig.colocar(col)
                    fps_['Right'] = fp = rig.mao('Right')
            if not (tipo in ('faca', 'granada') and lado == 'Left'):
                punho_ = lado == 'Right' and q.get('forma_d') == 'punho'
                # O punho fechado no cabo (faca, granada): a mão fica presa na
                # arma onde assentou da primeira vez, em todos os quadros de
                # todos os clipes, como lá. Assentada de novo a cada quadro,
                # nos golpes da faca ela saía até 1,7 cm do cabo e os dedos
                # (colados na arma) entravam 1 cm nele.
                preso = getattr(corpo, '_punho_preso', None) if punho_ else None
                g_ = rig.ref_da_mao('punho' if punho_ else None)
                na_arma_ = g_.inverted() @ fp['palma']
                if preso is not None and (rig.firme('punho') or (preso[0] - na_arma_).length < 0.004 * rig.escala):
                    h_ = g_ @ preso[1]
                    pulso = h_.translation.copy()
                    rot = h_.to_3x3().normalized()
                else:
                    pulso = afastar_palma(corpo, rig, lado, pulso, rot, fp, encostar=punho_,
                                          aninhar=punho_ and tipo == 'faca', apoio=tipo == 'fuzil' and lado == 'Left')
                    if punho_ and preso is None:
                        corpo._punho_preso = (na_arma_, g_.inverted() @ (Matrix.Translation(pulso) @ rot.to_4x4()))
            if tipo == 'pistola' and lado == 'Left' and q.get('forma_e') is None:
                # a de apoio abraça a do gatilho, que já está posta: os
                # dedos dela primeiro, e a palma desta por fora deles
                dedos_d.resolver(M, locais, rig, fps_['Right'], forma=q.get('forma_d'))
                pulso, recuou_ = afastar_da_outra(corpo, rig, 'Left', pulso, rot, dedos_d.feitos)
                q['recuou_e'] = recuou_
            medidas[lado] = br.resolver(M, locais, pulso, rot, pref, corpo.mao[lado], cotovelo_max_z=alto,
                                        peso_tempo=1.0 if k else 0.0, alvo=alvo_p)
            if lado == 'Right' and q.get('forma_d') == 'punho':
                # A faca e a granada vão na mão: se o braço não chega aonde a
                # mão devia ir (o golpe ampliado passa do alcance), é a arma
                # que vem até a mão, e não a mão que larga o cabo.
                real = M[br.ha]
                real = Matrix.Translation(real.translation) @ real.to_3x3().normalized().to_4x4()
                falta_ = real @ (Matrix.Translation(pulso) @ rot.to_4x4()).inverted()
                if falta_.translation.length > 2e-4 or falta_.to_quaternion().angle > 2e-3:
                    col = falta_ @ col
                    q['col'] = col
                    rig.colocar(col)
                    fps_['Right'] = rig.mao('Right')
        # no primeiro quadro o encaixe assenta (cada dedo se ajeita ao
        # vizinho a partir do que valeu antes): repete até parar, senão o
        # clipe começava com os dedos se arrumando e o laço não emendava
        for _ in range(4 if k == 0 else 1):
            dd = dedos_d.resolver(M, locais, rig, fps_['Right'], forma=q.get('forma_d'))
            de = dedos_e.resolver(M, locais, rig, fps_['Left'], forma=q.get('forma_e'), outra=dedos_d.feitos,
                                  fp_outra=fps_['Right'] if q.get('forma_e') is None else None)
        if k == 0:
            # a pegada de base; se foi criada agora, mais duas passadas já
            # coladas nela (tiram o que ainda entrava na arma) e ela fica
            # com o resultado
            nova_d = dedos_d.fixar(M, rig, fps_['Right'], q.get('forma_d'))
            nova_e = dedos_e.fixar(M, rig, fps_['Left'], q.get('forma_e'))
            for _ in range(2 if (nova_d or nova_e) else 0):
                dd = dedos_d.resolver(M, locais, rig, fps_['Right'], forma=q.get('forma_d'))
                de = dedos_e.resolver(M, locais, rig, fps_['Left'], forma=q.get('forma_e'), outra=dedos_d.feitos,
                                      fp_outra=fps_['Right'] if q.get('forma_e') is None else None)
                if nova_d:
                    dedos_d.fixar(M, rig, fps_['Right'], q.get('forma_d'), atualizar=True)
                if nova_e:
                    dedos_e.fixar(M, rig, fps_['Left'], q.get('forma_e'), atualizar=True)
        for lado in ('Right', 'Left'):
            medidas[lado]['palma'] = folga_palma(corpo, rig, lado, M)
        jogo, mundo = corpo.no_jogo(M)
        out.ossos.append({nome: jogo[corpo.n(nome)] for nome in OSSOS if corpo.n(nome) in jogo})
        # o conjunto no espaço do Spine2 (jogo): o olho da primeira pessoa
        # (a origem da cena do .glb) posto no corpo
        conj = EIXOS @ col @ EIXOS.inverted()
        peito = mundo[corpo.n('Spine2')]
        out.conjunto.append(peito.inverted() @ conj)
        out.locais.append(dict(locais))
        out.colocacoes.append(col.copy())
        out.medidas.append(dict(direita=medidas['Right'], esquerda=medidas['Left'], dedos_d=dd, dedos_e=de,
                                fp_d=(fps_['Right']['dobra'], fps_['Right']['lado']),
                                fp_e=(fps_['Left']['dobra'], fps_['Left']['lado'])))
    suavizar_dedos(corpo, out, clipe in LACOS, rig, clipe, (dedos_d, dedos_e))
    return out


def suavizar_dedos(corpo, out, laco, rig=None, clipe=None, dedos=()):
    """Os dedos sem tremer: cada quadro decide onde o dedo encosta, e de um
    quadro para o outro a decisão pode pular um pouco. Uma média curta no
    tempo (1-2-1) nas juntas dos dedos; em laço, o fim emenda no começo (o
    último quadro repete o primeiro). Refaz as rotações do jogo."""
    n = len(out.locais)
    if n < 4:
        return
    nomes = [corpo.n(x) for x in pa.FALANGES if corpo.n(x) in out.locais[0]]

    def antes(k):
        if k > 0:
            return k - 1
        return n - 2 if laco else 0

    def depois(k):
        if k < n - 1:
            return k + 1
        return 1 if laco else n - 1

    novo = []
    for k in range(n):
        a, b, c = out.locais[antes(k)], out.locais[k], out.locais[depois(k)]
        d = {}
        for nome in nomes:
            if nome not in a or nome not in b or nome not in c:
                continue
            qb = b[nome]
            qa = a[nome] if a[nome].dot(qb) >= 0 else -a[nome]
            qc = c[nome] if c[nome].dot(qb) >= 0 else -c[nome]
            q = Quaternion((qa.w * 0.25 + qb.w * 0.5 + qc.w * 0.25, qa.x * 0.25 + qb.x * 0.5 + qc.x * 0.25,
                            qa.y * 0.25 + qb.y * 0.5 + qc.y * 0.25, qa.z * 0.25 + qb.z * 0.5 + qc.z * 0.25))
            q.normalize()
            d[nome] = q
        novo.append(d)
    if rig is not None:
        # A média não pode pôr o dedo dentro da arma: quando a mão chega nela
        # depressa (a recarga, o mirar), o quadro de antes — o dedo ainda
        # aberto, ou fechado em outro lugar — puxava a ponta até 1,5 cm para
        # dentro. Onde a média entra mais que o quadro como foi resolvido,
        # a mão fica como foi resolvida.
        for k in range(n):
            if not novo[k]:
                continue
            rig.tocar(clipe, k)
            rig.colocar(out.colocacoes[k])
            cru = corpo.esq.fk(out.locais[k])
            liso = corpo.esq.fk(dict(out.locais[k], **novo[k]))
            for dd in dedos:
                d_liso = dd.dentro(liso, rig)
                if d_liso < -0.0008 and d_liso < dd.dentro(cru, rig) - 0.0005:
                    for d_ in DEDOS:
                        for osso in dd.ossos[d_][:3]:
                            novo[k].pop(osso, None)
    for k in range(n):
        out.locais[k].update(novo[k])
        jogo, _ = corpo.no_jogo(corpo.esq.fk(out.locais[k]))
        for nome in OSSOS:
            if corpo.n(nome) in jogo:
                out.ossos[k][nome] = jogo[corpo.n(nome)]


# ------------------------------------------------------------------ medidas

def resumo(clipe):
    """O pior de cada medida no clipe."""
    pior = dict(mao=0.0, punho=0.0, punho_lado=0.0, giro=0.0, dedo_dentro=0.0, junta_fora=0.0, palma=0.0, palma_dentro=0.0, dedos_juntos=0.0)
    onde = {}
    for k, m in enumerate(clipe.medidas):
        for lado in ('direita', 'esquerda'):
            b = m[lado]
            for chave, valor in (('mao', b['falta']), ('punho', abs(b['dobra'])), ('punho_lado', abs(b['lado'])), ('giro', abs(b['giro'])),
                                 ('palma', b.get('palma', 0.0) if 0.0 < b.get('palma', 1.0) < 0.05 else 0.0),
                                 ('palma_dentro', max(0.0, -b.get('palma', 0.0)))):
                if valor > pior[chave]:
                    pior[chave] = valor
                    onde[chave] = (k, lado)
        for lado in ('dedos_d', 'dedos_e'):
            if m[lado].get('_entre', 0.0) < -pior['dedos_juntos']:
                pior['dedos_juntos'] = -m[lado]['_entre']
                onde['dedos_juntos'] = (k, lado)
            for d, md in m[lado].items():
                if d == '_entre':
                    continue
                if md['folga'] is not None and -md['folga'] > pior['dedo_dentro']:
                    pior['dedo_dentro'] = -md['folga']
                    onde['dedo_dentro'] = (k, lado, d)
                lim = LIMITE_POLEGAR if d == 'Thumb' else LIMITE_DEDO
                for j, nome in enumerate(('mcp', 'pip', 'dip')):
                    a = md['juntas'][j]
                    lo, hi = lim[nome]
                    fora = max(lo - a, a - hi, 0.0)
                    if fora > pior['junta_fora']:
                        pior['junta_fora'] = fora
                        onde['junta_fora'] = (k, lado, d, nome, round(a, 1))
    return pior, onde


# ------------------------------------------------------------------ gravar

def q16(x):
    return max(-32767, min(32767, int(round(x * 32767))))


def p16(x):
    return max(-32767, min(32767, int(round(x * 10000))))


def gravar(personagem, arma, clipes, quadros_por_segundo=FPS, escala=1.0):
    pasta = os.path.join(RAIZ, 'assets/personagens', personagem, 'animacoes')
    os.makedirs(pasta, exist_ok=True)
    nomes = [n for n in OSSOS]
    dados = {'_': 'Feito por tools/personagens/blender/animacoes_armas.py. Por clipe e por quadro: a rotação '
                  'local de cada osso de "ossos" (x, y, z, w; inteiros de 16 bits ÷ 32767) e o conjunto da '
                  'primeira pessoa no espaço do Spine2 (posição ÷ 10000 m, rotação x, y, z, w ÷ 32767), em base64. '
                  '"escala": quantas vezes o conjunto cresce (em volta da origem dele) neste personagem.',
             'fps': quadros_por_segundo, 'escala': escala, 'ossos': nomes, 'clipes': {}}
    for c in clipes:
        valores = []
        for k in range(c.quadros):
            ossos = c.ossos[k]
            for nome in nomes:
                q = ossos.get(nome, Quaternion())
                if q.w < 0:
                    q = -q
                valores += [q16(q.x), q16(q.y), q16(q.z), q16(q.w)]
            m = c.conjunto[k]
            t = m.translation
            q = m.to_quaternion().normalized()
            if q.w < 0:
                q = -q
            valores += [p16(t.x), p16(t.y), p16(t.z), q16(q.x), q16(q.y), q16(q.z), q16(q.w)]
        bruto = struct.pack('<%dh' % len(valores), *valores)
        dados['clipes'][c.nome] = {'quadros': c.quadros, 'laco': c.nome in LACOS,
                                   'dados': base64.b64encode(bruto).decode('ascii')}
    caminho = os.path.join(pasta, arma + '.json')
    with open(caminho, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(dados, f, ensure_ascii=False, separators=(',', ':'))
        f.write('\n')
    return caminho


# ------------------------------------------------------------------ fotos

def preparar_fotos():
    pa.preparar_fotos()
    cena = bpy.context.scene
    cena.cycles.samples = 12
    cena.render.resolution_x = 520
    cena.render.resolution_y = 520


def foto_quadro(corpo, rig, caminho, vista):
    M = corpo.esq.fk({})
    d, e = M[corpo.n('RightArm')].translation, M[corpo.n('LeftArm')].translation
    centro = (d + e) * 0.5
    de, para, lente = VISTAS[vista]
    pa.foto(caminho, centro + de, centro + para, lente)


VISTAS = {
    'frente': (Vector((0.75, -2.0, 0.25)), Vector((0, -0.25, -0.15)), 45),
    'lado': (Vector((-2.2, -0.75, 0.15)), Vector((0, -0.25, -0.12)), 45),
    'perto': (Vector((-0.75, -1.05, 0.22)), Vector((-0.05, -0.32, -0.10)), 55),
    'mao': (Vector((-0.55, -0.9, 0.05)), Vector((-0.02, -0.38, -0.10)), 70),
    'cima': (Vector((-0.3, -1.0, 1.6)), Vector((0.0, -0.25, -0.1)), 40),
    'palma': (Vector((0.75, -0.55, -0.55)), Vector((0.12, -0.38, -0.14)), 70),
    'tras': (Vector((-0.9, 1.9, 0.5)), Vector((0.0, -0.1, -0.1)), 45),
}


def escala_arma(corpo, tipo='pistola'):
    """Quantas vezes a arma cresce neste personagem.

    O que se segura com uma mão (pistola, faca, granada) cresce com a mão: a
    arma da primeira pessoa é do tamanho da mão de lá (MAO_FP), e na mão do
    Barut (1,30× maior) o cabo sumia. O fuzil, que se segura com as duas,
    cresce com o que vier primeiro, a mão ou o braço (BRACO_FP): com a arma
    maior, a mão de apoio não chega mais onde a da primeira pessoa segura (o
    Barut tem o braço 0,91× o de lá; a 1,3× a mão dele ia parar no
    carregador, com os dedos retos para cima no corpo da arma)."""
    forcada = os.environ.get('ESCALA_ARMA')
    if forcada:
        return float(forcada)
    b = corpo.arm.data.bones

    def p(nome):
        return corpo.arm.matrix_world @ b[corpo.n(nome)].head_local

    def soma(cadeia):
        return sum((p(x) - p(y)).length for x, y in zip(cadeia, cadeia[1:]))

    mao = soma(['RightHand', 'RightHandMiddle1', 'RightHandMiddle2', 'RightHandMiddle3', 'RightHandMiddle4']) / MAO_FP
    braco = soma(['RightArm', 'RightForeArm', 'RightHand']) / BRACO_FP
    razao = min(mao, braco) if tipo == 'fuzil' else mao
    return round(max(ESCALA_MIN, min(ESCALA_MAX, razao)), 2)


def main():
    args = sys.argv[1:]
    fotos = None
    if '--fotos' in args:
        fotos = args[args.index('--fotos') + 1]
        args = [a for a in args if a not in ('--fotos', fotos)]
    so_clipes = None
    if '--clipes' in args:
        so_clipes = args[args.index('--clipes') + 1].split(',')
        args = [a for a in args if a not in ('--clipes', ','.join(so_clipes))]
    quadros_foto = None
    if '--quadros' in args:
        quadros_foto = [float(x) for x in args[args.index('--quadros') + 1].split(',')]
        args = [a for a in args if a not in ('--quadros', args[args.index('--quadros') + 1])]
    vistas = ['perto']
    if '--vistas' in args:
        vistas = args[args.index('--vistas') + 1].split(',')
        args = [a for a in args if a not in ('--vistas', ','.join(vistas))]
    sem_gravar = '--sem-gravar' in args
    armas = [a for a in args if a in ARMAS] or list(ARMAS)
    quem = [a for a in args if a in pa.PERSONAGENS] or list(pa.PERSONAGENS)

    for personagem in quem:
        glb = os.path.join(RAIZ, 'assets/personagens', personagem, 'humano.glb')
        for arma in armas:
            inicio = time.time()
            bpy.ops.wm.read_factory_settings(use_empty=True)
            bpy.context.scene.render.fps = FPS
            if fotos:
                preparar_fotos()
            corpo = Corpo(glb)
            rig = Conjunto(arma, escala_arma(corpo, ARMAS[arma]))
            print('ESCALA', personagem, arma, round(rig.escala, 3))
            plano = Plano(corpo, rig, arma)
            print('PLANO', personagem, arma, json.dumps(plano.relato, ensure_ascii=False))
            clipes = []
            # o parado e a mira primeiro: é neles que a pegada de base dos
            # dedos é fixada (Dedos.fixar), e os outros clipes partem dela
            for clipe in sorted(CLIPES[plano.tipo], key=lambda x: (x != 'parado', x != 'mira')):
                if clipe not in rig.clipes():
                    continue
                if so_clipes and clipe not in so_clipes:
                    continue
                c = resolver_clipe(corpo, rig, plano, clipe)
                clipes.append(c)
                pior, onde = resumo(c)
                print(f'CLIPE {personagem} {arma} {clipe} ({c.quadros} q): mão {pior["mao"] * 100:.1f} cm, '
                      f'punho {pior["punho"]:.0f}°/{pior["punho_lado"]:.0f}° lado/{pior["giro"]:.0f}° giro, '
                      f'dedo dentro {pior["dedo_dentro"] * 1000:.1f} mm, dedo no vizinho {pior["dedos_juntos"] * 1000:.1f} mm, junta fora {pior["junta_fora"]:.1f}°, '
                      f'palma longe {pior["palma"] * 1000:.1f} mm, palma dentro {pior["palma_dentro"] * 1000:.1f} mm {onde}')
                if os.environ.get('QUADRO_A_QUADRO'):
                    for k, m in enumerate(c.medidas):
                        d_, e_ = m['direita'], m['esquerda']
                        print(f'  q{k:02d} t={k / FPS:.3f} D falta {d_["falta"] * 100:4.1f} dobra {d_["dobra"]:6.1f} '
                              f'lado {d_["lado"]:6.1f} giro {d_["giro"]:6.1f} | E falta {e_["falta"] * 100:4.1f} '
                              f'dobra {e_["dobra"]:6.1f} lado {e_["lado"]:6.1f} giro {e_["giro"]:6.1f}')
                if fotos:
                    os.makedirs(fotos, exist_ok=True)
                    marcas = quadros_foto or [0.0, 0.5]
                    for f in marcas:
                        k = min(c.quadros - 1, int(round(f * (c.quadros - 1))))
                        rig.tocar(clipe, k)
                        corpo.aplicar(c.locais[k])
                        rig.colocar(c.colocacoes[k])
                        for vista in vistas:
                            foto_quadro(corpo, rig, os.path.join(fotos, f'{personagem}_{arma}_{clipe}_{k:03d}_{vista}.png'), vista)
            if not sem_gravar and not so_clipes:
                caminho = gravar(personagem, arma, clipes, escala=rig.escala)
                print('GRAVADO', caminho, f'{time.time() - inicio:.0f} s')
            else:
                print('PRONTO', personagem, arma, f'{time.time() - inicio:.0f} s')


if __name__ == '__main__':
    main()
