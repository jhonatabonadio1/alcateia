#!/usr/bin/env python3
"""Lista de arquivos do jogo exportado, para o desinstalador do NSIS.

Uso: gerar_lista.py PASTA_DO_JOGO SAIDA.nsh

O instalador copia a pasta inteira (File /r), mas o NSIS não guarda o que
copiou. Aqui lemos a mesma pasta e escrevemos os `Delete`/`RMDir` que o
desinstalador roda, de modo que ele só apaga o que o jogo trouxe — nunca a
pasta inteira, caso alguém instale numa pasta que já tinha outras coisas.
"""
import os
import sys


def nsis(caminho: str) -> str:
    if '"' in caminho or '\r' in caminho or '\n' in caminho:
        raise SystemExit(f"nome de arquivo que o NSIS não aceita: {caminho!r}")
    return caminho.replace("$", "$$").replace("/", "\\")


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    raiz, saida = sys.argv[1], sys.argv[2]
    arquivos, pastas = [], []
    for atual, subpastas, nomes in os.walk(raiz):
        rel = os.path.relpath(atual, raiz)
        if rel != ".":
            pastas.append(rel)
        arquivos += [os.path.normpath(os.path.join(rel, n)) for n in nomes]
    if not arquivos:
        raise SystemExit(f"{raiz} está vazia: o jogo foi exportado?")
    linhas = ["; gerado por instalador/gerar_lista.py — não editar"]
    linhas += [f'Delete "$INSTDIR\\{nsis(a)}"' for a in sorted(arquivos)]
    # as mais fundas primeiro, senão a pasta de fora ainda não está vazia
    linhas += [f'RMDir "$INSTDIR\\{nsis(p)}"' for p in sorted(pastas, key=lambda p: (-p.count(os.sep), p))]
    with open(saida, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(linhas) + "\n")
    print(f"{len(arquivos)} arquivos, {len(pastas)} pastas -> {saida}")


if __name__ == "__main__":
    main()
