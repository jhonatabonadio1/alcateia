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

| Arquivo | Responsabilidade |
|---|---|
| `Player.cs` | Movimento, corrida em parede, câmeras, stun, leitura de input |
| `Health.cs` | HP, escudo, dano, morte, respawn |
| `FormController.cs` | Máquina de estados da transformação + economia de WP |
| `Weapon.cs` | Slots, tiro hitscan, garrada, granada |
| `Grenade.cs` | Projétil de atordoamento com física |
| `WolfBreed.cs` | Resource com estatísticas por classe |
| `Hud.cs` | Barras e avisos, construído por código |
| `InputSetup.cs` | Registra ações de input sem passar pelo editor |
| `Dummy.cs` | Boneco de teste — descartável |
| `GameManager.cs` | Rede — dormente até a Fase 2 |

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
| 4 | Arremessar granada |
| Clique esq. | Atacar |
| F | Uivar (só de lobo) |
| Ctrl | Agachar (nas duas formas) |

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
- **Uma corrida por ida ao ar**: depois de grudar uma vez, é preciso
  tocar o chão antes de grudar de novo. Sem isso o lobo regruda na
  mesma altura após o salto e a parede vira elevador
- `RequireFloorBetweenWallRuns` desligado troca a exigência de chão
  por um tempo mínimo (`WallRunCooldown`), o que permite encadear
  saltos entre paredes opostas. Decisão de design em aberto
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
