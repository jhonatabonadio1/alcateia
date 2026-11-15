#!/usr/bin/env python3
"""
O AR-15, o fuzil padrão, em primeira pessoa: as animações que o Jhon fez
com o pacote da InfimaGames ("FPS Tutorial Blender Starter Files",
CC BY 4.0; o fuzil é de TastyTony), prontas para o jogo.

    python3 tools/armas/blender/infima.py

O pacote foi feito para a Unreal e vem em arquivos separados:

- os braços: o manequim da UE5 de primeira pessoa (malha da Epic — veja
  docs/PENDENCIAS.md, a licença precisa ser resolvida antes de publicar);
- as animações, uma por arquivo (``A_FP_AssaultRifle_*.fbx``), no
  esqueleto do manequim;
- o fuzil (``SK_AssaultRifle.fbx``), com o esqueleto dele (Grip, Trigger,
  Bolt, Magazine), a animação dele no tiro (``A_FP_WEP_*``), o carregador
  e a cápsula, e a textura (``T_AssaultRifle_*``).

Este script monta como a Unreal monta: o fuzil preso no osso
``ik_hand_gun`` (o pacote anima esse osso junto com a mão direita), o
carregador no osso ``Magazine`` do fuzil e o olho do jogador no osso da
cabeça (o pacote não traz câmera). Grava:

- ``assets/armas/fp/ar15.glb`` e a entrada ``ar15`` de ``bracos.json``
  (primeira pessoa, como os outros conjuntos — tools/armas/blender/bracos.py);
- ``assets/armas/ar15/`` e a entrada ``ar15`` de ``armas.json`` (o modelo
  na mão dos outros e no chão — scripts/visual/WeaponModel.cs).

Os arquivos ficam em ``assets/pacotes/infima/`` (fora do git), em qualquer
subpasta: o script procura pelo nome. Sem a malha dos braços, monta só o
fuzil (serve para conferir o olho e a pega); sem a textura, o fuzil sai
cinza.
"""
import json
import math
import os
import shutil
import sys

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

OSSO_ARMA = "ik_hand_gun"
OSSO_CARREGADOR = "Magazine"
OSSO_OLHO = "head"

# O olho, a partir do osso da cabeça, em metros (x para a direita do
# boneco, y para trás, z para cima: o espaço do Blender com o boneco
# olhando para −Y). O pacote não traz câmera; no osso da cabeça a arma
# fica onde a Unreal a mostra.
OLHO = (0.0, 0.0, 0.0)

# A abertura vertical da câmera para a qual o pacote foi animado: a da
# Unreal, 90° na horizontal em 16:9.
FOV = round(math.degrees(2 * math.atan(math.tan(math.radians(45)) * 9 / 16)), 1)

# O pacote é da Unreal, onde o coice e o balanço do passo são de código:
# as animações de tiro e de andar mexem poucos milímetros. No jogo, o
# coice de código vale mais neste conjunto que nos outros (0,3), e o
# balanço do passo continua ligado (bracos.json: "coice", "balanco").
COICE = 0.4

# Nomes que a malha dos braços pode ter no pacote (o manequim de primeira
# pessoa exportado).
BRACOS = ("fp", "firstperson", "first_person", "arms")


def achar(nome, ext=".fbx"):
    """O arquivo do pacote com este nome, em qualquer subpasta."""
    alvo = (nome + ext).lower()
    for base, _, arquivos in os.walk(PACOTE):
        for a in arquivos:
            if a.lower() == alvo:
                return os.path.join(base, a)
    return None


def achar_bracos():
    """A malha dos braços: um .fbx de manequim (Manny/Mannequin/SKM) de
    primeira pessoa. None se não está no pacote."""
    for base, _, arquivos in os.walk(PACOTE):
        for a in sorted(arquivos):
            nome = a.lower()
            if not nome.endswith(".fbx") or nome.startswith(("a_fp", "sk_assault", "sm_")):
                continue
            if any(k in nome for k in ("manny", "mannequin", "skm_", "quinn")) and any(k in nome for k in BRACOS):
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


def trilha(objeto, nome, acao):
    if objeto.animation_data is None:
        objeto.animation_data_create()
    objeto.animation_data.action = None
    t = objeto.animation_data.nla_tracks.new()
    t.name = nome
    tira = t.strips.new(nome, int(acao.frame_range[0]), acao)
    if hasattr(tira, "action_slot") and getattr(acao, "slots", None):
        try:
            tira.action_slot = acao.slots[0]
        except Exception:
            pass
    return t


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


def main():
    if not os.path.isdir(PACOTE):
        raise SystemExit(f"falta o pacote em {PACOTE} (veja docs/PACOTES.md)")
    faltando = [n for n in list(ANIMACOES.values()) + list(ANIMACOES_ARMA.values()) + [FUZIL, CARREGADOR] if not achar(n)]
    if faltando:
        raise SystemExit(f"falta no pacote: {', '.join(faltando)}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    cena = bpy.context.scene
    cena.render.fps = 30

    # --- Os braços (ou só o esqueleto, sem a malha).
    malha_bracos = achar_bracos()
    if malha_bracos:
        objetos = importar(malha_bracos)
        print(f"braços: {os.path.basename(malha_bracos)}")
    else:
        objetos = importar(achar(ANIMACOES["parado"]))
        print("braços: a malha não está no pacote — só o fuzil")
    esqueleto = next(o for o in objetos if o.type == "ARMATURE")
    esqueleto.name = "Bracos"
    if esqueleto.animation_data:
        esqueleto.animation_data.action = None
    malhas_bracos = [o for o in objetos if o.type == "MESH"]

    # --- As animações dos braços, uma trilha cada.
    acoes = {jogo: acao_de(importar(achar(arquivo)), jogo) for jogo, arquivo in ANIMACOES.items()}

    # --- O fuzil: o esqueleto dele entra no dos braços, com o Grip filho de
    #     ik_hand_gun (como a Unreal prende a arma no osso). Na pose de
    #     descanso o fuzil fica no lugar de ik_hand_gun; em qualquer pose,
    #     segue o osso.
    fuzil_objs = importar(achar(FUZIL))
    arma = next(o for o in fuzil_objs if o.type == "ARMATURE")
    fuzil = next(o for o in fuzil_objs if o.type == "MESH")
    fuzil.name = "fuzil"
    carregador = next(o for o in importar(achar(CARREGADOR)) if o.type == "MESH")
    carregador.name = "carregador"

    # A animação do próprio fuzil (ferrolho, gatilho), quadro a quadro, nos
    # ossos dele: entra depois na ação de mesmo nome dos braços.
    quadros_arma = {}
    for jogo, arquivo in ANIMACOES_ARMA.items():
        acao = acao_de(importar(achar(arquivo)), jogo + "_arma")
        if arma.animation_data is None:
            arma.animation_data_create()
        usar_acao(arma, acao)
        quadros = []
        for f in range(int(acao.frame_range[0]), int(acao.frame_range[1]) + 1):
            cena.frame_set(f)
            quadros.append((f, {pb.name: (pb.location.copy(), pb.rotation_quaternion.copy(), pb.scale.copy())
                                for pb in arma.pose.bones}))
        quadros_arma[jogo] = quadros
        usar_acao(arma, None)
        bpy.data.actions.remove(acao)

    # O carregador vira peça do fuzil, presa (pesada) no osso dele.
    carregador.parent = None
    grupo = carregador.vertex_groups.new(name=OSSO_CARREGADOR)
    grupo.add(range(len(carregador.data.vertices)), 1.0, "REPLACE")

    # O fuzil no lugar de ik_hand_gun, na pose de descanso dos braços.
    arma.matrix_world = esqueleto.matrix_world @ esqueleto.data.bones[OSSO_ARMA].matrix_local
    bpy.context.view_layer.update()
    for o in (fuzil, carregador):
        m = o.matrix_world.copy()
        o.parent = esqueleto
        o.matrix_world = m
        o.modifiers.clear()
        mod = o.modifiers.new("Armature", "ARMATURE")
        mod.object = esqueleto
    # O carregador estava no espaço do fuzil: vai com ele para ik_hand_gun.
    carregador.matrix_world = arma.matrix_world @ arma.data.bones[OSSO_CARREGADOR].matrix_local
    bpy.context.view_layer.update()

    bpy.ops.object.select_all(action="DESELECT")
    arma.select_set(True)
    esqueleto.select_set(True)
    bpy.context.view_layer.objects.active = esqueleto
    bpy.ops.object.join()
    bpy.ops.object.mode_set(mode="EDIT")
    ossos = esqueleto.data.edit_bones
    for nome in ("Grip", "Bone"):
        if nome in ossos and ossos[nome].parent is None:
            ossos[nome].parent = ossos[OSSO_ARMA]
            ossos[nome].use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")

    # Os quadros do fuzil entram na ação dos braços de mesmo nome.
    for jogo, quadros in quadros_arma.items():
        usar_acao(esqueleto, acoes[jogo])
        for f, poses in quadros:
            for nome, (loc, rot, esc) in poses.items():
                pb = esqueleto.pose.bones.get(nome)
                if pb is None:
                    continue
                pb.rotation_mode = "QUATERNION"
                pb.location, pb.rotation_quaternion, pb.scale = loc, rot, esc
                for caminho in ("location", "rotation_quaternion", "scale"):
                    pb.keyframe_insert(caminho, frame=f)
        usar_acao(esqueleto, None)
    for jogo, acao in acoes.items():
        trilha(esqueleto, jogo, acao)

    # --- Materiais.
    resolver_imagens()
    texturas = {}
    for base, _, arquivos in os.walk(PACOTE):
        texturas.update({k: v for k, v in achar_texturas(base, TEXTURA).items() if k not in texturas})
    for mat in {m for o in (fuzil, carregador) for m in o.data.materials if m}:
        vestir(mat, texturas)
    print(f"textura do fuzil: {sorted(texturas) or 'nenhuma (sai cinza)'}")

    # --- O olho na origem, olhando para a frente do jogo.
    olho = esqueleto.matrix_world @ esqueleto.pose.bones[OSSO_OLHO].head + Vector(OLHO)
    raiz = bpy.data.objects.new("Conjunto", None)
    cena.collection.objects.link(raiz)
    for o in [o for o in cena.objects if o.parent is None and o != raiz]:
        o.parent = raiz
    raiz.matrix_world = Matrix.Rotation(math.pi, 4, "Z") @ Matrix.Translation(-olho)
    bpy.context.view_layer.update()

    # --- A boca do cano, na pose de parado: o ponto mais à frente do fuzil.
    so_parado(esqueleto)
    cena.frame_set(int(acoes["parado"].frame_range[0]))
    bpy.context.view_layer.update()
    grafo = bpy.context.evaluated_depsgraph_get()
    avaliado = fuzil.evaluated_get(grafo)
    pontos = [avaliado.matrix_world @ v.co for v in avaliado.to_mesh().vertices]
    frente = max(p.y for p in pontos)
    ponta = [p for p in pontos if p.y > frente - 0.01]
    cano = Vector((sum(p.x for p in ponta) / len(ponta), frente, sum(p.z for p in ponta) / len(ponta)))
    avaliado.to_mesh_clear()
    for t in esqueleto.animation_data.nla_tracks:
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
                 for jogo, a in acoes.items()}
    ficha_em = os.path.join(DESTINO_FP, "bracos.json")
    ficha = json.load(open(ficha_em, encoding="utf-8")) if os.path.isfile(ficha_em) else {}
    # No jogo: X é o mesmo, Y é o Z do Blender, Z é −Y.
    ficha[NOME] = dict(arquivo=f"res://assets/armas/fp/{NOME}.glb", armas=ARMAS, animacoes=animacoes,
                       cano=[round(cano.x, 4), round(cano.z, 4), round(-cano.y, 4)], ajuste=[0.0, 0.0, 0.0],
                       fov=FOV, sacar=True, coice=COICE, balanco=True)
    with open(ficha_em, "w", encoding="utf-8") as f:
        json.dump(ficha, f, ensure_ascii=False, indent=1, sort_keys=True)
        f.write("\n")
    print(f"{NOME}: {sorted(animacoes)}, cano em {ficha[NOME]['cano']}, abertura {FOV}°")

    modelo_do_mundo(fuzil, carregador, esqueleto, texturas)
    if not malhas_bracos:
        print("AVISO: sem a malha dos braços — o conjunto mostra só o fuzil.")


def so_parado(*objetos):
    for o in objetos:
        for t in o.animation_data.nla_tracks:
            t.mute = t.name != "parado"


# ===================================================================== na mão dos outros e no chão

def modelo_do_mundo(fuzil, carregador, esqueleto, texturas):
    """O fuzil parado, em peças .obj, no formato de tools/gerar_armas.py:
    origem no punho, cano para −Z, em metros. O ferrolho e o carregador
    em nós próprios (o jogo os mexe na recarga)."""
    # Na pose de descanso, no espaço do fuzil (o de ik_hand_gun), em metros.
    esqueleto.data.pose_position = "REST"
    bpy.context.view_layer.update()
    espaco = (esqueleto.matrix_world @ esqueleto.data.bones[OSSO_ARMA].matrix_local).inverted()

    grafo = bpy.context.evaluated_depsgraph_get()
    os.makedirs(DESTINO_ARMA, exist_ok=True)
    for n in os.listdir(DESTINO_ARMA):
        if n.endswith(".obj"):
            os.remove(os.path.join(DESTINO_ARMA, n))

    # Blender (x, y, z), cano para −Y -> jogo (−x, z, y), cano para −Z.
    def jogo(p):
        return (-p.x, p.z, p.y)

    def no_fuzil(p):
        return (espaco @ p) * 0.01

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
            [(jogo(no_fuzil(avaliado.matrix_world @ v.co)), uv[li].uv if uv else (0, 0)) for v, li in zip(vs, tri.loops)])
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
    pecas["Magazine"] = [[(jogo(no_fuzil(mag.matrix_world @ mm.vertices[i].co)), uvm[li].uv if uvm else (0, 0))
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
