"""
Ferramentas de modelagem para os scripts do Blender desta pasta.

Tudo é dito nas coordenadas do jogo (X para o leste, Y para cima, Z para
o sul, em metros) e convertido para as do Blender (Z para cima) aqui —
quem modela não pensa nisso. Um modelo é uma malha só, com uma
superfície por material.

Rodar: `python3 tools/mapas/blender/<script>.py` (precisa do módulo
`bpy`: `pip install bpy`) ou `blender -b -P tools/mapas/blender/<script>.py`.
"""
import math
import os

import bpy  # antes do bmesh: é ele que traz os outros
import bmesh
from mathutils import Matrix, Vector

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
PECAS = os.path.join(RAIZ, "assets", "mapas", "pecas")


def b(x, y, z):
    """Do jogo para o Blender."""
    return Vector((x, -z, y))


class Modelo:
    def __init__(self, nome):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        self.nome = nome
        self.bm = bmesh.new()
        self.materiais = []
        self._indice = {}

    # ------------------------------------------------------------ materiais

    def material(self, nome, cor, aspereza=0.6, metal=0.0, brilho=None, energia=1.0, alfa=1.0):
        m = bpy.data.materials.new(nome)
        m.use_nodes = True
        p = m.node_tree.nodes["Principled BSDF"]
        p.inputs["Base Color"].default_value = (*cor, 1.0)
        p.inputs["Roughness"].default_value = aspereza
        p.inputs["Metallic"].default_value = metal
        if brilho:
            p.inputs["Emission Color"].default_value = (*brilho, 1.0)
            p.inputs["Emission Strength"].default_value = energia
        if alfa < 1.0:
            p.inputs["Alpha"].default_value = alfa
            m.surface_render_method = "BLENDED"
        self._indice[nome] = len(self.materiais)
        self.materiais.append(m)

    # ---------------------------------------------------------------- peças

    def _fechar(self, geom, mat, chanfro, liso):
        faces = [g for g in geom if isinstance(g, bmesh.types.BMFace)]
        if chanfro > 0:
            arestas = list({e for f in faces for e in f.edges})
            novo = bmesh.ops.bevel(self.bm, geom=arestas, offset=chanfro, segments=2, profile=0.5, affect="EDGES")
            faces = [f for f in set(faces) | set(novo["faces"]) if f.is_valid]
        for f in faces:
            f.material_index = self._indice[mat]
            f.smooth = liso

    def caixa(self, centro, tamanho, mat, chanfro=0.008, giro_y=0.0):
        """Caixa com as quinas arredondadas (`chanfro`, em metros)."""
        sx, sy, sz = tamanho
        chanfro = min(chanfro, min(sx, sy, sz) * 0.3)
        m = Matrix.Translation(b(*centro)) @ Matrix.Rotation(math.radians(giro_y), 4, "Z") @ Matrix.Diagonal((sx, sz, sy, 1.0))
        geom = bmesh.ops.create_cube(self.bm, size=1.0, matrix=m)["verts"]
        faces = list({f for v in geom for f in v.link_faces})
        self._fechar(faces, mat, chanfro, liso=chanfro > 0)

    def cilindro(self, centro, raio, comprimento, mat, eixo="y", lados=24, raio2=None):
        """Cilindro (ou tronco de cone) ao longo de um eixo do jogo."""
        giro = {"y": Matrix.Identity(4), "z": Matrix.Rotation(math.pi / 2, 4, "X"),
                "x": Matrix.Rotation(math.pi / 2, 4, "Y")}[eixo]
        geom = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=lados, radius1=raio,
                                     radius2=raio if raio2 is None else raio2, depth=comprimento,
                                     matrix=Matrix.Translation(b(*centro)) @ giro)["verts"]
        faces = list({f for v in geom for f in v.link_faces})
        for f in faces:
            f.material_index = self._indice[mat]
            f.smooth = len(f.verts) == 4
        return faces

    def tubo(self, pontos, raio, mat, lados=10):
        """Tubo reto de ponto a ponto (corrimão, balaústre)."""
        for a, c in zip(pontos, pontos[1:]):
            va, vc = b(*a), b(*c)
            d = vc - va
            giro = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
            geom = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=lados, radius1=raio, radius2=raio,
                                         depth=d.length, matrix=Matrix.Translation((va + vc) / 2) @ giro)["verts"]
            for f in {f for v in geom for f in v.link_faces}:
                f.material_index = self._indice[mat]
                f.smooth = len(f.verts) == 4

    def perfil(self, pontos_zy, x0, x1, mat, liso=True):
        """Um contorno fechado no plano ZY (corte do vagão) puxado de x0 a x1."""
        a = [self.bm.verts.new(b(x0, y, z)) for z, y in pontos_zy]
        c = [self.bm.verts.new(b(x1, y, z)) for z, y in pontos_zy]
        n = len(a)
        faces = [self.bm.faces.new((a[i], a[(i + 1) % n], c[(i + 1) % n], c[i])) for i in range(n)]
        faces += [self.bm.faces.new(list(reversed(a))), self.bm.faces.new(c)]
        for f in faces:
            f.material_index = self._indice[mat]
            f.smooth = liso and len(f.verts) == 4
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)

    # --------------------------------------------------------------- saída

    def salvar(self, arquivo=None):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=0.00001)
        malha = bpy.data.meshes.new(self.nome)
        self.bm.to_mesh(malha)
        self.bm.free()
        for m in self.materiais:
            malha.materials.append(m)
        # Liso onde a curva pede, quina viva onde o ângulo é grande.
        malha.set_sharp_from_angle(angle=math.radians(38))

        obj = bpy.data.objects.new(self.nome, malha)
        bpy.context.scene.collection.objects.link(obj)

        os.makedirs(PECAS, exist_ok=True)
        caminho = os.path.join(PECAS, (arquivo or self.nome) + ".glb")
        bpy.ops.export_scene.gltf(filepath=caminho, export_format="GLB", export_yup=True, export_apply=True,
                                  export_materials="EXPORT", export_animations=False, export_cameras=False,
                                  export_lights=False)
        print(f"{self.nome}: {len(malha.polygons)} faces, {len(self.materiais)} materiais -> {os.path.relpath(caminho, RAIZ)}")
