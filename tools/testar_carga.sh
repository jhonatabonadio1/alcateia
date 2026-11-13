#!/usr/bin/env bash
# Teste de carga do servidor: sobe o servidor (Release) contra um banco
# descartável e joga milhares de jogadores e mil salas nele, com centenas
# de servidores de partida falsos (servidor/Alcateia.Carga).
#
#   tools/testar_carga.sh                      3000 jogadores, 1000 salas, 250 × 4 lugares
#   tools/testar_carga.sh --usuarios=500 --salas=200 --frota=50
#
# De onde vem o banco: como em tools/testar_servidor.sh —
# ALCATEIA_TEST_DB_HOST/PORT/USER/PASSWORD/NAME, ou um PostgreSQL
# descartável no Docker. O banco é apagado e recriado: nunca aponte para
# o do jogo de verdade.
#
# O cadastro é a parte lenta (PBKDF2 de propósito: uns 0,1 s de CPU por
# conta). O resto mede o saguão e a frota com tudo acontecendo junto.

set -u
cd "$(dirname "$0")/.." || exit 1

CONTEINER=""
SERVIDOR=""
limpar() {
	[ -n "$SERVIDOR" ] && kill "$SERVIDOR" 2>/dev/null
	[ -n "$CONTEINER" ] && docker rm -f "$CONTEINER" >/dev/null 2>&1
}
trap limpar EXIT

if [ -z "${ALCATEIA_TEST_DB_HOST:-}" ]; then
	command -v docker >/dev/null || { echo "Sem Docker e sem ALCATEIA_TEST_DB_HOST: não tenho banco."; exit 1; }
	CONTEINER="alcateia-carga-$$"
	docker run -d --rm --name "$CONTEINER" -e POSTGRES_USER=alcateia -e POSTGRES_PASSWORD=segredo-de-teste \
		-e POSTGRES_DB=alcateia_teste -p 127.0.0.1:55433:5432 postgres:16-alpine -c max_connections=200 >/dev/null || exit 1
	for _ in $(seq 1 40); do
		docker exec "$CONTEINER" pg_isready -U alcateia -d alcateia_teste >/dev/null 2>&1 && break
		sleep 0.5
	done
	sleep 1
	export ALCATEIA_TEST_DB_HOST=127.0.0.1 ALCATEIA_TEST_DB_PORT=55433 ALCATEIA_TEST_DB_USER=alcateia \
		ALCATEIA_TEST_DB_PASSWORD=segredo-de-teste ALCATEIA_TEST_DB_NAME=alcateia_teste
fi

PORTA="${ALCATEIA_TEST_API_PORT:-18080}"
CHAVE="chave-de-carga-$$"

echo "== Limpando o banco de teste"
PGPASSWORD="$ALCATEIA_TEST_DB_PASSWORD" psql -q -h "$ALCATEIA_TEST_DB_HOST" -p "${ALCATEIA_TEST_DB_PORT:-5432}" \
	-U "$ALCATEIA_TEST_DB_USER" -d "$ALCATEIA_TEST_DB_NAME" -c "drop schema public cascade; create schema public;" >/dev/null 2>&1 \
	|| echo "(sem psql: o servidor migra por cima do que houver)"

echo "== Compilando (Release)"
dotnet build servidor/Alcateia.Servidor -c Release --nologo -v quiet || exit 1
dotnet build servidor/Alcateia.Carga -c Release --nologo -v quiet || exit 1

echo "== Subindo o servidor na porta $PORTA"
ALCATEIA_DB_HOST="$ALCATEIA_TEST_DB_HOST" ALCATEIA_DB_PORT="${ALCATEIA_TEST_DB_PORT:-5432}" \
	ALCATEIA_DB_USER="$ALCATEIA_TEST_DB_USER" ALCATEIA_DB_PASSWORD="$ALCATEIA_TEST_DB_PASSWORD" \
	ALCATEIA_DB_NAME="$ALCATEIA_TEST_DB_NAME" ALCATEIA_FLEET_KEY="$CHAVE" ALCATEIA_PORT="$PORTA" \
	ALCATEIA_LOGINS_PER_MINUTE=1000000 \
	dotnet servidor/Alcateia.Servidor/bin/Release/net8.0/Alcateia.Servidor.dll &
SERVIDOR=$!
for _ in $(seq 1 60); do
	curl -fsS -m 2 "http://127.0.0.1:$PORTA/health" >/dev/null 2>&1 && break
	sleep 1
done

echo "== Carga"
dotnet servidor/Alcateia.Carga/bin/Release/net8.0/Alcateia.Carga.dll --url="http://127.0.0.1:$PORTA" --chave="$CHAVE" "$@"
resultado=$?
echo "Memória do servidor no fim: $(ps -o rss= -p "$SERVIDOR" | awk '{printf "%.0f MB", $1/1024}')"
exit $resultado
