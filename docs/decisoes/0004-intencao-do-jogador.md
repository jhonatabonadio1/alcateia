# 0004 — O jogador lê a intenção uma vez por quadro

**Situação:** aceita (v0.27.0)

## Contexto

O `Player` lia o teclado em 17 lugares, em cinco métodos. Isso amarra
o movimento ao teclado local: não dá para mover o corpo a partir de um
pacote de rede, de uma gravação ou de um teste sem simular teclas.

## Decisão

No começo do passo de física o `Player` monta um `CharacterIntent`
(`scripts/actors/input/`) — direção, pulo, agachar, ataque, especial,
uivo, slot — e dali em diante só olha para ele. A rotação do mouse
continua por evento, no `_Input`.

## Consequências

- Multiplayer (Fase 2): o cliente manda a intenção de cada quadro; o
  servidor roda o mesmo `_PhysicsProcess` sobre ela. A predição do
  cliente é o mesmo código sobre a intenção local.
- Replay é uma lista de intenções com a semente da partida (ver 0006).
- Falta: a intenção ainda não carrega a mira (yaw/pitch); entra quando
  a rede chegar.
