# Pendências

O que ainda falta no Alcateia, num lugar só. Defeito relatado vem
primeiro; depois o que foi pedido e ainda não existe; por fim as
pendências técnicas que já se conhecem.

Resolveu um item? Tire daqui e escreva no `CHANGELOG.md`. Achou um novo?
Ponha aqui, com o que se sabe dele.

## Defeitos relatados (13/11/2026, jogando no Mac)

| Defeito | O que se sabe |
|---|---|
| **Faca e granada** | Corrigido (13/11) com os modelos do Sketchfab na mão do braço da pistola. A faca agora usa o conjunto animado que o Jhon trouxe (golpes animados). Falta o Jhon conferir. |
| **Glitches nos mapas** | Varridos os quatro (13/11) com `tools/mapas/conferir.py`: faces brigando, letreiros enterrados ou soltos, objetos flutuando na Polar — corrigidos. O conferidor só olha caixas retas: rampas, formas livres (trem, tanques, terreno) e os modelos prontos não são conferidos. Se ainda houver, falta print ou o lugar exato. |
| **Glitches na troca de arma / braços reiniciando** | Corrigidos os que se acharam (tamanho e lugar dos braços, travada na primeira troca, sacar; o pulo do M16 parado e o recomeço ao perder o chão). Falta o Jhon conferir. |
| **Personagens tremendo e piscando** | Corrigido (13/11): o desenho agora anda entre os passos da física (`VisualSmoothing`) e a borda vermelha saiu de cima da pele. Não deu para ver no Mac daqui: falta o Jhon conferir; se continuar, um vídeo curto mostra se é tremor de posição, de animação ou de sombra. |

## Pedido e ainda não feito

Lista do Jhon (13/11/2026), agrupada. "Hoje" diz o que já existe.

### Arte própria

- **Animações próprias das mãos.** Hoje: as do Sketchfab (CC BY) nas
  armas de fogo, as de código na faca e na granada.
- **Modelos próprios de armas.** Hoje: Sketchfab nas armas de fogo,
  código na faca e na granada.
- **Modelos próprios de humano e de lobo.** Hoje: Mixamo.
- **Logotipo do jogo.** Hoje: só o nome escrito na fonte do menu.
- **Imagens das patentes melhores.** Hoje: insígnias desenhadas por
  código (`RankBadge`).
- **Lootbox em 3D, com animação de abrir.**

### Combate e visual

- **Abrir a mira com o botão direito**, nas armas que têm mira (zoom,
  arma no centro da tela, menos dispersão). Hoje não existe.
- **Efeito de sangue nos tiros** (no corpo atingido e no chão/parede
  atrás).
- **Efeito de tiro melhor** (clarão, fumaça, traçante, marca na parede).
- **Animação de quadrúpede e de ataques do lobo melhores.**
- **Sombreamento melhor nos mapas, levando em conta o personagem**
  (sombra do personagem e luz do mapa sobre ele).
- **Qualidade de textura baixa, média e alta** nas opções (e, junto,
  sombra, oclusão, distância de desenho). Hoje não há opção gráfica.
- **Áudios melhores e mais precisos.**
- **HUD, fontes e menu melhores.**

### Mapas e modos

- **Mais mapas.**
- **Modo bomba**, com mapas feitos para ele (áreas de plantio, ataque e
  defesa, rodadas). Hoje só existe Equipe (mata-mata por time).

### Personagens, loja e economia

- **Comprar personagens novos e jogar com eles** — loja, inventário e
  partida (primeira e terceira pessoa, e na rede).
- **Cada personagem com o seu próprio lobo — e o lobo muda a raça**
  (decidido pelo Jhon, 13/11): estatísticas e golpes de cada raça.
  Cuidado com a regra de design 1 (um eixo de troca por classe) e com a
  3 (nenhuma vantagem permanente): cada raça nova troca uma coisa por
  outra, não é melhor que as outras.
- **Armaduras em personagens e lobos, dando mais vida** (decidido pelo
  Jhon, 13/11). Entra no `docs/ARSENAL.md` e nos testes de equilíbrio.
  Proposta para não virar "paga para ganhar": a armadura se compra com
  **Presas** (moeda de jogar), nunca com Sangue; o bônus é pequeno e com
  teto (como as tatuagens de hoje); e, se der vida, tira alguma outra
  coisa (velocidade, por exemplo) — regra 1.
- **Cash: dinheiro que o jogador paga.** Hoje o desenho já prevê o
  Sangue (moeda paga que nunca compra poder — `Catalog.cs`), mas não há
  compra. Falta: loja de Sangue, pagamento (Pix, cartão), saldo no
  servidor, recibo, estorno.
- **Lootboxes**, com animação e modelo 3D. **Atenção legal:** o ECA
  Digital (Lei 15.211/2025) proíbe caixas de recompensa em jogos voltados
  a crianças e adolescentes ou de acesso provável por eles, e o governo
  já cobra verificação de idade de quem as mantém. Antes de fazer, falar
  com advogado e decidir: classificação etária, verificação de idade, ou
  trocar por recompensa sem sorteio pago.
- **Recompensa diária definida no servidor**: armas, WP, lootboxes etc.
- **Desafios inseridos pelo servidor.** Hoje as missões existem, mas são
  fixas no jogo (`Missions.cs`): passam a vir do servidor.

### Contas e cliente

- **Sistema de contas.** Hoje: criar conta e entrar já existem (aba
  Online + servidor). Falta: e-mail, confirmação, recuperar senha, trocar
  senha, apagar conta (LGPD), entrar logo ao abrir o jogo.
- **Cliente do jogo onde o jogador entra na conta e dá play: o login fica
  na primeira tela do próprio jogo** (decidido pelo Jhon, 13/11). Sem
  launcher separado. A tela de entrada (`WelcomeScreen`) vira a de
  login; o jogo offline com bots continua acessível (treino).
- **Perfil, inventário e moedas no servidor** (hoje ficam no arquivo do
  jogador — dá para editar à mão).

### Comunicação na partida

- **Chat dentro da partida**: para o time e para todos, com o nome e a
  cor do time, por cima do jogo sem tirar o controle. O saguão já tem
  chat (`LobbyLink`); falta na partida, pelo servidor de partida.
- **Rádio**: chamadas prontas para o time ("Preciso de ajuda", "Lobo
  vindo", "Recuar", "Entendido"…), num menu rápido de teclas, com a voz
  gravada e a frase no chat do time. Marca no mapa onde quem chamou está.

### Ranking

- **Ranking que dura anos: ~8 anos de jogo até a última patente.**
  Hoje: 30 patentes, a última com ~150 mil XP; uma partida dá ~350 XP,
  então ~430 partidas (~70 horas) chegam ao topo. Para 8 anos, a curva
  precisa de outra conta: definir quanto o jogador típico joga por dia
  (por exemplo, 5 partidas por dia × 8 anos ≈ 14.600 partidas ≈ 5 milhões
  de XP) e, provavelmente, mais patentes. A curva fica em `Ranks.XpFor`
  e no servidor.

### Servidor e produção

- **Anti-trapaça** — hoje a vida e o acerto moram na máquina de cada
  jogador (`docs/decisoes/0008`). Passo um: vida e acerto no servidor de
  partida; depois, conferir velocidade e linha de tiro no servidor.
- **Subir em produção, na região do Brasil** (São Paulo), com domínio,
  HTTPS, banco gerenciado com backup, e servidores de partida perto dos
  jogadores.
- **Aguentar muitos jogadores ao mesmo tempo**: teste de carga (contas,
  saguão, partidas simultâneas), vários servidores de partida subindo
  sozinhos conforme a fila, saguão em mais de uma máquina (hoje as salas
  ficam na memória de um processo só), monitoramento.

## Pendências técnicas conhecidas

- **Braços animados não conferidos na tela.** As posições do M16 e da
  escopeta foram as mais chutadas.
- **Servidor no Docker nunca construído**: `docker compose up -d --build`
  é a primeira vez.
- **Partida online entre duas máquinas** nunca jogada (só dois jogos sem
  tela na mesma máquina).
- **Online**: compensação de lag, predição, reconexão, golpes
  especiais do lobo pela rede.
- **Bots online** (feito em 13/11) dependem da máquina do dono da sala:
  falta passar os bots para outro jogador quando o dono cai, e a sala
  online escolher a dificuldade deles.
- **Mapas vistos só no renderizador Compatibility**: ajustar luz e
  sombra no Forward+.
- **Bots** na Polar atiram metade do que atiravam (76 tiros em 40 s,
  eram 146); na Vila, 5 de 7 saem da base.
- **Modelos avulsos do Sketchfab** (AK-47, Beretta, SG552, UMP45, Benelli
  M3) baixados e não usados.
