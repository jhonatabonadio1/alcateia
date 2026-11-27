# Como trabalhar no Alcateia

O passo a passo para mexer no jogo sem quebrar o que já foi aprovado.
As regras de design estão no `CLAUDE.md`; o mapa do código, em
`docs/ARQUITETURA.md`.

## Preparar

1. **Godot 4.7.2 .NET** (a versão "mono"/".NET", não a comum) e o
   **.NET SDK 8**.
2. Abra o `project.godot` uma vez no editor: ele importa os modelos e
   gera os `.uid`/`.import` que faltarem. **Commite os `.uid` e os
   `.import` novos** — o Godot precisa deles para achar os arquivos.
3. Rode os testes (abaixo). Se passarem, está pronto.

## Testes

```sh
src/tools/testar.sh            # tudo (~1,5 min)
src/tools/testar.sh --rapido   # sem os dois testes de partida (~50 s)
src/tools/testar.sh Salto      # só os testes com "Salto" no nome
```

Sem o Godot no PATH: `GODOT=/caminho/do/Godot src/tools/testar.sh`. No Mac
o executável fica em `Godot_mono.app/Contents/MacOS/Godot`.

O GitHub roda o mesmo script a cada push. Commit com teste vermelho não
sobe. O script também procura **código repetido** (`src/tools/duplicados.py`):
seis linhas iguais em dois lugares reprovam — junte numa peça só.

## Antes de mudar algo

- **Leia a seção do sistema no `docs/DESIGN.md`.** Quase tudo ali tem
  um porquê que custou caro descobrir.
- **Vai mudar um comportamento de propósito?** Mude o número esperado
  no teste no mesmo commit, e diga por quê.
- **Teste novo se propõe antes** (`docs/decisoes/0020`): não crie teste
  para cada mudança. Ache que um vale a pena? Descreva numa linha (nome,
  camada, o que prova, por que vale, custo) e espere o sim do Jhon. Vai
  refatorar uma peça grande sem cobertura? É um bom caso para propor o
  teste antes de começar.

## Onde cada coisa entra

| Você vai… | Onde |
|---|---|
| Mudar um número (velocidade, dano, tempo) | o `.tres` ou o `[Export]` dele — o `docs/AJUSTES.md` diz qual — e a linha no guia |
| Escrever uma conta nova (dano, chance, alcance) | `src/scripts/domain/`, em C# puro, com teste em `src/tests/unit/` |
| Sortear algo que muda o jogo | `GameRandom` (`MatchRandom.Stream(nome)`), nunca `GD.Randf()` |
| Ler uma tecla nova | `CharacterIntent` (e `InputSetup`), nunca `Input` direto no `Player` |
| Algo que jogador e bot fazem igual | `src/scripts/actors/body/` |
| Um sistema novo num ator grande | uma parte nova da classe parcial (`Player.Coisa.cs`) com o seu `[ExportGroup]` |
| Algo novo no HUD | um sinal no componente e um handler na parte do canto (`ui/hud/`) |
| Um nível de bot, uma arma, uma classe | um `.tres` novo em `src/resources/` |
| Uma peça de tela que se repete (cartão, grade, linha rótulo/valor) | `UiKit` |
| Achar o jogador, os bots, um nó na árvore | `Groups` e `Nodes` (`src/scripts/core`), nunca o nome do grupo solto |
| Uma decisão de arquitetura | um ADR novo em `docs/decisoes/` |

## Estilo

- Comentários em português, dizendo **por quê** — o quê o código já diz.
- Tab para indentar (o `.editorconfig` cuida).
- Nomes de classe e método em inglês; texto de tela e comentários em
  português.
- Nada de nome, arte ou marca do WolfTeam (ver "Propriedade
  intelectual" no `CLAUDE.md`).

## Commit

- Um assunto por commit, com a mensagem dizendo o que mudou e por quê.
- Atualize o `docs/CHANGELOG.md` quando a mudança aparece para quem
  joga, e o `CLAUDE.md`/`DESIGN.md` quando muda uma regra.
- Rode `src/tools/testar.sh` antes.
