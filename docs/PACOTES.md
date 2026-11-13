# Pacotes de arte

De onde vem a arte que não é feita por código: texturas, objetos de
cenário, céus, braços e armas. Tudo com licença que permite uso
comercial.

A matéria-prima fica em `assets/pacotes/`, **fora do git e fora do
Godot** (`.gdignore`): é grande e dá para baixar de novo. O que o jogo
usa de fato é copiado de lá para as pastas do jogo, essas sim no git.

```
assets/pacotes/
  polyhaven/texturas/<id>/   cor, normal e AO+aspereza+metal
  polyhaven/modelos/<id>/    glTF com as texturas
  polyhaven/ceus/            HDRI: o céu que ilumina o mapa
  sketchfab/                 os .glb baixados à mão (braços e armas)
```

## 1. Poly Haven — um comando

[Poly Haven](https://polyhaven.com) é fotografia e escaneamento de
coisa real, tudo **CC0** (sem crédito obrigatório). Da raiz do projeto:

```
python3 tools/baixar_pacotes.py
```

Baixa ~57 texturas, ~85 objetos e 6 céus (uns 300 MB). Pode rodar de
novo: pula o que já veio e tenta de novo o que falhou. `--lista` mostra
os endereços sem baixar; `texturas`, `modelos` ou `ceus` baixa só uma
parte. Modelo com arquivo acima de 80 MB é pulado (`--tudo` traz).

A lista está no começo do script. O que é cada grupo:

| Grupo | Para quê |
|---|---|
| Concreto, reboco, tijolo, bloco | Paredes e pisos dos quatro mapas |
| Asfalto, calçada de pedra, arenito, telha de barro | Ruas, praça e telhados da Vila |
| Neve (3), asfalto com neve | Chão da Estação Polar |
| Areia, cascalho, terra rachada, rocha | Chão e encostas da Vila |
| Ferro ondulado, contêiner, chapa pintada, ferrugem, persiana | Galpões, hangar, vagão |
| Ladrilho, granilite, piso gasto | Estação do Metrô |
| Tábua, compensado | Caixotes, andaimes, bancas |
| Barris, caixotes militares, barreiras de concreto, pneus, gerador, botijão | Cobertura |
| Fachada de fábrica, escada de incêndio, alambrado, tubulação, duto, porta de enrolar, portão, ponte rolante | Arquitetura que não é caixa |
| Postes, refletores, luminárias, câmeras, ar-condicionado, hidrante | Detalhe de rua e de parede |
| Pedras e paredões (deserto e montanha), troncos, pinheiros | Borda dos mapas abertos |
| Pistola, granada de cabo, facão, fuzil de ferrolho | Reserva para as armas |
| Céus: neve nublada, pôr do sol no deserto (3), dia claro, noite | Luz de cada mapa |

Se o endereço de um item mudar no site, o script avisa qual falhou; a
página de cada um é `https://polyhaven.com/a/<id>`.

## 2. Sketchfab — à mão (precisa de login)

O Sketchfab só deixa baixar logado (a conta é grátis). Em cada página:
**Download 3D Model → GLB** (se perguntar o tamanho da textura, 2K) e
salve o arquivo em `assets/pacotes/sketchfab/`, com o nome que vier.

Todos são **CC BY 4.0**: uso comercial liberado, **com crédito** ao
autor na tela de créditos (a lista abaixo já é o crédito).

### Braços em primeira pessoa, com animação (o que mais importa)

Os cinco são do mesmo autor (Cransh) e usam os mesmos braços, então
combinam entre si. Cada um traz os braços, uma arma e as animações
(parado, tiro, recarga, sacar).

| Para | Modelo | Autor |
|---|---|---|
| Fuzis (M16A2, SG552) | [Animated FPS hands (rifle animation pack)](https://sketchfab.com/3d-models/none-5f2d0ed780a94724b36ab505f7564057) — 8 animações | Cransh; mãos de bumstrum; fuzil de doomsentinel |
| AK-47 | [FPS AK-74m animations](https://sketchfab.com/3d-models/none-94be8385c402474cacd39bc096c6ca14) — 8 animações | Cransh; braços de DJMaesen/bumstrum; arma de creationwasteland |
| UMP45 | [SMG FPS Animations](https://sketchfab.com/3d-models/none-ca37ea9148dc4fcc9cc632175d311b23) — 8 animações | Cransh; braços de DJMaesen/bumstrum; arma de eNse7en |
| M9 | [FPS pistol animations](https://sketchfab.com/3d-models/none-0d7a343dcb6f401197a73c91aee93f6d) — 5 animações | Cransh; mãos de bumstrum; pistola de raimeiyonke |
| M3 (escopeta) | [FPS Benelli M4 Animations](https://sketchfab.com/3d-models/none-225a62190f6043ca975eaa2798ab7e2c) — 8 animações | Cransh; arma de haoliu95; mãos de teenjust500 |
| M3, reserva (ação de bomba) | [FPS Arms remington (shotgun)](https://sketchfab.com/3d-models/none-e68ef617fe8a48cca8610d016ffd5881) — 4 animações | Cransh; braços de bumstrum; arma de tris09 |

### As armas do jogo

Para trocar a arma que vem no pacote de animação pela arma certa.

| Arma | Modelo | Autor |
|---|---|---|
| M16A2 | [M16 A2 Assault Rifle - Animated](https://sketchfab.com/3d-models/none-bd4ff595b593447f9a7f1d95f501fb6c) | user77 |
| SG552 | [Low-Poly Sig SG552 Commando](https://sketchfab.com/3d-models/none-d00c7ec69b3a4fde98c6212b2840ebc4) | TastyTony |
| AK-47 | [AK-47 game-ready free model](https://sketchfab.com/3d-models/none-0af36b8200b541ee91e63fa3d0866b2e) | Drus |
| UMP45 | [Low Poly UMP45](https://sketchfab.com/3d-models/none-391c9e2fc73046b798d64e169dff1f35) | Alpaca Studio |
| M3 | [Benelli M3 Tactical](https://sketchfab.com/3d-models/none-1eaea0239fdf43ad92eb276fbf9c60dd) | Amapsis |
| M9 | [Beretta M9](https://sketchfab.com/3d-models/none-d1200d9aa28f466484f7d3fdd7724e76) | emran.bayati |
| Faca | [Gerber Combat knife Low-Poly](https://sketchfab.com/3d-models/none-f8991297774e415c976cc8d476ca21e0) | Lucas Soler |
| Granada de choque | [M84 Stun Grenade - Flashbang](https://sketchfab.com/3d-models/none-81afe3b572ba4e2f92f2c001eaccb83d) | Coozy |

**Estes modelos foram escolhidos pela licença, pela contagem de
polígonos e pela descrição — ninguém do projeto os viu abertos ainda.**
Se algum for ruim ou não abrir, troca-se. Licenças diferentes de
CC BY/CC0 (NonCommercial, "Free Standard" restrita a certos usos) foram
deixadas de fora de propósito.

## O que já está em uso

| Do pacote | Onde entrou | Como |
|---|---|---|
| 44 texturas do Poly Haven | `assets/textures/pbr/` — todos os materiais dos mapas | `tools/mapas/texturas.py` |
| 2 céus (dia claro, neve nublada) | `assets/mapas/ceus/` — Vila e Estação Polar | copiados; `ceu=dict(hdri=…)` no mapa |
| 36 objetos do Poly Haven | `assets/mapas/pecas/*.glb` — nos cômodos e em volta | `tools/mapas/blender/pecas_polyhaven.py` |

Os braços e as armas do Sketchfab **ainda não entraram** — é o próximo
passo (trocar as mãos e as armas feitas por código pelas animadas).
Lembre: a pasta deles é `assets/pacotes/sketchfab/` (fora do Godot); se
ficarem em `assets/sketchfab/`, o Godot tenta importar 750 MB.

## Regras

- Só CC0 ou CC BY (ou outra que permita uso comercial em jogo). Nunca
  arte extraída de outro jogo.
- Todo item CC BY entra na tela de créditos e em `assets/audio/CREDITOS.md`
  (sons) ou nesta página (arte).
- Nada de `assets/pacotes/` é referenciado direto por cena: o jogo só
  enxerga o que foi copiado para as pastas dele.
