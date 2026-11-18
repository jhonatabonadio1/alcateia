# Ícones da interface

Os SVG desta pasta vêm do **Phosphor Icons** (https://phosphoricons.com,
licença MIT — `LICENSE-phosphor.txt`), pesos *bold* e *fill*, já com a
cor trocada para branco. O nome do arquivo é o do `IconKind` em
minúsculas (`scripts/ui/kit/Icon.cs`): `shop.svg` é `IconKind.Shop`.

O jogo não lê os SVG: lê um PNG branco de 96 px por ícone, em
`assets/ui/icones/`, e pinta na cor pedida. Para gerar os PNG de novo
(depois de trocar ou acrescentar um SVG):

    godot --headless --path . -s tools/svg2png.gd -- tools/icones assets/ui/icones 96

Depois, no Godot, o PNG novo precisa de **mipmaps** ligados na
importação (os que já existem têm): sem isso o traço serrilha quando o
ícone aparece pequeno.

Ícone sem PNG continua desenhado por código em `Icon.DrawKind` — é o
caso dos que são do jogo (lobo, presa, garra, granada de choque, arma).

A pasta tem `.gdignore`: o Godot não importa os SVG.

As insígnias de patente (`assets/ui/patentes/`) saem do mesmo roteiro:

    python3 tools/patentes.py /tmp/patentes
    godot --headless --path . -s tools/svg2png.gd -- /tmp/patentes assets/ui/patentes 256
