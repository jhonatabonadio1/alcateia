# 0011 — O servidor move os corpos (partida autoritativa, primeira leva)

## Contexto

Na decisão 0008 cada jogo manda no próprio corpo, e o servidor de partida
só repassa e arbitra. Não tem lag nenhum — e é fácil de burlar: um jogo
adulterado diz onde está, quanto dano deu e que não morreu. Antes do
anti-cheat, o servidor precisa mandar no que importa, sem o jogador
sentir atraso. É o desenho do Overwatch e dos jogos competitivos: o
servidor simula, o jogo adivinha e corrige.

## Decisão

Um modo novo da partida online, ligado no servidor de partida por
`ALCATEIA_NETCODE=autoritativo` (o padrão continua o repasse da 0008 até a
segunda leva ficar pronta). O servidor diz o modo no `Welcome`, e o jogo
se adapta.

**Ritmo.** O servidor anda a 60 passos por segundo, o mesmo da física do
jogo: cada comando do jogador é um passo, nos dois lados. O estado do
mundo vai a cada dois passos (30 por segundo). Os números estão em
`scripts/domain/net/NetRates.cs`.

**Comandos.** O jogo manda, a cada passo, o que o jogador apertou e para
onde olhava (`InputCommand`, 17 bytes), junto com os três anteriores — um
pacote perdido não perde o passo. O servidor guarda numa fila por jogador
(`CommandBuffer`): começa com dois de folga, consome um por passo, repete
o último segurado se faltar (sem repetir pulo nem tiro), pula buraco que
não fecha e consome dois por passo se o jogo correr na frente.

**Simulação.** Cada partida tem o próprio mundo de física (`MatchWorld`, um
`SubViewport` com mundo próprio, sem desenhar nada), com o mapa reduzido
ao que colide (malhas, luzes, navegação e rotas saem) e um `Player` por
jogador em modo `Driven`: o mesmo código de movimento do jogo, movido por
`Player.Simulate`, sem câmera, som, modelo nem HUD. O servidor guarda um
segundo de história de cada corpo (`PoseHistory`), base da volta no tempo
do tiro.

**Predição e correção.** O jogo continua movendo o próprio corpo na hora
(a entrada já comprimida como vai no pacote, para o passo ser idêntico) e
guarda onde ficou em cada comando (`Prediction`). O estado do mundo traz o
número do último comando dele que o servidor simulou; se o servidor pôs o
corpo em outro lugar (mais de 3 cm), o corpo de agora anda a diferença e o
desenho escorrega até lá em ~0,1 s (`VisualSmoothing.Absorb`). Correção
por deslocamento, não por re-simulação: é exata enquanto o movimento não
depende de onde se está, e o próximo estado corrige o resto.

**Os outros.** São desenhados 100 ms no passado, entre dois estados que
já chegaram (`InterpolationBuffer`); faltando estado, adivinha 100 ms à
frente pela velocidade e para. O relógio do servidor é estimado pelos
estados que chegam (`ServerClock`).

**Formato.** Binário enxuto, sem alocar no servidor (`PacketWriter` sobre
um buffer reaproveitado): posição em cm (3 bytes por eixo), velocidade em
cm/s, ângulos em 2 bytes, cada corpo em 24 bytes. O corpo do estado do
mundo é escrito uma vez e cada jogador recebe o cabeçalho dele.

## O que ainda vem do jogo nesta leva

A forma, a arma na mão, a vida (pacote `Status`, 10 por segundo), se está
morto e onde renasceu (no próprio comando, com o número da vida). O
servidor copia — ainda dá para mentir sobre isso. A classe de lobo e as
tatuagens vão no `Loadout` (o servidor monta o lobo igual, sem conferir se
o jogador as tem). O tiro, o dano e os bots são como na 0008.

## Medido

- Mesmo passo: andando, virando, pulando, agachando e andando devagar, com
  100 ms de atraso e um pacote a cada sete perdido, o corpo do servidor
  fica **exatamente** onde o jogo previu; discordando (o servidor empurra o
  corpo), o jogo corrige uma vez e volta a bater (`PredicaoNoServidor`).
- Custo: 16 corpos, 0,4 ms por passo; 64 corpos (quatro partidas), 1,5 ms
  (`PassoDoServidor`, numa máquina de 2 núcleos). O passo tem 16,7 ms.
- Ponta a ponta: dois jogos sem tela jogam uma partida inteira no modo
  autoritativo, com bots, tiros, abates e resultado
  (`ALCATEIA_NETCODE=autoritativo tools/testar_online.sh`).

## A leva seguinte

1. ~~A vida e o dano no servidor; o tiro conferido lá, voltando no tempo
   (`PoseHistory`, até `NetRates.MaxRewind`).~~ Feito na decisão 0016
   (20/11/2026), junto com o renascimento conferido (a espera e a base do
   time).
2. A forma, a arma na mão e o renascimento decididos pelo servidor.
3. Os bots no servidor (deixam de ser da máquina do dono da sala).
4. Não mandar inimigo que o jogador não pode ver (contra wallhack).
5. Ajuste do ritmo do jogo pela folga da fila (o servidor pede para o jogo
   adiantar ou atrasar um pouco) e compressão por diferença no estado do
   mundo.
