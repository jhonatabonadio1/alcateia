# O servidor do Alcateia (contas, salas, chat, ranking, frota, loja,
# desafios, segurança) e o painel administrativo.
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
# As camadas: domínio, aplicação, infra e a API (que é o programa).
COPY servidor/Alcateia.Dominio servidor/Alcateia.Dominio
COPY servidor/Alcateia.Aplicacao servidor/Alcateia.Aplicacao
COPY servidor/Alcateia.Infra servidor/Alcateia.Infra
COPY servidor/Alcateia.Servidor servidor/Alcateia.Servidor
RUN dotnet publish servidor/Alcateia.Servidor/Alcateia.Servidor.csproj -c Release -o /saida

# O painel administrativo (React): compila para arquivos estáticos que o
# próprio servidor entrega na porta do painel. As imagens das armas, dos
# mapas e dos personagens vêm do jogo (servidor/painel/scripts/imagens.mjs).
FROM node:22-alpine AS painel
WORKDIR /fonte/servidor/painel
COPY servidor/painel/package.json servidor/painel/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY assets/ui/armas /fonte/assets/ui/armas
COPY assets/ui/mapas /fonte/assets/ui/mapas
COPY assets/personagens /fonte/assets/personagens
COPY servidor/painel ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=construir /saida .
COPY --from=painel /fonte/servidor/painel/dist ./painel

# Sem privilégio de root dentro do contêiner.
USER app
ENV ALCATEIA_PORT=8080
# O painel escuta em todas as interfaces DO CONTÊINER; quem decide quem
# chega nele é o docker-compose, que só publica a porta em 127.0.0.1.
ENV ALCATEIA_ADMIN_PORT=8081
ENV ALCATEIA_ADMIN_BIND=0.0.0.0
EXPOSE 8080 8081

HEALTHCHECK --interval=10s --timeout=4s --start-period=20s --retries=3 \
	CMD ["dotnet", "Alcateia.Servidor.dll", "--saude"]

ENTRYPOINT ["dotnet", "Alcateia.Servidor.dll"]
