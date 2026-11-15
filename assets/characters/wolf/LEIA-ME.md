# Lobo — Mixamo Mutant + Creature Pack

`mutant.fbx` é o lobo do time azul; `mutant_red.fbx`, o do time
laranja (veio do Blender: sem `AnimationPlayer` e com o esqueleto em
`Armature` — o `WolfVisual` cria o tocador ao lado do esqueleto). Os
dois têm o mesmo esqueleto `mixamorig` de 37 ossos.

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
salva como repouso. O `WolfVisual` acerta isso na carga (`FitSprint`):
aponta as trilhas para o esqueleto do Mutant, segura no repouso do clipe
os ossos que ele não anima, ajusta o quadril às pernas do Mutant. Trocou
o arquivo? Meça de novo a passada (`SprintClipSpeed`) e o apoio no chão
(`SprintHipsLift`).

**Licença:** Mixamo (Adobe) — uso livre no jogo, inclusive comercial;
não redistribuir os arquivos crus.
