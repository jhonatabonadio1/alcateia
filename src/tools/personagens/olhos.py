"""O meio dos dois olhos do humano de cada personagem, no espaço do osso da cabeça.

É o CharacterSkin.EyeInHead do personagem.tres: onde fica a câmera da 1ª pessoa
(o olho de quem joga é o olho do corpo visto; Jhon, 26/11). Acha os triângulos
brancos da frente do rosto na malha da cabeça (o branco dos olhos, na textura) e
leva o meio deles ao osso mixamorig_Head pela ligação da pele.

Uso: python3 src/tools/personagens/olhos.py [raiz do projeto]   (pede numpy e Pillow)
Personagem novo: ponha a pasta dele na lista abaixo e copie o resultado para o
personagem.tres (EyeInHead = Vector3(x, y, z)).
"""
import json, struct, sys
import numpy as np
from PIL import Image

import os
ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..")
for p, tex in [('src/assets/personagens/eve/humano.glb', 'src/assets/personagens/eve/humano_f_Body_color.jpg'),
               ('src/assets/personagens/barut/humano.glb', 'src/assets/personagens/barut/humano_m_Body_color.jpg')]:
    d = open(f'{ROOT}/{p}', 'rb').read(); n = struct.unpack('<I', d[12:16])[0]; j = json.loads(d[20:20 + n]); b = d[20 + n + 8:]

    def acc(i):
        a = j['accessors'][i]; v = j['bufferViews'][a['bufferView']]
        dt = {5126: np.float32, 5123: np.uint16, 5125: np.uint32, 5121: np.uint8}[a['componentType']]
        nc = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}[a['type']]
        o = v.get('byteOffset', 0) + a.get('byteOffset', 0); st = v.get('byteStride')
        if st and st != np.dtype(dt).itemsize * nc:
            raw = np.frombuffer(b, np.uint8, st * (a['count'] - 1) + np.dtype(dt).itemsize * nc, o)
            idx = np.arange(a['count'])[:, None] * st + np.arange(np.dtype(dt).itemsize * nc)[None, :]
            return np.frombuffer(raw[idx].tobytes(), dt).reshape(a['count'], nc)
        return np.frombuffer(b, dt, a['count'] * nc, o).reshape(a['count'], nc)

    T = np.asarray(Image.open(f'{ROOT}/{tex}').convert('RGB')).astype(float) / 255; H, W = T.shape[:2]
    mi = [i for i, m in enumerate(j['meshes']) if 'head' in m['name'].lower()][0]
    pr = j['meshes'][mi]['primitives'][0]
    pos = acc(pr['attributes']['POSITION']).astype(float); uv = acc(pr['attributes']['TEXCOORD_0']).astype(float)
    idx = acc(pr['indices']).reshape(-1, 3).astype(int)
    # amostra o meio de cada triângulo (a cor do triângulo)
    cuv = uv[idx].mean(1); cpos = pos[idx].mean(1)
    c = T[np.clip((cuv[:, 1] % 1 * H).astype(int), 0, H - 1), np.clip((cuv[:, 0] % 1 * W).astype(int), 0, W - 1)]
    white = (c.min(1) > 0.65) & (c.max(1) - c.min(1) < 0.12) & (cpos[:, 2] > 0.03) & (np.abs(cpos[:, 0]) < 0.07)
    pts = cpos[white]
    left = pts[pts[:, 0] > 0].mean(0); right = pts[pts[:, 0] < 0].mean(0)
    mid = (left + right) / 2
    # a frente do olho: o ponto mais à frente da malha nessa altura
    print(p, 'triângulos brancos', white.sum(), 'olho esq', left.round(4), 'dir', right.round(4), 'meio', mid.round(4))
    skin = j['skins'][0]; names = [j['nodes'][k]['name'] for k in skin['joints']]
    ibm = acc(skin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
    h = names.index('mixamorig:Head')
    local = (ibm[h] @ np.r_[mid, 1.0])[:3]
    scale = np.linalg.norm(ibm[h][:3, 0])
    print('   no osso Head (unidades do esqueleto):', local.round(3), ' escala do osso por metro da malha:', scale.round(3))
    print('   cabeça (topo da malha):', pos[:, 1].max().round(4), ' olho abaixo do topo:', (pos[:, 1].max() - mid[1]).round(4))
