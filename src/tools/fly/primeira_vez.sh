#!/usr/bin/env bash
# Monta a produção no Fly.io do zero, uma vez (o passo a passo de
# docs/FLY.md, em um script). Roda no seu Mac, com o flyctl logado:
#
#   brew install flyctl gh && fly auth login && gh auth login
#   src/tools/fly/primeira_vez.sh
#
# Pode rodar de novo: o que já existe ele pula. Pergunta antes do que
# custa dinheiro (o IPv4 dedicado, o banco). Guarde o que ele imprimir no
# fim (as chaves) num cofre: sem a ALCATEIA_MASTER_KEY, o 2FA do painel
# tem de ser refeito.

set -euo pipefail
cd "$(dirname "$0")/../../.."

API="${FLY_APP_API:-wolfleague-api}"
PARTIDA="${FLY_APP_PARTIDA:-wolfleague-partida}"
BANCO="${FLY_APP_DB:-wolfleague-db}"
REGIAO="${FLY_REGIAO:-gru}"
MAQUINAS="${MAQUINAS:-3}"
ORG="${FLY_ORG:-personal}"

passo() { printf '\n\033[1;36m== %s\033[0m\n' "$*"; }
pergunta() { local r; read -r -p "$1 [s/N] " r; [[ "$r" =~ ^[sS] ]]; }
existe_app() { fly apps list --json 2>/dev/null | python3 -c "import json,sys; sys.exit(0 if any(a.get('Name')==sys.argv[1] for a in json.load(sys.stdin)) else 1)" "$1"; }

command -v fly >/dev/null || { echo "instale o flyctl: brew install flyctl"; exit 1; }
fly auth whoami >/dev/null || { echo "faça login: fly auth login"; exit 1; }
[ -f fly.servidor.toml ] || { echo "rode da raiz do projeto"; exit 1; }

if [ "$API" != "wolfleague-api" ] || [ "$PARTIDA" != "wolfleague-partida" ] || [ "$BANCO" != "wolfleague-db" ]; then
	echo "Nomes diferentes do padrão: troque também em fly.servidor.toml (app, ALCATEIA_DB_HOST, ALCATEIA_FLY_APP),"
	echo "fly.partida.toml (app), project.godot (wolfleague/rede/servidor) e nas variáveis do GitHub."
	pergunta "Já trocou?" || exit 1
fi

passo "1. Apps"
for app in "$API" "$PARTIDA"; do
	if existe_app "$app"; then echo "$app já existe"; else fly apps create "$app" --org "$ORG"; fi
done

passo "2. Banco ($BANCO): Postgres 16 num volume (fly.banco.toml)"
SENHA_DB=""
if existe_app "$BANCO"; then
	echo "$BANCO já existe."
	read -r -s -p "Senha do banco (a que você guardou; Enter se o $API já tem a certa): " SENHA_DB; echo
else
	echo "Cria o Postgres como app do Fly: 1 máquina de 1 GB + volume de 10 GB (uns 7 dólares por mês)."
	pergunta "Criar?" || exit 1
	fly apps create "$BANCO" --org "$ORG"
	SENHA_DB=$(openssl rand -hex 24)
	fly secrets set --stage -a "$BANCO" POSTGRES_PASSWORD="$SENHA_DB" >/dev/null
	fly deploy -c fly.banco.toml -a "$BANCO" --ha=false
fi

passo "3. Segredos"
FROTA=$(openssl rand -hex 32)
MESTRA=$(openssl rand -hex 32)
read -r -p "Usuário do primeiro administrador do painel: " ADMIN_USER
read -r -s -p "Senha dele (forte): " ADMIN_SENHA; echo
TOKEN_PARTIDA=$(fly tokens create deploy -a "$PARTIDA" --name "servidor-liga-frota" -x 8760h)
segredos=(ALCATEIA_FLEET_KEY="$FROTA" ALCATEIA_MASTER_KEY="$MESTRA"
	ALCATEIA_ADMIN_USER="$ADMIN_USER" ALCATEIA_ADMIN_PASSWORD="$ADMIN_SENHA"
	ALCATEIA_FLY_TOKEN="$TOKEN_PARTIDA")
[ -n "$SENHA_DB" ] && segredos+=(ALCATEIA_DB_PASSWORD="$SENHA_DB")
if fly secrets list -a "$API" 2>/dev/null | grep -q ALCATEIA_FLEET_KEY; then
	echo "O $API já tem segredos. Trocar a ALCATEIA_MASTER_KEY invalida o 2FA dos administradores."
	pergunta "Sobrescrever tudo?" || { echo "mantive os segredos; pulei para o deploy"; FROTA=""; }
fi
if [ -n "$FROTA" ]; then
	fly secrets set --stage -a "$API" "${segredos[@]}" >/dev/null
	fly secrets set --stage -a "$PARTIDA" ALCATEIA_FLEET_KEY="$FROTA" >/dev/null
	echo "segredos gravados (valem no próximo deploy)"
fi

passo "4. Servidor no ar"
fly deploy -c fly.servidor.toml -a "$API" --ha=false --remote-only
for i in $(seq 1 20); do
	if curl -fsS "https://$API.fly.dev/health"; then echo; break; fi
	sleep 6
done

passo "5. Frota de partidas"
IP=$(fly ips list -a "$PARTIDA" --json | python3 -c "import json,sys; print(next((i['Address'] for i in json.load(sys.stdin) if i.get('Type')=='v4'), ''))")
if [ -z "$IP" ]; then
	echo "UDP precisa de IPv4 dedicado (cobrado à parte, ~2 dólares por mês)."
	pergunta "Reservar?" || exit 1
	fly ips allocate-v4 -a "$PARTIDA" --yes
	IP=$(fly ips list -a "$PARTIDA" --json | python3 -c "import json,sys; print(next(i['Address'] for i in json.load(sys.stdin) if i.get('Type')=='v4'))")
fi
echo "IPv4 da frota: $IP"
ROTULO="inicial-$(git rev-parse --short HEAD)"
fly deploy -c fly.partida.toml -a "$PARTIDA" --build-only --push --remote-only --image-label "$ROTULO"
export FLY_API_TOKEN
FLY_API_TOKEN=$(fly auth token 2>/dev/null || fly tokens create deploy -a "$PARTIDA")
JA=$(python3 src/tools/fly/frota.py --app "$PARTIDA" estado | grep -c "udp" || true)
if [ "$JA" -gt 0 ]; then
	python3 src/tools/fly/frota.py --app "$PARTIDA" atualizar --imagem "registry.fly.io/$PARTIDA:$ROTULO"
else
	python3 src/tools/fly/frota.py --app "$PARTIDA" criar "$MAQUINAS" --imagem "registry.fly.io/$PARTIDA:$ROTULO" \
		--api "http://$API.internal:8080" --ip "$IP" --regiao "$REGIAO"
fi
python3 src/tools/fly/frota.py --app "$PARTIDA" estado

passo "6. GitHub (o pipeline de lançamento)"
if command -v gh >/dev/null && gh auth status >/dev/null 2>&1; then
	REPO=$(gh repo view --json nameWithOwner -q .nameWithOwner)
	# o ambiente "producao" dos jobs de deploy (aprovação: Settings → Environments)
	gh api -X PUT "repos/$REPO/environments/producao" >/dev/null
	fly tokens create org -o "$ORG" --name "github-actions" | gh secret set FLY_API_TOKEN --repo "$REPO"
	[ "$API" != "wolfleague-api" ] && gh variable set FLY_APP_API --repo "$REPO" --body "$API"
	[ "$PARTIDA" != "wolfleague-partida" ] && gh variable set FLY_APP_PARTIDA --repo "$REPO" --body "$PARTIDA"
	echo "GitHub pronto em $REPO: segredo FLY_API_TOKEN e ambiente producao."
else
	echo "Sem o gh logado (brew install gh && gh auth login), faça à mão em"
	echo "Settings → Secrets and variables → Actions: segredo FLY_API_TOKEN com"
	echo "    fly tokens create org -o $ORG"
fi

passo "Pronto"
echo "Servidor:  https://$API.fly.dev  (o jogo exportado aponta para ele: o pipeline grava no project.godot)"
echo "Painel:    fly proxy 8081 -a $API   e abra http://localhost:8081"
echo "Logs:      fly logs -a $API   /   fly logs -a $PARTIDA"
echo "Lançar:    git tag v0.4.0-alpha.1 && git push origin v0.4.0-alpha.1"
if [ -n "$FROTA" ]; then
	echo
	echo "GUARDE NUM COFRE (não aparece de novo):"
	echo "    ALCATEIA_FLEET_KEY  = $FROTA"
	echo "    ALCATEIA_MASTER_KEY = $MESTRA"
	[ -n "$SENHA_DB" ] && echo "    senha do banco      = $SENHA_DB"
fi
