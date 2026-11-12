# Guia de HUD e UI

> Guia de referência de interface escrito pelo Jhon em 2026-11-12 e
> implementado na v0.24.0. É documento interno de design: os nomes de
> outros jogos aparecem só como comparação e **não entram no jogo** —
> veja "Propriedade intelectual" no `CLAUDE.md`.
>
> O que foi implementado, em que arquivo, e onde o jogo diverge do guia
> está em [Implementação](#implementação-v0240), no fim.

## Fluxo de telas

O modelo do WolfTeam é **lobby persistente**: uma moldura fixa (header + barra de ícones) que nunca sai, e um miolo que troca de conteúdo. Loja e inventário não são telas separadas no sentido de navegação — são o miolo trocando, com o header intacto. Isso importa na arquitetura: você tem uma cena `Shell` permanente e cenas de conteúdo instanciadas dentro dela.

- **Login** → autenticação, notícias, seleção de personagem se houver.

- **Seleção de canal** → lista de canais com ocupação e, às vezes, restrição de patente. Pode ser pulada no começo do seu projeto.

- **Lista de salas** → o hub. Daqui o jogador abre loja, inventário, perfil, ranking, clã sem sair do canal.

- **Sala** → espera, configuração, times, chat, contagem regressiva.

- **Partida** → HUD de combate. Única tela que esconde a moldura do lobby.

- **Resultado** → veredito, placar, recompensa animada.

- **Volta à sala**, não à lista. Manter o grupo junto é o que segura a sessão longa.

O ciclo que você quer otimizar é o 3 → 4 → 5 → 6 → 4. Cada atrito ali custa partidas jogadas.

## Header persistente

O header é a única parte da UI que nunca sai da tela fora da partida — ele é a identidade do jogador e o acesso ao dinheiro. Em FPS de lobby da escola do WolfTeam ele ocupa uma faixa de 56–72 px no topo, dividida em três zonas: identidade à esquerda, moedas à direita, navegação global entre as duas ou logo abaixo.

| Zona | O que o WolfTeam coloca ali |
|---|---|
| Extrema esquerda | Três molduras pequenas: logo/emblema, ícone de clã, insígnia de classe |
| Identidade | Duas barras finas empilhadas, `P.T Bonus` e `GP`, com o nickname embaixo em letra pequena |
| Moedas | Três linhas rotuladas: `GOLD : G`, `WC : WC`, `CASH : C`, valores alinhados à direita |
| Promo | Dois botões coloridos fixos: `PRIDE CHAIN` (verde) e `WOLF PASS` (roxo) |
| Navegação | Fileira de ícones à direita: evento, ranking, loja, inventário, clã, caixa, amigos, estatísticas, região, configurações, e um `«` que recolhe a barra |

Três detalhes dessa barra valem copiar. O primeiro é que ela é **uma faixa só, com divisórias verticais**, não blocos soltos — isso dá a sensação de cockpit. O segundo é que as moedas vêm com a sigla repetida na frente e atrás do número (`GOLD : 43000 G`), redundância que ajuda quando são três moedas parecidas. O terceiro é o `«` no canto, que recolhe a barra de ícones: numa UI tão densa, dar ao jogador o poder de esconder parte dela é o que impede a tela de sufocar.

**Sobre as moedas e o WP.** O WolfTeam tem três moedas persistentes no header — GOLD, Wolf Coin e Cash — e isso é demais; cada uma exige que o jogador aprenda uma regra própria. Para o seu jogo, duas bastam: uma ganha jogando e uma comprada. **WP não é moeda de header.** WP é o custo do loadout: cada arma tem um preço em WP (`WP 160` no fuzil, `WP 10` na pistola, `WP 0` na faca padrão) e cada preset soma um total (`WP 1 · WP 160`). Você ganha WP durante a rodada com abates e assistências e gasta ao renascer, como o dinheiro de Counter-Strike. WP pertence ao HUD de combate e à tela de loadout, nunca ao header do lobby.

As patentes aparecem como **insígnia em moldura quadrada de ~32 px**, nunca como texto. No WolfTeam são 62 patentes, cada uma com nome e insígnia própria, ganhas por experiência acumulada. Com esse volume, a insígnia precisa distinguir famílias por forma e cor antes do detalhe fino — nas telas dá pra ver barras, galões, diamantes e coroas como famílias visuais distintas.

**Regras de implementação**

- Número de moeda sempre com separador de milhar e alinhado à direita, com largura fixa — senão o header "pula" quando o valor muda.

- Toda mudança de valor anima: conta de A até B em ~400 ms, com flash verde (ganho) ou vermelho (gasto). É o detalhe mais barato que mais faz parecer profissional.

- Patente, nível e barra de XP são um único cluster clicável que abre o perfil. Não espalhe.

- Nickname longo trunca com reticências, nunca quebra linha nem empurra o resto.

## Lista de salas

A lista de salas é a tela onde o jogador passa mais tempo fora da partida, e é onde a maioria dos jogos indie erra. O objetivo dela é um só: **decidir em menos de 3 segundos em qual sala entrar**. Tudo que não ajuda nessa decisão é ruído.

O padrão do gênero é uma grade de 2 colunas × 5 linhas por página (10 salas), com paginação, em vez de scroll infinito. Isso não é limitação técnica antiga — é escolha boa: posição estável entre atualizações, e o olho varre uma página inteira sem rolar.

**Anatomia de uma linha de sala**

| Campo | Formato | Observação |
|---|---|---|
| Número | `#12` | Identidade curta pra chamar no chat ("entra na 12") |
| Estado | Ícone + cor | Aguardando (verde), Em jogo (vermelho/cinza), Cheia (âmbar), Com senha (cadeado) |
| Título | Texto livre do host | Trunca. É onde o jogador escreve a regra da casa |
| Modo | Chip | Mata-mata, Equipe, Destruição, Sobrevivência |
| Mapa | Miniatura 48×48 + nome | A miniatura vale mais que o nome |
| Lotação | `6/16` | Com barra fina embaixo, mais rápido de ler que o número |
| Regra de transformação | Ícone | Específico do seu jogo: lobo liberado, só humano, só lobo |
| Faixa de patente | `Recruta–Sargento` | Só quando o host limitou |
| Ping | 3 barras | Cor, não número |

**Controles da tela**

- `Criar sala` e `Jogo rápido` em destaque, no canto superior direito da lista. São os dois botões primários; todo o resto é secundário.

- `Atualizar` com timestamp da última atualização, mais auto-refresh a cada 5 s enquanto a tela está visível.

- Filtros em linha: modo, mapa, "só com vaga", "esconder em jogo", "esconder com senha".

- `Ir para sala` por número, pra quem combinou no chat.

- Seletor de canal/servidor, geralmente numa barra lateral ou dropdown no topo, mostrando ocupação de cada canal.

**Painel lateral**

À direita ou abaixo fica o chat do lobby, a lista de amigos online e o clã. No WolfTeam e em todo lobby FPS da época esse painel é o que segura a comunidade — é ali que as salas se formam de verdade. Se seu jogo for ter poucos jogadores no início, esse painel importa mais que a lista.

**Erros clássicos**

- Linha de sala alta demais. Alvo: 48–56 px. Mais que isso e cabem 5 salas na tela.

- Estado comunicado só por cor. Sempre cor + ícone + texto.

- Duplo clique como única forma de entrar. Tenha um botão `Entrar` visível na linha em hover.

## Tela de sala (pré-partida)

A tela de sala do WolfTeam divide a largura em **três colunas**: times à esquerda (duas listas lado a lado), painel de configuração à direita, e uma faixa inferior com chat e info do usuário. O título `ROOM` fica no canto superior esquerdo e o canal (`Free Channel 1`) no superior direito — orientação constante, custo zero.

**Slots de jogador**

`REDTEAM` e `BLUETEAM` com cabeçalho grande na cor do time, contagem `3/8` ao lado, e um botão de troca de lado entre os dois. Cada slot ocupado é uma linha baixa com:

- Duas molduras pequenas à esquerda: emblema de clã e insígnia de patente

- Marcador de host: um ícone de cabeça de lobo onde ficaria a patente

- Chevrons verdes `»` indicando pronto — quem não está pronto simplesmente não tem o chevron

- Nome do clã acima do nickname, nickname alinhado à direita

- Ping em `ms` na ponta direita

- Insígnia de patente + nickname

- Coroa ou marcador no host

- Estado: `Pronto` (verde) ou `Aguardando` (cinza)

- Ping

- Ícone de clã, se houver

- Preview pequeno do personagem, se o jogo tiver customização visual

Slots vazios ficam como linhas listradas, visíveis e clicáveis. O contador regressivo de início de partida aparece como um **círculo grande no meio da área de slots**, sobrepondo o vazio — bom uso de espaço morto, e impossível de não ver.

**Centro da tela**

No WolfTeam a configuração não fica no centro: fica num **painel de formulário à direita**, com rótulo à esquerda e valor em caixa à direita, uma linha por regra. As linhas são `TITLE`, `PASSWORD`, `MODE`, `PLAYER` (8 vs 8), `TEAM`, `MISSION` (100 Points), `TIME` (10 min.), `WEAPON` (All / Normal + Heavy), `LIMIT CLASS` (duas insígnias marcando a faixa de patente permitida), mais três caixas de marcação: `SWITCH OFFENSE AND DEFENSE`, `TRESPASS`, `OBSERVE`.

Abaixo do formulário vem o `MAP`: miniatura larga do mapa com o nome embaixo (`Arrival Night`) e um botão `SETTING` no canto. E abaixo ainda, dois banners promocionais com `BUY NOW` — caixa de evento e boost de GP.

Esse formato de formulário é melhor que chips espalhados: o jogador lê as regras da sala de cima a baixo numa varredura só. Copie a estrutura. **Não copie os banners de loja dentro da sala** — é monetização agressiva ocupando o lugar onde deveria estar informação de partida.

**Rodapé**

- Chat ocupando toda a largura inferior esquerda, com abas de canal (`ALL`, `PRIDE`, `SHOUTING`). Separar chat de clã do chat geral por aba é o detalhe que faz a comunidade funcionar.

- Painel `USER INFO` à direita do chat, que preenche ao clicar num jogador da lista.

- `INVITE` (secundário, cinza) e `START` (primário, laranja sólido) no canto inferior direito da área de times, bem distantes de qualquer coisa destrutiva.

- Ticker no rodapé com resultados de partidas recentes do servidor — sensação de mundo vivo por quase nada de esforço.

**Específico do seu jogo**

Se a transformação for escolha de classe (as 8 mutações de lobo do WolfTeam, por exemplo, com tatuagens dando bônus de atributo), a sala é onde essa escolha acontece. Reserve um painel para a seleção de mutação e loadout, visível para o dono e resumido no slot dos outros — saber que o time tem três lobos rápidos e nenhum tanque muda a decisão de quem ainda vai escolher.

## Inventário

O inventário do WolfTeam é **duas colunas**: `EQUIPMENT STATEMENT` à esquerda (o que está equipado) e `INVENTORY` à direita (o que você possui). Nada de paper doll em cima de personagem 3D — aqui é lista contra grade, e funciona melhor do que parece.

**Coluna esquerda: os presets de loadout**

No topo, quatro abas: `Standard`, `WP 1`, `WP 2`, `WP 3`, cada uma mostrando o custo total embaixo do nome (`WP 160`). Essa é a peça central da economia — o jogador monta quatro conjuntos e escolhe um ao renascer, pagando o custo em WP.

Abaixo das abas, os slots do preset em coluna vertical: primária, secundária, faca, granada. Cada slot ocupado mostra a arte grande da arma, o nome, o tipo (`Basic Weapon`), o custo (`WP 150`) e a duração (`Permanent Use`). Slot bloqueado mostra um **cadeado centralizado** em fundo listrado — comunica "existe e você pode destravar", que é diferente de slot vazio.

**Slots típicos**

| Grupo | Slots |
|---|---|
| Armas | Primária, Secundária, Corpo a corpo, Granada/Especial |
| Visual | Cabeça, Rosto, Torso, Pernas, Costas |
| Bônus | Acessório, Item consumível, Tatuagem/mutação |

No seu jogo isso dobra: o lobo tem slots próprios (mutação, tatuagem, habilidade) que não existem no humano. Resolva com **abas Humano / Lobo** sobre o mesmo paper doll, nunca com os dois conjuntos na mesma tela.

**A grade de itens**

- **Dois níveis de aba.** Primeiro nível por natureza do item: `WEAPON`, `POWER`, `CHAR.`, `EQUIP`, `EX ITEM`. Segundo nível por subtipo dentro do primeiro: `ALL`, `PRIMARY`, `2nd`, `KNIFE`, `GRENADE`, `STICKER`. A aba ativa recebe sublinhado amarelo. Dois níveis é o certo para esse volume; três seria demais.

- **Card de item grande e horizontal**, não ícone quadrado: arte da arma ocupando a metade de cima, e um bloco de texto embaixo com nome à esquerda, e à direita o tipo, o custo em WP e a duração.

- **Estado por rótulo e por moldura.** Item equipado mostra `Installing` em branco no canto inferior esquerdo. Item especial ganha **moldura roxa com brilho**. Item EX tem selo laranja no canto. Item com upgrade disponível mostra `UPGRADE` com quatro quadradinhos vazios sobre a arte.

- **Duração escrita por extenso** (`Permanent Use`, `1 D`), não só número.

- Painel de detalhe no canto inferior direito com abas próprias: `DESCR.`, `STATS`, `UPGRADE`, mais uma lupa para ampliar a arte.

**Painel de detalhe**

Ao selecionar um item, um painel mostra: nome, raridade, duração restante, atributos com barras comparativas (dano, precisão, cadência, peso), descrição curta, e os botões `Equipar`, `Renovar`, `Descartar`. As barras comparativas devem mostrar o delta contra o item atualmente equipado — é a única forma de o jogador decidir sem abrir planilha.

**Erro comum**

Tratar o inventário como lista de texto. Olhe a proporção nas telas de referência: a arte da arma ocupa mais de metade do card, e o texto é pequeno e secundário. Nesse gênero o inventário é parte da fantasia de poder. Se o seu couber numa `ItemList` do Godot sem customização, está subaproveitado.

O outro erro é não dar estado visual suficiente. Conte quantos estados diferentes um card carrega naquela tela: possuído, equipado (`Installing`), especial (moldura roxa), EX (selo), com upgrade pendente, permanente ou alugado. Seis camadas de informação num card de 200 px, e ainda legível.

## Loja

A loja é o inventário invertido: mesma grade, mesmo card de item, mas com preço e seletor de duração no lugar da duração restante. Reaproveite os componentes — se a loja e o inventário parecem telas de jogos diferentes, o jogador sente.

**Estrutura**

A loja do WolfTeam (`WOLF COIN SHOP`) repete a estrutura do inventário, o que é certo: mesmo esqueleto, mesma gramática, custo cognitivo zero para quem já conhece o inventário.

- **Coluna esquerda: ****`CHARACTER`** — modelo 3D do personagem em tamanho grande, com dois botões pequenos no topo alternando humano e lobo, e um painel `STATS` flutuante no canto inferior com uma lista de atributos em barras percentuais (`GP 30%`, `G 30%`, e seis outros com ícone em vez de nome). O personagem ocupa um terço da tela inteira, e é o que dá peso à loja.

- **Abas de primeiro nível**: `HOT`, `DAILY`, `WEAPON`, `POWER`, `CHAR.`, `EQUIP`, `EX ITEM`, `SET ITEM`. As duas primeiras são comerciais (destaque e rotativo diário), o resto espelha as categorias do inventário.

- **Abas de segundo nível** dentro da categoria: `ALL`, `BAG`, `TATTOO`, `SCRATCH`, `ETC.`

- **Grade de 3 colunas** de cards grandes, com rolagem.

**Card de produto**

Card horizontal com a arte do item centralizada, **selo da moeda no canto superior direito** (`WOLF COIN` em caixa laranja), nome no canto inferior esquerdo, e preço no canto inferior direito no formato `duração : valor moeda` — `1 D : 899 WC`. O selo da moeda no topo é a decisão mais inteligente da tela: o jogador filtra visualmente o que pode pagar antes de ler qualquer preço.

Para o seu jogo, mantenha esse selo mas reduza para duas moedas. E tome cuidado com o que o WolfTeam faz aqui: essa categoria `POWER > TATTOO` vende bônus de atributo por dinheiro. É pay-to-win explícito, e foi a crítica mais constante ao jogo. Se quiser um sucessor que sobreviva, venda aparência e conveniência, não poder.

**Seletor de duração**

É o controle que define a economia do gênero. O WolfTeam resolve de forma rudimentar — cada card é uma SKU fixa de duração (`1 D : 899 WC`), então o mesmo item aparece várias vezes na grade com durações diferentes. Não copie isso: polui a lista.

Faça melhor com uma linha de botões `1d · 7d · 30d · 90d · Perm` dentro do card de detalhe, com o preço atualizando ao vivo e o **preço por dia** embaixo em letra menor. O desconto por volume precisa ficar visível, e a melhor opção ganha um selo de `melhor valor`.

**Confirmação de compra**

Modal com: item, duração, preço, saldo atual, saldo depois da compra. Botão primário `Confirmar`, secundário `Cancelar`. Se faltar saldo, o botão vira `Recarregar` e leva pra tela de compra de Cash.

**Duas regras honestas de design**

- Nunca deixe `Confirmar` no mesmo lugar onde estava `Comprar` no card. Posição diferente evita compra por inércia.

- Mostre sempre o saldo depois da compra. Loja que esconde isso é desenhada pra arrepender, e o jogador percebe.

## HUD de combate

O HUD in-game segue a regra dos cantos: informação de sobrevivência embaixo à esquerda, recursos embaixo à direita, estado da partida em cima, eventos à direita no meio. O centro fica livre — só mira e avisos críticos.

| Canto | Elementos |
|---|---|
| Superior centro | Placar dos times, cronômetro, modo |
| Superior esquerdo | Radar/minimapa, nome do mapa |
| Superior direito | Kill feed |
| Centro | Mira, hitmarker, indicador de dano direcional, avisos de rodada |
| Inferior esquerdo | Vida, armadura, estado de transformação |
| Inferior direito | Arma atual, munição `pente/reserva`, slots, granadas |
| Inferior centro | Barra de transformação, cooldowns de habilidade |

**O que é específico do seu jogo**

A transformação é o seu diferencial, então ela merece o espaço nobre. No WolfTeam a troca entre humano e lobo não é instantânea — leva alguns segundos durante os quais o jogador fica vulnerável. Isso é uma decisão de design que o HUD precisa comunicar com precisão:

- **Barra de transformação** no centro-inferior, acima da linha de munição. Cheia = pode transformar. Durante a transformação ela vira uma barra de canalização com contagem regressiva e um pulso visual, porque esse é o momento de maior risco da partida.

- **O HUD troca de forma.** Humano mostra munição, pente, granadas. Lobo mostra cooldowns de garra, salto e rugido, e nenhuma munição. Trate como **duas cenas de HUD distintas** com transição animada, não como `Visible = false` em dez nós.

- **Estado de forma sempre visível** mesmo em primeira pessoa, com um ícone persistente ao lado da vida. Parece redundante; não é, em partida rápida.

**WP na tela**

Se você adotar o modelo de economia de rodada do WolfTeam — ganhar moeda por kill e assistência e gastar ao renascer escolhendo entre loadouts, com o saldo zerando no fim da rodada — então o saldo precisa aparecer no HUD, perto da munição, e ganhar um pop animado a cada kill. E a tela de respawn vira uma tela de escolha: os loadouts com o custo, os que você não pode pagar em cinza.

**Detalhes que separam amador de profissional**

- Mira com spread dinâmico que abre ao correr e atirar. Custa pouco e informa muito.

- Hitmarker com som e escala; som diferente para headshot e para abate.

- Vida com dois estados visuais: abaixo de 30% a barra pulsa e a vinheta vermelha nas bordas entra.

- Kill feed com ícone da arma e cor por time, linhas somem em 5 s, máximo 5 linhas.

- Dano direcional como arco apontando a origem, desaparecendo em 1,5 s.

- Nada do HUD escala com FOV ou balança com a câmera. `CanvasLayer` sempre.

## Placar e fim de partida

**Placar (Tab)**

Sobreposição com os dois times em blocos separados, cabeçalho do time com nome, cor e pontuação agregada. Colunas por jogador:

| Coluna | Largura | Nota |
|---|---|---|
| Patente | 32 px | Só a insígnia |
| Nickname | flexível | Destaque no jogador local, linha com fundo diferente |
| Abates | 48 px |  |
| Mortes | 48 px |  |
| Assistências | 48 px |  |
| Pontos | 56 px | A coluna de ordenação |
| Forma | 32 px | Ícone humano/lobo — específico do seu jogo |
| Ping | 40 px | Barras |

Ordene por pontos, decrescente. Mostre jogador morto com nome esmaecido. Se tiver killing spree, mostre o contador ao lado do nome — o WolfTeam tem sistema de sequência de abates, e exibir isso no placar aumenta a pressão social que faz o modo funcionar.

**Fim de partida**

A tela de resultado tem uma função só: fazer o jogador querer jogar de novo. Estrutura em três tempos:

- **Veredito** — `Vitória` ou `Derrota` grande, com placar final. 1,5 s sozinho na tela.

- **Placar completo**, com MVP destacado no topo.

- **Recompensa animada** — XP ganho enchendo a barra de patente, moeda ganha contando pra cima, e o aviso de promoção se houver. Essa animação é o momento mais importante da tela inteira; não a pule nem a acelere.

Rodapé com `Jogar de novo`, `Voltar à sala`, `Sair para o lobby`. O primeiro é o botão primário e deve estar sob o cursor por padrão.

## O que copiar e o que não copiar

Vale separar as duas coisas antes de você começar a desenhar, porque a linha é clara e cara de descobrir depois.

**Pode usar à vontade — é convenção de gênero, não propriedade de ninguém**

- A arquitetura de telas: canal, lista de salas, sala, partida, resultado.

- A posição dos elementos: vida embaixo à esquerda, munição embaixo à direita, kill feed à direita.

- Os sistemas: aluguel de item por duração, duas moedas, patentes com insígnia, loadouts pagos com moeda de rodada.

- As colunas do placar e da lista de salas.

- A ideia de transformação humano/lobo como mecânica.

**Não copie — é design autoral da Softnyx**

- As molduras, painéis, botões e texturas do WolfTeam.

- Os ícones de arma e de item, as insígnias de patente específicas deles.

- O logo, o wordmark, a fonte do título, a silhueta do lobo deles.

- Os nomes próprios: nomes de patentes, de mutações, de mapas, de armas.

- A paleta exata e a identidade visual como conjunto reconhecível.

O teste prático: se alguém que jogou WolfTeam abrir seu jogo e disser "isso é WolfTeam" olhando só para a arte, você passou da linha. Se disser "isso me lembra WolfTeam" olhando para a mecânica, está exatamente onde um sucessor espiritual deve estar.

Isso não é só jurídico — é bom pro projeto. Arte própria é o que faz o jogo ter identidade em vez de parecer clone.

## O que faz uma UI parecer profissional

Não é addon e não é arte cara. São seis coisas, nesta ordem de impacto:

**1. Grade e ritmo.** Escolha uma unidade base (8 px) e derive todo espaçamento dela: 8, 16, 24, 32, 48. Margens iguais, alturas de linha iguais, alinhamentos consistentes. UI amadora se reconhece por espaçamentos aleatórios — 13 px aqui, 17 px ali. Isso sozinho responde por metade da percepção de qualidade.

**2. Hierarquia.** Em cada tela deve haver exatamente um elemento primário. Se tudo brilha, nada brilha. Botão primário: cor de destaque, sólido. Secundário: contorno. Terciário: só texto. Nunca dois primários na mesma tela.

**3. Tipografia.** Duas famílias no máximo: uma de display condensada para títulos, patentes e números grandes; uma neutra e legível para o resto. Números de HUD precisam de **fonte tabular** (largura fixa por dígito), senão a munição treme ao contar. Escala de no máximo cinco tamanhos.

**4. Paleta restrita.** Uma cor de marca, uma de perigo, uma de sucesso, e uma escala de cinzas de 6 a 8 passos. As raridades de item ganham a própria escala, separada. Resista a inventar cor nova por tela.

**5. Estados completos.** Todo controle tem cinco: normal, hover, pressionado, desabilitado, focado. O foco importa de verdade — sem ele o jogo não funciona com gamepad, e jogo de lobby sem navegação por gamepad parece inacabado.

**6. Movimento.** Nada aparece ou some instantaneamente. Transições de 120 a 200 ms com curva de saída. Número que muda, conta. Painel que abre, desliza. Essa camada é o que mais aproxima de produto comercial e é a mais barata de todas no Godot com `Tween`.

**E a densidade.** Lobby de FPS coreano é denso de propósito: muita informação por tela, porque o jogador veterano quer decidir rápido. Não confunda denso com bagunçado — denso com grade rígida é profissional, denso sem grade é amador. Se for seu primeiro projeto de UI, comece menos denso e aumente.

## Design tokens do projeto

Defina isso antes de abrir o Godot. São os valores que vão para o `Theme` e para as constantes do código.

**Espaçamento** — base 8: `4 · 8 · 16 · 24 · 32 · 48 · 64`. Nada fora dessa lista.

**Raio de canto** — `2` para controles pequenos, `4` para painéis, `0` em elementos de HUD. Estética militar/sombria pede canto duro.

**Tipografia**

| Papel | Tamanho | Peso | Uso |
|---|---|---|---|
| Display | 48 px | Bold, condensada | Veredito de partida, números grandes de HUD |
| Título | 28 px | Bold | Cabeçalho de tela |
| Subtítulo | 20 px | Semibold | Nome de seção, nome de item |
| Corpo | 15 px | Regular | Descrições, chat |
| Rótulo | 13 px | Medium, caixa alta, +5% de espaçamento | Cabeçalho de coluna, chips |
| Numérico | tabular | Medium | Munição, moeda, placar |

**Paleta**

| Papel | Nota |
|---|---|
| Fundo | Cinza quase preto, levemente azulado ou esverdeado — não preto puro |
| Superfície | Dois níveis acima do fundo, para painéis e cards |
| Borda | Cinza médio, 1 px, baixa opacidade |
| Texto primário / secundário / desabilitado | Três níveis de cinza claro |
| Destaque | A cor da sua marca. Âmbar ou vermelho-sangue combinam com a fantasia de lobisomem |
| Perigo / Sucesso / Aviso | Vermelho, verde, âmbar |
| Time A / Time B | Duas cores distinguíveis por daltônicos — azul e laranja funcionam melhor que vermelho e verde |
| Raridade | Escala própria de 5 passos, independente do resto |

**Altura de componentes** — botão 40 px, campo 36 px, linha de sala 52 px, linha de placar 32 px, header 64 px, slot de inventário 64 px.

**Resolução de referência** — projete em 1920×1080 e defina em Project Settings o stretch `canvas_items` com aspect `expand`. Teste em 1280×720 e em ultrawide antes de considerar qualquer tela pronta.

## Implementação no Godot 4 com C#

**Estrutura de cenas**

`Componente = cena `.tscn` com script C# e `[Export]` para os dados. Tela = cena que compõe componentes. Nenhuma tela desenha controle do zero.

**Ordem de construção** — não comece pelo bonito:

- `Theme` com os tokens da seção anterior. Antes de qualquer tela.

- Os 6 componentes base (botão, barra, slot, insígnia, moeda, linha de lista).

- Uma tela cinza completa com dados falsos — layout e navegação funcionando, zero arte.

- Fluxo de navegação entre todas as telas.

- Só então a arte: molduras, ícones, texturas.

- Por último o movimento: `Tween` em tudo.

Quem inverte 5 e 3 refaz a UI inteira. É o erro mais caro desse tipo de projeto.

**Regras técnicas**

- HUD sempre em `CanvasLayer`. Nunca filho da câmera.

- Zero leitura de estado no `_Process` do HUD. Use eventos C# ou `[Signal]` disparados quando vida, munição ou moeda mudam.

- Âncoras e `Container` para tudo. Posição absoluta só em elementos realmente fixos, como a mira.

- `TextureProgressBar` para barras; aceita modo radial se quiser barra circular de transformação.

- Radar e indicador de dano via `_Draw()` em vez de `SubViewport` — mais leve e mais fácil de estilizar.

- HUD humano e HUD lobo como cenas separadas, trocadas com transição. Não alterne `Visible`.

- Um `UiState` central (autoload) que guarda o estado da sessão. As telas leem dele, não umas das outras.

- No multiplayer, o HUD só enxerga o peer local. Isole isso desde o primeiro dia.

**Sobre addons**

Maaack's Menus Template resolve menu principal, opções, pause e carregamento de cena — vale instalar e economizar uma semana. Lista de salas, inventário, loja e HUD de combate são específicos demais: faça à mão. Lembre que quase todo addon de Godot é GDScript, então você vai acessá-lo por `GetNode` e sinais, sem tipagem forte.

## Especificação funcional: conta, social e canais

**Conta e sessão**

- Login com e-mail e senha, sessão persistente com token, e "lembrar de mim".

- Nickname único, 3 a 16 caracteres, imutável sem item de troca de nome — item clássico de loja, e uma das poucas monetizações de que ninguém reclama.

- Tela de notícias no login: banner rotativo, notas de versão, evento ativo.

- Detecção de sessão duplicada: ao logar em outro lugar, a sessão antiga cai com aviso claro.

**Perfil do jogador**

Registro persistente com: nickname, patente, XP acumulado, saldo das duas moedas, e estatísticas vitalícias — partidas, vitórias, derrotas, abates, mortes, assistências, taxa de vitória, K/D, tempo jogado, abates como humano, abates como lobo, tempo em cada forma, arma favorita, classe de lobo favorita, data de criação, matilha atual e histórico de patentes.

A tela abre em dois lugares: no seu próprio header, e ao clicar no nome de outro jogador em qualquer lista. Perfil alheio mostra tudo menos saldo e inventário.

**Amigos**

- Lista com estado: offline, no lobby, em sala, em partida.

- Pedido de amizade com aceite, bloqueio e remoção.

- `Seguir` — entrar direto na sala onde o amigo está, se houver vaga e não tiver senha.

- Convite para sala, que chega como notificação clicável.

- Mensagem direta.

**Matilha (clã)**

- Criação com custo em moeda do jogo, nome e tag de 2 a 4 caracteres exibida antes do nickname em toda lista.

- Hierarquia: Alfa (líder), Beta (oficial), Membro. O Alfa promove, rebaixa, expulsa e dissolve.

- Emblema escolhido de um catálogo, com slots extras vendidos na loja.

- Canal de chat próprio, sempre disponível como aba no chat do lobby.

- Painel de matilha: membros online, XP coletivo, nível da matilha, histórico de partidas.

- Guerra de matilhas: desafio entre duas matilhas, sala fechada, resultado registrado no histórico de ambas.

**Canais**

Separação do público por faixa de patente e por propósito: `Novatos` (limitado a patentes baixas, proteção contra smurf), `Livre`, `Veterano`, `Competitivo`. Cada canal mostra ocupação e capacidade. No começo do projeto, use um canal só — canal vazio é pior que canal lotado.

**Chat**

Abas de canal como no WolfTeam: `Geral`, `Matilha`, `Sussurro`, `Sistema`. Histórico de 200 linhas, filtro de palavrão opcional, e comandos `/w nick mensagem`, `/convite nick`, `/ignorar nick`. Mensagem de sistema em cor distinta para entradas, saídas, promoções e resultados.

## Especificação funcional: salas, modos e partida

**Criação de sala**

Todo campo aqui vira um filtro na lista e uma linha no painel da sala. O conjunto mínimo, espelhando o formulário do WolfTeam:

| Campo | Valores | Editável em sala |
|---|---|---|
| Título | Texto livre, até 24 caracteres | Sim |
| Senha | Vazio ou 4 a 8 caracteres | Sim |
| Modo | Mata-mata, Equipe, Dominação, Caçada, Sobrevivência | Sim, antes de iniciar |
| Tamanho | 4v4, 6v6, 8v8 | Só se ninguém ocupar os slots a remover |
| Mapa | Catálogo filtrado pelo modo | Sim |
| Objetivo | Pontos ou rodadas | Sim |
| Tempo | 5, 10, 15, 20 min | Sim |
| Armas permitidas | Todas, sem pesadas, só pistolas, só faca | Sim |
| Transformação | Livre, bloqueada, obrigatória, só um lado | Sim |
| Faixa de patente | Mínima e máxima | Sim |
| Fogo amigo | Liga/desliga | Sim |
| Troca de lado | Liga/desliga entre rodadas | Sim |
| Espectador | Permitir ou não | Sim |

**Regras de sala**

- O host é quem criou. Se sair, o host passa para o jogador mais antigo na sala — nunca dissolva a sala por saída do host.

- O host expulsa e bane por duplo clique no jogador, com confirmação. Jogador banido não volta naquela sala.

- Balanceamento automático opcional: ao iniciar, o servidor equilibra os times por patente se a diferença passar de um limiar.

- Contagem regressiva de 5 s ao iniciar, cancelável enquanto roda.

- Entrada em partida já começada (`join in progress`), configurável por sala, com o jogador entrando no time com menos gente.

**Modos de jogo**

- **Mata-mata** — todos contra todos, vence quem chegar ao limite de abates. Transformação livre.

- **Equipe** — dois times, limite de abates ou tempo. O modo padrão.

- **Dominação** — pontos de controle no mapa, pontuação por tempo de posse. Lobos dominam captura rápida, humanos defendem melhor à distância; o modo existe para forçar a troca de forma.

- **Caçada** — assimétrico: um time só de lobos sem armas, outro só de humanos com armas e munição limitada. Lobos vencem eliminando todos; humanos vencem sobrevivendo ao tempo. É o modo que vende o jogo.

- **Sobrevivência** — PvE por ondas, cooperativo, com lobos controlados por IA. Serve como tutorial longo e como conteúdo para horário vazio.

**Durante a partida**

- Respawn com tela de escolha de loadout, com os presets e seus custos em WP, e os inacessíveis em cinza.

- Tempo de respawn fixo por modo, ou por onda de respawn no modo Equipe.

- Espectador após a morte: câmera livre ou seguindo um aliado.

- Placar por `Tab`, chat por `Enter` (time) e `Shift+Enter` (todos), menu de pausa por `Esc` com opções, controles e `Sair`.

- Votação de expulsão durante a partida, com maioria simples e cooldown.

- Pedido de rendição a partir de metade do tempo, exigindo maioria do time.

- Reconexão: cair e voltar deve recolocar o jogador na mesma partida, no mesmo time, por até 2 minutos.

## Especificação funcional: combate, formas e classes

**A transformação**

É o sistema central. No WolfTeam a troca não é instantânea e deixa o jogador vulnerável por alguns segundos, e essa janela de risco é justamente o que torna a decisão interessante. As regras que você precisa definir, com os valores como ponto de partida:

| Parâmetro | Sugestão inicial | Por quê |
|---|---|---|
| Custo | Barra de energia cheia | Impede spam |
| Canalização | 1,5 s para virar lobo, 1,0 s para voltar | A janela de risco |
| Vulnerabilidade | Dano recebido +50% durante a canalização | Punição por transformar na hora errada |
| Duração | Ilimitada enquanto houver energia | Energia drena lentamente em forma de lobo |
| Drenagem | 3% por segundo, +5% por habilidade usada | Força o retorno |
| Retorno forçado | Energia zerada reverte para humano | Momento dramático, bom para o HUD |
| Recarga | 8% por segundo em forma humana, dobrada ao causar dano | Recompensa jogar bem como humano |
| Interrupção | Dano acima de um limiar cancela a canalização | Dá contrajogo aos humanos |

**Forma humana**

- Arma primária, secundária, faca e granada, vindos do preset de loadout.

- Recarga, mira de ferro, troca de arma, coronhada.

- Granadas: fragmentação, atordoamento, fumaça, e uma antilobo (prata, fogo — escolha sua) que impede transformação por alguns segundos numa área.

- Munição finita, com reposição em caixas no mapa ou ao abater.

**Forma de lobo**

- Sem armas de fogo. Ataque corpo a corpo em dois tipos: rápido e pesado.

- Três habilidades com cooldown independente, variando por classe.

- Mobilidade aumentada: corrida mais rápida, salto maior, escalada de paredes ou parkour.

- Recebe mais dano de certas munições, menos de outras. É a alavanca de balanceamento principal.

**Classes de lobo**

O WolfTeam tem 8 mutações, com tatuagens dando bônus de atributo. Para o seu jogo, comece com 4 e expanda — 8 classes mal balanceadas é pior que 4 bem resolvidas. Uma base possível, com nomes próprios seus:

| Classe | Papel | Atributos | Habilidades |
|---|---|---|---|
| Rastreador | Velocidade e flanco | Vida baixa, velocidade máxima | Investida, salto duplo, faro (revela inimigos próximos por 3 s) |
| Brutamontes | Linha de frente | Vida alta, lento | Pancada em área, rugido que atordoa, pele grossa |
| Espreitador | Emboscada | Vida média, furtivo | Camuflagem parcial, abate por trás com dano extra, salto silencioso |
| Alfa | Suporte | Equilibrado | Uivo que acelera aliados, cura regenerativa, marcação de alvo |

Cada classe tem slots de tatuagem para pequenos ajustes de atributo. **Venda tatuagem por moeda do jogo, não por dinheiro real** — foi exatamente aí que o WolfTeam virou pay-to-win, e é a crítica que mais pesou contra ele.

**Economia de rodada (WP)**

- Ganhos: abate, assistência, objetivo cumprido, vitória de rodada. Derrota de rodada dá um valor de consolação crescente, como em Counter-Strike.

- Gastos: ao renascer, escolhendo entre `Padrão` (grátis) e três presets com custo.

- Reset: o saldo zera no fim da rodada ou da partida, conforme o modo.

- Transformação pode ou não custar WP. Recomendo que não — misturar as duas economias confunde.

**Armas**

Cada arma tem: dano por acerto, multiplicador por região do corpo, cadência, pente, munição total, recuo horizontal e vertical, dispersão em pé, agachado e em movimento, tempo de recarga, tempo de troca, alcance efetivo, penetração, velocidade de movimento com ela equipada, dano contra lobo (multiplicador separado), e custo em WP.

Esse último par é o que faz o balanceamento do jogo existir: uma arma pode ser forte contra humanos e fraca contra lobos, e isso resolve metade dos problemas de equilíbrio sem nerfar nada.

## Especificação funcional: economia, itens e progressão

**Duas moedas, não três**

| Moeda | Como se ganha | O que compra |
|---|---|---|
| Presas (moeda do jogo) | Partidas jogadas, vitórias, missões, eventos | Armas por tempo, visual básico, tatuagens, criação de matilha, troca de nome |
| Sangue (moeda paga) | Compra com dinheiro real | Visual exclusivo, permanentes, conveniências, passe de temporada |

Regra que vale ouro: **tudo que afeta o combate deve ser comprável com a moeda do jogo**. A moeda paga compra aparência, tempo e conveniência. Não é moralismo; é sobrevivência do jogo.

**Itens e duração**

- Durações: 1, 7, 30, 90 dias e permanente, com preço por dia decrescente.

- Estados de item: possuído, equipado, expirando (menos de 3 dias), expirado, bloqueado por patente.

- Renovação de item expirado direto do inventário, com desconto se feito antes de expirar.

- Item expirado não some do inventário por 30 dias — aumenta renovação e reduz raiva.

- Categorias: arma primária, secundária, corpo a corpo, granada, visual humano (cabeça, rosto, torso, pernas), visual lobo (pelagem, olhos, marcas), tatuagem, consumível, emblema de matilha.

**Caixas e aleatoriedade**

Se você for usar caixa aleatória, mostre a tabela de probabilidade antes da compra. Em vários países isso já é lei, e mesmo onde não é, esconder as chances destrói a confiança. Alternativa melhor: caixa com pity timer visível, ou direto um catálogo de preço fixo.

**Progressão de patente**

- XP por partida: base por participação, mais abates, assistências, objetivos, bônus de vitória, bônus de MVP.

- Curva crescente, com as primeiras patentes rápidas (primeiras 3 em uma sessão) e as altas levando meses.

- Promoção com tela de celebração, recompensa em moeda do jogo, e anúncio no chat do canal.

- A patente destrava: canais, modos, armas na loja, e cargos em matilha.

- **Sem rebaixamento.** Patente de progressão não cai; se quiser rebaixamento, use um ranking competitivo separado com MMR, que é outra coisa.

**Missões**

- Diárias: 3 por dia, simples (jogue 3 partidas, 20 abates como lobo, vença 1 partida).

- Semanais: 5 por semana, mais exigentes, com recompensa maior.

- Permanentes: marcos vitalícios (1000 abates como lobo, 100 vitórias), com título exibível no perfil.

- Todas visíveis num painel próprio, com progresso em barra e recompensa resgatável manualmente — resgate manual dá o momento de recompensa.

**Ranking**

Quadros separados: XP da temporada, taxa de vitória, abates, abates como lobo, matilhas. Top 100 por quadro, atualizado de hora em hora, com a posição do próprio jogador sempre fixada no rodapé da lista.

**Temporadas**

Ciclo de 2 a 3 meses, com reset do ranking competitivo, recompensas por faixa atingida, e um passe com trilha gratuita e trilha paga. O passe é a monetização mais aceita hoje e mais previsível que caixa.

**Moderação e integridade**

- Denúncia dentro da partida e pelo perfil, com motivo: trapaça, ofensa, sabotagem.

- Detecção de inatividade (AFK) com expulsão automática e penalidade de tempo por reincidência.

- Penalidade por abandono de partida: perda de XP e bloqueio temporário de entrada.

- Validação de dano e posição no servidor. Em jogo com transformação, cliente autoritativo é convite a trapaça.

- Painel administrativo com busca por jogador, histórico, e ações de silêncio, suspensão e banimento.

## Escopo: o que entra agora e o que fica para depois

Acabei de descrever o jogo inteiro, e preciso ser direto com você: isso é trabalho de estúdio, não de uma pessoa nas noites e fins de semana. O WolfTeam tinha equipe dedicada e anos de operação. Se você tentar construir tudo isso antes de qualquer pessoa jogar, o projeto morre na metade — é o destino da maioria dos sucessores espirituais de FPS online.

A ordem abaixo existe para que você tenha algo jogável em semanas, não em anos.

| Fatia | O que entra | O que fica fora |
|---|---|---|
| **1. Protótipo jogável** | Movimento, tiro, uma arma, transformação com canalização e energia, uma classe de lobo, um mapa, bots ou hot-seat | Rede, UI de lobby, economia, progressão |
| **2. Multiplayer mínimo** | Sala única, modo Equipe, 4v4, lista de salas simples, HUD de combate completo | Loja, inventário, patentes, matilha, canais |
| **3. Primeiro loop** | Patentes, XP, moeda do jogo, inventário, 4 armas, 2 classes, 2 mapas, placar e tela de resultado | Moeda paga, caixas, temporadas, missões, ranking |
| **4. Retenção** | Missões diárias, amigos, chat com abas, mais classes e mapas, modo Caçada | Matilha, guerra de matilhas, passe |
| **5. Operação** | Loja paga, temporadas, ranking, matilha, moderação, painel admin | — |

**O teste da fatia 1.** Se a transformação não for divertida com um bot num mapa feio, nenhuma loja, patente ou matilha vai salvar o jogo. Gaste o tempo que for preciso ali, e só depois construa a UI deste guia.

**Sobre a UI especificamente.** Para a fatia 2, você precisa do HUD de combate e de uma lista de salas funcional e feia. O resto deste documento — inventário, loja, patente, perfil — é fatia 3 em diante. Construa o `Theme` e os componentes base desde já, porque eles são baratos cedo e caros depois, mas não desenhe tela que ainda não tem sistema atrás.

## Fontes

As descrições das telas vêm das capturas que você enviou (sala, loja com personagem, inventário). O resto:

- Wolf Team — review, MMOs.com — sistema de loadout e WP, transformação, mutações de lobo

- Wolf Team — review, MMOHuts — estrutura de lobby, tatuagens e mutações

- WolfTeam Aeria Wiki — Ranks — 62 patentes, nome e insígnia por patente

- Maaack's Menus Template — addon de menus para Godot 4

Os valores numéricos de balanceamento (canalização, drenagem, recarga de energia) são pontos de partida meus, não dados do WolfTeam — ajuste testando.


---

## Implementação (v0.24.0)

Escopo combinado: **tudo o que roda sem servidor**. Perfil, patentes,
XP, loja com moeda do jogo, inventário, missões e a sala inteira
funcionam e ficam salvos em `user://perfil.json`. O que depende de rede
aparece desabilitado com "em breve" ou não aparece.

| Seção do guia | Onde está |
|---|---|
| Design tokens | `scripts/ui/kit/UiTokens.cs` (grade, raios, cores, tempos) e `UiFonts.cs` (Barlow e Barlow Condensed, OFL, números tabulares) |
| Theme | `scripts/ui/kit/UiTheme.cs` — feito em código e aplicado por raiz (`UiTheme.Apply`), porque `CanvasLayer` corta a herança de tema |
| Componentes | `scripts/ui/kit/`: `UiKit` (texto, botão, campo, seletor, painel, chip), `Icon`, `Motion`, `RankBadge`, `CurrencyLabel`, `StatBar`, `WeaponArt`, `Tabs`, `Modal`, `CountdownRing`, `PingBars`, `Toast` |
| Fluxo de telas e header | `scripts/lobby/LobbyShell.cs` (moldura fixa, miolo trocando) e `LobbyHeader.cs` |
| Lista de salas | `RoomListScreen.cs` e `RoomRow.cs`; dados em `scripts/meta/RoomDirectory.cs` |
| Tela de sala | `RoomScreen.cs`; regras em `scripts/core/RoomConfig.cs` e `RoomRules.cs` |
| Inventário | `InventoryScreen.cs`, `ItemCard.cs`, `ItemDetail.cs`, `CharacterPreview.cs` |
| Loja | `ShopScreen.cs`; catálogo e preços em `scripts/meta/Catalog.cs` |
| Perfil, missões, opções | `ProfileScreen.cs`, `MissionsScreen.cs`, `OptionsScreen.cs` |
| HUD de combate | `scripts/ui/Hud.cs`, `Crosshair.cs`, `HitMarker.cs`, `DamageIndicator.cs`, `Minimap.cs` |
| Placar e topo da partida | `scripts/ui/MatchHud.cs` e `Scoreboard.cs` |
| Fim de partida | `scripts/ui/ResultScreen.cs` e `PromotionOverlay.cs`; recompensa em `scripts/meta/Progression.cs` |
| Respawn e Esc | `scripts/ui/LoadoutMenu.cs` e `GameMenu.cs` |
| Patentes | `scripts/meta/Ranks.cs` |
| Missões | `scripts/meta/Missions.cs` |

### Onde o jogo diverge do guia, e por quê

| Ponto | Guia | Jogo | Motivo |
|---|---|---|---|
| Patentes | ~62 | **30**, 10 famílias × 3 graus | A família se distingue por forma e cor antes do detalhe; 30 bastam para o primeiro ano de progressão |
| Moedas | duas | **Presas** (jogando) e **Sangue** (paga) | Sangue aparece no header mas não é vendido: compra paga precisa de servidor |
| Munição | `pente/reserva` | `pente / ∞` | Reserva infinita é regra antiga do Jhon: o custo de atirar é o tempo de recarga |
| Transformação | "alguns segundos" | 0,55 s para lobo, 0,2 s de volta | Mantida a atual, decisão do Jhon. A barra de canalização e o pulso existem do mesmo jeito |
| Lobo no HUD | garra, salto, rugido | garrada, patada, bote e uivo | São os ataques que o lobo tem (`Weapon.AttackCooldown`) |
| Ping no placar | barras | barras para você; ícone de bot para bot | Partida local não tem rede: inventar ping seria mentir no placar |
| Larguras do placar | 32/48/56/32/40 | 32/56/64/48/44 | O título da coluna em versalete não cabe em 48 px e desalinharia a coluna |
| HUD humano × lobo | cenas separadas | dois painéis trocados com transição | A UI é feita em código; o efeito é o mesmo — nenhum `Visible` espalhado em dez nós |
| Barras | `TextureProgressBar` | `ProgressBar` com `StyleBoxFlat` | Ainda não há arte de barra; trocar depois não muda quem usa |
| `_Process` no HUD | zero leitura | só o que é contínuo | Mira (dispersão), barra de transformação, recargas do lobo, uivo, relógio de renascer e vinheta são contínuos por natureza; o resto chega por sinal |
| `UiState` | autoload com a sessão | classe estática que conta menus | O estado da sessão está em `Profile`, `RoomConfig` e `RoomDirectory`; `UiState` só decide de quem é o mouse |
| Modos | Equipe, Caçada etc. | só **Equipe** jogável | Os outros aparecem na criação de sala como "em breve" |

### Fora do escopo (precisa de servidor)

Login de verdade, canais, amigos, matilha, chat entre jogadores,
ranking, compra paga, temporadas e moderação. O chat da sala existe e
mostra mensagens do sistema e as suas; ninguém responde.
