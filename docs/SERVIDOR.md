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
| **Servidor de partida** | O próprio jogo sem tela (`godot --headless -- --servidor`). Uma partida por vez: recebe os jogadores, repassa o que cada um faz, arbitra e entrega o resultado | `scripts/net/MatchServer.cs` |
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
| `tools/testar_servidor.sh` | 22 testes do servidor contra um PostgreSQL de verdade (sobe um descartável no Docker): banco, contas, salas, chat, partidas |
| `tools/testar.sh` | Inclui os testes de unidade do juiz (`RefereeTests`) |
| `tools/testar_online.sh` | Ponta a ponta: um servidor de partida e **dois jogos de verdade, sem tela**, criam conta, abrem sala, jogam 35 s (um atira, o outro morre e renasce) e conferem conta, placar e saguão. Precisa do servidor no ar (`docker compose up -d`, depois `docker compose stop partida`) |

O robô do teste é `scripts/net/OnlineRobot.cs` (`-- --robo=criar` /
`--robo=entrar`).

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
