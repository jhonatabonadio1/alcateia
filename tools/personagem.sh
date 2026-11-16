#!/usr/bin/env bash
# Prepara e confere os modelos de um personagem (docs/PERSONAGENS.md).
#
#   tools/personagem.sh medir <modelo.fbx|glb>       altura, quadril, ossos, texturas
#   tools/personagem.sh lobo  <lobo_cru.glb|fbx> <pasta>  limpa, 1,66 m, texturas 2048 → <pasta>/lobo.glb
#   tools/personagem.sh escala <humano.fbx>           o root_scale da importação para 1,79 m
#
# Usa o Blender (BLENDER= aponta para outro, se não estiver no lugar padrão).

set -euo pipefail
cd "$(dirname "$0")/.."

BLENDER="${BLENDER:-/Applications/Blender.app/Contents/MacOS/Blender}"
HERE=tools/personagens
HUMAN_HEIGHT=1.79

absolute() { (cd "$(dirname "$1")" && echo "$PWD/$(basename "$1")"); }

measure() {
	"$BLENDER" -b --factory-startup -P "$HERE/blender_medir.py" -- "$(absolute "$1")" 2>&1 | grep '^MEDIDA' | sed 's/^MEDIDA //'
}

case "${1:-}" in
	medir)
		measure "$2"
		;;
	lobo)
		mkdir -p "$3"
		"$BLENDER" -b --factory-startup -P "$HERE/blender_lobo.py" -- "$(absolute "$2")" "$(absolute "$3")/lobo.glb" 2>&1 \
			| grep -E '^PREPARO|Error' | sed 's/^PREPARO //'
		;;
	escala)
		height=$(measure "$2" | sed -n 's/^altura \([0-9.]*\) m.*/\1/p')
		python3 -c "h=float('$height'); print(f'altura {h:.3f} m → nodes/root_scale={$HUMAN_HEIGHT/h:.4f} no {\"$2\"}.import')"
		;;
	*)
		sed -n '2,9p' "$0"
		exit 1
		;;
esac
