#!/usr/bin/env python3
"""
As mãos padrão do jogo (``assets/armas/fp/maos/``) são os braços do
``RIG_Manny.blend`` do Jhon (``SK_Manny_Arms``), os mesmos que aparecem no
Blender — tirados do ``ar15.glb`` que o ``infima.py`` grava.

    python3 tools/armas/blender/infima.py   # primeiro (grava o ar15.glb)
    python3 tools/armas/blender/maos.py

Grava:

- ``maos.glb``: só a malha dos braços (o nó se chama ``maos``), presa ao
  esqueleto do manequim, sem fuzil e sem animação. O jogo a põe no
  esqueleto de cada arma desse esqueleto (scripts/ui/ViewModel.Maos.cs);
- ``maos_cor.jpg``, ``maos_normal.jpg``, ``maos_aspereza.jpg``: as
  texturas do material do Jhon (``M_FP_Arms``). O material do jogo é o
  ``padrao.tres``, que aponta para elas: trocar a textura ali troca as
  mãos de todas as armas.

A malha e a pose são as do arquivo: nada é ajustado.
"""
import os

import bpy

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(AQUI)))
FP = os.path.join(RAIZ, "assets", "armas", "fp")
ORIGEM = os.path.join(FP, "ar15.glb")
PASTA = os.path.join(FP, "maos")

BRACOS = "bracos"
# Entrada do material -> arquivo.
TEXTURAS = {"Base Color": "maos_cor.jpg", "Normal": "maos_normal.jpg", "Roughness": "maos_aspereza.jpg"}


def imagem_ligada(no, entrada, visto=None):
    """A imagem que chega, por qualquer caminho, a esta entrada do nó."""
    visto = visto or set()
    for link in no.inputs[entrada].links if entrada in no.inputs else []:
        de = link.from_node
        if de.name in visto:
            continue
        visto.add(de.name)
        if de.type == "TEX_IMAGE":
            return de.image
        for nome in [i.name for i in de.inputs]:
            achada = imagem_ligada(de, nome, visto)
            if achada:
                return achada
    return None


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=ORIGEM)
    bracos = bpy.data.objects[BRACOS]
    esqueleto = bracos.find_armature()
    for o in list(bpy.data.objects):
        if o not in (bracos, esqueleto):
            bpy.data.objects.remove(o, do_unlink=True)
    esqueleto.animation_data_clear()
    for acao in list(bpy.data.actions):
        bpy.data.actions.remove(acao)
    for osso in esqueleto.pose.bones:
        osso.matrix_basis.identity()
    bracos.name = "maos"
    bracos.data.name = "maos"

    os.makedirs(PASTA, exist_ok=True)
    material = bracos.data.materials[0]
    bsdf = next(n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    for entrada, arquivo in TEXTURAS.items():
        img = imagem_ligada(bsdf, entrada)
        if img is None:
            print(f"sem textura em {entrada}")
            continue
        copia = img.copy()
        copia.file_format = "JPEG"
        copia.save(filepath=os.path.join(PASTA, arquivo), quality=90)
        # O Godot recusa o JPEG que o Blender grava: regravado pelo Pillow.
        from PIL import Image
        Image.open(os.path.join(PASTA, arquivo)).convert("RGB").save(os.path.join(PASTA, arquivo), quality=90)
        print(f"{entrada}: {img.name} -> {arquivo} ({img.size[0]}x{img.size[1]})")

    bpy.ops.object.select_all(action="SELECT")
    destino = os.path.join(PASTA, "maos.glb")
    bpy.ops.export_scene.gltf(filepath=destino, export_format="GLB", use_selection=True, export_animations=False,
                              export_skins=True, export_image_format="NONE", export_materials="PLACEHOLDER",
                              export_cameras=False, export_lights=False)
    print(f"gravado {destino}: {os.path.getsize(destino) // 1024} KB, {len(bracos.data.vertices)} vértices")


if __name__ == "__main__":
    main()
