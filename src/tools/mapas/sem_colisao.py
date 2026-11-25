"""
Tira a colisão de peças de um mapa de kit já montado, direto no .glb dele: o nó com o sufixo ``-col`` (o Godot, ao
importar, faz dele um corpo com a forma da malha — ``kits/montagem.py``) perde o sufixo e fica só desenho.

Por quê (Jhon, 25/11, no Distrito Neon): "os fios do mapa neon não podem ter colisão (fios das torres de energia)". Os
fios que cruzam a rua de dentro dos quarteirões, de prédio a prédio e das antenas (``SM_Buildings_Part_24`` e
``_25``: 10 m e 5 m de fio, com a barriga de 0,8 m), seguravam a bala e o corpo — o lobo batia a cabeça num fio no pulo.
A torre (a antena, ``SM_Buildings_Part_31``) continua sólida. O gerador (``neon.py: FIOS``) já monta sem a colisão;
isto é para o .glb que está no projeto, sem refazer o mapa.

Uso, da raiz do projeto (o Godot importa de novo na próxima vez que abrir):
    python3 src/tools/mapas/sem_colisao.py src/assets/mapas/pecas/neon.glb SM_Buildings_Part_24_ SM_Buildings_Part_25_
"""
import json, struct, sys


def sem_colisao(caminho, prefixos):
    d = open(caminho, 'rb').read()
    _, versao, _ = struct.unpack('<4sII', d[:12])
    o = 12; partes = []
    while o < len(d):
        n, t = struct.unpack('<I4s', d[o:o + 8]); partes.append([t, d[o + 8:o + 8 + n]]); o += 8 + n
    j = json.loads(partes[0][1])
    tiradas = []
    for no in j['nodes']:
        nome = no.get('name', '')
        if nome.endswith('-col') and nome.startswith(tuple(prefixos)):
            no['name'] = nome[:-4]; tiradas.append(no['name'])
    texto = json.dumps(j, separators=(',', ':')).encode()
    while len(texto) % 4: texto += b' '
    partes[0][1] = texto
    corpo = b''.join(struct.pack('<I4s', len(c), t) + c for t, c in partes)
    with open(caminho, 'wb') as f:
        f.write(struct.pack('<4sII', b'glTF', versao, 12 + len(corpo))); f.write(corpo)
    return tiradas


if __name__ == '__main__':
    feitas = sem_colisao(sys.argv[1], sys.argv[2:])
    print(f"{len(feitas)} peças sem colisão: {', '.join(sorted(feitas))}")
