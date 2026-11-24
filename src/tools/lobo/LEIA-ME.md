# tools/lobo — o polimento dos lobos dos times

Pedido do Jhon (18/11): "mais AAA" — pelo com mais detalhe, cores mais
vivas, neon de verdade nos olhos, braceletes, peitoral, joelheiras e costas,
e o pelo mexendo no jogo. Só mexe nas **texturas** e soma dados à malha; o
esqueleto, os pesos e as três malhas (`corpo`, `roupa`, `equipamento`) saem
como entraram.

```
# 1. a oclusão assada (precisa do Blender como módulo: pip install bpy)
python3 tools/lobo/assar_ao.py lobo_azul_original.glb ao_azul.png
# 2. o polimento (numpy, scipy, pillow, matplotlib)
python3 tools/lobo/polir.py azul lobo_azul_original.glb ao_azul.png assets/personagens/eve/lobo_azul.glb --previa /tmp
python3 tools/lobo/polir.py vermelho lobo_vermelho_original.glb ao_vermelho.png assets/personagens/barut/lobo_vermelho.glb
# 3. conferir fora do Godot (Cycles): frente, costas, lado
python3 tools/lobo/render_glb.py assets/personagens/eve/lobo_azul.glb /tmp/lobo.png
```

**Sempre em cima do GLB original** (o do Meshy; no git, o de antes de
18/11: `git show 147a358:assets/personagens/eve/lobo_azul.glb > lobo_azul_original.glb`).
O arquivo polido fica marcado e o `polir.py` se recusa a rodar nele: a cor
seria aplicada duas vezes.

O que o `polir.py` faz:

- **Cor**: mais saturação e contraste por time; armadura e roupa um pouco
  mais claras (liam quase pretas).
- **Pelo pintado**: fios (ruído 3D esticado no sentido do pelo, contínuo de
  ilha em ilha da textura), mechas largas na juba e nas costas, raiz escura
  e ponta clara nos tufos, faixa escura na espinha. No vermelho entra bem
  menos: ele já estava no ponto.
- **Oclusão** assada: por vértice (a média do miolo dos triângulos em
  volta), porque o Cycles deixa um fio preto em toda aresta do Meshy. Vai
  na cor e no canal R do ORM.
- **Neon** (textura de emissão nova, `lobo_emissao`): o que já estava
  desenhado (peitoral, braceletes, ombreiras e joelheiras do vermelho, os
  olhos) e o que foi pedido: joelheiras e tornozeleiras do azul, e nas
  costas o **LED por baixo da armadura, vazando pelas bordas**. Só em
  armadura: neon em roupa parecia rasgo.
- **Olhos**: os do vermelho acesos como estavam; os do azul só o miolo de
  cada olho, na cor da própria textura, dentro de uma órbita escurecida
  (é o que dá a cara de bravo).
- **Cor de vértice** (máscaras para `assets/efeitos/lobo.gdshader`):
  R = quanto o vértice balança (pontas dos tufos; mãos, pés e focinho não),
  G = pelo (brilho de contorno), B = tom (0,5 neutro; raiz e ponta dos tufos).
- **Tufos pontudos** (malha nova `pelo`, só no azul e poucos — o vermelho já
  tem a quantidade certa): pirâmides de três lados deitadas no sentido do
  pelo, no corpo; na cabeça não (lascas finas ali ficaram estranhas).

No jogo, quem usa: `scripts/visual/wolf/WolfSkin.cs` troca o material de
cada malha pelo do shader (pelo balançando, neon que respira e acende com o
poder) e `scripts/visual/fx/WolfFx.cs` manda o empurrão do movimento.
