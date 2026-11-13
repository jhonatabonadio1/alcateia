# 0005 — Números de ajuste são dado (.tres), não código

**Situação:** aceita (v0.27.0)

## Contexto

Armas e classes de lobo já eram `.tres`. Os níveis de dificuldade eram
um `switch` de 24 atribuições por nível em `Difficulty.cs`, e os raios
de ruído eram cinco `[Export]` repetidos no `Player` e no `Bot`. Ajustar
exigia recompilar ou mudar dois lugares.

## Decisão

Número que alguém vai querer ajustar sem programar é um `Resource`
com `[GlobalClass]` e um `.tres` em `resources/`:

- `resources/weapons/`, `resources/breeds/` (já eram);
- `resources/bots/facil|medio|dificil.tres` (`BotDifficulty`);
- `resources/body/ruido.tres` (`BodyNoiseProfile`).

O código guarda o padrão (o valor do script) e as **regras** entre os
números viram teste, não comentário: o teste `Dificuldade` não prende
os valores, prende que cada nível é mais competente que o anterior.

## Consequências

- O guia `docs/AJUSTES.md` aponta o arquivo de cada número.
- Um `.tres` que falte não derruba a partida: `Difficulty.Preset` avisa
  e usa o padrão.
- Ainda no código: o tempo dos golpes (`WolfMoves`, que o teste de
  unidade cobre) e os pesos do cérebro do bot.
