# 0021 — O cliente de instalação: um programa à parte, em Avalonia, com pacote por pedaços

**Situação:** aceita (27/11/2026, pedido do Jhon: "crie um cliente de
instalação igual o battle.net para baixar assets do game, atualizar
automático, fazer login, adicionar novos amigos [...] desinstalar,
instalar"). Desfaz a decisão de 13/11 de não ter programa separado (o
login continua na primeira tela do jogo, para quem o abre direto).

## Contexto

Até aqui o jogador baixava um instalador com o jogo inteiro (uns 700 MB)
a cada versão e rodava por cima. Três problemas cresciam juntos:

- **Atualizar é reinstalar.** Cada versão do alpha baixa tudo de novo,
  quando quase tudo o que há num `.pck` de mais de 1 GB não mudou.
- **Não há onde avisar que saiu versão nova**: o jogador descobre quando
  o servidor recusa, ou pelo Discord.
- **O link de download são as Releases do GitHub, de um repositório
  privado**: só baixa quem tem login nele.

## Decisão

Um **cliente** separado do jogo (`cliente/`), que o jogador instala no
lugar dele e que: entra na conta, baixa o jogo, mantém atualizado, repara,
desinstala, mostra os amigos e abre o jogo já com a conta. As partes:

### 1. O pacote é próprio, por pedaços cortados pelo conteúdo

`cliente/Alcateia.Pacote`, C# puro e sem pacote de fora, como o servidor
(0009). Cada arquivo do jogo é cortado em pedaços **pelo conteúdo** (soma
rolante "gear", a do FastCDC; pedaços de 256 KB a 4 MB), cada pedaço
diferente é guardado uma vez, comprimido (Brotli), e um manifesto diz que
pedaços formam cada arquivo. Para atualizar, o cliente acha no disco os
pedaços que já tem e baixa só os outros, por trechos (`Range`) de um
arquivo estático.

Por que cortar pelo conteúdo e não em blocos fixos: o `.pck` é um arquivo
só; uma textura que cresce no começo empurra tudo, e com blocos fixos
nenhum bloco depois dela bate com o da versão anterior. Medido com o jogo
exportado (1,4 GB): 3 MB a mais no meio do `.pck` viram 4 MB de download.

Por que não um formato pronto (zsync, casync, o patch do Steam): ou exigem
servidor próprio, ou ferramenta nativa em cada sistema, ou não rodam em
C#. O que se precisa cabe em umas 900 linhas e o mesmo código empacota (no
pipeline) e instala (no cliente) — é o que garante que os dois cortam
igual.

O instalador nunca escreve por cima do que está funcionando: o arquivo
novo é montado ao lado, com cada pedaço conferido pelo SHA-256, e só
troca de lugar no fim. Interrompido, continua de onde parou. Desinstalar
apaga só o que o manifesto listou.

### 2. A interface é Avalonia

O mesmo C# do jogo e do servidor, em Windows e Mac, num executável único
de uns 40 MB que não pede nada instalado. É o único pacote de fora do
cliente (a 0009 é sobre o servidor: lá não há interface para justificar
um).

Alternativas pesadas e descartadas: **Electron** (mais de 100 MB e outra
linguagem para manter); **Tauri** (leve, mas põe Rust no pipeline e
depende do WebView do sistema); **um segundo projeto Godot** (70 MB de
motor para mostrar uma barra, e atualizar a si mesmo é pior); **NSIS**
(serve para instalar, não para uma janela que fica aberta).

### 3. Os arquivos ficam num lugar público só de arquivos

Hoje, um balde do Cloudflare R2 (não cobra o tráfego de saída, que é o
que pesa). Não passam pelo servidor do jogo (ele pagaria a banda de cada
download) nem pelo GitHub (repositório privado; o cliente não pode levar
um token dentro, qualquer um o extrairia).

O cliente **pergunta ao servidor** onde estão (`GET /v1/client`): mudar
de hospedagem é mudar uma variável do servidor, sem lançar cliente novo.

### 4. O cliente tem a própria ligação de amigos

`/v1/lobby?client=app`: convive com a do jogo (abrir o jogo não a
derruba), só vê e mexe nos amigos, e faz a conta aparecer "no aplicativo"
enquanto o jogo está fechado. Sala, grupo e chat continuam sendo de
dentro do jogo.

### 5. O jogo recebe a conta pelo ambiente

O cliente abre o jogo com o login em variáveis de ambiente do processo
dele (`ALCATEIA_SESSAO`, `ALCATEIA_CONTA`), não na linha de comando (que
qualquer programa do computador lê). O jogo entra com ela uma vez. Aberto
direto, sem o cliente, o jogo continua na tela de login com as contas
salvas (decisão do Jhon, 27/11: "mantenha o cache de contas").

### 6. O cliente se atualiza pelo mesmo pacote

O manifesto dele mora ao lado dos do jogo. No Windows o executável aberto
não se sobrescreve, mas troca de nome: o antigo sai da frente e é apagado
depois. O estado dessa atualização fica na pasta de dados do usuário, não
ao lado do programa: no Mac, um arquivo a mais dentro do `.app` quebra o
lacre da assinatura.

## O que isso custa

- **Um programa a mais para manter**, com a própria interface. As cores e
  as letras são as do menu do jogo (`UiTokens`, `UiFonts`), copiadas no
  `App.axaml`: mudou no jogo, tem de mudar lá.
- **Os números do corte são formato.** Mudar o tamanho dos pedaços ou a
  tabela da soma faz a próxima atualização de todo mundo baixar tudo.
- **Sem assinatura de código**, o Windows avisa ("O Windows protegeu o
  computador") e o Mac bloqueia na primeira vez, como já acontecia com o
  instalador do jogo. Agora é uma vez só (o cliente), e não a cada versão.
- **O manifesto não é assinado.** Quem confia é o HTTPS do lugar de onde
  ele vem. Quem tomar o balde troca o jogo de todo mundo; assinar o
  manifesto com uma chave do pipeline fecha isso (em `docs/PENDENCIAS.md`).
- **O servidor ainda não recusa jogo velho.** O cliente atualiza, mas
  quem abre o jogo direto entra com a versão que tiver.
- **Só em português.** O jogo tem inglês e espanhol; o cliente, não.

## Onde está

`cliente/LEIA-ME.md` (como rodar, o formato, o balde, o que foi
conferido); `.github/workflows/lancamento.yml` (os jobs `lancador`,
`lancador-mac` e `distribuir`); `instalador/cliente.nsi` (o instalador do
cliente para Windows); `servidor/Alcateia.Servidor/Rotas/ClientRoutes.cs`.
