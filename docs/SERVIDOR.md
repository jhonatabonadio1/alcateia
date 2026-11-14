# Servidor e jogo online

Como o Alcateia joga em rede: o que roda onde, como subir, como testar
e o que ainda não faz. As decisões por trás estão em
`docs/decisoes/0008` (quem decide o quê) e `0009` (servidor sem pacote).

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
| **Servidor** | ASP.NET (.NET 8). Contas, sessões, perfil, ranking, o saguão (salas e chat por WebSocket) e a frota de servidores de partida | `servidor/Alcateia.Servidor` |
| **Banco** | PostgreSQL 16. Contas, sessões, perfis, partidas | contêiner `banco` |
| **Servidor de partida** | O próprio jogo sem tela (`godot --headless -- --servidor`). Várias partidas ao mesmo tempo (4 por padrão, `ALCATEIA_MATCHES`), na mesma porta: recebe os jogadores de cada uma, repassa o que cada um faz, arbitra e entrega o resultado. Partida que todos abandonam acaba em 20 s e libera o lugar | `scripts/net/MatchServer.cs`, `HostedMatch.cs` |
| **Jogo** | A tela Online (`OnlineScreen`), a conta (`Backend`), o saguão ao vivo (`LobbyLink`) e a partida em rede (`NetMatch`) | `scripts/net`, `scripts/lobby` |

## Subir tudo

Precisa só do Docker.

```
cp .env.exemplo .env        # e troque a senha do banco e o segredo da frota
docker compose up -d --build
curl http://127.0.0.1:8080/health
```

A primeira vez demora: baixa o .NET, o PostgreSQL e o Godot. Sobem três
contêineres: `banco`, `servidor` (porta 8080) e `partida` (porta
7777/UDP). `docker compose logs -f servidor partida` mostra o que
acontece; `docker compose down` para (os dados ficam no volume `banco`).

No jogo: **Online → Criar conta**, com o servidor `http://127.0.0.1:8080`.

**Para jogar com alguém de fora da sua máquina**, ponha no `.env` o
endereço pelo qual os outros chegam nela
(`ALCATEIA_PUBLIC_ADDRESS=192.168.0.10`, ou o IP público), libere as
portas 8080/TCP e 7777/UDP, e cada jogador aponta o campo "Servidor"
para `http://esse-endereço:8080`.

**Mais partidas ao mesmo tempo**: cada servidor de partida cuida de uma.
Suba mais com `docker compose up -d --scale partida=3` — tirando do
`docker-compose.yml` a porta fixa e dando uma porta a cada um
(`ALCATEIA_GAME_PORT`).

### Variáveis

| Variável | Para quê | Padrão |
|---|---|---|
| `ALCATEIA_DB_PASSWORD` | Senha do banco | obrigatória |
| `ALCATEIA_FLEET_KEY` | Segredo entre o servidor e os servidores de partida. Assina os ingressos | obrigatória |
| `ALCATEIA_PUBLIC_ADDRESS` | O endereço que os jogadores usam para chegar ao servidor de partida | `127.0.0.1` |
| `ALCATEIA_PORT` | Porta HTTP do servidor na sua máquina | `8080` |
| `ALCATEIA_GAME_PORT` | Porta UDP do servidor de partida | `7777` |
| `ALCATEIA_MATCHES` | Quantas partidas o servidor de partida joga ao mesmo tempo | `4` |
| `ALCATEIA_NETCODE` | `autoritativo`: o servidor de partida move os corpos (decisão 0011); vazio ou `repasse`: cada jogo move o seu (0008) | vazio |
| `ALCATEIA_API` | (jogo e servidor de partida) onde fica o servidor; passa por cima do campo da tela | — |
| `ALCATEIA_SESSION_DAYS` | Validade do login | `30` |

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
5. No fim, o servidor de partida entrega o resultado
   (`POST /v1/matches`); o servidor calcula o prêmio com a mesma regra
   do jogo (`scripts/domain/Rewards.cs`), grava no perfil e avisa a
   sala, que volta a esperar.

O formato dos pacotes da partida está em `scripts/net/NetWire.cs`; as
rotas HTTP e as mensagens do saguão, em `servidor/LEIA-ME.md`.

## Testar

| Comando | O que prova |
|---|---|
| `tools/testar_servidor.sh` | Os testes do servidor contra um PostgreSQL de verdade (sobe um descartável no Docker): banco, contas, salas, chat, partidas, frota |
| `tools/testar_carga.sh` | Carga: 3000 jogadores de verdade (conta, WebSocket), 1000 salas jogando ao mesmo tempo e 250 servidores de partida falsos. Mede o tempo de cada resposta (veja "Escala") |
| `tools/testar.sh` | Inclui os testes de unidade do juiz (`RefereeTests`) |
| `tools/testar_online.sh` | Ponta a ponta: um servidor de partida e **dois jogos de verdade, sem tela**, criam conta, abrem sala, jogam 35 s (um atira, o outro morre e renasce) e conferem conta, placar e saguão. Precisa do servidor no ar (`docker compose up -d`, depois `docker compose stop partida`) |

O robô do teste é `scripts/net/OnlineRobot.cs` (`-- --robo=criar` /
`--robo=entrar`).

## Partida autoritativa (decisão 0011)

Com `ALCATEIA_NETCODE=autoritativo` no servidor de partida, ele carrega o
mapa (só o que colide), põe um corpo por jogador num mundo de física da
partida e move cada um pelos comandos que chegam, 60 passos por segundo.
O jogo adivinha o próprio movimento na hora e corrige quando o servidor
discorda; os outros aparecem 100 ms no passado, interpolados. A cada 30 s
o servidor escreve no log quanto o passo levou ("Passo do servidor:
média … pior …"): tem de caber com folga nos 16,7 ms.

| Medido (2 núcleos) | |
|---|---|
| Corpo do servidor × previsão do jogo | idênticos (com 100 ms de atraso e pacote perdido) |
| 16 jogadores / 64 jogadores | 0,4 ms / 1,5 ms por passo |
| Para cada jogador | ~4 KB/s de subida (comandos) e ~12 KB/s de descida numa sala cheia |

Nesta primeira leva a vida, o tiro, a forma e os bots ainda são do jogo
(como no repasse); a leva seguinte leva para o servidor.

## Escala

O que aguenta, medido (`tools/testar_carga.sh`, 13/11, numa máquina de
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

- **Não é à prova de trapaça** — a vida e o acerto moram nas máquinas
  dos jogadores (`docs/decisoes/0008`). Bom para jogar entre
  conhecidos; não para ranking público.
- **Bots online saem com o dono da sala**: é a máquina dele que os move
  (`docs/decisoes/0008`); se ele cai, os bots somem até o fim. A
  dificuldade é da sala: o dono escolhe ao criar e muda na própria sala.
- Sem entrar no meio da partida vindo de fora da sala, sem reconexão
  automática (quem cai volta para a sala).
- **Do lobo**, o uivo, a corrida na parede e os golpes especiais
  aparecem para os outros como movimento e ataque comum.
- **O perfil da loja continua local.** O servidor guarda a conta, o XP
  online e o ranking; inventário, Presas e missões ainda são os do
  arquivo do jogador (o XP da partida online entra nos dois).
- **Sem HTTPS**: ponha um proxy (Caddy, nginx) na frente antes de expor
  na internet — a senha viaja no corpo do pedido.
- O servidor guarda as salas na memória: reiniciar o contêiner
  `servidor` derruba as salas abertas (as contas e o histórico ficam).
