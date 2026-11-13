# Alcateia — as decisões de jogo

Cada sistema do jogo, com o que foi decidido e por quê: transformação,
controles, corrida na parede, granada, garras, escudo, som, o bot,
mapas, partida, times, arsenal, conjuntos, HUD, morte, personagens e
interface. Saiu do `CLAUDE.md` para que o guia dos agentes fique curto;
as **regras de design que não se violam** continuam lá.

> Os números de cada sistema, com o valor atual e onde mudar, estão em
> `docs/AJUSTES.md`. O código de cada um, em `docs/ARQUITETURA.md`.

## Sumário

- [Máquina de estados da transformação](#máquina-de-estados-da-transformação)
- [Controles](#controles)
- [Corrida em parede](#corrida-em-parede)
- [Granada](#granada)
- [O bot](#o-bot)
- [Mapas vindos de .glb](#mapas-vindos-de-glb)
- [O mapa Hangar](#o-mapa-hangar)
- [A partida](#a-partida)
- [Times](#times)
- [Do menu à partida](#do-menu-à-partida)
- [Arsenal](#arsenal)
- [Conjuntos e economia](#conjuntos-e-economia)
- [HUD de combate](#hud-de-combate)
- [Morte](#morte)
- [Armas na mão, soltar e pegar](#armas-na-mão-soltar-e-pegar)
- [Personagens](#personagens)
- [Fora da partida: perfil, patentes, loja e missões](#fora-da-partida-perfil-patentes-loja-e-missões)
- [Interface: o kit](#interface-o-kit)
- [O teste de mapa](#o-teste-de-mapa)

---

## Máquina de estados da transformação

```
Humano        --[tecla 3 + WP]--> VirandoLobo
VirandoLobo   --[0,8s]-->         Lobo
Lobo          --[tecla 1/2/4]-->  VirandoHumano
VirandoHumano --[0,8s]-->         Humano
```

| Estado | Hitbox | Ataca | Dano recebido | Física |
|---|---|---|---|---|
| Humano | humana | arma | normal | humana |
| VirandoLobo | humana | não | ×1,5 | **lobo** |
| Lobo | lupina | corpo-a-corpo | normal | lobo |
| VirandoHumano | lupina | não | ×1,5 | **humana** |

**A física e a câmera trocam no início da transição, não no fim.** Trocou de
personagem, trocou de velocidade, pulo e corrida em parede — sem
esperar a animação. Esperar o fim fazia o jogador apertar 3, pular na
sequência e receber o pulo humano, o que parece um bug mesmo sendo
intencional.

O que a transição tira é a capacidade de **atacar**, não a de se
mover. É essa a vulnerabilidade, junto com o dano aumentado.

A transição **não pode ser cancelada**. Comprometer-se faz parte do
custo.

**Renasce-se sempre humano** (decisão do Jhon), mesmo morrendo de lobo
ou no meio da transição: o `FormController` volta para Humano no
`Respawned`, antes do `Player` e do `Bot` tratarem o renascer (o filho
fica pronto primeiro, então o sinal chega nele antes). Antes o lobo
voltava lobo.

Não existe restrição de HP para transformar, e as classes básicas não
custam WP: dá para transformar sempre.

**Transformar não cura.** A proporção de vida é preservada entre os
corpos — metade da vida humana vira metade da vida do lobo. Sem isso,
com transformação livre, trocar de forma viraria cura infinita e o
uivo perderia a razão de existir. O comportamento antigo está atrás do
export `RefillOnTransform`, desligado por padrão.

---

## Controles

| Tecla | Ação |
|---|---|
| WASD | Andar |
| Space | Pular · saltar da parede |
| 1 | Arma principal (automática) |
| 2 | Arma secundária (semiautomática) |
| 3 | Virar lobo |
| 4 | Faca |
| 5 | Equipar granada (o ataque arremessa) |
| Botão direito | Lobo: salto no chão, bote no ar |
| W duas vezes (segurando) | Lobo: corrida de quatro patas, 30% mais rápida (`WolfSprintFactor`); soltar o W ou agachar volta a correr em pé |
| W segurado | Sustenta a corrida em parede |
| Ataque segurado | Agarra na parede sem avançar |
| Clique esq. | Atacar |
| Ctrl ou C | Agachar (nas duas formas). No Mac, Ctrl + clique vira clique direito no sistema; o jogo trata como ataque (`MacCtrlClick`), e o especial agachado sai agachando no C |
| Shift | Andar devagar e em silêncio (só humano) |
| R | Rugir (lobo): segure até o fim para o escudo voltar · recarregar (humano) |
| G | Soltar a arma da mão: cai à frente, e você passa para a próxima |
| Esc | Menu da partida (continuar, opções, controles, reiniciar, sair para a sala, sair do jogo) — **não pausa** |
| Tab (segurar) | Placar da partida |
| 1 a 4 na tela de respawn | Escolhe o conjunto |

Na tela de fim, **Jogar de novo** já nasce sob o cursor. Não há mais
Enter para recomeçar: a tela de resultado tem três saídas (jogar de
novo, voltar à sala, sair para o lobby), e um atalho escondido para
uma delas pularia a recompensa animada.

Um toque, um pulo: segurar Space não repete, e uma trava curta impede
que o mesmo toque dispare o pulo do chão e o da parede. Segurar Space
durante a **queda** alivia a gravidade levemente (`HoldFallGravity`) —
controle fino de aterrissagem, não planagem.

Input é registrado por código em `InputSetup.cs`. **Não peça ao
usuário para configurar o Input Map no editor** — já foi feito uma vez
e custou tempo. Ações definidas no editor têm prioridade e não são
sobrescritas.

---

## Corrida em parede

Só na forma lobo e no ar. **Pula-se contra a parede segurando W** —
é o W que sustenta a corrida.

**Segurar o botão de ataque agarra sem avançar.** É a pausa que
permite escolher a hora de saltar em vez de correr até a parede
acabar.

- **Velocidade: a do lobo, sem bônus** (`WallRunSpeedBonus` 1,0; era
  1,25 — 8,1 m/s no Bruto, rápido demais para escolher onde descer).
  Acelera em `WallAccel` (18 m/s²) ao grudar, em vez de sair a toda no
  primeiro quadro, e **agarrar freia em meio metro** (`WallBrake`,
  32 m/s²)
- Sem limite de tempo: corre enquanto a parede existir
- Solta ao largar o W, na ponta da parede, num obstáculo sem saída,
  ou se atordoado
- **Gruda a partir de 0,45 m do chão e fica na altura em que grudou**
  (`WallGrabHeight`; decisão do Jhon, 13/11: colado na parede, um
  pulinho basta — antes era preciso chegar ao alto do pulo). Abaixo
  disso, subindo rápido (`WallGrabMaxRise`, 0,6 m/s), ele ainda não
  gruda: espera subir. Antes, grudava na subida e a velocidade do pulo se perdia aos
  poucos — o lobo subia um pedaço depois de colar. Grudado, a altura
  não muda enquanto corre
- **Direção travada**: escolhida uma vez, ao grudar, pelo **momento**
  que o lobo já carrega — não pela câmera. Saltar de uma parede e
  encostar na seguinte mantém o sentido sem tocar no mouse. O olhar só
  decide quando não há momento horizontal. Nunca se inverte: para ir
  ao outro lado é preciso cair e recomeçar. Com embalo, se o sentido
  do embalo está bloqueado, não gruda — virar ao contrário fazia o lobo
  voltar pela parede
- **Na quina, contorna e continua pela parede** (decisão do Jhon: "ele
  não continua a parede, ele cai" era o erro). Passou 10 cm da ponta
  (`WallEndMargin`), o sentido gira junto com a normal em volta da
  aresta e o lobo segue na face seguinte, na mesma altura e no mesmo
  passo. Bloco, contêiner, pilar e coluna de 30 cm: dá a volta inteira.
  Só uma chapa mais fina que `WallWrapMinFace` (0,25 m) faz o lobo
  seguir reto no ar — contornar ali seria dar meia-volta. Indo para
  longe da parede (`WallLeaveSpeed`, 0,5 m/s) ele não gruda, então uma
  ponta que ficou para trás não o puxa de volta
- **No canto, vira e continua.** Uma parede à frente vira a parede da
  corrida: o lobo segue por ela, para longe da antiga, no mesmo passo.
  Antes ele soltava e regrudava na parede de trás, voltando
- **A parede é o ponto mais próximo dela**, não o que um raio acerta:
  um cilindro em volta do lobo pega a geometria e `GetRestInfo` dá o
  ponto de contato, e a normal é a direção do ponto até o lobo. Ponto
  mais perto **atrás** do lobo quer dizer que a parede acabou; no canto,
  a parede de trás é a antiga, e uma segunda busca sem ela acha a nova
  ao lado
- O olhar fica livre durante a corrida: mirar não desvia o movimento
- A/D não controlam durante a corrida
- **O salto da parede sai um pouco mais alto do que entrou**
  (`WallJumpClimb`, 1,02; era 1,35 e subia 2,8 m — 40% mais baixo,
  decisão do Jhon), então dá para escalar um corredor ricocheteando
  entre as duas paredes. É essa subida que dá ao lobo o ângulo que as
  armas defendem mal: de cima e por trás
- Space desgruda, salta para fora e para cima, e cai. O salto
  **conserva o avanço** da corrida (`WallJumpKeepSpeed`) e soma o
  empurrão perpendicular: sem isso o lobo sai de lado e perde tudo
  que acumulou, que é o oposto da jogada de cair em cima de alguém
- **A mesma parede não serve duas vezes** sem tocar o chão: subir,
  saltar e regrudar mais alto transformaria a parede em elevador.
  Uma parede **diferente** vale — é a jogada de quicar entre as duas
  paredes de um corredor, e ela é desejada. O que separa os dois casos
  é a semelhança entre as normais (`SameWallThreshold`), não o tempo
  nem o contato com o chão
- Perdeu a parede de repente (um vão), tem `CornerGrace` (0,15 s)
  antes de soltar
- **Fica a uma distância fixa da parede** (`WallHoldDistance`,
  0,34 m): a força que segura é proporcional ao quanto ele se afastou
  dela, até `WallStickForce`. A sucção fixa de antes batia na quina e
  empurrava o lobo para dentro dela

Esta é **a mecânica mais importante do jogo**. A memória central de
quem jogava de lobo é correr pela parede, ver o inimigo embaixo e
pular. Se um ajuste prejudicar essa jogada, o ajuste está errado.

---

## Granada

A resposta do humano contra o lobo. Sem ela, o humano sozinho num
corredor não tem jogada — só perde.

O ponto dela é **tirar mobilidade**, não dar dano. O lobo atordoado
perde velocidade e não corre em parede, que é de onde vem todo o poder
da forma.

Duas por vida, reabastece ao renascer. Só na forma humana.

**Segurar prepara, soltar arremessa** (decisão do Jhon): o ataque com a
granada na mão a deixa pronta, e ela sai quando o botão solta, na
direção da mira daquele instante — dá para apontar com calma, mais
alto para ir mais longe. **Enquanto segura, o braço arma o
arremesso**: na tela a granada sobe e inclina para trás (`ViewModel`);
no corpo que os outros veem, o clipe "grenade" puxa o braço e para no
braço armado (1,6 s do clipe), e soltando o resto toca a 2,5× — a mão
passa na frente logo depois de a granada sair (`CharacterAnimator`).
O bot arremessa direto.

**Arremesso forte** (decisão do Jhon): 14,5 m/s na mira e 4 para cima
(era 11 e 3,5), mais o embalo de quem corre. Olhando reto, de pé, cai a
15,5 m medidos (era ~11).

A granada sabe quem a lançou (`Grenade.Thrower`). Aliado passa ileso,
dano e atordoamento; quem lançou, não — granada aos próprios pés tem
que custar alguma coisa. Abate de granada entra no feed, no placar e
rende WP como qualquer outro. Antes não contava para ninguém, o que
numa partida por pontos é abate jogado fora.

---

## O bot

**O bot é um atirador, não um perseguidor.** Na distância certa ele
circula e atira em rajadas; longe, aproxima; perto demais, recua sem
dar as costas.

Virar lobo é recurso de desespero: só abaixo de 40% de vida e com o
alvo ao alcance. Um bot que vira lobo cedo gruda no jogador e nunca
mais atira — foi o primeiro comportamento implementado, e estava
errado.

Rajada e pausa existem para dar ao jogador janela de reagir e para
deixar claro de onde vem o tiro. Fogo contínuo esconde as duas coisas.

### Vida é uma só

Lobo e humano **compartilham a vida**. O que a forma lobo acrescenta é
o escudo, e é ele que define a forma. Vida separada faria de
transformar um segundo tanque de vida, o que esvazia a decisão.

### Munição e recuo

Carregador por arma, **reserva infinita**: o custo de atirar é o tempo
de recarga, não o estoque. Contar balas no inventário só pune quem já
está perdendo a luta.

Carregador vazio recarrega sozinho ao puxar o gatilho — obrigar o
jogador a apertar R depois de ouvir o clique adiciona um passo que ele
já quis dar.

O recuo tem **teto** (`RecoilMax`). Sem teto, segurar o gatilho aponta
a arma para o céu e ela vira inútil em vez de difícil. Cada arma traz
o próprio recuo na definição.

Enquanto se atira a mira **só sobe**; ela desce quando o gatilho solta
(`RecoilReturnDelay`, 0,12 s sem tiro), em linha reta na velocidade
`RecoilRecovery` da arma, em °/s. O que o jogador já puxou para baixo
com o mouse é descontado do que falta descer — sem isso, quem compensa
o recuo à mão termina mirando abaixo do alvo.

### O lobo de perto

Alcance de garra maior que o de uma faca, e **resistência no corpo**
(0,6 no Bruto, 0,78 no Ligeiro). O lobo é uma ameaça de frente, não só
um corpo rápido.

**Headshot atravessa a resistência inteira.** Sem essa saída, o humano
com rifle não teria resposta nenhuma contra um lobo vindo de frente —
e um inimigo sem resposta não é difícil, é injusto.

### Layout da tela

Regra dos cantos (`docs/UI.md`, "HUD de combate"). O centro fica livre:
só mira e avisos críticos.

| Canto | O quê |
|---|---|
| Superior esquerdo | Minimapa e nome do mapa |
| Superior centro | Placar dos times com barra até a meta, relógio e modo |
| Superior direito | Feed de abates: 5 linhas, 5 s cada, ícone da arma, cor do time |
| Centro | Mira com abertura dinâmica (o jogador monta a dele em Opções → Mira: cruz com ou sem ponto, ou só o ponto, de tamanho fixo; cor, opacidade, tamanho, espessura, espaço, contorno; a abertura pode ser desligada), marcador de acerto, dano direcional, avisos |
| Inferior esquerdo | Forma atual (sempre visível), escudo e vida |
| Inferior centro | Barra de transformação |
| Inferior direito | WP, e o painel da forma: arma, pente `/ ∞`, granadas e slots no humano; recargas de garrada, salto, bote e uivo no lobo |

**O HUD troca de forma.** Humano e lobo são dois painéis distintos,
trocados com transição. O lobo não tem munição; mostrar "30 / ∞" num
lobo seria informação errada.

**Vida abaixo de 30%** pulsa e põe a vinheta vermelha nas bordas. A
**barra de transformação** pulsa enquanto canaliza — é o momento de
maior risco da partida — e diz "bloqueado nesta sala" quando a regra
da sala proíbe o lobo.

### O minimapa

Desenhado em 2D a partir das posições, não por câmera ortogonal. Um
minimapa é um diagrama, não uma foto: câmera de cima desenharia chão,
sombra e iluminação, e nada disso ajuda a ler posição.

Gira com o jogador — o que está em cima é o que está à frente. Mapa
fixo obriga a traduzir mentalmente enquanto corre, e ninguém faz isso
num tiroteio.

**Só mostra quem está dentro do alcance.** Saber onde está todo mundo
o tempo todo destruiria a razão de existir do agachar, do andar
devagar e da audição dos bots.

### Menus e o cursor

Enquanto houver menu aberto, **o mouse pertence ao menu**: o cursor
aparece, a câmera não gira e o jogador não atira nem anda.

O controle é um contador em `UiState`, não um booleano em cada menu.
A razão é concreta: dois menus abriam juntos no início e cada um
capturava ou liberava o cursor sem saber do outro, então fechar o de
dificuldade capturava o mouse com a tela de lotes ainda aberta. Com
contador, o cursor só volta quando o último menu fecha — é a lógica
de um semáforo, liberado por quem solta por último.

O relógio da tela de conjuntos também só corre quando ela é a tela de
cima, senão o tempo escorre por trás de outro menu e o conjunto é
escolhido sozinho.

### A economia de WP

WP vem de **abater**, de **assistir** e de **renascer com o conjunto
padrão**, nunca do relógio (valores em "Conjuntos e economia").
Ganhar por tempo premia esperar; ganhar por abate premia jogar. O
bônus do padrão existe para que morrer não deixe o jogador sem
recurso nenhum — castigo que se acumula afunda quem já está perdendo.

O lobo custa 0 WP; o recurso existe para os conjuntos de armas.

A transição de ida é mais longa que a de volta (0,55s contra 0,2s).
Virar lobo é um compromisso; voltar é uma retirada, e punir a retirada
com a mesma espera só faz o jogador se sentir travado.

### Times e feed de abates

Azul contra **Laranja**, com bots nos dois lados. Laranja e não vermelho
porque daltônicos distinguem azul de laranja com folga. **Fogo amigo
desligado** por padrão — num jogo de duas formas, confundir aliado com
inimigo já acontece demais pela silhueta. A sala pode ligar.

O feed no canto superior direito não é enfeite. É como o jogador
entende o que aconteceu fora do campo de visão e, principalmente, de
que ele morreu. Morrer sem saber a causa é a forma mais rápida de um
jogo parecer injusto.

### A troca básica

O lobo **abre mão das armas e ganha mobilidade**. Transformar troca o
controlador inteiro: mais velocidade, pulo bem mais alto, e a parede
vira chão. Como todo ataque é corpo a corpo, o problema que a física
precisa resolver é *como encostar vinte metros antes de um rifle
matar*.

Três coisas fazem isso funcionar, e nenhuma é opcional:

1. **A diferença de velocidade é grande o bastante** para o humano não
   conseguir simplesmente recuar.
2. **Os ataques movem o lobo para frente**, então mira ruim ainda
   conecta.
3. **O espaço vertical é rota de ataque de verdade**, não decoração.

### As garras

Clique esquerdo: **combo de três golpes**, alternando os braços. O
terceiro machuca mais.

**Golpe sincronizado com a animação, e golpe não corta golpe.** O dano
sai no quadro em que a garra chega na animação, não no clique, e o
próximo golpe só sai quando a animação acaba. A tabela é uma só,
`scripts/forms/WolfMoves.cs`: para cada golpe, o trecho do clipe, a
velocidade e o instante do golpe, medidos nas mãos do esqueleto por
`tools/analisar_golpes.gd`. A arma lê dela para o dano e o
`WolfVisual` para a animação — não tem como sair de sincronia. Para um
golpe mais rápido ou mais pesado, mude só a velocidade; a duração e o
instante do dano acompanham. Trocou o clipe? Rode a ferramenta e
corrija a linha dele.

| Golpe | Clipe | Golpe na tela | Dura |
|---|---|---|---|
| Garrada, braço 1 | swiping, 1,00–1,85 s, 2,2× | 0,14 s | 0,39 s |
| Garrada, braço 2 | punch, 0–0,85 s, 2,2× | 0,14 s | 0,39 s |
| Salto (direito no chão) | jump attack inteiro, 0–3,00 s, 1,4× | 1,14 s, quando cai | 2,14 s, preso; voa até 8,4 m em arco |
| Bote (direito no ar) | jump attack: a queda, depois a aterrissagem | quando encosta | aterrissagem de 0,78 s, presa; sem recarga |
| Rugido (R, segurado) | roaring, 0,30–4,90 s, 2,3× | escudo no fim | 2,0 s; soltar cancela |

Durante um golpe o lobo não volta a humano: a tecla espera o golpe
acabar.

**O clique fica guardado** (`AttackBuffer`, 0,3 s): clicou na garrada
com o golpe anterior ainda no fim, o próximo sai no quadro em que ele
acaba. Sem isso o clique dado um pouco cedo se perdia e o lobo parecia
não responder. Clicando sem parar, dois golpes e meio por segundo.

**Golpes fluidos.** Medido quadro a quadro na velocidade dos ossos
(mãos, pés, cabeça), não só a olho:

- A garrada começa com a mão já recolhida (1,00 s do "swiping") e
  entra em 0,07 s: a garra chega 0,14 s depois do clique, e uma entrada
  mais longa comeria o golpe. Sai em 0,12 s, porque a próxima vem logo.
- As garradas ficam **da cintura para cima**. No corpo inteiro o
  "swiping" gira o quadril 90° e o lobo terminaria o golpe de lado.
- Parado, o corpo dá **o passo do golpe em sino** (`StepFrom`,
  `StepTo`, `Step` em `WolfMoves`): 0,91 m na garrada, como o quadril
  do clipe, e 0,35 m no punch. O avanço do salto usa o mesmo passo,
  com a distância da mira. A velocidade sobe e desce sem degrau, e
  as pernas andam um passo junto com o braço. O empurrão instantâneo
  antigo jogava os pés na pose de correr de uma vez. Correndo, vale o
  empurrão de sempre (`ClawLunge`): as pernas já estão correndo.
- Preso no lugar (salto, aterrissagem do bote), o lobo freia em
  `FreezeBrake` (30 m/s²) em vez de parar seco: desliza meio metro
  enquanto agacha.

**O golpe não é hitscan.** Cada patada cria um volume curto à frente
do corpo e pega quem estiver dentro. A diferença é concreta: um raio
sai da câmera e acerta onde se mira, enquanto um volume preso ao corpo
acerta o que estiver perto da garra. É isso que faz o alcance parecer
variar com a postura em vez de seguir o olhar.

**Cada garrada leva o lobo para frente** — o passo parado, o empurrão
correndo. Sem isso o combo deixa o jogador batendo no ar enquanto o
alvo anda: o golpe precisa levar o lobo até o alvo.

**Contra humano a patada vale 1,6×.** O lobo é feito para caçar
gente, não para duelar com outro lobo — sem isso, o encontro entre
dois lobos vira o combate padrão do jogo, que é o oposto do desenho.

**Botão direito no chão: o salto.** O "jump attack" inteiro — agacha,
salta, cai. É a arma do lobo para **fechar distância**: aperta-se de
longe e o lobo cai em cima do humano. O jogador não anda nem pula, mas
o salto voa: do impulso até os pés tocarem o chão, o corpo anda a
distância da carga, **até 8,4 m** (`PounceMaxDistance`; era 12, 30% a
menos na v0.27.1).

**Segurar carrega** (decisão do Jhon). Apertou, o lobo agacha e fica
encolhido (o clipe para no fim do agachar); soltou, salta. Em 1 s
(`PounceChargeTime`) a carga enche: o alcance vai de 1,5 m a 8,4 m e o
dano de 40% a 100% do cheio. Uma barra de força aparece enquanto
carrega (`ChargeBar`). Cheio, segura mais 0,6 s e salta sozinho. No ar
não carrega: o especial no ar é o bote, direto.

**A distância é só a da barra** (decisão do Jhon): a mesma carga dá
sempre o mesmo salto, em linha reta na direção da mira no instante de
soltar — olhando para o chão ou para o céu, com ou sem inimigo na
frente. Antes o salto parava no ponto mirado, ia até o inimigo perto
do centro da tela e seguia o alvo no voo; com alguém na frente, a carga
cheia ora passava do alcance, ora quase não saía do lugar, e o jogador
não controlava nada disso. Medido (carga cheia: reto, chão, céu,
inimigo a 5 m, ao lado, a 14 m): 12,0 m em todos; metade, 7,1 m em
todos. O que encurta é o mapa, igual para todo mundo: parede, bloco e
um corpo no caminho (bater em alguém para nele, e o golpe sai ali).
Acabado o voo, o lobo para onde caiu — o embalo de um voo barrado não
vira mais um deslize. O bot mira no alvo e escolhe a carga que leva
até ele (`PounceChargeFor`), com o erro da dificuldade.

**O voo é um arco de verdade, calculado no impulso.** Para a frente, em
sino (sai devagar, voa, assenta), como o quadril do clipe; para cima,
em parábola, subindo rápido no impulso. Antes de sair, o salto mede o
chão no caminho a cada 40 cm, com raios de cima para baixo:

- **O que tem até 2,6 m acima dos pés passa por cima**
  (`PounceMaxClear`): o arco sobe o bastante para os pés passarem
  15 cm acima. Caixote, mureta, contêiner.
- **Algo no fim do caminho que dá para alcançar vira o lugar de cair**:
  saltando além de um contêiner perto, o lobo cai **em cima dele**.
- **O que é mais alto encurta o salto**: diante de um bloco ou do muro,
  o lobo cai antes dele, nunca dentro dele. O ponto de queda também
  recua se cair dentro da quina de algo alto.
- O arco mínimo é `PounceArc` (0,6 m) e cresce com a distância, até
  1,6 m no chão plano.
- Gente não conta como chão: cair em cima de alguém é o objetivo.

- A direção sai da câmera do próprio jogador (`Player.AimCamera`), não
  da câmera que estiver na tela. O
dano (2,1×) sai **quando o lobo cai**, 1,14 s depois do clique. Preso
por 2,14 s: forte, mas lento e fácil de ver vindo — a lentidão é o que
impede que seja a resposta para tudo. O volume é o das garras.

**Botão direito no ar: o bote.** Um mergulho **em linha reta** na
direção da **mira inteira**, a 13 m/s (era 17: rápido demais para quem
está embaixo ver o lobo chegando), sem gravidade, sem freio e sem
desvio: olhar para baixo dá um bote curto e mergulhado, olhar à frente
dá um longo e raso (sempre descendo, no mínimo 0,35 de inclinação). O
avanço horizontal é 80% do da mira (`DiveForward`), e **metade depois
de um salto da parede** (`DiveForwardFromWall`): de lá o lobo sai mais
alto e o bote ia longe demais (decisão do Jhon). O
ponto de impacto é escolhido no clique, e é isso que deixa o bote
preciso. O golpe sai **no instante em que o lobo encosta** — no chão,
numa parede de frente ou em alguém —, não num tempo marcado: um golpe
só, 3×, com o bônus aéreo se a queda passou de 2,5 m. O volume é **um
pouco maior que o do salto** (1,7 m à frente, raio 1,5, contra 1,5 e
1,15). Encostou, aterrissa preso por 0,78 s; mirou no vazio, bate no ar
em 1,1 s (o alcance de antes, 14 m).

**Todo pulo tem o seu bote** (decisão do Jhon). Não há recarga: acabou
a aterrissagem, o próximo pulo já pode ter outro. E não dá para pular
antes de ela acabar — o lobo está preso —, então o bote nunca corta o
anterior. Com a recarga de 2 s, quem pulava logo depois de aterrissar
apertava e nada saía. O bote sai **em qualquer ponto do pulo**, até na
subida; **pulo e especial no mesmo quadro** viram bote (o pulo sai e o
bote no quadro seguinte, já no ar), e não o salto do chão.

O corpo acompanha: a queda do clipe é acelerada para o impacto dela
cair no quadro em que o lobo encosta (o tempo até o chão é medido no
clique), e o lobo tomba para a frente na inclinação do mergulho, até
40°. Sem a inclinação ele despencava de pé em vez de avançar no alvo.

O primário também funciona no ar — o bote é uma escolha, não a única
opção de quem pulou. A condição de estar no ar amarra o golpe mais
forte do lobo ao salto, que é a jogada que o jogo quer premiar.

**O lobo é um corpo inteiro, em terceira pessoa** (`WolfVisual`, com o
Mutant do Mixamo — veja "Personagens"). O dono vê o próprio lobo e os
outros reconhecem a silhueta de longe: curvado, largo, braços
compridos. Numa luta rápida ninguém lê textura — o que separa um lobo
de um humano a trinta metros é o contorno.

### O escudo do lobo

O escudo **escorre sozinho** enquanto o lobo está transformado. Não é
vida extra: é um relógio. **Segurar R ruge**: 2,0 s do começo ao fim,
e só então o escudo volta cheio. **Soltar antes cancela** sem pagar —
o rugido é um compromisso, não um botão de disparo. **O lobo ruge em
movimento** (decisão do Jhon): anda, pula e cai rugindo, sem frear —
parar no meio do pulo parecia travar. O rugido toca da cintura para
cima, por cima do andar. O preço é não atacar esse tempo todo e
entregar a posição pelo som.
Morrer ou deixar de ser lobo no meio também corta o rugido sem pagar.
O bot segue a mesma regra (ele não "solta": ruge até o fim ou até ser
interrompido).

Essa é a tensão central da forma lobo: quanto mais tempo transformado,
mais exposto, e a única forma de sustentar é parar — justamente o que
um corpo-a-corpo não pode fazer numa luta.

Vida e escudo aparecem como **números**, não barras. Com um recurso
que vaza continuamente, o jogador precisa ler quanto resta, não
estimar por comprimento.

### Som é informação de jogo

Ruído passa por um barramento (`scripts/core/NoiseBus.cs`): quem faz
barulho publica, quem ouve se inscreve, e nenhum dos dois conhece o
outro. É o padrão de *stimulus broadcast* usado em IA de stealth —
a alternativa, cada bot varrer o mundo procurando coisas barulhentas,
acopla a IA a tudo que existe.

| Evento | Raio |
|---|---|
| Passo humano | 13m |
| Passo de lobo | 20m |
| Aterrissagem | 17m |
| Tiro | 60m |
| Rugido | 45m |
| Explosão | ~39m |

**Nem todo som vira ida.** Cada bot nasce com uma curiosidade
sorteada, e tiro chama mais atenção que passo. Quem decide ficar ainda
assim vira na direção do som. Dois bots reagindo igual ao mesmo tiro
entregam que são scripts; um indo e outro ficando parece decisão —
pelo mesmo motivo que o erro de mira tem variação.

**Agachado e andando com Shift não fazem barulho nenhum.** São as
duas ferramentas de furtividade do humano, e é o que dá escolha
contra um inimigo que ouve. Andar devagar é só do humano: o lobo vive
de movimento, e dar a ele a opção de abrir mão disso criaria um lobo
furtivo, que é o oposto da forma. É isso que dá ao agachamento um
propósito tático em vez de ser só um botão de ficar menor.

O passo sai a cada **passada** — a distância entre um pé no chão e o
outro, que é a do clipe que está tocando (1,19 m o humano correndo,
1,05 m o lobo, 0,97 m na corrida de quatro) — e não por tempo: o som
cai com o pé da animação e a cadência acompanha a velocidade sozinha.
Até a v0.27 era um ruído a cada 2,2 m, metade dos passos que se via; o
bot agora ouve cada pisada, como o jogador. A fase da pisada não é
amarrada ao clipe (o ruído é regra de jogo e não pode depender de
animação, que o servidor não toca): só a cadência.

O áudio acompanha o evento em vez de substituí-lo. O que o bot ouve
e o que o jogador ouve precisam ser a mesma coisa, senão o jogador não
consegue prever a reação.

O que é só enfeite pode variar à vontade, porque não muda quem ouve
quem: o som do passo conforme o chão (metal, neve, madeira, terra), a
reverberação e o fundo de cada mapa, a bala estalando na parede, a
cápsula no chão, e o seu tiro ser outra gravação (estéreo, com mais
grave) que a do tiro dos outros.

### O áudio

Os arquivos são gerados por `tools/sons/processar.py` a partir de
gravações baixadas do GitHub por `tools/sons/baixar.py`. Para mudar um
som, mude a linha dele no gerador e rode de novo; editar o `.wav` à mão
se perde na próxima geração.

Três peças de código, e a lista de arquivos em `assets/audio/LEIA-ME.md`:

- `scripts/core/Sfx.cs` toca por **nome**: `Sfx.At("passo", ponto,
  raio)` acha `passo.wav`/`.ogg`/`.mp3` e as variações `passo_1`,
  `passo_2`... e sorteia entre elas com o tom variando ±6%. **Arquivo
  que não existe fica mudo, sem erro** — o jogo ganha som aos poucos,
  um arquivo por vez, sem tocar em código.
- `scripts/visual/WorldAudio.cs` (um por cena de jogo) **escuta o
  NoiseBus** e toca passo, pouso, tiro, uivo e explosão no ponto e no
  raio do ruído. **O uivo usa o mesmo som da transformação**
  (`lobo_transformar`), decisão do Jhon — `WorldAudio.HowlSound`. Não existe som de mundo que não seja ruído: é assim que
  a regra acima se mantém sozinha.
- `scripts/visual/CombatantAudio.cs` (filho de Player e Bot) toca o
  que não é ruído — transformação, garra, recarga, dor, morte — com
  **alcance de 12 m**. Os bots não ouvem esses sons, então eles não
  podem servir de informação de longe; de perto você já está vendo
  quem os fez.

Os seus próprios sons tocam sem posição, e o seu passo 3 dB abaixo do
dos outros: o seu passo não pode cobrir o de quem está atrás de você.

**Os bots emitem ruído também.** Antes só o jogador emitia passo,
pouso e uivo: os bots ouviam você, mas ninguém ouvia os bots. Com
áudio, isso virou bot silencioso chegando por trás. Efeito colateral:
bots de times opostos passaram a se ouvir.

**O tiro é a exceção ao raio** (decisão do Jhon): o de outro se ouve
de qualquer ponto do mapa, até 120 m (`WorldAudio.ShotReach`), vindo de
onde ele está e caindo com a distância — longe, a versão abafada. O bot
continua ouvindo só até o raio da arma.

Som novo que for informação de jogo **entra como `NoiseKind`**, com
raio, e não como som solto — senão o jogador passa a ouvir algo que o
bot não ouve.

### Como o bot pensa

**O lobo do bot usa os especiais** (`Bot.WolfTactics`): o salto
carregado de 4 m até 1 m antes do alcance cheio (7,4 m) — com a carga
que leva até uma garra do alvo,
errando `PounceError` — e, de 3 a 6 m, um pulo na direção do alvo com
o bote no alto. Sorteado a cada meio segundo com `WolfSpecialRate`; o
especial vem antes da garra (arranhar no meio do pulo deixava o golpe
em recarga e o bote não saía). O corpo do bot segue o voo, o mergulho
e a trava do golpe como o do jogador.

**Viu o alvo, atira** — em qualquer ação que não seja recarregar ou
recuar. Indo para a parede o bot faz fogo de cobertura, e patrulhando ou
procurando ele passa direto para o combate. Antes, "procurar parede"
calava o bot: dois bots a 3 m, os dois "indo para a parede", ficavam
meio minuto se encarando sem atirar. Parede que não protege (sem
caminho, ou chegou e ainda vê o alvo) fica esquecida por 3 s
(`NoCoverTime`). Numa partida de 5 min, os momentos com alvo à vista e
nenhum tiro caíram de 42 em 82 para 1 em 40.

Três camadas, que é como IA de FPS se organiza desde os anos 2000:

**Navegação** — `NavigationAgent3D` sobre malha assada em tempo de
execução (`NavBaker`). O desvio raso que existia antes funcionava em
campo aberto e travava na primeira parede longa: rotacionar o vetor
noventa graus não contorna vinte e quatro metros de concreto.

Perto do alvo o bot volta ao vetor direto — navegação ponto a ponto
estraga strafe e recuo, que são movimentos curtos e intencionais.

**Cobertura** — descoberta, não marcada. O bot testa posições num
anel em volta e dispara um raio de cada candidato até o peito do
alvo: linha bloqueada significa parede no meio, e parede no meio é
cobertura.

Isso funciona em qualquer geometria, inclusive num `.glb` que ninguém
anotou — e é a razão de não existir marcação de cobertura no mapa.
Vence o candidato mais perto: fugir para o outro lado do mapa não é
se proteger, é desaparecer da luta.

O lobo nunca procura parede. Esconder um corpo-a-corpo é desperdiçar
o que ele é.

**Percepção** — visão e audição são sentidos independentes que
alimentam uma única crença: onde o alvo *provavelmente* está. Visão
exige distância, ângulo e linha de visada. Audição dá posição com
erro, porque ouvir não é ver — um bot que localiza som com precisão
cirúrgica parece trapacear.

**Decisão** — **utilidade**, não transições fixas. Cada ação dá uma
nota de 0 a 1 e a maior vence (`scripts/ai/BotBrain.cs`).

A diferença aparece quando duas coisas são verdade ao mesmo tempo.
Uma máquina de estados precisa de uma regra para decidir se recarrega
ou atira com pouca munição e o inimigo longe; com utilidade, as duas
notas se comparam sozinhas.

Três regras de construção, e violar qualquer uma estraga o sistema:

**Multiplicar, não somar.** Uma consideração ruim derruba a ação
inteira. Recarregar no meio de um tiroteio deve valer zero, não a
média entre "preciso" e "não dá".

**Compensar o produto.** Com muitas considerações o produto afunda, e
uma ação bem avaliada perderia para outra mal avaliada e simples.

**Inércia.** A ação em curso ganha um bônus. Sem isso o bot oscila
entre duas opções quase empatadas e parece indeciso — o defeito mais
visível de utilidade mal feita.

As ações: Patrol, Investigate, Engage, Push, Reposition, Reload,
Transform, Revert, Howl, Retreat. Patrol é o piso e nunca vale zero,
senão um bot sem informação ficaria parado.

A decisão sai em intervalos, não a cada frame: pensar sessenta vezes
por segundo custa caro e produz um bot que muda de ideia antes de
terminar o que começou.

As **curvas** ficam separadas da decisão (`scripts/ai/Utility.cs`).
Trocar uma curva muda a personalidade do bot; trocar um `if` muda o
programa.

**Estado** — a máquina continua, agora só descrevendo percepção:

```
Idle (patrulha) --[ouviu]-->  Investigate
Idle            --[viu]-->    Combat
Investigate     --[viu]-->    Combat
Combat          --[perdeu]--> Search
Search          --[desistiu]--> Idle
```

Em deathmatch o bot **avança enquanto atira** em vez de administrar
distância (`Aggression`). O modo não premia quem segura posição, e um
bot que recua o tempo todo transforma a partida numa perseguição.

Em Idle o bot **patrulha**: escolhe um ponto aleatório da área, vai
até ele devagar, pausa e escolhe outro. Bot parado no spawn denuncia
que é script, e um bot caminhando também é informação — longe é
oportunidade, chegando é ameaça.

Sem navmesh não há garantia de que o ponto seja alcançável. Em vez de
provar que o caminho existe, o bot tenta e desiste quando para de
progredir (`StuckTime`). É mais barato e falha de forma visível, em
vez de empurrar a parede indefinidamente.

**Ação** — movimento e tiro decorrem do estado.

Alvo abatido deixa de ser alvo: o bot esquece o que sabia e volta a
patrulhar. Sem isso ele descarrega no corpo, o que é ruim de assistir
e transforma o respawn numa emboscada.

A regra que sustenta tudo: **o bot nunca lê a posição do jogador para
decidir**, só a última posição conhecida. É isso que produz
comportamento legível — ele vai aonde você estava, e você entende por
quê. Ler a posição real produz onisciência disfarçada, que o jogador
percebe mesmo sem saber nomear.

Ao chegar no destino sem achar ninguém, ele varre o entorno girando e
só então desiste. Procurar e desistir visivelmente é o que faz o
jogador confiar que esconder-se funciona.

### Dificuldade dos bots

Três níveis, como dado: `resources/bots/facil.tres`, `medio.tres` e `dificil.tres` (`BotDifficulty`; quem aplica é `scripts/core/Difficulty.cs`). O que muda é
**competência, nunca vida ou dano**: inflar o inimigo produz luta
longa, não luta difícil.

As alavancas, em ordem de impacto: tempo de reação, erro de mira,
taxa de erro deliberado, e sustentação de fogo. Depois vêm percepção
(alcance, campo de visão, memória) e mobilidade.

| | Fácil | Médio | Difícil |
|---|---|---|---|
| Reação | 0,8s | 0,4s | 0,2s |
| Erro de mira | 9° | 4,5° | 2° |
| Erro deliberado | 45% | 20% | 8% |
| Rajada / pausa | 0,45 / 1,3 | 0,7 / 0,65 | 0,95 / 0,35 |
| Visão | 26m / 100° | 40m / 140° | 55m / 170° |
| Velocidade | 2,5 | 3,0 | 3,5 |
| Especial do lobo (chance) | 25% | 50% | 75% |
| Erro da carga do salto | 25% | 12% | 6% |
| Audição | 0,6× | 1,0× | 1,35× |
| Erro ao localizar som | 5m | 2,5m | 1,2m |

**Nem no difícil o erro vai a zero.** Um bot que acerta sempre não é
difícil, é injusto — e o jogador desiste em vez de melhorar.

O erro deliberado é separado do erro de mira de propósito: aumentar
só o erro médio produz um chuvisco de quase-acertos, enquanto jogar
alguns tiros fora mantém os acertos convincentes e cria janelas
claras para o jogador se mover.

### O tiro sai do cano

O **raio** parte da câmera, porque é dela que vem a precisão. O
**traçante** parte da ponta do cano, porque é de lá que o jogador
espera vê-lo nascer. Desenhar a linha a partir da câmera deixa o tiro
saindo da testa — é o artefato que mais entrega um FPS improvisado.

Cada disparo também produz clarão no cano, recuo da arma e recuo de
câmera que volta sozinho. Sem isso atirar não tem peso.

### Retorno de combate

- **Acerto**: quatro riscos em volta do crosshair. Brancos no corpo,
  vermelhos e maiores na cabeça. Sem isso não se sabe se o tiro pegou.
- **Dano na cabeça**: 2,5× o dano. A zona é a faixa superior do corpo,
  medida a partir do centro — continua valendo quando houver modelo.
- **Levar dano**: vinheta vermelha nas bordas, que some em menos de
  meio segundo, e o arco em volta da mira apontando de onde veio.
- **Bot alvejado**: não vira na hora. Marca a origem do tiro, espera
  um tempo curto com variação aleatória, e só então gira — mais devagar
  que o giro normal, porque foi pego de surpresa. Reação instantânea é
  desumana e anula o prêmio de atacar pelas costas.

### Todo estado do jogador precisa aparecer na tela

Morrer sem aviso é indistinguível de travar — o jogador relatou
"fiquei congelado" quando na verdade estava morto e esperando os 3
segundos de respawn. O mesmo vale para WP insuficiente, atordoamento
e granadas acabando. Se um estado muda o que o jogador pode fazer,
ele precisa estar visível.

Todo disparo deixa traçante. Sem retorno visual o bot parecia
inofensivo enquanto acertava — não havia como distinguir "não atirou"
de "atirou e eu não vi".

Limites conhecidos: navegação é direção e desvio raso, sem caminho
calculado. Não há cobertura, rota nem memória de posição. Tudo isso
depende de navmesh, e navmesh depende de um mapa de verdade.

---

## Mapas vindos de .glb

`scripts/core/MapLoader.cs` carrega um `.glb`, gera colisão de malha,
coleta spawns e assa a navegação — tudo em tempo de execução.

Colisão normalmente se configura na janela de importação, que é GUI.
Fazer em tempo de execução custa um pouco de carregamento e devolve
uma vantagem: trocar o `.glb` não exige reconfigurar nada.

Convenções lidas do nome do objeto no arquivo: `-noco` não colide,
`-invis` colide sem aparecer, `spawn*` vira ponto de nascimento.
Nomear no Blender é a forma mais barata de passar intenção do modelo
para o jogo sem inventar um formato paralelo.

**Exigência da corrida em parede:** paredes de cerca de 1m de
espessura com faces verticais limpas. Quinas finas não funcionam,
porque os raios precisam de superfície para encontrar.

### Arrival: removido

O Arrival vinha extraído do cliente original e nunca pôde ser
publicado. Serviu de régua de proporção enquanto não havia mapa
próprio; com o Hangar, foi apagado do projeto a pedido.

### Geometria de uma face, e por que isso quebra meio jogo

Mapa extraído de jogo é feito de planos de **uma face só**. O lado que
cada plano considera frente é o que o modelador precisou para
desenhar, não o lado onde o jogador anda: no Arrival, 53% das faces
verticais na altura de jogo apontam para fora da arena — a parede do
perímetro em X = −39,9 tem normal (−1, 0) com a área jogável do lado
+X, ou seja, o jogador encosta no verso do triângulo.

`ConcavePolygonShape3D` nasce com `BackfaceCollision = false`. Com
isso, metade das paredes do mapa existe para os olhos e não existe
para a física. O sintoma não aponta para a causa em nenhum dos casos:

- o lobo não gruda em parede plana, e o log diz que não havia parede —
  porque para o motor não havia;
- o bot vê e atira **através** de parede, porque o raio de linha de
  visão atravessa;
- o bot não acha cobertura, porque cobertura é definida por raio
  bloqueado e nada bloqueia;
- o tiro do jogador atravessa parede.

Por isso `MapLoader.BuildCollision` monta a forma à mão em vez de usar
`CreateTrimeshCollision()`: aquele método não devolve a forma gerada,
e a forma precisa de `BackfaceCollision = true`.

Com colisão de dois lados vem uma consequência: bater no verso de um
triângulo devolve a normal geométrica, apontando para o mesmo lado em
que o raio viaja. **Toda leitura de normal de raycast precisa orientar
a normal contra o raio** (`if (n.Dot(direction) > 0) n = -n;`). Sem
isso a força que segura o lobo inverte e ele é empurrado para dentro
da parede em vez de colar. Vale para qualquer sentido de face e não
muda nada no caso normal.

### Pontos de nascimento

`scripts/core/SpawnPoints.cs`. Oito marcadores em `main.tscn`,
escolhidos por terem 2 m de folga em volta e 3 m de teto livre — foram
medidos no arquivo, não estimados a olho.

Nasce-se no ponto mais longe do inimigo vivo mais próximo, com uns
metros de ruído na escolha para o mesmo par de mortes não gerar sempre
o mesmo spawn. A distância ao mais próximo é o número que decide, não
a média: nascer longe de quatro e colado em um é nascer colado em um.

Sem `SpawnPoints` na cena, cada ator volta ao próprio ponto inicial —
é o que mantém `scenes/treino.tscn` funcionando sem configuração.

### Rondas: cada mapa traz as suas

`scripts/core/PatrolRoutes.cs`. Uma rota é uma sequência de pontos
que alguém já sabe que dá para percorrer. O bot pega uma, percorre e
pega outra.

Isso existe porque a alternativa não funciona: ponto aleatório dentro
de um raio produz bot andando em linha reta para o meio de um prédio,
porque o destino não tem relação nenhuma com a forma do mapa — é um
número dentro de um círculo.

**As rotas pertencem ao mapa, nunca ao bot.** Mapa novo traz as suas,
de duas formas:

1. Um nó `PatrolRoutes` na cena, com um filho por rota e um
   `Marker3D` por ponto. É como o Arrival faz.
2. Objetos chamados `route*` dentro do próprio `.glb`, com os pontos
   como filhos. Mesma ideia dos `spawn*`: quem desenha o mapa sabe por
   onde se anda nele, e dizer isso no Blender custa menos do que
   adivinhar por código depois.

Sem nenhuma rota o bot volta a vagar no raio. A ronda nunca depende
disto existir.

As quatro do Arrival foram **geradas a partir do `.glb`**, não postas
a olho: células de piso com dois metros de folga em volta e teto
livre, espalhadas por ângulo em torno do centro. Saíram um perímetro
de seis pontos, uma interna de quatro e duas travessias de três.

Dois detalhes tiram o cheiro de roteiro, e os dois custaram bug antes
de ficarem certos: o bot entra pelo **ponto mais próximo** e não pelo
primeiro, porque rota é caminho e não fila; e o **sentido é
sorteado** — mas invertido quando apontaria para fora da rota, senão
quem entra por uma ponta pega um ponto só e troca de rota, que de
longe é idêntico a não ter rota nenhuma.

### Vigilância: o bot não esquece quem atirou

Levar tiro marca a origem, espera um tempo curto — reação instantânea
é desumana e anula o prêmio de atacar pelas costas — e só então o bot
se vira.

O que vem depois é o que faz diferença. A memória normal de três
segundos serve para perder alguém de vista numa troca de tiros. Um
tiro pelas costas é outra coisa: ali existe alguém e o bot sabe
disso. Esquecer em três segundos o fazia voltar a patrulhar de costas
para o mesmo corredor, que é exatamente o que faz um bot parecer
burro.

Por isso a vigília de sete segundos mantém a crença viva, e o bot
continua indo e olhando para lá. Ver alguém interrompe: ver é
informação melhor do que lembrar.

### As cenas

| Cena | Para que serve |
|---|---|
| `scenes/menu/menu.tscn` | **Cena principal do projeto.** Início, sala, conjuntos e inventário |
| `scenes/main.tscn` | A partida. Os bots nascem da sala (`MatchSetup`); aberta direto pelo editor, monta um 4 contra 4 padrão |
| `scenes/maps/hangar.tscn` | O mapa, com tudo que é dele: geometria, spawns por base, rotas e navegação |
| `scenes/treino.tscn` | Caixa cinza com duas paredes paralelas e bonecos, para medir física sem o mapa no caminho |

A cena de treino não é sobra: quando a física quebra, uma sala limpa
com duas paredes e um cubo diz em dez segundos o que um mapa inteiro
esconde.

---

## O mapa Hangar

Mapa próprio: o pátio de uma base aérea. Nasceu em caixa cinza e hoje
tem material de verdade, oficinas em que se entra e letreiros — nenhuma
peça vinda de outro jogo.

Gerado por `tools/mapas/hangar.py` (com `tools/gerar_mapas.py hangar`),
que guarda cada medida com o motivo dela. **Rodar o script de novo sobrescreve o `.tscn`**: se o mapa for
editado no Godot, o `.tscn` passa a ser a fonte e o script vira só o
registro de como ele nasceu.

| Medida | Valor |
|---|---|
| Arena | 84 × 48 m, muro de 12 m |
| Bases | galpões de 10 × 21 m com teto a 5 m, A (azul) a oeste, B (laranja) a leste |
| Centro | heliponto de 12 × 12 m a 2 m, parapeito de 1 m, farol de 4 m |
| Blocos | quatro, 12 × 9 m e 5 m de altura, com rampa vindo do centro |
| Pontes | duas, 28 m de vão a 5 m, ligando os telhados |
| Becos | 3 m de largura entre cada bloco e o muro |

**Simétrico em X.** Num mapa de bases, qualquer vantagem de um lado é
vantagem de um time, e não tem como afinar isso jogando sozinho contra
bots.

### As quatro perguntas, respondidas pela geometria

1. **Caminho que só o lobo faz:** os becos. Duas paredes paralelas a
   3 m é o que a corrida em parede precisa para subir ricocheteando até
   o telhado de 5 m. O humano chega lá pelas rampas — pelo centro,
   exposto.
2. **Lugar onde o humano vê o lobo chegando:** o heliponto, 2 m acima
   do chão, de frente para as alas abertas.
3. **Corredor que o humano deve evitar:** os mesmos becos. Três metros
   é distância de garra, não de fuzil.
4. **Cair em cima de quem não olha:** as pontes. Grade só do lado do
   muro; do lado da ala, nada.

### Alturas amarradas à física

O humano pula 0,99 m: sobe caixote (0,8 m), não sobe contêiner
(2,2 m). Para o Bruto alcançar o contêiner a partir do caixote ele
precisa pular 1,4 m — `JumpMultiplier` de pelo menos **1,2** (com
1,28, 1,62 m). Ninguém alcança 5 m sem parede. **Mexeu no pulo, confira estes números** — um
caixote 2 cm alto demais vira parede para o humano sem ninguém notar.

### As oficinas

Os quatro blocos laterais eram maciços; agora cada um é uma oficina de
dois cômodos: a sala da frente com porta e duas janelas para a ala, o
depósito dos fundos com uma porta para o lado do centro e outra para o
lado da base, e uma porta ligando os dois. **A parede de trás ficou
inteira** — é uma das duas do beco, e o lobo precisa dela para subir.
O telhado, a mureta e a rampa são os de antes.

---

## A partida

`scripts/core/Match.cs`. Mata-mata em equipe, Azul contra Laranja:
ganha quem chegar primeiro ao objetivo da sala (padrão **40** abates);
se o tempo acabar antes (padrão **10 minutos**), ganha quem tiver
mais, e empate é empate. Pontos e tempo vêm de `RoomConfig`.

| Fase | O que acontece |
|---|---|
| Contagem (10 s) | Todos na base. Olha-se em volta, não se anda nem se atira. É também o tempo da tela de conjuntos |
| Valendo | Relógio corre, abates pontuam |
| Fim | O `Match` paga XP e Presas uma vez (`Progression.Apply`) e só então troca de fase; a tela de resultado lê `Match.Reward` |

**A partida não sabe quem atirou em quem.** Ela escuta o `KillFeed`, o
mesmo barramento do feed na tela. Quem mata não precisa saber que
existe placar. Só abate de inimigo pontua; queda e granada nos próprios
pés contam só como morte. Pontos no placar: abate 10, assistência 5.

`Match.IsLive` é o que Player e Bot consultam para saber se podem se
mexer. **Sem `Match` na cena ele responde verdadeiro** — é o que mantém
o treino funcionando como antes.

`Match.Restart()` e `Match.Leave(destino)` zeram o `UiState` antes de
trocar de cena: o contador de menus é estático e sobrevive à troca, e
um menu aberto no fim faria a tela de conjuntos da partida nova nunca
contar o tempo. **Sair no meio é abandonar**: não paga nada, e o Esc
pede confirmação.

Atalhos de desenvolvimento, para ver o fim sem jogar dez minutos:
`godot ... res://scenes/main.tscn -- --duracao=30 --contagem=2`, e
`--placar` deixa o Tab aberto.

### O HUD da partida

`scripts/ui/MatchHud.cs`, separado do `Hud` de propósito: o `Hud` fala
do corpo, este fala da partida, e some inteiro numa cena sem partida.

- **Topo:** placar de cada time com a barra até a meta — "27" não diz o
  quanto falta, um traço a dois terços do caminho diz —, relógio
  (vermelho no último minuto) e modo. O seu time leva "· VOCÊ".
- **Tab** (`Scoreboard.cs`): os dois times lado a lado. Colunas
  patente, jogador, abates, mortes, assistências, pontos, forma e
  ping, ordenado por pontos. Você com fundo dourado, morto esmaecido,
  sequência de abates (×N) ao lado do nome. As linhas são montadas uma
  vez e reaproveitadas: recriá-las quatro vezes por segundo derrubava o
  jogo ao fechar.
- **Fim** (`ResultScreen.cs`), em três tempos: o veredito sozinho por
  1,5 s; o placar com o MVP no topo; a recompensa animada — linhas de
  ganho uma a uma, barra de patente enchendo, Presas contando, missões
  concluídas e o aviso de promoção. Rodapé: **Jogar de novo**
  (primário, já sob o cursor), Voltar à sala, Sair para o lobby.

---

## Times

`Health.Team` diz o lado. Os bots recebem time e nome pela raiz
(`Bot.Team`, `Bot.Callsign`) e aplicam no `_EnterTree`, não no
`_Ready`: os filhos fazem `_Ready` antes do pai, e o corpo e a barra de
vida leem o time no `_Ready` deles para escolher a cor.

| Quem | Como sabe o lado |
|---|---|
| Corpo | Pintado na cor do time; juntas num tom escuro dela |
| Barra de vida | Sempre na cor do time |
| Minimapa | Ponto na cor do time |
| Feed e placar | `Health.PublicName`, que nunca vem vazio |

**O bot escolhe alvo por time.** Antes o alvo era sempre o jogador:
com times, o aliado mirava em você e dois inimigos passavam um pelo
outro sem se ver. Agora vale qualquer vivo do outro lado; quem está à
vista ganha, e entre iguais o mais perto, com 6 m de vantagem para o
alvo atual — sem isso a cabeça fica indo e voltando entre dois.

A crença **não** é zerada na troca de alvo. Com vários inimigos ela é
"de onde vem a ameaça", não "onde está fulano". Zerar apagava o tiro
pelas costas que a vigília existe para lembrar.

Passo de aliado não é pista: sem esse filtro o time inteiro passaria a
partida investigando os próprios passos dentro da base.

`PublicName` existe porque `DisplayName` vazio não é nulo, e o
`DisplayName ?? nome do nó` que reportava os abates nunca caía no nó.
O feed mostrava abate sem autor, e o placar juntaria todos os bots
numa linha só.

---

## Do menu à partida

```
menu.tscn (LobbyShell)
  Entrada (apelido) ─> Lista de salas ─> Sala ── Iniciar (5 s) ──> main.tscn
		header fixo: Salas · Loja · Inventário · Perfil · Missões · Opções
main.tscn ── fim ──> Resultado ── Jogar de novo ──> main.tscn
							   ── Voltar à sala ──> menu.tscn, na sala
							   ── Sair para o lobby ──> menu.tscn, na lista
```

O lobby é **uma cena só** com moldura fixa: o header nunca sai, o miolo
troca (`LobbyShell.Go`). Loja e inventário não são cenas separadas. O
fim da partida **volta à sala**, não à lista: manter o grupo junto é o
que segura a sessão longa.

**Lista de salas.** Grade 2 × 5 por página, filtros, "ir para a sala
nº", atualização a cada 5 s. As salas dos outros são simuladas
(`RoomDirectory`), com bots dentro; entrar numa delas funciona.

**Sala.** Times Azul e Laranja, até 8 em cada. O mestre (quem criou —
hoje, você) escolhe modo, tamanho, objetivo, tempo, regra de armas, de
transformação, dificuldade dos bots, faixa de patente, senha e fogo
amigo; enche com bots e troca de lado. Cada jogador marca **Pronto**,
menos o mestre: quem inicia é ele, então o botão some para ele. O
mestre inicia quando todos estão prontos — ou quando os prontos já
fecham times equilibrados, com no máximo **um a menos** de um lado
(esperar o último indeciso trava a sala; começar 5 contra 2 estraga a
partida). Quem não estava pronto fica na sala e não joga. Bots estão
sempre prontos. Iniciar abre uma contagem de 5 s, cancelável.

A regra de "pronto" existe hoje com um humano só, de propósito: na
Fase 2 muda de onde vêm os jogadores, não como a sala funciona.

`RoomConfig` (estático) leva a sala para a partida, porque sala e
partida são cenas diferentes. Com `Active` falso — partida aberta
direto pelo editor — o `MatchSetup` monta a escalação padrão e
`RoomConfig.Rules` devolve as regras padrão.

**Regras que valem na partida** (`RoomRules`): fogo amigo; "Sem
pesadas" deixa indisponível o conjunto com escopeta; "Só pistolas" põe
pistola nas duas mãos e tira as granadas, inclusive dos bots;
"Bloqueada" tira o lobo de todo mundo.

Na partida, o `MatchSetup` distribui cada time pelos spawns da sua
base em ordem, ninguém em cima de ninguém. O time do jogador é
aplicado no `_EnterTree` do Player, antes de corpo e barra de vida
escolherem cor.

**Esc não pausa.** É jogo multijogador: não existe pausa com outras
pessoas na sala, e treinar com a regra certa evita o hábito de abrir
o menu para fugir de uma luta. Com o menu aberto o corpo fica parado
(o mouse é do menu), mas continua tomando tiro. O Esc tem Opções
(volume por canal, sensibilidade e a mira) e Controles, os mesmos do lobby.

---

## Arsenal

Especificação: `docs/ARSENAL.md`. Implementação:
`scripts/combat/WeaponDefinition.cs` (um Resource por arma),
`scripts/combat/Arsenal.cs` (catálogo e preços) e
`resources/weapons/*.tres`, gerados por `tools/gerar_arsenal.py`.

Só a **primeira leva** do documento: M16A2, SG-552, AK-47, UMP-45 e M3,
mais a M9 do conjunto padrão. O documento explica por quê — cinco armas
bastam para saber se humano contra lobo funciona, e as outras bagunçam o
balanceamento se entrarem antes.

### Decisões que divergem do documento, e por quê

| Ponto | Documento | Jogo | Motivo |
|---|---|---|---|
| Vida do humano | 100 | **200** | Decisão do Jhon. O dano é multiplicado por `Arsenal.DamageScale = 2` na hora do tiro, o que preserva tiros-para-matar e TTK do documento sem reescrever os números |
| Reserva de munição | 90, 100… | **infinita** | Regra antiga do Jhon: o custo de atirar é o tempo de recarga. A reserva fica na definição só para exibir |
| Lobo | vendido no respawn | **grátis, tecla 3** | Decisão do Jhon: virar lobo é decisão tática, não compra |
| Cone da M3 | 2,5° a 0 m → 9° a 20 m | **4,5° fixo** | Cone que cresce com a distância é aproximado por um meio-ângulo fixo; a 20 m dá 1,6 m de raio |
| Rajada (SG-552, G36C) | modo extra | **só automático** | Um modo por arma até alguém sentir falta |

**Mexeu num número de dano?** Confira "tiros para matar" no documento.
O AK mata com um tiro na cabeça porque 42 × 2,4 × 2 = 201,6 passa de
200 — um ajuste de 1 ponto quebra a identidade da arma.

### O que a definição controla

Dispersão (base, por tiro, máxima, recuperação, multiplicador andando
e agachado), recuo vertical em duas fases e horizontal alternado ou
sorteado, queda de dano por distância, cadência, modo de tiro, pente,
recarga tática e vazia, recarga cartucho a cartucho interrompível,
chumbos por disparo, lentidão no lobo, velocidade com a arma na mão e
raio do estampido para a audição dos bots.

A dispersão de um tiro é a de **antes** dele: o primeiro tiro parado é
sempre o mais preciso. Os chumbos de um disparo somam dano por alvo
antes de aplicar, para abate e marcador saírem uma vez, não nove.

O recuo lateral não volta sozinho. É ele que o jogador aprende a
compensar, e devolvê-lo automaticamente apagaria o que diferencia uma
arma da outra.

---

## Conjuntos e economia

Especificação: `docs/CONJUNTOS.md`. Implementação:
`scripts/combat/LoadoutSet.cs`, `scripts/core/Profile.cs` (salvo em
`user://perfil.json`) e a tela de respawn em `scripts/ui/LoadoutMenu.cs`.

- Quatro conjuntos. O **primeiro é o padrão**: só itens grátis, sempre
  disponível, e escolhê-lo rende **+50 WP**. O `Profile.Repair` força
  isso a cada leitura — é a rede de segurança de quem está sem WP.
- O custo é **soma simples** e **nunca é salvo**: recalcula do arsenal,
  então ajuste de preço chega a todos os perfis sozinho.
- **Sem teto de custo.** Conjunto de 2000 WP é meta, não erro. O
  documento avisa que essa é a primeira regra que alguém tenta
  consertar. Não conserte.
- Editar só fora da partida. Na partida só se escolhe e paga.
- Respawn: 5 s, cartões que não cabem aparecem acinzentados com
  "faltam X", estouro do tempo usa o último conjunto ou o padrão.
- Paga-se no renascer. O equipamento some na morte, e **morrer custa
  100 WP** (`WpLostOnDeath`, decisão do Jhon) — um abate. Nunca abaixo
  de zero; o padrão continua devolvendo +50.

| Ganho | WP |
|---|---|
| Abate | 100 |
| Abate de lobo | 70 |
| Assistência (acertou nos 10 s antes da morte) | 35 |
| Escolher o padrão | 50 |
| Teto | 2500 |

Dano não rende mais WP: pagar por dano fazia spray em parede valer
ponto. A assistência existe porque sem ela quem tira 90% da vida e
perde o abate sai sem nada, e o jogo passa a premiar roubar o último
tiro.

O editor de conjuntos fica no Inventário e só aceita item que você
tem. Item com prazo vencido some do conjunto na próxima leitura do
perfil (`Profile.Repair`), e o conjunto volta a valer com o que sobrou.

A tela de respawn mostra todo conjunto, com o motivo de cada um
bloqueado: regra da sala, item fora do inventário ou "faltam X WP".
Morto, ela abre com "ABATIDO" e de quê.

---

## HUD de combate

`scripts/ui/hud/`, no layout da regra dos cantos (veja "Layout da
tela"). Tudo chega por sinal — vida, munição, WP, forma, slot, abates —
exceto o que é contínuo por natureza e é lido a cada quadro: abertura
da mira, barra de transformação, recargas do lobo, uivo, relógio de
renascer e vinheta.

**Mira** (`Crosshair.cs`): quatro hastes que se afastam com a dispersão
da arma (`Weapon.CurrentSpread`). No lobo **não aparece** — o lobo não
mira, ele chega, e em terceira pessoa o próprio lobo no meio da tela já
diz para onde vai.

**Marcador de acerto** (`HitMarker.cs`): branco no corpo, vermelho e
maior na cabeça, e segura mais no abate. O som de cada um é diferente.

**Dano direcional** (`scripts/ui/DamageIndicator.cs`): arco vermelho em
volta da mira apontando para quem atirou, por 1,5 s. Guarda a posição
de origem no mundo, não o ângulo do momento — é o que faz o arco girar
quando você vira, continuando a apontar para o atirador.

**Recargas do lobo** chegam pelo sinal `Weapon.AttackCooldown(tipo,
segundos)` — garrada, salto e bote —, e o uivo por
`Player.HowlProgress`.

**Minimapa** desenha toda caixa do mapa com a cor do material dela, as
mais altas por cima (telhado cobre o que está embaixo) e mais claras
quando bem acima de você. Letras curtas do mapa (o "A" e o "B" das
bases) viram marcas. Antes ele procurava um nó "Walls" que só existia
na caixa cinza antiga, e no Hangar não desenhava nada.

---

## Morte

**Quem te matou fica com a silhueta vermelha, só para você, e a câmera
vira para ele** (`KillCam`, decisão do Jhon), **com um cartão em cima
dele: patente e nome**. A silhueta é um
`MaterialOverlay` vermelho sem teste de profundidade nas malhas do
matador — aparece através das paredes, mais forte na borda — e sai
quando você renasce. A câmera é própria: sobe por cima do seu corpo, do
lado oposto ao matador (de trás do ombro o corpo caído o tapava), vira
para ele e fecha o quadro em uns 10 m em volta dele, até 15° — a 70 m
ele era um ponto. A escolha de conjunto espera 1,8 s para abrir
(`LoadoutMenu.KillCamTime`): o fundo escuro dela cobriria justamente
isso. O matador é o último que te acertou nos últimos 5 s
(`Health.Killer`); a granada conta quem a lançou.

**Matou ou morreu, sobe o cartão do duelo** (`DuelCard`, decisão do
Jhon): no centro de baixo, você à esquerda e o outro à direita, com a
patente, o nome e **quantas vezes cada um matou o outro nesta
partida**; no meio, o que aconteceu, a arma e a distância. Some em 4 s.

**Inimigo tem a borda levemente vermelha** (`EnemyMarks`, decisão do
Jhon), para separar de aliado à primeira vista. É a mesma marca do
matador, mais fraca e **com** teste de profundidade: só onde você já
vê o inimigo. Através de parede seria ver quem está escondido — isso
fica só para o matador, e só enquanto você está morto (`Silhouette`).

**A morte começa no quadro da morte.** O "mutant dying" fica parado
0,43 s antes de cair; tocado inteiro, o lobo morto ficava meio segundo
de pé. O clipe começa em 0,55 s (`WolfMoves`).

O corpo cai com a colisão **ligada** até tocar o chão, e só então a
colisão desliga e o corpo **para de se mover de vez** — sem gravidade.
A segunda metade foi o defeito: sem colisão, `IsOnFloor()` responde
falso no quadro seguinte, porque não existe chão para quem não colide,
e a gravidade voltava a puxar o corpo através do mapa.

---

## Armas na mão, soltar e pegar

**Cada arma tem o seu modelo** (`scripts/visual/WeaponModel.cs`),
montado com caixas e cilindros: a alça do M16, a madeira e o carregador
curvo da AK, o tubo da M3. O mesmo modelo aparece em três lugares —
na sua mão (`ViewModel`), na mão dos outros (`HeldWeapon`) e no chão
(`WeaponPickup`) —, então a AK caída é a mesma AK que o inimigo
segurava. Arma nova precisa de um caso em `WeaponModel.Build`; sem
ele, aparece um fuzil genérico.

**Recarga animada.** Em primeira pessoa a arma inclina, o carregador
sai por baixo e volta, e ela endireita; na escopeta cada cartucho é um
empurrão. Em terceira pessoa o clipe `reload` do Mixamo toca **só da
cintura para cima** (disparo único filtrado nos ossos do tronco e dos
braços), então quem recarrega correndo continua correndo. As duas
seguem o relógio da arma (`Weapon.ReloadProgress`): recarga vazia, mais
longa, dura mais na tela também.

**A arma na mão dos outros sem calibrar osso.** As animações são de
fuzil, com a mão direita no punho e a esquerda no guarda-mão: a arma
vai na mão direita apontando para a esquerda. Na recarga a mão
esquerda sai para buscar o carregador, então a arma congela a relação
com a mão direita do último quadro bom e só a acompanha.

**Soltar e pegar, como no CS:**

- **B** joga a arma da mão para frente. Da primária você vai para a
  secundária; da secundária, para a faca. Faca e granada não se soltam.
- **Morto em forma humana deixa cair** a primária (ou a pistola, se só
  tinha ela), com o pente como estava. Lobo não deixa nada. Quem
  morreu não perde o que tinha: renasce com o conjunto que escolher.
- **Passar por cima pega** — só se o espaço daquela arma estiver
  vazio. Para trocar, solte a sua primeiro. Pegar uma primária põe na
  mão; pegar uma pistola com a primária na mão, não.
- Quem soltou não pega de volta no primeiro 1,2 s. A arma no chão some
  em 30 s, e o mapa guarda no máximo 20.
- A arma no chão é uma **área, não um corpo rígido**: o voo é feito à
  mão, com raio de quadro em quadro. Corpo rígido viraria obstáculo
  para o tiro, para a visão do bot e para a câmera do lobo.

---

## Personagens

Todos do Mixamo, com o mesmo esqueleto `mixamorig` — por isso os
clipes servem para todos sem conversão.

| Quem | Arquivo | Animações |
|---|---|---|
| Humano, time azul | `assets/characters/human/exo_gray.fbx` (Exo Gray) | os clipes de `assets/characters/human/` |
| Humano, time laranja | `assets/characters/human/ely.fbx` (Ely, de K. Atienza) | os mesmos |
| Lobo, time azul | `assets/characters/wolf/mutant.fbx` | Creature Pack, em `assets/characters/wolf/` |
| Lobo, time laranja | `assets/characters/wolf/mutant_red.fbx` | os mesmos |

O `CharacterAnimator` troca o `xbot` da cena pelo personagem do time
antes de carregar os clipes (`BlueModel`, `OrangeModel`); com
personagem próprio o corpo não é pintado.

**Os clipes humanos são do X Bot**, e a trilha do quadril vem em
metros do corpo dele (quadril a 1,04 m). O Exo Gray (0,96 m) e a Ely
(0,98 m) têm pernas mais curtas, e com o quadril do X Bot os pés
ficavam 6 a 8 cm acima do chão. Cada clipe é ajustado na carga pela
razão entre os dois quadris em repouso
(`AnimationKit.MatchHips`). Os clipes do lobo são do próprio
Mutant e já pisam no chão. Trocou o personagem? Não precisa mexer em
nada: a razão é lida dos dois esqueletos. O `WolfVisual` faz o mesmo
para o lobo. O `mutant_red.fbx` veio do Blender, sem `AnimationPlayer`
e com o esqueleto dentro de `Armature`: o tocador é criado ao lado do
esqueleto, onde o caminho `Skeleton3D:osso` dos clipes existe.

Clipes do lobo: parado num ramo, andar e correr no outro, e o passo
**no ritmo do corpo**: o clipe toca na razão entre a velocidade do corpo
e a passada dele, medida no Mutant (andar 1,34 m/s, correr 2,42 m/s, já
na escala do jogo; varrido de 2,2 a 3,3 no esqueleto, 2,42 é a de menor
patinação) — o Bruto a 3,89 m/s corre o clipe a 1,61×. Antes ele tocava
a 1× até 7 m/s, e o corpo andava mais do dobro das pernas. Só anda até
2,0 m/s e só corre de 2,6 m/s: nenhuma velocidade do jogo para entre os
dois (os ciclos têm tamanhos diferentes, e misturados o pé patina).

**O lobo vira para onde anda** (`WolfVisual.UpdateLocomotion`). Só há
clipes para a frente: de lado (A, D e as diagonais da frente) o modelo
gira até 90° e anda de frente para onde vai; para trás (S e as
diagonais de trás) fica de frente e o passo toca **ao contrário**. A
divisa tem folga (105° a 120°) para não dar meia-volta à toa. Golpe,
carga do salto, bote, salto e parede voltam o modelo para a mira
(1400°/s); parado, ele fica virado para onde parou — voltar arrastaria
os pés sem passo. Medido no pé de apoio: de lado a patinação foi de 5,4
para 0,85 m/s, para trás de 7,3 para 0,72; para a frente fica em 0,73
(o pé rolando no apoio). Só a cápsula, a câmera e a mira continuam
olhando para a frente.

**Parando, o passo congela onde o corpo parou** — o clipe toca na
velocidade do corpo, que já é zero em 0,08 s — e as pernas se juntam em
0,22 s (`StopBlend`). Antes a velocidade da animação vinha com atraso e
um mínimo de 0,6×: o lobo corria no lugar mais um quarto de segundo, os
pés andando um metro com o corpo parado.

**Agachado** o lobo encolhe, parado, no fim da aterrissagem do "jump
attack" (1,95 a 2,25 s, indo e voltando): 0,98 m do chão ao topo da
cabeça, a altura da cápsula agachada (1,0 m). Andando agachado ele
**engatinha**: o Running Crawl da parede, no chão, na velocidade do
corpo (1,75 m/s toca a 0,76×), 10 cm mais alto para as mãos não
entrarem no chão (`CrawlLift`). Antes o lobo agachado continuava de pé,
a cabeça a 1,7 m — fora da cápsula, e o tiro passava por cima do que se
via. Testado e descartado: misturar a pose agachada no andar (o pé de
apoio patinava 0,9 a 1,4 m/s em qualquer passada) e pôr só no tronco (a
cabeça ficava a 1,58 m). Um clipe de andar agachado do Mixamo aplicado
no Mutant resolveria melhor que o engatinhar;
as garradas e o rugido **da cintura para cima**; o salto e o bote no
corpo inteiro; **o pulo**, só o voo do "mutant jumping", tocado pela
velocidade vertical (o agachar do clipe fica de fora: a física sai do
chão no quadro do clique), com o quadril na altura de pé — quem sobe é
a cápsula —, e a aterrissagem do clipe quando o lobo chega devagar
(chegando correndo, volta direto para a corrida, senão o pé patina).
**No ar o lobo cai, não anda**: soltando da parede, saltando dela ou
caindo de uma beirada, fica a pose do topo do pulo (encolhido, braços
para cima) até o chão chegar a 1 m, e aí as pernas esticam para pousar
— a altura é medida por um raio para baixo. Um instante no ar descendo
rampa (menos de 0,12 s) não conta. A morte; e, na parede, o
**Running Crawl** com a barriga virada para a parede em que está — o
modelo deita para o lado da parede (pela normal dela), a cápsula e a
câmera continuam de pé. **Virar lobo não tem animação** (decisão do
Jhon): o lobo aparece já na locomoção.

O clique esquerdo **alterna** dois golpes, o "swiping" e o "punch", um
braço de cada vez. O "jump attack" serve três clipes: inteiro no salto
(direito no chão); só a queda no bote (direito no ar), do ponto mais
alto ao impacto; e, quando o bote encosta, a aterrissagem. **O lobo não tem animação de pulo**, decisão do Jhon: ele só pula,
e o corpo segue na pose em que estava. Trechos, velocidades e instante
do golpe: `WolfMoves` (veja "As garras").

Texturas embutidas nos `.fbx` são extraídas pelo Godot na importação
(`exo_gray_0.png`...) e ficam fora do git: cada máquina gera as suas.

---

## Fora da partida: perfil, patentes, loja e missões

Tudo salvo em `user://perfil.json` (`Profile`, versão 2; o perfil da
versão 1 é migrado). Opções ficam à parte, em `user://opcoes.json`.

- **Duas moedas.** **Presas** se ganham jogando; **Sangue** é a moeda
  paga e só aparece — vender exige servidor. **WP não é moeda de
  lobby**: vive só dentro da partida. Perfil novo começa com 3.000
  Presas.
- **Patentes** (`meta/Ranks.cs`): 30, em 10 famílias de 3 graus
  (Filhote I até Alfa III). XP para a patente *r*: 250·r^1,9. Cada
  promoção dá Presas (150 + 100 por família) e a tela de promoção.
  Patente não cai. **Bot não tem patente** (decisão do Jhon, 13/11): no
  placar, na sala e no cartão de duelo aparece sem insígnia, como "Bot".
- **Partida rende** (`meta/Progression.cs`): 150 XP e 100 Presas por
  jogar, mais abate, assistência, vitória ou empate, e MVP. Abandonar
  não rende nada.
- **Loja** (`meta/Catalog.cs`): armas, granada, tatuagens (bônus
  pequenos de velocidade, pulo, escudo, garra e perda de escudo) e
  troca de nome. Prazos de 1, 7, 30 e 90 dias ou permanente; quatro
  itens do dia com 20% de desconto. Item com patente mínima aparece
  trancado, com a patente que destrava. A raça Ligeiro não é vendida:
  destrava na patente.
- **Missões** (`meta/Missions.cs`): três diárias e cinco semanais
  sorteadas pela data (mesmo dia, mesmas missões), e as permanentes,
  que dão título. **O resgate é manual** — o clique é o momento da
  recompensa.

---

## Interface: o kit

Guia completo, com as decisões de cada tela: `docs/UI.md`. Toda tela
nova usa `scripts/ui/kit/` — não crie `Label` com tamanho e cor soltos.

- **Tokens** em `UiTokens`: grade de 8 (4 · 8 · 16 · 24 · 32 · 48 ·
  64), raios, paleta, tempos de animação. Número fora da grade é bug.
- **Tema feito em código** (`UiTheme`) e aplicado na raiz de cada tela
  com `UiTheme.Apply(...)`: `CanvasLayer` corta a herança de tema, e
  sem isso a tela sai com o tema padrão do Godot.
- **Variações** em vez de override: `ThemeTypeVariation = "Title"`,
  `"Caption"`, `"Numeric"`, `"Surface1"`, `"HudPanel"`... Número de
  moeda, munição e placar usa fonte tabular (`Numeric`), senão treme
  ao contar.
- **Um primário por tela.** Primário dourado e sólido; secundário com
  contorno; terciário só texto.
- **Tudo anima** (`Motion`): número conta, painel desliza, nada aparece
  seco. 120 a 200 ms.
- Uma classe Godot por arquivo, com construtor sem parâmetros. Não
  chame método de `Show`, `Panel` ou `Button`: colide com
  `CanvasItem.Show` ou com tipos do Godot.
- Lista que se atualiza sozinha reaproveita os nós (veja
  `Scoreboard`). Recriar dezenas de controles por segundo acumula
  objetos do lado C# e derruba o jogo ao fechar.

---

## O teste de mapa

Antes de colocar arte em qualquer mapa, ande por ele em cinza e
responda:

1. Existe um caminho do spawn ao centro que só o lobo consegue fazer?
2. Existe um lugar onde o humano vê o lobo chegando com tempo de reagir?
3. Existe um corredor onde o humano sabe que vai perder e precisa evitar?
4. Dá para cair em cima de alguém de um ponto que ele não olha?

Se alguma resposta for não, o mapa ainda não é deste jogo — é um mapa
de FPS comum com um lobo solto dentro.
