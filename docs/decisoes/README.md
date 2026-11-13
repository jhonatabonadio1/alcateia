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
