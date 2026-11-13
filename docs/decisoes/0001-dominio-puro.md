# 0001 — Regras de jogo em C# puro, com teste de unidade

**Situação:** aceita (v0.27.0)

## Contexto

As contas do combate (dano com queda por distância, tiro na cabeça,
dispersão, recuo, garra do lobo, salto carregado, escudo) estavam
espalhadas dentro de `Weapon` e `Health`, misturadas com física, nós e
sinais do Godot. Para conferir uma conta era preciso abrir o jogo,
achar a situação e medir. Mudar uma fórmula sem querer só aparecia
jogando.

## Decisão

Tudo o que é **regra** — número que sai de número — mora em
`scripts/domain/`, em C# puro: sem `using Godot`, sem estado, sem
sorteio escondido. `CombatRules`, `WolfMoves` (o tempo dos golpes) e
`GameRandom` são os primeiros.

Os componentes do Godot (`Weapon`, `Health`) decidem **o que
aconteceu** (acertou a cabeça? estava agachado?) e pedem o número ao
domínio. Os pesos (os `[Export]`, os `.tres`) continuam com eles e
entram como parâmetro.

`tests/unit` compila só essa pasta, sem Godot e sem pacote NuGet:
roda em segundos, até sem internet.

## Consequências

- Uma fórmula nova vai para o domínio com o teste junto.
- O domínio não pode depender de nada do Godot. Se precisar de um
  vetor, use `System.Numerics` ou números soltos.
- O servidor dedicado (Fase 2) reaproveita esta pasta inteira.
