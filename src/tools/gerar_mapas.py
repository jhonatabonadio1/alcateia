#!/usr/bin/env python3
"""
Gera os mapas feitos da cena de demonstração do kit (27/11): Cidade, Cidade do Deserto, Estação de Trem e Obra —
src/scenes/maps/<id>.tscn, o modelo src/assets/mapas/pecas/<id>.glb e a miniatura src/assets/ui/mapas/<id>.png.

    python3 src/tools/gerar_mapas.py            # os quatro
    python3 src/tools/gerar_mapas.py deserto    # um só

Precisa de numpy e Pillow. Depois de gerar, o Godot importa o .glb de novo (abra o editor, ou `godot --headless
--import`), e as fotos saem de src/tools/fotos_mapas.gd.

ATENÇÃO: gerar SOBRESCREVE o .tscn e o .glb. O que foi mexido à mão na cena, no Godot, se perde: se o mapa já foi
editado lá, a cena é a fonte e este gerador fica só como registro de como ela nasceu (docs/MAPAS.md). Por isso só
os quatro mapas de 27/11 estão na lista: o Armazém e o Distrito Neon foram mexidos na cena depois de gerados e não
se geram mais (os módulos deles em mapas/ são o registro).
"""
import sys

from mapas import cidade, deserto, estacao, obra

MAPAS = {"cidade": cidade, "deserto": deserto, "estacao": estacao, "obra": obra}

if __name__ == "__main__":
    for nome in (sys.argv[1:] or MAPAS):
        MAPAS[nome].montar().salvar()
