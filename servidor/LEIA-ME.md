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
  Contas/Account.cs      conta, nome e senha válidos, a tag (Nome#0042), perfil, ranking
  Salas/Room.cs          a sala: entrar, times, dono, bots, regras, começar
  Salas/Group.cs         o grupo: quem está, quem lidera, os tetos e os "não dá"
  Partidas/Match.cs      escalação, resultado, quem ganha e quanto (Rewards.cs, do jogo)
  Partidas/Plausibility  o que um resultado tem de estranho (alerta de trapaça)
  Frota/Fleet.cs         servidores de partida: lugares, entrega, partidas fechadas
  Loja/Shop.cs           catálogo, promoção, preço, validade — "Sangue não compra poder"
  Desafios/Challenge.cs  desafios: métrica, período (dia e semana de Brasília), progresso
  Seguranca/             faixa de IP (CIDR), eventos, padrões de ataque, TOTP
  Painel/Admin.cs        papéis e permissões do painel, regras de conta
  Contas/RankRule.cs     a patente a partir do XP (a mesma conta do jogo) e o prêmio de promoção
  Contas/Friends.cs      amigos: o que uma conta é da outra, os tetos e os "não dá"
  GameClock.cs           o dia do jogo (Brasília): quando viram missões e vitrine
Alcateia.Aplicacao/      os casos de uso
  Portas/Ports.cs        o que a aplicação pede de fora: repositórios, senha, relógio, conexão
  Contas/AccountService  criar conta, entrar, sair, perfil, ranking, verificação em duas etapas
  Saguao/LobbyService    quem está ligado, salas e chat (o protocolo do WebSocket);
                         em LobbyService.Friends.cs, os amigos: lista, pedidos, presença e convites;
                         em LobbyService.Groups.cs, os grupos: amigos que entram juntos nas salas
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
  Repositorios/          contas, amizades, partidas, jogadores (carteira, inventário), dados do jogo,
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
  Rotas/                 contas, saguão (WebSocket), frota, partidas, saúde, o cliente de instalação (ClientRoutes),
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
| `POST /v1/sessions` `{name, password, code?}` | — | Entra: `{token, profile}`. 401 se errou; 429 se insistiu demais. Conta com verificação em duas etapas: sem `code` (o código de 6 dígitos do aplicativo autenticador) responde 401 `codigo_necessario` — a senha estava certa, a tela pede o código e manda de novo; código errado, `codigo_invalido` |
| `DELETE /v1/sessions` | jogador | Sai (o token deixa de valer) |
| `GET /v1/profile` | jogador | `{id, name, tag, twoFactor, xp, presas, stats{matches, wins, kills...}, data}` |
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
| `POST /v1/account/2fa` | jogador | Começa a ligar a verificação em duas etapas: devolve `secret` (a chave, Base32) e `uri` (`otpauth://…`, que vira QR code). Só vale depois do `confirm`. `ja_ligada` |
| `POST /v1/account/2fa/confirm` `{code}` | jogador | O primeiro código do aplicativo autenticador liga a segunda etapa (204). `codigo_invalido`, `sem_segredo` |
| `POST /v1/account/2fa/disable` `{password, code}` | jogador | Desliga (204): pede a senha e um código. `senha_errada`, `codigo_invalido`, `nao_ligada` |
| `GET /v1/client` | qualquer um | `{downloads}`: de onde o cliente de instalação baixa o jogo (a variável `ALCATEIA_DOWNLOADS`; vazio se não configurado). O formato dos arquivos está em `cliente/LEIA-ME.md` |
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

Mensagens em JSON com o tipo em `t`. Uma ligação do jogo por conta: abrir a
segunda derruba a primeira.

**O cliente de instalação** liga com `/v1/lobby?client=app&token=`. É uma
ligação à parte, que convive com a do jogo (abrir o jogo não a derruba, nem
o contrário) e só vê e mexe nos amigos: recebe `hello`, `friends`,
`friend.request` e `friend.accepted`, e pode mandar `ping`, `friends.list`,
`friend.add`, `friend.accept`, `friend.decline` e `friend.remove`. O resto
responde `so_no_jogo`. Com o jogo fechado, os amigos veem a conta com
`status: "app"`.

**A tag.** Toda conta tem quatro algarismos sorteados quando nasce (`tag`,
"0042", no perfil e no `hello`), que não mudam nem na troca de nome. O nome
com a tag (`Nome#0042`) é o que se escreve para pedir amizade: o nome
continua único (é com ele que se entra), e a tag é o que só quem a pessoa
contou sabe — ver um nome no placar não basta para encher a caixa dela.

| Do jogo | Campos | |
|---|---|---|
| `rooms.list` | | Pede a lista |
| `room.create` | `settings{title, password, map, teamSize, scoreToWin, minutes, weapons, transform, friendlyFire, difficulty}` (difficulty: 0 fáceis, 1 médios, 2 difíceis) | Abre uma sala e entra nela como dono. Num grupo: só o líder, e o grupo nasce dentro dela, no time dele (`grupo_nao_cabe` se os times são menores que o grupo) |
| `room.join` | `id`, `password` | **Num grupo**, só o líder entra numa sala, e leva o grupo junto, todos no mesmo time (`so_o_lider` para os outros; `grupo_nao_cabe` se não há lugar para todos do mesmo lado: ninguém entra). Quem não lidera só entra, sozinho e sem senha, na sala em que o líder está. Também com a partida em curso, se houver vaga (gente e bots que estão jogando contados) e ela não estiver quase acabando (`partida_acabando`): entra na **sala**, de fora da partida (`inMatch: false`), no time em que jogou (se voltou e ainda cabe) ou no que dá para entrar. Troca de lado com `room.team` e entra com `room.enter` |
| `room.enter` | | Na sala com a partida em curso, de fora dela: entra pelo time em que está. Recusa `time_cheio`, `time_desequilibrado` (o time ficaria com dois a mais que o outro) e `partida_acabando`. Quem já jogou volta para o lugar dele (com o que tinha feito); quem chega ganha um lugar novo (o servidor de partida o abre na próxima batida). Responde `match.start` com `late: true` |
| `room.leave` | | Se quem sai lidera um grupo, os do grupo que estão na sala, fora de partida, saem junto. Saindo o dono, a sala passa para o próximo (o mais antigo); com a partida em curso, ela continua. Sala vazia some, e a partida dela é descartada (sai da frota; o resultado, se vier, é recusado). O jogo manda isto ao sair da partida no meio (vai para o lobby) |
| `room.team` | `team` (1 azul, 2 laranja) | Num grupo, com a sala esperando e o líder nela, o time é do grupo: o líder troca e todos vão (`time_cheio` se não cabem), os outros recebem `so_o_lider`. Com a partida em curso, só para quem está de fora dela, e só para um time de `enter` |
| `room.ready` | `ready` | |
| `profile.photo` | `id` | A foto de perfil que os outros veem (uma das oito grátis, como `lobo_cinza`, ou uma comprada, item `foto_<id>`; a lista é `PhotoRule`). O servidor ignora foto que a conta não tem. Cada membro da sala sai com `photo`. |
| `room.settings` | `settings` | Só o dono |
| `room.bot.add` | `team` | Só o dono: põe um bot no time (se couber) |
| `room.bot.remove` | `id` (negativo) | Só o dono: tira o bot |
| `room.start` | | Só o dono, com todos prontos (bot está sempre pronto) |
| `room.restart` | | Só o dono: a sala inteira vai para uma partida nova, sem esperar pronto. Com partida em curso, ela é descartada (sai da frota; o resultado dela, quando vier, é recusado e não paga ninguém). Na tela do resultado é o "jogar de novo" |
| `chat` | `text` | Na sala, para a sala; fora, para o saguão |
| `friends.list` | | Pede a lista de amigos (ela também vem sozinha ao entrar e a cada mudança) |
| `friend.add` | `name` | Pede amizade pelo nome com a tag (`Nome#0042`), sem diferenciar maiúsculas. Sem a tag, `tag_faltando`; tag errada responde igual a nome que não existe (`conta_inexistente`). Se a outra conta já tinha pedido, vira amizade na hora. Recusa `conta_inexistente`, `amigo_voce`, `ja_amigos`, `pedido_ja_enviado`, `amigos_cheio` (100 amigos), `pedidos_demais` (50 pedidos seus esperando), `pedidos_demais_dele` (50 esperando na caixa do outro) e `devagar` (10 pedidos por minuto) |
| `friend.accept` | `id` | Aceita o pedido que a conta `id` mandou. `pedido_inexistente`; `amigos_cheio` ou `amigos_cheio_dele` se um dos dois não tem mais lugar |
| `friend.decline` | `id` | Recusa o pedido recebido de `id`, ou cancela o que você mandou para `id`. `pedido_inexistente` |
| `friend.remove` | `id` | Desfaz a amizade. `nao_e_amigo` |
| `friend.invite` | `id` | Chama o amigo `id` para a sala em que você está. `fora_de_sala`, `nao_e_amigo`, `amigo_offline`, `amigo_fora_do_jogo` (só com o cliente de instalação aberto), `ja_na_sala`, `devagar` (o mesmo amigo de novo em menos de 5 s, ou mais de 8 convites em 20 s) |
| `invite.accept` | `from` | Entra na sala para a qual a conta `from` convidou. O convite dispensa a senha e mais nada: lotação, times e partida quase acabando valem igual (`sala_cheia`, `partida_acabando`...; recusado assim, o convite continua valendo). `convite_vencido` se não há convite, se passou de 2 minutos ou se quem convidou já saiu daquela sala. Responde `room`, como o `room.join` |
| `invite.decline` | `from` | Recusa o convite; quem convidou recebe `invite.declined` |
| `group.invite` | `id` | Chama o amigo `id` (que está no jogo) para o grupo. Quem não tem grupo passa a liderar um quando o primeiro convite for aceito; com grupo, só o líder chama. `nao_e_amigo`, `amigo_offline`, `amigo_fora_do_jogo`, `nao_e_lider`, `ja_no_grupo`, `grupo_cheio` (6), `devagar` |
| `group.accept` | `from` | Entra no grupo de quem convidou (saindo do que estava). O convite vale 2 minutos, enquanto quem convidou está no jogo e lidera. Se o líder está numa sala, quem entrou vai para ela, no time dele; se não cabe, entra no grupo do mesmo jeito e recebe o `error` dizendo por que ficou de fora. `convite_vencido`, `grupo_cheio` |
| `group.decline` | `from` | Recusa; quem convidou recebe `group.declined` |
| `group.leave` | | Sai do grupo. Sobrou um só, o grupo acaba. `sem_grupo` |
| `group.kick` | `id` | Só o líder: tira alguém, que recebe `group.kicked`. `nao_e_lider`, `fora_do_grupo` |
| `group.lead` | `id` | Só o líder: passa a liderança. `nao_e_lider`, `fora_do_grupo` |
| `queue.join` | | Jogo rápido: entra na fila (num grupo, só o líder, e o grupo vai junto). O servidor junta gente de nota parecida, monta dois times da mesma força, sorteia o modo (um dos quatro contra jogadores) e o mapa (entre os ligados que servem para o tamanho dos times), abre a sala e começa a partida. Recusa `em_sala` (alguém do grupo numa sala), `so_o_lider`, `devagar` (8 entradas a cada 20 s). Pedir de novo só repete o estado |
| `queue.leave` | | Sai da fila (num grupo, para a busca de todos) |
| `ping` | | |

| Do servidor | |
|---|---|
| `hello` | Quem você é (`id`, `name`, `tag`) e quantos estão online |
| `rooms` | A lista de salas (a cada mudança). Cada uma com `players` (gente; jogando, só quem está na partida) e `bots` (o jogo mostra a soma), `open` (dá para entrar agora: esperando com lugar, ou jogando com vaga sem desequilibrar e sem estar acabando) e `closing` (a partida está quase acabando) |
| `room` | A sala em que você está: regras, membros, estado. Os bots vêm em `members` com `bot: true`, `id` negativo, `owner` (a conta do dono da sala, cuja máquina os move) e sem `xp`. Cada pessoa tem `inMatch` (está jogando a partida em curso). Com a partida em curso, a sala traz `enter` (por quais times dá para entrar agora) e `closing` |
| `left` | Você saiu da sala |
| `chat` | `from`, `text` |
| `match.start` | `address`, `port`, `matchKey`, `accountId`, `expires`, `ticket`, `settings`, `restart` (o dono reiniciou a partida que estava em curso), `late` (entrou com a partida em curso: os bots são bonecos até o servidor de partida dizer que passaram para você) — hora de ligar no servidor de partida |
| `match.end` | O placar e o que você ganhou (`you{xp, presas}`) |
| `friends` | `friends[]` (`id`, `name`, `tag`, `xp`, `photo`, `status`, `room`), `requests[]` (pedidos recebidos: `id`, `name`, `xp`, `photo`) e `sent[]` (pedidos enviados, igual). `status`: `offline`, `app` (jogo fechado, cliente de instalação aberto: dá para ver, não para convidar), `online` (ligado, fora de sala), `room` (numa sala, esperando ou de fora da partida dela) ou `playing` (jogando); `room` é o número da sala, ou `null`. No jogo primeiro, depois os que estão só no aplicativo, depois os desligados; em cada grupo, por nome. Vem ao entrar, em resposta a `friends.list`, quando a sua lista muda e quando um amigo muda de presença (entrou, caiu, sala, partida, foto). Amigo desligado sai com o nome e o XP de quando você entrou e a foto padrão |
| `friend.request` | `from{id, name, xp, photo}`: chegou um pedido de amizade (a `friends` nova vem junto) |
| `friend.accepted` | `by{id, name, xp, photo}`: aceitaram o seu pedido (ou pediram de volta: pedido cruzado) |
| `friend.invited` | `from{id, name, xp, photo}` e `room{id, title, map, mode, players, capacity, state}`: um amigo chamou você para a sala dele. `mode` é o `play` das regras (o modo de jogo); `players` conta gente e bots, como a lista de salas mostra |
| `invite.declined` | `by{id, name}`: recusaram o seu convite |
| `group` | O seu grupo inteiro: `leader` (a conta que lidera), `max` (6) e `members[]` na ordem em que entraram (`id`, `name`, `tag`, `xp`, `photo`, `status`, `room`). `members` vazio: você não está em grupo (saiu, foi tirado, o grupo acabou). Vem quando o grupo muda e quando alguém dele muda de presença |
| `group.invited` | `from{id, name, tag, xp, photo}`, `size` (quantos já estão) e `max`: um amigo chamou você para o grupo dele |
| `group.declined` | `by{id, name}`: recusaram o seu convite para o grupo |
| `group.kicked` | `by{id, name}`: o líder tirou você (o `group` vazio vem junto) |
| `queue` | O jogo rápido. `state`: `searching` (com `players`, quantos procuram; `waited`, segundos na fila; `size`, quantos na sua entrada; `teamMin`, o menor time que o formador já aceita para você; `note`, um recado, como sem servidor de partida livre), `idle` (parou: `note` diz por quê — cancelou, foi para uma sala, o grupo mudou, alguém do grupo caiu; vazio se foi você que cancelou) ou `found` (`room`, `map`, `play`, `teamSize`). Achada a partida, vêm a `room` (com `settings.quick: true` e todos em `inMatch`) e o `match.start`. A sala do jogo rápido não aparece na lista, não aceita entrar, trocar de time, mudar regra, bot, convite nem recomeçar (`sala_da_fila`), e acaba com a partida (chega `left`) |
| `error` | `code`, `message`. Na sala em partida: `servidor_caiu` (o servidor de partida parou de bater) ou `partida_encerrada` (fechou sem resultado) — a sala já voltou a esperar |
| `pong` | |
