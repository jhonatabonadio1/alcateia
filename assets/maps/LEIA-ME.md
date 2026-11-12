# Mapas

Jogue um `.glb` aqui e aponte o `MapLoader` para ele.

## O que está aqui

`arrival/Arrival.glb` — 82 × 44 m andáveis, piso em Y = 7,5 (o
`MapLoader` rebaixa para 0), telhados e passarelas até 17 m.

**É referência de teste, não entrega.** Vem extraído do cliente
original, o que `CLAUDE.md` proíbe em qualquer coisa publicada. Serve
para medir física em proporções reais — e só.

Nomes de pasta em ASCII, sempre. O original vinha com um `İ` turco
(U+0130) que quebra caminho de recurso, git e export para Windows.

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
