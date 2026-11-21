import os, shutil, sys
raw, dest = sys.argv[1], sys.argv[2]
guid = {}
for d in os.listdir(raw):
    p = os.path.join(raw, d, 'pathname'); a = os.path.join(raw, d, 'asset')
    if not os.path.isfile(p): continue
    nome = open(p, encoding='utf-8', errors='replace').readline().strip()
    nome = os.path.normpath(nome)
    if nome.startswith('..') or os.path.isabs(nome): continue
    guid[d] = nome
    if os.path.isfile(a) and not nome.endswith('.unitypackage'):
        alvo = os.path.join(dest, nome); os.makedirs(os.path.dirname(alvo), exist_ok=True); shutil.copyfile(a, alvo)
    m = os.path.join(raw, d, 'asset.meta')
    if os.path.isfile(m) and nome.endswith('.fbx'):
        alvo = os.path.join(dest, nome + '.meta'); os.makedirs(os.path.dirname(alvo), exist_ok=True); shutil.copyfile(m, alvo)
import json; json.dump(guid, open(os.path.join(dest, 'guids.json'), 'w'), indent=0)
print(len(guid))
