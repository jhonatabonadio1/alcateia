# 0014 — O escudo de segurança decide em memória e registra em lote

## Contexto

Blacklist de IP, freio de pedidos e detecção de ataque precisam olhar
todo pedido. O jogo não pode ficar lento por isso: uma consulta ao banco
por pedido dobraria a latência do saguão e derrubaria o servidor justo
durante um ataque.

## Decisão

- A **blacklist** fica em memória (um conjunto de endereços e uma lista
  curta de faixas), trocada inteira quando muda e relida do banco uma
  vez por minuto. Conferir um endereço é uma busca em dicionário.
- As **contagens** (pedidos por minuto, logins errados, nomes tentados,
  recusas) ficam em memória, por endereço. Somem ao reiniciar — é freio,
  não registro.
- Os **eventos** vão para uma fila; uma tarefa só grava em lote (até 500
  por vez) e cada um sai também como linha de JSON na saída padrão, de
  onde o Loki recolhe. Fila cheia descarta e conta
  (`wolfleague_security_events_dropped_total`): o registro nunca segura
  um pedido.
- Endereço de **rede interna** não é bloqueado nem freado sozinho: atrás
  de um proxy mal configurado todo mundo chegaria com o endereço dele, e
  um ataque derrubaria o jogo para todos.
- O endereço real só vem de cabeçalho quando a conexão é de um **proxy
  de confiança** (`ALCATEIA_TRUSTED_PROXIES`).
- A **partida em si não passa por aqui**: é UDP direto no servidor de
  partida. O escudo protege contas, saguão, loja e painel.

## O que custa

- Com várias cópias do servidor central (hoje é uma), cada cópia teria
  as próprias contagens; a blacklist, por estar no banco, vale para todas
  em até um minuto.
- Um bloqueio automático perde a "reincidência" ao reiniciar (recomeça
  em 15 minutos).
