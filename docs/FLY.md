# Produção no Fly.io

Como o WolfLeague roda no Fly.io e como montar tudo do zero. São três
peças, cada uma um app do Fly:

| App | O que é | Como sobe |
|---|---|---|
| `wolfleague-api` | O servidor: API, saguão (WebSocket), painel | `fly deploy -c fly.servidor.toml` (o lançamento faz) |
| `wolfleague-partida` | A frota de servidores de partida (o jogo sem tela, UDP) | máquinas criadas por `src/tools/fly/frota.py` |
| `wolfleague-db` | O Postgres 16 (imagem oficial, num volume) | `fly deploy -c fly.banco.toml` |

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
  sim pelo `src/tools/fly/frota.py`.
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

**O atalho:** `src/tools/fly/primeira_vez.sh` faz os passos 1 a 5 abaixo
sozinho (pergunta antes do que custa dinheiro e pula o que já existe).
Os passos ficam aqui para entender o que ele faz, ou para fazer à mão.

### 1. Os apps

```bash
fly apps create wolfleague-api
fly apps create wolfleague-partida
```

### 2. O banco

```bash
fly apps create wolfleague-db
SENHA_DB=$(openssl rand -hex 24)        # guarde: o Fly não mostra segredo de novo
fly secrets set --stage -a wolfleague-db POSTGRES_PASSWORD="$SENHA_DB"
fly deploy -c fly.banco.toml -a wolfleague-db --ha=false
fly status -a wolfleague-db             # a máquina tem de estar "started"
```

É o Postgres 16 oficial (o mesmo do `docker-compose.yml`) numa máquina do
Fly, com um volume de 10 GB. Não tem serviço público: o servidor o acha em
`wolfleague-db.internal:5432`, pela rede privada, sem TLS (a rede privada
já é cifrada). O usuário e o banco (`alcateia`) nascem na primeira subida.

**Por que não `fly postgres create`:** o Postgres "automatizado" do Fly
está descontinuado e sem suporte, e o próprio `flyctl` manda usar o
Managed Postgres. O Managed Postgres custa a partir de 38 dólares por mês
(mais 0,28 por GB) e a região de São Paulo existe, mas o cliente de
Postgres do servidor (`servidor/Alcateia.Infra/Banco/Pg.cs`) ainda não
fala TLS, e o gerenciado liga SSL por padrão — adaptar isso fica para
quando houver jogador de verdade.

**Banco: o que esperar.** Uma máquina só, com volume: se a máquina cair
(ou a cada deploy do banco), o jogo fica sem banco até ela voltar. O Fly
tira um retrato (snapshot) diário do volume, guardado 30 dias aqui
(`snapshot_retention`); o próprio Fly avisa que isso não deve ser o único
backup. Antes de ter gente jogando de verdade, falta um backup lógico
(`pg_dump`) automático guardado fora do Fly.

### 3. Os segredos do servidor

```bash
FROTA=$(openssl rand -hex 32)   # o mesmo vai para o app de partidas
fly secrets set --stage -a wolfleague-api \
  ALCATEIA_DB_PASSWORD="$SENHA_DB" \
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

# A imagem: o servidor dedicado exportado (docs/SERVIDOR_DEDICADO.md).
# A primeira vez baixa o Godot e os modelos (~1,3 GB); depois fica no cache.
fly deploy -c fly.partida.toml --build-only --push --image-label v0.3.0

# Quatro máquinas paradas, portas 7777 a 7780, em São Paulo.
FLY_API_TOKEN=$(fly auth token) python3 src/tools/fly/frota.py criar 4 \
  --imagem registry.fly.io/wolfleague-partida:v0.3.0 \
  --api http://wolfleague-api.internal:8080 \
  --ip O_IPV4_ANOTADO

FLY_API_TOKEN=$(fly auth token) python3 src/tools/fly/frota.py estado
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
(`src/scripts/net/Backend.cs`, `user://online.cfg`). Para produção, o endereço
é `https://wolfleague-api.fly.dev` — ou um domínio seu: `fly certs add
api.wolfleague.com.br -a wolfleague-api` e um CNAME no DNS.

## O jogo dos testadores

O jogo exportado (não o editor) fala com o servidor que está em
`project.godot` → `wolfleague/rede/servidor` (`https://wolfleague-api.fly.dev`).
No editor e nos testes continua o `127.0.0.1:8080`.

- **Windows:** o lançamento exporta sozinho e põe o `.zip` na Release do
  GitHub — é o link que vai para os testadores. Tag com hífen
  (`v0.4.0-alpha.1`) sai como pré-lançamento.
- **Mac:** exporte no editor (Projeto → Exportar → macOS). Sai sem
  assinatura da Apple: quem abrir pela primeira vez precisa de clique
  direito → Abrir.

Os modelos de exportação (Editor → Gerenciar modelos de exportação) têm de
estar instalados no editor para exportar à mão.

## Lançar uma versão

```bash
git tag v0.4.0
git push origin v0.4.0
```

O `lancamento.yml` não roda os testes (desde 26/11: eles rodam no push do
ramo, pelo `testes.yml`, e a tag não espera por eles), sobe o servidor, espera o `/health`, constrói a
imagem da partida e troca nas máquinas. As paradas trocam na hora. As
ligadas: se o `/health` do servidor diz que não há partida em curso
(`fleet.matches` 0), trocam na hora também (reiniciam na imagem nova em
segundos) — é o caso da reserva da frota (`ALCATEIA_FLEET_RESERVE`), que
nunca desliga sozinha. Com partida em curso, ele espera elas desligarem
(até 20 min). A que não trocou a tempo fica na versão anterior e aparece
como aviso no resumo do lançamento: rode
`frota.py atualizar --imagem ... --saude https://<app>.fly.dev/health` de
novo depois. Erros passageiros da API do Fly (502, 503, sem resposta) são
repetidos algumas vezes antes de o job falhar.

O servidor sobe antes da frota: uma frota nova pode precisar de rota nova
dele, e a frota velha continua falando com o servidor novo. Ao trocar o
servidor, o saguão cai por alguns segundos (os jogadores reconectam); as
partidas em curso não, elas estão nas máquinas de partida.

## O dia a dia

| Quero... | Comando |
|---|---|
| ver a frota | `python3 src/tools/fly/frota.py estado` |
| ligar uma na mão | `python3 src/tools/fly/frota.py ligar` |
| mais servidores | `python3 src/tools/fly/frota.py criar 4 --imagem ... --api ... --ip ...` |
| abrir o painel | `https://wolfleague-api.fly.dev:8443` (reserva: `fly proxy 8081 -a wolfleague-api` e `http://localhost:8081`) |
| logs | `fly logs -a wolfleague-api` / `fly logs -a wolfleague-partida` |
| entrar no banco | `fly ssh console -a wolfleague-db` e depois `psql -U alcateia` |
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
- **O painel tem porta pública própria (8443, só HTTPS)**, declarada em
  `[[services]]` no `fly.servidor.toml`, sem túnel. A proteção é a senha de
  12+, o 2FA obrigatório e a trava por tentativas; para fechar mais,
  `fly secrets set ALCATEIA_ADMIN_ALLOW="SEU.IP/32" -a wolfleague-api`
  (fora da lista: 404 e evento grave). O `fly proxy 8081` continua
  funcionando como reserva.

## O que ainda não foi testado

Nada disto rodou no Fly ainda — foi escrito a partir da documentação. Os
pontos que mais podem dar trabalho na primeira vez:

- o construtor remoto exportando o servidor dedicado (a importação é leve,
  ~2 min; se faltar memória, `fly deploy --vm-memory` não vale para o
  construtor — aí construa local com `--local-only`);
- o servidor conectando ao Postgres por `wolfleague-db.internal` (a rede
  privada é IPv6) com a senha do segredo — se `/health` responder
  `sem_banco`, é aqui: confira `fly logs -a wolfleague-api`;
- 1 GB de memória por máquina de partida (`--memoria` no `frota.py`);
- os segredos do app chegando às máquinas criadas pela API (o log do
  passo 5 mostra).
