# Segurança do servidor

O que o servidor faz para não ser derrubado, invadido ou enganado, como
pôr na internet sem abrir o que não deve, e o que fazer quando algo
acontece. É a aplicação, aqui, do que se aprendeu no CT. As decisões:
`docs/decisoes/0012` (painel), `0013` (dados no servidor) e `0014`
(escudo em memória).

## O caminho de um pedido

```
internet ─▶ (Cloudflare) ─▶ Caddy :443 ─▶ servidor :8080 ─▶ PostgreSQL
                                             │
equipe ──túnel SSH / VPN──▶ 127.0.0.1:8081 ──┘  (painel e /metrics)

jogador ──UDP 7777──▶ servidor de partida   (a partida em si não passa pela API)
```

Todo pedido HTTP passa primeiro pelo **escudo**
(`Alcateia.Servidor/Http/Shield.cs`), nesta ordem:

1. **Endereço real** — o do cabeçalho (`CF-Connecting-IP`,
   `X-Forwarded-For`) só se a conexão veio de um proxy de confiança
   (`ALCATEIA_TRUSTED_PROXIES`); senão, o da conexão. Cabeçalho de
   desconhecido qualquer um forja.
2. **Blacklist de IP** — endereço ou faixa bloqueada recebe 403 na hora.
3. **Painel só na porta dele** — e só para a lista `ALCATEIA_ADMIN_ALLOW`,
   se houver.
4. **Injeção no endereço** (`' OR 1=1`, `UNION SELECT`, `../`, `<script`,
   `${jndi`, `%00`): 400. **Rota de scanner** (`/.env`, `/.git`,
   `/wp-admin`, `/phpmyadmin`, `/actuator`, `*.php`…): 404.
5. **Freio**: 600 pedidos por minuto por endereço (429). Login e
   cadastro têm o deles, 12 por minuto.
6. **Cabeçalhos**: `nosniff`, `X-Frame-Options: DENY`, sem `Server`,
   `no-store`, HSTS sob HTTPS; no painel, uma política de conteúdo que só
   carrega o que vem do próprio servidor.
7. Na volta, conta as recusas (muitos 401/403/404 seguidos é
   enumeração) e registra erro 500.

Tudo isso é busca em memória: nenhum pedido vai ao banco por causa do
escudo.

## O que é detectado e o que acontece

| O quê | Como se vê | Reação |
|---|---|---|
| Força bruta | 10 logins errados do mesmo endereço em 10 min | evento grave + bloqueio do endereço |
| Uma senha em várias contas | 6 nomes diferentes do mesmo endereço em 10 min | evento grave + bloqueio |
| Login certo depois de errados | acerto depois de 3 ou mais erros do mesmo endereço | evento grave (crítico, no painel) — pode ser a senha que acertaram |
| Varredura, injeção | rotas e padrões da lista acima | evento grave; a 3ª em 10 min bloqueia |
| Enumeração | 60 recusas (401/403/404) em um minuto | evento grave + bloqueio |
| Pedidos demais | passou do freio | 429, evento, e conta para o bloqueio |
| Chave da frota errada | `X-Fleet-Key` inválida | evento grave; a 3ª bloqueia |
| Painel: senha errada | 5 erros seguidos | a conta trava 15 min (evento crítico) |
| Painel: endereço fora da lista | — | 404 e evento grave |
| Resultado de partida absurdo | mais de 8 abates por minuto, 90% na cabeça em 20+ abates, 40+ abates sem morrer | alerta na ficha do jogador — não pune sozinho |

**Bloqueio automático**: 15 minutos na primeira vez, e quatro vezes mais
a cada reincidência, até um dia. Aparece no painel (Blacklist de IP) com
a origem "Escudo" e pode ser desfeito lá. Endereços de rede interna e os
de `ALCATEIA_IP_ALLOW` nunca são bloqueados sozinhos.
`ALCATEIA_AUTO_BLOCK=0` deixa o escudo só registrando.

## O registro

Todo evento tem data, tipo, gravidade, endereço real, conta (se houver),
quem, rota e detalhe. **Sucesso também é evento** — sem o normal não se
enxerga o anormal. **Senha e token nunca são registrados.**

Os eventos ficam em dois lugares:

- na tabela `security_events` (o painel lê daqui; 90 dias,
  `ALCATEIA_EVENT_DAYS`);
- na saída do contêiner, uma linha de JSON por evento
  (`{"log":"seguranca",...}`), que o Promtail manda para o Loki
  (`docker-compose.monitor.yml`). **É a cópia que importa numa
  invasão**: quem tomar o servidor apaga a tabela, não o que já saiu
  dele. O ideal é o Loki em outra máquina.

O que a equipe faz no painel fica na **auditoria** (`audit_log`): quem,
o quê, em quem, de que endereço. O painel não tem tela para apagar nem
editar auditoria.

## Senhas, sessões e segredos

- Senha de jogador: PBKDF2-SHA256, 210 000 voltas, sal por conta. Erro de
  login é sempre "nome ou senha incorretos", exista a conta ou não.
- Login de jogador: um token aleatório; o banco guarda só o SHA-256
  dele. Banir ou derrubar apaga os tokens e a conexão cai na hora.
- Painel: senha de 12+, trava, TOTP (RFC 6238), sessão de 12 h em cookie
  `HttpOnly` + `SameSite=Strict` (+ `Secure` sob HTTPS), cabeçalho
  `X-Painel` em tudo o que muda algo.
- O segredo do TOTP fica no banco **cifrado** (AES-256-GCM) com
  `ALCATEIA_MASTER_KEY`, que só existe no `.env`: levar o banco não leva
  os segredos.
- O SQL é sempre com parâmetros separados do texto (o cliente fala o
  protocolo estendido do PostgreSQL): o valor nunca é colado no comando.
- O servidor guarda o mínimo de dado pessoal: nome de jogo, hash de
  senha e o último endereço de acesso. Nada de e-mail, CPF ou pagamento.

## O que o servidor confere (e o cliente não decide)

- **Compra**: preço, desconto, patente e saldo, numa transação que trava
  a carteira. Oito compras ao mesmo tempo com dinheiro para uma: uma
  passa (há teste).
- **Desafios**: o progresso vem do resultado que o servidor de partida
  manda; o prêmio sai uma vez por período.
- **XP e Presas de partida**: `Rewards.cs`, com teto por partida; só
  quem estava escalado recebe; partida repetida é recusada.
- **Entrada na partida**: ingresso assinado (HMAC) com validade de 3 min.
- **Combate** (só com `ALCATEIA_NETCODE=autoritativo` no servidor de
  partida, decisões 0011 e 0016): o servidor move os corpos, guarda a
  vida e confere cada acerto — de onde saiu, a arma na mão, o alcance, a
  parede no caminho (voltando o alvo no tempo), a mira, o dano máximo e o
  ritmo. A morte e o renascimento (a espera e a base do time) são
  decididos lá. Acerto recusado conta contra quem atirou; com 15 ou mais
  numa partida, alerta na ficha. **No modo de repasse (o padrão) nada
  disso vale**: vida e acerto ficam nos jogos (decisão 0008).
- **O que ainda é do cliente**, mesmo na autoritativa: a forma e a arma
  na mão, as armas do conjunto (o servidor de partida não confere se o
  jogador tem a arma), a guarda do especial do lobo e os bots (máquina
  do dono da sala). O servidor ainda manda todos os corpos para todos
  (wallhack). Veja "O que ainda vem do jogo" na decisão 0016.

## Pôr na internet

1. **`.env`** com segredos de verdade (`openssl rand -hex 32` para cada):
   `ALCATEIA_DB_PASSWORD`, `ALCATEIA_FLEET_KEY`, `ALCATEIA_MASTER_KEY`,
   `ALCATEIA_METRICS_KEY`. O servidor avisa no log se a chave da frota ou
   a chave mestra estiverem com o valor de desenvolvimento.
2. **Suba com o Caddy**:
   `docker compose -f docker-compose.yml -f docker-compose.producao.yml up -d --build`,
   com `ALCATEIA_DOMAIN` apontando para a máquina. O HTTPS é automático.
   A porta 8080 deixa de ser pública.
3. **Firewall da máquina** (ou do provedor): entram só 80 e 443 (TCP) e a
   faixa UDP das partidas (7777, ou 7777–7876 com a frota). SSH só com
   chave, de preferência por VPN. **Nunca** abra 5432 (banco), 8080 nem
   8081.
4. **Painel**: `ssh -L 8081:127.0.0.1:8081 usuario@servidor` e abra
   `http://127.0.0.1:8081`. Entre com o administrador do `.env`, troque a
   senha, ligue as duas etapas e apague `ALCATEIA_ADMIN_USER` e
   `ALCATEIA_ADMIN_PASSWORD` do `.env`. O `docker-compose.producao.yml`
   já exige as duas etapas de todos.
5. **Cloudflare (opcional, recomendado)**: domínio com o proxy ligado,
   SSL em "Full (strict)", e no `Caddyfile` descomente o bloco
   `trusted_proxies` (confira as faixas em cloudflare.com/ips). No
   firewall, deixe 80/443 só para as faixas do Cloudflare: a origem some
   da internet. As partidas são UDP e **não** passam pelo Cloudflare — o
   servidor de partida tem endereço próprio (`ALCATEIA_PUBLIC_ADDRESS`).
6. **Monitoramento**: some o `docker-compose.monitor.yml`. Prometheus lê
   `/metrics`, os alertas estão em `servidor/producao/alertas.yml`
   (servidor fora, sem servidor de partida, frota cheia, ataque em
   curso) e o Grafana (`127.0.0.1:3000`, por túnel) manda para o Telegram
   em Alerting → Contact points.
7. **Cópia do banco** todo dia, para fora da máquina:
   `docker compose exec banco pg_dump -U alcateia alcateia | gzip > alcateia-$(date +%F).sql.gz`.
   Guarde junto uma cópia do `.env` (a chave mestra), em lugar seguro.

## Quando algo acontecer

- **Ataque em curso** (alerta, ou gráfico de eventos subindo): painel →
  Segurança → Eventos, filtre por "Ataques"; clique no endereço para ver
  tudo dele; "Bloquear". Faixa inteira: `203.0.113.0/24`.
- **Conta roubada**: ficha do jogador → Derrubar conexão (os logins
  param de valer) e veja em "Alertas e eventos" de onde entraram.
- **Trapaceiro**: ficha → Alertas (resultados absurdos), Partidas e
  "Outras contas no mesmo endereço" → Banir, com o motivo.
- **Suspeita de invasão do painel**: Auditoria (o que foi feito e de
  onde). Desligue o administrador em Administradores (a sessão cai na
  hora). Pior caso, na máquina: `ALCATEIA_ADMIN_PORT=0` desliga o painel
  inteiro; `Alcateia.Servidor --admin <nome>` cria ou conserta um dono.
- **Precisa parar tudo**: painel → Configurações → Manutenção. Ninguém
  entra; quem está em partida termina.
- **Bloqueou a si mesmo**: na máquina,
  `docker compose exec banco psql -U alcateia -c "delete from ip_blocks where cidr = 'SEU.IP'"`
  (vale em até um minuto) e ponha o seu endereço em `ALCATEIA_IP_ALLOW`.
- **Vazou um segredo**: troque no `.env` e suba de novo. Chave da frota:
  troque também nos servidores de partida. Chave mestra: todos os
  administradores ligam as duas etapas de novo.

## O que ainda falta

- Vida e acerto decididos no servidor de partida (o anticheat de
  verdade) — a "leva seguinte" da decisão 0011.
- TLS entre o servidor e o banco (hoje a conversa fica na rede interna
  do Docker, na mesma máquina).
- Confirmação de e-mail e recuperação de senha (o servidor não guarda
  e-mail de propósito; recuperar conta hoje é pelo painel).
- Limite de contas criadas por endereço por dia (hoje vale o freio de 12
  por minuto e o registro de cada conta criada com o endereço).
