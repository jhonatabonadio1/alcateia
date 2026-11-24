"""
A textura detalhada do AR-15 em primeira pessoa, feita da paleta.

    /Applications/Blender.app/Contents/MacOS/Blender -b -P tools/armas/blender/ar15_detalhe.py -- --saida /tmp/ar15.glb
    (--tamanho 1024 para um teste rápido; sem --saida, grava no jogo)

Com ``--saida`` o jogo não muda: o resultado vai para outro arquivo, para
ver antes (as texturas ficam ao lado dele).

O fuzil do pacote é pintado por paleta: cada peça inteira aponta para um
quadrado de ``assets/armas/ar15/T_AssaultRifle_D.png`` (14 mil vértices,
155 pontos de UV). Detalhe desenhado ali não aparece. Este script:

1. abre ``assets/armas/fp/ar15.glb`` no Blender, e no fuzil e no carregador
   cria um UV de verdade (Smart UV, os dois na mesma folha);
2. assa nele, a partir da cor da paleta, três texturas de 2048 px:
   - cor: desgaste claro nas quinas (o metal aparecendo), sujeira nos
     cantos fundos, manchas grandes e grão fino;
   - aspereza: fosco no geral, mais liso nas quinas gastas;
   - relevo (normal): as quinas arredondadas e um grão fino na superfície;
3. regrava o ``.glb`` trocando SÓ a malha do fuzil e do carregador (o UV
   novo; os vértices que o UV corta ficam duplicados) e o material deles.
   Braços, ossos e animações saem como estavam: o Blender não reexporta
   o arquivo (reexportar arrisca a orientação dos ossos).

Rode de novo depois de cada ``infima.py`` (ele grava o fuzil com o UV da
paleta). Num ``.glb`` já detalhado o script para: a paleta já não serve
de fonte.

O fuzil na mão dos outros e no chão continua com a paleta: visto de longe
o detalhe não aparece, e o material dele só usa a cor (WeaponModel).
"""
import json
import math
import os
import shutil
import struct
import subprocess
import sys
import tempfile

import bpy

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(AQUI)))
GLB = os.path.join(RAIZ, "assets", "armas", "fp", "ar15.glb")
PALETA = os.path.join(RAIZ, "assets", "armas", "ar15", "T_AssaultRifle_D.png")

# Malhas do fuzil no .glb (nome do nó) e o prefixo do material delas.
PECAS = ("fuzil", "carregador")
MATERIAL = "M_AssaultRifle"

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
TAMANHO = int(ARGS[ARGS.index("--tamanho") + 1]) if "--tamanho" in ARGS else 2048
SAIDA = os.path.abspath(ARGS[ARGS.index("--saida") + 1]) if "--saida" in ARGS else GLB
AMOSTRAS = 12


def falhar(texto):
    print(f"DETALHE ERRO: {texto}")
    sys.exit(1)


# ------------------------------------------------------------------ glb

def ler_glb(caminho):
    raw = open(caminho, "rb").read()
    tam = struct.unpack("<I", raw[12:16])[0]
    doc = json.loads(raw[20:20 + tam])
    btam = struct.unpack("<I", raw[20 + tam:24 + tam])[0]
    return doc, raw[28 + tam:28 + tam + btam]


COMPONENTES = {5120: "b", 5121: "B", 5122: "h", 5123: "H", 5125: "I", 5126: "f"}
LARGURA = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def bytes_por_vertice(doc, binario, indice):
    """Os bytes de cada elemento de um acessor, numa lista."""
    a = doc["accessors"][indice]
    bv = doc["bufferViews"][a["bufferView"]]
    elem = struct.calcsize("<" + COMPONENTES[a["componentType"]]) * LARGURA[a["type"]]
    passo = bv.get("byteStride", elem)
    ini = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    return [binario[ini + i * passo: ini + i * passo + elem] for i in range(a["count"])]


def valores(doc, binario, indice):
    a = doc["accessors"][indice]
    fmt = "<" + COMPONENTES[a["componentType"]] * LARGURA[a["type"]]
    return [struct.unpack(fmt, b) for b in bytes_por_vertice(doc, binario, indice)]


# ------------------------------------------------------------------ Blender

def importar():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=GLB, merge_vertices=False)
    objs = {n: bpy.data.objects.get(n) for n in PECAS}
    for n, o in objs.items():
        if o is None or o.type != "MESH":
            falhar(f"não achei a malha '{n}' no {GLB}")
    for o in bpy.data.objects:
        if o.type == "ARMATURE":
            o.data.pose_position = "REST"
    bpy.context.view_layer.update()
    return objs


def conferir_vertices(doc, binario, objs):
    """O Blender leu a malha 1 para 1 (sem juntar vértices)? É o que deixa
    trocar o UV no .glb sem mexer no resto do vértice."""
    for n, o in objs.items():
        prim = malha_do_no(doc, n)["primitives"][0]
        pos = valores(doc, binario, prim["attributes"]["POSITION"])
        if len(pos) != len(o.data.vertices):
            falhar(f"{n}: {len(o.data.vertices)} vértices no Blender, {len(pos)} no .glb")
        # O importador põe a malha no espaço do esqueleto (escala 0,01,
        # espelhada): compara no mundo. glTF tem Y para cima; o Blender, Z.
        mundo = o.matrix_world
        for v, p in zip(o.data.vertices, pos):
            w = mundo @ v.co
            if abs(w.x - p[0]) > 1e-3 or abs(w.y + p[2]) > 1e-3 or abs(w.z - p[1]) > 1e-3:
                falhar(f"{n}: o vértice {v.index} não bate com o do .glb")


def malha_do_no(doc, nome):
    no = next((x for x in doc["nodes"] if x.get("name") == nome and "mesh" in x), None)
    if no is None:
        falhar(f"o .glb não tem o nó '{nome}'")
    return doc["meshes"][no["mesh"]]


def abrir_uv(objs):
    for o in bpy.data.objects:
        o.select_set(False)
    for o in objs.values():
        velho = o.data.uv_layers[0]
        velho.name = "paleta"
        novo = o.data.uv_layers.new(name="detalhe")
        o.data.uv_layers.active = novo
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs["fuzil"]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004,
                             area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")


def tamanho_do_fuzil(o):
    pts = [o.matrix_world @ v.co for v in o.data.vertices]
    return max(max(p[i] for p in pts) - min(p[i] for p in pts) for i in range(3))


def entrada(no, identificador):
    return next(s for s in no.inputs if s.identifier == identificador)


class Nos:
    """Ajuda a montar a árvore de nós sem repetir fio por fio."""

    def __init__(self, mat):
        mat.use_nodes = True
        self.arvore = mat.node_tree
        self.arvore.nodes.clear()

    def no(self, tipo, **props):
        n = self.arvore.nodes.new(tipo)
        for k, v in props.items():
            setattr(n, k, v)
        return n

    def liga(self, saida, entrada_):
        self.arvore.links.new(saida, entrada_)

    def matematica(self, op, a, b=None, clamp=False):
        n = self.no("ShaderNodeMath", operation=op, use_clamp=clamp)
        for i, v in enumerate((a, b)):
            if v is None:
                continue
            if isinstance(v, (int, float)):
                n.inputs[i].default_value = v
            else:
                self.liga(v, n.inputs[i])
        return n.outputs[0]

    def mistura(self, fator, a, b, modo="MIX"):
        n = self.no("ShaderNodeMix", data_type="RGBA", blend_type=modo)
        for ident, v in (("Factor_Float", fator), ("A_Color", a), ("B_Color", b)):
            s = entrada(n, ident)
            if isinstance(v, (int, float)):
                s.default_value = v
            elif isinstance(v, tuple):
                s.default_value = v
            else:
                self.liga(v, s)
        return next(s for s in n.outputs if s.identifier == "Result_Color")

    def ruido(self, escala, detalhe=4.0, aspereza=0.55):
        n = self.no("ShaderNodeTexNoise")
        n.inputs["Scale"].default_value = escala
        n.inputs["Detail"].default_value = detalhe
        n.inputs["Roughness"].default_value = aspereza
        coord = self.no("ShaderNodeTexCoord")
        self.liga(coord.outputs["Object"], n.inputs["Vector"])
        return n.outputs["Fac"]


def material_de_bake(o, paleta, alvo, tamanho, modo):
    """O material que o bake lê: a cor da paleta (pelo UV velho) com o
    desgaste por cima. ``modo``: "cor", "aspereza" ou "normal"."""
    mat = bpy.data.materials.new(f"bake_{o.name}_{modo}")
    o.data.materials.clear()
    o.data.materials.append(mat)
    n = Nos(mat)
    saida = n.no("ShaderNodeOutputMaterial")

    # Quinas: onde a normal arredondada (Bevel) se afasta da normal da face.
    # Raio curto (~1 mm): o gasto de verdade é uma linha fina na aresta.
    def quinas(raio, de, ate):
        bevel = n.no("ShaderNodeBevel", samples=8)
        bevel.inputs["Radius"].default_value = raio
        geo = n.no("ShaderNodeNewGeometry")
        ponto = n.no("ShaderNodeVectorMath", operation="DOT_PRODUCT")
        n.liga(bevel.outputs["Normal"], ponto.inputs[0])
        n.liga(geo.outputs["Normal"], ponto.inputs[1])
        faixa = n.no("ShaderNodeMapRange", clamp=True)
        n.liga(n.matematica("SUBTRACT", 1.0, next(s for s in ponto.outputs if s.name == "Value")), faixa.inputs["Value"])
        faixa.inputs["From Min"].default_value = de
        faixa.inputs["From Max"].default_value = ate
        return bevel, faixa.outputs["Result"]

    _, quina = quinas(tamanho * 0.0018, 0.03, 0.16)

    # Ruídos no espaço do objeto: manchas (~20 cm), mosqueado (~2 cm),
    # lascas (~6 mm) e grão (~2 mm).
    mancha = n.ruido(3.0 / tamanho, detalhe=6.0)
    mosqueado = n.ruido(28.0 / tamanho, detalhe=8.0, aspereza=0.6)
    lasca = n.ruido(95.0 / tamanho, detalhe=10.0, aspereza=0.75)
    grao = n.ruido(260.0 / tamanho, detalhe=2.0, aspereza=0.7)

    def faixa(valor, de, ate):
        m = n.no("ShaderNodeMapRange", clamp=True)
        n.liga(valor, m.inputs["Value"])
        m.inputs["From Min"].default_value = de
        m.inputs["From Max"].default_value = ate
        return m.outputs["Result"]

    # Gasto: a quina, recortada em lascas, mais forte onde a mancha deixa.
    recorte = faixa(lasca, 0.42, 0.58)
    peso = n.matematica("ADD", 0.45, n.matematica("MULTIPLY", faixa(mancha, 0.35, 0.65), 0.55))
    gasto = n.matematica("MULTIPLY", n.matematica("MULTIPLY", quina, recorte), peso, clamp=True)

    if modo == "normal":
        # Quinas arredondadas (raio maior que o do gasto) e um grão leve.
        bevel, _ = quinas(tamanho * 0.003, 0.0, 1.0)
        bump = n.no("ShaderNodeBump")
        bump.inputs["Strength"].default_value = 0.08
        bump.inputs["Distance"].default_value = tamanho * 0.0003
        n.liga(n.matematica("ADD", grao, n.matematica("MULTIPLY", gasto, -0.6)), bump.inputs["Height"])
        n.liga(bevel.outputs["Normal"], bump.inputs["Normal"])
        bsdf = n.no("ShaderNodeBsdfPrincipled")
        n.liga(bump.outputs["Normal"], bsdf.inputs["Normal"])
        n.liga(bsdf.outputs[0], saida.inputs["Surface"])
    else:
        uv = n.no("ShaderNodeUVMap", uv_map="paleta")
        tex = n.no("ShaderNodeTexImage", image=paleta, interpolation="Closest")
        n.liga(uv.outputs["UV"], tex.inputs["Vector"])
        cor = tex.outputs["Color"]

        # Cantos fundos: AO curto, só do próprio objeto.
        ao = n.no("ShaderNodeAmbientOcclusion", only_local=True, samples=8)
        ao.inputs["Distance"].default_value = tamanho * 0.01
        canto = n.matematica("SUBTRACT", 1.0, ao.outputs["AO"])

        if modo == "cor":
            # Gasto: a tinta sai e aparece o metal por baixo, um pouco mais
            # claro que a peça — discreto nas escuras, para não virar giz.
            aco = n.mistura(0.45, cor, (0.34, 0.335, 0.32, 1.0))
            gasta = n.mistura(n.matematica("MULTIPLY", gasto, 0.85), cor, aco)
            # Cantos: sujeira escura, e onde a mancha deixa, poeira clara.
            poeira = n.mistura(faixa(mancha, 0.5, 0.75), (0.05, 0.045, 0.04, 1.0), (0.42, 0.39, 0.34, 1.0))
            suja = n.mistura(n.matematica("MULTIPLY", canto, 0.5, clamp=True), gasta, poeira)
            # Tom irregular: manchas ±7% e grão ±4%.
            tom = n.matematica("ADD", 0.89, n.matematica("ADD", n.matematica("MULTIPLY", mosqueado, 0.14),
                                                          n.matematica("MULTIPLY", grao, 0.08)))
            cinza = n.no("ShaderNodeCombineColor")
            for i in range(3):
                n.liga(tom, cinza.inputs[i])
            valor = n.mistura(1.0, suja, cinza.outputs[0], "MULTIPLY")
        else:
            # Fosco irregular (0,5 a 0,75): é o que quebra o reflexo como
            # pintura de verdade. Liso onde gastou, mais áspero nos cantos.
            r = n.matematica("ADD", 0.5, n.matematica("MULTIPLY", mosqueado, 0.18))
            r = n.matematica("ADD", r, n.matematica("MULTIPLY", grao, 0.07))
            r = n.matematica("SUBTRACT", r, n.matematica("MULTIPLY", gasto, 0.3))
            r = n.matematica("ADD", r, n.matematica("MULTIPLY", canto, 0.15), clamp=True)
            valor = r

        emissao = n.no("ShaderNodeEmission")
        n.liga(valor, emissao.inputs["Color"])
        n.liga(emissao.outputs[0], saida.inputs["Surface"])

    alvo_no = n.no("ShaderNodeTexImage", image=alvo)
    n.arvore.nodes.active = alvo_no
    alvo_no.select = True
    return mat


def assar(objs, paleta, tamanho_px, tamanho_fuzil):
    cena = bpy.context.scene
    cena.render.engine = "CYCLES"
    cena.cycles.device = "CPU"
    cena.cycles.samples = AMOSTRAS
    cena.render.bake.margin = 16
    cena.render.bake.use_clear = True

    imagens = {}
    for modo, tipo, cor in (("cor", "EMIT", "sRGB"), ("aspereza", "EMIT", "Non-Color"), ("normal", "NORMAL", "Non-Color")):
        img = bpy.data.images.new(f"ar15_{modo}", tamanho_px, tamanho_px, alpha=False)
        img.colorspace_settings.name = cor
        for o in objs.values():
            material_de_bake(o, paleta, img, tamanho_fuzil, modo)
        for o in bpy.data.objects:
            o.select_set(o in objs.values())
        bpy.context.view_layer.objects.active = objs["fuzil"]
        print(f"DETALHE assando {modo} ({tamanho_px} px)…", flush=True)
        if tipo == "NORMAL":
            bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", margin=16, use_clear=True)
        else:
            bpy.ops.object.bake(type="EMIT", margin=16, use_clear=True)
        imagens[modo] = img
    return imagens


def gravar(imagens, pasta):
    """PNG pelo Blender; cor e aspereza viram JPEG pelo sips (o JPEG do
    Blender o Godot recusa). O relevo fica PNG: JPEG estraga normal."""
    sips = shutil.which("sips")
    arquivos = {}

    # Aspereza no canal G, metal (zero) no B, oclusão (um) no R: o
    # metallicRoughness do glTF.
    asp = imagens["aspereza"]
    px = list(asp.pixels)
    orm = bpy.data.images.new("ar15_orm", asp.size[0], asp.size[1], alpha=False)
    orm.colorspace_settings.name = "Non-Color"
    novo = [0.0] * len(px)
    for i in range(0, len(px), 4):
        novo[i], novo[i + 1], novo[i + 2], novo[i + 3] = 1.0, px[i], 0.0, 1.0
    orm.pixels = novo
    imagens = {"cor": imagens["cor"], "orm": orm, "normal": imagens["normal"]}

    for nome, img in imagens.items():
        png = os.path.join(pasta, f"{nome}.png")
        img.filepath_raw = png
        img.file_format = "PNG"
        img.save()
        caminho, mime = png, "image/png"
        if nome != "normal" and sips:
            jpg = os.path.join(pasta, f"{nome}.jpg")
            subprocess.run([sips, "-s", "format", "jpeg", "-s", "formatOptions", "90", png, "--out", jpg],
                           check=True, capture_output=True)
            caminho, mime = jpg, "image/jpeg"
        arquivos[nome] = (open(caminho, "rb").read(), mime)
    return arquivos


# ------------------------------------------------------------------ regravar

def nova_primitiva(doc, binario, prim, o):
    """Os triângulos do Blender com o UV novo: um vértice por par (vértice
    do .glb, UV). O resto do vértice (posição, normal, ossos) é copiado."""
    attrs = {k: bytes_por_vertice(doc, binario, v) for k, v in prim["attributes"].items()
             if k not in ("TEXCOORD_0", "TEXCOORD_1", "COLOR_0", "COLOR_1", "TANGENT")}
    uv = o.data.uv_layers["detalhe"].data
    chave = {}
    ordem = []
    indices = []
    for poli in o.data.polygons:
        if len(poli.loop_indices) != 3:
            falhar(f"{o.name}: face com {len(poli.loop_indices)} lados (o .glb só tem triângulos)")
        for li in poli.loop_indices:
            v = o.data.loops[li].vertex_index
            u, w = uv[li].uv
            k = (v, round(u, 6), round(w, 6))
            if k not in chave:
                chave[k] = len(ordem)
                ordem.append((v, u, 1.0 - w))
            indices.append(chave[k])

    saida = {nome: b"".join(lista[v] for v, _, _ in ordem) for nome, lista in attrs.items()}
    saida["TEXCOORD_0"] = b"".join(struct.pack("<2f", u, w) for _, u, w in ordem)
    grande = len(ordem) > 65535
    return saida, indices, grande, len(ordem)


def regravar(doc, binario, objs, imagens):
    blocos = []          # bytes de cada bufferView novo, na ordem
    novos_views = []

    def anexar(dados, alvo=None):
        novos_views.append({"buffer": 0, "byteLength": len(dados), **({"target": alvo} if alvo else {})})
        blocos.append(dados)
        return len(novos_views) - 1

    # 1. Os acessores em uso, copiados para um buffer novo (o que sobra do
    #    UV velho e da paleta sai).
    usados = set()
    for m in doc["meshes"]:
        for p in m["primitives"]:
            usados.update(p["attributes"].values())
            if "indices" in p:
                usados.add(p["indices"])
    for s in doc.get("skins", []):
        if "inverseBindMatrices" in s:
            usados.add(s["inverseBindMatrices"])
    for a in doc.get("animations", []):
        for s in a["samplers"]:
            usados.update((s["input"], s["output"]))

    novas_pecas = {}
    for n, o in objs.items():
        malha = malha_do_no(doc, n)
        prim = malha["primitives"][0]
        novas_pecas[id(prim)] = nova_primitiva(doc, binario, prim, o)
        for k in ("TEXCOORD_0", "TEXCOORD_1", "COLOR_0", "COLOR_1", "TANGENT"):
            usados.discard(prim["attributes"].get(k))
        usados.discard(prim.get("indices"))
        for k, v in prim["attributes"].items():
            if k not in ("TEXCOORD_0", "TEXCOORD_1", "COLOR_0", "COLOR_1", "TANGENT"):
                usados.discard(v)

    remap = {}
    acessores = []
    for i in sorted(u for u in usados if u is not None):
        a = dict(doc["accessors"][i])
        a.pop("byteOffset", None)
        dados = b"".join(bytes_por_vertice(doc, binario, i))
        alvo = doc["bufferViews"][a["bufferView"]].get("target")
        a["bufferView"] = anexar(dados, alvo)
        remap[i] = len(acessores)
        acessores.append(a)

    def novo_acessor(dados, comp, tipo, count, alvo, extra=None):
        acessores.append({"bufferView": anexar(dados, alvo), "componentType": comp, "type": tipo,
                          "count": count, **(extra or {})})
        return len(acessores) - 1

    for m in doc["meshes"]:
        for p in m["primitives"]:
            if id(p) in novas_pecas:
                saida, indices, grande, count = novas_pecas[id(p)]
                velhos = p["attributes"]
                novos = {}
                for nome, dados in saida.items():
                    base = doc["accessors"][velhos[nome]] if nome in velhos else None
                    if nome == "TEXCOORD_0":
                        novos[nome] = novo_acessor(dados, 5126, "VEC2", count, 34962)
                    else:
                        extra = {k: base[k] for k in ("normalized", "min", "max") if k in base}
                        novos[nome] = novo_acessor(dados, base["componentType"], base["type"], count, 34962, extra)
                fmt = "<%d%s" % (len(indices), "I" if grande else "H")
                p["indices"] = novo_acessor(struct.pack(fmt, *indices), 5125 if grande else 5123, "SCALAR",
                                            len(indices), 34963)
                p["attributes"] = novos
            else:
                p["attributes"] = {k: remap[v] for k, v in p["attributes"].items()}
                if "indices" in p:
                    p["indices"] = remap[p["indices"]]
    for s in doc.get("skins", []):
        if "inverseBindMatrices" in s:
            s["inverseBindMatrices"] = remap[s["inverseBindMatrices"]]
    for a in doc.get("animations", []):
        for s in a["samplers"]:
            s["input"], s["output"] = remap[s["input"]], remap[s["output"]]

    # 2. As texturas novas, no lugar da paleta.
    doc["images"], doc["textures"] = [], []
    doc["samplers"] = [{"magFilter": 9729, "minFilter": 9987, "wrapS": 10497, "wrapT": 10497}]
    tex = {}
    for nome, (dados, mime) in imagens.items():
        doc["images"].append({"name": f"ar15_{nome}", "mimeType": mime, "bufferView": anexar(dados)})
        doc["textures"].append({"sampler": 0, "source": len(doc["images"]) - 1})
        tex[nome] = len(doc["textures"]) - 1
    for m in doc["materials"]:
        if m["name"].startswith(MATERIAL):
            pbr = m.setdefault("pbrMetallicRoughness", {})
            pbr["baseColorFactor"] = [1, 1, 1, 1]
            pbr["baseColorTexture"] = {"index": tex["cor"]}
            pbr["metallicRoughnessTexture"] = {"index": tex["orm"]}
            pbr["metallicFactor"], pbr["roughnessFactor"] = 1.0, 1.0
            m["normalTexture"] = {"index": tex["normal"]}
        else:
            m.get("pbrMetallicRoughness", {}).pop("baseColorTexture", None)

    # 3. O buffer: cada bloco alinhado em 4 bytes.
    corpo = bytearray()
    for view, dados in zip(novos_views, blocos):
        while len(corpo) % 4:
            corpo.append(0)
        view["byteOffset"] = len(corpo)
        corpo += dados
    while len(corpo) % 4:
        corpo.append(0)
    doc["bufferViews"] = novos_views
    doc["accessors"] = acessores
    doc["buffers"] = [{"byteLength": len(corpo)}]

    js = json.dumps(doc, separators=(",", ":")).encode()
    while len(js) % 4:
        js += b" "
    total = 12 + 8 + len(js) + 8 + len(corpo)
    return (struct.pack("<4sII", b"glTF", 2, total) + struct.pack("<I4s", len(js), b"JSON") + js
            + struct.pack("<I4s", len(corpo), b"BIN\0") + bytes(corpo))


def main():
    doc, binario = ler_glb(GLB)
    for m in doc["materials"]:
        if m["name"].startswith(MATERIAL) and "normalTexture" in m:
            falhar("o ar15.glb já está detalhado. Rode o infima.py antes (ele grava o fuzil com o UV da paleta).")
    if not os.path.exists(PALETA):
        falhar(f"falta a paleta {PALETA}")

    objs = importar()
    conferir_vertices(doc, binario, objs)
    paleta = bpy.data.images.load(PALETA)
    medida = tamanho_do_fuzil(objs["fuzil"])
    print(f"DETALHE fuzil com {medida:.3f} m; UV novo…", flush=True)
    abrir_uv(objs)
    imagens = assar(objs, paleta, TAMANHO, medida)

    pasta = tempfile.mkdtemp() if SAIDA == GLB else os.path.dirname(SAIDA)
    arquivos = gravar(imagens, pasta)
    novo = regravar(doc, binario, objs, arquivos)
    with open(SAIDA, "wb") as fh:
        fh.write(novo)
    print(f"DETALHE pronto: {SAIDA} ({len(novo) / 1e6:.1f} MB); texturas em {pasta}")


if __name__ == "__main__":
    main()
