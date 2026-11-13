# Pendências

O que ainda falta no Alcateia, num lugar só. Defeito relatado vem
primeiro; depois o que foi pedido e ainda não existe; por fim as
pendências técnicas que já se conhecem.

Resolveu um item? Tire daqui e escreva no `CHANGELOG.md`. Achou um novo?
Ponha aqui, com o que se sabe dele.

## Defeitos relatados (13/11/2026, jogando no Mac)

| Defeito | O que se sabe |
|---|---|
| **Faca e granada não mudaram** | Ficaram com as mãos e os modelos feitos por código. Os modelos do Sketchfab (faca Gerber, granada M84) estão baixados, mas não têm braços nem animação: falta convertê-los e animar as mãos. |
| **Glitches nos mapas** | Não descrito ainda. Suspeitos: as paredes com porta e janela novas (`m.parede`), os letreiros (planos encostados na parede podem piscar com ela), os objetos do Poly Haven. Precisa de print ou do lugar exato. |
| **Glitches na troca de arma** | Provável: os braços animados (`ViewModel.Rig.cs`) — o conjunto novo entra sem a subida de "sacar" em algumas armas (pistola e escopeta não têm esse clipe), e a posição de cada conjunto não foi conferida na tela. |
| **Personagens com glitch** | Não descrito ainda. Precisa saber se é o próprio corpo, os bots, o lobo, ou os bonecos da partida online. |
| **Salas pré-criadas em vez das online** | A tela Salas ainda mostra as salas de mentira de `RoomDirectory.Seed()` (offline, com bots). As salas de verdade estão na aba Online. Decidir: a tela Salas passa a mostrar as do servidor quando houver conta, e as de mentira somem. |
| **Os mapas não aparecem para trocar** | `Maps.All` tem os quatro, e o seletor de criar sala e o filtro da lista usam essa lista. Precisa ver em que tela não apareceu (criar sala offline? sala já criada? online?). |

## Pedido e ainda não feito

- **Animações próprias das mãos** — hoje são as do Sketchfab (CC BY).
- **Modelos próprios de armas** — hoje são os do Sketchfab nas armas de
  fogo e os de código na faca e na granada.
- **Modelos próprios de humano e de lobo** — hoje são do Mixamo.
- **Comprar personagens novos e jogar com eles** — a loja precisa vender
  personagem, o inventário guardar, e a partida usar o escolhido (no
  corpo de primeira e de terceira pessoa, e na rede).
- **Cada personagem com o seu próprio lobo** — o personagem escolhido
  decide o lobo em que se transforma. Decidir: o lobo é só aparência ou
  também muda a raça (estatísticas, golpes)?
- **Armaduras para personagens e lobos** — decidir: só aparência ou com
  efeito de jogo (vida, escudo, velocidade)? Se tiver efeito, entra em
  `docs/ARSENAL.md` e nos testes de equilíbrio.
- **Anti-trapaça** — hoje a vida e o acerto moram na máquina de cada
  jogador (`docs/decisoes/0008`). O passo um é levar a vida e o acerto
  para o servidor de partida; depois, conferir velocidade e linha de
  tiro no servidor.

## Pendências técnicas conhecidas

- **Braços animados não conferidos na tela.** As posições do M16 e da
  escopeta foram as mais chutadas.
- **Servidor no Docker nunca construído**: `docker compose up -d --build`
  é a primeira vez.
- **Partida online entre duas máquinas** nunca jogada (só dois jogos sem
  tela na mesma máquina).
- **Online**: compensação de lag, predição, bots, reconexão, golpes
  especiais do lobo pela rede, perfil da loja no servidor, HTTPS.
- **Mapas vistos só no renderizador Compatibility**: ajustar luz e
  sombra no Forward+.
- **Bots** na Polar atiram metade do que atiravam (76 tiros em 40 s,
  eram 146); na Vila, 5 de 7 saem da base.
- **Modelos avulsos do Sketchfab** (AK-47, Beretta, SG552, UMP45, Benelli
  M3) baixados e não usados.
- **Fase 1**: os quatro testes de validação do `docs/ARSENAL.md` e "A
  pergunta que decide o projeto" (fim do `CLAUDE.md`).
