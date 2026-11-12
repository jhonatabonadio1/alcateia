# Alcateia — guia para agentes

FPS multiplayer com transformação humano ↔ lobo, em Godot 4 + C#.
Sucessor espiritual do WolfTeam: mecânica copiada livremente, nome,
arte e nomes de classe próprios.

Leia este arquivo inteiro antes de mexer em qualquer coisa.

---

> Para entender como o código está montado e onde mexer em cada coisa,
> veja `docs/ARQUITETURA.md`. Este guia trata das regras e decisões.

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

**Licenças de assets de terceiros.** Todo som tem a origem em
`assets/audio/CREDITOS.md`. A maior parte é CC0; o Oculus Audio Pack é
**CC BY 4.0** e exige crédito visível no jogo publicado. Asset novo só
entra com licença que permita uso comercial, e com a linha dele nos
créditos no mesmo commit.

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
| Botão direito | Bote aéreo (só de lobo, só no ar) |
| W segurado | Sustenta a corrida em parede |
| Ataque segurado | Agarra na parede sem avançar |
| Clique esq. | Atacar |
| Ctrl | Agachar (nas duas formas) |
| Shift | Andar devagar e em silêncio (só humano) |
| R | Rugir segurando (lobo) · recarregar (humano) |
| Esc | Menu da partida (continuar, reiniciar, voltar ao menu, sair) — **não pausa** |
| Tab (segurar) | Placar da partida |
| Enter | Jogar de novo, na tela de fim |

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

Só na forma lobo e no ar. **Pula-se contra a parede e segura o pulo**
— é a tecla de pulo que sustenta a corrida, não o W.

**Segurar o botão de ataque agarra sem avançar.** É a pausa que
permite escolher a hora de saltar em vez de correr até a parede
acabar.

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
- **O salto da parede sai mais alto do que entrou**
  (`WallJumpClimb`), então dá para escalar um corredor ricocheteando
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

A granada sabe quem a lançou (`Grenade.Thrower`). Aliado passa ileso,
dano e atordoamento; quem lançou, não — granada aos próprios pés tem
que custar alguma coisa. Abate de granada entra no feed, no placar e
rende WP como qualquer outro. Antes não contava para ninguém, o que
numa partida por pontos é abate jogado fora.

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

| Canto | O quê |
|---|---|
| Inferior esquerdo | Vida e escudo — o corpo |
| Inferior direito | WP, munição e forma — o equipamento |
| Superior direito | Minimapa quadrado, e o feed de abates abaixo dele |
| Rodapé central | Barra de slots, só ao trocar de arma |

A barra de slots aparece no gesto de trocar e some sozinha. Ela serve
para o jogador conferir o que tem sem abrir menu; permanente,
ocuparia tela o tempo todo para informar algo que ele já sabe na
maior parte do jogo.

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

O relógio da tela de lotes também só corre quando ela é a tela de
cima, senão o tempo escorre por trás de outro menu e o lote é
escolhido sozinho.

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

Clique esquerdo: **combo de três golpes**. O terceiro machuca mais, e
encadear é mais rápido que recomeçar.

**O golpe não é hitscan.** Cada patada cria um volume curto à frente
do corpo e pega quem estiver dentro. A diferença é concreta: um raio
sai da câmera e acerta onde se mira, enquanto um volume preso ao corpo
acerta o que estiver perto da garra. É isso que faz o alcance parecer
variar com a postura em vez de seguir o olhar.

**Cada golpe empurra o lobo para frente** (`ClawLunge`). Sem isso o
combo deixa o jogador batendo no ar enquanto o alvo anda — o golpe
precisa levar o lobo até o alvo.

**Contra humano a patada vale 1,6×.** O lobo é feito para caçar
gente, não para duelar com outro lobo — sem isso, o encontro entre
dois lobos vira o combate padrão do jogo, que é o oposto do desenho.

**Botão direito no chão: patada pesada da direita.** Um golpe, 2,1×
de dano, um segundo parado. É o castigo de quem deixou o lobo chegar
parado — e o segundo de recuperação é o que impede que seja a resposta
para tudo.

**Botão direito no ar: bote de três golpes.** O corpo viaja na direção
da **mira inteira**, com o pitch: olhar para baixo dá um bote curto e
mergulhado, olhar à frente dá um salto longo e raso. O jogador escolhe
o arco. Dois segundos de recuperação.

O primário também funciona no ar — o bote é uma escolha, não a única
opção de quem pulou.

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

O passo sai por distância percorrida, não por tempo: a cadência
acompanha a velocidade sozinha e andar devagar faz menos barulho.

O áudio acompanha o evento em vez de substituí-lo. O que o bot ouve
e o que o jogador ouve precisam ser a mesma coisa, senão o jogador não
consegue prever a reação.

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
  raio do ruído. Não existe som de mundo que não seja ruído: é assim que
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

Som novo que for informação de jogo **entra como `NoiseKind`**, com
raio, e não como som solto — senão o jogador passa a ouvir algo que o
bot não ouve.

### Como o bot pensa

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
- **Uivo de verdade.** O `lobo_uivo` atual é sintetizado, provisório.
  Os outros sons já são gravações (`assets/audio/CREDITOS.md`).
- **Tela de créditos**, antes de qualquer publicação: o Oculus Audio
  Pack é CC BY 4.0.
- Rodar os quatro testes de validação do `docs/ARSENAL.md` (duelos,
  lobo no corredor e em campo aberto, ritmo da economia).
- Responder "A pergunta que decide o projeto", no fim deste guia.

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

Mapa próprio no espírito do Arrival. Caixa cinza, nenhuma peça vinda de
outro jogo — é o primeiro mapa que pode ser publicado.

Gerado por `tools/gerar_hangar.py`, que guarda cada medida com o motivo
dela. **Rodar o script de novo sobrescreve o `.tscn`**: se o mapa for
editado no Godot, o `.tscn` passa a ser a fonte e o script vira só o
registro de como ele nasceu.

| Medida | Valor |
|---|---|
| Arena | 84 × 48 m, muro de 12 m |
| Bases | galpões de 10 × 21 m com teto a 5 m, A (azul) a oeste, B (vermelha) a leste |
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
(2,2 m). O Bruto pula 1,54 m: do caixote alcança o contêiner. Ninguém
alcança 5 m sem parede. **Mexeu no pulo, confira estes números** — um
caixote 2 cm alto demais vira parede para o humano sem ninguém notar.

### Textura de grade

`assets/textures/grid.png`, projetada no espaço do mundo: um ladrilho a
cada 2 m, subdivisão a cada 1 m. Em caixa cinza é o que dá escala e
velocidade — parede lisa não mostra o quanto você andou.

---

## A partida

`scripts/core/Match.cs`. Mata-mata em equipe, Azul contra Vermelho:
ganha quem fizer **40** abates primeiro; se os **10 minutos** acabarem
antes, ganha quem tiver mais, e empate é empate.

| Fase | O que acontece |
|---|---|
| Contagem (10 s) | Todos na base. Olha-se em volta, não se anda nem se atira. É também o tempo da tela de lote |
| Valendo | Relógio corre, abates pontuam |
| Fim | Tudo congela, tela de vitória ou derrota, Enter recomeça |

A contagem só começa quando o menu de dificuldade fecha, porque o menu
pausa a árvore e a partida é pausável. Abrir o menu no meio da partida
pausa o relógio junto.

**A partida não sabe quem atirou em quem.** Ela escuta o `KillFeed`, o
mesmo barramento do feed na tela. Quem mata não precisa saber que
existe placar. Só abate de inimigo pontua; queda e granada nos próprios
pés contam só como morte.

`Match.IsLive` é o que Player e Bot consultam para saber se podem se
mexer. **Sem `Match` na cena ele responde verdadeiro** — é o que mantém
o treino e a referência do Arrival funcionando como antes.

Reiniciar zera o `UiState` antes de recarregar: o contador de menus é
estático e sobrevive à troca de cena, e um menu aberto no fim faria a
tela de lote da partida nova nunca contar o tempo.

### O HUD da partida

`scripts/ui/MatchHud.cs`, separado do `Hud` de propósito: o `Hud` fala
do corpo, este fala da partida, e some inteiro numa cena sem partida.

Placar e relógio no topo, com uma barra por time até os 40 — porque
"27" não diz o quanto falta e um traço a dois terços do caminho diz. O
relógio fica vermelho no último minuto. Tab mostra a tabela por
jogador, com você em amarelo.

---

## Times

`Health.Team` diz o lado. Os bots recebem time e nome pela raiz
(`Bot.Team`, `Bot.Callsign`) e aplicam no `_EnterTree`, não no
`_Ready`: os filhos fazem `_Ready` antes do pai, e o corpo e a barra de
vida leem o time no `_Ready` deles para escolher a cor.

| Quem | Como sabe o lado |
|---|---|
| Corpo | Pintado na cor do time; juntas num tom escuro dela |
| Barra de vida | Aliado na cor do time, inimigo em vermelho |
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
menu.tscn ── Criar sala ──> sala ── Iniciar ──> main.tscn ── Esc ──> menu.tscn
            Conjuntos (editor)        (RoomConfig)
            Inventário
```

**Sala.** Times Azul e Vermelho, até 8 em cada. O mestre (quem criou —
hoje, você) adiciona e remove bots, escolhe pontos, tempo e
dificuldade. Cada jogador aperta **Pronto**; o mestre só inicia com
todos prontos e os dois times com alguém. Bots estão sempre prontos.

A regra de "pronto" existe hoje com um humano só, de propósito: na
Fase 2 muda de onde vêm os jogadores, não como a sala funciona.

`RoomConfig` (estático) leva a sala para a partida, porque sala e
partida são cenas diferentes. Com `Active` falso — partida aberta
direto pelo editor — o `MatchSetup` monta a escalação padrão.

Na partida, o `MatchSetup` distribui cada time pelos spawns da sua
base em ordem, ninguém em cima de ninguém. O time do jogador é
aplicado no `_EnterTree` do Player, antes de corpo e barra de vida
escolherem cor.

**Esc não pausa.** É jogo multijogador: não existe pausa com outras
pessoas na sala, e treinar com a regra certa evita o hábito de abrir
o menu para fugir de uma luta. Com o menu aberto o corpo fica parado
(o mouse é do menu), mas continua tomando tiro.

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
- Paga-se no renascer. O equipamento some na morte; o WP fica.

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

O inventário hoje tem todo o arsenal. Quando houver loja, a compra
entra no `Profile.Inventory`; o editor já só aceita item do inventário.

---

## HUD de combate

**Dano direcional** (`scripts/ui/DamageIndicator.cs`): arco vermelho em
volta da mira apontando para quem atirou. Guarda a posição de origem
no mundo, não o ângulo do momento — é o que faz o arco girar quando
você vira, continuando a apontar para o atirador. A borda vermelha da
tela inteira ficou fraca: ela diz "levou dano", o arco diz "de onde".

**Minimapa** desenha toda caixa do mapa com a cor do material dela, as
mais altas por cima (telhado cobre o que está embaixo) e mais claras
quando bem acima de você. Letras curtas do mapa (o "A" e o "B" das
bases) viram marcas. Antes ele procurava um nó "Walls" que só existia
na caixa cinza antiga, e no Hangar não desenhava nada.

---

## Morte

O corpo cai com a colisão **ligada** até tocar o chão, e só então a
colisão desliga e o corpo **para de se mover de vez** — sem gravidade.
A segunda metade foi o defeito: sem colisão, `IsOnFloor()` responde
falso no quadro seguinte, porque não existe chão para quem não colide,
e a gravidade voltava a puxar o corpo através do mapa.

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
