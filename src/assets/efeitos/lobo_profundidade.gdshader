// O lobo morto sumindo: a profundidade, sem cor, antes da cor (CorpseFade).
// Transparente (ALPHA 0) para cair na mesma fila da cor, mas escrevendo a
// profundidade (depth_draw_always) do pelo que balança como o de verdade.
shader_type spatial;
render_mode cull_disabled, unshaded, depth_draw_always, shadows_disabled, ambient_light_disabled, fog_disabled;
#define LOBO_PROFUNDIDADE
#include "res://src/assets/efeitos/lobo.gdshaderinc"
