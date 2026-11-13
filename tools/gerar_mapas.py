#!/usr/bin/env python3
"""
Gera os mapas do Alcateia:
scenes/maps/<id>.tscn e a miniatura assets/ui/mapas/<id>.png.

    python3 tools/mapas/texturas.py     # uma vez, ou quando mudar textura
    python3 tools/gerar_mapas.py        # todos
    python3 tools/gerar_mapas.py vila   # um só

Cada mapa é um arquivo em tools/mapas/ (hangar, metro, vila, polar) que diz, no
comentário do topo, o tema e como responde às quatro perguntas do teste
de mapa. Mapa novo: copie um, mude, e registre em scripts/meta/Maps.cs.

ATENÇÃO: rodar de novo SOBRESCREVE o .tscn. Editou o mapa no Godot? O
.tscn passa a ser a fonte, e o gerador vira o registro de como nasceu.
"""
import sys

from mapas import hangar, metro, vila, polar

MAPAS = {"hangar": hangar, "metro": metro, "vila": vila, "polar": polar}

if __name__ == "__main__":
    for nome in (sys.argv[1:] or MAPAS):
        MAPAS[nome].montar().salvar()
