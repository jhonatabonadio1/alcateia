# 0016 — Vida e acerto no servidor de partida (partida autoritativa, segunda leva)

## Contexto

Com a decisão 0011 o servidor de partida passou a mover os corpos, mas
a vida e o acerto continuavam nas máquinas dos jogadores (decisão 0008):
um jogo adulterado podia dizer que acertou sem acertar — através da
parede, do outro lado do mapa, sem mirar, mais rápido e mais forte que a
arma — e não aplicar o dano que levava. O juiz só barrava o que dava
placar de graça. Desde a decisão 0015 a partida paga XP e Presas da
conta, e trapaça passou a valer dinheiro do jogo.

## Decisão

Na partida autoritativa (`ALCATEIA_NETCODE=autoritativo`):

- **A vida é do servidor.** Cada corpo no servidor tem o `Health` de
  sempre, e é ele que vale. O jogo só pode contar que tem **menos** vida
  (dano que só ele viu: uma queda, a própria granada, um bot dele); se
  diz que tem mais, o servidor não aceita. Duas vezes por segundo o
  servidor manda a cada um a vida que tem dele (`Vitals`), e o jogo desce
  a dele se estiver acima. Para os outros, o servidor repassa a vida que
  ele tem, não a que o jogo contou.
- **O acerto é conferido no servidor** (`src/scripts/net/HitCheck.cs`, contas
  em `src/scripts/domain/net/HitRules.cs`). O jogo continua achando o acerto
  na própria tela (é o que não deixa o tiro parecer atrasado) e manda,
  junto, o número do comando em que atirou e o instante em que via os
  outros. O servidor espera simular aquele comando, pega onde o atirador
  estava e para onde olhava, **volta o alvo no tempo** até onde ele
  aparecia na tela de quem atirou (`PoseHistory`, até
  `NetRates.MaxRewind` = 0,3 s) e confere:
  - **de onde saiu**: a até 1,5 m do corpo do atirador (a granada, a um
    arremesso dele);
  - **a arma**: a que estava na mão naquele comando (a forma lobo para a
    garra, a faca para a faca);
  - **o alcance** da arma, com 2 m de folga;
  - **a parede**: alguma parte do alvo (cabeça, peito, meio, pernas,
    ombros) à vista de onde o tiro saiu — o mapa do servidor é o mesmo do
    jogo, só o que colide;
  - **a mira**: o alvo dentro da mira, com o tamanho dele, a dispersão
    máxima da arma e 8° de folga;
  - **o dano**: no máximo o que a arma dá (cabeça só se a cabeça estava à
    vista; senão vale corpo);
  - **o ritmo**: um balde por atirador que enche com o dano por segundo
    da arma (cadência + 30%, todos na cabeça) e esvazia a cada acerto.

  Valendo, o dano é aplicado no corpo do servidor, a vítima recebe como
  ficou (`Damage`) e, se a vida zerou, **o servidor decide a morte** e
  anuncia o abate. O jogo da vítima não conta mais a própria morte por
  acerto de outra máquina — só a sem autor ou por um bot dela.
- **Renascer** só morto, passada a espera da partida (10 s, mais 1 por
  morte, até 15), e a até 9 m de um ponto de nascimento do próprio time.
  Os pontos saem do mapa antes de o servidor tirar os marcadores.
- **Recusa conta contra quem atirou.** O que só trapaça explica (parede,
  mira, alcance, origem, ritmo, arma) soma em `rejectedHits`, que vai no
  resultado da partida; com 15 ou mais o servidor central põe um alerta
  na ficha do jogador (`Plausibility`). Ninguém é punido sozinho: quem
  decide é uma pessoa, no painel.
- **Abates como lobo** vão no resultado (`killsWolf`), em todos os
  modos: os desafios "como lobo" e "como humano" passam a andar.

Tudo tem folga a favor de quem atira: recusar um acerto honesto é pior
para o jogo do que deixar passar um trapaceiro raspando. O que a folga
não deixa passar é o que faz a trapaça valer a pena.

## O que custa

- **Ping acima de ~200 ms** deixa de ser favorecido: quem atira vê o alvo
  mais no passado do que o servidor volta, e parte dos acertos pode ser
  recusada (o marcador de acerto aparece na tela e o dano não sai).
- O servidor gasta mais: até 6 raios por passo da janela, por acerto.
  Medido (2 núcleos): conferir 40 acertos no mesmo passo levou 2,7 a
  2,9 ms, uns 0,07 ms cada. Uma sala cheia com todo mundo atirando de
  fuzil dá uns 3 acertos por passo — 0,2 ms, com folga nos 16,7 ms.
- O jogo da vítima recebe o dano meia ida e volta depois do tiro (antes,
  também — o acerto já passava pelo servidor).

## O que ainda vem do jogo

- **A forma e a arma na mão** (no comando) e **as armas do conjunto**
  (`Status`): o servidor não confere se o jogador tem a arma que diz ter
  (o inventário é do servidor central; o de partida ainda não pergunta).
- **A guarda do especial do lobo** (`Status`): a arma do lobo só roda no
  jogo; o servidor a aplica só em lobo, com o teto da arma.
- **Os bots** continuam na máquina do dono da sala (decisão 0008): o
  tiro de um bot é conferido só pela origem e pela parede; o acerto num
  bot é conferido como o num jogador, contra onde o dono disse que o bot
  estava.
- **Wallhack**: o servidor ainda manda todos os corpos para todos.

São os itens 2 a 4 da "leva seguinte" da decisão 0011.

## Medido

- `VidaNoServidor` (teste de regressão): uma partida autoritativa de
  verdade no Armazém, com acertos honestos e de trapaça — através da
  parede, 40 m acima do corpo, com outra arma, de costas, 900 de dano,
  40 tiros no mesmo instante, vida "cheia" contada pelo jogo, morte
  contada duas vezes, renascer cedo e fora da base. Todos recusados; os
  honestos valem e matam.
- Ponta a ponta (`ALCATEIA_NETCODE=autoritativo src/tools/testar_online.sh`):
  dois jogos sem tela jogam a partida inteira com abates decididos no
  servidor; o criador manda três acertos inventados e o servidor recusa
  os três, sem recusar nenhum acerto honesto dos dois.
