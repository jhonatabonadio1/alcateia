# Painel administrativo

A tela de quem cuida do jogo: jogadores, banimentos, moeda e itens,
armas e skins, mapas, promoções, desafios, segurança e auditoria. É
servida pelo próprio servidor, numa porta que não se abre para a
internet (`docs/decisoes/0012`).

## Abrir

Com o `docker compose up -d --build` da raiz, o painel está em
**http://127.0.0.1:8443** (no `docker compose`; dentro do contêiner, 8081), na máquina do servidor. De outra máquina:

```
ssh -L 8443:127.0.0.1:8443 usuario@servidor      # e abra http://127.0.0.1:8443
```

O primeiro administrador é o do `.env` (`ALCATEIA_ADMIN_USER` e
`ALCATEIA_ADMIN_PASSWORD`, 12 caracteres ou mais) e é o **dono**. Na
primeira entrada: **Minha conta → trocar a senha e ligar as duas
etapas**; depois apague as duas linhas do `.env`.

Trancou-se fora (perdeu o celular das duas etapas, esqueceu a senha)? Na
máquina do servidor:

```
docker compose exec servidor dotnet Alcateia.Servidor.dll --admin SEU_NOME
```

Pede a senha nova, desliga as duas etapas dessa conta e a destrava.

## O que tem

| Tela | Para quê | Quem pode mexer |
|---|---|---|
| **Visão geral** | Online, salas, partidas, contas e partidas por dia, eventos de segurança por hora, endereços suspeitos, moeda em circulação. Atualiza a cada 5 s | todos veem |
| **Jogadores** | Busca por nome, número ou IP. Na ficha: nível (e quanto falta para o próximo), carteira, inventário, extrato, partidas, alertas de trapaça, contas no mesmo endereço. Ações: **banir** (com prazo e motivo), desbanir, derrubar conexão, **dar ou tirar Presas e Sangue**, dar e tirar item, **mudar o nível** (1 a 3000, com motivo: o XP vai para o começo do nível; sobe ou desce; não paga o prêmio de subir de nível) | moderador bane; administrador mexe em moeda, item e nível |
| **Frota e salas** | Os servidores de partida vivos, o que cada um roda, e as salas do saguão | todos veem |
| **Armas, skins e itens** | Todo o catálogo com a arte do jogo: nome, raridade, patente mínima, preço por duração, em destaque, vem com a conta, **ligado ou não** | administrador |
| **Mapas** | Cada mapa com a foto: **liga e desliga a rotação** com um clique. Mapa desligado não abre sala nova. O cartão só anuncia modo que o jogo já tem (hoje, Equipe): mapa feito para um modo que ainda não existe, como a Obra para o mata-mata, diz isso e em que modo se joga nele por enquanto | administrador |
| **Promoções** | Desconto com começo e fim num item à venda. Vale na loja sozinho, no horário | administrador |
| **Desafios** | As missões diárias, semanais, permanentes e de **evento** (com datas): o que conta, o alvo e o prêmio (Presas, XP, Sangue, item por tantos dias, título). Das diárias e semanais ligadas o servidor sorteia as que valem em cada período (as mesmas para todos); a etiqueta **Valendo hoje / esta semana** mostra quais | administrador |
| **Eventos** (Segurança) | Tudo o que o escudo viu, com filtro por tipo, gravidade e IP; bloquear um endereço dali mesmo. O filtro **Partidas** mostra o que deu errado nos servidores de partida: servidor que sumiu, jogador recusado, ligação que caiu, partida vazia, resultado que não chegou, erro e lentidão (detalhe em `SEGURANCA.md`) | moderador bloqueia |
| **Blacklist de IP** | Endereços e faixas bloqueados (pelo escudo ou pela equipe), com prazo e motivo | moderador |
| **Banimentos** | As contas banidas e o histórico | todos veem |
| **Auditoria** | Quem fez o quê no painel, em quem e de onde | todos veem |
| **Configurações** | Manutenção (fecha a entrada, com recado), evento de bônus (XP e Presas multiplicados até uma data), aviso do saguão, versão mínima do jogo, **vitrine do dia** (quantos itens entram em oferta a cada dia e com quanto de desconto; 0 desliga) e **rodízio dos desafios** (quantas diárias e semanais valem por vez; 0 = todas as ligadas) | dono |
| **Administradores** | Criar, mudar o papel, trocar a senha, desligar | dono |
| **Central de ajuda** | Os documentos do projeto (o `CLAUDE.md`, o `CONTRIBUTING.md`, todo o `docs/` e as decisões), em temas, com busca sem acento que leva direto à seção, e o índice de cada página ao lado. Para uso interno | todos veem |

### A central de ajuda

Não há texto de ajuda dentro do painel: a central mostra os próprios
`.md` do repositório, que o servidor lê do disco a cada visita
(`GET /admin/v1/help`, `servidor/Alcateia.Servidor/Rotas/AdminHelpRoutes.cs`).
Para mudar uma página, mude o documento. Ela vem por uma rota com
sessão, e não junto com os arquivos do painel, porque esses abrem sem
login e os documentos contam como o escudo funciona.

- **De onde lê:** na imagem do Docker, a pasta `ajuda/` ao lado do
  programa (o `servidor/Dockerfile` copia `CLAUDE.md`, `CONTRIBUTING.md`,
  `docs/` e `servidor/LEIA-ME.md` para lá — subiu o servidor, a ajuda
  acompanha). Rodando do repositório, a raiz dele. Outra pasta:
  `ALCATEIA_ADMIN_DOCS`.
- **Documento novo** em `docs/` ou `docs/decisoes/` aparece sozinho (as
  decisões no tema delas; o resto em "Outros documentos"). Para pô-lo num
  tema e dar a linha de descrição, acrescente-o em `known`, no
  `servidor/painel/src/lib/help.ts`.
- Link entre documentos (`[x](AJUSTES.md#secao)`) e caminho de documento
  em código (`` `docs/AJUSTES.md` ``) viram link dentro da central; as
  âncoras são as do GitHub, então os links que já valem lá valem aqui.
  Caminho de arquivo do código aparece como texto (o painel não abre
  código).

Papéis: **dono** (tudo), **administrador** (modera, edita o jogo, mexe em
moeda e item), **moderador** (bane, derruba, bloqueia IP) e **leitor**
(só vê). A permissão é conferida em cada ação, no servidor.

Regras que o painel não deixa quebrar: o último mapa ligado não desliga;
desconto vai de 1 a 90%; Sangue só compra personagem e serviço; bônus de
1 a 5 vezes; o painel nunca fica sem dono; banir e mexer em moeda pedem
motivo.

## O que o jogo enxerga, e quando

O painel grava no banco e o servidor renova na hora os dados em memória.
O jogo lê `GET /v1/gamedata` ao entrar na conta e a cada minuto no
saguão (`src/scripts/net/ServerData.cs`): mapa desligado some da escolha de
mapa, o aviso aparece no saguão. Banimento e manutenção valem no mesmo
instante (são do servidor). Preço, promoção e vitrine do dia valem na
hora: a loja do jogo mostra o que veio do servidor e é ele que cobra
(`POST /v1/shop/buy`). **Moeda, item ou nível mudado pelo painel chega
ao jogo na hora** (27/11): o servidor manda o aviso `conta` pela
ligação do saguão (`LobbyService.AccountChanged`), que fica aberta até
no meio da partida, e o jogo relê a conta (`AccountSync.Refresh`).
Durante a partida nada aparece na tela; a comemoração de nível novo é
do saguão e espera a volta. Um salto de mais de 10 níveis comemora só o
último nível marcante dele (`Profile.BigJump`). Jogador desligado vê
tudo ao entrar (decisão 0015).

## Desenvolver o painel

```
cd servidor/painel
npm install
npm run dev          # http://127.0.0.1:5173, falando com o servidor em 127.0.0.1:8443
npm run build        # gera dist/, que o servidor entrega
```

O texto da central de ajuda é Markdown desenhado com o `marked` (a
única biblioteca de texto do painel), com o estilo em `.help-prose`
(`src/index.css`).

Fora do Docker, aponte o servidor para o que foi compilado:
`ALCATEIA_ADMIN_UI=servidor/painel/dist`. As imagens das armas, dos
mapas e dos personagens são copiadas de `src/assets/` por
`src/scripts/imagens.mjs` (roda sozinho antes do `dev` e do `build`).

```
src/
  lib/          api (fetch + erros), sessão e permissões, tipos, formatação, tema
  components/
    ui/         os componentes de base, no estilo shadcn (Radix + Tailwind)
    charts/     gráficos em SVG (colunas, barras) — sem biblioteca
    Shell       a moldura: menu, o pulso do servidor, a conta
  pages/        uma tela por arquivo
  index.css     as cores e a tipografia (tema Noite e Dia)
```

A API do painel está em `servidor/Alcateia.Servidor/Rotas/Admin*.cs`; os
testes dela, em `servidor/Alcateia.Servidor.Testes/PanelTests.cs`.

## Item, mapa ou missão nova no jogo

O servidor conhece o que veio na semente. Entrou coisa nova em
`Catalog.cs`, `Maps.cs` ou `Missions.cs`:

```
python3 src/tools/servidor/exportar_dados.py     # atualiza servidor/Alcateia.Infra/Semente/jogo.json
```

Faça commit e suba o servidor: ele acrescenta o que falta e não toca no
que o painel já editou. O que é do jogo e o painel não edita acompanha a
semente a cada subida: a arte de cada item, a foto de cada mapa, os
modos para que cada mapa foi feito e quais modos estão liberados
(`RoomSettings.ModeAvailable`, em `src/scripts/core/RoomConfig.cs`).
