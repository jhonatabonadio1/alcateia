# Arsenal — Especificação Completa

Referência de balanceamento. Todos os valores são ponto de partida para
teste, não números finais.

## Premissas do sistema

| | |
|---|---|
| Vida do humano | 100 HP, sem armadura |
| Vida do lobo | 100 HP + escudo (50 / 150 / 300 conforme a forma) |
| Dispersão | graus de meio-ângulo do cone |
| Recoil | graus por tiro, aplicados à rotação da câmera |
| Intervalo entre tiros | `60 / RPM` |
| Falloff | dano cheio até a 1ª distância, decaindo linearmente até a 2ª |
| Ruído | raio do evento de audição emitido para a IA |
| Mult. movimento | multiplicador de dispersão enquanto anda |
| Mult. velocidade | velocidade de caminhada com a arma na mão (1.0 = base) |

Convenção de estrelas: **escala 0–5, estrela alta = melhor para o jogador**,
inclusive em recuo (5 = quase sem coice). Nunca misturar a convenção.

---

# FUZIS DE ASSALTO

## M16A2 — fuzil padrão
*Referência do arsenal. Nada excepcional, e esse é o ponto.*

```
Custo WP                0 (conjunto padrão)
Dano                    30          Headshot ×2.5 (75)
RPM                     650         (0.092s entre tiros)
Pente / Reserva         30 / 90
Tiros para matar        4 corpo / 2 cabeça
TTK                     0.28s

Reload tática           2.30s
Reload vazia            2.90s
Draw / Holster          0.50s / 0.35s

Dispersão base          0.35°
Dispersão por tiro      +0.22°       Máxima 4.0°
Recuperação             9.0°/s       (inicia 0.12s após o último tiro)
Mult. movimento         2.2×         Mult. agachado 0.7×

Recoil vertical         1.00° tiros 1-4, depois 0.55°
Recoil horizontal       ±0.18° alternado
Recuperação de recoil   7.0°/s

Falloff                 100% até 35m → 70% a 60m
Mult. velocidade        1.00
Raio de ruído           45m
Modos de tiro           automático
```
Estrelas: Dano 2.5 · Pen 2.5 · Precisão 2.5 · Cadência 3.0 · Mob 2.5 · Recuo 2.5 · Alcance 3.0

## SG-552 — fuzil de controle
*A única arma que não pune atirar em movimento. Padrão de recoil quase reto,
totalmente decorável.*

```
Custo WP                320
Dano                    26          Headshot ×2.5 (65)
RPM                     550         (0.109s)
Pente / Reserva         30 / 90
Tiros para matar        4 corpo / 2 cabeça
TTK                     0.33s        ← o pior do arsenal

Reload tática           2.60s
Reload vazia            3.20s
Draw / Holster          0.55s / 0.40s

Dispersão base          0.25°
Dispersão por tiro      +0.15°       Máxima 2.8°
Recuperação             11.0°/s
Mult. movimento         1.5×         ← a assinatura da arma
Mult. agachado          0.65×

Recoil vertical         0.65° constante (padrão linear)
Recoil horizontal       ±0.08°
Recuperação de recoil   9.0°/s

Falloff                 100% até 45m → 80% a 70m
Mult. velocidade        0.95
Raio de ruído           45m
Modos de tiro           automático, rajada de 3
```
Estrelas: Dano 2.0 · Pen 2.5 · Precisão 4.0 · Cadência 2.5 · Mob 2.5 · Recuo 4.0 · Alcance 3.5

**Fraqueza deliberada:** pior arma do arsenal contra lobo em espaço fechado.

## AK-47 — fuzil de impacto
*Melhor TTK do jogo. Feito para rajadas de 3 a 5, nunca para spray.*

```
Custo WP                380
Dano                    42          Headshot ×2.4 (100.8 → morte instantânea)
RPM                     500         (0.120s)
Pente / Reserva         30 / 90
Tiros para matar        3 corpo / 1 cabeça
TTK                     0.24s        ← o melhor do arsenal

Reload tática           2.50s
Reload vazia            3.30s
Draw / Holster          0.60s / 0.45s

Dispersão base          0.45°
Dispersão por tiro      +0.40°       Máxima 6.5°
Recuperação             7.0°/s
Mult. movimento         3.0×         ← pune andar e atirar
Mult. agachado          0.60×

Recoil vertical         1.80° tiros 1-5, depois 0.90°
Recoil horizontal       ±0.15° até o 6º tiro, ±0.70° imprevisível depois
Recuperação de recoil   5.5°/s

Falloff                 100% até 30m → 75% a 55m
Mult. velocidade        0.92
Raio de ruído           55m          ← denuncia mais que os outros
Modos de tiro           automático
```
Estrelas: Dano 4.0 · Pen 3.5 · Precisão 1.5 · Cadência 2.0 · Mob 2.0 · Recuo 1.0 · Alcance 2.5

## G36C — fuzil de curta distância
*Cadência de SMG com dano de fuzil. A escolha de quem joga agressivo.*

```
Custo WP                340
Dano                    27          Headshot ×2.3 (62)
RPM                     750         (0.080s)
Pente / Reserva         30 / 90
Tiros para matar        4 corpo / 2 cabeça
TTK                     0.24s

Reload tática           2.20s
Reload vazia            2.80s
Draw / Holster          0.42s / 0.32s

Dispersão base          0.50°
Dispersão por tiro      +0.30°       Máxima 5.0°
Recuperação             10.0°/s
Mult. movimento         2.4×         Mult. agachado 0.75×

Recoil vertical         0.90° tiros 1-4, depois 0.60°
Recoil horizontal       ±0.30°
Recuperação de recoil   8.0°/s

Falloff                 100% até 22m → 65% a 45m
Mult. velocidade        1.02
Raio de ruído           42m
Modos de tiro           automático, rajada de 2
```
Estrelas: Dano 2.5 · Pen 2.0 · Precisão 2.0 · Cadência 4.0 · Mob 3.5 · Recuo 3.0 · Alcance 1.5

## AUG — fuzil de precisão
*Semiautomático com mira 2×. A ponte entre fuzil e sniper.*

```
Custo WP                420
Dano                    34          Headshot ×2.6 (88)
RPM                     480         (0.125s)
Pente / Reserva         30 / 90
Tiros para matar        3 corpo / 2 cabeça
TTK                     0.25s

Reload tática           2.80s
Reload vazia            3.50s
Draw / Holster          0.65s / 0.45s

Dispersão base          0.20°        Com mira 0.05°
Dispersão por tiro      +0.35°       Máxima 4.5°
Recuperação             10.0°/s
Mult. movimento         3.0×         Mult. agachado 0.55×

Recoil vertical         1.20° constante
Recoil horizontal       ±0.10°
Recuperação de recoil   11.0°/s      ← volta rápido à mira

Zoom                    2.0× (tempo de entrada 0.25s)
Falloff                 100% até 55m → 85% a 80m
Mult. velocidade        0.90
Raio de ruído           50m
Modos de tiro           semiautomático
```
Estrelas: Dano 3.5 · Pen 3.0 · Precisão 4.5 · Cadência 1.5 · Mob 1.5 · Recuo 3.0 · Alcance 4.0

---

# SUBMETRALHADORAS

## UMP-45 — SMG de dano
*A arma anti-lobo. Existe por causa da mobilidade do inimigo, não da distância.*

```
Custo WP                280
Dano                    28          Headshot ×1.8 (50)
RPM                     750         (0.080s)
Pente / Reserva         25 / 100
Tiros para matar        4 corpo / 2 cabeça (perto)
TTK                     0.24s

Reload tática           2.10s
Reload vazia            2.70s
Draw / Holster          0.35s / 0.25s   ← o saque mais rápido

Dispersão base          0.55°
Dispersão por tiro      +0.18°       Máxima 3.5°
Recuperação             12.0°/s
Mult. movimento         1.3×         ← feita para atirar correndo
Mult. agachado          0.80×

Recoil vertical         0.50° constante
Recoil horizontal       ±0.25°
Recuperação de recoil   10.0°/s

Falloff                 100% até 15m → 60% a 30m (6 tiros para matar)
Mult. velocidade        1.08
Raio de ruído           35m
Modos de tiro           automático
```
Estrelas: Dano 2.5 · Pen 1.5 · Precisão 2.0 · Cadência 4.0 · Mob 4.5 · Recuo 3.0 · Alcance 1.5

**É a arma que você puxa quando ouve o uivo.**

## P90 — SMG de sustentação
*Pente de 50. Feita para aguentar uma investida inteira de lobo sem recarregar.*

```
Custo WP                330
Dano                    22          Headshot ×1.7 (37)
RPM                     900         (0.067s)
Pente / Reserva         50 / 100
Tiros para matar        5 corpo / 3 cabeça
TTK                     0.27s

Reload tática           3.00s
Reload vazia            3.60s        ← o preço do pente grande
Draw / Holster          0.40s / 0.28s

Dispersão base          0.60°
Dispersão por tiro      +0.14°       Máxima 3.2°
Recuperação             13.0°/s
Mult. movimento         1.2×         Mult. agachado 0.85×

Recoil vertical         0.35° constante
Recoil horizontal       ±0.30°
Recuperação de recoil   12.0°/s

Falloff                 100% até 12m → 55% a 28m
Mult. velocidade        1.06
Raio de ruído           33m
Modos de tiro           automático
```
Estrelas: Dano 1.5 · Pen 1.0 · Precisão 2.0 · Cadência 5.0 · Mob 4.5 · Recuo 4.0 · Alcance 1.0

---

# ESCOPETAS

## M3 — escopeta pump
*Mata lobo de escudo 50 com um tiro. O ciclo de 0.85s é a janela em que o
lobo te alcança.*

```
Custo WP                350
Pellets por tiro        9
Dano por pellet         13          (117 a queima-roupa)
Headshot                ×1.5 por pellet
RPM                     70          (0.85s de ciclo da bomba)
Pente / Reserva         8 / 32

Reload                  0.55s por cartucho — INTERROMPÍVEL
Entrada / saída reload  0.30s / 0.40s
Draw / Holster          0.60s / 0.40s

Cone fixo               2.5° a 0m → 9° a 20m (não cresce com tiros)
Mult. movimento         1.0×         ← movimento não afeta
Recoil vertical         3.50° por tiro
Recuperação de recoil   4.0°/s

Falloff por pellet      100% até 8m → 45% a 20m
Stun em lobo            0.40s de lentidão
Mult. velocidade        0.90
Raio de ruído           60m
Modos de tiro           pump
```
Estrelas: Dano 5.0 · Pen 1.0 · Precisão 1.0 · Cadência 0.5 · Mob 2.0 · Recuo 1.0 · Alcance 0.5

**A recarga interrompível é a mecânica central:** cartucho a cartucho,
cancelável a qualquer momento. Cria a decisão sob pressão de recarregar mais
um ou atirar com o que tem.

## XM-26 — escopeta automática
*Menos dano por tiro, muito mais tiros. O preço é o custo e a recarga única.*

```
Custo WP                480
Pellets por tiro        8
Dano por pellet         10          (80 a queima-roupa)
Headshot                ×1.4 por pellet
RPM                     180         (0.33s entre tiros)
Pente / Reserva         6 / 24

Reload (pente inteiro)  3.40s        ← não é cartucho a cartucho
Draw / Holster          0.65s / 0.45s

Cone fixo               3.5° a 0m → 12° a 18m
Mult. movimento         1.0×
Recoil vertical         2.20° por tiro
Recuperação de recoil   5.0°/s

Falloff por pellet      100% até 6m → 40% a 18m
Stun em lobo            0.30s de lentidão
Mult. velocidade        0.88
Raio de ruído           58m
Modos de tiro           semiautomático
```
Estrelas: Dano 3.5 · Pen 1.0 · Precisão 1.0 · Cadência 2.5 · Mob 1.5 · Recuo 1.5 · Alcance 0.5

Dois tiros rápidos (0.33s) matam lobo de escudo 150.

---

# SNIPERS

## Scout — sniper móvel
*Não mata no corpo. É a sniper que consegue fugir do lobo.*

```
Custo WP                450
Dano                    85          Headshot ×2.0 (170 → morte instantânea)
RPM                     60          (1.00s de ciclo do ferrolho)
Pente / Reserva         10 / 30
Tiros para matar        2 corpo / 1 cabeça

Reload                  2.80s
Draw / Holster          0.55s / 0.40s

Dispersão sem mira      8.00°        ← inutilizável no quadril
Dispersão com mira      0.04°
Mult. movimento         3.5×
Recoil vertical         2.50°
Recuperação de recoil   6.0°/s

Zoom                    4.0× (entrada 0.30s)
Falloff                 nenhum
Mult. velocidade        0.95         ← a mais móvel das snipers
Raio de ruído           70m
Modos de tiro           ferrolho
```
Estrelas: Dano 3.5 · Pen 3.0 · Precisão 5.0 · Cadência 1.5 · Mob 3.0 · Recuo 2.0 · Alcance 5.0

## M40 — sniper intermediária
*Mata no torso. O ciclo de 1.3s é o que impede o domínio total.*

```
Custo WP                620
Dano                    98          Headshot ×2.0 (196)
RPM                     46          (1.30s de ciclo)
Pente / Reserva         5 / 25
Tiros para matar        2 corpo (1 se houver qualquer dano prévio) / 1 cabeça

Reload                  3.40s
Draw / Holster          0.75s / 0.50s

Dispersão sem mira      10.00°
Dispersão com mira      0.02°
Mult. movimento         4.0×
Recoil vertical         3.50°
Recuperação de recoil   4.5°/s

Zoom                    6.0× (entrada 0.38s)
Falloff                 nenhum
Penetração              madeira e chapa fina
Mult. velocidade        0.85
Raio de ruído           80m
Modos de tiro           ferrolho
```
Estrelas: Dano 4.5 · Pen 4.0 · Precisão 5.0 · Cadência 1.0 · Mob 1.5 · Recuo 1.5 · Alcance 5.0

## AWM — sniper pesada
*Mata qualquer humano e o lobo fantasma com um tiro. Praticamente imóvel.*

```
Custo WP                850
Dano                    160         Headshot ×2.0 (320)
RPM                     37          (1.60s de ciclo)
Pente / Reserva         5 / 20
Tiros para matar        1 corpo (humano e lobo de escudo 50)

Reload                  4.00s
Draw / Holster          0.90s / 0.60s

Dispersão sem mira      12.00°
Dispersão com mira      0.01°
Mult. movimento         5.0×         ← andar torna a arma inútil
Recoil vertical         5.00°
Recuperação de recoil   3.5°/s

Zoom                    8.0× (entrada 0.45s)
Falloff                 nenhum
Penetração              madeira, chapa, concreto fino
Mult. velocidade        0.78
Raio de ruído           95m
Modos de tiro           ferrolho
```
Estrelas: Dano 5.0 · Pen 5.0 · Precisão 5.0 · Cadência 0.5 · Mob 0.5 · Recuo 1.0 · Alcance 5.0

---

# SECUNDÁRIAS

## M9 — pistola padrão
```
Custo WP                0 (conjunto padrão)
Dano                    25          Headshot ×2.4 (60)
RPM                     420         Pente / Reserva  15 / 60
Reload                  1.90s tática / 2.40s vazia
Draw / Holster          0.30s / 0.20s
Dispersão base          0.50°       Por tiro +0.30°   Máx 4.0°
Recoil vertical         1.20°       Recuperação 9.0°/s
Falloff                 100% até 18m → 60% a 35m
Mult. velocidade        1.12        Raio de ruído  30m
Modos de tiro           semiautomático
```
Estrelas: Dano 2.0 · Pen 1.5 · Precisão 2.5 · Cadência 3.0 · Mob 5.0 · Recuo 3.0 · Alcance 2.0

## Desert Eagle — pistola pesada
```
Custo WP                150
Dano                    55          Headshot ×2.2 (121 → morte instantânea)
RPM                     240         Pente / Reserva  7 / 28
Reload                  2.40s tática / 2.90s vazia
Draw / Holster          0.40s / 0.28s
Dispersão base          0.45°       Por tiro +0.80°   Máx 6.0°
Recoil vertical         2.80°       Recuperação 5.5°/s
Falloff                 100% até 25m → 70% a 45m
Stun em lobo            0.25s
Mult. velocidade        1.08        Raio de ruído  50m
Modos de tiro           semiautomático
```
Estrelas: Dano 4.0 · Pen 3.0 · Precisão 2.5 · Cadência 1.5 · Mob 4.5 · Recuo 1.5 · Alcance 2.5

## M500 — revólver
```
Custo WP                180
Dano                    85          Headshot ×2.0 (170)
RPM                     120         Pente / Reserva  5 / 20
Reload                  3.20s (tambor inteiro)
Draw / Holster          0.50s / 0.35s
Dispersão base          0.40°       Por tiro +1.20°   Máx 7.0°
Recoil vertical         4.50°       Recuperação 4.0°/s
                        (0.45s para voltar à mira — o 2º tiro é lento)
Falloff                 100% até 22m → 65% a 40m
Stun em lobo            0.50s
Mult. velocidade        1.05        Raio de ruído  65m
Modos de tiro           ação simples
```
Estrelas: Dano 5.0 · Pen 4.0 · Precisão 2.5 · Cadência 0.5 · Mob 4.0 · Recuo 0.5 · Alcance 2.5

---

# UTILITÁRIOS

## Granada de fragmentação
```
Custo WP        40          Quantidade  1
Dano central    110         Raio letal  3.0m   Raio total  6.0m
Fuse            3.0s        Dano decai linearmente do centro à borda
Arremesso       14 m/s      Cozinhar  permitido
Raio de ruído   100m
```

## Granada de luz
```
Custo WP        30          Quantidade  2
Cegueira        3.5s no centro, 1.2s na borda
Raio            8.0m        Requer linha de visão
Fuse            1.8s        Surdez parcial  2.0s
Efeito em lobo  cegueira ×1.4 (o lobo depende mais do movimento)
```

## Granada de fumaça
```
Custo WP        30          Quantidade  1
Duração         12.0s       Raio  8.0m   Tempo de expansão  1.5s
Bloqueia        visão humana e cone de visão da IA
```

## Granada de choque — anti-lobo
```
Custo WP        60          Quantidade  1
Dano            20
Lentidão        60% por 2.5s (apenas em lobo)
Raio            5.0m        Fuse  2.0s
Impede          grude em parede durante o efeito
```

A granada de choque é a resposta direta à mobilidade do lobo: não mata,
mas tira a vantagem que define a forma.

---

# TABELA DE CUSTOS (WP)

| Item | WP | Categoria |
|---|---|---|
| M16A2 | 0 | padrão |
| M9 | 0 | padrão |
| UMP-45 | 280 | SMG |
| SG-552 | 320 | fuzil |
| P90 | 330 | SMG |
| G36C | 340 | fuzil |
| M3 | 350 | escopeta |
| AK-47 | 380 | fuzil |
| AUG | 420 | fuzil |
| Scout | 450 | sniper |
| XM-26 | 480 | escopeta |
| M40 | 620 | sniper |
| AWM | 850 | sniper |
| Desert Eagle | 150 | secundária |
| M500 | 180 | secundária |
| Granada de luz | 30 | utilitário |
| Granada de fumaça | 30 | utilitário |
| Granada de fragmentação | 40 | utilitário |
| Granada de choque | 60 | utilitário |

## Economia

| Ação | WP |
|---|---|
| Selecionar conjunto padrão ou lobo | +50 |
| Abate | +100 |
| Abate com lobo | +70 |
| Assistência | +35 |
| Objetivo (plantar, desarmar, capturar) | +75 |
| Teto acumulado | 2500 |

O conjunto é a unidade de compra, não a arma. Até 4 conjuntos salvos; o
primeiro é gratuito e sempre disponível. O equipamento comprado some na
morte e precisa ser recomprado no respawn — o WP acumulado permanece.

---

# ORDEM DE IMPLEMENTAÇÃO

**Primeira leva (balanceamento do duelo):** M16A2, SG-552, AK-47, UMP-45, M3.
Cinco armas bastam para descobrir se humano × lobo funciona.

**Segunda leva (profundidade):** Scout, M500, granada de choque.
Mudam o jogo de forma profunda e vão bagunçar o balanceamento se entrarem cedo.

**Terceira leva (variedade):** G36C, AUG, P90, XM-26, M40, AWM, Desert Eagle.
Só depois que a base estiver estável.

---

# TESTES DE VALIDAÇÃO

1. **Humano × humano a 25m** — nenhuma arma deve vencer mais de 60% dos duelos.
2. **Lobo × humano em corredor** — o lobo deve vencer contra SG-552 e perder
   contra M3.
3. **Lobo × humano em campo aberto** — o lobo deve perder contra tudo.
4. **Economia** — um jogador mediano deve acessar uma arma de 350 WP por volta
   do terço inicial da partida, não antes.

Se o teste 3 falhar, a velocidade do lobo está alta demais.
Se o teste 2 falhar para os dois lados, o problema é o alcance da garra.
Se o teste 4 falhar para cima, o ganho por abate está alto e a partida vira
bola de neve.