#!/usr/bin/env python3
"""
Os objetos de cenário do Poly Haven (CC0), prontos para o jogo.

    python3 tools/mapas/blender/pecas_polyhaven.py          # todos
    python3 tools/mapas/blender/pecas_polyhaven.py barril   # só os que começam assim

Abre cada modelo baixado em assets/pacotes/polyhaven/modelos/ no
Blender, junta as malhas, põe a origem no meio da base, reduz a
malha (até 9 mil faces) e as texturas (512 px para o que é pequeno, 1024
para o que é grande) e grava
em assets/mapas/pecas/<nome>.glb. No fim imprime o tamanho de cada um —
é o que se usa para a caixa de colisão em tools/mapas/pecas.py (OBJETOS).

Para acrescentar um objeto: baixe-o (tools/baixar_pacotes.py), ponha a
linha na tabela, rode, e copie o tamanho para OBJETOS.
"""
import os
import sys

import bpy
from mathutils import Vector

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
ORIGEM = os.path.join(RAIZ, "assets", "pacotes", "polyhaven", "modelos")
DESTINO = os.path.join(RAIZ, "assets", "mapas", "pecas")

FACES = 9000

# nome no jogo: (id no Poly Haven, lado da textura)
TABELA = {
    "barril_metal": ("Barrel_01", 512), "barril_azul": ("barrel_03", 512), "barril_vinho": ("wine_barrel_01", 512),
    "caixote_a": ("wooden_crate_01", 512), "caixote_b": ("wooden_crate_02", 512),
    "caixa_militar": ("old_military_crate", 512), "caixa_militar_madeira": ("wooden_military_crate", 512),
    "caixa_municao": ("ammo_box", 512), "caixa_papelao": ("cardboard_box_01", 512),
    "barreira": ("concrete_road_barrier", 512), "saco_cimento": ("cement_bag", 512), "saco_lixo": ("trashbag", 512),
    "pneu": ("old_tyre", 512), "galao_verde": ("metal_jerrycan_green", 512), "galao_plastico": ("plastic_jerrycan", 512),
    "botijao": ("propane_tank", 512), "gerador": ("portable_generator", 1024), "compressor": ("old_military_compressor", 1024),
    "lixeira": ("metal_trash_can", 512), "placa_piso": ("WetFloorSign_01", 512),
    "carrinho": ("hand_truck", 512), "estante_b": ("steel_frame_shelves_02", 512),
    "mesa_aco": ("metal_office_desk", 512), "bau_ferramenta": ("metal_tool_chest", 512), "carro_ferramenta": ("tool_cart", 512),
    "quadro_forca": ("utility_box_01", 512), "camera": ("security_camera_01", 512), "ar_condicionado": ("exterior_aircon_unit", 512),
    "poste_rua": ("street_lamp_01", 512), "escada_madeira": ("wooden_ladder", 512), "fogueira": ("stone_fire_pit", 512),
    "pedra_a": ("namaqualand_boulder_02", 1024), "pedra_b": ("namaqualand_boulder_03", 1024), "pedra_c": ("boulder_01", 1024),
    "luminaria_parede": ("industrial_wall_lamp", 512),
    "carro_coberto": ("covered_car", 1024),
}


def converter(nome, ph, lado):
    pasta = os.path.join(ORIGEM, ph)
    arquivos = [f for f in os.listdir(pasta) if f.endswith(".gltf")] if os.path.isdir(pasta) else []
    if not arquivos:
        return None

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(pasta, arquivos[0]))
    malhas = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not malhas:
        return None

    # Uma malha só, com a origem no meio da base.
    bpy.ops.object.select_all(action="DESELECT")
    for o in malhas:
        o.select_set(True)
    bpy.context.view_layer.objects.active = malhas[0]
    if len(malhas) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    cantos = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector((min(c.x for c in cantos), min(c.y for c in cantos), min(c.z for c in cantos)))
    hi = Vector((max(c.x for c in cantos), max(c.y for c in cantos), max(c.z for c in cantos)))
    desloca = Vector((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z))
    obj.data.transform(__import__("mathutils").Matrix.Translation(desloca))
    for o in list(bpy.context.scene.objects):
        if o != obj:
            bpy.data.objects.remove(o)
    obj.name = nome

    # Escaneamento vem com malha de sobra: no jogo, FACES bastam.
    if len(obj.data.polygons) > FACES:
        mod = obj.modifiers.new("Reduzir", "DECIMATE")
        mod.ratio = FACES / len(obj.data.polygons)
        bpy.ops.object.modifier_apply(modifier=mod.name)

    for img in bpy.data.images:
        if img.size[0] > lado:
            img.scale(lado, lado)

    os.makedirs(DESTINO, exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(DESTINO, nome + ".glb"), export_format="GLB", export_yup=True,
                              export_apply=True, export_image_format="JPEG", export_image_quality=82,
                              export_animations=False, export_cameras=False, export_lights=False)
    regravar_imagens(os.path.join(DESTINO, nome + ".glb"))
    # No jogo: largura (X), altura (Y), fundo (Z).
    return round(hi.x - lo.x, 2), round(hi.z - lo.z, 2), round(hi.y - lo.y, 2), len(obj.data.polygons)


def regravar_imagens(caminho, qualidade=84):
    """Regrava as imagens de dentro do .glb como JPEG comum (YCbCr,
    linha de base). O JPEG que o Blender grava o Godot não abre."""
    import io
    import json
    import struct
    from PIL import Image

    dados = open(caminho, "rb").read()
    tam_json = struct.unpack("<I", dados[12:16])[0]
    cena = json.loads(dados[20:20 + tam_json])
    inicio = 20 + tam_json + 8
    das_imagens = {img["bufferView"]: img for img in cena.get("images", [])}

    novo = bytearray()
    for i, vista in enumerate(cena["bufferViews"]):
        a = inicio + vista.get("byteOffset", 0)
        trecho = dados[a:a + vista["byteLength"]]
        if i in das_imagens:
            img = Image.open(io.BytesIO(trecho)).convert("RGB")
            saida = io.BytesIO()
            img.save(saida, "JPEG", quality=qualidade)
            trecho = saida.getvalue()
            das_imagens[i]["mimeType"] = "image/jpeg"
        vista["byteOffset"], vista["byteLength"] = len(novo), len(trecho)
        novo += trecho + b"\0" * (-len(trecho) % 4)
    cena["buffers"][0]["byteLength"] = len(novo)

    texto = json.dumps(cena, separators=(",", ":")).encode()
    texto += b" " * (-len(texto) % 4)
    total = 12 + 8 + len(texto) + 8 + len(novo)
    with open(caminho, "wb") as f:
        f.write(b"glTF" + struct.pack("<II", 2, total))
        f.write(struct.pack("<I", len(texto)) + b"JSON" + texto)
        f.write(struct.pack("<I", len(novo)) + b"BIN\0" + bytes(novo))


def main():
    filtro = sys.argv[1] if len(sys.argv) > 1 else ""
    if not os.path.isdir(ORIGEM):
        sys.exit(f"Não achei {ORIGEM}. Baixe os pacotes: python3 tools/baixar_pacotes.py modelos")
    linhas = []
    for nome, (ph, lado) in sorted(TABELA.items()):
        if not nome.startswith(filtro):
            continue
        r = converter(nome, ph, lado)
        linhas.append(f'    "{nome}": ({r[0]}, {r[1]}, {r[2]}),   # {r[3]} faces' if r else f"    # {nome}: faltou {ph} no pacote")
    print("\nOBJETOS = {")
    print("\n".join(linhas))
    print("}")


if __name__ == "__main__":
    main()
