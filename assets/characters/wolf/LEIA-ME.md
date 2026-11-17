# Lobo — o lobo do Meshy + Creature Pack

**Desde 17/11 os lobos dos times são `lobo_azul.glb` (azul) e
`lobo_vermelho.glb` (laranja)**: os dois do Jhon, no Meshy, mesmo
esqueleto de 24 ossos e 1,66 m, em três malhas (`corpo`, `roupa`,
`equipamento`) com uma textura. As placas dos braços e das pernas são
rígidas (um osso por peça); as do tronco (arnês, peitoral, cinto) seguem
a pele, porque o tronco dobra em três ossos e uma placa dura afundava
nas costas. Embaixo de toda peça há corpo. Cópias do mesmo vértice têm
os mesmos pesos, no máximo 4 ossos: é o que impede a malha de abrir. O texto abaixo é do lobo
anterior (`mutant*.fbx`), que ficou como referência de altura.

`mutant.fbx` é o lobo do time azul; `mutant_red.fbx`, o do time
laranja. Desde 15/11 os dois são o **lobo tático que o Jhon fez no
Meshy** (o nome `mutant` ficou para não mexer no resto): o mesmo modelo,
o laranja com a pelagem em ferrugem. Vêm com um esqueleto `mixamorig` de
24 ossos (sem dedos, com um osso extra `headfront`), em pose A (braços
caídos), em centímetros dentro de um nó de escala 0,01, com os pesos já
feitos, e as texturas de 2048 px embutidas (o Meshy dá 4096; reduzidas
para o arquivo ter 18 MB e não 53). Sem `AnimationPlayer`: o `WolfVisual`
cria o tocador ao lado do esqueleto.

**As animações não são mexidas: o jogo as adapta ao corpo** (`WolfVisual
.FitClip`, `AnimationKit.RetargetPose`). Os clipes são do T do Mutant e
este corpo está em A, com os eixos dos ossos girados de outro jeito, então
copiar o giro dos ossos dobrava os braços errado. Em vez disso o clipe
mantém a **direção de cada osso no mundo**: o giro que o osso faz do
repouso do clipe (o esqueleto do `walk.fbx`) para a pose vale também
para o osso do lobo, depois de ele ser girado do repouso dele para o do
clipe. O quadril sobe na razão das alturas (1,0 m do lobo, 0,93 m do
Mutant). Trocar o lobo por outro com os mesmos nomes de osso não exige
refazer clipe nenhum; só medir de novo o apoio no chão (`PassadaDoLobo`)
e a cápsula.

Para um outro lobo do Meshy: baixe o FBX **rigado** (Mixamo, "character
mixamo"), reduza as texturas (`tools/` não tem script: no Blender, dê
`scale(2048, 2048)` nas imagens e exporte FBX com as texturas
embutidas) e ponha aqui com o mesmo nome. O time laranja é o mesmo
arquivo com a cor da pelagem trocada.

**Animações do Jhon (`lobo_anim.glb`, 15/11).** Vêm do
`Wolf_animations.glb` que o Jhon animou no próprio lobo do Meshy
(esqueleto de 24 ossos, escala 0,01 no `Armature`). O arquivo do jogo
guarda só o esqueleto e os 15 clipes (691 KB): malha, textura e material
saem, mantendo a pele, sem a qual o Godot não cria o esqueleto. O Godot tira o `_Loop` do nome e põe o clipe em laço.

| Clipe | Papel no jogo | Como toca |
|---|---|---|
| `Idle_Pose` | parado (e a vitrine do lobby) | 1× |
| `Walk_Forward`, `Walk_Backward`, `Walk_Left`, `Walk_Right` | trote curvado em pé, nas quatro direções, de frente para a mira (ciclo de 0,67 s) | na velocidade do corpo, perto de 1× (feitos na velocidade do jogo) |
| `Crouch_Idle` | agachado parado | 1× |
| `Crouch_Walk` | andando agachado | na velocidade do corpo |
| `Run_Quad` | corrida de quatro (W duas vezes) | na velocidade do corpo (a 5,4 m/s, 1,37×); o som do passo sai quando as patas se juntam (0,567 s do clipe) |
| `Attack_Primary`, `Attack_Secondary` | as duas garradas | 1×, da cintura para cima |
| `Howl` | rugido (R) | 1×, de corpo inteiro; o lobo para para rugir |
| `Breath_Add` | respiração do lobo encolhido na carga do salto | em laço, somada à pose do agachar (é só a diferença entre o `Idle_Pose` e a pose parada, 5,5× mais funda) |
| `Charge_Walk` | andando com o salto carregado | em laço, na velocidade do corpo (0,91 m/s a 1×); agachado na pose em que a carga segura |
| `Special_Attack` | salto (direito no chão), bote (direito no ar) e a aterrissagem do bote | 1×; trechos em `WolfMoves.cs` |
| `Jump_Start`, `Jump_Air`, `Jump_Land` | pulo: impulso, voo em laço e pouso | 1× |

O `WolfOwnClips` os encaixa no corpo do jogo: tira escala e posição que
não são do quadril, converte o quadril para a altura do corpo (mantendo
o vaivém dele dentro do ciclo: os clipes são no lugar) e corrige
a diferença de eixos (`RetargetPose` com `referenceFrame` e
`bodyFrame`). Cada um vale no lugar do `.fbx` do Mixamo de mesmo papel;
só a morte, a corrida na parede (e o `run`, o `jump` e o `sprint`, de
reserva se o `lobo_anim.glb` faltar) continuam nos `.fbx` abaixo. Para
trocar um clipe, exporte o GLB com os mesmos nomes e meça de novo a
passada (`PassadaDoLobo`) e os tempos dos golpes (`WolfMoves.cs`).

**Revisão de 15/11 à noite.** Os 15 clipes foram refeitos com os mesmos nomes:
parado sem o quadril deslizar; os quatro andares viraram um trote curvado
feito na velocidade do jogo, com o pé de apoio a velocidade constante;
`Crouch_Walk` com ciclo de 1,0 s; garradas com o tronco girando e a
pisada do lado oposto (acertam em 0,20 s e duram 0,733 s, como antes);
`Howl` com as pernas (1,533 s, como antes). `Run_Quad`, `Special_Attack`
e os três do pulo mantêm duração e momentos. As passadas novas estão em
`WolfVisual.Locomotion.cs`, calculadas do arquivo e conferidas na
varrida do `PassadaDoLobo` (patinação: de frente 0,13 m/s, de lado 0,14,
para trás 0,22, engatinhando 0,23). **O
lobo azul veio com 1,80 m e o vermelho com 1,66 m: os dois foram
normalizados para 1,66 m.**

Os clipes são do **Creature Pack** do Mixamo, renomeados pelo papel
que fazem no jogo:

| Arquivo | Original | Quando toca |
|---|---|---|
| `idle.fbx` | mutant breathing idle | parado |
| `walk.fbx` | mutant walking | andando |
| `run.fbx` | mutant run | correndo |
| `claw.fbx` | mutant swiping | garrada (clique), alternando com o punch |
| `heavy.fbx` | mutant punch | a outra garrada do clique |
| `leap.fbx` | mutant jump attack | salto (direito no chão), inteiro; bote (direito no ar), a queda e a aterrissagem; agachado parado (o fim da aterrissagem, 1,95 a 2,25 s) |
| `howl.fbx` | mutant roaring | uivando (R) |
| `transform.fbx` | mutant flexing muscles | não usado: virar lobo não tem animação (decisão do Jhon) |
| `jump.fbx` | mutant jumping | pulo: só o voo, tocado pela velocidade vertical; a aterrissagem quando chega devagar |
| `death.fbx` | mutant dying | morreu de lobo |
| `wallrun.fbx` | Running Crawl | correndo na parede, deitado de lado com a barriga para ela; engatinhando no chão, agachado e andando |
| `sprint.fbx` | Quadruped Run (editado pelo Jhon no Blender) | corrida de quatro (W duas vezes) |
| `idle_alerta.fbx` | mutant idle | reserva, não usado |

**O pulo usa só o voo do `jump.fbx`** (de 1,27 a 1,87 s do arquivo):
o clipe agacha antes de sair do chão, e no jogo a física sai no quadro
do clique — tocado inteiro, o bicho pularia duas vezes. O voo anda pela
velocidade vertical do corpo, não pelo relógio: o topo do clipe cai no
topo do pulo, seja qual for a altura. O `jump.fbx` é importado sem
extrair as texturas (`fbx/embedded_image_handling=0`): só a animação
interessa, e as texturas repetiriam 16 MB das do `wallrun`. Caindo de
uma beirada, sem pular, o corpo segue na pose em que estava. O `leap.fbx` ("jump attack") serve três
clipes: inteiro no salto (direito no chão), só a queda no bote (direito
no ar, que começa com o lobo já no ar — `FallOnly`) e, quando o bote
encosta, a aterrissagem.

Clipe com deslocamento de raiz tem o deslocamento horizontal zerado na
carga (`AnimationKit.StripRootMotion`): quem move o corpo é a
física.

**Golpes e rugido tocam um trecho medido**, não o arquivo inteiro: a
garrada pula a preparação longa, o bote usa só a queda, o rugido corta
o começo e o fim parados. Os trechos, as velocidades, o instante do
golpe e o passo de cada clipe ficam em `scripts/forms/WolfMoves.cs`, e
a arma usa os mesmos números para o dano sair no quadro em que a garra
chega. Trocou um desses clipes? Meça de novo com
`godot --headless --path . -s res://tools/analisar_golpes.gd` (mãos,
passo do quadril, pés tocando o chão, trecho com movimento) e corrija a
linha dele.

`wallrun.fbx` veio com a malha do Mutant junto (19 MB); só a animação é
usada. Da próxima vez, baixe do Mixamo "without skin".

Para trocar uma animação, ponha o `.fbx` do Mixamo aqui com o mesmo
nome. Clipe que falta é pulado sem erro.

**O `sprint.fbx` não é do Creature Pack:** é do esqueleto do X Bot e veio
do Blender, com o esqueleto dentro de `Armature` e a pose de quatro
salva como repouso. O `WolfVisual` acerta isso na carga (`FitClip`):
aponta as trilhas para o esqueleto do lobo, segura no repouso do clipe
os ossos que ele não anima, reorienta os ossos ao lobo e ajusta o quadril
às pernas dele (`SprintHipsLift` vem em metros e é convertido para as
unidades do esqueleto). Trocou
o arquivo? Meça de novo a passada (`SprintClipSpeed`) e o apoio no chão
(`SprintHipsLift`).

**Licença:** Mixamo (Adobe) — uso livre no jogo, inclusive comercial;
não redistribuir os arquivos crus.
