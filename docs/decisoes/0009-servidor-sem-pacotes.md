# 0009 — O servidor não usa pacote de fora

## Contexto

O servidor (`servidor/Alcateia.Servidor`) é ASP.NET sobre PostgreSQL. O
caminho comum seria Npgsql + Entity Framework (ou Dapper), JWT, um
pacote de WebSocket, um de teste.

## Decisão

Nenhum pacote NuGet. O que o servidor precisa além do que vem no .NET
está escrito nele:

- **O cliente do PostgreSQL** (`Banco/Pg.cs`): o protocolo de fio v3
  com autenticação SCRAM-SHA-256, consulta com parâmetros (protocolo
  estendido), transação e um pool. ~500 linhas.
- **Migrações** (`Banco/Schema.cs`): uma lista numerada de SQL e a
  tabela `schema_version`.
- **Senhas e sessões** (`Passwords.cs`): PBKDF2-SHA256 e token opaco
  guardado como hash — sem JWT.
- **Testes** (`Alcateia.Servidor.Testes`): um executável que sobe o
  servidor de verdade contra um PostgreSQL de verdade.

## Por quê

- O projeto compila e testa **sem rede** (`dotnet build --no-restore`),
  em qualquer máquina com o SDK — inclusive nas que só alcançam o
  GitHub.
- Nada para atualizar, nenhuma licença de terceiro no servidor.
- O banco tem cinco tabelas e uma dúzia de consultas: um ORM custaria
  mais do que economiza.

## O que isso custa

- O `Pg.cs` cobre o que o servidor usa: parâmetros e resultados em
  texto, sem `COPY`, sem `LISTEN/NOTIFY`, sem TLS com o banco (o banco
  fica na rede interna do Docker). Se o servidor crescer para precisar
  disso, **troque por Npgsql** — a superfície usada (`PgPool.Query`,
  `Exec`, `Transaction`) é pequena de propósito.
- Erro de protocolo é problema nosso. Os testes de banco
  (`DatabaseTests.cs`) existem para isso.
