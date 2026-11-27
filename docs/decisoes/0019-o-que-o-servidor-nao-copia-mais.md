# 0019 — O que o servidor não copia mais: forma, pressa dos comandos, tiro de bot; e o acerto avisado na hora

## Contexto

Jhon, 27/11, jogando online com 20 ms de ping: "sinto delay ao atirar em
outras pessoas", "tremidas, voltadas no tempo durante o jogo, nada disso
pode acontecer", e o pedido: o jogo estável com ping bom e ruim, servidor
rápido e lento — "nunca travar o primeira pessoa" —, "e ao mesmo tempo,
não podemos abrir brechas para cheaters".

Na partida autoritativa (0011, 0016, 0017) o servidor já move os corpos,
guarda a vida e confere o acerto. Lendo o que ele ainda **copiava** do jogo:

1. **A forma** vinha no comando e valia como vinha: um jogo adulterado
   ficava lobo a partida inteira, virava de novo sem esperar os 20 s,
   pulava a transformação (meio segundo em que se leva mais dano) e virava
   lobo em modo só de humanos.
2. **A pressa dos comandos**: a fila do servidor consumia dois comandos por
   passo sempre que passava de 4. Quem mandasse comandos em dobro andava em
   dobro (speed hack).
3. **O tiro de um bot** (o bot roda na máquina do dono da sala, decisão
   0008) só passava pela origem e pela parede: sem teto de dano, sem ritmo,
   e o dono podia pôr o bot atrás de alguém de um passo para o outro.
4. **O acerto aceito** só era contado à vítima. Os outros (e quem atirou)
   ficavam sabendo pela notícia de vida dela, que sai 10 vezes por segundo e
   passa pela máquina dela: a barra de vida e a queda do alvo chegavam até
   ~0,15 s depois do abate já decidido — o "delay ao atirar".

## Decisão

- **A forma é conferida** (`FormAudit`, C# puro, em
  `src/scripts/domain/net`): o servidor conta, em passos de comando do
  próprio jogador, o tempo de lobo (30 s), a espera (20 s) e a
  transformação (0,55 s), e sabe o que o modo da sala deixa cada time ser
  (`HostedMatch.Play`, do "play" das regras). O que não cabe é recusado: o
  corpo do servidor fica humano, anda como humano (o jogo é corrigido para
  lá) e a garra não vale ("arma que não estava na mão"). Folga de 0,75 s no
  tempo e na espera, 8 passos na transformação. Nos modos que trocam de lado
  na metade o servidor ainda copia.
- **A pressa tem cota** (`CommandBuffer`): consumir dois comandos num passo
  gasta de uma cota que enche 0,1 por passo (até 6). Um jogo honesto usa a
  pressa de vez em quando (depois de um pico de rede); em regime, ninguém
  anda mais que 10% acima do relógio. A fila que passa de 30 joga fora o
  mais velho.
- **O tiro de bot tem teto, alcance e ritmo** (`HitCheck.BotLimits`): o
  máximo da arma que o bot diz usar, o alcance dela e o mesmo balde de dano
  por segundo dos jogadores, por bot. O bot que anda mais de 30 m/s entre
  dois estados (vivo antes e depois) foi teleportado: o acerto dele não vale
  por um segundo.
- **O acerto aceito vai a todos na hora** (`NetWire.Hurt`): vítima, autor,
  dano, vida e escudo como ficaram, se caiu. O boneco do alvo cai e a barra
  desce no passo em que o servidor decide.
- **Quem atira vê o acerto sem esperar a rede** (`Health.PredictHit`): a
  barra do alvo desce e o corpo reage no quadro do tiro, com a mesma conta
  do dano. É só o que se vê: a vida é a do servidor, que confirma ou, se
  recusar, a notícia seguinte devolve (0,35 s). **Nunca prevê a morte.**
- **O estampido do tiro dos outros toca quando chega**; só o golpe de corpo
  (garra, bote, faca, coronhada) espera o atraso com que o boneco é
  desenhado — e esse atraso passa a ser o medido na hora (com jitter ele
  passa dos 0,1 s), não um número fixo.

## No mesmo dia, mais tarde: o que ficava de fora

- **Wallhack** (`HostedMatch.Sight`, regras em `SightRules`): o servidor só conta onde o inimigo está a quem pode
  vê-lo (linha de visão no mapa do servidor, do olho, por cima — o pulo e a câmera do lobo — e de onde cada um vai
  estar em 0,3 s) ou ouvi-lo (o passo, a corrida, o rugido, e por 1,5 s quem atacou). Refeito 10 vezes por segundo;
  o visto vale 0,75 s. O inimigo que o jogador não pode saber vai no estado do mundo sem o lugar (sem `Present`),
  o jogo esconde o boneco e o mostra de novo no lugar certo, sem deslizar. O estado e o ataque de um bot inimigo não
  são repassados a quem não pode saber dele (o boneco some com `Gone`). Folga sempre para o lado de mostrar.
- **Aimbot** (`AimAudit`): a mira de cada comando fica guardada; a cada acerto de arma de fogo o servidor conta se ela
  girou 20° ou mais nos três passos antes do tiro. Não recusa: vai no resultado (`aimHits`, `aimSnaps`) e o servidor
  central denuncia quem tem 25 acertos ou mais com 40% assim.
- **Bots**: o servidor soma o dano que aceitou em cada bot e decide a morte do que o dono não mata (`BotLedger`).
- **A forma nos modos que trocam na metade**: o servidor segue a metade em curso, com 2 s sem conferir em volta da
  troca. As formas recusadas vão no resultado (`refusedForms`) e viram alerta com 3.

## O que isto não fecha

- **Os bots continuam na máquina do dono da sala**: ele decide onde o bot está e em quem atira, e por isso **recebe o
  lugar de todos** (o filtro de visão não vale para ele). Fechar é pôr os bots no servidor (item 3 da 0011); o que
  isso pede está em `docs/PENDENCIAS.md`.
- **Aimbot que não dá tranco** (acompanha o alvo sem errar) não é contado.
- O instante que o jogo diz que via o alvo (até 0,3 s atrás) é aceito como vem, dentro do limite.

## Medido

- `BateriaDoServidor` (regressão): o jogo de verdade num servidor de
  partida do teste, com 20 ms; o servidor a 30 e a 20 quadros por segundo;
  sobrecarregado (40% dos quadros não rodam); ping de 150 ms com
  sobrecarga; 400 ms com jitter de 100 ms e 10% de perda, com e sem
  sobrecarga. Em todas: **câmera 0 trancos, 0 voltas no tempo, 0 travadas**,
  com o servidor corrigindo o corpo 30 cm a cada 2 s; o outro jogador, 0
  voltas no tempo.
- Subir e descer escada andando, correndo, pulando e agachando, a 20 ms:
  **nenhuma correção** (medido com um teste que saiu no mesmo dia, pela
  decisão 0020).
- `VidaNoServidor`: o aviso `Hurt` sai no passo do acerto; lobo sem
  transformar e lobo sem esperar são recusados.
- Unidade: `FormAuditHonest`, `FormAuditCheats`, `CommandBufferSpeedHack`,
  `CommandBufferHonestDrift`.
- `VidaNoServidor`: quem está atrás da parede não recebe o lugar do atirador e recebe o do aliado; atirou, passa a
  receber, e deixa de receber passado o som; o dono de bots recebe todos; o bot que diz 900 de dano vale o máximo da
  arma, 40 tiros no mesmo instante são recusados pelo ritmo, e o teleportado não acerta. 6 raios por passo com 3
  jogadores.
- Unidade: `SightRulesHearing`, `InterpolationPresence`, `AimAuditSnaps`, `BotLedgerForcesDeath`. Servidor central:
  62 de 62, com a denúncia da mira e da forma.
