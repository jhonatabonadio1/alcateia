# Guia de animação de armas (primeira pessoa)

Para quem for animar a próxima arma — gente ou IA. Junta o que foi decidido,
medido e errado nas armas de 19/11 (M4A4, AK-74, SCAR-H, SVD, Desert Eagle,
faca, granada V40). Onde aparece uma frase entre aspas, é pedido do Jhon.

O AR-15 (`src/assets/armas/fp/ar15.glb`) é a referência de qualidade: foi animado
à mão no Blender. Tudo o mais é montado por programa em cima do esqueleto dele
(`src/tools/armas/conjuntos.py`) e tem de parecer feito pela mesma mão.

---

## 1. As regras

São critérios de aceite. Uma animação que quebra uma delas não vai para o jogo.

1. **Nada atravessa, tudo encosta.** "Nada pode atravessar os armamentos, mas
   deve encostar sempre." Pele da mão a menos de ~1 mm da arma e nunca do lado
   de dentro. Vale para dedo × arma, dedo × argola, mão × mão, carregador ×
   punho. Mede-se na pele das mãos do jogo, não no osso (seção 6).
   **Nos fuzis a régua é a M4A4**, que o Jhon aprovou ("tá perfeita"): as mãos
   do jogo são mais gordas que as do AR-15 original e, na M4A4, a pele entra
   até ~1 cm no guarda-mão sem que se veja. As outras armas repetem esse
   mesmo encosto, falange por falange (`pegada.pegar_como`); o que não pode é
   dedo no ar ou ponta de dedo sobrando acima da arma.
2. **Mão de gente.** "Igual uma mão humana, sem física mal feita." A mão fecha
   em volta da coisa junta por junta até encostar; não é uma pose de punho
   genérica esticada para caber. O indicador fica no gatilho nas armas de
   fogo. "Cuidado com as articulações da mão."
3. **O pulso não quebra.** A mão em linha com o antebraço. Em gesto desenhado,
   até 40° (`pulso_reto`); em arremesso e golpe largo, a mão sai **do braço**
   (`mao_pelo_braco`) e a dobra é a que se pediu (o "chicote", até ~30°).
   "Punho de lançamento torto" foi mão posada com o braço se virando depois.
4. **Fluido, com movimentos combinados.** Um gesto de várias chaves usa
   `Curva` (passa pelas chaves sem parar); `Chaves` para em cada chave e fica
   robótico. As duas mãos se movem ao mesmo tempo (a esquerda já vem enquanto
   a direita apresenta), e o fim de um movimento é o começo do outro.
5. **A mira é parada.** "Precisa de precisão, igual à AR-15, ele mexe muito
   pouco." Mirando, a arma anda décimos de milímetro; o coice fica quase todo
   na câmera do jogo, não na arma.
6. **A câmera acompanha a arma** no saque, na recarga, no tiro e no golpe —
   pouco. Mirando e armando a granada, a câmera fica parada.
7. **Só aparece o que deve.** Nada de braço passando na frente da câmera, nada
   de ver a arma por dentro, nada de carregador fora do lugar.
8. **Tamanho real, salvo quando some.** As armas têm o tamanho de verdade; a
   granada é 18% maior na primeira pessoa porque sumia na mão.

---

## 2. Como é montado

| Peça | Arquivo |
|---|---|
| Modelos convertidos (malha, cores, peças, ossos de origem) | `src/tools/armas/reais.py`, `src/tools/armas/blender/reais.py`, `src/assets/pacotes/reais/` |
| O construtor dos conjuntos | `src/tools/armas/conjuntos.py` |
| Esqueleto, IK, poses de dedos, `Chaves`, `Curva`, `mola` | `src/tools/armas/esqueleto.py` |
| A mão fechando em volta de um objeto, medida na pele | `src/tools/armas/pegada.py` |
| Clipes de mira (mirar, mira, mira_andar, mira_tiro, desmirar) | `src/tools/armas/mira.py` |
| Conferência por números | `src/tools/armas/conferir.py` |
| A ficha que o jogo lê | `src/assets/armas/fp/bracos.json` |
| Quem toca os clipes no jogo | `src/scripts/ui/ViewModel.Rig.cs` |

```
python3 src/tools/armas/conjuntos.py m4a4 ak74 scar svd deagle faca choque --ficha src/assets/armas/fp/bracos.json
python3 src/tools/armas/conferir.py m4a4 ak74 scar svd deagle faca choque
```

Depois de montar, reimportar no Godot (`--headless --import`) antes de olhar:
sem isso o Godot mostra o `.glb` antigo e a foto engana.

**Espaços.** *Arma*: origem no punho, cano para −Z, Y para cima, metros.
*Olho*: X direita, Y cima, −Z para a frente, metros. *Raiz*: o do esqueleto,
centímetros (`E.O2R`, `E.R2O` convertem). O conjunto entra no jogo com escala
0,45 e a câmera corta a 1,5 cm: **3,3 cm no espaço do conjunto**.

**Clipes que o jogo conhece** (nome na ficha → quando toca):

| Clipe | Quando |
|---|---|
| `parado`, `andar` | descanso, em laço |
| `sacar`, `guardar` | troca de arma |
| `tiro` | cada disparo |
| `recarga`, `recarga_vazia` | recarga com e sem bala na câmara |
| `mirar`, `mira`, `mira_andar`, `mira_tiro`, `desmirar` | botão direito |
| `coronhada` | tecla V (chave `contato`: o instante do golpe) |
| `golpe`, `golpe2` | faca: alternam a cada clique (chave `contato`) |
| `inspecionar` | tecla F, ou sozinho depois de 9 s parado; andar, mirar, atirar ou trocar interrompe |
| `armar`, `armado`, `arremesso` | granada: botão preso, laço enquanto segura, soltar (chave `soltar`: o instante em que sai da mão) |

Clipe que não está na ficha não toca. A faca e a pistola já tiveram
`inspecionar` no arquivo sem ninguém tocar: "o jogo só tá usando 2 animações".

---

## 3. Por tipo de arma

### Fuzil (M4A4, AK-74, SCAR-H, SVD)

- A arma entra alinhada à **mão do AR-15**: a linha da frente do punho, o
  fundo do guarda-mato e o ponto do indicador são os mesmos (`reais.ARMAS`,
  campo `t`). A M4A4 é a referência do que está certo ("tá perfeita").
- O indicador vai ao gatilho por IK (`Maos.no_gatilho`), resíduo 0 mm.
- A mão esquerda fica no guarda-mão da arma, não onde ficava o do AR-15
  (`LONGAS[...]['apoio']`), e **fecha nele**: `LONGAS[...]['fechar']` liga
  `fechar_no_guarda_mao`, que leva a palma e cada falange à distância que
  têm na M4A4. Com os dedos do AR-15 num guarda-mão mais baixo (AK-74, SVD),
  as pontas ficavam no ar, acima da arma.
- O ferrolho cicla no tiro; o carregador e as balas são peças com osso.
- **Recarga**: o gesto é o do AR-15, com a força de cada arma. Os ombros e os
  cotovelos são os do animador naquele instante (`corpo=`): com os da pose
  parada, o cotovelo esquerdo passava na frente da câmera quando a mão ia
  buscar o carregador — os "resquícios do movimento onde não deveria ver".
- **Sniper**: a luneta sobe ao olho e a tela corta para a mira redonda branca
  em fundo preto, com zoom (qualquer arma da classe Sniper).

### Pistola (Desert Eagle)

- Tiro, recarga, corrida e inspecionar vêm da pistola de referência
  (`src/tools/armas/dados/glock.npz`); a coronhada é de uma mão, com a lateral.
  O tiro da referência entra com 40% da força ("pode mexer bem menos quando
  atira").
- **O andar é o do AR-15**, igual ao da M4A4 (0,8 s, mesma força, mesma
  câmera): "ao andar não precisa mexer muito para os lados, faça igual à
  animação da AR-15". O andar da referência da pistola, mesmo a um terço,
  levava a arma 15 mm de um lado ao outro e a girava 6°; o do AR-15 são
  5 mm e meio grau. **Regra para arma nova**: o andar parte do do AR-15.
- **As duas mãos**: a esquerda abraça a direita sem uma passar por dentro da
  outra (`abracar`): a mão direita e o punho viram um sólido, a palma
  esquerda encosta de lado e cada dedo dobra até tocar. O antebraço esquerdo
  se alinha à mão (`braco=('l',)`); sem isso o pulso dobrava 78°.
- **O carregador tem caminho próprio**: sai e entra **reto, pelo eixo do
  punho**, e só fora dele a mão o leva. A mão esquerda vai presa nele. Preso
  ao gesto da mão da referência ele pulava para cima da arma e atravessava o
  punho ("cartuchos em posição estranha e incorreta").
- Na recarga a arma sobe 6 cm: com o punho na borda da tela, a troca
  acontecia fora dela.
- Parada: `LUGAR`. Mirada: mais perto e mais baixa que a linha de mira
  (`MIRAS`, `BAIXO`).

### Faca

- Os movimentos são os do arquivo de referência (`dados/faca_movimento.npz`):
  parado, estocada, corte, inspecionar, saque. A mão segue o pulso do
  arquivo; o braço se ajusta à mão (`antebraco_pela_mao`), não o contrário.
- Golpe curto e rápido: 0,40 s entre golpes, **um clique, um golpe** — o
  clique dado no meio do golpe fica guardado e sai quando ele acaba.
- **Quatro golpes, um a um**: estocada (`golpe`), corte (`golpe2`), estocada
  alta (`golpe4`), corte deitado (`golpe3`). O arquivo só tem os dois
  primeiros; os outros são os mesmos com a mão direita girada em torno de um
  eixo do olho, em sino (`girado`). Havendo arquivo com mais golpes, trocar.

### Granada (V40)

- **A peça**: a bola com a espoleta; a **alavanca**, que desce por um lado; o
  **pino**, que atravessa a espoleta; a **argola** do pino, do lado oposto.
- **Na mão**: em pé, a alavanca na forquilha do polegar (é onde a mão a
  aperta), a argola para a esquerda. A mão fecha em volta da bola com
  `pegada.pegar`. Antes: "o gatilho dela tá entrando dentro da mão".
- **Armar**: a direita deita a granada e vira a argola para a outra mão; o
  indicador esquerdo, esticado, entra na argola **de ponta, pelo eixo do
  aro**; o arame assenta no dedo; ele puxa — a argola vira para o dedo e
  arrasta o pino pelo furo (a conta de uma biela); o pino sai e vai embora
  pendurado no dedo. "Ele deve puxar com os dedos, ele tem uma alça."
- **Armado**: enquanto o botão está preso, a mão respira lá atrás, na tela.
- **Arremesso**: o braço pelo pulso, um arco só, solta na frente, o pulso
  termina o chicote, os dedos relaxam meio curvos (não a mão espalmada).
  **A mão entra na linha da cruz antes de soltar e solta em cima dela**:
  "a granada tem que ser lançada no meio da linha da mão, na direção exata
  do crosshair". No `soltar` o meio da granada está a 0 mm da linha da cruz
  para o lado e 7,5° acima dela (para não a tapar), com a mão indo reto
  para a frente (a 1° da linha), e ela ainda acompanha o lance 6 cm antes
  de cair. As chaves do arco marcadas `'meio'` em `GRANADA_ARCO` dão a
  posição **do meio da granada**, não do pulso; o programa imprime onde
  ela está ao soltar. Antes a mão soltava 11 cm à direita, cortando a tela
  em diagonal, e a granada parecia sair de lado, por cima da mão.
- No jogo a granada é **explosiva** e cai **onde a cruz aponta** (até 19 m).

#### O lançamento (a granada que voa)

"A granada deve sair da mão fazendo um pêndulo baseado na força, distância
e mira"; "granada vai na direção do crosshair".

- **Sai da mão**: soltou o botão, o braço lança; a granada de verdade nasce
  no instante `soltar` do clipe `arremesso` (0,11 s), **no lugar em que a da
  mão está na tela** — o conjunto é desenhado a 45% perto da câmera, então
  o ponto é levado para a distância de verdade pela mesma linha do olho
  (`ViewModel.HandPoint`, com o `centro` da ficha: o meio da granada no osso
  `Bolt`). Uma some e a outra aparece no mesmo ponto da tela. A física só
  anda com a granada um ou dois passos depois de ela nascer: até ela sair
  do lugar, a da mão continua à vista e a de verdade fica escondida
  (`ViewModel.UpdateThrown`) — sem isso eram dois quadros sem granada.
- **Vai para a cruz**: a velocidade é calculada da mão até o ponto que a
  cruz aponta nesse instante (`Weapon.ThrowVelocity`). A câmera fica parada
  desde o armar até a mão soltar — a animação não pode mexer na cruz
  enquanto se aponta (com ela mexendo, o erro a 12 m ia de 0,18 para 0,51 m).
- **O arco depende da distância**: perto (`ThrowLoftNear`, 9° acima da
  linha até o ponto) é um lance quase direto, que sai junto da cruz; no
  alcance máximo (`ThrowLoft`, 30°) é o arco alto. A força sai da conta:
  ~8 m/s a 3 m, ~12 a 10 m, 14,5 (o teto, `ThrowForce`) a 19 m. Sem alcance
  com esse arco, sai com toda a força no ângulo que chega; nem assim, no
  ângulo que vai mais longe. Mirando o céu: ~18 m.
- **A mão acompanha a força**: o clipe toca de 0,82× (perto) a 1,1× (longe).
- Colado numa parede, a granada nasce do lado de cá dela. Sem mão animada
  (bots, bonecos de rede) ela nasce na hora, 0,8 m à frente da câmera.
- **Ao mexer no `arremesso`**: no `soltar` o meio da granada fica em cima
  da linha da cruz (0 mm para o lado), à vista e sem tapar a cruz; mudou o
  tempo, muda a chave `soltar` da ficha. O teste `Granada` cobra: nasce a
  menos de 3 cm da linha, sai a menos de 1° dela e voa em cima dela.

---

## 4. A mira

- **Distância do olho** (`MIRAS`): a mira de trás fica o mais perto que dá
  **sem a arma ser cortada pelo plano da câmera dentro da tela**. Na AK-74, a
  21 cm via-se a arma por dentro; de 27 cm em diante, não. Conferir sempre
  (`conferir.py`, linha "na mira": tem de dar 0).
- **Linha de mira** (`reais.ONDE`: `alca`, `massa`): passa por onde o olho
  passaria de verdade. Na AK-74 a ponta do pino da massa fica 3 mm abaixo das
  orelhas: medindo pelo topo, o pino ficava escondido atrás da alça. "A mira
  da AK deve poder enxergar a mira pequena da frente."
- **Campo livre**: se a mira de trás tapa o alvo, **afastar** (`MIRAS`) e
  passar a linha de mira **por cima** da alça, na ponta do pino da massa. Foi
  o que resolveu a M4A4 (14 cm). Baixar a arma (`BAIXO`) não resolve: 3 mm
  puseram o centro da tela atrás do topo da alça ("tá tampando").
- **Estabilidade**: balanço na mira ≈ 0,07 mm parado e 0,2 mm andando; o
  coice na mira é fração do do tiro (`COICE_NA_MIRA`, M4A4 0,26).

---

## 5. Erros que já custaram retrabalho

| O que se viu | A causa | O que resolve |
|---|---|---|
| Pulso dobrado a 90° | mão posada, braço resolvido depois | `pulso_reto`; em arremesso, `mao_pelo_braco` |
| Braço vindo de cima | mexer no ombro para endireitar o pulso | a mão cede, o ombro fica |
| Punho fechado esquisito | dedos fechados além da pose (extrapolar) | fechar até encostar (`pegada`) |
| Alavanca dentro da mão | pose de cabo de arma numa bola | `pegada.pegar` |
| Dedo atravessando a argola | mão em punho ao lado do aro: polegar e médio cruzam o arame | indicador esticado, de ponta pelo eixo |
| Pino flutuando na tela | peça solta sem dono | presa à mão que a leva; depois, fora da tela |
| Carregador pulando | peça presa ao gesto da mão da referência | caminho próprio, reto pelo eixo |
| Vulto escuro na recarga | cotovelo da pose parada | ombros e cotovelos do animador (`corpo=`) |
| Ver a arma por dentro | olho perto demais na mira | `MIRAS` maior; conferir o corte |
| Massa escondida | linha de mira medida pelo ponto errado | `reais.ONDE` |
| Granada voando torta | peça calculada com a arma de antes do ajuste do pulso | `com_pulso` antes de calcular |
| Gesto robótico | `Chaves` (para em cada chave) | `Curva` |
| Foto diferente do que se montou | Godot sem reimportar | `--import` antes da foto |
| Clipe pronto que não toca | fora da ficha ou sem gatilho no jogo | ficha + `ViewModel.Rig.cs` |

---

## 6. Conferir antes de entregar

O construtor imprime as medidas; `conferir.py` repete para qualquer conjunto.

| Medida | Tem de dar |
|---|---|
| Pele da mão × arma (granada: impresso por dedo) | entre 0 e ~1 mm; nunca negativo |
| Aro × dedo, mão esquerda × direita (granada) | nunca negativo |
| "alcance que faltou" | 0,0 mm |
| Pulso no gesto desenhado | ≤ 40° |
| Braço esquerdo passando a menos de 15 cm da câmera | 0 quadros |
| Pontos da arma cortados na tela, na mira | 0 |
| Boca do cano fora do eixo, na mira | < 0,5 mm |

Testes de regressão que cobrem os conjuntos: `BracosNaMao`, `SacaEGuarda`,
`MiraDaArma`, `MaosPadrao`, `Coronhada`, `ZoomNaMira`, `MunicaoFinita`,
`Granada`, `GranadaNaMira`, `GranadaNaCruz`.

**O que os números não dizem**: se está bonito. Depois dos números, olhar a
sequência do olho do jogador e de perto, de dois lados. E o que foi conferido
fora do jogo (fotos do conjunto numa cena vazia) tem de ser dito como tal: só
o Jhon vê na partida.

**Ainda deve** (medido em 19/11): nos fuzis, o pulso passa de 40° no saque, no
guardar e na coronhada (50–78°), que são os gestos emprestados do pacote de
referência; na pistola, a mão esquerda ainda dobra o pulso no inspecionar e
na coronhada; a mão direita nos punhos (AK-74, SCAR-H, SVD) usa a pose do
AR-15, só com o indicador levado ao gatilho; e a SCAR-H mira por dentro do
anel da alça, que é grande na tela; na granada, a que voa é
menor na tela que a da mão (a da mão é 18% maior e o conjunto é esticado para
a câmera do jogo) e nasce sem a rotação da da mão — a troca acontece atrás
dos dedos, num quadro —, e a bola de colisão dela tem 14 cm de raio, então o
primeiro toque é até 14 cm antes do ponto.
