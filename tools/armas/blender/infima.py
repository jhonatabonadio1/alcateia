#!/usr/bin/env python3
"""
O AR-15, o fuzil padrão, em primeira pessoa, exatamente como o Jhon animou
no Blender: a cena dele (``Animations_Assault_Rifle.blend``, do "FPS
Tutorial Blender Starter Files" da InfimaGames, CC BY 4.0; o fuzil é de
TastyTony), os braços dele, o material dele, a câmera dele.

    python3 tools/armas/blender/infima.py

A cena puxa dois arquivos de ``../Rigs/``: ``Manny/RIG_Manny.blend`` (o rig,
com os braços que o Jhon pôs: o X Bot do Mixamo nos ossos do manequim, em
``SK_Manny_Arms``) e ``AssaultRifle/RIG_AssaultRifle.blend`` (o fuzil). O
script monta essa estrutura numa pasta temporária, abre a cena e:

- toca cada ação do Jhon no rig dele, com os constraints dele (a mão
  esquerda presa no carregador, a direita no ``ik_hand_gun``), quadro a
  quadro (a 60 por segundo, para as curvas chegarem inteiras);
- copia a pose final de cada osso que deforma a malha (os 89 do manequim)
  e dos ossos do fuzil para um esqueleto limpo, sem os controles;
- põe tudo no espaço da câmera dele: o olho na origem, olhando para a
  frente do jogo. A abertura vertical vai para a ficha (``fov``).

Nada é ajustado, trocado ou retargetado: as mãos, os dedos e o fuzil ficam
onde o Blender os mostra pela câmera da cena.

Sem o ``RIG_AssaultRifle.blend``, o fuzil vem dos .fbx do pacote
(``SK_AssaultRifle``, ``SM_AssaultRifle_Magazine``): o mesmo esqueleto (o
carregador fica onde o constraint da mão esquerda espera, 8 cm abaixo e à
frente do punho), no lugar do que falta. Sem a textura do fuzil
(``T_AssaultRifle_*``), ele sai cinza.

Grava:

- ``assets/armas/fp/ar15.glb`` e a entrada ``ar15`` de ``bracos.json``;
- ``assets/armas/ar15/`` e a entrada ``ar15`` de ``armas.json`` (o modelo
  na mão dos outros e no chão — scripts/visual/WeaponModel.cs).

Os arquivos do Jhon ficam em ``assets/pacotes/infima/`` (fora do git), em
qualquer subpasta: o script procura cada um pelo nome.
"""
import json
import math
import os
import shutil
import sys
import tempfile

import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(AQUI)))
PACOTE = os.path.join(RAIZ, "assets", "pacotes", "infima")
DESTINO_FP = os.path.join(RAIZ, "assets", "armas", "fp")
DESTINO_ARMA = os.path.join(RAIZ, "assets", "armas", "ar15")

sys.path.insert(0, AQUI)
sys.path.insert(0, os.path.join(RAIZ, "tools", "mapas", "blender"))
from bracos import achar_texturas, vestir  # noqa: E402
from pecas_polyhaven import regravar_imagens  # noqa: E402

NOME = "ar15"
ARMAS = ["ar15"]

CENA = "Animations_Assault_Rifle.blend"
# Onde a cena procura cada biblioteca, a partir da pasta dela.
BIBLIOTECAS = {
    "RIG_Manny.blend": os.path.join("Rigs", "Manny"),
    "RIG_AssaultRifle.blend": os.path.join("Rigs", "AssaultRifle"),
}

# Nome no jogo -> (ação do Jhon, marcador do fim). Todas começam no 0.
ANIMACOES = dict(
    parado=("A_FP_AssaultRifle_Idle_Pose_Loop", "END_IDDLE_LOOP"),
    andar=("A_FP_AssaultRifle_Walk_F_Loop", "END_WALK_LOOP"),
    sacar=("A_FP_AssaultRifle_Equip", "END_EQUIP_ANIMATION"),
    guardar=("A_FP_AssaultRifle_Holster", "END_HOLSTER_ANIMATION"),
    tiro=("A_FP_AssaultRifle_Fire", "END_FIRE"),
)
# A ação do próprio fuzil (ferrolho, gatilho, carregador), junto com a dos braços.
ANIMACOES_ARMA = dict(tiro="A_FP_WEP_AssaultRifle_Fire")

ESQUELETO = "Armature"
CAMERA = "Camera"
MALHA_BRACOS = "SK_Manny_Arms"
ESQUELETO_ARMA = "SKEL_AssaultRifle"
MALHA_FUZIL = "SK_AssaultRifle"
MALHA_CARREGADOR = "SK_AssaultRifle_Magazine"
FBX_FUZIL = "SK_AssaultRifle"
FBX_CARREGADOR = "SM_AssaultRifle_Magazine"
TEXTURA = "T_AssaultRifle"

OSSO_ARMA = "ik_hand_gun"
OSSO_CARREGADOR = "Magazine"

# Amostras por quadro da cena (30 por segundo): 2 dá 60 por segundo.
AMOSTRAS = 2


# ===================================================================== arquivos

def achar(nome, ext=""):
    """O arquivo do pacote com este nome, em qualquer subpasta."""
    alvo = (nome + ext).lower()
    for base, _, arquivos in os.walk(PACOTE):
        for a in arquivos:
            if a.lower() == alvo:
                return os.path.join(base, a)
    return None


def montar_pasta():
    """A cena e as bibliotecas na estrutura que a cena espera, numa pasta temporária."""
    cena = achar(CENA)
    if cena is None:
        raise SystemExit(f"falta {CENA} em {PACOTE}")
    pasta = tempfile.mkdtemp(prefix="infima_")
    os.makedirs(os.path.join(pasta, "Animations"))
    destino = os.path.join(pasta, "Animations", CENA)
    shutil.copy(cena, destino)
    for nome, sub in BIBLIOTECAS.items():
        achado = achar(nome)
        if achado:
            os.makedirs(os.path.join(pasta, sub), exist_ok=True)
            shutil.copy(achado, os.path.join(pasta, sub, nome))
        elif nome == "RIG_Manny.blend":
            raise SystemExit(f"falta {nome} em {PACOTE}")
        else:
            print(f"sem {nome}: o fuzil vem dos .fbx")
    return destino


def importar(caminho):
    bpy.context.view_layer.active_layer_collection = bpy.context.view_layer.layer_collection
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=caminho)
    return [o for o in bpy.data.objects if o not in antes]


def local(nome):
    """O objeto da cena (o override), não o da biblioteca."""
    return next(o for o in bpy.data.objects if o.name == nome and o.library is None)


def usar_acao(objeto, acao):
    if objeto.animation_data is None:
        objeto.animation_data_create()
    objeto.animation_data.action = acao
    if acao is not None and getattr(acao, "slots", None):
        objeto.animation_data.action_slot = acao.slots[0]


def trilha(objeto, nome, acao):
    usar_acao(objeto, None)
    t = objeto.animation_data.nla_tracks.new()
    t.name = nome
    tira = t.strips.new(nome, int(acao.frame_range[0]), acao)
    if hasattr(tira, "action_slot") and getattr(acao, "slots", None):
        tira.action_slot = acao.slots[0]
    return t


def resolver_imagens():
    """As imagens fora do .blend apontam para a pasta de quem salvou: acha
    cada uma no pacote pelo nome."""
    for img in bpy.data.images:
        if img.source != "FILE" or img.packed_file or not img.filepath:
            continue
        if os.path.isfile(bpy.path.abspath(img.filepath)):
            continue
        raiz, ext = os.path.splitext(os.path.basename(img.filepath.replace("\\", "/")))
        achado = achar(raiz, ext) if raiz else None
        if achado:
            img.filepath = achado
            img.reload()


def tem_imagem(mat):
    return mat.node_tree is not None and any(
        n.type == "TEX_IMAGE" and n.image is not None and n.image.size[0] > 0 for n in mat.node_tree.nodes)


def limpar(mat):
    """Tira os nós de imagem que não acharam a imagem (o glTF sairia branco):
    fica o valor do próprio material."""
    if mat.node_tree is None:
        return
    nos = mat.node_tree.nodes
    for n in list(nos):
        if n.type == "TEX_IMAGE" and (n.image is None or n.image.size[0] == 0):
            nos.remove(n)
    for n in list(nos):
        if n.type == "NORMAL_MAP" and not n.inputs["Color"].is_linked:
            nos.remove(n)


# ===================================================================== a cena

def fuzil_dos_fbx(skel):
    """Sem o RIG_AssaultRifle.blend: o esqueleto e as malhas dos .fbx no
    lugar do que falta. O esqueleto é o mesmo (os constraints acham os
    ossos pelo nome)."""
    faltando = [n for n in (FBX_FUZIL, FBX_CARREGADOR) if not achar(n, ".fbx")]
    if faltando:
        raise SystemExit(f"sem RIG_AssaultRifle.blend e sem {', '.join(faltando)}.fbx em {PACOTE}")
    objs = importar(achar(FBX_FUZIL, ".fbx"))
    arma = next(o for o in objs if o.type == "ARMATURE")
    fuzil = next(o for o in objs if o.type == "MESH")
    skel.data = arma.data
    bpy.context.view_layer.update()
    # A ação do tiro do fuzil é em ângulos de Euler.
    for pb in skel.pose.bones:
        pb.rotation_mode = "XYZ"
    carregador = next(o for o in importar(achar(FBX_CARREGADOR, ".fbx")) if o.type == "MESH")
    bpy.data.objects.remove(arma)
    return fuzil, carregador


def ordem(ossos):
    """Os ossos de pai para filho."""
    feitos, fila = [], list(ossos)
    while fila:
        for b in list(fila):
            if b.parent is None or b.parent.name in {f.name for f in feitos} or b.parent not in ossos:
                feitos.append(b)
                fila.remove(b)
    return feitos


def pai_que_fica(b, ficam):
    p = b.parent
    while p is not None and p.name not in ficam:
        p = p.parent
    return p


def main():
    caminho = montar_pasta()
    bpy.ops.wm.open_mainfile(filepath=caminho)
    cena = bpy.context.scene
    fps = cena.render.fps
    unidade = cena.unit_settings.scale_length or 1.0

    rig = local(ESQUELETO)
    camera = local(CAMERA)
    skel = local(ESQUELETO_ARMA)
    bracos_fonte = local(MALHA_BRACOS)
    for o in (rig, skel):
        if o.animation_data:
            for t in o.animation_data.nla_tracks:
                t.mute = True

    if len(skel.data.bones) == 0:
        fuzil_fonte, carregador_fonte = fuzil_dos_fbx(skel)
    else:
        fuzil_fonte, carregador_fonte = local(MALHA_FUZIL), local(MALHA_CARREGADOR)
    usar_acao(skel, None)
    pose_fuzil = {pb.name: pb.matrix_basis.copy() for pb in skel.pose.bones}

    marcadores = {m.name: m.frame for m in cena.timeline_markers}
    acoes = {}
    for jogo, (acao, fim) in ANIMACOES.items():
        if acao not in bpy.data.actions or fim not in marcadores:
            raise SystemExit(f"{jogo}: falta a ação {acao} ou o marcador {fim} na cena")
        acoes[jogo] = (bpy.data.actions[acao], 0, marcadores[fim])
    acoes_arma = {jogo: bpy.data.actions[n] for jogo, n in ANIMACOES_ARMA.items()}

    # --- A câmera: tem de ficar parada (os braços vão no espaço dela).
    def olho():
        m = camera.matrix_world
        return Matrix.Translation(m.translation) @ m.to_quaternion().to_matrix().to_4x4()

    usar_acao(rig, acoes["parado"][0])
    cena.frame_set(0)
    olho0 = olho()
    # Câmera do Blender: olha para −Z com +Y em cima. A do contrato do
    # conjunto (como o dos outros braços): olho na origem, olhando para +Y
    # com +Z em cima; o glTF leva +Y para o −Z do jogo.
    para_olho = Matrix.Rotation(math.radians(90), 4, "X") @ olho0.inverted()
    largura, altura = cena.render.resolution_x, cena.render.resolution_y
    # Com o encaixe automático do sensor, a abertura da lente vale para o
    # lado maior da imagem; o jogo quer a vertical.
    if largura >= altura:
        vertical = 2 * math.atan(math.tan(camera.data.angle / 2) * altura / largura)
    else:
        vertical = camera.data.angle
    fov = round(math.degrees(vertical), 2)

    # --- O esqueleto limpo: os ossos que deformam, mais os do fuzil.
    deformam = [b for b in rig.data.bones if b.use_deform]
    nomes = {b.name for b in deformam}
    ossos_fuzil = [b for b in skel.data.bones if not b.name.endswith("_end")]
    pega0 = rig.matrix_world @ rig.data.bones[OSSO_ARMA].matrix_local

    dados = bpy.data.armatures.new("Bracos")
    novo = bpy.data.objects.new("Bracos", dados)
    cena.collection.objects.link(novo)
    bpy.context.view_layer.active_layer_collection = bpy.context.view_layer.layer_collection
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = novo
    novo.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for b in deformam:
        eb = dados.edit_bones.new(b.name)
        eb.head, eb.tail = (0, 0, 0), (0, b.length, 0)
        eb.matrix = rig.matrix_world @ b.matrix_local
    for b in deformam:
        p = pai_que_fica(b, nomes)
        if p is not None:
            dados.edit_bones[b.name].parent = dados.edit_bones[p.name]
    for b in ossos_fuzil:
        eb = dados.edit_bones.new(b.name)
        eb.head, eb.tail = (0, 0, 0), (0, max(b.length, 0.5), 0)
        eb.matrix = pega0 @ b.matrix_local
    for b in ossos_fuzil:
        eb = dados.edit_bones[b.name]
        eb.parent = dados.edit_bones[b.parent.name] if b.parent and b.parent.name in dados.edit_bones else dados.edit_bones[OSSO_ARMA]
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in novo.pose.bones:
        pb.rotation_mode = "QUATERNION"

    # --- As malhas, cópias locais, pesadas no esqueleto limpo.
    def copiar(fonte, nome, mundo):
        malha = fonte.data.copy()
        # Materiais locais (os da cena vêm da biblioteca), no mesmo lugar:
        # cada face continua com o dela.
        for i, m in enumerate(malha.materials):
            if m is not None:
                malha.materials[i] = m.copy()
        o = bpy.data.objects.new(nome, malha)
        cena.collection.objects.link(o)
        for g in fonte.vertex_groups:
            if o.vertex_groups.get(g.name) is None:
                o.vertex_groups.new(name=g.name)
        o.matrix_world = mundo
        o.parent = novo
        o.matrix_world = mundo
        o.modifiers.new("Armature", "ARMATURE").object = novo
        return o

    bracos = copiar(bracos_fonte, "bracos", bracos_fonte.matrix_world.copy())
    fuzil = copiar(fuzil_fonte, "fuzil", pega0)
    carregador = copiar(carregador_fonte, "carregador", pega0 @ skel.data.bones[OSSO_CARREGADOR].matrix_local)
    if not carregador.vertex_groups.get(OSSO_CARREGADOR):
        g = carregador.vertex_groups.new(name=OSSO_CARREGADOR)
        g.add(range(len(carregador.data.vertices)), 1.0, "REPLACE")

    # --- Cada animação, quadro a quadro: a pose final de cada osso.
    na_ordem = ordem(list(novo.data.bones))
    fontes = {b.name: (rig, b.name) for b in deformam}
    fontes.update({b.name: (skel, b.name) for b in ossos_fuzil})
    novas = {}
    for jogo, (acao, inicio, fim) in acoes.items():
        usar_acao(rig, acao)
        usar_acao(skel, acoes_arma.get(jogo))
        if jogo not in acoes_arma:
            for pb in skel.pose.bones:
                pb.matrix_basis = pose_fuzil[pb.name]
        nova = bpy.data.actions.new(jogo)
        nova.use_fake_user = True
        usar_acao(novo, nova)
        antes = {}
        for i in range((fim - inicio) * AMOSTRAS + 1):
            q = inicio + i / AMOSTRAS
            cena.frame_set(int(math.floor(q)), subframe=q - math.floor(q))
            alvo = {}
            for nome, (obj, osso) in fontes.items():
                alvo[nome] = obj.matrix_world @ obj.pose.bones[osso].matrix
            for b in na_ordem:
                rel = b.parent.matrix_local.inverted() @ b.matrix_local if b.parent else b.matrix_local
                base_pai = alvo[b.parent.name] if b.parent else novo.matrix_world
                pb = novo.pose.bones[b.name]
                pb.matrix_basis = (base_pai @ rel).inverted() @ alvo[b.name]
                giro = pb.rotation_quaternion.copy()
                if b.name in antes and antes[b.name].dot(giro) < 0:
                    pb.rotation_quaternion = -giro
                antes[b.name] = pb.rotation_quaternion.copy()
                for c in ("location", "rotation_quaternion", "scale"):
                    pb.keyframe_insert(c, frame=i)
        usar_acao(novo, None)
        novas[jogo] = (nova, fim - inicio)
        print(f"  {jogo}: {acao.name}, quadros {inicio} a {fim}")
    usar_acao(rig, None)
    usar_acao(skel, None)

    # --- Só o que vai para o jogo fica na cena.
    fica = {novo, bracos, fuzil, carregador}
    for o in list(cena.objects):
        if o not in fica:
            o.hide_render = True
    for jogo, (acao, _) in novas.items():
        trilha(novo, jogo, acao)
    cena.render.fps = fps * AMOSTRAS

    # --- As texturas: as que a cena aponta, achadas no pacote; o fuzil
    # dos .fbx, sem textura nenhuma, ganha a do pacote se ela vier. O
    # pacote não trouxe a do fuzil: a que o Jhon pôs em assets/armas/ar15
    # (T_AssaultRifle_D.png, a paleta de cores, 16/11) vale se faltar lá.
    resolver_imagens()
    texturas = {}
    for base, _, _ in os.walk(PACOTE):
        texturas.update({k: v for k, v in achar_texturas(base, TEXTURA).items() if k not in texturas})
    if os.path.isdir(DESTINO_ARMA):
        texturas.update({k: v for k, v in achar_texturas(DESTINO_ARMA, TEXTURA).items() if k not in texturas})
    for mat in {m for o in (fuzil, carregador) for m in o.data.materials if m}:
        if not tem_imagem(mat) and texturas:
            vestir(mat, texturas)
        limpar(mat)
    print(f"textura do fuzil: {sorted(texturas) or 'nenhuma (sai cinza)'}")

    # --- O olho na origem, em metros.
    raiz = bpy.data.objects.new("Conjunto", None)
    cena.collection.objects.link(raiz)
    novo.parent = raiz
    raiz.matrix_world = Matrix.Scale(unidade, 4) @ para_olho
    novo.matrix_parent_inverse = Matrix.Identity(4)
    bpy.context.view_layer.update()

    # --- A boca do cano, na pose de parado: o ponto mais à frente do fuzil.
    for t in novo.animation_data.nla_tracks:
        t.mute = t.name != "parado"
    cena.frame_set(0)
    bpy.context.view_layer.update()
    grafo = bpy.context.evaluated_depsgraph_get()
    avaliado = fuzil.evaluated_get(grafo)
    pontos = [avaliado.matrix_world @ v.co for v in avaliado.to_mesh().vertices]
    frente = max(p.y for p in pontos)
    ponta = [p for p in pontos if p.y > frente - 0.01]
    cano = Vector((sum(p.x for p in ponta) / len(ponta), frente, sum(p.z for p in ponta) / len(ponta)))
    avaliado.to_mesh_clear()
    for t in novo.animation_data.nla_tracks:
        t.mute = False

    # --- Exporta só o conjunto.
    bpy.ops.object.select_all(action="DESELECT")
    for o in (raiz, novo, bracos, fuzil, carregador):
        o.select_set(True)
    os.makedirs(DESTINO_FP, exist_ok=True)
    saida = os.path.join(DESTINO_FP, NOME + ".glb")
    bpy.ops.export_scene.gltf(filepath=saida, export_format="GLB", use_selection=True, export_yup=True,
                              export_apply=False, export_animations=True, export_animation_mode="NLA_TRACKS",
                              export_force_sampling=True, export_optimize_animation_size=True,
                              export_image_format="JPEG", export_image_quality=85, export_cameras=False,
                              export_lights=False, export_skins=True, export_anim_scene_split_object=False)
    regravar_imagens(saida, 86)

    animacoes = {jogo: dict(clipe=jogo, de=0.0, ate=round(quadros / fps, 4)) for jogo, (_, quadros) in novas.items()}
    ficha_em = os.path.join(DESTINO_FP, "bracos.json")
    ficha = json.load(open(ficha_em, encoding="utf-8")) if os.path.isfile(ficha_em) else {}
    # No jogo: X é o mesmo, Y é o Z do Blender, Z é −Y.
    ficha[NOME] = dict(arquivo=f"res://assets/armas/fp/{NOME}.glb", armas=ARMAS, animacoes=animacoes,
                       cano=[round(cano.x, 4), round(cano.z, 4), round(-cano.y, 4)], ajuste=[0.0, 0.0, 0.0],
                       fov=fov, sacar=True, fiel=True)
    with open(ficha_em, "w", encoding="utf-8") as f:
        json.dump(ficha, f, ensure_ascii=False, indent=1, sort_keys=True)
        f.write("\n")
    print(f"{NOME}: {sorted(animacoes)}, cano em {ficha[NOME]['cano']}, abertura vertical {fov}°")

    modelo_do_mundo(fuzil, carregador, novo, texturas)


# ===================================================================== na mão dos outros e no chão

def so_rotacao(m):
    """A matriz sem escala."""
    loc, rot, _ = m.decompose()
    return Matrix.Translation(loc) @ rot.to_matrix().to_4x4()


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
    rig.data.pose_position = "POSE"

    # A textura, ao lado das peças.
    cor = None
    if "cor" in texturas and os.path.dirname(os.path.abspath(texturas["cor"])) == os.path.abspath(DESTINO_ARMA):
        cor = texturas["cor"]  # já está ao lado das peças
    elif "cor" in texturas:
        cor = os.path.join(DESTINO_ARMA, "ar15_cor.png")
        img = bpy.data.images.load(texturas["cor"])
        if img.size[0] > 1024:
            img.scale(1024, 1024 * img.size[1] // img.size[0])
        img.filepath_raw = cor
        img.file_format = "PNG"
        img.save()
    material = f"res://assets/armas/ar15/{os.path.basename(cor)}" if cor else "metal"

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
