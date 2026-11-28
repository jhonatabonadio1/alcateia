#!/usr/bin/env python3
"""Gera a Figtree em pesos fixos para o cliente, a partir da variável do jogo.

Uso: gerar_figtree.py   (precisa de fonttools: pip install fonttools)

O jogo usa um arquivo só, a Figtree variável (src/assets/fonts/Figtree-Variable.ttf),
e pede o peso ao Godot. O Avalonia, do cliente, não aplica o peso de uma fonte
variável: tudo sai no peso de fábrica dela (300, fino). Aqui se tiram do mesmo
arquivo os quatro pesos que os menus do jogo usam (UiFonts.cs: 400, 500, 600 e
700), cada um num .ttf, em cliente/Alcateia.Cliente/Assets/Fonts/.

Trocou a Figtree do jogo? Rode de novo. A licença é a OFL (src/assets/fonts/OFL-Figtree.txt),
sem nome reservado: os arquivos gerados seguem se chamando Figtree.
"""
import os
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ORIGEM = os.path.join(RAIZ, "src/assets/fonts/Figtree-Variable.ttf")
SAIDA = os.path.join(RAIZ, "cliente/Alcateia.Cliente/Assets/Fonts")
PESOS = {"Regular": 400, "Medium": 500, "SemiBold": 600, "Bold": 700}


def nomear(fonte: TTFont, estilo: str, peso: int) -> None:
    """Família "Figtree" e o estilo certo, para o sistema casar o peso pedido com o arquivo."""
    nomes = fonte["name"]
    for registro in list(nomes.names):
        if registro.nameID in (16, 17, 25):          # nomes tipográficos da variável
            nomes.removeNames(nameID=registro.nameID)
    for plataforma, codificacao, lingua in ((3, 1, 0x409), (1, 0, 0)):
        nomes.setName("Figtree", 1, plataforma, codificacao, lingua)
        nomes.setName(estilo, 2, plataforma, codificacao, lingua)
        nomes.setName(f"Figtree {estilo}", 4, plataforma, codificacao, lingua)
        nomes.setName(f"Figtree-{estilo}", 6, plataforma, codificacao, lingua)
        nomes.setName("Figtree", 16, plataforma, codificacao, lingua)
        nomes.setName(estilo, 17, plataforma, codificacao, lingua)
    fonte["OS/2"].usWeightClass = peso
    negrito = estilo == "Bold"
    fonte["OS/2"].fsSelection = (fonte["OS/2"].fsSelection & ~0b1100001) | (0b100000 if negrito else 0b1000000)
    fonte["head"].macStyle = 1 if negrito else 0


def main() -> None:
    os.makedirs(SAIDA, exist_ok=True)
    for estilo, peso in PESOS.items():
        fonte = instancer.instantiateVariableFont(TTFont(ORIGEM), {"wght": peso})
        nomear(fonte, estilo, peso)
        destino = os.path.join(SAIDA, f"Figtree-{estilo}.ttf")
        fonte.save(destino)
        print(f"{destino}  ({os.path.getsize(destino) // 1024} KB)")


if __name__ == "__main__":
    main()
