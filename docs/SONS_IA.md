# WolfLeague — sons para gerar com IA

Cópia de 18/11/2026 do guia vivo, que é onde se edita e comenta:
https://claude.ai/artifact/MNPtVPtQfL1ynagTZXfnyU — ao mudar lá, atualize aqui.

## Como usar

Cada som abaixo tem o **nome de arquivo que o jogo já procura**: gere, salve com esse nome em `src/assets/audio/` e ele passa a tocar na hora, sem mexer em código. Os marcados como **novo** ainda não tocam em lugar nenhum: precisam de uma linha de código.

- **Prompt em inglês.** Os geradores de som e de música entendem bem melhor em inglês. Cole o prompt como está e ajuste a duração na própria ferramenta.
- **Não escreva "League of Legends" no prompt.** A maioria das ferramentas recusa ou copia demais quando lê nome de jogo ou de artista. Os prompts já descrevem o estilo.
- **Variações.** Onde a tabela pede 3 ou mais, gere várias vezes e salve como `nome_1.wav`, `nome_2.wav`… O jogo sorteia entre elas; sem variação, tiro e passo soam como metralhadora de loop.
- **Formato.** Efeitos: WAV, 48 kHz, 16 bits, **mono** (o jogo põe o som no lugar 3D). Exceções em estéreo: o seu próprio tiro (`tiro_proprio_*`), a interface e as músicas. Músicas e ambientes: OGG estéreo. Se a ferramenta só entrega .opus (ou outro formato), `src/tools/sons/preparar_ia.py` converte.
- **Corte o silêncio do começo.** Tiro, clique e passo têm de começar no primeiro milissegundo; 50 ms de silêncio já viram atraso no gatilho. Pico em −1 dB. (`preparar_ia.py` também faz isso e iguala o volume.)
- **Laços.** Música do menu e ambientes tocam em laço: peça "seamless loop" e confira se a emenda não estala.
- **Licença.** Confira se o plano da ferramenta permite uso comercial antes de publicar o jogo.
- **Cuidado com o roteiro antigo.** `src/tools/sons/processar.py` refaz os sons provisórios a partir das gravações de pacotes grátis. Os que já foram trocados pelos gerados saíram dele; ao trocar outros, tire-os de lá também.

**Coluna "No jogo"** (atualizada em 18/11): **✅ IA** = feito pelo Jhon e já tocando · **provisório** = existe e toca hoje, tirado de pacote grátis (troque quando gerar) · **falta** = ainda não existe.

## Direção de som

Do League of Legends vale pegar o método, não o som: cada ação tem uma silhueta que se reconhece de olho fechado, a interface tem um brilho metálico e mágico (o "hextech"), a música é orquestral e épica, e um locutor de arena narra os marcos da partida.

No WolfLeague isso vira **dois mundos que não se confundem**:

| Mundo | Como soa | Palavras para os prompts |
| --- | --- | --- |
| Humano (armas, equipamento) | Metal, mecânica, seco e preciso | metallic, mechanical, tight, punchy, military |
| Lobo (fera, especial) | Grave, orgânico, rasgado, com eletricidade no especial | guttural, beastly, feral, primal, electric crackle |
| Interface e menus | Fantasia sombria: brilho místico curto, clangor de metal que ressoa, impacto sub-grave. Pesado e grandioso, nada fofo | mystical shimmer, resonant metallic clang, deep sub-bass impact, dark fantasy, cinematic game UI, no cute elements |
| Música | Orquestra com percussão grande, coro grave, um toque eletrônico; lobo como motivo | epic orchestral, taiko drums, low choir, dark heroic, hybrid electronic |
| Locutora (Iris) | Voz de arena, segura, sem exagero, com sala | confident female arena announcer, authoritative, slight reverb |

**A pegada, no prompt de referência do Jhon:** *An epic magical item reveal. Starts with a sharp, mystical shimmer, followed by a massive, resonant metallic clang and a deep sub-bass impact. Dark fantasy, grand, heavy and powerful. Cinematic game UI, no cute elements.* Os prompts abaixo seguem essa receita: brilho místico → metal que ressoa → grave fundo, na medida de cada som (o passar o mouse leva só uma pitada; a patente, tudo).

Regra de mistura: **o que informa vem antes do que enfeita.** Tiro, passo de inimigo e o aviso de especial têm de cortar a música e o ambiente. Na partida, música só no começo, no fim e no último minuto.

## Interface e menus

Fantasia sombria em todos: brilho místico, metal que ressoa e grave fundo, com o peso crescendo conforme o momento. Os que tocam toda hora (passar, clicar) levam só uma pitada disso e são curtos, para não cansar; os de recompensa (compra, patente, item) levam tudo.

| Arquivo | No jogo | Quando toca | Como deve soar | Prompt (inglês) | Duração | Variações |
| --- | --- | --- | --- | --- | --- | --- |
| `ui_passar` | ✅ IA | Mouse passa por botão, carta, item | Sussurro metálico grave, quase imperceptível, com um fio de brilho místico | Subtle dark fantasy UI hover, a faint low metallic whisper with a tiny mystical shimmer, restrained, very short, cinematic game UI, no cute elements | 0,08–0,15 s | 3 |
| `ui_clique` | ✅ IA | Clica e confirma | Trava de metal pesada com batida grave curta e um brilho rápido | Dark fantasy UI confirm click, a short heavy metallic latch clank with a low resonant thump and a brief mystical shimmer, cinematic game UI, no cute elements | 0,15–0,3 s | 3 |
| `ui_negado` | ✅ IA | Clique que não pode (sem dinheiro, item travado) | Ferro surdo batendo, com um ronco grave dissonante | Dark fantasy UI error, a dull heavy iron thud with a low dissonant rumble, short and firm, no cute elements | 0,25–0,4 s | 1 |
| `ui_abrir` | ✅ IA | Abre loja, inventário, perfil ou missões | Pedra e metal deslizando, subindo até um brilho místico | Dark fantasy UI panel open, a deep stone and metal slide whoosh rising into a mystical shimmer, heavy, cinematic | 0,3–0,5 s | 1 |
| `ui_fechar` | ✅ IA | Sai desses painéis | Metal pesado descendo até uma batida grave | Dark fantasy UI panel close, a heavy metallic slide down ending in a low thud, short | 0,2–0,4 s | 1 |
| `ui_compra` | ✅ IA | Compra na loja | Brilho místico, moedas batendo num baú de ferro com clangor que ressoa, e o grave | Epic dark fantasy purchase, a sharp mystical shimmer, then a heavy resonant metal clang of coins dropping into an iron chest, deep sub-bass impact, grand, no cute elements | 1–1,5 s | 1 |
| `ui_equipar` | ✅ IA | Equipa arma ou visual no inventário | Encaixe de arma pesado, metal que soa e batida grave | Dark fantasy equip sound, a heavy weapon latch clank with a resonant metallic ring and a short low thump, powerful, cinematic game UI | 0,5–0,8 s | 2 |
| `ui_jogar` | ✅ IA | Começar a partida na sala | Impacto sub-grave, clangor enorme e um coro sombrio subindo | Epic dark fantasy play button, deep sub-bass impact, massive resonant metallic clang, short rising dark choir swell, cinematic, heavy, no cute elements | 1,5–2 s | 1 |
| `ui_partida_encontrada` **novo** | falta | Partida encontrada / sala cheia, vai começar | Trompa de guerra sobre tambores, clangor e grave, urgente | Match found alert, a dark heroic war horn call over heavy drums, a massive metallic clang and sub-bass hit, urgent, grand, cinematic | 2–3 s | 1 |
| `ui_xp` **novo** | falta | Tela de resultado: a barra de XP enchendo | Tique curtíssimo de cristal escuro com toque de metal, que sobe de tom | XP gain tick, a very short dark crystalline shimmer with a faint low metallic tap, designed to repeat quickly as a counter fills, no cute elements | 0,05–0,1 s | 3 (tons subindo) |
| `ui_xp_fim` | ✅ IA | A barra de XP termina de encher | Brilho que cresce e fecha num clangor grave | XP bar complete, a mystical shimmer swelling into a deep resonant metallic clang, short, grand | 0,8–1,2 s | 1 |
| `ui_patente` | ✅ IA | Nova patente (sobe de patente) | O momento maior: brilho místico, clangor enorme, grave fundo e coro sombrio | An epic rank promotion reveal. Starts with a sharp, mystical shimmer, followed by a massive, resonant metallic clang and a deep sub-bass impact, then a dark choir swell. Dark fantasy, grand, heavy and powerful. Cinematic game UI, no cute elements. | 3–4 s | 1 |
| `ui_recompensa` | ✅ IA (2) | Resgatar missão, recompensa diária, item novo | Exatamente a pegada de referência | An epic magical item reveal. Starts with a sharp, mystical shimmer, followed by a massive, resonant metallic clang and a deep sub-bass impact. Dark fantasy, grand, heavy and powerful. Cinematic game UI, no cute elements. | 2–3 s | 1 |
| `ui_notificacao` **novo** | falta | Convite, mensagem, aviso na sala | Dois toques de sino de ferro, graves, como num salão de pedra | Dark fantasy notification, two low resonant iron bell tones echoing in a stone hall, short, no cute elements | 0,5–0,8 s | 1 |

Para a barra de XP: gere `ui_xp_1`, `_2` e `_3` em tons cada vez mais agudos; o jogo toca um por passo da barra.

## Músicas

Um tema só, repetido em todas: o do menu (já feito) é a referência, e a melodia dele entra nas outras (as ferramentas de música costumam aceitar um áudio de referência ou "estender" uma faixa).

| Arquivo | No jogo | Quando toca | Como deve soar | Prompt (inglês) | Duração |
| --- | --- | --- | --- | --- | --- |
| `musica_menu` | — | Saiu em 19/11 | O lobby inteiro toca `musica_sala` | | |
| `musica_sala` | ✅ IA | Sala, em laço (troca com o menu num fade) | O mesmo tema, mais tenso e mais rítmico, sem melodia na frente | Tense pre-battle lobby music, same dark heroic theme reduced to rhythm, pulsing low strings, ticking percussion, building anticipation, no lead melody, seamless loop | 1–2 min, laço (o atual tem 30 s) |
| `musica_carregando` **novo** | falta | Tela de carregamento da partida | Ambiente grave e tenso que cresce devagar | Dark cinematic loading screen ambience, low drones, slow distant drums, wind, growing tension, seamless loop | 30–60 s, laço |
| `partida_inicio` | ✅ IA | Começo da partida | Chamada curta de guerra: tambor, metais e um rugido | Short battle start orchestral stinger, huge drum hit, heroic brass call, wolf roar layered underneath, ends cleanly | 3–5 s (o atual tem 13 s) |
| `musica_fim_proximo` | ✅ IA | Último minuto da partida | Pulso rápido de cordas e tambor, urgência | Final minute tension music, fast pulsing strings and driving drums, rising urgency, dark heroic, seamless loop | 30–60 s, laço |
| `vitoria` | ✅ IA | Seu time venceu | Fanfarra triunfal com o tema, coro aberto | Victory fanfare, triumphant orchestral brass and full choir playing a heroic theme, big drums, glorious ending | 6–10 s |
| `derrota` | ✅ IA (2) | Seu time perdeu | O tema em menor, lento, digno, sem ser fofo | Defeat stinger, the heroic theme slowed in a minor key, low strings and somber horn, dignified, fades out | 6–10 s |
| `empate` | ✅ IA | Empate | Suspenso, sem resolver: metais interrompidos | Draw stinger, unresolved orchestral phrase, suspended brass chord, neither triumphant nor sad | 4–6 s |
| `musica_resultado` | ✅ IA | Tela de resultado, depois da vinheta, em laço | Tema calmo, para ler o placar | Calm post-match results music, gentle orchestral version of the heroic theme, warm strings, seamless loop | 1–2 min, laço |

As músicas de 30 s ou menos repetem rápido: se a ferramenta deixar estender a faixa, vale gerar versões de 1–2 min do tema e da sala.

## Locutora: Iris

A locutora do jogo é a **Iris**. Todas as falas saem da mesma voz e com o mesmo tratamento: gere sempre com a mesma voz salva na ferramenta, uma fala por arquivo, sem efeito (sala e volume são aplicados depois, iguais para todas). Na partida ela só fala nos marcos; nada de narrar cada tiro.

Como descrever a voz, se a ferramenta pedir: *confident female arena announcer, warm but commanding, mid-low pitch, clear diction, energetic without shouting, epic fantasy esports tone*.

| Arquivo | No jogo | Quando toca | Fala (PT-BR) | Fala (inglês) |
| --- | --- | --- | --- | --- |
| `voz_prepare` | provisório | Começa a contagem | Preparem-se! | Prepare yourselves! |
| `voz_3`, `voz_2`, `voz_1` | provisório | Últimos 3 segundos | Três · Dois · Um | Three · Two · One |
| `voz_lutar` | provisório | A partida começa | Lutem! | Fight! |
| `voz_primeiro_abate` **novo** | falta | Primeiro abate da partida | Primeiro sangue! | First blood! |
| `voz_double_kill` | falta | 2 abates seguidos (5 s entre eles) | Abate duplo! | Double kill! |
| `voz_triple_kill` | falta | 3 seguidos | Abate triplo! | Triple kill! |
| `voz_quadra_kill` | falta | 4 seguidos | Abate quádruplo! | Quadra kill! |
| `voz_penta_kill` | falta | 5 seguidos | Penta abate! | Penta kill! |
| `voz_hexa_kill` | falta | 6 seguidos | Hexa abate! | Hexa kill! |
| `voz_hepta_kill` | falta | 7 seguidos | Hepta abate! | Hepta kill! |
| `voz_octa_kill` | falta | 8 seguidos | Octa abate! Imbatível! | Octa kill! Unstoppable! |
| `voz_team_kill` | falta | Você derrubou o time inimigo inteiro | Time eliminado! | Team wiped! |
| `voz_vinganca` | falta | Matou quem tinha te matado | Vingança! | Revenge! |
| `voz_multi_kill` | provisório | Reserva: qualquer sequência que não tenha fala própria | Sequência! | Multi kill! |
| `voz_especial_pronto` **novo** | falta | Sua carga do especial chegou a 100% | Especial pronto! | Special ready! |
| `voz_tempo` | provisório | Falta 1 minuto | Um minuto restante! | One minute remaining! |
| `voz_dez_segundos` **novo** | falta | Faltam 10 segundos | Dez segundos! | Ten seconds! |
| `voz_vitoria` | provisório | Fim, seu time venceu | Vitória! | Victory! |
| `voz_derrota` | provisório | Fim, seu time perdeu | Derrota. | Defeat. |
| `voz_empate` | provisório | Fim, empate | Empate! | Draw! |

Os provisórios são a voz masculina da Kenney, em inglês. As 11 falas da Iris já geradas estão separadas e esperam só o texto de cada uma para entrar no lugar delas.

Escolha um idioma e mantenha. Os nomes de arquivo são os mesmos nos dois.

## Armas

Cada arma tem **três versões do tiro**: o seu (estéreo, pesado, perto do ouvido), o dos outros (mono, mais seco, o jogo põe no lugar) e o de longe (abafado, quase só eco). Gere as três de uma vez, com o mesmo prompt e a distância trocada, para soarem como a mesma arma.

No nome, troque `<arma>` por: `m16a2` (fuzil AR-15/M16), `ak47`, `sg552` (fuzil compacto), `ump45` (submetralhadora), `m3` (escopeta de bomba), `m9` (pistola).

**No jogo: todos provisórios** (gravações reais de armas, de pacote grátis, em camadas). Nenhum falta.

| Arquivo | Quando toca | Como deve soar | Prompt (inglês) | Duração | Variações |
| --- | --- | --- | --- | --- | --- |
| `tiro_proprio_<arma>` | Você atira | Estalo forte, soco no peito, cauda curta de sala e um clique do mecanismo | First-person [assault rifle] gunshot, very close to the ear, sharp crack, heavy low-end punch, short room tail, subtle mechanical action click, punchy action game, stereo | 0,6–1 s | 3–4 |
| `tiro_<arma>` | Outro jogador atira perto | O mesmo tiro, menos grave, mais seco | Single [assault rifle] gunshot heard from 10 meters, sharp crack, medium punch, short tail, dry, mono | 0,5–0,8 s | 3–4 |
| `tiro_distante_<arma>` | Tiro longe | Estampido abafado com eco longo | Distant [assault rifle] gunshot from 80 meters, muffled thump, long echo across a warehouse, mono | 1–2 s | 1–2 |
| `recarga_<arma>` | Começa a recarregar | Pente saindo: trava, deslize, pente batendo na mão | [Rifle] reload start, magazine release button, magazine sliding out, crisp mechanical, close perspective | 0,6–1 s | 1 |
| `recarga_fim_<arma>` | Termina de recarregar | Pente entrando com estalo e ferrolho puxado | [Rifle] reload finish, magazine inserted with a firm click, charging handle pulled and released, crisp metallic | 0,6–1 s | 1 |
| `arma_vazia` | Aperta o gatilho sem bala | Clique seco e oco | Empty gun dry fire click, hollow metallic trigger snap, close | 0,1–0,2 s | 1 |
| `trocar_arma` | Troca de arma | Arma saindo do coldre, tecido e metal | Weapon draw, quick cloth rustle and metallic gun handling rattle, short | 0,3–0,5 s | 2 |
| `capsula` | Cápsula caindo no chão depois do tiro | Tilintar pequeno de latão | Single brass bullet casing dropping and bouncing on concrete, small metallic tinkle | 0,3–0,6 s | 3 |
| `cartucho` | Escopeta: cada cartucho entrando | Cartucho empurrado no tubo, clique | Shotgun shell loading into tube magazine, single push and click, close | 0,3–0,5 s | 3–5 |
| `faca` | Golpe de faca | Corte de ar rápido com raspar de lâmina | Fast knife slash whoosh with a thin metallic blade scrape | 0,2–0,4 s | 2 |
| `granada_lancar` | Arremessa a granada de choque | Pino puxado, arremesso com sopro | Grenade pin pull click, then a quick throw whoosh | 0,5–0,8 s | 3 |
| `granada_explosao` | Granada de choque explode | Estouro elétrico, não fogo: estalo forte, zumbido de eletricidade e chiado | Electric shock grenade detonation, loud sharp crack, heavy electrical discharge buzz and crackle, short sci-fi stun blast | 1–2 s | 3–5 |
| `impacto` | Bala acerta parede, chão | Batida curta com poeira | Bullet impact on concrete, short sharp thud with small debris | 0,2–0,4 s | 4–6 |
| `impacto_metal` | Bala acerta metal (contêineres) | Ping metálico com ricochete | Bullet impact on metal container, sharp ping with short ricochet whine | 0,3–0,6 s | 4–6 |

Nos prompts, troque `[assault rifle]` / `[Rifle]` pelo tipo da arma: *submachine gun*, *pump-action shotgun*, *9mm pistol*. A escopeta (`m3`) pede um soco bem maior e um *pump action rack* no fim do `tiro_proprio`.

## Humano

Passo e pouso têm uma versão por chão: sem sufixo é concreto, e os outros são `_metal`, `_madeira`, `_terra` e `_neve` (ex.: `passo_metal_1.wav`). O passo é o som que mais toca no jogo: precisa de 5 variações por chão, ou vira um tique-taque.

**No jogo: todos provisórios** (de pacote grátis), com as versões por chão. Nenhum falta.

| Arquivo | Quando toca | Como deve soar | Prompt (inglês) | Duração | Variações |
| --- | --- | --- | --- | --- | --- |
| `passo` (+ chãos) | Cada passo do humano | Bota militar, firme, com um pouco de equipamento chacoalhando | Single footstep, military combat boot on [concrete], firm heel strike, faint gear rattle, close, dry | 0,2–0,4 s | 5 por chão |
| `pulo` | Sai do chão | Impulso de bota e respiro curto | Soldier jump takeoff, boots pushing off the ground, short exhale grunt, gear rustle | 0,3–0,5 s | 4 |
| `pouso` (+ chãos) | Volta ao chão | Dois pés batendo juntos, equipamento sacudindo | Soldier landing on [concrete] after a jump, two boots impact, gear jolt, weighty | 0,3–0,5 s | 2 por chão |
| `dor` | Leva tiro | Gemido curto, masculino, contido | Short male pain grunt, soldier hit by a bullet, restrained, no words | 0,3–0,6 s | 6 |
| `morte` | Morre | Grito curto que corta, corpo e equipamento caindo | Male soldier death cry, short and cut off, followed by body and gear collapsing on the floor | 0,8–1,5 s | 6 |
| `respawn` | Renasce | Brilho mágico que se fecha, como teleporte | Respawn sound, dark magical energy swirl closing into a resonant metallic shimmer, short teleport, no cute elements | 0,8–1,2 s | 1 |

Para a `neve`, use *snow crunch*; `terra`, *dirt and gravel*; `madeira`, *hollow wooden floor*; `metal`, *metal grating catwalk*.

## Lobo

O lobo tem de soar **grande e perigoso**: grave, orgânico, nada de cachorro. Os passos do lobo usam os mesmos chãos do humano (`lobo_passo_metal`…). Hoje o rugido do R usa o mesmo som da transformação; com um `lobo_uivo` próprio, basta trocar uma linha (`WorldAudio.HowlSound`) e cada um ganha o seu.

| Arquivo | No jogo | Quando toca | Como deve soar | Prompt (inglês) | Duração | Variações |
| --- | --- | --- | --- | --- | --- | --- |
| `lobo_transformar` | provisório | Vira lobo | Ossos estalando, rosnado que cresce até um rugido | Werewolf transformation, bones cracking and stretching, flesh tearing, growl building into a powerful bestial roar | 1,5–2,5 s | 3 |
| `lobo_uivo` **novo** | falta | Rugido do R (recupera o escudo) | Rugido longo e agressivo que vira uivo, com peito | Massive aggressive werewolf roar turning into a long howl, deep chest resonance, primal, intimidating | 1,5–2 s | 2 |
| `lobo_destransformar` | provisório | Volta a humano | Rosnado que encolhe para um respiro humano | Werewolf turning back into a human, growl shrinking into a human gasp, bones settling | 1–1,5 s | 3 |
| `lobo_garra` | provisório | Golpe de garra (cada clique) | Corte de ar pesado com rosnado curto | Heavy claw swipe whoosh, fast air cut, short feral snarl, monster melee attack | 0,3–0,5 s | 6 |
| `lobo_garra_acerto` | provisório | A garra acerta alguém | Rasgo com impacto carnudo | Claw slash hit, flesh tearing impact with a wet rip, brutal but short | 0,3–0,5 s | 4 |
| `lobo_bote` | provisório | Bote no ar (botão direito) | Sopro de mergulho e rosnado | Werewolf diving pounce from the air, fast whoosh and aggressive snarl | 0,5–0,8 s | 3 |
| `lobo_especial` **novo** | falta | Dispara o especial (Q) | Rugido curto com descarga elétrica, como os raios na tela | Werewolf ultimate leap, explosive roar with a crackling electric lightning discharge, energy burst whoosh | 0,8–1,2 s | 2 |
| `lobo_especial_queda` **novo** | falta | Cai do especial | Pancada grave no chão com raios correndo | Heavy ground slam impact, deep boom with electric lightning crackling across the floor, debris | 1–1,5 s | 2 |
| `lobo_passo` (+ chãos) | provisório | Cada passo do lobo | Pata pesada com garras arranhando | Single heavy werewolf footstep on [concrete], large clawed foot, claws scraping, weighty | 0,2–0,4 s | 5 por chão |
| `lobo_pulo` | provisório | Lobo sai do chão | Impulso forte com bufada | Werewolf leap takeoff, powerful push off with a sharp snort | 0,3–0,5 s | 2 |
| `lobo_pouso` (+ chãos) | provisório | Lobo cai | Baque pesado e garras | Werewolf landing on [concrete], heavy thud and claws skidding | 0,4–0,6 s | 2 por chão |
| `lobo_dor` | provisório | Lobo leva dano | Ganido raivoso, não choroso | Werewolf pain snarl, angry yelp, short, beastly | 0,3–0,6 s | 6 |
| `lobo_morte` | provisório | Lobo morre | Uivo que morre e corpo grande caindo | Werewolf death, dying howl fading out, large body collapsing heavily | 1,2–2 s | 5 |
| `lobo_ofego` **novo** | falta | Correndo de quatro, em laço | Respiração pesada e ritmada de fera | Werewolf heavy panting breath while sprinting on all fours, rhythmic, seamless loop | 2–3 s, laço | 1 |

## Combate e avisos

São os sons que dizem ao jogador que ele acertou: curtos, secos e inconfundíveis, tocados só para ele (não no mundo). O abate é o mais importante: o "tic" da caveira tem de dar vontade de ouvir de novo.

| Arquivo | No jogo | Quando toca | Como deve soar | Prompt (inglês) | Duração | Variações |
| --- | --- | --- | --- | --- | --- | --- |
| `acerto` | provisório | Seu tiro acerta o corpo | Tique seco e alto de metal, sem cauda: é informação, tem de ser instantâneo | Hit marker sound, very short dry metallic tick, crisp and hard, no tail, competitive shooter UI feedback | 0,05–0,1 s | 3 |
| `acerto_cabeca` | provisório | Seu tiro acerta a cabeça | O mesmo tique com um anel de metal agudo que ressoa | Headshot hit marker, a hard metallic tick layered with a sharp high resonant metal ring, short, no cute elements | 0,2–0,4 s | 3 |
| `abate` | provisório | Você mata alguém (a caveira no centro da mira) | O "tic" que vicia: estalo de brilho místico, clique de metal pesado e um soco sub-grave curto | Kill confirm, a sharp dark mystical shimmer snap with a heavy metallic click and a short deep sub-bass punch, satisfying and addictive, cinematic, no cute elements | 0,3–0,5 s | 1 |
| `abate_cabeca` **novo** | falta | Abate com tiro na cabeça | O tic do abate com um clangor alto que ressoa | Headshot kill confirm, the kill click plus a high resonant metallic clang and a dark shimmer tail, heavy, no cute elements | 0,5–0,7 s | 1 |
| `assistencia` **novo** | falta | Você ajudou num abate | O abate em miniatura: só o clique e um fio de brilho | Assist confirm, a soft low metallic click with a faint dark shimmer, subtle | 0,2–0,4 s | 1 |
| `sequencia` **novo** | falta | Aparece DOUBLE KILL, TRIPLE KILL… (por baixo da fala da Iris) | Clangor enorme e grave fundo, como um carimbo de ferro | Multi-kill banner stinger, a massive resonant metallic clang with a deep sub-bass impact and a sharp dark mystical shimmer, grand, cinematic, no cute elements | 0,8–1,2 s | 1 |
| `especial_pronto` **novo** | falta | Carga do especial chegou a 100% | Energia sombria subindo, estalo elétrico, rosnado e o grave no fim | Ultimate ready, a rising dark energy swell with a sharp mystical shimmer and electric crackle, ending in a deep sub-bass hit with a low wolf growl, powerful, no cute elements | 1,2–1,8 s | 1 |
| `morreu` **novo** | falta | Você morre (só para você) | Batida grave abafada, o mundo sumindo | Player death sting, a low muffled heartbeat thump with a dark reverse swell, the world fading out, short | 1–1,5 s | 1 |
| `escudo_cheio` **novo** | falta | Fim do rugido, escudo recarregado | Barreira fechando com clangor que ressoa e brilho | Shield recharged, an energy barrier sealing with a deep resonant metallic clang and a mystical shimmer, heavy | 0,8–1,2 s | 1 |
| `escudo_quebrado` **novo** | falta | Seu escudo de lobo acaba | Cristal escuro quebrando com estalo elétrico e batida grave | Energy shield break, a dark crystalline shatter with an electric pop and a low thud, no cute elements | 0,5–0,8 s | 1 |

## Partida e mundo

O ambiente toca baixo o tempo todo, em laço, e dá o lugar: o Armazém usa hoje o `ambiente_hangar`. Nada de música nem de vozes no ambiente, só o espaço.

| Arquivo | No jogo | Quando toca | Como deve soar | Prompt (inglês) | Duração |
| --- | --- | --- | --- | --- | --- |
| `contagem` | provisório | Cada segundo da contagem, antes da partida e na sala | Tique grave de relógio com um brilho | Countdown tick, deep clock tick with a faint metallic shimmer, tense, short | 0,2–0,4 s |
| `ambiente_hangar` | provisório | Fundo do Armazém, em laço | Galpão grande de contêineres à noite: vento nas frestas, metal rangendo longe, zumbido de lâmpada | Large night warehouse ambience, container yard, wind through gaps, distant metal creaks, faint fluorescent hum, distant wolf howl rarely, seamless loop | 60–90 s, laço |
| `ambiente_vila` | provisório | Fundo da Vila, em laço | Vila abandonada à noite: vento, folhas, grilos, madeira | Abandoned village at night ambience, wind, rustling leaves, crickets, creaking wood, seamless loop | 60–90 s, laço |
| `ambiente_polar` | provisório | Fundo da Estação Polar, em laço | Vento gelado forte, neve batendo em metal | Arctic research station ambience, strong icy wind, snow hitting metal walls, low howling gusts, seamless loop | 60–90 s, laço |
| `ambiente_evento` **novo** | falta | De tempos em tempos, longe | Um uivo distante, ou metal caindo, para o mapa viver | Distant wolf howl echoing across an empty industrial area at night, far away, reverberant | 3–5 s |

O início, a vitória e a derrota estão em Músicas; o renascer, em Humano; as falas da partida, na Iris.

## Ordem para gerar

Do que mais muda a sensação do jogo para o que menos muda:

1. **Iris** — mandar o texto das 11 falas já gravadas e gerar as que faltam (sequências, primeiro sangue, especial pronto).
2. **Abate, acerto e acerto na cabeça** — o "tic" da caveira é o que mais se ouve quando o jogo está bom.
3. ~~`musica_menu`~~ — feito em 18/11 (uma versão de 1–2 min ainda ajudaria).
4. **Tiros das seis armas** (as três versões de cada).
5. **Lobo**: `lobo_uivo`, `lobo_especial`, `lobo_especial_queda`, garras.
6. **Interface que falta**: `ui_partida_encontrada`, `ui_xp`, `ui_notificacao` (o resto foi feito em 18/11).
7. ~~Vitória, derrota, empate, início de partida~~ — feitos em 18/11.
8. Passos e o resto.

Lote novo: acrescente as linhas dele em `MAP` no `src/tools/sons/preparar_ia.py` e rode `python3 src/tools/sons/preparar_ia.py <pasta dos arquivos> src/assets/audio`. Ele corta o silêncio, iguala o volume e fecha o laço das músicas.
