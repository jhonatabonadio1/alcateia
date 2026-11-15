#!/usr/bin/env python3
"""
O AR-15, o fuzil padrão, em primeira pessoa: as animações que o Jhon fez
com o pacote da InfimaGames ("FPS Tutorial Blender Starter Files",
CC BY 4.0; o fuzil é de TastyTony), nos braços que as outras armas usam.

    python3 tools/armas/blender/infima.py

O pacote foi feito para a Unreal: as animações (``A_FP_AssaultRifle_*.fbx``,
uma por arquivo) são do esqueleto do manequim da UE5, e o braço dele é
malha da Epic, que não entra no jogo (decisão do Jhon, 15/11). Então as
animações passam para os braços CC BY do pacote da AK
(``fps-ak-74m-animations``, os mesmos braços da SG, da UMP e da pistola),
quadro a quadro:

- **a mão** fica onde a do manequim está, com a mesma orientação, alinhada
  pelos nós dos dedos (a nossa mão é maior: o punho fica mais atrás);
- **o braço** sai do ombro do manequim e chega na mão por IK de dois ossos,
  com o cotovelo para o lado do cotovelo do manequim; o antebraço gira aos
  poucos (três ossos de torção) para acompanhar o giro da mão;
- **cada dedo** aponta para onde o dedo do manequim aponta (metacarpos
  parados na mão, como no nosso rig);
- **o fuzil** vai num osso novo, ``ik_hand_gun``, que segue o
  ``ik_hand_gun`` do manequim (é onde a Unreal prende a arma). O
  esqueleto do fuzil (Grip, Trigger, Bolt, Magazine) entra filho dele, com
  a animação do ferrolho no tiro; o carregador vai pesado no osso dele.

O olho é a câmera do pacote da AK, que fica no lugar da cabeça do
manequim. Grava:

- ``assets/armas/fp/ar15.glb`` e a entrada ``ar15`` de ``bracos.json``
  (primeira pessoa, como os outros conjuntos — tools/armas/blender/bracos.py);
- ``assets/armas/ar15/`` e a entrada ``ar15`` de ``armas.json`` (o modelo
  na mão dos outros e no chão — scripts/visual/WeaponModel.cs).

O pacote da Infima fica em ``assets/pacotes/infima/`` (fora do git), em
qualquer subpasta: o script procura cada arquivo pelo nome. Sem a
textura do fuzil (``T_AssaultRifle_*``), ele sai cinza.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(AQUI)))
PACOTE = os.path.join(RAIZ, "assets", "pacotes", "infima")
SKETCHFAB = os.path.join(RAIZ, "assets", "pacotes", "sketchfab")
DESTINO_FP = os.path.join(RAIZ, "assets", "armas", "fp")
DESTINO_ARMA = os.path.join(RAIZ, "assets", "armas", "ar15")

sys.path.insert(0, AQUI)
sys.path.insert(0, os.path.join(RAIZ, "tools", "mapas", "blender"))
from bracos import BRACO, achar_texturas, vestir  # noqa: E402
from pecas_polyhaven import regravar_imagens  # noqa: E402

NOME = "ar15"
ARMAS = ["ar15"]

# Nome no jogo -> arquivo do pacote.
ANIMACOES = dict(
    parado="A_FP_AssaultRifle_Idle_Loop",
    andar="A_FP_AssaultRifle_Walk_Loop",
    sacar="A_FP_AssaultRifle_Equip",
    guardar="A_FP_AssaultRifle_Holster",
    tiro="A_FP_AssaultRifle_Fire",
)
# A animação do próprio fuzil (ferrolho e gatilho), junto com a dos braços.
ANIMACOES_ARMA = dict(tiro="A_FP_WEP_AssaultRifle_Fire")

FUZIL = "SK_AssaultRifle"
CARREGADOR = "SM_AssaultRifle_Magazine"
TEXTURA = "T_AssaultRifle"

# Os braços: os do pacote da AK (CC BY: braços de DJMaesen/bumstrum).
BRACOS_PACOTE = "fps-ak-74m-animations"
BRACOS_FBX = "FpsAnims.fbx"
# Os ossos que vêm com a AK do pacote e não servem aqui.
OSSOS_DA_AK = ("PBody", "Rif", "Trigger", "Safety", "Pmag", "Pmag2")

OSSO_ARMA = "ik_hand_gun"
OSSO_CARREGADOR = "Magazine"
OSSO_OLHO = "head"

# Os dedos: o do manequim (UE) -> os ossos do nosso (falanges; o metacarpo
# fica parado na mão). O primeiro da lista do nosso é o metacarpo.
DEDOS = {
    "index": ("004", "005", "006", "007"),
    "middle": ("008", "009", "010", "011"),
    "ring": ("012", "013", "014", "015"),
    "pinky": ("016", "017", "018", "019"),
}
POLEGAR = ("020", "021", "022")

# A abertura vertical da câmera para a qual o pacote foi animado: a da
# Unreal, 90° na horizontal em 16:9.
FOV = round(math.degrees(2 * math.atan(math.tan(math.radians(45)) * 9 / 16)), 1)

# "fiel" na ficha: no jogo, só a animação do Jhon, sem balanço, coice
# nem atraso de código por cima (pedido dele, 15/11).


# ===================================================================== arquivos

def achar(nome, ext=".fbx"):
    """O arquivo do pacote com este nome, em qualquer subpasta."""
    alvo = (nome + ext).lower()
    for base, _, arquivos in os.walk(PACOTE):
        for a in arquivos:
            if a.lower() == alvo:
                return os.path.join(base, a)
    return None


def importar(caminho):
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=caminho)
    return [o for o in bpy.data.objects if o not in antes]


def acao_de(objetos, nome):
    """Tira a ação do esqueleto importado, com o nome do jogo, e apaga o resto."""
    esqueleto = next(o for o in objetos if o.type == "ARMATURE")
    acao = esqueleto.animation_data.action if esqueleto.animation_data else None
    for o in objetos:
        bpy.data.objects.remove(o)
    if acao is None:
        raise SystemExit(f"{nome}: o arquivo não tem animação")
    acao.name = nome
    acao.use_fake_user = True
    return acao


def usar_acao(objeto, acao):
    """Liga (ou desliga) a ação ativa do objeto, com a faixa dela."""
    if objeto.animation_data is None:
        objeto.animation_data_create()
    objeto.animation_data.action = acao
    if acao is not None and hasattr(objeto.animation_data, "action_slot") and getattr(acao, "slots", None):
        try:
            objeto.animation_data.action_slot = acao.slots[0]
        except Exception:
            pass


def trilha(objeto, nome, acao):
    usar_acao(objeto, None)
    t = objeto.animation_data.nla_tracks.new()
    t.name = nome
    tira = t.strips.new(nome, int(acao.frame_range[0]), acao)
    if hasattr(tira, "action_slot") and getattr(acao, "slots", None):
        try:
            tira.action_slot = acao.slots[0]
        except Exception:
            pass
    return t


def resolver_imagens():
    """Os .fbx apontam para a pasta de quem exportou: acha cada imagem no pacote."""
    for img in bpy.data.images:
        if img.filepath and os.path.isfile(bpy.path.abspath(img.filepath)):
            continue
        nome = os.path.basename(img.filepath.replace("\\", "/"))
        raiz, ext = os.path.splitext(nome)
        achado = achar(raiz, ext) if raiz else None
        if achado:
            img.filepath = achado
            img.reload()


# ===================================================================== contas

def so_rotacao(m):
    """A matriz sem escala (o esqueleto do manequim vem com escala 0,01)."""
    loc, rot, _ = m.decompose()
    return Matrix.Translation(loc) @ rot.to_matrix().to_4x4()


def girar_para(m, direcao):
    """Gira a matriz de um osso em volta da cabeça dele até o eixo Y (o do
    osso) apontar para ``direcao``, com o menor giro: a torção fica."""
    y = (m.to_3x3() @ Vector((0, 1, 0))).normalized()
    q = y.rotation_difference(direcao.normalized())
    return Matrix.Translation(m.translation) @ q.to_matrix().to_4x4() @ Matrix.Translation(-m.translation) @ m


def girar_no_eixo(m, angulo):
    """Gira a matriz de um osso em volta do próprio eixo Y."""
    return m @ Matrix.Rotation(angulo, 4, "Y")


def angulo_em_volta(a, b, eixo):
    """O ângulo com sinal de ``a`` para ``b`` em volta de ``eixo``, no plano dele."""
    eixo = eixo.normalized()
    pa = (a - eixo * a.dot(eixo)).normalized()
    pb = (b - eixo * b.dot(eixo)).normalized()
    return math.atan2(eixo.dot(pa.cross(pb)), pa.dot(pb))


def moldura(pulso, medio, indicador, minimo, origem):
    """O espaço da mão: Y do pulso para o nó do dedo médio, X para o lado
    do indicador, Z para fora; origem em ``origem``."""
    y = (medio - pulso).normalized()
    lado = indicador - minimo
    x = (lado - y * lado.dot(y)).normalized()
    z = x.cross(y)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = origem
    return m


class Rig:
    """O nosso esqueleto, posado à mão: cada osso é calculado de cima para
    baixo e vira base (matrix_basis) no fim."""

    def __init__(self, obj):
        self.obj = obj
        self.ossos = obj.data.bones
        self.pose = {}

    def rel(self, nome):
        b = self.ossos[nome]
        return b.parent.matrix_local.inverted() @ b.matrix_local if b.parent else b.matrix_local.copy()

    def padrao(self, nome):
        """A matriz do osso com a base vazia, sob o pai já posado."""
        b = self.ossos[nome]
        pai = self.pose.get(b.parent.name, b.parent.matrix_local) if b.parent else Matrix.Identity(4)
        return pai @ self.rel(nome)

    def por(self, nome, m=None):
        self.pose[nome] = self.padrao(nome) if m is None else m
        return self.pose[nome]

    def base(self, nome):
        return self.padrao(nome).inverted() @ self.pose[nome]


# ===================================================================== retarget

def ler_manequim(fonte, s):
    """Os pontos do lado ``s`` ('l' ou 'r') do manequim, no espaço do mundo."""
    pb = fonte.pose.bones
    mw = fonte.matrix_world

    def cabeca(n):
        return mw @ pb[n].head

    pontos = {n: cabeca(n) for n in (f"upperarm_{s}", f"lowerarm_{s}", f"hand_{s}")}
    dedos = {}
    for dedo in list(DEDOS) + ["thumb"]:
        cadeia = [cabeca(f"{dedo}_0{i}_{s}") for i in (1, 2, 3)]
        # A ponta: o último osso não tem filho; segue o eixo dele que, no
        # manequim, aponta ao longo do dedo (achado no começo).
        m3 = mw @ pb[f"{dedo}_03_{s}"].matrix
        ponta = cadeia[2] + (m3.to_3x3() @ EIXO_DEDO[0]).normalized() * (cadeia[2] - cadeia[1]).length
        dedos[dedo] = cadeia + [ponta]
    return pontos, dedos


EIXO_DEDO = [Vector((0, 1, 0))]


def achar_eixo_do_dedo(fonte):
    """No manequim os ossos não apontam pelo Y do Blender: acha o eixo
    local do osso do dedo que aponta para o filho."""
    pb = fonte.pose.bones
    melhor, nota = None, -1.0
    for eixo in (Vector((1, 0, 0)), Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((0, -1, 0)),
                 Vector((0, 0, 1)), Vector((0, 0, -1))):
        soma = 0.0
        for dedo in ("index", "middle", "ring", "pinky"):
            for s in "lr":
                a, b = pb[f"{dedo}_02_{s}"], pb[f"{dedo}_03_{s}"]
                d = (b.head - a.head).normalized()
                soma += (a.matrix.to_3x3() @ eixo).normalized().dot(d)
        if soma > nota:
            melhor, nota = eixo, soma
    EIXO_DEDO[0] = melhor


def posar(rig, fonte, desloca, lado):
    """Um quadro: põe o braço ``lado`` ('L' ou 'R') do nosso rig na pose do
    manequim. ``desloca`` leva o espaço do manequim para o nosso."""
    s = lado.lower()
    pontos, dedos = ler_manequim(fonte, s)
    P = {k: desloca @ v for k, v in pontos.items()}
    D = {k: [desloca @ v for v in vs] for k, vs in dedos.items()}
    X = lado
    ossos = rig.ossos

    def nome_dedo(n):
        return f"Bone_{X}.{n}"

    # --- A mão: a orientação da do manequim, alinhada pelo nó do dedo médio.
    mao_fonte = moldura(P[f"hand_{s}"], D["middle"][0], D["index"][0], D["pinky"][0], D["middle"][0])
    mao_rest = ossos[f"Hand_{X}"].matrix_local
    k_medio = ossos[nome_dedo(DEDOS["middle"][1])].head_local
    mao_nossa = moldura(mao_rest.translation, k_medio, ossos[nome_dedo(DEDOS["index"][1])].head_local,
                        ossos[nome_dedo(DEDOS["pinky"][1])].head_local, k_medio)
    mao = mao_fonte @ mao_nossa.inverted() @ mao_rest
    pulso = mao.translation

    # --- O braço: do ombro do manequim até o pulso, IK de dois ossos.
    l1 = ossos[f"UpArm_{X}"].length
    l2 = (ossos[f"Hand_{X}"].head_local - ossos[f"BoneTwist_03.{X}"].head_local).length
    ombro = P[f"upperarm_{s}"].copy()
    d = pulso - ombro
    alcance = (l1 + l2) * 0.995
    if d.length > alcance:
        ombro = pulso - d.normalized() * alcance
    elif d.length < abs(l1 - l2) * 1.05 + 0.01:
        ombro = pulso - d.normalized() * (abs(l1 - l2) * 1.05 + 0.01)
    d = pulso - ombro
    dist = d.length
    eixo = d.normalized()
    # O cotovelo vai para o lado do cotovelo do manequim.
    cot_fonte = P[f"lowerarm_{s}"]
    linha = P[f"upperarm_{s}"] + (P[f"hand_{s}"] - P[f"upperarm_{s}"]).normalized() * (
        (cot_fonte - P[f"upperarm_{s}"]).dot((P[f"hand_{s}"] - P[f"upperarm_{s}"]).normalized()))
    polo = cot_fonte - linha
    polo = (polo - eixo * polo.dot(eixo))
    if polo.length < 1e-5:
        baixo = Vector((0, 0, -1))
        polo = baixo - eixo * baixo.dot(eixo)
    polo.normalize()
    a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist)
    h = math.sqrt(max(l1 * l1 - a * a, 0.0))
    cotovelo = ombro + eixo * a + polo * h

    # Clavícula: só desloca, para o ombro cair onde o do manequim está.
    clav = f"Arm_{X}"
    rest = rig.padrao(clav)
    rig.por(clav, Matrix.Translation(ombro - ossos[f"UpArm_{X}"].head_local) @ rest)
    braco = rig.por(f"UpArm_{X}", girar_para(rig.padrao(f"UpArm_{X}"), cotovelo - ombro))
    rig.por(f"Forearm_{X}", girar_para(rig.padrao(f"Forearm_{X}"), pulso - cotovelo))
    t3 = rig.por(f"BoneTwist_03.{X}", girar_para(rig.padrao(f"BoneTwist_03.{X}"), pulso - cotovelo))
    rig.por(f"BoneTwist_02.{X}")
    t1 = rig.por(f"BoneTwist_01.{X}")

    # Torção: o antebraço acompanha o giro da mão em três partes.
    antebraco = (pulso - cotovelo).normalized()
    x_rest_t1 = ossos[f"BoneTwist_01.{X}"].matrix_local.to_3x3() @ Vector((1, 0, 0))
    x_rest_mao = mao_rest.to_3x3() @ Vector((1, 0, 0))
    eixo_rest = (ossos[f"Hand_{X}"].head_local - ossos[f"BoneTwist_03.{X}"].head_local).normalized()
    giro_rest = angulo_em_volta(x_rest_t1, x_rest_mao, eixo_rest)
    giro = angulo_em_volta(t1.to_3x3() @ Vector((1, 0, 0)), mao.to_3x3() @ Vector((1, 0, 0)), antebraco)
    torcao = math.atan2(math.sin(giro - giro_rest), math.cos(giro - giro_rest))
    rig.por(f"BoneTwist_03.{X}", girar_no_eixo(t3, torcao / 3))
    rig.por(f"BoneTwist_02.{X}", girar_no_eixo(rig.padrao(f"BoneTwist_02.{X}"), torcao / 3))
    rig.por(f"BoneTwist_01.{X}", girar_no_eixo(rig.padrao(f"BoneTwist_01.{X}"), torcao / 3))

    # A mão é filha do controle de IK: ele vai onde a mão tem de ficar.
    rig.pose[f"IK_Hand_Cntrl_{X}"] = mao @ rig.rel(f"Hand_{X}").inverted()
    rig.por(f"Hand_{X}")

    # --- Os dedos: cada falange aponta para onde aponta a do manequim.
    for dedo, cadeia in list(DEDOS.items()) + [("thumb", (None,) + POLEGAR)]:
        if cadeia[0] is not None:
            rig.por(nome_dedo(cadeia[0]))
        pts = D[dedo]
        for i, osso in enumerate(cadeia[1:]):
            rig.por(nome_dedo(osso), girar_para(rig.padrao(nome_dedo(osso)), pts[i + 1] - pts[i]))
    return braco


def assar(rig_obj, fonte, desloca, acoes, quadros_arma, escala_arma):
    """Passa cada ação do manequim para o nosso rig, quadro a quadro."""
    rig = Rig(rig_obj)
    for pb in rig_obj.pose.bones:
        pb.rotation_mode = "QUATERNION"
    novas = {}
    cena = bpy.context.scene
    mexidos = None
    for jogo, acao in acoes.items():
        usar_acao(fonte, acao)
        nova = bpy.data.actions.new(jogo)
        nova.use_fake_user = True
        usar_acao(rig_obj, nova)
        primeiro, ultimo = int(acao.frame_range[0]), int(acao.frame_range[1])
        arma_quadros = dict(quadros_arma.get(jogo, []))
        for f in range(primeiro, ultimo + 1):
            cena.frame_set(f)
            rig.pose = {}
            for lado in ("L", "R"):
                posar(rig, fonte, desloca, lado)
            # O fuzil: o ik_hand_gun do manequim.
            gun = desloca @ so_rotacao(fonte.matrix_world @ fonte.pose.bones[OSSO_ARMA].matrix)
            rig.pose[OSSO_ARMA] = gun
            mexidos = list(rig.pose)
            for nome in mexidos:
                pb = rig_obj.pose.bones[nome]
                pb.matrix_basis = rig.base(nome)
                pb.keyframe_insert("location", frame=f)
                pb.keyframe_insert("rotation_quaternion", frame=f)
            # O ferrolho e o gatilho, no tiro.
            for nome, (loc, rot, esc) in arma_quadros.get(f, {}).items():
                pb = rig_obj.pose.bones.get(nome)
                if pb is None:
                    continue
                pb.location, pb.rotation_quaternion, pb.scale = loc * escala_arma, rot, esc
                for caminho in ("location", "rotation_quaternion", "scale"):
                    pb.keyframe_insert(caminho, frame=f)
        usar_acao(rig_obj, None)
        novas[jogo] = nova
        print(f"  {jogo}: {ultimo - primeiro + 1} quadros")
    return novas


# ===================================================================== montagem

def main():
    faltando = [n for n in list(ANIMACOES.values()) + list(ANIMACOES_ARMA.values()) + [FUZIL, CARREGADOR] if not achar(n)]
    if faltando:
        raise SystemExit(f"falta no pacote {PACOTE}: {', '.join(faltando)}")
    bracos_fbx = os.path.join(SKETCHFAB, BRACOS_PACOTE, "source", BRACOS_FBX)
    if not os.path.isfile(bracos_fbx):
        raise SystemExit(f"falta {bracos_fbx} (veja docs/PACOTES.md)")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    cena = bpy.context.scene
    cena.render.fps = 30

    # --- Os nossos braços: o rig da AK sem a AK, sem animação.
    objs = importar(bracos_fbx)
    rig = next(o for o in objs if o.type == "ARMATURE")
    rig.name = "Bracos"
    camera = next(o for o in objs if o.type == "CAMERA")
    olho = camera.matrix_world.translation.copy()
    bracos = next(o for o in objs if o.type == "MESH" and o.name.startswith("arms"))
    bracos.name = "bracos"
    for o in objs:
        if o not in (rig, bracos):
            bpy.data.objects.remove(o)
    usar_acao(rig, None)
    for t in list(rig.animation_data.nla_tracks):
        rig.animation_data.nla_tracks.remove(t)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in list(rig.data.edit_bones):
        if eb.name.split("_end")[0] in OSSOS_DA_AK:
            rig.data.edit_bones.remove(eb)
    bpy.ops.object.mode_set(mode="OBJECT")

    # --- O manequim, com as animações do Jhon.
    objs = importar(achar(ANIMACOES["parado"]))
    fonte = next(o for o in objs if o.type == "ARMATURE")
    fonte.name = "Manequim"
    acoes = {"parado": fonte.animation_data.action}
    acoes["parado"].name = "parado"
    for jogo, arquivo in ANIMACOES.items():
        if jogo != "parado":
            acoes[jogo] = acao_de(importar(achar(arquivo)), jogo + "_ue")
    usar_acao(fonte, acoes["parado"])
    cena.frame_set(int(acoes["parado"].frame_range[0]))
    bpy.context.view_layer.update()
    achar_eixo_do_dedo(fonte)
    # O manequim olha para −Y como o nosso; só a posição muda: a cabeça
    # dele vai para a câmera do nosso.
    cabeca = fonte.matrix_world @ fonte.pose.bones[OSSO_OLHO].head
    desloca = Matrix.Translation(olho - cabeca)

    # --- O fuzil: a animação do ferrolho, quadro a quadro, nos ossos dele.
    fuzil_objs = importar(achar(FUZIL))
    arma = next(o for o in fuzil_objs if o.type == "ARMATURE")
    fuzil = next(o for o in fuzil_objs if o.type == "MESH")
    fuzil.name = "fuzil"
    carregador = next(o for o in importar(achar(CARREGADOR)) if o.type == "MESH")
    carregador.name = "carregador"
    quadros_arma = {}
    for jogo, arquivo in ANIMACOES_ARMA.items():
        acao = acao_de(importar(achar(arquivo)), jogo + "_arma")
        usar_acao(arma, acao)
        quadros = []
        for f in range(int(acao.frame_range[0]), int(acao.frame_range[1]) + 1):
            cena.frame_set(f)
            quadros.append((f, {pb.name: (pb.location.copy(), pb.rotation_quaternion.copy(), pb.scale.copy())
                                for pb in arma.pose.bones}))
        quadros_arma[jogo] = quadros
        usar_acao(arma, None)
        bpy.data.actions.remove(acao)

    # --- O osso do fuzil no nosso rig, na pose de parado do manequim.
    usar_acao(fonte, acoes["parado"])
    cena.frame_set(int(acoes["parado"].frame_range[0]))
    bpy.context.view_layer.update()
    pega = desloca @ so_rotacao(fonte.matrix_world @ fonte.pose.bones[OSSO_ARMA].matrix)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    eb = rig.data.edit_bones.new(OSSO_ARMA)
    eb.head = (0, 0, 0)
    eb.tail = (0, 0.05, 0)
    eb.matrix = rig.matrix_world.inverted() @ pega
    eb.parent = rig.data.edit_bones["Root"]
    bpy.ops.object.mode_set(mode="OBJECT")

    # O carregador: pesado no osso dele, no espaço do fuzil.
    carregador.parent = None
    carregador.matrix_world = arma.matrix_world @ arma.data.bones[OSSO_CARREGADOR].matrix_local
    grupo = carregador.vertex_groups.new(name=OSSO_CARREGADOR)
    grupo.add(range(len(carregador.data.vertices)), 1.0, "REPLACE")

    # O fuzil (em centímetros) no lugar do osso, e o esqueleto dele entra no nosso.
    escala_arma = arma.matrix_world.to_scale().x
    rig_pega = rig.matrix_world @ rig.data.bones[OSSO_ARMA].matrix_local
    arma.matrix_world = rig_pega @ Matrix.Scale(escala_arma, 4)
    bpy.context.view_layer.update()
    carregador.matrix_world = arma.matrix_world @ arma.data.bones[OSSO_CARREGADOR].matrix_local
    for o in (fuzil, carregador):
        m = o.matrix_world.copy()
        o.parent = rig
        o.matrix_world = m
        o.modifiers.clear()
        o.modifiers.new("Armature", "ARMATURE").object = rig
    bpy.ops.object.select_all(action="DESELECT")
    arma.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.join()
    bpy.ops.object.mode_set(mode="EDIT")
    for nome in ("Grip", "Bone"):
        osso = rig.data.edit_bones.get(nome)
        if osso is not None and osso.parent is None:
            osso.parent = rig.data.edit_bones[OSSO_ARMA]
            osso.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")

    # --- As animações, passadas para o nosso rig.
    print("passando as animações para os nossos braços:")
    novas = assar(rig, fonte, desloca, acoes, quadros_arma, escala_arma)
    bpy.data.objects.remove(fonte)
    for a in acoes.values():
        bpy.data.actions.remove(a)
    for jogo, acao in novas.items():
        trilha(rig, jogo, acao)

    # --- Materiais: os braços com as texturas do pacote deles; o fuzil com a dele.
    resolver_imagens()
    vestir(bracos.data.materials[0], achar_texturas(os.path.join(SKETCHFAB, BRACOS_PACOTE, "textures"), BRACO))
    texturas = {}
    for base, _, _ in os.walk(PACOTE):
        texturas.update({k: v for k, v in achar_texturas(base, TEXTURA).items() if k not in texturas})
    for mat in {m for o in (fuzil, carregador) for m in o.data.materials if m}:
        vestir(mat, texturas)
    print(f"textura do fuzil: {sorted(texturas) or 'nenhuma (sai cinza)'}")

    # --- O olho na origem, olhando para a frente do jogo.
    raiz = bpy.data.objects.new("Conjunto", None)
    cena.collection.objects.link(raiz)
    for o in [o for o in cena.objects if o.parent is None and o != raiz]:
        o.parent = raiz
    raiz.matrix_world = Matrix.Rotation(math.pi, 4, "Z") @ Matrix.Translation(-olho)
    bpy.context.view_layer.update()

    # --- A boca do cano, na pose de parado: o ponto mais à frente do fuzil.
    for t in rig.animation_data.nla_tracks:
        t.mute = t.name != "parado"
    cena.frame_set(int(novas["parado"].frame_range[0]))
    bpy.context.view_layer.update()
    grafo = bpy.context.evaluated_depsgraph_get()
    avaliado = fuzil.evaluated_get(grafo)
    pontos = [avaliado.matrix_world @ v.co for v in avaliado.to_mesh().vertices]
    frente = max(p.y for p in pontos)
    ponta = [p for p in pontos if p.y > frente - 0.01]
    cano = Vector((sum(p.x for p in ponta) / len(ponta), frente, sum(p.z for p in ponta) / len(ponta)))
    avaliado.to_mesh_clear()
    for t in rig.animation_data.nla_tracks:
        t.mute = False

    # --- Exporta.
    os.makedirs(DESTINO_FP, exist_ok=True)
    caminho = os.path.join(DESTINO_FP, NOME + ".glb")
    bpy.ops.export_scene.gltf(filepath=caminho, export_format="GLB", export_yup=True, export_apply=False,
                              export_animations=True, export_animation_mode="NLA_TRACKS", export_force_sampling=True,
                              export_optimize_animation_size=True, export_image_format="JPEG", export_image_quality=85,
                              export_cameras=False, export_lights=False, export_skins=True,
                              export_anim_scene_split_object=False)
    regravar_imagens(caminho, 86)

    fps = cena.render.fps
    animacoes = {jogo: dict(clipe=jogo, de=0.0, ate=round((a.frame_range[1] - a.frame_range[0]) / fps, 4))
                 for jogo, a in novas.items()}
    ficha_em = os.path.join(DESTINO_FP, "bracos.json")
    ficha = json.load(open(ficha_em, encoding="utf-8")) if os.path.isfile(ficha_em) else {}
    # No jogo: X é o mesmo, Y é o Z do Blender, Z é −Y.
    ficha[NOME] = dict(arquivo=f"res://assets/armas/fp/{NOME}.glb", armas=ARMAS, animacoes=animacoes,
                       cano=[round(cano.x, 4), round(cano.z, 4), round(-cano.y, 4)], ajuste=[0.0, 0.0, 0.0],
                       fov=FOV, sacar=True, fiel=True)
    with open(ficha_em, "w", encoding="utf-8") as f:
        json.dump(ficha, f, ensure_ascii=False, indent=1, sort_keys=True)
        f.write("\n")
    print(f"{NOME}: {sorted(animacoes)}, cano em {ficha[NOME]['cano']}, abertura {FOV}°")

    modelo_do_mundo(fuzil, carregador, rig, texturas)


# ===================================================================== na mão dos outros e no chão

def modelo_do_mundo(fuzil, carregador, rig, texturas):
    """O fuzil parado, em peças .obj, no formato de tools/gerar_armas.py:
    origem no punho, cano para −Z, em metros. O ferrolho e o carregador
    em nós próprios (o jogo os mexe na recarga)."""
    # Na pose de descanso, no espaço do fuzil (o de ik_hand_gun), em metros.
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    espaco = so_rotacao(rig.matrix_world @ rig.data.bones[OSSO_ARMA].matrix_local).inverted()

    grafo = bpy.context.evaluated_depsgraph_get()
    os.makedirs(DESTINO_ARMA, exist_ok=True)
    for n in os.listdir(DESTINO_ARMA):
        if n.endswith(".obj"):
            os.remove(os.path.join(DESTINO_ARMA, n))

    # Blender (x, y, z), cano para −Y -> jogo (−x, z, y), cano para −Z.
    def jogo(p):
        p = espaco @ p
        return (-p.x, p.z, p.y)

    ferrolho = fuzil.vertex_groups.get("Bolt")
    pecas = {"": [], "Ferrolho": []}
    avaliado = fuzil.evaluated_get(grafo)
    malha = avaliado.to_mesh()
    malha.calc_loop_triangles()
    uv = malha.uv_layers.active.data if malha.uv_layers.active else None
    for tri in malha.loop_triangles:
        vs = [malha.vertices[i] for i in tri.vertices]
        no_ferrolho = ferrolho is not None and all(
            any(g.group == ferrolho.index and g.weight > 0.5 for g in v.groups) for v in vs)
        pecas["Ferrolho" if no_ferrolho else ""].append(
            [(jogo(avaliado.matrix_world @ v.co), uv[li].uv if uv else (0, 0)) for v, li in zip(vs, tri.loops)])
    todos = [p for tris in pecas.values() for t in tris for p, _ in t]
    frente = min(p[2] for p in todos)
    ponta = [p for p in todos if p[2] < frente + 0.01]
    muzzle = [sum(p[0] for p in ponta) / len(ponta), sum(p[1] for p in ponta) / len(ponta), frente]
    if pecas["Ferrolho"]:
        fp = [p for t in pecas["Ferrolho"] for p, _ in t]
        ejecao = [max(p[0] for p in fp) + 0.01, sum(p[1] for p in fp) / len(fp), sum(p[2] for p in fp) / len(fp)]
    else:
        ejecao = [0.03, 0.04, -0.08]
    avaliado.to_mesh_clear()

    mag = carregador.evaluated_get(grafo)
    mm = mag.to_mesh()
    mm.calc_loop_triangles()
    uvm = mm.uv_layers.active.data if mm.uv_layers.active else None
    pecas["Magazine"] = [[(jogo(mag.matrix_world @ mm.vertices[i].co), uvm[li].uv if uvm else (0, 0))
                          for i, li in zip(t.vertices, t.loops)] for t in mm.loop_triangles]
    mag.to_mesh_clear()

    # A textura, ao lado das peças.
    cor = None
    if "cor" in texturas:
        cor = os.path.join(DESTINO_ARMA, "ar15_cor.png")
        img = bpy.data.images.load(texturas["cor"])
        if img.size[0] > 1024:
            img.scale(1024, 1024 * img.size[1] // img.size[0])
        img.filepath_raw = cor
        img.file_format = "PNG"
        img.save()
    material = "res://assets/armas/ar15/ar15_cor.png" if cor else "metal"

    partes = []
    for no, tris in pecas.items():
        if not tris:
            continue
        nome = f"{no.lower() or 'corpo'}_ar15"
        gravar_obj(os.path.join(DESTINO_ARMA, nome + ".obj"), tris)
        partes.append(dict(malha=f"res://assets/armas/ar15/{nome}.obj", material=material, no=no))

    lo = [round(min(p[i] for p in todos), 3) for i in range(3)]
    hi = [round(max(p[i] for p in todos), 3) for i in range(3)]
    ficha_em = os.path.join(RAIZ, "assets", "armas", "armas.json")
    ficha = json.load(open(ficha_em, encoding="utf-8"))
    ficha["armas"][NOME] = dict(
        externo=True, partes=partes, limites=[lo, hi],
        pontos=dict(Muzzle=[round(v, 4) for v in muzzle], Ejecao=[round(v, 4) for v in ejecao]),
        # Para as mãos de código (só se o conjunto animado faltar): as do M16.
        maos=ficha["armas"]["m16a2"]["maos"])
    with open(ficha_em, "w", encoding="utf-8") as fh:
        json.dump(ficha, fh, ensure_ascii=False, indent=1, sort_keys=True)
    print(f"{NOME}: {len(partes)} peças no mundo, {hi[2] - lo[2]:.2f} m de comprimento")


def gravar_obj(caminho, tris):
    linhas, vts = [], []
    for t in tris:
        for p, uv in t:
            linhas.append(f"v {p[0]:.5f} {p[1]:.5f} {p[2]:.5f}")
            vts.append(f"vt {uv[0]:.5f} {uv[1]:.5f}")
    faces = [f"f {i * 3 + 1}/{i * 3 + 1} {i * 3 + 2}/{i * 3 + 2} {i * 3 + 3}/{i * 3 + 3}" for i in range(len(tris))]
    with open(caminho, "w", encoding="utf-8") as fh:
        fh.write("\n".join(linhas + vts + faces) + "\n")


if __name__ == "__main__":
    main()
