#!/usr/bin/env python3
"""Baixa os pacotes de arte do Poly Haven (CC0) para assets/pacotes/.

Da raiz do projeto, no seu computador (precisa de internet e do curl,
que já vem no macOS):

    python3 tools/baixar_pacotes.py            # tudo (~300 MB)
    python3 tools/baixar_pacotes.py texturas   # só uma parte
    python3 tools/baixar_pacotes.py modelos ceus
    python3 tools/baixar_pacotes.py --lista    # só mostra o que baixaria

Pode rodar de novo quantas vezes quiser: o que já está na pasta é
pulado, e o que falhou é tentado outra vez. No fim ele diz o que
faltou.

O que vem (lista completa, com o porquê de cada um, em docs/PACOTES.md):

- texturas  -> assets/pacotes/polyhaven/texturas/<id>/   (cor, normal, AO+aspereza+metal)
- modelos   -> assets/pacotes/polyhaven/modelos/<id>/    (glTF com as texturas)
- ceus      -> assets/pacotes/polyhaven/ceus/            (HDRI: o céu que ilumina o mapa)

Tudo do Poly Haven é CC0: uso livre, comercial, sem crédito obrigatório.
A pasta assets/pacotes/ fica fora do git e fora do Godot (.gdignore): é
grande, é matéria-prima, e este script a refaz. O que o jogo usar de
fato é copiado dela para as pastas do jogo, essas sim no git.

Os modelos do Sketchfab (braços, armas) não entram aqui: o Sketchfab
exige login para baixar. A lista deles está em docs/PACOTES.md."""
import concurrent.futures as cf
import hashlib
import json
import os
import subprocess
import sys

API = "https://api.polyhaven.com"
# O Poly Haven pede um User-Agent próprio em quem usa a API.
AGENT = "alcateia-baixar-pacotes/1.0 (jogo indie; download unico dos assets CC0)"
ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "assets", "pacotes", "polyhaven")

# Resolução. 1k nas texturas basta: elas repetem a cada 2-4 m no mapa.
TEX_RES, MODEL_RES, SKY_RES = "1k", "1k", "2k"
# Um modelo com arquivo maior que isto é pulado (árvore de 200 MB não
# entra em jogo). --tudo ignora o limite.
MAX_FILE_MB = 80

TEXTURAS = """
concrete_wall_008 concrete_slab_wall_02 concrete_floor_worn_001 hangar_concrete_floor garage_floor
painted_concrete_02 concrete_block_wall_02 ribbed_concrete_wall concrete_pavement
plastered_wall_02 white_stucco_02 worn_plaster_wall beige_wall_001 red_plaster_weathered
brick_wall_006 factory_brick painted_brick
asphalt_03 road_damaged asphalt_snow bitumen
snow_02 snow_03 snow_field_aerial
sandy_gravel_02 dense_sand cracked_red_ground rocky_trail_02
cobblestone_floor_09 cobblestone_pavement red_sandstone_pavement medieval_blocks_02
clay_roof_tiles_02 roof_tiles
corrugated_iron_02 rusty_corrugated_iron box_profile_metal_sheet container_side
blue_metal_plate metal_plate metal_plate_02 rusty_metal_04 green_metal_rust
painted_metal_shutter metal_grate_rusty
dirty_tiles interior_tiles worn_tile_floor terrazzo_tiles rectangular_facade_tiles
brown_planks_09 weathered_brown_planks dark_wooden_planks plywood
rock_face_03 cliff_side rocks_ground_05
""".split()

MODELOS = """
Barrel_01 Barrel_02 barrel_03 concrete_road_barrier concrete_road_barrier_02
old_military_crate wooden_military_crate wooden_crate_01 wooden_crate_02 ammo_box cardboard_box_01
metal_jerrycan_green plastic_jerrycan propane_tank small_lpg_tank old_tyre cement_bag trashbag
industrial_pastic_container metal_trash_can portable_generator old_military_compressor
utility_box_01 utility_box_02 power_box_01 exterior_aircon_unit fire_hydrant water_manhole_cover
security_light security_camera_01 street_lamp_01 street_lamp_02 portable_searchlight
mounted_fluorescent_lights hanging_industrial_lamp industrial_wall_lamp caged_hanging_light
steel_frame_shelves_01 steel_frame_shelves_02 worn_metal_rack metal_tool_chest tool_cart hand_truck
ladder_sectioned_01 wooden_ladder WetFloorSign_01 metal_office_desk
modular_factory_facade modular_fire_escape modular_chainlink_fence modular_industrial_pipes_01
modular_airduct_rectangular_01 modular_street_seating rollershutter_door rollershutter_window_01
large_iron_gate overhead_crane covered_car
namaqualand_boulder_02 namaqualand_boulder_03 namaqualand_boulder_05 namaqualand_cliff_01
namaqualand_cliff_02 sand_rocks_small_01 wooden_barrels_01 wine_barrel_01 dead_tree_trunk stone_fire_pit
boulder_01 rock_07 rock_09 rock_face_01 rock_face_02 tree_stump_01
pine_tree_01 fir_tree_01 fir_sapling_medium pine_sapling_medium island_tree_01
service_pistol stick_grenade machete bolt_action_rifle_7_62
""".split()

CEUS = """
snow_field_2_puresky rogland_sunset belfast_sunset_puresky qwantani_sunset_puresky
drakensberg_solitary_mountain_puresky rogland_clear_night
""".split()


def curl(url, dest=None, max_mb=None):
    """Baixa com o curl (usa os certificados do sistema; o Python do
    macOS às vezes não tem). Devolve o texto, ou grava em dest."""
    cmd = ["curl", "-fsSL", "--retry", "3", "--retry-delay", "2", "--connect-timeout", "20", "-A", AGENT]
    if max_mb: cmd += ["--max-filesize", str(int(max_mb * 1024 * 1024))]
    if dest:
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        part = dest + ".parte"
        r = subprocess.run(cmd + ["-o", part, url], capture_output=True, text=True)
        if r.returncode != 0:
            if os.path.exists(part): os.remove(part)
            raise RuntimeError("grande demais" if r.returncode == 63 else (r.stderr.strip() or f"curl saiu com {r.returncode}"))
        os.replace(part, dest)
        return dest
    r = subprocess.run(cmd + [url], capture_output=True, text=True)
    if r.returncode != 0:
        raise RuntimeError(r.stderr.strip() or f"curl saiu com {r.returncode}")
    return r.stdout


def entry(node):
    """A API descreve um arquivo como {"url", "size", "md5"}; aceita
    também só a URL."""
    return {"url": node} if isinstance(node, str) else node


def md5_ok(path, md5):
    if not md5: return True
    h = hashlib.md5()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest() == md5


def fetch(node, dest, max_mb=None):
    """Baixa um arquivo, a não ser que já esteja lá e inteiro."""
    node = entry(node)
    if os.path.exists(dest) and os.path.getsize(dest) > 0 and md5_ok(dest, node.get("md5")):
        return 0
    curl(node["url"], dest, max_mb)
    if not md5_ok(dest, node.get("md5")):
        os.remove(dest)
        raise RuntimeError("arquivo veio corrompido (md5 não bate)")
    return os.path.getsize(dest)


def files_of(asset):
    return json.loads(curl(f"{API}/files/{asset}"))


def pick(tree, res):
    """A resolução pedida, ou a mais próxima que existir."""
    if res in tree: return tree[res]
    order = ["1k", "2k", "4k", "8k", "16k"]
    have = [r for r in order if r in tree]
    if not have: raise RuntimeError("sem nenhuma resolução")
    return tree[have[0]]


def get_texture(asset, limit):
    info = files_of(asset)
    out = os.path.join(ROOT, "texturas", asset)
    # cor, normal (OpenGL, a do Godot) e o trio AO/aspereza/metal num
    # arquivo só. Sem o trio, aspereza e AO separados.
    wanted = ["Diffuse", "nor_gl"] + (["arm"] if "arm" in info else ["Rough", "AO"])
    total = 0
    for key in wanted:
        if key not in info:
            if key in ("Diffuse", "nor_gl"): raise RuntimeError(f"sem o mapa {key}")
            continue
        node = entry(pick(info[key], TEX_RES)["jpg"])
        total += fetch(node, os.path.join(out, os.path.basename(node["url"])), limit)
    return total


def get_model(asset, limit):
    info = files_of(asset)
    if "gltf" not in info: raise RuntimeError("sem versão glTF")
    gltf = entry(pick(info["gltf"], MODEL_RES)["gltf"])
    out = os.path.join(ROOT, "modelos", asset)
    try:
        total = fetch(gltf, os.path.join(out, os.path.basename(gltf["url"])), limit)
        for rel, node in (gltf.get("include") or {}).items():
            total += fetch(node, os.path.join(out, *rel.split("/")), limit)
    except RuntimeError as e:
        if "grande demais" in str(e):
            # Meio modelo não serve: tira o que veio.
            for base, _, names in os.walk(out, topdown=False):
                for n in names: os.remove(os.path.join(base, n))
                os.rmdir(base)
        raise
    return total


def get_sky(asset, limit):
    info = files_of(asset)
    node = entry(pick(info["hdri"], SKY_RES)["hdr"])
    return fetch(node, os.path.join(ROOT, "ceus", os.path.basename(node["url"])), None)


GROUPS = {"texturas": (TEXTURAS, get_texture), "modelos": (MODELOS, get_model), "ceus": (CEUS, get_sky)}

LICENCA = """Tudo nesta pasta veio do Poly Haven (https://polyhaven.com) e é CC0
(https://creativecommons.org/publicdomain/zero/1.0/): uso livre, inclusive
comercial, sem crédito obrigatório. Baixado por tools/baixar_pacotes.py.
A página de cada item é https://polyhaven.com/a/<nome da pasta>.
"""


def main(argv):
    global API
    args = [a for a in argv if not a.startswith("--")]
    flags = [a for a in argv if a.startswith("--")]
    for f in flags:
        if f.startswith("--api="): API = f.split("=", 1)[1].rstrip("/")
    unknown = [a for a in args if a not in GROUPS]
    if unknown or "--ajuda" in flags or "--help" in flags:
        print(__doc__)
        if unknown: print("Não conheço:", ", ".join(unknown))
        return 2
    chosen = args or list(GROUPS)
    limit = None if "--tudo" in flags else MAX_FILE_MB

    jobs = [(g, a) for g in chosen for a in GROUPS[g][0]]
    if "--lista" in flags:
        for g in chosen:
            print(f"{g} ({len(GROUPS[g][0])}):")
            for a in GROUPS[g][0]: print(f"  https://polyhaven.com/a/{a}")
        return 0

    os.makedirs(ROOT, exist_ok=True)
    with open(os.path.join(ROOT, "LICENCA.txt"), "w", encoding="utf-8") as f:
        f.write(LICENCA)
    # Matéria-prima: o Godot não importa esta pasta.
    open(os.path.join(os.path.dirname(ROOT), ".gdignore"), "a").close()
    os.makedirs(os.path.join(os.path.dirname(ROOT), "sketchfab"), exist_ok=True)

    print(f"Baixando {len(jobs)} itens do Poly Haven para {ROOT}")
    done, failed, skipped, total = 0, [], [], 0

    def work(job):
        group, asset = job
        try:
            return job, GROUPS[group][1](asset, limit), None
        except Exception as e:  # um item ruim não pode parar os outros
            return job, 0, str(e).splitlines()[-1][:160]

    with cf.ThreadPoolExecutor(6) as pool:
        for (group, asset), size, error in pool.map(work, jobs):
            done += 1
            if error and "grande demais" in error:
                skipped.append((group, asset))
                state = f"pulado: passa de {MAX_FILE_MB} MB (use --tudo para trazer)"
            elif error:
                failed.append((group, asset, error))
                state = "FALHOU: " + error
            else:
                total += size
                state = "já estava" if size == 0 else f"{size / 1e6:.1f} MB"
            print(f"[{done}/{len(jobs)}] {group}/{asset}: {state}", flush=True)

    print(f"\nPronto: {total / 1e6:.0f} MB novos, {len(jobs) - len(failed) - len(skipped)} de {len(jobs)} itens na pasta.")
    if skipped:
        print("Pulados por tamanho:", ", ".join(a for _, a in skipped))
    if failed:
        print(f"{len(failed)} falharam — rode de novo; se repetir, me mande esta lista:")
        for group, asset, error in failed: print(f"  {group}/{asset}: {error}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
