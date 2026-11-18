# Pendências

O que ainda falta no Alcateia, num lugar só. Defeito relatado vem
primeiro; depois o que foi pedido e ainda não existe; por fim as
pendências técnicas que já se conhecem.

Resolveu um item? Tire daqui e escreva no `CHANGELOG.md`. Achou um novo?
Ponha aqui, com o que se sabe dele.

## Defeitos relatados (13/11/2026, jogando no Mac)

| Defeito | O que se sabe |
|---|---|
| **Faca e granada** | Corrigido (13/11) com os modelos do Sketchfab na mão do braço da pistola. A faca agora usa o conjunto animado que o Jhon trouxe (golpes animados). Falta o Jhon conferir. |
| **Glitches nos mapas** | Varridos os quatro (13/11) com `tools/mapas/conferir.py`: faces brigando, letreiros enterrados ou soltos, objetos flutuando na Polar — corrigidos. O conferidor só olha caixas retas: rampas, formas livres (trem, tanques, terreno) e os modelos prontos não são conferidos. Se ainda houver, falta print ou o lugar exato. |
| **Glitches na troca de arma / braços reiniciando** | Corrigidos os que se acharam (tamanho e lugar dos braços, travada na primeira troca, sacar; o pulo do M16 parado e o recomeço ao perder o chão). Falta o Jhon conferir. |
| **Tiro na cabeça sem dano / M16 difícil** (14/11) | Corrigido: o tiro saía 1° acima da cruz (o recuo subia a câmera antes) e a cabeça era a faixa de cima da cápsula. Agora o tiro sai de onde a cruz está e a cabeça é a do modelo. Falta o Jhon conferir. |
| **Corpo do lobo fora da cápsula** (achado em 14/11) | O lobo anda curvado: a cabeça (já corrigida) e o pescoço ficam até meio metro à frente da cápsula, e tiro no pescoço e no peito curvado passa direto. Próximo passo: esferas do tronco seguindo os ossos, como a da cabeça. |
| **AR-15: recarga vazia** (16/11) | A recarga do AR-15 agora é a animação do Jhon (2,7 s, esticada para 2,3 s ou 2,9 s com o pente vazio). Falta uma "recarga vazia" própria (puxando o ferrolho): quando o Jhon animar, entra no `infima.py` como `recarga_vazia` e o jogo usa sozinho. |
| **Lobo do Meshy: licença e origem** (15/11) | O lobo vem do Meshy. No plano grátis a licença é CC BY 4.0 (com crédito, que já está em `Credits.cs`); nos pagos o uso comercial é do dono. Confirmar o plano em que foi gerado, e que o prompt ou imagem de referência não era arte do WolfTeam (regra de IP). Quase todas as animações já são do próprio Jhon (`lobo_anim.glb`); só a morte e a corrida na parede ainda são do Mixamo, adaptadas por código. |
| **Lobo agachado: cabeça fora da cápsula** (15/11) | O `Crouch_Idle` do Jhon deixa a cabeça a 1,18 m do chão; a cápsula agachada tem 1,0 m. A cabeça tem esfera própria e o tiro nela acerta, mas o peito acima de 1,0 m passa direto. Decidir: agachar mais a pose, ou subir a cápsula agachada. |
| **SG-552 contra a M16 nova** (14/11) | A M16 (e agora o AR-15, com os mesmos números) ficou mais precisa que a SG-552 (dispersão 0,15° contra 0,25°), que custa 320 WP e era a "arma de controle". Falta o Jhon decidir se a SG sobe junto. |
| **Lobo: garrada só da cintura para cima** (15/11 à noite) | O rugido toca de corpo inteiro (o lobo para para rugir). A garrada continua da cintura para cima (o lobo anda enquanto bate): a pisada do clipe não aparece. |
| **Testes de velocidade 6,7% acima** (15/11) | `VelocidadeNoChao`, `CorridaNaParede` e a passada do humano no `PassoNoRitmo` falham com tudo 6,7% mais rápido que o esperado (humano 3,456 em vez de 3,24). Já falhava antes das animações novas; vem das mudanças sem commit ou do último commit. Em 16/11 também falham `AlturaDoPulo` (Bruto pula 1,87 m em vez de 1,62), `SaltoCarregado` e `CargaDoSalto` (a mira do salto não sobe no bloco; o lobo pousa a 18 m dela) — todos de movimento do lobo, nas mudanças sem commit. |
| **Personagens tremendo e piscando** | Corrigido (13/11): o desenho agora anda entre os passos da física (`VisualSmoothing`) e a borda vermelha saiu de cima da pele. Não deu para ver no Mac daqui: falta o Jhon conferir; se continuar, um vídeo curto mostra se é tremor de posição, de animação ou de sombra. |

## Pedido e ainda não feito

Lista do Jhon (13/11/2026), agrupada. "Hoje" diz o que já existe.

### Arte própria

- **Animações próprias das mãos.** Hoje: as do Sketchfab (CC BY) nas
  armas de fogo, as de código na faca e na granada.
- **Modelos próprios de armas.** Hoje: Sketchfab nas armas de fogo,
  código na faca e na granada.
- **Modelos próprios de humano e de lobo.** Hoje: Mixamo.
- **Personagem online: os outros te veem com o padrão do time.** A vida
  do personagem vale online (cada jogo manda o máximo no status), mas a
  escalação do servidor (`OnlineMatch.Seat`) não leva o personagem, e o
  boneco dos outros nasce com o modelo do time. Precisa: o personagem na
  sala (servidor) e o boneco montar com ele.
- **Preço dos personagens.** Hoje todo personagem vem com o perfil
  (`Starter`). Com vida a mais, a Eve é vantagem de quem a escolhe; ter
  preço (em Presas, nunca em Sangue) é decisão de economia.
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

- **Partida autoritativa, segunda leva** (decisão 0011): a vida e o dano
  no servidor, o tiro conferido lá voltando no tempo, a forma, a arma na
  mão e o renascimento decididos pelo servidor, os bots no servidor, não
  mandar inimigo fora de vista, ajuste do ritmo do jogo pela folga da fila
  e compressão por diferença no estado do mundo. Até lá o modo
  autoritativo fica desligado por padrão.
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
- **HUD, fontes e menu melhores — prioridade** (Jhon pediu foco nisso em 17/11).

### Mapas e modos

- **Mais mapas.**
- **Aumentar o mapa do Armazém, com IA** (pedido em 17/11). Mexe em tudo
  que depende do tamanho: pontos de nascimento (hoje 6 por time), o
  minimapa, a rota dos bots e a luz. A decidir como: eu gerando as peças
  novas em `tools/mapas`, no mesmo kit, ou uma ferramenta externa de 3D.
- **Campo de treinamento** (pedido em 17/11): lugar para treinar mira,
  recuo, salto do lobo e movimento sem partida valendo. Hoje: a tela
  "Treino com bots" foi removida em 17/11 e só resta a cena de treino
  interna dos testes.
- **Jogo rápido: buscar partida automaticamente** (pedido em 17/11): um
  botão que põe o jogador numa partida aberta (ou cria uma) sem passar
  pela lista de salas, equilibrando times e patente. Pede fila e
  pareamento no servidor.
- **Modo espectador** (pedido em 17/11). Hoje a caixa "Espectador" da
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
- **Sistema de bag** (pedido em 17/11). A definir pelo Jhon o que a bag
  guarda: a `UI.md` cita a aba `BAG` da loja do WolfTeam, mas aqui o jogo
  ainda não tem bag. Perguntas: é a mochila do inventário (limite de
  espaço, itens de consumo), ou um item de equipar com efeito?
- **Health packs** (pedido em 17/11): item que devolve vida. A definir:
  achado no mapa (ponto fixo que volta depois de um tempo), carregado
  como consumível (na bag) ou comprado no respawn com a moeda de partida.
  A vida é única entre humano e lobo (`DESIGN.md`, "Vida é uma só"), então
  o pacote cura os dois.
- **Badge packs** (pedido em 17/11). A definir pelo Jhon: pacote de
  insígnias/emblemas para o perfil e a patente, ou outra coisa. Sem
  definição ainda; não dá para estimar.
- **Outro nome para as tatuagens** (pedido em 17/11): hoje são bônus
  pequenos de atributo, no máximo duas por jogador (`Profile.MaxTattoos`),
  e o nome vem da loja do WolfTeam. Junto com o sistema de armaduras
  (item abaixo): decidir se as duas viram a mesma coisa, num único
  sistema de equipamento com nome próprio.
- **Outro nome para Sangue e para WP** (pedido em 17/11). WP (Weapon
  Points) é termo do WolfTeam e aparece em cerca de 40 pontos do código,
  nos textos das telas e em `CONJUNTOS.md`; Sangue é a moeda paga. Trocar
  é só nome, mas toca na interface inteira, no servidor e nos documentos:
  melhor decidir antes de fazer a internacionalização, para o nome entrar
  já nas tabelas de tradução. Presas (a moeda de jogar) fica a decidir
  junto, para os três nomes combinarem.
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

- **Sistema de contas.** Hoje: criar conta e entrar já existem (aba
  Online + servidor). Falta: e-mail, confirmação, recuperar senha, trocar
  senha, apagar conta (LGPD), entrar logo ao abrir o jogo.
- **Cliente do jogo onde o jogador entra na conta e dá play: o login fica
  na primeira tela do próprio jogo** (decidido pelo Jhon, 13/11). Sem
  launcher separado. A tela de entrada (`WelcomeScreen`) vira a de
  login; o jogo offline com bots continua acessível (treino).
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

### Alcance: idioma e acessibilidade (pedido em 17/11)

- **Internacionalização.** Hoje: todo texto está escrito direto no código,
  em português; não há tabela de tradução. A fazer: tirar os textos para
  as tabelas de tradução do Godot (um arquivo por idioma), escolher o
  idioma nas Opções, começar por português e inglês. Cuidar de: fonte
  (acentos e, depois, outros alfabetos), texto que cresce e estoura botão,
  formato de número e data, as frases e a voz do rádio. Quanto antes,
  melhor: toda tela feita com texto fixo é tela a refazer. As telas em
  HTML (plano do `GUIA_UI_HTML_CEF.md`) precisam da própria tradução.
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

- **Ranking que dura anos: ~8 anos de jogo até a última patente.**
  Hoje: 30 patentes, a última com ~150 mil XP; uma partida dá ~350 XP,
  então ~430 partidas (~70 horas) chegam ao topo. Para 8 anos, a curva
  precisa de outra conta: definir quanto o jogador típico joga por dia
  (por exemplo, 5 partidas por dia × 8 anos ≈ 14.600 partidas ≈ 5 milhões
  de XP) e, provavelmente, mais patentes. A curva fica em `Ranks.XpFor`
  e no servidor.

- **Revisar as patentes** (pedido em 17/11): nomes, quantidade, curva de
  XP e imagens, junto com a conta dos 8 anos acima.

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
  na mesma máquina; o teste de carga está em `tools/testar_carga.sh`.

## Pendências técnicas conhecidas

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
- **Modelos avulsos do Sketchfab** (AK-47, Beretta, SG552, UMP45, Benelli
  M3) baixados e não usados.
