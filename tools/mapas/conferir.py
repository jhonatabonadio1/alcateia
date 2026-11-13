#!/usr/bin/env python3
"""
Procura defeitos de desenho nos mapas, pela geometria que os geradores
montam (sem abrir o jogo):

    python3 tools/mapas/conferir.py            # os quatro
    python3 tools/mapas/conferir.py polar      # um só
    python3 tools/mapas/conferir.py --cru      # antes do acerto automático

Sem `--cru`, confere o mapa como ele é gravado: depois de
`Mapa.acertar()`, que já desempata as faces e assenta os letreiros. O
que sobrar aqui é o que tem de ser arrumado à mão no gerador.

O que procura:

1. **Faces brigando** (pisca-pisca): duas caixas visíveis, de materiais
   diferentes, com uma face no mesmo plano, viradas para o mesmo lado e
   sobrepostas. De longe as duas disputam o mesmo ponto e a superfície
   pisca.
2. **Letreiro fora da parede**: placa que não tem parede atrás em toda a
   extensão (sobra no ar), ou que ficou enterrada dentro de uma caixa.
3. **Nascimento ruim**: ponto de nascer dentro de uma caixa sólida, ou
   sem chão embaixo.
4. **Objeto no ar ou afundado**: modelo pronto cuja base não encosta em
   nada (mais de 6 cm de folga) — só os que ficam no nível declarado.

Sai com 1 se achar algo. Só olha caixas retas (sem giro): rampas e
formas livres ficam de fora.
"""
import importlib
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(os.path.dirname(AQUI)))

MAPAS = ("hangar", "metro", "vila", "polar")
EPS = 0.004          # mesma face: até 4 mm
AREA_MIN = 0.03      # sobreposição que se vê, m²


def proporcao(imagem):
    """Altura sobre largura da imagem do letreiro."""
    from PIL import Image
    with Image.open(os.path.join(AQUI, "..", "..", "assets", "mapas", "letreiros", imagem + ".png")) as img:
        return img.height / img.width


def caixas(m, so_visiveis=False, so_solidas=False):
    for indice, (g, nome, c, tam, mat, rot, colide, forma) in enumerate(m.pecas):
        if rot is not None or not forma.startswith("caixa"):
            continue
        oculta = forma.endswith("_oculta")
        if so_visiveis and oculta:
            continue
        if so_solidas and not colide:
            continue
        lo = tuple(c[i] - tam[i] / 2 for i in range(3))
        hi = tuple(c[i] + tam[i] / 2 for i in range(3))
        yield dict(nome=f"{g}/{nome}", lo=lo, hi=hi, mat=mat, indice=indice)


def brigas(m):
    cx = list(caixas(m, so_visiveis=True))
    achadas = []
    for eixo in range(3):
        u, v = [i for i in range(3) if i != eixo]
        for lado in ("lo", "hi"):
            ordem = sorted(cx, key=lambda b: b[lado][eixo])
            for i, a in enumerate(ordem):
                for b in ordem[i + 1:]:
                    if b[lado][eixo] - a[lado][eixo] > EPS:
                        break
                    if a["mat"] == b["mat"]:
                        continue
                    du = min(a["hi"][u], b["hi"][u]) - max(a["lo"][u], b["lo"][u])
                    dv = min(a["hi"][v], b["hi"][v]) - max(a["lo"][v], b["lo"][v])
                    if du <= 0.02 or dv <= 0.02 or du * dv < AREA_MIN:
                        continue
                    # Por baixo do chão ninguém vê.
                    if eixo == 1 and lado == "lo" and a["lo"][1] <= 0.011:
                        continue
                    # Coberta por uma terceira peça: também não se vê.
                    meio = [0.0, 0.0, 0.0]
                    meio[u] = (max(a["lo"][u], b["lo"][u]) + min(a["hi"][u], b["hi"][u])) / 2
                    meio[v] = (max(a["lo"][v], b["lo"][v]) + min(a["hi"][v], b["hi"][v])) / 2
                    meio[eixo] = a[lado][eixo] + (0.01 if lado == "hi" else -0.01)
                    if any(c is not a and c is not b and all(c["lo"][i] <= meio[i] <= c["hi"][i] for i in range(3)) for c in cx):
                        continue
                    achadas.append((du * dv, "xyz"[eixo], lado, a, b))
    return sorted(achadas, key=lambda t: -t[0])


def letreiros(m):
    solidas = list(caixas(m, so_visiveis=True))
    achados = []
    for (g, nome, imagem, pos, largura, base, aceso) in m.letreiros:
        altura = largura * proporcao(imagem)
        direita = (base[0][0], base[1][0], base[2][0])
        cima = (base[0][1], base[1][1], base[2][1])
        frente = (base[0][2], base[1][2], base[2][2])
        sem_apoio, enterrado = 0, 0
        amostras = [(a, b) for a in (-0.46, 0, 0.46) for b in (-0.42, 0, 0.42)]
        for (a, b) in amostras:
            p = tuple(pos[i] + direita[i] * a * largura + cima[i] * b * altura for i in range(3))
            atras = tuple(p[i] - frente[i] * 0.045 for i in range(3))
            adiante = tuple(p[i] + frente[i] * 0.01 for i in range(3))
            dentro = lambda q: any(all(c["lo"][i] - 1e-4 <= q[i] <= c["hi"][i] + 1e-4 for i in range(3)) for c in solidas)
            if not dentro(atras):
                sem_apoio += 1
            # Coisa grande na frente (o farol em cima do anel pintado no
            # piso) é só uma coisa na frente; enterrado é ficar a menos de
            # um palmo de sair.
            longe = tuple(p[i] + frente[i] * 0.13 for i in range(3))
            if dentro(adiante) and not dentro(longe):
                enterrado += 1
        # Sem caixa nenhuma por perto, o letreiro está numa forma livre
        # (trem, tanque): daqui não dá para julgar.
        perto = any(all(c["lo"][i] - 0.3 <= pos[i] <= c["hi"][i] + 0.3 for i in range(3)) for c in solidas)
        if not perto:
            continue
        # Placa maior que a peça em que está (faixa numa borda): sobrar é o esperado.
        if sem_apoio == len(amostras) or enterrado:
            achados.append((f"{g}/{nome}", imagem, pos, sem_apoio, enterrado, len(amostras)))
    return achados


def nascimentos(m):
    solidas = list(caixas(m, so_solidas=True))
    achados = []
    for time, pontos in m.spawns.items():
        for (x, y, z) in pontos:
            dentro = [c["nome"] for c in solidas
                      if c["lo"][0] < x < c["hi"][0] and c["lo"][2] < z < c["hi"][2] and c["lo"][1] < y + 0.9 and c["hi"][1] > y - 0.7]
            # Só a parte de cima da cápsula dentro de algo já prende.
            chao = [c["hi"][1] for c in solidas if c["lo"][0] <= x <= c["hi"][0] and c["lo"][2] <= z <= c["hi"][2] and c["hi"][1] <= y - 0.7]
            if dentro:
                achados.append((time, (x, y, z), "dentro de " + ", ".join(dentro[:3])))
            elif not chao:
                achados.append((time, (x, y, z), "sem chão embaixo"))
            elif y - 0.9 - max(chao) > 1.5:
                achados.append((time, (x, y, z), f"nasce {y - 0.9 - max(chao):.1f} m acima do chão"))
    return achados


def objetos(m):
    from tools.mapas import pecas
    apoios = list(caixas(m))
    # Objeto empilhado em outro: o de baixo também é apoio.
    for (g, nome, arquivo, pos, yaw, escala) in m.modelos:
        w, h, d = (v * escala for v in pecas.OBJETOS.get(arquivo, (0.3, 0.3, 0.3)))
        r = max(w, d) / 2
        apoios.append(dict(nome=f"{g}/{nome}Colisao", lo=(pos[0] - r, pos[1], pos[2] - r), hi=(pos[0] + r, pos[1] + h, pos[2] + r)))
    achados = []
    for (g, nome, arquivo, pos, yaw, escala) in m.modelos:
        x, y, z = pos
        topos = [c["hi"][1] for c in apoios if c["nome"] != f"{g}/{nome}Colisao"
                 and c["lo"][0] - 0.05 <= x <= c["hi"][0] + 0.05 and c["lo"][2] - 0.05 <= z <= c["hi"][2] + 0.05
                 and c["hi"][1] <= y + 0.08]
        if not topos:
            continue          # sobre rampa, terreno ou forma livre: não dá para saber daqui
        folga = y - max(topos)
        if folga > 0.06 or folga < -0.06:
            achados.append((f"{g}/{nome}", arquivo, pos, folga))
    return achados


def main():
    quais = [a for a in sys.argv[1:] if a in MAPAS] or MAPAS
    total = 0
    for ident in quais:
        m = importlib.import_module(f"tools.mapas.{ident}").montar()
        if "--cru" not in sys.argv:
            m.acertar()
        b, l, n, o = brigas(m), letreiros(m), nascimentos(m), objetos(m)
        print(f"== {m.nome}: {len(m.pecas)} peças, {len(m.letreiros)} letreiros, {len(m.modelos)} objetos")
        for (area, eixo, lado, a, c) in b:
            plano = a[lado]["xyz".index(eixo)]
            print(f"  faces brigando ({area:.2f} m², {eixo} = {plano:.3f}): {a['nome']} [{a['mat']}] × {c['nome']} [{c['mat']}]")
        for (nome, imagem, pos, sem, ent, de) in l:
            print(f"  letreiro {nome} ({imagem}) em {tuple(round(v, 2) for v in pos)}: "
                  + (f"{sem}/{de} pontos sem parede atrás " if sem else "") + (f"{ent}/{de} pontos enterrados" if ent else ""))
        for (time, pos, que) in n:
            print(f"  nascimento do time {time} em {pos}: {que}")
        for (nome, arquivo, pos, folga) in o:
            print(f"  objeto {nome} ({arquivo}) em {tuple(round(v, 2) for v in pos)}: {'no ar' if folga > 0 else 'afundado'} {abs(folga) * 100:.0f} cm")
        achou = len(b) + len(l) + len(n) + len(o)
        if not achou:
            print("  nada")
        total += achou
    sys.exit(1 if total else 0)


if __name__ == "__main__":
    main()
