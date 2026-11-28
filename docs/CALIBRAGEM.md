# Guia de calibragem do Alcateia

Onde mexer para deixar o jogo mais rápido, mais lento, mais forte ou mais
fraco, e o que conferir depois. Este guia é organizado por tarefa
("quero o lobo mais lento"). O catálogo de todo número, campo a campo,
continua no `docs/AJUSTES.md`; aqui ficam o caminho, o valor que vale
hoje, o que muda junto e o teste que prende cada número.

**Valores conferidos no código em 27/11/2026.** Se este guia e o código
discordarem, vale o código. Mudou um número? Atualize este guia ou o
`AJUSTES.md` no mesmo commit (seção 17).

> **Nome na tela:** o WP do código aparece no jogo como **KP**, as
> Presas como **Cash**. Nos arquivos os nomes antigos continuam.

## Índice rápido

| Quero mexer em… | Seção |
|---|---|
| Onde um número mora e qual valor ganha | [1](#1-onde-os-valores-moram-e-qual-ganha) |
| Se o servidor online precisa mudar junto | [1.4](#14-online-o-servidor-lê-os-mesmos-números) |
| Gravidade, passos de física, cápsula, alturas do mapa | [2](#2-física-e-corpo) |
| Velocidade, corrida (Shift), pulo e agachar do humano | [3](#3-humano) |
| Velocidade, pulo, corrida de quatro e parede do lobo | [4](#4-lobo-o-corpo) |
| Tempo de lobo, espera, escudo, transformação, KP | [5](#5-lobo-tempo-escudo-transformação-e-kp) |
| Garra, especial (Q), bote, rugido, melhorias | [6](#6-lobo-golpes-e-melhorias) |
| Dano, cadência, pente, alcance, mira das armas | [7](#7-armas-de-fogo) |
| Recuo e dispersão | [8](#8-recuo-e-dispersão) |
| Faca, coronhada, granada, soltar arma | [9](#9-faca-coronhada-granada-e-soltar-arma) |
| Vida, escudo, renascer, regeneração, queda | [10](#10-vida-escudo-renascer-e-queda) |
| Animação presa à velocidade (o que não é botão) | [11](#11-velocidade-e-animação) |
| Abertura da câmera, sensibilidade, câmera do lobo | [12](#12-câmera-e-sensação) |
| Bots e dificuldade | [13](#13-bots-e-dificuldade) |
| Pontos, tempo, renascer da sala; XP, Cash, KP | [14](#14-partida-sala-e-economia) |
| Ruído (o que os bots ouvem) | [15](#15-ruído) |
| Receitas prontas | [16](#16-receitas) |
| Depois de calibrar: testes e documentos | [17](#17-depois-de-calibrar) |

---

## 1. Onde os valores moram (e qual ganha)

### 1.1 As camadas

| Onde | Exemplo | Quem ganha |
|---|---|---|
| Padrão no script: `[Export] public float X = …` | `Player.Movement.cs`: `HumanSpeed = 3.0f` | É o valor de fábrica. Vale onde nenhuma cena nem recurso o troca |
| Valor gravado na cena | `src/scenes/player.tscn`: `HumanSpeed = 3.2` | **A cena ganha do script**, só naquela cena |
| Recurso `.tres` | `src/resources/breeds/lobo.tres`, `weapons/*.tres`, `bots/*.tres`, `body/queda.tres`, `body/ruido.tres` | O que está escrito no `.tres` ganha do padrão da classe dele. Campo que não aparece no arquivo usa o padrão do `.cs` |
| Dificuldade dos bots | `src/resources/bots/medio.tres` | Na entrada da partida, `Difficulty.Apply` escreve por cima do `bot.tscn` (seção 13) |
| Montado na hora | melhorias de lobo, `HpMultiplier` do personagem, olho do humano | `Profile.BuildBreed` multiplica uma **cópia** do `lobo.tres`; o `personagem.tres` multiplica a vida; o `CharacterAnimator` afina `HumanEyeHeight` ao corpo visto |
| Constante no código | `CombatRules.FirearmDamageScale`, `WolfMoves.Of`, `MatchKp`, `Rewards`, `Arsenal`, `Catalog`, `RoomConfig` | Só muda editando o `.cs` |

Para saber o valor que vale: procure o campo na cena antes de olhar o
script. `grep -n "NomeDoCampo" src/scenes/player.tscn` responde em um
segundo.

### 1.2 O que as cenas trocam hoje

`src/scenes/player.tscn` (o jogador, e também cada corpo no servidor de
partida):

| Nó | Campo | Na cena | No script |
|---|---|---|---|
| `Player` | `HumanSpeed` | **3,2** | 3,0 |
| `Player` | `WolfSprintFactor` | **1,719** | 1,495 |
| `WolfVisual` | `WallClipSpeed` | **1,5** | 2,3 |
| `Weapon` | `DiveSpeed` | **18** | 13 |
| `Weapon` | `DiveMaxTime` | **1,0** | 1,1 |
| `Weapon` | `GrenadeScene` | `grenade.tscn` | — |
| `FormController` | `Breed` | `lobo.tres` | — |
| `Head` | posição Y | 0,6 (o olho do lobo, = `StandEyeHeight`) | — |
| `Head/SpringArm3D` | `spring_length` / `margin` | 3,5 / 0,2 (o comprimento é trocado no `_Ready` por `ThirdPersonDistance`, 2,5) | — |
| `CollisionShape3D` | cápsula | raio 0,3, altura 1,75 | — |

`src/scenes/bot.tscn` troca só `Health.Team = 2`, `Weapon.Debug = false`
e o `Breed`. Todo o resto do bot é padrão de script ou vem do nível de
dificuldade.

### 1.3 Jogador e bot não andam pelo mesmo código

Dividem `Health`, `FormController`, `Weapon` (dano, garra, faca,
granada), o `lobo.tres`, o `queda.tres` e o `ruido.tres`. **Não dividem
o movimento**: o bot anda pelo `Bot.Navigation.cs`.

| O que | Jogador | Bot |
|---|---|---|
| Velocidade do humano | `HumanSpeed` × `HumanSpeedBonus` × arma | `Speed` do nível (2,5 / 3,0 / 3,5 m/s), sem a arma |
| Velocidade do lobo | `HumanSpeed` × `SpeedMultiplier` | `Speed` do nível × `SpeedMultiplier` |
| Corrida de quatro | `Player.WolfSprintFactor` (1,719) | `Bot.WolfSprintFactor` (1,3), com o alvo a mais de `WolfSprintFrom` (7 m) |
| Pulo | `HumanJumpVelocity` / `JumpVelocity` × raça | `Bot.JumpVelocity` (4,4, outro campo) × raça no lobo |
| Bote | `DiveSpeed` 18, `DiveMaxTime` 1,0 (cena) | 13 e 1,1 (script) |
| Melhorias | sim | não (o `lobo.tres` puro) |

Mudou o pulo ou a velocidade do jogador? O bot não muda junto. Decida se
deve, e mude no campo dele.

### 1.4 Online: o servidor lê os mesmos números?

**O servidor de partida é o mesmo projeto Godot, exportado**
(`servidor/partida.Dockerfile`). Ele instancia o `player.tscn` para cada
jogador (`MatchWorld.cs`), lê as armas pelo `Arsenal.Get` (os mesmos
`.tres`), monta o lobo de cada um com `Profile.BuildBreed` e as
melhorias, e confere a forma com os tempos do próprio `FormController`
(`ServerBody` → `FormAudit`). O teto de dano de cada acerto sai da arma:
`Damage` × `FirearmDamageScale` × chumbos, a garra pelo `lobo.tres` e os
campos do `Weapon`, a granada pelo `MaxDamage` do `grenade.tscn`
(`HitCheck.cs`).

Ou seja: **não há segunda cópia dos números de combate e movimento.** Há
duas regras:

1. **Cliente e servidor na mesma versão.** Um jogo com números diferentes
   do servidor é corrigido o tempo todo (o corpo puxado de volta) e tem
   acertos recusados. A tag de lançamento sobe os dois juntos
   (`docs/FLY.md`).
2. **Algumas constantes do servidor amarram limites.** Só precisam mudar
   se a calibragem passar delas:

| Constante | Valor | Quando mexer |
|---|---|---|
| `HitCheck.BotWolfMax` (`src/scripts/net/HitCheck.cs`) | 520 (com a folga de 5%, 546) | Teto do golpe de lobo de **bot** online. Hoje o bote caindo é 546, exatamente o teto: subiu dano de lobo, suba isto |
| `HitCheck.BotKnifeMax` | 420 | Teto da faca de bot. A estocada mais forte hoje é 336 (obsidiana na cabeça) |
| `HitCheck.GrenadeReach` / `WolfReach` | 45 m / 6 m | Alcance aceito da granada e do golpe de lobo |
| `HitRules.RangeSlack` / `AimSlackDegrees` (`src/scripts/domain/net/HitRules.cs`) | 2 m / 8° | Folgas da conferência. A dispersão da arma já entra na conta sozinha |
| `FormAudit.SlackTicks` / `ShiftSlackTicks` | 45 / 8 passos | Folga no tempo de lobo e na transformação |
| `NetRates.TickRate` (`src/scripts/domain/net/NetRates.cs`) | 60 | Amarrado aos passos de física (seção 2). Não mexa |
| `HostedMatch.BotMaxSpeed` | 30 m/s | Bot mais rápido que isto foi teleportado |

Os bots online rodam no jogo do dono da sala (decisão 0008): valem o
`bot.tscn` e o nível de dificuldade da versão dele.

O servidor da conta (`servidor/`, .NET) é outro programa. Ele **compila
dois arquivos do jogo**: `src/scripts/domain/Rewards.cs` e
`src/scripts/domain/Rage.cs` (`Alcateia.Dominio.csproj`). O resto da
economia mora no banco dele (seção 14). Os números de combate não passam
pelo painel (decisão 0013).

---

## 2. Física e corpo

### 2.1 O projeto

`project.godot`, seção `[physics]`, só escolhe o motor:
`3d/physics_engine="Jolt Physics"`. O resto é o padrão do Godot:

| Ajuste | Valor | Por que não mexer |
|---|---|---|
| Gravidade (`physics/3d/default_gravity`) | 9,8 m/s² | O `Player`, o `Bot` e a mira da granada leem daqui. Mudar muda todo pulo e toda altura do mapa. Para o humano há a escala dele (`HumanGravityScale`) |
| Passos por segundo (`physics/common/physics_ticks_per_second`) | 60 | O `NetRates.TickRate` é 60, os testes rodam 60 quadros fixos, o servidor conta tempo em passos. Mudar quebra a rede |
| Jitter fix / interpolação de física | padrão (0,5 / desligada) | O desenho entre dois passos é do `VisualSmoothing`, que o `Player` cria |

### 2.2 A cápsula

Raio 0,3 m e altura 1,75 m, no `player.tscn` e no `bot.tscn`.
`StandHeight` 1,75 no `Player`. Agachado: `HumanCrouchHeight` 1,5 m e
`CrouchHeight` (lobo) 1,0 m. Muito código conta com 1,75 m: o nó `Human`
a −0,875, a altura dos pés no `Weapon.HeightAboveGround` (0,85), o raio do
salto (`PounceBodyRadius` 0,36 = 0,3 + folga), o `PounceStandOff` 0,7, o
`HitRules.BodyRadius` 0,45. **A cápsula não é número de calibragem**:
mexer pede conversa.

`FloorConstantSpeed` está ligado (rampa e escada andam o mesmo que o
chão). O ângulo máximo de chão e o ímã são os do Godot (45° e 0,1 m);
andando, o humano gruda até um degrau (`StepHeight`, seção 3).

### 2.3 Alturas amarradas à física

O mapa foi desenhado para o pulo de hoje (`docs/DESIGN.md`, "Alturas
amarradas à física"; `docs/MAPAS.md`):

| Altura | Quem sobe | Número que a prende |
|---|---|---|
| Caixote, 0,8 m | humano pulando | `HumanJumpVelocity` 5,2 com `HumanGravityScale` 1,45: ~1 m. Abaixo de ~4,8 m/s o humano não sobe mais |
| 2,2 m | só o lobo, saindo do caixote | `JumpMultiplier` ≥ 1,2 (hoje 1,435) |
| Terraço, 5 m | ninguém sem parede ou rampa | O lobo pula 2,09 m. `JumpMultiplier` perto de 2,2 já chegaria lá |
| Degrau | andando, sem pular | `StepHeight` 0,3 / `WolfStepHeight` 0,45 |
| Topo de parede | salto da parede | `WallMantleReach` 1,9; `WallClimbReach` 1,8 |
| Especial | passa por cima / sobe | `PounceMaxClear` 3,2; `PounceMaxClimb` 6,2 |
| Queda sem dano | — | `HumanSafe` 4 m, `WolfSafe` 7 m (seção 10) |

As melhorias multiplicam o pulo do lobo (até ×1,06 × 1,05). Conte com
elas ao subir o `JumpMultiplier`.

---

## 3. Humano

Tudo no nó `Player` do `player.tscn` (scripts em
`src/scripts/actors/player/`). O humano é lento de propósito (regra 9 do
`CLAUDE.md`): a mobilidade é do lobo.

### 3.1 Velocidade

A conta: `HumanSpeed` × `HumanSpeedBonus` × agachado × Shift × arma.

| Campo | Onde | Valor | O que faz |
|---|---|---|---|
| `HumanSpeed` | `player.tscn` (o script diz 3,0) | **3,2** | A base. **É também a base do lobo**: mexer aqui muda as duas formas |
| `HumanSpeedBonus` | `Player.Movement.cs` | 0,88 | Só o humano. **É o botão da velocidade do humano**: 2,82 m/s com o fuzil (0,8 e 2,56 até 27/11). A corrida do Shift é × isto: mexeu aqui e quer a corrida igual, mexa no `HumanSprintFactor` na conta inversa |
| `SpeedMultiplier` da arma | cada `.tres` | AR-15 e M4A4 1,0; AK 0,92; SCAR 0,88; SVD 0,86; Desert Eagle 1,08 | A arma na mão pesa: 2,82 / 2,59 / 2,48 / 2,42 / 3,04 m/s |
| `KnifeSpeedMultiplier` | nó `Weapon` (`Weapon.Knife.cs`) | 1,28 | Com a faca, andando: 3,60 m/s |
| `AimSpeedMultiplier` da arma | `.tres` | 0,7 (SCAR 0,6, SVD 0,5) | Passo que sobra mirando |
| `CrouchSpeedFactor` | `Player.Movement.cs` | 0,45 | Agachado, nas duas formas: 1,27 m/s o humano, 1,95 o lobo |

### 3.2 Corrida (Shift)

| Campo | Onde | Valor | O que faz |
|---|---|---|---|
| `HumanSprintFactor` | `Player.Movement.cs` | 1,74327 | × a velocidade de andar (2,816 × 1,74327 = 4,909). 4,91 m/s com fuzil, faca ou pistola (correndo a arma leve não soma, 27/11), 4,52 com a AK. Passa do lobo a pé (4,33), por decisão do Jhon |
| `SprintRecover` | idem | 0,28 s | Quanto a arma leva para voltar depois de correr. O clique dado nesse tempo fica guardado |
| `ViewModel.RigSprintIn` / `RigSprintOut` | `src/scripts/ui/ViewModel.Rig.cs` | 0,20 / 0,28 s | Os braços abaixando e trazendo a arma. **A volta cabe no `SprintRecover`**: mexeu num, mexa no outro |
| `SprintClip` / `SprintPaceMax` | nó `Human` (`CharacterAnimator.Locomotion.cs`) | 3,35 m/s / 1,6 | A passada do clipe de correr e até quanto ele acelera: a corrida passa a patinar acima de 5,36 m/s |

Quando corre (`UpdateHumanSprint`): Shift segurado, indo para a frente
(`Move.Y` < −0,6), em pé, sem atordoamento, sem atirar, mirar, recarregar,
armar granada nem estocar. Começa só no chão; no ar, segue. Correndo com
a faca o bônus dela não soma (27/11: a corrida é a mesma com qualquer
arma leve). De lobo não há Shift.

### 3.3 Pulo e ar

| Campo | Valor | O que faz |
|---|---|---|
| `HumanJumpVelocity` | 5,2 m/s | Impulso do humano. Com a gravidade dele sobe ~1 m (medido: 0,99 no `AlturaDoPulo`, 1,02 no `Medidas`) |
| `HumanGravityScale` | 1,45 | A gravidade do humano no ar: pulo curto e queda seca. Vale para cair de beirada também |
| `HoldFallGravity` | 0,78 | Segurando o pulo na queda, a gravidade cai para isto (nas duas formas) |
| `AirAccel` | 18 m/s² | Quanto se muda a direção no ar (regra 10: parcial) |
| `AirFriction` | 7 m/s² | O arrasto no ar sem tecla: soltar o W encurta o pulo. 0 tira a escolha |
| `JumpBuffer` | 0,15 s | O pulo apertado um pouco antes de pousar ainda sai |

### 3.4 Chão, degraus e escada

| Campo | Valor | O que faz |
|---|---|---|
| `GroundAccel` / `GroundFriction` | 60 / 50 m/s² | Arrancar e frear no chão. Alto = resposta imediata |
| `StepHeight` / `WolfStepHeight` | 0,3 / 0,45 m | Degrau que se sobe andando. Para o humano, também o ímã de descer degrau |
| `StepEase`, `StepRhythmWindow`, `StepAccel`, `StepLag`, `StepCatchUp` | 2,2 m/s; 0,4 s; 18 m/s²; 0,25 m; 10 | Como o olho alcança o corpo depois de um degrau ou numa escada. Só a câmera, o corpo não muda |
| `LadderSpeed` (`Player.Ladder.cs`) | 3,4 m/s | Subir e descer escada de mão |
| `LadderLookDown` / `LadderExitPush` / `LadderJumpOff` | 0,6 rad / 2,5 / 3 m/s | Olhar para baixo desce; empurrão no topo; pulo para soltar |
| `FreezeBrake` (`Player.cs`) | 30 m/s² | Freada de quem fica preso no lugar (golpe, carga) |
| `FallLimit` | −25 m | Abaixo disto morre de queda (até 27/11 voltava ao spawn) |
| `FallAwayDepth` | 200 m | Morto, o corpo continua caindo até este tanto abaixo do `FallLimit` e para |

### 3.5 Agachar e olho

`HumanCrouchHeight` 1,5 m e `CrouchLerpSpeed` 22. O olho
(`HumanEyeHeight` 1,592 e `HumanCrouchEyeHeight` 1,4 de fábrica) **não é
botão**: o `CharacterAnimator` o leva, a `EyeTuneSpeed` 0,4 m/s, até o
olho do corpo desenhado (~159 cm em pé, ~134 cm agachado). Os de fábrica
valem para o corpo sem modelo (servidor de partida). Testes:
`OlhoNoCorpoEve`, `OlhoNoCorpoBarut`, `PeNoChaoEve`, `PeNoChaoBarut`,
`AgachadoMira`.

**Testes que prendem o humano:** `VelocidadeNoChao` (2,816 e 1,267 escritos
no teste; Shift e faca pelos campos), `Medidas` (2,82, pulo 1,02, avanço
2,1), `AlturaDoPulo` (0,99), `CorridaDoHumano` (Shift e `SprintRecover`),
`ShiftPreso`, `Ruido` (6 a 8 passos em 3 s andando, 11 a 14 correndo: o passo
é a pisada do corpo animado, `CharacterAnimator.Feet`),
`PassoNoRitmo`, `PassadaHumana`, `EscadaObra` e as outras `Escada*`.

---

## 4. Lobo: o corpo

O lobo é um só (26/11): `src/resources/breeds/lobo.tres` (script
`src/scripts/forms/WolfBreed.cs`). Jogador e bots usam o mesmo arquivo;
as melhorias multiplicam uma cópia só do jogador.

> O padrão do `WolfBreed.cs` ainda é o do Bruto antigo (escudo 200, garra
> 120…). Não vale para nada hoje, porque o `lobo.tres` escreve todos os
> campos; um `.tres` novo, porém, nasce com esses números.

### 4.1 `lobo.tres`

| Campo | Valor | O que faz |
|---|---|---|
| `SpeedMultiplier` | 1,352 | × `HumanSpeed` (3,2): 4,33 m/s. **É o botão da velocidade do lobo** |
| `JumpMultiplier` | 1,435 | × `JumpVelocity` (4,4): 6,31 m/s, sobe 2,09 m |
| `Shield` | 100 | Escudo ao virar lobo; o rugido devolve inteiro |
| `ShieldDecay` | 5,5 /s | O escudo escorre: ~18 s do cheio ao zero |
| `BodyResistance` | 1,0 | Dano no corpo × isto (a cabeça ignora). 1 = leva o tiro inteiro (canhão de vidro) |
| `AttackDamage` | 65 | Base de **todos** os golpes (seção 6) |
| `AirAttackBonus` | 1,75 | × dano caindo de ≥ `Weapon.AirAttackMinFall` (2,5 m) |
| `AttackRange` | 4,1 m | Só bot: distância em que decide atacar |
| `WpCost` | 0 | KP para virar lobo (decisão do Jhon: 0) |

### 4.2 Movimento do lobo (nó `Player`)

| Campo | Onde | Valor | O que faz |
|---|---|---|---|
| `JumpVelocity` | `Player.Movement.cs` | 4,4 m/s | Base do pulo do lobo e do salto da parede. O humano tem o dele |
| `WolfJumpCarry` | idem | 0,9 | Quanto do passo o pulo leva: o pulo do lobo é para cima, não para a frente (~5,1 m de avanço correndo) |
| `WolfSprintFactor` | **`player.tscn`** (script 1,495) | 1,719 | Corrida de quatro (W W): 7,44 m/s. Mude na cena |
| `DoubleTapWindow` | `Player.Movement.cs` | 0,3 s | Tempo entre os dois toques no W |
| `WolfBackFactor` | idem | 0,75 | De costas, × a velocidade |
| `WolfChargeMoveFactor` | idem | 0,234 | Andando com o especial armado. Amarrado ao `ChargeWalkClipSpeed` (0,91, nó `WolfVisual`) |
| `CrouchHeight` / `StandEyeHeight` / `CrouchEyeHeight` | idem | 1,0 / 0,6 / 0,1 m | Cápsula agachada e o pivô da câmera de 3ª pessoa |

O lobo cai com a gravidade inteira (o `HumanGravityScale` é só do
humano).

### 4.3 Corrida na parede (`Player.WallRun.cs`)

| Campo | Valor | O que faz |
|---|---|---|
| `WallRunSpeedBonus` | 1,14268 | Velocidade na parede = `HumanSpeed` × `SpeedMultiplier` × isto: 4,94 m/s |
| `WallJumpClimb` | 1,02 | Subida do salto da parede, × `JumpVelocity` × `JumpMultiplier` |
| `WallJumpPush` / `WallJumpKeepSpeed` | 6,5 m/s / 0,8 | Empurrão para fora e quanto da velocidade da parede o salto leva |
| `WallGrabHeight` / `WallGrabMaxRise` | 0,45 m / 0,6 m/s | Quão alto e quão devagar precisa estar para grudar |
| `WallRunMaxTime` / `WallRunVerticalDrift` | 0 / 0 | Sem limite de tempo, altura travada |

Os outros 30 campos (quinas, vãos, postes, obstáculos) estão no
[`AJUSTES.md`, seção 3](AJUSTES.md#3-corrida-na-parede). A câmera não
inclina na parede (regra 8).

**Testes que prendem o lobo:** `VelocidadeNoChao` (4,326 e 1,947
escritos), `Medidas` (4,33; 7,44; 2,09), `AlturaDoPulo` (2,09),
`CorridaNaParede` (4,944), `CorridaDeQuatro` (pelo campo), `SaltoDaParede`
e os outros de `WallTests.cs` (pelos campos), `ParedeSobeObstaculo`,
`ParedeVaoEDegrau`, `ParedeNaCidade`, `ParedeNoArmazem`, `ParedeNoNeon`,
`ParedePosteEPlaca`, `PostesNaCidade`, `PassadaDoLobo`,
`EscadaDeQuatroDeserto`, `EscadaDeQuatroEstacao`, e online `ParedeOnline`
e companhia.

---

## 5. Lobo: tempo, escudo, transformação e KP

`src/scripts/forms/FormController.cs`, nó `FormController` no jogador e no
bot. Nenhuma cena troca estes campos: **o padrão do script vale para os
dois**, e o servidor de partida confere a forma com eles.

| Campo | Valor | O que faz |
|---|---|---|
| `WolfTimeLimit` | 30 s | Quanto se fica lobo de uma vez. Acabou, volta a humano sozinho. 0 desliga |
| `WolfCooldown` | 20 s | Espera para virar de novo, contada do começo da volta. Morrer zera |
| `WolfWarnAt` | 5 s | Aviso no HUD antes do fim |
| `ShiftDuration` / `ShiftBackDuration` | 0,55 / 0,2 s | Virar lobo / voltar a humano |
| `ShiftDamageMultiplier` | 1,5 | Dano recebido durante a transformação |
| `RefillOnTransform` | não | Encher a vida ao trocar de forma (sim vira cura infinita) |
| `HumanHp` | 200 | A vida, nas duas formas (seção 10) |
| `MaxWp` | 10000 | Teto de KP na partida |
| `WpPerKill` / `WpPerWolfKill` / `WpPerAssist` | 125 / 88 / 44 | KP por abate de arma, de lobo e assistência. **O padrão vem de `MatchKp`** (`src/scripts/domain/MatchKp.cs`), e o servidor confere o gasto com o `MatchKp`: mude lá, não no Inspetor |

O escudo (100, escorrendo 5,5/s) acaba antes do tempo de lobo (30 s): o
rugido é o que segura o lobo vivo. `Health.ShieldDecayGrace` (1 s) é a
carência depois do rugido.

**Testes:** `TempoDeLobo` (**prende 30, 20 e 5 de fábrica**: mudou, mude o
teste), `LoboCanhaoDeVidro` (escudo 100, resistência 1,0, garra mata em
dois golpes), `EfeitosDoLobo` (`ShiftDuration`), `KpNaoCaiAoMorrer`,
`KpNoServidor`, `MatchKpTests` (unidade).

---

## 6. Lobo: golpes e melhorias

Campos no nó `Weapon` (`src/scripts/combat/weapon/Weapon.Wolf*.cs`), o
ritmo dos clipes em `src/scripts/forms/WolfMoves.cs`, as contas em
`src/scripts/domain/CombatRules.cs`.

### 6.1 A conta do dano

dano = `AttackDamage` (65) × golpe × `ClawVsHumanBonus` (1,6, se o alvo é
humano) × `AirAttackBonus` (1,75, se caiu de ≥ 2,5 m). No alvo: ×
`ShiftDamageMultiplier` se ele está transformando, × `BodyResistance`
fora da cabeça, e o escudo absorve antes da vida.

| Golpe | Multiplicador | Contra humano | Contra lobo |
|---|---|---|---|
| Garra 1ª e 2ª | `ComboDamage` 1 / 1 | 104 (mata em dois) | 65 |
| Garra 3ª | 1,5 | 156 | 97,5 |
| Especial, alvo principal | `HeavyDamageBonus` 3,7 | 385 | 240 |
| Especial, em volta | `PounceSplash` 0,6 até 1,5 m, 0,3 na borda (3,5 m) | 231 → 116 | 144 → 72 |
| Bote | `LeapDamageBonus` 3,0 | 312 | 195 |
| Bote caindo ≥ 2,5 m | × 1,75 | 546 | 341 |

**O dano de todos os golpes sobe junto com o `AttackDamage`.** Para mexer
num golpe só, mexa no multiplicador dele.

### 6.2 Garra (clique esquerdo)

| Campo | Onde | Valor | O que faz |
|---|---|---|---|
| `ComboDamage` | `Weapon.WolfClaws.cs` | 1 / 1 / 1,5 | 1º, 2º e 3º golpe |
| `ComboReset` | idem | 1,1 s | Sem bater por isso, o combo volta ao 1º |
| `ClawReach` / `ClawRadius` | idem | 1,5 / 1,15 m | O volume do acerto |
| `ClawLunge` | idem | 6,5 m/s | O passo para a frente a cada golpe |
| `ClawVsHumanBonus` | idem | 1,6 | Bônus contra humano |
| `AttackBuffer` | idem | 0,45 s | Clique guardado para o próximo golpe |
| `ClawCancelAfter` | idem | 0,19 s | Depois do impacto, quando a próxima garrada pode sair |
| Velocidade do clipe | `WolfMoves.Of`, `WolfMove.Claw` | 1,3× | O golpe chega 0,154 s depois do clique e dura 0,56 s. **Mude só esta velocidade** para o golpe ficar mais rápido ou mais pesado: duração e instante do dano acompanham |

O compasso (`StrikeSpacing` = impacto + `ClawCancelAfter` = 0,344 s) é o
mínimo entre dois golpes que ferem, inclusive o bote. O servidor confere
o mesmo compasso (`WolfStrikePace`).

### 6.3 Especial (Q)

Arma com o Q quando a carga está cheia; o clique salta. Os campos ficam
em `Weapon.WolfPounce.cs` e `Weapon.SpecialCharge.cs`.

| Campo | Valor | O que faz |
|---|---|---|
| `HeavyDamageBonus` | 3,7 | Dano no alvo principal (× a garra) |
| `PounceAreaRadius` / `PounceSplashInner` | 3,5 / 1,5 m | A área e onde o dano começa a cair |
| `PounceSplash` / `PounceSplashFar` | 0,6 / 0,3 | Fração do dano do principal perto e na borda |
| `PounceMaxDistance` | 12 m | Alcance do salto |
| `SpecialGuard` | 0,4 | Fração do dano que o lobo leva armado e no salto |
| `SpecialArmTime` | 5 s | Depois do Q, o prazo para usar |
| `HeavyCooldown` | 1 s | Espera depois do salto |
| `LockAngle` / `LockRange` (nó `Player`, `Player.Lock.cs`) | 20° / 14 m (seguram até 32° / 16 m) | A trava no alvo |
| Velocidade do clipe | `WolfMoves.Of`, `Pounce` | 1× | O salto dura 2,2 s, o dano sai em 1,33 s |

**A carga** (`Weapon.SpecialCharge.cs`): na partida, um abate enche
1 / max(`SpecialMinKills` 6, pontos da sala × `SpecialScoreShare` 0,2) — 8
abates numa sala de 40 pontos; uma assistência vale `SpecialAssistShare`
0,4 de um abate; sozinha enche em `SpecialMatchShare` 0,7 do tempo da
partida (7 min numa de 10). Fora de partida (treino) valem
`SpecialPerKill` 0,125 e `SpecialFillTime` 420 s.

O caminho do voo (`Weapon.WolfPouncePath.cs`) e o resto do especial estão
no [`AJUSTES.md`, seção 4](AJUSTES.md#4-golpes-do-lobo).

### 6.4 Bote (botão direito no ar)

| Campo | Onde | Valor | O que faz |
|---|---|---|---|
| `LeapDamageBonus` | `Weapon.WolfDive.cs` | 3,0 | Dano do bote (× a garra) |
| `LeapCooldown` | idem | 1 s | Espera entre botes |
| `LeapMinHeight` | `Weapon.WolfPounce.cs` | 0,3 m | Altura mínima para sair |
| `DiveSpeed` / `DiveMaxTime` | **`player.tscn`** (script 13 / 1,1, que é o do bot) | 18 m/s / 1,0 s | Velocidade e duração do mergulho |
| `LeapReach` / `LeapRadius` | `Weapon.WolfDive.cs` | 1,7 / 1,5 m | Volume do acerto |
| `LeapBuffer` | `Player.Actions.cs` | 0,25 s | Botão direito apertado no chão fica guardado |
| Aterrissagem | `WolfMoves.Land` | 0,87 s | Quanto o bote prende o lobo no chão |

### 6.5 Rugido (R)

Dura 1,53 s (`WolfMoves`, clipe `howl` a 1×) e devolve o escudo inteiro.
O raio em que os bots ouvem é o `Howl` do `ruido.tres` (45 m).

### 6.6 Melhorias de lobo

`src/scripts/meta/Catalog.cs`, campo `WolfUpgrade` de cada item (`WolfUpgradeEffect`;
ids `up_*`, tipo `wolf_upgrade` no servidor — até 27/11, `tat_*` e
`tattoo`, do tempo em que se chamavam tatuagens). O
`Profile.BuildBreed` multiplica uma cópia do `lobo.tres` pelas
equipadas; o servidor de partida monta o lobo do mesmo jeito. Os bots não
têm melhorias.

| Melhoria | Efeito |
|---|---|
| Pata Leve | velocidade ×1,05 |
| Salto Longo | pulo ×1,06 |
| Couro Grosso | escudo ×1,10 |
| Garra Afiada | garra ×1,06 |
| Pelo Denso | escudo escorre ×0,8 |
| Caçador Noturno | velocidade ×1,06 e pulo ×1,05 |
| Presas de Aço | garra ×1,09 |
| Alfa | escudo ×1,12 e escorre ×0,8 |
| Fúria da Lua | garra ×1,07 e velocidade ×1,05 |

A regra 1 do `CLAUDE.md` pede um eixo só por melhoria; as três de dois
efeitos (épicas e lendárias, 19/11) são exceção registrada no comentário
do `Catalog.cs`. O preço na loja não é deste arquivo (seção 14).

**Testes dos golpes:** `DanoDaGarra` (104 escrito), `LoboCanhaoDeVidro`,
`EspecialEmArea`, `EspecialPreciso`, `EspecialArmado`, `CargaDoSalto`,
`CargaDoEspecial`, `SaltoCarregado`, `GarradaRapida`, `CliquesDaGarra`,
`LoboSemSpam`, `LoboSemSpamNoServidor`, `GarraDepoisDoBote`,
`BoteCortaGarra`, `LoboResponde`; na unidade, `CombatRulesTests`
(`WolfClaw`, `SpecialDamage`, `StrikeSpacing`) e `WolfMovesTests`
(`ClawTiming`, `PounceTiming`…).

---

## 7. Armas de fogo

Um `.tres` por arma em `src/resources/weapons/` (script
`src/scripts/combat/WeaponDefinition.cs`), editado à mão no Inspetor ou no
texto.

### 7.1 As versões (skins) são cópias inteiras

`ar15_ouro.tres`, `m4a4_dragao.tres` e as outras **não herdam nada** da
arma de base: cada uma tem todos os números dela, um pouco mais fortes.
Mexer no `ar15.tres` não muda as cinco versões do AR-15 (`ar15_8bit`,
`ar15_diamante`, `ar15_lava`, `ar15_ouro`, `ar15_ultimate`). Quer que elas
acompanhem? Edite cada arquivo. A ficha de cada uma está no
`docs/ARSENAL.md`.

O teste `ArmasEpicasELendarias` confere as versões da M4A4, AK-74,
SCAR-H, SVD e Desert Eagle (as do AR-15 não): cada uma precisa ter
`Damage` × `Rpm` ≥ a de base, `Magazine` ≥, `ReloadTactical` ≤,
`RecoilVertical` ≤ e `WpCost` >. Fortaleceu a arma de base além de uma
versão? O teste reprova, e a versão precisa subir junto.

A arma do conjunto padrão é a M4A4 com a Desert Eagle
(`Arsenal.DefaultPrimary` / `DefaultSecondary`).

### 7.2 Os campos

| Grupo | Campo | O que faz |
|---|---|---|
| Identidade | `Class`, `WpCost` | A classe (fuzil, sniper, pistola) e o preço em KP na partida |
| Dano | `Damage` | Por bala, **× `CombatRules.FirearmDamageScale` (1,38)** no tiro |
| | `HeadshotMultiplier` | × na cabeça |
| | `Pellets` | Chumbos por tiro (1 em todas hoje) |
| | `WolfStun` | Atraso no lobo atingido (Desert Eagle: 0,25 s) |
| Cadência e munição | `Rpm`, `Mode` | Tiros por minuto; `Auto` (0) ou `Semi` (1) |
| | `Magazine`, `Reserve` | Pente e reserva. **A munição é infinita** (`Weapon.InfiniteAmmo` ligado): a reserva não desce ao recarregar |
| | `ReloadTactical`, `ReloadEmpty` | Recarga com bala na câmara e com o pente vazio (s) |
| | `DrawTime`, `HolsterTime` | Sacar e guardar (s) |
| | `ShellByShell`, `ShellTime`, `ShellEnter` | Recarga cartucho a cartucho (nenhuma arma usa) |
| Dispersão | `SpreadBase`, `SpreadPerShot`, `SpreadMax`, `SpreadRecovery`, `SpreadDelay` | Seção 8 |
| | `MoveSpreadMultiplier`, `CrouchSpreadMultiplier` | × andando (acima de 0,6 m/s) e agachado |
| Recuo | `RecoilVertical`, `RecoilVerticalLate`, `RecoilEarlyShots`, `RecoilHorizontal`, `RecoilHorizontalLate`, `RecoilHorizontalAfter`, `RecoilRecovery`, `AimRecoilMultiplier` | Seção 8 |
| Alcance | `Range` | Até onde a bala vai (m) |
| | `FalloffStart`, `FalloffEnd`, `FalloffMin` | Dano inteiro até o começo, cai em linha reta até o fim, fica no mínimo depois |
| Corpo | `SpeedMultiplier` | Velocidade do humano com ela na mão (seção 3) |
| | `NoiseRadius` | Até onde os bots ouvem o tiro (m) |
| Mira | `AimFov` | Abertura da câmera mirando (graus; a de fora é 75) |
| | `AimSpreadMultiplier`, `AimSpeedMultiplier` | Dispersão e passo que sobram mirando |
| | `AimLift` | Quanto a arma sobe mirando (só a pistola) |
| Estrelas | `Star*` | Só a loja mostra. Não mudam nada no tiro |

O tempo de entrar na mira é do nó `Weapon`: `AimTime` 0,16 s (a luneta da
SVD, `ScopeTime` 0,3 s). A sensibilidade do mouse cai sozinha na razão
das aberturas.

### 7.3 Tiros para matar hoje

Sem queda de dano, contra 200 de vida (humano) e 300 (lobo: 200 + 100 de
escudo, resistência 1,0):

| Arma | Dano por bala (× 1,38) | Corpo, humano | Cabeça, humano | Corpo, lobo |
|---|---|---|---|---|
| AR-15 (27) | 37,3 | 6 | 3 | 9 |
| M4A4 (26) | 35,9 | 6 | 3 | 9 |
| AK-74 (42) | 58,0 | 4 | 2 | 6 |
| SCAR-H (48) | 66,2 | 4 (três dão 198,7) | 2 | 5 |
| SVD (85) | 117,3 | 2 | 1 | 3 |
| Desert Eagle (55) | 75,9 | 3 | 2 | 4 |

Os números completos de cada `.tres` estão no
[`AJUSTES.md`, seção 7](AJUSTES.md#7-armas-de-fogo) e no `docs/ARSENAL.md`.

**Mudar todas as armas de uma vez:** `CombatRules.FirearmDamageScale`
(constante em `src/scripts/domain/CombatRules.cs`). A garra, a faca e a
granada não passam por ele. O servidor usa a mesma constante.

**Testes:** `TiroNaCabeca` (pelo `HeadshotMultiplier`), `ZoomNaMira`
(`AimFov` e `AimSpreadMultiplier` de cada arma, pelos campos),
`MunicaoFinita`, `ArmasEpicasELendarias`, `TiroPelaMiraDaPistola`
(`AimLift`), `VidaNoServidor`; na unidade, `CombatRulesTests`
(`M16Damage` passa 27 e 2,5 escritos e o `FirearmDamageScale`).

---

## 8. Recuo e dispersão

### 8.1 Onde o recuo mora

| Peça | Onde | O que faz |
|---|---|---|
| Recuo por tiro | `.tres` da arma | `RecoilVertical` nos primeiros `RecoilEarlyShots` tiros, `RecoilVerticalLate` depois (graus, a mira sobe). `RecoilHorizontal`: o lado, alternando direita e esquerda (decorável); depois de `RecoilHorizontalAfter` tiros (só a AK, 6), sorteado entre ± `RecoilHorizontalLate` (0,7) |
| Volta | `.tres`: `RecoilRecovery` | Graus por segundo que a mira desce sozinha depois de parar de atirar. O lateral não volta: é ele que o jogador aprende a compensar |
| Mirando | `.tres`: `AimRecoilMultiplier` | O que sobra do recuo com o zoom (só a SCAR: 0,35) |
| Global | nó `Player` (`Player.Camera.cs`) | `RecoilScale` 1 (multiplica o recuo e a volta de toda arma), `RecoilMax` 4,5° (teto do vertical acumulado), `RecoilReturnDelay` 0,12 s (sem atirar por isso, a mira começa a descer) |
| A rajada | `Weapon.cs` (código) | A contagem de tiros seguidos zera com 0,3 s sem atirar (e ao trocar de arma) |
| A conta | `CombatRules.RecoilVertical` | Primeiros tiros ou fase de controle |

**O teto manda mais que o recuo por tiro.** O AR-15 sobe 1° nos quatro
primeiros (4°) e bate nos 4,5° no quinto; a AK (1,8°) bate no terceiro.
Daí em diante a mira não sobe mais, a não ser que o jogador puxe o mouse
para baixo: puxar desconta do acumulado, e cada tiro volta a subir o
`RecoilVerticalLate`. Como as automáticas atiram mais rápido que os 0,12 s
do `RecoilReturnDelay`, a mira não volta durante o spray.

**O coice da arma na tela não é código.** Os braços de primeira pessoa
de todas as armas são "fiéis" (`"fiel": true` em
`src/assets/armas/fp/bracos.json`): o coice que se vê é o da animação de
atirar. Os `ViewModel.Kick` (0,055 m) e `KickPitch` (3,2°) só valeriam
para uma arma sem conjunto fiel, e hoje não há nenhuma.

O bot não tem recuo: quem erra por ele é o nível de dificuldade (seção 13).

### 8.2 Dispersão

Meio-ângulo do cone, em graus: (`SpreadBase` + acumulado) ×
`MoveSpreadMultiplier` (andando acima de `CombatRules.MovingSpreadSpeed`,
0,6 m/s) × `CrouchSpreadMultiplier` (agachado) × `AimSpreadMultiplier`
(mirando). Cada tiro soma `SpreadPerShot` ao acumulado, até `SpreadMax`;
sem atirar por `SpreadDelay` (0,12 s de padrão; 0,05 no AR-15, M4A4 e
versões), o acumulado desce `SpreadRecovery` graus por segundo. O
primeiro tiro parado sai sempre com a `SpreadBase`. O servidor soma a
dispersão da arma à folga dele sozinho.

**Testes:** `ZoomNaMira`, `TiroNaCruz`, `ArmasEpicasELendarias`
(`RecoilVertical` das versões ≤ a de base); na unidade, `RecoilPhases`,
`SpreadMultipliers`, `BloomCaps`, `BloomRecovers` (números escritos no
teste).

---

## 9. Faca, coronhada, granada e soltar arma

### 9.1 Faca (`Weapon.Knife.cs`, nó `Weapon`)

| Campo | Valor | O que faz |
|---|---|---|
| `KnifeDamage` / `KnifeRange` / `KnifeCooldown` | 85 / 2,2 m / 0,4 s | O golpe rápido (clique esquerdo) |
| `KnifeStabDamage` / `KnifeStabBehind` / `KnifeStabCone` | 120 / ×2 / 60° | A estocada (botão direito): de frente, pelas costas, e o cone das costas |
| `KnifeStabHitDelay` / `KnifeStabCooldown` | 0,2 / 0,8 s | Quando o dano sai e quanto a faca fica ocupada |
| `KnifeSpeedMultiplier` | 1,28 | Velocidade andando com a faca |
| `Weapon.HeadshotMultiplier` (`Weapon.Damage.cs`) | 2,5 | Cabeça com a faca (na estocada não soma com as costas: vale o maior) |

As facas da loja multiplicam estes números: `Arsenal.KnifeStats`
(damasco ×1,06 dano e ×0,95 espera; obsidiana ×1,12, ×0,9 e alcance
×1,05). A faca não tem `.tres`. Online, o teto da faca de bot é
`HitCheck.BotKnifeMax` (420).

### 9.2 Coronhada (V, `Weapon.Butt.cs`)

`ButtDamage` 10, `ButtRange` 1,9 m, `ButtCooldown` 0,4 s, `ButtHitDelay`
0,075 s. Não gasta munição e não tem cabeça. O V com a faca é o golpe da
faca; com a granada, arremessa. O teste `Coronhada` confere que a 4 m não
alcança.

### 9.3 Granada

| Campo | Onde | Valor | O que faz |
|---|---|---|---|
| `MaxDamage` | `Grenade.cs` | 300 | Dano no centro, caindo em linha reta até a borda: mata 200 de vida a até 3 m |
| `Radius` | idem | 9 m | Raio do dano (o ruído vai a 6 vezes isso, 54 m) |
| `Fuse` / `MaxFlight` | idem | 1,2 / 6 s | Do primeiro toque até explodir; sem tocar nada, explode no `MaxFlight` |
| `SightGap` | idem | 0,06 m | Parede, barreira e fresta mais fina que isto protegem |
| `Bounciness` / `GroundFriction` | idem | 0,42 / 0,85 | O quique e o rolar |
| `ThrowForce` / `ThrowArc` | `Weapon.Grenade.cs` | 17,5 / 4 m/s | Força do arremesso e empurrão para cima |
| `ThrowMaxRange` | idem | 24 m | Até onde a granada cai onde a mira aponta |
| `ThrowLoft` / `ThrowLoftNear` | idem | 22° / 6° | O arco longe e perto |
| `ThrowCooldown` | idem | 0,9 s | Entre duas granadas |
| Quantidade | `Arsenal.Utilities` (`Count`) | 1 por espaço | Sem conjunto escolhido, o padrão do `Weapon`: `MaxGrenades` 2 |
| Preço | `Arsenal.Utilities` (`WpCost`) | 60 KP | Todas as versões custam o mesmo |
| Massa e tamanho | `src/scenes/grenade.tscn` | 0,45 kg, raio 0,14 m | A física do corpo |

O servidor lê o `MaxDamage` da cena da granada. A de choque (`Kind`
`Shock`, `StunDuration` 2,5 s) está guardada, fora de uso.

### 9.4 Soltar arma (G)

`DropThrow` 5,5 m/s e `DropLift` 1,8 m/s (`Weapon.Inventory.cs`).

**Testes:** `EstocadaDaFaca` (120 de frente, mata pelas costas),
`EstocadaDeFora`, `Coronhada`, `Granada`, `GranadaNaMira`, `GranadaNaCruz`,
`GranadaFere`, `GranadaParede` (**300 escrito no teste**), `GranadaQuica`,
`GranadaSom`, `GranadaNoPe`, `GranadaNoPeOnline`, `BotsJogamGranada`,
`ArmaNoChaoRepasse`, `ArmaNoChaoAutoritativo`; na unidade, `KnifeStab`.

---

## 10. Vida, escudo, renascer e queda

### 10.1 Vida e renascer (`src/scripts/combat/Health.cs`, nó `Health`)

| Campo | Valor | O que faz |
|---|---|---|
| `FormController.HumanHp` | 200 | A vida de todo mundo, nas duas formas |
| `HpMultiplier` (`src/assets/personagens/*/personagem.tres`) | Eve 1,0; Barut 1,0 | O personagem multiplica a vida |
| `RespawnDelay` | 10 s | Espera para renascer na partida |
| `TrainingRespawnDelay` | 3 s | No campo de treino |
| `SpawnProtection` | 3 s | Imortal contra os outros ao renascer (a própria granada fere) |
| `RegenDelay` / `RegenRate` | 15 s / 0,08 | Sem levar dano por 15 s, a vida volta 8% da máxima por segundo. O escudo não volta sozinho |
| `ShieldDecayGrace` | 1 s | Carência do escudo depois do rugido |

O escudo e a resistência do lobo estão no `lobo.tres` (seção 4). A cabeça
de quem não tem modelo à vista: `Weapon.HeadZone` 0,72 m acima do centro.

### 10.2 Dano de queda (`src/resources/body/queda.tres`)

| Campo | Valor | O que faz |
|---|---|---|
| `HumanSafe` / `HumanPerMeter` | 4 m / 25 | O humano não se fere até 4 m; depois, 25 por metro (a 12 m, 200) |
| `WolfSafe` / `WolfPerMeter` | 7 m / 12,5 | O lobo, mais alto e metade |

A queda conta do ponto mais alto até o pouso. Bote, especial e salto da
parede não ferem. Conta em `CombatRules.FallDamage`, medida em
`FallTracker.cs`.

**Testes:** `RenasceHumano`, `RenasceInteiro`, `VidaVoltaRepasse`,
`VidaVoltaAutoritativo` e os outros `VidaVolta*`, `VidaNoServidor`,
`DanoDeQueda` (faixas: ~2 m nada, ~8 m entre 60 e 140, ~13 m mata; lobo
de ~12 m entre 30 e 100), `TiroDeCimaCidade` e os outros `TiroDeCima*`.

---

## 11. Velocidade e animação

As duas formas andam o que as pernas andam (regra 9): o clipe toca na
velocidade do corpo dividida pela passada medida dele. Daí a divisão:

### 11.1 Medidas — não são botão

Mudar estes números faz o pé patinar sem ninguém notar na hora.

| Campo | Onde | Valor |
|---|---|---|
| `WalkClip`, `RunClip`, `BackWalkClip`, `BackRunClip`, `StrafeClip` | nó `Human` (`CharacterAnimator.Locomotion.cs`) | 1,0 / 3,02 / 1,1 / 2,59 / 1,03 m/s |
| `SprintClip` | idem | 3,35 m/s |
| `WalkClipSpeed`, `WalkBackClipSpeed`, `WalkSideClipSpeed`, `RunClipSpeed`, `CrawlClipSpeed`, `SprintClipSpeed` | nó `WolfVisual` (`WolfVisual.Locomotion.cs`) | 3,95 / 3,6 / 3,75 / 2,42 / 1,5 / 3,95 m/s |
| `WallClipSpeed` | `WolfVisual.Air.cs` | 2,3 no script, **1,5 no `player.tscn`**. A conta tem teto de 2,5×: com o jogador a 4,94 m/s na parede o clipe toca no teto |
| `ChargeWalkClipSpeed` | `WolfVisual.Moves.cs` | 0,91 m/s |
| `StrideHuman`, `StrideHumanSprint`, `StrideWolf`, `StrideWolfSprint` | `ruido.tres` (`BodyNoiseProfile.cs`) | 0,94 / 1,12 / 1,32 / 2,37 m |
| `ModelScale` | nó `WolfVisual` = `WolfMoves.BodyScale` | 1,1 |

### 11.2 Botões

| Campo | Onde | Valor | O que faz |
|---|---|---|---|
| `RunClipRate` | nó `Human` | 0,88 | O ritmo da corrida do humano × o do arquivo. **Nunca acima de 1** (regra 9) |
| `SprintPaceMax` | nó `Human` | 1,6 | Até quanto o clipe do Shift acelera |
| `BlendSmoothing` / `SprintBlendSmoothing` | nó `Human` | 9 / 16 | Quão rápido o corpo muda de pose |
| `CrouchWalkSpeed` | nó `Human` | 0,6 m/s | A partir disto o agachado toca o clipe de andar inteiro |
| `WalkAnimScale`, `SprintAnimScale`, `WallAnimScale` | nó `WolfVisual` | 1 | Quanto a animação do lobo corre em relação ao corpo (só a animação) |
| `WalkSpeed` / `RunSpeed` | nó `WolfVisual` | 2,0 / 2,6 m/s | Onde o lobo troca de andar para correr |
| Velocidade de cada golpe | `WolfMoves.Of` | garra 1,3×, resto 1× | Seção 6 |

**Para o corpo andar mais rápido sem patinar é preciso um clipe de passada
maior, não só o número.** Subiu a velocidade de uma forma? Rode
`PassadaHumana` ou `PassadaDoLobo`, `PassoNoRitmo` e `Ruido` (a contagem
de passos muda junto).

`WolfChargeMoveFactor` (seção 4) e `ChargeWalkClipSpeed` andam juntos:
mudou um, mude o outro.

---

## 12. Câmera e sensação

| O quê | Onde | Valor | Nota |
|---|---|---|---|
| Abertura da 1ª pessoa | `Head/Camera3D` do `player.tscn` (sem `fov` escrito) | 75° | O `Player` lê a da cena no `_Ready`. Mas a câmera da morte volta a 75 escrito no código (`KillCam.cs`, `KillCam.Pov.cs`): mudou a abertura, mude lá também |
| Abertura do lobo | `Head/SpringArm3D/WolfCamera` | 75° | Mais `WolfFx.AirFovKick` (5°) no ar |
| Abertura mirando | `AimFov` de cada `.tres` | 55 (SCAR 50, Deagle 65, SVD 18) | Nunca abre mais que a de fora |
| Sensibilidade | `Player.MouseSensitivity` | 0,002 rad por pixel | × `Settings.Sensitivity` (Opções do jogador, padrão 1). Mirando, cai na razão das aberturas sozinha |
| Distância do lobo | `Player.ThirdPersonDistance` | 2,5 m | Posta no braço da câmera no `_Ready` (o 3,5 da cena não vale) |
| Braço do lobo | `CameraArmEase` (filho do braço) | `InSpeed` 12, `OutSpeed` 2,5 m/s, `Slack` 0,15 m | Entra rápido quando algo fica atrás, sai devagar |
| Pivô do lobo | `StandEyeHeight` / `CrouchEyeHeight` | 0,6 / 0,1 m | Acima do centro da cápsula |
| Balanço da câmera | — | não há | A câmera não balança no passo nem inclina na parede (regra 8). O degrau e a escada: seção 3.4 |
| Balanço da arma | `ViewModel` (`BobAmount`, `SwayAmount`…) | — | Não vale hoje: todas as armas são conjuntos fiéis (seção 8.1) |
| Recuo da câmera | `Player.RecoilScale`, `RecoilMax`, `RecoilReturnDelay` | 1 / 4,5° / 0,12 s | Seção 8 |
| Câmera da morte | `LoadoutMenu.KillCamTime` | 4,2 s | |

---

## 13. Bots e dificuldade

### 13.1 O que vem do nível

`src/resources/bots/facil.tres`, `medio.tres`, `dificil.tres` (classe
`BotDifficulty`). Na entrada da partida, `Difficulty.Apply` escreve estes
campos por cima do `bot.tscn`. **Para esses campos, mude no `.tres` do
nível, não na cena.**

| Campo | Fácil | Médio | Difícil | O que faz |
|---|---|---|---|---|
| `ReactionTime` (± `ReactionJitter`) | 0,8 (±0,35) | 0,4 (±0,2) | 0,2 (±0,1) s | Reação ao ver ou levar tiro |
| `AimError` | 9° | 4,5° | 2° | Erro de mira |
| `MissChance` | 45% | 20% | 8% | Tiros errados de propósito |
| `SightRange` / `FieldOfView` | 26 m / 100° | 40 m / 140° | 55 m / 170° | Visão |
| `EngageRange` | 24 | 40 | 55 m | Começa a atirar |
| `BurstTime` / `BurstPause` | 0,45 / 1,3 | 0,7 / 0,65 | 0,95 / 0,35 s | Rajada |
| `Speed` | 2,5 | 3,0 | 3,5 m/s | Velocidade do humano do bot (lobo × 1,352) |
| `Aggression` | 0,5 | 0,8 | 1,0 | Avança em vez de circular |
| `GrenadeRate` / `WolfSpecialRate` | 0,12 / 0,25 | 0,3 / 0,5 | 0,5 / 0,75 | Granada e especial do lobo |

Os outros campos do nível (audição, memória, giro, strafe, ronda) estão
no [`AJUSTES.md`, seção 9](AJUSTES.md#9-bots-e-dificuldade).

O teste `Dificuldade` confere que cada nível é mais competente que o
anterior em cada alavanca e que o difícil ainda erra. Mude à vontade, sem
inverter a ordem.

### 13.2 O que fica no `bot.tscn` (padrões do script)

`MissSpread` 14°, `WolfHealthThreshold` 0,4 e `WolfRange` 14 m (quando
vira lobo), `WolfSprintFactor` 1,3, `JumpVelocity` 4,4, `JumpMaxHeight`
1,1 m, o humor de lobo (`WolfLove` 0,38, `Bot.Persona.cs`) e a granada
(`Bot.Grenade.cs`). Lista completa no `AJUSTES.md`, seção 9. Lembre a
seção 1.3: o movimento do bot não é o do jogador.

Note que o bot humano anda a `Speed` do nível, sem o peso da arma: no
médio, 3,0 m/s, mais que o jogador (2,82).

**Testes:** `Dificuldade`, `BotsAtiram`, `BotsCacam`, `BotsJogamGranada`,
`LobosUsamEspeciais`, `GalopeDosBots`, `TempoDeLobo` (o bot respeita o
tempo e a espera).

---

## 14. Partida, sala e economia

### 14.1 A sala e a partida

| O quê | Onde | Valor |
|---|---|---|
| Sala padrão | `RoomSettings` em `src/scripts/core/RoomConfig.cs` | 6 por time, Armazém, 40 pontos, 10 min, todas as armas, bots no Médio |
| Opções da tela da sala | `src/scripts/lobby/RoomScreen.cs` | pontos 20, 40, 60, 100; minutos 5 a 30 |
| Opções da sala online | `src/scripts/lobby/OnlineScreen.cs` (`Scores`, `Durations`) | pontos 10 a 100; minutos 5 a 30 |
| Tamanho máximo do time | `RoomConfig.MaxTeamSize` **e** `servidor/Alcateia.Dominio/Salas/Room.cs` (`MaxTeamSize`) | 6, nos dois |
| Contagem inicial | `Match.CountdownTime` | 10 s (`ScoreToWin` e `Duration` do `Match` são trocados pela sala) |
| Escolher conjunto | `LoadoutMenu.ChooseTime` | 15 s |
| Renascer | `Health.RespawnDelay` | 10 s (seção 10) |
| Entrar no meio online | `servidor/Alcateia.Dominio/Frota/Fleet.cs` | Fecha nos últimos 120 s ou 20% do tempo (`LastSeconds`, `LastShare`), ou com um time a 80% dos pontos (`DecidedShare`) |

A carga do especial do lobo anda com os pontos e o tempo da sala (seção
6.3).

### 14.2 KP na partida

| O quê | Onde |
|---|---|
| Ganhos: 125 por abate, 88 de lobo, 44 por assistência, 50 por renascer com o conjunto padrão, teto 10000 | `src/scripts/domain/MatchKp.cs` (o servidor confere com ele) |
| Preço de cada arma | `WpCost` do `.tres` |
| Preço da granada | `Arsenal.Utilities` (60) |
| Virar lobo | `WpCost` do `lobo.tres` (0) |

### 14.3 XP e Cash (fora da partida)

| O quê | Onde | Nota |
|---|---|---|
| XP por partida: 30 base, 4 por abate, 2 por assistência, 24 vitória, 12 empate, 15 MVP | `src/scripts/domain/Rewards.cs` | **Compilado também no servidor da conta.** Mudou, suba os dois |
| Cash por partida (padrão: 150, 10, 5, 100, 50, 50) | `Rewards.cs` (`Presas*`) | O valor que vale online é o do **painel** ("recompensas", `CashRates`); o `.cs` é o padrão e a previsão da tela de resultado |
| Tetos de abates e assistências (120) | `Rewards.MaxKills` / `MaxAssists` | O servidor não paga acima |
| Curva das patentes | `src/scripts/meta/Ranks.cs` **e** `servidor/Alcateia.Dominio/Contas/RankRule.cs` | Mudou num, mude no outro (há teste) |
| Preço, raridade e patente na loja | `src/scripts/meta/Catalog.cs` → `src/tools/servidor/exportar_dados.py` → `servidor/Alcateia.Infra/Semente/jogo.json` | A semente só entra no banco o que ainda não existe. **Num servidor já povoado, mude o preço no painel** (decisão 0013) |
| Alertas de trapaça (8 abates por minuto, 90% de cabeça…) | `servidor/Alcateia.Dominio/Partidas/Plausibility.cs` | Só avisam. Revise se a calibragem mudar o ritmo da partida |

**Testes:** `RewardsTests` (unidade, números escritos), `MatchKpTests`,
`KpNoServidor`, `CenaPartida`, `VitoriaEmCameraLenta`, e no servidor
`src/tools/testar_servidor.sh`.

---

## 15. Ruído

Um arquivo só para o jogador e o bot: `src/resources/body/ruido.tres`
(classe `BodyNoiseProfile`). **O arquivo está vazio**: todos os valores
são os padrões do `BodyNoiseProfile.cs`. Mudar no Inspetor grava no
`.tres`; mudar no `.cs` muda o padrão.

| Campo | Valor (m) | O que é |
|---|---|---|
| `StepHuman` / `StepHumanSprint` | 13 / 22 | Passo do humano, andando e com Shift |
| `StepWolf` | 20 | Passo do lobo |
| `Land` | 17 | O baque do pouso |
| `Howl` | 45 | O rugido |
| `NoiseRadius` da arma | 45 a 80 | O tiro (`.tres`) |
| Granada | `Radius` × 6 = 54 | A explosão |

Agachado não faz passo; a faca é silenciosa. A audição de cada bot
multiplica os raios (`Hearing` do nível). As passadas (`Stride*`) são
medidas (seção 11).

**Testes:** `Ruido` (13, 22, 17 e 20 escritos; 8 passos em 3 s andando e
13 correndo), `PassoNoRitmo`.

---

## 16. Receitas

### 16.1 "O lobo está forte demais"

Escolha **um eixo** (regra 1):

- **Tempo:** `WolfTimeLimit` (30 s) ou `WolfCooldown` (20 s) no
  `FormController.cs`. O bot e o servidor seguem sozinhos. Reprova
  `TempoDeLobo`, que prende os de fábrica: mude o número esperado.
- **Dano:** `AttackDamage` no `lobo.tres` (todos os golpes juntos) ou o
  multiplicador de um golpe só (seção 6.1). Confira `DanoDaGarra` (104) e
  `LoboCanhaoDeVidro` (a garra ainda mata em dois?).
- **Resistência:** `Shield` ou `ShieldDecay` no `lobo.tres`.
  `LoboCanhaoDeVidro` prende o escudo em 100.

Decisão de jogo: anote no `DESIGN.md` (seção do lobo) e no `CLAUDE.md`,
que cita o canhão de vidro e os tempos.

### 16.2 "O lobo está rápido demais"

`SpeedMultiplier` no `lobo.tres`. **Não mexa no `HumanSpeed`**: ele é a
base das duas formas. A corrida de quatro é `WolfSprintFactor` **no
`player.tscn`** (1,719); a parede, `WallRunSpeedBonus`. Mais devagar não
patina; mais rápido, confira `PassadaDoLobo`. Reprovam `VelocidadeNoChao`
(4,326), `Medidas` (4,33 e 7,44) e `CorridaNaParede` (4,944): atualize. O
bot lobo anda `Speed` do nível × `SpeedMultiplier` e segue junto.

### 16.3 "Quero o pulo do lobo mais alto"

`JumpMultiplier` no `lobo.tres`. Antes: veja a seção 2.3. Acima de ~2,2
o lobo alcança o terraço de 5 m sem parede, e as melhorias ainda somam
até 11%. O salto da parede sobe junto (`WallJumpClimb` × pulo), e o bot
lobo também. Reprovam `AlturaDoPulo` e `Medidas` (2,09). Abra o Armazém e
pule no caixote, no contêiner e no terraço antes de fechar.

### 16.4 "O humano está lento"

`HumanSpeedBonus` (0,88 desde 27/11; era 0,8) no `Player.Movement.cs`. Acima
da passada de corrida (~3,0 m/s no corpo do jogo) o pé começa a patinar: aí o
problema é o clipe, não o número (seção 11). A corrida do Shift é × o andar:
para ela não mudar junto, desça o `HumanSprintFactor` na conta inversa (foi o
que se fez em 27/11). Reprovam `VelocidadeNoChao` e `Medidas` (2,816 e 2,82
escritos); o `Ruido` não (o passo segue o pé da animação, não a distância). O
bot humano não muda (`Speed` do nível).

### 16.5 "A corrida (Shift) está rápida ou lenta"

`HumanSprintFactor` (× o andar: 1,74327 sobre 2,816 = 4,91 m/s). O teste
`VelocidadeNoChao` lê o campo; o `Ruido` conta de 11 a 14 passos em 3 s (a
pisada do corpo animado). Correndo, faca e pistola não somam (27/11): todas
a 4,91, abaixo do teto do clipe (5,36, `SprintPaceMax`). A arma demora a voltar?
`SprintRecover` e `ViewModel.RigSprintOut` juntos.

### 16.6 "O pulo do humano"

`HumanJumpVelocity` (5,2) ou `HumanGravityScale` (1,45). O humano precisa
subir o caixote de 0,8 m e não o de 2,2. Reprovam `AlturaDoPulo` (0,99) e
`Medidas` (1,02). O bot humano usa o `Bot.JumpVelocity`, outro campo.

### 16.7 "O fuzil sobe demais no spray"

No `.tres` da arma: `RecoilVertical` para os primeiros tiros,
`RecoilVerticalLate` para o spray controlado, `RecoilEarlyShots` para
quando a fase muda. Para a mira voltar mais rápido, `RecoilRecovery`. Para
um teto menor em todas as armas, `Player.RecoilMax` (4,5°) — é ele que
segura o spray de quem não compensa (seção 8.1). Para todas mais leves,
`Player.RecoilScale`. Lembre das versões: cada `.tres` é uma cópia, e a
versão precisa ter `RecoilVertical` ≤ o da base (`ArmasEpicasELendarias`).

### 16.8 "A arma espalha demais andando"

`MoveSpreadMultiplier` do `.tres`. O limite de "andando" é
`CombatRules.MovingSpreadSpeed` (0,6 m/s), igual para todas. Mirando, a
dispersão é a que sobra pelo `AimSpreadMultiplier` (`ZoomNaMira` lê o
campo).

### 16.9 "A sniper mata (ou não mata) com um tiro no corpo"

Hoje não mata: 85 × 1,38 = 117 contra 200. Para matar no corpo,
`Damage` ≥ 145 no `svd.tres` (145 × 1,38 = 200,1); o lobo (300) passa a
cair em dois. A cabeça já mata (× 2,4). Edite também `svd_carbono.tres` e
`svd_tempestade.tres`, que precisam ter `Damage` × `Rpm` ≥ a de base. O
servidor lê o `.tres`: nada a mudar lá.

### 16.10 "Todas as armas estão fortes (ou fracas)"

`CombatRules.FirearmDamageScale` (1,38). Muda só as armas de fogo.
Reprova o `M16Damage` da unidade (37,26 = 27 × 1,38): atualize. Refaça a
tabela de tiros para matar (seção 7.3) no `AJUSTES.md` e no `ARSENAL.md`.

### 16.11 "Os bots difíceis estão difíceis demais"

`dificil.tres`: `AimError`, `MissChance`, `ReactionTime` primeiro. Não
deixe o difícil ficar abaixo do médio em nenhuma alavanca (`Dificuldade`
reprova). Para o bot não correr mais que o jogador, `Speed`.

### 16.12 "A granada está forte demais"

`MaxDamage` (300) ou `Radius` (9 m) no `Grenade.cs`. `GranadaParede`
prende os 300; `GranadaFere` e `BotsJogamGranada` conferem o resto. O
servidor lê a cena: nada a mudar lá.

### 16.13 "O especial carrega rápido demais"

`SpecialScoreShare` (0,2 dos pontos da sala), `SpecialMinKills` (6) e
`SpecialMatchShare` (0,7 do tempo) no nó `Weapon`. O teste
`CargaDoEspecial` lê os campos.

---

## 17. Depois de calibrar

### 17.1 Antes do commit

1. **Confira que mudou o valor que vale.** Campo na cena ganha do script
   (seção 1.2); campo do nível de dificuldade ganha do `bot.tscn`.
2. **Rode os testes que prendem o número.** Um nome filtra:
   `src/tools/testar.sh VelocidadeNoChao` (vale um pedaço do nome,
   `src/tools/testar.sh Granada` roda todos os de granada; o filtro vale
   também para os de unidade). Depois, a bateria inteira:
   `src/tools/testar.sh` (`--rapido` pula os de partida).
3. **Teste vermelho depois de uma mudança de propósito** quer dizer
   "atualize o número esperado": mude o teste no mesmo commit, dizendo no
   comentário por quê (data e pedido). Teste vermelho sem mudança
   combinada é regressão: conserte o código.
4. **Não crie teste novo para uma calibragem.** Teste novo só depois de
   propor ao Jhon e ele concordar.
5. **Mexeu em servidor ou rede** (as constantes da seção 1.4, `MatchKp`,
   `Rewards`)? Rode também `src/tools/testar_servidor.sh` e
   `src/tools/testar_online.sh`.

### 17.2 Que documento atualizar

| Mudou | Atualize |
|---|---|
| Qualquer número | A linha dele no `docs/AJUSTES.md` |
| Uma decisão de jogo (o lobo mais fraco, a granada menor) | A seção do sistema no `docs/DESIGN.md` |
| Velocidade, pulo, tempo de lobo, escudo | A tabela da regra 9 e o "O que o jogo é" no `CLAUDE.md`; o `docs/MEDIDAS.md` se foi medido |
| Arma | A ficha no `docs/ARSENAL.md` |
| Preço, ganho, KP | `docs/CONJUNTOS.md` |
| Algo que o jogador nota | `docs/CHANGELOG.md` |
| Este guia ficou errado | Esta página |

### 17.3 Que testes prendem cada número

| Número | Testes |
|---|---|
| `HumanSpeed`, `HumanSpeedBonus`, `CrouchSpeedFactor` | `VelocidadeNoChao`, `Medidas`, `Ruido`, `PassadaHumana` |
| `HumanSprintFactor`, `SprintRecover` | `VelocidadeNoChao`, `CorridaDoHumano`, `Ruido`, `ShiftPreso` |
| `KnifeSpeedMultiplier` | `VelocidadeNoChao` |
| `HumanJumpVelocity`, `HumanGravityScale` | `AlturaDoPulo`, `Medidas` |
| `SpeedMultiplier` do lobo, `WolfBackFactor` | `VelocidadeNoChao`, `Medidas`, `CorridaDeQuatro`, `PassadaDoLobo` |
| `WolfSprintFactor`, `DoubleTapWindow` | `Medidas`, `CorridaDeQuatro` |
| `JumpMultiplier`, `JumpVelocity`, `WallJumpClimb` | `AlturaDoPulo`, `Medidas`, `SaltoDaParede` |
| `WallRunSpeedBonus` e a parede | `CorridaNaParede`, `WallTests.cs`, `WallObstacleTests.cs`, `WallPoleTests.cs`, `ParedeOnline` |
| `Shield`, `BodyResistance`, `AttackDamage` | `LoboCanhaoDeVidro`, `DanoDaGarra`, `EspecialEmArea` |
| `WolfTimeLimit`, `WolfCooldown`, `WolfWarnAt` | `TempoDeLobo` |
| `ShiftDuration` | `EfeitosDoLobo` |
| Golpes do lobo, `ClawCancelAfter`, `LeapCooldown` | `CliquesDaGarra`, `GarradaRapida`, `LoboSemSpam`, `LoboSemSpamNoServidor`, `GarraDepoisDoBote`; unidade `CombatRulesTests`, `WolfMovesTests` |
| Especial e carga | `EspecialEmArea`, `EspecialPreciso`, `SaltoCarregado`, `CargaDoEspecial`, `CargaDoSalto` |
| Armas (`.tres`) | `ZoomNaMira`, `TiroNaCabeca`, `ArmasEpicasELendarias`, `MunicaoFinita`, `TiroPelaMiraDaPistola` |
| `FirearmDamageScale`, recuo e dispersão (contas) | unidade `CombatRulesTests` |
| Faca, coronhada | `EstocadaDaFaca`, `EstocadaDeFora`, `Coronhada`, `ArmasEpicasELendarias` |
| Granada | `GranadaParede`, `GranadaFere`, `Granada`, `GranadaQuica`, `GranadaSom` |
| Vida, renascer, regeneração | `RenasceInteiro`, `VidaVolta*`, `VidaNoServidor` |
| `queda.tres` | `DanoDeQueda`, `TiroDeCima*` |
| `ruido.tres` | `Ruido`, `PassoNoRitmo` |
| Dificuldade | `Dificuldade` |
| `MatchKp`, `Rewards` | `MatchKpTests`, `RewardsTests`, `KpNoServidor` |

Os testes de unidade passam os números escritos neles (65, 1,6, 3,7…):
mudar um `.tres` não os deixa vermelhos, mas a conta deles passa a contar
o valor de antes. Se o teste descreve o valor de hoje ("a garra de 65"),
atualize-o no mesmo commit.
