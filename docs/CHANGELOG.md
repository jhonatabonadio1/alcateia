# Changelog

## Não lançado

### Bots no multiplayer (13/11)
- **A sala online aceita bots.** O dono põe ("+ Bot" em cada time) e
  tira; quem entra na sala toma o lugar de um bot se o time estiver
  cheio; a lista de salas mostra "+N bots".
- **Na partida**, os bots são movidos pela máquina do dono da sala, que
  fala por eles com o servidor; para os outros jogadores são bonecos,
  como qualquer jogador. Atiram, levam dano, morrem, renascem e pontuam
  para o time. Se o dono cai, os bots saem com ele (pendência).
- **Bot não tem patente**, em lugar nenhum (offline também): sem
  insígnia no placar e na sala, "Bot" no cartão de duelo. Não ganha XP
  nem entra na partida gravada no servidor.
- Servidor: `room.bot.add` / `room.bot.remove`, bots na escalação com o
  dono; servidor de partida: pacotes `BotState`, `BotAttack`, `BotHit`,
  `BotDied`, `HitBot` e `Touch`. **Quem roda o servidor no Docker
  precisa reconstruir as duas imagens** (`docker compose up -d --build`).
- Teste de ponta a ponta (`tools/testar_online.sh`) agora joga com um
  bot em cada time.

### Correções do que o Jhon viu jogando (13/11)
- **Salas**: saem as salas de mentira. A aba Salas é a lista online; o
  treino com bots (offline) ficou num botão, "Treino com bots".
- **Trocar o mapa da sala**: o botão dizia "o Hangar é o único mapa";
  agora abre a escolha entre os quatro.
- **Faca e granada** com os modelos do Sketchfab (faca Gerber, granada
  M84), na mão do braço animado da pistola (sem o braço esquerdo), com
  o golpe e o arremesso.
- **Braços em primeira pessoa**:
  - estavam desenhados com a abertura de câmera do jogo (75°), não com a
    do animador (50,5°): ficavam pequenos e no canto. O conjunto agora é
    esticado na razão entre as duas;
  - o cano ficava a um metro do olho e atravessava a parede: o conjunto
    é trazido para perto do olho (45%), com a mesma aparência na tela;
  - a primeira troca para cada arma travava lendo o arquivo: os conjuntos
    são carregados ao entrar na partida;
  - parado e andar repetem sem tranco; quem não tem animação de sacar
    sobe de baixo; pistola, faca e granada um pouco mais altas.
- **Faca com animação própria**: o conjunto "knife animated" (DJMaesen,
  CC BY) que o Jhon trouxe — braços, faca e animações. Atacar alterna a
  estocada e o corte de lado, no tempo entre dois golpes; sacar e parado
  também são do conjunto. Sai a faca Gerber presa no braço da pistola e
  o golpe feito por código.
- **Varredura dos quatro mapas** (`tools/mapas/conferir.py`, novo): lê a
  geometria de cada mapa atrás do que vira defeito na tela. Achou e
  corrigiu:
  - **170 faces brigando** (duas peças de materiais diferentes com uma
    face no mesmo plano — o pisca-pisca nas portas das oficinas e dos
    alojamentos, onde o marco de aço empatava com a parede; tetos e
    peitoris do Metrô, da Vila e da Polar). O kit agora desempata
    sozinho ao gravar: a peça de enfeite cresce 3 mm.
  - **31 letreiros mal assentados**: placas da Polar cortadas pelo friso
    de aço, o número do portão do Hangar atravessado por uma coluna,
    letras das bases do Metrô e da Vila soltas a um palmo da parede. O
    kit assenta cada letreiro 2 cm à frente da superfície que tem atrás.
  - **Polar**: o carrinho e os galões dos alojamentos flutuavam 24 cm
    (ficaram na altura do piso antigo).
  - Os testes de mapa agora conferem, com a física do jogo, que nenhum
    ponto de nascimento fica dentro de parede e que não há buraco no
    chão; e dão até 100 s para o primeiro tiro no mapa grande.
- **Braços reiniciando**: no M16 (a arma com que se nasce) o trecho de
  "parado" pegava o fim do saque, e a cada 0,9 s os braços pulavam 10 cm.
  Trecho corrigido; nos conjuntos de linha do tempo única o parado vai e
  volta em vez de emendar; e a troca entre parado e andar espera 0,18 s
  (perder o chão por um quadro recomeçava a animação). O teste agora
  mede, arma por arma, que parado não há recomeço nem tranco.
- **Personagens tremendo e piscando**: a física anda 60 passos por
  segundo e a tela do Mac no ritmo dela (120 Hz, ou 60 Hz com quadros
  desencontrados). O corpo era desenhado onde o último passo o deixou: a
  120 quadros, o bot só andava em metade deles. Novo `VisualSmoothing`
  desenha corpo, câmera, arma e lobo entre um passo e o outro (jogador,
  bots e bonecos da rede); medido a 120 quadros, a variação da velocidade
  na tela caiu de 106% para 38% (o resto é o bot mudando de rumo). A
  jogabilidade continua lendo a posição da física, e o giro do mouse
  continua na hora.
- **Borda vermelha dos inimigos** numa casca 4 mm para fora da pele: na
  mesma superfície ela brigava com o boneco pelo pixel e podia piscar.
- **Mapas**: os letreiros ficavam a 1 cm da superfície e piscavam de
  longe; agora a 2 cm a mais. Na Polar, o piso dos cômodos novos ficava
  24 cm acima do chão (os pés afundavam). O piso do Hangar estava claro
  demais ao sol.

### Braços animados em primeira pessoa (Sketchfab, CC BY)
- As seis armas de fogo passam a usar os conjuntos animados baixados do
  Sketchfab: braços, arma e animações de animador (parado, sacar, tiro,
  recarga, recarga com pente vazio, andar). M16A2, AK, SG552 (fuzil
  compacto do pacote de fuzis), UMP45 (submetralhadora do pacote), M9
  (pistola do pacote) e M3 (escopeta do pacote Benelli).
- `tools/armas/blender/bracos.py` converte cada .fbx no Blender: olho do
  jogador na origem, escala em metros, materiais com as texturas,
  animações com o nome que o jogo usa. Saída em `assets/armas/fp/` e a
  ficha `bracos.json`.
- `ViewModel.Rig.cs` toca a animação certa: o tiro a cada disparo, a
  recarga na velocidade que termina junto com a recarga de verdade (a
  de pente vazio quando esvaziou; na escopeta, um ciclo por cartucho).
- Faca e granada continuam com as mãos feitas por código.
- Teste `BracosNaMao` reescrito para os braços animados.
- **Não conferido na tela**: o teste passa, mas as fotos não chegaram a
  ser tiradas. Se algum conjunto aparecer torto, apague a entrada dele
  em `assets/armas/fp/bracos.json` e a arma volta às mãos de código.
- Créditos: os autores dos pacotes do Sketchfab.

### Hangar: de caixa cinza a base aérea
- O primeiro mapa passou para o mesmo kit dos outros
  (`tools/mapas/hangar.py`; sai `tools/gerar_hangar.py` e a textura de
  grade). **A colisão é a mesma, peça por peça** — conferida
  automaticamente contra o gerador antigo —, com uma diferença de
  propósito: as quatro oficinas, que eram bloco maciço, agora têm dois
  cômodos, três portas e duas janelas cada. A parede de trás, a do beco
  do lobo, ficou inteira.
- Tijolo de fábrica, piso de concreto, chapa trapezoidal nos galpões em
  volta com fita de janelas, portões de enrolar numerados, contêineres
  com cantoneira, anel pintado e faixas de perigo na plataforma do
  centro, farol com lanterna, céu fotografado.
- Letreiros (HANGAR 7, OFICINA, ALMOXARIFADO, FERRAMENTARIA, PINTURA,
  avisos) e objetos do Poly Haven nas oficinas.

### Mapas: material de verdade, lugares para entrar, trem e letreiros
- **Texturas fotográficas** (Poly Haven, CC0) no lugar das desenhadas por
  código: 44 materiais com cor, relevo e oclusão/aspereza. Tijolo,
  reboco gasto, telha de barro, calçamento, contêiner, neve, brita.
- **Céu fotografado** (HDRI) iluminando a Vila e a Estação Polar.
- **Lugares para entrar**, todos com três entradas ou mais:
  - **Metrô**: uma galeria de lojas atrás de cada plataforma (44 × 8 m),
    com café, banca, sala de controle com janela de tiro e corredor
    atrás dos balcões. O mapa passou de 48 × 30 para 48 × 50 m.
  - **Vila**: as quatro casas grandes agora têm dois cômodos no térreo
    (padaria, mercado, café, barbearia), com porta para a praça, para o
    beco e para a viela, e janelas por onde se atira.
  - **Estação Polar**: a fileira da frente de cada alojamento virou
    laboratório e refeitório, atravessáveis por dentro.
- **Trem de verdade** no Metrô, modelado no Blender: carroceria de inox,
  janelas com borracha, portas de correr abertas, cabine com para-brisa
  e faróis, truques com rodas no trilho, e o salão por dentro (bancos,
  balaústres, corrimão com alças, luminárias).
- **Letreiros com letra de verdade**: placa de estação, saída,
  plataforma, mapa da linha, destino do trem, nome de loja pintado,
  estêncil de galpão, cartazes, pichação, marcação de piso e a letra da
  base — 66 imagens desenhadas por `tools/mapas/letreiros.py` com
  Overpass, Saira Stencil, Alfa Slab, Bebas Neue e outras de uso livre.
- **Objetos de cenário** do Poly Haven (barris, caixotes, estantes,
  carrinho, gerador, sacos de cimento…), convertidos no Blender.
- Kit: `m.parede` (parede com porta e janela), `m.letreiro`, `m.modelo`,
  `P.objeto`. Sai o `Label3D` e sai `tools/gerar_texturas.py`.

### Jogo online
- **Servidor** em .NET 8 + PostgreSQL, no Docker (`docker compose up`):
  contas com senha, sessões, perfil, ranking, salas e chat ao vivo
  (WebSocket), e a frota de servidores de partida. Sem pacote de fora —
  o cliente do PostgreSQL é do projeto (`docs/decisoes/0009`).
- **Tela Online** no menu: criar conta ou entrar, lista de salas, criar
  sala (mapa, times, pontos, tempo, regras, senha), a sala com os dois
  times, pronto/iniciar, chat e ranking.
- **Servidor de partida**: o jogo sem tela (`-- --servidor`) recebe a
  partida do servidor, confere o ingresso de cada jogador, repassa o
  que cada um faz, arbitra os abates (`Referee`) e entrega o resultado,
  que vira XP e ranking na conta.
- **Partida em rede** entre jogadores: os outros aparecem como bonecos
  com o mesmo corpo, arma e animação; tiro, faca, golpe de lobo e
  granada são encenados; dano, morte, renascimento, placar e relógio
  passam pelo servidor. Quem decide o quê: `docs/decisoes/0008`.
- Testes: 22 do servidor contra PostgreSQL de verdade
  (`tools/testar_servidor.sh`), 8 de unidade do juiz, e um de ponta a
  ponta com dois jogos sem tela (`tools/testar_online.sh`).
- Limites desta versão: sem bots online, não é à prova de trapaça, o
  perfil da loja continua local. Tudo em `docs/SERVIDOR.md`.

### Créditos
- **Tela de créditos** (Opções → Créditos) com a origem e a licença de
  cada material de terceiros — o que a CC BY do Oculus Audio Pack
  exigia antes de publicar. O conteúdo fica em `scripts/meta/Credits.cs`.

### Braços e armas em primeira pessoa
- **Duas mãos de luva na arma**, em vez de arma solta: a direita no
  punho, a esquerda no guarda-mão (ou apoiando a pistola, ou em guarda
  com a faca).
- **Recarga animada**: a mão esquerda tira o carregador, some com ele,
  volta com outro, encaixa e — se o pente esvaziou — puxa o ferrolho.
  Na escopeta, um cartucho por vez pela janela de carga; depois de cada
  tiro, a bombeada. A granada tem o pino puxado; a faca corta
  atravessando a tela.
- **Armas redesenhadas** (M16A2, SG552, AK-47, UMP45, M3, M9, faca,
  granada de choque): silhueta de verdade em vez de caixas — alça de
  transporte, carregador curvo, coronha vazada, guarda-mão estriado. O
  mesmo modelo vale na mão dos outros e no chão.
- Tiro: coice com o cano subindo, clarão em estrela com luz, ferrolho
  recuando, cápsula saltando. A arma atrasa ao olhar, afunda no pouso,
  e o balanço do passo cai junto com o som do pé.
- `tools/gerar_armas.py` desenha tudo; teste novo `BracosNaMao`.

### Mapas remodelados
- Metrô, Vila e Estação Polar ganharam uma camada de **acabamento** por
  cima do desenho de jogo (que não mudou — os testes de mapa passam
  iguais): o que era caixa lisa agora é lugar.
  - **Metrô**: abóbada de azulejo com nervuras, colunas redondas com
    capitel, trem de teto curvo com janelas, faróis e balaústres,
    escadas de degrau com corrimão, duto de ventilação, canos e quadros
    no corredor de serviço, anúncios acesos, bilheteria com guichê,
    portões de enrolar.
  - **Vila**: os muros viraram frentes de casa (reboco de várias cores,
    janelas com veneziana e grade, portas em arco, vigas e beiral),
    chafariz torneado, bancas com lona e fruta, varal de luzes, torres
    com sineira e cúpula, carroça, pérgola, palmeiras e minaretes para
    além do muro.
  - **Estação Polar**: paredões de gelo irregulares e serra nevada em
    volta, neve caindo, neve nos telhados e sincelos, janelas acesas,
    tratores de esteira, tanques com cúpula e escada, torre de rádio
    treliçada com estais, canos com flange, refletores.
- `tools/mapas/formas.py` (formas que não são caixa, gravadas como
  .obj) e `tools/mapas/pecas.py` (peças prontas). Rampas podem ser
  desenhadas como escada (`degraus=`); peças podem ser só colisão
  (`oculta=True`).

### Som
- **Passo na passada.** O ruído do passo sai a cada passada do clipe
  (1,19 m o humano, 1,05 m o lobo, 0,97 m de quatro) em vez de a cada
  2,2 m: o som cai com o pé e acelera com o corpo. Os bots ouvem cada
  pisada. Teste novo `PassoNoRitmo` amarra os números aos clipes.
- **Passo conforme o chão**: metal, neve, madeira e terra, para humano e
  lobo. O gerador de mapas marca cada peça pelo material.
- **Tiros com variação**: de duas a quatro gravações por arma em vez de
  uma, e o seu tiro é outra gravação (estéreo, mais grave).
- Bala estalando na parede (concreto e metal) e a cápsula do seu tiro no
  chão.
- **O som do lugar**: cada mapa tem reverberação e fundo próprios — a
  estação ecoa e tem trem passando no túnel ao lado, a neve abafa e
  venta. Limitador na saída: tiroteio cheio não estoura.
- Teste novo `SomDoMapa`.

### Arte de verdade a caminho
- `tools/baixar_pacotes.py`: um comando baixa do Poly Haven (CC0) as
  texturas fotografadas, os objetos de cenário e os céus dos mapas para
  `assets/pacotes/` (fora do git e do Godot).
- `docs/PACOTES.md`: a lista do que baixar, de onde, com que licença —
  incluindo os braços em primeira pessoa e as armas do Sketchfab
  (CC BY, baixados à mão).

## v0.28.0 — 2026-11-13 (o jogo profissional, parte 1: mapas)

### Adicionado
- **Três mapas novos**, de temas e tamanhos diferentes:
  - **Metrô** (pequeno, 48 × 30 m): estação fechada com um trem parado
    no meio, galerias a 5 m e corredores de serviço;
  - **Vila** (médio, 72 × 52 m): vila de deserto no fim da tarde, com
    praça, chafariz, feira, terraços e vielas;
  - **Estação Polar** (grande, 110 × 70 m): base de pesquisa na neve,
    com garagem atravessável, alojamentos, passarelas e tanques.
- **16 texturas com relevo e aspereza**, desenhadas por código
  (`tools/gerar_texturas.py`), sem arquivo de terceiros.
- **Céu, neblina, sol e reflexos por mapa.**
- **A partida carrega o mapa da sala** (`MapSlot`); antes o Hangar
  estava preso na cena.
- **Gerador de mapas** (`tools/gerar_mapas.py` e o kit em
  `tools/mapas/`) e o guia `docs/MAPAS.md`.
- Testes `MapaHangar`, `MapaMetro`, `MapaVila`, `MapaPolar`.

### Mudado
- **Fase 1 fechada** (decisão do Jhon). A "Regra zero" do `CLAUDE.md`
  agora é: rede fora das regras de jogo.

## v0.27.1 — 2026-11-13

### Mudado
- **Salto carregado 30% mais curto no fim da barra**: cheio, voa 8,4 m
  (era 12). Sem carga continua 1,5 m. O bot só tenta o salto até 7,4 m
  do alvo (era 11), para não cair longe dele.
- **Lobo 10% mais rápido na parede**: Bruto 4,45 m/s (era 4,05),
  Ligeiro 4,82 (era 4,38). O salto da parede leva 80% disso, então
  sobe junto.
- **Humano 8% mais rápido**: 3,24 m/s com o fuzil (era 3,0), agachado
  1,46, com Shift 1,36. O lobo não muda (`HumanSpeedBonus`, só no
  humano). Os bots humanos seguem a velocidade da dificuldade.
- **Com a faca, 12% mais rápido**: 3,36 m/s (era 3,0).

### Adicionado
- **Corrida de quatro do lobo**: W duas vezes (segurando no segundo) e
  o lobo cai nas quatro patas, 30% mais rápido (Bruto 5,06 m/s, Ligeiro
  5,48), tocando o "Running Crawl". Soltar o W ou agachar volta a
  correr em pé. Só o jogador; os bots ainda não usam.
- **C também agacha.**
- **Velocidade fixa da animação do lobo**, se quiser: `WalkAnimSpeed`,
  `SprintAnimSpeed` e `WallAnimSpeed` no nó `WolfVisual` (1 = normal).
  Em 0 (padrão) a animação acompanha o corpo, como antes.

### Corrigido
- **Mac: agachar no Ctrl e clicar não atirava.** O macOS (pelo Godot)
  entrega Ctrl + clique esquerdo como clique direito; o humano não
  atirava e o lobo carregava o salto em vez de arranhar. Agora o
  clique direito com Ctrl vira ataque no Mac; o especial agachado sai
  agachando no C.
- **O andar de quatro (engatinhar) patina menos**: usava a passada da
  parede (2,3 m/s); a medida no chão é 2,9 (`CrawlClipSpeed`).

## v0.27.0 — 2026-11-12 (organização da casa)

Série de commits que reorganiza o código sem mudar o jogo. Os testes
desta versão provam que o comportamento continua o aprovado; as poucas
diferenças de comportamento estão em "Mudado", com o porquê.

### Adicionado
- **Testes automatizados** (`tools/testar.sh`, ~1,5 min):
  - 33 de unidade (`tests/unit`): C# puro, sem Godot e sem pacote —
    contas do combate, tempo dos golpes, sorteio com semente;
  - 21 de regressão (`tests/regression`): o jogo de verdade, sem tela —
    todas as telas do lobby, salto carregado em 8 situações, velocidades no chão, altura do
    pulo, corrida na parede, salto da parede, agarrar na parede,
    passada do lobo, granada, dano da garra, ruído, níveis dos bots,
    clipes de animação, HUD, renascer humano, bots atirando, lobos
    usando especiais, cenas abrindo sem erro.
- **Testes no GitHub a cada push** (`.github/workflows/testes.yml`).
- **Detector de código repetido** (`tools/duplicados.py`), no
  `testar.sh`: seis linhas iguais em dois lugares reprovam.
- **Semente da partida**: a partida diz a semente no Output, e
  `-- --seed=N` repete a sorte (mira dos bots, dispersão, rotas).
- **Documentação**: `CONTRIBUTING.md` (o passo a passo),
  `docs/DESIGN.md` (as decisões de jogo, que saíram do `CLAUDE.md`) e
  `docs/decisoes/` (os registros de arquitetura, ADR 0001 a 0007).

### Mudado
- **Código dividido por assunto** (classes parciais): `Player`, `Bot`,
  `Weapon`, `CharacterAnimator`, `WolfVisual` e `Hud`, cada um numa
  pasta com um arquivo por responsabilidade. No Inspector os campos
  aparecem em grupos. Nenhuma linha de código mudou nessas divisões.
- **Jogador e bot dividem o corpo** (`scripts/actors/body`): o bote, o
  voo do salto, o lobo preso ao chão, o ruído e o cadáver eram cópias.
  Onde as cópias tinham divergido, valeu a regra do jogador:
  - o bote do bot só bate em parede quando vai de encontro a ela;
  - o pouso do salto do bot faz o baque (os outros ouvem);
  - o rugido faz ruído já no começo, sempre.
- **Ruído do corpo num arquivo só** (`resources/body/ruido.tres`), no
  lugar dos cinco campos repetidos no jogador e no bot.
- **Níveis dos bots viram dado** (`resources/bots/*.tres`), com o nome
  e a linha do menu junto.
- **Contas do combate em C# puro** (`scripts/domain/CombatRules.cs`):
  dano, queda por distância, dispersão, recuo, garra, salto, escudo.
- **O jogador lê o teclado uma vez por quadro** (`CharacterIntent`).
- **Sorteio de regra com semente** (`GameRandom`), um fluxo por bot e
  por arma, no lugar do sorteio global do Godot.
- **Ferramentas de animação num lugar só** (`AnimationKit`), que o
  humano, o lobo e o lobby usavam em cópias.
- **Corrida na parede em passos com nome** (o método tinha 286 linhas).
- **O resto do código repetido juntado**: o id do que está na mão
  (`Weapon.HeldId`, que a arma na tela, a do corpo e o HUD calculavam
  cada um), os ids da faca e da granada (`Arsenal.KnifeId`, `GrenadeId`),
  a pintura do time (`MaterialTint`), os nomes de grupo (`Groups`), a
  busca na árvore (`Nodes`), e no lobby o cartão, a grade com rolagem,
  a linha rótulo/valor, a miniatura do mapa, a página e o botão do
  header (`UiKit`, `LobbyScreen`).
- `CLAUDE.md` com 390 linhas (era 1850): as regras ficam, as decisões
  de cada sistema foram para o `docs/DESIGN.md`.
- `WolfVisual` saiu de `scripts/ui` para `scripts/visual/wolf`.
- `Player.WallDebug` vem desligado.
- **Indentação com tab em todo o C#**, fixada no `.editorconfig`.
- **Bruto: dano 75, alcance 5, sem espera entre garradas** (os valores
  com que o Jhon joga).

### Removido
- Cópias velhas de cenas e do Bruto na raiz do projeto.
- `WolfBreed.Hp` e `WolfBreed.AttackCooldown`, que nada usava.

## v0.26.12 — 2026-11-12

### Adicionado
- **Guia de ajustes** (`docs/AJUSTES.md`): todo número regulável do
  jogo — movimento, parede, classes de lobo, golpes, armas, granada,
  vida, WP, bots, partida, som e animação —, com o valor atual, onde
  mudar e o que acontece.

## v0.26.11 — 2026-11-12

### Mudado
- **Lobo 3% mais rápido no chão** (Bruto 3,89 m/s, Ligeiro 4,22) e
  **7% mais rápido na parede** (Bruto 4,05, Ligeiro 4,38). O salto da
  parede leva 80% da velocidade dela e sobe junto.

## v0.26.10 — 2026-11-12

### Adicionado
- **Lobo agachado de verdade:** parado, encolhe (0,98 m, a altura da
  cápsula agachada); andando, engatinha. Antes ele continuava em pé
  com a cabeça fora da própria cápsula.
- **Humano agachado mira com o fuzil no ombro**, como de pé (antes o
  tiro saía com a arma baixa, na cintura).
- **Granada armada:** segurando o botão, o braço vai para trás (na mão
  da tela e no corpo que os outros veem); soltou, arremessa.

### Mudado
- **Salto carregado com distância fixa pela barra** (1,5 a 12 m), com
  ou sem inimigo na frente, olhando para onde for: sai na direção da
  mira e não persegue ninguém no voo. Só parede e bloco encurtam.
- **Lobo 5% mais rápido** no chão e na parede (Bruto 3,78 m/s, Ligeiro
  4,1).
- **Granada mais forte:** olhando reto cai a ~16 m (era 11).
- **Lobo vira para onde anda:** de lado ele vira e anda de frente; para
  trás fica de frente e o passo toca ao contrário. A patinação dos pés
  caiu de 5,4 para 0,85 m/s de lado e de 7,3 para 0,72 para trás.
- **Lobo parando:** o passo congela onde parou e as pernas se juntam em
  0,2 s; antes ele corria no lugar mais um quarto de segundo.

### Corrigido
- **Renascer sempre como humano**, mesmo morrendo de lobo.
- **Salto que mudava sozinho com inimigo na frente**: ia até o inimigo
  e passava do alcance da barra.

## v0.26.9 — 2026-11-12

### Adicionado
- **Salto do lobo carregado:** segurar o botão direito no chão carrega
  em 1 s; mais carga, mais longe (1,5 a 12 m, até o ponto mirado) e
  mais dano (40% a 100%). Barra de força na tela.
- **Granada: segurar prepara, soltar arremessa**, na mira de quando
  solta.
- **Cartão do duelo** ao matar ou morrer: patentes, nomes e quantas
  vezes cada um matou o outro.
- **Inimigos com a borda levemente vermelha** (só onde estão à vista).
- **Cartão sobre o matador** na câmera de morte, com patente e nome.
- **Lobos dos bots usam o salto carregado e o pulo com bote**, mais ou
  menos conforme a dificuldade.

### Mudado
- **Humano a 3,0 m/s, na passada da animação** (era 4,2): correndo, o
  pé não patina mais, e a animação não acelera. Os lobos continuam a
  3,6 e 3,9; os bots seguem a mesma conta (2,5 / 3,0 / 3,5).

### Corrigido
- **O strafe do humano tocava ao contrário**: o pé andava junto com o
  corpo.

## v0.26.8 — 2026-11-12

### Adicionado
- **Quem te matou fica com a silhueta vermelha** (só para você, através
  das paredes) **e a câmera vira para ele**, aproximando se estiver
  longe. A escolha de conjunto abre 1,8 s depois.

### Mudado
- **Lobo mais lento a pé, com a corrida fluida:** Bruto a 3,6 m/s (era
  5,7) e Ligeiro a 3,9 (era 6,1), também na parede. A corrida toca a
  1,5× em vez de 2,4×.
- **Pulo do lobo 5% mais alto** (Bruto 1,62 m); **salto da parede 40%
  mais baixo**.
- **Especial no ar avança 20% menos**, e metade depois de um salto da
  parede.
- **No ar o lobo cai, não anda:** pose de queda ao soltar ou saltar da
  parede e ao cair de beirada.
- **Morrer custa 100 WP.**
- **O tiro do inimigo se ouve de longe**, vindo de onde ele está.
- **Soltar a arma agora é G** (era B).

### Corrigido
- **Bots paravam de atirar:** indo para a parede ou procurando, eles
  viam o alvo e não atiravam — dois bots ficavam meio minuto se
  encarando. Agora atiram em qualquer ação que não seja recarregar ou
  recuar.
- **O lobo morto ficava meio segundo de pé** antes de cair.

## v0.26.7 — 2026-11-12

### Mudado
- **Na quina, o lobo contorna e continua pela parede** em vez de cair:
  bloco, contêiner, pilar e coluna fina, na mesma altura e no mesmo
  passo.
- **O salto (especial no chão) cai no ponto do clique:** segurar W não
  aumenta mais a distância, e virar a câmera no agachar não muda nada.
- **O bote cai mais devagar:** 13 m/s (eram 17), com o mesmo alcance.

### Corrigido
- **O lobo andava mais rápido que os passos:** o clipe de correr tocava
  a 1× e o pé patinava. Agora toca no ritmo do corpo (2,36× no Bruto
  correndo) e o pé apoiado fica parado no chão, correndo e agachado.

## v0.26.6 — 2026-11-12

### Adicionado
- **Animação de pulo do lobo** ("mutant jumping"): o voo acompanha o
  pulo de verdade — saindo do chão, no topo, caindo —, e chegando
  devagar o lobo aterrissa agachando; chegando correndo, já sai
  correndo. Vale para o pulo do chão e o da parede.

## v0.26.5 — 2026-11-12

### Mudado
- **Na quina, o lobo segue reto.** A corrida na parede acaba na ponta e
  o lobo continua no ar, no mesmo passo. Antes ele contornava e, numa
  parede fina, voltava pelo outro lado.
- **No canto, vira e continua** pela parede da frente, no mesmo passo.
- **Pulo do lobo mais curto e um pouco mais alto:** o pulo do chão leva
  55% do passo (o Bruto avança 3,7 m; eram uns 6) e sobe 1,54 m no
  Bruto (eram 1,31) e 2,38 m no Ligeiro (eram 2,22). O salto da parede
  não muda.
- **Bote em todo pulo:** sem a recarga de 2 s. Acabou a aterrissagem, o
  próximo pulo já tem outro, e pular só depois que ela acaba.
- **Pulo e especial juntos viram bote**, não o salto do chão. O bote sai
  em qualquer ponto do pulo, até na subida.
- **O lobo ruge em movimento:** anda, pula e cai rugindo, sem frear. O
  rugido toca da cintura para cima.
- **Virar lobo não tem mais animação.**

### Corrigido
- **O lobo voltava pela parede ao chegar num canto:** soltava e
  regrudava na parede de trás, no sentido contrário.

## v0.26.4 — 2026-11-12

### Mudado
- **Salto do lobo em arco, passando por cima de obstáculos:** o salto
  mede o chão no caminho e sobe o bastante para passar por cima do que
  tiver até 2,6 m (caixote, contêiner). Mirando além de um contêiner
  perto, cai em cima dele; diante de algo mais alto (bloco, muro), cai
  antes, nunca dentro.
- **W + botão direito avança:** segurando W, o salto anda pelo menos
  6 m, mesmo mirando o chão perto.
- **O salto acompanha o alvo:** o ponto de queda segue quem você mirou
  a 3 m/s durante o voo. Quem só caminha não escapa mais, e quem corre
  de lado desde o começo ainda escapa.
- **Rugido segurando R, em 2,0 s** (eram 2,88): soltar antes cancela
  sem devolver o escudo.

### Corrigido
- **O lobo subia depois de grudar na parede.** Agora ele só gruda no
  alto do pulo e fica na altura em que grudou.
- **Trancos ao contornar quina e coluna fina.** A parede agora é o
  ponto mais próximo dela, não o que um leque de raios acerta: a
  normal gira contínua em volta da quina, e o lobo dá a volta inteira
  numa coluna de 30 cm sem soltar, na mesma altura.

## v0.26.3 — 2026-11-12

### Mudado
- **Salto do lobo de longe:** até 12 m (eram 5), para apertar de longe
  e cair em cima do humano. Um inimigo a até 10° do centro da tela vira
  o alvo — o lobo não tem mira, e acertar um corpo a 12 m só com o
  centro da câmera seria loteria.
- **Garradas respondem mais rápido:** a garra chega 0,14 s depois do
  clique (eram 0,23 e 0,16) e cada golpe dura 0,39 s (eram 0,52). A
  garrada começa com a mão já recolhida.
- **Lobo mais lento:** Bruto a 5,7 m/s (1,35×) e Ligeiro a 6,1 m/s
  (1,45×). O pulo fica mais curto pela velocidade; a altura não muda,
  porque está amarrada ao mapa.
- **Corrida na parede mais lenta e controlada:** sem bônus de
  velocidade (5,7 m/s no Bruto; eram 8,1), acelerando ao grudar e
  freando em meio metro ao agarrar.

### Corrigido
- **Humanos flutuavam 6 a 8 cm acima do chão.** Os clipes são do X Bot,
  que tem perna mais longa que o Exo Gray e a Ely; a trilha do quadril
  agora é ajustada à altura de cada modelo.

## v0.26.2 — 2026-11-12

### Mudado
- **O salto no chão avança até a mira:** do impulso até os pés tocarem
  o chão, o lobo anda até as garras caírem no ponto para onde a câmera
  aponta — em cima de quem você mirou, até 5 m. Antes ele saltava no
  lugar.
- **Garradas mais rápidas e respondendo ao clique:** 0,52 s por golpe
  (eram 0,69 e 0,71), garra chegando em 0,23 s e 0,16 s. O clique dado
  no fim de um golpe fica guardado e o próximo sai no quadro em que ele
  acaba — antes ele se perdia.
- **O mestre da sala não marca pronto.** Ele inicia com todos prontos,
  ou com os prontos já em times equilibrados (um lado pode ter um a
  menos). Quem não estava pronto fica na sala.
- **Sem mira na forma lobo.** Em terceira pessoa o próprio lobo no meio
  da tela já diz para onde ele vai.

## v0.26.1 — 2026-11-12

### Mudado
- **Rugido em 2,9 s** (era 3,6): corta o começo e o fim parados do
  clipe e toca um pouco mais rápido. O escudo continua voltando só no
  fim.
- **Bote (direito no ar) refeito:** mergulho em linha reta na direção
  da mira, a 17 m/s, sem gravidade nem freio no meio — o ponto de
  impacto é escolhido no clique. O golpe sai no instante em que o lobo
  encosta (chão, parede ou alguém), não num tempo marcado: um golpe só,
  3×, num volume um pouco maior que o do salto. Aterrissa preso por
  0,78 s; recarga de 2 s.
- **Salto (direito no chão):** toca o "jump attack" inteiro com o lobo
  preso no lugar, e o dano sai só quando ele cai, 1,14 s depois do
  clique. Substitui a patada pesada.
- **Golpes fluidos**, medidos quadro a quadro na velocidade dos ossos:
  - a garrada entra em 0,15 s, e os braços não voam mais até a pose
    inicial;
  - parado, a garrada dá um passo em sino (0,91 m, como o do clipe; o
    punch, 0,35 m) e as pernas andam junto — antes o empurrão
    instantâneo jogava os pés na pose de correr de uma vez;
  - a queda do bote acelera para o impacto da animação coincidir com o
    impacto de verdade, e a aterrissagem entra por cima dela, sem
    tranco;
  - no bote o lobo tomba para a frente na inclinação do mergulho;
  - preso no lugar (salto, rugido, aterrissagem), o lobo freia deslizando
    em vez de parar seco.
- `tools/analisar_golpes.gd` também mede o passo do quadril, o quadro
  em que os pés voltam ao chão e o trecho do clipe com movimento.

## v0.26.0 — 2026-11-12

### Adicionado
- **Lobo correndo na parede** com o Running Crawl, deitado de lado com
  a barriga virada para a parede em que está (pela normal dela). Entra
  e sai suave; parado agarrado, o clipe para.
- **`WolfMoves`**: tabela única com trecho, velocidade e instante do
  golpe de cada animação do lobo, medidos nas mãos do esqueleto.
- `tools/analisar_golpes.gd`: mede de novo os golpes quando um clipe
  for trocado.

### Mudado
- **Golpes do lobo sincronizados com a animação:** o dano sai no quadro
  em que a garra chega (garrada 0,31 s e 0,21 s, patada 0,30 s, bote
  0,50 s depois do clique), e o próximo golpe só sai quando a animação
  acaba. Golpe não corta golpe, nem para voltar a humano.
- **A garrada alterna os braços na arma**, não só na animação: o combo
  continua (o terceiro golpe machuca mais).
- **Rugido inteiro:** um toque em R começa, a animação vai até o fim
  (3,6 s) e só então o escudo volta cheio. Soltar não cancela; parado e
  sem atacar o tempo todo. Bots seguem a mesma regra (antes recuperavam
  aos poucos).
- O som da garra sai no instante do golpe.

### Corrigido
- **O jogo abortava ao fechar depois de uma partida longa**
  ("script_bindings is not empty"). Os objetos que o C# monta (animações,
  árvores de animação) ficavam na fila do coletor, e a fila rodava
  depois de o C# já ter desligado. O autoload `Saida` esvazia a fila
  depois que a cena some. Em 25 saídas de teste: 7 abortos antes, 0
  depois.
- O HUD não monta estilo novo a cada quadro, e o golpe do lobo espera
  numa fila da arma em vez de um timer novo por golpe: menos lixo para
  o coletor durante a partida.

## v0.25.1 — 2026-11-12

### Mudado
- **Garrada do lobo alterna os golpes:** cada clique esquerdo usa um
  braço — o "swiping" e o "punch" do pack, um de cada vez.
- **Bote no ar toca só a queda** do "jump attack", do ponto mais alto
  ao impacto. O agachar e o salto do clipe saíram: o bote começa com o
  lobo já no ar.
- **O lobo não tem mais animação de pulo:** ele só pula, e o corpo
  segue na pose em que estava. O clipe `jump.fbx` saiu.

## v0.25.0 — 2026-11-12

### Adicionado
- **A arma escolhida na mão.** Cada arma tem modelo 3D próprio
  (`WeaponModel`): M16A2 com a alça, AK-47 com madeira e carregador
  curvo, SG-552 compacta, UMP-45 quadrada, M3 com o tubo, M9, faca e
  granada. Aparece na sua mão, na dos outros e no chão.
- **Recarga animada.** Primeira pessoa: a arma inclina, o carregador
  sai e volta, e ela endireita; a escopeta empurra cartucho por
  cartucho. Terceira pessoa: o clipe `reload` só da cintura para cima,
  sem parar as pernas. As duas seguem o tempo de recarga da arma.
- **Os outros seguram a arma deles** (`HeldWeapon`), presa na mão
  direita e apontando para a esquerda; troca junto com o slot.
- **Soltar e pegar armas, como no CS.** B joga a arma da mão para
  frente e passa para a próxima. Morto em forma humana deixa a arma
  cair. Passar por cima pega, se o espaço da arma estiver vazio. A arma
  no chão some em 30 s; no máximo 20 no mapa.
- **Personagens por time:** Exo Gray no azul e Ely (K. Atienza) no
  laranja, no lugar do boneco xbot.
- **Lobo de verdade:** o Mutant do Mixamo (um por time, o vermelho no
  laranja), animado com o Creature Pack — parado, andar, correr, pulo,
  garrada, patada, bote, rugido no R, transformação e morte.
- A vitrine da loja e do inventário mostra o Exo Gray e o Mutant.
- **Sair do jogo** no canto direito do topo do lobby, com confirmação.
- HUD: "Pegou: AK-47" e "Soltou: AK-47"; o slot sem arma fica apagado.

### Mudado
- **O uivo (R) toca o mesmo som da transformação**, decisão do Jhon.
  O uivo sintetizado (`lobo_uivo`) saiu.
- Tecla B: soltar a arma. O Enter de recomeçar saiu do mapa de teclas.

### Corrigido
- O soldado aparecia por dentro do lobo: o corpo humano agora some na
  forma lobo (`CharacterAnimator.HideInWolfForm` ligado).
- Aviso do Godot 4.7 sobre ponto de mistura sem nome nas animações.

## v0.24.0 — 2026-11-12

Interface nova inteira, seguindo o guia `docs/UI.md`. Escopo combinado:
tudo o que roda sem servidor.

### Adicionado
- **Kit de interface** (`scripts/ui/kit/`): tokens de design (grade de
  8, paleta, raios, tempos), tema feito em código, fontes Barlow e
  Barlow Condensed com números tabulares (OFL, `assets/fonts/OFL.txt`)
  e componentes: botões em seis variações, ícones desenhados, insígnia
  de patente, moeda que conta ao mudar, barras de atributo, silhuetas
  de arma, abas, modal, contagem em círculo, ping, avisos.
- **Lobby** (`scripts/lobby/`, `menu.tscn`): moldura fixa com header
  (identidade clicável, navegação, missões prontas, Presas e Sangue) e
  miolo que troca. Telas: entrada com apelido e novidades, lista de
  salas (2 × 5 por página, filtros, ir para a sala nº, atualização a
  cada 5 s), sala, loja, inventário com editor de conjuntos e prévia
  do personagem, perfil, missões e opções.
- **Sala**: modo, jogadores por time, objetivo, tempo, armas,
  transformação, dificuldade, faixa de patente, senha, fogo amigo;
  encher com bots, trocar de lado, chat, Pronto e Iniciar com contagem
  de 5 s cancelável. Modos e regras que dependem de servidor aparecem
  como "em breve".
- **Regras da sala valendo na partida** (`RoomRules`): fogo amigo, "Sem
  pesadas", "Só pistolas" (inclusive nos bots) e lobo bloqueado.
- **Perfil v2** (`user://perfil.json`, migra o antigo): patente, XP,
  Presas, Sangue, inventário com prazo, raça, tatuagens, estatísticas
  vitalícias, missões, títulos e histórico de patentes.
- **30 patentes** em 10 famílias (`meta/Ranks.cs`), promoção com
  recompensa e tela própria.
- **Loja** (`meta/Catalog.cs`): armas, granada, tatuagens e troca de
  nome, com prazos de 1, 7, 30 e 90 dias ou permanente, itens do dia
  com desconto, patente mínima e confirmação de compra.
- **Missões**: três diárias e cinco semanais sorteadas pela data, e
  permanentes que dão título. Resgate manual.
- **Recompensa de partida** (`meta/Progression.cs`): XP e Presas por
  jogar, abate, assistência, vitória ou empate e MVP. Abandonar não
  rende nada.
- **Opções** (`user://opcoes.json`): volume geral, efeitos, interface e
  música, sensibilidade do mouse. Também no Esc da partida.
- **HUD de combate novo** (`Hud.cs`), pela regra dos cantos: minimapa
  e nome do mapa em cima à esquerda; feed com ícone da arma em cima à
  direita; forma, escudo e vida embaixo à esquerda; WP e o painel da
  forma embaixo à direita; barra de transformação no centro. O painel
  troca entre humano (arma, pente `/ ∞`, granadas, slots) e lobo
  (recargas de garrada, patada, bote e uivo).
- Mira com abertura dinâmica (`Crosshair.cs`), marcador de acerto
  maior e vermelho na cabeça e no abate (`HitMarker.cs`), vida baixa
  pulsando com vinheta vermelha, barra de transformação que pulsa
  enquanto canaliza.
- **Placar do Tab** (`Scoreboard.cs`): patente, jogador, abates,
  mortes, assistências, pontos, forma e ping; você destacado, morto
  esmaecido, sequência de abates ao lado do nome.
- **Tela de resultado** (`ResultScreen.cs`): veredito sozinho por 1,5 s,
  placar com o MVP, recompensa animada (linhas, barra de patente, Presas
  contando, missões concluídas, promoção) e Jogar de novo / Voltar à
  sala / Sair para o lobby.
- Tela de respawn com o kit: motivo de cada conjunto bloqueado, causa
  da morte ("ABATIDO por X com Y"), contagem em círculo.
- Esc com Opções e Controles; reiniciar e sair pedem confirmação.
- Miniatura do mapa (`tools/gerar_miniaturas.gd`).
- `Weapon.AttackCooldown`, `FormController.ShiftLeft/ShiftTotal`,
  `KillFeed.HowDied`; o abate leva a arma e os assistentes.
- Atalhos de desenvolvimento: `-- --duracao=30 --contagem=2 --placar`
  na partida, `-- --tela=loja --foco=ak47` no lobby.

### Mudado
- **Times: Azul contra Laranja** (era Vermelho). Corpo, base B do
  Hangar, barras e placar seguem a cor; barra de vida sempre na cor do
  time.
- Partida termina na tela de resultado; não há mais Enter para
  recomeçar.
- A pontuação do placar: abate 10, assistência 5.
- Dano direcional dura 1,5 s.

### Removido
- `scripts/menu/MainMenu.cs`, substituído pelo lobby.

### Corrigido
- Recriar as linhas do placar quatro vezes por segundo derrubava o
  jogo ao fechar; agora as linhas são reaproveitadas.
- Item de arma sem arquivo `.tres` (faca) não gera mais erro no
  `Arsenal.Get`.

### Fora do escopo (precisa de servidor)
- Login, canais, amigos, matilha, chat entre jogadores, ranking,
  compra paga, temporadas e moderação.


## v0.23.0 — 2026-11-12

### Adicionado
- **130 arquivos de som** em `assets/audio/`, cobrindo toda a lista:
  passos e pousos (humano e lobo), tiro de perto e de longe para cada
  uma das seis armas, recarga com pente saindo e entrando no tempo de
  cada arma, fim de recarga por arma, cartucho a cartucho da M3,
  bombeada depois do tiro da M3, transformação nas duas direções,
  garra, bote, faca, granada, dor, morte, marcador de acerto, abate,
  contagem, vitória, derrota, interface e ambiente do menu.
- Fontes: Free Firearm Sound Library (CC0), Kenney (CC0) e Oculus Audio
  Pack 1 (CC BY 4.0). Origem de cada arquivo em
  `assets/audio/CREDITOS.md`.
- `tools/sons/baixar.py` e `tools/sons/processar.py`: regeneram tudo a
  partir das gravações originais.
- Fim de recarga por arma (`recarga_fim_<id>`).

### Pendente
- `lobo_uivo` é sintetizado, provisório.
- O Oculus Audio Pack exige crédito na tela de créditos antes de
  publicar.


## v0.22.0 — 2026-11-12

### Adicionado
- **Sistema de som**, pronto para receber arquivos. Cada arquivo com o
  nome certo em `assets/audio/` passa a tocar; o que falta fica mudo.
  Lista em `assets/audio/LEIA-ME.md`.
- `Sfx`: toca por nome, sorteia variações (`passo_1`, `passo_2`...) com
  tom variando, limita vozes simultâneas e cria os canais SFX,
  Interface e Musica.
- `WorldAudio`: passo, pouso, tiro (um por arma, versão distante),
  uivo e explosão, tocados a partir do NoiseBus — o mesmo ruído que os
  bots ouvem. Contagem, início e fim de partida, confirmação de abate.
- `CombatantAudio`: transformação, garra, bote, faca, recarga,
  cartucho, troca de arma, granada, pulo, dor, morte, marcador de
  acerto. Alcance de 12 m para os outros.
- Sons de interface nos menus e música do menu principal.

### Alterado
- **Bots emitem ruído** de passo, pouso e uivo, como o jogador. Antes
  andavam em silêncio. Bots de times opostos passam a se ouvir.


## v0.21.0 — 2026-11-12

### Adicionado
- **Menu principal** como cena de entrada: início, sala, conjuntos e
  inventário.
- **Sala**: times Azul e Vermelho até 8 cada, adicionar e remover bots,
  pontos, tempo e dificuldade; cada um aperta Pronto e o mestre inicia.
- **Arsenal, primeira leva** (`docs/ARSENAL.md`): M16A2, SG-552, AK-47,
  UMP-45, M3 e M9, com dispersão, recuo em fases, queda por distância,
  chumbos e recarga cartucho a cartucho interrompível.
- **Conjuntos** (`docs/CONJUNTOS.md`): quatro salvos no perfil, padrão
  grátis que rende +50 WP, editor com validação, inventário.
- **Tela de respawn** do documento: WP disponível, custo, "faltam X",
  5 s, débito no renascer.
- **Assistência**: +35 WP para quem acertou nos 10 s antes da morte.
- **Dano direcional**: arco que aponta para o atirador e gira com a
  câmera.
- **Menu do Esc sem pausa**: continuar, reiniciar, voltar ao menu,
  sair.

### Alterado
- Economia do documento: abate 100, abate de lobo 70, teto 2500. Dano
  deixa de render WP.
- Minimapa desenha o mapa inteiro com as cores dele e as letras das
  bases.
- Bots da partida nascem da sala (`MatchSetup`), distribuídos pelos
  spawns da base.

### Removido
- Mapa Arrival e a cena de referência.
- O `Loadout` antigo e os quatro lotes `.tres`.

### Corrigido
- Mira não descia depois do recuo: a volta somava ao ângulo em vez de
  subtrair. Agora desce 0,12 s após o último tiro, na velocidade de
  recuperação da arma, descontando o que o jogador puxou com o mouse.
- Corpo morto atravessava o mapa: sem colisão, `IsOnFloor()` dava
  falso e a gravidade voltava. Assentado, o corpo para de vez.

## v0.20.0 — 2026-11-12

### Adicionado
- **Mapa Hangar**, próprio, no espírito do Arrival: 84 × 48 m,
  simétrico, base A (azul) e base B (vermelha), heliponto central,
  quatro blocos com telhado, duas pontes a 5 m, becos de 3 m para a
  corrida em parede. Gerado por `tools/gerar_hangar.py`.
- **Partida**: mata-mata em equipe até 40 abates, contagem de 10 s,
  10 minutos, tela de vitória/derrota/empate com destaque, Enter
  recomeça.
- **HUD da partida**: placar e relógio no topo com barra até a
  vitória, contagem no centro, tabela por jogador no Tab.
- **Times**: você e três bots azuis contra quatro vermelhos, cada um
  com nome. Corpo pintado e barra de vida na cor do time.
- **Spawns por base**, longe do inimigo vivo mais próximo.
- Textura de grade em espaço de mundo para a caixa cinza.

### Alterado
- Bots escolhem alvo entre os inimigos vivos, preferindo quem está à
  vista, em vez de mirar sempre no jogador. Ignoram passo de aliado.
- Menu de dificuldade foi do Tab para o Esc; Tab é o placar.
- Arrival saiu da partida e ficou em `scenes/arrival_referencia.tscn`.

### Corrigido
- Granada feria aliado e o abate dela não contava para ninguém.
- Bots tinham nome vazio no feed: `DisplayName` vazio não é nulo, e o
  `??` que devia cair no nome do nó nunca caía.
- Esc soltava o mouse por conta própria no Player, brigando com o
  `UiState` pelo cursor.

## v0.19.1 — 2026-11-11

### Corrigido
- **Metade das paredes do Arrival não existia para a física.** O mapa
  é feito de planos de uma face e 53% das faces verticais na altura de
  jogo apontam para fora da arena; `ConcavePolygonShape3D` nasce sem
  `BackfaceCollision`, então o jogador via parede e o raio atravessava.
  O `MapLoader` agora monta a forma à mão com colisão de dois lados.
  Isso conserta de uma vez o lobo não grudando, o bot vendo e atirando
  através de parede, o bot não achando cobertura e o tiro atravessando.
- **Normal de raycast orientada contra o raio** no `Cast` da corrida em
  parede: com colisão de dois lados a normal crua do verso inverteria a
  força que segura o lobo, empurrando-o para dentro da parede.

### Alterado
- Log da corrida em parede religado. Ele só imprime quando o motivo
  muda, então não enche o Output.

## v0.19.0 — 2026-11-11

### Adicionado
- **Mapa Arrival** carregado em `scenes/main.tscn`: 82 × 44 m de área
  andável, telhados e passarelas de 9 a 17 m, 5.213 triângulos. O
  `MapLoader` gera a colisão de malha, coleta spawns e assa a
  navegação em tempo de execução.
- **`MapOffset`** no `MapLoader`: o piso do Arrival vem em Y = 7,5 e é
  rebaixado para 0 na entrada, em vez de corrigido no Blender.
- **`NavBounds`**: caixa de bake da navegação. Sem ela o Godot varre
  os 300 m de altura das cortinas do mapa para achar um piso que está
  nos três primeiros metros.
- **`SpawnPoints`**: oito pontos de nascimento, medidos no arquivo por
  folga em volta e teto livre. Nasce-se no mais longe do inimigo vivo
  mais próximo, com ruído na escolha.
- **`scenes/treino.tscn`**: a caixa cinza de antes, preservada para
  medir física sem o mapa no caminho.

### Alterado
- Cinco bots em vez de dois, patrulhando os 38 m de raio do mapa real.
- Alcance do minimapa de 42 para 30 m — em 42 ele mostrava o mapa
  inteiro, o que é planta baixa, não minimapa.
- Pasta do mapa renomeada de `ARRİVAL` para `arrival`: o `İ` era um
  U+0130, que quebra caminho de recurso, git e export para Windows.

## v0.18.1 — 2026-11-11

### Corrigido
- **O lobo voltou a grudar na parede subindo.** A trava do ápice
  barrava a subida inteira. O conserto certo do pulo não era impedir
  de grudar subindo e sim preservar o impulso ao grudar: agora a
  subida continua e a altura só trava quando o pulo se esgota.

### Alterado
- **Botão direito no chão** virou patada pesada da direita: 2,1× de
  dano e um segundo de recuperação.
- **Botão direito no ar** virou bote de três golpes na direção da
  mira, com o pitch — não cai mais reto. Dois segundos de recuperação.
- As patas **alternam** a cada golpe do combo.

## v0.18.0 — 2026-11-11

### Adicionado
- **Cobertura nos bots**: ação `TakeCover` que procura, por raycast,
  um ponto de onde o alvo não enxerga. Nada é marcado no mapa, então
  funciona em qualquer geometria. Recarregar atrás de parede passa a
  valer mais que recarregar exposto.

### Corrigido
- **Lobo colado na parede não conseguia pular**: a corrida grudava no
  frame seguinte ao salto e travava a altura em zero, matando o pulo.
  Agora só gruda a partir do ápice e não gruda rente ao chão.
- Cartão de lote é clicável inteiro; o botão interno saiu.

## v0.17.1 — 2026-11-11

### Corrigido
- **Bots ficavam presos atrás das paredes longas.** O desvio por
  raycast não contorna geometria extensa. Agora há navegação assada em
  tempo de execução (`NavBaker`) e os bots seguem caminho com
  `NavigationAgent3D`.

### Adicionado
- **Reação variada a ruído**: cada bot nasce com uma curiosidade
  sorteada, e o tipo de som pesa — tiro chama mais que passo. Quem
  decide ficar ainda assim vira na direção do som.

## v0.17.0 — 2026-11-11

### Adicionado
- **`MapLoader`**: carrega mapa de `.glb`, gera colisão de malha,
  coleta spawns e assa navegação em tempo de execução. Convenções por
  nome de nó: `-noco`, `-invis`, `spawn*`.
- `assets/maps/LEIA-ME.md` com as convenções e as exigências de
  geometria para a corrida em parede.

## v0.16.0 — 2026-11-11

### Alterado
- **A decisão dos bots virou utility AI.** Dez ações pontuadas de 0 a
  1, com a maior vencendo, no lugar de transições fixas. Considerações
  multiplicam (uma ruim veta a ação), o produto é compensado para não
  punir ações bem avaliadas, e a ação em curso ganha inércia para o
  bot não oscilar.
- Decidir forma, recarregar e recuar deixaram de ser regras soltas e
  passaram a competir como ações.

### Adicionado
- `scripts/ai/Utility.cs` — curvas de consideração separadas da
  decisão.
- `scripts/ai/BotBrain.cs` — contexto e pontuação das ações.
- Ações novas que a máquina de estados não tinha: Reload, Retreat,
  Howl e Reposition.

## v0.15.0 — 2026-11-11

### Alterado
- **O ataque do lobo virou combo de três golpes**, com o terceiro mais
  forte, e deixou de ser hitscan: cada patada cria um volume curto à
  frente do corpo. O alcance passa a variar com a postura em vez de
  seguir a mira.
- **Cada golpe empurra o lobo para frente** (`ClawLunge`), para que
  mira ruim ainda conecte.
- **A corrida em parede é sustentada pelo pulo**, não pelo W, e não
  exige mais estar caindo para grudar.
- **Segurar o ataque agarra na parede sem avançar.**
- **O salto da parede ganha altura** (`WallJumpClimb` 1.35), o que
  permite escalar um corredor ricocheteando entre as duas paredes.

## v0.14.2 — 2026-11-11

### Adicionado
- **Barra de slots** no rodapé, aparecendo ao trocar de arma e sumindo
  sozinha. Mostra os quatro itens com o atual destacado.

### Alterado
- Minimapa virou quadrado e foi para o canto superior direito.
- Feed de abates desceu para baixo do minimapa e ficou maior.

## v0.14.1 — 2026-11-11

### Corrigido
- Com menu aberto, o mouse agora pertence ao menu: cursor visível,
  câmera travada, sem atirar nem andar.
- Dois menus abrindo juntos no início brigavam pelo cursor — fechar o
  de dificuldade capturava o mouse com a tela de lotes aberta. O
  estado virou um contador em `UiState`.
- O relógio da tela de lotes corria por trás do menu de dificuldade e
  escolhia o lote sozinho. Agora só conta quando é a tela de cima.

## v0.14.0 — 2026-11-11

### Adicionado
- **Lotes de armamento** com escolha em 10 segundos, no início e a
  cada morte. Quatro lotes: padrão (concede 50 WP), assalto (200),
  precisão (420) e elite (520). Tempo esgotado entra o padrão.
- **Munição por carregador** com reserva infinita, recarga no R
  (humano) e recarga automática ao atirar com o carregador vazio.
- **Recuo por arma**, com teto de acumulação.
- **Minimapa** no canto superior esquerdo, girando com o jogador e
  mostrando só quem está no alcance.
- **Resistência de corpo do lobo**, atravessada por headshot.

### Alterado
- Alcance da garra aumentado (2,5 → 3,8 no Bruto).
- WP vira número puro com teto de 5000; HUD direito mostra WP,
  munição e forma, como na referência.
- Bots avançam enquanto atiram, em vez de administrar distância, e
  patrulham puxando para o meio do mapa.
- Escudo só volta a escorrer 1 segundo depois de ser recarregado.
- HUD reescrito: esquerda é o corpo, direita é o equipamento.

## v0.13.1 — 2026-11-11

### Alterado
- O golpe especial do lobo passa a ser uma **investida**: ao apertar,
  ele despenca na direção em que olha enquanto dá as duas patadas.

### Adicionado
- **Braços e garras do lobo** presos ao corpo, visíveis em terceira
  pessoa e para os outros jogadores, com balanço ao correr e abertura
  no golpe.

## v0.13.0 — 2026-11-11

### Adicionado
- **Times**: azul (jogador) e vermelho (bots), com fogo amigo
  desligado.
- **Feed de abates** no canto superior direito, com nomes coloridos
  por time e a causa da morte: na cabeça, na faca, de lobo, de
  granada.
- **Patada dupla do lobo**: botão direito no ar. Dois golpes em
  sequência, 1,45× de dano e cooldown quase o dobro.
- **Bônus da patada contra humano** (1,6×).

## v0.12.0 — 2026-11-11

### Alterado
- **Vida é uma só**: lobo e humano compartilham o mesmo pool. O que a
  forma acrescenta é o escudo. Vida separada fazia de transformar um
  segundo tanque de vida.
- **Slots**: 4 virou faca, granada foi para o 5.
- **Rugido completa em 1 segundo** segurando R, em vez de recuperar
  continuamente. Soltar antes do fim não recupera nada — é o que
  torna rugir uma aposta.
- **WP vem de abater (150) e renascer (40)**, não mais do relógio.
  Ganhar por tempo premia esperar; ganhar por abate premia jogar.
- Painel de WP, slot e granadas foi para o lado direito da tela, com
  a vida à esquerda. Separar os recursos no espaço evita ler o errado
  no meio da luta.

### Adicionado
- **Faca** no slot 4: 85 de dano, alcance curto, sem ruído de tiro.
- **Imortalidade de 3 segundos ao renascer**, para quem morre perto de
  um inimigo não voltar direto para a mesma morte.

## v0.11.2 — 2026-11-11

### Corrigido
- Bots continuavam atirando no jogador morto: a percepção via o nó do
  alvo mas não checava se ele estava vivo. Alvo abatido agora deixa de
  ser alvo, e o bot esquece o que sabia e volta a patrulhar.

## v0.11.1 — 2026-11-11

### Adicionado
- **Andar devagar com Shift**, só na forma humana: 42% da velocidade
  e sem ruído. Segunda ferramenta de furtividade ao lado do agachar.
- **Clarão no cano** e **recuo de câmera** a cada disparo.

### Alterado
- O traçante passa a nascer na ponta do cano em vez da câmera. O raio
  continua partindo da câmera, que é de onde vem a precisão.

## v0.11.0 — 2026-11-11

### Corrigido
- **R não rugia**: a ação `howl` estava gravada no `project.godot`
  com F, do Input Map configurado à mão no início, e o `InputSetup`
  não sobrescreve ações existentes. A seção `[input]` foi removida e
  o código virou a única fonte das teclas.
- **Sair do lobo parecia levar dano**: o alerta vermelho vinha de ver
  a vida baixar, e trocar de forma troca o pool de vida. Agora vem do
  sinal de dano.
- Transição de volta para humano caiu de 0,8s para 0,2s.

### Alterado
- **Cada forma guarda a própria vida.** Sair do lobo e voltar devolve
  o lobo como estava, em vez de restaurar proporção. Morrer zera os
  dois corpos.
- **Granada equipa em vez de arremessar**: 4 equipa, o botão de ataque
  joga. Depois do arremesso volta sozinha para a arma anterior.
- Corpos mais magros (raio 0,4 → 0,3) e zona de cabeça menor
  (0,55 → 0,72 do centro), tornando o headshot mais difícil.

### Adicionado
- **Arma visível em primeira pessoa** (`ViewModel`): rifle, pistola,
  granada e garras, com recuo no disparo e balanço no passo. Formas
  geométricas por enquanto; trocar por modelo é trocar a malha.

## v0.10.1 — 2026-11-11

### Adicionado
- **Patrulha**: em Idle o bot escolhe um ponto aleatório da área, vai
  até ele devagar, pausa e escolhe outro. Patrulhar devagar também faz
  menos barulho.
- Detector de travamento: sem navmesh não há garantia de que o destino
  seja alcançável, então o bot desiste quando para de progredir em vez
  de empurrar a parede.
- Velocidade e pausa de patrulha entram nos perfis de dificuldade.

## v0.10.0 — 2026-11-11

### Adicionado
- **Barramento de ruído** (`NoiseBus`): passos, aterrissagem, tiro,
  rugido e explosão publicam posição e raio. Agachado não emite nada.
- **Audição nos bots**: localizam a origem do som com erro, porque
  ouvir não é ver. Não acumula com a visão — quem já vê o alvo ignora
  som.
- **Máquina de estados do bot**: Idle, Investigate, Combat e Search.
  O bot age sempre sobre a última posição conhecida, nunca sobre a
  posição real do jogador. Ao chegar sem achar ninguém, varre o
  entorno girando e desiste.
- **Barra de vida sobre os bots**, no mundo e não no HUD, com billboard
  e teste de profundidade — barra visível através da parede entregaria
  posição de graça. Mostra escudo quando há.
- Audição e precisão de localização entram nos perfis de dificuldade.

## v0.9.0 — 2026-11-11

### Adicionado
- **Menu de dificuldade** (Tab, e aberto no início): fácil, médio e
  difícil. Muda reação, erro de mira, taxa de erro deliberado,
  sustentação de fogo, percepção e mobilidade — nunca vida ou dano.
- **Erro deliberado** nos bots (`MissChance`): fração dos tiros que
  sai de propósito com desvio grande, separada do erro de mira comum.
  Nem no difícil vai a zero.
- **Escudo do lobo escorre** com o tempo (`ShieldDecay`). Deixa de ser
  vida extra e vira relógio.

### Alterado
- Rugido mudou de F para **R** e passou a ser **segurado**: recupera
  escudo continuamente enquanto trava o lobo no lugar.
- Vida e escudo passam a ser exibidos como números grandes em vez de
  barras. Com um recurso que vaza, o jogador precisa ler o valor.
- Escudo do Bruto 100 → 200, do Ligeiro 60 → 140.

## v0.8.0 — 2026-11-11

### Adicionado
- **Dano na cabeça**: 2,5× o dano normal. A zona é a faixa superior
  do corpo, medida a partir do centro, então continua válida quando
  houver modelo no lugar da cápsula.
- **Marcador de acerto**: quatro riscos em volta do crosshair,
  brancos no corpo e vermelhos e maiores na cabeça.
- **Alerta de dano**: faixas vermelhas nas quatro bordas da tela ao
  levar tiro, desaparecendo em menos de meio segundo.
- **Reação do bot ao ser alvejado**: marca a origem do tiro, espera
  um tempo curto com variação aleatória e então gira mais devagar que
  o normal. Reação instantânea é desumana e anula o prêmio de atacar
  pelas costas.
- Sinais `Damaged` em `Health` (com a origem do dano) e `HitConfirmed`
  em `Weapon`.

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
