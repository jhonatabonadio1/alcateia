# 0010 — O servidor em camadas (clean architecture)

## Contexto

O servidor começou como um projeto só (`servidor/Alcateia.Servidor`), com
cada arquivo fazendo tudo: `Lobby.cs` tinha as regras da sala, a trava, o
protocolo do saguão e o WebSocket; `Accounts.cs` e `Matches.cs` misturavam
a regra com o SQL. Funcionava, mas mudar uma regra (o que acontece quando
a partida é abandonada, quantas partidas cabem num servidor de partida)
pedia mexer em código de rede e de banco junto, e a regra não tinha teste
próprio — só pelo HTTP, com banco de pé.

## Decisão

Quatro projetos, cada um só conhece os de dentro:

- **`Alcateia.Dominio`** — as regras, puras: conta e nome válido, a sala
  (entrar, times, dono, bots, regras, começar), o resultado de uma
  partida (com `Rewards.cs`, o mesmo arquivo do jogo) e a frota (lugares,
  entrega, partida fechada sem resultado). Sem banco, sem rede, sem
  relógio próprio (quem chama diz a hora). Recusa é `Failure`, com um
  tipo (`FailureKind`) que não é código HTTP.
- **`Alcateia.Aplicacao`** — os casos de uso (`AccountService`,
  `LobbyService`, `FleetService`, `MatchService`) e as portas
  (`Portas/Ports.cs`): repositórios, senha, tokens, assinatura, relógio e
  a conexão do jogador. Aqui ficam as travas e quem precisa saber do quê.
- **`Alcateia.Infra`** — o PostgreSQL (cliente próprio, decisão 0009), os
  repositórios, PBKDF2, tokens, a assinatura dos ingressos.
- **`Alcateia.Servidor`** — a borda: HTTP, WebSocket, configuração, a
  tradução de `Failure` em status HTTP e a montagem de tudo (`App.cs`).
  É o programa que a imagem Docker roda.

## Por quê

- A regra muda sem tocar em rede nem banco, e pode ter teste de unidade.
- O WebSocket virou uma porta (`ILobbyConnection`): o saguão não sabe o
  que é um socket.
- O contrato com o jogo não mudou: as mesmas rotas, as mesmas mensagens,
  os mesmos testes de integração (`src/tools/testar_servidor.sh`) passando.

## Custo

Mais arquivos e um passo a mais para seguir uma chamada (rota → serviço
→ domínio → repositório). Para o tamanho do servidor, vale.
