#!/usr/bin/env bash
# Roda os testes do servidor (contas, salas, chat, partidas) contra um
# PostgreSQL de verdade.
#
#   tools/testar_servidor.sh           tudo
#   tools/testar_servidor.sh Sala      só o que tem "Sala" no nome
#
# De onde vem o banco:
#   - se ALCATEIA_TEST_DB_HOST estiver definida, usa esse banco
#     (com ALCATEIA_TEST_DB_PORT/USER/PASSWORD/NAME);
#   - senão, sobe um PostgreSQL descartável no Docker e o derruba no fim.
#
# Os testes apagam e recriam as tabelas: nunca aponte para o banco do
# jogo de verdade.

set -u
cd "$(dirname "$0")/.." || exit 1

CONTEINER=""
limpar() {
	[ -n "$CONTEINER" ] && docker rm -f "$CONTEINER" >/dev/null 2>&1
}
trap limpar EXIT

if [ -z "${ALCATEIA_TEST_DB_HOST:-}" ]; then
	if ! command -v docker >/dev/null; then
		echo "Sem Docker e sem ALCATEIA_TEST_DB_HOST: não tenho banco para testar."
		exit 1
	fi

	echo "== Subindo um PostgreSQL descartável"
	CONTEINER="alcateia-teste-$$"
	docker run -d --rm --name "$CONTEINER" \
		-e POSTGRES_USER=alcateia -e POSTGRES_PASSWORD=segredo-de-teste -e POSTGRES_DB=alcateia_teste \
		-p 127.0.0.1:55432:5432 postgres:16-alpine >/dev/null || exit 1

	for _ in $(seq 1 40); do
		docker exec "$CONTEINER" pg_isready -U alcateia -d alcateia_teste >/dev/null 2>&1 && break
		sleep 0.5
	done
	sleep 1

	export ALCATEIA_TEST_DB_HOST=127.0.0.1 ALCATEIA_TEST_DB_PORT=55432 \
		ALCATEIA_TEST_DB_USER=alcateia ALCATEIA_TEST_DB_PASSWORD=segredo-de-teste ALCATEIA_TEST_DB_NAME=alcateia_teste
fi

echo "== Testes do servidor"
dotnet run --project servidor/Alcateia.Servidor.Testes/Alcateia.Servidor.Testes.csproj -- "$@"
