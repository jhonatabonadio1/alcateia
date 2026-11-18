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
`ui_recompensa_1`, `ui_recompensa_2`.

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
(`granada_explosao`), Voiceover Pack Fighter (a voz do locutor:
`voz_prepare`, `voz_lutar`, `voz_3`, `voz_2`, `voz_1`, `voz_vitoria`,
`voz_derrota`, `voz_empate`, `voz_multi_kill`, `voz_tempo` — deixada
mais grave e lenta, comprimida, com uma sala curta). Via os espelhos no
GitHub de Calinou e Boyquotes. O espelho da voz
(`Boyquotes/kenney-voiceover-for-godot`) traz uma licença GPL v3 para o
*addon* de Godot que o acompanha; usamos só as gravações, que são da
Kenney e CC0 — vale conferir na página do pacote em kenney.nl antes de
publicar.

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

Não tem arquivo próprio: o uivo (R, de lobo) toca o mesmo som da
transformação, `lobo_transformar`, por decisão do Jhon
(`WorldAudio.HowlSound`).
