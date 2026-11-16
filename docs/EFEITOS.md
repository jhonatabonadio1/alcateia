# Efeitos do lobo

O que está no jogo do guia de efeitos visuais (16/11/2026), onde fica e
como mexer. O guia descrevia cinco efeitos; quatro estão prontos, e o
que mudou em relação a ele está no fim.

| Efeito | Quando aparece | Onde |
|---|---|---|
| Fumaça da transformação | virando lobo e voltando a humano; o corpo troca na metade dela | `scripts/visual/fx/TransformSmoke.cs`, `assets/efeitos/fumaca.png` |
| Rastro das garras | na garrada, no salto (no ar) e no bote, na mão que bate | `scripts/visual/fx/ClawTrail.cs`, `assets/efeitos/rastro.gdshader` |
| Aura | rugindo (R) e carregando o salto | `assets/efeitos/aura.gdshader` |
| Ar de velocidade | de quatro, no salto e no bote (acima de 60% da velocidade de quatro); some ao frear | riscos: partículas em `WolfFx.cs`; brilho nas bordas: `assets/efeitos/vento.gdshader` |
| Câmera abre com a velocidade | para o dono do lobo, junto com o ar (+8°) | `WolfFx.cs`, `AirFovKick` |

Quem liga e desliga tudo é o `scripts/visual/fx/WolfFx.cs`, um por
corpo (jogador e bot), criado pelo `WolfVisual`. O teste é o
`EfeitosDoLobo` (`tests/regression/WolfFxTests.cs`); com
`-- --fotos=pasta` ele fotografa cada efeito.

## A cor é do personagem

Cada personagem tem a sua cor de efeito: rastro, aura e o clarão da
fumaça. Fica no `personagem.tres`, campo **Effect Color** (grupo
Efeitos). A Eve é verde.

Sem personagem, ou com Effect Color transparente (alfa 0), vale a cor do
time: azul `(0.35, 0.75, 1.0)` e laranja `(1.0, 0.5, 0.15)`
(`WolfFx.BlueEffect`, `WolfFx.OrangeEffect`).

## Regras que o código segue

- **Nada vai pela rede.** Cada máquina desenha os efeitos de cada lobo a
  partir do estado que já recebe (forma, golpe, velocidade): o mesmo
  que a animação usa.
- **Um lobo não afeta o outro.** O que muda por lobo (cor, quanto a aura
  e o ar aparecem) é `instance uniform` no shader. Os dois shaders que
  vão na mesma malha (aura e vento) usam **índices fixos**
  (`instance_index(0)` a `(3)`): sem eles, os dois disputam as mesmas
  posições e o lobo inteiro fica com a cor errada. Shader novo na mesma
  malha continua a partir do 4.
- **Sem custo parado.** Com a aura e o ar apagados, o material sai de
  cima do lobo.
- **Qualidade.** Na Baixa: menos fumaça e riscos, rastro mais curto, sem
  o ar de velocidade. Média e Alta crescem daí
  (`WolfFx.ApplyQuality`).
- **Sem travar no primeiro uso.** O material da aura vai ao lobo por
  alguns quadros quando a partida começa, para o shader compilar ali.
- **Servidor dedicado** não cria efeito nenhum.

## A troca do corpo

O corpo que aparece troca na **metade** da transformação, dentro da
fumaça: `FormController.ShowsWolf` e o sinal `BodySwapped`. É só visual.
O estado do jogo (vida, dano, arma) continua mudando no começo da
transformação, como antes: `FormController.IsWolf`.

## O que ficou diferente do guia

- **Ar de velocidade (6A):** riscos desenhados sobre a pele viravam
  manchas brancas, porque o corpo curvo estica qualquer padrão
  projetado. Ficou como o 6B do guia: riscos em partícula passando em
  volta do corpo, mais um brilho fraco nas bordas.
- **Rastro das garras:** em vez de uma trilha da animação para cada
  garra, as duas mãos ficam ligadas na janela do golpe e o rastro só
  aparece onde a garra anda rápido. A mão que bate risca e a outra não;
  a alternância sai sozinha, e serve para todo lobo sem mexer nos
  clipes. Mistura em vez de somar, para aparecer também contra fundo
  claro.
- **Emissão (olhos, garras, veias):** depende de uma textura de
  emissão pintada no UV de cada lobo. Nenhum lobo tem ainda (a Eve já
  vem com as garras verdes acesas no próprio material). Quando um tiver,
  basta ligar Emission no material dele.
- **Dissolve (seção 8):** não feito. A fumaça já esconde a troca, e o
  dissolve pede trocar o material de cada modelo por um shader próprio.
  Fica para quando os modelos finais chegarem.
