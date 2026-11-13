# 0007 — Duas camadas de teste, e o teste muda junto com a regra

**Situação:** aceita (v0.27.0)

## Contexto

O jogo era verificado jogando. Cada ajuste de movimento ou de golpe
podia quebrar outro (a passada do lobo, a distância do salto) sem
ninguém ver até a próxima partida.

## Decisão

- **Unidade** (`tests/unit`): C# puro sobre `scripts/domain`, com um
  executor próprio de 100 linhas — sem xUnit, porque o ambiente nem
  sempre alcança o NuGet. Segundos.
- **Regressão** (`tests/regression`): o jogo de verdade, sem tela, a 60
  quadros fixos, um processo por teste. Mede o que o jogador sente:
  velocidade, altura, distância do salto, a passada do pé, o dano, o
  HUD. Um minuto e meio.
- `tools/testar.sh` roda os dois; o GitHub roda a cada push.
- Um teste falha também se aparecer `ERROR` no Output.

**Regra:** mudou um comportamento de propósito, mude o número esperado
no teste **no mesmo commit**, dizendo por quê. Teste quebrado sem
mudança pedida é defeito.

## Consequências

- Antes de refatorar algo sem cobertura, escreve-se o teste primeiro
  (a corrida na parede ganhou `SaltoDaParede` e `AgarraNaParede` antes
  de ser dividida).
- Teste de regra (níveis de dificuldade) prende a relação, não o
  número, para não travar o ajuste fino.
- `BotsAtiram` e `LobosUsamEspeciais` são fumaça: contam se acontece,
  com margem larga, porque uma partida de bots varia muito.
