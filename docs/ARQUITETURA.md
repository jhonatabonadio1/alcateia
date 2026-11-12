# Arquitetura do Alcateia

Guia para quem vai abrir o código e mexer com as próprias mãos.

O `CLAUDE.md` diz **o que** o jogo deve ser e **por que** cada regra
existe. Este arquivo diz **como o código está montado**, por onde a
informação passa e **onde mexer** para cada tipo de mudança. Quando os
dois parecerem discordar, o `CLAUDE.md` manda nas regras de design e
este manda na descrição do código — e um dos dois precisa ser
corrigido.

---

## 1. Antes de começar

**Ferramentas**

- Godot **4.7.2 .NET** (a versão "Mono"/C#, não a padrão)
- .NET SDK compatível com essa versão do Godot
- Python 3, só para os geradores em `tools/`
- Um editor C# com suporte a Godot: Rider ou VS Code com a extensão C#

**Rodar**

| Atalho no Godot | O que abre |
|---|---|
| F5 | A cena principal: `scenes/menu/menu.tscn` |
| F6 com `scenes/main.tscn` aberta | A partida direto, sem menu, com um 4 contra 4 padrão |
| F6 com `scenes/treino.tscn` aberta | Caixa de areia: chão, paredes, bonecos e dois bots, sem partida |

Depois de mudar C#, o Godot recompila ao dar play. Se aparecer erro de
build, ele está na aba **MSBuild** do painel de baixo, não na aba
Output.

**Onde fica o seu perfil salvo** (conjuntos, último usado):
`user://perfil.json`. No macOS isso é
`~/Library/Application Support/Godot/app_userdata/alcateia/perfil.json`.
Apagar esse arquivo devolve os quatro conjuntos padrão.

---

## 2. Godot em uma página

O suficiente para ler este projeto. Quem já sabe pode pular.

**Nó e cena.** Tudo no jogo é um nó. Uma cena (`.tscn`) é uma árvore de
nós salva em arquivo, que pode ser instanciada dentro de outra cena —
o jogador é uma cena, a partida instancia ela.

**Script.** Um `.cs` com `public partial class X : TipoDeNó` vira o
comportamento de um nó. O nome da classe precisa ser igual ao do
arquivo.

**Ciclo de vida, na ordem em que acontece:**

| Método | Quando roda | Uso aqui |
|---|---|---|
| `_EnterTree()` | Ao entrar na árvore. **Pai antes dos filhos** | Aplicar time e nome antes que os filhos se pintem |
| `_Ready()` | Quando o nó e **todos os filhos** estão prontos. Filhos antes do pai | Buscar nós irmãos, ligar sinais |
| `_Process(delta)` | Todo quadro de tela | HUD, animação, timers visuais |
| `_PhysicsProcess(delta)` | Passo fixo de física (60/s) | Movimento, tiro, IA |
| `_Input(event)` | Cada evento de teclado/mouse | Olhar com o mouse |
| `_ExitTree()` | Ao sair da árvore | Desligar eventos estáticos (senão vazam) |

**`[Export]`** expõe um campo no Inspector. Todo número de
balanceamento é `[Export]` — você ajusta no editor, sem recompilar.

**`[Signal]`** é um evento do Godot. Declara-se como
`[Signal] public delegate void DiedEventHandler(int killerId);`, emite
com `EmitSignal(SignalName.Died, id)` e escuta com
`health.Died += OnDied;`.

**Resource (`.tres`)** é um objeto de dados salvo em arquivo. As armas
são Resources: cada `.tres` em `resources/weapons/` é uma
`WeaponDefinition` preenchida.

**`GetNode<T>("Caminho")`** busca um nó pelo caminho relativo. Este
projeto depende muito disso — veja a seção 4.

**Grupos.** `AddToGroup("player")` marca um nó; quem precisar acha com
`GetTree().GetFirstNodeInGroup("player")`.

**`CallDeferred`** adia uma chamada para o fim do quadro. Usado quando
mexer na árvore no meio de um sinal causaria erro.

---

## 3. Mapa das pastas

```
alcateia/
├── project.godot          configuração; cena principal = menu
├── CLAUDE.md / AGENTS.md  regras de design e decisões (o mesmo arquivo)
├── docs/
│   ├── ARQUITETURA.md     este guia
│   ├── ARSENAL.md         especificação das armas
│   ├── CONJUNTOS.md       especificação de conjuntos e economia
│   └── CHANGELOG.md
├── scenes/
│   ├── menu/menu.tscn     entrada do jogo
│   ├── main.tscn          a partida
│   ├── treino.tscn        caixa de areia
│   ├── player.tscn        o seu personagem
│   ├── bot.tscn           o bot
│   ├── dummy.tscn         boneco de tiro
│   ├── grenade.tscn       granada de choque
│   └── maps/hangar.tscn   o mapa (gerado por tools/gerar_hangar.py)
├── resources/
│   ├── weapons/*.tres     armas (geradas por tools/gerar_arsenal.py)
│   └── breeds/*.tres      classes de lobo
├── assets/                modelos, animações, texturas
├── tools/                 geradores em Python
└── scripts/
    ├── actors/            quem decide: Player (input) e Bot (IA)
    ├── combat/            vida, armas, granada, arsenal, conjuntos
    ├── forms/             transformação humano ↔ lobo e WP
    ├── ai/                pontuação de decisões do bot
    ├── core/              partida, sala, perfil, mapa, barramentos
    ├── ui/                HUD, menus da partida, minimapa
    ├── menu/              menu principal
    ├── visual/            animação do personagem
    ├── net/               rede — dormente até a Fase 2
    └── debug/             peças descartáveis
```

> **Lixo antigo na raiz.** `main.tscn`, `player.tscn`, `dummy.tscn` e
> `bruto.tres` soltos na raiz são cópias da v0.2.0 que apontam para
> scripts que não existem mais (`res://Player.cs`). Nada os usa. As
> versões de verdade estão em `scenes/` e `resources/`. Podem ser
> apagados.

A divisão é **por domínio, não por dono**. Não existe pasta `player/`
porque `Health`, `Weapon` e `FormController` são usados igualzinho pelo
jogador e pelo bot. Se um dia você escrever um `PlayerWeapon` separado
do `BotWeapon`, os dois passam a jogar jogos diferentes e o
balanceamento contra bot deixa de valer contra gente.

---

## 4. As camadas

```
            ┌──────────────────────────────────────────┐
  QUEM      │  Player.cs (lê teclado/mouse)            │
  DECIDE    │  Bot.cs + BotBrain.cs (lê o mundo)       │
            └──────────────┬───────────────────────────┘
                           │ chama métodos
            ┌──────────────▼───────────────────────────┐
  O QUE     │  Weapon      FormController     Health   │  ← os mesmos
  ACONTECE  │  (atirar)    (transformar, WP)  (vida)   │    para os dois
            └──────────────┬───────────────────────────┘
                           │ emite sinais / eventos estáticos
            ┌──────────────▼───────────────────────────┐
  QUEM      │  Hud, MatchHud, Minimap, DamageIndicator │
  MOSTRA    │  ViewModel, WolfVisual, CharacterAnimator│
  E TOCA    │  WorldAudio, CombatantAudio              │
            └──────────────────────────────────────────┘

  SERVIÇOS ESTÁTICOS (qualquer um consulta):
  Match.Current · KillFeed · NoiseBus · UiState · Arsenal · Profile
  RoomConfig · Difficulty · SpawnPoints · PatrolRoutes · Teams · Sfx
```

**Regras que mantêm isso de pé:**

1. **Setas só para baixo.** Quem decide chama os componentes; os
   componentes avisam por sinal; a UI só escuta. O HUD nunca chama
   `Weapon.TryAttack()`, e `Health` não sabe que existe HUD.
2. **A UI não faz polling** de estado de jogo quando existe sinal para
   aquilo. Precisa mostrar algo novo? Crie um sinal no componente.
3. **`FormController` não lê input. `Weapon` não decide nada
   autoritativo. `Health` não sabe de movimento.** Na Fase 2 essas
   classes vão para o servidor; acoplar input ou câmera nelas vira
   reescrita.
4. **`Player._PhysicsProcess` precisa ser determinístico** — nada de
   `GD.Randf()` nem de leitura de estado visual ali. Na Fase 2 ele vira
   a predição do cliente.

---

## 5. Anatomia de um combatente

`player.tscn` e `bot.tscn` têm a mesma espinha:

```
Player (CharacterBody3D)  ← Player.cs      Bot (CharacterBody3D) ← Bot.cs
├── CollisionShape3D                        ├── CollisionShape3D
├── MeshInstance3D        cápsula reserva   ├── MeshInstance3D
├── Human                 ← CharacterAnimator├── Human ← CharacterAnimator
│   └── Model             o FBX animado     │   └── Model
├── WolfVisual            ← WolfVisual.cs   ├── WolfVisual
├── Head                  gira no eixo X    ├── Head
│   ├── Camera3D          1ª pessoa         │   └── Camera3D
│   │   └── ViewModel     arma na tela      ├── Health
│   └── SpringArm3D                         ├── FormController
│       └── WolfCamera    3ª pessoa (lobo)  ├── HealthBar ← barra 3D
├── Health                                  └── Weapon
├── FormController
├── Weapon
├── MultiplayerSynchronizer  (Fase 2)
└── CombatantAudio        sons do corpo (o Bot também tem)
```

**Os nomes são contrato.** O código acha as peças por nome:
`GetNode<Health>("Health")`, `"Weapon"`, `"FormController"`, `"Head"`,
`"Head/Camera3D"`, `"Human"`. E o `Weapon` de um atirador procura o
`Health` **do alvo** pelo nome também. Renomear um desses nós no editor
quebra o jogo em tempo de execução, sem erro de compilação.

**Quem gira o quê.** O corpo gira no eixo Y (olhar para os lados). O
`Head` gira no eixo X (olhar para cima e para baixo). O recuo mexe nos
dois: vertical no `Head`, lateral no corpo.

**Grupos.** O jogador entra no grupo `"player"`; os bots no `"bot"`. É
assim que HUD, minimapa e menus acham você.

---

## 6. Os fluxos principais

### 6.1 Do menu à partida

```
menu.tscn (MainMenu.cs)
  │  Criar sala → tela da sala: times, bots, pontos, tempo, dificuldade
  │  Pronto → Iniciar partida
  │     preenche RoomConfig (estático): Active, PlayerTeam, Bots,
  │     ScoreToWin, Duration · Difficulty.Set(nível)
  ▼
ChangeSceneToFile("res://scenes/main.tscn")
  │
  ├─ Player._EnterTree     lê RoomConfig.PlayerTeam → Health.Team
  ├─ Match._Ready          lê ScoreToWin e Duration; começa a contagem
  └─ MatchSetup._Ready     põe você num spawn da sua base;
                           instancia bot.tscn para cada bot da sala
                           (ou a escalação padrão se RoomConfig.Active
                           for falso — partida aberta pelo editor)
```

`RoomConfig` é estático porque sala e partida são cenas diferentes e o
Godot destrói uma ao abrir a outra. Estático é a ponte mais simples.

**A ordem dos nós em `main.tscn` importa.** `MatchSetup` vem depois de
`Bots` para o nó `Bots` já existir quando ele for preencher. HUD e menus
vêm depois do `Player` para achá-lo no `_Ready`.

### 6.2 Um tiro, ponta a ponta

```
Player._PhysicsProcess
  └─ Input "attack" → Weapon.TryAttack()
        ├─ checa: forma permite atacar? cadência? pente? recarga?
        └─ Shoot()
             ├─ dispersão atual (SpreadNow) → direção com Scatter()
             ├─ para cada chumbo: Raycast() → acerta Health de alguém?
             │     soma dano por alvo × DamageScale × falloff × cabeça
             ├─ Tracer() (visual)       NoiseBus.Emit(Shot)  ← bots ouvem
             ├─ EmitSignal(Fired)  → Player.OnWeaponFired (recuo)
             │                     → ViewModel / WolfVisual (coice)
             └─ Damage(alvo, ...)
                  ├─ mesmo time? ignora (sem fogo amigo)
                  ├─ alvo.NoteAttacker(eu)       (para assistências)
                  ├─ alvo.TakeDamage(...)
                  │     ├─ EmitSignal(Damaged) → Hud, DamageIndicator,
                  │     │                         CharacterAnimator.Hit,
                  │     │                         Bot.OnDamaged
                  │     ├─ escudo absorve primeiro, depois HP
                  │     ├─ EmitSignal(Changed) → barras
                  │     └─ HP ≤ 0 → Die() → EmitSignal(Died)
                  ├─ EmitSignal(HitConfirmed) → marcador de acerto
                  └─ se matou:
                       FormController.OnKill (+WP)
                       TakeAssists → OnAssist (+WP) para quem ajudou
                       KillFeed.Report(Kill) → Hud (feed), Match (placar)
```

O dano é calculado **no `Weapon` do atirador**, e o `Health` do alvo só
aplica. É o lugar que vai virar servidor na Fase 2.

### 6.3 Morte e renascimento

```
Health.Died
  ├─ Player.OnDied / Bot.OnDied   câmera, estado, zera recuo
  ├─ CharacterAnimator            animação de morte, depois some aos poucos
  ├─ Hud                          aviso
  └─ LoadoutMenu (só o jogador)   abre a tela de respawn (5 s):
                                  escolhe conjunto → paga WP → Weapon.Equip
Health.TryRespawn → Respawned → volta ao spawn via SpawnPoints.Pick
```

O corpo morto cai **com colisão** até tocar o chão e só então desliga a
colisão e para de se mover (`Settle`). Veja a seção 9 sobre por que a
ordem importa.

### 6.4 Transformação

`FormController` é uma máquina de estados:

```
Human → ShiftingToWolf (0,8 s) → Wolf → ShiftingToHuman (0,8 s) → Human
```

Quem pede a troca é o `Weapon.SelectSlot` (tecla 3 = lobo, 1/2/4 =
humano). O `FormController` troca a vida mantendo a proporção
(`Health.SetPool(..., keepRatio: true)`) e emite `FormChanged`.
Quem escuta: `Player` (física e câmera), `ViewModel`, `WolfVisual`,
`CharacterAnimator`, `Hud`.

### 6.5 Som

```
Player/Bot/Weapon/Grenade ─ NoiseBus.Emit(ponto, raio, tipo) ─┬─> Bot.OnNoise (IA)
                                                              └─> WorldAudio ─> Sfx.At(nome, ponto, raio)
Health/Weapon/FormController ─ sinais ─> CombatantAudio ─> Sfx (até 12 m)
Menus ─ Sfx.Wire(botão) ─> Sfx.Ui
```

O som do mundo **é** o ruído que os bots ouvem, tocado. Por isso não
existe tiro audível que o bot não ouviu.

### 6.6 O bot, a cada passo de física

```
Bot._PhysicsProcess
  ├─ morto? Settle · partida não começou? Fall
  ├─ ChooseTarget()     a cada 0,5 s: inimigo visível > lembrado > perto
  ├─ ninguém vivo do outro lado? → RunPatrol (rotas do mapa)
  ├─ UpdateReaction     atraso humano antes de reagir
  ├─ UpdateSight        CanSee: distância, ângulo, linha de visão
  ├─ UpdateState        Idle / Alert / Combat / Search
  ├─ UpdateBrain        monta um BotContext → BotBrain.Choose()
  │                     pontua cada BotAction com curvas de Utility.cs
  │                     e escolhe a maior (com inércia para não tremer)
  └─ executa a ação: RunCombat, RunTakeCover, RunRetreat, RunHowl,
     RunSearch, Reload, Transform, Revert...
```

**Ouvido.** O bot assina o `NoiseBus` no `_Ready`. Tiro, pouso, uivo e
explosão emitem ruído com raio; o bot que está dentro do raio passa a
"acreditar" que tem alguém naquele ponto (`Believe`) e vai checar.

**Dificuldade.** `Difficulty.Apply(bot)` sobrescreve os `[Export]` do
bot (reação, precisão, agressividade) no `_Ready`. Para mudar como um
nível joga, mexa em `Difficulty.cs`, não no `bot.tscn`.

---

## 7. Como as peças conversam

Três jeitos, cada um com seu lugar:

| Jeito | Quando usar | Exemplos |
|---|---|---|
| **Chamada direta** | Quem decide manda um componente agir | `_weapon.TryAttack()`, `_form.ToggleForm()` |
| **Sinal do Godot** | Um componente avisa quem está pendurado **nele** | `Health.Died`, `Weapon.Fired`, `FormController.WpChanged` |
| **Evento estático** | Avisar **todo mundo**, sem saber quem | `KillFeed.Reported`, `NoiseBus` |

**Sinais existentes** (para a lista exata de quem escuta, procure
`.NomeDoSinal +=` no projeto)

| Componente | Sinal | Quem escuta |
|---|---|---|
| `Health` | `Changed(hp, maxHp, shield)` | Hud, HealthBar3D, Weapon |
| | `Damaged(amount, from)` | Hud, DamageIndicator, CharacterAnimator, Bot |
| | `Died(killerId)` | Player, Bot, Hud, LoadoutMenu, CharacterAnimator |
| | `Respawned()` | Bot, e quem precisa reagir ao renascer |
| `Weapon` | `Fired()` | Player (recuo), ViewModel, WolfVisual |
| | `HitConfirmed(headshot)` | Hud |
| | `AmmoChanged(inMag, magSize, reloading)` | Hud |
| | `SlotChanged(slot)`, `GrenadesChanged(left)`, `DamageDealt(amount)` | HUD e quem mais precisar |
| `FormController` | `FormChanged(form)` | Player, Hud, ViewModel, WolfVisual, CharacterAnimator |
| | `WpChanged(wp, max)` | Hud, LoadoutMenu |
| | `TransformDenied(missing)` | aviso de "falta WP" |
| `Match` | `PhaseChanged(phase)`, `ScoreChanged(blue, red)` | HUD da partida |

**Evento estático tem uma regra:** quem assina no `_Ready` desassina no
`_ExitTree`. Senão, ao reiniciar a partida, o objeto destruído continua
recebendo eventos e o jogo quebra com `ObjectDisposedException`.

```csharp
public override void _Ready()    => KillFeed.Reported += OnKill;
public override void _ExitTree() => KillFeed.Reported -= OnKill;
```

---

## 8. Serviços estáticos

| Classe | Guarda | Vive enquanto |
|---|---|---|
| `Match.Current` | A partida em andamento. `Match.IsLive` é **verdadeiro quando não há partida** (treino) | A cena da partida |
| `RoomConfig` | Configuração da sala para a partida ler | O jogo aberto |
| `Profile` | Conjuntos, inventário, último usado. Salva em `user://perfil.json` | Em disco |
| `Arsenal` | Catálogo: ids, preços, `Get(id)` carrega o `.tres` | Sempre |
| `Difficulty` | Nível atual dos bots | O jogo aberto |
| `UiState` | Quantos menus estão abertos — **dono do cursor** | O jogo aberto; `Reset()` ao trocar de cena |
| `SpawnPoints` | Pontos de nascimento por time, registrados pelo mapa | A cena do mapa |
| `PatrolRoutes` | Rotas de ronda, registradas pelo mapa | A cena do mapa |
| `KillFeed` | Evento de abate | Sempre |
| `Sfx` | Toca som por nome; cria os canais SFX, Interface e Musica | Sempre |
| `NoiseBus` | Evento de som | Sempre |
| `Teams` | Constantes `Blue = 1`, `Red = 2`, cor e nome | Sempre |

**`UiState` é um contador, não um booleano.** Cada menu que abre chama
`UiState.Push()` e ao fechar `UiState.Pop()`. Enquanto
`UiState.AnyOpen`, o cursor fica solto e o `Player` ignora o mouse.
Antes de trocar de cena ou recarregar, chame `UiState.Reset()`, senão o
contador fica preso e o mouse nunca volta a ser capturado.

---

## 9. Armadilhas que já custaram horas

**Desligar a colisão faz `IsOnFloor()` mentir.** Sem colisão não existe
chão, então `IsOnFloor()` responde falso e a gravidade volta a puxar.
Foi o corpo morto atravessando o mapa. Depois de assentar, não chame
mais `MoveAndSlide`.

**Propriedade de `.tres`/`.tscn` com nome errado é ignorada em
silêncio.** Se o `.tres` diz `RecoilRecovry = 9`, o Godot não reclama:
usa o valor padrão do `[Export]`. Renomeou um `[Export]`? Procure o
nome antigo em `resources/` e `scenes/`.

**Renomear um nó quebra o `GetNode`** — sem erro de compilação, só
exceção ao rodar. Veja a seção 5.

**`_EnterTree` vs `_Ready`.** O pai faz `_EnterTree` antes dos filhos,
mas `_Ready` depois deles. Para o pai configurar algo que os filhos usam
no `_Ready` deles (o time, que define a cor), tem que ser no
`_EnterTree`.

**Arquivo gerado sobrescreve edição à mão.** `resources/weapons/*.tres`
e `scenes/maps/hangar.tscn` saem dos scripts em `tools/`. Ajustou no
Inspector e depois rodou o gerador? Perdeu o ajuste. Mude no gerador.

**Método com nome de tipo do Godot.** Um método chamado `Panel()` ou
`Button()` dentro de uma classe que herda de `Control` conflita com os
tipos `Panel` e `Button`. Use `MakePanel`, `MakeButton`.

**Animação Mixamo e a "Take 001".** O FBX traz mais de uma animação; o
`CharacterAnimator` escolhe a que tem "mixamo" no nome. Escolher pela
mais longa ou com mais trilhas pega a errada e o boneco fica em T.

**Deslocamento do modelo.** A altura do modelo é aplicada no nó
`Human`; o `Model` fica em zero. Aplicar nos dois afunda o boneco.

**Mapa importado de uma face só.** Parede com uma face só colide de um
lado só — o lobo atravessa ou não gruda. O `MapLoader.BuildCollision`
gera colisão dos dois lados.

**Dano em dobro.** As armas usam os números do `ARSENAL.md` (feitos
para 100 de vida) e o `Weapon` multiplica por `Arsenal.DamageScale = 2`
porque o humano tem 200. Não multiplique de novo no `.tres`.

---

## 10. Receitas

### Mudar o balanceamento de uma arma

1. Abra `tools/gerar_arsenal.py` e mude o número no dicionário da arma.
2. Rode da raiz do projeto: `python3 tools/gerar_arsenal.py`.
3. Confira no `docs/ARSENAL.md` se "tiros para matar" ainda bate —
   lembrando que o dano é multiplicado por 2 na hora do tiro.
4. Teste no `treino.tscn` contra os bonecos.

Para experimentar rápido, pode mexer direto no `.tres` pelo Inspector;
só passe o valor final para o gerador depois.

### Adicionar uma arma

1. No `gerar_arsenal.py`, copie o dicionário de uma arma parecida, mude
   `Id`, `DisplayName`, `Class`, `WpCost` e os números. Rode o gerador.
2. Em `scripts/combat/Arsenal.cs`, ponha o id em `Primaries` ou
   `Secondaries`.
3. Pronto: aparece no inventário, no editor de conjuntos e no catálogo.
   Se não aparecer num perfil já salvo, confira o `Profile.Repair` —
   ou apague o `perfil.json`.

Se a arma precisa de um comportamento novo (rajada, mira telescópica),
o campo vai em `WeaponDefinition.cs` e o comportamento em
`Weapon.Shoot()`/`TryAttack()`.

### Ajustar movimento, pulo, lobo

Tudo é `[Export]` do `Player` (e do `Bot`, que tem os próprios). Ajuste
no Inspector com `player.tscn` aberto. As classes de lobo estão em
`resources/breeds/*.tres` (`WolfBreed`).

### Adicionar uma tecla

1. `scripts/core/InputSetup.cs`: `Key("nome_da_acao", Godot.Key.F);`
2. Leia onde fizer sentido: `Input.IsActionJustPressed("nome_da_acao")`.
3. Atualize a tabela de controles do `CLAUDE.md`.

As ações são registradas por código, não pelo editor, para não depender
do `project.godot` e para não haver duas fontes.

### Mostrar algo novo no HUD

1. No componente que sabe a informação, crie um sinal:
   `[Signal] public delegate void XEventHandler(float valor);`
2. Emita quando mudar: `EmitSignal(SignalName.X, valor);`
3. Em `Hud.cs`, no `_Ready`, ligue `_componente.X += OnX;` e desenhe.

O HUD é montado por código (sem cena), então os elementos são criados
no próprio `Hud.cs`.

### Adicionar um som

**Trocar um som existente:** os arquivos de `assets/audio/` saem de
`tools/sons/processar.py` (precisa de ffmpeg e numpy). Mude a linha do
som lá e rode, da raiz:

```
python3 tools/sons/baixar.py /tmp/sons_brutos
python3 tools/sons/processar.py /tmp/sons_brutos assets/audio
```

Ou jogue um arquivo seu com o mesmo nome por cima, e anote a origem em
`assets/audio/CREDITOS.md`. Só lembre que rodar o gerador de novo
sobrescreve.

**Som de um evento que já existe, sem arquivo ainda:** jogue o arquivo
em `assets/audio/` com o nome da tabela do `LEIA-ME.md`. Nada de
código.

**Som de um evento novo:**

- É informação de jogo (dá para saber onde alguém está pelo som)?
  Crie um `NoiseKind`, emita com `NoiseBus.Emit(posição, raio, tipo,
  this)` e trate o tipo novo no `switch` do `WorldAudio.OnNoise`. Os
  bots passam a ouvir junto, automaticamente.
- É detalhe do corpo, que só importa de perto? Toque no
  `CombatantAudio`, a partir de um sinal do componente.
- É seu (retorno, interface)? `Sfx.Local("nome")` ou
  `Sfx.Ui("nome")` de onde fizer sentido.

Acrescente o nome novo no `assets/audio/LEIA-ME.md`.

Botão novo de menu ganha som com `Sfx.Wire(botao)`.

### Abrir um menu novo

```csharp
void Abrir()  { Visible = true;  UiState.Push(); }
void Fechar() { Visible = false; UiState.Pop();  }
```

Se o menu troca de cena, chame `UiState.Reset()` antes do
`ChangeSceneToFile`. Use um `CanvasLayer` com `Layer` acima do HUD.

### Criar um mapa

O caminho atual é gerar o `.tscn` por script, como
`tools/gerar_hangar.py`. A cena do mapa entra no `main.tscn` como o nó
`Map` (o minimapa procura por esse nome) e precisa ter:

- **Geometria** em `StaticBody3D` com `BoxMesh` — o minimapa desenha
  toda caixa que encontrar, com a cor do material dela, e ignora pisos
  maiores que 1200 m².
- **Dois `SpawnPoints`**, um com `Team = 1` (azul) e outro com
  `Team = 2` (vermelho), com os pontos de nascimento de cada base.
- **`PatrolRoutes`** com as rondas dos bots. Cada mapa traz as suas.
- **`NavBaker`**, que gera a malha de navegação ao carregar.
- **`Label3D`** com texto curto ("A", "B") onde quiser marca no
  minimapa.

Depois troque o mapa instanciado em `scenes/main.tscn` (nó `Map`).

As alturas precisam conversar com a física: um degrau que o humano
sobe, uma caixa que só o lobo alcança. O `CLAUDE.md`, seção "O mapa
Hangar", tem as medidas.

### Ensinar uma decisão nova ao bot

1. `scripts/ai/BotBrain.cs`: acrescente o valor em `BotAction` e uma
   linha no `switch` de `Score`, pontuando com as curvas de `Utility.cs`
   e os campos do `BotContext`.
2. Se a decisão precisa de uma informação nova, acrescente o campo em
   `BotContext` e preencha em `Bot.UpdateBrain`.
3. Em `Bot._PhysicsProcess`, no `switch (Action)`, chame um `RunX` novo.

Mantenha as pontuações entre 0 e 1. Uma ação que sempre ganha vira o
único comportamento do bot.

---

## 11. Depurar

- **Ver a árvore enquanto roda:** com o jogo aberto, aba **Remote** no
  painel Scene. Dá para clicar num bot e ver os valores ao vivo.
- **Ver colisões:** menu Debug → Visible Collision Shapes.
- **Ver navegação:** menu Debug → Visible Navigation.
- **Log:** `GD.Print(...)` aparece na aba Output. Tire antes de
  commitar o que for só investigação.
- **Isolar:** reproduza no `treino.tscn` antes de caçar na partida
  inteira. Menos coisa acontecendo, menos suspeitos.
- **Perfil corrompido:** apague o `perfil.json` (seção 1).

---

## 12. O que está dormente

`scripts/net/GameManager.cs` e o `MultiplayerSynchronizer` do
`player.tscn` são da Fase 2 (rede). Não estão ligados na partida. Não
apague: a separação de responsabilidades da seção 4 existe para que
essa fase seja uma migração, não uma reescrita.

`scripts/debug/` e o `Dummy` são descartáveis.

---

## 13. Glossário

| Termo | No código |
|---|---|
| WP | Pontos de lobo: moeda da partida. `FormController.Wp` |
| Conjunto | `LoadoutSet`: primária, secundária, utilitários |
| Padrão | O conjunto 1, grátis, sempre disponível |
| Forma | `Form`: Human, ShiftingToWolf, Wolf, ShiftingToHuman |
| Slot | `Slot`: o que está na mão (primária, secundária, lobo...) |
| Mestre | Quem criou a sala; só ele inicia |
| Crença | O ponto onde o bot acha que o inimigo está (`_hasBelief`) |
| Falloff | Queda de dano com a distância |
| Spread | Dispersão: o cone em que o tiro pode sair |
| Recoil | Recuo: quanto a mira sobe a cada tiro |
