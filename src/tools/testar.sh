#!/usr/bin/env bash
# Roda todos os testes do Alcateia.
#
#   src/tools/testar.sh              tudo
#   src/tools/testar.sh Salto        só o que tem "Salto" no nome
#   src/tools/testar.sh --rapido     sem os testes de partida (os mais lentos)
#   JUNTOS=8 src/tools/testar.sh     quantos de regressão ao mesmo tempo (padrão 6)
#   TEMPOS=tempos.txt src/tools/testar.sh   grava quanto cada um levou
#
# O Godot vem de $GODOT, ou do "godot" no PATH. No Mac:
#   GODOT=/Applications/Godot_mono.app/Contents/MacOS/Godot src/tools/testar.sh
#
# Três etapas: compila o jogo, roda os testes de unidade (C# puro, em
# segundos) e os de regressão (o jogo de verdade, sem tela, um processo
# por teste para nada vazar de um para o outro). Um teste de regressão
# também reprova se o Output tiver erro, mesmo que as medidas passem.
# Sai com 1 se qualquer coisa falhar.

set -u
cd "$(dirname "$0")/../.." || exit 1

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
LENTOS="BotsAtiram BotsCacam BotsJogamGranada LobosUsamEspeciais MapaArmazemEstendido VoltarASalaSemCrash ParedeNaCidade ParedeNoArmazem ParedeNoNeon PostesNaCidade"

# Quantos testes de regressão rodam ao mesmo tempo (JUNTOS=1: um por vez, como antes).
JUNTOS="${JUNTOS:-6}"
# Rodam um por vez, depois dos outros: o que mede tempo de relógio (o passo do
# servidor) e os que mexem nos arquivos do jogador (user://: a conta salva, o
# perfil) — dois ao mesmo tempo escreviam por cima um do outro (o Login reprovou
# assim uma vez em três, 27/11).
SOZINHOS="PassoDoServidor ContaNoPerfil Login TelasDoLobby PerfilPersiste"

falhas=0
inicio=$(date +%s)

echo "== Compilando"
if ! dotnet build WolfLeague.csproj --nologo -v quiet -clp:ErrorsOnly; then
	echo "A compilação falhou."
	exit 1
fi

echo
echo "== Testes de unidade"
if ! dotnet run --project src/tests/unit/Alcateia.UnitTests.csproj -- ${FILTRO}; then
	falhas=$((falhas + 1))
fi

echo
echo "== Código repetido"
if command -v python3 >/dev/null; then
	python3 src/tools/duplicados.py || falhas=$((falhas + 1))
else
	echo "(sem python3: pulei)"
fi

echo
echo "== Testes de regressão"
lista=$("$GODOT" --headless --path . res://src/tests/regression/bench.tscn -- --lista 2>/dev/null \
	| awk -F'\t' 'NF == 2 { print $1 }')

if [ -z "$lista" ]; then
	echo "Não consegui listar os testes de regressão (o Godot rodou? GODOT=$GODOT)."
	exit 1
fi

# Um teste: roda, escreve o relatório em <pasta>/<teste>.txt e o tempo em
# <pasta>/<teste>.s; sai com 1 se reprovou (o código do Godot ou erro no Output).
um_teste() {
	local teste="$1" pasta="$2" t0 saida codigo erros ruim=0
	t0=$(date +%s)
	saida=$("$GODOT" --headless --fixed-fps 60 --path . res://src/tests/regression/bench.tscn \
		-- --teste="$teste" --contagem=1 2>&1)
	codigo=$?
	{
		# O relatório do teste: a linha TESTE e as anotações abaixo dela.
		echo "$saida" | awk '/^  · /{print} /^TESTE /{p=1} p && (/^TESTE / || /^              /)'
		erros=$(echo "$saida" \
			| grep -E '^(ERROR|SCRIPT ERROR|USER ERROR)|Unhandled exception|NullReferenceException' \
			| grep -v -E 'resources still in use at exit|ObjectDB instances leaked|RID allocations|Pages in use exist at exit' \
			| sort | uniq -c | head -5)
		if [ "$codigo" -ne 0 ] || [ -n "$erros" ]; then
			ruim=1
			if ! echo "$saida" | grep -q '^TESTE FALHOU'; then
				if [ "$codigo" -eq 0 ]; then echo "TESTE FALHOU  $teste: erro no Output"
				else echo "TESTE FALHOU  $teste: o jogo saiu com o código $codigo sem dar o resultado"; fi
			fi
			[ -n "$erros" ] && echo "$erros" | sed 's/^/              /'
		fi
	} > "$pasta/$teste.txt"
	echo "$(( $(date +%s) - t0 ))" > "$pasta/$teste.s"
	[ "$ruim" = 1 ] && touch "$pasta/$teste.falhou"
	return 0
}

escolhidos=""
for teste in $lista; do
	if [ -n "$FILTRO" ] && [[ "$teste" != *"$FILTRO"* ]]; then continue; fi
	if [ "$RAPIDO" = 1 ] && [[ " $LENTOS " == *" $teste "* ]]; then continue; fi
	escolhidos="$escolhidos $teste"
done

# Vários de uma vez (27/11: a bateria inteira levava 18 minutos, um por vez):
# cada teste é um processo próprio, com o passo fixo em 60 por segundo, então
# o resultado não depende de quantos rodam juntos. Os de SOZINHOS rodam depois,
# um por vez.
PASTA_T=$(mktemp -d)
trap 'rm -rf "$PASTA_T"' EXIT
for teste in $escolhidos; do
	if [[ " $SOZINHOS " == *" $teste "* ]]; then continue; fi
	# (o bash do Mac não tem "wait -n": espera abrir uma vaga)
	while [ "$(jobs -rp | wc -l)" -ge "$JUNTOS" ]; do sleep 0.2; done
	um_teste "$teste" "$PASTA_T" &
done
wait
for teste in $escolhidos; do
	if [[ " $SOZINHOS " == *" $teste "* ]]; then um_teste "$teste" "$PASTA_T"; fi
done

for teste in $escolhidos; do
	cat "$PASTA_T/$teste.txt" 2>/dev/null
	[ -f "$PASTA_T/$teste.falhou" ] && falhas=$((falhas + 1))
done

# Os mais demorados (segundos de relógio, com os outros rodando junto).
echo
echo "== Os dez testes mais demorados"
for teste in $escolhidos; do
	[ -f "$PASTA_T/$teste.s" ] && echo "$(cat "$PASTA_T/$teste.s") $teste"
done | sort -rn | head -10 | awk '{ printf "  %4d s  %s\n", $1, $2 }'
if [ -n "${TEMPOS:-}" ]; then
	for teste in $escolhidos; do [ -f "$PASTA_T/$teste.s" ] && echo "$(cat "$PASTA_T/$teste.s") $teste"; done | sort -rn > "$TEMPOS"
fi

echo
duracao=$(( $(date +%s) - inicio ))
if [ "$falhas" -eq 0 ]; then
	echo "Tudo passou (${duracao} s)."
	exit 0
fi

echo "$falhas etapa(s) ou teste(s) falharam (${duracao} s)."
exit 1
