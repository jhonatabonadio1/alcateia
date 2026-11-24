#!/usr/bin/env python3
"""Prepara o projeto para o build de produção (o pipeline roda antes de exportar).

    preparar_build.py --versao 0.4.0-alpha.1 --numero 0.4.0.0 \
                      --servidor https://wolfleague-api.fly.dev

- project.godot: wolfleague/rede/servidor (para onde o jogo exportado
  conecta; no editor continua o servidor local) e application/config/version;
- export_presets.cfg: a versão no .exe (Windows) e no .app (Mac).

Só mexe na cópia do checkout do pipeline; não é para commitar o resultado.
"""
import argparse
import re
import sys


def trocar_ou_por(texto, secao, chave, valor):
    """Põe `chave=valor` dentro de [secao], trocando a linha se ela já existe."""
    linha = f'{chave}="{valor}"'
    inicio = texto.find(f"[{secao}]")
    if inicio < 0:
        return texto.rstrip("\n") + f"\n\n[{secao}]\n\n{linha}\n"
    fim = texto.find("\n[", inicio + 1)
    fim = len(texto) if fim < 0 else fim
    bloco = texto[inicio:fim]
    padrao = re.compile(rf"^{re.escape(chave)}=.*$", re.M)
    if padrao.search(bloco):
        bloco = padrao.sub(lambda _: linha, bloco, count=1)
    else:
        cab = bloco.index("\n") + 1
        bloco = bloco[:cab] + linha + "\n" + bloco[cab:]
    return texto[:inicio] + bloco + texto[fim:]


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--versao", required=True)
    p.add_argument("--numero", required=True, help="quatro números, para o Windows")
    p.add_argument("--servidor", required=True)
    p.add_argument("--raiz", default=".")
    a = p.parse_args()

    url = a.servidor.strip().rstrip("/")
    if not re.fullmatch(r"https://[a-z0-9.-]+(:\d+)?", url):
        sys.exit(f"servidor de produção tem de ser https://host, veio {url!r}")
    if not re.fullmatch(r"\d+\.\d+\.\d+\.\d+", a.numero):
        sys.exit(f"--numero tem de ser x.y.z.w, veio {a.numero!r}")
    curta = a.versao.split("-")[0]

    proj = f"{a.raiz}/project.godot"
    s = open(proj, encoding="utf-8").read()
    s = trocar_ou_por(s, "wolfleague", "rede/servidor", url)
    s = trocar_ou_por(s, "application", "config/version", a.versao)
    open(proj, "w", encoding="utf-8").write(s)

    pre = f"{a.raiz}/export_presets.cfg"
    s = open(pre, encoding="utf-8").read()
    for chave, valor in (("application/file_version", a.numero), ("application/product_version", a.numero),
                         ("application/short_version", curta), ("application/version", curta)):
        s = re.sub(rf'^{re.escape(chave)}=.*$', f'{chave}="{valor}"', s, flags=re.M)
    open(pre, "w", encoding="utf-8").write(s)

    print(f"build {a.versao} ({a.numero}) -> {url}")


if __name__ == "__main__":
    main()
