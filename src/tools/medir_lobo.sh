#!/usr/bin/env bash
# Mede as animações do lobo no jogo de verdade e guarda o resultado em
# .medidas/lobo.txt (pasta com ponto: o Godot não importa).
#
#   src/tools/medir_lobo.sh
#
# Roda os testes que dependem dos clipes do lobo: PassadaDoLobo (quanto o
# pé de apoio patina em cada direção) e PassoNoRitmo (som do passo na
# cadência). Serve para conferir as passadas (WalkClipSpeed e as outras)
# depois de trocar o lobo_anim.glb, sem precisar copiar a saída à mão.

set -u
cd "$(dirname "$0")/../.." || exit 1

export GODOT="${GODOT:-/Applications/Godot_mono.app/Contents/MacOS/Godot}"
mkdir -p .medidas

{
	date
	src/tools/testar.sh Pass
} 2>&1 | tee .medidas/lobo.txt
