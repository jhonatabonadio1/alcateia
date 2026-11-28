#!/usr/bin/env bash
# Monta o cliente de instalação para Mac: publica o programa, põe num .app
# com o ícone do jogo, assina (ad hoc) e fecha num .dmg com o atalho para
# Aplicativos. Roda num Mac (precisa de codesign e hdiutil); o pipeline chama
# uma vez por processador.
#
#   src/tools/lancamento/cliente_mac.sh VERSAO arm64|x64 [PASTA_DE_SAIDA]
#
# Sai em PASTA_DE_SAIDA (padrão build/lancador/mac-<arq>):
#   WolfLeague.app                        o cliente (é o que o pacote de atualização leva)
#   WolfLeague-Launcher-<versão>-mac-<arq>.dmg
#
# SERVIDOR_URL (opcional): o servidor com que o cliente fala; sem ela, o de
# produção que está no .csproj.
#
# Sem conta de desenvolvedor da Apple o cliente não é autenticado: na
# primeira vez o jogador libera em Ajustes do Sistema → Privacidade e
# Segurança → Abrir Mesmo Assim (como o jogo, até aqui). O que o cliente
# baixa depois (o jogo, a atualização dele mesmo) não passa por isso.

set -euo pipefail
cd "$(dirname "$0")/../../.."

VERSAO="${1:?uso: cliente_mac.sh VERSAO arm64|x64 [SAIDA]}"
ARQ="${2:?uso: cliente_mac.sh VERSAO arm64|x64 [SAIDA]}"
SAIDA="${3:-build/lancador/mac-$ARQ}"
[ "$ARQ" = arm64 ] || [ "$ARQ" = x64 ] || { echo "processador: arm64 ou x64"; exit 2; }

rm -rf "$SAIDA"
mkdir -p "$SAIDA/publicado"

extra=()
[ -n "${SERVIDOR_URL:-}" ] && extra+=("-p:ServidorUrl=$SERVIDOR_URL")
dotnet publish cliente/Alcateia.Cliente/Alcateia.Cliente.csproj -c Release -r "osx-$ARQ" --self-contained true \
	-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
	-p:PublishTrimmed=true -p:TrimMode=partial -p:DebugType=none \
	-p:Version="$VERSAO" ${extra[@]+"${extra[@]}"} -o "$SAIDA/publicado" --nologo -v quiet

APP="$SAIDA/WolfLeague.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$SAIDA/publicado/WolfLeagueLauncher" "$APP/Contents/MacOS/WolfLeagueLauncher"
chmod +x "$APP/Contents/MacOS/WolfLeagueLauncher"

# O ícone: a maior imagem de dentro do .ico do jogo, convertida para .icns.
python3 - instalador/WolfLeague.ico "$SAIDA/icone.png" <<'PY'
import struct, sys
dados = open(sys.argv[1], "rb").read()
_, _, quantos = struct.unpack_from("<HHH", dados, 0)
maior = None
for i in range(quantos):
    largura, _, _, _, _, _, tamanho, onde = struct.unpack_from("<BBBBHHII", dados, 6 + 16 * i)
    largura = largura or 256
    if maior is None or largura > maior[0]:
        maior = (largura, dados[onde:onde + tamanho])
if maior is None or not maior[1].startswith(b"\x89PNG"):
    raise SystemExit("o .ico não tem uma imagem PNG para virar ícone")
open(sys.argv[2], "wb").write(maior[1])
PY
sips -s format icns "$SAIDA/icone.png" --out "$APP/Contents/Resources/icone.icns" >/dev/null

# A versão do pacote só leva números (0.5.0-alpha.17 -> 0.5.0).
NUMEROS="${VERSAO%%-*}"
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleName</key><string>WolfLeague</string>
	<key>CFBundleDisplayName</key><string>WolfLeague</string>
	<key>CFBundleIdentifier</key><string>com.wolfleague.launcher</string>
	<key>CFBundleExecutable</key><string>WolfLeagueLauncher</string>
	<key>CFBundleIconFile</key><string>icone</string>
	<key>CFBundlePackageType</key><string>APPL</string>
	<key>CFBundleShortVersionString</key><string>$NUMEROS</string>
	<key>CFBundleVersion</key><string>$NUMEROS</string>
	<key>LSMinimumSystemVersion</key><string>11.0</string>
	<key>NSHighResolutionCapable</key><true/>
	<key>LSApplicationCategoryType</key><string>public.app-category.games</string>
</dict>
</plist>
PLIST

# Assinatura ad hoc com o codesign do próprio Mac (como o jogo): sem ela o
# macOS mata o programa ao abrir, no Apple Silicon. Não conferiu, não sai .dmg.
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"

rm -rf "$SAIDA/dmg"
mkdir -p "$SAIDA/dmg"
cp -R "$APP" "$SAIDA/dmg/WolfLeague.app"
ln -s /Applications "$SAIDA/dmg/Applications"
DMG="$SAIDA/WolfLeague-Launcher-$VERSAO-mac-$ARQ.dmg"
hdiutil create -volname "WolfLeague" -srcfolder "$SAIDA/dmg" -ov -format UDZO "$DMG" >/dev/null
rm -rf "$SAIDA/dmg" "$SAIDA/publicado" "$SAIDA/icone.png"
ls -la "$SAIDA"
