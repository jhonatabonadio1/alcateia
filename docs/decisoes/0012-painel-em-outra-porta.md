# 0012 — O painel administrativo: outra porta, contas próprias, React à parte

## Contexto

O servidor precisa de um painel: banir, dar moeda, ligar e desligar
mapa, criar promoção e desafio, ver ataques. É a parte mais perigosa do
sistema — quem entra nele dá dinheiro e bane quem quiser. No CT a lição
foi clara: tela administrativa atrás só de login, na mesma porta do
resto, é o primeiro lugar que um invasor procura.

## Decisão

- **Outra porta.** As rotas `/admin/v1/...` e os arquivos do painel só
  existem na porta do painel (`ALCATEIA_ADMIN_PORT`, 8081). Na porta do
  jogo, `/admin` responde 404 e conta como varredura. A porta do painel
  escuta só em `127.0.0.1` (fora do Docker) e o `docker-compose` só a
  publica em `127.0.0.1`: de fora, por túnel SSH ou VPN. A conferência é
  pela porta em que a conexão chegou (`LocalPort`), não pelo cabeçalho
  `Host`, que o cliente escreve.
- **Contas próprias.** Administrador não é jogador com uma marca: é outra
  tabela, com senha de 12 ou mais, trava depois de cinco erros,
  verificação em duas etapas (TOTP) e sessão de doze horas num cookie
  `HttpOnly` e `SameSite=Strict`. Pedido que muda algo precisa do
  cabeçalho `X-Painel`.
- **Papéis por ação.** Dono, administrador, moderador e leitor; cada
  caso de uso confere a permissão (`AdminContext.Require`), não só a
  rota. Toda ação vai para a auditoria com quem, o quê, em quem e de onde.
- **React à parte.** O painel é um projeto de frontend (`servidor/painel`:
  React, Tailwind, Radix, componentes no estilo shadcn) que compila para
  arquivos estáticos; o servidor só os entrega. A regra "sem pacote de
  fora" (0009) continua valendo para o servidor: nenhum pacote do painel
  roda dentro dele, e a política de conteúdo (CSP) só deixa o navegador
  carregar o que vem do próprio servidor.

## O que custa

- Para usar o painel de fora da máquina é preciso túnel ou VPN — é de
  propósito.
- O painel tem dependências de npm, com as atualizações que isso pede
  (`npm audit`). Ficam só no navegador de quem administra.
- Esquecer a senha do único dono pede acesso à máquina:
  `Alcateia.Servidor --admin <nome>`.
