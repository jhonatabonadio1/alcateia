# 0020 — Teste novo se propõe antes; não se cria teste para cada mudança

**Situação:** aceita (27/11/2026, pedido do Jhon). Muda a regra de
criação de teste da [0001](0001-dominio-puro.md) e da
[0007](0007-testes.md); o resto delas continua valendo.

## Contexto

Em duas semanas a bancada passou de uns 20 para 104 testes de
regressão (um processo do Godot cada), 12 arquivos de unidade e 11 do
servidor. Boa parte nasceu porque a regra dizia "conta nova vai com
teste" e "conserto vai com teste": cada mudança ganhava o seu, mesmo
quando era um ajuste de número, um conserto visual ou algo que se vê na
primeira partida.

O custo aparece em três lugares: o `testar.sh` fica mais lento a cada
semana; um ajuste de calibragem deixa três ou quatro testes vermelhos
que prendiam o mesmo número; e fica difícil saber quais testes guardam
algo que importa.

## Decisão

**Ninguém cria teste novo sem propor antes e o Jhon aceitar.** Vale para
pessoa e para agente, nas três bancadas (`src/tests/unit`,
`src/tests/regression`, `servidor/Alcateia.Servidor.Testes`).

O que **não** precisa de proposta:

- **Ajustar um teste que já existe** porque o comportamento mudou de
  propósito (a regra da 0007: no mesmo commit, dizendo no teste por
  quê). Isso não é criar teste.
- Rodar os testes. Continua obrigatório antes de todo commit.

### Como propor

No fim do trabalho, numa linha por teste, sem escrever o código dele:

> **Teste sugerido:** `NomeDoTeste` (unidade | regressão | servidor) —
> o que ele prova, em uma frase — por que vale: o que quebraria em
> silêncio sem ele — custo: segundos a mais na bancada, ou "cresce o
> `TesteTal` existente".

Se o Jhon disser sim, escreve-se. Se não disser nada, não se escreve.

### Quando vale propor

- Uma **conta de regra** nova em `src/scripts/domain` com caso de borda
  que não se vê jogando (dano no limite da queda, divisão por zero,
  arredondamento de WP, tempo que vira o quadro).
- Um **número de sensação aprovado** pelo Jhon que outra mudança pode
  estragar sem ninguém ver (velocidade, pulo, passada, alcance do
  salto).
- Um **defeito que voltou**: relatado, consertado e relatado de novo.
- **Rede, servidor e segurança**: protocolo, quem decide o quê,
  dinheiro e item da conta, trapaça. Aí o erro não aparece jogando
  sozinho.
- **Antes de refatorar** uma peça grande que não tem cobertura (a
  corrida na parede ganhou `SaltoDaParede` e `AgarraNaParede` antes de
  ser dividida).

### Quando não vale (não proponha)

- Mudança de número de calibragem: atualize o teste que já prende o
  número, se houver; não crie outro.
- Texto, layout, cor, ícone, tela do lobby ou do painel.
- Troca de arte, som, modelo, mapa (a cena sem erro já é coberta pelos
  testes de cena).
- Peça marcada como descartável, ferramenta de depuração.
- O que se vê na primeira partida e não volta sozinho.
- Teste que só repete a implementação (conferir que a constante é a
  constante).
- Um teste por conserto: se um teste existente cobre o assunto, a
  proposta é **crescer ele** com mais uma conferência, não um arquivo
  novo.

### Tirar teste

Teste que nunca reprovou e repete outro também se propõe para sair, do
mesmo jeito. Não se apaga teste sem o Jhon aceitar.

## Consequências

- A bancada para de crescer a cada commit; cresce quando alguém decide
  que um risco merece guarda.
- O código de regra continua indo para `src/scripts/domain` (a 0001 não
  muda nisso): ele fica testável, mesmo que o teste não seja escrito
  agora.
- O risco é um defeito escapar onde ninguém propôs teste. A resposta é
  a regra "defeito que voltou": na segunda vez, propõe-se o teste.
