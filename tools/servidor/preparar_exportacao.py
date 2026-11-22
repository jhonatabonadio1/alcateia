#!/usr/bin/env python3
"""Prepara uma CÓPIA do projeto para exportar o servidor de partida.

    python3 tools/servidor/preparar_exportacao.py [--arquitetura x86_64|arm64] [--raiz .]

Roda dentro do build da imagem (servidor/partida.Dockerfile), antes do
Godot importar e exportar. Nunca no projeto de quem desenvolve: muda os
.import e não é para commitar.

O servidor exportado troca toda textura por uma "casca" (o preset
"Servidor (Linux)" é de servidor dedicado: Strip Visuals), então importar
a arte em qualidade de jogo é trabalho jogado fora — era isso que levava
uns 10 minutos no construtor. Aqui:

- textura: reduzida a 64 px, sem mipmaps (a casca guarda só o tamanho, e o
  servidor não desenha nada);
- modelo 3D (glb/fbx/obj): sem níveis de detalhe, sem malha de sombra e
  sem desdobrar UV para luz assada — nada disso é colisão. A colisão que o
  importador gera (e as malhas, de que ela sai) continuam iguais;
- o preset do servidor: a arquitetura pedida (o Mac com Apple Silicon
  monta a imagem em arm64 no docker compose; o Fly roda x86_64).
"""
import argparse
import glob
import os
import re


def trocar(texto, chave, valor):
    """`chave=valor` em [params]: troca a linha, ou acrescenta no fim do bloco."""
    padrao = re.compile(rf"^{re.escape(chave)}=.*$", re.M)
    if padrao.search(texto):
        return padrao.sub(lambda _: f"{chave}={valor}", texto, count=1)
    inicio = texto.find("[params]")
    if inicio < 0:
        return texto
    return texto[:inicio + len("[params]")] + f"\n{chave}={valor}" + texto[inicio + len("[params]"):]


def importacao_leve(raiz):
    texturas = modelos = 0
    for imp in glob.glob(os.path.join(raiz, "**", "*.import"), recursive=True):
        texto = open(imp, encoding="utf-8", errors="ignore").read()
        antes = texto
        if 'importer="texture"' in texto:
            texto = trocar(texto, "process/size_limit", "64")
            texto = trocar(texto, "mipmaps/generate", "false")
            texto = trocar(texto, "compress/mode", "0")
            if texto != antes:
                texturas += 1
        elif 'importer="scene"' in texto:
            texto = trocar(texto, "meshes/generate_lods", "false")
            texto = trocar(texto, "meshes/create_shadow_meshes", "false")
            texto = re.sub(r"^meshes/light_baking=2$", "meshes/light_baking=1", texto, flags=re.M)
            if texto != antes:
                modelos += 1
        if texto != antes:
            open(imp, "w", encoding="utf-8").write(texto)
    return texturas, modelos


def arquitetura(raiz, arq):
    caminho = os.path.join(raiz, "export_presets.cfg")
    texto = open(caminho, encoding="utf-8").read()
    inicio = texto.find('name="Servidor (Linux)"')
    if inicio < 0:
        raise SystemExit('export_presets.cfg sem o preset "Servidor (Linux)"')
    opcoes = texto.find("binary_format/architecture=", inicio)
    fim = texto.find("\n", opcoes)
    texto = texto[:opcoes] + f'binary_format/architecture="{arq}"' + texto[fim:]
    export = re.compile(r'(export_path="build/servidor/WolfLeagueServer)\.[a-z0-9_]+(")')
    texto = export.sub(lambda m: f"{m.group(1)}.{arq}{m.group(2)}", texto, count=1)
    open(caminho, "w", encoding="utf-8").write(texto)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--raiz", default=".")
    p.add_argument("--arquitetura", default="x86_64", choices=["x86_64", "arm64"])
    a = p.parse_args()
    texturas, modelos = importacao_leve(a.raiz)
    arquitetura(a.raiz, a.arquitetura)
    print(f"importação leve: {texturas} texturas a 64 px, {modelos} modelos sem LOD; servidor em {a.arquitetura}")


if __name__ == "__main__":
    main()
