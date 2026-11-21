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

A arma na mão (`HeldWeapon`) fica com o punho na palma direita e o
guarda-mão na linha da palma esquerda, e não entra no corpo: desliza para
a frente se a coronha tocar o tronco, afasta-se da cabeça e, no que
faltar, a cabeça sobe um pouco.

Na vitrine do inventário o humano fica na pose de pé, sem arma
(`HeroPose`).
