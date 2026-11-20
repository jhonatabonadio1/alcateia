#!/usr/bin/env python3
"""Tira do código do jogo a semente do servidor.

O catálogo (scripts/meta/Catalog.cs), os mapas (scripts/meta/Maps.cs), as
missões (scripts/meta/Missions.cs), os personagens (assets/personagens) e
os modos de jogo liberados (scripts/core/RoomConfig.cs) viram
servidor/Alcateia.Infra/Semente/jogo.json. O servidor lê esse arquivo ao
subir e põe no banco SÓ o que ainda não existe: o que o painel já editou
(preço, raridade, ligado ou não) nunca é sobrescrito. O que é do jogo e o
painel não edita — a arte de cada item e, nos mapas, a foto e os modos
para que cada um foi feito — acompanha o jogo a cada subida.

Entrou arma, skin, mapa ou missão nova no jogo? Rode de novo e faça commit:

    python3 tools/servidor/exportar_dados.py

Sem dependências: só a biblioteca padrão.
"""
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "servidor" / "Alcateia.Infra" / "Semente" / "jogo.json"

CONSTANTS = {"Arsenal.KnifeId": "faca", "Arsenal.GrenadeId": "granada"}
KINDS = {"Weapon": "weapon", "Utility": "utility", "Tattoo": "tattoo", "Breed": "breed", "Character": "character", "Service": "service"}
MODES = {"Equipe": "equipe", "MataMata": "mata_mata", "Dominacao": "dominacao", "Cacada": "cacada", "Sobrevivencia": "sobrevivencia"}
METRICS = {"Matches": "matches", "Wins": "wins", "Kills": "kills", "KillsWolf": "kills_wolf", "KillsHuman": "kills_human",
           "Headshots": "headshots", "Assists": "assists", "Minutes": "minutes", "Mvps": "mvps"}


def closing(text, opening):
    """Onde fecha a chave que abre em `opening`."""
    depth, i = 1, opening + 1
    while depth:
        depth += {"{": 1, "}": -1}.get(text[i], 0)
        i += 1
    return i - 1


def blocks(text, start):
    """Os blocos `new() { ... }` de dentro da lista que começa em `start`."""
    opening = text.index("{", text.index(start))
    end = closing(text, opening)
    found, at = [], opening + 1
    while True:
        m = re.compile(r"new\(\)\s*\{").search(text, at, end)
        if not m:
            return found
        close = closing(text, m.end() - 1)
        found.append(text[m.end():close])
        at = close + 1


def text_of(block, field):
    m = re.search(rf'\b{field}\s*=\s*"((?:[^"\\]|\\.)*)"', block)
    if m:
        return m.group(1).replace('\\"', '"')
    m = re.search(rf"\b{field}\s*=\s*([A-Za-z_.]+)", block)
    return CONSTANTS.get(m.group(1)) if m else None


def int_of(block, field, fallback=0):
    m = re.search(rf"\b{field}\s*=\s*(-?\d+)", block)
    return int(m.group(1)) if m else fallback


def flag(block, field):
    return re.search(rf"\b{field}\s*=\s*true", block) is not None


def image(*candidates):
    for relative, served in candidates:
        if (ROOT / relative).exists():
            return served
    return ""


def catalog():
    source = (ROOT / "scripts/meta/Catalog.cs").read_text(encoding="utf-8")
    source = re.sub(r"^\s*//.*$", "", source, flags=re.M)
    items = []
    for block in blocks(source, "_items = WithCharacters("):
        item_id = text_of(block, "Id")
        if not item_id:
            continue
        prices = re.search(r"Prices\s*=\s*new\[\]\s*\{([^}]*)\}", block)
        kind = re.search(r"Kind\s*=\s*ItemKind\.(\w+)", block).group(1)
        items.append({
            "id": item_id, "name": text_of(block, "Name"), "type": text_of(block, "Type") or "", "kind": KINDS[kind],
            "rarity": int_of(block, "Rarity"), "minRank": int_of(block, "MinRank"), "currency": "presas",
            "prices": [int(p) for p in prices.group(1).split(",")] if prices else [],
            "starter": flag(block, "Starter"), "hot": flag(block, "Hot"), "consumable": flag(block, "Consumable"), "team": 0,
            "image": image((f"assets/ui/armas/{item_id}.png", f"armas/{item_id}.png")),
        })

    # Os personagens são uma pasta cada (assets/personagens/<pasta>/personagem.tres).
    services = next((i for i, item in enumerate(items) if item["kind"] == "service"), len(items))
    people = []
    for folder in sorted((ROOT / "assets/personagens").glob("*/personagem.tres")):
        tres = folder.read_text(encoding="utf-8")
        name = folder.parent.name
        team = int_of(tres, "Team")
        side = {1: "Vanguarda", 2: "Sindicato"}.get(team, "os dois times")
        people.append({
            "id": f"pers_{name}", "name": text_of(tres, "DisplayName") or name, "type": f"Personagem · {side}", "kind": "character",
            "rarity": int_of(tres, "Rarity"), "minRank": 0, "currency": "presas", "prices": [], "starter": True, "hot": False,
            "consumable": False, "team": team,
            "image": image((f"assets/personagens/{name}/thumb.jpg", f"personagens/{name}.jpg")),
        })
    items[services:services] = people
    for order, item in enumerate(items):
        item["sort"] = order * 10
    return items


def maps():
    source = (ROOT / "scripts/meta/Maps.cs").read_text(encoding="utf-8")
    source = re.sub(r"^\s*//.*$", "", source, flags=re.M)
    kept = source.index("Guardados = new()")
    found = []
    for enabled, part in ((True, source[:kept]), (False, source[kept:])):
        for block in blocks(part, "= new()"):
            map_id = text_of(block, "Id")
            if not map_id:
                continue
            modes = re.search(r"Modes\s*=\s*new\[\]\s*\{([^}]*)\}", block)
            found.append({
                "id": map_id, "name": text_of(block, "Name"), "size": text_of(block, "Size") or "Médio",
                "description": text_of(block, "Description") or "",
                "modes": [MODES[m] for m in re.findall(r"GameMode\.(\w+)", modes.group(1))] if modes else [],
                "enabled": enabled, "sort": len(found) * 10,
                "image": image((f"assets/ui/mapas/{map_id}_foto.png", f"mapas/{map_id}_foto.png"),
                               (f"assets/ui/mapas/{map_id}.png", f"mapas/{map_id}.png")),
            })
    return found


def challenges():
    source = (ROOT / "scripts/meta/Missions.cs").read_text(encoding="utf-8")
    period = {"D": "daily", "W": "weekly", "P": "permanent"}
    found = []
    pattern = re.compile(r'\b([DWP])\("(\w+)",\s*"([^"]*)",\s*MissionMetric\.(\w+),\s*(\d+),\s*(\d+),\s*(\d+)(?:,\s*"([^"]*)")?\)')
    for kind, mission_id, text, metric, target, presas, xp, title in pattern.findall(source):
        found.append({
            "id": mission_id, "period": period[kind], "text": text, "metric": METRICS[metric], "target": int(target),
            "presas": int(presas),
            # A conta de MissionXp, em Missions.cs: um quarto, de 5 em 5, no mínimo 5.
            "xp": max(5, round(int(xp) / 4 / 5) * 5),
            "title": title or None, "sort": len(found) * 10,
        })
    return found


def released_modes():
    """Os modos que o jogo deixa escolher hoje (RoomSettings.ModeAvailable, em RoomConfig.cs)."""
    source = (ROOT / "scripts/core/RoomConfig.cs").read_text(encoding="utf-8")
    line = next((l for l in source.splitlines() if "ModeAvailable(GameMode" in l), "")
    found = [MODES[m] for m in re.findall(r"GameMode\.(\w+)", line.split("=>", 1)[-1])]
    if not found:
        print("não achei RoomSettings.ModeAvailable: considerei só o modo Equipe liberado", file=sys.stderr)
    return found or ["equipe"]


def main():
    data = {"releasedModes": released_modes(), "catalog": catalog(), "maps": maps(), "challenges": challenges()}
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(data, ensure_ascii=False, indent="\t") + "\n", encoding="utf-8")
    print(f"{OUT.relative_to(ROOT)}: {len(data['catalog'])} itens, {len(data['maps'])} mapas, {len(data['challenges'])} desafios; "
          f"modos liberados: {', '.join(data['releasedModes'])}")
    missing = [i["id"] for i in data["catalog"] if not i["image"]] + [m["id"] for m in data["maps"] if not m["image"]]
    if missing:
        print("sem imagem (o painel mostra o nome):", ", ".join(missing), file=sys.stderr)


if __name__ == "__main__":
    main()
