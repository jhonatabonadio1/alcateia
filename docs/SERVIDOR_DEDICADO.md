# O servidor de partida exportado (servidor dedicado)

Pedido do Jhon (21/11): o servidor de partida como programa à parte, do
jeito dos jogos grandes — o mesmo código do jogo, mas sem gráficos, sem
som e sem interface. No Godot isso não são dois executáveis compilados
separados (como o alvo `Server` do Unreal): é o mesmo código C#, o
executável de exportação (sem editor) e um pacote de recursos próprio, em
que toda a arte vira casca.

## Como sai

`servidor/partida.Dockerfile`, em duas etapas:

1. **exporta**: o editor do Godot .NET (só nesta etapa) prepara a cópia
   (`tools/servidor/preparar_exportacao.py`), importa e exporta o preset
   **"Servidor (Linux)"** de `export_presets.cfg`;
2. **roda**: só `WolfLeagueServidor` (o modelo de exportação),
   `WolfLeagueServidor.pck` e `data_WolfLeague_linuxbsd_*` (o C# em Release e
   o .NET dele), em cima de `mcr.microsoft.com/dotnet/runtime-deps:8.0`,
   como usuário sem privilégio.

O preset "Servidor (Linux)":

- **Exportar como servidor dedicado** (`dedicated_server=true`): o projeto
  exportado ganha a marca `dedicated_server` e roda sem tela sozinho;
- tudo em **Strip Visuals** (`"res://": "strip"`): textura vira
  `PlaceholderTexture` (guarda só o tamanho) e material vira
  `PlaceholderMaterial`. Malhas, formas de colisão, animações, cenas e
  recursos de jogo (armas, raças) ficam como são;
- fora do pacote, além do que o cliente já deixa de fora: `assets/pacotes`
  (o material de origem).

Com a marca `dedicated_server`, o jogo abre `scenes/servidor.tscn` (um nó
vazio, `run/main_scene.dedicated_server` no `project.godot`) em vez do
menu, e `Inicio` o troca pelo `MatchServer` — o mesmo que
`-- --servidor` faz no editor.

## A importação leve

O servidor não desenha nada, e a casca de uma textura só guarda o tamanho.
Importar a arte em qualidade de jogo (texturas comprimidas para placa de
vídeo, níveis de detalhe das malhas) era o que levava ~10 minutos no
construtor. `preparar_exportacao.py` muda os `.import` da cópia do build
(nunca do projeto): texturas a 64 px sem mipmaps; modelos sem LOD, sem
malha de sombra e sem UV de luz assada. A colisão que o importador gera
sai das malhas, que continuam iguais.

## Conferido (21/11)

Exportado aqui (Linux x86_64) e rodado contra um servidor falso que
entrega uma partida de cada mapa:

| | importação | pacote | memória com 4 partidas |
|---|---|---|---|
| antes (editor + projeto inteiro) | ~10 min | o projeto (~1,3 GB importado) | 355 MB |
| servidor dedicado | ~2 min | 243 MB (pck 103 MB + executável 74 MB + .NET 66 MB) | 228 MB |

A colisão de cada mapa no servidor exportado é a mesma do jogo rodado no
editor com a mesma importação: o servidor escreve, ao abrir cada partida,
`Mundo <mapa>: N formas de colisão, T triângulos, P pontos de nascimento`,
e os números bateram em todos os mapas. Rodou como usuário sem privilégio.

**Não conferido aqui**: a imagem Docker em si (não há Docker nesta máquina)
e uma partida com jogadores de verdade contra o servidor exportado (o
teste de ponta a ponta, `tools/testar_online.sh`, usa o jogo no editor).
A primeira partida no Fly é esse teste.

## Desenvolvimento

Nada muda: `godot --headless --path . -- --servidor` continua sendo o
servidor de partida no editor (é o que o `tools/testar_online.sh` usa). Para
exportar à mão, com o Godot .NET e os modelos de exportação instalados:

```
python3 tools/servidor/preparar_exportacao.py   # só numa CÓPIA do projeto
godot --headless --path . --export-release "Servidor (Linux)" build/servidor/WolfLeagueServidor
```

Atenção: num projeto que vai continuar em uso, não rode o
`preparar_exportacao.py` — ele estraga a importação da arte para o jogo.
O preset pode ser exportado sem ele (só demora mais).

## Se algo do servidor precisar de imagem de verdade

Se um dia o servidor ler os pixels de uma textura (um mapa de altura, uma
máscara de colisão), essa textura tem de ficar como **Keep** no preset
(`customized_files`), senão ele lê uma casca vazia. Hoje nada lê.
