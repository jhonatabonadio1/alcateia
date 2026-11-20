# 0013 — Economia e conteúdo moram no servidor; o painel edita, o jogo lê

## Contexto

Preço, raridade, patente mínima, que mapas estão em rotação e quais são
as missões estavam no código do jogo (`Catalog.cs`, `Maps.cs`,
`Missions.cs`), e a carteira e o inventário, num arquivo na máquina do
jogador. Mudar um preço pedia versão nova; e arquivo local qualquer um
edita.

## Decisão

- O **banco do servidor** é o dono de: catálogo (a parte de economia),
  mapas (ligado ou não), promoções, desafios, configurações, carteira
  (Presas e Sangue), inventário, extrato e progresso nos desafios.
- O servidor sobe com uma **semente** tirada do código do jogo
  (`tools/servidor/exportar_dados.py` → `Alcateia.Infra/Semente/jogo.json`)
  e põe no banco só o que ainda não existe. Depois disso quem manda é o
  painel: a semente nunca sobrescreve o que foi editado.
- **O que é do jogo continua do jogo**: a arte dos itens, a foto dos
  mapas, os modos para que cada mapa foi feito e quais modos existem
  vêm da semente a cada subida, mesmo em banco já povoado. O painel só
  anuncia modo liberado.
- O jogo pede tudo de uma vez (`GET /v1/gamedata`) com `ETag`: sem
  mudança, 304. No servidor esses dados ficam em memória
  (`GameDataService`) e só são relidos quando o painel muda algo — saber
  um preço ou se um mapa está ligado nunca vai ao banco.
- **Compra é do servidor**: o jogo diz item e duração; preço, desconto,
  patente e saldo são conferidos numa transação que trava a carteira.
- **Desafio anda com o resultado da partida** (que vem do servidor de
  partida), nunca com o que o cliente diz; o prêmio sai uma vez por
  período.
- **Sangue (a moeda paga) nunca compra poder**: só personagem e serviço
  podem custar Sangue (`Shop.SangueAllowed`), e a regra recusa o resto
  mesmo vindo do painel.
- Os **números de combate** das armas (dano, cadência, recuo) continuam
  no jogo (`.tres`, decisão 0005): o painel cuida da economia, não do
  balanceamento.

## O que custa

- Item, mapa ou missão nova no jogo pede rodar o exportador e subir o
  servidor de novo (ele acrescenta o que falta).
- A carteira do arquivo local acabou na decisão 0015: o jogo só
  espelha a conta.
- A conta da patente existe em dois lugares (`Ranks.cs` no jogo,
  `RankRule.cs` no servidor): mudou num, mude no outro — há teste.
