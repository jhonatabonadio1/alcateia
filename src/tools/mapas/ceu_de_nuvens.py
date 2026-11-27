#!/usr/bin/env python3
"""
Troca o céu de fotografia de um mapa pelo céu calculado, com nuvens que andam
(``src/assets/mapas/ceus/ceu_nuvens.gdshader``), **direto na cena** (``src/scenes/maps/<id>.tscn``) — para o mapa
que já foi mexido no Godot e não se gera mais (Jhon, 27/11: "coloque shaders de nuvens também, em todos os mapas
que são de dia"). O resto da cena fica como está. Os mapas que saem do gerador (src/tools/gerar_mapas.py) já
nascem com este céu e não precisam disto.

    python3 src/tools/mapas/ceu_de_nuvens.py <id> tarde      # o clima é uma linha de CEUS (kits/montagem.py)

O que muda: o material do céu (as cores do ar e a camada de nuvens do clima), o recurso Sky (tempo real, 256 px: o
céu anda), a névoa do mapa deixa de entrar no céu (ele tem o ar do horizonte dele) e a luz de preenchimento fica
fora do céu (o sol é a primeira luz direcional que entra nele). Rodar de novo com outro clima só troca os valores.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from mapas.kit import ROOT, cor, f
from mapas.kits.montagem import CEUS


def material(clima):
    c = CEUS[clima]
    o = ['[sub_resource type="ShaderMaterial" id="CeuMat"]', 'shader = ExtResource("s_ceu")', 'shader_parameter/ruido = ExtResource("t_nuvens")', "shader_parameter/energia = 1"]
    o += [f"shader_parameter/{n} = {cor(c[n])}" for n in ("topo", "horizonte", "chao") if n in c]
    o += [f"shader_parameter/{n} = {f(c[n])}" for n in ("curva", "clarao", "disco") if n in c]
    for n, v in c["nuvens"].items():
        o += [f"shader_parameter/{n} = " + (cor(v) if n.startswith("nuvem_") else f"Vector2({f(v[0])}, {f(v[1])})" if n == "vento" else f(v))]
    return "\n".join(o) + '\n\n[sub_resource type="Sky" id="Ceu"]\nsky_material = SubResource("CeuMat")\nprocess_mode = 3\nradiance_size = 3\n'


def trocar(ident, clima):
    cena = os.path.join(ROOT, "scenes", "maps", ident + ".tscn"); s = open(cena, encoding="utf-8").read()
    recursos = ('[ext_resource type="Shader" path="res://src/assets/mapas/ceus/ceu_nuvens.gdshader" id="s_ceu"]\n'
                '[ext_resource type="Texture2D" path="res://src/assets/mapas/ceus/nuvens_ruido.png" id="t_nuvens"]\n')
    if 'id="s_ceu"' not in s:
        s, n = re.subn(r'\[ext_resource type="Texture2D" path="res://src/assets/mapas/ceus/[^"]+\.hdr" id="t_ceu"\]\n', recursos, s)
        if n != 1: raise SystemExit(f"{ident}: não achei o céu de fotografia (t_ceu) na cena")
    s, n = re.subn(r'\[sub_resource type="(PanoramaSkyMaterial|ShaderMaterial)" id="CeuMat"\]\n.*?\[sub_resource type="Sky" id="Ceu"\]\n(?:[^\[\n][^\n]*\n)*', lambda m: material(clima), s, flags=re.S)
    if n != 1: raise SystemExit(f"{ident}: não achei o material do céu (CeuMat) na cena")
    s = re.sub(r"^sky_rotation = .*\n", "", s, flags=re.M)
    s = re.sub(r"^fog_sky_affect = .*$", "fog_sky_affect = 0", s, flags=re.M)
    # a luz de preenchimento fora do céu
    def fora(m):
        bloco = m.group(0)
        return bloco if "sky_mode" in bloco else bloco.rstrip("\n") + "\nsky_mode = 1\n\n"
    s = re.sub(r'\[node name="Preenchimento" type="DirectionalLight3D" parent="\."\]\n(?:[^\[\n][^\n]*\n)*\n?', fora, s)
    open(cena, "w", encoding="utf-8").write(s)
    print(f"{ident}: céu de nuvens ({clima})")


if __name__ == "__main__":
    if len(sys.argv) != 3 or sys.argv[2] not in CEUS:
        raise SystemExit(__doc__)
    trocar(sys.argv[1], sys.argv[2])
