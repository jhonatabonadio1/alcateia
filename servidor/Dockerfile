# O servidor do Alcateia (contas, salas, chat, ranking, frota).
#
# O contexto é a raiz do repositório, porque as regras de recompensa
# (scripts/domain/Rewards.cs) são as do jogo:
#
#   docker build -f servidor/Dockerfile -t alcateia-servidor .
#
# Normalmente quem constrói é o docker compose (na raiz).

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS construir
WORKDIR /fonte
COPY scripts/domain/Rewards.cs scripts/domain/Rewards.cs
COPY servidor/Alcateia.Servidor servidor/Alcateia.Servidor
RUN dotnet publish servidor/Alcateia.Servidor/Alcateia.Servidor.csproj -c Release -o /saida

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=construir /saida .

# Sem privilégio de root dentro do contêiner.
USER app
ENV ALCATEIA_PORT=8080
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=4s --start-period=20s --retries=3 \
	CMD ["dotnet", "Alcateia.Servidor.dll", "--saude"]

ENTRYPOINT ["dotnet", "Alcateia.Servidor.dll"]
