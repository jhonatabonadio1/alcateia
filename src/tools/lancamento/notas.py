#!/usr/bin/env python3
"""As novidades de uma versão, em poucas linhas, para o cliente de instalação mostrar.

    notas.py [docs/CHANGELOG.md] > notas.txt

Lê a primeira seção do CHANGELOG (a que está no topo: "Não lançado", ou a da
versão que está saindo) e fica só com o título de cada item — o trecho em
negrito que abre a linha ("- **Fôlego na corrida** (Jhon, …") —, um por linha.
O texto inteiro de cada item é para quem desenvolve; o jogador quer saber o
que mudou. O pipeline passa o resultado a `pacote empacotar --notas`, e ele
vai dentro do manifesto (aba Novidades do cliente).
"""
import re
import sys

MAXIMO = 24   # itens: mais que isso ninguém lê numa aba de novidades


def titulos(texto: str) -> list[str]:
    secao = re.split(r"^## .*$", texto, flags=re.M)
    corpo = secao[1] if len(secao) > 1 else ""
    achados = []
    for linha in corpo.splitlines():
        m = re.match(r"^- \*\*(.+?)\*\*", linha)      # só os itens de fora, não os recuados
        if not m:
            continue
        titulo = re.sub(r"`([^`]*)`", r"\1", m.group(1)).strip().rstrip(".:")
        if titulo:
            achados.append(titulo)
    return achados


def main() -> None:
    caminho = sys.argv[1] if len(sys.argv) > 1 else "docs/CHANGELOG.md"
    with open(caminho, encoding="utf-8") as f:
        lista = titulos(f.read())
    resto = len(lista) - MAXIMO
    linhas = [f"• {t}" for t in lista[:MAXIMO]]
    if resto > 0:
        linhas.append(f"… e mais {resto} mudança(s).")
    print("\n".join(linhas))


if __name__ == "__main__":
    main()
