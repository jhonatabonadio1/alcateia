// O corpo do lobo (pedido do Jhon, 18/11: "mais AAA"): o material do
// modelo (cor, normal, oclusão/rugosidade/metal) mais três coisas que o
// material padrão não faz:
//
// - o pelo balança: os vértices das pontas dos tufos (cor de vértice R,
//   gravada por tools/lobo/polir.py) andam com um sopro que atravessa o
//   corpo e com o empurrão do movimento (fur_push: correu, o pelo deita
//   para trás; freou, vai para a frente);
// - o neon (olhos, braceletes, peitoral, joelheiras, costas) é emissão de
//   verdade, respira devagar e acende mais com o poder em uso (neon_boost:
//   rugido e especial);
// - um brilho de contorno no pelo (cor de vértice G), que é o que dá
//   volume ao pelo contra a luz.
//
// Quem monta e alimenta: scripts/visual/wolf/WolfSkin.cs e WolfFx.cs.
shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx;

#include "res://src/assets/efeitos/lobo.gdshaderinc"
