# Mapas

Os mapas do jogo estão em `Maps.All` (`src/scripts/meta/Maps.cs`): seis,
todos na lista.

| Mapa | id | Tamanho | Feito com |
|---|---|---|---|
| **Armazém** | `armazem_estendido` | 75 × 45 m (médio), 13 m de altura | o pacote LowPolyFPS4 da Unity (abaixo) |
| **Cidade** | `cidade` | 86 × 92 m (grande) | a cena de demonstração do kit fps1 (veja "Mapas da cena de demonstração") |
| **Cidade do Deserto** | `deserto` | 64 × 64 m (médio) | a cena de demonstração do kit fps2 |
| **Estação de Trem** | `estacao` | 105 × 65 m (grande) | a cena de demonstração do kit fps5 |
| **Obra** | `obra` | 40 × 40 m, três pisos (pequeno) | a cena de demonstração do kit fps6 |
| **Distrito Neon** | `neon` | 124 × 100 m (grande) | o pacote Low Poly Sci-Fi City da Unity (veja "Distrito Neon") |

**O que saiu.** O Hangar, o Metrô e a Rua Abandonada saíram em 16/11 (a
pedido). O Armazém original, a Vila e a Estação Polar saíram em 24/11
(Jhon: "mapas que não são utilizados no jogo podem ser removidos e nem
devem aparecer no dashboard"): as cenas, os modelos, as fotos, os
letreiros e os geradores (`vila.py`, `polar.py`, `letreiros.py`) foram
apagados (estão no histórico do git). Salas e perfis antigos com
`armazem`, `vila` ou `polar` caem no Armazém (`Maps.Get`). No servidor, a
semente também apaga do banco (e do painel) o mapa que o jogo não tem
mais (`PgGameDataRepository.SeedMaps`).

A sala escolhe o mapa; a cena da partida (`src/scenes/main.tscn`) tem um nó
`Map` com o script `MapSlot`, que carrega a cena do mapa escolhido antes
de os jogadores nascerem. Para abrir um mapa direto no editor, ponha o
id dele em **Map Id** no nó `Map`.

## A fonte de cada mapa é a cena

**Desde 24/11 a fonte da verdade é o `.tscn` em `src/scenes/maps/`.**
Mexer num mapa é abrir a cena no Godot e mexer nela.

Até ali os mapas eram feitos por script: um arquivo por mapa em
`src/tools/mapas/` (`armazem_estendido.py`, `cidade.py`, `deserto.py`,
`estacao.py`, `obra.py`, `neon.py`, sobre o kit `kit.py`, as formas de
`formas.py` e as peças de `kits/`), e o `src/tools/gerar_mapas.py`
escrevia o `.tscn` e a miniatura (`src/assets/ui/mapas/<id>.png`). O
`gerar_mapas.py` **saiu em 24/11**, junto com `mapas/texturas.py`,
`mapas/pecas.py`, `mapas/blender/pecas_polyhaven.py`,
`mapas/blender/malha.py` e `baixar_pacotes.py`. Os módulos dos seis mapas
ficaram, mas **nada os chama mais**: são o registro de como cada mapa
nasceu (as medidas, as plantas em letras, as rotas). **Em 27/11 o `gerar_mapas.py` voltou, só para quatro mapas** — a Cidade, a
Cidade do Deserto, a Estação de Trem e a Obra, refeitos naquele dia da cena
de demonstração de cada kit (veja "Mapas da cena de demonstração"):
`python3 src/tools/gerar_mapas.py [cidade|deserto|estacao|obra]`. Gerar
sobrescreve o `.tscn` e apaga o que foi mexido no Godot desde então:
enquanto ninguém mexer nesses quatro na cena, o módulo é a fonte; mexeu na
cena, a cena passa a ser a fonte e o mapa sai da lista do gerador. O
Armazém e o Neon não estão na lista (foram mexidos na cena).

O resto desta página conta como cada mapa foi feito e o que precisa
continuar valendo em qualquer mapa, feito à mão ou por script.

### O kit de mapa (`src/tools/mapas/kit.py`)

O `Mapa` do kit tem `caixa`, `cilindro`, `rampa`, `parapeito`, `luz`,
`parede`, `modelo`, `escada`, `foco` (luminária de teto com sombra),
`spawn`, `rota`, mais o céu, a neblina e o sol do mapa. Para mapa fechado,
`m.ambiente` aceita ainda `sdfgi` (luz rebatida), `ssil`, `agx`
(mapeamento de cor) e `volumetrica` (névoa com raio de sol).

Quando a forma que se vê é mais rica que a caixa, a caixa fica com
`oculta=True`: continua sendo a colisão, só não é desenhada. **O que se
vê não pode fugir muito do que colide** — tiro e corpo batem na caixa.
A caixa oculta aparece no minimapa em cinza; `cor=(r, g, b)` dá a cor
dela ali (o Armazém passa a cor de cada peça, tirada da paleta do
modelo pelo conversor). As formas que não são caixa saíam de
`src/tools/mapas/formas.py` como `.obj` em `src/assets/mapas/<id>/`;
cena com `.obj` novo precisa do Godot aberto (ou `godot --headless
--import`) para importar, e o `.obj` e o `.obj.import` vão para o git
junto com a cena.

`python3 src/tools/mapas/conferir.py <id>` ainda lê o módulo de um mapa e
acusa face brigando com face, ponto de nascimento dentro de parede e
objeto no ar — mas, sem a tabela de tamanhos do `pecas.py`, conta cada
objeto como uma caixa de 30 cm. Confere o módulo, não a cena.

### As texturas e o céu

As texturas de fotografia (`src/assets/textures/pbr/`, 44 conjuntos do
[Poly Haven](https://polyhaven.com), CC0) eram da Vila e da Polar; dos
mapas de hoje, só aparecem areia, asfalto, cascalho e madeira. Cada
material são três arquivos de 1024 px: `<nome>_cor.jpg`,
`<nome>_normal.jpg` e `<nome>_arm.jpg` (oclusão em R, aspereza em G,
metal em B). O resto do acabamento dos mapas de hoje é o detalhe de
superfície por cima da paleta (veja "O detalhe das superfícies").

**O céu dos mapas de dia é calculado, com nuvens que andam** (27/11, pedido
do Jhon: "shaders e nuvens"; "em todos os mapas que são de dia"):
`src/assets/mapas/ceus/ceu_nuvens.gdshader` na Cidade, no Deserto, na
Obra e na Estação. A cor do ar (o azul do alto, o claro do horizonte, o
clarão e o disco do sol) e uma camada de nuvens com volume — a barriga
escura, o lado do sol aceso, a borda que brilha contra a luz —, tirada do
ruído `nuvens_ruido.png` (desenhado por `kits/fundos.py: nuvens`). As
nuvens são desenhadas em meia resolução; como o céu anda, o recurso `Sky`
da cena fica em tempo real (`process_mode = 3`, 256 px) e a luz ambiente
acompanha. O sol do céu é a primeira luz direcional que entra nele: a luz
de preenchimento do mapa fica fora (`sky_mode = 1`). Os valores de cada
clima (`dia`, `deserto`, `tarde`) estão em `CEUS`, em
`src/tools/mapas/kits/montagem.py` (e na cena: os `shader_parameter/…` do
material `CeuMat`). Numa cena que já foi mexida no Godot, troca-se o céu
sem gerar o mapa: `python3 src/tools/mapas/ceu_de_nuvens.py <id> <clima>`.
O céu de fotografia (`dia_claro.hdr`, Poly Haven) saiu dos mapas. O Neon
continua com `noite_neon.hdr` (desenhado, `kits/fundos.py`); o Armazém é
fechado e não tem céu.

### Escadas

A escada de subir (de marinheiro) é do jogo, não do mapa: em todo mapa
ela sobe e desce igual (decisão do Jhon, 17/11). Na cena, é um nó
`Ladder` (Area3D com o script `src/scripts/core/Ladder.cs`) e uma chapa
lisa de colisão no lugar dos degraus; o desenho da escada é do mapa. No
kit era `m.escada(grupo, "Escada1", pe, yaw, altura, largura=1.0)`: `pe` é
o ponto no chão, no meio da escada, encostado na frente dos degraus;
`yaw` é para onde ela olha (com 0, a parede fica em −Z); `altura` vai do
chão ao piso de cima.

Como se usa (`Player.Ladder.cs`): agarra andando para ela no pé, ou
caindo dentro dela (quem sai da passarela pela abertura fica preso
nela). W sobe, S desce, olhando bem para baixo o W desce; A e D de lado.
No alto, o corpo é empurrado para cima do piso; o pulo solta para trás.
Vale para humano e lobo. Os bots ainda não usam escada (a navegação não
liga o pé ao topo).

### Lugares de entrar

**Todo lugar de entrar tem pelo menos três entradas** (quem segura uma
não segura as outras), nenhuma sala é beco sem saída, e o que há de um
lado do mapa há do outro. As referências são os mapas de sala e
corredor dos jogos táticos (plantas compartimentadas, janela para atirar
de dentro e de fora, rotação entre cômodos) e os de mata-mata por
equipe (três corredores, um prédio no meio, flanco por dentro). No kit,
`m.parede(grupo, nome, eixo, fixo, de, ate, altura, material, vaos)` fazia
parede com aberturas de verdade, por onde se passa e se atira.

### O que todo mapa precisa ter

- **Convenção dos times:** o azul nasce em −X e olha para +X.
- **Oito pontos de nascimento por time**, sob cobertura.
- **Rotas de ronda** para os bots, com o Y do piso de cada ponto.
- **Rampa em todo desnível que o bot precisa vencer** (ele não sobe
  degrau maior que 0,5 m).
- **Alturas presas à física:** caixote 0,8 m (o humano sobe); 2,2 m (só
  o lobo, a partir do caixote); terraço a 5 m (rampa, ou o lobo
  ricocheteando num beco de 3 m).
- **As quatro perguntas** (o teste de mapa do `docs/DESIGN.md`): um
  caminho que só o lobo faz, um lugar onde o humano vê o lobo chegando,
  um corredor que o humano deve evitar, e de onde cair em cima de quem
  não olha. Os módulos de `src/tools/mapas/` respondem no comentário do
  topo.

O que a cena precisa ter, nó por nó, está em `docs/ARQUITETURA.md`
("Criar um mapa").

### Mapa feito no Blender

Quando o mapa vem pronto de fora (um modelo inteiro, não caixas), o
desenho entra como um modelo só, e a colisão sai do próprio modelo:
`src/tools/mapas/blender/<id>.py` converte (escala, eixos, origem no
meio do pátio, texturas menores), grava o `.glb` em
`src/assets/mapas/pecas/` e um `.json` com uma caixa orientada por peça e
o chão como malha; o `src/tools/mapas/<id>.py` lia esse `.json`. Cuidado com
três coisas:

- **Escala**: confira a altura de uma porta ou de um andar (2,2 m e uns
  3 m). A Rua Abandonada (que saiu) veio em escala de maquete: ×3.
- **O chão como malha**: o Godot quer a frente do triângulo em sentido
  horário, o Blender anti-horário. Sem inverter, a física funciona (a
  colisão é dos dois lados), mas a navegação acha que o chão está de
  cabeça para baixo e os bots ficam parados.
- **Paredes invisíveis** na beira do pátio, altas, para ninguém sair
  pelos telhados.

### O Armazém

Montado com as peças do pacote LowPolyFPS4 da Unity Asset Store, sem
modelo novo: é o Armazém original (que saiu em 24/11) maior e mais alto.
O FBX exportado da cena da Unity não tem UV; os modelos de dentro do
`.unitypackage` têm. O conversor:

```sh
pip install bpy numpy scipy
python3 src/tools/mapas/blender/armazem_estendido.py <cena.fbx> <pacote.unitypackage>
```

Ele usa a leitura do conversor do original (`blender/armazem.py`,
`armazem.carregar`: casa cada peça da cena com o modelo de origem e copia
a UV e a cor) e remonta, gravando `src/assets/mapas/pecas/armazem_estendido.glb`
e o `.json`. A casca do galpão é colisão de malha dentro do `.glb`; o
resto é uma caixa orientada por peça, e o contêiner é um túnel de quatro
caixas. O `src/tools/mapas/armazem_estendido.py` (com o `armazem.py`, de
que ele herda) montava a cena.

- **O miolo** de 45 × 30 m é o do Armazém original, peça por peça, no
  mesmo lugar.
- **As paredes andam para fora**: as compridas 7,5 m (45 m de fundo) e
  as das pontas 15 m (75 m de comprido). Cada parede é um módulo de 15 m
  do pacote, com a passarela, a escada, as janelas e o que está
  encostado nela (prateleiras, extintor, caixas na passarela),
  repetido; um pilar em cada emenda. São 10 escadas (5 por parede
  comprida).
- **Mais alto**: o teto sobe de 9 para 13 m. A parede estica só acima
  de 5 m — as janelas ficam mais altas; a passarela (3,63 m), o
  corrimão e a escada não mudam. O telhado, com as claraboias e as
  vigas, sobe inteiro e se alarga para os 45 m.
- **Alas novas** (os 15 m de cada ponta): uma pirâmide de contêineres
  1-2-3-2-1 (até 8,7 m, degraus de 2,9 m para o lobo; cinco túneis por
  baixo), uma pilha de dois e contêineres soltos, com enfeites copiados
  do miolo (barris, pneus, sacos de areia, refletores). A oeste é a
  leste girada 180°: o azul nasce no canto −X/+Z, o laranja no oposto.
- **Corredores** novos de ponta a ponta junto às paredes compridas, com
  pouca cobertura.

As medidas estão no topo do script (`CONTEINERES`, `ENFEITES`,
`ALTURA`). As rotas das alas foram achadas por busca de caminho com
0,5 m de folga, longe das escadas das paredes. As 25 luminárias do teto
estão a 12 m; só as das fileiras ímpares fazem sombra, para o custo não
dobrar. As janelas sem vidro (uma em cada parede da ponta) vêm do pacote
e continuam assim nas cópias; as paredes invisíveis por fora seguram
quem pula nelas.

### A foto do mapa

Cada mapa tem uma foto (1920 × 1080) tirada no próprio jogo, de um bom
ângulo, para a escolha do mapa, a sala e a tela de carregamento
(`MapInfo.Cover`, `src/assets/ui/mapas/<id>_foto.png`):

```sh
godot --path . -s src/tools/fotos_mapas.gd                 # todos
godot --path . -s src/tools/fotos_mapas.gd -- obra         # um só
```

O ângulo de cada mapa (de onde a câmera olha e para onde) está em
`FOTOS`, no topo do script.

### Mapa novo

1. Monte a cena em `src/scenes/maps/<id>.tscn` no Godot (o que ela
   precisa ter está em "O que todo mapa precisa ter", acima, e no
   `docs/ARQUITETURA.md`). Copiar a cena de um mapa parecido é o
   caminho mais curto. Para fazer por script, restaure o
   `gerar_mapas.py` do histórico do git e escreva um módulo em
   `src/tools/mapas/`.
2. Registre em `src/scripts/meta/Maps.cs` (com a `Cover` e a miniatura
   `src/assets/ui/mapas/<id>.png`) e ponha o ângulo da foto em
   `src/tools/fotos_mapas.gd`.
3. Acrescente um teste em `src/tests/regression/MapTests.cs` (três linhas).
4. `src/tools/testar.sh Mapa`.

## Mapas da cena de demonstração (27/11)

Em 27/11 o Jhon trouxe o "Low Poly FPS Bundle" na versão do Unreal (`Archive.zip`: `.uasset` e `.umap`), que tem o que
o pacote em FBX não tinha — **o mapa que o autor montou com cada kit** — e pediu os mapas feitos dele ("use o que tem lá
de demo"), com "iluminação AAA+, shaders e nuvens". Os quatro mapas de planta própria (de 19 e 20/11) saíram; estão no
histórico do git. Os ids são os mesmos: salas e perfis não mudam.

| Mapa | O que é | O que mudou em relação à cena |
|---|---|---|
| **Cidade** | Um bairro em volta de uma avenida de 80 m, entre a prefeitura e a torre do heliponto: casas de três e quatro andares, estacionamento, praça, oficina, o pátio do prédio em obras, e os becos por trás das casas. 2.140 m² de chão andável | Girada 90° (a avenida no eixo dos times). Na fileira de barreiras de concreto que fecha a avenida a 17 m do fim saíram cinco, abrindo duas passagens: atrás dela nasce o laranja. Sem o chão de fundo da cena: o mapa tem o asfalto dele, com o topo 2 cm abaixo da calçada do kit, como na Estação (até a correção de 27/11 estava no mesmo plano, e a calçada aparecia em cacos claros) |
| **Cidade do Deserto** | Uma cidadela de 64 × 64 m dentro da muralha: casas de adobe de um e dois andares com escada de pedra, toldos, bancas, a praça do chafariz, o largo da feira, palmeiras. 1.470 m² de chão andável | **Sem montanhas** (Jhon: "montanha com gelo no deserto não faz sentido"; "quero somente morros de areia"; "deserto não tem montanha nem picos"): saíram as cinco de pico branco, as dunas entram a 65% da altura e o painel do fundo ficou na proporção do desenho (a 120 m de altura as dunas redondas dele viravam picos) |
| **Estação de Trem** | Um pátio de estação fechado por paredes de 9 m: três linhas com trens parados ao norte, o saguão coberto no meio, mais duas linhas ao sul, os prédios da estação em volta e duas escadas para o piso de cima (5,1 m). 2.620 m² de chão andável | Nada: é a cena. (As vidraças do piso de cima são o cubo do Unreal esticado; aqui, uma chapa da cor do vidro) |
| **Obra** | O topo de um prédio **acima das nuvens** (27/11, Jhon: "ao invés de cidade ao redor do prédio, será um prédio alto acima das nuvens; o precipício será uma grande nuvem": saíram as torres e as ruas da demo, o prédio desce andar sobre andar até 250 m e some no mar de nuvens — quatro camadas de `nuvens.gdshader`, o do precipício do Setor Zero, no tom do pôr do sol, e a névoa adensa abaixo de −90 m; a beira continua fechada pela parede invisível), onde se levantam mais andares: 40 × 40 m, paredes de concreto pela metade, três escadas para o segundo piso (3,8 m), um pedaço de terceiro (7,6 m), duas gruas, andaimes. 1.150 m² de térreo andável | Saem 7 módulos de parede, o mínimo para o térreo ser um chão só (abaixo) |

**Todos no tamanho da cena.** O primeiro pedido era "crie um maior, igual ao Armazém e ao Distrito Neon" (dobrar com a
cópia girada 180°). Medidas as cenas, o Jhon decidiu não dobrar a Cidade nem o Deserto ("para não ficar exagerado") e,
vista a Obra dobrada (80 × 40 m), voltou atrás nela também: "deixa o mapa da construção igual o da demo mesmo". Por
isso **nenhum dos quatro é simétrico**: cada time nasce num lugar diferente (`BASES`, no módulo), e ninguém mediu se um
lado leva vantagem. A Obra, com 40 × 40 m, é apertada para 6 contra 6.

### Como são feitos

```sh
# uma vez por kit: as plantas (só Python; precisa do pacote do Unreal, que não vai para o repositório)
python3 src/tools/mapas/kits/unreal_umap.py <pacote>/LowPolyFPS1/Maps/DEMO_Map.umap src/tools/mapas/fps1_demo.json <pacote>/LowPolyFPS1/Meshes
# o mapa (numpy e Pillow), e depois o Godot importa o .glb (abra o editor, ou godot --headless --import)
python3 src/tools/gerar_mapas.py cidade
```

- **`kits/unreal_umap.py`** lê o `.umap` do Unreal 5.0 sem o Unreal: a tabela de nomes, os objetos e, de cada peça, a
  malha e a matriz (com a do pai, se estiver presa a outro ator), já no eixo do Godot — o Unreal é de mão esquerda, Z
  para cima, em centímetros: posição (x, z, y) / 100. Grava também as luzes, os pontos de fogo e a caixa de cada malha
  (`ExtendedBounds`), com que se conferiu o eixo de cada peça contra o kit em FBX. O nome no Unreal é o do kit com `SM_`
  e a pasta na frente (`SM_Props_Box_01` → `Box_01`); as exceções estão em `kits/demo.py` — duas malhas de tamanho
  diferente (`ESCALAS`), as paredes e os telhados da Estação, que mudaram de nome (`NOMES`), e as malhas com esqueleto
  (a casa com porta, a cancela, a caixa de munição), que entram paradas. O palete deitado da Obra não veio no FBX: é
  feito de tábuas (`demo.palete`).
- **`kits/demo.py`** (`Demo`) põe as peças numa `Montagem` e tem o que os mapas dividem: o lado de fora
  (`fora_do_anel`: o que se alcança da beira do mapa sem passar por fresta de menos de 5 m entre os prédios e muros
  que o fecham; vira `Barreira…`, do chão ao teto), o miolo dos prédios fechados (`fechar_predios`, abaixo), o chão que
  se alcança a pé (`alcancavel`), os pontos de nascer de mapa que não é simétrico (`nascer_em`), a cópia girada
  (`girar`, de quando a Obra foi dobrada) e a planta para conferir (`vista_de_cima`).
- **Os módulos** (`cidade.py`, `deserto.py`, `estacao.py`, `obra.py`): a colisão de cada tipo de peça, as escadas com a
  chapa de subida (`ESCADAS`), onde nasce cada time e as rotas dos bots (`ROTAS`: pontos soltos, cada um cai no chão
  livre mais perto e o caminho entre eles é achado na grade).
- **A colisão é a da própria malha**, menos o que é caixa de verdade (caixote, piso, pilar) — `CAIXA` e `SEM` no topo
  de cada módulo. A caixa de um barril, de um pote, de uma bobina ou do guindaste segurava o tiro de cima no ar, sem
  nada que se visse (os testes `TiroDeCima…`).
- **O miolo dos prédios fechados** (`demo.fechar_predios`, `miolo_da_peca`): a casa do kit é uma casca, e por dentro
  dela ficava um cômodo fantasma, com o chão do mapa por piso. O prédio em que nenhum raio, na altura de quem anda,
  passa da parede (não tem porta nem vão) e que tem telhado ganha uma caixa de colisão escondida por dentro, 60 cm para
  dentro de cada parede (`miolo…`). No prédio que **fecha o mapa** (encosta no lado de fora) essa caixa vai do chão ao
  teto e é uma `Barreira…`: ninguém passa por cima dele para fora. Muro e parede fina não ganham caixa: quem os fecha
  é a mancha de fora.
- **As paredes abertas da Obra** (`obra.py: abrir`): na cena o térreo é uma fileira de cômodos fechados — paredes
  cegas, de janela (peitoril de 50 cm) e algumas com porta de 1 m. A navegação dos bots anda com meio metro de raio
  (`NavBaker`): por porta de 1 m eles não passam, e o térreo ficava partido em pedaços sem ligação (quem nascesse num
  deles não saía). Como é obra, a parede que falta "ainda não foi levantada": saem **só** os módulos (5 m) que ligam um
  cômodo ao vizinho, o mínimo para o térreo ser um chão só — de preferência a que já tinha porta, depois a quebrada,
  depois a de janela. São 7. `ATALHOS` abriria mais alguns (está em zero). As paredes da beira da laje ficam: são elas
  e a parede invisível por fora que seguram quem chega à beira.
- **A luz**: a de jogo dos mapas de kit (`luz_de_jogo`, abaixo), com o céu calculado do clima — `dia` na Cidade,
  `deserto` no Deserto, `tarde` na Estação e na Obra (sol a 24°). A névoa do mapa não entra no céu (com ela o azul
  virava lilás). Nos tambores acesos da Cidade, uma lâmpada cor de fogo; nas luminárias da Estação e da Obra, lâmpadas.
- **O material de cada face** é o detalhe de superfície dos kits (abaixo), como antes: as peças são as mesmas.
- **O palete carregado da Obra** (`demo.palete`): o `Pallet_02` do Unreal é uma pilha de 1,21 × 1,0 × 1,18 m, empilhável,
  e não veio no FBX. Até a revisão de 27/11 ele era um palete vazio de 15 cm, e o que a demo põe em cima dele (latas,
  pincéis, tijolos, o martelo, o carretel, outro palete) flutuava 85 cm (Jhon: "alguns pontos do mapa construção estão
  com madeiras voando"). Agora é o palete do kit (`Pallet_01`) com quatro camadas de blocos (`Brick_01`/`02`) em cima,
  cruzadas; o detalhe pinta de tijolo o que fica acima de 16 cm (`detalhe.palete_carregado`).
- **A escada do prédio em obras da Cidade** (`cidade.OBRA_ESCADA`) tem a chapa de subida das outras escadas (27/11):
  sem ela, descendo, o corpo escorregava pela quina arredondada do degrau e voava três degraus de cada vez.
- **O que estava no terreno da demo** (`demo.assentar`): o Deserto do Unreal tem um terreno com relevo (*landscape*),
  que não vem; aqui o chão é plano, e as palmeiras, pedras e plantas que estavam num montinho ficavam até 78 cm no
  ar. Descem até o que está embaixo (as pedras antes, que há planta em cima de pedra).

### A revisão de colisão (27/11)

Pedido do Jhon: "revise todos os itens de colisões dos mapas que implementamos". Duas varreduras, peça por peça, nos
quatro mapas (as ferramentas ficaram de fora do repositório; o jeito de refazer está abaixo):

- **Peça que se vê e não colide** (de 35 cm para cima, no chão jogável). Passaram a colidir: na Obra, o palete
  carregado (20, por caixa), as pilhas de barras de aço (`ConstructionMat_01`, ~1 m, na forma delas) e a escadinha de
  abrir; na Estação, as vidraças do piso de cima (o cubo do Unreal esticado — passava-se por elas e caía-se no pátio),
  a porta do piso de cima e as torres em cima dos telhados (o lobo que corria no telhado passava por dentro delas); na
  Cidade, os montes de lama (até 65 cm, na forma deles) e as placas de rua de 1,8 m (pela haste). Ficam sem colisão, de
  propósito: planta, arbusto, varal, jarro, bandeira, o que é pendurado em parede (luminária, relógio, janela, cano,
  enfeite) e os fios.
- **Peça solta no ar** (o ponto mais baixo dela não acha nada embaixo a menos de 6 cm). Além do palete e das plantas
  do Deserto (acima), o que sobra é de propósito: o material pendurado no gancho das gruas da Obra, as tábuas pregadas
  atravessando a porta (Obra), os caixas eletrônicos de parede, os barris deitados na mureta dos telhados da Cidade, as
  caixas empilhadas e os vagões de toras da Estação (conferidos em foto).
- **Caixa de colisão maior que o desenho** (parede invisível): nenhuma; o `TiroDeCima…` confere de cima, nos quatro.

### O que foi conferido e o que não foi

Conferido: a planta de cada mapa vista de cima contra a cena (peça a peça no lugar e no giro certos), o chão andável e
as rotas na grade, fotos Forward+ de cada mapa (de cima e da altura do jogador) e os testes (abaixo). **Ninguém jogou
uma partida neles.** Não foi medido o custo do céu em tempo real em máquina fraca, nem o de tanta colisão de malha. Na
Obra, quem chega ao terceiro piso é só o lobo (nenhuma escada vai até lá, como na cena), e as portas de 1 m servem ao
jogador, não aos bots.

<!-- mapas de kit: início -->

**O muro da cidade no Neon** (25/11): o Neon fecha no muro da cidade, não nas paredes `Limite…` da borda: tudo o que fica do lado de fora vira a parede invisível `BarreiraMuro` (do chão ao teto; por cima do carro e da sala de piso alto que encostam no muro, a partir de 3 m acima do carro e do telhado da sala). Ela é medida na colisão da cena: `godot --headless --path . -s res://src/tools/mapas/muro_da_cidade.gd -- neon [--seco] [--imagem=grade.png]` (rode de novo se o mapa mudar: refaz o corpo). O teste é `NeonMuroDaCidade`. **Os fios** do Neon (`SM_Buildings_Part_24` e `_25`) não têm colisão: `neon.py: FIOS`, e no `.glb` já montado `python3 src/tools/mapas/sem_colisao.py <glb> <prefixos>`.

**Colisão das peças finas** (24/11): nos mapas de kit, peça fina ou recortada (poste, placa, mesa, banco) colide na própria malha, não numa caixa do tamanho total dela — a caixa virava parede invisível. No Neon a troca está feita; para outro mapa, ou se a cena voltar a ter caixa nessas peças: `godot --headless --path . -s res://src/tools/mapas/colisao_exata.gd -- <mapa> [--seco]` (a lista de peças, `FINAS`, é a do pacote do Neon; outro kit pede a lista dele). Na Obra (26/11) entram as pilhas de sacos de cimento (`CementC`), com o casco convexo (`CONVEXAS`), não a malha inteira.

## Mapas de kit (Low Poly FPS Bundle)

Em 19/11 o Jhon trouxe o pacote "Low Poly FPS Bundle" (JustCreate, na Fab) em FBX e pediu os mapas no jogo. O pacote em
FBX traz **as peças** de cada mapa (casas, paredes, pisos, trens, objetos) e a textura, mas **não o mapa montado** — a
planta só existe nas cenas da Unity e da Unreal. Estes mapas foram montados aqui, com planta própria, pelo tema de cada
kit (não são cópia do mapa de demonstração do autor). Em 20/11 o Jhon pediu **mapas maiores e mais robustos, com
caminhos e corredores** em vez de um retângulo aberto: foram refeitos no tamanho da Estação Polar (que saiu em 24/11), com a planta em letras
(`src/tools/mapas/kits/planta.py`):

| Mapa | Tamanho | Tema | Módulo (registro; veja "A fonte de cada mapa é a cena") |
|---|---|---|---|

**Desde 27/11 nenhum mapa do jogo é desta leva.** A Cidade, o Deserto, a Estação e a Obra de planta própria (120 × 80,
110 × 75, 120 × 95 e 100 × 70 m, com a planta em letras) foram trocados pelos da cena de demonstração (acima); o que
este capítulo conta deles — as calçadas da Cidade, a muralha do Deserto, a passarela da Estação, os andares da Obra — é
o registro de como eram. As ferramentas (a planta em letras, a `Montagem`, a luz de jogo) continuam em uso.

O Vol.4 (Warehouse) é o pacote do Armazém, que já estava no jogo.

### Como são feitos

```sh
# uma vez por kit (precisa do Godot e dos .fbx em src/assets/pacotes/, que não vão para o repositório)
godot --headless --path . -s src/tools/mapas/kits/extrair.gd -- res://src/assets/pacotes/fps_bundle/fps1 /tmp/kits/fps1
python3 src/tools/mapas/kits/kit_fbx.py fps1 /tmp/kits <a textura do kit>.png
# o mapa (só Python: numpy e Pillow) — era assim até 24/11; o gerador saiu (veja "A fonte de cada mapa é a cena")
python3 src/tools/gerar_mapas.py cidade
```

O que segue descreve como os módulos montavam os mapas: serve para entender a cena e para quem restaurar o gerador.

- **`src/tools/mapas/kits/extrair.gd`** lê cada `.fbx` no Godot e grava os triângulos (posição, normal, UV) de cada peça;
  **`kit_fbx.py`** junta tudo em `src/tools/mapas/kits/<kit>.npz` e copia a textura para `src/assets/mapas/kits/<kit>.png`.
  Alguns arquivos vêm em centímetros ou deitados: as listas `EM_CM` e `DEITADA`, no topo, acertam (vistas peça a peça).
- **`src/tools/mapas/kits/planta.py`** (`Planta`) é a planta do mapa **desenhada em letras**, no topo do arquivo de cada mapa:
  cada letra é uma célula de 2,5 m (a largura de um beco) vista de cima. Desenha-se só a metade oeste; a leste sai dela
  girada 180° ou espelhada. O que cada letra quer dizer é o mapa que diz (a legenda está em cima do desenho): um prédio
  por retângulo de letras iguais (`regioes`, `fatiar`), esticado para encher o retângulo e com a fachada para o lado de
  mais chão livre (`frente`); `-`, `|` e `+` são parede fina no meio da célula, com letra de vão para a porta
  (`paredes`); os algarismos são as marcas por onde passam as rotas dos bots (`rota`). **Para mudar um beco de lugar,
  abrir uma passagem ou pôr um carro, trocava-se a letra e rodava-se o gerador** (hoje, mexe-se na cena).
- **`src/tools/mapas/kits/montagem.py`** (`Montagem`) põe as peças: `por(nome, x, z, yaw)` — o meio da peça em (x, z), a base
  no chão; `pivo=True` usa a origem da peça (paredes e pisos modulares). `simetrico()` repete tudo girado 180° (o outro
  time vê o mesmo mapa); `espelhar()` espelha em x = 0 (quando o mapa tem um lado norte, como o saguão da Estação).
  A frente das casas é o +Z da peça: `yaw=0` olha para +Z, `yaw=90` para +X. `encaixar` estica a peça para encher um
  retângulo, `muro` põe uma fileira de módulos de parede de um ponto a outro e `chapa` uma caixa lisa de uma cor da
  textura do kit (o asfalto, a calçada, a laje). A peça esticada vira uma malha própria no `.glb`: o nó fica só com giro
  e posição (corpo de colisão com escala não funciona direito no Godot).
- **O desenho** vai num `.glb` por mapa (`src/assets/mapas/pecas/<id>.glb`): uma malha por peça diferente, um nó por peça
  posta, um material só (a textura do kit embutida, desenhada dos dois lados).
- **A colisão** (`colide=`): `'malha'` — o nó ganha o sufixo `-col` e o Godot cria o corpo e a forma a partir da própria
  malha (onde se entra ou se sobe: lojas, o prédio em obras, escadas); `'caixa'` — uma caixa escondida no `.tscn` (prédio
  fechado, parede lisa, carros, caixotes, barris; é o que o minimapa desenha, na cor média da peça); `None` — enfeite. Cada mapa tem a tabela dele no topo (`CAIXA`, `SEM`).
  Árvore e poste: a peça sem colisão e uma `barra` (só o tronco). O minimapa recebe ainda uma caixa invisível por peça de
  malha (nó `Planta`).
- **O chão e os limites**: `fechar(m, X, Z)` põe o `Piso` e as paredes invisíveis `Limite…` (a regra de `MapBounds`).
  **`barrar(m, P, letras, altura)`** fecha o alto do muro de fora: o lobo corre na parede e, de salto em salto, chega em
  cima dela — então sobre todo o maciço que encosta na borda da planta vai uma caixa invisível do chão ao teto
  (`Barreira…`; pedido do Jhon, 20/11: "nas paredes, coloque um limite invisível do mapa, em todos"). O que fica no
  meio do mapa (os prédios da Cidade, a muralha de dentro do Deserto, as lajes da Obra) continua de se subir. O teste de
  mapa confere: 244 pontos em toda a volta, a 1 m do teto, têm de cair dentro de barreira.
  **O lobo não corre em parede invisível** (Jhon, 20/11): a corrida em parede ignora os corpos `Limite…` e `Barreira…`
  (`Player.IsInvisibleLimit`) — não gruda neles e, se um deles fecha a frente, solta em vez de virar a quina para ele. A
  barreira fica 6 cm para dentro da face do muro que dá para o chão, para que embaixo quem o lobo toca seja o muro
  (teste `ParedeInvisivel`).
- **A altura e o fundo** (pedido do Jhon, 20/11: "bem mais alto, tão alto que nem os lobos alcançam", "preencha o
  fundo"): o que fecha cada mapa tem de 14 a 20 m ou mais (a muralha de fora do Deserto em dois panos, as empenas dos
  vizinhos na Obra, o muro da Estação, os prédios de dois corpos da Cidade, os vizinhos e a torre do Escritório —
  `Planta.marcada` separa o maciço de fora do de dentro, `Montagem.torre` faz o prédio de faixas de vidro). Para fora do
  limite, `Montagem.arredores` põe duas fileiras de prédios do kit em toda a volta e `Mapa.panorama` quatro painéis com
  a imagem do horizonte (`src/assets/mapas/fundos/`, desenhadas por `src/tools/mapas/kits/fundos.py`: a cidade e a serra).
- **Enfeite não tromba**: nos laços que espalham, `Montagem.grupo()` confere a caixa de cada peça nova contra o que já
  estava posto e tira o que entrou em outra coisa; a parede fina só serve de encosto de frente (não a ponta nem o canto).
- **Os enfeites** (pedido do Jhon, 20/11: "interiores mais ricos", "vegetação, detalhes, objetos"): além das letras, cada
  mapa espalha miudezas **sem colisão** ao pé das paredes (`Planta.beiras` + `Montagem.encostar`) e no chão livre, sorteadas
  pelo lugar (`Planta.sorte`: o mesmo sorteio no ponto do outro time) — não mudam por onde se anda. O que tem colisão
  (banco, caixote, mesa) entra por letra na planta, e as rotas são conferidas de novo. Os kits da Estação e da Obra não
  têm planta: o mato deles vem do kit do Escritório, num segundo `.glb` (`<id>_mato.glb`, função `mato()`). Na Obra as
  colunas vão na malha de 7,5 m do prédio, em todos os pisos (`colunas`): inteiras onde há laje por cima, o toco com a
  ferragem de espera onde o piso de cima ainda não foi feito.
- **Nascimentos e rotas**: `Grade` é a planta de onde se anda, tirada da colisão (25 cm por célula, 55 cm de folga);
  `nascer` acha seis pontos livres em volta do lugar dado (e os do outro time, do outro lado); `Grade.rota` liga os
  pontos de cada rota pelo caminho andável. Mapa com desnível (as plataformas da Estação) passa a função `piso(x, z)`.
- **A foto e a miniatura**: a foto é a de sempre (`src/tools/fotos_mapas.gd`); a miniatura sai de `Montagem.planta`.

Para mexer num mapa hoje: abra a cena no Godot. Com o gerador, editava-se `desenho()` no arquivo dele, gerava-se e o Godot
importava o `.glb` de novo; a grade (`Grade.imagem`) mostrava o que ficou livre e as rotas.

### O que foi conferido e o que não foi

Cada mapa passa o teste de mapa (carrega, os times nascem em chão livre, sem buraco no chão, o alto do muro de fora é
barrado, os bots saem da base pela navegação e trocam tiro) e foi fotografado no Godot de cima e do olho do jogador. **Ninguém jogou uma partida neles**: o
ritmo, os pontos de vantagem e o que o lobo alcança de fato são do desenho no papel. A colisão de malha é de um lado só
(como a casca do Armazém); prédio fechado, parede, laje e muro são caixas. As larguras: a parede fina fica no meio da
célula, então um corredor de duas letras entre duas paredes tem 7,3 m, não 5. Cada mapa tem o seu ambiente sonoro e o seu
som de vez em quando (`Ambience` e `EventSound` em `Maps.cs`).
<!-- mapas de kit: fim -->
## O detalhe das superfícies (mapas de paleta)

Os mapas de kit e o Armazém são pintados por uma paleta: a face tem uma cor lisa e mais nada. Em 20/11 o Jhon
pediu textura mais detalhada no chão, nos metais, nos vidros, "sem tirar a essência". A cor continua a da paleta; por
cima dela entra o **grão do material** de cada face — relevo, aspereza, um claro-escuro leve e manchas largas; o metal
vira metal e o vidro reflete o céu. São quatro peças:

- **`src/assets/mapas/detalhe/superficie.gdshader`** — o material. O grão é projetado pelo mundo (de cima e dos lados): não
  depende da UV da peça e emenda de uma peça para a outra. As tabelas no topo dizem, por material, a camada de textura, o
  tamanho (metros por repetição), a força do relevo e do claro-escuro, a aspereza, o metal. No material de cada kit
  (`src/assets/mapas/detalhe/<kit>.tres`): `intensidade` (0 = o mapa liso de antes, 1 = o padrão), `relevo`, `manchas` e
  `ver_classes` (pinta cada material de uma cor, para conferir).
- **`src/tools/mapas/detalhe_texturas.py`** — desenha as texturas (`detalhe_n.jpg` o relevo e a oclusão, `detalhe_m.jpg` o
  claro-escuro, a aspereza e a altura, `detalhe_c.jpg` a **tinta** — a cor que multiplica a da paleta: o tijolo mais
  alaranjado, o musgo na junta, a ferrugem no risco — e `macro.png`): 20 camadas de 1024 px num mosaico 5 × 4, que o
  Godot importa como Texture2DArray. Desde a tarde de 20/11 **todas são desenhadas ali** (ruído e geometria, sem foto),
  no estilo que o Jhon pediu ("nível Riot, Overwatch", "tirar o aspecto de Roblox"): forma grande e limpa, quina
  chanfrada, cada tijolo, laje ou tábua de um tom, pouco ruído miúdo. O reboco descasca e mostra o tijolo por baixo; a
  telha segue a queda do telhado.
- **`src/tools/mapas/kits/detalhe.py`** — decide **de que material é cada face** e grava no `.glb` (o vermelho da cor do
  vértice). A paleta não diz (o mesmo cinza é asfalto, parede e lataria); a conta é pela peça (`House_01` é prédio,
  `Car_01` é veículo), pela cor (num prédio, o azul-claro é vidro e a cor que mais cobre as faces em pé é a parede) e por
  para onde a face olha (o escuro para cima é telhado; em pé, esquadria). As regras por tipo de peça estão em `PECAS`; o
  Armazém, que vem do Blender em pedaços grandes, usa a caixa de cada peça do `.json` dele (`src/tools/mapas/armazem_estendido.json`; regra `armazem`). São 27 materiais
  (`CLASSES`): asfalto, piso, concreto, reboco, tijolo, metal pintado, metal cru, vidro, madeira, folhagem, areia, rocha,
  tecido, plástico, adobe, terra, louça, papel, borracha, telhado, faixa, lataria, água, calçamento, brita, tronco.
- **Toda face do Armazém tem material** (25/11): uma primitiva exportada sem material o Godot pinta de branco liso, sem paleta nem detalhe (foi a parede branca do bloco do fundo leste). O `remendar` dá o material da paleta a toda primitiva do Armazém, e o teste `ArmazemTodaFaceComMaterial` reprova a que ficar com o material padrão. Se o Blender exportar um modelo novo, rode `detalhar.py` de novo.
- **O `.glb.import`** de cada modelo troca o material embutido pelo `.tres` (`_subresources`, `use_external`): é o que
  `detalhe.ligar` escreve.

Os geradores faziam isso sozinhos (`Montagem.glb` e `src/tools/mapas/armazem*.py`). O `detalhar.py` trabalha direto no `.glb`
que já está no projeto, então continua servindo sem o gerador:

```sh
python3 src/tools/mapas/detalhar.py            # todos; ou: detalhar.py cidade
python3 src/tools/mapas/detalhe_texturas.py    # as texturas (só se mudar uma camada)
```

**Peça com o material errado**: uma linha em `PECAS` (ou em `FIXAS`, se for só ela), `detalhar.py`, e o Godot importa de
novo. **Material forte ou fraco demais**: a linha dele nas tabelas do shader.

O que foi conferido (20/11, com os mapas de então): os seis mapas de paleta renderizados no Godot 4.7 (Forward+) antes e depois, do ângulo da foto e da altura do
jogador; com `intensidade = 0` o quadro sai igual ao de antes. **Não foi medido o custo** no Mac (o material lê de 2 a
6 texturas a mais por ponto) nem rodado `src/tools/testar.sh`.

### O que mudou nos mapas na tarde de 20/11 (pedidos do Jhon)

- **O que fecha o mapa ficou baixo** ("não precisam de prédios, muro alto ao redor, pode ser metade do que já é" — o lobo
  não corre no limite invisível, cai): na Estação os prédios da volta têm um corpo só (10 a 15 m; eram dois, um em cima
  do outro); no Deserto a muralha de fora tem 7,6 m (era 14); na Obra é um **muro de obra** de 3,8 m — pano de tijolo,
  pilar de concreto a cada 5 m e capa de concreto (era de 16 m, com a empena dos vizinhos). O limite invisível
  (`barrar`) continua do chão ao teto.
- **Telhado segura quem sobe**: na Estação os telhados de duas águas, as coberturas da plataforma e a parede alta do
  saguão têm colisão; na Cidade o corpo de cima dos prédios altos; no Deserto todas as tendas.
- **Deserto sem vegetação e sem água**: saíram as palmeiras, as plantas, o poço e o chafariz (no lugar, pedras, potes,
  carga e um estrado de pedra no meio da praça); a rua da feira é calçada de ponta a ponta e o resto é areia, com
  lajotas e blocos soltos. O fundo são dunas largas e redondas (as peças e o painel `serra.png`).
- **Cidade**: as bombas do posto são de chapa pintada (saíam de tijolo); o que ficava a cavalo no meio-fio vai inteiro
  para a rua ou para a calçada (flutuava 14 cm); o ar-condicionado só vai em parede alta e fechada.
- **A capa de cada mapa tem clima de guerra** (`src/tools/fotos_mapas.gd`, tabela `GUERRA`): névoa de poeira, colunas de
  fumaça ao longe, menos cor, mais contraste e vinheta — só na foto. `-- limpa` tira sem isso; `-- rascunho`, pequena.

**Não foi feito ainda**: o chanfro das quinas e os frisos (geometria, no Blender), o interior das janelas, peças novas
(trens, carros) e a planta dos mapas em compartimentos. A textura sozinha não tira o aspecto de bloco das peças do kit.

### Lixo, ar-condicionado e cercas da Cidade (25/11)

Pedido do Jhon, com fotos. Feito direto no `cidade.glb` (os nós de cada peça) e no `cidade.tscn` (as colisões):

- **O lixo (`Garbage_01`) vai pela lama, não pelo fundo**: a peça tem os sacos 18 cm abaixo da lama. Quem puser lixo de novo
  põe a origem da peça no chão (a lama fica em y = 0 da malha), não a caixa.
- **Ar-condicionado só em parede lisa e fechada, encostado**: atrás do aparelho, o meio e os quatro cantos têm de bater na
  mesma parede, a no máximo 30 cm um do outro (rebaixo de janela), e nada de vidro, janela ou letreiro. Ficaram 19.
- **Cerca não fica na rua**: as quatro que estavam de pé no asfalto saíram.

### As calçadas da Cidade

Pedido do Jhon (20/11): "coloque calçadas na cidade, não deixe asfalto e chão se misturarem". O asfalto era uma chapa
3 cm acima da calçada, de parede a parede. Agora são **dois níveis**, no desenho e na colisão (`calcadas`, em
`src/tools/mapas/cidade.py`): a calçada é o nível zero de sempre (prédios, becos e o pátio do posto não mudam), com 1,6 m ao
pé dos prédios (`CALCADA`); o asfalto fica 14 cm abaixo (`MEIO_FIO` — o jogador sobe 0,3 m andando, o bot 0,5) e entre os
dois vai o meio-fio, mais claro (`GUIA`). O chão de colisão (`Piso`) desceu para −0,14 e a calçada tem as caixas dela
(`Calcada…`). O que está na rua desce com ela; o carro ou a barreira que ficaria metade na calçada vai inteiro para a
rua, e a pintura que não cabe no asfalto sai. A rua da Estação de Trem ainda é do jeito antigo.
## Testes

`MapaArmazemEstendido`, `MapaCidade`, `MapaDeserto`, `MapaEstacao`, `MapaObra`, `MapaNeon`: o mapa carrega
na partida, os dois times nascem, ninguém cai do mapa, os bots saem da
base pela malha de navegação e trocam tiro.

## A luz, as escadas e o limite dos mapas de kit (20/11)

- **Luz** — `luz_de_jogo(m, clima, yaw, queda)` em `src/tools/mapas/kits/montagem.py` (a tabela `CLIMAS`: `dia`, `tarde`,
  `deserto`). Sol quente e forte, sombra fria e clara (ambiente azul-violeta que não depende de o céu "ver" o lugar: beco,
  sala e vão de laje ficam legíveis), uma luz fraca sem sombra apontada para cima na direção do sol (a luz que volta do
  chão), SSAO largo + SSIL, ACES com branco 6,5, névoa com perspectiva aérea, sol com tamanho (sombra macia).
  `lampadas(m, [M])` põe uma lâmpada quente, sem sombra, em cada poste do kit. **Sem SDFGI**: testada em foto, apagava
  tudo o que o céu não vê (sala sem lâmpada preta) — o Armazém, fechado, usa. Conferido em fotos Forward+ do mapa com
  personagens parados no sol, no beco e debaixo de laje; **não** conferido dentro da partida (a nuvem não roda o jogo em
  Forward+ em tempo útil).
- **Escadas** — `M.rampa(escada, pé, alto, lados)`: uma chapa inclinada só de colisão por cima dos degraus (nós `pr…` em
  `Colisao`). Com `FloorConstantSpeed` no jogador e no bot, sobe-se na velocidade do chão, sem tranco. Testes
  `EscadaObra`, `EscadaEstacao`, `EscadaDeserto`.
- **Limite** — o muro de fora (o que tem uma `Barreira…` até 30 cm atrás da face) não segura a corrida do lobo
  (`Player.BackedByBarrier`); por isso os prédios que fecham o mapa têm a altura natural do kit. Teste `ParedeInvisivel`.
- **Mata-mata** — cada cena tem o nó `NascerLivre` com 16 `Marker3D` espalhados (`espalhar(m, grade, base)`), para o
  modo todos contra todos, que ainda não existe no jogo (`RoomConfig.ModeAvailable`). A Obra está marcada só mata-mata
  em `Maps.cs`.
- **Estação** — nas pontas das linhas, o muro de arrimo e as bocas de túnel (`Wall_08` do kit, um arco por linha:
  `Kit.recorte`); a rua continua para fora do mapa, interditada; duas torres de relógio, inteiras, na calçada.
- **Obra** — muro de tijolo de 6,2 m; as muretas têm a colisão da própria peça (passa-se pelo vão).

## Distrito Neon (20/11) — o pacote do Unity como kit

O Jhon mandou o `.unitypackage` "Low Poly Sci-Fi City" ("implemente esse mapa no jogo, refaça a luz e as texturas e deixe
ele maior; use LED para iluminar as partes que representam LED; de fundo, prédios bem altos, com letreiros").

- **O pacote vira kit** em três passos (`src/tools/mapas/kits/`): `unity_pacote.py` põe cada arquivo do pacote no caminho dele;
  `unity_fbx.py` (precisa do Blender: `pip install bpy`) lê a malha de cada `.fbx`; `unity_kit.py` junta — um prefab = uma
  peça (301), as quatro texturas e as cores dos materiais lisos num atlas de 2048 px (`src/assets/mapas/kits/scifi.png`), o que
  acende em `scifi_luz.png`, e a cena de demonstração (`src/tools/mapas/LP_SciFiCity_DEMO.json`: a matriz de cada uma das 1.059
  peças). O Unity é de mão esquerda: posição (−x, y, z), rotação (x, −y, −z, w); a malha do `.fbx` entra como está, × 0,01.
- **O mapa** (`src/tools/mapas/neon.py`): a cena sem a fileira oeste, mais a cópia girada 180° encostada ali. Colisão: malha
  nos prédios, caixa nos objetos, rampa nas quatro escadas; o chão é o piso do mapa (5 cm). O pacote não fecha o quarteirão
  em toda a volta: onde o chão livre encosta no vazio vai uma `Barreira…` invisível (78). Essas barreiras saíram em 24/11 (tapavam portas e salas); desde 25/11 o lado de fora do muro da cidade
  inteiro é a `BarreiraMuro` (`muro_da_cidade.gd`, acima).
- **A luz**: céu de noite desenhado (`kits/fundos.py: ceu_noite` → `ceus/noite_neon.hdr`), lua fria com sombra, névoa roxa,
  e os LEDs — `superficie.gdshader` ganhou `luz`/`luz_forca` (preta e zero nos outros mapas: nada muda neles); `leds()` põe
  uma lâmpada da cor de cada letreiro grande (44) e 22 postes.
- **Material de cada face**: a regra `scifi` em `kits/detalhe.py` olha em que quadrante do atlas a face cai.
- **O que foi conferido**: fotos Forward+ do mapa (de cima, da rua, da avenida), a grade de onde se anda, as rotas e os pontos
  de nascer; as quinas dos degraus em cima da chapa das escadas. Depois entrou o teste `MapaNeon` (carrega, nasce, os bots
  saem da base). **Não conferido**: o custo das 66 luzes em máquina fraca.

### A Obra, o som e as capas (20/11, noite)

- **Obra** (`obra.py: canteiro`): montes (`a` areia, `p` brita na planta), lama, poças (classe `agua`) e tábuas.
- **Som**: `src/tools/sons/ambiente_mapas.py`; o fundo e o som de vez em quando de cada mapa estão em `Maps.cs`.
- **Capas**: `src/tools/fotos_mapas.gd` (`CAPA` e `GRADE`): cor viva e desfoque leve embaixo e nos cantos.

### Depois de jogar (27/11, fim do dia)

- **Obra de dia** (Jhon: "mapa da construção pode ser de dia, não em pôr do sol"): `luz_de_jogo(m, 'dia', 62, 48, …)`
  e o mar de nuvens branco, de sombra azulada (`NUVENS`, em `obra.py`). Capa refeita.
- **Estação: as plataformas de 50 cm** (as placas `Floor_06`, `Floor_07`, `Floor_08`; a `Floor_08` tem quatro degraus
  de 12 a 15 cm em cada ponta) tinham colisão de **caixa** — a regra das placas de chão —, e a caixa era uma parede de
  55 cm no lugar dos degraus. São `DEGRAUS` em `estacao.py`: colisão da malha.
  **Peça de chão com altura não é caixa**: conferir a altura da peça antes de pô-la em `CAIXA`.
- **Obra sem parede na beira** (Jhon: "eu posso cair, não precisa ter a colisão nos limites do mapa"): `fechar(...,
  margem=BEIRA[0] - X, paredes=False)` — o chão invisível vai só até a beira da laje, sem `Barreira…`. Quem passa da
  beira cai e morre abaixo de `Player.FallLimit` (−25 m), e o corpo continua caindo. As paredes `Limite…` existem, mas
  **sem colidir** (camada 0, 27/11): só dão à câmera de espectador a caixa do mapa (`MapBounds`) — sem elas a caixa era
  toda a colisão, prédio incluído, e a câmera saía do mapa e nascia dentro do prédio. Num mapa de beira aberta novo,
  `fechar(..., paredes=False)` já faz isso.
- **Obra: as tábuas colidem** (`WoodenPlank`, menos a deitada no chão): a ponte do segundo piso e as de cima da viga
  da grua eram só desenho.
