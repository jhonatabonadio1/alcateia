# Mapas

Dois mapas, os dois de equipe, registrados em `scripts/meta/Maps.cs`:

| Mapa | Tamanho | Tema | Gerador |
|---|---|---|---|
| Vila | 72 × 52 m (médio) | Vila de deserto no fim da tarde: praça, feira, terraços, vielas | `tools/mapas/vila.py` |
| Estação Polar | 110 × 70 m (grande) | Base de pesquisa na neve, ao crepúsculo: garagem, alojamentos, passarelas | `tools/mapas/polar.py` |

O Hangar, o Metrô e a Rua Abandonada saíram do jogo em 16/11 (a pedido).
O Metrô tinha um evento de som (`EventSound`, o trem passando): o campo
continua em `MapInfo` para o próximo mapa que quiser um.

A sala escolhe o mapa; a cena da partida (`scenes/main.tscn`) tem um nó
`Map` com o script `MapSlot`, que carrega a cena do mapa escolhido antes
de os jogadores nascerem. Para abrir um mapa direto no editor, ponha o
id dele em **Map Id** no nó `Map`.

## Como um mapa é feito

Por script, não à mão no editor:

```sh
pip install numpy pillow          # uma vez
python3 tools/mapas/texturas.py   # as texturas (assets/textures/pbr), dos pacotes
python3 tools/mapas/letreiros.py  # o que está escrito nos mapas (assets/mapas/letreiros)
python3 tools/gerar_mapas.py      # todos os mapas; ou: gerar_mapas.py vila
```

Cada mapa é um arquivo em `tools/mapas/` que usa o kit
(`tools/mapas/kit.py`): `caixa`, `cilindro`, `rampa`, `parapeito`, `luz`,
`parede`, `letreiro`, `modelo`, `spawn`, `rota`, mais o céu, a neblina e o sol do mapa. O
gerador escreve `scenes/maps/<id>.tscn` e a miniatura
`assets/ui/mapas/<id>.png`.

**Rodar o gerador sobrescreve o `.tscn`.** Se você editar um mapa no
Godot, o `.tscn` passa a ser a fonte.

### Jogo embaixo, acabamento em cima

Cada mapa tem duas camadas, no mesmo arquivo:

1. **`montar()` — o jogo.** Caixas, rampas e cilindros com colisão: o
   que cobre, o que se sobe, onde o lobo ricocheteia, onde se nasce. É o
   que os testes medem e o que o bot enxerga. Mexeu aqui, mudou o mapa.
2. **`acabamento()` — o lugar.** O que faz a caixa virar estação, vila
   ou base: abóbada, colunas, janelas, portas em arco, telhados com
   neve, canos, corrimãos, trator, torre treliçada, palmeiras, serra ao
   fundo. Quase tudo **sem colisão**; mexer aqui não muda o jogo.

Quando a forma que se vê é mais rica que a caixa (o barril torneado, o
vagão de teto curvo, a escada de degraus sobre a rampa), a caixa fica
com `oculta=True`: continua sendo a colisão, só não é desenhada. O que
se vê não pode fugir muito do que colide — tiro e corpo batem na caixa.

As formas que não são caixa saem de `tools/mapas/formas.py` (extrusão
de perfil, revolução, cano por um caminho, relevo, pedra, caixa
chanfrada, abóbada, arco, escada, rosca) e viram arquivos `.obj` em
`assets/mapas/<id>/` — um por forma diferente; vinte barris iguais são
um arquivo só. As peças prontas estão em `tools/mapas/pecas.py`:
janela, porta, beiral, coluna, tubulação com flange, cabo, corrimão,
barril, caixote ripado, barreira de concreto, sacos de areia, pedra,
pneus, palete, poste de luz, arandela, toldo.

Depois de gerar, abra o projeto no Godot (ou rode
`godot --headless --import`) para ele importar os `.obj` novos, e
versione os `.obj` e os `.obj.import` junto com a cena.

### As texturas

São fotografia de material de verdade: 44 conjuntos do
[Poly Haven](https://polyhaven.com) (CC0), copiados de `assets/pacotes/`
para `assets/textures/pbr/` por `tools/mapas/texturas.py`, com o nome
que os mapas usam. Cada material são três arquivos de 1024 px:
`<nome>_cor.jpg`, `<nome>_normal.jpg` e `<nome>_arm.jpg` (oclusão em R,
aspereza em G, metal em B).

A tabela no começo do script diz qual foto vira qual material e se ela
entra **neutra**: sem a cor da foto, só com o claro-escuro (a junta, a
sujeira, o risco). É neutro tudo o que o mapa pinta — o reboco de cada casa da Vila é o
mesmo reboco; os módulos da Polar são o mesmo contêiner. Tijolo, madeira,
telha, areia e neve entram com a cor da foto.

Para trocar uma textura, mude o id na tabela e rode o script (precisa
dos pacotes: `tools/baixar_pacotes.py texturas`).

### O céu

A Vila e a Estação Polar são iluminadas por uma fotografia do céu
(HDRI, Poly Haven, CC0, em `assets/mapas/ceus/`): `ceu=dict(hdri="…",
giro=…)` no `m.ambiente`. O `giro` põe o sol da foto onde está o sol do
mapa — a conta está em `tools/mapas/vila.py`. Mapa fechado não tem céu.

### Os letreiros

Tudo o que está escrito num mapa — placa de estação, saída, nome de
loja, estêncil de galpão, cartaz, pichação, a letra da base — é uma
imagem desenhada por `tools/mapas/letreiros.py` com letra de verdade
(Overpass, a de sinalização; Saira Stencil; Alfa Slab; Bebas Neue;
Permanent Marker — todas de uso livre, em `tools/mapas/fontes/` com as
licenças) e gravada em `assets/mapas/letreiros/`. O mapa só diz onde:

```python
m.letreiro(grupo, "Nome", "polar_estacao", pos, largura, yaw)          # na parede
m.letreiro(grupo, "Pouso", "polar_pouso", pos, 2.4, yaw, chao=True)    # pintado no piso
m.letreiro(grupo, "Lab", "polar_lab", pos, 1.6, 0, aceso=0.5)          # painel luminoso
```

`pos` é o centro, encostado na superfície (um ou dois centímetros para
fora); a altura sai da proporção da imagem. Letreiro novo: uma linha em
`_lista()`, rode o script, use o nome. Não há mais `Label3D` nos mapas.

### Os objetos prontos e o trem (Blender)

O que não dá para fazer bem com forma de código vem pronto, em
`assets/mapas/pecas/<nome>.glb`:

- **Objetos do Poly Haven** (barril, caixote, estante, gerador, carrinho,
  saco de cimento…): `tools/mapas/blender/pecas_polyhaven.py` abre cada
  um no Blender, junta, reduz a malha e as texturas e grava. O tamanho
  de cada um fica em `pecas.OBJETOS`; põe-se com
  `P.objeto(m, grupo, nome, "barril_vinho", pos, yaw)` — com colisão do
  tamanho dele, ou `colide=False` para o que é enfeite.

Os scripts rodam com `python3` e o módulo `bpy` (`pip install bpy`) ou
com `blender -b -P`. `tools/mapas/blender/malha.py` tem as ferramentas
(caixa chanfrada, cilindro, tubo, perfil) nas coordenadas do jogo.

### Paredes com porta e janela

`m.parede(grupo, nome, eixo, fixo, de, ate, altura, material, vaos)` faz
uma parede com aberturas de verdade — passa-se e atira-se por elas. É
com isso que se faz lugar de entrar: as casas da Vila
(`casa_aberta`), a fileira da frente dos alojamentos da Polar
(`alojamento_aberto`).

**Todo lugar de entrar tem pelo menos três entradas** (quem segura uma
não segura as outras), nenhuma sala é beco sem saída, e o que há de um
lado do mapa há do outro. As referências são os mapas de sala e
corredor dos jogos táticos (plantas compartimentadas, janela para atirar
de dentro e de fora, rotação entre cômodos) e os de mata-mata por
equipe (três corredores, um prédio no meio, flanco por dentro).

### O que todo mapa precisa ter

- **Convenção dos times:** o azul nasce em −X e olha para +X.
- **Oito pontos de nascimento por time**, sob cobertura.
- **Rotas de ronda** para os bots, com o Y do piso de cada ponto.
- **Rampa em todo desnível que o bot precisa vencer** (ele não sobe
  degrau maior que 0,5 m).
- **Alturas presas à física:** caixote 0,8 m (o humano sobe); 2,2 m (só
  o lobo, a partir do caixote); terraço a 5 m (rampa, ou o lobo
  ricocheteando num beco de 3 m).
- **As quatro perguntas** (o teste de mapa do `docs/DESIGN.md`), que
  cada arquivo responde no comentário do topo: um caminho que só o lobo
  faz, um lugar onde o humano vê o lobo chegando, um corredor que o
  humano deve evitar, e de onde cair em cima de quem não olha.

### Mapa feito no Blender

Quando o mapa vem pronto de fora (um modelo inteiro, não caixas), o
desenho entra como um `m.modelo(...)` só, e a colisão sai do próprio
modelo: `tools/mapas/blender/<id>.py` converte (escala, eixos, origem no
meio do pátio, texturas menores), grava o `.glb` em
`assets/mapas/pecas/` e um `.json` com uma caixa orientada por peça e
o chão como malha; o `tools/mapas/<id>.py` lê esse `.json`. Cuidado com
três coisas:

- **Escala**: confira a altura de uma porta ou de um andar (2,2 m e uns
  3 m). A Rua Abandonada (que saiu) veio em escala de maquete: ×3.
- **O chão como malha**: o Godot quer a frente do triângulo em sentido
  horário, o Blender anti-horário. Sem inverter, a física funciona (a
  colisão é dos dois lados), mas a navegação acha que o chão está de
  cabeça para baixo e os bots ficam parados.
- **Paredes invisíveis** na beira do pátio, altas, para ninguém sair
  pelos telhados.

### Mapa novo

1. Copie um arquivo de `tools/mapas/`, mude o id e monte o seu.
2. Registre em `tools/gerar_mapas.py` e em `scripts/meta/Maps.cs`.
3. Acrescente um teste em `tests/regression/MapTests.cs` (três linhas).
4. `python3 tools/mapas/conferir.py <id>` (faces brigando, letreiro mal assentado, objeto no ar), `python3 tools/gerar_mapas.py <id>` e `tools/testar.sh Mapa`.

## Testes

`MapaVila` e `MapaPolar`: cada mapa carrega
na partida, os dois times nascem, ninguém cai do mapa, os bots saem da
base pela malha de navegação e trocam tiro.
