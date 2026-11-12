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
| `leap.fbx` | mutant jump attack | salto (direito no chão), inteiro; bote (direito no ar), a queda e a aterrissagem |
| `howl.fbx` | mutant roaring | uivando (R) |
| `transform.fbx` | mutant flexing muscles | não usado: virar lobo não tem animação (decisão do Jhon) |
| `jump.fbx` | mutant jumping | pulo: só o voo, tocado pela velocidade vertical; a aterrissagem quando chega devagar |
| `death.fbx` | mutant dying | morreu de lobo |
| `wallrun.fbx` | Running Crawl | correndo na parede, deitado de lado com a barriga para ela |
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
carga (`CharacterAnimator.StripRootMotion`): quem move o corpo é a
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

**Licença:** Mixamo (Adobe) — uso livre no jogo, inclusive comercial;
não redistribuir os arquivos crus.
