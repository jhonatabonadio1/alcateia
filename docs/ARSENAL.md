# Arsenal — Especificação Completa

Referência de balanceamento. Todos os valores são ponto de partida para
teste, não números finais.

**Onde os números moram:** cada arma de fogo tem o seu
`src/resources/weapons/<id>.tres`, editado à mão (os geradores
`gerar_arsenal.py` e `gerar_armas.py` saíram em 24/11). Mudou um número
lá, mude a ficha dele aqui e a linha no `docs/AJUSTES.md`. O modelo e a
primeira pessoa de cada arma saem de `src/tools/armas/reais.py`,
`conjuntos.py` e `peles.py` (as skins).

> **Nome na tela:** o WP deste documento aparece no jogo como **KP**
> (kill points), desde 24/11. As Presas viraram **Cash** e o Sangue,
> **IPs** (Inipoints). No código e no servidor os nomes antigos
> continuam (`WpCost`, `Currency.Presas`).

## Premissas do sistema

| | |
|---|---|
| Vida do humano | 100 HP na conta deste documento, sem armadura. No jogo são 200, e o dano das armas de fogo dobra (`CombatRules.FirearmDamageScale` 2): tiros para matar e TTK ficam os mesmos |
| Vida do lobo | a mesma do humano, mais o escudo da classe (`docs/AJUSTES.md`, seção 2) |
| Dispersão | graus de meio-ângulo do cone |
| Recoil | graus por tiro, aplicados à rotação da câmera |
| Intervalo entre tiros | `60 / RPM` |
| Falloff | dano cheio até a 1ª distância, decaindo linearmente até a 2ª |
| Ruído | raio do evento de audição emitido para a IA |
| Mult. movimento | multiplicador de dispersão enquanto anda |
| Mult. velocidade | velocidade de caminhada com a arma na mão (1.0 = base) |

Convenção de estrelas: **escala 0–5, estrela alta = melhor para o jogador**,
inclusive em recuo (5 = quase sem coice). Nunca misturar a convenção.

**O arsenal de hoje** (desde 19/11): AR-15, M4A4, AK-74, SCAR-H, SVD,
Desert Eagle, a faca e a granada, cada uma com as suas skins (épicas e
lendárias: a mesma arma, outra pintura, um pouco mais forte). O conjunto
padrão leva a **M4A4**, a **Desert Eagle**, a faca e a granada.

---

# FUZIS DE ASSALTO

## M4A4 — fuzil padrão (desde 19/11)
*Carabina rápida e fácil de segurar.* Pedido do Jhon: "a arma padrão
agora é a M4A4" (`Arsenal.DefaultPrimary`). Vem com o perfil e é a única
primária que entra no conjunto padrão (só arma comum vai nele). Um pouco
mais de cadência que o AR-15, um pouco menos de alcance.

```
Custo WP                200         (no conjunto padrão, 0)
Dano                    26          Headshot ×2.5 (65)
RPM                     700         (0.086s entre tiros)
Pente / Reserva         30 / 90
Tiros para matar        4 corpo / 2 cabeça
TTK                     0.26s

Reload tática           2.30s
Reload vazia            2.90s
Draw / Holster          0.50s / 0.50s

Dispersão base          0.16°
Dispersão por tiro      +0.09°      Máxima 1.9°
Recuperação             12.0°/s     (inicia 0.05s após o último tiro)
Mult. movimento         1.6×        Mult. agachado 0.7×
Na mira (zoom)          dispersão ×0.15

Recoil vertical         0.85° tiros 1-4, depois 0.55°
Recoil horizontal       ±0.14°
Recuperação de recoil   7.5°/s

Falloff                 100% até 30m → 70% a 55m
Mult. velocidade        1.00
Raio de ruído           45m
Modos de tiro           automático
```
Estrelas: Dano 2.0 · Pen 2.5 · Precisão 3.5 · Cadência 3.5 · Mob 2.5 · Recuo 3.0 · Alcance 2.5

Skins: **M4A4 Neon** (épica, 350 WP: 742 RPM, pente 32) e **M4A4 Dragão**
(lendária, 500 WP: dano 29, pente 36, recarga mais rápida).

## AR-15 — a referência (padrão de 15/11 a 19/11)
*Preciso no tiro a tiro, previsível na rajada. Não é o melhor em nada e
não é ruim em nada.* Modelo de TastyTony (com silenciador, mira red dot e
coronha retrátil), animações em primeira pessoa feitas pelo Jhon no
pacote da InfimaGames. Vem com o perfil, mas é incomum: não entra no
conjunto padrão (Jhon, 19/11: "somente arma comum pode").

O silenciador é só visual: o som e o raio de ruído para os bots são os
de um fuzil sem ele.

```
Custo WP                0
Dano                    27          Headshot ×2.5 (67.5)
RPM                     650         (0.092s entre tiros)
Pente / Reserva         30 / 90
Tiros para matar        4 corpo / 2 cabeça
TTK                     0.28s

Reload tática           2.30s
Reload vazia            2.90s
Draw / Holster          0.50s / 0.50s

Dispersão base          0.15°
Dispersão por tiro      +0.09°       Máxima 1.8°
Recuperação             12.0°/s      (inicia 0.05s após o último tiro)
Mult. movimento         1.6×         Mult. agachado 0.7×
Na mira (zoom)          dispersão ×0.2

Recoil vertical         1.00° tiros 1-4, depois 0.55°
Recoil horizontal       ±0.18° alternado
Recuperação de recoil   7.0°/s

Falloff                 100% até 35m → 70% a 60m
Mult. velocidade        1.00
Raio de ruído           45m
Modos de tiro           automático
```
Estrelas: Dano 2.0 · Pen 2.5 · Precisão 3.5 · Cadência 3.0 · Mob 2.5 · Recuo 2.5 · Alcance 3.0

Guardar leva **0,5 s** (`HolsterTime`, o tempo da animação do Jhon): ao
trocar do AR-15 para outra arma, a outra só sobe depois disso e atira
0,2 s depois de subir.

*14/11 (ainda como M16A2, de quem o AR-15 herdou os números): o Jhon
achou a arma padrão difícil de usar. Ficou mais precisa, com um pouco
menos de dano (30 → 27, ainda 4 no corpo e 2 na cabeça; contra lobo, 10% a
menos) e a mira mais fechada (×0.35 → ×0.2). O recuo ficou como estava.*

## AR-15 Ultimate — o AR-15 no limite (18/11)
*O mesmo fuzil, um degrau acima.* Está à venda na loja (raridade épica, patente 3
e Cash, como as outras armas de combate: nada de IPs). É o AR-15 com a pele
roxa de flames: o mesmo modelo, o mesmo rig e as mesmas animações do Jhon (o
`ar15_ultimate.glb` só troca a malha e a textura do fuzil e do carregador). "Um
pouco mais forte que a padrão", pedido do Jhon: números de ponto de partida, para
afinar no teste.

```
Custo WP                300
Dano                    30          Headshot ×2.5 (75)
RPM                     700         (0.086s entre tiros)
Pente / Reserva         35 / 105
Tiros para matar        4 corpo / 2 cabeça
TTK                     0.26s       (o AR-15: 0.28s)

Reload tática           2.30s       (a animação do AR-15)
Reload vazia            2.90s
Draw / Holster          0.50s / 0.50s

Dispersão base          0.13°
Dispersão por tiro      +0.08°      Máxima 1.6°
Recuperação             12.5°/s     (inicia 0.05s após o último tiro)
Mult. movimento         1.5×        Mult. agachado 0.7×
Na mira (zoom)          dispersão ×0.2

Recoil vertical         0.90° tiros 1-4, depois 0.50°
Recoil horizontal       ±0.16° alternado
Recuperação de recoil   7.5°/s

Falloff                 100% até 40m → 75% a 65m
Mult. velocidade        1.00
Raio de ruído           45m
Modos de tiro           automático
```
Estrelas: Dano 2.5 · Pen 2.5 · Precisão 3.8 · Cadência 3.3 · Mob 2.5 · Recuo 3.0 · Alcance 3.3

Contra o AR-15: +11% de dano (30, ainda 4 no corpo e 2 na cabeça), +8% de
cadência, pente 5 maior, dispersão e recuo cerca de 10% menores e alcance um
pouco maior. Não tem um eixo de troca novo: é o AR-15 melhor, e o preço está em
Cash e patente, nunca em vantagem permanente de quem pagou.

**A pele** (`src/tools/armas/blender/ar15_ultimate.py`): base roxa e índigo, flames
de lado nos dois lados da arma (silenciador, guarda-mão e caixa), quinas com
brilho e um padrão hexagonal discreto; as peças pequenas de metal claro viram
douradas. A cor está em `src/assets/armas/ar15_ultimate/T_AR15_Ultimate_D.png` e a
emissão (só as flames e as quinas) em `T_AR15_Ultimate_E.png`;
`WeaponModel.Material` liga a emissão sozinho quando acha o `_E.png` ao lado do
`_D.png`. O desenho das flames está em `FLAMES` no script: mexeu, rode de novo.

**As outras skins do AR-15** (19/11), cada uma com um jeito:

| Skin | Raridade | WP | Dano | RPM | Pente |
|---|---|---|---|---|---|
| AR-15 8 Bit | épica | 320 | 29 | 740 | 35 |
| AR-15 Lava | épica | 320 | 31 | 690 | 35 |
| AR-15 Ouro | lendária | 400 | 35 | 720 | 40 |
| AR-15 Diamante | lendária | 400 | 33 | 760 | 42 |

O resto de cada uma está no `.tres` dela.

## AK-74 — fuzil de impacto
*Melhor TTK do jogo. Feito para rajadas de 3 a 5, nunca para spray.* É a
ficha da AK-47 antiga, com o modelo novo (19/11).

```
Custo WP                380
Dano                    42          Headshot ×2.4 (100.8 → morte instantânea)
RPM                     500         (0.120s)
Pente / Reserva         30 / 90
Tiros para matar        3 corpo / 1 cabeça
TTK                     0.24s        ← o melhor do arsenal

Reload tática           2.50s
Reload vazia            3.30s
Draw / Holster          0.55s / 0.50s

Dispersão base          0.45°
Dispersão por tiro      +0.40°       Máxima 6.5°
Recuperação             7.0°/s
Mult. movimento         3.0×         ← pune andar e atirar
Mult. agachado          0.60×
Na mira (zoom)          dispersão ×0.3

Recoil vertical         1.80° tiros 1-5, depois 0.90°
Recoil horizontal       ±0.15° até o 6º tiro, ±0.70° imprevisível depois
Recuperação de recoil   5.5°/s

Falloff                 100% até 30m → 75% a 55m
Mult. velocidade        0.92
Raio de ruído           55m          ← denuncia mais que os outros
Modos de tiro           automático
```
Estrelas: Dano 4.0 · Pen 3.5 · Precisão 1.5 · Cadência 2.0 · Mob 2.0 · Recuo 1.0 · Alcance 2.5

Skins: **AK-74 Tigre** (épica, 530 WP: dano 44, coice menor) e **AK-74
Imperial** (lendária, 680 WP: dano 46, 525 RPM, pente 35).

---

# ARMA PESADA

## SCAR-H — fuzil de batalha 7,62
*Pente de 20, tiro lento e pesado. Segura um corredor de longe; de perto,
perde para qualquer fuzil.*

```
Custo WP                450
Dano                    48          Headshot ×2.3 (110 → morte instantânea)
RPM                     400         (0.150s)
Pente / Reserva         20 / 80
Tiros para matar        3 corpo / 1 cabeça
TTK                     0.30s

Reload tática           2.90s
Reload vazia            3.50s
Draw / Holster          0.60s / 0.55s

Dispersão base          0.30°
Dispersão por tiro      +0.35°       Máxima 5.0°
Recuperação             8.0°/s
Mult. movimento         2.6×         Mult. agachado 0.60×
Na mira (zoom)          dispersão ×0.25, recuo ×0.35

Recoil vertical         2.10° tiros 1-3, depois 1.20°
Recoil horizontal       ±0.20°
Recuperação de recoil   5.0°/s

Falloff                 100% até 40m → 80% a 70m
Mult. velocidade        0.88
Raio de ruído           60m
Modos de tiro           automático
```
Estrelas: Dano 4.5 · Pen 4.0 · Precisão 3.0 · Cadência 1.5 · Mob 1.5 · Recuo 1.5 · Alcance 3.5

Skins: **SCAR-H Ártico** (épica, 600 WP: recuo menor, pente 22) e **SCAR-H
Galáxia** (lendária, 750 WP: dano 52, 432 RPM, pente 25).

---

# SNIPER

## SVD Dragunov — fuzil de precisão
*Semiautomático com luneta. Dois no corpo ou um na cabeça; fora da mira,
a bala vai para qualquer lado.*

```
Custo WP                600
Dano                    85          Headshot ×2.4 (204 → morte instantânea)
RPM                     120         (0.50s entre tiros)
Pente / Reserva         10 / 30
Tiros para matar        2 corpo / 1 cabeça

Reload tática           3.00s
Reload vazia            3.50s
Draw / Holster          0.60s / 0.50s

Dispersão sem mira      4.00°        ← inutilizável no quadril
Na mira                 dispersão ×0.006 (0.02°)
Dispersão por tiro      +0.50°       Máxima 6.0°
Recuperação             9.0°/s
Mult. movimento         4.0×         Mult. agachado 0.7×
Recoil vertical         3.00°
Recoil horizontal       ±0.30°
Recuperação de recoil   5.0°/s

Zoom                    abertura 18° (fora da mira, 75°)
Falloff                 nenhum
Mult. velocidade        0.86
Raio de ruído           80m
Modos de tiro           semiautomático
```
Estrelas: Dano 4.5 · Pen 4.0 · Precisão 5.0 · Cadência 1.5 · Mob 1.5 · Recuo 1.5 · Alcance 5.0

Skins: **SVD Carbono** (épica, 750 WP: 130 RPM, recarga e saque mais
rápidos) e **SVD Tempestade** (lendária, 900 WP: dano 94, pente 12).

---

# SECUNDÁRIA

## Desert Eagle — pistola padrão
*Sete tiros que pesam: dois no corpo resolvem, mas o coice cobra cada um.*
A secundária de todo conjunto (`Arsenal.DefaultSecondary`). A pegada, o
saque e as recargas vêm da P9 (23/11; `docs/AJUSTES.md`, seção 7).
**Pendente:** a pistola padrão vai virar a Glock 17
(`docs/PENDENCIAS.md`).

```
Custo WP                0 (conjunto padrão)
Dano                    55          Headshot ×2.2 (121 → morte instantânea)
RPM                     240         Pente / Reserva  7 / 28
Reload                  1.80s tática / 2.20s vazia
Draw / Holster          0.40s / 0.30s
Dispersão base          0.45°       Por tiro +0.80°   Máx 6.0°   Recuperação 9.0°/s
Mult. movimento         1.5×        Mult. agachado 0.8×
Na mira (zoom)          dispersão ×0.6
Recoil vertical         1.60°       Horizontal ±0.20°   Recuperação 5.5°/s
Falloff                 100% até 25m → 70% a 45m
Stun em lobo            0.25s
Mult. velocidade        1.08        Raio de ruído  50m
Modos de tiro           semiautomático
```
Estrelas: Dano 4.0 · Pen 3.0 · Precisão 2.5 · Cadência 1.5 · Mob 4.5 · Recuo 1.5 · Alcance 2.5

Skins: **Desert Eagle Cobra** (épica, 120 WP: 259 RPM, coice menor) e
**Desert Eagle Fênix** (lendária, 240 WP: dano 61, pente 8, recarga mais
rápida).

---

# CORPO A CORPO

## Faca
*Silenciosa: não faz ruído para os bots.* Não tem `.tres`: os números
estão na arma (`Weapon.Knife.cs`).

```
Golpe rápido (clique esquerdo)   85 de dano, 2,2 m, um a cada 0,40s
Estocada (botão direito)         120 de frente, ×2 pelas costas (240)
                                 cone das costas 60°, dano em 0,2s, ocupa 0,8s
Mult. velocidade                 1.037 sobre o humano
Raio de ruído                    nenhum
```

A estocada (24/11) é o golpe lento que mata pelas costas: 240 derruba um
humano de vida cheia. A tecla F (olhar a faca) saiu junto. As versões
multiplicam a comum (`Arsenal.KnifeStats`): **Faca Damasco** (épica)
dano ×1,06 e golpe ×0,95; **Faca Obsidiana** (lendária) dano ×1,12, golpe
×0,9 e alcance ×1,05.

---

# UTILITÁRIO

## Granada
A única do jogo: a V40, com o id `choque` (de quando era de choque, em
18/11); explosiva desde 19/11 (`Grenade.Kind` = `Frag`). Os números
estão em `src/scripts/combat/Grenade.cs` e `Weapon.Grenade.cs` (tabela
no `docs/AJUSTES.md`, seção 8).

```
Custo WP        60          Quantidade  1
Dano central    420         Raio  9.0m   Dano decai linearmente do centro à borda
Fuse            1.2s, contado do primeiro toque no chão
Arremesso       cai onde a mira aponta (ThrowForce 17,5 m/s)
```

Com 420 no centro, mata quem tem 200 de vida a até 4,7 m. As versões
(Carbono, Neon, Magma, Ouro) só mudam a pintura, custam o mesmo e
qualquer uma vai no conjunto padrão.

---

# TABELA DE CUSTOS (WP)

| Item | WP | Categoria |
|---|---|---|
| Desert Eagle | 0 | secundária, padrão |
| AR-15 | 0 | fuzil |
| M4A4 | 200 (0 no padrão) | fuzil, padrão |
| AR-15 Ultimate | 300 | fuzil |
| AK-74 | 380 | fuzil |
| SCAR-H | 450 | pesada |
| SVD | 600 | sniper |
| Granada | 60 | utilitário |

As skins custam mais que a arma de base (tabelas acima). O conjunto
padrão custa sempre 0 (`LoadoutSet.TotalCost`).

## Economia

| Ação | WP |
|---|---|
| Selecionar o conjunto padrão | +50 |
| Abate | +100 |
| Abate com lobo | +70 |
| Assistência | +35 |
| Morrer | −100 |
| Teto acumulado | 10000 (era 2500; Jhon, 21/11) |

Transformar em lobo não custa (as classes básicas são de 0 WP). O
conjunto é a unidade de compra, não a arma. Até 4 conjuntos salvos; o
primeiro é gratuito e sempre disponível. O equipamento comprado some na
morte e precisa ser recomprado no respawn — o WP acumulado permanece,
menos os 100 da morte. Os números estão em `FormController` (seção 5 do
`docs/AJUSTES.md`).

---

# TESTES DE VALIDAÇÃO

Jogados em 13/11, com o arsenal de então, e aprovados (fecharam a
Fase 1):

1. **Humano × humano a 25m** — nenhuma arma deve vencer mais de 60% dos duelos.
2. **Lobo × humano em corredor** — o lobo deve vencer contra o fuzil de
   controle (a SG-552) e perder contra a escopeta (a M3).
3. **Lobo × humano em campo aberto** — o lobo deve perder contra tudo.
4. **Economia** — um jogador mediano deve acessar uma arma de ~350 WP por volta
   do terço inicial da partida, não antes.

Se o teste 3 falhar, a velocidade do lobo está alta demais.
Se o teste 2 falhar para os dois lados, o problema é o alcance da garra.
Se o teste 4 falhar para cima, o ganho por abate está alto e a partida vira
bola de neve.

---

# Histórico

Até 19/11 o arsenal era o da especificação original: M16A2, SG-552,
AK-47, UMP-45, M3, M9 (as que chegaram ao jogo), mais G36C, AUG, P90,
XM-26, Scout, M40, AWM, M500 e as granadas de luz, fumaça e
fragmentação (que só existiam no papel). Saíram todas a pedido do Jhon,
trocadas pelas armas de hoje; as fichas antigas estão no git.
