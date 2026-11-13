#!/usr/bin/env python3
"""
Os braços em primeira pessoa: os conjuntos animados do Sketchfab
(CC BY — créditos em docs/PACOTES.md e na tela de créditos), prontos
para o jogo.

    python3 tools/armas/blender/bracos.py          # todos
    python3 tools/armas/blender/bracos.py ak       # um só

Cada conjunto é um .fbx com os dois braços, uma arma e as animações
(parado, sacar, tiro, recarga…). Este script abre cada um no Blender e:

1. põe **o olho do jogador na origem, olhando para a frente do jogo** —
   no jogo o conjunto entra na câmera sem ajuste;
2. acerta a escala para metros;
3. monta os materiais com as texturas do pacote, reduzidas;
4. dá às animações os nomes que o jogo usa e joga fora o resto;
5. grava assets/armas/fp/<nome>.glb e a ficha assets/armas/fp/bracos.json
   (que arma usa que conjunto, que trecho de que clipe é cada animação,
   onde fica a boca do cano).

Precisa dos pacotes em assets/pacotes/sketchfab/ (veja docs/PACOTES.md)
e do módulo `bpy` (`pip install bpy`), ou rode com `blender -b -P`.

**Conjunto novo:** uma entrada em CONJUNTOS. `olho` (nas unidades do
arquivo) só é preciso quando o .fbx não traz câmera; `comprimento` é o
tamanho real da arma, em metros, quando o arquivo não está em metros.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(AQUI)))
PACOTES = os.path.join(RAIZ, "assets", "pacotes", "sketchfab")
DESTINO = os.path.join(RAIZ, "assets", "armas", "fp")

sys.path.insert(0, os.path.join(RAIZ, "tools", "mapas", "blender"))
from pecas_polyhaven import regravar_imagens  # noqa: E402

BRACO = "arm"

# nome: o pacote, o arquivo, as armas do jogo que usam o conjunto, as
# animações (nome do jogo -> ação do arquivo, ou (quadro inicial, quadro
# final) quando o arquivo traz tudo numa linha do tempo só), e o prefixo
# das texturas de cada material.
CONJUNTOS = {
    "ak": dict(
        pacote="fps-ak-74m-animations", fbx="FpsAnims.fbx", armas=["ak47"], arma="Ak",
        acoes=dict(parado="AK_Idle", sacar="AK_Draw", tiro="AK_Shot", recarga="AK_Reload", recarga_vazia="AK_Reload_full",
                   andar="AK_Walk", correr="AK_Run"),
        texturas={"arms": BRACO, "ak74m": "ak74m"}),
    "fuzil": dict(
        pacote="animated-fps-hands-rifle-animation-pack", fbx="Arm3.fbx", armas=["sg552"], arma="ACRRifle",
        acoes=dict(parado="Arms_FPS_Anim_Idle", sacar="Arms_FPS_Anim_Draw", tiro="Arms_FPS_Anim_Shoot",
                   recarga="Arms_FPS_Anim_Reload_F", andar="Arms_FPS_Anim_Walk", correr="Arms_FPS_Anim_Run"),
        texturas={"arms": BRACO, "Rifle": "Rifle", "Silencer": "Silencer", "Scope": "Scope", "Foregrip": "Rifle", "Pmag": "Pmag"}),
    "m16": dict(
        pacote="m16-a2-assault-rifle-animated", fbx="M16 A2 Rifle - Animated.fbx", armas=["m16a2"], arma="m16a2_base",
        linha=dict(parado=(224, 251), sacar=(206, 224), tiro=(2, 8), recarga=(71, 137), recarga_vazia=(8, 71)),
        olho=(0.09, 0.22, 0.26), texturas={"arms": BRACO, "m16a2": "m16a2"}),
    "smg": dict(
        pacote="smg-fps-animations", fbx="PistolArm.fbx", armas=["ump45"], arma="LP SST30",
        acoes=dict(parado="KDW_DPose_Idle", sacar="KDW_Draw", tiro="KDW_Shot", recarga="KDW_Reload_fast",
                   recarga_vazia="KDW_Reload_full", andar="KDW_Walk", correr="KDW_Run"),
        texturas={"arms": BRACO, "Material_457": "SST30_Worn_ODGreen", "Material_458": "SST30_Handguard_Worn_ODGreen",
                  "Material_459": "SST30_Worn_ODGreen"}),
    "pistola": dict(
        pacote="fps-pistol-animations", fbx="Armpist.fbx", armas=["m9"], arma="xd_frame",
        acoes=dict(parado="FPS_Pistol_Idle", tiro="FPS_Pistol_Fire", recarga="FPS_Pistol_Reload_easy",
                   recarga_vazia="FPS_Pistol_Reload_full", andar="FPS_Pistol_Walk"),
        texturas={"arms": BRACO, "Material": "for_texturing_None.001"}),
    "escopeta": dict(
        pacote="fps-benelli-m4-animations", fbx="HandWithGloves_AnimateRDY.fbx", armas=["m3"], arma="benelli_m4",
        acoes=dict(parado="M4_Idle", tiro="M4_Fire", recarga_um="M4_ReloadOne_type1", andar="M4_Walk", correr="M4_Run"),
        comprimento=1.01, olho=(0.0, 2.0, 36.5),
        texturas={"12gauge": "12gauge", "TTI_Benelli_M4": "TTI_Benelli_M4", "matchsaverz": "matchsaverz",
                  "sleeve_st6_generalist": "sleeve_st6_generalist", "glove_hardknuckle": "glove_hardknuckle"}),
}

PAPEIS = (("cor", ("basecolor", "base_color", "albedo", "diffuse", "color", "_base")),
          ("normal", ("normal",)), ("aspereza", ("roughness",)), ("metal", ("metallic",)))


def achar_texturas(pasta, prefixo):
    achadas = {}
    for nome in sorted(os.listdir(pasta)):
        raiz, ext = os.path.splitext(nome)
        if ext.lower() not in (".png", ".jpg", ".jpeg") or not raiz.lower().startswith(prefixo.lower()):
            continue
        resto = raiz[len(prefixo):].lower()
        for papel, chaves in PAPEIS:
            if papel not in achadas and any(c in resto for c in chaves):
                achadas[papel] = os.path.join(pasta, nome)
    return achadas


def vestir(material, texturas):
    """Monta o material com as texturas do pacote."""
    material.use_nodes = True
    nos, fios = material.node_tree.nodes, material.node_tree.links
    p = next((n for n in nos if n.type == "BSDF_PRINCIPLED"), None)
    if p is None:
        return
    for n in list(nos):
        if n.type in ("TEX_IMAGE", "NORMAL_MAP"):
            nos.remove(n)

    def imagem(caminho, dado):
        no = nos.new("ShaderNodeTexImage")
        no.image = bpy.data.images.load(caminho)
        lado = 2048 if os.path.basename(caminho).lower().startswith("armcolor") else 1024
        if no.image.size[0] > lado:
            no.image.scale(lado, lado * no.image.size[1] // no.image.size[0])
        if dado:
            no.image.colorspace_settings.name = "Non-Color"
        return no

    if "cor" in texturas:
        fios.new(imagem(texturas["cor"], False).outputs["Color"], p.inputs["Base Color"])
    if "normal" in texturas:
        mapa = nos.new("ShaderNodeNormalMap")
        fios.new(imagem(texturas["normal"], True).outputs["Color"], mapa.inputs["Color"])
        fios.new(mapa.outputs["Normal"], p.inputs["Normal"])
    if "aspereza" in texturas:
        fios.new(imagem(texturas["aspereza"], True).outputs["Color"], p.inputs["Roughness"])
    else:
        p.inputs["Roughness"].default_value = 0.55
    if "metal" in texturas:
        fios.new(imagem(texturas["metal"], True).outputs["Color"], p.inputs["Metallic"])
    p.inputs["Alpha"].default_value = 1.0
    material.surface_render_method = "DITHERED"


def usar(obj, acao):
    """Liga uma ação a um objeto (com a faixa dela, no Blender novo)."""
    if obj.animation_data is None:
        obj.animation_data_create()
    obj.animation_data.action = acao
    if hasattr(obj.animation_data, "action_slot") and acao is not None and getattr(acao, "slots", None):
        try:
            obj.animation_data.action_slot = acao.slots[0]
        except Exception:
            pass


def converter(nome, c):
    fbx = os.path.join(PACOTES, c["pacote"], "source", c["fbx"])
    if not os.path.isfile(fbx):
        print(f"{nome}: não achei {fbx}")
        return None

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    cena = bpy.context.scene
    esqueleto = next(o for o in cena.objects if o.type == "ARMATURE")

    # --- O olho: a câmera do arquivo, ou o ponto dado na tabela.
    camera = next((o for o in cena.objects if o.type == "CAMERA"), None)
    osso = esqueleto.data.bones.get("Head_Cam")
    if "olho" in c:
        olho = Vector(c["olho"])
    elif camera is not None:
        olho = camera.matrix_world.translation.copy()
    elif osso is not None:
        olho = esqueleto.matrix_world @ osso.head_local + Vector((0, 0, 0.021))
    else:
        olho = Vector((0, 0, 0))
    for o in [o for o in cena.objects if o.type in ("CAMERA", "LIGHT")]:
        bpy.data.objects.remove(o)

    # --- A escala: pelo comprimento da arma, quando o arquivo não está em metros.
    arma = next(o for o in cena.objects if o.type == "MESH" and o.name.startswith(c["arma"]))
    escala = 1.0
    if "comprimento" in c:
        cantos = [arma.matrix_world @ Vector(k) for k in arma.bound_box]
        maior = max(max(k[i] for k in cantos) - min(k[i] for k in cantos) for i in range(3))
        escala = c["comprimento"] / maior

    # --- Tudo debaixo de um nó só, com o olho na origem e a frente do
    #     arquivo (−Y do Blender) virada para a frente do jogo (+Y do
    #     Blender, que na exportação vira −Z).
    raiz = bpy.data.objects.new("Bracos", None)
    cena.collection.objects.link(raiz)
    for o in [o for o in cena.objects if o.parent is None and o != raiz]:
        o.parent = raiz
    raiz.matrix_world = Matrix.Rotation(math.pi, 4, "Z") @ Matrix.Scale(escala, 4) @ Matrix.Translation(-olho)

    # --- Materiais.
    pasta = os.path.join(PACOTES, c["pacote"], "textures")
    for mat in bpy.data.materials:
        prefixo = c["texturas"].get(mat.name) or c["texturas"].get(mat.name.split(".")[0])
        if prefixo:
            vestir(mat, achar_texturas(pasta, prefixo))

    # --- Animações.
    animacoes = {}
    if "linha" in c:
        # Tudo numa linha do tempo: exporta inteira e a ficha diz os trechos.
        cena.frame_start = min(a for a, _ in c["linha"].values())
        cena.frame_end = max(b for _, b in c["linha"].values())
        fps = cena.render.fps
        for jogo, (a, b) in c["linha"].items():
            animacoes[jogo] = dict(clipe="Scene", de=round((a - cena.frame_start) / fps, 4), ate=round((b - cena.frame_start) / fps, 4))
        modo = "SCENE"
        pose = c["linha"]["parado"][0]
    else:
        usar(esqueleto, None)
        for trilha in list(esqueleto.animation_data.nla_tracks):
            esqueleto.animation_data.nla_tracks.remove(trilha)
        acoes = {}
        for jogo, original in c["acoes"].items():
            candidatas = [a for a in bpy.data.actions if "Camera" not in a.name and a.name.split("|")[-1].startswith(original)]
            acao = min(candidatas, key=lambda a: len(a.name), default=None)
            if acao is None:
                print(f"{nome}: sem a ação {original}")
                continue
            acoes[jogo] = acao
            trilha = esqueleto.animation_data.nla_tracks.new()
            trilha.name = jogo
            tira = trilha.strips.new(jogo, 1, acao)
            if hasattr(tira, "action_slot") and getattr(acao, "slots", None):
                try:
                    tira.action_slot = acao.slots[0]
                except Exception:
                    pass
            fps = cena.render.fps
            animacoes[jogo] = dict(clipe=jogo, de=0.0, ate=round((acao.frame_range[1] - acao.frame_range[0]) / fps, 4))
        for acao in list(bpy.data.actions):
            if acao not in acoes.values():
                bpy.data.actions.remove(acao)
        modo = "NLA_TRACKS"
        pose = 1
        # Para medir a boca do cano, a pose de parado.
        for trilha in esqueleto.animation_data.nla_tracks:
            trilha.mute = trilha.name != "parado"

    # --- A boca do cano, na pose de parado: o ponto mais à frente da arma.
    cena.frame_set(pose)
    grafo = bpy.context.evaluated_depsgraph_get()
    avaliada = arma.evaluated_get(grafo)
    pontos = [avaliada.matrix_world @ v.co for v in avaliada.to_mesh().vertices]
    frente = max(p.y for p in pontos)
    ponta = [p for p in pontos if p.y > frente - 0.015]
    cano = Vector((sum(p.x for p in ponta) / len(ponta), frente, sum(p.z for p in ponta) / len(ponta)))
    if modo == "NLA_TRACKS":
        for trilha in esqueleto.animation_data.nla_tracks:
            trilha.mute = False

    os.makedirs(DESTINO, exist_ok=True)
    caminho = os.path.join(DESTINO, nome + ".glb")
    bpy.ops.export_scene.gltf(filepath=caminho, export_format="GLB", export_yup=True, export_apply=False,
                              export_animations=True, export_animation_mode=modo, export_force_sampling=True,
                              export_optimize_animation_size=True, export_image_format="JPEG", export_image_quality=85,
                              export_cameras=False, export_lights=False, export_skins=True, export_frame_range=(modo == "SCENE"),
                              export_anim_scene_split_object=False)
    regravar_imagens(caminho, 86)

    # No jogo: X é o mesmo, Y é o Z do Blender, Z é −Y.
    return dict(arquivo=f"res://assets/armas/fp/{nome}.glb", armas=c["armas"], animacoes=animacoes,
                cano=[round(cano.x, 4), round(cano.z, 4), round(-cano.y, 4)])


def main():
    filtro = sys.argv[1] if len(sys.argv) > 1 else ""
    ficha_em = os.path.join(DESTINO, "bracos.json")
    ficha = json.load(open(ficha_em, encoding="utf-8")) if os.path.isfile(ficha_em) else {}
    for nome, c in CONJUNTOS.items():
        if not nome.startswith(filtro):
            continue
        r = converter(nome, c)
        if r:
            ficha[nome] = r
            print(f"{nome}: {sorted(r['animacoes'])}, cano em {r['cano']}")
    with open(ficha_em, "w", encoding="utf-8") as f:
        json.dump(ficha, f, ensure_ascii=False, indent=1, sort_keys=True)
        f.write("\n")


if __name__ == "__main__":
    main()
