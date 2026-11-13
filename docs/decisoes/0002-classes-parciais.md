# 0002 — Classes grandes divididas em arquivos parciais por assunto

**Situação:** aceita (v0.27.0)

## Contexto

`Player` (1580 linhas), `Bot` (1650), `Weapon` (1500), `CharacterAnimator`
(1350), `WolfVisual` (1020) e `Hud` (855) eram um arquivo cada. Achar
algo era rolar; dois assuntos no mesmo arquivo viravam conflito de
merge quando a equipe crescer.

Quebrar cada uma em vários nós (componentes) seria o caminho "de
livro", mas muda as cenas, os caminhos de nó que o código usa
(`GetNode("Weapon")` é contrato) e os valores salvos no Inspector — um
risco grande sem ganho de jogo.

## Decisão

Cada classe grande vira **uma classe parcial em vários arquivos**,
`Classe.Assunto.cs`, numa pasta com o nome dela (`actors/player/`,
`combat/weapon/`, `visual/human/`, `visual/wolf/`, `ui/hud/`). O arquivo
principal guarda o ciclo (`_Ready`, `_PhysicsProcess`) e diz no topo
quais são as partes. Cada parte abre o seu `[ExportGroup]`, então o
Inspector mostra os campos agrupados do mesmo jeito.

As divisões foram feitas por um script que move membros inteiros e
confere, linha a linha, que nenhuma linha de código mudou.

## Consequências

- As cenas e os `.tres` não mudaram; só o caminho do script principal.
- Parcial não isola estado: um campo declarado num arquivo é visível em
  todos. A disciplina é declarar o campo na parte que é dona dele.
- Quando um assunto ganhar vida própria (o motor do lobo ganhou), ele
  sai da parcial para uma classe de verdade — ver 0003.
