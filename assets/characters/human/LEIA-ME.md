# Humano — Mixamo Basic Shooter Pack

`xbot.fbx` é o personagem (malha + esqueleto). Os outros 16 são
clipes, um por arquivo, todos sobre o **mesmo rig `mixamorig:` de 65
ossos** — foi conferido osso por osso, então misturam entre si.

Todos os takes se chamam `mixamo.com` dentro do arquivo. O nome real
do clipe é o nome do arquivo.

## Duração e deslocamento de raiz, medidos no arquivo

| Arquivo | Duração | Deslocamento da raiz |
|---|---|---|
| `idle_rifle` | 3,07 s | — |
| `walk_fwd` | 1,37 s | 136 cm à frente (≈1,0 m/s) |
| `walk_back` | 1,40 s | 154 cm atrás (≈1,1 m/s) |
| `run_fwd` | 0,70 s | 221 cm à frente (≈3,2 m/s) |
| `run_back` | 0,50 s | 138 cm atrás (≈2,8 m/s) |
| `strafe_right` | 0,97 s | 134 cm em +X |
| `strafe_right_alt` | 0,53 s | 151 cm em +X |
| `strafe_left` | 1,43 s | 108 cm em −X |
| `strafe_left_alt` | 0,67 s | 221 cm em −X |
| `jump` | 0,60 s | — (sobe 32 cm) |
| `fire` | 0,27 s | — |
| `reload` | 3,30 s | — |
| `grenade` | 3,00 s | — |
| `hit` | 0,43 s | — |
| `turn_left` | 0,90 s | — (giro no lugar) |
| `turn_right` | 1,00 s | — (giro no lugar) |

## Dois cuidados

**Os strafes vinham com o nome trocado.** O arquivo que o Mixamo
chamava de `strafe left` anda em +X e o `strafe right` anda em −X. Os
nomes aqui seguem a **medição**, não o rótulo original. Como a
conversão de eixo do importador pode inverter Z, se no jogo o strafe
sair espelhado, basta inverter `InvertStrafe` no animador — os pares
estão corretos entre si, só o rótulo esquerda/direita é que depende
do eixo final.

`strafe_*_alt` são o segundo clipe de cada lado. Ficam de reserva:
escolha o que parecer melhor em movimento e descarte o outro.

**Oito clipes andam de verdade** (os de locomoção). Nosso
`CharacterBody3D` já calcula o deslocamento, então animação que
também anda briga com ele e o personagem patina. A trilha de posição
do Hips é removida no carregamento, no `CharacterAnimator`.

## Segunda leva

Catorze arquivos a mais, todos do **mesmo rig** (65 ossos, assinatura
conferida contra o xbot), então misturam com os primeiros sem
retargeting.

| Vira | Veio de | Duração |
|---|---|---|
| `death` | Walking To Dying | 3,07 s |
| `crouch_idle` | Idle Crouching | 2,10 s |
| `fall` | Jump Loop | 1,00 s |
| `knife` | Stabbing | 2,63 s |
| `equip` | Rifle Pull Out | 2,03 s |
| `knife_idle` | Knife Idle | 4,47 s |

Os `*_alt` são alternativas medidas e descartadas por ora, guardadas
porque trocar é renomear: `idle_rifle_alt` (10,63 s, idle longo com
mais variação), `fire_alt` (1,03 s — longo demais para a cadência de
um rifle), `reload_alt` (6,87 s, idem), `reload_moving_alt` (recarga
andando), `knife_alt`, `jump_up_alt` (só o impulso, sem o arco),
`death_kneel_alt` (morre ajoelhado caindo para trás) e
`kneel_stand_alt`.

Treze dos catorze vieram **com malha** — cada um carrega uma cópia do
Beta_Surface. Não quebra nada, porque o carregador arranca a animação
e descarta a cena, mas é peso morto no disco.

## O que ainda falta

`crouch_walk` e `land`. O jogo agacha andando e já toca ruído de
pouso; os dois estão listados no animador, então basta soltar o
arquivo com esse nome.
