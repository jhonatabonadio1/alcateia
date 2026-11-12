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
| `heavy.fbx` | mutant punch | a outra garrada do clique; patada (direito no chão) |
| `leap.fbx` | mutant jump attack | bote (direito no ar) — **só a queda** |
| `howl.fbx` | mutant roaring | uivando (R) |
| `transform.fbx` | mutant flexing muscles | acabou de virar lobo |
| `death.fbx` | mutant dying | morreu de lobo |
| `idle_alerta.fbx` | mutant idle | reserva, não usado |

**O lobo não tem animação de pulo** (decisão do Jhon): no ar o corpo
segue na pose em que estava. Pelo mesmo motivo, o bote usa só a queda
do "jump attack" — do ponto mais alto até ficar de pé de novo —, já
que ele começa com o lobo no ar (`WolfVisual.FallOnly`).

Clipe com deslocamento de raiz tem o deslocamento horizontal zerado na
carga (`CharacterAnimator.StripRootMotion`): quem move o corpo é a
física.

Para trocar uma animação, ponha o `.fbx` do Mixamo aqui com o mesmo
nome. Clipe que falta é pulado sem erro.

**Licença:** Mixamo (Adobe) — uso livre no jogo, inclusive comercial;
não redistribuir os arquivos crus.
