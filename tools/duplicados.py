#!/usr/bin/env python3
"""Acha código repetido em scripts/: trechos de N linhas iguais (ou mais)
em dois lugares, ignorando espaço, comentário, linha vazia, chave solta e
`using`. Sai com erro se achar — o tools/testar.sh roda isto.

uso: tools/duplicados.py [N]     (padrão: 6)

Achou? Junte numa peça só: conta de regra em scripts/domain, o que o
jogador e o bot fazem igual em scripts/actors/body, peça de tela no
UiKit, busca na árvore em Nodes, nome de grupo em Groups.
"""
import collections
import glob
import re
import sys

N = int(sys.argv[1]) if len(sys.argv) > 1 else 6
TRIVIAL = {"{", "}", "};", "else", "break;", "return;"}

lines = {}
for path in sorted(glob.glob("scripts/**/*.cs", recursive=True)):
    kept = []
    for number, line in enumerate(open(path, encoding="utf-8").read().split("\n"), 1):
        code = re.sub(r"//.*", "", line).strip()
        if not code or code in TRIVIAL or code.startswith("using "):
            continue
        kept.append((number, code))
    lines[path] = kept

windows = collections.defaultdict(list)
for path, kept in lines.items():
    for k in range(len(kept) - N + 1):
        key = "\n".join(code for _, code in kept[k:k + N])
        windows[key].append((path, kept[k][0], kept[k + N - 1][0]))

seen = set()
found = 0
for key, places in windows.items():
    if len(places) < 2 or any((p, a - 1) in seen for p, a, _ in places):
        for p, a, _ in places:
            seen.add((p, a))
        continue
    for p, a, _ in places:
        seen.add((p, a))
    found += 1
    print("repetido: " + "  |  ".join(f"{p}:{a}-{b}" for p, a, b in places))
    print("    " + key.split("\n")[0][:100])

if found:
    print(f"{found} trecho(s) de {N} linhas ou mais repetido(s).")
    sys.exit(1)
print(f"Nenhum trecho de {N} linhas ou mais repetido.")
