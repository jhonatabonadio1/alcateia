#!/usr/bin/env python3
"""
Põe o detalhe de superfície nos mapas de paleta que já estão no projeto, sem gerar o mapa de novo: grava o material de
cada face no .glb e liga o material de detalhe no ``.glb.import`` (veja ``tools/mapas/kits/detalhe.py`` e a seção
"O detalhe das superfícies" de ``docs/MAPAS.md``).

    python3 tools/mapas/detalhar.py             # todos
    python3 tools/mapas/detalhar.py cidade obra  # só esses

Os geradores já chamam isto sozinhos (``Montagem.glb`` e ``tools/mapas/armazem*.py``); rode à mão depois de mudar uma
regra em ``detalhe.py``. Depois, abra o Godot (ou ``godot --headless --import``) para ele importar de novo.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from mapas.kits import detalhe as D          # noqa: E402

PECAS = os.path.join(D.RAIZ, "assets", "mapas", "pecas")
# o .glb -> o kit (a paleta e as regras)
KITS = dict(cidade="fps1", deserto="fps2", estacao="fps5", estacao_cidade="fps1", estacao_mato="fps3", obra="fps6", obra_mato="fps3")
ARMAZENS = ("armazem", "armazem_estendido")


def main(pedidos):
    for nome in list(KITS) + list(ARMAZENS):
        if pedidos and nome not in pedidos: continue
        caminho = os.path.join(PECAS, nome + ".glb")
        if not os.path.exists(caminho): continue
        if nome in ARMAZENS: D.detalhar_armazem(nome)
        else: D.detalhar(caminho, KITS[nome])


if __name__ == "__main__":
    main(sys.argv[1:])
