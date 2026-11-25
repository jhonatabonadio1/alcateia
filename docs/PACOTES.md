# Pacotes de arte

De onde vem a arte que não é feita por código: texturas, objetos de
cenário, céus, personagens, braços e armas. Tudo com licença que permite
uso comercial.

A matéria-prima fica em `src/assets/pacotes/`, **fora do git e fora do
Godot** (`.gdignore`): é grande e dá para baixar de novo. O que o jogo
usa de fato é copiado de lá para as pastas do jogo, essas sim no git.

```
src/assets/pacotes/
  polyhaven/texturas/<id>/   cor, normal e AO+aspereza+metal
  polyhaven/modelos/<id>/    glTF com as texturas
  polyhaven/ceus/            HDRI: o céu que ilumina o mapa
  reais/                     as armas de 19/11 (os .fbx e o .blend em origem/, um .npz por arma)
  infima/                    os .uasset da recarga do AR-15
  sketchfab/                 os .glb baixados à mão
```

## 1. Poly Haven — à mão, pelo site

[Poly Haven](https://polyhaven.com) é fotografia e escaneamento de
coisa real, tudo **CC0** (sem crédito obrigatório). A página de cada
item é `https://polyhaven.com/a/<id>`.

O `baixar_pacotes.py`, que baixava tudo de uma vez (57 texturas, ~85
objetos, 6 céus), e o `mapas/texturas.py`, que copiava as texturas para
o jogo, **saíram em 24/11**: a lista deles era a da Vila e da Estação
Polar, que saíram junto (estão no histórico do git). O que já veio
continua em `src/assets/pacotes/polyhaven/`. Para trazer um item novo,
baixe pelo site e copie para a pasta do jogo com o nome que a cena usa.

## 2. Sketchfab — à mão (precisa de login)

O Sketchfab só deixa baixar logado (a conta é grátis). Em cada página:
**Download 3D Model → GLB** (se perguntar o tamanho da textura, 2K) e
salve o arquivo em `src/assets/pacotes/sketchfab/`, com o nome que vier.
Lembre: é essa pasta (fora do Godot); em `src/assets/sketchfab/` o Godot
tenta importar centenas de MB.

A maioria é **CC BY 4.0**: uso comercial liberado, **com crédito** ao
autor na tela de créditos. Licenças diferentes de CC BY/CC0
(NonCommercial, "Free Standard" restrita a certos usos) ficam de fora;
a exceção é a P9, abaixo, que é só referência de movimento.

O que se usa hoje do Sketchfab:

| O quê | Modelo | Autor | Licença |
|---|---|---|---|
| Faca | [knife animated](https://sketchfab.com/3d-models/knife-animated-e5db6b73878f4123baa48cea6d9af84c) — braços, faca e animações; trazida pelo Jhon em 13/11, pasta `src/assets/sketchfab/knife-animated/`. A faca do jogo é a dele: `src/tools/armas/dados/faca.npz` (tirada do `faca.glb` de antes), posta no esqueleto das outras armas pelo `reais.py` | DJMaesen (bumstrum) | CC BY 4.0 |
| Movimento da pistola (pegada, saque, recarga) | P9 Manny FPS Animations (trazido pelo Jhon em 23/11) — só os ossos, quadro a quadro (`src/tools/armas/dados/p9.npz`, por `src/tools/armas/blender/p9.py`); vai para a Desert Eagle por `RefP9` em `src/tools/armas/conjuntos.py`. O `.glb` fica em `src/assets/pacotes/sketchfab/p9-manny-fps-animations/` | Blend_GUY | Sketchfab Standard |

**Os braços animados do Cransh e as armas da primeira leva saíram.** Os
seis conjuntos (fuzis, AK-74m, SMG, pistola, Benelli M4, Remington) e os
modelos avulsos (M16A2, SG552, AK-47, UMP45, Benelli M3, Beretta M9, a
M84) eram das armas antigas, que deixaram o jogo; o conversor deles
(`armas/blender/bracos.py`) saiu em 24/11. Os `.glb` ainda estão em
`src/assets/pacotes/sketchfab/`, fora do jogo. Autores, para quem os
trouxer de volta: Cransh (animações), bumstrum, DJMaesen e teenjust500
(braços), doomsentinel, creationwasteland, eNse7en, raimeiyonke,
haoliu95, tris09, user77, TastyTony, Drus, Alpaca Studio, Amapsis,
emran.bayati e Coozy — todos CC BY 4.0.

## 3. As armas de 19/11 (`reais/`)

O Jhon trouxe em 19/11 (`Archive 2.zip`) cinco `.fbx` e um `.blend`:
M4A4, AK-74, FN SCAR-H, SVD Dragunov, Desert Eagle e a granada V40.
`src/tools/armas/blender/reais.py` (Blender) deixa um `.npz` por arma em
`src/assets/pacotes/reais/`; daí em diante é só Python:

    python3 src/tools/armas/reais.py       # os modelos (src/assets/armas/<id>/) e armas.json
    python3 src/tools/armas/conjuntos.py   # a primeira pessoa (src/assets/armas/fp/<id>.glb)
    python3 src/tools/armas/peles.py       # as épicas e lendárias

**A licença destes modelos ainda é do Jhon confirmar** (veja
`docs/PENDENCIAS.md`).

## 4. O AR-15 (o fuzil padrão): pacote da InfimaGames

[FPS Tutorial Blender Starter Files](https://infimagames.gumroad.com/l/fps-tutorial-blender-starter-files),
da InfimaGames (David Stenfors), **CC BY 4.0** ("You're free to use these
files for both personal use AND commercial. All we ask is that you give
credit"). Traz o fuzil de **TastyTony** (com carregador, cápsula e
textura) e o rig do manequim da UE5 para primeira pessoa. As animações do
AR-15 (parado, andar, sacar, guardar, tiro) foram **feitas pelo Jhon** em
cima desse rig, em 15/11, e os braços são os dele: **o X Bot do Mixamo**
(a mesma licença dos nossos personagens) recortado e ligado aos ossos do
manequim, no lugar da malha da Epic, que não entra no jogo.

**O `ar15.glb` é a fonte desde 24/11.** O `infima.py`, que exportava a
cena do Jhon (`Animations_Assault_Rifle.blend`, `RIG_Manny.blend`,
`RIG_AssaultRifle.blend`), saiu: os `.blend` de origem não estão mais no
projeto. O que mexe no AR-15 hoje trabalha sobre o
`src/assets/armas/fp/ar15.glb` que existe — `ar15_detalhe.py` e
`ar15_ultimate.py` (Blender), e os scripts de cima (recarga, mira,
coronhada). Todas as outras armas de fogo são o `ar15.glb` com a arma
trocada (`conjuntos.py`), então ele é a base de tudo.

**A recarga** (19/11) vem da recarga de referência do projeto de Unreal
do mesmo tutorial ("UE5.3 Source Files"), que o Jhon trouxe. Dois
arquivos, em `src/assets/pacotes/infima/`:
`A_FP_AssaultRifle_Reload.uasset` (os braços) e
`A_FP_WEP_AssaultRifle_Reload.uasset` (o carregador). O `uasset.py` lê as
curvas direto do `.uasset` (não precisa do Unreal) e o `recarga.py` as
põe sobre a pose parada do Jhon, no tempo da recarga do jogo — os
detalhes estão no cabeçalho dele. Depois de mexer no `ar15.glb` (ou numa
skin), rode de novo, junto com a mira e a coronhada:

    python3 src/tools/armas/recarga.py src/assets/armas/fp/ar15*.glb --ficha src/assets/armas/fp/bracos.json

Falta confirmar que a licença CC BY do pacote de Blender vale também para
os arquivos de Unreal do tutorial (vieram da mesma página).

## 5. Os personagens: Stylized Sci-Fi Characters

Os humanos da **Eve** e do **Barut** (`src/assets/personagens/eve/` e
`barut/`, `humano.glb` e as texturas) vêm do pacote **Stylized Sci-Fi
Characters**, da Unity Asset Store, sob a **licença da Asset Store** (a
linha está na tela de créditos, `src/scripts/meta/Credits.cs`). Os FBX
"Unity" do pacote já trazem o esqueleto com os nomes do Mixamo.

O `personagens/scifi.py` e o `personagens/texturas.py`, que convertiam o
pacote, **saíram em 24/11**: refazer o `humano.glb` por eles desfaria os
ajustes de mão e braço que foram feitos depois. O `humano.glb` de cada
pasta é a fonte. Como fazer um personagem novo: `docs/PERSONAGENS.md`.

Os lobos (`eve/lobo_azul.glb`, `barut/lobo_vermelho.glb`) foram gerados
pelo Jhon no **Meshy** (termos do plano dele), e as animações do lobo
(`_comum/lobo/lobo_anim.glb`) são dele. Desde 25/11 os dois são o mesmo
modelo ("WerewolfNaturalWolf", malha e texturas do Meshy), posto no
esqueleto do lobo de 17/11 e pintado por time pelo `src/tools/lobo`
(veja o LEIA-ME de lá). O corpo base e as animações do
humano (`_comum/humano/`, o X Bot) e a morte e a corrida na parede do
lobo são do **Mixamo**.

## O que já está em uso

| Do pacote | Onde entrou | Como |
|---|---|---|
| Texturas do Poly Haven (44 conjuntos copiados) | `src/assets/textures/pbr/`; os mapas de hoje usam areia, asfalto, cascalho e madeira | copiadas pelo `mapas/texturas.py` (saiu em 24/11); hoje, à mão |
| 1 céu do Poly Haven (dia claro) | `src/assets/mapas/ceus/dia_claro.hdr` — Cidade, Deserto, Estação e Obra | copiado; o da Neon (`noite_neon.hdr`) é desenhado (`src/tools/mapas/kits/fundos.py`) |
| Objetos do Poly Haven | `src/assets/mapas/pecas/*.glb` (barris, caixotes, gerador...) — **nenhum mapa de hoje os usa**: eram da Vila e da Polar. O conversor (`pecas_polyhaven.py`) saiu em 24/11 | — |
| Low Poly FPS Bundle (JustCreate, Fab; o Jhon trouxe os .fbx — kits de peças, sem os mapas montados) | `src/tools/mapas/kits/*.npz` e `src/assets/mapas/kits/*.png` (as peças e a textura de cada kit: fps1, fps2, fps3, fps5, fps6); `src/assets/mapas/pecas/` `cidade.glb`, `deserto.glb`, `estacao*.glb`, `obra*.glb` — os mapas Cidade, Cidade do Deserto, Estação de Trem e Obra | `src/tools/mapas/kits/extrair.gd` + `kit_fbx.py` (uma vez por kit); os mapas foram montados por `src/tools/mapas/<mapa>.py` (veja `docs/MAPAS.md`) |
| Pacote LowPolyFPS4 (Unity Asset Store; o Jhon trouxe o `.unitypackage`) | `src/assets/mapas/pecas/armazem_estendido.glb` — o Armazém | `src/tools/mapas/blender/armazem_estendido.py` (lê o pacote pelo `blender/armazem.py`). O `armazem.glb` do Armazém original saiu em 24/11 |
| Low Poly Sci-Fi City (Unity Asset Store; o Jhon trouxe o `.unitypackage`) | `src/assets/mapas/kits/scifi*.png`, `src/tools/mapas/kits/scifi.npz` e `src/assets/mapas/pecas/neon.glb` — o Distrito Neon | `src/tools/mapas/kits/unity_*.py` e `src/tools/mapas/neon.py` |
| As armas de 19/11 (`reais/`) | `src/assets/armas/<id>/` e `src/assets/armas/fp/<id>.glb`: M4A4, AK-74, SCAR-H, SVD, Desert Eagle, granada de choque, e as peles | `reais.py`, `conjuntos.py`, `peles.py` (seção 3) |
| O AR-15 da InfimaGames (fuzil, braços e animações do Jhon) | `src/assets/armas/fp/ar15.glb` e `src/assets/armas/ar15/` | o `.glb` é a fonte (seção 4) |
| A faca (knife animated, DJMaesen) | `src/assets/armas/faca*/` e `fp/faca*.glb` | `reais.py` + `conjuntos.py` |
| P9 Manny FPS Animations — só o movimento | a Desert Eagle na primeira e na terceira pessoa | `blender/p9.py`, `RefP9` |
| Stylized Sci-Fi Characters | `src/assets/personagens/eve/` e `barut/` (`humano.glb`) | o `.glb` é a fonte (seção 5) |

## Regras

- Só CC0 ou CC BY (ou outra que permita uso comercial em jogo). Nunca
  arte extraída de outro jogo.
- Todo item CC BY entra na tela de créditos e em `src/assets/audio/CREDITOS.md`
  (sons) ou nesta página (arte).
- Nada de `src/assets/pacotes/` é referenciado direto por cena: o jogo só
  enxerga o que foi copiado para as pastas dele.
