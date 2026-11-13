#!/usr/bin/env python3
"""
As texturas dos mapas: fotografia de material de verdade (Poly Haven,
CC0), copiada de assets/pacotes/ para assets/textures/pbr/ com o nome
que os mapas usam.

    python3 tools/mapas/texturas.py

Cada material do jogo são três arquivos:

    <nome>_cor.jpg      a cor
    <nome>_normal.jpg   o relevo (normal, convenção OpenGL)
    <nome>_arm.jpg      oclusão (R), aspereza (G) e metal (B)

`neutra` tira a cor da foto e deixa só o claro-escuro (a sujeira, a
junta, o risco), com a média em `claro`: é o que os mapas pintam com a
cor que quiserem (o azulejo azul e o laranja são o mesmo azulejo; o
reboco de cada casa da Vila é o mesmo reboco). Sem `neutra`, a cor é a
da foto.

Precisa dos pacotes baixados (tools/baixar_pacotes.py). Para trocar uma
textura, mude o id na tabela e rode de novo; para ver as opções,
https://polyhaven.com/textures.
"""
import os
import sys
from PIL import Image, ImageOps

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ORIGEM = os.path.join(RAIZ, "assets", "pacotes", "polyhaven", "texturas")
DESTINO = os.path.join(RAIZ, "assets", "textures", "pbr")

# nome no jogo: (id no Poly Haven, neutra, claro)
TABELA = {
    # pisos
    "piso":            ("worn_tile_floor", 1.0, 0.74),
    "piso_concreto":   ("hangar_concrete_floor", 0.0, None),
    "granilite":       ("terrazzo_tiles", 0.85, 0.72),
    "asfalto":         ("asphalt_03", 0.5, 0.42),
    "asfalto_neve":    ("asphalt_snow", 0.0, None),
    "pedra_calcada":   ("cobblestone_floor_09", 0.35, 0.62),
    "lajota":          ("red_sandstone_pavement", 0.0, None),
    "calcada":         ("concrete_pavement", 0.6, 0.7),
    "areia":           ("dense_sand", 0.0, None),
    "terra":           ("rocky_trail_02", 0.0, None),
    "cascalho":        ("sandy_gravel_02", 0.0, None),
    "brita":           ("rocks_ground_05", 0.6, 0.5),
    "neve":            ("snow_02", 0.0, None),
    "neve_pisada":     ("snow_03", 0.3, 0.86),
    # paredes
    "azulejo":         ("rectangular_facade_tiles", 1.0, 0.8),
    "concreto":        ("concrete_wall_008", 0.8, 0.72),
    "concreto_laje":   ("concrete_slab_wall_02", 0.7, 0.66),
    "concreto_riscado": ("ribbed_concrete_wall", 0.8, 0.66),
    "bloco":           ("concrete_block_wall_02", 1.0, 0.7),
    "reboco":          ("white_stucco_02", 1.0, 0.86),
    "reboco_velho":    ("worn_plaster_wall", 0.75, 0.74),
    "reboco_liso":     ("plastered_wall_02", 1.0, 0.86),
    "tijolo":          ("brick_wall_006", 0.0, None),
    "tijolo_fabrica":  ("factory_brick", 0.0, None),
    "tijolo_pintado":  ("painted_brick", 1.0, 0.8),
    "pedra_muro":      ("medieval_blocks_02", 0.3, 0.66),
    "rocha":           ("rock_face_03", 0.3, 0.6),
    "penhasco":        ("cliff_side", 0.4, 0.6),
    # metal
    "metal_pintado":   ("blue_metal_plate", 1.0, 0.78),
    "metal_ondulado":  ("corrugated_iron_02", 1.0, 0.72),
    "metal_trapezio":  ("box_profile_metal_sheet", 1.0, 0.76),
    "conteiner":       ("container_side", 1.0, 0.76),
    "persiana":        ("painted_metal_shutter", 1.0, 0.74),
    "chapa_xadrez":    ("metal_plate", 1.0, 0.6),
    "ferrugem":        ("rusty_metal_04", 0.0, None),
    "ferrugem_ondulada": ("rusty_corrugated_iron", 0.0, None),
    "grade_piso":      ("metal_grate_rusty", 0.6, 0.5),
    # madeira e telhado
    "madeira":         ("brown_planks_09", 0.0, None),
    "madeira_velha":   ("weathered_brown_planks", 0.0, None),
    "madeira_escura":  ("dark_wooden_planks", 0.0, None),
    "compensado":      ("plywood", 0.0, None),
    "telha":           ("clay_roof_tiles_02", 0.0, None),
    "telha_velha":     ("roof_tiles", 0.0, None),
    "betume":          ("bitumen", 0.0, None),
}

LADO = 1024


def _achar(pasta, pedaco):
    for nome in sorted(os.listdir(pasta)):
        if pedaco in nome and nome.lower().endswith((".jpg", ".png")):
            return os.path.join(pasta, nome)
    return None


def _neutra(img, quanto, claro):
    """Mistura a foto com o próprio claro-escuro, com a média em `claro`."""
    cinza = ImageOps.grayscale(img)
    media = sum(i * n for i, n in enumerate(cinza.histogram())) / (cinza.width * cinza.height) / 255.0
    ganho = (claro / media) if claro and media > 0 else 1.0
    cinza = cinza.point(lambda v: min(255, int(v * ganho)))
    return Image.blend(img, Image.merge("RGB", (cinza,) * 3), quanto)


def main():
    if not os.path.isdir(ORIGEM):
        sys.exit(f"Não achei {ORIGEM}. Baixe os pacotes: python3 tools/baixar_pacotes.py texturas")
    os.makedirs(DESTINO, exist_ok=True)

    faltou = []
    for nome, (ph, quanto, claro) in sorted(TABELA.items()):
        pasta = os.path.join(ORIGEM, ph)
        cor = _achar(pasta, "diff") if os.path.isdir(pasta) else None
        normal = _achar(pasta, "nor_gl") if cor else None
        arm = _achar(pasta, "arm") if cor else None
        if not (cor and normal and arm):
            faltou.append(f"{nome} ({ph})")
            continue

        img = Image.open(cor).convert("RGB").resize((LADO, LADO), Image.LANCZOS)
        if quanto > 0:
            img = _neutra(img, quanto, claro)
        img.save(os.path.join(DESTINO, f"{nome}_cor.jpg"), quality=88)
        Image.open(normal).convert("RGB").resize((LADO, LADO), Image.LANCZOS).save(
            os.path.join(DESTINO, f"{nome}_normal.jpg"), quality=90)
        Image.open(arm).convert("RGB").resize((LADO, LADO), Image.LANCZOS).save(
            os.path.join(DESTINO, f"{nome}_arm.jpg"), quality=86)

    print(f"{len(TABELA) - len(faltou)} texturas em assets/textures/pbr/")
    if faltou:
        print("Faltou no pacote:", ", ".join(faltou))


if __name__ == "__main__":
    main()
