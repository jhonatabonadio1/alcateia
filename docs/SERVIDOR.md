# Servidor e jogo online

Como o Alcateia joga em rede: o que roda onde, como subir, como testar
e o que ainda não faz. As decisões por trás estão em
`docs/decisoes/0008` (quem decide o quê) e `0009` (servidor sem pacote).
O painel administrativo está em `docs/PAINEL.md`; a segurança (o escudo,
a blacklist de IP, como pôr na internet), em `docs/SEGURANCA.md`.

## As três peças

```
   jogo ──HTTP──▶ servidor ◀──HTTP── servidor de partida
    │  ──WebSocket─▶  │  (conta, salas, chat,      ▲
    │                 ▼   ranking, frota)          │
    │            PostgreSQL                        │
    └──────────────────UDP 7777────────────────────┘
                  (a partida em si)
```

| Peça | O que é | Onde está |
|---|---|---|
| **Servidor** | ASP.NET (.NET 8). Contas, sessões, perfil, ranking, o saguão (salas e chat por WebSocket), a frota de servidores de partida, os dados do jogo (catálogo, mapas, promoções, desafios), a loja, o inventário e o escudo de segurança | `servidor/Alcateia.Servidor` |
| **Painel** | React. Jogadores, banimentos, moeda e itens, armas e skins, mapas, promoções, desafios, eventos de segurança, blacklist de IP, auditoria. Servido pelo próprio servidor, **em outra porta** (8081), que não se abre para a internet | `servidor/painel`, `docs/PAINEL.md` |
| **Banco** | PostgreSQL 16. É onde tudo mora: contas, sessões, perfis, carteira, inventário, extrato, partidas, catálogo, mapas, promoções, desafios, banimentos, blacklist de IP, eventos de segurança, administradores e auditoria | contêiner `banco` |
| **Servidor de partida** | Um programa à parte, exportado do mesmo projeto como servidor dedicado (`servidor/partida.Dockerfile`, docs/SERVIDOR_DEDICADO.md); no desenvolvimento, o jogo sem tela (`godot --headless -- --servidor`). Várias partidas ao mesmo tempo (4 por padrão, `ALCATEIA_MATCHES`), na mesma porta: recebe os jogadores de cada uma, repassa o que cada um faz, arbitra e entrega o resultado. Partida que todos abandonam acaba em 20 s e libera o lugar | `src/scripts/net/MatchServer.cs`, `HostedMatch.cs` |
| **Jogo** | A tela Online (`OnlineScreen`), a conta (`Backend`), o saguão ao vivo (`LobbyLink`) e a partida em rede (`NetMatch`) | `src/scripts/net`, `src/scripts/lobby` |

## Subir tudo

Precisa só do Docker.

```
cp .env.exemplo .env        # e troque os segredos; ponha o nome e a senha do primeiro administrador
docker compose up -d --build
curl http://127.0.0.1:8080/health
```

O painel abre em **http://127.0.0.1:8443** (só nesta máquina; dentro do contêiner ele escuta na 8081), com o
administrador que você pôs no `.env` (`ALCATEIA_ADMIN_USER` e
`ALCATEIA_ADMIN_PASSWORD`). Entre, troque a senha e ligue a verificação
em duas etapas (`docs/PAINEL.md`).

A primeira vez demora: baixa o .NET, o PostgreSQL e o Godot. Sobem três
contêineres: `banco`, `servidor` (porta 8080) e `partida` (porta
7777/UDP). `docker compose logs -f servidor partida` mostra o que
acontece; `docker compose down` para (os dados ficam no volume `banco`).

No jogo: na tela de login, **Criar conta**. O endereço do servidor é fixo no jogo por enquanto (`Backend.DefaultUrl`, `http://127.0.0.1:8080`); `ALCATEIA_API` passa por cima. A escolha do servidor mais rápido e mais perto vem depois.

**Para jogar com alguém de fora da sua máquina**, ponha no `.env` o
endereço pelo qual os outros chegam nela
(`ALCATEIA_PUBLIC_ADDRESS=192.168.0.10`, ou o IP público), libere as
portas 8080/TCP e 7777/UDP, e cada jogador abre o jogo com
`ALCATEIA_API=http://esse-endereço:8080` (a tela de login não tem mais o
campo "Servidor": o endereço é fixo no jogo por enquanto).

**Mais partidas ao mesmo tempo**: cada servidor de partida cuida de uma.
Suba mais com `docker compose up -d --scale partida=3` — tirando do
`docker-compose.yml` a porta fixa e dando uma porta a cada um
(`ALCATEIA_GAME_PORT`).

### Variáveis

| Variável | Para quê | Padrão |
|---|---|---|
| `ALCATEIA_DB_PASSWORD` | Senha do banco | obrigatória |
| `ALCATEIA_FLY_APP`, `ALCATEIA_FLY_TOKEN`, `ALCATEIA_FLEET_RESERVE` | Servidores de partida sob demanda no Fly.io (docs/FLY.md) | — |
| `ALCATEIA_FLEET_KEY` | Segredo entre o servidor e os servidores de partida. Assina os ingressos | obrigatória |
| `ALCATEIA_PUBLIC_ADDRESS` | O endereço que os jogadores usam para chegar ao servidor de partida | `127.0.0.1` |
| `ALCATEIA_PORT` | Porta HTTP do servidor na sua máquina | `8080` |
| `ALCATEIA_GAME_PORT` | Porta UDP do servidor de partida | `7777` |
| `ALCATEIA_MATCHES` | Quantas partidas o servidor de partida joga ao mesmo tempo | `4` |
| `ALCATEIA_NETCODE` | `autoritativo`: o servidor de partida move os corpos (decisão 0011) e decide vida, acerto e morte (0016); vazio ou `repasse`: cada jogo move o seu e decide a própria vida (0008) | vazio |
| `ALCATEIA_API` | (jogo e servidor de partida) onde fica o servidor; passa por cima do endereço fixo do jogo | — |
| `ALCATEIA_SESSION_DAYS` | Validade do login | `30` |
| `ALCATEIA_MASTER_KEY` | Cifra o que não pode ficar legível no banco (o segredo das duas etapas) | obrigatória |
| `ALCATEIA_ADMIN_USER`, `ALCATEIA_ADMIN_PASSWORD` | O primeiro administrador do painel; só vale enquanto não há nenhum | — |
| `ALCATEIA_ADMIN_PORT` | Porta do painel (0 desliga o painel) | `8081` |
| `ALCATEIA_ADMIN_BIND` | Em que endereço a porta do painel escuta. Fora do Docker, só a própria máquina | `127.0.0.1` |
| `ALCATEIA_ADMIN_ALLOW` | Quem pode falar com o painel (endereços ou faixas, com vírgula) | qualquer um que alcance a porta |
| `ALCATEIA_ADMIN_REQUIRE_2FA` | `1`: sem as duas etapas ligadas, o administrador só consegue ligá-las | `0` |
| `ALCATEIA_ADMIN_UI` | A pasta do painel compilado | `painel/` ao lado do programa |
| `ALCATEIA_TRUSTED_PROXIES` | De quem o servidor aceita `X-Forwarded-For` e `CF-Connecting-IP` (o proxy da frente) | ninguém |
| `ALCATEIA_REQUESTS_PER_MINUTE` | Freio de pedidos por minuto, por endereço | `600` |
| `ALCATEIA_LOGINS_PER_MINUTE` | Tentativas de login ou cadastro por minuto, por endereço | `12` |
| `ALCATEIA_AUTO_BLOCK` | `0`: o escudo só registra, não bloqueia sozinho | `1` |
| `ALCATEIA_IP_ALLOW` | Endereços que o escudo nunca bloqueia nem freia | — |
| `ALCATEIA_EVENT_DAYS` | Por quantos dias os eventos de segurança ficam no banco | `90` |
| `ALCATEIA_METRICS_KEY` | A chave que o Prometheus apresenta em `/metrics` | sem chave |

## Como uma partida acontece

1. O jogador entra na conta (`POST /v1/sessions`) e abre o WebSocket do
   saguão (`/v1/lobby?token=`). Cria ou entra numa sala, escolhe o
   time, marca pronto.
2. O dono inicia. O servidor escolhe um servidor de partida livre,
   manda a ele a escalação (na resposta da próxima batida de ponto) e a
   cada jogador o endereço, a porta e um **ingresso** assinado
   (`HMAC-SHA256(segredo, "partida|conta|validade")`, vale 3 minutos).
3. Cada jogo troca de cena, monta os outros jogadores como bonecos e
   liga no servidor de partida por UDP (ENet), mostrando o ingresso.
4. Durante a partida, 20 vezes por segundo cada jogo conta onde está;
   ataques, acertos e mortes vão com garantia de entrega. O juiz
   (`Referee`) valida os abates e manda o placar e o relógio.
   Quem sai da partida no meio sai da sala e vai para o lobby; o
   mestre passa ao próximo jogador, e os bots dele passam para a
   máquina desse jogador (pacote `BotOwner`). Com vaga, dá para entrar
   na sala jogando: a pessoa entra na sala, escolhe o lado (só um que
   não deixe um time com dois a mais) e entra na partida
   (`room.enter`). Quem já jogou volta no lugar dele (com o que tinha
   feito), quem chega ganha um lugar novo, e os outros veem o boneco
   dele aparecer (pacote `Seat`). Partida quase acabando (últimos 2
   minutos ou últimos 20% do tempo, o que for menor, ou um time com 80%
   dos pontos) não recebe ninguém: o servidor de partida conta tempo e
   placar a cada batida. Sala vazia descarta a partida.
5. No fim, o servidor de partida entrega o resultado
   (`POST /v1/matches`); o servidor calcula o prêmio com a mesma regra
   do jogo (`src/scripts/domain/Rewards.cs`), grava no perfil e avisa a
   sala, que volta a esperar.

O formato dos pacotes da partida está em `src/scripts/net/NetWire.cs`; as
rotas HTTP e as mensagens do saguão, em `servidor/LEIA-ME.md`.

## Testar

| Comando | O que prova |
|---|---|
| `src/tools/testar_servidor.sh` | Os testes do servidor contra um PostgreSQL de verdade (sobe um descartável no Docker): banco, contas, salas, chat, partidas, frota |
| `src/tools/testar_carga.sh` | Carga: 3000 jogadores de verdade (conta, WebSocket), 1000 salas jogando ao mesmo tempo e 250 servidores de partida falsos. Mede o tempo de cada resposta (veja "Escala") |
| `src/tools/testar.sh` | Inclui os testes de unidade do juiz (`RefereeTests`) |
| `src/tools/testar_online.sh` | Ponta a ponta: um servidor de partida e **dois jogos de verdade, sem tela**, criam conta, abrem sala, jogam 35 s (um atira, o outro morre e renasce) e conferem conta, placar e saguão. Precisa do servidor no ar (`docker compose up -d`, depois `docker compose stop partida`). Com `--voltar`, o criador (dono dos bots) sai no meio e volta pela lista de salas: o visitante tem de herdar os bots e o criador, entrar de novo no lugar dele. `ALCATEIA_TESTE_LOGS=<pasta>` guarda os logs |

O robô do teste é `src/scripts/net/OnlineRobot.cs` (`-- --robo=criar` /
`--robo=entrar`).

## Partida autoritativa (decisão 0011)

Com `ALCATEIA_NETCODE=autoritativo` no servidor de partida, ele carrega o
mapa (só o que colide), põe um corpo por jogador num mundo de física da
partida e move cada um pelos comandos que chegam, 60 passos por segundo.
O jogo adivinha o próprio movimento na hora e corrige quando o servidor
discorda; os outros aparecem 100 ms no passado (mais a folga do jitter, até
+80 ms), interpolados. O desenho dos outros anda liso: só muda de ritmo, até
10%, nunca pula nem volta, e freia quando os estados param de chegar; a
correção do próprio corpo escorrega numa mola (decisão 0018). A cada 30 s
o servidor escreve no log quanto o passo levou ("Passo do servidor:
média … pior …"): tem de caber com folga nos 16,7 ms.

| Medido (2 núcleos) | |
|---|---|
| Corpo do servidor × previsão do jogo | idênticos (com 100 ms de atraso e pacote perdido) |
| 16 jogadores / 64 jogadores | 0,4 ms / 1,5 ms por passo |
| Para cada jogador | ~4 KB/s de subida (comandos) e ~12 KB/s de descida numa sala cheia |

**Vida e acerto** (decisão 0016): o servidor guarda a vida de cada um,
confere cada acerto voltando o alvo no tempo (parede, mira, alcance,
ritmo, arma, de onde saiu), aplica o dano e decide a morte; renascer só
passada a espera e na base do time. No fim de cada partida escreve no log
quantos acertos valeram e os recusados por motivo ("Acertos em …"); cada
recusa suspeita conta contra quem atirou e vai no resultado
(`rejectedHits`). A forma, a arma na mão e os bots ainda são do jogo.

**Fluidez com a rede ruim** (decisão 0018, `FluidezAutoritativo` e
`FluidezRepasse`): o jogo de verdade ligado a um servidor de partida no
teste (`TestMatchServer`, que simula atraso, jitter, perda e o servidor
engasgando), o jogador e o outro correndo em círculo, quadro a quadro:

| Rede (autoritativo) | Câmera, trancos / para trás | Boneco, trancos / para trás |
|---|---|---|
| limpa | 0 / 0 (antes 9 / 4) | 0 / 0 (antes 295 / 2) |
| 80 ms, jitter 40, perda 2% | 0 / 0 (antes 9 / 6) | 0 / 0 (antes 226 / 0) |
| 150 ms, jitter 40, perda 5% | 0 / 0 (antes 9 / 3) | 0 / 0 (antes 224 / 0) |
| 250 ms, jitter 40, perda 5% | 0 / 0 (antes 9 / 6) | 0 / 0 (antes 199 / 4) |
| servidor parando 0,1–0,3 s a cada 1,5 s | 0 / 0 (antes 9 / 6) | 1 / 0 (antes 194 / 8) |

Tranco: a velocidade do que se vê muda mais de 2 m/s de um quadro para o
outro. Na câmera, com o servidor corrigindo o corpo 30 cm a cada 2 s. No
repasse, o boneco: 0 / 0 em todas (antes até 11 trancos a 150 ms). O
"antes" do boneco autoritativo é inflado pelo relógio de parede no teste
sem tela (que roda mais rápido que o tempo real); com o relógio no passo
e o resto antigo, eram 1 a 14 trancos e 2 voltas no tempo no engasgo.

`ALCATEIA_TESTE_TELEPORTE=1` deixa o jogador "renascer" vivo em qualquer
lugar: só o teste de ponta a ponta liga (o robô se põe de frente para o
alvo). Nunca em produção.

## Escala

O que aguenta, medido (`src/tools/testar_carga.sh`, 13/11, numa máquina de
**2 núcleos** dividida entre o servidor e o próprio teste):

| | |
|---|---|
| Jogadores no saguão ao mesmo tempo | 3000, ligados em 1,5 s |
| Salas e partidas ao mesmo tempo | 1000 (o limite do jogo), em 250 servidores de partida × 4 |
| Criar sala / entrar / começar a partida | mediana 14 / 17 / 31 ms; p99 17 / 247 / 71 ms |
| Ping do saguão com tudo acontecendo | mediana 0,8 ms, p99 22 ms |
| Batida da frota / resultado da partida | mediana 0,4 / 3,3 ms |
| Memória do servidor no pico | ~370 MB |
| Erros | nenhum |

Como chega lá:

- **Saguão**: cada conexão tem a própria fila de envio — mandar para
  milhares não espera nenhum deles; quem não dá conta da fila (512
  mensagens) é desligado e reconecta. A lista de salas vai junta, duas
  vezes por segundo, e só para quem está olhando para ela; mostra as 200
  em que dá para entrar primeiro (e o total). **Até 1000 salas** ao mesmo
  tempo (`Config.MaxRooms`).
- **Frota**: um servidor de partida joga várias partidas
  (`ALCATEIA_MATCHES`, 4); a frota acha cada partida pela chave, sem
  varrer os servidores. **Milhares de partidas são centenas de
  servidores de partida**, em quantas máquinas for preciso, todos
  batendo no mesmo servidor — a frota se monta sozinha. Na mesma máquina:
  `docker compose -f docker-compose.yml -f docker-compose.frota.yml up -d --scale partida=20`
  (cada um pega a primeira porta livre de `ALCATEIA_GAME_PORT=7777-7876`).
- **Banco**: o pool tem 32 ligações (`ALCATEIA_DB_POOL`); quem é dono de
  cada token fica meio minuto em memória.
- **O cadastro é lento de propósito** (PBKDF2, ~0,1 s de CPU por conta):
  3000 contas novas de uma vez levaram 2 minutos em 2 núcleos. Login em
  massa pesa o mesmo; mais núcleos, mais rápido.
- **Um processo só** de servidor (as salas vivem na memória dele). Para
  passar de dezenas de milhares no saguão, o próximo passo é dividir o
  saguão entre vários processos com um canal entre eles — hoje não é
  preciso.

## O que ainda não faz

- **No modo de repasse (o padrão) não é à prova de trapaça** — a vida e
  o acerto moram nas máquinas dos jogadores (`docs/decisoes/0008`). Para
  ranking público, `ALCATEIA_NETCODE=autoritativo`: lá o servidor decide
  vida, acerto e morte (decisão 0016). Mesmo nela, a forma, a arma na
  mão e as armas do conjunto ainda vêm do jogo, e todos recebem todos os
  corpos.
- **Bots online andam na máquina de um jogador**: a do dono da sala
  (`docs/decisoes/0008`); se ele sai, passam para o próximo jogador
  ligado; sem ninguém, saem junto. A
  dificuldade é da sala: o dono escolhe ao criar e muda na própria sala.
- Sem reconexão automática (quem cai volta para a sala; de lá, entra de
  novo pela sala, se houver vaga). Quem entra no meio vê a linha de cada
  jogador zerada no placar (o placar dos times vem certo).
- **Do lobo**, o uivo, a corrida na parede e os golpes especiais
  aparecem para os outros como movimento e ataque comum.
- **Sem conta não há progressão** (decisão 0015): loja, inventário,
  missões, XP e Presas são da conta. Partida sem servidor é treino e
  não rende nada.
- As estatísticas da tela de perfil do jogo ainda são as locais; as da
  conta aparecem no ranking e no painel.
