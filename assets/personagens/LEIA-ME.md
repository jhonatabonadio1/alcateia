# Personagens

Uma pasta por personagem: `personagem.tres` (nome, time, vida), o humano
(`humano.glb`), o lobo, `retrato.png` e, se houver, `thumb.jpg` (16:9), a
imagem do cartão no inventário. O jogo acha as pastas sozinho e cada uma
vira um item da aba Personagem.

**`retrato.png`** (quadrado, 512×512, o rosto centrado) é a foto de quem
joga com o personagem na partida: no placar (Tab) e na insígnia do HUD,
no lugar da foto de perfil (21/11). Todo personagem novo precisa ter um;
sem ele, o jogo cai no `thumb.jpg`, que é 16:9 e fica esticado na
insígnia.

Como fazer um novo, com as proporções certas para as animações
servirem: **`docs/PERSONAGENS.md`**.

| Pasta | Nome | Time | Vida | Humano | Lobo |
|---|---|---|---|---|---|
| `eve/` | Eve | azul (Vanguarda) | ×1 (200) | `eve/humano.glb` | o lobo azul (`assets/characters/wolf/lobo_azul.glb`) |
| `barut/` | Barut | laranja (Sindicato) | ×1 (200) | `barut/humano.glb` | o lobo vermelho (`assets/characters/wolf/lobo_vermelho.glb`) |

**Eles são os personagens dos times** (19/11): quem não escolhe nada, e
todo bot, joga de Eve no azul e de Barut no laranja
(`CharacterAnimator.BlueModel` / `OrangeModel`). Os do Mixamo (Exo Gray e
Ely) saíram.

## De onde vêm os humanos

Do pacote **Stylized Sci-Fi Characters** (os FBX "Unity", que já trazem o
esqueleto com os nomes do Mixamo). Dois passos, os dois em `tools/personagens/`:

    python3 tools/personagens/texturas.py <pasta do pacote> /tmp/tex
    python3 tools/personagens/scifi.py -- --fbx <pacote>/SM_Sci-Fi_Male_Unity.fbx \
        --tex /tmp/tex --prefixo m \
        --partes "Body_head,Male_Hair_01,Boots,Gloves,Pants,Upper Body" \
        --saida assets/personagens/barut/humano.glb
    python3 tools/personagens/scifi.py -- --fbx <pacote>/SM_Sci-Fi_Female_Unity.fbx \
        --tex /tmp/tex --prefixo f \
        --partes "Body_head,Body_arms_upper,Body_arms_elbow,Female_Hair_04,Accessory01,Boots,Gloves,Pants,Upper Body" \
        --saida assets/personagens/eve/humano.glb

O `scifi.py` fica só com as peças da roupa e um cabelo (o pacote traz
cinco de cada; trocar é mudar `Hair_01` para `Hair_02`…), põe o alto do
crânio a 1,79 m, dá aos ossos o prefixo `mixamorig:` e cria as pontas que
o jogo procura (`HeadTop_End`, `Toe_End`).

## Como as animações servem

O esqueleto tem os nomes do Mixamo, mas outro repouso (o eixo de cada osso
é outro). Os clipes do X Bot não entram direto: o `CharacterAnimator` nota
a diferença (`AnimationKit.SameRig`) e ajusta cada clipe ao corpo
(`AnimationKit.FitToBody`: a direção de cada osso no mundo, e o quadril na
altura do corpo). O ajuste é feito uma vez por modelo e vale para todos
os corpos iguais da partida.

**A arma na mão** (terceira pessoa) vem das animações da primeira pessoa,
refeitas no Blender para cada personagem e cada arma
(`animacoes/<arma>.json`, na pasta dele; 22/11). O script é
`tools/personagens/blender/animacoes_armas.py`: ele abre o humano e o
conjunto da primeira pessoa da arma (`assets/armas/fp/`), põe o conjunto no
corpo (`POSTURA`: no quadril e na mira) e, quadro a quadro de cada clipe,
leva as mãos onde as da primeira pessoa estão, escolhe o cotovelo que
deixa o punho no que ele dobra e fecha cada dedo até encostar na arma. A
faca e a granada têm o corpo inteiro coreografado (`FACA`, `GRANADA`). No
jogo (`HeldWeapon.HoldByClips`) o clipe toca junto com o do conjunto, o
peito desfaz o giro do quadril da animação de andar e inclina para a mira,
e o conjunto vai onde o clipe diz, em relação ao peito.

Sem a pasta `animacoes/`, vale a pose parada de antes (`poses_armas.json`,
`tools/personagens/blender/poses_armas.py`). Para ajustar uma pose à mão:
`poses_armas.py <pasta> --blend` grava
`tools/personagens/blender/poses/<pasta>.blend` (uma ação por tipo; a arma
de cada tipo presa na mão direita, na coleção `arma_<tipo>`). Mude a pose
da ação no Blender e rode `poses_armas.py <pasta> --do-blend` para
regravar o `.json` a partir dele. O `.blend` tem ~22 MB com as texturas e
fica fora do git (a pasta tem `.gdignore`: o Godot não tenta importá-lo).

Na vitrine do inventário o humano fica na pose de pé, sem arma
(`HeroPose`).
