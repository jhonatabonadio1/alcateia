# 0008 — Online: cada jogo decide o próprio corpo, o servidor decide a partida

## Contexto

O jogo nasceu offline, com tudo numa máquina: `Health.TakeDamage`
aplica o dano, `Weapon` acha o alvo, `Match` conta o placar. Para jogar
em rede, alguém precisa ser dono de cada uma dessas decisões.

O desenho "de livro" é o servidor simular tudo (corpos, tiros, vida) e
os jogos só mandarem o que apertaram — com predição, reconciliação e
compensação de lag para não parecer lento. É o mais difícil de burlar e
o mais caro de fazer: pede o mapa e a física rodando no servidor e
reescrever o movimento do jogador para aceitar correção.

## Decisão

A primeira versão online divide assim:

| O quê | Quem decide |
|---|---|
| Posição, forma, arma na mão e **vida** de um corpo | A máquina do dono |
| Se um tiro acertou | A máquina de quem atirou (a mesma `Weapon` de sempre) |
| Quanto o acerto tira | A máquina da vítima (o mesmo `Health` de sempre: escudo, cabeça, proteção) |
| Se uma morte vale como abate, o placar, o relógio, o resultado | O servidor de partida (`Referee`, C# puro em `scripts/domain`) |
| Conta, XP, ranking | O servidor do Alcateia, a partir do resultado que o servidor de partida entrega |

Os outros jogadores aparecem como **bonecos**: o corpo do bot com
`Bot.Remote` ligado, sem IA, movido pelo que a rede conta e encenando os
ataques do dono com o código de sempre (`Weapon.Puppet` não fere).

O servidor de partida (`MatchServer`) é o próprio jogo sem tela, mas
**não carrega mapa nem simula corpos**: repassa estados, leva cada
acerto até a vítima e arbitra. O juiz só aceita abate de quem acertou a
vítima nos últimos 10 s, de inimigo, com a partida valendo; recusa dano
impossível e morte repetida.

## O que isso custa

- **Não é à prova de trapaça.** Um jogo adulterado pode mentir a
  própria posição, não aplicar o dano que recebe ou dizer que acertou
  sem acertar. O juiz barra o que dá placar de graça (abate sem acerto,
  dano absurdo, abate em aliado), não mira automática nem imortalidade.
  Serve para jogar entre conhecidos; **antes de abrir ao público com
  ranking valendo, a vida e o acerto têm de ir para o servidor.**
- **Sem compensação de lag.** Quem atira acerta o que vê na própria
  tela (bom para quem atira); a vítima pode levar dano já atrás da
  parede na tela dela.
- **Os bots são da máquina do dono da sala** (13/11/2026). A regra
  "cada máquina manda no próprio corpo" foi estendida: o dono da sala
  manda também nos bots dela. A máquina dele os move e fala por eles
  (estado, ataque, acerto, morte) com pacotes que levam o número do bot;
  o servidor confere que o bot é mesmo dele e trata o bot como jogador.
  Nas outras máquinas, bot é boneco como qualquer jogador. Custos: os
  bots jogam com a latência do dono (para ele, zero; para os outros, a
  dele); **se o dono cai, os bots saem da partida** (não há troca de
  dono); e o dono, adulterando o jogo, manda nos bots como manda em si.
  Bot não é conta: não ganha XP, não tem patente, não entra no
  resultado gravado.
- Coisas do lobo que não passam pela rede ainda: o uivo, a corrida na
  parede e os golpes especiais aparecem para os outros como movimento e
  ataque comum.

## O caminho daqui

Os pontos de troca já estão isolados: `Health.Remote`/`SendHit`
(onde o dano sai), `Bot.RemoteUpdate` (onde o estado entra) e o
`Referee` (onde se decide). Levar a vida para o servidor é fazer o
`MatchServer` guardar o HP e responder ao `Hit` em vez de repassá-lo; o
passo seguinte são os quatro pilares do `CLAUDE.md` (predição,
reconciliação, interpolação, compensação de lag).
