# Produção no Fly.io

Como o WolfLeague roda no Fly.io e como montar tudo do zero. São três
peças, cada uma um app do Fly:

| App | O que é | Como sobe |
|---|---|---|
| `wolfleague-api` | O servidor: API, saguão (WebSocket), painel | `fly deploy -c fly.servidor.toml` (o lançamento faz) |
| `wolfleague-partida` | A frota de servidores de partida (o jogo sem tela, UDP) | máquinas criadas por `tools/fly/frota.py` |
| `wolfleague-db` | O Postgres | `fly postgres create` |

Os nomes são o padrão dos arquivos; se o nome que você quer já estiver
tomado no Fly, troque em `fly.servidor.toml`, `fly.partida.toml` e nas
variáveis do GitHub (`FLY_APP_API`, `FLY_APP_PARTIDA`).

## Como a frota sob demanda funciona

```
jogador ──"começar"──▶ wolfleague-api ──falta lugar?──▶ API de máquinas do Fly
                         │      ▲                         "liga a partida-7779"
                         │      │ bate ponto a cada 2 s          │
                         ▼      │                                ▼
                 ingresso + IP:porta ◀───────────── máquina partida-7779 (UDP 7779)
                                                     ociosa 10 min → "posso ir?"
                                                     → sim → encerra, máquina para
```

- **A frota é um conjunto de máquinas criadas paradas.** Parada, uma
  máquina do Fly não cobra CPU nem memória, só o disco. Quatro máquinas
  paradas = quatro servidores prontos para ligar em segundos.
- **Cada máquina tem a sua porta UDP** (7777, 7778, 7779...) no mesmo IPv4
  dedicado. O Fly entrega o pacote da porta 7779 só à máquina que declarou
  a 7779 — é assim que o jogador cai no servidor certo. Por isso as
  máquinas não sobem com `fly deploy` (que daria a mesma porta a todas), e
  sim pelo `tools/fly/frota.py`.
- **Quem liga é o servidor do jogo.** Quando os lugares livres ficam abaixo
  de `ALCATEIA_FLEET_RESERVE` (1 no `fly.servidor.toml`), ou quando uma sala
  pede para começar e não acha lugar, ele liga uma máquina parada pela API
  de máquinas (`servidor/Alcateia.Infra/Fly/FlyMachines.cs`). Liga no
  máximo uma a cada 45 s, o tempo de ela subir e bater ponto.
- **Quem desliga é a própria máquina.** Sem partida nenhuma por
  `ALCATEIA_IDLE_EXIT` segundos (600 = 10 min), ela pergunta ao servidor
  (`POST /v1/fleet/leave`) se pode ir. O servidor diz não se ela for o
  último lugar livre da reserva; dizendo sim, já a tira da lista (nenhuma
  partida nova cai nela no caminho) e ela encerra com código 0. A máquina
  tem `restart = on-failure`: saiu com 0, fica parada; caiu, sobe de novo.
- **O teto é o número de máquinas.** Acabaram as paradas, a sala recebe
  "sem servidor livre" e o log diz `Frota: falta lugar e não há máquina
  parada`. Para crescer: `frota.py criar 4 ...` de novo.

Cada máquina roda até `ALCATEIA_MATCHES` partidas (4). Com reserva 1, de
madrugada fica uma máquina ligada esperando; no pico, liga quantas forem
precisas.

## Montar do zero (uma vez)

Precisa do `flyctl` (`brew install flyctl`) e de `fly auth login`.

### 1. Os apps

```bash
fly apps create wolfleague-api
fly apps create wolfleague-partida
```

### 2. O banco

```bash
fly postgres create --name wolfleague-db --region gru
fly postgres attach wolfleague-db -a wolfleague-api \
  --database-name alcateia --database-user alcateia
```

O `attach` imprime uma `DATABASE_URL` do tipo
`postgres://alcateia:SENHA@wolfleague-db.flycast:5432/alcateia`. Guarde a
SENHA para o passo 3. (Ele também grava `DATABASE_URL` como segredo do
app; o servidor não usa, pode apagar com `fly secrets unset DATABASE_URL
-a wolfleague-api`.)

> É o Postgres "não gerenciado" do Fly, falado pela rede privada sem TLS
> (a rede privada já é cifrada). O **Managed Postgres** do Fly liga SSL
> por padrão, e o cliente de Postgres do servidor
> (`servidor/Alcateia.Infra/Banco/Pg.cs`) ainda não fala TLS — para usar o
> gerenciado, falta isso.

### 3. Os segredos do servidor

```bash
FROTA=$(openssl rand -hex 32)   # o mesmo vai para o app de partidas
fly secrets set --stage -a wolfleague-api \
  ALCATEIA_DB_PASSWORD='SENHA' \
  ALCATEIA_FLEET_KEY="$FROTA" \
  ALCATEIA_MASTER_KEY="$(openssl rand -hex 32)" \
  ALCATEIA_ADMIN_USER='jhon' \
  ALCATEIA_ADMIN_PASSWORD='uma-senha-forte' \
  ALCATEIA_FLY_TOKEN="$(fly tokens create deploy -a wolfleague-partida)"
```

`ALCATEIA_FLY_TOKEN` é o que deixa o servidor ligar máquinas — e só do app
de partidas. A `ALCATEIA_MASTER_KEY` cifra o segredo das duas etapas no
banco: perdeu, os administradores refazem o 2FA. Guarde as duas chaves num
cofre (1Password, Bitwarden).

### 4. O servidor no ar

```bash
fly deploy -c fly.servidor.toml --ha=false
curl https://wolfleague-api.fly.dev/health
```

`--ha=false`: uma máquina só. O saguão e a frota ficam em memória; duas
máquinas seriam dois saguões que não se enxergam.

### 5. A frota

```bash
# O IPv4 dedicado (UDP não funciona no IPv4 compartilhado). Cobrado à parte.
fly ips allocate-v4 -a wolfleague-partida
fly ips list -a wolfleague-partida          # anote o IPv4

fly secrets set --stage -a wolfleague-partida ALCATEIA_FLEET_KEY="$FROTA"

# A imagem (o Godot importa os assets no construtor remoto: demora).
fly deploy -c fly.partida.toml --build-only --push --image-label v0.3.0

# Quatro máquinas paradas, portas 7777 a 7780, em São Paulo.
FLY_API_TOKEN=$(fly auth token) python3 tools/fly/frota.py criar 4 \
  --imagem registry.fly.io/wolfleague-partida:v0.3.0 \
  --api http://wolfleague-api.internal:8080 \
  --ip O_IPV4_ANOTADO

FLY_API_TOKEN=$(fly auth token) python3 tools/fly/frota.py estado
```

Em um ou dois minutos o servidor nota que não há lugar livre (reserva 1) e
liga uma. Confira com `fly logs -a wolfleague-partida`: tem de aparecer
`Servidor de partida ... na porta 7777/UDP`. Se aparecer `falta
ALCATEIA_FLEET_KEY`, o segredo do passo acima não chegou à máquina.

### 6. O GitHub

No repositório: Settings → Secrets and variables → Actions.

- Segredo `FLY_API_TOKEN`: `fly tokens create org` (um token que faz deploy
  nos dois apps). Ele vale para a organização inteira; se quiser algo mais
  estreito, crie uma organização só para o jogo.
- Variáveis `FLY_APP_API` e `FLY_APP_PARTIDA`: só se os nomes não forem os
  padrão.

### 7. O jogo apontando para lá

O jogo fala com `http://127.0.0.1:8080` até alguém trocar
(`scripts/net/Backend.cs`, `user://online.cfg`). Para produção, o endereço
é `https://wolfleague-api.fly.dev` — ou um domínio seu: `fly certs add
api.wolfleague.com.br -a wolfleague-api` e um CNAME no DNS.

## Lançar uma versão

```bash
git tag v0.4.0
git push origin v0.4.0
```

O `lancamento.yml` roda os testes (inclusive a checagem de que a semente
do painel está em dia), sobe o servidor, espera o `/health`, constrói a
imagem da partida e troca nas máquinas. As paradas trocam na hora; as
ligadas estão com partida, então ele espera elas desligarem sozinhas (até
20 min). A que não desligou a tempo fica na versão anterior e aparece como
aviso no resumo do lançamento: rode `frota.py atualizar --imagem ...` de
novo depois.

O servidor sobe antes da frota: uma frota nova pode precisar de rota nova
dele, e a frota velha continua falando com o servidor novo. Ao trocar o
servidor, o saguão cai por alguns segundos (os jogadores reconectam); as
partidas em curso não, elas estão nas máquinas de partida.

## O dia a dia

| Quero... | Comando |
|---|---|
| ver a frota | `python3 tools/fly/frota.py estado` |
| ligar uma na mão | `python3 tools/fly/frota.py ligar` |
| mais servidores | `python3 tools/fly/frota.py criar 4 --imagem ... --api ... --ip ...` |
| abrir o painel | `fly proxy 8081 -a wolfleague-api` e `http://localhost:8081` |
| logs | `fly logs -a wolfleague-api` / `fly logs -a wolfleague-partida` |
| entrar no banco | `fly postgres connect -a wolfleague-db -d alcateia` |
| mudar a reserva | `ALCATEIA_FLEET_RESERVE` no `fly.servidor.toml` e lançar |

(`frota.py` precisa de `FLY_API_TOKEN` no ambiente: `export
FLY_API_TOKEN=$(fly auth token)`.)

## Variáveis novas

Servidor:

| Variável | O que faz | Padrão |
|---|---|---|
| `ALCATEIA_FLY_APP` | O app das máquinas de partida. Com o token, liga a frota sob demanda | — |
| `ALCATEIA_FLY_TOKEN` | Token que pode ligar as máquinas desse app (segredo) | — |
| `ALCATEIA_FLY_API` | A API de máquinas | `https://api.machines.dev` |
| `ALCATEIA_FLEET_RESERVE` | Lugares livres que a frota mantém de pé | `0` (liga só quando falta) |

Servidor de partida:

| Variável | O que faz | Padrão |
|---|---|---|
| `ALCATEIA_BIND` | Em que endereço a porta UDP abre. No Fly: `fly-global-services` | todos |
| `ALCATEIA_IDLE_EXIT` | Segundos ocioso antes de pedir para desligar | `0` (nunca) |
| `FLY_MACHINE_ID` | O Fly põe sozinho; vira o id do servidor no painel | — |

O `frota.py` já põe `ALCATEIA_BIND`, `ALCATEIA_IDLE_EXIT`,
`ALCATEIA_GAME_PORT`, `ALCATEIA_API` e `ALCATEIA_PUBLIC_ADDRESS` em cada
máquina.

## Por que assim (o que o Fly exige)

- **UDP precisa de IPv4 dedicado**, e o servidor tem de abrir a porta no
  endereço `fly-global-services`, não no curinga — senão o Linux responde
  do endereço errado e o jogador nunca recebe a resposta. É o
  `ALCATEIA_BIND`.
- **O endereço do jogador** chega no cabeçalho `Fly-Client-IP`; o
  servidor só acredita nele vindo do proxy do Fly (`172.16.0.0/12` em
  `ALCATEIA_TRUSTED_PROXIES`). No `X-Forwarded-For` do Fly o último
  endereço é o IP do próprio app, por isso o `Fly-Client-IP` vem antes.
- **O painel não tem porta pública.** Ele escuta na rede privada
  (`ALCATEIA_ADMIN_BIND = "::"`) e só se chega por `fly proxy`, que passa
  pelo WireGuard da sua conta.

## O que ainda não foi testado

Nada disto rodou no Fly ainda — foi escrito a partir da documentação. Os
pontos que mais podem dar trabalho na primeira vez:

- o construtor remoto importando os assets do Godot (imagem grande, build
  demorado; se faltar memória, `fly deploy --vm-memory` não vale para o
  construtor — aí construa local com `--local-only`);
- 1 GB de memória por máquina de partida (`--memoria` no `frota.py`);
- os segredos do app chegando às máquinas criadas pela API (o log do
  passo 5 mostra).
