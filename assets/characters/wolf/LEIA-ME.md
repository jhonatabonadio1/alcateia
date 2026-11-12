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
| `jump.fbx` | mutant jumping | no ar |
| `claw.fbx` | mutant swiping | garrada (clique) |
| `heavy.fbx` | mutant punch | patada (direito no chão) |
| `leap.fbx` | mutant jump attack | bote (direito no ar) |
| `howl.fbx` | mutant roaring | uivando (R) |
| `transform.fbx` | mutant flexing muscles | acabou de virar lobo |
| `death.fbx` | mutant dying | morreu de lobo |
| `idle_alerta.fbx` | mutant idle | reserva, não usado |

Clipe com deslocamento de raiz tem o deslocamento horizontal zerado na
carga (`CharacterAnimator.StripRootMotion`): quem move o corpo é a
física.

Para trocar uma animação, ponha o `.fbx` do Mixamo aqui com o mesmo
nome. Clipe que falta é pulado sem erro.

**Licença:** Mixamo (Adobe) — uso livre no jogo, inclusive comercial;
não redistribuir os arquivos crus.
