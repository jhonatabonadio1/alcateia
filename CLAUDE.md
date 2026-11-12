# Alcateia — guia para agentes

FPS multiplayer com transformação humano ↔ lobo, em Godot 4 + C#.
Sucessor espiritual do WolfTeam: mecânica copiada livremente, nome,
arte e nomes de classe próprios.

Leia este arquivo inteiro antes de mexer em qualquer coisa.

---

## Regra zero

**Fase 1 é local, sem rede.** Não implemente multiplayer, RPC,
`MultiplayerSynchronizer` ou qualquer sincronização até a Fase 1 estar
fechada. O objetivo é descobrir se a mecânica é divertida antes de
pagar o custo de sincronização — netcode em cima de gameplay não
validado é retrabalho garantido.

`GameManager.cs` existe mas está dormente. Não o ative.

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

9. **O humano é deliberadamente lento e pesado.** Velocidade 4.2 e
   pulo 3.0. Toda a mobilidade do jogo pertence ao lobo; o humano
   compra distância e tempo com arma e granada, não com as pernas.

   Atenção ao mexer: a velocidade e o pulo do lobo são
   **multiplicadores** dos valores humanos. Baixar o humano encolhe o
   lobo junto e o contraste entre as formas não muda. Para separar as
   duas, mexa no `SpeedMultiplier` das classes, não só aqui.

   Valores absolutos atuais:

   | | Humano | Bruto | Ligeiro |
   |---|---|---|---|
   | Velocidade | 4.2 | 6.3 | 8.8 |
   | Pulo | 3.0 | 5.7 | 7.5 |
   | Agachado | 1.9 | 2.8 | 4.0 |

10. **O alcance do pulo é controlado pela tecla.** Segurar para frente
    mantém o avanço até o fim; soltar encurta, via um arrasto suave no
    ar (`AirFriction`). Não existe distância máxima fixa — quem decide
    é o jogador, durante o salto. Os dois extremos são erros: zerar a
    velocidade trava o corpo no ar, e conservar tudo tira a escolha.
    O controle de direção no ar é parcial (`AirAccel`).

11. **Agachar vale nas duas formas.** Encolhe a cápsula, baixa o olho
    e corta a velocidade para 45%. Não se agacha na parede — a corrida
    já é um estado de corpo inteiro.

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

---

## Arquitetura

```
alcateia/
├── CLAUDE.md           este guia (fica na raiz, é onde agentes olham)
├── AGENTS.md           atalho para o mesmo arquivo
├── project.godot
├── docs/
│   └── CHANGELOG.md
├── scenes/             tudo que é .tscn
│   ├── main.tscn       cena principal
│   ├── player.tscn
│   ├── bot.tscn
│   ├── dummy.tscn
│   └── grenade.tscn
├── resources/
│   └── breeds/         estatísticas das classes de lobo
│       ├── bruto.tres
│       └── ligeiro.tres
└── scripts/
    ├── actors/         quem ocupa o mundo e decide
    │   ├── Player.cs
    │   └── Bot.cs
    ├── combat/         dano e o que o causa
    │   ├── Health.cs
    │   ├── Weapon.cs
    │   └── Grenade.cs
    ├── forms/          a transformação e seus dados
    │   ├── FormController.cs
    │   └── WolfBreed.cs
    ├── ui/
    │   └── Hud.cs
    ├── core/
    │   └── InputSetup.cs
    ├── net/            dormente até a Fase 2
    │   └── GameManager.cs
    └── debug/          peças descartáveis
        └── Dummy.cs
```

| Arquivo | Responsabilidade |
|---|---|
| `actors/Player.cs` | Movimento, corrida em parede, câmeras, agachar, stun, input |
| `actors/Bot.cs` | Percepção, combate à distância, strafe, decisão de forma |
| `combat/Health.cs` | HP, escudo, dano, morte, respawn |
| `combat/Weapon.cs` | Slots, tiro hitscan, garrada, granada |
| `combat/Grenade.cs` | Projétil de atordoamento com física |
| `forms/FormController.cs` | Máquina de estados da transformação + economia de WP |
| `forms/WolfBreed.cs` | Resource com estatísticas por classe |
| `ui/Hud.cs` | Barras e avisos, construído por código |
| `core/InputSetup.cs` | Registra ações de input sem passar pelo editor |
| `net/GameManager.cs` | Rede — dormente até a Fase 2 |
| `debug/Dummy.cs` | Boneco de teste — descartável |

### Por que não há pasta `player/`

`Health`, `Weapon` e `FormController` **não pertencem ao jogador**: o
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
tempo de parede, nada de leitura de estado visual.

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
| Botão direito | Patada dupla (só de lobo, só no ar) |
| Clique esq. | Atacar |
| Ctrl | Agachar (nas duas formas) |
| Shift | Andar devagar e em silêncio (só humano) |
| R | Rugir segurando (lobo) · recarregar (humano) |
| Tab | Menu de dificuldade |

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

Só na forma lobo, só no ar, só segurando W, **e só na descida do
pulo** — encostar na parede no chão não gruda. É preciso pular e
esperar começar a cair. Isso faz grudar ser uma decisão, não um
acidente de encostar na parede.

- Sem limite de tempo: corre enquanto a parede existir
- Solta ao largar o W, ao bater em obstáculo, ou se atordoado
- **Altura travada**: não sobe nem desce enquanto corre
- **Direção travada**: escolhida uma vez, ao grudar, pelo **momento**
  que o lobo já carrega — não pela câmera. Saltar de uma parede e
  encostar na seguinte mantém o sentido sem tocar no mouse. O olhar só
  decide quando não há momento horizontal. Acompanha a curva da
  parede, mas nunca se inverte: para ir ao outro lado é preciso cair
  e recomeçar
- A busca por parede é um leque de 8 raios em volta, não dois raios
  laterais: o ângulo do corpo não decide se existe parede ali
- O olhar fica livre durante a corrida: mirar não desvia o movimento
- A/D não controlam durante a corrida
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
- **Contorna quinas**: ao acabar uma face, troca para a face seguinte
  sem soltar, com uma janela curta de tolerância. Dá para dar a volta
  num bloco inteiro colado
- Sucção constante contra a parede mantém o lobo colado, exceto
  durante a transição de quina, onde ela o prenderia no canto
- Ao trocar para uma parede em ângulo, quem decide o sentido é o
  **movimento** que o lobo carrega, não o sentido antigo: em paredes
  perpendiculares a comparação com o sentido antigo dá zero e o
  desempate mandava o lobo de volta pelo outro lado
- Quinas muito finas não funcionam: os raios precisam de superfície
  para encontrar. Paredes de cerca de 1m de espessura são adequadas

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

---

## Convenções de código

- Comentários em português, explicando **por quê**, não o quê
- Números de balanceamento sempre `[Export]`, nunca constante escondida
- Estatísticas de classe vão em `.tres`, não em código
- Um `[Signal]` para cada coisa que o HUD precisa saber; o HUD nunca
  faz polling
- Peças descartáveis (Dummy, HUD atual, clarão da granada) estão
  marcadas como tal no comentário de topo

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
o próprio recuo no lote.

### O lobo de perto

Alcance de garra maior que o de uma faca, e **resistência no corpo**
(0,6 no Bruto, 0,78 no Ligeiro). O lobo é uma ameaça de frente, não só
um corpo rápido.

**Headshot atravessa a resistência inteira.** Sem essa saída, o humano
com rifle não teria resposta nenhuma contra um lobo vindo de frente —
e um inimigo sem resposta não é difícil, é injusto.

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

### A economia de WP

WP vem de **abater** (150) e de **renascer** (40), nunca do relógio.
Ganhar por tempo premia esperar; ganhar por abate premia jogar. O
bônus por renascer existe para que morrer não deixe o jogador sem
recurso nenhum — castigo que se acumula afunda quem já está perdendo.

As classes básicas custam 0 WP; o recurso existe para o que ainda não
foi feito: lotes de armamento e classes caras.

A transição de ida é mais longa que a de volta (0,55s contra 0,2s).
Virar lobo é um compromisso; voltar é uma retirada, e punir a retirada
com a mesma espera só faz o jogador se sentir travado.

### Times e feed de abates

Azul é o jogador, vermelho são os bots. **Fogo amigo desligado**: num
jogo de duas formas, confundir aliado com inimigo já acontece demais
pela silhueta.

O feed no canto superior direito não é enfeite. É como o jogador
entende o que aconteceu fora do campo de visão e, principalmente, de
que ele morreu. Morrer sem saber a causa é a forma mais rápida de um
jogo parecer injusto.

### As garras

Clique esquerdo: uma patada por clique, nunca automático.

**Contra humano a patada vale 1,6×.** O lobo é feito para caçar
gente, não para duelar com outro lobo — sem isso, o encontro entre
dois lobos vira o combate padrão do jogo, que é o oposto do desenho.

**Botão direito no ar: investida.** O lobo despenca na direção em que
olha e dá duas patadas em sequência, com 1,45× de dano e cooldown
quase o dobro.

O mergulho é o que transforma o golpe numa investida em vez de dois
golpes dados no ar — e é também o compromisso: o jogador escolhe o
ponto de impacto no clique e não controla mais nada até aterrissar.

A condição de estar no ar amarra o golpe mais forte do lobo ao salto,
que é a jogada que o jogo quer premiar. Errar custa o dobro do tempo.

**Os braços do lobo ficam no corpo, não na câmera.** A forma é em
terceira pessoa: o dono precisa ver os próprios braços e os outros
precisam reconhecer a silhueta de longe. Numa luta rápida ninguém lê
textura — o que separa um lobo de um humano a trinta metros é o
contorno.

### O escudo do lobo

O escudo **escorre sozinho** enquanto o lobo está transformado. Não é
vida extra: é um relógio. Segurar R rugindo recupera o escudo, ao
preço de ficar parado e de entregar a posição pelo som.

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

**Agachado e andando com Shift não fazem barulho nenhum.** São as
duas ferramentas de furtividade do humano, e é o que dá escolha
contra um inimigo que ouve. Andar devagar é só do humano: o lobo vive
de movimento, e dar a ele a opção de abrir mão disso criaria um lobo
furtivo, que é o oposto da forma. É isso que dá ao agachamento um
propósito tático em vez de ser só um botão de ficar menor.

O passo sai por distância percorrida, não por tempo: a cadência
acompanha a velocidade sozinha e andar devagar faz menos barulho.

Quando houver áudio de verdade, ele acompanha o evento em vez de
substituí-lo. O que o bot ouve e o que o jogador ouve precisam ser a
mesma coisa, senão o jogador não consegue prever a reação.

### Como o bot pensa

Três camadas, que é como IA de FPS se organiza desde os anos 2000:

**Percepção** — visão e audição são sentidos independentes que
alimentam uma única crença: onde o alvo *provavelmente* está. Visão
exige distância, ângulo e linha de visada. Audição dá posição com
erro, porque ouvir não é ver — um bot que localiza som com precisão
cirúrgica parece trapacear.

**Estado** — máquina explícita com quatro estados:

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

Três níveis, em `scripts/core/Difficulty.cs`. O que muda é
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
| Velocidade | 3,4 | 4,2 | 4,9 |
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
- **Levar dano**: faixas vermelhas nas quatro bordas da tela, que
  somem em menos de meio segundo.
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

## Estado atual e próximos marcos

Veja `CHANGELOG.md` para o que já entrou.

**Fase 1 — falta:**
- Bot que navega, persegue e ataca (sem alguém se mexendo, não dá para
  saber se o combate funciona)
- Escalada de parede
- Placar
- Som — o uivo precisa ser audível à distância, é sinal de posição

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

## O teste de mapa

Antes de colocar arte em qualquer mapa, ande por ele em cinza e
responda:

1. Existe um caminho do spawn ao centro que só o lobo consegue fazer?
2. Existe um lugar onde o humano vê o lobo chegando com tempo de reagir?
3. Existe um corredor onde o humano sabe que vai perder e precisa evitar?
4. Dá para cair em cima de alguém de um ponto que ele não olha?

Se alguma resposta for não, o mapa ainda não é deste jogo — é um mapa
de FPS comum com um lobo solto dentro.

---

## A pergunta que decide o projeto

**Transformar é gostoso? Saltar da parede em cima de alguém é gostoso?**

Se não der prazer com cápsula cinza e sem som, não vai dar com modelo
caro. Arte conserta mapa feio; não conserta mecânica morna.
