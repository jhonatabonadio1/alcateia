"""
Peças para gerar um mapa do Alcateia como cena do Godot (.tscn): caixas,
rampas, cilindros, luzes, placas, pontos de nascimento, rotas de ronda,
o céu e o sol do mapa, e a miniatura vista de cima.

Cada mapa é um arquivo em tools/mapas/ que monta um `Mapa` e chama
`salvar()`. As texturas vêm de tools/gerar_texturas.py.

Convenções que o jogo espera (não mude sem mudar o jogo):
- X é o eixo dos times: o azul nasce em −X e olha para +X.
- Y é a altura; o chão fica em 0.
- Alturas presas à física: o humano sobe 0,8 m (caixote) e não 2,2 m
  (contêiner); o lobo alcança 2,2 m a partir do caixote; telhado a 5 m
  só por rampa ou, o lobo, ricocheteando num beco de 3 m.
- Bot não sobe degrau maior que 0,5 m: todo desnível que ele precisa
  vencer tem rampa.
"""
import math
import os

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
SUPERFICIES = {"metal_pintado": "metal", "metal_ondulado": "metal", "chapa_xadrez": "metal", "ferrugem": "metal",
               "neve": "neve", "madeira": "madeira", "areia": "terra"}


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
        self.placas, self.luzes = [], []
        self.spawns = {AZUL: [], LARANJA: []}
        self.rotas = []
        self.ambiente = {}
        self.sol = None

    # ------------------------------------------------------------ materiais

    def material(self, nome, textura=None, cor=(1, 1, 1), ladrilho=2.0, aspereza=1.0,
                 metal=0.0, relevo=1.0, brilho=None, energia=1.0):
        """textura: nome em assets/textures/pbr (ou None, cor lisa).
        ladrilho: metros que a textura cobre antes de repetir."""
        self.materiais[nome] = dict(textura=textura, cor=cor, ladrilho=ladrilho, aspereza=aspereza,
                                    metal=metal, relevo=relevo, brilho=brilho, energia=energia)

    # ---------------------------------------------------------------- peças

    def caixa(self, grupo, nome, centro, tamanho, material, colide=True, rot=None):
        self.pecas.append((grupo, nome, centro, tamanho, material, rot, colide, "caixa"))

    def cilindro(self, grupo, nome, centro, raio, altura, material, colide=True):
        self.pecas.append((grupo, nome, centro, (raio, altura, raio), material, None, colide, "cilindro"))

    def rampa(self, grupo, nome, x_baixo, x_alto, h, zc, largura, material, t=0.4, base=0.0):
        """Rampa ao longo de X: de `base` em x_baixo até base + h em x_alto."""
        dx = x_alto - x_baixo
        theta = math.copysign(math.atan2(h, abs(dx)), dx)
        c, s = math.cos(theta), math.sin(theta)
        normal = (-s, c)
        mx, my = (x_baixo + x_alto) / 2.0, base + h / 2.0
        centro = (mx - normal[0] * t / 2, my - normal[1] * t / 2, zc)
        self.caixa(grupo, nome, centro, (math.hypot(abs(dx), h) + 0.8, t, largura), material, True,
                   ((c, -s, 0.0), (s, c, 0.0), (0.0, 0.0, 1.0)))

    def rampa_z(self, grupo, nome, z_baixo, z_alto, h, xc, largura, material, t=0.4, base=0.0):
        """Rampa ao longo de Z."""
        dz = z_alto - z_baixo
        theta = math.copysign(math.atan2(h, abs(dz)), dz)
        c, s = math.cos(theta), math.sin(theta)
        mz, my = (z_baixo + z_alto) / 2.0, base + h / 2.0
        centro = (xc, my - c * t / 2, mz + s * t / 2)
        self.caixa(grupo, nome, centro, (largura, t, math.hypot(abs(dz), h) + 0.8), material, True,
                   ((1.0, 0.0, 0.0), (0.0, c, s), (0.0, -s, c)))

    def parapeito(self, grupo, nome, centro, comprimento, ao_longo="x", altura=1.0, painel="aco", corrimao="grade"):
        """Parapeito: painel baixo que cobre e um corrimão amarelo em cima.
        `centro` é o meio do painel, no nível do piso + altura/2."""
        e = 0.12
        tam = (comprimento, altura - 0.1, e) if ao_longo == "x" else (e, altura - 0.1, comprimento)
        topo = (comprimento, 0.1, e * 1.6) if ao_longo == "x" else (e * 1.6, 0.1, comprimento)
        self.caixa(grupo, nome, (centro[0], centro[1] - 0.05, centro[2]), tam, painel)
        self.caixa(grupo, nome + "Corrimao", (centro[0], centro[1] + altura / 2 - 0.05, centro[2]), topo, corrimao, colide=False)

    def placa(self, grupo, nome, pos, texto, tamanho, cor_, yaw):
        self.placas.append((grupo, nome, pos, texto, tamanho, cor_, yaw))

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
            chave = (forma, *[round(x, 4) for x in tam])
            malhas.setdefault(chave, f"B{len(malhas) + 1}")
            if colide:
                formas.setdefault(chave, f"S{len(formas) + 1}")

        texturas = sorted({m["textura"] for m in self.materiais.values() if m["textura"]})
        ext, tex_id = [], {}
        for t in texturas:
            for sufixo in ("cor", "normal", "asp"):
                rid = f"t_{t}_{sufixo}"
                tex_id[(t, sufixo)] = rid
                ext.append(("Texture2D", f"res://assets/textures/pbr/{t}_{sufixo}.jpg", rid))
        ext += [("Script", "res://scripts/core/SpawnPoints.cs", "s_spawns"),
                ("Script", "res://scripts/core/PatrolRoutes.cs", "s_routes"),
                ("Script", "res://scripts/core/NavBaker.cs", "s_nav")]

        o = ["[gd_scene format=3]", ""]
        o += [f'[ext_resource type="{k}" path="{p}" id="{i}"]' for k, p, i in ext] + [""]

        for nome, m in self.materiais.items():
            o.append(f'[sub_resource type="StandardMaterial3D" id="M_{nome}"]')
            o.append(f"albedo_color = {cor(m['cor'])}")
            if m["textura"]:
                t = m["textura"]
                o += [f'albedo_texture = ExtResource("{tex_id[(t, "cor")]}")',
                      f'roughness_texture = ExtResource("{tex_id[(t, "asp")]}")',
                      "normal_enabled = true",
                      f"normal_scale = {f(m['relevo'])}",
                      f'normal_texture = ExtResource("{tex_id[(t, "normal")]}")',
                      f"uv1_scale = {vec((1.0 / m['ladrilho'],) * 3)}",
                      "uv1_triplanar = true", "uv1_world_triplanar = true"]
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
        for item in self.pecas + self.placas + self.luzes:
            if item[0] not in grupos:
                grupos.append(item[0])
        for g in grupos:
            o += [f'[node name="{g}" type="Node3D" parent="."]', ""]

        for (g, nome, c, tam, mat, rows, colide, forma) in self.pecas:
            chave = (forma, *[round(x, 4) for x in tam])
            if colide:
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

        for (g, nome, pos, texto, tam, c, yaw) in self.placas:
            o += [f'[node name="{nome}" type="Label3D" parent="{g}"]', f"transform = {xform(pos, giro_y(yaw))}",
                  "pixel_size = 0.01", f"modulate = {cor(c)}", f'text = "{texto}"', f"font_size = {tam}",
                  f"outline_size = {max(8, tam // 14)}", ""]

        for (g, nome, pos, c, energia, alcance, sombra) in self.luzes:
            o += [f'[node name="{nome}" type="OmniLight3D" parent="{g}"]', f"transform = {xform(pos)}",
                  f"light_color = {cor(c)}", f"light_energy = {f(energia)}", f"omni_range = {f(alcance)}"]
            if sombra:
                o.append("shadow_enabled = true")
            o.append("")

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
        if a.get("ceu"):
            c = a["ceu"]
            o += ['[sub_resource type="ProceduralSkyMaterial" id="CeuMat"]',
                  f"sky_top_color = {cor(c['topo'])}", f"sky_horizon_color = {cor(c['horizonte'])}",
                  f"ground_bottom_color = {cor(c['chao'])}", f"ground_horizon_color = {cor(c['horizonte'])}",
                  f"sky_energy_multiplier = {f(c.get('energia', 1.0))}", "",
                  '[sub_resource type="Sky" id="Ceu"]', 'sky_material = SubResource("CeuMat")', ""]
        o.append('[sub_resource type="Environment" id="Env"]')
        if a.get("ceu"):
            o += ["background_mode = 2", 'sky = SubResource("Ceu")']
        else:
            o += ["background_mode = 1", f"background_color = {cor(a.get('fundo', (0, 0, 0)))}"]
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
            if forma == "cilindro":
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
        with open(cena, "w", encoding="utf-8") as fh:
            fh.write(self.tscn())
        self.miniatura(os.path.join(ROOT, "assets", "ui", "mapas", f"{self.id}.png"))
        print(f"{self.id}: {len(self.pecas)} peças, {len(self.luzes)} luzes, "
              f"{sum(len(v) for v in self.spawns.values())} spawns, {len(self.rotas)} rotas")
