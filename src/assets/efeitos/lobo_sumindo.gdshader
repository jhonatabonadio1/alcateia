// O lobo morto sumindo: a cor, transparente por igual (Jhon, 26/11). Desenha por
// cima da profundidade que lobo_profundidade.gdshader escreveu antes (o mesmo
// corpo): só a pele de fora passa, e o de dentro não aparece. Montado pelo
// CorpseFade.
shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx;
#define LOBO_SUMINDO
#include "res://src/assets/efeitos/lobo.gdshaderinc"
