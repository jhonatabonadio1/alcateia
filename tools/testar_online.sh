#!/usr/bin/env bash
# Teste de ponta a ponta do jogo online: o servidor, um servidor de
# partida e dois jogos de verdade (sem tela), que criam conta, abrem
# sala com um bot em cada time, jogam uma partida curta e conferem o
# resultado — inclusive que os bots (movidos pela máquina de quem criou
# a sala) andam, atiram, matam e morrem para os dois.
#
#   tools/testar_online.sh
#
# Precisa de um servidor do Alcateia no ar. Aponte para ele com
#   ALCATEIA_API=http://127.0.0.1:8080  ALCATEIA_FLEET_KEY=<o segredo>
# (são os padrões do "docker compose up" com o .env). Com o compose no
# ar, pare antes o servidor de partida dele, para a partida cair neste:
#   docker compose stop partida
#
# O Godot vem de $GODOT, ou do "godot" no PATH.

set -u
cd "$(dirname "$0")/.." || exit 1

GODOT="${GODOT:-godot}"
export ALCATEIA_API="${ALCATEIA_API:-http://127.0.0.1:8080}"
export ALCATEIA_FLEET_KEY="${ALCATEIA_FLEET_KEY:-}"
PORTA="${ALCATEIA_TEST_GAME_PORT:-17777}"

if [ -z "$ALCATEIA_FLEET_KEY" ] && [ -f .env ]; then
	ALCATEIA_FLEET_KEY=$(grep -E '^ALCATEIA_FLEET_KEY=' .env | cut -d= -f2-)
	export ALCATEIA_FLEET_KEY
fi
if [ -z "$ALCATEIA_FLEET_KEY" ]; then
	echo "Defina ALCATEIA_FLEET_KEY (o segredo da frota do servidor)."
	exit 1
fi
if ! curl -fsS -m 5 "$ALCATEIA_API/health" >/dev/null; then
	echo "Não achei o servidor em $ALCATEIA_API. Suba com: docker compose up -d"
	exit 1
fi

echo "== Compilando"
dotnet build alcateia.csproj --nologo -v quiet -clp:ErrorsOnly || exit 1

PASTA=$(mktemp -d)
limpar() {
	[ -n "${PARTIDA:-}" ] && kill "$PARTIDA" 2>/dev/null
	rm -rf "$PASTA"
}
trap limpar EXIT

echo "== Servidor de partida (porta $PORTA/UDP)"
ALCATEIA_GAME_PORT="$PORTA" ALCATEIA_PUBLIC_ADDRESS=127.0.0.1 XDG_DATA_HOME="$PASTA/servidor" \
	"$GODOT" --headless --path . -- --servidor --duracao=35 --contagem=3 > "$PASTA/servidor.log" 2>&1 &
PARTIDA=$!
sleep 4

SUFIXO=$(date +%s | tail -c 6)
echo "== Dois jogadores"
XDG_DATA_HOME="$PASTA/a" timeout 200 "$GODOT" --headless --path . -- --robo=criar --conta="RoboA$SUFIXO" > "$PASTA/a.log" 2>&1 &
A=$!
sleep 1
XDG_DATA_HOME="$PASTA/b" timeout 200 "$GODOT" --headless --path . -- --robo=entrar --conta="RoboB$SUFIXO" > "$PASTA/b.log" 2>&1 &
B=$!
wait "$A"; saida_a=$?
wait "$B"; saida_b=$?

echo
grep -h "^ROBO" "$PASTA/a.log" "$PASTA/b.log"
echo
grep -hE "Partida|Entrou|Recusado" "$PASTA/servidor.log"

falhas=0
conferir() {
	if grep -qE "$2" "$1"; then echo "  ok      $3"; else echo "  FALHOU  $3"; falhas=$((falhas + 1)); fi
}

echo
conferir "$PASTA/a.log" "^ROBO RoboA$SUFIXO fim: partidas=1 " "o criador tem a partida registrada na conta"
conferir "$PASTA/b.log" "^ROBO RoboB$SUFIXO fim: partidas=1 " "o visitante tem a partida registrada na conta"
conferir "$PASTA/a.log" "abates=([2-9]|[1-9][0-9])" "o criador abateu o visitante mais de uma vez (acerto, morte, abate e renascimento passaram pela rede)"
conferir "$PASTA/b.log" "mortes=[1-9]" "as mortes do visitante estão na conta dele"
conferir "$PASTA/b.log" "tiros_do_boneco=[1-9]" "o visitante viu o boneco do criador atirar"
conferir "$PASTA/b.log" "acertos_levados=[1-9]" "o visitante levou o dano na máquina dele"
conferir "$PASTA/a.log" "vencedor=[012] " "o servidor deu o resultado da partida"
conferir "$PASTA/a.log" "bots_meus=2 " "os dois bots da sala são bots de verdade na máquina do criador"
conferir "$PASTA/b.log" "bonecos_bot=2 " "e bonecos na máquina do visitante"
conferir "$PASTA/a.log" "dificuldade=Hard " "os bots jogam na dificuldade da sala (difíceis)"
conferir "$PASTA/b.log" "bots_andaram=[12] " "o visitante viu os bots andarem"
conferir "$PASTA/b.log" "tiros_de_bot=[1-9]" "o visitante viu bot atirar"
conferir "$PASTA/b.log" "abateu_bot=[1-9]" "o visitante abateu um bot (o acerto foi até a máquina do criador, e a morte voltou pelo servidor)"
conferir "$PASTA/a.log" "abates_com_bot=[1-9]" "o criador viu abates com bot no placar do servidor"
conferir "$PASTA/a.log" "avisos_do_saguao=1 .*sala=waiting" "o saguão avisou o fim e a sala voltou a esperar"

# Como nos testes de regressão: erro no Output reprova, mesmo com o resto certo.
if grep -hE '^(ERROR|SCRIPT ERROR|USER ERROR)|Unhandled exception|NullReferenceException' "$PASTA"/*.log \
	| grep -v -E 'resources still in use at exit|ObjectDB instances leaked|RID allocations|Pages in use exist at exit' > "$PASTA/erros.txt"; then
	echo "  FALHOU  nenhum erro no Output dos três processos"
	sort "$PASTA/erros.txt" | uniq -c | head -n 12
	falhas=$((falhas + 1))
else
	echo "  ok      nenhum erro no Output dos três processos"
fi

if [ "$saida_a" -ne 0 ] || [ "$saida_b" -ne 0 ] || [ "$falhas" -ne 0 ]; then
	echo
	echo "O teste online falhou. Últimas linhas de cada processo:"
	for f in servidor a b; do echo "--- $f"; tail -n 15 "$PASTA/$f.log"; done
	exit 1
fi

echo
echo "O jogo online passou de ponta a ponta."
