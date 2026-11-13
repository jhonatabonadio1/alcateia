# 0003 — Jogador e bot dividem o corpo

**Situação:** aceita (v0.27.0)

## Contexto

O bote, o voo do salto, a parada depois do voo, o lobo preso ao chão,
o ruído dos passos e o cadáver tinham uma cópia no `Player` e outra no
`Bot`. As cópias já tinham divergido: o bote do bot contava qualquer
parede como impacto, o pouso do salto do bot não fazia barulho. Cada
correção precisava ser feita duas vezes, e uma das vezes era esquecida.

## Decisão

O que é do **corpo** — e não de quem decide — mora em
`scripts/actors/body/` e os dois usam o mesmo objeto:

- `WolfMoveMotor`: o corpo enquanto a arma está com ele.
- `BodyNoise` + `BodyNoiseProfile` (`resources/body/ruido.tres`).
- `Corpse`: cair e assentar morto.
- `BodyMotion.Hold`: parado com gravidade.

São classes C# simples (não nós): o ator cria no `_Ready` e chama no
passo de física.

## Consequências

- Diferença entre jogador e bot no corpo agora é decisão explícita (um
  parâmetro), não acidente de cópia.
- As divergências antigas foram resolvidas a favor da regra do jogador
  (CHANGELOG v0.27.0).
- Próximo candidato: o movimento no chão. O bot anda pela malha de
  navegação e o jogador pela intenção — juntar os dois passa por o bot
  também produzir uma intenção (ver 0004).
