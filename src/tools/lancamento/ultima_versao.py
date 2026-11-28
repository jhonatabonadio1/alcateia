#!/usr/bin/env python3
"""Escreve o latest.json: a última versão do cliente de instalação, para o site.

    ultima_versao.py --versao 0.5.0 --publico https://downloads.exemplo --pasta saida > saida/latest.json

O site põe no botão de baixar os links de nome fixo (sempre a última versão):

    <publico>/WolfLeague-Launcher-Setup.exe          Windows
    <publico>/WolfLeague-Launcher-mac-arm64.dmg      Mac com Apple Silicon
    <publico>/WolfLeague-Launcher-mac-x64.dmg        Mac com Intel

e, se quiser mostrar a versão, o tamanho ou conferir o download, lê este
arquivo (<publico>/latest.json). Cada sistema traz o link fixo (`url`), o da
versão (`versioned`, que nunca muda de conteúdo), o tamanho em bytes e o
SHA-256. Para uma página ler o JSON pelo navegador, o balde precisa de uma
regra de CORS liberando o domínio do site (cliente/LEIA-ME.md).
"""
import argparse
import datetime
import hashlib
import json
import os

ARQUIVOS = {
    # sistema: (nome com a versão, nome fixo)
    "windows": ("WolfLeague-Launcher-Setup-{v}.exe", "WolfLeague-Launcher-Setup.exe"),
    "mac-arm64": ("WolfLeague-Launcher-{v}-mac-arm64.dmg", "WolfLeague-Launcher-mac-arm64.dmg"),
    "mac-x64": ("WolfLeague-Launcher-{v}-mac-x64.dmg", "WolfLeague-Launcher-mac-x64.dmg"),
}


def sha256(caminho: str) -> str:
    soma = hashlib.sha256()
    with open(caminho, "rb") as f:
        for bloco in iter(lambda: f.read(1 << 20), b""):
            soma.update(bloco)
    return soma.hexdigest()


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--versao", required=True)
    p.add_argument("--publico", required=True, help="o endereço público do balde")
    p.add_argument("--pasta", required=True, help="onde estão os instaladores, com a versão no nome")
    a = p.parse_args()

    publico = a.publico.rstrip("/")
    saida = {
        "version": a.versao,
        "date": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    for sistema, (com_versao, fixo) in ARQUIVOS.items():
        nome = com_versao.format(v=a.versao)
        caminho = os.path.join(a.pasta, nome)
        if not os.path.isfile(caminho):
            raise SystemExit(f"falta o instalador {nome} em {a.pasta}")
        saida[sistema] = {
            "url": f"{publico}/{fixo}",
            "versioned": f"{publico}/v{a.versao}/{nome}",
            "size": os.path.getsize(caminho),
            "sha256": sha256(caminho),
        }
    print(json.dumps(saida, indent=2))


if __name__ == "__main__":
    main()
