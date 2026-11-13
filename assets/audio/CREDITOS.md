# Créditos dos sons

Todos os arquivos desta pasta foram gerados por
`tools/sons/processar.py` a partir das fontes abaixo: cortados,
convertidos para mono, com volume normalizado e, em vários casos, com
tom alterado ou misturados em camadas.

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
(`granada_explosao`). Via os espelhos no GitHub de Calinou e Boyquotes.

Cada `tiro_<arma>_N` e `tiro_proprio_<arma>_N` é um dos tiros da mesma
gravação de perto da arma (as gravações têm de dois a quatro); o
`proprio` é a versão estéreo com reforço de grave.

`impacto` vem do Impact Sounds da Kenney (CC0).

## Gerados por código — sem fonte externa

`ambiente_polar` e `ambiente_vila` são vento sintetizado (ruído filtrado
com rajadas) por `tools/sons/processar.py`. São do projeto.

## O uivo

Não tem arquivo próprio: o uivo (R, de lobo) toca o mesmo som da
transformação, `lobo_transformar`, por decisão do Jhon
(`WorldAudio.HowlSound`).
