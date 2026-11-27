# 0015 — Tudo o que vale alguma coisa é da conta; o jogo só espelha

## Contexto

Depois da decisão 0013 o servidor já guardava carteira, inventário e
desafios, mas o jogo continuava com os dele num arquivo na máquina do
jogador (`user://profile.json`): havia duas carteiras, e a do arquivo
qualquer um edita. Jhon decidiu (20/11): **"tudo exige conta"** — loja,
inventário e missões só existem com conta no servidor, e o arquivo
local deixa de guardar Presas e itens.

## Decisão

- **XP, patente, Presas, Sangue, inventário, missões e títulos são da
  conta.** No jogo, `Profile` é um espelho em memória do que o servidor
  disse (`Profile.ApplyAccount`); nada disso vai para o disco. Sem
  conta, é zero, Filhote e só o equipamento de série.
- **O arquivo local guarda só escolhas**: nome de exibição, conjuntos,
  classe e personagens escolhidos, melhorias de lobo, título em uso,
  estatísticas de treino, a última sala. Arquivo de versão antiga é
  lido e o que era carteira e inventário é ignorado (as contas de teste
  não valiam nada; nada foi migrado).
- **Um caminho só entre o jogo e a conta: `AccountSync`.** Entrar,
  retomar o login guardado, comprar, descartar, resgatar missão e
  trocar de nome passam por ele, que faz a chamada e relê a conta
  (`GET /v1/inventory` + `GET /v1/challenges`). Carteira e itens chegam
  juntos: assim nenhum conjunto é desfeito por um inventário que ainda
  não veio.
- **As telas seguem a conta** (`Profile.AccountChanged`,
  `LobbyScreen.FollowsAccount`). Sem conta, a loja mostra a vitrine mas
  o botão é "Entrar na conta para comprar"; as missões mostram o
  convite para entrar (`AccountGate`).
- **Partida sem servidor é treino**: não rende XP, Presas nem missão, e
  a tela de resultado diz isso. Na partida online a tela de resultado
  mostra a **previsão** (a mesma conta de `Rewards.cs`, que o servidor
  também usa); quem paga é o servidor quando recebe o resultado do
  servidor de partida, e o jogo relê a conta em seguida.
- **Conjunto guardado não se perde por estar sem conta**: o que não é
  do jogador entra na partida como o item de série (`Profile.Usable`),
  e só é tirado do conjunto quando a conta está carregada e mostra que
  o item não é dele.
- **O que mudava com o relógio do jogador passou para o servidor**
  (`GameClock`, dia de Brasília):
  - o **rodízio das missões** — três diárias e cinco semanais sorteadas
    do que está ligado, as mesmas para todo mundo (`Challenges.Running`;
    os números são a configuração `desafios`);
  - o **item do dia** — quatro itens com 20% (`Shop.DailyShowcase`,
    configuração `vitrine`), que entra no preço da compra como qualquer
    promoção;
  - o **prêmio de promoção de patente** (`RankRule.PromotionReward`),
    pago na mesma transação que deu o XP, com linha no extrato.
- **Descartar item e trocar de nome são do servidor**
  (`DELETE /v1/inventory/{id}`, `POST /v1/account/name`): a troca de
  nome confere se está livre e gasta um uso do item `troca_nome` na
  mesma transação.
- O catálogo do jogo continua sendo a lista de itens que existem (arte,
  números de combate); preço, raridade, patente mínima, destaque e
  "vem com a conta" são sobrescritos pelo que o servidor manda
  (`Catalog.ApplyServer`) e voltam ao que está no código se o servidor
  some (`Catalog.RestoreBuiltIn`).

## O que custa

- **Sem servidor não há progressão.** Quem joga só offline tem o
  equipamento de série e nenhuma missão. É o preço de a economia não
  ser falsificável.
- Abrir o lobby faz três pedidos (`/v1/profile`, `/v1/inventory`,
  `/v1/challenges`) e cada compra relê a conta. Nenhum deles acontece
  durante a partida: o jogo não fala com o servidor de contas enquanto
  se joga.
- A previsão da tela de resultado pode divergir do que o servidor paga
  (resultado recusado, evento de bônus que virou no meio): vale o
  servidor, e a carteira mostra o número dele assim que chega.
- O item expirado some do inventário na hora (o servidor só devolve o
  que está valendo); antes ficava 30 dias à mostra para recomprar.
- As estatísticas da tela de perfil ainda são as locais (contam treino
  também); as da conta estão em `/v1/profile` e no painel.
- Enquanto a vida e o acerto forem decididos nas máquinas dos jogadores
  (decisão 0008), o resultado que paga ainda pode ser forjado por quem
  joga: o servidor só desconfia (`Plausibility`). É o que a leva
  seguinte da decisão 0011 resolve.
