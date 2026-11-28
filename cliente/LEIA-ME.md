# O cliente de instalação

O programa que o jogador instala no lugar do jogo (Jhon, 27/11: "um
cliente de instalação igual o battle.net"): entra na conta, baixa o jogo,
mantém atualizado, repara, desinstala, mostra os amigos e abre o jogo já
com a conta entrada. Roda em Windows e Mac.

Por que existe e por que é assim: `docs/decisoes/0021-cliente-de-instalacao.md`.

## O que faz

| | |
|---|---|
| **Conta** | Entrar e criar conta (as mesmas do jogo, no mesmo servidor), até 3 contas lembradas, entrar sozinho na última. Verificação em duas etapas opcional (QR code para o aplicativo autenticador). |
| **Jogo** | Um botão que é sempre a próxima coisa a fazer: instalar, atualizar, jogar, continuar. Barra com tamanho, velocidade e tempo que falta; pausar e continuar; verificar e reparar; mudar a pasta; desinstalar. |
| **Atualização** | Confere ao abrir e a cada 20 minutos. Baixa só o que mudou (uma versão nova costuma ser uma fração do jogo). Com "atualizar sozinho" ligado, começa sem esperar o clique. O próprio cliente também se atualiza, em segundo plano. |
| **Amigos** | A lista com onde cada um está (no jogo, na sala, em partida, no aplicativo, desconectado), adicionar pelo `Nome#0000`, aceitar e recusar pedidos, desfazer amizade. Convidar para sala e para grupo é de dentro do jogo. |
| **Jogar** | Abre o jogo já com a conta do cliente: o jogador não digita a senha de novo. |
| **Bandeja** | Fechar a janela deixa o cliente na bandeja (os amigos seguem vendo você, o download segue). Abrir junto com o Windows é opcional. |

## As peças

```
cliente/
├── Alcateia.Pacote/             o pacote: cortar, empacotar, instalar, atualizar, reparar, desinstalar
│                                (C# puro, sem pacote NuGet, sem interface)
├── Alcateia.Pacote.Ferramenta/  a linha de comando do pacote (o pipeline empacota com ela)
├── Alcateia.Cliente/            o cliente (Avalonia 12)
│   ├── Core/                    sem tela: AppInfo, Settings, Api, FriendsLink, GameService,
│   │                            SelfUpdate, Startup, QrCode
│   ├── ViewModels/              o que cada tela mostra (texto e botões, tirados do Core)
│   ├── Views/                   as telas (.axaml)
│   └── Assets/Fonts/            a Figtree em pesos fixos (gerada, ver abaixo)
├── Alcateia.Cliente.Fotos/      fotografa as telas sem abrir janela, com dados de mentira
└── ferramentas/gerar_figtree.py tira a Figtree de pesos fixos da variável do jogo
```

O jogo não compila esta pasta (`Compile Remove="cliente/**"` no
`WolfLeague.csproj`) e o Godot não a importa (`.gdignore`).

## Rodar na sua máquina

```sh
# o servidor local (docker compose up -d) e o cliente apontado para ele
ALCATEIA_API=http://127.0.0.1:8080 dotnet run --project cliente/Alcateia.Cliente

# as telas em PNG, sem janela e sem servidor (confira depois de mexer na interface)
dotnet run --project cliente/Alcateia.Cliente.Fotos -- /caminho/para/as/fotos
```

Variáveis que o cliente obedece:

| Variável | |
|---|---|
| `ALCATEIA_API` | O servidor de contas e amigos (passa por cima do que foi posto na montagem; o cliente a repassa ao jogo que abre). |
| `WOLFLEAGUE_DOWNLOADS` | De onde baixar, no lugar do que o servidor diz: um endereço ou **uma pasta do disco** com os manifestos e os pacotes. É como se testa um pacote antes de publicar. |
| `WOLFLEAGUE_LAUNCHER_DADOS` | Onde guardar os ajustes e as contas salvas (padrão: `%APPDATA%\WolfLeagueLauncher`, `~/Library/Application Support/WolfLeagueLauncher`). |

Testar o caminho inteiro sem publicar nada:

```sh
dotnet run --project cliente/Alcateia.Pacote.Ferramenta -- empacotar \
    --origem build/macos/app --saida /tmp/canal --versao 0.1.0 --plataforma mac \
    --exe alcateia.app/Contents/MacOS/alcateia
WOLFLEAGUE_DOWNLOADS=/tmp/canal ALCATEIA_API=http://127.0.0.1:8080 dotnet run --project cliente/Alcateia.Cliente
```

## O pacote: como o jogo vira download

O jogo exportado é quase todo um arquivo só (o `.pck`, mais de 1 GB).
Baixar ele inteiro a cada versão não serve. O pacote resolve assim:

1. **Corta cada arquivo em pedaços pelo conteúdo** (`Chunker`, "gear" do
   FastCDC: de 256 KB a 4 MB, uns 1,25 MB em média). O corte cai onde os
   bytes mandam, não numa posição fixa: se uma textura cresce no começo
   do `.pck`, o resto muda de lugar mas cai nos mesmos pedaços.
2. **Guarda cada pedaço diferente uma vez**, comprimido (Brotli), em
   arquivos de pacote (`wolfleague-<versão>-<plataforma>.000.pack`, de até
   1,5 GB cada).
3. **Escreve o manifesto** (`wolfleague-<plataforma>.manifest.json`): a
   lista de arquivos, os pedaços de cada um (SHA-256) e onde cada pedaço
   está nos pacotes.

Para instalar ou atualizar, o cliente lê o manifesto, procura cada pedaço
no que já está no disco e baixa só os que faltam, em trechos seguidos do
pacote (`Range`). O arquivo novo é montado ao lado (`.baixando`), com cada
pedaço conferido, e só troca de lugar com o antigo quando todos estão
prontos: interrompido, o jogo de antes continua inteiro e a próxima
tentativa segue de onde parou.

Medido com o jogo de verdade (a exportação de Mac, 379 arquivos, 1,4 GB):

| | Download |
|---|---|
| Instalar do zero | 711 MB (o `.zip` da mesma pasta tem 769 MB) |
| Atualizar para uma versão com 3 MB a mais no meio do `.pck`, uma DLL trocada, um arquivo novo e um a menos | **4 MB** |
| Adotar uma instalação antiga, feita sem o cliente, e levar à mesma versão nova | 4 MB |
| Reparar um byte estragado no `.pck` | 1 MB |

**Não mude os números do corte** (`Chunker.Min`, `Max`, a máscara, a
semente da tabela): toda instalação existente passaria a cortar diferente
do pacote novo e a próxima atualização de todo mundo baixaria tudo.

### A linha de comando

```sh
pacote() { dotnet run --project cliente/Alcateia.Pacote.Ferramenta -c Release -- "$@"; }

pacote empacotar --origem PASTA --saida PASTA --versao 0.5.0 --plataforma windows \
       [--produto wolfleague] [--exe WolfLeague.exe] [--base https://…/v0.5.0/] [--notas notas.txt]
pacote instalar --manifesto ARQUIVO_OU_URL --destino PASTA [--reparar] [--pacotes PASTA_OU_URL]
pacote desinstalar --destino PASTA
pacote info --manifesto ARQUIVO_OU_URL
```

`instalar` faz o que o cliente faz, sem a interface: serve para conferir
um pacote (o pipeline confere assim o que acabou de publicar).

### O que o desinstalar apaga

Só o que o manifesto instalado listou, as sobras de download e o estado
(`.pacote/`, dentro da pasta do jogo). Nunca a pasta inteira: se alguém
instalou numa pasta com outras coisas, elas ficam. O que o jogador salva
(conta, ajustes) não mora ali e não é tocado.

Consequência: numa instalação antiga adotada, um arquivo que só existia
na versão antiga (uma DLL que saiu do jogo) fica na pasta, porque o
cliente nunca o instalou. Não atrapalha o jogo.

## De onde o cliente baixa

Três ligações, todas saindo do cliente:

| | Para onde | O quê |
|---|---|---|
| Conta | o servidor (`ALCATEIA_API`), HTTPS | `/v1/sessions`, `/v1/accounts`, `/v1/profile`, `/v1/account/2fa…` |
| Amigos | o servidor, WebSocket `/v1/lobby?client=app` | lista, pedidos, presença (`servidor/LEIA-ME.md`) |
| Download | um lugar público só de arquivos | manifestos e pacotes |

O cliente pergunta ao servidor onde estão os downloads (`GET /v1/client`,
campo `downloads`, que é a variável `ALCATEIA_DOWNLOADS` do servidor).
Trocar de hospedagem é trocar essa variável; não é preciso lançar cliente
novo. Os arquivos **não** passam pelo servidor (ele pagaria a banda de
cada download) nem pelas Releases do GitHub (o repositório é privado: só
baixa quem tem login, e o cliente não pode levar um token dentro).

Hoje é um balde do **Cloudflare R2**. O que há nele:

```
wolfleague-windows.manifest.json      sempre o da última versão (sem cache)
wolfleague-mac.manifest.json
launcher-windows.manifest.json        o próprio cliente (ele se atualiza por aqui)
launcher-mac-arm64.manifest.json
launcher-mac-x64.manifest.json
WolfLeague-Launcher-Setup.exe         o instalador do cliente, sempre o último: o link do site
WolfLeague-Launcher-mac-arm64.dmg
WolfLeague-Launcher-mac-x64.dmg
latest.json                           versão, tamanho e SHA-256 dos três, para o site
v0.5.0/                               os pacotes e os instaladores de cada versão (nunca mudam)
    wolfleague-0.5.0-windows.000.pack
    launcher-0.5.0-windows.000.pack
    WolfLeague-Launcher-Setup-0.5.0.exe
    …
```

O manifesto diz de onde vêm os pacotes dele (`base`, com a versão no
caminho): quem começou a baixar uma versão termina nela, mesmo que saia
outra no meio.

### Os links do site

```
<endereço público do balde>/WolfLeague-Launcher-Setup.exe        Windows
<endereço público do balde>/WolfLeague-Launcher-mac-arm64.dmg    Mac com Apple Silicon
<endereço público do balde>/WolfLeague-Launcher-mac-x64.dmg      Mac com Intel
<endereço público do balde>/latest.json                          versão, tamanho, SHA-256
```

Não mudam de uma versão para outra. Para uma página ler o `latest.json`
pelo navegador (mostrar a versão no botão), o balde precisa de uma regra
de CORS liberando o domínio do site (painel do R2 → o balde → Settings →
CORS Policy); o link de download em si não precisa.

### Configuração, uma vez

No GitHub (Settings → Secrets and variables → Actions):

| | Nome | |
|---|---|---|
| segredo | `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY` | Token de API do R2, com leitura e escrita só nesse balde. |
| variável (ou segredo) | `R2_BUCKET_NAME` | O balde. |
| variável (ou segredo) | `R2_PUBLIC_URL` | O endereço público (`https://pub-….r2.dev` ou um domínio). Não é segredo: como variável, os links aparecem inteiros no resumo do pipeline. |
| variável | `R2_ENDPOINT` | Opcional: a API S3 da conta. O padrão, no workflow, é a da conta do projeto. |

No R2: o balde com **acesso público** ligado (Public Development URL, ou
um domínio próprio — o que a Cloudflare recomenda para produção).

O servidor recebe o endereço sozinho: o pipeline grava
`ALCATEIA_DOWNLOADS` no Fly a partir do `R2_PUBLIC_URL`, no deploy.

## O lançamento

`.github/workflows/lancamento.yml`, depois da Release de sempre:

1. **`lancador`** (Linux): empacota o jogo de Windows (a pasta do `.zip`
   que o job `cliente` montou), publica o cliente, empacota o cliente e
   monta o instalador dele (`instalador/cliente.nsi`).
2. **`lancador-mac`** (Mac): o mesmo, com o jogo de dentro do `.dmg` e o
   cliente um por processador (`src/tools/lancamento/cliente_mac.sh`: o
   `.app`, a assinatura ad hoc e o `.dmg`).
3. **`distribuir`**: sobe para o R2 — primeiro os pacotes, depois os
   instaladores, e só no fim os manifestos (é o manifesto que avisa "há
   versão nova", e ele não pode apontar para pacote que não chegou) — e
   confere pelo endereço público, como um jogador: os manifestos
   respondem, o pacote do cliente instala em partes, os links do site
   respondem 200 e o instalador de nome fixo é o desta versão.

Esses três jobs vêm depois da Release e não a seguram. Sem os segredos do
R2, são pulados.

As novidades que o cliente mostra (aba Novidades) saem do CHANGELOG:
`src/tools/lancamento/notas.py` fica com o título de cada item da seção
do topo.

## O cliente e o jogo

- **O login passa pelo ambiente.** O cliente abre o jogo com
  `ALCATEIA_SESSAO` (o login) e `ALCATEIA_CONTA` (o nome) nas variáveis de
  ambiente do processo do jogo, que só ele enxerga (na linha de comando
  qualquer programa do computador leria). O jogo entra com ela uma vez
  (`Backend.TakeLauncherAccess`, na `LoginScreen`). Aberto direto, sem o
  cliente, o jogo abre na tela de login de sempre, com as contas salvas.
- **O jogo instalado pelo instalador antigo** (`windows.nsi`) é
  aproveitado: a pasta padrão do cliente no Windows é a mesma
  (`%LOCALAPPDATA%\Programs\WolfLeague`, ou a que o jogador tinha
  escolhido, lida do registro), e o que já está lá entra na conta como
  pedaços que não precisam vir da rede.
- **O iniciador de vídeo continua** (`instalador/iniciador.nsi`): o
  `WolfLeague.exe` que o cliente abre é ele, que acha o modo de vídeo que
  funciona; o jogo de verdade fica em `game\`.

## A interface

As cores são as do menu do jogo (`UiTokens`, paleta Menu) e as letras
também (`UiFonts`): **Figtree** para ler e nos rótulos em caixa alta,
**Kanit itálica** nos títulos e nos botões. Mudou a letra do jogo, muda
no `App.axaml`.

O Avalonia não aplica o peso de uma fonte variável (a Figtree do jogo é
um arquivo só, variável; saía tudo fino). Por isso os quatro pesos que os
menus usam ficam em arquivos próprios, tirados da variável do jogo:

```sh
pip install fonttools && python3 cliente/ferramentas/gerar_figtree.py
```

Depois de mexer numa tela, rode as fotos (`Alcateia.Cliente.Fotos`) e
olhe os PNG: elas montam cada tela com conta, amigos e download de
mentira, sem servidor.

O QR code da verificação em duas etapas é desenhado pelo próprio cliente
(`Core/QrCode.cs`: bytes, correção M, versões 1 a 10), conferido com um
leitor de verdade.

## O que foi conferido e o que não foi

Conferido nesta máquina (Mac), sem janela:

- O pacote, com o jogo de verdade: instalar, atualizar, adotar, reparar,
  desinstalar, baixar por HTTP em partes, interromper e continuar, queda
  do servidor de arquivos. O resultado comparado byte a byte com a pasta
  de origem.
- O núcleo do cliente contra o servidor local: criar conta, entrar, a
  tag, pedir e aceitar amizade pelas duas pontas, a presença "no
  aplicativo", instalar, pausar, continuar, jogar (um jogo de mentira que
  grava o login recebido), desinstalar.
- O executável publicado (arquivo único, 40 MB) sobe inteiro, na bandeja.
- A atualização do próprio cliente no Mac: a versão 1 rodando acha a 2,
  troca o executável e o `.app` continua com a assinatura válida.
- As telas, pelas fotos.

**Não conferido** (não há Windows nem R2 aqui):

- O cliente rodando no Windows: a janela, a bandeja, abrir com o sistema,
  a atualização do próprio cliente com o executável aberto.
- O instalador `instalador/cliente.nsi` (não foi compilado: não há
  `makensis` nesta máquina).
- Os três jobs novos do pipeline e o envio ao R2 (o YAML foi validado,
  não executado).
- A velocidade de download numa rede de verdade.

Veja `docs/PENDENCIAS.md` para o que falta.
