# Conjuntos de Armas (WP Sets) — Especificação

Sistema de montagem e seleção de equipamento. Complementa `WEAPONS.md`,
que define os itens e seus custos.

---

## 1. Conceito

A unidade de compra é o **conjunto**, não a arma individual. O jogador monta
conjuntos fora da partida e os seleciona ao renascer, pagando o custo total
com WP acumulado.

Duas telas, estritamente separadas:

| | Onde | O que faz |
|---|---|---|
| **Editor de conjuntos** | fora da partida | montar, nomear e salvar |
| **Tela de respawn** | dentro da partida | apenas selecionar e pagar |

**Não é permitido editar conjuntos durante a partida.** Se o jogador puder
montar em tempo real, a decisão perde custo e vira otimização sem fim.

---

## 2. Estrutura de um conjunto

| Espaço | Aceita | Obrigatório |
|---|---|---|
| Primária | fuzil, SMG, escopeta, sniper | sim |
| Secundária | pistola, revólver | sim |
| Utilitário 1 | qualquer granada | não |
| Utilitário 2 | granada de luz ou fumaça apenas | não |

O custo é a **soma simples** dos itens. Sem descontos, sem bônus de
combinação — o jogador precisa conseguir somar de cabeça olhando a tela.

### Exemplo

```
Conjunto "Caçador"
  Primária      M3                   350
  Secundária    M500                 180
  Utilitário 1  Granada de choque     60
  Utilitário 2  Granada de luz        30
  ─────────────────────────────────────
  Total                              620 WP
```

---

## 3. Os quatro slots

O jogador possui **4 conjuntos salvos**.

### Conjunto 1 — Padrão
- Custo fixo **0**
- Aceita **apenas itens de custo 0** (M16A2, M9)
- Não pode receber arma paga, mesmo que o jogador a possua
- Sempre disponível, em qualquer situação de WP
- **Selecioná-lo concede +50 WP**

É a rede de segurança. Não é só gratuito: ele *paga*. Quem está sem pontos
tem caminho de recuperação, e a partida não trava para quem está perdendo.

### Conjuntos 2, 3 e 4 — Livres
- Qualquer item do inventário do jogador
- Custo total sem teto
- Nomeáveis pelo jogador (máx. 16 caracteres)

---

## 4. Tela de respawn

```
  WP disponível: 480

  [1] Padrão             GRÁTIS  (+50 WP)      ✓
  [2] Assalto               380                ✓
  [3] Caçador               620                ✗  faltam 140
  [4] Longa distância       680                ✗  faltam 200

  [L] Lobo — Battle Wolf    225                ✓
```

### Regras

1. Conjuntos inacessíveis aparecem **acinzentados mas visíveis**, exibindo
   quanto falta. Ver o que não se pode pagar é o que cria desejo e dá sentido
   ao próximo abate.
2. A seleção debita o WP **no momento do respawn**, não antes.
3. O lobo é uma opção na mesma lista, com custo por forma.
4. **Timeout de 5s:** entra o último conjunto usado; se não for pagável,
   entra o Padrão.
5. O conjunto selecionado fica destacado até o próximo respawn.

---

## 5. Regra econômica central

> **O equipamento comprado some na morte. O WP acumulado permanece.**

Comprou um AWM de 850 WP e morreu em dez segundos? Perdeu os 850 e precisa
juntar de novo. Mas o WP ganho com abates continua.

Isso torna cada morte uma perda econômica além da tática, e faz o jogador
pensar duas vezes antes de levar a arma cara para uma posição exposta —
exatamente a tensão desejada num jogo onde o inimigo escala paredes.

### Ganhos

| Ação | WP |
|---|---|
| Selecionar conjunto Padrão ou Lobo | +50 |
| Abate | +100 |
| Abate em forma de lobo | +70 |
| Assistência | +35 |
| Objetivo (plantar, desarmar, capturar) | +75 |
| **Teto acumulado** | **2500** |

O WP persiste durante a sessão e é zerado a cada partida.

### Custo das formas de lobo

| Forma | WP |
|---|---|
| Baby Wolf | 200 |
| Battle Wolf | 225 |
| Elite Wolf | 250 |
| Blood Wolf | 275 |
| Monster Wolf | 300 |
| Death Wolf | 325 |
| Master Wolf | 350 |
| Perfect Wolf | 375 |

---

## 6. Validação no editor

Verificações antes de salvar:

1. Primária preenchida
2. Secundária preenchida
3. Sem itens duplicados no mesmo conjunto
4. Todos os itens presentes no inventário do jogador
5. Máximo de 2 utilitários
6. Conjunto 1 aceita apenas itens de custo 0
7. **O custo total NÃO é limitado**

A regra 7 é intencional e costuma ser a primeira que alguém tenta
"consertar". O jogador pode salvar um conjunto de 2000 WP que raramente vai
conseguir pagar. Isso é uma meta, não um erro. Não conserte.

---

## 7. Conjuntos de exemplo

Sugestões pré-montadas para jogadores novos, exibidas como ponto de partida.

### Assalto — 380 WP
```
AK-47            380
M9                 0
──────────────────────
Total            380
```
Entrada direta. O melhor TTK do jogo com secundária gratuita.

### Caçador — 620 WP
```
M3               350
M500             180
Granada choque    60
Granada luz       30
──────────────────────
Total            620
```
Anti-lobo dedicado. Dois stuns e morte com um tiro de perto.

### Controle — 530 WP
```
SG-552           320
Desert Eagle     150
Granada fumaça    30
Granada luz       30
──────────────────────
Total            530
```
Distância média, recoil decorável, utilitários para negar espaço.

### Longa distância — 680 WP
```
Scout            450
M500             180
Granada choque     60
──────────────────────
Total            680
```
A combinação sniper + resposta de perto. Cara, mas legítima.

### Móvel — 350 WP
```
UMP-45           280
M9                 0
Granada choque    60
Granada luz       30
──────────────────────
Total            370
```
Saque rápido e velocidade alta. Para quem joga em movimento.

---

## 8. Modelo de dados

```csharp
[GlobalClass]
public partial class LoadoutSet : Resource
{
    [Export] public string Name = "Novo conjunto";
    [Export] public bool IsDefault = false;

    [Export] public WeaponDefinition Primary;
    [Export] public WeaponDefinition Secondary;
    [Export] public Godot.Collections.Array<UtilityDefinition> Utilities = new();

    public int TotalCost()
    {
        if (IsDefault) return 0;

        int cost = 0;
        if (Primary   != null) cost += Primary.WpCost;
        if (Secondary != null) cost += Secondary.WpCost;
        foreach (var u in Utilities)
            if (u != null) cost += u.WpCost;
        return cost;
    }

    public bool IsAffordable(int currentWp) => TotalCost() <= currentWp;

    public bool Validate(PlayerInventory inv, out string error)
    {
        if (Primary == null)
            { error = "Primária obrigatória"; return false; }
        if (Secondary == null)
            { error = "Secundária obrigatória"; return false; }
        if (Utilities.Count > 2)
            { error = "Máximo 2 utilitários"; return false; }
        if (IsDefault && TotalCost() > 0)
            { error = "O conjunto padrão só aceita itens gratuitos"; return false; }
        if (!inv.OwnsAll(this))
            { error = "Item não possuído"; return false; }

        error = null;
        return true;
    }
}
```

O perfil do jogador guarda apenas `LoadoutSet[4]` e o inventário.

**O custo nunca é persistido.** É sempre recalculado a partir das definições,
então um ajuste de balanceamento se propaga sozinho para todos os conjuntos
de todos os jogadores.

---

## 9. Decisões de design

### Permitir sniper + escopeta
Vários jogos proíbem combinações assim por medo de desequilíbrio. Aqui seria
um erro: o custo somado (450 + 350 = 800 WP) já é a limitação natural, e esse
conjunto específico — matar de longe, se defender de perto — é uma resposta
legítima ao lobo que alguém vai descobrir sozinho e achar genial.

**Deixe a economia balancear em vez de uma regra arbitrária.** Se um conjunto
virar dominante, aumente o custo do item; não proíba a combinação.

### Controlar a bola de neve
O sistema recompensa quem está ganhando com equipamento melhor, o que acelera
a vantagem. Duas travas baratas:

1. **Bônus de desvantagem** — o time que está perdendo ganha WP um pouco mais
   rápido. Invisível ao jogador, mantém a partida viva.
2. **Nenhum item pago com dinheiro real pode custar menos WP** por desempenho
   igual. No momento em que o custo vira vantagem comprável, toda a economia
   deixa de significar algo.

---

## 10. Testes de validação

1. Um jogador mediano acessa um conjunto de ~350 WP por volta do terço
   inicial da partida — não antes.
2. Um jogador que morre repetidamente consegue voltar ao jogo pelo conjunto
   Padrão sem espiral de derrota.
3. Nenhum conjunto deve ser escolhido em mais de 40% dos respawns no
   agregado. Se um passar disso, o custo dele está baixo.
4. O conjunto Padrão deve permanecer viável o suficiente para vencer duelos
   ocasionais. Se nunca vencer, o arsenal pago está forte demais.