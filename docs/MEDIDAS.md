# Medidas

Os números do jogo que a arte tem de respeitar. Um mapa é desenhado em
volta deles: a altura de um caixote, a largura de um beco, a distância
entre duas coberturas. Mudar um destes números depois que um mapa está
modelado é remodelar o mapa.

Para quem faz mapa, personagem ou objeto. Os detalhes de cada assunto
ficam nos outros documentos:

- `docs/MAPAS.md`: como um mapa é montado, as texturas, o céu, o que
  todo mapa precisa ter.
- `docs/PERSONAGENS.md`: como pôr um humano e um lobo novos, com altura
  e ossos certos.
- `docs/DESIGN.md`: por que o jogo é assim (corrida em parede, garras,
  salto, bot).

Os números vêm do código e da cena do jogador (`src/scenes/player.tscn`), e
os de movimento foram **medidos no jogo** pelo teste `Medidas`
(`src/tests/regression/MeasureTests.cs`). Se uma mudança de jogo mexer em
algum deles, o teste falha: ou a mudança volta, ou este documento e os
mapas mudam junto.

Valores de 16/11/2026. Gravidade 9,8 m/s². Unidade: metro.

---

## Travado e solto

**Travado (mexe na arte):** tudo neste documento: corpo, movimento,
pulo, alcances do lobo, corrida em parede, câmera.

**Solto (não mexe na arte):** dano, vida, munição, recargas (a do
salto está em 15 s, pode mudar à vontade), custo de WP, duração das
habilidades. É balanceamento: muda até o lançamento sem tocar num
modelo.

---

## 1. O corpo

| | Medida | De onde |
|---|---|---|
| Cápsula de colisão (humano e lobo) | 0,30 de raio, 1,75 de altura | `player.tscn`, `CapsuleShape3D` |
| Agachado | 1,30 de altura no humano, 1,00 no lobo | `Player.HumanCrouchHeight`, `Player.CrouchHeight` |
| Olho do humano | 1,59 do chão em pé, 1,09 agachado | os olhos do corpo visto (`Player.HumanEyeHeight`, afinados pelo `CharacterAnimator`); eram 1,475 e 0,60 até 26/11 |
| Olho do lobo (pivô da câmera) | ~1,48 do chão | centro (0,875) + `Head` (0,60) |
| Humano visível (topo da cabeça) | 1,79 | `CharacterSkin.HumanHeight` |
| Lobo visível (topo da cabeça) | 1,66 | `CharacterSkin.WolfHeight` |

O lobo usa a **mesma cápsula** do humano: o que ele é maior ou menor
na tela não muda por onde ele passa.

**O que isso pede do mapa:**

- Passagem em pé: **1,80 livre** no mínimo. Agachado: **1,35** para o humano (1,05 para o lobo).
- Vão entre duas paredes: **0,65** no mínimo para passar (0,60 da
  cápsula e uma folga). Para passar lutando, bem mais.
- A cabeça que leva tiro é a do modelo. Um personagem fora da altura
  muda o combate, por isso o teste `Personagens` reprova quem passar de
  4 cm da medida (veja `docs/PERSONAGENS.md`).

## 2. Andar e correr

Medidos no chão plano, segurando a tecla.

| | Humano | Lobo |
|---|---|---|
| Correndo (W) | 3,0 m/s (faca 3,84) | 4,33 m/s |
| Andando (Shift) | 1,26 m/s | — |
| Agachado | 1,35 m/s | 1,95 m/s |
| De quatro (W duas vezes) | — | 7,44 m/s |
| Carregando o salto | — | ~1,0 m/s |

Velocidade de base: `Player.HumanSpeed` 3,2 na cena; o humano anda isto
× `HumanSpeedBonus` 0,9375 (desde 26/11; era 1,08, 3,46 m/s). O lobo é um só
desde 26/11 (as classes Bruto e Ligeiro saíram; ele fica no meio delas):
multiplica por `src/resources/breeds/lobo.tres` (`SpeedMultiplier` 1,352) e,
de quatro, por `WolfSprintFactor` (1,719 na cena). Até 25/11: Bruto 4,15 /
7,14, Ligeiro 4,50 / 7,73.

**O lobo dura 30 s de cada vez** (e espera 20 s para virar de novo,
`FormController.WolfTimeLimit`/`WolfCooldown`, 26/11): um percurso de lobo
de mais de ~200 m (de quatro) não cabe numa transformação só.

**O que isso pede do mapa:** atravessar uma área aberta de 20 m leva
quase 6 s para o humano e menos de 3 s para o lobo de quatro. É o que
decide se um campo aberto é armadilha para o lobo ou para o humano.

## 3. Pulo e degrau

| | Humano | Lobo |
|---|---|---|
| O pulo parado sobe (pés) | 1,02 | 2,09 |
| Pulo correndo, do chão ao chão | 2,6 | 5,1 |
| Sobe andando, sem pular | 0,05 | 0,05 |
| Sobe pulando, colado na quina | 1,10 | 1,1* |

Até 25/11 o Bruto pulava 1,67 (4,4 correndo) e o Ligeiro 2,56 (5,9).

\* O lobo que pula contra uma parede segurando W **gruda nela** (corrida
em parede, seção 5) em vez de subir. Para subir em algo mais alto ele
pula e só então aperta W, ou ricocheteia entre paredes.

**O que isso pede do mapa:**

- **Não há degrau automático.** Qualquer borda de mais de 5 cm trava
  quem anda: meio-fio, soleira de porta, rodapé de caixote, emenda de
  piso. Desnível que se atravessa andando é **rampa**.
- O bot também não sobe degrau maior que 0,5 m: todo desnível que ele
  precisa vencer tem rampa (`docs/MAPAS.md`).
- Alturas de referência, que já estão nos mapas (`docs/MAPAS.md`):
  - **0,8**: caixote que o humano sobe pulando;
  - **2,2**: só o lobo sobe, partindo de um caixote de 0,8;
  - **5,0**: terraço, por rampa ou com o lobo ricocheteando num beco de
    3 m.
- Buraco que o humano pula correndo: até ~2,5 m (3,2 é o máximo, sem
  folga para erro). Maior que isso, só o lobo.

## 4. Alcances do lobo

| | Medida | De onde |
|---|---|---|
| Salto carregado (direito no chão) | de 1,5 a 12 conforme a carga | `PounceMinDistance`, `PounceMaxDistance` |
| Mirando o chão dentro do alcance | cai no ponto mirado | `Weapon.PounceAimPoint` |
| O salto passa por cima de | até 2,6 acima dos pés | `PounceMaxClear` |
| Arco mínimo do salto | 0,6 de altura | `PounceArc` |
| Bote (direito no ar) | 18 m/s, até 1 s, a partir de 0,35 de queda | `DiveSpeed`, `DiveMaxTime`, `DiveMinDrop` (cena) |
| Garrada | 1,5 à frente, 1,15 de raio | `ClawReach`, `ClawRadius` |

**O que isso pede do mapa:**

- Duas coberturas a menos de 8,4 m uma da outra: o lobo cruza de uma à
  outra num salto. Mais longe que isso, ele fica exposto no caminho.
- Contêiner de 2,2 a 2,6: o salto passa por cima. Bloco de 5 m ou muro:
  o salto para antes dele.
- Um lugar alto com chão embaixo a até 18 m é de onde o lobo dá o bote
  em quem não olha para cima.

## 5. Corrida em parede (só o lobo)

| | Medida | De onde |
|---|---|---|
| Gruda a partir de | 0,45 acima do chão | `WallGrabHeight` |
| Fica na altura em que grudou | sim, sem limite de tempo | `WallRunMaxTime` 0 |
| Face mínima para contornar a quina | 0,25 | `WallWrapMinFace` |
| O salto da parede sobe | 1,02 × o que entrou | `WallJumpClimb` |
| Procura parede até | 0,75 do centro | `WallCheckDistance` |

**O que isso pede do mapa:**

- Parede que o lobo usa precisa de **altura e comprimento**: ele corre
  enquanto ela existir e solta na ponta.
- Pilar, coluna ou caixote a partir de 0,25 de face: ele contorna. Uma
  chapa mais fina que isso, ele segue reto e cai.
- Beco de **3 m** entre duas paredes altas: o lobo sobe ricocheteando
  (é como ele chega ao terraço de 5 m).
- Parede com saliência ou janela no caminho interrompe a corrida. Se a
  ideia é o lobo passar ali, a face fica lisa.

## 6. A câmera do lobo

O lobo é em **terceira pessoa**: a câmera fica **2,5 m atrás** dele
(`Player.ThirdPersonDistance`), num braço que encolhe quando bate em
alguma coisa (`SpringArm3D`, 0,2 de folga).

**O que isso pede do mapa:** em corredor estreito ou de teto baixo, a
câmera cola na nuca do lobo e o jogador perde a visão. Caminho pensado
para o lobo tem pelo menos uns 2,5 m de folga atrás e acima dele.
(Recomendação, ainda não medida: vale conferir na fatia vertical.)

## 7. Convenções de mapa

- O time azul nasce em −X e olha para +X.
- Oito pontos de nascimento por time, sob cobertura.
- O céu de foto (`dia_claro`) gira com o sol: **giro = yaw do sol + 36**.
  Errado, o sol do céu fica do lado oposto ao da luz e as sombras
  parecem soltas do chão.

O resto, com os exemplos, está em `docs/MAPAS.md`.

## 8. Arte de personagem e de primeira pessoa

- **Personagens:** uma pasta por personagem em `src/assets/personagens/`,
  com humano de 1,79 e lobo de 1,66, e os ossos que as animações usam.
  Passo a passo em `docs/PERSONAGENS.md`.
- **Mãos de primeira pessoa:** ficam em `src/assets/armas/fp/maos/`.
  `maos.glb` é a malha dos dois braços, no esqueleto do AR-15 (o da
  Unreal), e `padrao.tres` é o material. A textura das mãos de todas as
  armas se troca ali, num lugar só. Arma nova com o mesmo esqueleto
  recebe as mãos sozinha.
- **Texturas:** 2048 px é o teto atual de personagem e mãos, em JPG.

## 9. Ainda em aberto

A definir na fatia vertical (o Hangar com arte final, um humano, um
lobo e os efeitos principais):

- orçamento de triângulos por personagem e por mapa;
- tamanho de textura de mapa;
- quanto pesa um mapa inteiro, medido no Mac e numa máquina fraca;
- a folga de câmera da seção 6, medida.
