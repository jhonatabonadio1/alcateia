# Alcateia — guia para agentes

FPS multiplayer com transformação humano ↔ lobo, em Godot 4 + C#.
Sucessor espiritual do WolfTeam: mecânica copiada livremente, nome,
arte e nomes de classe próprios.

Leia este arquivo inteiro antes de mexer em qualquer coisa, e a seção
do `docs/DESIGN.md` do sistema que for mexer.

---

> **Onde está cada coisa:**
>
> | Arquivo | O quê |
> |---|---|
> | este guia | regras que não se violam, arquitetura, convenções, testes, estado |
> | `docs/DESIGN.md` | as decisões de jogo, sistema por sistema, e o porquê de cada uma |
> | `docs/ARQUITETURA.md` | como o código está montado e onde mexer em cada coisa |
> | `docs/AJUSTES.md` | todo número regulável, com o valor atual e onde mudar |
> | `docs/MAPAS.md` | os mapas, como são gerados e como fazer um novo |
> | `docs/decisoes/` | as decisões de arquitetura (ADR) |
> | `docs/ARSENAL.md`, `docs/CONJUNTOS.md`, `docs/UI.md` | especificações de armas, economia e interface |
> | `docs/CHANGELOG.md` | o que entrou em cada versão |
> | `CONTRIBUTING.md` | o passo a passo para trabalhar no projeto |
>
> **Mudou um número? Atualize a linha dele no `AJUSTES.md`.** Mudou uma
> decisão de jogo? No `DESIGN.md`. Vai mexer num sistema? Leia a seção
> dele no `DESIGN.md` antes.

## Regra zero

**A Fase 1 está fechada** (decisão do Jhon, v0.28.0): a mecânica local
foi validada. Agora o trabalho é deixar o jogo profissional — mapas,
arte, som, interface — e pôr o multiplayer de pé (Fase 2).

O que continua valendo da regra antiga: **não misture rede no que é
regra de jogo**. `Health`, `Weapon` e `FormController` não sabem de
rede; o servidor roda o mesmo código sobre a intenção do jogador
(`CharacterIntent`) e a semente da partida (`GameRandom`).

---

## O que o jogo é

Um FPS com transformação humano ↔ lobo, onde a escolha de forma é a
decisão central de cada momento.

**Decisão do autor (nov/2026): as classes básicas custam 0 WP.** O
jogador transforma quando quiser. O WP continua existindo no sistema e
é o que vai separar as classes caras — Couraçado, Espectro e as demais
mutações devem custar, criando a economia que o básico não tem.

Risco conhecido e aceito: sem custo, o lobo domina. Ele é mais rápido,
tem mais vida, escudo, corre em parede e mata em dois golpes. Se nos
testes ninguém mais quiser ficar humano, as alavancas para corrigir
são, nesta ordem: custo de WP no básico, buff nas armas humanas, e
granadas mais fortes.

---

## Regras de design — não viole sem conversar

1. **Um eixo de troca por classe.** Não dois, não três. O Bruto troca
   mobilidade por dano; o Ligeiro troca dano por mobilidade. Resista a
   dar "mais uma habilidadezinha" para uma classe.

2. **Toda habilidade forte precisa de uma condição onde não serve.**
   O Smart Wolf original era quase impossível de acertar, mas tinha
   dano baixo e perdia para outros lobos.

3. **Nenhuma vantagem permanente entre jogadores.** Toda assimetria é
   escolha dentro da partida, nunca acúmulo fora dela.

4. **Mesma gramática de controle em todas as formas.** Clique esquerdo
   ataca, sempre. Controles que mudam por forma destroem a memória
   muscular.

5. **Transformar é um slot, não um botão separado.** Ocupa a tecla 3,
   junto das armas. Apertar 1, 2 ou 4 volta para humano.

6. **Os estados de transição são estados reais**, com hitbox e regras
   próprias — nunca "animação tocando". Hitbox ambígua gera dano
   fantasma, o pior bug de rastrear quando a rede entrar.

7. **Sensação acima de conteúdo.** Diante da escolha entre mais
   classes e melhor feel, escolha feel. Ninguém lembra do oitavo lobo;
   todos lembram de pular de uma parede em cima de alguém.

8. **Câmera: humano em primeira pessoa, lobo em terceira.** A câmera
   não inclina na corrida em parede.

9. **O humano é deliberadamente lento e pesado.** Velocidade 3,24 m/s
   e pulo de 0,99 m. **As duas formas andam o que as pernas andam**
   (decisão do Jhon): a velocidade de cada uma é a passada do próprio
   clipe de corrida, sem acelerar a animação do humano. A mobilidade do
   jogo pertence ao lobo — 20% mais rápido a pé, e o pulo, a parede, o
   salto e o bote. O humano compra distância e tempo com arma e
   granada, não com as pernas.

   Atenção ao mexer: a velocidade e o pulo do lobo são
   **multiplicadores** dos valores humanos. Baixar o humano encolhe o
   lobo junto e o contraste entre as formas não muda. Para separar as
   duas, mexa no `SpeedMultiplier` das classes, não só aqui.

   Valores absolutos atuais (velocidade em m/s, pulo em altura):

   | | Humano | Bruto | Ligeiro |
   |---|---|---|---|
   | Velocidade | 3,24 (faca 3,36) | 3,89 (1,298×) | 4,22 (1,406×) |
   | Pulo | 0,99 m | 1,62 m (1,28×) | 2,50 m (1,59×) |
   | Avanço do pulo correndo | ~2,7 m | ~3,9 m | ~5,0 m |
   | Agachado | 1,46 | 1,75 | 1,90 |
   | De quatro (W W) | — | 5,06 | 5,48 |
   | Na parede | — | 4,45 | 4,82 |

   **A velocidade do humano é a passada do X Bot**, medida no quadril:
   correr ("run_fwd") anda 3,0 m/s a 1×, andar 1,0, de costas 1,1 e
   2,6, de lado ~1,0. O ponto da mistura sai da velocidade por esses
   números, trecho a trecho (`CharacterAnimator.WalkClip`, `RunClip`...),
   e o clipe nunca acelera: correndo para a frente o pé apoiado fica
   parado no chão (0,2 m/s medidos), de costas quase (0,6). Era 4,2 m/s,
   com a caminhada em 2,1 — o pé patinava o dobro. **De lado ainda
   patina** (~2 m/s): só há strafe andando, e o corpo vai a 3,0. Resolve
   com um clipe de corrida lateral, não com número. O strafe tocava
   **ao contrário** (`InvertStrafe` agora marcado): o pé andava junto
   com o corpo, 4 m/s de patinada.

   **A velocidade do lobo é a da passada do Mutant.** A corrida do
   clipe anda 2,42 m/s na escala do jogo; mais rápido que isso ela
   precisa tocar acelerada para o pé não patinar, e a 2,4× (o Bruto a
   5,7 m/s) ficava corrida. A 3,89 m/s (decisões do Jhon: +5% e depois
   +3% sobre os 3,6 de antes) toca a 1,61×. **Na parede o lobo vai 14%
   além disso** (`WallRunSpeedBonus` 1,1427 = 1,07 / 1,03 × 1,10;
   decisões do Jhon): 4,45 m/s no Bruto, e o engatinhar da parede toca
   a 1,94× — perto do limite de parecer corrido. O salto da parede
   leva 80% da velocidade dela, então sobe junto.
   Para o lobo correr mais sem ficar corrido, é preciso um clipe de
   corrida com passada maior, não só o número.

   O lobo estava rápido demais (o Bruto a 6,5 m/s, 8,1 na parede): ser
   lobo ficava vantajoso demais. **O pulo do lobo é para cima, não para
   a frente**: o pulo do chão leva 90% do passo (`WolfJumpCarry`), e o
   ar não acelera além disso até tocar o chão ou grudar numa parede —
   uns 3,8 m de avanço, o que o Jhon aprovou. Com o Bruto a 5,7 m/s e o
   passo inteiro ele planava 6 m parado, e ficava estranho. O salto da parede não entra:
   conserva o avanço da corrida. A altura está amarrada ao mapa (veja
   "Alturas amarradas à física").

10. **O alcance do pulo é controlado pela tecla.** Segurar para frente
	mantém o avanço até o fim; soltar encurta, via um arrasto suave no
	ar (`AirFriction`). Não existe distância máxima fixa — quem decide
	é o jogador, durante o salto. Os dois extremos são erros: zerar a
	velocidade trava o corpo no ar, e conservar tudo tira a escolha.
	O controle de direção no ar é parcial (`AirAccel`).

11. **Agachar vale nas duas formas.** Encolhe a cápsula, baixa o olho
	e corta a velocidade para 45%. Não se agacha na parede — a corrida
	já é um estado de corpo inteiro. **O corpo visto agacha junto**: o
	humano ajoelha com o fuzil no ombro (da cintura para cima, a pose
	de mira do parado em pé, `CharacterAnimator.CrouchAim` — agacha e
	atira, decisão do Jhon; os clipes agachados seguravam a arma na
	cintura), e o lobo encolhe parado e engatinha andando (veja "Clipes
	do lobo").

---

## Propriedade intelectual

Mecânica de jogo não é protegida. Copie à vontade: transformação com
custo de recurso, uivo que recarrega escudo, corrida em parede,
ataque aéreo, modos restritos por espécie.

**Nunca use:** o nome WolfTeam, logo ou variação próxima; modelos,
texturas, sons ou mapas extraídos do cliente original; os nomes das
mutações (Power, Ghost, Smart, Guardian, Berserker, Ice, Psycho).

Nomes das classes aqui são inventados: Bruto, Ligeiro, e depois
Couraçado, Espectro, Detonador, Congelante, Arcano.

**Licenças de assets de terceiros.** Todo som tem a origem em
`assets/audio/CREDITOS.md`. A maior parte é CC0; o Oculus Audio Pack é
**CC BY 4.0** e exige crédito visível no jogo publicado. Asset novo só
entra com licença que permita uso comercial, e com a linha dele nos
créditos no mesmo commit.

Personagens e animações vêm do **Mixamo** (Adobe): uso livre em jogo,
inclusive comercial, mas **não** se redistribui o arquivo cru como
asset. Se o repositório um dia for público, os `.fbx` do Mixamo
precisam sair dele.

As fontes Barlow e Barlow Condensed (`assets/fonts/`) são **SIL OFL
1.1**: uso comercial livre, e a licença vai junto (`OFL.txt`). Não
venda a fonte sozinha nem distribua com outro nome.

---

## Arquitetura

```
alcateia/
├── CLAUDE.md           este guia (fica na raiz, é onde agentes olham)
├── AGENTS.md           atalho para o mesmo arquivo
├── project.godot
├── CONTRIBUTING.md     o passo a passo para trabalhar no projeto
├── docs/               DESIGN, ARQUITETURA, AJUSTES, UI, ARSENAL, CONJUNTOS, CHANGELOG, decisoes/ (ADR)
├── tests/              unit (C# puro) e regression (o jogo sem tela)
├── assets/             audio, characters, fonts, maps, textures, ui (miniaturas de mapa)
├── resources/
│   ├── breeds/         estatísticas das classes de lobo
│   ├── weapons/        uma definição por arma (gerada por tools/gerar_arsenal.py)
│   ├── bots/           níveis de dificuldade (facil, medio, dificil)
│   └── body/           ruido.tres — quanto barulho o corpo faz
├── scenes/
│   ├── menu/menu.tscn  o lobby inteiro (LobbyShell)
│   ├── main.tscn       a partida
│   ├── treino.tscn     campo de treino, sem partida
│   ├── maps/           hangar.tscn (gerado por tools/gerar_hangar.py)
│   └── player, bot, dummy, grenade
├── tools/              geradores, e testar.sh (os testes)
└── scripts/
	├── domain/         regras em C# puro (combate, sorteio), com teste de unidade
	├── actors/         player/, bot/ — quem decide; body/ — o corpo que os dois
	│                   dividem; input/ — a intenção do jogador
	├── ai/             o cérebro do bot e os níveis de dificuldade
	├── combat/         Health, weapon/ (Weapon.*), Grenade, Arsenal, LoadoutSet
	├── forms/          FormController (transformação e WP), WolfBreed
	├── core/           partida, regras da sala, perfil, opções, som, input
	├── meta/           o que vive fora da partida: patentes, catálogo,
	│                   missões, progressão, mapas, lista de salas
	├── lobby/          as telas do lobby
	├── ui/             hud/ (HUD por canto), placar, resultado, respawn, Esc
	│   └── kit/        tokens, tema e componentes de interface
	├── visual/         human/ e wolf/ (os corpos animados), anim/ (AnimationKit), áudio
	├── net/            dormente até a Fase 2
	└── debug/          peças descartáveis
```

O mapa completo, arquivo por arquivo, e o caminho de cada dado da
sala até a partida estão em `docs/ARQUITETURA.md`.

| Arquivo | Responsabilidade |
|---|---|
| `actors/player/Player.cs` | O ciclo do jogador: nascer, o passo de física, morrer e renascer |
| `actors/player/Player.Movement.cs` | Andar, pular, agachar, Shift, atordoamento |
| `actors/player/Player.WallRun.cs` | Corrida em parede do lobo (grudar, quinas, agarrar, saltar da parede) |
| `actors/player/Player.Camera.cs` | Olhar com o mouse, câmeras de 1ª e 3ª pessoa, recuo, câmera da morte |
| `actors/player/Player.Actions.cs` | Trocar arma, atacar, salto e bote, recarregar, soltar, rugir |
| `actors/input/CharacterIntent.cs` | O que o jogador quer no quadro, lido uma vez do teclado (a costura para o multiplayer) |
| `actors/bot/Bot.cs` | O ciclo do bot, o estado (Idle/Investigate/Combat/Search) e o cérebro |
| `actors/bot/Bot.Perception.cs` | Visão, audição, crença de onde o alvo está, escolha de alvo, reação |
| `actors/bot/Bot.Combat.cs` | Rajadas, strafe, distância, mira que erra de propósito |
| `actors/bot/Bot.Wolf.cs` | Quando virar lobo, salto carregado, pulo com bote, rugido |
| `actors/bot/Bot.Navigation.cs` | Malha de navegação, desvio, pular obstáculo, virar o corpo |
| `actors/bot/Bot.Cover.cs` | Achar cobertura testando a linha de tiro, recuar |
| `actors/bot/Bot.Patrol.cs` | Ronda pelas rotas do mapa e busca onde o alvo sumiu |
| `actors/body/` | O corpo que o jogador e o bot dividem: `WolfMoveMotor` (bote, voo do salto, preso ao chão), `BodyNoise` + `BodyNoiseProfile` (passo, baque, rugido; `resources/body/ruido.tres`), `Corpse` (cair e assentar morto), `BodyMotion.Hold` |
| `combat/Health.cs` | HP, escudo, dano, morte, respawn |
| `combat/weapon/Weapon*.cs` | A arma na mão, dividida por responsabilidade (classe parcial): `Weapon.cs` (slots, troca, roteamento do ataque), `.Inventory` (equipar, munição, recarga, soltar e pegar), `.Firearms` (tiro, dispersão, recuo), `.Knife`, `.Grenade`, `.WolfClaws` (garras e o relógio dos golpes), `.WolfPounce` (salto carregado), `.WolfDive` (bote), `.Damage` (aplicar dano) |
| `forms/FormController.cs` | Máquina de estados da transformação + economia de WP |
| `domain/CombatRules.cs` | As contas do combate em C# puro (dano, queda, dispersão, recuo, golpes do lobo, salto, vida e escudo), com teste de unidade |
| `ai/BotDifficulty.cs` | Um nível de dificuldade como dado (`resources/bots/*.tres`) |
| `visual/human/CharacterAnimator*.cs` | O corpo humano animado: `.Model` e `.Tree` montam uma vez (clipes, modelo do time, árvore); `.Locomotion`, `.Actions` (recarga, granada, reação) e `.Death` tocam por quadro |
| `visual/wolf/WolfVisual*.cs` | O corpo do lobo: `.Model` e `.Tree` montam; `.Locomotion`, `.Air` (parede, pulo, bote) e `.Moves` (golpes, rugido) tocam |
| `visual/anim/AnimationKit.cs` | O que os dois corpos e o lobby dividem: clipe do Mixamo, raiz parada, quadril, esqueleto, filtro do tronco, transições |
| `core/Match.cs` | Fases da partida, placar, MVP, pagamento de XP e Presas no fim |
| `core/Saida.cs` | Autoload: esvazia o coletor do C# na saída, para o jogo fechar sem abortar |
| `core/Profile.cs` | O perfil salvo: nome, patente, XP, moedas, inventário, conjuntos, missões |
| `core/RoomConfig.cs` | A sala que vai para a partida e suas regras |
| `meta/*.cs` | Patentes, catálogo da loja, missões, progressão, mapas, salas |
| `lobby/LobbyShell.cs` | Moldura do lobby: header fixo, miolo trocando |
| `ui/kit/*.cs` | Tokens, tema e componentes — toda tela usa estes |
| `ui/hud/Hud*.cs` | HUD de combate, um arquivo por canto: `.Layout` monta; `.Vitals`, `.Equipment`, `.Form`, `.Feed` reagem aos sinais |
| `core/InputSetup.cs` | Registra ações de input sem passar pelo editor |
| `net/GameManager.cs` | Rede — dormente até a Fase 2 |

### Por que `Health`, `Weapon` e `FormController` não ficam em `actors/player/`

Eles **não pertencem ao jogador**: o
bot usa exatamente os mesmos componentes, e é isso que faz o
balanceamento medido contra o bot valer contra uma pessoa. Agrupar por
dono criaria a tentação de escrever uma versão para cada um, e aí os
dois estariam jogando jogos diferentes.

A divisão é por **domínio**: quem age, o que causa dano, o que
transforma. Componente compartilhado mora no domínio dele, não no de
quem o usa.

### Separação que precisa ser preservada

`FormController` **não lê Input**. `Weapon` **não decide nada
autoritativo**. `Health` não sabe nada sobre movimento.

Isso não é purismo: na Fase 2 essas classes migram para o servidor
quase sem mudança. Se você acoplar input ou câmera dentro delas, a
migração vira reescrita.

`Player._PhysicsProcess` vira a **predição do cliente** na Fase 2.
Mantenha determinístico: nada de `GD.Randf`, nada de dependência de
tempo de parede, nada de leitura de estado visual. O teclado entra uma
vez por quadro, no `CharacterIntent`; sorteio de regra é `GameRandom`
(`MatchRandom.Stream(nome)`), com semente (`-- --seed=N` repete uma
partida). As razões estão em `docs/decisoes/`.

---

## Convenções de código

- Comentários em português, explicando **por quê**, não o quê
- Números de balanceamento sempre `[Export]`, nunca constante escondida
- Estatísticas de classe vão em `.tres`, não em código
- Um `[Signal]` para cada coisa que o HUD precisa saber; o HUD nunca
  faz polling
- Peças descartáveis (Dummy, HUD atual, clarão da granada) estão
  marcadas como tal no comentário de topo
- Nada de cache `static` de recurso montado em runtime (animação,
  material, nó de árvore): o C# segura a referência até o coletor
  passar, e o que sobra na fila quando o jogo fecha derruba a saída
  ("script_bindings is not empty"). O `Saida` limpa a fila no fim; não
  dependa dele para mais nada
- Indentação com **tab** em todo `.cs` (fixado no `.editorconfig`)
- **Classe grande é parcial, um arquivo por assunto**
  (`Classe.Assunto.cs`, numa pasta com o nome dela). O arquivo
  principal guarda o ciclo e lista as partes no comentário do topo;
  cada parte abre o seu `[ExportGroup]`. Campo mora na parte que é dona
  dele (ADR 0002)
- **Conta de regra vai para `scripts/domain`** (C# puro, sem Godot),
  com teste de unidade (ADR 0001). Número que se ajusta sem programar
  vai num `.tres` (ADR 0005)
- O que o jogador e o bot fazem igual mora uma vez só
  (`scripts/actors/body`, ADR 0003). Achou uma cópia? Junte — o
  `tools/testar.sh` reprova seis linhas iguais em dois lugares
  (`tools/duplicados.py`). Nome de grupo é `Groups`, peça de tela
  repetida é `UiKit`
- Método passou de uns 80 linhas? Quebre em passos com nome (a corrida
  na parede é o exemplo)

---

## Testes

**Rode antes de todo commit:** `tools/testar.sh` (com `GODOT=` apontando
para o Godot .NET, se ele não estiver no PATH). `--rapido` pula os dois
testes de partida; um nome filtra (`tools/testar.sh Salto`). O GitHub
roda o mesmo script a cada push (`.github/workflows/testes.yml`).

| Camada | Onde | O que prova |
|---|---|---|
| Unidade | `tests/unit` (C# puro, sem Godot nem pacote) | As regras: dano, WP, tempos dos golpes... Tudo de `scripts/domain` e o `WolfMoves` |
| Regressão | `tests/regression` (o jogo de verdade, sem tela, 60 quadros fixos, um processo por teste, semente fixa) | O comportamento aprovado: distância do salto, velocidades, altura do pulo, parede (correr, saltar, agarrar), passada do lobo, granada, dano da garra, ruído, níveis dos bots, clipes de animação, HUD, renascer, bots atirando, cenas sem erro |

- **Mudou um comportamento de propósito?** Mude o teste no mesmo
  commit, dizendo no teste por quê. Teste vermelho sem mudança
  combinada é regressão: conserte o código, não o teste.
- **Regra nova que dá para testar sem o Godot vai para `scripts/domain`**
  (C# puro) com teste de unidade. O que precisa de física, animação ou
  cena vira teste de regressão (`RegressionTest`, achado sozinho pela
  bancada).
- Um teste de regressão também reprova se o Output tiver `ERROR`.
- Para medir velocidade, use `bench.Displacement(corpo, quadros)`, não
  segundos: o cronômetro pode chegar um quadro depois e somar 3%.

---

## Estado atual e próximos marcos

Veja `CHANGELOG.md` para o que já entrou.

**Fase 1 — falta:**
- **Tela de créditos**, antes de qualquer publicação: o Oculus Audio
  Pack é CC BY 4.0.
- Rodar os quatro testes de validação do `docs/ARSENAL.md` (duelos,
  lobo no corredor e em campo aberto, ritmo da economia).
- Responder "A pergunta que decide o projeto", no fim deste guia.

**Fora do escopo enquanto não houver servidor** (o guia `docs/UI.md`
descreve, mas depende de rede): login de verdade, canais, amigos,
matilha, chat entre jogadores, ranking, compra paga (Sangue),
temporadas e moderação. Na criação de sala, os modos além de Equipe, a
regra "Só faca" e as transformações "Obrigatória" e "Só um lado"
aparecem como "em breve".

**Fase 2 — os quatro pilares de netcode**, nesta ordem:
1. Predição no cliente
2. Reconciliação por número de input
3. Interpolação dos outros jogadores (~100ms no passado)
4. Lag compensation no servidor (rebobinar hitboxes)

Teste com latência artificial de 80ms e 150ms antes de abrir para
internet. O Godot tem simulação de rede embutida.

**Fase 3** — o que faz lembrar: uivo audível, ataque aéreo com peso,
modo assimétrico, segunda classe, mapa com verticalidade real.

---

## A pergunta que decide o projeto

**Transformar é gostoso? Saltar da parede em cima de alguém é gostoso?**

Se não der prazer com cápsula cinza e sem som, não vai dar com modelo
caro. Arte conserta mapa feio; não conserta mecânica morna.
