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

| Estado | Hitbox | Ataca | Dano recebido |
|---|---|---|---|
| Humano | humana | arma | normal |
| VirandoLobo | humana | não | ×1,5 |
| Lobo | lupina | corpo-a-corpo | normal |
| VirandoHumano | lupina | não | ×1,5 |

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

Input é registrado por código em `InputSetup.cs`. **Não peça ao
usuário para configurar o Input Map no editor** — já foi feito uma vez
e custou tempo. Ações definidas no editor têm prioridade e não são
sobrescritas.

---

## Corrida em parede

Só na forma lobo, só no ar, só segurando W.

- Sem limite de tempo: corre enquanto a parede existir
- Solta ao largar o W, ao bater em obstáculo, ou se atordoado
- **Altura travada**: não sobe nem desce enquanto corre
- **Direção travada**: escolhida uma vez, ao grudar, pelo lado para
  onde o jogador olhava. Acompanha a curva da parede, mas nunca se
  inverte. Para ir ao outro lado é preciso cair e recomeçar
- O olhar fica livre durante a corrida: mirar não desvia o movimento
- A/D não controlam durante a corrida
- Space salta para fora e para cima

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
