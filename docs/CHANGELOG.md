# Changelog

## Não lançado

- **Junção de 26/11: o que só aparecia com tudo junto.** Arma pega do chão não passa pela conferência de KP no servidor autoritativo (o AK pego virava a arma padrão e contava contra quem pegou). A queda não conta o corpo levado de lugar (mais de 2 m num passo: renascer, correção da rede); levar o lobo do alto de uma parede para o chão contava 40 m de queda e o matava. Na parede, o vão vem antes da coisa redonda: o batente dentro do vão virava a face da parede, e o lobo único (4,9 m/s na parede) soltava no meio do vão (Neon x −32 z −10: 5,5 → 11,8 m). O corpo morto não mede o chão fora da árvore (30 erros na troca de cena do "Voltar à sala").
- **Obra: o tiro não para mais no ar em cima das pilhas de cimento** (achado na junção de 26/11: com a câmera nos olhos do corpo, 12 cm mais alta, `TiroDeCimaObra` pegou 2 tiros dos andaimes parados em "caixa invisível"). A colisão das 132 pilhas de sacos (`CementC1`/`CementC2`) era a caixa inteira da pilha, que cobre os cantos vazios dela; agora é o casco convexo dos sacos (`colisao_exata.gd`, lista `CONVEXAS`; 4 formas, ~150 pontos cada, em vez dos 4 mil triângulos da malha). Antes: 2 de 10 tiros de cima parados no ar; depois: 0, e o `MapaObra` passa (7 de 7 bots saem da base).
- **O lobo: um só, canhão de vidro, e por tempo** (decisões do Jhon, 26/11: "remover 'Classes de lobo' do jogo, deixar apenas melhorias de lobo"; "arquétipo do lobo: canhão de vidro"; "um tempo limite para usar o lobo — ao passar esse tempo volta a ser humano, deve avisar o player — e cooldown para voltar a ser lobo, para forçar os players a jogarem com armas"; 30 s de lobo e 20 s de espera).
  - **Sem classes.** Saíram o Bruto e o Ligeiro: do catálogo (`raca_bruto`, `raca_ligeiro`), da loja (a categoria "Classes de lobo"), do inventário (o espaço "Classe" e a aba), do detalhe e do cartão do item, do ícone, do perfil (a "classe de lobo favorita") e da semente do servidor (`exportar_dados.py`: 53 itens, eram 55). O lobo é um só para jogador e bots, `src/resources/breeds/lobo.tres` (no lugar do `bruto.tres`, mesmo uid), **no meio dos dois**: 4,33 m/s (eram 4,15 e 4,50), de quatro 7,44 (7,14 e 7,73), na parede 4,94 (4,75 e 5,14), o pulo sobe 2,09 m (1,67 e 2,56), garra 65 (75 e 55), bônus aéreo 1,75. As melhorias continuam e multiplicam uma cópia dele. O perfil salvo com classe carrega normal (o campo é ignorado). Online, o pacote do conjunto leva só as melhorias (o servidor de partida e o jogo novos vão juntos).
  - **Canhão de vidro.** A garra tira 104 de um humano (era 120 no Bruto): dois golpes matam os 200. Mas o escudo cai de 200 para **100** e o corpo **leva o tiro inteiro** (`BodyResistance` 1,0; era 0,6 no Bruto). O escudo escorre 5,5 por segundo: dura uns 18 s, o meio dos 22 e 15 s de antes. O rugido devolve os 100.
  - **Tempo de lobo** (`FormController`: `WolfTimeLimit` 30 s, `WolfCooldown` 20 s, `WolfWarnAt` 5 s). Acabou o tempo, volta a humano sozinho (pela transição de sempre, com a arma que tinha). Nos últimos 5 s: "Você volta a humano em 5 s", o bipe `contagem` e a barra de baixo vermelha, pulsando ("HUMANO EM 4 S"; antes, "LOBO · 24 S"). Voltar a humano — pelo tempo ou pela tecla — começa a espera: a barra enche ("ESPERE 12 S"), o espaço 3 mostra os segundos ("12s"), e apertar 3 avisa "Lobo de novo em 12 s" (`ui_negado`). Morrer zera os dois. O bot respeita a espera. Não vale no modo em que o time é lobo a partida toda. Online, o relógio é o do dono: o boneco e o corpo do servidor espelham a forma e não voltam sozinhos. O guia da partida (E) explica.
  - Testes novos: `TempoDeLobo` (33 verificações; o relógio encurtado para 4/3/2 s: o aviso sai a 2,02 s, a volta a 4,02 s; a espera, o espaço 3, a tecla 1, a morte, o bot e o boneco) e `LoboCanhaoDeVidro` (escudo 100, o corpo leva 40 de 40, a garra 104, o rugido não passa de 100, nenhuma classe no catálogo). Alinhados ao lobo único, dizendo por quê: `VelocidadeNoChao`, `AlturaDoPulo`, `CorridaNaParede`, `Medidas`, `DanoDaGarra` e o de unidade da garra. A bancada (`BecomeWolf`) desliga o relógio nos testes que não são sobre ele.
  - **Não testado:** não jogado nem online; o HUD só por teste (o Docker não estava de pé para foto). O banco de produção ainda tem as linhas `raca_bruto` e `raca_ligeiro` no catálogo (a semente só acrescenta): o jogo não as mostra; dá para desligar pelo painel.
- **Humano: agachado fundo, a câmera nos olhos do corpo visto, mais devagar sem deslizar e a faca mais rápida** (Jhon, 26/11: "o humano tá agachando mais do que na animação de 3ª pessoa. O nível de agachamento deve ser idêntico ao de 3ª pessoa, extremamente preciso. A animação de agachar em 3ª pessoa tá muito curta, precisa agachar mais"; "andar para frente com humano tá deslizando no mapa, ele deve andar um pouco mais devagar"; "aumentar velocidade de movimento com faca equipada"). Medido sem tela:
  - **Agachado do corpo visto**: o único clipe agachado (Mixamo) mal dobra os joelhos — os olhos desciam ~10 cm, enquanto a câmera descia 87,5. Agora o clipe é aprofundado ao carregar (`CharacterAnimator.Kneel`): o quadril desce até 0,6 m, o mesmo tanto no passo inteiro (o que a perna de apoio deixa), com as pernas dobradas por cinemática inversa e os pés no lugar. Os olhos do corpo descem ~50 cm (Eve 159,3 → 108,6 cm; Barut 159,1 → 109,8). Parado agachado, o pé não anda mais no lugar (era 31 cm/s); a pele não entra no chão (−0,9 cm, de pé −1,1); andando agachado o corpo sobe e desce 3,6 cm (o rascunho de manhã, 9). A mistura parado/andando agachado saiu (`CrouchWalkSpeed` 1,9 → 0,6): no meio dela a ponta do pé entrava 16 cm no chão.
  - **A câmera é o olho do corpo**: em pé e agachado, a câmera da 1ª pessoa fica no meio dos olhos do corpo que os outros veem (o ponto medido na malha da cabeça, `CharacterSkin.EyeInHead`, `src/tools/personagens/olhos.py`). O corpo desenhado é lido parado e de cabeça reta (a arma na mão mexe no tronco: montar a conta só pelos clipes errava 13 a 20 cm) e dá os dois números à câmera (`Player.TuneHumanEye`). Antes → depois, câmera menos olhos do corpo: em pé −11,8 → 0,0 cm (a câmera estava 12 cm abaixo dos olhos; sobe para 159 cm); agachado −49,3 → 0,0 cm (60 → 109 cm). Andando, a câmera fica a 0,2 cm (em pé) e 0,8 cm (agachado) da média dos olhos. **Isso muda o jogo**: agachar abaixa ~50 cm, não 87,5, e a câmera em pé vê 12 cm mais alto. A cápsula agachada do humano passa a 1,3 m (era 1,0, a do lobo, que não muda) para levar a cabeça: passar agachado por baixo de algo pede 1,3 m de vão. O boneco da rede e o servidor usam a mesma altura.
  - **Mais devagar, sem deslizar**: o humano corre a 3,0 m/s (era 3,46; `HumanSpeedBonus` 1,08 → 0,9375), e a corrida toca a 0,88× (`RunClipRate`): no corpo do jogo ela anda 3,41 m/s a 1×. O pé de apoio, antes → depois: de frente 0,05 → 0,01 m/s (Eve; Barut 0,11 → 0,06), de costas 0,79 → 0,33, de lado 2,70 → 2,25 (só há strafe andando), agachado 0,97 → 0,70 (o andar agachado anda 0,67 m/s e não acelera). Agachado 1,35 m/s, Shift 1,26, pulo correndo vai ~2,25 m (a altura não muda). O lobo não muda (a base dele é o `HumanSpeed` da cena).
  - **Faca**: ×1,037 → ×1,28 (3,58 → 3,84 m/s), abaixo do lobo a pé. A corrida não toca mais rápido que o arquivo (regra 9): com a faca o pé de apoio fica 0,8–0,9 m/s atrás do corpo. Para tirar isso, só acelerando a corrida da faca (1,13×), o que a regra 9 não deixa sem conversar.
  - Testes novos: `OlhoNoCorpoEve` e `OlhoNoCorpoBarut` (câmera nos olhos, até 1 cm, em pé e agachado, parado e andando; agachado desce ≥ 45 cm; pele, peça da cabeça e pé parado) — reprovam antes (11,8 e 49,3 cm de diferença) e passam depois; `PassadaHumana` (o pé de apoio; com a faca, só medido). Alinhados: `VelocidadeNoChao` e `Medidas` (3,0 m/s, agachado 1,35, faca 3,84, pulo 2,25 m). Não foi visto em tela nem jogado online.
- **Arma largada aparece para todos online, e qualquer um pega** (Jhon, 26/11, alpha fechado: "arma largada não aparece pros outros jogadores do mapa, amigo e inimigo. Somente eu consigo ver minhas armas e as armas do meu time. Devo conseguir pegar a arma de qualquer jogador na partida"). **Causa:** a arma no chão nunca foi pela rede. Cada máquina só via o que os corpos dela soltavam (o jogador e os bots que ela move); a arma solta com G não saía da tela de quem soltou, e o boneco de outra máquina que morria soltava uma cópia só da tela de quem via (que dava para pegar sem ninguém saber). Medido no código antigo, com o jogo de verdade ligado a um servidor de partida no teste: soltei a M4A4 e o outro jogador recebeu 0 pacotes de arma no chão (repasse e autoritativo); o boneco dele morreu aqui e apareceu 1 arma a mais só nesta tela. Na autoritativa havia mais um defeito: a arma pega do chão que não é da loja de quem pegou virava a arma padrão no servidor (`AllowedWeapon`), e o tiro com ela seria recusado e contado contra o jogador.
  - **Agora a arma no chão é da partida** (pacotes `Drop`, `Take` e `Taken`, nos dois modos; `NetMatch.Pickups`, `HostedMatch.Pickups`): quem solta (o jogador, ou a máquina dona do bot) avisa, com um número único da arma; o servidor de partida guarda as que estão no chão e passa a todos, amigo e inimigo; cada tela mostra a arma caindo de onde saiu, com a mesma força e a mesma munição. Pegar é pedir: só o corpo da própria máquina pede (o boneco de outra máquina nunca pega nem solta), o servidor dá ao **primeiro pedido** e avisa todos, e a arma some de todas as telas; o segundo pedido não leva nada. Quem entra no meio recebe as que estão no chão. Some depois de 30 s ou com mais de 20 no chão (a mais velha), em todas as telas. O servidor confere que o número é de quem fala, que o corpo é dele (ou de um bot dele), que a arma existe e a munição cabe; na autoritativa, que é uma das armas que o corpo carrega, e que quem pega está vivo. A arma pega do chão passa a valer no corpo do servidor (sem virar a padrão nem contar contra). Offline nada muda.
  - Testes novos `ArmaNoChaoRepasse` (17 verificações) e `ArmaNoChaoAutoritativo` (19): a arma que solto chega ao outro (M4A4, 30 no pente); ele pede primeiro e leva, a arma some da minha tela e do servidor, ninguém leva duas vezes; a arma que ele solta aparece aqui e eu pego com a munição dela (AK, 17 no pente); o bot desta máquina morre e a arma dele chega ao outro; o boneco dele morre aqui sem deixar cópia local; quem volta recebe as 2 que estão no chão, com a idade delas; com 21, a mais velha some para todos. **Não rodados:** `testar_online.sh` e `testar_servidor.sh` (o Docker estava parado), nem partida com gente; sem foto (o Docker com tela também estava parado).
- **Morrer não tira KP, ganha-se 25% mais, e as moedas só valem com o servidor conferindo** (Jhon, 26/11, alpha fechado: "possível perda de KP ao morrer. Ao morrer, não pode perder KP. E devem ganhar um pouco mais por partida"; "validação de compra no servidor", decidido: só moedas, sem dinheiro real).
  - **A causa da perda**: a morte tirava 100 KP (`WpLostOnDeath`), e o conjunto pago era cobrado de novo a cada renascer. Medido no teste novo `KpNaoCaiAoMorrer` com o código antigo: 450 KP → 350 ao morrer; renasceu com o conjunto de 260 → 90; morreu → 0, e o conjunto já não cabia — não renascia com a arma que acabou de pagar (o "desbloqueei e morri e resetou" do testador). Com o novo: 450 → 450 ao morrer; 450 → 190 na primeira vez; morreu e renasceu com o mesmo → 190.
  - **O modelo novo**: morrer não tira nada, e a arma paga com KP fica **desbloqueada até o fim da partida** (`MatchKp`, `src/scripts/domain/MatchKp.cs`): renascer com ela não cobra, e o conjunto custa só os itens ainda não pagos nela (o cartão mostra "JÁ PAGO" ou "380 de 440"). Efeito na economia: o KP vira progresso dentro da partida — juntar abates libera as armas melhores para o resto dela, em vez de comprar cada vida; o padrão continua grátis e pagando +50.
  - **Ganhos +25%**: abate 100 → 125, abate de lobo 70 → 88, assistência 35 → 44. Uma partida típica (10 abates, 4 assistências, 8 mortes no padrão) rendia 740 KP e agora rende 1826.
  - **O que o servidor já conferia** (sem mudança): a compra na loja (preço do catálogo dele, desconto, patente, item existente e saldo, numa transação que trava a carteira); o XP e as Presas da partida (só do resultado que o servidor de partida manda com a chave da frota, partida em curso e uma vez só); o progresso e o resgate das missões; a arma, a classe e as melhorias que a conta leva para a partida autoritativa. O jogo nunca credita moeda: só espelha a conta.
  - **O que passou a conferir**: (1) **o mesmo pedido de compra repetido compra uma vez só** — o jogo manda um número por clique (`requestId`), o servidor guarda o recibo (`purchase_requests`, passo 3 do banco) e devolve o mesmo sem cobrar; número de outro item é recusado (`pedido_repetido`), número estranho também (`pedido_invalido`). Teste novo do servidor: o mesmo pedido sete vezes, seis ao mesmo tempo, cobra uma e dá um uso. (2) **O gasto de KP na partida autoritativa**: a arma paga que chega no estado do jogador só vale se o KP que ele pode ter ganhado **pelo placar do servidor** cobre o preço (`HostedMatch.PaidWeapon`, `MatchKp.MostEarned`); senão vira a padrão e conta contra a conta uma vez. A arma do conjunto padrão é de graça. No repasse cada jogo decide o próprio corpo e o KP não é conferido. Teste novo `KpNoServidor`.
  - Testes: `KpNaoCaiAoMorrer` e `KpNoServidor` (novos), `RenasceHumano` alinhado (o WP não cai mais; decisão do Jhon, 26/11), unidade `MatchKpTests` (4 novos).
- **Trocar as teclas nas Opções** (Jhon, 26/11: "opção de editar controles nas configurações do game"). A aba **Controles** das Opções (no saguão e no menu do Esc da partida) lista as 22 ações trocáveis em quatro grupos (Movimento, Combate, Armas, Interface), cada uma com a tecla de agora. Clicar na tecla e apertar a nova (teclado ou mouse, inclusive botão do meio, roda e laterais) troca na hora (`KeyBindings`, `KeyCapture`); o Esc cancela a captura sem fechar o menu. Tecla já usada por outra ação pergunta logo abaixo da faixa e, confirmando, troca as duas. `↺` volta uma tecla à de fábrica; "Restaurar padrão" volta todas. A troca fica em `user://settings.json` (`Keys`, só o que difere do padrão) e vale ao reabrir o jogo; arquivo editado à mão com duas ações na mesma tecla ou com o Esc volta à de fábrica nelas. O padrão de fábrica continua o de sempre, agora numa tabela só no `InputSetup`. Ficam fixos o Esc (menu) e as teclas de número do rádio. As dicas do HUD que citam tecla (caixinhas do uivo e do especial, "R · RECARREGAR", "PRONTO · Q ARMA", a faixa da transformação, o guia, "Segure Tab") e a tabela de Controles do menu do Esc passam a mostrar a tecla de agora; as frases longas do guia que citam tecla no meio do texto ("Clique atira, clique direito mira, R recarrega") ainda não. O vigia do Shift/Ctrl preso (`StuckKeys`) só solta o andar devagar e o agachar quando eles estão mesmo no Shift e no Ctrl. Teste novo `TrocarTeclas` (30 verificações, sem tela): pular no K e o corpo pula com o K (99 cm) e não com o Espaço; coronhada no botão do meio; conflito pergunta e troca as duas; o Esc não se troca; padrão de uma e de todas; salvo e lido de novo do arquivo; e o mesmo pela tela, no menu do Esc (clicar, apertar, conflito, Esc cancela sem fechar o menu, Restaurar padrão).
- **Granada: parede protege, 300 no centro, e som novo com o pavio que se ouve** (Jhon, 26/11: "área de efeito da granada atravessa paredes, não deve acontecer mais"; "granada forte demais: deixar mais balanceada"; "som de explosão de granada melhor AAA+, com fuse perceptível").
  - **Parede, barreira e fresta protegem.** A explosão não conferia nada: tirava o dano cheio atrás de parede, de parede invisível e por fresta. Agora só fere quem ela enxerga por algum dos 6 pontos do corpo (cabeça, peito, meio, joelhos e os dois lados do peito), com uma bola de 6 cm em vez de um raio (`Grenade.Exposed`, `SightGap`): a junta entre duas peças da parede ou a fresta de uma porta não deixam passar, e quem aparece por cima de um muro baixo leva. Vale offline, no repasse (a máquina de quem lançou confere) e no servidor autoritativo (`HitCheck` passou a conferir visão para a granada, também a de bot, com um raio só — quem passa no jogo passa lá). Medido (`GranadaParede`), antes → depois: atrás de parede de 3 m 200 → 0; atrás de barreira 200 → 0; por uma fresta de 4 cm 200 → 0; por cima de um muro de 0,8 m 200 → 200. No servidor (`VidaNoServidor`): a granada do outro lado da parede valia (200 → 100) e agora é recusada; a que está à vista vale.
  - **300 no centro** (era 420), raio 9 m: mata quem tem 200 de vida a até 3 m (era 4,7); a 4 m tira 164 (antes matava), a 6 m 100. Contra o lobo de hoje (200 + 200 de escudo; o Bruto leva 60% no corpo) ela não matava de vida cheia nem antes: o máximo caiu de 252 para 180 (Bruto) e de 328 para 234 (Ligeiro). Se o lobo virar canhão de vidro (escudo 100, corpo levando 100%), colada tira os 300 e a 2 m, 233.
  - **O som.** A explosão era o "Sci-Fi Sounds" da Kenney: um inchaço que chegava ao pico em 110–258 ms, sem estalo, e sumia em 0,75–1,1 s. As novas são feitas por código (`src/tools/sons/granada.py`): o estalo da onda de choque, um soco grave que desce de 90 para 38 Hz, o corpo de 80–250 Hz (que se ouve em fone e notebook), o bafo de ar, estilhaços (cliques metálicos e terra caindo), o ronco que rola e dois ecos. Medido nos arquivos: pico em 5–12 ms, a −1 dBFS; cai 40 dB em ~2 s; energia 31–39% abaixo de 80 Hz, 30–39% de 80 a 250 Hz, ~10% nos médios e 8–10% de 1 a 4 kHz (a primeira tentativa tinha 80% abaixo de 80 Hz e sumia em alto-falante pequeno). Três versões, escolhidas por quem ouve (`WorldAudio.ExplosionName`): de perto; de longe (mais de 30 m: sem agudo, ronco e eco mais longos, 3,1 s); e debaixo de teto (teto até 6 m acima: reflexões curtas e o grave da sala, 1 s). A explosão se ouve até 140 m (era o raio do ruído, 54 m). O quinto arquivo antigo saiu (tocava no meio dos novos).
  - **O pavio se ouve.** A granada voava muda. Agora ela chia (`granada_pavio`, em laço, preso a ela, 14 m) do arremesso até a explosão, e no primeiro toque, quando o pavio começa a contar, toca o fim do pavio (`granada_pavio_fim`, 1,2 s: o chiado sobe e cliques secos que se aceleram de 0,32 s para 0,05 s até estourar), no ritmo do `Fuse`. A granada de outro jogador online também chia (ela é encenada na sua tela).
  - Testes novos `GranadaParede` e `GranadaSom`; `VidaNoServidor` ganhou os dois casos da granada. Com o código antigo, `GranadaParede` e `VidaNoServidor` reprovam.
- **Corpo do morto: fica no chão e some por igual, sem mostrar o de dentro** (Jhon, 26/11: "corpo do morto afundando/ficando transparente, dá para ver o interior dos personagens. Ele deve ficar transparente por igual, sem poder ver o interior do corpo. E não deve afundar, deve ficar no limite do chão"). Medido com a pele deformada pelo esqueleto, na pose que se desenha (com o rabo simulado), contra o chão logo abaixo, a cada quadro da morte, nos humanos e lobos dos dois times (teste novo `CorpoMortoNoChao`):
  - **O de dentro aparecia** no sumiço do humano: o corpo desbotava com o "AlphaDepthPrePass" do Godot, que só escreve a profundidade onde o alfa passa de 0,99 — no primeiro quadro do sumiço ela sumia, e olhos, dentes, cabelo de baixo e forro se desenhavam por cima da pele (72 de 72 quadros do sumiço, na Eve e no Barut). Agora cada superfície some em duas passadas: primeiro só a profundidade do corpo inteiro (`corpo_profundidade.gdshader`, que respeita o recorte do cabelo), depois a cor transparente, que só passa na pele de fora: 0 quadros. A arma na mão e os olhos acesos do lobo somem pela transparência da própria malha.
  - **O lobo não desbotava** (sumia de uma vez 1,2 s depois do clipe) e **ficava enterrado**: o rabo, uma mola com gravidade que só bate nas pernas, pendia 42 cm para dentro do chão com o lobo deitado (o "mutant dying" do Mixamo), do meio da queda até sumir. Agora o lobo morto some como o humano (o mesmo tempo, `lobo_sumindo.gdshader` e `lobo_profundidade.gdshader`, que dividem o `lobo.gdshaderinc` com o lobo vivo e balançam o pelo igual), e o rabo encosta num plano no chão (`WolfVisual.Corpse`): 0 cm dentro.
  - **A Eve morta pairava 5 cm**: todo humano descia 12 cm fixos ao cair (`CorpseDrop`, feito para o X Bot). Agora o corpo (humano e lobo) é levado ao chão pela medida: caindo, só sobe o que entraria no chão; caído, encosta (`DeadBody`). Eve 4,9 → 0 cm acima; Barut 0,2 → 0,03; lobos 42 cm dentro → 0.
  - Vale para o seu corpo, o dos bots e o dos bonecos da rede (o teste usa o boneco). O `OlhosDoLobo` passa a ler o neon no `lobo.gdshaderinc`. Passam também `RenasceInteiro`, `RenasceHumano`, `EfeitosDoLobo`, `RaboDoLobo`, `BonecoLobo`, `OlhosDoLobo` e `VitrineNoPedestal`. **Não visto em tela**: o Docker não subiu nesta rodada, então a transparência "por igual" está provada pelo material (cada superfície com a profundidade antes da cor), não por foto. Enquanto some, o corpo esconde o que for transparente atrás dele (vidro, fumaça), por 1,2 s.
- **Lobo: especial mais forte e em área, teto no clique em rajada, e dano de queda** (Jhon, 26/11: "Ultimate precisa de mais poder de dano e em área, afetando mais pessoas ao redor e mais dano ao alvo principal"; "Spam de clique no primário e secundário do ataque do lobo pode estar passando do limite pretendido"; "Humano começar a tomar dano de queda, lobo também, mas menos. Não deve tomar dano de queda no ataque secundário e com a habilidade suprema").
  - **O especial** (Q): o alvo principal leva 3,7 garras (`HeavyDamageBonus`; era 2,6 no código, o AJUSTES dizia 2,1): com a garra de 65 do lobo do meio, 385 num humano (mata de vida cheia) e 240 num lobo de 300. Em volta, até 3,5 m (`PounceAreaRadius`; era 30% e só a 1,8 m): 60% até 1,5 m, caindo até 30% na borda (`CombatRules.PounceSplash`), e parede no meio protege. O anel da mira tem o tamanho da área. Medido com o Bruto (garra 75) num humano (`EspecialEmArea`): principal 444 (era 312), a 2,0 m 232 (52%), a 3,1 m 160 (36%), a 3,3 m atrás de uma parede 0, a 5,5 m 0.
  - **O clique em rajada**: um clique por quadro por 4 s, colado num humano (`LoboSemSpam`). Só a garrada e garrada com secundário no chão já estavam no ritmo (11 golpes, 375–390 de dano por segundo, uma a cada 0,35 s): não mudou. O furo era o ciclo pulo-bote-garra: 17 golpes, 870/s, golpes a 0,1 s um do outro; e o bote, um por pulo: 6 em 4 s, 540/s. Agora nenhum golpe que fere sai a menos de uma garrada (0,344 s, `Weapon.StrikeSpacing`) do anterior — o bote espera no ar, a garrada depois do bote vem no mesmo compasso (era 0,05 s) — e o bote tem espera de 1 s (`LeapCooldown`, era 0): ciclo 285/s com golpes a 0,35 s, só bote 4 em 4 s (360/s). O bote que corta uma garrada ainda no ar continua saindo na hora (23/11).
  - **No servidor autoritativo**: o balde de dano do lobo aceitava uns 2.000 por segundo, e a rajada passava inteira. Agora o servidor conta o compasso pelo número do comando de quem bateu (`WolfStrikePace`): no mesmo alvo, um golpe do lobo por garrada e um bote por segundo (o mesmo golpe pega vários alvos), e o teto do acerto é o do golpe dito (garrada, bote ou especial), não o maior dos três. `LoboSemSpamNoServidor`: garradas a cada 0,1 s por 4 s, 40 de 40 aceitas → 10; botes a cada 0,5 s, 8 de 8 → 4.
  - **Dano de queda** (não existia): o humano se fere a partir de 4 m, 25 por metro (12 m mata); o lobo a partir de 7 m, metade por metro (12,5; os 300 dele só a 31 m). Conta do ponto mais alto do voo até onde pousou; parede e escada contam como apoio. Não fere no bote, no especial (do começo ao fim, inclusive o resto da queda depois do voo) e no salto da parede. Fere pela vida de quem caiu (`Health.TakeOwnDamage`: a proteção de quem nasceu não vale), e se matar o feed diz "Queda". O jogador e o bot pela mesma peça (`src/scripts/actors/body/FallTracker.cs`, números em `src/resources/body/queda.tres`). Medido (`DanoDeQueda`): humano solto de ~2 m, 0; de ~8 m, 103; de ~13 m, morre com "Queda" no feed; lobo de ~6 m, 0; de ~12 m, 38 (com a resistência do corpo de hoje); bote de ~12 m, 0 (sem a isenção, 46); especial de uma plataforma de 10 m, 0 (sem a isenção, 36).
  - Testes novos: `LoboSemSpam`, `LoboSemSpamNoServidor`, `EspecialEmArea`, `DanoDeQueda`, e de unidade (o especial, a área, o compasso, a queda, o `WolfStrikePace`). `GarraDepoisDoBote` mudou de propósito (a garra depois do bote sai 0,19 s depois do dano, não 0,05). Os `TiroDeCima*` (que levam o jogador de telhado em telhado) desligam a queda: as escorregadas da beira somadas o matavam no meio do teste. **Não joguei** nem rodei online com gente; o servidor de partida precisa ir junto com o jogo novo.
- **Online fluido com a rede ruim: a câmera e os outros sem tranco e sem voltar no tempo** (Jhon, 26/11: "microtravamentos no servidor, às vezes os personagens voltam no tempo e a tela fica tremendo com glitches, isso não pode acontecer, precisa ser um jogo fluido, independente da condição do servidor"). Decisão 0018. Medido com o jogo de verdade ligado a um servidor de partida no teste, que agora simula atraso, jitter, perda e o servidor engasgando (`TestMatchServer`): o jogador e o outro correndo em círculo, quadro a quadro, nos dois modos (testes novos `FluidezAutoritativo` e `FluidezRepasse`; tabela no `docs/SERVIDOR.md`). Cinco causas:
  - **O relógio da partida online era o de parede do sistema**, lido em qualquer ponto do quadro, e o boneco anda no passo da física: tremia mesmo com a rede limpa. Agora conta os passos de física (`NetMatch.LocalSeconds`).
  - **O desenho dos outros pulava**: o estado que chegava mais cedo puxava todo mundo para a frente, e no engasgo do servidor o desenho passava do último estado, adivinhava e voltava. Agora tem relógio próprio que só muda de ritmo (até 10%, a no máximo 6 por segundo), nunca pula nem volta, fica mais atrás com jitter (até +80 ms) e freia quando os estados param de chegar (`ServerClock`). O que sobra de degrau na pose do boneco escorrega no desenho (`Bot.SmoothNetJump`).
  - **A correção do próprio corpo dava tranco**: começava na velocidade máxima (30 cm faziam a câmera saltar a 6 m/s, picos de 42 m/s) e entrava também no deslocamento do passo, recuando o desenho por um quadro. Agora escorrega numa mola amortecida (`VisualSmoothing.CorrectionSpring` 12: 30 cm somem em ~0,3 s, no máximo ~1,3 m/s a mais), e um estado velho que chega depois de um novo não corrige de novo. Com o servidor corrigindo o corpo 30 cm a cada 2 s: antes 9 trancos e 3 a 6 voltas no tempo da câmera por condição; agora 0 e 0 em todas.
  - **A fila de comandos do servidor apressava no engasgo dele mesmo**: recuperando os passos que devia, via a fila funda e consumia dois por passo, e quem via esse corpo o via correr o dobro. Agora, com vários passos no mesmo quadro (o servidor recuperando), não apressa (`CommandBuffer.WantedThisTick(catchingUp)`); fora disso, como antes. Segurar a fila funda o tempo todo foi testado e descartado: o servidor ficava mais atrás e o jogo voltava para a parede mais tarde (`ParedeCorrecao`).
  - **No repasse o boneco perseguia a última notícia** e voltava para trás com a rede irregular. O estado agora leva o instante de quem mandou (`NetState.Time`, no fim: jogo antigo ignora) e o boneco é desenhado entre dois estados, como na autoritativa. Antes até 11 trancos; agora 0 em todas as redes.
  - Boneco autoritativo: antes 194 a 295 trancos por condição (inflado no teste sem tela pelo relógio de parede; com o relógio no passo e o resto antigo, 1 a 14 trancos e 2 voltas no tempo no engasgo); agora 0, e 1 tranco no limite (2,2 m/s) no engasgo.
  - **Ainda corrige**: o esbarrão entre dois jogadores (o jogo vê o outro no passado, o servidor no presente; perto um do outro, 4 ou 5 correções de 5 a 15 cm seguidas). Agora escorregam; para não haver, o corpo do inimigo teria de não ser parede no movimento (decisão de jogo, não mexi).
  - Não joguei nem rodei o `testar_online.sh` (pediria parar o servidor de partida do Docker do Jhon). Vale online depois do jogo novo e do servidor de partida novo (`docker compose up -d --build partida`), juntos: o servidor muda a fila de comandos, e o jogo antigo no repasse não manda o instante.
- **Lobo na parede: gruda e dá a volta em poste, haste de placa e coluna fina; corre a placa e a parede curta** (Jhon, 26/11: "o lobo não gruda em paredes e objetos muito curtos ou pequenos, como um poste ou placa de rua"). Em cima do conserto dos degraus e vãos (entrada abaixo).
  - **Causa**: o poste fino era "a ponta de uma parede fina" (face seguinte com menos de 25 cm): o lobo grudava e, no quadro seguinte, saía reto e caía — na haste de 12 cm, 1 quadro na parede e 10 m de queda. Na placa e na parede curta, chegando pela ponta, a sonda achava a aresta antes da face e o lobo grudava torto.
  - **Conserto** (`Player.WallRun.cs`): objeto cujo corte, na altura do corpo, cabe em 0,6 m (`WallPoleMaxWidth`) e está solto (nada em volta na altura do peito) é tratado como o tonel: o lobo dá a volta, com teto de giro (`WallOrbitMaxTurn`, 360°/s). Mourão de grade, batente e decoração colada na parede não contam como soltos. Saltou de um poste, não regruda nele por outro lado até tocar o chão (o objeto é a forma de colisão, não o corpo do mapa). Na placa e na parede curta, na aresta ele espera os poucos centímetros até ficar ao lado da face e gruda reto; já ao lado de uma face (fachada com enfeite), gruda na hora. (Esperando sempre, numa fachada do Distrito Neon — x −40, z 2,5 — o lobo grudava um quadro depois numa normal torta e soltava com 0,56 m; o `ParedeNoNeon` pegou.) A mureta de 25 cm, que o conserto dos degraus fazia sair reto na ponta, voltou a contornar.
  - **Medido** (sem tela), antes (o `main` de 25/11) → depois: poste de 12 cm, 1 quadro na parede e caiu → 3 s sem soltar (1.084°); poste quadrado de 28 cm, 1.472° em 3 s → 1.065° (teto de giro); poste redondo de 12 cm, 1.872° → 1.083°; coluna de 50 cm, soltava 8 de 180 quadros → 0; mureta de 25 cm, soltava 8 de 181 → 0. Nos mapas, varredura dos objetos finos com o código final (correu na face ou deu a volta): Cidade, postes 28 → 44 de 44; Estação, postes 4 → 12 de 12; Distrito Neon, postes 0 → 8 de 18 e placas e muretas 7 → 15 de 30 (o que sobra são grades de mourão e painel com colisão de um lado só: o raio passa pelo painel visto de fora, e a parede "acaba" ali — é colisão do mapa — e dois balizadores de 1,37 m em que o lobo não chega a grudar); Armazém, placas 34 → 34 de 36 (sem mudança; os três "postes" da varredura são enfeites colados na parede, e neles o lobo corre a parede, 9–10 m).
  - Testes novos `ParedePosteEPlaca` (postes, coluna, placa, parede curta, mureta e "o mesmo poste não vira elevador") e `PostesNaCidade` (fora do `--rapido`); os dois reprovam com o código antigo. Os de parede de antes passam. **Não jogado**, nem online, e sem vídeo (o Docker caiu com o disco cheio).
- **Lobo na parede: segue nos degraus, batentes, pilares e vãos, e passa por cima de obstáculo pequeno** (alpha fechado, 26/11: "wall-run não vira mais em cantos e passa por baixo de obstáculos pequenos"; "na parede, não contorna mais — anda reto e passa por baixo de obstáculos pequenos em vez de subir neles").
  - **Causa, e quando**: o código da corrida na parede não mudou desde 22/11 (em 24/11 só saiu o agarrar). A regra da quina é de 12/11, feita para as paredes lisas da fase da cápsula cinza: passou 10 cm da ponta, ou a face seguinte tem 25 cm de fundo (contorna) ou o lobo larga a parede e sai reto. Os mapas de verdade (a casca do Armazém, 17–19/11; os de kit, 20/11; a colisão fina do Neon, 24/11) estão cheios de pontas que não são ponta: o degrau de uma fachada, o fim de um pilar raso, um batente, um vão de porta. Ali a face "acabava", o raio da regra não achava nada (o recuo era raso, ou o raio saía torto porque o sentido tinha girado para contornar um pilar logo antes) e o lobo saía reto. E o obstáculo à frente era visto por um raio só, no meio do corpo: o que batia na cabeça ou nas patas fazia o corpo escorregar por baixo ou parar e cair.
  - **Conserto** (`Player.WallRun.cs`): passando da ponta, o lobo procura a mesma face logo atrás (até 0,6 m, `WallStepBack`) ou depois de um vão (até 1,6 m, `WallGapMax`) e segue nela — no vão, reto pela linha da parede, sem entrar nele; a quina e a ponta fina são medidas pela face em que ele corria. A cápsula inteira varre o caminho à frente (`WallRunClearance`), e o obstáculo com o topo até 1,8 m acima (`WallClimbReach`) o lobo sobe por cima, a 7 m/s, e segue na parede na altura nova — só ao lado da parede, e nunca em gente nem no limite do mapa. Velocidades, salto da parede, grudar, regra da mesma parede e parede invisível não mudaram (os testes deles passam).
  - **Medido** (sem tela): numa parede de teste, obstáculo de 25 × 50 cm na altura da cabeça e das patas — antes, o lobo largava a parede e caía 10 m (87 de 172 quadros fora); agora sobe 1,62 m e 0,68 m e segue. Vão de porta de 1 m — antes contornava a ponta e voltava pelo outro lado da parede; agora atravessa. Varredura de 80 pontos de parede por mapa (média percorrida em 2,5 s, e quantas corridas acabavam antes de 3 m): Cidade 4,7 → 6,6 m (31 → 24), Armazém 7,3 → 9,3 (21 → 9), Neon 3,1 → 5,3 (29 → 21), Estação 3,9 → 6,4 (31 → 18), Cidade do Deserto 5,5 → 8,4 (32 → 11), Obra 4,2 → 4,2 (lá as paredes são chapas de 20 cm, que de propósito não se contornam). De 362 corridas, 102 melhoraram mais de 1 m e 8 pioraram (3 no Armazém, perto das janelas do andar de cima).
  - **Testes novos**: `ParedeSobeObstaculo` e `ParedeVaoEDegrau` (parede de teste) e `ParedeNaCidade`, `ParedeNoArmazem`, `ParedeNoNeon` (pontos reais que antes soltavam em menos de 2 m e agora correm 6,7 a 12 m; esses três ficam fora do `--rapido`). Os cinco reprovam com o código antigo. Passam também `CorridaNaParede`, `SaltoDaParede`, `AgarraBaixo`, `RegrudaNaParede`, `VoltaNoTonel`, `ParedeInvisivel`, `SemGarraNaParede`, `AtaqueNaoParaNaParede`, `NeonMuroDaCidade` e `ParedeOnline`.
  - **Não testado**: jogando de verdade (o "sentir" da subida por cima e do vão), nem online. O servidor autoritativo roda o mesmo código: o servidor de partida novo e o jogo novo precisam ir juntos, senão um lado solta a parede onde o outro segue. O defeito antigo "wall server: off the wall … client on the wall" não foi investigado aqui.
- **Arte nova da Eve e do Barut nos cartões, no instalador e na capa** (Jhon, 25/11, com as quatro artes novas: "coloca Eve e Barut nas thumb do personagem no jogo"; "pode tirar lateral_fim também, do instalador"). As thumbs dos personagens (`src/assets/personagens/{eve,barut}/thumb.jpg`, o cartão da loja e do inventário e a foto do item no catálogo do servidor) são as artes novas (Eve com o lobo azul, Barut com o lobo vermelho), em 1280 × 720 (eram 768 × 432). O instalador do Windows não tem mais a lateral do Barut na tela final (`lateral_fim.bmp` saiu; a tela final usa a lateral das boas-vindas), e a lateral e a capa da Release foram geradas de novo com a arte nova da Eve (`instalador/gerar_arte.py`). As quatro artes em WebP otimizado (1920 × 1080 e 1200 × 1200, 130–175 KB cada, de 2–2,7 MB) ficam fora do repositório, em `.synctmp/imagens_webp/`. O instalador não foi compilado aqui (sem `makensis`): o lançamento compila.
- **Painel em produção: o login voltou** (Jhon, 25/11: "https://wolfleague-api.fly.dev:8443 todo login tá dando pedido recusado"). O painel confere que o pedido que muda algo vem dele mesmo (o cabeçalho `X-Painel` e a origem do navegador igual ao endereço do servidor). No Fly o navegador abre o painel em `https://wolfleague-api.fly.dev:8443` e manda essa origem, mas o proxy entrega o pedido com o endereço sem a porta (`wolfleague-api.fly.dev`): a comparação, com a porta, recusava todo login. Medido em produção com um "sair" sem sessão: origem com `:8443` → 403 "pedido_recusado"; sem a porta → 200. Agora a origem vale pelo nome do servidor, sem a porta (`AdminApi.RequirePanel`); outro site continua recusado, e o `X-Painel` continua obrigatório. Teste novo do servidor ("a origem vale pelo nome do servidor, não pela porta"); os 61 testes do servidor passam. Vale em produção depois do deploy do servidor.
- **Servidor autoritativo: a bala vale inteira** (achado no ajuste do dano, 25/11). O teto de dano por tiro do servidor (`HitCheck`) usava o dano da arma sem a escala das armas de fogo (`CombatRules.FirearmDamageScale`), que o jogo aplica a toda bala: o tiro honesto era cortado. Com a escala 2 de antes, o AR-15 mandava 54 e valia 29 no servidor (online autoritativo, as armas matavam bem mais devagar que offline); com a escala nova, a M4A4 mandava 35,9 e valia 28,4. Agora o teto inclui a escala, e o tiro vale no servidor o mesmo que no jogo. `VidaNoServidor` ganhou a verificação (reprova sem o conserto: "28,4 de 35,9"). Vale para o Docker local e a produção no modo autoritativo: precisa do servidor de partida novo.
- **O tiro acerta o que se vê, o headshot exige mira, e a bala tira menos** (Jhon, 25/11: "ajuste o hitbox dos personagens, deixe mais preciso, exigir mais mira para dar headshot. as armas estão muito apelonas, os bonecos precisam ser mais resistentes. tá muito fácil dar headshot").
  - **Medido antes**: o tiro acertava a cápsula de movimento (60 cm de largura e 1,75 m de altura) e a cabeça era uma esfera de 0,55 × a distância entre os ossos da cabeça (11,9 cm de raio na Eve, 13,1 no Barut, 17,9 no lobo). Contra a silhueta de verdade (a pele deformada pelo esqueleto, numa grade de 1 cm, de frente e de lado; teste novo `CorpoPrecisoNoTiro`), o ar em volta que levava tiro era 3.900 a 4.900 cm² por vista (55 a 90% da silhueta). Do lado, a cabeça do Barut saía da cabeça visível 176 cm² (36% dela), a do lobo 381 cm² (38%). E 5.166 cm² do lobo visto de frente (os braços abertos) não levavam tiro nenhum.
  - **O hitbox agora** (`BodyHitbox`, conta em `src/scripts/domain/BodyHit.cs`): cada peça do corpo (cabeça, pescoço, tronco, braço, antebraço, mão, coxa, canela, pé) é uma cápsula de corte oval ajustada à pele que pesa no osso dela, seguindo o osso; o membro comprido vira duas peças (ele afina). Ajuste: o corpo contém 80% da pele (`BodyHit.Contains`), a cabeça 60% (`HeadContains`, mais justa de propósito), e a cabeça leva só a pele com 80% do peso na cabeça (a juba do lobo que desce pelas costas é corpo). O rabo não leva tiro (como já era). A faca e a coronhada continuam pela cápsula de movimento, com a cabeça nova.
  - **Medido depois** (Eve, Barut e lobo; de frente / de lado):

    | | Ar que leva tiro (cm²) | Corpo visível sem tiro | Headshot fora da cabeça | Área que dá headshot |
    |---|---|---|---|---|
    | Eve | 4.417 → 712 / 4.911 → 663 | 188 → 319 / 624 → 435 | 49 → 0 / 25 → 30 | 447 → 313 / 447 → 456 |
    | Barut | 4.231 → 915 / 4.629 → 723 | 117 → 96 / 1.161 → 303 | 120 → 10 / 176 → 28 | 545 → 398 / 540 → 403 |
    | Lobo | 4.325 → 1.738 / 4.451 → 1.081 | 5.166 → 359 / 1.547 → 311 | 88 → 89 / 381 → 93 | 1.000 → 1.056 / 1.005 → 892 |

    O ar que ainda leva tiro é uma faixa de 1 a 2 cm em volta do corpo. Teste novo `CabecaExigeMira`: o raio da arma, de 8 m, no meio da cabeça que se vê é headshot; 3 cm do lado ou acima dela, não é (no Barut, com a esfera de antes, 3 cm do lado era headshot).
  - **A bala tira 31% menos** (`CombatRules.FirearmDamageScale` 2 → 1,38): como uma vida de 290 contra bala. Tiros para matar no corpo do humano: AR-15 e M4A4 4 → 6 (0,28 → 0,46 s), AK-74 3 → 4, SCAR-H 3 → 4, Desert Eagle 2 → 3, SVD 2 → 2. Na cabeça: o AR-15 e a M4A4 2 → 3, a AK, a SCAR e a Desert Eagle deixam de matar com um (2), e só a SVD mata com um. Contra o Bruto (400 com o escudo, corpo a 60%), o AR-15 vai de 13 a 18 tiros no corpo e de 3 a 5 na cabeça. Tabela completa no `docs/ARSENAL.md` ("No jogo, desde 25/11").
  - **O que não mudou, e o efeito no lobo**: a garra, o salto, o especial, a faca e a granada tiram o mesmo; o humano continua com 200 e o lobo com 200 + escudo. O lobo continua matando em dois golpes, mas leva ~40% mais balas para cair: na luta humano contra lobo, o lobo fica relativamente mais forte (o risco do CLAUDE.md; as alavancas, se precisar, são as de lá). Os bots usam as mesmas armas e o mesmo hitbox: o corpo menor que a cápsula faz a mira deles errar mais, e eles demoram mais para matar.
  - **Online**: no repasse quem atira decide o acerto pelo modelo que vê (o boneco), com as peças novas. O servidor autoritativo não tem modelo e continua pela cápsula e pela faixa da cabeça (`HeadZone`). Achado de passagem, não mexido (é rede): pela leitura do código, o teto de dano do `HitCheck` (`def.Damage × chumbos + 1`, vezes a cabeça e 1,05) não multiplica pela `FirearmDamageScale`. Se for isso, o servidor autoritativo corta o tiro do AR-15 de 37 para 29 (com a escala 2, de 54 para 29). Não foi medido; o `ServerHealthTests` manda o dano sem a escala e não pega.
  - Testes: unidade 89 de 89 (`BodyHitTests` novo; `CombatRulesTests` com os números novos), `CorpoPrecisoNoTiro` e `CabecaExigeMira` novos, `TiroNaCabeca` alinhado (o "peito" era meio metro abaixo da cabeça, que de lado cai no ar na frente do peito; agora é o meio da peça do peito), `CorpoDoLoboNoTiro` e `TiroPelaMiraDaPistola` passam. **Não jogado**: o "sentir" da mira e do TTK é do Jhon.
- **Online: a vida volta sozinha de novo, lisa, sem o vaivém** (Jhon, 25/11, no Docker autoritativo: "a vida não está se recuperando automaticamente no modo online; ela fica indo um pouco e voltando", "depois de um tempo bugado assim ela aparece do nada cheia"). Na partida autoritativa o jogo e o servidor regeneram cada um no seu relógio, e a diferença da defasagem (até `Health.RegenLag`, 0,5 s de volta) não conta como dano. Mas essa folga só valia **enquanto a vida não estava cheia**: quem enchia primeiro perdia a folga no mesmo instante, e a vida do outro lado, ainda uns pontos atrás, virava "dano". O servidor descia, zerava a espera de 15 s, e o `Vitals` (2/s) puxava o jogo para baixo a cada meio segundo enquanto o jogo voltava a subir: o vaivém. Passados os 15 s do servidor, ele enchia e o jogo pulava para cheio. Agora a folga vale também por `RegenLag` depois de encher (`Health.Regenerating`). Medido com o jogo de verdade ligado a um servidor de partida no teste, com 120 ms de ida e volta (atraso novo no `TestMatchServer`):
  - **Dano próprio:** antes, 17 descidas sem dano (a pior −2,7) e o servidor parado 2 pontos abaixo do máximo até o fim da medida. Depois, nenhuma descida, e os dois enchem juntos (diferença máxima de 2,7 na subida).
  - **Tiro de outro jogador** (o servidor aplica): antes, o servidor parava em 199 e ficava assim até o fim da medida (12 s depois de encher no jogo). Depois, 200 nos dois.
  - Com 300 ms também passa.
  - A folga não esconde dano de verdade: cheio, um dano de 5 que só o jogo viu chega ao servidor (195 nos dois).
  - O escudo do lobo não volta sozinho. O repasse não muda (sem `Vitals`, nunca teve o vaivém).
  - Testes novos: `VidaVoltaRepasse`, `VidaVoltaAutoritativo`, `VidaVoltaAutoritativoTiro` e `VidaVoltaAutoritativoLobo`. Os dois do meio reprovam sem o conserto. O lugar à vista para o outro atirar virou ajudante do `OnlineGameTest` (`ShooterInSight`), dividido com o `CamDeMorte*`.
  - Para valer no Docker: `docker compose up -d --build partida` e o jogo novo juntos (o conserto está no `Health`, que roda nos dois). **Não joguei online com gente.**
- **O crash do "Voltar à sala" (e o do "jogar novamente" de manhã)** (Jhon, 25/11: "deu um crash ao clicar em 'Voltar à sala' após o fim da partida, em prod", `crash.txt`). Reproduzido e consertado.
  - **Onde caía**: o relatório do macOS vem sem nomes, mas o binário exportado guarda as mensagens de erro do Godot. Lendo as que cada função cita, a pilha é: troca de cena (`_flush_scene_change`) → apagar a cena velha nó a nó (`Node::_notification`, a recursão) → o servidor de desenho apaga uma malha (`RendererSceneCull::free`) → atualiza a malha e percorre as luzes presas a ela → lê uma luz já apagada (o 0x6c do relatório). No log de manhã, o mesmo crash vinha depois de três "BUG, indexing did not unpair geometries from light"; o binário exportado não escreve esse aviso, por isso o log de produção acabava sem erro.
  - **A causa**: no Godot 4.7 com o Forward+, a malha e cada luz que a alcança ficam emparelhadas, e o par só se desfaz se a máscara da luz ainda pegar a camada de desenho da malha. Trocar a camada de uma malha não desfaz os pares. A luz dos personagens (`ViewModel`, a que segue a câmera) só acende a camada 18. Morto, você assiste um aliado em primeira pessoa, e a câmera de morte esconde o corpo dele pondo-o só na camada escondida (`KillCam.WatchedLayer`). O corpo perdia a camada 18 e ficava preso à luz para sempre. Na troca de cena a luz sumia, a malha guardava a luz apagada, e o jogo caía na próxima atualização dela. Por isso só caía em fim de partida com você morto assistindo alguém (nos dois logs, um bot aparece na camada 1048576, a escondida).
  - **O conserto**: toda troca de camada de desenho passa por `RenderLayers.Set` (novo, `src/scripts/visual/RenderLayers.cs`). Quando a malha perde camada, ela é escondida (o que desfaz os pares com a camada antiga), troca de camada e volta à vista, tudo no mesmo quadro, então nada pisca. Os lugares que trocavam camada direto passam a usá-lo: a câmera de morte, a arma (`WeaponModel.SetLayers`, também a da mão), o corpo do dono (`CharacterAnimator`) e a mão em primeira pessoa (`ViewModel`).
  - **Conferido com o renderizador de verdade**, antes da regra de não abrir janela:
    - No caso isolado (luz de máscara restrita, malha perde a camada, luz some, malha anda): antes, dois avisos "BUG, indexing…" e sinal 11; depois, nenhum.
    - No fluxo real (Cidade, 12 lutadores, morto assistindo o Bot1, fim por placar, "Voltar à sala"): antes, três avisos e sinal 11 na troca de cena, com a mesma pilha recursiva do `crash.txt`; depois, duas partidas seguidas sem aviso e sem crash.
  - **Testes novos**:
    - `CamadaSemLuzPresa`: tirar camada de uma malha à vista esconde e mostra (2 trocas de visibilidade); pôr camada a mais não pisca; escondida, não aparece.
    - `VoltarASalaSemCrash`: três partidas seguidas com bots, a partir de uma sala offline, na Cidade. Em cada uma, morto assistindo um aliado em primeira pessoa com o corpo dele na camada escondida, depois fim por placar e o botão do resultado: "Voltar à sala" duas vezes e "Sair para o lobby". Termina no lobby, com o tempo normal e sem erro. Está entre os testes de partida, que o `--rapido` pula.
    - Sem tela, como o `testar.sh` roda, o desenho é de mentira, e os dois testes não pegam o aviso do renderizador; com tela pegam.
  - Sem tela, a carga em segundo plano da tela de carregamento não lê a `main.tscn` do zero (o recurso C# do ruído do bot falha na leitura da `bot.tscn`). No jogo ela lê; o teste carrega as cenas antes. Não investiguei mais.
  - Não rodei: o binário exportado e nenhuma partida online.
- **Neon: o mapa fecha no muro da cidade, e os fios não têm colisão** (Jhon, 25/11: "mapa neon, tô conseguindo sair pelos muros da cidade com o lobo; o mapa é delimitado por este muro, não pelo que tem no lado de fora"; "os fios do mapa neon não podem ter colisão (fios das torres de energia)").
  - **O muro**: as paredes de limite (`Limite…`) ficam na borda do mapa, de 10 a 20 m além do muro e dos prédios do anel, e as barreiras de cima do muro tinham saído em 24/11 (tapavam portas). O lobo subia o muro (o salto carregado sobe até 6,2 m; o muro tem 5,7) e andava lá fora. Agora tudo o que fica do lado de fora do muro é a parede invisível `BarreiraMuro`, do chão ao teto: 1.345 caixas, 924 do chão ao teto e 421 por cima do que é baixo e encosta no muro (o carro estacionado rente ao prédio: pisa-se nele, a barreira começa 3 m acima; a sala de piso alto atrás da porta da delegacia e do hospital: dentro se anda, no telhado não). Medida com a colisão de verdade numa grade de 12,5 cm (`src/tools/mapas/muro_da_cidade.gd`): a cidade é onde cabe um corpo e se chega andando de onde se nasce (6.670 m²); o lado de fora, o que se alcança da borda sem entrar nela (5.323 m²). A face da barreira fica atrás da face do muro (até 12,5 cm), e o nome `Barreira…` faz valer a regra de 20/11: o lobo não corre nela nem no muro que ela tem logo atrás, e a bala não marca.
  - **Medido** (teste `NeonMuroDaCidade`, sem tela): de 8 pontos encostados no muro em volta da cidade, o salto carregado mirado no alto do muro ou do telhado de fora, depois pular e andar contra ele, depois correr e saltar da parede. Sem a barreira o lobo sai em 3 dos 8; com ela, em 0. Nenhum dos 148 lugares de andar (onde se nasce, as rotas dos bots, o alto das escadas) ficou tapado; a volta do mapa é barrada do chão ao teto (732 pontos em três alturas), e de dentro para fora, em 36 direções a 8 m e rente ao teto, a primeira parede invisível é a do muro. `MapaNeon` passou a conferir o alto da volta do mapa, como os outros mapas.
  - **Correr no muro de dentro continua** onde o andar de cima do prédio do anel avança sobre a rua (a barreira segue o térreo): com a barreira o lobo ainda grudou em 2 dos 8 pontos, e não passou em nenhum.
  - **Os fios**: os 14 que cruzam a rua de dentro dos quarteirões, de prédio a prédio e das antenas (`SM_Buildings_Part_24`, de 10 m, e `_25`, de 5 m), perderam a colisão; a antena continua sólida. Antes, 32 de 42 tiros cruzando o fio paravam nele e o corpo encostava nos 14; agora, 0 e 0 (teste `NeonFiosSemColisao`). No `.glb` do mapa (`src/tools/mapas/sem_colisao.py`, que tira o sufixo `-col` do nó) e no gerador (`neon.py: FIOS`).
  - `colisao_exata.gd --sem-barreiras` tira só as barreiras de dentro (`Barreira1`, `2`…), não a do muro. Sem fotos: só testes sem tela. Não joguei com gente nem online.
- **Neon do lobo forte e olhos acesos, no jogo** (Jhon, 25/11, vendo os retratos dos lobos para as thumbs: "coloque os leds do equipamento brilhando bem forte", "e o olho também", "quero o neon desse jeito no game"). Medido com fotos do lobo dentro do jogo, de frente, no Armazém e na Cidade (antes e depois em `.synctmp/neon_jogo/antes_depois_*.png`):
  - **O neon do corpo** (braceletes, peitoral, costas; `lobo.gdshader`, `neon_energy`) foi de 2,6 para **16**: era um fio aceso sem halo; agora o miolo passa do branco e o glow dos mapas (todos ligados, limiar 1) faz o halo, nos dois times. O poder multiplica menos (`neon_boost_gain` 1,6 → 0,6): com o neon forte, o pico do rugido (escudo cheio) ia a 93 e estourava a tela; vai a ~44.
  - **Peitoral e costas com o mesmo brilho do bracelete** (Jhon: "os leds no equipamento do peitoral e das costas devem ter os mesmos brilhos"). Medido na textura de emissão, no miolo de cada faixa: bracelete 0,98, costas 0,74–0,82, peitoral 0,40–0,45. O material agora leva todo LED aceso à força cheia, na cor dele (`neon_even`), com a borda suave; o que não é LED continua apagado. Fotos antes e depois, de frente e de costas, nos dois lobos, em `.synctmp/tela/leds_antes_depois.png`: as costas ficam como os braceletes; o peitoral acende inteiro, mas é um friso fino na placa do peito e continua menor.
  - **Os olhos** do modelo são emissão, mas pequenos (2,7 cm): mesmo fortes, viravam um pontinho. Cada olho ganhou um disco de luz preso ao osso da cabeça, 2 cm à frente da pele (`EyeGlow`, `olho.gdshader`): virado para a câmera, miolo quase branco e halo na cor do personagem, some onde encosta na cara, sobe com o poder (`WolfFx.EyeBoost`). O lugar dos olhos foi medido no miolo aceso da textura de emissão: o mesmo rosto nos dois lobos, os olhos a 8,5 cm um do outro. De costas a cabeça tampa o brilho; o lobo humano não tem.
  - Vale para todo lobo que aparece na tela: o seu, os bots e os outros jogadores online (o inimigo fica mais visível de frente; é o que foi pedido). O servidor dedicado não desenha efeitos. A vitrine do inventário pega o neon forte (o mesmo material), sem o disco dos olhos.
  - Teste novo `OlhosDoLobo` (o neon é o forte; os dois olhos na frente do rosto, presos à cabeça, acesos no lobo e apagados no humano). `EfeitosDoLobo` passa a olhar só as malhas da pele (os discos dos olhos não levam aura nem vento). Números no `AJUSTES.md` ("Neon e olhos do lobo"). **Não joguei online** nem vi em tela cheia fora das fotos.
- **Armazém: a parede branca do bloco do fundo leste ganhou a textura** (Jhon, 25/11, com a foto: "somente coloque a textura nessa parte do armazém"). Era uma parede branca lisa, com as janelas e os portões desenhados só em relevo, no bloco de 15 × 15 m do lado leste (`estendido_41`, 447 m², dentro do galpão, onde ficam o portão do fundo, a lâmpada e o extintor). **Causa:** o Blender exportou esse bloco sem material no `.glb` (uma das duas primitivas da peça), e o Godot pinta o que vem sem material de branco liso: nem a paleta nem o material de detalhe (reboco, chapa, concreto) chegavam nele. Conferi com o jogo de verdade: a peça tinha duas superfícies, uma com o `StandardMaterial3D` branco do `.glb` e a outra com o material de detalhe; e com as classes de material ligadas (`ver_classes`) a parede continuava branca em vez de colorida. **Conserto:** o `src/tools/mapas/kits/detalhe.py` (`remendar`) passa a dar o material da paleta a toda primitiva do Armazém, e `src/assets/mapas/pecas/armazem_estendido.glb` ganhou só essa linha (refeito pelo gerador: o resto do arquivo, 42 139 352 bytes de geometria, saiu idêntico). Agora a parede é reboco com o tijolo aparecendo onde caiu, os portões são de chapa com junta e o chão do bloco é o da paleta, como o resto do galpão. Fotos antes e depois do mesmo lugar em `.synctmp/armazem_parede_antes_depois.png`. Teste novo `ArmazemTodaFaceComMaterial` (nenhuma superfície do modelo fica com o material padrão). Só o Armazém: não mexi na colisão (que é à parte) nem nos outros mapas. **Não joguei online.**
- **A granada no pé mata de novo, e a câmera de morte volta a mostrar quem te matou** (Jhon, 25/11, no Docker autoritativo: "a granada no pé não te mata, olhando 100% para baixo"; "antes, quando eu morria, mostrava o corpo de quem me matou em vermelho, o nome..."). Medido com o jogo de verdade ligado a um servidor de partida dentro do teste (`TestMatchServer`, ENet, sem Docker), autoritativo e de repasse:
  - **A granada no pé** mata nos dois modos olhando para baixo (pitch −89): nasce 1 granada, pousa a ~0,7 m abaixo do corpo, a vida do jogo vai a 0, o servidor anuncia e o corpo do servidor morre; o defeito não era de rede. Era a **proteção de nascimento (3 s)**: jogada logo depois de renascer (trocar para a granada leva ~1 s, mais o pavio de 1,2 s), ela explodia ainda protegida e não fazia nada. Antes: vida 200 de 200, sem morte, sem feed; depois: vida 0, morto, no feed ("de granada"). A proteção é contra os outros; o dano que o corpo se dá não é barrado (`Health.TakeOwnDamage`, `TakeDamage(ignoreProtection)`).
  - **A câmera de morte sem matador**: a própria granada que **feria e não matava** deixava a causa da morte "granada" armada até a morte seguinte — e o inimigo que terminasse o serviço virava "se matou": `Health.Killer` era apagado, e a câmera de morte não mostrava a silhueta vermelha nem o nome. É o que acontece a quem testa a granada aos pés (que não matou) e depois morre jogando. Antes: `Killer` nulo, 0 peças em vermelho, nome vazio, nos três modos; depois: o matador, as 16 peças em vermelho e o nome ("ABATIDO POR"), offline, de repasse e autoritativo.
  - Testes novos: `GranadaNoPeOnline`, `GranadaNoPeRepasse`, `GranadaNoPeLogoDepoisDeRenascer`, `CamDeMorteOffline`/`Repasse`/`Autoritativo` e `CamDeMorteAposGranadaPropria`/`Repasse`/`Autoritativo`.
- **A pistola mirando com a massa e a alça no centro, como o fuzil** (Jhon, 25/11: "a mira da pistola escopada não está atirando a bala onde eu miro, tá mais acima"; depois: "pode ficar igual o fuzil ao centro, alinhar melhor atrás com a frente, igual à AR-15"). Medido: o tiro sai exatamente do centro da tela (0,0 cm de desvio da câmera, pistola e fuzil), mas a pistola mirava com a linha de mira 11 mm abaixo do olho (`BAIXO`, pedido de 19/11: "desça ela mais"), e a ponta da massa aparecia **2,1° abaixo do centro**: quem punha a massa no alvo atirava 2,1° acima (55 cm a 15 m, 29 cm a 8 m). Agora a arma sobe mirando (`AimLift` no grupo "Mira" das armas, 5 mm nas três pistolas: Desert Eagle, Cobra e Fênix; 0 nos fuzis) e a massa fica no centro, dentro do entalhe da alça de trás, com o topo da alça na linha do centro; a bala segue saindo do centro, e agora sai onde a massa aponta. Fotos do jogo com 0, 3, 6 e 11 mm de subida acharam o 5 (a antes e depois está em `.synctmp/mira_pistola_antes_depois.png`); o 11 que o `BAIXO` sugeria era demais. Não regerei os clipes de mira (o gerador `conjuntos.py` refaz o conjunto inteiro da pistola, mãos e recarga): é uma subida em tempo de execução em `ViewModel`. Teste novo `TiroPelaMiraDaPistola`: 6 tiros por caso, a 13 m, a bala sai do centro (0,00° da cintura, 0,02° mirando; com a pele Cobra e com o fuzil igual). Não joguei online; o servidor autoritativo não muda (a mira sai do centro como sempre).
- **Partida autoritativa: o especial, o bote e o encostar num bot sem o lobo voltar para trás** (Jhon, 25/11, no Docker: "o lobo não avança e cai e fica, indo e voltando"; "avança no inimigo, mata (ou não) e volta para trás"; "com movimentos complexos dá uma lagada, volta no tempo"; "no primeira pessoa que buga"). Decisão 0017. Medido nos testes de ponta a ponta (servidor de partida dentro do teste, rede oscilando):
  - **O especial não saía no servidor**: a carga dele é contada em cada máquina, e o corpo do servidor nunca enchia. Agora ele conta os abates que anuncia e o tempo, e copia a carga do jogo só para cima, até 0,2 além do que ganhou (`Weapon.ChargeSlack`). Alvo a 8 m: antes, 45 correções somando 21,2 m (a maior de 3,25 m), 20 quadros puxado para trás; depois, 0.
  - **O salto ia para outro lugar**: o servidor não tem a câmera nem a trava. O comando leva o ponto mirado no clique e o alvo travado (o lugar na escalação e onde o jogo o via); o servidor confere alcance e lugar (`ServerBody.FocusSlack` 3 m). Contra um bot (que não tem corpo no servidor): antes, 30 correções somando 10,7 m (a maior de 1,56 m), e o lobo terminava a 3,6 m do alvo; depois, 0, colado a 0,7 m. Contra um jogador a 12 m: 12 correções (0,70 m) → 1 (7,5 cm).
  - **A arma do corpo do servidor dá o passo com o comando** (`Weapon.Tick` dentro de `Player.Simulate`), não com o relógio do servidor.
  - **O bot não existia no servidor**: agora tem uma cápsula onde o dono diz que ele está (`BotProxies`). Andando, arranhando e dando o bote num bot: antes, 22 correções somando 8,3 m (a maior de 71 cm), e a correção arrastava o lobo através do bot; depois, 0.
  - **Aliado atravessa no servidor** como no jogo (os corpos do servidor entram no `TeamCollision`); antes era parede lá. E o corpo do servidor guarda o time de quem ele é: com a sala ativa na máquina (no teste, ou num jogo que hospeda), o `Player._EnterTree` punha todos no time da sala.
  - Com tudo, o roteiro de movimentos difíceis (parede, salto da parede, de quatro, bote, especial sem alvo, rugido) corrige o jogo 1 vez, 3 cm, com a rede limpa e com a rede oscilando (`MovimentosOnline`).
  - Testes novos: `EspecialOnline`, `MovimentosOnline`, `EncostaNoBotOnline`; de unidade, o comando do especial (51 bytes no máximo).
- **A própria granada no pé no feed, online** (Jhon, 25/11: "quando eu me matei, não apareceu eu me matando e a arma no feed de kills"). O jogo contava toda morte sem autor como "queda", e o servidor (de repasse e autoritativo) só anunciava abate com autor: a morte sozinho sumia do feed e não contava. Agora o jogo conta a causa e a arma (`Health.LastSelfCause`) e o servidor anuncia a todos, com a pessoa dos dois lados, e conta a morte sem ponto para ninguém (`Referee.SelfDeath`); quem se matou não ganha WP de abate. Offline a granada lançada aos pés mata (420 no centro; medido no `GranadaNoPe`).
- **O rugido do inimigo, online**: no roteiro autoritativo o servidor ruge o rugido inteiro (92 passos de 92), a marca chega ao boneco sem piscar (liga e desliga uma vez) e o boneco toca o clipe até o fim (92 de 92). O defeito relatado não se reproduziu aqui; veja o `docs/PENDENCIAS.md`.

- **O vento do lobo só no seu** (Jhon, 25/11: "não devo ver os efeitos de vento do lobo inimigo, somente o meu"). Os riscos de vento e o brilho frio de velocidade (`WolfFx`) só aparecem no lobo de quem joga; o dos bots e dos bonecos da rede corre sem eles. O resto dos efeitos (aura do poder, garras, raios do especial, rugido) continua em todos. `EfeitosDoLobo` passa.
- **Erro do `CombatantAudio` no servidor de partida** (no log do Jhon, 25/11: "Cannot access a disposed object 'CombatantAudio'" a cada tiro de um corpo no servidor autoritativo). O servidor tira o som do corpo, mas a arma continuava chamando o nó apagado. Agora ele solta todos os sinais ao sair (`_ExitTree`).
- **Inventário: o personagem e o lobo em 3D num estúdio** (Jhon, 25/11: "no inventário do player, onde mostra o personagem e o lobo 3D, coloque uma iluminação melhor, textura melhor e cole eles no chão, está muito sem qualidade, meio escuro, deixe AAA"). A vitrine (`CharacterPreview`, no painel de detalhe do inventário e da loja) foi refeita:
  - **Nitidez**: era desenhada em 320 × 300 pontos e esticada na tela (576 × 540 pixels no Mac, 1,8×): tudo saía borrado. Agora o 3D é desenhado na resolução de verdade da tela (até 2,5×), com MSAA 4×, filtro anisotrópico 16× e as texturas um pouco mais nítidas de perto (`TextureMipmapBias` −0,5).
  - **Luz de estúdio**: antes, uma direcional fraca (0,75), ambiente chapado (0,3) e um ponto amarelo atrás. Agora, a principal quente com sombra macia (2,1), um preenchimento frio do outro lado, dois recortes atrás que desenham a borda contra o fundo, e o céu do mapa de dia (o HDR) como ambiente e reflexo no metal; tons de cinema, oclusão (SSAO) e um brilho leve no neon. Atrás, um halo frio, como o foco batendo na parede.
  - **No chão**: o pedestal virou um disco escuro e fosco (o de antes era marrom e chapado, com um anel amarelo grosso), que recebe a sombra e a oclusão, com uma sombra de contato embaixo dos pés. O corpo encosta no tampo (eram 2 mm acima). O lobo não desce os 2 cm que desce no jogo (as garras furariam o tampo, e o `VitrineNoPedestal` reprova): quem o segura no chão é a sombra de contato.
  - Fotos antes e depois, Eve e Barut, humano e lobo, no jogo em janela. Não foi visto em tela sem Retina nem com o menu de outra cor.
- **Especial do lobo na Cidade: a trava segura, o voo não bate em nada e não sobe à toa** (Jhon, 25/11, com vídeo na Cidade: "o especial errando" — travado num humano a 14 m do outro lado da rua, o lobo subiu alto, bateu num poste no meio do caminho e caiu a uns 4 m dele, sem acertar). Teste novo `EspecialNaCidade`, no mapa de verdade, com o fluxo do jogador (Q arma, a trava pega o alvo no centro da tela, o clique salta).
  - **Por que caiu longe**: a trava pega até 14 m e soltava em 14 m. Com a tela dizendo "saltar 14 m", o alvo passava a 14,05 m no clique, a trava sumia e o salto ia para a mira, no máximo 12 m (`PounceMaxDistance`), e no caminho batia no poste. Reproduzido: travado a 13,8 m, o alvo a 14,6 m no clique — **antes** sem trava, caiu a 2,6 m dele, 3 batidas no poste, 0 de dano; **agora** travado, cai a 0,7 m, sem batida, 312 de dano. A trava já pega segura até 16 m (`LockKeepRange`), e o voo do travado o alcança.
  - **Por que batia no ar**: o caminho do salto era medido por uma linha só, no meio do corpo, a cada 40 cm. O lado do corpo batia no que a linha não via (o poste meio palmo ao lado, a quina de uma barreira, um cercado) e o lobo escorregava de lado. Agora o voo é testado com a cápsula do corpo ao longo do caminho de verdade (`Weapon.WolfPouncePath.cs`), e **postes, placas e hastes** (até 35 cm) são atravessados. Nos dois casos do vídeo (o poste no caminho, alvo a 10 e a 13,5 m): **antes** 2 a 3 batidas no poste e 0,35 a 0,38 m de desvio de lado; **agora** nenhuma, 0 m de lado.
  - **Por que subia tanto**: o que ficava colado na saída ou na chegada (uma barreira, um carro) pedia um arco enorme, porque o voo já avançava enquanto subia. Agora o lobo sobe antes de avançar (ou desce na vertical no fim) quando isso poupa subida, e cai do lado do alvo que dá o voo mais baixo. Passando por cima de algo: **antes** até 2,96 m acima do caminho; **agora** até 1,8 m. Com a rua livre, igual (até 1,4 m).
  - **Pares sorteados** (com semente) pelas ruas, 30 com a rua livre e 15 com algo baixo no meio: **antes** 3 a 4 saltos batendo no ar e até 0,55 m de desvio de lado; **agora** 43 de 43 travados acertam, todos caem até 1,3 m do alvo, nenhum bate, nenhum recua, desvio de lado de no máximo 3 cm.
  - O teto da velocidade no voo (`WolfMoveMotor.MaxFlightSpeed`) foi de 45 para 90 m/s: com 45, o especial de 13 m (58 m/s no meio do sino) ficava para trás e corrigia no fim.
  - **O que não se reproduziu**: o "flutuar" armado do vídeo é o lobo andando (pisando no chão, no andar de sempre, medido); a pose torta depois de cair (0,55 s, o tempo exato do levantar do salto) não apareceu no teste: o corpo fica parado (0 cm de deslize), sem girar, e nenhum outro golpe entra — é o clipe da pancada e do levantar (`Special_Attack` de 1,33 s ao fim, a 1,7×). Se for ele o que parece torto, é mexer no clipe ou cortar a pancada.
  - Números em `docs/AJUSTES.md`; a regra em `docs/DESIGN.md` (o modo alvo). **Não foi jogado, nem online com gente.**
- **Quatro defeitos antigos da lista conferidos: três já não acontecem** (Jhon, 25/11: "resolva esses, veja se realmente acontecem").
  - **Pescoço e peito do lobo fora da cápsula; peito do lobo agachado passando o tiro** (de 14 e 15/11): não acontecem mais com o lobo novo. Teste novo `CorpoDoLoboNoTiro`: calcula a pele do lobo na pose daquela hora (em pé, andando, agachado e de quatro), acha os pontos do pescoço e do tronco a mais de 4 cm fora da cápsula de movimento e o bot atira em cada um, de fora. São 8 pontos no total (1 em pé, 2 andando, 5 agachado, nenhum de quatro), e os 8 levam o tiro: o tronco desse modelo é mais reto que o do lobo de 14/11 e cabe quase todo na cápsula, e o que sai fica colado nela ou na esfera da cabeça. Cheguei a fazer cápsulas de tiro nos ossos do tronco, e saíram: não mudavam nenhum tiro. O teste fica para pegar isso se o modelo ou as poses mudarem. (No caminho: a primeira versão do teste "errava" o lobo agachado de lado porque o pente do bot acabava no meio da série.)
  - **A luz da mão dando erro no Output a cada partida**: corrigida em 18/11 (entra na câmera no quadro seguinte). Uma partida de bots jogada inteira, em janela, terminou sem nenhum erro no Output. A linha velha saiu de `docs/PENDENCIAS.md`.
  - **Personagem online: os outros te veem com o padrão do time**: no código é verdade (a escalação do servidor não leva o personagem), mas hoje não aparece: cada time tem um personagem só, e o padrão do time é o escolhido. Fica na lista para quando entrar o terceiro personagem.
- **Alpha: economia, segurança do conjunto e acabamento** (Jhon, 25/11).
  - **Cash e IPs** começam em 0 na conta nova; o Cash por partida (base 150, abate 10, assistência 5, vitória 100, empate 50, MVP 50) e o da conta nova são do painel (chave `recompensas`), e o jogo lê o mesmo valor para a previsão do resultado. Mensagem de compra e painel dizem Cash e IPs.
  - **O servidor confere o conjunto**: o saguão manda na escalação o que a conta tem (sem itens vencidos) e o servidor de partida troca a arma, a classe de lobo ou a melhoria que não é de série nem da conta pela padrão e conta contra o jogador. Faca e granada não precisam (o servidor usa a comum).
  - **Resultado**: o ícone de perfil (não o personagem) na tabela e no MVP; sem "Jogar de novo", só "Voltar à sala" e "Sair para o lobby"; a voz de vitória/derrota sai junto do texto.
  - **Minimapa**: inimigo só aparece se um aliado o enxerga, e some 3 s depois de perdido de vista.
  - **Barra de vida do inimigo** a qualquer distância, crescendo até 3,2×. **Morte sem autor** (queda, a própria granada) entra no feed e conta a morte. **No máximo 3 contas salvas** no login. **Vozes da Eve** mais agudas (dor, morte, respiração). Botão de créditos escondido (`ShowCredits`).
  - Conferidos pelo Jhon (25/11) e fora das pendências: faca e granada, glitches de mapa, troca de arma, tiro na cabeça, tremor dos personagens e os clipes do `Animacoes` (`crouch_idle`, `fall`, `land`).
  - Testes automatizados que estavam vermelhos (`MaosPadrao`, `PassadaDoLobo`, `Granada`, `GranadaFere`, `Suavizacao`, `EfeitosDoLobo`, `CtrlCliqueNoMac`): resolvidos, segundo o Jhon (25/11).
  - Resolvido e fora das pendências: velocidades e salto do lobo alinhados, partida autoritativa (vida e acerto no servidor, ligada em produção e testada com latência), nomes das moedas (Cash, IPs, KP), bag, health packs e Badge packs descartados.
- **Cidade: lixo no chão, ar-condicionado na parede e sem cerca no meio da rua** (Jhon, 25/11, com fotos: "condensador de ar-condicionado flutuando ao invés de estar grudado nos prédios"; "lixos flutuando, todos do mapa"; "objetos sem sentido no mapa"; "tire da frente do bank"). Mexido direto no `src/assets/mapas/pecas/cidade.glb` (a fonte do mapa é a cena desde 24/11) e no `cidade.tscn`; o de antes está em `.synctmp/cidade_antes/`.
  - **Lixo** (`Garbage_01`, os 23): a peça tem os sacos 18 cm abaixo da lama, e o gerador a punha pelo ponto mais baixo, então a lama ficava 18 cm no ar com os sacos pendurados. Desceram 18,1 cm: a lama encosta no chão e os sacos entram nele.
  - **Ar-condicionado** (`Fan_01`, eram 38): ficavam de 15 cm a 3,3 m da parede, e três no ar sem parede atrás. Cada um foi medido no Blender com cinco raios para trás (o meio e os quatro cantos): 31 tinham parede lisa e encostaram nela; os 7 sem parede lisa atrás saíram. Conferidos em foto no Godot, saíram mais 12 que, encostados, ficavam em vidro, em cima de janela ou **na frente do letreiro do banco**. Ficaram 19, todos em parede fechada.
  - **Cerca no meio da rua** (`Fence_03` 166 a 169, duas de cada lado do mapa): de pé no asfalto, atravessando a rua ao lado do estacionamento da polícia. Saíram, com as colisões (`pc166` a `pc169`). As cercas das calçadas e dos pátios ficaram.
  - Testes: os seis `Mapa…` passam (a Cidade carrega, os times nascem, os bots lutam). Conferido em fotos no Godot (lixo, os 31 aparelhos encostados, a rua sem cerca, as duas fachadas do banco). Não foi jogado.
- **O erro intermitente dos testes ("Handle is not initialized") tinha endereço**: era sempre no `WolfOwnClips.Open`, que carregava o `lobo_anim.glb` de novo a cada lobo montado; o Godot devolvia o mesmo recurso enquanto o coletor do C# ainda soltava o invólucro da vez anterior. O arquivo carregado agora fica guardado entre um lobo e outro (e o `Saida` o solta no fim). `Personagens`, o que mais caía (2 em 4 rodadas, e 4 testes numa bateria), passou 8 de 8 seguidas. De quebra, o segundo lobo em diante monta sem reler o arquivo.
- **Especial do lobo: liso e no alvo** (Jhon, 25/11: "quando dou o especial em um personagem, ele dá uma bugada, ele vai e volta no ar e acaba errando o alvo, isso precisa ser extremamente preciso e fluido"). O voo do especial (Q e clique no alvo travado) refazia, a cada quadro, o arco inteiro do ponto de partida até o **centro** do alvo. Três defeitos saíam disso, medidos no teste novo:
  - **Recuava no ar** quando o alvo vinha na direção do lobo: no fim do voo o arco anda junto com o alvo, e o lobo voltava até 12 cm por quadro contra um lobo de quatro (4 quadros seguidos; 6 cm por quadro contra quem anda; 3 a 4 cm com o boneco da rede). Agora o voo corrige a partir de onde o lobo está e nunca anda para trás da direção em que saiu: 0 em todos os casos.
  - **Batia no alvo e parava seco** a 0,6 m (as duas cápsulas encostadas), 4 a 5 quadros antes de cair, e descia reto. Agora cai colado na frente dele, a 0,7 m (`PounceStandOff`), chegando liso. Mirar alguém sem trava também cai na frente dele.
  - **Escorregava em volta de quem estava no caminho**: a física deslizava o lobo de lado na cápsula do outro (até 6,8 m/s de lado) e ele dava a volta nele no ar. Com trava, quem está no caminho agora é atravessado (volta a ser sólido quando o lobo sai de cima dele); sem trava, o lobo para nele de verdade, e o golpe sai ali.
  - E ficou preciso com quem se mexe: mira onde o alvo vai estar quando o lobo cair (a velocidade dele, alisada) e o ponto de queda anda atrás dele no máximo a 9 m/s (`PounceSteerSpeed`). Quem esquiva de um lado para o outro (A e D) ou o boneco da rede, que anda aos pacotes, não fazem o lobo balançar.
  - **Teste novo `EspecialPreciso`** (121 verificações): parado a 2, 4, 8 e 11 m e de raspão; andando para o lobo, de lado e fugindo; um lobo vindo de quatro, reto e de viés; esquivando; o boneco da rede parado, de lado, vindo e esquivando (pacotes a 30 por segundo); alguém no caminho. Em todos: 0 quadros para trás, no máximo uma troca de lado, cai entre 0,58 e 0,74 m do alvo e acerta (312 de dano). Antes: 3 a 4 quadros para trás nos casos em que o alvo vinha, cair a 0,48 m (dentro do alvo) e, com alguém no caminho, a volta em torno dele.
  - Números em `docs/AJUSTES.md`; a regra em `docs/DESIGN.md` (o modo alvo). Passam também `SaltoCarregado`, `EspecialArmado`, `TravaAtrasDoPoste`, `CargaDoSalto`, `AliadoNaoTrava`, `LobosUsamEspeciais`, `BonecoLobo` e os da garra e do bote. **Não foi jogado, nem online com gente.**
- **Resultado: o retrato com a borda do nível maior e com a foto dentro** (Jhon, 25/11, com print da faixa do MVP e do card da recompensa: "os escudos (molduras) devem ser um pouco maiores e ter o ícone do player neles"). Eram só a borda, com o miolo vazio, de 40 px (MVP) e 48 px (recompensa); agora 64 e 72 px, com a placa do número e as estrelas (`ResultScreen.MvpPortrait`, `RewardPortrait`). Na faixa do MVP vai a foto da partida dele (o retrato do personagem, a mesma do placar embaixo); MVP bot mostra o ícone de bot. No card da recompensa vai a sua foto de perfil, porque o nível é da conta (a mesma da tela de promoção). Conferido em foto do jogo; não foi jogado.
- **Crash no "jogar novamente" online: investigado, não reproduzido** (Jhon, 25/11, `src/crash.txt`). O jogo caiu (sinal 11, dentro do Godot, sem símbolos) na troca para a partida nova, logo depois de o log mostrar três vezes "BUG, indexing did not unpair geometries from light" no renderizador (`ChangeSceneToPacked`, `LoadingScreen.cs:85`). Esse erro aparece quando alguma malha está com a caixa inválida (NaN ou infinita); o suspeito é o rabo novo, que move ossos (e com eles a caixa da malha) e estava no jogo dessa partida. Três partidas offline seguidas com bots, trocando de cena, rodaram sem erro e sem NaN em nenhum osso. Fica a guarda no rabo (entrada do lobo, abaixo). Se cair de novo, o log está em `~/Library/Application Support/Godot/app_userdata/WolfLeague/logs/` (a cópia deste está em `.synctmp/crash_jogar_novamente_godot.log`).
- **A granada soa a cada batida** (Jhon, 25/11: "a granada, ao cair, tem que fazer um barulho de algo caindo no chão e respeitando a física a cada batida"). Som novo `granada_quique` (cinco variações, o `impact_metal_medium` do Impact Sounds da Kenney, CC0): um por batida no chão, na parede ou no teto, no volume da velocidade da batida (metade da velocidade, 6 dB a menos). A batida é lida pela física (`Grenade.Knock`): o que a velocidade mudou no passo além da gravidade e do freio é o que a superfície devolveu; rolando, quase nada, e não soa. Teste novo `GranadaQuica`: solta de 2 m, soa a 0,63 s a 5,9 m/s (a queda chega a 6,3), quica a 1,8 e a 0,5 m/s, cada uma mais fraca, e depois rola calada. Os outros cinco testes de granada passam. **Não foi ouvido**: o som foi escolhido pela medida (curto, metálico e médio, sem o zunido do `impacto_metal` da bala); a de outro jogador online também soa (ela cai na sua tela pela mesma física).
- **Lobos novos: o modelo natural do Meshy nos dois times, olhos de luz e rabo com física** (Jhon, 25/11: "o modelo dos lobos não é tão polido como eu gostaria... aparência muito de anime e pouco ameaçadora"; "tire os tons azuis, deixe o pelo mais obscuro e próximo de lobos"; "olhos brilhantes... deveria ser todo luminoso"; "as roupas podem ter as cores da roupa da Eve e do Barut"; sobre o modelo em `src/assets/222`: "quero algo bem próximo daquilo"; "o rabo deve ter movimentação e física durante o game, podendo mexer conforme o personagem anda ou corre, bem próximo da física e respeitando leis da gravidade").
  - **O corpo**: `eve/lobo_azul.glb` e `barut/lobo_vermelho.glb` passam a ser o mesmo modelo, o "WerewolfNaturalWolf" que o Jhon gerou no Meshy (10.253 triângulos numa malha só, contra 27.826 e 30.462 em três malhas; cor, normal e emissão em 4096, ORM em 2048). O Meshy entrega sem esqueleto: o modelo foi levado ao **esqueleto do lobo azul de 17/11**, que não mudou (mesma altura de 1,66 m, pés no chão; a malha nova fica a 1,8 cm da antiga, na mediana), com os pesos tirados do lobo antigo. Os clipes, a passada e a cabeça que leva tiro são os mesmos, e agora iguais nos dois times (o vermelho tinha o topo da cabeça 10 cm mais baixo).
  - **A pintura por time** (`src/tools/lobo/pintar.py`): a pelagem e os detalhes são os do modelo. O da **Eve** fica com a pelagem cinza e branca, olhos e neon em ciano e a calça em azul-ardósia; o do **Barut** é escuro (pelagem a 50%, um pouco quente), com olhos de brasa, neon vermelho e a calça em carmim. **Os olhos são inteiros de luz**: a amêndoa do modelo (27 mm), 2 mm maior, borda na cor do time, miolo quase branco, sem pupila. O neon acende onde o modelo tem faixa: braceletes, peitoral e as tiras das costas. O mapa de metal do Meshy acendia em lascas de pelo (cacos espelhados na cabeça): pelo e pano saem sem metal e foscos.
  - **O rabo** (o lobo do jogo não tinha): cinco ossos novos, `rabo1` a `rabo5` (66 cm, 29 ossos no total), e `WolfVisual.Tail.cs` com o `SpringBoneSimulator3D` do Godot. A gravidade puxa a ponta, a mola devolve ao desenho, cápsulas nas pernas e no quadril seguram o rabo fora do corpo, e a aceleração do corpo vira força: arrancou, o rabo fica para trás; freou, vem para a frente; virou, vai para fora da curva. A conta roda no referencial do corpo: no do mundo o amortecimento virava vento e o rabo ficava esticado a 45° só de andar a passo. Não tem hitbox, e o corpo do servidor não simula. Números no `AJUSTES.md`.
  - **O rabo é uma peça solta, presa só na raiz** (Jhon, 25/11: "a bunda do lobo fica mexendo junto com o rabo"; "continua se movendo e com glitches ao movimentar o rabo, somente o rabo deve mexer"; "ainda pegando resquícios das costas... deve ser somente a cauda mesmo"). Foram quatro causas, achadas girando só os ossos do rabo dentro do jogo e marcando o que mudava na foto:
    - No modelo do Meshy o rabo é fundido no corpo: os triângulos da emenda esticavam por cima da calça a cada balanço. O `malha.py` agora **corta** a malha na divisa da cauda (o pelo castanho ligado ao que está para trás do corpo, até 0,93 m de altura) e fecha os dois lados com tampa: a do corpo na cor da calça, a do rabo na do pelo escuro. A cauda fica presa por um pescoço de 18 arestas, e só os primeiros 6 cm seguem o corpo.
    - O corte ia até 0,99 m e levava a franja de pelo das costas, embaixo do cinto (ali o pelo alarga de 16 para 30 cm). Desceu para 0,93 m.
    - Os ossos do rabo estavam gravados com escala 100 em relação ao quadril (o esqueleto do Mixamo é em centímetros). Agora são como os outros, e a mola, o peso, a grossura, a força e as cápsulas de colisão vão na unidade do esqueleto (`WolfVisual.Tail`): antes as cápsulas das pernas tinham 1 mm e não seguravam nada.
    - A raiz ficou firme (mola 3,5, peso 0,3) e a cápsula do quadril fina (9 cm): a de 17 engolia a raiz e travava o rabo.
    - De passagem: o pelo escuro do rabo perto da calça era pintado como pano (azul-ardósia ou carmim). O pano agora é só o que não é castanho.
  - **Guarda contra NaN no rabo**: a força do movimento tem teto, quadro curto demais (engasgo) não conta, a velocidade lida para no máximo 20 m/s (o boneco da rede corrigido de um salto), e se a ponta virar NaN o simulador recomeça do desenho. Ver o crash do "jogar novamente", abaixo.
  - **Ferramentas** (`src/tools/lobo`, LEIA-ME novo): `malha.py` (alinha, tangentes do Blender, pesos, cadeia do rabo), `pintar.py` (texturas dos dois times), `montar.py` (o GLB) e `atlas.py` (o que as três dividem); o `assar_ao.py` assa em 4096. O `polir.py`, que pintava por cima dos lobos antigos, saiu com eles. Antes de chegar o modelo novo tentei pintar pelagem nos lobos antigos (pelo transferido de outro modelo, normais lisas): ficou em retalhos e foi descartado sem entrar.
  - **Testes**: novo `RaboDoLobo` (15 verificações: parado, a ponta fica 3 cm abaixo e 9 cm mais perto do corpo que no desenho, e anda menos de 1 cm por quadro; correndo de quatro a 7,1 m/s fica na horizontal, até 54 cm atrás e 13 cm acima da raiz; freando vem 30 cm para a frente, sem passar da raiz, e volta ao lugar). O `Personagens` aceita o lobo com 24 ossos ou com os 29 (os 5 do rabo), dizendo por quê. O `testar.sh` inteiro rodou duas vezes com os lobos novos (79/79 de unidade; regressão verde menos um teste por rodada, `RenasceInteiro` numa e `Personagens` na outra, no erro intermitente de sempre, "Handle is not initialized"; os dois passam sozinhos). Os testes do servidor e o online não rodaram de novo: nada de rede mudou aqui.
  - **Conferido em fotos dentro do jogo**: os dois lobos parados (frente, costas, três quartos, rosto), em movimento (andando, correndo, de quatro, freando, agachado, virando) e nos 22 clipes (golpes, uivo, bote, parede, morte), sem a malha abrir. **Não foi jogado.** O olho tem uns 30 texels de largura no atlas do Meshy: de muito perto é uma mancha de luz, não um desenho fino.
  - **As referências** que o Jhon pôs em `src/assets` (`222`, o lobo tático e a escultura `WerewolfBlueMenacing3D`, uns 240 MB, fora do git) continuam lá. As duas pastas têm `.gdignore`; o `.fbx` solto da escultura o Godot importa, e iria no jogo exportado.
- **Aliado não é parede: o lobo não treme nem trava no time** (Jhon, 25/11: "ao apertar W W com o lobo e encostar num aliado, dá um bug, lobo vai pra frente e pra trás piscando e às vezes atravessa o aliado"; "isso pode afetar até o ataque especial e os ataques secundários"; "pense em uma solução definitiva"). Corpos do mesmo time agora passam um pelo outro; o inimigo continua sólido (`src/scripts/actors/body/TeamCollision.cs`, chamado no passo de física do `Player` e do `Bot`, inclusive o boneco da rede).
  - **A causa**: o aliado era sólido como uma parede. O aliado da rede é um boneco posto onde a máquina dele diz (`Bot.RemoteStep` põe a posição, com atraso e sem física): ele aparecia por cima do lobo, e a física tirava o lobo de dentro dele empurrando para trás, a cada quadro — o vai e volta; quando o boneco saltava para a frente, passava através. O bote para no que encosta (`WolfMoveMotor.Dive`) e o aliado contava; o salto e o passo da garrada batiam no aliado do caminho.
  - **A solução**: uma exceção de colisão entre cada par do mesmo time, refeita quando o corpo chega ou troca de time. Vale só para mover o corpo; tiro, garra e visão continuam vendo o aliado e decidem por time, como antes.
  - **A câmera do lobo não recua por gente**: passando pelo aliado, o corpo dele ficava entre o lobo e a câmera, e o braço da câmera saltava de 2,5 m para 0,1 m (medido). Agora só o cenário segura a câmera.
  - **Teste novo, `AliadoNaoTrava`**: de quatro contra o aliado parado, passa por ele sem recuar, a 7,02 m/s, e a câmera fica nos 2,5 m (antes parava nele, a 0,6 m; atravessando sem a câmera corrigida, ela caía a 0,1 m); com o aliado da rede posto por cima do lobo a cada quadro, 0 quadros para trás (antes 59, empurrado a −8,4 m/s); o inimigo ainda barra (para a 0,6 m dele); o salto carregado com um aliado a 3,5 m anda os 12 m do alcance (antes parava a 2,9 m); a garra com aliado e inimigo colados acerta só o inimigo.
  - **Não testado em jogo, nem online com gente.** O inimigo da rede, posto por cima, ainda empurra (é sólido de propósito). Bots do mesmo time podem se sobrepor andando juntos.
- **Online: entrar no meio passa pela sala, com times equilibrados, e partida quase acabando fecha** (Jhon, 25/11: "partida com bot deve contabilizar aqui, e não +10 bots"; "se a partida tiver iniciado, ele entra na sala para poder escolher o time"; "ele não pode entrar em time desequilibrado, um time só pode ter 1 a mais do que o outro"; "não pode entrar em sala cuja partida está quase acabando").
  - **Lista de salas**: os bots contam nos lugares (1 jogador + 10 bots aparece **11/12**, sem o "+10 bots"; o detalhe fica na dica do número). Jogando, contam só quem está na partida. O botão é "Entrar" nas duas; sala com a partida quase acabando mostra **acabando** e não deixa entrar.
  - **Entrar com a partida em curso** leva para a **sala**, não direto para a partida. Lá a pessoa aparece "na sala" (quem joga aparece "jogando"), começa no time em que jogou (se voltou e ainda cabe) ou no que está aberto, troca de lado no botão de sempre e entra com **"Entrar na partida"** (mensagem nova `room.enter`). O botão explica quando não dá: cheia, time desequilibrado ou quase acabando.
  - **Equilíbrio**: só entra por um time que tenha lugar e não fique com dois a mais que o outro, gente e bots que estão jogando contados (`Room.CanEnter`). Quem voltou e escolheu o outro lado muda de time no servidor de partida, com os abates que tinha (`Referee.AddSeat`), e os outros veem o boneco dele trocar de cor.
  - **Quase acabando**: o servidor de partida passa a contar, a cada batida, o tempo que falta, a duração e o placar de cada partida (`progress`). O saguão fecha a entrada nos últimos 2 minutos ou nos últimos 20% do tempo, o que for menor (36 s numa partida de 3 minutos), ou quando um time tem 80% dos pontos para vencer (`MatchProgress`, números em `docs/AJUSTES.md`). A lista e quem está na sala de fora ficam sabendo na hora.
  - Achado no caminho: o progresso da partida era ignorado enquanto a escalação esperava ir de novo ao servidor de partida (logo depois de alguém entrar). Corrigido no `Fleet.Heartbeat`.
  - **Testes**: servidor 60/60 (`RejoinTests` refeito para o fluxo novo e um teste novo: bots contados na lista, `time_cheio`, `time_desequilibrado`, fechado faltando pouco tempo, fechado com o placar decidido, aberto de novo numa partida curta com folga); unidade, `RefereeTests.RejoinOnTheOtherTeam`; ponta a ponta, `testar_online.sh --voltar` (o robô agora volta pela sala e entra com `room.enter`). A primeira tentativa do `--voltar` reprovou de verdade: com a regra fixa de 2 minutos, a partida de teste (35 s) ficava "acabando" desde o começo, e uma partida de 3 minutos fecharia em dois terços do tempo. Daí a janela relativa à duração.
  - **Não foi jogado com gente**; a tela da sala de fora da partida não foi vista na tela, só pelo teste com robôs.
- **Lançamento com os testes de volta** (Jhon, 25/11: "volte os testes no CI/CD de release"; tinham saído em 21/11). A tag `v*` roda o `testes.yml` inteiro (semente, iniciador do Windows, unidade e regressão) antes de subir qualquer coisa: o `servidor` (e com ele a `partida`) e os dois clientes esperam os testes; o `importar` do Godot, o job mais demorado, roda ao mesmo tempo que eles, então o lançamento não fica muito mais longo. Teste vermelho = nada vai para o Fly e não sai Release. Conferido só que o YAML é válido e a ordem dos jobs; roda de verdade na próxima tag.
- **CI: o teste do iniciador do Windows compila no Ubuntu** (o job `iniciador` quebrava em "reading stub zlib-amd64-unicode"). O iniciador é de 64 bits (`Target amd64-unicode`), e o NSIS oficial de Windows, o do runner e do choco, só traz os stubs x86; o do apt do Ubuntu traz os amd64, e é com ele que o `lancamento.yml` compila. Agora um job novo, `iniciador-compilar`, compila o iniciador e o jogo de mentira no Ubuntu, e o `iniciador` só roda o teste no Windows com esses binários (`testar_iniciador.ps1 -Pronto <pasta>`), então o teste roda o mesmo binário que vai para o jogador. Conferido aqui que os dois compilam no NSIS 3.09 do Ubuntu 24.04 (o iniciador com os mesmos 136 KB) e que o YAML é válido; **o teste no Windows só roda no próximo push**.
- **Online: sair da partida vai para o lobby, o mestre passa ao próximo, e dá para voltar** (Jhon, 24/11: "ao sair da partida, posso entrar nela novamente caso tenha vaga"; "se eu sair da partida, deve ir para o lobby e o mestre passa a ser o próximo jogador"; "e o jogo continua, mas se não tiver mais players na sala, o jogo é excluído").
  - **Sair**: no Esc da partida online, "Sair para a sala" virou **"Sair da partida"**. Ele tira o jogador da sala (`room.leave`) e leva ao lobby; a partida dos outros continua. O "Sair para o lobby" do resultado também deixa a sala. Saindo o mestre, o mais antigo da sala assume.
  - **Os bots do mestre que sai**: eram movidos pela máquina dele e sumiam da partida junto com ele. Agora passam para o próximo jogador ligado (`Referee.HandBots`; o servidor de partida avisa com o pacote novo `BotOwner`). Na máquina dele, o boneco de cada bot vira bot de verdade, no mesmo lugar. O boneco velho sai de cena sem ser apagado: a câmera de espectador, a mira e o minimapa podiam estar segurando ele.
  - **Voltar ou entrar no meio**: a lista de salas deixa entrar em sala jogando se houver vaga (o servidor manda `open` em cada sala, contando os bots que estão jogando; o botão vira "Entrar na partida").
    - Quem já jogou a partida volta no time e no lugar dele, com os abates que tinha.
    - Quem chega ganha um lugar novo: o saguão põe o lugar na escalação (`Fleet.AddSeat`) e a manda de novo ao servidor de partida como atualização (`update: true`). O servidor de partida abre o lugar (`HostedMatch.AddSeats`), segura o ingresso de quem chegou antes do lugar por até 15 s e avisa os outros com o pacote novo `Seat`. Para os outros, o boneco de quem entrou aparece no meio da partida e ele entra no placar.
    - Quem entra no meio vê os bots como bonecos (`match.start` com `late`) até o servidor dizer que passaram para ele.
  - **Sala vazia**: some, e a partida dela é descartada (sai da frota; o resultado, se chegar, é recusado como de partida desconhecida, como no reiniciar).
  - **O que não vem para quem entra no meio**: os abates que os outros já tinham feito. O placar dos times vem certo (pelo relógio do servidor), mas a linha de cada jogador começa zerada na tela de quem chegou.
  - **Testes**:
    - Unidade, `RefereeTests`: lugar novo, volta com o que tinha, bots para o próximo ligado.
    - Servidor, `RejoinTests`: partida cheia recusa; o mestre sai e o próximo assume com a partida seguindo; quem saiu volta no time dele sem mexer na escalação; quem chega no lugar vago vai na atualização; sala vazia descarta.
    - Ponta a ponta, `src/tools/testar_online.sh --voltar`: o criador sai aos 8 s, os dois bots passam para a máquina do visitante, e o criador volta pela lista de salas e entra de novo no lugar dele.
    - O `testar_online.sh` de sempre passou com o servidor novo.
  - Não foi jogado com gente.
- **Pistola de fora: os dedos juntos, a mão de apoio em concha e o polegar do Barut afinado** (Jhon, 24/11: "dedos tortos, mirado e não mirado, na pistola, mão direita e esquerda, Eve e Barut"; "dedo mínimo e dedo médio tortos, esses devem seguir a direção dos outros, e a articulação também"; "o dedão do Barut parece maior, em ambas as mãos"; "encoste a mão base nos dedos da mão que segura, apenas arrastando para o lado, para formar uma concha com a mão").
  - **Os dedos**: a mão de fora copia a da P9 da primeira pessoa osso por osso, e lá a esquerda fecha o médio a 135° e o mínimo a 105°, com as juntas giradas até 72°, contra 61–70° do indicador e do anelar. Na primeira pessoa esses dedos ficam escondidos atrás da arma; de frente, ficavam tortos. Agora, na pistola, o médio dobra como a média do indicador e do anelar, e o mínimo como o anelar (`PISTOLA_DEDOS_JUNTOS` em `mao_manny_anim.py`). Na esquerda, o médio fecha 69° com as juntas até 9° fora da linha (eram 135° e 72°); o mínimo fecha 59°, com até 19° (eram 105° e 48°). Na direita, o médio vai de 33° fora para 8°. Na recarga a esquerda segura o pente e fica com os dedos de lá.
  - **A concha**: a palma esquerda ficava a 26–32 mm das pontas dos dedos da direita. Ela anda de lado (na horizontal, perpendicular ao cano) 48 mm na Eve e 52 no Barut (`PISTOLA_CONCHA`), e fica a 4,5–6,6 mm da pele deles, sem passar da luva (uma mão entra na outra até 5,3 mm). Vale só com ela no punho da arma: some quando sai para o pente e no começo do saque. Medida nova: `MEDIR_CONCHA` (`=curva` mostra o vão a cada 5 mm).
  - **E recua** (Jhon: "mão mais para trás, para os dedos, da frente ao dedão, encostarem o máximo possível nos dedos que seguram a arma; o dedão da mão base ficará atrás dos dedos da mão da arma"). Os dedos dela estavam à frente, abaixo e um pouco ao lado dos da direita (36 mm, na mediana). Ela recua ao longo do cano 25 mm na Eve e 30 no Barut, com um ajuste de lado e de altura (`PISTOLA_RECUO`, achado com `MEDIR_RECUO=busca`, que testa recuo, lado e altura e fica com o que mais encosta os dedos com a mão entrando até 6 mm). Pontos dos dedos encostando nos da direita: de 4 para 18–23 de 387 na Eve e 26 de 445 no Barut; a distância mediana caiu de 36 para 16 mm. Mais que isso a mão entra na outra (com 50 mm de recuo, 16 a 22 mm dentro). **O polegar esquerdo ainda não fica atrás dos dedos da direita**: está 32–38 mm à frente do começo deles. Levá-lo para trás com a mão inteira a enfiaria na outra; dá para fazer fechando os dedos dela por cima dos da direita, ainda não feito.
  - **O polegar do Barut**: o esqueleto da mão é o mesmo da Eve, mas a luva do kit tinha o polegar com o dobro da grossura do indicador (raio médio da pele 19,5 mm). Ferramenta nova `src/tools/personagens/afinar_polegar.py`: aproxima a pele de cada falange do osso, direto nas posições do `humano.glb` (o resto do arquivo fica igual). Com o fator 0,6, 351 vértices andaram até 9,8 mm, e o raio foi a 13,4 e 12,9 mm, como o da Eve (13,4 e 13,7).
  - A `deagle.json` da Eve e do Barut foi gerada de novo. Para comparar com a primeira pessoa, `FOTOS_LA=1` fotografa a mão de lá (`maos.glb`) no lugar da daqui. Conferido em fotos no Blender e no jogo (o boneco, parado e mirando); não foi jogado.
- **O código repetido juntado, e o `testar.sh` passa inteiro** (Jhon, 24/11: "resolva o problema do teste"). O `duplicados.py` reprovava cinco cópias: o degrau à frente do jogador e do bot (`BodyMotion.ProbeStep`), o ruído dos fundos do login e do menu (`ShaderBits.ValueNoise`), o ícone de forma do placar e do Tab (`Icon.ShowForm`), o quadril e o arco das animações do lobo e da pose do herói (`AnimationKit.MoveHips` e `Arc`, agora públicos) e a nuvem de partículas do choque e da poeira da corrida (`FxQuads.Cloud`). Junto, os geradores de pose e animação das armas (`poses_armas.py`, `animacoes_armas.py`, `armas/mira.py`) acham de novo os arquivos com o caminho `res://src/` ("Please select a file").
- **Faca e granada saem da bainha e do bolso na terceira pessoa, conferido** (Jhon, 24/11: "valide se a granada surge na mão em 3ª pessoa pegando do bolso, igual às armas; e valide isso para a faca também"). O teste `ArmaNovaNaMao` cobre agora Eve e Barut, faca e granada: na troca da pistola, a peça fica escondida 8 quadros enquanto a mão desce, aparece com a mão a 22–25 cm do quadril (a bainha e o bolso) e, visível, anda presa na mão (a até 2,8 cm na faca e 0,2 cm na granada). Conferido também em fotos quadro a quadro: a faca surge no quadril com a lâmina para baixo e sobe para a guarda; a granada surge na frente do bolso.
- **A granada da terceira pessoa conferida: do tamanho da primeira** (Jhon, 24/11: "valide se o tamanho da granada em 3ª pessoa está o mesmo da 1ª"). Teste novo `GranadaMesmoTamanho`: a escala do osso que leva a granada (Grip) no boneco da Eve e do Barut é a mesma do conjunto da primeira pessoa (1,000 e 1,000; medida no espaço do conjunto, sem o encolher e esticar que a primeira pessoa faz para a tela). Com o crescimento antigo (1,4×) o teste reprova.
- **Neon: placas de anúncio sólidas e portas, salas e pátios abertos** (Jhon, 24/11, com fotos: "essas placas estão sem colisão, então estou passando por elas, e nesses locais não consigo entrar"). As 14 placas de anúncio (`SM_Props_Ads`), que o gerador deixava sem colisão, colidem na forma delas (26 de 28 tiros nelas param; os 2 que passam vão rente à borda fina). E as 78 paredes invisíveis "Barreira…" saíram: o gerador as punha onde o ladrilho da rua acaba, e elas tapavam portas, salas e pátios dos prédios do norte e do sul. Medido numa grade de 0,5 m do que se anda na altura do corpo: sem elas, o andável a partir da rua vai de 9 988 para 10 514 m², todo em cômodo ou pátio fechado, e nenhum ponto dele encosta na borda do mapa (os prédios já fecham a cidade; as paredes de limite ficam). `colisao_exata.gd` ganhou as duas coisas (`SOLIDAS_SEM_COLISAO` e `--sem-barreiras`); o teste `NeonColisaoFina` confere as placas, que não há barreira dentro do mapa e que os limites ficam. Não foi jogado.
- **Neon: postes, placas e peças pequenas colidem na forma delas** (Jhon, 24/11: "postes, placas e coisas pequenas estão com áreas de colisão muito grandes: não consegui andar em regiões que pode andar, não consegui atirar em oponentes nessas regiões"). O gerador do Neon punha uma caixa do tamanho total da peça em 476 delas; no poste de luz com braço era uma parede invisível de 1,5 × 4 m, na mesa um bloco até o chão. Agora 216 peças finas ou recortadas (os 54 postes, placas e letreiros, mesas e cadeiras, bancos, móveis, quiosques, telas, canteiros, pedras e troncos) colidem na própria malha (31 formas, divididas entre as peças iguais; 117 mil triângulos, a Jolt dá conta). Caixa ficou no que é bloco de verdade (caixote, máquina, lixeira, carro, laje), nos itens do mercado (a caixa deles já é justa) e nas paredes invisíveis da borda. Ferramenta nova, `src/tools/mapas/colisao_exata.gd` (grava na cena, que é a fonte do mapa; `--seco` só conta). O minimapa desenha também essas peças. Teste novo `NeonColisaoFina`: dos tiros que passam a meio metro de um poste, 0 de 216 batem nele (antes, 108); dos tiros no poste, 98 de 108 param nele. O `MapaNeon` (bots lutando no mapa) continua passando. Não foi jogado.
- **Painel local na 8443** (Jhon, 24/11: "a porta 8081 precisa ficar disponível para outras coisas"). O `docker compose` publica o painel em `http://127.0.0.1:8443` (a mesma porta do painel no Fly); dentro do contêiner ele segue na 8081, e a produção não muda. Mudaram junto o `.env.exemplo`, o `vite.config.ts` do painel (`npm run dev`) e o `SERVIDOR.md`/`PAINEL.md`.
- **Docs alinhados ao jogo de hoje**: `ARSENAL`, `CONJUNTOS`, `AJUSTES`, `AUDIO`, `DESIGN`, `ARQUITETURA`, `PACOTES` e `MAPAS` não descrevem mais armas, mapas, personagens e geradores que saíram (viraram notas curtas de "saiu em"); as armas de hoje têm ficha, e o mapa tem a cena como fonte.
- **Ferramentas: saíram as que não servem mais e as desalinhadas com o jogo** (Jhon, 24/11: "pode apagar"; "pode excluir esses"). Sem uso: `gerar_armas.py` (rodar apagaria a ficha de várias armas do `armas.json`), `gerar_arsenal.py` (gerava a M9), `gerar_miniaturas.gd` (vazio), as mãos antigas (`armas/maos_lowpoly.py`, `armas/blender/maos.py`), `rugido/rugido.py` (trocado pelo `rugido2.py`), `mapas/blender/malha.py`, `mapas/pecas.py`, `mapas/blender/pecas_polyhaven.py` e `armas/blender/bracos.py`. Desalinhados: `sons/processar.py` e `sons/baixar.py` (geravam o som das armas antigas), `mapas/texturas.py` e `baixar_pacotes.py` (traziam a Vila e a Polar), `armas/blender/infima.py` (o `.blend` de origem do AR-15 não existe mais), `personagens/scifi.py` e `personagens/texturas.py` (refaziam a Eve e o Barut desfazendo os ajustes de mão e braço) e `gerar_mapas.py`. O `conferir.py` dos mapas conta cada objeto como uma caixa de 30 cm (a tabela de tamanhos era do `pecas.py`). Sem os geradores de entrada, ficaram sem quem os chame: os módulos dos 6 mapas (`mapas/*.py`, que o `gerar_mapas.py` montava) e o `ar15_detalhe.py`/`ar15_ultimate.py`, que só rodam sobre o `ar15.glb` que já existe. Os testes e o CI não dependiam de nenhum deles.
- **Personagens numa pasta só** (Jhon, 24/11: "por que existe a pasta characters e a pasta personagens em assets? organize melhor isso, tá confuso"). `src/assets/characters/` saiu: o que é de todos foi para `src/assets/personagens/_comum/` (`humano/`: o X Bot e as animações do Mixamo; `lobo/`: as animações do lobo), e cada lobo foi para a pasta do personagem que o usa (`eve/lobo_azul.glb`, `barut/lobo_vermelho.glb`). A `_comum` não tem `personagem.tres`, então o jogo, o servidor e o painel não a listam como personagem. 63 arquivos com caminho trocado (cenas, código, testes, geradores, `.gitignore`, docs); a semente do servidor não mudou.
- **Limpeza do repositório** (Jhon, 24/11: "pode remover"; "trenchbroom pode excluir"). Saiu, depois de conferir que nada carrega (nem por caminho, nem por uid, nem pelo nome): uma cópia antiga do Mutant dentro de `src/scripts/actors/body/` (99 MB); os arquivos do lobo que o código não usa (`transform`, `idle_alerta`, `Werewolf_Quadruped_Run_v2`, `mutant`, `mutant_red`) e o `analisar_golpes.gd`, que media os golpes no Mutant; as malhas antigas das armas (as sem o id no nome); os restos do Armazém original (`pecas/armazem.glb`, a paleta, o detalhe, o `armazem.json` e a `montar()` do `armazem.py`); texturas extraídas de modelos que já saíram; o céu da Polar e os sons de ambiente da Polar, da Vila e do Metrô; as insígnias de patente (e o `patentes.py`; os créditos citam agora o `bordas.py`), as 13 fotos de perfil antigas, o ícone antigo de Sangue e as fontes dos letreiros. E o TrenchBroom: o plugin `func_godot` (`addons/`), a config dele e a linha no `project.godot`. Uns 190 MB a menos no git (e no jogo exportado, que leva tudo de `src/`). O que tinha confiança média ficou anotado no `PENDENCIAS.md`.
- **O jogo foi para `src/`** (Jhon, 24/11: "mova todos os dados do jogo para src"; "somente as pastas vão para src, o que precisa ficar na raiz pode ficar"; "servidor não precisa"). `assets`, `scripts`, `scenes`, `resources`, `tests` e `tools` agora moram em `src/` (no Godot, `res://src/...`: 2 748 caminhos em 1 810 arquivos, os `.import` incluídos). Na raiz ficam `project.godot`, `WolfLeague.csproj`/`.sln`, `export_presets.cfg`, `icon.svg`, `addons/` (o Godot só acha plugin em `res://addons`), `servidor/`, `docs/`, `instalador/`, os arquivos do Docker e do Fly e o `.github/`. Os comandos rodam da raiz: `src/tools/testar.sh`. Ajustados junto: os workflows (testes e lançamento), os Dockerfiles do servidor e da partida e os `.dockerignore`, o `Alcateia.Dominio.csproj` (as regras de recompensa e de fúria vêm de `src/scripts/domain`), o painel (`imagens.mjs`), o `.gitignore`, os filtros de exportação, o instalador (`gerar_arte.py`), os scripts de `src/tools` que entram na raiz, e o `exportar_dados.py` (grava a semente em `servidor/`). A primeira importação depois da mudança refaz o cache inteiro (aqui, 22 min). O CLAUDE.md, o CONTRIBUTING e os `docs/` apontam os caminhos novos; o CHANGELOG, por ser histórico, não. O repositório no GitHub se chama `wolfleague` (o remoto e os links do Discord foram trocados).
- **Os mapas antigos saíram** (Jhon, 24/11: "mapas que não são utilizados no jogo podem ser removidos e nem devem aparecer no dashboard"). O Armazém original, a Vila e a Estação Polar (a lista `Maps.Guardados`) foram apagados: cenas, modelos, fotos, letreiros e os geradores `vila.py`, `polar.py` e `letreiros.py` (o `armazem.py` ficou: o Armazém de hoje usa o código dele). Salas e perfis antigos com esses ids caem no Armazém. No servidor, a semente agora apaga do banco o mapa que o jogo não tem mais (`PgGameDataRepository.SeedMaps`), então eles somem do painel na próxima subida; a semente tem 6 mapas, todos ligados. Testes do servidor ajustados (`GameDataTests`, `PanelTests`); o teste de som que usava a Polar usa o Armazém (madeira e metal).
- **Os testes alinhados ao jogo de hoje** (Jhon, 24/11: "veja os testes automatizados que estão falhando"; "alinhe os testes ao estado atual do jogo, não o contrário"). Quase todos falhavam por o jogo ter mudado de propósito sem o teste acompanhar:
  - **Velocidades** (`VelocidadeNoChao`, `CorridaNaParede`, `PassadaDoLobo`, `PassoNoRitmo`): a cena do jogador põe `HumanSpeed` 3,2 (o código diz 3,0), e o `docs/MEDIDAS.md` já registrava 3,46 m/s; os testes e o CLAUDE.md seguiam 3,24. Agora: humano 3,46 (faca 3,58), Bruto 4,15, na parede 4,75; o limite de patinação do lobo para trás foi a 0,5 m/s (mede 0,46); a passada do som confere o 1,19 do perfil (o ritmo dos passos, que é o que se ouve, já batia).
  - **`Medidas`**: o pulo correndo do humano vai 2,6 m (o teste e o `MEDIDAS.md` diziam 3,2).
  - **Em chamas** (6 testes de unidade): as regras mudaram em 23/11 (acende com 300, três abates; apaga abaixo de 150; 6 s de folga e 40/s) e os testes eram das de 21/11.
  - **`Animacoes`**: `crouch_idle`, `fall` e `land` saíram em 16/11 e seguem fora; o teste ainda os pedia.
  - **`MaosPadrao`**: as mãos novas (20/11) são de shader, com 8 986 triângulos; o teste pedia material de paleta e menos de 4 000.
  - **`Suavizacao`**: a qualidade muda só o custo dos efeitos, todas com a mesma luz e sem o reflexo da Ultra; o teste pedia a Baixa sem efeitos e a Ultra com reflexo.
  - **`GranadaFere`**: a granada que encosta em algo ao surgir conta o pavio (1,2 s); o teste esperava 0,3 s e checava antes de ela estourar.
  - **Servidor** (`src/tools/testar_servidor.sh`, 58 de 58): o "em chamas" com as regras novas (três abates, 26 s); a patente e o que a promoção paga conferidos pela própria regra (`RankRule`), não por números fixos que os prêmios de hoje já passaram; e o "último mapa ligado" desliga também a Neon, que entrou depois do teste.
  - Dois consertos pequenos no jogo, para o Output ficar limpo: sem tela (testes, servidor de partida) o Ctrl-clique do Mac não pergunta mais o mouse ao sistema ("Mouse is not supported", `CtrlCliqueNoMac`), e o login só põe o foco no campo se a tela ainda estiver aberta (`Login`).
- **Moedas com nome e ícone novos** (Jhon, 24/11: "Presas vira Cash, Sangue vira IPs, WP vira KP, kill points"; "quero ícones profissionais em Cash e IPs"). Na tela: **Cash** (era Presas, a moeda ganha jogando), **IPs**, Inipoints (era Sangue, a paga), e **KP** (era WP, o da partida: conjunto ao renascer e transformação). São 33 textos, com o inglês e o espanhol levados junto em `assets/i18n/textos.csv`; a concordância foi refeita ("o seu Cash", "Cash insuficiente", "Faltam {0} de Cash"). "Sangue ao acertar" (a opção do respingo) e o item "Presas de Aço" continuam. Os ícones são novos e coloridos (`tools/icones/moedas/*.svg`, em `assets/ui/moedas/`): uma moeda de ouro com aro, relevo e um C cortado por duas barras (Cash) e uma moeda violeta com uma gema lapidada (IPs); o `Icon` os desenha com as cores deles, sem pintar por cima. A sigla depois do número virou C e IP, e as cores dos números, o ouro e o violeta dos ícones. No código, no perfil salvo e no servidor os nomes de antes continuam (`Currency.Presas`, `"presas"`, `Wp`): trocar lá mexe no banco e no protocolo. Conferido numa foto da loja; o resto das telas não foi visto.
- **AR-15 8 Bit mais pixelada, laranja e amarela, com azul** (Jhon, 24/11, com uma AK em pixel art de referência: "a arma 8bit deve ser bem mais pixelada, esse é o estilo dela"; "a 8bit é laranja e amarelo, sem a parte escura"; "coloque um azul na 8bit também, em algum lugar"). Blocos de 2,6 cm (eram 1,3; `PIXEL_BLOCO` em `tools/armas/peles.py`), um em cada três trocando de tom, contorno 2,5 vezes mais grosso; a caixa e o cano, que eram pretos, ficaram laranja; o silenciador, azul; o preto ficou só no contorno. A foto da interface (`assets/ui/armas/ar15_8bit.png`) foi tirada de novo. O trilho de cima ainda sai escuro (os dentes dele são finos e o contorno grosso toma conta).
- **A granada da terceira pessoa do tamanho da primeira** (Jhon, 24/11). Na mão de fora ela era 1,4 vez a de dentro (`HeldWeapon.GrenadeHeldGrow`, agora 1); no Blender a mão não abre mais para ela e a argola não cresce (`GRANADA_MAO_ABRE` 0, `GRANADA_CRESCE` 1), e a `granada.json` da Eve e do Barut foi gerada de novo (as medidas da mão iguais às de antes). Não foi conferido no jogo.
- **Faca: a estocada no botão direito, e sangue nas facadas** (Jhon, 24/11: "tire o F [...] vamos pegar somente o primeiro trecho da animação, que é a empunhalada para frente, sem a outra parte do giro da faca [...] será um ataque secundário da faca, dará mais dano que o primário e é feito pelo botão direito, é um ataque mais lento, mas eficaz caso pegue o adversário de costas"; "as facadas tiram sangue do oponente?").
  - **O golpe** (`Weapon.TryStab`, `Weapon.Knife.cs`): o dano sai 0,2 s depois do clique, quando a lâmina chega à frente; a faca fica ocupada 0,8 s (o golpe rápido, 0,40 s). De frente tira 120 (o rápido, 85); pelas costas, o dobro, 240, que mata um humano de vida cheia. "Pelas costas" é o golpe ir para onde o alvo olha, até 60° de diferença (`CombatRules.FromBehind` e `KnifeStab`, com teste de unidade). Na cabeça vale o multiplicador da cabeça; costas e cabeça não somam. O servidor aceita até a estocada pelas costas (`HitCheck`).
  - **A primeira pessoa** toca os quadros 0 a 0,633 s do "inspecionar" da faca, a nova `"estocada"` da ficha (`bracos.json`, e `tools/armas/conjuntos.py` para ela voltar se a faca for refeita): a mão sobe, crava para a frente e volta exatamente à pose parada. O resto do clipe (o giro) não toca mais em lugar nenhum.
  - **A terceira pessoa**: o clipe "estocada" no `faca.json` da Eve e do Barut (`mao_manny_anim.py`, `FACA_GOLPES`): a mão sobe ao lado do ombro e crava para a frente, de cima para baixo, com o tronco girando junto. Os pontos foram escolhidos pelo punho: com a mão esticada na altura do peito ele dobrava 57° e ia 81° de lado; ficou em até 28° e 29° (o de lá vai a 14° e 18°). Os outros clipes da faca saíram idênticos.
  - **Na rede** vai no `Attack` da faca, com um byte no fim (`NetWire.KnifeStabMove`): quem vê de fora vê a estocada, não o golpe rápido.
  - **A tecla F saiu** (olhava a arma na mão). As outras armas ainda se olham sozinhas depois de um tempo parado; a faca não (acima).
  - **Sangue**: o golpe rápido e a estocada agora tiram sangue e fazem o som do impacto, como o tiro e a garra (antes a faca não chamava o sangue).
  - Testes novos: `EstocadaDaFaca` (anima na primeira pessoa, o dano só sai no contato, 120 de frente sem matar, ocupada logo depois, pelas costas mata) e `EstocadaDeFora` (o boneco da Eve e do Barut toca a estocada que chega pela rede e não fere ninguém). Conferido em fotos do clipe da Eve; não foi jogado.
- **Faca: sem a animação sozinha, parada** (Jhon, 24/11: "pode tirar a animação idle da faca, a que ela faz movimentos"). Na primeira pessoa, depois de 9 s parado e a cada 16 s, a faca girava na mão (o "inspecionar" do conjunto). Na faca isso saiu (`ViewModel.KnifeIdleInspect`, desligado); as outras armas continuam se olhando sozinhas. A tecla F, que também tocava, saiu junto com a estocada (acima). Não foi conferido no jogo.
- **Release: a capa com o ícone de agora, e a release na lateral do repositório** (Jhon, 24/11). A capa (`instalador/capa.png`) tinha ficado com o lobo antigo, de olhos laranja: o `gerar_arte.py` não tinha sido rodado de novo depois da troca do ícone. Foi gerada de novo; o resto da arte do instalador já estava em dia. E as versões saíam como *pre-release*, que o GitHub nunca mostra na lateral ("Releases"), só na lista; agora saem como a mais recente (`--latest`), e rodar de novo numa tag que já tem release também a promove. A capa da alpha.7 continua a antiga: o endereço dela aponta para a tag.
- **Lobo: o ataque não para mais a corrida na parede** (Jhon, 24/11: "se eu estiver na parede, apertar o botão de ataque não deve parar o lobo, ele deve continuar andando"). Segurar o ataque na parede "agarrava": o lobo freava até parar e ficava nela mesmo sem o W. Esse agarrar saiu. Na parede, o clique continua sem arranhar (`SemGarraNaParede`) e a corrida segue só com o W. O `WallBrake` sobrou para quando o lobo chega à parede mais rápido que a corrida dela. O teste `AgarraNaParede` virou `AtaqueNaoParaNaParede`: segurando ou clicando, ele corre na velocidade da parede.
- **HUD: o número e as estrelas do nível no retrato** (Jhon, 24/11: "as estrelas do rank não aparecem na HUD do jogo"). O retrato do canto (`HexBadge`) só desenhava a borda; a placa com o número e as estrelas, que o perfil tem (`LevelPlate`), não tinha sido posta nele. Agora fica no vértice de baixo do hexágono, como no perfil (`LevelPlate.Side`: o lado do retrato, porque a caixa do HUD é maior que ele). Conferido numa imagem com dez níveis, de 6 a 1690, com e sem louros. Na partida, com o HUD inteiro, não foi visto.
- **A pistola pela P9: a pegada, o saque e as recargas, na primeira e na terceira pessoa** (Jhon, 23/11: "joguei a P9 Manny para você ter ideia de como fazer a mão segurando, a recarga da pistola e o draw"; "Fire e Walk não precisa, somente draw, idle, reload"). O pacote (`P9 Manny FPS Animations`, Sketchfab Standard) tem o esqueleto da nossa primeira pessoa (o Manny do UE5: mesmos ossos, mesmo repouso, mesmo tamanho de mão), então o movimento passa direto: `tools/armas/blender/p9.py` assa os três clipes em `tools/armas/dados/p9.npz` e `RefP9` (`conjuntos.py`) leva para a Desert Eagle. O `.glb` foi para `assets/pacotes/sketchfab/` (fora do Godot e do build).
  - **A pegada**: as duas mãos altas no punho, os pulsos dobrados e os antebraços vindo de baixo, como na P9 (os nossos vinham deitados, de trás); a direita vai inteira (lugar e dedos; o indicador vai ao gatilho desta arma, 0 mm). As nossas mãos são mais grossas que as do Manny de lá: com a pose de lá, os dedos da esquerda entravam 2 a 3 cm nos da direita (a própria P9 tem as mãos se atravessando). A esquerda sai do lugar dela na P9 e se afasta até a palma encostar; cada dedo fecha a partir da pose de lá até encostar (0,3 a 0,9 mm, nada atravessa) e o polegar deita para a frente (1,2 mm).
  - **O saque** (0,36 s): a pistola sobe de baixo, à direita, e a mão esquerda vem encontrá-la — o da P9 (0,58 s) no nosso tempo.
  - **As recargas** (1,67 s e 2,17 s, os tempos do jogo; a da P9 tem 2,67 s): o gesto da arma, o caminho da mão esquerda, os antebraços e os tempos da P9. A mão desce ao pé do carregador, puxa-o para baixo e volta com o novo; o carregador sai e entra reto pelo poço daqui (o da P9 é 6° mais inclinado: o caminho de lá é girado em volta do alto do carregador). Na vazia, o velho cai sozinho e o ferrolho, travado atrás, solta depois que o novo assenta. A arma continua subindo 6 cm durante a troca.
  - Na troca do carregador, 60% do gesto da arma da P9, e a arma gira o cano para dentro e tomba (`RECARGA_GIRO_P9`), como a da Glock fazia: com o gesto inteiro o cano subia 30° e, de fora, a Eve levava a pistola ao rosto com o cotovelo direito apontando para cima (170° do para baixo); só com os 60%, o cotovelo abria para o lado (100°). Agora 32° (era 26° com a Glock) e o braço não entra no corpo além do encosto do ombro de sempre (medidas novas no `mao_manny_anim.py`: `MEDIR_COTOVELO`).
  - A terceira pessoa (Eve, `mao_manny_anim.py`; Barut, `animacoes_armas.py`) foi refeita sobre a primeira. No saque da Eve, a braçadeira do braço direito afunda até 37 mm na pele do braço num quadro (era 25 mm no saque de antes): é a malha dela no ombro dobrado, não o braço no tronco.
  - **A pistola reta na terceira pessoa** (Jhon, 24/11: "pulso e arma precisam estar retas no 3 pessoa"). A terceira pessoa copiava a dobra do pulso da primeira, e a pegada da P9 dobra o pulso direito 47° e o leva 52° para o lado: de fora, a Eve segurava a pistola com o pulso quebrado e o cano 21° para baixo. Agora (`PISTOLA_RETA`, `mao_manny_anim.py`) o cano fica na horizontal e a arma anda (frente, lado, altura; até 4° de giro) até os dois pulsos ficarem retos: o cano a 3° da horizontal no parado e a 0–3° na mira, e o pulso direito sem ir para o lado (52° → 10°). A dobra que sobra no direito (30° parado, 39° na mira) é a da mão na pegada da P9; para zerá-la a arma teria de girar dentro da mão. O preço: os cotovelos abrem um pouco mais (o direito a 36° do para baixo no parado, era 21°; 74° na mira, era 67°). Visto no jogo na vitrine (`scenes/debug/vitrine_eve.tscn`). O Barut (`animacoes_armas.py`) não mudou.
- **Lobo: garra no ar depois de pular da corrida de quatro** (Jhon, 23/11: "quando tô correndo de 4 com o lobo e pulo, não consigo dar ataque primário, só secundário"). A corrida de quatro segue ligada no pulo com o W apertado (para voltar correndo ao cair) e prendia a garra junto. A regra "de quatro não arranha" agora vale só no chão (`Player.Actions`). O `SemGarraNaCorrida` ganhou o caso do pulo.
- **Lobo: sem garra na parede, e na hora ao sair dela** (Jhon, 24/11: "andar na parede com o lobo não pode atacar, mas se sair da parede tem que responder aos ataques imediatamente"). Antes o clique arranhava na parede e o seguinte ficava guardado, saindo depois. Agora, na parede, o botão só agarra (como já fazia) e o clique não arranha nem fica guardado; ao grudar, a garrada que vinha do ar é cortada (`Weapon.CutClaw`: o dano que ainda não saiu não sai, a garra some da tela), então, saltou da parede, o clique arranha no quadro seguinte. Teste `SemGarraNaParede`.
- **O rugido (R) volta com os efeitos de antes, com a voz de agora** (Jhon, 24/11: "o rugido deve voltar a ser como era antes (somente o R), com os efeitos visuais e sonoros, mas o rugido tem que ser o que tá atualmente"). O que saiu em 23/11 voltou: os fachos de luz do alto, as fagulhas, a poça de luz nos pés, a onda no chão, o ar da boca, o anel do escudo cheio e a luz que clareia o lobo (`RoarBurst`, `WolfFx` como estavam antes do 9bb989d), e, no som, a energia carregando (`lobo_uivo_energia`) e o estalo do escudo cheio (`lobo_uivo_escudo`). A voz continua a de agora (`lobo_rugido`), e virar lobo e voltar (tecla 3) continuam com o "puff". Os choques (`lobo_uivo_choque`) ficam no disco. Conferido no jogo, em fotos de um lobo rugindo (sem som: os jogos rodaram sem áudio). `EfeitosDoLobo` voltou a conferir a luz baixa do uivo (até 2,5 m dos pés) e passa.
- **Lançamento: o Godot que cai ao fechar depois de exportar não derruba mais o build** (Jhon, 24/11: o Windows deu "Segmentation fault" logo depois de "[ DONE ] savepack"). No Windows e no Mac (`lancamento.yml`) a exportação vale se o pack terminou e os arquivos estão lá; se a queda vier antes do fim, tenta de novo uma vez. Simulado aqui (um Godot falso que grava, imprime o DONE e cai com 139); **não rodado no GitHub** até a tag nova.
- **O cotovelo direito do Barut para fora na mira do fuzil, como o da Eve** (Jhon, 24/11: "na mira do fuzil é o cotovelo do barut, o da eve tá pra fora, do barut deve ficar igual"). Com a mesma preferência de cotovelo para baixo (`COTOVELO_BAIXO`), as proporções dele deixavam o cotovelo colado ao corpo: 14° do para baixo em todos os fuzis, contra 15° a 52° da Eve (AK-74 52°, SVD 48°, SCAR 33°, AR-15 30°, M4A4 15°). Agora, na mira, o braço direito do Barut prefere o cotovelo para fora (`COTOVELO_D_MIRA`, por personagem; misturado no mirar e no desmirar): 62° a 64° em todos. O braço dele segura mais esticado (o cotovelo a 15 cm da linha ombro-pulso; o da Eve, a 19), e a 64° o cotovelo sai para o lado 13,5 cm, o que o dela sai (15 cm). Os quadros guardam quanto são da mira (`w_mira`, `mao_manny.bases_do_clipe`). Os 5 fuzis do Barut regerados; a Eve não muda. Conferido no jogo, de frente e de trás, na mira com tiro dos 5 fuzis (a Eve, o Barut de antes e o de agora lado a lado). `BonecoMaos` passa. Os de antes: `.synctmp/barut_como_eve_2411/antes_cotovelo/`.
- **Na troca de arma a arma nova sai da mão, e o punho reto no saque da faca** (Jhon, 24/11, comparando a Eve e o Barut: a faca solta embaixo da mão; "verifique a granada tmb, ela precisa sair do bolso, no momento, ela surge fora da mão e depois se desloca para mão"; "pulso torto"). Na troca o corpo leva 0,18 s (`PoseBlendTime`) para chegar à pose do saque, e a arma nova ia direto para o fim do caminho: medido no jogo, ficava até 78–81 cm da mão na faca e na granada, 46–61 cm nos fuzis e 45 cm na pistola, nos dois personagens. Agora (`HeldWeapon.HoldByClips`), enquanto o corpo chega, a arma vai presa na mão direita, onde o clipe a põe nela, e no saque ela só aparece quando a mão está chegando ao bolso, à bainha ou atrás (`DrawShowAt` 0,85): a mão desce vazia e a granada sai do bolso com ela. O saque da faca tinha o punho direito 67° para o lado na bainha (63° na Eve): a pegada de lá com a lâmina para baixo só cabia quebrando o punho. Agora o punho fica dentro de 25° de dobra e 15° para o lado (`FACA_SACAR_PUNHO`, `mao_manny_anim.py`) e a faca gira junto com a palma (a lâmina aponta mais para baixo na bainha, −69° contra −14°); no fim do saque o punho está em 12°/15°, o da guarda (10°/16°). Faca da Eve e do Barut regeradas. Conferido no jogo, quadro a quadro, na vitrine: a troca da pistola para a faca e o saque da granada, nos dois. Teste novo: `ArmaNovaNaMao` (sem o conserto, a faca ficava a 64 cm da mão). Fica: no meio de um gesto rápido (o saque) a mão do conjunto e a do corpo se separam até 2 cm entre dois quadros do clipe (30 por segundo), e voltam a 0 no quadro do clipe; a pistola e o fuzil sacam com o punho até 29–36° para o lado no meio do caminho (não mexido). As facas de antes: `.synctmp/barut_como_eve_2411/`.
- **O Barut com tudo o que a Eve ganhou na terceira pessoa** (Jhon, 24/11: "tudo que fizemos com Eve, vamos aplicar ao barut"). O mesmo caminho dela, sem mudar uma linha dos geradores: os ossos da mão do Manny na pose T (`mao_manny_tpose.py barut --aplicar`: metacarpos, dedos com os nomes e as juntas do Manny, a palma com o comprimento e a largura da dele, 0,68× a de antes), o osso de torção no braço de cima (`torcao_braco.py barut --aplicar`: 236 vértices no direito, 189 no esquerdo) e as 8 armas regeradas pelo `mao_manny_anim.py barut <arma>`. Com isso ele ganha a mão copiando a da primeira pessoa osso por osso (a mão a 0,0 mm do lugar e cada junta a até 0,1 mm da de lá, em todos os clipes de segurar), o saque de trás, a pistola esticada, reta e apertada com as duas mãos (`PISTOLA_RETA`), a faca desenhada, o lance da granada do Mixamo com a palma para o rosto, o pino que cai, a granada tirada do bolso e a recarga que troca o pente. A arma deixa de crescer na mão dele (era 1,30× na pistola, na faca e na granada; com a mão do tamanho da do Manny, 1,00 como na Eve). Conferido no jogo, em fotos da vitrine com o Barut (`vitrine_eve.tscn` com `Team = 2`): as 8 armas paradas, mirando, sacando e recarregando, o pente caindo, a faca e a granada armada e lançada. Passam `BonecoMaos` (Eve e Barut), `Personagens`, `CabecaNoLugar`, `BracosNaMao`, `SacaEGuarda`, `BonecoMarcas`, `Granada`, `GranadaNaCruz`, `BotsJogamGranada` e `ArmaAcompanhaCorpo`. O `humano.glb` e os `.json` de antes: `.synctmp/barut_como_eve_2411/`.
- **A arma vista de fora não descola mais das mãos com o corpo andando** (Jhon, 24/11: saindo de agachada para o pulo, a pistola da Eve aparecia uns 25 cm à frente e acima das mãos por um quadro; "deve acontecer com outras armas também"). A arma na mão vista de fora é uma peça solta (`TopLevel`), posta no mundo a cada quadro; o alisamento do desenho entre os passos da física (`VisualSmoothing`) deixa o modelo do corpo um pouco atrás de onde a física está, e a arma não ia junto. Andando, ela ficava até 5 cm à frente das mãos (um tremor fino o tempo todo, em toda arma, em quem você vê e no seu corpo de fora); saindo de agachado, o corpo sobe 17 cm num passo e a diferença passava de 10 cm. Agora o `HeldWeapon` prende a arma ao alisamento (`VisualSmoothing.Follow`): a distância da pistola à mão, que ia de 15,5 a 35,7 cm, fica entre 23,8 e 24,5 cm em toda a sequência (parado, andando, agachando, pulando). Teste `ArmaAcompanhaCorpo`.

- **Ajustes de 23/11: granada, pistola, bote no ar e o rugido do lobo** (pedidos do Jhon, um por um):
  - **A granada maior e o pino menor, nas duas pessoas** ("granada está muito pequena, pode aumentar mais um pouco e diminuir o tamanho do pino que aciona ela"). Na primeira pessoa ela passou de 1,18 para 1,40 da de verdade (`conjuntos.py`, `GRANADA`), e a argola do pino ficou com 60% do tamanho (`formas_mao.ARGOLA`: um aro de 3,5 cm, era 5,8). A mão direita foi refeita em volta da bola maior (o polegar atravessava 10,5 mm; agora a faixa dos dedos abre mais e cada dedo encosta a 0,6–0,7 mm); a esquerda, ao entrar na argola menor, encostava 8,9 mm na direita — a argola agora levanta mais cedo e a escolha de quanto ela levanta exige folga na entrada inteira (fica a 2,4 mm no mínimo). Onde a malha nova vai em cada osso (`dados/na_mao.json`) passou a ser calculado pelo `conjuntos.py` (era um número medido uma vez, que ficava velho quando o tamanho mudava; a conta confere com o antigo a 0,5 mm). As cinco pinturas foram refeitas (`peles.py`). Na terceira pessoa ela é 1,4× a da primeira pessoa (1,96 da de verdade; o Barut, que já cresce a arma 1,3× com a mão, cresce só o que falta), e a que voa é desenhada do tamanho da primeira pessoa (`Grenade.ModelScale`, só a aparência).
  - **A mão da granada armada mais para trás e para cima na primeira pessoa** ("quase não mostrando nada"): o pulso 5 cm acima do olho e a 11 cm à frente (era 3,5 cm e 16,5 cm); armada, sobra só a ponta da granada no canto de cima da tela.
  - **A mão esquerda grudada no pino na terceira pessoa** ("grudar mais um pouco a mão no gatilho da granada"): ela soltava a granada aos 0,22 s, antes de o pino sair (0,26 s), e, tratada como mão livre (o punho alinhado com o antebraço), ficava a 12–16 cm da argola. Agora segue a de lá até o pino sair (`LANCE_PINO_SAI` 0,27 s) com a ponta do indicador posta onde a de lá está (0 mm de diferença), dentro da argola crescida do jogo (`GRANADA_CRESCE`).
  - **O pino cai no chão** ("o gatilho da granada deve cair no chão também"): de fora, quando sai, o pino e a argola vão presos na mão esquerda do corpo (na primeira pessoa eles seguem a mão de lá, que some embaixo da câmera, e de fora ficavam soltos no ar); quando ela abre a mão (`LANCE_LARGA` 0,12 s depois), uma cópia cai pela física até o chão e some em 8 s (`HeldWeapon.Pin.cs`, como o pente da recarga; os tempos vão no `.json`, `"pino"`). Na Eve e, desde 24/11, no Barut.
  - **A Eve tira a granada do bolso** ("igual às outras armas"): o saque começa com a mão no bolso da frente da direita, a granada na palma virada para a coxa, e sobe até a pegada girando, com o braço pendurado e a cabeça acompanhando (`Copia.sacar_do_bolso`, `GRANADA_BOLSO`). A granada não entra no corpo (0 mm).
  - **A pistola apertada com as duas mãos** ("dedão da mão base da pistola está para cima, as mãos precisam estar uma apertando a outra, na primeira e terceira pessoa"): a mão de apoio é o espelho da do gatilho e o polegar dela ficava em pé ao lado do ferrolho. Agora ele deita para a frente ao longo da armação, embaixo do da direita (`conjuntos.polegar_a_frente`: a pose do polegar que aponta mais para a frente e um pouco para baixo sem atravessar nada e encostada — 13° do alvo, a 1,9 mm); a mão esquerda continua abraçando a direita (0,6 a 1,7 mm). A terceira pessoa da Eve copia: as duas mãos se encostam (a esquerda aperta a direita até 3 mm). O Barut foi refeito pelo gerador dele.
  - **No ar, o bote corta a garrada** ("se eu estiver no ar apertando o ataque primário e apertar o secundário, ele deve interromper o primário e acionar o secundário imediatamente"): a garrada segurava o bote até acabar. Agora o botão direito no ar sai na hora (a altura mínima continua), o dano da garra que ainda não saiu não sai, os cliques guardados são esquecidos e o clipe da garra some. Teste novo: `BoteCortaGarra`.
  - **Virar lobo (3) e voltar a humano não rugem mais** ("precisa tirar o som ou trocar o rugido do lobo ao apertar 3 e sair"): tocam um "puff" de fumaça (`forma_puff_1..3`, novo, feito por código em `tools/sons/choque_puff.py`; depois do Jhon: "ao transformar não tô ouvindo o puff"). Os rugidos de antes ficam no disco.
  - **O rugido: só a silhueta azul e os raios, só o som dos choques** ("deixando apenas a silhueta azul e os raios; somente o barulho dos choques, sem o estalo no final"). Saíram os fachos de luz do alto, as fagulhas, a poça de luz nos pés, a onda no chão, o ar da boca, o anel do escudo cheio e a luz que clareava o lobo (`RoarBurst` ficou só com os arcos); a silhueta continua acendendo com a carga. Do som, ficam a voz do rugido (`lobo_rugido`) e os choques: `lobo_uivo_choque_1..2` (novo, `tools/sons/choque_puff.py`), só estalos e arcos secos, sem o grave e a bobina que subiam (Jhon: "o barulho do choque deve ser apenas do choque, sem esse som subindo, zuuuum; só choque e rugido"). O estalo do escudo cheio (`lobo_uivo_escudo`) saiu.
  - Conferido no jogo: a granada maior na primeira pessoa (parada, armando com o dedo na argola, armada e lançando) e na terceira pessoa (vitrine: o saque do bolso, o dedo na argola, o pino indo na mão e caindo até o chão); a pistola na primeira pessoa, parada e mirando. Não conferidos no jogo: o rugido e a troca de forma sem som (só pelos testes) e o Barut.

- **O tiro ganhou traçante de verdade (23/11)** (Jhon: "deixar o tiro que sai do cano da arma mais elaborado? atualmente é só uma linha branca"). A linha fina que aparecia inteira por 0,08 s virou `BulletTracer` (`scripts/visual/fx/`): a bala é um risco quente de 3,2 m que viaja do cano até onde o tiro parou a 190 m/s (mais devagar que uma bala de verdade para dar para ver: 30 m em 9 quadros), com a ponta mais grossa e quase branca e o clarão na cor do traçante; atrás dela fica o rastro do ar quente, fino e fraco, que cada trecho começa a apagar quando a bala passa por ele, alarga, sobe um pouco e ondula enquanto some (0,45 s); e no primeiro instante, uma estrela de luz na boca do cano (só visto de fora: a arma da primeira pessoa já tem o clarão dela). Tudo em fitas de luz de frente para a câmera, com o material que os efeitos do lobo já usam (um material novo por disparo travava o quadro). Perto do olho as fitas não passam de uma largura proporcional à distância, para o tiro em primeira pessoa não cobrir a tela. A cor ficou mais quente (`TracerColor`). No servidor sem tela não se monta. Só visual: o acerto continua sendo o raio no instante do disparo. Visto no campo de treino com um boneco atirando (de lado e por trás do ombro); não testado numa partida nem em primeira pessoa com as mãos.

- **Lobo correndo na parede online: no jogo exportado o clipe da parede não carregava** (Jhon, 22/11: "no meu godot funciona, mas quando é em prod não"; o log do jogo instalado: `Failed loading resource: res://assets/characters/wolf/wallrun.fbx`). O `wallrun.fbx` foi baixado com a pele, e o Godot extraía as duas texturas dele (`wallrun_0.png`, `wallrun_1.png`) — que o `.gitignore` deixa fora do git. Na máquina de quem desenvolve elas existem e foram importadas; no build do lançamento (checkout limpo) não, e o `.fbx` inteiro falhava: sem o clipe, quem via de fora via o lobo na pose do pulo (no repasse e no autoritativo). Agora o `wallrun.fbx` e o `crouch_walk.fbx` (o mesmo caso, do humano) não extraem as texturas (`embedded_image_handling=0`, como o `jump.fbx` já fazia): só a animação é usada. Conferido sem os `.png`: o clipe carrega e `ParedeOnline` e `ParedeRepasse` passam. Teste novo `ParedeRepasse` (o boneco pelo estado do repasse) e log das trocas de parede no jogo (`wall net`, `wall view`) e no servidor (`wall server`, com o motivo quando não volta para a parede). Precisa de um lançamento novo (o jogo); o servidor não muda.

- **Terceira pessoa: os dedos encaixam na arma pela malha da luva, a palma encosta e a mão não escorrega mais nela — testado no jogo só parado e na mira (23/11)** (Jhon, 22/11: "os dedos continuam tortos, o rig é diferente do personagem e da mão da primeira pessoa"; "os dedos precisam ter o mesmo resultado, mesmo sendo maiores ou menores"; "nada pode atravessar outros materiais"; "da faca pode grudar mais, faça da granada também"). O que se achou medindo no Blender: o esqueleto da mão do personagem não é o da primeira pessoa (lá, metacarpo e três falanges de 4,2 / 2,6 / 2,2 cm no indicador; no Barut, três falanges e a ponta, de 4,0 / 2,9 / 4,6 cm — a última duas vezes mais comprida — e o dedo com 12,7 mm de raio; a mão dele tem 25,8 cm contra 19,9 de lá, e o braço é 9% mais curto), e copiar a direção de cada falange de lá dava o dedo torto. E a mão escorregava na arma: a cada quadro o script girava a mão até 30° em volta do cabo para aliviar o punho, quando na primeira pessoa os dedos **não se mexem** na arma em nenhum clipe fora a recarga e o gatilho. Tudo em `tools/personagens/blender/animacoes_armas.py`:
  - **Encaixe dos dedos** (`Dedos._encaixar`): cada dedo parte das direções de lá (giradas junto com a mão do personagem) e procura a dobra das três juntas em que a pele dele — os pontos da malha da luva de cada falange (`PELE_PONTOS = 32`, o dobro na ponta), levados pelos pesos do osso de trás **e, em cima de cada junta, também pelos da falange seguinte** (`do_pai`) — não entra na arma, a falange que lá encosta na arma encosta aqui, e o resto fica o mais perto de lá que der. Entrar na arma se vê por raio do osso até a pele (`Conjunto.corta`). Sem a pele da junta levada pelas duas falanges, o nó do polegar do Barut entrava 1 cm na caixa do SCAR sem a conta ver; com 14 pontos por falange passavam 3 a 5 mm.
  - **O polegar vai de lado no movimento que a dobra não faz** (o eixo era "para a arma mais perto": quando ela ficava no plano da dobra os dois eixos eram o mesmo e o polegar atravessava a caixa do SCAR e do AK-74 por baixo, 2 a 3 cm).
  - **A palma** (`afastar_palma`, `pele_palma`): pela malha da mão; sai da arma ponto por ponto. **A mão de apoio agora desce até a palma encostar no guarda-mão** (`apoio`): na primeira pessoa o meio da palma fica a 2-3 cm do fuzil (ele assenta de través na raiz dos dedos), e aqui a palma inteira ficava de 0,7 a 1,9 cm abaixo dele, a arma segura com a ponta dos dedos. Fica a 1,5 mm.
  - **Na mira a mão segura como no quadril** (`PERTO_DO_QUADRIL`): o jeito de pegar da mira era procurado do zero e saía até 20° diferente (o dedo médio do Barut atravessava o cabo do AR-15, 9 mm; a palma entrava 12 mm no SCAR). Agora só sai do jeito do quadril se o braço não der.
  - **A pegada de base** (`Dedos.fixar`): o encaixe é feito uma vez por arma e os dedos ficam colados nele, em relação à arma, em todos os clipes em que os de lá não se mexem; a da mira começa de onde a do quadril parou. O giro da mão em volta do cabo caiu de ±30° para ±8° (`GIRA_NA_ARMA`).
  - **Faca e granada: o punho fechado preso na arma** (`_punho_preso`, `Conjunto.firme`): a mão assenta uma vez (na faca, logo atrás da guarda, o cabo na raiz dos dedos) e fica presa ali em todos os quadros de todos os clipes, os dedos colados. Na primeira pessoa a lâmina anda até 1,7 cm dentro da mão nos golpes e no saque; aqui a mão vai presa na própria faca. Se o braço não chega, é a arma que vem até a mão.
  - **A média dos dedos no tempo não entra na arma** (`suavizar_dedos`): onde a média entra mais que o quadro como foi resolvido, vale o resolvido (na recarga e no mirar a média puxava a ponta do dedo até 1,5 cm para dentro).
  - **O indicador que não cabe no guarda-mato** (o do Barut é mais grosso que o vão) fica estendido no corpo da arma, fora do gatilho.
  - **A arma só cresce no que se segura com uma mão**: pistola, faca e granada crescem com a mão do personagem (Barut 1,30; Eve 1,00); o fuzil fica 1,00 nos dois. O `.json` leva `"escala"` e o jogo aplica (`HoldClipSet.Scale`, `HeldWeapon.HoldByClips`). Para desligar: `ESCALA_MAX = 1.0`.
  - **O tronco vira mais com o fuzil quando a mão de apoio não chega** (`GIRO_A_MAIS`: até 18° além dos 22°). No Barut custa folga entre a coronha e o ombro na mira: 6,5 a 7,8 cm (era 3,8 a 5,2); na Eve, 0 a 0,7 cm.
  - **Conferido com a malha deformada pelo Blender**, à parte da conta do script (o maior tanto que a arma entra na pele, a cada 3 quadros, 74 clipes por personagem). **Segurando a arma** (parado, andar, tiro, mira, mira andando e atirando): Eve — 0 em todas, fora 0,6 mm no SCAR e 0,4 na Deagle; Barut — AK-74 0,1, AR-15 1,6, Deagle 1,7, M4A4 3,2 e SVD 3,3 (o dedo médio da mão do gatilho, na mira), SCAR 3,8 (a ponta do indicador da mão de apoio), faca e granada 0. A palma não entra em nenhuma. Os dedos não tremem (0,1° por quadro; até 7° no tiro, do gatilho) e todos os laços emendam (0 a 0,4°). **Faca: 0 mm e 0,1° por quadro em todos os clipes, golpes e saque incluídos.** **Nas transições ainda entra em alguns quadros**: recarga até 9,0 mm no SCAR do Barut (5 de 30 quadros acima de 3 mm) e a palma da mão de apoio 12 mm num trecho da recarga da SVD do Barut e do AK-74 da Eve; mirar/desmirar até 6 mm num quadro (Barut); sacar até 6 mm (SVD do Barut, AK-74 da Eve); o arremesso da granada 3,3 mm num quadro (Eve). E na recarga e no saque um dedo ainda pula de 60° a 95° de um quadro para o outro quando a mão chega na arma ou sai dela (onde a média foi desfeita para não entrar). Na mira da SVD a palma da mão de apoio do Barut ainda fica a 1,6 cm do guarda-mão.
  - **23/11 — rodada no Mac, com o jogo** (Jhon: "dedos em leque na mão de apoio", "o polegar da Eve na granada de joinha"). Antes de mexer: `dotnet build` passou, `BonecoMaos` passou (38 verificações) e o boneco foi fotografado dentro do jogo (o corpo que os outros veem, de perto de cada mão) — o jogo mostra o que o Blender mostra. O que mudou no `animacoes_armas.py`:
    - **A mão de apoio não gira mais deitada no guarda-mão** (`GUINA_APOIO`, de ±50° para 0). A procura girava a mão 20° a 30° em todo fuzil para aliviar o punho; a fileira dos nós ficava na diagonal do cano, o nó do indicador embaixo do guarda-mão (o dedo saía reto para a frente) e o do mínimo a 3 cm da arma, no ar: o leque. Sem o giro os quatro dedos sobem juntos pelo lado, como lá. Custa punho: dobra de lado 20° a 33° (era 0° a 16°); o da primeira pessoa dobra 15° a 22° nessa mão.
    - **Cada dedo dobra o tanto a mais que o vizinho dobrou** (`PESO_JUNTO`): o que não alcança a arma acompanha os outros em vez de ficar esticado.
    - **O polegar do punho fechado fecha por cima dos dedos** (`PESO_FECHA`): o da Eve achava a espoleta da granada e ficava em pé nela.
    - **O dedo colado na pegada de base que entra mais de 1,5 mm na arma é encaixado de novo, solto** (a mão na mira fica uns graus mais girada no cabo que no quadril).
    - **Medido na malha deformada** (validador refeito, 74 clipes por personagem, a cada 3 quadros). Segurando a arma, o maior tanto que ela entra num dedo — Barut: AK-74 0,0 (era 0,1), AR-15 0,3 (1,6), M4A4 **4,5** (3,2: piorou; o dedo médio direito na mira andando e atirando — a conta do script não vê, só a malha inteira), SCAR 0,8 (3,8), SVD 0,1 (3,3), Deagle 0,0 (1,7), faca e granada 0. Eve: 0 em tudo, fora 0,4 na Deagle. A palma não entra em nenhuma. Transições, o pior de cada: recarga do SCAR do Barut 4,0 (era 9,0), do AR-15 2,2 (5,0), do AK-74 da Eve 2,3 (5,7); mirar do AK-74 e do M4A4 do Barut 6,1 e 6,3 num quadro; a palma de apoio na recarga da SVD do Barut 20 mm e do AK-74 da Eve 16 mm (não mexido).
    - **Não feito nesta rodada:** os pulos de dedo nas transições (ainda 40° a 97° num quadro na recarga e no saque), a coronha do Barut longe do ombro na mira (6,4 a 8,3 cm) e a palma de apoio dele na SVD. (A mão de apoio aberta na pistola e o indicador fora do guarda-mato, nos dois personagens, foram tratados na segunda rodada, abaixo.)
    - **Segunda rodada, 23/11 — o indicador no gatilho e a pistola** (Jhon: "não ficou bom, quero que encaixe perfeitamente os dedos no gatilho e perfeitamente segurando cada arma"). Medido antes: o dedo cabe — o vão do guarda-mato tem 26 a 37 mm de altura e a ponta do indicador 15 mm (Eve) e 21 mm (Barut). Ele ficava fora porque só o AR-15 tem o gatilho como peça à parte (nas outras armas o script não sabia que aquele era o dedo do gatilho) e porque um plano B o tirava de lá quando a procura não achava lugar.
      - **`Dedos._no_gatilho`**: o indicador da mão direita, em fuzil e pistola, é posto no gatilho por conta própria, sem copiar os ângulos de lá (o esqueleto de lá tem metacarpo e três falanges com outros nomes; o daqui, três falanges e a ponta, com a última até duas vezes mais comprida): a polpa da ponta do dedo (o meio da última falange, do lado da palma) vai no ponto do gatilho em que o dedo de lá encosta; a base abre até 18° para o lado, as juntas dobram dentro do que uma mão dobra (base −12° a 90°, meio 0° a 105°, ponta −5° a 80°), a da ponta perto de dois terços da do meio, e nada atravessa o guarda-mato. O dedo fica colado ali em todos os clipes (não acompanha o aperto do gatilho de lá). O plano B (`INDICADOR_FORA`) ficou desligado. A polpa ficou a: AK-74 1,0/0,7 mm (Barut/Eve), M4A4 1,2/3,3, SCAR 1,2/0,6, SVD 0,6/0,3, Deagle 3,3/0,6 e **AR-15 8,1/4,7** (o pior: a base do dedo já bate nos 18° de lado).
      - **A mão do gatilho gira no máximo ±10° em volta do cabo** (`GIRO_CABO`, era ±50): na pistola a procura girava 25° a 30° para aliviar o punho, a mão saía de trás do cabo e o indicador ficava a 12-21 mm do gatilho. O punho continua em até ~15° de dobra e de lado.
      - **A mão de apoio da pistola encosta** (`afastar_da_outra`): a palma dela sai dos dedos da mão do gatilho, se entrar, e vem pela normal até tocar neles ou no cabo. Ficava onde a de lá fica, a 2-3 cm (a mão daqui é mais grossa e tem outra forma), com os dedos fechando no ar. As falanges dela estavam a 4-8 cm da arma; ficaram a 1-5 cm (as de lá: 1,3 a 5,9). No jogo a palma esquerda do Barut fica a 3,2 cm da de lá (o teste aceita 4).
      - **Medido de novo** (segurando, mm que a arma entra num dedo) — Barut: AK-74 0,0, AR-15 0,3, M4A4 4,5 (igual: o médio direito na mira andando), SCAR 0,8, SVD 1,5 (o indicador, na mira andando), Deagle 2,1 (o indicador, na mira); Eve: 0 em AK-74, AR-15, SCAR e SVD, M4A4 1,4, **Deagle 4,3 (o polegar direito, parado: piorou)**. Faca e granada 0. A palma não entra em nenhuma.
      - **O que ainda não está natural:** o polegar da mão de apoio na pistola do Barut fica em pé atrás do ferrolho; no Barut o fuzil continua pequeno na mão (mão 1,3× a de lá, fuzil 1,0×) e a ponta dos dedos passa do guarda-mão; os pulos de dedo nas transições e a coronha longe do ombro do Barut continuam como estavam.
    - **Terceira rodada, 23/11 — a mão da Eve com as proporções do Manny (só Eve, só AR-15; prova levada ao jogo)** (Jhon: "readaptarmos a eve e barut, braços, ombros, mãos e dedos para se comportarem exatamente como a do Manny"). Script novo `tools/personagens/blender/mao_manny.py` (usa `validar_maos.py`): põe cada junta de cada dedo da luva da Eve onde a do Manny fica (alinhada pela palma; a grossura é a da luva) e **remenda o `humano.glb` direto no binário** — só mudam os vértices e normais que dedo leva, as matrizes de ligação e a translação local dos nós dos dedos (40 ossos); os nomes e as rotações de repouso de todos os ossos ficam iguais (passar pelo exportador do Blender giraria os ossos do corpo e quebraria os clipes do Mixamo). O `ar15.json` da Eve passa a vir da mão copiando a de lá osso por osso. A mão de lá que segura a arma fecha a palma (os metacarpos dela andam até 25 mm) e a Eve não tem metacarpos: as falanges são acertadas por mínimos quadrados para a junta do meio, a da ponta e a ponta caírem onde as de lá caem (indicador, médio e anelar até 1,5 e 3,5 mm; o polegar até 8 mm) e a base dos dedos que entram na arma abre o mínimo (polegar 10°, mínimo −12°). O `.blend` de ajuste (`tools/personagens/blender/poses/eve_ar15_manny.blend`, fora do git) traz uma ação por clipe e as caixas de controle; `mao_manny.py eve ar15 --do-blend` regrava o `.json` a partir das ações. Cópias de antes em `.synctmp/terceira_pessoa_2311/` (`eve_antes/`, `humano_eve_antes_manny.glb`). **Efeito colateral:** as outras armas da Eve ainda usam os `.json` de antes, feitos para o repouso antigo da mão — na Deagle, na faca e na granada os dedos ficaram fora do lugar até serem refeitos pelo mesmo caminho. Passam `BonecoMaos`, `Personagens` e `CabecaNoLugar`.
    - **Quarta rodada, 23/11 — ossos do Manny na pose T (Eve e Barut), ainda fora do jogo** (Jhon: "1 - ossos do manny na eve; 2 - aproximar as raízes dos dedos; 3 - deixar tamanhos dos dedos e da mão iguais a do manny"). `tools/personagens/blender/mao_manny_tpose.py <p>` parte do `humano.glb` (o do git, copiado para `.synctmp/terceira_pessoa_2311/humano_<p>_antes_manny.glb`; o Blender importa e exporta ele sem mexer em nenhum osso do corpo — conferido nó a nó): os dedos ganham os nomes do Manny (`index_01_r`…), entram os metacarpos (`index_metacarpal_r`… entre a mão e cada dedo, com pesos tirados da pele da palma), as juntas vão onde as do Manny ficam (pulso ancorado), com a grossura dele, e a palma ganha o comprimento e a largura da dele (Eve 0,97×; Barut 0,68×). Grava `poses/<p>_tpose_manny.blend` (fora do git) e `.synctmp/terceira_pessoa_2311/tpose/<p>_humano_manny_tpose.glb`; só com `--aplicar` troca o `humano.glb` do jogo. O jogo ainda usa os nomes antigos (`HoldClips`, `HeldWeapon`, `HeroPose`, `BonecoMaos`).
    - **Quinta rodada, 23/11 — a Eve no jogo com os ossos do Manny (fuzis e pistola; sem gerador)** (Jhon: "aprovado"). `humano.glb` da Eve trocado pelo da pose T (a cópia de antes: `.synctmp/terceira_pessoa_2311/humano_eve_antes_manny.glb`; o do git também serve). `tools/personagens/blender/mao_manny_anim.py eve <arma>` gera o `.json` sem procura de encaixe, sem alívio e sem mínimos quadrados: cada osso de dedo e de metacarpo recebe, a cada quadro, a rotação **e a posição** do osso de mesmo nome da primeira pessoa — os eixos dos ossos daqui são exatamente os de lá (conferido: 0,00° de diferença nos 19 ossos de cada mão) e na primeira pessoa a palma fecha deslizando os nós (até 2,4 cm), então o arquivo ganhou `"posicoes"` e, por clipe, `"pos"` (posição local em 0,1 mm). Só o braço (IK até o pulso) e a postura do tronco são os de antes. Medido no Blender: todas as juntas de todos os dedos a no máximo 0,9 mm das do Manny, em todos os clipes, e a mão a 0,0 mm do lugar (AR-15, AK-74, M4A4, SCAR, SVD, Deagle). No jogo: `BonecoMaos` com mão direita e esquerda a 0,0 cm do conjunto (parado, mira, pistola) e 0,7 cm na recarga (eram 0,6 a 1,2). Código: `HoldClips` lê as posições, `HeldWeapon` as toca (e restaura) e aceita os nomes dos dedos do Manny (`index_01_r`…) além dos do Mixamo; `HeroPose` e o teste também. **O Barut não foi tocado** (`humano.glb` e `.json` de antes). **A pistola parada da Eve estica os braços para a frente** (Jhon: "a pistola deve esticar o braço da eve até na frente"): `Plano._esticar_pistola` põe o cabo o mais longe do peito que as duas mãos alcançam (93% do braço, `PISTOLA_ESTICADO`), um pouco acima do desenho de antes (`PISTOLA_ALTURA`); a Deagle da Eve foi regerada. **O ombro da Eve não torce mais** (Jhon: "imagem 1 torcida, imagem 2 como deveria ser o ombro e o braço"): o braço de cima chegava a girar 128° (parado) e 168° (mira) em volta de si em relação à pose T, com a pele do ombro dobrada. O cotovelo agora escolhe, entre os lugares que deixam a mão onde está, o que gira menos o braço de cima (`Braco.peso_torcao`, `torcao_do_ombro`; 6 por grau no `mao_manny_anim.py`, 0 no `animacoes_armas.py`). Com 1 por grau o ombro esquerdo dos fuzis ainda ficava com 71° a 77° contra 7° a 23° do direito (Jhon: "o ombro esquerdo está um pouco torcido em relação ao direito"); com 6, parado, andando e na mira os dois ficam entre 0° e 16° em todas as armas, e o cotovelo de apoio sai um pouco para o lado. Fica alto onde o braço vai acima do ombro: a granada armada e o lance (70° a 83°, o braço atrás da orelha) e alguns quadros de saque e recarga (até 44°; na pistola, a esquerda até 67° no saque). **No lance da granada o braço esquerdo desce junto com o direito** (Jhon: "o braço oposto da granada… deve ficar no mesmo lugar"; depois "o braço esquerdo deve abaixar ao lançar, de forma sincronizada"; `BRACO_E_FIXO_NO_LANCE`): sai de onde estava com a granada armada, apontando o alvo, e desce girando no ombro, esticado ("deve ir descendo esticando até ficar numa pose natural"), até ficar solto ao lado do corpo no fim do lance do braço da granada (0,30 s); o braço desce esticado e só nos últimos 40% o cotovelo relaxa na pose solta de antes (Jhon: "a dobra anterior estava boa… apenas alinhe a mão à direção do antebraço"; uma versão com o braço pendurado e esticado ao lado da coxa foi desfeita). **As duas mãos da granada vão na linha do antebraço em todos os clipes** (o punho a até 6°, `PUNHO_ALINHADO`; "o braço da granada terá o mesmo princípio"), a granada junto com a palma; só no lance, o punho direito saltava na passagem da granada armada para o arremesso. Solta, a mão fica em concha (a pose de repouso da mão do Manny; "mãos abertas em concha") em vez de copiar a de lá. Antes puxava para o lado do quadril, e na primeira versão a mão descia em linha reta e o cotovelo dobrava no caminho. **E o braço da granada não torce no fim do lance** ("braço de lançamento fica torto"): cruzava até o quadril esquerdo com a palma para fora; agora termina na frente da barriga com a palma para o corpo (`SEGUE_GRANADA`, `MAO_SEGUE_GRANADA`). **O punho da pistola reto** (Jhon: "pulso um pouco torto para baixo"; "as mãos estão encostando umas nas outras, uma apertando a outra"): na pegada da pistola a palma fica de lado, e o "para baixo" é o desvio lateral do punho, que estava 23° contra 7° na primeira pessoa (o braço daqui sai do ombro dela, mais baixo que o olho). A pistola agora gira e desce inteira, com as duas mãos presas nela (`giro_da_pistola`, um giro por postura, misturado nas transições): parada, 7° de inclinação e 9 cm mais baixa, o punho direito a 8° do de lá; na mira o cano fica na linha (até 3°, `LIMITE_GIRO_MIRA`) e os ombros sobem 26° (`OMBRO_MIRA_PISTOLA`): o desvio vai a 9°. Girar cada mão sozinha enfiava a esquerda 1,5 cm na direita; com a arma girando inteira a esquerda abraça a direita como antes (as peles se tocam, 3 a 4 mm, a pegada de duas mãos da primeira pessoa). O punho ainda dobra para dentro (24° contra −6°), dos braços que se fecham na frente do peito; e na mira a pistola fica uns 9 cm abaixo do olho. **Depois do lance os dedos relaxam, e a arma vem de trás** (Jhon: "ao lançar a granada, os dedos voltam para o estado de repouso, mesmo da mão oposta"; "e já puxa de trás a arma"): a mão que lançou solta a granada (0,11 s) e em 0,12 s os dedos vão para a concha de repouso, como os da outra. E o saque do fuzil e da pistola, visto de fora, vem de trás (`sacar_de_tras`): na primeira pessoa a arma sobe de baixo da câmera, e de fora ela aparecia do nada na frente do corpo. A arma começa pendurada na mão direita, com o braço solto e esticado ao lado da coxa (99,5% do alcance, `SACAR_ESTICA`; o ombro 8° mais baixo, `SACAR_OMBRO_DESCE`), e sobe até a pegada presa nela; a esquerda fica pendurada do outro lado, em concha, e entra na arma na segunda metade; a cabeça olha de leve para a mão que saca (40% do caminho, no máximo 20°, voltando para a mira no fim; `olhar_a_arma`, Jhon: "ao sacar a arma, o personagem deve olhar levemente para a direção em que a arma está sendo sacada"). Girar a arma em volta do ombro (a primeira versão) torcia o braço (Jhon: "braço muito torcido"; 72° a 107° nos fuzis): somava ao giro do saque da primeira pessoa, e agora a pegada é a parada dela, congelada. Três coisas acertadas medindo: (1) o alcance (`aproximar`) puxava o fuzil para a mão esquerda, que no começo do saque está solta, e o braço direito dobrava com o cotovelo para fora — agora a mão solta não puxa a arma; (2) o cotovelo do braço pendurado preferia "para baixo" (sem sentido com o braço já para baixo) e o braço torcia 50° — no saque ele prefere para trás e a torção livre cai a 5°: 0° a 7° no começo dos dois braços, até 28° no meio (fuzis) e o fim é a pegada de antes; (3) a arma entrava no corpo — com a mão colada na coxa a coronha do fuzil entrava 75 mm no quadril, e a pistola girada em volta do ombro 77 mm na coxa; com o braço afastado do corpo (`SACAR_BRACO_FUZIL`, `SACAR_BRACO_PISTOLA`) e o cano para baixo e para a frente (`SACAR_CANO_*`), 0 mm em todos os quadros (medido contra a malha deformada do tronco, do quadril e das pernas, `MEDIR_CORPO=sacar`; o fim do fuzil fica nos mesmos 10 a 13 mm da coronha no ombro da pose parada). O jogo já voltava sozinho para a arma depois do arremesso e tocava o saque. **Cotovelos para baixo** (Jhon: "os braços precisam ter o cotovelo apontando para baixo, não para os lados"; "faça até o máximo que fique sem torcer"): nos fuzis e na pistola os dois cotovelos preferem apontar para baixo (`COTOVELO_BAIXO`, `PESO_COTOVELO` 1,5 por grau), e a torção do braço de cima só custa acima de 50° (`TORCAO_LIVRE`: a 71° o ombro esquerdo já aparecia torcido; 35° e 50° conferidos de perto no jogo, os dois lisos, e 50° deixa o cotovelo mais para baixo). Parado e na mira, o cotovelo fica de 12° a 52° do "para baixo" nos fuzis (era de lado), com a torção do ombro em até 53°; na pistola mirando o esquerdo segue a 74°, com o braço quase esticado. Os braços continuam esticados como estavam. **Faca e granada da Eve no caminho novo**: a coreografia do corpo inteiro (o golpe ampliado, o tronco girando, a guarda; o lance da granada) é a do `animacoes_armas.py`, e a mão que segura copia a da primeira pessoa osso por osso (a mão a 0,0 mm, as juntas a 0,9 mm); a esquerda vai para a guarda (faca) e para o pino e o alvo (granada) com os dedos de lá. Um primeiro conserto (o braço rolar 25°, `ROLO_BRACO`) torcia o cotovelo e ficou desligado. A suíte inteira: as mesmas 13 falhas de antes, nenhuma nova.
    - **Sexta rodada, 23/11 — o lance da granada da Eve refeito** (Jhon: "refaça, não está natural… só precisa visualmente fazer o mesmo movimento de puxar o braço para trás e lançar"; "use somente o braço, ombro, pulso e mãos de lançamento como referência"; "a Eve ainda deve puxar o pino com o outro braço"). O braço que lança (clavícula, braço, cotovelo, punho e mão) segue um arremesso do Mixamo ("Grenade Throw"): `tools/personagens/blender/lance_extrair.py` tira dele só o braço direito, quadro a quadro, no referencial do peito e em comprimentos de braço (`lance_referencia.json`; o tronco que gira, as pernas que andam e o outro braço do clipe ficam de fora, e o .fbx não entra no repositório). `Copia.lance` (`mao_manny_anim.py`) monta o resto preso no tempo desse braço: **armar** — a granada vem para a frente do peito, a esquerda puxa o pino (a mão da primeira pessoa), e então o braço desce, vai para trás e sobe engatilhado atrás da cabeça enquanto o tronco gira o ombro direito para trás (38°) e a esquerda, aberta, aponta o alvo; **armado** — essa pose; **arremesso** — o tronco gira para a frente antes do braço (chega no giro do lance em 0,15 s, passa do alvo 30° e inclina 22°: o tronco puxa, o braço chicoteia), o braço passa por cima, solta em 0,11 s (quando a granada sai da mão no jogo), acompanha para a frente e para baixo e volta; a esquerda desce esticada junto; os dedos da mão que lançou relaxam depois da soltura; no fim os dois braços ficam soltos ao lado das coxas, a pose em que o saque da arma começa (antes terminavam na frente do corpo e o saque saltava). O punho é o da referência (engatilhado dobra 34° para trás, depois da soltura estala 28° para a frente), e o cotovelo vai onde o dela vai. A medida de torção do ombro dá 130° engatilhado e 138° no lance: é a rotação externa do ombro de quem arremessa, e a pele do ombro, vista de perto, fica lisa. A cabeça continua olhando o alvo. Faca, pistola e fuzis não mudaram; parado, andar e saque da granada também não. Não conferido no jogo, só no Blender; `Granada`, `GranadaNaCruz`, `GranadaNaMira`, `BotsJogamGranada`, `BonecoMaos` e `SacaEGuarda` passam (`GranadaFere` já falhava). O `.json` de antes: `.synctmp/terceira_pessoa_2311/eve_antes_lance_mixamo/`.
    - **Sétima rodada, 23/11 — a faca da Eve desenhada** (Jhon: "corrija esses pontos"; "e como ela saca a faca?"). Antes, `animacoes_armas.coreografia` ampliava 35% o caminho da mão da primeira pessoa: os cortes (de revés) abriam o braço reto para o lado e o corte sumia em 2 quadros, a estocada recuava com a lâmina para cima ao lado do rosto, o tronco só girava, a esquerda ficava parada de punho fechado e a guarda tinha a lâmina atravessada na cintura. Agora `Copia.faca` (`mao_manny_anim.py`, `FACA_GOLPES`): cada golpe passa por pontos-chave da mão (curva suave), com a orientação da mão de lá nos extremos (a pegada é a dela) e os tempos de lá (a lâmina no extremo no quadro 4, 0,133 s; de volta à guarda no quadro 11, 0,367 s). **Estocada** e **estocada alta**: recolhe a mão para o lado da cintura (ou do peito) com a ponta para a frente e empurra reto; o tronco gira o ombro direito para a frente (18°/15°) e inclina com o peso (10°/5°); a esquerda recua para o peito. **Corte** e **corte deitado** (de revés, como na primeira pessoa): a mão vai para a frente do peito esquerdo com o cotovelo dobrado e a lâmina para fora, o tronco gira 18° para a esquerda, corta atravessando e termina à direita (o tronco gira 18° para a direita e inclina); a esquerda sai do caminho para o lado da costela e volta. **Guarda** (parado e andar, com o balanço de lá): a mão na frente do peito com a lâmina para a frente e para cima (girada 55° da de lá); a esquerda aberta, mais alta, protegendo o rosto. **Saque**: a mão vai à bainha no quadril direito, puxa a faca para cima com a lâmina para baixo, gira a lâmina no caminho até a guarda; a esquerda sobe da pose solta; a cabeça olha de leve para a mão. Medido: a faca não entra no corpo em nenhum quadro (0 mm, tronco, quadril e pernas) e a lâmina fica a pelo menos 34 cm do centro da cabeça; o braço torce até 52° nos golpes. No jogo a primeira pessoa pula a armada do golpe (`RigSwingLead`, a lâmina chega 0,07 s depois do clique) e a terceira toca o clipe inteiro (chega 0,13 s depois): não mexido (`HeldRig.cs`). Não conferido no jogo; `BonecoMaos`, `BracosNaMao`, `BonecoMarcas`, `ArmasEpicasELendarias`, `SacaEGuarda` e `Personagens` passam. O `.json` de antes: `.synctmp/terceira_pessoa_2311/eve_antes_faca_desenhada/`.
    - **Oitava rodada, 23/11 — os braços fora do tronco** (Jhon: "o cotovelo da faca está para dentro"). Medida nova no `mao_manny_anim.py` (`MEDIR_BRACO=clipe,...`): quanto a pele do braço, do meio dele para baixo, e do antebraço entra no tronco (malha deformada; perto do ombro a axila sempre encosta e fica de fora). **Faca**: na guarda o cotovelo direito ficava 10 cm dentro do colete — a mão tinha sido girada 55° em volta da vertical para a lâmina apontar para a frente, e nessa pegada (a lâmina sai de lado dos dedos) isso punha os dedos para a direita e o antebraço vindo de dentro do corpo; agora a mão gira só em volta do antebraço, até a lâmina ficar de pé e um pouco para a frente (`FACA_GUARDA_LAMINA`): 9 mm. Os cortes recuavam a mão até o ombro esquerdo e o braço de cima afundava 6 cm no peito: o recuo agora é na frente do peito, no meio (`FACA_RECUO2`, `FACA_RECUO3`): 0 a 2 mm. No saque o antebraço entrava 4 cm no quadril: a bainha um pouco mais para fora (`FACA_BAINHA`). **A mão esquerda solta** (o começo do saque de todas as armas e o fim da granada) entrava 2,4 cm na coxa: o braço vai um pouco mais aberto (`SACAR_BRACO_E`): 0. **Granada**: no armar, depois do pino a esquerda seguia a mão de lá, presa no conjunto da granada, que vai junto com a direita engatilhando e a arrastava 8 cm para dentro do colete — agora, solto o pino, ela se solta do conjunto e vai em arco pela frente até apontar o alvo (`LANCE_ARCO_E`): 0; no lance a esquerda descia raspando a lateral (4 cm): desce um pouco aberta (`LANCE_ABRE_E`): até 7 mm; logo depois da soltura o braço de cima direito passava na frente do rosto e atravessava a gola alta do colete (69 mm) — a mão e o cotovelo passam um pouco por fora (`LANCE_FORA`), e o tronco agora também inclina de lado (6° para a direita engatilhado, 16° para a esquerda no lance, `TRONCO_LADO`; sem isso o braço passava em pé acima da cabeça): até 15 mm. **O que fica**: a braçadeira afunda no próprio deltoide quando o braço sobe (até 46 mm com o braço engatilhado atrás da cabeça, 13 a 25 mm nas outras poses, até na parada) — é a pele do ombro do modelo que incha, não o movimento; e a coronha dos fuzis parados fica 10 a 30 mm dentro do peito (AK-74 30 mm; o fim do saque é a mesma pose) — da pose parada de antes, não mexida. As 8 armas da Eve regeradas; `Granada`, `GranadaNaCruz`, `GranadaNaMira`, `BotsJogamGranada`, `BonecoMaos`, `BracosNaMao`, `SacaEGuarda` e `Personagens` passam (`GranadaFere` já falhava). Os `.json` de antes: `.synctmp/terceira_pessoa_2311/eve_antes_bracos_fora_do_tronco/`.
    - **Nona rodada, 23/11 — osso de torção no braço da Eve, e a vitrine** (Jhon: "braço da granada torcendo aqui"; "crie uma scene com várias Eves ao lado realizando as animações"). Engatilhada, a granada gira o braço de cima 130° em volta de si, e sem osso de torção esse giro inteiro aparecia de uma vez na pele, logo abaixo do ombro, em espiral. Mudar a pose não resolvia (com a mão à frente da orelha a torção só caía a 90° e o punho dobrava 70° a 84°). `tools/personagens/blender/torcao_braco.py eve --aplicar` põe um osso de torção no braço de cima de cada lado (`mixamorig:RightArmTwist`, `mixamorig:LeftArmTwist`: no ombro, com os eixos do braço, filho dele) e divide a pele que o braço levava — toda no osso de torção perto do ombro, nada perto do cotovelo (776 vértices). O `mao_manny_anim.py` dá a ele, a cada quadro, o giro contrário ao do braço em volta do próprio eixo, 60% dele (`TORCAO_PARTE`: com 100% a pele afinava na braçadeira, metade num osso e metade no outro a 130°): o ombro e o braço de cima ficam lisos. O `.json` ganhou os ossos `RightArmTwist` e `LeftArmTwist` (o jogo já aplica os ossos pelo nome, e devolve o repouso quando a arma sai da mão); sem animação neles (andar, lobby) o braço deforma como antes. As 8 armas da Eve regeradas. O `humano.glb` de antes: `.synctmp/terceira_pessoa_2311/humano_eve_antes_torcao.glb`; os `.json`: `.synctmp/terceira_pessoa_2311/eve_antes_torcao_jsons/`. **Vitrine** (descartável): `scenes/debug/vitrine_eve.tscn` (`scripts/debug/VitrineEve.cs`) — o campo de treino com uma Eve por arma em fila (AK-74, AR-15, M4A4, SCAR-H, SVD, Desert Eagle, faca, granada), viradas 60° para o lado, bonecos de rede tocados como numa partida online, cada uma repetindo saque, parado, mira com tiros e recarga (a faca, os quatro golpes e o saque; a granada, armar, lançar e sacar o fuzil), com o nome e a fase em cima; o menu do nível dos bots sai — tirado antes de a cena entrar no jogo (tirado depois, ele já tinha pausado o jogo e ninguém despausava: a física ficava parada e os pentes soltos na recarga ficavam no ar; Jhon: "por que aqui eles ficam flutuando?") — e elas animam mesmo com o jogo pausado (o menu de conjunto); ficam no chão embaixo de onde o jogador nasce (um raio para baixo: ele nasce no alto e cai, e na altura dele elas ficavam com os pés a 2 m do chão). Conferido no jogo, em fotos: a Eve da granada engatilha com o braço liso. `Personagens`, `BonecoMaos`, `BracosNaMao`, `SacaEGuarda`, `Granada`, `GranadaNaCruz`, `GranadaNaMira`, `BotsJogamGranada`, `CabecaNoLugar`, `BonecoMarcas` e `ArmasEpicasELendarias` passam (`GranadaFere` já falhava).
    - **Décima rodada, 23/11 — a granada engatilhada na frente, a mão virada** (Jhon: "e como ficaria o lançamento respeitando os limites de torção do original?"; depois "gostei da amplitude do lançamento, mas a mão com granada está ao contrário"). Engatilhada, a mão vem um comprimento de braço para a frente da referência, ao lado da cabeça (`LANCE_ARMADO_ADIANTA`, do fim do armar ao começo do lance; `perto_do_armado`), e o braço de cima gira 54° em vez de 130° (`TORCAO_LANCE` 50 no braço do lance). Com a orientação de atrás da cabeça a palma ficava para o rosto: agora vira para a frente e um pouco para cima, os dedos para cima segurando a granada e o dorso para o rosto (`LANCE_ARMADO_MAO`). A passagem por cima da cabeça no lance continua girando o braço até 119° (é o lance por cima; o osso de torção desfaz 60% disso perto do ombro). A esquerda desce um pouco mais aberta no lance (`LANCE_ABRE_E` 0,7: a 0,45 o antebraço entrava 18 mm no colete depois que ela passou a terminar mais aberta). Medido: o braço não entra no tronco, fora a braçadeira no próprio deltoide com o braço levantado (até 49 mm). `Granada`, `GranadaNaCruz`, `GranadaNaMira`, `BotsJogamGranada` e `BonecoMaos` passam (`GranadaFere` já falhava). O `.json` de antes: `.synctmp/terceira_pessoa_2311/eve_antes_torcao_jsons/granada_antes_mao_virada.json`.
    - **Décima primeira rodada, 23/11 — a recarga troca o pente** (Jhon: "na hora que a Eve tira o carregador vazio e vai colocar outro, dá umas torcidas e ela coloca o mesmo carregador"; por fim: "a Eve tira o pente, ele cai no chão (velocidade da gravidade), tira outro pente do bolso; o pente precisa estar nas mãos, e orientado na posição correta baseada na pegada da mão, veja a referência na recarga em primeira pessoa"). Na primeira pessoa a mão tira o pente, desce com ele até fora da câmera (uns 40 cm) e volta com o mesmo; de fora isso aparecia, e a mão ia até a cintura com a orientação de quem segura o pente na frente do olho (o braço torcia). Agora (`Copia.trocar_pente`, `fases_do_pente`, `punho_no_bolso` no `mao_manny_anim.py`): da primeira pessoa vêm a saída do pente (a mão o tira como lá), **a pegada** (como a mão de lá segura o pente: rígida do quadro em que o agarra até o encaixe) e a pose do encaixe; o meio é daqui. A esquerda solta o pente quando já o puxou uns 20 cm para fora do encaixe; desce vazia e aberta, cotovelo para trás, até o bolso no lado esquerdo do quadril (`RECARGA_BOLSO`); pega o novo — de pé ao lado do quadril, seguro pela pegada de lá, na orientação (boca para cima ou para baixo, inclinado até 43°, virado em volta do eixo) que deixa o braço mais à vontade (na AR-15: boca para baixo, inclinado 24°, o punho dobrando 35°) —; sobe até ficar 12 cm abaixo do encaixe, já alinhada com ele, e o empurra para dentro em 3 quadros, chegando na pose de lá no quadro do encaixe. Na recarga de pente vazio da Desert Eagle a mão de lá nem segura o pente (sai antes, e ele é ejetado): aqui também ele cai direto da arma. Os tempos (agarra, solta, pega, encaixa) vão no `.json` de cada recarga (`"carregador"`); o jogo os lê (`HoldClips`) e `HeldWeapon.SwapMagazine` prende o pente do conjunto na mão esquerda do corpo pela pegada de lá, quadro a quadro (onde o pente do conjunto está em relação à mão do conjunto agora, levado para a mão do corpo; a relação entre as duas mãos é medida com elas juntas, parado ou mirando); ao soltar, o pente some e uma cópia cai e obedece à física até parar deitada no chão (`DropMagazine`; Jhon: "o pente precisa cair 100% no chão e respeitar a física"): sai com 35% da velocidade e do giro que tinha na mão (`MagazineThrow`; com tudo, voava 1,3 m para o lado: lá a mão arranca o pente a mais de 2 m/s), a gravidade faz o resto, a forma que bate é a do próprio pente (o casco convexo da malha: com a caixa em volta dela ele parava de pé no ar, a 7 cm do chão), com atrito, um pouco de quique e colisão contínua; medido: bate no chão 0,7 s depois de solto, tomba e dorme deitado com o centro a 8 mm do chão, a 30 cm de onde saiu. A cópia usa uma malha do pente sem os dados da pele, montada uma vez por arma (`LooseMagazine`: a do conjunto, solta num corpo físico, o motor não desenhava). Só bate no cenário, não fere ninguém, e some em 8 s (`DroppedMagazineLife`); do bolso ao encaixe o novo vai na mão. Três erros achados no caminho: a troca não rodava no boneco de outro jogador (ele não sabe que recarrega, só toca a animação; basta o conjunto tocar a recarga); as medidas do conjunto saíam em centímetros (a escala 0,01 dele; o pente era "solto" ainda dentro da arma); e uma versão guardava o pente no bolso em vez de soltar (desfeita). As 6 armas da Eve (as duas recargas da Desert Eagle); `HeldRig.cs` não foi mexido. O teste `BonecoMaos` mudou junto: na troca só a direita fica com a de lá, e a esquerda tem de ir ao bolso (37 cm). Conferido no jogo, com a câmera na mão esquerda de um boneco (AR-15 e Desert Eagle): o pente na palma ao sair, some ao soltar e a cópia cai até o chão, o novo aparece na mão no bolso, sobe e encaixa. Os `.json` de antes: `.synctmp/terceira_pessoa_2311/eve_antes_troca_pente/`.
    - **Décima segunda rodada, 23/11 — o lance da granada vai para dentro, a granada maior, a palma para o rosto** (Jhon: "o lançamento da granada, a mão gira para dentro, não para fora. aumente o tamanho da granada também"; depois: "a palma da mão tem que estar na direção do rosto"). Depois de soltar, o braço acompanha para dentro: a mão cruza na frente da barriga, para o lado esquerdo dela, e volta ao braço solto (`LANCE_DENTRO` −0,3/0,5/−0,15 do comprimento do braço, pico em 0,27 s, `LANCE_DENTRO_PICO`); o empurrão para fora de antes (`LANCE_FORA`) ficou em 0. Engatilhada ao lado da cabeça, a mão agora tem os dedos para cima e a palma para o rosto (para dentro e um pouco para trás), com a granada entre a mão e a cabeça e o dorso para fora (`LANCE_ARMADO_MAO`, agora regulável por variável; antes a palma ia para a frente). A granada na mão, vista de fora, ficou 1,4× maior (`HeldWeapon.GrenadeHeldGrow`, cresce o osso `Grip` do conjunto em volta do centro dela; a da primeira pessoa e a que voa não mudam), e os dedos da direita abrem um pouco para ela (`GRANADA_MAO_ABRE` 0,3). Um erro achado no jogo: a animação repõe a posição e a rotação do `Grip` a cada quadro, mas não a escala, e o aumento se acumulava quadro a quadro até a granada sumir; agora parte sempre da escala sem aumento. Medido no Blender (`MEDIR_BRACO`): o braço direito não entra no tronco com a palma para o rosto; ficam as leituras da braçadeira contra a pele do braço (12–51 mm, o artefato conhecido) e as do lance que já existiam (antebraço 44 mm no colete num quadro, 20–27 mm do braço na lateral do colete). Conferido no jogo, na vitrine: a granada maior na mão, a palma para o rosto engatilhada e a mão indo para dentro depois de soltar. Testes: `Granada`, `GranadaNaCruz`, `GranadaNaMira`, `BotsJogamGranada`, `BonecoMaos`, `BonecoLobo`, `BonecoMarcas`, `Personagens` passam; `GranadaFere` continua falhando como antes. O `.json` de antes: `.synctmp/terceira_pessoa_2311/granada_antes_palma_rosto.json`.
    - **Testado no jogo:** `BonecoMaos`, `BonecoMarcas` e `BonecoLobo` com os `.json` novos, e fotos do boneco no jogo (parado e mira de cada arma). **Não testado:** partida com dois clientes de verdade, nem os clipes em movimento no jogo (recarga, sacar, mirar) — esses só no Blender. As ferramentas (validador, fotos do Blender e do jogo), as fotos de antes e depois e as tabelas estão em `.synctmp/terceira_pessoa_2311/`; os `.json` e o script de antes desta rodada, em `.synctmp/terceira_pessoa_antes_2311/`.
  - **Não rodado (22/11):** o jogo, o `dotnet build` e o `tools/testar.sh` (ambiente sem o Godot .NET). O teste `BonecoMaos` compara o jogo com o Blender e precisa ser rodado. As animações de antes de 22/11 à noite, o `HeldWeapon.cs`, o `HoldClips.cs` e o script de antes estão em `.synctmp/terceira_pessoa_antes_escala/`.
  - Fotos: `--vistas palma` (a mão direita vista por baixo). Cada arma leva ~2 min por personagem agora (eram ~15 s).

- **Terceira pessoa: cada arma toca as animações da primeira pessoa, refeitas no Blender para o corpo** (Jhon, 22/11: "vamos copiar exatamente as animações dos braços, tronco e etc e vamos adaptá-las nos personagens"; depois, olhando as fotos: armas que não encostam nas mãos, gatilho fora do dedo, arma atravessando o corpo, ponta dos dedos virada para trás, pulso torto, dedos separados e longos, faca longe de um movimento humano, granada mal segurada e sem força, recarga que não pega o carregador, mira de cada arma, "cada arma tem sua posição de segurar", "a faca, ele deve segurar a faca envolvendo os dedos sob ela", "ele não tá encostando a arma na palma da mão", "Braço ta para dentro do corpo, está sem dinamismo no lançamento"). Antes era uma pose parada por tipo de arma (`poses_armas.json`) e só as mãos seguiam a primeira pessoa; agora o corpo inteiro da cintura para cima tem um clipe para cada clipe da primeira pessoa, por arma e por personagem.
  - **O script novo** `tools/personagens/blender/animacoes_armas.py` abre o humano e o conjunto da primeira pessoa de cada arma (ak74, ar15, m4a4, scar, svd, deagle, faca, granada), põe o conjunto no corpo (no quadril; na mira, a linha da mira no olho e a coronha no ombro) e resolve quadro a quadro, a 30 por segundo: as mãos onde as de lá estão, com a palma encostada na arma; os braços por IK de dois ossos, com o cotovelo que deixa o punho no que ele dobra; cada dedo na direção do de lá, dentro do que a junta dobra, fechando até encostar na arma e nunca dentro dela (o indicador no gatilho). Cada arma tem a própria pegada (o giro da mão no cabo, a mão de apoio deslizando no guarda-mão, procurados para cada arma). Quando o braço do personagem não alcança (recarga), a arma vem para perto do corpo, como manda o manual ("draw the weapon in close"). Grava `assets/personagens/<p>/animacoes/<arma>.json` (a rotação de cada osso em cada quadro e onde o conjunto fica em relação ao peito; ~1,2 MB por personagem).
  - **Mira de cada arma**: mirar, mira (parado e andando), tiro na mira e desmirar, com os clipes de mira de cada conjunto: o fuzil com o rosto na coronha e a coronha no ombro; a pistola com os dois braços quase esticados. A mira vai pela rede: o estado leva um bit novo (`NetWire`, no byte do equipamento; na partida autoritativa, `EntityFlags.Aiming`); jogo antigo lê "não está mirando".
  - **Faca**: a mão fechada em volta do cabo, os quatro dedos e o polegar envolvendo; o golpe 1,35× maior que o da primeira pessoa, o tronco girando junto com a lâmina, e a outra mão em guarda na frente do peito.
  - **Granada** (o manual do exército, FM 3-23.30): a esquerda puxa o pino; armado, de lado para o alvo, o braço esquerdo apontando e a granada atrás da orelha. No lance o tronco gira 87° — antes girava **ao contrário** (armava com o ombro direito para a frente e lançava girando para trás: por isso ficava sem força e o braço esquerdo entrava no corpo) —, o cotovelo vem primeiro, a mão passa por cima do ombro ao lado da cabeça, solta na frente dela em 0,11 s e segue cruzando o corpo até o quadril esquerdo, com o tronco dobrando para a frente; a esquerda puxa para o lado do quadril, o cotovelo para trás. A mão livre e a da granada (uma bola na palma: a granada gira junto com a mão) ficam em até ~60° de dobra e ~25° de lado no punho.
  - **No jogo** (`HoldClips`, `HeldRig`, `HeldWeapon.HoldByClips`): o clipe de terceira pessoa toca junto com o do conjunto — o mesmo clipe, no mesmo instante, misturando nas trocas como a arma mistura. O peito fica como no Blender em relação ao corpo: o quadril vem da animação de andar e as vértebras desfazem o giro dele (sem isso a arma apontava 40° para o lado). O peito inclina para a mira e o conjunto vai onde o clipe diz, em relação ao peito. Personagem sem a pasta `animacoes/` segura a arma pela pose de antes.
  - A coronhada continua só na primeira pessoa (no corpo o punho ia a 94°).
  - Teste `BonecoMaos` refeito: nos dois personagens, as mãos a 0,8–4,2 cm das do conjunto (a esquerda pode deslizar no guarda-mão), na mira o cano para a mira (1,00), pela rede o boneco mira e volta ao descanso, e na recarga as mãos a no máximo 2,4 cm das do conjunto. Passam também `BonecoMarcas`, `BonecoLobo`, `Granada`, `GranadaNaMira`, `GranadaNaCruz`, `BotsJogamGranada`, `BracosNaMao`, `SacaEGuarda`, `Personagens`, `CabecaNoLugar`, `MarcaNoOmbro`, `EstadoComEquipamento`, `ParedeOnline`, `PassoDoServidor`, `DadosDoServidor`, `AgachadoMira`, `MiraDaArma`, `EspectadorCorposInteiros`, `Coronhada`, `ZoomNaMira` e `MunicaoFinita`. Falham como já falhavam sem esta mudança: `GranadaFere` (quem lançou, a 4 m, não leva dano), `MaosPadrao` e `Animacoes`. Conferido também com fotos do jogo (pistola, faca, fuzil, recarga, granada armada e o lance quadro a quadro).
  - Para valer online: o jogo novo nas duas máquinas e a imagem do servidor de partida publicada de novo (o bit da mira vai no estado). **Não testado em partida online de verdade**; `testar_online.sh` e `testar_servidor.sh` não rodaram aqui (sem Docker).

- **Online: o servidor "não deixava andar para frente" por um tempo** (Jhon, 22/11: "senti o servidor dando umas bugadas, de não deixar eu andar para frente, mas foi só por um tempo, depois normalizou"). Na partida autoritativa, duas causas:
  - **O corpo de quem morreu ficava sólido no servidor até renascer.** Morto, o corpo do servidor não dá passo, e é num passo (ao assentar, `Corpse.Settle`) que a colisão de um morto sai — então a cápsula ficava lá, invisível, onde ele caiu. Quem passava por ali era barrado no servidor e puxado de volta no jogo, que já não via ninguém, até o morto renascer. Agora a colisão sai na morte (`Corpse.Drop`) e volta ao renascer. Teste novo `MortoNaoBarra`: vivo no caminho, barra; morto, quem anda passa por onde ele caiu; renascido, barra de novo (sem o conserto, quem andava parava 0,6 m antes de onde o outro caiu).
  - **A fila de comandos dava passos a mais e a menos com a rede oscilando** (`CommandBuffer`). Quando um comando atrasava, o servidor repetia o último — um passo que o jogo não deu —, e quando um buraco não fechava, pulava o comando — um passo a menos. Cada vez o jogo era corrigido, para a frente e para trás. Agora, faltando o comando, o corpo espera parado (o servidor fica um passo atrás, como já ficava), e o comando perdido é dado com o último segurado, no número dele: o servidor dá exatamente os passos do jogo. Teste novo `AndarComRedeOscilando` (atraso de 50 a 183 ms, um pacote em cada cinco perdido; andando, virando, pulando e de lado): antes, 84 correções em 6 s, até 35 cm; agora, nenhuma. O teste de unidade da fila mudou junto, e a decisão 0011 também.

- **Lobo na parede online: a animação não começava e o lobo deslizava na pose do pulo** (Jhon, 22/11: "animação de andar sob parede do lobo não inicia, o lobo fica com a pose de pular deslizando pela parede"). Quem vê de fora vê o corpo do servidor; quando ele saía da parede e o do jogo não, a regra da mesma parede ("salte para outra ou toque o chão") não o deixava regrudar: caía de pé, deslizando, e a correção arrastava o jogo. Os passos a mais e a menos da fila (acima) eram a fonte principal dessa diferença. Além disso, o jogo agora conta no comando que está na parede (`InputCommand.OnWall`, um bit novo; servidor antigo ignora) e, com três passos seguidos do jogo na parede e o corpo do servidor fora, o servidor volta para ela (`Player.ResumeWall`, com as regras do corpo: no ar, lobo, segurando W ou agarrado, a parede ao alcance). Teste novo `ParedeServidorSolta` (o servidor solta a parede sozinho, com a rede oscilando): volta em 3 passos e nenhuma correção depois; sem os consertos, 119 passos fora e correções de até 49 cm. No `ParedeCorrecao` as correções caíram de 51 para 6.

- **Rosto quebrado, cabelo sumindo e mãos "rasgadas" depois de algumas mortes** (Jhon, 22/11: "o servidor iniciou ela corretamente, sem bugs, depois que matei algumas vezes, ficou assim, para ambos jogadores"). Ao morrer, o corpo some aos poucos com uma cópia transparente de cada material; ao renascer só a opacidade voltava a 1, e a cópia continuava transparente — e um corpo transparente desenha o que tem por dentro (olhos, dentes, o cabelo de baixo, o forro das luvas) por cima da pele. Agora, ao renascer, cada superfície volta ao material que tinha antes da morte (`CharacterAnimator.RestoreMaterials`), e durante a morte a cópia desenha a profundidade antes da cor (`AlphaDepthPrePass`), então nem o sumiço mostra o de dentro. Teste novo `RenasceInteiro` (Barut e Eve mortos e renascidos: os mesmos materiais de antes, nenhum transparente; sem o conserto, 6 e 9 superfícies continuavam transparentes).

  Para valer online (os três acima): o jogo novo nas duas máquinas e a imagem do servidor de partida publicada de novo, com a frota atualizada (`tools/fly/frota.py`). Os consertos da fila, do morto e da parede são da partida autoritativa (`ALCATEIA_NETCODE=autoritativo`); no repasse nada muda. O de desenho vale nos dois modos.

- **Primeira partida num servidor de partida novo caía** (Jhon, 22/11: "ao ficar muito tempo sem partidas, na hora que vai jogar, ele entra na partida e logo depois a partida cai, somente na segunda partida que fica estável"). Não era hibernação: a reserva da frota (`ALCATEIA_FLEET_RESERVE=1`) liga uma máquina nova quando a partida ocupa a que estava de pé, e depois de um tempo sem jogo é essa máquina nova — que nunca abriu partida — que fica. No processo novo, o primeiro mundo e o primeiro corpo carregam o mapa e o jogador do disco e compilam o C# na hora; o servidor inteiro para nesse tempo (a rede não responde, a ligação do jogador cai por tempo, e o relógio da partida dá um salto que conta como "ninguém entrou"/"calado demais"). Medido aqui (jogo com a arte inteira): 4,8 s o mundo e 36,7 s o primeiro corpo; da segunda vez, 0,07 s e 0,14 s. Agora:
  - **O servidor aquece antes de se oferecer** (`MatchWorld.Warmup`, chamado no `_Ready` do `MatchServer` antes de abrir a porta e bater): carrega os 9 mapas e o jogador (e os mantém na memória), monta um mundo e um corpo e joga fora; o log diz quanto levou (`Match server: warmed up in … ms`). Medido aqui: aquecimento 6,3 s; depois, o primeiro mundo e o primeiro corpo de cada mapa abrem em 0,2 a 0,6 s.
  - **Um quadro lento não conta como tempo para os prazos**: no `_Process` do servidor de partida o quadro conta no máximo 0,25 s (quem ligou e não se apresentou, a partida vazia, o relógio).
  Testes `VidaNoServidor`, `PassoDoServidor`, `PredicaoNoServidor` e `ParedeOnline` passam. **Não testado no Fly** (o servidor exportado, com a arte trocada por cascas, deve aquecer mais rápido; a memória com os 9 mapas carregados não foi medida lá — a máquina tem 1 GB). Para valer: publicar a imagem da partida de novo (o lançamento faz) e atualizar a frota (`tools/fly/frota.py`).

- **Dois jogos na mesma máquina, cada um com a sua conta** (Jhon, 22/11: "como eu jogo 2 jogos ao mesmo tempo no mac? para testar multijogador"). Todo jogo da máquina lia o mesmo login: a segunda janela entrava com a mesma conta. Agora `-- --perfil=b` (depois do `--`) guarda o login, o perfil e a foto em `user://perfis/b/` (`UserFiles.Slot`/`OfAccount`); as opções continuam as da máquina, de todos. Sem o argumento nada muda. Para abrir em janela, os argumentos do próprio Godot: `--windowed --resolution 1280x720 --position 0,40`. Conferido aqui: sem o argumento os caminhos são os de sempre; com `--perfil=B-2`, `user://perfis/b-2/profile.json` e `online.cfg`, e as opções em `user://settings.json`.

- **Opções no jeito do Overwatch, com rolagem** (Jhon, 22/11: "não consigo descer. organize isso como overwatch"). As opções eram três colunas de cartões com a explicação embaixo de cada ajuste, sem rolagem: a coluna do vídeo passava da tela e o fim dela (desempenho, sangue) não tinha como ser alcançado. Agora são abas no alto (**Vídeo, Áudio, Controles, Mira, Jogo**), um ajuste por faixa — o nome à esquerda, o controle à direita, seções em destaque (Imagem, Quadros, Sistema; Volume; Mouse, Teclas; Formato, Cor; Partida, Idioma) —, a lista rola, e o painel ao lado mostra o que a faixa sob o mouse (ou com o foco) faz; a faixa apontada acende. Liga/desliga virou "‹ Ligado ›", número e deslizante como no Overwatch, as teclas viram faixas com a tecla numa caixinha. Na aba Mira a prévia fica no painel ao lado. **Restaurar padrão** volta a aba aberta ao de fábrica (o modo de vídeo do Windows e o idioma não mudam). A aba aberta fica lembrada. O menu do Esc na partida usa a mesma vista, num tamanho que cabe na tela. "Sangue ao acertar" e o idioma foram para a aba Jogo; "Mostrar desempenho" ficou em Vídeo. `MiraDoJogador` passa pela aba Mira. Os textos novos entraram no `textos.csv` (`tools/i18n/extrair.py`, que também pegou os de outras mudanças desde a última vez). Conferido com fotos da tela a 1920×1080.

- **Terceira pessoa: as poses com arma agora são feitas no Blender** (Jhon, 22/11: "use o blender para isso, não ficou bom"; "pistola pode ir mais na frente"). Para cada personagem (Barut, Eve) e cada tipo de arma (fuzil, pistola, faca, granada), o script novo `tools/personagens/blender/poses_armas.py` monta no Blender a pose da cintura para cima: o tronco (o fuzil com o corpo de lado, a coronha no ombro; a pistola com os dois braços esticados à frente; a faca baixa, apontada para a frente, com a outra mão fechada em guarda; a granada na palma, para cima, à mostra), a cabeça, os ombros, o cotovelo escolhido para o punho ficar reto e cada dedo fechado na arma como na primeira pessoa. A arma é a da primeira pessoa, no tamanho de verdade. Grava `assets/personagens/<p>/poses_armas.json` (a rotação de cada osso no espaço do jogo, com uma respiração leve).
  - No jogo (`HeldWeapon`, `HoldPoses`), o corpo da cintura para cima vai para a pose do tipo da arma (troca suave entre uma e outra), o peito vira para a mira (as pernas e o quadril continuam vindo da animação de andar) e o conjunto da primeira pessoa é posto pela mão direita da pose. A animação da primeira pessoa (sacar, tiro, recarga, golpes da faca, armar e lançar a granada) mexe as mãos a partir da pose: a direita segue a de lá e a arma vai com ela; a esquerda vai na arma (fuzil, pistola) ou fica na pose (faca, granada). Personagem sem `poses_armas.json` segura a arma do jeito de 21/11.
  - Para ajustar à mão: `poses_armas.py <pasta> --blend` grava `tools/personagens/blender/poses/<pasta>.blend` (uma ação por tipo, a arma presa na mão); depois de mexer, `--do-blend` regrava o `.json`. O `.blend` fica fora do git (`.gitignore`) e a pasta tem `.gdignore`. Personagem novo: rodar o script (`docs/PERSONAGENS.md`).
  - Teste `BonecoMaos` refeito: nos dois personagens, as mãos, a cabeça e a arma no jogo ficam onde o Blender deixou (a maior diferença foi 1,4 cm), a pistola aponta para a mira (1,00), o fuzil sobe com ela, o punho com pistola, faca e granada fica dentro do limite e a granada encosta na palma. Passam também `BonecoMarcas`, `BonecoLobo`, `CabecaNoLugar`, `Personagens`, `BracosNaMao`, `Granada`, `GranadaNaCruz`, `BotsJogamGranada`, `Coronhada`, `SacaEGuarda`, `ArmasEpicasELendarias`, `Espectador`, `TelaDoAliado`, `EstadoComEquipamento` e `MunicaoFinita`; `Animacoes` falha como antes (14 de 17 clipes do Mixamo: faltam `crouch_idle`, `fall`, `land`…). **Não testado em partida online de verdade.**
- **Terceira pessoa: a arma e as mãos vêm da primeira pessoa** (Jhon, 21/11: "melhore essas animações, não estão profissionais… use o posicionamento das armas, tamanho, posição dos dedos, punho, ombro das animações primeira pessoa… mas não ficar torto"; e depois: "granada pode ser maior, tem que tocar na mão", "mão sem granada totalmente torta"). Cada corpo segurando uma arma com conjunto de primeira pessoa (`assets/armas/fp/`, a ficha `bracos.json`) tem uma cópia dele, com os braços escondidos (`HeldRig`): a arma que os outros veem é a mesma, no tamanho de verdade, tocando a mesma animação — parado, andar, tiro, **recarga** (no tempo da recarga; o carregador sai na mão), os **golpes da faca**, **armar e arremessar a granada** (ela some da mão quando sai) e sacar. As mãos do corpo vão onde as do conjunto estão, o punho girado como lá e cada dedo dobrado o quanto o de lá dobra (o afastamento dos dedos fica o do personagem). O ombro e o cotovelo continuam os do corpo: no conjunto eles são arrumados para a câmera, e copiá-los entortava o braço. O que mudou além disso:
  - **O punho não entorta**: o cotovelo vai para trás da mão, na linha dela; o que sobra de dobra além de 28° a mão desfaz (`WristBend`). O giro da mão é repartido com o antebraço (`ForearmTwist`; o corpo não tem osso de torção).
  - **A arma de uma mão vai com a mão** (pistola, faca, granada): nunca sai dela. A pistola leva a mão esquerda junto, e os **polegares deitam na arma** (o do personagem, mais comprido, ficava em pé em cima do ferrolho).
  - **A granada cresce com a mão** do personagem (≈1,5×: a mão dele é maior que a do conjunto), fica **encostada na palma** e os dedos e o polegar fecham em volta dela.
  - **A mão que não segura nada fica solta ao lado do corpo**: a esquerda com a faca, e com a granada quando a do conjunto está fora da tela (ela não foi feita para ser vista). Antes ficava torta ou aberta como garra.
  - **O fuzil encosta no ombro**, com a mão de apoio fechada no guarda-mão (`SupportCurl`; os dedos do personagem, mais longos, passavam por cima). Sobe e desce com a mira.
  - Com a arma vindo do conjunto, a recarga e o lance de granada dos clipes Mixamo (`reload.fbx`, `grenade.fbx`) não tocam mais por cima (`CharacterAnimator`); continuam valendo para arma sem conjunto.
  - Os ajustes ficam no `HeldWeapon` (grupo "Terceira pessoa: conjunto"): onde o conjunto fica em relação ao olho por tipo de arma (`RigRifle`, `RigPistol`, `RigKnife`, `RigGrenade`), `WristBend`, `ElbowFollowsHand`, `GrenadeGrow`, `ThumbForward`, `SupportCurl`; `UseFirstPersonRig` desligado volta a arma de antes.
  - Teste `BonecoMaos` refeito: a pistola aponta para a mira (0,99) com as duas mãos no cabo, o punho com pistola, faca e granada dentro do limite, a granada a 5 cm da palma e 1,47× maior, a mão de apoio do fuzil onde a do conjunto está e o fuzil subindo com a mira. Passam também `BonecoMarcas`, `BonecoLobo`, `BracosNaMao`, `Granada`, `GranadaNaCruz`, `BotsJogamGranada`, `Coronhada`, `SacaEGuarda`, `ArmasEpicasELendarias`, `CabecaNoLugar`, `Espectador`, `TelaDoAliado`, `EstadoComEquipamento` e `MunicaoFinita` (`MaosPadrao` falha como antes: material e triângulos das mãos da primeira pessoa). **Não testado em partida online de verdade.**
- **Debug, instalador e arquivos do jogo em inglês** (Jhon, 21/11: "essas infos de debug, instaladores, arquivos do jogo, devem tudo ser em inglês"). A interface do jogo continua com os idiomas de sempre (`textos.csv`).
  - **Painel de desempenho**: "perda" virou "loss"; a linha é a mesma em qualquer idioma.
  - **Instalador** (`windows.nsi`): textos em inglês e o NSIS em `English`; o desinstalador agora é `Uninstall.exe` (o `Desinstalar.exe` antigo é apagado ao instalar por cima); a pasta no registro é `InstallPath`.
  - **Iniciador** (`iniciador.nsi`): mensagens em inglês; o jogo fica em `game\` (era `jogo\`; ao instalar por cima, a pasta velha é apagada); o `video.ini` grava `mode=` e ainda lê o `modo=` antigo (o seletor de Opções também).
  - **Arquivos na pasta do jogador** (`UserFiles`): `settings.json` (era `opcoes.json`), `profile.json` (era `perfil.json`), `avatar.cfg` (era `foto.cfg`, chave `[profile] avatar`); `online.cfg` com `[server]` e `[accounts]`. O nome antigo é renomeado na primeira abertura e as chaves antigas ainda são lidas: ninguém perde perfil, opções nem login.
  - **Log** (`godot.log`): as mensagens do jogo e do servidor de partida em inglês, inclusive os motivos de acerto recusado (`HitCheck.Describe`) e o log da corrida na parede. Ficaram em português os textos que o jogador vê (o aviso de partida que caiu, o motivo da recusa na porta), os incidentes que vão para o painel e a linha `ROBO` do robô de teste (o `testar_online.sh` lê).
  - **Servidor de partida**: o executável é `WolfLeagueServer` (era `WolfLeagueServidor`), em `/game` na imagem; `export_presets.cfg`, `partida.Dockerfile` e `preparar_exportacao.py` acompanham. **A próxima imagem da partida precisa ser construída de novo** (o lançamento já faz).
  - **Mac**: o atalho no `.dmg` é `Applications`; o identificador do app é `com.wolfleague.game` (era `com.wolfleague.jogo`: o Mac pode pedir "Abrir Mesmo Assim" de novo uma vez).
  - **Capa da Release** (`capa.png`): "Online FPS · Humans × Wolves" e "ALPHA TEST".
  Compilado; `Idiomas`, `PerfilPersiste`, `ContaNoPerfil`, `Login` e `TelasDoLobby` passam, e a migração dos arquivos foi conferida aqui. **O instalador e o iniciador não foram compilados nem testados no Windows** (sem NSIS aqui): rode `instalador/teste/testar_iniciador.ps1` num Windows antes de lançar.

- **Online, só o mestre da sala reinicia, e reinicia para todos** (Jhon, 21/11: "somente o mestre da sala pode reiniciar partida"). Antes, online, "Reiniciar partida" (Esc) e "Jogar de novo" (resultado) não reiniciavam nada: mandavam só quem clicou de volta para a sala, e os outros seguiam jogando. Agora quem não é mestre não vê o "Reiniciar partida" no Esc, e no resultado o primário dele é "Voltar à sala" (dica: "Só o mestre da sala reinicia a partida."). O mestre confirma ("Reiniciar para todos? A partida recomeça do zero para todos da sala. Esta não rende XP, Presas nem missões para ninguém.") e o jogo manda `room.restart` ao saguão: só o dono pode; a partida em curso sai da frota (o resultado dela, quando o servidor de partida mandar, é recusado como partida desconhecida — ninguém ganha nada por ela) e a sala inteira recebe o ingresso de uma nova, como no começar, sem esperar ninguém marcar pronto (`Room.RestartLineup`, `LobbyService.Restart`; `match.start` leva `restart`). No resultado (a sala já esperando), o "Jogar de novo" do mestre faz o mesmo e leva todos. No jogo, a partida nova chegando com uma em curso larga o servidor da de agora na hora e não volta para a sala (`NetMatch.Supersede`): o carregamento leva direto para a nova. Sem servidor livre, aparece o aviso do saguão. Fora do online, nada muda. A partida descartada fecha sozinha no servidor de partida 20 s depois de todos saírem (o painel registra como "partida.abandonada"). Teste novo do servidor ("reiniciar: só o mestre…"): o convidado é recusado, os dois recebem a nova, o resultado da descartada volta 409 e não entra no histórico, o da nova paga, e o "jogar de novo" no resultado leva os dois. Testes do servidor: 54 de 58 (as mesmas 4 falhas de antes). **Precisa publicar o servidor (API) e o jogo novo; não testado em partida de verdade.**

- **Online: o outro jogador aparece fazendo o que faz** (Jhon, 21/11, jogando com dois PCs: lobo andando em pé aparecia de quatro; o rugido e o especial não apareciam para o oponente; ao virar lobo o boneco voltava a humano e virava lobo de novo; leve atraso; personagens com defeito no desenho; pistola, faca e granada fora das mãos; o lance da granada não aparecia). O que mudou:
  - O estado de cada jogador passa a dizer se ele está **correndo de quatro, agachado, rugindo, andando quieto** e com a **granada armada** (antes o boneco adivinhava a corrida pela velocidade e errava, e o resto não existia). Na partida autoritativa o servidor já mandava correr e agachar; rugir entrou (`EntityFlags.Howling`).
  - Agachado, a **cápsula do boneco encolhe** como a do dono (o tiro por cima da cabeça abaixada passa) e o corpo desce com os pés no chão. O passo de quem anda agachado ou devagar não faz barulho nas outras máquinas.
  - **Golpes do lobo vão quando começam**, com qual golpe é: a garrada do braço certo, o **salto do especial** (com o voo no ritmo do dono) e o **bote** (batendo no tempo que o dono previu). Antes ia só "atacou", na hora do impacto, e o boneco tocava uma garrada atrasada.
  - **A transformação não pisca mais**: o "ainda virando" que chega depois de o boneco terminar é ignorado. E o estado vai em ordem (`UnreliableOrdered`): estado velho que chega depois de um novo é jogado fora.
  - **Menos atraso**: 30 estados por segundo (eram 20) e o boneco se adianta também o atraso da própria perseguição suave (~55 ms a menos).
  - **Personagens sem buracos**: a Eve e o Barut eram importados com LOD automático, e a poucos metros o Godot trocava para malhas simplificadas — cabelo oco, buracos pretos na jaqueta, o rosto sem a pele em volta dos olhos e da boca. Desligado no `.import` (o lobo já estava) e travado no detalhe cheio em `SkinCulling`.
  - **Pistola, faca e granada nas mãos como na primeira pessoa**: ficam numa pose de mira e os braços vão até elas (`ArmIK`, dois ossos), com palma e dedos nos pontos do `armas.json`. Pistola com as duas mãos; faca e granada na direita, a esquerda em guarda; a faca estoca e puxa na diagonal, alternando. O tronco inclina com a mira (o fuzil sobe e desce junto). Ajustes no Inspector do `HeldWeapon` (grupo "Terceira pessoa").
  - **O lance da granada aparece** para quem vê de fora: com a granada armada o braço arma; sem isso (bot, ou soltou rápido), o lance toca quando ela sai.
  - Testes novos: `BonecoMarcas` e `BonecoLobo`. Nada disto foi compilado aqui (sem Godot nem dotnet no ambiente): abra o projeto, rode os testes e jogue com os dois PCs. **Os dois jogos e a imagem da partida precisam ser a versão nova** (o servidor de partida repassa o estado em ordem).

- **Opções de vídeo novas: resolução dinâmica, limite de FPS, V-Sync e painel de desempenho** (Jhon, 21/11: "o que mais o Overwatch faz"; "quero também opção para jogar com 60, 75, 140 e etc hz"; "uma opção para exibir o fps, ping e informações pertinentes para debug, mas não muita coisa"; a luz assada ficou de fora a pedido dele). Em Opções → Vídeo:
  - **Resolução dinâmica** (ligada por padrão): `ResolutionTuner` mede a cada 0,5 s o tempo da placa no quadro e, se passa da meta (o limite de FPS ou os Hz do monitor), baixa o 3D em 5% (10% se passou muito), até 50%; sobe 5% quando sobra (abaixo de 75% do tempo), esperando 2 s depois de cada descida. A "Resolução 3D" vira o teto. Mede a placa, não o quadro (com V-Sync o quadro sempre dura a meta; e se o atraso for do processador, baixar a resolução não ajuda). Na compatibilidade, que não mede a placa, usa o tempo do quadro. Em máquina que aguenta, fica no teto.
  - **Limite de FPS**: o do monitor (padrão, os Hz dele), 30, 60, 75, 120, 144, 165, 240 ou sem limite (`Engine.MaxFps`). O jogo não troca os Hz do monitor (isso é do sistema); para passar deles, desligar o V-Sync.
  - **V-Sync**: desligado, ligado (padrão, como sempre foi) ou adaptativo.
  - **Mostrar desempenho (FPS e ping)** (desligado por padrão): `PerfOverlay`, uma linha só no alto à esquerda, por cima de tudo, do menu à partida: FPS e ms do quadro, ms da placa, resolução do 3D e, na partida online, ping e perda de pacotes (do ENet: `NetMatch.PingMs`/`PacketLoss`). Fica avermelhada abaixo de 30 FPS ou acima de 120 ms.
  - Placa fraca na primeira abertura (`AutoTune`) também liga a resolução dinâmica. O servidor de partida não usa nada disso (continua a 60).
  Compilado aqui; o teste `Idiomas` passa e o jogo abre sem erro novo (sem tela: os tempos da placa não foram medidos). **Não testado com placa de verdade.** Ficou para depois: oclusores (`OccluderInstance3D`) nos mapas fechados e distância de visibilidade em objetos pequenos, que precisam do editor e de conferir mapa a mapa.

- **Jogo lento em placa fraca: resolução do 3D nas Opções e ajuste automático** (Jhon, 21/11: "o pc windows que testei é um i7 8gen, nvidia mx110 2gb e 20gb de ram, o jogo está extremamente lento"). Em Opções → Vídeo, "Resolução 3D" (100%, 85%, 75%, 65%, 50%): o 3D é desenhado em menos pixels (com FSR 1 no Forward+; filtro simples na compatibilidade) e o HUD, o menu e a mira ficam inteiros. A luz não muda (pedido de 20/11). Na primeira abertura do jogo (`Settings.AutoTune`, uma vez só; vale também para quem já tinha arquivo de opções), se a placa for fraca (MX, GT de entrada, GeForce 8/9xx, vídeo integrado Intel/Radeon, `llvmpipe`) o jogo põe Qualidade Baixa, suavização FXAA e 3D a 65%; o nome da placa vai para o log. Compilado aqui; **não medido em máquina fraca**: não sei os FPS que a MX110 dá agora. O que mais pesa nela é o que o mapa liga em todas as qualidades (luz rebatida SDFGI, oclusão SSAO/SSIL, névoa volumétrica); se não bastar, o caminho é uma luz assada (`LightmapGI`/`VoxelGI`) para máquina fraca, o que muda a luz e exige a decisão do Jhon.

- **Online: o lobo na parede, o seu e o dos outros** (Jhon, 21/11: "a animação do lobo de andar na parede não tá funcionando com o servidor em produção"; "o meu lobo corria sem animação, em pé, na pose do pulo, mas andando na parede"). Dois defeitos.
  - **O seu lobo, na sua tela.** Com a rede oscilando, o servidor às vezes dá um passo a mais ou a menos que o jogo; a correção acerta só a posição, e um puxão de centímetros fazia o jogo soltar a parede ("nada para encostar"). Solto, ele não regrudava — a regra da mesma parede ("salte para outra ou toque o chão") —, enquanto o servidor seguia na parede: a correção arrastava o lobo pela parede de pé, na pose do pulo, até ele tocar o chão (no alto de uma parede, a corrida inteira). Agora o `NetMatch` guarda se o jogo estava na parede em cada passo mandado e, quando o servidor confirma um passo com o corpo na parede em que o jogo estava fora, o jogo volta para ela (`Player.ResumeWallFromServer`: no ar, segurando W ou agarrado, com a parede ao alcance; sem a regra da mesma parede nem a recarga, que são para o jogador, não para a correção). O contrário (o jogo na parede e o servidor não) não solta: um passo de diferença no grudar viraria soltar e regrudar. Saltar da parede e soltar o W continuam como antes (nesses o jogo e o servidor concordam no passo).
  - **O lobo dos outros.** O lobo de quem joga em outra máquina é um boneco (`Bot` remoto), e o desenho do lobo (`WolfVisual`) só perguntava "está na parede?" ao jogador daqui: o boneco corria pela parede com o clipe de pulo, de pé. O servidor autoritativo já mandava o estado (`EntityFlags.WallRunning`) e ninguém usava; no repasse nem ia. Agora o boneco recebe (`Bot.RemoteWallRun`: autoritativo pela bandeira do estado do mundo, repasse por um bit novo no byte de Grounded/Dead do `NetState`, que jogo antigo ignora), acha a parede ao lado do corpo (dois raios, para a normal e o sentido) e o lobo deita nela e engatinha com o clipe da parede. O bot daqui não muda.
  - Testes novos (`tests/regression/WallNetTests.cs`), com o servidor de partida dentro do teste e um pátio com uma parede de 18 m igual nos dois mundos: `ParedeOnline` (o boneco movido pelo estado que chega: 126 passos na parede, 126 com o clipe dela, deitado; com o desenho antigo, 0 com o clipe) e `ParedeCorrecao` (rede oscilando de 50 a 180 ms e 1 pacote em 5 perdido; no meio da corrida o corpo do jogo, só ele, leva um puxão de meio metro para fora: com o conserto o jogo volta para a parede e fica fora dela 3 passos depois do primeiro terço de segundo; sem, 15, caindo até o chão). Os testes de parede e o `PredicaoNoServidor` continuam passando. **Não testado em partida de verdade com o servidor de produção.** Vale com o jogo novo; o servidor não precisa de nada.

- **Os erros de partida aparecem no painel, em Segurança → filtro "Partidas"** (Jhon, 21/11: "a partida caiu do nada... quero que todos esses erros de partida sejam registrados no dashboard"). Quatro grupos, todos como eventos `partida.*`: servidor de partida que sumiu ou desligou sozinho (a API vê pela frota); erros do próprio servidor de partida (exceção no relógio ou na física, resultado que não chegou à API, passo da física lento); jogador recusado na porta ou partida que fechou vazia; ligação do jogador que caiu no meio. O servidor de partida manda os incidentes junto da batida que já dá a cada 2 s (até 20 por batida; fila de 40 se a API estiver fora), e a API só grava os tipos `partida.*`, com a gravidade limitada. O mesmo incidente só é anotado a cada 15 s. Uma exceção numa partida não derruba mais as outras (cada uma roda no seu `try`). O jogo exportado passa a gravar o log na hora (`run/flush_stdout_on_print`): antes o log do Fly ficava preso no buffer e parava de aparecer. **Para valer é preciso publicar de novo o servidor e a imagem da partida.** Não dá para saber, no servidor, se o jogador fechou o jogo ou perdeu a rede: os dois viram `partida.conexao_caiu`. Nada disto foi compilado nem testado aqui (sem dotnet nem Godot no ambiente): rode `dotnet test` e abra o projeto no Godot antes de publicar.

- **A vinheta da Initech roda inteira e fica 3 s no preto antes do jogo** (Jhon, 21/11: "precisa esperar acabar a intro antes de abrir o jogo, e ficar uns 3 seg com tela preta"). Não se pula mais com tecla ou clique (o clique que abre o jogo podia pular). Ela só começa depois de 4 quadros no preto (a janela abrindo engasga), um quadro lento não come pedaço dela (o tempo anda no máximo 1/20 s por quadro) e o menu só começa a carregar no preto do fim, para não disputar com a animação; se ele levar mais que 3 s, o preto dura até ficar pronto. Gravado aqui do jogo (30 quadros/s): vinheta até 3,9 s, preto até 7,0 s, menu. `--sem-vinheta`, robô e servidor continuam indo direto ao menu.

- **Perfil mais limpo, fotos de perfil novas e, na partida, o retrato do personagem** (Jhon, 21/11: "diminua esse 1, tire iniciante, esse menu lateral não precisa"; "se eu tiver de eve, exibir a foto da eve, no tab e na hud"; "os ícones do jogador devem ser no estilo lol"). **Perfil**: saiu o painel do lado (histórico e próximas patentes); embaixo da insígnia só o nível ("NÍVEL 1"), sem o nome da fase; a placa com o número no pé da insígnia ficou menor (`LevelPlate`, 0,2 → 0,13 do lado). **Fotos de perfil**: as 13 antigas (Eve, Barut, a fera e as 10 armas) deram lugar a 20 ícones pintados no estilo dos ícones de jogador dos MOBAs (gerados com IA no Figma, arquivo "WolfLeague — ícones de jogador"): 8 de graça (Lobo Cinzento, a nova padrão, Filhote, Lua Cheia, Coruja Piloto, Geleia, Drone, Panda Vermelho, Corvo) e 12 na loja, de 800 a 2.500 Presas (`Avatars.cs`, itens `foto_*` no `Catalog.cs`, `PhotoRule` no servidor, `jogo.json` regenerado). Quem tinha uma das antigas volta para a padrão. 10 dos 20 estão em 256 px (o Figma limitou as exportações), os outros em 512. **Na partida**: o Tab e a insígnia do HUD mostram o retrato do personagem com que cada um joga (`assets/personagens/<nome>/retrato.png`, das artes da Eve e do Barut que o Jhon mandou), não a foto de perfil. Bots e jogadores de outra máquina aparecem com o do time (Eve no azul, Barut no laranja): a escalação online ainda não leva o personagem, e é esse o modelo que se vê na partida. A sala continua com a foto de perfil. Todo personagem novo precisa de um `retrato.png` (`assets/personagens/LEIA-ME.md`). Compilado aqui (jogo e servidor); testes do servidor: os mesmos 52 de 56 do commit atual (as 4 falhas já estavam lá). **Não aberto no jogo.** As 10 fotos antigas continuam no banco do servidor (a semente só acrescenta): desligar no painel.

- **Vinheta da Initech Games na abertura, sem a tela do motor, e o projeto passa a se chamar WolfLeague** (Jhon, 21/11: "tela preta... aparece logo da produtora (com efeito profissional e transição, som), e enfim o jogo"; "mude o nome do projeto e do jogo para wolfleague"). A tela de abertura do Godot saiu (`boot_splash/show_image=false`, fundo preto) e a primeira cena agora é `scenes/intro/vinheta.tscn` (`scripts/ui/StudioIntro.cs`): preto, o emblema vem de longe com o som subindo e bate no impacto (1,0 s) com o brilho dourado se abrindo, um risco de luz e um tremor curto; INITECH sobe, GAMES abre do meio com as linhas; uma faixa de luz passa pela logo; some no preto e entra o menu, que carrega por trás. Qualquer tecla, clique ou botão de controle pula. Servidor de partida, robô e jogo sem tela não passam por ela (`--sem-vinheta` também pula). A arte é a logo do Jhon recortada em emblema, INITECH e GAMES (`assets/ui/estudio`, de `tools/estudio/recortar_logo.py` com o `initech_games.svg`); o som (`assets/audio/vinheta_initech.wav`, 4,2 s) é sintetizado em `tools/estudio/som.py`: sopro que sobe, impacto grave, acorde de lá menor com nona e cintilado, com reverberação; nada de amostra de terceiros. Gravado do jogo aqui (modo de gravação de filme, 30 quadros/s) para conferir quadro a quadro e com som. O nome do projeto (`config/name`) e do código (`WolfLeague.csproj`, `WolfLeague.sln`, `assembly_name`) agora é WolfLeague: no Mac o executável dentro do .app deixa de se chamar "alcateia", e a pasta de dados do .NET vira `data_WolfLeague_*`; scripts, CI e o Dockerfile da partida foram atualizados. O editor passa a salvar em `app_userdata/WolfLeague` (o login salvo do editor fica para trás). Os projetos do servidor (`servidor/Alcateia.*`) e a biblioteca de animações "alcateia" continuam com o nome antigo: ninguém de fora vê. **Tela de login**: a logo de sempre (`assets/ui/marca/wolfleague_logo_horizontal.png`, a do site e do instalador) no lugar do lobo com o nome escrito, e sem o "HUMANOS CONTRA LOBOS".

- **Windows: o jogo não fecha mais sem dizer nada, e o instalador confere os requisitos** (Jhon, 21/11: "o jogo não abre no windows, clico nele para abrir e nada"; "isso não pode acontecer"; "veja a melhor opção, usada por grandes jogos"). No Windows do Parallels o log mostrava o Direct3D 12 falhando (`No support for any of the suitable shader models (6.0-6.6)`) e o jogo saindo; com Vulkan, saía sem log. O Godot só troca de modo de vídeo quando o driver nem existe: se a placa anuncia o DirectX 12 e falha ao iniciar, ele fecha sem tentar outro (o mesmo aparece em jogos Godot no itch.io). O `project.godot` forçava o DirectX 12 no Windows desde o 11/11; agora o padrão é o do Godot 4.7 (Vulkan). E, como os motores grandes fazem (lista em ordem, o primeiro que abre vale), o `WolfLeague.exe` que o jogador abre virou um **iniciador** (`instalador/iniciador.nsi`, NSIS, 136 KB) e o jogo foi para `jogo\`. Ele tenta, nesta ordem: o modo escolhido nas Opções, o que abriu da última vez (`HKCU\Software\WolfLeague`), Vulkan, DirectX 12, compatibilidade pelo ANGLE (OpenGL traduzido para DirectX 11, o que o Chrome usa; já vem embutido no modelo de exportação oficial, conferido no binário) e OpenGL direto. "Não abriu" = fechou com erro em menos de 20 s. Se nada abre, uma mensagem manda atualizar o driver de vídeo e abre a pasta do log, em vez de nada. `--rendering-driver` na mão passa direto. Nas Opções (só no Windows) entrou **Modo de vídeo**: Automático, Vulkan, DirectX 12 ou Compatibilidade, gravado em `%APPDATA%\WolfLeague\video.ini` (`scripts/core/VideoMode.cs`); se o escolhido não abre, o iniciador volta para Automático. O **instalador** confere os requisitos antes de instalar: Windows de 32 bits ou anterior ao 10 não instala; menos de 8 GB de memória ou Windows para ARM (roda por emulação, bem mais lento — o caso do Parallels) avisa e pergunta. Na atualização apaga o `.pck` e a pasta de dados do .NET da versão de antes do iniciador, só elas. Testado aqui no Wine (iniciador com um jogo de mentira: ordem, guardado, Opções, repasse de argumentos e a mensagem; instalador de 64 bits de teste: atualização e desinstalação) e, a cada push, no Windows do GitHub (`instalador/teste/testar_iniciador.ps1`, job `iniciador` do `testes.yml`). **Não testado num Windows de verdade com o jogo exportado.**

- **Lançamento: o Mac não importa mais as texturas sozinho** (Jhon, 21/11: o job do Mac foi cancelado em `[47%] reimport | T_Scar_D.png`). O import do Godot comprime ~2,5 GB de texturas (BPTC de alta qualidade, 2048 px por arma). No runner não há GPU e o Godot comprime na CPU; no Mac (ARM, 3 núcleos) passava dos 50 min do job, e minuto de Mac custa 10 vezes o de Linux. Agora um job `importar`, no Linux, importa uma vez, partindo do cache da execução anterior (só o que mudou é refeito), e guarda `.godot/imported` no cache do Actions; o Windows e o Mac restauram e só exportam. Se o cache não chegar ao Mac, ele avisa e importa como antes. Na primeira execução o cache chegou ao Mac (scan em segundos), e a exportação parou em outra coisa: o Mac universal (Intel + Apple Silicon) exige as texturas também em ETC2/ASTC. O projeto passou a importar os dois formatos (`import_etc2_astc`): as 744 texturas comprimidas ganham a cópia ETC2 (as 121 de alta qualidade, ASTC 4×4). O Windows continua levando só a BPTC; o Mac leva as duas.

- **Lançamento: a máquina de partida ligada troca de imagem, e um 502 do Fly não derruba mais o job** (Jhon, 21/11: o job `partida` falhou depois de 8 min). Eram dois problemas. A reserva da frota (`ALCATEIA_FLEET_RESERVE = 1`) fica sempre ligada e nunca desliga sozinha: o `frota.py atualizar` esperava ela desligar por 20 min e a deixava na imagem antiga — era nela que se jogava. Agora, com `--saude` (o `/health` do servidor, que o pipeline passa), se não há partida em curso na frota a ligada troca na hora e reinicia na imagem nova; com partida, espera como antes. E qualquer erro da API de máquinas encerrava o script: um 502 no meio da espera derrubou o lançamento. 429/5xx e falta de resposta agora são repetidos (3, 6, 12, 24 s). A saída do script sai linha a linha (antes o erro aparecia no topo do log, antes das linhas que vieram primeiro). Conferido contra uma API de mentira: com o 502 no primeiro pedido, sem partida troca as duas, com partida espera e avisa.

- **A vida volta de novo na partida online** (Jhon, 21/11: "vida não tá se recuperando"). O jogo e o servidor de partida regeneram juntos, mas o jogo começa meia ida e volta depois e o que ele conta (Status, 10/s) chega ao servidor mais meia ida e volta depois. O `LowerTo` não tinha folga: essa diferença virava "dano que só o jogo viu", zerava a espera de 15 s do servidor, e o Vitals (2/s) puxava o jogo de volta. A vida ficava parada. Agora, regenerando, a diferença de até 0,5 s de volta (`Health.RegenLag`; 9 pontos num lobo de 200) não é dano, nem no servidor (`LowerTo`) nem no jogo (`CorrectDown`). Fora da regeneração, nada muda. Numa simulação do protocolo (100 ms), de 57/200 a vida ia a 61 em 40 s; agora enche. **C# não compilado.**
- **Instalador do Windows: o `makensis` achava o ícone em `instalador/instalador/`**. Ele entra na pasta do `.nsi` antes de compilar, e a arte vem de `${__FILEDIR__}`, que saía relativo. O pipeline passa o `.nsi` com caminho absoluto. Conferido aqui com o NSIS 3: relativo dá o mesmo erro do Actions, absoluto gera o `.exe`.
- **Lobo: o bote no ar só sai a partir de 1,1 m do chão** (Jhon, 21/11: "preciso atingir uma altura mínima antes, não muito alto, mas o suficiente para não ser praticamente instantâneo"). `Weapon.LeapMinHeight` (1,1 m, pelo raio dos pés para baixo). O botão direito apertado antes (junto com o pulo ou logo depois) fica guardado e o bote sai sozinho ao chegar lá; pousou antes, cai fora. Num pulo do chão plano: uns 0,25 s depois do pulo no Bruto (que sobe até 1,6 m), 0,18 s no Ligeiro. O bot tenta até pousar. Substitui o "bote no primeiro passo no ar" de 20/11; `LoboResponde` passou a conferir a altura.
- **Lobo: andar também solta a aterrissagem do bote** (Jhon, 21/11: "W W, pula, secundário com o W apertado; ao cair, o lobo corre no mesmo lugar travado, depois corre normal"). Só o pulo e a corrida de quatro cortavam o levantar; qualquer tecla de andar corta agora, e quem entrou no ar correndo de quatro volta correndo, mesmo se soltou o W um instante no ar. `LoboResponde` ganhou "depois do bote, só W". **O vídeo do Jhon mostrou o resto**: as pernas correndo (a animação vem da velocidade do corpo) e a posição parada por ~0,9 s, a aterrissagem inteira. É a partida autoritativa: o jogo corta, o servidor de partida (imagem montada antes do corte, de 21/11 01:09) segura o lobo, e a reconciliação puxa o jogo de volta a cada estado. Precisa do servidor de partida remontado com o mesmo código do jogo. A altura do bote mede só até o cenário: um corpo embaixo o jogo vê no passado e o servidor no presente, e os dois dariam o bote em passos diferentes.
- **Ícone novo: o lobo de frente, silhueta simples** (Jhon, 21/11). `icon.svg`, `instalador/icone.svg`, `WolfLeague.ico`, cabeçalho e capa do instalador refeitos; `assets/ui/icones/wolf.png` (silhueta branca) no lugar do lobo desenhado em código nas artes de classe (`WeaponArt.WolfHead`, `Person`). Kit de identidade v2 (ícones, símbolo, logos, fundos do menu e geométrico em vários tamanhos) entregue fora do repositório.
- **Sala: a borda do painel do jogador alinhada com os nomes, e a sua foto aparece mesmo com o servidor antigo** (Jhon, 21/11). As fotos dos outros só chegam com o servidor novo (`profile.photo`).

- **Lançamento sem os testes automatizados, por enquanto** (Jhon, 21/11). A tag `v*` sobe servidor, frota, Windows e Mac sem esperar os testes; o `testes.yml` continua rodando a cada push em ramo. Como religar está no comentário do `lancamento.yml`.
- **Morto, o feed de abates continua na tela** (Jhon, 21/11: "quando eu morrer, o feed de kills deve continuar aparecendo, mesmo no modo espectador"). O feed de quem joga mora numa camada própria, acima da escolha de conjunto: antes ia junto com o HUD e sumia na morte e nos 15 s da escolha. O HUD do aliado assistido não repete o feed; a linha do aliado assistido ganha a borda dourada, como a sua. Teste novo `FeedMorto`; `TelaDoAliado` confere o feed no espectador.
- **Sniper sem luneta: o tiro cai dentro da cruz** (Jhon, 21/11: "o tiro da sniper não está coincidindo com o crosshair sem escopo"). A abertura da cruz era o espalhamento × pixels por grau, uma conta que só vale perto do centro: em espalhamento largo (a SVD sem luneta) a cruz ficava com metade do tamanho de onde as balas iam. Agora sai da projeção da câmera (`Crosshair.Opening`). Teste novo `TiroNaCruz` (SVD, AK-74 e Desert Eagle contra a parede: o tiro mais aberto da SVD foi a 87 px do centro, a cruz abre 87 px; antes, 41).
- **Mapas, itens, personagens, armas e patentes no idioma do jogo** (Jhon, 21/11). Nome, tipo e descrição do catálogo (`CatalogItem`), nome, descrição e tamanho dos mapas (`MapInfo`), nomes das armas (`Arsenal.NameOf`) e das patentes (`Ranks`) saem por `Loc`; o `tools/i18n/extrair.py` passou a ler também os `.tres` de `resources/` e de `assets/personagens/`. `textos.csv` com inglês e espanhol para tudo (eram 114 sem). O `Idiomas` confere o Distrito Neon, uma arma, uma granada, um personagem e uma patente em inglês.
- **Lobo: o bote corta o pulo quase seco** (Jhon, 21/11: "ao pular e atacar, a resposta do ataque secundário deve ter menos delay e quase cancelar a animação"). A mistura do bote por cima do pulo caiu de 0,08 s para 0,03 s (dois quadros): num bote curto, metade do golpe saía ainda com cara de pulo. `LoboResponde` confere o golpe tomando o corpo já no passo do clique.
- **Lobo: W W, pulo e bote não prendem mais o lobo correndo no lugar** (Jhon, 21/11: "ele anima e depois buga, fica andando no mesmo lugar"). O prazo em que a corrida corta a aterrissagem do bote contava no relógio de parede: um engasgo no impacto (o efeito do bote compilando na primeira vez) passava do prazo antes do passo seguinte, a corrida não cortava e o lobo ficava ~1 s preso com o W apertado. Agora conta em tempo de jogo, como o resto do golpe. `LoboResponde` simula um engasgo de 1,2 s: sai correndo em 2 passos (com o relógio antigo, 56).
- **Especial: a trava vê o inimigo atrás de poste e caixote baixo** (Jhon, 21/11: "revise o especial, parece que ele não tá indo no oponente", no Distrito Neon contra bots). Só o centro do corpo contava para a trava: atrás do poste de uma luminária de chão ou de um caixote na cintura, com o inimigo quase todo à vista, o modo alvo não travava e o salto ia para onde a mira batia no cenário (às vezes 1 m, no pé do obstáculo). Agora contam o centro, o peito, a cabeça e os ombros. Com a trava, no Distrito Neon contra bots, o salto cai a 0,6–1,6 m do alvo e a animação pousa no quadro em que o corpo pousa. Teste novo `TravaAtrasDoPoste` (poste e caixote travam, parede não).

- **Som do "em chamas" — feito e desligado no mesmo dia** (Jhon, ao ouvir: "tire esse som por enquanto, não ficou bom"). O HUD não toca mais nada; os arquivos foram para `_to_delete/`, o gerador e o `FireSound` ficaram para outra tentativa. Como era: só para quem está em chamas. Ao pegar fogo, `chamas_acender` e o mundo abafa um instante; enquanto arde, `chamas_laco` acompanha o calor e os sons do mundo ganham grave (`Sfx.SetFire`, prateleira no barramento Mundo); abate ou assistência em chamas soma `chamas_explosao`; ao apagar, `chamas_apagar`. Sons gerados por `tools/sons/chamas.py`; quem toca é `scripts/visual/FireSound.cs`, pelo HUD. Ninguém ouviu: as medidas estão em `docs/AUDIO.md`.
- **Retrato no lugar certo de cada tela** (Jhon, 21/11: "pequeno e sem espaço"). Cabeçalho: só a foto quadrada (`PhotoTile`, 48 px, filete na cor do metal), o nível no chip ao lado. Perfil: retrato com borda de 240 px. Sala: foto quadrada e o nível em estrelas ("32 ★") na frente do nome. Ranking e placar do resultado: foto ou chip, nada de borda de 20 px. TAB maior (cartões de 204 px, retrato de 124, números de 26, nome do time de 34; bot com o ícone no lugar do retrato). HUD: o hexágono ao lado da vida cresceu (84 × 96, numa caixa de 156 × 138 para a borda caber) e mostra a foto de perfil; o ícone de humano/lobo só aparece sem foto (a forma continua escrita embaixo da vida). Textos novos traduzidos (en, es), e nível e metal passam pelo `Loc`.
- **Chama de verdade, TAB novo com blur, brilho nas bordas** (Jhon, 21/11: "é uma chama mesmo, pegando fogo"; "mude a tab para ter outro estilo, capriche, coloque um blur"; "brilhos VFX nas bordas de ouro, prata etc., nos níveis mais altos"). `FireAura` virou um shader de fogo azul (ruído subindo, três labaredas, faíscas, clarão), em duas camadas (atrás do retrato e as pontas por cima). `FrameShine`: reflexo que varre o metal e estrelinhas que piscam, com força por `Ranks.Shine` (nada no Iniciante e no começo do Bronze, máximo no Mestre), no retrato e na insígnia do HUD; a placa do número virou `LevelPlate`, por cima do brilho. `TabBoard` refeito: mapa borrado atrás (`HudStyle.Backdrop`), entrada em 0,12 s, faixas por time com marca, nome, fio e placar, cartões facetados com filete do time, retrato, estado (EM CHAMAS / SEQUÊNCIA ×N / ABATIDO), forma, nome, conexão e abates/assistências/mortes; o seu em ouro, quem está em chamas com borda azul acesa. Os dois shaders foram vistos rodando num Godot 4.7 fora do jogo; o C# não foi compilado.
- **Rage no anti-cheat** (Jhon, 21/11: "já conecte com o anti-cheating e manda para o servidor"). O servidor de partida manda o rage de cada um aos jogos duas vezes por segundo (pacote `Fire`, 24): o HUD e o TAB mostram o que ele conta, não o que a máquina de cada um acha. O servidor central só paga o tempo em chamas que os abates e assistências permitem (`Rage.MaxFireSeconds`: 15 s por abate, 12,5 s por assistência, zero sem 200 de calor) e marca na ficha do jogador, como evento de segurança, o resultado que passar disso ou da duração da partida (`Plausibility`).
- **Fotos de perfil à venda e visíveis para todos**: as 10 da loja viraram itens `foto_<id>` (tipo `photo`, só permanentes; aba "Fotos de perfil" na loja, aba Conta no inventário, `jogo.json` regenerado, painel copia `assets/ui/avatares`). O jogo manda a foto escolhida ao saguão (`profile.photo`); o servidor só aceita grátis ou comprada (`PhotoRule`, lê os itens ao entrar e de novo se pedirem uma que não estava) e põe `photo` em cada membro da sala, que vai para a escalação da partida: a sala e o placar mostram a foto de cada um.
- **Rage (em chamas)** (Jhon, 21/11): `scripts/domain/Rage.cs`. Abate +100 de calor, assistência +50, teto 300; acima de 200 pega fogo; 10 s sem abate nem assistência e esfria 20/s; abaixo de 100 apaga. Cada 6 s em chamas = 1 XP no fim, até 40 (`Rewards.FireXp`, linha "Em chamas" no resultado). O servidor de partida conta (`Referee`) e manda `fire` no resultado; o servidor central paga. Testes em `tests/unit/RageTests.cs`.
- **TAB no estilo do Overwatch** (`TabBoard`): inimigos em cima, seu time embaixo, "VS" no meio; cada jogador com retrato (foto + borda + nível e estrelas), nome, abates · assistências · mortes e forma; seus números na base (inclusive tempo em chamas). Quem está em chamas ganha fogo azul animado em volta do retrato (`FireAura`), e o mesmo fogo acende na insígnia do HUD. A tela de resultado continua com a tabela.
- **Patente virou nível, com borda no estilo do Overwatch** (Jhon, 21/11: "ao invés de patentes, será níveis; cada nível tem uma borda"; "melhore todas as bordas, use o Overwatch como inspiração"). Níveis de 1 a 3000 sobre o mesmo XP da conta (nada migra: `Ranks`/`RankRule` mudaram a regra, o "rank" é o nível − 1). Do nível L ao L+1: 100 + 10 × (L−1) XP, até 1.000 por nível a partir do 91; o 100 sai em umas 600 partidas. Metais: Iniciante (1–99), Bronze, Prata, Ouro, Platina, Diamante (500 níveis cada, uma estrela a cada 100) e Mestre (2600+). A placa mostra o nível sem as centenas: o 520 é "20 ★★★★★" em bronze (`LevelChip`, no cabeçalho e no perfil). 70 bordas (7 metais × 10 desenhos, um a cada 10 níveis: aro, aro duplo, divisa, asas, penas, joia, louros, coroa) em `assets/ui/bordas/`, geradas por `tools/bordas.py` (metal polido por mapa de altura). `RankBadge` agora é foto de perfil + borda + placa; a borda também vai em volta da insígnia humano/lobo do HUD. A tela de promoção só abre a cada borda ou estrela nova; Presas: 20 por nível, 100+ por borda, 500+ por estrela. O filtro de nível da sala anda em degraus. `minRank` dos itens passou a ser lido como nível − 1 (o item com `minRank` 9 libera no nível 10). As imagens antigas de patente (`assets/ui/patentes/`) ficaram sem uso.
- **Fotos de perfil** (Jhon, 21/11, "igual League of Legends"): 3 grátis (Eve, Barut, a fera do Barut, recortadas da arte dos personagens) e 10 da loja (armas e granadas raras), em `assets/ui/avatares/`, `scripts/meta/Avatars.cs`; escolha no perfil. **Falta:** vender as 10 no catálogo do servidor e mandar a foto pelo saguão para os outros verem; por enquanto a escolha é local e as da loja aparecem com cadeado.
- **WP máximo na partida: 10.000** (era 2.500; Jhon, 21/11).
- **Kit de identidade visual**: o lobo (`icon.svg`), logos horizontal/vertical/fundo claro e paleta, em PNG sem fundo (`instalador/gerar_arte.py` faz a arte do instalador).
- **Lançamento com instalador e Mac** (Jhon, 20/11: "o pipeline deve gerar o release tmb e deve ter o instalador do jogo"; "deve gerar o jogo de mac e windows"; o instalador "com cores do jogo", a Eve de capa). Tag `v*` agora publica a Release só depois de servidor, frota, Windows e Mac prontos, com `WolfLeague-Setup-<versão>.exe` (NSIS, `instalador/windows.nsi`: azul-noite e ouro do menu, a Eve na boas-vindas, o Barut no fim, instala sem administrador em `%LOCALAPPDATA%\Programs`, atalhos, desinstalador que só apaga o que trouxe), o `.zip` portátil, `WolfLeague-<versão>-mac.dmg` (exportado num Mac do GitHub, universal, assinatura ad hoc) e `SHA256SUMS.txt`; a capa (`instalador/capa.png`) abre as notas. `tools/lancamento/preparar_build.py` grava no build o endereço de produção (`https://<FLY_APP_API>.fly.dev` ou a variável `SERVIDOR_URL`) e a versão da tag. O ícone do jogo (`icon.svg`) virou o lobo, o mesmo do instalador; a arte sai de `instalador/gerar_arte.py`. `primeira_vez.sh` também configura o GitHub (segredo `FLY_API_TOKEN`, ambiente `producao`) se o `gh` estiver logado. O `.exe` do instalador compilou aqui com um jogo de mentira; **o pipeline nunca rodou.**
- **Banco da produção: Postgres 16 num app do Fly, não o `fly postgres`** (Jhon, 20/11: "me ensine passo a passo como montar o servidor no fly, banco de dados e etc"). O Postgres "automatizado" do Fly (`fly postgres create`) está descontinuado e sem suporte, e o gerenciado (a partir de 38 dólares por mês) liga SSL por padrão, que o `Pg.cs` ainda não fala. Passou a ser a imagem oficial `postgres:16` num volume de 10 GB (`fly.banco.toml`), na rede privada (`wolfleague-db.internal`), com snapshots do volume por 30 dias. `primeira_vez.sh` e `docs/FLY.md` refeitos. O token do Fly agora vai sempre depois do `Bearer` (`FlyMachines.cs`, `frota.py`), como a documentação do Fly manda. **Nada rodado.**
- **Pronto para o alfa** (Jhon, 20/11: "quero subir o servidor para produção e começar os testes alpha"). O jogo exportado fala com o servidor de produção (`project.godot` → `wolfleague/rede/servidor`, `https://wolfleague-api.fly.dev`); no editor e nos testes continua o local (`Backend.DefaultUrl`). `export_presets.cfg` novo (Windows e macOS). O lançamento ganhou o job `cliente`: exporta o Windows e publica o `.zip` numa Release do GitHub (tag com hífen = pré-lançamento). `tools/fly/primeira_vez.sh` monta a produção no Fly de uma vez (apps, banco, segredos, servidor, IPv4, frota). Em produção o painel exige a segunda etapa (`ALCATEIA_ADMIN_REQUIRE_2FA = 1`). **Nada rodado aqui.**
- **A granada volta a ferir — inimigo, aliado com fogo amigo e quem lançou** (Jhon, 20/11: "a granada não está dando dano em área nos inimigos e nem em mim; nunca matei alguém com granada"). A explosão perguntava à física quem estava no raio de 9 m e ficava com as 32 primeiras respostas; nos mapas de jogo, feitos de centenas de peças (a Obra tem 498 corpos, o Armazém Estendido 1.040), essas 32 eram paredes, degraus e caixas, e o jogador ficava de fora. No campo de treino, quase vazio, funcionava — por isso o teste `Granada` passava. Agora jogadores e bots vêm direto da lista de quem luta (como as garras do lobo já faziam), e a física só procura o resto que tem vida. **Som de acerto**: ferir inimigo com a granada dá o mesmo retorno do tiro — o som, a marca na mira e a carga do especial (`Weapon.ConfirmHit`; "tem que dar o barulho de dano igual da arma"). Ferir a si mesmo não dá. Teste novo `GranadaFere`, na Obra. **C# não compilado nem rodado aqui.**
- **Fotos da loja da faca e das granadas refeitas** (Jhon, 20/11: "não estão nas thumbs corretamente"). As três facas e as cinco granadas (a comum e as quatro versões, que não tinham foto) saíram da forma nova. A pose mudou no `WeaponPhotos.cs`: a faca com a face da lâmina para a câmera e a ponta subindo para a direita (de perfil ela ficava em pé, um risco), a granada girada 120° (de frente, a alavanca tapava o corpo). `tools/fotos_armas.gd` (novo) faz o mesmo em GDScript, sem compilar o C# — foi com ele que estas saíram. E a semente do painel chamava a granada de `granada`, mas no jogo ela é `choque`: o `exportar_dados.py` agora lê os ids do `Arsenal.cs`. Se o seu banco local já tinha o item `granada`, ele fica órfão no painel (pode desligar).
- **Produção no Fly.io, com lançamento pelo GitHub Actions e servidores de partida sob demanda** (Jhon, 20/11: "crie este pipeline para github actions"; "vou hospedar no fly"; "me ensine a montar a infra no fly para criar servidores sob demanda"). Guia completo em `docs/FLY.md`. **Lançamento** (`.github/workflows/lancamento.yml`): tag `v*` → testes → deploy do servidor (`fly.servidor.toml`) e espera o `/health` → imagem do servidor de partida (`fly.partida.toml`, só build) → troca nas máquinas (as paradas na hora, as ligadas quando desligarem, até 20 min). Os **testes** ganharam a checagem da semente do painel (falha se alguém pôs item no jogo e não rodou o `exportar_dados.py`; avisa item de loja sem foto) e podem ser chamados por outro workflow. **Frota sob demanda**: as máquinas de partida nascem paradas (`tools/fly/frota.py`), cada uma com a sua porta UDP; o servidor liga uma pela API de máquinas quando os lugares livres ficam abaixo de `ALCATEIA_FLEET_RESERVE` ou uma sala não acha lugar (`Infra/Fly/FlyMachines.cs`, `IFleetScaler`); a máquina ociosa por `ALCATEIA_IDLE_EXIT` pede para sair (`POST /v1/fleet/leave`, `Fleet.Retire`: não sai se tiver partida ou for o último lugar da reserva) e encerra. Para o Fly: o servidor de partida abre a porta em `ALCATEIA_BIND` (`fly-global-services`) e usa o id da máquina; o servidor lê o `Fly-Client-IP` (só vindo de proxy de confiança) e o `Fly-Forwarded-Proto`. `.dockerignore` novo (o contexto ia com `.godot` e `.synctmp`). Testes novos: o `Fly-Client-IP` e a frota que desliga só quando não faz falta. **Nada disto rodou: C# não compilado, nenhum deploy feito.**
- **Granada redonda, com quatro versões** (Jhon, 20/11: "deixe ela mais redonda, tá muito quadrada, e cria variantes; granada pode ser equipada qualquer uma no padrão"). A granada deixou de ser a malha de 12 gomos do pacote: é torneada (`tools/armas/formas_mao.py`) — bola de 40 gomos, colar e espoleta, alavanca de chapa dobrada, pino com olhal e argola de arame —, no mesmo lugar e tamanho (as animações não mudam). Texturas novas pelo `peles.py` (verde-oliva com faixa amarela, tinta lascada, espoleta torneada). Versões, só de aparência: **Carbono** (rara), **Neon** e **Magma** (épicas), **Ouro** (lendária) — `Arsenal.Grenades`/`IsGrenade`, itens no catálogo com `DefaultOk` (vão no conjunto padrão), a da mão e a que voa são a do conjunto (`Weapon.GrenadeModelId`, `Grenade.ModelId`). Online, a granada dos outros ainda aparece como a comum. **C# não compilado nem testado aqui; faltam as fotos da loja (`WeaponPhotos`).**
- **Faca mais afiada e menos quadrada, com mais brilho** (Jhon, 20/11). Forma nova (`formas_mao.py`): lâmina com dorso, face, desbaste largo até o fio de espessura zero, ponta caída com falso fio; guarda e cabo ovais, pomo arredondado. A face é aço acetinado escuro com o sulco; o desbaste é polido como espelho e o fio tem uma linha de luz que não apaga na sombra. Na mão de primeira pessoa a faca vai rolada 90° em volta do cabo (`GIRO_NA_MAO`): a câmera olhava a faca pelo dorso e só via um risco. Damasco e Obsidiana refeitas na forma nova. A faca comum passou a ter textura (era cor por peça).
- **Explosão da granada refeita: luz e forma, poucas partículas** (Jhon, 20/11: "explosão mais luminosa, não precisa de tanta partícula"). `Explosion.cs`: uma luz forte que acende o lugar e faz sombra, o clarão, a bola de fogo e a fumaça como esferas de material (`assets/efeitos/explosao_*.gdshader`), a onda de choque e a mancha de calor no chão, e 28 fagulhas (eram quase 200 partículas simuladas). Visual conferido num protótipo em GDScript com os mesmos tempos; **o C# não foi compilado**.
- `tools/armas/conjuntos.py` agora chama `peles.reaplicar` no fim: o conjunto refeito recebe de volta a malha nova, o UV e o material (e as peles ganham as animações novas), sem redesenhar textura.
- **O jogo abre no login** (20/11, pedido do Jhon). Toda abertura passa pela tela de login (`LoginScreen`, no lugar da antiga tela de nickname): à esquerda, as **contas salvas** neste computador — o acesso fica salvo, mas **só entra com o clique na conta** (o login guardado é conferido no servidor; se venceu, a conta sai da lista e a tela pede a senha) e cada uma tem o "esquecer"; à direita, as abas **Entrar** e **Criar conta** (nome, senha, repetir a senha, servidor) e "Lembrar acesso neste computador". Sair da conta, sessão vencida ou conta derrubada voltam ao login. O **"Recomeçar perfil" das opções saiu** (a carteira e os itens são da conta, decisão 0015). O treino com bots segue sem conta, num botão discreto embaixo do login. O `online.cfg` antigo (um login que entrava sozinho) vira uma conta salva. Teste novo `Login`: com servidor no ar, cria a conta pela tela, "fecha o jogo", entra clicando na conta, erra a senha e esquece o acesso.

- **O lobo responde na hora** (20/11, pedido do Jhon: "é jogo rápido; a resposta do lobo tem que ser precisa"). **Um corta o outro**: depois que o bote encosta (ou o salto cai), o pulo e a corrida de quatro (W W, ou o W ainda segurado na corrida) soltam o lobo da aterrissagem no mesmo passo — antes ele ficava preso 0,87 s e o espaço apertado nesse tempo se perdia. **Tecla um pouco cedo fica guardada**: espaço até 0,15 s antes de pousar pula ao pousar, e botão direito junto com o pulo (ou um instante antes) vira bote no primeiro passo no ar. A aterrissagem do pulo anterior e a do bote saem da tela na hora em que o lobo pula ou corre. Teste novo `LoboResponde` (em passos de 1/60 s): W W 2, pulo 2, bote no ar 2, pulo depois do bote 2 (antes, nunca), W W depois do bote 5 (antes, 52), bote correndo e pousando já correndo 4 (antes, 56).

- **Obra: o que está no andar de cima não aparece mais no teto do de baixo** (Jhon, 20/11). A laje era uma caixa sem a face de baixo: de baixo se via o verso da face de cima, na mesma altura da base dos tijolos, caixotes e muretas de cima, e os dois piscavam. A laje ganhou fundo (`Kit.chapa(..., fundo=True)`, só na Obra).
- **Armas com textura nova** (Jhon, 20/11: "melhore o gráfico das armas, deixe as texturas menos pixeladas possível, principalmente as skins brilhantes, como a dourada e a diamante"; "parecidas com as do Overwatch e do Valorant"). O `tools/armas/peles.py` foi reescrito e passou a fazer também as seis armas de fábrica (AR-15, M4A4, AK-74, SCAR-H, SVD, Desert Eagle — eram cor chapada por face) e as cinco peles do AR-15 (Ultimate, Ouro, Diamante, 8 Bit, Lava — eram do Blender, com atlas picado e 60% da resolução). Cada arma sai com quatro texturas de 2048 px desenhadas a 4096 e reduzidas (`_D` cor, `_E` emissão, `_ORM` oclusão/rugosidade/metal, `_N` relevo): normal lisa onde a dobra é suave, quina chanfrada e canto fundo no relevo, desgaste na quina, material por material nas de fábrica (polímero, aço oxidado, aço escovado, madeira com veio, baquelite). Ouro: ouro de joalheria com arabesco gravado dentro de moldura, ônix com friso. Diamante: facetas lapidadas com relevo, safira no que era preto. A Galáxia ficou como era, com o relevo novo. As texturas saem do `.glb` (a arma na mão e a do mundo usam o mesmo arquivo), importadas em BPTC de alta qualidade com mipmap. `WeaponModel.cs` passou a ler `_ORM` e `_N`. As texturas antigas embutidas (49 MB) foram para `.synctmp/armas_fp_antigas/` — pode apagar. **Não rodado aqui:** o jogo em C# e o `tools/testar.sh`; as fotos da loja (`WeaponPhotos`) precisam ser tiradas de novo. (A faca e a granada entraram depois, na mesma noite.)
- **Mãos de primeira pessoa desenhadas, com bracelete de LED do time** (Jhon, 20/11). `tools/armas/maos.py` (novo; o `maos_lowpoly.py` fica de registro): a malha inteira e lisa (7.240 triângulos), manga de tecido com trama e dobras, luva de couro com costura, pele limpa — cor, relevo e rugosidade em 2048 px — e um bracelete de metal em cada pulso com um filete de LED (o esquerdo tem o módulo com tela). O LED acende na cor do time: azul na Vanguarda, vermelho no Sindicato (`ViewModel.PaintBracelet`, parâmetro `led` do `maos.gdshader`).
- **Distrito Neon, mapa novo** (Jhon, 20/11: o pacote "Low Poly Sci-Fi City"). A cena de demonstração do pacote, lida do arquivo do Unity (`tools/mapas/kits/unity_kit.py`) e posta duas vezes — a cópia girada 180°, com a rua oeste das duas virando a avenida do meio: 124 × 100 m, de noite, com os LEDs acesos de verdade (`scifi_luz.png` no material de detalhe) e 66 lâmpadas na cor dos letreiros; ao fundo, torres de até 105 m e o painel de arranha-céus com letreiros. `tools/mapas/neon.py`, `docs/MAPAS.md`. Testes novos `MapaNeon` e `EscadaNeon` — **escritos, não rodados**.
- **Obra**: montes de areia e de brita (sobe-se neles), barro pisado em volta do prédio com tábuas jogadas por cima, poças de água no barro e no pátio.
- **Som ambiente por mapa** (Jhon, 20/11): Cidade (trânsito ao longe, pássaros, buzinas), Deserto (vento seco e pássaros, sem carro), Estação (cidade, trilhos, pássaros; um trem ao longe de vez em quando), Obra (canteiro, vento, pássaros; cachorro), Neon (cidade abafada, zumbido dos letreiros, carros voadores), e pássaros de vez em quando no Armazém. Gravações do Oculus Audio Pack em camadas, 60 s em laço: `tools/sons/ambiente_mapas.py`. **Não ouvido por quem fez** (só medido: volume, espectro e a emenda do laço).
- **Capas dos mapas com cara de arte de jogo de herói** (Jhon, 20/11: "aspecto Valorant e Overwatch, o leve blur embaixo e nos cantos"): cor viva, sombra fria e claro quente, desfoque leve na faixa de baixo e nos cantos, vinheta fraca; a da Cidade passou para o nível da rua. Sai o "clima de guerra" (`tools/fotos_mapas.gd`).
- **Gráfico Baixo, Médio, Alto e Ultra com a mesma luz** (pedido do Jhon: "a luz padrão em todos, não pode ficar mais clara"). Antes a Baixa desligava a oclusão e a Média a luz rebatida, e o mapa clareava: no Armazém, +40% de luz na Baixa e +47% na Média. Agora todo efeito que mexe na luz (oclusão, luz que rebate na tela, luz rebatida do mapa, névoa, brilho) fica ligado nas quatro, como o mapa pede; a qualidade muda só o custo (sombra, amostras, resolução, raios). O reflexo na tela da Ultra saiu (clareava o chão). Medido em foto Forward+ de Armazém, Obra e Vila: as quatro ficam a ±1,2% da Alta. A Baixa ficou mais pesada que antes (a oclusão e, no Armazém, a luz rebatida agora rodam nela, mais baratas). `Settings.ApplyQuality`.
- **Marca de tiro**: cada bala que bate no mapa (parede, chão, carro, caixa) deixa um furo com a lasca em volta, que fica 10 s e some em 2,5 s. Decal projetado (deita na superfície, não boia), até 96 marcas ao mesmo tempo; nada em gente nem no limite invisível. `scripts/visual/fx/BulletMarks.cs`, texturas de `tools/efeitos/marca_tiro.py`.
- **Tiro de cima sem dano (corrigido)**: de cima dos telhados, o tiro no oponente lá embaixo batia na caixa de colisão invisível do próprio prédio — ela pegava a sacada, a escada de incêndio e a marquise e passava da fachada. Na Cidade os prédios e os carros, na Estação os vagões, o galpão, a guarita e a torre do relógio, no Deserto os caixotes e na Obra o toco de coluna com a ferragem e a torre da grua agora têm a colisão da própria malha. As caixas dos postes da Cidade e da Estação ficavam 1,1 m ao lado do poste, no ar, e agora vão nele, mais finas. Teste novo `TiroDeCima` nos quatro mapas: de 22 a 29 telhados por mapa, todos com dano e nenhum tiro parado em colisão que não se vê.
- **Cidade sem o posto de gasolina**: cada time nasce num largo na ponta da avenida, com o carro, a barreira e os caixotes.
- **Vida e acerto no servidor de partida** (20/11, decisão 0016; a segunda leva da 0011). Na partida autoritativa (`ALCATEIA_NETCODE=autoritativo`) a vida de cada um é do servidor: o jogo só pode contar que tem menos (uma queda, a própria granada), nunca mais. Cada acerto é conferido lá antes de valer — de onde saiu, a arma que estava na mão, o alcance, **a parede no caminho voltando o alvo no tempo** até onde ele aparecia na tela de quem atirou (até 0,3 s), a mira, o dano máximo da arma (cabeça só se a cabeça estava à vista) e o ritmo (o dano por segundo que a arma dá) — e, valendo, o dano é aplicado no servidor, a vítima recebe como ficou e **a morte é decidida lá**. Renascer só passada a espera e na base do time. Acerto recusado conta contra quem atirou e vai no resultado (`rejectedHits`); com 15 ou mais numa partida, alerta na ficha do jogador no painel. **Abates como lobo** vão no resultado (`killsWolf`), em todos os modos: os desafios "como lobo" e "como humano" passam a andar. Teste novo `VidaNoServidor` (acerto através da parede, de 40 m acima do corpo, com outra arma, de costas, 900 de dano, 40 tiros de uma vez, vida "cheia" contada pelo jogo, renascer cedo e fora da base: tudo recusado); o ponta a ponta autoritativo agora tenta trapacear e confere que só os acertos inventados são recusados. No modo de repasse (o padrão) nada muda. Ainda vêm do jogo: a forma, a arma na mão, as armas do conjunto e os bots.

- **Loja, inventário e missões pela conta** (20/11, decisão 0015; Jhon: "tudo exige conta"). XP, patente, Presas, Sangue, inventário, missões e títulos agora são da conta no servidor e o jogo só os espelha (`Profile.ApplyAccount`); o `perfil.json` guarda só escolhas (conjuntos, raça, tatuagens, personagens, título em uso). Um caminho só entre o jogo e a conta, `scripts/net/AccountSync.cs`: retomar o login ao abrir, comprar, descartar, resgatar missão e trocar de nome passam pelo servidor e relêem a conta. **Sem conta**: Filhote, zero Presas, equipamento de série; a loja mostra a vitrine e pede para entrar, as missões também (`AccountGate`); o conjunto guardado não se desfaz, e na partida entra a arma de série no lugar do que não é seu (`Profile.Usable`). **Partida sem servidor é treino** e não rende; online com conta, a tela de resultado mostra a previsão e quem paga é o servidor. Preço, raridade, patente mínima, promoção e a vitrine do dia vêm do servidor (`Catalog.ApplyServer`). **No servidor**: rodízio das missões (3 diárias e 5 semanais sorteadas no dia de Brasília, as mesmas para todos; configuração `desafios`), vitrine do dia (4 itens a 20%; configuração `vitrine`), prêmio de promoção de patente pago com o XP, títulos, `DELETE /v1/inventory/{id}`, `POST /v1/account/name` (gasta a troca de nome). No painel: as duas configurações novas, "Valendo hoje / esta semana" nos desafios e "Item do dia" no catálogo. Testes: `ContaNoPerfil` (novo) e o ponta a ponta online (`tools/testar_online.sh`) agora confere XP e Presas pagos, missões andando, uma compra aceita e uma recusada pela patente; servidor 54 de 55 (a de sempre, SCRAM, falha só na nuvem). Fica para a próxima: o item expirado some do inventário na hora (antes ficava 30 dias à mostra) e as estatísticas da tela de perfil ainda são as locais.

- **Servidor e painel: só o modo que existe** (20/11). O painel mostrava "Equipe e Mata-mata" em todo mapa, e o mata-mata ainda não existe. O servidor passou a saber quais modos estão liberados (`RoomSettings.ModeAvailable`, pela semente) e o cartão do mapa só anuncia esses; a Obra, feita só para mata-mata, diz isso e que por enquanto joga em Equipe. Modos e foto de cada mapa e a arte dos itens acompanham o jogo a cada subida do servidor, mesmo em banco já povoado.

- **Capas da Cidade, do Deserto, da Estação e da Obra refeitas** em cima dos mapas como ficaram depois da junção das duas entregas de 20/11 (a luz nova, os túneis da Estação, o muro de obra), pelo mesmo `tools/fotos_mapas.gd` (clima de guerra). A da Obra e a da Estação com o ponto de vista novo.
- **Luz dos mapas de kit refeita (Cidade, Cidade do Deserto, Estação de Trem, Obra)**, na linha dos jogos de referência do Jhon (Overwatch, Valorant): sol quente e forte, sombra fria e clara (nunca preta: beco, sala e vão de laje ficam legíveis), a luz que volta do chão por baixo, oclusão larga + SSIL, ACES com branco 6,5, névoa com perspectiva aérea, sombra macia, e uma lâmpada quente em cada poste do kit. Uma receita só, `luz_de_jogo` em `tools/mapas/kits/montagem.py`, com três climas (dia, tarde, deserto). Sem luz rebatida de verdade (SDFGI): testada, deixava preto o que o céu não vê. Conferido em fotos Forward+ do mapa com personagens parados no sol, no beco e debaixo de laje — não dentro da partida. 
- **Escadas**: sobe-se e desce-se na velocidade de andar, sem travar em cada degrau (humano e lobo) — uma rampa de colisão por cima dos degraus nas escadas da Estação, da Obra e do Deserto, e `FloorConstantSpeed` no jogador e no bot. Testes novos `EscadaObra`, `EscadaEstacao`, `EscadaDeserto` (3,45 m/s na escada para 3,46 no chão; o lobo, 4,15).
- **O lobo não corre nem sobe no muro que fecha o mapa**: a parede que tem a barreira invisível logo atrás da face não segura a corrida (`Player.BackedByBarrier`); ele cai. Vale para os quatro mapas de kit; o Armazém não muda. Teste `ParedeInvisivel` ampliado.
- **Obra**: as muretas de tijolo agora têm a colisão da própria peça — passa-se pelo vão da mureta quebrada; a escada chega à laje (o último degrau ficava 20 cm abaixo). Marcada como mapa só de mata-mata em `Maps.cs`.
- **Estação de Trem**: bocas de túnel nas pontas das três linhas (a parede de arco do kit, escura por dentro, entra 5 m) num muro de arrimo de 10 m; a rua continua para fora do mapa, interditada; as torres de relógio são duas, inteiras, na calçada da entrada (eram quatro enfiadas nos cantos); as guaritas com sacada entram no plano da fachada, na altura dela; janelas e relógio no ático do saguão; os prédios em volta têm um corpo só, na altura do kit, sem repetir o vizinho (eram dois empilhados). O mesmo nos prédios que fecham a Cidade.
- **Mata-mata (só a parte do mapa)**: cada mapa de kit tem o nó `NascerLivre` com 16 pontos espalhados e alcançáveis a pé. O modo todos contra todos ainda não existe no jogo (`RoomConfig.ModeAvailable` só libera Equipe).
- **Granada**: o arremesso (só ele) ficou mais rápido — o clipe toca de 1,05 a 1,4 da velocidade, conforme a força (era de 0,82 a 1,1).
- **Mãos de primeira pessoa mais lisas**: 5.600 triângulos (eram 3.200), quina só acima de 72° (era 50°), 12 tons (eram 8). `tools/armas/maos_lowpoly.py`.
- (Esta entrega foi juntada com a da outra sessão, das 15:45: valem dela o muro de obra de 3,8 m com pilares, os telhados com colisão, o Deserto sem vegetação, a muralha de 7,6 m, os materiais desenhados e as capas "clima de guerra"; valem desta a luz, os túneis, as torres, as escadas, os prédios sem repetir e as muretas. As capas não foram refeitas aqui.)
- **Materiais desenhados nos mapas de paleta** (Jhon, 20/11: "nível Riot, Overwatch", "tirar o aspecto de Roblox"): tijolo com junta, laje com emenda, calçamento de pedra, tábua, chapa com rebite, telha que segue a queda do telhado, reboco que descasca e mostra o tijolo; cada peça de um tom (a "tinta", `detalhe_c.jpg`). Luz do sol mais quente e sombra de contato (SSAO, SSIL) na Cidade, no Deserto, na Estação e na Obra.
- **Mapas mais baixos em volta**: Estação com os prédios da volta de um corpo só, Deserto com a muralha de 7,6 m, Obra com muro de obra de 3,8 m (tijolo, pilar e capa). **Telhados com colisão** (Estação, tendas do Deserto, corpo de cima dos prédios da Cidade).
- **Deserto sem vegetação nem água**, com a rua da feira calçada, lajotas e blocos no chão e dunas redondas ao fundo.
- **Cidade**: bombas do posto de chapa pintada (saíam de tijolo); nada mais a cavalo no meio-fio (flutuava); ar-condicionado só em parede alta.
- **Capas dos mapas com clima de guerra** (névoa de poeira, fumaça ao longe, cor mais dura), só na foto (`tools/fotos_mapas.gd`).
- **Servidor: painel administrativo, segurança e economia no servidor** (20/11). O servidor central virou dono da carteira (Presas e Sangue), do inventário, do extrato, do catálogo, dos mapas, das promoções, dos desafios e das configurações (banco na versão 2; `docs/decisoes/0013`). **Painel** em React + Tailwind + shadcn, numa porta só dele (8081, `docs/PAINEL.md`, decisão 0012): visão geral ao vivo, jogadores (banir com prazo e motivo, dar e tirar moeda e item), armas e skins, mapas (liga e desliga a rotação), promoções, desafios (inclusive de evento), eventos de segurança, blacklist de IP, auditoria, manutenção, bônus de XP e administradores com papéis e verificação em duas etapas. **Escudo** (`docs/SEGURANCA.md`, decisão 0014): endereço real só de proxy de confiança, blacklist de IP, freio de pedidos, varredura e injeção recusadas, força bruta e "uma senha em várias contas" bloqueadas sozinhas, todo evento registrado (sem senha), alerta de resultado de partida absurdo. Compra conferida no servidor; desafio anda com o resultado do servidor de partida. Produção: Caddy (HTTPS), Prometheus, Loki, Grafana (`docker-compose.producao.yml`, `docker-compose.monitor.yml`). No jogo: `ServerData` lê do servidor mapas em rotação, preços, promoções, aviso e manutenção; a sala respeita o mapa desligado. **O `.env` agora pede `ALCATEIA_MASTER_KEY`.** Falta: a loja e as missões do jogo trocarem o arquivo local pelo servidor, e vida e acerto no servidor de partida.

- **Superfícies com detalhe nos mapas de paleta** (pedido do Jhon, 20/11: "textura mais detalhada no chão, metais, vidros, sem tirar a essência"): Armazém, Cidade, Cidade do Deserto, Estação de Trem e Obra mantêm a cor da paleta e ganham o grão de cada material — relevo, aspereza e um claro-escuro leve; o metal é metal e o vidro reflete o céu. Cada face tem o material decidido pela peça, pela cor e por para onde olha (`tools/mapas/kits/detalhe.py`); o material é `assets/mapas/detalhe/superficie.gdshader` (`intensidade` 0 volta ao liso). Veja "O detalhe das superfícies" em `docs/MAPAS.md`.
- **Calçadas na Cidade** (pedido do Jhon, 20/11): a calçada e o asfalto são dois níveis de verdade, com meio-fio — o asfalto 14 cm abaixo, 1,6 m de calçada ao pé dos prédios; os carros e as barreiras ficam na rua. Antes o asfalto era uma chapa 3 cm acima da calçada, de parede a parede, e de longe as duas se misturavam.
- **Estação de Trem refeita no centro da cidade** (Jhon, 20/11: "muito desértico", "muito quadrada", a escada da passarela não chegava em cima): 120 × 95 m, com a rua e a praça ao norte, os prédios do centro fechando o mapa (kit da Cidade, num segundo desenho com colisão), o prédio com telhado de duas águas, o saguão mais alto entre quatro torres de relógio, sacadas e cobertura na plataforma, chão de asfalto e leito de brita só nas linhas. A passarela desceu para 5 m, a altura do último degrau da escada do kit (estava a 6 m: faltava 1 m).
- O mapa **Escritório saiu do jogo** (Jhon, 20/11).
- Espectador: pedaços do personagem sumiam e voltavam (Jhon, 20/11). As malhas do corpo (cabelo, cabeça, luvas, tronco, calça, botas; o lobo) ganharam folga de 2,5 m na conta de o que está na tela (`SkinCulling`): a caixa de cada uma era a da pose de descanso.
- Mapas de kit **adaptados ao lobo** (Jhon, 20/11): o que fecha cada mapa tem de 14 a 20 m ou mais; o Escritório virou o térreo de um prédio comercial de 46 m, com pé-direito de 6 m. **Fundo**: duas fileiras de prédios em toda a volta e o horizonte em painéis (a cidade; a serra no Deserto, com mais montanhas). Enfeite que trombava com outra peça sai (`Montagem.grupo`).
- O lobo não corre em parede invisível (Jhon, 20/11): a corrida em parede ignora os limites do mapa (`Limite…`) e as barreiras do alto do muro (`Barreira…`) — não gruda e, se uma delas fecha a frente, solta. As barreiras dos mapas de kit ficam 6 cm para dentro da face do muro (teste `ParedeInvisivel`).
- **Escritório** (mapa novo, 100 × 70 m): um andar de escritórios coberto, todo corredor e sala, com o pátio do chafariz aberto no meio e a rua em volta.
- **Barreira invisível no alto do muro de fora, nos cinco mapas de kit** (pedido do Jhon, 20/11: o lobo corre na parede e chegava em cima do muro): do chão ao teto sobre todo o maciço que fecha o mapa (`barrar`). O teste de mapa confere 244 pontos em toda a volta.
- **Mapas de kit mais ricos** (pedido do Jhon, 20/11): na Obra, as colunas na malha do prédio em todos os pisos (com a ferragem de espera onde falta laje), refletores, cones, tábuas, tijolos, latas e mato ao pé do muro; na Estação, relógios, quadros de horários, luminárias, mais bancos e caixas no prédio, sinais nas linhas e capim na brita; no Escritório, luminárias, quadros, vasos, estantes, baias completas, cortinas e o jardim; no Deserto, as casas mobiliadas, toldos, varais nos becos, potes e palmeiras; na Cidade, árvores, canteiros, placas, ar-condicionado, postes com fio, faixas de pedestre.
- **Obra** (mapa novo, 100 × 70 m): um prédio de concreto em construção no meio do canteiro — corredor de 60 m, salas e salões no térreo, duas lajes abertas (4 e 8 m) com escadas —, andaimes, gruas e material em volta.
- Minimapa: a laje de um andar de cima e o teto não são desenhados (`metadata/mapa_fora`, posto pelo gerador dos mapas de kit) — tapavam as paredes de baixo.
- **Estação de Trem** (mapa novo, 120 × 70 m): o prédio da estação com saguão, corredores e salas, duas plataformas, três linhas com trens parados, o pátio de carga e a passarela de 6 m por cima dos trens.
- **Cidade** e **Cidade do Deserto** refeitos maiores e com corredores (pedido do Jhon, 20/11: "maiores e mais robustos", "com caminhos, corredores"): a Cidade passa a 120 × 80 m (três ruas, bulevar, becos de 2,5 m por dentro dos quarteirões) e o Deserto a 110 × 75 m (cidadela talhada na muralha: três vielas, becos, a rua da feira, a praça). A planta de cada mapa agora é um desenho em letras no topo do arquivo dele (`tools/mapas/kits/planta.py`).
- Teste de mapa: "bots que saíram da base" mede o mais longe que cada um chegou (a cada segundo), não a posição do fim — no mapa grande quem morre no meio renasce na base.
- Dois mapas novos: **Cidade** (duas ruas, praça, prédio em obras, postos de gasolina) e **Cidade do Deserto** (vila murada, praça do chafariz, casas de laje). Montados com as peças do pacote Low Poly FPS Bundle, com planta própria.
- Armas épicas e lendárias novas, cada uma com pintura própria e um pouco mais forte que a de origem: M4A4 Neon e Dragão, AK-74 Tigre e Imperial, SCAR-H Ártico e Galáxia, SVD Carbono e Tempestade, Desert Eagle Cobra e Fênix, Faca Damasco e Obsidiana. A faca agora se escolhe por conjunto (aba Facas na loja).
- Vitrine: o lobo fica em pé em cima do pedestal (afundava 13 cm no disco e as garras atravessavam); qualquer personagem é assentado pelo ponto mais baixo da pele ao longo do parado.
- Eve e Barut novos: são os personagens dos times (jogador e bots), com todas as animações.
- A arma que os outros veem fica encaixada nas duas mãos e não atravessa o corpo.
- No inventário, o personagem aparece de pé, em pose parada, e o cartão mostra a arte dele.

- Arremesso da granada: a mão entra na linha da cruz antes de soltar; a granada sai do meio da mão, em cima da linha da cruz, e voa reto para ela (antes saía 11 cm à direita e convergia no voo).
- A granada sai da mão: nasce no instante e no lugar em que a mão a solta no arremesso (antes nascia no clique, à frente da câmera) e voa para o ponto que a cruz aponta. O arco depende da distância — perto, um lance quase direto, junto da cruz; longe, o arco alto — e a mão lança mais rápido quanto mais longe. Alcance: cai no ponto até 19 m (eram 16); mirando o céu, ~18 m (eram 15,5).
- Desert Eagle: o andar é o do AR-15 (a arma quase não vai mais para os lados).
- Dedos fechando em cada arma: na AK-74, na SCAR-H e na SVD a mão esquerda fecha no guarda-mão como na M4A4 (as pontas dos dedos não sobram mais acima da arma).
- Desert Eagle: as duas mãos se abraçam sem uma passar por dentro da outra; recarga mais rápida (1,8 s e 2,2 s); bem menos coice ao atirar e bem menos balanço ao andar.
- M4A4 mirada: a alça ficou pequena e abaixo do centro da tela, com o pino da massa no meio — dá para ver o oponente.
- Faca: quatro golpes que passam um a um (estocada, corte, estocada alta, corte deitado).
- Granada: a mão armada fica mais alta e mais atrás.

- O tiro atravessa aliados (com o fogo amigo desligado) e acerta o inimigo atrás deles; os bots também atiram por cima do time.

- A granada é explosiva (antes era a de choque) e cai onde a mira aponta, até 16 m; ao tocar o chão ela fica, não rola. Já vem no conjunto padrão de todo mundo.
- Granada na mão refeita: a mão fecha em volta dela sem atravessar, com a alavanca na forquilha do polegar; o indicador esquerdo entra na argola e arranca o pino; a mão fica à vista enquanto se mira o arremesso; pulso reto ao arremessar. Ela é 18% maior na primeira pessoa.
- Faca mais rápida (0,40 s entre golpes) e cada clique vale um golpe, mesmo dado no meio do anterior.
- Tecla F olha a arma na mão (a faca e a Desert Eagle); parado por um tempo, o jogador faz isso sozinho.
- Desert Eagle: na recarga o carregador sai e entra reto pelo punho, com a mão esquerda segurando; parada, a arma fica um pouco mais alta e à frente; mirada, mais perto e mais baixa.
- M4A4 mirada: mais estável ao atirar e com o campo acima da massa mais livre.
- AK-74 mirada: não se vê mais a arma por dentro, e o pino da massa aparece por cima da alça.
- Recarga dos fuzis sem o braço passar na frente da câmera.

- Vitória e derrota aparecem já sobre o fundo escuro e desfocado da tela de resultado.

- Eve no inventário: personagem do time azul, com o soldado e o lobo azul de sempre.

- Faca com todos os movimentos da animação de referência: parado com a mão esquerda à vista, estocada, corte, o giro de inspecionar e o saque.
- Desert Eagle com as animações da pistola de referência: tiro com mais coice, recarga com e sem bala (o carregador novo vem na mão esquerda), andar e inspecionar; coronhada com a lateral da arma.
- A M4A4 é a arma padrão. O conjunto padrão aceita qualquer arma comum (M4A4, AK-74, SCAR-H, SVD, Desert Eagle) e continua de graça, rendendo +50 WP; o AR-15 e as skins dele não vão mais no padrão.
- Armas novas, com os modelos que o Jhon trouxe: M4A4, AK-74, SCAR-H (a arma pesada), SVD Dragunov (fuzil de precisão, com luneta) e Desert Eagle (a pistola padrão, no lugar da M9). A granada agora é a V40. Todas com as mesmas mãos, o dedo no gatilho, ferrolho que anda no tiro, recarga, saque, mira e coronhada.
- Fuzil de precisão: a luneta sobe ao olho e a tela corta para a mira redonda branca em fundo preto, com zoom.
- Granada: a mão esquerda arranca o pino, a direita vai lá atrás e arremessa; a arma seguinte só sobe depois.
- Faca: dois ataques curtos e rápidos que se alternam (o corte na diagonal e a estocada), com as mãos padrão.
- A câmera acompanha a animação da arma (saque, recarga, tiro, golpe).

- Bots lobo correm de quatro com a animação de galope, como o jogador.

- Ao renascer, o espectador sai de vez (antes a tela do aliado podia ficar misturada com a sua).
- No minimapa, a sua seta é branca.

- Câmera de abate e jogada da partida removidas; ao morrer, espectador direto.
- Espectador sai sozinho quando você renasce, inclusive na escolha automática.
- Espectador não atravessa parede, chão nem teto; teto do armazém estendido a 13 m.
- Primeira pessoa do espectador com HUD, mira, tiro, recuo e animações da arma de quem você assiste.
- Feed de abates continua no espectador, destacando quem você assiste.
- Vitória/derrota fica 4 s em câmera lenta antes do resultado.
- Mudanças no inventário persistem ao fechar e abrir o jogo.
- Calça dos lobos não abre mais no pulo.
- Textos em inglês e espanhol.

- Recarga do AR-15 refeita a partir da recarga de referência do tutorial da InfimaGames (`tools/armas/recarga.py`): a mão esquerda vai ao carregador pela frente, por baixo da arma, tira, traz o novo por baixo e encaixa. A arma, o corpo e a pegada das duas mãos partem do parado do Jhon e voltam a ele; a mão direita não sai do punho. O clipe tem os 2,3 s da recarga do jogo, com a saída (0,32 s) e o encaixe (1,42 s) do carregador em cima das batidas do som. Vale para o AR-15 e as cinco skins.

- AR-15 Ouro e AR-15 Diamante bem mais fortes que as épicas: mais dano ou cadência, pente maior, recarga mais rápida e menos recuo.

- Jogada da partida no fim, antes do resultado: os melhores segundos pela tela de quem fez. Espaço pula.
- Câmera de abate: "VOCÊ" com uma seta no seu corpo; o corpo de quem matou não aparece duplicado; o lobo anda no replay; a câmera de lobo não olha para o teto.
- Espectador: não sai mais do mapa pela vista de cima; o tiro sai da arma dos bots; a tela do aliado acompanha pente, vida, recarga, dano e som como a sua.
- A câmera de abate só abre quando você morre.
- Bordas só no contorno: aliados em branco visíveis no mapa todo, inimigos em vermelho; nunca por cima da sua arma; lobos também.
- Bots falam no rádio, sem exagero.
- A voz de abate duplo, triplo… é só para quem fez.
- O especial do lobo no feed com o mesmo brilho do botão.
- Armas novas: AR-15 Ouro e AR-15 Diamante (lendárias), AR-15 8 Bit e AR-15 Lava (épicas). Melhorias novas de lobo: Caçador Noturno, Presas de Aço (épicas), Alfa e Fúria da Lua (lendárias).
- Ficam só os AR-15, a pistola, a faca e a granada: M16A2, UMP-45, AK-47, M3 e SG-552 saíram.
- A Eve saiu do jogo.
- O menu toca o tema da sala; as músicas do fim da partida e do resultado estão mais altas.
- Armazém com sombras menos escuras; sem a mancha de luz nas costas do lobo.

- Câmera de abate: o replay é a tela de quem te matou — a arma na mão dele com tiro, clarão, recarga e coronhada, o som, o HUD do jogo dele (vida, munição, especial, WP, minimapa) e o feed dele; se era lobo, a câmera de lobo com o HUD de lobo. Sem música.
- Espectador: a tela do aliado, com a arma e o HUD dele.
- Contorno dos aliados (branco) e inimigos (vermelho): só em volta do corpo, sem passar por cima da arma nem marcar linhas por dentro.
- Coronhada mais rápida (0,40 s) e com 10 de dano.
- O círculo no chão do uivo do lobo voltou.

### Câmera de abate e espectador refeitos, bordas de time, sala direta (18/11, à noite)
- **Câmera de abate em primeira pessoa** (`KillCam`, com o Overwatch de
  referência): ao morrer, 1 s no próprio corpo; depois o **replay pelo
  olho de quem te matou**, com a **arma dele na mão** (o mesmo conjunto de
  primeira pessoa do jogo, com as mãos padrão), parado, andando e
  atirando, a **mira** dele (giro e inclinação da cabeça) e o **HUD dele**:
  nome, vida em gomos e escudo embaixo à esquerda, arma e munição embaixo
  à direita, a mira no meio. Faixas escuras em cima e embaixo, a imagem
  levemente tingida, **"CÂMERA DE ABATE"** grande e "RENASCE EM" com os
  segundos, uma **música** por trás (`musica_camera_abate`, sintetizada em
  `tools/sons/camera_abate.py`; trocar o arquivo troca a música) e o nome
  dele marcado no feed. **Espaço pula.**
- A fita (`ReplayTape`) agora guarda também a inclinação da cabeça, a arma,
  a munição, a vida, o escudo, a forma e os tiros de cada corpo.
- **Quem matou era lobo**: a câmera é a de lobo do jogo (atrás dele, na
  mola, sem atravessar parede), e o lobo é uma cópia que refaz o caminho
  (`WolfVisual.MakeGhost`).
- **Espectador** direto em **primeira pessoa** de um aliado, com o HUD
  dele. Clique esquerdo/direito trocam de aliado; Espaço: primeira pessoa
  → de cima → câmera livre. Aliado lobo: câmera de lobo.
- **A câmera livre não sai do mapa** (`MapBounds`): a caixa é o lado de
  dentro das paredes `Limite…` (Oeste, Leste, Norte, Sul, Teto) e o topo
  do `Piso`. **Todo mapa novo deve ter essas paredes**; sem elas, vale a
  colisão inteira do mapa com 6 m de folga em cima.
- Morto, o HUD de quem joga esconde a vida, a arma e a mira (o placar, o
  relógio, o minimapa e o feed ficam). O rádio não fala morto.
- **Bordas de time** (`TeamOutlines`): aliado com borda branca suave
  (vista através da parede), inimigo com borda vermelha suave e leve (só
  à vista).
- **Minimapa**: a área onde o inimigo nasce pintada de vermelho, em cima.
  Teste novo, `ZonaInimigaNoMapa`.
- **Tela vermelha de quase morrendo pela vida, nas duas formas**: o escudo
  do lobo por cima não apaga mais o aviso.
- **Coronhada do AR-15 refeita**: o golpe chega em 0,11 s, a arma segue
  numa leve empurrada, segura e volta (0,62 s no total). Som próprio
  (`coronhada_1..3`, `tools/sons/coronhada.py`): o equipamento, o ar e
  um "uh" curto. Sem o som da faca.
- **Granada**: o arremesso mais fraco (o teto é um braço, ~15 m); quem
  lança continua levando dano e choque da própria granada.
- **Renascer**: 10 s, +1 s por morte, no máximo 15 s.
- **Rugido sem o círculo com borda no chão**; fica a luz macia nos pés.
- **Criar sala vai direto para a sala** (sem as janelas de modo e de
  regras), com as regras da última sala; lá o dono muda o que quiser. O
  tempo na sala vai até **30 minutos**.

### Tatuagem vira melhoria (18/11, à noite)
- Nos textos da loja, do inventário e das mensagens, **"tatuagem" passa a
  ser "melhoria"** (pedido do Jhon). Os identificadores do código e dos
  itens (`ItemKind.Tattoo`, `tat_*`) ficam como estão; a planilha de
  textos foi atualizada (13 chaves trocadas).

### Replay do abate, espectador, granada de choque e coronhada do AR-15 (18/11, à noite)
- **A partida é gravada para o replay do abate** (`ReplayTape`): os últimos
  5 s de posição e giro de todo mundo. Ao morrer, a câmera de abate passa
  **os 3 s antes do abate, do olho de quem te matou**: a câmera anda pelo
  caminho dele olhando para você, e o seu corpo é posto de pé e refaz o
  que fez até cair de novo. Só os dois do abate são reencenados (os
  outros corpos aparecem onde estão agora), e a câmera olha para a vítima
  — a mira exata do matador não é gravada. Espaço pula. Sem fita
  bastante (morreu ao nascer), fica a visão ao vivo.
- **Espectador**: clique esquerdo passa ao próximo aliado, direito ao
  anterior; **Espaço troca a visão**: de cima → primeira pessoa do aliado
  → câmera livre → de cima.
- **A escolha de conjunto só aparece na hora de renascer**: morto, a tela
  é do replay e do espectador. O relógio dela corre mesmo com o menu do
  Esc aberto — o Esc não segura nada: não escolheu, vai o padrão e renasce.
- **Granada de choque**: o estouro é elétrico (`ShockBlast`: clarão frio,
  raios pelo chão, dois anéis azuis até o raio, faíscas), **desacelera e
  atordoa** jogador e bot (o bot atordoado anda devagar e não ataca) e
  tira pouco (60 no centro). A explosão de fogo que mata (260) ficou
  guardada como tipo **fragmentação** (`Grenade.Kind`), para a granada
  desse tipo que ainda vai entrar.
- **O pavio só conta depois que a granada encosta** em algo (1,2 s).
- **Coronhada do AR-15 com animação própria** (`tools/armas/coronhada.py`):
  a arma atravessa na frente do rosto e bate com a lateral, com os braços
  acompanhando (cinemática inversa: só a arma se move, os ombros ficam).
  As outras armas seguem com o movimento de código.
- **Conjunto padrão editável**: aceita itens gratuitos e os liberados no
  catálogo (`CatalogItem.DefaultOk`): hoje a **AR-15 Ultimate** e a
  **granada de choque**. No padrão eles não custam WP. A mesma arma pode
  estar em mais de um conjunto (nunca houve trava).
- **Armas na loja e no inventário nítidas**: saíram o clareamento e o
  brilho branco que passava; a foto aparece como é.
- **Vermelho de quase morrendo para o lobo**: conta a vida mais o escudo
  (o lobo sem escudo e com meia vida não via nada).
- Testes novos: `PerigoNaTela`; `Espectador` e `GranadaNaMira` ampliados.

### Coronhada, granada, câmera de abate e espectador (18/11, à noite)
- **Coronhada (V)**: com arma de fogo na mão, um golpe rápido com a arma
  (55 de dano, 1,9 m, 0,45 s; `Weapon.Butt.cs`). Não gasta munição, corta
  a recarga e tira da mira. Na mão, a arma recolhe, gira e soca
  (`ViewModel.Butt`, de código: vale para todas as armas). Em terceira
  pessoa ainda não há animação própria. Teste novo, `Coronhada`.
- **Granada**:
  - **cai onde a mira aponta**: a força do arremesso é calculada para o
    ponto mirado (`Weapon.ThrowVelocity`); encostou, quica pouco e para.
    No teste, explode a ~0,5 m do ponto a 5,5 m e a 12 m;
  - **voa o modelo da granada**, não mais a bola;
  - **explosão de fogo** (`Explosion`): clarão, línguas de fogo que incham
    e escurecem, onda de choque até o raio, fagulhas, fumaça e estilhaços;
  - **dano em área que mata**: 260 no centro, caindo até a borda (6,5 m).
    Ao lado de alguém, mata de primeira. Era 70.
  - Teste novo, `GranadaNaMira`.
- **Câmera de abate**: ao morrer, a câmera vai para o olho de quem te
  matou, olhando para onde você caiu, com o nome dele no alto. Dura 2,6 s
  e **Espaço pula**. É a visão dele ao vivo, não uma repetição gravada do
  tiro.
- **Espectador**: depois da câmera de abate, até renascer, a câmera fica
  **por cima de um aliado vivo**; Espaço ou clique passam para o próximo;
  **F solta a câmera** para andar pelo mapa (WASD, mouse, Shift corre,
  Espaço sobe, Ctrl desce) e F de novo volta aos aliados. Teste novo,
  `Espectador`.
- **Bala de raspão**: tiro inimigo que passa a menos de 1,7 m da cabeça
  (pegando ou não) faz o "vuum" (`bala_raspao_1..4`, sintetizados por
  `tools/sons/raspao.py`), mais alto quanto mais perto.
- **Rádio**: quem fala ganha o **ícone do rádio em cima da cabeça** (visto
  pelo time, através da parede) e **pulsa no minimapa** por 2,6 s.
- **Mirando agachado no Mac, atirar não tira mais da mira**: o clique
  esquerdo com Ctrl chega como "direito", e soltá-lo soltava a mira.
- **Música do fim mais alta** (de −14 para −5 dB).
- **Vermelho de quase morrendo** só abaixo de 30% da vida (era 55%).
- Ajuda de teclas: V (coronhada) e Z (rádio).

### Mãos low poly (18/11, à noite)
- As mãos de primeira pessoa (as de todas as armas do esqueleto do AR-15)
  ficaram **low poly, na pegada do lobo e das armas**: de 7.240 para 3.200
  triângulos, com **cores chapadas** — uma tira de 8 cores
  (`maos_paleta.png`) tirada da média da pele e da luva antigas, no lugar
  da pele fotográfica. Superfícies suaves ficam lisas e só as quinas de
  verdade (acima de 50°) viram faceta: a primeira tentativa, toda facetada
  com 2.000 triângulos e um tom por face, ficou "quadriculada demais".
- `tools/armas/maos_lowpoly.py` refaz tudo a partir das mãos originais,
  guardadas em `tools/armas/fontes/` (fora do projeto do Godot). O esqueleto
  e os pesos são os mesmos: as animações não mudam.
- Saíram `maos_cor.jpg`, `maos_normal.jpg` e `maos_aspereza.jpg`. Para
  trocar a cor das mãos: editar a tira ou o `padrao.tres`, como antes.
- **O mapa e a textura do AR-15 ficam como estão** (o Jhon desistiu de
  retexturizar os dois no mesmo dia).

### Mira do AR-15 alinhada de verdade (18/11, à noite)
- **A alça e a massa agora se alinham no alvo.** A primeira versão media o
  meio da "caixa" de cada mira, 5 mm abaixo do furo do anel: o olho ficava
  olhando para o metal e a mira só atrapalhava (relato do Jhon). Agora a
  linha passa pelo **centro do furo da alça** e pela **ponta do poste da
  massa**, e as duas ficam no centro da tela (`tools/armas/mira.py`).
- **Mais perto do olho**: a alça fica a 6,2 cm (era 10,5), então o anel
  aparece maior e se vê a massa e o alvo por dentro dele.
- **O anel da alça foi aberto** na malha (furo de 2,3 para 4,6 mm de raio,
  aro fino): de fábrica era um buraco de fechadura.
- Respiração e balanço na mira menores (10% e 16% do clipe de origem), para
  as duas miras não se desencontrarem.
- O corte de perto da câmera foi de 3 para 1,5 cm: o conjunto é desenhado
  encolhido (`RigScale`) e a alça, tão perto, sumia.

### Rádio do time (18/11, à noite)
- **Z abre a lista de calls** no canto inferior esquerdo, acima da vida;
  **1 a 9 e 0** (o décimo) falam e fecham. Z de novo, Esc ou 5 s sem
  escolher fecham. Com a lista aberta os números não trocam de arma.
- As dez falas: Enemy Spotted!, Roger That!, Follow Me!, Cover Me!, Go,
  Go, Go!, Fall Back!, Hold Position!, Need Backup!, Sector Clear!, Nice
  Shot!
- **A fala toca para todo o time de quem falou, e só para ele**, sem
  posição (é rádio), e aparece escrita no canto com o nome. Online o call
  viaja pela partida (`NetWire.Radio`).
- **Time azul, voz feminina; time vermelho, voz masculina**
  (`radio_f_1..10`, `radio_m_1..10`): os dois áudios do Jhon cortados nas
  pausas, com som de rádio (`tools/sons/radio.py`).
- 1,2 s de intervalo entre um call e outro do mesmo jogador.
- Teste novo, `RadioDoTime`. Planilha de textos atualizada (12 novos).

### HUD e retorno (18/11, à noite)
- **Quase morrendo**: abaixo de 55% da vida a borda da tela avermelha e
  fecha, com veios escuros, pulsando num coração que acelera; cada golpe dá
  um clarão vermelho. Abaixo de 30% entra a **respiração ofegante**
  (`respiracao_ofegante`, `tools/sons/respiracao.py`), mais alta quanto
  menos vida.
- **Minimapa**: virado para o time de quem joga — a nossa base embaixo, a
  do inimigo em cima. Aliados em azul, **inimigos em vermelho com contorno
  escuro** (o ponto laranja sumia em cima dos contêineres) e desenhados por
  cima. Jogadores de verdade também aparecem (antes só bots).
- **O Sindicato é vermelho** (`UiTokens.TeamOrange`), não mais laranja.
- **Barra de vida no mundo** refeita: fina, inclinada, em gomos de 25,
  com o escudo numa faixa em cima e o "fantasma" do dano. **Só aparece
  quando você acerta o inimigo** (ou quando o aliado apanha) e some 2,4 s
  depois. Inimigo vermelha, aliado branca.
- **Feed de abates**: arma épica ou lendária acende a linha inteira na cor
  da raridade — borda, halo pulsando e um reflexo varrendo.
- **Saiu o cartão "abatido por"** (o duelo com o placar entre os dois).
- **Acerto**: por cima do som de acerto, o sangue espirrando
  (`CombatantAudio.SplashDb`).
- **Música do fim** também toca quando um time chega a 10% da vitória por
  pontos; antes só no último minuto do relógio, e partida que acabava por
  pontos ficava muda.
- **Poeira da corrida de quatro** (`SprintDust`): pontinhos redondos e
  macios, de 2 a 6 cm, da cor do chão que o lobo pisa, saindo das patas.
- **Abate com o especial do lobo no feed**: no lugar do ícone simples, o
  selo do especial (`SpecialBadge`) — o disco laranja com a garra, raios
  girando e faíscas, como o disco do especial pronto. Todo mundo vê que
  foi com o especial.
- **O laranja do lobo ficou separado do time** (`UiTokens.WolfOrange`): o
  especial, a transformação, o escudo e os golpes no feed seguem laranja;
  só o Sindicato virou vermelho.

### Regras de partida (18/11, à noite)
- **Especial (Q) com prazo e sem volta**: armou, tem 5 s para saltar
  (`Weapon.SpecialArmTime`); passou, perde a carga. Q de novo não desarma
  mais. O disco mostra o tempo que resta. Morrer ou virar humano armado
  também gasta.
- **A carga demora bem mais e segue a sala**: abates para encher = 20% da
  pontuação da sala (40 pontos = 8 abates; nunca menos de 6), e sozinha
  enche em 70% do tempo da partida (10 min = 7 min). Assistência = 40% do
  abate. Tudo exportado em `Weapon.SpecialCharge.cs`.
- **Especial mais forte**: armado e no salto o lobo leva 40% do dano (era
  50%) e o salto bate 2,6× (era 2,1×).
- **Modo sem lobo** (só humanos): o especial não carrega e o HUD não mostra
  nem o disco nem a faixa "virar lobo".
- **Renascer**: 10 s no mínimo para todo mundo (bots também), +2,5 s a cada
  morte, até 30 s (`Health.RespawnDelay/Growth/Max`). No treino continua 3 s.
- **Escolha de conjunto**: 15 s no máximo; não escolheu, vai o conjunto
  padrão.
- **Bots sobem degrau baixo andando** (até 0,5 m, o mesmo que a navegação
  aceita): antes paravam na quina dos contêineres baixos.
- **Lobos colados não ficam mais parados**: o bot lobo, no alcance da
  garra, roda em volta do alvo entrando e saindo, e troca de lado quando
  apanha.
- **Garra logo depois do bote**: o dano do bote (pulo + botão direito)
  caiu, 0,05 s depois o clique esquerdo já arranha e corta a aterrissagem
  (`Weapon.DiveFollowUp`). Antes a garra esperava a aterrissagem inteira.
  Teste novo, `GarraDepoisDoBote`.
- **Sem garra na corrida de quatro**: no W W o clique não arranha; tem de
  sair da corrida para bater. (Desfaz o "bater correndo" da tarde, que foi
  uma leitura errada do pedido.) Teste novo, `SemGarraNaCorrida`.
- **O C não agacha mais**: só o Ctrl (pedido do Jhon). No Mac, agachado no
  Ctrl, o botão direito de verdade mira.

### Mira animada do AR-15 (18/11)
- **Mirar com o AR-15 (e o Ultimate) leva a arma ao olho.** Antes o botão
  direito só fechava a câmera e desenhava uma mira circular; agora a arma
  sobe da cintura, a alça e a massa ficam em linha no centro da tela e a
  cruz some. Cinco clipes novos no conjunto (`tools/armas/mira.py`):
  `mirar` (0,26 s, sobe num arco, inclinada, e assenta), `desmirar`
  (0,24 s), `mira` (respiração com um quarto do tamanho), `mira_andar`
  (balanço com 35%) e `mira_tiro` (coice com 60%).
- Soltar no meio da subida (ou apertar no meio da descida) continua de
  onde a arma está. Recarregar, guardar e sacar tiram da mira.
- A boca do cano (clarão, traçante, cápsula) segue o osso da arma
  (`cano_osso` na ficha), então o tiro na mira sai do cano e não da
  cintura.
- As outras armas miram como antes, até ganharem os clipes.
- Teste novo, `MiraDaArma`.

### Lobo agachado refeito; bater na corrida de quatro; pulo, mira e Shift do humano (18/11)
- **Lobo agachado e andando agachado** refeitos
  (`tools/rugido/agachar.py`). Antes: braços congelados (as mãos andavam
  de 1 a 4 cm no ciclo), quadril a 0,49 m sem subir nem descer e coxas
  abertas na horizontal, que esticavam o short. Agora é uma espreita:
  quadril a 0,63 m, tronco deitado, cabeça erguida (mesma altura de
  antes), braços soltos balançando ao contrário das pernas, joelhos para
  a frente. `CrawlClipSpeed` 1,5.
- **Bater correndo de quatro (W W)**: o golpe já saía e feria, mas a
  garrada por cima do galope dobrava o lobo no chão e não se via nada.
  Agora o corpo se ergue para o golpe e volta a correr de quatro, sem
  perder velocidade.
- **Pulo do humano**: de 0,80 m para 0,99 m (volta a subir num caixote de
  1 m).
- **Mirar agachado no Ctrl (Mac)**: o botão direito de verdade com Ctrl
  agora mira; o Ctrl-clique (esquerdo) continua atirando. Separados pelo
  botão que está de fato apertado no mouse.
- **Shift preso**: se o "soltou" do Shift (ou do Ctrl) não chega do
  sistema, o próximo evento de mouse ou teclado solta a ação
  (`StuckKeys`). Teste novo, `ShiftPreso`.

### Sons de acerto, X vermelho e voz do lobo (18/11)
- **Acerto**: a cada bala que pega, em qualquer parte do corpo, o som que
  o Jhon escolheu (Hit Generic 2-1, `marcador_1`). **Headshot**: o "plin"
  por cima, só na cabeça, a cada um (`marcador_cabeca_1..2`). Só quem
  atirou ouve; o impacto no mundo (`impacto_carne`) fica para os outros.
- O **X em volta da mira** é vermelho em todo acerto (era branco no corpo).
- **Pulo e morte do lobo** refeitos (`lobo_pulo_1..3`, `lobo_morte_1..3`,
  `tools/sons/lobo_voz.py`): os de antes eram gravações de "troll" e
  "ogro" (gente fazendo voz de criatura) e algumas variações soavam como
  o humano. Os novos saem do mesmo bicho do rugido.
- A voz (pulo, dor, morte) é a do corpo que se vê: voltando a humano, na
  primeira metade da fumaça ainda é a do lobo.

### Golpe do lobo que não feria perto de cenário (18/11)
- **O especial travado (e a garrada) podiam não dar dano nenhum.** A área
  do golpe era uma consulta de física com teto de contatos (8 na garrada,
  48 no salto) que misturava cenário e gente: perto de um contêiner, a
  lista enchia de peças de cenário e o inimigo ficava de fora — o lobo
  caía em cima dele e nada. Agora jogadores e bots entram por geometria
  (a esfera do golpe contra a cápsula do corpo), sem teto; a consulta fica
  só para o boneco do treino. O alvo travado tem 0,6 m de folga.
- Teste novo, `GolpeNoCenario`: inimigo cercado de 80 peças sólidas, a
  garrada e o especial têm de ferir. Falha no código antigo.

### Marcador de acerto mais alto, com "tic" na cabeça (18/11)
- Acertou alguém: um estalo seco com um "tuc" curto, bem mais alto que o
  antigo (`marcador_1..3`). Na cabeça, um "tic" de metal agudo por cima
  (`marcador_cabeca_1..2`). Só quem atirou ouve. Volume em
  `CombatantAudio.MarkerDb` e `HeadMarkerDb`. Sintetizados por
  `tools/sons/marcador.py`.

### Becos escuros, caveira do abate e som ambiente (18/11)
- **Arma e mão pretas no escuro**: a luz da mão (de 17/11) nunca tinha
  acendido — era posta na câmera dentro do `_Ready` de um filho dela, e o
  Godot recusava. Agora entra no quadro seguinte (energia 0,6).
- **Personagens no escuro**: uma luz fraca presa à câmera que só acende
  lobos e humanos (camada 18), sem sombra: perto de você eles não somem no
  beco, e o mapa não muda. `ViewModel.CharacterFillEnergy` (0,38) regula.
- **Caveira do abate** redesenhada (`tools/icones/skullsolid.svg`): mais
  limpa, com contorno escuro em volta, um clarão branco na hora do abate e
  um aro que abre e some.
- **Som ambiente do Armazém**: `ambiente_armazem` (o vento do pacote
  entrando pelo galpão, com o zumbido antigo por baixo), bem baixo. O
  fundo antigo era só grave (60 a 200 Hz) e em caixa pequena não se ouvia.
  `tools/sons/ambiente.py`; o volume é `AmbienceDb` em `Maps.cs`.

### Habilidades do lobo no HUD e guia no E (18/11)
- As habilidades do lobo viraram **quadradinhos** no canto direito, com a
  tecla embaixo: garrada (clique), bote (direito, no ar) e **uivo (R)** —
  que estava numa linha de texto e pouca gente sabia que existia. O
  quadradinho escurece enquanto recarrega e enche de cor enquanto se
  segura o R. O especial (Q) continua no disco do meio.
- **Segurar E** abre o guia das habilidades da forma atual (lobo ou
  humano): ícone, nome, tecla e uma frase para cada uma. Não pausa; soltou,
  some. Os números (dano da garra, duração do uivo) vêm da raça e do jogo.
- Ícone novo: escudo com raio (`IconKind.Shield`).

### Textos num arquivo só, pronto para outros idiomas (18/11)
- `assets/i18n/textos.csv` tem **todos os textos do jogo** (940): a coluna
  `texto` é o texto como está no código; `pt_BR` troca o que aparece em
  português sem mexer no código (renomear arma, botão, patente); `en`,
  `es`... são as traduções — vazia, aparece em português. Idioma novo é só
  mais uma coluna com o código dele.
- `Loc` (scripts/core/Loc.cs) carrega o arquivo no começo do jogo. Rótulo
  e botão com texto fixo o Godot traduz sozinho; texto montado com número
  ou nome passa por `Loc.F("... {0} ...", valor)` (113 foram convertidos) e
  caixa alta por `Loc.Upper`.
- Opções → **Idioma** aparece quando alguma coluna além do português tiver
  tradução (hoje estão vazias).
- `python3 tools/i18n/extrair.py` varre o código e atualiza o arquivo sem
  perder o que já foi traduzido; `--ligar` converte texto montado novo.
- Fora por enquanto: ItemCard, WeaponArt, Arsenal e os outros arquivos que
  estão com mudança local do AR-15 Ultimate (entram quando ele for
  commitado e o extrator rodar de novo).

### Sangue refeito e som de acerto no corpo (18/11)
- **Sangue sem formas geométricas**: antes tudo era disco (gota redonda,
  névoa redonda, mancha de círculos). Agora as formas são campos de
  metabolas gerados em código — borrões tortos, fios que afinam e se
  partem em gotas — e o material corta o campo num limiar: a borda é
  nítida em qualquer tamanho e o sangue **afina e se desfaz** em vez de só
  ficar transparente. Camadas: estouro no ponto, jato para onde o golpe
  empurra, gotas e respingo fino esticados no sentido em que voam, névoa.
- **Mancha molhada**: recebe a luz do lugar, brilha, e na parede
  **escorre** (dois ou três fios descem devagar). Seca encolhendo para
  dentro depois de 12 s.
- **Som do golpe entrando no corpo** (`impacto_carne_1..3`), no ponto do
  acerto, para quem está perto (26 m; de longe chega fraco até 60 m), e o
  **molhado do sangue** (`sangue_1..2`) por cima. Uma rajada não empilha
  dez impactos no mesmo instante. O marcador de acerto de quem atirou
  continua igual. Arquivos do "Free Sounds Pack" que o Jhon mandou,
  preparados por `tools/sons/acertos.py`.
- As formas do sangue são geradas na carga da partida, não no primeiro
  tiro que acerta.

### Uivo: pose, luz de regeneração, eletricidade e som de poder (18/11)
- **Pose do rugido** refeita nas pernas: um pé dá um passo curto à frente,
  os dois ficam chapados no chão e os joelhos apontam para a frente (o
  joelho era calculado para o lado errado quando o pé ia longe do quadril
  e dobrava para fora e para trás). A postura fica 5,5 cm mais baixa.
  Mesma duração (1,53 s), mesmo primeiro e último quadro.
- Saíram os **anéis de choque** na frente da boca; ficam os riscos de ar.
- **Luz de regeneração** (rugir enche o escudo): raios de luz descem em
  volta do lobo, fagulhas sobem do chão, uma poça de luz acende nos pés e
  **arcos elétricos** saltam do chão para o corpo, cada vez mais juntos.
  A luz é baixa de propósito (2,35 m dos pés, já transparente no topo):
  não passa por cima de parede nem entrega lobo escondido. O neon do
  corpo acende junto.
- **Escudo cheio**: rugiu até o fim, um anel sobe dos pés à cabeça com uma
  descarga elétrica para todos os lados. Soltar o R no meio não dá nada
  disso.
- **Som de poder** por cima do rugido: `lobo_uivo_energia` (o poder
  carregando: grave que sobe, bobina elétrica acelerando, estalos) e
  `lobo_uivo_escudo` (o poder solto: pancada grave, trovão, raio e o
  chiado do escudo — sem nota nem sino). Chegam a 22 m; quem entrega a
  posição de longe continua sendo o rugido. Soltar no meio corta o som da
  energia. Sintetizados por `tools/sons/uivo.py`.
- **Rugido mais agressivo** (`lobo_rugido_1`, por `tools/sons/rugido.py`):
  a criatura do pacote em camadas — mais grave, com a garganta rasgada do
  zumbi, a aspereza do rosnado e a faixa de 1,5 a 5 kHz que a gravação não
  tinha. Rosna baixo puxando o ar, explode junto com o pé batendo no chão
  (0,22 s) e cai de tom no fim.

### Sons do pacote do Jhon (18/11)
- **AR-15 com voz própria** (era a única arma no tiro e na recarga
  genéricos): tiro seu em estéreo, o dos outros, o distante, recarga em
  três passos e o ferrolho no fim (Free Weapon SFX v1.1). A skin usa os
  sons da arma dela (`Sfx.ModelOf`: `ar15_ultimate` → `ar15`).
- **Rugido do lobo** com gravação própria (`lobo_rugido`; antes tocava o
  som da transformação), **faca acertando** (`faca_acerto`) e mais duas
  variações de trocar de arma. Preparo em `tools/sons/pacote_armas.py`.
- Falta confirmar autor e licença dos dois pacotes (`assets/audio/CREDITOS.md`).

### Lobos polidos, rugido novo, efeitos novos (18/11, tarde)
- **Modelo** (`tools/lobo/polir.py`): cores mais vivas, pelo pintado (fios,
  mechas, raiz escura e ponta clara), oclusão assada, **neon de verdade**
  (textura de emissão: olhos, braceletes, peitoral, joelheiras,
  tornozeleiras, e nas costas do azul o LED vazando por baixo da placa);
  órbita escura em volta dos olhos do azul; poucos tufos pontudos no corpo
  do azul (malha `pelo`). O vermelho quase não muda de cor nem ganha tufo.
- **Material** `assets/efeitos/lobo.gdshader` (`WolfSkin`): o pelo balança
  (sopro + empurrão do movimento), o neon respira e acende com o poder,
  brilho de contorno no pelo. Vale também na vitrine do lobby.
- **Rugido** refeito de corpo inteiro (`tools/rugido/rugido2.py`): arma,
  explode, sustenta inclinado para a frente varrendo a cabeça, volta; os
  mesmos 1,533 s. No jogo, anéis de choque saem da boca, onda pelo chão e
  riscos de força (`RoarBurst`); a aura virou só contorno.
- **Especial**: os raios aparecem no lobo ao armar (Q) e ficam até o fim
  do golpe (`LightningAura`); o clipe do salto começa no impulso e a queda
  toca 1,7× mais rápida: a pancada cai junto com a chegada e o dano; **som
  de raio** ao armar, no salto e na queda, que o mapa inteiro ouve
  (`lobo_especial_*`, sintetizados por `tools/sons/raios.py`).
- **Garrada**: um clique, um golpe inteiro de um braço; 1,3× mais rápida
  (dano em 0,15 s), encadeia a cada 0,34 s; cliques rápidos ficam
  guardados (até 2); o golpe de um braço não esconde mais o do outro. Três
  riscos de neon no rastro da garra e no corte (`NeonSlash`).
- **Efeitos**: raios em fita de neon com galhos e onda de choque na queda;
  vento em fios finos que ondulam (`WindWisps`, no lugar dos traços retos);
  sangue com esguichos e estouro no ponto; eletricidade em volta do botão
  do especial pronto.
- Testes: `CliquesDaGarra` (novo); `EspecialArmado`, `CargaDoSalto` e
  `EfeitosDoLobo` acompanham.

### Especial armado, com trava e perseguição (18/11)
- Q **arma** o especial (carga cheia): o lobo segue andando pelo mapa, com
  a aura acesa, e o modo alvo abre — losango nos inimigos à vista, trava
  no mais perto da mira (até 20°; segura o mesmo até 32°). O clique
  esquerdo salta; Q de novo desarma sem gastar. Sem alvo, o salto vai
  para onde a mira toca o mapa, como antes.
- O voo **persegue o alvo**: andou para a frente, de lado ou para trás, o
  lobo cai onde ele está (até 18 m de onde saltou).
- **Escudo extra**: armado e no salto, o lobo leva metade do dano
  (`Weapon.SpecialGuard`, via `Health.GuardMultiplier`).
- A mira deixa de ser presa pela trava: o mouse gira sempre.
- Testes: `EspecialArmado` (novo); `CargaDoSalto`, `CtrlCliqueNoMac` e
  `EfeitosDoLobo` agora apertam Q e clicam.

### Luz do Armazém (18/11)
- ACES (branco 6, exposição 0,95) no lugar do AgX lavado; sol mais forte
  e quente (3,4) contra ambiente frio e baixo (0,38); oclusão forte e
  larga; GI 0,7; névoa com feixe de sol nas claraboias; brilho nas
  luminárias; contraste 1,1 e saturação 1,2. Gerador (`tools/mapas`) e as
  duas cenas regeradas.

### AR-15 Ultimate: a skin roxa com flames, na loja (18/11)
- **Arma nova à venda**, `ar15_ultimate` ("AR-15 Ultimate"): o AR-15 um pouco
  mais forte (dano 30, 700 RPM, pente 35, dispersão e recuo menores; números em
  `docs/ARSENAL.md` e `tools/gerar_arsenal.py`). Raridade épica, patente 3, em
  Presas. Entrou em `Arsenal.Primaries`, `Catalog`, `WeaponArt` e `WeaponPhotos`.
- **Pele roxa com flames, estilo Valorant**, nos dois modelos da arma: o rig de
  primeira pessoa (`assets/armas/fp/ar15_ultimate.glb`, com as animações do
  Jhon, sem mexer no `ar15.glb`) e o modelo da mão dos outros e do chão
  (`assets/armas/ar15_ultimate/`). Sai de `tools/armas/blender/ar15_ultimate.py`,
  que dá UV de verdade ao fuzil (a paleta não comporta desenho) e pinta a
  textura sobre a malha, mais uma textura de emissão.
- `WeaponModel.Material` liga a emissão quando existe um `_E.png` ao lado da
  textura de cor `_D.png`: as flames e as quinas brilham.
- Fichas: `armas.json` e `bracos.json` ganharam `ar15_ultimate` (copiadas do
  `ar15`). A foto da loja (`assets/ui/armas/ar15_ultimate.png`) é provisória,
  renderizada no Blender: rode `fotos_armas.tscn` para a do jogo.

### Locutora: uma fala por vez (18/11)
- Abate novo durante uma fala corta a que está tocando e diz a próxima
  (o "triple kill" se perdia embaixo do "double kill"), e abates quase
  juntos (escopeta, granada, 0,12 s) viram uma fala só, a maior: três de
  uma vez é "triple kill" direto. Teste `Locutora`.

### Sangue ao acertar; música do lobby mais alta (18/11)
- **Sangue** (`Blood`): tiro que acerta gente ou lobo solta um esguicho
  de gotas na direção do tiro, uma névoa vermelha curta e uma mancha na
  parede ou no chão logo atrás, que some depois de 12 s (no máximo 48 no
  mapa). A garra do lobo espirra para o lado do corte, maior; o salto do
  especial, maior ainda. Só sai quando o golpe fere: sem fogo amigo, sem
  a proteção de quem acabou de nascer. Dá para desligar em Opções →
  Vídeo → "Sangue ao acertar".
- **Música do menu e da sala 8 dB mais alta** (estavam bem abaixo da
  música de início da partida); a da sala subiu mais 3 dB para empatar
  com a do menu. A do resultado subiu 6 dB.

### A Iris narra a partida (18/11)
- **11 falas da Iris** no jogo: "Prepare for combat", 3-2-1, double,
  triple, quadra, penta e hexa kill, vitória e derrota (os nomes vieram
  do Jhon; os cortes, do áudio dele). Volume igualado e silêncio do
  começo cortado.
- **A voz da Kenney saiu**: "Fight", empate, um minuto e a reserva das
  sequências eram dela, e duas vozes na mesma partida soavam erradas. O
  que a Iris ainda não gravou fica em silêncio (lista em
  `docs/SONS_IA.md`), e `tools/sons/processar.py` não refaz mais as
  falas antigas.

### Música de início durante a contagem (18/11)
- `partida_inicio` (pedido do Jhon): entra quando a tela de carregamento
  sai, toca durante a contagem e acaba com ela, num fade de 0,8 s. Antes
  tocava na largada, e os 13 s dela caíam em cima dos primeiros tiros.
  `Sfx.Once` toca uma música uma vez devolvendo o tocador, para quem
  chama poder terminá-la.

### Primeiros sons gerados por IA: músicas, vinhetas e interface (18/11)
- **21 sons do Jhon** no jogo: tema do menu, música da sala, do último
  minuto e do resultado; vinhetas de início, vitória (uma), derrota (duas,
  sorteadas) e empate; e a interface inteira (passar, clicar, negado,
  abrir e fechar painel, equipar, comprar, jogar, fim da barra de XP,
  nova patente, recompensa). Convertidos de .opus, sem silêncio no
  começo, volume igualado ao resto do jogo (o clique estava 17 dB acima
  do antigo, o tema 13 dB) e as músicas com o laço fechado.
- **Ligados no código**: a música troca entre menu e sala com fade; a
  do último minuto entra faltando 60 s e sai no fim; a do resultado sobe
  por baixo da vinheta; loja, inventário, perfil e missões tocam abrir e
  fechar; equipar, comprar, resgatar missão, começar a partida, nova
  patente e o fim da barra de XP têm som próprio.
- Os sons antigos que eles substituem saíram (as variações de clique e de
  passar o mouse tocariam sorteadas junto) e `tools/sons/processar.py`
  não os refaz mais. `tools/sons/preparar_ia.py` prepara os próximos
  lotes.

### Especial em %, também no ar (18/11)
- **Carga em vez de recarga** (pedido do Jhon): o especial do lobo enche
  com abates (25% cada), assistências (10%) e, sozinho, bem devagar
  (cheio em 4 min). Cheio, o Q salta e zera. A carga enche também de
  humano e não cai ao morrer. O disco do especial mostra o % e fica
  sempre na tela; cheio e de humano, diz "PRONTO · VIRE LOBO".
- **Q no ar**: pulou e apertou Q, o especial sai dali, para onde a mira
  aponta (antes só no chão).
- Os bots seguem a mesma regra, então usam o especial bem menos.
- Teste novo `CargaDoEspecial`; `CargaDoSalto` mede o Q no ar e o Q sem
  carga.

### Rugido com as pernas (18/11)
- **`Howl` mais agressivo** (pedido do Jhon): agacha fundo com os
  joelhos abertos, bate o pé esquerdo à frente, sobe na ponta do direito
  e projeta o quadril. O joelho passa a ir de 73° a 150° (ia de 129° a
  151°). Só quadril e pernas mudam; duração, braços e momentos são os
  mesmos. Feito por `tools/rugido/rugido.py` em cima do clipe do Jhon —
  detalhes em `assets/characters/wolf/LEIA-ME.md`.

### Bots que caçam, viram lobo e jogam granada (18/11)
Pedido do Jhon: bots burros, previsíveis, sem se espalhar, sem granada,
sem lobo. Medido antes, em 90 s de partida com 7 bots: de 71 a 81% do
tempo em ronda, 1% de lobo, nenhuma granada.
- **Humor de lobo**: cada bot decide de tempos em tempos se é hora de
  lobo ou de arma (antes só virava lobo quase morto). De lobo, corre
  (1,3×) e chega em ziguezague. Medido depois: de 35 a 46% do tempo de
  lobo, 10 transformações por minuto.
- **Rádio do time e faro** (`TeamIntel`, `Bot.Persona.cs`): quem está à
  toa vai para onde um aliado viu inimigo, chegando pelo lado; sem
  notícia por 5 s, vai atrás de um inimigo sorteado. Em ronda: 29% do
  tempo. Tiros em 40 s: de 74 para 152.
- **Granada** (`Bot.Grenade.cs`): no alvo à vista, onde ele acabou de
  sumir e, em dobro, no lobo que vem. A cena do bot não tinha a granada
  atribuída — por isso, no online, a granada dos outros jogadores também
  não aparecia; a arma agora carrega a cena sozinha.
- **Personalidade**: distância preferida, gosto por lobo e lado do
  flanco sorteados por bot.
- **Rotas espalhadas**: cada bot numa rota a encarece para o próximo
  (`PatrolRoutes.Spread`); ronda mais rápida e sem parar em todo ponto.
- **Strafe consertado**: o relógio do lado do strafe só andava um quadro
  a cada decisão — o bot circulava para o mesmo lado por mais de meio
  minuto. Agora troca a cada 1 a 2 s, com um pulo de esquiva de vez em
  quando; rajadas de tamanho desigual; fogo no canto onde o alvo sumiu.
- **Erros do Output**: o clarão da granada animava uma propriedade que
  malha 3D não tem (um erro por explosão, e o clarão não desbotava); o
  boneco pedia o estado "Fall" que não existe sem o clipe (três erros a
  cada corpo no ar).
- **Aura no especial**: com o especial no Q a aura da carga tinha sumido;
  agora acende no voo.
- **Testes**: `BotsCacam` e `BotsJogamGranada` novos. `CargaDoSalto`,
  `CtrlCliqueNoMac`, `EfeitosDoLobo` e `PassadaDoLobo` ainda mediam o
  salto carregado no botão direito e quebraram com o Q (commit
  anterior, sem rodar os testes): reescritos para o Q.

### Tiros refeitos, voz do locutor, plano de áudio (18/11)
- **Tiros em camadas** (todas as armas, o seu e o dos outros): estalo da
  gravação + soco grave + cauda (a gravação de meia distância) + clique
  do mecanismo. Antes, 93–99% da energia ficava nos primeiros 30 ms e
  menos de 3% abaixo de 150 Hz; agora 54–80% e 11–15%. Ajustado por
  medida, sem ouvir — conferir no jogo. A escopeta ficou bem mais alta.
- **Locutor com voz**: `voz_prepare`, `voz_3/2/1`, `voz_lutar`,
  `voz_vitoria/derrota/empate`, `voz_multi_kill` (Kenney Voiceover Pack
  Fighter, CC0, em inglês). As falas próprias de cada sequência (double,
  triple…) e a música do menu continuam faltando.
- **`docs/AUDIO.md`**: referências (Overwatch, LoL), como cada som é
  feito, onde buscar as falas e a música que faltam, com as licenças, e
  o nome de arquivo que o jogo espera.

### Especial no Q com raios, menu do Esc no estilo do jogo (18/11)
- **O especial do lobo é o Q, num toque** (era segurar o botão direito
  no chão para carregar): salta na hora, com a carga cheia, para onde a
  mira aponta — ou em cima do inimigo que estiver na mira (até 8° do
  centro, `Player.PounceAssistAngle`). O botão direito, de lobo, ficou
  só com o bote no ar. O modo alvo do Tab saiu: sem carga, não há quando
  escolher; o Tab voltou a ser só o placar.
- **Raios** (`LightningBurst`): saem do corpo quando o especial dispara
  e correm pelo chão quando o lobo cai, na cor do personagem.
- **Menu do Esc** no estilo dos menus: a partida borrada e azulada atrás,
  cartões de vidro, as letras e os botões do menu (`UiTokens.MenuScope`).
- **Locutor**: o jogo passa a tocar `voz_prepare`, `voz_3/2/1`,
  `voz_lutar`, `voz_vitoria/derrota/empate` e `voz_multi_kill` (a fala
  de qualquer sequência enquanto as específicas não existem).

### WolfLeague, caveira na mira, DOUBLE KILL no alto (18/11)
- **O jogo se chama WolfLeague** na tela (topo do menu, entrada,
  créditos, título da janela: `Credits.GameName`). A pasta, o
  repositório, o nome do projeto no Godot e as variáveis `ALCATEIA_*`
  continuam como estão — trocar o nome do projeto mudaria a pasta do
  perfil salvo.
- **Escolha de modo**: cartões mais altos, com folga dos lados e o ícone
  numa ficha própria; os textos falam em Vanguarda e Sindicato.
- **Abate**: a caveira pulsa no centro da mira (estoura, assenta e some
  em 0,4 s: `HitMarker`); "ELIMINOU <jogador>" continua embaixo, acima
  do especial, agora só texto.
- **DOUBLE KILL** e as outras chamadas aparecem no alto, abaixo da barra
  do placar.
- **Cartão de duelo** só quando você morre; ao matar, não aparece mais.

### Feed de abates com história e sequências (17/11)
- **O abate leva mais detalhe** (`Kill`): o golpe do lobo (`WolfHit`:
  garra, o especial, o bote), **pelas costas** e **de cima**. Viaja no
  mesmo texto da arma ("ak47#c"), sem campo novo na rede.
- **Feed**: ícone próprio para a garra, o especial do lobo e o bote;
  marcas de pelas costas, de cima, na cabeça e **vingança** (a vítima
  era quem tinha te matado por último); a sequência de quem matou
  (×2, ×3…); e a foto da arma de verdade.
- **Sequências** (`KillStory`): DOUBLE, TRIPLE, QUADRA, PENTA, HEXA,
  HEPTA e OCTA KILL (até 5 s entre um abate e outro), TEAM KILL quando o
  time inimigo inteiro cai na sua sequência, e VINGANÇA — numa chamada
  grande no meio da tela (`KillBanner.Announce`).
- **Garras, não patas** (o Jhon: "lobo não tem patinhas fofinhas"): o
  ícone da garrada e o do especial do lobo são arranhões (`claw.svg`,
  `pounce.svg`), e a insígnia de Filhote troca a pata por três garras.
- **Falas**: o jogo toca `assets/audio/voz_double_kill` (…`triple`,
  `quadra`, `penta`, `hexa`, `hepta`, `octa`, `voz_team_kill`,
  `voz_vinganca`) se o arquivo existir. Os arquivos ainda não existem.

### Menus escuros, letras, ícones, patentes e fotos das armas novas (17/11)
- **Menus no escuro** (o claro ficou perto demais de Overwatch; Valorant
  e League of Legends de referência): azul-noite, cartões de vidro
  escuro, destaque em ouro, cantos arredondados e botões retos. A chave
  é `UiTokens.Menu` (era `Light`), que troca paleta, estilo e letras; a
  partida não muda. `MenuBackdrop` novo: névoa, clarão frio, brasa.
- **Letras** dos menus: Kanit (títulos e números, itálico de verdade) e
  Figtree (texto), as duas OFL, em `assets/fonts/`. A partida continua
  em Barlow.
- **Ícones** do Phosphor Icons (MIT) no lugar dos riscados por código:
  PNG em `assets/ui/icones/`, SVG e roteiro em `tools/icones/`.
- **Patentes**: 30 insígnias novas em imagem (`assets/ui/patentes/`),
  brasões facetados em metal com emblema e grau; `tools/patentes.py`.
- **Armas de verdade na interface**: fotos do próprio modelo do jogo
  (`assets/ui/armas/`, tiradas por `scenes/tools/fotos_armas.tscn`) na
  loja, no inventário, no HUD e no feed de abates.
- **Inventário** em três passos numerados (conjunto → espaço → item),
  com uma escolha só no alto (Armas · Lobo · Personagem · Especial).
- **Loja** com a lista de categorias à esquerda (com contagem e o
  saldo), no lugar das abas em dois níveis e do personagem em 3D.
- **Times**: o azul é a **Vanguarda**, o laranja é o **Sindicato**.

### HUD sem fundo, especial que acende, "ELIMINOU", vidro fosco no menu (17/11)
- **HUD da partida sem painel**: vida, munição e transformação direto
  sobre o jogo (contorno escuro, gomos com sombra), os blocos de baixo
  girados 3° para dentro, uma insígnia hexagonal da forma ao lado da
  vida (`HexBadge`). Feed e placar em placa chapada, sem borrar o mapa.
- **Especial do lobo** no inferior centro, como a suprema de Overwatch
  (`AbilityTile`): disco com aro e segundos em recarga; pronto, acende
  com halo, raios e uma onda.
- **Aviso de abate** (`KillBanner`): caveira e "ELIMINOU <jogador>"
  abaixo da mira para quem matou. Ícone novo `IconKind.SkullSolid`.
- **Menus**: os cartões brancos viraram **vidro fosco** sobre o fundo
  (`LobbyShell.Frost` + `HudStyle.Glass`); o `MenuBackdrop` ganhou uma
  luz quente no alto e um tom lilás embaixo, para o vidro ter o que borrar.

### Menus claros, estilo Overwatch com Valorant de inspiração (17/11)
- Os menus fora da partida passam à **paleta clara** (`UiTokens.Light`):
  fundo quase branco, cartões brancos com sombra curta, texto
  azul-marinho, destaque laranja. A partida continua na escura.
- **Fundo novo** sem o mapa: degradê claro, mosaico de triângulos, faixas
  diagonais e um fio laranja que andam devagar (`MenuBackdrop`).
- Insígnias de patente com placa sempre escura; desenho das armas, as
  Presas e as listras das vagas ajustados para o claro.

### Modo alvo no Tab, salto de um alvo só, mira redonda (17/11)
- **Modo alvo** no lugar da trava do botão do meio: carregando o salto,
  o Tab liga o modo alvo (não abre o placar) e marca os inimigos à vista;
  o clique trava no destacado (a mira segue ele, o anel no chão some);
  Tab de novo volta à mira no chão.
- **Salto de um alvo só**: o principal (o travado, senão o mais perto de
  onde caiu) leva o dano cheio; quem estiver do lado leva 30%
  (`PounceSplash`). A área caiu de 2 para 1,8 m.
- **Mira**: o ponto agora é redondo (era um quadrado).
- **Topo do menu**: a barra de XP mostrava XP negativo (-8.170 /
  11.960) — usava o XP local com a patente da conta. Agora usa o mesmo
  XP da patente (`Profile.RankXp`); o Perfil também.

### Menus no estilo low poly / Overwatch (17/11)
- **Fundo dos menus**: a foto do mapa borrada e escurecida, com mosaico
  de triângulos fraco, vinheta e zoom lento (`MenuBackdrop`).
- **Painéis translúcidos** sobre esse fundo (vidro), **cantos facetados**
  em todo o tema (`UiTheme.Box`), **botões e abas inclinados**, títulos
  e números na fonte condensada inclinada, botão primário mais forte.
- Janela de confirmação com o fundo borrado.

### W duplo cancela o salto carregado (17/11)
- Carregando o especial do lobo (botão direito no chão), apertar W duas
  vezes desiste do salto, sem saltar e sem gastar a recarga, e sai
  correndo de quatro. A carga cancelada (aqui, no menu do Esc ou no fim
  da partida) agora também para o clipe do salto (`WolfMoveCancelled`):
  antes o lobo podia tocar o pulo no lugar.

### HUD mais low poly, estilo Overwatch (17/11)
- Painéis de **vidro**: o mapa borrado e escurecido de fundo, em vez da
  caixa preta (`HudStyle`). Cantos cortados a 45° e painéis inclinados.
- **Números inclinados** e grandes: vida (58), munição (64), relógio,
  placar, WP, contagem.
- **Vida em gomos** de 25 pontos (`SegmentBar`), com o pedaço perdido em
  vermelho escorrendo; escudo do lobo em gomos laranja.
- Placar de cima com vidro tingido da cor de cada time, inclinado para
  fora; ícones de habilidade facetados com vidro; feed de abates em
  vidro; barras finas inclinadas.
- Menus por cima da partida (Esc, conjunto, resultado) com o mapa
  borrado de fundo.

### Patente bem mais lenta; a patente do topo bate com a da sala (17/11)
- **XP por partida caiu para um quarto**: 30 por participar, 4 por
  abate, 2 por assistência, 24 pela vitória, 12 pelo empate, 15 pelo MVP
  (`Rewards`, o mesmo no jogo e no servidor) — uns 100 numa partida
  comum, eram ~400. As missões também rendem um quarto do XP; as Presas
  não mudaram.
- **Curva mais íngreme e cada vez mais longa**: 230 × r^2,6 (era
  250 × r^1,9). A segunda patente sai em ~2 partidas, a terceira em ~14,
  Batedor I em ~40, e a última em ~14.600 (uns 8 anos a 5 partidas por
  dia). De uma patente para a outra, no fim, são ~1.270 partidas. Tabela
  em `Ranks.cs`. Quem já tinha XP pode cair de patente na tela, porque a
  curva mudou.
- **A patente do topo do menu não batia com a da sala**: o topo usava o
  XP local e a sala online o XP da conta no servidor. Logado, tudo usa o
  da conta (`Profile.OnlineXp`, lido ao entrar e depois de cada partida
  online); fora da conta, o local.

### Salto com área maior, trava no botão do meio, uivo cancela na parede (17/11)
- **Área do salto**: ao cair, pega quem estiver a até 2 m em volta do
  lobo (`PounceHitRadius`), em vez da garra à frente (que errava quem
  ficava embaixo ou do lado). O anel da mira tem esse tamanho.
- **Trava**: carregando o salto, segurar o botão do meio trava no
  inimigo mais perto da mira; a mira segue ele sozinha, o salto cai nos
  pés dele e a tela marca o travado (cantos vermelhos, "TRAVADO" e a
  distância). Soltar destrava. A barra de força avisa a tecla.
- **Uivo**: grudar numa parede corta o uivo (sem escudo).

### O Armazém maior vira o Armazém; fim de partida sem congelar (17/11)
- **Só um Armazém na lista: o maior**, com o nome "Armazém" (o id segue
  `armazem_estendido`). O original, menor, foi para `Maps.Guardados`
  com o teste dele; a sala padrão, o servidor e os testes passam a usar
  o novo. Sala ou perfil antigo com `armazem` cai no novo.
- **Fim de partida não congela**: como no menu do Esc, o corpo segue com
  a intenção vazia — o pulo, o golpe e a queda terminam, com a animação.
  Os bots terminam o golpe em curso e param. Só a contagem segura o
  corpo parado.

### A vida volta sozinha (17/11)
- 15 s sem levar dano e a vida sobe aos poucos, 8% da máxima por
  segundo, até encher (`Health.RegenDelay`, `RegenRate`). Vale para
  jogador e bots, de humano e de lobo; o escudo não volta assim.
- As granadas agora voltam só ao renascer (antes voltavam ao chegar à
  vida cheia, o que com a regeneração viraria granada de graça).

### Salto da parede com folga, menu sem congelar, salto maior, sombra nítida (17/11)
- **Salto da parede com folga**: soltou o W (ou a parede acabou) e
  apertou espaço até 0,22 s depois, ainda no ar: sai o salto da parede,
  como se ainda estivesse nela (`WallJumpGrace`). Antes era preciso
  apertar no mesmo instante.
- **O menu do Esc não congela o personagem**: o corpo segue com a
  intenção vazia — termina o golpe ou o pulo, cai, a animação continua —
  em vez de parar no lugar. O salto carregado é largado sem sair e o
  rugido é cortado. A contagem e o fim da partida continuam segurando o
  corpo.
- **Mira do salto do lobo** vai até 12 m (era 8,4; `PounceMaxDistance`).
- **Sombra mais marcada nos dois Armazéns**, sem mexer na luz: borda
  nítida (`shadow_blur` 0,5, era 1,5; `kit.borrao_sombra`). O contraste
  chegou a ir a 1,12, escureceu demais e voltou a 1,02.
- **Parede: batendo em algo, o lobo cai.** Correndo na parede e travado
  num obstáculo que não se contorna, em 0,15 s solta e cai
  (`WallStallTime`), em vez de ficar pendurado parado; por 0,35 s não
  gruda de novo (`WallStallCooldown`).

### Armazém (Extended): o Armazém maior e mais alto (17/11)
- **Mapa novo na lista**, ao lado do Armazém, que continua igual:
  75 × 45 m (era 46 × 30) e teto a 13 m (era 9). O miolo é o Armazém
  inteiro; as paredes andam para fora e esticam acima da passarela
  (janelas mais altas, passarela e escadas iguais); o telhado sobe
  junto. Nas pontas, alas novas com uma pirâmide de contêineres até
  8,7 m e corredores compridos junto às paredes. Detalhes em
  `docs/MAPAS.md`.
- Montado com as peças do mesmo pacote por
  `tools/mapas/blender/armazem_estendido.py`; o conversor do Armazém
  foi dividido em funções para os dois usarem a mesma leitura (o
  Armazém sai igual, byte a byte).
- Teste `MapaArmazemEstendido` (carrega, nascem os dois times, bots
  saem da base e trocam tiro). A foto do mapa foi feita no Blender;
  `tools/fotos_mapas.gd` já tem o ângulo para tirá-la no Godot.

### Minimapa cheio, luz, degrau, salto rápido, paredes e morte do humano (17/11)
- **Minimapa** fixo (norte para cima, a seta gira) e cheio: a janela
  tem o lado menor do mapa, segue o jogador e para na borda.
- **Armazém mais claro**: ambiente 0,85, exposição 1,45, oclusão 1,6
  (era 2,5) e uma luz de preenchimento sem sombra do lado oposto ao sol
  (`kit.preenchimento`). **Mãos e arma** ganham uma lâmpada fraca só
  delas (`ViewModel.HandFillEnergy`, camada 20): na sombra escurecem,
  mas não ficam pretas.
- **Degrau**: andando, o corpo sobe degrau de até 30 cm (lobo 45 cm),
  como a borda do contêiner; o olho vem atrás, sem tranco.
- **Salto do lobo**: carregado inteiro, voa em 0,36 s em vez de 0,8
  (`PounceFullFlight`); a animação do voo acelera junto e o dano sai no
  pouso. Mirando em cima de algo, sobe até 6,2 m (dois contêineres).
- **Parede do lobo**: o salto segue a câmera — olhando para a parede,
  sobe mais e afasta menos; ao longo dela, vai para aquele lado; olhando
  para cima, mais alto. Com o topo da parede ao alcance (1,9 m), o salto
  sobe nele. E só gruda numa parede **diferente** da última, até tocar
  o chão: escala-se de parede em parede.
- **Escada** 3,4 m/s (era 2,2). **Pulo do humano** mais baixo e mais
  rápido: 0,80 m em 0,67 s (era 0,99 m em 0,90 s); o lobo não muda.
- **Morte do humano** voltou: o `death.fbx` tinha saído no commit
  08a63e8 (16/11) junto com outros clipes, e sem ele o humano não
  tocava morte nenhuma. Restaurado. Saíram no mesmo commit, e seguem
  fora: `crouch_idle`, `fall`, `equip` e os `*_alt`.

### Lobo sem rachaduras, salto na mira, minimapa do Armazém (17/11)
- **Lobos (`lobo_azul.glb`, `lobo_vermelho.glb`): as "partes
  transparentes" nas costas e nos braços.** Três causas, as três no
  arquivo e nenhuma no jogo:
  1. As juntas (ombro, cotovelo, quadril, joelho) ganham triângulos
     extras, e a rotina que os cria dividia só uma aresta por triângulo
     (somava booleanos do numpy, que "somam" como OU). Sobravam 838
     vértices no meio da aresta do vizinho; com o corte para 4 ossos por
     vértice do arquivo, cada um abria uma fenda em cunha no movimento.
     Agora são 2, os mesmos do modelo original do Meshy.
  2. As peças do tronco (arnês das costas, peitoral, cinto) eram
     rígidas, presas a um osso só. O tronco dobra em três, e parado na
     pose do jogo a pele das costas cobria metade do arnês. Elas voltam
     a seguir a pele, como no modelo original; braçadeiras, joelheiras
     e caneleiras continuam rígidas.
  3. Onde uma peça é a própria pele (borda do cinto), o forro era o
     mesmo triângulo outra vez, na cor do pelo, e os dois brigavam.
     Esses triângulos do forro saíram.
  Cópias do mesmo vértice têm agora exatamente os mesmos pesos, já
  cortados para 4 ossos. Conferido em 12 poses dos clipes do jogo, com e
  sem as faces de trás: zero pixel com o interior à mostra no azul
  (eram 863); no vermelho, só a barra solta do colete, como no original.
- **Salto do lobo (botão direito no chão) cai onde a mira aponta.** O
  ponto em que o centro da tela toca o mundo dá a distância e a altura
  da queda, até 8,4 m; a carga só dá o dano. Mirando em alguém, o salto
  cai nos pés dele (antes o corpo contava como parede e a mira era
  ignorada); numa parede, ao pé dela; no céu, vale a barra, como antes.
  Sobe em contêiner de 2,9 m (`PounceMaxClear` 2,6 → 3,2), desce até
  6,5 m (`PounceMaxDrop`) e passa rasante embaixo de teto baixo. O tempo
  de voo continua o do clipe (0,8 s), para o dano sair quando os pés
  tocam o chão: distância e desnível dão as velocidades de saída.
  `PounceChargeLimitsReach` volta à regra de antes (a barra é o limite).
  Bots não mudam: escolhem a carga que leva até o alvo.
- **Minimapa do Armazém.** Estava vazio: ele só desenhava caixa com
  malha de caixa, e o Armazém é um modelo com caixas só de colisão.
  Agora desenha também essas, com a cor da peça no modelo (contêiner
  vermelho é vermelho), gravada pelo conversor em `armazem.json` e pelo
  gerador em `metadata/mapa_cor`. Peça com menos de 0,12 m² vista de
  cima não entra.

### Barra de vida verde e vermelha, Armazém menos escuro (17/11)
- A barra de vida sobre os personagens é verde no aliado e vermelha no
  inimigo, sempre, para quem olha (`HealthBar3D`); antes era a cor do
  time. A cor do time segue no corpo e no placar.
- Armazém: a sombra estava preta (ambiente 0,25). Ambiente 0,6,
  exposição 1,3, contraste 1,02, luz rebatida 1,5 e focos mais fortes.

### Lobos fechados por baixo da armadura, e ajustes (17/11)
- `lobo_azul.glb` e `lobo_vermelho.glb`: o forro (a pele inteira, uns
  milímetros para dentro, na cor do pelo) agora existe sob TODA peça —
  alças, peitoral, cinto, braçadeiras, bolsos —, uma folha só, com um
  vértice por posição soldada. Antes só havia sob a roupa cortada do
  corpo; sob as alças rígidas das costas e do peito não havia corpo, e
  quando a alça descolava via-se através do lobo. Sem a armadura, o
  corpo sozinho não tem mais furo (conferido no Blender, de costas e de
  frente). Azul 26,5 mil triângulos, vermelho 28,3 mil.
- Contra bots, cada jogador fica no time que quiser (não mais só no
  azul); um bot dá o lugar no time cheio. Caçar lobos é azul humano
  contra laranja lobo; Caçar humanos, o contrário.
- A luz rebatida (SDFGI) e a névoa do Armazém seguem a opção de
  qualidade (da Alta para cima); o atlas das sombras dos focos também.
- Modo espectador anotado em `PENDENCIAS.md`.

### Luz do Armazém (17/11)
- As luminárias do teto viraram focos para baixo com sombra
  (`kit.foco`): personagem e objeto fazem sombra no chão embaixo delas.
- Luz rebatida (SDFGI) e SSIL: a luz esquenta na parede cor de areia e
  volta para o chão; o ambiente plano caiu de 0,8 para 0,25 (contraste).
- Mapeamento de cor AgX (o da foto do mapa), sol mais forte e quente, e
  uma névoa volumétrica fraca que mostra o raio de sol das claraboias.
- Custa mais placa de vídeo: se pesar, `sdfgi` e `volumetrica` saem em
  `tools/mapas/armazem.py`.

### A sala online no visual da sala, sem a tela de treino (17/11)
- A sala online abre na `RoomScreen` (times grandes, regras à direita,
  chat embaixo), a mesma da sala offline; cada ação vai para o servidor
  e a tela se refaz com a resposta. Regras de patente, espectador e
  troca de lado passam a ir pela rede também.
- Saiu a lista "Treino com bots" (`RoomListScreen`, `RoomRow`) e os
  botões que levavam a ela. A aba Salas abre a sala em que você está, ou
  as salas online.
- As missões do dia (`DailyMissions`) entram na lateral das salas online.

### Modos de jogo, times de 1 a 6, foto dos mapas e tela de carregamento (17/11)
- Antes de criar a sala escolhe-se o modo (`PlayMode.cs`, `ModePicker`):
  Livre, Só humanos, Só lobos, Humanos × Lobos (trocam na metade),
  Contra bots, Bots só humanos, Bots só lobos, Caçar lobos e Caçar
  humanos. A forma de cada time vale no `FormController` (nasce e
  renasce lobo quando o modo manda; tecla 3 barrada quando não pode).
- Bots só nos modos contra bots: os jogadores ficam no azul e o mestre
  põe quantos bots quiser em cada time (offline e online); contra
  jogadores não há bot nenhum (o servidor recusa).
- Times de 1 a 6 jogadores, na criação e na sala; o servidor de salas
  aceita de 1 a 6 e o campo "play" (o modo). O Armazém tem 6 pontos de
  nascimento por time.
- Foto de cada mapa (`MapInfo.Cover`) na escolha do mapa, na sala e no
  carregamento; `tools/fotos_mapas.gd` tira no próprio jogo.
- Tela de carregamento (`LoadingScreen`): foto em tela cheia, nome,
  modo, tempo, pontos e times, descrição e barra com o progresso real;
  some quando a partida está montada.

### Escadas que sobem e descem, iguais em todo mapa (17/11)
- `scripts/core/Ladder.cs` (o volume da escada) e `Player.Ladder.cs` (o
  que o corpo faz nela): W sobe, S desce, olhando para baixo o W desce,
  A/D de lado, pulo solta para trás, no alto sai para o piso de cima.
  Humano e lobo. Números em `AJUSTES.md`.
- `kit.escada(...)` põe a escada em qualquer mapa por código: o volume e
  uma chapa lisa de colisão no lugar dos degraus.
- Armazém: as 6 escadas de marinheiro das paredes compridas levam às
  passarelas (3,63 m). Os bots ainda não usam escada.

### Mapa novo, o único da lista: Armazém (17/11)
- Galpão fechado de 46 × 30 m, do pacote LowPolyFPS4 (Unity Asset Store)
  que o Jhon trouxe: contêineres que viram túnel (as duas pontas abertas,
  portas à parte), pilhas de dois a 5,8 m com pranchas por cima, teto a
  9 m com claraboias.
- O FBX exportado da cena da Unity veio sem UV (tudo cinza). O
  `tools/mapas/blender/armazem.py` casa cada uma das 714 peças com o
  modelo original do `.unitypackage` (713 exatas; um palete com 11 cm de
  erro) e copia a UV de lá. Grava `assets/mapas/pecas/armazem.glb` (seis
  pedaços de desenho + a casca do galpão como colisão de malha) e
  `tools/mapas/armazem.json` (611 caixas de colisão orientadas, 24 luzes,
  os vidros).
- `tools/mapas/armazem.py` monta a cena: colisão, luminárias do teto, o
  sol entrando pelas claraboias (vidro sem sombra), cada time
  nasce junto (8 pontos, 4 × 2, a 1,5 m), no canto que o Jhon marcou
  (azul em −X/+Z, laranja em +X/−Z) e sete
  rotas de ronda. Teste `MapaArmazem` no lugar de `MapaVila` e `MapaPolar`.
- As cores de cada peça vêm da cena da Unity (`LowPolyFPS4_Demo.unity`,
  lida pelo conversor): contêiner vermelho, azul e amarelo, barris de
  várias cores, parede cor de areia. Peças de mesma malha que só mudam
  de cor (166 delas) tinham saído todas na primeira cor.
- Facetado como o original: a solda dos vértices tinha apagado as
  normais e deixado tudo com sombreado suave (o "embaçado"). A paleta
  entra sem mipmap e sem compressão (de longe e em ângulo, o mipmap
  misturava as cores vizinhas).
- Renascer: cada um volta sempre ao ponto que recebeu na largada, na
  base do time (`SpawnPoints.SetHome`), com os 3 s de proteção.
- Só o Armazém está na lista (`Maps.All`). A Vila e a Estação Polar
  ficaram em `Maps.Guardados`, com cena e gerador intactos. O mapa padrão
  da sala, do servidor e dos testes passa a ser o Armazém.

### Lobos novos dos times: azul e vermelho (17/11)
- O lobo do time azul é o `lobo_azul.glb` e o do laranja o
  `lobo_vermelho.glb` (os dois do Jhon, no Meshy). O antigo continua em
  `mutant.fbx` / `mutant_red.fbx` e segue como referência de altura do
  teste `Personagens`.
- Três malhas: `corpo`, `roupa` e `equipamento`, com uma textura só.
  Cada placa da armadura segue um osso só e não entorta; as joelheiras
  ficam entre coxa e canela (70/30). A placa do peito saiu do osso da
  cabeça (descolava do peito na corrida de quatro).
- Embaixo das placas foi feito o corpo que faltava (vem do lobo "Clean"
  do Meshy), preso à placa. Sob a roupa cortada do corpo (calça, alças)
  há um forro na cor do pelo, com os mesmos vértices e pesos da roupa:
  nada fica vazado por trás e ele não atravessa o tecido.
- Importados sem LOD (`meshes/generate_lods=false`): com três malhas, o
  Godot simplificava cada uma por conta própria e abria vãos nas emendas
  (costas e frente pareciam transparentes).
- Mesmo esqueleto de 24 ossos, 1,66 m, sem animações; cerca de 20 mil
  triângulos por lobo (antes 15 mil).

### Só dois mapas: Vila e Estação Polar (16/11)
- Saíram o Hangar, o Metrô e a Rua Abandonada (esta entrou e saiu no
  mesmo dia), a pedido: cenas, peças (o trem, os letreiros), geradores
  e testes. O mapa padrão da sala, do servidor e dos testes passa a ser
  a Vila.
- A Vila tinha dois nós com o mesmo nome no mesmo lugar (as pipas da casa
  e as da porta, `Pipa1`/`Pipa2`): no servidor, ao tirar do mapa o que
  não colide, o Godot dava "Children name does not match parent name".
  As da casa agora são `PipaCasa1`/`PipaCasa2`.
- Ficou o "mapa feito no Blender" em docs/MAPAS.md (como trazer um mapa
  pronto: escala, chão como malha, sentido dos triângulos para a
  navegação, paredes invisíveis).

### Mãos do arquivo do Jhon (16/11)
- As mãos padrão (`assets/armas/fp/maos/`) agora são os braços do
  `RIG_Manny.blend` (`SK_Manny_Arms`), os mesmos do Blender, com o
  material dele (cor, normal, aspereza; sem AO). Saem do `ar15.glb` por
  `tools/armas/blender/maos.py`. Continua valendo: trocar a textura em
  `padrao.tres` troca as mãos de todas as armas.
- O AR-15 foi reconvertido da versão final do Jhon (`AR15_FP`, 16/11 às
  18h: cena e `RIG_Manny.blend` novos). As 6 animações (parado, andar,
  sacar, guardar, tiro, recarga) são as dela. O `infima.py` agora lê da
  cena onde ela procura cada biblioteca, e aceita cena sem a ação do
  fuzil na recarga (esta não tem: o carregador vai na mão pela própria
  cena).

### AR-15 com a recarga do Jhon (16/11)
- A recarga do AR-15 é a animação do Jhon (`A_FP_AssaultRifle_Reload`,
  2,7 s), com a ação do fuzil junto: a mão esquerda tira o carregador e
  põe de volta. Entra pelo `infima.py` (a recarga e a ação do fuzil
  foram acrescentadas à lista) e substitui a recarga de código. O jogo
  estica a animação para o tempo da recarga (2,3 s; 2,9 s com o pente
  vazio).

### AR-15 com textura (16/11)
- O fuzil saía cinza: a textura não veio no pacote. A do Jhon
  (`assets/armas/ar15/T_AssaultRifle_D.png`, uma paleta de 256 px que o
  modelo pinta peça por peça pelo UV) está no fuzil da primeira pessoa
  (embutida no `fp/ar15.glb`) e no da mão dos outros e do chão
  (`armas.json`).
- O `infima.py` acha a textura em `assets/armas/ar15/` quando o pacote
  não a tem, e o sufixo `_D` (diffuse) agora conta como cor: na próxima
  exportação ela não se perde.

### Personagens: a Eve, uma pasta por personagem (16/11)
- **Personagem jogável**: humano e lobo juntos, escolhidos no inventário
  (aba **Personagem** no lado do equipado, um lugar por time; aba
  **Personagens** na grade). Escolheu a pessoa, transforma no lobo dela.
  Sem personagem, o time usa o de sempre (Exo Gray / Ely).
- **Eve**, do time laranja: humano do Mixamo e loba do Meshy, com **10%
  a mais de vida nas duas formas** (220; o AR-15 passa a precisar de 5
  tiros no corpo em vez de 4). Vem com o perfil.
- **Uma pasta por personagem** em `assets/personagens/` (`personagem.tres`
  com nome, time, descrição, raridade e vida; `humano.fbx`; `lobo.glb`).
  O catálogo acha as pastas sozinho.
- **Mesmas proporções**: a Eve humana veio com 1,39 m e é levada a 1,79 m
  na importação (`root_scale` 1,2868); a loba, de 1,68 m a 1,66 m. A
  cabeça que leva tiro é a do modelo: personagem mais baixo seria mais
  difícil de acertar.
- **Guia** `docs/PERSONAGENS.md` e ferramenta `tools/personagem.sh`
  (medir, preparar o lobo no Blender, a escala do humano). Teste
  `Personagens`: altura, ossos, vida e o corpo montado com os modelos de
  cada pasta.
- O detalhe do item mostra o personagem em 3D, humano e lobo.

### Lobo: andar agachado da carga do salto (16/11)
- **`Charge_Walk`** (17º clipe do `lobo_anim.glb`): andando com o salto
  carregado, o lobo anda agachado na mesma pose em que a carga segura,
  passos curtos e pernas abertas. Antes tocava o `Crouch_Walk` (um joelho
  no chão): ao soltar o W ele trocava de postura. Patinação 0,14 m/s de
  frente, 0,11 de lado, 0,10 para trás.
- **Mais devagar**: `WolfChargeMoveFactor` 0,45 → 0,234 (0,91 m/s no
  Bruto, a passada do clipe, `ChargeWalkClipSpeed` 0,91).
- **`Special_Attack`**: no ponto em que a carga segura (0,47 s) a pose é o
  agachamento mais fundo, garras à frente; antes era um quadro de
  passagem, com os braços subindo. O resto do salto e os tempos não
  mudam.

### Lobo: rugido no ar segue o pulo, carga sem patinar de lado (16/11)
- **R no ar**: ruge no ar, e o corpo segue o pulo para a frente como
  vinha até pousar (antes o comando de andar saía e ele perdia embalo no
  ar); no chão, rugindo, para.
- **Andando com o salto carregado de lado ou para trás, a pata patinava
  2,6 m/s**: a carga conta como golpe e prendia o corpo virado para a
  mira. Agora ele vira para onde anda (0,25 m/s de lado, 0,2 para trás,
  como o engatinhar) e, parando, fica como está; ao soltar, vira para a
  câmera. O `PassadaDoLobo` mede a carga nas três direções e o
  engatinhar de lado.

### Lobo: anda carregando o salto, garrada rápida, corrida de quatro +15% (16/11)
- **Carregando o salto o lobo anda**, encolhido, no passo do agachado
  (`WolfChargeMoveFactor` 0,45, ~1,75 m/s no Bruto): toca o `Crouch_Walk`
  por cima da pose da carga e vira para onde anda; parado, volta à pose
  do agachar respirando. A mira acompanha. Soltou, o salto sai de onde
  ele está, para a câmera.
- **Garrada rápida**: a próxima corta a atual 0,08 s depois do impacto
  (`ClawCancelAfter`): clicando rápido sai uma a cada 0,28 s em vez de
  0,733 s (até ~3,6 por segundo; o dano por segundo da garrada sobe na
  mesma proporção, bots incluídos). Teste `GarradaRapida`.
- **Corrida de quatro 15% mais rápida**: `WolfSprintFactor` 1,3 → 1,495
  (Bruto a 6,2 m/s; o `Run_Quad` toca a ~1,58×). Patinação de quatro
  0,64 m/s (era 0,56); a freada leva 7 quadros (o `PassadaDoLobo` aceita
  até 9 de quatro).

### Lobo: rugido parado, carga do salto com mira, braços no pulo (15/11)
- **Rugindo o lobo para**: apertou R andando ou no ar, o comando de andar
  sai e o `Howl` toca de corpo inteiro, como foi animado (decisão do
  Jhon; o rugido somado ao passo, `Howl_Add`, saiu). No ar ele segue o
  arco do pulo.
- **Carga do salto**: o lobo fica preso no lugar (dava para andar
  encolhido, deslizando), mantém a pose do fim do agachar e **respira**
  nela: `Breath_Add` (o 16º clipe do `lobo_anim.glb`) é a respiração do
  `Idle_Pose`, 5,5× mais funda, somada por cima; `ChargeBreathRate` 1,6.
- **Mira do salto** (`PounceAim`): carregando, um anel marca onde o lobo
  pousa se soltar agora — laranja, vermelho com a carga cheia. Usa o
  plano do salto de verdade (`Weapon.PounceLanding`, pelo `PlanPounce`):
  aparece em cima do caixote ou do teto que o arco alcança, e antes do
  muro que ele não passa.
- **Pulo e queda com os braços mais fechados** (`Jump_Start`, `Jump_Air`,
  `Jump_Land`): abriam 54° para os lados, agora 22°, com o cotovelo mais
  dobrado.
- Teste `CargaDoSalto`: rugindo o lobo para e a mão sobe; carregando não
  anda (mesmo vindo andando), o quadril fica e o peito respira; a mira
  está no ponto da arma, o lobo pousa nela no chão e em cima de um bloco
  de 1,5 m.

### Lobo: volta no tonel, recuo mais lento, rugido de corpo inteiro (15/11)
- **Parede redonda**: o lobo contorna tonel, tanque e pilar cilíndrico a
  volta inteira. A curva contava como "ponta de parede fina": ele saía
  reto, caía, grudava de novo e repetia (Jhon, tonel da Estação Polar).
  Teste `VoltaNoTonel` (442° em 3 s sem soltar; sem a correção, 80° e
  queda).
- **Para trás o lobo anda a 75%** (`WolfBackFactor`, pedido do Jhon); na
  diagonal de trás, em parte. O humano não muda. `VelocidadeNoChao`
  espera a velocidade de frente × o fator.
- **Rugido parado toca de corpo inteiro** (as pernas do `Howl`); andando,
  da cintura para cima como antes. Saiu andando no meio, o rugido sai.
- **`Special_Attack`**: a recuperação depois da pancada não fica mais
  parada (0,3 s quase sem movimento): levanta de uma vez, com a cabeça
  subindo e sacudindo. Pancada em 1,33 s e duração de 2,2 s, como antes.

### Inimigo marcado com brilho no ombro (15/11)
- **No lugar da borda vermelha no corpo inteiro, um brilho vermelho no
  ombro esquerdo** do inimigo (pedido do Jhon). Preso ao osso do braço,
  do mesmo tamanho no humano e no lobo (30 cm de halo, 11 cm acima da
  junta), pulsa de leve, não atravessa parede e apaga quando ele morre.
  A silhueta de quem te matou continua como era.
- Teste `MarcaNoOmbro`: o brilho em cada esqueleto do inimigo, na junta
  do ombro, sem a borda; apaga morto; aliado e o próprio jogador sem
  brilho. Com `-- --fotos=pasta`, fotografa o humano e o lobo inimigos.

### Lobo: animações refeitas na velocidade do jogo (15/11 à noite)
- `lobo_anim.glb` trocado (mesmos 15 nomes, 691 KB). **Os quatro andares
  em pé viraram um trote curvado** de 0,67 s por ciclo, feito na
  velocidade em que o lobo anda: tocam perto de 1× (o de frente tocava a
  ~3× e os de lado a 5,5×). O pé de apoio anda a velocidade constante no
  arquivo. Passadas: `WalkClipSpeed` 3,95, `WalkBackClipSpeed` 3,6,
  `WalkSideClipSpeed` 3,75; `StrideWolf` 1,32 m (som do passo).
- **Agachado andando**: `Crouch_Walk` com ciclo de 1,0 s e passada maior
  (`CrawlClipSpeed` 1,62): toca a ~1,1× em vez de 1,8×.
- **Parado** sem o quadril deslizar de lado; **garradas** com giro de
  tronco e a mão cruzando na altura do peito (acerto em 0,20 s, 0,733 s
  no total: `WolfMoves` não muda); **rugido** com as pernas (1,533 s).
- As passadas saíram da conta no arquivo (pé de apoio × 1,13) e a
  varrida no `PassadaDoLobo` confirmou. Patinação: de frente 0,13 m/s
  (era 0,16), de lado 0,14 (0,50 a 0,67), para trás 0,22 (0,19), na
  diagonal 0,14, engatinhando 0,23 (0,25).
- `PassadaDoLobo`: o limite de "pés depois de parar" em pé foi de 1,9
  para 2,9 m (o trote para com as pernas mais abertas que o andar: 1,7
  a 1,8 m de frente, 2,2 a 2,7 de lado). Com `-- --fotos=pasta` e tela,
  o teste fotografa o lobo de lado em cada fase. `tools/medir_lobo.sh`
  roda os testes do passo e guarda a saída em `.medidas/`.

### Lobo: pé no chão, câmera mais perto, corrida de quatro sem patinar (15/11)
- **O pé parou de patinar**: os clipes do Jhon mantêm o vaivém do
  quadril dentro do ciclo (antes era tirado como deslocamento de raiz, e
  o pé de apoio andava junto). Patinação: de frente 0,16 m/s (era 0,66),
  para trás 0,19 (0,92), de lado 0,67 (1,75), engatinhando 0,25 (0,41).
  Passadas medidas de novo: `WalkClipSpeed` 1,4, `WalkBackClipSpeed` 1,0.
- **Corrida de quatro acompanha o corpo**: 5,4 m/s (`WolfSprintFactor`
  1,3, o padrão do código; o `player.tscn` tinha 1,45) e o clipe a 1,37×
  (`SprintClipSpeed` 3,95): um pouco mais rápido e sem patinar (0,56 m/s,
  era 2,46). O som continua uma vez por ciclo, quando as patas se juntam.
  O `player.tscn` perdeu `WolfSprintFactor` e `SprintAnimScale` (aval do
  Jhon).
- **Parado em pé o lobo desce 2 cm** (`FootSink`): a pata só encostava no
  chão pelas garras e ele parecia flutuar.
- **Câmera do lobo mais perto**: `ThirdPersonDistance` 2,5 m (era 3,5).
- `PassadaDoLobo` com limites bem mais apertados (de frente 0,4, de lado
  0,9, de quatro 0,9) e passando inteiro.

### Animações novas do lobo, todas do Jhon (15/11)
- `lobo_anim.glb` trocado pelo `Wolf_animations.glb` do Jhon (15 clipes,
  722 KB só com esqueleto e clipes). Entram: **andar nas quatro
  direções** (de frente para a mira; nas diagonais o corpo vira o resto),
  **garradas** (`Attack_Primary`/`Secondary`), **rugido** (`Howl`), **pulo**
  em três partes (`Jump_Start` ao sair do chão pulando, `Jump_Air` em
  laço, `Jump_Land`) e o **`Special_Attack`** como salto, bote e pouso do
  bote. Atualizados: parado, agachado, engatinhar e corrida de quatro. Do
  Mixamo ficam só a morte e a corrida na parede.
- **Animações na velocidade em que foram feitas** (pedido do Jhon):
  garradas 0,73 s com o dano em 0,20 s (eram 0,39 e 0,14), salto 2,2 s com
  o dano em 1,33 s (eram 2,14 e 1,14), rugido 1,53 s (era 1,6), pouso do
  bote 0,87 s (era 0,78), pouso do pulo 0,77 s.
- **Em pé o lobo só anda, acelerado até a velocidade do corpo** (4,15
  m/s): o pé não desliza. Passadas medidas no jogo: `WalkClipSpeed` 1,45,
  `WalkBackClipSpeed` 1,15, `WalkSideClipSpeed` 0,75 (novos),
  `CrawlClipSpeed` 1,05. Patinação: de frente 0,66 m/s, para trás 0,92,
  engatinhando 0,41; de lado 1,35 a 1,75 (o clipe toca a 5,5×).
- **Corrida de quatro a 1×** com a velocidade de sempre (6 m/s; o pé do
  clipe anda 4,1, então desliza 2,5 m/s). `SprintAnimScale` agora é o
  ritmo (1 = o do Jhon); o `player.tscn` traz 0,5 e precisa de 1.
- **Som da corrida de quatro: um passo por ciclo, quando as patas da
  frente e as de trás se juntam** (0,567 s do clipe). A corrida sempre
  começa do começo do clipe e o som conta pelo relógio dele
  (`WolfVisual.SprintBeats`), então cai no quadro certo. Em pé, a
  passada do som é a do andar que está tocando.
- Testes: `PassadaDoLobo` com os limites novos (de lado 2,0; para trás
  1,0; de quatro 2,8 a 1×; parada em pé 1,5 m); `PassoNoRitmo` confere a
  batida da corrida de quatro (primeiro passo em 0,57 s, um a cada 0,6 s);
  `WolfMovesTests` com os tempos novos.

### Lobo do Meshy com as animações do Jhon (15/11)
- O lobo do jogo é o do Meshy de novo, agora com as **animações que o
  Jhon fez nele** (`assets/characters/wolf/lobo_anim.glb`, 487 KB, só
  esqueleto e clipes): parado, andar agachado, agachado parado, engatinhar
  e corrida de quatro (`Idle`, `Crouch_Idle`, `Crouch_Walk`, `Run_Quad`).
  `Walk` só aparece nas misturas (o lobo não tem andar com Shift). O resto
  (garrada, soco, rugido, pulo, bote, morte, correr em pé e na parede)
  continua do Mixamo; o `Special_Attack` ainda não toca (`PENDENCIAS`).
- Modelos: `mutant.fbx` (azul) e `mutant_red.fbx` (vermelho) vêm dos
  arquivos novos do Jhon (`werewolf_animado.glb`, `wolf_red.fbx`), com
  texturas de 2048 px e **normalizados para 1,66 m** (o azul tinha 1,80).
- `WolfOwnClips` encaixa cada clipe no corpo: `AnimationKit.RetargetPose`
  ganhou `referenceFrame` e `bodyFrame` (o `Armature` do GLB tem escala
  0,01 e giro de 90°) e o quadril converte pela razão das alturas.
  Vitrine do lobby usa o mesmo parado.
- Passadas medidas no jogo (pé de apoio): `WalkClipSpeed` 1,36,
  `CrawlClipSpeed` 1,4 (patina 0,29 m/s) e `SprintClipSpeed` 2,7 com a
  escala 0,5 da cena (patina 0,19 m/s a 6 m/s; regra: velocidade do pé 5,4
  × `SprintAnimScale`). `StrideWolfSprint` 2,88 m: uma batida por ciclo
  (0,533 s), quando mãos e pés se juntam. `PassoNoRitmo` e
  `CorridaDeQuatro` passam a ler o clipe novo e a escala da cena.
- Agachado: a cabeça fica a 1,25 m do chão, 25 cm acima da cápsula
  agachada (1,0 m); anotado em `PENDENCIAS`.

### Lobo novo, do Meshy (15/11)
- O lobo do jogo é agora o **lobo tático que o Jhon fez no Meshy**, no
  lugar do Mutant do Mixamo: `mutant.fbx` (azul) e `mutant_red.fbx`
  (laranja, a mesma malha com a pelagem em ferrugem). Esqueleto
  `mixamorig` de 24 ossos, pose A, pesos do próprio Meshy; texturas
  reduzidas de 4096 para 2048 px (18 MB cada FBX, não 53).
- **Animações iguais, adaptadas ao corpo na carga**: `AnimationKit.
  RetargetPose` mantém a direção de cada osso no mundo (os clipes são do
  T do Mutant; o lobo novo é em A e com os eixos dos ossos girados), e o
  quadril sobe na razão das alturas. `WolfVisual.FitClip` faz isso em
  todos os clipes (antes só a corrida de quatro era ajustada). Com o
  Mutant antigo o resultado é o mesmo de antes.
- Medido: o pé mais baixo fica a −2 mm do chão no andar e no correr
  (−17 mm engatinhando); patinação de 0,70 m/s (era 0,82) e 3,1 m/s na
  corrida de quatro (era 3,25). `PassadaDoLobo` aceita 1,25 m de
  deslize ao parar (eram 1,1).
- Crédito do Meshy em `Credits.cs`; a licença do plano dele precisa ser
  conferida antes de publicar (`PENDENCIAS.md`).

### Corrida de quatro do lobo, versão 2 (15/11)
- `sprint.fbx` trocado pelo `Quadruped_Run_WolfTeam_v2` do Jhon: ciclo de
  0,867 s (era 0,667), com quatro pisadas — as duas mãos perto de 0,1 s
  e os dois pés perto de 0,45 s. O som do passo cai entre elas: duas
  batidas por ciclo, a cada `StrideWolfSprint` = 1,99 m (era 1,77).
- Velocidade do clipe `SprintClipSpeed` 4,6 m/s (era 5,3): varrida de 3 a
  6 m/s no teste de patinação, o mínimo (1,3 m/s) fica entre 4,5 e 4,8.
  Quadril sobe 6,2 cm (`SprintHipsLift`): o pé mais baixo fica a −1 cm.

### Corrida de quatro nova do lobo (15/11)
- O lobo correndo de quatro (W duas vezes) toca **a corrida de quatro
  patas do Jhon**: o "Quadruped Run" do Mixamo editado por ele no
  Blender (`assets/characters/wolf/sprint.fbx`). Antes era o "Running
  Crawl" acelerado; engatinhar agachado continua com ele.
- O clipe é do esqueleto do X Bot e veio do Blender com a pose salva
  como repouso. No Mutant: as trilhas passam a apontar para o esqueleto
  dele (`AnimationKit.PointAt`), os ossos sem trilha ficam no repouso do
  clipe (`AnimationKit.KeepClipRest`; sem isso os ombros iam para o T do
  Mutant) e o quadril desce na razão das pernas e sobe 5,8 cm: o pé de
  trás desce até o chão como no correr, e a mão grande do Mutant encosta.
- Passada medida: 5,3 m/s (`SprintClipSpeed`, a média das patas no
  chão), o que toca o clipe quase no ritmo do Blender a 6 m/s; o som do
  passo segue (`StrideWolfSprint` 1,77 m). PassadaDoLobo aceita até
  3,6 m/s de patinação de quatro (3,25 medido: no galope o pé de trás
  varre o chão no fim do impulso).

### AR-15, o fuzil padrão novo (15/11)
- **Arma nova: AR-15**, o fuzil do conjunto padrão (no lugar da M16A2).
  Modelo de TastyTony (silenciador, mira red dot, coronha retrátil) e
  animações de primeira pessoa **feitas pelo Jhon** com o pacote da
  InfimaGames (CC BY 4.0): parado, andar, sacar, guardar e tiro, com o
  ferrolho e o gatilho do fuzil animados no tiro. Começa com os números
  da M16A2 de 14/11.
- **A M16A2 continua**, agora à venda (raridade 1, a mais barata dos
  fuzis). Quem já tinha continua com ela.
- `tools/armas/blender/infima.py` exporta **a cena do Jhon como ela
  está** (`Animations_Assault_Rifle.blend`, com `RIG_Manny.blend` e
  `RIG_AssaultRifle.blend`): os braços dele (o X Bot do Mixamo nos ossos
  do manequim), o material dele, a câmera dele (95° na horizontal, 63,09°
  na vertical na ficha, `fov`) e as ações dele com os constraints do rig
  (a mão esquerda presa no carregador), copiadas osso por osso a 60
  amostras por segundo. Sem retarget e sem ajuste: renderizado pela
  câmera dele e no jogo, só difere nas bordas. Também grava o modelo para
  a mão dos outros e o chão (`assets/armas/ar15/`); `tools/gerar_armas.py`
  passou a preservar modelos de artista.
- Primeira pessoa (`ViewModel`): o AR-15 **saca animado** (até 0,4 s) e
  **guarda animado** ao trocar para outra arma, no tempo da animação do
  Jhon (0,5 s, pedido dele): a outra só sobe depois, e atira 0,2 s depois
  de subir. O tempo é da arma (`WeaponDefinition.HolsterTime`, o mesmo
  no servidor), igual ao do `guardar` na ficha. Nada de código por cima da animação do Jhon (`"fiel"` na
  ficha): sem balanço do passo, atraso ao olhar, coice nem afundar no
  pouso — andar, parado e tiro são exatamente os dele. O pacote não tem recarga: a do AR-15
  é de código (a arma inclina, o carregador cai e volta, o ferrolho é
  puxado com o pente vazio — `RigPieces`, um modificador do esqueleto).
- **Braços:** os do Jhon (o X Bot do Mixamo recortado, no rig do
  manequim), não a malha da Epic, que não entra no jogo.
- HUD: desenho do AR-15 no ícone da arma.

### Ajustes de 14/11: tiro na cabeça, M16, parede, rugido, escudo
- **Tiro na cabeça sem dano** (o Jhon: "mirei escopado exatamente na
  cabeça do humano, e não deu dano"). Dois defeitos:
  - o recuo subia a câmera **antes** de o tiro sair, então todo tiro ia
    1° acima da cruz — a 15 m, 26 cm, por cima da cabeça. Agora a mira do
    tiro é lida antes do recuo, que só move o próximo. Era também boa
    parte da M16 "difícil de usar";
  - a cabeça era a faixa de cima da cápsula (de 0,72 m para cima): pegava
    só a metade de cima da cabeça do humano e nada da do lobo, curvado,
    com a cabeça meio metro à frente, fora da cápsula. Agora a cabeça é a
    do modelo que se vê (`HeadHitbox`): uma esfera entre os ossos da
    cabeça, que segue a animação. Sem modelo (boneco do treino, servidor),
    a faixa continua.
  - A faca deixava a câmera dar o coice do último tiro; não dá mais.
- **M16A2 mais fácil** (arma padrão): dispersão 0,35° → 0,15° (por tiro
  0,22 → 0,09, teto 4 → 1,8, volta 9 → 12°/s depois de 0,05 s), andando
  ×2,2 → ×1,6, no zoom ×0,35 → ×0,2. Dano 30 → 27: ainda 4 no corpo e 2
  na cabeça; contra lobo, 10% a menos. O recuo ficou como estava.
- **Lobo na parede**: saltar dela olhando para ela e voltar fazia o lobo
  escorregar até o chão (a mesma parede não regrudava). Agora ele regruda
  quando desce até a altura de onde saltou — nunca acima, para a parede
  não virar elevador.
- **Rugido 20% mais rápido**: 2,0 s → 1,6 s (clipe a 2,875×).
- **Escudo do Ligeiro 140 → 200**, o mesmo do Bruto: o lobo começa com
  200 sem tatuagem, e o Ligeiro não perde nada.
- Testes: `TiroNaCabeca` (cintura e zoom, a 8 e 25 m, humano e lobo),
  `RegrudaNaParede` e 3 testes de unidade da cabeça.

### Partida autoritativa: o servidor move os corpos (13/11, primeira leva)
- Modo novo da partida online (`ALCATEIA_NETCODE=autoritativo` no servidor
  de partida; o padrão continua o de hoje). Decisão 0011.
- O servidor de partida carrega o mapa (só o que colide, num mundo de
  física por partida) e move o corpo de cada jogador pelos comandos que
  chegam, a 60 passos por segundo, com o mesmo código do jogo.
- O jogo manda o que apertou a cada passo (17 bytes, com os três anteriores
  de folga), continua movendo o corpo na hora e corrige quando o servidor
  discorda, sem o desenho pular. Os outros jogadores aparecem 100 ms no
  passado, interpolados entre dois estados do mundo.
- Medido: corpo do servidor exatamente onde o jogo previu (com atraso e
  pacote perdido); 16 jogadores custam 0,4 ms por passo, 64 custam 1,5 ms.
- Ainda do jogo nesta leva: vida, forma, arma na mão, renascimento, tiro e
  bots — a leva seguinte leva para o servidor.
- Testes: `PredicaoNoServidor`, `PassoDoServidor`, 11 testes de unidade
  do núcleo de rede e o ponta a ponta no modo autoritativo.

### Servidor: várias partidas ao mesmo tempo e em camadas (13/11)
- **Encerrar uma partida e não conseguir criar outra** (o Jhon: "não tinha
  servidor", só voltava reiniciando). O servidor de partida jogava uma
  partida por vez e só se liberava no fim pelo placar ou pelo tempo — com
  todo mundo fora, ficava preso até 45 s depois, e se o resultado não
  chegasse, para sempre. Agora:
  - **um servidor de partida joga várias partidas ao mesmo tempo** (4 por
    padrão, `ALCATEIA_MATCHES`), na mesma porta, cada uma com os seus
    jogadores — várias salas jogam juntas;
  - partida abandonada acaba em 20 s (era 45) e libera o lugar;
  - a batida diz quais partidas o servidor de partida tem abertas: a que
    some sem resultado libera o lugar sozinha, e a sala volta a esperar
    com o aviso "a partida foi encerrada sem resultado".
- **O servidor em camadas** (clean architecture, decisão 0010):
  `Alcateia.Dominio` (regras puras), `Alcateia.Aplicacao` (casos de uso
  e portas), `Alcateia.Infra` (PostgreSQL, senhas) e `Alcateia.Servidor`
  (HTTP e WebSocket). Mesmas rotas, mesmas mensagens.
- Testes novos do servidor: duas salas no mesmo servidor de partida (e a
  terceira esperando) e partida fechada sem resultado liberando o lugar.

### Servidor para milhares de jogadores e de partidas (13/11)
- **Até 1000 salas** ao mesmo tempo (era 500; decisão do Jhon).
- Saguão: cada conexão com a própria fila de envio (um jogador lento não
  segura os outros; quem não dá conta é desligado); a lista de salas vai
  junta duas vezes por segundo, para quem está olhando, com as 200 em que
  dá para entrar primeiro e o total.
- Frota: a partida é achada pela chave, sem varrer os servidores; o
  servidor de partida pega a primeira porta livre de uma faixa
  (`ALCATEIA_GAME_PORT=7777-7876`), e `docker-compose.frota.yml` sobe
  vários na mesma máquina (`--scale partida=N`).
- Banco: pool de 32 ligações (`ALCATEIA_DB_POOL`).
- **Teste de carga** (`tools/testar_carga.sh`, `servidor/Alcateia.Carga`):
  3000 jogadores, 1000 partidas ao mesmo tempo, 250 servidores de partida
  falsos — sem nenhum erro, p99 abaixo de 350 ms em 2 núcleos. Os números
  estão em `docs/SERVIDOR.md` ("Escala").

### Munição que acaba e zoom com mira circular (13/11)
- **A munição acaba.** Cada arma tem o pente e a reserva (M16/SG/AK 90,
  UMP 100, M3 32, M9 60); recarregar tira da reserva e, sem ela, não
  recarrega: o gatilho dá um clique seco (som novo, sintetizado) e o HUD
  mostra "SEM MUNIÇÃO". O HUD mostra a reserva no lugar do `∞`, vermelha
  quando zera. **Renascer enche tudo.** Passar por cima de uma arma igual
  à sua, caída, junta a munição dela (aviso "Munição: M16A2"); a arma que
  cai de quem morre leva o pente e a reserva que ele tinha.
- Os bots trocam para a pistola quando a principal seca e, sem bala em
  nenhuma, viram lobo.
- **Zoom: segurar o botão direito, de humano.** A câmera fecha (55° nos
  fuzis, 60° na UMP, 65° na M3 e na M9), a dispersão cai (sobra 35% nos
  fuzis, 50% na UMP, 60% na M9, 80% na M3) e o passo fica 30% mais lento.
  A cruz vira uma **mira circular** detalhada: anel, hastes com marcas e
  um círculo fino com o cone de verdade do próximo tiro. A arma fica onde
  está (não cresce com o zoom) e balança e coiceia menos; o mouse fica
  proporcionalmente mais fino. Recarregar tira do zoom. De lobo, o botão
  direito continua sendo o salto e o bote. (Mirar pela alça da arma foi
  tentado antes e ficou ruim.)
- Testes novos: `MunicaoFinita` e `ZoomNaMira` (com `--fotos=pasta`,
  fotografa cada arma sem e com zoom).

### Opções → Vídeo: suavização e qualidade (13/11)
- **Suavização (anti-aliasing)**: o jogo não tinha nenhuma — bordas
  serrilhadas em grade, palmeira e corrimão. Agora: Desligada, FXAA,
  MSAA 2×, **MSAA 4× (padrão)**, MSAA 4× + FXAA e TAA. Vale na janela
  inteira, do menu à partida, na hora.
- **Qualidade**: Baixa, Média, **Alta (padrão)** e Ultra. Mexe no tamanho
  e no filtro das sombras e nos efeitos de tela: oclusão nos cantos
  (SSAO) e brilho das luzes da Média para cima, luz que rebate nas
  paredes (SSIL) da Alta para cima, reflexo no chão e nas superfícies
  lisas (SSR) só na Ultra. Os efeitos só ligam onde o mapa os tem.
- No menu e no Esc da partida. Teste novo: `Suavizacao`.

### Luz dos mapas no Forward+ (13/11)
- **Vila, Hangar e Polar estavam estourados** (o Jhon: tudo branco,
  difícil de ver os outros jogadores). A luz tinha sido acertada no
  renderizador Compatibility, o único que fotografava aqui; no Forward+
  do Mac o céu da foto (HDRI) a 1,7–1,8 também iluminava a cena inteira
  e apagava as sombras. Agora: céu a 1 (0,8 na Polar), ambiente 0,35 com
  metade vindo do céu, exposição 0,85 (0,8 na Polar) e sol 1,4 (1,1 na
  Polar). As sombras voltam, o céu fica azul, e os corpos se destacam da
  parede e do chão. O Metrô (fechado, sem céu) ficou como estava.
- As fotos de conferência agora saem no Forward+ (Vulkan por software).

### Dificuldade dos bots na sala online (13/11)
- O dono escolhe a dificuldade dos bots ao criar a sala online (Fáceis,
  Médios, Difíceis) e pode trocar na própria sala, ao lado das regras;
  os outros veem "bots: médios". Médios se ninguém escolheu. A sala de
  treino offline já tinha.
- Vai nas regras da sala (`difficulty`, 0 a 2, o servidor corrige fora
  da faixa) e vale para os bots na partida. O teste de ponta a ponta
  joga com bots difíceis.

### Mira do jogador (13/11)
- **Opções → Mira**, no menu e no Esc da partida: estilo (cruz com
  ponto, cruz, ou só o ponto — este de um tamanho só, 4 px), cor (seis
  prontas e uma livre), opacidade, tamanho, espessura e espaço das
  hastes, contorno escuro, e **abrir ao atirar e ao correr** (desligado,
  a mira fica parada; a dispersão do tiro continua valendo). Prévia ao
  lado, abrindo como num tiro seguido. "Mira padrão" volta à de sempre.
- Salvo em `user://opcoes.json` com as outras opções; vale no quadro
  seguinte, no meio da partida. Teste novo: `MiraDoJogador`.

### Fase 1 concluída e ajustes do Jhon (13/11)
- **Fase 1 concluída**: o Jhon jogou os quatro testes de validação do
  arsenal e respondeu a pergunta que decide o projeto — sim, é gostoso.
- **Agarrar a parede com um pulinho**: colado na parede, o lobo gruda a
  partir de 0,45 m do chão, ainda subindo (`WallGrabHeight`). Antes
  esperava o alto do pulo. Teste novo: `AgarraBaixo`.
- **Velocidade da animação do lobo**: `SprintAnimSpeed`,
  `WalkAnimSpeed` e `WallAnimSpeed` viraram `SprintAnimScale`,
  `WalkAnimScale` e `WallAnimScale`: uma escala sobre o ritmo que
  acompanha o corpo (1 = igual, 0,7 = 30% mais lenta), só na animação.
  O ajuste antigo era velocidade fixa do clipe, e como a corrida de
  quatro toca o clipe a ~1,7× o normal, qualquer valor ficava lento.
- **Arma principal sem animação de pegar**: sobe de baixo, como a
  pistola. Só a faca tem saque animado.
- **Faca sincronizada com o golpe**: o dano sai no clique, mas a lâmina
  só chegava na tela 0,2 s depois, e a volta era cortada para caber no
  intervalo. Agora a armada corre em 0,07 s (`RigSwingLead`) e a volta
  toca inteira.

### Bots no multiplayer (13/11)
- **A sala online aceita bots.** O dono põe ("+ Bot" em cada time) e
  tira; quem entra na sala toma o lugar de um bot se o time estiver
  cheio; a lista de salas mostra "+N bots".
- **Na partida**, os bots são movidos pela máquina do dono da sala, que
  fala por eles com o servidor; para os outros jogadores são bonecos,
  como qualquer jogador. Atiram, levam dano, morrem, renascem e pontuam
  para o time. Se o dono cai, os bots saem com ele (pendência).
- **Bot não tem patente**, em lugar nenhum (offline também): sem
  insígnia no placar e na sala, "Bot" no cartão de duelo. Não ganha XP
  nem entra na partida gravada no servidor.
- Servidor: `room.bot.add` / `room.bot.remove`, bots na escalação com o
  dono; servidor de partida: pacotes `BotState`, `BotAttack`, `BotHit`,
  `BotDied`, `HitBot` e `Touch`. **Quem roda o servidor no Docker
  precisa reconstruir as duas imagens** (`docker compose up -d --build`).
- Teste de ponta a ponta (`tools/testar_online.sh`) agora joga com um
  bot em cada time.

### Correções do que o Jhon viu jogando (13/11)
- **Salas**: saem as salas de mentira. A aba Salas é a lista online; o
  treino com bots (offline) ficou num botão, "Treino com bots".
- **Trocar o mapa da sala**: o botão dizia "o Hangar é o único mapa";
  agora abre a escolha entre os quatro.
- **Faca e granada** com os modelos do Sketchfab (faca Gerber, granada
  M84), na mão do braço animado da pistola (sem o braço esquerdo), com
  o golpe e o arremesso.
- **Braços em primeira pessoa**:
  - estavam desenhados com a abertura de câmera do jogo (75°), não com a
    do animador (50,5°): ficavam pequenos e no canto. O conjunto agora é
    esticado na razão entre as duas;
  - o cano ficava a um metro do olho e atravessava a parede: o conjunto
    é trazido para perto do olho (45%), com a mesma aparência na tela;
  - a primeira troca para cada arma travava lendo o arquivo: os conjuntos
    são carregados ao entrar na partida;
  - parado e andar repetem sem tranco; quem não tem animação de sacar
    sobe de baixo; pistola, faca e granada um pouco mais altas.
- **Faca com animação própria**: o conjunto "knife animated" (DJMaesen,
  CC BY) que o Jhon trouxe — braços, faca e animações. Atacar alterna a
  estocada e o corte de lado, no tempo entre dois golpes; sacar e parado
  também são do conjunto. Sai a faca Gerber presa no braço da pistola e
  o golpe feito por código.
- **Varredura dos quatro mapas** (`tools/mapas/conferir.py`, novo): lê a
  geometria de cada mapa atrás do que vira defeito na tela. Achou e
  corrigiu:
  - **170 faces brigando** (duas peças de materiais diferentes com uma
    face no mesmo plano — o pisca-pisca nas portas das oficinas e dos
    alojamentos, onde o marco de aço empatava com a parede; tetos e
    peitoris do Metrô, da Vila e da Polar). O kit agora desempata
    sozinho ao gravar: a peça de enfeite cresce 3 mm.
  - **31 letreiros mal assentados**: placas da Polar cortadas pelo friso
    de aço, o número do portão do Hangar atravessado por uma coluna,
    letras das bases do Metrô e da Vila soltas a um palmo da parede. O
    kit assenta cada letreiro 2 cm à frente da superfície que tem atrás.
  - **Polar**: o carrinho e os galões dos alojamentos flutuavam 24 cm
    (ficaram na altura do piso antigo).
  - Os testes de mapa agora conferem, com a física do jogo, que nenhum
    ponto de nascimento fica dentro de parede e que não há buraco no
    chão; e dão até 100 s para o primeiro tiro no mapa grande.
- **Braços reiniciando**: no M16 (a arma com que se nasce) o trecho de
  "parado" pegava o fim do saque, e a cada 0,9 s os braços pulavam 10 cm.
  Trecho corrigido; nos conjuntos de linha do tempo única o parado vai e
  volta em vez de emendar; e a troca entre parado e andar espera 0,18 s
  (perder o chão por um quadro recomeçava a animação). O teste agora
  mede, arma por arma, que parado não há recomeço nem tranco.
- **Personagens tremendo e piscando**: a física anda 60 passos por
  segundo e a tela do Mac no ritmo dela (120 Hz, ou 60 Hz com quadros
  desencontrados). O corpo era desenhado onde o último passo o deixou: a
  120 quadros, o bot só andava em metade deles. Novo `VisualSmoothing`
  desenha corpo, câmera, arma e lobo entre um passo e o outro (jogador,
  bots e bonecos da rede); medido a 120 quadros, a variação da velocidade
  na tela caiu de 106% para 38% (o resto é o bot mudando de rumo). A
  jogabilidade continua lendo a posição da física, e o giro do mouse
  continua na hora.
- **Borda vermelha dos inimigos** numa casca 4 mm para fora da pele: na
  mesma superfície ela brigava com o boneco pelo pixel e podia piscar.
- **Mapas**: os letreiros ficavam a 1 cm da superfície e piscavam de
  longe; agora a 2 cm a mais. Na Polar, o piso dos cômodos novos ficava
  24 cm acima do chão (os pés afundavam). O piso do Hangar estava claro
  demais ao sol.

### Braços animados em primeira pessoa (Sketchfab, CC BY)
- As seis armas de fogo passam a usar os conjuntos animados baixados do
  Sketchfab: braços, arma e animações de animador (parado, sacar, tiro,
  recarga, recarga com pente vazio, andar). M16A2, AK, SG552 (fuzil
  compacto do pacote de fuzis), UMP45 (submetralhadora do pacote), M9
  (pistola do pacote) e M3 (escopeta do pacote Benelli).
- `tools/armas/blender/bracos.py` converte cada .fbx no Blender: olho do
  jogador na origem, escala em metros, materiais com as texturas,
  animações com o nome que o jogo usa. Saída em `assets/armas/fp/` e a
  ficha `bracos.json`.
- `ViewModel.Rig.cs` toca a animação certa: o tiro a cada disparo, a
  recarga na velocidade que termina junto com a recarga de verdade (a
  de pente vazio quando esvaziou; na escopeta, um ciclo por cartucho).
- Faca e granada continuam com as mãos feitas por código.
- Teste `BracosNaMao` reescrito para os braços animados.
- **Não conferido na tela**: o teste passa, mas as fotos não chegaram a
  ser tiradas. Se algum conjunto aparecer torto, apague a entrada dele
  em `assets/armas/fp/bracos.json` e a arma volta às mãos de código.
- Créditos: os autores dos pacotes do Sketchfab.

### Hangar: de caixa cinza a base aérea
- O primeiro mapa passou para o mesmo kit dos outros
  (`tools/mapas/hangar.py`; sai `tools/gerar_hangar.py` e a textura de
  grade). **A colisão é a mesma, peça por peça** — conferida
  automaticamente contra o gerador antigo —, com uma diferença de
  propósito: as quatro oficinas, que eram bloco maciço, agora têm dois
  cômodos, três portas e duas janelas cada. A parede de trás, a do beco
  do lobo, ficou inteira.
- Tijolo de fábrica, piso de concreto, chapa trapezoidal nos galpões em
  volta com fita de janelas, portões de enrolar numerados, contêineres
  com cantoneira, anel pintado e faixas de perigo na plataforma do
  centro, farol com lanterna, céu fotografado.
- Letreiros (HANGAR 7, OFICINA, ALMOXARIFADO, FERRAMENTARIA, PINTURA,
  avisos) e objetos do Poly Haven nas oficinas.

### Mapas: material de verdade, lugares para entrar, trem e letreiros
- **Texturas fotográficas** (Poly Haven, CC0) no lugar das desenhadas por
  código: 44 materiais com cor, relevo e oclusão/aspereza. Tijolo,
  reboco gasto, telha de barro, calçamento, contêiner, neve, brita.
- **Céu fotografado** (HDRI) iluminando a Vila e a Estação Polar.
- **Lugares para entrar**, todos com três entradas ou mais:
  - **Metrô**: uma galeria de lojas atrás de cada plataforma (44 × 8 m),
    com café, banca, sala de controle com janela de tiro e corredor
    atrás dos balcões. O mapa passou de 48 × 30 para 48 × 50 m.
  - **Vila**: as quatro casas grandes agora têm dois cômodos no térreo
    (padaria, mercado, café, barbearia), com porta para a praça, para o
    beco e para a viela, e janelas por onde se atira.
  - **Estação Polar**: a fileira da frente de cada alojamento virou
    laboratório e refeitório, atravessáveis por dentro.
- **Trem de verdade** no Metrô, modelado no Blender: carroceria de inox,
  janelas com borracha, portas de correr abertas, cabine com para-brisa
  e faróis, truques com rodas no trilho, e o salão por dentro (bancos,
  balaústres, corrimão com alças, luminárias).
- **Letreiros com letra de verdade**: placa de estação, saída,
  plataforma, mapa da linha, destino do trem, nome de loja pintado,
  estêncil de galpão, cartazes, pichação, marcação de piso e a letra da
  base — 66 imagens desenhadas por `tools/mapas/letreiros.py` com
  Overpass, Saira Stencil, Alfa Slab, Bebas Neue e outras de uso livre.
- **Objetos de cenário** do Poly Haven (barris, caixotes, estantes,
  carrinho, gerador, sacos de cimento…), convertidos no Blender.
- Kit: `m.parede` (parede com porta e janela), `m.letreiro`, `m.modelo`,
  `P.objeto`. Sai o `Label3D` e sai `tools/gerar_texturas.py`.

### Jogo online
- **Servidor** em .NET 8 + PostgreSQL, no Docker (`docker compose up`):
  contas com senha, sessões, perfil, ranking, salas e chat ao vivo
  (WebSocket), e a frota de servidores de partida. Sem pacote de fora —
  o cliente do PostgreSQL é do projeto (`docs/decisoes/0009`).
- **Tela Online** no menu: criar conta ou entrar, lista de salas, criar
  sala (mapa, times, pontos, tempo, regras, senha), a sala com os dois
  times, pronto/iniciar, chat e ranking.
- **Servidor de partida**: o jogo sem tela (`-- --servidor`) recebe a
  partida do servidor, confere o ingresso de cada jogador, repassa o
  que cada um faz, arbitra os abates (`Referee`) e entrega o resultado,
  que vira XP e ranking na conta.
- **Partida em rede** entre jogadores: os outros aparecem como bonecos
  com o mesmo corpo, arma e animação; tiro, faca, golpe de lobo e
  granada são encenados; dano, morte, renascimento, placar e relógio
  passam pelo servidor. Quem decide o quê: `docs/decisoes/0008`.
- Testes: 22 do servidor contra PostgreSQL de verdade
  (`tools/testar_servidor.sh`), 8 de unidade do juiz, e um de ponta a
  ponta com dois jogos sem tela (`tools/testar_online.sh`).
- Limites desta versão: sem bots online, não é à prova de trapaça, o
  perfil da loja continua local. Tudo em `docs/SERVIDOR.md`.

### Créditos
- **Tela de créditos** (Opções → Créditos) com a origem e a licença de
  cada material de terceiros — o que a CC BY do Oculus Audio Pack
  exigia antes de publicar. O conteúdo fica em `scripts/meta/Credits.cs`.

### Braços e armas em primeira pessoa
- **Duas mãos de luva na arma**, em vez de arma solta: a direita no
  punho, a esquerda no guarda-mão (ou apoiando a pistola, ou em guarda
  com a faca).
- **Recarga animada**: a mão esquerda tira o carregador, some com ele,
  volta com outro, encaixa e — se o pente esvaziou — puxa o ferrolho.
  Na escopeta, um cartucho por vez pela janela de carga; depois de cada
  tiro, a bombeada. A granada tem o pino puxado; a faca corta
  atravessando a tela.
- **Armas redesenhadas** (M16A2, SG552, AK-47, UMP45, M3, M9, faca,
  granada de choque): silhueta de verdade em vez de caixas — alça de
  transporte, carregador curvo, coronha vazada, guarda-mão estriado. O
  mesmo modelo vale na mão dos outros e no chão.
- Tiro: coice com o cano subindo, clarão em estrela com luz, ferrolho
  recuando, cápsula saltando. A arma atrasa ao olhar, afunda no pouso,
  e o balanço do passo cai junto com o som do pé.
- `tools/gerar_armas.py` desenha tudo; teste novo `BracosNaMao`.

### Mapas remodelados
- Metrô, Vila e Estação Polar ganharam uma camada de **acabamento** por
  cima do desenho de jogo (que não mudou — os testes de mapa passam
  iguais): o que era caixa lisa agora é lugar.
  - **Metrô**: abóbada de azulejo com nervuras, colunas redondas com
    capitel, trem de teto curvo com janelas, faróis e balaústres,
    escadas de degrau com corrimão, duto de ventilação, canos e quadros
    no corredor de serviço, anúncios acesos, bilheteria com guichê,
    portões de enrolar.
  - **Vila**: os muros viraram frentes de casa (reboco de várias cores,
    janelas com veneziana e grade, portas em arco, vigas e beiral),
    chafariz torneado, bancas com lona e fruta, varal de luzes, torres
    com sineira e cúpula, carroça, pérgola, palmeiras e minaretes para
    além do muro.
  - **Estação Polar**: paredões de gelo irregulares e serra nevada em
    volta, neve caindo, neve nos telhados e sincelos, janelas acesas,
    tratores de esteira, tanques com cúpula e escada, torre de rádio
    treliçada com estais, canos com flange, refletores.
- `tools/mapas/formas.py` (formas que não são caixa, gravadas como
  .obj) e `tools/mapas/pecas.py` (peças prontas). Rampas podem ser
  desenhadas como escada (`degraus=`); peças podem ser só colisão
  (`oculta=True`).

### Som
- **Passo na passada.** O ruído do passo sai a cada passada do clipe
  (1,19 m o humano, 1,05 m o lobo, 0,97 m de quatro) em vez de a cada
  2,2 m: o som cai com o pé e acelera com o corpo. Os bots ouvem cada
  pisada. Teste novo `PassoNoRitmo` amarra os números aos clipes.
- **Passo conforme o chão**: metal, neve, madeira e terra, para humano e
  lobo. O gerador de mapas marca cada peça pelo material.
- **Tiros com variação**: de duas a quatro gravações por arma em vez de
  uma, e o seu tiro é outra gravação (estéreo, mais grave).
- Bala estalando na parede (concreto e metal) e a cápsula do seu tiro no
  chão.
- **O som do lugar**: cada mapa tem reverberação e fundo próprios — a
  estação ecoa e tem trem passando no túnel ao lado, a neve abafa e
  venta. Limitador na saída: tiroteio cheio não estoura.
- Teste novo `SomDoMapa`.

### Arte de verdade a caminho
- `tools/baixar_pacotes.py`: um comando baixa do Poly Haven (CC0) as
  texturas fotografadas, os objetos de cenário e os céus dos mapas para
  `assets/pacotes/` (fora do git e do Godot).
- `docs/PACOTES.md`: a lista do que baixar, de onde, com que licença —
  incluindo os braços em primeira pessoa e as armas do Sketchfab
  (CC BY, baixados à mão).

## v0.28.0 — 2026-11-13 (o jogo profissional, parte 1: mapas)

### Adicionado
- **Três mapas novos**, de temas e tamanhos diferentes:
  - **Metrô** (pequeno, 48 × 30 m): estação fechada com um trem parado
    no meio, galerias a 5 m e corredores de serviço;
  - **Vila** (médio, 72 × 52 m): vila de deserto no fim da tarde, com
    praça, chafariz, feira, terraços e vielas;
  - **Estação Polar** (grande, 110 × 70 m): base de pesquisa na neve,
    com garagem atravessável, alojamentos, passarelas e tanques.
- **16 texturas com relevo e aspereza**, desenhadas por código
  (`tools/gerar_texturas.py`), sem arquivo de terceiros.
- **Céu, neblina, sol e reflexos por mapa.**
- **A partida carrega o mapa da sala** (`MapSlot`); antes o Hangar
  estava preso na cena.
- **Gerador de mapas** (`tools/gerar_mapas.py` e o kit em
  `tools/mapas/`) e o guia `docs/MAPAS.md`.
- Testes `MapaHangar`, `MapaMetro`, `MapaVila`, `MapaPolar`.

### Mudado
- **Fase 1 fechada** (decisão do Jhon). A "Regra zero" do `CLAUDE.md`
  agora é: rede fora das regras de jogo.

## v0.27.1 — 2026-11-13

### Mudado
- **Salto carregado 30% mais curto no fim da barra**: cheio, voa 8,4 m
  (era 12). Sem carga continua 1,5 m. O bot só tenta o salto até 7,4 m
  do alvo (era 11), para não cair longe dele.
- **Lobo 10% mais rápido na parede**: Bruto 4,45 m/s (era 4,05),
  Ligeiro 4,82 (era 4,38). O salto da parede leva 80% disso, então
  sobe junto.
- **Humano 8% mais rápido**: 3,24 m/s com o fuzil (era 3,0), agachado
  1,46, com Shift 1,36. O lobo não muda (`HumanSpeedBonus`, só no
  humano). Os bots humanos seguem a velocidade da dificuldade.
- **Com a faca, 12% mais rápido**: 3,36 m/s (era 3,0).

### Adicionado
- **Corrida de quatro do lobo**: W duas vezes (segurando no segundo) e
  o lobo cai nas quatro patas, 30% mais rápido (Bruto 5,06 m/s, Ligeiro
  5,48), tocando o "Running Crawl". Soltar o W ou agachar volta a
  correr em pé. Só o jogador; os bots ainda não usam.
- **C também agacha.**
- **Velocidade fixa da animação do lobo**, se quiser: `WalkAnimSpeed`,
  `SprintAnimSpeed` e `WallAnimSpeed` no nó `WolfVisual` (1 = normal).
  Em 0 (padrão) a animação acompanha o corpo, como antes.

### Corrigido
- **Mac: agachar no Ctrl e clicar não atirava.** O macOS (pelo Godot)
  entrega Ctrl + clique esquerdo como clique direito; o humano não
  atirava e o lobo carregava o salto em vez de arranhar. Agora o
  clique direito com Ctrl vira ataque no Mac; o especial agachado sai
  agachando no C.
- **O andar de quatro (engatinhar) patina menos**: usava a passada da
  parede (2,3 m/s); a medida no chão é 2,9 (`CrawlClipSpeed`).

## v0.27.0 — 2026-11-12 (organização da casa)

Série de commits que reorganiza o código sem mudar o jogo. Os testes
desta versão provam que o comportamento continua o aprovado; as poucas
diferenças de comportamento estão em "Mudado", com o porquê.

### Adicionado
- **Testes automatizados** (`tools/testar.sh`, ~1,5 min):
  - 33 de unidade (`tests/unit`): C# puro, sem Godot e sem pacote —
    contas do combate, tempo dos golpes, sorteio com semente;
  - 21 de regressão (`tests/regression`): o jogo de verdade, sem tela —
    todas as telas do lobby, salto carregado em 8 situações, velocidades no chão, altura do
    pulo, corrida na parede, salto da parede, agarrar na parede,
    passada do lobo, granada, dano da garra, ruído, níveis dos bots,
    clipes de animação, HUD, renascer humano, bots atirando, lobos
    usando especiais, cenas abrindo sem erro.
- **Testes no GitHub a cada push** (`.github/workflows/testes.yml`).
- **Detector de código repetido** (`tools/duplicados.py`), no
  `testar.sh`: seis linhas iguais em dois lugares reprovam.
- **Semente da partida**: a partida diz a semente no Output, e
  `-- --seed=N` repete a sorte (mira dos bots, dispersão, rotas).
- **Documentação**: `CONTRIBUTING.md` (o passo a passo),
  `docs/DESIGN.md` (as decisões de jogo, que saíram do `CLAUDE.md`) e
  `docs/decisoes/` (os registros de arquitetura, ADR 0001 a 0007).

### Mudado
- **Código dividido por assunto** (classes parciais): `Player`, `Bot`,
  `Weapon`, `CharacterAnimator`, `WolfVisual` e `Hud`, cada um numa
  pasta com um arquivo por responsabilidade. No Inspector os campos
  aparecem em grupos. Nenhuma linha de código mudou nessas divisões.
- **Jogador e bot dividem o corpo** (`scripts/actors/body`): o bote, o
  voo do salto, o lobo preso ao chão, o ruído e o cadáver eram cópias.
  Onde as cópias tinham divergido, valeu a regra do jogador:
  - o bote do bot só bate em parede quando vai de encontro a ela;
  - o pouso do salto do bot faz o baque (os outros ouvem);
  - o rugido faz ruído já no começo, sempre.
- **Ruído do corpo num arquivo só** (`resources/body/ruido.tres`), no
  lugar dos cinco campos repetidos no jogador e no bot.
- **Níveis dos bots viram dado** (`resources/bots/*.tres`), com o nome
  e a linha do menu junto.
- **Contas do combate em C# puro** (`scripts/domain/CombatRules.cs`):
  dano, queda por distância, dispersão, recuo, garra, salto, escudo.
- **O jogador lê o teclado uma vez por quadro** (`CharacterIntent`).
- **Sorteio de regra com semente** (`GameRandom`), um fluxo por bot e
  por arma, no lugar do sorteio global do Godot.
- **Ferramentas de animação num lugar só** (`AnimationKit`), que o
  humano, o lobo e o lobby usavam em cópias.
- **Corrida na parede em passos com nome** (o método tinha 286 linhas).
- **O resto do código repetido juntado**: o id do que está na mão
  (`Weapon.HeldId`, que a arma na tela, a do corpo e o HUD calculavam
  cada um), os ids da faca e da granada (`Arsenal.KnifeId`, `GrenadeId`),
  a pintura do time (`MaterialTint`), os nomes de grupo (`Groups`), a
  busca na árvore (`Nodes`), e no lobby o cartão, a grade com rolagem,
  a linha rótulo/valor, a miniatura do mapa, a página e o botão do
  header (`UiKit`, `LobbyScreen`).
- `CLAUDE.md` com 390 linhas (era 1850): as regras ficam, as decisões
  de cada sistema foram para o `docs/DESIGN.md`.
- `WolfVisual` saiu de `scripts/ui` para `scripts/visual/wolf`.
- `Player.WallDebug` vem desligado.
- **Indentação com tab em todo o C#**, fixada no `.editorconfig`.
- **Bruto: dano 75, alcance 5, sem espera entre garradas** (os valores
  com que o Jhon joga).

### Removido
- Cópias velhas de cenas e do Bruto na raiz do projeto.
- `WolfBreed.Hp` e `WolfBreed.AttackCooldown`, que nada usava.

## v0.26.12 — 2026-11-12

### Adicionado
- **Guia de ajustes** (`docs/AJUSTES.md`): todo número regulável do
  jogo — movimento, parede, classes de lobo, golpes, armas, granada,
  vida, WP, bots, partida, som e animação —, com o valor atual, onde
  mudar e o que acontece.

## v0.26.11 — 2026-11-12

### Mudado
- **Lobo 3% mais rápido no chão** (Bruto 3,89 m/s, Ligeiro 4,22) e
  **7% mais rápido na parede** (Bruto 4,05, Ligeiro 4,38). O salto da
  parede leva 80% da velocidade dela e sobe junto.

## v0.26.10 — 2026-11-12

### Adicionado
- **Lobo agachado de verdade:** parado, encolhe (0,98 m, a altura da
  cápsula agachada); andando, engatinha. Antes ele continuava em pé
  com a cabeça fora da própria cápsula.
- **Humano agachado mira com o fuzil no ombro**, como de pé (antes o
  tiro saía com a arma baixa, na cintura).
- **Granada armada:** segurando o botão, o braço vai para trás (na mão
  da tela e no corpo que os outros veem); soltou, arremessa.

### Mudado
- **Salto carregado com distância fixa pela barra** (1,5 a 12 m), com
  ou sem inimigo na frente, olhando para onde for: sai na direção da
  mira e não persegue ninguém no voo. Só parede e bloco encurtam.
- **Lobo 5% mais rápido** no chão e na parede (Bruto 3,78 m/s, Ligeiro
  4,1).
- **Granada mais forte:** olhando reto cai a ~16 m (era 11).
- **Lobo vira para onde anda:** de lado ele vira e anda de frente; para
  trás fica de frente e o passo toca ao contrário. A patinação dos pés
  caiu de 5,4 para 0,85 m/s de lado e de 7,3 para 0,72 para trás.
- **Lobo parando:** o passo congela onde parou e as pernas se juntam em
  0,2 s; antes ele corria no lugar mais um quarto de segundo.

### Corrigido
- **Renascer sempre como humano**, mesmo morrendo de lobo.
- **Salto que mudava sozinho com inimigo na frente**: ia até o inimigo
  e passava do alcance da barra.

## v0.26.9 — 2026-11-12

### Adicionado
- **Salto do lobo carregado:** segurar o botão direito no chão carrega
  em 1 s; mais carga, mais longe (1,5 a 12 m, até o ponto mirado) e
  mais dano (40% a 100%). Barra de força na tela.
- **Granada: segurar prepara, soltar arremessa**, na mira de quando
  solta.
- **Cartão do duelo** ao matar ou morrer: patentes, nomes e quantas
  vezes cada um matou o outro.
- **Inimigos com a borda levemente vermelha** (só onde estão à vista).
- **Cartão sobre o matador** na câmera de morte, com patente e nome.
- **Lobos dos bots usam o salto carregado e o pulo com bote**, mais ou
  menos conforme a dificuldade.

### Mudado
- **Humano a 3,0 m/s, na passada da animação** (era 4,2): correndo, o
  pé não patina mais, e a animação não acelera. Os lobos continuam a
  3,6 e 3,9; os bots seguem a mesma conta (2,5 / 3,0 / 3,5).

### Corrigido
- **O strafe do humano tocava ao contrário**: o pé andava junto com o
  corpo.

## v0.26.8 — 2026-11-12

### Adicionado
- **Quem te matou fica com a silhueta vermelha** (só para você, através
  das paredes) **e a câmera vira para ele**, aproximando se estiver
  longe. A escolha de conjunto abre 1,8 s depois.

### Mudado
- **Lobo mais lento a pé, com a corrida fluida:** Bruto a 3,6 m/s (era
  5,7) e Ligeiro a 3,9 (era 6,1), também na parede. A corrida toca a
  1,5× em vez de 2,4×.
- **Pulo do lobo 5% mais alto** (Bruto 1,62 m); **salto da parede 40%
  mais baixo**.
- **Especial no ar avança 20% menos**, e metade depois de um salto da
  parede.
- **No ar o lobo cai, não anda:** pose de queda ao soltar ou saltar da
  parede e ao cair de beirada.
- **Morrer custa 100 WP.**
- **O tiro do inimigo se ouve de longe**, vindo de onde ele está.
- **Soltar a arma agora é G** (era B).

### Corrigido
- **Bots paravam de atirar:** indo para a parede ou procurando, eles
  viam o alvo e não atiravam — dois bots ficavam meio minuto se
  encarando. Agora atiram em qualquer ação que não seja recarregar ou
  recuar.
- **O lobo morto ficava meio segundo de pé** antes de cair.

## v0.26.7 — 2026-11-12

### Mudado
- **Na quina, o lobo contorna e continua pela parede** em vez de cair:
  bloco, contêiner, pilar e coluna fina, na mesma altura e no mesmo
  passo.
- **O salto (especial no chão) cai no ponto do clique:** segurar W não
  aumenta mais a distância, e virar a câmera no agachar não muda nada.
- **O bote cai mais devagar:** 13 m/s (eram 17), com o mesmo alcance.

### Corrigido
- **O lobo andava mais rápido que os passos:** o clipe de correr tocava
  a 1× e o pé patinava. Agora toca no ritmo do corpo (2,36× no Bruto
  correndo) e o pé apoiado fica parado no chão, correndo e agachado.

## v0.26.6 — 2026-11-12

### Adicionado
- **Animação de pulo do lobo** ("mutant jumping"): o voo acompanha o
  pulo de verdade — saindo do chão, no topo, caindo —, e chegando
  devagar o lobo aterrissa agachando; chegando correndo, já sai
  correndo. Vale para o pulo do chão e o da parede.

## v0.26.5 — 2026-11-12

### Mudado
- **Na quina, o lobo segue reto.** A corrida na parede acaba na ponta e
  o lobo continua no ar, no mesmo passo. Antes ele contornava e, numa
  parede fina, voltava pelo outro lado.
- **No canto, vira e continua** pela parede da frente, no mesmo passo.
- **Pulo do lobo mais curto e um pouco mais alto:** o pulo do chão leva
  55% do passo (o Bruto avança 3,7 m; eram uns 6) e sobe 1,54 m no
  Bruto (eram 1,31) e 2,38 m no Ligeiro (eram 2,22). O salto da parede
  não muda.
- **Bote em todo pulo:** sem a recarga de 2 s. Acabou a aterrissagem, o
  próximo pulo já tem outro, e pular só depois que ela acaba.
- **Pulo e especial juntos viram bote**, não o salto do chão. O bote sai
  em qualquer ponto do pulo, até na subida.
- **O lobo ruge em movimento:** anda, pula e cai rugindo, sem frear. O
  rugido toca da cintura para cima.
- **Virar lobo não tem mais animação.**

### Corrigido
- **O lobo voltava pela parede ao chegar num canto:** soltava e
  regrudava na parede de trás, no sentido contrário.

## v0.26.4 — 2026-11-12

### Mudado
- **Salto do lobo em arco, passando por cima de obstáculos:** o salto
  mede o chão no caminho e sobe o bastante para passar por cima do que
  tiver até 2,6 m (caixote, contêiner). Mirando além de um contêiner
  perto, cai em cima dele; diante de algo mais alto (bloco, muro), cai
  antes, nunca dentro.
- **W + botão direito avança:** segurando W, o salto anda pelo menos
  6 m, mesmo mirando o chão perto.
- **O salto acompanha o alvo:** o ponto de queda segue quem você mirou
  a 3 m/s durante o voo. Quem só caminha não escapa mais, e quem corre
  de lado desde o começo ainda escapa.
- **Rugido segurando R, em 2,0 s** (eram 2,88): soltar antes cancela
  sem devolver o escudo.

### Corrigido
- **O lobo subia depois de grudar na parede.** Agora ele só gruda no
  alto do pulo e fica na altura em que grudou.
- **Trancos ao contornar quina e coluna fina.** A parede agora é o
  ponto mais próximo dela, não o que um leque de raios acerta: a
  normal gira contínua em volta da quina, e o lobo dá a volta inteira
  numa coluna de 30 cm sem soltar, na mesma altura.

## v0.26.3 — 2026-11-12

### Mudado
- **Salto do lobo de longe:** até 12 m (eram 5), para apertar de longe
  e cair em cima do humano. Um inimigo a até 10° do centro da tela vira
  o alvo — o lobo não tem mira, e acertar um corpo a 12 m só com o
  centro da câmera seria loteria.
- **Garradas respondem mais rápido:** a garra chega 0,14 s depois do
  clique (eram 0,23 e 0,16) e cada golpe dura 0,39 s (eram 0,52). A
  garrada começa com a mão já recolhida.
- **Lobo mais lento:** Bruto a 5,7 m/s (1,35×) e Ligeiro a 6,1 m/s
  (1,45×). O pulo fica mais curto pela velocidade; a altura não muda,
  porque está amarrada ao mapa.
- **Corrida na parede mais lenta e controlada:** sem bônus de
  velocidade (5,7 m/s no Bruto; eram 8,1), acelerando ao grudar e
  freando em meio metro ao agarrar.

### Corrigido
- **Humanos flutuavam 6 a 8 cm acima do chão.** Os clipes são do X Bot,
  que tem perna mais longa que o Exo Gray e a Ely; a trilha do quadril
  agora é ajustada à altura de cada modelo.

## v0.26.2 — 2026-11-12

### Mudado
- **O salto no chão avança até a mira:** do impulso até os pés tocarem
  o chão, o lobo anda até as garras caírem no ponto para onde a câmera
  aponta — em cima de quem você mirou, até 5 m. Antes ele saltava no
  lugar.
- **Garradas mais rápidas e respondendo ao clique:** 0,52 s por golpe
  (eram 0,69 e 0,71), garra chegando em 0,23 s e 0,16 s. O clique dado
  no fim de um golpe fica guardado e o próximo sai no quadro em que ele
  acaba — antes ele se perdia.
- **O mestre da sala não marca pronto.** Ele inicia com todos prontos,
  ou com os prontos já em times equilibrados (um lado pode ter um a
  menos). Quem não estava pronto fica na sala.
- **Sem mira na forma lobo.** Em terceira pessoa o próprio lobo no meio
  da tela já diz para onde ele vai.

## v0.26.1 — 2026-11-12

### Mudado
- **Rugido em 2,9 s** (era 3,6): corta o começo e o fim parados do
  clipe e toca um pouco mais rápido. O escudo continua voltando só no
  fim.
- **Bote (direito no ar) refeito:** mergulho em linha reta na direção
  da mira, a 17 m/s, sem gravidade nem freio no meio — o ponto de
  impacto é escolhido no clique. O golpe sai no instante em que o lobo
  encosta (chão, parede ou alguém), não num tempo marcado: um golpe só,
  3×, num volume um pouco maior que o do salto. Aterrissa preso por
  0,78 s; recarga de 2 s.
- **Salto (direito no chão):** toca o "jump attack" inteiro com o lobo
  preso no lugar, e o dano sai só quando ele cai, 1,14 s depois do
  clique. Substitui a patada pesada.
- **Golpes fluidos**, medidos quadro a quadro na velocidade dos ossos:
  - a garrada entra em 0,15 s, e os braços não voam mais até a pose
    inicial;
  - parado, a garrada dá um passo em sino (0,91 m, como o do clipe; o
    punch, 0,35 m) e as pernas andam junto — antes o empurrão
    instantâneo jogava os pés na pose de correr de uma vez;
  - a queda do bote acelera para o impacto da animação coincidir com o
    impacto de verdade, e a aterrissagem entra por cima dela, sem
    tranco;
  - no bote o lobo tomba para a frente na inclinação do mergulho;
  - preso no lugar (salto, rugido, aterrissagem), o lobo freia deslizando
    em vez de parar seco.
- `tools/analisar_golpes.gd` também mede o passo do quadril, o quadro
  em que os pés voltam ao chão e o trecho do clipe com movimento.

## v0.26.0 — 2026-11-12

### Adicionado
- **Lobo correndo na parede** com o Running Crawl, deitado de lado com
  a barriga virada para a parede em que está (pela normal dela). Entra
  e sai suave; parado agarrado, o clipe para.
- **`WolfMoves`**: tabela única com trecho, velocidade e instante do
  golpe de cada animação do lobo, medidos nas mãos do esqueleto.
- `tools/analisar_golpes.gd`: mede de novo os golpes quando um clipe
  for trocado.

### Mudado
- **Golpes do lobo sincronizados com a animação:** o dano sai no quadro
  em que a garra chega (garrada 0,31 s e 0,21 s, patada 0,30 s, bote
  0,50 s depois do clique), e o próximo golpe só sai quando a animação
  acaba. Golpe não corta golpe, nem para voltar a humano.
- **A garrada alterna os braços na arma**, não só na animação: o combo
  continua (o terceiro golpe machuca mais).
- **Rugido inteiro:** um toque em R começa, a animação vai até o fim
  (3,6 s) e só então o escudo volta cheio. Soltar não cancela; parado e
  sem atacar o tempo todo. Bots seguem a mesma regra (antes recuperavam
  aos poucos).
- O som da garra sai no instante do golpe.

### Corrigido
- **O jogo abortava ao fechar depois de uma partida longa**
  ("script_bindings is not empty"). Os objetos que o C# monta (animações,
  árvores de animação) ficavam na fila do coletor, e a fila rodava
  depois de o C# já ter desligado. O autoload `Saida` esvazia a fila
  depois que a cena some. Em 25 saídas de teste: 7 abortos antes, 0
  depois.
- O HUD não monta estilo novo a cada quadro, e o golpe do lobo espera
  numa fila da arma em vez de um timer novo por golpe: menos lixo para
  o coletor durante a partida.

## v0.25.1 — 2026-11-12

### Mudado
- **Garrada do lobo alterna os golpes:** cada clique esquerdo usa um
  braço — o "swiping" e o "punch" do pack, um de cada vez.
- **Bote no ar toca só a queda** do "jump attack", do ponto mais alto
  ao impacto. O agachar e o salto do clipe saíram: o bote começa com o
  lobo já no ar.
- **O lobo não tem mais animação de pulo:** ele só pula, e o corpo
  segue na pose em que estava. O clipe `jump.fbx` saiu.

## v0.25.0 — 2026-11-12

### Adicionado
- **A arma escolhida na mão.** Cada arma tem modelo 3D próprio
  (`WeaponModel`): M16A2 com a alça, AK-47 com madeira e carregador
  curvo, SG-552 compacta, UMP-45 quadrada, M3 com o tubo, M9, faca e
  granada. Aparece na sua mão, na dos outros e no chão.
- **Recarga animada.** Primeira pessoa: a arma inclina, o carregador
  sai e volta, e ela endireita; a escopeta empurra cartucho por
  cartucho. Terceira pessoa: o clipe `reload` só da cintura para cima,
  sem parar as pernas. As duas seguem o tempo de recarga da arma.
- **Os outros seguram a arma deles** (`HeldWeapon`), presa na mão
  direita e apontando para a esquerda; troca junto com o slot.
- **Soltar e pegar armas, como no CS.** B joga a arma da mão para
  frente e passa para a próxima. Morto em forma humana deixa a arma
  cair. Passar por cima pega, se o espaço da arma estiver vazio. A arma
  no chão some em 30 s; no máximo 20 no mapa.
- **Personagens por time:** Exo Gray no azul e Ely (K. Atienza) no
  laranja, no lugar do boneco xbot.
- **Lobo de verdade:** o Mutant do Mixamo (um por time, o vermelho no
  laranja), animado com o Creature Pack — parado, andar, correr, pulo,
  garrada, patada, bote, rugido no R, transformação e morte.
- A vitrine da loja e do inventário mostra o Exo Gray e o Mutant.
- **Sair do jogo** no canto direito do topo do lobby, com confirmação.
- HUD: "Pegou: AK-47" e "Soltou: AK-47"; o slot sem arma fica apagado.

### Mudado
- **O uivo (R) toca o mesmo som da transformação**, decisão do Jhon.
  O uivo sintetizado (`lobo_uivo`) saiu.
- Tecla B: soltar a arma. O Enter de recomeçar saiu do mapa de teclas.

### Corrigido
- O soldado aparecia por dentro do lobo: o corpo humano agora some na
  forma lobo (`CharacterAnimator.HideInWolfForm` ligado).
- Aviso do Godot 4.7 sobre ponto de mistura sem nome nas animações.

## v0.24.0 — 2026-11-12

Interface nova inteira, seguindo o guia `docs/UI.md`. Escopo combinado:
tudo o que roda sem servidor.

### Adicionado
- **Kit de interface** (`scripts/ui/kit/`): tokens de design (grade de
  8, paleta, raios, tempos), tema feito em código, fontes Barlow e
  Barlow Condensed com números tabulares (OFL, `assets/fonts/OFL.txt`)
  e componentes: botões em seis variações, ícones desenhados, insígnia
  de patente, moeda que conta ao mudar, barras de atributo, silhuetas
  de arma, abas, modal, contagem em círculo, ping, avisos.
- **Lobby** (`scripts/lobby/`, `menu.tscn`): moldura fixa com header
  (identidade clicável, navegação, missões prontas, Presas e Sangue) e
  miolo que troca. Telas: entrada com apelido e novidades, lista de
  salas (2 × 5 por página, filtros, ir para a sala nº, atualização a
  cada 5 s), sala, loja, inventário com editor de conjuntos e prévia
  do personagem, perfil, missões e opções.
- **Sala**: modo, jogadores por time, objetivo, tempo, armas,
  transformação, dificuldade, faixa de patente, senha, fogo amigo;
  encher com bots, trocar de lado, chat, Pronto e Iniciar com contagem
  de 5 s cancelável. Modos e regras que dependem de servidor aparecem
  como "em breve".
- **Regras da sala valendo na partida** (`RoomRules`): fogo amigo, "Sem
  pesadas", "Só pistolas" (inclusive nos bots) e lobo bloqueado.
- **Perfil v2** (`user://perfil.json`, migra o antigo): patente, XP,
  Presas, Sangue, inventário com prazo, raça, tatuagens, estatísticas
  vitalícias, missões, títulos e histórico de patentes.
- **30 patentes** em 10 famílias (`meta/Ranks.cs`), promoção com
  recompensa e tela própria.
- **Loja** (`meta/Catalog.cs`): armas, granada, tatuagens e troca de
  nome, com prazos de 1, 7, 30 e 90 dias ou permanente, itens do dia
  com desconto, patente mínima e confirmação de compra.
- **Missões**: três diárias e cinco semanais sorteadas pela data, e
  permanentes que dão título. Resgate manual.
- **Recompensa de partida** (`meta/Progression.cs`): XP e Presas por
  jogar, abate, assistência, vitória ou empate e MVP. Abandonar não
  rende nada.
- **Opções** (`user://opcoes.json`): volume geral, efeitos, interface e
  música, sensibilidade do mouse. Também no Esc da partida.
- **HUD de combate novo** (`Hud.cs`), pela regra dos cantos: minimapa
  e nome do mapa em cima à esquerda; feed com ícone da arma em cima à
  direita; forma, escudo e vida embaixo à esquerda; WP e o painel da
  forma embaixo à direita; barra de transformação no centro. O painel
  troca entre humano (arma, pente `/ ∞`, granadas, slots) e lobo
  (recargas de garrada, patada, bote e uivo).
- Mira com abertura dinâmica (`Crosshair.cs`), marcador de acerto
  maior e vermelho na cabeça e no abate (`HitMarker.cs`), vida baixa
  pulsando com vinheta vermelha, barra de transformação que pulsa
  enquanto canaliza.
- **Placar do Tab** (`Scoreboard.cs`): patente, jogador, abates,
  mortes, assistências, pontos, forma e ping; você destacado, morto
  esmaecido, sequência de abates ao lado do nome.
- **Tela de resultado** (`ResultScreen.cs`): veredito sozinho por 1,5 s,
  placar com o MVP, recompensa animada (linhas, barra de patente, Presas
  contando, missões concluídas, promoção) e Jogar de novo / Voltar à
  sala / Sair para o lobby.
- Tela de respawn com o kit: motivo de cada conjunto bloqueado, causa
  da morte ("ABATIDO por X com Y"), contagem em círculo.
- Esc com Opções e Controles; reiniciar e sair pedem confirmação.
- Miniatura do mapa (`tools/gerar_miniaturas.gd`).
- `Weapon.AttackCooldown`, `FormController.ShiftLeft/ShiftTotal`,
  `KillFeed.HowDied`; o abate leva a arma e os assistentes.
- Atalhos de desenvolvimento: `-- --duracao=30 --contagem=2 --placar`
  na partida, `-- --tela=loja --foco=ak47` no lobby.

### Mudado
- **Times: Azul contra Laranja** (era Vermelho). Corpo, base B do
  Hangar, barras e placar seguem a cor; barra de vida sempre na cor do
  time.
- Partida termina na tela de resultado; não há mais Enter para
  recomeçar.
- A pontuação do placar: abate 10, assistência 5.
- Dano direcional dura 1,5 s.

### Removido
- `scripts/menu/MainMenu.cs`, substituído pelo lobby.

### Corrigido
- Recriar as linhas do placar quatro vezes por segundo derrubava o
  jogo ao fechar; agora as linhas são reaproveitadas.
- Item de arma sem arquivo `.tres` (faca) não gera mais erro no
  `Arsenal.Get`.

### Fora do escopo (precisa de servidor)
- Login, canais, amigos, matilha, chat entre jogadores, ranking,
  compra paga, temporadas e moderação.


## v0.23.0 — 2026-11-12

### Adicionado
- **130 arquivos de som** em `assets/audio/`, cobrindo toda a lista:
  passos e pousos (humano e lobo), tiro de perto e de longe para cada
  uma das seis armas, recarga com pente saindo e entrando no tempo de
  cada arma, fim de recarga por arma, cartucho a cartucho da M3,
  bombeada depois do tiro da M3, transformação nas duas direções,
  garra, bote, faca, granada, dor, morte, marcador de acerto, abate,
  contagem, vitória, derrota, interface e ambiente do menu.
- Fontes: Free Firearm Sound Library (CC0), Kenney (CC0) e Oculus Audio
  Pack 1 (CC BY 4.0). Origem de cada arquivo em
  `assets/audio/CREDITOS.md`.
- `tools/sons/baixar.py` e `tools/sons/processar.py`: regeneram tudo a
  partir das gravações originais.
- Fim de recarga por arma (`recarga_fim_<id>`).

### Pendente
- `lobo_uivo` é sintetizado, provisório.
- O Oculus Audio Pack exige crédito na tela de créditos antes de
  publicar.


## v0.22.0 — 2026-11-12

### Adicionado
- **Sistema de som**, pronto para receber arquivos. Cada arquivo com o
  nome certo em `assets/audio/` passa a tocar; o que falta fica mudo.
  Lista em `assets/audio/LEIA-ME.md`.
- `Sfx`: toca por nome, sorteia variações (`passo_1`, `passo_2`...) com
  tom variando, limita vozes simultâneas e cria os canais SFX,
  Interface e Musica.
- `WorldAudio`: passo, pouso, tiro (um por arma, versão distante),
  uivo e explosão, tocados a partir do NoiseBus — o mesmo ruído que os
  bots ouvem. Contagem, início e fim de partida, confirmação de abate.
- `CombatantAudio`: transformação, garra, bote, faca, recarga,
  cartucho, troca de arma, granada, pulo, dor, morte, marcador de
  acerto. Alcance de 12 m para os outros.
- Sons de interface nos menus e música do menu principal.

### Alterado
- **Bots emitem ruído** de passo, pouso e uivo, como o jogador. Antes
  andavam em silêncio. Bots de times opostos passam a se ouvir.


## v0.21.0 — 2026-11-12

### Adicionado
- **Menu principal** como cena de entrada: início, sala, conjuntos e
  inventário.
- **Sala**: times Azul e Vermelho até 8 cada, adicionar e remover bots,
  pontos, tempo e dificuldade; cada um aperta Pronto e o mestre inicia.
- **Arsenal, primeira leva** (`docs/ARSENAL.md`): M16A2, SG-552, AK-47,
  UMP-45, M3 e M9, com dispersão, recuo em fases, queda por distância,
  chumbos e recarga cartucho a cartucho interrompível.
- **Conjuntos** (`docs/CONJUNTOS.md`): quatro salvos no perfil, padrão
  grátis que rende +50 WP, editor com validação, inventário.
- **Tela de respawn** do documento: WP disponível, custo, "faltam X",
  5 s, débito no renascer.
- **Assistência**: +35 WP para quem acertou nos 10 s antes da morte.
- **Dano direcional**: arco que aponta para o atirador e gira com a
  câmera.
- **Menu do Esc sem pausa**: continuar, reiniciar, voltar ao menu,
  sair.

### Alterado
- Economia do documento: abate 100, abate de lobo 70, teto 2500. Dano
  deixa de render WP.
- Minimapa desenha o mapa inteiro com as cores dele e as letras das
  bases.
- Bots da partida nascem da sala (`MatchSetup`), distribuídos pelos
  spawns da base.

### Removido
- Mapa Arrival e a cena de referência.
- O `Loadout` antigo e os quatro lotes `.tres`.

### Corrigido
- Mira não descia depois do recuo: a volta somava ao ângulo em vez de
  subtrair. Agora desce 0,12 s após o último tiro, na velocidade de
  recuperação da arma, descontando o que o jogador puxou com o mouse.
- Corpo morto atravessava o mapa: sem colisão, `IsOnFloor()` dava
  falso e a gravidade voltava. Assentado, o corpo para de vez.

## v0.20.0 — 2026-11-12

### Adicionado
- **Mapa Hangar**, próprio, no espírito do Arrival: 84 × 48 m,
  simétrico, base A (azul) e base B (vermelha), heliponto central,
  quatro blocos com telhado, duas pontes a 5 m, becos de 3 m para a
  corrida em parede. Gerado por `tools/gerar_hangar.py`.
- **Partida**: mata-mata em equipe até 40 abates, contagem de 10 s,
  10 minutos, tela de vitória/derrota/empate com destaque, Enter
  recomeça.
- **HUD da partida**: placar e relógio no topo com barra até a
  vitória, contagem no centro, tabela por jogador no Tab.
- **Times**: você e três bots azuis contra quatro vermelhos, cada um
  com nome. Corpo pintado e barra de vida na cor do time.
- **Spawns por base**, longe do inimigo vivo mais próximo.
- Textura de grade em espaço de mundo para a caixa cinza.

### Alterado
- Bots escolhem alvo entre os inimigos vivos, preferindo quem está à
  vista, em vez de mirar sempre no jogador. Ignoram passo de aliado.
- Menu de dificuldade foi do Tab para o Esc; Tab é o placar.
- Arrival saiu da partida e ficou em `scenes/arrival_referencia.tscn`.

### Corrigido
- Granada feria aliado e o abate dela não contava para ninguém.
- Bots tinham nome vazio no feed: `DisplayName` vazio não é nulo, e o
  `??` que devia cair no nome do nó nunca caía.
- Esc soltava o mouse por conta própria no Player, brigando com o
  `UiState` pelo cursor.

## v0.19.1 — 2026-11-11

### Corrigido
- **Metade das paredes do Arrival não existia para a física.** O mapa
  é feito de planos de uma face e 53% das faces verticais na altura de
  jogo apontam para fora da arena; `ConcavePolygonShape3D` nasce sem
  `BackfaceCollision`, então o jogador via parede e o raio atravessava.
  O `MapLoader` agora monta a forma à mão com colisão de dois lados.
  Isso conserta de uma vez o lobo não grudando, o bot vendo e atirando
  através de parede, o bot não achando cobertura e o tiro atravessando.
- **Normal de raycast orientada contra o raio** no `Cast` da corrida em
  parede: com colisão de dois lados a normal crua do verso inverteria a
  força que segura o lobo, empurrando-o para dentro da parede.

### Alterado
- Log da corrida em parede religado. Ele só imprime quando o motivo
  muda, então não enche o Output.

## v0.19.0 — 2026-11-11

### Adicionado
- **Mapa Arrival** carregado em `scenes/main.tscn`: 82 × 44 m de área
  andável, telhados e passarelas de 9 a 17 m, 5.213 triângulos. O
  `MapLoader` gera a colisão de malha, coleta spawns e assa a
  navegação em tempo de execução.
- **`MapOffset`** no `MapLoader`: o piso do Arrival vem em Y = 7,5 e é
  rebaixado para 0 na entrada, em vez de corrigido no Blender.
- **`NavBounds`**: caixa de bake da navegação. Sem ela o Godot varre
  os 300 m de altura das cortinas do mapa para achar um piso que está
  nos três primeiros metros.
- **`SpawnPoints`**: oito pontos de nascimento, medidos no arquivo por
  folga em volta e teto livre. Nasce-se no mais longe do inimigo vivo
  mais próximo, com ruído na escolha.
- **`scenes/treino.tscn`**: a caixa cinza de antes, preservada para
  medir física sem o mapa no caminho.

### Alterado
- Cinco bots em vez de dois, patrulhando os 38 m de raio do mapa real.
- Alcance do minimapa de 42 para 30 m — em 42 ele mostrava o mapa
  inteiro, o que é planta baixa, não minimapa.
- Pasta do mapa renomeada de `ARRİVAL` para `arrival`: o `İ` era um
  U+0130, que quebra caminho de recurso, git e export para Windows.

## v0.18.1 — 2026-11-11

### Corrigido
- **O lobo voltou a grudar na parede subindo.** A trava do ápice
  barrava a subida inteira. O conserto certo do pulo não era impedir
  de grudar subindo e sim preservar o impulso ao grudar: agora a
  subida continua e a altura só trava quando o pulo se esgota.

### Alterado
- **Botão direito no chão** virou patada pesada da direita: 2,1× de
  dano e um segundo de recuperação.
- **Botão direito no ar** virou bote de três golpes na direção da
  mira, com o pitch — não cai mais reto. Dois segundos de recuperação.
- As patas **alternam** a cada golpe do combo.

## v0.18.0 — 2026-11-11

### Adicionado
- **Cobertura nos bots**: ação `TakeCover` que procura, por raycast,
  um ponto de onde o alvo não enxerga. Nada é marcado no mapa, então
  funciona em qualquer geometria. Recarregar atrás de parede passa a
  valer mais que recarregar exposto.

### Corrigido
- **Lobo colado na parede não conseguia pular**: a corrida grudava no
  frame seguinte ao salto e travava a altura em zero, matando o pulo.
  Agora só gruda a partir do ápice e não gruda rente ao chão.
- Cartão de lote é clicável inteiro; o botão interno saiu.

## v0.17.1 — 2026-11-11

### Corrigido
- **Bots ficavam presos atrás das paredes longas.** O desvio por
  raycast não contorna geometria extensa. Agora há navegação assada em
  tempo de execução (`NavBaker`) e os bots seguem caminho com
  `NavigationAgent3D`.

### Adicionado
- **Reação variada a ruído**: cada bot nasce com uma curiosidade
  sorteada, e o tipo de som pesa — tiro chama mais que passo. Quem
  decide ficar ainda assim vira na direção do som.

## v0.17.0 — 2026-11-11

### Adicionado
- **`MapLoader`**: carrega mapa de `.glb`, gera colisão de malha,
  coleta spawns e assa navegação em tempo de execução. Convenções por
  nome de nó: `-noco`, `-invis`, `spawn*`.
- `assets/maps/LEIA-ME.md` com as convenções e as exigências de
  geometria para a corrida em parede.

## v0.16.0 — 2026-11-11

### Alterado
- **A decisão dos bots virou utility AI.** Dez ações pontuadas de 0 a
  1, com a maior vencendo, no lugar de transições fixas. Considerações
  multiplicam (uma ruim veta a ação), o produto é compensado para não
  punir ações bem avaliadas, e a ação em curso ganha inércia para o
  bot não oscilar.
- Decidir forma, recarregar e recuar deixaram de ser regras soltas e
  passaram a competir como ações.

### Adicionado
- `scripts/ai/Utility.cs` — curvas de consideração separadas da
  decisão.
- `scripts/ai/BotBrain.cs` — contexto e pontuação das ações.
- Ações novas que a máquina de estados não tinha: Reload, Retreat,
  Howl e Reposition.

## v0.15.0 — 2026-11-11

### Alterado
- **O ataque do lobo virou combo de três golpes**, com o terceiro mais
  forte, e deixou de ser hitscan: cada patada cria um volume curto à
  frente do corpo. O alcance passa a variar com a postura em vez de
  seguir a mira.
- **Cada golpe empurra o lobo para frente** (`ClawLunge`), para que
  mira ruim ainda conecte.
- **A corrida em parede é sustentada pelo pulo**, não pelo W, e não
  exige mais estar caindo para grudar.
- **Segurar o ataque agarra na parede sem avançar.**
- **O salto da parede ganha altura** (`WallJumpClimb` 1.35), o que
  permite escalar um corredor ricocheteando entre as duas paredes.

## v0.14.2 — 2026-11-11

### Adicionado
- **Barra de slots** no rodapé, aparecendo ao trocar de arma e sumindo
  sozinha. Mostra os quatro itens com o atual destacado.

### Alterado
- Minimapa virou quadrado e foi para o canto superior direito.
- Feed de abates desceu para baixo do minimapa e ficou maior.

## v0.14.1 — 2026-11-11

### Corrigido
- Com menu aberto, o mouse agora pertence ao menu: cursor visível,
  câmera travada, sem atirar nem andar.
- Dois menus abrindo juntos no início brigavam pelo cursor — fechar o
  de dificuldade capturava o mouse com a tela de lotes aberta. O
  estado virou um contador em `UiState`.
- O relógio da tela de lotes corria por trás do menu de dificuldade e
  escolhia o lote sozinho. Agora só conta quando é a tela de cima.

## v0.14.0 — 2026-11-11

### Adicionado
- **Lotes de armamento** com escolha em 10 segundos, no início e a
  cada morte. Quatro lotes: padrão (concede 50 WP), assalto (200),
  precisão (420) e elite (520). Tempo esgotado entra o padrão.
- **Munição por carregador** com reserva infinita, recarga no R
  (humano) e recarga automática ao atirar com o carregador vazio.
- **Recuo por arma**, com teto de acumulação.
- **Minimapa** no canto superior esquerdo, girando com o jogador e
  mostrando só quem está no alcance.
- **Resistência de corpo do lobo**, atravessada por headshot.

### Alterado
- Alcance da garra aumentado (2,5 → 3,8 no Bruto).
- WP vira número puro com teto de 5000; HUD direito mostra WP,
  munição e forma, como na referência.
- Bots avançam enquanto atiram, em vez de administrar distância, e
  patrulham puxando para o meio do mapa.
- Escudo só volta a escorrer 1 segundo depois de ser recarregado.
- HUD reescrito: esquerda é o corpo, direita é o equipamento.

## v0.13.1 — 2026-11-11

### Alterado
- O golpe especial do lobo passa a ser uma **investida**: ao apertar,
  ele despenca na direção em que olha enquanto dá as duas patadas.

### Adicionado
- **Braços e garras do lobo** presos ao corpo, visíveis em terceira
  pessoa e para os outros jogadores, com balanço ao correr e abertura
  no golpe.

## v0.13.0 — 2026-11-11

### Adicionado
- **Times**: azul (jogador) e vermelho (bots), com fogo amigo
  desligado.
- **Feed de abates** no canto superior direito, com nomes coloridos
  por time e a causa da morte: na cabeça, na faca, de lobo, de
  granada.
- **Patada dupla do lobo**: botão direito no ar. Dois golpes em
  sequência, 1,45× de dano e cooldown quase o dobro.
- **Bônus da patada contra humano** (1,6×).

## v0.12.0 — 2026-11-11

### Alterado
- **Vida é uma só**: lobo e humano compartilham o mesmo pool. O que a
  forma acrescenta é o escudo. Vida separada fazia de transformar um
  segundo tanque de vida.
- **Slots**: 4 virou faca, granada foi para o 5.
- **Rugido completa em 1 segundo** segurando R, em vez de recuperar
  continuamente. Soltar antes do fim não recupera nada — é o que
  torna rugir uma aposta.
- **WP vem de abater (150) e renascer (40)**, não mais do relógio.
  Ganhar por tempo premia esperar; ganhar por abate premia jogar.
- Painel de WP, slot e granadas foi para o lado direito da tela, com
  a vida à esquerda. Separar os recursos no espaço evita ler o errado
  no meio da luta.

### Adicionado
- **Faca** no slot 4: 85 de dano, alcance curto, sem ruído de tiro.
- **Imortalidade de 3 segundos ao renascer**, para quem morre perto de
  um inimigo não voltar direto para a mesma morte.

## v0.11.2 — 2026-11-11

### Corrigido
- Bots continuavam atirando no jogador morto: a percepção via o nó do
  alvo mas não checava se ele estava vivo. Alvo abatido agora deixa de
  ser alvo, e o bot esquece o que sabia e volta a patrulhar.

## v0.11.1 — 2026-11-11

### Adicionado
- **Andar devagar com Shift**, só na forma humana: 42% da velocidade
  e sem ruído. Segunda ferramenta de furtividade ao lado do agachar.
- **Clarão no cano** e **recuo de câmera** a cada disparo.

### Alterado
- O traçante passa a nascer na ponta do cano em vez da câmera. O raio
  continua partindo da câmera, que é de onde vem a precisão.

## v0.11.0 — 2026-11-11

### Corrigido
- **R não rugia**: a ação `howl` estava gravada no `project.godot`
  com F, do Input Map configurado à mão no início, e o `InputSetup`
  não sobrescreve ações existentes. A seção `[input]` foi removida e
  o código virou a única fonte das teclas.
- **Sair do lobo parecia levar dano**: o alerta vermelho vinha de ver
  a vida baixar, e trocar de forma troca o pool de vida. Agora vem do
  sinal de dano.
- Transição de volta para humano caiu de 0,8s para 0,2s.

### Alterado
- **Cada forma guarda a própria vida.** Sair do lobo e voltar devolve
  o lobo como estava, em vez de restaurar proporção. Morrer zera os
  dois corpos.
- **Granada equipa em vez de arremessar**: 4 equipa, o botão de ataque
  joga. Depois do arremesso volta sozinha para a arma anterior.
- Corpos mais magros (raio 0,4 → 0,3) e zona de cabeça menor
  (0,55 → 0,72 do centro), tornando o headshot mais difícil.

### Adicionado
- **Arma visível em primeira pessoa** (`ViewModel`): rifle, pistola,
  granada e garras, com recuo no disparo e balanço no passo. Formas
  geométricas por enquanto; trocar por modelo é trocar a malha.

## v0.10.1 — 2026-11-11

### Adicionado
- **Patrulha**: em Idle o bot escolhe um ponto aleatório da área, vai
  até ele devagar, pausa e escolhe outro. Patrulhar devagar também faz
  menos barulho.
- Detector de travamento: sem navmesh não há garantia de que o destino
  seja alcançável, então o bot desiste quando para de progredir em vez
  de empurrar a parede.
- Velocidade e pausa de patrulha entram nos perfis de dificuldade.

## v0.10.0 — 2026-11-11

### Adicionado
- **Barramento de ruído** (`NoiseBus`): passos, aterrissagem, tiro,
  rugido e explosão publicam posição e raio. Agachado não emite nada.
- **Audição nos bots**: localizam a origem do som com erro, porque
  ouvir não é ver. Não acumula com a visão — quem já vê o alvo ignora
  som.
- **Máquina de estados do bot**: Idle, Investigate, Combat e Search.
  O bot age sempre sobre a última posição conhecida, nunca sobre a
  posição real do jogador. Ao chegar sem achar ninguém, varre o
  entorno girando e desiste.
- **Barra de vida sobre os bots**, no mundo e não no HUD, com billboard
  e teste de profundidade — barra visível através da parede entregaria
  posição de graça. Mostra escudo quando há.
- Audição e precisão de localização entram nos perfis de dificuldade.

## v0.9.0 — 2026-11-11

### Adicionado
- **Menu de dificuldade** (Tab, e aberto no início): fácil, médio e
  difícil. Muda reação, erro de mira, taxa de erro deliberado,
  sustentação de fogo, percepção e mobilidade — nunca vida ou dano.
- **Erro deliberado** nos bots (`MissChance`): fração dos tiros que
  sai de propósito com desvio grande, separada do erro de mira comum.
  Nem no difícil vai a zero.
- **Escudo do lobo escorre** com o tempo (`ShieldDecay`). Deixa de ser
  vida extra e vira relógio.

### Alterado
- Rugido mudou de F para **R** e passou a ser **segurado**: recupera
  escudo continuamente enquanto trava o lobo no lugar.
- Vida e escudo passam a ser exibidos como números grandes em vez de
  barras. Com um recurso que vaza, o jogador precisa ler o valor.
- Escudo do Bruto 100 → 200, do Ligeiro 60 → 140.

## v0.8.0 — 2026-11-11

### Adicionado
- **Dano na cabeça**: 2,5× o dano normal. A zona é a faixa superior
  do corpo, medida a partir do centro, então continua válida quando
  houver modelo no lugar da cápsula.
- **Marcador de acerto**: quatro riscos em volta do crosshair,
  brancos no corpo e vermelhos e maiores na cabeça.
- **Alerta de dano**: faixas vermelhas nas quatro bordas da tela ao
  levar tiro, desaparecendo em menos de meio segundo.
- **Reação do bot ao ser alvejado**: marca a origem do tiro, espera
  um tempo curto com variação aleatória e então gira mais devagar que
  o normal. Reação instantânea é desumana e anula o prêmio de atacar
  pelas costas.
- Sinais `Damaged` em `Health` (com a origem do dano) e `HitConfirmed`
  em `Weapon`.

## v0.7.3 — 2026-11-11

### Alterado
- Saltar de uma parede para **outra** volta a grudar. A proibição
  deixa de ser "uma corrida por ida ao ar" e passa a ser "não na mesma
  parede": o elevador continua impossível, mas quicar entre paredes
  opostas de um corredor funciona. O critério é a semelhança entre as
  normais (`SameWallThreshold`), não o tempo nem o contato com o chão.

### Corrigido
- Traçante criava um material novo a cada disparo, o que obriga o
  Godot a montar pipeline de render na hora e travava o quadro com
  dois bots atirando. Agora o material é único e compartilhado.

## v0.7.2 — 2026-11-11

### Corrigido
- Morrer era indistinguível de travar: a tela não dizia nada, o
  controle parava e o jogador revivia no mesmo lugar 3 segundos
  depois. Agora o HUD mostra "ABATIDO" com a contagem.
- Reviver devolvia o jogador exatamente de onde ele tinha sido morto,
  ou seja, de volta ao tiroteio e ao mesmo bot. Jogador e bot voltam
  ao ponto de origem.
- Bot que morria como lobo revivia como lobo. Volta a humano ao
  renascer.

### Adicionado
- Sinal `Respawned` em `Health`.

## v0.7.1 — 2026-11-11

### Corrigido
- O bot virava lobo a menos de 9m e, sendo o lobo corpo-a-corpo,
  grudava no jogador e nunca atirava. Agora virar lobo é recurso de
  desespero: só abaixo de 40% de vida e com o alvo ao alcance.
- Disparos não tinham retorno visual nenhum, então o bot parecia
  inofensivo enquanto acertava. Todo tiro agora deixa traçante.

### Adicionado
- Combate à distância no estilo de bots de FPS: mantém o alcance da
  arma, circula com strafe de lado alternado, e atira em rajadas com
  pausa em vez de fogo contínuo.
- `EngageRange` limita o alcance efetivo do bot — acertar do outro
  lado do mapa torna o jogo insuportável.

## v0.7.0 — 2026-11-11

### Adicionado
- **Bot oponente** (`scripts/actors/Bot.cs`) reusando Health,
  FormController e Weapon do jogador. Percepção por distância, campo
  de visão e linha de visada, com memória curta. De humano mantém o
  alcance da arma; de lobo fecha a distância. Duas instâncias no mapa.

### Alterado
- **Projeto reorganizado em pastas**: `scenes/`, `resources/breeds/`,
  `scripts/{actors,combat,forms,ui,core,net,debug}` e `docs/`. Antes
  eram 14 arquivos soltos na raiz. `CLAUDE.md` e `AGENTS.md` ficam na
  raiz, que é onde agentes procuram.
- `run/main_scene` passou a apontar para o caminho explícito em vez de
  um uid, que não sobrevive bem a mover arquivos fora do editor.

## v0.6.1 — 2026-11-11

### Corrigido
- Atraso ao voltar para humano: a física já trocava no início da
  transição, mas a câmera só voltava à primeira pessoa no fim. Agora
  a câmera segue o corpo pela mesma regra.

### Adicionado
- Segurar o pulo durante a queda alivia a gravidade em 22%
  (`HoldFallGravity`). Sutil de propósito: controle fino de onde
  aterrissar, sem virar planagem.

## v0.6.0 — 2026-11-11

### Alterado
- **A física troca no início da transição de forma**, não no fim.
  Antes, apertar 3 e pular em seguida ainda usava o pulo humano
  durante os 0,8s da transição. A vulnerabilidade da transição
  continua sendo a impossibilidade de atacar, não de se mover.
- Pulo passa a ser um por toque: segurar não repete e uma trava curta
  impede que o mesmo toque dispare o pulo do chão e o da parede.
- Granada também respeita a transição: não é arremessada enquanto a
  troca de forma está em curso.

## v0.5.4 — 2026-11-11

### Corrigido
- O alcance do pulo não respondia à tecla: o impulso era conservado
  por inteiro e o salto sempre ia até o fim. Um arrasto suave no ar
  (`AirFriction`) devolve o controle — segurar mantém o avanço,
  soltar encurta.
- Ao encontrar uma parede em ângulo durante a corrida, o lobo voltava
  pelo outro lado. O sentido era decidido comparando com o sentido
  antigo, e em paredes perpendiculares essa comparação dá zero. Agora
  quem decide é o movimento que o lobo já carrega, descartando o lado
  bloqueado.

## v0.5.3 — 2026-11-11

### Corrigido
- Soltar as teclas no ar travava o corpo no lugar, inclusive no meio
  do pulo. A desaceleração era aplicada sem considerar o tempo nem se
  o jogador estava no chão, zerando a velocidade horizontal em um
  frame. Agora chão e ar são tratados separadamente: o chão freia
  rápido (`GroundFriction`) e o ar conserva o impulso com aceleração
  reduzida (`AirAccel`).

## v0.5.2 — 2026-11-11

### Corrigido
- Agachamento acontecia em dois tempos. A cápsula encolhe em torno do
  próprio centro, então os pés subiam no ar e a gravidade os trazia de
  volta. Agora o corpo desce metade da diferença de altura a cada
  passo, mantendo os pés plantados.
- Transição de agachar ficou mais rápida (12 → 22).

## v0.5.1 — 2026-11-11

### Alterado
- Humano mais lento e com pulo mais curto: velocidade 5.2 → 4.2,
  pulo 3.9 → 3.0.
- Multiplicadores das classes subiram para compensar, já que a
  velocidade do lobo deriva da humana: Bruto 1.1 → 1.5 de velocidade
  e 1.3 → 1.9 de pulo; Ligeiro 1.6 → 2.1 e 1.8 → 2.5. Sem isso o lobo
  teria encolhido junto com o humano e o contraste continuaria igual.

## v0.5.0 — 2026-11-11

### Adicionado
- **Agachar** (Ctrl) nas duas formas: encolhe a cápsula de 1.8 para
  1.0, baixa o olho e corta a velocidade para 45%. Não funciona
  durante a corrida em parede.
- Volta ao spawn ao cair abaixo de `FallLimit`. Antes, sair do mapa
  era queda infinita.

### Alterado
- Humano mais lento e mais pesado: velocidade 7.0 → 5.2, pulo
  5.0 → 3.9. A mobilidade passa a pertencer ao lobo.
- Diagnóstico de parede imprime só quando a mensagem muda, e a
  transição de quina passou a avisar quando dispara e quando desiste.

## v0.4.3 — 2026-11-11

### Corrigido
- O lobo regrudava na parede na mesma altura logo após o salto,
  transformando a parede em elevador. Agora é necessário tocar o chão
  entre uma corrida e a seguinte (`RequireFloorBetweenWallRuns`).
  Desligando a exigência, um tempo mínimo (`WallRunCooldown`) impede
  o regrude imediato e permite encadear paredes opostas.

## v0.4.2 — 2026-11-11

### Corrigido
- O salto da parede zerava o avanço acumulado na corrida e empurrava
  o lobo apenas de lado. Agora conserva uma fração do avanço
  (`WallJumpKeepSpeed`, 0.8) e soma o empurrão perpendicular, o que
  torna possível saltar em direção a alguém à frente.

## v0.4.1 — 2026-11-11

### Alterado
- A direção da corrida em parede passa a vir do **momento** do lobo,
  não da câmera. Saltar de uma parede e encostar na seguinte mantém o
  sentido sem precisar mover o mouse. O olhar só decide quando não há
  movimento horizontal.
- `FindWall` virou um leque de 8 raios em volta, substituindo os dois
  raios laterais. O ângulo do corpo deixa de influenciar se a parede é
  encontrada, e a parede mais próxima vence.

## v0.4.0 — 2026-11-11

### Alterado
- Corrida em parede só começa **na descida do pulo**. Encostar na
  parede estando no chão não gruda mais.
- Sucção contra a parede (`WallStickForce`): o lobo fica colado em vez
  de apenas correr perto da superfície.

### Adicionado
- **Transição de quina**: ao acabar uma face, a corrida troca para a
  face seguinte em vez de soltar. A nova normal é a direção em que se
  corria; o novo sentido é o oposto da normal antiga. Uma janela de
  tolerância (`CornerGrace`) cobre o trecho sem contato entre as duas
  faces. Permite dar a volta completa num bloco.

## v0.3.0 — 2026-11-11

### Alterado
- **Transformação livre**: `WpCost` das classes básicas vai a zero.
  Decisão do autor. O WP continua no sistema para as classes caras.
- **Transformar não cura mais**: a proporção de vida passa a ser
  preservada entre os corpos. Com transformação gratuita, encher a
  vida a cada troca era cura infinita e tornava o uivo inútil.
  `RefillOnTransform` traz o comportamento antigo de volta.
- `Health.SetPool` ganhou o modo `keepRatio`.

## v0.2.1 — 2026-11-11

### Corrigido
- Grupo `player` no `main.tscn`: a declaração vai dentro dos colchetes
  do cabeçalho do nó. Como linha de propriedade o Godot ignorava em
  silêncio e o HUD não encontrava o jogador.
- HUD ganhou varredura da cena como plano B quando o grupo não existe.

### Alterado
- Corrida em parede: altura travada (não sobe nem desce) e direção
  travada no momento de grudar. O olhar passa a ser livre durante a
  corrida — mirar não desvia mais o movimento. Para mudar de lado é
  preciso cair e recomeçar.
- `WallRunGravityScale` virou `WallRunVerticalDrift`, em metros por
  segundo, com zero como padrão.

## v0.2.0 — 2026-11-11

### Adicionado
- Granada de atordoamento (`Grenade.cs`, `grenade.tscn`): arremesso em
  arco com física, pavio de 1,8s, dano com falloff e atordoamento em
  área. Duas por vida, reabastece ao renascer, só na forma humana.
- Atordoamento no `Player`: reduz velocidade e desativa a corrida em
  parede. É a resposta do humano contra o lobo.
- Contador de granadas no HUD.
- `CLAUDE.md` — guia para agentes, com as regras de design, a
  arquitetura e o que não pode ser violado.

### Decisões de design
- A granada tira mobilidade, não dá dano pesado — o poder do lobo vem
  da mobilidade, então é ela que precisa ser atacada.
- Granada é slot de ação, não de arma: apertar 4 arremessa na hora,
  sem deixar o jogador segurando granada no meio do tiroteio.
- Arremesso soma a velocidade do jogador, então correr e jogar leva
  mais longe.

## v0.1.0 — 2026-11-11

Primeira versão jogável. Fase 1, local, sem rede.

### Adicionado
- Controlador em primeira pessoa
- Vida, escudo separado do HP, morte e respawn
- Máquina de estados da transformação com quatro estados e economia
  de WP
- Sistema de slots: 1 principal, 2 secundária, 3 lobo, 4 granada
- Duas armas: principal automática, secundária semiautomática
- Garrada do lobo com bônus de ataque aéreo
- Uivo: recarrega escudo, trava no lugar
- Corrida em parede e salto da parede, só na forma lobo
- Câmera em primeira pessoa no humano, terceira pessoa no lobo
- HUD com vida, escudo, WP e slot atual
- Registro de input por código, sem configuração no editor
- Boneco de teste que pisca ao levar dano
- Classes Bruto e Ligeiro como recursos editáveis

### Decisões de design
- Transformar é um slot, não um botão separado
- Corrida em parede sem limite de tempo; solta ao largar W ou bater
- Câmera não inclina na parede
- Sem restrição de HP para transformar; o único custo é WP
- Estados de transição são reais, com hitbox e regras próprias
