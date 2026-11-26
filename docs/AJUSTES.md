# Guia de ajustes do Alcateia

Tudo o que dá para regular no jogo — altura, velocidade, golpes, armas,
granada, vida, WP, bots e partida —, com o valor atual, onde mudar e o
que acontece quando muda. Valores do repositório na v0.27.0.

> Mexeu em algum número daqui? Atualize a linha dele neste guia e, se
> for decisão de design, também no `docs/DESIGN.md`.

> **Nome na tela:** o WP daqui aparece no jogo como **KP** (kill
> points), desde 24/11; as Presas como **Cash** e o Sangue como **IPs**.
> No código, no perfil e no servidor os nomes antigos continuam.

---

## Onde os valores moram

| Onde | Como mudar | Vale para |
|---|---|---|
| **Inspetor do Godot** — campos `[Export]` dos scripts | Abra a cena (`src/scenes/player.tscn` para você, `src/scenes/bot.tscn` para os bots), selecione o nó (`Player`, `Weapon`, `Health`, `FormController`, `WolfVisual`, `Human`) e mude no Inspetor | Só aquela cena. O valor do script é o padrão; o da cena, quando existe, ganha |
| **Recursos `.tres`** | Duplo clique no Godot, ou edite o texto | `src/resources/breeds/` (classes de lobo), `src/resources/weapons/` (armas de fogo), `src/resources/bots/` (níveis de dificuldade), `src/resources/body/ruido.tres` (ruído do corpo) |
| **Código** | Edite o `.cs` | `WolfMoves.cs` (tempo dos golpes), `Catalog.cs` (tatuagens e preços), `Arsenal.cs` (granada, faca), `RoomConfig.cs` (sala padrão) |

Para mudar o padrão de todo mundo, mude no script; para mudar só o
jogador ou só os bots, mude na cena. Hoje as cenas só sobrescrevem
time, nome, classe de lobo, cena da granada, `Debug` da arma do bot e
o renascer do bot (6 s).

### Antes de mexer

- **O lobo é multiplicador do humano.** Velocidade e pulo do lobo são
  `HumanSpeed` e `JumpVelocity` vezes o `SpeedMultiplier` e o
  `JumpMultiplier` da classe. Mudou o humano, o lobo muda junto.
- **Os bots ignoram parte do `bot.tscn`.** O nível de dificuldade
  (`src/resources/bots/facil.tres`, `medio.tres`, `dificil.tres`)
  sobrescreve visão, mira, reação, velocidade e outros ao entrar na
  partida. Para esses campos, mude no `.tres` do nível (tabela na seção
  [Bots](#9-bots-e-dificuldade)).
- **Animação presa à velocidade.** O clipe toca na velocidade do corpo
  dividida pela passada medida. Acima de ~1,6× a corrida do lobo começa
  a parecer apressada; o pé não patina, mas a perna gira rápido. As
  passadas (`WalkClipSpeed`, `RunClipSpeed`, `WallClipSpeed`, `CrawlClipSpeed`, `SprintClipSpeed`,
  `WalkClip`, `RunClip`...) são medidas no esqueleto: não são botão de
  ajuste. Com os andares do Jhon (15/11 à noite, feitos na velocidade do jogo:
  tocam perto de 1×): `WalkClipSpeed` 3,95 (frente), `WalkBackClipSpeed`
  3,6, `WalkSideClipSpeed` 3,75, `CrawlClipSpeed` 1,62 (calculadas do
  arquivo e conferidas na varrida do `PassadaDoLobo`), `SprintClipSpeed` 3,95 (com
  o corpo a 5,4 m/s a corrida de quatro toca a 1,37×). Os clipes do Jhon mantêm o vaivém do quadril: tirá-lo
  fazia o pé de apoio andar junto (de frente, 0,66 m/s de patinação
  contra 0,16).
- **Alturas presas ao mapa.** O humano pula 0,99 m e sobe caixote
  (0,8 m), não contêiner (2,2 m). O lobo precisa de `JumpMultiplier`
  ≥ 1,2 para chegar ao contêiner a partir do caixote (o lobo único tem 1,435). Mexeu no pulo,
  confira isso.
- **Tem teste.** Velocidades, pulo, corrida na parede, salto
  carregado, dano da garra, granada e ruído têm teste
  (`src/tools/testar.sh`, seção Testes do CLAUDE.md). Mudou um valor de
  propósito, mude o número esperado no teste junto.

---

## 1. Movimento (humano e base do lobo)

`src/scripts/actors/player/` (Player.cs e as partes Movement, WallRun, Camera, Actions) → nó `Player` em `src/scenes/player.tscn`. No Inspector os campos aparecem em grupos: Corpo, Movimento, Corrida na parede, Câmera e mira.

| Campo | Valor | O que faz |
|---|---|---|
| `HumanSpeed` | 3,2 m/s na cena (`src/scenes/player.tscn`; o código diz 3,0) | Base da velocidade. **Base do lobo também.** É a passada do clipe de corrida: subir faz o pé patinar |
| `HumanSpeedBonus` | 1,08 | Só o humano anda isto acima da base (3,46 m/s); o lobo não muda |
| `JumpVelocity` | 4,4 m/s | Impulso do pulo do lobo (vezes `JumpMultiplier`) e base do salto da parede |
| `HumanJumpVelocity` | 4,75 m/s | Impulso do pulo do humano. Com `HumanGravityScale`, sobe 0,80 m e volta em 0,67 s |
| `HumanGravityScale` | 1,45 | Gravidade do humano no ar (o lobo usa 1). Pulo mais rápido e queda mais seca |
| `StepHeight` / `WolfStepHeight` | 0,30 / 0,45 m | Degrau que se sobe andando, sem pular (a borda do contêiner tem 14 cm) |
| `StepEase` | 2,2 m/s | Quão rápido o olho alcança o corpo depois de um degrau |
| `GroundAccel` | 60 m/s² | Aceleração no chão (alto = resposta imediata) |
| `GroundFriction` | 50 m/s² | Freada ao soltar as teclas (de 3,9 m/s para em 0,08 s) |
| `AirAccel` | 18 m/s² | Controle de direção no ar |
| `AirFriction` | 7 | Arrasto no ar sem tecla: soltar W encurta o pulo |
| `HoldFallGravity` | 0,78 | Gravidade na queda segurando o pulo (menos que 1 = cai mais devagar) |
| `WolfJumpCarry` | 0,9 | Quanto da velocidade do chão o lobo leva para o pulo |
| `CrouchSpeedFactor` | 0,45 | Velocidade agachado (as duas formas) |
| `WolfSprintFactor` | 1,495 | Corrida de quatro do lobo (W duas vezes, segurando): × a corrida normal. 7,44 m/s no lobo (medido, 26/11), o `Run_Quad` a ~1,58× (era 1,3: +15% a pedido do Jhon, 16/11) |
| `WolfChargeMoveFactor` | 0,234 | Lobo carregando o salto: × a velocidade normal. ~1,0 m/s no lobo, a passada do `Charge_Walk` (`ChargeWalkClipSpeed`, nó `WolfVisual`): mudou um, mude o outro |
| `WolfBackFactor` | 0,75 | De lobo, andando para trás: × a velocidade normal (na diagonal de trás, em parte). O humano não muda |
| `DoubleTapWindow` | 0,3 s | Tempo máximo entre os dois toques no W |
| `WalkAnimScale` / `SprintAnimScale` / `WallAnimScale` (nó `WolfVisual`) | 1 | Andando em pé e na parede: quanto a animação corre em relação ao corpo (1 acompanha, 0,7 é 30% mais lenta; a pata desliza a diferença). De quatro, o mesmo (acompanha o corpo). Só a animação. ~~Antes:~~ velocidade fixa da animação do lobo andando/correndo em pé, de quatro e na parede (1 = normal). 0 = acompanha o corpo, sem patinar |
| `WalkSpeedFactor` | 0,42 | Velocidade andando com Shift (só humano) |
| `StandHeight` / `CrouchHeight` | 1,75 / 1,0 m | Altura da cápsula (é o hitbox do corpo) |
| `StandEyeHeight` / `CrouchEyeHeight` | 0,62 / 0,1 m | Altura do olho a partir do centro da cápsula |
| `CrouchLerpSpeed` | 22 | Rapidez de agachar e levantar |
| `FreezeBrake` | 30 m/s² | Freada de quem fica preso no lugar (golpe, carga do salto) |
| `FallLimit` | −25 m | Abaixo disto volta ao spawn |
| `MouseSensitivity` | 0,002 | Sensibilidade base. O jogador multiplica nas Opções (`Settings.Sensitivity`, padrão 1) |
| `ThirdPersonDistance` | 2,5 m | Distância da câmera do lobo (era 3,5; longe demais, Jhon, 15/11) |
| `FootSink` (nó `WolfVisual`) | 0,02 m | Quanto o lobo parado em pé desce: a pata do Idle_Pose só encosta pelas garras e ele parecia flutuar |
| `TailStiffnessRoot` / `TailStiffnessTip` (nó `WolfVisual`, grupo Rabo) | 3,5 / 0,6 | A mola que devolve o rabo ao desenho do modelo, na raiz e na ponta. Mais alto: rabo duro, quase sem cair. A raiz firme segura o começo do rabo no lugar. Os números são por metro do modelo: o código multiplica pela unidade do esqueleto (100) |
| `TailGravityRoot` / `TailGravityTip` | 0,3 / 1,5 | O peso do rabo, na mesma unidade da mola. Parado, a ponta fica 3 cm abaixo e 9 cm mais perto do corpo do que no desenho |
| `TailDrag` | 0,22 | Quanto do embalo o rabo perde por passo (0 balança para sempre, 1 não balança) |
| `TailInertia` / `TailInertiaMax` / `TailInertiaSmooth` | 0,55 por g / 1,6 / 0,09 s | Quanto da aceleração do corpo vira força no rabo, o teto (o lobo arranca a 4 g) e o tempo para a força acompanhar |
| `TailAir` | 0,011 por (m/s)² | Arrasto do ar: levanta o rabo na corrida (de quatro, a 7,1 m/s, o rabo fica na horizontal, a ponta até 13 cm acima da raiz); a passo quase nada |
| `TailTurn` | 0,16 por rad/s | Quanto o giro do corpo joga o rabo para fora da curva |
| `TailRadiusRoot` / `TailRadiusTip` | 0,07 / 0,035 m | A grossura do rabo para a colisão com as pernas e o quadril (cápsulas de 11,5 cm nas coxas, 8,5 nas canelas e 9 no quadril, em `WolfVisual.Tail.cs`) |

### Escada (`Player.Ladder.cs`)

| Campo | Valor | O que faz |
|---|---|---|
| `LadderSpeed` | 3,4 m/s | Subida e descida (3,6 m em 1,1 s; era 2,2) |
| `LadderLookDown` | 0,6 rad (~35°) | Olhando mais para baixo que isto, o W desce |
| `LadderExitPush` | 2,5 m/s | Empurrão para a frente ao chegar no alto, para cima do piso |
| `LadderJumpOff` | 3 m/s | Pulo para trás que solta da escada |
| Volume (`Ladder.Size`, posto por `kit.escada`) | largura + 0,12 × altura + 1,03 × 1,05 m | Onde o corpo está na escada |

### Resultado de hoje

| | Humano | Lobo (um só, 26/11) |
|---|---|---|
| Velocidade | 3,46 m/s (faca 3,58) | 4,33 (×1,352) |
| Agachado | 1,46 | 1,95 |
| Shift | 1,36 | — |
| De quatro (W W) | — | 7,44 |
| Na parede | — | 4,94 |
| Altura do pulo | 0,99 m | 2,09 m (×1,435) |
| Avanço do pulo correndo | ~2,6 m | 5,1 m |

Medido em 26/11 (`Medidas`, `VelocidadeNoChao`, `AlturaDoPulo`,
`CorridaNaParede`). Até 25/11 havia duas classes: o Bruto (4,15 m/s, de
quatro 7,14, parede 4,75, pulo 1,67 m) e o Ligeiro (4,50, 7,73, 5,14, 2,56 m);
o lobo único fica no meio.

Cada arma também multiplica a velocidade do humano com ela na mão
(`SpeedMultiplier` da arma, seção 7): Desert Eagle 1,08, AK-74 0,92,
SCAR-H 0,88, SVD 0,86...

---

## 2. O lobo (um só) e o tempo de lobo

**As classes saíram** (decisão do Jhon, 26/11: "remover 'Classes de lobo' do
jogo, deixar apenas melhorias de lobo"). O lobo é um só, para o jogador e
para os bots: `src/resources/breeds/lobo.tres` (script
`src/scripts/forms/WolfBreed.cs`), **no meio do Bruto e do Ligeiro de antes**,
e **canhão de vidro** (decisão do Jhon, 26/11): mata rápido, aguenta pouco.
As melhorias (tatuagens, abaixo) multiplicam uma cópia dele.

| Campo | Lobo | Bruto / Ligeiro (até 25/11) | O que faz |
|---|---|---|---|
| `SpeedMultiplier` | 1,352 | 1,298 / 1,406 | × `HumanSpeed` (3,2 da cena) — 4,33 m/s no chão, 7,44 de quatro, 4,94 na parede (com `WallRunSpeedBonus`) |
| `JumpMultiplier` | 1,435 | 1,28 / 1,59 | × `JumpVelocity` — o pulo sobe 2,09 m (eram 1,67 e 2,56) |
| `Shield` | **100** | 200 / 200 | Escudo do lobo (a vida é a do humano, 200). O rugido devolve ele inteiro |
| `ShieldDecay` | 5,5 /s | 9 / 13 /s | Quanto escudo escorre por segundo: ~18 s do cheio ao zero, o meio dos 22 e 15 s de antes |
| `BodyResistance` | **1,0** | 0,6 / 0,78 | Dano no corpo × isto: o lobo leva o tiro inteiro, como o humano |
| `AttackDamage` | 65 | 75 / 55 | Base de **todos** os golpes (seção 4): a garra tira 104 de um humano, dois golpes matam |
| `AirAttackBonus` | 1,75 | 1,5 / 2,0 | × dano ao acertar caindo de ≥ 2,5 m |
| `AttackRange` | 4,1 | 5 / 3,2 | Só os bots usam: distância em que o bot lobo decide atacar |
| `WpCost` | 0 | 0 | WP para transformar |

### O tempo de lobo (`FormController`, nó `FormController` no jogador e no bot, grupo "Tempo de lobo")

Decisão do Jhon, 26/11: "um tempo limite para usar o lobo (ao passar esse
tempo volta a ser humano, deve avisar o player) e cooldown para voltar a
ser lobo, para forçar os players a jogarem com armas".

| Campo | Valor | O que faz |
|---|---|---|
| `WolfTimeLimit` | 30 s | Quanto se fica lobo de uma vez. Conta só na forma lobo (não na fumaça); acabou, volta a humano sozinho pela mesma transição da tecla 1. 0 desliga. Não vale no modo em que o time é lobo a partida toda |
| `WolfCooldown` | 20 s | A espera para virar lobo de novo, contada de quando a volta começa (pela tecla ou pelo tempo). Morrer zera o tempo e a espera |
| `WolfWarnAt` | 5 s | A quantos segundos do fim o HUD avisa: texto ("Você volta a humano em 5 s"), o bipe `contagem` e a barra vermelha pulsando |

O HUD: com o lobo, a barra de baixo esvazia ("LOBO · 24 S", e nos últimos
5 s "HUMANO EM 4 S"); na espera, ela enche ("ESPERE 12 S") e o espaço 3 da
barra de armas mostra os segundos ("12s"), apagado. Apertar 3 na espera
não transforma: avisa quanto falta ("Lobo de novo em 12 s") com o
`ui_negado`. O bot respeita a mesma espera. O relógio é o do dono do corpo:
o boneco da rede e o corpo do servidor de partida espelham a forma que o
dono manda e não voltam sozinhos.

### Tatuagens (`src/scripts/meta/Catalog.cs`)

Multiplicam o lobo do jogador (não o dos bots):

| Tatuagem | Efeito |
|---|---|
| Pata Leve | velocidade ×1,05 |
| Salto Longo | pulo ×1,06 |
| Couro Grosso | escudo ×1,10 |
| Garra Afiada | dano da garra ×1,06 |
| Pelo Denso | escudo escorre ×0,8 |

---

## 3. Corrida na parede

`Player.cs` → nó `Player`.

| Campo | Valor | O que faz |
|---|---|---|
| `WallRunSpeedBonus` | 1,1427 | Velocidade na parede = `HumanSpeed` × classe × isto |
| `WallAccel` | 18 m/s² | Aceleração ao grudar |
| `WallBrake` | 32 m/s² | Freada quando o lobo chega à parede mais rápido que a corrida dela (o ataque não agarra mais, 24/11) |
| `WallRunMaxTime` | 0 | Tempo máximo na parede (0 = sem limite) |
| `WallRunVerticalDrift` | 0 m/s | Sobe/desce na parede (0 = altura travada) |
| `WallCheckDistance` | 0,75 m | Distância máxima até a parede para grudar |
| `WallHoldDistance` | 0,34 m | Distância do corpo à parede correndo |
| `WallStickForce` | 4 m/s | Força que segura o corpo na parede |
| `WallMinHeight` | 0,18 m | Altura mínima dos pés para a parede pegar |
| `WallGrabMaxRise` | 0,6 m/s | Subindo mais rápido que isto **e** abaixo de `WallGrabHeight`, ainda não gruda |
| `WallGrabHeight` | 0,45 m | Com os pés a esta altura do chão, gruda mesmo subindo. Menor = agarra com um pulinho; maior = precisa pular mais alto |
| `WallLeaveSpeed` | 0,5 m/s | Afastando-se mais rápido que isto, não gruda |
| `WallRunClearance` | 0,7 m | Checagem à frente: bater em algo solta |
| `WallEndMargin` | 0,1 m | Quanto passa da ponta antes de decidir a quina |
| `WallWrapMinFace` | 0,25 m | Face mínima para contornar a quina (mais fina: segue reto) |
| `CornerGrace` | 0,15 s | Tempo sem encostar em nada antes de soltar |
| `WallJumpClimb` | 1,02 | Altura do salto da parede (era 1,35; −40%) |
| `WallJumpPush` | 6,5 m/s | Empurrão para fora no salto da parede |
| `WallJumpKeepSpeed` | 0,8 | Quanto da velocidade da parede o salto leva |
| `SameWallThreshold` | 0,85 | Semelhança para contar como a mesma parede. Deixou uma parede, só gruda numa diferente até tocar o chão |
| `SameWallGap` | 0,8 m | Viradas para o mesmo lado, mas mais longe que isto uma da outra, são paredes diferentes |
| `WallJumpPushFacing` | 0,3 | Fração do empurrão para fora quando se salta olhando reto para a parede (sobe mais, afasta menos) |
| `WallJumpLookLift` | 0,35 | Quanto o salto da parede sobe a mais olhando 45° para cima |
| `WallMantleReach` | 1,9 m | Topo da parede até esta altura acima dos pés: o salto da parede sobe nele |
| `WallRunCooldown` | 0,12 s | Tempo mínimo entre duas corridas |
| `WallStallTime` | 0,15 s | Correndo na parede e travado em algo à frente por este tempo: solta e cai |
| `WallStallCooldown` | 0,35 s | Depois de soltar por ter batido, o tempo sem grudar de novo |
| `WallJumpGrace` | 0,22 s | Saiu da parede sem saltar (soltou o W, a parede acabou): o pulo dentro deste tempo ainda é o salto da parede |

---

## 4. Golpes do lobo

Campos em `src/scripts/combat/weapon/` (`Weapon.WolfClaws.cs`, `.WolfPounce.cs`, `.WolfDive.cs`) → nó `Weapon`, grupos "Lobo: ..." no Inspetor. Tempos em
`src/scripts/forms/WolfMoves.cs`.

`ClawCancelAfter` (nó `Weapon`, grupo da garra) = 0,08 s: quanto depois do impacto a próxima garrada pode cortar a atual (clicando rápido, uma a cada 0,28 s; pedido do Jhon, 16/11).

**Fórmula:** dano = `AttackDamage` da classe × multiplicador do golpe
× `ClawVsHumanBonus` (1,6, se o alvo é humano) × `AirAttackBonus` (se
caindo de ≥ 2,5 m). Depois o alvo aplica a resistência dele (lobo:
`BodyResistance` no corpo; cabeça ignora).

### Garras (clique esquerdo)

| Campo | Valor | O que faz |
|---|---|---|
| `ComboDamage` | 1 / 1 / 1,5 | Multiplicador do 1º, 2º e 3º golpe |
| `ComboReset` | 1,1 s | Sem atacar por isso, o combo volta ao 1º |
| `ClawReach` / `ClawRadius` | 1,5 / 1,15 m | Volume do acerto: à frente do corpo, e o raio |
| `ClawLunge` | 6,5 m/s | Empurrão para a frente a cada golpe |
| `AttackBuffer` | 0,3 s | Clique guardado para o próximo golpe |
| `ClawVsHumanBonus` | 1,6 | Bônus contra humano |
| `AirAttackMinFall` | 2,5 m | Queda mínima para o bônus aéreo |

Ritmo (em `WolfMoves.Of`): garra e soco tocam a **1×**, a velocidade
das animações do Jhon → cada golpe dura 0,73 s, e o dano sai 0,20 s
depois do clique (eram 2,2×, 0,39 s e 0,14 s, com o Mixamo). Para golpe mais
rápido ou mais pesado, mude só essa velocidade: duração e instante do
dano acompanham.

### Especial do lobo (Q, num toque)

Desde 18/11 o especial sai no Q, na hora, sempre com a força cheia —
não se segura mais o botão direito. Sai no chão ou no ar.

**Não tem mais recarga de tempo** (eram 15 s, `PounceRecharge`): é uma
carga em %, como a suprema de Overwatch, no `Weapon`
(`Weapon.SpecialCharge.cs`). Cheia, o Q salta e ela zera. Morrer não
tira; morto ou fora da partida valendo, não enche.

| Campo | Valor | O que faz |
|---|---|---|
| `SpecialPerKill` | 0,25 | Quanto um abate enche (quatro abates enchem do zero) |
| `SpecialPerAssist` | 0,1 | Quanto uma assistência enche |
| `SpecialFillTime` | 240 s | Tempo para encher sozinha, sem abate nenhum |
 `PounceChargeTime`,
`PounceMinPower` e `PounceChargeHold` continuam valendo só para o bot,
que escolhe a carga pela distância.

| Campo | Valor | O que faz |
|---|---|---|
| `PounceChargeTime` | 1,0 s | Tempo segurando até a carga cheia |
| `PounceChargeLimitsReach` | desligado | Desligado: o salto cai onde a mira aponta, com qualquer carga. Ligado: a barra é o limite (regra antiga) |
| `PounceMinDistance` | 1,5 m | Distância sem carga, quando a mira não toca nada (céu) e para o bot |
| `PounceMaxDistance` | 12 m (era 8,4) | Alcance do salto: o mais longe que o ponto mirado pode estar. O bot só salta de até 1 m antes disso |
| `PounceMinPower` | 0,4 | Fração do dano sem carga |
| `HeavyDamageBonus` | 2,1 | Multiplicador do dano com carga cheia |
| `PounceChargeHold` | 0,6 s | Segura cheio isso e salta sozinho |
| `PounceMaxClear` | 3,2 m | Altura em que o salto pousa ou que ele passa por cima (contêiner de 2,9 m sim, dois empilhados não) |
| `PounceMaxDrop` | 6,5 m | Quanto o salto pode descer até o ponto de queda |
| `PounceMaxClimb` | 6,2 m | Mirando em cima de algo, o salto sobe até isto (dois contêineres empilhados, 5,8 m) |
| `PounceFullFlight` | 0,45 | Tempo de voo com a carga cheia, em fração do clipe (0,8 s → 0,36 s). Sem carga, 1 |
| `PounceArc` | 0,6 m | Altura mínima do arco |
| `PounceHitRadius` | 1,8 m | Quem o salto pega ao cair: em volta do lobo (era a garra: 1,15 m de raio, 1,5 m à frente). O anel da mira tem este tamanho |
| `PounceSplash` | 0,3 | O salto é de um alvo só: o principal leva o dano cheio, quem estiver do lado na área leva esta fração |
| `PounceStandOff` | 0,7 m | O especial travado cai a esta distância do centro do alvo, na frente dele (as duas cápsulas têm 0,3 m de raio). Menor que 0,6, o lobo cai dentro do alvo e a física o empurra |
| `PounceSteerSpeed` | 9 m/s | O mais rápido que o ponto de queda do especial anda atrás do alvo que se mexe. Mais alto, o lobo balança com quem esquiva; mais baixo, quem corre (o lobo de quatro, 7,7 m/s) escapa |
| `PounceLeadSmooth` / `PounceMaxLead` | 0,25 s / 2,5 m | Quanto o especial antecipa o alvo: a velocidade dele alisada nesse tempo, vezes o voo que falta, até esse tanto |
| `PounceHitReach` | 0,4 m | Quanto o centro da área do salto fica à frente do lobo |
| `PounceBodyRadius` (grupo "Lobo: caminho do salto") | 0,36 m | A cápsula que o salto testa ao longo do voo: a do corpo (0,3) e a folga com que a física já conta um encosto. Menor, o lobo raspa na quina de barreiras e carros |
| `PounceGrazeShare` | 0,6 | Sem caminho limpo de lado nenhum do alvo, testa de novo com o corpo nesta fração do raio (aceita raspar) |
| `PounceFootClear` | 0,03 m | O que é mais baixo que isto acima dos pés não conta no caminho (junta de calçada) |
| `PounceThinProp` | 0,35 m | Poste, placa, haste mais finos que isto (de lado a lado) não barram o salto: o voo atravessa |
| `PounceLowArc` | 0,3 m | O arco mais rasante que o salto aceita (por baixo de copa ou toldo) |
| `PounceShapeAfter` | 0,3 m | Até quanto acima do arco de sempre o voo fica com a forma de sempre; passou disso, prova subir antes de avançar (ou descer na vertical no fim). As formas e os custos ficam no `Weapon.WolfPouncePath.cs` |
| `MaxFlightSpeed` (`WolfMoveMotor`, constante) | 90 m/s (era 45) | Teto da velocidade no voo do salto. Com 45, o especial de 13 m (58 m/s no meio do sino) ficava para trás e corrigia no fim |
| `LockAngle` / `LockKeepAngle` (nó `Player`) | 20° / 32° | Especial armado (Q): o quanto o inimigo pode estar fora do centro da tela para travar / para a trava segurar |
| `LockRange` / `LockKeepRange` | 14 / 16 m | Até onde trava / até onde o já travado segura (25/11: o alvo a 14 m que passava a 14,05 m no clique soltava a trava, e o salto ia para a mira e caía longe) |
| `LockTurnRate` | 16 /s | Quão rápido a mira vai até o travado |
| `HeavyCooldown` | 1,0 s | Espera depois do salto |

Ritmo: o clipe toca a **1,4×** → o salto inteiro dura 2,1 s, o voo
começa 0,32 s depois de soltar e o dano sai em 1,14 s.

### Bote (botão direito no ar)

| Campo | Valor | O que faz |
|---|---|---|
| `DiveSpeed` | 13 m/s | Velocidade do mergulho (constante, em linha reta) |
| `DiveMinDrop` | 0,35 | Inclinação mínima para baixo |
| `DiveMaxTime` | 1,1 s | Teto do mergulho (alcance ~14 m) |
| `DiveForward` | 0,8 | Quanto do avanço horizontal o bote mantém |
| `DiveForwardFromWall` | 0,5 | O mesmo, depois de um salto da parede |
| `LeapDamageBonus` | 3,0 | Multiplicador do dano |
| `LeapReach` / `LeapRadius` | 1,7 / 1,5 m | Volume do acerto (maior que o da garra) |
| `LeapCooldown` | 0 | Espera entre botes (0 = um por pulo) |

A aterrissagem prende o lobo 0,87 s (`WolfMoves.Land`, a 1×). No ar, o bote
corta a garrada em curso na hora (`Weapon.TrySpecial`; o dano dela que ainda
não saiu não sai mais). A altura mínima (`LeapMinHeight`) continua valendo.

### Rugido (R)

Dura **1,53 s** (o Howl do Jhon a 1×, em `WolfMoves.Of`) e devolve o escudo
inteiro. Pode andar rugindo. `HowlNoise` 45 m (ouvido pelos bots) e
`Health.ShieldDecayGrace` 1 s (o escudo espera isso para voltar a
escorrer).

O que se vê e se ouve (23/11): só a silhueta na cor do lobo (`WolfFx`,
pelo `RoarBurst.Glow`) e os raios elétricos (`RoarBurst`). Dois sons: a
voz (`WorldAudio.HowlSound` = `lobo_rugido`) e os choques, só estalos e
arcos, sem tom que sobe (`WolfFx.HowlEnergySound` = `lobo_uivo_choque`,
22 m; era `lobo_uivo_energia`). O estalo do escudo cheio saiu
(`WolfFx.HowlShieldSound` vazio, era `lobo_uivo_escudo`). Virar lobo e
voltar a humano tocam o "puff" (`CombatantAudio.TransformSound` e
`UntransformSound` = `forma_puff`; eram os rugidos `lobo_transformar` e
`lobo_destransformar`, que ficam no disco). Os dois sons novos saem de
`src/tools/sons/choque_puff.py`.

### Quanto dá hoje (contra humano, 200 de vida)

| Golpe | Lobo (65) |
|---|---|
| Garra 1º/2º | 104 (dois golpes matam) |
| Garra 3º | 156 |
| Salto sem carga | 108 |
| Salto cheio (`HeavyDamageBonus` 2,6) | 270 |
| Bote | 312 |
| Bote caindo ≥ 2,5 m | 546 |

Contra lobo, tire o ×1,6 (o lobo leva o dano inteiro desde 26/11:
`BodyResistance` 1,0). As contas
estão em `src/scripts/domain/CombatRules.cs`, com teste
(`src/tests/unit/CombatRulesTests.cs`): mudou a fórmula, o teste diz.

---

## 5. Transformação e WP

`src/scripts/forms/FormController.cs` → nó `FormController`.

| Campo | Valor | O que faz |
|---|---|---|
| `HumanHp` | 200 | Vida (a mesma nas duas formas). O personagem multiplica (`HpMultiplier` no `personagem.tres` de cada pasta em `src/assets/personagens/`; Eve ×1,1 = 220) |
| `MaxWp` | 10000 | Teto de WP (era 2500; Jhon, 21/11) |
| `WpPerKill` | 100 | WP por abate de arma |
| `WpPerWolfKill` | 70 | WP por abate de lobo |
| `WpPerAssist` | 35 | WP por assistência |
| `WpPerDamageDealt` | 0 | WP por dano (desligado) |
| `WpLostOnDeath` | 100 | WP perdido ao morrer |
| `ShiftDuration` | 0,55 s | Virar lobo |
| `ShiftBackDuration` | 0,2 s | Voltar a humano |
| `ShiftDamageMultiplier` | 1,5 | Dano recebido durante a transformação |
| `RefillOnTransform` | não | Encher a vida ao transformar |

Mais: `WpCost` da classe (seção 2), `DefaultBonus` 50 WP ao escolher o
conjunto padrão (`LoadoutMenu.cs`) e o `WpCost` de cada arma. Renasce
sempre humano.

---

## 6. Vida, escudo e renascer

`src/scripts/combat/Health.cs` → nó `Health`.

| Campo | Valor | O que faz |
|---|---|---|
| `MaxHp` | 200 | Vida máxima (o `FormController` define a pool) |
| `RespawnDelay` | 3 s (bots: 6 s na cena) | Espera para renascer |
| `SpawnProtection` | 3 s | Imortal ao renascer, contra os outros: a própria granada fere do mesmo jeito (`Health.TakeOwnDamage`, 25/11) |
| `RegenDelay` | 15 s | Sem levar dano por este tempo, a vida começa a voltar (0 desliga). O escudo não volta sozinho |
| `RegenRate` | 0,08 | Fração da vida máxima que volta por segundo (do zero ao cheio em 12,5 s) |
| `AutoRespawn` | sim (o jogador desliga) | Renascer sozinho |
| `ShieldDecayGrace` | 1 s | Carência do escudo depois do rugido |

O tiro acerta o que se vê (25/11, `BodyHitbox`, conta em
`src/scripts/domain/BodyHit.cs`): cada peça do corpo (cabeça, pescoço,
tronco, braço, antebraço, mão, coxa, canela, pé) é uma cápsula de corte
oval ajustada à pele que pesa no osso dela, e segue o osso. A proporção do
oval sai da caixa de 3% a 97% da pele (`BodyHit.CrossQuantile` 0,03) e o
comprimento de 2% a 98% (`LengthQuantile` 0,02); o tamanho, de conter
`BodyHit.Contains` **0,8** da pele (a cabeça, `BodyHit.HeadContains`
**0,6**, mais justa: headshot exige mira). Peça com o comprimento (a
metade) acima de `BodyHit.ElongatedFrom` 1,5 vez o maior raio do corte é
cápsula, e o membro vira duas peças, uma por metade (ele afina); abaixo, é
um elipsoide (a cabeça). A cabeça leva só a pele com 80% do peso nos ossos
dela (`BodyHitbox.HeadWeight`; a juba do lobo é corpo). Peça com menos de 16
pontos vai para o osso pai (`BodyHitbox.MinPoints`). O rabo do lobo não
leva tiro. A faca e a coronhada continuam pela cápsula de movimento, com a
cabeça pela peça até `HeadHit.Depth` 0,45 m dentro da cápsula. Medido
(teste `CorpoPrecisoNoTiro`): o ar em volta que leva tiro fica em 11–18% da
silhueta e o corpo visível sem tiro em 2–8% (antes, 55–90% e 2–50%). Até
25/11 o tiro acertava a cápsula de movimento e a cabeça era uma esfera de
`HeadHit.RadiusFactor` 0,55 × a distância entre os ossos `mixamorig_Head`
e `mixamorig_HeadTop_End` (11,9 cm de raio na Eve, 13,1 no Barut, 17,9 no
lobo). Sem modelo à vista (boneco do treino, corpo no servidor), vale a
cápsula e `Weapon.HeadZone` 0,72 m acima do centro do corpo. A faca usa
`Weapon.HeadshotMultiplier`
2,5; as armas, o delas. Câmera de morte:
`LoadoutMenu.KillCamTime` 4,2 s, `KillCam.Hold` 0,35 s,
`KillCam.TurnRate` 5.

---

## 7. Armas de fogo

`src/resources/weapons/*.tres` (script `WeaponDefinition.cs`).
Um `.tres` por arma, **editado à mão** (no Inspector ou no texto; os
geradores `gerar_arsenal.py` e `gerar_armas.py` saíram em 24/11). As
skins (`ar15_ouro`, `m4a4_dragao`...) têm o `.tres` delas, um pouco mais
fortes que a arma de base; a ficha de cada uma está no `docs/ARSENAL.md`.

| Campo | AR-15 | M4A4 | AK-74 | SCAR-H | SVD | Desert Eagle |
|---|---|---|---|---|---|---|
| Classe | fuzil | fuzil | fuzil | pesada (`Class` fuzil) | sniper | pistola |
| `WpCost` | 0 | 200 (0 no padrão) | 380 | 450 | 600 | 0 |
| `Damage` (× `CombatRules.FirearmDamageScale` 1,38 no tiro; era 2 até 25/11) | 27 | 26 | 42 | 48 | 85 | 55 |
| `HeadshotMultiplier` | 2,5 | 2,5 | 2,4 | 2,3 | 2,4 | 2,2 |
| `Rpm` | 650 | 700 | 500 | 400 | 120 | 240 |
| `Mode` | Auto | Auto | Auto | Auto | Semi | Semi |
| `Magazine` / `Reserve` | 30 / 90 | 30 / 90 | 30 / 90 | 20 / 80 | 10 / 30 | 7 / 28 |
| `ReloadTactical` / `Empty` (s) | 2,3 / 2,9 | 2,3 / 2,9 | 2,5 / 3,3 | 2,9 / 3,5 | 3,0 / 3,5 | 1,8 / 2,2 |
| `DrawTime` / `HolsterTime` (s) | 0,5 / 0,5 | 0,5 / 0,5 | 0,55 / 0,5 | 0,6 / 0,55 | 0,6 / 0,5 | 0,4 / 0,3 |
| `SpreadBase` / `PerShot` / `Max` (°) | 0,15 / 0,09 / 1,8 | 0,16 / 0,09 / 1,9 | 0,45 / 0,4 / 6,5 | 0,3 / 0,35 / 5 | 4 / 0,5 / 6 | 0,45 / 0,8 / 6 |
| `SpreadRecovery` (°/s) | 12 (espera 0,05 s) | 12 (espera 0,05 s) | 7 | 8 | 9 | 9 |
| `MoveSpreadMultiplier` | 1,6 | 1,6 | 3,0 | 2,6 | 4,0 | 1,5 |
| `CrouchSpreadMultiplier` | 0,7 | 0,7 | 0,6 | 0,6 | 0,7 | 0,8 |
| `RecoilVertical` / `Late` (tiros antes do `Late`) | 1,0 / 0,55 (4) | 0,85 / 0,55 (4) | 1,8 / 0,9 (5) | 2,1 / 1,2 (3) | 3,0 | 1,6 |
| `RecoilHorizontal` | 0,18 | 0,14 | 0,15 (0,7 após 6 tiros) | 0,2 | 0,3 | 0,2 |
| `RecoilRecovery` | 7 | 7,5 | 5,5 | 5 | 5 | 5,5 |
| `Range` (m) | 150 | 150 | 150 | 200 | 300 | 100 |
| Queda de dano (m → fração) | 35–60 → 0,7 | 30–55 → 0,7 | 30–55 → 0,75 | 40–70 → 0,8 | nenhuma | 25–45 → 0,7 |
| `SpeedMultiplier` | 1,0 | 1,0 | 0,92 | 0,88 | 0,86 | 1,08 |
| `NoiseRadius` (m) | 45 | 45 | 55 | 60 | 80 | 50 |

Outros campos: `WolfStun` (Desert Eagle: 0,25 s de lentidão no lobo),
`Pellets` (chumbos por tiro) e `ShellByShell`/`ShellTime`/`ShellEnter`
(recarga cartucho a cartucho; nenhuma arma de hoje usa os dois),
`RecoilEarlyShots` (tiros antes do recuo "Late"), `SpreadDelay` (espera
para a dispersão voltar). `Reserve` é a reserva com que se nasce e o
teto do que se junta do chão (a munição acaba). Os `Star*` são só
exibição (da loja).

**Zoom** (botão direito segurado), no grupo "Mira" de cada `.tres`:

| Campo | AR-15 | M4A4 | AK-74 | SCAR-H | SVD | Desert Eagle |
|---|---|---|---|---|---|---|
| `AimFov` (graus; fora da mira, 75) | 55 | 55 | 55 | 50 | 18 | 65 |
| `AimSpreadMultiplier` (dispersão que sobra) | 0,2 | 0,15 | 0,3 | 0,25 | 0,006 | 0,6 |
| `AimSpeedMultiplier` (passo que sobra) | 0,7 | 0,7 | 0,7 | 0,6 | 0,5 | 0,7 |
| `AimRecoilMultiplier` (recuo que sobra) | 1 | 1 | 1 | 0,35 | 1 | 1 |
| `AimLift` (m que a arma sobe mirando) | 0 | 0 | 0 | 0 | 0 | 0,005 |

`AimLift` (25/11): a pistola mirava com a linha de mira 11 mm abaixo do olho
(`BAIXO` em `src/tools/armas/conjuntos.py`, pedido de 19/11), a ponta da massa
2,1° abaixo do centro da tela, e a bala sai do centro: ia 2,1° acima de onde a
massa apontava (55 cm a 15 m). Agora a arma sobe 5 mm mirando e a massa e a alça
de trás ficam no centro, como no fuzil; a bala segue saindo do centro. O 5 mm
saiu de fotos do jogo com 0, 3, 6 e 11 mm (13 px de tela por mm): com 5 a ponta
da massa fica no centro, dentro do entalhe da alça. Se `BAIXO` ou o lugar da
pistola na mira mudar, refaça a medida. O teste `TiroPelaMiraDaPistola` guarda
que o tiro sai do centro.

`Weapon.AimTime` 0,16 s (entrar e sair). A sensibilidade do mouse cai na
razão das aberturas, para o mesmo gesto andar o mesmo na tela. A mira
circular: `Crosshair.PaintScope` (anel a 11% da altura da tela; a cor e
a opacidade são as da mira do jogador).

Ajustes globais do recuo, no `Player`: `RecoilScale` 1 (multiplica o de
toda arma), `RecoilMax` 4,5° (teto acumulado), `RecoilReturnDelay`
0,12 s. Traçante: `Weapon.ShowTracer` e `TracerColor`; o efeito em
`BulletTracer` (`Speed` 190 m/s, `StreakLength` 3,2 m, `CoreWidth` 0,022 m,
`GlowWidth` 0,12 m, `TrailLife` 0,45 s, `TrailWidth` 0,03 m, `TrailStrength`
0,38, `TrailWisp` 0,03 m, `FlashSize` 0,22 m, `FlashLife` 0,05 s). Ruído do
tiro para os bots: `Weapon.ShotNoise` 60 m.

Preços da loja e patente mínima: `src/scripts/meta/Catalog.cs`.

### A pistola na mão (a P9, 23/11)

A pegada, o saque e as recargas da Desert Eagle vêm da P9
(`src/tools/armas/dados/p9.npz`, `RefP9` em `src/tools/armas/conjuntos.py`). Depois
de mudar, rode `conjuntos.py deagle`, `peles.py deagle deagle_cobra
deagle_fenix` e a terceira pessoa (`mao_manny_anim.py eve deagle`,
`animacoes_armas.py barut deagle`).

| Onde | Valor | O quê |
|---|---|---|
| `pistola()`: `da_p9('draw', 'sacar', 0,36, …)` | 0,36 s (a P9 tem 0,58) | O saque |
| `RECARGA_FORCA` | 0,6 | Quanto do gesto da arma da P9 entra na recarga (inteiro, o cano subia 30° e de fora a Eve levava a arma ao rosto) |
| `RECARGA_PERTO_P9` | x −1,8 cm, y +4 cm, z +2 cm (no olho) | Para onde a arma vai durante a troca, para o carregador aparecer |
| `RECARGA_GIRO_P9` | pitch 0°, yaw +20°, roll −25° | O giro a mais na troca: o cano para dentro e tombado. Sem ele o cotovelo direito da Eve abria para o lado (100°; agora 32°) |
| `PISTOLA_RETA` (`mao_manny_anim.py`) | ligado | Terceira pessoa: o cano na horizontal e os pulsos retos, não com a dobra da primeira pessoa (24/11). `PISTOLA_RETA=0` volta ao de antes |
| `PUNHO_RETO` (`mao_manny_anim.py`) | 8° | Quanto o pulso pode dobrar sem custo na pistola reta |
| `FACA_SACAR_PUNHO` (`mao_manny_anim.py`) | 25°, 15° | O punho direito no saque da faca: até quanto dobra e vai para o lado (a faca gira junto com a palma); `0` desliga |
| `COTOVELO_D_MIRA` (`mao_manny_anim.py`) | Barut `2.0,-1.0,0.0` (64° do para baixo); Eve sem | Para onde o cotovelo direito prefere ir na mira do fuzil, por personagem (fora, cima, trás); a variável de ambiente vale para todos |

As durações das recargas continuam as do jogo (`ReloadTactical` 1,8 s e
`ReloadEmpty` 2,2 s no `.tres`; o clipe tem 1,67 e 2,17 s).

---

## 8. Faca, granada e soltar arma

`src/scripts/combat/weapon/` (`Weapon.Knife.cs`, `.Grenade.cs`, `.Inventory.cs`) → nó `Weapon`, e `src/scripts/combat/Grenade.cs` (cena
`src/scenes/grenade.tscn`).

| Campo | Valor | O que faz |
|---|---|---|
| `KnifeDamage` / `KnifeRange` / `KnifeCooldown` | 85 / 2,2 m / 0,40 s | Faca, o golpe rápido (clique esquerdo) |
| `KnifeStabDamage` / `KnifeStabBehind` / `KnifeStabCone` | 120 / 2× / 60° | A estocada (botão direito): o dano de frente, quanto vale pelas costas (240 mata um humano de vida cheia) e o cone das costas (graus entre o golpe e a frente do alvo) |
| `KnifeStabHitDelay` / `KnifeStabCooldown` | 0,2 s / 0,8 s | Quando o dano da estocada sai (a lâmina chega à frente no clipe) e quanto a faca fica ocupada |
| `KnifeSpeedMultiplier` | 1,037 | Velocidade com a faca na mão, sobre a do humano (3,36 m/s) |
| `ThrowForce` | 17,5 m/s | Força do arremesso na mira (era 14,5; 20/11). Olhando reto, de pé, vai a uns 24 m; a granada cai onde a mira aponta |
| `ThrowArc` | 4 m/s | Empurrão para cima (era 3,5) |
| `ThrowCooldown` | 0,9 s | Entre uma granada e outra |
| Quantidade | 1 por espaço de utilitário | `Arsenal.cs` (`Count`) e o conjunto |
| `Grenade.Fuse` | 1,2 s | Tempo até explodir, contado do primeiro toque (sem tocar nada, explode em `MaxFlight` 6 s) |
| `Grenade.Radius` | 9 m | Raio (era 6,5; 20/11) |
| `Grenade.BounceLoudSpeed` / `BounceMinSpeed` | 7 / 0,45 m/s | O som de cada batida: a 7 m/s soa a 0 dB, metade da velocidade 6 dB a menos; abaixo de 0,45 é rolar e não soa. Solta de 2 m: três batidas, a 5,9, 1,8 e 0,5 m/s |
| `Grenade.BounceRange` / `BounceGap` | 25 m / 0,07 s | Até onde a batida se ouve, e o tempo mínimo entre duas (o mesmo toque pode durar dois passos) |
| `Grenade.MaxDamage` | 420 | Dano no centro, caindo até a borda: mata quem tem 200 de vida a até 4,7 m |
| `Grenade.Kind` | `Frag` | Explosiva desde 19/11 (o id ainda é `choque`). `Shock`, a de choque de 18/11, está guardada: `ShockDamage` 60, `StunDuration` 2,5 s e `StunSlowFactor` 0,3 só valem nela |
| `DropThrow` / `DropLift` | 5,5 / 1,8 m/s | Arremesso da arma ao soltar (G) |

Segurar o botão arma o braço; soltar arremessa. A animação do braço
fica em `ViewModel.cs` (na tela) e `CharacterAnimator.cs` (no corpo).

O tamanho e a pegada da granada (23/11):

| Onde | Campo | Valor | O que faz |
|---|---|---|---|
| `src/tools/armas/conjuntos.py` | `GRANADA['tamanho']` | 1,40 (era 1,18) | A granada na mão em primeira pessoa, em relação à de verdade |
| `src/tools/armas/formas_mao.py` | `ARGOLA` | 0,6 | O tamanho da argola do pino (o aro de 5,8 cm da V40 vira um de 3,5 cm) |
| `src/tools/armas/conjuntos.py` | `GRANADA_ATRAS` | pulso 21,5 cm à direita, 5 cm acima e 11 cm à frente do olho | A mão armada, quase toda fora da tela |
| `Grenade` | `ModelScale` | 1,4 | A granada voando (só a aparência; a colisão não muda) |
| `HeldWeapon` | `GrenadeHeldGrow` | 1 | A granada na mão em terceira pessoa, sobre a da primeira pessoa (era 1,4; desde 24/11 do mesmo tamanho, 1,40 da de verdade) |
| `HeldWeapon` | `DrawShowAt` | 0,85 | Na troca de arma, quanto do caminho até a pose do saque a mão anda antes de a arma nova aparecer nela (sai do bolso, da bainha, de trás) |
| `mao_manny_anim.py` | `GRANADA_CRESCE` / `GRANADA_MAO_ABRE` | 1 / 0 | O mesmo número no Blender (o dedo que puxa o pino entra na argola do tamanho do jogo) e quanto a mão abre para a granada (0, a pegada da primeira pessoa; era 0,3 com ela 1,4 vez maior) |
| `src/tools/armas/peles.py` | `PIXEL_BLOCO` | 2,6 cm | O tamanho do bloco da AR-15 8 Bit (era 1,3) |
| `mao_manny_anim.py` | `LANCE_PINO_SAI` / `LANCE_LARGA` | 0,27 / 0,12 s | Quando o pino sai (a esquerda o leva) e quando ela o larga (cai no chão: `HeldWeapon.DroppedPinLife` 8 s) |
| `mao_manny_anim.py` | `GRANADA_BOLSO` | 0,22; 0,10; −0,45 m | O bolso de onde a terceira pessoa saca a granada (no peito: direita, frente, cima) |

`dados/na_mao.json` (onde a malha da granada vai em cada osso) é escrito
por `conjuntos.py` a cada vez: mudou o tamanho, rode `conjuntos.py choque`
(com `SEM_PELES=1`) e depois `peles.py choque choque_carbono choque_neon
choque_magma choque_ouro`.

---

## 9. Bots e dificuldade

**Estes valores vêm de `src/resources/bots/facil.tres`, `medio.tres` e
`dificil.tres`** (classe `BotDifficulty`; abra no Inspector) e
sobrescrevem o `bot.tscn`. O nome e a linha que aparecem no menu de
dificuldade (`Label`, `Summary`) também moram ali. O teste
`Dificuldade` confere que cada nível é mais competente que o anterior
em cada alavanca e que o difícil ainda erra — mude à vontade, mas sem
inverter a ordem:

| Campo | Fácil | Médio | Difícil | O que faz |
|---|---|---|---|---|
| `SightRange` | 26 | 40 | 55 m | Alcance da visão |
| `FieldOfView` | 100 | 140 | 170° | Cone de visão |
| `Memory` | 1,5 | 3 | 5 s | Quanto lembra do alvo fora de vista |
| `Hearing` | 0,6 | 1,0 | 1,35 | × raio dos ruídos |
| `HearingError` | 5 | 2,5 | 1,2 m | Erro ao localizar um som |
| `Curiosity` | 0,35 | 0,6 | 0,85 | Chance de ir atrás do som |
| `ReactionTime` (± `Jitter`) | 0,8 (±0,35) | 0,4 (±0,2) | 0,2 (±0,1) s | Reação ao ver ou levar tiro |
| `TurnSpeed` / `AlertTurnSpeed` | 4 / 2,8 | 8 / 4,5 | 11 / 7,5 | Rapidez de virar |
| `AimError` | 9 | 4,5 | 2° | Erro de mira |
| `MissChance` | 45% | 20% | 8% | Tiros errados de propósito |
| `BurstTime` / `BurstPause` | 0,45 / 1,3 | 0,7 / 0,65 | 0,95 / 0,35 s | Rajada e pausa |
| `EngageRange` | 24 | 40 | 55 m | Distância em que começa a atirar |
| `Speed` | 2,5 | 3,0 | 3,5 m/s | Velocidade (o lobo do bot × 1,298) |
| `StrafeFactor` / `StrafeInterval` | 0,5 / 2,2 | 0,8 / 1,4 | 1,0 / 0,9 | Movimento lateral atirando |
| `PreferredRange` | 11 | 9 | 7 m | Distância que tenta manter |
| `Aggression` | 0,5 | 0,8 | 1,0 | Quanto avança em vez de circular |
| `GrenadeRate` | 0,12 | 0,3 | 0,5 | Chance por segundo de jogar granada quando dá (dobra contra lobo) |
| `WolfSpecialRate` | 0,25 | 0,5 | 0,75 | Frequência dos especiais do lobo |
| `PounceError` | 25% | 12% | 6% | Erro da carga do salto |
| `PatrolSpeedFactor` / `PatrolPause` | 0,7 / 2,2 | 0,85 / 1,5 | 1,0 / 0,9 | Ronda (a velocidade era 0,45 / 0,55 / 0,7 até 18/11; a pausa agora só no fim da rota) |

Ficam no `bot.tscn` (a dificuldade não mexe): `MissSpread` 14°,
`TargetStickiness` 6 m, `RetargetInterval` 0,5 s, `DecisionInterval`
0,4 s, `Vigilance` 7 s, `MinRange` 4 m, `WolfHealthThreshold` 0,4 e
`WolfRange` 14 m (quando vira lobo), `HumanRange` 20 m,
`CoverSamples` 12, `CoverSearchRadius` 11 m, `CoverMinDistance` 6 m,
`NoCoverTime` 3 s, `ArriveRadius` 2,5 m, `ScanTime` 2,5 s,
`PatrolRadius` 16 m, `StuckTime` 1,2 s, `JumpMaxHeight` 1,1 m,
`CuriositySpread` 0,35.

Desde 18/11, também no `bot.tscn` (padrões no código, `Bot.Persona.cs`,
`Bot.Grenade.cs` e `Bot.Combat.cs`):

| Campo | Padrão | O que faz |
|---|---|---|
| `WolfLove` (± `WolfLoveSpread`) | 0,38 (± 0,3) | Chance de o próximo humor do bot ser de lobo; cada bot sorteia a sua |
| `WolfMoodTime` / `HumanMoodTime` | 20 / 16 s | Duração média de um humor de lobo e de um de arma |
| `RangeSpread` | 0,35 | Quanto a distância preferida varia de um bot para o outro |
| `Teamwork` | 0,7 | Chance de largar a ronda para ajudar quando o time avisa de um inimigo |
| `IntelRange` | 45 m | Até onde um aviso do time interessa |
| `FlankWidth` | 12 m | Chegando de longe, mira um ponto até isto de lado do aviso |
| `HuntAfter` / `HuntError` | 5 s / 10 m | Sem notícia por esse tempo, vai atrás de um inimigo sorteado, com esse erro no palpite |
| `GrenadeMinRange` / `GrenadeMaxRange` | 8 / 20 m | De onde arremessa (o mínimo fica além do raio da explosão) |
| `GrenadeWindup` / `GrenadeRest` | 0,4 / 9 s | Tempo mirando antes de soltar; descanso entre granadas |
| `GrenadeFriendRadius` | 6 m | Não arremessa com aliado a menos disto da queda |
| `WolfSprintFactor` / `WolfSprintFrom` | 1,3 / 7 m | O lobo do bot corre isto mais rápido com o alvo além dessa distância |
| `WolfWeave` | 0,5 | Quanto o lobo ziguezagueia chegando |
| `DodgeJump` | 0,18 | Chance de pular ao trocar o lado do strafe, levando tiro |
| `SuppressChance` / `SuppressTime` | 0,5 / 1,2 s | Perdeu o alvo de vista: atira onde ele sumiu |

`PatrolRoutes.CrowdCost` (18 m) e `SpreadJitter` (10 m) espalham o time
pelas rotas: cada bot numa rota a encarece para o próximo.

O cérebro (as notas de cada ação) está em `src/scripts/ai/BotBrain.cs`.

---

## 10. Partida e sala

| Onde | Campo | Padrão |
|---|---|---|
| `Match.cs` | `ScoreToWin` | 40 abates (a sala sobrescreve) |
| `Match.cs` | `Duration` | 600 s (a sala sobrescreve) |
| `Match.cs` | `CountdownTime` | 10 s |
| `LoadoutMenu.cs` | `ChooseTime` | 15 s para escolher o conjunto ao renascer (não escolheu, vai o padrão) |
| `RoomConfig.cs` | `RoomSettings` | Modo Livre, 6 por time (de 1 a 6, `TeamSizes`), Armazém, 40 pontos, 10 min, todas as armas, bots no Médio |
| `LoadingScreen.cs` | `MinShown` | 2 s mínimos na tela de carregamento |
| `servidor/Alcateia.Dominio/Frota/Fleet.cs` (`MatchProgress`) | `LastSeconds`, `LastShare` | A partida online fecha para quem chega nos últimos 120 s ou nos últimos 20% do tempo, o que for menor (36 s numa de 3 min) |
| `servidor/Alcateia.Dominio/Frota/Fleet.cs` (`MatchProgress`) | `DecidedShare` | ...ou quando um time tem 80% dos pontos para vencer |
| `servidor/Alcateia.Dominio/Salas/Room.cs` | `CanEnter` | Entrar no meio: um time nunca fica com dois a mais que o outro (regra fixa, decisão do Jhon) |

Na tela da sala: modo de jogo (`PlayMode.cs`), jogadores por time,
mapa, pontos, minutos, armas (todas, sem pesadas ou só pistolas), fogo
amigo, troca de lado, espectadores e dificuldade dos bots. Todos os
modos são mata-mata em equipe.

---

## 11. Ruído e som

Raio em metros em que os bots ouvem. Um arquivo só para o jogador e
para o bot: `src/resources/body/ruido.tres` (classe `BodyNoiseProfile`).
Abra no Inspector e mude ali — vale para os dois.

| Campo | Valor |
|---|---|
| `StepHuman` | 13 |
| `StepWolf` | 20 |
| `Land` | 17 |
| `Howl` | 45 (já no começo do rugido e a cada 0,5 s) |
| `StrideHuman` / `StrideWolf` / `StrideWolfSprint` | 1,19 / 1,32 / 2,37 m — a passada: um ruído de passo a cada tanto. Sai dos clipes (teste `PassoNoRitmo`); trocou clipe ou velocidade, refaça. De lobo, só vale sem o modelo: com ele, em pé a passada é a do andar que toca (frente, trás ou lado) e de quatro o som sai uma vez por ciclo do clipe, quando as patas se juntam (`WolfVisual.SprintGather`, 0,567 s) |
| `NoiseRadius` da arma | 45 a 80 (seção 7) |

O som do lugar fica no mapa (`src/scripts/meta/Maps.cs`):

| Mapa | `RoomSize` / `RoomWet` | `Ambience` / `AmbienceDb` | `EventSound` / `EventEvery` (s) |
|---|---|---|---|
| Armazém | 0,75 / 0,35 | `ambiente_armazem` / −12 | `evento_passaros` / 60–130 |
| Cidade | 0,5 / 0,12 | `ambiente_cidade` / −13 | `evento_buzina` / 35–80 |
| Cidade do Deserto | 0,4 / 0,08 | `ambiente_deserto` / −14 | `evento_passaros` / 45–100 |
| Estação de Trem | 0,6 / 0,2 | `ambiente_estacao` / −15 | `evento_trem` / 70–140 |
| Obra | 0,55 / 0,18 | `ambiente_obra` / −15 | `evento_cachorro` / 50–110 |
| Distrito Neon | 0,6 / 0,22 | `ambiente_neon` / −13 | `evento_drone` / 40–90 |

`RoomSize` / `RoomWet` é a reverberação (tamanho de 0 a 1 e quanto
entra); `Ambience` o fundo em laço e o volume; `EventSound` o som
eventual, sorteado nesse intervalo.

No nó `CombatantAudio`: `ImpactRange` 25 m (bala na parede),
`CasingDelay` 0,42 s e `CasingInterval` 0,28 s (cápsula no chão). No
`WorldAudio`: `OwnStepVolume` −3 dB.

Agachado e com Shift não fazem ruído; a faca é silenciosa. Teste:
`Ruido`.
`WorldAudio.ShotReach` 120 m (até onde você ouve o tiro de outro),
`DistantShot` 35 m (troca para o som distante), `OwnStepVolume` −3 dB.

---

## 12. Animação e visual

Mexa com cuidado: muitos destes são medidas.

**Marca do inimigo** (`src/scripts/visual/ShoulderMark.cs`, constantes: o
nó é criado por código):

| Constante | Valor | O que faz |
|---|---|---|
| `Size` | 0,30 m | Lado do brilho do ombro (o halo inteiro); igual no humano e no lobo |
| `Lift` | 0,11 m | Quanto fica acima da junta do ombro |
| `TowardCamera` | 0,15 m | Quanto vem na direção da câmera: o próprio ombro não tampa, a parede tampa. Com 0,25 o brilho do ombro de lá passava na frente do rosto visto de lado |
| `Energy` | 3,0 | Força do brilho (acima de 1 acende o bloom) |

**Lobo** (`src/scripts/visual/wolf/`, nó `WolfVisual`):

| Campo | Valor | O que faz |
|---|---|---|
| `ModelScale` | 1,1 | Tamanho do lobo (o passo dos golpes acompanha; o modelo tem 1,8 m, 1,98 m no jogo) |
| `WalkSpeed` / `RunSpeed` | 2,0 / 2,6 m/s | Abaixo só anda, acima só corre |
| `StartBlend` / `StopBlend` | 0,12 / 0,22 s | Sair do parado e voltar a ele |
| `TurnRate` | 540°/s | Virar para o lado em que anda |
| `AimTurnRate` | 1400°/s | Voltar para a mira ao atacar |
| `CrawlLift` | 0,1 m | Altura extra engatinhando |
| `WallTurnTime` | 0,12 s | Deitar na parede |
| `DiveLeanMax` | 40° | Inclinação no bote |
| `TeamTint` | 0,3 | Cor do time na pele |

**Neon e olhos do lobo** (25/11, Jhon: "coloque os leds do equipamento brilhando bem forte", "e o olho também", "quero o neon desse jeito no game"):

| Onde | Campo | Valor | O que faz |
|---|---|---|---|
| `src/assets/efeitos/lobo.gdshader` | `neon_energy` | 16 (era 2,6) | Força do neon do corpo (braceletes, peitoral, costas e o olho do modelo). Acima de 1 o glow dos mapas (limiar 1) faz o halo; a 16 o miolo passa do branco. Medido no jogo (Armazém e Cidade): 2,6 era um fio aceso sem halo; 10 esquenta; 16 a 24 fica como nas imagens das thumbs |
| idem | `neon_boost_gain` | 0,6 (era 1,6) | Quanto o poder (rugido, especial) multiplica o neon: 1 + isto × o `neon_boost` do `WolfFx`. Com o neon a 16 e o 1,6 de antes, o pico do rugido (escudo cheio, boost ~3) ia a 93 e estourava a tela; agora vai a ~44 |
| idem | `neon_even` | 1 | Todo LED acende igual: cada texel aceso da emissão vai à força cheia, na cor dele (a borda da faixa continua suave; abaixo de 0,04 fica apagado). A textura pintou o miolo do peitoral a ~0,4 e o das costas a ~0,75–0,8 do bracelete (0,98). 0 volta à textura como veio |
| `src/assets/efeitos/olho.gdshader` | `energy` | 4 | Força do miolo do brilho dos olhos (passa do branco; o halo é do glow do mapa) |
| idem | `fade_distance` | 0,03 m | Onde o disco encosta na pele ele some, em vez de cortar reto |
| `src/scripts/visual/fx/EyeGlow.cs` | `Size` | 0,11 m | Tamanho de cada disco (o halo; o miolo é bem menor) |
| idem | `LeftEye` / `RightEye` | (6,47; 15,64; 22,7) / (−1,03; 15,58; 24,43) cm | Onde ficam os olhos no osso da cabeça, 2 cm à frente da pele, medidos no miolo aceso da emissão do modelo (o mesmo rosto nos dois times). Modelo novo: meça de novo |
| `WolfFx` | `EyeBoost` | 0,8 | Quanto o brilho dos olhos sobe com o poder (1 + isto × o neon do poder) |

**Humano** (`src/scripts/visual/human/`, nó `Human`): `CrouchAim` 1
(fuzil no ombro agachado), `BlendSmoothing` 9, `HitCooldown` 1,6 s,
`CorpseHold` / `CorpseFade` 1,2 s, `TintByTeam`, `BlueBody`,
`OrangeBody`.

**Arma na tela** (`src/scripts/ui/ViewModel.cs`): `Kick` 0,055 m e
`KickPitch` 3,2° (o coice: para trás e o cano para cima), `FlashLife`
0,05 s, `BobAmount` 0,011 m e `BobSide` 0,007 m (balanço do passo, no
ritmo da passada), `SwayAmount` 0,55 / `SwayMax` 3,5° / `SwayReturn` 9
(atraso ao olhar), `LandDip` 0,006 m por m/s de queda. Onde a arma fica
na tela: as âncoras no começo do arquivo (`RifleAnchor`...). Onde cada
mão fica em cada arma: a ficha `src/assets/armas/armas.json` (`maos`),
escrita com as peças das armas por `src/tools/armas/reais.py`; a primeira
pessoa animada, por `src/tools/armas/conjuntos.py`, e as skins por
`peles.py`.

**Braços animados** (`src/scripts/ui/ViewModel.Rig.cs`): `RigFov` 50,5° (a
abertura para a qual os conjuntos foram animados; a ficha
`src/assets/armas/fp/bracos.json` pode trazer a sua em `fov` — o AR-15 usa a
da câmera do Jhon: 95° na horizontal, 63,09° na vertical em 16:9), `RigScale` 0,45 (o conjunto encolhido na direção do
olho), `RigKick` 0,3, `RigDrawMax` 0,4 s (o saque animado mais longo:
a arma atira 0,2 s depois da troca), `RigHolsterTime` 0,16 s (quanto o
guardar dura quando a arma não diz o dela; o AR-15 diz:
`HolsterTime`, na seção 7), `RigIdleInspect` 9 s e `RigIdleInspectAgain` 16 s
(parado esse tempo, olha a arma sozinho), `KnifeIdleInspect` desligado (a
faca não se olha sozinha; a tecla F saiu). Saque animado na
arma principal só com `"sacar": true` na ficha (o AR-15). Com `"fiel": true`
(o AR-15, pedido do Jhon) o conjunto toca só a animação dele: sem balanço
do passo, atraso ao olhar, coice nem afundar no pouso de código. A recarga de
código (conjunto sem animação de recarga, o AR-15) está em
`CodeReload`: a arma inclina 10° em torno do punho e desce em até 16% da recarga, o carregador cai
24 cm entre 20% e 36%, some até 60% e volta até 74%; com o pente vazio,
o ferrolho recua 5 cm entre 78% e 90%.

**Mira** (`src/scripts/ui/Crosshair.cs`): `PixelsPerDegree` 9, `MinGap`
5, `Length` 8, `Thickness` 2.

**Vitrine do personagem** (`src/scripts/lobby/CharacterPreview.cs`, o
humano e o lobo em 3D no inventário e na loja; 25/11): `KeyEnergy` 2,1 (a
principal, quente, com sombra macia), `FillEnergy` 0,55 (o preenchimento
frio), `RimEnergy` 7 (os dois recortes atrás), `SkyEnergy` 0,55 (o céu do
mapa de dia como ambiente e reflexo), `ContactShadow` 0,75 (a sombra
embaixo dos pés), `BackdropColor`/`BackdropStrength` 0,32 (o halo atrás) e `Spin` 0,35
rad/s. O lobo não desce o `FootSink` do jogo aqui: as garras furariam o
tampo (o teste `VitrineNoPedestal` reprova).

**Partida autoritativa** (decisões 0011, 0016 e 0017; constantes em C#,
mudam com o servidor de partida junto): `Weapon.ChargeSlack` 0,2 (o
quanto a carga do especial do corpo do servidor pode passar da que ele
mesmo ganhou, copiando a do jogo), `ServerBody.FocusSlack` 3 m (a
distância máxima entre onde o jogo diz que via o alvo travado e onde ele
esteve no servidor), `ServerBody.WallBehindSteps` 3 (passos com o jogo
na parede e o servidor fora antes de o servidor voltar para ela),
`BotProxies.MaxAhead` 0,1 s (quanto a cápsula do bot anda com a
velocidade dele depois do último estado) e `BotProxies.StaleAfter` 1 s
(sem estado novo, a cápsula sai), `InputCommand.MaxSize` 51 bytes (o
comando com renascimento, ponto mirado e alvo travado).

---

## 13. Diagnóstico (desligar para versão final)

Imprimem no Output: `Player.WallDebug` (desligado; ligue para ver por
que a corrida na parede não começou), `Weapon.Debug` (desligado no
bot), `Match.Debug`, `CharacterAnimator.Debug`, `WolfVisual.Debug`,
`Bot.Debug`, `WorldAudio.Debug`. A partida também imprime a semente do
sorteio: `-- --seed=N` repete as decisões dos bots e a dispersão.
