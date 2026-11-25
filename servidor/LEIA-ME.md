# Servidor do Alcateia

ASP.NET (.NET 8) + PostgreSQL, sem pacote de fora. Como subir, como as
peças se encaixam e o que falta: `docs/SERVIDOR.md`. O painel
administrativo: `docs/PAINEL.md`. A segurança: `docs/SEGURANCA.md`. Aqui
fica a referência do que o servidor fala.

Organizado em camadas (clean architecture): cada uma só conhece as de
dentro, e a regra não sabe de banco nem de HTTP.

```
Alcateia.Dominio/        as regras, puras (sem banco, rede ou ASP.NET)
  Failure.cs             "não dá": tipo, código e frase (a API traduz em HTTP)
  Contas/Account.cs      conta, nome e senha válidos, perfil, ranking
  Salas/Room.cs          a sala: entrar, times, dono, bots, regras, começar
  Partidas/Match.cs      escalação, resultado, quem ganha e quanto (Rewards.cs, do jogo)
  Partidas/Plausibility  o que um resultado tem de estranho (alerta de trapaça)
  Frota/Fleet.cs         servidores de partida: lugares, entrega, partidas fechadas
  Loja/Shop.cs           catálogo, promoção, preço, validade — "Sangue não compra poder"
  Desafios/Challenge.cs  desafios: métrica, período (dia e semana de Brasília), progresso
  Seguranca/             faixa de IP (CIDR), eventos, padrões de ataque, TOTP
  Painel/Admin.cs        papéis e permissões do painel, regras de conta
  Contas/RankRule.cs     a patente a partir do XP (a mesma conta do jogo) e o prêmio de promoção
  GameClock.cs           o dia do jogo (Brasília): quando viram missões e vitrine
Alcateia.Aplicacao/      os casos de uso
  Portas/Ports.cs        o que a aplicação pede de fora: repositórios, senha, relógio, conexão
  Contas/AccountService  criar conta, entrar, sair, perfil, ranking
  Saguao/LobbyService    quem está ligado, salas e chat (o protocolo do WebSocket)
  Frota/FleetService     a frota em uso (trava, relógio, partida perdida)
  Partidas/MatchService  recebe o resultado, grava, paga, libera a sala, anda os desafios
  Jogo/GameDataService   catálogo, mapas, promoções, desafios e configurações, em memória
  Jogo/ShopService       a compra, conferida no servidor
  Jogo/ChallengeService  progresso e prêmio dos desafios
  Seguranca/SecurityService  o escudo: blacklist em memória, eventos em lote, detecção, bloqueio automático
  Painel/                login do painel (TOTP), jogadores, conteúdo, visão geral, auditoria
Alcateia.Infra/          o mundo de fora
  Banco/Pg.cs            cliente do PostgreSQL (protocolo de fio)
  Banco/Schema.cs        migrações
  Repositorios/          contas, partidas, jogadores (carteira, inventário), dados do jogo,
                         segurança (blacklist, eventos, banimentos) e painel no PostgreSQL
  Seguranca/Passwords.cs senha (PBKDF2), tokens, assinatura dos ingressos, relógio
  Seguranca/Crypto.cs    AES-256-GCM para o que não pode ficar legível no banco
  Seguranca/JsonSecurityLog  cada evento de segurança como uma linha de JSON na saída
  Semente/               o que vem com o jogo (jogo.json, de src/tools/servidor/exportar_dados.py)
Alcateia.Servidor/       a borda (o programa)
  Program.cs, App.cs     a montagem de tudo (raiz da composição)
  Config.cs              variáveis de ambiente
  Http/                  JSON, erros, chave da frota, freio de tentativas,
                         o escudo (Shield), o endereço real (ClientIp), o painel (AdminApi, PanelFiles)
  Rotas/                 contas, saguão (WebSocket), frota, partidas, saúde,
                         dados do jogo e loja (GameRoutes), e o painel (Admin*Routes)
painel/                  o painel administrativo: React + Tailwind + componentes no estilo shadcn
producao/                Caddy (HTTPS), Prometheus, alertas, Promtail, Grafana
Alcateia.Servidor.Testes/   testes de integração (src/tools/testar_servidor.sh)
Alcateia.Carga/             teste de carga: milhares de jogadores e partidas (src/tools/testar_carga.sh)
Dockerfile                  a imagem do servidor
partida.Dockerfile          a imagem do servidor de partida (o servidor dedicado exportado)
```

Rodar fora do Docker, com um PostgreSQL à mão:

```
ALCATEIA_DB_HOST=127.0.0.1 ALCATEIA_DB_PASSWORD=... ALCATEIA_FLEET_KEY=... \
  dotnet run --project servidor/Alcateia.Servidor
```

## HTTP

Corpo e resposta em JSON. Erro: `{"error":{"code":"...","message":"..."}}`.
As rotas de jogador pedem `Authorization: Bearer <token>`; as de frota,
`X-Fleet-Key: <segredo>`.

| Rota | Quem | O quê |
|---|---|---|
| `GET /health` | qualquer um | Está no ar? Versão, esquema, jogadores online, frota |
| `POST /v1/accounts` `{name, password}` | — | Cria a conta e já devolve `{token, profile}`. 409 se o nome existe |
| `POST /v1/sessions` `{name, password}` | — | Entra: `{token, profile}`. 401 se errou; 429 se insistiu demais |
| `DELETE /v1/sessions` | jogador | Sai (o token deixa de valer) |
| `GET /v1/profile` | jogador | `{id, name, xp, presas, stats{matches, wins, kills...}, data}` |
| `PUT /v1/profile/data` | jogador | Guarda um JSON do jogo (até 64 kB) |
| `GET /v1/ranking?limit=` | jogador | Os primeiros por XP |
| `GET /v1/rooms` | jogador | As salas abertas |
| `GET /v1/matches` | jogador | As últimas partidas da conta |
| `POST /v1/fleet/heartbeat` `{id, address, port, players, capacity, matches[], progress[]}` | frota | Um servidor de partida bate ponto: quantas partidas aguenta (`capacity`, 1 se faltar) e as chaves das que tem abertas (`matches`). `progress[]` diz onde cada uma está (`key`, `live`, `timeLeft`, `duration`, `blue`, `orange`, `toWin`): é com isso que o saguão fecha a entrada da partida quase acabando. A resposta traz `assignments[]`, as partidas novas para ele (e `assignment`, a primeira, para servidor antigo); em `players[]`, bot tem `accountId` negativo e `owner`. Partida entregue que some de `matches` (fechou sem resultado: ninguém entrou, ou todos saíram) libera o lugar, e a sala volta a esperar. Alguém entrou na partida em curso (`room.enter`): a mesma partida vem de novo, com o lugar novo em `players` (ou o de quem voltou, no time que escolheu agora) e `update: true` (o servidor de partida só abre os lugares; nunca abre partida nova com ela) |
| `POST /v1/matches` `{matchKey, serverId, scoreBlue, scoreOrange, players[]}` | frota | O resultado. 409 se já foi entregue. Em `players[]`, `killsWolf` (opcional) diz quantos abates foram como lobo e `rejectedHits` (opcional) quantos acertos o servidor de partida recusou (decisão 0016; 15 ou mais viram alerta na ficha) `fire` (opcional) são os segundos em chamas (o rage): vira XP, cortado no máximo que os abates e assistências permitem (`Rage.MaxFireSeconds`); acima disso vira evento de segurança. |
| `GET /v1/gamedata` | qualquer um | Catálogo, mapas, promoções, desafios e configurações, de uma vez. Com `If-None-Match`, 304 se nada mudou |
| `GET /v1/inventory` | jogador | `{presas, sangue, xp, rank, items[]}` (só o que está valendo) |
| `DELETE /v1/inventory/{id}` | jogador | Descarta um item da conta (`item_inexistente` se ela não tem). O que vem com a conta não está no inventário do servidor: não há o que descartar |
| `POST /v1/account/name` `{name}` | jogador | Troca o nome: confere se está livre e gasta um uso do item `troca_nome`. `nome_em_uso`, `sem_troca_de_nome` |
| `POST /v1/shop/buy` `{itemId, duration}` | jogador | Compra: o servidor confere preço, desconto, patente e saldo. `duration` de 0 a 4 (1, 7, 30, 90 dias, para sempre) |
| `GET /v1/challenges` | jogador | `{challenges[], titles[], dailyResetsAt, weeklyResetsAt}`: os desafios valendo agora (o rodízio do dia e da semana, os permanentes e os de evento), o progresso da conta, os títulos ganhos e quando viram |
| `POST /v1/challenges/{id}/claim` | jogador | Recebe o prêmio de um desafio completo (uma vez por período) |
| `/admin/v1/...` | administrador | O painel — só na porta do painel (`docs/PAINEL.md`) |
| `GET /metrics` | Prometheus | Os números, no formato do Prometheus — só na porta do painel |

Erros que o jogo precisa saber mostrar: `conta_banida` (403, a mensagem
diz até quando e por quê), `manutencao` (503, com o recado do painel),
`bloqueado` (403, endereço na blacklist), `devagar` (429),
`mapa_desligado` (no saguão, ao criar sala ou trocar o mapa).

## Saguão — WebSocket em `/v1/lobby?token=`

Mensagens em JSON com o tipo em `t`. Uma ligação por conta: abrir a
segunda derruba a primeira.

| Do jogo | Campos | |
|---|---|---|
| `rooms.list` | | Pede a lista |
| `room.create` | `settings{title, password, map, teamSize, scoreToWin, minutes, weapons, transform, friendlyFire, difficulty}` (difficulty: 0 fáceis, 1 médios, 2 difíceis) | Abre uma sala e entra nela como dono |
| `room.join` | `id`, `password` | Também com a partida em curso, se houver vaga (gente e bots que estão jogando contados) e ela não estiver quase acabando (`partida_acabando`): entra na **sala**, de fora da partida (`inMatch: false`), no time em que jogou (se voltou e ainda cabe) ou no que dá para entrar. Troca de lado com `room.team` e entra com `room.enter` |
| `room.enter` | | Na sala com a partida em curso, de fora dela: entra pelo time em que está. Recusa `time_cheio`, `time_desequilibrado` (o time ficaria com dois a mais que o outro) e `partida_acabando`. Quem já jogou volta para o lugar dele (com o que tinha feito); quem chega ganha um lugar novo (o servidor de partida o abre na próxima batida). Responde `match.start` com `late: true` |
| `room.leave` | | Saindo o dono, a sala passa para o próximo (o mais antigo); com a partida em curso, ela continua. Sala vazia some, e a partida dela é descartada (sai da frota; o resultado, se vier, é recusado). O jogo manda isto ao sair da partida no meio (vai para o lobby) |
| `room.team` | `team` (1 azul, 2 laranja) | Com a partida em curso, só para quem está de fora dela, e só para um time de `enter` |
| `room.ready` | `ready` | |
| `profile.photo` | `id` | A foto de perfil que os outros veem (uma das oito grátis, como `lobo_cinza`, ou uma comprada, item `foto_<id>`; a lista é `PhotoRule`). O servidor ignora foto que a conta não tem. Cada membro da sala sai com `photo`. |
| `room.settings` | `settings` | Só o dono |
| `room.bot.add` | `team` | Só o dono: põe um bot no time (se couber) |
| `room.bot.remove` | `id` (negativo) | Só o dono: tira o bot |
| `room.start` | | Só o dono, com todos prontos (bot está sempre pronto) |
| `room.restart` | | Só o dono: a sala inteira vai para uma partida nova, sem esperar pronto. Com partida em curso, ela é descartada (sai da frota; o resultado dela, quando vier, é recusado e não paga ninguém). Na tela do resultado é o "jogar de novo" |
| `chat` | `text` | Na sala, para a sala; fora, para o saguão |
| `ping` | | |

| Do servidor | |
|---|---|
| `hello` | Quem você é e quantos estão online |
| `rooms` | A lista de salas (a cada mudança). Cada uma com `players` (gente; jogando, só quem está na partida) e `bots` (o jogo mostra a soma), `open` (dá para entrar agora: esperando com lugar, ou jogando com vaga sem desequilibrar e sem estar acabando) e `closing` (a partida está quase acabando) |
| `room` | A sala em que você está: regras, membros, estado. Os bots vêm em `members` com `bot: true`, `id` negativo, `owner` (a conta do dono da sala, cuja máquina os move) e sem `xp`. Cada pessoa tem `inMatch` (está jogando a partida em curso). Com a partida em curso, a sala traz `enter` (por quais times dá para entrar agora) e `closing` |
| `left` | Você saiu da sala |
| `chat` | `from`, `text` |
| `match.start` | `address`, `port`, `matchKey`, `accountId`, `expires`, `ticket`, `settings`, `restart` (o dono reiniciou a partida que estava em curso), `late` (entrou com a partida em curso: os bots são bonecos até o servidor de partida dizer que passaram para você) — hora de ligar no servidor de partida |
| `match.end` | O placar e o que você ganhou (`you{xp, presas}`) |
| `error` | `code`, `message`. Na sala em partida: `servidor_caiu` (o servidor de partida parou de bater) ou `partida_encerrada` (fechou sem resultado) — a sala já voltou a esperar |
| `pong` | |
