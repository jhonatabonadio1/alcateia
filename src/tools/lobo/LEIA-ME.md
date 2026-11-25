# tools/lobo — os lobos dos times

Desde 25/11 os dois lobos (`eve/lobo_azul.glb` e `barut/lobo_vermelho.glb`)
são **o mesmo corpo**: o modelo que o Jhon gerou no Meshy ("WerewolfNaturalWolf",
10 mil triângulos, texturas de 4096), que ele pôs em `src/assets/222` como
alvo ("quero algo bem próximo daquilo"). O Meshy entrega malha e textura,
sem esqueleto; estas ferramentas põem o modelo no esqueleto do lobo do jogo
e fazem das texturas dele as dos dois times.

```
# o esqueleto: o lobo azul original (o do Meshy de 17/11, 24 ossos), do git
git show 147a358:assets/characters/wolf/lobo_azul.glb > /tmp/lobo_original.glb

# 1. a malha no esqueleto (Blender como módulo: pip install bpy; numpy, scipy)
python3 src/tools/lobo/malha.py /tmp/lobo_original.glb src/assets/222 /tmp/malha.npz
# 2. um GLB sem oclusão, só para assá-la (Cycles, uns 8 minutos em 4096)
python3 src/tools/lobo/montar.py azul /tmp/lobo_original.glb /tmp/malha.npz src/assets/222 /tmp/sem_ao.glb
python3 src/tools/lobo/assar_ao.py /tmp/sem_ao.glb /tmp/ao.png 64 4096
# 3. os dois lobos (numpy, scipy, pillow, matplotlib)
python3 src/tools/lobo/montar.py azul /tmp/lobo_original.glb /tmp/malha.npz src/assets/222 src/assets/personagens/eve/lobo_azul.glb --ao /tmp/ao.png --previa /tmp
python3 src/tools/lobo/montar.py vermelho /tmp/lobo_original.glb /tmp/malha.npz src/assets/222 src/assets/personagens/barut/lobo_vermelho.glb --ao /tmp/ao.png
# 4. conferir fora do Godot (Cycles): frente, costas, lado
python3 src/tools/lobo/render_glb.py src/assets/personagens/eve/lobo_azul.glb /tmp/lobo.png
```

Depois, o Godot reimporta o GLB e tira dele as quatro texturas
(`lobo_*_lobo_{base,orm,normal,emissao}.jpg`, ao lado do arquivo).

## `malha.py` — a malha no esqueleto

- **Alinha** o modelo ao lobo do jogo: mesma altura (1,66 m), pés no chão,
  o meio no meio. As duas malhas são o mesmo desenho e ficam a 1,8 cm uma
  da outra (mediana), então **o esqueleto não muda**: os clipes, a passada,
  a altura e a cabeça que leva tiro continuam os mesmos.
- **Tangentes** do Blender (MikkTSpace) com as normais do arquivo: são as
  que o mapa de normal do Meshy espera.
- **Pesos** tirados do lobo do jogo, do ponto mais perto da malha dele,
  alisados pelos vizinhos, no máximo 4 ossos. Cantos do mesmo vértice têm
  os mesmos pesos: a malha não abre.
- **O rabo** (o lobo do jogo não tinha) vira uma **peça solta**. No modelo
  ele é fundido no corpo, e com ossos a emenda esticava por cima da calça.
  O `soltar_rabo` acha a cauda (o pelo castanho ligado, aresta com aresta,
  ao que está bem para trás do corpo, até `TOPO_DO_RABO` = 0,93 m: acima
  disso é a franja das costas), corta a malha na divisa e tampa os dois
  lados (o corpo na cor da calça, o rabo na do pelo escuro). Ela ganha uma
  cadeia de 5 juntas, `rabo1` a `rabo5`, pela linha do meio (66 cm); a
  última é só a ponta. Só os primeiros 6 cm seguem o corpo (com os pesos
  do vértice gêmeo do outro lado do corte, para a raiz não abrir).

## `pintar.py` — as texturas dos dois times

A pelagem e os detalhes ficam como o Meshy pintou. Tudo é decidido pela
posição do texel no corpo (o atlas é picado em centenas de ilhas: vizinho
na imagem não é vizinho no lobo). O que muda:

- **Olhos inteiros de luz** (pedido de sempre do Jhon: "todo luminoso",
  sem ponto escuro): a amêndoa azul do modelo, 2 mm maior, com a borda na
  cor do time, o miolo quase branco e um clarão no contorno escuro.
- **Neon** na cor do time onde o modelo tem faixa azul: braceletes,
  peitoral e as tiras das costas. É emissão de verdade (`lobo_emissao`).
- **A roupa na cor do personagem** ("esses lobos são transformações de Eve
  e Barut"): o pano do quadril ao joelho em azul-ardósia (Eve) ou carmim
  escuro (Barut). A costura clara fica quase sem cor, para não virar risco.
- **O lobo do Barut é escuro** (a pelagem a 50%, mais contraste, um pouco
  quente); o da Eve é a pelagem do modelo. É o que separa os times de
  longe, junto com o neon e os olhos. Números em `TIMES`.
- **Sem cacos espelhados**: o mapa de metal do Meshy acende em lascas de
  pelo (cabeça, mãos, pés, rabo); pelo e pano saem sem metal e foscos.
- **Oclusão** assada, estendida à margem das ilhas (sem borda preta).
- **Cor de vértice** (máscaras de `src/assets/efeitos/lobo.gdshader`):
  R = quanto o vértice balança (as pontas do pelo; equipamento, mãos, pés
  e focinho não), G = pelo (brilho de contorno), B = 0,5 (tom neutro).

## `montar.py` — o GLB

O esqueleto do lobo original, mais os ossos do rabo (filhos do quadril, o
+Y de cada um apontando para a junta seguinte, em centímetros e sem
escala, como os outros ossos), uma malha só (`corpo`) e
as quatro texturas: cor e emissão e normal em 4096, ORM em 2048.

No jogo: `src/scripts/visual/wolf/WolfSkin.cs` troca o material pelo do
shader, `WolfFx.cs` manda o empurrão do movimento no pelo, e
`WolfVisual.Tail.cs` simula o rabo (mola, gravidade e inércia).

## O que saiu

O `polir.py` (18/11), que pintava por cima dos lobos antigos (três malhas,
tufos em malha), saiu com eles em 25/11; está no histórico do git.
