#!/usr/bin/env python3
"""
As poses de terceira pessoa com arma, feitas no Blender (Jhon, 22/11:
"use o blender para isso").

    python3 tools/personagens/blender/poses_armas.py                  # todos (eve, barut…)
    python3 tools/personagens/blender/poses_armas.py barut            # um só
    python3 tools/personagens/blender/poses_armas.py barut --fotos /tmp/poses

Para cada personagem e cada tipo de arma (fuzil, pistola, faca, granada)
monta a pose do corpo da cintura para cima: o tronco, os ombros, a cabeça,
os braços e cada dedo. A arma é a mesma da primeira pessoa
(`assets/armas/fp/`, a ficha `bracos.json`), no tamanho de verdade, e a
mão vai nela como a mão da primeira pessoa vai: o mesmo jeito de pegar,
os dedos fechando o mesmo tanto (Jhon, 21/11: "use o posicionamento das
armas, tamanho, posição dos dedos, punho, ombro das animações primeira
pessoa como referência").

O que não vem da primeira pessoa é decidido aqui, em DESENHO: onde a
arma fica no corpo (a coronha no ombro, a pistola na frente do peito),
como o tronco e a cabeça ficam, para onde o cotovelo aponta. O cotovelo é
escolhido para o punho ficar reto (Jhon: "mas não ficar torto"): o
script procura, em volta do braço, o giro do cotovelo em que o antebraço
fica na linha da mão.

Grava, por personagem:
- `assets/personagens/<p>/poses_armas.json`: a rotação de cada osso (no
  espaço do osso do jogo, que é o do glTF), em três chaves (respira) por
  tipo, e a rotação do peito no corpo (o jogo endireita o tronco para a
  mira a partir dela);
- com `--blend`, `tools/personagens/blender/poses/<p>.blend`: a cena com
  uma ação por tipo e a arma de cada tipo presa na mão (coleção
  `arma_<tipo>`), para abrir e ajustar à mão. Depois de mexer nas ações,
  `--do-blend` regrava o .json a partir do .blend sem refazer as poses.
  (O .blend tem ~22 MB com as texturas: fica fora do git; a pasta tem
  `.gdignore` para o Godot não tentar importá-lo.)

Precisa do módulo `bpy` (`pip install bpy`).
"""
import json
import math
import os
import struct
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(AQUI)))
FICHA = json.load(open(os.path.join(RAIZ, 'assets/armas/fp/bracos.json'), encoding='utf-8'))

# Todo personagem com humano.glb em assets/personagens/ (o glTF guarda os
# eixos de cada osso como o jogo os usa).
PERSONAGENS = tuple(sorted(d for d in os.listdir(os.path.join(RAIZ, 'assets/personagens'))
                           if os.path.isfile(os.path.join(RAIZ, 'assets/personagens', d, 'humano.glb'))))
TIPOS = ('fuzil', 'pistola', 'faca', 'granada')
PREFIXO = 'mixamorig:'

# Do Blender (Z para cima, o personagem olhando para −Y) para o glTF e o
# jogo (Y para cima, o personagem olhando para +Z).
EIXOS = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))

FRENTE = Vector((0, -1, 0))   # para onde o personagem olha, no Blender
DIREITA = Vector((-1, 0, 0))  # a mão direita dele
CIMA = Vector((0, 0, 1))

DEDOS = ('Thumb', 'Index', 'Middle', 'Ring', 'Pinky')
DEDOS_FP = ('thumb', 'index', 'middle', 'ring', 'pinky')

# Os ossos que a pose leva (da cintura para cima).
TRONCO = ('Spine', 'Spine1', 'Spine2', 'Neck', 'Head')
BRACOS = tuple(f'{lado}{osso}' for lado in ('Left', 'Right') for osso in ('Shoulder', 'Arm', 'ForeArm', 'Hand'))
FALANGES = tuple(f'{lado}Hand{d}{n}' for lado in ('Left', 'Right') for d in DEDOS for n in (1, 2, 3))
OSSOS = TRONCO + BRACOS + FALANGES

# ------------------------------------------------------------------ o desenho de cada tipo
#
# Ângulos em graus. tronco: giro (+ vira para a esquerda dele), inclinação
# (+ para a frente) e caimento (+ para a direita), repartidos pelas três
# vértebras. cabeca: inclinação e caimento da cabeça (ela olha para a
# frente, para a mira). ombro_d / ombro_e: a clavícula sobe (+) e vai para
# a frente (+), em graus.
#
# arma: onde a arma fica, em metros, no espaço do peito do personagem em
# pé (x à direita dele, y para a frente, z para cima), e para onde o cano
# aponta (inclinação para baixo, em graus). "apoio" diz que ponto da arma
# vai ali: a coronha (fuzil), o cabo (pistola, faca) ou o meio (granada).
#
# cotovelo_d / cotovelo_e: para onde o cotovelo prefere apontar (fora,
# baixo, trás), antes de o script girá-lo para o punho ficar reto.
#
# esquerda: "arma" (a mão esquerda segura a arma como na primeira pessoa),
# "guarda" (punho fechado na frente do peito) ou "solta" (ao lado do
# corpo).
DESENHO = {
    'fuzil': dict(
        conjunto='ar15',
        tronco=(-25, 6, 0), cabeca=(8, 7), ombro_d=(4, 6), ombro_e=(0, 14),
        apoio='coronha', arma=(0.12, 0.12, 0.0), mira=5,
        cotovelo_d=(0.6, -0.7, 0.2), cotovelo_e=(-0.15, -1.0, 0.0),
        esquerda='arma', apoio_fecha=0.7,
    ),
    'pistola': dict(
        conjunto='deagle',
        tronco=(0, 8, 0), cabeca=(4, 0), ombro_d=(2, 18), ombro_e=(2, 18),
        apoio='cabo', arma=(0.02, 0.62, 0.10), mira=2,
        cotovelo_d=(0.7, -0.7, 0.0), cotovelo_e=(-0.7, -0.7, 0.0),
        esquerda='arma', polegar_frente=0.6,
    ),
    'faca': dict(
        conjunto='faca',
        tronco=(14, 12, 0), cabeca=(8, 0), ombro_d=(0, 10), ombro_e=(4, 8),
        apoio='cabo', arma=(0.12, 0.42, -0.18), mira=-20,
        cotovelo_d=(0.6, -0.8, 0.2), cotovelo_e=(0.5, -0.9, 0.0),
        esquerda='guarda', guarda=(-0.20, 0.24, -0.04),
    ),
    'granada': dict(
        conjunto='granada',
        tronco=(-8, 4, 0), cabeca=(4, 0), ombro_d=(0, 6), ombro_e=(-2, 0),
        apoio='meio', arma=(0.10, 0.38, -0.08), mira=None,
        cotovelo_d=(0.6, -0.8, 0.0), cotovelo_e=(-0.4, -1.0, 0.2),
        esquerda='solta', cresce=1.15,
        # a mão direita desenhada (no peito): o pulso, para onde vão os dedos
        # e para onde a palma olha — a granada na palma, para cima, à mostra
        mao_d=dict(pulso=(0.16, 0.30, -0.14), dedos=(-0.3, 0.92, 0.25), palma=(-0.25, 0.2, 0.95)),
    ),
}

# A respiração: na chave do meio o peito sobe um pouco (graus para trás).
RESPIRA = 1.2
CHAVES = (0.0, 1.6, 3.2)


def upd():
    bpy.context.view_layer.update()


def orient(x, y):
    """A base com x na direção dada e y o mais perto possível de y."""
    X = x.normalized()
    Z = X.cross(y).normalized()
    Y = Z.cross(X)
    return Matrix((X, Y, Z)).transposed()


def girar(pb, q):
    """Gira o osso (e o que vem depois dele) em torno da cabeça dele, no mundo."""
    m = pb.matrix.copy()
    pb.matrix = Matrix.Translation(m.translation) @ (q.to_matrix() @ m.to_3x3()).to_4x4()
    upd()


def por_rotacao(pb, rot):
    m = pb.matrix.copy()
    pb.matrix = Matrix.Translation(m.translation) @ rot.to_4x4()
    upd()


def apontar(pb, direcao):
    """O eixo do osso (Y no Blender) para a direção, com o menor giro."""
    y = (pb.matrix.to_3x3() @ Vector((0, 1, 0))).normalized()
    girar(pb, y.rotation_difference(direcao.normalized()))


def assentar(pb, y_mundo, h_mundo, h_local):
    """O osso ao longo de y_mundo, com o eixo de dobra dele (h_local) em h_mundo."""
    yl = Vector((0, 1, 0))
    hl = (h_local - yl * h_local.dot(yl)).normalized()
    Y = y_mundo.normalized()
    H = (h_mundo - Y * h_mundo.dot(Y)).normalized()
    ml = Matrix((yl, hl, yl.cross(hl))).transposed()
    mw = Matrix((Y, H, Y.cross(H))).transposed()
    por_rotacao(pb, mw @ ml.inverted())


def dobra(direcao, ao_longo, eixo):
    """O quanto uma direção dobra a partir de ao_longo, em torno do eixo (rad)."""
    plano = direcao - eixo * direcao.dot(eixo)
    return math.atan2(eixo.dot(ao_longo.cross(plano)), ao_longo.dot(plano))


def girado(v, eixo, angulo):
    return (Quaternion(eixo, angulo) @ v) if abs(angulo) > 1e-6 else v.copy()


# ------------------------------------------------------------------ o personagem

def gltf_repouso(caminho):
    """O repouso de cada osso no glTF original: no mundo, e quem é o pai."""
    dados = open(caminho, 'rb').read()
    n = struct.unpack('<I', dados[12:16])[0]
    j = json.loads(dados[20:20 + n])
    nos = j['nodes']
    pai = {}
    for i, no in enumerate(nos):
        for c in no.get('children', []):
            pai[c] = i

    def local(i):
        no = nos[i]
        t = Vector(no.get('translation', [0, 0, 0]))
        r = no.get('rotation', [0, 0, 0, 1])
        s = no.get('scale', [1, 1, 1])
        return Matrix.Translation(t) @ Quaternion((r[3], r[0], r[1], r[2])).to_matrix().to_4x4() @ Matrix.Diagonal((s[0], s[1], s[2], 1))

    mundo = {}

    def no_mundo(i):
        if i not in mundo:
            mundo[i] = no_mundo(pai[i]) @ local(i) if i in pai else local(i)
        return mundo[i]

    juntas = {nos[i]['name']: i for i in j['skins'][0]['joints']}
    nomes = {i: nome for nome, i in juntas.items()}
    return {nome: (no_mundo(i), nomes.get(pai.get(i))) for nome, i in juntas.items()}


class Corpo:
    def __init__(self, caminho, arm=None):
        if arm is None:
            antes = set(bpy.data.objects)
            bpy.ops.import_scene.gltf(filepath=caminho)
            novos = [o for o in bpy.data.objects if o not in antes]
            arm = next(o for o in novos if o.type == 'ARMATURE')
            for o in novos:
                # O importador desenha os ossos com uma "Icosphere" solta.
                if o.type == 'MESH' and o.parent is None:
                    bpy.data.objects.remove(o)
        self.arm = arm
        self.malhas = [o for o in bpy.data.objects if o.parent == self.arm and o.type == 'MESH']
        assert self.arm.matrix_world == Matrix.Identity(4)
        self.repouso = gltf_repouso(caminho)
        self.pb = self.arm.pose.bones

        # O que leva o osso do Blender ao osso do glTF (o importador vira os
        # ossos para desenhar bonito; o jogo usa os do arquivo).
        self.acerto = {}
        for nome, (mundo, _) in self.repouso.items():
            osso = self.arm.data.bones.get(nome)
            if osso is not None:
                self.acerto[nome] = osso.matrix_local.inverted() @ EIXOS.inverted() @ mundo

        # A mão de cada lado, no espaço do osso da mão: para onde vão os
        # dedos, para onde a palma olha, e o começo do dedo médio.
        self.mao = {}
        for lado in ('Left', 'Right'):
            r = self.arm.data.bones[PREFIXO + lado + 'Hand'].matrix_local
            volta = r.to_3x3().inverted()
            pulso = r.translation
            meio = self.arm.data.bones[PREFIXO + lado + 'HandMiddle1'].head_local
            indic = self.arm.data.bones[PREFIXO + lado + 'HandIndex1'].head_local
            minimo = self.arm.data.bones[PREFIXO + lado + 'HandPinky1'].head_local
            ao_longo = meio - pulso
            travessa = indic - minimo
            dentro = travessa.cross(ao_longo) if lado == 'Right' else ao_longo.cross(travessa)
            self.mao[lado] = dict(dedos=(volta @ ao_longo).normalized(), palma=(volta @ dentro).normalized(),
                                  nos=volta @ ao_longo, repouso=r.to_3x3())

        # O eixo em que o cotovelo dobra, no espaço de cada osso do braço:
        # no T, o braço dobra para a frente, em torno do vertical.
        self.dobra_local = {}
        for lado, sinal in (('Right', 1), ('Left', -1)):
            for osso in ('Arm', 'ForeArm'):
                m = self.arm.data.bones[PREFIXO + lado + osso].matrix_local.to_3x3()
                self.dobra_local[lado + osso] = (m.inverted() @ Vector((0, 0, sinal))).normalized()

        # A falange de cada dedo no repouso (reto), no espaço da mão.
        self.falange_repouso = {}
        for lado in ('Left', 'Right'):
            volta = self.mao[lado]['repouso'].inverted()
            for d in DEDOS:
                pontos = [self.arm.data.bones[f'{PREFIXO}{lado}Hand{d}{n}'].head_local for n in (1, 2, 3, 4)]
                self.falange_repouso[lado + d] = [volta @ (pontos[k + 1] - pontos[k]) for k in range(3)]

    def b(self, nome):
        return self.pb[PREFIXO + nome]

    def repousar(self):
        for pb in self.pb:
            pb.rotation_mode = 'QUATERNION'
            pb.rotation_quaternion = Quaternion()
            pb.location = Vector()
            pb.scale = Vector((1, 1, 1))
        upd()

    def peito(self):
        """O meio dos ombros, e a base do peito (x direita, y frente, z cima)."""
        d, e = self.b('RightArm').head, self.b('LeftArm').head
        m = self.b('Spine2').matrix.to_3x3()
        r = self.arm.data.bones[PREFIXO + 'Spine2'].matrix_local.to_3x3()
        giro = m @ r.inverted()
        base = Matrix((giro @ DIREITA, giro @ FRENTE, giro @ CIMA)).transposed()
        return (d + e) * 0.5, base

    # -------------------------------------------------------- pose

    def postura(self, giro, inclina, cai):
        """O tronco: giro, inclinação e caimento repartidos pelas vértebras."""
        q = (Quaternion(CIMA, math.radians(giro / 3)) @ Quaternion(Vector((1, 0, 0)), math.radians(inclina / 3))
             @ Quaternion(FRENTE, math.radians(cai / 3)))
        for nome in ('Spine', 'Spine1', 'Spine2'):
            girar(self.b(nome), q)

    def olhar(self, inclina, cai):
        """A cabeça para a frente (a mira), inclinada e caída um pouco."""
        cabeca = self.b('Head')
        r = self.arm.data.bones[PREFIXO + 'Head'].matrix_local.to_3x3()
        quer = (Quaternion(Vector((1, 0, 0)), math.radians(inclina)) @ Quaternion(FRENTE, math.radians(cai))).to_matrix() @ r
        agora = cabeca.matrix.to_3x3()
        delta = (quer @ agora.inverted()).to_quaternion()
        metade = Quaternion().slerp(delta, 0.5)
        girar(self.b('Neck'), metade)
        agora = cabeca.matrix.to_3x3()
        girar(cabeca, (quer @ agora.inverted()).to_quaternion())

    def clavicula(self, lado, sobe, frente):
        pb = self.b(lado + 'Shoulder')
        # Subir: gira em torno da frente; para a frente: em torno do vertical.
        q = Quaternion(FRENTE, math.radians(sobe) * (-1 if lado == 'Right' else 1))
        q = Quaternion(CIMA, math.radians(frente) * (1 if lado == 'Right' else -1)) @ q
        girar(pb, q)

    def braco(self, lado, pulso, rot_mao, cotovelo, limite=None, torcao=0.5):
        """
        O braço até o pulso, a mão na rotação pedida. O cotovelo gira em
        volta da linha ombro–pulso até o antebraço ficar o mais na linha da
        mão (sem subir acima do ombro nem entrar no peito). Se ainda sobrar
        dobra no punho além de `limite` graus, a mão volta até lá.
        Devolve a rotação final da mão e a dobra que ficou.
        """
        up, lo, ha = self.b(lado + 'Arm'), self.b(lado + 'ForeArm'), self.b(lado + 'Hand')
        S = up.head.copy()
        a = (lo.head - up.head).length
        bb = (ha.head - lo.head).length
        d = pulso - S
        dist = min(d.length, (a + bb) * 0.999)
        direc = d.normalized()
        cos_a = (a * a + dist * dist - bb * bb) / (2 * a * dist)
        alfa = math.acos(max(-1.0, min(1.0, cos_a)))
        C = S + direc * math.cos(alfa) * a
        raio = a * math.sin(alfa)
        # (fora, cima, trás): fora é para o lado do braço.
        fora = DIREITA if lado == 'Right' else -DIREITA
        pref = fora * cotovelo[0] + CIMA * cotovelo[1] - FRENTE * cotovelo[2]
        u = pref - direc * pref.dot(direc)
        if u.length < 1e-4:
            u = CIMA.cross(direc)
        u.normalize()
        v = direc.cross(u)
        W = S + direc * dist
        ao_longo = (rot_mao @ self.mao[lado]['dedos']).normalized()
        dentro_x = 1 if lado == 'Right' else -1

        melhor = None
        for i in range(-36, 37):
            th = math.radians(i * 5)
            E = C + (u * math.cos(th) + v * math.sin(th)) * raio
            ante = (W - E).normalized()
            torto = math.degrees(ante.angle(ao_longo))
            custo = torto + abs(i * 5) * 0.12
            if E.z > S.z - 0.03:
                custo += (E.z - S.z + 0.03) * 800
            # para dentro do peito (passando do ombro em direção ao meio)
            dentro = (E.x - S.x) * dentro_x
            if dentro > 0.10:
                custo += (dentro - 0.10) * 600
            # muito para trás das costas
            if E.y > S.y + 0.12:
                custo += (E.y - S.y - 0.12) * 400
            if melhor is None or custo < melhor[0]:
                melhor = (custo, E, torto)
        E = melhor[1]
        Yu = (E - S).normalized()
        Yf = (W - E).normalized()
        H = Yu.cross(Yf)
        if H.length < 1e-4:
            H = Yu.cross(u)
        assentar(up, Yu, H, self.dobra_local[lado + 'Arm'])
        assentar(lo, Yf, H, self.dobra_local[lado + 'ForeArm'])

        torto = math.degrees(Yf.angle(ao_longo))
        if limite is not None and torto > limite:
            q = Quaternion().slerp(ao_longo.rotation_difference(Yf), (torto - limite) / torto)
            rot_mao = q.to_matrix() @ rot_mao
            torto = limite
        por_rotacao(ha, rot_mao)

        # Metade do giro da mão em torno do antebraço vai para o antebraço
        # (o corpo não tem osso de torção: sem isso o pulso torce).
        if torcao > 0:
            rf = lo.matrix.to_3x3().normalized()
            rf0 = self.arm.data.bones[PREFIXO + lado + 'ForeArm'].matrix_local.to_3x3()
            rh0 = self.mao[lado]['repouso']
            rel = rf.inverted() @ rot_mao
            rel0 = rf0.inverted() @ rh0
            delta = (rel @ rel0.inverted()).to_quaternion()
            if delta.w < 0:
                delta.negate()
            ang = 2 * math.atan2(delta.y, delta.w)
            if ang > math.pi:
                ang -= 2 * math.pi
            girar(lo, Quaternion(Yf, ang * torcao))
            por_rotacao(ha, rot_mao)
        return rot_mao, torto

    def dedos(self, lado, rig, extra=0.0, polegar_frente=None, bola=None, solto=None):
        """
        Cada dedo dobra o tanto que o da primeira pessoa dobra (no plano em
        que os dedos do personagem fecham); o polegar copia a direção de
        cada falange de lá. `extra` fecha mais; `bola` (centro, raio) fecha
        cada dedo em volta dela; `solto` (radianos) ignora a primeira pessoa
        e deixa a mão relaxada ou fechada.
        """
        ha = self.b(lado + 'Hand')
        rot = ha.matrix.to_3x3().normalized()
        ao_longo = (rot @ self.mao[lado]['dedos']).normalized()
        dentro = (rot @ self.mao[lado]['palma']).normalized()
        enrola = ao_longo.cross(dentro).normalized()
        if rig is not None:
            fp = rig.mao(lado)
        for i, d in enumerate(DEDOS):
            quer = []
            for k in range(3):
                r = (rot @ self.falange_repouso[lado + d][k]).normalized()
                if solto is not None:
                    if i == 0 and solto >= 0.8:
                        # punho fechado: o polegar deita por cima dos dedos,
                        # atravessando a mão para o lado do mínimo
                        travessa = dentro.cross(ao_longo) if lado == 'Right' else ao_longo.cross(dentro)
                        if k == 0:
                            quer.append(girado(r, ao_longo, 0.0).lerp(dentro, 0.35).normalized())
                        else:
                            quer.append((travessa * 0.85 + ao_longo * 0.35 + dentro * 0.3).normalized())
                        continue
                    # dobra acumulada nas três juntas (base, meio, ponta)
                    junta = (1.45, 1.7, 1.1) if i else (0.2, 0.45, 0.4)
                    alvo = solto * sum(junta[:k + 1])
                    quer.append(girado(r, enrola, alvo - dobra(r, ao_longo, enrola)) if i else girado(r, enrola, alvo))
                    continue
                seg = fp['dedos'][i][k]
                if i == 0:
                    q = seg.copy()
                    if polegar_frente is not None and k > 0:
                        q = q.lerp(polegar_frente, 0.6).normalized()
                    quer.append(q)
                    continue
                alvo = dobra(seg, fp['ao_longo'], fp['enrola']) + extra * (k + 1) / 3
                quer.append(girado(r, enrola, alvo - dobra(r, ao_longo, enrola)))
            if bola is not None:
                eixo = enrola
                if i == 0:
                    proprio = quer[0].cross(quer[1]) + quer[1].cross(quer[2])
                    if proprio.length > 0.1:
                        eixo = proprio.normalized()
                quer = self._envolver(lado, d, quer, eixo, bola, (ao_longo + dentro).normalized() if i == 0 else None)
            for k in range(3):
                apontar(self.b(f'{lado}Hand{d}{k + 1}'), quer[k])

    def _envolver(self, lado, d, quer, eixo, bola, deita):
        centro, raio = bola
        pele = raio + 0.012
        cadeia = [self.b(f'{lado}Hand{d}{n}') for n in (1, 2, 3, 4)]
        k0 = cadeia[0].head.copy()
        comp = [(cadeia[k + 1].head - cadeia[k].head).length for k in range(3)]
        melhor = None
        a = -0.8
        while a <= 1.8:
            p1 = k0 + girado(quer[0], eixo, a / 3) * comp[0]
            p2 = p1 + girado(quer[1], eixo, a * 2 / 3) * comp[1]
            p3 = p2 + girado(quer[2], eixo, a) * comp[2]
            custo = abs((p3 - centro).length - pele)
            for p in (p1, p2, p3):
                custo += max(0.0, pele - (p - centro).length) * 4
            if deita is not None:
                custo += (1 - (p3 - k0).normalized().dot(deita)) * 0.03
            if melhor is None or custo < melhor[0]:
                melhor = (custo, a)
            a += 0.04
        a = melhor[1]
        return [girado(quer[k], eixo, a * (k + 1) / 3) for k in range(3)]

    # -------------------------------------------------------- para o jogo

    def no_glTF(self):
        """A rotação local de cada osso no espaço do glTF (o do jogo)."""
        mundo = {}
        for nome in self.acerto:
            pb = self.pb.get(nome)
            if pb is not None:
                mundo[nome] = EIXOS @ (self.arm.matrix_world @ pb.matrix) @ self.acerto[nome]
        local = {}
        for nome, m in mundo.items():
            pai = self.repouso[nome][1]
            l = mundo[pai].inverted() @ m if pai in mundo else m
            q = l.to_quaternion().normalized()
            local[nome] = q
        return local, mundo


# ------------------------------------------------------------------ o conjunto da primeira pessoa

class Conjunto:
    """A arma e as mãos da primeira pessoa, paradas no começo do "parado"."""

    def __init__(self, chave):
        ficha = FICHA[chave]
        self.chave = chave
        caminho = os.path.join(RAIZ, ficha['arquivo'].replace('res://', ''))
        antes_obj, antes_acao = set(bpy.data.objects), set(bpy.data.actions)
        bpy.ops.import_scene.gltf(filepath=caminho)
        novos = [o for o in bpy.data.objects if o not in antes_obj]
        acoes = {a.name.split('.')[0]: a for a in bpy.data.actions if a not in antes_acao}
        self.objetos = novos
        self.raiz = next(o for o in novos if o.type == 'EMPTY' and o.name.startswith('Conjunto'))
        self.arm = next(o for o in novos if o.type == 'ARMATURE')
        for o in novos:
            if o.type == 'MESH' and (o.parent is None or o.name.lower().startswith('bracos')):
                o.hide_render = True
                o.hide_viewport = True
        self.armas = [o for o in novos if o.type == 'MESH' and not o.hide_render]

        parado = ficha['animacoes']['parado']
        acao = acoes[parado['clipe']]
        ad = self.arm.animation_data or self.arm.animation_data_create()
        for trilha in ad.nla_tracks:
            trilha.mute = True
        ad.action = acao
        if hasattr(ad, 'action_slot') and acao.slots:
            ad.action_slot = acao.slots[0]
        fps = bpy.context.scene.render.fps / bpy.context.scene.render.fps_base
        bpy.context.scene.frame_set(int(round(parado['de'] * fps)))
        # O conjunto vem com o olho na origem, olhando para +Y (bracos.py);
        # colocar() põe isso no corpo.
        self.base_raiz = self.raiz.matrix_world.copy()
        self.colocacao = Matrix.Identity(4)
        upd()

        # A arma no conjunto: o osso dela e os eixos (frente = para onde a
        # câmera olha, +Y; cima = +Z), antes de pôr o conjunto no corpo.
        self.osso_arma = self.arm.pose.bones['ik_hand_gun']
        g = self.m('ik_hand_gun')
        eixos = [(g.to_3x3() @ v).normalized() for v in (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))]
        eixos = eixos + [-e for e in eixos]
        frente = max(eixos, key=lambda e: e.dot(Vector((0, 1, 0))))
        cima = max((e for e in eixos if abs(e.dot(frente)) < 0.5), key=lambda e: e.dot(Vector((0, 0, 1))))
        # A frente de verdade é a do cano: do osso da arma à boca do cano
        # ("cano" da ficha, no espaço do olho do jogo: x, y, z → x, −z, y),
        # como o jogo mede (HeldRig.GunForward). O eixo do osso no Blender
        # aponta para o osso filho, não para o cano.
        if 'cano' in ficha:
            c = ficha['cano']
            boca = Vector((c[0], -c[2], c[1]))
            if (boca - g.translation).length > 0.05:
                frente = (boca - g.translation).normalized()
        cima = (cima - frente * cima.dot(frente)).normalized()
        self.base_arma = Matrix((frente.cross(cima), frente, cima)).transposed()
        self.origem_arma = g.translation.copy()
        self.pontos = self._pontos()

    def m(self, osso):
        return self.arm.matrix_world @ self.arm.pose.bones[osso].matrix

    def base_arma_mundo(self):
        return self.colocacao.to_3x3().normalized() @ self.base_arma

    def _pontos(self):
        """Os vértices da arma (já na pose), no mundo."""
        dg = bpy.context.evaluated_depsgraph_get()
        pontos = []
        for o in self.armas:
            e = o.evaluated_get(dg)
            malha = e.to_mesh()
            pontos += [o.matrix_world @ v.co for v in malha.vertices]
            e.to_mesh_clear()
        return pontos

    def apoio(self, tipo):
        """O ponto da arma que vai no lugar pedido, no conjunto parado."""
        if tipo == 'cabo':
            return self.origem_arma.copy()
        if tipo == 'meio':
            lo = Vector([min(p[i] for p in self.pontos) for i in range(3)])
            hi = Vector([max(p[i] for p in self.pontos) for i in range(3)])
            return (lo + hi) * 0.5
        # a coronha: o fim de trás da arma, no meio da altura dela ali
        frente = self.base_arma.col[1]
        ts = [(p - self.origem_arma).dot(frente) for p in self.pontos]
        fim = min(ts)
        atras = [p for p, t in zip(self.pontos, ts) if t < fim + 0.025]
        return sum(atras, Vector()) / len(atras)

    def colocar(self, mundo):
        """Põe o olho do conjunto (e tudo nele) em `mundo`."""
        self.colocacao = mundo
        self.raiz.matrix_world = mundo @ self.base_raiz
        upd()

    def mao(self, lado):
        """A mão da primeira pessoa no mundo: pulso, dedos, palma e as falanges."""
        s = 'r' if lado == 'Right' else 'l'
        pulso = self.m(f'hand_{s}').translation
        meio = self.m(f'middle_01_{s}').translation
        indic = self.m(f'index_01_{s}').translation
        minimo = self.m(f'pinky_01_{s}').translation
        ao_longo = meio - pulso
        travessa = indic - minimo
        dentro = travessa.cross(ao_longo) if lado == 'Right' else ao_longo.cross(travessa)
        dedos = []
        for d in DEDOS_FP:
            a, b, c = (self.m(f'{d}_0{n}_{s}') for n in (1, 2, 3))
            ultimo = (c.to_3x3() @ Vector((0, 1, 0))).normalized()
            dedos.append([(b.translation - a.translation).normalized(), (c.translation - b.translation).normalized(), ultimo])
        return dict(pulso=pulso, meio=meio, ao_longo=ao_longo.normalized(), dentro=dentro.normalized(),
                    enrola=ao_longo.normalized().cross(dentro.normalized()).normalized(), dedos=dedos,
                    comprimento=ao_longo.length)

    def centro_granada(self):
        """O meio do corpo da granada (só o que vai no osso Bolt: sem a
        alavanca e o pino), como o "centro" da ficha que o jogo usa."""
        dg = bpy.context.evaluated_depsgraph_get()
        pontos = []
        for o in self.armas:
            grupo = o.vertex_groups.get('Bolt')
            e = o.evaluated_get(dg)
            malha = e.to_mesh()
            for v, vo in zip(malha.vertices, o.data.vertices):
                if grupo is not None:
                    pesos = {g.group: g.weight for g in vo.groups}
                    if not pesos or max(pesos, key=pesos.get) != grupo.index:
                        continue
                pontos.append(o.matrix_world @ v.co)
            e.to_mesh_clear()
        lo = Vector([min(p[i] for p in pontos) for i in range(3)])
        hi = Vector([max(p[i] for p in pontos) for i in range(3)])
        return (lo + hi) * 0.5, max(hi - lo) * 0.5

    def remover(self):
        for o in self.objetos:
            if o.name in bpy.data.objects:
                bpy.data.objects.remove(o)


def mao_no_conjunto(corpo, lado, fp, alinha=0.8):
    """Onde o pulso do corpo vai e como a mão gira para ficar como a da
    primeira pessoa: os dedos e a palma para o mesmo lado, o começo dos
    dedos no mesmo ponto (a mão do personagem é maior)."""
    m = corpo.mao[lado]
    rot = orient(fp['ao_longo'], fp['dentro']) @ orient(m['dedos'], m['palma']).inverted()
    ponto = fp['pulso'].lerp(fp['meio'], alinha)
    pulso = ponto - rot @ (m['nos'] * alinha)
    return pulso, rot


def levar_conjunto(corpo, rig, lado, rot_mao, alinha=0.8, cresce=1.0):
    """Põe o conjunto de modo que a mão dele caia na mão do corpo (a arma
    vai junto com a mão)."""
    fp = rig.mao(lado)
    m = corpo.mao[lado]
    ha = corpo.b(lado + 'Hand')
    corpo_base = orient(rot_mao @ m['dedos'], rot_mao @ m['palma'])
    corpo_ponto = ha.head + rot_mao @ (m['nos'] * alinha)
    fp_base = orient(fp['ao_longo'], fp['dentro'])
    fp_ponto = fp['pulso'].lerp(fp['meio'], alinha)
    alvo = Matrix.Translation(corpo_ponto) @ corpo_base.to_4x4() @ Matrix.Scale(cresce, 4)
    agora = Matrix.Translation(fp_ponto) @ fp_base.to_4x4()
    rig.colocar(alvo @ agora.inverted() @ rig.colocacao)


# ------------------------------------------------------------------ montar uma pose

def posar(corpo, tipo):
    des = DESENHO[tipo]
    corpo.repousar()
    corpo.postura(*des['tronco'])
    corpo.clavicula('Right', *des['ombro_d'])
    corpo.clavicula('Left', *des['ombro_e'])
    corpo.olhar(*des['cabeca'])

    rig = Conjunto(des['conjunto'])
    centro, base = corpo.peito()

    # A arma no lugar desenhado, de frente, o cano na mira (um pouco para
    # baixo). Faca e granada: o conjunto vai para a frente do peito como a
    # câmera o vê (o jeito de segurar é o do animador).
    alvo = centro + base @ Vector(des['arma'])
    if des['mira'] is not None:
        # o cano para a frente do corpo (para a mira), não para onde o peito
        # virou
        frente = Quaternion(DIREITA, math.radians(des['mira'])) @ FRENTE
        cima = (CIMA - frente * CIMA.dot(frente)).normalized()
        quer = Matrix((frente.cross(cima), frente, cima)).transposed()
        giro = quer @ rig.base_arma.inverted()
    else:
        # os eixos da câmera da primeira pessoa (x direita, y frente, z cima)
        # nos do peito
        giro = base.copy()
    apoio = rig.apoio(des['apoio'])
    mundo = Matrix.Translation(alvo) @ giro.to_4x4() @ Matrix.Translation(-apoio)
    rig.colocar(mundo)

    # Arma de duas mãos longe demais para os braços: ela vem para trás, na
    # linha do cano, até as duas mãos alcançarem.
    if des['esquerda'] == 'arma':
        frente_arma = rig.base_arma_mundo().col[1]
        for _ in range(60):
            longe = 0.0
            for lado in ('Right', 'Left'):
                pulso_l, _r = mao_no_conjunto(corpo, lado, rig.mao(lado))
                ombro = corpo.b(lado + 'Arm').head
                comp = (corpo.b(lado + 'ForeArm').head - ombro).length + (corpo.b(lado + 'Hand').head - corpo.b(lado + 'ForeArm').head).length
                longe = max(longe, (pulso_l - ombro).length - comp * 0.96)
            if longe <= 0:
                break
            rig.colocar(Matrix.Translation(-frente_arma * min(longe, 0.01) * 1.0) @ rig.colocacao)

    # A direita: onde a mão da primeira pessoa está, com o cotovelo que
    # deixa o punho reto (ou a mão desenhada, e a arma vem com ela).
    fp = rig.mao('Right')
    pulso, rot = mao_no_conjunto(corpo, 'Right', fp)
    if 'mao_d' in des:
        md = des['mao_d']
        pulso = centro + base @ Vector(md['pulso'])
        rot = orient(base @ Vector(md['dedos']), base @ Vector(md['palma'])) @ orient(corpo.mao['Right']['dedos'], corpo.mao['Right']['palma']).inverted()
    um_so = tipo in ('faca', 'granada')
    rot, torto_d = corpo.braco('Right', pulso, rot, des['cotovelo_d'], limite=25 if um_so else None)
    bola = None
    raio = 0.0
    # A arma vai com a mão direita (no jogo também: o conjunto é posto pela
    # mão desta pose). A granada cresce com a mão do personagem (a dele é
    # maior que a do conjunto) e fica na palma.
    cresce = 1.0
    if tipo == 'granada':
        cresce = max(1.0, min(1.8, corpo.mao['Right']['nos'].length / fp['comprimento'])) * des.get('cresce', 1.0)
    levar_conjunto(corpo, rig, 'Right', rot, cresce=cresce)
    if um_so:
        if tipo == 'granada':
            ha = corpo.b('RightHand')
            palma = ha.head.lerp(corpo.b('RightHandMiddle1').head, 0.6)
            dentro = (rot @ corpo.mao['Right']['palma']).normalized()
            raio = 0.036 * cresce
            centro_quer = palma + dentro * raio
            agora, _ = rig.centro_granada()
            rig.colocar(Matrix.Translation(centro_quer - agora) @ rig.colocacao)
            bola = (centro_quer, raio)

    # A esquerda.
    torto_e = None
    recuo = 0.0
    if des['esquerda'] == 'arma':
        fpe = rig.mao('Left')
        pulso_e, rot_e = mao_no_conjunto(corpo, 'Left', fpe)
        # Braço curto para o guarda-mão: a mão desliza nele para trás (quem
        # atira escolhe onde segura).
        ombro = corpo.b('LeftArm').head
        comp = (corpo.b('LeftForeArm').head - ombro).length + (corpo.b('LeftHand').head - corpo.b('LeftForeArm').head).length
        frente_arma = rig.base_arma_mundo().col[1]
        while (pulso_e - frente_arma * recuo - ombro).length > comp * 0.93 and recuo < des.get('desliza', 0.15):
            recuo += 0.005
        pulso_e = pulso_e - frente_arma * recuo
        rot_e, torto_e = corpo.braco('Left', pulso_e, rot_e, des['cotovelo_e'])
        corpo.dedos('Left', rig, extra=des.get('apoio_fecha', 0.0),
                    polegar_frente=(rig.base_arma_mundo().col[1] if des.get('polegar_frente') else None))
    elif des['esquerda'] == 'guarda':
        alvo_e = centro + base @ Vector(des['guarda'])
        # punho fechado, os nós dos dedos para cima e para a frente, a palma
        # para dentro (para a direita dele)
        rot_e = orient(base @ Vector((0.1, 0.35, 0.93)), base @ Vector((0.85, -0.25, -0.1)))
        rot_e = rot_e @ orient(corpo.mao['Left']['dedos'], corpo.mao['Left']['palma']).inverted()
        rot_e, torto_e = corpo.braco('Left', alvo_e, rot_e, des['cotovelo_e'], limite=20)
        corpo.dedos('Left', None, solto=1.0)
    else:
        ombro = corpo.b('LeftArm').head
        comp = (corpo.b('LeftForeArm').head - ombro).length + (corpo.b('LeftHand').head - corpo.b('LeftForeArm').head).length
        alvo_e = ombro + CIMA * -comp * 0.9 + base.col[1] * 0.08 - base.col[0] * 0.07
        rot_e = orient(base @ Vector((0.05, 0.25, -1.0)), base @ Vector((1, 0, 0)))
        rot_e = rot_e @ orient(corpo.mao['Left']['dedos'], corpo.mao['Left']['palma']).inverted()
        rot_e, torto_e = corpo.braco('Left', alvo_e, rot_e, des['cotovelo_e'], limite=15)
        corpo.dedos('Left', None, solto=0.3)

    corpo.dedos('Right', rig, polegar_frente=(rig.base_arma_mundo().col[1] if des.get('polegar_frente') else None), bola=bola)
    conferir = {nome: [round(v, 4) for v in (EIXOS @ corpo.b(nome).head)] for nome in ('RightHand', 'LeftHand', 'Head')}
    conferir['arma'] = [round(v, 4) for v in (EIXOS @ rig.m('ik_hand_gun').translation)]
    return rig, dict(torto_d=torto_d, torto_e=torto_e, cresce=round(cresce, 4), raio=round(raio, 4),
                     recuo=round(recuo, 4), conferir=conferir)


# ------------------------------------------------------------------ fotos de conferência

def preparar_fotos():
    cena = bpy.context.scene
    cena.render.engine = 'CYCLES'
    cena.cycles.device = 'CPU'
    cena.cycles.samples = 24
    cena.cycles.use_denoising = True
    cena.render.resolution_x = 720
    cena.render.resolution_y = 720
    cena.render.film_transparent = False
    mundo = bpy.data.worlds.new('Fundo')
    mundo.use_nodes = True
    fundo = mundo.node_tree.nodes['Background']
    fundo.inputs[0].default_value = (0.55, 0.6, 0.7, 1)
    fundo.inputs[1].default_value = 0.9
    cena.world = mundo
    sol = bpy.data.objects.new('Sol', bpy.data.lights.new('Sol', 'SUN'))
    sol.data.energy = 3.0
    sol.rotation_euler = (math.radians(50), 0, math.radians(-30))
    cena.collection.objects.link(sol)
    bpy.ops.mesh.primitive_plane_add(size=20)
    cam = bpy.data.objects.new('Foto', bpy.data.cameras.new('Foto'))
    cam.data.lens = 50
    cena.collection.objects.link(cam)
    cena.camera = cam


def foto(caminho, de, para, lente=50):
    cam = bpy.context.scene.camera
    cam.data.lens = lente
    cam.location = de
    cam.rotation_euler = (para - de).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.render.filepath = caminho
    bpy.ops.render.render(write_still=True)


VISTAS = {
    # de onde, para onde (relativo ao peito), lente
    'frente': (Vector((0.9, -2.6, 0.15)), Vector((0, -0.2, -0.15)), 50),
    'lado': (Vector((-2.6, -0.7, 0.1)), Vector((0, -0.25, -0.15)), 50),
    'perto': (Vector((-0.9, -1.3, 0.25)), Vector((-0.05, -0.3, -0.12)), 60),
}


# ------------------------------------------------------------------ gravar

def gravar_json(personagem, poses):
    saida = os.path.join(RAIZ, 'assets/personagens', personagem, 'poses_armas.json')
    dados = {'_': 'Feito por tools/personagens/blender/poses_armas.py: a rotação local de cada osso '
                  '(x, y, z, w, no espaço do osso do jogo) em cada chave (segundos), e a do peito no corpo.',
             'chaves': list(CHAVES), 'poses': poses}
    with open(saida, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(dados, f, ensure_ascii=False, indent=1)
    print('POSES', saida)


def chave(corpo):
    local, mundo = corpo.no_glTF()
    ossos = {}
    for nome in OSSOS:
        q = local.get(PREFIXO + nome)
        if q is not None:
            ossos[nome] = [round(q.x, 5), round(q.y, 5), round(q.z, 5), round(q.w, 5)]
    pq = mundo[PREFIXO + 'Spine2'].to_quaternion().normalized()
    return ossos, [round(pq.x, 5), round(pq.y, 5), round(pq.z, 5), round(pq.w, 5)]


def respirar(corpo, quanto):
    """O peito sobe (a cabeça fica onde estava)."""
    cabeca = corpo.b('Head').matrix.to_3x3().copy()
    q = Quaternion(Vector((1, 0, 0)), -math.radians(quanto) / 2)
    girar(corpo.b('Spine1'), q)
    girar(corpo.b('Spine2'), q)
    agora = corpo.b('Head').matrix.to_3x3()
    girar(corpo.b('Head'), (cabeca @ agora.inverted()).to_quaternion())


def guardar_acao(corpo, tipo):
    """A pose atual como ação do .blend (para ajustar à mão)."""
    acao = bpy.data.actions.new(tipo)
    acao.use_fake_user = True
    ad = corpo.arm.animation_data or corpo.arm.animation_data_create()
    ad.action = acao
    for nome in OSSOS:
        pb = corpo.b(nome)
        pb.keyframe_insert('rotation_quaternion', frame=1, group=nome)
    ad.action = None


def prender_na_mao(corpo, rig, tipo):
    """No .blend, a arma de cada tipo fica presa na mão direita (Child Of)
    e numa coleção própria: com a ação do tipo no esqueleto, ela segue a
    mão enquanto se ajusta a pose."""
    col = bpy.data.collections.new('arma_' + tipo)
    bpy.context.scene.collection.children.link(col)
    for o in rig.objetos:
        if o.name not in bpy.data.objects:
            continue
        for c in list(o.users_collection):
            c.objects.unlink(o)
        col.objects.link(o)
    con = rig.raiz.constraints.new('CHILD_OF')
    con.target = corpo.arm
    con.subtarget = PREFIXO + 'RightHand'
    con.inverse_matrix = (corpo.arm.matrix_world @ corpo.b('RightHand').matrix).inverted()
    col.hide_render = tipo != 'fuzil'
    col.hide_viewport = tipo != 'fuzil'


def exportar(corpo, tipo, medida):
    ossos0, peito0 = chave(corpo)
    respirar(corpo, RESPIRA)
    ossos1, peito1 = chave(corpo)
    pose = {'conjunto': DESENHO[tipo]['conjunto'], 'esquerda': DESENHO[tipo]['esquerda'],
            'ossos': {n: [ossos0[n], ossos1[n], ossos0[n]] for n in ossos0},
            'peito': [peito0, peito1, peito0]}
    if medida is not None:
        pose.update({k: medida[k] for k in ('cresce', 'raio', 'recuo', 'conferir')})
    return pose


def caminho_blend(personagem):
    pasta = os.path.join(AQUI, 'poses')
    os.makedirs(pasta, exist_ok=True)
    marca = os.path.join(pasta, '.gdignore')
    if not os.path.exists(marca):
        open(marca, 'w').close()
    return os.path.join(pasta, personagem + '.blend')


def main():
    args = sys.argv[1:]
    fotos = None
    if '--fotos' in args:
        fotos = args[args.index('--fotos') + 1]
        args = [a for a in args if a not in ('--fotos', fotos)]
    tipos = [a for a in args if a in TIPOS] or list(TIPOS)
    quem = [a for a in args if a in PERSONAGENS] or list(PERSONAGENS)

    for personagem in quem:
        pasta = os.path.join(RAIZ, 'assets/personagens', personagem)
        glb = os.path.join(pasta, 'humano.glb')
        if '--do-blend' in args:
            # As ações do .blend (ajustadas à mão) viram o .json de novo.
            bpy.ops.wm.open_mainfile(filepath=caminho_blend(personagem))
            arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE' and o.data.bones.get(PREFIXO + 'Hips'))
            corpo = Corpo(glb, arm)
            antigo = json.load(open(os.path.join(pasta, 'poses_armas.json'), encoding='utf-8'))['poses']
            poses = {}
            for tipo in TIPOS:
                acao = bpy.data.actions.get(tipo)
                if acao is None:
                    continue
                corpo.repousar()
                arm.animation_data.action = acao
                bpy.context.scene.frame_set(1)
                upd()
                # A pose da ação vira a pose dos ossos (para respirar em cima).
                guardada = {pb.name: pb.matrix.copy() for pb in arm.pose.bones}
                arm.animation_data.action = None
                for pb in sorted(arm.pose.bones, key=lambda b: len(b.parent_recursive)):
                    pb.matrix = guardada[pb.name]
                    upd()
                pose = exportar(corpo, tipo, None)
                for k in ('cresce', 'raio', 'recuo', 'conferir'):
                    if k in antigo.get(tipo, {}):
                        pose[k] = antigo[tipo][k]
                poses[tipo] = pose
            gravar_json(personagem, poses)
            continue

        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.context.scene.render.fps = 30
        if fotos:
            preparar_fotos()
        corpo = Corpo(glb)
        poses = {}
        for tipo in tipos:
            rig, medida = posar(corpo, tipo)
            print(f'POSE {personagem} {tipo}: punho direito {medida["torto_d"]:.0f}°'
                  + (f', esquerdo {medida["torto_e"]:.0f}°' if medida['torto_e'] is not None else '')
                  + f', recuo {medida["recuo"]:.3f}, cresce {medida["cresce"]:.2f}')
            if fotos:
                os.makedirs(fotos, exist_ok=True)
                centro, base = corpo.peito()
                for vista, (de, para, lente) in VISTAS.items():
                    foto(os.path.join(fotos, f'{personagem}_{tipo}_{vista}.png'), centro + de, centro + para, lente)
            guardar_acao(corpo, tipo)
            if fotos:
                rig.remover()
            else:
                prender_na_mao(corpo, rig, tipo)
            poses[tipo] = exportar(corpo, tipo, medida)
        if not fotos:
            gravar_json(personagem, poses)
        if not fotos and '--blend' in args:
            corpo.repousar()
            ad = corpo.arm.animation_data or corpo.arm.animation_data_create()
            ad.action = bpy.data.actions.get('fuzil')
            bpy.context.scene.frame_set(1)
            bpy.ops.wm.save_as_mainfile(filepath=caminho_blend(personagem), compress=True)
            print('BLEND', caminho_blend(personagem))


if __name__ == '__main__':
    main()
