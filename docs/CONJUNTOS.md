# Conjuntos de Armas (WP Sets) — Especificação

Sistema de montagem e seleção de equipamento. Complementa `ARSENAL.md`,
que define os itens e seus custos.

> **Nome na tela:** o WP aparece no jogo como **KP** (kill points),
> desde 24/11. No código continua `Wp`.

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
| Primária | fuzil, arma pesada, sniper | sim |
| Secundária | pistola | sim |
| Corpo a corpo | a faca ou uma versão dela | sim (a comum é de todo mundo) |
| Utilitário 1 | a granada (qualquer versão) | não |
| Utilitário 2 | granada de luz ou fumaça apenas | não |

O segundo utilitário existe na regra (`UtilityInfo.AllowedInSecondSlot`),
mas nenhuma granada de hoje cabe nele: na prática, uma granada por
conjunto.

O custo é a **soma simples** dos itens. Sem descontos, sem bônus de
combinação — o jogador precisa conseguir somar de cabeça olhando a tela.

### Exemplo

```
Conjunto "Longa distância"
  Primária      SVD Dragunov         600
  Secundária    Desert Eagle           0
  Corpo a corpo Faca                   —
  Utilitário 1  Granada               60
  ─────────────────────────────────────
  Total                              660 WP
```

---

## 3. Os quatro slots

O jogador possui **4 conjuntos salvos**.

### Conjunto 1 — Padrão
- Custo fixo **0**
- Aceita **apenas armas comuns** (a M4A4, a Desert Eagle e a faca
  comum) e qualquer versão da granada (`LoadoutSet.AllowedInDefault`;
  Jhon, 19/11: "somente arma comum pode")
- Não pode receber arma incomum ou melhor, mesmo que o jogador a possua
  (nem o AR-15)
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
  [2] Assalto               440                ✓
  [3] Pesado                510                ✗  faltam 30
  [4] Longa distância       660                ✗  faltam 180

  Lobo: grátis, a qualquer hora — tecla 3
```

### Regras

1. Conjuntos inacessíveis aparecem **acinzentados mas visíveis**, exibindo
   quanto falta. Ver o que não se pode pagar é o que cria desejo e dá sentido
   ao próximo abate.
2. A seleção debita o WP **no momento do respawn**, não antes — e só o que
   ainda não foi pago nesta partida (seção 5).
3. O lobo não está na lista: transformar é um slot da partida (tecla 3)
   e as classes básicas não custam WP. A tela só avisa a regra do modo
   (lobo livre, bloqueado ou obrigatório).
4. **Tempo de 15 s** (`LoadoutMenu.ChooseTime`; Jhon, 18/11): não
   escolheu, entra o Padrão.
5. O conjunto selecionado fica destacado até o próximo respawn.

---

## 5. Regra econômica central

> **Morrer não tira KP. A arma paga fica desbloqueada até o fim da partida.**
> (Jhon, 26/11: "ao morrer, não pode perder KP. E devem ganhar um pouco mais
> por partida.")

Comprou uma SVD de 600 KP e morreu em dez segundos? Renasce com ela de novo
sem pagar: o que foi pago numa partida vale até o fim dela
(`MatchKp`, `src/scripts/domain/MatchKp.cs`). Um conjunto custa só a soma
dos itens que ainda não foram pagos nesta partida — trocar para outro paga
só a arma nova, e voltar ao anterior é de graça. O cartão do conjunto mostra
"JÁ PAGO" ou o que falta ("380 de 440").

Até 25/11 era o contrário: o equipamento sumia na morte (o conjunto era
cobrado de novo a cada renascer) e a morte tirava 100 (`WpLostOnDeath`, que
saiu). Quem desbloqueava uma arma e morria logo ficava sem KP para renascer
com ela — o relato do alpha fechado. O KP agora é progresso dentro da
partida: juntar abates libera armas melhores para o resto dela.

**Online (partida autoritativa)** o servidor de partida confere o gasto
(`HostedMatch.PaidWeapon`): a arma paga que chega no estado do jogador só
vale se o KP que ele pode ter ganhado, pelo placar do servidor
(`MatchKp.MostEarned`: abates, assistências e o bônus do padrão em cada
entrada), cobre o preço; a que não cabe vira a padrão e conta contra a conta,
como a arma que não é dela. A arma do conjunto padrão é de graça. No modo de
repasse cada jogo decide o próprio corpo (decisão 0008) e o KP não é
conferido.

### Ganhos

| Ação | WP |
|---|---|
| Selecionar o conjunto Padrão | +50 |
| Abate | +125 (era 100; +25% em 26/11) |
| Abate em forma de lobo | +88 (era 70) |
| Assistência | +44 (era 35) |
| Morrer | 0 (era −100) |
| **Teto acumulado** | **10000** (era 2500; Jhon, 21/11) |

O WP persiste durante a sessão e é zerado a cada partida, com as armas
desbloqueadas nela.

Uma partida típica (10 abates de arma, 4 assistências, 8 mortes renascendo
no padrão) rendia 740 KP; agora rende 1826.

### Custo das classes de lobo

| Classe | WP |
|---|---|
| Bruto | 0 |
| Ligeiro | 0 |

Decisão do autor (nov/2026): as classes básicas custam 0 WP. As
mutações que vierem (Couraçado, Espectro...) é que devem custar
(`WolfBreed.WpCost`).

---

## 6. Validação no editor

Verificações antes de salvar:

1. Primária preenchida
2. Secundária preenchida
3. Sem itens duplicados no mesmo conjunto
4. Todos os itens presentes no inventário do jogador
5. Máximo de 2 utilitários; o segundo, só luz ou fumaça
6. Conjunto 1 aceita apenas armas comuns e as granadas liberadas
7. **O custo total NÃO é limitado**

A regra 7 é intencional e costuma ser a primeira que alguém tenta
"consertar". O jogador pode salvar um conjunto de 2000 WP que raramente vai
conseguir pagar. Isso é uma meta, não um erro. Não conserte.

---

## 7. Conjuntos de exemplo

Sugestões pré-montadas para jogadores novos, exibidas como ponto de partida.

### Assalto — 440 WP
```
AK-74            380
Desert Eagle       0
Granada           60
──────────────────────
Total            440
```
Entrada direta. O melhor TTK do jogo com secundária gratuita.

### Pesado — 510 WP
```
SCAR-H           450
Desert Eagle       0
Granada           60
──────────────────────
Total            510
```
Segura o corredor de longe; de perto, a pistola resolve.

### Longa distância — 660 WP
```
SVD Dragunov     600
Desert Eagle       0
Granada           60
──────────────────────
Total            660
```
A combinação sniper + resposta de perto. Cara, mas legítima.

---

## 8. Modelo de dados

`src/scripts/combat/LoadoutSet.cs` (resumido). Os itens são ids do
catálogo (`Arsenal`), não recursos:

```csharp
public sealed class LoadoutSet
{
	public const int MaxNameLength = 16;
	public const int MaxUtilities = 2;

	public string Name { get; set; } = "Novo conjunto";
	public bool IsDefault { get; set; }
	public string Primary { get; set; } = Arsenal.DefaultPrimary;     // m4a4
	public string Secondary { get; set; } = Arsenal.DefaultSecondary; // deagle
	public string Melee { get; set; } = Arsenal.KnifeId;              // faca
	public List<string> Utilities { get; set; } = new();

	public int TotalCost()
	{
		if (IsDefault) return 0;

		int cost = Arsenal.CostOf(Primary) + Arsenal.CostOf(Secondary);
		foreach (string id in Utilities)
			cost += Arsenal.CostOf(id);
		return cost;
	}

	public bool IsAffordable(float wp) => TotalCost() <= wp;

	// Primária e secundária obrigatórias, até 2 utilitários (o segundo só
	// luz ou fumaça), sem repetidos, o padrão só com itens comuns
	// (AllowedInDefault) e tudo no inventário (Profile.Owns).
	public bool Validate(out string error) { ... }
}
```

O perfil do jogador guarda apenas os 4 conjuntos (`Profile.Sets`,
`Profile.SetCount`) e o inventário.

**O custo nunca é persistido.** É sempre recalculado a partir das definições,
então um ajuste de balanceamento se propaga sozinho para todos os conjuntos
de todos os jogadores.

---

## 9. Decisões de design

### Não proibir combinações
Vários jogos proíbem combinações por medo de desequilíbrio. Aqui seria
um erro: o custo somado já é a limitação natural. A sniper com a granada
(660 WP) — matar de longe, se defender de perto — é uma resposta legítima
ao lobo que alguém vai descobrir sozinho e achar genial.

**Deixe a economia balancear em vez de uma regra arbitrária.** Se um conjunto
virar dominante, aumente o custo do item; não proíba a combinação.

### Controlar a bola de neve
O sistema recompensa quem está ganhando com equipamento melhor, o que acelera
a vantagem. Duas travas baratas:

1. **Bônus de desvantagem** — o time que está perdendo ganha WP um pouco mais
   rápido. Invisível ao jogador, mantém a partida viva.
2. **Nenhum item pago com dinheiro real (IPs, o antigo Sangue) pode custar menos WP** por desempenho
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