# Áudio

O plano de som do WolfLeague: as referências, o que o jogo já toca, de
onde vem cada som e o que falta. Pedido do Jhon em 18/11: tema do menu,
tiros, passos, narração e uma pesquisa de fontes, com Overwatch e League
of Legends de referência.

**Para gerar sons com IA:** `docs/SONS_IA.md` tem cada som com o nome de
arquivo, quando toca, como deve soar, o prompt em inglês e o que já existe.

Todo som do jogo é **gerado por roteiro** — `tools/sons/baixar.py` baixa
as gravações de origem e `tools/sons/processar.py` corta, mistura e
normaliza para `assets/audio/`, com o nome que o `Sfx` procura. Para
mudar um som, muda-se a linha dele no roteiro. As fontes e licenças de
cada arquivo estão em `assets/audio/CREDITOS.md`.

> Quem escreveu os roteiros (o Claude) **não ouve** o resultado: os sons
> são ajustados por medida (pico, volume médio, quanto de grave, quanto
> de cauda). O ouvido é o do Jhon. Se um som ficou pior, `git checkout`
> do arquivo volta o anterior.

## As referências

**Overwatch — "jogar pelo som".** A equipe de som do jogo (Scott Lawlor
e Tomas Neumann, GDC 2016) trabalhou com a meta de se poder jogar quase
sem olhar a tela. Os pilares que valem para nós:

- **Mistura limpa.** Cada jogador tem uma "importância" na mistura, que
  sobe com a ameaça: quem está mirando ou atirando em você soa mais alto
  que um inimigo mais perto que não te viu. O resto baixa.
- **Dá para apontar de onde vem.** Passo, tiro e habilidade são
  localizáveis de olho fechado.
- **O som informa.** Passo de inimigo é mais alto que o de aliado. Cada
  herói tem passo e arma próprios; a suprema tem uma fala só dela.
- **Fala contextual.** Em vez de todo grito de todo jogador, só a fala
  que informa.

**League of Legends — o locutor.** Uma voz só, de arena, que narra os
marcos da partida: o começo, o primeiro abate, as sequências (double,
triple, quadra, penta kill), o time eliminado, a vitória e a derrota.
Curta, grave, sempre igual — o jogador reconhece o evento pela voz sem
ler nada.

**O que isso vira aqui**

| Ideia | No WolfLeague |
|---|---|
| O seu tiro tem peso; o dos outros tem lugar | `tiro_proprio_*` (estéreo, com soco e cauda) e `tiro_*` (mono, num ponto do mapa), mais `tiro_distante_*` de longe |
| Inimigo soa mais que aliado | **a fazer**: hoje passo de aliado e de inimigo têm o mesmo volume |
| Lobo e humano se distinguem de ouvido | já: `lobo_passo` é mais grave e abafado que `passo` |
| O especial se anuncia | **a fazer**: um som próprio no disparo do especial (hoje só os raios) |
| Locutor | começou (veja abaixo); faltam as falas das sequências |
| Música só onde não atrapalha | menu, e acordes de começo e fim de partida; na partida, nada |

## O tiro (refeito em 18/11)

Medido antes: de 93 a 99% da energia de cada tiro ficava nos primeiros
30 ms, e menos de 3% abaixo de 150 Hz (fora o M16). Ou seja: um estalo,
sem corpo e sem cauda. Agora cada tiro é montado em camadas, como se faz
em jogo de tiro:

- **estalo** — a gravação de perto: o ataque e o caráter da arma;
- **soco** — uma varredura grave sintetizada (o peito do tiro);
- **cauda** — a gravação de meia distância da mesma arma, abafada e um
  nada atrasada: a sala respondendo. No seu tiro, aberta em estéreo;
- **ferro** — um clique metálico baixinho logo depois (o mecanismo);
- e um limitador no fim, que junta as camadas.

Depois: o grave ficou entre 11 e 15% da energia, e o ataque entre 54 e
80% (o resto é corpo e cauda). Os tiros duram de 0,65 a 1,1 s (eram de
0,4 a 0,85). A escopeta (M3) ficou bem mais alta que antes na média —
se incomodar, é o primeiro número a baixar (`guns["m3"]`, o soco).

## A locutora: Iris

Desde 18/11 a narração é da **Iris**, voz gerada pelo Jhon. A voz
provisória da Kenney saiu de vez: duas vozes na mesma partida soavam
erradas. Hoje ela diz:

| Arquivo | Fala | Quando toca |
|---|---|---|
| `voz_prepare` | "Prepare for combat" | começo da contagem |
| `voz_3`, `voz_2`, `voz_1` | "3", "2", "1" | os três últimos segundos |
| `voz_double_kill` … `voz_hexa_kill` | "Double kill" … "Sextuple kill" | 2 a 6 abates seguidos |
| `voz_vitoria` / `voz_derrota` | "Victory" / "Defeat" | fim, depois do acorde |

**Faltam** (sem o arquivo, fica em silêncio): `voz_lutar` ("Fight!"),
`voz_empate`, `voz_hepta_kill`, `voz_octa_kill`, `voz_team_kill`,
`voz_vinganca` e `voz_multi_kill` (a reserva de qualquer sequência sem
fala própria). A lista completa, com os prompts, está em
`docs/SONS_IA.md`.

Onde buscar (o Jhon baixa; o ambiente do Claude só alcança o GitHub):

- **WARLORD – Video Game Announcer**, de VoiceBosch (OpenGameArt e
  itch.io, gratuito): Double Kill, Triple Kill, Dominating, Rampage,
  Revenge Kill, Annihilation, Game Over… É o mais próximo do que se quer.
  Conferir a licença na página antes de publicar (pede crédito a
  VoiceBosch). https://opengameart.org/content/warlord-video-game-announcer
- **Classic Killstreak Announcer Voices** (OpenGameArt): first blood,
  double, triple, quadruple e quintuple kill. Conferir a licença.
  https://opengameart.org/node/179490
- **Announcer kill sounds**, de TripleSnail (OpenGameArt): first blood,
  double kill, triple kill, rampage, dominating…
  https://opengameart.org/content/announcer-kill-sounds
- Pagos, prontos e com licença clara: *Shooter Game Announcer Voice*
  (Epic Stock Media, uns US$ 11 a 19, 199 falas com sequências,
  contagens e vitória/derrota) e *FPS Announcer* (EchoFrame, US$ 5).
- Ou gravar: são nove falas curtas; qualquer voz grave com compressão e
  uma sala curta serve, e o roteiro já faz esse tratamento.

Para instalar: pôr o `.wav` em `assets/audio/` com o nome da tabela.

## Música

Hoje o "tema" do menu (`musica_menu.ogg`) é um ambiente de floresta à
noite, e a partida tem três acordes (`partida_inicio`, `vitoria`,
`derrota`, `empate`). Não há música de verdade ainda.

O que procurar: um tema de menu de 1 a 3 minutos que faça laço, sombrio
e heroico (percussão grave, cordas, sem melodia grudenta), e versões
curtas dele para vitória e derrota. Na partida, sem música: ela cobre
passo e tiro, que é o que se joga pelo som.

Fontes com uso comercial liberado:

| Fonte | Licença | O que tem |
|---|---|---|
| OpenGameArt, filtro CC0 | CC0 | "Epic Main Menu Theme ~ Loop" (CleytonKauffman), "Battle Theme A" (cynicmusic), a coleção "CC0 – Cinematic Music" |
| Pixabay Music | licença Pixabay: comercial, sem crédito | muita trilha orquestral e eletrônica; não pode revender o arquivo solto |
| Sonniss – GDC Game Audio Bundle | royalty-free, comercial, sem crédito (não pode treinar IA) | efeitos de estúdio em volume: armas, impactos, ambientes, criaturas |
| Freesound, filtro CC0 | CC0 | efeitos avulsos; cuidado com CC BY-NC, que não serve |
| Kenney | CC0 | interface, impactos, vozes (já usamos) |

Para instalar a música do menu: salvar como
`assets/audio/musica_menu.ogg` (o jogo já toca esse nome em laço).

## O que falta, em ordem

1. Falas das sequências (acima).
2. Tema do menu de verdade.
3. Som do especial do lobo (disparo e queda), casando com os raios.
4. Passo de inimigo mais alto que o de aliado; ameaça na mistura.
5. Tiro com variação por ambiente (dentro e fora) e mais gravações por
   arma: hoje são de duas a quatro variações.
6. Dublagem dos personagens (em inglês), já anotada em
   `docs/PENDENCIAS.md`.
