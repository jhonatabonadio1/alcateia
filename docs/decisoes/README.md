# Decisões de arquitetura

Um arquivo por decisão que muda como o código é organizado (não as de
jogo — essas estão no `docs/DESIGN.md`). Cada um diz o contexto, o que
foi decidido e o que isso custa, para que quem chegar depois saiba
**por que** antes de mudar.

Decidiu algo novo, ou mudou de ideia sobre um destes? Escreva um
arquivo novo com o próximo número; não reescreva o antigo — marque-o
como "substituído por NNNN".

| Nº | Decisão |
|---|---|
| [0001](0001-dominio-puro.md) | Regras de jogo em C# puro, com teste de unidade |
| [0002](0002-classes-parciais.md) | Classes grandes divididas em arquivos parciais por assunto |
| [0003](0003-corpo-compartilhado.md) | Jogador e bot dividem o corpo |
| [0004](0004-intencao-do-jogador.md) | O jogador lê a intenção uma vez por quadro |
| [0005](0005-dados-em-tres.md) | Números de ajuste são dado (.tres), não código |
| [0006](0006-sorteio-com-semente.md) | Sorteio de regra com semente, um fluxo por dono |
| [0007](0007-testes.md) | Duas camadas de teste, e o teste muda junto com a regra |
| [0008](0008-rede-quem-decide.md) | Online: cada jogo decide o próprio corpo, o servidor decide a partida |
| [0009](0009-servidor-sem-pacotes.md) | O servidor não usa pacote de fora (cliente PostgreSQL próprio) |
| [0010](0010-servidor-em-camadas.md) | O servidor em camadas: domínio, aplicação, infra e a API |
| [0011](0011-servidor-autoritativo.md) | O servidor move os corpos: comandos, predição e correção, interpolação (primeira leva) |
| [0012](0012-painel-em-outra-porta.md) | O painel administrativo: outra porta, contas próprias, React à parte |
| [0013](0013-dados-do-jogo-no-servidor.md) | Economia e conteúdo moram no servidor; o painel edita, o jogo lê |
| [0014](0014-escudo-em-memoria.md) | O escudo de segurança decide em memória e registra em lote |
| [0015](0015-economia-da-conta.md) | Tudo o que vale alguma coisa é da conta; o jogo só espelha |
| [0016](0016-vida-e-acerto-no-servidor.md) | Vida e acerto no servidor de partida: o acerto é conferido, a morte é decidida lá (segunda leva da 0011) |
| [0017](0017-o-servidor-repete-o-especial-e-os-bots.md) | O servidor repete o especial do jogo (o ponto e o alvo vão no comando, a carga segue a do jogo) e põe os bots no mundo dele |
| [0018](0018-o-desenho-online-anda-liso.md) | O desenho online anda liso: relógio no passo, o desenho dos outros só muda de ritmo (nunca pula nem volta), correção em mola, fila do servidor apressa devagar, repasse com o instante de quem mandou |
| [0019](0019-o-que-o-servidor-nao-copia-mais.md) | O que o servidor não copia mais (a forma, a pressa dos comandos, o tiro de bot) e o acerto avisado a todos na hora, com a barra prevista em quem atira |
| [0020](0020-teste-novo-se-propoe-antes.md) | Teste novo se propõe antes e o Jhon aceita; não se cria teste para cada mudança (muda a criação de teste da 0001 e da 0007) |
