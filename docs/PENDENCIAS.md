# Pendências

O que ainda falta no Alcateia, num lugar só. Defeito relatado vem
primeiro; depois o que foi pedido e ainda não existe; por fim as
pendências técnicas que já se conhecem.

Resolveu um item? Tire daqui e escreva no `CHANGELOG.md`. Achou um novo?
Ponha aqui, com o que se sabe dele.

## Corrida do humano (Shift, 26/11)

- **Os bots não correm**, e o bot humano anda 3,0 m/s (`Bot.Speed`), mais
  que o jogador desde que o andar caiu para 2,56. Baixar o `Bot.Speed`
  baixa também o lobo do bot (a velocidade dele é esse número × a classe).
- **As poses de correr em terceira pessoa**: veja na cena
  `src/scenes/debug/vitrine_corrida.tscn`. As medidas do gerador apontam
  o cotovelo direito do Barut aberto com a pistola (81° do "para baixo").
- **A coronhada em terceira pessoa** (nova em 26/11, cena
  `src/scenes/debug/vitrine_coronhada.tscn`): com os fuzis o gerador mede
  o ombro esquerdo torcido até 72–97° e o cotovelo direito a 93–134° do
  "para baixo" (acima do ombro na AK-74, na SCAR e na SVD): a arma
  atravessada a 90° na frente do rosto. Falta olhar de perto e, se
  preciso, conter o giro só na terceira pessoa.
- **Clipe novo de primeira pessoa feito por cinemática inversa**: confira a
  linha "na troca de animação a mão sai do caminho" que o `correr.py`
  imprime. O Godot mistura animações pelo giro de cada osso a partir do
  repouso; osso perto de 180° do repouso (o braço de cima esquerdo da
  pistola, 177°) atravessa a tela na troca se o clipe novo o girar.
- **O pulo correndo do humano avança 1,9 m** (eram 2,6): conferir nos mapas
  se algum vão que o humano pulava ficou fora de alcance.
- O `sprint_fwd.fbx` tem 15 MB (veio com a pele do X Bot); dá para trocar
  por um só com o esqueleto. O `Rifle Run.fbx` e o zip do tutorial na raiz
  do projeto não são usados pelo jogo e podem sair de lá.
- De lado o humano ainda patina (só há strafe andando).

## Para o alpha (anotado em 25/11)

Estado: rede autoritativa em produção e testada com latência real (Jhon);
testes de movimento alinhados (Jhon). O que falta antes de abrir:

- **Rodar a bateria inteira** com o Godot .NET: `src/tools/testar.sh`,
  `testar_servidor.sh` e `testar_online.sh` (no modo autoritativo). As
  mudanças de 25/11 abaixo só foram compiladas, não jogadas.
- **Conferir o conjunto de armas numa partida online** com uma conta que
  tem uma arma comprada: ela não pode ser trocada nem gerar recusa no log
  (`HostedMatch.AllowedWeapon`). Publicar o servidor central e o de partida
  juntos; um só novo não conferem nada.
- **Commit limpo**: mais de 100 arquivos sem commit (arte apagada e
  modificada, `src/crash.txt` e a pasta `erros_espeical_lobo_e_mapa_cidade`
  soltos). Proposta: ajustes, servidor, lobos, limpeza do Sketchfab, docs.
- **Créditos escondidos** (`OptionsScreen.ShowCredits = false`): o botão
  fica escondido durante o alpha. **Religar quando liberar no beta e em
  produção**: o Oculus Audio Pack (CC BY) pede crédito visível no jogo
  publicado.
- **Voz da Eve**: a dor, a morte e a respiração usam a gravação
  masculina tocada mais aguda (`FemaleVoicePitch`); o Jhon aprovou o
  resultado da respiração (25/11). Se quiser, trocar dor e morte por
  gravações próprias.
- **Mudanças de 25/11 para o Jhon ver jogando**: ícone de perfil no
  resultado e no MVP, sem "Jogar de novo", minimapa só com inimigo à vista
  de um aliado (some em 3 s), barra de vida do inimigo a qualquer
  distância (cresce até 3,2×), morte sem autor no feed, voz de
  vitória/derrota junto do texto, no máximo 3 contas salvas no login.
- **Servidor autoritativo, o que ainda é do jogo**: a forma e a arma na mão
  (o servidor só confere o acerto contra elas), os bots (máquina do dono da
  sala) e a guarda do especial do lobo. O servidor ainda manda todos os
  corpos para todos (wallhack).
- **Segurança do servidor autoritativo, para depois do alpha** (anotado em
  25/11; aceito no alpha aberto). Ainda vêm do jogo: a forma (humano ou
  lobo), a arma na mão (o servidor só confere o acerto contra elas), os
  bots (rodam na máquina do dono da sala) e a guarda do especial do lobo.
  E o servidor manda **todos os corpos para todos**: wallhack (ver
  através da parede) é possível com um jogo adulterado. Ordem sugerida:
  1. **Não mandar inimigo fora de vista** (o servidor já tem o mapa e o
     histórico de poses para decidir quem cada um vê); é o que mais
     aparece num alpha aberto.
  2. **A forma e a arma na mão decididas pelo servidor**, a partir do
     comando de troca.
  3. **Os bots no servidor de partida**, em vez da máquina do dono.
  Enquanto isso: acompanhar no painel os alertas de acerto recusado
  (`rejectedHits`, alerta a partir de 15 numa partida) e a ficha dos
  jogadores com suspeita (`Plausibility`).
- **Texto morto**: `textos.csv` ainda traz três textos de Presas/Fangs
  marcados "(não está mais no código)" (linhas 64, 73 e 91).
- **`docs/DESIGN.md`**: a curva de XP ("250·r^1,9") e o "150 XP por jogar"
  estão desatualizados em relação ao código (100·r de custo, 30 XP).

## Defeitos relatados (13/11/2026, jogando no Mac)

| Defeito | O que se sabe |
|---|---|
| **AR-15: recarga vazia** (16/11) | A recarga do AR-15 agora é a animação do Jhon (2,7 s, esticada para 2,3 s ou 2,9 s com o pente vazio). Falta uma "recarga vazia" própria (puxando o ferrolho): quando o Jhon animar, entra no `infima.py` como `recarga_vazia` e o jogo usa sozinho. |
| **Lobo do Meshy: licença e origem** (15/11) | O lobo vem do Meshy. No plano grátis a licença é CC BY 4.0 (com crédito, que já está em `Credits.cs`); nos pagos o uso comercial é do dono. Confirmar o plano em que foi gerado, e que o prompt ou imagem de referência não era arte do WolfTeam (regra de IP). Quase todas as animações já são do próprio Jhon (`lobo_anim.glb`); só a morte e a corrida na parede ainda são do Mixamo, adaptadas por código. |
| **Lobo: garrada só da cintura para cima** (15/11 à noite) | O rugido toca de corpo inteiro (o lobo para para rugir). A garrada continua da cintura para cima (o lobo anda enquanto bate): a pisada do clipe não aparece. |
| **Rugido do inimigo sem animação, online** (25/11: "a animação do lobo rugindo inimigo não consigo ver, somente os efeitos dela") | No roteiro autoritativo (`MovimentosOnline`) o servidor ruge inteiro, a marca chega ao boneco estável e o clipe toca até o fim: não se reproduziu com o inimigo sendo **jogador**. Os efeitos e o clipe saem da mesma marca no boneco (`WolfVisual.Howling`), então se o efeito aparece a marca chegou. Falta saber se o inimigo era um bot (o rugido do bot vem pelo `BotState`, não pelo estado do servidor) e, nesse caso, olhar o boneco do bot rugindo andando (o clipe é de corpo inteiro; o andar pode estar por cima). Um vídeo curto resolve. |
| **Lobo sai da parede no servidor** (25/11: ~30 "wall server: off the wall … client on the wall: True" em 30 min) | O pátio de teste com parede reta não diverge (0 estados diferentes, `MovimentosOnline`). Dois motivos de divergência saíram (o bot sem corpo no servidor e o aliado sólido lá, decisão 0017). Falta medir nas paredes de verdade dos mapas (quinas, peças de colisão do mapa do servidor contra as do jogo). |

## Pedido e ainda não feito

### Armas, lobos e site (anotado em 24/11)

| Pedido | O que se sabe |
|---|---|
| **Pistola padrão: Desert Eagle → G17** | A pistola de quem começa vira a Glock 17. A primeira pessoa da pistola já usa referências da Glock e da P9 (`src/tools/armas/conjuntos.py`, `RefPistola`); falta o modelo, a ficha (`armas.json`, `bracos.json`), a terceira pessoa (Eve e Barut) e trocar o padrão do conjunto. |
| **Polir os lobos** | **Feito em 25/11** (modelo novo do Meshy nos dois times, olhos de luz, rabo com física; veja o CHANGELOG). Falta o Jhon ver jogando. Se quiser um corpo diferente para o Barut, é gerar outro modelo no Meshy e passar pelo `src/tools/lobo`. |
| **Medidas reais das armas** | Pegar o tamanho real de cada arma e aplicar na primeira e na terceira pessoa (hoje cada uma foi ajustada no olho). |
| **Som da pistola** | Os tiros da pistola estão simples demais. |
| **Distância dos tiros, mirando e sem mirar** | Rever o alcance e a queda do dano (e a dispersão) com e sem a mira. |
| **Granadas e a 8 Bit no site** | Aparecem erradas no site; arrumar (a foto da 8 Bit foi refeita em 24/11, `src/assets/ui/armas/ar15_8bit.png`). |

### Retrato, moldura e fundo (anotado em 21/11)

- ~~Fotos de perfil de verdade~~ (feito 21/11: 20 ícones pintados,
  `src/assets/ui/avatares/`). Falta: 10 deles estão em 256 px (o Figma limitou
  as exportações); trocar pelos de 1024 px do arquivo "WolfLeague — ícones
  de jogador" no Figma.
- **Na partida, o retrato é o personagem** (feito em parte, 21/11: o Tab e
  o HUD mostram `src/assets/personagens/<nome>/retrato.png`, Eve ou Barut).
  Falta: o retrato do lobo na forma lobo (troca junto com a
  transformação) e, para os outros jogadores online, o personagem que
  escolheram: hoje a escalação não leva o personagem (veja "Personagem
  online" abaixo), então eles aparecem com o do time (Eve no azul, Barut
  no laranja), o mesmo modelo que se vê na partida.
- **TAB: a moldura do nível vira o cartão inteiro.** Cada jogador ganha
  uma moldura retangular, no cartão todo, de acordo com o nível; o retrato
  do centro fica só a imagem, sem a moldura hexagonal. A moldura
  retangular fica mais robusta conforme o nível (como as de perfil: metal
  e estágio), mas sem folhas nem louros. Hoje: `TabBoard`, cartões de
  204 px com o `RankBadge` (foto + borda + placa) no centro. A imagem do
  centro é o retrato do personagem, como no HUD (Jhon, 21/11).
- **Fundos do cartão, na loja.** O interior da moldura retangular pode ser
  personalizado com fundos comprados na loja. Tipo de item novo (como as
  fotos: catálogo, regra no servidor de que é dele, escolha mandada ao
  saguão e na escalação, para os outros verem).

### Nome do WP, classes de lobo, Discord e site (anotado em 21/11)

- **Revisar o servidor do Discord e o site.** O Discord sai de
  `src/tools/discord/montar.py` + `servidor.json` (cargos, canais, regras,
  "como jogar", convite): conferir textos, canais e permissões depois de
  montado, e o link de download (aponta para as Releases do GitHub; se o
  repositório for privado, os testadores não baixam).
- **Mais classes de lobo, no servidor, com thumbs.** Hoje são duas, Bruto
  e Ligeiro (`src/resources/breeds/*.tres`), já no catálogo como tipo `breed`
  (`exportar_dados.py` leva para o servidor e o painel). Criar as classes
  novas (atributos no `.tres`, item no `Catalog.cs`, preço), regenerar o
  `jogo.json` para o servidor conhecê-las, e fazer uma thumb de verdade
  para cada uma — hoje a arte da classe na loja, no inventário e no
  conjunto é desenhada em código (`WeaponArt.WolfHead`, a silhueta do
  lobo num escudo).

### Para depois (anotado em 19/11)

- **Moldura do personagem no HUD pela patente.** A moldura do retrato
  (embaixo à esquerda, ao lado da barra de vida) evolui com a patente:
  mais robusta a cada patente, com a patente escrita no centro de baixo.
- **Câmera de abate e jogada da partida — de volta.** Removidas por
  inteiro em 19/11 (`ReplayTape`, `PlayOfTheGame` e a fase de replay da
  `KillCam` saíram; a partida não é gravada). Hoje, morto: o corpo por um
  instante e o espectador. Para voltar, fazer do jeito do Overwatch: o
  servidor guarda os últimos segundos do estado de todos e o cliente
  remonta a cena com os modelos de verdade.
- **Tradução revisada por nativo.** Inglês e espanhol estão preenchidos em
  `src/assets/i18n/textos.csv` (946 textos); falta alguém nativo passar o olho
  nos nomes (Presas = Fangs/Colmillos, Sangue = Blood/Sangre, patentes).

Lista do Jhon (13/11/2026), agrupada. "Hoje" diz o que já existe.

### Arte própria

- **Animações próprias das mãos.** Hoje: as do Sketchfab (CC BY) nas
  armas de fogo, as de código na faca e na granada.
- **Modelos próprios de armas.** Hoje: Sketchfab nas armas de fogo,
  código na faca e na granada.
- **Modelos próprios de humano e de lobo.** Hoje: Mixamo.
- **Personagem online: a escalação não leva o personagem** (conferido em
  25/11). Hoje não aparece: cada time tem um personagem só (Eve no azul,
  Barut no laranja), e o padrão do time é o que se escolhe. Quando entrar
  um terceiro personagem, ou um que sirva nos dois times, os outros vão
  ver o padrão do time no lugar dele: a escalação do servidor
  (`OnlineMatch.Seat`) não leva o personagem, e o boneco nasce com o
  modelo do time. Precisa: o personagem na sala (servidor) e o boneco
  montar com ele.
- **Preço dos personagens.** Hoje todo personagem vem com o perfil
  (`Starter`). Personagem com vida a mais seria vantagem de quem o
  escolhe; ter preço (em Presas, nunca em Sangue) é decisão de economia.
- **Logotipo do jogo.** Hoje: só o nome escrito na fonte do menu.
- **Imagens das patentes melhores.** Hoje: insígnias desenhadas por
  código (`RankBadge`).
- **Dublagem dos personagens, em inglês** (pedido em 17/11): falas de
  combate, dor, morte, transformação e uivo. Hoje: nenhuma voz de
  personagem. Decidir a origem (dublador, voz sintética) e conferir a
  licença de uso comercial e de voz antes de gravar; e ligar à
  internacionalização (a dublagem fica em inglês, o texto muda de idioma).
- **Lootbox em 3D, com animação de abrir.**

### Combate e visual

- **Mirar pela alça da arma (ADS de verdade).** Hoje o botão direito dá
  zoom com mira circular (13/11). Mirar pela arma foi tentado e ficou
  ruim: pede animação própria de mirar em cada conjunto dos braços.
  Luneta de verdade para uma arma de precisão, também.
- **Efeito de sangue nos tiros** (no corpo atingido e no chão/parede
  atrás).
- **Efeito de tiro melhor** (clarão, fumaça, traçante, marca na parede).
- **Animação de quadrúpede e de ataques do lobo melhores.**
- **Sombreamento melhor nos mapas, levando em conta o personagem**
  (sombra do personagem e luz do mapa sobre ele).
- **Qualidade de textura baixa, média e alta** nas opções. Sombra,
  oclusão, reflexo e suavização já têm opção (Opções → Vídeo, 13/11);
  falta a resolução das texturas e a distância de desenho.
- **Áudios e sons de nível profissional** (17/11): armas, passos, lobo, ambiente, menu.
- **Efeitos visuais (GFX) em geral** (17/11): o que já existe está em
  `docs/EFEITOS.md`; faltam os do lobo (fumaça, ar, feixes nas garras,
  brilho) e os de tiro, sangue e impacto listados acima.
- **Sobras do salto carregado.** Com o especial no Q, a carga andando
  (`Charge_Walk`), o anel de mira no chão (`PounceAim`) e
  `PounceChargeHold` não têm mais como acontecer para o jogador. Tirar,
  ou guardar para outro golpe.
- **Bots: o que ainda falta.** Espalhar de verdade (o aliado mais próximo
  segue a uns 8 m em média: nascem juntos e convergem para a mesma
  briga), usar escada e passarela de propósito, recuar em grupo, e um
  papel por bot (atirador de longe, flanco, linha de frente) em vez de
  só gostos sorteados.
- **Falas das sequências (pedido do Jhon, 17/11) — falta gravar.** O
  feed, os ícones e as chamadas de DOUBLE/TRIPLE/… KILL, TEAM KILL e
  VINGANÇA estão prontos (`KillStory`, `KillBanner`), e o jogo já toca a
  fala se o arquivo existir: `src/assets/audio/voz_double_kill.wav`,
  `voz_triple_kill`, `voz_quadra_kill`, `voz_penta_kill`,
  `voz_hexa_kill`, `voz_hepta_kill`, `voz_octa_kill`, `voz_team_kill`,
  `voz_vinganca`. Falta a voz: gravar, comprar ou gerar (uma tentativa
  de gerar com voz sintética em 17/11 foi barrada pelo ambiente). Desde
  18/11 toda sequência toca um "multi kill" genérico (`voz_multi_kill`).
  Onde buscar as falas certas, e a **música do menu** (hoje é só um
  ambiente de floresta): `docs/AUDIO.md`.
- **HUD, fontes e menu melhores — prioridade** (Jhon pediu foco nisso em 17/11).
  Primeira passada no HUD de combate feita em 17/11 (vidro, facetado,
  números inclinados, vida em gomos — `docs/UI.md`) e nos menus (fundo do
  mapa borrado, painéis de vidro, facetado, botões inclinados). Falta
  conferir na tela e acertar tela por tela (loja, inventário, salas).

### Mapas e modos

- **Mais mapas.**
- **Colisões dos objetos do mapa Neon** (anotado em 21/11): resolver as
  colisões dos objetos do mapa. Junto, a revisão pedida em 20/11 para
  todos os mapas, que ficou parada: nenhuma colisão maior que o objeto
  que se vê (o teste que varre as caixas de colisão contra o desenho está
  pronto fora do repositório, `CollisionFitTests.cs`; na Cidade apontou
  41 caixas, a maioria grades, lixeiras e barras de poste e árvore, ainda
  a conferir em foto).
- **Campo de treinamento** (pedido em 17/11): lugar para treinar mira,
  recuo, salto do lobo e movimento sem partida valendo. Hoje: a tela
  "Treino com bots" foi removida em 17/11 e só resta a cena de treino
  interna dos testes.
- **Jogo rápido: buscar partida automaticamente** (pedido em 17/11): um
  botão que põe o jogador numa partida aberta (ou cria uma) sem passar
  pela lista de salas, equilibrando times e patente. Pede fila e
  pareamento no servidor.
- **Modo espectador pela sala** (pedido em 17/11). Morto, o espectador já
  existe (19/11: primeira pessoa do aliado com o HUD dele, vista de cima e
  câmera livre presa no mapa — `KillCam`). Mas a caixa "Espectador" da
  sala só guarda a escolha: nada no jogo nem no servidor a lê. A fazer:
  botão "Assistir" na lista de salas (o espectador não ocupa vaga nos
  times e fica numa faixa à parte na sala); na partida, sem corpo e fora
  do placar, com três câmeras — seguir um jogador (trocando com clique
  e Q/E), câmera livre e vista de cima; entrar com a partida em
  andamento; sem conversar com quem joga durante a partida (ele vê os
  dois times). No servidor: a lista de espectadores da sala, mandar a
  eles o estado de todos sem aceitar comando, e recusar ação de
  espectador.
- **Modo bomba**, com mapas feitos para ele (áreas de plantio, ataque e
  defesa, rodadas). Hoje só existe Equipe (mata-mata por time).

### Personagens, loja e economia

- **Recompensa de testador do alpha** (prometido no site do alpha, 21/11):
  quem jogou o alpha ganha armas e itens exclusivos no lançamento, que não
  vão para a loja. A fazer: criar essas armas e itens; guardar no
  servidor quem foi testador (conta que jogou partida no alpha, ou o
  cargo de testador do Discord) de um jeito que sobreviva se as contas
  do alpha forem zeradas; e entregar na conta no lançamento. O site
  também diz que o progresso do alpha pode ser zerado, mas a recompensa
  não depende disso.
- **Comprar personagens novos e jogar com eles** — loja, inventário e
  partida (primeira e terceira pessoa, e na rede).
- **Cada personagem com o seu próprio lobo — e o lobo muda a raça**
  (decidido pelo Jhon, 13/11): estatísticas e golpes de cada raça.
  Cuidado com a regra de design 1 (um eixo de troca por classe) e com a
  3 (nenhuma vantagem permanente): cada raça nova troca uma coisa por
  outra, não é melhor que as outras.
- **Lobo com poderes** (pedido em 17/11): poderes ativos de cada lobo,
  além das garras e do salto, com o brilho de poder que você pediu em
  16/11. Liga ao "cada personagem com o seu lobo". Cada poder troca uma
  coisa por outra (regra 1) e não pode ser vantagem comprada (regra 3).
- **Habilidade suprema** (pedido em 17/11): uma habilidade forte que
  carrega durante a partida (por abates, dano ou tempo) e se usa uma vez
  por vida. A definir: uma para o humano, uma para o lobo, ou uma por
  personagem; quanto carrega; o que a cancela. O servidor decide o uso
  (como o resto da Fase 2), e ela precisa de efeito e som próprios para o
  adversário ver que veio.
- **Revisar o funcionamento da loja e do inventário** (pedido em 17/11):
  conferir compra, duração e vencimento do item, equipar e desequipar,
  limite de tatuagens, o que o inventário mostra para cada categoria e o
  que chega à partida (e à rede). Hoje o perfil mora no arquivo do jogador,
  que se edita à mão (ver "Perfil, inventário e moedas no servidor").
- **Armaduras em personagens e lobos, dando mais vida** (decidido pelo
  Jhon, 13/11). Entra no `docs/ARSENAL.md` e nos testes de equilíbrio.
  Proposta para não virar "paga para ganhar": a armadura se compra com
  **Presas** (moeda de jogar), nunca com Sangue; o bônus é pequeno e com
  teto (como as tatuagens de hoje); e, se der vida, tira alguma outra
  coisa (velocidade, por exemplo) — regra 1.
- **Cash: dinheiro que o jogador paga** (17/11: o sistema de pagamento
  entra na lista de prioridades). Hoje o desenho já prevê o
  Sangue (moeda paga que nunca compra poder — `Catalog.cs`), mas não há
  compra. Falta: loja de Sangue, pagamento (Pix, cartão), saldo no
  servidor, recibo, estorno.
- **Lootboxes**, com animação e modelo 3D. **Atenção legal:** o ECA
  Digital (Lei 15.211/2025) proíbe caixas de recompensa em jogos voltados
  a crianças e adolescentes ou de acesso provável por eles, e o governo
  já cobra verificação de idade de quem as mantém. Antes de fazer, falar
  com advogado e decidir: classificação etária, verificação de idade, ou
  trocar por recompensa sem sorteio pago.
- **Recompensa diária definida no servidor**: armas, WP, lootboxes etc.
- **Desafios inseridos pelo servidor.** Hoje as missões existem, mas são
  fixas no jogo (`Missions.cs`): passam a vir do servidor.

### Contas e cliente

- **Sistema de contas.** Hoje: criar conta e entrar já existem, na
  primeira tela do jogo (`LoginScreen`, 20/11). Falta: e-mail,
  confirmação, recuperar senha, trocar senha, apagar conta (LGPD).
- ~~**O login fica na primeira tela do próprio jogo**~~ (decidido pelo
  Jhon, 13/11; feito em 20/11): o jogo abre no login, com as contas
  salvas para clicar (salvo não entra sozinho), entrar e criar conta. O
  "Recomeçar perfil" das opções saiu. O treino com bots segue sem conta,
  num botão discreto embaixo do login.
- **Launcher externo, estilo Battle.net** (pedido em 17/11): programa à
  parte que baixa e instala o jogo, atualiza sozinho, obriga a atualizar
  quando o servidor exige, desinstala e abre o jogo. Contradiz a decisão
  de 13/11 logo acima (sem launcher separado): confirmar quando chegar a
  hora. Base que falta: preset de exportação para Mac e Windows (o jogo
  nunca foi exportado), número de versão no projeto e checagem de versão
  no servidor (cliente velho é recusado). O Mac exige assinatura e
  notarização. Vem por último: só vale com uma versão estável para
  distribuir.
- **Perfil, inventário e moedas no servidor** (hoje ficam no arquivo do
  jogador — dá para editar à mão).
- **Uma instância só do jogo por máquina** (anotado em 22/11, para
  definir depois). Hoje dá para abrir quantos jogos quiser (`open -n` no
  Mac, o .exe de novo no Windows), e o `-- --perfil=b` (22/11) existe
  justamente para testar o multijogador com duas janelas, cada uma com a
  sua conta. Para os jogadores, a ideia é travar em uma: o segundo jogo
  avisa que já há um aberto (e traz o primeiro para a frente) e fecha.
  Decidir: se o `--perfil=` (ou o editor) continua liberando mais de uma
  para teste, e como travar (arquivo trancado na pasta do jogador, ou
  mutex com nome no Windows e trava de arquivo no Mac).

### Alcance: idioma e acessibilidade (pedido em 17/11)

- **Internacionalização.** Hoje: os 946 textos de tela estão em
  `src/assets/i18n/textos.csv` (gerado por `src/tools/i18n/extrair.py`), com
  português, inglês e espanhol, e o idioma se escolhe nas Opções. Falta:
  revisão de nativo, conferir texto que cresce e estoura botão em inglês e
  espanhol, formato de número e data, as falas do rádio, e a tradução das
  telas em HTML (plano do `GUIA_UI_HTML_CEF.md`) quando existirem.
- **Editar controles e teclas** (pedido em 17/11). Hoje: as teclas moram
  no código (`InputSetup`) e a tabela de Opções (`ControlsTable`) só as
  mostra, não deixa mudar. A fazer: tela de Opções → Controles para
  remapear cada ação (teclado e botões do mouse), avisar de conflito,
  restaurar o padrão, guardar no arquivo de opções do jogador, e a tabela
  passar a ler as teclas de verdade em vez de uma lista escrita à mão.
  Sensibilidade e inversão do mouse entram aqui.
- **Acessibilidade.** Hoje: nenhuma opção feita para isso (o que já existe
  em Opções → Controles não foi conferido para esta lista). Candidatos:
  - **Daltonismo.** A vida do aliado verde e do inimigo vermelho (decidida
    em 17/11) é justamente o par que mais se confunde: precisa de um sinal
    além da cor (forma, ícone, marca) ou de uma paleta alternativa nas
    Opções.
  - Tamanho da fonte e da interface; contraste e cor da mira.
  - **Som como informação** (`DESIGN.md`): aviso visual de direção para
    passos, tiro e uivo, para quem não ouve bem; legendas nos sons
    importantes.
  - Reduzir tremor e balanço da câmera.
  - Alternar em vez de segurar (agachar, mirar); o remapeamento é o
    item acima.

### Social e salas (pedido em 17/11)

- **Amigos**: lista, pedido de amizade, ver quem está online e em que
  sala. Hoje: só o saguão com chat (`UI.md` lista canais, amigos e
  matilha em "ainda fora").
- **Convidar para o jogo**: convite direto para um amigo entrar na sala,
  com aviso na tela dele.
- **Sala pública e privada.** Hoje: a sala aceita senha na criação
  (`Room.cs`), mas não há o conceito de pública ou privada: a lista mostra
  tudo. A fazer: privada fora da lista, só por convite ou código.
- **Reportar usuário** (pedido em 17/11): denunciar um jogador pelo placar,
  pelo fim da partida, pelo chat e pelo perfil, com motivo (trapaça,
  ofensa, nome impróprio, outro) e um campo de texto. O servidor guarda a
  denúncia com o contexto (sala, partida, últimas mensagens) e ela chega
  ao painel administrativo; quem denuncia recebe a resposta. Cuidar de
  denúncia falsa em massa. Liga ao chat de voz (silenciar e denunciar) e
  à moderação.

### Comunicação na partida

- **Chat dentro da partida**: para o time e para todos, com o nome e a
  cor do time, por cima do jogo sem tirar o controle. O saguão já tem
  chat (`LobbyLink`); falta na partida, pelo servidor de partida.
- **Rádio**: chamadas prontas para o time ("Preciso de ajuda", "Lobo
  vindo", "Recuar", "Entendido"…), num menu rápido de teclas, com a voz
  gravada e a frase no chat do time. Marca no mapa onde quem chamou está.

- **Chat de voz** (pedido em 17/11): por time e na sala, com apertar para
  falar, silenciar e denunciar jogador. Pede um serviço de voz em tempo
  real (WebRTC) e moderação; as duas coisas entram junto com a
  verificação de idade, já que há menores no público provável.

### Ranking

- **Revisar as patentes** (pedido em 17/11): nomes, quantidade e
  imagens. A curva dos ~8 anos e o XP por partida menor já entraram
  (17/11, `Ranks.cs`, `Rewards.cs`).

### Servidor e produção

- **Anti-trapaça** — hoje a vida e o acerto moram na máquina de cada
  jogador (`docs/decisoes/0008`). Passo um: vida e acerto no servidor de
  partida; depois, conferir velocidade e linha de tiro no servidor.
- **Subir em produção, na região do Brasil** (São Paulo), com domínio,
  HTTPS, banco gerenciado com backup, e servidores de partida perto dos
  jogadores.
- **Aguentar muitos jogadores ao mesmo tempo**: teste de carga (contas,
  saguão, partidas simultâneas), vários servidores de partida subindo
  sozinhos conforme a fila, saguão em mais de uma máquina (hoje as salas
  ficam na memória de um processo só), monitoramento.

- **Painel administrativo (backoffice)** (pedido em 17/11): contas,
  banimento e moderação, denúncias, salas e partidas ao vivo, saldo e
  compras, missões e recompensas diárias, versão mínima do cliente.
  Precisa de login próprio e registro de quem fez o quê.
- **Testar com 8 jogadores de verdade** (pedido em 17/11): uma partida com
  oito pessoas, não bots, com latência real. Hoje: só dois jogos sem tela
  na mesma máquina; o teste de carga está em `src/tools/testar_carga.sh`.
- **Windows: testar o iniciador com o jogo de verdade e medir os
  requisitos** (21/11). O iniciador (`instalador/iniciador.nsi`) foi testado
  no Wine e é testado no Windows do GitHub com um jogo de mentira; falta
  abrir a Release num PC com Vulkan, num só com DirectX 11 (cai na
  compatibilidade) e no Parallels. Os requisitos do instalador e do site
  (Windows 10/11 de 64 bits, 8 GB) são estimativa: medir memória, FPS e
  placa mínima de verdade, no Forward+ e na compatibilidade, e conferir
  se o visual da compatibilidade está aceitável (sem os efeitos do Forward+).
- **Links de download das releases no Discord, automático** (pedido em
  21/11): quando o CI de lançamento (`.github/workflows/lancamento.yml`,
  tag `v*`) publica a Release do GitHub, postar no canal de download do
  Discord a versão, as novidades e os links da Release
  (`WolfLeague-Setup-<versão>.exe`, o `.zip` portátil do Windows e o
  `.dmg` do Mac), por um webhook do canal guardado como segredo no GitHub.

## Pendências técnicas conhecidas

### O pé do humano entra no chão andando e correndo de pé (medido em 26/11)

Com o ajuste do pé de 26/11 (o pé copia o giro a partir do repouso), parado em pé a sola fica a +0,3/+0,5 cm. Andando e correndo ela ainda entra: Eve −2,1 (andar) e −3,1 cm (correr), Barut −4,1 e −4,6 (era −5,3 e −6,4). A perna da Eve e do Barut tem outra proporção que a do X Bot, e o tornozelo pousa baixo. O ajuste de perna do agachado (`AnimationKit.ReachFloor`) resolve (todos a −0,5 cm ou mais), mas nos clipes de pé mudou a medida de deslize do `PassadaHumana` (para trás 0,36 → 0,55 m/s), sem o pé andar na horizontal: muda quais pontos a medida conta como apoiados. Ficou só no agachado até combinar se a medida muda.

### Limpeza do repositório: o que ficou para decidir (levantado em 24/11)

O que tinha confiança alta saiu em 24/11 (CHANGELOG). Ficou, com confiança média (ninguém carrega hoje, mas pode servir):
- clipes reserva do lobo em `src/assets/personagens/_comum/lobo/` (`claw`, `heavy`, `howl`, `leap`, `jump`, `sprint`, ~21 MB): só entram se faltar o clipe no `lobo_anim.glb`, que tem todos;
- 36 peças do Poly Haven em `src/assets/mapas/pecas/` e 35 texturas de `src/assets/textures/pbr/` que nenhum mapa usa (~60 MB);
- o pacote cru da faca em `src/assets/sketchfab/` (41 MB, a única cópia: mover para `pacotes/`, não apagar; o crédito CC BY continua);
- os passos na neve (a neve era da Polar), o `ambiente_hangar.ogg` (matéria-prima do som do Armazém), a paleta das mãos antigas;
- o `textos.csv` guarda 93 textos que saíram do código.

Lembrete: a exportação leva tudo de `src/` (`all_resources`), então o que sobra aqui também vai no jogo exportado.



- **Braços animados não conferidos na tela.** As posições do M16 e da
  escopeta foram as mais chutadas.
- **Servidor no Docker nunca construído**: `docker compose up -d --build`
  é a primeira vez.
- **Partida online entre duas máquinas** nunca jogada (só dois jogos sem
  tela na mesma máquina).
- **Online**: compensação de lag, predição, reconexão, golpes
  especiais do lobo pela rede.
- **Bots online** (feito em 13/11) dependem da máquina do dono da sala:
  falta passar os bots para outro jogador quando o dono cai.
- **Bots** na Polar atiram metade do que atiravam (76 tiros em 40 s,
  eram 146); na Vila, 5 de 7 saem da base.

### Armas de 19/11 (M4A4, AK-74, SCAR-H, SVD, Desert Eagle, V40, faca)

- Feitas por `src/tools/armas/reais.py` (modelos) e `src/tools/armas/conjuntos.py` (primeira pessoa). Os arquivos de origem e os `.npz` ficam em `src/assets/pacotes/reais/` (fora do git); a licença dos modelos é do Jhon confirmar.
- **Não foi visto numa partida de verdade**: os testes automáticos passam e as poses foram conferidas em fotos fora do jogo.
- A faca foi refeita com a mão levada pelo braço (pulso reto, sem torcer a manga); falta o Jhon aprovar no jogo. Não achei referência utilizável de ataques de faca na internet: os golpes são desenhados à mão.
- Os sons são provisórios (`src/tools/sons/reais.py`): feitos dos do AR-15 e da pistola antiga.
- Números das armas e loja: todas entraram liberadas (`Starter`); o Desert Eagle usa a ficha "pistola pesada" do ARSENAL.md sendo a pistola padrão — rever o balanceamento.
- Guia para as próximas armas: `docs/ANIMACAO_ARMAS.md` (regras, como é montado, o que conferir). `python3 src/tools/armas/conferir.py` mede pulso, braço na frente da câmera e corte na mira.
- **Dedos nas outras armas** (feito em 19/11 para a mão esquerda): AK-74, SCAR-H e SVD repetem no guarda-mão o encosto da M4A4 (`pegada.pegar_como`); na Desert Eagle a mão esquerda abraça a direita sem atravessar (`abracar`). A mão direita nos punhos da AK-74, da SCAR-H e da SVD continua com a pose do AR-15 (só o indicador vai ao gatilho).
- Faca: o arquivo de referência só tem dois golpes; o terceiro e o quarto são derivados deles (a mão girada). Se aparecer arquivo com mais golpes, trocar.
- SCAR-H mirada: ainda mira por dentro do anel da alça, que é grande na tela (a M4A4 foi aberta em 19/11; a SCAR-H ninguém pediu).
- Pulsos que ainda passam de 40° (medidos por `conferir.py`): nos fuzis, no saque, no guardar e na coronhada (50–78°); a mão esquerda da Desert Eagle no apoio (78°).
- Granada: virou explosiva e de todo mundo (`Starter`, sem preço). A de choque continua no código (`GrenadeKind.Shock`), sem uso. O perfil foi para a versão 3: quem já tinha perfil ganha a granada no conjunto padrão uma vez.
- A granada sai da mão (19/11): nasce quando a mão solta no clipe `arremesso` (`ViewModel.HandPoint` → `Weapon.ReleaseFromHand`), e a velocidade é calculada dali até o ponto da cruz; a câmera fica parada do armar até soltar. Arco de 9° (perto) a 30° (no alcance máximo) acima da linha até o ponto. Com ponto na mira cai nele até 19 m; mirando o céu, ~18 m. Medido na bancada (`GranadaNaCruz`): o primeiro toque fica de 0,12 a 0,28 m do ponto (a bola de colisão tem 0,14 m de raio) no chão de 3 a 17 m, em parede a 9 e a 15 m e andando de lado; colado numa parede, não a atravessa. Fotografado na bancada com o jogo rodando (não numa partida): a troca da granada da mão pela que voa acontece atrás dos dedos.
- Arremesso refeito para soltar **em cima da linha da cruz** (19/11, "a granada tem que ser lançada no meio da linha da mão, na direção exata do crosshair"): medido no jogo, ela nasce a 1 cm da linha, sai a 0,04° dela e chega ao chão em cima dela; a granada da mão fica à vista até a de verdade começar a andar.
- A granada que voa é menor na tela que a da mão (a da mão é 18% maior e o conjunto é esticado para a câmera) e nasce sem a rotação da da mão. Os bots e os bonecos de rede lançam como antes, da frente da câmera; a mira do bot (`Bot.Grenade.ThrowPitch`) ainda faz a conta do arremesso antigo.
- A granada da primeira pessoa é 18% maior que a que voa.
- Versões épicas e lendárias (19/11; `src/tools/armas/peles.py`, sem Blender): atlas UV novo por arma e desenho calculado em 3D; os números estão nos .tres (e, da faca, em `Arsenal.KnifeStats`). A faca escolhida (`LoadoutSet.Melee`) não vai pela rede: os outros veem a faca comum, e o abate conta como "faca". No mundo, o metal das pinturas é a metade do desenhado (sem céu para refletir ficava preto). Conferido na bancada (`ArmasEpicasELendarias`) e por fotos; os desenhos são de programa, não de artista.
