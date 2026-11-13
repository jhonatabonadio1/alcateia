# Mapas

Quatro mapas, todos de equipe, registrados em `scripts/meta/Maps.cs`:

| Mapa | Tamanho | Tema | Gerador |
|---|---|---|---|
| Hangar | 84 × 48 m | Hangar de caixa cinza, heliponto no centro | `tools/gerar_hangar.py` |
| Metrô | 48 × 30 m (pequeno) | Estação fechada com um trem parado; luz fria de lâmpada | `tools/mapas/metro.py` |
| Vila | 72 × 52 m (médio) | Vila de deserto no fim da tarde: praça, feira, terraços, vielas | `tools/mapas/vila.py` |
| Estação Polar | 110 × 70 m (grande) | Base de pesquisa na neve, ao crepúsculo: garagem, alojamentos, passarelas | `tools/mapas/polar.py` |

A sala escolhe o mapa; a cena da partida (`scenes/main.tscn`) tem um nó
`Map` com o script `MapSlot`, que carrega a cena do mapa escolhido antes
de os jogadores nascerem. Para abrir um mapa direto no editor, ponha o
id dele em **Map Id** no nó `Map`.

## Como um mapa é feito

Por script, não à mão no editor:

```sh
pip install numpy pillow          # uma vez
python3 tools/gerar_texturas.py   # as texturas (assets/textures/pbr)
python3 tools/gerar_mapas.py      # todos os mapas; ou: gerar_mapas.py vila
```

Cada mapa é um arquivo em `tools/mapas/` que usa o kit
(`tools/mapas/kit.py`): `caixa`, `cilindro`, `rampa`, `parapeito`, `luz`,
`placa`, `spawn`, `rota`, mais o céu, a neblina e o sol do mapa. O
gerador escreve `scenes/maps/<id>.tscn` e a miniatura
`assets/ui/mapas/<id>.png`.

**Rodar o gerador sobrescreve o `.tscn`.** Se você editar um mapa no
Godot, o `.tscn` passa a ser a fonte.

### As texturas

`tools/gerar_texturas.py` desenha 16 materiais por código — concreto,
reboco, tijolo, bloco, asfalto, azulejo, piso, metal pintado, metal
ondulado, ferrugem, madeira, areia, neve, chapa xadrez, rocha e pedra de
calçada —, cada um com cor, relevo (normal) e aspereza, 512 px, sem
emenda. Nada vem de foto nem de pacote de terceiros: não há licença para
cumprir. Os tons neutros (reboco, metais) são tingidos pela cor do
material do mapa.

Para trocar por texturas fotográficas (Poly Haven, ambientCG — CC0),
salve os três arquivos com o mesmo nome em `assets/textures/pbr/`
(`<nome>_cor.jpg`, `<nome>_normal.jpg`, `<nome>_asp.jpg`) e os mapas
passam a usá-las sem mudar nada.

### O que todo mapa precisa ter

- **Convenção dos times:** o azul nasce em −X e olha para +X.
- **Oito pontos de nascimento por time**, sob cobertura.
- **Rotas de ronda** para os bots, com o Y do piso de cada ponto.
- **Rampa em todo desnível que o bot precisa vencer** (ele não sobe
  degrau maior que 0,5 m).
- **Alturas presas à física:** caixote 0,8 m (o humano sobe); 2,2 m (só
  o lobo, a partir do caixote); terraço a 5 m (rampa, ou o lobo
  ricocheteando num beco de 3 m).
- **As quatro perguntas** (o teste de mapa do `docs/DESIGN.md`), que
  cada arquivo responde no comentário do topo: um caminho que só o lobo
  faz, um lugar onde o humano vê o lobo chegando, um corredor que o
  humano deve evitar, e de onde cair em cima de quem não olha.

### Mapa novo

1. Copie um arquivo de `tools/mapas/`, mude o id e monte o seu.
2. Registre em `tools/gerar_mapas.py` e em `scripts/meta/Maps.cs`.
3. Acrescente um teste em `tests/regression/MapTests.cs` (três linhas).
4. `python3 tools/gerar_mapas.py <id>` e `tools/testar.sh Mapa`.

## Testes

`MapaHangar`, `MapaMetro`, `MapaVila` e `MapaPolar`: cada mapa carrega
na partida, os dois times nascem, ninguém cai do mapa, os bots saem da
base pela malha de navegação e trocam tiro.
