# Sons do Alcateia

Os sons que estão aqui foram gerados por `tools/sons/processar.py` a
partir de gravações livres — origem e licença de cada um em
`CREDITOS.md`. Para trocar um, ou acrescentar o que falta, jogue o
arquivo **nesta pasta** com os nomes abaixo. O jogo acha
sozinho: o que existir toca, o que faltar fica mudo. Não precisa mexer
em código nem em cena.

## Regras

- Formatos: `.wav` para efeito curto; `.ogg` para música, ambiente e
  uivo. `.mp3` também funciona.
- **Som do mundo precisa ser mono.** Estéreo não tem como sair de um
  ponto do mapa. No Audacity: Tracks → Mix → Mix Stereo Down to Mono.
  Interface e música podem ser estéreo.
- **Variações:** `passo_1.wav`, `passo_2.wav`, `passo_3.wav`... Todas
  entram num sorteio sem repetir a anterior, com o tom variando ±6%.
  Pode ter só `passo.wav`, só as numeradas, ou os dois. A numeração
  começa em 1 e para no primeiro número que faltar.
- Corte o silêncio do começo do arquivo: tiro com 50 ms de silêncio
  antes soa atrasado.
- Volume: normalize tudo para perto de −1 dB e ajuste o equilíbrio
  entre eles no jogo, não no arquivo.
- **Licença:** só CC0, domínio público, ou licença que permita uso
  comercial em jogo. Nunca som extraído de outro jogo. Anote de onde
  veio cada um em `CREDITOS.md` nesta pasta.

## Mundo — os bots ouvem a mesma coisa

Tocam no ponto e no raio do ruído que os bots ouvem. Variações são
muito bem-vindas aqui.

| Arquivo | Quando | Alcance |
|---|---|---|
| `passo` | Passo humano (correndo; agachado e Shift são mudos) | 13 m |
| `lobo_passo` | Passo de lobo, mais pesado, com unha | 20 m |
| `pouso` | Humano tocando o chão depois de pular ou cair | 17 m |
| `lobo_pouso` | Lobo tocando o chão | 17 m |
| `lobo_uivo` | Uivo (uma vez por uivo). Cauda longa, `.ogg` | 45 m |
| `tiro` | Tiro genérico, se a arma não tiver o seu | da arma |
| `tiro_m16a2` `tiro_sg552` `tiro_ak47` `tiro_ump45` `tiro_m3` `tiro_m9` | Tiro de cada arma | da arma |
| `tiro_distante` | Tiro de longe (mais de 35 m), abafado | da arma |
| `tiro_distante_ak47` etc. | Versão distante de uma arma específica | da arma |
| `granada_explosao` | Explosão da granada de choque | ~39 m |

## Corpo — só de perto (12 m)

Os bots não ouvem estes, então eles não carregam informação de longe.

| Arquivo | Quando |
|---|---|
| `pulo` / `lobo_pulo` | Saiu do chão pulando |
| `lobo_transformar` | Começou a virar lobo (0,8 s): rosnado, osso, respiração |
| `lobo_destransformar` | Voltando a humano (sem ele, usa `lobo_transformar`) |
| `lobo_garra` | Garrada ou patada no chão: o "vush" |
| `lobo_bote` | Bote no ar (sem ele, usa `lobo_garra`) |
| `lobo_garra_acerto` | A garra pegou alguém: impacto carnudo |
| `faca` | Golpe de faca |
| `recarga` | Começo da recarga, genérico |
| `recarga_ak47` etc. | Recarga de uma arma específica |
| `recarga_fim` | Recarga terminou (ferrolho, trava), genérico |
| `recarga_fim_ak47` etc. | Fim de recarga de uma arma específica |
| `cartucho` | Cada cartucho entrando na M3 |
| `trocar_arma` | Trocou de arma |
| `granada_lancar` | Arremessou a granada |
| `dor` / `lobo_dor` | Levou dano (no máximo um a cada 0,4 s) |
| `morte` / `lobo_morte` | Morreu |

## Só para você

| Arquivo | Quando |
|---|---|
| `acerto` | Seu tiro acertou alguém: tique curto e seco |
| `acerto_cabeca` | Acertou na cabeça (sem ele, usa `acerto`) |
| `abate` | Você matou alguém |
| `respawn` | Você renasceu |
| `contagem` | Bipe nos últimos 3 s da contagem inicial |
| `partida_inicio` | A partida começou |
| `vitoria` / `derrota` / `empate` | Fim da partida |

## Interface

| Arquivo | Quando |
|---|---|
| `ui_passar` | Mouse passando sobre um botão |
| `ui_clique` | Clicou num botão |
| `ui_negado` | Clicou num conjunto que você não pode pagar |
| `musica_menu` | Música do menu principal, em loop |

## Volumes

Os sons saem em três canais — **SFX**, **Interface** e **Musica** —
criados por código (`scripts/core/Sfx.cs`) e ligados ao Master. Por
isso eles não aparecem na aba Audio do editor. Quando houver tela de
opções, cada canal ganha um controle de volume.

Até lá, para equilibrar: normalize os arquivos e, se um som específico
ficar alto ou baixo demais, ajuste o volume do próprio arquivo. O seu
passo já toca 3 dB mais baixo que o dos outros (`OwnStepVolume` no nó
WorldAudio).
