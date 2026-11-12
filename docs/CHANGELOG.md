# Changelog

## v0.7.3 — 2026-11-11

### Alterado
- Saltar de uma parede para **outra** volta a grudar. A proibição
  deixa de ser "uma corrida por ida ao ar" e passa a ser "não na mesma
  parede": o elevador continua impossível, mas quicar entre paredes
  opostas de um corredor funciona. O critério é a semelhança entre as
  normais (`SameWallThreshold`), não o tempo nem o contato com o chão.

### Corrigido
- Traçante criava um material novo a cada disparo, o que obriga o
  Godot a montar pipeline de render na hora e travava o quadro com
  dois bots atirando. Agora o material é único e compartilhado.

## v0.7.2 — 2026-11-11

### Corrigido
- Morrer era indistinguível de travar: a tela não dizia nada, o
  controle parava e o jogador revivia no mesmo lugar 3 segundos
  depois. Agora o HUD mostra "ABATIDO" com a contagem.
- Reviver devolvia o jogador exatamente de onde ele tinha sido morto,
  ou seja, de volta ao tiroteio e ao mesmo bot. Jogador e bot voltam
  ao ponto de origem.
- Bot que morria como lobo revivia como lobo. Volta a humano ao
  renascer.

### Adicionado
- Sinal `Respawned` em `Health`.

## v0.7.1 — 2026-11-11

### Corrigido
- O bot virava lobo a menos de 9m e, sendo o lobo corpo-a-corpo,
  grudava no jogador e nunca atirava. Agora virar lobo é recurso de
  desespero: só abaixo de 40% de vida e com o alvo ao alcance.
- Disparos não tinham retorno visual nenhum, então o bot parecia
  inofensivo enquanto acertava. Todo tiro agora deixa traçante.

### Adicionado
- Combate à distância no estilo de bots de FPS: mantém o alcance da
  arma, circula com strafe de lado alternado, e atira em rajadas com
  pausa em vez de fogo contínuo.
- `EngageRange` limita o alcance efetivo do bot — acertar do outro
  lado do mapa torna o jogo insuportável.

## v0.7.0 — 2026-11-11

### Adicionado
- **Bot oponente** (`scripts/actors/Bot.cs`) reusando Health,
  FormController e Weapon do jogador. Percepção por distância, campo
  de visão e linha de visada, com memória curta. De humano mantém o
  alcance da arma; de lobo fecha a distância. Duas instâncias no mapa.

### Alterado
- **Projeto reorganizado em pastas**: `scenes/`, `resources/breeds/`,
  `scripts/{actors,combat,forms,ui,core,net,debug}` e `docs/`. Antes
  eram 14 arquivos soltos na raiz. `CLAUDE.md` e `AGENTS.md` ficam na
  raiz, que é onde agentes procuram.
- `run/main_scene` passou a apontar para o caminho explícito em vez de
  um uid, que não sobrevive bem a mover arquivos fora do editor.

## v0.6.1 — 2026-11-11

### Corrigido
- Atraso ao voltar para humano: a física já trocava no início da
  transição, mas a câmera só voltava à primeira pessoa no fim. Agora
  a câmera segue o corpo pela mesma regra.

### Adicionado
- Segurar o pulo durante a queda alivia a gravidade em 22%
  (`HoldFallGravity`). Sutil de propósito: controle fino de onde
  aterrissar, sem virar planagem.

## v0.6.0 — 2026-11-11

### Alterado
- **A física troca no início da transição de forma**, não no fim.
  Antes, apertar 3 e pular em seguida ainda usava o pulo humano
  durante os 0,8s da transição. A vulnerabilidade da transição
  continua sendo a impossibilidade de atacar, não de se mover.
- Pulo passa a ser um por toque: segurar não repete e uma trava curta
  impede que o mesmo toque dispare o pulo do chão e o da parede.
- Granada também respeita a transição: não é arremessada enquanto a
  troca de forma está em curso.

## v0.5.4 — 2026-11-11

### Corrigido
- O alcance do pulo não respondia à tecla: o impulso era conservado
  por inteiro e o salto sempre ia até o fim. Um arrasto suave no ar
  (`AirFriction`) devolve o controle — segurar mantém o avanço,
  soltar encurta.
- Ao encontrar uma parede em ângulo durante a corrida, o lobo voltava
  pelo outro lado. O sentido era decidido comparando com o sentido
  antigo, e em paredes perpendiculares essa comparação dá zero. Agora
  quem decide é o movimento que o lobo já carrega, descartando o lado
  bloqueado.

## v0.5.3 — 2026-11-11

### Corrigido
- Soltar as teclas no ar travava o corpo no lugar, inclusive no meio
  do pulo. A desaceleração era aplicada sem considerar o tempo nem se
  o jogador estava no chão, zerando a velocidade horizontal em um
  frame. Agora chão e ar são tratados separadamente: o chão freia
  rápido (`GroundFriction`) e o ar conserva o impulso com aceleração
  reduzida (`AirAccel`).

## v0.5.2 — 2026-11-11

### Corrigido
- Agachamento acontecia em dois tempos. A cápsula encolhe em torno do
  próprio centro, então os pés subiam no ar e a gravidade os trazia de
  volta. Agora o corpo desce metade da diferença de altura a cada
  passo, mantendo os pés plantados.
- Transição de agachar ficou mais rápida (12 → 22).

## v0.5.1 — 2026-11-11

### Alterado
- Humano mais lento e com pulo mais curto: velocidade 5.2 → 4.2,
  pulo 3.9 → 3.0.
- Multiplicadores das classes subiram para compensar, já que a
  velocidade do lobo deriva da humana: Bruto 1.1 → 1.5 de velocidade
  e 1.3 → 1.9 de pulo; Ligeiro 1.6 → 2.1 e 1.8 → 2.5. Sem isso o lobo
  teria encolhido junto com o humano e o contraste continuaria igual.

## v0.5.0 — 2026-11-11

### Adicionado
- **Agachar** (Ctrl) nas duas formas: encolhe a cápsula de 1.8 para
  1.0, baixa o olho e corta a velocidade para 45%. Não funciona
  durante a corrida em parede.
- Volta ao spawn ao cair abaixo de `FallLimit`. Antes, sair do mapa
  era queda infinita.

### Alterado
- Humano mais lento e mais pesado: velocidade 7.0 → 5.2, pulo
  5.0 → 3.9. A mobilidade passa a pertencer ao lobo.
- Diagnóstico de parede imprime só quando a mensagem muda, e a
  transição de quina passou a avisar quando dispara e quando desiste.

## v0.4.3 — 2026-11-11

### Corrigido
- O lobo regrudava na parede na mesma altura logo após o salto,
  transformando a parede em elevador. Agora é necessário tocar o chão
  entre uma corrida e a seguinte (`RequireFloorBetweenWallRuns`).
  Desligando a exigência, um tempo mínimo (`WallRunCooldown`) impede
  o regrude imediato e permite encadear paredes opostas.

## v0.4.2 — 2026-11-11

### Corrigido
- O salto da parede zerava o avanço acumulado na corrida e empurrava
  o lobo apenas de lado. Agora conserva uma fração do avanço
  (`WallJumpKeepSpeed`, 0.8) e soma o empurrão perpendicular, o que
  torna possível saltar em direção a alguém à frente.

## v0.4.1 — 2026-11-11

### Alterado
- A direção da corrida em parede passa a vir do **momento** do lobo,
  não da câmera. Saltar de uma parede e encostar na seguinte mantém o
  sentido sem precisar mover o mouse. O olhar só decide quando não há
  movimento horizontal.
- `FindWall` virou um leque de 8 raios em volta, substituindo os dois
  raios laterais. O ângulo do corpo deixa de influenciar se a parede é
  encontrada, e a parede mais próxima vence.

## v0.4.0 — 2026-11-11

### Alterado
- Corrida em parede só começa **na descida do pulo**. Encostar na
  parede estando no chão não gruda mais.
- Sucção contra a parede (`WallStickForce`): o lobo fica colado em vez
  de apenas correr perto da superfície.

### Adicionado
- **Transição de quina**: ao acabar uma face, a corrida troca para a
  face seguinte em vez de soltar. A nova normal é a direção em que se
  corria; o novo sentido é o oposto da normal antiga. Uma janela de
  tolerância (`CornerGrace`) cobre o trecho sem contato entre as duas
  faces. Permite dar a volta completa num bloco.

## v0.3.0 — 2026-11-11

### Alterado
- **Transformação livre**: `WpCost` das classes básicas vai a zero.
  Decisão do autor. O WP continua no sistema para as classes caras.
- **Transformar não cura mais**: a proporção de vida passa a ser
  preservada entre os corpos. Com transformação gratuita, encher a
  vida a cada troca era cura infinita e tornava o uivo inútil.
  `RefillOnTransform` traz o comportamento antigo de volta.
- `Health.SetPool` ganhou o modo `keepRatio`.

## v0.2.1 — 2026-11-11

### Corrigido
- Grupo `player` no `main.tscn`: a declaração vai dentro dos colchetes
  do cabeçalho do nó. Como linha de propriedade o Godot ignorava em
  silêncio e o HUD não encontrava o jogador.
- HUD ganhou varredura da cena como plano B quando o grupo não existe.

### Alterado
- Corrida em parede: altura travada (não sobe nem desce) e direção
  travada no momento de grudar. O olhar passa a ser livre durante a
  corrida — mirar não desvia mais o movimento. Para mudar de lado é
  preciso cair e recomeçar.
- `WallRunGravityScale` virou `WallRunVerticalDrift`, em metros por
  segundo, com zero como padrão.

## v0.2.0 — 2026-11-11

### Adicionado
- Granada de atordoamento (`Grenade.cs`, `grenade.tscn`): arremesso em
  arco com física, pavio de 1,8s, dano com falloff e atordoamento em
  área. Duas por vida, reabastece ao renascer, só na forma humana.
- Atordoamento no `Player`: reduz velocidade e desativa a corrida em
  parede. É a resposta do humano contra o lobo.
- Contador de granadas no HUD.
- `CLAUDE.md` — guia para agentes, com as regras de design, a
  arquitetura e o que não pode ser violado.

### Decisões de design
- A granada tira mobilidade, não dá dano pesado — o poder do lobo vem
  da mobilidade, então é ela que precisa ser atacada.
- Granada é slot de ação, não de arma: apertar 4 arremessa na hora,
  sem deixar o jogador segurando granada no meio do tiroteio.
- Arremesso soma a velocidade do jogador, então correr e jogar leva
  mais longe.

## v0.1.0 — 2026-11-11

Primeira versão jogável. Fase 1, local, sem rede.

### Adicionado
- Controlador em primeira pessoa
- Vida, escudo separado do HP, morte e respawn
- Máquina de estados da transformação com quatro estados e economia
  de WP
- Sistema de slots: 1 principal, 2 secundária, 3 lobo, 4 granada
- Duas armas: principal automática, secundária semiautomática
- Garrada do lobo com bônus de ataque aéreo
- Uivo: recarrega escudo, trava no lugar
- Corrida em parede e salto da parede, só na forma lobo
- Câmera em primeira pessoa no humano, terceira pessoa no lobo
- HUD com vida, escudo, WP e slot atual
- Registro de input por código, sem configuração no editor
- Boneco de teste que pisca ao levar dano
- Classes Bruto e Ligeiro como recursos editáveis

### Decisões de design
- Transformar é um slot, não um botão separado
- Corrida em parede sem limite de tempo; solta ao largar W ou bater
- Câmera não inclina na parede
- Sem restrição de HP para transformar; o único custo é WP
- Estados de transição são reais, com hitbox e regras próprias
