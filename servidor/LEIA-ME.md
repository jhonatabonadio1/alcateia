# Servidor do Alcateia

ASP.NET (.NET 8) + PostgreSQL, sem pacote de fora. Como subir, como as
peças se encaixam e o que falta: `docs/SERVIDOR.md`. Aqui fica a
referência do que o servidor fala.

Organizado em camadas (clean architecture): cada uma só conhece as de
dentro, e a regra não sabe de banco nem de HTTP.

```
Alcateia.Dominio/        as regras, puras (sem banco, rede ou ASP.NET)
  Failure.cs             "não dá": tipo, código e frase (a API traduz em HTTP)
  Contas/Account.cs      conta, nome e senha válidos, perfil, ranking
  Salas/Room.cs          a sala: entrar, times, dono, bots, regras, começar
  Partidas/Match.cs      escalação, resultado, quem ganha e quanto (Rewards.cs, do jogo)
  Frota/Fleet.cs         servidores de partida: lugares, entrega, partidas fechadas
Alcateia.Aplicacao/      os casos de uso
  Portas/Ports.cs        o que a aplicação pede de fora: repositórios, senha, relógio, conexão
  Contas/AccountService  criar conta, entrar, sair, perfil, ranking
  Saguao/LobbyService    quem está ligado, salas e chat (o protocolo do WebSocket)
  Frota/FleetService     a frota em uso (trava, relógio, partida perdida)
  Partidas/MatchService  recebe o resultado, grava, paga, libera a sala
Alcateia.Infra/          o mundo de fora
  Banco/Pg.cs            cliente do PostgreSQL (protocolo de fio)
  Banco/Schema.cs        migrações
  Repositorios/          contas e partidas no PostgreSQL
  Seguranca/Passwords.cs senha (PBKDF2), tokens, assinatura dos ingressos, relógio
Alcateia.Servidor/       a borda (o programa)
  Program.cs, App.cs     a montagem de tudo (raiz da composição)
  Config.cs              variáveis de ambiente
  Http/                  JSON, erros, chave da frota, freio de tentativas
  Rotas/                 contas, saguão (WebSocket), frota, partidas, saúde
Alcateia.Servidor.Testes/   testes de integração (tools/testar_servidor.sh)
Dockerfile                  a imagem do servidor
partida.Dockerfile          a imagem do servidor de partida (o jogo sem tela)
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
| `POST /v1/fleet/heartbeat` `{id, address, port, players, capacity, matches[]}` | frota | Um servidor de partida bate ponto: quantas partidas aguenta (`capacity`, 1 se faltar) e as chaves das que tem abertas (`matches`). A resposta traz `assignments[]`, as partidas novas para ele (e `assignment`, a primeira, para servidor antigo); em `players[]`, bot tem `accountId` negativo e `owner`. Partida entregue que some de `matches` (fechou sem resultado: ninguém entrou, ou todos saíram) libera o lugar, e a sala volta a esperar |
| `POST /v1/matches` `{matchKey, serverId, scoreBlue, scoreOrange, players[]}` | frota | O resultado. 409 se já foi entregue |

## Saguão — WebSocket em `/v1/lobby?token=`

Mensagens em JSON com o tipo em `t`. Uma ligação por conta: abrir a
segunda derruba a primeira.

| Do jogo | Campos | |
|---|---|---|
| `rooms.list` | | Pede a lista |
| `room.create` | `settings{title, password, map, teamSize, scoreToWin, minutes, weapons, transform, friendlyFire, difficulty}` (difficulty: 0 fáceis, 1 médios, 2 difíceis) | Abre uma sala e entra nela como dono |
| `room.join` | `id`, `password` | |
| `room.leave` | | Saindo o dono, a sala passa para o próximo |
| `room.team` | `team` (1 azul, 2 laranja) | |
| `room.ready` | `ready` | |
| `room.settings` | `settings` | Só o dono |
| `room.bot.add` | `team` | Só o dono: põe um bot no time (se couber) |
| `room.bot.remove` | `id` (negativo) | Só o dono: tira o bot |
| `room.start` | | Só o dono, com todos prontos (bot está sempre pronto) |
| `chat` | `text` | Na sala, para a sala; fora, para o saguão |
| `ping` | | |

| Do servidor | |
|---|---|
| `hello` | Quem você é e quantos estão online |
| `rooms` | A lista de salas (a cada mudança) |
| `room` | A sala em que você está: regras, membros, estado. Os bots vêm em `members` com `bot: true`, `id` negativo, `owner` (a conta do dono da sala, cuja máquina os move) e sem `xp` |
| `left` | Você saiu da sala |
| `chat` | `from`, `text` |
| `match.start` | `address`, `port`, `matchKey`, `accountId`, `expires`, `ticket`, `settings` — hora de ligar no servidor de partida |
| `match.end` | O placar e o que você ganhou (`you{xp, presas}`) |
| `error` | `code`, `message`. Na sala em partida: `servidor_caiu` (o servidor de partida parou de bater) ou `partida_encerrada` (fechou sem resultado) — a sala já voltou a esperar |
| `pong` | |
