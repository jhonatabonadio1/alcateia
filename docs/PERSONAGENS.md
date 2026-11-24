# Personagens

Como pôr um personagem novo no jogo — humano e lobo — com as mesmas
proporções dos que já existem, para as animações servirem sem refazer
nada e a cabeça que leva tiro ficar no mesmo lugar.

Resumo: **uma pasta por personagem em `src/assets/personagens/`**, com o
humano, o lobo e um `personagem.tres` com os números. O jogo acha a
pasta sozinho; o teste `Personagens` reprova o que sair da medida.

---

## 1. A pasta

```
src/assets/personagens/
└── eve/                  o nome da pasta vira o id: pers_eve
    ├── personagem.tres   nome, time, descrição, raridade, vida
    ├── humano.fbx        o corpo humano (Mixamo)
    └── lobo.glb          o lobo (Meshy), já preparado
```

O nome da pasta é minúsculo e sem acento (`eve`, `kael`, `luna_negra`):
ele vira o id do item (`pers_eve`) e fica salvo no perfil de quem usa.
**Não renomeie a pasta depois de lançado** — quem tinha o personagem
equipado volta ao padrão.

Os arquivos que o Godot cria na importação (`humano_0.png`,
`lobo_Image_0.jpg`, os `.import`) ficam na mesma pasta e vão juntos no
commit.

## 2. O que vai no `personagem.tres`

Copie o da Eve e troque. No editor do Godot ele abre como um recurso
com três grupos.

| Campo | Exemplo | O que faz |
|---|---|---|
| `DisplayName` | Eve | O nome no inventário |
| `Description` | Pirata do espaço… | O texto do cartão |
| `Team` | 2 | 1 azul, 2 laranja, 0 os dois times |
| `Rarity` | 3 | 0 comum … 4 lendário (a cor da moldura) |
| `HumanModel` | `res://src/assets/personagens/eve/humano.fbx` | O corpo humano |
| `WolfModel` | `res://src/assets/personagens/eve/lobo.glb` | O lobo |
| `HpMultiplier` | 1,1 | Vida, nas duas formas: 200 × isto |

**A vida é a mesma como humano e como lobo** (`FormController.HumanHp`,
200); o lobo soma o escudo da classe por cima. A classe de lobo (Bruto,
Ligeiro) continua sendo escolha à parte: o personagem muda o corpo e a
vida, não a velocidade, o pulo nem a garra.

Cuidado com a vida — ela mexe em quantos tiros matam:

| Vida | × | AR-15 no corpo (54) | Garra do Bruto (120) |
|---|---|---|---|
| 200 | 1,0 | 4 tiros | 2 golpes |
| 216 | 1,08 | 4 tiros (o limite) | 2 golpes |
| 220 | 1,1 | **5 tiros** | 2 golpes |
| 240 | 1,2 | 5 tiros | 2 golpes (o limite) |
| 241+ | 1,21+ | 5 tiros | **3 golpes** |

A regra de design 3 do `CLAUDE.md` vale aqui: vida a mais é vantagem
de quem tem o personagem. Hoje todo personagem vem com o perfil (de
graça, `Starter`); quando personagem tiver preço, a economia decide.

## 3. O humano

**Fonte: Mixamo** (ou qualquer modelo com o esqueleto do Mixamo).

1. No Mixamo, escolha o personagem (ou suba o seu e passe pelo
   auto-rig) e baixe em **FBX Binary, "T-pose", com a malha ("With
   Skin")**. Não precisa de animação: o jogo usa os clipes do X Bot em
   `src/assets/personagens/_comum/humano/`.
2. Ponha o arquivo na pasta como `humano.fbx`.
3. Meça:

   ```
   src/tools/personagem.sh medir src/assets/personagens/<nome>/humano.fbx
   ```

   Precisa mostrar o esqueleto `mixamorig` com 65 ossos ou mais (com
   dedos) e nenhuma linha "falta o osso".
4. **Leve à altura do jogo, 1,79 m.** O modelo do Mixamo vem em
   qualquer altura (a Eve veio com 1,39 m). Não escale no Blender —
   reexportar FBX do Mixamo pelo Blender estraga a orientação dos ossos.
   A escala vai na importação do Godot:

   ```
   src/tools/personagem.sh escala src/assets/personagens/<nome>/humano.fbx
   ```

   Ele diz o número; abra o Godot uma vez (para criar o
   `humano.fbx.import`) e troque nele `nodes/root_scale=1.0` pelo número
   (a Eve usa 1,2868). No editor é o mesmo campo: aba Importar, *Root
   Scale*, Reimportar.

**Por que 1,79 m:** é a altura da Ely e do Exo Gray, e a cápsula, a
câmera e a cabeça foram medidas com eles. A cabeça que leva tiro é a do
modelo que se vê (`HeadHitbox`): um personagem mais baixo seria mais
difícil de acertar na cabeça.

**Por que os clipes servem:** são do X Bot, e o `CharacterAnimator`
ajusta o quadril de cada clipe à altura do quadril do modelo na
importação (`MatchHips`). Com a mesma altura total, as pernas ficam
proporcionais e o pé não flutua nem afunda.

### As animações com arma (Blender)

Em terceira pessoa o corpo toca, para cada arma (ak74, ar15, m4a4, scar,
svd, deagle, faca, granada), um clipe para cada clipe da primeira pessoa
(parado, andar, tiro, recarga, sacar, a mira, os golpes, armar e lançar),
feito no Blender para aquele personagem: o tronco, a cabeça, os braços e
cada dedo, com a arma da primeira pessoa na mão. Personagem novo precisa
dos dele:

```
python3 src/tools/personagens/blender/animacoes_armas.py <pasta>                 # todas as armas
python3 src/tools/personagens/blender/animacoes_armas.py <pasta> ar15 --clipes recarga,mira \
    --fotos /tmp/anim --quadros 0,0.5 --vistas frente,lado,perto --sem-gravar   # fotos para conferir
```

Grava `src/assets/personagens/<pasta>/animacoes/<arma>.json` e diz, de cada
clipe, o pior quadro: a mão que não chegou, o punho (dobra, lado, giro),
o dedo dentro da arma, a junta fora do que dobra e a palma longe da arma.
As regras de cada arma estão no começo do script (`POSTURA`, `FACA`,
`GRANADA`, os limites das juntas). Sem a pasta `animacoes/` o personagem
segura a arma pela pose parada de antes (`poses_armas.py`,
`poses_armas.json`). O teste `BonecoMaos` confere que o jogo põe as mãos
e a arma onde o Blender pôs, e que o cano vai para a mira.

## 4. O lobo

**Fonte: Meshy**, a partir do mesmo lobo de base (o lobo tático do
Jhon). O que importa é o esqueleto: **24 ossos `mixamorig` sem dedos,
com o osso extra `headfront`** — é o esqueleto em que o Jhon animou o
`lobo_anim.glb`. Com outro esqueleto as animações não encaixam.

1. No Meshy, gere a partir do lobo de base (mesma silhueta e proporção:
   corpo curvado, braços longos) e baixe o **modelo rigado**, em GLB ou
   FBX.
2. Prepare:

   ```
   src/tools/personagem.sh lobo ~/Downloads/<lobo>.glb src/assets/personagens/<nome>
   ```

   Isso, no Blender:
   - tira o que não é a malha presa ao esqueleto;
   - tira as animações que vêm junto (o jogo usa as do `lobo_anim.glb`);
   - leva a **1,66 m** com o pé no chão (a altura do lobo do jogo);
   - reduz as texturas para 2048 px e grava em JPEG pelo `sips` do
     macOS (o JPEG que o Blender grava o Godot recusa);
   - salva `lobo.glb` na pasta.
3. Confira:

   ```
   src/tools/personagem.sh medir src/assets/personagens/<nome>/lobo.glb
   ```

   Altura 1,660 m, 24 ossos, texturas 2048, nenhuma animação.

**Por que as animações ficam iguais:** os clipes do `lobo_anim.glb` são
do lobo de base, e o `WolfOwnClips` os encaixa no corpo pelo nome do
osso e pela altura do quadril. Mesmo esqueleto e mesma altura = mesma
passada, o pé sem patinar e a garra acertando onde acerta hoje. Se a
silhueta mudar muito (pernas mais curtas, tronco mais longo), a passada
muda: rode `src/tools/medir_lobo.sh` e compare a patinação com a do lobo de
sempre (veja `src/assets/personagens/_comum/lobo/LEIA-ME.md`).

## 5. Conferir no jogo

```
src/tools/testar.sh Personagens
```

O teste abre cada pasta e reprova se:
- o personagem não estiver no catálogo;
- a vida passar de ×0,5 a ×2;
- o topo da cabeça do humano estiver a mais de 4 cm do da Ely, ou o do
  lobo a mais de 4 cm do lobo de sempre;
- faltar um osso que as animações, a arma na mão ou a cabeça usam;
- o lobo não tiver os 24 ossos;
- o corpo montado não usar os dois modelos e a vida do personagem.

Depois, no jogo: Inventário → aba **Personagem** (no lado do equipado)
→ escolha → entre numa partida **no time do personagem**. O detalhe do
item mostra o humano e o lobo girando, para ver a textura.

## 6. Antes do commit

- A linha nos créditos (`src/scripts/meta/Credits.cs`), no mesmo commit.
  Personagem do Mixamo cai na linha do Mixamo; lobo do Meshy, na do
  Meshy — confira se já está coberto.
- O `CHANGELOG.md`.
- Personagem do Mixamo não se redistribui como arquivo cru: se o
  repositório ficar público, os `.fbx` saem dele (veja `CLAUDE.md`).

## Como funciona por dentro

| Peça | O quê |
|---|---|
| `src/scripts/forms/CharacterSkin.cs` | O recurso (`personagem.tres`) e a lista das pastas |
| `src/scripts/meta/Catalog.cs` | `WithCharacters`: um item `Character` por pasta |
| `src/scripts/core/Profile.cs` | `BlueCharacter`, `OrangeCharacter`: o escolhido de cada time |
| `src/scripts/actors/player/Player.cs` | `_EnterTree`: com o time já certo, passa os modelos e a vida aos filhos antes do `_Ready` deles |
| `CharacterAnimator.ModelOverride`, `WolfVisual.ModelOverride` | O modelo do personagem no lugar do modelo do time |
| `src/scripts/lobby/InventoryScreen.cs` | A aba Personagem e o equipar |
| `src/tests/regression/CharacterTests.cs` | O teste `Personagens` |
| `src/tools/personagem.sh`, `src/tools/personagens/` | Medir, preparar o lobo, escala do humano |

**Online:** a vida vale (cada jogo decide a própria vida e manda o
máximo no status), mas os outros jogadores ainda te veem com o
personagem padrão do time — a escalação do servidor não leva o
personagem (pendência em `PENDENCIAS.md`). Os bots usam sempre os de
sempre.
