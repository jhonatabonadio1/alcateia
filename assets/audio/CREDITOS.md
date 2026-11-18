# Créditos dos sons

Todos os arquivos desta pasta foram gerados por
`tools/sons/processar.py` a partir das fontes abaixo: cortados,
convertidos para mono, com volume normalizado e, em vários casos, com
tom alterado ou misturados em camadas.

## Gerados para o jogo (IA, 18/11)

Feitos pelo Jhon com um gerador de áudio por IA, a partir dos prompts do
guia `docs/SONS_IA.md`, e preparados por `tools/sons/preparar_ia.py`
(cortados, volume igualado, laço fechado nas músicas):
`musica_menu`, `musica_sala`, `musica_fim_proximo`, `musica_resultado`,
`partida_inicio`, `vitoria`, `derrota_1`, `derrota_2`, `empate`,
`ui_passar`, `ui_clique`, `ui_negado`, `ui_abrir`, `ui_fechar`,
`ui_equipar`, `ui_compra`, `ui_jogar`, `ui_xp_fim`, `ui_patente`,
`ui_recompensa_1`, `ui_recompensa_2`; e a voz da locutora **Iris**:
`voz_prepare`, `voz_3`, `voz_2`, `voz_1`, `voz_double_kill`,
`voz_triple_kill`, `voz_quadra_kill`, `voz_penta_kill`, `voz_hexa_kill`,
`voz_vitoria`, `voz_derrota` (cortadas de um áudio só, volume igualado).

> **Antes de publicar:** anotar aqui qual ferramenta gerou e conferir
> que o plano usado permite uso comercial.

## Exige crédito — CC BY 4.0

**Oculus Audio Pack 1**, Meta Platforms (equipe de som da Oculus),
licença [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/),
via [facebookincubator/Meta-open-sound](https://github.com/facebookincubator/Meta-open-sound).
Modificados.

> **Antes de publicar o jogo:** a CC BY exige crédito visível. Coloque
> na tela de créditos: "Sons: Oculus Audio Pack 1 por Meta Platforms,
> CC BY 4.0 (modificados)".

Usado em: `passo`, `pouso`, `lobo_passo`, `lobo_pouso` (com Kenney) e
as versões por superfície (`_metal`, `_neve`, `_madeira`, `_terra`),
`capsula`, `impacto_metal` (com Kenney), `ambiente_metro`,
`ambiente_hangar`, `metro_trem`,
`pulo`, `lobo_pulo`, `lobo_garra`, `lobo_bote`, `lobo_garra_acerto`,
`granada_lancar`, `dor`, `morte`, `lobo_dor`, `lobo_morte`,
`lobo_transformar`, `lobo_destransformar`, `respawn`,
`partida_inicio`, `vitoria`, `derrota`, `empate`, `musica_menu`.

## CC0 — domínio público, sem exigência

**Free Firearm Sound Library** (Kickstarter, 2014), CC0, via
[buddingmonkey/FreeFirearmsSFXLibrary](https://github.com/buddingmonkey/FreeFirearmsSFXLibrary).

| Arquivo | Gravação |
|---|---|
| `tiro_m16a2`, `tiro_proprio_m16a2`, `tiro`, `recarga_m16a2`, `recarga`, `recarga_fim_m16a2`, `recarga_fim` | AR-15 |
| `tiro_sg552` | Norinco SKS |
| `recarga_sg552`, `recarga_fim_sg552` | AR-15 (tom alterado) |
| `tiro_ak47`, `tiro_distante_ak47`, `tiro_distante`, `recarga_ak47`, `recarga_fim_ak47` | AK-47 (pente saindo: AR-15) |
| `tiro_ump45`, `recarga_ump45`, `recarga_fim_ump45` | Carl Gustav M45 |
| `tiro_m3`, `recarga_fim_m3`, `cartucho` | Benelli Nova |
| `tiro_m9`, `recarga_m9`, `recarga_fim_m9` | Walther PPQ |
| `tiro_distante_*` | Versões "mid distance" das mesmas armas |

**Kenney** ([kenney.nl](https://kenney.nl)), CC0: UI Audio
(`ui_passar`, `ui_clique`), Interface Sounds (`ui_negado`, `acerto`,
`abate`, `contagem`), RPG Audio (`faca`, `trocar_arma`, `recarga_m3`),
Impact Sounds (`acerto`, `acerto_cabeca`, `lobo_pouso`), Sci-Fi Sounds
(`granada_explosao`). Via os espelhos no GitHub de Calinou e
Boyquotes. (A voz provisória do locutor, do Voiceover Pack Fighter, saiu
em 18/11: a narração agora é da Iris.)

Cada `tiro_<arma>_N` e `tiro_proprio_<arma>_N` parte de um dos tiros da
mesma gravação de perto da arma (as gravações têm de dois a quatro),
montado em camadas: o estalo da gravação, um soco grave sintetizado, a
gravação de meia distância da mesma arma como cauda e um clique
metálico. O `proprio` é a versão estéreo, com a cauda aberta. O porquê
e as medidas estão em `docs/AUDIO.md`.

`impacto` vem do Impact Sounds da Kenney (CC0).

## Gerados por código — sem fonte externa

`ambiente_polar` e `ambiente_vila` são vento sintetizado (ruído filtrado
com rajadas) por `tools/sons/processar.py`. São do projeto.

`arma_vazia` (o clique seco do gatilho sem bala) é sintetizado pelo mesmo
script. É do projeto.

## O uivo

O uivo (R, de lobo) toca `lobo_rugido` (`WorldAudio.HowlSound`); sem ele, cai
no som da transformação, `lobo_transformar`. Ver "Poder do uivo" abaixo.

## Raios do especial do lobo (18/11)

`lobo_especial_armar_1..2`, `lobo_especial_raio_1..3` e
`lobo_especial_queda_1..2` são **sintetizados por código**
(`tools/sons/raios.py`: ruído filtrado e senoides, numpy e scipy). Nenhuma
gravação; sem licença de terceiros.

## Pacotes que o Jhon mandou (18/11)

Preparados por `tools/sons/pacote_armas.py`:

- **Free Weapon SFX v1.1** (amostra grátis do *Weapon Sound Effects Bundle*,
  GameDev Market — gamedevmarket.net/asset/weapon-sound-effects-bundle):
  `tiro_ar15_1..3`, `tiro_proprio_ar15_1..3`, `tiro_distante_ar15`,
  `recarga_ar15`, `recarga_fim_ar15`, `trocar_arma_3..4`.
- **Free Sounds Pack**: `lobo_rugido_1` (Creature 1-21) e `faca_acerto_1`
  (Stab 4-1).

**A conferir antes de publicar:** o zip não trouxe arquivo de licença nem o
nome do autor. O Jhon precisa confirmar a licença e o crédito de cada pacote
(a tela de créditos usa o texto de `scripts/meta/Credits.cs`).

## Poder do uivo (18/11)

`lobo_uivo_energia_1..2` (o poder carregando, junto com o rugido) e
`lobo_uivo_escudo_1..2` (o escudo cheio, no fim) são **sintetizados por
código** (`tools/sons/uivo.py`: senoides, serras e ruído filtrado, numpy e
scipy). Nenhuma gravação; sem licença de terceiros.

O rugido em si, `lobo_rugido_1`, é montado por `tools/sons/rugido.py` a partir
de duas gravações do **Free Sounds Pack** (Creature 1-21 e Zombie 1 - Short
1-01), em camadas e com distorção. Vale a pendência de licença do pacote.

## Acertos e sangue (18/11)

Do **Free Sounds Pack** (o mesmo pacote do rugido e da faca), preparados por
`tools/sons/acertos.py`: `impacto_carne_1` (Hit Generic 5-1),
`impacto_carne_2` (Hit Generic 2-1), `impacto_carne_3` (Punch 2-1) e
`sangue_1..2` (Stab 4-1, só a parte do golpe; a segunda, mais grave).
Vale a mesma pendência de licença e crédito do pacote.

## Ambiente do Armazém (18/11)

`ambiente_armazem.ogg` é o **Ambient Wind Loop 1** do Free Sounds Pack (sem o
ronco abaixo de 70 Hz, com a faixa de 300 Hz a 2 kHz levantada e as rajadas
amansadas), com o `ambiente_hangar` antigo por baixo, 60 s em laço. Montado
por `tools/sons/ambiente.py`. Vale a pendência de licença do pacote.

## Marcador de acerto (18/11)

`marcador_1..3` (corpo) e `marcador_cabeca_1..2` (cabeça, com o "tic") são
**sintetizados por código** (`tools/sons/marcador.py`). Nenhuma gravação; sem
licença de terceiros. Os antigos `acerto` e `acerto_cabeca` ficam como reserva.
