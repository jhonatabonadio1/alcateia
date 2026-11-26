# 0018 — O desenho online anda liso: relógio no passo, desenho dos outros sem pulo, correção em mola

## Contexto

Jhon, 26/11, no alpha fechado: "microtravamentos no servidor, às vezes os
personagens voltam no tempo e a tela fica tremendo com glitches, isso não
pode acontecer, precisa ser um jogo fluido, independente da condição do
servidor".

Medido com o jogo de verdade ligado a um servidor de partida no teste
(`FluidezAutoritativo`, `FluidezRepasse`: o jogador e o outro correndo em
círculo, rede limpa, 80/150/250 ms com jitter de 40 ms e perda de 2–5%, e
o servidor parando de 0,1 a 0,3 s a cada 1,5 s), cinco causas:

1. **O relógio daqui era o de parede do sistema** (`Time.GetTicksUsec`),
   lido em qualquer ponto do quadro, e o boneco anda no passo da física:
   o instante em que um estado "chegou" e o do desenho saíam de relógios
   diferentes, e o boneco tremia.
2. **O desenho dos outros era "o agora do servidor menos 100 ms"**: o
   estado que chegava mais cedo puxava todo mundo para a frente de uma
   vez; o servidor engasgando deixava o desenho passar do último estado,
   adivinhar à frente pela velocidade e voltar quando os atrasados
   chegavam.
3. **A correção do próprio corpo** escorregava num decaimento de ~40 ms
   que começava na velocidade máxima (30 cm faziam a câmera saltar a 6
   m/s), e entrava também no deslocamento do passo que o alisamento
   reparte pelo quadro: o desenho recuava até o tamanho da correção num
   quadro e voltava no seguinte.
4. **A fila de comandos do servidor** consumia dois por passo assim que
   passava de 4: depois de um engasgo do próprio servidor (a fila enche
   porque ele deve passos) o corpo andava o dobro por passo.
5. **No repasse**, o boneco perseguia a última notícia e se adiantava pela
   velocidade: a notícia que chegava mais tarde o puxava para trás.

## Decisão

- **O relógio da partida online é o dos passos de física**
  (`NetMatch.LocalSeconds`): a física anda no tempo real, e o desenho e a
  chegada dos estados contam no mesmo relógio.
- **O desenho dos outros tem relógio próprio, que só muda de ritmo**
  (`ServerClock.RenderTime`): anda junto com o daqui, até 10% mais rápido
  ou mais devagar para alcançar o alvo (`MaxRate`), e o ritmo muda no
  máximo 6 por segundo (`RateSlew`). Nunca pula nem volta (só se ficar
  mais de 1 s longe, `SnapError`). O atraso cresce com o jitter medido
  (até +80 ms, `MaxExtraDelay`). Chegando no último estado, freia para
  parar no máximo 80 ms além dele (`Overrun`), em vez de adivinhar e
  voltar.
- **O degrau que sobrar na pose do boneco** (o que se adivinhou e o
  caminho que chegou depois) o corpo dá, o desenho não: escorrega
  (`Bot.SmoothNetJump`, só na horizontal).
- **A correção do próprio corpo escorrega numa mola criticamente
  amortecida** (`VisualSmoothing`, `CorrectionSpring` 12): sai devagar e
  chega sem passar; 30 cm somem em ~0,3 s com no máximo ~1,3 m/s a mais na
  câmera. O ponto de partida do passo anda junto com a correção. Um estado
  que chega depois de um mais novo não corrige de novo.
- **A fila do servidor só apressa depois de meio segundo funda, e
  devagar** (`CommandBuffer.DeepPatience` 30 passos, um a mais a cada 3).
- **No repasse, o estado leva o instante de quem mandou**
  (`NetState.Time`, no fim da mensagem: jogo antigo não manda nem lê) e o
  boneco é desenhado como na autoritativa: entre dois estados, no relógio
  liso de cada dono. Morte e renascimento (ou mais de 4 m entre dois
  estados) são teleporte, não deslizam.

## Consequências

- Medido depois (veja o CHANGELOG): nenhum tranco nem volta no tempo na
  câmera em nenhuma condição, com o servidor corrigindo o corpo; o boneco,
  liso de 80 a 250 ms, com um tranco no limite durante o engasgo.
- A regra de jogo não mudou: nada disto toca `Health`, `Weapon` ou
  `FormController`. É só o desenho, o relógio e o ritmo da fila.
- O atraso maior com jitter alonga a volta no tempo do tiro: com 250 ms de
  ida e volta e jitter forte ele pode encostar no limite (`MaxRewind`,
  0,3 s).
- **Ainda corrige**: o esbarrão entre dois jogadores. O jogo vê o outro no
  passado e o servidor no presente: perto um do outro, eles esbarram em
  lugares diferentes e o servidor corrige (no teste, 4 ou 5 correções de
  5 a 15 cm seguidas). Agora a correção escorrega; para não haver, seria
  preciso o corpo do inimigo não ser parede no movimento (decisão de jogo).
