#!/usr/bin/env bash
# Roda todos os testes do Alcateia.
#
#   tools/testar.sh              tudo
#   tools/testar.sh Salto        só o que tem "Salto" no nome
#   tools/testar.sh --rapido     sem os testes de partida (os mais lentos)
#
# O Godot vem de $GODOT, ou do "godot" no PATH. No Mac:
#   GODOT=/Applications/Godot_mono.app/Contents/MacOS/Godot tools/testar.sh
#
# Três etapas: compila o jogo, roda os testes de unidade (C# puro, em
# segundos) e os de regressão (o jogo de verdade, sem tela, um processo
# por teste para nada vazar de um para o outro). Um teste de regressão
# também reprova se o Output tiver erro, mesmo que as medidas passem.
# Sai com 1 se qualquer coisa falhar.

set -u
cd "$(dirname "$0")/.." || exit 1

GODOT="${GODOT:-godot}"
FILTRO=""
RAPIDO=0
for arg in "$@"; do
	case "$arg" in
		--rapido) RAPIDO=1 ;;
		*) FILTRO="$arg" ;;
	esac
done

# Os testes de partida simulam minutos de jogo; o --rapido os pula.
LENTOS="BotsAtiram LobosUsamEspeciais"

falhas=0
inicio=$(date +%s)

echo "== Compilando"
if ! dotnet build alcateia.csproj --nologo -v quiet -clp:ErrorsOnly; then
	echo "A compilação falhou."
	exit 1
fi

echo
echo "== Testes de unidade"
if ! dotnet run --project tests/unit/Alcateia.UnitTests.csproj -- ${FILTRO}; then
	falhas=$((falhas + 1))
fi

echo
echo "== Código repetido"
if command -v python3 >/dev/null; then
	python3 tools/duplicados.py || falhas=$((falhas + 1))
else
	echo "(sem python3: pulei)"
fi

echo
echo "== Testes de regressão"
lista=$("$GODOT" --headless --path . res://tests/regression/bench.tscn -- --lista 2>/dev/null \
	| awk -F'\t' 'NF == 2 { print $1 }')

if [ -z "$lista" ]; then
	echo "Não consegui listar os testes de regressão (o Godot rodou? GODOT=$GODOT)."
	exit 1
fi

for teste in $lista; do
	if [ -n "$FILTRO" ] && [[ "$teste" != *"$FILTRO"* ]]; then continue; fi
	if [ "$RAPIDO" = 1 ] && [[ " $LENTOS " == *" $teste "* ]]; then continue; fi

	saida=$("$GODOT" --headless --fixed-fps 60 --path . res://tests/regression/bench.tscn \
		-- --teste="$teste" --contagem=1 2>&1)
	codigo=$?

	# O relatório do teste: a linha TESTE e as anotações abaixo dela.
	echo "$saida" | awk '/^  · /{print} /^TESTE /{p=1} p && (/^TESTE / || /^              /)'

	erros=$(echo "$saida" \
		| grep -E '^(ERROR|SCRIPT ERROR|USER ERROR)|Unhandled exception|NullReferenceException' \
		| grep -v -E 'resources still in use at exit|ObjectDB instances leaked|RID allocations' \
		| sort | uniq -c | head -5)

	if [ "$codigo" -ne 0 ] || [ -n "$erros" ]; then
		falhas=$((falhas + 1))
		if [ "$codigo" -eq 0 ]; then echo "TESTE FALHOU  $teste: erro no Output"; fi
		[ -n "$erros" ] && echo "$erros" | sed 's/^/              /'
	fi
done

echo
duracao=$(( $(date +%s) - inicio ))
if [ "$falhas" -eq 0 ]; then
	echo "Tudo passou (${duracao} s)."
	exit 0
fi

echo "$falhas etapa(s) ou teste(s) falharam (${duracao} s)."
exit 1
