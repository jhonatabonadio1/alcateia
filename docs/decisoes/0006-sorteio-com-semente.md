# 0006 — Sorteio de regra com semente, um fluxo por dono

**Situação:** aceita (v0.27.0)

## Contexto

A dispersão do tiro, o recuo sorteado e todas as escolhas do bot usavam
`GD.Randf()`, o sorteio global do Godot. Isso tem dois problemas:

- um defeito que dependeu da sorte não volta para ser investigado;
- no multiplayer, o servidor e a predição do cliente precisam sortear a
  mesma dispersão — com um sorteio global, a ordem em que as coisas
  sorteiam muda tudo.

## Decisão

`GameRandom` (`scripts/domain`, SplitMix64) é o sorteio de regra. Cada
dono tem o seu fluxo, derivado da semente da partida e do nome do nó:
`MatchRandom.Stream("Bot3")`, `MatchRandom.Stream("Bot3/arma")`. Quem
não tem dono (pontos de nascimento, rotas) usa `MatchRandom.For(nome)`.

A semente vem do relógio, sai no Output no começo da partida, e
`-- --seed=N` a fixa (`Inicio`, autoload). Os testes de regressão usam
sempre a mesma.

Enfeite (o giro de uma arma no chão) pode continuar no `GD.Randf()`.

## Consequências

- Um tiro a mais de um bot não muda a sorte dos outros.
- `GameRandom.State` guarda e volta a posição na sequência: é o que a
  correção do servidor e o replay precisam.
- A física (Jolt) ainda não é determinística entre máquinas; a semente
  repete as decisões, não necessariamente cada posição.
