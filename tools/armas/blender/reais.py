"""
Converte as armas que o Jhon trouxe em 19/11 (``Archive 2.zip``: M4A4, AK-74, FN SCAR-H,
SVD Dragunov, Desert Eagle e a granada V40 Mini — cinco .fbx e um .blend, cada um com a
malha e os ossos das peças que se mexem) em ``.npz`` que o resto das ferramentas lê sem
Blender (``tools/armas/reais.py``).

    blender -b -P tools/armas/blender/reais.py -- <pasta com os arquivos> assets/pacotes/reais

Cada ``.npz``: os triângulos no espaço do arquivo (``pos``, ``normal``), de que material
(``mat``) e de que osso (``osso``) é cada um, os nomes e as cores dos materiais como vieram
e a cabeça de cada osso. As peças soltas sem osso (a luneta do SVD) entram com o nome do
objeto no lugar do osso.
"""
import bpy, sys, os
import numpy as np

ARQUIVOS = {'M4a4.fbx': 'm4a4', 'AK-74.fbx': 'ak74', 'FN Scar H.fbx': 'scar', 'SVD Dragunov.fbx': 'svd', 'Deagle.fbx': 'deagle', 'V40 Mini.blend': 'v40'}


def converter(caminho, destino):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if caminho.endswith('.blend'): bpy.ops.wm.open_mainfile(filepath=caminho)
    else: bpy.ops.import_scene.fbx(filepath=caminho)
    pos, normal, mat, osso = [], [], [], []; materiais, cores, ossos, cabecas = [], [], [], []
    def indice(lista, nome):
        if nome not in lista: lista.append(nome)
        return lista.index(nome)
    for o in bpy.data.objects:
        if o.type == 'ARMATURE':
            for b in o.data.bones: indice(ossos, b.name.lower()); cabecas.append(tuple(o.matrix_world @ b.head_local))
    cabecas = dict(zip(ossos, cabecas))
    for o in bpy.data.objects:
        if o.type != 'MESH': continue
        m = o.data; m.calc_loop_triangles(); W = o.matrix_world; N = W.to_3x3().inverted().transposed()
        grupos = {g.index: g.name.lower() for g in o.vertex_groups}
        cn = m.corner_normals
        for t in m.loop_triangles:
            vs = [m.vertices[i] for i in t.vertices]
            pos.append([tuple(W @ v.co) for v in vs]); normal.append([tuple((N @ cn[l].vector).normalized()) for l in t.loops])
            slot = o.material_slots[t.material_index].material if o.material_slots else None
            nome = slot.name if slot else 'sem'
            if nome not in materiais:
                bs = next((n for n in slot.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if slot and slot.node_tree else None
                cores.append(tuple(bs.inputs['Base Color'].default_value)[:3] + (bs.inputs['Metallic'].default_value, bs.inputs['Roughness'].default_value) if bs else (0.8, 0.8, 0.8, 0.0, 0.5))
            mat.append(indice(materiais, nome))
            g = [max(v.groups, key=lambda x: x.weight).group for v in vs if v.groups]
            osso.append(indice(ossos, grupos[g[0]] if g else o.name.lower()))
    np.savez_compressed(destino, pos=np.array(pos, np.float32), normal=np.array(normal, np.float32), mat=np.array(mat, np.int16), osso=np.array(osso, np.int16),
                        materiais=np.array(materiais), cores=np.array(cores, np.float32), ossos=np.array(ossos),
                        cabecas=np.array([cabecas.get(n, (0, 0, 0)) for n in ossos], np.float32))
    print(f"{os.path.basename(caminho)} -> {destino}: {len(pos)} triângulos, {len(materiais)} materiais, ossos {ossos}")


if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    origem, destino = args[0], args[1]; os.makedirs(destino, exist_ok=True)
    for raiz, _, nomes in os.walk(origem):
        for nome in nomes:
            if nome in ARQUIVOS: converter(os.path.join(raiz, nome), os.path.join(destino, ARQUIVOS[nome] + '.npz'))
