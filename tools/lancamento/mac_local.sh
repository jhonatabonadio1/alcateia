#!/usr/bin/env bash
# Gera o .dmg do Mac aqui, sem o CI.
#   tools/lancamento/mac_local.sh 0.4.0-alpha.1
# Antes: feche o editor do Godot; tenha o Godot 4.7.2 .NET e os modelos de
# exportação 4.7.2.mono instalados (Editor → Gerenciar Modelos de Exportação).
set -euo pipefail
cd "$(dirname "$0")/../.."

VERSAO="${1:?uso: mac_local.sh 0.4.0-alpha.1}"
GODOT="${GODOT:-/Applications/Godot_mono.app/Contents/MacOS/Godot}"
SERVIDOR="${SERVIDOR_URL:-https://wolfleague-api.fly.dev}"
NUMERO="$(echo "${VERSAO%%-*}" | awk -F. '{printf "%d.%d.%d.0", $1, $2, $3}')"

test -x "$GODOT" || { echo "Godot não achado em $GODOT (defina GODOT=...)"; exit 1; }

# o preparar_build.py mexe nestes dois arquivos; devolve ao original no fim
copia="$(mktemp -d)"
cp project.godot export_presets.cfg "$copia/"
trap 'cp "$copia/project.godot" "$copia/export_presets.cfg" . && rm -rf "$copia"' EXIT

python3 tools/lancamento/preparar_build.py --versao "$VERSAO" --numero "$NUMERO" --servidor "$SERVIDOR"

dotnet build WolfLeague.csproj --nologo -v quiet
"$GODOT" --headless --path . --import || true

rm -rf build/macos dmg
mkdir -p build/macos entrega
"$GODOT" --headless --path . --export-release "macOS" build/macos/WolfLeague.zip
ditto -x -k build/macos/WolfLeague.zip build/macos/app

app="$(find build/macos/app -maxdepth 1 -name '*.app' | head -1)"
test -d "$app"
# O Godot exporta sem assinar (codesign=0): assinatura ad hoc com o codesign
# do Mac. A do Godot fazia o macOS matar o jogo ao abrir (21/11).
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict --verbose=2 "$app"

mkdir -p dmg && cp -R "$app" dmg/WolfLeague.app && ln -s /Applications dmg/Applications
dmg_arq="entrega/WolfLeague-${VERSAO}-mac.dmg"
hdiutil create -volname "WolfLeague" -srcfolder dmg -ov -format UDZO "$dmg_arq"
ls -lh "$dmg_arq"
shasum -a 256 "$dmg_arq"
