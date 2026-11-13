# Guia de ajustes do Alcateia

Tudo o que dá para regular no jogo — altura, velocidade, golpes, armas,
granada, vida, WP, bots e partida —, com o valor atual, onde mudar e o
que acontece quando muda. Valores do repositório na v0.26.11.

> Mexeu em algum número daqui? Atualize a linha dele neste guia e, se
> for decisão de design, também no `CLAUDE.md`.

---

## Onde os valores moram

| Onde | Como mudar | Vale para |
|---|---|---|
| **Inspetor do Godot** — campos `[Export]` dos scripts | Abra a cena (`scenes/player.tscn` para você, `scenes/bot.tscn` para os bots), selecione o nó (`Player`, `Weapon`, `Health`, `FormController`, `WolfVisual`, `Human`) e mude no Inspetor | Só aquela cena. O valor do script é o padrão; o da cena, quando existe, ganha |
| **Recursos `.tres`** | Duplo clique no Godot, ou edite o texto | `resources/breeds/` (classes de lobo), `resources/weapons/` (armas de fogo) |
| **Código** | Edite o `.cs` | `WolfMoves.cs` (tempo dos golpes), `Difficulty.cs` (bots), `Catalog.cs` (tatuagens e preços), `Arsenal.cs` (granada, faca), `RoomConfig.cs` (sala padrão) |

Para mudar o padrão de todo mundo, mude no script; para mudar só o
jogador ou só os bots, mude na cena. Hoje as cenas só sobrescrevem
time, nome, classe de lobo, cena da granada, `Debug` da arma do bot e
o renascer do bot (6 s).

### Antes de mexer

- **O lobo é multiplicador do humano.** Velocidade e pulo do lobo são
  `HumanSpeed` e `JumpVelocity` vezes o `SpeedMultiplier` e o
  `JumpMultiplier` da classe. Mudou o humano, o lobo muda junto.
- **Os bots ignoram parte do `bot.tscn`.** A dificuldade
  (`Difficulty.cs`) sobrescreve visão, mira, reação, velocidade e
  outros ao entrar na partida. Para esses campos, mude em
  `Difficulty.cs` (tabela na seção [Bots](#9-bots-e-dificuldade)).
- **Animação presa à velocidade.** O clipe toca na velocidade do corpo
  dividida pela passada medida. Acima de ~1,6× a corrida do lobo começa
  a parecer apressada; o pé não patina, mas a perna gira rápido. As
  passadas (`WalkClipSpeed`, `RunClipSpeed`, `WallClipSpeed`,
  `WalkClip`, `RunClip`...) são medidas no esqueleto: não são botão de
  ajuste.
- **Alturas presas ao mapa.** O humano pula 0,99 m e sobe caixote
  (0,8 m), não contêiner (2,2 m). O Bruto precisa de `JumpMultiplier`
  ≥ 1,2 para chegar ao contêiner a partir do caixote. Mexeu no pulo,
  confira isso.
- **Sua cópia local do `bruto.tres`** está diferente do repositório:
  `AttackDamage` 75 (repo 150), `AttackRange` 5 (repo 3,8),
  `AttackCooldown` 0 (repo 1). Os demais campos que faltam nela usam o
  padrão do script, que é igual ao do repositório.

---

## 1. Movimento (humano e base do lobo)

`scripts/actors/Player.cs` → nó `Player` em `scenes/player.tscn`.

| Campo | Valor | O que faz |
|---|---|---|
| `HumanSpeed` | 3,0 m/s | Velocidade do humano correndo. **Base do lobo também.** É a passada do clipe de corrida: subir faz o pé patinar |
| `JumpVelocity` | 4,4 m/s | Impulso do pulo. Altura = v² / 19,6 → 0,99 m no humano |
| `GroundAccel` | 60 m/s² | Aceleração no chão (alto = resposta imediata) |
| `GroundFriction` | 50 m/s² | Freada ao soltar as teclas (de 3,9 m/s para em 0,08 s) |
| `AirAccel` | 18 m/s² | Controle de direção no ar |
| `AirFriction` | 7 | Arrasto no ar sem tecla: soltar W encurta o pulo |
| `HoldFallGravity` | 0,78 | Gravidade na queda segurando o pulo (menos que 1 = cai mais devagar) |
| `WolfJumpCarry` | 0,9 | Quanto da velocidade do chão o lobo leva para o pulo |
| `CrouchSpeedFactor` | 0,45 | Velocidade agachado (as duas formas) |
| `WalkSpeedFactor` | 0,42 | Velocidade andando com Shift (só humano) |
| `StandHeight` / `CrouchHeight` | 1,75 / 1,0 m | Altura da cápsula (é o hitbox do corpo) |
| `StandEyeHeight` / `CrouchEyeHeight` | 0,62 / 0,1 m | Altura do olho a partir do centro da cápsula |
| `CrouchLerpSpeed` | 22 | Rapidez de agachar e levantar |
| `FreezeBrake` | 30 m/s² | Freada de quem fica preso no lugar (golpe, carga do salto) |
| `FallLimit` | −25 m | Abaixo disto volta ao spawn |
| `MouseSensitivity` | 0,002 | Sensibilidade base. O jogador multiplica nas Opções (`Settings.Sensitivity`, padrão 1) |
| `ThirdPersonDistance` | 3,5 m | Distância da câmera do lobo |

### Resultado de hoje

| | Humano | Bruto | Ligeiro |
|---|---|---|---|
| Velocidade | 3,0 m/s | 3,89 (×1,298) | 4,22 (×1,406) |
| Agachado | 1,35 | 1,75 | 1,90 |
| Shift | 1,26 | — | — |
| Na parede | — | 4,05 | 4,38 |
| Altura do pulo | 0,99 m | 1,62 m (×1,28) | 2,50 m (×1,59) |
| Avanço do pulo correndo | ~2,7 m | ≈4,0 m | ≈5,4 m |

O avanço do lobo é estimativa (a última medida foi ~3,8 m com o Bruto
a 3,6 m/s).

Cada arma também multiplica a velocidade do humano com ela na mão
(`SpeedMultiplier` da arma, seção 7): M9 1,12, UMP 1,08, AK 0,92...

---

## 2. Classes de lobo

`resources/breeds/bruto.tres` e `ligeiro.tres` (script
`scripts/forms/WolfBreed.cs`). **Os bots usam sempre o Bruto.** O
jogador usa a classe escolhida no perfil, com as tatuagens aplicadas.

| Campo | Bruto | Ligeiro | O que faz |
|---|---|---|---|
| `SpeedMultiplier` | 1,298 | 1,406 | × `HumanSpeed` — velocidade no chão e (com `WallRunSpeedBonus`) na parede |
| `JumpMultiplier` | 1,28 | 1,59 | × `JumpVelocity` — altura do pulo |
| `Shield` | 200 | 140 | Escudo do lobo (a vida é a mesma do humano, 200) |
| `ShieldDecay` | 9 /s | 13 /s | Quanto escudo escorre por segundo |
| `BodyResistance` | 0,6 | 0,78 | Dano no corpo × isto (cabeça ignora). 0,6 = leva 60% |
| `AttackDamage` | 150 (local 75) | 55 | Base de **todos** os golpes (seção 4) |
| `AirAttackBonus` | 1,5 | 2,0 | × dano ao acertar caindo de ≥ 2,5 m |
| `AttackRange` | 3,8 (local 5) | 3,2 | Só os bots usam: distância em que o bot lobo decide atacar |
| `WpCost` | 0 | 0 | WP para transformar |
| `Hp` | 300 | 160 | **Não usado hoje** (vida é única) |
| `AttackCooldown` | 1,0 (local 0) | 0,35 | **Não usado hoje** (o ritmo vem da animação) |

### Tatuagens (`scripts/meta/Catalog.cs`)

Multiplicam a classe do jogador (não a dos bots):

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
| `WallRunSpeedBonus` | 1,0388 | Velocidade na parede = `HumanSpeed` × classe × isto |
| `WallAccel` | 18 m/s² | Aceleração ao grudar |
| `WallBrake` | 32 m/s² | Freada ao agarrar (botão de ataque na parede) |
| `WallRunMaxTime` | 0 | Tempo máximo na parede (0 = sem limite) |
| `WallRunVerticalDrift` | 0 m/s | Sobe/desce na parede (0 = altura travada) |
| `WallCheckDistance` | 0,75 m | Distância máxima até a parede para grudar |
| `WallHoldDistance` | 0,34 m | Distância do corpo à parede correndo |
| `WallStickForce` | 4 m/s | Força que segura o corpo na parede |
| `WallMinHeight` | 0,18 m | Altura mínima dos pés para a parede pegar |
| `WallGrabMaxRise` | 0,6 m/s | Subindo mais rápido que isto, ainda não gruda |
| `WallLeaveSpeed` | 0,5 m/s | Afastando-se mais rápido que isto, não gruda |
| `WallRunClearance` | 0,7 m | Checagem à frente: bater em algo solta |
| `WallEndMargin` | 0,1 m | Quanto passa da ponta antes de decidir a quina |
| `WallWrapMinFace` | 0,25 m | Face mínima para contornar a quina (mais fina: segue reto) |
| `CornerGrace` | 0,15 s | Tempo sem encostar em nada antes de soltar |
| `WallJumpClimb` | 1,02 | Altura do salto da parede (era 1,35; −40%) |
| `WallJumpPush` | 6,5 m/s | Empurrão para fora no salto da parede |
| `WallJumpKeepSpeed` | 0,8 | Quanto da velocidade da parede o salto leva |
| `SameWallThreshold` | 0,85 | Semelhança para contar como a mesma parede (não regruda nela sem tocar o chão) |
| `WallRunCooldown` | 0,12 s | Tempo mínimo entre duas corridas |

---

## 4. Golpes do lobo

Campos em `scripts/combat/weapon/` (`Weapon.WolfClaws.cs`, `.WolfPounce.cs`, `.WolfDive.cs`) → nó `Weapon`, grupos "Lobo: ..." no Inspetor. Tempos em
`scripts/forms/WolfMoves.cs`.

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

Ritmo (em `WolfMoves.Of`): garra e soco tocam a **2,2×** → cada golpe
dura 0,39 s, e o dano sai 0,14 s depois do clique. Para golpe mais
rápido ou mais pesado, mude só essa velocidade: duração e instante do
dano acompanham.

### Salto carregado (botão direito no chão)

| Campo | Valor | O que faz |
|---|---|---|
| `PounceChargeTime` | 1,0 s | Tempo segurando até a carga cheia |
| `PounceMinDistance` | 1,5 m | Distância sem carga |
| `PounceMaxDistance` | 12 m | Distância com carga cheia (em linha reta, sempre a mesma) |
| `PounceMinPower` | 0,4 | Fração do dano sem carga |
| `HeavyDamageBonus` | 2,1 | Multiplicador do dano com carga cheia |
| `PounceChargeHold` | 0,6 s | Segura cheio isso e salta sozinho |
| `PounceMaxClear` | 2,6 m | Altura que o salto passa por cima (contêiner passa, bloco não) |
| `PounceArc` | 0,6 m | Altura mínima do arco |
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

A aterrissagem prende o lobo 0,78 s (`WolfMoves.Land`, a 1,5×).

### Rugido (R)

Dura **2,0 s** (clipe a 2,3×, em `WolfMoves.Of`) e devolve o escudo
inteiro. Pode andar rugindo. `HowlNoise` 45 m (ouvido pelos bots) e
`Health.ShieldDecayGrace` 1 s (o escudo espera isso para voltar a
escorrer).

### Quanto dá hoje (contra humano, 200 de vida)

| Golpe | Bruto (repo 150) | Bruto (sua cópia, 75) | Ligeiro (55) |
|---|---|---|---|
| Garra 1º/2º | 240 | 120 | 88 |
| Garra 3º | 360 | 180 | 132 |
| Salto sem carga | 202 | 101 | 74 |
| Salto cheio | 504 | 252 | 185 |
| Bote | 720 | 360 | 264 |
| Bote caindo ≥ 2,5 m | 1080 | 540 | 528 |

Contra lobo, tire o ×1,6 e aplique a `BodyResistance` dele. Com o
valor do repositório, uma garra do Bruto mata um humano cheio — é o
que sua cópia local corrige.

---

## 5. Transformação e WP

`scripts/forms/FormController.cs` → nó `FormController`.

| Campo | Valor | O que faz |
|---|---|---|
| `HumanHp` | 200 | Vida (a mesma nas duas formas) |
| `MaxWp` | 2500 | Teto de WP |
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

`scripts/combat/Health.cs` → nó `Health`.

| Campo | Valor | O que faz |
|---|---|---|
| `MaxHp` | 200 | Vida máxima (o `FormController` define a pool) |
| `RespawnDelay` | 3 s (bots: 6 s na cena) | Espera para renascer |
| `SpawnProtection` | 3 s | Imortal ao renascer |
| `AutoRespawn` | sim (o jogador desliga) | Renascer sozinho |
| `ShieldDecayGrace` | 1 s | Carência do escudo depois do rugido |

Cabeça: `Weapon.HeadZone` 0,72 m acima do centro do corpo (a faca usa
`Weapon.HeadshotMultiplier` 2,5; as armas, o delas). Câmera de morte:
`LoadoutMenu.KillCamTime` 1,8 s, `KillCam.Hold` 0,35 s,
`KillCam.TurnRate` 5.

---

## 7. Armas de fogo

`resources/weapons/*.tres` (script `WeaponDefinition.cs`).

| Campo | M16A2 | AK-47 | SG-552 | UMP-45 | M3 | M9 |
|---|---|---|---|---|---|---|
| Classe | fuzil | fuzil | fuzil | SMG | escopeta | pistola |
| `WpCost` | 0 | 380 | 320 | 280 | 350 | 0 |
| `Damage` | 30 | 42 | 26 | 28 | 13 ×9 | 25 |
| `HeadshotMultiplier` | 2,5 | 2,4 | 2,5 | 1,8 | 1,5 | 2,4 |
| `Rpm` | 650 | 500 | 550 | 750 | 70 | 420 |
| `Mode` | Auto | Auto | Auto | Auto | Pump | Semi |
| `Magazine` | 30 | 30 | 30 | 25 | 8 | 15 |
| `ReloadTactical` / `Empty` (s) | 2,3 / 2,9 | 2,5 / 3,3 | 2,6 / 3,2 | 2,1 / 2,7 | 0,55 cartucho | 1,9 / 2,4 |
| `DrawTime` (s) | 0,5 | 0,6 | 0,55 | 0,35 | 0,6 | 0,3 |
| `SpreadBase` / `PerShot` / `Max` (°) | 0,35 / 0,22 / 4 | 0,45 / 0,4 / 6,5 | 0,25 / 0,15 / 2,8 | 0,55 / 0,18 / 3,5 | 4,5 / 0 / 4,5 | 0,5 / 0,3 / 4 |
| `SpreadRecovery` (°/s) | 9 | 7 | 11 | 12 | 99 | 9 |
| `MoveSpreadMultiplier` | 2,2 | 3,0 | 1,5 | 1,3 | 1,0 | 1,5 |
| `CrouchSpreadMultiplier` | 0,7 | 0,6 | 0,65 | 0,8 | 1,0 | 0,8 |
| `RecoilVertical` / `Late` | 1,0 / 0,55 | 1,8 / 0,9 | 0,65 | 0,5 | 3,5 | 1,2 |
| `RecoilHorizontal` | 0,18 | 0,15 (0,7 após 6 tiros) | 0,08 | 0,25 | 0,3 | 0,15 |
| `RecoilRecovery` | 7 | 5,5 | 9 | 10 | 4 | 9 |
| `Range` (m) | 150 | 150 | 150 | 100 | 40 | 100 |
| Queda de dano (m → fração) | 35–60 → 0,7 | 30–55 → 0,75 | 45–70 → 0,8 | 15–30 → 0,6 | 8–20 → 0,45 | 18–35 → 0,6 |
| `SpeedMultiplier` | 1,0 | 0,92 | 0,95 | 1,08 | 0,9 | 1,12 |
| `NoiseRadius` (m) | 45 | 55 | 45 | 35 | 60 | 30 |

Outros campos: `Pellets` (chumbos por tiro), `WolfStun` (M3: 0,4 s de
lentidão no lobo), `ShellByShell`/`ShellTime`/`ShellEnter` (recarga
cartucho a cartucho), `RecoilEarlyShots` (tiros antes do recuo
"Late"), `SpreadDelay` (espera para a dispersão voltar). `Reserve` e os
`Star*` são só exibição (a reserva é infinita; as estrelas são da
loja).

Ajustes globais do recuo, no `Player`: `RecoilScale` 1 (multiplica o de
toda arma), `RecoilMax` 4,5° (teto acumulado), `RecoilReturnDelay`
0,12 s. Traçante: `Weapon.ShowTracer`, `TracerLife` 0,08 s. Ruído do
tiro para os bots: `Weapon.ShotNoise` 60 m.

Preços da loja e patente mínima: `scripts/meta/Catalog.cs`.

---

## 8. Faca, granada e soltar arma

`scripts/combat/weapon/` (`Weapon.Knife.cs`, `.Grenade.cs`, `.Inventory.cs`) → nó `Weapon`, e `scripts/combat/Grenade.cs` (cena
`scenes/grenade.tscn`).

| Campo | Valor | O que faz |
|---|---|---|
| `KnifeDamage` / `KnifeRange` / `KnifeCooldown` | 85 / 2,2 m / 0,55 s | Faca |
| `ThrowForce` | 14,5 m/s | Força do arremesso na mira (era 11) |
| `ThrowArc` | 4 m/s | Empurrão para cima (era 3,5). Olhando reto cai a ~15,5 m |
| `ThrowCooldown` | 0,9 s | Entre uma granada e outra |
| Quantidade | 1 por espaço de utilitário | `Arsenal.cs` (`Count`) e o conjunto |
| `Grenade.Fuse` | 1,8 s | Tempo até explodir |
| `Grenade.Radius` | 6,5 m | Raio |
| `Grenade.MaxDamage` | 70 | Dano no centro (cai até a borda) |
| `Grenade.StunDuration` | 2,5 s | Atordoamento no centro |
| `Grenade.StunSlowFactor` | 0,3 | Velocidade de quem está atordoado |
| `DropThrow` / `DropLift` | 5,5 / 1,8 m/s | Arremesso da arma ao soltar (G) |

Segurar o botão arma o braço; soltar arremessa. A animação do braço
fica em `ViewModel.cs` (na tela) e `CharacterAnimator.cs` (no corpo).

---

## 9. Bots e dificuldade

**Estes valores vêm de `scripts/core/Difficulty.cs`** e sobrescrevem o
`bot.tscn`:

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
| `WolfSpecialRate` | 0,25 | 0,5 | 0,75 | Frequência dos especiais do lobo |
| `PounceError` | 25% | 12% | 6% | Erro da carga do salto |
| `PatrolSpeedFactor` / `PatrolPause` | 0,45 / 2,2 | 0,55 / 1,5 | 0,7 / 0,9 | Patrulha |

Ficam no `bot.tscn` (a dificuldade não mexe): `MissSpread` 14°,
`TargetStickiness` 6 m, `RetargetInterval` 0,5 s, `DecisionInterval`
0,4 s, `Vigilance` 7 s, `MinRange` 4 m, `WolfHealthThreshold` 0,4 e
`WolfRange` 14 m (quando vira lobo), `HumanRange` 20 m,
`CoverSamples` 12, `CoverSearchRadius` 11 m, `CoverMinDistance` 6 m,
`NoCoverTime` 3 s, `ArriveRadius` 2,5 m, `ScanTime` 2,5 s,
`PatrolRadius` 16 m, `StuckTime` 1,2 s, `JumpMaxHeight` 1,1 m,
`CuriositySpread` 0,35.

O cérebro (as notas de cada ação) está em `scripts/ai/BotBrain.cs`.

---

## 10. Partida e sala

| Onde | Campo | Padrão |
|---|---|---|
| `Match.cs` | `ScoreToWin` | 40 abates (a sala sobrescreve) |
| `Match.cs` | `Duration` | 600 s (a sala sobrescreve) |
| `Match.cs` | `CountdownTime` | 10 s |
| `LoadoutMenu.cs` | `ChooseTime` | 5 s para escolher o conjunto ao renascer |
| `RoomConfig.cs` | `RoomSettings` | Equipe, 8 por time, Hangar, 40 pontos, 10 min, todas as armas, transformação livre, bots no Médio |

Na tela da sala: mapa, pontos, minutos, armas (todas, sem pesadas ou
só pistolas), transformação (livre ou bloqueada), fogo amigo, troca de
lado, espectadores e dificuldade dos bots. Modo, por enquanto, só
Equipe; os outros existem em `RoomConfig.cs` mas estão desligados.

---

## 11. Ruído e som

Raio em metros em que os bots ouvem. Um arquivo só para o jogador e
para o bot: `resources/body/ruido.tres` (classe `BodyNoiseProfile`).
Abra no Inspector e mude ali — vale para os dois.

| Campo | Valor |
|---|---|
| `StepHuman` | 13 |
| `StepWolf` | 20 |
| `Land` | 17 |
| `Howl` | 45 (já no começo do rugido e a cada 0,5 s) |
| `StepDistance` | 2,2 m entre passos |
| `NoiseRadius` da arma | 30 a 60 (seção 7) |

Agachado e com Shift não fazem ruído; a faca é silenciosa. Teste:
`Ruido`.
`WorldAudio.ShotReach` 120 m (até onde você ouve o tiro de outro),
`DistantShot` 35 m (troca para o som distante), `OwnStepVolume` −3 dB.

---

## 12. Animação e visual

Mexa com cuidado: muitos destes são medidas.

**Lobo** (`scripts/ui/WolfVisual.cs`):

| Campo | Valor | O que faz |
|---|---|---|
| `ModelScale` | 1,1 | Tamanho do Mutant (o passo dos golpes acompanha) |
| `WalkSpeed` / `RunSpeed` | 2,0 / 2,6 m/s | Abaixo só anda, acima só corre |
| `StartBlend` / `StopBlend` | 0,12 / 0,22 s | Sair do parado e voltar a ele |
| `TurnRate` | 540°/s | Virar para o lado em que anda |
| `AimTurnRate` | 1400°/s | Voltar para a mira ao atacar |
| `CrawlLift` | 0,1 m | Altura extra engatinhando |
| `WallTurnTime` | 0,12 s | Deitar na parede |
| `DiveLeanMax` | 40° | Inclinação no bote |
| `TeamTint` | 0,3 | Cor do time na pele |

**Humano** (`scripts/visual/CharacterAnimator.cs`): `CrouchAim` 1
(fuzil no ombro agachado), `BlendSmoothing` 9, `HitCooldown` 1,6 s,
`CorpseHold` / `CorpseFade` 1,2 s, `TintByTeam`, `BlueBody`,
`OrangeBody`.

**Arma na tela** (`scripts/ui/ViewModel.cs`): `Kick` 0,08 m,
`BobAmount` 0,015, `BobSpeed` 9, `FlashLife` 0,045 s.

**Mira** (`scripts/ui/Crosshair.cs`): `PixelsPerDegree` 9, `MinGap`
5, `Length` 8, `Thickness` 2.

---

## 13. Diagnóstico (desligar para versão final)

Imprimem no Output: `Player.WallDebug`, `Weapon.Debug` (desligado no
bot), `Match.Debug`, `CharacterAnimator.Debug`, `WolfVisual.Debug`,
`Bot.Debug`, `WorldAudio.Debug`.
