# Changelog

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
