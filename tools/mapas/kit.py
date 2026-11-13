"""
Peças para gerar um mapa do Alcateia como cena do Godot (.tscn): caixas,
rampas, cilindros, luzes, placas, pontos de nascimento, rotas de ronda,
o céu e o sol do mapa, e a miniatura vista de cima.

Cada mapa é um arquivo em tools/mapas/ que monta um `Mapa` e chama
`salvar()`. As texturas vêm de tools/mapas/texturas.py.

Convenções que o jogo espera (não mude sem mudar o jogo):
- X é o eixo dos times: o azul nasce em −X e olha para +X.
- Y é a altura; o chão fica em 0.
- Alturas presas à física: o humano sobe 0,8 m (caixote) e não 2,2 m
  (contêiner); o lobo alcança 2,2 m a partir do caixote; telhado a 5 m
  só por rampa ou, o lobo, ricocheteando num beco de 3 m.
- Bot não sobe degrau maior que 0,5 m: todo desnível que ele precisa
  vencer tem rampa.
"""
import hashlib
import math
import os

from . import formas

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
AZUL, LARANJA = 1, 2


def f(v):
    s = f"{v:.4f}".rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


def vec(v):
    return "Vector3(" + ", ".join(f(x) for x in v) + ")"


def cor(c, a=1.0):
    return "Color(" + ", ".join(f(x) for x in (*c, a)) + ")"


def xform(pos, rows=None):
    rows = rows or ((1, 0, 0), (0, 1, 0), (0, 0, 1))
    return "Transform3D(" + ", ".join(f(x) for x in (*[x for r in rows for x in r], *pos)) + ")"


def giro_y(graus):
    a = math.radians(graus)
    c, s = math.cos(a), math.sin(a)
    return ((c, 0, s), (0, 1, 0), (-s, 0, c))


def olhar(yaw, queda):
    """Base de uma luz direcional: gira `yaw` em Y e aponta `queda` graus
    para baixo."""
    y, p = math.radians(yaw), math.radians(-queda)
    cy, sy, cp, sp = math.cos(y), math.sin(y), math.cos(p), math.sin(p)
    return ((cy, sy * sp, sy * cp), (0, cp, -sp), (-sy, cy * sp, cy * cp))


# O que o jogo ouve ao pisar ou atirar numa peça (scripts/visual/Surfaces.cs):
# sai da textura do material. Sem entrada aqui é concreto, o som padrão.
SUPERFICIES = {"metal_pintado": "metal", "metal_ondulado": "metal", "metal_trapezio": "metal", "conteiner": "metal",
               "persiana": "metal", "chapa_xadrez": "metal", "grade_piso": "metal", "ferrugem": "metal",
               "ferrugem_ondulada": "metal", "neve": "neve", "neve_pisada": "neve", "asfalto_neve": "neve",
               "madeira": "madeira", "madeira_velha": "madeira", "madeira_escura": "madeira", "compensado": "madeira",
               "areia": "terra", "terra": "terra", "cascalho": "terra", "brita": "terra"}


def superficie(material):
    if material.get("textura") in SUPERFICIES:
        return SUPERFICIES[material["textura"]]
    return "metal" if material.get("metal", 0) >= 0.5 else ""


class Mapa:
    def __init__(self, ident, nome, largura, fundo):
        self.id, self.nome = ident, nome
        self.largura, self.fundo = largura, fundo      # X e Z, em metros
        self.materiais = {}
        self.pecas = []      # (grupo, nome, centro, tamanho, material, rotação, colide, forma)
        self.formas = []     # (grupo, nome, Malha, posição, material, rotação, colisão)
        self.letreiros, self.luzes = [], []   # letreiro: (grupo, nome, imagem, pos, largura, base, aceso)
        self.modelos = []    # (grupo, nome, arquivo, posição, yaw, escala)
        self.cru_recursos, self.cru_nos = [], []     # linhas de .tscn prontas (partículas)
        self.spawns = {AZUL: [], LARANJA: []}
        self.rotas = []
        self.ambiente = {}
        self.sol = None

    # ------------------------------------------------------------ materiais

    def material(self, nome, textura=None, cor=(1, 1, 1), ladrilho=2.0, aspereza=1.0,
                 metal=0.0, relevo=1.0, brilho=None, energia=1.0, dupla=False):
        """textura: nome em assets/textures/pbr (ou None, cor lisa).
        ladrilho: metros que a textura cobre antes de repetir.
        dupla: desenha os dois lados (lona, folha, chapa fina)."""
        self.materiais[nome] = dict(textura=textura, cor=cor, ladrilho=ladrilho, aspereza=aspereza,
                                    metal=metal, relevo=relevo, brilho=brilho, energia=energia, dupla=dupla)

    # ---------------------------------------------------------------- peças

    def caixa(self, grupo, nome, centro, tamanho, material, colide=True, rot=None, oculta=False):
        """`oculta`: só a colisão, sem desenho — para quando o que se vê é
        uma forma mais rica por cima (degraus sobre a rampa, vagão)."""
        self.pecas.append((grupo, nome, centro, tamanho, material, rot, colide, "caixa_oculta" if oculta else "caixa"))

    def parede(self, grupo, nome, eixo, fixo, de, ate, altura, material, vaos=(), espessura=0.5, base=0.0):
        """Parede com portas e janelas de verdade (passa-se e atira-se por
        elas). Corre ao longo de `eixo` ("x" ou "z"), de `de` a `ate`, na
        coordenada `fixo` do outro eixo. `vaos`: (meio, largura, de_y,
        ate_y) de cada abertura — porta é (meio, 1.6, 0, 2.2), janela é
        (meio, 1.4, 1.0, 2.2)."""
        a, b = min(de, ate), max(de, ate)

        def peca(sufixo, c0, c1, y0, y1):
            if c1 - c0 < 0.01 or y1 - y0 < 0.01:
                return
            meio, comp = (c0 + c1) / 2, c1 - c0
            centro = (meio, (y0 + y1) / 2, fixo) if eixo == "x" else (fixo, (y0 + y1) / 2, meio)
            tam = (comp, y1 - y0, espessura) if eixo == "x" else (espessura, y1 - y0, comp)
            self.caixa(grupo, f"{nome}{sufixo}", centro, tam, material)

        cursor = a
        for i, (meio, largura, y0, y1) in enumerate(sorted(vaos)):
            v0, v1 = meio - largura / 2, meio + largura / 2
            peca(f"Pano{i}", cursor, v0, base, base + altura)
            peca(f"Peitoril{i}", v0, v1, base, base + y0)
            peca(f"Verga{i}", v0, v1, base + y1, base + altura)
            cursor = v1
        peca("PanoFim", cursor, b, base, base + altura)

    def forma(self, grupo, nome, malha, pos, material, yaw=0.0, colisao=None):
        """Uma forma de tools/mapas/formas.py. `colisao`: None (só
        enfeite), "convexa" (o casco da forma), "concava" (a própria
        malha: terreno) — ou use uma `caixa(..., oculta=True)` à parte."""
        self.formas.append((grupo, nome, malha, pos, material, giro_y(yaw) if yaw else None, colisao))

    def cilindro(self, grupo, nome, centro, raio, altura, material, colide=True, oculta=False):
        self.pecas.append((grupo, nome, centro, (raio, altura, raio), material, None, colide,
                           "cilindro_oculta" if oculta else "cilindro"))

    def rampa(self, grupo, nome, x_baixo, x_alto, h, zc, largura, material, t=0.4, base=0.0, degraus=0):
        """Rampa ao longo de X: de `base` em x_baixo até base + h em x_alto.
        Com `degraus`, o que se vê é uma escada; a colisão continua sendo
        a rampa lisa (o bot e o jogador sobem sem tropeçar)."""
        dx = x_alto - x_baixo
        theta = math.copysign(math.atan2(h, abs(dx)), dx)
        c, s = math.cos(theta), math.sin(theta)
        normal = (-s, c)
        mx, my = (x_baixo + x_alto) / 2.0, base + h / 2.0
        centro = (mx - normal[0] * t / 2, my - normal[1] * t / 2, zc)
        self.caixa(grupo, nome, centro, (math.hypot(abs(dx), h) + 0.8, t, largura), material, True,
                   ((c, -s, 0.0), (s, c, 0.0), (0.0, 0.0, 1.0)), oculta=degraus > 0)
        if degraus:
            # Meio degrau abaixo: a rampa passa pelo meio de cada espelho.
            self.forma(grupo, nome + "Degraus", formas.escada(largura, h, abs(dx), degraus),
                       (x_baixo, base - h / degraus / 2, zc), material, yaw=0 if dx > 0 else 180)

    def rampa_z(self, grupo, nome, z_baixo, z_alto, h, xc, largura, material, t=0.4, base=0.0, degraus=0):
        """Rampa ao longo de Z."""
        dz = z_alto - z_baixo
        theta = math.copysign(math.atan2(h, abs(dz)), dz)
        c, s = math.cos(theta), math.sin(theta)
        mz, my = (z_baixo + z_alto) / 2.0, base + h / 2.0
        centro = (xc, my - c * t / 2, mz + s * t / 2)
        self.caixa(grupo, nome, centro, (largura, t, math.hypot(abs(dz), h) + 0.8), material, True,
                   ((1.0, 0.0, 0.0), (0.0, c, s), (0.0, -s, c)), oculta=degraus > 0)
        if degraus:
            self.forma(grupo, nome + "Degraus", formas.escada(largura, h, abs(dz), degraus),
                       (xc, base - h / degraus / 2, z_baixo), material, yaw=-90 if dz > 0 else 90)

    def parapeito(self, grupo, nome, centro, comprimento, ao_longo="x", altura=1.0, painel="aco", corrimao="grade"):
        """Parapeito: painel baixo que cobre e um corrimão amarelo em cima.
        `centro` é o meio do painel, no nível do piso + altura/2."""
        e = 0.12
        tam = (comprimento, altura - 0.1, e) if ao_longo == "x" else (e, altura - 0.1, comprimento)
        topo = (comprimento, 0.1, e * 1.6) if ao_longo == "x" else (e * 1.6, 0.1, comprimento)
        self.caixa(grupo, nome, (centro[0], centro[1] - 0.05, centro[2]), tam, painel)
        self.caixa(grupo, nome + "Corrimao", (centro[0], centro[1] + altura / 2 - 0.05, centro[2]), topo, corrimao, colide=False)

    def letreiro(self, grupo, nome, imagem, pos, largura, yaw=0.0, aceso=0.0, chao=False):
        """Um letreiro pintado: a imagem assets/mapas/letreiros/<imagem>.png
        (desenhada por tools/mapas/letreiros.py, com letra de verdade)
        num plano de `largura` metros; a altura sai da proporção da
        imagem. `pos` é o centro, encostado na parede; `yaw` é para onde
        a frente olha (0 = +Z). `aceso`: quanto brilha (painel luminoso).
        `chao`: pintado no piso; com yaw = 0 lê quem olha para −Z."""
        base = giro_y(yaw)
        if chao:
            # Deita o plano: a frente (+Z) vira para cima, o alto da letra para −Z.
            (a, b, c), (d, e, f_), (g, h, i) = base
            base = ((a, -c, b), (d, -f_, e), (g, -i, h))
        self.letreiros.append((grupo, nome, imagem, pos, largura, base, aceso))

    def modelo(self, grupo, nome, arquivo, pos, yaw=0.0, escala=1.0):
        """Um modelo pronto (assets/mapas/pecas/<arquivo>.glb): só o
        desenho. Se precisa barrar o caminho, ponha uma
        `caixa(..., oculta=True)` junto — veja `pecas.objeto`."""
        self.modelos.append((grupo, nome, arquivo, pos, yaw, escala))

    def nevasca(self, altura=24.0, quantos=6000):
        """Neve caindo sobre o mapa todo (partículas; só enfeite)."""
        self.cru_recursos += [
            '[sub_resource type="ParticleProcessMaterial" id="NevePM"]', "emission_shape = 3",
            f"emission_box_extents = {vec((self.largura / 2 + 4, 1, self.fundo / 2 + 4))}",
            "direction = Vector3(0.25, -1, 0.1)", "spread = 14.0", "initial_velocity_min = 1.2",
            "initial_velocity_max = 2.4", "gravity = Vector3(0, -0.2, 0)", "scale_min = 0.6", "scale_max = 1.5", "",
            '[sub_resource type="StandardMaterial3D" id="NeveMat"]', "transparency = 1", "shading_mode = 0",
            "albedo_color = Color(1, 1, 1, 0.75)", "billboard_mode = 3", "billboard_keep_scale = true", "",
            '[sub_resource type="QuadMesh" id="NeveQuad"]', 'material = SubResource("NeveMat")',
            "size = Vector2(0.035, 0.035)", ""]
        self.cru_nos += [
            '[node name="Neve" type="GPUParticles3D" parent="."]', f"transform = {xform((0, altura, 0))}",
            f"amount = {quantos}", "lifetime = 13.0", "preprocess = 13.0",
            f"visibility_aabb = AABB({f(-self.largura / 2 - 8)}, {f(-altura - 4)}, {f(-self.fundo / 2 - 8)}, "
            f"{f(self.largura + 16)}, {f(altura + 8)}, {f(self.fundo + 16)})",
            'process_material = SubResource("NevePM")', 'draw_pass_1 = SubResource("NeveQuad")', ""]

    def luz(self, grupo, nome, pos, cor_, energia, alcance, sombra=False):
        self.luzes.append((grupo, nome, pos, cor_, energia, alcance, sombra))

    def spawn(self, time, x, z, y=1.2):
        self.spawns[time].append((x, y, z))

    def rota(self, nome, pontos):
        self.rotas.append((nome, pontos))

    # ------------------------------------------------------------- escrita

    def tscn(self):
        formas, malhas = {}, {}
        for (_, _, _, tam, _, _, colide, forma) in self.pecas:
            chave = (forma.replace("_oculta", ""), *[round(x, 4) for x in tam])
            if not forma.endswith("_oculta"):
                malhas.setdefault(chave, f"B{len(malhas) + 1}")
            if colide:
                formas.setdefault(chave, f"S{len(formas) + 1}")

        # As formas livres viram .obj em assets/mapas/<id>/ (uma por
        # conteúdo: vinte barris iguais são um arquivo só).
        self._objs, obj_id = {}, {}
        for (_, _, malha, _, _, _, _) in self.formas:
            texto = malha.obj()
            h = hashlib.md5(texto.encode()).hexdigest()[:10]
            if h not in self._objs:
                self._objs[h] = texto
                obj_id[h] = f"o_{h}"
            malha._hash = h

        texturas = sorted({m["textura"] for m in self.materiais.values() if m["textura"]})
        ext, tex_id = [], {}
        for t in texturas:
            for sufixo in ("cor", "normal", "arm"):
                rid = f"t_{t}_{sufixo}"
                tex_id[(t, sufixo)] = rid
                ext.append(("Texture2D", f"res://assets/textures/pbr/{t}_{sufixo}.jpg", rid))
        hdri = (self.ambiente.get("ceu") or {}).get("hdri")
        if hdri:
            ext.append(("Texture2D", f"res://assets/mapas/ceus/{hdri}.hdr", "t_ceu"))
        for imagem in sorted({l[2] for l in self.letreiros}):
            ext.append(("Texture2D", f"res://assets/mapas/letreiros/{imagem}.png", f"l_{imagem}"))
        for arquivo in sorted({x[2] for x in self.modelos}):
            ext.append(("PackedScene", f"res://assets/mapas/pecas/{arquivo}.glb", f"p_{arquivo}"))
        ext += [("Script", "res://scripts/core/SpawnPoints.cs", "s_spawns"),
                ("Script", "res://scripts/core/PatrolRoutes.cs", "s_routes"),
                ("Script", "res://scripts/core/NavBaker.cs", "s_nav")]

        ext += [("ArrayMesh", f"res://assets/mapas/{self.id}/{h}.obj", rid) for h, rid in obj_id.items()]

        o = ["[gd_scene format=3]", ""]
        o += [f'[ext_resource type="{k}" path="{p}" id="{i}"]' for k, p, i in ext] + [""]

        for nome, m in self.materiais.items():
            o.append(f'[sub_resource type="StandardMaterial3D" id="M_{nome}"]')
            o.append(f"albedo_color = {cor(m['cor'])}")
            if m["textura"]:
                t = m["textura"]
                # O arquivo _arm traz oclusão (R), aspereza (G) e metal (B).
                o += [f'albedo_texture = ExtResource("{tex_id[(t, "cor")]}")',
                      f'roughness_texture = ExtResource("{tex_id[(t, "arm")]}")',
                      "roughness_texture_channel = 1",
                      "ao_enabled = true", "ao_light_affect = 0.25",
                      f'ao_texture = ExtResource("{tex_id[(t, "arm")]}")',
                      "normal_enabled = true",
                      f"normal_scale = {f(m['relevo'])}",
                      f'normal_texture = ExtResource("{tex_id[(t, "normal")]}")',
                      f"uv1_scale = {vec((1.0 / m['ladrilho'],) * 3)}",
                      "uv1_triplanar = true", "uv1_world_triplanar = true"]
            if m.get("dupla"):
                o.append("cull_mode = 2")
            o.append(f"roughness = {f(m['aspereza'])}")
            if m["metal"]:
                o.append(f"metallic = {f(m['metal'])}")
            if m["brilho"]:
                o += ["emission_enabled = true", f"emission = {cor(m['brilho'])}",
                      f"emission_energy_multiplier = {f(m['energia'])}"]
            o.append("")

        for chave, rid in formas.items():
            if chave[0] == "caixa":
                o += [f'[sub_resource type="BoxShape3D" id="{rid}"]', f"size = {vec(chave[1:])}", ""]
            else:
                o += [f'[sub_resource type="CylinderShape3D" id="{rid}"]', f"height = {f(chave[2])}",
                      f"radius = {f(chave[1])}", ""]
        for chave, rid in malhas.items():
            if chave[0] == "caixa":
                o += [f'[sub_resource type="BoxMesh" id="{rid}"]', f"size = {vec(chave[1:])}", ""]
            else:
                o += [f'[sub_resource type="CylinderMesh" id="{rid}"]', f"top_radius = {f(chave[1])}",
                      f"bottom_radius = {f(chave[1])}", f"height = {f(chave[2])}", "radial_segments = 24", ""]

        cascos = {}
        for i, (_, _, malha, _, _, _, colisao) in enumerate(self.formas):
            if not colisao or (malha._hash, colisao) in cascos:
                continue
            rid = f"C{len(cascos) + 1}"
            cascos[(malha._hash, colisao)] = rid
            if colisao == "convexa":
                pts = ", ".join(f(x) for p in malha.pontos() for x in p)
                o += [f'[sub_resource type="ConvexPolygonShape3D" id="{rid}"]', f"points = PackedVector3Array({pts})", ""]
            else:
                pts = ", ".join(f(x) for t in malha.tris for p, _ in t for x in p)
                o += [f'[sub_resource type="ConcavePolygonShape3D" id="{rid}"]', "backface_collision = true",
                      f"data = PackedVector3Array({pts})", ""]

        # Letreiros: um material por imagem (e por brilho), um plano por tamanho.
        from PIL import Image
        pinturas, quadros = {}, {}
        for (_, _, imagem, _, largura, _, aceso) in self.letreiros:
            if (imagem, aceso) not in pinturas:
                rid = f"L{len(pinturas) + 1}"
                pinturas[(imagem, aceso)] = rid
                o += [f'[sub_resource type="StandardMaterial3D" id="{rid}"]', "transparency = 2",
                      "alpha_scissor_threshold = 0.5", f'albedo_texture = ExtResource("l_{imagem}")',
                      "roughness = 0.85", "texture_filter = 5"]
                if aceso:
                    o += ["emission_enabled = true", f'emission_texture = ExtResource("l_{imagem}")',
                          f"emission_energy_multiplier = {f(aceso)}"]
                o.append("")
            chave = (imagem, round(largura, 3))
            if chave not in quadros:
                with Image.open(os.path.join(ROOT, "assets", "mapas", "letreiros", imagem + ".png")) as img:
                    altura = largura * img.height / img.width
                rid = f"Q{len(quadros) + 1}"
                quadros[chave] = rid
                o += [f'[sub_resource type="QuadMesh" id="{rid}"]', f"size = Vector2({f(largura)}, {f(altura)})", ""]

        o += self.cru_recursos
        o += self._ambiente()
        o += [f'[node name="{self.nome}" type="Node3D"]', ""]
        o += ['[node name="Ambiente" type="WorldEnvironment" parent="."]', 'environment = SubResource("Env")', ""]
        if self.sol:
            s = self.sol
            o += ['[node name="Sol" type="DirectionalLight3D" parent="."]',
                  f"transform = {xform((0, 40, 0), olhar(s['yaw'], s['queda']))}",
                  f"light_color = {cor(s['cor'])}", f"light_energy = {f(s['energia'])}",
                  "shadow_enabled = true", "shadow_blur = 1.5",
                  f"directional_shadow_max_distance = {f(s.get('alcance', 120))}", ""]

        # Reflexos: sem isto, metal não tem o que refletir e fica preto.
        o += ['[node name="Reflexo" type="ReflectionProbe" parent="."]',
              f"transform = {xform((0, 4, 0))}", f"size = {vec((self.largura + 2, 14, self.fundo + 2))}",
              "box_projection = true", f"interior = {'true' if not self.ambiente.get('ceu') else 'false'}",
              "ambient_mode = 1" if not self.ambiente.get('ceu') else "ambient_mode = 0", ""]

        grupos = []
        for item in self.pecas + self.formas + self.letreiros + self.modelos + self.luzes:
            if item[0] not in grupos:
                grupos.append(item[0])
        for g in grupos:
            o += [f'[node name="{g}" type="Node3D" parent="."]', ""]

        for (g, nome, malha, pos, mat, rows, colisao) in self.formas:
            desenho = [f'mesh = ExtResource("{obj_id[malha._hash]}")', f'material_override = SubResource("M_{mat}")', ""]
            if colisao:
                piso = superficie(self.materiais.get(mat, {}))
                o += [f'[node name="{nome}" type="StaticBody3D" parent="{g}"]', f"transform = {xform(pos, rows)}"]
                if piso:
                    o.append(f'metadata/piso = "{piso}"')
                o += ["", f'[node name="Colisao" type="CollisionShape3D" parent="{g}/{nome}"]',
                      f'shape = SubResource("{cascos[(malha._hash, colisao)]}")', "",
                      f'[node name="Malha" type="MeshInstance3D" parent="{g}/{nome}"]'] + desenho
            else:
                o += [f'[node name="{nome}" type="MeshInstance3D" parent="{g}"]', f"transform = {xform(pos, rows)}"] + desenho

        for (g, nome, c, tam, mat, rows, colide, forma) in self.pecas:
            oculta = forma.endswith("_oculta")
            forma = forma.replace("_oculta", "")
            chave = (forma, *[round(x, 4) for x in tam])
            if colide and oculta:
                piso = superficie(self.materiais.get(mat, {}))
                o += [f'[node name="{nome}" type="StaticBody3D" parent="{g}"]', f"transform = {xform(c, rows)}"]
                if piso:
                    o.append(f'metadata/piso = "{piso}"')
                o += ["", f'[node name="Colisao" type="CollisionShape3D" parent="{g}/{nome}"]',
                      f'shape = SubResource("{formas[chave]}")', ""]
            elif colide:
                piso = superficie(self.materiais.get(mat, {}))
                o += [f'[node name="{nome}" type="StaticBody3D" parent="{g}"]', f"transform = {xform(c, rows)}"]
                if piso:
                    o.append(f'metadata/piso = "{piso}"')
                o += ["",
                      f'[node name="Colisao" type="CollisionShape3D" parent="{g}/{nome}"]',
                      f'shape = SubResource("{formas[chave]}")', "",
                      f'[node name="Malha" type="MeshInstance3D" parent="{g}/{nome}"]',
                      f'mesh = SubResource("{malhas[chave]}")', f'material_override = SubResource("M_{mat}")', ""]
            else:
                o += [f'[node name="{nome}" type="MeshInstance3D" parent="{g}"]', f"transform = {xform(c, rows)}",
                      f'mesh = SubResource("{malhas[chave]}")', f'material_override = SubResource("M_{mat}")', ""]

        for (g, nome, imagem, pos, largura, base, aceso) in self.letreiros:
            o += [f'[node name="{nome}" type="MeshInstance3D" parent="{g}"]', f"transform = {xform(pos, base)}",
                  f'mesh = SubResource("{quadros[(imagem, round(largura, 3))]}")',
                  f'material_override = SubResource("{pinturas[(imagem, aceso)]}")', "cast_shadow = 0", ""]

        for (g, nome, arquivo, pos, yaw, escala) in self.modelos:
            base = tuple(tuple(v * escala for v in linha) for linha in giro_y(yaw))
            o += [f'[node name="{nome}" parent="{g}" instance=ExtResource("p_{arquivo}")]',
                  f"transform = {xform(pos, base)}", ""]

        for (g, nome, pos, c, energia, alcance, sombra) in self.luzes:
            o += [f'[node name="{nome}" type="OmniLight3D" parent="{g}"]', f"transform = {xform(pos)}",
                  f"light_color = {cor(c)}", f"light_energy = {f(energia)}", f"omni_range = {f(alcance)}"]
            if sombra:
                o.append("shadow_enabled = true")
            o.append("")

        o += self.cru_nos

        for time, tag in ((AZUL, "A"), (LARANJA, "B")):
            o += [f'[node name="Spawn{tag}" type="Node3D" parent="."]', 'script = ExtResource("s_spawns")',
                  f"Team = {time}", ""]
            for i, p in enumerate(self.spawns[time], 1):
                o += [f'[node name="p{i}" type="Marker3D" parent="Spawn{tag}"]', f"transform = {xform(p)}", ""]

        o += ['[node name="PatrolRoutes" type="Node3D" parent="."]', 'script = ExtResource("s_routes")', ""]
        for nome, pontos in self.rotas:
            o += [f'[node name="{nome}" type="Node3D" parent="PatrolRoutes"]', ""]
            for i, p in enumerate(pontos, 1):
                o += [f'[node name="p{i}" type="Marker3D" parent="PatrolRoutes/{nome}"]', f"transform = {xform(p)}", ""]

        o += ['[node name="NavBaker" type="Node3D" parent="."]', 'script = ExtResource("s_nav")', ""]
        return "\n".join(o)

    def _ambiente(self):
        a = self.ambiente
        o = []
        hdri = (a.get("ceu") or {}).get("hdri")
        if hdri:
            # O céu é uma fotografia (HDRI): ilumina e reflete como o lugar.
            c = a["ceu"]
            o += ['[sub_resource type="PanoramaSkyMaterial" id="CeuMat"]', 'panorama = ExtResource("t_ceu")',
                  f"energy_multiplier = {f(c.get('energia', 1.0))}", "",
                  '[sub_resource type="Sky" id="Ceu"]', 'sky_material = SubResource("CeuMat")', ""]
        elif a.get("ceu"):
            c = a["ceu"]
            o += ['[sub_resource type="ProceduralSkyMaterial" id="CeuMat"]',
                  f"sky_top_color = {cor(c['topo'])}", f"sky_horizon_color = {cor(c['horizonte'])}",
                  f"ground_bottom_color = {cor(c['chao'])}", f"ground_horizon_color = {cor(c['horizonte'])}",
                  f"sky_energy_multiplier = {f(c.get('energia', 1.0))}", "",
                  '[sub_resource type="Sky" id="Ceu"]', 'sky_material = SubResource("CeuMat")', ""]
        o.append('[sub_resource type="Environment" id="Env"]')
        if a.get("ceu"):
            o += ["background_mode = 2", 'sky = SubResource("Ceu")']
            if hdri:
                o += [f"sky_rotation = Vector3(0, {f(math.radians(a['ceu'].get('giro', 0.0)))}, 0)"]
        else:
            o += ["background_mode = 1", f"background_color = {cor(a.get('fundo', (0, 0, 0)))}"]
        if hdri:
            o += ["ambient_light_source = 3", f"ambient_light_sky_contribution = {f(a['ceu'].get('ambiente', 0.7))}",
                  f"ambient_light_color = {cor(a.get('luz', (1, 1, 1)))}",
                  f"ambient_light_energy = {f(a.get('luz_energia', 0.5))}", "reflected_light_source = 2"]
            o += ["tonemap_mode = 3", f"tonemap_exposure = {f(a.get('exposicao', 1.0))}",
                  "ssao_enabled = true", "ssao_intensity = 2.5",
                  "glow_enabled = true", f"glow_intensity = {f(a.get('brilho', 0.6))}", "glow_bloom = 0.08"]
        else:
            o += ["ambient_light_source = 2", f"ambient_light_color = {cor(a.get('luz', (1, 1, 1)))}",
                  f"ambient_light_energy = {f(a.get('luz_energia', 0.5))}",
                  "tonemap_mode = 3", f"tonemap_exposure = {f(a.get('exposicao', 1.0))}",
                  "ssao_enabled = true", "ssao_intensity = 2.5",
                  "glow_enabled = true", f"glow_intensity = {f(a.get('brilho', 0.6))}", "glow_bloom = 0.08"]
        if a.get("neblina"):
            n = a["neblina"]
            o += ["fog_enabled = true", f"fog_light_color = {cor(n['cor'])}", f"fog_density = {f(n['densidade'])}",
                  f"fog_sky_affect = {f(n.get('ceu', 0.6))}"]
        o += ["adjustment_enabled = true", f"adjustment_contrast = {f(a.get('contraste', 1.06))}",
              f"adjustment_saturation = {f(a.get('saturacao', 1.05))}", ""]
        return o

    # ----------------------------------------------------------- miniatura

    def miniatura(self, caminho, tamanho=(512, 288)):
        """Vista de cima: cada peça na cor do material, mais clara quanto
        mais alta, com uma sombra curta. Sem renderizar nada do Godot."""
        from PIL import Image, ImageDraw
        w, h = tamanho
        escala = min(w / (self.largura + 4), h / (self.fundo + 4))
        img = Image.new("RGB", (w, h), (15, 19, 24))
        d = ImageDraw.Draw(img)

        def px(x, z):
            return (w / 2 + x * escala, h / 2 + z * escala)

        def pegada(c, tam, rows, forma, dy=0.0):
            if forma.startswith("cilindro"):
                r = tam[0]
                return [px(c[0] + r * math.cos(a) + dy, c[2] + r * math.sin(a) + dy) for a in
                        [i * math.pi / 8 for i in range(16)]]
            hx, hz = tam[0] / 2, tam[2] / 2
            pts = []
            for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                v = (sx * hx, 0, sz * hz)
                if rows:
                    v = tuple(sum(rows[i][j] * v[j] for j in range(3)) for i in range(3))
                pts.append(px(c[0] + v[0] + dy, c[2] + v[2] + dy))
            return pts

        ordem = sorted(self.pecas, key=lambda p: p[2][1] + p[3][1] / 2)
        for (_, _, c, tam, mat, rows, _, forma) in ordem:
            topo = c[1] + tam[1] / 2
            if topo > 6.0 and (tam[0] > self.largura * 0.5 or tam[2] > self.fundo * 0.5):
                continue      # teto e muros altos esconderiam o mapa
            if topo > 0.3:
                d.polygon(pegada(c, tam, rows, forma, min(topo, 6) * 0.12), fill=(8, 10, 13))
        for (_, _, c, tam, mat, rows, _, forma) in ordem:
            topo = c[1] + tam[1] / 2
            if topo > 6.0 and (tam[0] > self.largura * 0.5 or tam[2] > self.fundo * 0.5):
                continue
            m = self.materiais[mat]
            base = m["brilho"] or m["cor"]
            tom = {"asfalto": 0.25, "areia": 0.8, "neve": 0.95, "tijolo": 0.6, "madeira": 0.6,
                   "ferrugem": 0.5, "rocha": 0.5}.get(m["textura"], 0.62)
            luz = tom * (0.55 + 0.45 * min(max(topo, 0), 6) / 6)
            d.polygon(pegada(c, tam, rows, forma), fill=tuple(int(min(1, x * luz) * 255) for x in base))
        for time, c in ((AZUL, (90, 150, 255)), (LARANJA, (255, 150, 70))):
            for (x, _, z) in self.spawns[time]:
                cx, cy = px(x, z)
                d.ellipse((cx - 2.5, cy - 2.5, cx + 2.5, cy + 2.5), fill=c)
        os.makedirs(os.path.dirname(caminho), exist_ok=True)
        img.save(caminho)

    def salvar(self):
        cena = os.path.join(ROOT, "scenes", "maps", f"{self.id}.tscn")
        texto = self.tscn()
        with open(cena, "w", encoding="utf-8") as fh:
            fh.write(texto)

        # As formas: só as que este mapa usa ficam na pasta (o .import de
        # cada uma é do Godot e acompanha o arquivo).
        pasta = os.path.join(ROOT, "assets", "mapas", self.id)
        os.makedirs(pasta, exist_ok=True)
        for nome in os.listdir(pasta):
            if nome.split(".")[0] not in self._objs:
                os.remove(os.path.join(pasta, nome))
        for h, obj in self._objs.items():
            destino = os.path.join(pasta, f"{h}.obj")
            if not os.path.exists(destino):
                with open(destino, "w", encoding="utf-8") as fh:
                    fh.write(obj)
        self.miniatura(os.path.join(ROOT, "assets", "ui", "mapas", f"{self.id}.png"))
        print(f"{self.id}: {len(self.pecas)} peças, {len(self.formas)} formas ({len(self._objs)} arquivos), {len(self.luzes)} luzes, "
              f"{sum(len(v) for v in self.spawns.values())} spawns, {len(self.rotas)} rotas")
