# 0017 — O servidor repete o especial do jogo e põe os bots no mundo dele

## Contexto

Na partida autoritativa (0011, 0016) o servidor move o corpo de cada um
com os comandos, e o jogo é corrigido para onde o servidor diz. Em
25/11, jogando no Docker com bots, o Jhon viu o próprio lobo "não avança e
cai e fica, indo e voltando", "avança no inimigo, mata (ou não) e volta
para trás" — e "com movimentos complexos dá uma lagada, volta no tempo".
Os lobos dos outros pareciam certos: o defeito era a correção do próprio.

Medido no teste de ponta a ponta (`EspecialOnline`, `EncostaNoBotOnline`),
o servidor fazia outro movimento que o jogo, por três motivos:

1. **O especial não saía no servidor.** A carga dele é contada em cada
   máquina (abates, tempo) e o corpo do servidor nunca enchia: o Q e o
   clique chegavam e não havia salto. O jogo voava 8 m e era puxado de
   volta (45 correções, 21 m somados, a maior de 3,25 m).
2. **O salto ia para outro lugar.** O servidor não tem câmera nem trava: o
   ponto mirado e o alvo travado são da tela do jogador. O alvo, ainda,
   é visto pelo jogo uns 100 ms no passado. Contra um bot (que não tem
   corpo no servidor), 30 correções, 10,7 m somados, e o lobo terminava a
   3,6 m do alvo em vez de colado nele.
3. **O bot não existia no servidor.** No jogo o lobo bate na cápsula do
   bot; no servidor atravessava, e a correção o puxava para dentro e
   através dele (22 correções, 8,3 m somados, a maior de 71 cm). E o
   aliado, que o jogo atravessa (TeamCollision), era parede no servidor.

## Decisão

- **O comando do especial leva o que a tela do jogador decidiu**
  (`InputCommand.HasAim`/`Aim`/`AimNone`, `HasFocus`/`Focus`/`FocusAt`/
  `FocusVelocity`; até 51 bytes): o ponto mirado no passo do clique e,
  enquanto armado ou voando, o alvo travado (o lugar dele na escalação) e
  onde o jogo o via. O servidor salta para o mesmo ponto e persegue o
  mesmo alvo como o jogo o via. **Confere**: o ponto ao alcance do salto;
  o alvo inimigo, vivo, ao alcance da trava, e o ponto em que o jogo o
  via a até `ServerBody.FocusSlack` (3 m) de onde ele esteve aqui. Bot (sem
  corpo aqui): vale só a vista, dentro do alcance do voo — o mesmo que
  um ponto mirado, que o jogo já podia escolher.
- **A carga do especial do servidor segue a do jogo, com teto**: o corpo
  daqui conta os abates que ele mesmo anunciou e o tempo
  (`Weapon.RulesScoreToWin`/`RulesDuration`), e copia a carga que o jogo
  conta no `Status` só para cima, e no máximo `Weapon.ChargeSlack` (0,2)
  além do que ganhou aqui. Um jogo adulterado salta, no máximo, um pouco
  antes do que devia.
- **A arma do corpo do servidor dá o passo junto com o comando**
  (`Player.Simulate` → `Weapon.Tick`), não no relógio do servidor: com
  dois comandos num passo (a fila alcançando o jogo) o voo andava meio
  passo atrás do corpo.
- **Os bots têm cápsula no mundo do servidor** (`BotProxies`), onde o
  dono diz que estão (o `BotState` a 30 por segundo, andando com a
  velocidade dele até 0,1 s), com time e vida: o movimento trata gente
  como gente (o bote para no inimigo, o especial atravessa quem não é o
  alvo). Morto, sai; sem estado novo por 1 s (o dono caiu), sai. O dono
  já decidia onde o bot dele está (é contra isso que o tiro no bot é
  conferido): não é confiança nova.
- **Aliado atravessa no servidor também**: os corpos do servidor entram
  no par de colisão do time (`Groups.ServerBodies`, `TeamCollision`).
- **Morrer sozinho conta e aparece**: o jogo conta a causa e a arma da
  morte sem autor (`Health.LastSelfCause`; ia sempre "queda") e o
  servidor, de repasse e autoritativo, anuncia a todos com a vítima nos
  dois lados e conta a morte, sem ponto para ninguém
  (`Referee.SelfDeath`). Antes não anunciava nada.

## Consequências

- O especial, o bote e o encostar num bot quase não corrigem mais o jogo
  (medido: 0 a 1 correção, a maior de 7,5 cm). Quem vê o bot de outra
  máquina o vê 100 ms no passado e o servidor no presente: para eles, o
  encostar ainda pode corrigir um pouco (era pior: o servidor não tinha
  o bot).
- Comando e servidor andam juntos: jogo novo com servidor velho (ou o
  contrário) lê o comando do especial errado. Suba os dois.
- O jogo ainda escolhe o ponto e o alvo do especial. O servidor confere
  alcance e lugar, não a mira (como no tiro até a 0016).
