"""Lê cada .fbx do kit com o Blender (bpy) e grava a malha crua (no espaço local da malha, como está no arquivo):
malhas/<nome>.npz com tri (T×3×8: pos, normal, uv), mat (T, índice do material), e um json com nomes e unidade."""
import bpy, sys, os, json, struct, numpy as np
raiz, saida = sys.argv[-2], sys.argv[-1]

def unidade(caminho):
    d = open(caminho, 'rb').read(); i = d.find(b'UnitScaleFactor')
    if i < 0: return 1.0
    j = d.find(b'D', i + 15, i + 80)
    # o último campo do registro P é um double
    k = d.rfind(b'D', i, i + 70)
    return struct.unpack('<d', d[k + 1:k + 9])[0]

indice = {}
for pasta, _, arqs in os.walk(raiz):
    for a in sorted(arqs):
        if not a.lower().endswith('.fbx'): continue
        c = os.path.join(pasta, a)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        try: bpy.ops.import_scene.fbx(filepath=c)
        except Exception as e: print('FALHOU', a, e); continue
        malhas = []
        for o in bpy.data.objects:
            if o.type != 'MESH': continue
            m = o.data; m.calc_loop_triangles()
            uv = m.uv_layers.active.data if m.uv_layers.active else None
            try: m.calc_normals_split()
            except Exception: pass
            T = len(m.loop_triangles); tri = np.zeros((T, 3, 8), np.float32); mat = np.zeros(T, np.int32)
            for t, lt in enumerate(m.loop_triangles):
                mat[t] = lt.material_index
                for k in range(3):
                    v = m.vertices[lt.vertices[k]].co; n = lt.split_normals[k]; lo = lt.loops[k]
                    tri[t, k, :3] = v; tri[t, k, 3:6] = n
                    if uv: tri[t, k, 6:8] = uv[lo].uv
            nome = os.path.splitext(a)[0] + ('' if len([x for x in bpy.data.objects if x.type == 'MESH']) == 1 else '__' + o.name)
            np.savez_compressed(os.path.join(saida, nome + '.npz'), tri=tri, mat=mat)
            malhas.append(dict(arquivo=nome, objeto=o.name, malha=m.name, materiais=[s.material.name if s.material else '' for s in o.material_slots],
                               mundo=[list(r) for r in o.matrix_world], pai=o.parent.name if o.parent else None, tris=T))
        indice[os.path.splitext(a)[0]] = dict(unidade=unidade(c), malhas=malhas)
json.dump(indice, open(os.path.join(saida, 'indice.json'), 'w'), indent=1)
print(len(indice), 'fbx')
