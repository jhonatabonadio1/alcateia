# Mapas

Jogue um `.glb` aqui e aponte o `MapLoader` para ele.

## Convenções de nome, lidas do arquivo

| Sufixo no nome do objeto | Efeito |
|---|---|
| `-noco` | Não gera colisão (vidro, decoração, folhagem) |
| `-invis` | Colide mas não aparece (parede invisível, limite do mapa) |
| `spawn...` | Vira ponto de nascimento |

Nomear no Blender é a forma mais barata de passar intenção do modelo
para o jogo sem inventar um formato paralelo.

## O que o carregador faz

Gera colisão de malha para tudo que não estiver marcado, coleta os
spawns e assa a navegação. Tudo em tempo de execução, então trocar o
`.glb` não exige reconfigurar nada.

## O que ele não faz

Não corrige um mapa ruim. Antes de colocar arte, ande pelo mapa em
cinza e responda:

1. Existe um caminho do spawn ao centro que só o lobo consegue fazer?
2. Existe um lugar onde o humano vê o lobo chegando com tempo de reagir?
3. Existe um corredor onde o humano sabe que vai perder e precisa evitar?
4. Dá para cair em cima de alguém de um ponto que ele não olha?

## Exigências da corrida em parede

Paredes com cerca de **1m de espessura** e faces verticais limpas.
Quinas muito finas não funcionam: os raios precisam de superfície para
encontrar.

Corredores com paredes paralelas permitem escalar ricocheteando — é a
geometria que dá ao lobo o ângulo de cima.

## Onde achar modelos CC0

Kenney, Quaternius, Poly Pizza, Sketchfab com filtro CC0.
