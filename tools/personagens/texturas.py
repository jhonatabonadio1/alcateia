"""
Prepara as texturas do pacote Stylized Sci-Fi Characters para o scifi.py:
reduz (roupa e corpo 2048, cabelo 1024), monta o ORM a partir da "Mask" do
Unity (G oclusão, A lisura -> aspereza, R metal) e grava em JPG.

    python3 tools/personagens/texturas.py <pasta do pacote> <pasta de saída>

Saída: {m,f}_{Outfit,Body,Hair}_{color,normal,orm}.jpg e {m,f}_Outfit_emissive.jpg
"""
import sys, os
from PIL import Image

src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
for g, G in (('m', 'Male'), ('f', 'Female')):
    for part, size in (('', 2048), ('Body_', 2048), ('Hair_', 1024)):
        base = os.path.join(src, f'T_StylizedSciFiCharacter_{G}_{part}')
        name = os.path.join(out, f'{g}_{part or "Outfit_"}')
        Image.open(base + 'Color.png').convert('RGB').resize((size, size), Image.LANCZOS).save(name + 'color.jpg', quality=92)
        Image.open(base + 'Normal.png').convert('RGB').resize((size, size), Image.LANCZOS).save(name + 'normal.jpg', quality=88)
        r, gc, b, a = Image.open(base + 'Mask.png').convert('RGBA').resize((size, size), Image.LANCZOS).split()
        Image.merge('RGB', (gc, a.point(lambda v: 255 - v), r)).save(name + 'orm.jpg', quality=92)
    Image.open(os.path.join(src, f'T_StylizedSciFiCharacter_{G}_Emissive.png')).convert('RGB') \
        .resize((1024, 1024), Image.LANCZOS).save(os.path.join(out, f'{g}_Outfit_emissive.jpg'), quality=92)
print('ok', out)
