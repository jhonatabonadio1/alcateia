# Tutorial com a Iris — roteiro

Treinamento opcional, oferecido ao criar a conta e sempre disponível
depois (Jogar → Treinamento). O jogador joga de verdade, num cenário
vazio com bonecos de treino; a **Iris** (a locutora do jogo) manda, ele
faz, e o jogo só avança quando ele fez. Modelo: a Athena do Overwatch.

## Regras do roteiro

- **Tom da Iris:** segura, calma, de arena, frases curtas, sem piada
  exagerada. Elogio seco ("Good.", "Clean."). Nunca explica duas coisas
  numa fala só.
- **Fala em inglês** (a voz da Iris é gerada em inglês, como as de
  `docs/AUDIO.md`); a **legenda** sai no idioma do jogo (`textos.csv`).
- **Uma ação por etapa.** Cada etapa tem: a fala, o que o jogo espera, e
  uma dica que toca se o jogador não fizer nada em 12 s (e de novo, mais
  clara, aos 25 s). Aos 40 s sem progresso, o jogo mostra a tecla na tela.
- **Sempre dá para sair:** Esc → "Sair do treinamento". Quem sai no meio
  retoma da última etapa concluída.
- **Duração-alvo:** 5 a 6 minutos, sem pressa. Cada bloco termina com um
  "ponto de salvamento".
- Teclas entre `[ ]` na legenda mostram a tecla **configurada** pelo
  jogador, não a de fábrica.

## Entrada (ao criar a conta)

Tela depois do primeiro login, antes do lobby:

> **Quer um treinamento rápido?** A Iris mostra o básico em 5 minutos.
> [ Começar ]   [ Agora não ]

"Agora não" leva ao lobby e deixa um aviso discreto em Jogar →
Treinamento. Quem concluir ganha a recompensa (ver o fim).

## Blocos e etapas

### Bloco 1 — Mover e olhar (humano)

| # | Iris fala (EN) | Legenda (PT) | O jogo espera | Dica aos 12 s |
|---|---|---|---|---|
| 1.0 | "Welcome to the arena. I'm Iris. I'll be your voice out there." | "Bem-vindo à arena. Eu sou a Iris. Vou ser a sua voz lá fora." | 3 s de fala, sem ação | — |
| 1.1 | "Look around. Move your mouse." | "Olhe em volta. Mexa o mouse." | A câmera girou 90° | "Move the mouse to look." |
| 1.2 | "Good. Now walk to the marker." | "Bom. Agora vá até a marca." | Chegou ao marcador (WASD) | "Use W, A, S and D." |
| 1.3 | "Jump the gap." | "Pule o vão." | Passou o vão com Space | "Press space to jump." |
| 1.4 | "Crouch under the beam." | "Agache sob a viga." | Passou agachado (Ctrl) | "Hold crouch to go under." |
| 1.5 | "Hold shift to walk quietly. Enemies can't hear you." | "Segure Shift para andar em silêncio. Ninguém ouve você." | 3 s andando devagar | "Hold shift while you move." |
| 1.6 | "Clean. That's the basics." | "Limpo. É o básico." | — (ponto de salvamento) | — |

### Bloco 2 — Armas (humano)

| # | Iris fala (EN) | Legenda (PT) | O jogo espera | Dica |
|---|---|---|---|---|
| 2.0 | "Time to arm up. Press 1 for your primary weapon." | "Hora de se armar. Aperte 1 para a arma principal." | Slot 1 equipado | "Press 1." |
| 2.1 | "Hit the three targets." | "Acerte os três alvos." | 3 alvos derrubados | "Left click to fire." |
| 2.2 | "You're running low. Reload." | "A munição acabou. Recarregue." | Recarga concluída (R) | "Press R to reload." |
| 2.3 | "Zoom in. Hold the right button, then hit the far target." | "Mire de perto. Segure o botão direito e acerte o alvo distante." | Alvo longe atingido com mira | "Hold right click to aim." |
| 2.4 | "Switch to your pistol. Press 2." | "Troque para a pistola. Aperte 2." | Slot 2 equipado | "Press 2." |
| 2.5 | "It's slower, but it hits hard. Two shots do the job." | "É mais lenta, mas bate forte. Dois tiros resolvem." | 1 boneco derrubado com 2 tiros | — |
| 2.6 | "Close range? Melee. Press V." | "Perto demais? Coronhada. Aperte V." | Coronhada acertou o boneco | "Press V to strike." |
| 2.7 | "Your knife is on 4." | "A faca está no 4." | Slot 4 e um golpe | "Press 4, then click." |
| 2.8 | "Grenade. Press 5, then throw it behind the wall." | "Granada. Aperte 5 e jogue atrás da parede." | Granada explodiu atrás da parede, no alvo | "Press 5, then click to throw." |
| 2.9 | "Good. That's your arsenal." | "Bom. Esse é o seu arsenal." | — (ponto de salvamento) | — |

### Bloco 3 — Virar lobo

| # | Iris fala (EN) | Legenda (PT) | O jogo espera | Dica |
|---|---|---|---|---|
| 3.0 | "Now the part that makes us different." | "Agora, o que nos faz diferentes." | — | — |
| 3.1 | "Press 3. Become the wolf." | "Aperte 3. Vire o lobo." | Transformação concluída (o puff) | "Press 3." |
| 3.2 | "Strong. Fast. Hard to kill. But no guns." | "Forte. Rápido. Difícil de matar. Mas sem armas." | — | — |
| 3.3 | "Claws. Click to attack." | "Garras. Clique para atacar." | 3 golpes nos bonecos | "Left click to slash." |
| 3.4 | "Double-tap forward and hold. You'll run on all fours." | "Toque duas vezes para frente e segure. Você corre de quatro." | Corrida de quatro 2 s | "Tap W twice and hold." |
| 3.5 | "Charge a leap. Hold the right button, aim, release." | "Carregue um salto. Segure o botão direito, mire e solte." | Salto concluído | "Hold right click to charge." |
| 3.6 | "Strike from above. Press right click in the air." | "Ataque de cima. Botão direito no ar." | Bote no boneco | "In the air, press right click." |
| 3.7 | "The wolf's best trick. Jump at the wall while holding forward." | "O melhor truque do lobo. Pule contra a parede segurando W." | Corrida na parede 2 s | "Jump toward the wall, hold W." |
| 3.8 | "Hold attack to grab the wall. Jump when you're ready." | "Segure o ataque para agarrar a parede. Salte quando quiser." | Agarrou e saltou | "Hold attack on the wall." |
| 3.9 | "Hold R to howl. Your shield comes back." | "Segure R para uivar. Seu escudo volta." | Uivo completo | "Hold R until it ends." |
| 3.10 | "Now the special. Press Q." | "Agora o especial. Aperte Q." | Q usada (carga cheia só no treino) | "Press Q." |
| 3.11 | "It charges with your kills and slowly on its own. Use it well." | "Ele carrega com os seus abates e, devagar, sozinho. Use bem." | — (ponto de salvamento) | — |

### Bloco 4 — Em chamas

| # | Iris fala (EN) | Legenda (PT) | O jogo espera | Dica |
|---|---|---|---|---|
| 4.0 | "Three more targets are coming. Take them down fast." | "Vêm mais três alvos. Derrube rápido." | Bonecos aparecem em sequência | — |
| 4.1 | (a chama azul acende; toca o brilho e a fala do personagem: "I'm on fire") | "Você está em chamas." | 3 abates seguidos dentro do prazo | "Kill them one after another." |
| 4.2 | "That's what we call being on fire. Keep winning and it earns you more." | "É isso que chamamos de estar em chamas. Continue vencendo e rende mais." | — | — |
| 4.2b | "See the number in the corner? Those are your kill points. Every kill adds to it." | "Veja o número no canto? São os seus kill points. Cada abate soma nele." | O contador de KP do HUD brilha 3 s | — |
| 4.2c | "You spend them when you respawn, to pick a better loadout. Die, and you keep the points." | "Você gasta ao renascer, para escolher um conjunto melhor. Morreu? Os pontos ficam." | — | — |
| 4.3 | "Hold E any time to see your abilities." | "Segure E quando quiser para ver as suas habilidades." | Guia aberto 1 s | "Hold E." |
| 4.4 | "Hold Tab for the scoreboard. Z opens the team radio." | "Segure Tab para o placar. Z abre o rádio do time." | Tab e Z usados | "Press Tab, then Z." |

### Bloco 5 — Conjuntos, no jogo (tela de renascer)

Sem inventário e sem lobby: a Iris só mostra como escolher o conjunto
**dentro da partida**, na tela de renascer. O jogo faz o jogador "cair"
de propósito (um boneco de treino o derruba) para a tela abrir.

| # | Iris fala (EN) | Legenda (PT) | O jogo espera | Dica |
|---|---|---|---|---|
| 5.0 | "You will fall. Everyone does. What matters is how you come back." | "Você vai cair. Todo mundo cai. O que importa é como volta." | Um boneco derruba o jogador | — |
| 5.1 | "This is your respawn. Pick a loadout." | "Este é o seu renascer. Escolha um conjunto." | Tela de renascer aberta | — |
| 5.2 | "Each set has a price. You pay with your kill points." | "Cada conjunto tem um preço. Você paga com os seus kill points." | — | — |
| 5.3 | "Grayed out means you need more kill points. It shows how many." | "Cinza quer dizer que faltam kill points. Aparece quantos." | — | — |
| 5.4 | "Press 1 to 4 to choose. Or the wolf, if you want to come back as one." | "Aperte de 1 a 4 para escolher. Ou o lobo, se quiser voltar como ele." | Conjunto escolhido | "Press a number to choose." |
| 5.5 | "Be quick. In five seconds I'll pick your last one for you." | "Seja rápido. Em cinco segundos eu escolho o último que você usou." | Escolheu, ou 5 s passaram | — |
| 5.6 | "Good. The kill points are spent when you respawn, not before." | "Bom. Os kill points são gastos ao renascer, não antes." | Renasceu | — |
| 5.7 | "Sets are built outside the match. Not here." | "Os conjuntos são montados fora da partida. Aqui não." | — (ponto de salvamento) | — |

**KP no treino:** os abates dos blocos 2 a 4 já enchem o contador. Antes
da queda, o jogo ajusta o saldo para o bastante para pagar um conjunto e
deixar outro cinza (o treino não mexe nos KP de partidas de verdade).

### Fechamento

| # | Iris fala (EN) | Legenda (PT) |
|---|---|---|
| 6.0 | "That's everything I can teach you." | "É tudo o que posso ensinar." |
| 6.1 | "The rest, you learn out there. Good hunting." | "O resto você aprende lá fora. Boa caça." |

Em seguida: tela de recompensa (valor a decidir) e botão **Jogar**.

## Falas extras (reservas)

| Quando | Fala (EN) |
|---|---|
| Acertou na hora | "Clean." / "Nice." / "Exactly." |
| Errou 3 vezes seguidas | "Take your time." |
| Ficou parado 40 s | "Still with me?" |
| Pulou uma etapa | "Skipping ahead. Fine." |
| Voltou ao treinamento depois | "Welcome back. Pick up where we left off?" |

## Arquivos de voz a gerar

Uma fala por arquivo, mesma voz da Iris, sem efeito (ver
`docs/SONS_IA.md`). Nome: `voz_tutorial_<etapa>.wav`, por exemplo
`voz_tutorial_1_0.wav`, `voz_tutorial_2_3.wav`. As dicas ficam em
`voz_tutorial_<etapa>_dica.wav`. Total aproximado: 55 falas principais e
25 dicas, todas de 1 a 5 s.

## Como encaixar no jogo

- **Cenário:** um mapa próprio de treino (`scenes/maps/Treino.tscn`) com
  marcas no chão, um vão, uma viga, uma parede alta para o lobo, bonecos
  (`scripts/debug/Dummy.cs`) e alvos a distâncias diferentes. Não entra na
  rotação de mapas das salas.
- **Roteiro como dados:** um arquivo (`assets/tutorial/roteiro.json`) com
  as etapas: `id`, `fala`, `legenda`, `espera` (tipo do evento), `dica`.
  Assim a Iris se ajusta sem mexer em código.
- **Eventos que a etapa espera:** o jogo já tem a maioria (tiro, recarga,
  troca de arma, transformação, abate, chama, uivo, parede). Faltam só os
  de "chegou na marca" (zona), "passou o vão" e "granada acertou no alvo".
- **Sem servidor:** o treino roda offline, sem partida. Só a recompensa
  e o "concluído" precisam do servidor (e valem uma vez por conta).
- **Teclas:** as legendas leem as teclas configuradas (`InputSetup`),
  nunca texto fixo.

## A decidir

0. **KP = kill points** (o que o código chama de `WP`; o nome novo segue na lista de pendências). A Iris fala "kill points" por extenso. Quando o nome mudar no jogo, só as legendas e `textos.csv` mudam.
1. **Recompensa** ao concluir (um valor de Presas, uma skin, um título).
2. O tutorial pode ser **refeito** quantas vezes? (sugestão: sim, mas a
   recompensa só na primeira.)
3. O treino de **bomba** e de **papéis do time** entra depois, em outro bloco?
4. Confirmar o que cada botão faz no lobo antes de gravar a voz: o salto
   (botão direito), o bote no ar, o especial (Q) e o escudo do uivo (R).
   O roteiro segue `docs/DESIGN.md` e `InputSetup.cs`; se o jogo mudou,
   as falas 3.5 a 3.10 mudam.
5. Se a Iris também fala em **português** para quem joga em PT-BR ou se
   fica só em inglês com legenda (o jogo todo hoje é assim).
