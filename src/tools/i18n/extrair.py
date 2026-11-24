#!/usr/bin/env python3
"""
Internacionalização do WolfLeague (pedido do Jhon, 18/11): junta todos os textos
do jogo em assets/i18n/textos.csv, para trocar nomes e textos num lugar só e abrir
outros idiomas.

    python3 tools/i18n/extrair.py            # varre scripts/ e atualiza o CSV (não perde o que já foi traduzido)
    python3 tools/i18n/extrair.py --ligar    # além disso, passa os textos montados em código pelo Loc (uma vez)

O CSV: a coluna "texto" é o texto como está no código (a chave; não mexa nela).
"pt_BR" é o que aparece em português: vazia = igual à chave; preencha para
trocar um nome sem tocar no código. "en", "es"...: as traduções; vazia = aparece
em português. Para abrir outro idioma, some uma coluna com o código dele.
"onde" diz de que arquivo veio (só para ajudar quem traduz).
"""
import csv, io, os, re, sys
sys.path.insert(0, os.path.dirname(__file__))
from cs import tokens, unescape, escape, split_interp

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CSV = os.path.join(RAIZ, "assets", "i18n", "textos.csv")
PASTAS = ["scripts"]
# só o que vai para a tela: ferramenta, depuração, animação, rede de baixo nível e física não têm texto de jogo
FORA = ("scripts/tools/", "scripts/debug/", "scripts/visual/wolf/", "scripts/visual/anim/", "scripts/visual/human/",
        "scripts/visual/fx/", "scripts/actors/", "scripts/ai/", "scripts/net/MatchServer", "scripts/net/Net", "scripts/net/Server",
        "scripts/core/Sfx", "scripts/core/NavBaker", "scripts/core/InputSetup", "scripts/combat/weapon/", "scripts/visual/Weapon",
        "scripts/visual/WorldAudio", "scripts/visual/CombatantAudio", "scripts/core/Loc.cs",
        "scripts/ui/kit/UiTheme", "scripts/ui/kit/UiFonts", "scripts/net/OnlineRobot",
        "scripts/net/HitCheck", "scripts/net/HostedMatch", "scripts/visual/VisualSmoothing", "scripts/visual/Silhouette")
# nomes de nó e de corpo (a lógica e a rede dependem deles), cabeçalhos e variáveis: nunca são texto
NUNCA = {"Authorization", "Bearer ", "X-Fleet-Key", "Content-Type", "ALCATEIA_API", "Jogador{0}", "Bot{0}", "If-None-Match", "../Bots"}

# contexto que NÃO é texto de tela: o literal vem logo depois disto
NAO = re.compile(r"""(GetNode(OrNull)?(<[^>]*>)?\(|FindChild(ren)?\(|GetNodesInGroup\(|IsInGroup\(|AddToGroup\(|HasNode\(|Has\(|Own\(|Source\(|Fire\(|Cap\(
    |Is(Action|Key)\w*\(|ActionPress\(|ActionRelease\(|Tap\(|Press\(|Mouse\(|Key\(
    |SetShaderParameter\(|SetInstanceShaderParameter\(|\.Set\(|\.Get\(|\.Call\(|CallDeferred\(|EmitSignal\(|Connect\(|HasFeature\(|HasMethod\(
    |Sfx\.\w+\(|Say\(|Play(Music)?\(|GD\.Load(<[^>]*>)?\(|ResourceLoader\.\w+\(|FileAccess\.\w+\(|DirAccess\.\w+\(|ProjectSettings\.\w+\(
    |GD\.Print\w*\(|GD\.Push\w+\(|Note\(|Check\w*\(|new\s+StringName\(|nameof\(|AddThemeStyleboxOverride\(|AddTheme\w+Override\(|GetTheme\w+\(|Has Theme
    |EndsWith\(|StartsWith\(|Contains\(|IndexOf\(|Replace\(|Split\(|Trim\w*\(|ToString\(|Parse\(|Format\(|Join\(|Hash\(
    |Animation\s*=\s*|Bus\s*=\s*|ThemeTypeVariation\s*=\s*|case\s+|==\s*|!=\s*|\[\s*|is\s+|or\s+|and\s+|Clip\(|Clips\[|Link\(|AddNode\(|ConnectNode\(|Travel\(|Start\(
    |UiKit\.Text\([^,()]*,\s*|UiKit\.Chip\([^,()]*,\s*[^,()]*,\s*|typeof\(|Shader\s*\{\s*Code\s*=\s*|Loc\.\w+\()\s*$""", re.X)
# propriedades que são nome de nó, não texto
NOME_DE_NO = re.compile(r"\bName\s*=\s*$")
ARQUIVOS_COM_NOME = ("scripts/meta/", "scripts/core/Ranks", "scripts/core/Maps", "scripts/forms/", "scripts/net/RoomDirectory", "scripts/core/RoomConfig")

def parece_texto(t):
    s = t.strip()
    if len(s) < 2 or not re.search(r"[A-Za-zÀ-ÿ]", s): return False
    if t in NUNCA or s.startswith("/") or re.fullmatch(r"[A-Z0-9_]+", s) and "_" in s: return False
    if re.search(r"\.(ttf|otf|png|jpg|wav|ogg|glb|tscn|tres|json|csv)$", s): return False
    if re.match(r"^(res|user|uid)://", s) or s.startswith(("parameters/", "%", "--", "#", "<", "[", "0x")): return False
    if re.fullmatch(r"[a-z0-9_./:\-+*%{}]+", s): return False                 # identificador, caminho, ação
    if re.fullmatch(r"[A-Za-z0-9_]+", s) and re.search(r"[a-z][A-Z]", s): return False    # CamelCase: nome de nó
    if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*(/[A-Za-z_0-9:]+)+", s): return False          # caminho de nó
    if re.fullmatch(r"[0-9a-fA-F]{6,8}", s): return False                      # cor
    if "shader_type" in s or "uniform " in s: return False
    if re.fullmatch(r"\{\d+(:[^}]*)?\}([ %x×/.,:·\-]*\{\d+(:[^}]*)?\})*[ %x×sm/.,:·\-]*", s): return False   # só números
    letras = re.sub(r"\{\d+(:[^}]*)?\}", "", s)
    if len(re.findall(r"[A-Za-zÀ-ÿ]", letras)) < 2: return False
    return True

def varrer():
    achados = {}          # texto -> onde
    trocas = {}           # arquivo -> [(início, fim, novo)]
    for pasta in PASTAS:
        for dp, _, fn in os.walk(os.path.join(RAIZ, pasta)):
            for f in sorted(fn):
                if not f.endswith(".cs"): continue
                caminho = os.path.join(dp, f)
                rel = os.path.relpath(caminho, RAIZ).replace(os.sep, "/")
                if rel.startswith(FORA) or rel.endswith("/Loc.cs"): continue
                src = open(caminho, encoding="utf-8").read()
                tk = tokens(src)
                antes = ""
                for tipo, texto, a, b in tk:
                    if tipo == "code":
                        antes = (antes + texto)[-240:]
                        continue
                    if tipo == "comment":
                        continue
                    ctx = antes
                    linha = src[src.rfind("\n", 0, a) + 1:a]
                    antes = (antes + '""')[-240:]
                    if tipo == "verb": continue
                    # o que já passa pelo Loc é texto, sem discussão
                    if tipo == "str" and re.search(r"Loc\.(T|F|Upper)\(\s*$", ctx):
                        achados.setdefault(unescape(texto[1:-1]), rel)
                        continue
                    if NAO.search(ctx): continue
                    if re.search(r"\[(Export|Signal|Tool|GlobalClass)[^\]]*$", ctx): continue
                    if re.search(r"\bconst\s+string\s+\w+\s*=\s*$", ctx) and not rel.startswith("scripts/domain/KillStory"): continue
                    if NOME_DE_NO.search(ctx) and not rel.startswith(ARQUIVOS_COM_NOME): continue
                    if re.search(r"GD\.(Print|Push)\w*\(|Debug\b|throw new|Exception\(|WallLog\(|Log\(", linha): continue
                    if re.search(r"(GD\.(Print|Push)\w*|throw new \w+|WallLog|Log)\([^;]*$", ctx): continue
                    if tipo == "str":
                        t = unescape(texto[1:-1])
                        if parece_texto(t):
                            achados.setdefault(t, rel)
                            # fora de Control (desenho na mão): passa pelo Loc
                            if re.search(r"Draw(String|StringOutline|MultilineString)\([^;]*$", ctx) or re.search(r"\b(Announce|Toast\.\w+|Title\s*=|Hint\s*=|Caption\s*=)\s*\(?\s*$", ctx) and False:
                                trocas.setdefault(caminho, []).append((a, b, f"Loc.T({texto})"))
                    else:
                        r = split_interp(texto)
                        if r is None: continue
                        fmt, args = r
                        t = unescape(fmt)
                        if not args or not parece_texto(t): continue
                        achados.setdefault(t, rel)
                        trocas.setdefault(caminho, []).append((a, b, f'Loc.F("{fmt}", {", ".join(args)})'))
    return achados, trocas

# Os textos que moram em recurso (.tres): o nome e a descrição das armas,
# das classes de lobo, dos personagens (pedido do Jhon, 21/11: "os nomes do
# mapa e descrição devem estar no i18n, o mesmo para itens, personagens").
PASTAS_TRES = ["resources", "assets/personagens"]
CAMPOS_TRES = re.compile(r'^(DisplayName|Description|Title|Subtitle|Lore|Tagline)\s*=\s*"((?:[^"\\]|\\.)*)"\s*$', re.M)

def varrer_recursos(achados):
    for pasta in PASTAS_TRES:
        for dp, _, fn in os.walk(os.path.join(RAIZ, pasta)):
            for f in sorted(fn):
                if not f.endswith(".tres"): continue
                caminho = os.path.join(dp, f)
                rel = os.path.relpath(caminho, RAIZ).replace(os.sep, "/")
                for m in CAMPOS_TRES.finditer(open(caminho, encoding="utf-8").read()):
                    t = m.group(2).replace('\\"', '"').replace("\\n", "\n")
                    if parece_texto(t): achados.setdefault(t, rel)

def ler_csv():
    linhas, cab = {}, ["texto", "pt_BR", "en", "es", "onde"]
    if os.path.exists(CSV):
        with open(CSV, encoding="utf-8", newline="") as f:
            r = csv.reader(f)
            cab = next(r)
            for row in r:
                if row: linhas[row[0]] = dict(zip(cab, row + [""] * (len(cab) - len(row))))
    return cab, linhas

def main():
    achados, trocas = varrer()
    varrer_recursos(achados)
    cab, linhas = ler_csv()
    novos = 0
    for t, onde in achados.items():
        if t not in linhas:
            linhas[t] = {"texto": t}; novos += 1
        linhas[t]["onde"] = onde
    sumidos = [t for t in linhas if t not in achados]
    for t in sumidos:
        # saiu do código: a linha só fica (marcada) se alguém já tinha traduzido ou renomeado
        if any(linhas[t].get(c) for c in cab if c not in ("texto", "onde")): linhas[t]["onde"] = "(não está mais no código)"
        else: del linhas[t]
    os.makedirs(os.path.dirname(CSV), exist_ok=True)
    with open(CSV, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(cab)
        for t in sorted(linhas, key=lambda k: (linhas[k].get("onde", ""), k.lower())):
            w.writerow([linhas[t].get(c, "") if c != "texto" else t for c in cab])
    print(f"{len(linhas)} textos em {os.path.relpath(CSV, RAIZ)} ({novos} novos, {len(sumidos)} que saíram do código)")

    if "--ligar" in sys.argv:
        n = 0
        for caminho, lista in trocas.items():
            src = open(caminho, encoding="utf-8").read()
            for a, b, novo in sorted(lista, reverse=True):
                src = src[:a] + novo + src[b:]; n += 1
            open(caminho, "w", encoding="utf-8").write(src)
        print(f"{n} textos montados em código passaram a usar o Loc, em {len(trocas)} arquivos")
    else:
        print(f"({sum(len(v) for v in trocas.values())} textos montados em código; --ligar os passa pelo Loc)")

if __name__ == "__main__":
    main()
